using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PropertyManagementPortal.Models
{
    public class MaintenanceRequest
    {
        [Key]
        public int RequestId { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public int UnitId { get; set; }

        [Required]
        public string Category { get; set; } = string.Empty; // Plumbing/Electrical/Aircon/General/Other

        [Required]
        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty;

        public string? PhotoUrl { get; set; }

        [Required]
        public string Status { get; set; } = string.Empty; // Submitted/Assigned/InProgress/Completed

        public string? AssignedStaffId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(TenantId))]
        public ApplicationUser Tenant { get; set; } = null!;

        [ForeignKey(nameof(AssignedStaffId))]
        public ApplicationUser? AssignedStaff { get; set; }

        [ForeignKey(nameof(UnitId))]
        public Unit Unit { get; set; } = null!;

        public ICollection<MaintenanceUpdate> Updates { get; set; } = new List<MaintenanceUpdate>();
    }
}
