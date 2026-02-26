using System.Collections.Generic;

namespace OracleToSQL.Core.Models
{
    /// <summary>
    /// Represents metadata for an Oracle package
    /// </summary>
    public class PackageMetadata
    {
        public string SchemaName { get; set; } = string.Empty;
        public string PackageName { get; set; } = string.Empty;
        public string FullName => $"{SchemaName}.{PackageName}";
        public string SpecificationCode { get; set; } = string.Empty;
        public string BodyCode { get; set; } = string.Empty;
        public List<StoredProcedureMetadata> Procedures { get; set; } = new();
        public List<FunctionMetadata> Functions { get; set; } = new();
        public string? ConversionStrategy { get; set; }
        public List<string> ConversionWarnings { get; set; } = new();
    }
}
