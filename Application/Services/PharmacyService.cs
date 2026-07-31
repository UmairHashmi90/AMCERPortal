using ERPaperless.Abstractions.Application;
using ERPaperless.Application.Common;
using ERPaperless.Models;
using ERPaperless.Services;
using System.Collections.Generic;

namespace ERPaperless.Application.Services
{
    public class PharmacyService : IPharmacyService
    {
        public List<PackageOrderRowViewModel> GetAllOrders(int companyCode, int branchCode, int userCode, bool pendingOnly)
        {
            return ERReviewFormRepository.GetAllPackageOrdersForPharmacy(companyCode, branchCode, userCode, pendingOnly);
        }

        public List<PackageOrderRowViewModel> GetOrdersForPatient(string patientId, int companyCode)
        {
            return ERReviewFormRepository.GetPackageOrdersForPharmacy(patientId, companyCode);
        }

        public OperationResult<PackageOrderRowViewModel> AcknowledgePackageOrder(long orderId, string patientId, int companyCode, int userCode, string userName)
        {
            var result = ERReviewFormRepository.AcknowledgePackageOrderForPharmacy(
                orderId, patientId, companyCode, userCode, userName);

            if (result == null)
                return OperationResult<PackageOrderRowViewModel>.Fail(ERReviewFormRepository.LastError ?? "Could not charge item.");

            if (!string.IsNullOrWhiteSpace(ERReviewFormRepository.LastError) && !result.IsPharmacyCharged)
                return OperationResult<PackageOrderRowViewModel>.Fail(ERReviewFormRepository.LastError);

            return OperationResult<PackageOrderRowViewModel>.Success(result, "Item charged / issued.");
        }
    }
}
