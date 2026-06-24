using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PropertyManagementPortal.Models;

namespace PropertyManagementPortal.Controllers
{
    [Authorize(Roles = "PropertyManager")]
    public class ManagerController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public ManagerController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<IActionResult> Dashboard()
        {
            ViewBag.User = await _userManager.GetUserAsync(User);
            return View();
        }
    }
}
