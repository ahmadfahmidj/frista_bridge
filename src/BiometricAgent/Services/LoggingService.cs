using BiometricAgent.Configuration;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace BiometricAgent.Services;

/// <summary>
/// Configures and initializes Serilog structured logging.
/// </summary>
public static class LoggingService
{
    /// <summary>
    /// Initializes Serilog global logger from configuration.
    /// </summary>
    public static void Initialize(LoggingConfig config)
    {
        var loggerConfig = new LoggerConfiguration();

        // Set minimum log level
        var minimumLevel = ParseLogLevel(config.MinimumLevel);
        loggerConfig.MinimumLevel.Is(minimumLevel);

        // Configure file sink with rolling policy
        if (config.UseStructuredLogging)
        {
            loggerConfig.WriteTo.File(
                formatter: new JsonFormatter(),
                path: config.LogPath,
                rollingInterval: ParseRollingInterval(config.RollingInterval),
                retainedFileCountLimit: config.RetainedFileCountLimit,
                fileSizeLimitBytes: config.FileSizeLimitBytes,
                shared: false,
                flushToDiskInterval: TimeSpan.FromSeconds(1)
            );
        }
        else
        {
            loggerConfig.WriteTo.File(
                path: config.LogPath,
                rollingInterval: ParseRollingInterval(config.RollingInterval),
                retainedFileCountLimit: config.RetainedFileCountLimit,
                fileSizeLimitBytes: config.FileSizeLimitBytes,
                shared: false,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] {Message:lj}{NewLine}{Exception}"
            );
        }

        // Configure console sink (development mode)
        if (config.EnableConsoleLogging)
        {
            loggerConfig.WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss}] [{Level:u3}] {Message:lj}{NewLine}{Exception}"
            );
        }

        // Enrich logs with context
        loggerConfig
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithProcessId()
            .Enrich.WithThreadId();

        // Create and set global logger
        Log.Logger = loggerConfig.CreateLogger();

        Log.Information("Logging initialized with level {MinimumLevel}", config.MinimumLevel);
    }

    /// <summary>
    /// Parses string log level to Serilog LogEventLevel.
    /// </summary>
    private static LogEventLevel ParseLogLevel(string level)
    {
        return level.ToLowerInvariant() switch
        {
            "verbose" => LogEventLevel.Verbose,
            "debug" => LogEventLevel.Debug,
            "information" => LogEventLevel.Information,
            "warning" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            "fatal" => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };
    }

    /// <summary>
    /// Parses string rolling interval to Serilog RollingInterval.
    /// </summary>
    private static RollingInterval ParseRollingInterval(string interval)
    {
        return interval.ToLowerInvariant() switch
        {
            "day" => RollingInterval.Day,
            "hour" => RollingInterval.Hour,
            "infinite" => RollingInterval.Infinite,
            _ => RollingInterval.Day
        };
    }

    /// <summary>
    /// Flushes and closes the global logger (call on shutdown).
    /// </summary>
    public static void Shutdown()
    {
        Log.Information("Shutting down logging system");
        Log.CloseAndFlush();
    }
}