using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OracleToSQL.Core.Models;

namespace OracleToSQL.Core.Interfaces
{
    /// <summary>
    /// Interface for analyzing database schemas
    /// </summary>
    public interface ISchemaAnalyzer
    {
        /// <summary>
        /// Analyzes the complete schema of a database
        /// </summary>
        Task<DatabaseSchema> AnalyzeSchemaAsync(
            string connectionString,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all tables from the database
        /// </summary>
        Task<List<TableMetadata>> GetTablesAsync(
            string connectionString,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets metadata for a specific table
        /// </summary>
        Task<TableMetadata> GetTableMetadataAsync(
            string connectionString,
            string tableName,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all indexes for a table
        /// </summary>
        Task<List<IndexMetadata>> GetIndexesAsync(
            string connectionString,
            string tableName,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all constraints for a table
        /// </summary>
        Task<List<ConstraintMetadata>> GetConstraintsAsync(
            string connectionString,
            string tableName,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all views from the database
        /// </summary>
        Task<List<ViewMetadata>> GetViewsAsync(
            string connectionString,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all sequences from the database
        /// </summary>
        Task<List<SequenceMetadata>> GetSequencesAsync(
            string connectionString,
            CancellationToken cancellationToken = default);
    }
}
