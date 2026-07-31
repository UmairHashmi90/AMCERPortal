using ERPaperless.Abstractions.Application;
using ERPaperless.Application.Common;
using ERPaperless.Models;
using ERPaperless.Services;
using System.Collections.Generic;

namespace ERPaperless.Application.Services
{
    public class BillingService : IBillingService
    {
        public List<InvestigationRowViewModel> GetAllInvestigations(int companyCode, int branchCode, int userCode, bool pendingOnly)
        {
            return ERReviewFormRepository.GetAllInvestigationsForBilling(companyCode, branchCode, userCode, pendingOnly);
        }

        public List<InvestigationRowViewModel> GetInvestigations(string patientId, int companyCode)
        {
            return ERReviewFormRepository.GetInvestigations(patientId, companyCode);
        }

        public OperationResult<InvestigationRowViewModel> AcknowledgeInvestigation(long investigationId, string patientId, int companyCode, int userCode, string userName)
        {
            var result = ERReviewFormRepository.AcknowledgeInvestigationForBilling(
                investigationId, patientId, companyCode, userCode, userName);

            if (result == null)
                return OperationResult<InvestigationRowViewModel>.Fail(ERReviewFormRepository.LastError ?? "Could not acknowledge test.");

            if (!string.IsNullOrWhiteSpace(ERReviewFormRepository.LastError) && !result.IsAcknowledged)
                return OperationResult<InvestigationRowViewModel>.Fail(ERReviewFormRepository.LastError);

            return OperationResult<InvestigationRowViewModel>.Success(result, "Test acknowledged for billing.");
        }
    }
}
