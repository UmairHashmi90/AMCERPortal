using ERPaperless.Abstractions.Application;
using ERPaperless.Abstractions.Security;
using ERPaperless.Application.Services;
using ERPaperless.Infrastructure.Security;
using ERPaperless.Models;
using ERPaperless.Services;
using System.Configuration;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Security;

namespace ERPaperless.Controllers
{
    [Authorize]
    public abstract class BaseController : Controller
    {
        private readonly ICurrentUserContext _currentUserContext;
        private readonly IPatientWorkspaceService _patientWorkspaceService;
        protected ERUserRoleModel CurrentUserRole { get; private set; }

        protected BaseController(
            ICurrentUserContext currentUserContext = null,
            IPatientWorkspaceService patientWorkspaceService = null)
        {
            _currentUserContext = currentUserContext ?? new CurrentUserContext();
            _patientWorkspaceService = patientWorkspaceService ?? new PatientWorkspaceService();
        }

     
        protected int ErBranchCode
        {
            get
            {
                if (int.TryParse(ConfigurationManager.AppSettings["ER:BranchCode"], out var code)
                    && code > 0)
                    return code;
                return 1;
            }
        }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            base.OnActionExecuting(filterContext);

            PreventBrowserCache();

            CurrentUserRole = _currentUserContext.GetCurrentUser(
                HttpContext,
                User?.Identity?.Name);

            if (CurrentUserRole == null || CurrentUserRole.UserCode <= 0)
            {
                FormsAuthentication.SignOut();
                Session?.Clear();
                _currentUserContext.ClearCurrentUser(Session);
                filterContext.Result = new RedirectToRouteResult(
                    new RouteValueDictionary
                    {
                        { "controller", "Account" },
                        { "action", "Login" }
                    });
                return;
            }

             if (CurrentUserRole != null
                && CurrentUserRole.UserCode > 0
                && !CurrentUserRole.IdentityLinksResolved)
            {
                UserRepository.EnrichIdentityLinks(CurrentUserRole);
                _currentUserContext.SetCurrentUser(Session, CurrentUserRole);
            }

            ViewBag.UserRole       = CurrentUserRole;
            ViewBag.CanEdit        = CurrentUserRole.MO || CurrentUserRole.Nursing;
            ViewBag.CanAssignBed   = CurrentUserRole.MO || CurrentUserRole.Nursing;
            ViewBag.CanEditErForm  = CurrentUserRole.MO || CurrentUserRole.Nursing;
            ViewBag.CanWorkOnPage  = false;
            ViewBag.PageModeLabel  = "View Only";
            ViewBag.IsMO           = CurrentUserRole.MO;
            ViewBag.IsNursing      = CurrentUserRole.Nursing;
            ViewBag.IsPharmacy     = CurrentUserRole.Pharmacy;
            ViewBag.IsBilling      = CurrentUserRole.Billing;
            ViewBag.HasMultipleWorkRoles = CurrentUserRole.HasMultipleWorkRoles;
            ViewBag.IsPharmacyOnly = CurrentUserRole.IsPharmacyOnly;
            ViewBag.IsBillingOnly  = CurrentUserRole.IsBillingOnly;
            ViewBag.IsClinicalUser = CurrentUserRole.IsClinicalUser;
            ViewBag.ShowPharmacyOnBedCard = CurrentUserRole.ShowPharmacyOnBedCard;
            ViewBag.ShowBillingOnBedCard  = CurrentUserRole.ShowBillingOnBedCard;
            ViewBag.RoleLabel      = CurrentUserRole.RoleLabel;
            ViewBag.RoleBadge      = CurrentUserRole.RoleBadgeClass;
            ViewBag.UserFullName   = CurrentUserRole.UserName;
            ViewBag.UserCode       = CurrentUserRole.UserCode;
            ViewBag.EmployeeCode   = CurrentUserRole.EmployeeCode;
            ViewBag.ConsultantCode = CurrentUserRole.ConsultantCode;
            ViewBag.CompanyCode    = CurrentUserRole.CompanyCode;
            ViewBag.BranchCode     = ErBranchCode;
        }

        private void PreventBrowserCache()
        {
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();
            Response.Cache.SetRevalidation(HttpCacheRevalidation.AllCaches);
            Response.Cache.SetExpires(System.DateTime.UtcNow.AddDays(-1));
            Response.AppendHeader("Pragma", "no-cache");
        }

        protected void UsePatientWorkspaceLayout(string activeForm)
        {
            ViewBag.HideSidebar = true;
            ViewBag.ActivePatientForm = activeForm;
        }

        protected void ApplyReturnContext(string returnTo)
        {
            if (string.IsNullOrWhiteSpace(returnTo))
                return;

            var key = returnTo.Trim().ToLowerInvariant();
            if (key == "pharmacy" || key == "billing" || key == "discharged")
                ViewBag.ReturnTo = key;
        }

        protected PatientFormContextViewModel BuildPatientFormContext(string patientId)
        {
            if (string.IsNullOrWhiteSpace(patientId)) return null;
            return _patientWorkspaceService.BuildByPatientId(
                patientId,
                CurrentUserRole.CompanyCode,
                CurrentUserRole.UserCode);
        }

        protected string BuildPatientAccessError()
        {
            return string.IsNullOrWhiteSpace(_patientWorkspaceService.LastError)
                ? "Patient record not found or has been discharged."
                : "Patient record not found: " + _patientWorkspaceService.LastError;
        }

        protected PatientFormContextViewModel BuildPatientFormContextFromAdmission(int admissionCode)
        {
            return _patientWorkspaceService.BuildByAdmissionCode(
                admissionCode,
                CurrentUserRole.CompanyCode,
                CurrentUserRole.UserCode);
        }
    }
}
