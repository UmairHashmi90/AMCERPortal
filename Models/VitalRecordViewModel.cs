using System;

namespace ERPaperless.Models
{
    public class VitalRecordViewModel
    {
        public long Id { get; set; }
        public DateTime RecordedOn { get; set; }
        public int RecordStatusCode { get; set; }
        public bool IsDeleted => RecordStatusCode == 8;
        public int? HeartRate { get; set; }
        public int? Pulse { get; set; }
        public int? BP1 { get; set; }
        public int? BP2 { get; set; }
        public int? RespiratoryRate { get; set; }
        public int? Height { get; set; }
        public decimal? Weight { get; set; }
        public int? MetricBMI { get; set; }
        public int? Spo2 { get; set; }
        public decimal? GlucoseF { get; set; }
        public decimal? GlucoseR { get; set; }
        public decimal? Temperature { get; set; }
        public int? FallRisk { get; set; }
        public int? PainScore { get; set; }
        public string BloodPressure { get; set; }
        public int Bsr { get; set; }
    }
}
