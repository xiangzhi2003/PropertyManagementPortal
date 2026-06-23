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
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AdminController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _db = db;
            _userManager = userManager;
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

        // ── Dashboard ────────────────────────────────────────────────────────────
        public async Task<IActionResult> Dashboard()
        {
            var managers = await _userManager.GetUsersInRoleAsync("PropertyManager");
            var tenants = await _userManager.GetUsersInRoleAsync("Tenant");
            var staff = await _userManager.GetUsersInRoleAsync("MaintenanceStaff");
            var admin = await _userManager.GetUserAsync(User);

            var vm = new DashboardViewModel
            {
                AdminName = admin!.FullName,
                TotalUsers = managers.Count + tenants.Count + staff.Count,
                TotalProperties = await _db.Properties.CountAsync(),
                PendingMaintenance = await _db.MaintenanceRequests
                    .CountAsync(r => r.Status == "Submitted" || r.Status == "Assigned" || r.Status == "InProgress"),
                OverduePayments = await _db.Payments.CountAsync(p => p.Status == "Overdue")
            };

            ViewBag.RecentLogs = await _db.ActivityLogs
                .OrderByDescending(l => l.Timestamp)
                .Take(5)
                .ToListAsync();

            return View(vm);
        }

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

            ViewBag.Role = roles.FirstOrDefault() ?? "No Role";
            ViewBag.TenancyCount = tenancyCount;
            ViewBag.MaintenanceCount = maintenanceCount;
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUserStatus(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains("Admin"))
            {
                TempData["Error"] = "Cannot deactivate an Admin account.";
                return RedirectToAction(nameof(Users));
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (user.Id == currentUser!.Id)
            {
                TempData["Error"] = "Cannot deactivate your own account.";
                return RedirectToAction(nameof(Users));
            }

            user.IsActive = !user.IsActive;
            await _userManager.UpdateAsync(user);
            await LogAsync(user.IsActive ? "Activated User" : "Deactivated User", "User", user.Id, user.Email);
            TempData["Success"] = $"User {user.FullName} has been {(user.IsActive ? "activated" : "deactivated")}.";
            return RedirectToAction(nameof(Users));
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
            if (hasTenancies || hasRequests)
            {
                TempData["Error"] = "Cannot delete user with active tenancies or maintenance requests.";
                return RedirectToAction(nameof(Users));
            }

            var name = user.FullName;
            var email = user.Email;
            await _userManager.DeleteAsync(user);
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
                .Include(p => p.Units)
                .FirstOrDefaultAsync(p => p.PropertyId == id);

            if (property == null) return NotFound();
            return View(property);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TogglePropertyStatus(int id)
        {
            var property = await _db.Properties.FindAsync(id);
            if (property == null) return NotFound();

            property.Status = property.Status == "Active" ? "Inactive" : "Active";
            await _db.SaveChangesAsync();
            await LogAsync($"Set Property {property.Status}", "Property", id.ToString(), property.Name);
            TempData["Success"] = $"Property '{property.Name}' is now {property.Status}.";
            return RedirectToAction(nameof(Properties));
        }

        // ── Reports ──────────────────────────────────────────────────────────────
        public async Task<IActionResult> Reports()
        {
            var vm = new ReportsViewModel
            {
                TotalUnits = await _db.Units.CountAsync(),
                OccupiedUnits = await _db.Units.CountAsync(u => u.Status == "Occupied"),
                VacantUnits = await _db.Units.CountAsync(u => u.Status == "Vacant"),
                TotalPayments = await _db.Payments.CountAsync(),
                PaidPayments = await _db.Payments.CountAsync(p => p.Status == "Paid"),
                PendingPayments = await _db.Payments.CountAsync(p => p.Status == "Pending"),
                OverduePayments = await _db.Payments.CountAsync(p => p.Status == "Overdue"),
                SubmittedRequests = await _db.MaintenanceRequests.CountAsync(m => m.Status == "Submitted"),
                AssignedRequests = await _db.MaintenanceRequests.CountAsync(m => m.Status == "Assigned"),
                InProgressRequests = await _db.MaintenanceRequests.CountAsync(m => m.Status == "InProgress"),
                CompletedRequests = await _db.MaintenanceRequests.CountAsync(m => m.Status == "Completed")
            };
            return View(vm);
        }

        // ── Activity Log ─────────────────────────────────────────────────────────
        public async Task<IActionResult> ActivityLog(int page = 1)
        {
            const int pageSize = 20;
            var total = await _db.ActivityLogs.CountAsync();
            var logs = await _db.ActivityLogs
                .OrderByDescending(l => l.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);
            return View(logs);
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
