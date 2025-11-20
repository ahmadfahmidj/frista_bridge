namespace BiometricAgent.Models;

/// <summary>
/// Represents the outcome of an automation workflow execution.
/// </summary>
public sealed class AutomationResult
{
    /// <summary>
    /// Correlation ID matching the original request.
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether automation completed successfully.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Error code if automation failed (null on success).
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Human-readable message describing outcome.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// BPJS participant number (Nomor Kartu) from request.
    /// </summary>
    public string NoPeserta { get; set; } = string.Empty;

    /// <summary>
    /// Total execution duration in milliseconds.
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// Timestamp when automation started (UTC).
    /// </summary>
    public DateTime StartedAtUtc { get; set; }

    /// <summary>
    /// Timestamp when automation completed (UTC).
    /// </summary>
    public DateTime CompletedAtUtc { get; set; }

    /// <summary>
    /// Workflow type that was executed.
    /// </summary>
    public WorkflowType WorkflowType { get; set; }

    /// <summary>
    /// Creates a successful automation result.
    /// </summary>
    public static AutomationResult Success(AutomationRequest request, long durationMs)
    {
        return new AutomationResult
        {
            CorrelationId = request.CorrelationId,
            IsSuccess = true,
            Message = "Automation completed successfully",
            NoPeserta = request.NoPeserta,
            DurationMs = durationMs,
            WorkflowType = request.WorkflowType,
            CompletedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a failed automation result with error details.
    /// </summary>
    public static AutomationResult Failure(AutomationRequest request, string errorCode, string message, long durationMs)
    {
        return new AutomationResult
        {
            CorrelationId = request.CorrelationId,
            IsSuccess = false,
            ErrorCode = errorCode,
            Message = message,
            NoPeserta = request.NoPeserta,
            DurationMs = durationMs,
            WorkflowType = request.WorkflowType,
            CompletedAtUtc = DateTime.UtcNow
        };
    }
}