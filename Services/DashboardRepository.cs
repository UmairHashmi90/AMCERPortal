using ERPaperless.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;

namespace ERPaperless.Services
{
    public static class DashboardRepository
    {
        private static readonly string[] ToneCycle =
        {
            "tone-primary",
            "tone-warning",
            "tone-info",
            "tone-danger"
        };

        public static List<KpiCardViewModel> GetDashboardCards(int companyCode)
        {
            var cards = new List<KpiCardViewModel>();

            try
            {
                using (var conn = DBHelper.GetConnection())
                using (var cmd = new SqlCommand("ProcDashboardForERPortal", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@intCompanyCode", SqlDbType.Int).Value = companyCode;
                    conn.Open();

                    using (var rdr = cmd.ExecuteReader())
                    {
                        var i = 0;
                        while (rdr.Read())
                        {
                            var title = rdr["strTitle"] == DBNull.Value
                                ? string.Empty
                                : Convert.ToString(rdr["strTitle"]);
                            var total = rdr["intTotal"] == DBNull.Value
                                ? 0
                                : Convert.ToInt32(rdr["intTotal"], CultureInfo.InvariantCulture);
                            var description = rdr["strDescription"] == DBNull.Value
                                ? string.Empty
                                : Convert.ToString(rdr["strDescription"]);

                            cards.Add(new KpiCardViewModel
                            {
                                Title = title ?? string.Empty,
                                Value = total.ToString(CultureInfo.InvariantCulture),
                                Subtitle = description ?? string.Empty,
                                ToneClass = ToneCycle[i % ToneCycle.Length]
                            });
                            i++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(DashboardRepository), nameof(GetDashboardCards), ex);
            }

            return cards;
        }
    }
}
