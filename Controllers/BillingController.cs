using ERPaperless.Abstractions.Application;
using ERPaperless.Abstractions.Security;
using ERPaperless.Application.Services;
using ERPaperless.Filters;
using ERPaperless.Models;
using ERPaperless.Services;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Web.Mvc;

namespace ERPaperless.Controllers
{
    public class BillingController : BaseController
    {
        private readonly IBillingService _billingService;

        public BillingController(
            IBillingService billingService = null,
            IPatientWorkspaceService patientWorkspaceService = null,
            ICurrentUserContext currentUserContext = null)
            : base(currentUserContext, patientWorkspaceService)
        {
            _billingService = billingService ?? new BillingService();
        }

        [ERPageMode("Billing")]
        public ActionResult Index()
        {
            return RedirectToAction("PatientForm");
        }

        [ERPageMode("Billing")]
        public ActionResult RefreshInvestigations()
        {
            SetupBillingWorkViewBag();
            ViewBag.ShowPatientColumns = true;

            var investigations = _billingService.GetAllInvestigations(
                CurrentUserRole.CompanyCode,
                ErBranchCode,
                CurrentUserRole.UserCode,
                pendingOnly: false);

            return PartialView("~/Views/Billing/_BillingInvestigationsBody.cshtml", investigations);
        }

        [ERPageMode("Billing")]
        public ActionResult PatientForm(string patientId, string returnTo = null)
        {
            ApplyReturnContext(returnTo);
            SetupBillingWorkViewBag();

            if (string.IsNullOrWhiteSpace(patientId))
            {
                LoadChangeBedLists();
                ViewBag.ShowPatientColumns = true;

                var allInvestigations = _billingService.GetAllInvestigations(
                    CurrentUserRole.CompanyCode,
                    ErBranchCode,
                    CurrentUserRole.UserCode,
                    pendingOnly: false);

                return View(BillingPatientFormViewModel.CreateAll(allInvestigations));
            }

            var patient = BuildPatientFormContext(patientId);
            if (patient == null)
            {
                TempData["AssignError"] = BuildPatientAccessError();
                return RedirectToAction("Patients", "Home");
            }

            UsePatientWorkspaceLayout("billing");
            ViewBag.PatientFormContext = patient;
            ViewBag.ErPatientId = patient.PatientId;
            ViewBag.ShowPatientColumns = false;
            LoadChangeBedLists();

            return View(BillingPatientFormViewModel.Create(
                patient,
                _billingService.GetInvestigations(patient.PatientId, CurrentUserRole.CompanyCode)));
        }

        private void SetupBillingWorkViewBag()
        {
            ViewBag.HideSidebar = true;
            ViewBag.ActivePatientForm = "billing";
            if (CurrentUserRole.Billing)
            {
                ViewBag.CanWorkOnPage = true;
                ViewBag.PageModeLabel = "Billing Work Mode";
            }
        }

        [ERPageMode("Billing")]
        [HttpPost]
        public ActionResult AcknowledgeInvestigation()
        {
            if (!CurrentUserRole.Billing)
                return Json(new { ok = false, message = "Only Billing users can acknowledge investigations." });

            AcknowledgeInvestigationInput input;
            try
            {
                Request.InputStream.Position = 0;
                using (var reader = new StreamReader(Request.InputStream))
                    input = JsonConvert.DeserializeObject<AcknowledgeInvestigationInput>(reader.ReadToEnd());
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(BillingController), nameof(AcknowledgeInvestigation), ex);
                return Json(new { ok = false, message = "Invalid request." });
            }

            if (input == null || input.InvestigationId <= 0 || string.IsNullOrWhiteSpace(input.PatientId))
                return Json(new { ok = false, message = "Investigation record is missing." });

            var ack = _billingService.AcknowledgeInvestigation(
                input.InvestigationId,
                input.PatientId,
                CurrentUserRole.CompanyCode,
                CurrentUserRole.UserCode,
                CurrentUserRole.UserName);

            if (!ack.Ok || ack.Data == null)
            {
                return Json(new
                {
                    ok = false,
                    message = ack.Message ?? "Could not acknowledge test."
                });
            }

            return Json(new
            {
                ok = true,
                message = ack.Message ?? "Test acknowledged for billing.",
                investigation = MapInvestigationJson(ack.Data)
            });
        }

        private static object MapInvestigationJson(InvestigationRowViewModel inv)
        {
            return new
            {
                id = inv.Id,
                isCancelled = inv.IsCancelled,
                isAcknowledged = inv.IsAcknowledged,
                ackByName = inv.AckByName,
                ackDate = inv.AckDate?.ToString("dd-MMM-yyyy")
            };
        }

        private class AcknowledgeInvestigationInput
        {
            public string PatientId { get; set; }
            public long InvestigationId { get; set; }
        }

        [ERPageMode("Billing")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangeBed(long erPatientCode, int newBedCode, int newBranchCode, string returnPatientId = null)
        {
            if (!CurrentUserRole.Billing)
            {
                TempData["BillingError"] = "Only Billing users can change beds.";
                return RedirectAfterChangeBed(returnPatientId);
            }

            if (erPatientCode <= 0 || newBedCode <= 0 || newBranchCode <= 0)
            {
                TempData["BillingError"] = "Please select current patient and new bed.";
                return RedirectAfterChangeBed(returnPatientId);
            }

            var changed = ERPatientRepository.ChangeBed(
                erPatientCode,
                newBedCode,
                newBranchCode,
                CurrentUserRole.CompanyCode,
                CurrentUserRole.UserCode,
                out var oldBedCode,
                out var newBedStatus);

            if (!changed)
            {
                TempData["BillingError"] = ERPatientRepository.LastError ?? "Could not change bed.";
                return RedirectAfterChangeBed(returnPatientId);
            }

            BedRepository.MarkBedStatus(
                newBedCode,
                newBedStatus,
                CurrentUserRole.CompanyCode,
                CurrentUserRole.UserCode);

            if (oldBedCode > 0)
            {
                BedRepository.MarkBedAsUnoccupied(
                    oldBedCode,
                    CurrentUserRole.CompanyCode,
                    CurrentUserRole.UserCode);
            }

            TempData["BillingSuccess"] = "Patient shifted to new bed.";
            return RedirectAfterChangeBed(returnPatientId);
        }

        private void LoadChangeBedLists()
        {
            var beds = BedRepository.GetBeds(
                companyCode: CurrentUserRole.CompanyCode,
                branchCode: ErBranchCode,
                userCode: CurrentUserRole.UserCode);

            ViewBag.ChangeBedFrom = beds
                .Where(b => b.StateClass == "status-red" || b.StateClass == "status-orange")
                .Where(b => long.TryParse(b.PatientId, out _))
                .ToList();

            ViewBag.ChangeBedTo = beds
                .Where(b => b.StateClass == "status-green")
                .ToList();
        }

        private ActionResult RedirectAfterChangeBed(string returnPatientId)
        {
            if (!string.IsNullOrWhiteSpace(returnPatientId))
                return RedirectToAction("PatientForm", new { patientId = returnPatientId });

            return RedirectToAction("PatientForm");
        }
    }
}
