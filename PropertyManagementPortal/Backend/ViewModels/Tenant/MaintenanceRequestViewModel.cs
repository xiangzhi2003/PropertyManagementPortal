using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace PropertyManagementPortal.ViewModels.Tenant
{
    public class MaintenanceRequestViewModel
    {
        [Required]
        public int UnitId { get; set; }

        [Required]
        public string Category { get; set; } = string.Empty;

        [Required]
        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty;

        public List<SelectListItem> Units { get; set; } = new();
    }
}