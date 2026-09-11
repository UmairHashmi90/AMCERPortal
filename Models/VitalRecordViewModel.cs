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
        public string Spo2Remark { get; set; }
        /// <summary>Selected SPO2 LOV key (ERLovType.SPo2). Saved as description text in strSPo2.</summary>
        public int? Spo2Code { get; set; }
        public decimal? GlucoseF { get; set; }
        public decimal? GlucoseR { get; set; }
        public decimal? Temperature { get; set; }
        public decimal? TemperatureC { get; set; }
        public int? FallRisk { get; set; }
        public int? PainScore { get; set; }
        /// <summary>LOV key stored in tblERPatientVitals.intConciousness.</summary>
        public int? ConsciousnessCode { get; set; }
        /// <summary>Resolved Consciousness LOV description for display.</summary>
        public string ConsciousnessText { get; set; }
        public string BloodPressure { get; set; }
        public int Bsr { get; set; }

        // NEWS2 fields from procGrdERPatientVitalsForERPortal
        public int? NewsRespiratoryScore { get; set; }
        public int? NewsSystolicScore { get; set; }
        public int? NewsHeartRateScore { get; set; }
        public int? NewsTemperatureScore { get; set; }
        public int? News2AvailableScore { get; set; }
        public int? News2Score { get; set; }
        public string News2DataStatus { get; set; }
        public string News2Risk { get; set; }
        public string News2RedFlag { get; set; }
        public string News2FocusParameter { get; set; }
        public int? PreviousNews2AvailableScore { get; set; }
        public int? News2AvailableChange { get; set; }
        public string News2Trend { get; set; }
        public string News2Trigger { get; set; }
        public string SuggestedMonitoring { get; set; }
        public string SuggestedCareArea { get; set; }
    }
}
