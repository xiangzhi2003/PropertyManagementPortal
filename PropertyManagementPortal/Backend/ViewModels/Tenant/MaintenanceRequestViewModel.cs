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

        public string Description { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        // The browser uploads straight to S3 via a presigned URL and posts back only
        // the resulting object key — the file itself never touches this server.
        public string? PhotoObjectKey { get; set; }

        public List<SelectListItem> Units { get; set; } = new();
    }
}