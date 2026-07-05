using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PropertyManagementPortal.Models;

namespace PropertyManagementPortal.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Property> Properties { get; set; }
        public DbSet<Unit> Units { get; set; }
        public DbSet<Tenancy> Tenancies { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<MaintenanceRequest> MaintenanceRequests { get; set; }
        public DbSet<MaintenanceUpdate> MaintenanceUpdates { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<ActivityLog> ActivityLogs { get; set; }
        public DbSet<RoleRequest> RoleRequests { get; set; }
        public DbSet<UnitApplication> UnitApplications { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ApplicationUser>()
                .Property(u => u.PhoneNumber)
                .HasMaxLength(20);

            // Property → ApplicationUser (Manager): nullable FK
            builder.Entity<Property>()
                .HasOne(p => p.Manager)
                .WithMany(u => u.ManagedProperties)
                .HasForeignKey(p => p.ManagerId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            // Unit → Property: cascade
            builder.Entity<Unit>()
                .HasOne(u => u.Property)
                .WithMany(p => p.Units)
                .HasForeignKey(u => u.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);

            // Tenancy → Unit: restrict
            builder.Entity<Tenancy>()
                .HasOne(t => t.Unit)
                .WithMany(u => u.Tenancies)
                .HasForeignKey(t => t.UnitId)
                .OnDelete(DeleteBehavior.Restrict);

            // Tenancy → ApplicationUser (Tenant): restrict
            builder.Entity<Tenancy>()
                .HasOne(t => t.Tenant)
                .WithMany(u => u.Tenancies)
                .HasForeignKey(t => t.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            // Payment → Tenancy: cascade
            builder.Entity<Payment>()
                .HasOne(p => p.Tenancy)
                .WithMany(t => t.Payments)
                .HasForeignKey(p => p.TenancyId)
                .OnDelete(DeleteBehavior.Cascade);

            // MaintenanceRequest → ApplicationUser (Tenant): restrict
            builder.Entity<MaintenanceRequest>()
                .HasOne(m => m.Tenant)
                .WithMany(u => u.MaintenanceRequests)
                .HasForeignKey(m => m.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            // MaintenanceRequest → ApplicationUser (AssignedStaff): nullable, no action
            builder.Entity<MaintenanceRequest>()
                .HasOne(m => m.AssignedStaff)
                .WithMany(u => u.AssignedMaintenanceRequests)
                .HasForeignKey(m => m.AssignedStaffId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            // MaintenanceRequest → Unit: restrict
            builder.Entity<MaintenanceRequest>()
                .HasOne(m => m.Unit)
                .WithMany(u => u.MaintenanceRequests)
                .HasForeignKey(m => m.UnitId)
                .OnDelete(DeleteBehavior.Restrict);

            // MaintenanceUpdate → MaintenanceRequest: cascade
            builder.Entity<MaintenanceUpdate>()
                .HasOne(mu => mu.MaintenanceRequest)
                .WithMany(m => m.Updates)
                .HasForeignKey(mu => mu.RequestId)
                .OnDelete(DeleteBehavior.Cascade);

            // MaintenanceUpdate → ApplicationUser (Staff): restrict
            builder.Entity<MaintenanceUpdate>()
                .HasOne(mu => mu.Staff)
                .WithMany(u => u.MaintenanceUpdates)
                .HasForeignKey(mu => mu.StaffId)
                .OnDelete(DeleteBehavior.Restrict);

            // Notification → ApplicationUser: cascade
            builder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
