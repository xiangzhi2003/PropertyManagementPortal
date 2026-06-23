# PropertyManagementPortal

![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-8.0-512BD4?logo=dotnet)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-NeonDB-336791?logo=postgresql)
![Bootstrap](https://img.shields.io/badge/Bootstrap-5-7952B3?logo=bootstrap)

A centralized web platform for managing properties, tenants, maintenance requests, and payments. Built as a university assignment for **CT071-3-3-DDAC** (Designing and Developing ASP.NET Core Applications).

---

## Features

### All Users
- Login with email/password or Google OAuth
- My Profile — edit full name and phone number; view role, account status, member since
- Change Password

---

### Admin ✅

**Dashboard**
- Stat cards: Total Users, Total Properties, Pending Maintenance, Overdue Payments — each links to the relevant page
- Welcome message with admin name and recent activity feed

**Manage Users**
- Table view: name, email, phone, role badge, status badge
- Search and filter by name, email, role, or status
- Create new user (PropertyManager, Tenant, MaintenanceStaff)
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

---

### Maintenance Staff 🔧 *(in progress)*

**Dashboard**
- Assigned jobs count by status
- Latest assigned job highlight
- Completed jobs this month count

**Assigned Jobs**
- View all jobs assigned to them with unit, property, issue description, tenant, date, and status
- Filter by status (Assigned / InProgress / Completed)
- Click through to full job details

**Update Job Status**
- Assigned → InProgress when starting work
- InProgress → Completed when done
- Each status change requires a note/comment
- Tenant and Property Manager notified on each change

**Upload Repair Evidence**
- Upload photo of completed repair
- Add completion notes (work done, parts used)
- Required when marking a job as Completed

**Notifications**
- New job assignment alerts
- Follow-up comments from Property Manager

---

### Property Manager 🏢 *(planned)*
### Tenant 🏠 *(planned)*

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
   The app seeds an Admin user and all roles on first startup.

---

## Project Structure

```
PropertyManagementPortal/
├── Controllers/
│   ├── HomeController.cs
│   └── AdminController.cs
├── Models/
│   ├── ApplicationUser.cs      # Extends IdentityUser
│   ├── Property.cs
│   ├── Unit.cs
│   ├── Tenancy.cs
│   ├── Payment.cs
│   ├── MaintenanceRequest.cs
│   ├── MaintenanceUpdate.cs
│   ├── Notification.cs
│   └── ActivityLog.cs
├── ViewModels/Admin/           # View models for admin views
├── Views/
│   ├── Admin/                  # Admin panel views + _AdminLayout.cshtml
│   ├── Home/
│   └── Shared/
├── Areas/Identity/             # Scaffolded Identity pages (Login, Register, Manage)
├── Data/
│   ├── ApplicationDbContext.cs
│   └── SeedData.cs
├── Migrations/
└── wwwroot/css/site.css        # Custom CSS with Bootstrap 5 overrides
```

---

## Roles

| Role | Description |
|---|---|
| Admin | Full system access — user management, property oversight, reports |
| PropertyManager | Manages assigned properties, units, and tenants |
| Tenant | Views tenancy details, submits maintenance requests, tracks payments |
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
