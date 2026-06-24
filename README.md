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

## Project Structure

### Shared Infrastructure (all roles)

| File | Purpose |
|---|---|
| `Program.cs` | App startup, DI, auth config, role seeding |
| `appsettings.json` | DB + Google OAuth secrets (**gitignored**) |
| `appsettings.example.json` | Safe template to share with teammates |
| `Data/ApplicationDbContext.cs` | EF Core DbContext, all DbSets, model config |
| `Data/SeedData.cs` | Seeds all four roles on startup |
| `Views/Shared/_Layout.cshtml` | Public layout (login/register/home pages) |
| `Views/Home/Index.cshtml` | Landing page + role-based redirect after login |
| `wwwroot/css/site.css` | All custom CSS (shared + admin-specific classes) |

**Identity pages** (`Areas/Identity/Pages/Account/`) — used by all roles:

| File | Purpose |
|---|---|
| `Login.cshtml / .cs` | Email + Google OAuth login |
| `Register.cshtml / .cs` | Registration — auto-assigns Tenant role |
| `ExternalLogin.cshtml / .cs` | Google OAuth callback — auto-assigns Tenant role |
| `Manage/Index.cshtml / .cs` | My Profile (edit name, phone; view role/status) |
| `Manage/ChangePassword.cshtml / .cs` | Change password |
| `Manage/_ManageNav.cshtml` | Trimmed nav (only My Profile + Change Password visible) |

**Models** (`Models/`) — DB tables shared by all roles:

| File | Description |
|---|---|
| `ApplicationUser.cs` | Extends IdentityUser — adds FullName, IsActive, CreatedAt |
| `Property.cs` | Property record |
| `Unit.cs` | Unit within a property |
| `Tenancy.cs` | Links a Tenant user to a Unit (lease) |
| `Payment.cs` | Rent payment record |
| `MaintenanceRequest.cs` | Maintenance job |
| `MaintenanceUpdate.cs` | Status update / note on a maintenance job |
| `Notification.cs` | In-app notifications |
| `ActivityLog.cs` | Admin audit trail |
| `RoleRequest.cs` | Tenant → Manager/Staff role upgrade request |

---

### Admin

> Controller: `Controllers/AdminController.cs` — `[Authorize(Roles = "Admin")]`
> Layout: `Views/Admin/_AdminLayout.cshtml`

| View | Purpose |
|---|---|
| `Views/Admin/Dashboard.cshtml` | Stat cards + recent activity feed |
| `Views/Admin/Users.cshtml` | User list with search/filter |
| `Views/Admin/CreateUser.cshtml` | Create new user form |
| `Views/Admin/EditUser.cshtml` | Edit user details + role |
| `Views/Admin/ViewUser.cshtml` | Read-only user detail page |
| `Views/Admin/Properties.cshtml` | Property list with search/filter |
| `Views/Admin/AddProperty.cshtml` | Add property form |
| `Views/Admin/EditProperty.cshtml` | Edit property form |
| `Views/Admin/ViewProperty.cshtml` | Property detail + units table (add/edit/delete) |
| `Views/Admin/EditUnit.cshtml` | Edit unit form |
| `Views/Admin/Reports.cshtml` | Occupancy, payment, maintenance summaries |
| `Views/Admin/ActivityLog.cshtml` | Paginated audit log |
| `Views/Admin/RoleRequests.cshtml` | Approve/reject tenant role upgrade requests |

ViewModels in `ViewModels/Admin/`: `DashboardViewModel`, `UserListViewModel`, `CreateUserViewModel`, `EditUserViewModel`, `PropertyFormViewModel`, `ReportsViewModel`

---

### Tenant *(partial)*

> Controller: `Controllers/TenantController.cs` — `[Authorize(Roles = "Tenant")]`

| File | Purpose |
|---|---|
| `Views/Tenant/Dashboard.cshtml` | Role upgrade request UI (request Manager or Staff role) |

*Tenancy details, maintenance requests, payment history — not yet built*

---

### Property Manager *(stub)*

> Controller: `Controllers/ManagerController.cs` — `[Authorize(Roles = "PropertyManager")]`

| File | Purpose |
|---|---|
| `Views/Manager/Dashboard.cshtml` | "Coming soon" placeholder |

---

### Maintenance Staff *(stub)*

> Controller: `Controllers/MaintenanceController.cs` — `[Authorize(Roles = "MaintenanceStaff")]`

| File | Purpose |
|---|---|
| `Views/Maintenance/Dashboard.cshtml` | "Coming soon" placeholder |

---

> **Rule of thumb:** each role owns its `Controllers/XController.cs` + `Views/X/` folder. `Models/`, `Data/`, and `Areas/Identity/` are shared — teammates generally don't need to modify those.

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
