using ERPaperless.Models;
using System;
using System.Diagnostics;
using System.Web;

namespace ERPaperless.Services
{
    public static class ErrorLogging
    {
        public static void Log(Exception ex)
        {
            if (ex == null) return;

            var trace = new StackTrace(ex, true);
            var frame = trace.GetFrame(0);
            var method = frame?.GetMethod();
            var formName = method?.DeclaringType?.FullName ?? "Unknown";
            var methodName = method?.Name ?? "Unknown";
            WriteLog(formName, methodName, ex);
        }

        public static void Log(string strControllerName, string strMethodName, Exception ex)
        {
            if (ex == null) return;
            WriteLog(strControllerName ?? "Unknown", strMethodName ?? "Unknown", ex);
        }

        private static void WriteLog(string formName, string methodName, Exception ex)
        {
            try
            {
                var userCode = 1;
                var companyCode = 1;

                if (HttpContext.Current?.Session?["UserRole"] is ERUserRoleModel role)
                {
                    if (role.UserCode > 0) userCode = role.UserCode;
                    if (role.CompanyCode > 0) companyCode = role.CompanyCode;
                }

                using (var db = dbAMCEntities.Create())
                {
                    db.tblExceptions.Add(new tblException
                    {
                        strFormName = formName,
                        strMethod = methodName,
                        strInnerException = ex.InnerException?.ToString()
                            ?? ex.TargetSite?.ToString(),
                        strDetail = ex.Message + Environment.NewLine + ex.StackTrace,
                        strSystemIP = GetClientIp(),
                        strSystemName = Environment.MachineName + " " + Environment.UserName,
                        dtmException = DateTime.Now,
                        intUserCode = userCode,
                        intCompanyCode = companyCode
                    });
                    db.SaveChanges();
                }
            }
            catch
            {
                // Never throw from the logger.
            }
        }

        private static string GetClientIp()
        {
            try
            {
                var request = HttpContext.Current?.Request;
                if (request == null)
                    return Environment.MachineName;

                var forwarded = request.ServerVariables["HTTP_X_FORWARDED_FOR"];
                if (!string.IsNullOrWhiteSpace(forwarded))
                {
                    var parts = forwarded.Split(',');
                    return parts[0].Trim();
                }

                return request.UserHostAddress ?? Environment.MachineName;
            }
            catch
            {
                return Environment.MachineName;
            }
        }
    }
}
