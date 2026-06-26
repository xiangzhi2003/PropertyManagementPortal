using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PropertyManagementPortal.Models
{
    public class MaintenanceUpdate
    {
        [Key]
        public int UpdateId { get; set; }

        public int RequestId { get; set; }

        public string StaffId { get; set; } = string.Empty;

        [Required]
        public string StatusUpdate { get; set; } = string.Empty;

        public string? EvidencePhotoUrl { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(RequestId))]
        public MaintenanceRequest MaintenanceRequest { get; set; } = null!;

        [ForeignKey(nameof(StaffId))]
        public ApplicationUser Staff { get; set; } = null!;
    }
}
