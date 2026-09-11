using ERPaperless.Filters;
using ERPaperless.Models;
using ERPaperless.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace ERPaperless.Controllers
{
    public class HomeController : BaseController
    {
        // ── Dashboard ────────────────────────────────────────────────────────
        public ActionResult Index()
        {
            ViewBag.HideSidebar = true;

            var model = new DashboardViewModel
            {
                Cards = DashboardRepository.GetDashboardCards(CurrentUserRole.CompanyCode)
            };

            return View(model);
        }

        // ── Patients / Beds Screen ───────────────────────────────────────────
        public ActionResult Patients()
        {
            ViewBag.HideSidebar = true;
            return View(GetVisibleBeds());
        }

        [HttpGet]
        public ActionResult PatientsGrid()
        {
            return PartialView("_LocationGrid", GetVisibleBeds());
        }

        [RequireERRole("Billing")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CancelPendingMr(int bedId, int branchCode)
        {
            if (!CurrentUserRole.Billing)
                return Json(new { ok = false, message = "Only Billing users can cancel Pending MR patients." });

            if (bedId <= 0 || branchCode <= 0)
                return Json(new { ok = false, message = "Invalid bed selection." });

            var ok = ERPatientRepository.CancelPendingMr(
                bedId,
                branchCode,
                CurrentUserRole.CompanyCode,
                CurrentUserRole.UserCode);

            if (!ok)
            {
                return Json(new
                {
                    ok = false,
                    message = ERPatientRepository.LastError ?? "Could not cancel Pending MR patient."
                });
            }

            return Json(new
            {
                ok = true,
                message = "Pending MR cancelled. Bed is now available."
            });
        }

        private List<LocationCardViewModel> GetVisibleBeds()
        {
            var all = BedRepository.GetBeds(
                companyCode: CurrentUserRole.CompanyCode,
                branchCode: ErBranchCode,
                userCode: CurrentUserRole.UserCode);

            return (all ?? new List<LocationCardViewModel>())
                .Where(b => b.StateClass != "status-purple" &&
                            b.StateClass != "status-gray")
                .ToList();
        }

        [RequireERRole("MO", "Nursing")]
        public ActionResult DischargedPatients(string q, int page = 1)
        {
            if (!CurrentUserRole.MO && !CurrentUserRole.Nursing)
            {
                TempData["AccessError"] = "Only MO and Nursing can view discharged patients.";
                return RedirectToAction("Patients");
            }

            ViewBag.HideSidebar = true;
            ViewBag.IsDischargedPatientsPage = true;

            const int pageSize = 12;
            var search = (q ?? string.Empty).Trim();
            var all = BedRepository.GetDischargedPatients(
                companyCode: CurrentUserRole.CompanyCode,
                branchCode: ErBranchCode) ?? new List<LocationCardViewModel>();

            IEnumerable<LocationCardViewModel> filtered = all;
            if (!string.IsNullOrWhiteSpace(search))
            {
                filtered = all.Where(p =>
                    ContainsDischargedSearch(p.PatientName, search)
                    || ContainsDischargedSearch(p.MrNo, search)
                    || ContainsDischargedSearch(p.AdmissionNo, search));
            }

            var matched = filtered.ToList();
            var totalCount = matched.Count;
            var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);
            if (page < 1) page = 1;
            if (page > totalPages) page = totalPages;

            var pageItems = matched
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return View(new DischargedPatientsPageViewModel
            {
                Patients = pageItems,
                Search = search,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            });
        }

        private static bool ContainsDischargedSearch(string value, string search)
        {
            if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(search))
                return false;
            if (value == "-" || string.Equals(value, "PENDING", StringComparison.OrdinalIgnoreCase))
                return false;
            return value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ── Assign a patient to an available bed  [MO / Nursing] ─────────────
        [RequireERRole("MO", "Nursing")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AssignPatient(AssignPatientInputViewModel model)
        {
            if (!CurrentUserRole.MO && !CurrentUserRole.Nursing)
            {
                TempData["AssignError"] = "You do not have permission to assign patients.";
                return RedirectToAction("Patients");
            }

            if (model == null || model.BedId <= 0 ||
                string.IsNullOrWhiteSpace(model.PatientName))
            {
                TempData["AssignError"] = "Patient name and a valid bed are required.";
                return RedirectToAction("Patients");
            }

            var ok = BedRepository.AssignPatient(
                model,
                companyCode:    CurrentUserRole.CompanyCode,
                userCode:       CurrentUserRole.UserCode,
                assignedByName: User.Identity.Name);

            TempData[ok ? "AssignSuccess" : "AssignError"] = ok
                ? $"{model.PatientName} has been assigned successfully."
                : "Could not assign the patient. Please try again.";

            return RedirectToAction("Patients");
        }

        // ── Mark unoccupied bed as Pending MR and open its ER Form ──────────
        [RequireERRole("MO", "Nursing")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MarkBedPending(int bedId, int branchCode, string patientName)
        {
            if (!CurrentUserRole.MO && !CurrentUserRole.Nursing)
            {
                TempData["AssignError"] = "You do not have permission to open beds.";
                return RedirectToAction("Patients");
            }

            if (bedId <= 0 || branchCode <= 0)
            {
                TempData["AssignError"] = "Invalid bed selection.";
                return RedirectToAction("Patients");
            }

            var patient = ERPatientRepository.GetOrCreatePendingPatient(
                bedId,
                branchCode,
                CurrentUserRole.CompanyCode,
                CurrentUserRole.UserCode,
                patientName);

            if (patient == null)
            {
                TempData["AssignError"] = "Could not create ER patient. " +
                    (ERPatientRepository.LastError ?? "Please try again.");
                return RedirectToAction("Patients");
            }

            var ok = BedRepository.MarkBedAsPendingMR(
                bedId,
                CurrentUserRole.CompanyCode,
                CurrentUserRole.UserCode);

            if (!ok)
            {
                TempData["AssignError"] =
                    "ER patient was created but bed status could not be updated. " +
                    "Please open View Form from the bed card.";
                return RedirectToAction("ErForm", "EmergencyForm",
                    new { patientId = patient.intERPatientCode });
            }

            return RedirectToAction("ErForm", "EmergencyForm", new { patientId = patient.intERPatientCode });
        }

        // ── Static pages ─────────────────────────────────────────────────────
        public ActionResult About()   { return View(); }
        public ActionResult Contact() { return View(); }

    }
}
