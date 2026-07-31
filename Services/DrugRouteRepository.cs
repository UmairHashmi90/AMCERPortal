using ERPaperless.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace ERPaperless.Services
{
    public static class DrugRouteRepository
    {
        private static readonly object CacheLock = new object();
        private static readonly Dictionary<int, List<DrugRouteViewModel>> CachedRoutes =
            new Dictionary<int, List<DrugRouteViewModel>>();

        public static string LastError { get; private set; }

        public static IReadOnlyList<DrugRouteViewModel> GetByCompanyCode(int companyCode)
        {
            LastError = null;

            lock (CacheLock)
            {
                if (CachedRoutes.TryGetValue(companyCode, out var cached))
                    return cached;

                var loaded = LoadByCompanyCode(companyCode);
                CachedRoutes[companyCode] = loaded;
                return loaded;
            }
        }

        public static void ClearCache()
        {
            lock (CacheLock)
            {
                CachedRoutes.Clear();
            }
        }

        private static List<DrugRouteViewModel> LoadByCompanyCode(int companyCode)
        {
            var result = new List<DrugRouteViewModel>();

            try
            {
                using (var conn = DBHelper.GetConnection())
                {
                    conn.Open();

                    using (var cmd = new SqlCommand("procCmbRoute", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.Add("@intCompanyCode", SqlDbType.Int).Value = companyCode;

                        using (var rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                result.Add(new DrugRouteViewModel
                                {
                                    Code = rdr["Code"] == DBNull.Value
                                        ? 0
                                        : Convert.ToInt32(rdr["Code"]),
                                    Route = rdr["Route"]?.ToString() ?? string.Empty,
                                    ShortName = rdr["ShortName"]?.ToString() ?? string.Empty
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(DrugRouteRepository), nameof(GetByCompanyCode), ex);
                LastError = ex.GetBaseException().Message;
            }

            return result;
        }
    }
}
