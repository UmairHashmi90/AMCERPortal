using ERPaperless.Abstractions.Infrastructure;
using ERPaperless.Services;
using System.Data.SqlClient;

namespace ERPaperless.Infrastructure.Infrastructure
{
    public class DbConnectionFactory : IDbConnectionFactory
    {
        public SqlConnection CreateConnection()
        {
            return DBHelper.GetConnection();
        }
    }
}
