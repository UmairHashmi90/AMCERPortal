using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;

namespace ERPaperless.Services
{
    public static class DBManager
    {
        public static string strConnection = ConfigurationManager.ConnectionStrings["ERDatabase"].ConnectionString;


    }
}