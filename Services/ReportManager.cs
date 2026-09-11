using DevExpress.XtraRichEdit;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.Reporting.WebForms;
using Newtonsoft.Json.Linq;
using QRCoder;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;
using System.Web.UI.WebControls;
using static DevExpress.XtraPrinting.Native.ExportOptionsPropertiesNames;

namespace ERPaperless.Services
{
    public static class ReportManager
    {
        private static ReportDataSource datasource2 = null;
        private static byte[] MergePdfReports(IEnumerable<byte[]> reports, string strTitle = null)
        {
            var reportList = reports?.Where(r => r != null && r.Length > 0).ToList();
            if (reportList == null || reportList.Count == 0)
            {
                return null;
            }

            using (var outputStream = new MemoryStream())
            {
                using (var document = new Document())
                using (var copy = new PdfCopy(document, outputStream))
                {
                    document.Open();
                    foreach (var report in reportList)
                    {
                        if (report == null || report.Length == 0)
                            continue;

                        try
                        {
                            using (var reader = new PdfReader(report))
                            {
                                for (var pageNo = 1; pageNo <= reader.NumberOfPages; pageNo++)
                                {
                                    copy.AddPage(copy.GetImportedPage(reader, pageNo));
                                }
                            }
                        }
                        catch
                        {
                            // Skip invalid/corrupt PDF blobs so one bad report does not break merge.
                        }
                    }

                    document.Close();
                }
                return outputStream.ToArray();
            }
        }
        public static byte[] MergeReportStreams(IEnumerable<byte[]> reports, string strTitle = null)
        {
            return MergePdfReports(reports, strTitle);
        }
        private static async Task<byte[]> GetAndMergePatientEncounterReports(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return null;

            var reports = new List<byte[]>();
            var reportBytes = await PDFReport(
                "rptOPDPatientEncounter",
                ".rdlc",
                "procRptOPDPatientEncounter",
                "tblOPDPatientEncounter.intOPDPatientEncounterCode = " + code);

            if (reportBytes != null && reportBytes.Length > 0)
                reports.Add(reportBytes);

            return MergePdfReports(reports);
        }
        public async static Task<byte[]> GetPatientEncounterReportBytes(string strCode)
        {
            if (string.IsNullOrWhiteSpace(strCode))
                return null;

            long encounterCode;
            if (!long.TryParse(Data.Decrypt(strCode), out encounterCode) || encounterCode <= 0)
                return null;

            return await GetAndMergePatientEncounterReports(encounterCode.ToString());
        }
        public async static Task<List<long>> GetReportCodesForSameOrderDate(long intPatientOrderDetailCode, int intCompanyCode = 1)
        {
            var codes = new List<long>();
            if (intPatientOrderDetailCode <= 0 || intCompanyCode <= 0)
                return codes;

            const string sql = @"
;WITH BaseOrder AS (
    SELECT TOP 1
        po.intPatientCode,
        po.intCompanyCode,
        CAST(po.dtmOrder AS DATE) AS OrderDate
    FROM tblPatientOrderDetail pod
    INNER JOIN tblPatientOrder po
        ON po.intPatientOrderCode = pod.intPatientOrderCode
        AND po.intBranchCode = pod.intBranchCode
        AND po.intCompanyCode = pod.intCompanyCode
    WHERE pod.intPatientOrderDetailCode = @intPatientOrderDetailCode
      AND pod.intCompanyCode = @intCompanyCode
)
SELECT DISTINCT pod2.intPatientOrderDetailCode
FROM BaseOrder b
INNER JOIN tblPatientOrder po2
    ON po2.intPatientCode = b.intPatientCode
    AND po2.intCompanyCode = b.intCompanyCode
    AND CAST(po2.dtmOrder AS DATE) = b.OrderDate
INNER JOIN tblPatientOrderDetail pod2
    ON pod2.intPatientOrderCode = po2.intPatientOrderCode
    AND pod2.intBranchCode = po2.intBranchCode
    AND pod2.intCompanyCode = po2.intCompanyCode
WHERE pod2.intCompanyCode = @intCompanyCode
  AND ISNULL(pod2.intServiceStatusCode, 0) <> 8
ORDER BY pod2.intPatientOrderDetailCode DESC;";

            using (var conn = new SqlConnection(DBManager.strConnection))
            using (var cmd = new SqlCommand(sql, conn))
            {
                try
                {
                    await conn.OpenAsync();
                    cmd.Parameters.Add("@intPatientOrderDetailCode", SqlDbType.BigInt).Value = intPatientOrderDetailCode;
                    cmd.Parameters.Add("@intCompanyCode", SqlDbType.Int).Value = intCompanyCode;

                    using (var rdr = await cmd.ExecuteReaderAsync())
                    {
                        while (await rdr.ReadAsync())
                        {
                            if (!rdr.IsDBNull(0))
                                codes.Add(Convert.ToInt64(rdr.GetValue(0)));
                        }
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogging.Log("PatientPortal.Logic.DAL", "ReportManager.GetReportCodesForSameOrderDate", ex);
                }
                finally
                {
                    conn.Close();
                }
            }

            if (codes.Count == 0)
                codes.Add(intPatientOrderDetailCode);

            return codes;
        }
        public async static Task<byte[]> GetReportBytes(string strCode)

        {
            byte[] streamBytes = null;
            try
            {
                long Code = 0;
                int TypeCode = 0;
                string SampleNo = null, strPNRNo = null;
                Int64.TryParse(Data.Decrypt(strCode), out Code);
                DataTable dtPatientOrderDetailStatus = await ReportManager.GetPatientOrderDetailStatus(Code, 1);
                if (dtPatientOrderDetailStatus.Rows.Count > 0)
                {
                    TypeCode = (Int32)dtPatientOrderDetailStatus.Rows[0]["intReportTypeCode"];
                    SampleNo = dtPatientOrderDetailStatus.Rows[0]["intSampleNo"].ToString();
                    strPNRNo = dtPatientOrderDetailStatus.Rows[0]["strPNRNo"].ToString();
                }
                if ((Int32)TypeCode == 1)
                {
                    //streamBytes = await  PDFReport("rptSimpleTabular", ".rdlc", "procRptLabSimpleReport", "tblPatientOrderDetailStatus.intSampleNo=" + SampleNo);
                    streamBytes = await PDFReport("rptSimpleTabular", ".rdlc", "procRptLabSimpleReport", "tblPatientOrderDetail.intPatientOrderDetailCode=" + Code);
                }
                else if ((Int32)TypeCode == 2)
                {
                    //streamBytes = await  PDFReport("rptMicrobiology", ".rdlc", "procRptLabMicrobiologyReport", "tblPatientOrderDetailStatus.intSampleNo=" + SampleNo);
                    streamBytes = await PDFReport("rptMicrobiology", ".rdlc", "procRptLabMicrobiologyReport", "tblPatientOrderDetail.intPatientOrderDetailCode=" + Code);

                }
                else if ((Int32)TypeCode == 3)
                {

                    byte[] img = await GetPatientReportResult(Code, 1);
                    MemoryStream stream = new MemoryStream(img);
                    MemoryStream stream2 = new MemoryStream();
                    RichEditDocumentServer richEdit = new RichEditDocumentServer();
                    richEdit.LoadDocument(stream);
                    richEdit.ExportToPdf(stream2);
                    streamBytes = stream2.ToArray();
                }
                else if ((Int32)TypeCode == 4)
                {
                    streamBytes = await PDFReport("rptMicrobiologyCulture", ".rdlc", "procRptLabMicrobiologyReport", "tblLabTestResult.intPatientOrderDetailCode=" + Code);


                }
                else if ((Int32)TypeCode == 5)
                {
                    if (strPNRNo.Length > 0)
                    {
                        streamBytes = await PDFReport("rptSimpleTabularPCRWithQR", ".rdlc", "procRptLabSimpleReportPCRQR", "tblPatientOrderDetail.intPatientOrderDetailCode=" + Code);
                    }
                    else
                    {
                        streamBytes = await PDFReport("rptSimpleTabularPCR", ".rdlc", "procRptLabSimpleReportPCR", "tblPatientOrderDetail.intPatientOrderDetailCode=" + +Code);
                    }

                }
                return streamBytes;
            }
            catch (Exception ex)
            {
                ErrorLogging.Log("PatientPortal.Logic.DAL", "ReportManager.GetReportBytes", ex);
                return streamBytes;
            }

        }
        public async static Task<DataTable> GetReportData(String strReportName, String strProcName, Int32? intFinancialYearCode, DateTime? dtFrom, DateTime? dtTo, DateTime? dtDate1, DateTime? dtDate2, String strData1, String strData2, Int32? intBranchCode, Int32 intCompanyCode, String strWhereClause)
        {
            DataTable dtblData = new DataTable();
            using (SqlConnection objConn = new SqlConnection(DBManager.strConnection))
            {
                try
                {
                    await objConn.OpenAsync(); // Use OpenAsync for asynchronous opening of the connection
                    using (SqlCommand objCommand = new SqlCommand())
                    {
                        objCommand.Connection = objConn;
                        objCommand.CommandType = CommandType.StoredProcedure;
                        objCommand.CommandText = strProcName;
                        objCommand.Parameters.AddWithValue("@intFinancialYearCode", intFinancialYearCode);
                        objCommand.Parameters.AddWithValue("@dtFrom", dtFrom);
                        objCommand.Parameters.AddWithValue("@dtTo", dtTo);
                        objCommand.Parameters.AddWithValue("@dtDate1", dtDate1);
                        objCommand.Parameters.AddWithValue("@dtDate2", dtDate2);
                        objCommand.Parameters.AddWithValue("@strData1", strData1);
                        objCommand.Parameters.AddWithValue("@strData2", strData2);
                        objCommand.Parameters.AddWithValue("@intBranchCode", intBranchCode);
                        objCommand.Parameters.AddWithValue("@intCompanyCode", intCompanyCode);
                        if (!string.IsNullOrEmpty(strWhereClause))
                        {
                            if (strWhereClause.Trim().Substring(0, 3).Equals("AND"))
                                objCommand.Parameters.AddWithValue("@strWhereClause", strWhereClause);
                            else
                                objCommand.Parameters.AddWithValue("@strWhereClause", " AND " + strWhereClause);
                        }
                        else
                            objCommand.Parameters.AddWithValue("@strWhereClause", "");
                        using (SqlDataAdapter objDA = new SqlDataAdapter(objCommand))
                        {
                            await Task.Run(() => objDA.Fill(dtblData)); // Use Task.Run for synchronous Fill method
                            return dtblData;
                        }
                    }
                }
                catch (SqlException ex)
                {
                    ErrorLogging.Log("PatientPortal.Logic.DAL", "ReportManager.GetReportData", ex);
                    return null;
                }
                finally
                {
                    objConn.Close();
                }

            }
        }
        public async static Task<byte[]> GetPatientReportResult(Nullable<long> intPatientOrderDetailCode, Nullable<int> intCompanyCode)
        {
            byte[] vbrResult = null;

            string sprocname = "procPatientReportResultForMobileAPI";
            SqlDataReader dr = null;

            using (SqlConnection conn = new SqlConnection(DBManager.strConnection))
            {
                try
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand(sprocname, conn))
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;
                        var paraIntPatientOrderDetailCode = intPatientOrderDetailCode.HasValue ?
                            new SqlParameter("intpatientOrderDetailCode", intPatientOrderDetailCode) :
                            new SqlParameter("intpatientOrderDetailCode", typeof(long));
                        var paraIntCompanyCode = intCompanyCode.HasValue ?
                            new SqlParameter("intCompanyCode", intCompanyCode) :
                            new SqlParameter("intCompanyCode", typeof(int));
                        cmd.Parameters.Add(paraIntPatientOrderDetailCode);
                        cmd.Parameters.Add(paraIntCompanyCode);
                        dr = cmd.ExecuteReader();
                        while (dr.Read())
                        {
                            vbrResult = (byte[])dr[0]; // Use Task.Run for synchronous Fill method
                            return vbrResult;

                        }
                        conn.Close();
                        return vbrResult;
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogging.Log("PatientPortal.Logic.DAL", "ReportManager.GetPatientReportResult", ex);
                    return null;
                }
                finally
                {
                    conn.Close();
                }
            }

        }
        public async static Task<DataTable> GetBranchDetail(Nullable<int> intBranchCode, Nullable<int> intCompanyCode)
        {
            DataTable dtblData = new DataTable();
            string sprocname = "procGetBranchForMobileAPI";
            using (SqlConnection conn = new SqlConnection(DBManager.strConnection))
            {
                try
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand(sprocname, conn))
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;
                        var paraIntBranchCode = intBranchCode.HasValue ?
                            new SqlParameter("intBranchCode", intBranchCode) :
                            new SqlParameter("intBranchCode", typeof(long));
                        var paraIntCompanyCode = intCompanyCode.HasValue ?
                            new SqlParameter("intCompanyCode", intCompanyCode) :
                            new SqlParameter("intCompanyCode", typeof(int));
                        cmd.Parameters.Add(paraIntBranchCode);
                        cmd.Parameters.Add(paraIntCompanyCode);
                        using (SqlDataAdapter objDA = new SqlDataAdapter(cmd))
                        {
                            await Task.Run(() => objDA.Fill(dtblData)); // Use Task.Run for synchronous Fill method
                            return dtblData;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogging.Log("PatientPortal.Logic.DAL", "ReportManager.GetBranchDetail", ex);
                    return null;
                }
                finally
                {
                    conn.Close();
                }
            }
        }
        public static DataTable GetIPDAdmServiceOrder(Nullable<long> intIPDAdmOrderCode, Nullable<int> intCompanyCode, Nullable<int> intBarachCode)
        {
            DataTable dtblData = new DataTable();
            try
            {

                string sprocname = "procGrdIPDAdmServiceOrderForMobileAPI";
                string jsonOutputParam = "@json";

                using (SqlConnection conn = new SqlConnection(DBManager.strConnection))
                {
                    conn.Open();

                    using (SqlCommand cmd = new SqlCommand(sprocname, conn))
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;
                        var intIPDAdmOrderCodeParameter = intIPDAdmOrderCode.HasValue ?
                            new SqlParameter("intIPDAdmOrderCode", intIPDAdmOrderCode) :
                             new SqlParameter("intIPDAdmOrderCode", typeof(long));

                        var intBranchCodeParameter = intBarachCode.HasValue ?
                            new SqlParameter("intBranchCode", intBarachCode) :
                            new SqlParameter("intBranchCode", typeof(int));

                        var intCompanyCodeParameter = intCompanyCode.HasValue ?
                            new SqlParameter("intCompanyCode", intCompanyCode) :
                            new SqlParameter("intCompanyCode", typeof(int));

                        cmd.Parameters.Add(intIPDAdmOrderCodeParameter);
                        cmd.Parameters.Add(intBranchCodeParameter);
                        cmd.Parameters.Add(intCompanyCodeParameter);

                        using (SqlDataAdapter objDA = new SqlDataAdapter(cmd))
                        {
                            objDA.Fill(dtblData);
                            return dtblData;
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(ex);
                throw;
            }
        }
        public static DataTable GetMedicationOrder(Nullable<long> intIPDAdmOrderCode, Nullable<int> intCompanyCode, Nullable<int> intBarachCode)
        {
            DataTable dtblData = new DataTable();
            try
            {

                string sprocname = "procGrdMedOrderForMobileAPI";
                string jsonOutputParam = "@json";

                using (SqlConnection conn = new SqlConnection(DBManager.strConnection))
                {
                    conn.Open();

                    using (SqlCommand cmd = new SqlCommand(sprocname, conn))
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;
                        var intIPDAdmOrderCodeParameter = intIPDAdmOrderCode.HasValue ?
                            new SqlParameter("intIPDAdmOrderCode", intIPDAdmOrderCode) :
                             new SqlParameter("intIPDAdmOrderCode", typeof(long));

                        var intBranchCodeParameter = intBarachCode.HasValue ?
                            new SqlParameter("intBranchCode", intBarachCode) :
                            new SqlParameter("intBranchCode", typeof(int));

                        var intCompanyCodeParameter = intCompanyCode.HasValue ?
                            new SqlParameter("intCompanyCode", intCompanyCode) :
                            new SqlParameter("intCompanyCode", typeof(int));

                        cmd.Parameters.Add(intIPDAdmOrderCodeParameter);
                        cmd.Parameters.Add(intBranchCodeParameter);
                        cmd.Parameters.Add(intCompanyCodeParameter);

                        using (SqlDataAdapter objDA = new SqlDataAdapter(cmd))
                        {
                            objDA.Fill(dtblData);
                            return dtblData;
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log(ex);
                throw;
            }
        }

        public async static Task<DataTable> GetPatientOrderDetailStatus(Nullable<long> intPatientOrderDetailCode, Nullable<int> intCompanyCode)
        {
            DataTable dtblData = new DataTable();
            string sprocname = "procGetPatientOrderDetailStatusForMobileAPI";
            using (SqlConnection conn = new SqlConnection(DBManager.strConnection))
            {
                try
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand(sprocname, conn))
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;
                        var paraIntOrderDetailCode = @intPatientOrderDetailCode.HasValue ?
                            new SqlParameter("intPatientOrderDetailCode", @intPatientOrderDetailCode) :
                            new SqlParameter("intPatientOrderDetailCode", typeof(long));
                        var paraIntCompanyCode = intCompanyCode.HasValue ?
                            new SqlParameter("intCompanyCode", intCompanyCode) :
                            new SqlParameter("intCompanyCode", typeof(int));
                        cmd.Parameters.Add(paraIntOrderDetailCode);
                        cmd.Parameters.Add(paraIntCompanyCode);
                        using (SqlDataAdapter objDA = new SqlDataAdapter(cmd))
                        {
                            await Task.Run(() => objDA.Fill(dtblData)); // Use Task.Run for synchronous Fill method
                            return dtblData;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogging.Log("PatientPortal.Logic.DAL", "ReportManager.GetPatientOrderDetailStatus", ex);
                    return null;
                }
                finally
                {
                    conn.Close();
                }
            }
        }
        public async static Task<List<ReportParameter>> GetReportParameters()
        {
            List<ReportParameter> RptParamList = new List<ReportParameter>();
            try
            {

                DataTable dtBranch = await GetBranchDetail(1, 1);
                if (dtBranch.Rows.Count > 0)
                {
                    if (dtBranch.Rows[0]["strReportLine1"].ToString().Length > 0)
                        RptParamList.Add(new ReportParameter("ReportLine1", dtBranch.Rows[0]["strReportLine1"].ToString(), true));
                    else
                        RptParamList.Add(new ReportParameter("ReportLine1", "", false));

                    if (dtBranch.Rows[0]["strReportLine2"].ToString().Length > 0)
                        RptParamList.Add(new ReportParameter("ReportLine2", dtBranch.Rows[0]["strReportLine2"].ToString(), true));
                    else
                        RptParamList.Add(new ReportParameter("ReportLine2", "", false));

                    if (dtBranch.Rows[0]["strReportLine3"].ToString().Length > 0)
                        RptParamList.Add(new ReportParameter("ReportLine3", dtBranch.Rows[0]["strReportLine3"].ToString(), true));
                    else
                        RptParamList.Add(new ReportParameter("ReportLine3", "", false));

                    if (dtBranch.Rows[0]["strReportLine4"].ToString().Length > 0)
                        RptParamList.Add(new ReportParameter("ReportLine4", dtBranch.Rows[0]["strReportLine4"].ToString(), true));
                    else
                        RptParamList.Add(new ReportParameter("ReportLine4", "", false));
                }
                RptParamList.Add(new ReportParameter("LogoFile", HostingEnvironment.MapPath("~/assests/images/") + "isologo.jpg", true));
                RptParamList.Add(new ReportParameter("strFilter", "", true));
                RptParamList.Add(new ReportParameter("strUser", "Online Report", true));
                RptParamList.Add(new ReportParameter("strSystemIP", "", false));
                RptParamList.Add(new ReportParameter("strSystemName", "", false));
                RptParamList.Add(new ReportParameter("dtmPrintDateTime", DateTime.Now.ToString("dd-MM-yyyy HH:mm"), true));
                RptParamList.Add(new ReportParameter("strPrintDateTime", DateTime.Now.ToString("dd-MM-yyyy HH:mm"), true));
                return await Task.FromResult(RptParamList);
            }
            catch (Exception ex)
            {

                ErrorLogging.Log("PatientPortal.Logic.DAL", "ReportManager.GetReportParameters", ex);
                return RptParamList;
            }


        }
        public async static Task<byte[]> PDFReport(string strReportName, string strReportType, string strProcName, string strWhereClass)
        {
            byte[] streamBytes = null;
            try
            {
                List<ReportParameter> rptParamList = await GetReportParameters();

                DataTable dtblReportData = await GetReportData(strReportName, strProcName, null, null, null, null, null, "", "", 1, 1, strWhereClass);
                if (dtblReportData.Rows.Count > 0)
                {
                    ReportViewer rptViewer = new Microsoft.Reporting.WebForms.ReportViewer();
                    rptViewer.LocalReport.ReportPath = HttpContext.Current.Request.PhysicalApplicationPath + strReportName + strReportType;
                    rptViewer.LocalReport.DisplayName = GetOutcomePdfTitle(strReportName) ?? "Online Report";
                    rptViewer.LocalReport.ReportEmbeddedResource = strReportName + strReportType;
                    rptViewer.LocalReport.EnableExternalImages = true;
                    rptViewer.LocalReport.SubreportProcessing += LocalReport_SubreportProcessing;
                    if (dtblReportData.Columns.Contains("QRCodeOnline"))
                    {
                        foreach (DataRow dr in dtblReportData.Rows)
                        {
                            Int64 OrderDetailCode = (Int64)dr["intPatientOrderDetailCode"];
                            String strMRNo = dr["MRNO"].ToString();
                            string reportCode = Data.Encrypt(OrderDetailCode.ToString());
                            ImageConverter imgCon = new ImageConverter();
                            string strDataOnline = "http://reports.alimedical.org:54555/api/GetReport?q=" + reportCode;
                            dr["QRCodeOnline"] = (byte[])imgCon.ConvertTo(RenderQrCode(strDataOnline), typeof(byte[]));
                        }
                    }
                    if (dtblReportData.Columns.Contains("QRCodeOffline"))
                    {
                        foreach (DataRow dr in dtblReportData.Rows)
                        {
                            Int64 OrderDetailCode = (Int64)dr["intPatientOrderDetailCode"];
                            String strData = "Name : " + dtblReportData.Rows[0]["Patient"] + System.Environment.NewLine +
                                             "Passport No : " + dtblReportData.Rows[0]["PassportNo"] + System.Environment.NewLine +
                                             "Ticket No : " + dtblReportData.Rows[0]["PNR"] + System.Environment.NewLine +
                                             "Flight No : " + dtblReportData.Rows[0]["FlightNo"] + System.Environment.NewLine +
                                             "Destination : " + dtblReportData.Rows[0]["Destination"] + System.Environment.NewLine +
                                             "Result : " + dtblReportData.Rows[0]["strResult1"] + System.Environment.NewLine;
                            ImageConverter imgCon = new ImageConverter();
                            dr["QRCodeOffline"] = (byte[])imgCon.ConvertTo(RenderQrCode(strData), typeof(byte[]));
                        }
                    }
                    ReportDataSource datasource = new ReportDataSource("dsReport", dtblReportData);
                    rptViewer.LocalReport.DataSources.Clear();
                    rptViewer.LocalReport.DataSources.Add(datasource);
                    if (strReportName == "rptERDischargeSummary")
                    {
                        DataTable dtblMedication = await GetMedicationForDischargeSummary(strWhereClass);
                        datasource2 = new ReportDataSource("dsMedication", dtblMedication);
                        rptViewer.LocalReport.DataSources.Add(datasource2);
                    }
                    if (strReportName == "rptIPDAdmOrder")
                    {
                        string result = strWhereClass.Split('=')[1];
                        Int64.TryParse(result, out Int64 AdmCode);
                        DataTable dttblMedicationData = ReportManager.GetMedicationOrder(AdmCode, 1, 1);
                        DataTable dttblServicesData = ReportManager.GetIPDAdmServiceOrder(AdmCode, 1, 1);
                        ReportDataSource medDataSource = new ReportDataSource("dsMedication", dttblMedicationData);
                        ReportDataSource serDataSource = new ReportDataSource("dsServices", dttblServicesData);
                        rptViewer.LocalReport.DataSources.Add(medDataSource);
                        rptViewer.LocalReport.DataSources.Add(serDataSource);
                    }


                    ReportParameterInfoCollection rpc = rptViewer.LocalReport.GetParameters();
                    Boolean bolIsfound = false;
                    List<ReportParameter> RptParamListFinal = new List<ReportParameter>();
                abc:
                    foreach (ReportParameter rpi in rptParamList)
                    {
                        bolIsfound = false;
                        foreach (ReportParameterInfo rp in rpc)
                        {
                            if (rpi.Name.Equals(rp.Name, StringComparison.CurrentCultureIgnoreCase))
                            {
                                bolIsfound = true;
                                break;
                            }
                        }
                        if (bolIsfound == false)
                        {
                            rptParamList.Remove(rpi);
                            goto abc;
                        }
                    }
                    foreach (ReportParameterInfo rp in rpc)
                        rptViewer.LocalReport.SetParameters(rptParamList);
                    rptViewer.LocalReport.Refresh();
                    string mimeType = "";
                    string encoding = "";
                    string filenameExtension = "";
                    string[] streamids = null;
                    Warning[] warnings = null;
                    streamBytes = rptViewer.LocalReport.Render("PDF", null, out mimeType, out encoding, out filenameExtension, out streamids, out warnings);
                    var pdfTitle = GetOutcomePdfTitle(strReportName);
                    if (!string.IsNullOrEmpty(pdfTitle))
                        streamBytes = ApplyPdfTitle(streamBytes, pdfTitle);

                }
                return streamBytes;
            }
            catch (Exception ex)
            {
                ErrorLogging.Log("PatientPortal.Logic.DAL", "ReportManager.PDFReport", ex);
                return streamBytes;
            }
        }

        private static string GetOutcomePdfTitle(string reportName)
        {
            switch (reportName)
            {
                case "rptIPDAdmOrder": return "Admission Order";
                case "rptERDischargeSummary": return "Discharge Summary";
                case "rptERDeathCertificate": return "Death Certificate";
                case "rptERLAMA": return "LAMA";
                case "rptERPatientReferral": return "Patient Referral";
                default: return null;
            }
        }

        private static byte[] ApplyPdfTitle(byte[] pdfBytes, string title)
        {
            if (pdfBytes == null || pdfBytes.Length == 0 || string.IsNullOrWhiteSpace(title))
                return pdfBytes;

            try
            {
                using (var reader = new PdfReader(pdfBytes))
                using (var output = new MemoryStream())
                {
                    using (var stamper = new PdfStamper(reader, output))
                    {
                        var info = new Dictionary<string, string>();
                        var existing = reader.Info;
                        if (existing != null)
                        {
                            foreach (var entry in existing)
                                info[entry.Key] = entry.Value;
                        }
                        info["Title"] = title;
                        stamper.MoreInfo = info;
                    }
                    return output.ToArray();
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.Log("PatientPortal.Logic.DAL", "ReportManager.ApplyPdfTitle", ex);
                return pdfBytes;
            }
        }
        private static async void LocalReport_SubreportProcessing(object sender, SubreportProcessingEventArgs e)
        {
            try
            {
                String strPar = Convert.ToString(e.Parameters[0].Values[0]);
                if (e.ReportPath == "rptMicrobiologyOrgnasim")
                    e.DataSources.Add(new ReportDataSource("dsReport", GetSubReportDataForMicrobiologyOrganism(strPar)));

            }
            catch (Exception ex)
            {
                ErrorLogging.Log("ReportViewerController", "LocalReport_SubreportProcessing", ex);
                throw ex;
            }

        }
        public async static Task<DataTable> GetMedicationForDischargeSummary(String strWhereClause)
        {
            DataTable dtblData = new DataTable();
            string admissionCode = strWhereClause.Contains("=") ? strWhereClause.Split('=')[1].Trim() : string.Empty;
            using (SqlConnection objConn = new SqlConnection(DBManager.strConnection))
            {
                try
                {
                    await objConn.OpenAsync(); // Use OpenAsync for asynchronous opening of the connection
                    using (SqlCommand objCommand = new SqlCommand())
                    {
                        objCommand.Connection = objConn;
                        objCommand.CommandType = CommandType.StoredProcedure;
                        objCommand.CommandText = "procRptERDischargeMedicine";
                        objCommand.Parameters.AddWithValue("@intERAdmissionCode", admissionCode);
                        objCommand.Parameters.AddWithValue("@intBranchCode", 1);
                        objCommand.Parameters.AddWithValue("@intCompanyCode", 1);

                        using (SqlDataAdapter objDA = new SqlDataAdapter(objCommand))
                        {
                            await Task.Run(() => objDA.Fill(dtblData)); // Use Task.Run for synchronous Fill method
                            return dtblData;
                        }
                    }
                }
                catch (SqlException ex)
                {
                    ErrorLogging.Log("PatientPortal.Logic.DAL", "ReportManager.GetReportData", ex);
                    return null;
                }
                finally
                {
                    objConn.Close();
                }

            }
        }
        private static DataTable GetSubReportDataForMicrobiologyOrganism(String strParam)
        {
            DataTable dtblData = new DataTable();
            try
            {
                using (SqlConnection objConn = new SqlConnection(DBManager.strConnection))
                {
                    objConn.Open();
                    using (SqlCommand objCommand = new SqlCommand())
                    {
                        objCommand.Connection = objConn;
                        objCommand.CommandType = CommandType.Text;
                        objCommand.CommandText = @"SELECT tblTest.intLabTestResultCode ,
                                                   tblTest.strLabOrganismName ,
                                                   tblTest.strDrugEffectiveness ,
                                                   CASE WHEN LEN(tblTest.Medicine) > 0 THEN
	                                               LEFT(tblTest.Medicine,LEN(tblTest.Medicine)-1)
	                                               ELSE '' END Medicine
	   
	                                                FROM (

                                            SELECT DISTINCT tblLabTestResultMedicine.intLabTestResultCode, tblLabOrganism.strLabOrganismName,tblDrugEffectiveness.strDrugEffectiveness,
                                            REPLACE(REPLACE(
                                            (
	                                            SELECT  tblLabOrgMedicine.strLabOrgMedicineName AS Medicine
	
	                                            FROM tblLabTestResultMedicine tblLabTestResultMedicineInner
	                                            INNER JOIN tblLabOrganismDetail ON tblLabOrganismDetail.intLabOrganismDetailCode = tblLabTestResultMedicineInner.intLabOrganismDetailCode AND tblLabOrganismDetail.intCompanyCode = tblLabTestResultMedicineInner.intCompanyCode
	                                            INNER JOIN tblLabOrgMedicine ON tblLabOrgMedicine.intLabOrgMedicineCode = tblLabOrganismDetail.intLabOrgMedicineCode AND tblLabOrgMedicine.intCompanyCode = tblLabOrganismDetail.intCompanyCode
	                                            WHERE tblLabTestResultMedicineInner.intLabTestResultCode =tblLabTestResultMedicine.intLabTestResultCode
	                                            AND tblLabTestResultMedicine.intLabOrganismCode = tblLabTestResultMedicineInner.intLabOrganismCode
	                                            AND tblLabTestResultMedicine.intDrugEffectivenessCode = tblLabTestResultMedicineInner.intDrugEffectivenessCode
	                                            AND tblLabTestResultMedicine.intBranchCode = tblLabTestResultMedicineInner.intBranchCode
	                                            AND tblLabTestResultMedicine.intCompanyCode = tblLabTestResultMedicineInner.intCompanyCode
	                                              FOR XML PATH('')
                                            ),'<Medicine>',''),'</Medicine>',',') AS Medicine

                                             FROM tblLabTestResultMedicine
                                             INNER JOIN tblLabTestResult ON tblLabTestResult.intLabTestResultCode = tblLabTestResultMedicine.intLabTestResultCode AND tblLabTestResult.intBranchCode = tblLabTestResultMedicine.intBranchCode AND tblLabTestResult.intCompanyCode = tblLabTestResultMedicine.intCompanyCode
                                             INNER JOIN tblPatientOrderDetail ON tblPatientOrderDetail.intPatientOrderDetailCode = tblLabTestResult.intPatientOrderDetailCode 
                                            AND tblPatientOrderDetail.intBranchCode = tblLabTestResult.intBranchCode 
                                            AND tblPatientOrderDetail.intCompanyCode = tblLabTestResult.intCompanyCode

                                            INNER JOIN tblPatientOrder ON tblPatientOrder.intPatientOrderCode = tblPatientOrderDetail.intPatientOrderCode 
                                            AND tblPatientOrder.intBranchCode = tblPatientOrderDetail.intBranchCode 
                                            AND tblPatientOrder.intCompanyCode = tblPatientOrderDetail.intCompanyCode

                                             INNER JOIN tblDrugEffectiveness ON tblDrugEffectiveness.intDrugEffectivenessCode = tblLabTestResultMedicine.intDrugEffectivenessCode AND tblDrugEffectiveness.intCompanyCode = tblLabTestResultMedicine.intCompanyCode
                                            INNER JOIN tblLabOrganism ON tblLabOrganism.intLabOrganismCode = tblLabTestResultMedicine.intLabOrganismCode AND tblLabOrganism.intCompanyCode = tblLabTestResultMedicine.intCompanyCode
                                            WHERE tblLabTestResultMedicine.intLabTestResultCode = " + strParam + @"
                                            AND tblLabTestResultMedicine.intBranchCode = 1" + @"
                                            AND tblLabTestResultMedicine.intCompanyCode = 1" + @") tblTest";

                        using (SqlDataAdapter objDA = new SqlDataAdapter(objCommand))
                        {
                            objDA.Fill(dtblData);

                        }

                    }

                    objConn.Close();
                }
                return dtblData;
            }
            catch (Exception ex)
            {
                ErrorLogging.Log("Portal.Logic.DAL.Report", "GetSubReportDataForMicrobiologyOrganism", ex);
                return dtblData;
            }

        }
        private static System.Drawing.Image RenderQrCode(String strData)
        {
            System.Drawing.Image imgQR = null;
            QRCodeGenerator.ECCLevel eccLevel = (QRCodeGenerator.ECCLevel.H);
            using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
            {
                using (QRCodeData qrCodeData = qrGenerator.CreateQrCode(strData, eccLevel))
                {
                    using (QRCode qrCode = new QRCode(qrCodeData))
                    {
                        imgQR = qrCode.GetGraphic(5, Color.Black, Color.White,
                            GetIconBitmap(), 0);
                    }
                }
            }
            return imgQR;
        }
        public async static Task<byte[]> GetDeathCertificateBytes(string strCode)

        {
            byte[] streamBytes = null;
            try
            {
                long Code;
                if (!TryResolveReportCode(strCode, out Code) || Code <= 0)
                    return null;
                streamBytes = await PDFReport("rptERDeathCertificate", ".rdlc", "procRptDeathCertificateER", $"tblDeathCertificate.intERAdmissionCode={Code}");
                return streamBytes;
            }
            catch (Exception ex)
            {
                ErrorLogging.Log("PatientPortal.Logic.DAL", "ReportManager.GetDeathCertificateBytes", ex);
                return streamBytes;
            }

        }
        public async static Task<byte[]> GetAdmissionOrderBytes(string strCode)

        {
            byte[] streamBytes = null;
            try
            {
                long Code;
                if (!TryResolveReportCode(strCode, out Code) || Code <= 0)
                    return null;
                streamBytes = await PDFReport("rptIPDAdmOrder", ".rdlc", "procRptIPDAdmOrder", $"tblIPDAdmOrder.intERAdmissionCode={Code}");
                return streamBytes;
            }
            catch (Exception ex)
            {
                ErrorLogging.Log("PatientPortal.Logic.DAL", "ReportManager.GetAdmissionOrderBytes", ex);
                return streamBytes;
            }

        }


        public async static Task<byte[]> GetDischargeSummaryBytes(string strCode)

        {
            byte[] streamBytes = null;
            try
            {
                long Code;
                if (!TryResolveReportCode(strCode, out Code) || Code <= 0)
                    return null;
                streamBytes = await PDFReport("rptERDischargeSummary", ".rdlc", "procRptERDischargeCertificate", $"tblDischargeSummary.intERAdmissionCode={Code}");
                return streamBytes;
            }
            catch (Exception ex)
            {
                ErrorLogging.Log("PatientPortal.Logic.DAL", "ReportManager.GetDischargeSummaryBytes", ex);
                return streamBytes;
            }

        }
        public async static Task<byte[]> GetLAMABytes(string strCode)

        {
            byte[] streamBytes = null;
            try
            {
                long Code;
                if (!TryResolveReportCode(strCode, out Code) || Code <= 0)
                    return null;
                streamBytes = await PDFReport("rptERLAMA", ".rdlc", "procRptERLAMA", $"tblLAMA.intERAdmissionCode={Code}");
                return streamBytes;
            }
            catch (Exception ex)
            {
                ErrorLogging.Log("PatientPortal.Logic.DAL", "ReportManager.GetLAMABytes", ex);
                return streamBytes;
            }

        }
        public async static Task<byte[]> GetPatientReferralBytes(string strCode)

        {
            byte[] streamBytes = null;
            try
            {
                long Code;
                if (!TryResolveReportCode(strCode, out Code) || Code <= 0)
                    return null;
                streamBytes = await PDFReport("rptERPatientReferral", ".rdlc", "procRptERPatientReferral", $"tblPatientReferral.intERAdmissionCode={Code}");
                return streamBytes;
            }
            catch (Exception ex)
            {
                ErrorLogging.Log("PatientPortal.Logic.DAL", "ReportManager.GetPatientReferralBytes", ex);
                return streamBytes;
            }

        }
        private static bool TryResolveReportCode(string rawCode, out long code)
        {
            code = 0;
            var value = (rawCode ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
                return false;

            if (long.TryParse(value, out code) && code > 0)
                return true;

            try
            {
                var decrypted = Data.Decrypt(value);
                return long.TryParse(decrypted, out code) && code > 0;
            }
            catch
            {
                return false;
            }
        }
        private static Bitmap GetIconBitmap()
        {
            Bitmap img = null;
            return img;
        }

      
    }
}