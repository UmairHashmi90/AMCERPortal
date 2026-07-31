using ERPaperless.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace ERPaperless.Services
{
    public static class ERPackageDetailRepository
    {
        public static string LastError { get; private set; }

        public static IReadOnlyList<ERPackageDetailViewModel> GetByPackageCode(int packageCode)
        {
            LastError = null;
            var result = new List<ERPackageDetailViewModel>();

            if (packageCode <= 0)
                return result;

            try
            {
                using (var conn = DBHelper.GetConnection())
                {
                    conn.Open();

                    using (var cmd = new SqlCommand("procGrdERPackageDetails", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.Add("@intPackageCode", SqlDbType.Int).Value = packageCode;

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
                ErrorLogging.Log(nameof(ERPackageDetailRepository), nameof(GetByPackageCode), ex);
                LastError = ex.GetBaseException().Message;
            }

            return result;
        }

        private static ERPackageDetailViewModel MapRow(SqlDataReader rdr)
        {
            return new ERPackageDetailViewModel
            {
                ItemCode = ReadInt32(rdr, "intERItemCode"),
                ItemName = ReadString(rdr, "strItemName", "ItemName"),
                Quantity = ReadString(rdr, "QTY", "Qty", "numQuantity", "strQTY")
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
                        return rdr.GetValue(ordinal)?.ToString()?.Trim() ?? string.Empty;
                }
                catch (IndexOutOfRangeException)
                {
                }
            }

            return string.Empty;
        }
    }
}
