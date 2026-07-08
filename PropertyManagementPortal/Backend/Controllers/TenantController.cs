using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using PropertyManagementPortal.Data;
using PropertyManagementPortal.Models;
using PropertyManagementPortal.ViewModels.Tenant;
using Stripe;
using Stripe.Checkout;

namespace PropertyManagementPortal.Controllers
{
    [Authorize(Roles = "Tenant")]
    public class TenantController : AppControllerBase
    {
        private readonly IConfiguration _configuration;

        public TenantController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IConfiguration configuration)
        : base(db, userManager)
        {
            _configuration = configuration;
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
                .Where(t => t.TenantId == user.Id && t.Status == "Approved" && t.Payments.Any(p => p.Status == "Paid"))
                .ToListAsync();
            var unitCount = await _db.Units
                .CountAsync(u => u.Status == "Vacant");
            var applicationCount = await _db.Tenancies
                .CountAsync(t => t.TenantId == user.Id);
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
                CurrentTenancies = currentTenancy,
                UnitCount = unitCount,
                ApplicationCount = applicationCount,
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

            var roleLabel = role == "PropertyManager" ? "Property Manager" : "Maintenance Staff";
            foreach (var admin in await _userManager.GetUsersInRoleAsync("Admin"))
                AddNotification(admin.Id, $"{user.FullName} requested to become a {roleLabel}.");

            await _db.SaveChangesAsync();

            TempData["Success"] = $"Your request to become a {roleLabel} has been submitted. Please wait for admin approval.";
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

        public async Task<IActionResult> Apply(int unitId)
        {
            var unit = await _db.Units
                .Include(u => u.Property)
                .FirstOrDefaultAsync(u => u.UnitId == unitId);

            if (unit == null)
                return Challenge();

            var vm = new ApplyUnitViewModel
            {
                UnitId = unit.UnitId,
                PropertyName = unit.Property.Name,
                UnitNumber = unit.UnitNumber,
                RentAmount = unit.RentAmount,
                Floor = unit.Floor,
                Description = unit.Description ?? "No description",
                StartDate = DateTime.Today,
                EndDate = DateTime.Today
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Apply(ApplyUnitViewModel vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            return View("ConfirmApplication", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmApplication(ApplyUnitViewModel vm)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Challenge();

            var exists = await _db.Tenancies.AnyAsync(t =>
                t.UnitId == vm.UnitId &&
                t.TenantId == user.Id &&
                t.Status == "Pending");

            if (exists)
            {
                TempData["Error"] = "You already applied for this unit.";
                return RedirectToAction(nameof(BrowseUnits));
            }

            var tenancy = new Tenancy
            {
                UnitId = vm.UnitId,
                TenantId = user.Id,
                StartDate = vm.StartDate.ToUniversalTime(),
                EndDate = vm.EndDate.ToUniversalTime(),
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _db.Tenancies.Add(tenancy);

            await _db.SaveChangesAsync();

            TempData["Success"] = "Application submitted successfully.";

            return RedirectToAction(nameof(MyApplications));
        }

        public async Task<IActionResult> MyApplications()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var applications = await _db.Tenancies
                .Include(a => a.Unit)
                .ThenInclude(u => u.Property)
                .Include(t => t.Payments)
                .Where(a => a.TenantId == user.Id)
                .ToListAsync();

            return View(applications);
        }

        public async Task<IActionResult> Maintenance()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var tenancyUnits = await _db.Tenancies
                .Include(t => t.Unit)
                .ThenInclude(u => u.Property)
                .Where(t => t.TenantId == user.Id && t.Status == "Approved")
                .ToListAsync();

            var vm = new MaintenanceRequestViewModel
            {
                Units = tenancyUnits.Select(t => new SelectListItem
                {
                    Value = t.UnitId.ToString(),
                    Text = $"{t.Unit.Property.Name} - Unit {t.Unit.UnitNumber}"
                }).ToList()
            };

            // auto-select first unit
            if (tenancyUnits.Count == 1)
            {
                vm.UnitId = tenancyUnits.First().UnitId;
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Maintenance(MaintenanceRequestViewModel vm)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            if (!ModelState.IsValid)
            {
                // reload dropdown if validation fails
                vm.Units = await _db.Tenancies
                    .Include(t => t.Unit)
                    .ThenInclude(u => u.Property)
                    .Where(t => t.TenantId == user.Id && t.Status == "Approved")
                    .Select(t => new SelectListItem
                    {
                        Value = t.UnitId.ToString(),
                        Text = $"{t.Unit.Property.Name} - Unit {t.Unit.UnitNumber}"
                    })
                    .ToListAsync();

                return View(vm);
            }

            var request = new MaintenanceRequest
            {
                TenantId = user.Id,
                UnitId = vm.UnitId,
                Category = vm.Category,
                Description = vm.Description,
                Status = "Submitted",
                CreatedAt = DateTime.UtcNow
            };

            _db.MaintenanceRequests.Add(request);

            var unit = await _db.Units.Include(u => u.Property).FirstAsync(u => u.UnitId == vm.UnitId);
            foreach (var admin in await _userManager.GetUsersInRoleAsync("Admin"))
                AddNotification(admin.Id, $"New maintenance request ({vm.Category}) at {unit.Property.Name}, Unit {unit.UnitNumber}.");

            await _db.SaveChangesAsync();

            TempData["Success"] = "Maintenance request submitted.";
            return RedirectToAction(nameof(MyMaintenance));
        }

        public async Task<IActionResult> MyMaintenance()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var requests = await _db.MaintenanceRequests
                .Include(m => m.Unit)
                .ThenInclude(u => u.Property)
                .Where(m => m.TenantId == user.Id)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();


            ViewBag.Units = await _db.Tenancies
                .Include(t => t.Unit)
                .ThenInclude(u => u.Property)
                .Where(t => t.TenantId == user.Id 
                        && t.Status == "Approved")
                .Select(t => t.Unit)
                .ToListAsync();


            return View(requests);
        }

        public async Task<IActionResult> MyPayments()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var payments = await _db.Payments
                .Include(p => p.Tenancy)
                    .ThenInclude(t => t.Unit)
                        .ThenInclude(u => u.Property)
                .Where(p => p.Tenancy.TenantId == user.Id)
                .OrderByDescending(p => p.DueDate)
                .Select(p => new PaymentViewModel
                {
                    PaymentId = p.PaymentId,
                    PropertyName = p.Tenancy.Unit.Property.Name,
                    UnitNumber = p.Tenancy.Unit.UnitNumber,
                    Amount = p.Amount,
                    PaymentDate = p.PaymentDate,
                    DueDate = p.DueDate,
                    Status = p.Status
                })
                .ToListAsync();

            // Overdue is derived, not stored: a Pending payment past its due date
            // displays as overdue. Mirrors ManagerController's derive-on-read logic.
            var today = DateTime.UtcNow.Date;
            foreach (var p in payments)
            {
                if (p.Status == "Pending" && p.DueDate.Date < today)
                    p.Status = "Overdue";
            }

            return View(payments);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(int paymentId)
        {
            var payment = await _db.Payments
                .FirstOrDefaultAsync(p => p.PaymentId == paymentId);

            if (payment == null)
                return NotFound();

            StripeConfiguration.ApiKey =
                _configuration["Stripe:SecretKey"];

            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string>
                {
                    "card"
                },

                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        Quantity = 1,

                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = "myr",

                            UnitAmount =
                                (long)(payment.Amount * 100),

                            ProductData =
                                new SessionLineItemPriceDataProductDataOptions
                                {
                                    Name =
                                        $"Rent Payment #{payment.PaymentId}"
                                }
                        }
                    }
                },

                Mode = "payment",

                SuccessUrl =
                    Url.Action(
                        "PaymentSuccess",
                        "Tenant",
                        new { paymentId = payment.PaymentId },
                        Request.Scheme),

                CancelUrl =
                    Url.Action(
                        "MyPayments",
                        "Tenant",
                        null,
                        Request.Scheme)
            };

            var service = new SessionService();

            var session =
                await service.CreateAsync(options);

            return Redirect(session.Url);
        }

        public async Task<IActionResult> PaymentSuccess(int paymentId)
        {
            var payment = await _db.Payments
                .FirstOrDefaultAsync(p => p.PaymentId == paymentId);

            if (payment == null)
                return NotFound();

            payment.Status = "Paid";

            payment.PaymentDate = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["Success"] =
                "Payment completed successfully.";

            return RedirectToAction(nameof(MyPayments));
        }
    }
}
