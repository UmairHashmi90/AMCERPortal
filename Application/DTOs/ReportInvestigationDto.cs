using System;

namespace ERPaperless.Application.DTOs
{
    public class ReportInvestigationDto
    {
        public int ReportTypeCode { get; set; }
        public long PatientOrderDetailCode { get; set; }
        public DateTime? OrderDate { get; set; }
        public string Test { get; set; }
        public string Status { get; set; }
    }
}
