using ERPaperless.Abstractions.Application;
using ERPaperless.Abstractions.Security;
using ERPaperless.Application.Services;
using ERPaperless.Filters;
using ERPaperless.Models;
using ERPaperless.Services;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Newtonsoft.Json;

namespace ERPaperless.Controllers
{
    public class EmergencyFormController : BaseController
    {
        private readonly IErFormService _erFormService;

        public EmergencyFormController(
            IErFormService erFormService = null,
            IPatientWorkspaceService patientWorkspaceService = null,
            ICurrentUserContext currentUserContext = null)
            : base(currentUserContext, patientWorkspaceService)
        {
            _erFormService = erFormService ?? new ErFormService();
        }

        public ActionResult ErForm(string patientId, string returnTo = null)
        {
            if (string.IsNullOrWhiteSpace(patientId))
            {
                TempData["AssignError"] = "No patient is assigned to that bed.";
                return RedirectToAction("Patients", "Home");
            }

            ApplyReturnContext(returnTo);
            var patientContext = BuildPatientFormContext(patientId);
            if (patientContext == null)
            {
                TempData["AssignError"] = BuildPatientAccessError();
                return RedirectToAction("Patients", "Home");
            }

            UsePatientWorkspaceLayout("er");
            var isAdmissionDischarged = IsErAdmissionDischarged(patientContext.AdmissionCode, CurrentUserRole.CompanyCode);
            ViewBag.IsMOUser = CurrentUserRole.MO;
            ViewBag.IsNursingUser = CurrentUserRole.Nursing;
            ViewBag.IsMO = CurrentUserRole.MO && !isAdmissionDischarged;
            ViewBag.IsNursing = CurrentUserRole.Nursing && !isAdmissionDischarged;
            ViewBag.PatientFormContext = patientContext;
            ViewBag.CanEditVitals = !isAdmissionDischarged && (CurrentUserRole.MO || CurrentUserRole.Nursing);
            ViewBag.CanEditClinical = !isAdmissionDischarged && CurrentUserRole.MO;
            ViewBag.CanEditSurgical = !isAdmissionDischarged && CurrentUserRole.Nursing;
            ViewBag.CanEditNursingNotes = !isAdmissionDischarged && CurrentUserRole.Nursing;
            ViewBag.CanEditNursingCarePlan = !isAdmissionDischarged && CurrentUserRole.MO;
            ViewBag.CanUploadDocuments = !isAdmissionDischarged && (CurrentUserRole.MO || CurrentUserRole.Nursing);
            ViewBag.CanWorkOnPage = !isAdmissionDischarged && (CurrentUserRole.MO || CurrentUserRole.Nursing);
            ViewBag.IsAdmissionDischarged = isAdmissionDischarged;
            ViewBag.PageModeLabel = isAdmissionDischarged
                ? "Discharged (Read Only)"
                : CurrentUserRole.MO
                ? "MO Work Mode"
                : CurrentUserRole.Nursing
                        ? "Nursing Work Mode"
                    : "View Only";
            var dischargeEligibility = EvaluateDischargeEligibility(patientContext.PatientId, patientContext.AdmissionCode);
            ViewBag.CanDischargeNow = dischargeEligibility.CanDischarge;
            ViewBag.DischargeBlockedReason = dischargeEligibility.Message;
            var printableOutcomes = GetPrintableOutcomeFlags(patientContext.AdmissionCode, CurrentUserRole.CompanyCode);
            ViewBag.CanPrintDischargeSummary = printableOutcomes.CanPrintDischarge;
            ViewBag.CanPrintDeathCertificate = printableOutcomes.CanPrintDeath;
            ViewBag.CanPrintPatientReferral = printableOutcomes.CanPrintReferral;
            ViewBag.CanPrintLama = printableOutcomes.CanPrintLama;
            ViewBag.CanPrintOutcome = printableOutcomes.CanPrintAny;

            var model = _erFormService.BuildErFormModel(
                patientContext.PatientId,
                CurrentUserRole.CompanyCode,
                CurrentUserRole.UserCode);
            if (model == null)
            {
                TempData["AssignError"] = BuildPatientAccessError();
                return RedirectToAction("Patients", "Home");
            }

            return View("~/Views/Home/ErForm.cshtml", model);
        }

        [RequireERRole("MO", "Nursing")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddVital(AddVitalInputViewModel model)
        {
            if (!CurrentUserRole.MO && !CurrentUserRole.Nursing)
            {
                const string message = "You do not have permission to add vitals.";
                if (WantsJson()) return Json(new { ok = false, message });
                TempData["VitalSaveError"] = message;
                return RedirectToAction("Patients", "Home");
            }

            if (model == null || string.IsNullOrWhiteSpace(model.PatientId))
            {
                const string message = "Patient record is missing. Please reopen the ER form.";
                if (WantsJson()) return Json(new { ok = false, message });
                return RedirectToAction("Patients", "Home");
            }

            if (IsPatientAdmissionDischarged(model.PatientId, null, CurrentUserRole.CompanyCode))
            {
                const string message = "ER form is discharged and now read-only.";
                if (WantsJson()) return Json(new { ok = false, message });
                TempData["VitalSaveError"] = message;
                return RedirectToAction("ErForm", new { patientId = model.PatientId });
            }

            if (!ModelState.IsValid)
            {
                var message = string.Join(" ",
                    ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .Where(e => !string.IsNullOrWhiteSpace(e)));
                if (string.IsNullOrWhiteSpace(message))
                    message = "Invalid vitals input. Please check the values.";

                if (WantsJson()) return Json(new { ok = false, message });
                TempData["VitalSaveError"] = message;
                return RedirectToAction("ErForm", new { patientId = model.PatientId });
            }

            var addResult = _erFormService.AddVital(
                model.PatientId,
                CurrentUserRole.CompanyCode,
                CurrentUserRole.UserCode,
                new VitalRecordViewModel
            {
                HeartRate       = model.HeartRate,
                Pulse           = model.Pulse,
                BP1             = model.BP1,
                BP2             = model.BP2,
                RespiratoryRate = model.RespiratoryRate,
                Height          = model.Height,
                Weight          = model.Weight,
                MetricBMI       = model.MetricBMI,
                Spo2            = model.Spo2,
                GlucoseF        = model.GlucoseF,
                GlucoseR        = model.GlucoseR,
                Temperature     = model.Temperature,
                FallRisk        = model.FallRisk,
                PainScore       = model.PainScore
            });
            var resultMessage = addResult.Message;

            if (WantsJson())
            {
                return Json(new
                {
                    ok = addResult.Ok,
                    message = resultMessage,
                    refreshUrl = Url.Action("ErForm", new { patientId = model.PatientId })
                });
            }

            TempData[addResult.Ok ? "VitalSaved" : "VitalSaveError"] = resultMessage;
            return RedirectToAction("ErForm", new { patientId = model.PatientId });
        }

        [RequireERRole("MO", "Nursing")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteVital(long vitalId, string patientId)
        {
            if (!CurrentUserRole.MO && !CurrentUserRole.Nursing)
            {
                TempData["VitalSaveError"] = "You do not have permission to delete vitals.";
                return RedirectToAction("Patients", "Home");
            }

            if (string.IsNullOrWhiteSpace(patientId))
                return RedirectToAction("Patients", "Home");

            if (IsPatientAdmissionDischarged(patientId, null, CurrentUserRole.CompanyCode))
            {
                TempData["VitalSaveError"] = "ER form is discharged and now read-only.";
                return RedirectToAction("ErForm", new { patientId });
            }

            var deleteResult = _erFormService.DeleteVital(
                vitalId,
                CurrentUserRole.CompanyCode,
                CurrentUserRole.UserCode);

            TempData[deleteResult.Ok ? "VitalSaved" : "VitalSaveError"] = deleteResult.Message;

            return RedirectToAction("ErForm", new { patientId });
        }

        [RequireERRole("MO", "Nursing")]
        [HttpGet]
        public ActionResult GetPackageDetails(int packageCode)
        {
            if (!CurrentUserRole.MO && !CurrentUserRole.Nursing)
            {
                return Json(new { ok = false, message = "Access denied." }, JsonRequestBehavior.AllowGet);
            }

            if (packageCode <= 0)
                return Json(new { ok = false, message = "Package is required." }, JsonRequestBehavior.AllowGet);

            var details = _erFormService.GetPackageDetails(packageCode);
            if (!details.Ok)
                return Json(new { ok = false, message = details.Message }, JsonRequestBehavior.AllowGet);

            return Json(new { ok = true, items = details.Data }, JsonRequestBehavior.AllowGet);
        }

        [RequireERRole("MO", "Nursing")]
        [HttpPost]
        public ActionResult SaveReviewForm()
        {
            if (!CurrentUserRole.MO && !CurrentUserRole.Nursing)
                return JsonSaveError("Only MO or Nursing can save the form.");

            SaveErReviewFormInputViewModel model;
            try
            {
                Request.InputStream.Position = 0;
                using (var reader = new StreamReader(Request.InputStream))
                    model = JsonConvert.DeserializeObject<SaveErReviewFormInputViewModel>(reader.ReadToEnd());
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(EmergencyFormController), nameof(SaveReviewForm), ex);
                return JsonSaveError("Invalid save request.");
            }

            if (model == null || string.IsNullOrWhiteSpace(model.PatientId))
                return JsonSaveError("Patient record is missing.");

            var reviewContext = GetDischargeContext(model.PatientId, null, CurrentUserRole.CompanyCode);
            if (reviewContext == null)
                return JsonSaveError("Patient context not found.");
            if (reviewContext.IsAdmissionDischarged)
                return JsonSaveError("ER form is discharged and now read-only.");

            if (model.DischargeFinalize)
            {
                if (!CurrentUserRole.MO)
                    return JsonSaveError("Only MO can finalize discharge.");

                var finalizeSelectionError = ValidateDischargeFinalizeSelections(model);
                if (!string.IsNullOrWhiteSpace(finalizeSelectionError))
                    return JsonSaveError(finalizeSelectionError);

                using (var db = dbAMCEntities.Create())
                {
                    var preContext = ResolveDischargeContext(db, model.PatientId, null, CurrentUserRole.CompanyCode);
                    if (preContext == null)
                        return JsonSaveError("Patient context not found.");
                    if (preContext.IsAdmissionDischarged)
                        return JsonSaveError("Patient is already discharged.");

                    var finalizeGate = EvaluateFinalizeDischargeRequirements(db, preContext, CurrentUserRole.CompanyCode);
                    if (!finalizeGate.CanDischarge)
                        return JsonSaveError(finalizeGate.Message);
                }
            }

            var saveResult = _erFormService.SaveReviewForm(
                model,
                CurrentUserRole.CompanyCode,
                CurrentUserRole.UserCode,
                CurrentUserRole.MO,
                CurrentUserRole.Nursing);

            if (!saveResult.Ok)
                return JsonSaveError(saveResult.Message ?? "Could not save form.");

            if (model.DischargeFinalize)
            {
                using (var db = dbAMCEntities.Create())
                {
                    var context = ResolveDischargeContext(db, model.PatientId, null, CurrentUserRole.CompanyCode);
                    if (context == null)
                        return JsonSaveError("Patient context not found.");
                    if (context.IsAdmissionDischarged)
                        return JsonSaveError("Patient is already discharged.");

                    // Re-check after save in case status changed concurrently.
                    var pending = EvaluateFinalizeDischargeRequirements(db, context, CurrentUserRole.CompanyCode);
                    if (!pending.CanDischarge)
                        return JsonSaveError(pending.Message);

                    if (!UpdateErAdmissionDischargedForPortal(context.AdmissionCode, CurrentUserRole.UserCode, CurrentUserRole.CompanyCode))
                        return JsonSaveError("Could not update discharge status.");

                    var now = DateTime.Now;
                    var pdfBytes = BuildErFormPdfBytes(db, context, now);
                    if (pdfBytes == null || pdfBytes.Length == 0)
                        return JsonSaveError("Discharge finalized but could not generate ER Form PDF.");

                    var saveDocOk = SaveClinicalDocumentEr(
                        db,
                        context,
                        pdfBytes,
                        CurrentUserRole.CompanyCode,
                        CurrentUserRole.UserCode,
                        now);
                    if (!saveDocOk)
                        return JsonSaveError("Discharge finalized but ER document could not be archived in tblClinicalDocumentER.");
                }
            }

            var state = saveResult.Data;
            return Json(new
            {
                ok = true,
                message = model.DischargeFinalize
                    ? "Form saved successfully. ER Form PDF archived."
                    : "Form saved successfully.",
                savedOn = state.SavedOn?.ToString("dd-MMM-yyyy HH:mm"),
                investigations = state.Investigations.Select(x => new
                {
                    id = x.Id,
                    testName = x.TestName,
                    remarks = x.Remarks,
                    isCancelled = x.IsCancelled,
                    isAcknowledged = x.IsAcknowledged,
                    ackByName = x.AckByName,
                    ackDate = x.AckDate?.ToString("dd-MMM-yyyy")
                })
            });
        }

        private static string ValidateDischargeFinalizeSelections(SaveErReviewFormInputViewModel model)
        {
            if (model == null || !model.DischargeFinalize)
                return string.Empty;

            if (!model.GcsCode.HasValue || model.GcsCode.Value <= 0) return "GCS is required before Discharged Finalize.";
            if (!model.PlanterCode.HasValue || model.PlanterCode.Value <= 0) return "Planter response is required before Discharged Finalize.";
            if (!model.CvsCode.HasValue || model.CvsCode.Value <= 0) return "CVS selection is required before Discharged Finalize.";
            if (!model.RespiratoryCode.HasValue || model.RespiratoryCode.Value <= 0) return "Respiratory selection is required before Discharged Finalize.";
            if (!model.BowelSoundCode.HasValue || model.BowelSoundCode.Value <= 0) return "Bowel sound selection is required before Discharged Finalize.";
            if (!model.AbdomenCode.HasValue || model.AbdomenCode.Value <= 0) return "Abdomen selection is required before Discharged Finalize.";
            if (!model.AdmissionCategoryCode.HasValue || model.AdmissionCategoryCode.Value <= 0) return "Admission category is required before Discharged Finalize.";
            if (!model.ReceivedFromCode.HasValue || model.ReceivedFromCode.Value <= 0) return "Received from is required before Discharged Finalize.";
            if (!model.OutcomeCode.HasValue || model.OutcomeCode.Value <= 0) return "Outcome selection is required before Discharged Finalize.";
            if (!model.ConditionUponReleaseCode.HasValue || model.ConditionUponReleaseCode.Value <= 0) return "Condition upon release is required before Discharged Finalize.";
            if (!model.AdrCode.HasValue || model.AdrCode.Value <= 0) return "ADR selection is required before Discharged Finalize.";
            if (model.ChiefComplaints == null || model.ChiefComplaints.Count == 0) return "At least one chief complaint tick is required before Discharged Finalize.";
            if (model.PastHistoryCodes == null || model.PastHistoryCodes.Count == 0) return "At least one past history tick is required before Discharged Finalize.";

            return string.Empty;
        }

        [RequireERRole("MO", "Nursing")]
        [HttpPost]
        public ActionResult SavePackageOrders()
        {
            if (!CurrentUserRole.MO && !CurrentUserRole.Nursing)
                return JsonSaveError("Only MO or Nursing can save package orders.");

            SavePackageOrdersInputViewModel model;
            try
            {
                Request.InputStream.Position = 0;
                using (var reader = new StreamReader(Request.InputStream))
                    model = JsonConvert.DeserializeObject<SavePackageOrdersInputViewModel>(reader.ReadToEnd());
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(EmergencyFormController), nameof(SavePackageOrders), ex);
                return JsonSaveError("Invalid save request.");
            }

            if (model == null || string.IsNullOrWhiteSpace(model.PatientId))
                return JsonSaveError("Patient record is missing.");

            if (IsPatientAdmissionDischarged(model.PatientId, null, CurrentUserRole.CompanyCode))
                return JsonSaveError("ER form is discharged and now read-only.");

            if (CurrentUserRole.Nursing && !CurrentUserRole.MO && model.PackageTypeCode != 2)
                return JsonSaveError("Nursing can save surgical package only.");

            if (CurrentUserRole.MO && !CurrentUserRole.Nursing && model.PackageTypeCode == 2)
                return JsonSaveError("Surgical package can be saved by Nursing only.");

            var saveResult = _erFormService.SavePackageOrders(
                model,
                CurrentUserRole.CompanyCode,
                CurrentUserRole.UserCode);

            if (!saveResult.Ok)
                return JsonSaveError(saveResult.Message ?? "Could not save package orders.");

            var state = saveResult.Data;
            var rows = model.PackageTypeCode == 1 ? state.MedicineOrders : state.SurgicalOrders;

            return Json(new
            {
                ok = true,
                message = model.PackageTypeCode == 1
                    ? "Medicine package saved."
                    : "Surgical package saved.",
                items = rows.Select(x => new
                {
                    id = x.Id,
                    itemName = x.ItemName,
                    dose = x.Dose,
                    drugRouteCode = x.DrugRouteCode,
                    discontinue = x.Discontinue,
                    remarks = x.Remarks,
                    isAcknowledged = x.IsAcknowledged,
                    ackByName = x.AckByName,
                    ackDate = x.AckDate?.ToString("dd-MMM-yyyy")
                })
            });
        }

        [RequireERRole("MO", "Nursing")]
        [HttpGet]
        public ActionResult GetOutcomeAutoPopulate(string patientId, long? admissionCode)
        {
            if (string.IsNullOrWhiteSpace(patientId))
                return Json(new { ok = false, message = "Patient record is missing." }, JsonRequestBehavior.AllowGet);

            var data = _erFormService.GetOutcomeAutoPopulateData(
                patientId,
                admissionCode,
                CurrentUserRole.CompanyCode);

            return Json(new { ok = true, data }, JsonRequestBehavior.AllowGet);
        }

        [RequireERRole("MO")]
        [HttpPost]
        public ActionResult SaveOutcomeForm()
        {
            if (!CurrentUserRole.MO)
                return JsonSaveError("Only MO can save outcome forms.");

            SaveOutcomeFormInputViewModel model;
            try
            {
                Request.InputStream.Position = 0;
                using (var reader = new StreamReader(Request.InputStream))
                    model = JsonConvert.DeserializeObject<SaveOutcomeFormInputViewModel>(reader.ReadToEnd());
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(EmergencyFormController), nameof(SaveOutcomeForm), ex);
                return JsonSaveError("Invalid save request.");
            }

            if (model == null || string.IsNullOrWhiteSpace(model.PatientId))
                return JsonSaveError("Patient record is missing.");

            if (IsPatientAdmissionDischarged(model.PatientId, model.AdmissionCode, CurrentUserRole.CompanyCode))
                return JsonSaveError("ER form is discharged and now read-only.");

            var formLabel = "Outcome form";
            var normalizedType = (model.FormType ?? string.Empty).Trim().ToLowerInvariant();
            if (normalizedType == "lama")
                formLabel = "LAMA";
            else if (normalizedType == "referral")
                formLabel = "Referral";
            else if (normalizedType == "discharge")
                formLabel = "Discharge";
            else if (normalizedType == "death")
                formLabel = "Death Certificate";

            var saveResult = _erFormService.SaveOutcomeForm(
                model,
                CurrentUserRole.CompanyCode,
                CurrentUserRole.UserCode);

            if (!saveResult.Ok)
            {
                var rawMessage = saveResult.Message ?? string.Empty;
                if (rawMessage.StartsWith("CONFIRM_REPLACE_OUTCOME|", StringComparison.OrdinalIgnoreCase)
                    || rawMessage.StartsWith("CONFIRM_CLOSE_DISCHARGE|", StringComparison.OrdinalIgnoreCase))
                {
                    var prefix = rawMessage.StartsWith("CONFIRM_REPLACE_OUTCOME|", StringComparison.OrdinalIgnoreCase)
                        ? "CONFIRM_REPLACE_OUTCOME|"
                        : "CONFIRM_CLOSE_DISCHARGE|";
                    var confirmMessage = rawMessage.Substring(prefix.Length);
                    return Json(new
                    {
                        ok = false,
                        requiresConfirmation = true,
                        confirmationType = "replace_outcome",
                        message = string.IsNullOrWhiteSpace(confirmMessage)
                            ? "Another outcome form already exists. Do you want to close it and continue?"
                            : confirmMessage
                    });
                }

                var errorMessage = string.IsNullOrWhiteSpace(saveResult.Message)
                    ? ("Error occurred in " + formLabel + ".")
                    : ("Error occurred in " + formLabel + ": " + saveResult.Message);
                return JsonSaveError(errorMessage);
            }

            return Json(new
            {
                ok = true,
                message = formLabel + " save successfully."
            });
        }

        [RequireERRole("MO", "Nursing")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UploadDocument(string patientId, string documentType, HttpPostedFileBase file)
        {
            if (!CurrentUserRole.MO && !CurrentUserRole.Nursing)
                return JsonSaveError("Only MO or Nursing can upload documents.");

            if (string.IsNullOrWhiteSpace(patientId) || file == null || file.ContentLength <= 0)
                return JsonSaveError("Please choose a file to upload.");

            if (IsPatientAdmissionDischarged(patientId, null, CurrentUserRole.CompanyCode))
                return JsonSaveError("ER form is discharged and now read-only.");

            if (string.IsNullOrWhiteSpace(documentType))
                return JsonSaveError("Please select a document type.");

            if (file.ContentLength > 10 * 1024 * 1024)
                return JsonSaveError("File exceeds 10 MB limit.");

            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                file.InputStream.CopyTo(ms);
                bytes = ms.ToArray();
            }

            var uploadResult = _erFormService.UploadDocument(
                patientId,
                documentType,
                Path.GetFileName(file.FileName),
                file.ContentType,
                bytes,
                CurrentUserRole.CompanyCode,
                CurrentUserRole.UserCode);

            if (!uploadResult.Ok)
                return JsonSaveError(uploadResult.Message ?? "Could not upload document.");

            var state = uploadResult.Data;
            var doc = state.Documents.FirstOrDefault();
            return Json(new
            {
                ok = true,
                message = "Document uploaded.",
                document = doc == null ? null : new
                {
                    id = doc.Id,
                    fileName = doc.FileName,
                    documentType = doc.DocumentType,
                    uploadedByName = doc.UploadedByName,
                    uploadedOn = doc.UploadedOn.ToString("dd-MMM-yyyy HH:mm")
                }
            });
        }

        [HttpGet]
        public ActionResult DownloadDocument(long documentId, string patientId)
        {
            var bytes = _erFormService.GetDocumentBytes(
                documentId,
                patientId,
                CurrentUserRole.CompanyCode);

            if (bytes == null || bytes.Length == 0)
                return HttpNotFound();

            using (var db = dbAMCEntities.Create())
            {
                var doc = db.tblERPatientDocuments.FirstOrDefault(x =>
                    x.intERPatientDocumentCode == documentId
                    && x.intCompanyCode == CurrentUserRole.CompanyCode
                    && x.intRecordStatusCode == 1);

                if (doc == null) return HttpNotFound();
                return File(bytes, doc.strContentType ?? "application/octet-stream", doc.strFileName);
            }
        }

        [RequireERRole("MO", "Nursing")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteDocument(long documentId, string patientId)
        {
            if (!CurrentUserRole.MO && !CurrentUserRole.Nursing)
                return Json(new { ok = false, message = "Only MO or Nursing can remove documents." });
            if (IsPatientAdmissionDischarged(patientId, null, CurrentUserRole.CompanyCode))
                return Json(new { ok = false, message = "ER form is discharged and now read-only." });
            var deleteResult = _erFormService.DeleteDocument(
                documentId,
                patientId,
                CurrentUserRole.CompanyCode,
                CurrentUserRole.UserCode);

            return Json(new
            {
                ok = deleteResult.Ok,
                message = deleteResult.Message
            });
        }

        [RequireERRole("MO")]
        [HttpGet]
        public ActionResult GetDischargeEligibility(string patientId, long? admissionCode)
        {
            var result = EvaluateDischargeEligibility(patientId, admissionCode);
            return Json(new
            {
                ok = true,
                canDischarge = result.CanDischarge,
                message = result.Message ?? string.Empty
            }, JsonRequestBehavior.AllowGet);
        }

        [RequireERRole("MO")]
        [HttpPost]
        public ActionResult DischargePatient(string patientId, long? admissionCode)
        {
            if (!CurrentUserRole.MO)
                return Json(new { ok = false, message = "Only MO can discharge patient." });

            try
            {
                using (var db = dbAMCEntities.Create())
                using (var tx = db.Database.BeginTransaction())
                {
                    var context = ResolveDischargeContext(db, patientId, admissionCode, CurrentUserRole.CompanyCode);
                    if (context == null)
                        return Json(new { ok = false, message = "Could not resolve patient/admission context." });

                    var eligibility = EvaluateDischargeEligibility(db, context, CurrentUserRole.CompanyCode);
                    if (!eligibility.CanDischarge)
                        return Json(new { ok = false, message = eligibility.Message ?? "Patient cannot be discharged yet." });

                    var now = DateTime.Now;
                    context.ErPatient.bolIsDischarge = true;
                    context.ErPatient.dtmDischarge = now;
                    context.ErPatient.dtmLastM = now;
                    context.ErPatient.intAlteredByCode = CurrentUserRole.UserCode;

                    db.SaveChanges();

                    //var bedStatusUpdated = UpdateWardBedStatusForDischarge(
                    //    context.ErPatient.intWardBedCode,
                    //    CurrentUserRole.UserCode,
                    //    CurrentUserRole.CompanyCode);
                    //if (!bedStatusUpdated)
                    //    return Json(new { ok = false, message = "Patient discharge blocked: could not update bed status." });

                    var pdfBytes = BuildErFormPdfBytes(db, context, now);
                    if (pdfBytes == null || pdfBytes.Length == 0)
                        return Json(new { ok = false, message = "Could not generate ER Form PDF." });

                    var saveDocOk = SaveClinicalDocumentEr(
                        db,
                        context,
                        pdfBytes,
                        CurrentUserRole.CompanyCode,
                        CurrentUserRole.UserCode,
                        now);
                    if (!saveDocOk)
                        return Json(new { ok = false, message = "Patient discharged but ER document could not be archived in tblClinicalDocumentER." });

                    tx.Commit();
                    return Json(new
                    {
                        ok = true,
                        message = "Patient discharged successfully."
                    });
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(EmergencyFormController), nameof(DischargePatient), ex);
                return Json(new { ok = false, message = "Could not discharge patient: " + ex.GetBaseException().Message });
            }
        }

        [RequireERRole("MO")]
        [HttpGet]
        public async Task<ActionResult> PrintDischargeSummary(long? admissionCode)
        {
            if (!admissionCode.HasValue || admissionCode.Value <= 0)
                return HttpNotFound();

            var bytes = await ReportManager.GetDischargeSummaryBytes(admissionCode.Value.ToString());
            if (bytes == null || bytes.Length == 0)
                return HttpNotFound();

            return File(bytes, "application/pdf");
        }

        [RequireERRole("MO")]
        [HttpGet]
        public async Task<ActionResult> PrintDeathCertificate(long? admissionCode)
        {
            if (!admissionCode.HasValue || admissionCode.Value <= 0)
                return HttpNotFound();

            var bytes = await ReportManager.GetDeathCertificateBytes(admissionCode.Value.ToString());
            if (bytes == null || bytes.Length == 0)
                return HttpNotFound();

            return File(bytes, "application/pdf");
        }

        [RequireERRole("MO")]
        [HttpGet]
        public async Task<ActionResult> PrintPatientReferral(long? admissionCode)
        {
            if (!admissionCode.HasValue || admissionCode.Value <= 0)
                return HttpNotFound();

            var bytes = await ReportManager.GetPatientReferralBytes(admissionCode.Value.ToString());
            if (bytes == null || bytes.Length == 0)
                return HttpNotFound();

            return File(bytes, "application/pdf");
        }

        [RequireERRole("MO")]
        [HttpGet]
        public async Task<ActionResult> PrintLama(long? admissionCode)
        {
            if (!admissionCode.HasValue || admissionCode.Value <= 0)
                return HttpNotFound();

            var bytes = await ReportManager.GetLAMABytes(admissionCode.Value.ToString());
            if (bytes == null || bytes.Length == 0)
                return HttpNotFound();

            return File(bytes, "application/pdf");
        }

        [RequireERRole("MO", "Nursing")]
        [HttpGet]
        public async Task<ActionResult> PrintOutcome(string patientId, long? admissionCode)
        {
            using (var db = dbAMCEntities.Create())
            {
                var context = ResolveDischargeContext(db, patientId, admissionCode, CurrentUserRole.CompanyCode);
                if (context == null)
                    return HttpNotFound();

                var hasDischarge = db.tblDischargeSummaries.Any(x =>
                    x.intERAdmissionCode == context.AdmissionCode
                    && x.intCompanyCode == CurrentUserRole.CompanyCode
                    && x.intRecordStatusCode != 8);
                if (hasDischarge)
                {
                    var bytes = await ReportManager.GetDischargeSummaryBytes(context.AdmissionCode.ToString());
                    if (bytes != null && bytes.Length > 0)
                        return File(bytes, "application/pdf");
                }

                var hasDeath = db.tblDeathCertificates.Any(x =>
                    x.intERAdmissionCode == context.AdmissionCode
                    && x.intCompanyCode == CurrentUserRole.CompanyCode
                    && x.intRecordStatusCode != 8);
                if (hasDeath)
                {
                    var bytes = await ReportManager.GetDeathCertificateBytes(context.AdmissionCode.ToString());
                    if (bytes != null && bytes.Length > 0)
                        return File(bytes, "application/pdf");
                }

                var referral = db.tblPatientReferrals
                    .Where(x => x.intERAdmissionCode == context.AdmissionCode
                                && x.intCompanyCode == CurrentUserRole.CompanyCode
                                && x.intRecordStatusCode != 8)
                    .OrderByDescending(x => x.intPatientReferralCode)
                    .FirstOrDefault();
                if (referral != null)
                {
                    var bytes = await ReportManager.GetPatientReferralBytes(context.AdmissionCode.ToString());
                    if (bytes != null && bytes.Length > 0)
                        return File(bytes, "application/pdf");
                }

                var lama = db.tblLAMAs
                    .Where(x => x.intERAdmissionCode == context.AdmissionCode
                                && x.intCompanyCode == CurrentUserRole.CompanyCode
                                && x.intRecordStatusCode != 8)
                    .OrderByDescending(x => x.intLAMACode)
                    .FirstOrDefault();
                if (lama != null)
                {
                    var bytes = await ReportManager.GetLAMABytes(context.AdmissionCode.ToString());
                    if (bytes != null && bytes.Length > 0)
                        return File(bytes, "application/pdf");
                }

                return HttpNotFound("No active outcome form found to print.");
            }
        }

        private JsonResult JsonSaveError(string message)
        {
            return Json(new { ok = false, message });
        }

        private bool WantsJson()
        {
            return string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest",
                StringComparison.OrdinalIgnoreCase);
        }

        private DischargeEligibilityResult EvaluateDischargeEligibility(string patientId, long? admissionCode)
        {
            using (var db = dbAMCEntities.Create())
            {
                var context = ResolveDischargeContext(db, patientId, admissionCode, CurrentUserRole.CompanyCode);
                if (context == null)
                    return DischargeEligibilityResult.Fail("Patient context not found.");
                return EvaluateDischargeEligibility(db, context, CurrentUserRole.CompanyCode);
            }
        }

        private static DischargeEligibilityResult EvaluateDischargeEligibility(
            dbAMCEntities db,
            DischargeContext context,
            int companyCode)
        {
            if (context == null) return DischargeEligibilityResult.Fail("Patient context not found.");
            if (context.ErPatient.bolIsDischarge == true) return DischargeEligibilityResult.Fail("Patient is already discharged.");

            return EvaluateFinalizeDischargeRequirements(db, context, companyCode);
        }

        private static DischargeEligibilityResult EvaluateFinalizeDischargeRequirements(
            dbAMCEntities db,
            DischargeContext context,
            int companyCode)
        {
            if (context == null) return DischargeEligibilityResult.Fail("Patient context not found.");

            var issues = new List<string>();

            var hasPendingInvestigations = db.tblERPatientInvestigations.Any(x =>
                x.intERPatientCode == context.ErPatientCode
                && x.intCompanyCode == companyCode
                && x.intRecordStatusCode != 8
                && !x.bolIsAcknowledged
                && !x.bolIsCancelled);
            if (hasPendingInvestigations) issues.Add("Investigations are pending.");

            var hasPendingMedicine = db.tblERPatientPackageOrders.Any(x =>
                x.intERPatientCode == context.ErPatientCode
                && x.intCompanyCode == companyCode
                && x.intRecordStatusCode != 8
                && x.intPackageTypeCode == 1
                && !x.bolIsAcknowledged
                && !x.bolDiscontinue);
            if (hasPendingMedicine) issues.Add("Medicine items are pending.");

            var hasPendingSurgical = db.tblERPatientPackageOrders.Any(x =>
                x.intERPatientCode == context.ErPatientCode
                && x.intCompanyCode == companyCode
                && x.intRecordStatusCode != 8
                && x.intPackageTypeCode == 2
                && !x.bolIsAcknowledged
                && !x.bolDiscontinue);
            if (hasPendingSurgical) issues.Add("Surgical items are pending.");

            var admissionCode = context.AdmissionCode;
            var hasAnyOutcome =
                db.tblDischargeSummaries.Any(x =>
                    x.intERAdmissionCode == admissionCode
                    && x.intCompanyCode == companyCode
                    && x.intRecordStatusCode != 8)
                || db.tblPatientReferrals.Any(x =>
                    x.intERAdmissionCode == admissionCode
                    && x.intCompanyCode == companyCode
                    && x.intRecordStatusCode != 8)
                || db.tblDeathCertificates.Any(x =>
                    x.intERAdmissionCode == admissionCode
                    && x.intCompanyCode == companyCode
                    && x.intRecordStatusCode != 8)
                || db.tblLAMAs.Any(x =>
                    x.intERAdmissionCode == admissionCode
                    && x.intCompanyCode == companyCode
                    && x.intRecordStatusCode != 8);

            // If no outcome form exists, bypass this check.
            // If one exists, at least one must be saved and finalized.
            if (hasAnyOutcome)
            {
                var hasFinalizedOutcome =
                    db.tblDischargeSummaries.Any(x =>
                        x.intERAdmissionCode == admissionCode
                        && x.intCompanyCode == companyCode
                        && x.intRecordStatusCode != 8
                        && x.bolIsFinal)
                    || db.tblPatientReferrals.Any(x =>
                        x.intERAdmissionCode == admissionCode
                        && x.intCompanyCode == companyCode
                        && x.intRecordStatusCode != 8
                        && x.bolIsFinal)
                    || db.tblDeathCertificates.Any(x =>
                        x.intERAdmissionCode == admissionCode
                        && x.intCompanyCode == companyCode
                        && x.intRecordStatusCode != 8
                        && x.bolIsFinal)
                    || db.tblLAMAs.Any(x =>
                        x.intERAdmissionCode == admissionCode
                        && x.intCompanyCode == companyCode
                        && x.intRecordStatusCode != 8
                        && x.bolIsFinal);

                if (!hasFinalizedOutcome)
                {
                    issues.Add("At least one outcome form (Discharge Summary, Patient Referral, Death Certificate, or LAMA) must be saved and finalized.");
                }
            }

            if (issues.Count > 0)
                return DischargeEligibilityResult.Fail(string.Join(" ", issues));

            return DischargeEligibilityResult.Success();
        }

        private static DischargeEligibilityResult EvaluatePendingItemsForFinalize(
            dbAMCEntities db,
            DischargeContext context,
            int companyCode)
        {
            return EvaluateFinalizeDischargeRequirements(db, context, companyCode);
        }

        private static DischargeContext ResolveDischargeContext(
            dbAMCEntities db,
            string patientId,
            long? admissionCode,
            int companyCode)
        {
            long erPatientCode;
            if (!long.TryParse((patientId ?? string.Empty).Trim(), out erPatientCode) || erPatientCode <= 0)
                return null;

            var erPatient = db.tblERPatients.FirstOrDefault(x =>
                x.intERPatientCode == erPatientCode
                && x.intCompanyCode == companyCode
                && x.intRecordStatusCode != 8);
            if (erPatient == null)
                return null;

            var resolvedAdmissionCode = admissionCode.HasValue && admissionCode.Value > 0
                ? admissionCode.Value
                : (erPatient.intERAdmissionCode ?? 0L);
            if (resolvedAdmissionCode <= 0)
                return null;

            using (var conn = DBHelper.GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand(@"
SELECT TOP 1 intPatientCode, intBranchCode, ISNULL(bolIsDischarged, 0) AS bolIsDischarged
FROM tblERAdmission
WHERE intERAdmissionCode = @admissionCode
  AND intCompanyCode = @companyCode", conn))
                {
                    cmd.Parameters.Add("@admissionCode", SqlDbType.BigInt).Value = resolvedAdmissionCode;
                    cmd.Parameters.Add("@companyCode", SqlDbType.Int).Value = companyCode;
                    using (var rdr = cmd.ExecuteReader())
                    {
                        if (!rdr.Read()) return null;
                        var patientCode = rdr["intPatientCode"] == DBNull.Value ? 0L : Convert.ToInt64(rdr["intPatientCode"]);
                        var branchCode = rdr["intBranchCode"] == DBNull.Value ? 0 : Convert.ToInt32(rdr["intBranchCode"]);
                        var isDischarged = rdr["bolIsDischarged"] != DBNull.Value && Convert.ToBoolean(rdr["bolIsDischarged"]);
                        if (patientCode <= 0 || branchCode <= 0) return null;

                        return new DischargeContext
                        {
                            ErPatient = erPatient,
                            ErPatientCode = erPatient.intERPatientCode,
                            AdmissionCode = resolvedAdmissionCode,
                            PatientCode = patientCode,
                            BranchCode = branchCode,
                            IsAdmissionDischarged = isDischarged
                        };
                    }
                }
            }
        }

        private static byte[] BuildSimpleOutcomePdf(
            string title,
            long admissionCode,
            long patientCode,
            IDictionary<string, string> fields)
        {
            using (var ms = new MemoryStream())
            {
                var document = new Document(PageSize.A4, 36f, 36f, 36f, 36f);
                PdfWriter.GetInstance(document, ms);
                document.Open();

                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14f);
                var labelFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10f);
                var valueFont = FontFactory.GetFont(FontFactory.HELVETICA, 10f);

                document.Add(new Paragraph(title ?? "Outcome Form", titleFont));
                document.Add(new Paragraph("ER Admission Code: " + admissionCode, valueFont));
                document.Add(new Paragraph("Patient Code: " + patientCode, valueFont));
                document.Add(new Paragraph("Generated On: " + DateTime.Now.ToString("dd-MMM-yyyy HH:mm"), valueFont));
                document.Add(new Paragraph(" ", valueFont));

                foreach (var kv in fields ?? new Dictionary<string, string>())
                {
                    AddPdfField(document, kv.Key ?? string.Empty, kv.Value, labelFont, valueFont);
                }

                document.Close();
                return ms.ToArray();
            }
        }

        private static byte[] BuildErFormPdfBytes(dbAMCEntities db, DischargeContext context, DateTime now)
        {
            var review = db.tblERPatientReviewForms
                .Where(x => x.intERPatientCode == context.ErPatientCode
                            && x.intCompanyCode == context.ErPatient.intCompanyCode
                            && x.intRecordStatusCode != 8)
                .OrderByDescending(x => x.dtmSaved)
                .FirstOrDefault();

            using (var ms = new MemoryStream())
            {
                var document = new Document(PageSize.A4, 36f, 36f, 36f, 36f);
                PdfWriter.GetInstance(document, ms);
                document.Open();

                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14f);
                var labelFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10f);
                var valueFont = FontFactory.GetFont(FontFactory.HELVETICA, 10f);

                document.Add(new Paragraph("ER Form Discharge Document", titleFont));
                document.Add(new Paragraph("Generated On: " + now.ToString("dd-MMM-yyyy HH:mm"), valueFont));
                document.Add(new Paragraph("ER Admission Code: " + context.AdmissionCode, valueFont));
                document.Add(new Paragraph("Patient Code: " + context.PatientCode, valueFont));
                document.Add(new Paragraph("ER Patient Code: " + context.ErPatientCode, valueFont));
                document.Add(new Paragraph(" ", valueFont));

                if (review != null)
                {
                    AddPdfField(document, "Saved On", review.dtmSaved.ToString("dd-MMM-yyyy HH:mm"), labelFont, valueFont);
                    AddPdfField(document, "Assessment / Diagnosis", review.strAssessmentDiagnosis, labelFont, valueFont);
                    AddPdfField(document, "Treatment Notes", review.strTreatmentNotes, labelFont, valueFont);
                    AddPdfField(document, "Consultant Plan", review.strConsultantPlan, labelFont, valueFont);
                    AddPdfField(document, "Nursing Observations", review.strNursingObservations, labelFont, valueFont);
                    AddPdfField(document, "Nursing Care Plan", review.strNursingCarePlan, labelFont, valueFont);
                }
                else
                {
                    document.Add(new Paragraph("No ER review form data found.", valueFont));
                }

                document.Close();
                return ms.ToArray();
            }
        }

        private static void AddPdfField(Document document, string label, string value, Font labelFont, Font valueFont)
        {
            document.Add(new Paragraph(label + ":", labelFont));
            document.Add(new Paragraph(string.IsNullOrWhiteSpace(value) ? "-" : value.Trim(), valueFont));
            document.Add(new Paragraph(" ", valueFont));
        }

        private static bool SaveClinicalDocumentEr(
            dbAMCEntities db,
            DischargeContext context,
            byte[] pdfBytes,
            int companyCode,
            int userCode,
            DateTime now)
        {
            if (db == null || context == null || pdfBytes == null || pdfBytes.Length == 0)
                return false;

            var entity = db.tblClinicalDocumentERs
                .Where(x => x.intCompanyCode == companyCode
                            && x.intERAdmissionCode == context.AdmissionCode
                            && x.intClinicalDocumentTemplateCode == 9
                            && x.intRecordStatusCode != 8)
                .OrderByDescending(x => x.intClinicalDocumentERCode)
                .FirstOrDefault();

            if (entity != null)
            {
                entity.dtmClinicalDocumentER = now;
                entity.intPatientCode = context.PatientCode;
                entity.strDocumentName = "ERForm-" + context.AdmissionCode;
                entity.strDocumentExtension = ".pdf";
                entity.vbrDocument = pdfBytes;
                entity.bolIsFinal = true;
                entity.dtmLastM = now;
                entity.intAlteredByCode = userCode;
                entity.intRecordStatusCode = 1;
                entity.intBranchCode = context.BranchCode;
            }
            else
            {
                var nextCode = db.tblClinicalDocumentERs
                    .Where(x => x.intCompanyCode == companyCode)
                    .Select(x => (long?)x.intClinicalDocumentERCode)
                    .Max();

                entity = new tblClinicalDocumentER
                {
                    intClinicalDocumentERCode = (nextCode ?? 0L) + 1L,
                    dtmClinicalDocumentER = now,
                    intClinicalDocumentERID = Guid.NewGuid(),
                    intClinicalDocumentTemplateCode = 9,
                    intFileTypeCode = null,
                    intERAdmissionCode = context.AdmissionCode,
                    intPatientCode = context.PatientCode,
                    strDocumentName = "ERForm-" + context.AdmissionCode,
                    strDocumentExtension = ".pdf",
                    vbrDocument = pdfBytes,
                    bolIsFinal = true,
                    dtmCreated = now,
                    dtmLastM = now,
                    intOwnerCode = userCode,
                    intCreatedByCode = userCode,
                    intAlteredByCode = userCode,
                    intRecordStatusCode = 1,
                    intBranchCode = context.BranchCode,
                    intCompanyCode = companyCode
                };

                db.tblClinicalDocumentERs.Add(entity);
            }

            return db.SaveChanges() > 0;
        }

        private static bool UpdateWardBedStatusForDischarge(int wardBedCode, int userCode, int companyCode)
        {
            if (wardBedCode <= 0 || userCode <= 0 || companyCode <= 0)
                return false;

            try
            {
                using (var conn = DBHelper.GetConnection())
                using (var cmd = new SqlCommand("procUpdateWardBedStatusForERPortal", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@intWardBedStatusCode", SqlDbType.Int).Value = 1;
                    cmd.Parameters.Add("@intWardBedCode", SqlDbType.Int).Value = wardBedCode;
                    cmd.Parameters.Add("@intUserCode", SqlDbType.Int).Value = userCode;
                    cmd.Parameters.Add("@intCompanyCode", SqlDbType.Int).Value = companyCode;
                    conn.Open();
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(EmergencyFormController), nameof(UpdateWardBedStatusForDischarge), ex);
                return false;
            }
        }

        private static bool UpdateErAdmissionDischargedForPortal(long admissionCode, int userCode, int companyCode)
        {
            if (admissionCode <= 0 || userCode <= 0 || companyCode <= 0)
                return false;

            try
            {
                using (var conn = DBHelper.GetConnection())
                using (var cmd = new SqlCommand("procUpdateERPatientDischargedForERPortal", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@bolIsDischarge", SqlDbType.Bit).Value = true;
                    cmd.Parameters.Add("@intERAdmissionCode", SqlDbType.BigInt).Value = admissionCode;
                    cmd.Parameters.Add("@intUserCode", SqlDbType.Int).Value = userCode;
                    cmd.Parameters.Add("@intCompanyCode", SqlDbType.Int).Value = companyCode;
                    conn.Open();
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(EmergencyFormController), nameof(UpdateErAdmissionDischargedForPortal), ex);
                return false;
            }
        }

        private bool IsPatientAdmissionDischarged(string patientId, long? admissionCode, int companyCode)
        {
            using (var db = dbAMCEntities.Create())
            {
                var context = ResolveDischargeContext(db, patientId, admissionCode, companyCode);
                return context != null && context.IsAdmissionDischarged;
            }
        }

        private static bool IsErAdmissionDischarged(long? admissionCode, int companyCode)
        {
            if (!admissionCode.HasValue || admissionCode.Value <= 0) return false;
            try
            {
                using (var conn = DBHelper.GetConnection())
                using (var cmd = new SqlCommand(@"
SELECT TOP 1 ISNULL(bolIsDischarged, 0)
FROM tblERAdmission
WHERE intERAdmissionCode = @admissionCode
  AND intCompanyCode = @companyCode", conn))
                {
                    cmd.Parameters.Add("@admissionCode", SqlDbType.BigInt).Value = admissionCode.Value;
                    cmd.Parameters.Add("@companyCode", SqlDbType.Int).Value = companyCode;
                    conn.Open();
                    var result = cmd.ExecuteScalar();
                    return result != null && result != DBNull.Value && Convert.ToBoolean(result);
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(EmergencyFormController), nameof(IsErAdmissionDischarged), ex);
                return false;
            }
        }

        private static bool HasAnyOutcomeForm(long? admissionCode, int companyCode)
        {
            return GetPrintableOutcomeFlags(admissionCode, companyCode).CanPrintAny;
        }

        private static PrintableOutcomeFlags GetPrintableOutcomeFlags(long? admissionCode, int companyCode)
        {
            var flags = new PrintableOutcomeFlags();
            if (!admissionCode.HasValue || admissionCode.Value <= 0 || companyCode <= 0)
                return flags;

            var code = admissionCode.Value;
            using (var db = dbAMCEntities.Create())
            {
                flags.CanPrintDischarge = db.tblDischargeSummaries.Any(x =>
                    x.intERAdmissionCode == code && x.intCompanyCode == companyCode && x.intRecordStatusCode != 8);
                flags.CanPrintDeath = db.tblDeathCertificates.Any(x =>
                    x.intERAdmissionCode == code && x.intCompanyCode == companyCode && x.intRecordStatusCode != 8);
                flags.CanPrintReferral = db.tblPatientReferrals.Any(x =>
                    x.intERAdmissionCode == code && x.intCompanyCode == companyCode && x.intRecordStatusCode != 8);
                flags.CanPrintLama = db.tblLAMAs.Any(x =>
                    x.intERAdmissionCode == code && x.intCompanyCode == companyCode && x.intRecordStatusCode != 8);
            }

            return flags;
        }

        private sealed class PrintableOutcomeFlags
        {
            public bool CanPrintDischarge { get; set; }
            public bool CanPrintDeath { get; set; }
            public bool CanPrintReferral { get; set; }
            public bool CanPrintLama { get; set; }
            public bool CanPrintAny => CanPrintDischarge || CanPrintDeath || CanPrintReferral || CanPrintLama;
        }

        private DischargeContext GetDischargeContext(string patientId, long? admissionCode, int companyCode)
        {
            using (var db = dbAMCEntities.Create())
            {
                return ResolveDischargeContext(db, patientId, admissionCode, companyCode);
            }
        }

        private sealed class DischargeContext
        {
            public long ErPatientCode { get; set; }
            public long AdmissionCode { get; set; }
            public long PatientCode { get; set; }
            public int BranchCode { get; set; }
            public bool IsAdmissionDischarged { get; set; }
            public tblERPatient ErPatient { get; set; }
        }

        private sealed class DischargeEligibilityResult
        {
            public bool CanDischarge { get; private set; }
            public string Message { get; private set; }

            public static DischargeEligibilityResult Success()
            {
                return new DischargeEligibilityResult { CanDischarge = true, Message = string.Empty };
            }

            public static DischargeEligibilityResult Fail(string message)
            {
                return new DischargeEligibilityResult { CanDischarge = false, Message = message ?? string.Empty };
            }
        }
    }
}
