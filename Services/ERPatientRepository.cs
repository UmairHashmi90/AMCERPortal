using ERPaperless.Models;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace ERPaperless.Services
{
    public static class ERPatientRepository
    {
        public static string LastError { get; private set; }

        /// <summary>
        /// Resolves an active ER patient from a route token (intERPatientCode,
        /// intERAdmissionCode, or BED-{bedCode}-{branchCode}). Creates a pending
        /// patient when allowed and none exists yet.
        /// </summary>
        public static tblERPatient ResolveActivePatient(
            string patientId,
            int companyCode,
            int userCode,
            bool createIfMissing = true)
        {
            LastError = null;

            if (string.IsNullOrWhiteSpace(patientId))
            {
                LastError = "Patient identifier is missing.";
                return null;
            }

            var trimmed = patientId.Trim();

            if (trimmed.StartsWith("BED-", StringComparison.OrdinalIgnoreCase))
            {
                var (bedId, branchCode) = BedRepository.ParseBedToken(trimmed);
                if (bedId <= 0 || branchCode <= 0)
                {
                    LastError = "Invalid bed reference.";
                    return null;
                }

                var byBed = GetActiveByBed(bedId, branchCode, companyCode);
                if (byBed != null) return byBed;

                if (createIfMissing && userCode > 0)
                    return GetOrCreatePendingPatient(bedId, branchCode, companyCode, userCode, null);

                LastError = "No active ER patient on this bed.";
                return null;
            }

            if (!long.TryParse(trimmed, out var numericId) || numericId <= 0)
            {
                LastError = "Invalid patient identifier.";
                return null;
            }

            var byCode = GetByCode(numericId, companyCode, includeDischarged: true);
            if (byCode != null) return byCode;

            if (createIfMissing && userCode > 0)
            {
                var byAdmission = GetOrCreateAdmissionPatient((int)numericId, companyCode, userCode);
                if (byAdmission != null) return byAdmission;
            }

            LastError = LastError ?? "Patient record not found or has been discharged.";
            return null;
        }

        public static tblERPatient GetByCode(long erPatientCode, int companyCode, bool includeDischarged = false)
        {
            if (erPatientCode <= 0 || companyCode <= 0) return null;

            try
            {
                using (var db = dbAMCEntities.Create())
                {
                    var query = db.tblERPatients.Where(p =>
                        p.intERPatientCode == erPatientCode &&
                        p.intCompanyCode == companyCode &&
                        (p.intRecordStatusCode == 1 || p.intRecordStatusCode == 2));
                    if (!includeDischarged)
                        query = query.Where(p => p.bolIsDischarge != true);
                    return query.FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(ERPatientRepository), nameof(GetByCode), ex);
                return null;
            }
        }

        public static tblERPatient GetActiveByBed(int bedCode, int branchCode, int companyCode)
        {
            if (bedCode <= 0 || branchCode <= 0 || companyCode <= 0) return null;

            try
            {
                using (var db = dbAMCEntities.Create())
                {
                    return db.tblERPatients
                        .Where(p => p.intWardBedCode == bedCode &&
                                    p.intBranchCode == branchCode &&
                                    p.intCompanyCode == companyCode &&
                                    p.bolIsDischarge != true &&
                                    (p.intRecordStatusCode == 1 || p.intRecordStatusCode == 2))
                        .OrderByDescending(p => p.dtmAdmission)
                        .FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(ERPatientRepository), nameof(GetActiveByBed), ex);
                return null;
            }
        }

        public static tblERPatient GetOrCreatePendingPatient(
            int bedCode,
            int branchCode,
            int companyCode,
            int userCode,
            string patientName)
        {
            LastError = null;

            if (bedCode <= 0 || branchCode <= 0 || companyCode <= 0 || userCode <= 0)
            {
                LastError = "Invalid bed, branch, company, or user.";
                return null;
            }

            try
            {
                using (var db = dbAMCEntities.Create())
                {
                    var existing = db.tblERPatients
                        .Where(p => p.intWardBedCode == bedCode &&
                                    p.intBranchCode == branchCode &&
                                    p.intCompanyCode == companyCode &&
                                    p.intERAdmissionCode == null &&
                                    p.bolIsDischarge != true &&
                                    (p.intRecordStatusCode == 1 || p.intRecordStatusCode == 2))
                        .OrderByDescending(p => p.dtmAdmission)
                        .FirstOrDefault();

                    var bedName = BedRepository.GetWardBedName(bedCode, branchCode, companyCode)
                                  ?? (bedCode > 0 ? ("BED-" + bedCode) : "Pending");
                    var displayName = !string.IsNullOrWhiteSpace(patientName)
                        ? patientName.Trim()
                        : bedName;

                    if (existing != null)
                    {
                        // Only overwrite name when caller supplied one, or existing is empty / old BED# placeholder.
                        if (!string.IsNullOrWhiteSpace(patientName)
                            || IsPlaceholderPendingName(existing.strName, bedCode))
                        {
                            existing.strName = displayName;
                        }
                        existing.dtmLastM = DateTime.Now;
                        existing.intAlteredByCode = userCode;
                        db.SaveChanges();
                        return existing;
                    }

                    var now = DateTime.Now;
                    var patient = new tblERPatient
                    {
                        intWardBedCode = bedCode,
                        intERAdmissionCode = null,
                        dtmAdmission = now,
                        strName = displayName,
                        bolIsDischarge = false,
                        dtmCreated = now,
                        intOwnerCode = userCode,
                        intCreatedByCode = userCode,
                        intRecordStatusCode = 1,
                        intBranchCode = branchCode,
                        intCompanyCode = companyCode
                    };

                    db.tblERPatients.Add(patient);
                    db.SaveChanges();
                    return patient;
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(ERPatientRepository), nameof(GetOrCreatePendingPatient), ex);
                LastError = ex.GetBaseException().Message;
                return null;
            }
        }

        private static bool IsPlaceholderPendingName(string name, int bedCode)
        {
            if (string.IsNullOrWhiteSpace(name) || name == "-")
                return true;

            var trimmed = name.Trim();
            if (string.Equals(trimmed, "BED#" + bedCode, StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(trimmed, "BED-" + bedCode, StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }

        public static tblERPatient GetOrCreateAdmissionPatient(
            int admissionCode,
            int companyCode,
            int userCode)
        {
            LastError = null;

            if (admissionCode <= 0 || companyCode <= 0) return null;

            try
            {
                using (var db = dbAMCEntities.Create())
                {
                    var existing = db.tblERPatients
                        .Where(p => p.intERAdmissionCode == admissionCode &&
                                    p.intCompanyCode == companyCode &&
                                    (p.intRecordStatusCode == 1 || p.intRecordStatusCode == 2))
                        .OrderBy(p => p.bolIsDischarge == true)
                        .ThenByDescending(p => p.intERPatientCode)
                        .FirstOrDefault();
                    if (existing != null) return existing;
                }

                var admission = BedRepository.GetPatientByAdmissionCode(admissionCode, companyCode);
                if (admission == null)
                {
                    LastError = "Admission record not found.";
                    return null;
                }

                using (var db = dbAMCEntities.Create())
                {
                    var now = DateTime.Now;
                    var patient = new tblERPatient
                    {
                        intWardBedCode = admission.BedId,
                        intERAdmissionCode = admissionCode,
                        dtmAdmission = admission.AdmissionDate ?? now,
                        strName = admission.PatientName,
                        bolIsDischarge = false,
                        dtmCreated = now,
                        intOwnerCode = userCode,
                        intCreatedByCode = userCode,
                        intRecordStatusCode = 1,
                        intBranchCode = admission.BranchCode,
                        intCompanyCode = companyCode
                    };

                    db.tblERPatients.Add(patient);
                    db.SaveChanges();
                    return patient;
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(ERPatientRepository), nameof(GetOrCreateAdmissionPatient), ex);
                LastError = ex.GetBaseException().Message;
                return null;
            }
        }

        public static bool AttachToAdmission(
            long erPatientCode,
            long admissionCode,
            int companyCode,
            int userCode)
        {
            LastError = null;

            try
            {
                using (var db = dbAMCEntities.Create())
                {
                    var patient = db.tblERPatients.FirstOrDefault(p =>
                        p.intERPatientCode == erPatientCode &&
                        p.intCompanyCode == companyCode &&
                        p.bolIsDischarge != true &&
                        (p.intRecordStatusCode == 1 || p.intRecordStatusCode == 2));

                    if (patient == null) return false;

                    patient.intERAdmissionCode = admissionCode;
                    patient.dtmLastM = DateTime.Now;
                    patient.intAlteredByCode = userCode;
                    return db.SaveChanges() > 0;
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(ERPatientRepository), nameof(AttachToAdmission), ex);
                LastError = ex.GetBaseException().Message;
                return false;
            }
        }

        public static bool ChangeBed(
            long erPatientCode,
            int newBedCode,
            int newBranchCode,
            int companyCode,
            int userCode,
            out int oldBedCode,
            out WardBedStatus newBedStatus)
        {
            LastError = null;
            oldBedCode = 0;
            newBedStatus = WardBedStatus.PendingMR;

            if (erPatientCode <= 0 || newBedCode <= 0 || newBranchCode <= 0 ||
                companyCode <= 0 || userCode <= 0)
            {
                LastError = "Invalid patient or bed selection.";
                return false;
            }

            try
            {
                using (var db = dbAMCEntities.Create())
                {
                    var patient = db.tblERPatients.FirstOrDefault(p =>
                        p.intERPatientCode == erPatientCode &&
                        p.intCompanyCode == companyCode &&
                        p.bolIsDischarge != true &&
                        (p.intRecordStatusCode == 1 || p.intRecordStatusCode == 2));

                    if (patient == null)
                    {
                        LastError = "Patient not found or already discharged.";
                        return false;
                    }

                    oldBedCode = (int)patient.intWardBedCode;
                    if (oldBedCode == newBedCode && patient.intBranchCode == newBranchCode)
                    {
                        LastError = "Patient is already on the selected bed.";
                        return false;
                    }

                    newBedStatus = patient.intERAdmissionCode == null
                        ? WardBedStatus.PendingMR
                        : WardBedStatus.Occupied;

                    patient.intWardBedCode = newBedCode;
                    patient.intBranchCode = newBranchCode;
                    patient.dtmLastM = DateTime.Now;
                    patient.intAlteredByCode = userCode;

                    return db.SaveChanges() > 0;
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(ERPatientRepository), nameof(ChangeBed), ex);
                LastError = ex.GetBaseException().Message;
                return false;
            }
        }

        private static string BuildDisplayName(string patientName, int bedCode, int branchCode)
        {
            // Kept for callers; pending MR default is resolved in GetOrCreatePendingPatient via bed name.
            return string.IsNullOrWhiteSpace(patientName)
                ? (bedCode > 0 ? ("BED-" + bedCode) : "Pending")
                : patientName.Trim();
        }

        /// <summary>
        /// Returns canonical triage color (Red/Orange/Yellow/Green/Blue) or null when invalid/empty.
        /// </summary>
        public static string NormalizeTriageColor(string triageColor)
        {
            if (string.IsNullOrWhiteSpace(triageColor)) return null;

            switch (triageColor.Trim().ToLowerInvariant())
            {
                case "red": return "Red";
                case "orange": return "Orange";
                case "yellow": return "Yellow";
                case "green": return "Green";
                case "blue": return "Blue";
                default: return null;
            }
        }

        /// <summary>
        /// Maps a stored triage color to a location-card CSS class. Returns null when not set / invalid.
        /// </summary>
        public static string TryGetTriageStateClass(string triageColor)
        {
            switch (NormalizeTriageColor(triageColor))
            {
                case "Red": return "status-red";
                case "Orange": return "status-orange";
                case "Yellow": return "status-yellow";
                case "Green": return "status-green";
                case "Blue": return "status-blue";
                default: return null;
            }
        }

        /// <summary>
        /// Billing cancel of a Pending MR patient: soft-delete tblERPatient (status 8)
        /// and set the matching tblWardBed to Unoccupied (status 1).
        /// </summary>
        public static bool CancelPendingMr(int bedCode, int branchCode, int companyCode, int userCode)
        {
            LastError = null;

            if (bedCode <= 0 || branchCode <= 0 || companyCode <= 0 || userCode <= 0)
            {
                LastError = "Invalid bed, branch, company, or user.";
                return false;
            }

            try
            {
                using (var conn = DBHelper.GetConnection())
                {
                    conn.Open();
                    using (var tx = conn.BeginTransaction())
                    {
                        int patientRows;
                        using (var cmd = new SqlCommand(@"
UPDATE tblERPatient
SET intRecordStatusCode = 8,
    dtmLastM = GETDATE(),
    intAlteredByCode = @userCode
WHERE intWardBedCode = @bedCode
  AND intBranchCode = @branchCode
  AND intCompanyCode = @companyCode
  AND intRecordStatusCode IN (1, 2)
  AND intERAdmissionCode IS NULL", conn, tx))
                        {
                            cmd.Parameters.Add("@userCode", SqlDbType.Int).Value = userCode;
                            cmd.Parameters.Add("@bedCode", SqlDbType.Int).Value = bedCode;
                            cmd.Parameters.Add("@branchCode", SqlDbType.Int).Value = branchCode;
                            cmd.Parameters.Add("@companyCode", SqlDbType.Int).Value = companyCode;
                            patientRows = cmd.ExecuteNonQuery();
                        }

                        if (patientRows <= 0)
                        {
                            tx.Rollback();
                            LastError = "No active Pending MR patient found on this bed.";
                            return false;
                        }

                        int bedRows;
                        using (var cmd = new SqlCommand(@"
UPDATE tblWardBed
SET intWardBedStatusCode = 1
WHERE intWardBedCode = @bedCode
  AND intBranchCode = @branchCode
  AND intCompanyCode = @companyCode", conn, tx))
                        {
                            cmd.Parameters.Add("@bedCode", SqlDbType.Int).Value = bedCode;
                            cmd.Parameters.Add("@branchCode", SqlDbType.Int).Value = branchCode;
                            cmd.Parameters.Add("@companyCode", SqlDbType.Int).Value = companyCode;
                            bedRows = cmd.ExecuteNonQuery();
                        }

                        if (bedRows <= 0)
                        {
                            tx.Rollback();
                            LastError = "Patient was found but the bed status could not be updated.";
                            return false;
                        }

                        tx.Commit();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(ERPatientRepository), nameof(CancelPendingMr), ex);
                LastError = ex.GetBaseException().Message;
                return false;
            }
        }
    }
}
