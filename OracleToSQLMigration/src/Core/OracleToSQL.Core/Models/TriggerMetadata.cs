using System.Collections.Generic;

namespace OracleToSQL.Core.Models
{
    /// <summary>
    /// Represents metadata for a database trigger
    /// </summary>
    public class TriggerMetadata
    {
        public string TriggerName { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public string TriggerType { get; set; } = string.Empty;
        public string TriggerEvent { get; set; } = string.Empty;
        public string SourceCode { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public string? ConvertedCode { get; set; }
        public List<string> ConversionWarnings { get; set; } = new();
    }
}
