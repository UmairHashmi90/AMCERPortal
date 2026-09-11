using ERPaperless.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;

namespace ERPaperless.Services
{
    public static class ERReviewFormRepository
    {
        public static string LastError { get; private set; }

        public static ErReviewFormStateViewModel GetFormState(string patientId, int companyCode)
        {
            var target = ResolvePatientTarget(patientId, companyCode);
            if (!target.IsValid)
                return new ErReviewFormStateViewModel();

            try
            {
                using (var db = dbAMCEntities.Create())
                {
                    var state = new ErReviewFormStateViewModel();

                    var header = db.tblERPatientReviewForms.FirstOrDefault(f =>
                        f.intERPatientCode == target.ERPatientCode
                        && f.intBranchCode == target.BranchCode
                        && f.intCompanyCode == target.CompanyCode
                        && f.intRecordStatusCode == 1);

                    if (header != null)
                        MapHeader(header, state);

                    state.SelectedPastHistoryCodes = new HashSet<int>(
                        db.tblERPatientPastHistories
                            .Where(x => x.intERPatientCode == target.ERPatientCode
                                        && x.intBranchCode == target.BranchCode
                                        && x.intCompanyCode == target.CompanyCode
                                        && x.intRecordStatusCode == 1)
                            .Select(x => x.intPastHistoryCode));

                    state.Investigations = db.tblERPatientInvestigations
                        .Where(x => x.intERPatientCode == target.ERPatientCode
                                    && x.intBranchCode == target.BranchCode
                                    && x.intCompanyCode == target.CompanyCode
                                    && x.intRecordStatusCode == 1)
                        .OrderBy(x => x.intSortOrder)
                        .ThenBy(x => x.intERPatientInvestigationCode)
                        .ToList()
                        .Select(MapInvestigation)
                        .ToList();
                    EnrichInvestigationUserNames(state.Investigations, target.CompanyCode);

                    var packageOrders = db.tblERPatientPackageOrders
                        .Where(x => x.intERPatientCode == target.ERPatientCode
                                    && x.intBranchCode == target.BranchCode
                                    && x.intCompanyCode == target.CompanyCode
                                    && x.intRecordStatusCode == 1)
                        .OrderBy(x => x.intSortOrder)
                        .ThenBy(x => x.intERPatientPackageOrderCode)
                        .ToList();

                    state.MedicineOrders = packageOrders
                        .Where(x => x.intPackageTypeCode == 1)
                        .Select(MapPackageOrder)
                        .ToList();

                    state.SurgicalOrders = packageOrders
                        .Where(x => x.intPackageTypeCode == 2)
                        .Select(MapPackageOrder)
                        .ToList();

                    EnrichPackageOrderDisplayFields(state.MedicineOrders, target.CompanyCode);
                    EnrichPackageOrderDisplayFields(state.SurgicalOrders, target.CompanyCode);

                    var documents = db.tblERPatientDocuments
                        .Where(x => x.intERPatientCode == target.ERPatientCode
                                    && x.intBranchCode == target.BranchCode
                                    && x.intCompanyCode == target.CompanyCode
                                    && x.intRecordStatusCode == 1)
                        .OrderByDescending(x => x.dtmCreated)
                        .ToList();

                    var documentUserCodes = documents
                        .Select(x => x.intCreatedByCode)
                        .Where(x => x > 0)
                        .Distinct()
                        .ToList();
                    var documentUserNames = LookupUserNames(documentUserCodes, target.CompanyCode);

                    state.Documents = documents
                        .Select(x => new DocumentRowViewModel
                        {
                            Id = x.intERPatientDocumentCode,
                            FileName = x.strFileName,
                            DocumentType = x.strDocumentType,
                            UploadedByName = documentUserNames.TryGetValue(x.intCreatedByCode, out var userName)
                                ? userName
                                : ("User " + x.intCreatedByCode),
                            UploadedOn = x.dtmCreated
                        })
                        .ToList();

                    return state;
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(ERReviewFormRepository), nameof(GetFormState), ex);
                LastError = ex.GetBaseException().Message;
                return new ErReviewFormStateViewModel();
            }
        }

        public static Dictionary<int, ChiefComplaintItemViewModel> GetChiefComplaintSelections(
            string patientId,
            int companyCode,
            IEnumerable<ChiefComplaintItemViewModel> allComplaints)
        {
            var target = ResolvePatientTarget(patientId, companyCode);
            var map = (allComplaints ?? Enumerable.Empty<ChiefComplaintItemViewModel>())
                .ToDictionary(x => x.Id, x => new ChiefComplaintItemViewModel
                {
                    Id = x.Id,
                    Name = x.Name,
                    IsSelected = false,
                    Remark = string.Empty
                });

            if (!target.IsValid)
                return map;

            try
            {
                using (var db = dbAMCEntities.Create())
                {
                    var saved = db.tblERPatientChiefComplaints
                        .Where(x => x.intERPatientCode == target.ERPatientCode
                                    && x.intBranchCode == target.BranchCode
                                    && x.intCompanyCode == target.CompanyCode
                                    && x.intRecordStatusCode == 1)
                        .ToList();

                    foreach (var row in saved)
                    {
                        if (!map.ContainsKey(row.intChiefComplaintCode))
                            continue;

                        map[row.intChiefComplaintCode].IsSelected = true;
                        map[row.intChiefComplaintCode].Remark = row.strRemark ?? string.Empty;
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(ERReviewFormRepository), nameof(GetChiefComplaintSelections), ex);
            }

            return map;
        }

        public static Dictionary<int, DrugAllergyItemViewModel> GetDrugAllergySelections(
            string patientId,
            int companyCode,
            IEnumerable<DrugAllergyItemViewModel> allAllergies)
        {
            var target = ResolvePatientTarget(patientId, companyCode);
            var map = (allAllergies ?? Enumerable.Empty<DrugAllergyItemViewModel>())
                .ToDictionary(x => x.Id, x => new DrugAllergyItemViewModel
                {
                    Id = x.Id,
                    Name = x.Name,
                    IsSelected = false,
                    Remark = string.Empty
                });

            if (!target.IsValid)
                return map;

            try
            {
                using (var db = dbAMCEntities.Create())
                {
                    var saved = db.tblERPatientDrugAllergies
                        .Where(x => x.intERPatientCode == target.ERPatientCode
                                    && x.intBranchCode == target.BranchCode
                                    && x.intCompanyCode == target.CompanyCode
                                    && x.intRecordStatusCode == 1
                                    && x.intItemGenericCode.HasValue)
                        .ToList();

                    foreach (var row in saved)
                    {
                        var code = row.intItemGenericCode.Value;
                        if (!map.ContainsKey(code))
                            continue;

                        map[code].IsSelected = true;
                        map[code].Remark = row.strRemarks ?? string.Empty;
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(ERReviewFormRepository), nameof(GetDrugAllergySelections), ex);
            }

            return map;
        }

        public static bool SaveReviewForm(
            SaveErReviewFormInputViewModel input,
            int companyCode,
            int userCode,
            bool isMO,
            bool isNursing)
        {
            LastError = null;

            if (input == null || string.IsNullOrWhiteSpace(input.PatientId))
            {
                LastError = "Patient record is missing.";
                return false;
            }

            var target = ResolvePatientTarget(input.PatientId, companyCode);
            if (!target.IsValid)
            {
                LastError = "Could not resolve patient record.";
                return false;
            }

            try
            {
                using (var db = dbAMCEntities.Create())
                using (var tx = db.Database.BeginTransaction())
                {
                    var now = DateTime.Now;

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

                    ApplyHeader(input, header, isMO, isNursing, userCode, now);

                    ApplyTriageColor(db, target, input.TriageColor, userCode, now);

                    if (isMO)
                    {
                        SyncChiefComplaints(db, target, input.ChiefComplaints, userCode, now);
                        SyncDrugAllergies(db, target, input.DrugAllergies, userCode, now);
                        SyncPastHistory(db, target, input.PastHistoryCodes, userCode, now);
                        SyncInvestigations(db, target, input.Investigations, userCode, now, true);
                    }

                    db.SaveChanges();
                    tx.Commit();
                    return true;
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(ERReviewFormRepository), nameof(SaveReviewForm), ex);
                LastError = ex.GetBaseException().Message;
                return false;
            }
        }

        public static bool SavePackageOrders(
            SavePackageOrdersInputViewModel input,
            int companyCode,
            int userCode)
        {
            LastError = null;

            if (input == null || string.IsNullOrWhiteSpace(input.PatientId))
            {
                LastError = "Patient record is missing.";
                return false;
            }

            if (input.PackageTypeCode != 1 && input.PackageTypeCode != 2)
            {
                LastError = "Invalid package type.";
                return false;
            }

            var target = ResolvePatientTarget(input.PatientId, companyCode);
            if (!target.IsValid)
            {
                LastError = "Could not resolve patient record.";
                return false;
            }

            var items = input.Items ?? new List<PackageOrderSaveItemViewModel>();

            try
            {
                using (var db = dbAMCEntities.Create())
                using (var tx = db.Database.BeginTransaction())
                {
                    var now = DateTime.Now;

                    UpdatePackageCodeOnHeader(db, target, input.PackageTypeCode, input.PackageCode, userCode, now);

                    var existing = db.tblERPatientPackageOrders
                        .Where(x => x.intERPatientCode == target.ERPatientCode
                                    && x.intBranchCode == target.BranchCode
                                    && x.intCompanyCode == target.CompanyCode
                                    && x.intPackageTypeCode == input.PackageTypeCode
                                    && x.intRecordStatusCode == 1)
                        .ToList();

                    var payloadIds = new HashSet<long>(
                        items.Where(x => x.Id > 0).Select(x => x.Id));

                    foreach (var row in existing.Where(x => !payloadIds.Contains(x.intERPatientPackageOrderCode)))
                    {
                        if (row.bolIsAcknowledged)
                            continue;

                        row.intRecordStatusCode = 8;
                        row.dtmLastM = now;
                        row.intAlteredByCode = userCode;
                    }

                    for (var i = 0; i < items.Count; i++)
                    {
                        var item = items[i];
                        if (string.IsNullOrWhiteSpace(item.ItemName))
                            continue;

                        if (string.IsNullOrWhiteSpace(item.Dose))
                            continue;

                        tblERPatientPackageOrder entity = null;
                        if (item.Id > 0)
                        {
                            entity = existing.FirstOrDefault(x =>
                                x.intERPatientPackageOrderCode == item.Id);
                        }

                        if (entity == null)
                        {
                            entity = new tblERPatientPackageOrder
                            {
                                intERPatientCode = target.ERPatientCode,
                                intBranchCode = target.BranchCode,
                                intCompanyCode = target.CompanyCode,
                                intPackageTypeCode = input.PackageTypeCode,
                                dtmCreated = now,
                                intOwnerCode = userCode,
                                intCreatedByCode = userCode,
                                intRecordStatusCode = 1
                            };
                            db.tblERPatientPackageOrders.Add(entity);
                        }
                        else if (entity.bolIsAcknowledged)
                        {
                            continue;
                        }

                        var isCustomItem = item.PackageCode <= 0 && item.PackageDetailCode <= 0;

                        if (isCustomItem)
                        {
                            entity.intERPackageCode = null;
                            entity.intERPackageDetailCode = null;
                        }
                        else
                        {
                            entity.intERPackageCode = item.PackageCode > 0
                                ? item.PackageCode
                                : (input.PackageCode ?? 0);
                            entity.intERPackageDetailCode = item.PackageDetailCode > 0
                                ? item.PackageDetailCode
                                : (int?)null;
                        }
                        entity.strItemName = item.ItemName.Trim();
                        entity.strDose = NullIfWhiteSpace(item.Dose);
                        entity.intDrugRouteCode = item.DrugRouteCode;
                        entity.strRemarks = NullIfWhiteSpace(item.Remarks);
                        entity.intSortOrder = i;

                        if (item.Discontinue && !entity.bolDiscontinue)
                        {
                            entity.bolDiscontinue = true;
                            entity.intDiscontinueByCode = userCode;
                            entity.dtmDiscontinue = now;
                        }
                        else if (!item.Discontinue && !entity.bolIsAcknowledged)
                        {
                            entity.bolDiscontinue = false;
                            entity.intDiscontinueByCode = null;
                            entity.dtmDiscontinue = null;
                        }

                        entity.dtmLastM = now;
                        entity.intAlteredByCode = userCode;
                    }

                    db.SaveChanges();
                    tx.Commit();
                    return true;
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(ERReviewFormRepository), nameof(SavePackageOrders), ex);
                LastError = ex.GetBaseException().Message;
                return false;
            }
        }

        public static bool SaveDocument(
            string patientId,
            int companyCode,
            int userCode,
            string fileName,
            string documentType,
            string contentType,
            byte[] fileBytes,
            string storedPath)
        {
            LastError = null;
            var target = ResolvePatientTarget(patientId, companyCode);
            if (!target.IsValid)
            {
                LastError = "Could not resolve patient record.";
                return false;
            }

            if (fileBytes == null || fileBytes.Length == 0)
            {
                LastError = "File is empty.";
                return false;
            }

            try
            {
                using (var db = dbAMCEntities.Create())
                {
                    var now = DateTime.Now;
                    db.tblERPatientDocuments.Add(new tblERPatientDocument
                    {
                        intERPatientCode = target.ERPatientCode,
                        intBranchCode = target.BranchCode,
                        intCompanyCode = target.CompanyCode,
                        strFileName = fileName,
                        strFilePath = storedPath,
                        strDocumentType = documentType,
                        vbrFile = fileBytes,
                        strContentType = contentType,
                        dtmCreated = now,
                        intOwnerCode = userCode,
                        intCreatedByCode = userCode,
                        intRecordStatusCode = 1
                    });

                    return db.SaveChanges() > 0;
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(ERReviewFormRepository), nameof(SaveDocument), ex);
                LastError = ex.GetBaseException().Message;
                return false;
            }
        }

        public static byte[] GetDocumentBytes(long documentId, string patientId, int companyCode)
        {
            var target = ResolvePatientTarget(patientId, companyCode);
            if (!target.IsValid) return null;

            try
            {
                using (var db = dbAMCEntities.Create())
                {
                    var doc = db.tblERPatientDocuments.FirstOrDefault(x =>
                        x.intERPatientDocumentCode == documentId
                        && x.intERPatientCode == target.ERPatientCode
                        && x.intCompanyCode == target.CompanyCode
                        && x.intRecordStatusCode == 1);

                    return doc?.vbrFile;
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(ERReviewFormRepository), nameof(GetDocumentBytes), ex);
                return null;
            }
        }

        public static bool DeleteDocument(long documentId, string patientId, int companyCode, int userCode)
        {
            LastError = null;
            var target = ResolvePatientTarget(patientId, companyCode);
            if (!target.IsValid) return false;

            try
            {
                using (var db = dbAMCEntities.Create())
                {
                    var doc = db.tblERPatientDocuments.FirstOrDefault(x =>
                        x.intERPatientDocumentCode == documentId
                        && x.intERPatientCode == target.ERPatientCode
                        && x.intCompanyCode == target.CompanyCode
                        && x.intRecordStatusCode == 1);

                    if (doc == null) return false;

                    doc.intRecordStatusCode = 8;
                    doc.dtmLastM = DateTime.Now;
                    doc.intAlteredByCode = userCode;
                    return db.SaveChanges() > 0;
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(ERReviewFormRepository), nameof(DeleteDocument), ex);
                LastError = ex.GetBaseException().Message;
                return false;
            }
        }

        private static void ApplyTriageColor(
            dbAMCEntities db,
            PatientTarget target,
            string triageColor,
            int userCode,
            DateTime now)
        {
            var patient = db.tblERPatients.FirstOrDefault(p =>
                p.intERPatientCode == target.ERPatientCode
                && p.intCompanyCode == target.CompanyCode
                && (p.intRecordStatusCode == 1 || p.intRecordStatusCode == 2));

            if (patient == null) return;

            patient.strTriageColor = ERPatientRepository.NormalizeTriageColor(triageColor);
            patient.dtmLastM = now;
            patient.intAlteredByCode = userCode;
        }

        private static void ApplyHeader(
            SaveErReviewFormInputViewModel input,
            tblERPatientReviewForm header,
            bool isMO,
            bool isNursing,
            int userCode,
            DateTime now)
        {
            if (isMO)
            {
                header.strPastMedicalHistory = NullIfWhiteSpace(input.PastMedicalHistory);
                header.strFoodAllergy = NullIfWhiteSpace(input.FoodAllergy);
                header.strPastSurgicalHistory = NullIfWhiteSpace(input.PastSurgicalHistory);

                header.intGcsCode = input.GcsCode;
                header.intPlanterCode = input.PlanterCode;
                header.intCvsCode = input.CvsCode;
                header.intRespiratoryCode = input.RespiratoryCode;
                header.intBowelSoundCode = input.BowelSoundCode;
                header.intAbdomenCode = input.AbdomenCode;
                header.intAdmissionCategoryCode = input.AdmissionCategoryCode;
                header.intReceivedFromCode = input.ReceivedFromCode;
                header.strDiscussedWith = NullIfWhiteSpace(input.DiscussedWith);
                header.strReferredTo = NullIfWhiteSpace(input.ReferredTo);

                header.intOutcomeCode = input.OutcomeCode;
                header.strAssessmentDiagnosis = NullIfWhiteSpace(input.AssessmentDiagnosis);
                header.intConditionUponReleaseLovCode = input.ConditionUponReleaseCode;
                header.intAdrLovCode = input.AdrCode;
                header.strTreatmentNotes = NullIfWhiteSpace(input.TreatmentNotes);
                header.strConsultantPlan = NullIfWhiteSpace(input.ConsultantPlan);
            }

            if (isNursing)
                header.strNursingObservations = NullIfWhiteSpace(input.NursingObservations);

            if (isMO)
                header.strNursingCarePlan = NullIfWhiteSpace(input.NursingCarePlan);

            header.dtmSaved = now;
            header.dtmLastM = now;
            header.intAlteredByCode = userCode;
        }

        private static void SyncChiefComplaints(
            dbAMCEntities db,
            PatientTarget target,
            List<ChiefComplaintSaveItemViewModel> items,
            int userCode,
            DateTime now)
        {
            items = items ?? new List<ChiefComplaintSaveItemViewModel>();
            var selectedCodes = new HashSet<int>(items.Select(x => x.Code));

            var existing = db.tblERPatientChiefComplaints
                .Where(x => x.intERPatientCode == target.ERPatientCode
                            && x.intBranchCode == target.BranchCode
                            && x.intCompanyCode == target.CompanyCode
                            && x.intRecordStatusCode == 1)
                .ToList();

            foreach (var row in existing.Where(x => !selectedCodes.Contains(x.intChiefComplaintCode)))
            {
                row.intRecordStatusCode = 8;
                row.dtmLastM = now;
                row.intAlteredByCode = userCode;
            }

            foreach (var item in items)
            {
                if (item.Code <= 0) continue;

                var entity = existing.FirstOrDefault(x => x.intChiefComplaintCode == item.Code);
                if (entity == null)
                {
                    entity = new tblERPatientChiefComplaint
                    {
                        intERPatientCode = target.ERPatientCode,
                        intBranchCode = target.BranchCode,
                        intCompanyCode = target.CompanyCode,
                        intChiefComplaintCode = item.Code,
                        dtmCreated = now,
                        intOwnerCode = userCode,
                        intCreatedByCode = userCode,
                        intRecordStatusCode = 1
                    };
                    db.tblERPatientChiefComplaints.Add(entity);
                }

                entity.strRemark = NullIfWhiteSpace(item.Remark);
                entity.dtmLastM = now;
                entity.intAlteredByCode = userCode;
            }
        }

        private static void SyncDrugAllergies(
            dbAMCEntities db,
            PatientTarget target,
            List<DrugAllergySaveItemViewModel> items,
            int userCode,
            DateTime now)
        {
            items = items ?? new List<DrugAllergySaveItemViewModel>();
            var selectedCodes = new HashSet<int>(items.Where(x => x != null && x.Code > 0).Select(x => x.Code));

            var existing = db.tblERPatientDrugAllergies
                .Where(x => x.intERPatientCode == target.ERPatientCode
                            && x.intBranchCode == target.BranchCode
                            && x.intCompanyCode == target.CompanyCode
                            && x.intRecordStatusCode == 1)
                .ToList();

            foreach (var row in existing.Where(x => !x.intItemGenericCode.HasValue || !selectedCodes.Contains(x.intItemGenericCode.Value)))
            {
                row.intRecordStatusCode = 8;
                row.dtmLastM = now;
                row.intAlteredByCode = userCode;
            }

            var nextCode = GetNextDrugAllergyCode(db, target.CompanyCode);

            foreach (var item in items)
            {
                if (item == null || item.Code <= 0) continue;

                var entity = existing.FirstOrDefault(x => x.intItemGenericCode == item.Code);
                if (entity == null)
                {
                    entity = new tblERPatientDrugAllergy
                    {
                        intERPatientAllergyCode = nextCode++,
                        intERPatientCode = target.ERPatientCode,
                        intERAdmissionCode = target.ERAdmissionCode > 0 ? (long?)target.ERAdmissionCode : null,
                        intItemGenericCode = item.Code,
                        dtmEntry = now,
                        dtmCreated = now,
                        intOwnerCode = userCode,
                        intCreatedByCode = userCode,
                        intRecordStatusCode = 1,
                        intBranchCode = target.BranchCode,
                        intCompanyCode = target.CompanyCode
                    };
                    db.tblERPatientDrugAllergies.Add(entity);
                }

                entity.strRemarks = NullIfWhiteSpace(item.Remark);
                entity.dtmLastM = now;
                entity.intAlteredByCode = userCode;
                entity.intRecordStatusCode = 1;
            }
        }

        private static long GetNextDrugAllergyCode(dbAMCEntities db, int companyCode)
        {
            var maxCode = db.tblERPatientDrugAllergies
                .Where(x => x.intCompanyCode == companyCode)
                .Select(x => (long?)x.intERPatientAllergyCode)
                .Max();

            return (maxCode ?? 0L) + 1L;
        }

        private static void SyncPastHistory(
            dbAMCEntities db,
            PatientTarget target,
            List<int> codes,
            int userCode,
            DateTime now)
        {
            codes = codes ?? new List<int>();
            var selected = new HashSet<int>(codes.Where(x => x > 0));

            var existing = db.tblERPatientPastHistories
                .Where(x => x.intERPatientCode == target.ERPatientCode
                            && x.intBranchCode == target.BranchCode
                            && x.intCompanyCode == target.CompanyCode
                            && x.intRecordStatusCode == 1)
                .ToList();

            foreach (var row in existing.Where(x => !selected.Contains(x.intPastHistoryCode)))
            {
                row.intRecordStatusCode = 8;
                row.dtmLastM = now;
                row.intAlteredByCode = userCode;
            }

            foreach (var code in selected)
            {
                if (existing.Any(x => x.intPastHistoryCode == code))
                    continue;

                db.tblERPatientPastHistories.Add(new tblERPatientPastHistory
                {
                    intERPatientCode = target.ERPatientCode,
                    intBranchCode = target.BranchCode,
                    intCompanyCode = target.CompanyCode,
                    intPastHistoryCode = code,
                    dtmCreated = now,
                    intOwnerCode = userCode,
                    intCreatedByCode = userCode,
                    intRecordStatusCode = 1
                });
            }
        }

        private static void SyncInvestigations(
            dbAMCEntities db,
            PatientTarget target,
            List<InvestigationSaveItemViewModel> items,
            int userCode,
            DateTime now,
            bool isMO)
        {
            if (!isMO)
                return;

            items = items ?? new List<InvestigationSaveItemViewModel>();

            var existing = db.tblERPatientInvestigations
                .Where(x => x.intERPatientCode == target.ERPatientCode
                            && x.intBranchCode == target.BranchCode
                            && x.intCompanyCode == target.CompanyCode
                            && x.intRecordStatusCode == 1)
                .ToList();

            var payloadIds = new HashSet<long>(items.Where(x => x.Id > 0).Select(x => x.Id));

            foreach (var row in existing.Where(x => !payloadIds.Contains(x.intERPatientInvestigationCode)))
            {
                if (row.bolIsAcknowledged)
                    continue;

                row.intRecordStatusCode = 8;
                row.dtmLastM = now;
                row.intAlteredByCode = userCode;
            }

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (string.IsNullOrWhiteSpace(item.TestName))
                    continue;

                tblERPatientInvestigation entity = null;
                if (item.Id > 0)
                    entity = existing.FirstOrDefault(x => x.intERPatientInvestigationCode == item.Id);

                if (entity == null)
                {
                    entity = new tblERPatientInvestigation
                    {
                        intERPatientCode = target.ERPatientCode,
                        intBranchCode = target.BranchCode,
                        intCompanyCode = target.CompanyCode,
                        dtmCreated = now,
                        intOwnerCode = userCode,
                        intCreatedByCode = userCode,
                        intRecordStatusCode = 1,
                        bolIsAcknowledged = false,
                        bolIsCancelled = false
                    };
                    db.tblERPatientInvestigations.Add(entity);
                }
                else if (entity.bolIsAcknowledged)
                {
                    continue;
                }

                entity.intERServiceGroupCode = item.ServiceGroupCode;
                entity.strTestName = item.TestName.Trim();
                entity.strRemarks = NullIfWhiteSpace(item.Remarks);
                entity.intSortOrder = i;

                if (item.IsCancelled && !entity.bolIsCancelled)
                {
                    entity.bolIsCancelled = true;
                    entity.intCancelledByCode = userCode;
                    entity.dtmCancelled = now;
                }
                else if (!item.IsCancelled && entity.bolIsCancelled && !entity.bolIsAcknowledged)
                {
                    entity.bolIsCancelled = false;
                    entity.intCancelledByCode = null;
                    entity.dtmCancelled = null;
                }

                entity.dtmLastM = now;
                entity.intAlteredByCode = userCode;
            }
        }

        public static List<PackageOrderRowViewModel> GetPackageOrdersForPharmacy(string patientId, int companyCode)
        {
            var target = ResolvePatientTarget(patientId, companyCode);
            if (!target.IsValid)
                return new List<PackageOrderRowViewModel>();

            LastError = null;

            var fromProc = TryLoadPackageOrdersForPharmacyFromProc(target);
            if (fromProc != null && fromProc.Count > 0)
            {
                foreach (var order in fromProc)
                    order.PatientCode = target.ERPatientCode;
                EnrichPackageOrderPatientFields(fromProc, companyCode, target.BranchCode, 0);
                return fromProc;
            }

            var fromDb = LoadPackageOrdersForPharmacyFromDb(target);
            if (fromDb.Count > 0)
            {
                EnrichPackageOrderDisplayFields(fromDb, companyCode);
                EnrichPackageOrderPatientFields(fromDb, companyCode, target.BranchCode, 0);
                return fromDb;
            }

            return fromProc ?? fromDb;
        }

        /// <summary>All pharmacy package orders for the branch — no patient filter.</summary>
        public static List<PackageOrderRowViewModel> GetAllPackageOrdersForPharmacy(
            int companyCode,
            int branchCode,
            int userCode,
            bool pendingOnly = true)
        {
            LastError = null;

            try
            {
                using (var db = dbAMCEntities.Create())
                {
                    var query =
                        from o in db.tblERPatientPackageOrders
                        join p in db.tblERPatients on o.intERPatientCode equals p.intERPatientCode
                        where o.intCompanyCode == companyCode
                              && o.intBranchCode == branchCode
                              && (o.intRecordStatusCode == 1 || o.intRecordStatusCode == 8)
                              && (o.intPackageTypeCode == 1 || o.intPackageTypeCode == 2)
                              && (p.intRecordStatusCode == 1 || p.intRecordStatusCode == 2)
                              && (p.bolIsDischarge != true)
                        select new { Order = o, Patient = p };

                    if (pendingOnly)
                    {
                        query = query.Where(x => x.Order.intRecordStatusCode == 1
                                                 && !x.Order.bolIsAcknowledged
                                                 && !x.Order.bolDiscontinue);
                    }

                    var rows = query
                        .OrderBy(x => x.Order.intPackageTypeCode)
                        .ThenByDescending(x => x.Order.dtmCreated)
                        .ToList();

                    var orders = rows.Select(x => MapPackageOrder(x.Order)).ToList();
                    EnrichPackageOrderDisplayFields(orders, companyCode);
                    EnrichPackageOrderPatientFields(orders, companyCode, branchCode, userCode);
                    return orders;
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(ERReviewFormRepository), nameof(GetAllPackageOrdersForPharmacy), ex);
                LastError = ex.GetBaseException().Message;
                return new List<PackageOrderRowViewModel>();
            }
        }

        private static List<PackageOrderRowViewModel> TryLoadPackageOrdersForPharmacyFromProc(PatientTarget target)
        {
            var result = new List<PackageOrderRowViewModel>();

            try
            {
                using (var conn = DBHelper.GetConnection())
                {
                    conn.Open();

                    using (var cmd = new SqlCommand("procGrdERPatientPackageOrderForPortal", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.Add("@intERPatientCode", SqlDbType.BigInt).Value = target.ERPatientCode;
                        cmd.Parameters.Add("@intBranchCode", SqlDbType.Int).Value = target.BranchCode;
                        cmd.Parameters.Add("@intCompanyCode", SqlDbType.Int).Value = target.CompanyCode;

                        using (var rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                                result.Add(MapPackageOrderFromReader(rdr));
                        }
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(ERReviewFormRepository), nameof(TryLoadPackageOrdersForPharmacyFromProc), ex);
                LastError = ex.GetBaseException().Message;
                return null;
            }
        }

        private static List<PackageOrderRowViewModel> LoadPackageOrdersForPharmacyFromDb(PatientTarget target)
        {
            try
            {
                using (var db = dbAMCEntities.Create())
                {
                    return db.tblERPatientPackageOrders
                        .Where(x => x.intERPatientCode == target.ERPatientCode
                                    && x.intBranchCode == target.BranchCode
                                    && x.intCompanyCode == target.CompanyCode
                                    && (x.intPackageTypeCode == 1 || x.intPackageTypeCode == 2)
                                    && (x.intRecordStatusCode == 1 || x.intRecordStatusCode == 8))
                        .OrderBy(x => x.intPackageTypeCode)
                        .ThenBy(x => x.intSortOrder)
                        .ThenBy(x => x.intERPatientPackageOrderCode)
                        .Select(MapPackageOrder)
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(ERReviewFormRepository), nameof(LoadPackageOrdersForPharmacyFromDb), ex);
                LastError = ex.GetBaseException().Message;
                return new List<PackageOrderRowViewModel>();
            }
        }

        private static void EnrichPackageOrderDisplayFields(List<PackageOrderRowViewModel> orders, int companyCode)
        {
            if (orders == null || orders.Count == 0)
                return;

            var routes = DrugRouteRepository.GetByCompanyCode(companyCode)
                .ToDictionary(x => x.Code, x => x.Route);

            var userCodes = new HashSet<int>();
            foreach (var order in orders)
            {
                if (order.CreatedByCode > 0)
                    userCodes.Add(order.CreatedByCode);
                if (order.AckByCode.HasValue)
                    userCodes.Add(order.AckByCode.Value);
                if (order.DiscontinueByCode.HasValue)
                    userCodes.Add(order.DiscontinueByCode.Value);
            }

            var userNames = LookupUserNames(userCodes, companyCode);

            foreach (var order in orders)
            {
                if (order.DrugRouteCode.HasValue
                    && routes.TryGetValue(order.DrugRouteCode.Value, out var routeName))
                {
                    order.DrugRouteName = routeName;
                }

                if (order.CreatedByCode > 0
                    && userNames.TryGetValue(order.CreatedByCode, out var createdName))
                {
                    order.RequestedByName = createdName;
                }

                if (order.AckByCode.HasValue
                    && userNames.TryGetValue(order.AckByCode.Value, out var ackName))
                {
                    order.AckByName = ackName;
                }

                if (order.DiscontinueByCode.HasValue
                    && userNames.TryGetValue(order.DiscontinueByCode.Value, out var discName))
                {
                    order.DiscontinueByName = discName;
                }
            }
        }

        private static void EnrichPackageOrderPatientFields(
            List<PackageOrderRowViewModel> orders,
            int companyCode,
            int branchCode,
            int userCode)
        {
            if (orders == null || orders.Count == 0)
                return;

            var beds = BedRepository.GetBeds(companyCode, branchCode, userCode)
                       ?? new List<LocationCardViewModel>();
            var bedByPatient = new Dictionary<long, LocationCardViewModel>();
            foreach (var bed in beds)
            {
                if (long.TryParse(bed.PatientId, out var patientCode) && patientCode > 0)
                    bedByPatient[patientCode] = bed;
            }

            var missingCodes = orders
                .Where(x => x.PatientCode > 0 && !bedByPatient.ContainsKey(x.PatientCode))
                .Select(x => x.PatientCode)
                .Distinct()
                .ToList();

            var patientNames = new Dictionary<long, string>();
            if (missingCodes.Count > 0)
            {
                try
                {
                    using (var db = dbAMCEntities.Create())
                    {
                        patientNames = db.tblERPatients
                            .Where(p => missingCodes.Contains(p.intERPatientCode))
                            .ToDictionary(p => p.intERPatientCode, p => p.strName);
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogging.Log(nameof(ERReviewFormRepository), nameof(EnrichPackageOrderPatientFields), ex);
                }
            }

            foreach (var order in orders)
            {
                if (order.PatientCode <= 0)
                    continue;

                if (bedByPatient.TryGetValue(order.PatientCode, out var bed))
                {
                    order.BedNo = bed.SlotName;
                    order.MrNo = bed.MrNo;
                    order.PatientName = !string.IsNullOrWhiteSpace(bed.PatientName) && bed.PatientName != "-"
                        ? bed.PatientName
                        : order.PatientName;
                }
                else
                {
                    order.BedNo = "BED-" + order.PatientCode;
                    order.MrNo = "PENDING";
                }

                if (string.IsNullOrWhiteSpace(order.PatientName)
                    && patientNames.TryGetValue(order.PatientCode, out var name)
                    && !string.IsNullOrWhiteSpace(name))
                {
                    order.PatientName = name;
                }

                if (string.IsNullOrWhiteSpace(order.PatientName))
                    order.PatientName = "Unknown";
            }
        }

        private static Dictionary<int, string> LookupUserNames(IEnumerable<int> userCodes, int companyCode)
        {
            var result = new Dictionary<int, string>();
            var codes = (userCodes ?? Enumerable.Empty<int>()).Where(x => x > 0).Distinct().ToList();
            if (codes.Count == 0)
                return result;

            try
            {
                using (var conn = DBHelper.GetConnection())
                {
                    conn.Open();

                    var paramNames = codes.Select((_, i) => "@u" + i).ToArray();
                    // Look up by user code only. Company filter caused missed names when
                    // tblUser.intCompanyCode differs from the ER session company.
                    var sql = @"
SELECT u.intUserCode,
       COALESCE(NULLIF(LTRIM(RTRIM(u.strUserName)), ''), NULLIF(LTRIM(RTRIM(u.strLoginName)), ''), CONCAT('User ', u.intUserCode)) AS strUserName
FROM dbo.tblUser u
WHERE u.intUserCode IN (" + string.Join(",", paramNames) + @")";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        for (var i = 0; i < codes.Count; i++)
                            cmd.Parameters.Add(paramNames[i], SqlDbType.Int).Value = codes[i];

                        using (var rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                var code = Convert.ToInt32(rdr["intUserCode"]);
                                var name = rdr["strUserName"]?.ToString();
                                if (!string.IsNullOrWhiteSpace(name))
                                    result[code] = name.Trim();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(ERReviewFormRepository), nameof(LookupUserNames), ex);
            }

            return result;
        }

        public static List<PackageOrderRowViewModel> GetMedicineOrders(string patientId, int companyCode)
        {
            return GetPackageOrdersForPharmacy(patientId, companyCode)
                .Where(x => x.PackageTypeCode == 1)
                .ToList();
        }

        public static PackageOrderRowViewModel AcknowledgePackageOrderForPharmacy(
            long orderId,
            string patientId,
            int companyCode,
            int userCode,
            string pharmacyUserName)
        {
            LastError = null;
            var target = ResolvePatientTarget(patientId, companyCode);
            if (!target.IsValid)
            {
                LastError = "Could not resolve patient record.";
                return null;
            }

            if (orderId <= 0)
            {
                LastError = "Package order record is missing.";
                return null;
            }

            try
            {
                using (var db = dbAMCEntities.Create())
                {
                    var entity = db.tblERPatientPackageOrders.FirstOrDefault(x =>
                        x.intERPatientPackageOrderCode == orderId
                        && x.intERPatientCode == target.ERPatientCode
                        && x.intBranchCode == target.BranchCode
                        && x.intCompanyCode == target.CompanyCode
                        && (x.intPackageTypeCode == 1 || x.intPackageTypeCode == 2)
                        && x.intRecordStatusCode == 1);

                    if (entity == null)
                    {
                        LastError = "Package order not found.";
                        return null;
                    }

                    if (entity.bolDiscontinue)
                    {
                        LastError = "This item is discontinued and cannot be charged.";
                        var discontinued = MapPackageOrder(entity);
                        EnrichPackageOrderDisplayFields(new List<PackageOrderRowViewModel> { discontinued }, companyCode);
                        return discontinued;
                    }

                    if (entity.bolIsAcknowledged)
                    {
                        var alreadyCharged = MapPackageOrder(entity);
                        EnrichPackageOrderDisplayFields(new List<PackageOrderRowViewModel> { alreadyCharged }, companyCode);
                        return alreadyCharged;
                    }

                    var now = DateTime.Now;
                    entity.bolIsAcknowledged = true;
                    entity.intAckByCode = userCode;
                    entity.dtmAck = now;
                    entity.dtmLastM = now;
                    entity.intAlteredByCode = userCode;

                    db.SaveChanges();

                    var mapped = MapPackageOrder(entity);
                    EnrichPackageOrderDisplayFields(new List<PackageOrderRowViewModel> { mapped }, companyCode);
                    if (string.IsNullOrWhiteSpace(mapped.AckByName) && !string.IsNullOrWhiteSpace(pharmacyUserName))
                        mapped.AckByName = pharmacyUserName;
                    return mapped;
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(ERReviewFormRepository), nameof(AcknowledgePackageOrderForPharmacy), ex);
                LastError = ex.GetBaseException().Message;
                return null;
            }
        }

        public static PackageOrderRowViewModel AcknowledgeMedicineOrderForPharmacy(
            long orderId,
            string patientId,
            int companyCode,
            int userCode,
            string pharmacyUserName)
        {
            return AcknowledgePackageOrderForPharmacy(
                orderId, patientId, companyCode, userCode, pharmacyUserName);
        }

        public static List<InvestigationRowViewModel> GetInvestigations(string patientId, int companyCode)
        {
            var target = ResolvePatientTarget(patientId, companyCode);
            if (!target.IsValid)
                return new List<InvestigationRowViewModel>();

            try
            {
                using (var db = dbAMCEntities.Create())
                {
                    return db.tblERPatientInvestigations
                        .Where(x => x.intERPatientCode == target.ERPatientCode
                                    && x.intBranchCode == target.BranchCode
                                    && x.intCompanyCode == target.CompanyCode
                                    && (x.intRecordStatusCode == 1 || x.intRecordStatusCode == 8))
                        .OrderBy(x => x.intSortOrder)
                        .ThenBy(x => x.intERPatientInvestigationCode)
                        .Select(MapInvestigation)
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(ERReviewFormRepository), nameof(GetInvestigations), ex);
                LastError = ex.GetBaseException().Message;
                return new List<InvestigationRowViewModel>();
            }
        }

        /// <summary>All billing investigations for the branch — no patient filter.</summary>
        public static List<InvestigationRowViewModel> GetAllInvestigationsForBilling(
            int companyCode,
            int branchCode,
            int userCode,
            bool pendingOnly = true)
        {
            LastError = null;

            try
            {
                using (var db = dbAMCEntities.Create())
                {
                    var query =
                        from i in db.tblERPatientInvestigations
                        join p in db.tblERPatients on i.intERPatientCode equals p.intERPatientCode
                        where i.intCompanyCode == companyCode
                              && i.intBranchCode == branchCode
                              && (i.intRecordStatusCode == 1 || i.intRecordStatusCode == 8)
                              && (p.intRecordStatusCode == 1 || p.intRecordStatusCode == 2)
                              && (p.bolIsDischarge != true)
                        select new { Investigation = i, Patient = p };

                    if (pendingOnly)
                    {
                        query = query.Where(x => x.Investigation.intRecordStatusCode == 1
                                                 && !x.Investigation.bolIsAcknowledged
                                                 && !x.Investigation.bolIsCancelled);
                    }

                    var rows = query
                        .OrderByDescending(x => x.Investigation.dtmCreated)
                        .ToList();

                    var investigations = rows.Select(x => MapInvestigation(x.Investigation)).ToList();
                    EnrichInvestigationUserNames(investigations, companyCode);
                    EnrichInvestigationPatientFields(investigations, companyCode, branchCode, userCode);
                    return investigations;
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(ERReviewFormRepository), nameof(GetAllInvestigationsForBilling), ex);
                LastError = ex.GetBaseException().Message;
                return new List<InvestigationRowViewModel>();
            }
        }

        private static void EnrichInvestigationUserNames(List<InvestigationRowViewModel> investigations, int companyCode)
        {
            if (investigations == null || investigations.Count == 0)
                return;

            var userCodes = new HashSet<int>();
            foreach (var inv in investigations)
            {
                if (inv.CreatedByCode > 0)
                    userCodes.Add(inv.CreatedByCode);
                if (inv.AckByCode.HasValue && inv.AckByCode.Value > 0)
                    userCodes.Add(inv.AckByCode.Value);
            }

            var userNames = LookupUserNames(userCodes, companyCode);
            foreach (var inv in investigations)
            {
                if (inv.CreatedByCode > 0
                    && userNames.TryGetValue(inv.CreatedByCode, out var createdName))
                {
                    inv.RequestedByName = createdName;
                }

                if (inv.AckByCode.HasValue
                    && userNames.TryGetValue(inv.AckByCode.Value, out var ackName))
                {
                    inv.AckByName = ackName;
                }
            }
        }

        private static void EnrichInvestigationPatientFields(
            List<InvestigationRowViewModel> investigations,
            int companyCode,
            int branchCode,
            int userCode)
        {
            if (investigations == null || investigations.Count == 0)
                return;

            var beds = BedRepository.GetBeds(companyCode, branchCode, userCode)
                       ?? new List<LocationCardViewModel>();
            var bedByPatient = new Dictionary<long, LocationCardViewModel>();
            foreach (var bed in beds)
            {
                if (long.TryParse(bed.PatientId, out var patientCode) && patientCode > 0)
                    bedByPatient[patientCode] = bed;
            }

            foreach (var inv in investigations)
            {
                if (inv.PatientCode <= 0)
                    continue;

                if (bedByPatient.TryGetValue(inv.PatientCode, out var bed))
                {
                    inv.BedNo = bed.SlotName;
                    inv.MrNo = bed.MrNo;
                    inv.PatientName = !string.IsNullOrWhiteSpace(bed.PatientName) && bed.PatientName != "-"
                        ? bed.PatientName
                        : inv.PatientName;
                }
                else
                {
                    inv.BedNo = "BED-" + inv.PatientCode;
                    inv.MrNo = "PENDING";
                }

                if (string.IsNullOrWhiteSpace(inv.PatientName))
                    inv.PatientName = "Unknown";
            }
        }

        public static InvestigationRowViewModel AcknowledgeInvestigationForBilling(
            long investigationId,
            string patientId,
            int companyCode,
            int userCode,
            string billingUserName)
        {
            LastError = null;
            var target = ResolvePatientTarget(patientId, companyCode);
            if (!target.IsValid)
            {
                LastError = "Could not resolve patient record.";
                return null;
            }

            if (investigationId <= 0)
            {
                LastError = "Investigation record is missing.";
                return null;
            }

            try
            {
                using (var db = dbAMCEntities.Create())
                {
                    var entity = db.tblERPatientInvestigations.FirstOrDefault(x =>
                        x.intERPatientInvestigationCode == investigationId
                        && x.intERPatientCode == target.ERPatientCode
                        && x.intBranchCode == target.BranchCode
                        && x.intCompanyCode == target.CompanyCode
                        && x.intRecordStatusCode == 1);

                    if (entity == null)
                    {
                        LastError = "Investigation not found.";
                        return null;
                    }

                    if (entity.bolIsCancelled)
                    {
                        LastError = "This test is cancelled and cannot be acknowledged.";
                        var cancelled = MapInvestigation(entity);
                        EnrichInvestigationUserNames(new List<InvestigationRowViewModel> { cancelled }, companyCode);
                        return cancelled;
                    }

                    if (entity.bolIsAcknowledged)
                    {
                        var alreadyAcked = MapInvestigation(entity);
                        EnrichInvestigationUserNames(new List<InvestigationRowViewModel> { alreadyAcked }, companyCode);
                        return alreadyAcked;
                    }

                    var now = DateTime.Now;
                    entity.bolIsAcknowledged = true;
                    entity.intAckByCode = userCode;
                    entity.dtmAck = now;
                    entity.dtmLastM = now;
                    entity.intAlteredByCode = userCode;

                    db.SaveChanges();

                    var mapped = MapInvestigation(entity);
                    EnrichInvestigationUserNames(new List<InvestigationRowViewModel> { mapped }, companyCode);
                    if (string.IsNullOrWhiteSpace(mapped.AckByName) && !string.IsNullOrWhiteSpace(billingUserName))
                        mapped.AckByName = billingUserName;
                    return mapped;
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(ERReviewFormRepository), nameof(AcknowledgeInvestigationForBilling), ex);
                LastError = ex.GetBaseException().Message;
                return null;
            }
        }

        private static void UpdatePackageCodeOnHeader(
            dbAMCEntities db,
            PatientTarget target,
            int packageTypeCode,
            int? packageCode,
            int userCode,
            DateTime now)
        {
            if (!packageCode.HasValue || packageCode.Value <= 0)
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

            if (packageTypeCode == 1)
                header.intMedicinePackageCode = packageCode;
            else
                header.intSurgicalPackageCode = packageCode;

            header.dtmLastM = now;
            header.intAlteredByCode = userCode;
        }

        private static void MapHeader(tblERPatientReviewForm header, ErReviewFormStateViewModel state)
        {
            state.ReviewFormCode = header.intERPatientReviewFormCode;
            state.SavedOn = header.dtmSaved;
            state.PastMedicalHistory = header.strPastMedicalHistory;
            state.FoodAllergy = header.strFoodAllergy;
            state.PastSurgicalHistory = header.strPastSurgicalHistory;
            state.GcsCode = header.intGcsCode;
            state.PlanterCode = header.intPlanterCode;
            state.CvsCode = header.intCvsCode;
            state.RespiratoryCode = header.intRespiratoryCode;
            state.BowelSoundCode = header.intBowelSoundCode;
            state.AbdomenCode = header.intAbdomenCode;
            state.AdmissionCategoryCode = header.intAdmissionCategoryCode;
            state.ReceivedFromCode = header.intReceivedFromCode;
            state.OutcomeCode = header.intOutcomeCode;
            state.DiscussedWith = header.strDiscussedWith;
            state.ReferredTo = header.strReferredTo;
            state.AssessmentDiagnosis = header.strAssessmentDiagnosis;
            state.MedicinePackageCode = header.intMedicinePackageCode;
            state.SurgicalPackageCode = header.intSurgicalPackageCode;
            state.ConditionUponReleaseCode = header.intConditionUponReleaseLovCode;
            state.AdrCode = header.intAdrLovCode;
            state.TreatmentNotes = header.strTreatmentNotes;
            state.ConsultantPlan = header.strConsultantPlan;
            state.NursingObservations = header.strNursingObservations;
            state.NursingCarePlan = header.strNursingCarePlan;
        }

        private static InvestigationRowViewModel MapInvestigation(tblERPatientInvestigation x)
        {
            return new InvestigationRowViewModel
            {
                Id = x.intERPatientInvestigationCode,
                PatientCode = x.intERPatientCode,
                RecordStatusCode = x.intRecordStatusCode,
                ServiceGroupCode = x.intERServiceGroupCode,
                TestName = x.strTestName,
                Remarks = x.strRemarks,
                IsAcknowledged = x.bolIsAcknowledged,
                AckByCode = x.intAckByCode,
                AckByName = x.intAckByCode.HasValue ? "User " + x.intAckByCode : null,
                AckDate = x.dtmAck,
                IsCancelled = x.bolIsCancelled,
                CancelledByCode = x.intCancelledByCode,
                CancelledByName = x.intCancelledByCode.HasValue ? "User " + x.intCancelledByCode : null,
                CancelledDate = x.dtmCancelled,
                SortOrder = x.intSortOrder,
                CreatedByCode = x.intCreatedByCode,
                RequestedOn = x.dtmCreated,
                RequestedByName = "User " + x.intCreatedByCode
            };
        }

        private static PackageOrderRowViewModel MapPackageOrder(tblERPatientPackageOrder x)
        {
            return new PackageOrderRowViewModel
            {
                Id = x.intERPatientPackageOrderCode,
                PatientCode = x.intERPatientCode,
                RecordStatusCode = x.intRecordStatusCode,
                PackageTypeCode = x.intPackageTypeCode,
                PackageCode = x.intERPackageCode ?? 0,
                PackageDetailCode = x.intERPackageDetailCode ?? 0,
                ItemName = x.strItemName,
                Dose = x.strDose,
                DrugRouteCode = x.intDrugRouteCode,
                Discontinue = x.bolDiscontinue,
                Remarks = x.strRemarks,
                DiscontinueByCode = x.intDiscontinueByCode,
                DiscontinueDate = x.dtmDiscontinue,
                IsAcknowledged = x.bolIsAcknowledged,
                AckByCode = x.intAckByCode,
                AckByName = x.intAckByCode.HasValue ? "User " + x.intAckByCode : null,
                AckDate = x.dtmAck,
                SortOrder = x.intSortOrder,
                CreatedByCode = x.intCreatedByCode,
                RequestedOn = x.dtmCreated,
                RequestedByName = "User " + x.intCreatedByCode
            };
        }

        private static PackageOrderRowViewModel MapPackageOrderFromReader(SqlDataReader rdr)
        {
            return new PackageOrderRowViewModel
            {
                Id = ReadInt64(rdr, "intERPatientPackageOrderCode"),
                PackageTypeCode = ReadInt32(rdr, "intPackageTypeCode"),
                ItemName = ReadFirstString(rdr, "strItemName"),
                Dose = ReadFirstString(rdr, "Dose", "strDose"),
                DrugRouteName = ReadFirstString(rdr, "Route", "strDrugRoute"),
                Discontinue = ReadOptionalBoolean(rdr, "Discontinue", "bolDiscontinue"),
                Remarks = ReadFirstString(rdr, "Remarks", "strRemarks"),
                DiscontinueByName = ReadFirstString(rdr, "strDiscontinueByName"),
                DiscontinueDate = ReadOptionalDateTime(rdr, "dtmDiscontinue"),
                IsAcknowledged = ReadOptionalBoolean(rdr, "Acknowledged", "bolIsAcknowledged"),
                AckByName = ReadFirstString(rdr, "strAckByName"),
                AckDate = ReadOptionalDateTime(rdr, "dtmAck"),
                RequestedOn = ReadOptionalDateTime(rdr, "dtmCreated") ?? DateTime.MinValue,
                RequestedByName = ReadFirstString(rdr, "strCreatedByName")
            };
        }

        private static string ReadFirstString(SqlDataReader rdr, params string[] columnNames)
        {
            foreach (var columnName in columnNames)
            {
                try
                {
                    var ordinal = rdr.GetOrdinal(columnName);
                    if (!rdr.IsDBNull(ordinal))
                        return rdr.GetString(ordinal);
                }
                catch (IndexOutOfRangeException)
                {
                }
            }

            return null;
        }

        private static int ReadInt32(SqlDataReader rdr, string columnName)
        {
            var ordinal = rdr.GetOrdinal(columnName);
            return rdr.IsDBNull(ordinal) ? 0 : Convert.ToInt32(rdr.GetValue(ordinal));
        }

        private static long ReadInt64(SqlDataReader rdr, string columnName)
        {
            var ordinal = rdr.GetOrdinal(columnName);
            return rdr.IsDBNull(ordinal) ? 0L : Convert.ToInt64(rdr.GetValue(ordinal));
        }

        private static int? ReadNullableInt32(SqlDataReader rdr, string columnName)
        {
            var ordinal = rdr.GetOrdinal(columnName);
            return rdr.IsDBNull(ordinal) ? (int?)null : Convert.ToInt32(rdr.GetValue(ordinal));
        }

        private static bool ReadOptionalBoolean(SqlDataReader rdr, params string[] columnNames)
        {
            foreach (var columnName in columnNames)
            {
                try
                {
                    var ordinal = rdr.GetOrdinal(columnName);
                    return !rdr.IsDBNull(ordinal) && Convert.ToBoolean(rdr.GetValue(ordinal));
                }
                catch (IndexOutOfRangeException)
                {
                }
            }

            return false;
        }

        private static DateTime? ReadOptionalDateTime(SqlDataReader rdr, params string[] columnNames)
        {
            foreach (var columnName in columnNames)
            {
                try
                {
                    var ordinal = rdr.GetOrdinal(columnName);
                    if (!rdr.IsDBNull(ordinal))
                        return Convert.ToDateTime(rdr.GetValue(ordinal));
                }
                catch (IndexOutOfRangeException)
                {
                }
            }

            return null;
        }

        private static bool ReadBoolean(SqlDataReader rdr, string columnName)
        {
            return ReadOptionalBoolean(rdr, columnName);
        }

        private static DateTime ReadDateTime(SqlDataReader rdr, string columnName)
        {
            var ordinal = rdr.GetOrdinal(columnName);
            return rdr.IsDBNull(ordinal) ? DateTime.MinValue : Convert.ToDateTime(rdr.GetValue(ordinal));
        }

        private static DateTime? ReadNullableDateTime(SqlDataReader rdr, string columnName)
        {
            var ordinal = rdr.GetOrdinal(columnName);
            return rdr.IsDBNull(ordinal) ? (DateTime?)null : Convert.ToDateTime(rdr.GetValue(ordinal));
        }

        private static string NullIfWhiteSpace(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static PatientTarget ResolvePatientTarget(string patientId, int companyCode)
        {
            if (string.IsNullOrWhiteSpace(patientId) || companyCode <= 0)
                return PatientTarget.Invalid;

            if (!long.TryParse(patientId, out var erPatientCode) || erPatientCode <= 0)
                return PatientTarget.Invalid;

            var patient = ERPatientRepository.GetByCode(erPatientCode, companyCode, includeDischarged: true);
            if (patient == null)
                return PatientTarget.Invalid;

            return new PatientTarget
            {
                ERPatientCode = patient.intERPatientCode,
                ERAdmissionCode = patient.intERAdmissionCode ?? 0L,
                BranchCode = patient.intBranchCode,
                CompanyCode = companyCode
            };
        }

        private struct PatientTarget
        {
            public static PatientTarget Invalid => new PatientTarget();

            public long ERPatientCode { get; set; }
            public long ERAdmissionCode { get; set; }
            public int BranchCode { get; set; }
            public int CompanyCode { get; set; }

            public bool IsValid =>
                ERPatientCode > 0 &&
                BranchCode > 0 &&
                CompanyCode > 0;
        }
    }
}
