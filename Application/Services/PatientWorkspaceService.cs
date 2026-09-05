using ERPaperless.Abstractions.Application;
using ERPaperless.Models;
using ERPaperless.Services;

namespace ERPaperless.Application.Services
{
    public class PatientWorkspaceService : IPatientWorkspaceService
    {
        public string LastError { get; private set; }

        public PatientFormContextViewModel BuildByPatientId(string patientId, int companyCode, int userCode)
        {
            LastError = null;
            if (string.IsNullOrWhiteSpace(patientId)) return null;

            var erPatient = ERPatientRepository.ResolveActivePatient(patientId, companyCode, userCode);
            if (erPatient == null)
            {
                LastError = ERPatientRepository.LastError;
                return null;
            }

            var admission = erPatient.intERAdmissionCode.HasValue
                ? BedRepository.GetPatientByAdmissionCode((int)erPatient.intERAdmissionCode.Value, companyCode)
                : null;

            return new PatientFormContextViewModel
            {
                PatientId = erPatient.intERPatientCode.ToString(),
                PatientName = ResolvePatientDisplayName(
                    erPatient.strName,
                    admission?.PatientName,
                    erPatient.intWardBedCode),
                MrNo = string.IsNullOrWhiteSpace(admission?.MrNo) ? "PENDING" : admission.MrNo,
                AdmissionNo = string.IsNullOrWhiteSpace(admission?.AdmissionNo) ? "-" : admission.AdmissionNo,
                BedNo = BedRepository.ResolveBedDisplayName(
                    admission,
                    erPatient.intWardBedCode,
                    erPatient.intBranchCode,
                    companyCode,
                    userCode),
                AgeGender = string.IsNullOrWhiteSpace(admission?.AgeGender) ? "-" : admission.AgeGender,
                AdmissionDate = erPatient.dtmAdmission,
                AdmissionCode = erPatient.intERAdmissionCode.HasValue
                    ? (int?)erPatient.intERAdmissionCode.Value
                    : null
            };
        }

        public PatientFormContextViewModel BuildByAdmissionCode(int admissionCode, int companyCode, int userCode)
        {
            LastError = null;
            if (admissionCode <= 0) return null;

            var admission = BedRepository.GetPatientByAdmissionCode(admissionCode, companyCode);
            if (admission == null) return null;

            var erPatient = ERPatientRepository.GetOrCreateAdmissionPatient(admissionCode, companyCode, userCode);
            return new PatientFormContextViewModel
            {
                PatientId = erPatient?.intERPatientCode.ToString() ?? admissionCode.ToString(),
                PatientName = ResolvePatientDisplayName(
                    admission.PatientName,
                    erPatient?.strName,
                    admission.BedId),
                MrNo = string.IsNullOrWhiteSpace(admission.MrNo) ? "PENDING" : admission.MrNo,
                AdmissionNo = string.IsNullOrWhiteSpace(admission.AdmissionNo) ? "-" : admission.AdmissionNo,
                BedNo = string.IsNullOrWhiteSpace(admission.SlotName) ? "-" : admission.SlotName,
                AgeGender = string.IsNullOrWhiteSpace(admission.AgeGender) ? "-" : admission.AgeGender,
                AdmissionDate = admission.AdmissionDate ?? erPatient?.dtmAdmission ?? System.DateTime.Now,
                AdmissionCode = admissionCode
            };
        }

        private static string ResolvePatientDisplayName(string primary, string secondary, int bedCode)
        {
            if (!string.IsNullOrWhiteSpace(primary) && primary != "-")
                return primary.Trim();
            if (!string.IsNullOrWhiteSpace(secondary) && secondary != "-")
                return secondary.Trim();
            return bedCode > 0 ? $"BED#{bedCode}" : "-";
        }
    }
}
