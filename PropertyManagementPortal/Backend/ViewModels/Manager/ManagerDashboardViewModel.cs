namespace PropertyManagementPortal.ViewModels.Manager
{
    public class ManagerDashboardViewModel
    {
        public string ManagerName { get; set; } = "";
 
        // Units overview
        public int TotalProperties { get; set; }
        public int TotalUnits { get; set; }
        public int OccupiedUnits { get; set; }
        public int VacantUnits { get; set; }
        public double OccupancyRate => TotalUnits == 0 ? 0 : Math.Round((double)OccupiedUnits / TotalUnits * 100, 1);
 
        // Pending tenant applications
        public int PendingApplications { get; set; }
 
        // Rent dues
        public int PendingPayments { get; set; }
        public int OverduePayments { get; set; }
 
        // Maintenance
        public int UnassignedMaintenance { get; set; }
    }
}