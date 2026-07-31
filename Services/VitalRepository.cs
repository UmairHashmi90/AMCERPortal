using ERPaperless.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ERPaperless.Services
{
    public static class VitalRepository
    {
        public static string LastError { get; private set; }

        public static List<VitalRecordViewModel> GetVitals(string patientId, int companyCode)
        {
            var target = ResolveVitalTarget(patientId, companyCode);
            if (!target.IsValid) return new List<VitalRecordViewModel>();

            try
            {
                using (var db = dbAMCEntities.Create())
                {
                    var query = db.tblERPatientVitals
                        .Where(v => v.intCompanyCode == target.CompanyCode
                                    && v.intERPatientCode == target.ERPatientCode
                                    && (v.intRecordStatusCode == 1 || v.intRecordStatusCode == 8));

                    return query
                        .OrderByDescending(v => v.dtmVitals)
                        .ToList()
                        .Select(MapVital)
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(VitalRepository), nameof(GetVitals), ex);
                return new List<VitalRecordViewModel>();
            }
        }

        public static bool AddVital(
            string patientId,
            int companyCode,
            int userCode,
            VitalRecordViewModel record)
        {
            LastError = null;
            var target = ResolveVitalTarget(patientId, companyCode);
            if (!target.IsValid || record == null)
            {
                LastError = "Could not resolve bed/admission for this patient.";
                return false;
            }

            try
            {
                using (var db = dbAMCEntities.Create())
                {
                    var now = DateTime.Now;
                    db.tblERPatientVitals.Add(new tblERPatientVital
                    {
                        intERPatientCode   = target.ERPatientCode,
                        dtmVitals          = now,
                        intHeartRate       = record.HeartRate,
                        intPulse           = record.Pulse,
                        intBP1             = record.BP1,
                        intBP2             = record.BP2,
                        intResp            = record.RespiratoryRate,
                        numTemp            = record.Temperature,
                        intHeight          = record.Height,
                        numWeight          = record.Weight,
                        intMetricBMI       = record.MetricBMI,
                        intSPO2            = record.Spo2,
                        numGlucoseF        = record.GlucoseF,
                        numGlucoseR        = record.GlucoseR,
                        intFallRisk        = record.FallRisk,
                        intPainScore       = record.PainScore,
                        dtmCreated         = now,
                        intOwnerCode       = userCode,
                        intCreatedByCode   = userCode,
                        intRecordStatusCode = 1,
                        intBranchCode      = target.BranchCode,
                        intCompanyCode     = target.CompanyCode
                    });

                    return db.SaveChanges() > 0;
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(VitalRepository), nameof(AddVital), ex);
                LastError = ex.GetBaseException().Message;
                return false;
            }
        }

        public static bool DeleteVital(
            long vitalId,
            int companyCode,
            int userCode)
        {
            LastError = null;
            if (vitalId <= 0) return false;

            try
            {
                using (var db = dbAMCEntities.Create())
                {
                    var vital = db.tblERPatientVitals.FirstOrDefault(v =>
                        v.intERPatientVitalsCode == vitalId
                        && v.intCompanyCode == companyCode
                        && v.intRecordStatusCode == 1);

                    if (vital == null) return false;

                    vital.intRecordStatusCode = 8;
                    vital.dtmLastM = DateTime.Now;
                    vital.intAlteredByCode = userCode;

                    return db.SaveChanges() > 0;
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(VitalRepository), nameof(DeleteVital), ex);
                LastError = ex.GetBaseException().Message;
                return false;
            }
        }

        public static bool AttachPatientToAdmission(
            long erPatientCode,
            int companyCode,
            long admissionCode,
            int userCode)
        {
            return ERPatientRepository.AttachToAdmission(
                erPatientCode,
                admissionCode,
                companyCode,
                userCode);
        }

        private static VitalTarget ResolveVitalTarget(string patientId, int companyCode)
        {
            if (string.IsNullOrWhiteSpace(patientId) || companyCode <= 0)
                return VitalTarget.Invalid;

            if (!long.TryParse(patientId, out var erPatientCode) || erPatientCode <= 0)
                return VitalTarget.Invalid;

            try
            {
                var patient = ERPatientRepository.GetByCode(erPatientCode, companyCode);
                if (patient == null) return VitalTarget.Invalid;

                return new VitalTarget
                {
                    ERPatientCode = patient.intERPatientCode,
                    BranchCode    = patient.intBranchCode,
                    CompanyCode   = companyCode
                };
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(nameof(VitalRepository), nameof(ResolveVitalTarget), ex);
                return VitalTarget.Invalid;
            }
        }

        private static VitalRecordViewModel MapVital(tblERPatientVital vital)
        {
            var bp1 = vital.intBP1 ?? 0;
            var bp2 = vital.intBP2 ?? 0;
            var bsr = vital.numGlucoseR.HasValue
                ? Convert.ToInt32(vital.numGlucoseR.Value)
                : vital.numGlucoseF.HasValue
                    ? Convert.ToInt32(vital.numGlucoseF.Value)
                    : 0;

            return new VitalRecordViewModel
            {
                Id              = vital.intERPatientVitalsCode,
                RecordedOn      = vital.dtmVitals,
                RecordStatusCode = vital.intRecordStatusCode,
                HeartRate       = vital.intHeartRate,
                Pulse           = vital.intPulse,
                BP1             = vital.intBP1,
                BP2             = vital.intBP2,
                Bsr             = bsr,
                RespiratoryRate = vital.intResp,
                BloodPressure   = bp1 > 0 || bp2 > 0 ? $"{bp1}/{bp2}" : "0/0",
                Height          = vital.intHeight,
                Weight          = vital.numWeight,
                MetricBMI       = vital.intMetricBMI,
                Spo2            = vital.intSPO2,
                GlucoseF        = vital.numGlucoseF,
                GlucoseR        = vital.numGlucoseR,
                Temperature     = vital.numTemp,
                FallRisk        = vital.intFallRisk,
                PainScore       = vital.intPainScore
            };
        }

        private struct VitalTarget
        {
            public static VitalTarget Invalid => new VitalTarget();

            public long ERPatientCode { get; set; }
            public int BranchCode { get; set; }
            public int CompanyCode { get; set; }

            public bool IsValid =>
                ERPatientCode > 0 &&
                BranchCode > 0 &&
                CompanyCode > 0;
        }
    }
}
