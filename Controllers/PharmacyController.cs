using ERPaperless.Abstractions.Application;
using ERPaperless.Abstractions.Security;
using ERPaperless.Application.Services;
using ERPaperless.Filters;
using ERPaperless.Models;
using ERPaperless.Services;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Web.Mvc;

namespace ERPaperless.Controllers
{
    public class PharmacyController : BaseController
    {
        private readonly IPharmacyService _pharmacyService;

        public PharmacyController(
            IPharmacyService pharmacyService = null,
            IPatientWorkspaceService patientWorkspaceService = null,
            ICurrentUserContext currentUserContext = null)
            : base(currentUserContext, patientWorkspaceService)
        {
            _pharmacyService = pharmacyService ?? new PharmacyService();
        }

        [ERPageMode("Pharmacy")]
        public ActionResult Index()
        {
            return RedirectToAction("PatientForm");
        }

        [ERPageMode("Pharmacy")]
        public ActionResult RefreshOrders()
        {
            SetupPharmacyWorkViewBag();

            var orders = _pharmacyService.GetAllOrders(
                CurrentUserRole.CompanyCode,
                ErBranchCode,
                CurrentUserRole.UserCode,
                pendingOnly: false);

            return PartialView("~/Views/Pharmacy/_PharmacyIndexGrids.cshtml",
                PharmacyPatientFormViewModel.CreateAll(orders));
        }

        [ERPageMode("Pharmacy")]
        public ActionResult PatientForm(string patientId, string returnTo = null)
        {
            ApplyReturnContext(returnTo);
            SetupPharmacyWorkViewBag();

            if (string.IsNullOrWhiteSpace(patientId))
            {
                var allOrders = _pharmacyService.GetAllOrders(
                    CurrentUserRole.CompanyCode,
                    ErBranchCode,
                    CurrentUserRole.UserCode,
                    pendingOnly: false);

                return View(PharmacyPatientFormViewModel.CreateAll(allOrders));
            }

            var patient = BuildPatientFormContext(patientId);
            if (patient == null)
            {
                TempData["AssignError"] = BuildPatientAccessError();
                return RedirectToAction("Patients", "Home");
            }

            UsePatientWorkspaceLayout("pharmacy");
            ViewBag.PatientFormContext = patient;
            ViewBag.ErPatientId = patient.PatientId;

            var orders = _pharmacyService.GetOrdersForPatient(
                patient.PatientId,
                CurrentUserRole.CompanyCode);

            return View(PharmacyPatientFormViewModel.Create(patient, orders));
        }

        private void SetupPharmacyWorkViewBag()
        {
            ViewBag.HideSidebar = true;
            ViewBag.ActivePatientForm = "pharmacy";
            ViewBag.CanChargePharmacyOrders = CurrentUserRole.Pharmacy;
            if (CurrentUserRole.Pharmacy)
            {
                ViewBag.CanWorkOnPage = true;
                ViewBag.PageModeLabel = "Pharmacy Work Mode";
            }
        }

        [ERPageMode("Pharmacy")]
        [HttpPost]
        public ActionResult AcknowledgePackageOrder()
        {
            if (!CurrentUserRole.Pharmacy)
                return Json(new { ok = false, message = "Only Pharmacy users can charge items." });

            AcknowledgePackageOrderInput input;
            try
            {
                Request.InputStream.Position = 0;
                using (var reader = new StreamReader(Request.InputStream))
                    input = JsonConvert.DeserializeObject<AcknowledgePackageOrderInput>(reader.ReadToEnd());
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(PharmacyController), nameof(AcknowledgePackageOrder), ex);
                return Json(new { ok = false, message = "Invalid request." });
            }

            if (input == null || input.OrderId <= 0 || string.IsNullOrWhiteSpace(input.PatientId))
                return Json(new { ok = false, message = "Package order record is missing." });

            var ack = _pharmacyService.AcknowledgePackageOrder(
                input.OrderId,
                input.PatientId,
                CurrentUserRole.CompanyCode,
                CurrentUserRole.UserCode,
                CurrentUserRole.UserName);

            if (!ack.Ok || ack.Data == null)
            {
                return Json(new
                {
                    ok = false,
                    message = ack.Message ?? "Could not charge item."
                });
            }

            return Json(new
            {
                ok = true,
                message = ack.Message ?? "Item charged / issued.",
                order = MapPackageOrderJson(ack.Data)
            });
        }

        [ERPageMode("Pharmacy")]
        [HttpPost]
        public ActionResult AcknowledgeMedicine()
        {
            return AcknowledgePackageOrder();
        }

        private static object MapPackageOrderJson(PackageOrderRowViewModel order)
        {
            return new
            {
                id = order.Id,
                discontinue = order.Discontinue,
                isPharmacyCharged = order.IsPharmacyCharged,
                ackByName = order.AckByName,
                ackDate = order.AckDate?.ToString("dd-MMM-yyyy")
            };
        }

        private class AcknowledgePackageOrderInput
        {
            public string PatientId { get; set; }
            public long OrderId { get; set; }
        }

    }
}
