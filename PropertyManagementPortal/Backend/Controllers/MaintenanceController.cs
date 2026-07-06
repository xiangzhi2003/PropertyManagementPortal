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
    public class MaintenanceController : AppControllerBase
    {
        public MaintenanceController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
            : base(db, userManager)
        {
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

            var vm = new DashboardViewModel
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

        // ── ASSIGNED JOBS ─────────────────────────────────────────────────────
        public async Task<IActionResult> Jobs(string? status = null)
        {
            var userId = _userManager.GetUserId(User);
            var user = await _userManager.GetUserAsync(User);

            // Only this staff member's jobs. Filtering happens in the query (SQL),
            // so unrelated rows never leave the database.
            var query = _db.MaintenanceRequests.Where(r => r.AssignedStaffId == userId);

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(r => r.Status == status);

            var jobs = await query
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new JobRowViewModel
                {
                    RequestId = r.RequestId,
                    UnitNumber = r.Unit.UnitNumber,
                    PropertyName = r.Unit.Property.Name,
                    Category = r.Category,
                    Description = r.Description,
                    TenantName = r.Tenant.FullName,
                    Status = r.Status,
                    Priority = r.Priority ?? "",
                    CreatedAt = r.CreatedAt
                })
                .ToListAsync();

            // Order: active work first (Assigned, InProgress), Completed last;
            // newest within each group. Done in memory on the small result set.
            jobs = jobs
                .OrderBy(j => j.Status == "Assigned" ? 0
                            : j.Status == "InProgress" ? 1 : 2)
                .ThenByDescending(j => j.CreatedAt)
                .ToList();

            var vm = new JobsViewModel
            {
                StaffName = user!.FullName,
                Jobs = jobs,
                StatusFilter = status
            };

            return View(vm);
        }

        // ── JOB DETAILS ─────────────────────────────────────────────────────
        public async Task<IActionResult> JobDetails(int id)
        {
            var userId = _userManager.GetUserId(User);
            var user = await _userManager.GetUserAsync(User);

            var vm = await _db.MaintenanceRequests
                .Where(r => r.RequestId == id && r.AssignedStaffId == userId)   // ownership guard
                .Select(r => new JobDetailsViewModel
                {
                    RequestId = r.RequestId,
                    Category = r.Category,
                    Description = r.Description,
                    PhotoUrl = r.PhotoUrl,
                    Status = r.Status,
                    Priority = r.Priority,
                    AssignmentNotes = r.AssignmentNotes,
                    CreatedAt = r.CreatedAt,
                    TenantName = r.Tenant.FullName,
                    TenantPhone = r.Tenant.PhoneNumber,
                    PropertyName = r.Unit.Property.Name,
                    UnitNumber = r.Unit.UnitNumber,
                    Updates = r.Updates
                        .OrderByDescending(u => u.UpdatedAt)
                        .Select(u => new UpdateRowViewModel
                        {
                            StatusUpdate = u.StatusUpdate,
                            Notes = u.Notes,
                            EvidencePhotoUrl = u.EvidencePhotoUrl,
                            StaffName = u.Staff.FullName,
                            UpdatedAt = u.UpdatedAt
                        }).ToList()
                })
                .FirstOrDefaultAsync();

            if (vm == null) return NotFound();

            return View(vm);
        }

        // Notifications + MarkRead are inherited from AppControllerBase.
    }
}
