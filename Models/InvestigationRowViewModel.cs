using System;

namespace ERPaperless.Models
{
    public class InvestigationRowViewModel
    {
        public long Id { get; set; }
        public int? ServiceGroupCode { get; set; }
        public string TestName { get; set; }
        public string Remarks { get; set; }
        public bool IsAcknowledged { get; set; }
        public int? AckByCode { get; set; }
        public string AckByName { get; set; }
        public DateTime? AckDate { get; set; }
        public bool IsCancelled { get; set; }
        public int? CancelledByCode { get; set; }
        public string CancelledByName { get; set; }
        public DateTime? CancelledDate { get; set; }
        public int SortOrder { get; set; }
        public DateTime RequestedOn { get; set; }
        public string RequestedByName { get; set; }
        public int CreatedByCode { get; set; }

        public long PatientCode { get; set; }
        public string PatientId => PatientCode > 0 ? PatientCode.ToString() : string.Empty;
        public string PatientName { get; set; }
        public string BedNo { get; set; }
        public string MrNo { get; set; }

        public int RecordStatusCode { get; set; } = 1;
        public bool IsDeleted => RecordStatusCode == 8;

        /// <summary>Billing has acknowledged (bolIsAcknowledged).</summary>
        public bool IsBillingAcknowledged => IsAcknowledged;

        /// <summary>Pending | completed | cancelled — for work-queue filter chips.</summary>
        public string WorkQueueFilterStatus =>
            IsDeleted || IsCancelled ? "cancelled"
            : IsBillingAcknowledged ? "completed"
            : "pending";

        public bool IsLockedForClinicalEdit => IsAcknowledged;
    }
}
