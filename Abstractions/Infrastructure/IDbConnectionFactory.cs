using System.Data.SqlClient;

namespace ERPaperless.Abstractions.Infrastructure
{
    public interface IDbConnectionFactory
    {
        SqlConnection CreateConnection();
    }
}
