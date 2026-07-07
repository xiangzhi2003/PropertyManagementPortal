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

            // Completed-jobs trend: count of Completed updates per month over the
            // last 6 months (including empty months), oldest first.
            var trendStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-5);
            var completedDates = await _db.MaintenanceUpdates
                .Where(u => u.StaffId == userId
                         && u.StatusUpdate == "Completed"
                         && u.UpdatedAt >= trendStart)
                .Select(u => u.UpdatedAt)
                .ToListAsync();

            var completedTrend = new List<MonthlyCount>();
            for (var i = 0; i < 6; i++)
            {
                var m = trendStart.AddMonths(i);
                completedTrend.Add(new MonthlyCount
                {
                    Label = m.ToString("MMM"),
                    Count = completedDates.Count(d => d.Year == m.Year && d.Month == m.Month)
                });
            }

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
                CompletedThisMonth = completedThisMonth,
                CompletedTrend = completedTrend
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

            // "Active" is a pseudo-filter covering the two in-flight statuses.
            if (status == "Active")
                query = query.Where(r => r.Status == "Assigned" || r.Status == "InProgress");
            else if (!string.IsNullOrWhiteSpace(status))
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
        // Allowed forward moves. Only progress is permitted — never backwards, and a
        // Completed job is terminal. InProgress may be skipped (Assigned → Completed).
        private static bool IsValidTransition(string current, string target) => (current, target) switch
        {
            ("Assigned", "InProgress") => true,
            ("Assigned", "Completed") => true,
            ("InProgress", "Completed") => true,
            _ => false
        };

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateJob(UpdateJobViewModel vm)
        {
            var userId = _userManager.GetUserId(User);
            var user = await _userManager.GetUserAsync(User);

            var request = await _db.MaintenanceRequests
                .Include(r => r.Unit).ThenInclude(u => u.Property)
                .FirstOrDefaultAsync(r => r.RequestId == vm.RequestId && r.AssignedStaffId == userId);

            if (request == null) return NotFound();

            // A completed job is terminal.
            if (request.Status == "Completed")
            {
                TempData["Error"] = "This job is already completed and cannot be updated.";
                return RedirectToAction(nameof(JobDetails), new { id = request.RequestId });
            }

            // Validate the chosen move against the CURRENT DB state — never trust the
            // posted status blindly. Forward-only; skipping InProgress is allowed.
            if (!IsValidTransition(request.Status, vm.TargetStatus))
            {
                TempData["Error"] = "That status change isn't allowed.";
                return RedirectToAction(nameof(JobDetails), new { id = request.RequestId });
            }
            var next = vm.TargetStatus;

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

            // If a photo was supplied, it must pass the format + size checks.
            if (vm.EvidencePhoto != null)
            {
                var photoError = ValidateEvidencePhoto(vm.EvidencePhoto);
                if (photoError != null)
                {
                    TempData["Error"] = photoError;
                    return RedirectToAction(nameof(JobDetails), new { id = request.RequestId });
                }
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

            // 3) Notify the tenant, and the property manager, of the change.
            //    Queued via the shared helper; committed in the SaveChanges below.
            AddNotification(request.TenantId,
                $"Your maintenance {request.Category} request at Unit {request.Unit.UnitNumber} is now {next}.");

            var managerId = request.Unit.Property.ManagerId;
            if (!string.IsNullOrEmpty(managerId))
                AddNotification(managerId,
                    $"{user!.FullName} updated {request.Category} maintenance at Unit {request.Unit.UnitNumber} to {next}.");

            await _db.SaveChangesAsync();

            TempData["Success"] = $"Job marked as {next}.";
            return RedirectToAction(nameof(JobDetails), new { id = request.RequestId });
        }

        // Allowed evidence photo formats and size cap.
        private static readonly string[] AllowedPhotoExtensions = [".jpg", ".jpeg", ".png", ".webp"];
        private const long MaxPhotoBytes = 5 * 1024 * 1024; // 5 MB

        // Returns an error message if the upload is not an accepted image, else null.
        private static string? ValidateEvidencePhoto(IFormFile photo)
        {
            if (photo.Length == 0)
                return "The selected file is empty.";

            if (photo.Length > MaxPhotoBytes)
                return "The photo must be 5 MB or smaller.";

            var ext = Path.GetExtension(photo.FileName).ToLowerInvariant();
            if (!AllowedPhotoExtensions.Contains(ext))
                return "Only JPG, PNG, or WEBP images are allowed.";

            // Content-type guard in addition to the extension check.
            if (!photo.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return "The uploaded file is not a valid image.";

            return null;
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
