using ERPaperless.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace ERPaperless.Services
{
    public static class ERQueueRepository
    {
        public static string LastError { get; private set; }

        public static List<QueueItemViewModel> GetPharmacyQueue(int companyCode, int branchCode, int userCode)
        {
            LastError = null;
            var bedLookup = BuildPatientBedLookup(companyCode, branchCode, userCode);

            try
            {
                using (var db = dbAMCEntities.Create())
                {
                    var rows = (
                        from o in db.tblERPatientPackageOrders
                        join p in db.tblERPatients on o.intERPatientCode equals p.intERPatientCode
                        where o.intCompanyCode == companyCode
                              && o.intBranchCode == branchCode
                              && o.intRecordStatusCode == 1
                              && (o.intPackageTypeCode == 1 || o.intPackageTypeCode == 2)
                              && p.intRecordStatusCode == 1
                              && (p.bolIsDischarge != true)
                        orderby o.bolIsAcknowledged,
                            o.bolDiscontinue,
                            o.dtmCreated descending
                        select new { Order = o, Patient = p })
                        .ToList();

                    var userCodes = rows
                        .Select(x => x.Order.intCreatedByCode)
                        .Where(x => x > 0)
                        .Distinct()
                        .ToList();
                    var userNames = LookupUserNames(userCodes, companyCode);

                    var result = new List<QueueItemViewModel>();
                    var sr = 0;
                    foreach (var row in rows)
                    {
                        sr++;
                        var patientId = row.Patient.intERPatientCode.ToString();
                        bedLookup.TryGetValue(row.Patient.intERPatientCode, out var bedInfo);

                        var typeLabel = row.Order.intPackageTypeCode == 2 ? "Surgical" : "Medicine";
                        var dosePart = string.IsNullOrWhiteSpace(row.Order.strDose)
                            ? string.Empty
                            : " (" + row.Order.strDose.Trim() + ")";
                        var detail = typeLabel + " " + (row.Order.strItemName ?? "-") + dosePart;

                        string statusClass;
                        string statusLabel;
                        if (row.Order.bolDiscontinue)
                        {
                            statusClass = "status-red";
                            statusLabel = "Discontinued";
                        }
                        else if (row.Order.bolIsAcknowledged)
                        {
                            statusClass = "status-green";
                            statusLabel = "Charged";
                        }
                        else
                        {
                            statusClass = "status-orange";
                            statusLabel = "Pending";
                        }

                        var requestedBy = row.Order.intCreatedByCode > 0
                                          && userNames.TryGetValue(row.Order.intCreatedByCode, out var byName)
                            ? byName
                            : "-";

                        result.Add(new QueueItemViewModel
                        {
                            SrNo = sr,
                            PatientId = patientId,
                            RecordId = row.Order.intERPatientPackageOrderCode,
                            BedNo = bedInfo?.SlotName ?? ("BED-" + row.Patient.intWardBedCode),
                            MrNo = bedInfo?.MrNo ?? "PENDING",
                            Name = ResolvePatientName(row.Patient, bedInfo),
                            RequestDate = row.Order.dtmCreated.ToString("dd/MM/yyyy HH:mm"),
                            RequestBy = requestedBy,
                            RequestDetail = detail.Trim(),
                            StatusClass = statusClass,
                            StatusLabel = statusLabel
                        });
                    }

                    return result;
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(ERQueueRepository), nameof(GetPharmacyQueue), ex);
                LastError = ex.GetBaseException().Message;
                return new List<QueueItemViewModel>();
            }
        }

        public static List<QueueItemViewModel> GetBillingQueue(int companyCode, int branchCode, int userCode)
        {
            LastError = null;
            var bedLookup = BuildPatientBedLookup(companyCode, branchCode, userCode);

            try
            {
                using (var db = dbAMCEntities.Create())
                {
                    var rows = (
                        from i in db.tblERPatientInvestigations
                        join p in db.tblERPatients on i.intERPatientCode equals p.intERPatientCode
                        where i.intCompanyCode == companyCode
                              && i.intBranchCode == branchCode
                              && i.intRecordStatusCode == 1
                              && p.intRecordStatusCode == 1
                              && (p.bolIsDischarge != true)
                        orderby i.bolIsAcknowledged,
                            i.bolIsCancelled,
                            i.dtmCreated descending
                        select new { Investigation = i, Patient = p })
                        .ToList();

                    var userCodes = rows
                        .Select(x => x.Investigation.intCreatedByCode)
                        .Where(x => x > 0)
                        .Distinct()
                        .ToList();
                    var userNames = LookupUserNames(userCodes, companyCode);

                    var result = new List<QueueItemViewModel>();
                    var sr = 0;
                    foreach (var row in rows)
                    {
                        sr++;
                        var patientId = row.Patient.intERPatientCode.ToString();
                        bedLookup.TryGetValue(row.Patient.intERPatientCode, out var bedInfo);

                        string statusClass;
                        string statusLabel;
                        if (row.Investigation.bolIsCancelled)
                        {
                            statusClass = "status-red";
                            statusLabel = "Cancelled";
                        }
                        else if (row.Investigation.bolIsAcknowledged)
                        {
                            statusClass = "status-green";
                            statusLabel = "Acknowledged";
                        }
                        else
                        {
                            statusClass = "status-orange";
                            statusLabel = "Pending";
                        }

                        var requestedBy = row.Investigation.intCreatedByCode > 0
                                          && userNames.TryGetValue(row.Investigation.intCreatedByCode, out var byName)
                            ? byName
                            : "-";

                        var detail = row.Investigation.strTestName ?? "-";
                        if (!string.IsNullOrWhiteSpace(row.Investigation.strRemarks))
                            detail += " — " + row.Investigation.strRemarks.Trim();

                        result.Add(new QueueItemViewModel
                        {
                            SrNo = sr,
                            PatientId = patientId,
                            RecordId = row.Investigation.intERPatientInvestigationCode,
                            BedNo = bedInfo?.SlotName ?? ("BED-" + row.Patient.intWardBedCode),
                            MrNo = bedInfo?.MrNo ?? "PENDING",
                            Name = ResolvePatientName(row.Patient, bedInfo),
                            RequestDate = row.Investigation.dtmCreated.ToString("dd/MM/yyyy HH:mm"),
                            RequestBy = requestedBy,
                            RequestDetail = detail,
                            StatusClass = statusClass,
                            StatusLabel = statusLabel
                        });
                    }

                    return result;
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(ERQueueRepository), nameof(GetBillingQueue), ex);
                LastError = ex.GetBaseException().Message;
                return new List<QueueItemViewModel>();
            }
        }

        private static Dictionary<long, LocationCardViewModel> BuildPatientBedLookup(
            int companyCode,
            int branchCode,
            int userCode)
        {
            var lookup = new Dictionary<long, LocationCardViewModel>();
            var beds = BedRepository.GetBeds(companyCode, branchCode, userCode) ?? new List<LocationCardViewModel>();

            foreach (var bed in beds)
            {
                if (long.TryParse(bed.PatientId, out var patientCode) && patientCode > 0)
                    lookup[patientCode] = bed;
            }

            return lookup;
        }

        private static string ResolvePatientName(tblERPatient patient, LocationCardViewModel bedInfo)
        {
            if (!string.IsNullOrWhiteSpace(patient.strName))
                return patient.strName.Trim();

            if (bedInfo != null && !string.IsNullOrWhiteSpace(bedInfo.PatientName) && bedInfo.PatientName != "-")
                return bedInfo.PatientName;

            return "Unknown";
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
                    var sql = @"
SELECT u.intUserCode, u.strUserName
FROM dbo.tblUser u
WHERE u.intUserCode IN (" + string.Join(",", paramNames) + @")
  AND (u.intCompanyCode = @companyCode OR @companyCode = 0)";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.Add("@companyCode", SqlDbType.Int).Value = companyCode;
                        for (var i = 0; i < codes.Count; i++)
                            cmd.Parameters.Add(paramNames[i], SqlDbType.Int).Value = codes[i];

                        using (var rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                var code = Convert.ToInt32(rdr["intUserCode"]);
                                var name = rdr["strUserName"]?.ToString();
                                if (!string.IsNullOrWhiteSpace(name))
                                    result[code] = name;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(ERQueueRepository), nameof(LookupUserNames), ex);
            }

            return result;
        }
    }
}
