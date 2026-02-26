using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using OracleToSQL.Core.Interfaces;
using Serilog;

namespace OracleToSQL.Core.Services
{
    /// <summary>
    /// Connection manager for SQL Server databases
    /// </summary>
    public class SqlServerConnectionManager : IConnectionManager
    {
        private readonly ILogger _logger;

        public SqlServerConnectionManager(ILogger? logger = null)
        {
            _logger = logger ?? Log.Logger;
        }

        /// <summary>
        /// Tests if a SQL Server connection string is valid
        /// </summary>
        public async Task<bool> TestConnectionAsync(string connectionString)
        {
            try
            {
                _logger.Information("Testing SQL Server connection...");

                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var version = await GetDatabaseVersionAsync(connectionString);
                _logger.Information("SQL Server connection successful. Version: {Version}", version);

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to connect to SQL Server database");
                return false;
            }
        }

        /// <summary>
        /// Creates a new SQL Server database connection
        /// </summary>
        public async Task<IDbConnection> CreateConnectionAsync(string connectionString)
        {
            try
            {
                var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
                _logger.Debug("SQL Server connection created successfully");
                return connection;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to create SQL Server connection");
                throw;
            }
        }

        /// <summary>
        /// Gets the SQL Server version
        /// </summary>
        public async Task<string> GetDatabaseVersionAsync(string connectionString)
        {
            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = "SELECT @@VERSION";

                var version = await command.ExecuteScalarAsync();
                return version?.ToString() ?? "Unknown";
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get SQL Server version");
                return "Unknown";
            }
        }

        /// <summary>
        /// Gets the database name from SQL Server connection string
        /// </summary>
        public string GetDatabaseName(string connectionString)
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(connectionString);
                return builder.InitialCatalog ?? "master";
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to extract database name from connection string");
                return "Unknown";
            }
        }

        /// <summary>
        /// Creates a new database if it doesn't exist
        /// </summary>
        public async Task<bool> CreateDatabaseIfNotExistsAsync(string connectionString, string databaseName)
        {
            try
            {
                // Connect to master database
                var builder = new SqlConnectionStringBuilder(connectionString)
                {
                    InitialCatalog = "master"
                };

                using var connection = new SqlConnection(builder.ConnectionString);
                await connection.OpenAsync();

                // Check if database exists
                using var checkCommand = connection.CreateCommand();
                checkCommand.CommandText = "SELECT database_id FROM sys.databases WHERE name = @name";
                checkCommand.Parameters.AddWithValue("@name", databaseName);

                var exists = await checkCommand.ExecuteScalarAsync();

                if (exists == null)
                {
                    _logger.Information("Creating database: {DatabaseName}", databaseName);

                    using var createCommand = connection.CreateCommand();
                    createCommand.CommandText = $"CREATE DATABASE [{databaseName}]";
                    await createCommand.ExecuteNonQueryAsync();

                    _logger.Information("Database created successfully: {DatabaseName}", databaseName);
                    return true;
                }
                else
                {
                    _logger.Information("Database already exists: {DatabaseName}", databaseName);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to create database: {DatabaseName}", databaseName);
                throw;
            }
        }

        /// <summary>
        /// Gets all schemas in the database
        /// </summary>
        public async Task<System.Collections.Generic.List<string>> GetSchemasAsync(string connectionString)
        {
            var schemas = new System.Collections.Generic.List<string>();

            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT name
                    FROM sys.schemas
                    WHERE name NOT IN ('sys', 'guest', 'INFORMATION_SCHEMA', 'db_owner',
                                      'db_accessadmin', 'db_securityadmin', 'db_ddladmin',
                                      'db_backupoperator', 'db_datareader', 'db_datawriter', 'db_denydatareader',
                                      'db_denydatawriter')
                    ORDER BY name";

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

        /// <summary>
        /// Creates a schema if it doesn't exist
        /// </summary>
        public async Task CreateSchemaIfNotExistsAsync(string connectionString, string schemaName)
        {
            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = $@"
                    IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = @schemaName)
                    BEGIN
                        EXEC('CREATE SCHEMA [{schemaName}]')
                    END";
                command.Parameters.AddWithValue("@schemaName", schemaName);

                await command.ExecuteNonQueryAsync();
                _logger.Information("Schema ensured: {SchemaName}", schemaName);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to create schema: {SchemaName}", schemaName);
                throw;
            }
        }
    }
}
