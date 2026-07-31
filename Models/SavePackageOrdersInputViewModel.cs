using System.Collections.Generic;

namespace ERPaperless.Models
{
    public class SavePackageOrdersInputViewModel
    {
        public string PatientId { get; set; }
        public int PackageTypeCode { get; set; }
        public int? PackageCode { get; set; }
        public List<PackageOrderSaveItemViewModel> Items { get; set; }
    }

    public class PackageOrderSaveItemViewModel
    {
        public long Id { get; set; }
        public int PackageCode { get; set; }
        public int PackageDetailCode { get; set; }
        public string ItemName { get; set; }
        public string Dose { get; set; }
        public int? DrugRouteCode { get; set; }
        public bool Discontinue { get; set; }
        public string Remarks { get; set; }
    }
}
