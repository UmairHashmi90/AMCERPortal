using ERPaperless.Models;
using System;
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

                        return GetUserRights(user.UserCode, user.CompanyCode) ?? user;
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
                            return MapRole(rdr);
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
