using Microsoft.AspNetCore.Http;
using Serilog;
using System.Diagnostics;

namespace BiometricAgent.Endpoints;

/// <summary>
/// Handles GET /stop_exe endpoint for terminating Frista process.
/// </summary>
public static class StopExeEndpoint
{
    /// <summary>
    /// Terminates Frista.exe process.
    /// </summary>
    public static async Task<IResult> HandleAsync(HttpContext context)
    {
        var correlationId = Guid.NewGuid().ToString();

        Log.Information("[{CorrelationId}] Received /stop_exe request", correlationId);

        try
        {
            // Find Frista.exe processes
            var processes = Process.GetProcessesByName("Frista");

            if (processes.Length == 0)
            {
                Log.Information("[{CorrelationId}] No Frista.exe process found", correlationId);
                return Results.Ok(new
                {
                    success = true,
                    message = "No Frista.exe process running",
                    processesTerminated = 0,
                    correlationId
                });
            }

            int terminated = 0;
            foreach (var process in processes)
            {
                try
                {
                    Log.Information("[{CorrelationId}] Terminating Frista.exe PID={ProcessId}",
                        correlationId, process.Id);

                    // Try graceful close first
                    process.CloseMainWindow();

                    // Wait up to 2 seconds for graceful exit
                    if (!process.WaitForExit(2000))
                    {
                        // Force kill if still running
                        Log.Warning("[{CorrelationId}] Frista.exe PID={ProcessId} did not exit gracefully, forcing termination",
                            correlationId, process.Id);
                        process.Kill();
                        process.WaitForExit();
                    }

                    terminated++;
                    Log.Information("[{CorrelationId}] Frista.exe PID={ProcessId} terminated successfully",
                        correlationId, process.Id);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[{CorrelationId}] Failed to terminate Frista.exe PID={ProcessId}",
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
                message = $"Terminated {terminated} Frista.exe process(es)",
                processesTerminated = terminated,
                correlationId
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{CorrelationId}] Error in /stop_exe endpoint", correlationId);
            return Results.Problem("Failed to terminate Frista.exe", statusCode: 500);
        }
    }
}