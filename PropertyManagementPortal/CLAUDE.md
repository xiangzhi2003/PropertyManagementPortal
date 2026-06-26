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
- EF Core migrations in `Database/Migrations/`
- Run `dotnet ef database update` to apply pending migrations
- `Database/Data/SeedData.cs` seeds all four roles on startup (Admin user created manually)

## Roles

| Role | Notes |
|---|---|
| Admin | Full access — created manually or via seed |
| PropertyManager | Manages assigned properties and tenants |
| Tenant | Default role assigned on registration |
| MaintenanceStaff | Handles assigned maintenance requests |

## Key Conventions

## Folder Structure (After Reorganization)

```
PropertyManagementPortal/   ← project root
├── Backend/
│   ├── Controllers/        ← all role controllers
│   └── ViewModels/Admin/   ← data shapes for admin views
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

## Key Conventions

### ApplicationUser
`Database/Models/ApplicationUser.cs` extends `IdentityUser`. **Never add a property with the `new` keyword that shadows a base-class property.** This caused a phone-number bug where EF Core saved to the derived property but `UserManager.GetPhoneNumberAsync` always returned null (reads base class via generic constraint). Fix: removed the hiding property; preserved `MaxLength(20)` via fluent API in `OnModelCreating`.

### Registration — Auto Tenant Role
Both `Areas/Identity/Pages/Account/Register.cshtml.cs` and `ExternalLogin.cshtml.cs` call `AddToRoleAsync(user, "Tenant")` immediately after user creation. Every self-registered user (email or Google OAuth) gets the Tenant role automatically.

### Home Page Routing
`Views/Home/Index.cshtml` uses `User.IsInRole()` to route each authenticated user to their role's dashboard:
- Admin → `/Admin/Dashboard`
- PropertyManager → `/Manager/Dashboard`
- MaintenanceStaff → `/Maintenance/Dashboard`
- Tenant (or no role) → `/Tenant/Dashboard`

### Admin Panel
- Controller: `Backend/Controllers/AdminController.cs` — protected with `[Authorize(Roles = "Admin")]`
- Views: `Views/Admin/` using `Views/Admin/_AdminLayout.cshtml`
- Every mutating action calls `LogAsync(action, entityType, entityId, details)` to write to `ActivityLogs`

### Role Panel Controllers
| Controller | Role | Status |
|---|---|---|
| `Backend/Controllers/AdminController.cs` | Admin | Full |
| `Backend/Controllers/TenantController.cs` | Tenant | Partial (role request only) |
| `Backend/Controllers/ManagerController.cs` | PropertyManager | Stub |
| `Backend/Controllers/MaintenanceController.cs` | MaintenanceStaff | Stub |

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

### Admin (BUILT — `Backend/Controllers/AdminController.cs`)

**Dashboard**
- Stat cards: Total Users, Total Properties, Pending Maintenance, Overdue Payments
- Each card links to the relevant page
- Welcome message with admin's full name
- Recent activity feed

**Manage Users**
- Table: name, email, phone, role badge, status badge
- Search/filter by name, email, role, status
- Create user (any role)
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

**Role Requests**
- Tenants can request to become PropertyManager or MaintenanceStaff
- Admin sees pending requests with a badge count in the sidebar
- Approve: changes user's role immediately
- Reject: marks request rejected, optionally adds a reason note
- Model: `Database/Models/RoleRequest.cs` — fields: UserId, RequestedRole, Status, RequestedAt, ReviewedAt, ReviewedBy, AdminNotes

---

### Tenant (PARTIAL — `Backend/Controllers/TenantController.cs`)

**Dashboard**
- Welcome card with name and Tenant role badge
- Role Upgrade section: two cards to request PropertyManager or MaintenanceStaff role
- If request pending: shows pending banner, hides upgrade cards
- If request rejected: shows rejection reason, upgrade cards reappear

**Not yet built**
- Tenancy view (lease details, unit info)
- Maintenance request submission and tracking
- Payment history

---

### Property Manager (STUB — `Backend/Controllers/ManagerController.cs`)
- Stub dashboard: "coming soon" page
- Full panel: NOT YET BUILT

### Maintenance Staff (STUB — `Backend/Controllers/MaintenanceController.cs`)

**Planned features**
- Dashboard: assigned jobs count by status, latest job, completed this month
- Assigned Jobs: list with unit, property, issue, tenant, date, status; filter by status
- Update Job Status: Assigned → InProgress → Completed, each requires a note
- Upload Repair Evidence: photo + completion notes, required when marking Completed
- Notifications: new assignment alerts, PM follow-ups

**Status: NOT YET BUILT** (stub dashboard only)

---

## What Is Not Built Yet

Models and DB tables exist for all entities. Missing controllers/views:
- **PropertyManager** — full panel (spec not yet defined)
- **MaintenanceStaff** — full panel (spec defined above)
- **Tenant** — tenancy view, maintenance requests, payments
