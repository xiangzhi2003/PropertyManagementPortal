using System.ComponentModel.DataAnnotations;

namespace PropertyManagementPortal.ViewModels.Maintenance
{
    // Bound from the Update-status modal on the Job Details page. The target status
    // is not carried here — the controller re-derives it from the current DB state.
    public class UpdateJobViewModel
    {
        public int RequestId { get; set; }

        [Required(ErrorMessage = "Please add a note describing this update.")]
        [MaxLength(1000)]
        public string Notes { get; set; } = "";

        // Required only when completing — enforced in the POST action.
        public IFormFile? EvidencePhoto { get; set; }
    }
}
