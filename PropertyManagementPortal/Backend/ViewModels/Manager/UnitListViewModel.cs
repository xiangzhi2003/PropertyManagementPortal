using Microsoft.AspNetCore.Mvc.Rendering;
 
namespace PropertyManagementPortal.ViewModels.Manager
{
    public class UnitListViewModel : PaginatedViewModel
    {
        public List<UnitRowViewModel> Units { get; set; } = new();
 
        // Filters (round-trip back to the form so selections persist)
        public int? PropertyFilter { get; set; }
        public string? StatusFilter { get; set; }
        public string? SearchTerm { get; set; }
 
        // Dropdown source, reused by the filter bar and the Add-Unit modal
        public List<SelectListItem> PropertyOptions { get; set; } = new();
    }
 
    public class UnitRowViewModel
    {
        public int UnitId { get; set; }
        public string PropertyName { get; set; } = "";
        public string UnitNumber { get; set; } = "";
        public string Type { get; set; } = "";
        public decimal RentAmount { get; set; }
        public string Status { get; set; } = "";
        public int Floor { get; set; }
    }
}