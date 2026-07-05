using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertyManagementPortal.Data;
using PropertyManagementPortal.Models;
using PropertyManagementPortal.ViewModels.Tenant;

namespace PropertyManagementPortal.Controllers
{
    [Authorize(Roles = "Tenant")]
    public class TenantController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public TenantController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();
            var pendingRequest = await _db.RoleRequests
                .Where(r => r.UserId == user!.Id && r.Status == "Pending")
                .FirstOrDefaultAsync();
            var rejectedRequest = await _db.RoleRequests
                .Where(r => r.UserId == user!.Id && r.Status == "Rejected")
                .OrderByDescending(r => r.ReviewedAt)
                .FirstOrDefaultAsync();
            var currentTenancy = await _db.Tenancies
                .Include(t => t.Unit)
                .ThenInclude(u => u.Property)
                .FirstOrDefaultAsync(t => t.TenantId == user.Id && t.Status == "Approved");
            var pendingApplicationsCount = await _db.Set<Tenancy>()
                .CountAsync(t => t.TenantId == user.Id && t.Status == "Pending");
            var activeMaintenanceCount = await _db.MaintenanceRequests
                .CountAsync(m => m.TenantId == user.Id && m.Status != "Completed");
            var outstandingPayments = await _db.Payments
                .Where(p => p.Tenancy.TenantId == user.Id && p.Status != "Paid")
                .SumAsync(p => (decimal?)p.Amount) ?? 0;

            var model = new TenantDashboardViewModel{
                User = user,
                PendingRequest = pendingRequest,
                RejectedRequest = rejectedRequest,
                CurrentTenancy = currentTenancy,
                PendingApplicationsCount = pendingApplicationsCount,
                ActiveMaintenanceCount = activeMaintenanceCount,
                OutstandingPayments = outstandingPayments
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestRole(string role)
        {
            var validRoles = new[] { "PropertyManager", "MaintenanceStaff" };
            if (!validRoles.Contains(role))
            {
                TempData["Error"] = "Invalid role selected.";
                return RedirectToAction(nameof(Dashboard));
            }

            var user = await _userManager.GetUserAsync(User);
            var existing = await _db.RoleRequests
                .AnyAsync(r => r.UserId == user!.Id && r.Status == "Pending");

            if (existing)
            {
                TempData["Error"] = "You already have a pending role request.";
                return RedirectToAction(nameof(Dashboard));
            }

            _db.RoleRequests.Add(new RoleRequest
            {
                UserId = user!.Id,
                RequestedRole = role,
                Status = "Pending",
                RequestedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Your request to become a {(role == "PropertyManager" ? "Property Manager" : "Maintenance Staff")} has been submitted. Please wait for admin approval.";
            return RedirectToAction(nameof(Dashboard));
        }

        public async Task<IActionResult> BrowseUnits()
        {
            var units = await _db.Units
                .Include(u => u.Property)
                .Where(u => u.Status == "Vacant")
                .ToListAsync();

            return View(units);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Apply(int unitId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null) return Challenge();

            // prevent duplicate application
            var existing = await _db.UnitApplications
                .AnyAsync(a => a.UnitId == unitId && a.TenantId == user.Id && a.Status == "Pending");

            if (existing)
            {
                TempData["Error"] = "You already applied for this unit.";
                return RedirectToAction(nameof(BrowseUnits));
            }

            var application = new UnitApplication
            {
                UnitId = unitId,
                TenantId = user.Id,
                Status = "Pending",
                AppliedAt = DateTime.UtcNow
            };

            _db.UnitApplications.Add(application);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Application submitted successfully.";
            return RedirectToAction(nameof(BrowseUnits));
        }
    }
}
