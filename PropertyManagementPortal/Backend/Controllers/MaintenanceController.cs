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
        private readonly IWebHostEnvironment _env;

        public MaintenanceController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IWebHostEnvironment env)
            : base(db, userManager)
        {
            _env = env;
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

        // ── UPDATE JOB STATUS ────────────────────────────────────────────────
        // The status lifecycle. Each job advances one step at a time; there is no
        // skipping and no going back.
        private static string? NextStatusFor(string current) => current switch
        {
            "Assigned" => "InProgress",
            "InProgress" => "Completed",
            _ => null            // Completed (or anything unexpected) is terminal
        };

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateJob(UpdateJobViewModel vm)
        {
            var userId = _userManager.GetUserId(User);

            var request = await _db.MaintenanceRequests
                .Include(r => r.Unit).ThenInclude(u => u.Property)
                .FirstOrDefaultAsync(r => r.RequestId == vm.RequestId && r.AssignedStaffId == userId);

            if (request == null) return NotFound();

            // Re-derive the allowed next status from the CURRENT DB state — never trust
            // the posted status. This blocks skipping steps or replaying an old form.
            var next = NextStatusFor(request.Status);
            if (next == null)
            {
                TempData["Error"] = "This job is already completed and cannot be updated.";
                return RedirectToAction(nameof(JobDetails), new { id = request.RequestId });
            }

            // Note is always required; evidence photo is required only when completing.
            if (string.IsNullOrWhiteSpace(vm.Notes))
            {
                TempData["Error"] = "Please add a note describing this update.";
                return RedirectToAction(nameof(JobDetails), new { id = request.RequestId });
            }

            if (next == "Completed" && vm.EvidencePhoto == null)
            {
                TempData["Error"] = "A repair evidence photo is required to complete the job.";
                return RedirectToAction(nameof(JobDetails), new { id = request.RequestId });
            }

            string? evidenceUrl = null;
            if (vm.EvidencePhoto != null)
                evidenceUrl = await SaveEvidencePhotoAsync(vm.EvidencePhoto);

            // 1) Write a history row for this transition.
            _db.MaintenanceUpdates.Add(new MaintenanceUpdate
            {
                RequestId = request.RequestId,
                StaffId = userId!,
                StatusUpdate = next,
                Notes = vm.Notes,
                EvidencePhotoUrl = evidenceUrl,
                UpdatedAt = DateTime.UtcNow
            });

            // 2) Advance the request itself.
            request.Status = next;

            await _db.SaveChangesAsync();

            TempData["Success"] = $"Job marked as {next}.";
            return RedirectToAction(nameof(JobDetails), new { id = request.RequestId });
        }

        // Saves an uploaded photo to wwwroot/uploads and returns its web path.
        // Isolated here so swapping to S3 later is a one-method change.
        private async Task<string> SaveEvidencePhotoAsync(IFormFile photo)
        {
            var uploadsDir = Path.Combine(_env.WebRootPath, "uploads");
            Directory.CreateDirectory(uploadsDir);

            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(photo.FileName)}";
            var fullPath = Path.Combine(uploadsDir, fileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await photo.CopyToAsync(stream);
            }

            return $"/uploads/{fileName}";
        }
    }
}
