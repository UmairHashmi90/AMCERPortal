using ERPaperless.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace ERPaperless.Services
{
    public static class ReportsRepository
    {
        public static string LastError { get; private set; }

        public static List<ReportInvestigationRowViewModel> GetPatientInvestigationsForPortal(
            int erAdmissionCode,
            int companyCode)
        {
            LastError = null;
            var rows = new List<ReportInvestigationRowViewModel>();

            if (erAdmissionCode <= 0 || companyCode <= 0)
                return rows;

            try
            {
                using (var conn = DBHelper.GetConnection())
                using (var cmd = new SqlCommand("procGetPatientInvestigationsForERPortal", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@intERAdmissionCode", SqlDbType.BigInt).Value = erAdmissionCode;
                    cmd.Parameters.Add("@intCompanyCode", SqlDbType.Int).Value = companyCode;

                    conn.Open();
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            rows.Add(new ReportInvestigationRowViewModel
                            {
                                ReportTypeCode = ReadInt(rdr, "intReportTypeCode"),
                                PatientOrderDetailCode = ReadLong(rdr, "intPatientOrderDetailCode"),
                                OrderDate = ReadNullableDateTime(rdr, "OrderDate"),
                                Test = ReadString(rdr, "Test"),
                                Status = ReadString(rdr, "Status")
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LastError = ex.GetBaseException().Message;
                ErrorLogging.Log(nameof(ReportsRepository), nameof(GetPatientInvestigationsForPortal), ex);
            }

            return rows;
        }

        public static List<PrescriptionVisitRowViewModel> GetPatientPrescriptionsForPortal(
            int erAdmissionCode,
            int companyCode)
        {
            LastError = null;
            var rows = new List<PrescriptionVisitRowViewModel>();

            if (erAdmissionCode <= 0 || companyCode <= 0)
                return rows;

            try
            {
                using (var conn = DBHelper.GetConnection())
                using (var cmd = new SqlCommand("procGetPatientPrescriptionForERPortal", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@intERAdmissionCode", SqlDbType.BigInt).Value = erAdmissionCode;
                    cmd.Parameters.Add("@intCompanyCode", SqlDbType.Int).Value = companyCode;

                    conn.Open();
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            rows.Add(new PrescriptionVisitRowViewModel
                            {
                                Code = ReadLong(rdr, "Code"),
                                VisitDate = ReadNullableDateTime(rdr, "VisitDate"),
                                Consultant = ReadString(rdr, "Consultant"),
                                Speciality = ReadString(rdr, "Speciality")
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LastError = ex.GetBaseException().Message;
                ErrorLogging.Log(nameof(ReportsRepository), nameof(GetPatientPrescriptionsForPortal), ex);
            }

            return rows;
        }

        private static int ReadInt(SqlDataReader rdr, string columnName)
        {
            var ordinal = rdr.GetOrdinal(columnName);
            if (ordinal < 0 || rdr.IsDBNull(ordinal))
                return 0;
            return Convert.ToInt32(rdr.GetValue(ordinal));
        }

        private static long ReadLong(SqlDataReader rdr, string columnName)
        {
            var ordinal = rdr.GetOrdinal(columnName);
            if (ordinal < 0 || rdr.IsDBNull(ordinal))
                return 0L;
            return Convert.ToInt64(rdr.GetValue(ordinal));
        }

        private static DateTime? ReadNullableDateTime(SqlDataReader rdr, string columnName)
        {
            var ordinal = rdr.GetOrdinal(columnName);
            if (ordinal < 0 || rdr.IsDBNull(ordinal))
                return null;
            return Convert.ToDateTime(rdr.GetValue(ordinal));
        }

        private static string ReadString(SqlDataReader rdr, string columnName)
        {
            var ordinal = rdr.GetOrdinal(columnName);
            if (ordinal < 0 || rdr.IsDBNull(ordinal))
                return string.Empty;
            return Convert.ToString(rdr.GetValue(ordinal));
        }
    }
}
