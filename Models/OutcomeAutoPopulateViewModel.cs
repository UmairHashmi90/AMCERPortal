namespace ERPaperless.Models
{
    public class OutcomeAutoPopulateViewModel
    {
        public string Diagnosis { get; set; }
        public string Treatment { get; set; }
        public string ChiefComplaints { get; set; }
        public string ConditionUponRelease { get; set; }
        public string Medicines { get; set; }
        public string Investigations { get; set; }
        public OutcomeAutoPopulateSavedFlags Discharge { get; set; }
        public OutcomeAutoPopulateSavedFlags Referral { get; set; }
    }

    public class OutcomeAutoPopulateSavedFlags
    {
        public bool IsFinal { get; set; }
        public bool HasDiagnosis { get; set; }
        public bool HasBriefHistory { get; set; }
        public bool HasPatientComplaint { get; set; }
        public bool HasTreatment { get; set; }
        public bool HasClinicalAssessment { get; set; }
        public bool HasCondition { get; set; }
        public bool HasInvestigations { get; set; }
    }
}
