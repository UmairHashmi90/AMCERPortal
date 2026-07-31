using System;
using System.Collections.Generic;

namespace ERPaperless.Models
{
    public class ErReviewFormStateViewModel
    {
        public long ReviewFormCode { get; set; }
        public DateTime? SavedOn { get; set; }

        public string PastMedicalHistory { get; set; }
        public string FoodAllergy { get; set; }
        public string PastSurgicalHistory { get; set; }

        public int? GcsCode { get; set; }
        public int? PlanterCode { get; set; }
        public int? CvsCode { get; set; }
        public int? RespiratoryCode { get; set; }
        public int? BowelSoundCode { get; set; }
        public int? AbdomenCode { get; set; }
        public int? AdmissionCategoryCode { get; set; }
        public int? ReceivedFromCode { get; set; }
        public int? OutcomeCode { get; set; }
        public string DiscussedWith { get; set; }
        public string ReferredTo { get; set; }

        public string AssessmentDiagnosis { get; set; }

        public int? MedicinePackageCode { get; set; }
        public int? SurgicalPackageCode { get; set; }
        public int? ConditionUponReleaseCode { get; set; }
        public int? AdrCode { get; set; }
        public string TreatmentNotes { get; set; }
        public string ConsultantPlan { get; set; }

        public string NursingObservations { get; set; }
        public string NursingCarePlan { get; set; }

        public HashSet<int> SelectedPastHistoryCodes { get; set; } = new HashSet<int>();
        public List<InvestigationRowViewModel> Investigations { get; set; } = new List<InvestigationRowViewModel>();
        public List<PackageOrderRowViewModel> MedicineOrders { get; set; } = new List<PackageOrderRowViewModel>();
        public List<PackageOrderRowViewModel> SurgicalOrders { get; set; } = new List<PackageOrderRowViewModel>();
        public List<DocumentRowViewModel> Documents { get; set; } = new List<DocumentRowViewModel>();
    }
}
