using BiometricAgent.Configuration;
using BiometricAgent.Models;
using System.Diagnostics;
using Serilog;

namespace BiometricAgent.Services;

/// <summary>
/// Manages lifecycle of BPJS application processes (Frista.exe, Finger.exe).
/// </summary>
public sealed class ProcessManager
{
    private readonly ApplicationConfig _config;
    private readonly WorkflowType _workflowType;
    private ProcessState _state;

    public ProcessManager(ApplicationConfig config, WorkflowType workflowType)
    {
        _config = config;
        _workflowType = workflowType;
        _state = new ProcessState { WorkflowType = workflowType };
    }

    /// <summary>
    /// Launches the BPJS application process.
    /// </summary>
    public async Task<ProcessState> LaunchAsync(string correlationId)
    {
        Log.Information("[{CorrelationId}] Launching {Workflow} from {Path}",
            correlationId, _workflowType, _config.ExecutablePath);

        // TODO: Implement process launch logic
        // - Verify executable exists
        // - Start process with ProcessStartInfo
        // - Wait for main window (with timeout)
        // - Capture window handle
        // - Update ProcessState

        _state.Status = ProcessStatus.Running;
        _state.LaunchedAtUtc = DateTime.UtcNow;
        _state.ActiveCorrelationId = correlationId;

        return _state;
    }

    /// <summary>
    /// Terminates the BPJS application process.
    /// </summary>
    public async Task<bool> TerminateAsync(string correlationId, bool force = false)
    {
        Log.Information("[{CorrelationId}] Terminating {Workflow} process (force={Force})",
            correlationId, _workflowType, force);

        // TODO: Implement process termination logic
        // - Find process by PID or name
        // - Send graceful close (WM_CLOSE) if not force
        // - Wait for exit with timeout
        // - Force kill if timeout or force=true
        // - Update ProcessState

        _state.Status = ProcessStatus.Stopped;
        _state.TerminatedAtUtc = DateTime.UtcNow;
        _state.ActiveCorrelationId = null;

        return true;
    }

    /// <summary>
    /// Gets current process state.
    /// </summary>
    public ProcessState GetState() => _state;

    /// <summary>
    /// Checks if process is currently running.
    /// </summary>
    public bool IsRunning()
    {
        // TODO: Implement actual process status check
        return _state.Status == ProcessStatus.Running || _state.Status == ProcessStatus.Automating;
    }
}