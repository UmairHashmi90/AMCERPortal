using ERPaperless.Application.Common;
using ERPaperless.Application.DTOs;
using ERPaperless.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ERPaperless.Abstractions.Application
{
    public interface IReportsPortalService
    {
        List<ReportInvestigationDto> GetInvestigationsForAdmission(int admissionCode, int companyCode);
        List<PrescriptionVisitDto> GetPrescriptionsForAdmission(int admissionCode, int companyCode);
        string EncryptCode(long code);
        string DecryptCode(string encrypted);
        Task<OperationResult<byte[]>> BuildReportPdfForCodeAsync(long patientOrderDetailCode);
        Task<OperationResult<byte[]>> BuildPrescriptionPdfForCodeAsync(long encounterCode);
    }
}
