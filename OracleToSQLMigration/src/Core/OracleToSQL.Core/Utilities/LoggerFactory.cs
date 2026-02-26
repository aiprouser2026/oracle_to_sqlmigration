using System;
using Serilog;
using Serilog.Events;

namespace OracleToSQL.Core.Utilities
{
    /// <summary>
    /// Factory for creating configured Serilog loggers
    /// </summary>
    public static class LoggerFactory
    {
        /// <summary>
        /// Creates a logger with console and optional file output
        /// </summary>
        public static ILogger CreateLogger(LogEventLevel logLevel = LogEventLevel.Information, string? logFilePath = null)
        {
            var config = new LoggerConfiguration()
                .MinimumLevel.Is(logLevel)
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");

            if (!string.IsNullOrEmpty(logFilePath))
            {
                config.WriteTo.File(
                    logFilePath,
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}");
            }

            return config.CreateLogger();
        }

        /// <summary>
        /// Creates a logger from migration configuration
        /// </summary>
        public static ILogger CreateLogger(Models.MigrationConfiguration config)
        {
            var logLevel = config.LogLevel switch
            {
                Models.LogLevel.Debug => LogEventLevel.Debug,
                Models.LogLevel.Information => LogEventLevel.Information,
                Models.LogLevel.Warning => LogEventLevel.Warning,
                Models.LogLevel.Error => LogEventLevel.Error,
                _ => LogEventLevel.Information
            };

            return CreateLogger(logLevel, config.LogFilePath);
        }
    }
}
