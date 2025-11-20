using System.Text;
using BiometricAgent.Configuration;
using BiometricAgent.Endpoints;
using BiometricAgent.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace BiometricAgent;

/// <summary>
/// Main entry point for the Biometric Automation Agent.
/// Initializes configuration, logging, HTTP server, and services.
/// </summary>
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

            // Build ASP.NET Core Minimal API application
            var builder = WebApplication.CreateBuilder(args);

            // Configure Kestrel to listen on configured host:port
            builder.WebHost.UseUrls($"http://{config.HttpServer.Host}:{config.HttpServer.Port}");

            // Add Serilog to ASP.NET Core
            builder.Services.AddSerilog();

            var app = builder.Build();

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

            // Start HTTP server
            await app.RunAsync();
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
        app.MapGet("/run_exe", async (HttpContext ctx, string? noka) =>
            await RunExeEndpoint.HandleAsync(ctx, noka));

        app.MapGet("/run_finger_exe", async (HttpContext ctx, string? noka) =>
            await RunFingerExeEndpoint.HandleAsync(ctx, noka));

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
