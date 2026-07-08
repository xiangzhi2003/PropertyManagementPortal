namespace PropertyManagementPortal.ViewModels.Admin
{
    public class GlobalSearchViewModel
    {
        public string? Query { get; set; }
        public List<UserRowViewModel> Users { get; set; } = new();
        public List<PropertyManagementPortal.Models.Property> Properties { get; set; } = new();
        public List<PaymentDetailRow> Payments { get; set; } = new();

        public bool HasResults => Users.Any() || Properties.Any() || Payments.Any();
    }
}
