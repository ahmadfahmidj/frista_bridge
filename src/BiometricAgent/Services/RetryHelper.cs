using Serilog;

namespace BiometricAgent.Services;

/// <summary>
/// Helper for executing operations with exponential backoff retry logic.
/// T050: Implements 3 retries with 1s, 2s, 4s delays for transient errors.
/// </summary>
public static class RetryHelper
{
    /// <summary>
    /// Executes an async operation with exponential backoff retry logic.
    /// </summary>
    /// <typeparam name="T">Return type of the operation</typeparam>
    /// <param name="operation">The operation to execute</param>
    /// <param name="maxRetries">Maximum number of retry attempts (default: 3)</param>
    /// <param name="initialDelayMs">Initial delay in milliseconds (default: 1000ms = 1s)</param>
    /// <param name="backoffMultiplier">Multiplier for each retry delay (default: 2.0 for exponential)</param>
    /// <param name="operationName">Name of operation for logging</param>
    /// <param name="correlationId">Correlation ID for logging</param>
    /// <param name="shouldRetry">Optional predicate to determine if exception should trigger retry</param>
    /// <returns>Result of the operation</returns>
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        int maxRetries = 3,
        int initialDelayMs = 1000,
        double backoffMultiplier = 2.0,
        string operationName = "Operation",
        string? correlationId = null,
        Func<Exception, bool>? shouldRetry = null)
    {
        var attempt = 0;
        var currentDelayMs = initialDelayMs;

        while (true)
        {
            try
            {
                attempt++;

                if (attempt > 1)
                {
                    Log.Information("[{CorrelationId}] Retry attempt {Attempt}/{MaxRetries} for {Operation}",
                        correlationId ?? "N/A", attempt, maxRetries + 1, operationName);
                }

                return await operation();
            }
            catch (Exception ex)
            {
                // Check if we should retry this exception
                bool canRetry = shouldRetry?.Invoke(ex) ?? IsTransientError(ex);

                if (!canRetry || attempt > maxRetries)
                {
                    Log.Error(ex, "[{CorrelationId}] {Operation} failed after {Attempt} attempts (non-retryable or max retries exceeded)",
                        correlationId ?? "N/A", operationName, attempt);
                    throw;
                }

                // Calculate delay with exponential backoff
                var delayMs = (int)(currentDelayMs * Math.Pow(backoffMultiplier, attempt - 1));

                Log.Warning(ex, "[{CorrelationId}] {Operation} failed on attempt {Attempt}/{MaxRetries}. Retrying in {DelayMs}ms...",
                    correlationId ?? "N/A", operationName, attempt, maxRetries + 1, delayMs);

                await Task.Delay(delayMs);
            }
        }
    }

    /// <summary>
    /// Executes a synchronous operation with exponential backoff retry logic.
    /// </summary>
    /// <typeparam name="T">Return type of the operation</typeparam>
    /// <param name="operation">The operation to execute</param>
    /// <param name="maxRetries">Maximum number of retry attempts (default: 3)</param>
    /// <param name="initialDelayMs">Initial delay in milliseconds (default: 1000ms = 1s)</param>
    /// <param name="backoffMultiplier">Multiplier for each retry delay (default: 2.0 for exponential)</param>
    /// <param name="operationName">Name of operation for logging</param>
    /// <param name="correlationId">Correlation ID for logging</param>
    /// <param name="shouldRetry">Optional predicate to determine if exception should trigger retry</param>
    /// <returns>Result of the operation</returns>
    public static T ExecuteWithRetry<T>(
        Func<T> operation,
        int maxRetries = 3,
        int initialDelayMs = 1000,
        double backoffMultiplier = 2.0,
        string operationName = "Operation",
        string? correlationId = null,
        Func<Exception, bool>? shouldRetry = null)
    {
        var attempt = 0;
        var currentDelayMs = initialDelayMs;

        while (true)
        {
            try
            {
                attempt++;

                if (attempt > 1)
                {
                    Log.Information("[{CorrelationId}] Retry attempt {Attempt}/{MaxRetries} for {Operation}",
                        correlationId ?? "N/A", attempt, maxRetries + 1, operationName);
                }

                return operation();
            }
            catch (Exception ex)
            {
                // Check if we should retry this exception
                bool canRetry = shouldRetry?.Invoke(ex) ?? IsTransientError(ex);

                if (!canRetry || attempt > maxRetries)
                {
                    Log.Error(ex, "[{CorrelationId}] {Operation} failed after {Attempt} attempts (non-retryable or max retries exceeded)",
                        correlationId ?? "N/A", operationName, attempt);
                    throw;
                }

                // Calculate delay with exponential backoff
                var delayMs = (int)(currentDelayMs * Math.Pow(backoffMultiplier, attempt - 1));

                Log.Warning(ex, "[{CorrelationId}] {Operation} failed on attempt {Attempt}/{MaxRetries}. Retrying in {DelayMs}ms...",
                    correlationId ?? "N/A", operationName, attempt, maxRetries + 1, delayMs);

                Thread.Sleep(delayMs);
            }
        }
    }

    /// <summary>
    /// Determines if an exception represents a transient error that should be retried.
    /// </summary>
    private static bool IsTransientError(Exception ex)
    {
        // Transient errors that typically resolve on retry:
        // - TimeoutException: UI element not found yet
        // - InvalidOperationException: Window not ready yet
        // - NullReferenceException: Element not available yet (common in UI automation)

        return ex is TimeoutException
            || ex is InvalidOperationException
            || (ex.Message?.Contains("not found", StringComparison.OrdinalIgnoreCase) ?? false)
            || (ex.Message?.Contains("not ready", StringComparison.OrdinalIgnoreCase) ?? false);
    }
}
