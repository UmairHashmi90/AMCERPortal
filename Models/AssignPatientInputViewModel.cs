using System.ComponentModel.DataAnnotations;

namespace ERPaperless.Models
{
    public class AssignPatientInputViewModel
    {
        [Required(ErrorMessage = "Bed is required.")]
        public int BedId { get; set; }

        [Required(ErrorMessage = "Branch is required.")]
        public int BranchCode { get; set; }

        [Required(ErrorMessage = "Patient name is required.")]
        [StringLength(100)]
        public string PatientName { get; set; }

        [StringLength(10)]
        public string Gender { get; set; }

        [StringLength(20)]
        public string Age { get; set; }

        [StringLength(50)]
        public string MrNo { get; set; }
    }
}
