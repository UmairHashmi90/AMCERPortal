using ERPaperless.Abstractions.Application;
using ERPaperless.Abstractions.Security;
using ERPaperless.Application.Services;
using ERPaperless.Models;
using System.Linq;
using System.Web.Mvc;

namespace ERPaperless.Controllers
{
    public class ReportsController : BaseController
    {
        private readonly IReportsPortalService _reportsPortalService;

        public ReportsController(
            IReportsPortalService reportsPortalService = null,
            IPatientWorkspaceService patientWorkspaceService = null,
            ICurrentUserContext currentUserContext = null)
            : base(currentUserContext, patientWorkspaceService)
        {
            _reportsPortalService = reportsPortalService ?? new ReportsPortalService();
        }

        public ActionResult Index(int admissionCode)
        {
            if (IsReportsAccessBlocked())
                return ReportsAccessDenied();
            return PatientModuleView(admissionCode, "reports", "Reports");
        }

        public ActionResult Prescription(int admissionCode)
        {
            if (IsReportsAccessBlocked())
                return ReportsAccessDenied();
            return PatientModuleView(admissionCode, "prescription", "Prescription");
        }

        public ActionResult ViewReport(long patientOrderDetailCode, int reportTypeCode, int admissionCode)
        {
            if (IsReportsAccessBlocked())
                return ReportsAccessDenied();
            if (patientOrderDetailCode <= 0)
                return RedirectToAction("Index", new { admissionCode });

            if (reportTypeCode == 3)
                return RedirectToAction("Open", "Dicom", new { patientOrderDetailCode });

            var encryptedCode = _reportsPortalService.EncryptCode(patientOrderDetailCode);
            var url = Url.Content("~/api/GetReport?q=" + encryptedCode);
            return Redirect(url);
        }

        public ActionResult ViewPrescription(long code, int admissionCode)
        {
            if (IsReportsAccessBlocked())
                return ReportsAccessDenied();
            if (code <= 0)
                return RedirectToAction("Prescription", new { admissionCode });

            var encryptedCode = _reportsPortalService.EncryptCode(code);
            var url = Url.Content("~/api/GetPrescriptionReport?q=" + encryptedCode);
            return Redirect(url);
        }

        private ActionResult PatientModuleView(int admissionCode, string activeForm, string title)
        {
            if (IsReportsAccessBlocked())
                return ReportsAccessDenied();

            var patient = BuildPatientFormContextFromAdmission(admissionCode);
            if (patient == null)
            {
                TempData["AssignError"] = "Patient admission record not found.";
                return RedirectToAction("Patients", "Home");
            }

            UsePatientWorkspaceLayout(activeForm);
            ViewBag.PatientFormContext = patient;
            ViewBag.AdmissionCode = admissionCode;

            var model = new ReportsPatientFormViewModel
            {
                Patient = patient,
                AdmissionCode = admissionCode
            };

            if (activeForm == "reports")
            {
                model.Investigations = _reportsPortalService
                    .GetInvestigationsForAdmission(admissionCode, CurrentUserRole.CompanyCode)
                    .Select(x => new ReportInvestigationRowViewModel
                    {
                        ReportTypeCode = x.ReportTypeCode,
                        PatientOrderDetailCode = x.PatientOrderDetailCode,
                        OrderDate = x.OrderDate,
                        Test = x.Test,
                        Status = x.Status
                    })
                    .ToList();
            }
            else if (activeForm == "prescription")
            {
                model.Prescriptions = _reportsPortalService
                    .GetPrescriptionsForAdmission(admissionCode, CurrentUserRole.CompanyCode)
                    .Select(x => new PrescriptionVisitRowViewModel
                    {
                        Code = x.Code,
                        VisitDate = x.VisitDate,
                        Consultant = x.Consultant,
                        Speciality = x.Speciality
                    })
                    .ToList();
            }

            ViewBag.Title = title + " – " + patient.PatientName;
            return View(title == "Prescription" ? "Prescription" : "Index", model);
        }

        private bool IsReportsAccessBlocked()
        {
            var isNursingOnly = CurrentUserRole.Nursing
                && !CurrentUserRole.MO
                && !CurrentUserRole.Pharmacy
                && !CurrentUserRole.Billing;
            return CurrentUserRole.IsBillingOnly
                || CurrentUserRole.IsPharmacyOnly
                || isNursingOnly;
        }

        private ActionResult ReportsAccessDenied()
        {
            TempData["AccessError"] = "You do not have permission to open Reports or Prescriptions.";
            return RedirectToAction("Patients", "Home");
        }

    }
}
