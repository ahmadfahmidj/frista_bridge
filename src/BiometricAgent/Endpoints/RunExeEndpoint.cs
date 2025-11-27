using BiometricAgent.Automation;
using BiometricAgent.Configuration;
using BiometricAgent.Models;
using BiometricAgent.Services;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace BiometricAgent.Endpoints;

/// <summary>
/// Handles GET /run_exe endpoint for Frista automation.
/// T048: Integrated with RequestQueueService for request queueing.
/// </summary>
public static class RunExeEndpoint
{
    private static AgentConfiguration? _config;
    private static RequestQueueService? _queueService;

    /// <summary>
    /// Initializes endpoint with configuration and queue service.
    /// </summary>
    public static void Initialize(AgentConfiguration config, RequestQueueService queueService)
    {
        _config = config;
        _queueService = queueService;
    }

    /// <summary>
    /// Processes Frista automation request.
    /// </summary>
    public static async Task<IResult> HandleAsync(HttpContext context, string? bpjs)
    {
        var correlationId = Guid.NewGuid().ToString();

        Log.Information("[{CorrelationId}] Received /run_exe request for NOKA={NOKA}",
            correlationId, bpjs ?? "<missing>");

        // Validate NOKA parameter
        if (string.IsNullOrWhiteSpace(bpjs))
        {
            Log.Warning("[{CorrelationId}] Missing NOKA parameter", correlationId);
            return Results.BadRequest(new
            {
                success = false,
                errorCode = ErrorCodes.ERR_MISSING_PARAMETER,
                message = "Missing required parameter: bpjs",
                correlationId
            });
        }

        // Validate NOKA format (13 digits)
        if (bpjs.Length != 13 || !bpjs.All(char.IsDigit))
        {
            Log.Warning("[{CorrelationId}] Invalid NOKA format: {NOKA}", correlationId, bpjs);
            return Results.BadRequest(new
            {
                success = false,
                errorCode = ErrorCodes.ERR_INVALID_NOKA,
                message = "NOKA must be exactly 13 digits",
                correlationId
            });
        }

        if (_config == null || _queueService == null)
        {
            Log.Error("[{CorrelationId}] Configuration or queue service not initialized", correlationId);
            return Results.Problem("Server configuration error", statusCode: 500);
        }

        try
        {
            // Create automation request
            var request = new AutomationRequest
            {
                CorrelationId = correlationId,
                NoPeserta = bpjs,
                WorkflowType = WorkflowType.Frista,
                ReceivedAtUtc = DateTime.UtcNow,
                ClientIpAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown"
            };

            Log.Information("[{CorrelationId}] Starting Frista automation workflow", correlationId);

            // T048: Execute workflow through queue service for serialized execution
            var result = await _queueService.EnqueueAndExecuteAsync(request, async (req) =>
            {
                var workflow = new FristaWorkflow(
                    _config.Applications.Frista,
                    _config.Credentials,
                    _config.UIAutomation
                );

                return await workflow.ExecuteAsync(req);
            });

            // Log result
            if (result.IsSuccess)
            {
                Log.Information("[{CorrelationId}] Frista automation completed successfully in {DurationMs}ms",
                    correlationId, result.DurationMs);
            }
            else
            {
                Log.Warning("[{CorrelationId}] Frista automation failed: {ErrorCode} - {Message}",
                    correlationId, result.ErrorCode, result.Message);
            }

            // Return JSON response
            var statusCode = result.IsSuccess ? 200 : 500;

            return Results.Json(new
            {
                success = result.IsSuccess,
                correlationId = result.CorrelationId,
                bpjs = result.NoPeserta,
                durationMs = result.DurationMs,
                message = result.Message,
                errorCode = result.ErrorCode
            }, statusCode: statusCode);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{CorrelationId}] Unhandled exception in /run_exe endpoint", correlationId);

            return Results.Json(new
            {
                success = false,
                correlationId,
                bpjs,
                durationMs = 0,
                message = "Internal server error",
                errorCode = ErrorCodes.ERR_CRITICAL_INTERNAL
            }, statusCode: 500);
        }
    }
}