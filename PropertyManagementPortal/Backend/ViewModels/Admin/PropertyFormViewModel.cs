using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PropertyManagementPortal.ViewModels.Admin
{
    public class PropertyFormViewModel
    {
        public int PropertyId { get; set; }

        [Required]
        public string Name { get; set; } = "";

        [Required]
        public string Address { get; set; } = "";

        [Required]
        public string Type { get; set; } = "";

        public string? ManagerId { get; set; }

        public List<SelectListItem> ManagerOptions { get; set; } = new();

        public static readonly List<string> PropertyTypes =
            new() { "Apartment", "Condo", "House", "Commercial" };
    }
}
