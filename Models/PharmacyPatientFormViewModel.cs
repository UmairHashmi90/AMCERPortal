using System.Collections.Generic;
using System.Linq;

namespace ERPaperless.Models
{
    public class PharmacyPatientFormViewModel
    {
        public PatientFormContextViewModel Patient { get; set; }

        public bool ShowAllPatients => Patient == null;

        public PharmacyPackageOrdersGridViewModel MedicineGrid { get; set; }
            = new PharmacyPackageOrdersGridViewModel();

        public PharmacyPackageOrdersGridViewModel SurgicalGrid { get; set; }
            = new PharmacyPackageOrdersGridViewModel();

        public static PharmacyPatientFormViewModel CreateAll(List<PackageOrderRowViewModel> orders)
        {
            var all = orders ?? new List<PackageOrderRowViewModel>();

            return new PharmacyPatientFormViewModel
            {
                Patient = null,
                MedicineGrid = new PharmacyPackageOrdersGridViewModel
                {
                    Title = "Medicine Orders",
                    Description = "All pending medicine requests. Tick Ack when dispensed.",
                    TableId = "pharmacyMedicineTable",
                    BodyId = "pharmacyMedicineBody",
                    ShowPatientColumns = true,
                    Orders = all.Where(x => x.PackageTypeCode == 1).ToList()
                },
                SurgicalGrid = new PharmacyPackageOrdersGridViewModel
                {
                    Title = "Surgical Orders",
                    Description = "All pending surgical requests. Tick Ack when issued.",
                    TableId = "pharmacySurgicalTable",
                    BodyId = "pharmacySurgicalBody",
                    DoseColumnLabel = "QTY",
                    ShowPatientColumns = true,
                    Orders = all.Where(x => x.PackageTypeCode == 2).ToList()
                }
            };
        }

        public static PharmacyPatientFormViewModel Create(
            PatientFormContextViewModel patient,
            List<PackageOrderRowViewModel> orders)
        {
            var all = orders ?? new List<PackageOrderRowViewModel>();

            return new PharmacyPatientFormViewModel
            {
                Patient = patient,
                MedicineGrid = new PharmacyPackageOrdersGridViewModel
                {
                    Title = "Medicine Orders",
                    Description = "Medicine items ordered by MO. Tick Ack when dispensed.",
                    TableId = "pharmacyMedicineTable",
                    BodyId = "pharmacyMedicineBody",
                    ShowPatientColumns = false,
                    Orders = all.Where(x => x.PackageTypeCode == 1).ToList()
                },
                SurgicalGrid = new PharmacyPackageOrdersGridViewModel
                {
                    Title = "Surgical Orders",
                    Description = "Surgical items ordered by MO. Tick Ack when issued.",
                    TableId = "pharmacySurgicalTable",
                    BodyId = "pharmacySurgicalBody",
                    DoseColumnLabel = "QTY",
                    ShowPatientColumns = false,
                    Orders = all.Where(x => x.PackageTypeCode == 2).ToList()
                }
            };
        }
    }
}
