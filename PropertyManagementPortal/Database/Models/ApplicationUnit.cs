using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PropertyManagementPortal.Models
{
    public class UnitApplication
    {
        [Key]
        public int ApplicationId { get; set; }

        public int UnitId { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public string Status { get; set; } = "Pending"; 
        // Pending / Approved / Rejected

        public DateTime AppliedAt { get; set; } = DateTime.UtcNow;

        public string? Notes { get; set; }

        [ForeignKey(nameof(UnitId))]
        public Unit Unit { get; set; } = null!;

        [ForeignKey(nameof(TenantId))]
        public ApplicationUser Tenant { get; set; } = null!;
    }
}