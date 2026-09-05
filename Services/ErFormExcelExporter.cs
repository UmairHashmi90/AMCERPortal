using ERPaperless.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace ERPaperless.Services
{
    /// <summary>
    /// ER Form Excel export (Excel 2003 SpreadsheetML .xls).
    /// Single worksheet with visually separated sections, freeze panes, and row grouping.
    /// LOVs export selected description text only (never codes / full lists).
    /// </summary>
    public static class ErFormExcelExporter
    {
        private static readonly XNamespace Ss = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        private const int FirstCol = 2;
        private const int LastCol = 11;
        private const int ColCount = 10;
        private const int ShortLovMaxItems = 7;

        private const int SPage = 0;
        private const int SCard = 1;
        private const int SLabel = 2;
        private const int SValue = 3;
        private const int SSection = 4;
        private const int SOrange = 5;
        private const int SGreen = 6;
        private const int SRed = 7;
        private const int STableHead = 8;
        private const int STitle = 9;
        private const int SMuted = 10;
        private const int SInput = 11;
        private const int SVitalBlue = 12;
        private const int SVitalOrange = 13;
        private const int SVitalTeal = 14;
        private const int SVitalPink = 15;
        private const int SVitalCyan = 16;
        private const int SVitalGreen = 17;
        private const int SVitalPurple = 18;
        private const int SVitalYellow = 19;
        private const int SCardCenter = 20;
        private const int SOrangeCenter = 21;
        private const int SStatusOk = 22;
        private const int SStatusBad = 23;
        private const int SStatusPending = 24;

        public static byte[] Build(ErFormViewModel model, DateTime generatedOn)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));

            var sheet = new SheetBuilder();
            var review = model.ReviewForm ?? new ErReviewFormStateViewModel();
            var outcome = model.OutcomeForms ?? new OutcomeFormStateViewModel();

            // ---- Banner (frozen) ----
            sheet.AddSpacer(1);
            sheet.BeginCard();
            sheet.AddMerged(STitle, Clean(model.PatientName) ?? "ER Form", FirstCol, LastCol, 32);
            sheet.AddMerged(SMuted,
                "MR " + TextOr(model.MrNo)
                + "  |  Admission " + TextOr(model.AdmissionNo)
                + "  |  Bed " + TextOr(model.BedNo)
                + "  |  Generated "
                + generatedOn.ToString("dd-MMM-yyyy HH:mm", CultureInfo.InvariantCulture),
                FirstCol, LastCol, 22);
            sheet.EndCard();
            sheet.FreezeAfterCurrentRow();

            // ---- Patient Details ----
            BeginSection(sheet, "PATIENT DETAILS");
            sheet.AddMerged(SMuted,
                "Latest vitals recorded: " + (model.LatestVital != null
                    ? model.LatestVital.RecordedOn.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
                    : "-"),
                FirstCol, LastCol, 16);
            AddDetailPair(sheet, "Patient Name", model.PatientName, "MR Number", model.MrNo);
            AddDetailPair(sheet, "Admission No", model.AdmissionNo, "Bed / Chair", model.BedNo);
            AddDetailPair(sheet, "Age / Gender", model.AgeGender, "Admission Date",
                model.AdmissionDate.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture));
            sheet.AddCardRow();

            var v = model.LatestVital ?? new VitalRecordViewModel();
            var bp = string.IsNullOrWhiteSpace(v.BloodPressure)
                ? (Nz(v.BP1) + "/" + Nz(v.BP2)).Trim('/')
                : v.BloodPressure;
            AddVitalTileRow(sheet,
                "Heart Rate", Nz(v.HeartRate ?? v.Pulse), SVitalBlue,
                "Resp", Nz(v.RespiratoryRate), SVitalOrange,
                "BP", bp, SVitalPink,
                "SPO2", Nz(v.Spo2) + (v.Spo2.HasValue ? "%" : ""), SVitalTeal);
            AddVitalTileRow(sheet,
                "Temp", Nz(v.Temperature), SVitalCyan,
                "Glucose R", Nz(v.GlucoseR), SVitalPink,
                "BMI", Nz(v.MetricBMI), SVitalGreen,
                "Pain", Nz(v.PainScore), SVitalPurple);
            if (!string.IsNullOrWhiteSpace(v.Spo2Remark))
                sheet.AddMerged(SMuted, "SPO2 Comment: " + Clean(v.Spo2Remark), FirstCol, LastCol, 16);
            EndSection(sheet);

            // ---- Vitals History ----
            BeginSection(sheet, "VITALS HISTORY");
            sheet.AddTableHeader(new[] { "Date/Time", "HR", "BP", "Resp", "Temp", "Ht", "Wt", "BMI", "SPO2", "Pain" });
            var vitals = (model.VitalHistory ?? Enumerable.Empty<VitalRecordViewModel>())
                .OrderByDescending(x => x.RecordedOn).Take(12).ToList();
            if (vitals.Count == 0)
                sheet.AddTableRow(SCard, Pad10("-", "", "", "", "", "", "", "", "", ""));
            else
            {
                foreach (var row in vitals)
                {
                    var rbp = string.IsNullOrWhiteSpace(row.BloodPressure)
                        ? (Nz(row.BP1) + "/" + Nz(row.BP2)).Trim('/')
                        : row.BloodPressure;
                    sheet.AddTableRow(SCard, Pad10(
                        row.RecordedOn.ToString("dd/MM HH:mm", CultureInfo.InvariantCulture),
                        Nz(row.HeartRate ?? row.Pulse), rbp, Nz(row.RespiratoryRate), Nz(row.Temperature),
                        Nz(row.Height), Nz(row.Weight), Nz(row.MetricBMI), Nz(row.Spo2), Nz(row.PainScore)));
                }
            }
            EndSection(sheet);

            // ---- Chief Complaints ----
            BeginSection(sheet, "CHIEF COMPLAINTS");
            var complaints = (model.ChiefComplaints ?? Enumerable.Empty<ChiefComplaintItemViewModel>())
                .Where(x => x != null).ToList();
            WriteSelectedLovItems(sheet,
                complaints.Select(x => (x.Name, x.IsSelected, x.Remark)),
                complaints.Count);
            EndSection(sheet);

            // ---- Past History ----
            BeginSection(sheet, "PAST MEDICAL HISTORY");
            var selectedPast = review.SelectedPastHistoryCodes ?? new HashSet<int>();
            var pastAll = (model.PastHistories ?? Enumerable.Empty<ERLovOptionViewModel>())
                .Where(x => x != null).ToList();
            WriteSelectedLovItems(sheet,
                pastAll.Select(x => (x.Name, selectedPast.Contains(x.Id), (string)null)),
                pastAll.Count);
            sheet.AddFieldBlock("Other (specify)...", review.PastMedicalHistory);
            sheet.AddFieldBlock("Past Surgical History", review.PastSurgicalHistory);
            EndSection(sheet);

            // ---- Allergies ----
            BeginSection(sheet, "DRUG ALLERGY");
            var allergies = (model.DrugAllergies ?? Enumerable.Empty<DrugAllergyItemViewModel>())
                .Where(x => x != null).ToList();
            WriteSelectedLovItems(sheet,
                allergies.Select(x => (x.Name, x.IsSelected, x.Remark)),
                allergies.Count);
            sheet.AddFieldBlock("Food Allergy", review.FoodAllergy);
            EndSection(sheet);

            // ---- Clinical Findings ----
            BeginSection(sheet, "CLINICAL FINDINGS");
            AddFindingCell(sheet, "CNS / GCS", LovText(model.GcsOptions, review.GcsCode));
            AddFindingCell(sheet, "Planters", LovText(model.PlanterOptions, review.PlanterCode));
            AddFindingCell(sheet, "CVS", LovText(model.CvsOptions, review.CvsCode));
            AddFindingCell(sheet, "Respiratory", LovText(model.RespiratoryOptions, review.RespiratoryCode));
            AddFindingCell(sheet, "Bowel Sound", LovText(model.BowelSoundOptions, review.BowelSoundCode));
            AddFindingCell(sheet, "Abdomen", LovText(model.AbdomenOptions, review.AbdomenCode));
            AddFindingCell(sheet, "Admission Category", LovText(model.AdmissionCategoryOptions, review.AdmissionCategoryCode));
            AddFindingCell(sheet, "Received From", LovText(model.ReceivedFromOptions, review.ReceivedFromCode));
            AddFindingCell(sheet, "Outcome", LovText(model.OutcomeOptions, review.OutcomeCode));
            AddFindingCell(sheet, "Condition Upon Release", LovText(model.ConditionUponReleaseOptions, review.ConditionUponReleaseCode));
            AddFindingCell(sheet, "ADR", LovText(model.AdrOptions, review.AdrCode));
            sheet.AddFieldBlock("Discussed With", review.DiscussedWith);
            sheet.AddFieldBlock("Referred To", review.ReferredTo);
            EndSection(sheet);

            // ---- Diagnosis ----
            BeginSection(sheet, "DIAGNOSIS");
            sheet.AddFieldBlock("Assessment / Diagnosis", review.AssessmentDiagnosis);
            EndSection(sheet);

            // ---- Investigations ----
            BeginSection(sheet, "INVESTIGATIONS", orange: true);
            sheet.AddTableHeader(new[] { "S.No", "Item Name", "Remark", "Status", "By", "Date", "", "", "", "" });
            var invs = (review.Investigations ?? new List<InvestigationRowViewModel>())
                .Where(x => x != null && !x.IsDeleted).OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToList();
            if (invs.Count == 0)
                sheet.AddTableRow(SGreen, Pad10("-", "No investigations", "", "", "", "", "", "", "", ""));
            else
            {
                var n = 1;
                foreach (var inv in invs)
                {
                    var cancelled = inv.IsCancelled;
                    var style = cancelled ? SRed : SGreen;
                    var status = cancelled ? "Cancelled" : inv.IsAcknowledged ? "Completed/Charged" : "Pending";
                    var statusStyle = cancelled ? SStatusBad : inv.IsAcknowledged ? SStatusOk : SStatusPending;
                    sheet.AddTableRowMixed(style, new[]
                    {
                        (n.ToString(CultureInfo.InvariantCulture), style),
                        (Clean(inv.TestName) ?? "", style),
                        (Clean(inv.Remarks) ?? "", style),
                        (status, statusStyle),
                        (Clean(cancelled ? inv.CancelledByName : inv.AckByName) ?? "", style),
                        ((cancelled ? inv.CancelledDate : inv.AckDate)?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "", style),
                        ("", style), ("", style), ("", style), ("", style)
                    });
                    n++;
                }
            }
            EndSection(sheet);

            // ---- Medicine ----
            BeginSection(sheet, "MEDICINE PACKAGE", orange: true);
            sheet.AddMerged(SMuted,
                "Package: " + LovText(model.MedicinePackageOptions, review.MedicinePackageCode),
                FirstCol, LastCol, 16);
            WritePackageTable(sheet, review.MedicineOrders);
            EndSection(sheet);

            // ---- Surgical ----
            BeginSection(sheet, "SURGICAL PACKAGE", orange: true);
            sheet.AddMerged(SMuted,
                "Package: " + LovText(model.SurgicalPackageOptions, review.SurgicalPackageCode),
                FirstCol, LastCol, 16);
            WritePackageTable(sheet, review.SurgicalOrders);
            EndSection(sheet);

            // ---- Treatment ----
            BeginSection(sheet, "OTHER TREATMENT / CONSULTANT PLAN");
            sheet.AddFieldBlock("Treatment Notes", review.TreatmentNotes);
            sheet.AddFieldBlock("Consultant Plan", review.ConsultantPlan);
            EndSection(sheet);

            // ---- Nursing ----
            BeginSection(sheet, "NURSING ASSESSMENT & NOTES");
            sheet.AddTwoColumnFields("Nursing Observations", review.NursingObservations,
                "Care Plan / Instructions", review.NursingCarePlan);
            EndSection(sheet);

            // ---- Documents ----
            BeginSection(sheet, "DOCUMENTS");
            sheet.AddTableHeader(new[] { "S.No", "Document Name", "Type", "Uploaded By", "Uploaded On", "", "", "", "", "" });
            var docs = review.Documents ?? new List<DocumentRowViewModel>();
            if (docs.Count == 0)
                sheet.AddTableRow(SCard, Pad10("-", "No documents", "", "", "", "", "", "", "", ""));
            else
            {
                var n = 1;
                foreach (var d in docs)
                {
                    sheet.AddTableRow(SCard, Pad10(
                        n.ToString(CultureInfo.InvariantCulture),
                        Clean(d.FileName) ?? "",
                        Clean(d.DocumentType) ?? "",
                        Clean(d.UploadedByName) ?? "",
                        d.UploadedOn.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
                        "", "", "", "", ""));
                    n++;
                }
            }
            EndSection(sheet);

            WriteOutcomeSections(sheet, model, outcome);
            sheet.AddSpacer(2);
            return Pack(sheet);
        }

        private static void BeginSection(SheetBuilder sheet, string title, bool orange = false)
        {
            sheet.AddSpacer(1);
            sheet.BeginCard();
            // Section header stays at outline level 0; body rows are grouped (level 1).
            sheet.AddSectionHeader(orange ? SOrangeCenter : SSection, title);
            sheet.StartGroup();
        }

        private static void EndSection(SheetBuilder sheet)
        {
            sheet.EndGroup();
            sheet.EndCard();
            sheet.AddSpacer(1);
        }

        private static void WriteOutcomeSections(SheetBuilder sheet, ErFormViewModel model, OutcomeFormStateViewModel outcome)
        {
            var referral = outcome.Referral;
            if (referral != null && (referral.IsFinal || referral.ReferralReasonCode.GetValueOrDefault() > 0
                || HasText(referral.PatientComplaint, referral.ClinicalSummary, referral.Treatment,
                    referral.PertinentInvestigation, referral.ClinicalDiagnosis)))
            {
                BeginSection(sheet, "OUTCOME - PATIENT REFERRAL" + (referral.IsFinal ? "  [Finalized]" : ""));
                sheet.AddFieldBlock("Referral Reason", LovText(model.ReferralReasonOptions, referral.ReferralReasonCode));
                sheet.AddFieldBlock("Date/Time", referral.ReferralDateTime?.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture));
                sheet.AddFieldBlock("MO On Duty", LovText(model.MoEmployeeOptions, referral.MoOnDutyCode));
                sheet.AddFieldBlock("Presenting Complaint", referral.PatientComplaint);
                sheet.AddFieldBlock("Clinical Summary", referral.ClinicalSummary);
                sheet.AddFieldBlock("Treatment", referral.Treatment);
                sheet.AddFieldBlock("Pertinent Investigation", referral.PertinentInvestigation);
                sheet.AddFieldBlock("Clinical Diagnosis", referral.ClinicalDiagnosis);
                EndSection(sheet);
            }

            var discharge = outcome.Discharge;
            if (discharge != null && (discharge.IsFinal
                || HasText(discharge.FinalDiagnosis, discharge.BriefHistory, discharge.SurgeryProcedures,
                    discharge.DietInstructions, discharge.ClinicalAssessment, discharge.FollowUpInstructions)
                || (discharge.Medicines != null && discharge.Medicines.Count > 0)))
            {
                BeginSection(sheet, "OUTCOME - DISCHARGE SUMMARY" + (discharge.IsFinal ? "  [Finalized]" : ""));
                sheet.AddFieldBlock("Final Diagnosis", discharge.FinalDiagnosis);
                sheet.AddFieldBlock("Brief History / HOPI", discharge.BriefHistory);
                sheet.AddFieldBlock("Surgery / Procedures", discharge.SurgeryProcedures);
                sheet.AddFieldBlock("Diet Instructions", discharge.DietInstructions);
                sheet.AddFieldBlock("Clinical Assessment", discharge.ClinicalAssessment);
                sheet.AddFieldBlock("Follow-up Instructions", discharge.FollowUpInstructions);
                sheet.AddTableHeader(new[] { "Medicine", "Dose", "Frequency", "Days", "Instruction", "", "", "", "", "" });
                var meds = discharge.Medicines ?? new List<DischargeMedicineStateViewModel>();
                if (meds.Count == 0)
                    sheet.AddTableRow(SGreen, Pad10("No discharge medicines", "", "", "", "", "", "", "", "", ""));
                else
                {
                    foreach (var m in meds)
                        sheet.AddTableRow(SGreen, Pad10(
                            Clean(m.DrugName), Clean(m.Dose), Clean(m.Frequency), Nz(m.Days), Clean(m.Instruction),
                            "", "", "", "", ""));
                }
                EndSection(sheet);
            }

            var death = outcome.Death;
            if (death != null && (death.IsFinal || death.PrimaryConsultantCode.GetValueOrDefault() > 0
                || HasText(death.PrimaryCause, death.SecondaryCause, death.MedicalCondition, death.HandOverTo)))
            {
                BeginSection(sheet, "OUTCOME - DEATH CERTIFICATE" + (death.IsFinal ? "  [Finalized]" : ""));
                sheet.AddFieldBlock("Primary Consultant", LovText(model.ConsultantOptions, death.PrimaryConsultantCode));
                sheet.AddFieldBlock("Death Date/Time", death.DeathDateTime?.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture));
                sheet.AddFieldBlock("Primary Cause", death.PrimaryCause);
                sheet.AddFieldBlock("Secondary Cause", death.SecondaryCause);
                sheet.AddFieldBlock("Brought In Dead", death.IsBroughtInDead ? "Yes" : "No");
                sheet.AddFieldBlock("Medical Condition", death.MedicalCondition);
                sheet.AddFieldBlock("Hand Over To", death.HandOverTo);
                sheet.AddFieldBlock("Relative CNIC/Passport", death.RelativeCnicPassport);
                sheet.AddFieldBlock("Relation", LovText(model.RelationOptions, death.RelationCode));
                sheet.AddFieldBlock("Duty Nurse", LovText(model.EmployeeOptions, death.DutyNurseCode));
                sheet.AddFieldBlock("Duty MO", LovText(model.MoEmployeeOptions, death.DutyMoCode));
                EndSection(sheet);
            }

            var lama = outcome.Lama;
            if (lama != null && (lama.IsFinal || lama.DutyMoCode.GetValueOrDefault() > 0
                || HasText(lama.RequesterName, lama.Reason, lama.DoctorRemarks, lama.NurseComments)))
            {
                BeginSection(sheet, "OUTCOME - LAMA" + (lama.IsFinal ? "  [Finalized]" : ""));
                sheet.AddFieldBlock("Requester", lama.RequesterName);
                sheet.AddFieldBlock("Relation", LovText(model.RelationOptions, lama.RelationCode));
                sheet.AddFieldBlock("Requested On", lama.RequestedOn?.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture));
                sheet.AddFieldBlock("Reason", lama.Reason);
                sheet.AddFieldBlock("Doctor Remarks", lama.DoctorRemarks);
                sheet.AddFieldBlock("Nurse Comments", lama.NurseComments);
                sheet.AddFieldBlock("Duty Nurse", LovText(model.EmployeeOptions, lama.DutyNurseCode));
                sheet.AddFieldBlock("Duty MO", LovText(model.MoEmployeeOptions, lama.DutyMoCode));
                EndSection(sheet);
            }
        }

        private static void WritePackageTable(SheetBuilder sheet, IEnumerable<PackageOrderRowViewModel> orders)
        {
            sheet.AddTableHeader(new[] { "Item Name", "Dose", "Route", "Remark", "Status", "By", "Date", "", "", "" });
            var list = (orders ?? Enumerable.Empty<PackageOrderRowViewModel>())
                .Where(x => x != null && !x.IsDeleted).OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToList();
            if (list.Count == 0)
            {
                sheet.AddTableRow(SGreen, Pad10("No items", "", "", "", "", "", "", "", "", ""));
                return;
            }

            foreach (var o in list)
            {
                var disc = o.Discontinue;
                var style = disc ? SRed : SGreen;
                var status = disc ? "Discontinued" : o.IsAcknowledged ? "Completed/Charged" : "Pending";
                var statusStyle = disc ? SStatusBad : o.IsAcknowledged ? SStatusOk : SStatusPending;
                sheet.AddTableRowMixed(style, new[]
                {
                    (Clean(o.ItemName) ?? "", style),
                    (Clean(o.Dose) ?? "", style),
                    (Clean(o.DrugRouteName) ?? "", style),
                    (Clean(o.Remarks) ?? "", style),
                    (status, statusStyle),
                    (Clean(o.IsAcknowledged ? o.AckByName : o.DiscontinueByName) ?? "", style),
                    ((o.IsAcknowledged ? o.AckDate : o.DiscontinueDate)?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "", style),
                    ("", style), ("", style), ("", style)
                });
            }
        }

        /// <summary>
        /// Selected LOV descriptions only (never codes / full LOV).
        /// Short LOVs: selected values as pipe-separated text.
        /// Long LOVs: selected values listed only.
        /// </summary>
        private static void WriteSelectedLovItems(
            SheetBuilder sheet,
            IEnumerable<(string Name, bool Selected, string Remark)> items,
            int totalOptionCount)
        {
            var selected = items
                .Where(x => x.Selected && !string.IsNullOrWhiteSpace(x.Name))
                .Select(x =>
                {
                    var name = Clean(x.Name) ?? "";
                    var remark = Clean(x.Remark);
                    return string.IsNullOrWhiteSpace(remark) ? name : (name + " - " + remark);
                })
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            if (selected.Count == 0)
            {
                sheet.AddMerged(SMuted, "None selected", FirstCol, LastCol, 16);
                return;
            }

            if (totalOptionCount > 0 && totalOptionCount <= ShortLovMaxItems)
            {
                sheet.AddMerged(SValue, string.Join("  |  ", selected), FirstCol, LastCol, 20);
                return;
            }

            for (var i = 0; i < selected.Count; i += 2)
            {
                var left = selected[i];
                var right = i + 1 < selected.Count ? selected[i + 1] : "";
                sheet.AddTwoCols(SValue, left, SValue, right, 18);
            }
        }

        private static void AddDetailPair(SheetBuilder sheet, string l1, string v1, string l2, string v2)
        {
            sheet.AddFourCols(SLabel, l1, SValue, TextOr(v1), SLabel, l2, SValue, TextOr(v2), 22);
        }

        private static void AddFindingCell(SheetBuilder sheet, string label, string value)
        {
            sheet.AddFourCols(SLabel, label, SInput, TextOr(value), SCard, "", SCard, "", 24);
        }

        private static void AddVitalTileRow(SheetBuilder sheet,
            string l1, string v1, int s1,
            string l2, string v2, int s2,
            string l3, string v3, int s3,
            string l4, string v4, int s4)
        {
            sheet.AddCustomRow(20, new[]
            {
                (FirstCol, FirstCol + 1, s1, l1),
                (FirstCol + 2, FirstCol + 3, s2, l2),
                (FirstCol + 4, FirstCol + 5, s3, l3),
                (FirstCol + 6, FirstCol + 7, s4, l4),
                (FirstCol + 8, LastCol, SCard, "")
            });
            sheet.AddCustomRow(30, new[]
            {
                (FirstCol, FirstCol + 1, s1, string.IsNullOrWhiteSpace(v1) ? "-" : v1),
                (FirstCol + 2, FirstCol + 3, s2, string.IsNullOrWhiteSpace(v2) ? "-" : v2),
                (FirstCol + 4, FirstCol + 5, s3, string.IsNullOrWhiteSpace(v3) ? "-" : v3),
                (FirstCol + 6, FirstCol + 7, s4, string.IsNullOrWhiteSpace(v4) ? "-" : v4),
                (FirstCol + 8, LastCol, SCard, "")
            });
        }

        private static bool HasText(params string[] values) =>
            values != null && values.Any(v => !string.IsNullOrWhiteSpace(v));

        private static string TextOr(string v)
        {
            var cleaned = Clean(v);
            return string.IsNullOrWhiteSpace(cleaned) ? "-" : cleaned;
        }

        private static string Nz(object value)
        {
            if (value == null) return "";
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        }

        private static string LovText(IEnumerable<ERLovOptionViewModel> options, int? code)
        {
            if (!code.HasValue || code.Value <= 0) return "-";
            var match = (options ?? Enumerable.Empty<ERLovOptionViewModel>())
                .FirstOrDefault(x => x != null && x.Id == code.Value);
            if (match == null || string.IsNullOrWhiteSpace(match.Name))
                return "-";
            return Clean(match.Name) ?? "-";
        }

        private static string[] Pad10(params string[] values)
        {
            var arr = new string[ColCount];
            for (var i = 0; i < ColCount; i++)
                arr[i] = i < values.Length ? (Clean(values[i]) ?? "") : "";
            return arr;
        }

        private static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var t = text;

            var replacements = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "Ã¢â‚¬â€", "-" },
                { "Ã¢â‚¬â€œ", "-" },
                { "Ã¢â‚¬Ëœ", "'" },
                { "Ã¢â‚¬â„¢", "'" },
                { "Ã¢â‚¬Å“", "\"" },
                { "Ã¢â‚¬Â", "\"" },
                { "Ã‚Â·", "|" },
                { "Ã‚Â", "" },
                { "Ã¢Å“â€œ", "[OK]" },
                { "Ã¢Ëœâ€˜", "[x]" },
                { "Ã¢ËœÂ", "[ ]" },
                { "Ã¢â€â‚¬", "-" },
                { "â€”", "-" },
                { "â€“", "-" },
                { "â€˜", "'" },
                { "â€™", "'" },
                { "â€œ", "\"" },
                { "â€", "\"" },
                { "Â·", "|" },
                { "Â", "" },
                { "\u00A0", " " }
            };

            foreach (var kv in replacements)
            {
                if (t.IndexOf(kv.Key, StringComparison.Ordinal) >= 0)
                    t = t.Replace(kv.Key, kv.Value);
            }

            t = t.Replace('\u2013', '-').Replace('\u2014', '-')
                 .Replace('\u2018', '\'').Replace('\u2019', '\'')
                 .Replace('\u201C', '"').Replace('\u201D', '"')
                 .Replace('\u2022', '-').Replace('\u00B7', '|')
                 .Replace('\u2610', ' ').Replace('\u2611', ' ');

            return SanitizeXml(t.Trim());
        }

        private static string SanitizeXml(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new StringBuilder(text.Length);
            foreach (var ch in text)
            {
                if (ch == 0x9 || ch == 0xA || ch == 0xD
                    || (ch >= 0x20 && ch <= 0xD7FF)
                    || (ch >= 0xE000 && ch <= 0xFFFD))
                    sb.Append(ch);
            }
            return sb.ToString();
        }

        private sealed class SheetBuilder
        {
            public List<XElement> Rows { get; } = new List<XElement>();
            public List<string> Merges { get; } = new List<string>();
            public int Row { get; private set; } = 1;
            public int FreezeRows { get; private set; }
            private int _outlineLevel;

            public void FreezeAfterCurrentRow()
            {
                FreezeRows = Math.Max(0, Row - 1);
            }

            public void StartGroup() { _outlineLevel = 1; }
            public void EndGroup() { _outlineLevel = 0; }

            public void AddSpacer(int count)
            {
                for (var i = 0; i < count; i++)
                {
                    Rows.Add(MakeRow(Row, 12, Enumerable.Range(1, 12).Select(c => Cell(Row, c, "", SPage)), _outlineLevel, autoFit: false));
                    Row++;
                }
            }

            public void BeginCard() { }
            public void EndCard() { AddCardRow(); }

            public void AddCardRow()
            {
                Rows.Add(MakeRow(Row, 10, Enumerable.Range(1, 12).Select(c =>
                    Cell(Row, c, "", c >= FirstCol && c <= LastCol ? SCard : SPage)), _outlineLevel, autoFit: false));
                Row++;
            }

            public void AddSectionHeader(int style, string title)
            {
                // Header itself is never grouped so it remains a navigation anchor.
                var cells = new List<XElement>();
                for (var c = 1; c <= 12; c++)
                {
                    if (c < FirstCol || c > LastCol) cells.Add(Cell(Row, c, "", SPage));
                    else if (c == FirstCol) cells.Add(Cell(Row, c, title ?? "", style));
                    else cells.Add(Cell(Row, c, "", style));
                }
                Rows.Add(MakeRow(Row, 28, cells, outlineLevel: 0, autoFit: false));
                Merges.Add(ColName(FirstCol) + Row + ":" + ColName(LastCol) + Row);
                Row++;
            }

            public void AddMerged(int style, string text, int c1, int c2, int height)
            {
                var cells = new List<XElement>();
                for (var c = 1; c <= 12; c++)
                {
                    if (c < FirstCol || c > LastCol) cells.Add(Cell(Row, c, "", SPage));
                    else if (c == c1) cells.Add(Cell(Row, c, text ?? "", style));
                    else if (c >= c1 && c <= c2) cells.Add(Cell(Row, c, "", style));
                    else cells.Add(Cell(Row, c, "", SCard));
                }
                var span = Math.Max(1, c2 - c1 + 1);
                var charsPerLine = Math.Max(20, span * 9);
                var resolvedHeight = Math.Max(height, EstimateHeight(text, charsPerLine, minHeight: height, maxHeight: 220));
                Rows.Add(MakeRow(Row, resolvedHeight, cells, _outlineLevel, autoFit: true));
                if (c2 > c1) Merges.Add(ColName(c1) + Row + ":" + ColName(c2) + Row);
                Row++;
            }

            public void AddFieldBlock(string label, string value)
            {
                AddMerged(SLabel, label ?? "", FirstCol, LastCol, 18);
                var cleaned = TextOr(value);
                // Full-width value row: size by wrapped lines so long notes stay readable.
                var height = EstimateHeight(cleaned, charsPerLine: 70, minHeight: 32, maxHeight: 240, lineHeight: 15);
                AddMerged(SInput, cleaned, FirstCol, LastCol, height);
            }

            public void AddTwoColumnFields(string l1, string v1, string l2, string v2)
            {
                AddTwoCols(SLabel, l1, SLabel, l2, 18);
                var a = TextOr(v1);
                var b = TextOr(v2);
                var h = Math.Max(
                    EstimateHeight(a, charsPerLine: 35, minHeight: 40, maxHeight: 220, lineHeight: 15),
                    EstimateHeight(b, charsPerLine: 35, minHeight: 40, maxHeight: 220, lineHeight: 15));
                AddTwoCols(SInput, a, SInput, b, h);
            }

            public void AddTwoCols(int s1, string t1, int s2, string t2, int height)
            {
                var mid = FirstCol + 4;
                var cells = new List<XElement>();
                for (var c = 1; c <= 12; c++)
                {
                    if (c < FirstCol || c > LastCol) cells.Add(Cell(Row, c, "", SPage));
                    else if (c == FirstCol) cells.Add(Cell(Row, c, t1 ?? "", s1));
                    else if (c < mid) cells.Add(Cell(Row, c, "", s1));
                    else if (c == mid) cells.Add(Cell(Row, c, t2 ?? "", s2));
                    else cells.Add(Cell(Row, c, "", s2));
                }
                var resolved = Math.Max(height,
                    Math.Max(
                        EstimateHeight(t1, 35, minHeight: height, maxHeight: 220),
                        EstimateHeight(t2, 35, minHeight: height, maxHeight: 220)));
                Rows.Add(MakeRow(Row, resolved, cells, _outlineLevel, autoFit: true));
                Merges.Add(ColName(FirstCol) + Row + ":" + ColName(mid - 1) + Row);
                Merges.Add(ColName(mid) + Row + ":" + ColName(LastCol) + Row);
                Row++;
            }

            public void AddFourCols(int s1, string t1, int s2, string t2, int s3, string t3, int s4, string t4, int height)
            {
                var cells = new List<XElement>();
                for (var c = 1; c <= 12; c++)
                {
                    if (c < FirstCol || c > LastCol) { cells.Add(Cell(Row, c, "", SPage)); continue; }
                    if (c == 2) cells.Add(Cell(Row, c, t1 ?? "", s1));
                    else if (c == 3) cells.Add(Cell(Row, c, "", s1));
                    else if (c == 4) cells.Add(Cell(Row, c, t2 ?? "", s2));
                    else if (c == 5) cells.Add(Cell(Row, c, "", s2));
                    else if (c == 6) cells.Add(Cell(Row, c, t3 ?? "", s3));
                    else if (c == 7) cells.Add(Cell(Row, c, "", s3));
                    else if (c == 8) cells.Add(Cell(Row, c, t4 ?? "", s4));
                    else cells.Add(Cell(Row, c, "", s4));
                }
                var resolved = Math.Max(height, 22);
                resolved = Math.Max(resolved, EstimateHeight(t1, 18, minHeight: resolved, maxHeight: 120));
                resolved = Math.Max(resolved, EstimateHeight(t2, 18, minHeight: resolved, maxHeight: 120));
                resolved = Math.Max(resolved, EstimateHeight(t3, 18, minHeight: resolved, maxHeight: 120));
                resolved = Math.Max(resolved, EstimateHeight(t4, 18, minHeight: resolved, maxHeight: 120));
                Rows.Add(MakeRow(Row, resolved, cells, _outlineLevel, autoFit: true));
                Merges.Add("B" + Row + ":C" + Row);
                Merges.Add("D" + Row + ":E" + Row);
                Merges.Add("F" + Row + ":G" + Row);
                Merges.Add("H" + Row + ":K" + Row);
                Row++;
            }

            public void AddTableHeader(string[] headers)
            {
                var cells = new List<XElement> { Cell(Row, 1, "", SPage) };
                for (var i = 0; i < ColCount; i++)
                    cells.Add(Cell(Row, FirstCol + i, i < headers.Length ? (headers[i] ?? "") : "", STableHead));
                cells.Add(Cell(Row, 12, "", SPage));
                Rows.Add(MakeRow(Row, 22, cells, _outlineLevel, autoFit: false));
                Row++;
            }

            public void AddTableRow(int style, string[] values)
            {
                var cells = new List<XElement> { Cell(Row, 1, "", SPage) };
                for (var i = 0; i < ColCount; i++)
                    cells.Add(Cell(Row, FirstCol + i, i < values.Length ? (values[i] ?? "") : "", style));
                cells.Add(Cell(Row, 12, "", SPage));
                var tallest = 22;
                if (values != null)
                {
                    foreach (var value in values)
                        tallest = Math.Max(tallest, EstimateHeight(value, charsPerLine: 12, minHeight: 22, maxHeight: 90, lineHeight: 14));
                }
                Rows.Add(MakeRow(Row, tallest, cells, _outlineLevel, autoFit: true));
                Row++;
            }

            public void AddTableRowMixed(int defaultStyle, (string Text, int Style)[] values)
            {
                var cells = new List<XElement> { Cell(Row, 1, "", SPage) };
                for (var i = 0; i < ColCount; i++)
                {
                    if (i < values.Length)
                        cells.Add(Cell(Row, FirstCol + i, values[i].Text ?? "", values[i].Style));
                    else
                        cells.Add(Cell(Row, FirstCol + i, "", defaultStyle));
                }
                cells.Add(Cell(Row, 12, "", SPage));
                var tallest = 22;
                if (values != null)
                {
                    foreach (var value in values)
                        tallest = Math.Max(tallest, EstimateHeight(value.Text, charsPerLine: 12, minHeight: 22, maxHeight: 90, lineHeight: 14));
                }
                Rows.Add(MakeRow(Row, tallest, cells, _outlineLevel, autoFit: true));
                Row++;
            }

            public void AddCustomRow(int height, (int C1, int C2, int Style, string Text)[] spans)
            {
                var cells = new List<XElement>();
                for (var c = 1; c <= 12; c++)
                {
                    if (c < FirstCol || c > LastCol)
                    {
                        cells.Add(Cell(Row, c, "", SPage));
                        continue;
                    }

                    (int C1, int C2, int Style, string Text)? match = null;
                    foreach (var s in spans)
                    {
                        if (c >= s.C1 && c <= s.C2)
                        {
                            match = s;
                            break;
                        }
                    }

                    if (!match.HasValue)
                        cells.Add(Cell(Row, c, "", SCard));
                    else if (c == match.Value.C1)
                        cells.Add(Cell(Row, c, match.Value.Text ?? "", match.Value.Style));
                    else
                        cells.Add(Cell(Row, c, "", match.Value.Style));
                }
                var resolved = Math.Max(height, 22);
                if (spans != null)
                {
                    foreach (var s in spans)
                    {
                        var spanWidth = Math.Max(1, s.C2 - s.C1 + 1);
                        resolved = Math.Max(resolved,
                            EstimateHeight(s.Text, charsPerLine: spanWidth * 8, minHeight: resolved, maxHeight: 120));
                    }
                }
                Rows.Add(MakeRow(Row, resolved, cells, _outlineLevel, autoFit: true));
                foreach (var s in spans)
                {
                    if (s.C2 > s.C1)
                        Merges.Add(ColName(s.C1) + Row + ":" + ColName(s.C2) + Row);
                }
                Row++;
            }

            private static int EstimateHeight(
                string text,
                int charsPerLine,
                int minHeight,
                int maxHeight,
                double lineHeight = 14.5)
            {
                if (string.IsNullOrWhiteSpace(text) || text == "-")
                    return minHeight;

                charsPerLine = Math.Max(8, charsPerLine);
                var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
                var hardLines = normalized.Split('\n');
                var totalLines = 0;
                foreach (var hardLine in hardLines)
                {
                    var len = string.IsNullOrEmpty(hardLine) ? 1 : hardLine.Length;
                    totalLines += Math.Max(1, (int)Math.Ceiling(len / (double)charsPerLine));
                }

                totalLines = Math.Max(1, totalLines);
                var calculated = (int)Math.Ceiling(totalLines * lineHeight) + 8; // padding
                if (calculated < minHeight) return minHeight;
                if (calculated > maxHeight) return maxHeight;
                return calculated;
            }

            private static XElement MakeRow(int r, int height, IEnumerable<XElement> cells, int outlineLevel, bool autoFit)
            {
                var row = new XElement(Ss + "row",
                    new XAttribute("r", r),
                    new XAttribute("ht", Math.Max(12, height)),
                    new XAttribute("customHeight", 1),
                    new XAttribute("autoFit", autoFit ? 1 : 0),
                    cells);
                if (outlineLevel > 0)
                    row.Add(new XAttribute("outlineLevel", outlineLevel));
                return row;
            }

            private static XElement Cell(int r, int c, string text, int style)
            {
                return new XElement(Ss + "c",
                    new XAttribute("r", ColName(c) + r),
                    new XAttribute("s", style),
                    new XAttribute("t", "inlineStr"),
                    new XElement(Ss + "is",
                        new XElement(Ss + "t",
                            new XAttribute(XNamespace.Xml + "space", "preserve"),
                            Clean(text) ?? "")));
            }
        }

        private static string ColName(int index)
        {
            var name = "";
            while (index > 0)
            {
                var rem = (index - 1) % 26;
                name = (char)('A' + rem) + name;
                index = (index - 1) / 26;
            }
            return name;
        }

        private static byte[] Pack(SheetBuilder sheet)
        {
            var ss = XNamespace.Get("urn:schemas-microsoft-com:office:spreadsheet");
            var o = XNamespace.Get("urn:schemas-microsoft-com:office:office");
            var x = XNamespace.Get("urn:schemas-microsoft-com:office:excel");
            var html = XNamespace.Get("http://www.w3.org/TR/REC-html40");

            var table = BuildTable(ss, sheet);
            var options = new XElement(x + "WorksheetOptions",
                new XElement(x + "PageSetup",
                    new XElement(x + "Layout", new XAttribute(x + "Orientation", "Portrait")),
                    new XElement(x + "Header", new XAttribute(x + "Margin", "0.2")),
                    new XElement(x + "Footer", new XAttribute(x + "Margin", "0.2")),
                    new XElement(x + "PageMargins",
                        new XAttribute(x + "Bottom", "0.35"),
                        new XAttribute(x + "Left", "0.25"),
                        new XAttribute(x + "Right", "0.25"),
                        new XAttribute(x + "Top", "0.35"))),
                new XElement(x + "FitToPage"),
                new XElement(x + "Print",
                    new XElement(x + "FitWidth", 1),
                    new XElement(x + "FitHeight", 0)),
                new XElement(x + "Selected"),
                new XElement(x + "ProtectObjects", "False"),
                new XElement(x + "ProtectScenarios", "False"));

            if (sheet.FreezeRows > 0)
            {
                options.Add(new XElement(x + "FreezePanes"));
                options.Add(new XElement(x + "FrozenNoSplit"));
                options.Add(new XElement(x + "SplitHorizontal", sheet.FreezeRows));
                options.Add(new XElement(x + "TopRowBottomPane", sheet.FreezeRows));
                options.Add(new XElement(x + "ActivePane", 2));
            }

            var workbook = new XElement(ss + "Workbook",
                new XAttribute(XNamespace.Xmlns + "o", o.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "x", x.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "ss", ss.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "html", html.NamespaceName),
                new XElement(o + "DocumentProperties",
                    new XElement(o + "Title", "ER Form"),
                    new XElement(o + "Author", "ER Paperless")),
                new XElement(x + "ExcelWorkbook",
                    new XElement(x + "WindowHeight", 12000),
                    new XElement(x + "WindowWidth", 16000),
                    new XElement(x + "ProtectStructure", "False"),
                    new XElement(x + "ProtectWindows", "False")),
                BuildSpreadsheetMlStyles(ss),
                new XElement(ss + "Worksheet",
                    new XAttribute(ss + "Name", "ER Form"),
                    table,
                    options));

            var xml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>"
                      + "<?mso-application progid=\"Excel.Sheet\"?>"
                      + workbook.ToString(SaveOptions.DisableFormatting);

            var utf8 = new UTF8Encoding(true);
            return utf8.GetPreamble().Concat(utf8.GetBytes(xml)).ToArray();
        }

        private static XElement BuildTable(XNamespace ss, SheetBuilder sheet)
        {
            var mergeStarts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var merge in sheet.Merges)
            {
                var parts = (merge ?? "").Split(':');
                if (parts.Length != 2) continue;
                var start = parts[0].Trim();
                var end = parts[1].Trim();
                int startCol, startRow, endCol, endRow;
                if (!TryParseCellRef(start, out startCol, out startRow)) continue;
                if (!TryParseCellRef(end, out endCol, out endRow)) continue;
                if (startRow != endRow || endCol <= startCol) continue;
                mergeStarts[start.ToUpperInvariant()] = endCol - startCol;
            }

            var covered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in mergeStarts)
            {
                int c0, r0;
                if (!TryParseCellRef(kv.Key, out c0, out r0)) continue;
                for (var c = c0 + 1; c <= c0 + kv.Value; c++)
                    covered.Add(ColName(c) + r0);
            }

            var table = new XElement(ss + "Table",
                new XAttribute(ss + "ExpandedColumnCount", 12),
                new XAttribute(ss + "ExpandedRowCount", Math.Max(1, sheet.Row - 1)),
                new XAttribute(ss + "DefaultRowHeight", 18),
                new XElement(ss + "Column", new XAttribute(ss + "Index", 1), new XAttribute(ss + "Width", 12)),
                new XElement(ss + "Column", new XAttribute(ss + "Width", 58), new XAttribute(ss + "Span", 9)),
                new XElement(ss + "Column", new XAttribute(ss + "Index", 12), new XAttribute(ss + "Width", 12)));

            foreach (var rowEl in sheet.Rows)
            {
                var rowNumAttr = rowEl.Attribute("r")?.Value;
                int rowNum;
                if (!int.TryParse(rowNumAttr, out rowNum))
                    continue;

                double height = 15;
                double.TryParse(rowEl.Attribute("ht")?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out height);

                var row = new XElement(ss + "Row",
                    new XAttribute(ss + "Index", rowNum),
                    new XAttribute(ss + "Height", Math.Max(12, height)));

                var autoFitAttr = rowEl.Attribute("autoFit");
                var autoFit = autoFitAttr != null && autoFitAttr.Value == "1";
                row.Add(new XAttribute(ss + "AutoFitHeight", autoFit ? 1 : 0));

                var outlineAttr = rowEl.Attribute("outlineLevel");
                if (outlineAttr != null)
                    row.Add(new XAttribute(ss + "OutlineLevel", outlineAttr.Value));

                foreach (var cellEl in rowEl.Elements(Ss + "c"))
                {
                    var refAttr = cellEl.Attribute("r")?.Value ?? "";
                    int col, r;
                    if (!TryParseCellRef(refAttr, out col, out r))
                        continue;
                    if (covered.Contains(refAttr))
                        continue;

                    var text = cellEl.Element(Ss + "is")?.Element(Ss + "t")?.Value ?? "";
                    var styleId = "s" + (cellEl.Attribute("s")?.Value ?? "0");

                    var cell = new XElement(ss + "Cell",
                        new XAttribute(ss + "Index", col),
                        new XAttribute(ss + "StyleID", styleId));

                    int mergeAcross;
                    if (mergeStarts.TryGetValue(refAttr.ToUpperInvariant(), out mergeAcross) && mergeAcross > 0)
                        cell.Add(new XAttribute(ss + "MergeAcross", mergeAcross));

                    cell.Add(new XElement(ss + "Data",
                        new XAttribute(ss + "Type", "String"),
                        Clean(text) ?? ""));
                    row.Add(cell);
                }

                table.Add(row);
            }

            return table;
        }

        private static bool TryParseCellRef(string cellRef, out int col, out int row)
        {
            col = 0;
            row = 0;
            if (string.IsNullOrWhiteSpace(cellRef)) return false;

            var i = 0;
            while (i < cellRef.Length && char.IsLetter(cellRef[i]))
            {
                col = col * 26 + (char.ToUpperInvariant(cellRef[i]) - 'A' + 1);
                i++;
            }

            if (col <= 0 || i >= cellRef.Length) return false;
            return int.TryParse(cellRef.Substring(i), NumberStyles.Integer, CultureInfo.InvariantCulture, out row) && row > 0;
        }

        private static XElement BuildSpreadsheetMlStyles(XNamespace ss)
        {
            XElement Style(string id, string fontColor, double size, bool bold,
                string fill, string hAlign, string vAlign = "Center")
            {
                var style = new XElement(ss + "Style", new XAttribute(ss + "ID", id));
                var align = new XElement(ss + "Alignment",
                    new XAttribute(ss + "Vertical", vAlign),
                    new XAttribute(ss + "WrapText", 1));
                if (!string.IsNullOrEmpty(hAlign))
                    align.Add(new XAttribute(ss + "Horizontal", hAlign));
                style.Add(align);

                var font = new XElement(ss + "Font",
                    new XAttribute(ss + "FontName", "Calibri"),
                    new XAttribute(ss + "Size", size),
                    new XAttribute(ss + "Color", fontColor));
                if (bold) font.Add(new XAttribute(ss + "Bold", 1));
                style.Add(font);

                if (!string.IsNullOrEmpty(fill))
                {
                    style.Add(new XElement(ss + "Interior",
                        new XAttribute(ss + "Color", fill),
                        new XAttribute(ss + "Pattern", "Solid")));
                }

                style.Add(new XElement(ss + "Borders",
                    Border(ss, "Bottom"), Border(ss, "Left"),
                    Border(ss, "Right"), Border(ss, "Top")));
                return style;
            }

            return new XElement(ss + "Styles",
                new XElement(ss + "Style", new XAttribute(ss + "ID", "Default"),
                    new XAttribute(ss + "Name", "Normal"),
                    new XElement(ss + "Alignment", new XAttribute(ss + "Vertical", "Bottom")),
                    new XElement(ss + "Font", new XAttribute(ss + "FontName", "Calibri"), new XAttribute(ss + "Size", 10))),
                Style("s0", "#1F2A37", 10, false, "#F4F6FB", null, "Top"),
                Style("s1", "#1F2A37", 10, false, "#FFFFFF", null),
                Style("s2", "#6B7280", 9, true, "#FFFFFF", null),
                Style("s3", "#1F2A37", 10, false, "#FFFFFF", null),
                Style("s4", "#1F2A37", 12, true, "#FFFFFF", null),
                Style("s5", "#FFFFFF", 11, true, "#F97316", "Left"),
                Style("s6", "#1F2A37", 10, false, "#DCFCE7", null),
                Style("s7", "#1F2A37", 10, false, "#FEE2E2", null),
                Style("s8", "#6B7280", 9, true, "#F3F4F6", "Center"),
                Style("s9", "#1F2A37", 14, true, "#FFFFFF", "Left"),
                Style("s10", "#6B7280", 9, false, "#FFFFFF", null),
                Style("s11", "#1F2A37", 10, false, "#F3F4F6", null, "Top"),
                Style("s12", "#1F2A37", 12, true, "#EFF6FF", "Center"),
                Style("s13", "#1F2A37", 12, true, "#FFEDD5", "Center"),
                Style("s14", "#1F2A37", 12, true, "#CCFBF1", "Center"),
                Style("s15", "#1F2A37", 12, true, "#FCE7F3", "Center"),
                Style("s16", "#1F2A37", 12, true, "#CFFAFE", "Center"),
                Style("s17", "#1F2A37", 12, true, "#D1FAE5", "Center"),
                Style("s18", "#1F2A37", 12, true, "#EDE9FE", "Center"),
                Style("s19", "#1F2A37", 12, true, "#FFEDD5", "Center"),
                Style("s20", "#1F2A37", 10, false, "#FFFFFF", "Center"),
                Style("s21", "#FFFFFF", 11, true, "#F97316", "Center"),
                Style("s22", "#166534", 9, true, "#DCFCE7", "Center"),
                Style("s23", "#991B1B", 9, true, "#FEE2E2", "Center"),
                Style("s24", "#9A3412", 9, true, "#F3F4F6", "Center"));
        }

        private static XElement Border(XNamespace ss, string position)
        {
            return new XElement(ss + "Border",
                new XAttribute(ss + "Position", position),
                new XAttribute(ss + "LineStyle", "Continuous"),
                new XAttribute(ss + "Weight", 1),
                new XAttribute(ss + "Color", "#E5E7EB"));
        }
    }
}
