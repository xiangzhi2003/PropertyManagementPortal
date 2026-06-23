# CLAUDE.md — PropertyManagementPortal

Project context for Claude Code sessions.

## Project

ASP.NET Core MVC 8 web application for property management.
University assignment: CT071-3-3-DDAC (Designing and Developing ASP.NET Core Applications).

## How to Run

```bash
dotnet run
```

Must be run from the `PropertyManagementPortal/` project folder (where the `.csproj` is).
Requires `appsettings.json` — see below.

## appsettings.json — NEVER COMMIT

This file is gitignored. It holds real secrets and must never be committed.

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

## Database

- Provider: Npgsql (PostgreSQL) via NeonDB (serverless Postgres, hosted on AWS ap-southeast-1)
- EF Core migrations in `Migrations/`
- Run `dotnet ef database update` to apply pending migrations
- `Data/SeedData.cs` seeds the Admin user and all four roles on startup

## Roles

| Role | Notes |
|---|---|
| Admin | Full access — seeded on first run |
| PropertyManager | Manages assigned properties and tenants |
| Tenant | Views tenancy, submits maintenance requests |
| MaintenanceStaff | Handles assigned maintenance requests |

## Key Conventions

### ApplicationUser
`Models/ApplicationUser.cs` extends `IdentityUser`. **Never add a property with the `new` keyword that shadows a base-class property.** This caused a phone-number bug where EF Core saved to the derived property but `UserManager.GetPhoneNumberAsync` always returned null (reads base class via generic constraint). Fix: removed the hiding property; preserved `MaxLength(20)` via fluent API in `OnModelCreating`.

### Admin Panel
- Controller: `Controllers/AdminController.cs` — protected with `[Authorize(Roles = "Admin")]`
- Views: `Views/Admin/` using `Views/Admin/_AdminLayout.cshtml`
- Every mutating action calls `LogAsync(action, entityType, entityId, details)` to write to `ActivityLogs`

### Identity Manage Pages
- `Areas/Identity/Pages/Account/Manage/`
- Nav is trimmed to **My Profile** + **Change Password** only (`_ManageNav.cshtml`)
- Other scaffolded pages (Email, TwoFactor, PersonalData, ExternalLogins) remain on disk but are unreachable from the UI

### CSS
- Custom properties (`--primary`, `--accent`, `--text`, etc.) defined at top of `wwwroot/css/site.css`
- Admin-specific classes prefixed with `.admin-` (`.admin-sidebar`, `.admin-nav-link`, `.admin-main`, etc.)
- Sticky footer pattern: `<body class="d-flex flex-column min-vh-100">` + `<main class="flex-grow-1">`

### Activity Logging
Call `await LogAsync(action, entityType, entityId, details)` in `AdminController` after every create/edit/delete/toggle operation. This writes to the `ActivityLogs` table and shows up on the Activity Log page.

## Branch Strategy

| Branch | Purpose |
|---|---|
| `main` | Stable, deployable |
| `dev` | Integration branch |
| `feature/admin` | Admin panel (merged) |
| `feature/manager` | Property Manager panel (not started) |
| `feature/tenant` | Tenant panel (not started) |
| `feature/maintenance` | Maintenance Staff panel (not started) |

## Feature Specifications

### Admin (BUILT — `Controllers/AdminController.cs`)

**Dashboard**
- Stat cards: Total Users, Total Properties, Pending Maintenance, Overdue Payments
- Each card links to the relevant page
- Welcome message with admin's full name
- Recent activity feed

**Manage Users**
- Table: name, email, phone, role badge, status badge
- Search/filter by name, email, role, status
- Create user (PropertyManager, Tenant, MaintenanceStaff only — not Admin)
- Edit user details and role (email is read-only)
- View user detail page
- Toggle activate/deactivate (cannot deactivate Admin or self)
- Delete user (blocked if Admin, or has active tenancies/maintenance requests)

**Manage Properties**
- Table: name, address, type, assigned manager, unit count, status
- Search/filter by name, type, status
- Add property (assign to a Property Manager)
- Edit property details
- View property detail page (includes units list)
- Toggle Active/Inactive status

**System Reports**
- Occupancy Report: total units, occupied vs vacant, occupancy rate %
- Payment Summary: total, paid, pending, overdue counts
- Maintenance Summary: submitted, assigned, in-progress, completed counts

**Activity Log**
- Paginated list: who did what, when
- All admin mutations logged via `LogAsync()`

---

### Maintenance Staff (PLANNED — `feature/maintenance` branch)

**Dashboard**
- Assigned jobs count by status
- Latest assigned job highlight
- Completed jobs this month count

**Assigned Jobs**
- View all jobs assigned to them
- Each job shows: unit, property, issue description, tenant name, date, status
- Filter by status (Assigned / InProgress / Completed)
- Click to view full job details

**Update Job Status**
- Assigned → InProgress (when starting work)
- InProgress → Completed (when done)
- Each status change requires a note/comment
- Tenant and Property Manager notified on each change

**Upload Repair Evidence**
- Upload photo of completed repair
- Add completion notes (work done, parts used)
- Required when marking job as Completed

**Notifications**
- New job assignment alerts
- Follow-up comments from Property Manager

---

### Property Manager (PLANNED — `feature/manager` branch)
*(spec not yet defined)*

### Tenant (PLANNED — `feature/tenant` branch)
*(spec not yet defined)*

---

## What Is Not Built Yet

Models and DB tables exist for all entities, but these role panels have no controllers or views:
- **MaintenanceStaff** — see spec above
- **PropertyManager** — spec pending
- **Tenant** — spec pending
