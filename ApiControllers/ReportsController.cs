using ERPaperless.Abstractions.Application;
using ERPaperless.Application.Services;
using ERPaperless.Services;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Http;

namespace ERPaperless.ApiControllers
{
    public class ReportsController : ApiController
    {
        private readonly IReportsPortalService _reportsPortalService;

        public ReportsController(IReportsPortalService reportsPortalService = null)
        {
            _reportsPortalService = reportsPortalService ?? new ReportsPortalService();
        }

        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/GetReport")]
        public async Task<HttpResponseMessage> GetOnlinePdfReports(string q)
        {
            byte[] streamBytes = null;
            long code = -1;
            try
            {
                if (string.IsNullOrWhiteSpace(q))
                {
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Not Found");
                }

                var decrypted = _reportsPortalService.DecryptCode(q);
                if (!Int64.TryParse(decrypted, out code) || code <= 0)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Not Found");
                }

                var resultData = await _reportsPortalService.BuildReportPdfForCodeAsync(code);
                if (!resultData.Ok || resultData.Data == null || resultData.Data.Length == 0)
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, resultData.Message ?? "Report not found");
                streamBytes = resultData.Data;

                HttpResponseMessage result = Request.CreateResponse(HttpStatusCode.OK);
                result.Content = new ByteArrayContent(streamBytes);
                result.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline");
                result.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                return result;
            }
            catch (Exception ex)
            {
                ErrorLogging.Log("ReportsController", "GetOnlinePdfReports", ex);
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Not Found");
            }
        }

        [System.Web.Http.HttpGet]
        [System.Web.Http.Route("api/GetPrescriptionReport")]
        public async Task<HttpResponseMessage> GetPrescriptionPdf(string q)
        {
            byte[] streamBytes = null;
            long code = -1;
            try
            {
                if (string.IsNullOrWhiteSpace(q))
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Not Found");

                var decrypted = _reportsPortalService.DecryptCode(q);
                if (!Int64.TryParse(decrypted, out code) || code <= 0)
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Not Found");

                var resultData = await _reportsPortalService.BuildPrescriptionPdfForCodeAsync(code);
                if (!resultData.Ok || resultData.Data == null || resultData.Data.Length == 0)
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, resultData.Message ?? "Prescription not found");

                streamBytes = resultData.Data;
                HttpResponseMessage result = Request.CreateResponse(HttpStatusCode.OK);
                result.Content = new ByteArrayContent(streamBytes);
                result.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline");
                result.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                return result;
            }
            catch (Exception ex)
            {
                ErrorLogging.Log("ReportsController", "GetPrescriptionPdf", ex);
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Not Found");
            }
        }
    }
}