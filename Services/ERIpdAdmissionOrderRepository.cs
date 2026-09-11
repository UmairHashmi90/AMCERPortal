using ERPaperless.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity.Validation;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;

namespace ERPaperless.Services
{
    public static class ERIpdAdmissionOrderRepository
    {
        public static string LastError { get; private set; }

        public static IpdAdmissionOrderStateViewModel GetState(
            string patientId,
            int? admissionCode,
            int companyCode,
            string patientNameFallback = null)
        {
            var state = new IpdAdmissionOrderStateViewModel
            {
                PatientName = patientNameFallback
            };

            var target = ResolveTarget(patientId, admissionCode, companyCode);
            if (!target.IsValid)
                return state;

            state.PatientCode = target.PatientCode;
            if (string.IsNullOrWhiteSpace(state.PatientName))
                state.PatientName = ResolvePatientDisplayName(target, companyCode);

            try
            {
                using (var db = dbAMCEntities.Create())
                {
                    var erPatient = db.tblERPatients
                        .FirstOrDefault(x => x.intERPatientCode == target.ERPatientCode
                                             && x.intCompanyCode == companyCode);
                    if (erPatient != null)
                        state.Hopi = erPatient.strHOPI;

                    var header = QueryOrders(db, target, companyCode, activeOnly: true)
                        .OrderByDescending(x => x.intIPDAdmOrderCode)
                        .FirstOrDefault();

                    if (header == null)
                        return state;

                    state.Id = header.intIPDAdmOrderCode;
                    state.OrderNo = header.strIPDAdmOrderNo;
                    state.OrderDate = header.dtmIPDAdmOrder;
                    state.AdmissionRequestDate = header.dtAdmissionRequest;
                    state.PatientCode = header.intPatientCode;
                    state.AdmittingConsultantCode = header.intAdmConsultantCode.HasValue && header.intAdmConsultantCode.Value > 0
                        ? header.intAdmConsultantCode
                        : null;
                    state.ReferringConsultantCode = header.intReffConsultantCode.HasValue && header.intReffConsultantCode.Value > 0
                        ? header.intReffConsultantCode
                        : null;
                    state.AdmissionTypeCode = header.intAdmissionTypeCode;
                    state.CareLevelCode = header.intAdmissionCareLevelCode;
                    state.Diagnosis = header.strDiagnosis;
                    state.DietInstruction = header.strDietInstruction;
                    state.Comments = header.strComments;
                    state.IsFinal = header.intIPDAdmOrderStatusCode == 2;

                    var medicineNames = ERLovRepository.GetIpdPrescriptionMedicines(companyCode)
                        .GroupBy(x => x.Id)
                        .ToDictionary(g => (long)g.Key, g => g.First().Name);
                    var serviceNames = ERLovRepository.GetIpdInvestigationServices(companyCode)
                        .Concat(ERLovRepository.GetIpdRoutineServices(companyCode))
                        .GroupBy(x => x.Id)
                        .ToDictionary(g => (long)g.Key, g => g.First().Name);

                    state.Medicines = db.tblIPDAdmPrescriptionOrders
                        .Where(x => x.intIPDAdmOrderCode == header.intIPDAdmOrderCode
                                    && x.intCompanyCode == companyCode
                                    && x.intBranchCode == target.BranchCode
                                    && x.intRecordStatusCode == 1)
                        .OrderBy(x => x.intIPDAdmPrescriptionOrderCode)
                        .AsEnumerable()
                        .Select(x => new IpdAdmissionMedicineRowViewModel
                        {
                            Id = x.intIPDAdmPrescriptionOrderCode,
                            ItemCode = x.intItemCode,
                            MedicineName = x.intItemCode.HasValue && medicineNames.ContainsKey(x.intItemCode.Value)
                                ? medicineNames[x.intItemCode.Value]
                                : string.Empty,
                            PrescribedDose = x.numPrescribedDose,
                            DoseUnitCode = x.intPrescribedUnitCode,
                            FrequencyCode = x.intDrugFrequencyCode,
                            RouteCode = x.intDrugRouteCode,
                            Days = x.intDays,
                            Instruction = x.strInstruction
                        })
                        .ToList();

                    state.Investigations = db.tblIPDAdmServiceOrders
                        .Where(x => x.intIPDAdmOrderCode == header.intIPDAdmOrderCode
                                    && x.intCompanyCode == companyCode
                                    && x.intBranchCode == target.BranchCode
                                    && x.intRecordStatusCode == 1)
                        .OrderBy(x => x.intIPDAdmServiceOrderCode)
                        .AsEnumerable()
                        .Select(x => new IpdAdmissionServiceRowViewModel
                        {
                            Id = x.intIPDAdmServiceOrderCode,
                            ServiceCode = x.intServiceCode,
                            ServiceName = x.intServiceCode.HasValue && serviceNames.ContainsKey(x.intServiceCode.Value)
                                ? serviceNames[x.intServiceCode.Value]
                                : string.Empty
                        })
                        .ToList();

                    state.RoutineServices = db.tblIPDAdmServiceInsts
                        .Where(x => x.intIPDAdmOrderCode == header.intIPDAdmOrderCode
                                    && x.intCompanyCode == companyCode
                                    && x.intBranchCode == target.BranchCode
                                    && x.intRecordStatusCode == 1)
                        .OrderBy(x => x.intIPDAdmServiceInstCode)
                        .AsEnumerable()
                        .Select(x => new IpdAdmissionRoutineServiceRowViewModel
                        {
                            Id = x.intIPDAdmServiceInstCode,
                            ServiceCode = x.intServiceCode,
                            ServiceName = x.intServiceCode.HasValue && serviceNames.ContainsKey(x.intServiceCode.Value)
                                ? serviceNames[x.intServiceCode.Value]
                                : string.Empty,
                            Instruction = x.strInstruction
                        })
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(ERIpdAdmissionOrderRepository), nameof(GetState), ex);
            }

            return state;
        }

        public static bool Save(SaveIpdAdmissionOrderInputViewModel input, int companyCode, int userCode)
        {
            LastError = null;

            if (input == null || string.IsNullOrWhiteSpace(input.PatientId))
            {
                LastError = "Patient record is missing.";
                return false;
            }

            var target = ResolveTarget(input.PatientId, input.AdmissionCode, companyCode);
            if (!target.IsValid)
            {
                LastError = target.IsPendingMr
                    ? "MRNO is pending."
                    : "Could not resolve patient record. Open the ER form again and retry.";
                return false;
            }

            try
            {
                using (var db = dbAMCEntities.Create())
                using (var tx = db.Database.BeginTransaction())
                {
                    var now = DateTime.Now;
                    EnsureExclusiveWithOutcomes(db, target, input, companyCode, userCode);

                    var header = QueryOrders(db, target, companyCode, activeOnly: true)
                        .OrderByDescending(x => x.intIPDAdmOrderCode)
                        .FirstOrDefault();

                    if (header != null && header.intIPDAdmOrderStatusCode == 2)
                        throw new InvalidOperationException("IPD Admission Order is finalized and cannot be updated.");

                    if (header == null)
                    {
                        header = new tblIPDAdmOrder
                        {
                            intIPDAdmOrderCode = GetNextOrderCode(db, companyCode),
                            strIPDAdmOrderNo = GenerateNextOrderNo(db, companyCode, target.BranchCode),
                            dtmIPDAdmOrder = now,
                            dtAdmissionRequest = now,
                            intERAdmissionCode = target.ERAdmissionCode,
                            intPatientCode = target.PatientCode,
                            dtmCreated = now,
                            intOwnerCode = userCode,
                            intCreatedByCode = userCode,
                            intRecordStatusCode = 1,
                            intBranchCode = target.BranchCode,
                            intCompanyCode = companyCode,
                            intIPDAdmOrderStatusCode = 1
                        };
                        db.tblIPDAdmOrders.Add(header);
                    }

                    header.intAdmConsultantCode = input.AdmittingConsultantCode;
                    header.intReffConsultantCode = input.ReferringConsultantCode;
                    header.intAdmissionTypeCode = input.AdmissionTypeCode;
                    header.intAdmissionCareLevelCode = input.CareLevelCode;
                    header.strDiagnosis = NullIfWhiteSpace(input.Diagnosis);
                    header.strDietInstruction = NullIfWhiteSpace(input.DietInstruction);
                    header.strComments = NullIfWhiteSpace(input.Comments);
                    header.dtmLastM = now;
                    header.intAlteredByCode = userCode;
                    header.intRecordStatusCode = 1;
                    header.intIPDAdmOrderStatusCode = input.IsFinal ? 2 : 1;
                    if (header.dtAdmissionRequest == default(DateTime))
                        header.dtAdmissionRequest = now;

                    var erPatient = db.tblERPatients
                        .FirstOrDefault(x => x.intERPatientCode == target.ERPatientCode
                                             && x.intCompanyCode == companyCode);
                    if (erPatient != null)
                    {
                        var hopi = NullIfWhiteSpace(input.Hopi);
                        if (hopi != null && hopi.Length > 500)
                            hopi = hopi.Substring(0, 500);
                        erPatient.strHOPI = hopi;
                        erPatient.dtmLastM = now;
                        erPatient.intAlteredByCode = userCode;
                    }

                    db.SaveChanges();

                    SyncMedicines(db, header, input.Medicines, target, userCode, now);
                    SyncInvestigations(db, header, input.Investigations, target, userCode, now);
                    SyncRoutineServices(db, header, input.RoutineServices, target, userCode, now);

                    // Keep Outcome radio on Admitted/Admission — do not leave/revert to Discharge.
                    EROutcomeFormRepository.SyncReviewFormOutcomeCode(
                        db,
                        target.ERPatientCode,
                        target.BranchCode,
                        companyCode,
                        "admission",
                        userCode,
                        now);

                    db.SaveChanges();
                    tx.Commit();
                    return true;
                }
            }
            catch (InvalidOperationException ex)
            {
                LastError = ex.Message;
                return false;
            }
            catch (DbEntityValidationException vex)
            {
                LastError = string.Join(" ",
                    vex.EntityValidationErrors.SelectMany(e => e.ValidationErrors).Select(e => e.ErrorMessage));
                ErrorLogging.Log(nameof(ERIpdAdmissionOrderRepository), nameof(Save), vex);
                return false;
            }
            catch (Exception ex)
            {
                LastError = ex.GetBaseException().Message;
                ErrorLogging.Log(nameof(ERIpdAdmissionOrderRepository), nameof(Save), ex);
                return false;
            }
        }

        public static void SoftDeleteActiveOrders(
            dbAMCEntities db,
            long erAdmissionCode,
            long patientCode,
            int branchCode,
            int companyCode,
            int userCode)
        {
            if (db == null || companyCode <= 0) return;

            var now = DateTime.Now;
            var orders = db.tblIPDAdmOrders
                .Where(x => x.intCompanyCode == companyCode
                            && x.intBranchCode == branchCode
                            && x.intRecordStatusCode == 1
                            && ((erAdmissionCode > 0 && x.intERAdmissionCode == erAdmissionCode)
                                || (patientCode > 0 && x.intPatientCode == patientCode)))
                .ToList();

            foreach (var order in orders)
            {
                order.intRecordStatusCode = 8;
                order.dtmLastM = now;
                order.intAlteredByCode = userCode;

                foreach (var row in db.tblIPDAdmPrescriptionOrders.Where(x =>
                    x.intIPDAdmOrderCode == order.intIPDAdmOrderCode
                    && x.intCompanyCode == companyCode
                    && x.intBranchCode == branchCode
                    && x.intRecordStatusCode == 1))
                {
                    row.intRecordStatusCode = 8;
                    row.dtmLastM = now;
                    row.intAlteredByCode = userCode;
                }

                foreach (var row in db.tblIPDAdmServiceOrders.Where(x =>
                    x.intIPDAdmOrderCode == order.intIPDAdmOrderCode
                    && x.intCompanyCode == companyCode
                    && x.intBranchCode == branchCode
                    && x.intRecordStatusCode == 1))
                {
                    row.intRecordStatusCode = 8;
                    row.dtmLastM = now;
                    row.intAlteredByCode = userCode;
                }

                foreach (var row in db.tblIPDAdmServiceInsts.Where(x =>
                    x.intIPDAdmOrderCode == order.intIPDAdmOrderCode
                    && x.intCompanyCode == companyCode
                    && x.intBranchCode == branchCode
                    && x.intRecordStatusCode == 1))
                {
                    row.intRecordStatusCode = 8;
                    row.dtmLastM = now;
                    row.intAlteredByCode = userCode;
                }
            }
        }

        public static bool HasActiveOrder(dbAMCEntities db, long erAdmissionCode, long patientCode, int branchCode, int companyCode)
        {
            if (db == null || companyCode <= 0) return false;
            return db.tblIPDAdmOrders.Any(x =>
                x.intCompanyCode == companyCode
                && x.intBranchCode == branchCode
                && x.intRecordStatusCode == 1
                && ((erAdmissionCode > 0 && x.intERAdmissionCode == erAdmissionCode)
                    || (patientCode > 0 && x.intPatientCode == patientCode)));
        }

        private static void EnsureExclusiveWithOutcomes(
            dbAMCEntities db,
            OrderTarget target,
            SaveIpdAdmissionOrderInputViewModel input,
            int companyCode,
            int userCode)
        {
            // Reuse outcome exclusivity by saving as a temporary outcome conflict check via public path:
            // Call into EROutcomeFormRepository through a light SaveOutcomeForm-compatible check.
            var probe = new SaveOutcomeFormInputViewModel
            {
                PatientId = target.ERPatientCode.ToString(),
                AdmissionCode = target.ERAdmissionCode > 0 ? (int?)Convert.ToInt32(target.ERAdmissionCode) : null,
                FormType = "admission",
                ReplaceExistingOutcome = input != null && input.ReplaceExistingOutcome
            };

            // Detect conflicts the same way Outcome forms do, including other outcomes.
            var conflicts = new List<string>();
            if (HasOtherOutcome(db, target, companyCode, "referral")) conflicts.Add("Patient Referral");
            if (HasOtherOutcome(db, target, companyCode, "discharge")) conflicts.Add("Discharge Summary");
            if (HasOtherOutcome(db, target, companyCode, "death")) conflicts.Add("Death Certificate");
            if (HasOtherOutcome(db, target, companyCode, "lama")) conflicts.Add("LAMA");

            if (conflicts.Count == 0)
                return;

            if (probe.ReplaceExistingOutcome != true)
            {
                var existingText = string.Join(", ", conflicts);
                throw new InvalidOperationException(
                    "CONFIRM_REPLACE_OUTCOME|" + existingText +
                    " already exists for this admission. Only one outcome form is allowed at a time. " +
                    "Do you want to permanently delete " + existingText + " and continue with IPD Admission Order?");
            }

            // Soft-delete/remove other outcomes by routing through outcome save exclusivity helper.
            // Use death form as a no-op delete trigger is too risky; instead soft-delete via reflection-free SQL deletes:
            SoftDeleteForeignOutcomes(db, target, companyCode, userCode);
        }

        private static bool HasOtherOutcome(dbAMCEntities db, OrderTarget target, int companyCode, string formType)
        {
            switch ((formType ?? string.Empty).ToLowerInvariant())
            {
                case "referral":
                    return db.tblPatientReferrals.Any(x =>
                        x.intCompanyCode == companyCode
                        && x.intRecordStatusCode != 8
                        && ((target.ERAdmissionCode > 0 && x.intERAdmissionCode == target.ERAdmissionCode)
                            || x.intPatientCode == target.PatientCode));
                case "discharge":
                    return db.tblDischargeSummaries.Any(x =>
                        x.intCompanyCode == companyCode
                        && x.intRecordStatusCode != 8
                        && ((target.ERAdmissionCode > 0 && x.intERAdmissionCode == target.ERAdmissionCode)
                            || x.intPatientCode == target.PatientCode));
                case "death":
                    return db.tblDeathCertificates.Any(x =>
                        x.intCompanyCode == companyCode
                        && x.intRecordStatusCode != 8
                        && ((target.ERAdmissionCode > 0 && x.intERAdmissionCode == target.ERAdmissionCode)
                            || x.intPatientCode == target.PatientCode));
                case "lama":
                    return db.tblLAMAs.Any(x =>
                        x.intCompanyCode == companyCode
                        && x.intRecordStatusCode != 8
                        && ((target.ERAdmissionCode > 0 && x.intERAdmissionCode == target.ERAdmissionCode)
                            || x.intPatientCode == target.PatientCode));
                default:
                    return false;
            }
        }

        private static void SoftDeleteForeignOutcomes(dbAMCEntities db, OrderTarget target, int companyCode, int userCode)
        {
            // Match outcome exclusivity: hard-delete other outcome entities when replacing.
            var referrals = db.tblPatientReferrals
                .Where(x => x.intCompanyCode == companyCode
                            && ((target.ERAdmissionCode > 0 && x.intERAdmissionCode == target.ERAdmissionCode)
                                || x.intPatientCode == target.PatientCode))
                .ToList();
            if (referrals.Count > 0) db.tblPatientReferrals.RemoveRange(referrals);

            var dischargeMeds = db.tblERDischargeMedicines
                .Where(x => x.intCompanyCode == companyCode
                            && ((target.ERAdmissionCode > 0 && x.intERAdmissionCode == target.ERAdmissionCode)
                                || x.intPatientCode == target.PatientCode))
                .ToList();
            if (dischargeMeds.Count > 0) db.tblERDischargeMedicines.RemoveRange(dischargeMeds);

            var discharges = db.tblDischargeSummaries
                .Where(x => x.intCompanyCode == companyCode
                            && ((target.ERAdmissionCode > 0 && x.intERAdmissionCode == target.ERAdmissionCode)
                                || x.intPatientCode == target.PatientCode))
                .ToList();
            if (discharges.Count > 0) db.tblDischargeSummaries.RemoveRange(discharges);

            var deaths = db.tblDeathCertificates
                .Where(x => x.intCompanyCode == companyCode
                            && ((target.ERAdmissionCode > 0 && x.intERAdmissionCode == target.ERAdmissionCode)
                                || x.intPatientCode == target.PatientCode))
                .ToList();
            if (deaths.Count > 0) db.tblDeathCertificates.RemoveRange(deaths);

            var lamas = db.tblLAMAs
                .Where(x => x.intCompanyCode == companyCode
                            && ((target.ERAdmissionCode > 0 && x.intERAdmissionCode == target.ERAdmissionCode)
                                || x.intPatientCode == target.PatientCode))
                .ToList();
            if (lamas.Count > 0) db.tblLAMAs.RemoveRange(lamas);

            if (target.ERAdmissionCode > 0)
            {
                db.Database.ExecuteSqlCommand(
                    "EXEC procUpdateERAdmissionForERPortal @bolIsDeathCertificate, @bolIsPatientReferral, @bolIsDichargeSummary, @bolIsLAMA, @intERAdmissionCode, @intUserCode, @intCompanyCode",
                    new SqlParameter("@bolIsDeathCertificate", SqlDbType.Bit) { Value = false },
                    new SqlParameter("@bolIsPatientReferral", SqlDbType.Bit) { Value = false },
                    new SqlParameter("@bolIsDichargeSummary", SqlDbType.Bit) { Value = false },
                    new SqlParameter("@bolIsLAMA", SqlDbType.Bit) { Value = false },
                    new SqlParameter("@intERAdmissionCode", SqlDbType.BigInt) { Value = target.ERAdmissionCode },
                    new SqlParameter("@intUserCode", SqlDbType.Int) { Value = userCode },
                    new SqlParameter("@intCompanyCode", SqlDbType.Int) { Value = companyCode });
            }
        }

        private static void SyncMedicines(
            dbAMCEntities db,
            tblIPDAdmOrder header,
            List<IpdAdmissionMedicineRowViewModel> rows,
            OrderTarget target,
            int userCode,
            DateTime now)
        {
            var incoming = (rows ?? new List<IpdAdmissionMedicineRowViewModel>())
                .Where(x => x != null && (x.ItemCode.HasValue && x.ItemCode.Value > 0
                                          || x.PrescribedDose.HasValue
                                          || x.DoseUnitCode.HasValue
                                          || x.FrequencyCode.HasValue
                                          || x.RouteCode.HasValue
                                          || x.Days.HasValue
                                          || !string.IsNullOrWhiteSpace(x.Instruction)))
                .ToList();

            var existing = db.tblIPDAdmPrescriptionOrders
                .Where(x => x.intIPDAdmOrderCode == header.intIPDAdmOrderCode
                            && x.intCompanyCode == header.intCompanyCode
                            && x.intBranchCode == header.intBranchCode
                            && x.intRecordStatusCode == 1)
                .ToList();

            var keepIds = new HashSet<long>();
            var nextCode = GetNextPrescriptionCode(db, header.intCompanyCode);

            foreach (var row in incoming)
            {
                tblIPDAdmPrescriptionOrder entity = null;
                if (row.Id > 0)
                    entity = existing.FirstOrDefault(x => x.intIPDAdmPrescriptionOrderCode == row.Id);

                if (entity == null)
                {
                    entity = new tblIPDAdmPrescriptionOrder
                    {
                        intIPDAdmPrescriptionOrderCode = nextCode++,
                        intIPDAdmOrderCode = header.intIPDAdmOrderCode,
                        dtmCreated = now,
                        intOwnerCode = userCode,
                        intCreatedByCode = userCode,
                        intRecordStatusCode = 1,
                        intBranchCode = header.intBranchCode,
                        intCompanyCode = header.intCompanyCode
                    };
                    db.tblIPDAdmPrescriptionOrders.Add(entity);
                }

                entity.intConsultantCode = header.intAdmConsultantCode;
                entity.intItemCode = row.ItemCode;
                entity.numPrescribedDose = row.PrescribedDose;
                entity.intPrescribedUnitCode = row.DoseUnitCode;
                entity.intDrugFrequencyCode = row.FrequencyCode;
                entity.intDrugRouteCode = row.RouteCode;
                entity.intDays = row.Days;
                entity.strInstruction = NullIfWhiteSpace(row.Instruction);
                entity.dtmLastM = now;
                entity.intAlteredByCode = userCode;
                entity.intRecordStatusCode = 1;
                keepIds.Add(entity.intIPDAdmPrescriptionOrderCode);
            }

            foreach (var old in existing.Where(x => !keepIds.Contains(x.intIPDAdmPrescriptionOrderCode)))
            {
                old.intRecordStatusCode = 8;
                old.dtmLastM = now;
                old.intAlteredByCode = userCode;
            }
        }

        private static void SyncInvestigations(
            dbAMCEntities db,
            tblIPDAdmOrder header,
            List<IpdAdmissionServiceRowViewModel> rows,
            OrderTarget target,
            int userCode,
            DateTime now)
        {
            var incoming = (rows ?? new List<IpdAdmissionServiceRowViewModel>())
                .Where(x => x != null && x.ServiceCode.HasValue && x.ServiceCode.Value > 0)
                .ToList();

            var existing = db.tblIPDAdmServiceOrders
                .Where(x => x.intIPDAdmOrderCode == header.intIPDAdmOrderCode
                            && x.intCompanyCode == header.intCompanyCode
                            && x.intBranchCode == header.intBranchCode
                            && x.intRecordStatusCode == 1)
                .ToList();

            var keepIds = new HashSet<long>();
            var nextCode = GetNextServiceOrderCode(db, header.intCompanyCode);

            foreach (var row in incoming)
            {
                tblIPDAdmServiceOrder entity = null;
                if (row.Id > 0)
                    entity = existing.FirstOrDefault(x => x.intIPDAdmServiceOrderCode == row.Id);

                if (entity == null)
                {
                    entity = new tblIPDAdmServiceOrder
                    {
                        intIPDAdmServiceOrderCode = nextCode++,
                        intIPDAdmOrderCode = header.intIPDAdmOrderCode,
                        dtmCreated = now,
                        intOwnerCode = userCode,
                        intCreatedByCode = userCode,
                        intRecordStatusCode = 1,
                        intBranchCode = header.intBranchCode,
                        intCompanyCode = header.intCompanyCode
                    };
                    db.tblIPDAdmServiceOrders.Add(entity);
                }

                entity.intConsultantCode = header.intAdmConsultantCode;
                entity.intServiceCode = row.ServiceCode;
                entity.dtmLastM = now;
                entity.intAlteredByCode = userCode;
                entity.intRecordStatusCode = 1;
                keepIds.Add(entity.intIPDAdmServiceOrderCode);
            }

            foreach (var old in existing.Where(x => !keepIds.Contains(x.intIPDAdmServiceOrderCode)))
            {
                old.intRecordStatusCode = 8;
                old.dtmLastM = now;
                old.intAlteredByCode = userCode;
            }
        }

        private static void SyncRoutineServices(
            dbAMCEntities db,
            tblIPDAdmOrder header,
            List<IpdAdmissionRoutineServiceRowViewModel> rows,
            OrderTarget target,
            int userCode,
            DateTime now)
        {
            var incoming = (rows ?? new List<IpdAdmissionRoutineServiceRowViewModel>())
                .Where(x => x != null && ((x.ServiceCode.HasValue && x.ServiceCode.Value > 0)
                                          || !string.IsNullOrWhiteSpace(x.Instruction)))
                .ToList();

            var existing = db.tblIPDAdmServiceInsts
                .Where(x => x.intIPDAdmOrderCode == header.intIPDAdmOrderCode
                            && x.intCompanyCode == header.intCompanyCode
                            && x.intBranchCode == header.intBranchCode
                            && x.intRecordStatusCode == 1)
                .ToList();

            var keepIds = new HashSet<long>();
            var nextCode = GetNextServiceInstCode(db, header.intCompanyCode);

            foreach (var row in incoming)
            {
                tblIPDAdmServiceInst entity = null;
                if (row.Id > 0)
                    entity = existing.FirstOrDefault(x => x.intIPDAdmServiceInstCode == row.Id);

                if (entity == null)
                {
                    entity = new tblIPDAdmServiceInst
                    {
                        intIPDAdmServiceInstCode = nextCode++,
                        intIPDAdmOrderCode = header.intIPDAdmOrderCode,
                        dtmCreated = now,
                        intOwnerCode = userCode,
                        intCreatedByCode = userCode,
                        intRecordStatusCode = 1,
                        intBranchCode = header.intBranchCode,
                        intCompanyCode = header.intCompanyCode
                    };
                    db.tblIPDAdmServiceInsts.Add(entity);
                }

                entity.intConsultantCode = header.intAdmConsultantCode;
                entity.intServiceCode = row.ServiceCode;
                entity.strInstruction = NullIfWhiteSpace(row.Instruction);
                entity.dtmLastM = now;
                entity.intAlteredByCode = userCode;
                entity.intRecordStatusCode = 1;
                keepIds.Add(entity.intIPDAdmServiceInstCode);
            }

            foreach (var old in existing.Where(x => !keepIds.Contains(x.intIPDAdmServiceInstCode)))
            {
                old.intRecordStatusCode = 8;
                old.dtmLastM = now;
                old.intAlteredByCode = userCode;
            }
        }

        private static IQueryable<tblIPDAdmOrder> QueryOrders(
            dbAMCEntities db,
            OrderTarget target,
            int companyCode,
            bool activeOnly)
        {
            var q = db.tblIPDAdmOrders.Where(x =>
                x.intCompanyCode == companyCode
                && x.intBranchCode == target.BranchCode);

            if (activeOnly)
                q = q.Where(x => x.intRecordStatusCode == 1);

            if (target.ERAdmissionCode > 0)
                q = q.Where(x => x.intERAdmissionCode == target.ERAdmissionCode);
            else
                q = q.Where(x => x.intPatientCode == target.PatientCode);

            return q;
        }

        private static long GetNextOrderCode(dbAMCEntities db, int companyCode)
        {
            var maxCode = db.tblIPDAdmOrders
                .Where(x => x.intCompanyCode == companyCode)
                .Select(x => (long?)x.intIPDAdmOrderCode)
                .Max();
            return (maxCode ?? 0L) + 1L;
        }

        private static long GetNextPrescriptionCode(dbAMCEntities db, int companyCode)
        {
            var maxCode = db.tblIPDAdmPrescriptionOrders
                .Where(x => x.intCompanyCode == companyCode)
                .Select(x => (long?)x.intIPDAdmPrescriptionOrderCode)
                .Max();
            return (maxCode ?? 0L) + 1L;
        }

        private static long GetNextServiceOrderCode(dbAMCEntities db, int companyCode)
        {
            var maxCode = db.tblIPDAdmServiceOrders
                .Where(x => x.intCompanyCode == companyCode)
                .Select(x => (long?)x.intIPDAdmServiceOrderCode)
                .Max();
            return (maxCode ?? 0L) + 1L;
        }

        private static long GetNextServiceInstCode(dbAMCEntities db, int companyCode)
        {
            var maxCode = db.tblIPDAdmServiceInsts
                .Where(x => x.intCompanyCode == companyCode)
                .Select(x => (long?)x.intIPDAdmServiceInstCode)
                .Max();
            return (maxCode ?? 0L) + 1L;
        }

        private static string GenerateNextOrderNo(dbAMCEntities db, int companyCode, int branchCode)
        {
            var existing = db.tblIPDAdmOrders
                .Where(x => x.intCompanyCode == companyCode && x.intBranchCode == branchCode)
                .Select(x => x.strIPDAdmOrderNo)
                .ToList();

            var max = 0;
            foreach (var raw in existing)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var parts = raw.Trim().Split('-');
                var suffix = parts.Length > 0 ? parts[parts.Length - 1] : raw;
                int n;
                if (int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out n) && n > max)
                    max = n;
            }

            return "IADO-" + (max + 1);
        }

        private static OrderTarget ResolveTarget(string patientId, int? admissionCode, int companyCode)
        {
            if (string.IsNullOrWhiteSpace(patientId) || companyCode <= 0)
                return OrderTarget.Invalid;

            long erPatientCode;
            if (!long.TryParse(patientId.Trim(), out erPatientCode) || erPatientCode <= 0)
                return OrderTarget.Invalid;

            var erPatient = ERPatientRepository.GetByCode(erPatientCode, companyCode, includeDischarged: true);
            if (erPatient == null)
                return OrderTarget.Invalid;

            long erAdmissionCode = admissionCode.HasValue && admissionCode.Value > 0
                ? admissionCode.Value
                : (erPatient.intERAdmissionCode ?? 0L);

            if (erAdmissionCode <= 0)
            {
                return new OrderTarget
                {
                    ERPatientCode = erPatient.intERPatientCode,
                    BranchCode = erPatient.intBranchCode,
                    CompanyCode = companyCode,
                    PatientCode = erPatient.intERPatientCode,
                    ERAdmissionCode = 0,
                    IsPendingMr = true
                };
            }

            var info = ResolveAdmissionInfo(erAdmissionCode, companyCode);
            var patientCode = info.PatientCode > 0 ? info.PatientCode : erPatient.intERPatientCode;
            var branchCode = info.BranchCode > 0 ? info.BranchCode : erPatient.intBranchCode;

            return new OrderTarget
            {
                ERPatientCode = erPatient.intERPatientCode,
                ERAdmissionCode = erAdmissionCode,
                PatientCode = patientCode,
                BranchCode = branchCode,
                CompanyCode = companyCode,
                IsPendingMr = false
            };
        }

        private static (long PatientCode, int BranchCode) ResolveAdmissionInfo(long erAdmissionCode, int companyCode)
        {
            try
            {
                using (var conn = DBHelper.GetConnection())
                using (var cmd = new SqlCommand(@"
SELECT TOP 1 intPatientCode, intBranchCode
FROM tblERAdmission
WHERE intERAdmissionCode = @admissionCode
  AND intCompanyCode = @companyCode", conn))
                {
                    cmd.Parameters.Add("@admissionCode", SqlDbType.BigInt).Value = erAdmissionCode;
                    cmd.Parameters.Add("@companyCode", SqlDbType.Int).Value = companyCode;
                    conn.Open();
                    using (var rdr = cmd.ExecuteReader())
                    {
                        if (!rdr.Read()) return (0L, 0);
                        var patientCode = rdr["intPatientCode"] == DBNull.Value ? 0L : Convert.ToInt64(rdr["intPatientCode"]);
                        var branchCode = rdr["intBranchCode"] == DBNull.Value ? 0 : Convert.ToInt32(rdr["intBranchCode"]);
                        return (patientCode, branchCode);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(ERIpdAdmissionOrderRepository), nameof(ResolveAdmissionInfo), ex);
                return (0L, 0);
            }
        }

        private static string ResolvePatientDisplayName(OrderTarget target, int companyCode)
        {
            try
            {
                var er = ERPatientRepository.GetByCode(target.ERPatientCode, companyCode, includeDischarged: true);
                if (er != null && !string.IsNullOrWhiteSpace(er.strName))
                    return er.strName.Trim();
            }
            catch
            {
            }

            return string.Empty;
        }

        private static string NullIfWhiteSpace(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private struct OrderTarget
        {
            public static OrderTarget Invalid => new OrderTarget();

            public long ERPatientCode { get; set; }
            public long ERAdmissionCode { get; set; }
            public long PatientCode { get; set; }
            public int BranchCode { get; set; }
            public int CompanyCode { get; set; }
            public bool IsPendingMr { get; set; }

            public bool IsValid =>
                ERPatientCode > 0
                && PatientCode > 0
                && BranchCode > 0
                && CompanyCode > 0
                && !IsPendingMr;
        }
    }
}
