namespace BiometricAgent.Configuration;

/// <summary>
/// Root configuration model for the Biometric Automation Agent.
/// Maps to config.json structure.
/// </summary>
public sealed class AgentConfiguration
{
    /// <summary>
    /// HTTP server configuration for REST API endpoints.
    /// </summary>
    public HttpServerConfig HttpServer { get; set; } = new();

    /// <summary>
    /// Application-specific configurations (Frista, Finger).
    /// </summary>
    public ApplicationsConfig Applications { get; set; } = new();

    /// <summary>
    /// Encrypted credential storage for BPJS applications.
    /// </summary>
    public CredentialsConfig Credentials { get; set; } = new();

    /// <summary>
    /// UI automation behavior and retry settings.
    /// </summary>
    public UIAutomationConfig UIAutomation { get; set; } = new();

    /// <summary>
    /// Structured logging configuration (Serilog).
    /// </summary>
    public LoggingConfig Logging { get; set; } = new();

    /// <summary>
    /// Performance and concurrency settings.
    /// </summary>
    public PerformanceConfig Performance { get; set; } = new();

    /// <summary>
    /// Error handling and recovery policies.
    /// </summary>
    public ErrorHandlingConfig ErrorHandling { get; set; } = new();

    /// <summary>
    /// Health monitoring configuration.
    /// </summary>
    public HealthConfig Health { get; set; } = new();
}

/// <summary>
/// HTTP server binding and timeout configuration.
/// </summary>
public sealed class HttpServerConfig
{
    /// <summary>
    /// Host address for HTTP listener (must be 127.0.0.1 for security).
    /// </summary>
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>
    /// Port number for HTTP API (default: 5000).
    /// </summary>
    public int Port { get; set; } = 5000;

    /// <summary>
    /// Maximum request processing time in seconds (default: 30).
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Container for application-specific configurations.
/// </summary>
public sealed class ApplicationsConfig
{
    /// <summary>
    /// Frista.exe (BPJS SIMRS) application settings.
    /// </summary>
    public ApplicationConfig Frista { get; set; } = new();

    /// <summary>
    /// Finger.exe (BPJS Fingerprint) application settings.
    /// </summary>
    public ApplicationConfig Finger { get; set; } = new();
}

/// <summary>
/// Configuration for a single BPJS application executable.
/// </summary>
public sealed class ApplicationConfig
{
    /// <summary>
    /// Full path to the application executable.
    /// </summary>
    public string ExecutablePath { get; set; } = string.Empty;

    /// <summary>
    /// Maximum time to wait for application startup (seconds).
    /// </summary>
    public int StartupTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Maximum time for automation workflow execution (seconds).
    /// </summary>
    public int AutomationTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Number of retry attempts for UI element detection.
    /// </summary>
    public int RetryAttempts { get; set; } = 3;

    /// <summary>
    /// Delay between retry attempts (milliseconds).
    /// </summary>
    public int RetryDelayMs { get; set; } = 500;
}

/// <summary>
/// DPAPI-encrypted credentials for BPJS application logins.
/// </summary>
public sealed class CredentialsConfig
{
    /// <summary>
    /// Encrypted username for Frista login (base64 DPAPI blob).
    /// </summary>
    public string FristaUsername { get; set; } = string.Empty;

    /// <summary>
    /// Encrypted password for Frista login (base64 DPAPI blob).
    /// </summary>
    public string FristaPassword { get; set; } = string.Empty;

    /// <summary>
    /// Encrypted username for Finger login (base64 DPAPI blob).
    /// </summary>
    public string FingerUsername { get; set; } = string.Empty;

    /// <summary>
    /// Encrypted password for Finger login (base64 DPAPI blob).
    /// </summary>
    public string FingerPassword { get; set; } = string.Empty;
}

/// <summary>
/// UI automation engine configuration (FlaUI behavior).
/// </summary>
public sealed class UIAutomationConfig
{
    /// <summary>
    /// Primary selector strategy: AutomationId, Name, or ControlType.
    /// </summary>
    public string PrimarySelectorStrategy { get; set; } = "AutomationId";

    /// <summary>
    /// Fallback strategies evaluated in order if primary fails.
    /// </summary>
    public List<string> FallbackStrategies { get; set; } = new() { "Name", "ControlType" };

    /// <summary>
    /// Wait time for UI elements to appear (milliseconds).
    /// </summary>
    public int ElementWaitTimeMs { get; set; } = 1000;

    /// <summary>
    /// Maximum depth for UI tree traversal during element search.
    /// </summary>
    public int MaxTreeDepth { get; set; } = 10;

    /// <summary>
    /// Enable UI element reference caching for performance.
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Cache entry expiration time (seconds).
    /// </summary>
    public int CacheExpirationSeconds { get; set; } = 60;
}

/// <summary>
/// Serilog structured logging configuration.
/// </summary>
public sealed class LoggingConfig
{
    /// <summary>
    /// Log file path (relative or absolute).
    /// </summary>
    public string LogPath { get; set; } = "logs/biometric-agent.log";

    /// <summary>
    /// Minimum log level: Verbose, Debug, Information, Warning, Error, Fatal.
    /// </summary>
    public string MinimumLevel { get; set; } = "Information";

    /// <summary>
    /// Enable structured JSON log formatting.
    /// </summary>
    public bool UseStructuredLogging { get; set; } = true;

    /// <summary>
    /// Rolling log file interval: Day, Hour, Infinite.
    /// </summary>
    public string RollingInterval { get; set; } = "Day";

    /// <summary>
    /// Number of log files to retain (older files deleted).
    /// </summary>
    public int RetainedFileCountLimit { get; set; } = 7;

    /// <summary>
    /// Maximum size per log file in bytes (null = unlimited).
    /// </summary>
    public long? FileSizeLimitBytes { get; set; } = 104857600; // 100MB

    /// <summary>
    /// Enable console logging (development mode only).
    /// </summary>
    public bool EnableConsoleLogging { get; set; } = true;
}

/// <summary>
/// Performance and concurrency tuning parameters.
/// </summary>
public sealed class PerformanceConfig
{
    /// <summary>
    /// Maximum concurrent automation requests (1 = serial execution).
    /// </summary>
    public int MaxConcurrentRequests { get; set; } = 1;

    /// <summary>
    /// Maximum time requests wait in queue before timeout (seconds).
    /// </summary>
    public int QueueTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Enable performance metrics collection and reporting.
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Log warning if automation exceeds this threshold (seconds).
    /// </summary>
    public double WarningThresholdSeconds { get; set; } = 5.0;
}

/// <summary>
/// Error recovery and retry policies.
/// </summary>
public sealed class ErrorHandlingConfig
{
    /// <summary>
    /// Terminate agent on critical unrecoverable errors.
    /// </summary>
    public bool TerminateOnCriticalError { get; set; } = true;

    /// <summary>
    /// Maximum retry attempts for transient errors.
    /// </summary>
    public int MaxErrorRetries { get; set; } = 3;

    /// <summary>
    /// Exponential backoff multiplier for retry delays.
    /// </summary>
    public double RetryBackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Capture screenshots on automation failures (debugging).
    /// </summary>
    public bool CaptureScreenshotOnError { get; set; } = false;

    /// <summary>
    /// Directory path for error screenshots.
    /// </summary>
    public string ScreenshotPath { get; set; } = "logs/screenshots";
}

/// <summary>
/// Health monitoring and status reporting configuration.
/// </summary>
public sealed class HealthConfig
{
    /// <summary>
    /// Interval between health status checks (seconds).
    /// </summary>
    public int CheckIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Memory usage threshold for unhealthy status (MB).
    /// </summary>
    public int MaxMemoryUsageMB { get; set; } = 200;

    /// <summary>
    /// Enable /health HTTP endpoint.
    /// </summary>
    public bool EnableHealthEndpoint { get; set; } = true;
}
