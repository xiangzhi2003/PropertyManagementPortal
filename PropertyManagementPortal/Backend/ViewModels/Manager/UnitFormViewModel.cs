using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
 
namespace PropertyManagementPortal.ViewModels.Manager
{
    public class UnitFormViewModel
    {
        public int UnitId { get; set; }
 
        public int PropertyId { get; set; }
 
        // Display only — shown on the Edit page so the manager knows which
        // property the unit belongs to (property is not editable after creation).
        public string? PropertyName { get; set; }
 
        // Populates the property picker on the Add form (manager's own properties).
        public List<SelectListItem> PropertyOptions { get; set; } = new();
 
        [Required]
        [MaxLength(20)]
        [Display(Name = "Unit Number")]
        public string UnitNumber { get; set; } = "";
 
        [Required]
        public string Type { get; set; } = "";
 
        [Required]
        [Range(0.01, 1000000, ErrorMessage = "Rent must be greater than 0.")]
        [Display(Name = "Rent Amount")]
        public decimal RentAmount { get; set; }
 
        [Required]
        public string Status { get; set; } = "";
 
        [Range(0, 300)]
        public int Floor { get; set; }
 
        [MaxLength(500)]
        public string? Description { get; set; }
 
        public static readonly List<string> UnitTypes =
            new() { "Studio", "1BR", "2BR", "3BR" };
 
        public static readonly List<string> UnitStatuses =
            new() { "Vacant", "Occupied" };
    }
}