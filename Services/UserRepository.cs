using ERPaperless.Models;
using System;
using System.Data;
using System.Data.SqlClient;

namespace ERPaperless.Services
{
    public static class UserRepository
    {
        // ── Login ────────────────────────────────────────────────────────────
        // Validates password via procValidateLoginForERPortal, then loads rights
        // with procGetRoleRightForERPortal using user code + company code.
        // ────────────────────────────────────────────────────────────────────
        public static ERUserRoleModel ValidateAndGetUser(string loginName, string password)
        {
            if (string.IsNullOrWhiteSpace(loginName) || string.IsNullOrWhiteSpace(password))
                return null;

            try
            {
                using (var conn = DBHelper.GetConnection())
                {
                    conn.Open();

                    using (var cmd = new SqlCommand("procValidateLoginForERPortal", conn))
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@strLoginName",    loginName.Trim());
                        cmd.Parameters.AddWithValue("@strUserPassword", password);

                        ERUserRoleModel user;
                        using (var rdr = cmd.ExecuteReader())
                        {
                            if (!rdr.Read()) return null;
                            user = MapUserIdentity(rdr);
                        }

                        user = GetUserRights(user.UserCode, user.CompanyCode) ?? user;
                        EnrichIdentityLinks(user);
                        return user;
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(UserRepository), nameof(ValidateAndGetUser), ex);
                return null;
            }
        }

       
        public static ERUserRoleModel GetUserRights(int userCode, int companyCode)
        {
            if (userCode <= 0 || companyCode <= 0) return null;

            try
            {
                using (var conn = DBHelper.GetConnection())
                {
                    conn.Open();

                    using (var cmd = new SqlCommand("procGetRoleRightForERPortal", conn))
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@intUserCode",    userCode);
                        cmd.Parameters.AddWithValue("@intCompanyCode", companyCode);

                        using (var rdr = cmd.ExecuteReader())
                        {
                            if (!rdr.Read()) return null;
                            var user = MapRole(rdr);
                            EnrichIdentityLinks(user);
                            return user;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(UserRepository), nameof(GetUserRights), ex);
                return null;
            }
        }

        /// <summary>
        /// Resolves EmployeeCode / ConsultantCode for the logged-in user so MO LOVs
        /// (Referral / LAMA / Death) can auto-pick by ID instead of display name.
        /// </summary>
        public static void EnrichIdentityLinks(ERUserRoleModel user)
        {
            if (user == null || user.UserCode <= 0 || user.CompanyCode <= 0) return;
            if (user.IdentityLinksResolved) return;

            try
            {
                using (var conn = DBHelper.GetConnection())
                {
                    conn.Open();
                    if (user.EmployeeCode <= 0)
                        user.EmployeeCode = ResolveEmployeeCode(conn, user.UserCode, user.CompanyCode);

                    if (user.ConsultantCode <= 0)
                        user.ConsultantCode = ResolveConsultantCode(conn, user);
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(UserRepository), nameof(EnrichIdentityLinks), ex);
            }
            finally
            {
                user.IdentityLinksResolved = true;
            }
        }

        private static int ResolveEmployeeCode(SqlConnection conn, int userCode, int companyCode)
        {
            var userEmpColumn = FirstExistingColumn(conn, "tblUser",
                "intEmpCode", "intEmployeeCode", "intEmpUserCode");
            if (string.IsNullOrEmpty(userEmpColumn)) return 0;

            using (var cmd = new SqlCommand(
                "SELECT TOP 1 " + userEmpColumn +
                " FROM tblUser WHERE intUserCode = @userCode AND intCompanyCode = @companyCode", conn))
            {
                cmd.Parameters.Add("@userCode", SqlDbType.Int).Value = userCode;
                cmd.Parameters.Add("@companyCode", SqlDbType.Int).Value = companyCode;
                var value = cmd.ExecuteScalar();
                if (value == null || value == DBNull.Value) return 0;
                return Convert.ToInt32(value);
            }
        }

        private static int ResolveConsultantCode(SqlConnection conn, ERUserRoleModel user)
        {
            // 1) Direct user link on consultant (if column exists).
            if (ColumnExists(conn, "tblConsultant", "intUserCode"))
            {
                var code = ExecuteInt(conn,
                    @"SELECT TOP 1 intConsultantCode
                      FROM tblConsultant
                      WHERE intUserCode = @userCode
                        AND intCompanyCode = @companyCode
                        AND bolIsActive = 1",
                    user.UserCode, user.CompanyCode, null);
                if (code > 0) return code;
            }

            // 2) User -> Employee -> Consultant via shared emp key.
            var consultantEmpColumn = FirstExistingColumn(conn, "tblConsultant",
                "intEmpCode", "intEmployeeCode");
            if (!string.IsNullOrEmpty(consultantEmpColumn) && user.EmployeeCode > 0)
            {
                var code = ExecuteInt(conn,
                    "SELECT TOP 1 intConsultantCode FROM tblConsultant WHERE "
                    + consultantEmpColumn +
                    @" = @empCode
                        AND intCompanyCode = @companyCode
                        AND bolIsActive = 1",
                    user.UserCode, user.CompanyCode, user.EmployeeCode);
                if (code > 0) return code;
            }

            // 3) Some deployments keep consultant code equal to user/employee code.
            if (ConsultantExists(conn, user.UserCode, user.CompanyCode))
                return user.UserCode;
            if (user.EmployeeCode > 0 && ConsultantExists(conn, user.EmployeeCode, user.CompanyCode))
                return user.EmployeeCode;

            // 4) Last resort: exact full-name match.
            if (!string.IsNullOrWhiteSpace(user.UserName))
            {
                using (var cmd = new SqlCommand(
                    @"SELECT TOP 1 intConsultantCode
                      FROM tblConsultant
                      WHERE intCompanyCode = @companyCode
                        AND bolIsActive = 1
                        AND LTRIM(RTRIM(strFullName)) = LTRIM(RTRIM(@userName))", conn))
                {
                    cmd.Parameters.Add("@companyCode", SqlDbType.Int).Value = user.CompanyCode;
                    cmd.Parameters.Add("@userName", SqlDbType.NVarChar, 200).Value = user.UserName.Trim();
                    var value = cmd.ExecuteScalar();
                    if (value != null && value != DBNull.Value)
                        return Convert.ToInt32(value);
                }
            }

            return 0;
        }

        private static bool ConsultantExists(SqlConnection conn, int consultantCode, int companyCode)
        {
            if (consultantCode <= 0) return false;
            using (var cmd = new SqlCommand(
                @"SELECT TOP 1 1
                  FROM tblConsultant
                  WHERE intConsultantCode = @code
                    AND intCompanyCode = @companyCode
                    AND bolIsActive = 1", conn))
            {
                cmd.Parameters.Add("@code", SqlDbType.Int).Value = consultantCode;
                cmd.Parameters.Add("@companyCode", SqlDbType.Int).Value = companyCode;
                return cmd.ExecuteScalar() != null;
            }
        }

        private static int ExecuteInt(
            SqlConnection conn,
            string sql,
            int userCode,
            int companyCode,
            int? empCode)
        {
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add("@userCode", SqlDbType.Int).Value = userCode;
                cmd.Parameters.Add("@companyCode", SqlDbType.Int).Value = companyCode;
                if (empCode.HasValue)
                    cmd.Parameters.Add("@empCode", SqlDbType.Int).Value = empCode.Value;

                var value = cmd.ExecuteScalar();
                if (value == null || value == DBNull.Value) return 0;
                return Convert.ToInt32(value);
            }
        }

        private static string FirstExistingColumn(SqlConnection conn, string tableName, params string[] candidates)
        {
            foreach (var column in candidates)
            {
                if (ColumnExists(conn, tableName, column))
                    return column;
            }
            return null;
        }

        private static bool ColumnExists(SqlConnection conn, string tableName, string columnName)
        {
            using (var cmd = new SqlCommand(
                @"SELECT 1
                  FROM INFORMATION_SCHEMA.COLUMNS
                  WHERE TABLE_NAME = @tableName
                    AND COLUMN_NAME = @columnName", conn))
            {
                cmd.Parameters.Add("@tableName", SqlDbType.NVarChar, 128).Value = tableName;
                cmd.Parameters.Add("@columnName", SqlDbType.NVarChar, 128).Value = columnName;
                return cmd.ExecuteScalar() != null;
            }
        }

        private static ERUserRoleModel MapUserIdentity(SqlDataReader rdr)
        {
            return new ERUserRoleModel
            {
                UserCode    = Convert.ToInt32(rdr["intUserCode"]),
                LoginName   = rdr["strLoginName"].ToString(),
                UserName    = rdr["strUserName"].ToString(),
                CompanyCode = Convert.ToInt32(rdr["intCompanyCode"])
            };
        }

        private static ERUserRoleModel MapRole(SqlDataReader rdr)
        {
            return new ERUserRoleModel
            {
                UserCode    = Convert.ToInt32(rdr["intUserCode"]),
                LoginName   = rdr["strLoginName"].ToString(),
                UserName    = rdr["strUserName"].ToString(),
                CompanyCode = Convert.ToInt32(rdr["intCompanyCode"]),
                MO          = Convert.ToBoolean(rdr["MO"]),
                Nursing     = Convert.ToBoolean(rdr["Nursing"]),
                Pharmacy    = Convert.ToBoolean(rdr["Pharmacy"]),
                Billing     = Convert.ToBoolean(rdr["Billing"])
            };
        }
    }
}
