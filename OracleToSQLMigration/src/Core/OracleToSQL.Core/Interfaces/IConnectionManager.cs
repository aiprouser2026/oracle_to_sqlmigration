using System.Data;
using System.Threading.Tasks;

namespace OracleToSQL.Core.Interfaces
{
    /// <summary>
    /// Interface for managing database connections
    /// </summary>
    public interface IConnectionManager
    {
        /// <summary>
        /// Tests if a connection string is valid
        /// </summary>
        Task<bool> TestConnectionAsync(string connectionString);

        /// <summary>
        /// Creates a new database connection
        /// </summary>
        Task<IDbConnection> CreateConnectionAsync(string connectionString);

        /// <summary>
        /// Gets the database version
        /// </summary>
        Task<string> GetDatabaseVersionAsync(string connectionString);

        /// <summary>
        /// Gets the database name from connection string
        /// </summary>
        string GetDatabaseName(string connectionString);
    }
}
