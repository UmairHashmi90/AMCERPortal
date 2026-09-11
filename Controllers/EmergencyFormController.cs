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
using System.Globalization;
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
            var erPatient = ResolveErPatientForSave(patientContext.PatientId, CurrentUserRole.CompanyCode);
            var isFormReadOnly = IsDischargeStarted(erPatient);
            ViewBag.IsMOUser = CurrentUserRole.MO;
            ViewBag.IsNursingUser = CurrentUserRole.Nursing;
            ViewBag.IsMO = CurrentUserRole.MO && !isFormReadOnly;
            ViewBag.IsNursing = CurrentUserRole.Nursing && !isFormReadOnly;
            ViewBag.PatientFormContext = patientContext;
            ViewBag.CanEditVitals = !isFormReadOnly && (CurrentUserRole.MO || CurrentUserRole.Nursing);
            ViewBag.CanEditClinical = !isFormReadOnly && CurrentUserRole.MO;
            ViewBag.CanEditSurgical = !isFormReadOnly && CurrentUserRole.Nursing;
            ViewBag.CanEditNursingNotes = !isFormReadOnly && CurrentUserRole.Nursing;
            ViewBag.CanEditNursingCarePlan = !isFormReadOnly && CurrentUserRole.MO;
            ViewBag.CanUploadDocuments = !isFormReadOnly && (CurrentUserRole.MO || CurrentUserRole.Nursing);
            ViewBag.CanWorkOnPage = !isFormReadOnly && (CurrentUserRole.MO || CurrentUserRole.Nursing);
            ViewBag.IsAdmissionDischarged = isFormReadOnly;
            ViewBag.PageModeLabel = isFormReadOnly
                ? "Discharge Finalized (Read Only)"
                : CurrentUserRole.MO
                ? "MO Work Mode"
                : CurrentUserRole.Nursing
                        ? "Nursing Work Mode"
                    : "View Only";
            var dischargeEligibility = isFormReadOnly
                ? DischargeEligibilityResult.Fail("Discharge is already finalized. Form is read-only.")
                : EvaluateDischargeEligibility(patientContext.PatientId, patientContext.AdmissionCode);
            ViewBag.CanDischargeNow = !isFormReadOnly && dischargeEligibility.CanDischarge;
            ViewBag.DischargeBlockedReason = dischargeEligibility.Message;
            var printableOutcomes = GetPrintableOutcomeFlags(patientContext.AdmissionCode, CurrentUserRole.CompanyCode);
            ViewBag.CanPrintDischargeSummary = printableOutcomes.CanPrintDischarge;
            ViewBag.CanPrintDeathCertificate = printableOutcomes.CanPrintDeath;
            ViewBag.CanPrintPatientReferral = printableOutcomes.CanPrintReferral;
            ViewBag.CanPrintLama = printableOutcomes.CanPrintLama;
            ViewBag.CanPrintAdmissionOrder = printableOutcomes.CanPrintAdmission;
            ViewBag.CanPrintOutcome = printableOutcomes.CanPrintAny;
            ViewBag.CanDownloadErFormPdf = HasStoredErFormPdf(
                erPatient != null ? erPatient.intERAdmissionCode : null,
                CurrentUserRole.CompanyCode);

            var model = _erFormService.BuildErFormModel(
                patientContext.PatientId,
                CurrentUserRole.CompanyCode,
                CurrentUserRole.UserCode);
            if (model == null)
            {
                TempData["AssignError"] = BuildPatientAccessError();
                return RedirectToAction("Patients", "Home");
            }

            ApplyDefaultMoFromLoggedInUser(model);
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
                const string message = "ER form is discharge finalized and now read-only.";
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
                Spo2Remark      = model.Spo2Remark,
                Spo2Code        = model.Spo2Code,
                GlucoseF        = model.GlucoseF,
                GlucoseR        = model.GlucoseR,
                Temperature     = model.Temperature,
                FallRisk        = model.FallRisk,
                PainScore       = model.PainScore,
                ConsciousnessCode = model.ConsciousnessCode
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
                TempData["VitalSaveError"] = "ER form is discharge finalized and now read-only.";
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

            // Pending-MR patients have no ER admission yet — do not require admission for normal save.
            var erPatientForSave = ResolveErPatientForSave(model.PatientId, CurrentUserRole.CompanyCode);
            if (erPatientForSave == null)
                return JsonSaveError("Patient context not found.");
            if (IsDischargeStarted(erPatientForSave))
                return JsonSaveError("ER form is discharge finalized and now read-only.");

            if (model.DischargeFinalize)
            {
                if (!CurrentUserRole.MO)
                    return JsonSaveError("Only MO can finalize discharge.");

                if (!model.DischargeSummaryNotRequired)
                {
                    var issues = new List<string>();
                    var finalizeSelectionError = ValidateDischargeFinalizeSelections(model);
                    if (!string.IsNullOrWhiteSpace(finalizeSelectionError))
                        issues.Add(finalizeSelectionError);

                    using (var db = dbAMCEntities.Create())
                    {
                        var preContext = ResolveDischargeContext(db, model.PatientId, null, CurrentUserRole.CompanyCode);
                        if (preContext == null)
                            return JsonSaveError("Admission/MR is required before Discharged Finalize.");
                        if (IsDischargeStarted(preContext.ErPatient))
                            return JsonSaveError("Discharge is already finalized. Form is read-only.");

                        var finalizeGate = EvaluateFinalizeDischargeRequirements(db, preContext, CurrentUserRole.CompanyCode);
                        if (!finalizeGate.CanDischarge && !string.IsNullOrWhiteSpace(finalizeGate.Message))
                            issues.Add(finalizeGate.Message);
                    }

                    if (issues.Count > 0)
                        return JsonSaveError(string.Join(" ", issues));
                }
                else
                {
                    // Discharge Summary not required: skip form/outcome checks, but still block on pending items.
                    using (var db = dbAMCEntities.Create())
                    {
                        var preContext = ResolveDischargeContext(db, model.PatientId, null, CurrentUserRole.CompanyCode);
                        if (preContext == null)
                            return JsonSaveError("Admission/MR is required before Discharged Finalize.");
                        if (IsDischargeStarted(preContext.ErPatient))
                            return JsonSaveError("Discharge is already finalized. Form is read-only.");

                        var pendingGate = EvaluatePendingItemsForFinalize(db, preContext, CurrentUserRole.CompanyCode);
                        if (!pendingGate.CanDischarge)
                            return JsonSaveError(pendingGate.Message);
                    }
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
                        return JsonSaveError("Admission/MR is required before Discharged Finalize.");
                    if (IsDischargeStarted(context.ErPatient))
                        return JsonSaveError("Discharge is already finalized. Form is read-only.");

                    // Always re-check pending Pharmacy / Investigation / Surgical items.
                    // Full form + outcome checks only when Discharge Summary is required.
                    var pending = model.DischargeSummaryNotRequired
                        ? EvaluatePendingItemsForFinalize(db, context, CurrentUserRole.CompanyCode)
                        : EvaluateFinalizeDischargeRequirements(db, context, CurrentUserRole.CompanyCode);
                    if (!pending.CanDischarge)
                        return JsonSaveError(pending.Message);

                    var now = DateTime.Now;

                    // Discharge Finalize: mark discharge start on ER patient + set bed status 8.
                    if (!MarkErPatientDischargeStart(db, context.ErPatient, CurrentUserRole.UserCode, now))
                        return JsonSaveError("Could not update discharge start on patient.");

                    if (!UpdateWardBedStatusForDischarge(
                        context.ErPatient.intWardBedCode,
                        CurrentUserRole.UserCode,
                        CurrentUserRole.CompanyCode,
                        wardBedStatusCode: 8))
                    {
                        return JsonSaveError("Could not update bed status.");
                    }

                    byte[] pdfBytes = null;
                    byte[] excelBytes = null;
                    byte[] excelPdfBytes = null;
                    string excelDocumentName = null;
                    try
                    {
                        pdfBytes = BuildErFormPdfBytes(db, context, now);
                    }
                    catch (Exception pdfEx)
                    {
                        ErrorLogging.Log(nameof(EmergencyFormController), nameof(BuildErFormPdfBytes), pdfEx);
                    }

                    try
                    {
                        var erFormModel = _erFormService.BuildErFormModel(
                            model.PatientId,
                            CurrentUserRole.CompanyCode,
                            CurrentUserRole.UserCode);
                        if (erFormModel != null)
                        {
                            // In-memory Excel only (never written to disk/folder / never stored in DB).
                            excelBytes = ErFormExcelExporter.Build(erFormModel, now);
                            var safeMr = string.IsNullOrWhiteSpace(erFormModel.MrNo) ? "PENDING" : erFormModel.MrNo.Trim();
                            var safeVisit = string.IsNullOrWhiteSpace(erFormModel.AdmissionNo)
                                ? context.AdmissionCode.ToString(CultureInfo.InvariantCulture)
                                : erFormModel.AdmissionNo.Trim();
                            foreach (var ch in Path.GetInvalidFileNameChars())
                            {
                                safeMr = safeMr.Replace(ch, '_');
                                safeVisit = safeVisit.Replace(ch, '_');
                            }
                            var stamp = now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                            excelDocumentName = "ERForm-" + safeMr + "-" + safeVisit + "-" + stamp;

                            if (excelBytes != null && excelBytes.Length > 0)
                            {
                                // Generate Excel → convert to PDF → compress → store PDF only.
                                excelPdfBytes = ErFormExcelToPdfConverter.ConvertToPdf(excelBytes);
                            }
                        }
                    }
                    catch (Exception excelEx)
                    {
                        ErrorLogging.Log(nameof(EmergencyFormController), nameof(ErFormExcelExporter.Build), excelEx);
                    }

                    var pdfArchived = false;
                    if (pdfBytes != null && pdfBytes.Length > 0)
                    {
                        pdfArchived = SaveClinicalDocument(
                            db,
                            context,
                            pdfBytes,
                            ".pdf",
                            CurrentUserRole.CompanyCode,
                            CurrentUserRole.UserCode,
                            now);
                    }

                    var excelPdfArchived = false;
                    if (excelPdfBytes != null && excelPdfBytes.Length > 0)
                    {
                        // Store compressed PDF bytes only (no Excel in DB, no server folder).
                        excelPdfArchived = SaveClinicalDocument(
                            db,
                            context,
                            excelPdfBytes,
                            ".pdf",
                            CurrentUserRole.CompanyCode,
                            CurrentUserRole.UserCode,
                            now,
                            excelDocumentName);
                    }

                    var stateAfterFinalize = saveResult.Data;
                    var archiveBits = new List<string>();
                    if (excelPdfArchived || pdfArchived) archiveBits.Add("PDF");
                    var archiveMsg = archiveBits.Count > 0
                        ? (" ER Form " + string.Join(" + ", archiveBits) + " archived.")
                        : " Discharge finalized (document archive skipped or failed).";

                    return Json(new
                    {
                        ok = true,
                        discharged = true,
                        documentSaved = excelPdfArchived || pdfArchived,
                        message = "Form saved successfully." + archiveMsg,
                        savedOn = stateAfterFinalize.SavedOn?.ToString("dd-MMM-yyyy HH:mm"),
                        investigations = stateAfterFinalize.Investigations.Select(x => new
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
            }

            var state = saveResult.Data;
            return Json(new
            {
                ok = true,
                discharged = false,
                message = "Form saved successfully.",
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
            if (model.DischargeSummaryNotRequired)
                return string.Empty;

            var issues = new List<string>();
            if (!model.GcsCode.HasValue || model.GcsCode.Value <= 0) issues.Add("GCS is required.");
            if (!model.PlanterCode.HasValue || model.PlanterCode.Value <= 0) issues.Add("Planter response is required.");
            if (!model.CvsCode.HasValue || model.CvsCode.Value <= 0) issues.Add("CVS selection is required.");
            if (!model.RespiratoryCode.HasValue || model.RespiratoryCode.Value <= 0) issues.Add("Respiratory selection is required.");
            if (!model.BowelSoundCode.HasValue || model.BowelSoundCode.Value <= 0) issues.Add("Bowel sound selection is required.");
            if (!model.AbdomenCode.HasValue || model.AbdomenCode.Value <= 0) issues.Add("Abdomen selection is required.");
            if (!model.AdmissionCategoryCode.HasValue || model.AdmissionCategoryCode.Value <= 0) issues.Add("Admission category is required.");
            if (!model.ReceivedFromCode.HasValue || model.ReceivedFromCode.Value <= 0) issues.Add("Received from is required.");
            if (!model.OutcomeCode.HasValue || model.OutcomeCode.Value <= 0) issues.Add("Outcome selection is required.");
            if (!model.ConditionUponReleaseCode.HasValue || model.ConditionUponReleaseCode.Value <= 0) issues.Add("Condition upon release is required.");
            if (!model.AdrCode.HasValue || model.AdrCode.Value <= 0) issues.Add("ADR selection is required.");
            if (model.ChiefComplaints == null || model.ChiefComplaints.Count == 0) issues.Add("At least one chief complaint tick is required.");
            if (model.PastHistoryCodes == null || model.PastHistoryCodes.Count == 0) issues.Add("At least one past history tick is required.");

            if (issues.Count == 0)
                return string.Empty;

            return "Complete these before Discharged Finalize: " + string.Join(" ", issues);
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
                return JsonSaveError("ER form is discharge finalized and now read-only.");

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
                return JsonSaveError("ER form is discharge finalized and now read-only.");

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

        [RequireERRole("MO")]
        [HttpPost]
        public ActionResult SaveIpdAdmissionOrder()
        {
            if (!CurrentUserRole.MO)
                return JsonSaveError("Only MO can save IPD Admission Order.");

            SaveIpdAdmissionOrderInputViewModel model;
            try
            {
                Request.InputStream.Position = 0;
                using (var reader = new StreamReader(Request.InputStream))
                    model = JsonConvert.DeserializeObject<SaveIpdAdmissionOrderInputViewModel>(reader.ReadToEnd());
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(EmergencyFormController), nameof(SaveIpdAdmissionOrder), ex);
                return JsonSaveError("Invalid save request.");
            }

            if (model == null || string.IsNullOrWhiteSpace(model.PatientId))
                return JsonSaveError("Patient record is missing.");

            if (IsPatientAdmissionDischarged(model.PatientId, model.AdmissionCode, CurrentUserRole.CompanyCode))
                return JsonSaveError("ER form is discharge finalized and now read-only.");

            var saveResult = _erFormService.SaveIpdAdmissionOrder(
                model,
                CurrentUserRole.CompanyCode,
                CurrentUserRole.UserCode);

            if (!saveResult.Ok)
            {
                var rawMessage = saveResult.Message ?? string.Empty;
                if (rawMessage.StartsWith("CONFIRM_REPLACE_OUTCOME|", StringComparison.OrdinalIgnoreCase))
                {
                    var confirmMessage = rawMessage.Substring("CONFIRM_REPLACE_OUTCOME|".Length);
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

                return JsonSaveError(string.IsNullOrWhiteSpace(saveResult.Message)
                    ? "Error occurred in IPD Admission Order."
                    : ("Error occurred in IPD Admission Order: " + saveResult.Message));
            }

            return Json(new
            {
                ok = true,
                message = "IPD Admission Order save successfully."
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
                return JsonSaveError("ER form is discharge finalized and now read-only.");

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

        [HttpGet]
        public ActionResult DownloadErFormExcel(string patientId)
        {
            if (string.IsNullOrWhiteSpace(patientId))
                return HttpNotFound();

            var erPatient = ResolveErPatientForSave(patientId, CurrentUserRole.CompanyCode);
            if (erPatient == null || !erPatient.intERAdmissionCode.HasValue || erPatient.intERAdmissionCode.Value <= 0)
                return HttpNotFound();

            var admissionCode = erPatient.intERAdmissionCode.Value;
            try
            {
                using (var db = dbAMCEntities.Create())
                {
                    // Retrieve stored PDF from tblClinicalDocument — do not regenerate.
                    var entity = QueryStoredErFormDocuments(db, admissionCode, CurrentUserRole.CompanyCode)
                        .OrderByDescending(x => x.intClinicalDocumentCode)
                        .FirstOrDefault();

                    if (entity == null || entity.vbrDocument == null || entity.vbrDocument.Length == 0)
                        return HttpNotFound();

                    var bytes = entity.vbrDocument;
                    var fileName = string.IsNullOrWhiteSpace(entity.strDocumentName)
                        ? ("ERForm-" + admissionCode + ".pdf")
                        : (entity.strDocumentName.Trim().EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                            ? entity.strDocumentName.Trim()
                            : (entity.strDocumentName.Trim() + ".pdf"));

                    return File(bytes, "application/pdf", fileName);
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(EmergencyFormController), nameof(DownloadErFormExcel), ex);
                return HttpNotFound();
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
                return Json(new { ok = false, message = "ER form is discharge finalized and now read-only." });
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
        public ActionResult GetDischargeEligibility(string patientId, long? admissionCode, bool? dischargeSummaryNotRequired)
        {
            using (var db = dbAMCEntities.Create())
            {
                var context = ResolveDischargeContext(db, patientId, admissionCode, CurrentUserRole.CompanyCode);
                if (context == null)
                {
                    return Json(new
                    {
                        ok = true,
                        canDischarge = false,
                        message = "Patient context not found.",
                        bypassed = false
                    }, JsonRequestBehavior.AllowGet);
                }

                if (IsDischargeStarted(context.ErPatient))
                {
                    return Json(new
                    {
                        ok = true,
                        canDischarge = false,
                        message = "Discharge is already finalized. Form is read-only.",
                        bypassed = false
                    }, JsonRequestBehavior.AllowGet);
                }

                // Discharge Summary not required → pending Pharmacy/Investigation/Surgical only.
                var result = dischargeSummaryNotRequired == true
                    ? EvaluatePendingItemsForFinalize(db, context, CurrentUserRole.CompanyCode)
                    : EvaluateFinalizeDischargeRequirements(db, context, CurrentUserRole.CompanyCode);

                return Json(new
                {
                    ok = true,
                    canDischarge = result.CanDischarge,
                    message = result.Message ?? string.Empty,
                    bypassed = false,
                    pendingItemsOnly = dischargeSummaryNotRequired == true
                }, JsonRequestBehavior.AllowGet);
            }
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

                    var saveDocOk = SaveClinicalDocument(
                        db,
                        context,
                        pdfBytes,
                        ".pdf",
                        CurrentUserRole.CompanyCode,
                        CurrentUserRole.UserCode,
                        now);
                    if (!saveDocOk)
                        return Json(new { ok = false, message = "Patient discharged but ER document could not be archived in tblClinicalDocument." });

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
            return InlineOutcomePdf(bytes, "Discharge Summary");
        }

        [RequireERRole("MO")]
        [HttpGet]
        public async Task<ActionResult> PrintAdmisisonOrder(long? admissionCode)
        {
            if (!admissionCode.HasValue || admissionCode.Value <= 0)
                return HttpNotFound();

            var bytes = await ReportManager.GetAdmissionOrderBytes(admissionCode.Value.ToString());
            return InlineOutcomePdf(bytes, "Admission Order");
        }


        [RequireERRole("MO")]
        [HttpGet]
        public async Task<ActionResult> PrintDeathCertificate(long? admissionCode)
        {
            if (!admissionCode.HasValue || admissionCode.Value <= 0)
                return HttpNotFound();

            var bytes = await ReportManager.GetDeathCertificateBytes(admissionCode.Value.ToString());
            return InlineOutcomePdf(bytes, "Death Certificate");
        }

        [RequireERRole("MO")]
        [HttpGet]
        public async Task<ActionResult> PrintPatientReferral(long? admissionCode)
        {
            if (!admissionCode.HasValue || admissionCode.Value <= 0)
                return HttpNotFound();

            var bytes = await ReportManager.GetPatientReferralBytes(admissionCode.Value.ToString());
            return InlineOutcomePdf(bytes, "Patient Referral");
        }

        [RequireERRole("MO")]
        [HttpGet]
        public async Task<ActionResult> PrintLama(long? admissionCode)
        {
            if (!admissionCode.HasValue || admissionCode.Value <= 0)
                return HttpNotFound();

            var bytes = await ReportManager.GetLAMABytes(admissionCode.Value.ToString());
            return InlineOutcomePdf(bytes, "LAMA");
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
                        return InlineOutcomePdf(bytes, "Discharge Summary");
                }

                var hasDeath = db.tblDeathCertificates.Any(x =>
                    x.intERAdmissionCode == context.AdmissionCode
                    && x.intCompanyCode == CurrentUserRole.CompanyCode
                    && x.intRecordStatusCode != 8);
                if (hasDeath)
                {
                    var bytes = await ReportManager.GetDeathCertificateBytes(context.AdmissionCode.ToString());
                    if (bytes != null && bytes.Length > 0)
                        return InlineOutcomePdf(bytes, "Death Certificate");
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
                        return InlineOutcomePdf(bytes, "Patient Referral");
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
                        return InlineOutcomePdf(bytes, "LAMA");
                }

                return HttpNotFound("No active outcome form found to print.");
            }
        }

        private ActionResult InlineOutcomePdf(byte[] bytes, string title)
        {
            if (bytes == null || bytes.Length == 0)
                return HttpNotFound();

            var safeTitle = (title ?? "Document")
                .Replace("\"", string.Empty)
                .Replace("\r", string.Empty)
                .Replace("\n", string.Empty)
                .Trim();
            if (string.IsNullOrEmpty(safeTitle))
                safeTitle = "Document";

            Response.AppendHeader("Content-Disposition", "inline; filename=\"" + safeTitle + ".pdf\"");
            return File(bytes, "application/pdf");
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

        private void ApplyDefaultMoFromLoggedInUser(ErFormViewModel model)
        {
            if (model?.OutcomeForms == null || CurrentUserRole == null) return;

            var empCode = CurrentUserRole.EmployeeCode > 0
                ? CurrentUserRole.EmployeeCode
                : CurrentUserRole.UserCode;

            ApplyDefaultFromOptions(
                model.MoEmployeeOptions,
                empCode,
                CurrentUserRole.UserCode,
                CurrentUserRole.UserName,
                code =>
                {
                    if (model.OutcomeForms.Referral != null
                        && (!model.OutcomeForms.Referral.MoOnDutyCode.HasValue
                            || model.OutcomeForms.Referral.MoOnDutyCode.Value <= 0))
                        model.OutcomeForms.Referral.MoOnDutyCode = code;
                    if (model.OutcomeForms.Lama != null
                        && (!model.OutcomeForms.Lama.DutyMoCode.HasValue
                            || model.OutcomeForms.Lama.DutyMoCode.Value <= 0))
                        model.OutcomeForms.Lama.DutyMoCode = code;
                    if (model.OutcomeForms.Death != null
                        && (!model.OutcomeForms.Death.DutyMoCode.HasValue
                            || model.OutcomeForms.Death.DutyMoCode.Value <= 0))
                        model.OutcomeForms.Death.DutyMoCode = code;
                });

            ApplyDefaultFromOptions(
                model.EmployeeOptions,
                empCode,
                CurrentUserRole.UserCode,
                CurrentUserRole.UserName,
                code =>
                {
                    if (model.OutcomeForms.Lama != null && !model.OutcomeForms.Lama.DutyNurseCode.HasValue)
                        model.OutcomeForms.Lama.DutyNurseCode = code;
                    if (model.OutcomeForms.Death != null && !model.OutcomeForms.Death.DutyNurseCode.HasValue)
                        model.OutcomeForms.Death.DutyNurseCode = code;
                });
        }

        private static void ApplyDefaultFromOptions(
            IEnumerable<ERLovOptionViewModel> options,
            int employeeCode,
            int userCode,
            string userName,
            Action<int> apply)
        {
            if (apply == null) return;

            var list = (options ?? Enumerable.Empty<ERLovOptionViewModel>())
                .Where(x => x != null && x.Id > 0)
                .ToList();
            if (list.Count == 0) return;

            var match = list.FirstOrDefault(x => employeeCode > 0 && x.Id == employeeCode)
                ?? list.FirstOrDefault(x => userCode > 0 && x.Id == userCode);

            if (match == null && !string.IsNullOrWhiteSpace(userName))
            {
                var normalized = userName.Trim();
                match = list.FirstOrDefault(x =>
                    string.Equals((x.Name ?? string.Empty).Trim(), normalized, StringComparison.OrdinalIgnoreCase));
            }

            if (match != null)
                apply(match.Id);
        }

        private static DischargeEligibilityResult EvaluateDischargeEligibility(
            dbAMCEntities db,
            DischargeContext context,
            int companyCode)
        {
            if (context == null) return DischargeEligibilityResult.Fail("Patient context not found.");
            if (IsDischargeStarted(context.ErPatient))
                return DischargeEligibilityResult.Fail("Discharge is already finalized. Form is read-only.");

            return EvaluateFinalizeDischargeRequirements(db, context, companyCode);
        }

        private static DischargeEligibilityResult EvaluateFinalizeDischargeRequirements(
            dbAMCEntities db,
            DischargeContext context,
            int companyCode)
        {
            if (context == null) return DischargeEligibilityResult.Fail("Patient context not found.");

            var pending = EvaluatePendingItemsForFinalize(db, context, companyCode);
            if (!pending.CanDischarge)
                return pending;

            var issues = new List<string>();
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
            if (context == null) return DischargeEligibilityResult.Fail("Patient context not found.");

            var issues = new List<string>();

            // Investigation: Completed (acknowledged/charged) or Cancelled/Discontinued.
            var hasPendingInvestigations = db.tblERPatientInvestigations.Any(x =>
                x.intERPatientCode == context.ErPatientCode
                && x.intCompanyCode == companyCode
                && x.intRecordStatusCode == 1
                && !x.bolIsAcknowledged
                && !x.bolIsCancelled);
            if (hasPendingInvestigations)
                issues.Add("Investigations are pending (mark Completed/Charged or Cancelled/Discontinued).");

            // Pharmacy medicines: Charged/Completed or Discontinued/Cancelled.
            var hasPendingMedicine = db.tblERPatientPackageOrders.Any(x =>
                x.intERPatientCode == context.ErPatientCode
                && x.intCompanyCode == companyCode
                && x.intRecordStatusCode == 1
                && x.intPackageTypeCode == 1
                && !x.bolIsAcknowledged
                && !x.bolDiscontinue);
            if (hasPendingMedicine)
                issues.Add("Pharmacy medicines are pending (mark Completed/Charged or Cancelled/Discontinued).");

            // Surgical: Completed/Charged or Discontinued/Cancelled.
            var hasPendingSurgical = db.tblERPatientPackageOrders.Any(x =>
                x.intERPatientCode == context.ErPatientCode
                && x.intCompanyCode == companyCode
                && x.intRecordStatusCode == 1
                && x.intPackageTypeCode == 2
                && !x.bolIsAcknowledged
                && !x.bolDiscontinue);
            if (hasPendingSurgical)
                issues.Add("Surgical items are pending (mark Completed/Charged or Cancelled/Discontinued).");

            if (issues.Count == 0)
                return DischargeEligibilityResult.Success();

            return DischargeEligibilityResult.Fail(
                "Discharge cannot be finalized/closed until all Pharmacy, Investigation, and Surgical items are Completed/Charged or Cancelled/Discontinued. "
                + string.Join(" ", issues));
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

        private static byte[] CompressGZip(byte[] source)
        {
            if (source == null || source.Length == 0)
                return source;

            try
            {
                using (var output = new MemoryStream())
                {
                    using (var gzip = new System.IO.Compression.GZipStream(output, System.IO.Compression.CompressionLevel.Optimal, true))
                    {
                        gzip.Write(source, 0, source.Length);
                    }
                    return output.ToArray();
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(EmergencyFormController), nameof(CompressGZip), ex);
                return source;
            }
        }

        private static byte[] TryDecompressGZip(byte[] source)
        {
            if (source == null || source.Length < 2)
                return source;

            // GZip magic header 1F 8B
            if (source[0] != 0x1F || source[1] != 0x8B)
                return source;

            try
            {
                using (var input = new MemoryStream(source))
                using (var gzip = new System.IO.Compression.GZipStream(input, System.IO.Compression.CompressionMode.Decompress))
                using (var output = new MemoryStream())
                {
                    gzip.CopyTo(output);
                    return output.ToArray();
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(EmergencyFormController), nameof(TryDecompressGZip), ex);
                return source;
            }
        }

        private const int ErFormClinicalDocumentTemplateCode = 9;

        private static IQueryable<tblClinicalDocument> QueryStoredErFormDocuments(
            dbAMCEntities db,
            long admissionCode,
            int companyCode,
            string extension = ".pdf")
        {
            var docExt = string.IsNullOrWhiteSpace(extension) ? ".pdf" : extension.Trim();
            if (!docExt.StartsWith(".", StringComparison.Ordinal))
                docExt = "." + docExt;

            return db.tblClinicalDocuments
                .Where(x => x.intCompanyCode == companyCode
                            && x.intERAdmissionCode == admissionCode
                            && x.intClinicalDocumentTemplateCode == ErFormClinicalDocumentTemplateCode
                            && x.strDocumentExtension == docExt
                            && x.intRecordStatusCode != 8
                            && x.vbrDocument != null);
        }

        private static bool HasStoredErFormPdf(long? admissionCode, int companyCode)
        {
            if (!admissionCode.HasValue || admissionCode.Value <= 0 || companyCode <= 0)
                return false;

            try
            {
                using (var db = dbAMCEntities.Create())
                {
                    return QueryStoredErFormDocuments(db, admissionCode.Value, companyCode)
                        .Any();
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(EmergencyFormController), nameof(HasStoredErFormPdf), ex);
                return false;
            }
        }

        private static string NormalizeClinicalDocumentName(string documentNameOverride, long admissionCode, string docExt)
        {
            var documentName = !string.IsNullOrWhiteSpace(documentNameOverride)
                ? documentNameOverride.Trim()
                : ("ERForm-" + admissionCode);
            if (documentName.EndsWith(".xls.gz", StringComparison.OrdinalIgnoreCase))
                documentName = documentName.Substring(0, documentName.Length - 7);
            else if (documentName.EndsWith(".xlsx.gz", StringComparison.OrdinalIgnoreCase))
                documentName = documentName.Substring(0, documentName.Length - 8);
            else if (documentName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                documentName = documentName.Substring(0, documentName.Length - 5);
            else if (documentName.EndsWith(".xls", StringComparison.OrdinalIgnoreCase))
                documentName = documentName.Substring(0, documentName.Length - 4);
            else if (documentName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                documentName = documentName.Substring(0, documentName.Length - 4);
            else if (documentName.EndsWith(docExt, StringComparison.OrdinalIgnoreCase))
                documentName = documentName.Substring(0, documentName.Length - docExt.Length);
            if (documentName.Length > 50)
                documentName = documentName.Substring(0, 50);
            return documentName;
        }

        private static bool SaveClinicalDocument(
            dbAMCEntities db,
            DischargeContext context,
            byte[] documentBytes,
            string extension,
            int companyCode,
            int userCode,
            DateTime now,
            string documentNameOverride = null)
        {
            if (db == null || context == null || documentBytes == null || documentBytes.Length == 0)
                return false;

            var docExt = string.IsNullOrWhiteSpace(extension) ? ".pdf" : extension.Trim();
            if (!docExt.StartsWith(".", StringComparison.Ordinal))
                docExt = "." + docExt;

            try
            {
                var documentName = NormalizeClinicalDocumentName(documentNameOverride, context.AdmissionCode, docExt);

                var entity = db.tblClinicalDocuments
                    .Where(x => x.intCompanyCode == companyCode
                                && x.intERAdmissionCode == context.AdmissionCode
                                && x.intClinicalDocumentTemplateCode == ErFormClinicalDocumentTemplateCode
                                && x.strDocumentExtension == docExt
                                && x.intRecordStatusCode != 8)
                    .OrderByDescending(x => x.intClinicalDocumentCode)
                    .FirstOrDefault();

                if (entity != null)
                {
                    entity.dtmClinicalDocument = now;
                    entity.intPatientCode = context.PatientCode;
                    entity.intERAdmissionCode = context.AdmissionCode;
                    entity.intIPDAdmissionCode = null;
                    entity.strDocumentName = documentName;
                    entity.strDocumentExtension = docExt;
                    entity.vbrDocument = documentBytes;
                    entity.bolIsFinal = true;
                    entity.dtmLastM = now;
                    entity.intAlteredByCode = userCode;
                    entity.intRecordStatusCode = 1;
                    entity.intBranchCode = context.BranchCode;
                }
                else
                {
                    var nextCode = db.tblClinicalDocuments
                        .Where(x => x.intCompanyCode == companyCode)
                        .Select(x => (long?)x.intClinicalDocumentCode)
                        .Max();

                    entity = new tblClinicalDocument
                    {
                        intClinicalDocumentCode = (nextCode ?? 0L) + 1L,
                        dtmClinicalDocument = now,
                        intClinicalDocumentID = Guid.NewGuid(),
                        intClinicalDocumentTemplateCode = ErFormClinicalDocumentTemplateCode,
                        intFileTypeCode = null,
                        intIPDAdmissionCode = null,
                        intERAdmissionCode = context.AdmissionCode,
                        intPatientCode = context.PatientCode,
                        strDocumentName = documentName,
                        strDocumentExtension = docExt,
                        vbrDocument = documentBytes,
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

                    db.tblClinicalDocuments.Add(entity);
                }

                db.SaveChanges();
                return true;
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(EmergencyFormController), nameof(SaveClinicalDocument), ex);
                return SaveClinicalDocumentSql(context, documentBytes, docExt, companyCode, userCode, now, documentNameOverride);
            }
        }

        private static bool SaveClinicalDocumentSql(
            DischargeContext context,
            byte[] documentBytes,
            string extension,
            int companyCode,
            int userCode,
            DateTime now,
            string documentNameOverride = null)
        {
            if (context == null || documentBytes == null || documentBytes.Length == 0)
                return false;

            var docExt = string.IsNullOrWhiteSpace(extension) ? ".pdf" : extension.Trim();
            if (!docExt.StartsWith(".", StringComparison.Ordinal))
                docExt = "." + docExt;

            try
            {
                var documentName = NormalizeClinicalDocumentName(documentNameOverride, context.AdmissionCode, docExt);

                using (var conn = DBHelper.GetConnection())
                {
                    conn.Open();

                    long existingCode = 0;
                    using (var findCmd = new SqlCommand(@"
SELECT TOP 1 intClinicalDocumentCode
FROM tblClinicalDocument
WHERE intCompanyCode = @companyCode
  AND intERAdmissionCode = @admissionCode
  AND intClinicalDocumentTemplateCode = @templateCode
  AND strDocumentExtension = @docExt
  AND intRecordStatusCode <> 8
ORDER BY intClinicalDocumentCode DESC", conn))
                    {
                        findCmd.Parameters.Add("@companyCode", SqlDbType.Int).Value = companyCode;
                        findCmd.Parameters.Add("@admissionCode", SqlDbType.BigInt).Value = context.AdmissionCode;
                        findCmd.Parameters.Add("@templateCode", SqlDbType.Int).Value = ErFormClinicalDocumentTemplateCode;
                        findCmd.Parameters.Add("@docExt", SqlDbType.NVarChar, 10).Value = docExt;
                        var result = findCmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                            existingCode = Convert.ToInt64(result);
                    }

                    if (existingCode > 0)
                    {
                        using (var upd = new SqlCommand(@"
UPDATE tblClinicalDocument
SET dtmClinicalDocument = @docDate,
    intPatientCode = @patientCode,
    intERAdmissionCode = @admissionCode,
    intIPDAdmissionCode = NULL,
    strDocumentName = @docName,
    strDocumentExtension = @docExt,
    vbrDocument = @docBytes,
    bolIsFinal = 1,
    dtmLastM = @now,
    intAlteredByCode = @userCode,
    intRecordStatusCode = 1,
    intBranchCode = @branchCode
WHERE intClinicalDocumentCode = @code
  AND intCompanyCode = @companyCode", conn))
                        {
                            upd.Parameters.Add("@docDate", SqlDbType.DateTime).Value = now;
                            upd.Parameters.Add("@patientCode", SqlDbType.BigInt).Value = context.PatientCode;
                            upd.Parameters.Add("@admissionCode", SqlDbType.BigInt).Value = context.AdmissionCode;
                            upd.Parameters.Add("@docName", SqlDbType.NVarChar, 50).Value = documentName;
                            upd.Parameters.Add("@docExt", SqlDbType.NVarChar, 10).Value = docExt;
                            upd.Parameters.Add("@docBytes", SqlDbType.VarBinary, -1).Value = documentBytes;
                            upd.Parameters.Add("@now", SqlDbType.DateTime).Value = now;
                            upd.Parameters.Add("@userCode", SqlDbType.Int).Value = userCode;
                            upd.Parameters.Add("@branchCode", SqlDbType.Int).Value = context.BranchCode;
                            upd.Parameters.Add("@code", SqlDbType.BigInt).Value = existingCode;
                            upd.Parameters.Add("@companyCode", SqlDbType.Int).Value = companyCode;
                            return upd.ExecuteNonQuery() > 0;
                        }
                    }

                    long nextCode;
                    using (var maxCmd = new SqlCommand(@"
SELECT ISNULL(MAX(intClinicalDocumentCode), 0) + 1
FROM tblClinicalDocument
WHERE intCompanyCode = @companyCode", conn))
                    {
                        maxCmd.Parameters.Add("@companyCode", SqlDbType.Int).Value = companyCode;
                        nextCode = Convert.ToInt64(maxCmd.ExecuteScalar());
                    }

                    using (var ins = new SqlCommand(@"
INSERT INTO tblClinicalDocument
(
    intClinicalDocumentCode, dtmClinicalDocument, intClinicalDocumentID,
    intClinicalDocumentTemplateCode, intFileTypeCode, intIPDAdmissionCode,
    intERAdmissionCode, intPatientCode,
    strDocumentName, strDocumentExtension, vbrDocument, bolIsFinal,
    dtmCreated, dtmLastM, intOwnerCode, intCreatedByCode, intAlteredByCode,
    intRecordStatusCode, intBranchCode, intCompanyCode
)
VALUES
(
    @code, @docDate, @docId,
    @templateCode, NULL, NULL,
    @admissionCode, @patientCode,
    @docName, @docExt, @docBytes, 1,
    @now, @now, @userCode, @userCode, @userCode,
    1, @branchCode, @companyCode
)", conn))
                    {
                        ins.Parameters.Add("@code", SqlDbType.BigInt).Value = nextCode;
                        ins.Parameters.Add("@docDate", SqlDbType.DateTime).Value = now;
                        ins.Parameters.Add("@docId", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
                        ins.Parameters.Add("@templateCode", SqlDbType.Int).Value = ErFormClinicalDocumentTemplateCode;
                        ins.Parameters.Add("@admissionCode", SqlDbType.BigInt).Value = context.AdmissionCode;
                        ins.Parameters.Add("@patientCode", SqlDbType.BigInt).Value = context.PatientCode;
                        ins.Parameters.Add("@docName", SqlDbType.NVarChar, 50).Value = documentName;
                        ins.Parameters.Add("@docExt", SqlDbType.NVarChar, 10).Value = docExt;
                        ins.Parameters.Add("@docBytes", SqlDbType.VarBinary, -1).Value = documentBytes;
                        ins.Parameters.Add("@now", SqlDbType.DateTime).Value = now;
                        ins.Parameters.Add("@userCode", SqlDbType.Int).Value = userCode;
                        ins.Parameters.Add("@branchCode", SqlDbType.Int).Value = context.BranchCode;
                        ins.Parameters.Add("@companyCode", SqlDbType.Int).Value = companyCode;
                        return ins.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(EmergencyFormController), nameof(SaveClinicalDocumentSql), ex);
                return false;
            }
        }

        private static bool UpdateWardBedStatusForDischarge(
            int wardBedCode,
            int userCode,
            int companyCode,
            int wardBedStatusCode = 8)
        {
            if (wardBedCode <= 0 || userCode <= 0 || companyCode <= 0)
                return false;

            try
            {
                using (var conn = DBHelper.GetConnection())
                using (var cmd = new SqlCommand("procUpdateWardBedStatusForERPortal", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@intWardBedStatusCode", SqlDbType.Int).Value = wardBedStatusCode;
                    cmd.Parameters.Add("@intWardBedCode", SqlDbType.Int).Value = wardBedCode;
                    cmd.Parameters.Add("@intUserCode", SqlDbType.Int).Value = userCode;
                    cmd.Parameters.Add("@intCompanyCode", SqlDbType.Int).Value = companyCode;
                    conn.Open();
                    cmd.ExecuteNonQuery();
                    return true;
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
                {
                    conn.Open();

                    // Existing portal proc (may update ER patient discharge flags).
                    using (var cmd = new SqlCommand("procUpdateERPatientDischargedForERPortal", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.Add("@bolIsDischarge", SqlDbType.Bit).Value = true;
                        cmd.Parameters.Add("@intERAdmissionCode", SqlDbType.BigInt).Value = admissionCode;
                        cmd.Parameters.Add("@intUserCode", SqlDbType.Int).Value = userCode;
                        cmd.Parameters.Add("@intCompanyCode", SqlDbType.Int).Value = companyCode;
                        cmd.ExecuteNonQuery();
                    }

                    // Ensure admission-level flag used by ER form read-only mode.
                    using (var upd = new SqlCommand(@"
UPDATE tblERAdmission
SET bolIsDischarged = 1,
    dtmLastM = GETDATE(),
    intAlteredByCode = @userCode
WHERE intERAdmissionCode = @admissionCode
  AND intCompanyCode = @companyCode", conn))
                    {
                        upd.Parameters.Add("@userCode", SqlDbType.Int).Value = userCode;
                        upd.Parameters.Add("@admissionCode", SqlDbType.BigInt).Value = admissionCode;
                        upd.Parameters.Add("@companyCode", SqlDbType.Int).Value = companyCode;
                        upd.ExecuteNonQuery();
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(EmergencyFormController), nameof(UpdateErAdmissionDischargedForPortal), ex);
                return false;
            }
        }

        private static tblERPatient ResolveErPatientForSave(string patientId, int companyCode)
        {
            long erPatientCode;
            if (!long.TryParse((patientId ?? string.Empty).Trim(), out erPatientCode) || erPatientCode <= 0)
                return null;

            try
            {
                using (var db = dbAMCEntities.Create())
                {
                    return db.tblERPatients.FirstOrDefault(x =>
                        x.intERPatientCode == erPatientCode
                        && x.intCompanyCode == companyCode
                        && x.intRecordStatusCode != 8);
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(EmergencyFormController), nameof(ResolveErPatientForSave), ex);
                return null;
            }
        }

        private bool IsPatientAdmissionDischarged(string patientId, long? admissionCode, int companyCode)
        {
            using (var db = dbAMCEntities.Create())
            {
                long erPatientCode;
                if (!long.TryParse((patientId ?? string.Empty).Trim(), out erPatientCode) || erPatientCode <= 0)
                    return false;

                var erPatient = db.tblERPatients.FirstOrDefault(x =>
                    x.intERPatientCode == erPatientCode
                    && x.intCompanyCode == companyCode
                    && x.intRecordStatusCode != 8);

                // Primary gate: discharge finalize start flag on tblERPatient.
                if (IsDischargeStarted(erPatient))
                    return true;

                // Pending MR (no admission) remains editable unless discharge start is set.
                var context = ResolveDischargeContext(db, patientId, admissionCode, companyCode);
                if (context == null)
                    return false;

                return IsDischargeStarted(context.ErPatient);
            }
        }

        private static bool IsDischargeStarted(tblERPatient erPatient)
        {
            return erPatient != null
                && (erPatient.bolIsDischargeStart == true || erPatient.bolIsDischarge == true);
        }

        private static bool MarkErPatientDischargeStart(
            dbAMCEntities db,
            tblERPatient erPatient,
            int userCode,
            DateTime now)
        {
            if (db == null || erPatient == null)
                return false;

            try
            {
                erPatient.bolIsDischargeStart = true;
                erPatient.dtmDischargeStart = now;
                erPatient.dtmLastM = now;
                erPatient.intAlteredByCode = userCode;
                db.SaveChanges();
                return true;
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(EmergencyFormController), nameof(MarkErPatientDischargeStart), ex);
                return false;
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
                flags.CanPrintAdmission = db.tblIPDAdmOrders.Any(x =>
                    x.intERAdmissionCode == code && x.intCompanyCode == companyCode && x.intRecordStatusCode == 1);
            }

            return flags;
        }

        private sealed class PrintableOutcomeFlags
        {
            public bool CanPrintDischarge { get; set; }
            public bool CanPrintDeath { get; set; }
            public bool CanPrintReferral { get; set; }
            public bool CanPrintLama { get; set; }
            public bool CanPrintAdmission { get; set; }
            public bool CanPrintAny =>
                CanPrintDischarge || CanPrintDeath || CanPrintReferral || CanPrintLama || CanPrintAdmission;
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
