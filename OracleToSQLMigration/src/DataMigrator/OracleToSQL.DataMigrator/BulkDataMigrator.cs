using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Oracle.ManagedDataAccess.Client;
using OracleToSQL.Core.Interfaces;
using OracleToSQL.Core.Models;
using Serilog;

namespace OracleToSQL.DataMigrator
{
    /// <summary>
    /// High-performance bulk data migrator for Oracle to SQL Server
    /// </summary>
    public class BulkDataMigrator : IDataMigrator
    {
        private readonly ILogger _logger;

        public BulkDataMigrator(ILogger? logger = null)
        {
            _logger = logger ?? Log.Logger;
        }

        /// <summary>
        /// Migrates data for a single table
        /// </summary>
        public async Task<MigrationResult> MigrateTableAsync(
            string sourceConnectionString,
            string targetConnectionString,
            TableMetadata table,
            MigrationOptions options,
            IProgress<MigrationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new MigrationResult
            {
                TableName = table.TableName
            };

            try
            {
                _logger.Information("Starting migration for table: {TableName} ({RowCount} rows)",
                    table.TableName, table.RowCount);

                // Disable indexes if requested
                if (options.DisableIndexesDuringMigration)
                {
                    await DisableIndexesAsync(targetConnectionString, table, cancellationToken);
                }

                // Disable constraints if requested
                if (options.DisableConstraintsDuringMigration)
                {
                    await DisableConstraintsAsync(targetConnectionString, table, cancellationToken);
                }

                // Perform the actual data migration
                var rowsMigrated = await BulkCopyDataAsync(
                    sourceConnectionString,
                    targetConnectionString,
                    table,
                    options,
                    progress,
                    cancellationToken);

                result.RowsMigrated = rowsMigrated;

                // Re-enable constraints
                if (options.DisableConstraintsDuringMigration)
                {
                    await EnableConstraintsAsync(targetConnectionString, table, cancellationToken);
                }

                // Rebuild indexes
                if (options.DisableIndexesDuringMigration)
                {
                    await RebuildIndexesAsync(targetConnectionString, table, cancellationToken);
                }

                result.Success = true;
                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;

                _logger.Information("Migration completed for table: {TableName}. Rows: {RowCount}, Duration: {Duration}",
                    table.TableName, rowsMigrated, result.Duration);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.Exception = ex;

                _logger.Error(ex, "Failed to migrate table: {TableName}", table.TableName);

                if (!options.ContinueOnError)
                    throw;
            }

            return result;
        }

        /// <summary>
        /// Migrates data for all tables in a schema
        /// </summary>
        public async Task<BatchMigrationResult> MigrateAllTablesAsync(
            string sourceConnectionString,
            string targetConnectionString,
            DatabaseSchema schema,
            MigrationOptions options,
            IProgress<MigrationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var batchResult = new BatchMigrationResult
            {
                TotalTables = schema.Tables.Count
            };

            _logger.Information("Starting batch migration for {TableCount} tables", schema.Tables.Count);

            // Order tables by dependencies (tables with no foreign keys first)
            var orderedTables = OrderTablesByDependencies(schema.Tables);

            // Use parallel processing if specified
            if (options.MaxDegreeOfParallelism > 1)
            {
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = options.MaxDegreeOfParallelism,
                    CancellationToken = cancellationToken
                };

                await Parallel.ForEachAsync(orderedTables, parallelOptions, async (table, ct) =>
                {
                    var result = await MigrateTableAsync(
                        sourceConnectionString,
                        targetConnectionString,
                        table,
                        options,
                        progress,
                        ct);

                    lock (batchResult)
                    {
                        batchResult.Results.Add(result);
                        batchResult.TotalRowsMigrated += result.RowsMigrated;

                        if (result.Success)
                            batchResult.SuccessfulTables++;
                        else
                            batchResult.FailedTables++;
                    }
                });
            }
            else
            {
                // Sequential migration
                foreach (var table in orderedTables)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    var result = await MigrateTableAsync(
                        sourceConnectionString,
                        targetConnectionString,
                        table,
                        options,
                        progress,
                        cancellationToken);

                    batchResult.Results.Add(result);
                    batchResult.TotalRowsMigrated += result.RowsMigrated;

                    if (result.Success)
                        batchResult.SuccessfulTables++;
                    else
                        batchResult.FailedTables++;
                }
            }

            stopwatch.Stop();
            batchResult.TotalDuration = stopwatch.Elapsed;

            _logger.Information("Batch migration completed. Success: {Success}, Failed: {Failed}, Total Rows: {Rows}, Duration: {Duration}",
                batchResult.SuccessfulTables, batchResult.FailedTables, batchResult.TotalRowsMigrated, batchResult.TotalDuration);

            return batchResult;
        }

        /// <summary>
        /// Validates data after migration
        /// </summary>
        public async Task<ValidationResult> ValidateDataAsync(
            string sourceConnectionString,
            string targetConnectionString,
            TableMetadata table,
            CancellationToken cancellationToken = default)
        {
            var validation = new ValidationResult
            {
                TableName = table.TableName
            };

            try
            {
                _logger.Information("Validating data for table: {TableName}", table.TableName);

                // Get source row count
                using (var sourceConn = new OracleConnection(sourceConnectionString))
                {
                    await sourceConn.OpenAsync(cancellationToken);

                    using var cmd = sourceConn.CreateCommand();
                    cmd.CommandText = $"SELECT COUNT(*) FROM {table.TableName}";

                    var count = await cmd.ExecuteScalarAsync(cancellationToken);
                    validation.SourceRowCount = Convert.ToInt64(count);
                }

                // Get target row count
                using (var targetConn = new SqlConnection(targetConnectionString))
                {
                    await targetConn.OpenAsync(cancellationToken);

                    using var cmd = targetConn.CreateCommand();
                    cmd.CommandText = $"SELECT COUNT(*) FROM [{table.SchemaName}].[{table.TableName}]";

                    var count = await cmd.ExecuteScalarAsync(cancellationToken);
                    validation.TargetRowCount = Convert.ToInt64(count);
                }

                // Compare row counts
                if (validation.SourceRowCount == validation.TargetRowCount)
                {
                    validation.IsValid = true;
                    _logger.Information("Validation passed for {TableName}. Row count: {RowCount}",
                        table.TableName, validation.SourceRowCount);
                }
                else
                {
                    validation.IsValid = false;
                    validation.ValidationErrors.Add(
                        $"Row count mismatch: Source={validation.SourceRowCount}, Target={validation.TargetRowCount}");

                    _logger.Warning("Validation failed for {TableName}. Source: {Source}, Target: {Target}",
                        table.TableName, validation.SourceRowCount, validation.TargetRowCount);
                }
            }
            catch (Exception ex)
            {
                validation.IsValid = false;
                validation.ValidationErrors.Add($"Validation error: {ex.Message}");
                _logger.Error(ex, "Failed to validate table: {TableName}", table.TableName);
            }

            return validation;
        }

        /// <summary>
        /// Performs the actual bulk copy operation
        /// </summary>
        private async Task<long> BulkCopyDataAsync(
            string sourceConnectionString,
            string targetConnectionString,
            TableMetadata table,
            MigrationOptions options,
            IProgress<MigrationProgress>? progress,
            CancellationToken cancellationToken)
        {
            long totalRowsCopied = 0;

            using var sourceConn = new OracleConnection(sourceConnectionString);
            await sourceConn.OpenAsync(cancellationToken);

            using var sourceCmd = sourceConn.CreateCommand();
            sourceCmd.CommandText = $"SELECT * FROM {table.TableName}";
            sourceCmd.CommandTimeout = options.CommandTimeout;

            using var reader = await sourceCmd.ExecuteReaderAsync(cancellationToken);

            using var targetConn = new SqlConnection(targetConnectionString);
            await targetConn.OpenAsync(cancellationToken);

            using var bulkCopy = new SqlBulkCopy(targetConn)
            {
                DestinationTableName = $"[{table.SchemaName}].[{table.TableName}]",
                BatchSize = options.BatchSize,
                BulkCopyTimeout = options.CommandTimeout,
                EnableStreaming = true
            };

            // Map columns
            foreach (var column in table.Columns)
            {
                bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
            }

            // Track progress
            var stopwatch = Stopwatch.StartNew();
            bulkCopy.NotifyAfter = options.BatchSize;
            bulkCopy.SqlRowsCopied += (sender, e) =>
            {
                totalRowsCopied = e.RowsCopied;

                if (progress != null)
                {
                    var elapsed = stopwatch.Elapsed;
                    var rowsPerSecond = totalRowsCopied / elapsed.TotalSeconds;
                    var remainingRows = table.RowCount - totalRowsCopied;
                    var estimatedRemaining = remainingRows > 0 && rowsPerSecond > 0
                        ? TimeSpan.FromSeconds(remainingRows / rowsPerSecond)
                        : TimeSpan.Zero;

                    progress.Report(new MigrationProgress
                    {
                        TableName = table.TableName,
                        RowsProcessed = totalRowsCopied,
                        TotalRows = table.RowCount,
                        Status = $"Copying data... {totalRowsCopied:N0} / {table.RowCount:N0} rows",
                        ElapsedTime = elapsed,
                        EstimatedTimeRemaining = estimatedRemaining
                    });
                }
            };

            // Perform the bulk copy
            await bulkCopy.WriteToServerAsync(reader, cancellationToken);

            return totalRowsCopied;
        }

        /// <summary>
        /// Disables indexes on a table for faster data loading
        /// </summary>
        private async Task DisableIndexesAsync(string connectionString, TableMetadata table, CancellationToken cancellationToken)
        {
            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);

                foreach (var index in table.Indexes.Where(i => !i.IsPrimaryKey))
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = $"ALTER INDEX [{index.IndexName}] ON [{table.SchemaName}].[{table.TableName}] DISABLE";

                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                    _logger.Debug("Disabled index: {IndexName} on {TableName}", index.IndexName, table.TableName);
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to disable indexes for table: {TableName}", table.TableName);
            }
        }

        /// <summary>
        /// Rebuilds indexes after data loading
        /// </summary>
        private async Task RebuildIndexesAsync(string connectionString, TableMetadata table, CancellationToken cancellationToken)
        {
            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);

                foreach (var index in table.Indexes.Where(i => !i.IsPrimaryKey))
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = $"ALTER INDEX [{index.IndexName}] ON [{table.SchemaName}].[{table.TableName}] REBUILD";

                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                    _logger.Debug("Rebuilt index: {IndexName} on {TableName}", index.IndexName, table.TableName);
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to rebuild indexes for table: {TableName}", table.TableName);
            }
        }

        /// <summary>
        /// Disables constraints on a table
        /// </summary>
        private async Task DisableConstraintsAsync(string connectionString, TableMetadata table, CancellationToken cancellationToken)
        {
            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = $"ALTER TABLE [{table.SchemaName}].[{table.TableName}] NOCHECK CONSTRAINT ALL";

                await cmd.ExecuteNonQueryAsync(cancellationToken);
                _logger.Debug("Disabled constraints on table: {TableName}", table.TableName);
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to disable constraints for table: {TableName}", table.TableName);
            }
        }

        /// <summary>
        /// Enables constraints on a table
        /// </summary>
        private async Task EnableConstraintsAsync(string connectionString, TableMetadata table, CancellationToken cancellationToken)
        {
            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);

                using var cmd = connection.CreateCommand();
                cmd.CommandText = $"ALTER TABLE [{table.SchemaName}].[{table.TableName}] WITH CHECK CHECK CONSTRAINT ALL";

                await cmd.ExecuteNonQueryAsync(cancellationToken);
                _logger.Debug("Enabled constraints on table: {TableName}", table.TableName);
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to enable constraints for table: {TableName}", table.TableName);
            }
        }

        /// <summary>
        /// Orders tables by their foreign key dependencies
        /// </summary>
        private List<TableMetadata> OrderTablesByDependencies(List<TableMetadata> tables)
        {
            var orderedTables = new List<TableMetadata>();
            var processedTables = new HashSet<string>();
            var tableDict = tables.ToDictionary(t => t.TableName, t => t);

            void ProcessTable(TableMetadata table)
            {
                if (processedTables.Contains(table.TableName))
                    return;

                // Process dependencies first (tables referenced by foreign keys)
                foreach (var fk in table.ForeignKeys)
                {
                    if (tableDict.TryGetValue(fk.ToTable, out var dependentTable) &&
                        !processedTables.Contains(dependentTable.TableName))
                    {
                        ProcessTable(dependentTable);
                    }
                }

                orderedTables.Add(table);
                processedTables.Add(table.TableName);
            }

            foreach (var table in tables)
            {
                ProcessTable(table);
            }

            return orderedTables;
        }
    }
}
