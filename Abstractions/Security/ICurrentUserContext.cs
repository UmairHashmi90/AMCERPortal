using ERPaperless.Models;
using System.Web;

namespace ERPaperless.Abstractions.Security
{
    public interface ICurrentUserContext
    {
        ERUserRoleModel GetCurrentUser(HttpContextBase httpContext, string fallbackLoginName = null);
        void SetCurrentUser(HttpSessionStateBase session, ERUserRoleModel user);
        void ClearCurrentUser(HttpSessionStateBase session);
    }
}
