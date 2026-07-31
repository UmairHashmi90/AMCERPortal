using System;

namespace ERPaperless.Models
{
    public class PrescriptionVisitRowViewModel
    {
        public long Code { get; set; }
        public DateTime? VisitDate { get; set; }
        public string Consultant { get; set; }
        public string Speciality { get; set; }
    }
}
