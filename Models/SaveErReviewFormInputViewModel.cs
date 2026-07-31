using System.Collections.Generic;

namespace ERPaperless.Models
{
    public class SaveErReviewFormInputViewModel
    {
        public string PatientId { get; set; }
        public bool DischargeFinalize { get; set; }

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

        public int? ConditionUponReleaseCode { get; set; }
        public int? AdrCode { get; set; }
        public string TreatmentNotes { get; set; }
        public string ConsultantPlan { get; set; }

        public string NursingObservations { get; set; }
        public string NursingCarePlan { get; set; }

        public List<ChiefComplaintSaveItemViewModel> ChiefComplaints { get; set; }
        public List<DrugAllergySaveItemViewModel> DrugAllergies { get; set; }
        public List<int> PastHistoryCodes { get; set; }
        public List<InvestigationSaveItemViewModel> Investigations { get; set; }
    }

    public class ChiefComplaintSaveItemViewModel
    {
        public int Code { get; set; }
        public string Remark { get; set; }
    }

    public class DrugAllergySaveItemViewModel
    {
        public int Code { get; set; }
        public string Remark { get; set; }
    }

    public class InvestigationSaveItemViewModel
    {
        public long Id { get; set; }
        public int? ServiceGroupCode { get; set; }
        public string TestName { get; set; }
        public string Remarks { get; set; }
        public bool IsCancelled { get; set; }
    }
}
