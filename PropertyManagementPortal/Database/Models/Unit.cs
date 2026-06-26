using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PropertyManagementPortal.Models
{
    public class Unit
    {
        [Key]
        public int UnitId { get; set; }

        public int PropertyId { get; set; }

        [Required]
        [MaxLength(20)]
        public string UnitNumber { get; set; } = string.Empty;

        [Required]
        public string Type { get; set; } = string.Empty; // Studio/1BR/2BR/3BR

        [Required]
        public decimal RentAmount { get; set; }

        [Required]
        public string Status { get; set; } = string.Empty; // Vacant/Occupied

        public int Floor { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [ForeignKey(nameof(PropertyId))]
        public Property Property { get; set; } = null!;

        public ICollection<Tenancy> Tenancies { get; set; } = new List<Tenancy>();
        public ICollection<MaintenanceRequest> MaintenanceRequests { get; set; } = new List<MaintenanceRequest>();
    }
}
