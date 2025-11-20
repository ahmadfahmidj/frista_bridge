namespace BiometricAgent.Models;

/// <summary>
/// Defines a sequence of automation steps for a workflow.
/// </summary>
public sealed class AutomationWorkflow
{
    /// <summary>
    /// Workflow type (Frista or Finger).
    /// </summary>
    public WorkflowType Type { get; set; }

    /// <summary>
    /// Ordered list of automation steps to execute.
    /// </summary>
    public List<AutomationStep> Steps { get; set; } = new();

    /// <summary>
    /// Maximum allowed execution time for entire workflow (seconds).
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Workflow display name for logging.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
}

/// <summary>
/// Represents a single step in an automation workflow.
/// </summary>
public sealed class AutomationStep
{
    /// <summary>
    /// Step sequence number (1-based).
    /// </summary>
    public int StepNumber { get; set; }

    /// <summary>
    /// Action to perform: Click, TypeText, WaitFor, etc.
    /// </summary>
    public StepAction Action { get; set; }

    /// <summary>
    /// UI element to interact with (if applicable).
    /// </summary>
    public UIElementReference? TargetElement { get; set; }

    /// <summary>
    /// Input value for TypeText actions.
    /// </summary>
    public string? InputValue { get; set; }

    /// <summary>
    /// Wait duration for WaitFor actions (milliseconds).
    /// </summary>
    public int WaitDurationMs { get; set; }

    /// <summary>
    /// Human-readable description for logging.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Indicates if step failure should abort workflow.
    /// </summary>
    public bool IsCritical { get; set; } = true;
}

/// <summary>
/// Automation step action types.
/// </summary>
public enum StepAction
{
    /// <summary>
    /// Click a UI element (button, link, etc.).
    /// </summary>
    Click = 0,

    /// <summary>
    /// Type text into an input field.
    /// </summary>
    TypeText = 1,

    /// <summary>
    /// Wait for UI element to appear.
    /// </summary>
    WaitForElement = 2,

    /// <summary>
    /// Wait for a fixed duration.
    /// </summary>
    WaitForDelay = 3,

    /// <summary>
    /// Verify UI element exists.
    /// </summary>
    VerifyElement = 4,

    /// <summary>
    /// Read text from UI element.
    /// </summary>
    ReadText = 5,

    /// <summary>
    /// Focus on UI element.
    /// </summary>
    Focus = 6,

    /// <summary>
    /// Press keyboard key.
    /// </summary>
    PressKey = 7
}