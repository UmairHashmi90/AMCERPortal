using ERPaperless.Abstractions.Application;
using ERPaperless.Application.Common;
using ERPaperless.Models;
using ERPaperless.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ERPaperless.Application.Services
{
    public class ErFormService : IErFormService
    {
        public OperationResult AddVital(string patientId, int companyCode, int userCode, VitalRecordViewModel vital)
        {
            var ok = VitalRepository.AddVital(patientId, companyCode, userCode, vital);
            return ok
                ? OperationResult.Success("Vital record added.")
                : OperationResult.Fail("Could not save vital record. " + (VitalRepository.LastError ?? "Please try again."));
        }

        public OperationResult DeleteVital(long vitalId, int companyCode, int userCode)
        {
            var ok = VitalRepository.DeleteVital(vitalId, companyCode, userCode);
            return ok
                ? OperationResult.Success("Vital record deleted.")
                : OperationResult.Fail("Could not delete vital record. " + (VitalRepository.LastError ?? "Please try again."));
        }

        public OperationResult<List<object>> GetPackageDetails(int packageCode)
        {
            var items = ERPackageDetailRepository.GetByPackageCode(packageCode)
                .Select(x => (object)new
                {
                    code = x.ItemCode,
                    name = x.ItemName,
                    qty = x.Quantity,
                    dose = x.Quantity
                })
                .ToList();

            if (!string.IsNullOrWhiteSpace(ERPackageDetailRepository.LastError))
                return OperationResult<List<object>>.Fail(ERPackageDetailRepository.LastError);

            if (!items.Any())
                return OperationResult<List<object>>.Fail("No items found for this package. Check tblERPackageDetail for intERPackageCode = " + packageCode + ".");

            return OperationResult<List<object>>.Success(items);
        }

        public OperationResult<ErReviewFormStateViewModel> SaveReviewForm(SaveErReviewFormInputViewModel model, int companyCode, int userCode, bool isMo, bool isNursing)
        {
            var ok = ERReviewFormRepository.SaveReviewForm(model, companyCode, userCode, isMo, isNursing);
            if (!ok)
                return OperationResult<ErReviewFormStateViewModel>.Fail(ERReviewFormRepository.LastError ?? "Could not save form.");

            var state = ERReviewFormRepository.GetFormState(model.PatientId, companyCode);
            return OperationResult<ErReviewFormStateViewModel>.Success(state, "Review form saved.");
        }

        public OperationResult<ErReviewFormStateViewModel> SavePackageOrders(SavePackageOrdersInputViewModel model, int companyCode, int userCode)
        {
            var ok = ERReviewFormRepository.SavePackageOrders(model, companyCode, userCode);
            if (!ok)
                return OperationResult<ErReviewFormStateViewModel>.Fail(ERReviewFormRepository.LastError ?? "Could not save package orders.");

            var state = ERReviewFormRepository.GetFormState(model.PatientId, companyCode);
            return OperationResult<ErReviewFormStateViewModel>.Success(state);
        }

        public OperationResult SaveOutcomeForm(SaveOutcomeFormInputViewModel model, int companyCode, int userCode)
        {
            var ok = EROutcomeFormRepository.SaveOutcomeForm(model, companyCode, userCode);
            return ok
                ? OperationResult.Success("Outcome form saved.")
                : OperationResult.Fail(EROutcomeFormRepository.LastError ?? "Could not save outcome form.");
        }

        public OperationResult SaveIpdAdmissionOrder(SaveIpdAdmissionOrderInputViewModel model, int companyCode, int userCode)
        {
            var ok = ERIpdAdmissionOrderRepository.Save(model, companyCode, userCode);
            return ok
                ? OperationResult.Success("IPD Admission Order saved.")
                : OperationResult.Fail(ERIpdAdmissionOrderRepository.LastError ?? "Could not save IPD Admission Order.");
        }

        public OutcomeAutoPopulateViewModel GetOutcomeAutoPopulateData(string patientId, long? admissionCode, int companyCode)
        {
            var result = new OutcomeAutoPopulateViewModel
            {
                Diagnosis = string.Empty,
                Treatment = string.Empty,
                ChiefComplaints = string.Empty,
                ConditionUponRelease = string.Empty,
                Medicines = string.Empty,
                Investigations = string.Empty,
                Discharge = new OutcomeAutoPopulateSavedFlags(),
                Referral = new OutcomeAutoPopulateSavedFlags()
            };

            if (string.IsNullOrWhiteSpace(patientId))
                return result;

            var review = ERReviewFormRepository.GetFormState(patientId, companyCode) ?? new ErReviewFormStateViewModel();
            var complaints = ERReviewFormRepository.GetChiefComplaintSelections(
                    patientId,
                    companyCode,
                    GetChiefComplaints())
                .Values
                .Where(x => x != null && x.IsSelected)
                .Select(x =>
                {
                    var name = (x.Name ?? string.Empty).Trim();
                    var remark = (x.Remark ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(remark))
                        return string.Empty;
                    if (string.IsNullOrWhiteSpace(remark))
                        return name;
                    if (string.IsNullOrWhiteSpace(name))
                        return remark;
                    return name + ": " + remark;
                })
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var conditionOptions = GetLovOptions(ERLovType.ConditionUponRelease);
            var conditionName = conditionOptions
                .FirstOrDefault(x => review.ConditionUponReleaseCode.HasValue && x.Id == review.ConditionUponReleaseCode.Value)
                ?.Name ?? string.Empty;

            var medicineParts = (review.MedicineOrders ?? new List<PackageOrderRowViewModel>())
                .Where(x => x != null && !x.IsDeleted && !x.Discontinue && !string.IsNullOrWhiteSpace(x.ItemName))
                .Select(x =>
                {
                    var parts = new List<string> { x.ItemName.Trim() };
                    if (!string.IsNullOrWhiteSpace(x.Dose))
                        parts.Add("Dose: " + x.Dose.Trim());
                    if (!string.IsNullOrWhiteSpace(x.DrugRouteName))
                        parts.Add("Route: " + x.DrugRouteName.Trim());
                    return string.Join(", ", parts);
                })
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var investigationParts = (review.Investigations ?? new List<InvestigationRowViewModel>())
                .Where(x => x != null && !x.IsDeleted && !x.IsCancelled && !string.IsNullOrWhiteSpace(x.TestName))
                .Select(x => x.TestName.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            result.Diagnosis = (review.AssessmentDiagnosis ?? string.Empty).Trim();
            result.Treatment = (review.TreatmentNotes ?? string.Empty).Trim();
            result.ChiefComplaints = string.Join(Environment.NewLine, complaints);
            result.ConditionUponRelease = conditionName.Trim();
            result.Medicines = string.Join(" | ", medicineParts);
            result.Investigations = string.Join(" | ", investigationParts);

            var outcome = EROutcomeFormRepository.GetOutcomeFormState(
                patientId,
                admissionCode.HasValue ? (int?)admissionCode.Value : null,
                companyCode) ?? new OutcomeFormStateViewModel();

            var discharge = outcome.Discharge ?? new DischargeFormStateViewModel();
            result.Discharge = new OutcomeAutoPopulateSavedFlags
            {
                IsFinal = discharge.IsFinal,
                HasDiagnosis = !string.IsNullOrWhiteSpace(discharge.FinalDiagnosis),
                HasBriefHistory = !string.IsNullOrWhiteSpace(discharge.BriefHistory),
                HasTreatment = !string.IsNullOrWhiteSpace(discharge.SurgeryProcedures),
                HasClinicalAssessment = !string.IsNullOrWhiteSpace(discharge.ClinicalAssessment)
            };

            var referral = outcome.Referral ?? new ReferralFormStateViewModel();
            result.Referral = new OutcomeAutoPopulateSavedFlags
            {
                IsFinal = referral.IsFinal,
                HasPatientComplaint = !string.IsNullOrWhiteSpace(referral.PatientComplaint),
                HasDiagnosis = !string.IsNullOrWhiteSpace(referral.ClinicalDiagnosis),
                HasTreatment = !string.IsNullOrWhiteSpace(referral.Treatment),
                HasInvestigations = !string.IsNullOrWhiteSpace(referral.PertinentInvestigation),
                HasCondition = !string.IsNullOrWhiteSpace(referral.ClinicalSummary)
            };

            return result;
        }

        public OperationResult<ErReviewFormStateViewModel> UploadDocument(string patientId, string documentType, string fileName, string contentType, byte[] bytes, int companyCode, int userCode)
        {
            var finalFileName = BuildDocumentFileName(patientId, documentType, fileName, companyCode, userCode);
            var storedPath = "db://" + patientId + "/" + finalFileName;
            var ok = ERReviewFormRepository.SaveDocument(
                patientId,
                companyCode,
                userCode,
                finalFileName,
                documentType,
                contentType,
                bytes,
                storedPath);

            if (!ok)
                return OperationResult<ErReviewFormStateViewModel>.Fail(ERReviewFormRepository.LastError ?? "Could not upload document.");

            var state = ERReviewFormRepository.GetFormState(patientId, companyCode);
            return OperationResult<ErReviewFormStateViewModel>.Success(state, "Document uploaded.");
        }

        private static string BuildDocumentFileName(string patientId, string documentType, string originalFileName, int companyCode, int userCode)
        {
            var extension = Path.GetExtension(originalFileName);
            if (string.IsNullOrWhiteSpace(extension))
                extension = ".bin";

            var safeDocType = SanitizeFilePart(documentType);
            if (string.IsNullOrWhiteSpace(safeDocType))
                safeDocType = "Document";

            var mrNo = GetPatientMrNo(patientId, companyCode, userCode);
            var safeMrNo = SanitizeFilePart(mrNo);
            if (string.IsNullOrWhiteSpace(safeMrNo))
                safeMrNo = "PENDING";

            var timePart = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            return safeDocType + "-" + safeMrNo + "-" + timePart + extension.ToLowerInvariant();
        }

        private static string GetPatientMrNo(string patientId, int companyCode, int userCode)
        {
            var erPatient = ERPatientRepository.ResolveActivePatient(patientId, companyCode, userCode);
            if (erPatient == null || !erPatient.intERAdmissionCode.HasValue)
                return "PENDING";

            var admission = BedRepository.GetPatientByAdmissionCode((int)erPatient.intERAdmissionCode.Value, companyCode);
            return string.IsNullOrWhiteSpace(admission?.MrNo) ? "PENDING" : admission.MrNo;
        }

        private static string SanitizeFilePart(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var chars = value.Trim().ToCharArray();
            var safeChars = chars
                .Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '-')
                .ToArray();

            var normalized = new string(safeChars);
            while (normalized.Contains("--"))
                normalized = normalized.Replace("--", "-");

            return normalized.Trim('-');
        }

        public byte[] GetDocumentBytes(long documentId, string patientId, int companyCode)
        {
            return ERReviewFormRepository.GetDocumentBytes(documentId, patientId, companyCode);
        }

        public OperationResult DeleteDocument(long documentId, string patientId, int companyCode, int userCode)
        {
            var ok = ERReviewFormRepository.DeleteDocument(documentId, patientId, companyCode, userCode);
            return ok
                ? OperationResult.Success("Document removed.")
                : OperationResult.Fail(ERReviewFormRepository.LastError ?? "Could not remove document.");
        }

        public ErFormViewModel BuildErFormModel(string patientId, int companyCode, int userCode)
        {
            if (string.IsNullOrWhiteSpace(patientId)) return null;

            // Always reload LOVs when opening/refreshing ER Form so DB label changes show immediately.
            ERLovRepository.ClearCache();

            var erPatient = ERPatientRepository.ResolveActivePatient(patientId, companyCode, userCode);
            if (erPatient == null) return null;

            var history = VitalRepository.GetVitals(erPatient.intERPatientCode.ToString(), companyCode);
            var latest = history.OrderByDescending(v => v.RecordedOn).FirstOrDefault()
                      ?? new VitalRecordViewModel { RecordedOn = DateTime.Now, BloodPressure = "0/0" };

            var admission = erPatient.intERAdmissionCode.HasValue
                ? BedRepository.GetPatientByAdmissionCode((int)erPatient.intERAdmissionCode.Value, companyCode)
                : null;

            var reviewForm = ERReviewFormRepository.GetFormState(erPatient.intERPatientCode.ToString(), companyCode);

            var chiefComplaintList = GetChiefComplaints();
            var complaintMap = ERReviewFormRepository.GetChiefComplaintSelections(
                erPatient.intERPatientCode.ToString(),
                companyCode,
                chiefComplaintList);

            var drugAllergyList = GetDrugAllergies(companyCode);
            var drugAllergyMap = ERReviewFormRepository.GetDrugAllergySelections(
                erPatient.intERPatientCode.ToString(),
                companyCode,
                drugAllergyList);

            return new ErFormViewModel
            {
                PatientId = erPatient.intERPatientCode.ToString(),
                PatientName = ResolvePatientDisplayName(
                    erPatient.strName,
                    admission?.PatientName,
                    erPatient.intWardBedCode),
                MrNo = string.IsNullOrWhiteSpace(admission?.MrNo) ? "PENDING" : admission.MrNo,
                AdmissionNo = string.IsNullOrWhiteSpace(admission?.AdmissionNo) ? "-" : admission.AdmissionNo,
                BedNo = BedRepository.ResolveBedDisplayName(
                    admission,
                    erPatient.intWardBedCode,
                    erPatient.intBranchCode,
                    companyCode,
                    userCode),
                AgeGender = string.IsNullOrWhiteSpace(admission?.AgeGender) ? "-" : admission.AgeGender,
                AdmissionDate = erPatient.dtmAdmission,
                TriageColor = erPatient.strTriageColor,
                LatestVital = latest,
                VitalHistory = history.OrderByDescending(v => v.RecordedOn).ToList(),
                ChiefComplaints = chiefComplaintList
                    .Select(x => complaintMap.ContainsKey(x.Id) ? complaintMap[x.Id] : x)
                    .ToList(),
                DrugAllergies = drugAllergyList
                    .Select(x => drugAllergyMap.ContainsKey(x.Id) ? drugAllergyMap[x.Id] : x)
                    .ToList(),
                ReviewForm = reviewForm,
                PastHistories = GetLovOptions(ERLovType.PastHistory),
                GcsOptions = GetLovOptions(ERLovType.GcsScore),
                ConsciousnessOptions = GetLovOptions(ERLovType.Conciousness),
                Spo2Options = GetLovOptions(ERLovType.SPo2),
                PlanterOptions = GetLovOptions(ERLovType.Planters),
                CvsOptions = GetLovOptions(ERLovType.Cvs),
                RespiratoryOptions = GetLovOptions(ERLovType.Respiratory),
                BowelSoundOptions = GetLovOptions(ERLovType.BowelSound),
                AbdomenOptions = GetLovOptions(ERLovType.Abdomen),
                ReceivedFromOptions = GetLovOptions(ERLovType.ReceivedFrom),
                OutcomeOptions = GetLovOptions(ERLovType.Outcome),
                ServiceGroups = GetServiceGroups(),
                ConditionUponReleaseOptions = GetLovOptions(ERLovType.ConditionUponRelease),
                AdmissionCategoryOptions = GetLovOptions(ERLovType.AdmissionCategory),
                AdrOptions = GetLovOptions(ERLovType.Adr),
                MedicinePackageOptions = GetItemPackageOptions(0),
                SurgicalPackageOptions = GetItemPackageOptions(1),
                DrugRouteOptions = GetDrugRouteOptions(companyCode),
                DocumentTypeOptions = ERLovRepository.GetDocumentTypesForERPortal(companyCode),
                EmployeeOptions = ERLovRepository.GetNursingEmployeesForER(companyCode),
                MoEmployeeOptions = ERLovRepository.GetMoEmployeesForER(companyCode),
                ConsultantOptions = ERLovRepository.GetConsultantsForERPortal(companyCode),
                RelationOptions = ERLovRepository.GetRelationsForERPortal(companyCode),
                ReferralReasonOptions = ERLovRepository.GetReferralReasonsForERPortal(companyCode),
                DischargeMedicineOptions = ERLovRepository.GetDischargeMedicineItemsForERPortal(companyCode),
                DrugFrequencyOptions = ERLovRepository.GetDrugFrequencyForERPortal(companyCode),
                AdmissionTypeOptions = ERLovRepository.GetAdmissionTypes(companyCode),
                AdmissionCareLevelOptions = ERLovRepository.GetAdmissionCareLevels(companyCode),
                IpdMedicineOptions = ERLovRepository.GetIpdPrescriptionMedicines(companyCode),
                IpdInvestigationServiceOptions = ERLovRepository.GetIpdInvestigationServices(companyCode),
                IpdRoutineServiceOptions = ERLovRepository.GetIpdRoutineServices(companyCode),
                DoseUnitOptions = ERLovRepository.GetDoseUnitsForERPortal(companyCode),
                ErItemOptions = GetErItemOptions(),
                InvestigationTestOptions = ERServiceGroupRepository.GetAllGroupServiceNames(),
                OutcomeForms = EROutcomeFormRepository.GetOutcomeFormState(
                    erPatient.intERPatientCode.ToString(),
                    erPatient.intERAdmissionCode.HasValue ? (int?)erPatient.intERAdmissionCode.Value : null,
                    companyCode),
                IpdAdmissionOrder = ERIpdAdmissionOrderRepository.GetState(
                    erPatient.intERPatientCode.ToString(),
                    erPatient.intERAdmissionCode.HasValue ? (int?)erPatient.intERAdmissionCode.Value : null,
                    companyCode,
                    ResolvePatientDisplayName(
                        erPatient.strName,
                        admission?.PatientName,
                        erPatient.intWardBedCode)),
                AddVitalInput = new AddVitalInputViewModel
                {
                    PatientId = erPatient.intERPatientCode.ToString()
                }
            };
        }

        private static string ResolvePatientDisplayName(string primary, string secondary, int bedCode)
        {
            if (!string.IsNullOrWhiteSpace(primary) && primary != "-")
                return primary.Trim();
            if (!string.IsNullOrWhiteSpace(secondary) && secondary != "-")
                return secondary.Trim();
            return bedCode > 0 ? $"BED#{bedCode}" : "-";
        }

        private static List<ChiefComplaintItemViewModel> GetChiefComplaints()
        {
            var chiefComplaints = ERLovRepository.GetByType(ERLovType.ChiefComplaints);
            return chiefComplaints
                .OrderBy(x => x.Description)
                .Select(x => new ChiefComplaintItemViewModel
                {
                    Id = x.LovCode,
                    Name = x.Description
                })
                .ToList();
        }

        private static List<DrugAllergyItemViewModel> GetDrugAllergies(int companyCode)
        {
            return ERLovRepository.GetItemGenericForERPortal(companyCode)
                .Where(x => x != null && x.Id > 0 && !string.IsNullOrWhiteSpace(x.Name))
                .OrderBy(x => x.Name)
                .Select(x => new DrugAllergyItemViewModel
                {
                    Id = x.Id,
                    Name = x.Name
                })
                .ToList();
        }

        private static List<ERLovOptionViewModel> GetLovOptions(ERLovType lovType)
        {
            return ERLovRepository.GetByType(lovType)
                .OrderBy(x => x.LovCode)
                .Select(x => new ERLovOptionViewModel
                {
                    Id = x.LovCode,
                    Name = x.Description
                })
                .ToList();
        }

        private static List<ERServiceGroupViewModel> GetServiceGroups()
        {
            return ERServiceGroupRepository.GetAll()
                .OrderBy(x => x.GroupName)
                .ToList();
        }

        private static List<ERLovOptionViewModel> GetItemPackageOptions(int intSurgicalPackage)
        {
            return ERItemPackageRepository.GetBySurgicalPackage(intSurgicalPackage)
                .OrderBy(x => x.PackageName)
                .Select(x => new ERLovOptionViewModel
                {
                    Id = x.ItemPackageCode,
                    Name = x.PackageName
                })
                .ToList();
        }

        private static List<ERItemViewModel> GetErItemOptions()
        {
            return ERItemRepository.GetAll()
                .Where(x => x.ItemCode > 0 && !string.IsNullOrWhiteSpace(x.ItemName))
                .OrderBy(x => x.ItemName)
                .ToList();
        }

        private static List<ERLovOptionViewModel> GetDrugRouteOptions(int companyCode)
        {
            return DrugRouteRepository.GetByCompanyCode(companyCode)
                .OrderBy(x => x.Route)
                .Select(x => new ERLovOptionViewModel
                {
                    Id = x.Code,
                    Name = x.Route
                })
                .ToList();
        }
    }
}
