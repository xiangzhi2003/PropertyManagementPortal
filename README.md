# PropertyManagementPortal

![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-8.0-512BD4?logo=dotnet)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-NeonDB-336791?logo=postgresql)
![Bootstrap](https://img.shields.io/badge/Bootstrap-5-7952B3?logo=bootstrap)

A centralized web platform for managing properties, tenants, maintenance requests, and payments. Built as a university assignment for **CT071-3-3-DDAC** (Designing and Developing ASP.NET Core Applications).

---

## Features

### Admin
- Dashboard with live stats (users, properties, pending maintenance, overdue payments)
- Manage Users — create, edit, view, activate/deactivate, delete users across all roles
- Manage Properties — add, edit, view, toggle status, assign property managers
- Reports — occupancy rates, payment breakdown, maintenance request status
- Activity Log — paginated audit trail of all admin actions

### All Users
- Login with email/password or Google OAuth
- My Profile — edit full name and phone number; view role, account status, member since
- Change Password

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
