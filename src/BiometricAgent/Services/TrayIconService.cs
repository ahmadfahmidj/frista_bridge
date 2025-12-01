using System.Drawing;
using System.Reflection;
using System.Runtime.Versioning;
using System.Windows.Forms;
using Serilog;

namespace BiometricAgent.Services;

/// <summary>
/// Manages a system tray icon to monitor the BiometricAgent service status.
/// Provides visual feedback and context menu for quick actions.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class TrayIconService : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private ContextMenuStrip? _contextMenu;
    private readonly string _healthUrl;
    private System.Threading.Timer? _healthCheckTimer;
    private bool _isHealthy = true;
    private bool _disposed = false;

    public TrayIconService(string host, int port)
    {
        _healthUrl = $"http://{host}:{port}/health";
    }

    /// <summary>
    /// Initializes and displays the system tray icon.
    /// Must be called from a thread with Windows Forms message loop.
    /// </summary>
    public void Initialize()
    {
        try
        {
            // Create context menu
            _contextMenu = new ContextMenuStrip();
            _contextMenu.Items.Add("BiometricAgent", null, null!);
            _contextMenu.Items[0].Enabled = false;
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add("Check Health", null, OnCheckHealth!);
            _contextMenu.Items.Add("Open Logs Folder", null, OnOpenLogs!);
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add("Exit", null, OnExit!);

            // Load icon from embedded resource or file
            var icon = LoadIcon();

            // Create notify icon
            _notifyIcon = new NotifyIcon
            {
                Icon = icon,
                Text = "BiometricAgent - Running",
                Visible = true,
                ContextMenuStrip = _contextMenu
            };

            _notifyIcon.DoubleClick += OnCheckHealth!;

            // Start periodic health check (every 30 seconds)
            _healthCheckTimer = new System.Threading.Timer(
                async _ => await CheckHealthAsync(),
                null,
                TimeSpan.FromSeconds(5),  // Initial delay
                TimeSpan.FromSeconds(30)  // Interval
            );

            Log.Information("System tray icon initialized");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to initialize system tray icon - continuing without tray icon");
        }
    }

    /// <summary>
    /// Loads the icon from the asset folder or creates a default icon.
    /// </summary>
    private Icon LoadIcon()
    {
        try
        {
            // Try to load .ico file first (preferred)
            var iconPath = ConfigurationLoader.ResolvePathRelativeToExecutable("asset/heartbeat.ico");
            
            if (File.Exists(iconPath))
            {
                Log.Debug("Loading tray icon from {Path}", iconPath);
                return new Icon(iconPath);
            }

            // Try .png as fallback
            iconPath = ConfigurationLoader.ResolvePathRelativeToExecutable("asset/heartbeat.png");
            if (File.Exists(iconPath))
            {
                Log.Debug("Loading tray icon from {Path}", iconPath);
                using var bitmap = new Bitmap(iconPath);
                var hIcon = bitmap.GetHicon();
                return Icon.FromHandle(hIcon);
            }

            Log.Warning("Tray icon file not found, using default icon");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load custom tray icon, using default");
        }

        // Create a simple default icon (green circle)
        return CreateDefaultIcon();
    }

    /// <summary>
    /// Creates a simple default icon if the custom icon is not available.
    /// </summary>
    private Icon CreateDefaultIcon()
    {
        using var bitmap = new Bitmap(16, 16);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.FillEllipse(Brushes.LimeGreen, 2, 2, 12, 12);
        graphics.DrawEllipse(Pens.DarkGreen, 2, 2, 12, 12);
        var hIcon = bitmap.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    /// <summary>
    /// Periodically checks the health endpoint and updates the tray icon status.
    /// </summary>
    private async Task CheckHealthAsync()
    {
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            var response = await client.GetAsync(_healthUrl);
            
            var wasHealthy = _isHealthy;
            _isHealthy = response.IsSuccessStatusCode;

            if (_isHealthy != wasHealthy)
            {
                UpdateIconStatus();
            }
        }
        catch
        {
            if (_isHealthy)
            {
                _isHealthy = false;
                UpdateIconStatus();
            }
        }
    }

    /// <summary>
    /// Updates the tray icon appearance based on health status.
    /// </summary>
    private void UpdateIconStatus()
    {
        if (_notifyIcon == null) return;

        try
        {
            if (_isHealthy)
            {
                _notifyIcon.Text = "BiometricAgent - Running";
                _notifyIcon.Icon = LoadIcon();
            }
            else
            {
                _notifyIcon.Text = "BiometricAgent - Unhealthy!";
                // Create a red icon for unhealthy state
                using var bitmap = new Bitmap(16, 16);
                using var graphics = Graphics.FromImage(bitmap);
                graphics.Clear(Color.Transparent);
                graphics.FillEllipse(Brushes.Red, 2, 2, 12, 12);
                graphics.DrawEllipse(Pens.DarkRed, 2, 2, 12, 12);
                var hIcon = bitmap.GetHicon();
                _notifyIcon.Icon = Icon.FromHandle(hIcon);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to update tray icon status");
        }
    }

    /// <summary>
    /// Shows a balloon notification with the current health status.
    /// </summary>
    private void OnCheckHealth(object sender, EventArgs e)
    {
        if (_notifyIcon == null) return;

        Task.Run(async () =>
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                var response = await client.GetAsync(_healthUrl);
                var content = await response.Content.ReadAsStringAsync();

                _isHealthy = response.IsSuccessStatusCode;
                UpdateIconStatus();

                _notifyIcon.ShowBalloonTip(
                    3000,
                    "BiometricAgent Health",
                    _isHealthy ? "✓ Service is healthy and running" : "✗ Service is not responding",
                    _isHealthy ? ToolTipIcon.Info : ToolTipIcon.Error
                );
            }
            catch (Exception ex)
            {
                _isHealthy = false;
                UpdateIconStatus();
                _notifyIcon.ShowBalloonTip(
                    3000,
                    "BiometricAgent Health",
                    $"✗ Failed to check health: {ex.Message}",
                    ToolTipIcon.Error
                );
            }
        });
    }

    /// <summary>
    /// Opens the logs folder in Windows Explorer.
    /// </summary>
    private void OnOpenLogs(object sender, EventArgs e)
    {
        try
        {
            var logsPath = ConfigurationLoader.ResolvePathRelativeToExecutable("logs");
            if (Directory.Exists(logsPath))
            {
                System.Diagnostics.Process.Start("explorer.exe", logsPath);
            }
            else
            {
                _notifyIcon?.ShowBalloonTip(
                    3000,
                    "BiometricAgent",
                    "Logs folder not found",
                    ToolTipIcon.Warning
                );
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to open logs folder");
        }
    }

    /// <summary>
    /// Handles the Exit menu item click.
    /// </summary>
    private void OnExit(object sender, EventArgs e)
    {
        Log.Information("Exit requested from tray icon");
        
        _notifyIcon?.ShowBalloonTip(
            2000,
            "BiometricAgent",
            "Shutting down...",
            ToolTipIcon.Info
        );

        // Give time for balloon to show, then exit
        Task.Delay(1000).ContinueWith(_ =>
        {
            Environment.Exit(0);
        });
    }

    /// <summary>
    /// Shows a notification balloon.
    /// </summary>
    public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
    {
        _notifyIcon?.ShowBalloonTip(3000, title, message, icon);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _healthCheckTimer?.Dispose();
        _healthCheckTimer = null;

        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        _contextMenu?.Dispose();
        _contextMenu = null;

        Log.Information("System tray icon disposed");
    }
}
