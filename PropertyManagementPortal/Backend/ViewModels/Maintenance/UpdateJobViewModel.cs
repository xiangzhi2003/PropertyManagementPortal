using System.ComponentModel.DataAnnotations;

namespace PropertyManagementPortal.ViewModels.Maintenance
{
    // Bound from the Update-status modal on the Job Details page. The target status
    // is not carried here — the controller re-derives it from the current DB state.
    public class UpdateJobViewModel
    {
        public int RequestId { get; set; }

        // The status the staffer is moving the job to. Validated against the current
        // DB state in the POST — only forward moves are allowed (InProgress may be skipped).
        [Required]
        public string TargetStatus { get; set; } = "";

        [Required(ErrorMessage = "Describe what has been done.")]
        [MaxLength(1000)]
        public string Notes { get; set; } = "";

        // Required only when completing — enforced in the POST action.
        public IFormFile? EvidencePhoto { get; set; }
    }
}
