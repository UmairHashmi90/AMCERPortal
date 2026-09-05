using System.Security.Principal;
using System.Web.Mvc;
using System.Web.Security;

namespace ERPaperless.Filters
{
    /// <summary>
    /// Anti-forgery check for the anonymous Login POST.
    /// If a stale FormsAuth cookie still says the user is authenticated (e.g. browser Back
    /// after login, or leftover cookie), ASP.NET MVC rejects an antiforgery token that was
    /// generated for "" while Identity.Name is "admin". Clear identity for this request first.
    /// </summary>
    public sealed class ValidateAnonymousAntiForgeryTokenAttribute : FilterAttribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationContext filterContext)
        {
            if (filterContext == null)
                return;

            var httpContext = filterContext.HttpContext;
            if (httpContext?.User?.Identity != null && httpContext.User.Identity.IsAuthenticated)
            {
                FormsAuthentication.SignOut();
                httpContext.User = new GenericPrincipal(new GenericIdentity(string.Empty), new string[0]);
            }

            new ValidateAntiForgeryTokenAttribute().OnAuthorization(filterContext);
        }
    }
}
