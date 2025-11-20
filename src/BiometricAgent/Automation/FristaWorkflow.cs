using BiometricAgent.Configuration;
using BiometricAgent.Models;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using Serilog;
using System.Diagnostics;

namespace BiometricAgent.Automation;

/// <summary>
/// Implements the complete automation workflow for Frista.exe application.
/// Handles: Launch → Login → Inject NOKA → Trigger Verification
/// </summary>
public sealed class FristaWorkflow
{
    private readonly ApplicationConfig _config;
    private readonly CredentialsConfig _credentials;
    private readonly UIAutomationConfig _uiConfig;
    private Process? _process;
    private AutomationBase? _automation;

    public FristaWorkflow(
        ApplicationConfig config,
        CredentialsConfig credentials,
        UIAutomationConfig uiConfig)
    {
        _config = config;
        _credentials = credentials;
        _uiConfig = uiConfig;
    }

    /// <summary>
    /// Executes the complete Frista automation workflow.
    /// </summary>
    public async Task<AutomationResult> ExecuteAsync(AutomationRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        var correlationId = request.CorrelationId;

        Log.Information("[{CorrelationId}] Starting Frista workflow for NOKA={NOKA}",
            correlationId, request.NoPeserta);

        try
        {
            // Step 1: Launch Frista.exe
            await LaunchFristaAsync(correlationId);

            // Step 2: Perform auto-login
            await AutoLoginAsync(correlationId);

            // Step 3: Inject NOKA into input field
            await InjectNokaAsync(correlationId, request.NoPeserta);

            // Step 4: Trigger biometric verification
            await TriggerVerificationAsync(correlationId);

            stopwatch.Stop();

            Log.Information("[{CorrelationId}] Frista workflow completed successfully in {DurationMs}ms",
                correlationId, stopwatch.ElapsedMilliseconds);

            return AutomationResult.Success(request, stopwatch.ElapsedMilliseconds);
        }
        catch (TimeoutException ex)
        {
            stopwatch.Stop();
            Log.Error(ex, "[{CorrelationId}] Frista workflow timed out after {DurationMs}ms",
                correlationId, stopwatch.ElapsedMilliseconds);

            return AutomationResult.Failure(
                request,
                ErrorCodes.ERR_AUTOMATION_TIMEOUT,
                $"Workflow timed out: {ex.Message}",
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Log.Error(ex, "[{CorrelationId}] Frista workflow failed after {DurationMs}ms",
                correlationId, stopwatch.ElapsedMilliseconds);

            return AutomationResult.Failure(
                request,
                ErrorCodes.ERR_AUTOMATION_TIMEOUT,
                $"Automation failed: {ex.Message}",
                stopwatch.ElapsedMilliseconds);
        }
        finally
        {
            CleanupResources();
        }
    }

    /// <summary>
    /// Step 1: Launch Frista.exe and wait for main window.
    /// </summary>
    private async Task LaunchFristaAsync(string correlationId)
    {
        Log.Information("[{CorrelationId}] Launching Frista from {Path}",
            correlationId, _config.ExecutablePath);

        if (!File.Exists(_config.ExecutablePath))
        {
            throw new FileNotFoundException($"Frista executable not found: {_config.ExecutablePath}");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _config.ExecutablePath,
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(_config.ExecutablePath)
        };

        _process = Process.Start(startInfo);

        if (_process == null)
        {
            throw new InvalidOperationException("Failed to start Frista process");
        }

        Log.Information("[{CorrelationId}] Frista process started with PID={ProcessId}",
            correlationId, _process.Id);

        // Initialize FlaUI automation
        _automation = new UIA3Automation();

        // Wait for main window to appear
        var timeout = TimeSpan.FromSeconds(_config.StartupTimeoutSeconds);
        var endTime = DateTime.UtcNow.Add(timeout);

        Window? mainWindow = null;

        while (DateTime.UtcNow < endTime)
        {
            try
            {
                var element = _automation.GetDesktop().FindFirstChild(cf => cf.ByProcessId(_process.Id));

                if (element != null)
                {
                    mainWindow = element.AsWindow();
                    if (mainWindow != null && !string.IsNullOrEmpty(mainWindow.Title))
                    {
                        Log.Information("[{CorrelationId}] Frista main window detected: {WindowTitle}",
                            correlationId, mainWindow.Title);
                        return;
                    }
                }
            }
            catch
            {
                // Window not ready yet
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"Frista main window did not appear within {timeout.TotalSeconds} seconds");
    }

    /// <summary>
    /// Step 2: Perform automatic login using configured credentials.
    /// </summary>
    private async Task AutoLoginAsync(string correlationId)
    {
        Log.Information("[{CorrelationId}] Performing auto-login to Frista",
            correlationId);

        if (_automation == null || _process == null)
        {
            throw new InvalidOperationException("Automation not initialized");
        }

        var mainWindow = _automation.GetDesktop().FindFirstChild(cf => cf.ByProcessId(_process.Id));

        if (mainWindow == null)
        {
            throw new InvalidOperationException("Main window not found");
        }

        // Find the first TkChild input field (username field)
        var usernameField = mainWindow.FindFirstDescendant(cf => cf.ByClassName("TkChild").And(cf.ByControlType(ControlType.Pane)));
        if (usernameField == null)
        {
            throw new InvalidOperationException("Username field not found");
        }

        Log.Debug("[{CorrelationId}] Username field found, starting keyboard-based login", correlationId);

        // Step 1: Focus on username field and enter username
        usernameField.Focus();
        await Task.Delay(200);
        usernameField.AsTextBox().Text = _credentials.FristaUsername;
        Log.Debug("[{CorrelationId}] Username entered", correlationId);

        await Task.Delay(300);

        // Step 2: Press Tab to move to password field
        FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.TAB);
        Log.Debug("[{CorrelationId}] Tab pressed (moving to password field)", correlationId);

        await Task.Delay(300);

        // Step 3: Enter password
        FlaUI.Core.Input.Keyboard.Type(_credentials.FristaPassword);
        Log.Debug("[{CorrelationId}] Password entered", correlationId);

        await Task.Delay(300);

        // Step 4: Find and click Login button by ClassName "Button"
        var loginButton = mainWindow.FindFirstDescendant(cf => cf.ByClassName("Button").And(cf.ByControlType(ControlType.Button)));
        if (loginButton == null)
        {
            throw new InvalidOperationException("Login button not found");
        }

        Log.Debug("[{CorrelationId}] Login button found, clicking it", correlationId);
        loginButton.Click();
        Log.Information("[{CorrelationId}] Login button clicked", correlationId);

        // Wait for post-login UI (adjust based on actual Frista behavior)
        await Task.Delay(_uiConfig.ElementWaitTimeMs * 2);

        Log.Information("[{CorrelationId}] Auto-login completed successfully", correlationId);
    }

    /// <summary>
    /// Step 3: Inject NOKA (participant number) into input field.
    /// </summary>
    private async Task InjectNokaAsync(string correlationId, string noka)
    {
        Log.Information("[{CorrelationId}] Injecting NOKA={NOKA} into Frista",
            correlationId, noka);

        if (_automation == null || _process == null)
        {
            throw new InvalidOperationException("Automation not initialized");
        }

        var mainWindow = _automation.GetDesktop().FindFirstChild(cf => cf.ByProcessId(_process.Id));

        if (mainWindow == null)
        {
            throw new InvalidOperationException("Main window not found");
        }

        // Find the TkChild pane (NOKA input field) on the main screen
        var nokaField = mainWindow.FindFirstDescendant(cf => cf.ByClassName("TkChild").And(cf.ByControlType(ControlType.Pane)));
        if (nokaField == null)
        {
            throw new InvalidOperationException("NOKA input field not found");
        }

        Log.Debug("[{CorrelationId}] NOKA field found, clicking to focus", correlationId);

        // Click on the field to ensure it has focus, then clear and type NOKA
        nokaField.Click();
        await Task.Delay(300);

        // Clear any existing content (Ctrl+A then Delete)
        FlaUI.Core.Input.Keyboard.TypeSimultaneously(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL, FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_A);
        await Task.Delay(100);
        FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.DELETE);
        await Task.Delay(100);

        // Type the NOKA
        FlaUI.Core.Input.Keyboard.Type(noka);
        Log.Information("[{CorrelationId}] NOKA typed successfully: {NOKA}", correlationId, noka);

        await Task.Delay(_uiConfig.ElementWaitTimeMs / 2);
    }

    /// <summary>
    /// Step 4: Trigger biometric verification process.
    /// </summary>
    private async Task TriggerVerificationAsync(string correlationId)
    {
        Log.Information("[{CorrelationId}] Triggering biometric verification in Frista",
            correlationId);

        if (_automation == null || _process == null)
        {
            throw new InvalidOperationException("Automation not initialized");
        }

        var mainWindow = _automation.GetDesktop().FindFirstChild(cf => cf.ByProcessId(_process.Id));

        if (mainWindow == null)
        {
            throw new InvalidOperationException("Main window not found");
        }

        // Find the "Ambil Foto" button by ClassName "Button"
        var verifyButton = mainWindow.FindFirstDescendant(cf => cf.ByClassName("Button").And(cf.ByControlType(ControlType.Button)));
        if (verifyButton == null)
        {
            throw new InvalidOperationException("Ambil Foto button not found");
        }

        Log.Debug("[{CorrelationId}] Ambil Foto button found, clicking it", correlationId);
        verifyButton.Click();
        Log.Information("[{CorrelationId}] Verification triggered", correlationId);

        // Wait for verification result (adjust timeout based on actual Frista behavior)
        await Task.Delay(_uiConfig.ElementWaitTimeMs * 3);

        Log.Information("[{CorrelationId}] Verification completed", correlationId);
    }

    /// <summary>
    /// Finds a UI element using multiple fallback strategies.
    /// </summary>
    private AutomationElement? FindElement(
        AutomationElement parent,
        string description,
        string automationId,
        string name,
        ControlType controlType)
    {
        Log.Debug("Searching for element: {Description} (AutomationId={AutomationId}, Name={Name}, ControlType={ControlType})",
            description, automationId, name, controlType);

        for (int attempt = 0; attempt < _config.RetryAttempts; attempt++)
        {
            // Strategy 1: AutomationId (preferred)
            var element = parent.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
            if (element != null)
            {
                Log.Debug("Found element by AutomationId: {Description}", description);
                return element;
            }

            // Strategy 2: Name
            element = parent.FindFirstDescendant(cf => cf.ByName(name));
            if (element != null)
            {
                Log.Debug("Found element by Name: {Description}", description);
                return element;
            }

            // Strategy 3: ControlType + Name
            element = parent.FindFirstDescendant(cf => cf.ByControlType(controlType).And(cf.ByName(name)));
            if (element != null)
            {
                Log.Debug("Found element by ControlType+Name: {Description}", description);
                return element;
            }

            if (attempt < _config.RetryAttempts - 1)
            {
                Thread.Sleep(_config.RetryDelayMs);
            }
        }

        Log.Warning("Element not found after {Attempts} attempts: {Description}",
            _config.RetryAttempts, description);

        return null;
    }

    /// <summary>
    /// Cleanup automation resources and optionally terminate process.
    /// </summary>
    private void CleanupResources()
    {
        try
        {
            _automation?.Dispose();
            _automation = null;

            // Note: Not terminating process here - Frista window should remain open for user
            // Process will be terminated by explicit /stop_exe endpoint call
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error during cleanup");
        }
    }
}
