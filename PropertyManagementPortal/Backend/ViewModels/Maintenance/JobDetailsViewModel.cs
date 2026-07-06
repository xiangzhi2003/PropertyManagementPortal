namespace PropertyManagementPortal.ViewModels.Maintenance
{
    public class JobDetailsViewModel
    {
        public int RequestId { get; set; }
        public string Category { get; set; } = "";
        public string Description { get; set; } = "";
        public string? PhotoUrl { get; set; }          // tenant's photo of the issue
        public string Status { get; set; } = "";
        public string? Priority { get; set; }
        public string? AssignmentNotes { get; set; }
        public DateTime CreatedAt { get; set; }

        // Flattened tenant + location (resolved in the query)
        public string TenantName { get; set; } = "";
        public string? TenantPhone { get; set; }
        public string PropertyName { get; set; } = "";
        public string UnitNumber { get; set; } = "";

        // History, projected — not raw entities
        public List<UpdateRowViewModel> Updates { get; set; } = new();
    }

    public class UpdateRowViewModel
    {
        public string StatusUpdate { get; set; } = "";
        public string? Notes { get; set; }
        public string? EvidencePhotoUrl { get; set; }
        public string StaffName { get; set; } = "";
        public DateTime UpdatedAt { get; set; }
    }
}
