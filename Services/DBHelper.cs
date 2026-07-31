using System;
using System.Configuration;
using System.Data.Entity.Core.EntityClient;
using System.Data.SqlClient;

namespace ERPaperless.Services
{
    public static class DBHelper
    {
        private const string EntityMetadata =
            "res://*/Models.dbERPatient.csdl|res://*/Models.dbERPatient.ssdl|res://*/Models.dbERPatient.msl";

        /// <summary>
        /// Opens and returns a new SqlConnection using the ERDatabase connection string.
        /// Caller is responsible for disposing (use inside a using block).
        /// </summary>
        public static SqlConnection GetConnection()
        {
            var connectionString = GetSqlConnectionString();
            return new SqlConnection(connectionString);
        }

        /// <summary>
        /// Builds the EF6 entity connection string from ERDatabase so only one SQL
        /// connection string needs to be maintained on the server.
        /// </summary>
        public static string GetEntityConnectionString()
        {
            return new EntityConnectionStringBuilder
            {
                Metadata = EntityMetadata,
                Provider = "System.Data.SqlClient",
                ProviderConnectionString = GetSqlConnectionString()
            }.ConnectionString;
        }

        private static string GetSqlConnectionString()
        {
            var connectionString = ConfigurationManager
                .ConnectionStrings["ERDatabase"]?.ConnectionString;

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException(
                    "ERDatabase connection string is missing from Web.config. " +
                    "Add it under <connectionStrings>.");

            if (connectionString.IndexOf("metadata=", StringComparison.OrdinalIgnoreCase) >= 0)
                throw new InvalidOperationException(
                    "ERDatabase must be a plain SQL connection string. " +
                    "Do not copy the dbAMCEntities metadata connection string into ERDatabase.");

            return connectionString;
        }
    }
}
