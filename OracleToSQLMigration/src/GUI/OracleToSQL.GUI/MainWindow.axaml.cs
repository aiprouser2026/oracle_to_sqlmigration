using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using OracleToSQL.Core.Interfaces;
using OracleToSQL.Core.Models;
using OracleToSQL.Core.Services;
using OracleToSQL.Core.Utilities;
using OracleToSQL.DataMigrator;
using OracleToSQL.SchemaAnalyzer;
using OracleToSQL.SchemaConverter;
using Serilog;
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OracleToSQL.GUI;

public partial class MainWindow : Window
{
    private readonly ILogger _logger;
    private readonly OracleConnectionManager _oracleManager;
    private readonly SqlServerConnectionManager _sqlServerManager;
    private readonly OracleSchemaAnalyzer _schemaAnalyzer;
    private readonly SqlServerSchemaConverter _schemaConverter;
    private readonly BulkDataMigrator _dataMigrator;

    private DatabaseSchema? _analyzedSchema;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _oracleConnected = false;
    private bool _sqlServerConnected = false;

    public MainWindow()
    {
        InitializeComponent();

        // Initialize logger
        _logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Sink(new GuiLogSink(this))
            .CreateLogger();

        Log.Logger = _logger;

        // Initialize services
        _oracleManager = new OracleConnectionManager(_logger);
        _sqlServerManager = new SqlServerConnectionManager(_logger);
        _schemaAnalyzer = new OracleSchemaAnalyzer(_logger);
        _schemaConverter = new SqlServerSchemaConverter(_logger);
        _dataMigrator = new BulkDataMigrator(_logger);

        LogMessage("Application started. Ready for migration.");
    }

    private async void TestOracleButton_Click(object? sender, RoutedEventArgs e)
    {
        var connectionString = OracleConnectionTextBox?.Text ?? "";

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            LogMessage("ERROR: Please enter an Oracle connection string");
            return;
        }

        LogMessage("Testing Oracle connection...");
        DisableButtons();

        try
        {
            _oracleConnected = await _oracleManager.TestConnectionAsync(connectionString);

            if (_oracleConnected)
            {
                var version = await _oracleManager.GetDatabaseVersionAsync(connectionString);
                LogMessage($"✓ Oracle connection successful! Version: {version}");
                UpdateConnectionStatus();
            }
            else
            {
                LogMessage("✗ Oracle connection failed");
                _oracleConnected = false;
            }
        }
        catch (Exception ex)
        {
            LogMessage($"✗ Oracle connection error: {ex.Message}");
            _oracleConnected = false;
        }
        finally
        {
            EnableButtons();
        }
    }

    private async void TestSqlServerButton_Click(object? sender, RoutedEventArgs e)
    {
        var connectionString = SqlServerConnectionTextBox?.Text ?? "";

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            LogMessage("ERROR: Please enter a SQL Server connection string");
            return;
        }

        LogMessage("Testing SQL Server connection...");
        DisableButtons();

        try
        {
            _sqlServerConnected = await _sqlServerManager.TestConnectionAsync(connectionString);

            if (_sqlServerConnected)
            {
                var version = await _sqlServerManager.GetDatabaseVersionAsync(connectionString);
                LogMessage($"✓ SQL Server connection successful!");
                LogMessage($"  Version: {version.Split('\n')[0]}");
                UpdateConnectionStatus();
            }
            else
            {
                LogMessage("✗ SQL Server connection failed");
                _sqlServerConnected = false;
            }
        }
        catch (Exception ex)
        {
            LogMessage($"✗ SQL Server connection error: {ex.Message}");
            _sqlServerConnected = false;
        }
        finally
        {
            EnableButtons();
        }
    }

    private async void AnalyzeButton_Click(object? sender, RoutedEventArgs e)
    {
        var oracleConn = OracleConnectionTextBox?.Text ?? "";

        if (string.IsNullOrWhiteSpace(oracleConn))
        {
            LogMessage("ERROR: Please enter Oracle connection string");
            return;
        }

        LogMessage("========================================");
        LogMessage("Starting Oracle schema analysis...");
        LogMessage("========================================");

        DisableButtons();
        UpdateStatus("Analyzing Oracle schema...", 0);

        try
        {
            _cancellationTokenSource = new CancellationTokenSource();

            _analyzedSchema = await Task.Run(async () =>
                await _schemaAnalyzer.AnalyzeSchemaAsync(oracleConn, _cancellationTokenSource.Token));

            LogMessage($"\n✓ Schema analysis complete!");
            LogMessage($"  Database: {_analyzedSchema.DatabaseName}");
            LogMessage($"  Schema: {_analyzedSchema.SchemaName}");
            LogMessage($"  Tables: {_analyzedSchema.Tables.Count}");
            LogMessage($"  Total Rows: {_analyzedSchema.Tables.Sum(t => t.RowCount):N0}");
            LogMessage($"  Views: {_analyzedSchema.Views.Count}");
            LogMessage($"  Sequences: {_analyzedSchema.Sequences.Count}");
            LogMessage($"  Stored Procedures: {_analyzedSchema.StoredProcedures.Count}");
            LogMessage($"  Functions: {_analyzedSchema.Functions.Count}");
            LogMessage($"  Triggers: {_analyzedSchema.Triggers.Count}");
            LogMessage($"  Total Size: {_analyzedSchema.TotalSizeBytes / 1024.0 / 1024.0:N2} MB");
            LogMessage("\nReady for migration!");

            UpdateStatus("Analysis complete", 100);
        }
        catch (OperationCanceledException)
        {
            LogMessage("Analysis cancelled by user");
            UpdateStatus("Cancelled", 0);
        }
        catch (Exception ex)
        {
            LogMessage($"✗ Analysis failed: {ex.Message}");
            UpdateStatus("Analysis failed", 0);
        }
        finally
        {
            EnableButtons();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    private async void MigrateButton_Click(object? sender, RoutedEventArgs e)
    {
        var oracleConn = OracleConnectionTextBox?.Text ?? "";
        var sqlServerConn = SqlServerConnectionTextBox?.Text ?? "";

        if (string.IsNullOrWhiteSpace(oracleConn) || string.IsNullOrWhiteSpace(sqlServerConn))
        {
            LogMessage("ERROR: Please enter both connection strings");
            return;
        }

        if (_analyzedSchema == null)
        {
            LogMessage("Please analyze the schema first (click 'Analyze Schema')");
            return;
        }

        var migrationType = MigrationTypeCombo?.SelectedIndex ?? 2;
        var parallel = (int)(ParallelNumeric?.Value ?? 4);
        var batchSize = (int)(BatchSizeNumeric?.Value ?? 10000);

        LogMessage("========================================");
        LogMessage("Starting migration...");
        LogMessage($"Type: {GetMigrationType(migrationType)}");
        LogMessage($"Parallel tables: {parallel}");
        LogMessage($"Batch size: {batchSize:N0}");
        LogMessage("========================================");

        DisableButtons();

        try
        {
            _cancellationTokenSource = new CancellationTokenSource();

            // Migrate schema if needed
            if (migrationType == 0 || migrationType == 2)
            {
                UpdateStatus("Migrating schema...", 0);
                await MigrateSchemaAsync(sqlServerConn);
            }

            // Migrate data if needed
            if (migrationType == 1 || migrationType == 2)
            {
                UpdateStatus("Migrating data...", 0);
                await MigrateDataAsync(oracleConn, sqlServerConn, parallel, batchSize);
            }

            LogMessage("\n✓ Migration completed successfully!");
            UpdateStatus("Migration complete", 100);
        }
        catch (OperationCanceledException)
        {
            LogMessage("Migration cancelled by user");
            UpdateStatus("Cancelled", 0);
        }
        catch (Exception ex)
        {
            LogMessage($"✗ Migration failed: {ex.Message}");
            UpdateStatus("Migration failed", 0);
        }
        finally
        {
            EnableButtons();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    private async void ValidateButton_Click(object? sender, RoutedEventArgs e)
    {
        var oracleConn = OracleConnectionTextBox?.Text ?? "";
        var sqlServerConn = SqlServerConnectionTextBox?.Text ?? "";

        if (string.IsNullOrWhiteSpace(oracleConn) || string.IsNullOrWhiteSpace(sqlServerConn))
        {
            LogMessage("ERROR: Please enter both connection strings");
            return;
        }

        if (_analyzedSchema == null)
        {
            LogMessage("Please analyze the schema first");
            return;
        }

        LogMessage("========================================");
        LogMessage("Starting validation...");
        LogMessage("========================================");

        DisableButtons();
        UpdateStatus("Validating migration...", 0);

        try
        {
            int validTables = 0;
            int invalidTables = 0;
            int currentTable = 0;

            foreach (var table in _analyzedSchema.Tables)
            {
                currentTable++;
                var progress = (double)currentTable / _analyzedSchema.Tables.Count * 100;
                UpdateStatus($"Validating {table.TableName}...", progress);

                var validation = await _dataMigrator.ValidateDataAsync(
                    oracleConn,
                    sqlServerConn,
                    table,
                    _cancellationTokenSource?.Token ?? CancellationToken.None);

                if (validation.IsValid)
                {
                    validTables++;
                    LogMessage($"✓ {table.TableName}: {validation.SourceRowCount:N0} rows");
                }
                else
                {
                    invalidTables++;
                    LogMessage($"✗ {table.TableName}: FAILED");
                    foreach (var error in validation.ValidationErrors)
                    {
                        LogMessage($"  - {error}");
                    }
                }
            }

            LogMessage($"\n========================================");
            LogMessage($"Validation Results:");
            LogMessage($"  Valid tables: {validTables}/{_analyzedSchema.Tables.Count}");
            LogMessage($"  Invalid tables: {invalidTables}");
            LogMessage($"========================================");

            UpdateStatus($"Validation complete: {validTables}/{_analyzedSchema.Tables.Count} valid", 100);
        }
        catch (Exception ex)
        {
            LogMessage($"✗ Validation failed: {ex.Message}");
            UpdateStatus("Validation failed", 0);
        }
        finally
        {
            EnableButtons();
        }
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        _cancellationTokenSource?.Cancel();
        LogMessage("Cancellation requested...");
    }

    private async Task MigrateSchemaAsync(string sqlServerConn)
    {
        if (_analyzedSchema == null) return;

        LogMessage("\nGenerating SQL Server DDL scripts...");

        var scripts = await _schemaConverter.GenerateMigrationScriptsAsync(_analyzedSchema);

        LogMessage($"Generated {scripts.SchemaCreationScripts.Count} table scripts");
        LogMessage($"Generated {scripts.IndexCreationScripts.Count} index scripts");
        LogMessage($"Generated {scripts.ConstraintCreationScripts.Count} constraint scripts");

        if (scripts.Warnings.Count > 0)
        {
            LogMessage($"\n⚠ {scripts.Warnings.Count} conversion warnings:");
            foreach (var warning in scripts.Warnings.Take(5))
            {
                LogMessage($"  - {warning.Key}: {warning.Value}");
            }
            if (scripts.Warnings.Count > 5)
            {
                LogMessage($"  ... and {scripts.Warnings.Count - 5} more");
            }
        }

        LogMessage("\nExecuting DDL scripts on SQL Server...");

        using var connection = new Microsoft.Data.SqlClient.SqlConnection(sqlServerConn);
        await connection.OpenAsync(_cancellationTokenSource?.Token ?? CancellationToken.None);

        var allScripts = scripts.PreMigrationScripts
            .Concat(scripts.SchemaCreationScripts)
            .Concat(scripts.IndexCreationScripts)
            .Concat(scripts.ConstraintCreationScripts);

        int scriptCount = 0;
        foreach (var script in allScripts)
        {
            if (string.IsNullOrWhiteSpace(script)) continue;

            scriptCount++;
            using var cmd = connection.CreateCommand();
            cmd.CommandText = script;
            await cmd.ExecuteNonQueryAsync(_cancellationTokenSource?.Token ?? CancellationToken.None);
        }

        LogMessage($"✓ Executed {scriptCount} SQL scripts successfully");
    }

    private async Task MigrateDataAsync(string oracleConn, string sqlServerConn, int parallel, int batchSize)
    {
        if (_analyzedSchema == null) return;

        var options = new MigrationOptions
        {
            BatchSize = batchSize,
            MaxDegreeOfParallelism = parallel,
            DisableIndexesDuringMigration = true,
            DisableConstraintsDuringMigration = true
        };

        var progress = new Progress<MigrationProgress>(p =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                UpdateStatus($"{p.TableName}: {p.PercentComplete:F1}%", p.PercentComplete);
                LogMessage($"  {p.TableName}: {p.RowsProcessed:N0}/{p.TotalRows:N0} rows ({p.PercentComplete:F1}%)");
            });
        });

        var result = await _dataMigrator.MigrateAllTablesAsync(
            oracleConn,
            sqlServerConn,
            _analyzedSchema,
            options,
            progress,
            _cancellationTokenSource?.Token ?? CancellationToken.None);

        LogMessage($"\n✓ Data migration complete!");
        LogMessage($"  Successful tables: {result.SuccessfulTables}/{result.TotalTables}");
        LogMessage($"  Failed tables: {result.FailedTables}");
        LogMessage($"  Total rows migrated: {result.TotalRowsMigrated:N0}");
        LogMessage($"  Total duration: {result.TotalDuration}");

        if (result.FailedTables > 0)
        {
            LogMessage($"\nFailed tables:");
            foreach (var failed in result.Results.Where(r => !r.Success))
            {
                LogMessage($"  ✗ {failed.TableName}: {failed.ErrorMessage}");
            }
        }
    }

    private void UpdateStatus(string message, double progress)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (StatusLabel != null) StatusLabel.Text = message;
            if (ProgressBar != null) ProgressBar.Value = progress;
            if (ProgressTextLabel != null)
                ProgressTextLabel.Text = $"{progress:F1}% - {message}";
        });
    }

    private void UpdateConnectionStatus()
    {
        var oracleStatus = _oracleConnected ? "✓ Connected" : "Not connected";
        var sqlServerStatus = _sqlServerConnected ? "✓ Connected" : "Not connected";

        Dispatcher.UIThread.Post(() =>
        {
            if (ConnectionStatusLabel != null)
                ConnectionStatusLabel.Text = $"Oracle: {oracleStatus} | SQL Server: {sqlServerStatus}";
        });
    }

    public void LogMessage(string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (LogTextBlock != null)
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                var currentText = LogTextBlock.Text;

                if (currentText == "Log output will appear here...")
                    currentText = "";

                LogTextBlock.Text = currentText + $"[{timestamp}] {message}\n";
            }
        });
    }

    private void DisableButtons()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (AnalyzeButton != null) AnalyzeButton.IsEnabled = false;
            if (MigrateButton != null) MigrateButton.IsEnabled = false;
            if (ValidateButton != null) ValidateButton.IsEnabled = false;
            if (TestOracleButton != null) TestOracleButton.IsEnabled = false;
            if (TestSqlServerButton != null) TestSqlServerButton.IsEnabled = false;
            if (CancelButton != null) CancelButton.IsEnabled = true;
        });
    }

    private void EnableButtons()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (AnalyzeButton != null) AnalyzeButton.IsEnabled = true;
            if (MigrateButton != null) MigrateButton.IsEnabled = true;
            if (ValidateButton != null) ValidateButton.IsEnabled = true;
            if (TestOracleButton != null) TestOracleButton.IsEnabled = true;
            if (TestSqlServerButton != null) TestSqlServerButton.IsEnabled = true;
            if (CancelButton != null) CancelButton.IsEnabled = false;
        });
    }

    private string GetMigrationType(int index)
    {
        return index switch
        {
            0 => "Schema Only",
            1 => "Data Only",
            2 => "Full Migration (Schema + Data)",
            _ => "Unknown"
        };
    }
}

// Custom Serilog sink for GUI logging
public class GuiLogSink : Serilog.Core.ILogEventSink
{
    private readonly MainWindow _window;

    public GuiLogSink(MainWindow window)
    {
        _window = window;
    }

    public void Emit(Serilog.Events.LogEvent logEvent)
    {
        var message = logEvent.RenderMessage();
        _window.LogMessage(message);
    }
}
