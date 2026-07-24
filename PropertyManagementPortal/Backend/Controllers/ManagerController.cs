using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PropertyManagementPortal.Data;
using PropertyManagementPortal.Models;
using PropertyManagementPortal.ViewModels.Manager;
 
namespace PropertyManagementPortal.Controllers
{
    [Authorize(Roles = "PropertyManager")]
    public class ManagerController : AppControllerBase
    {
        private const int PageSize = 10;

        // Fronts the Lambda that mints presigned S3 URLs for the tenant's maintenance
        // photo shown in the detail modal — the manager only ever views, never uploads.
        private readonly IConfiguration _config;

        public ManagerController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IConfiguration config)
            : base(db, userManager)
        {
            _config = config;
        }
 
        // ── SCOPING HELPERS ──────────────────────────────────────────────────
        // Every Manager action is limited to the properties assigned to the
        // logged-in manager. These two helpers are the single source of truth.
        private async Task<List<int>> GetManagedPropertyIdsAsync()
        {
            var userId = _userManager.GetUserId(User);
            return await _db.Properties
                .Where(p => p.ManagerId == userId)
                .Select(p => p.PropertyId)
                .ToListAsync();
        }
 
        // selectedId marks the active filter so the dropdown keeps its selection
        // after a filter round-trip (asp-items honours SelectListItem.Selected).
        private async Task<List<SelectListItem>> GetPropertyOptionsAsync(int? selectedId = null)
        {
            var userId = _userManager.GetUserId(User);
            return await _db.Properties
                .Where(p => p.ManagerId == userId)
                .OrderBy(p => p.Name)
                .Select(p => new SelectListItem
                {
                    Value = p.PropertyId.ToString(),
                    Text = p.Name,
                    Selected = selectedId.HasValue && p.PropertyId == selectedId.Value
                })
                .ToListAsync();
        }
 
        // Clamp a requested page to the valid range for a given item count.
        private static int NormalizePage(int page, int totalItems)
        {
            var totalPages = (int)Math.Ceiling((double)totalItems / PageSize);
            if (page < 1) return 1;
            if (totalPages > 0 && page > totalPages) return totalPages;
            return page;
        }
        // ─────────────────────────────────────────────────────────────────────
 
        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            var propertyIds = await GetManagedPropertyIdsAsync();
 
            var units = await _db.Units
                .Where(u => propertyIds.Contains(u.PropertyId))
                .ToListAsync();
 
            var unitIds = units.Select(u => u.UnitId).ToList();
 
            var pendingApplications = await _db.Tenancies
                .CountAsync(t => unitIds.Contains(t.UnitId) && t.Status == "Pending");
 
            // Overdue is derived, not stored: a Pending payment past its due date counts
            // as overdue. Mirrors the same derive-on-read logic used on the Payments page.
            var todayForPayments = TodayLocal();
            var pendingDueDates = await _db.Payments
                .Where(p => unitIds.Contains(p.Tenancy.UnitId) && p.Status == "Pending")
                .Select(p => p.DueDate)
                .ToListAsync();
            var overduePayments = pendingDueDates.Count(d => d.Date < todayForPayments);
            var pendingPayments = pendingDueDates.Count - overduePayments;
 
            var unassignedMaintenance = await _db.MaintenanceRequests
                .CountAsync(m => unitIds.Contains(m.UnitId) && m.AssignedStaffId == null);
 
            var vm = new ManagerDashboardViewModel
            {
                ManagerName = user!.FullName,
                TotalProperties = propertyIds.Count,
                TotalUnits = units.Count,
                OccupiedUnits = units.Count(u => u.Status == "Occupied"),
                VacantUnits = units.Count(u => u.Status == "Vacant"),
                PendingApplications = pendingApplications,
                PendingPayments = pendingPayments,
                OverduePayments = overduePayments,
                UnassignedMaintenance = unassignedMaintenance
            };
 
            return View(vm);
        }
 
        // ── MANAGE UNITS ─────────────────────────────────────────────────────
 
        public async Task<IActionResult> Units(int? propertyId, string? status, string? search, int page = 1)
        {
            var propertyIds = await GetManagedPropertyIdsAsync();
 
            var query = _db.Units.Where(u => propertyIds.Contains(u.PropertyId));
 
            if (propertyId.HasValue)
                query = query.Where(u => u.PropertyId == propertyId.Value);
 
            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(u => u.Status == status);
 
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(u => u.UnitNumber.Contains(search));
 
            var allRows = await query
                .OrderBy(u => u.Property.Name).ThenBy(u => u.UnitNumber)
                .Select(u => new UnitRowViewModel
                {
                    UnitId = u.UnitId,
                    PropertyName = u.Property.Name,
                    UnitNumber = u.UnitNumber,
                    Type = u.Type,
                    RentAmount = u.RentAmount,
                    Status = u.Status,
                    Floor = u.Floor
                })
                .ToListAsync();
 
            page = NormalizePage(page, allRows.Count);
            var pageRows = allRows.Skip((page - 1) * PageSize).Take(PageSize).ToList();
 
            var vm = new UnitListViewModel
            {
                Units = pageRows,
                PropertyFilter = propertyId,
                StatusFilter = status,
                SearchTerm = search,
                PropertyOptions = await GetPropertyOptionsAsync(propertyId),
                CurrentPage = page,
                PageSize = PageSize,
                TotalItems = allRows.Count
            };
 
            return View(vm);
        }
 
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddUnit(UnitFormViewModel vm)
        {
            var propertyIds = await GetManagedPropertyIdsAsync();
 
            // Ownership guard: can only add to a property you manage.
            if (!propertyIds.Contains(vm.PropertyId))
            {
                TempData["Error"] = "You can only add units to your own properties.";
                return RedirectToAction(nameof(Units));
            }
 
            if (!ModelState.IsValid)
            {
                // Surface the real field errors — the Add modal posts from the list
                // page, so there is no validation summary on screen to render into.
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .ToList();
 
                TempData["Error"] = errors.Count > 0
                    ? string.Join(" ", errors)
                    : "Please check the unit details and try again.";
                return RedirectToAction(nameof(Units));
            }
 
            // Reject a unit number that already exists inside the same property.
            var duplicate = await _db.Units.AnyAsync(u =>
                u.PropertyId == vm.PropertyId && u.UnitNumber == vm.UnitNumber);
 
            if (duplicate)
            {
                TempData["Error"] = $"Unit {vm.UnitNumber} already exists in that property.";
                return RedirectToAction(nameof(Units));
            }
 
            _db.Units.Add(new Unit
            {
                PropertyId = vm.PropertyId,
                UnitNumber = vm.UnitNumber,
                Type = vm.Type,
                RentAmount = vm.RentAmount,
                Status = vm.Status,
                Floor = vm.Floor,
                Description = vm.Description
            });
            await _db.SaveChangesAsync();
 
            TempData["Success"] = $"Unit {vm.UnitNumber} added.";
            return RedirectToAction(nameof(Units));
        }
 
        [HttpGet]
        public async Task<IActionResult> EditUnit(int id)
        {
            var propertyIds = await GetManagedPropertyIdsAsync();
 
            var unit = await _db.Units
                .Include(u => u.Property)
                .FirstOrDefaultAsync(u => u.UnitId == id && propertyIds.Contains(u.PropertyId));
 
            if (unit == null)
            {
                TempData["Error"] = "Unit not found or not in your properties.";
                return RedirectToAction(nameof(Units));
            }
 
            var vm = new UnitFormViewModel
            {
                UnitId = unit.UnitId,
                PropertyId = unit.PropertyId,
                PropertyName = unit.Property.Name,
                UnitNumber = unit.UnitNumber,
                Type = unit.Type,
                RentAmount = unit.RentAmount,
                Status = unit.Status,
                Floor = unit.Floor,
                Description = unit.Description
            };
 
            return View(vm);
        }
 
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUnit(UnitFormViewModel vm)
        {
            var propertyIds = await GetManagedPropertyIdsAsync();
            // Re-fetch and re-check ownership before touching anything.
            var unit = await _db.Units
                .FirstOrDefaultAsync(u => u.UnitId == vm.UnitId && propertyIds.Contains(u.PropertyId));
 
            if (unit == null)
            {
                TempData["Error"] = "Unit not found or not in your properties.";
                return RedirectToAction(nameof(Units));
            }
 
            if (!ModelState.IsValid)
            {
                vm.PropertyName = (await _db.Properties.FindAsync(unit.PropertyId))?.Name;
                return View(vm);
            }
 
            // Property is intentionally NOT reassigned here — a unit stays in its property.
            unit.UnitNumber = vm.UnitNumber;
            unit.Type = vm.Type;
            unit.RentAmount = vm.RentAmount;
            unit.Status = vm.Status;
            unit.Floor = vm.Floor;
            unit.Description = vm.Description;
            await _db.SaveChangesAsync();
 
            TempData["Success"] = $"Unit {unit.UnitNumber} updated.";
            return RedirectToAction(nameof(Units));
        }
 
        // ── TENANT APPLICATIONS ──────────────────────────────────────────────
 
        public async Task<IActionResult> Applications(string? status, int page = 1)
        {
            var propertyIds = await GetManagedPropertyIdsAsync();
 
            var query = _db.Tenancies.Where(t => propertyIds.Contains(t.Unit.PropertyId));
 
            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(t => t.Status == status);
 
            var allRows = await query
                .Select(t => new ApplicationRowViewModel
                {
                    TenancyId = t.TenancyId,
                    TenantName = t.Tenant.FullName,
                    TenantEmail = t.Tenant.Email!,
                    PropertyName = t.Unit.Property.Name,
                    UnitNumber = t.Unit.UnitNumber,
                    StartDate = t.StartDate,
                    EndDate = t.EndDate,
                    Status = t.Status,
                    CreatedAt = t.CreatedAt,
                    Notes = t.Notes
                })
                .ToListAsync();
 
            // Show pending first so the actionable ones are on top.
            allRows = allRows
                .OrderBy(r => r.Status == "Pending" ? 0 : 1)
                .ThenByDescending(r => r.CreatedAt)
                .ToList();
 
            var pendingCount = allRows.Count(r => r.Status == "Pending");
 
            page = NormalizePage(page, allRows.Count);
            var pageRows = allRows.Skip((page - 1) * PageSize).Take(PageSize).ToList();
 
            var vm = new ApplicationListViewModel
            {
                Applications = pageRows,
                StatusFilter = status,
                PendingCount = pendingCount,
                CurrentPage = page,
                PageSize = PageSize,
                TotalItems = allRows.Count
            };
 
            return View(vm);
        }
 
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveApplication(int id)
        {
            var propertyIds = await GetManagedPropertyIdsAsync();
 
            var tenancy = await _db.Tenancies
                .Include(t => t.Unit)
                .FirstOrDefaultAsync(t => t.TenancyId == id && propertyIds.Contains(t.Unit.PropertyId));
 
            if (tenancy == null)
            {
                TempData["Error"] = "Application not found or not in your properties.";
                return RedirectToAction(nameof(Applications));
            }
 
            if (tenancy.Status != "Pending")
            {
                TempData["Error"] = "Only pending applications can be approved.";
                return RedirectToAction(nameof(Applications));
            }
 
            // Approve this application and occupy the unit.
            tenancy.Status = "Approved";
            tenancy.Unit.Status = "Occupied";
 
            // Auto-reject every other pending application on the same unit.
            var others = await _db.Tenancies
                .Where(t => t.UnitId == tenancy.UnitId
                         && t.TenancyId != tenancy.TenancyId
                         && t.Status == "Pending")
                .ToListAsync();
 
            foreach (var other in others)
            {
                other.Status = "Rejected";
                other.Notes = "Automatically declined — the unit was assigned to another applicant.";
            }
 
            // Generate the monthly rent schedule for the whole lease period.
            var rent = tenancy.Unit.RentAmount;
            var scheduled = 0;
            DateTime firstDue = default;

            if (tenancy.EndDate >= tenancy.StartDate)
            {
                // DateTimeKind.Utc is required for PostgreSQL timestamptz.
                var startDate = DateTime.SpecifyKind(tenancy.StartDate.Date, DateTimeKind.Utc);

                // First payment is due 7 days after the start date; every following month
                // is due on the start date's day-of-month.
                firstDue = startDate.AddDays(7);

                // Each due date is derived from startDate rather than from the previous
                // iteration — otherwise a 31st-of-month start drifts to the 28th in
                // February and never recovers.
                for (var monthIndex = 0; ; monthIndex++)
                {
                    var monthDate = startDate.AddMonths(monthIndex);
                    if (monthDate >= tenancy.EndDate.Date) break;

                    var due = monthIndex == 0 ? firstDue : monthDate;

                    _db.Payments.Add(new Payment
                    {
                        TenancyId = tenancy.TenancyId,
                        Amount = rent,
                        DueDate = due,
                        PaymentDate = null,
                        Status = "Pending"
                    });
                    scheduled++;
                }
            }
 
            // Notify the tenant in-app. Queued here, committed by the SaveChangesAsync
            // below so it shares this action's transaction.
            AddNotification(tenancy.TenantId, scheduled > 0
                ? $"Your application for Unit {tenancy.Unit.UnitNumber} was approved. {scheduled} monthly rent payment(s) of RM {rent:N2} scheduled, starting {firstDue:dd MMM yyyy}."
                : $"Your application for Unit {tenancy.Unit.UnitNumber} was approved.");
 
            await _db.SaveChangesAsync();
 
            var extra = others.Count > 0 ? $" {others.Count} other pending application(s) were auto-declined." : "";
            TempData["Success"] = $"Application approved, unit {tenancy.Unit.UnitNumber} occupied, and {scheduled} rent payment(s) scheduled.{extra}";
            return RedirectToAction(nameof(Applications));
        }
 
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectApplication(int id, string? reason)
        {
            var propertyIds = await GetManagedPropertyIdsAsync();
 
            var tenancy = await _db.Tenancies
                .Include(t => t.Unit)
                .FirstOrDefaultAsync(t => t.TenancyId == id && propertyIds.Contains(t.Unit.PropertyId));
 
            if (tenancy == null)
            {
                TempData["Error"] = "Application not found or not in your properties.";
                return RedirectToAction(nameof(Applications));
            }
 
            if (tenancy.Status != "Pending")
            {
                TempData["Error"] = "Only pending applications can be rejected.";
                return RedirectToAction(nameof(Applications));
            }
 
            tenancy.Status = "Rejected";
            tenancy.Notes = string.IsNullOrWhiteSpace(reason) ? "No reason provided." : reason.Trim();
 
            // Mirror the approve path — the tenant hears back either way.
            AddNotification(tenancy.TenantId,
                $"Your application for Unit {tenancy.Unit.UnitNumber} was not approved. Reason: {tenancy.Notes}");
 
            await _db.SaveChangesAsync();
 
            TempData["Success"] = "Application rejected.";
            return RedirectToAction(nameof(Applications));
        }
 
        // ── TRACK RENT PAYMENTS ──────────────────────────────────────────────
 
        public async Task<IActionResult> Payments(string? status, int? propertyId, int page = 1)
        {
            var propertyIds = await GetManagedPropertyIdsAsync();
 
            // Property filter is applied in SQL; the status filter is applied in memory
            // below so it can use the DERIVED overdue state, not just the stored status.
            var baseQuery = _db.Payments.Where(p => propertyIds.Contains(p.Tenancy.Unit.PropertyId));
 
            if (propertyId.HasValue)
                baseQuery = baseQuery.Where(p => p.Tenancy.Unit.PropertyId == propertyId.Value);
 
            var allRows = await baseQuery
                .OrderBy(p => p.DueDate)
                .Select(p => new PaymentRowViewModel
                {
                    PaymentId = p.PaymentId,
                    TenantName = p.Tenancy.Tenant.FullName,
                    PropertyName = p.Tenancy.Unit.Property.Name,
                    UnitNumber = p.Tenancy.Unit.UnitNumber,
                    Amount = p.Amount,
                    DueDate = p.DueDate,
                    PaymentDate = p.PaymentDate,
                    Status = p.Status,
                    Notes = p.Notes
                })
                .ToListAsync();
 
            // Derive-on-read: an unpaid payment past its due date shows as Overdue.
            var today = TodayLocal();
            foreach (var r in allRows)
            {
                if (r.Status == "Pending" && r.DueDate.Date < today)
                    r.Status = "Overdue";
            }
 
            // Summary reflects ALL payments in scope (computed before the status filter).
            var pendingCount = allRows.Count(r => r.Status == "Pending");
            var overdueCount = allRows.Count(r => r.Status == "Overdue");
            var paidCount = allRows.Count(r => r.Status == "Paid");
            var outstandingAmount = allRows.Where(r => r.Status != "Paid").Sum(r => r.Amount);
            var paidAmount = allRows.Where(r => r.Status == "Paid").Sum(r => r.Amount);
 
            // Apply the status filter against the derived status.
            if (!string.IsNullOrWhiteSpace(status))
                allRows = allRows.Where(r => r.Status == status).ToList();
 
            // Order: Overdue first, then Pending, then Paid; soonest due within each.
            allRows = allRows
                .OrderBy(r => r.Status == "Overdue" ? 0 : r.Status == "Pending" ? 1 : 2)
                .ThenBy(r => r.DueDate)
                .ToList();
 
            page = NormalizePage(page, allRows.Count);
            var pageRows = allRows.Skip((page - 1) * PageSize).Take(PageSize).ToList();
 
            var vm = new PaymentListViewModel
            {
                Payments = pageRows,
                StatusFilter = status,
                PropertyFilter = propertyId,
                PropertyOptions = await GetPropertyOptionsAsync(propertyId),
                PendingCount = pendingCount,
                OverdueCount = overdueCount,
                PaidCount = paidCount,
                OutstandingAmount = outstandingAmount,
                PaidAmount = paidAmount,
                Today = today,
                CurrentPage = page,
                PageSize = PageSize,
                TotalItems = allRows.Count
            };
 
            return View(vm);
        }
 
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkPaid(int id, DateTime paymentDate)
        {
            var propertyIds = await GetManagedPropertyIdsAsync();
 
            var payment = await _db.Payments
                .FirstOrDefaultAsync(p => p.PaymentId == id && propertyIds.Contains(p.Tenancy.Unit.PropertyId));
 
            if (payment == null)
            {
                TempData["Error"] = "Payment not found or not in your properties.";
                return RedirectToAction(nameof(Payments));
            }
 
            if (payment.Status == "Paid")
            {
                TempData["Error"] = "This payment is already marked as paid.";
                return RedirectToAction(nameof(Payments));
            }
 
            // Guard: don't allow a future payment date.
            var today = TodayLocal();
            if (paymentDate.Date > today)
            {
                TempData["Error"] = "Payment date cannot be in the future.";
                return RedirectToAction(nameof(Payments));
            }
 
            payment.Status = "Paid";
            // Store as UTC — required for PostgreSQL timestamptz columns.
            payment.PaymentDate = DateTime.SpecifyKind(paymentDate.Date, DateTimeKind.Utc);
            await _db.SaveChangesAsync();
 
            TempData["Success"] = $"Payment recorded as received on {paymentDate:dd MMM yyyy}.";
            return RedirectToAction(nameof(Payments));
        }
 
        // ── ASSIGN MAINTENANCE TASKS ─────────────────────────────────────────
 
        public async Task<IActionResult> Maintenance(string? status, int? propertyId, int page = 1)
        {
            var propertyIds = await GetManagedPropertyIdsAsync();
 
            var query = _db.MaintenanceRequests.Where(m => propertyIds.Contains(m.Unit.PropertyId));
 
            if (propertyId.HasValue)
                query = query.Where(m => m.Unit.PropertyId == propertyId.Value);
 
            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(m => m.Status == status);
 
            var allRows = await query
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new MaintenanceRowViewModel
                {
                    RequestId = m.RequestId,
                    TenantName = m.Tenant.FullName,
                    PropertyName = m.Unit.Property.Name,
                    UnitNumber = m.Unit.UnitNumber,
                    Category = m.Category,
                    Description = m.Description,
                    Status = m.Status,
                    CreatedAt = m.CreatedAt,
                    AssignedStaffName = m.AssignedStaff != null ? m.AssignedStaff.FullName : null,
                    Priority = m.Priority,
                    AssignmentNotes = m.AssignmentNotes,
                    PhotoUrl = m.PhotoUrl
                })
                .ToListAsync();
 
            // Order: Submitted first (needs action), then Assigned, InProgress, Completed.
            allRows = allRows
                .OrderBy(r => r.Status == "Submitted" ? 0
                            : r.Status == "Assigned" ? 1
                            : r.Status == "InProgress" ? 2 : 3)
                .ThenByDescending(r => r.CreatedAt)
                .ToList();
 
            // Active maintenance staff available for assignment (not property-scoped —
            // staff are a shared pool, not tied to a property).
            var staff = await _userManager.GetUsersInRoleAsync("MaintenanceStaff");
            var staffOptions = staff
                .Where(s => s.IsActive)
                .OrderBy(s => s.FullName)
                .Select(s => new SelectListItem { Value = s.Id, Text = s.FullName })
                .ToList();
 
            var unassignedCount = await _db.MaintenanceRequests
                .CountAsync(m => propertyIds.Contains(m.Unit.PropertyId) && m.AssignedStaffId == null);
 
            page = NormalizePage(page, allRows.Count);
            var pageRows = allRows.Skip((page - 1) * PageSize).Take(PageSize).ToList();
 
            // r.PhotoUrl holds the raw S3 object key — this server has no AWS
            // credentials in the serverless upload path, so the view resolves each
            // key to a viewable URL client-side by asking the Lambda.
            ViewBag.S3Endpoint = _config["ApiGateway:S3Endpoint"] ?? "";

            var vm = new MaintenanceListViewModel
            {
                Requests = pageRows,
                StatusFilter = status,
                PropertyFilter = propertyId,
                PropertyOptions = await GetPropertyOptionsAsync(propertyId),
                StaffOptions = staffOptions,
                UnassignedCount = unassignedCount,
                CurrentPage = page,
                PageSize = PageSize,
                TotalItems = allRows.Count
            };
 
            return View(vm);
        }
 
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignRequest(int id, string staffId, string priority, string? notes)
        {
            var propertyIds = await GetManagedPropertyIdsAsync();
 
            var request = await _db.MaintenanceRequests
                .Include(m => m.Unit)
                .FirstOrDefaultAsync(m => m.RequestId == id && propertyIds.Contains(m.Unit.PropertyId));
 
            if (request == null)
            {
                TempData["Error"] = "Request not found or not in your properties.";
                return RedirectToAction(nameof(Maintenance));
            }
 
            if (request.Status == "Completed")
            {
                TempData["Error"] = "This request is already completed and cannot be reassigned.";
                return RedirectToAction(nameof(Maintenance));
            }
 
            // Validate the chosen staff member really is active maintenance staff.
            var staff = await _userManager.FindByIdAsync(staffId);
            if (staff == null || !staff.IsActive || !await _userManager.IsInRoleAsync(staff, "MaintenanceStaff"))
            {
                TempData["Error"] = "Please select a valid maintenance staff member.";
                return RedirectToAction(nameof(Maintenance));
            }
 
            if (!MaintenanceListViewModel.Priorities.Contains(priority))
            {
                TempData["Error"] = "Please select a valid priority.";
                return RedirectToAction(nameof(Maintenance));
            }
 
            request.AssignedStaffId = staff.Id;
            request.Status = "Assigned";
            request.Priority = priority;
            request.AssignmentNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
            // Notify the assigned maintenance staff.
            AddNotification(staff.Id,
                $"You have been assigned a {priority} priority {request.Category} request at Unit {request.Unit.UnitNumber}.");
            await _db.SaveChangesAsync();
 
            TempData["Success"] = $"Request assigned to {staff.FullName} ({priority} priority).";
            return RedirectToAction(nameof(Maintenance));
        }
    }
}