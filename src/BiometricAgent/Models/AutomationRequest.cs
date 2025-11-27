namespace BiometricAgent.Models;

/// <summary>
/// Represents an incoming automation request from HTTP API.
/// </summary>
public sealed class AutomationRequest
{
    /// <summary>
    /// Unique correlation ID for request tracking across logs.
    /// </summary>
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// BPJS participant number (Nomor Kartu).
    /// </summary>
    public string NoPeserta { get; set; } = string.Empty;

    /// <summary>
    /// Type of automation workflow: Frista or Finger.
    /// </summary>
    public WorkflowType WorkflowType { get; set; }

    /// <summary>
    /// Timestamp when request was received (UTC).
    /// </summary>
    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// IP address of client making request (for logging).
    /// </summary>
    public string ClientIpAddress { get; set; } = "127.0.0.1";

    /// <summary>
    /// Username for Frista login (optional, falls back to config if not provided).
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Password for Frista login (optional, falls back to config if not provided).
    /// </summary>
    public string? Password { get; set; }
}

/// <summary>
/// Automation workflow type enumeration.
/// </summary>
public enum WorkflowType
{
    /// <summary>
    /// Frista.exe automation (BPJS SIMRS).
    /// </summary>
    Frista = 0,

    /// <summary>
    /// Finger.exe automation (BPJS Fingerprint).
    /// </summary>
    Finger = 1
}