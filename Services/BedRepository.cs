using ERPaperless.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;

namespace ERPaperless.Services
{
    public static class BedRepository
    {
        public static List<LocationCardViewModel> GetBeds(
            int companyCode,
            int branchCode,
            int userCode = 0)
        {
            var result = new List<LocationCardViewModel>();

            try
            {
                using (var conn = DBHelper.GetConnection())
                {
                    conn.Open();

                    using (var cmd = new SqlCommand("procGrdWardBedForERPortal", conn))
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@intBranchCode",  branchCode);
                        cmd.Parameters.AddWithValue("@intCompanyCode", companyCode);

                        using (var rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                var bedCode      = Convert.ToInt32(rdr["intWardBedCode"]);
                                var bedBranch    = Convert.ToInt32(rdr["intBranchCode"]);
                                var bedName      = rdr["strWardBedName"].ToString();
                                var bedStatusRaw = rdr["strWardBedStatusName"]?.ToString()?.Trim()
                                                   ?? string.Empty;
                                var admCode      = rdr["intERAdmissionCode"] != DBNull.Value
                                                   ? Convert.ToInt32(rdr["intERAdmissionCode"]) : 0;

                                string patientId = admCode > 0
                                                   ? admCode.ToString()
                                                   : $"BED-{bedCode}-{bedBranch}";
                                bool showForm    = admCode > 0;

                                string patientName = "-", ageGender = "-",
                                       mrNo = "-",  admNo = "-",
                                       stateClass,  stateLabel;
                                DateTime? admDate = null;

                                // ── Colour / label driven by strWardBedStatusName ──────────────
                                switch (bedStatusRaw.ToUpperInvariant())
                                {
                                    case "OCCUPIED":
                                        var mrNoRaw = rdr["strMrNo"]?.ToString()        ?? string.Empty;
                                        patientName = rdr["strDisplayName"]?.ToString() ?? "-";
                                        ageGender   = BuildAgeGender(
                                                          rdr["age"]?.ToString()           ?? string.Empty,
                                                          rdr["strGenderName"]?.ToString() ?? string.Empty);
                                        mrNo   = string.IsNullOrWhiteSpace(mrNoRaw) ? "PENDING" : mrNoRaw;
                                        admNo  = rdr["strERAdmissionNo"]?.ToString() ?? "-";
                                        if (rdr["dtmERAdmission"] != DBNull.Value)
                                            admDate = Convert.ToDateTime(rdr["dtmERAdmission"]);
                                        stateClass = string.IsNullOrWhiteSpace(mrNoRaw)
                                                     ? "status-orange" : "status-red";
                                        stateLabel = string.IsNullOrWhiteSpace(mrNoRaw)
                                                     ? "Pending MR"    : "Occupied";
                                        if (admCode > 0 && userCode > 0)
                                        {
                                            var erAdmissionPatient = ERPatientRepository.GetOrCreateAdmissionPatient(
                                                admCode, companyCode, userCode);
                                            if (erAdmissionPatient != null)
                                                patientId = erAdmissionPatient.intERPatientCode.ToString();
                                        }
                                        break;

                                    case "UNOCCUPIED":
                                    case "UNASSIGNED":
                                        stateClass = "status-green";
                                        stateLabel = "Available";
                                        break;

                                    case "CLOSED":
                                        stateClass = "status-gray";
                                        stateLabel = "Closed";
                                        break;

                                    case "HOUSEKEEPING":
                                        stateClass = "status-purple";
                                        stateLabel = "Housekeeping";
                                        break;

                                    default:
                                        // Covers Pending MR and any future statuses returned by the procedure.
                                        stateClass = "status-orange";
                                        stateLabel = bedStatusRaw.Length > 0 ? bedStatusRaw : "Pending MR";
                                        showForm   = true;
                                        var erPatient = ERPatientRepository.GetActiveByBed(
                                            bedCode, bedBranch, companyCode);
                                        if (erPatient != null)
                                        {
                                            patientId   = erPatient.intERPatientCode.ToString();
                                            patientName = string.IsNullOrWhiteSpace(erPatient.strName)
                                                ? $"BED#{bedCode}"
                                                : erPatient.strName;
                                        }
                                        else if (userCode > 0)
                                        {
                                            var pendingPatient = ERPatientRepository.GetOrCreatePendingPatient(
                                                bedCode, bedBranch, companyCode, userCode, null);
                                            if (pendingPatient != null)
                                            {
                                                patientId   = pendingPatient.intERPatientCode.ToString();
                                                patientName = string.IsNullOrWhiteSpace(pendingPatient.strName)
                                                    ? $"BED#{bedCode}"
                                                    : pendingPatient.strName;
                                            }
                                        }
                                        break;
                                }

                                result.Add(new LocationCardViewModel
                                {
                                    BedId              = bedCode,
                                    BranchCode         = bedBranch,
                                    AdmissionCode      = admCode,
                                    PatientId          = patientId,
                                    SlotName           = bedName,
                                    IsChair            = bedName.IndexOf("chair",
                                                             StringComparison.OrdinalIgnoreCase) >= 0,
                                    PatientName        = patientName,
                                    AgeGender          = ageGender,
                                    MrNo               = mrNo,
                                    AdmissionNo        = admNo,
                                    AdmissionDate      = admDate,
                                    StateClass         = stateClass,
                                    StateLabel         = stateLabel,
                                    ShowViewFormAction = showForm
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(BedRepository), nameof(GetBeds), ex);
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Load patient data for the ER Form header by intERAdmissionCode.
        // ─────────────────────────────────────────────────────────────────────
        public static LocationCardViewModel GetPatientByAdmissionCode(
            int admissionCode, int companyCode)
        {
            try
            {
                using (var conn = DBHelper.GetConnection())
                {
                    conn.Open();

                    const string sql = @"
                        SELECT  b.intWardBedCode,
                                b.intBranchCode,
                                b.strWardBedName,
                                a.intERAdmissionCode,
                                p.strDisplayName,
                                g.strGenderName,
                                CASE WHEN p.dtBirth IS NOT NULL
                                     THEN dbo.fnAgeInString(p.dtBirth, GETDATE())
                                     ELSE NULL END AS age,
                                p.strMrNo,
                                a.strERAdmissionNo,
                                a.dtmERAdmission
                        FROM    tblERAdmission a
                        JOIN    tblWardBed b
                                  ON  b.intWardBedCode = a.intWardBedCode
                                  AND b.intBranchCode  = a.intBranchCode
                                  AND b.intCompanyCode = a.intCompanyCode
                        LEFT JOIN tblPatient p
                                  ON  p.intPatientCode = a.intPatientCode
                                  AND p.intCompanyCode = a.intCompanyCode
                        LEFT JOIN tblGender g
                                  ON  g.intGenderCode  = p.intGenderCode
                        WHERE   a.intERAdmissionCode = @ID
                          AND   a.intCompanyCode     = @CompanyCode";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@ID",          admissionCode);
                        cmd.Parameters.AddWithValue("@CompanyCode", companyCode);

                        using (var rdr = cmd.ExecuteReader())
                        {
                            if (!rdr.Read()) return null;

                            var mrNoRaw = rdr["strMrNo"]?.ToString()       ?? string.Empty;
                            var age     = rdr["age"]?.ToString()           ?? string.Empty;
                            var gender  = rdr["strGenderName"]?.ToString() ?? string.Empty;

                            return new LocationCardViewModel
                            {
                                BedId         = Convert.ToInt32(rdr["intWardBedCode"]),
                                BranchCode    = Convert.ToInt32(rdr["intBranchCode"]),
                                PatientId     = rdr["intERAdmissionCode"].ToString(),
                                SlotName      = rdr["strWardBedName"].ToString(),
                                PatientName   = rdr["strDisplayName"]?.ToString()    ?? "-",
                                AgeGender     = BuildAgeGender(age, gender),
                                MrNo          = string.IsNullOrWhiteSpace(mrNoRaw) ? "PENDING" : mrNoRaw,
                                AdmissionNo   = rdr["strERAdmissionNo"]?.ToString() ?? "-",
                                AdmissionDate = rdr["dtmERAdmission"] != DBNull.Value
                                                    ? Convert.ToDateTime(rdr["dtmERAdmission"])
                                                    : (DateTime?)null,
                                StateClass         = "status-red",
                                StateLabel         = "Occupied",
                                ShowViewFormAction = true
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(BedRepository), nameof(GetPatientByAdmissionCode), ex);
                return null;
            }
        }


        public static bool AssignPatient(
            AssignPatientInputViewModel model,
            int companyCode,
            int userCode,
            string assignedByName)
        {
            if (model == null || model.BedId <= 0 ||
                string.IsNullOrWhiteSpace(model.PatientName))
                return false;

            try
            {
                using (var conn = DBHelper.GetConnection())
                {
                    conn.Open();

                    // Step 1: deactivate any current active assignment on this bed
                    const string deactivate = @"
                        UPDATE tblERBedAssignment
                        SET    intRecordStatusCode = 0,
                               dtmLastM            = GETDATE(),
                               intAlteredByCode    = @UserCode
                        WHERE  intWardBedCode      = @BedId
                          AND  intBranchCode       = @BranchCode
                          AND  intCompanyCode      = @CompanyCode
                          AND  intRecordStatusCode = 1";

                    using (var cmd = new SqlCommand(deactivate, conn))
                    {
                        cmd.Parameters.AddWithValue("@BedId",       model.BedId);
                        cmd.Parameters.AddWithValue("@BranchCode",  model.BranchCode);
                        cmd.Parameters.AddWithValue("@CompanyCode", companyCode);
                        cmd.Parameters.AddWithValue("@UserCode",    userCode);
                        cmd.ExecuteNonQuery();
                    }

                    // Step 2: insert new assignment
                    const string insert = @"
                        INSERT INTO tblERBedAssignment
                            (intCompanyCode, intBranchCode, intWardBedCode,
                             strPatientName, strGender, strAge, strMrNo,
                             intERAdmissionNo, dtmAdmission, intAssignedBy,
                             dtmCreated, intOwnerCode, intCreatedByCode,
                             intRecordStatusCode)
                        VALUES
                            (@CompanyCode, @BranchCode, @BedId,
                             @PatientName, @Gender, @Age, @MrNo,
                             NULL, GETDATE(), @UserCode,
                             GETDATE(), @UserCode, @UserCode,
                             1)";

                    using (var cmd = new SqlCommand(insert, conn))
                    {
                        cmd.Parameters.AddWithValue("@CompanyCode",  companyCode);
                        cmd.Parameters.AddWithValue("@BranchCode",   model.BranchCode);
                        cmd.Parameters.AddWithValue("@BedId",        model.BedId);
                        cmd.Parameters.AddWithValue("@PatientName",
                            (object)model.PatientName ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Gender",
                            string.IsNullOrWhiteSpace(model.Gender)
                                ? (object)DBNull.Value : model.Gender);
                        cmd.Parameters.AddWithValue("@Age",
                            string.IsNullOrWhiteSpace(model.Age)
                                ? (object)DBNull.Value : model.Age);
                        cmd.Parameters.AddWithValue("@MrNo",
                            string.IsNullOrWhiteSpace(model.MrNo)
                                ? (object)DBNull.Value : model.MrNo);
                        cmd.Parameters.AddWithValue("@UserCode", userCode);
                        cmd.ExecuteNonQuery();
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(BedRepository), nameof(AssignPatient), ex);
                return false;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Update ward bed status through procUpdateWardBedStatusForERPortal.
        // ─────────────────────────────────────────────────────────────────────
        public static bool MarkBedStatus(
            int bedCode,
            WardBedStatus status,
            int companyCode,
            int userCode)
        {
            try
            {
                using (var conn = DBHelper.GetConnection())
                {
                    conn.Open();

                    using (var cmd = new SqlCommand("procUpdateWardBedStatusForERPortal", conn))
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@intWardBedStatusCode", (int)status);
                        cmd.Parameters.AddWithValue("@intWardBedCode",       bedCode);
                        cmd.Parameters.AddWithValue("@intUserCode",          userCode);
                        cmd.Parameters.AddWithValue("@intCompanyCode",       companyCode);

                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(BedRepository), nameof(MarkBedStatus), ex);
                return false;
            }
        }

        public static bool MarkBedAsPendingMR(
            int bedCode, int companyCode, int userCode)
        {
            return MarkBedStatus(bedCode, WardBedStatus.PendingMR, companyCode, userCode);
        }

        public static bool MarkBedAsUnoccupied(
            int bedCode, int companyCode, int userCode)
        {
            return MarkBedStatus(bedCode, WardBedStatus.Unoccupied, companyCode, userCode);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Ward bed display name (e.g. ER # 6) from intWardBedCode when no admission exists.
        /// </summary>
        public static string GetBedSlotName(int bedCode, int branchCode, int companyCode, int userCode = 0)
        {
            if (bedCode <= 0)
                return null;

            var bed = GetBeds(companyCode, branchCode, userCode)
                .FirstOrDefault(x => x.BedId == bedCode);

            return string.IsNullOrWhiteSpace(bed?.SlotName) ? null : bed.SlotName;
        }

        /// <summary>
        /// Bed label for UI: admission slot name, else ward bed name, else BED-{code}.
        /// </summary>
        public static string ResolveBedDisplayName(
            LocationCardViewModel admission,
            int bedCode,
            int branchCode,
            int companyCode,
            int userCode = 0)
        {
            if (!string.IsNullOrWhiteSpace(admission?.SlotName))
                return admission.SlotName;

            var slotName = GetBedSlotName(bedCode, branchCode, companyCode, userCode);
            if (!string.IsNullOrWhiteSpace(slotName))
                return slotName;

            return bedCode > 0 ? $"BED-{bedCode}" : "-";
        }

        /// <summary>
        /// Parses "BED-{bedCode}-{branchCode}" token.
        /// Returns (bedCode, branchCode) or (0,0) on failure.
        /// </summary>
        public static (int BedId, int BranchCode) ParseBedToken(string token)
        {
            if (!string.IsNullOrWhiteSpace(token) &&
                token.StartsWith("BED-", StringComparison.OrdinalIgnoreCase))
            {
                var parts = token.Substring(4).Split('-');
                if (parts.Length == 2 &&
                    int.TryParse(parts[0], out var bid) &&
                    int.TryParse(parts[1], out var branch))
                    return (bid, branch);
            }
            return (0, 0);
        }

        private static string BuildAgeGender(string age, string gender)
        {
            var s = $"{age} / {gender}".Trim().Trim('/').Trim();
            return string.IsNullOrWhiteSpace(s) ? "-" : s;
        }
    }
}
