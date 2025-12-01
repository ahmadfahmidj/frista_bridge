using System.Text;
using System.Runtime.Versioning;
using System.Diagnostics;
using System.Net.NetworkInformation;
using BiometricAgent.Configuration;
using BiometricAgent.Endpoints;
using BiometricAgent.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting.WindowsServices;
using Serilog;

namespace BiometricAgent;

/// <summary>
/// Main entry point for the Biometric Automation Agent.
/// Initializes configuration, logging, HTTP server, and services.
/// </summary>
[SupportedOSPlatform("windows")]
public class Program
{
    public static async Task Main(string[] args)
    {
        // Handle CLI commands before starting the web server
        if (args.Length > 0 && args[0] == "--encrypt-password")
        {
            RunPasswordEncryptionUtility();
            return;
        }

        try
        {
            // Load configuration
            var config = ConfigurationLoader.Load();

            // Initialize logging
            LoggingService.Initialize(config.Logging);

            Log.Information("=== Biometric Automation Agent Starting ===");
            Log.Information("Host: {Host}:{Port}", config.HttpServer.Host, config.HttpServer.Port);

            // Kill any process using the configured port before starting
            KillProcessOnPort(config.HttpServer.Port);

            // Build ASP.NET Core Minimal API application
            var builder = WebApplication.CreateBuilder(args);

            // Enable Windows Service hosting (allows running as background service)
            builder.Host.UseWindowsService(options =>
            {
                options.ServiceName = "BiometricAgent";
            });

            // Configure Kestrel to listen on configured host:port
            builder.WebHost.UseUrls($"http://{config.HttpServer.Host}:{config.HttpServer.Port}");

            // Add Serilog to ASP.NET Core
            builder.Services.AddSerilog();

            // Add CORS services
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowKiosk", policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });

            var app = builder.Build();

            // Use CORS middleware
            app.UseCors("AllowKiosk");

            // Map HTTP endpoints
            MapEndpoints(app);

            Log.Information("HTTP API initialized with 5 endpoints");

            // T048: Initialize RequestQueueService for User Story 3 (telemedicine queueing)
            var queueService = new RequestQueueService(config.Performance);
            Log.Information("RequestQueueService initialized with max {MaxConcurrent} concurrent requests",
                config.Performance.MaxConcurrentRequests);

            // Initialize endpoints with configuration and queue service
            RunExeEndpoint.Initialize(config, queueService);
            RunFingerExeEndpoint.Initialize(config, queueService);
            HealthEndpoint.Initialize(queueService);

            // Initialize system tray icon for monitoring (only when not running as Windows Service)
            TrayIconService? trayIcon = null;
            if (!WindowsServiceHelpers.IsWindowsService())
            {
                trayIcon = new TrayIconService(config.HttpServer.Host, config.HttpServer.Port);
                
                // Start tray icon on a separate STA thread (required for Windows Forms)
                var trayThread = new Thread(() =>
                {
                    System.Windows.Forms.Application.EnableVisualStyles();
                    System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
                    trayIcon.Initialize();
                    System.Windows.Forms.Application.Run();
                });
                trayThread.SetApartmentState(ApartmentState.STA);
                trayThread.IsBackground = true;
                trayThread.Start();

                Log.Information("System tray icon started for monitoring");
            }
            else
            {
                Log.Information("Running as Windows Service - tray icon disabled");
            }

            // Start HTTP server
            await app.RunAsync();

            // Cleanup tray icon on shutdown
            trayIcon?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application failed to start");
            throw;
        }
        finally
        {
            LoggingService.Shutdown();
        }
    }

    /// <summary>
    /// Maps HTTP endpoints to handlers.
    /// </summary>
    private static void MapEndpoints(WebApplication app)
    {
        // Automation endpoints
        app.MapGet("/run_exe", async (HttpContext ctx, string? bpjs, string? username, string? password) =>
            await RunExeEndpoint.HandleAsync(ctx, bpjs, username, password));

        app.MapGet("/run_finger_exe", async (HttpContext ctx, string? bpjs) =>
            await RunFingerExeEndpoint.HandleAsync(ctx, bpjs));

        // Process control endpoints
        app.MapGet("/stop_exe", async (HttpContext ctx) =>
            await StopExeEndpoint.HandleAsync(ctx));

        app.MapGet("/stop_finger_exe", async (HttpContext ctx) =>
            await StopFingerExeEndpoint.HandleAsync(ctx));

        // Health monitoring endpoint
        app.MapGet("/health", async (HttpContext ctx) =>
            await HealthEndpoint.HandleAsync(ctx));

        Log.Information("Endpoints mapped: /run_exe, /run_finger_exe, /stop_exe, /stop_finger_exe, /health");
    }

    /// <summary>
    /// Kills any process currently using the specified port.
    /// This ensures the agent can bind to the port on startup.
    /// </summary>
    /// <param name="port">The port number to check and free up.</param>
    private static void KillProcessOnPort(int port)
    {
        try
        {
            Log.Information("Checking for processes using port {Port}...", port);
            
            // Get all active TCP listeners
            var ipProperties = IPGlobalProperties.GetIPGlobalProperties();
            var listeners = ipProperties.GetActiveTcpListeners();
            
            // Check if the port is in use
            bool portInUse = listeners.Any(ep => ep.Port == port);
            
            if (!portInUse)
            {
                Log.Information("Port {Port} is available", port);
                return;
            }
            
            Log.Warning("Port {Port} is in use, attempting to free it...", port);
            
            // Use netstat to find the PID using this port
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c netstat -ano | findstr :{port} | findstr LISTENING",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using var netstatProcess = Process.Start(startInfo);
            if (netstatProcess == null) return;
            
            string output = netstatProcess.StandardOutput.ReadToEnd();
            netstatProcess.WaitForExit();
            
            if (string.IsNullOrWhiteSpace(output))
            {
                Log.Information("No listening process found on port {Port}", port);
                return;
            }
            
            // Parse the output to get PIDs
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var killedPids = new HashSet<int>();
            
            foreach (var line in lines)
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 5 && int.TryParse(parts[^1].Trim(), out int pid) && pid > 0 && !killedPids.Contains(pid))
                {
                    try
                    {
                        var process = Process.GetProcessById(pid);
                        string processName = process.ProcessName;
                        
                        // Don't kill system processes
                        if (processName.Equals("System", StringComparison.OrdinalIgnoreCase) ||
                            processName.Equals("Idle", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                        
                        Log.Warning("Killing process {ProcessName} (PID: {Pid}) using port {Port}", processName, pid, port);
                        
                        // Use taskkill /F /T which works better with elevated processes
                        var killStartInfo = new ProcessStartInfo
                        {
                            FileName = "taskkill",
                            Arguments = $"/F /T /PID {pid}",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        };
                        
                        using var killProcess = Process.Start(killStartInfo);
                        if (killProcess != null)
                        {
                            killProcess.WaitForExit(5000);
                            killedPids.Add(pid);
                            Log.Information("Process {ProcessName} (PID: {Pid}) terminated", processName, pid);
                        }
                    }
                    catch (ArgumentException)
                    {
                        // Process already exited
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to kill process with PID {Pid}", pid);
                    }
                }
            }
            
            if (killedPids.Count > 0)
            {
                // Wait a moment for the port to be released
                Thread.Sleep(2000);
                Log.Information("Freed port {Port} by terminating {Count} process(es)", port, killedPids.Count);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error while checking/freeing port {Port}", port);
        }
    }

    /// <summary>
    /// Interactive CLI utility for encrypting passwords using Windows DPAPI.
    /// Allows administrators to generate encrypted credentials for config.json.
    /// </summary>
    private static void RunPasswordEncryptionUtility()
    {
        Console.WriteLine("=== Biometric Agent - Password Encryption Utility ===");
        Console.WriteLine();
        Console.WriteLine("This utility encrypts passwords using Windows DPAPI (Data Protection API).");
        Console.WriteLine("Encrypted passwords are tied to the current Windows user account.");
        Console.WriteLine();
        Console.WriteLine("IMPORTANT: Run this tool as the same user that will run the agent service.");
        Console.WriteLine();

        while (true)
        {
            Console.Write("Enter password to encrypt (or 'exit' to quit): ");

            // Read password without echoing to console
            string? plaintext = ReadPasswordFromConsole();

            if (string.IsNullOrEmpty(plaintext))
            {
                Console.WriteLine("Error: Password cannot be empty.");
                Console.WriteLine();
                continue;
            }

            if (plaintext.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Exiting encryption utility.");
                break;
            }

            try
            {
                string encrypted = CredentialEncryption.Encrypt(plaintext);
                Console.WriteLine();
                Console.WriteLine("Encrypted password (copy this to config.json):");
                Console.WriteLine(encrypted);
                Console.WriteLine();
                Console.WriteLine("Example config.json usage:");
                Console.WriteLine("  \"FristaPassword\": \"{0}\"", encrypted);
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("Error encrypting password: {0}", ex.Message);
                Console.WriteLine();
            }
        }
    }

    /// <summary>
    /// Reads password from console without echoing characters to the screen.
    /// Displays asterisks (*) for each character typed.
    /// </summary>
    /// <returns>The password entered by the user.</returns>
    private static string ReadPasswordFromConsole()
    {
        var password = new StringBuilder();

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (password.Length > 0)
                {
                    password.Length--;
                    Console.Write("\b \b"); // Erase the last asterisk
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                password.Append(key.KeyChar);
                Console.Write("*");
            }
        }

        return password.ToString();
    }
}
