using System;
using System.Collections.Generic;

namespace ERPaperless.Models
{
    public class OutcomeFormStateViewModel
    {
        public ReferralFormStateViewModel Referral { get; set; } = new ReferralFormStateViewModel();
        public DischargeFormStateViewModel Discharge { get; set; } = new DischargeFormStateViewModel();
        public DeathFormStateViewModel Death { get; set; } = new DeathFormStateViewModel();
        public LamaFormStateViewModel Lama { get; set; } = new LamaFormStateViewModel();
    }

    public class ReferralFormStateViewModel
    {
        public bool IsFinal { get; set; }
        public int? ReferralReasonCode { get; set; }
        public DateTime? ReferralDateTime { get; set; }
        public int? MoOnDutyCode { get; set; }
        public string PatientComplaint { get; set; }
        public string ClinicalSummary { get; set; }
        public string Treatment { get; set; }
        public string PertinentInvestigation { get; set; }
        public string ClinicalDiagnosis { get; set; }
    }

    public class DischargeFormStateViewModel
    {
        public bool IsFinal { get; set; }
        public string FinalDiagnosis { get; set; }
        public string BriefHistory { get; set; }
        public string SurgeryProcedures { get; set; }
        public string DietInstructions { get; set; }
        public string ClinicalAssessment { get; set; }
        public string FollowUpInstructions { get; set; }
        public List<DischargeMedicineStateViewModel> Medicines { get; set; } = new List<DischargeMedicineStateViewModel>();
    }

    public class DischargeMedicineStateViewModel
    {
        public int? ItemCode { get; set; }
        public string DrugName { get; set; }
        public string Dose { get; set; }
        public string Frequency { get; set; }
        public int? Days { get; set; }
        public string Instruction { get; set; }
    }

    public class DeathFormStateViewModel
    {
        public bool IsFinal { get; set; }
        public int? PrimaryConsultantCode { get; set; }
        public DateTime? DeathDateTime { get; set; }
        public string PrimaryCause { get; set; }
        public string SecondaryCause { get; set; }
        public bool IsBroughtInDead { get; set; }
        public string MedicalCondition { get; set; }
        public string HandOverTo { get; set; }
        public string RelativeCnicPassport { get; set; }
        public int? RelationCode { get; set; }
        public DateTime? HandOverAt { get; set; }
        public int? DutyNurseCode { get; set; }
        public int? DutyMoCode { get; set; }
    }

    public class LamaFormStateViewModel
    {
        public bool IsFinal { get; set; }
        public string RequesterName { get; set; }
        public int? DutyNurseCode { get; set; }
        public int? DutyMoCode { get; set; }
        public int? RelationCode { get; set; }
        public DateTime? RequestedOn { get; set; }
        public string Reason { get; set; }
        public string DoctorRemarks { get; set; }
        public string NurseComments { get; set; }
    }
}
