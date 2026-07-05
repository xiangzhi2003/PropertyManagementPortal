using Microsoft.AspNetCore.Mvc.Rendering;
 
namespace PropertyManagementPortal.ViewModels.Manager
{
    public class MaintenanceListViewModel
    {
        public List<MaintenanceRowViewModel> Requests { get; set; } = new();
 
        // Filters
        public string? StatusFilter { get; set; }
        public int? PropertyFilter { get; set; }
        public List<SelectListItem> PropertyOptions { get; set; } = new();
 
        // Staff to assign to (active MaintenanceStaff users)
        public List<SelectListItem> StaffOptions { get; set; } = new();
 
        public int UnassignedCount { get; set; }
 
        public static readonly List<string> Priorities =
            new() { "Low", "Medium", "High" };
    }
 
    public class MaintenanceRowViewModel
    {
        public int RequestId { get; set; }
        public string TenantName { get; set; } = "";
        public string PropertyName { get; set; } = "";
        public string UnitNumber { get; set; } = "";
        public string Category { get; set; } = "";
        public string Description { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string? AssignedStaffName { get; set; }
        public string? Priority { get; set; }
        public string? AssignmentNotes { get; set; }
    }
}