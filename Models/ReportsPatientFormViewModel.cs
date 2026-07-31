namespace ERPaperless.Models
{
    using System.Collections.Generic;

    public class ReportsPatientFormViewModel
    {
        public PatientFormContextViewModel Patient { get; set; }
        public int AdmissionCode { get; set; }
        public List<ReportInvestigationRowViewModel> Investigations { get; set; }
            = new List<ReportInvestigationRowViewModel>();
        public List<PrescriptionVisitRowViewModel> Prescriptions { get; set; }
            = new List<PrescriptionVisitRowViewModel>();
    }
}
