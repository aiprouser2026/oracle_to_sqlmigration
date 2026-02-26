using System.Collections.Generic;

namespace OracleToSQL.Core.Models
{
    /// <summary>
    /// Configuration for migration operations
    /// </summary>
    public class MigrationConfiguration
    {
        public string OracleConnectionString { get; set; } = string.Empty;
        public string SqlServerConnectionString { get; set; } = string.Empty;
        public string SourceSchema { get; set; } = string.Empty;
        public string TargetSchema { get; set; } = "dbo";
        public string TargetDatabase { get; set; } = string.Empty;

        public MigrationScope Scope { get; set; } = MigrationScope.Full;

        public bool MigrateSchema { get; set; } = true;
        public bool MigrateData { get; set; } = true;
        public bool MigrateStoredProcedures { get; set; } = true;
        public bool MigrateFunctions { get; set; } = true;
        public bool MigrateTriggers { get; set; } = true;
        public bool MigrateViews { get; set; } = true;
        public bool MigrateSequences { get; set; } = true;

        public List<string> IncludedTables { get; set; } = new();
        public List<string> ExcludedTables { get; set; } = new();
        public List<string> TablePatterns { get; set; } = new();

        public int BatchSize { get; set; } = 10000;
        public int MaxDegreeOfParallelism { get; set; } = 4;
        public int CommandTimeout { get; set; } = 600;
        public bool ContinueOnError { get; set; } = false;

        public string OutputDirectory { get; set; } = "./migration_output";
        public bool GenerateScripts { get; set; } = true;
        public bool ExecuteScripts { get; set; } = true;

        public LogLevel LogLevel { get; set; } = LogLevel.Information;
        public string? LogFilePath { get; set; }
    }

    public enum MigrationScope
    {
        SchemaOnly,
        DataOnly,
        Full
    }

    public enum LogLevel
    {
        Debug,
        Information,
        Warning,
        Error
    }
}
