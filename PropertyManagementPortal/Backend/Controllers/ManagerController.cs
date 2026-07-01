using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertyManagementPortal.Data;
using PropertyManagementPortal.Models;
using PropertyManagementPortal.ViewModels.Manager;
 
namespace PropertyManagementPortal.Controllers
{
    [Authorize(Roles = "PropertyManager")]
    public class ManagerController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
 
        public ManagerController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }
 
        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);
 
            // ── SCOPING PATTERN ──────────────────────────────────────────────
            // A manager only ever sees data belonging to the properties assigned
            // to them. Everything else in this controller reuses these two lists.
 
            // 1. Every property this manager is assigned to (they may have several)
            var propertyIds = await _db.Properties
                .Where(p => p.ManagerId == user!.Id)
                .Select(p => p.PropertyId)
                .ToListAsync();
 
            // 2. Every unit inside those properties
            var units = await _db.Units
                .Where(u => propertyIds.Contains(u.PropertyId))
                .ToListAsync();
 
            var unitIds = units.Select(u => u.UnitId).ToList();
            // ─────────────────────────────────────────────────────────────────
 
            // Pending tenant applications (Tenancy → Unit)
            var pendingApplications = await _db.Tenancies
                .CountAsync(t => unitIds.Contains(t.UnitId) && t.Status == "Pending");
 
            // Rent dues (Payment → Tenancy → Unit)
            var pendingPayments = await _db.Payments
                .CountAsync(p => unitIds.Contains(p.Tenancy.UnitId) && p.Status == "Pending");
 
            var overduePayments = await _db.Payments
                .CountAsync(p => unitIds.Contains(p.Tenancy.UnitId) && p.Status == "Overdue");
 
            // Maintenance requests not yet assigned to any staff (MaintenanceRequest → Unit)
            var unassignedMaintenance = await _db.MaintenanceRequests
                .CountAsync(m => unitIds.Contains(m.UnitId) && m.AssignedStaffId == null);
 
            var vm = new ManagerDashboardViewModel
            {
                ManagerName = user!.FullName,
                TotalProperties = propertyIds.Count,
                TotalUnits = units.Count,
                OccupiedUnits = units.Count(u => u.Status == "Occupied"),
                VacantUnits = units.Count(u => u.Status == "Vacant"),
                PendingApplications = pendingApplications,
                PendingPayments = pendingPayments,
                OverduePayments = overduePayments,
                UnassignedMaintenance = unassignedMaintenance
            };
 
            return View(vm);
        }
    }
}
