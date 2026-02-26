namespace OracleToSQL.Core.Models
{
    /// <summary>
    /// Represents metadata for a database sequence
    /// </summary>
    public class SequenceMetadata
    {
        public string SequenceName { get; set; } = string.Empty;
        public long CurrentValue { get; set; }
        public long IncrementBy { get; set; } = 1;
        public long MinValue { get; set; }
        public long MaxValue { get; set; }
        public bool IsCyclic { get; set; }
        public int CacheSize { get; set; }
        public string? TargetTable { get; set; }
        public string? TargetColumn { get; set; }
    }
}
