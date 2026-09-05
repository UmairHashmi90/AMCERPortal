using System.ComponentModel.DataAnnotations;

namespace ERPaperless.Models
{
    public class AddVitalInputViewModel
    {
        [Required]
        public string PatientId { get; set; }

        [Range(0, 240)]
        public int? HeartRate { get; set; }

        [Range(0, 240)]
        public int? Pulse { get; set; }

        [Range(0, 300)]
        public int? BP1 { get; set; }

        [Range(0, 300)]
        public int? BP2 { get; set; }

        [Range(5, 80)]
        public int? RespiratoryRate { get; set; }

        [Range(0, 300)]
        public int? Height { get; set; }

        [Range(0, 500)]
        public decimal? Weight { get; set; }

        [Range(0, 200)]
        public int? MetricBMI { get; set; }

        [Range(0, 100)]
        public int? Spo2 { get; set; }

        [StringLength(50)]
        public string Spo2Remark { get; set; }

        [Range(0, 800)]
        public decimal? GlucoseF { get; set; }

        [Range(0, 800)]
        public decimal? GlucoseR { get; set; }

        [Range(0, 110)]
        public decimal? Temperature { get; set; }

        [Range(0, 10)]
        public int? FallRisk { get; set; }

        [Range(0, 10)]
        public int? PainScore { get; set; }

        /// <summary>Selected Consciousness LOV key (tblERPatientVitals.intConciousness).</summary>
        public int? ConsciousnessCode { get; set; }
    }
}
