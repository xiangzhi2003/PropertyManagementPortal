namespace PropertyManagementPortal.ViewModels.Admin
{
    public class DashboardViewModel
    {
        public string AdminName { get; set; } = "";
        public int TotalUsers { get; set; }
        public int TotalProperties { get; set; }
        public int PendingMaintenance { get; set; }
        public int OverduePayments { get; set; }
    }
}
