using ERPaperless.Application.Common;
using ERPaperless.Models;
using System.Collections.Generic;

namespace ERPaperless.Abstractions.Application
{
    public interface IErFormService
    {
        ErFormViewModel BuildErFormModel(string patientId, int companyCode, int userCode);
        OperationResult AddVital(string patientId, int companyCode, int userCode, VitalRecordViewModel vital);
        OperationResult DeleteVital(long vitalId, int companyCode, int userCode);
        OperationResult<List<object>> GetPackageDetails(int packageCode);
        OperationResult<ErReviewFormStateViewModel> SaveReviewForm(SaveErReviewFormInputViewModel model, int companyCode, int userCode, bool isMo, bool isNursing);
        OperationResult<ErReviewFormStateViewModel> SavePackageOrders(SavePackageOrdersInputViewModel model, int companyCode, int userCode);
        OperationResult SaveOutcomeForm(SaveOutcomeFormInputViewModel model, int companyCode, int userCode);
        OutcomeAutoPopulateViewModel GetOutcomeAutoPopulateData(string patientId, long? admissionCode, int companyCode);
        OperationResult<ErReviewFormStateViewModel> UploadDocument(string patientId, string documentType, string fileName, string contentType, byte[] bytes, int companyCode, int userCode);
        byte[] GetDocumentBytes(long documentId, string patientId, int companyCode);
        OperationResult DeleteDocument(long documentId, string patientId, int companyCode, int userCode);
    }
}
