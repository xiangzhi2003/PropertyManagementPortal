using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertyManagementPortal.Data;
using PropertyManagementPortal.Models;
using PropertyManagementPortal.ViewModels.Maintenance;

namespace PropertyManagementPortal.Controllers
{
    [Authorize(Roles = "MaintenanceStaff")]
    public class MaintenanceController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public MaintenanceController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // ── DASHBOARD ────────────────────────────────────────────────────────
        public async Task<IActionResult> Dashboard()
        {
            var userId = _userManager.GetUserId(User);
            var user = await _userManager.GetUserAsync(User);

            // All requests assigned to this staff member.
            var myRequests = _db.MaintenanceRequests.Where(r => r.AssignedStaffId == userId);

            var assignedCount = await myRequests.CountAsync(r => r.Status == "Assigned");
            var inProgressCount = await myRequests.CountAsync(r => r.Status == "InProgress");
            var completedCount = await myRequests.CountAsync(r => r.Status == "Completed");

            // "Completed this month" is based on when the Completed update was written,
            // not when the request was created — that's the accurate completion date.
            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var completedThisMonth = await _db.MaintenanceUpdates
                .CountAsync(u => u.StaffId == userId
                              && u.StatusUpdate == "Completed"
                              && u.UpdatedAt >= monthStart);

            // Latest still-open job (most recently created), with unit + property names.
            var latest = await myRequests
                .Where(r => r.Status != "Completed")
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    r.RequestId,
                    UnitNumber = r.Unit.UnitNumber,
                    PropertyName = r.Unit.Property.Name,
                    r.Category,
                    r.Status,
                    r.Priority,
                    r.CreatedAt
                })
                .FirstOrDefaultAsync();

            var vm = new MaintenanceDashboardViewModel
            {
                StaffName = user!.FullName,
                AssignedCount = assignedCount,
                InProgressCount = inProgressCount,
                CompletedCount = completedCount,
                CompletedThisMonth = completedThisMonth
            };

            if (latest != null)
            {
                vm.HasLatestJob = true;
                vm.LatestJobId = latest.RequestId;
                vm.LatestUnitNumber = latest.UnitNumber;
                vm.LatestPropertyName = latest.PropertyName;
                vm.LatestCategory = latest.Category;
                vm.LatestStatus = latest.Status;
                vm.LatestPriority = latest.Priority;
                vm.LatestCreatedAt = latest.CreatedAt;
            }

            return View(vm);
        }

        

        // ── ASSIGNED JOBS (placeholder — full list built next) ────────────────
        public IActionResult Jobs()
        {
            return View();
        }

        // ── NOTIFICATIONS (placeholder — full list built next) ────────────────
        public IActionResult Notifications()
        {
            return View();
        }
    }
}
