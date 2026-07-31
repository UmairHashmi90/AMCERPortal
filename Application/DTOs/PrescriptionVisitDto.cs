using System;

namespace ERPaperless.Application.DTOs
{
    public class PrescriptionVisitDto
    {
        public long Code { get; set; }
        public DateTime? VisitDate { get; set; }
        public string Consultant { get; set; }
        public string Speciality { get; set; }
    }
}
