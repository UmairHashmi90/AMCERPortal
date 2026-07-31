using System;

namespace ERPaperless.Models
{
    public class PatientFormContextViewModel
    {
        public string PatientId { get; set; }
        public string PatientName { get; set; }
        public string MrNo { get; set; }
        public string AdmissionNo { get; set; }
        public string BedNo { get; set; }
        public string AgeGender { get; set; }
        public DateTime AdmissionDate { get; set; }
        public int? AdmissionCode { get; set; }

        public bool HasAdmission => AdmissionCode.HasValue && AdmissionCode.Value > 0;
    }
}
