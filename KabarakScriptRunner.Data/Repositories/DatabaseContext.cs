using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace KabarakScriptRunner.Data.Repositories
{
    public class DatabaseContext
    {
        private readonly string _connectionString;

        public DatabaseContext(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found in appsettings.json.");
        }

        /// <summary>
        /// Plain connection (no retry). Use for short, simple queries.
        /// </summary>
        public IDbConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }

        /// <summary>
        /// Connection with the retry policy configured in <see cref="RetryPolicyFactory"/>.
        /// Use for anything that could be affected by transient network/SQL issues.
        /// </summary>
        public string ConnectionString => _connectionString;
    }
}