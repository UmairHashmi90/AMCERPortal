using ERPaperless.Abstractions.Security;
using ERPaperless.Infrastructure.Security;
using ERPaperless.Models;
using System;
using System.Web.Mvc;

namespace ERPaperless.Filters
{
    /// <summary>
    /// Marks a page as editable/workable only for one ER role while keeping it
    /// visible to the other authenticated roles in view-only mode.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ERPageModeAttribute : ActionFilterAttribute
    {
        private readonly ICurrentUserContext _currentUserContext = new CurrentUserContext();
        private readonly string _workRole;

        public ERPageModeAttribute(string workRole)
        {
            _workRole = (workRole ?? string.Empty).Trim();
        }

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var role = _currentUserContext.GetCurrentUser(filterContext.HttpContext);
            var canWork = HasRole(role, _workRole);

            filterContext.Controller.ViewBag.PageModeRole = _workRole;
            filterContext.Controller.ViewBag.CanWorkOnPage = canWork;
            filterContext.Controller.ViewBag.PageModeLabel = canWork
                ? _workRole + " Work Mode"
                : "View Only";

            filterContext.Controller.ViewBag.CanWorkOnBilling = role?.Billing == true;
            filterContext.Controller.ViewBag.CanWorkOnPharmacy = role?.Pharmacy == true;

            base.OnActionExecuting(filterContext);
        }

        private static bool HasRole(ERUserRoleModel role, string roleName)
        {
            if (role == null || string.IsNullOrWhiteSpace(roleName)) return false;

            switch (roleName.Trim().ToUpperInvariant())
            {
                case "MO":       return role.MO;
                case "NURSING":  return role.Nursing;
                case "PHARMACY": return role.Pharmacy;
                case "BILLING":  return role.Billing;
                default:          return false;
            }
        }
    }
}
