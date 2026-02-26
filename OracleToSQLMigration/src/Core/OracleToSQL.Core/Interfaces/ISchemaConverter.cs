using System.Collections.Generic;
using System.Threading.Tasks;
using OracleToSQL.Core.Models;

namespace OracleToSQL.Core.Interfaces
{
    /// <summary>
    /// Interface for converting Oracle schema to SQL Server schema
    /// </summary>
    public interface ISchemaConverter
    {
        /// <summary>
        /// Converts Oracle data type to SQL Server data type
        /// </summary>
        string ConvertDataType(string oracleType, int? length = null, int? precision = null, int? scale = null);

        /// <summary>
        /// Converts table metadata from Oracle to SQL Server format
        /// </summary>
        TableMetadata ConvertTable(TableMetadata oracleTable);

        /// <summary>
        /// Generates SQL Server DDL script for a table
        /// </summary>
        string GenerateCreateTableScript(TableMetadata table);

        /// <summary>
        /// Generates SQL Server DDL script for indexes
        /// </summary>
        string GenerateCreateIndexScript(IndexMetadata index, string tableName);

        /// <summary>
        /// Generates SQL Server DDL script for constraints
        /// </summary>
        string GenerateConstraintScript(ConstraintMetadata constraint, string tableName);

        /// <summary>
        /// Generates SQL Server DDL script for foreign keys
        /// </summary>
        string GenerateForeignKeyScript(ForeignKeyMetadata foreignKey);

        /// <summary>
        /// Converts Oracle sequence to SQL Server IDENTITY or SEQUENCE
        /// </summary>
        string ConvertSequence(SequenceMetadata sequence);

        /// <summary>
        /// Generates complete migration scripts for a database schema
        /// </summary>
        Task<MigrationScripts> GenerateMigrationScriptsAsync(DatabaseSchema oracleSchema);
    }

    /// <summary>
    /// Container for migration scripts
    /// </summary>
    public class MigrationScripts
    {
        public List<string> PreMigrationScripts { get; set; } = new();
        public List<string> SchemaCreationScripts { get; set; } = new();
        public List<string> IndexCreationScripts { get; set; } = new();
        public List<string> ConstraintCreationScripts { get; set; } = new();
        public List<string> PostMigrationScripts { get; set; } = new();
        public Dictionary<string, string> Warnings { get; set; } = new();
    }
}
