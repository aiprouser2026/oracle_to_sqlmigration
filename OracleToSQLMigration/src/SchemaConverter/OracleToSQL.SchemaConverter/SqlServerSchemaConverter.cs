using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OracleToSQL.Core.Interfaces;
using OracleToSQL.Core.Models;
using Serilog;

namespace OracleToSQL.SchemaConverter
{
    /// <summary>
    /// Converts Oracle schema to SQL Server schema
    /// </summary>
    public class SqlServerSchemaConverter : ISchemaConverter
    {
        private readonly DataTypeMapper _typeMapper;
        private readonly ILogger _logger;

        public SqlServerSchemaConverter(ILogger? logger = null)
        {
            _typeMapper = new DataTypeMapper();
            _logger = logger ?? Log.Logger;
        }

        /// <summary>
        /// Converts Oracle data type to SQL Server data type
        /// </summary>
        public string ConvertDataType(string oracleType, int? length = null, int? precision = null, int? scale = null)
        {
            return _typeMapper.MapDataType(oracleType, length, precision, scale);
        }

        /// <summary>
        /// Converts table metadata from Oracle to SQL Server format
        /// </summary>
        public TableMetadata ConvertTable(TableMetadata oracleTable)
        {
            _logger.Debug("Converting table: {TableName}", oracleTable.TableName);

            var convertedTable = new TableMetadata
            {
                SchemaName = oracleTable.SchemaName,
                TableName = oracleTable.TableName,
                RowCount = oracleTable.RowCount,
                SizeBytes = oracleTable.SizeBytes,
                IsPartitioned = oracleTable.IsPartitioned
            };

            // Convert columns
            foreach (var column in oracleTable.Columns)
            {
                var convertedColumn = new ColumnMetadata
                {
                    ColumnName = column.ColumnName,
                    DataType = column.DataType,
                    TargetDataType = _typeMapper.MapDataType(
                        column.DataType,
                        column.MaxLength,
                        column.Precision,
                        column.Scale),
                    MaxLength = column.MaxLength,
                    Precision = column.Precision,
                    Scale = column.Scale,
                    IsNullable = column.IsNullable,
                    DefaultValue = ConvertDefaultValue(column.DefaultValue),
                    IsIdentity = column.IsIdentity,
                    IsPrimaryKey = column.IsPrimaryKey,
                    IsForeignKey = column.IsForeignKey,
                    OrdinalPosition = column.OrdinalPosition,
                    ConversionNotes = _typeMapper.GetConversionNotes(column.DataType)
                };

                convertedTable.Columns.Add(convertedColumn);
            }

            // Copy indexes, constraints, and foreign keys (will be converted separately)
            convertedTable.Indexes = oracleTable.Indexes;
            convertedTable.Constraints = oracleTable.Constraints;
            convertedTable.ForeignKeys = oracleTable.ForeignKeys;

            return convertedTable;
        }

        /// <summary>
        /// Generates SQL Server DDL script for creating a table
        /// </summary>
        public string GenerateCreateTableScript(TableMetadata table)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"-- Create table {table.FullName}");
            sb.AppendLine($"CREATE TABLE [{table.SchemaName}].[{table.TableName}] (");

            var columnDefinitions = new List<string>();

            foreach (var column in table.Columns.OrderBy(c => c.OrdinalPosition))
            {
                var colDef = new StringBuilder();
                colDef.Append($"    [{column.ColumnName}] ");

                // Use target data type if available, otherwise convert
                var dataType = !string.IsNullOrEmpty(column.TargetDataType)
                    ? column.TargetDataType
                    : _typeMapper.MapDataType(column.DataType, column.MaxLength, column.Precision, column.Scale);

                colDef.Append(dataType);

                // Identity column
                if (column.IsIdentity)
                {
                    colDef.Append(" IDENTITY(1,1)");
                }

                // Nullable
                colDef.Append(column.IsNullable ? " NULL" : " NOT NULL");

                // Default value
                if (!string.IsNullOrEmpty(column.DefaultValue))
                {
                    colDef.Append($" DEFAULT {column.DefaultValue}");
                }

                columnDefinitions.Add(colDef.ToString());
            }

            sb.AppendLine(string.Join(",\n", columnDefinitions));

            // Add primary key constraint inline if exists
            var pkConstraint = table.Constraints.FirstOrDefault(c => c.ConstraintType == ConstraintType.PrimaryKey);
            if (pkConstraint != null)
            {
                sb.AppendLine(",");
                sb.Append($"    CONSTRAINT [{pkConstraint.ConstraintName}] PRIMARY KEY CLUSTERED (");
                sb.Append(string.Join(", ", pkConstraint.Columns.Select(c => $"[{c}]")));
                sb.AppendLine(")");
            }

            sb.AppendLine(");");
            sb.AppendLine();

            return sb.ToString();
        }

        /// <summary>
        /// Generates SQL Server DDL script for creating an index
        /// </summary>
        public string GenerateCreateIndexScript(IndexMetadata index, string tableName)
        {
            var sb = new StringBuilder();

            // Skip primary key indexes (created with table)
            if (index.IsPrimaryKey)
                return string.Empty;

            sb.Append("CREATE ");

            if (index.IsUnique)
                sb.Append("UNIQUE ");

            // Bitmap indexes are not directly supported in SQL Server
            // Convert to filtered index or regular index
            if (index.IsBitmap)
            {
                sb.AppendLine($"-- Original Oracle bitmap index: {index.IndexName}");
                sb.AppendLine("-- Note: Bitmap indexes converted to regular non-clustered index");
                sb.AppendLine("-- Consider using filtered index or columnstore for better performance");
            }

            sb.Append("NONCLUSTERED INDEX ");
            sb.AppendLine($"[{index.IndexName}]");
            sb.AppendLine($"    ON [{tableName}] (");
            sb.AppendLine($"        {string.Join(", ", index.Columns.Select(c => $"[{c}] ASC"))}");
            sb.Append("    )");

            // Add included columns if any
            if (index.IncludedColumns.Any())
            {
                sb.AppendLine();
                sb.Append($"    INCLUDE ({string.Join(", ", index.IncludedColumns.Select(c => $"[{c}]"))})");
            }

            // Add filter condition if available
            if (!string.IsNullOrEmpty(index.FilterCondition))
            {
                sb.AppendLine();
                sb.Append($"    WHERE {ConvertFilterCondition(index.FilterCondition)}");
            }

            // Function-based indexes need special handling
            if (index.IsFunctionBased)
            {
                sb.AppendLine();
                sb.AppendLine($"-- Original function-based index expression: {index.FunctionExpression}");
                sb.AppendLine("-- Manual conversion may be required");
            }

            sb.AppendLine(";");
            sb.AppendLine();

            return sb.ToString();
        }

        /// <summary>
        /// Generates SQL Server DDL script for creating a constraint
        /// </summary>
        public string GenerateConstraintScript(ConstraintMetadata constraint, string tableName)
        {
            var sb = new StringBuilder();

            switch (constraint.ConstraintType)
            {
                case ConstraintType.PrimaryKey:
                    // Primary keys are created with the table
                    return string.Empty;

                case ConstraintType.Unique:
                    sb.AppendLine($"ALTER TABLE [{tableName}]");
                    sb.AppendLine($"    ADD CONSTRAINT [{constraint.ConstraintName}] UNIQUE (");
                    sb.AppendLine($"        {string.Join(", ", constraint.Columns.Select(c => $"[{c}]"))}");
                    sb.AppendLine("    );");
                    break;

                case ConstraintType.Check:
                    sb.AppendLine($"ALTER TABLE [{tableName}]");
                    sb.Append($"    ADD CONSTRAINT [{constraint.ConstraintName}] CHECK (");
                    sb.Append(ConvertCheckCondition(constraint.CheckCondition ?? string.Empty));
                    sb.AppendLine(");");
                    break;

                case ConstraintType.NotNull:
                    // NOT NULL is handled in column definition
                    return string.Empty;

                case ConstraintType.Default:
                    // Defaults are handled in column definition
                    return string.Empty;
            }

            sb.AppendLine();
            return sb.ToString();
        }

        /// <summary>
        /// Generates SQL Server DDL script for creating a foreign key
        /// </summary>
        public string GenerateForeignKeyScript(ForeignKeyMetadata foreignKey)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"ALTER TABLE [{foreignKey.FromTable}]");
            sb.AppendLine($"    ADD CONSTRAINT [{foreignKey.ForeignKeyName}]");
            sb.AppendLine($"    FOREIGN KEY ({string.Join(", ", foreignKey.FromColumns.Select(c => $"[{c}]"))})");
            sb.AppendLine($"    REFERENCES [{foreignKey.ToTable}] ({string.Join(", ", foreignKey.ToColumns.Select(c => $"[{c}]"))})");

            // Convert delete action
            if (!string.IsNullOrEmpty(foreignKey.OnDeleteAction) && foreignKey.OnDeleteAction != "NO ACTION")
            {
                var deleteAction = foreignKey.OnDeleteAction.ToUpperInvariant() switch
                {
                    "CASCADE" => "CASCADE",
                    "SET NULL" => "SET NULL",
                    "SET DEFAULT" => "SET DEFAULT",
                    _ => "NO ACTION"
                };
                sb.AppendLine($"    ON DELETE {deleteAction}");
            }

            sb.AppendLine(";");
            sb.AppendLine();

            return sb.ToString();
        }

        /// <summary>
        /// Converts Oracle sequence to SQL Server IDENTITY or SEQUENCE
        /// </summary>
        public string ConvertSequence(SequenceMetadata sequence)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"-- Create sequence {sequence.SequenceName}");
            sb.AppendLine($"CREATE SEQUENCE [{sequence.SequenceName}]");
            sb.AppendLine($"    START WITH {sequence.CurrentValue}");
            sb.AppendLine($"    INCREMENT BY {sequence.IncrementBy}");
            sb.AppendLine($"    MINVALUE {sequence.MinValue}");
            sb.AppendLine($"    MAXVALUE {sequence.MaxValue}");

            if (sequence.IsCyclic)
                sb.AppendLine("    CYCLE");
            else
                sb.AppendLine("    NO CYCLE");

            if (sequence.CacheSize > 0)
                sb.AppendLine($"    CACHE {sequence.CacheSize}");

            sb.AppendLine(";");
            sb.AppendLine();

            return sb.ToString();
        }

        /// <summary>
        /// Generates complete migration scripts for a database schema
        /// </summary>
        public async Task<MigrationScripts> GenerateMigrationScriptsAsync(DatabaseSchema oracleSchema)
        {
            _logger.Information("Generating migration scripts for schema: {SchemaName}", oracleSchema.SchemaName);

            var scripts = new MigrationScripts();

            // Pre-migration scripts
            scripts.PreMigrationScripts.Add($"-- Migration for schema: {oracleSchema.SchemaName}");
            scripts.PreMigrationScripts.Add($"-- Database: {oracleSchema.DatabaseName}");
            scripts.PreMigrationScripts.Add($"-- Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            scripts.PreMigrationScripts.Add(string.Empty);

            // Create schema if not exists
            scripts.PreMigrationScripts.Add($@"
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = '{oracleSchema.SchemaName}')
BEGIN
    EXEC('CREATE SCHEMA [{oracleSchema.SchemaName}]')
END
GO
");

            // Create sequences
            foreach (var sequence in oracleSchema.Sequences)
            {
                scripts.SchemaCreationScripts.Add(ConvertSequence(sequence));
            }

            // Create tables (without foreign keys)
            foreach (var table in oracleSchema.Tables)
            {
                var convertedTable = ConvertTable(table);
                scripts.SchemaCreationScripts.Add(GenerateCreateTableScript(convertedTable));

                // Track any conversion warnings
                foreach (var column in convertedTable.Columns)
                {
                    if (!string.IsNullOrEmpty(column.ConversionNotes))
                    {
                        var key = $"{table.FullName}.{column.ColumnName}";
                        scripts.Warnings[key] = column.ConversionNotes;
                    }
                }
            }

            // Create indexes
            foreach (var table in oracleSchema.Tables)
            {
                foreach (var index in table.Indexes)
                {
                    var indexScript = GenerateCreateIndexScript(index, table.TableName);
                    if (!string.IsNullOrEmpty(indexScript))
                    {
                        scripts.IndexCreationScripts.Add(indexScript);
                    }
                }
            }

            // Create constraints (unique, check)
            foreach (var table in oracleSchema.Tables)
            {
                foreach (var constraint in table.Constraints)
                {
                    var constraintScript = GenerateConstraintScript(constraint, table.TableName);
                    if (!string.IsNullOrEmpty(constraintScript))
                    {
                        scripts.ConstraintCreationScripts.Add(constraintScript);
                    }
                }
            }

            // Create foreign keys
            foreach (var table in oracleSchema.Tables)
            {
                foreach (var fk in table.ForeignKeys)
                {
                    scripts.ConstraintCreationScripts.Add(GenerateForeignKeyScript(fk));
                }
            }

            // Post-migration scripts
            scripts.PostMigrationScripts.Add("-- Update statistics");
            foreach (var table in oracleSchema.Tables)
            {
                scripts.PostMigrationScripts.Add($"UPDATE STATISTICS [{table.SchemaName}].[{table.TableName}];");
            }

            _logger.Information("Migration scripts generated successfully. Tables: {TableCount}, Indexes: {IndexCount}",
                oracleSchema.Tables.Count, oracleSchema.Tables.Sum(t => t.Indexes.Count));

            return await Task.FromResult(scripts);
        }

        /// <summary>
        /// Converts Oracle default value to SQL Server format
        /// </summary>
        private string ConvertDefaultValue(string? oracleDefault)
        {
            if (string.IsNullOrEmpty(oracleDefault))
                return string.Empty;

            var defaultValue = oracleDefault.Trim();

            // Convert SYSDATE to GETDATE()
            if (defaultValue.Contains("SYSDATE", StringComparison.OrdinalIgnoreCase))
                defaultValue = defaultValue.Replace("SYSDATE", "GETDATE()", StringComparison.OrdinalIgnoreCase);

            // Convert SYSTIMESTAMP to SYSDATETIME()
            if (defaultValue.Contains("SYSTIMESTAMP", StringComparison.OrdinalIgnoreCase))
                defaultValue = defaultValue.Replace("SYSTIMESTAMP", "SYSDATETIME()", StringComparison.OrdinalIgnoreCase);

            // Convert NVL to ISNULL
            if (defaultValue.Contains("NVL", StringComparison.OrdinalIgnoreCase))
                defaultValue = defaultValue.Replace("NVL", "ISNULL", StringComparison.OrdinalIgnoreCase);

            return defaultValue;
        }

        /// <summary>
        /// Converts Oracle check condition to SQL Server format
        /// </summary>
        private string ConvertCheckCondition(string oracleCondition)
        {
            if (string.IsNullOrEmpty(oracleCondition))
                return string.Empty;

            var condition = oracleCondition;

            // Convert NVL to ISNULL
            condition = condition.Replace("NVL", "ISNULL", StringComparison.OrdinalIgnoreCase);

            // Convert DECODE to CASE (simplified - may need manual review)
            // This is a basic conversion, complex DECODE needs manual intervention

            return condition;
        }

        /// <summary>
        /// Converts Oracle filter condition to SQL Server format
        /// </summary>
        private string ConvertFilterCondition(string oracleFilter)
        {
            if (string.IsNullOrEmpty(oracleFilter))
                return string.Empty;

            var filter = oracleFilter;

            // Basic conversions
            filter = filter.Replace("NVL", "ISNULL", StringComparison.OrdinalIgnoreCase);

            return filter;
        }
    }
}
