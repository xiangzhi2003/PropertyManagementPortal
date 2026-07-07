using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertyManagementPortal.Data;
using PropertyManagementPortal.Models;

namespace PropertyManagementPortal.ViewComponents
{
    // Renders the header notification bell with the current user's unread count.
    // Self-contained: runs its own query, so any page/layout can drop it in.
    public class NotificationBellViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationBellViewComponent(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var userId = _userManager.GetUserId(HttpContext.User);

            var unread = userId == null
                ? 0
                : await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);

            return View(unread);
        }
    }
}
