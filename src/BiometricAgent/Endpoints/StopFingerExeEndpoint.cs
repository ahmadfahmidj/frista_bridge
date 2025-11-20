using Microsoft.AspNetCore.Http;
using Serilog;
using System.Diagnostics;

namespace BiometricAgent.Endpoints;

/// <summary>
/// Handles GET /stop_finger_exe endpoint for terminating Finger process.
/// </summary>
public static class StopFingerExeEndpoint
{
    /// <summary>
    /// Terminates After.exe (Finger) process.
    /// </summary>
    public static async Task<IResult> HandleAsync(HttpContext context)
    {
        var correlationId = Guid.NewGuid().ToString();

        Log.Information("[{CorrelationId}] Received /stop_finger_exe request", correlationId);

        try
        {
            // Find After.exe processes (Finger application executable)
            var processes = Process.GetProcessesByName("After");

            if (processes.Length == 0)
            {
                Log.Information("[{CorrelationId}] No After.exe process found", correlationId);
                return Results.Ok(new
                {
                    success = true,
                    message = "No After.exe (Finger) process running",
                    processesTerminated = 0,
                    correlationId
                });
            }

            int terminated = 0;
            foreach (var process in processes)
            {
                try
                {
                    Log.Information("[{CorrelationId}] Terminating After.exe PID={ProcessId}",
                        correlationId, process.Id);

                    // Try graceful close first
                    process.CloseMainWindow();

                    // Wait up to 2 seconds for graceful exit
                    if (!process.WaitForExit(2000))
                    {
                        // Force kill if still running
                        Log.Warning("[{CorrelationId}] After.exe PID={ProcessId} did not exit gracefully, forcing termination",
                            correlationId, process.Id);
                        process.Kill();
                        process.WaitForExit();
                    }

                    terminated++;
                    Log.Information("[{CorrelationId}] After.exe PID={ProcessId} terminated successfully",
                        correlationId, process.Id);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[{CorrelationId}] Failed to terminate After.exe PID={ProcessId}",
                        correlationId, process.Id);
                }
                finally
                {
                    process.Dispose();
                }
            }

            return Results.Ok(new
            {
                success = true,
                message = $"Terminated {terminated} After.exe (Finger) process(es)",
                processesTerminated = terminated,
                correlationId
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{CorrelationId}] Error in /stop_finger_exe endpoint", correlationId);
            return Results.Problem("Failed to terminate After.exe", statusCode: 500);
        }
    }
}