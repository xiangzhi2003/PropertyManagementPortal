namespace PropertyManagementPortal.ViewModels.Manager
{
    public class ApplicationListViewModel : PaginatedViewModel
    {
        public List<ApplicationRowViewModel> Applications { get; set; } = new();
        public string? StatusFilter { get; set; }
        public int PendingCount { get; set; }
    }
 
    public class ApplicationRowViewModel
    {
        public int TenancyId { get; set; }
        public string TenantName { get; set; } = "";
        public string TenantEmail { get; set; } = "";
        public string PropertyName { get; set; } = "";
        public string UnitNumber { get; set; } = "";
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Status { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string? Notes { get; set; }
    }
}