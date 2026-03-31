using BiometricAgent.Configuration;
using BiometricAgent.Models;
using BiometricAgent.Services;
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
    private readonly int _maxErrorDialogAttempts;
    private Process? _process;
    private AutomationBase? _automation;

    public FristaWorkflow(
        ApplicationConfig config,
        CredentialsConfig credentials,
        UIAutomationConfig uiConfig,
        ErrorHandlingConfig errorHandling)
    {
        _config = config;
        _credentials = credentials;
        _uiConfig = uiConfig;
        _maxErrorDialogAttempts = Math.Max(1, errorHandling.MaxErrorRetries);
    }

    /// <summary>
    /// Executes the complete Frista automation workflow with intelligent state detection.
    /// Detects if app is already running and determines current window state (login vs main).
    /// </summary>
    public async Task<AutomationResult> ExecuteAsync(AutomationRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        var correlationId = request.CorrelationId;

        Log.Information("[{CorrelationId}] Starting Frista workflow for NOKA={NOKA}",
            correlationId, request.NoPeserta);

        try
        {
            // Step 1: Launch Frista.exe or attach to existing process
            var windowState = await LaunchOrAttachFristaAsync(correlationId);

            // Step 2: Perform auto-login (only if on login window)
            if (windowState == WindowState.LoginWindow)
            {
                Log.Information("[{CorrelationId}] Detected login window - performing authentication",
                    correlationId);
                await AutoLoginAsync(correlationId, request);
            }
            else
            {
                Log.Information("[{CorrelationId}] Detected main window - skipping authentication",
                    correlationId);
            }

            // Step 3: Inject NOKA into input field
            await InjectNokaAsync(correlationId, request.NoPeserta);

            // Step 4: Trigger biometric verification
            await TriggerVerificationAsync(correlationId);

            // Step 5: Watch for error dialogs and retry until process is stopped
            await WatchForErrorDialogAsync(correlationId, request.NoPeserta);

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
    /// Window state enumeration for intelligent workflow branching.
    /// </summary>
    private enum WindowState
    {
        LoginWindow,
        MainWindow
    }

    /// <summary>
    /// Step 1: Kill any existing Frista processes and launch a fresh instance.
    /// </summary>
    private async Task<WindowState> LaunchOrAttachFristaAsync(string correlationId)
    {
        // Initialize FlaUI automation
        _automation = new UIA3Automation();

        // Kill any existing Frista processes before launching fresh
        var existingProcesses = Process.GetProcessesByName("Frista").ToArray();

        if (existingProcesses.Length > 0)
        {
            Log.Information("[{CorrelationId}] Found {Count} existing Frista process(es), terminating them before starting new instance",
                correlationId, existingProcesses.Length);

            foreach (var existingProcess in existingProcesses)
            {
                try
                {
                    Log.Debug("[{CorrelationId}] Terminating Frista process PID={ProcessId}",
                        correlationId, existingProcess.Id);
                    existingProcess.Kill();
                    existingProcess.WaitForExit(2000);
                    Log.Information("[{CorrelationId}] Frista process PID={ProcessId} terminated",
                        correlationId, existingProcess.Id);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[{CorrelationId}] Failed to terminate Frista process PID={ProcessId}",
                        correlationId, existingProcess.Id);
                }
            }

            await Task.Delay(500);
        }

        // Launch new instance
        Log.Information("[{CorrelationId}] Launching Frista from {Path}",
            correlationId, _config.ExecutablePath);

        if (!File.Exists(_config.ExecutablePath))
        {
            throw new FileNotFoundException($"Frista executable not found: {_config.ExecutablePath}");
        }

        _process = InteractiveProcessLauncher.LaunchInUserSession(
            _config.ExecutablePath,
            null,
            Path.GetDirectoryName(_config.ExecutablePath));

        if (_process == null)
        {
            throw new InvalidOperationException("Failed to start Frista process. " +
                "If running as a Windows Service, ensure a user is logged in to the console.");
        }

        Log.Information("[{CorrelationId}] Frista process started with PID={ProcessId}",
            correlationId, _process.Id);

        // Wait for window to appear
        var timeout = TimeSpan.FromSeconds(_config.StartupTimeoutSeconds);
        var endTime = DateTime.UtcNow.Add(timeout);

        while (DateTime.UtcNow < endTime)
        {
            try
            {
                var element = _automation.GetDesktop().FindFirstChild(cf => cf.ByProcessId(_process.Id));

                if (element != null)
                {
                    var window = element.AsWindow();
                    if (window != null && !string.IsNullOrEmpty(window.Title))
                    {
                        Log.Information("[{CorrelationId}] Frista window detected: {WindowTitle}",
                            correlationId, window.Title);

                        return await DetectWindowStateAsync(correlationId);
                    }
                }
            }
            catch
            {
                // Window not ready yet
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"Frista window did not appear within {timeout.TotalSeconds} seconds");
    }

    /// <summary>
    /// Detects the current window state (Login vs Main) based on window title and UI elements.
    /// </summary>
    private async Task<WindowState> DetectWindowStateAsync(string correlationId)
    {
        if (_automation == null || _process == null)
        {
            throw new InvalidOperationException("Automation not initialized");
        }

        await Task.Delay(500); // Allow UI to stabilize

        var window = _automation.GetDesktop().FindFirstChild(cf => cf.ByProcessId(_process.Id));

        if (window == null)
        {
            throw new InvalidOperationException("Frista window not found");
        }

        var windowTitle = window.Name?.ToLower() ?? string.Empty;

        // Strategy 1: Check window title for "login" keyword
        if (windowTitle.Contains("login"))
        {
            Log.Information("[{CorrelationId}] Window state detected: LOGIN (based on title '{WindowTitle}')",
                correlationId, window.Name);
            return WindowState.LoginWindow;
        }

        // Strategy 2: Look for login-specific UI elements (username/password fields)
        var usernameField = window.FindFirstDescendant(cf => 
            cf.ByClassName("TkChild").And(cf.ByControlType(ControlType.Pane)));

        var loginButton = window.FindFirstDescendant(cf => 
            cf.ByClassName("Button").And(cf.ByControlType(ControlType.Button)));

        if (usernameField != null && loginButton != null)
        {
            Log.Information("[{CorrelationId}] Window state detected: LOGIN (found username field and login button)",
                correlationId);
            return WindowState.LoginWindow;
        }

        // Strategy 3: If no login indicators, assume main window
        Log.Information("[{CorrelationId}] Window state detected: MAIN (no login indicators found, title '{WindowTitle}')",
            correlationId, window.Name);
        return WindowState.MainWindow;
    }

    /// <summary>
    /// Brings the Frista window to the foreground, activating it if minimized or in background.
    /// </summary>
    private async Task BringWindowToFrontAsync(string correlationId)
    {
        if (_automation == null || _process == null)
        {
            throw new InvalidOperationException("Automation not initialized");
        }

        try
        {
            var window = _automation.GetDesktop().FindFirstChild(cf => cf.ByProcessId(_process.Id));

            if (window == null)
            {
                Log.Warning("[{CorrelationId}] Could not find Frista window to bring to front", correlationId);
                return;
            }

            var mainWindow = window.AsWindow();
            
            if (mainWindow != null)
            {
                // Check if window is minimized and restore it
                try
                {
                    var windowPattern = mainWindow.Patterns.Window.PatternOrDefault;
                    if (windowPattern != null)
                    {
                        var state = windowPattern.WindowVisualState.Value;
                        if (state == FlaUI.Core.Definitions.WindowVisualState.Minimized)
                        {
                            Log.Information("[{CorrelationId}] Restoring minimized Frista window", correlationId);
                            windowPattern.SetWindowVisualState(FlaUI.Core.Definitions.WindowVisualState.Normal);
                            await Task.Delay(300); // Wait for restore animation
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "[{CorrelationId}] Could not check/restore window state, continuing", correlationId);
                }

                // Bring to foreground
                Log.Information("[{CorrelationId}] Bringing Frista window to foreground: '{WindowTitle}'",
                    correlationId, mainWindow.Title);
                mainWindow.SetForeground();
                await Task.Delay(200); // Wait for focus

                Log.Information("[{CorrelationId}] Frista window activated successfully", correlationId);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[{CorrelationId}] Failed to bring Frista window to front, continuing anyway", correlationId);
            // Don't throw - window activation is best-effort
        }
    }

    /// <summary>
    /// Step 2: Perform automatic login using credentials from request or config fallback.
    /// </summary>
    private async Task AutoLoginAsync(string correlationId, AutomationRequest request)
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

        // Determine which credentials to use (request overrides config)
        var username = !string.IsNullOrWhiteSpace(request.Username) 
            ? request.Username 
            : _credentials.FristaUsername;
        var password = !string.IsNullOrWhiteSpace(request.Password) 
            ? request.Password 
            : _credentials.FristaPassword;

        Log.Debug("[{CorrelationId}] Using credentials: Username={Username} (Source={Source})",
            correlationId, username, !string.IsNullOrWhiteSpace(request.Username) ? "request" : "config");

        // Step 1: Click on username field to ensure focus, then type username using keyboard
        Log.Debug("[{CorrelationId}] Clicking on username field to focus", correlationId);
        usernameField.Click();
        await Task.Delay(300);

        // Clear any existing content first (Ctrl+A then Delete)
        FlaUI.Core.Input.Keyboard.TypeSimultaneously(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL, FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_A);
        await Task.Delay(100);
        FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.DELETE);
        await Task.Delay(100);

        // Type the username using keyboard input (more reliable than TextBox.Text for TkChild)
        FlaUI.Core.Input.Keyboard.Type(username);
        Log.Debug("[{CorrelationId}] Username typed: {Username}", correlationId, username);

        await Task.Delay(300);

        // Step 2: Press Tab to move to password field
        FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.TAB);
        Log.Debug("[{CorrelationId}] Tab pressed (moving to password field)", correlationId);

        await Task.Delay(300);

        // Step 3: Enter password using keyboard
        FlaUI.Core.Input.Keyboard.Type(password);
        Log.Debug("[{CorrelationId}] Password typed", correlationId);

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

        Log.Debug("[{CorrelationId}] Ambil Foto button found, waiting 3 seconds before clicking", correlationId);
        
        // Wait 3 seconds before clicking as requested
        await Task.Delay(3000);
        
        // Click twice with 0.5s gap to ensure capture starts
        verifyButton.Click();
        await Task.Delay(500);
        verifyButton.Click();
        Log.Information("[{CorrelationId}] Verification triggered (double click with 0.5s gap)", correlationId);

        // Wait briefly for verification to start
        await Task.Delay(_uiConfig.ElementWaitTimeMs);

        Log.Information("[{CorrelationId}] Verification triggered, starting error dialog watch loop", correlationId);
    }

    /// <summary>
    /// Watches for error dialogs from Frista and handles retry logic.
    /// Continues until the Frista process is terminated (via stop_exe).
    /// </summary>
    private async Task WatchForErrorDialogAsync(string correlationId, string noka)
    {
        Log.Information("[{CorrelationId}] Starting error dialog watch loop", correlationId);

        if (_automation == null || _process == null)
        {
            throw new InvalidOperationException("Automation not initialized");
        }

        // Known error dialog names from Frista (exact matches)
        var knownErrorDialogs = new[] { "Hasil Pengenalan Wajah" };
        var retryAttempts = 0;
        var maxAttemptsReached = false;
        var successDetected = false;

        while (!_process.HasExited && retryAttempts < _maxErrorDialogAttempts)
        {
            try
            {
                var desktop = _automation.GetDesktop();
                AutomationElement? errorDialog = null;
                string? detectedDialogName = null;

                // Strategy 1: Check for known error dialogs by exact name
                foreach (var dialogName in knownErrorDialogs)
                {
                    errorDialog = desktop.FindFirstDescendant(cf => 
                        cf.ByName(dialogName).And(cf.ByControlType(ControlType.Window)));
                    
                    if (errorDialog != null)
                    {
                        detectedDialogName = dialogName;
                        break;
                    }
                }

                // Strategy 2: Check for any dialog with "Error" in its name
                if (errorDialog == null)
                {
                    var allWindows = desktop.FindAllDescendants(cf => cf.ByControlType(ControlType.Window));
                    foreach (var window in allWindows)
                    {
                        var windowName = window.Name ?? string.Empty;
                        if (windowName.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
                            windowName.Contains("Gagal", StringComparison.OrdinalIgnoreCase) ||
                            windowName.Contains("Failed", StringComparison.OrdinalIgnoreCase))
                        {
                            errorDialog = window;
                            detectedDialogName = windowName;
                            break;
                        }
                    }
                }

                if (errorDialog != null && detectedDialogName != null)
                {
                    // Check for success message inside the dialog before treating it as an error
                    var dialogTextElements = errorDialog.FindAllDescendants(cf => cf.ByControlType(ControlType.Text));
                    var dialogTextContent = string.Empty;

                    foreach (var textElement in dialogTextElements)
                    {
                        if (!string.IsNullOrWhiteSpace(textElement.Name))
                        {
                            dialogTextContent += textElement.Name + " ";
                        }
                    }

                    dialogTextContent = dialogTextContent.Trim();

                    var isSuccessDialog =
                        dialogTextContent.Contains("berhasil", StringComparison.OrdinalIgnoreCase) ||
                        dialogTextContent.Contains("peserta telah terdaftar", StringComparison.OrdinalIgnoreCase);

                    if (isSuccessDialog)
                    {
                        Log.Information("[{CorrelationId}] Success dialog detected: '{DialogName}' with message '{DialogText}'", correlationId, detectedDialogName, dialogTextContent);

                        var successOkButton = errorDialog.FindFirstDescendant(cf =>
                            cf.ByName("OK").And(cf.ByControlType(ControlType.Button)));

                        if (successOkButton != null)
                        {
                            successOkButton.Click();
                            await Task.Delay(100);
                        }

                        successDetected = true;
                        break;
                    }

                    Log.Warning("[{CorrelationId}] Error dialog detected: '{DialogName}'", correlationId, detectedDialogName);
                    retryAttempts++;
                    Log.Information("[{CorrelationId}] Error dialog retry attempt {Attempt}/{MaxAttempts}", correlationId, retryAttempts, _maxErrorDialogAttempts);

                    if (retryAttempts >= _maxErrorDialogAttempts)
                    {
                        Log.Warning("[{CorrelationId}] Max error dialog attempts reached, exiting watch loop", correlationId);
                        maxAttemptsReached = true;
                        break;
                    }

                    // Find and click the OK button
                    var okButton = errorDialog.FindFirstDescendant(cf => 
                        cf.ByName("OK").And(cf.ByControlType(ControlType.Button)));

                    if (okButton != null)
                    {
                        Log.Information("[{CorrelationId}] Clicking OK button on error dialog", correlationId);
                        okButton.Click();
                        await Task.Delay(100); // Wait for dialog to close

                        // Check if process was killed before retrying
                        if (_process.HasExited)
                        {
                            Log.Information("[{CorrelationId}] Frista process exited after dismissing dialog, stopping retry", correlationId);
                            break;
                        }

                        // Re-inject NOKA and trigger verification again
                        Log.Information("[{CorrelationId}] Retrying - re-injecting NOKA and triggering verification", correlationId);
                        await InjectNokaAsync(correlationId, noka);
                        
                        // Check again before triggering verification
                        if (_process.HasExited)
                        {
                            Log.Information("[{CorrelationId}] Frista process exited after NOKA injection, stopping retry", correlationId);
                            break;
                        }
                        
                        await TriggerVerificationAsync(correlationId);
                    }
                    else
                    {
                        Log.Warning("[{CorrelationId}] OK button not found in error dialog", correlationId);
                    }
                }
            }
            catch (Exception ex)
            {
                // Process might have exited, which is expected when stop_exe is called
                if (_process.HasExited)
                {
                    Log.Information("[{CorrelationId}] Frista process has exited, stopping watch loop", correlationId);
                    break;
                }
                Log.Debug(ex, "[{CorrelationId}] Error while checking for dialogs (may be transient)", correlationId);
            }

            // Poll every 500ms
            await Task.Delay(500);
        }

        if (successDetected)
        {
            Log.Information("[{CorrelationId}] Error dialog watch loop ended (success dialog detected)", correlationId);
        }
        else if (maxAttemptsReached)
        {
            Log.Information("[{CorrelationId}] Error dialog watch loop ended (max attempts reached)", correlationId);
        }
        else
        {
            Log.Information("[{CorrelationId}] Error dialog watch loop ended (process exited)", correlationId);
        }
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
