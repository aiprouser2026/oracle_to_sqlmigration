using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using OracleToSQL.Core.Interfaces;
using OracleToSQL.Core.Models;
using Serilog;

namespace OracleToSQL.SchemaAnalyzer
{
    /// <summary>
    /// Analyzes Oracle database schemas and extracts metadata
    /// </summary>
    public class OracleSchemaAnalyzer : ISchemaAnalyzer
    {
        private readonly ILogger _logger;

        public OracleSchemaAnalyzer(ILogger? logger = null)
        {
            _logger = logger ?? Log.Logger;
        }

        /// <summary>
        /// Analyzes the complete schema of an Oracle database
        /// </summary>
        public async Task<DatabaseSchema> AnalyzeSchemaAsync(
            string connectionString,
            CancellationToken cancellationToken = default)
        {
            _logger.Information("Starting Oracle schema analysis...");

            var schema = new DatabaseSchema();

            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync(cancellationToken);

                // Get the current schema name
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA') FROM DUAL";
                    schema.SchemaName = (await cmd.ExecuteScalarAsync(cancellationToken))?.ToString() ?? "Unknown";
                }

                // Get database name
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT name FROM v$database";
                    schema.DatabaseName = (await cmd.ExecuteScalarAsync(cancellationToken))?.ToString() ?? "Unknown";
                }

                _logger.Information("Analyzing schema: {SchemaName} in database: {DatabaseName}",
                    schema.SchemaName, schema.DatabaseName);

                // Analyze different database objects
                schema.Tables = await GetTablesAsync(connectionString, cancellationToken);
                _logger.Information("Found {Count} tables", schema.Tables.Count);

                schema.Views = await GetViewsAsync(connectionString, cancellationToken);
                _logger.Information("Found {Count} views", schema.Views.Count);

                schema.Sequences = await GetSequencesAsync(connectionString, cancellationToken);
                _logger.Information("Found {Count} sequences", schema.Sequences.Count);

                schema.StoredProcedures = await GetStoredProceduresAsync(connectionString, cancellationToken);
                _logger.Information("Found {Count} stored procedures", schema.StoredProcedures.Count);

                schema.Functions = await GetFunctionsAsync(connectionString, cancellationToken);
                _logger.Information("Found {Count} functions", schema.Functions.Count);

                schema.Triggers = await GetTriggersAsync(connectionString, cancellationToken);
                _logger.Information("Found {Count} triggers", schema.Triggers.Count);

                schema.Packages = await GetPackagesAsync(connectionString, cancellationToken);
                _logger.Information("Found {Count} packages", schema.Packages.Count);

                // Calculate total size
                schema.TotalSizeBytes = schema.Tables.Sum(t => t.SizeBytes);

                _logger.Information("Schema analysis complete. Total objects: {Count}, Total size: {Size} MB",
                    schema.TotalObjectCount, schema.TotalSizeBytes / 1024 / 1024);

                return schema;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to analyze Oracle schema");
                throw;
            }
        }

        /// <summary>
        /// Gets all tables from the Oracle database
        /// </summary>
        public async Task<List<TableMetadata>> GetTablesAsync(
            string connectionString,
            CancellationToken cancellationToken = default)
        {
            var tables = new List<TableMetadata>();

            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync(cancellationToken);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT
                        owner,
                        table_name,
                        num_rows,
                        blocks * 8192 as size_bytes,
                        partitioned,
                        tablespace_name
                    FROM all_tables
                    WHERE owner = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                    ORDER BY table_name";

                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var table = new TableMetadata
                    {
                        SchemaName = reader.GetString(0),
                        TableName = reader.GetString(1),
                        RowCount = reader.IsDBNull(2) ? 0 : reader.GetInt64(2),
                        SizeBytes = reader.IsDBNull(3) ? 0 : reader.GetInt64(3),
                        IsPartitioned = reader.GetString(4) == "YES",
                        Tablespace = reader.IsDBNull(5) ? null : reader.GetString(5)
                    };

                    // Get detailed metadata for each table
                    table.Columns = await GetColumnsAsync(connectionString, table.TableName, cancellationToken);
                    table.Indexes = await GetIndexesAsync(connectionString, table.TableName, cancellationToken);
                    table.Constraints = await GetConstraintsAsync(connectionString, table.TableName, cancellationToken);
                    table.ForeignKeys = await GetForeignKeysAsync(connectionString, table.TableName, cancellationToken);

                    tables.Add(table);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get tables");
                throw;
            }

            return tables;
        }

        /// <summary>
        /// Gets metadata for a specific table
        /// </summary>
        public async Task<TableMetadata> GetTableMetadataAsync(
            string connectionString,
            string tableName,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync(cancellationToken);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT
                        owner,
                        table_name,
                        num_rows,
                        blocks * 8192 as size_bytes,
                        partitioned,
                        tablespace_name
                    FROM all_tables
                    WHERE owner = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND table_name = :tableName";
                cmd.Parameters.Add("tableName", OracleDbType.Varchar2).Value = tableName.ToUpper();

                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    var table = new TableMetadata
                    {
                        SchemaName = reader.GetString(0),
                        TableName = reader.GetString(1),
                        RowCount = reader.IsDBNull(2) ? 0 : reader.GetInt64(2),
                        SizeBytes = reader.IsDBNull(3) ? 0 : reader.GetInt64(3),
                        IsPartitioned = reader.GetString(4) == "YES",
                        Tablespace = reader.IsDBNull(5) ? null : reader.GetString(5)
                    };

                    table.Columns = await GetColumnsAsync(connectionString, tableName, cancellationToken);
                    table.Indexes = await GetIndexesAsync(connectionString, tableName, cancellationToken);
                    table.Constraints = await GetConstraintsAsync(connectionString, tableName, cancellationToken);
                    table.ForeignKeys = await GetForeignKeysAsync(connectionString, tableName, cancellationToken);

                    return table;
                }

                throw new Exception($"Table '{tableName}' not found");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get table metadata for {TableName}", tableName);
                throw;
            }
        }

        /// <summary>
        /// Gets all columns for a table
        /// </summary>
        private async Task<List<ColumnMetadata>> GetColumnsAsync(
            string connectionString,
            string tableName,
            CancellationToken cancellationToken = default)
        {
            var columns = new List<ColumnMetadata>();

            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync(cancellationToken);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT
                        column_name,
                        data_type,
                        data_length,
                        data_precision,
                        data_scale,
                        nullable,
                        data_default,
                        column_id,
                        virtual_column
                    FROM all_tab_columns
                    WHERE owner = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND table_name = :tableName
                    ORDER BY column_id";
                cmd.Parameters.Add("tableName", OracleDbType.Varchar2).Value = tableName.ToUpper();

                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var column = new ColumnMetadata
                    {
                        ColumnName = reader.GetString(0),
                        DataType = reader.GetString(1),
                        MaxLength = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                        Precision = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                        Scale = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                        IsNullable = reader.GetString(5) == "Y",
                        DefaultValue = reader.IsDBNull(6) ? null : reader.GetString(6),
                        OrdinalPosition = reader.GetInt32(7),
                        IsVirtual = reader.GetString(8) == "YES"
                    };

                    columns.Add(column);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get columns for table {TableName}", tableName);
            }

            return columns;
        }

        /// <summary>
        /// Gets all indexes for a table
        /// </summary>
        public async Task<List<IndexMetadata>> GetIndexesAsync(
            string connectionString,
            string tableName,
            CancellationToken cancellationToken = default)
        {
            var indexes = new List<IndexMetadata>();

            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync(cancellationToken);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT
                        i.index_name,
                        i.index_type,
                        i.uniqueness,
                        i.tablespace_name
                    FROM all_indexes i
                    WHERE i.owner = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND i.table_name = :tableName
                    ORDER BY i.index_name";
                cmd.Parameters.Add("tableName", OracleDbType.Varchar2).Value = tableName.ToUpper();

                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var indexName = reader.GetString(0);
                    var indexType = reader.GetString(1);
                    var uniqueness = reader.GetString(2);

                    var index = new IndexMetadata
                    {
                        IndexName = indexName,
                        TableName = tableName,
                        IsUnique = uniqueness == "UNIQUE",
                        IsBitmap = indexType.Contains("BITMAP"),
                        IsFunctionBased = indexType.Contains("FUNCTION-BASED"),
                        Tablespace = reader.IsDBNull(3) ? null : reader.GetString(3)
                    };

                    // Determine index type
                    if (index.IsBitmap)
                        index.IndexType = IndexType.Bitmap;
                    else if (index.IsFunctionBased)
                        index.IndexType = IndexType.FunctionBased;
                    else if (index.IsUnique)
                        index.IndexType = IndexType.Unique;
                    else
                        index.IndexType = IndexType.BTree;

                    // Get index columns
                    index.Columns = await GetIndexColumnsAsync(connectionString, indexName, cancellationToken);

                    indexes.Add(index);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get indexes for table {TableName}", tableName);
            }

            return indexes;
        }

        /// <summary>
        /// Gets columns for an index
        /// </summary>
        private async Task<List<string>> GetIndexColumnsAsync(
            string connectionString,
            string indexName,
            CancellationToken cancellationToken = default)
        {
            var columns = new List<string>();

            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync(cancellationToken);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT column_name
                    FROM all_ind_columns
                    WHERE index_owner = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND index_name = :indexName
                    ORDER BY column_position";
                cmd.Parameters.Add("indexName", OracleDbType.Varchar2).Value = indexName;

                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    columns.Add(reader.GetString(0));
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get columns for index {IndexName}", indexName);
            }

            return columns;
        }

        /// <summary>
        /// Gets all constraints for a table
        /// </summary>
        public async Task<List<ConstraintMetadata>> GetConstraintsAsync(
            string connectionString,
            string tableName,
            CancellationToken cancellationToken = default)
        {
            var constraints = new List<ConstraintMetadata>();

            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync(cancellationToken);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT
                        constraint_name,
                        constraint_type,
                        search_condition,
                        status,
                        validated
                    FROM all_constraints
                    WHERE owner = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND table_name = :tableName
                      AND constraint_type IN ('P', 'U', 'C')
                    ORDER BY constraint_name";
                cmd.Parameters.Add("tableName", OracleDbType.Varchar2).Value = tableName.ToUpper();

                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var constraintName = reader.GetString(0);
                    var constraintType = reader.GetString(1);

                    var constraint = new ConstraintMetadata
                    {
                        ConstraintName = constraintName,
                        TableName = tableName,
                        ConstraintType = constraintType switch
                        {
                            "P" => ConstraintType.PrimaryKey,
                            "U" => ConstraintType.Unique,
                            "C" => ConstraintType.Check,
                            _ => ConstraintType.Check
                        },
                        CheckCondition = reader.IsDBNull(2) ? null : reader.GetString(2),
                        IsEnabled = reader.GetString(3) == "ENABLED",
                        IsValidated = reader.GetString(4) == "VALIDATED"
                    };

                    // Get constraint columns
                    constraint.Columns = await GetConstraintColumnsAsync(connectionString, constraintName, cancellationToken);

                    constraints.Add(constraint);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get constraints for table {TableName}", tableName);
            }

            return constraints;
        }

        /// <summary>
        /// Gets columns for a constraint
        /// </summary>
        private async Task<List<string>> GetConstraintColumnsAsync(
            string connectionString,
            string constraintName,
            CancellationToken cancellationToken = default)
        {
            var columns = new List<string>();

            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync(cancellationToken);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT column_name
                    FROM all_cons_columns
                    WHERE owner = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND constraint_name = :constraintName
                    ORDER BY position";
                cmd.Parameters.Add("constraintName", OracleDbType.Varchar2).Value = constraintName;

                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    columns.Add(reader.GetString(0));
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get columns for constraint {ConstraintName}", constraintName);
            }

            return columns;
        }

        /// <summary>
        /// Gets all foreign keys for a table
        /// </summary>
        private async Task<List<ForeignKeyMetadata>> GetForeignKeysAsync(
            string connectionString,
            string tableName,
            CancellationToken cancellationToken = default)
        {
            var foreignKeys = new List<ForeignKeyMetadata>();

            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync(cancellationToken);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT
                        c.constraint_name,
                        c.r_constraint_name,
                        c.delete_rule,
                        c.status,
                        rc.table_name as referenced_table
                    FROM all_constraints c
                    JOIN all_constraints rc ON c.r_constraint_name = rc.constraint_name AND c.r_owner = rc.owner
                    WHERE c.owner = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND c.table_name = :tableName
                      AND c.constraint_type = 'R'
                    ORDER BY c.constraint_name";
                cmd.Parameters.Add("tableName", OracleDbType.Varchar2).Value = tableName.ToUpper();

                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var fkName = reader.GetString(0);

                    var foreignKey = new ForeignKeyMetadata
                    {
                        ForeignKeyName = fkName,
                        FromTable = tableName,
                        ToTable = reader.GetString(4),
                        OnDeleteAction = reader.GetString(2),
                        IsEnabled = reader.GetString(3) == "ENABLED"
                    };

                    // Get FK columns
                    foreignKey.FromColumns = await GetConstraintColumnsAsync(connectionString, fkName, cancellationToken);
                    foreignKey.ToColumns = await GetConstraintColumnsAsync(connectionString, reader.GetString(1), cancellationToken);

                    foreignKeys.Add(foreignKey);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get foreign keys for table {TableName}", tableName);
            }

            return foreignKeys;
        }

        /// <summary>
        /// Gets all views from the database
        /// </summary>
        public async Task<List<ViewMetadata>> GetViewsAsync(
            string connectionString,
            CancellationToken cancellationToken = default)
        {
            var views = new List<ViewMetadata>();

            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync(cancellationToken);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT view_name, text
                    FROM all_views
                    WHERE owner = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                    ORDER BY view_name";

                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var view = new ViewMetadata
                    {
                        SchemaName = await GetCurrentSchemaAsync(connectionString, cancellationToken),
                        ViewName = reader.GetString(0),
                        Definition = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        IsMaterialized = false
                    };

                    views.Add(view);
                }

                // Get materialized views
                cmd.CommandText = @"
                    SELECT mview_name, query
                    FROM all_mviews
                    WHERE owner = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                    ORDER BY mview_name";

                using var mviewReader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await mviewReader.ReadAsync(cancellationToken))
                {
                    var view = new ViewMetadata
                    {
                        SchemaName = await GetCurrentSchemaAsync(connectionString, cancellationToken),
                        ViewName = mviewReader.GetString(0),
                        Definition = mviewReader.IsDBNull(1) ? string.Empty : mviewReader.GetString(1),
                        IsMaterialized = true
                    };

                    views.Add(view);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get views");
            }

            return views;
        }

        /// <summary>
        /// Gets all sequences from the database
        /// </summary>
        public async Task<List<SequenceMetadata>> GetSequencesAsync(
            string connectionString,
            CancellationToken cancellationToken = default)
        {
            var sequences = new List<SequenceMetadata>();

            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync(cancellationToken);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT
                        sequence_name,
                        last_number,
                        increment_by,
                        min_value,
                        max_value,
                        cycle_flag,
                        cache_size
                    FROM all_sequences
                    WHERE sequence_owner = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                    ORDER BY sequence_name";

                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var sequence = new SequenceMetadata
                    {
                        SequenceName = reader.GetString(0),
                        CurrentValue = reader.GetInt64(1),
                        IncrementBy = reader.GetInt64(2),
                        MinValue = reader.GetInt64(3),
                        MaxValue = reader.GetInt64(4),
                        IsCyclic = reader.GetString(5) == "Y",
                        CacheSize = reader.GetInt32(6)
                    };

                    sequences.Add(sequence);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get sequences");
            }

            return sequences;
        }

        /// <summary>
        /// Gets all stored procedures from the database
        /// </summary>
        private async Task<List<StoredProcedureMetadata>> GetStoredProceduresAsync(
            string connectionString,
            CancellationToken cancellationToken = default)
        {
            var procedures = new List<StoredProcedureMetadata>();

            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync(cancellationToken);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT object_name
                    FROM all_objects
                    WHERE owner = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND object_type = 'PROCEDURE'
                    ORDER BY object_name";

                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var procName = reader.GetString(0);

                    var procedure = new StoredProcedureMetadata
                    {
                        SchemaName = await GetCurrentSchemaAsync(connectionString, cancellationToken),
                        ProcedureName = procName,
                        SourceCode = await GetSourceCodeAsync(connectionString, procName, "PROCEDURE", cancellationToken)
                    };

                    procedures.Add(procedure);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get stored procedures");
            }

            return procedures;
        }

        /// <summary>
        /// Gets all functions from the database
        /// </summary>
        private async Task<List<FunctionMetadata>> GetFunctionsAsync(
            string connectionString,
            CancellationToken cancellationToken = default)
        {
            var functions = new List<FunctionMetadata>();

            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync(cancellationToken);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT object_name
                    FROM all_objects
                    WHERE owner = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND object_type = 'FUNCTION'
                    ORDER BY object_name";

                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var funcName = reader.GetString(0);

                    var function = new FunctionMetadata
                    {
                        SchemaName = await GetCurrentSchemaAsync(connectionString, cancellationToken),
                        FunctionName = funcName,
                        SourceCode = await GetSourceCodeAsync(connectionString, funcName, "FUNCTION", cancellationToken)
                    };

                    functions.Add(function);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get functions");
            }

            return functions;
        }

        /// <summary>
        /// Gets all triggers from the database
        /// </summary>
        private async Task<List<TriggerMetadata>> GetTriggersAsync(
            string connectionString,
            CancellationToken cancellationToken = default)
        {
            var triggers = new List<TriggerMetadata>();

            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync(cancellationToken);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT
                        trigger_name,
                        table_name,
                        trigger_type,
                        triggering_event,
                        status
                    FROM all_triggers
                    WHERE owner = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                    ORDER BY trigger_name";

                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var triggerName = reader.GetString(0);

                    var trigger = new TriggerMetadata
                    {
                        TriggerName = triggerName,
                        TableName = reader.GetString(1),
                        TriggerType = reader.GetString(2),
                        TriggerEvent = reader.GetString(3),
                        IsEnabled = reader.GetString(4) == "ENABLED",
                        SourceCode = await GetSourceCodeAsync(connectionString, triggerName, "TRIGGER", cancellationToken)
                    };

                    triggers.Add(trigger);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get triggers");
            }

            return triggers;
        }

        /// <summary>
        /// Gets all packages from the database
        /// </summary>
        private async Task<List<PackageMetadata>> GetPackagesAsync(
            string connectionString,
            CancellationToken cancellationToken = default)
        {
            var packages = new List<PackageMetadata>();

            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync(cancellationToken);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT DISTINCT object_name
                    FROM all_objects
                    WHERE owner = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND object_type IN ('PACKAGE', 'PACKAGE BODY')
                    ORDER BY object_name";

                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var packageName = reader.GetString(0);

                    var package = new PackageMetadata
                    {
                        SchemaName = await GetCurrentSchemaAsync(connectionString, cancellationToken),
                        PackageName = packageName,
                        SpecificationCode = await GetSourceCodeAsync(connectionString, packageName, "PACKAGE", cancellationToken),
                        BodyCode = await GetSourceCodeAsync(connectionString, packageName, "PACKAGE BODY", cancellationToken)
                    };

                    packages.Add(package);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get packages");
            }

            return packages;
        }

        /// <summary>
        /// Gets source code for a database object
        /// </summary>
        private async Task<string> GetSourceCodeAsync(
            string connectionString,
            string objectName,
            string objectType,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var connection = new OracleConnection(connectionString);
                await connection.OpenAsync(cancellationToken);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT text
                    FROM all_source
                    WHERE owner = SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA')
                      AND name = :objectName
                      AND type = :objectType
                    ORDER BY line";
                cmd.Parameters.Add("objectName", OracleDbType.Varchar2).Value = objectName.ToUpper();
                cmd.Parameters.Add("objectType", OracleDbType.Varchar2).Value = objectType.ToUpper();

                var sourceCode = new System.Text.StringBuilder();
                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    sourceCode.Append(reader.GetString(0));
                }

                return sourceCode.ToString();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get source code for {ObjectType} {ObjectName}", objectType, objectName);
                return string.Empty;
            }
        }

        /// <summary>
        /// Gets the current schema name
        /// </summary>
        private async Task<string> GetCurrentSchemaAsync(string connectionString, CancellationToken cancellationToken = default)
        {
            using var connection = new OracleConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA') FROM DUAL";

            return (await cmd.ExecuteScalarAsync(cancellationToken))?.ToString() ?? "Unknown";
        }
    }
}
