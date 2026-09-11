using System;
using System.Collections.Generic;

namespace ERPaperless.Models
{
    public class ErFormViewModel
    {
        public string PatientId { get; set; }
        public string PatientName { get; set; }
        public string MrNo { get; set; }
        public string AdmissionNo { get; set; }
        public string BedNo { get; set; }
        public string AgeGender { get; set; }
        public DateTime AdmissionDate { get; set; }
        /// <summary>Selected triage color: Red, Orange, Yellow, Green, or Blue.</summary>
        public string TriageColor { get; set; }
        public VitalRecordViewModel LatestVital { get; set; }
        public IEnumerable<VitalRecordViewModel> VitalHistory { get; set; }
        public IEnumerable<ChiefComplaintItemViewModel> ChiefComplaints { get; set; }
        public IEnumerable<DrugAllergyItemViewModel> DrugAllergies { get; set; }
        public IEnumerable<ERLovOptionViewModel> PastHistories { get; set; }
        public IEnumerable<ERLovOptionViewModel> GcsOptions { get; set; }
        public IEnumerable<ERLovOptionViewModel> ConsciousnessOptions { get; set; }
        public IEnumerable<ERLovOptionViewModel> Spo2Options { get; set; }
        public IEnumerable<ERLovOptionViewModel> PlanterOptions { get; set; }
        public IEnumerable<ERLovOptionViewModel> CvsOptions { get; set; }
        public IEnumerable<ERLovOptionViewModel> RespiratoryOptions { get; set; }
        public IEnumerable<ERLovOptionViewModel> BowelSoundOptions { get; set; }
        public IEnumerable<ERLovOptionViewModel> AbdomenOptions { get; set; }
        public IEnumerable<ERLovOptionViewModel> ReceivedFromOptions { get; set; }
        public IEnumerable<ERLovOptionViewModel> OutcomeOptions { get; set; }
        public IEnumerable<ERServiceGroupViewModel> ServiceGroups { get; set; }
        public IEnumerable<ERLovOptionViewModel> ConditionUponReleaseOptions { get; set; }
        public IEnumerable<ERLovOptionViewModel> AdmissionCategoryOptions { get; set; }
        public IEnumerable<ERLovOptionViewModel> AdrOptions { get; set; }
        public IEnumerable<ERLovOptionViewModel> MedicinePackageOptions { get; set; }
        public IEnumerable<ERLovOptionViewModel> SurgicalPackageOptions { get; set; }
        public IEnumerable<ERLovOptionViewModel> DrugRouteOptions { get; set; }
        public IEnumerable<ERLovOptionViewModel> DocumentTypeOptions { get; set; }
        public IEnumerable<ERLovOptionViewModel> EmployeeOptions { get; set; }
        public IEnumerable<ERLovOptionViewModel> MoEmployeeOptions { get; set; }
        public IEnumerable<ERLovOptionViewModel> ConsultantOptions { get; set; }
        public IEnumerable<ERLovOptionViewModel> RelationOptions { get; set; }
        public IEnumerable<ERLovOptionViewModel> ReferralReasonOptions { get; set; }
        public IEnumerable<ERLovOptionViewModel> DischargeMedicineOptions { get; set; }
        public IEnumerable<ERLovOptionViewModel> DrugFrequencyOptions { get; set; }
        public IEnumerable<ERLovOptionViewModel> AdmissionTypeOptions { get; set; }
        public IEnumerable<ERLovOptionViewModel> AdmissionCareLevelOptions { get; set; }
        public IEnumerable<ERLovOptionViewModel> IpdMedicineOptions { get; set; }
        public IEnumerable<ERLovOptionViewModel> IpdInvestigationServiceOptions { get; set; }
        public IEnumerable<ERLovOptionViewModel> IpdRoutineServiceOptions { get; set; }
        public IEnumerable<ERLovOptionViewModel> DoseUnitOptions { get; set; }
        public IEnumerable<ERItemViewModel> ErItemOptions { get; set; }
        public IEnumerable<string> InvestigationTestOptions { get; set; }
        public OutcomeFormStateViewModel OutcomeForms { get; set; }
        public IpdAdmissionOrderStateViewModel IpdAdmissionOrder { get; set; }
        public AddVitalInputViewModel AddVitalInput { get; set; }
        public ErReviewFormStateViewModel ReviewForm { get; set; }
    }
}
