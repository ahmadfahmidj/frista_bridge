namespace BiometricAgent.Models;

/// <summary>
/// Standardized error codes for automation failures.
/// Maps to 5 categories: Application, Authentication, UI Automation, Queueing, System.
/// </summary>
public static class ErrorCodes
{
    // ===== Application Errors =====

    /// <summary>
    /// Executable file not found at configured path.
    /// </summary>
    public const string ERR_APP_NOT_FOUND = "ERR_APP_NOT_FOUND";

    /// <summary>
    /// Application failed to start within timeout period.
    /// </summary>
    public const string ERR_APP_START_TIMEOUT = "ERR_APP_START_TIMEOUT";

    /// <summary>
    /// Application crashed during automation.
    /// </summary>
    public const string ERR_APP_CRASHED = "ERR_APP_CRASHED";

    // ===== Authentication Errors =====

    /// <summary>
    /// Login credentials were rejected by application.
    /// </summary>
    public const string ERR_LOGIN_FAILED = "ERR_LOGIN_FAILED";

    /// <summary>
    /// Credential decryption failed (DPAPI error).
    /// </summary>
    public const string ERR_CREDENTIAL_DECRYPT_FAILED = "ERR_CREDENTIAL_DECRYPT_FAILED";

    /// <summary>
    /// Credentials are missing or empty in configuration.
    /// </summary>
    public const string ERR_CREDENTIALS_MISSING = "ERR_CREDENTIALS_MISSING";

    // ===== UI Automation Errors =====

    /// <summary>
    /// Required UI element could not be found after retries.
    /// </summary>
    public const string ERR_UI_ELEMENT_NOT_FOUND = "ERR_UI_ELEMENT_NOT_FOUND";

    /// <summary>
    /// Automation workflow exceeded maximum execution time.
    /// </summary>
    public const string ERR_AUTOMATION_TIMEOUT = "ERR_AUTOMATION_TIMEOUT";

    /// <summary>
    /// UI element interaction failed (click, type, etc.).
    /// </summary>
    public const string ERR_UI_INTERACTION_FAILED = "ERR_UI_INTERACTION_FAILED";

    /// <summary>
    /// Application window not found after launch.
    /// </summary>
    public const string ERR_WINDOW_NOT_FOUND = "ERR_WINDOW_NOT_FOUND";

    // ===== Queueing Errors =====

    /// <summary>
    /// Request timed out while waiting in queue.
    /// </summary>
    public const string ERR_QUEUE_TIMEOUT = "ERR_QUEUE_TIMEOUT";

    /// <summary>
    /// Queue is full and cannot accept new requests.
    /// </summary>
    public const string ERR_QUEUE_FULL = "ERR_QUEUE_FULL";

    // ===== Validation Errors =====

    /// <summary>
    /// NOKA parameter is invalid or malformed.
    /// </summary>
    public const string ERR_INVALID_NOKA = "ERR_INVALID_NOKA";

    /// <summary>
    /// Required HTTP parameter is missing.
    /// </summary>
    public const string ERR_MISSING_PARAMETER = "ERR_MISSING_PARAMETER";

    // ===== System Errors =====

    /// <summary>
    /// Unrecoverable internal error requiring agent restart.
    /// </summary>
    public const string ERR_CRITICAL_INTERNAL = "ERR_CRITICAL_INTERNAL";

    /// <summary>
    /// Configuration file is invalid or malformed.
    /// </summary>
    public const string ERR_INVALID_CONFIG = "ERR_INVALID_CONFIG";

    /// <summary>
    /// FlaUI automation library error.
    /// </summary>
    public const string ERR_FLAUI_ERROR = "ERR_FLAUI_ERROR";
}