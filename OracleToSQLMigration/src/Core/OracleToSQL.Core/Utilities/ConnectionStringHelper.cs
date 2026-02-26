using System;
using System.Text.RegularExpressions;

namespace OracleToSQL.Core.Utilities
{
    /// <summary>
    /// Helper class for working with connection strings
    /// </summary>
    public static class ConnectionStringHelper
    {
        /// <summary>
        /// Masks sensitive information in a connection string for logging
        /// </summary>
        public static string MaskConnectionString(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                return string.Empty;

            try
            {
                // Mask password
                var masked = Regex.Replace(
                    connectionString,
                    @"(password|pwd)\s*=\s*[^;]+",
                    "$1=****",
                    RegexOptions.IgnoreCase);

                // Mask user id if needed for security
                // masked = Regex.Replace(masked, @"(user id|uid)\s*=\s*[^;]+", "$1=****", RegexOptions.IgnoreCase);

                return masked;
            }
            catch
            {
                return "****";
            }
        }

        /// <summary>
        /// Validates if a connection string has minimum required components
        /// </summary>
        public static bool IsValidConnectionString(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return false;

            // Basic validation - should contain at least a data source or server
            return connectionString.Contains("Data Source", StringComparison.OrdinalIgnoreCase) ||
                   connectionString.Contains("Server", StringComparison.OrdinalIgnoreCase) ||
                   connectionString.Contains("Host", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines if a connection string is for Oracle
        /// </summary>
        public static bool IsOracleConnectionString(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return false;

            return connectionString.Contains("Data Source", StringComparison.OrdinalIgnoreCase) &&
                   (connectionString.Contains("DBA Privilege", StringComparison.OrdinalIgnoreCase) ||
                    connectionString.Contains("Oracle", StringComparison.OrdinalIgnoreCase) ||
                    !connectionString.Contains("Initial Catalog", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Determines if a connection string is for SQL Server
        /// </summary>
        public static bool IsSqlServerConnectionString(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return false;

            return connectionString.Contains("Server", StringComparison.OrdinalIgnoreCase) ||
                   connectionString.Contains("Initial Catalog", StringComparison.OrdinalIgnoreCase) ||
                   connectionString.Contains("Database", StringComparison.OrdinalIgnoreCase);
        }
    }
}
