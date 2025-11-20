using BiometricAgent.Configuration;
using BiometricAgent.Models;
using BiometricAgent.Services;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace BiometricAgent.Endpoints;

/// <summary>
/// Handles GET /run_finger_exe endpoint for Finger automation.
/// T048: Integrated with RequestQueueService for request queueing.
/// </summary>
public static class RunFingerExeEndpoint
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
    /// Processes Finger automation request.
    /// </summary>
    public static async Task<IResult> HandleAsync(HttpContext context, string? noka)
    {
        var correlationId = Guid.NewGuid().ToString();

        Log.Information("[{CorrelationId}] Received /run_finger_exe request for NOKA={NOKA}",
            correlationId, noka ?? "<missing>");

        // Validate NOKA parameter
        if (string.IsNullOrWhiteSpace(noka))
        {
            Log.Warning("[{CorrelationId}] Missing NOKA parameter", correlationId);
            return Results.BadRequest(new
            {
                success = false,
                errorCode = ErrorCodes.ERR_MISSING_PARAMETER,
                message = "Missing required parameter: noka",
                correlationId
            });
        }

        // Validate NOKA format (13 digits)
        if (noka.Length != 13 || !noka.All(char.IsDigit))
        {
            Log.Warning("[{CorrelationId}] Invalid NOKA format: {NOKA}", correlationId, noka);
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
            return Results.StatusCode(500);
        }

        Log.Information("[{CorrelationId}] Starting Finger automation workflow", correlationId);

        // Create automation request
        var request = new AutomationRequest
        {
            NoPeserta = noka,
            CorrelationId = correlationId,
            WorkflowType = WorkflowType.Finger,
            ReceivedAtUtc = DateTime.UtcNow,
            ClientIpAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown"
        };

        // T048: Execute workflow through queue service for serialized execution
        var result = await _queueService.EnqueueAndExecuteAsync(request, async (req) =>
        {
            var workflow = new BiometricAgent.Automation.FingerWorkflow(
                _config.Applications.Finger,
                _config.Credentials,
                _config.UIAutomation
            );

            return await workflow.ExecuteAsync(req);
        });

        if (result.IsSuccess)
        {
            Log.Information("[{CorrelationId}] Finger automation completed successfully in {DurationMs}ms",
                correlationId, result.DurationMs);

            return Results.Ok(new
            {
                success = true,
                correlationId,
                noka,
                durationMs = result.DurationMs,
                message = "Automation completed successfully",
                errorCode = (string?)null
            });
        }
        else
        {
            Log.Warning("[{CorrelationId}] Finger automation failed: {ErrorCode} - {Message}",
                correlationId, result.ErrorCode, result.Message);

            return Results.Json(new
            {
                success = false,
                correlationId,
                noka,
                durationMs = result.DurationMs,
                message = result.Message,
                errorCode = result.ErrorCode
            }, statusCode: 500);
        }
    }
}