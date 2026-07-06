namespace PropertyManagementPortal.ViewModels.Maintenance
{
    public class JobsViewModel
    {
        public string StaffName { get; set; } = "";

        // List of upcoming assigned jobs
        public List<JobRowViewModel> Jobs { get; set; } = new();

        // Filters
        public string? StatusFilter { get; set; }
    }

    public class JobRowViewModel
    {
        public int RequestId { get; set; }
        public string UnitNumber { get; set; } = "";
        public string PropertyName { get; set; } = "";
        public string Category { get; set; } = "";
        public string Description { get; set; } = "";
        public string TenantName { get; set; } = "";
        public string Status { get; set; } = "";
        public string Priority { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }
}