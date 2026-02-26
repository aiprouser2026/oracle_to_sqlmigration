using System.Collections.Generic;

namespace OracleToSQL.Core.Models
{
    /// <summary>
    /// Represents metadata for a database constraint
    /// </summary>
    public class ConstraintMetadata
    {
        public string ConstraintName { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public ConstraintType ConstraintType { get; set; }
        public List<string> Columns { get; set; } = new();
        public string? CheckCondition { get; set; }
        public bool IsEnabled { get; set; } = true;
        public bool IsValidated { get; set; } = true;
    }

    public enum ConstraintType
    {
        PrimaryKey,
        ForeignKey,
        Unique,
        Check,
        NotNull,
        Default
    }
}
