using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PropertyManagementPortal.Models
{
    public class Property
    {
        [Key]
        public int PropertyId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string Address { get; set; } = string.Empty;

        [Required]
        public string Type { get; set; } = string.Empty; // Apartment/Condo/House/Commercial

        [Required]
        public string Status { get; set; } = string.Empty; // Active/Inactive

        public string? ManagerId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(ManagerId))]
        public ApplicationUser? Manager { get; set; }

        public ICollection<Unit> Units { get; set; } = new List<Unit>();
    }
}
