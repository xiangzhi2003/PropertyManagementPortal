using Microsoft.AspNetCore.Mvc.Rendering;
 
using PropertyManagementPortal.ViewModels.Shared;
 
namespace PropertyManagementPortal.ViewModels.Manager
{
    public class MaintenanceListViewModel : PaginatedViewModel
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
 
        // Raw S3 object key — resolved to a viewable URL client-side by the view,
        // since this server has no AWS credentials in the serverless upload path.
        public string? PhotoUrl { get; set; }
    }
}