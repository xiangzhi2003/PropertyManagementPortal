namespace PropertyManagementPortal.ViewModels.Maintenance
{
    public class MaintenanceDashboardViewModel
    {
        public string StaffName { get; set; } = "";

        // Job counts by status (scoped to the logged-in staff member)
        public int AssignedCount { get; set; }      // Status == "Assigned" (not started)
        public int InProgressCount { get; set; }    // Status == "InProgress"
        public int CompletedCount { get; set; }      // Status == "Completed" (all time)
        public int CompletedThisMonth { get; set; }  // Completed updates written this month

        // Total jobs still needing work (Assigned + InProgress)
        public int ActiveCount => AssignedCount + InProgressCount;

        // Latest active job assigned to this staff member (null if none)
        public bool HasLatestJob { get; set; }
        public int LatestJobId { get; set; }
        public string? LatestUnitNumber { get; set; }
        public string? LatestPropertyName { get; set; }
        public string? LatestCategory { get; set; }
        public string? LatestStatus { get; set; }
        public string? LatestPriority { get; set; }
        public DateTime LatestCreatedAt { get; set; }
    }
}
