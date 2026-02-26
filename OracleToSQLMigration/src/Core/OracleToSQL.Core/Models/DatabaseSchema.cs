using System.Collections.Generic;

namespace OracleToSQL.Core.Models
{
    /// <summary>
    /// Represents the complete schema of a database
    /// </summary>
    public class DatabaseSchema
    {
        public string SchemaName { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
        public List<TableMetadata> Tables { get; set; } = new();
        public List<ViewMetadata> Views { get; set; } = new();
        public List<SequenceMetadata> Sequences { get; set; } = new();
        public List<StoredProcedureMetadata> StoredProcedures { get; set; } = new();
        public List<FunctionMetadata> Functions { get; set; } = new();
        public List<TriggerMetadata> Triggers { get; set; } = new();
        public List<PackageMetadata> Packages { get; set; } = new();
        public Dictionary<string, string> Metadata { get; set; } = new();

        public long TotalSizeBytes { get; set; }
        public int TotalObjectCount => Tables.Count + Views.Count + StoredProcedures.Count +
                                       Functions.Count + Triggers.Count + Packages.Count;
    }
}
