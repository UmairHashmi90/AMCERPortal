using System;

namespace ERPaperless.Models
{
    public class PackageOrderRowViewModel
    {
        public long Id { get; set; }
        public int PackageTypeCode { get; set; }
        public int PackageCode { get; set; }
        public int PackageDetailCode { get; set; }
        public string ItemName { get; set; }
        public string Dose { get; set; }
        public int? DrugRouteCode { get; set; }
        public string DrugRouteName { get; set; }
        public bool Discontinue { get; set; }
        public string Remarks { get; set; }
        public int? DiscontinueByCode { get; set; }
        public string DiscontinueByName { get; set; }
        public DateTime? DiscontinueDate { get; set; }
        public bool IsAcknowledged { get; set; }
        public int? AckByCode { get; set; }
        public string AckByName { get; set; }
        public DateTime? AckDate { get; set; }
        public int SortOrder { get; set; }
        public int CreatedByCode { get; set; }
        public DateTime RequestedOn { get; set; }
        public string RequestedByName { get; set; }

        public long PatientCode { get; set; }
        public string PatientId => PatientCode > 0 ? PatientCode.ToString() : string.Empty;
        public string PatientName { get; set; }
        public string BedNo { get; set; }
        public string MrNo { get; set; }

        public int RecordStatusCode { get; set; } = 1;
        public bool IsDeleted => RecordStatusCode == 8;

        /// <summary>Pharmacy has charged/issued this line (bolIsAcknowledged).</summary>
        public bool IsPharmacyCharged => IsAcknowledged;

        /// <summary>Pending | completed | cancelled — for work-queue filter chips.</summary>
        public string WorkQueueFilterStatus =>
            IsDeleted || Discontinue ? "cancelled"
            : IsPharmacyCharged ? "completed"
            : "pending";

        public bool IsLockedForClinicalEdit => IsAcknowledged;
    }
}
