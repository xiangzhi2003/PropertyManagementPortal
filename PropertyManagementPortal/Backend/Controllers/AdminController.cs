using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PropertyManagementPortal.Data;
using PropertyManagementPortal.Models;
using PropertyManagementPortal.ViewModels.Admin;

namespace PropertyManagementPortal.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : AppControllerBase
    {
        private readonly RoleManager<IdentityRole> _roleManager;

        public AdminController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
            : base(db, userManager)
        {
            _roleManager = roleManager;
        }

        private async Task LogAsync(string action, string entityType, string? entityId = null, string? details = null)
        {
            var user = await _userManager.GetUserAsync(User);
            _db.ActivityLogs.Add(new ActivityLog
            {
                UserId = user!.Id,
                UserName = user.FullName,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Details = details,
                Timestamp = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }

        // ── Dashboard (includes what used to be the separate Reports page) ────────
        public async Task<IActionResult> Dashboard()
        {
            var managers = await _userManager.GetUsersInRoleAsync("PropertyManager");
            var tenants = await _userManager.GetUsersInRoleAsync("Tenant");
            var staff = await _userManager.GetUsersInRoleAsync("MaintenanceStaff");
            var admin = await _userManager.GetUserAsync(User);

            // Overdue is derived, not stored: a Pending payment past its due date counts
            // as overdue. Mirrors the same derive-on-read logic ManagerController uses.
            // Fetched with full property/unit/tenant detail so the dashboard can show
            // exactly which payment and property each unpaid record belongs to.
            var today = DateTime.UtcNow.Date;
            var unpaidPayments = await _db.Payments
                .Include(p => p.Tenancy).ThenInclude(t => t.Tenant)
                .Include(p => p.Tenancy).ThenInclude(t => t.Unit).ThenInclude(u => u.Property).ThenInclude(pr => pr.Manager)
                .Where(p => p.Status != "Paid")
                .Select(p => new PaymentDetailRow
                {
                    TenantId = p.Tenancy.TenantId,
                    TenantName = p.Tenancy.Tenant.FullName,
                    TenantEmail = p.Tenancy.Tenant.Email ?? "",
                    TenantPhone = p.Tenancy.Tenant.PhoneNumber ?? "",
                    PropertyName = p.Tenancy.Unit.Property.Name,
                    UnitNumber = p.Tenancy.Unit.UnitNumber,
                    ManagerName = p.Tenancy.Unit.Property.Manager != null ? p.Tenancy.Unit.Property.Manager.FullName : null,
                    Amount = p.Amount,
                    DueDate = p.DueDate,
                    Status = p.Status
                })
                .ToListAsync();

            foreach (var p in unpaidPayments)
                if (p.Status == "Pending" && p.DueDate.Date < today)
                    p.Status = "Overdue";

            unpaidPayments = unpaidPayments
                .OrderBy(p => p.Status == "Overdue" ? 0 : 1)
                .ThenBy(p => p.DueDate)
                .ToList();

            var overduePayments = unpaidPayments.Count(p => p.Status == "Overdue");
            var pendingPaymentsCount = unpaidPayments.Count - overduePayments;

            // Occupancy broken down per property, so a single overall rate isn't the
            // only view — an admin can see exactly which property is under-occupied.
            var occupancyByProperty = await _db.Properties
                .Select(p => new PropertyOccupancyRow
                {
                    PropertyName = p.Name,
                    TotalUnits = p.Units.Count,
                    OccupiedUnits = p.Units.Count(u => u.Status == "Occupied")
                })
                .OrderByDescending(p => p.TotalUnits)
                .ToListAsync();

            // Open maintenance requests with full context, so an admin can see exactly
            // which property/unit/tenant each still-open job belongs to.
            var openMaintenanceDetails = await _db.MaintenanceRequests
                .Include(m => m.Tenant)
                .Include(m => m.AssignedStaff)
                .Include(m => m.Unit).ThenInclude(u => u.Property).ThenInclude(p => p.Manager)
                .Where(m => m.Status != "Completed")
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new MaintenanceDetailRow
                {
                    RequestId = m.RequestId,
                    Category = m.Category,
                    PropertyName = m.Unit.Property.Name,
                    UnitNumber = m.Unit.UnitNumber,
                    TenantId = m.TenantId,
                    TenantName = m.Tenant.FullName,
                    TenantPhone = m.Tenant.PhoneNumber ?? "",
                    Status = m.Status,
                    Priority = m.Priority,
                    ManagerName = m.Unit.Property.Manager != null ? m.Unit.Property.Manager.FullName : null,
                    AssignedStaffId = m.AssignedStaffId,
                    AssignedStaffName = m.AssignedStaff != null ? m.AssignedStaff.FullName : null,
                    CreatedAt = m.CreatedAt
                })
                .ToListAsync();

            var vm = new DashboardViewModel
            {
                AdminName = admin!.FullName,
                TotalUsers = managers.Count + tenants.Count + staff.Count,
                TotalProperties = await _db.Properties.CountAsync(),
                PendingMaintenance = await _db.MaintenanceRequests
                    .CountAsync(r => r.Status == "Submitted" || r.Status == "Assigned" || r.Status == "InProgress"),
                OverduePayments = overduePayments,

                // Occupancy
                TotalUnits = await _db.Units.CountAsync(),
                OccupiedUnits = await _db.Units.CountAsync(u => u.Status == "Occupied"),
                VacantUnits = await _db.Units.CountAsync(u => u.Status == "Vacant"),
                OccupancyByProperty = occupancyByProperty,

                // Payments
                TotalPayments = await _db.Payments.CountAsync(),
                PaidPayments = await _db.Payments.CountAsync(p => p.Status == "Paid"),
                PendingPayments = pendingPaymentsCount,
                UnpaidPaymentDetails = unpaidPayments,

                // Maintenance
                SubmittedRequests = await _db.MaintenanceRequests.CountAsync(m => m.Status == "Submitted"),
                AssignedRequests = await _db.MaintenanceRequests.CountAsync(m => m.Status == "Assigned"),
                InProgressRequests = await _db.MaintenanceRequests.CountAsync(m => m.Status == "InProgress"),
                CompletedRequests = await _db.MaintenanceRequests.CountAsync(m => m.Status == "Completed"),
                OpenMaintenanceDetails = openMaintenanceDetails
            };

            ViewBag.RecentLogs = await _db.ActivityLogs
                .OrderByDescending(l => l.Timestamp)
                .Take(5)
                .ToListAsync();

            return View(vm);
        }

        // Old bookmarks/links to /Admin/Reports still work — Reports is now part of Dashboard.
        public IActionResult Reports() => RedirectToAction(nameof(Dashboard));

        // ── Users ────────────────────────────────────────────────────────────────
        public async Task<IActionResult> Users(string? search, string? role, string? status)
        {
            var allUsers = _userManager.Users.ToList();
            var rows = new List<UserRowViewModel>();

            foreach (var u in allUsers)
            {
                var roles = await _userManager.GetRolesAsync(u);
                var userRole = roles.FirstOrDefault() ?? "No Role";

                if (!string.IsNullOrEmpty(role) && userRole != role) continue;
                if (!string.IsNullOrEmpty(status))
                {
                    if (status == "Active" && !u.IsActive) continue;
                    if (status == "Inactive" && u.IsActive) continue;
                }
                if (!string.IsNullOrEmpty(search))
                {
                    var s = search.ToLower();
                    if (!u.FullName.ToLower().Contains(s) && !u.Email!.ToLower().Contains(s)) continue;
                }

                rows.Add(new UserRowViewModel
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Email = u.Email!,
                    PhoneNumber = u.PhoneNumber,
                    Role = userRole,
                    IsActive = u.IsActive,
                    CreatedAt = u.CreatedAt
                });
            }

            return View(new UserListViewModel
            {
                Users = rows.OrderBy(r => r.FullName).ToList(),
                SearchTerm = search,
                RoleFilter = role,
                StatusFilter = status
            });
        }

        public IActionResult CreateUser() => View(new CreateUserViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(CreateUserViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var validRoles = new[] { "Admin", "PropertyManager", "Tenant", "MaintenanceStaff" };
            if (!validRoles.Contains(model.Role))
            {
                ModelState.AddModelError("Role", "Invalid role selected.");
                return View(model);
            }

            var user = new ApplicationUser
            {
                FullName = model.FullName,
                UserName = model.Email,
                Email = model.Email,
                PhoneNumber = model.PhoneNumber,
                EmailConfirmed = true,
                IsActive = true
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
                return View(model);
            }

            await _userManager.AddToRoleAsync(user, model.Role);
            await LogAsync("Created User", "User", user.Id, $"{model.Email} ({model.Role})");
            TempData["Success"] = $"User {model.FullName} created successfully.";
            return RedirectToAction(nameof(Users));
        }

        public async Task<IActionResult> EditUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            return View(new EditUserViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email!,
                PhoneNumber = user.PhoneNumber,
                Role = roles.FirstOrDefault() ?? "",
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(EditUserViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null) return NotFound();

            var currentRoles = await _userManager.GetRolesAsync(user);
            var isAdmin = currentRoles.Contains("Admin");

            user.FullName = model.FullName;
            user.PhoneNumber = model.PhoneNumber;
            if (!isAdmin) user.IsActive = model.IsActive;

            await _userManager.UpdateAsync(user);

            if (!isAdmin && model.Role != currentRoles.FirstOrDefault())
            {
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
                await _userManager.AddToRoleAsync(user, model.Role);
            }

            await LogAsync("Edited User", "User", user.Id, model.Email);
            TempData["Success"] = $"User {model.FullName} updated successfully.";
            return RedirectToAction(nameof(Users));
        }

        public async Task<IActionResult> ViewUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            var tenancyCount = await _db.Tenancies.CountAsync(t => t.TenantId == id);
            var maintenanceCount = await _db.MaintenanceRequests.CountAsync(m => m.TenantId == id);
            var outstandingAmount = await _db.Payments
                .Where(p => p.Tenancy.TenantId == id && p.Status != "Paid")
                .SumAsync(p => (decimal?)p.Amount) ?? 0;

            ViewBag.Role = roles.FirstOrDefault() ?? "No Role";
            ViewBag.TenancyCount = tenancyCount;
            ViewBag.MaintenanceCount = maintenanceCount;
            ViewBag.OutstandingAmount = outstandingAmount;
            // Actions performed ON this account (Created/Edited/Deleted User), not
            // actions this user performed themselves — matches EntityId, not UserId.
            ViewBag.RecentActivity = await _db.ActivityLogs
                .Where(l => l.EntityType == "User" && l.EntityId == id)
                .OrderByDescending(l => l.Timestamp)
                .Take(5)
                .ToListAsync();
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains("Admin"))
            {
                TempData["Error"] = "Cannot delete an Admin account.";
                return RedirectToAction(nameof(Users));
            }

            var hasTenancies = await _db.Tenancies.AnyAsync(t => t.TenantId == id);
            var hasRequests = await _db.MaintenanceRequests.AnyAsync(m => m.TenantId == id || m.AssignedStaffId == id);
            var hasManagedProperties = await _db.Properties.AnyAsync(p => p.ManagerId == id);
            var hasMaintenanceUpdates = await _db.MaintenanceUpdates.AnyAsync(u => u.StaffId == id);

            if (hasTenancies || hasRequests)
            {
                TempData["Error"] = "Cannot delete user with active tenancies or maintenance requests.";
                return RedirectToAction(nameof(Users));
            }

            if (hasManagedProperties)
            {
                TempData["Error"] = "Cannot delete a user who is assigned as a property manager. Reassign or unassign their properties first.";
                return RedirectToAction(nameof(Users));
            }

            if (hasMaintenanceUpdates)
            {
                TempData["Error"] = "Cannot delete a user who has a maintenance job history (status updates). Deactivate the account instead.";
                return RedirectToAction(nameof(Users));
            }

            var name = user.FullName;
            var email = user.Email;

            try
            {
                var result = await _userManager.DeleteAsync(user);
                if (!result.Succeeded)
                {
                    TempData["Error"] = "Could not delete this user: " + string.Join(" ", result.Errors.Select(e => e.Description));
                    return RedirectToAction(nameof(Users));
                }
            }
            catch (DbUpdateException)
            {
                TempData["Error"] = "Cannot delete this user because other records still reference their account. Deactivate the account instead.";
                return RedirectToAction(nameof(Users));
            }

            await LogAsync("Deleted User", "User", id, $"{email}");
            TempData["Success"] = $"User {name} has been deleted.";
            return RedirectToAction(nameof(Users));
        }

        // ── Properties ───────────────────────────────────────────────────────────
        public async Task<IActionResult> Properties(string? search, string? type, string? status)
        {
            var query = _db.Properties.Include(p => p.Manager).Include(p => p.Units).AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(p => p.Name.ToLower().Contains(search.ToLower()) ||
                                         p.Address.ToLower().Contains(search.ToLower()));
            if (!string.IsNullOrEmpty(type))
                query = query.Where(p => p.Type == type);
            if (!string.IsNullOrEmpty(status))
                query = query.Where(p => p.Status == status);

            ViewBag.Search = search;
            ViewBag.TypeFilter = type;
            ViewBag.StatusFilter = status;
            return View(await query.OrderBy(p => p.Name).ToListAsync());
        }

        public async Task<IActionResult> AddProperty()
        {
            return View(new PropertyFormViewModel
            {
                ManagerOptions = await GetManagerOptionsAsync()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddProperty(PropertyFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.ManagerOptions = await GetManagerOptionsAsync();
                return View(model);
            }

            var property = new Property
            {
                Name = model.Name,
                Address = model.Address,
                Type = model.Type,
                ManagerId = string.IsNullOrEmpty(model.ManagerId) ? null : model.ManagerId,
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };

            _db.Properties.Add(property);
            await _db.SaveChangesAsync();
            await LogAsync("Added Property", "Property", property.PropertyId.ToString(), model.Name);
            TempData["Success"] = $"Property '{model.Name}' added successfully.";
            return RedirectToAction(nameof(Properties));
        }

        public async Task<IActionResult> EditProperty(int id)
        {
            var property = await _db.Properties.FindAsync(id);
            if (property == null) return NotFound();

            return View(new PropertyFormViewModel
            {
                PropertyId = property.PropertyId,
                Name = property.Name,
                Address = property.Address,
                Type = property.Type,
                ManagerId = property.ManagerId,
                ManagerOptions = await GetManagerOptionsAsync()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProperty(int id, PropertyFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.ManagerOptions = await GetManagerOptionsAsync();
                return View(model);
            }

            var property = await _db.Properties.FindAsync(id);
            if (property == null) return NotFound();

            property.Name = model.Name;
            property.Address = model.Address;
            property.Type = model.Type;
            property.ManagerId = string.IsNullOrEmpty(model.ManagerId) ? null : model.ManagerId;

            await _db.SaveChangesAsync();
            await LogAsync("Edited Property", "Property", id.ToString(), model.Name);
            TempData["Success"] = $"Property '{model.Name}' updated successfully.";
            return RedirectToAction(nameof(Properties));
        }

        public async Task<IActionResult> ViewProperty(int id)
        {
            var property = await _db.Properties
                .Include(p => p.Manager)
                .Include(p => p.Units).ThenInclude(u => u.Tenancies).ThenInclude(t => t.Tenant)
                .FirstOrDefaultAsync(p => p.PropertyId == id);

            if (property == null) return NotFound();
            return View(property);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProperty(int id)
        {
            var property = await _db.Properties.Include(p => p.Units).FirstOrDefaultAsync(p => p.PropertyId == id);
            if (property == null) return NotFound();

            var unitIds = property.Units.Select(u => u.UnitId).ToList();
            var hasTenancies = await _db.Tenancies.AnyAsync(t => unitIds.Contains(t.UnitId));
            var hasMaintenanceRequests = await _db.MaintenanceRequests.AnyAsync(m => unitIds.Contains(m.UnitId));

            if (hasTenancies || hasMaintenanceRequests)
            {
                TempData["Error"] = "Cannot delete a property that has tenancies or maintenance requests on its units. Remove those first.";
                return RedirectToAction(nameof(Properties));
            }

            var name = property.Name;

            try
            {
                _db.Properties.Remove(property); // cascades to its (now childless) units
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                TempData["Error"] = "Cannot delete this property because related records still exist.";
                return RedirectToAction(nameof(Properties));
            }

            await LogAsync("Deleted Property", "Property", id.ToString(), name);
            TempData["Success"] = $"Property '{name}' and its units have been deleted.";
            return RedirectToAction(nameof(Properties));
        }

        // ── Activity Log ─────────────────────────────────────────────────────────
        public async Task<IActionResult> ActivityLog(int page = 1, string? search = null, string? entityType = null)
        {
            const int pageSize = 20;

            var query = _db.ActivityLogs.AsQueryable();

            if (!string.IsNullOrWhiteSpace(entityType))
                query = query.Where(l => l.EntityType == entityType);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(l => l.UserName.ToLower().Contains(s)
                                       || l.Action.ToLower().Contains(s)
                                       || (l.Details != null && l.Details.ToLower().Contains(s)));
            }

            var total = await query.CountAsync();
            var logs = await query
                .OrderByDescending(l => l.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);
            ViewBag.TotalCount = total;
            ViewBag.Search = search;
            ViewBag.EntityTypeFilter = entityType;
            ViewBag.EntityTypes = await _db.ActivityLogs
                .Select(l => l.EntityType)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync();
            return View(logs);
        }

        // ── Units ────────────────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddUnit(int propertyId, string unitNumber, string type, decimal rentAmount, int floor, string? description)
        {
            var property = await _db.Properties.FindAsync(propertyId);
            if (property == null) return NotFound();

            _db.Units.Add(new Unit
            {
                PropertyId = propertyId,
                UnitNumber = unitNumber,
                Type = type,
                RentAmount = rentAmount,
                Floor = floor,
                Description = description,
                Status = "Vacant"
            });
            await _db.SaveChangesAsync();
            await LogAsync("Added Unit", "Unit", propertyId.ToString(), $"{unitNumber} — {property.Name}");
            TempData["Success"] = $"Unit {unitNumber} added successfully.";
            return RedirectToAction(nameof(ViewProperty), new { id = propertyId });
        }

        public async Task<IActionResult> EditUnit(int id)
        {
            var unit = await _db.Units.Include(u => u.Property).FirstOrDefaultAsync(u => u.UnitId == id);
            if (unit == null) return NotFound();
            return View(unit);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUnit(int id, string unitNumber, string type, decimal rentAmount, int floor, string? description)
        {
            var unit = await _db.Units.Include(u => u.Property).FirstOrDefaultAsync(u => u.UnitId == id);
            if (unit == null) return NotFound();

            unit.UnitNumber = unitNumber;
            unit.Type = type;
            unit.RentAmount = rentAmount;
            unit.Floor = floor;
            unit.Description = description;
            await _db.SaveChangesAsync();
            await LogAsync("Edited Unit", "Unit", id.ToString(), $"{unitNumber} — {unit.Property.Name}");
            TempData["Success"] = $"Unit {unitNumber} updated successfully.";
            return RedirectToAction(nameof(ViewProperty), new { id = unit.PropertyId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUnit(int id)
        {
            var unit = await _db.Units.Include(u => u.Property).FirstOrDefaultAsync(u => u.UnitId == id);
            if (unit == null) return NotFound();

            var hasActive = await _db.Tenancies.AnyAsync(t => t.UnitId == id);
            if (hasActive)
            {
                TempData["Error"] = "Cannot delete a unit with active tenancies.";
                return RedirectToAction(nameof(ViewProperty), new { id = unit.PropertyId });
            }

            var propertyId = unit.PropertyId;
            var number = unit.UnitNumber;
            _db.Units.Remove(unit);
            await _db.SaveChangesAsync();
            await LogAsync("Deleted Unit", "Unit", id.ToString(), $"{number} — {unit.Property.Name}");
            TempData["Success"] = $"Unit {number} deleted.";
            return RedirectToAction(nameof(ViewProperty), new { id = propertyId });
        }

        // ── Role Requests ────────────────────────────────────────────────────────
        public async Task<IActionResult> RoleRequests(string? status)
        {
            var query = _db.RoleRequests.Include(r => r.User).AsQueryable();
            if (!string.IsNullOrEmpty(status))
                query = query.Where(r => r.Status == status);

            ViewBag.StatusFilter = status;
            ViewBag.PendingCount = await _db.RoleRequests.CountAsync(r => r.Status == "Pending");
            ViewBag.ApprovedCount = await _db.RoleRequests.CountAsync(r => r.Status == "Approved");
            ViewBag.RejectedCount = await _db.RoleRequests.CountAsync(r => r.Status == "Rejected");
            return View(await query.OrderByDescending(r => r.RequestedAt).ToListAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveRoleRequest(int id)
        {
            var request = await _db.RoleRequests.Include(r => r.User).FirstOrDefaultAsync(r => r.Id == id);
            if (request == null) return NotFound();

            var user = request.User;
            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, request.RequestedRole);

            var reviewer = await _userManager.GetUserAsync(User);
            request.Status = "Approved";
            request.ReviewedAt = DateTime.UtcNow;
            request.ReviewedBy = reviewer!.FullName;
            await _db.SaveChangesAsync();

            await LogAsync("Approved Role Request", "RoleRequest", id.ToString(), $"{user.FullName} → {request.RequestedRole}");
            TempData["Success"] = $"{user.FullName} has been approved as {(request.RequestedRole == "PropertyManager" ? "Property Manager" : "Maintenance Staff")}.";
            return RedirectToAction(nameof(RoleRequests));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectRoleRequest(int id, string? adminNotes)
        {
            var request = await _db.RoleRequests.Include(r => r.User).FirstOrDefaultAsync(r => r.Id == id);
            if (request == null) return NotFound();

            var reviewer = await _userManager.GetUserAsync(User);
            request.Status = "Rejected";
            request.ReviewedAt = DateTime.UtcNow;
            request.ReviewedBy = reviewer!.FullName;
            request.AdminNotes = adminNotes;
            await _db.SaveChangesAsync();

            await LogAsync("Rejected Role Request", "RoleRequest", id.ToString(), $"{request.User.FullName} → {request.RequestedRole}");
            TempData["Success"] = $"Role request from {request.User.FullName} has been rejected.";
            return RedirectToAction(nameof(RoleRequests));
        }

        // ── Helpers ──────────────────────────────────────────────────────────────
        private async Task<List<SelectListItem>> GetManagerOptionsAsync()
        {
            var managers = await _userManager.GetUsersInRoleAsync("PropertyManager");
            var options = managers.Select(m => new SelectListItem
            {
                Value = m.Id,
                Text = $"{m.FullName} ({m.Email})"
            }).ToList();
            options.Insert(0, new SelectListItem { Value = "", Text = "— Unassigned —" });
            return options;
        }
    }
}
