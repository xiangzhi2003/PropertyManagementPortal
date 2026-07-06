using PropertyManagementPortal.Models;
namespace PropertyManagementPortal.ViewModels.Tenant
{
    public class TenantDashboardViewModel
    {
        public ApplicationUser? User { get; set; }
        public RoleRequest? PendingRequest { get; set; }
        public RoleRequest? RejectedRequest { get; set; }
        public Tenancy? CurrentTenancy { get; set; }

        public int UnitCount { get; set; }
        public int ApplicationCount { get; set; }
        public int PendingApplicationsCount { get; set; }
        public int ActiveMaintenanceCount { get; set; }
        public decimal OutstandingPayments { get; set; }
    }
}
