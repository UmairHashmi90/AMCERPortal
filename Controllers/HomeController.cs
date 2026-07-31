using ERPaperless.Filters;
using ERPaperless.Models;
using ERPaperless.Services;
using System.Linq;
using System.Web.Mvc;

namespace ERPaperless.Controllers
{
    public class HomeController : BaseController
    {
        // ── Dashboard ────────────────────────────────────────────────────────
        public ActionResult Index()
        {
            return View();
        }

        // ── Patients / Beds Screen ───────────────────────────────────────────
        public ActionResult Patients()
        {
            ViewBag.HideSidebar = true;

            var all = BedRepository.GetBeds(
                companyCode: CurrentUserRole.CompanyCode,
                branchCode:  ErBranchCode,
                userCode:    CurrentUserRole.UserCode);

            // Housekeeping (status-purple) and Closed (status-gray) beds
            // are maintenance states — hide them from the clinical view.
            var beds = all
                .Where(b => b.StateClass != "status-purple" &&
                            b.StateClass != "status-gray")
                .ToList();

            return View(beds);
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
