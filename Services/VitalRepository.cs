using ERPaperless.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace ERPaperless.Services
{
    public static class VitalRepository
    {
        public static string LastError { get; private set; }

        public static List<VitalRecordViewModel> GetVitals(string patientId, int companyCode)
        {
            var target = ResolveVitalTarget(patientId, companyCode);
            if (!target.IsValid) return new List<VitalRecordViewModel>();

            try
            {
                var list = new List<VitalRecordViewModel>();

                using (var conn = DBHelper.GetConnection())
                {
                    conn.Open();

                    using (var cmd = new SqlCommand("procGrdERPatientVitalsForERPortal", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.Add("@intERPatientCode", SqlDbType.BigInt).Value = target.ERPatientCode;
                        cmd.Parameters.Add("@intBranchCode", SqlDbType.Int).Value = target.BranchCode;
                        cmd.Parameters.Add("@intCompanyCode", SqlDbType.Int).Value = target.CompanyCode;

                        using (var rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                                list.Add(MapVitalFromProc(rdr));
                        }
                    }
                }

                AttachExtraVitalFields(list, target.CompanyCode);
                ResolveConsciousnessTexts(list);
                return list;
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(VitalRepository), nameof(GetVitals), ex);
                return new List<VitalRecordViewModel>();
            }
        }

        public static bool AddVital(
            string patientId,
            int companyCode,
            int userCode,
            VitalRecordViewModel record)
        {
            LastError = null;
            var target = ResolveVitalTarget(patientId, companyCode);
            if (!target.IsValid || record == null)
            {
                LastError = "Could not resolve bed/admission for this patient.";
                return false;
            }

            try
            {
                using (var db = dbAMCEntities.Create())
                {
                    var now = DateTime.Now;
                    // Keep Pulse in sync with Heart Rate (Pulse field is hidden in UI).
                    var heartRate = record.HeartRate ?? record.Pulse;
                    var entity = new tblERPatientVital
                    {
                        intERPatientCode   = target.ERPatientCode,
                        dtmVitals          = now,
                        intHeartRate       = heartRate,
                        intPulse           = heartRate,
                        intBP1             = record.BP1,
                        intBP2             = record.BP2,
                        intResp            = record.RespiratoryRate,
                        numTemp            = record.Temperature,
                        intHeight          = record.Height,
                        numWeight          = record.Weight,
                        intMetricBMI       = record.MetricBMI,
                        intSPO2            = record.Spo2,
                        numGlucoseF        = record.GlucoseF,
                        numGlucoseR        = record.GlucoseR,
                        intFallRisk        = record.FallRisk,
                        intPainScore       = record.PainScore,
                        intConciousness    = record.ConsciousnessCode,
                        dtmCreated         = now,
                        intOwnerCode       = userCode,
                        intCreatedByCode   = userCode,
                        intRecordStatusCode = 1,
                        intBranchCode      = target.BranchCode,
                        intCompanyCode     = target.CompanyCode
                    };
                    db.tblERPatientVitals.Add(entity);

                    if (db.SaveChanges() <= 0)
                        return false;

                    TrySaveSpo2Remark(entity.intERPatientVitalsCode, target.CompanyCode, record.Spo2Remark);
                    return true;
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(VitalRepository), nameof(AddVital), ex);
                LastError = ex.GetBaseException().Message;
                return false;
            }
        }

        public static bool DeleteVital(
            long vitalId,
            int companyCode,
            int userCode)
        {
            LastError = null;
            if (vitalId <= 0) return false;

            try
            {
                using (var db = dbAMCEntities.Create())
                {
                    var vital = db.tblERPatientVitals.FirstOrDefault(v =>
                        v.intERPatientVitalsCode == vitalId
                        && v.intCompanyCode == companyCode
                        && v.intRecordStatusCode == 1);

                    if (vital == null) return false;

                    vital.intRecordStatusCode = 8;
                    vital.dtmLastM = DateTime.Now;
                    vital.intAlteredByCode = userCode;

                    return db.SaveChanges() > 0;
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(VitalRepository), nameof(DeleteVital), ex);
                LastError = ex.GetBaseException().Message;
                return false;
            }
        }

        public static bool AttachPatientToAdmission(
            long erPatientCode,
            int companyCode,
            long admissionCode,
            int userCode)
        {
            return ERPatientRepository.AttachToAdmission(
                erPatientCode,
                admissionCode,
                companyCode,
                userCode);
        }

        private static VitalTarget ResolveVitalTarget(string patientId, int companyCode)
        {
            if (string.IsNullOrWhiteSpace(patientId) || companyCode <= 0)
                return VitalTarget.Invalid;

            if (!long.TryParse(patientId, out var erPatientCode) || erPatientCode <= 0)
                return VitalTarget.Invalid;

            try
            {
                var patient = ERPatientRepository.GetByCode(erPatientCode, companyCode);
                if (patient == null) return VitalTarget.Invalid;

                return new VitalTarget
                {
                    ERPatientCode = patient.intERPatientCode,
                    BranchCode    = patient.intBranchCode,
                    CompanyCode   = companyCode
                };
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(VitalRepository), nameof(ResolveVitalTarget), ex);
                return VitalTarget.Invalid;
            }
        }

        private static VitalRecordViewModel MapVitalFromProc(SqlDataReader rdr)
        {
            var bp1 = ReadNullableInt32(rdr, "BP (Systolic)");
            var bp2 = ReadNullableInt32(rdr, "BP (Diastolic)");
            var glucoseF = ReadNullableDecimal(rdr, "Glucose(F)");
            var glucoseR = ReadNullableDecimal(rdr, "Glucose(R)");
            var bsr = glucoseR.HasValue
                ? Convert.ToInt32(glucoseR.Value)
                : glucoseF.HasValue
                    ? Convert.ToInt32(glucoseF.Value)
                    : 0;

            return new VitalRecordViewModel
            {
                Id = ReadInt64(rdr, "Code"),
                RecordedOn = ReadDateTime(rdr, "VitalDate"),
                RecordStatusCode = ReadInt32(rdr, "intRecordStatusCode"),
                Height = ReadNullableInt32(rdr, "Height (cm)"),
                Weight = ReadNullableDecimal(rdr, "Weight (kg)"),
                HeartRate = ReadNullableInt32(rdr, "HeartRate"),
                Pulse = ReadNullableInt32(rdr, "Pulse (BPM)"),
                BP1 = bp1,
                BP2 = bp2,
                BloodPressure = (bp1.HasValue && bp1.Value > 0) || (bp2.HasValue && bp2.Value > 0)
                    ? $"{bp1 ?? 0}/{bp2 ?? 0}"
                    : "0/0",
                RespiratoryRate = ReadNullableInt32(rdr, "Respiratory"),
                Temperature = ReadNullableDecimal(rdr, "Temp (F)"),
                TemperatureC = ReadNullableDecimal(rdr, "Temp (C)"),
                GlucoseF = glucoseF,
                GlucoseR = glucoseR,
                Bsr = bsr,
                NewsRespiratoryScore = ReadNullableInt32(rdr, "NEWS_RespiratoryScore"),
                NewsSystolicScore = ReadNullableInt32(rdr, "NEWS_SystolicScore"),
                NewsHeartRateScore = ReadNullableInt32(rdr, "NEWS_HeartRateScore"),
                NewsTemperatureScore = ReadNullableInt32(rdr, "NEWS_TemperatureScore"),
                News2AvailableScore = ReadNullableInt32(rdr, "NEWS2AvailableScore"),
                News2Score = ReadNullableInt32(rdr, "NEWS2Score"),
                News2DataStatus = ReadString(rdr, "NEWS2DataStatus"),
                News2Risk = ReadString(rdr, "NEWS2Risk"),
                News2RedFlag = ReadString(rdr, "NEWS2RedFlag"),
                News2FocusParameter = ReadString(rdr, "NEWS2FocusParameter"),
                PreviousNews2AvailableScore = ReadNullableInt32(rdr, "PreviousNEWS2AvailableScore"),
                News2AvailableChange = ReadNullableInt32(rdr, "NEWS2AvailableChange"),
                News2Trend = ReadString(rdr, "NEWS2Trend"),
                News2Trigger = ReadString(rdr, "NEWS2Trigger"),
                SuggestedMonitoring = ReadString(rdr, "SuggestedMonitoring"),
                SuggestedCareArea = ReadString(rdr, "SuggestedCareArea")
            };
        }

        private static void TrySaveSpo2Remark(long vitalId, int companyCode, string remark)
        {
            if (vitalId <= 0 || companyCode <= 0)
                return;

            var value = string.IsNullOrWhiteSpace(remark) ? null : remark.Trim();
            if (value != null && value.Length > 100)
                value = value.Substring(0, 100);

            try
            {
                using (var conn = DBHelper.GetConnection())
                using (var cmd = new SqlCommand(@"
UPDATE tblERPatientVitals
SET strSPO2Remark = @remark
WHERE intERPatientVitalsCode = @vitalId
  AND intCompanyCode = @companyCode", conn))
                {
                    cmd.Parameters.Add("@remark", SqlDbType.NVarChar, 100).Value = (object)value ?? DBNull.Value;
                    cmd.Parameters.Add("@vitalId", SqlDbType.BigInt).Value = vitalId;
                    cmd.Parameters.Add("@companyCode", SqlDbType.Int).Value = companyCode;
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                // Column may not exist yet — vitals still save without remark.
                ErrorLogging.Log(nameof(VitalRepository), nameof(TrySaveSpo2Remark), ex);
            }
        }

        /// <summary>
        /// Proc does not return SPO2 / BMI / FallRisk / PainScore / remark.
        /// Load those from the table so header tiles and existing grid columns still work.
        /// </summary>
        private static void AttachExtraVitalFields(List<VitalRecordViewModel> list, int companyCode)
        {
            if (list == null || list.Count == 0 || companyCode <= 0)
                return;

            try
            {
                var ids = list.Select(x => x.Id).Where(x => x > 0).Distinct().ToList();
                if (ids.Count == 0) return;

                var idList = string.Join(",", ids);
                var sqlWithRemark = @"
SELECT intERPatientVitalsCode, intMetricBMI, intSPO2, intFallRisk, intPainScore, intConciousness, strSPO2Remark
FROM tblERPatientVitals
WHERE intCompanyCode = @companyCode
  AND intERPatientVitalsCode IN (" + idList + ")";
                var sqlWithoutRemark = @"
SELECT intERPatientVitalsCode, intMetricBMI, intSPO2, intFallRisk, intPainScore, intConciousness
FROM tblERPatientVitals
WHERE intCompanyCode = @companyCode
  AND intERPatientVitalsCode IN (" + idList + ")";

                using (var conn = DBHelper.GetConnection())
                {
                    conn.Open();
                    var map = ReadExtraVitalFields(conn, sqlWithRemark, companyCode, includeRemark: true)
                           ?? ReadExtraVitalFields(conn, sqlWithoutRemark, companyCode, includeRemark: false);

                    if (map == null) return;

                    foreach (var item in list)
                    {
                        if (!map.TryGetValue(item.Id, out var extra))
                            continue;

                        item.MetricBMI = extra.MetricBMI;
                        item.Spo2 = extra.Spo2;
                        item.FallRisk = extra.FallRisk;
                        item.PainScore = extra.PainScore;
                        item.ConsciousnessCode = extra.ConsciousnessCode;
                        item.Spo2Remark = extra.Spo2Remark;
                    }
                }
            }
            catch (Exception ex)
            {
                // strSPO2Remark may be missing — ignore enrichment failure.
                ErrorLogging.Log(nameof(VitalRepository), nameof(AttachExtraVitalFields), ex);
            }
        }

        private static int ReadInt32(SqlDataReader rdr, params string[] columnNames)
        {
            foreach (var columnName in columnNames)
            {
                try
                {
                    var ordinal = rdr.GetOrdinal(columnName);
                    return rdr.IsDBNull(ordinal) ? 0 : Convert.ToInt32(rdr.GetValue(ordinal));
                }
                catch (IndexOutOfRangeException)
                {
                }
            }

            return 0;
        }

        private static long ReadInt64(SqlDataReader rdr, params string[] columnNames)
        {
            foreach (var columnName in columnNames)
            {
                try
                {
                    var ordinal = rdr.GetOrdinal(columnName);
                    return rdr.IsDBNull(ordinal) ? 0L : Convert.ToInt64(rdr.GetValue(ordinal));
                }
                catch (IndexOutOfRangeException)
                {
                }
            }

            return 0L;
        }

        private static int? ReadNullableInt32(SqlDataReader rdr, params string[] columnNames)
        {
            foreach (var columnName in columnNames)
            {
                try
                {
                    var ordinal = rdr.GetOrdinal(columnName);
                    return rdr.IsDBNull(ordinal) ? (int?)null : Convert.ToInt32(rdr.GetValue(ordinal));
                }
                catch (IndexOutOfRangeException)
                {
                }
            }

            return null;
        }

        private static decimal? ReadNullableDecimal(SqlDataReader rdr, params string[] columnNames)
        {
            foreach (var columnName in columnNames)
            {
                try
                {
                    var ordinal = rdr.GetOrdinal(columnName);
                    return rdr.IsDBNull(ordinal) ? (decimal?)null : Convert.ToDecimal(rdr.GetValue(ordinal));
                }
                catch (IndexOutOfRangeException)
                {
                }
            }

            return null;
        }

        private static DateTime ReadDateTime(SqlDataReader rdr, params string[] columnNames)
        {
            foreach (var columnName in columnNames)
            {
                try
                {
                    var ordinal = rdr.GetOrdinal(columnName);
                    return rdr.IsDBNull(ordinal) ? DateTime.MinValue : Convert.ToDateTime(rdr.GetValue(ordinal));
                }
                catch (IndexOutOfRangeException)
                {
                }
            }

            return DateTime.MinValue;
        }

        private static string ReadString(SqlDataReader rdr, params string[] columnNames)
        {
            foreach (var columnName in columnNames)
            {
                try
                {
                    var ordinal = rdr.GetOrdinal(columnName);
                    if (!rdr.IsDBNull(ordinal))
                        return rdr.GetValue(ordinal)?.ToString()?.Trim() ?? string.Empty;
                }
                catch (IndexOutOfRangeException)
                {
                }
            }

            return string.Empty;
        }

        private static Dictionary<long, ExtraVitalFields> ReadExtraVitalFields(
            SqlConnection conn,
            string sql,
            int companyCode,
            bool includeRemark)
        {
            try
            {
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.Add("@companyCode", SqlDbType.Int).Value = companyCode;
                    using (var rdr = cmd.ExecuteReader())
                    {
                        var map = new Dictionary<long, ExtraVitalFields>();
                        while (rdr.Read())
                        {
                            var id = rdr.GetInt64(0);
                            map[id] = new ExtraVitalFields
                            {
                                MetricBMI = rdr.IsDBNull(1) ? (int?)null : Convert.ToInt32(rdr.GetValue(1)),
                                Spo2 = rdr.IsDBNull(2) ? (int?)null : Convert.ToInt32(rdr.GetValue(2)),
                                FallRisk = rdr.IsDBNull(3) ? (int?)null : Convert.ToInt32(rdr.GetValue(3)),
                                PainScore = rdr.IsDBNull(4) ? (int?)null : Convert.ToInt32(rdr.GetValue(4)),
                                ConsciousnessCode = rdr.IsDBNull(5) ? (int?)null : Convert.ToInt32(rdr.GetValue(5)),
                                Spo2Remark = includeRemark && !rdr.IsDBNull(6)
                                    ? (rdr.GetString(6) ?? string.Empty).Trim()
                                    : string.Empty
                            };
                        }

                        return map;
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(VitalRepository), nameof(ReadExtraVitalFields), ex);
                return null;
            }
        }

        private struct ExtraVitalFields
        {
            public int? MetricBMI { get; set; }
            public int? Spo2 { get; set; }
            public int? FallRisk { get; set; }
            public int? PainScore { get; set; }
            public int? ConsciousnessCode { get; set; }
            public string Spo2Remark { get; set; }
        }

        private static void ResolveConsciousnessTexts(List<VitalRecordViewModel> list)
        {
            if (list == null || list.Count == 0) return;

            try
            {
                var lovMap = ERLovRepository.GetByType(ERLovType.Conciousness)
                    .Where(x => x != null && x.LovCode > 0)
                    .GroupBy(x => x.LovCode)
                    .ToDictionary(g => g.Key, g => g.First().Description ?? string.Empty);

                foreach (var item in list)
                {
                    if (!item.ConsciousnessCode.HasValue || item.ConsciousnessCode.Value <= 0)
                    {
                        item.ConsciousnessText = string.Empty;
                        continue;
                    }

                    item.ConsciousnessText = lovMap.TryGetValue(item.ConsciousnessCode.Value, out var text)
                        ? text
                        : string.Empty;
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(VitalRepository), nameof(ResolveConsciousnessTexts), ex);
            }
        }

        private struct VitalTarget
        {
            public static VitalTarget Invalid => new VitalTarget();

            public long ERPatientCode { get; set; }
            public int BranchCode { get; set; }
            public int CompanyCode { get; set; }

            public bool IsValid =>
                ERPatientCode > 0 &&
                BranchCode > 0 &&
                CompanyCode > 0;
        }
    }
}
