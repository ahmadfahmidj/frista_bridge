namespace BiometricAgent.Models;

/// <summary>
/// Represents a queued automation request awaiting execution.
/// </summary>
public sealed class QueuedRequest
{
    /// <summary>
    /// The automation request payload.
    /// </summary>
    public AutomationRequest Request { get; set; } = new();

    /// <summary>
    /// Timestamp when request was enqueued (UTC).
    /// </summary>
    public DateTime EnqueuedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// TaskCompletionSource for async result retrieval.
    /// </summary>
    public TaskCompletionSource<AutomationResult> CompletionSource { get; set; } = new();

    /// <summary>
    /// Queue position (1-based, 0 = currently executing).
    /// </summary>
    public int QueuePosition { get; set; }

    /// <summary>
    /// Maximum time request can wait in queue before timeout.
    /// </summary>
    public TimeSpan QueueTimeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Checks if request has exceeded queue timeout.
    /// </summary>
    public bool IsTimedOut => DateTime.UtcNow - EnqueuedAtUtc > QueueTimeout;
}