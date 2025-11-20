using BiometricAgent.Configuration;
using BiometricAgent.Models;
using Serilog;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace BiometricAgent.Services;

/// <summary>
/// FIFO queue for serializing concurrent automation requests with monitoring and timeout handling.
/// Supports User Story 3: Telemedicine Pre-Session Verification.
/// </summary>
public sealed class RequestQueueService
{
    private readonly PerformanceConfig _config;
    private readonly ConcurrentQueue<QueuedRequest> _queue = new();
    private readonly SemaphoreSlim _semaphore;
    private int _totalRequests = 0;
    private int _totalCompletedRequests = 0;
    private int _totalTimedOutRequests = 0;
    private int _totalFailedRequests = 0;
    private readonly object _statsLock = new();

    // Queue depth warning threshold
    private const int QUEUE_DEPTH_WARNING_THRESHOLD = 10;

    public RequestQueueService(PerformanceConfig config)
    {
        _config = config;
        _semaphore = new SemaphoreSlim(config.MaxConcurrentRequests, config.MaxConcurrentRequests);
    }

    /// <summary>
    /// Enqueues an automation request and executes it with queue monitoring and timeout handling.
    /// T048: Request queueing with in-progress detection
    /// T049: Queue depth monitoring
    /// T051: Queue timeout handling
    /// T052: Correlation ID tracking (all logs)
    /// T053: Performance metrics logging
    /// </summary>
    public async Task<AutomationResult> EnqueueAndExecuteAsync(
        AutomationRequest request,
        Func<AutomationRequest, Task<AutomationResult>> workflowExecutor)
    {
        var queueStopwatch = Stopwatch.StartNew();
        var correlationId = request.CorrelationId;

        var queuedRequest = new QueuedRequest
        {
            Request = request,
            EnqueuedAtUtc = DateTime.UtcNow,
            QueueTimeout = TimeSpan.FromSeconds(_config.QueueTimeoutSeconds)
        };

        _queue.Enqueue(queuedRequest);

        lock (_statsLock)
        {
            _totalRequests++;
        }

        int queueDepth = _queue.Count;

        // T049: Queue depth monitoring - warn if queue > 10 requests
        if (queueDepth > QUEUE_DEPTH_WARNING_THRESHOLD)
        {
            Log.Warning("[{CorrelationId}] High queue depth detected: {QueueDepth} requests waiting (threshold: {Threshold})",
                correlationId, queueDepth, QUEUE_DEPTH_WARNING_THRESHOLD);
        }

        Log.Information("[{CorrelationId}] Request enqueued at position {Position}, queue depth: {QueueDepth}",
            correlationId, queueDepth, queueDepth);

        // T051: Check queue timeout before waiting for semaphore
        if (queuedRequest.IsTimedOut)
        {
            lock (_statsLock)
            {
                _totalTimedOutRequests++;
            }

            Log.Warning("[{CorrelationId}] Request already timed out in queue (age: {AgeSeconds}s, timeout: {TimeoutSeconds}s)",
                correlationId,
                (DateTime.UtcNow - queuedRequest.EnqueuedAtUtc).TotalSeconds,
                queuedRequest.QueueTimeout.TotalSeconds);

            return AutomationResult.Failure(
                request,
                ErrorCodes.ERR_QUEUE_TIMEOUT,
                $"Request timed out in queue after {(DateTime.UtcNow - queuedRequest.EnqueuedAtUtc).TotalSeconds:F1}s",
                0);
        }

        // Wait for semaphore slot
        var acquired = await _semaphore.WaitAsync(queuedRequest.QueueTimeout);
        queueStopwatch.Stop();

        if (!acquired)
        {
            lock (_statsLock)
            {
                _totalTimedOutRequests++;
            }

            Log.Warning("[{CorrelationId}] Request timed out waiting for execution slot (queue wait time: {QueueWaitMs}ms)",
                correlationId, queueStopwatch.ElapsedMilliseconds);

            // T053: Performance metrics - queue wait time
            return AutomationResult.Failure(
                request,
                ErrorCodes.ERR_QUEUE_TIMEOUT,
                $"Request timed out in queue after waiting {queueStopwatch.ElapsedMilliseconds}ms",
                0);
        }

        // T053: Performance metrics - log queue wait time
        Log.Information("[{CorrelationId}] Acquired execution slot after {QueueWaitMs}ms in queue",
            correlationId, queueStopwatch.ElapsedMilliseconds);

        var executionStopwatch = Stopwatch.StartNew();

        try
        {
            // Execute the provided workflow
            var result = await workflowExecutor(request);
            executionStopwatch.Stop();

            if (result.IsSuccess)
            {
                lock (_statsLock)
                {
                    _totalCompletedRequests++;
                }

                // T053: Performance metrics - total execution time
                Log.Information("[{CorrelationId}] Request completed successfully - Queue wait: {QueueWaitMs}ms, Execution: {ExecutionMs}ms, Total: {TotalMs}ms",
                    correlationId,
                    queueStopwatch.ElapsedMilliseconds,
                    executionStopwatch.ElapsedMilliseconds,
                    queueStopwatch.ElapsedMilliseconds + executionStopwatch.ElapsedMilliseconds);
            }
            else
            {
                lock (_statsLock)
                {
                    _totalFailedRequests++;
                }

                Log.Warning("[{CorrelationId}] Request failed - Queue wait: {QueueWaitMs}ms, Execution: {ExecutionMs}ms, Error: {ErrorCode}",
                    correlationId,
                    queueStopwatch.ElapsedMilliseconds,
                    executionStopwatch.ElapsedMilliseconds,
                    result.ErrorCode);
            }

            return result;
        }
        catch (Exception ex)
        {
            executionStopwatch.Stop();

            lock (_statsLock)
            {
                _totalFailedRequests++;
            }

            Log.Error(ex, "[{CorrelationId}] Unhandled exception during workflow execution - Queue wait: {QueueWaitMs}ms, Execution: {ExecutionMs}ms",
                correlationId, queueStopwatch.ElapsedMilliseconds, executionStopwatch.ElapsedMilliseconds);

            return AutomationResult.Failure(
                request,
                ErrorCodes.ERR_CRITICAL_INTERNAL,
                $"Internal error: {ex.Message}",
                executionStopwatch.ElapsedMilliseconds);
        }
        finally
        {
            _semaphore.Release();

            // Clean up processed request from queue
            _queue.TryDequeue(out _);
        }
    }

    /// <summary>
    /// Gets current queue depth.
    /// </summary>
    public int GetQueueDepth() => _queue.Count;

    /// <summary>
    /// Gets total requests received.
    /// </summary>
    public int GetTotalRequests()
    {
        lock (_statsLock)
        {
            return _totalRequests;
        }
    }

    /// <summary>
    /// Gets total completed requests.
    /// </summary>
    public int GetCompletedRequests()
    {
        lock (_statsLock)
        {
            return _totalCompletedRequests;
        }
    }

    /// <summary>
    /// Gets total timed out requests.
    /// </summary>
    public int GetTimedOutRequests()
    {
        lock (_statsLock)
        {
            return _totalTimedOutRequests;
        }
    }

    /// <summary>
    /// Gets total failed requests.
    /// </summary>
    public int GetFailedRequests()
    {
        lock (_statsLock)
        {
            return _totalFailedRequests;
        }
    }

    /// <summary>
    /// Gets queue statistics for monitoring.
    /// </summary>
    public QueueStatistics GetStatistics()
    {
        lock (_statsLock)
        {
            return new QueueStatistics
            {
                CurrentQueueDepth = _queue.Count,
                TotalRequests = _totalRequests,
                CompletedRequests = _totalCompletedRequests,
                TimedOutRequests = _totalTimedOutRequests,
                FailedRequests = _totalFailedRequests,
                AvailableSlots = _semaphore.CurrentCount
            };
        }
    }
}

/// <summary>
/// Statistics snapshot for queue monitoring.
/// </summary>
public sealed class QueueStatistics
{
    public int CurrentQueueDepth { get; set; }
    public int TotalRequests { get; set; }
    public int CompletedRequests { get; set; }
    public int TimedOutRequests { get; set; }
    public int FailedRequests { get; set; }
    public int AvailableSlots { get; set; }
}