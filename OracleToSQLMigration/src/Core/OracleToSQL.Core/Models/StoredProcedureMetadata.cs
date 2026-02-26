using System.Collections.Generic;

namespace OracleToSQL.Core.Models
{
    /// <summary>
    /// Represents metadata for a stored procedure
    /// </summary>
    public class StoredProcedureMetadata
    {
        public string SchemaName { get; set; } = string.Empty;
        public string ProcedureName { get; set; } = string.Empty;
        public string FullName => $"{SchemaName}.{ProcedureName}";
        public string SourceCode { get; set; } = string.Empty;
        public List<ParameterMetadata> Parameters { get; set; } = new();
        public string? ConvertedCode { get; set; }
        public List<string> ConversionWarnings { get; set; } = new();
    }
}
