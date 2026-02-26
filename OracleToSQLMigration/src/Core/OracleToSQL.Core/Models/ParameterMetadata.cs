namespace OracleToSQL.Core.Models
{
    /// <summary>
    /// Represents metadata for a procedure/function parameter
    /// </summary>
    public class ParameterMetadata
    {
        public string ParameterName { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public string Direction { get; set; } = "IN"; // IN, OUT, INOUT
        public string? DefaultValue { get; set; }
        public int Position { get; set; }
    }
}
