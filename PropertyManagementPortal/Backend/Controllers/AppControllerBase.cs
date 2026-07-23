using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertyManagementPortal.Data;
using PropertyManagementPortal.Models;
using PropertyManagementPortal.ViewModels.Shared;

namespace PropertyManagementPortal.Controllers
{
    // Shared base for every role controller. Holds the common dependencies and
    // the cross-cutting Notifications actions (identical for all roles). No
    // [Authorize] here — each derived controller keeps its own role attribute.
    public abstract class AppControllerBase : Controller
    {
        protected readonly ApplicationDbContext _db;
        protected readonly UserManager<ApplicationUser> _userManager;

        protected AppControllerBase(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // AWS runs the server in UTC but the business operates in UTC+8. Any
        // "is this date today / in the past / in the future" comparison must use
        // Malaysian local time, otherwise the answer is wrong between midnight
        private static readonly TimeZoneInfo MalaysiaTimeZone = ResolveMalaysiaTimeZone();

        private static TimeZoneInfo ResolveMalaysiaTimeZone()
        {
            try
            {
                // Linux / AWS Elastic Beanstalk
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur");
            }
            catch (TimeZoneNotFoundException)
            {
                // Windows dev machines use the legacy id (same offset, no DST)
                return TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
            }
        }

        // Today's date in Malaysian local time. Available to every role controller.
        protected static DateTime TodayLocal() =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, MalaysiaTimeZone).Date;

        // Queues a notification for a user. Does NOT save — the caller commits it
        // inside its own SaveChangesAsync so it shares that action's transaction.
        // (protected, so it is never routable as an action.)
        protected void AddNotification(string userId, string message)
        {
            _db.Notifications.Add(new Notification
            {
                UserId = userId,
                Message = message
            });
        }

        // GET /{Role}/Notifications — the inherited action resolves its view by the
        // runtime controller name, so each role renders under its own layout.
        public async Task<IActionResult> Notifications(string? readFilter)
        {
            var userId = _userManager.GetUserId(User);

            var query = _db.Notifications.Where(n => n.UserId == userId);

            if (readFilter == "Unread")
                query = query.Where(n => !n.IsRead);

            var rows = await query
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new NotificationRowViewModel
                {
                    NotificationId = n.NotificationId,
                    Message = n.Message,
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt
                })
                .ToListAsync();

            // Counts reflect ALL of the user's notifications, ignoring the filter.
            var vm = new NotificationsViewModel
            {
                Notifications = rows,
                ReadFilter = readFilter,
                TotalCount = await _db.Notifications.CountAsync(n => n.UserId == userId),
                UnreadCount = await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead)
            };

            return View(vm);
        }

        // POST /{Role}/MarkRead — flips one notification to read.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkRead(int id)
        {
            var userId = _userManager.GetUserId(User);

            // Ownership guard: only the current user's own notification.
            var notification = await _db.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == id && n.UserId == userId);

            if (notification != null && !notification.IsRead)
            {
                notification.IsRead = true;
                await _db.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Notifications));
        }
    }
}