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
    public class ManagerController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
 
        public ManagerController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
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
 
        private async Task<List<SelectListItem>> GetPropertyOptionsAsync()
        {
            var userId = _userManager.GetUserId(User);
            return await _db.Properties
                .Where(p => p.ManagerId == userId)
                .OrderBy(p => p.Name)
                .Select(p => new SelectListItem { Value = p.PropertyId.ToString(), Text = p.Name })
                .ToListAsync();
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
 
            var pendingPayments = await _db.Payments
                .CountAsync(p => unitIds.Contains(p.Tenancy.UnitId) && p.Status == "Pending");
 
            var overduePayments = await _db.Payments
                .CountAsync(p => unitIds.Contains(p.Tenancy.UnitId) && p.Status == "Overdue");
 
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
 
        public async Task<IActionResult> Units(int? propertyId, string? status, string? search)
        {
            var propertyIds = await GetManagedPropertyIdsAsync();
 
            var query = _db.Units.Where(u => propertyIds.Contains(u.PropertyId));
 
            if (propertyId.HasValue)
                query = query.Where(u => u.PropertyId == propertyId.Value);
 
            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(u => u.Status == status);
 
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(u => u.UnitNumber.Contains(search));
 
            var rows = await query
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
 
            var vm = new UnitListViewModel
            {
                Units = rows,
                PropertyFilter = propertyId,
                StatusFilter = status,
                SearchTerm = search,
                PropertyOptions = await GetPropertyOptionsAsync()
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
                TempData["Error"] = "Please check the unit details and try again.";
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
 
        public async Task<IActionResult> Applications(string? status)
        {
            var propertyIds = await GetManagedPropertyIdsAsync();
 
            var query = _db.Tenancies.Where(t => propertyIds.Contains(t.Unit.PropertyId));
 
            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(t => t.Status == status);
 
            var rows = await query
                .OrderByDescending(t => t.CreatedAt)
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
            rows = rows
                .OrderBy(r => r.Status == "Pending" ? 0 : 1)
                .ThenByDescending(r => r.CreatedAt)
                .ToList();
 
            var vm = new ApplicationListViewModel
            {
                Applications = rows,
                StatusFilter = status,
                PendingCount = rows.Count(r => r.Status == "Pending")
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
 
            // Auto-reject every other pending application on the same unit —
            // a unit can only be assigned to one tenant.
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
 
            await _db.SaveChangesAsync();
 
            var extra = others.Count > 0 ? $" {others.Count} other pending application(s) were auto-declined." : "";
            TempData["Success"] = $"Application approved and unit {tenancy.Unit.UnitNumber} marked as occupied.{extra}";
            return RedirectToAction(nameof(Applications));
        }
 
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectApplication(int id, string? reason)
        {
            var propertyIds = await GetManagedPropertyIdsAsync();
 
            var tenancy = await _db.Tenancies
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
            await _db.SaveChangesAsync();
 
            TempData["Success"] = "Application rejected.";
            return RedirectToAction(nameof(Applications));
        }
    }
}