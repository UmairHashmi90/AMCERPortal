using ERPaperless.Abstractions.Application;
using ERPaperless.Application.Common;
using ERPaperless.Application.DTOs;
using ERPaperless.Models;
using ERPaperless.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ERPaperless.Application.Services
{
    public class ReportsPortalService : IReportsPortalService
    {
        public List<ReportInvestigationDto> GetInvestigationsForAdmission(int admissionCode, int companyCode)
        {
            return ReportsRepository.GetPatientInvestigationsForPortal(admissionCode, companyCode)
                .Select(x => new ReportInvestigationDto
                {
                    ReportTypeCode = x.ReportTypeCode,
                    PatientOrderDetailCode = x.PatientOrderDetailCode,
                    OrderDate = x.OrderDate,
                    Test = x.Test,
                    Status = x.Status
                })
                .ToList();
        }

        public List<PrescriptionVisitDto> GetPrescriptionsForAdmission(int admissionCode, int companyCode)
        {
            return ReportsRepository.GetPatientPrescriptionsForPortal(admissionCode, companyCode)
                .Select(x => new PrescriptionVisitDto
                {
                    Code = x.Code,
                    VisitDate = x.VisitDate,
                    Consultant = x.Consultant,
                    Speciality = x.Speciality
                })
                .ToList();
        }

        public string EncryptCode(long code)
        {
            return Data.Encrypt(code.ToString());
        }

        public string DecryptCode(string encrypted)
        {
            return Data.Decrypt(encrypted);
        }

        public async Task<OperationResult<byte[]>> BuildReportPdfForCodeAsync(long patientOrderDetailCode)
        {
            var reportCodes = await ReportManager.GetReportCodesForSameOrderDate(patientOrderDetailCode, 1);
            var reportStreams = new List<byte[]>();

            foreach (var reportCode in reportCodes)
            {
                var reportBytes = await ReportManager.GetReportBytes(Data.Encrypt(reportCode.ToString()));
                if (reportBytes != null && reportBytes.Length > 0)
                    reportStreams.Add(reportBytes);
            }

            if (reportStreams.Count == 0)
                return OperationResult<byte[]>.Fail("Report not found");

            var streamBytes = reportStreams.Count == 1
                ? reportStreams[0]
                : ReportManager.MergeReportStreams(reportStreams);

            if (streamBytes == null || streamBytes.Length == 0)
                return OperationResult<byte[]>.Fail("Report not found");

            return OperationResult<byte[]>.Success(streamBytes);
        }

        public async Task<OperationResult<byte[]>> BuildPrescriptionPdfForCodeAsync(long encounterCode)
        {
            var encrypted = Data.Encrypt(encounterCode.ToString());
            var streamBytes = await ReportManager.GetPatientEncounterReportBytes(encrypted);

            if (streamBytes == null || streamBytes.Length == 0)
                return OperationResult<byte[]>.Fail("Prescription not found");

            return OperationResult<byte[]>.Success(streamBytes);
        }
    }
}
