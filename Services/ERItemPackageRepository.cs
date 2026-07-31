using ERPaperless.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace ERPaperless.Services
{
    public static class ERItemPackageRepository
    {
        private static readonly object CacheLock = new object();
        private static readonly Dictionary<int, List<ERItemPackageViewModel>> CachedPackages =
            new Dictionary<int, List<ERItemPackageViewModel>>();

        public static string LastError { get; private set; }

        public static IReadOnlyList<ERItemPackageViewModel> GetBySurgicalPackage(int intSurgicalPackage)
        {
            LastError = null;

            lock (CacheLock)
            {
                if (CachedPackages.TryGetValue(intSurgicalPackage, out var cached))
                    return cached;

                var loaded = LoadBySurgicalPackage(intSurgicalPackage);
                CachedPackages[intSurgicalPackage] = loaded;
                return loaded;
            }
        }

        public static void ClearCache()
        {
            lock (CacheLock)
            {
                CachedPackages.Clear();
            }
        }

        private static List<ERItemPackageViewModel> LoadBySurgicalPackage(int intSurgicalPackage)
        {
            var result = new List<ERItemPackageViewModel>();

            try
            {
                using (var conn = DBHelper.GetConnection())
                {
                    conn.Open();

                    using (var cmd = new SqlCommand("procCmbERItemPackages", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.Add("@intSurgicalPackage", SqlDbType.Int).Value = intSurgicalPackage;

                        using (var rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                result.Add(new ERItemPackageViewModel
                                {
                                    ItemPackageCode = ReadPackageCode(rdr),
                                    PackageName = rdr["strPackageName"]?.ToString() ?? string.Empty,
                                    PackageType = rdr["PackageType"]?.ToString() ?? string.Empty
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(ERItemPackageRepository), nameof(LoadBySurgicalPackage), ex);
                LastError = ex.GetBaseException().Message;
            }

            return result;
        }

        private static int ReadPackageCode(SqlDataReader rdr)
        {
            // procGrdERPackageDetails filters on tblERPackageDetail.intERPackageCode
            var packageCode = ReadInt32Column(rdr, "intERPackageCode");
            if (packageCode > 0)
                return packageCode;

            return ReadInt32Column(rdr, "intERItemPackageCode");
        }

        private static int ReadInt32Column(SqlDataReader rdr, string columnName)
        {
            try
            {
                var ordinal = rdr.GetOrdinal(columnName);
                return rdr.IsDBNull(ordinal) ? 0 : Convert.ToInt32(rdr.GetValue(ordinal));
            }
            catch (IndexOutOfRangeException)
            {
                return 0;
            }
        }
    }
}
