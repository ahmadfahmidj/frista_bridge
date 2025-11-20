namespace BiometricAgent.Models;

/// <summary>
/// Tracks the lifecycle state of a managed BPJS application process.
/// </summary>
public sealed class ProcessState
{
    /// <summary>
    /// Process ID of the running application (0 if not running).
    /// </summary>
    public int ProcessId { get; set; }

    /// <summary>
    /// Workflow type associated with this process.
    /// </summary>
    public WorkflowType WorkflowType { get; set; }

    /// <summary>
    /// Current execution state of the process.
    /// </summary>
    public ProcessStatus Status { get; set; } = ProcessStatus.NotStarted;

    /// <summary>
    /// Timestamp when process was launched (UTC, null if not started).
    /// </summary>
    public DateTime? LaunchedAtUtc { get; set; }

    /// <summary>
    /// Timestamp when process was terminated (UTC, null if still running).
    /// </summary>
    public DateTime? TerminatedAtUtc { get; set; }

    /// <summary>
    /// Main window handle for UI automation access.
    /// </summary>
    public IntPtr WindowHandle { get; set; }

    /// <summary>
    /// Current automation step being executed (for logging).
    /// </summary>
    public string CurrentStep { get; set; } = string.Empty;

    /// <summary>
    /// Correlation ID of active automation request (null if idle).
    /// </summary>
    public string? ActiveCorrelationId { get; set; }
}

/// <summary>
/// Process execution state enumeration.
/// </summary>
public enum ProcessStatus
{
    /// <summary>
    /// Process has not been launched.
    /// </summary>
    NotStarted = 0,

    /// <summary>
    /// Process is starting but main window not yet available.
    /// </summary>
    Starting = 1,

    /// <summary>
    /// Process is running and ready for automation.
    /// </summary>
    Running = 2,

    /// <summary>
    /// Automation workflow is actively executing.
    /// </summary>
    Automating = 3,

    /// <summary>
    /// Process is being gracefully terminated.
    /// </summary>
    Stopping = 4,

    /// <summary>
    /// Process has been terminated (gracefully or forcefully).
    /// </summary>
    Stopped = 5,

    /// <summary>
    /// Process encountered an error and is in failed state.
    /// </summary>
    Failed = 6
}