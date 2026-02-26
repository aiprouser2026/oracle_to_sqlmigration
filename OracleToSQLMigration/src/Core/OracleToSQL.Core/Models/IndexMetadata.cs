using System.Collections.Generic;

namespace OracleToSQL.Core.Models
{
    /// <summary>
    /// Represents metadata for a database index
    /// </summary>
    public class IndexMetadata
    {
        public string IndexName { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public IndexType IndexType { get; set; }
        public bool IsUnique { get; set; }
        public bool IsPrimaryKey { get; set; }
        public List<string> Columns { get; set; } = new();
        public List<string> IncludedColumns { get; set; } = new();
        public string? FilterCondition { get; set; }
        public string? Tablespace { get; set; }

        // Oracle-specific
        public bool IsBitmap { get; set; }
        public bool IsFunctionBased { get; set; }
        public string? FunctionExpression { get; set; }

        public Dictionary<string, object> AdditionalProperties { get; set; } = new();
    }

    public enum IndexType
    {
        BTree,
        Bitmap,
        FunctionBased,
        Unique,
        FullText,
        Spatial,
        Hash
    }
}
