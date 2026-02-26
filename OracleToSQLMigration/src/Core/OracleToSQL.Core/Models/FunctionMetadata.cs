using System.Collections.Generic;

namespace OracleToSQL.Core.Models
{
    /// <summary>
    /// Represents metadata for a database function
    /// </summary>
    public class FunctionMetadata
    {
        public string SchemaName { get; set; } = string.Empty;
        public string FunctionName { get; set; } = string.Empty;
        public string FullName => $"{SchemaName}.{FunctionName}";
        public string SourceCode { get; set; } = string.Empty;
        public string ReturnType { get; set; } = string.Empty;
        public List<ParameterMetadata> Parameters { get; set; } = new();
        public string? ConvertedCode { get; set; }
        public List<string> ConversionWarnings { get; set; } = new();
    }
}
