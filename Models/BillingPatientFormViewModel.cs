using System.Collections.Generic;

namespace ERPaperless.Models
{
    public class BillingPatientFormViewModel
    {
        public PatientFormContextViewModel Patient { get; set; }

        public bool ShowAllPatients => Patient == null;

        public List<InvestigationRowViewModel> Investigations { get; set; } = new List<InvestigationRowViewModel>();

        public static BillingPatientFormViewModel CreateAll(List<InvestigationRowViewModel> investigations)
        {
            return new BillingPatientFormViewModel
            {
                Patient = null,
                Investigations = investigations ?? new List<InvestigationRowViewModel>()
            };
        }

        public static BillingPatientFormViewModel Create(
            PatientFormContextViewModel patient,
            List<InvestigationRowViewModel> investigations)
        {
            return new BillingPatientFormViewModel
            {
                Patient = patient,
                Investigations = investigations ?? new List<InvestigationRowViewModel>()
            };
        }
    }
}
