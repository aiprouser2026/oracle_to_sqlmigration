using System.Collections.Generic;

namespace OracleToSQL.Core.Models
{
    /// <summary>
    /// Represents metadata for a database table
    /// </summary>
    public class TableMetadata
    {
        public string SchemaName { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public string FullName => $"{SchemaName}.{TableName}";

        public List<ColumnMetadata> Columns { get; set; } = new();
        public List<IndexMetadata> Indexes { get; set; } = new();
        public List<ConstraintMetadata> Constraints { get; set; } = new();
        public List<ForeignKeyMetadata> ForeignKeys { get; set; } = new();

        public long RowCount { get; set; }
        public long SizeBytes { get; set; }
        public bool IsPartitioned { get; set; }
        public string? PartitioningType { get; set; }
        public string? Tablespace { get; set; }

        public Dictionary<string, object> AdditionalProperties { get; set; } = new();
    }
}
