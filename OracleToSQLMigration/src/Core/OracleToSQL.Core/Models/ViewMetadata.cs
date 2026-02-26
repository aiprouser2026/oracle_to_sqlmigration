namespace OracleToSQL.Core.Models
{
    /// <summary>
    /// Represents metadata for a database view
    /// </summary>
    public class ViewMetadata
    {
        public string SchemaName { get; set; } = string.Empty;
        public string ViewName { get; set; } = string.Empty;
        public string FullName => $"{SchemaName}.{ViewName}";
        public string Definition { get; set; } = string.Empty;
        public bool IsMaterialized { get; set; }
        public string? RefreshMethod { get; set; }
        public string? ConvertedDefinition { get; set; }
    }
}
