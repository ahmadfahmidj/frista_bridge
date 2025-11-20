using BiometricAgent.Services;
using Microsoft.AspNetCore.Http;
using System.Diagnostics;
using System.Reflection;

namespace BiometricAgent.Endpoints;

/// <summary>
/// Handles GET /health endpoint for health monitoring.
/// T059-T060: Health status with version, timestamp, and queue statistics.
/// </summary>
public static class HealthEndpoint
{
    private static readonly DateTime StartTimeUtc = DateTime.UtcNow;
    private static RequestQueueService? _queueService;

    /// <summary>
    /// Initializes health endpoint with queue service for statistics.
    /// </summary>
    public static void Initialize(RequestQueueService queueService)
    {
        _queueService = queueService;
    }

    /// <summary>
    /// Returns current health status with comprehensive metrics.
    /// </summary>
    public static async Task<IResult> HandleAsync(HttpContext context)
    {
        var uptime = DateTime.UtcNow - StartTimeUtc;
        var process = Process.GetCurrentProcess();
        var memoryUsageMB = process.WorkingSet64 / 1024 / 1024;

        // Get application version
        var version = Assembly.GetExecutingAssembly()
            .GetName()
            .Version?
            .ToString() ?? "1.0.0";

        // Get queue statistics
        var queueStats = _queueService?.GetStatistics();
        var totalRequests = queueStats?.TotalRequests ?? 0;
        var errorRate = totalRequests > 0
            ? (double)(queueStats?.FailedRequests + queueStats?.TimedOutRequests ?? 0) / totalRequests
            : 0.0;

        return Results.Ok(new
        {
            status = "Healthy",
            version = version,
            timestamp = DateTime.UtcNow,
            uptimeSeconds = (int)uptime.TotalSeconds,
            uptime = uptime.ToString(@"hh\:mm\:ss"),
            memoryUsageMB = memoryUsageMB,
            queue = new
            {
                currentDepth = queueStats?.CurrentQueueDepth ?? 0,
                totalRequests = queueStats?.TotalRequests ?? 0,
                completedRequests = queueStats?.CompletedRequests ?? 0,
                failedRequests = queueStats?.FailedRequests ?? 0,
                timedOutRequests = queueStats?.TimedOutRequests ?? 0,
                availableSlots = queueStats?.AvailableSlots ?? 0,
                errorRate = Math.Round(errorRate * 100, 2)
            }
        });
    }
}