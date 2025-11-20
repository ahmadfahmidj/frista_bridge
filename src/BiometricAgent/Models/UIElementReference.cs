namespace BiometricAgent.Models;

/// <summary>
/// Defines a UI element selector for FlaUI automation.
/// </summary>
public sealed class UIElementReference
{
    /// <summary>
    /// Element identifier using AutomationId property.
    /// </summary>
    public string? AutomationId { get; set; }

    /// <summary>
    /// Element identifier using Name property.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Element identifier using ControlType (e.g., Button, TextBox).
    /// </summary>
    public string? ControlType { get; set; }

    /// <summary>
    /// Human-readable description for logging purposes.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Selector strategy to use: Primary, Fallback1, Fallback2.
    /// </summary>
    public SelectorStrategy Strategy { get; set; } = SelectorStrategy.Primary;

    /// <summary>
    /// Creates a reference using AutomationId (preferred).
    /// </summary>
    public static UIElementReference ByAutomationId(string automationId, string description)
    {
        return new UIElementReference
        {
            AutomationId = automationId,
            Description = description,
            Strategy = SelectorStrategy.Primary
        };
    }

    /// <summary>
    /// Creates a reference using Name property.
    /// </summary>
    public static UIElementReference ByName(string name, string description)
    {
        return new UIElementReference
        {
            Name = name,
            Description = description,
            Strategy = SelectorStrategy.Fallback1
        };
    }

    /// <summary>
    /// Creates a reference using ControlType.
    /// </summary>
    public static UIElementReference ByControlType(string controlType, string description)
    {
        return new UIElementReference
        {
            ControlType = controlType,
            Description = description,
            Strategy = SelectorStrategy.Fallback2
        };
    }
}

/// <summary>
/// UI element selector strategy enumeration.
/// </summary>
public enum SelectorStrategy
{
    /// <summary>
    /// Primary strategy (AutomationId preferred).
    /// </summary>
    Primary = 0,

    /// <summary>
    /// First fallback strategy (Name).
    /// </summary>
    Fallback1 = 1,

    /// <summary>
    /// Second fallback strategy (ControlType).
    /// </summary>
    Fallback2 = 2
}