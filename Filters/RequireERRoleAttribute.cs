using ERPaperless.Abstractions.Security;
using ERPaperless.Infrastructure.Security;
using ERPaperless.Models;
using System;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace ERPaperless.Filters
{
    /// <summary>
    /// Restricts an action (or controller) to users with the specified ER role(s).
    ///
    /// Usage examples:
    ///   [RequireERRole]                          — any role (MO, Nursing, Pharmacy, Billing)
    ///   [RequireERRole("MO")]                    — MO only
    ///   [RequireERRole("Nursing")]               — Nursing only
    ///   [RequireERRole("MO", "Nursing")]         — MO or Nursing
    ///   [RequireERRole("Pharmacy", "Billing")]   — Pharmacy or Billing
    ///
    /// On failure: sets TempData["AccessError"] and redirects to Home/Patients.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class RequireERRoleAttribute : AuthorizeAttribute
    {
        private readonly ICurrentUserContext _currentUserContext = new CurrentUserContext();
        private readonly string[] _requiredRoles;

        /// <param name="roles">
        /// Zero or more role names: "MO", "Nursing", "Pharmacy", "Billing".
        /// Pass nothing to require any role (view-only users are denied).
        /// </param>
        public RequireERRoleAttribute(params string[] roles)
        {
            _requiredRoles = roles ?? Array.Empty<string>();
        }

        // ── Core authorization logic ─────────────────────────────────────────
        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            // Must be authenticated first.
            if (!base.AuthorizeCore(httpContext))
                return false;
            var role = _currentUserContext.GetCurrentUser(httpContext);

            if (role == null) return false;
            if (_requiredRoles.Length == 0)
                return role.HasAnyRole;
            foreach (var r in _requiredRoles)
            {
                switch (r.Trim().ToUpperInvariant())
                {
                    case "MO":       if (role.MO)       return true; break;
                    case "NURSING":  if (role.Nursing)  return true; break;
                    case "PHARMACY": if (role.Pharmacy) return true; break;
                    case "BILLING":  if (role.Billing)  return true; break;
                }
            }

            return false;
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            var roleList = _requiredRoles.Length > 0
                ? string.Join(" or ", _requiredRoles)
                : "an authorized role";

            filterContext.Controller.TempData["AccessError"] =
                $"Access denied. This action requires the {roleList} role.";

            filterContext.Result = new RedirectToRouteResult(
                new RouteValueDictionary
                {
                    { "controller", "Home" },
                    { "action",     "Patients" }
                });
        }
    }
}
