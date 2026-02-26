namespace OracleToSQL.Core.Models
{
    /// <summary>
    /// Represents metadata for a database column
    /// </summary>
    public class ColumnMetadata
    {
        public string ColumnName { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public int? MaxLength { get; set; }
        public int? Precision { get; set; }
        public int? Scale { get; set; }
        public bool IsNullable { get; set; }
        public string? DefaultValue { get; set; }
        public bool IsIdentity { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsForeignKey { get; set; }
        public int OrdinalPosition { get; set; }
        public string? Comment { get; set; }

        // Oracle-specific
        public bool IsVirtual { get; set; }
        public string? VirtualExpression { get; set; }

        // Conversion tracking
        public string? TargetDataType { get; set; }
        public string? ConversionNotes { get; set; }
    }
}
