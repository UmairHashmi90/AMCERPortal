using ERPaperless.Application.Common;
using ERPaperless.Models;
using System.Collections.Generic;

namespace ERPaperless.Abstractions.Application
{
    public interface IPharmacyService
    {
        List<PackageOrderRowViewModel> GetAllOrders(int companyCode, int branchCode, int userCode, bool pendingOnly);
        List<PackageOrderRowViewModel> GetOrdersForPatient(string patientId, int companyCode);
        OperationResult<PackageOrderRowViewModel> AcknowledgePackageOrder(long orderId, string patientId, int companyCode, int userCode, string userName);
    }
}
