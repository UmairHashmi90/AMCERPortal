using ERPaperless.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace ERPaperless.Services
{
    public static class ERServiceGroupRepository
    {
        private static readonly object CacheLock = new object();
        private static List<ERServiceGroupViewModel> _cachedGroups;

        public static string LastError { get; private set; }

        public static IReadOnlyList<ERServiceGroupViewModel> GetAll()
        {
            LastError = null;

            lock (CacheLock)
            {
                if (_cachedGroups != null)
                    return _cachedGroups;

                _cachedGroups = LoadAll();
                return _cachedGroups;
            }
        }

        public static void ClearCache()
        {
            lock (CacheLock)
            {
                _cachedGroups = null;
            }
        }

        /// <summary>Unique test names parsed from all groups' strGroupServices.</summary>
        public static List<string> GetAllGroupServiceNames()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var group in GetAll())
            {
                if (string.IsNullOrWhiteSpace(group.GroupServices))
                    continue;

                foreach (var token in group.GroupServices.Split(
                    new[] { ',', ';', '\n', '\r', '|' },
                    StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = token.Trim();
                    if (trimmed.Length > 0)
                        names.Add(trimmed);
                }
            }

            var list = new List<string>(names);
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        private static List<ERServiceGroupViewModel> LoadAll()
        {
            var result = new List<ERServiceGroupViewModel>();

            try
            {
                using (var conn = DBHelper.GetConnection())
                {
                    conn.Open();

                    using (var cmd = new SqlCommand("procCmbERServiceGroup", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        using (var rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                result.Add(new ERServiceGroupViewModel
                                {
                                    ServiceGroupCode = rdr["intERServiceGroupCode"] == DBNull.Value
                                        ? 0
                                        : Convert.ToInt32(rdr["intERServiceGroupCode"]),
                                    GroupName = rdr["strGroupName"]?.ToString() ?? string.Empty,
                                    GroupServices = rdr["strGroupServices"]?.ToString() ?? string.Empty,
                                    IsActive = rdr["bolIsActive"] != DBNull.Value
                                        && Convert.ToBoolean(rdr["bolIsActive"])
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(ERServiceGroupRepository), nameof(LoadAll), ex);
                LastError = ex.GetBaseException().Message;
            }

            return result;
        }
    }
}
