using System;
using System.Threading;
using System.Threading.Tasks;
using OracleToSQL.Core.Models;

namespace OracleToSQL.Core.Interfaces
{
    /// <summary>
    /// Interface for migrating data between databases
    /// </summary>
    public interface IDataMigrator
    {
        /// <summary>
        /// Migrates data for a single table
        /// </summary>
        Task<MigrationResult> MigrateTableAsync(
            string sourceConnectionString,
            string targetConnectionString,
            TableMetadata table,
            MigrationOptions options,
            IProgress<MigrationProgress>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Migrates data for all tables in a schema
        /// </summary>
        Task<BatchMigrationResult> MigrateAllTablesAsync(
            string sourceConnectionString,
            string targetConnectionString,
            DatabaseSchema schema,
            MigrationOptions options,
            IProgress<MigrationProgress>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates data after migration
        /// </summary>
        Task<ValidationResult> ValidateDataAsync(
            string sourceConnectionString,
            string targetConnectionString,
            TableMetadata table,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Options for data migration
    /// </summary>
    public class MigrationOptions
    {
        public int BatchSize { get; set; } = 10000;
        public int MaxDegreeOfParallelism { get; set; } = 4;
        public bool DisableIndexesDuringMigration { get; set; } = true;
        public bool DisableConstraintsDuringMigration { get; set; } = true;
        public bool UseTransaction { get; set; } = true;
        public int CommandTimeout { get; set; } = 600;
        public bool ContinueOnError { get; set; } = false;
        public string? SessionId { get; set; }
    }

    /// <summary>
    /// Progress information for migration
    /// </summary>
    public class MigrationProgress
    {
        public string TableName { get; set; } = string.Empty;
        public long RowsProcessed { get; set; }
        public long TotalRows { get; set; }
        public double PercentComplete => TotalRows > 0 ? (double)RowsProcessed / TotalRows * 100 : 0;
        public string Status { get; set; } = string.Empty;
        public TimeSpan ElapsedTime { get; set; }
        public TimeSpan EstimatedTimeRemaining { get; set; }
    }

    /// <summary>
    /// Result of a table migration
    /// </summary>
    public class MigrationResult
    {
        public string TableName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public long RowsMigrated { get; set; }
        public TimeSpan Duration { get; set; }
        public string? ErrorMessage { get; set; }
        public Exception? Exception { get; set; }
    }

    /// <summary>
    /// Result of batch migration
    /// </summary>
    public class BatchMigrationResult
    {
        public int TotalTables { get; set; }
        public int SuccessfulTables { get; set; }
        public int FailedTables { get; set; }
        public long TotalRowsMigrated { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public System.Collections.Generic.List<MigrationResult> Results { get; set; } = new();
    }

    /// <summary>
    /// Result of data validation
    /// </summary>
    public class ValidationResult
    {
        public string TableName { get; set; } = string.Empty;
        public bool IsValid { get; set; }
        public long SourceRowCount { get; set; }
        public long TargetRowCount { get; set; }
        public System.Collections.Generic.List<string> ValidationErrors { get; set; } = new();
    }
}
