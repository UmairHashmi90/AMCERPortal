using ERPaperless.Models;

namespace ERPaperless.Abstractions.Application
{
    public interface IPatientWorkspaceService
    {
        PatientFormContextViewModel BuildByPatientId(string patientId, int companyCode, int userCode);
        PatientFormContextViewModel BuildByAdmissionCode(int admissionCode, int companyCode, int userCode);
        string LastError { get; }
    }
}
