using System;
using System.Collections.Generic;

namespace ERPaperless.Models
{
    public class SaveOutcomeFormInputViewModel
    {
        public string PatientId { get; set; }
        public int? AdmissionCode { get; set; }
        public string FormType { get; set; }
        public bool IsFinal { get; set; }
        public bool CloseExistingDischargeOnDeath { get; set; }
        public bool ReplaceExistingOutcome { get; set; }

        // Referral
        public int? ReferralReasonCode { get; set; }
        public DateTime? ReferralDateTime { get; set; }
        public int? ReferralMoOnDutyCode { get; set; }
        public string ReferralPatientComplaint { get; set; }
        public string ReferralClinicalSummary { get; set; }
        public string ReferralTreatment { get; set; }
        public string ReferralPertinentInvestigation { get; set; }
        public string ReferralClinicalDiagnosis { get; set; }

        // Discharge
        public string DischargeFinalDiagnosis { get; set; }
        public string DischargeBriefHistory { get; set; }
        public string DischargeSurgeryProcedures { get; set; }
        public string DischargeDietInstructions { get; set; }
        public string DischargeClinicalAssessment { get; set; }
        public string DischargeFollowUpInstructions { get; set; }
        public List<SaveDischargeMedicineItemInputViewModel> DischargeMedicines { get; set; }

        // Death
        public int? DeathPrimaryConsultantCode { get; set; }
        public DateTime? DeathDateTime { get; set; }
        public string DeathPrimaryCause { get; set; }
        public string DeathSecondaryCause { get; set; }
        public bool DeathBroughtInDead { get; set; }
        public string DeathMedicalCondition { get; set; }
        public string DeathHandOverTo { get; set; }
        public string DeathRelativeCnicPassport { get; set; }
        public int? DeathRelationCode { get; set; }
        public DateTime? DeathHandOverAt { get; set; }
        public int? DeathDutyNurseCode { get; set; }
        public int? DeathDutyMoCode { get; set; }

        // LAMA
        public string LamaRequesterName { get; set; }
        public int? LamaDutyNurseCode { get; set; }
        public int? LamaDutyMoCode { get; set; }
        public int? LamaRelationCode { get; set; }
        public DateTime? LamaRequestedOn { get; set; }
        public string LamaReason { get; set; }
        public string LamaDoctorRemarks { get; set; }
        public string LamaNurseComments { get; set; }
    }

    public class SaveDischargeMedicineItemInputViewModel
    {
        public long? Id { get; set; }
        public int? ItemCode { get; set; }
        public string DrugName { get; set; }
        public string Dose { get; set; }
        public string Frequency { get; set; }
        public int? Days { get; set; }
        public string Instruction { get; set; }
    }
}
