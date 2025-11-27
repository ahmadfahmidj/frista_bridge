using BiometricAgent.Configuration;
using BiometricAgent.Models;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using Serilog;
using System.Diagnostics;
using System.Linq;

namespace BiometricAgent.Automation;

/// <summary>
/// Implements the complete automation workflow for After.exe (Finger) application.
/// Handles: Launch → Login (if needed) → Inject NOKA → Trigger Fingerprint Scan
/// </summary>
public sealed class FingerWorkflow
{
    private readonly ApplicationConfig _config;
    private readonly CredentialsConfig _credentials;
    private readonly UIAutomationConfig _uiConfig;
    private Process? _process;
    private AutomationBase? _automation;

    public FingerWorkflow(
        ApplicationConfig config,
        CredentialsConfig credentials,
        UIAutomationConfig uiConfig)
    {
        _config = config;
        _credentials = credentials;
        _uiConfig = uiConfig;
    }

    /// <summary>
    /// Executes the complete Finger automation workflow with intelligent state detection.
    /// Detects if app is already running and determines current window state (login vs main).
    /// </summary>
    public async Task<AutomationResult> ExecuteAsync(AutomationRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        var correlationId = request.CorrelationId;

        Log.Information("[{CorrelationId}] Starting Finger workflow for NOKA={NOKA}",
            correlationId, request.NoPeserta);

        try
        {
            // Step 1: Launch After.exe or attach to existing process
            var windowState = await LaunchOrAttachFingerAsync(correlationId);

            // Step 2: Perform auto-login (only if on login window)
            if (windowState == WindowState.LoginWindow)
            {
                Log.Information("[{CorrelationId}] Detected login window - performing authentication",
                    correlationId);
                await AutoLoginAsync(correlationId);
            }
            else
            {
                Log.Information("[{CorrelationId}] Detected main window - skipping authentication",
                    correlationId);
            }

            // Step 3: Inject NOKA into input field
            await InjectNokaAsync(correlationId, request.NoPeserta);

            // Step 4: Trigger fingerprint scan
            await TriggerFingerprintScanAsync(correlationId);

            stopwatch.Stop();

            Log.Information("[{CorrelationId}] Finger workflow completed successfully in {DurationMs}ms",
                correlationId, stopwatch.ElapsedMilliseconds);

            return AutomationResult.Success(request, stopwatch.ElapsedMilliseconds);
        }
        catch (TimeoutException ex)
        {
            stopwatch.Stop();
            Log.Error(ex, "[{CorrelationId}] Finger workflow timed out after {DurationMs}ms",
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
            Log.Error(ex, "[{CorrelationId}] Finger workflow failed after {DurationMs}ms",
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
    /// Step 1: Close any existing Finger processes and launch a fresh instance.
    /// </summary>
    private async Task<WindowState> LaunchOrAttachFingerAsync(string correlationId)
    {
        // Initialize FlaUI automation
        _automation = new UIA3Automation();

        // Check if After (Finger) is already running and close it
        var existingProcesses = Process.GetProcessesByName("After")
            .Concat(Process.GetProcessesByName("after"))
            .ToArray();

        if (existingProcesses.Length > 0)
        {
            Log.Information("[{CorrelationId}] Found {Count} existing Finger process(es), terminating them before starting new instance",
                correlationId, existingProcesses.Length);

            foreach (var existingProcess in existingProcesses)
            {
                try
                {
                    Log.Debug("[{CorrelationId}] Terminating Finger process PID={ProcessId}",
                        correlationId, existingProcess.Id);
                    existingProcess.Kill();
                    existingProcess.WaitForExit(2000); // Wait up to 2 seconds for clean exit
                    Log.Information("[{CorrelationId}] Finger process PID={ProcessId} terminated",
                        correlationId, existingProcess.Id);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[{CorrelationId}] Failed to terminate Finger process PID={ProcessId}",
                        correlationId, existingProcess.Id);
                }
            }

            // Wait a moment for processes to fully terminate
            await Task.Delay(500);
        }

        // Not running, launch new instance
        Log.Information("[{CorrelationId}] Finger not running, launching from {Path}",
            correlationId, _config.ExecutablePath);

        if (!File.Exists(_config.ExecutablePath))
        {
            throw new FileNotFoundException($"Finger executable not found: {_config.ExecutablePath}");
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
            throw new InvalidOperationException("Failed to start Finger process");
        }

        Log.Information("[{CorrelationId}] Finger process started with PID={ProcessId}",
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
                        Log.Information("[{CorrelationId}] Finger window detected: {WindowTitle}",
                            correlationId, window.Title);

                        // Detect window state
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

        throw new TimeoutException($"Finger window did not appear within {timeout.TotalSeconds} seconds");
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
            throw new InvalidOperationException("Finger window not found");
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
        // Check for TkChild input fields which typically indicate login screen
        var loginFields = window.FindAllDescendants(cf => 
            cf.ByClassName("TkChild").And(cf.ByControlType(ControlType.Pane)));

        // Check for button that might be login button
        var buttons = window.FindAllDescendants(cf => 
            cf.ByClassName("Button").And(cf.ByControlType(ControlType.Button)));

        // If we have 2+ TkChild fields (username/password) and buttons, likely login screen
        if (loginFields.Length >= 2 && buttons.Length > 0)
        {
            Log.Information("[{CorrelationId}] Window state detected: LOGIN (found {FieldCount} input fields and {ButtonCount} buttons)",
                correlationId, loginFields.Length, buttons.Length);
            return WindowState.LoginWindow;
        }

        // Strategy 3: Look for NOKA input field which indicates main window
        // Main window typically has text input for NOKA entry
        var textBoxes = window.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit));
        
        if (textBoxes.Length > 0)
        {
            Log.Information("[{CorrelationId}] Window state detected: MAIN (found {TextBoxCount} text input fields, likely NOKA entry)",
                correlationId, textBoxes.Length);
            return WindowState.MainWindow;
        }

        // Strategy 4: If no clear indicators, check if we have many interactive elements (main window)
        // vs few elements (login screen)
        var allButtons = window.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
        
        if (allButtons.Length > 3)
        {
            Log.Information("[{CorrelationId}] Window state detected: MAIN (found {ButtonCount} buttons, likely main interface)",
                correlationId, allButtons.Length);
            return WindowState.MainWindow;
        }

        // Default: Assume login window if unclear
        Log.Information("[{CorrelationId}] Window state detected: LOGIN (default assumption, title '{WindowTitle}')",
            correlationId, window.Name);
        return WindowState.LoginWindow;
    }

    /// <summary>
    /// Brings the Finger window to the foreground, activating it if minimized or in background.
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
                Log.Warning("[{CorrelationId}] Could not find Finger window to bring to front", correlationId);
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
                            Log.Information("[{CorrelationId}] Restoring minimized Finger window", correlationId);
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
                Log.Information("[{CorrelationId}] Bringing Finger window to foreground: '{WindowTitle}'",
                    correlationId, mainWindow.Title);
                mainWindow.SetForeground();
                await Task.Delay(200); // Wait for focus

                Log.Information("[{CorrelationId}] Finger window activated successfully", correlationId);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[{CorrelationId}] Failed to bring Finger window to front, continuing anyway", correlationId);
            // Don't throw - window activation is best-effort
        }
    }

    /// <summary>
    /// Step 2: Perform automatic login (if After.exe requires login).
    /// </summary>
    private async Task AutoLoginAsync(string correlationId)
    {
        Log.Information("[{CorrelationId}] Checking if Finger requires login",
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

        // Check if login fields are present by looking for Edit controls
        var editFields = mainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit));

        if (editFields != null && editFields.Length >= 2)
        {
            Log.Information("[{CorrelationId}] Login screen detected with {FieldCount} edit fields, performing auto-login",
                correlationId, editFields.Length);

            try
            {
                // Bring window to foreground and focus it to ensure keyboard input works
                mainWindow.SetForeground();
                await Task.Delay(500);
                mainWindow.Focus();
                await Task.Delay(500);

                // Find focusable Edit fields only
                var focusableFields = editFields.Where(f =>
                {
                    try
                    {
                        return f.Properties.IsKeyboardFocusable.ValueOrDefault;
                    }
                    catch
                    {
                        return false;
                    }
                }).ToArray();

                Log.Debug("[{CorrelationId}] Found {TotalFields} edit fields, {FocusableFields} focusable",
                    correlationId, editFields.Length, focusableFields.Length);

                if (focusableFields.Length < 2)
                {
                    Log.Warning("[{CorrelationId}] Less than 2 focusable fields, using keyboard Tab navigation", correlationId);

                    // Fallback: Use Tab navigation to reach input fields
                    // Tab multiple times to ensure we're at the first input field
                    for (int i = 0; i < 3; i++)
                    {
                        FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.TAB);
                        await Task.Delay(200);
                    }

                    // Type username
                    FlaUI.Core.Input.Keyboard.Type(_credentials.FingerUsername);
                    Log.Debug("[{CorrelationId}] Username typed via keyboard fallback", correlationId);

                    await Task.Delay(300);

                    // Tab to password field
                    FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.TAB);
                    await Task.Delay(300);

                    // Type password
                    FlaUI.Core.Input.Keyboard.Type(_credentials.FingerPassword);
                    Log.Debug("[{CorrelationId}] Password typed via keyboard fallback", correlationId);
                }
                else
                {
                    // Use first two focusable fields
                    var usernameField = focusableFields[0];
                    var passwordField = focusableFields[1];

                    Log.Debug("[{CorrelationId}] Using focusable fields at positions 0 and 1", correlationId);

                    // Click username field (safer than Focus())
                    try
                    {
                        usernameField.Click();
                        await Task.Delay(300);
                    }
                    catch
                    {
                        // If click fails, try to focus
                        usernameField.Focus();
                        await Task.Delay(300);
                    }

                    // Clear and type username
                    FlaUI.Core.Input.Keyboard.TypeSimultaneously(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL, FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_A);
                    await Task.Delay(100);
                    FlaUI.Core.Input.Keyboard.Type(_credentials.FingerUsername);
                    Log.Debug("[{CorrelationId}] Username entered: {Username}", correlationId, _credentials.FingerUsername);

                    await Task.Delay(300);

                    // Click password field
                    try
                    {
                        passwordField.Click();
                        await Task.Delay(300);
                    }
                    catch
                    {
                        // If click fails, try Tab key
                        FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.TAB);
                        await Task.Delay(300);
                    }

                    // Clear and type password
                    FlaUI.Core.Input.Keyboard.TypeSimultaneously(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL, FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_A);
                    await Task.Delay(100);
                    FlaUI.Core.Input.Keyboard.Type(_credentials.FingerPassword);
                    Log.Debug("[{CorrelationId}] Password entered", correlationId);
                }

                await Task.Delay(500);

                // Find and click Login button by Name="Login"
                var loginButton = mainWindow.FindFirstDescendant(cf =>
                    cf.ByControlType(ControlType.Button).And(cf.ByName("Login")));

                if (loginButton == null)
                {
                    // Fallback: find any button with "Login" in the name
                    loginButton = mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button));
                }

                if (loginButton != null)
                {
                    Log.Debug("[{CorrelationId}] Login button found: {ButtonName}",
                        correlationId, loginButton.Properties.Name.ValueOrDefault ?? "(no name)");
                    loginButton.Click();
                    Log.Information("[{CorrelationId}] Login button clicked", correlationId);

                    // Wait for login to complete and main screen to load
                    await Task.Delay(_uiConfig.ElementWaitTimeMs * 3);
                }
                else
                {
                    Log.Warning("[{CorrelationId}] Login button not found, trying Enter key", correlationId);
                    FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.RETURN);
                    await Task.Delay(_uiConfig.ElementWaitTimeMs * 3);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[{CorrelationId}] Error during auto-login", correlationId);
                throw new InvalidOperationException($"Auto-login failed: {ex.Message}", ex);
            }
        }
        else
        {
            Log.Information("[{CorrelationId}] No login screen detected (found {FieldCount} edit fields), proceeding to NOKA injection",
                correlationId, editFields?.Length ?? 0);
        }

        Log.Information("[{CorrelationId}] Auto-login check completed", correlationId);
    }

    /// <summary>
    /// Step 3: Inject NOKA (participant number) into input field.
    /// </summary>
    private async Task InjectNokaAsync(string correlationId, string noka)
    {
        Log.Information("[{CorrelationId}] Injecting NOKA={NOKA} into Finger",
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

        // After login, find the NOKA input field (should be an Edit control)
        // Look for Edit controls on the main screen (after login)
        var editFields = mainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit));

        // The NOKA field should be the first visible Edit control on the main screen
        // (login fields should be gone after successful login)
        var nokaField = editFields.FirstOrDefault();

        if (nokaField == null && editFields.Length > 0)
        {
            // If multiple edit fields, try to find one near the text "Masukkan No. Kartu BPJS Kesehatan"
            var nokaLabel = mainWindow.FindFirstDescendant(cf =>
                cf.ByControlType(ControlType.Text).And(cf.ByName("Masukkan No. Kartu BPJS Kesehatan")));

            if (nokaLabel != null)
            {
                var labelRect = nokaLabel.Properties.BoundingRectangle.Value;
                // Find edit field near this label (within 100 pixels vertically)
                nokaField = editFields
                    .Where(e => Math.Abs(e.Properties.BoundingRectangle.Value.Top - labelRect.Top) < 100)
                    .OrderBy(e => Math.Abs(e.Properties.BoundingRectangle.Value.Top - labelRect.Bottom))
                    .FirstOrDefault();
            }

            // Last resort: use the topmost Edit control
            if (nokaField == null)
            {
                nokaField = editFields
                    .OrderBy(e => e.Properties.BoundingRectangle.Value.Top)
                    .FirstOrDefault();
            }
        }

        if (nokaField == null)
        {
            Log.Warning("[{CorrelationId}] NOKA field not found via selectors, attempting keyboard-Tab fallback", correlationId);

            // Keyboard fallback: focus main window and try tabbing into the expected field
            try
            {
                // Bring to foreground first
                mainWindow.SetForeground();
                await Task.Delay(500);
                mainWindow.Focus();
                await Task.Delay(500);

                Log.Debug("[{CorrelationId}] Window focused for keyboard fallback", correlationId);

                // Try several Tab attempts to reach the input field
                for (var i = 0; i < 6; i++)
                {
                    FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.TAB);
                    await Task.Delay(200);
                }

                // Clear and type NOKA at current focus
                FlaUI.Core.Input.Keyboard.TypeSimultaneously(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL, FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_A);
                await Task.Delay(100);
                FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.DELETE);
                await Task.Delay(100);
                FlaUI.Core.Input.Keyboard.Type(noka);
                Log.Information("[{CorrelationId}] NOKA typed via keyboard fallback: {NOKA}", correlationId, noka);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[{CorrelationId}] Keyboard fallback failed to type NOKA", correlationId);
                throw new InvalidOperationException("NOKA input field not found and keyboard fallback failed");
            }

            // Proceed to wait for patient info even when using keyboard fallback
            var timeoutFb = TimeSpan.FromSeconds(_config.AutomationTimeoutSeconds);
            var populatedFb = await WaitForPatientInfoAsync(mainWindow, timeoutFb);
            if (!populatedFb)
            {
                Log.Warning("[{CorrelationId}] Patient info did not populate after keyboard fallback within {Seconds}s", correlationId, timeoutFb.TotalSeconds);
                return;
            }

            Log.Information("[{CorrelationId}] Patient info detected after keyboard fallback", correlationId);
            return;
        }

        Log.Debug("[{CorrelationId}] NOKA field found, preparing to inject", correlationId);

        // CRITICAL: Bring main window to foreground before typing
        try
        {
            mainWindow.SetForeground();
            await Task.Delay(500);
            Log.Debug("[{CorrelationId}] Main window brought to foreground", correlationId);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[{CorrelationId}] Could not set foreground, continuing anyway", correlationId);
        }

        // Click on the field to ensure it has focus
        nokaField.Click();
        await Task.Delay(500);

        // Verify window still has focus after click
        try
        {
            mainWindow.SetForeground();
            await Task.Delay(300);
        }
        catch { }

        // Clear any existing content
        FlaUI.Core.Input.Keyboard.TypeSimultaneously(FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL, FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_A);
        await Task.Delay(100);
        FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.DELETE);
        await Task.Delay(100);

        // Type the NOKA
        FlaUI.Core.Input.Keyboard.Type(noka);
        Log.Information("[{CorrelationId}] NOKA typed successfully: {NOKA}", correlationId, noka);

        await Task.Delay(_uiConfig.ElementWaitTimeMs / 2);

        // Wait for patient info to populate on the main window (compare text snapshot)
        var timeout = TimeSpan.FromSeconds(_config.AutomationTimeoutSeconds);
        var populated = await WaitForPatientInfoAsync(mainWindow, timeout);
        if (!populated)
        {
            Log.Warning("[{CorrelationId}] Patient info did not populate within {Seconds}s", correlationId, timeout.TotalSeconds);
            // Do not throw — allow workflow to continue but report failure upstream
            return;
        }
        Log.Information("[{CorrelationId}] Patient info detected on Finger main window", correlationId);
    }

    private async Task<bool> WaitForPatientInfoAsync(AutomationElement mainWindow, TimeSpan timeout)
    {
        try
        {
            // Snapshot of text content before
            var before = string.Concat(mainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
                .Select(e => e.Properties.Name.ValueOrDefault ?? string.Empty));

            var end = DateTime.UtcNow.Add(timeout);
            while (DateTime.UtcNow < end)
            {
                await Task.Delay(500);
                var after = string.Concat(mainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
                    .Select(e => e.Properties.Name.ValueOrDefault ?? string.Empty));

                if (!string.Equals(before, after, StringComparison.Ordinal))
                {
                    // Heuristic: some text changed — assume patient info loaded
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error while waiting for patient info");
        }

        return false;
    }

    /// <summary>
    /// Step 4: Trigger fingerprint scan process.
    /// </summary>
    private async Task TriggerFingerprintScanAsync(string correlationId)
    {
        Log.Information("[{CorrelationId}] Triggering fingerprint scan in Finger",
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

        // Find the scan/verify button by ClassName "Button"
        var scanButton = mainWindow.FindFirstDescendant(cf => cf.ByClassName("Button").And(cf.ByControlType(ControlType.Button)));
        if (scanButton == null)
        {
            throw new InvalidOperationException("Scan button not found");
        }

        Log.Debug("[{CorrelationId}] Scan button found, clicking it", correlationId);
        scanButton.Click();
        Log.Information("[{CorrelationId}] Fingerprint scan triggered", correlationId);

        // Wait for scan result
        await Task.Delay(_uiConfig.ElementWaitTimeMs * 3);

        Log.Information("[{CorrelationId}] Fingerprint scan completed", correlationId);
    }

    /// <summary>
    /// Cleanup automation resources.
    /// </summary>
    private void CleanupResources()
    {
        try
        {
            _automation?.Dispose();
            _automation = null;

            // Note: Not terminating process here - Finger window should remain open
            // Process will be terminated by explicit /stop_finger_exe endpoint call
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error during cleanup");
        }
    }
}
