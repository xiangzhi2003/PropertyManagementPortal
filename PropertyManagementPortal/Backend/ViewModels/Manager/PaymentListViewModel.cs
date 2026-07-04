using Microsoft.AspNetCore.Mvc.Rendering;
 
namespace PropertyManagementPortal.ViewModels.Manager
{
    public class PaymentListViewModel
    {
        public List<PaymentRowViewModel> Payments { get; set; } = new();
 
        // Filters
        public string? StatusFilter { get; set; }
        public int? PropertyFilter { get; set; }
        public List<SelectListItem> PropertyOptions { get; set; } = new();
 
        // Summary (reflects all payments in the manager's units, ignoring filters)
        public int PendingCount { get; set; }
        public int OverdueCount { get; set; }
        public int PaidCount { get; set; }
        public decimal OutstandingAmount { get; set; }
    }
 
    public class PaymentRowViewModel
    {
        public int PaymentId { get; set; }
        public string TenantName { get; set; } = "";
        public string PropertyName { get; set; } = "";
        public string UnitNumber { get; set; } = "";
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? PaymentDate { get; set; }
        public string Status { get; set; } = "";
        public string? Notes { get; set; }
    }
}