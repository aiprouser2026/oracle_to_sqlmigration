using System;
using System.Data;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using OracleToSQL.Core.Interfaces;
using Serilog;

namespace OracleToSQL.Core.Services
{
    /// <summary>
    /// Connection manager for Oracle databases
    /// </summary>
    public class OracleConnectionManager : IConnectionManager
    {
        private readonly ILogger _logger;

        public OracleConnectionManager(ILogger? logger = null)
        {
            _logger = logger ?? Log.Logger;
        }

        /// <summary>
        /// Tests if an Oracle connection string is valid
        /// </summary>
        public async Task<bool> TestConnectionAsync(string connectionString)
        {
            try
            {
                _logger.Information("Testing Oracle connection...");

                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();

                var version = await GetDatabaseVersionAsync(connectionString);
                _logger.Information("Oracle connection successful. Version: {Version}", version);

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to connect to Oracle database");
                return false;
            }
        }

        /// <summary>
        /// Creates a new Oracle database connection
        /// </summary>
        public async Task<IDbConnection> CreateConnectionAsync(string connectionString)
        {
            try
            {
                var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();
                _logger.Debug("Oracle connection created successfully");
                return connection;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to create Oracle connection");
                throw;
            }
        }

        /// <summary>
        /// Gets the Oracle database version
        /// </summary>
        public async Task<string> GetDatabaseVersionAsync(string connectionString)
        {
            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = "SELECT BANNER FROM v$version WHERE ROWNUM = 1";

                var version = await command.ExecuteScalarAsync();
                return version?.ToString() ?? "Unknown";
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get Oracle version");
                return "Unknown";
            }
        }

        /// <summary>
        /// Gets the database name from Oracle connection string
        /// </summary>
        public string GetDatabaseName(string connectionString)
        {
            try
            {
                var builder = new OracleConnectionStringBuilder(connectionString);

                // Try to extract database name from Data Source or Service Name
                if (!string.IsNullOrEmpty(builder.DataSource))
                {
                    // Extract service name from TNS or Easy Connect string
                    var dataSource = builder.DataSource;

                    // For Easy Connect: host:port/service_name
                    if (dataSource.Contains("/"))
                    {
                        var parts = dataSource.Split('/');
                        return parts[parts.Length - 1];
                    }

                    return dataSource;
                }

                return "Unknown";
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to extract database name from connection string");
                return "Unknown";
            }
        }

        /// <summary>
        /// Gets the current schema name for the connection
        /// </summary>
        public async Task<string> GetCurrentSchemaAsync(string connectionString)
        {
            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = "SELECT SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA') FROM DUAL";

                var schema = await command.ExecuteScalarAsync();
                return schema?.ToString() ?? "Unknown";
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get current schema");
                return "Unknown";
            }
        }

        /// <summary>
        /// Gets all available schemas in the database
        /// </summary>
        public async Task<System.Collections.Generic.List<string>> GetSchemasAsync(string connectionString)
        {
            var schemas = new System.Collections.Generic.List<string>();

            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT username
                    FROM all_users
                    WHERE username NOT IN (
                        'SYS', 'SYSTEM', 'OUTLN', 'DBSNMP', 'APPQOSSYS',
                        'WMSYS', 'EXFSYS', 'CTXSYS', 'XDB', 'ANONYMOUS',
                        'ORDSYS', 'ORDDATA', 'MDSYS', 'LBACSYS', 'DVSYS',
                        'DVF', 'AUDSYS', 'OJVMSYS', 'GSMADMIN_INTERNAL'
                    )
                    ORDER BY username";

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    schemas.Add(reader.GetString(0));
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get schemas");
            }

            return schemas;
        }
    }
}
