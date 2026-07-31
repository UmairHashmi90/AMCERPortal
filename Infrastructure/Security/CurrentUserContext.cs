using ERPaperless.Abstractions.Security;
using ERPaperless.Models;
using System.Web;

namespace ERPaperless.Infrastructure.Security
{
    public class CurrentUserContext : ICurrentUserContext
    {
        public ERUserRoleModel GetCurrentUser(HttpContextBase httpContext, string fallbackLoginName = null)
        {
            var role = httpContext?.Session?["UserRole"] as ERUserRoleModel;
            if (role != null) return role;

            return new ERUserRoleModel
            {
                LoginName = fallbackLoginName,
                CompanyCode = 1
            };
        }

        public void SetCurrentUser(HttpSessionStateBase session, ERUserRoleModel user)
        {
            if (session == null) return;
            session["UserRole"] = user;
        }

        public void ClearCurrentUser(HttpSessionStateBase session)
        {
            if (session == null) return;
            session.Remove("UserRole");
        }
    }
}
