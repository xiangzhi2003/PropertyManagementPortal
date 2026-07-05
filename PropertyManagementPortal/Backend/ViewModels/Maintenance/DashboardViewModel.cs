namespace PropertyManagementPortal.ViewModels.Maintenance
{
    public class DashboardViewModel
    {
        public string StaffName { get; set; } = "";
        public int TotalAssignedRequests { get; set; }
        public int InProgressRequests { get; set; }
        public int CompletedRequests { get; set; }
    }
}