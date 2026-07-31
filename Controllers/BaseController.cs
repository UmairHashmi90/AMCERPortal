using ERPaperless.Abstractions.Application;
using ERPaperless.Abstractions.Security;
using ERPaperless.Application.Services;
using ERPaperless.Infrastructure.Security;
using ERPaperless.Models;
using ERPaperless.Services;
using System.Configuration;
using System.Web.Mvc;

namespace ERPaperless.Controllers
{
    /// <summary>
    /// All authenticated controllers inherit from this base.
    /// Loads ERUserRoleModel from Session (or re-queries DB on first request
    /// after login / session timeout) and publishes role flags to ViewBag.
    /// </summary>
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

        /// <summary>
        /// Branch code read from Web.config key "ER:BranchCode".
        /// Default = 1 if not set.
        /// </summary>
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

            // Session is populated by AccountController at login time after
            // procGetRoleRightForERPortal is called with user + company code.
            CurrentUserRole = _currentUserContext.GetCurrentUser(
                HttpContext,
                User?.Identity?.Name);

            // ── Push to ViewBag so every view / partial can read it ──────────
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
            ViewBag.CompanyCode    = CurrentUserRole.CompanyCode;
            ViewBag.BranchCode     = ErBranchCode;
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
            if (key == "pharmacy" || key == "billing")
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
