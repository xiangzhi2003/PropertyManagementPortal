using System.ComponentModel.DataAnnotations;

namespace PropertyManagementPortal.ViewModels.Admin
{
    public class EditUserViewModel
    {
        public string Id { get; set; } = "";

        [Required]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = "";

        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";

        [Phone]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        [Required]
        public string Role { get; set; } = "";

        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
