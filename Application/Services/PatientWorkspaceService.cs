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
                PatientName = !string.IsNullOrWhiteSpace(erPatient.strName)
                    ? erPatient.strName
                    : admission?.PatientName ?? $"BED#{erPatient.intWardBedCode}",
                MrNo = admission?.MrNo ?? "PENDING",
                AdmissionNo = admission?.AdmissionNo ?? "-",
                BedNo = BedRepository.ResolveBedDisplayName(
                    admission,
                    erPatient.intWardBedCode,
                    erPatient.intBranchCode,
                    companyCode,
                    userCode),
                AgeGender = admission?.AgeGender ?? "-",
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
                PatientName = admission.PatientName,
                MrNo = admission.MrNo,
                AdmissionNo = admission.AdmissionNo,
                BedNo = admission.SlotName,
                AgeGender = admission.AgeGender,
                AdmissionDate = admission.AdmissionDate ?? erPatient?.dtmAdmission ?? System.DateTime.Now,
                AdmissionCode = admissionCode
            };
        }
    }
}
