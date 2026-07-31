using ERPaperless.Application.Common;
using ERPaperless.Models;
using System.Collections.Generic;

namespace ERPaperless.Abstractions.Application
{
    public interface IBillingService
    {
        List<InvestigationRowViewModel> GetAllInvestigations(int companyCode, int branchCode, int userCode, bool pendingOnly);
        List<InvestigationRowViewModel> GetInvestigations(string patientId, int companyCode);
        OperationResult<InvestigationRowViewModel> AcknowledgeInvestigation(long investigationId, string patientId, int companyCode, int userCode, string userName);
    }
}
