using System.Collections.Generic;

namespace OracleToSQL.Core.Models
{
    /// <summary>
    /// Represents metadata for a foreign key relationship
    /// </summary>
    public class ForeignKeyMetadata
    {
        public string ForeignKeyName { get; set; } = string.Empty;
        public string FromTable { get; set; } = string.Empty;
        public List<string> FromColumns { get; set; } = new();
        public string ToTable { get; set; } = string.Empty;
        public List<string> ToColumns { get; set; } = new();
        public string OnDeleteAction { get; set; } = "NO ACTION";
        public string OnUpdateAction { get; set; } = "NO ACTION";
        public bool IsEnabled { get; set; } = true;
    }
}
