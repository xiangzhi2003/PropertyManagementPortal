namespace PropertyManagementPortal.ViewModels.Tenant
{
    public class PaymentViewModel
    {
        public string PropertyName { get; set; } = "";
        public string UnitNumber { get; set; } = "";
        public decimal Amount { get; set; }
        public DateTime? PaymentDate { get; set; }
        public DateTime DueDate { get; set; }
        public string Status { get; set; } = "";
    }
}