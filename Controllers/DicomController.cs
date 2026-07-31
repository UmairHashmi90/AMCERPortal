using System.Configuration;
using System.Web.Mvc;

namespace ERPaperless.Controllers
{
    public class DicomController : BaseController
    {
        public ActionResult Open(long patientOrderDetailCode = 0)
        {
            var code = patientOrderDetailCode;

            if (code <= 0)
                return RedirectToAction("Patients", "Home");

            var url = ConfigurationManager.AppSettings["ER:DicomUrl"];
            if (string.IsNullOrWhiteSpace(url))
                return RedirectToAction("Patients", "Home");
            else if (url.Contains("{0}"))
                url = string.Format(url, code);

            return Redirect(url);
        }
    }
}
