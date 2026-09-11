using ERPaperless.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;

namespace ERPaperless.Services
{
    public static class BedRepository
    {
        private sealed class BedRowDraft
        {
            public LocationCardViewModel Card { get; set; }
            public bool EnrichAdmission { get; set; }
            public bool EnrichPending { get; set; }
        }

        public static List<LocationCardViewModel> GetBeds(
            int companyCode,
            int branchCode,
            int userCode = 0)
        {
            var drafts = new List<BedRowDraft>();

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

                        // Read all proc rows first. Do not call EF while the reader is open —
                        // that can fail the whole bed list and blank patient info everywhere.
                        using (var rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                var bedCode      = Convert.ToInt32(rdr["intWardBedCode"]);
                                var bedBranch    = Convert.ToInt32(rdr["intBranchCode"]);
                                var bedName      = ReadDbString(rdr, "strWardBedName") ?? string.Empty;
                                var bedStatusRaw = (ReadDbString(rdr, "strWardBedStatusName") ?? string.Empty).Trim();
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
                                var enrichAdmission = false;
                                var enrichPending = false;

                                // ── Colour / label driven by strWardBedStatusName ──────────────
                                switch (bedStatusRaw.ToUpperInvariant())
                                {
                                    case "OCCUPIED":
                                        ApplyAdmissionPatientFields(
                                            rdr,
                                            out patientName,
                                            out ageGender,
                                            out mrNo,
                                            out admNo,
                                            out admDate);
                                        stateClass = string.Equals(mrNo, "PENDING", StringComparison.OrdinalIgnoreCase)
                                                     ? "status-orange" : "status-red";
                                        stateLabel = string.Equals(mrNo, "PENDING", StringComparison.OrdinalIgnoreCase)
                                                     ? "Pending MR" : "Occupied";
                                        enrichAdmission = admCode > 0 && userCode > 0;
                                        break;

                                    case "DISCHARGE START":
                                    case "DISCHARGESTARTED":
                                    case "DISCHARGE STARTED":
                                        // Discharge finalize started — patient still on bed; show full admission info.
                                        ApplyAdmissionPatientFields(
                                            rdr,
                                            out patientName,
                                            out ageGender,
                                            out mrNo,
                                            out admNo,
                                            out admDate);
                                        stateClass = "status-orange";
                                        stateLabel = "Discharge Start";
                                        showForm = true;
                                        enrichAdmission = admCode > 0 && userCode > 0;
                                        if (!enrichAdmission)
                                            enrichPending = true;
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
                                        // Pending MR and any other in-use status — still show admission fields when present.
                                        stateClass = "status-orange";
                                        stateLabel = bedStatusRaw.Length > 0 ? bedStatusRaw : "Pending MR";
                                        showForm   = true;
                                        if (admCode > 0)
                                        {
                                            ApplyAdmissionPatientFields(
                                                rdr,
                                                out patientName,
                                                out ageGender,
                                                out mrNo,
                                                out admNo,
                                                out admDate);
                                            enrichAdmission = userCode > 0;
                                        }
                                        else
                                        {
                                            enrichPending = true;
                                        }
                                        break;
                                }

                                drafts.Add(new BedRowDraft
                                {
                                    EnrichAdmission = enrichAdmission,
                                    EnrichPending = enrichPending,
                                    Card = new LocationCardViewModel
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
                                    }
                                });
                            }
                        }
                    }
                }

                // Enrich triage / pending patient ids after the SQL reader is fully closed.
                foreach (var draft in drafts)
                {
                    var bed = draft.Card;
                    string triageColor = null;

                    try
                    {
                        if (draft.EnrichAdmission)
                        {
                            var erAdmissionPatient = ERPatientRepository.GetOrCreateAdmissionPatient(
                                bed.AdmissionCode, companyCode, userCode);
                            if (erAdmissionPatient != null)
                            {
                                bed.PatientId = erAdmissionPatient.intERPatientCode.ToString();
                                triageColor = erAdmissionPatient.strTriageColor;
                                if (string.IsNullOrWhiteSpace(bed.PatientName) || bed.PatientName == "-")
                                {
                                    bed.PatientName = FirstNonEmpty(
                                        erAdmissionPatient.strName,
                                        bed.PatientName,
                                        $"BED#{bed.BedId}");
                                }
                            }

                            if (bed.AdmissionCode > 0
                                && (string.IsNullOrWhiteSpace(bed.MrNo)
                                    || bed.MrNo == "-"
                                    || string.IsNullOrWhiteSpace(bed.AdmissionNo)
                                    || bed.AdmissionNo == "-"
                                    || !bed.AdmissionDate.HasValue
                                    || string.IsNullOrWhiteSpace(bed.AgeGender)
                                    || bed.AgeGender == "-"))
                            {
                                var admission = GetPatientByAdmissionCode(bed.AdmissionCode, companyCode);
                                if (admission != null)
                                    ApplyAdmissionCardOverlay(bed, admission);
                            }
                        }
                        else if (draft.EnrichPending)
                        {
                            var erPatient = ERPatientRepository.GetActiveByBed(
                                bed.BedId, bed.BranchCode, companyCode);
                            if (erPatient != null)
                            {
                                bed.PatientId = erPatient.intERPatientCode.ToString();
                                bed.PatientName = FirstNonEmpty(
                                    erPatient.strName,
                                    bed.PatientName,
                                    $"BED#{bed.BedId}");
                                triageColor = erPatient.strTriageColor;

                                // Pending / Discharge Start beds may still have an admission —
                                // backfill MR / admission details when the card is incomplete.
                                if (erPatient.intERAdmissionCode.HasValue
                                    && erPatient.intERAdmissionCode.Value > 0
                                    && (bed.AdmissionCode <= 0
                                        || string.IsNullOrWhiteSpace(bed.MrNo)
                                        || bed.MrNo == "-"
                                        || bed.MrNo == "PENDING"
                                        || string.IsNullOrWhiteSpace(bed.AdmissionNo)
                                        || bed.AdmissionNo == "-"
                                        || !bed.AdmissionDate.HasValue
                                        || string.IsNullOrWhiteSpace(bed.AgeGender)
                                        || bed.AgeGender == "-"))
                                {
                                    bed.AdmissionCode = (int)erPatient.intERAdmissionCode.Value;
                                    var admission = GetPatientByAdmissionCode(
                                        bed.AdmissionCode, companyCode);
                                    if (admission != null)
                                        ApplyAdmissionCardOverlay(bed, admission);
                                }
                            }
                            else if (userCode > 0)
                            {
                                var pendingPatient = ERPatientRepository.GetOrCreatePendingPatient(
                                    bed.BedId, bed.BranchCode, companyCode, userCode, null);
                                if (pendingPatient != null)
                                {
                                    bed.PatientId = pendingPatient.intERPatientCode.ToString();
                                    bed.PatientName = FirstNonEmpty(
                                        pendingPatient.strName,
                                        bed.SlotName,
                                        bed.PatientName,
                                        $"BED-{bed.BedId}");
                                    triageColor = pendingPatient.strTriageColor;
                                }
                            }
                        }
                    }
                    catch (Exception enrichEx)
                    {
                        ErrorLogging.Log(nameof(BedRepository), nameof(GetBeds) + ".Enrich", enrichEx);
                    }

                    bed.CardBackgroundClass = ERPatientRepository.TryGetTriageStateClass(triageColor);
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(BedRepository), nameof(GetBeds), ex);
            }

            return drafts.Select(d => d.Card).ToList();
        }

        public static List<LocationCardViewModel> GetDischargedPatients(
            int companyCode,
            int branchCode)
        {
            var cards = new List<LocationCardViewModel>();

            try
            {
                using (var conn = DBHelper.GetConnection())
                {
                    conn.Open();

                    using (var cmd = new SqlCommand("procGrdERDischargedPatientForERPortal", conn))
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@intBranchCode", branchCode);
                        cmd.Parameters.AddWithValue("@intCompanyCode", companyCode);

                        using (var rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                var bedCode = Convert.ToInt32(rdr["intWardBedCode"]);
                                var bedBranch = Convert.ToInt32(rdr["intBranchCode"]);
                                var bedName = ReadDbString(rdr, "strWardBedName") ?? string.Empty;
                                var admCode = rdr["intERAdmissionCode"] != DBNull.Value
                                    ? Convert.ToInt32(rdr["intERAdmissionCode"]) : 0;

                                string patientName, ageGender, mrNo, admNo;
                                DateTime? admDate;
                                ApplyAdmissionPatientFields(
                                    rdr,
                                    out patientName,
                                    out ageGender,
                                    out mrNo,
                                    out admNo,
                                    out admDate);

                                cards.Add(new LocationCardViewModel
                                {
                                    BedId = bedCode,
                                    BranchCode = bedBranch,
                                    AdmissionCode = admCode,
                                    PatientId = admCode > 0
                                        ? admCode.ToString()
                                        : ("BED-" + bedCode + "-" + bedBranch),
                                    SlotName = bedName,
                                    IsChair = bedName.IndexOf("chair", StringComparison.OrdinalIgnoreCase) >= 0,
                                    PatientName = patientName,
                                    AgeGender = ageGender,
                                    MrNo = mrNo,
                                    AdmissionNo = admNo,
                                    AdmissionDate = admDate,
                                    StateClass = "status-gray",
                                    StateLabel = "Discharged",
                                    ShowViewFormAction = true
                                });
                            }
                        }
                    }
                }

                var admissionCodes = cards
                    .Where(c => c.AdmissionCode > 0)
                    .Select(c => (long)c.AdmissionCode)
                    .Distinct()
                    .ToList();

                if (admissionCodes.Count == 0 || companyCode <= 0)
                    return cards;

                using (var db = dbAMCEntities.Create())
                {
                    var erRows = db.tblERPatients
                        .Where(p => p.intCompanyCode == companyCode
                                    && (p.intRecordStatusCode == 1 || p.intRecordStatusCode == 2)
                                    && p.intERAdmissionCode != null
                                    && admissionCodes.Contains(p.intERAdmissionCode.Value))
                        .Select(p => new
                        {
                            p.intERAdmissionCode,
                            p.intERPatientCode,
                            p.strTriageColor,
                            p.strName,
                            p.bolIsDischarge
                        })
                        .ToList();

                    var byAdmission = erRows
                        .GroupBy(x => x.intERAdmissionCode.Value)
                        .ToDictionary(
                            g => g.Key,
                            g => g.OrderByDescending(x => x.bolIsDischarge == true)
                                  .ThenByDescending(x => x.intERPatientCode)
                                  .First());

                    foreach (var card in cards)
                    {
                        if (card.AdmissionCode <= 0)
                            continue;

                        var row = byAdmission.ContainsKey(card.AdmissionCode)
                            ? byAdmission[card.AdmissionCode]
                            : null;
                        if (row == null)
                            continue;

                        card.PatientId = row.intERPatientCode.ToString();
                        card.CardBackgroundClass = ERPatientRepository.TryGetTriageStateClass(row.strTriageColor);
                        if (string.IsNullOrWhiteSpace(card.PatientName) || card.PatientName == "-")
                            card.PatientName = FirstNonEmpty(row.strName, card.PatientName, "-");
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(BedRepository), nameof(GetDischargedPatients), ex);
            }

            return cards;
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

                            var mrNoRaw = ReadDbString(rdr, "strMrNo") ?? string.Empty;
                            var age     = ReadDbString(rdr, "age") ?? string.Empty;
                            var gender  = ReadDbString(rdr, "strGenderName") ?? string.Empty;
                            var admCode = Convert.ToInt32(rdr["intERAdmissionCode"]);

                            return new LocationCardViewModel
                            {
                                BedId         = Convert.ToInt32(rdr["intWardBedCode"]),
                                BranchCode    = Convert.ToInt32(rdr["intBranchCode"]),
                                AdmissionCode = admCode,
                                PatientId     = admCode.ToString(),
                                SlotName      = ReadDbString(rdr, "strWardBedName") ?? string.Empty,
                                PatientName   = FirstNonEmpty(ReadDbString(rdr, "strDisplayName"), "-"),
                                AgeGender     = BuildAgeGender(age, gender),
                                MrNo          = string.IsNullOrWhiteSpace(mrNoRaw) ? "PENDING" : mrNoRaw,
                                AdmissionNo   = FirstNonEmpty(ReadDbString(rdr, "strERAdmissionNo"), "-"),
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
            // Prefer lightweight lookup — avoids recursion through GetBeds enrichment.
            var direct = GetWardBedName(bedCode, branchCode, companyCode);
            if (!string.IsNullOrWhiteSpace(direct))
                return direct;

            if (bedCode <= 0)
                return null;

            var bed = GetBeds(companyCode, branchCode, userCode)
                .FirstOrDefault(x => x.BedId == bedCode);

            return string.IsNullOrWhiteSpace(bed?.SlotName) ? null : bed.SlotName;
        }

        /// <summary>
        /// Reads strWardBedName directly from tblWardBed (no GetBeds / enrich side effects).
        /// </summary>
        public static string GetWardBedName(int bedCode, int branchCode, int companyCode)
        {
            if (bedCode <= 0 || branchCode <= 0)
                return null;

            try
            {
                using (var conn = DBHelper.GetConnection())
                using (var cmd = new SqlCommand(@"
SELECT TOP 1 strWardBedName
FROM tblWardBed
WHERE intWardBedCode = @bedCode
  AND intBranchCode = @branchCode", conn))
                {
                    cmd.Parameters.AddWithValue("@bedCode", bedCode);
                    cmd.Parameters.AddWithValue("@branchCode", branchCode);
                    conn.Open();
                    var value = cmd.ExecuteScalar();
                    if (value == null || value == DBNull.Value)
                        return null;
                    var name = Convert.ToString(value);
                    return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(BedRepository), nameof(GetWardBedName), ex);
                return null;
            }
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

        private static void ApplyAdmissionPatientFields(
            SqlDataReader rdr,
            out string patientName,
            out string ageGender,
            out string mrNo,
            out string admNo,
            out DateTime? admDate)
        {
            var mrNoRaw = ReadDbString(rdr, "strMrNo") ?? string.Empty;
            patientName = FirstNonEmpty(ReadDbString(rdr, "strDisplayName"), "-");
            ageGender = BuildAgeGender(
                ReadDbString(rdr, "age") ?? string.Empty,
                ReadDbString(rdr, "strGenderName") ?? string.Empty);
            mrNo = string.IsNullOrWhiteSpace(mrNoRaw) ? "PENDING" : mrNoRaw;
            admNo = FirstNonEmpty(ReadDbString(rdr, "strERAdmissionNo"), "-");
            admDate = null;
            try
            {
                var ordinal = rdr.GetOrdinal("dtmERAdmission");
                if (!rdr.IsDBNull(ordinal))
                    admDate = Convert.ToDateTime(rdr.GetValue(ordinal));
            }
            catch
            {
                // Column missing from some proc variants — leave null.
            }
        }

        private static void ApplyAdmissionCardOverlay(
            LocationCardViewModel bed,
            LocationCardViewModel admission)
        {
            if (bed == null || admission == null)
                return;

            if (admission.AdmissionCode > 0)
                bed.AdmissionCode = admission.AdmissionCode;

            bed.PatientName = FirstNonEmpty(bed.PatientName, admission.PatientName, "-");
            bed.MrNo = FirstNonEmpty(
                bed.MrNo != "PENDING" ? bed.MrNo : null,
                admission.MrNo,
                "PENDING");
            bed.AdmissionNo = FirstNonEmpty(
                bed.AdmissionNo != "-" ? bed.AdmissionNo : null,
                admission.AdmissionNo,
                "-");
            bed.AgeGender = FirstNonEmpty(
                bed.AgeGender != "-" ? bed.AgeGender : null,
                admission.AgeGender,
                "-");
            if (!bed.AdmissionDate.HasValue && admission.AdmissionDate.HasValue)
                bed.AdmissionDate = admission.AdmissionDate;
            if (string.IsNullOrWhiteSpace(bed.SlotName) || bed.SlotName == "-")
                bed.SlotName = FirstNonEmpty(admission.SlotName, bed.SlotName);
        }

        /// <summary>
        /// Reads a string column safely. DBNull.Value.ToString() returns "" which
        /// would skip null-coalescing fallbacks and blank patient info in the UI.
        /// </summary>
        private static string ReadDbString(SqlDataReader rdr, string columnName)
        {
            try
            {
                var ordinal = rdr.GetOrdinal(columnName);
                if (rdr.IsDBNull(ordinal))
                    return null;
                var value = rdr.GetValue(ordinal)?.ToString();
                return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            }
            catch
            {
                return null;
            }
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null || values.Length == 0)
                return null;

            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value) && value != "-")
                    return value.Trim();
            }

            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }

            return null;
        }
    }
}
