using DevExpress.Xpo.DB;
using ERPaperless.Models;
using Microsoft.Reporting.Map.WebForms.BingMaps;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity.Validation;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Security;

namespace ERPaperless.Services
{
    public static class EROutcomeFormRepository
    {
        public static string LastError { get; private set; }

        public static OutcomeFormStateViewModel GetOutcomeFormState(string patientId, int? admissionCode, int companyCode)
        {
            var state = new OutcomeFormStateViewModel();
            var target = ResolveTarget(patientId, admissionCode, companyCode);
            if (!target.IsValid)
                return state;

            try
            {
                using (var db = dbAMCEntities.Create())
                {
                    var referral = db.tblPatientReferrals
                        .Where(x => x.intERAdmissionCode == target.ERAdmissionCode
                                    && x.intCompanyCode == companyCode
                                    && x.intRecordStatusCode != 8)
                        .OrderByDescending(x => x.intPatientReferralCode)
                        .FirstOrDefault();
                    if (referral != null)
                    {
                        state.Referral = new ReferralFormStateViewModel
                        {
                            IsFinal = referral.bolIsFinal,
                            ReferralReasonCode = referral.intReferralReasonCode,
                            ReferralDateTime = referral.dtmPatientReferral,
                            MoOnDutyCode = referral.intMOOnDutyCode,
                            PatientComplaint = referral.strPresentingComplaint,
                            ClinicalSummary = referral.strClinicalSummary,
                            Treatment = referral.strTreatment,
                            PertinentInvestigation = referral.strPertinentInvestigation,
                            ClinicalDiagnosis = referral.strClinicalDiagnosis
                        };
                    }

                    var discharge = db.tblDischargeSummaries
                        .Where(x => x.intERAdmissionCode == target.ERAdmissionCode
                                    && x.intCompanyCode == companyCode
                                    && x.intRecordStatusCode != 8)
                        .OrderByDescending(x => x.intDischargeSummaryCode)
                        .FirstOrDefault();
                    if (discharge != null)
                    {
                        state.Discharge = new DischargeFormStateViewModel
                        {
                            IsFinal = discharge.bolIsFinal,
                            FinalDiagnosis = discharge.strFinalDiagnosis,
                            BriefHistory = discharge.strBriefHistory,
                            SurgeryProcedures = discharge.strProcedure,
                            DietInstructions = discharge.strDietInstructions,
                            ClinicalAssessment = discharge.strClinicalAssessment,
                            FollowUpInstructions = discharge.strFollowUpInstructions
                        };
                    }

                    var medicines = db.tblERDischargeMedicines
                        .Where(x => x.intERAdmissionCode == target.ERAdmissionCode
                                    && x.intCompanyCode == companyCode
                                    && x.intRecordStatusCode != 8)
                        .OrderByDescending(x => x.intERDischargeMedicineCode)
                        .ToList();
                    foreach (var med in medicines)
                    {
                        var parsed = ParseInstruction(med.strInstruction);
                        state.Discharge.Medicines.Add(new DischargeMedicineStateViewModel
                        {
                            ItemCode = med.intItemCode.HasValue ? (int?)Convert.ToInt32(med.intItemCode.Value) : null,
                            DrugName = med.strDrugName,
                            Dose = med.strDosage,
                            Frequency = parsed.frequency,
                            Days = med.intDays,
                            Instruction = parsed.instruction
                        });
                    }

                    var death = db.tblDeathCertificates
                        .Where(x => x.intERAdmissionCode == target.ERAdmissionCode
                                    && x.intCompanyCode == companyCode
                                    && x.intRecordStatusCode != 8)
                        .OrderByDescending(x => x.intDeathCertificateCode)
                        .FirstOrDefault();
                    if (death != null)
                    {
                        state.Death = new DeathFormStateViewModel
                        {
                            IsFinal = death.bolIsFinal,
                            PrimaryConsultantCode = (int)death.intAttendingConsultant,
                            DeathDateTime = death.dtmDeath,
                            PrimaryCause = death.strPrimaryCause,
                            SecondaryCause = death.strSecondaryCause,
                            IsBroughtInDead = death.bolIsBroughtDead,
                            MedicalCondition = death.strMedicalCondition,
                            HandOverTo = death.strHandOverTo,
                            RelativeCnicPassport = death.strRelativeCNIC,
                            RelationCode = death.intRelationCode,
                            HandOverAt = death.dtmHandedOver,
                            DutyNurseCode = death.intNurseOnDutyCode,
                            DutyMoCode = death.intMOOnDutyCode
                        };
                    }

                    var lama = db.tblLAMAs
                        .Where(x => x.intERAdmissionCode == target.ERAdmissionCode
                                    && x.intCompanyCode == companyCode
                                    && x.intRecordStatusCode != 8)
                        .OrderByDescending(x => x.intLAMACode)
                        .FirstOrDefault();
                    if (lama != null)
                    {
                        state.Lama = new LamaFormStateViewModel
                        {
                            IsFinal = lama.bolIsFinal,
                            RequesterName = lama.strRequesterName,
                            DutyNurseCode = lama.intNurseUserCode,
                            DutyMoCode = lama.intMOUserCode,
                            RelationCode = ResolveRelationCodeByName(lama.strRelation, companyCode),
                            RequestedOn = lama.dtmRequest,
                            Reason = lama.strReason,
                            DoctorRemarks = lama.strDoctorRemarks,
                            NurseComments = lama.strNurseRemarks
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(EROutcomeFormRepository), nameof(GetOutcomeFormState), ex);
            }

            return state;
        }

        public static bool SaveOutcomeForm(SaveOutcomeFormInputViewModel input, int companyCode, int userCode)
        {
            LastError = null;

            if (input == null || string.IsNullOrWhiteSpace(input.PatientId))
            {
                LastError = "Patient record is missing.";
                return false;
            }

            var formType = Normalize(input.FormType);
            if (string.IsNullOrWhiteSpace(formType))
            {
                LastError = "Form type is required.";
                return false;
            }

            var target = ResolveTarget(input.PatientId, input.AdmissionCode, companyCode);
            if (!target.IsValid)
            {
                LastError = "Could not resolve admission reference.";
                return false;
            }

            try
            {
                using (var db = dbAMCEntities.Create())
                using (var tx = db.Database.BeginTransaction())
                {
                    var now = DateTime.Now;

                    EnsureExclusiveOutcome(db, target, formType, input, companyCode, userCode, now);

                    switch (formType)
                    {
                        case "REFERRAL":
                            SaveReferral(db, target, input, companyCode, userCode, now);
                            break;
                        case "DISCHARGE":
                            SaveDischarge(db, target, input, companyCode, userCode, now);
                            break;
                        case "DEATH":
                            SaveDeath(db, target, input, companyCode, userCode, now);
                            break;
                        case "LAMA":
                            SaveLama(db, target, input, companyCode, userCode, now);
                            break;
                        default:
                            LastError = "Unsupported form type.";
                            return false;
                    }

                    SyncReviewFormOutcomeCode(db, target, formType, userCode, now);
                    db.SaveChanges();
                    if (input.IsFinal)
                    {
                        UpdateErAdmissionOutcomeFlags(db, target, formType, input.IsFinal, companyCode, userCode);
                    }
                    tx.Commit();
                    return true;
                }
            }
            catch (DbEntityValidationException vex)
            {
                var validationErrors = vex.EntityValidationErrors
                    .SelectMany(e => e.ValidationErrors)
                    .Select(e => e.PropertyName + ": " + e.ErrorMessage)
                    .ToList();
                LastError = validationErrors.Count > 0
                    ? string.Join(" | ", validationErrors)
                    : vex.GetBaseException().Message;
                ErrorLogging.Log(nameof(EROutcomeFormRepository), nameof(SaveOutcomeForm), vex);
                return false;
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(EROutcomeFormRepository), nameof(SaveOutcomeForm), ex);
                LastError = ex.GetBaseException().Message;
                return false;
            }
        }

        private static void SaveReferral(
            dbAMCEntities db,
            OutcomeTarget target,
            SaveOutcomeFormInputViewModel input,
            int companyCode,
            int userCode,
            DateTime now)
        {
            var entity = db.tblPatientReferrals
                .Where(x => x.intERAdmissionCode == target.ERAdmissionCode
                            && x.intCompanyCode == companyCode
                            && x.intRecordStatusCode != 8)
                .OrderByDescending(x => x.intPatientReferralCode)
                .FirstOrDefault();

            if (entity != null && entity.bolIsFinal)
                throw new InvalidOperationException("Referral form is finalized and cannot be updated.");

            if (entity == null)
            {
                entity = new tblPatientReferral
                {
                    intPatientReferralCode = GetNextPatientReferralCode(db, companyCode),
                    intPatientCode = target.PatientCode,
                    intERAdmissionCode = target.ERAdmissionCode,
                    dtmCreated = now,
                    intOwnerCode = userCode,
                    intCreatedByCode = userCode,
                    intRecordStatusCode = 1,
                    intBranchCode = target.BranchCode,
                    intCompanyCode = companyCode
                };
                db.tblPatientReferrals.Add(entity);
            }
            if (entity != null)
            {
                entity.dtmLastM = now;
                entity.intAlteredByCode = userCode;
                entity.intRecordStatusCode = 1;
            }
            entity.dtmPatientReferral = input.ReferralDateTime ?? now;
            entity.strPresentingComplaint = TextOrEmpty(input.ReferralPatientComplaint);
            entity.strClinicalSummary = TextOrEmpty(input.ReferralClinicalSummary);
            entity.strTreatment = TextOrEmpty(input.ReferralTreatment);
            entity.strPertinentInvestigation = TextOrEmpty(input.ReferralPertinentInvestigation);
            entity.strClinicalDiagnosis = TextOrEmpty(input.ReferralClinicalDiagnosis);
            entity.intReferralReasonCode = input.ReferralReasonCode;
            entity.intMOOnDutyCode = input.ReferralMoOnDutyCode;
            entity.bolIsFinal = input.IsFinal;
        
        }

        private static void SaveDischarge(
            dbAMCEntities db,
            OutcomeTarget target,
            SaveOutcomeFormInputViewModel input,
            int companyCode,
            int userCode,
            DateTime now)
        {
            var entity = db.tblDischargeSummaries
                .Where(x => x.intERAdmissionCode == target.ERAdmissionCode
                            && x.intCompanyCode == companyCode
                            && x.intRecordStatusCode != 8)
                .OrderByDescending(x => x.intDischargeSummaryCode)
                .FirstOrDefault();

            if (entity != null && entity.bolIsFinal)
                throw new InvalidOperationException("Discharge form is finalized and cannot be updated.");

            if (entity == null)
            {
                entity = new tblDischargeSummary
                {
                    intDischargeSummaryCode = GetNextDischargeSummaryCode(db, companyCode),
                    intPatientCode = target.PatientCode,
                    intERAdmissionCode = target.ERAdmissionCode,
                    dtmCreated = now,
                    intOwnerCode = userCode,
                    intCreatedByCode = userCode,
                    intRecordStatusCode = 1,
                    intBranchCode = target.BranchCode,
                    intCompanyCode = companyCode
                };
                db.tblDischargeSummaries.Add(entity);
            }
            if (entity != null)
            {
                entity.dtmLastM = now;
                entity.intAlteredByCode = userCode;
                entity.intRecordStatusCode = 1;
            }
            entity.dtmDischarge = now;
            entity.strFinalDiagnosis = TextOrEmpty(input.DischargeFinalDiagnosis);
            entity.strBriefHistory = TextOrEmpty(input.DischargeBriefHistory);
            entity.strProcedure = TextOrEmpty(input.DischargeSurgeryProcedures);
            entity.strDietInstructions = TextOrEmpty(input.DischargeDietInstructions);
            entity.strClinicalAssessment = TextOrEmpty(input.DischargeClinicalAssessment);
            entity.strFollowUpInstructions = TextOrEmpty(input.DischargeFollowUpInstructions);
            entity.strDischargeMedication = BuildDischargeMedicationSummary(input.DischargeMedicines);
            entity.bolIsFinal = input.IsFinal;
          

            var existingMeds = db.tblERDischargeMedicines
                .Where(x => x.intERAdmissionCode == target.ERAdmissionCode
                            && x.intCompanyCode == companyCode
                            && x.intRecordStatusCode == 1)
                .ToList();

            foreach (var old in existingMeds)
            {
                old.intRecordStatusCode = 8;
                old.dtmLastM = now;
                old.intAlteredByCode = userCode;
            }

            var nextMedicineCode = GetNextErDischargeMedicineCode(db, companyCode);
            foreach (var med in (input.DischargeMedicines ?? new List<SaveDischargeMedicineItemInputViewModel>()))
            {
                var hasData = !string.IsNullOrWhiteSpace(med.DrugName)
                              || !string.IsNullOrWhiteSpace(med.Dose)
                              || !string.IsNullOrWhiteSpace(med.Frequency)
                              || med.Days.HasValue
                              || !string.IsNullOrWhiteSpace(med.Instruction);
                if (!hasData)
                    continue;

                if (string.IsNullOrWhiteSpace(med.DrugName))
                    throw new InvalidOperationException("Medicine name is required in discharge medicine.");
                if (string.IsNullOrWhiteSpace(med.Dose))
                    throw new InvalidOperationException("Dose is required in discharge medicine.");
                if (string.IsNullOrWhiteSpace(med.Frequency))
                    throw new InvalidOperationException("Frequency is required in discharge medicine.");
                if (!med.Days.HasValue || med.Days.Value <= 0)
                    throw new InvalidOperationException("Days is required in discharge medicine.");

                db.tblERDischargeMedicines.Add(new tblERDischargeMedicine
                {
                    intERDischargeMedicineCode = nextMedicineCode++,
                    intPatientCode = target.PatientCode,
                    intERAdmissionCode = target.ERAdmissionCode,
                    intItemCode = med.ItemCode,
                    strDrugName = NullIfWhiteSpace(med.DrugName),
                    strDosage = NullIfWhiteSpace(med.Dose),
                    intDrugFrequencyCode = null,
                    intDays = med.Days,
                    strInstruction = BuildInstruction(med.Frequency, med.Instruction),
                    dtmCreated = now,
                    intOwnerCode = userCode,
                    intCreatedByCode = userCode,
                    intRecordStatusCode = 1,
                    intBranchCode = target.BranchCode,
                    intCompanyCode = companyCode
                });
            }
        }

        private static void EnsureExclusiveOutcome(
            dbAMCEntities db,
            OutcomeTarget target,
            string formType,
            SaveOutcomeFormInputViewModel input,
            int companyCode,
            int userCode,
            DateTime now)
        {
            var current = Normalize(formType);
            var currentLabel = GetOutcomeFormLabel(current);
            var conflicts = new List<string>();

            var hasReferral = current != "REFERRAL" && db.tblPatientReferrals.Any(x =>
                x.intERAdmissionCode == target.ERAdmissionCode
                && x.intCompanyCode == companyCode
                && x.intRecordStatusCode != 8);
            if (hasReferral) conflicts.Add("Patient Referral");

            var hasDischarge = current != "DISCHARGE" && db.tblDischargeSummaries.Any(x =>
                x.intERAdmissionCode == target.ERAdmissionCode
                && x.intCompanyCode == companyCode
                && x.intRecordStatusCode != 8);
            if (hasDischarge) conflicts.Add("Discharge Summary");

            var hasDeath = current != "DEATH" && db.tblDeathCertificates.Any(x =>
                x.intERAdmissionCode == target.ERAdmissionCode
                && x.intCompanyCode == companyCode
                && x.intRecordStatusCode != 8);
            if (hasDeath) conflicts.Add("Death Certificate");

            var hasLama = current != "LAMA" && db.tblLAMAs.Any(x =>
                x.intERAdmissionCode == target.ERAdmissionCode
                && x.intCompanyCode == companyCode
                && x.intRecordStatusCode != 8);
            if (hasLama) conflicts.Add("LAMA");

            if (conflicts.Count == 0)
                return;

            var allowReplace = input != null && (input.ReplaceExistingOutcome || input.CloseExistingDischargeOnDeath);
            if (!allowReplace)
            {
                var existingText = string.Join(", ", conflicts);
                throw new InvalidOperationException(
                    "CONFIRM_REPLACE_OUTCOME|" + existingText +
                    " already exists for this admission. Only one outcome form is allowed at a time. " +
                    "Do you want to permanently delete " + existingText + " and continue with " + currentLabel + "?");
            }

            DeleteOtherOutcomeForms(db, target, current, companyCode);
            ClearErAdmissionOutcomeFlags(db, target.ERAdmissionCode, companyCode, userCode);
        }

        private static void DeleteOtherOutcomeForms(
            dbAMCEntities db,
            OutcomeTarget target,
            string currentFormType,
            int companyCode)
        {
            if (currentFormType != "REFERRAL")
            {
                var referrals = db.tblPatientReferrals
                    .Where(x => x.intERAdmissionCode == target.ERAdmissionCode
                                && x.intCompanyCode == companyCode)
                    .ToList();
                if (referrals.Count > 0)
                    db.tblPatientReferrals.RemoveRange(referrals);
            }

            if (currentFormType != "DISCHARGE")
            {
                var medicines = db.tblERDischargeMedicines
                    .Where(x => x.intERAdmissionCode == target.ERAdmissionCode
                                && x.intCompanyCode == companyCode)
                    .ToList();
                if (medicines.Count > 0)
                    db.tblERDischargeMedicines.RemoveRange(medicines);

                var discharges = db.tblDischargeSummaries
                    .Where(x => x.intERAdmissionCode == target.ERAdmissionCode
                                && x.intCompanyCode == companyCode)
                    .ToList();
                if (discharges.Count > 0)
                    db.tblDischargeSummaries.RemoveRange(discharges);
            }

            if (currentFormType != "DEATH")
            {
                var deaths = db.tblDeathCertificates
                    .Where(x => x.intERAdmissionCode == target.ERAdmissionCode
                                && x.intCompanyCode == companyCode)
                    .ToList();
                if (deaths.Count > 0)
                    db.tblDeathCertificates.RemoveRange(deaths);
            }

            if (currentFormType != "LAMA")
            {
                var lamas = db.tblLAMAs
                    .Where(x => x.intERAdmissionCode == target.ERAdmissionCode
                                && x.intCompanyCode == companyCode)
                    .ToList();
                if (lamas.Count > 0)
                    db.tblLAMAs.RemoveRange(lamas);
            }
        }

        private static void ClearErAdmissionOutcomeFlags(
            dbAMCEntities db,
            long admissionCode,
            int companyCode,
            int userCode)
        {
            // Columns are non-nullable bits; clear by setting false (never NULL).
            db.Database.ExecuteSqlCommand(
                "EXEC procUpdateERAdmissionForERPortal @bolIsDeathCertificate, @bolIsPatientReferral, @bolIsDichargeSummary, @bolIsLAMA, @intERAdmissionCode, @intUserCode, @intCompanyCode",
                new SqlParameter("@bolIsDeathCertificate", SqlDbType.Bit) { Value = false },
                new SqlParameter("@bolIsPatientReferral", SqlDbType.Bit) { Value = false },
                new SqlParameter("@bolIsDichargeSummary", SqlDbType.Bit) { Value = false },
                new SqlParameter("@bolIsLAMA", SqlDbType.Bit) { Value = false },
                new SqlParameter("@intERAdmissionCode", SqlDbType.BigInt) { Value = admissionCode },
                new SqlParameter("@intUserCode", SqlDbType.Int) { Value = userCode },
                new SqlParameter("@intCompanyCode", SqlDbType.Int) { Value = companyCode });
        }

        private static string GetOutcomeFormLabel(string formType)
        {
            switch (Normalize(formType))
            {
                case "REFERRAL": return "Patient Referral";
                case "DISCHARGE": return "Discharge Summary";
                case "DEATH": return "Death Certificate";
                case "LAMA": return "LAMA";
                default: return "Outcome Form";
            }
        }

        private static void SaveDeath(
            dbAMCEntities db,
            OutcomeTarget target,
            SaveOutcomeFormInputViewModel input,
            int companyCode,
            int userCode,
            DateTime now)
        {
            var entity = db.tblDeathCertificates
                .Where(x => x.intERAdmissionCode == target.ERAdmissionCode
                            && x.intCompanyCode == companyCode
                            && x.intRecordStatusCode != 8)
                .OrderByDescending(x => x.intDeathCertificateCode)
                .FirstOrDefault();

            if (entity != null && entity.bolIsFinal)
                throw new InvalidOperationException("Death form is finalized and cannot be updated.");

            if (entity == null)
            {
                entity = new tblDeathCertificate
                {
                    intDeathCertificateCode = GetNextDeathCertificateCode(db, companyCode),
                    intPatientCode = target.PatientCode,
                    intERAdmissionCode = target.ERAdmissionCode,
                    dtmCreated = now,
                    intOwnerCode = userCode,
                    intCreatedByCode = userCode,
                    intRecordStatusCode = 1,
                    intBranchCode = target.BranchCode,
                    intCompanyCode = companyCode
                };
                db.tblDeathCertificates.Add(entity);
            }
            if (entity != null)
            {
                entity.dtmLastM = now;
                entity.intAlteredByCode = userCode;
                entity.intRecordStatusCode = 1;
            }
            entity.intAttendingConsultant = input.DeathPrimaryConsultantCode ?? 0;
            entity.dtmDeath = input.DeathDateTime ?? now;
            entity.strPrimaryCause = TextOrEmpty(input.DeathPrimaryCause);
            entity.strSecondaryCause = TextOrEmpty(input.DeathSecondaryCause);
            entity.bolIsBroughtDead = input.DeathBroughtInDead;
            entity.strMedicalCondition = TextOrEmpty(input.DeathMedicalCondition);
            entity.strHandOverTo = TextOrEmpty(input.DeathHandOverTo);
            entity.strRelativeCNIC = TextOrEmpty(input.DeathRelativeCnicPassport);
            entity.intRelationCode = input.DeathRelationCode ?? 0;
            entity.dtmHandedOver = input.DeathHandOverAt;
            entity.intNurseOnDutyCode = input.DeathDutyNurseCode;
            entity.intMOOnDutyCode = input.DeathDutyMoCode;
            entity.bolIsFinal = input.IsFinal;
          
        }

        private static void SaveLama(
            dbAMCEntities db,
            OutcomeTarget target,
            SaveOutcomeFormInputViewModel input,
            int companyCode,
            int userCode,
            DateTime now)
        {
            var entity = db.tblLAMAs
                .Where(x => x.intERAdmissionCode == target.ERAdmissionCode
                            && x.intCompanyCode == companyCode
                            && x.intRecordStatusCode != 8)
                .OrderByDescending(x => x.intLAMACode)
                .FirstOrDefault();

            if (entity != null && entity.bolIsFinal)
                throw new InvalidOperationException("LAMA form is finalized and cannot be updated.");

            if (entity == null)
            {
                entity = new tblLAMA
                {
                    intLAMACode = GetNextLamaCode(db, companyCode),
                    intPatientCode = target.PatientCode,
                    intIPDAdmissionCode = null,
                    intERAdmissionCode = target.ERAdmissionCode,
                    dtmLAMA = now,
                    dtmCreated = now,
                    dtmRequest = input.LamaRequestedOn ?? now,
                    intOwnerCode = userCode,
                    intCreatedByCode = userCode,
                    intRecordStatusCode = 1,
                    intBranchCode = target.BranchCode,
                    intCompanyCode = companyCode
                };
                db.tblLAMAs.Add(entity);
            }
            if (entity != null) {
                entity.dtmLastM = now;
                entity.intAlteredByCode = userCode;
                entity.intRecordStatusCode = 1;
            }
            entity.strRequesterName = TextOrEmpty(input.LamaRequesterName);
            entity.dtmRequest = input.LamaRequestedOn ?? now;
            entity.strRelation = TextOrEmpty(ResolveRelationName(input.LamaRelationCode, companyCode) ?? input.LamaRelationCode?.ToString());
            entity.strReason = TextOrEmpty(input.LamaReason);
            entity.strDoctorRemarks = TextOrEmpty(input.LamaDoctorRemarks);
            entity.intMOUserCode = input.LamaDutyMoCode;
            entity.strNurseRemarks = TextOrEmpty(input.LamaNurseComments);
            entity.intNurseUserCode = input.LamaDutyNurseCode;
            entity.bolIsFinal = input.IsFinal;
          
        }

        private static long GetNextLamaCode(dbAMCEntities db, int companyCode)
        {
            var maxCode = db.tblLAMAs
                .Where(x => x.intCompanyCode == companyCode)
                .Select(x => (long?)x.intLAMACode)
                .Max();

            return (maxCode ?? 0L) + 1L;
        }

        private static long GetNextPatientReferralCode(dbAMCEntities db, int companyCode)
        {
            var maxCode = db.tblPatientReferrals
                .Where(x => x.intCompanyCode == companyCode)
                .Select(x => (long?)x.intPatientReferralCode)
                .Max();

            return (maxCode ?? 0L) + 1L;
        }

        private static long GetNextDischargeSummaryCode(dbAMCEntities db, int companyCode)
        {
            var maxCode = db.tblDischargeSummaries
                .Where(x => x.intCompanyCode == companyCode)
                .Select(x => (long?)x.intDischargeSummaryCode)
                .Max();

            return (maxCode ?? 0L) + 1L;
        }

        private static long GetNextDeathCertificateCode(dbAMCEntities db, int companyCode)
        {
            var maxCode = db.tblDeathCertificates
                .Where(x => x.intCompanyCode == companyCode)
                .Select(x => (long?)x.intDeathCertificateCode)
                .Max();

            return (maxCode ?? 0L) + 1L;
        }

        private static long GetNextErDischargeMedicineCode(dbAMCEntities db, int companyCode)
        {
            var maxCode = db.tblERDischargeMedicines
                .Where(x => x.intCompanyCode == companyCode)
                .Select(x => (long?)x.intERDischargeMedicineCode)
                .Max();

            return (maxCode ?? 0L) + 1L;
        }

        private static OutcomeTarget ResolveTarget(string patientId, int? admissionCode, int companyCode)
        {
            long erPatientCode = 0;
            tblERPatient erPatient = null;
            if (long.TryParse((patientId ?? string.Empty).Trim(), out erPatientCode) && erPatientCode > 0)
            {
                erPatient = ERPatientRepository.GetByCode(erPatientCode, companyCode);
            }

            var resolvedAdmissionCode = admissionCode.HasValue && admissionCode.Value > 0
                ? admissionCode.Value
                : (erPatient != null && erPatient.intERAdmissionCode.HasValue && erPatient.intERAdmissionCode.Value > 0
                    ? (int)erPatient.intERAdmissionCode.Value
                    : 0);
            if (resolvedAdmissionCode <= 0)
                return OutcomeTarget.Invalid;

            var admissionInfo = ResolveAdmissionInfo(resolvedAdmissionCode, companyCode);
            var patientCode = admissionInfo.PatientCode > 0
                ? admissionInfo.PatientCode
                : (erPatient != null ? erPatient.intERPatientCode : 0);
            var branchCode = admissionInfo.BranchCode > 0
                ? admissionInfo.BranchCode
                : (erPatient != null ? erPatient.intBranchCode : 0);
            if (patientCode <= 0 || branchCode <= 0)
                return OutcomeTarget.Invalid;

            return new OutcomeTarget
            {
                ERPatientCode = erPatient != null ? erPatient.intERPatientCode : erPatientCode,
                ERAdmissionCode = resolvedAdmissionCode,
                PatientCode = patientCode,
                BranchCode = branchCode,
                CompanyCode = companyCode
            };
        }

        private static (long PatientCode, int BranchCode) ResolveAdmissionInfo(long erAdmissionCode, int companyCode)
        {
            try
            {
                using (var conn = DBHelper.GetConnection())
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(@"
SELECT TOP 1 intPatientCode, intBranchCode
FROM tblERAdmission
WHERE intERAdmissionCode = @admissionCode
  AND intCompanyCode = @companyCode", conn))
                    {
                        cmd.Parameters.Add("@admissionCode", SqlDbType.BigInt).Value = erAdmissionCode;
                        cmd.Parameters.Add("@companyCode", SqlDbType.Int).Value = companyCode;
                        using (var rdr = cmd.ExecuteReader())
                        {
                            if (!rdr.Read())
                                return (0L, 0);

                            var patientCode = rdr["intPatientCode"] == DBNull.Value
                                ? 0L
                                : Convert.ToInt64(rdr["intPatientCode"]);
                            var branchCode = rdr["intBranchCode"] == DBNull.Value
                                ? 0
                                : Convert.ToInt32(rdr["intBranchCode"]);
                            return (patientCode, branchCode);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(EROutcomeFormRepository), nameof(ResolveAdmissionInfo), ex);
                return (0L, 0);
            }
        }

        private static string ResolveRelationName(int? relationCode, int companyCode)
        {
            if (!relationCode.HasValue || relationCode.Value <= 0)
                return null;

            try
            {
                using (var conn = DBHelper.GetConnection())
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(@"
SELECT TOP 1 strRelationName
FROM tblRelation
WHERE intRelationCode = @relationCode
  AND intCompanyCode = @companyCode", conn))
                    {
                        cmd.Parameters.Add("@relationCode", SqlDbType.Int).Value = relationCode.Value;
                        cmd.Parameters.Add("@companyCode", SqlDbType.Int).Value = companyCode;
                        return cmd.ExecuteScalar()?.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(EROutcomeFormRepository), nameof(ResolveRelationName), ex);
                return null;
            }
        }

        private static int? ResolveRelationCodeByName(string relationName, int companyCode)
        {
            if (string.IsNullOrWhiteSpace(relationName))
                return null;

            try
            {
                using (var conn = DBHelper.GetConnection())
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(@"
SELECT TOP 1 intRelationCode
FROM tblRelation
WHERE intCompanyCode = @companyCode
  AND UPPER(LTRIM(RTRIM(strRelationName))) = UPPER(LTRIM(RTRIM(@relationName)))", conn))
                    {
                        cmd.Parameters.Add("@companyCode", SqlDbType.Int).Value = companyCode;
                        cmd.Parameters.Add("@relationName", SqlDbType.NVarChar, 100).Value = relationName.Trim();
                        var result = cmd.ExecuteScalar();
                        if (result == null || result == DBNull.Value) return null;
                        return Convert.ToInt32(result);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(EROutcomeFormRepository), nameof(ResolveRelationCodeByName), ex);
                return null;
            }
        }

        private static string BuildDischargeMedicationSummary(IEnumerable<SaveDischargeMedicineItemInputViewModel> medicines)
        {
            var lines = new List<string>();
            foreach (var med in medicines ?? Enumerable.Empty<SaveDischargeMedicineItemInputViewModel>())
            {
                if (string.IsNullOrWhiteSpace(med.DrugName))
                    continue;

                var dose = string.IsNullOrWhiteSpace(med.Dose) ? string.Empty : " Dose: " + med.Dose.Trim();
                var freq = string.IsNullOrWhiteSpace(med.Frequency) ? string.Empty : " Freq: " + med.Frequency.Trim();
                var days = med.Days.HasValue ? " Days: " + med.Days.Value : string.Empty;
                lines.Add((med.DrugName ?? string.Empty).Trim() + dose + freq + days);
            }
            return lines.Count == 0 ? string.Empty : string.Join(" | ", lines);
        }

        private static string BuildInstruction(string frequency, string instruction)
        {
            var f = NullIfWhiteSpace(frequency);
            var i = NullIfWhiteSpace(instruction);
            if (string.IsNullOrWhiteSpace(f))
                return i;
            if (string.IsNullOrWhiteSpace(i))
                return "Frequency: " + f;
            return "Frequency: " + f + " | " + i;
        }

        private static (string frequency, string instruction) ParseInstruction(string value)
        {
            var raw = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw))
                return (string.Empty, string.Empty);

            const string prefix = "Frequency:";
            if (!raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return (string.Empty, raw);

            var after = raw.Substring(prefix.Length).Trim();
            var split = after.IndexOf('|');
            if (split < 0)
                return (after, string.Empty);

            var freq = after.Substring(0, split).Trim();
            var ins = after.Substring(split + 1).Trim();
            return (freq, ins);
        }

        private static string NullIfWhiteSpace(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string TextOrEmpty(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static void SyncReviewFormOutcomeCode(
            dbAMCEntities db,
            OutcomeTarget target,
            string formType,
            int userCode,
            DateTime now)
        {
            var outcomeCode = ResolveOutcomeLovCodeForFormType(formType);
            if (!outcomeCode.HasValue || outcomeCode.Value <= 0)
                return;

            var header = db.tblERPatientReviewForms.FirstOrDefault(f =>
                f.intERPatientCode == target.ERPatientCode
                && f.intBranchCode == target.BranchCode
                && f.intCompanyCode == target.CompanyCode
                && f.intRecordStatusCode == 1);

            if (header == null)
            {
                header = new tblERPatientReviewForm
                {
                    intERPatientCode = target.ERPatientCode,
                    intBranchCode = target.BranchCode,
                    intCompanyCode = target.CompanyCode,
                    dtmSaved = now,
                    dtmCreated = now,
                    intOwnerCode = userCode,
                    intCreatedByCode = userCode,
                    intRecordStatusCode = 1
                };
                db.tblERPatientReviewForms.Add(header);
            }

            header.intOutcomeCode = outcomeCode.Value;
            header.dtmSaved = now;
            header.dtmLastM = now;
            header.intAlteredByCode = userCode;
        }

        private static int? ResolveOutcomeLovCodeForFormType(string formType)
        {
            var normalized = Normalize(formType);
            if (string.IsNullOrWhiteSpace(normalized))
                return null;

            var outcomes = ERLovRepository.GetByType(ERLovType.Outcome)
                .Where(x => x != null && x.LovCode > 0 && !string.IsNullOrWhiteSpace(x.Description))
                .ToList();
            if (!outcomes.Any())
                return null;

            Func<string, bool> isMatch = description =>
            {
                var d = (description ?? string.Empty).Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(d)) return false;
                if (normalized == "DISCHARGE")
                    return d.Contains("DISCHARGE");
                if (normalized == "LAMA")
                    return d.Contains("LAMA");
                if (normalized == "REFERRAL")
                    return d.Contains("REFERRAL") || d.Contains("REFER");
                if (normalized == "DEATH")
                    return d.Contains("DEATH") || d.Contains("EXPIRE");
                return false;
            };

            var matched = outcomes.FirstOrDefault(x => isMatch(x.Description));
            return matched?.LovCode;
        }

        private static void UpdateErAdmissionOutcomeFlags(
            dbAMCEntities db,
            OutcomeTarget target,
            string formType,
            bool isFinal,
            int companyCode,
            int userCode)
        {
            var admissionCode = target.ERAdmissionCode;
            var normalized = Normalize(formType);

            // These admission columns are non-nullable; set the active form flag and force others to false.
            var deathValue = normalized == "DEATH" && isFinal;
            var referralValue = normalized == "REFERRAL" && isFinal;
            var dischargeValue = normalized == "DISCHARGE" && isFinal;
            var lamaValue = normalized == "LAMA" && isFinal;

            db.Database.ExecuteSqlCommand(
                "EXEC procUpdateERAdmissionForERPortal @bolIsDeathCertificate, @bolIsPatientReferral, @bolIsDichargeSummary, @bolIsLAMA, @intERAdmissionCode, @intUserCode, @intCompanyCode",
                new SqlParameter("@bolIsDeathCertificate", SqlDbType.Bit) { Value = deathValue },
                new SqlParameter("@bolIsPatientReferral", SqlDbType.Bit) { Value = referralValue },
                new SqlParameter("@bolIsDichargeSummary", SqlDbType.Bit) { Value = dischargeValue },
                new SqlParameter("@bolIsLAMA", SqlDbType.Bit) { Value = lamaValue },
                new SqlParameter("@intERAdmissionCode", SqlDbType.BigInt) { Value = admissionCode },
                new SqlParameter("@intUserCode", SqlDbType.Int) { Value = userCode },
                new SqlParameter("@intCompanyCode", SqlDbType.Int) { Value = companyCode });
        }

        private struct OutcomeTarget
        {
            public static OutcomeTarget Invalid => new OutcomeTarget();

            public long ERPatientCode { get; set; }
            public long ERAdmissionCode { get; set; }
            public long PatientCode { get; set; }
            public int BranchCode { get; set; }
            public int CompanyCode { get; set; }
            public bool IsValid => ERPatientCode > 0 && ERAdmissionCode > 0 && PatientCode > 0 && BranchCode > 0 && CompanyCode > 0;
        }
    }
}
