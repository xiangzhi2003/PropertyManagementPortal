# PropertyManagementPortal

![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-8.0-512BD4?logo=dotnet)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-NeonDB-336791?logo=postgresql)
![Bootstrap](https://img.shields.io/badge/Bootstrap-5-7952B3?logo=bootstrap)

A centralized web platform for managing properties, tenants, maintenance requests, and payments. Built as a university assignment for **CT071-3-3-DDAC** (Designing and Developing ASP.NET Core Applications).

---

## Features

### All Users
- Register with email/password or Google OAuth — automatically assigned **Tenant** role
- Login with email/password or Google OAuth
- My Profile — edit full name and phone number; view role, account status, member since
- Change Password
- Role-based dashboard routing after login

---

### Admin ✅

**Dashboard**
- Stat cards: Total Users, Total Properties, Pending Maintenance, Overdue Payments — each links to the relevant page
- Welcome message with admin name and recent activity feed

**Manage Users**
- Table view: name, email, phone, role badge, status badge
- Search and filter by name, email, role, or status
- Create new user (any role)
- Edit user details and role assignment (email is read-only)
- View full user detail page
- Toggle activate / deactivate (Admin accounts protected)
- Delete user (blocked if user has active tenancies or maintenance requests)

**Manage Properties**
- Table view: name, address, type, assigned manager, unit count, status
- Search and filter by name, type, or status
- Add new property and assign to a Property Manager
- Edit property details
- View property detail page with units list
- Toggle Active / Inactive status

**System Reports**
- Occupancy Report — total units, occupied vs vacant, occupancy rate %
- Payment Summary — total, paid, pending, overdue counts
- Maintenance Summary — submitted, assigned, in-progress, completed counts

**Activity Log**
- Paginated audit trail of all system actions — who, what, when

**Role Requests**
- Tenants can request to become a Property Manager or Maintenance Staff
- Pending request count shown as a badge in the admin sidebar
- Approve — instantly changes the user's role
- Reject — marks request as rejected with an optional reason note

---

### Tenant 🏠 *(partial)*

**Role Upgrade Request**
- Dashboard shows two cards: request Property Manager or Maintenance Staff role
- Pending requests show a status banner — upgrade cards hidden until resolved
- Rejected requests show the admin's reason — user can re-submit

*Full tenant features (tenancy view, maintenance requests, payment history) — coming soon*

---

### Property Manager 🏢 *(stub — coming soon)*
### Maintenance Staff 🔧 *(stub — coming soon)*

---

## Tech Stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core MVC 8 |
| ORM | Entity Framework Core 8 |
| Database | PostgreSQL (NeonDB serverless) via Npgsql |
| Authentication | ASP.NET Core Identity + Google OAuth 2.0 |
| UI | Bootstrap 5, Inter font, custom CSS |

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

2. **Create `appsettings.json`** in the `PropertyManagementPortal/` project folder (gitignored — never committed):
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
     "Logging": {
       "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" }
     },
     "AllowedHosts": "*"
   }
   ```

3. **Apply database migrations**
   ```bash
   dotnet ef database update
   ```

4. **Run**
   ```bash
   dotnet run
   ```
   The app seeds all roles on first startup. Create an Admin user via the Register page then assign the Admin role via the database or a seed script.

---

## How the Code Is Organized (MVC Pattern)

This project follows the **MVC (Model-View-Controller)** pattern. Every feature is split into three parts:

### Areas (Login & Registration)
`Areas/Identity/` contains all the login, register, and account management pages. These are provided by ASP.NET Core Identity (a built-in auth library) and we customized them. Think of Areas as a sub-application — it has its own pages, layouts, and logic, separate from the main MVC structure.

### Models (`Models/`)
Models represent the **database tables**. Each `.cs` file in `Models/` maps directly to a table in PostgreSQL via Entity Framework Core. You define the fields (columns) here, and EF Core creates/updates the actual table when you run `dotnet ef database update`.

Example: `Unit.cs` has fields `UnitNumber`, `Type`, `RentAmount`, `Floor`, `Status` → these become columns in the `Units` table.

### Controllers (`Controllers/`)
Controllers are the **brain**. When a user clicks a link or submits a form, a controller action runs. It:
1. Reads data from the database (via `ApplicationDbContext`)
2. Packages the data into a ViewModel (or passes a Model directly)
3. Returns a View to display

Example: User visits `/Admin/Users` → `AdminController.Users()` runs → queries the DB for all users → passes the list to `Views/Admin/Users.cshtml`.

Each role has its own controller:
- `AdminController.cs` — handles all admin pages
- `TenantController.cs` — handles tenant pages
- `ManagerController.cs` — handles property manager pages
- `MaintenanceController.cs` — handles maintenance staff pages

Controllers are protected with `[Authorize(Roles = "RoleName")]` so only the right role can access them.

### Views (`Views/`)
Views are the **HTML pages** the user sees. They receive data from the controller and render it. Views use Razor syntax (`.cshtml`) which is HTML mixed with C# `@` expressions.

Each role has its own subfolder:
- `Views/Admin/` — all admin pages
- `Views/Tenant/` — tenant pages
- `Views/Manager/` — property manager pages
- `Views/Maintenance/` — maintenance staff pages
- `Views/Shared/` — shared layouts and partials used by all roles

### ViewModels (`ViewModels/`)
ViewModels are **data containers made for a specific view**. Instead of passing a raw database model (which may have 20+ fields), the controller creates a ViewModel with only the fields that page needs.

Example: `EditUserViewModel` has `FullName`, `PhoneNumber`, `Role`, `CreatedAt` — exactly what the Edit User page needs. The raw `ApplicationUser` DB model also has password hashes, security stamps, and other Identity fields the view should never touch.

Only the Admin panel uses ViewModels right now (it's the most complex). Other roles can pass models directly for simpler pages.

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

### Data Layer (`Data/`)
- `ApplicationDbContext.cs` — the EF Core database context. It holds a `DbSet<>` for every model (table). Controllers inject this to read/write the DB.
- `SeedData.cs` — runs on startup to create the four roles (Admin, PropertyManager, Tenant, MaintenanceStaff) if they don't exist yet.

### Migrations (`Migrations/`)
Every time a Model is added or changed, a migration is created with `dotnet ef migrations add <Name>`. Migrations are the history of DB schema changes. Run `dotnet ef database update` to apply them to the actual database. **Never edit migration files manually.**

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
    │  # Business logic. Handles HTTP requests, talks to DB, decides what to show.
    │
    ├── Backend/
    │   ├── Controllers/
    │   │   ├── HomeController.cs                role-based redirect after login
    │   │   ├── AdminController.cs               all admin actions (users, properties, units, reports)
    │   │   ├── TenantController.cs              tenant dashboard + role upgrade requests
    │   │   ├── ManagerController.cs             property manager panel (stub)
    │   │   └── MaintenanceController.cs         maintenance staff panel (stub)
    │   │
    │   └── ViewModels/Admin/                    data shapes passed from controller to view
    │       ├── DashboardViewModel.cs
    │       ├── UserListViewModel.cs
    │       ├── CreateUserViewModel.cs
    │       ├── EditUserViewModel.cs
    │       ├── PropertyFormViewModel.cs
    │       └── ReportsViewModel.cs
    │
    ├── Program.cs                               app startup, middleware, DI, auth config
    │
    │  # ── DATABASE ───────────────────────────────────────────────────────
    │  # Data layer. Defines DB tables and connects them to PostgreSQL via EF Core.
    │
    ├── Database/
    │   ├── Models/                              database table definitions
    │   │   ├── ApplicationUser.cs               user account (extends IdentityUser)
    │   │   ├── Property.cs                      property record
    │   │   ├── Unit.cs                          unit within a property
    │   │   ├── Tenancy.cs                       lease linking a tenant to a unit
    │   │   ├── Payment.cs                       rent payment record
    │   │   ├── MaintenanceRequest.cs            maintenance job
    │   │   ├── MaintenanceUpdate.cs             status update / note on a job
    │   │   ├── Notification.cs                  in-app notifications
    │   │   ├── ActivityLog.cs                   admin audit trail
    │   │   └── RoleRequest.cs                   tenant role upgrade request
    │   │
    │   ├── Data/
    │   │   ├── ApplicationDbContext.cs          EF Core DbContext — connects models to DB
    │   │   └── SeedData.cs                     seeds the 4 roles on first startup
    │   │
    │   └── Migrations/                          auto-generated DB schema history
    │                                            ⚠ never edit these files manually
    │
    │  # ── FRONTEND ───────────────────────────────────────────────────────
    │  # Presentation layer. HTML pages and static files the browser receives.
    │
    ├── Views/                                   Razor .cshtml templates
    │   ├── Admin/
    │   │   ├── _AdminLayout.cshtml              sidebar layout used by all admin pages
    │   │   ├── Dashboard.cshtml
    │   │   ├── Users.cshtml
    │   │   ├── CreateUser.cshtml
    │   │   ├── EditUser.cshtml
    │   │   ├── ViewUser.cshtml
    │   │   ├── Properties.cshtml
    │   │   ├── AddProperty.cshtml
    │   │   ├── EditProperty.cshtml
    │   │   ├── ViewProperty.cshtml              includes add / edit / delete unit
    │   │   ├── EditUnit.cshtml
    │   │   ├── Reports.cshtml
    │   │   ├── ActivityLog.cshtml
    │   │   └── RoleRequests.cshtml
    │   ├── Tenant/
    │   │   └── Dashboard.cshtml                role upgrade request UI
    │   ├── Manager/
    │   │   └── Dashboard.cshtml                stub — coming soon
    │   ├── Maintenance/
    │   │   └── Dashboard.cshtml                stub — coming soon
    │   ├── Home/
    │   │   └── Index.cshtml                    landing page + role-based redirect
    │   └── Shared/
    │       ├── _Layout.cshtml                  public layout (login, register, home)
    │       └── _LoginPartial.cshtml            login / logout nav buttons
    │
    ├── wwwroot/                                 static files served directly to browser
    │   └── css/site.css                        all custom CSS and Bootstrap overrides
    │
    │  # ── AUTHENTICATION ─────────────────────────────────────────────────
    │  # Login, register, Google OAuth, My Profile — powered by ASP.NET Core Identity.
    │
    ├── Areas/Identity/Pages/Account/
    │   ├── Login.cshtml / .cs                  email + Google OAuth login
    │   ├── Register.cshtml / .cs               registration — auto-assigns Tenant role
    │   ├── ExternalLogin.cshtml / .cs          Google OAuth callback
    │   └── Manage/
    │       ├── Index.cshtml / .cs              My Profile (edit name, phone)
    │       └── ChangePassword.cshtml / .cs
    │
    │  # ── CONFIG ──────────────────────────────────────────────────────────
    │  # Environment-specific settings. Never commit appsettings.json.
    │
    ├── appsettings.json                         DB + Google OAuth secrets (gitignored)
    └── appsettings.example.json                 safe template to share with teammates
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

### Role ownership

| Role | Controller | Views folder |
|---|---|---|
| Admin | `Backend/Controllers/AdminController.cs` | `Views/Admin/` |
| Tenant | `Backend/Controllers/TenantController.cs` | `Views/Tenant/` |
| Property Manager | `Backend/Controllers/ManagerController.cs` | `Views/Manager/` |
| Maintenance Staff | `Backend/Controllers/MaintenanceController.cs` | `Views/Maintenance/` |

> `Models/`, `Data/`, and `Areas/Identity/` are **shared by all roles** — do not modify these unless adding a new DB table.

---

## Roles

| Role | Description |
|---|---|
| Admin | Full system access — user management, property oversight, reports, role approvals |
| PropertyManager | Manages assigned properties, units, and tenants |
| Tenant | Default role on registration; can request role upgrade |
| MaintenanceStaff | Receives and updates assigned maintenance requests |

---

## Database

Hosted on [NeonDB](https://neon.tech) (serverless PostgreSQL). The connection string goes in `appsettings.json` (not committed). Migrations are managed with EF Core.

---

## Branches

| Branch | Purpose |
|---|---|
| `main` | Stable, production-ready |
| `dev` | Integration branch |
| `feature/admin` | Admin panel (complete) |
| `feature/manager` | Property Manager panel |
| `feature/tenant` | Tenant panel |
| `feature/maintenance` | Maintenance Staff panel |
