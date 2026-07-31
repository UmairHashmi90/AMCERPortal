using ERPaperless.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace ERPaperless.Services
{
    public static class ERItemRepository
    {
        private static readonly object CacheLock = new object();
        private static List<ERItemViewModel> _cachedItems;

        public static string LastError { get; private set; }

        public static IReadOnlyList<ERItemViewModel> GetAll()
        {
            LastError = null;

            lock (CacheLock)
            {
                if (_cachedItems != null)
                    return _cachedItems;

                _cachedItems = LoadAll();
                return _cachedItems;
            }
        }

        public static void ClearCache()
        {
            lock (CacheLock)
            {
                _cachedItems = null;
            }
        }

        private static List<ERItemViewModel> LoadAll()
        {
            var result = new List<ERItemViewModel>();

            try
            {
                using (var conn = DBHelper.GetConnection())
                {
                    conn.Open();

                    using (var cmd = new SqlCommand("procCmbERItems", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        using (var rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                result.Add(new ERItemViewModel
                                {
                                    ItemCode = rdr["intERItemCode"] == DBNull.Value
                                        ? 0
                                        : Convert.ToInt32(rdr["intERItemCode"]),
                                    ItemName = rdr["strItemName"]?.ToString() ?? string.Empty,
                                    IsSurgical = rdr["bolIsSurgical"] != DBNull.Value
                                        && Convert.ToBoolean(rdr["bolIsSurgical"])
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(ERItemRepository), nameof(LoadAll), ex);
                LastError = ex.GetBaseException().Message;
            }

            return result;
        }
    }
}
