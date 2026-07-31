using ERPaperless.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace ERPaperless.Services
{
    public static class ERLovRepository
    {
        private static readonly object CacheLock = new object();
        private static List<ERLovItemViewModel> _cachedLovs;

        private static readonly Dictionary<ERLovType, string[]> TypeDescriptionHints =
            new Dictionary<ERLovType, string[]>
            {
                { ERLovType.ChiefComplaints, new[] { "CHIEF COMPLAINTS", "CHIEF COMPLAINT" } },
                { ERLovType.PastHistory, new[] { "PAST HISTORY" } },
                { ERLovType.GcsScore, new[] { "GCS SCORE", "GCS" } },
                { ERLovType.Cvs, new[] { "CVS" } },
                { ERLovType.Respiratory, new[] { "RESPIRATORY" } },
                { ERLovType.AdmissionCategory, new[] { "ADMISSION CAT", "ADMISSION CATEGORY" } },
                { ERLovType.Planters, new[] { "PLANTERS", "PLANTAR" } },
                { ERLovType.BowelSound, new[] { "BOWEL SOUND", "BOWEL SOUNDS" } },
                { ERLovType.Abdomen, new[] { "ABDOMEN", "ABDOMINAL" } },
                { ERLovType.ReceivedFrom, new[] { "RECEIVED FROM" } },
                { ERLovType.Outcome, new[] { "OUTCOME" } },
                { ERLovType.ConditionUponRelease, new[] { "CONDITION UPON RELEASE", "CONDITION ON RELEASE" } },
                { ERLovType.Adr, new[] { "ADR", "ADVERSE DRUG REACTION" } }
            };

        public static string LastError { get; private set; }

        public static IReadOnlyList<ERLovItemViewModel> GetAll()
        {
            LastError = null;

            lock (CacheLock)
            {
                if (_cachedLovs != null)
                    return _cachedLovs;

                var loaded = LoadAll();
                if (string.IsNullOrWhiteSpace(LastError))
                    _cachedLovs = loaded;

                return loaded;
            }
        }

        public static IReadOnlyList<ERLovItemViewModel> GetByType(string typeDescription)
        {
            if (string.IsNullOrWhiteSpace(typeDescription))
                return new List<ERLovItemViewModel>();

            var normalizedType = Normalize(typeDescription);

            return GetAll()
                .Where(x => IsValidDetail(x) && Normalize(x.Type) == normalizedType)
                .ToList();
        }

        public static IReadOnlyList<ERLovItemViewModel> GetByType(ERLovType lovType)
        {
            var lovTypeCode = (int)lovType;
            TypeDescriptionHints.TryGetValue(lovType, out var hints);

            return GetAll()
                .Where(x =>
                    IsValidDetail(x) &&
                    (x.LovTypeCode == lovTypeCode ||
                     (hints != null && hints.Contains(Normalize(x.Type)))))
                .ToList();
        }

        public static IReadOnlyList<ERLovItemViewModel> GetByAnyType(params string[] typeDescriptions)
        {
            if (typeDescriptions == null || typeDescriptions.Length == 0)
                return new List<ERLovItemViewModel>();

            var normalizedTypes = typeDescriptions
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(Normalize)
                .ToList();

            if (!normalizedTypes.Any())
                return new List<ERLovItemViewModel>();

            return GetAll()
                .Where(x => normalizedTypes.Contains(Normalize(x.Type)))
                .ToList();
        }

        public static IReadOnlyList<ERLovItemViewModel> GetByAnyType(params ERLovType[] lovTypes)
        {
            if (lovTypes == null || lovTypes.Length == 0)
                return new List<ERLovItemViewModel>();

            var items = new List<ERLovItemViewModel>();
            foreach (var lovType in lovTypes)
                items.AddRange(GetByType(lovType));

            return items
                .GroupBy(x => x.LovCode)
                .Select(g => g.First())
                .ToList();
        }

        public static List<ERLovOptionViewModel> GetDocumentTypesForERPortal(int companyCode)
        {
            LastError = null;
            var result = new List<ERLovOptionViewModel>();

            try
            {
                using (var conn = DBHelper.GetConnection())
                {
                    conn.Open();

                    using (var cmd = new SqlCommand("procCmbDocumentTypeForERPortal", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.Add("@intCompanyCode", SqlDbType.Int).Value = companyCode;

                        using (var rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                var id = ReadInt32(rdr, "Code", "intDocumentTypeCode");
                                var name = ReadString(rdr, "Name", "strDocumentTypeName");
                                if (id > 0 && !string.IsNullOrWhiteSpace(name))
                                {
                                    result.Add(new ERLovOptionViewModel
                                    {
                                        Id = id,
                                        Name = name.Trim()
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(ERLovRepository), nameof(GetDocumentTypesForERPortal), ex);
                LastError = ex.GetBaseException().Message;
            }

            return result
                .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(x => x.Name)
                .ToList();
        }

        public static List<ERLovOptionViewModel> GetEmployeesForERPortal(int companyCode)
        {
            return ExecuteLovProc(
                "procCmbEmployeeForERPortal",
                companyCode,
                "Code",
                "Name",
                nameof(GetEmployeesForERPortal));
        }

        public static List<ERLovOptionViewModel> GetConsultantsForERPortal(int companyCode)
        {
            return ExecuteLovProc(
                "procCmbConsultant",
                companyCode,
                "Code",
                "Name",
                nameof(GetConsultantsForERPortal));
        }

        public static List<ERLovOptionViewModel> GetRelationsForERPortal(int companyCode)
        {
            return ExecuteLovProc(
                "procCmbRelation",
                companyCode,
                "Code",
                "Relation",
                nameof(GetRelationsForERPortal));
        }

        public static List<ERLovOptionViewModel> GetReferralReasonsForERPortal(int companyCode)
        {
            return ExecuteLovProc(
                "procCmbReferralReason",
                companyCode,
                "Code",
                "Reason",
                nameof(GetReferralReasonsForERPortal));
        }

        public static List<ERLovOptionViewModel> GetDischargeMedicineItemsForERPortal(int companyCode)
        {
            return ExecuteLovProc(
                "procCmbItemInterListForERPortal",
                companyCode,
                "Code",
                "Item",
                nameof(GetDischargeMedicineItemsForERPortal));
        }

        public static List<ERLovOptionViewModel> GetDrugFrequencyForERPortal(int companyCode)
        {
            LastError = null;
            var result = new List<ERLovOptionViewModel>();

            try
            {
                using (var conn = DBHelper.GetConnection())
                {
                    conn.Open();
                    using (var cmd = new SqlCommand("procCmbDrugFrequencyForERPortal", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.Add("@intCompanyCode", SqlDbType.Int).Value = companyCode;

                        using (var rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                var id = ReadInt32(rdr, "Code", "intDrugFrequencyCode");
                                var name = ReadString(rdr, "Descrption", "Description", "Name");
                                if (id > 0 && !string.IsNullOrWhiteSpace(name))
                                {
                                    result.Add(new ERLovOptionViewModel
                                    {
                                        Id = id,
                                        Name = name.Trim()
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(ERLovRepository), nameof(GetDrugFrequencyForERPortal), ex);
                LastError = ex.GetBaseException().Message;
            }

            return result
                .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(x => x.Name)
                .ToList();
        }

        public static List<ERLovOptionViewModel> GetItemGenericForERPortal(int companyCode)
        {
            LastError = null;
            var result = new List<ERLovOptionViewModel>();

            try
            {
                using (var conn = DBHelper.GetConnection())
                {
                    conn.Open();
                    using (var cmd = new SqlCommand("procCmbItemGeneric", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.Add("@intCompanyCode", SqlDbType.Int).Value = companyCode;

                        using (var rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                var id = ReadInt32(rdr, "Code", "intItemGenericCode");
                                var name = ReadString(rdr, "Generic", "strItemGenericName", "Name");
                                if (id > 0 && !string.IsNullOrWhiteSpace(name))
                                {
                                    result.Add(new ERLovOptionViewModel
                                    {
                                        Id = id,
                                        Name = name.Trim()
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(ERLovRepository), nameof(GetItemGenericForERPortal), ex);
                LastError = ex.GetBaseException().Message;
            }

            return result
                .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(x => x.Name)
                .ToList();
        }

        public static void ClearCache()
        {
            lock (CacheLock)
            {
                _cachedLovs = null;
            }
        }

        private static List<ERLovItemViewModel> LoadAll()
        {
            LastError = null;
            var result = FilterValidRows(TryLoadFromProc());
            if (result.Count > 0 || !string.IsNullOrWhiteSpace(LastError))
                return result;

            var fallback = FilterValidRows(TryLoadFromSql());
            if (fallback.Count > 0)
            {
                LastError = null;
                return fallback;
            }

            if (result.Count == 0 && fallback.Count == 0)
                LastError = LastError ?? "No LOV detail rows found in tblERLovDetail.";

            return fallback;
        }

        private static List<ERLovItemViewModel> TryLoadFromProc()
        {
            var result = new List<ERLovItemViewModel>();

            try
            {
                using (var conn = DBHelper.GetConnection())
                {
                    conn.Open();

                    using (var cmd = new SqlCommand("procCmbERLovList", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        using (var rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                                result.Add(MapRow(rdr));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(ERLovRepository), nameof(TryLoadFromProc), ex);
                LastError = ex.GetBaseException().Message;
            }

            return result;
        }

        private static List<ERLovItemViewModel> TryLoadFromSql()
        {
            var result = new List<ERLovItemViewModel>();

            const string sql = @"
SELECT  d.intERLovCode,
        d.intERLovTypeCode,
        t.strLovDescription AS [Type],
        d.strDescription
FROM    dbo.tblERLovDetail d
INNER JOIN dbo.tblERLovTypes t
        ON t.intERLovTypeCode = d.intERLovTypeCode
ORDER BY d.intERLovTypeCode, d.strDescription";

            try
            {
                using (var conn = DBHelper.GetConnection())
                {
                    conn.Open();

                    using (var cmd = new SqlCommand(sql, conn))
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                            result.Add(MapRow(rdr));
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(ERLovRepository), nameof(TryLoadFromSql), ex);
                LastError = ex.GetBaseException().Message;
            }

            return result;
        }

        private static List<ERLovItemViewModel> FilterValidRows(IEnumerable<ERLovItemViewModel> rows)
        {
            return (rows ?? Enumerable.Empty<ERLovItemViewModel>())
                .Where(IsValidDetail)
                .ToList();
        }

        private static bool IsValidDetail(ERLovItemViewModel row)
        {
            return row != null &&
                   row.LovCode > 0 &&
                   !string.IsNullOrWhiteSpace(row.Description);
        }

        private static ERLovItemViewModel MapRow(SqlDataReader rdr)
        {
            return new ERLovItemViewModel
            {
                LovCode = ReadInt32(rdr, "intERLovCode"),
                LovTypeCode = ReadInt32(rdr, "intERLovTypeCode"),
                Type = ReadString(rdr, "Type", "strLovDescription", "LovType", "strType"),
                Description = ReadString(rdr, "strDescription", "Description")
            };
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

        private static string ReadString(SqlDataReader rdr, params string[] columnNames)
        {
            foreach (var columnName in columnNames)
            {
                try
                {
                    var ordinal = rdr.GetOrdinal(columnName);
                    if (!rdr.IsDBNull(ordinal))
                        return rdr.GetValue(ordinal)?.ToString() ?? string.Empty;
                }
                catch (IndexOutOfRangeException)
                {
                }
            }

            return string.Empty;
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static List<ERLovOptionViewModel> ExecuteLovProc(
            string procedureName,
            int companyCode,
            string codeColumn,
            string nameColumn,
            string methodName)
        {
            LastError = null;
            var result = new List<ERLovOptionViewModel>();

            try
            {
                using (var conn = DBHelper.GetConnection())
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(procedureName, conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.Add("@intCompanyCode", SqlDbType.Int).Value = companyCode;

                        using (var rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                var id = ReadInt32(rdr, codeColumn);
                                var name = ReadString(rdr, nameColumn);
                                if (id > 0 && !string.IsNullOrWhiteSpace(name))
                                {
                                    result.Add(new ERLovOptionViewModel
                                    {
                                        Id = id,
                                        Name = name.Trim()
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(ERLovRepository), methodName, ex);
                LastError = ex.GetBaseException().Message;
            }

            return result
                .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(x => x.Name)
                .ToList();
        }
    }
}
