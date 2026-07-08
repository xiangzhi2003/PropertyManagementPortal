# CLAUDE.md — PropertyManagementPortal

Project context for Claude Code sessions.

## Project

ASP.NET Core MVC 8 web application for property management.
University assignment: CT071-3-3-DDAC (Designing and Developing ASP.NET Core Applications).

All four role panels (Admin, Property Manager, Tenant, Maintenance Staff) are now fully built.

## How to Run

```bash
dotnet run
```

Must be run from the `PropertyManagementPortal/` project folder (where the `.csproj` is).
Requires `appsettings.json` — see below. Default dev URL: `http://localhost:5206`.

## appsettings.json — NEVER COMMIT

This file is gitignored. It holds real secrets and must never be committed. Copy `appsettings.example.json` and fill in real values.

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "<NeonDB postgres connection string>"
  },
  "Authentication": {
    "Google": {
      "ClientId": "<Google OAuth client ID>",
      "ClientSecret": "<Google OAuth client secret>"
    }
  },
  "Logging": {
    "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" }
  },
  "AllowedHosts": "*"
}
```

Stripe is referenced in the csproj (`Stripe.net`) for the Tenant payment gateway (`TenantController.Checkout`) — a Stripe secret key would need to be added under configuration if that flow is exercised for real payments.

## Database

- Provider: Npgsql (PostgreSQL) via NeonDB (serverless Postgres, hosted on AWS ap-southeast-1)
- EF Core migrations in `Database/Migrations/` — never edit these files manually
- Run `dotnet ef database update` to apply pending migrations
- `Database/Data/SeedData.cs` seeds all four roles on startup only — the Admin **user** itself is not seeded, it's created manually (register normally, then promote the account's role directly in the database — see "Promoting a user to Admin" below)

### Promoting a user to Admin

There is no UI path to grant the Admin role (by design — Admin accounts are managed at the DB level). To promote an existing registered user, run in the Postgres console:

```sql
DELETE FROM "AspNetUserRoles"
WHERE "UserId" = (SELECT "Id" FROM "AspNetUsers" WHERE "NormalizedEmail" = UPPER('their@email.com'));

INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
SELECT u."Id", r."Id"
FROM "AspNetUsers" u, "AspNetRoles" r
WHERE u."NormalizedEmail" = UPPER('their@email.com') AND r."Name" = 'Admin';
```

The user must fully log out and back in afterward — the role is baked into the auth cookie at login time, so an active session won't pick up the change.

## Roles

| Role | Notes |
|---|---|
| Admin | Full access — created manually, never via seed or self-registration |
| PropertyManager | Manages assigned properties, units, applications, payments, maintenance |
| Tenant | Default role assigned on registration; can request a role upgrade |
| MaintenanceStaff | Handles assigned maintenance requests |

## Key Conventions

## Folder Structure

```
PropertyManagementPortal/   ← project root
├── Backend/
│   ├── Controllers/        ← all role controllers + shared base
│   ├── ViewComponents/     ← NotificationBellViewComponent (shared across all layouts)
│   └── ViewModels/         ← Admin/, Manager/, Tenant/, Maintenance/, Shared/
├── Database/
│   ├── Models/             ← EF Core entity models (DB tables)
│   ├── Data/               ← DbContext + SeedData
│   └── Migrations/         ← auto-generated migration history
├── Views/                  ← Razor HTML templates (cannot move — ASP.NET convention)
├── wwwroot/                ← static files (cannot move — ASP.NET convention)
├── Areas/Identity/         ← login/register/profile (cannot move — ASP.NET convention)
├── Program.cs
└── appsettings.json
```

### AppControllerBase — shared role-controller base
`Backend/Controllers/AppControllerBase.cs` is an abstract base every role controller (`AdminController`, `TenantController`, `ManagerController`, `MaintenanceController`) inherits from. It holds:
- `protected readonly ApplicationDbContext _db` and `protected readonly UserManager<ApplicationUser> _userManager`
- `protected void AddNotification(string userId, string message)` — queues a notification; caller must still call `SaveChangesAsync()`
- `Notifications(string? readFilter)` and `MarkRead(int id)` actions — identical for every role, so each derived controller gets `/{Role}/Notifications` for free. To add notifications support to a new role controller: inherit `AppControllerBase`, chain `base(db, userManager)`, add a `Views/{Role}/Notifications.cshtml` that renders `<partial name="_NotificationList" model="Model" />`, and drop `@await Component.InvokeAsync("NotificationBell")` into that role's layout header.

Currently wired to actually send notifications:
- `TenantController.RequestRole` → notifies all Admins when a tenant requests a role upgrade
- `TenantController.Maintenance` (POST) → notifies all Admins when a new maintenance request is submitted
- `MaintenanceController` → notifies the tenant and property manager on job assignment/status updates

### ApplicationUser
`Database/Models/ApplicationUser.cs` extends `IdentityUser`. **Never add a property with the `new` keyword that shadows a base-class property.** This caused a phone-number bug where EF Core saved to the derived property but `UserManager.GetPhoneNumberAsync` always returned null (reads base class via generic constraint). Fix: removed the hiding property; preserved `MaxLength(20)` via fluent API in `OnModelCreating`.

### Payment status is derived, never stored as "Overdue"
`Payment.Status` in the database is only ever `"Pending"` or `"Paid"`. **Nothing ever writes `"Overdue"` to the database.** Every place that shows payment status (Admin Dashboard, Admin Global Search, Admin CSV export, Manager Dashboard, Manager Payments page, Tenant My Payments) independently derives it at read time: `if (Status == "Pending" && DueDate.Date < today) Status = "Overdue"`. This is intentional (no background job persists the transition), but it means **any new place that displays a payment must repeat this derivation** — do not trust `Payment.Status == "Overdue"` in a raw DB query, it will always be false.

### No real email sender configured
There is no `IEmailSender` implementation in this project — Identity's default no-op sender is used. Since no email is ever actually delivered, both the Register and Forgot Password flows show the confirmation/reset link **directly on the confirmation page** instead of relying on email delivery (`RegisterConfirmation.cshtml.cs` → `DisplayConfirmAccountLink`; `ForgotPasswordConfirmation.cshtml.cs` → `ResetUrl`). This is an accepted trade-off for this assignment — if real email delivery is ever needed, a real `IEmailSender` (SMTP or a provider like SendGrid) must be registered in `Program.cs`, and these fallbacks should be removed.

### Registration — Auto Tenant Role
Both `Areas/Identity/Pages/Account/Register.cshtml.cs` and `ExternalLogin.cshtml.cs` call `AddToRoleAsync(user, "Tenant")` immediately after user creation. Every self-registered user (email or Google OAuth) gets the Tenant role automatically. There is no UI path to become Admin — see "Promoting a user to Admin" above.

### Login redirect
After a successful login (password or Google), every role lands on the **Home page** (`Views/Home/Index.cshtml`), which shows a "Welcome back, {name} [{role}]" message and a role-appropriate "Go to Dashboard" button — there is no automatic redirect straight to `/Admin/Dashboard` (this was deliberately removed; Admin behaves the same as every other role here).

### Admin Panel
- Controller: `Backend/Controllers/AdminController.cs` — protected with `[Authorize(Roles = "Admin")]`, inherits `AppControllerBase`
- Views: `Views/Admin/` using `Views/Admin/_AdminLayout.cshtml`
- Every mutating action calls `LogAsync(action, entityType, entityId, details)` to write to `ActivityLogs` — this includes the CSV export actions (`ExportUsers`, `ExportProperties`, `ExportPayments`, `ExportActivityLog`), which are treated as loggable even though they're read-only, for audit-trail consistency
- Dashboard, Properties, Users, and Activity Log pages use Chart.js (loaded from CDN) for interactive tooltips — see `Views/Admin/Dashboard.cshtml`'s `@section Scripts` block
- There is no "Deactivate" toggle for Users or Properties — it was removed because `IsActive`/`Property.Status` were never enforced anywhere (not checked at login, not checked anywhere that would actually restrict access), so it was purely cosmetic. Deleting a user/property is the only account-removal path now, and it's blocked when the user/property has dependent records (tenancies, maintenance history, etc.)

### CSS
- Custom properties (`--primary`, `--accent`, `--text`, etc.) defined at top of `wwwroot/css/site.css`
- Admin-specific classes prefixed with `.admin-` (`.admin-sidebar`, `.admin-nav-link`, `.admin-main`, etc.) — also reused by Manager/Tenant/Maintenance layouts, not Admin-exclusive despite the prefix
- `.admin-shell` (opt-in class on the sidebar+content flex wrapper) makes the sidebar collapse to a full-width strip above the content at ≤767px — used in every role layout
- `.admin-main { min-width: 0; }` is required so wide tables inside `.table-responsive` scroll within their own box instead of forcing the whole page to overflow horizontally (a flexbox default-min-width gotcha)
- Sticky footer pattern: `<body class="d-flex flex-column min-vh-100">` + `<main class="flex-grow-1">`

## Feature Specifications

### Admin (BUILT — `Backend/Controllers/AdminController.cs`)

**Dashboard** (Reports page was merged into Dashboard — `/Admin/Reports` redirects here)
- Stat cards: Total Users, Total Properties, Pending Maintenance, Overdue Payments
- Occupancy Overview: overall stats + a per-property occupancy breakdown table with progress bars
- Payment Summary: Chart.js doughnut (Paid/Pending/Overdue) + a drill-down table of every unpaid payment (tenant, property/unit, manager, amount, due date, days overdue) with clickable tenant/manager links to their profile — plus a CSV export button
- Maintenance Summary: Chart.js bar chart by status + a drill-down table of every open request (tenant, manager, assigned staff, category, status) with clickable links
- Revenue Trend & Occupancy Trend: two Chart.js line charts, last 6 months. Revenue is exact (sums `Paid` payments by month). Occupancy is an **approximation** — it divides historical active-tenancy counts by the *current* unit count, since there's no historical snapshot of unit counts (flagged with an inline code comment)
- Recent Activity feed with color-coded badges by action type

**Manage Users**
- Table: name (with initials avatar), email, phone, role badge, status badge, joined date
- Search/filter by name, email, role, status; result count shown
- Create user (any role), Edit user, View full user detail page (with outstanding balance + account activity history), Delete user (blocked if Admin, or has active tenancies/maintenance requests)
- CSV export button

**Manage Properties**
- Table: name, address, type, assigned manager, inline occupancy bar, status
- Add/Edit property, View property detail (units list with current tenant name + occupancy summary)
- CSV export button

**Activity Log**
- Paginated, filterable (search + entity-type) audit trail with color-coded action badges
- CSV export button

**Role Requests**
- Pending/Approved/Rejected stat cards, applicant "member since" context
- Approve/Reject with optional reason note

**Global Search**
- Header search box (`_AdminLayout.cshtml`) → `AdminController.GlobalSearch(string? q)` searches Users (name/email), Properties (name/address), and unpaid Payments (tenant name) simultaneously

---

### Property Manager (BUILT — `Backend/Controllers/ManagerController.cs`)
- Dashboard: property/unit counts, pending applications, pending/overdue payments (derived, see note above), unassigned maintenance, a "what needs attention" chart
- Manage Units: list/filter units across their managed properties, add/edit units
- Applications: approve/reject tenant applications (approval auto-generates the monthly rent/payment schedule and notifies the tenant)
- Rent Payments: list with status filter, mark-as-paid with a date picker (rejects future dates), Chart.js doughnut (status) + bar (collected vs outstanding)
- Maintenance: assign requests to staff with priority/notes, view history

### Tenant (BUILT — `Backend/Controllers/TenantController.cs`)
- Dashboard: role upgrade request cards (Property Manager / Maintenance Staff), current tenancies, outstanding payments summary
- Browse Units / Apply / Confirm Application — submit a rental application
- My Applications — track application status
- My Payments — view payment history, pay via Stripe checkout (`Checkout` action)
- My Maintenance — submit and track maintenance requests (submitting one notifies all Admins)
- Role upgrade request → notifies all Admins

### Maintenance Staff (BUILT — `Backend/Controllers/MaintenanceController.cs`)
- Dashboard: assigned job counts by status, latest job, completed-this-month, 6-month completed-jobs trend chart
- Assigned Jobs: filterable list
- Job Details: update status (Assigned → InProgress → Completed, forward-only), upload repair evidence photo (required to mark Completed), each update notifies the tenant/manager

---

## What's Not Built

Nothing at the feature level — all four role panels are complete. Possible future additions (not started): real email delivery (`IEmailSender`), a shared partial for the auth pages' duplicated card markup, a shared helper for the payment-overdue derivation logic repeated across controllers, background job to actually persist overdue status.
