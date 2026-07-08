using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PropertyManagementPortal.Data;
using PropertyManagementPortal.Models;
using PropertyManagementPortal.ViewModels.Admin;
using System.Net.Http.Json;

namespace PropertyManagementPortal.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : AppControllerBase
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public AdminController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager,
            IHttpClientFactory httpClientFactory, IConfiguration configuration)
            : base(db, userManager)
        {
            _roleManager = roleManager;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        private async Task LogAsync(string action, string entityType, string? entityId = null, string? details = null)
        {
            var user = await _userManager.GetUserAsync(User);
            _db.ActivityLogs.Add(new ActivityLog
            {
                UserId = user!.Id,
                UserName = user.FullName,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Details = details,
                Timestamp = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }

        // ── Global Search ────────────────────────────────────────────────────────
        public async Task<IActionResult> GlobalSearch(string? q)
        {
            var vm = new GlobalSearchViewModel { Query = q };
            if (string.IsNullOrWhiteSpace(q))
                return View(vm);

            var term = q.Trim().ToLower();

            var matchedUsers = _userManager.Users
                .Where(u => u.FullName.ToLower().Contains(term) || (u.Email != null && u.Email.ToLower().Contains(term)))
                .Take(10)
                .ToList();
            foreach (var u in matchedUsers)
            {
                var role = (await _userManager.GetRolesAsync(u)).FirstOrDefault() ?? "No Role";
                vm.Users.Add(new UserRowViewModel
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Email = u.Email ?? "",
                    PhoneNumber = u.PhoneNumber,
                    Role = role,
                    IsActive = u.IsActive,
                    CreatedAt = u.CreatedAt
                });
            }

            vm.Properties = await _db.Properties
                .Include(p => p.Manager)
                .Include(p => p.Units)
                .Where(p => p.Name.ToLower().Contains(term) || p.Address.ToLower().Contains(term))
                .Take(10)
                .ToListAsync();

            var today = DateTime.UtcNow.Date;
            var paymentMatches = await _db.Payments
                .Include(p => p.Tenancy).ThenInclude(t => t.Tenant)
                .Include(p => p.Tenancy).ThenInclude(t => t.Unit).ThenInclude(u => u.Property).ThenInclude(pr => pr.Manager)
                .Where(p => p.Status != "Paid" && p.Tenancy.Tenant.FullName.ToLower().Contains(term))
                .Select(p => new PaymentDetailRow
                {
                    TenantId = p.Tenancy.TenantId,
                    TenantName = p.Tenancy.Tenant.FullName,
                    TenantEmail = p.Tenancy.Tenant.Email ?? "",
                    TenantPhone = p.Tenancy.Tenant.PhoneNumber ?? "",
                    PropertyName = p.Tenancy.Unit.Property.Name,
                    UnitNumber = p.Tenancy.Unit.UnitNumber,
                    ManagerName = p.Tenancy.Unit.Property.Manager != null ? p.Tenancy.Unit.Property.Manager.FullName : null,
                    Amount = p.Amount,
                    DueDate = p.DueDate,
                    Status = p.Status
                })
                .Take(10)
                .ToListAsync();

            foreach (var p in paymentMatches)
                if (p.Status == "Pending" && p.DueDate.Date < today)
                    p.Status = "Overdue";
            vm.Payments = paymentMatches;

            return View(vm);
        }

        // ── Dashboard (includes what used to be the separate Reports page) ────────
        public async Task<IActionResult> Dashboard()
        {
            var managers = await _userManager.GetUsersInRoleAsync("PropertyManager");
            var tenants = await _userManager.GetUsersInRoleAsync("Tenant");
            var staff = await _userManager.GetUsersInRoleAsync("MaintenanceStaff");
            var admin = await _userManager.GetUserAsync(User);

            // Overdue is derived, not stored: a Pending payment past its due date counts
            // as overdue. Mirrors the same derive-on-read logic ManagerController uses.
            // Fetched with full property/unit/tenant detail so the dashboard can show
            // exactly which payment and property each unpaid record belongs to.
            var today = DateTime.UtcNow.Date;
            var unpaidPayments = await _db.Payments
                .Include(p => p.Tenancy).ThenInclude(t => t.Tenant)
                .Include(p => p.Tenancy).ThenInclude(t => t.Unit).ThenInclude(u => u.Property).ThenInclude(pr => pr.Manager)
                .Where(p => p.Status != "Paid")
                .Select(p => new PaymentDetailRow
                {
                    TenantId = p.Tenancy.TenantId,
                    TenantName = p.Tenancy.Tenant.FullName,
                    TenantEmail = p.Tenancy.Tenant.Email ?? "",
                    TenantPhone = p.Tenancy.Tenant.PhoneNumber ?? "",
                    PropertyName = p.Tenancy.Unit.Property.Name,
                    UnitNumber = p.Tenancy.Unit.UnitNumber,
                    ManagerName = p.Tenancy.Unit.Property.Manager != null ? p.Tenancy.Unit.Property.Manager.FullName : null,
                    Amount = p.Amount,
                    DueDate = p.DueDate,
                    Status = p.Status
                })
                .ToListAsync();

            foreach (var p in unpaidPayments)
                if (p.Status == "Pending" && p.DueDate.Date < today)
                    p.Status = "Overdue";

            unpaidPayments = unpaidPayments
                .OrderBy(p => p.Status == "Overdue" ? 0 : 1)
                .ThenBy(p => p.DueDate)
                .ToList();

            var overduePayments = unpaidPayments.Count(p => p.Status == "Overdue");
            var pendingPaymentsCount = unpaidPayments.Count - overduePayments;

            // Occupancy broken down per property, so a single overall rate isn't the
            // only view — an admin can see exactly which property is under-occupied.
            var occupancyByProperty = await _db.Properties
                .Select(p => new PropertyOccupancyRow
                {
                    PropertyName = p.Name,
                    TotalUnits = p.Units.Count,
                    OccupiedUnits = p.Units.Count(u => u.Status == "Occupied")
                })
                .OrderByDescending(p => p.TotalUnits)
                .ToListAsync();

            // Open maintenance requests with full context, so an admin can see exactly
            // which property/unit/tenant each still-open job belongs to.
            var openMaintenanceDetails = await _db.MaintenanceRequests
                .Include(m => m.Tenant)
                .Include(m => m.AssignedStaff)
                .Include(m => m.Unit).ThenInclude(u => u.Property).ThenInclude(p => p.Manager)
                .Where(m => m.Status != "Completed")
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new MaintenanceDetailRow
                {
                    RequestId = m.RequestId,
                    Category = m.Category,
                    PropertyName = m.Unit.Property.Name,
                    UnitNumber = m.Unit.UnitNumber,
                    TenantId = m.TenantId,
                    TenantName = m.Tenant.FullName,
                    TenantPhone = m.Tenant.PhoneNumber ?? "",
                    Status = m.Status,
                    Priority = m.Priority,
                    ManagerName = m.Unit.Property.Manager != null ? m.Unit.Property.Manager.FullName : null,
                    AssignedStaffId = m.AssignedStaffId,
                    AssignedStaffName = m.AssignedStaff != null ? m.AssignedStaff.FullName : null,
                    CreatedAt = m.CreatedAt
                })
                .ToListAsync();

            // ── Trends (last 6 months, oldest first) ────────────────────────────
            var now = DateTime.UtcNow;
            var trendStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-5);

            var paidInRange = await _db.Payments
                .Where(p => p.Status == "Paid" && p.PaymentDate != null && p.PaymentDate >= trendStart)
                .Select(p => new { p.PaymentDate, p.Amount })
                .ToListAsync();

            var revenueTrend = new List<MonthlyAmount>();
            for (var i = 0; i < 6; i++)
            {
                var m = trendStart.AddMonths(i);
                var sum = paidInRange
                    .Where(p => p.PaymentDate!.Value.Year == m.Year && p.PaymentDate.Value.Month == m.Month)
                    .Sum(p => p.Amount);
                revenueTrend.Add(new MonthlyAmount { Label = m.ToString("MMM"), Amount = sum });
            }

            // Occupancy trend is approximated from Approved tenancy date ranges overlapping
            // each month (against the CURRENT total unit count — there's no historical
            // snapshot of unit counts, so this is a best-effort trend, not exact history).
            var totalUnitsNow = await _db.Units.CountAsync();
            var approvedTenancies = await _db.Tenancies
                .Where(t => t.Status == "Approved")
                .Select(t => new { t.StartDate, t.EndDate })
                .ToListAsync();

            var occupancyTrend = new List<MonthlyRate>();
            for (var i = 0; i < 6; i++)
            {
                var m = trendStart.AddMonths(i);
                var monthStart = m;
                var monthEnd = m.AddMonths(1).AddDays(-1);
                var activeCount = approvedTenancies.Count(t => t.StartDate <= monthEnd && t.EndDate >= monthStart);
                var rate = totalUnitsNow == 0 ? 0 : Math.Round((double)activeCount / totalUnitsNow * 100, 1);
                occupancyTrend.Add(new MonthlyRate { Label = m.ToString("MMM"), Rate = rate });
            }

            var vm = new DashboardViewModel
            {
                AdminName = admin!.FullName,
                TotalUsers = managers.Count + tenants.Count + staff.Count,
                TotalProperties = await _db.Properties.CountAsync(),
                PendingMaintenance = await _db.MaintenanceRequests
                    .CountAsync(r => r.Status == "Submitted" || r.Status == "Assigned" || r.Status == "InProgress"),
                OverduePayments = overduePayments,

                // Occupancy
                TotalUnits = await _db.Units.CountAsync(),
                OccupiedUnits = await _db.Units.CountAsync(u => u.Status == "Occupied"),
                VacantUnits = await _db.Units.CountAsync(u => u.Status == "Vacant"),
                OccupancyByProperty = occupancyByProperty,

                // Payments
                TotalPayments = await _db.Payments.CountAsync(),
                PaidPayments = await _db.Payments.CountAsync(p => p.Status == "Paid"),
                PendingPayments = pendingPaymentsCount,
                UnpaidPaymentDetails = unpaidPayments,

                // Maintenance
                SubmittedRequests = await _db.MaintenanceRequests.CountAsync(m => m.Status == "Submitted"),
                AssignedRequests = await _db.MaintenanceRequests.CountAsync(m => m.Status == "Assigned"),
                InProgressRequests = await _db.MaintenanceRequests.CountAsync(m => m.Status == "InProgress"),
                CompletedRequests = await _db.MaintenanceRequests.CountAsync(m => m.Status == "Completed"),
                OpenMaintenanceDetails = openMaintenanceDetails,

                // Trends
                RevenueTrend = revenueTrend,
                OccupancyTrend = occupancyTrend
            };

            ViewBag.RecentLogs = await _db.ActivityLogs
                .OrderByDescending(l => l.Timestamp)
                .Take(5)
                .ToListAsync();

            return View(vm);
        }

        // ── AI Property Report Summary ──────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateAiSummary()
        {
            var apiKey = _configuration["Gemini:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey) || apiKey.StartsWith("YOUR_") || apiKey.StartsWith("PASTE_"))
                return Json(new { error = "Gemini API key is not configured. Add it to appsettings.json under Gemini:ApiKey." });

            var today = DateTime.UtcNow.Date;

            // ── Gather the full portfolio picture ───────────────────────────────
            var properties = await _db.Properties
                .Include(p => p.Manager)
                .Include(p => p.Units)
                .ToListAsync();

            var totalProperties = properties.Count;
            var totalUnits = properties.Sum(p => p.Units.Count);
            var occupiedUnits = properties.Sum(p => p.Units.Count(u => u.Status == "Occupied"));
            var vacantUnits = totalUnits - occupiedUnits;
            var occupancyRate = totalUnits == 0 ? 0 : Math.Round((double)occupiedUnits / totalUnits * 100, 1);

            // Approved tenancies → active tenants (overall + per property)
            var approvedTenancies = await _db.Tenancies
                .Where(t => t.Status == "Approved")
                .Select(t => new { t.TenantId, t.Unit.PropertyId })
                .ToListAsync();
            var activeTenants = approvedTenancies.Select(t => t.TenantId).Distinct().Count();

            // Payments (overdue is derived, per property + overall)
            var totalPayments = await _db.Payments.CountAsync();
            var paidPayments = await _db.Payments.CountAsync(p => p.Status == "Paid");
            var unpaid = await _db.Payments
                .Where(p => p.Status == "Pending")
                .Select(p => new { p.DueDate, p.Amount, p.Tenancy.Unit.PropertyId })
                .ToListAsync();
            var overdue = unpaid.Where(x => x.DueDate.Date < today).ToList();
            var overdueCount = overdue.Count;
            var overdueAmount = overdue.Sum(x => x.Amount);
            var pendingCount = unpaid.Count - overdueCount;
            var collectionRate = totalPayments == 0 ? 0 : Math.Round((double)paidPayments / totalPayments * 100, 1);

            // Maintenance (open per property + per staff)
            var submittedCount = await _db.MaintenanceRequests.CountAsync(m => m.Status == "Submitted");
            var assignedCount = await _db.MaintenanceRequests.CountAsync(m => m.Status == "Assigned");
            var inProgressCount = await _db.MaintenanceRequests.CountAsync(m => m.Status == "InProgress");
            var completedCount = await _db.MaintenanceRequests.CountAsync(m => m.Status == "Completed");
            var openMaintenance = await _db.MaintenanceRequests
                .Where(m => m.Status != "Completed")
                .Select(m => new { m.Unit.PropertyId, m.AssignedStaffId })
                .ToListAsync();
            var completedByStaff = (await _db.MaintenanceRequests
                .Where(m => m.Status == "Completed" && m.AssignedStaffId != null)
                .GroupBy(m => m.AssignedStaffId)
                .Select(g => new { StaffId = g.Key, Count = g.Count() })
                .ToListAsync())
                .ToDictionary(x => x.StaffId!, x => x.Count);

            // Team
            var managers = await _userManager.GetUsersInRoleAsync("PropertyManager");
            var staff = await _userManager.GetUsersInRoleAsync("MaintenanceStaff");

            // Per-property factual rows
            var propertyRows = properties.Select(p => new
            {
                name = p.Name,
                manager = p.Manager != null ? p.Manager.FullName : "Unassigned",
                units = p.Units.Count,
                occupied = p.Units.Count(u => u.Status == "Occupied"),
                tenants = approvedTenancies.Where(t => t.PropertyId == p.PropertyId).Select(t => t.TenantId).Distinct().Count(),
                overdue = overdue.Count(x => x.PropertyId == p.PropertyId),
                openMaintenance = openMaintenance.Count(x => x.PropertyId == p.PropertyId)
            }).ToList();

            var managerRows = managers.Select(m => new
            {
                name = m.FullName,
                properties = properties.Count(p => p.ManagerId == m.Id),
                units = properties.Where(p => p.ManagerId == m.Id).Sum(p => p.Units.Count)
            }).ToList();

            var staffRows = staff.Select(s => new
            {
                name = s.FullName,
                openJobs = openMaintenance.Count(x => x.AssignedStaffId == s.Id),
                completed = completedByStaff.GetValueOrDefault(s.Id, 0)
            }).ToList();

            // Factual data block returned to the client and rendered as tables (100%
            // accurate — the AI narrative is layered on top, never replaces these numbers).
            var facts = new
            {
                totalProperties,
                totalUnits,
                occupiedUnits,
                vacantUnits,
                occupancyRate,
                activeTenants,
                totalManagers = managers.Count,
                totalStaff = staff.Count,
                totalPayments,
                paidPayments,
                pendingCount,
                overdueCount,
                overdueAmount,
                collectionRate,
                submittedCount,
                assignedCount,
                inProgressCount,
                completedCount,
                properties = propertyRows,
                managers = managerRows,
                staff = staffRows
            };

            var occupancyRateStr = totalUnits == 0 ? "n/a" : $"{occupancyRate}%";
            var collectionRateStr = totalPayments == 0 ? "n/a" : $"{collectionRate}%";

            var propertyLines = propertyRows.Count == 0
                ? "  (no properties registered yet)"
                : string.Join("\n", propertyRows.Select(p =>
                    $"  - \"{p.name}\" (manager: {p.manager}): {p.units} units, {p.occupied} occupied, {p.tenants} active tenants, {p.overdue} overdue payments, {p.openMaintenance} open maintenance requests."));
            var managerLines = managerRows.Count == 0
                ? "  (no property managers)"
                : string.Join("\n", managerRows.Select(m => $"  - {m.name}: manages {m.properties} propert(y/ies), {m.units} units."));
            var staffLines = staffRows.Count == 0
                ? "  (no maintenance staff)"
                : string.Join("\n", staffRows.Select(s => $"  - {s.name}: {s.openJobs} open job(s), {s.completed} completed."));

            var prompt = $"""
                You are a senior property-management operations analyst preparing an executive briefing for the portfolio administrator.
                Analyse the real portfolio data below and produce a professional, structured status report the administrator can read at a glance to understand everything happening across the portfolio.

                Guidelines:
                - Write in a confident, professional, executive tone — like a real operations report, not casual chat.
                - Base every statement strictly on the data provided. Never invent figures, names, or properties not listed.
                - Refer to specific properties, managers, and staff by name where it adds insight (e.g. which property has overdue payments, which manager oversees it, which staff member is overloaded or idle).
                - For each operational area (Occupancy, Rent Collection, Maintenance), assess its health and explain WHY in 2-3 sentences, citing the relevant numbers and naming the specific properties/people involved where relevant.
                - Choose a status for each area: "Good" (healthy), "Warning" (needs monitoring), or "Critical" (needs immediate action). Judge realistically — any overdue payments or unassigned maintenance are at least a Warning; a large overdue share, low occupancy, or an unassigned property manager is Critical.
                - The executive summary should be 3-4 sentences giving the whole portfolio picture at a glance, including the scale (properties, units, tenants) and the single most important thing needing attention.
                - overallHealth is a short verdict: "Excellent", "Good", "Fair", or "Needs Attention".
                - priorityActions: 3-5 concrete, specific next steps ordered most-urgent first, naming the specific property/manager/staff involved (e.g. "Follow up on the 3 overdue accounts at Sunrise Condo"). If everything is healthy, give proactive suggestions instead.

                PORTFOLIO OVERVIEW
                - Properties: {totalProperties}. Units: {totalUnits} ({occupiedUnits} occupied, {vacantUnits} vacant, {occupancyRateStr} occupancy). Active tenants: {activeTenants}.
                - Team: {managers.Count} property manager(s), {staff.Count} maintenance staff.
                - Rent collection: {totalPayments} total payment records — {paidPayments} paid ({collectionRateStr} collection rate), {pendingCount} pending (not yet due), {overdueCount} overdue totalling RM {overdueAmount:N2}.
                - Maintenance: {submittedCount} submitted but unassigned, {assignedCount} assigned, {inProgressCount} in progress, {completedCount} completed.

                PER-PROPERTY BREAKDOWN
                {propertyLines}

                PROPERTY MANAGERS
                {managerLines}

                MAINTENANCE STAFF
                {staffLines}
                """;

            // Force Gemini to return JSON matching this shape, so we can render it as a
            // styled report (badges, section cards, action list) instead of raw text.
            var responseSchema = new
            {
                type = "object",
                properties = new
                {
                    executiveSummary = new { type = "string" },
                    overallHealth = new { type = "string", @enum = new[] { "Excellent", "Good", "Fair", "Needs Attention" } },
                    sections = new
                    {
                        type = "array",
                        items = new
                        {
                            type = "object",
                            properties = new
                            {
                                title = new { type = "string" },
                                status = new { type = "string", @enum = new[] { "Good", "Warning", "Critical" } },
                                insight = new { type = "string" }
                            },
                            required = new[] { "title", "status", "insight" }
                        }
                    },
                    priorityActions = new { type = "array", items = new { type = "string" } }
                },
                required = new[] { "executiveSummary", "overallHealth", "sections", "priorityActions" }
            };

            try
            {
                var client = _httpClientFactory.CreateClient();
                var requestBody = new
                {
                    contents = new[]
                    {
                        new { parts = new[] { new { text = prompt } } }
                    },
                    generationConfig = new
                    {
                        responseMimeType = "application/json",
                        responseSchema
                    }
                };

                var response = await client.PostAsJsonAsync(
                    $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-lite:generateContent?key={apiKey}",
                    requestBody);

                if (!response.IsSuccessStatusCode)
                    return Json(new { error = $"Gemini API request failed ({(int)response.StatusCode})." });

                using var doc = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var reportJson = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                if (string.IsNullOrWhiteSpace(reportJson))
                    return Json(new { error = "The AI returned an empty report. Please try again." });

                // Deserialize into a strong type so it survives past this method's scope
                // (a raw JsonElement would be tied to the disposed JsonDocument).
                var report = System.Text.Json.JsonSerializer.Deserialize<AiReport>(reportJson, _aiJsonOptions);

                return Json(new { report, facts });
            }
            catch (Exception)
            {
                return Json(new { error = "Could not reach the AI service. Please try again." });
            }
        }

        private static readonly System.Text.Json.JsonSerializerOptions _aiJsonOptions =
            new() { PropertyNameCaseInsensitive = true };

        private record AiReport(
            string ExecutiveSummary,
            string OverallHealth,
            List<AiReportSection> Sections,
            List<string> PriorityActions);

        private record AiReportSection(string Title, string Status, string Insight);

        // Old bookmarks/links to /Admin/Reports still work — Reports is now part of Dashboard.
        public IActionResult Reports() => RedirectToAction(nameof(Dashboard));

        // ── Users ────────────────────────────────────────────────────────────────
        public async Task<IActionResult> Users(string? search, string? role, string? status)
        {
            var allUsers = _userManager.Users.ToList();
            var rows = new List<UserRowViewModel>();

            foreach (var u in allUsers)
            {
                var roles = await _userManager.GetRolesAsync(u);
                var userRole = roles.FirstOrDefault() ?? "No Role";

                if (!string.IsNullOrEmpty(role) && userRole != role) continue;
                if (!string.IsNullOrEmpty(status))
                {
                    if (status == "Active" && !u.IsActive) continue;
                    if (status == "Inactive" && u.IsActive) continue;
                }
                if (!string.IsNullOrEmpty(search))
                {
                    var s = search.ToLower();
                    if (!u.FullName.ToLower().Contains(s) && !u.Email!.ToLower().Contains(s)) continue;
                }

                rows.Add(new UserRowViewModel
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Email = u.Email!,
                    PhoneNumber = u.PhoneNumber,
                    Role = userRole,
                    IsActive = u.IsActive,
                    CreatedAt = u.CreatedAt
                });
            }

            return View(new UserListViewModel
            {
                Users = rows.OrderBy(r => r.FullName).ToList(),
                SearchTerm = search,
                RoleFilter = role,
                StatusFilter = status
            });
        }

        public IActionResult CreateUser() => View(new CreateUserViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(CreateUserViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var validRoles = new[] { "Admin", "PropertyManager", "Tenant", "MaintenanceStaff" };
            if (!validRoles.Contains(model.Role))
            {
                ModelState.AddModelError("Role", "Invalid role selected.");
                return View(model);
            }

            var user = new ApplicationUser
            {
                FullName = model.FullName,
                UserName = model.Email,
                Email = model.Email,
                PhoneNumber = model.PhoneNumber,
                EmailConfirmed = true,
                IsActive = true
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
                return View(model);
            }

            await _userManager.AddToRoleAsync(user, model.Role);
            await LogAsync("Created User", "User", user.Id, $"{model.Email} ({model.Role})");
            TempData["Success"] = $"User {model.FullName} created successfully.";
            return RedirectToAction(nameof(Users));
        }

        public async Task<IActionResult> EditUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            return View(new EditUserViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email!,
                PhoneNumber = user.PhoneNumber,
                Role = roles.FirstOrDefault() ?? "",
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(EditUserViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null) return NotFound();

            var currentRoles = await _userManager.GetRolesAsync(user);
            var isAdmin = currentRoles.Contains("Admin");

            user.FullName = model.FullName;
            user.PhoneNumber = model.PhoneNumber;
            if (!isAdmin) user.IsActive = model.IsActive;

            await _userManager.UpdateAsync(user);

            if (!isAdmin && model.Role != currentRoles.FirstOrDefault())
            {
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
                await _userManager.AddToRoleAsync(user, model.Role);
            }

            await LogAsync("Edited User", "User", user.Id, model.Email);
            TempData["Success"] = $"User {model.FullName} updated successfully.";
            return RedirectToAction(nameof(Users));
        }

        public async Task<IActionResult> ViewUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            var tenancyCount = await _db.Tenancies.CountAsync(t => t.TenantId == id);
            var maintenanceCount = await _db.MaintenanceRequests.CountAsync(m => m.TenantId == id);
            var outstandingAmount = await _db.Payments
                .Where(p => p.Tenancy.TenantId == id && p.Status != "Paid")
                .SumAsync(p => (decimal?)p.Amount) ?? 0;

            ViewBag.Role = roles.FirstOrDefault() ?? "No Role";
            ViewBag.TenancyCount = tenancyCount;
            ViewBag.MaintenanceCount = maintenanceCount;
            ViewBag.OutstandingAmount = outstandingAmount;
            // Actions performed ON this account (Created/Edited/Deleted User), not
            // actions this user performed themselves — matches EntityId, not UserId.
            ViewBag.RecentActivity = await _db.ActivityLogs
                .Where(l => l.EntityType == "User" && l.EntityId == id)
                .OrderByDescending(l => l.Timestamp)
                .Take(5)
                .ToListAsync();
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains("Admin"))
            {
                TempData["Error"] = "Cannot delete an Admin account.";
                return RedirectToAction(nameof(Users));
            }

            var hasTenancies = await _db.Tenancies.AnyAsync(t => t.TenantId == id);
            var hasRequests = await _db.MaintenanceRequests.AnyAsync(m => m.TenantId == id || m.AssignedStaffId == id);
            var hasManagedProperties = await _db.Properties.AnyAsync(p => p.ManagerId == id);
            var hasMaintenanceUpdates = await _db.MaintenanceUpdates.AnyAsync(u => u.StaffId == id);

            if (hasTenancies || hasRequests)
            {
                TempData["Error"] = "Cannot delete user with active tenancies or maintenance requests.";
                return RedirectToAction(nameof(Users));
            }

            if (hasManagedProperties)
            {
                TempData["Error"] = "Cannot delete a user who is assigned as a property manager. Reassign or unassign their properties first.";
                return RedirectToAction(nameof(Users));
            }

            if (hasMaintenanceUpdates)
            {
                TempData["Error"] = "Cannot delete a user who has a maintenance job history (status updates).";
                return RedirectToAction(nameof(Users));
            }

            var name = user.FullName;
            var email = user.Email;

            try
            {
                var result = await _userManager.DeleteAsync(user);
                if (!result.Succeeded)
                {
                    TempData["Error"] = "Could not delete this user: " + string.Join(" ", result.Errors.Select(e => e.Description));
                    return RedirectToAction(nameof(Users));
                }
            }
            catch (DbUpdateException)
            {
                TempData["Error"] = "Cannot delete this user because other records still reference their account.";
                return RedirectToAction(nameof(Users));
            }

            await LogAsync("Deleted User", "User", id, $"{email}");
            TempData["Success"] = $"User {name} has been deleted.";
            return RedirectToAction(nameof(Users));
        }

        // ── Properties ───────────────────────────────────────────────────────────
        public async Task<IActionResult> Properties(string? search, string? type, string? status)
        {
            var query = _db.Properties.Include(p => p.Manager).Include(p => p.Units).AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(p => p.Name.ToLower().Contains(search.ToLower()) ||
                                         p.Address.ToLower().Contains(search.ToLower()));
            if (!string.IsNullOrEmpty(type))
                query = query.Where(p => p.Type == type);
            if (!string.IsNullOrEmpty(status))
                query = query.Where(p => p.Status == status);

            ViewBag.Search = search;
            ViewBag.TypeFilter = type;
            ViewBag.StatusFilter = status;
            return View(await query.OrderBy(p => p.Name).ToListAsync());
        }

        public async Task<IActionResult> AddProperty()
        {
            return View(new PropertyFormViewModel
            {
                ManagerOptions = await GetManagerOptionsAsync()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddProperty(PropertyFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.ManagerOptions = await GetManagerOptionsAsync();
                return View(model);
            }

            var property = new Property
            {
                Name = model.Name,
                Address = model.Address,
                Type = model.Type,
                ManagerId = string.IsNullOrEmpty(model.ManagerId) ? null : model.ManagerId,
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };

            _db.Properties.Add(property);
            await _db.SaveChangesAsync();
            await LogAsync("Added Property", "Property", property.PropertyId.ToString(), model.Name);
            TempData["Success"] = $"Property '{model.Name}' added successfully.";
            return RedirectToAction(nameof(Properties));
        }

        public async Task<IActionResult> EditProperty(int id)
        {
            var property = await _db.Properties.FindAsync(id);
            if (property == null) return NotFound();

            return View(new PropertyFormViewModel
            {
                PropertyId = property.PropertyId,
                Name = property.Name,
                Address = property.Address,
                Type = property.Type,
                ManagerId = property.ManagerId,
                ManagerOptions = await GetManagerOptionsAsync()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProperty(int id, PropertyFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.ManagerOptions = await GetManagerOptionsAsync();
                return View(model);
            }

            var property = await _db.Properties.FindAsync(id);
            if (property == null) return NotFound();

            property.Name = model.Name;
            property.Address = model.Address;
            property.Type = model.Type;
            property.ManagerId = string.IsNullOrEmpty(model.ManagerId) ? null : model.ManagerId;

            await _db.SaveChangesAsync();
            await LogAsync("Edited Property", "Property", id.ToString(), model.Name);
            TempData["Success"] = $"Property '{model.Name}' updated successfully.";
            return RedirectToAction(nameof(Properties));
        }

        public async Task<IActionResult> ViewProperty(int id)
        {
            var property = await _db.Properties
                .Include(p => p.Manager)
                .Include(p => p.Units).ThenInclude(u => u.Tenancies).ThenInclude(t => t.Tenant)
                .FirstOrDefaultAsync(p => p.PropertyId == id);

            if (property == null) return NotFound();
            return View(property);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProperty(int id)
        {
            var property = await _db.Properties.Include(p => p.Units).FirstOrDefaultAsync(p => p.PropertyId == id);
            if (property == null) return NotFound();

            var unitIds = property.Units.Select(u => u.UnitId).ToList();
            var hasTenancies = await _db.Tenancies.AnyAsync(t => unitIds.Contains(t.UnitId));
            var hasMaintenanceRequests = await _db.MaintenanceRequests.AnyAsync(m => unitIds.Contains(m.UnitId));

            if (hasTenancies || hasMaintenanceRequests)
            {
                TempData["Error"] = "Cannot delete a property that has tenancies or maintenance requests on its units. Remove those first.";
                return RedirectToAction(nameof(Properties));
            }

            var name = property.Name;

            try
            {
                _db.Properties.Remove(property); // cascades to its (now childless) units
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                TempData["Error"] = "Cannot delete this property because related records still exist.";
                return RedirectToAction(nameof(Properties));
            }

            await LogAsync("Deleted Property", "Property", id.ToString(), name);
            TempData["Success"] = $"Property '{name}' and its units have been deleted.";
            return RedirectToAction(nameof(Properties));
        }

        // ── Activity Log ─────────────────────────────────────────────────────────
        public async Task<IActionResult> ActivityLog(int page = 1, string? search = null, string? entityType = null)
        {
            const int pageSize = 20;

            var query = _db.ActivityLogs.AsQueryable();

            if (!string.IsNullOrWhiteSpace(entityType))
                query = query.Where(l => l.EntityType == entityType);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(l => l.UserName.ToLower().Contains(s)
                                       || l.Action.ToLower().Contains(s)
                                       || (l.Details != null && l.Details.ToLower().Contains(s)));
            }

            var total = await query.CountAsync();
            var logs = await query
                .OrderByDescending(l => l.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);
            ViewBag.TotalCount = total;
            ViewBag.Search = search;
            ViewBag.EntityTypeFilter = entityType;
            ViewBag.EntityTypes = await _db.ActivityLogs
                .Select(l => l.EntityType)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync();
            return View(logs);
        }

        // ── Units ────────────────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddUnit(int propertyId, string unitNumber, string type, decimal rentAmount, int floor, string? description)
        {
            var property = await _db.Properties.FindAsync(propertyId);
            if (property == null) return NotFound();

            _db.Units.Add(new Unit
            {
                PropertyId = propertyId,
                UnitNumber = unitNumber,
                Type = type,
                RentAmount = rentAmount,
                Floor = floor,
                Description = description,
                Status = "Vacant"
            });
            await _db.SaveChangesAsync();
            await LogAsync("Added Unit", "Unit", propertyId.ToString(), $"{unitNumber} — {property.Name}");
            TempData["Success"] = $"Unit {unitNumber} added successfully.";
            return RedirectToAction(nameof(ViewProperty), new { id = propertyId });
        }

        public async Task<IActionResult> EditUnit(int id)
        {
            var unit = await _db.Units.Include(u => u.Property).FirstOrDefaultAsync(u => u.UnitId == id);
            if (unit == null) return NotFound();
            return View(unit);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUnit(int id, string unitNumber, string type, decimal rentAmount, int floor, string? description)
        {
            var unit = await _db.Units.Include(u => u.Property).FirstOrDefaultAsync(u => u.UnitId == id);
            if (unit == null) return NotFound();

            unit.UnitNumber = unitNumber;
            unit.Type = type;
            unit.RentAmount = rentAmount;
            unit.Floor = floor;
            unit.Description = description;
            await _db.SaveChangesAsync();
            await LogAsync("Edited Unit", "Unit", id.ToString(), $"{unitNumber} — {unit.Property.Name}");
            TempData["Success"] = $"Unit {unitNumber} updated successfully.";
            return RedirectToAction(nameof(ViewProperty), new { id = unit.PropertyId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUnit(int id)
        {
            var unit = await _db.Units.Include(u => u.Property).FirstOrDefaultAsync(u => u.UnitId == id);
            if (unit == null) return NotFound();

            var hasActive = await _db.Tenancies.AnyAsync(t => t.UnitId == id);
            if (hasActive)
            {
                TempData["Error"] = "Cannot delete a unit with active tenancies.";
                return RedirectToAction(nameof(ViewProperty), new { id = unit.PropertyId });
            }

            var propertyId = unit.PropertyId;
            var number = unit.UnitNumber;
            _db.Units.Remove(unit);
            await _db.SaveChangesAsync();
            await LogAsync("Deleted Unit", "Unit", id.ToString(), $"{number} — {unit.Property.Name}");
            TempData["Success"] = $"Unit {number} deleted.";
            return RedirectToAction(nameof(ViewProperty), new { id = propertyId });
        }

        // ── Role Requests ────────────────────────────────────────────────────────
        public async Task<IActionResult> RoleRequests(string? status)
        {
            var query = _db.RoleRequests.Include(r => r.User).AsQueryable();
            if (!string.IsNullOrEmpty(status))
                query = query.Where(r => r.Status == status);

            ViewBag.StatusFilter = status;
            ViewBag.PendingCount = await _db.RoleRequests.CountAsync(r => r.Status == "Pending");
            ViewBag.ApprovedCount = await _db.RoleRequests.CountAsync(r => r.Status == "Approved");
            ViewBag.RejectedCount = await _db.RoleRequests.CountAsync(r => r.Status == "Rejected");
            return View(await query.OrderByDescending(r => r.RequestedAt).ToListAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveRoleRequest(int id)
        {
            var request = await _db.RoleRequests.Include(r => r.User).FirstOrDefaultAsync(r => r.Id == id);
            if (request == null) return NotFound();

            var user = request.User;
            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, request.RequestedRole);

            var reviewer = await _userManager.GetUserAsync(User);
            request.Status = "Approved";
            request.ReviewedAt = DateTime.UtcNow;
            request.ReviewedBy = reviewer!.FullName;
            await _db.SaveChangesAsync();

            await LogAsync("Approved Role Request", "RoleRequest", id.ToString(), $"{user.FullName} → {request.RequestedRole}");
            TempData["Success"] = $"{user.FullName} has been approved as {(request.RequestedRole == "PropertyManager" ? "Property Manager" : "Maintenance Staff")}.";
            return RedirectToAction(nameof(RoleRequests));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectRoleRequest(int id, string? adminNotes)
        {
            var request = await _db.RoleRequests.Include(r => r.User).FirstOrDefaultAsync(r => r.Id == id);
            if (request == null) return NotFound();

            var reviewer = await _userManager.GetUserAsync(User);
            request.Status = "Rejected";
            request.ReviewedAt = DateTime.UtcNow;
            request.ReviewedBy = reviewer!.FullName;
            request.AdminNotes = adminNotes;
            await _db.SaveChangesAsync();

            await LogAsync("Rejected Role Request", "RoleRequest", id.ToString(), $"{request.User.FullName} → {request.RequestedRole}");
            TempData["Success"] = $"Role request from {request.User.FullName} has been rejected.";
            return RedirectToAction(nameof(RoleRequests));
        }

        // ── CSV Export ───────────────────────────────────────────────────────────
        public async Task<IActionResult> ExportUsers()
        {
            var allUsers = _userManager.Users.ToList();
            var rows = new List<string[]> { new[] { "Full Name", "Email", "Phone", "Role", "Status", "Joined" } };

            foreach (var u in allUsers)
            {
                var role = (await _userManager.GetRolesAsync(u)).FirstOrDefault() ?? "No Role";
                rows.Add(new[]
                {
                    u.FullName, u.Email ?? "", u.PhoneNumber ?? "", role,
                    u.IsActive ? "Active" : "Inactive", u.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd")
                });
            }

            await LogAsync("Exported Users", "User", null, $"{allUsers.Count} record(s)");
            return CsvFile(rows, "users");
        }

        public async Task<IActionResult> ExportProperties()
        {
            var properties = await _db.Properties.Include(p => p.Manager).Include(p => p.Units).ToListAsync();
            var rows = new List<string[]> { new[] { "Property Name", "Address", "Type", "Manager", "Total Units", "Occupied", "Vacant", "Status" } };

            foreach (var p in properties)
            {
                var occupied = p.Units.Count(u => u.Status == "Occupied");
                rows.Add(new[]
                {
                    p.Name, p.Address, p.Type, p.Manager?.FullName ?? "Unassigned",
                    p.Units.Count.ToString(), occupied.ToString(), (p.Units.Count - occupied).ToString(), p.Status
                });
            }

            await LogAsync("Exported Properties", "Property", null, $"{properties.Count} record(s)");
            return CsvFile(rows, "properties");
        }

        public async Task<IActionResult> ExportPayments()
        {
            var payments = await _db.Payments
                .Include(p => p.Tenancy).ThenInclude(t => t.Tenant)
                .Include(p => p.Tenancy).ThenInclude(t => t.Unit).ThenInclude(u => u.Property)
                .OrderByDescending(p => p.DueDate)
                .ToListAsync();

            var today = DateTime.UtcNow.Date;
            var rows = new List<string[]> { new[] { "Tenant", "Property", "Unit", "Amount", "Due Date", "Payment Date", "Status" } };

            foreach (var p in payments)
            {
                var status = p.Status == "Pending" && p.DueDate.Date < today ? "Overdue" : p.Status;
                rows.Add(new[]
                {
                    p.Tenancy.Tenant.FullName, p.Tenancy.Unit.Property.Name, p.Tenancy.Unit.UnitNumber,
                    p.Amount.ToString("F2"), p.DueDate.ToString("yyyy-MM-dd"),
                    p.PaymentDate?.ToString("yyyy-MM-dd") ?? "", status
                });
            }

            await LogAsync("Exported Payments", "Payment", null, $"{payments.Count} record(s)");
            return CsvFile(rows, "payments");
        }

        public async Task<IActionResult> ExportActivityLog()
        {
            var logs = await _db.ActivityLogs.OrderByDescending(l => l.Timestamp).ToListAsync();
            var rows = new List<string[]> { new[] { "Timestamp", "Admin", "Action", "Entity", "Details" } };

            foreach (var l in logs)
            {
                rows.Add(new[]
                {
                    l.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm"), l.UserName, l.Action, l.EntityType, l.Details ?? ""
                });
            }

            await LogAsync("Exported Activity Log", "ActivityLog", null, $"{logs.Count} record(s)");
            return CsvFile(rows, "activity-log");
        }

        // Builds a CSV file download from rows (first row is the header).
        // Values are comma/quote/newline-escaped per RFC 4180.
        private FileContentResult CsvFile(List<string[]> rows, string fileNamePrefix)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var row in rows)
                sb.AppendLine(string.Join(",", row.Select(CsvEscape)));

            var fileName = $"{fileNamePrefix}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
            return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", fileName);
        }

        private static string CsvEscape(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }

        // ── Helpers ──────────────────────────────────────────────────────────────
        private async Task<List<SelectListItem>> GetManagerOptionsAsync()
        {
            var managers = await _userManager.GetUsersInRoleAsync("PropertyManager");
            var options = managers.Select(m => new SelectListItem
            {
                Value = m.Id,
                Text = $"{m.FullName} ({m.Email})"
            }).ToList();
            options.Insert(0, new SelectListItem { Value = "", Text = "— Unassigned —" });
            return options;
        }
    }
}
