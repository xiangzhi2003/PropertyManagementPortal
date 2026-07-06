using System.ComponentModel.DataAnnotations;

namespace PropertyManagementPortal.ViewModels.Maintenance
{
    public class UpdateJobViewModel
    {
        public int RequestId { get; set; }

        // Read-only context shown on the form
        public string Category { get; set; } = "";
        public string PropertyName { get; set; } = "";
        public string UnitNumber { get; set; } = "";
        public string CurrentStatus { get; set; } = "";

        // The single allowed next status, computed from CurrentStatus by the controller.
        public string NextStatus { get; set; } = "";

        [Required(ErrorMessage = "Please add a note describing this update.")]
        [MaxLength(1000)]
        public string Notes { get; set; } = "";

        // Required only when completing — enforced in the POST action.
        [Display(Name = "Evidence Photo")]
        public IFormFile? EvidencePhoto { get; set; }

        public bool IsCompleting => NextStatus == "Completed";
    }
}
