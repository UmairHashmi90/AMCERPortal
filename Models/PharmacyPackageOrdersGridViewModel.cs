using System.Collections.Generic;

namespace ERPaperless.Models
{
    public class PharmacyPackageOrdersGridViewModel
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string TableId { get; set; }
        public string BodyId { get; set; }
        public string DoseColumnLabel { get; set; } = "Dose";
        public bool ShowPatientColumns { get; set; }
        public List<PackageOrderRowViewModel> Orders { get; set; } = new List<PackageOrderRowViewModel>();
    }
}
