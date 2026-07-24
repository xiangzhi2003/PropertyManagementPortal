# PropertyManagementPortal

![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-8.0-512BD4?logo=dotnet)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-NeonDB-336791?logo=postgresql)
![Bootstrap](https://img.shields.io/badge/Bootstrap-5-7952B3?logo=bootstrap)
![Chart.js](https://img.shields.io/badge/Chart.js-4-FF6384?logo=chartdotjs)
![Gemini](https://img.shields.io/badge/Gemini-3.1%20Flash--Lite-8E75B2?logo=googlegemini)
![AWS Lambda](https://img.shields.io/badge/AWS-Lambda%20%2B%20API%20Gateway%20%2B%20S3-FF9900?logo=amazonaws)

A centralized web platform for managing properties, tenants, maintenance requests, and payments. Built as a university assignment for **CT071-3-3-DDAC** (Designing and Developing ASP.NET Core Applications).

All four role panels — Admin, Property Manager, Tenant, and Maintenance Staff — are fully built.

---

## Features

### All Users
- Register with email/password or Google OAuth — automatically assigned **Tenant** role
- Login with email/password or Google OAuth — lands on the Home page with a "Welcome back, {name} [{role}]" message and a role-appropriate "Go to Dashboard" button
- My Profile — edit full name and phone number; view role, account status, member since
- Change Password / Forgot Password (reset link is shown directly on-screen, since this project has no real email sender configured)
- In-app notifications (bell icon in the header) — role-relevant events push a notification with an unread badge

---

### Admin ✅

**Dashboard**
- Stat cards: Total Users, Total Properties, Pending Maintenance, Overdue Payments — each links to the relevant page
- Occupancy Overview — overall occupancy stats plus a per-property breakdown table with progress bars
- Payment Summary — Chart.js doughnut chart (Paid/Pending/Overdue) with interactive hover tooltips, a drill-down table of every unpaid payment (tenant, property/unit, manager in charge, amount, due date, days overdue) with clickable links to the tenant's/manager's profile, and a CSV export
- Maintenance Summary — Chart.js bar chart by status, a drill-down table of every open request (tenant, manager, assigned staff, category, status) with clickable profile links
- Revenue Trend & Occupancy Trend — two Chart.js line charts covering the last 6 months
- Recent Activity feed with color-coded badges by action type
- **AI Property Report** — "Generate AI Report" button builds a full portfolio briefing (no page reload). The factual half is rendered from live data: a "portfolio at a glance" stat row, a per-property table (manager, units, occupied, tenants, overdue, open jobs), and team tables (managers and staff with their workloads). Google's Gemini API (`gemini-3.1-flash-lite`, structured JSON output) layers the analytical half on top: an overall-health verdict, an executive summary, a status assessment (Good/Warning/Critical) per operational area, and a numbered list of priority actions — all referencing specific properties, managers, and staff by name. Hard numbers come from the app, never the AI, so nothing is fabricated.

**Manage Users**
- Table view: avatar initials, name, email, phone, role badge, status badge, joined date
- Search and filter by name, email, role, or status
- Create new user (any role), edit user details/role, view full user detail page (outstanding balance + account activity history)
- Delete user (blocked if Admin, or has active tenancies/maintenance requests)
- CSV export

**Manage Properties**
- Table view: name, address, type, assigned manager, inline occupancy bar, status
- Search and filter by name, type, or status
- Add/edit property, view property detail page (units list with current tenant name, occupancy summary)
- CSV export

**Activity Log**
- Paginated, searchable, filterable audit trail with color-coded action badges
- CSV export

**Role Requests**
- Pending/Approved/Rejected stat cards
- Approve — instantly changes the user's role; Reject — with an optional reason note

**Global Search**
- Header search box — searches Users, Properties, and unpaid Payments at once, with click-through to each result's detail page

---

### Property Manager ✅

- **Dashboard** — managed property/unit counts, pending applications, pending/overdue payments, unassigned maintenance, an "attention needed" chart
- **Manage Units** — list/filter units across managed properties, add/edit units
- **Applications** — approve/reject tenant applications (approval auto-generates the monthly rent schedule and notifies the tenant)
- **Rent Payments** — filterable list, mark-as-paid with a date picker (rejects future dates), Chart.js status/amount charts
- **Maintenance** — assign requests to staff with priority and notes, view job history

---

### Tenant ✅

- **Dashboard** — role upgrade request cards, current tenancies, outstanding payments summary
- **Browse Units / Apply** — submit a rental application, track its status
- **My Payments** — payment history, pay via Stripe checkout
- **My Maintenance** — submit and track maintenance requests

---

### Maintenance Staff ✅

- **Dashboard** — assigned job counts by status, latest job, completed-this-month, 6-month completed-jobs trend chart
- **Assigned Jobs** — filterable list
- **Job Details** — update status (Assigned → In Progress → Completed, forward-only), upload repair evidence photo (required to mark Completed)

---

## Serverless Photo Upload (Task 2)

Every photo in the app — the tenant's problem photo, the staff's repair-evidence photo, and the manager's read-only view of both — uploads through a serverless chain instead of through the web server:

```
Browser → API Gateway → Lambda (mints a presigned S3 URL)
Browser → S3 directly (uploads the file — the web server never sees the bytes)
Browser → ASP.NET MVC (posts back only the resulting object key, saved to Postgres)
```

The Lambda function's source lives in [`aws/lambda/index.mjs`](aws/lambda/index.mjs); the console setup steps (bucket, CORS, Lambda, API Gateway) are documented in [`aws/README.md`](aws/README.md). The app side needs no AWS SDK and no AWS credentials — only one config value:

```json
"ApiGateway": { "S3Endpoint": "https://<your-api-id>.execute-api.ap-southeast-1.amazonaws.com/prod/photos" }
```

`Backend/Services/PhotoKeyValidator.cs` checks the shape of every object key before it's trusted into the database, since it arrives from the client after its own direct upload to S3.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core MVC 8 |
| ORM | Entity Framework Core 8 |
| Database | PostgreSQL (NeonDB serverless) via Npgsql |
| Authentication | ASP.NET Core Identity + Google OAuth 2.0 |
| Payments | Stripe.net |
| AI | Google Gemini API (`gemini-3.1-flash-lite`) — Admin Dashboard AI summary |
| Serverless | AWS Lambda + API Gateway + S3 — presigned-URL photo upload (Task 2) |
| UI | Bootstrap 5, Chart.js 4, Inter font, custom CSS |

---

## Getting Started

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- [EF Core CLI](https://learn.microsoft.com/en-us/ef/core/cli/dotnet): `dotnet tool install --global dotnet-ef`
- A PostgreSQL database (NeonDB free tier works)

### Setup

1. **Clone the repo**
   ```bash
   git clone https://github.com/xiangzhi2003/PropertyManagementPortal.git
   cd PropertyManagementPortal/PropertyManagementPortal
   ```

2. **Create `appsettings.json`** in the `PropertyManagementPortal/` project folder (gitignored — never committed). Copy `appsettings.example.json` as a starting template:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=<host>;Database=<db>;Username=<user>;Password=<password>;SSL Mode=Require;"
     },
     "Authentication": {
       "Google": {
         "ClientId": "<your-google-client-id>",
         "ClientSecret": "<your-google-client-secret>"
       }
     },
     "Gemini": {
       "ApiKey": "<your-gemini-api-key>"
     },
     "ApiGateway": {
       "S3Endpoint": "<your-api-gateway-invoke-url>/photos"
     },
     "Logging": {
       "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" }
     },
     "AllowedHosts": "*"
   }
   ```
   Get a free Gemini API key at [aistudio.google.com/apikey](https://aistudio.google.com/apikey) — only needed for the Admin Dashboard's "Generate AI Summary" button; the rest of the app works without it.

   `ApiGateway:S3Endpoint` powers photo uploads (see [Serverless Photo Upload](#serverless-photo-upload-task-2) above) — set up your own bucket/Lambda/API Gateway following [`aws/README.md`](aws/README.md), or leave it blank; every upload button then shows "not configured yet" instead of erroring, and the rest of the app works normally.

3. **Apply database migrations**
   ```bash
   dotnet ef database update
   ```

4. **Run**
   ```bash
   dotnet run
   ```
   The app seeds the four roles on first startup, but not an Admin user. Register a normal account first, then promote it to Admin directly in the database:
   ```sql
   INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
   SELECT u."Id", r."Id"
   FROM "AspNetUsers" u, "AspNetRoles" r
   WHERE u."NormalizedEmail" = UPPER('your@email.com') AND r."Name" = 'Admin';
   ```
   Log out and back in afterward — the role is read into your session at login time.

---

## How the Code Is Organized (MVC Pattern)

This project follows the **MVC (Model-View-Controller)** pattern. Every feature is split into three parts:

### Areas (Login & Registration)
`Areas/Identity/` contains all the login, register, and account management pages, provided by ASP.NET Core Identity and customized to match the site's design. Think of Areas as a sub-application — its own pages, layouts, and logic, separate from the main MVC structure. Note: this project has no real email sender configured, so the Register and Forgot Password confirmation pages show the confirmation/reset link directly on-screen instead of emailing it.

### Models (`Database/Models/`)
Models represent the **database tables**. Each `.cs` file maps directly to a table in PostgreSQL via Entity Framework Core.

Example: `Unit.cs` has fields `UnitNumber`, `Type`, `RentAmount`, `Floor`, `Status` → these become columns in the `Units` table.

### Controllers (`Backend/Controllers/`)
Controllers are the **brain**. When a user clicks a link or submits a form, a controller action runs, queries the DB, packages the data into a ViewModel, and returns a View.

Each role has its own controller, and all four inherit a shared `AppControllerBase` (holds the DB context, user manager, and the shared `Notifications`/`MarkRead` actions + `AddNotification` helper):
- `AdminController.cs` — all admin pages
- `TenantController.cs` — tenant pages
- `ManagerController.cs` — property manager pages
- `MaintenanceController.cs` — maintenance staff pages

Controllers are protected with `[Authorize(Roles = "RoleName")]` so only the right role can access them.

### Views (`Views/`)
The HTML pages the user sees, written in Razor syntax (`.cshtml`). Each role has its own subfolder (`Views/Admin/`, `Views/Tenant/`, `Views/Manager/`, `Views/Maintenance/`), plus `Views/Shared/` for layouts/partials used by all roles.

### ViewModels (`Backend/ViewModels/`)
Data containers made for a specific view, so the controller only passes the fields that page needs instead of a raw 20+ field database model. Organized per role: `ViewModels/Admin/`, `ViewModels/Manager/`, `ViewModels/Tenant/`, `ViewModels/Maintenance/`, plus `ViewModels/Shared/` for the notifications view model used by every role.

### ViewComponents (`Backend/ViewComponents/`)
`NotificationBellViewComponent` — a self-contained component dropped into every role layout's header that renders the unread-notification bell icon.

### The Full Flow

```
User clicks a link or submits a form
        ↓
Controller action runs (e.g. AdminController.EditUser)
        ↓
Controller queries the database via ApplicationDbContext
        ↓
Controller creates a ViewModel and fills it with data
        ↓
Controller returns View(viewModel)
        ↓
View (.cshtml) renders the HTML using the ViewModel data
        ↓
HTML page sent back to the user's browser
```

### Data Layer (`Database/Data/`)
- `ApplicationDbContext.cs` — the EF Core database context, holds a `DbSet<>` for every model
- `SeedData.cs` — runs on startup to create the four roles if they don't exist yet (does **not** create an Admin user — see Setup above)

### Migrations (`Database/Migrations/`)
Every time a Model is added or changed, a migration is created with `dotnet ef migrations add <Name>`. Run `dotnet ef database update` to apply. **Never edit migration files manually.**

---

## Project Structure

```
PropertyManagementPortal/                        ← git repo / solution root
├── PropertyManagementPortal.sln
├── README.md
│
└── PropertyManagementPortal/                    ← .NET web project
    │
    │  # ── BACKEND ────────────────────────────────────────────────────────
    ├── Backend/
    │   ├── Controllers/
    │   │   ├── HomeController.cs                landing page (role-aware welcome message)
    │   │   ├── AppControllerBase.cs              shared base: notifications, DB/UserManager access
    │   │   ├── AdminController.cs                all admin actions
    │   │   ├── TenantController.cs                tenant dashboard, applications, payments, maintenance
    │   │   ├── ManagerController.cs               property manager panel
    │   │   └── MaintenanceController.cs          maintenance staff panel
    │   │
    │   ├── ViewComponents/
    │   │   └── NotificationBellViewComponent.cs  header bell icon, used by every role layout
    │   │
    │   └── ViewModels/
    │       ├── Admin/                            DashboardViewModel, UserListViewModel, GlobalSearchViewModel, ...
    │       ├── Manager/
    │       ├── Tenant/
    │       ├── Maintenance/
    │       └── Shared/                           NotificationsViewModel
    │
    ├── Backend/Services/
    │   └── PhotoKeyValidator.cs                  validates S3 object keys posted back by the browser
    │
    ├── Program.cs                                app startup, middleware, DI, auth config
    │
    │  # ── DATABASE ───────────────────────────────────────────────────────
    ├── Database/
    │   ├── Models/                              database table definitions
    │   │   ├── ApplicationUser.cs               user account (extends IdentityUser)
    │   │   ├── Property.cs / Unit.cs / Tenancy.cs / Payment.cs
    │   │   ├── MaintenanceRequest.cs / MaintenanceUpdate.cs
    │   │   ├── Notification.cs
    │   │   ├── ActivityLog.cs
    │   │   └── RoleRequest.cs
    │   │
    │   ├── Data/
    │   │   ├── ApplicationDbContext.cs
    │   │   └── SeedData.cs
    │   │
    │   └── Migrations/                          ⚠ never edit these files manually
    │
    │  # ── FRONTEND ───────────────────────────────────────────────────────
    ├── Views/
    │   ├── Admin/                                Dashboard, Users, Properties, ActivityLog, RoleRequests, GlobalSearch, Notifications, ...
    │   ├── Manager/                              Dashboard, Units, Applications, Payments, Maintenance
    │   ├── Tenant/                                Dashboard, Apply, MyApplications, MyPayments, MyMaintenance, Notifications
    │   ├── Maintenance/                          Dashboard, Jobs, JobDetails, Notifications
    │   ├── Home/
    │   │   └── Index.cshtml                     landing page — welcome message + role-based dashboard link
    │   └── Shared/
    │       ├── _Layout.cshtml
    │       ├── _LoginPartial.cshtml
    │       └── _NotificationList.cshtml         shared notifications page partial used by every role
    │
    ├── wwwroot/
    │   └── css/site.css                        all custom CSS and Bootstrap overrides
    │
    │  # ── AUTHENTICATION ─────────────────────────────────────────────────
    ├── Areas/Identity/Pages/Account/
    │   ├── Login.cshtml / .cs, Register.cshtml / .cs, ExternalLogin.cshtml / .cs
    │   ├── ForgotPassword.cshtml / .cs, ResetPassword.cshtml / .cs (+ confirmation pages)
    │   ├── Lockout.cshtml, AccessDenied.cshtml, and other Identity scaffold pages
    │   └── Manage/
    │       ├── Index.cshtml / .cs              My Profile (edit name, phone)
    │       └── ChangePassword.cshtml / .cs
    │
    │  # ── CONFIG ──────────────────────────────────────────────────────────
    ├── appsettings.json                         DB + Google OAuth secrets (gitignored)
    └── appsettings.example.json                 safe template to share with teammates

# ── SERVERLESS (Task 2) ─────────────────────────────────────────────────
aws/                                              not part of the .NET project — reference only
├── lambda/index.mjs                              Lambda source, paste into the AWS console
├── s3-cors.json                                  bucket CORS config
└── README.md                                     console setup steps: bucket → Lambda → API Gateway
```

---

### Which folder do I work in?

| I want to... | Touch this |
|---|---|
| Add or change a page / UI | `Views/` |
| Add or change business logic / an action | `Backend/Controllers/` |
| Add a new DB table or new field | `Database/Models/` → run `dotnet ef migrations add <Name>` |
| Pass cleaner data from controller to view | `Backend/ViewModels/` |
| Change DB connection or auth settings | `appsettings.json` + `Program.cs` |
| Change login / register / My Profile flow | `Areas/Identity/` |
| Add notifications to a role that doesn't have them yet | See `AppControllerBase` in `CLAUDE.md` |

### Role ownership

| Role | Controller | Views folder |
|---|---|---|
| Admin | `Backend/Controllers/AdminController.cs` | `Views/Admin/` |
| Tenant | `Backend/Controllers/TenantController.cs` | `Views/Tenant/` |
| Property Manager | `Backend/Controllers/ManagerController.cs` | `Views/Manager/` |
| Maintenance Staff | `Backend/Controllers/MaintenanceController.cs` | `Views/Maintenance/` |

> `Database/Models/`, `Database/Data/`, and `Areas/Identity/` are **shared by all roles** — do not modify these unless adding a new DB table.

---

## Roles

| Role | Description |
|---|---|
| Admin | Full system access — user management, property oversight, activity log, role approvals, global search, CSV export |
| PropertyManager | Manages assigned properties, units, applications, payments, and maintenance |
| Tenant | Default role on registration; browses/applies for units, tracks payments and maintenance requests, can request a role upgrade |
| MaintenanceStaff | Receives and updates assigned maintenance requests |

---

## Database

Hosted on [NeonDB](https://neon.tech) (serverless PostgreSQL). The connection string goes in `appsettings.json` (not committed). Migrations are managed with EF Core.

---

## Branches

| Branch | Purpose |
|---|---|
| `main` | Stable, deployable |
| `dev` | Integration branch |
| `feature/admin` | Admin panel (merged) |
| `feature/manager` | Property Manager panel (merged) |
| `feature/tenant` | Tenant panel (merged) |
| `feature/maintenance` | Maintenance Staff panel (merged) |
