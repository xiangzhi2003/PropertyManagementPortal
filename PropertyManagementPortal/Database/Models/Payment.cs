using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PropertyManagementPortal.Models
{
    public class Payment
    {
        [Key]
        public int PaymentId { get; set; }

        public int TenancyId { get; set; }

        [Required]
        public decimal Amount { get; set; }

        public DateTime? PaymentDate { get; set; }

        public DateTime DueDate { get; set; }

        [Required]
        public string Status { get; set; } = string.Empty; // Paid/Pending/Overdue

        [MaxLength(500)]
        public string? Notes { get; set; }

        [ForeignKey(nameof(TenancyId))]
        public Tenancy Tenancy { get; set; } = null!;
    }
}
