using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using OracleToSQL.Core.Interfaces;
using OracleToSQL.Core.Models;
using OracleToSQL.Core.Services;
using OracleToSQL.Core.Utilities;
using OracleToSQL.DataMigrator;
using OracleToSQL.SchemaAnalyzer;
using OracleToSQL.SchemaConverter;
using Serilog;
using Serilog.Events;

namespace OracleToSQL.CLI
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            if (args.Length == 0)
            {
                ShowHelp();
                return 0;
            }

            var command = args[0].ToLower();

            try
            {
                return command switch
                {
                    "analyze" => await HandleAnalyzeCommand(args),
                    "convert" => await HandleConvertCommand(args),
                    "migrate" => await HandleMigrateCommand(args),
                    "validate" => await HandleValidateCommand(args),
                    "test-connection" => await HandleTestConnectionCommand(args),
                    "help" or "--help" or "-h" => ShowHelp(),
                    _ => ShowHelp()
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        static int ShowHelp()
        {
            Console.WriteLine(@"
Oracle to SQL Server Migration Tool
====================================

Usage: oracletosql <command> [options]

Commands:
  analyze           Analyze Oracle database schema
  convert           Convert Oracle schema to SQL Server DDL scripts
  migrate           Migrate data from Oracle to SQL Server
  validate          Validate migration results
  test-connection   Test database connections
  help              Show this help message

Examples:
  # Analyze Oracle schema
  oracletosql analyze --oracle ""Data Source=...;User Id=...;"" --output schema.json

  # Convert schema to SQL scripts
  oracletosql convert --input schema.json --output ./scripts

  # Migrate schema only
  oracletosql migrate schema --oracle ""..."" --sqlserver ""...""

  # Migrate data only
  oracletosql migrate data --oracle ""..."" --sqlserver ""..."" --parallel 4

  # Full migration (schema + data)
  oracletosql migrate full --oracle ""..."" --sqlserver ""...""

  # Validate migration
  oracletosql validate --oracle ""..."" --sqlserver ""...""

  # Test connections
  oracletosql test-connection --oracle ""..."" --sqlserver ""...""

Options:
  --oracle, -o      Oracle connection string
  --sqlserver, -s   SQL Server connection string
  --input, -i       Input file path
  --output, -out    Output file/directory path
  --parallel, -p    Maximum degree of parallelism (default: 4)
  --batch, -b       Batch size for data migration (default: 10000)
  --verbose, -v     Enable verbose logging
");
            return 0;
        }

        static async Task<int> HandleAnalyzeCommand(string[] args)
        {
            var oracle = GetArgValue(args, "--oracle", "-o");
            var output = GetArgValue(args, "--output", "-out") ?? "schema.json";
            var verbose = HasArg(args, "--verbose", "-v");

            if (string.IsNullOrEmpty(oracle))
            {
                Console.WriteLine("Error: --oracle parameter is required");
                return 1;
            }

            var logger = CreateLogger(verbose);
            Log.Logger = logger;

            logger.Information("Starting Oracle schema analysis...");

            var analyzer = new OracleSchemaAnalyzer(logger);
            var schema = await analyzer.AnalyzeSchemaAsync(oracle);

            // Save to JSON
            var json = JsonSerializer.Serialize(schema, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(output, json);

            logger.Information("Schema analysis complete!");
            logger.Information("Output saved to: {OutputFile}", output);
            logger.Information("Summary:");
            logger.Information("  - Tables: {Count}", schema.Tables.Count);
            logger.Information("  - Views: {Count}", schema.Views.Count);
            logger.Information("  - Sequences: {Count}", schema.Sequences.Count);
            logger.Information("  - Stored Procedures: {Count}", schema.StoredProcedures.Count);
            logger.Information("  - Functions: {Count}", schema.Functions.Count);

            return 0;
        }

        static async Task<int> HandleConvertCommand(string[] args)
        {
            var input = GetArgValue(args, "--input", "-i");
            var output = GetArgValue(args, "--output", "-out") ?? "./scripts";
            var verbose = HasArg(args, "--verbose", "-v");

            if (string.IsNullOrEmpty(input))
            {
                Console.WriteLine("Error: --input parameter is required");
                return 1;
            }

            var logger = CreateLogger(verbose);
            Log.Logger = logger;

            logger.Information("Loading schema from: {InputFile}", input);

            var json = await File.ReadAllTextAsync(input);
            var schema = JsonSerializer.Deserialize<DatabaseSchema>(json);

            if (schema == null)
            {
                logger.Error("Failed to parse schema file");
                return 1;
            }

            logger.Information("Converting schema to SQL Server...");

            var converter = new SqlServerSchemaConverter(logger);
            var scripts = await converter.GenerateMigrationScriptsAsync(schema);

            // Create output directory
            Directory.CreateDirectory(output);

            // Write scripts to files
            await File.WriteAllTextAsync(
                Path.Combine(output, "01_pre_migration.sql"),
                string.Join("\n", scripts.PreMigrationScripts));

            await File.WriteAllTextAsync(
                Path.Combine(output, "02_create_tables.sql"),
                string.Join("\n", scripts.SchemaCreationScripts));

            await File.WriteAllTextAsync(
                Path.Combine(output, "03_create_indexes.sql"),
                string.Join("\n", scripts.IndexCreationScripts));

            await File.WriteAllTextAsync(
                Path.Combine(output, "04_create_constraints.sql"),
                string.Join("\n", scripts.ConstraintCreationScripts));

            await File.WriteAllTextAsync(
                Path.Combine(output, "05_post_migration.sql"),
                string.Join("\n", scripts.PostMigrationScripts));

            // Write warnings if any
            if (scripts.Warnings.Count > 0)
            {
                var warningsFile = Path.Combine(output, "warnings.txt");
                await File.WriteAllTextAsync(warningsFile,
                    string.Join("\n", scripts.Warnings.Select(w => $"{w.Key}: {w.Value}")));

                logger.Warning("{Count} conversion warnings - see {File}", scripts.Warnings.Count, warningsFile);
            }

            logger.Information("Conversion complete! Scripts saved to: {OutputDirectory}", output);

            return 0;
        }

        static async Task<int> HandleMigrateCommand(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: oracletosql migrate <schema|data|full> --oracle \"...\" --sqlserver \"...\"");
                return 1;
            }

            var subCommand = args[1].ToLower();
            var oracle = GetArgValue(args, "--oracle", "-o");
            var sqlServer = GetArgValue(args, "--sqlserver", "-s");
            var parallel = int.Parse(GetArgValue(args, "--parallel", "-p") ?? "4");
            var batch = int.Parse(GetArgValue(args, "--batch", "-b") ?? "10000");
            var verbose = HasArg(args, "--verbose", "-v");

            if (string.IsNullOrEmpty(oracle) || string.IsNullOrEmpty(sqlServer))
            {
                Console.WriteLine("Error: Both --oracle and --sqlserver parameters are required");
                return 1;
            }

            return subCommand switch
            {
                "schema" => await MigrateSchemaAsync(oracle, sqlServer, verbose),
                "data" => await MigrateDataAsync(oracle, sqlServer, parallel, batch, verbose),
                "full" => await MigrateFullAsync(oracle, sqlServer, parallel, batch, verbose),
                _ => ShowHelp()
            };
        }

        static async Task<int> HandleValidateCommand(string[] args)
        {
            var oracle = GetArgValue(args, "--oracle", "-o");
            var sqlServer = GetArgValue(args, "--sqlserver", "-s");
            var verbose = HasArg(args, "--verbose", "-v");

            if (string.IsNullOrEmpty(oracle) || string.IsNullOrEmpty(sqlServer))
            {
                Console.WriteLine("Error: Both --oracle and --sqlserver parameters are required");
                return 1;
            }

            var logger = CreateLogger(verbose);
            Log.Logger = logger;

            logger.Information("Starting validation...");

            // Analyze source schema
            var analyzer = new OracleSchemaAnalyzer(logger);
            var schema = await analyzer.AnalyzeSchemaAsync(oracle);

            // Validate each table
            var migrator = new BulkDataMigrator(logger);
            int validTables = 0;
            int invalidTables = 0;

            foreach (var table in schema.Tables)
            {
                var validation = await migrator.ValidateDataAsync(oracle, sqlServer, table);

                if (validation.IsValid)
                {
                    validTables++;
                    logger.Information("✓ {TableName}: {RowCount} rows", table.TableName, validation.SourceRowCount);
                }
                else
                {
                    invalidTables++;
                    logger.Error("✗ {TableName}: Validation failed", table.TableName);
                    foreach (var error in validation.ValidationErrors)
                    {
                        logger.Error("  - {Error}", error);
                    }
                }
            }

            logger.Information("Validation complete!");
            logger.Information("Valid tables: {Valid} / {Total}", validTables, schema.Tables.Count);

            if (invalidTables > 0)
            {
                logger.Error("Invalid tables: {Invalid}", invalidTables);
                return 1;
            }

            return 0;
        }

        static async Task<int> HandleTestConnectionCommand(string[] args)
        {
            var oracle = GetArgValue(args, "--oracle", "-o");
            var sqlServer = GetArgValue(args, "--sqlserver", "-s");

            var logger = CreateLogger(false);
            Log.Logger = logger;

            bool allSuccess = true;

            if (!string.IsNullOrEmpty(oracle))
            {
                logger.Information("Testing Oracle connection...");
                var oracleManager = new OracleConnectionManager(logger);
                var oracleSuccess = await oracleManager.TestConnectionAsync(oracle);

                if (oracleSuccess)
                    logger.Information("✓ Oracle connection successful");
                else
                {
                    logger.Error("✗ Oracle connection failed");
                    allSuccess = false;
                }
            }

            if (!string.IsNullOrEmpty(sqlServer))
            {
                logger.Information("Testing SQL Server connection...");
                var sqlServerManager = new SqlServerConnectionManager(logger);
                var sqlServerSuccess = await sqlServerManager.TestConnectionAsync(sqlServer);

                if (sqlServerSuccess)
                    logger.Information("✓ SQL Server connection successful");
                else
                {
                    logger.Error("✗ SQL Server connection failed");
                    allSuccess = false;
                }
            }

            return allSuccess ? 0 : 1;
        }

        // Helper methods

        static async Task<int> MigrateSchemaAsync(string oracle, string sqlServer, bool verbose)
        {
            var logger = CreateLogger(verbose);
            Log.Logger = logger;

            logger.Information("Starting schema migration...");

            // Analyze Oracle schema
            var analyzer = new OracleSchemaAnalyzer(logger);
            var schema = await analyzer.AnalyzeSchemaAsync(oracle);

            // Convert to SQL Server
            var converter = new SqlServerSchemaConverter(logger);
            var scripts = await converter.GenerateMigrationScriptsAsync(schema);

            // Execute scripts on SQL Server
            using var connection = new SqlConnection(sqlServer);
            await connection.OpenAsync();

            logger.Information("Executing migration scripts...");

            foreach (var script in scripts.PreMigrationScripts.Concat(scripts.SchemaCreationScripts))
            {
                if (string.IsNullOrWhiteSpace(script)) continue;

                using var cmd = connection.CreateCommand();
                cmd.CommandText = script;
                await cmd.ExecuteNonQueryAsync();
            }

            logger.Information("Schema migration complete!");

            return 0;
        }

        static async Task<int> MigrateDataAsync(string oracle, string sqlServer, int parallel, int batch, bool verbose)
        {
            var logger = CreateLogger(verbose);
            Log.Logger = logger;

            logger.Information("Starting data migration...");

            // Analyze schema
            var analyzer = new OracleSchemaAnalyzer(logger);
            var schema = await analyzer.AnalyzeSchemaAsync(oracle);

            // Migrate data
            var migrator = new BulkDataMigrator(logger);
            var options = new MigrationOptions
            {
                BatchSize = batch,
                MaxDegreeOfParallelism = parallel
            };

            var progress = new Progress<MigrationProgress>(p =>
            {
                logger.Information("{Table}: {Percent:F1}% ({Rows:N0}/{Total:N0})",
                    p.TableName, p.PercentComplete, p.RowsProcessed, p.TotalRows);
            });

            var result = await migrator.MigrateAllTablesAsync(oracle, sqlServer, schema, options, progress);

            logger.Information("Data migration complete!");
            logger.Information("Success: {Success}, Failed: {Failed}, Total Rows: {Rows:N0}",
                result.SuccessfulTables, result.FailedTables, result.TotalRowsMigrated);

            return result.FailedTables > 0 ? 1 : 0;
        }

        static async Task<int> MigrateFullAsync(string oracle, string sqlServer, int parallel, int batch, bool verbose)
        {
            var logger = CreateLogger(verbose);
            Log.Logger = logger;

            logger.Information("Starting full migration (schema + data)...");

            var schemaResult = await MigrateSchemaAsync(oracle, sqlServer, verbose);
            if (schemaResult != 0)
                return schemaResult;

            var dataResult = await MigrateDataAsync(oracle, sqlServer, parallel, batch, verbose);

            logger.Information("Full migration complete!");

            return dataResult;
        }

        static ILogger CreateLogger(bool verbose)
        {
            return LoggerFactory.CreateLogger(
                verbose ? LogEventLevel.Debug : LogEventLevel.Information);
        }

        static string? GetArgValue(string[] args, params string[] names)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (names.Contains(args[i], StringComparer.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }
            return null;
        }

        static bool HasArg(string[] args, params string[] names)
        {
            return args.Any(arg => names.Contains(arg, StringComparer.OrdinalIgnoreCase));
        }
    }
}
