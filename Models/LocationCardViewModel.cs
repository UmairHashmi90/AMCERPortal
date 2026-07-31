using System;

namespace ERPaperless.Models
{
    public class LocationCardViewModel
    {
        public int BedId { get; set; }
        public int BranchCode { get; set; }
        public int AdmissionCode { get; set; }
        public string PatientId { get; set; }
        public string SlotName { get; set; }
        public bool IsChair { get; set; }
        public string PatientName { get; set; }
        public string AgeGender { get; set; }
        public string MrNo { get; set; }
        public string AdmissionNo { get; set; }
        public DateTime? AdmissionDate { get; set; }
        public string StateClass { get; set; }
        public string StateLabel { get; set; }
        public bool ShowViewFormAction { get; set; }

        public bool HasAdmission => AdmissionCode > 0;
    }
}
