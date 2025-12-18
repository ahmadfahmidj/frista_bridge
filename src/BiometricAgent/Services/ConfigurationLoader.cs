using System.Reflection;
using System.Text.Json;
using BiometricAgent.Configuration;
using BiometricAgent.Models;
using Serilog;

namespace BiometricAgent.Services;

/// <summary>
/// Loads and validates agent configuration from config.json.
/// </summary>
public sealed class ConfigurationLoader
{
    private const string DEFAULT_CONFIG_PATH = "config/config.json";

    /// <summary>
    /// Gets the base directory where the executable is located.
    /// This is important for Windows Service mode where working directory differs.
    /// </summary>
    public static string GetExecutableDirectory()
    {
        // Use AppContext.BaseDirectory which works correctly for single-file and service deployments
        return AppContext.BaseDirectory;
    }

    /// <summary>
    /// Resolves a relative path to be relative to the executable directory.
    /// </summary>
    public static string ResolvePathRelativeToExecutable(string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            return relativePath;
        }
        return Path.Combine(GetExecutableDirectory(), relativePath);
    }

    /// <summary>
    /// Loads configuration from file path.
    /// </summary>
    public static AgentConfiguration Load(string? configPath = null)
    {
        // Resolve path relative to executable directory (important for Windows Service mode)
        var path = ResolvePathRelativeToExecutable(configPath ?? DEFAULT_CONFIG_PATH);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Configuration file not found: {path}");
        }

        Log.Information("Loading configuration from {ConfigPath}", path);

        try
        {
            var json = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<AgentConfiguration>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

            if (config == null)
            {
                throw new InvalidOperationException("Deserialized configuration is null");
            }

            ValidateConfiguration(config);

            Log.Information("Configuration loaded successfully");
            return config;
        }
        catch (JsonException ex)
        {
            Log.Fatal(ex, "Invalid JSON in configuration file: {ConfigPath}", path);
            throw new InvalidOperationException($"Configuration file contains invalid JSON: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates required configuration values.
    /// </summary>
    private static void ValidateConfiguration(AgentConfiguration config)
    {
        var errors = new List<string>();

        // Validate HTTP server config - allow 127.0.0.1, localhost, or 0.0.0.0 (for service mode)
        var validHosts = new[] { "127.0.0.1", "localhost", "0.0.0.0", "+" };
        if (!validHosts.Contains(config.HttpServer.Host))
        {
            errors.Add("HttpServer.Host must be 127.0.0.1, localhost, 0.0.0.0, or + for security");
        }

        if (config.HttpServer.Port < 1 || config.HttpServer.Port > 65535)
        {
            errors.Add("HttpServer.Port must be between 1 and 65535");
        }

        // Validate application paths
        if (string.IsNullOrWhiteSpace(config.Applications.Frista.ExecutablePath))
        {
            errors.Add("Applications.Frista.ExecutablePath is required");
        }
        else if (!File.Exists(config.Applications.Frista.ExecutablePath))
        {
            Log.Warning("Frista executable not found at {Path}", config.Applications.Frista.ExecutablePath);
        }

        if (string.IsNullOrWhiteSpace(config.Applications.Finger.ExecutablePath))
        {
            errors.Add("Applications.Finger.ExecutablePath is required");
        }
        else if (!File.Exists(config.Applications.Finger.ExecutablePath))
        {
            Log.Warning("Finger executable not found at {Path}", config.Applications.Finger.ExecutablePath);
        }

        // Validate credentials are present (not decrypted yet)
        if (string.IsNullOrWhiteSpace(config.Credentials.FristaUsername))
        {
            errors.Add("Credentials.FristaUsername is required");
        }

        if (string.IsNullOrWhiteSpace(config.Credentials.FristaPassword))
        {
            errors.Add("Credentials.FristaPassword is required");
        }

        if (string.IsNullOrWhiteSpace(config.Credentials.FingerUsername))
        {
            errors.Add("Credentials.FingerUsername is required");
        }

        if (string.IsNullOrWhiteSpace(config.Credentials.FingerPassword))
        {
            errors.Add("Credentials.FingerPassword is required");
        }

        // Validate logging config
        if (string.IsNullOrWhiteSpace(config.Logging.LogPath))
        {
            errors.Add("Logging.LogPath is required");
        }

        if (errors.Any())
        {
            var errorMessage = $"Configuration validation failed:\n" + string.Join("\n", errors.Select(e => $"  - {e}"));
            Log.Fatal(errorMessage);
            throw new InvalidOperationException(errorMessage);
        }
    }
}