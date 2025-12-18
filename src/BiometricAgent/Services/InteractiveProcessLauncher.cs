using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Serilog;

namespace BiometricAgent.Services;

/// <summary>
/// Launches processes in the active user's interactive session.
/// Required when running as a Windows Service (Session 0) but needing to
/// start GUI applications that are visible to the logged-in user.
/// </summary>
public static class InteractiveProcessLauncher
{
    #region Win32 API Imports

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSEnumerateSessions(
        IntPtr hServer,
        int Reserved,
        int Version,
        ref IntPtr ppSessionInfo,
        ref int pCount);

    [DllImport("wtsapi32.dll")]
    private static extern void WTSFreeMemory(IntPtr pMemory);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSQueryUserToken(int sessionId, ref IntPtr phToken);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool CreateProcessAsUser(
        IntPtr hToken,
        string? lpApplicationName,
        string lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool DuplicateTokenEx(
        IntPtr hExistingToken,
        uint dwDesiredAccess,
        IntPtr lpTokenAttributes,
        int ImpersonationLevel,
        int TokenType,
        ref IntPtr phNewToken);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern bool CreateEnvironmentBlock(ref IntPtr lpEnvironment, IntPtr hToken, bool bInherit);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern bool DestroyEnvironmentBlock(IntPtr lpEnvironment);

    private const int WTS_CURRENT_SERVER_HANDLE = 0;
    private const uint MAXIMUM_ALLOWED = 0x2000000;
    private const int SecurityIdentification = 1;
    private const int TokenPrimary = 1;
    private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
    private const uint CREATE_NEW_CONSOLE = 0x00000010;

    private enum WTS_CONNECTSTATE_CLASS
    {
        WTSActive,
        WTSConnected,
        WTSConnectQuery,
        WTSShadow,
        WTSDisconnected,
        WTSIdle,
        WTSListen,
        WTSReset,
        WTSDown,
        WTSInit
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WTS_SESSION_INFO
    {
        public int SessionId;
        public IntPtr pWinStationName;
        public WTS_CONNECTSTATE_CLASS State;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct STARTUPINFO
    {
        public int cb;
        public string lpReserved;
        public string lpDesktop;
        public string lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    #endregion

    /// <summary>
    /// Gets the session ID of the active console user.
    /// Returns -1 if no active session is found.
    /// </summary>
    public static int GetActiveSessionId()
    {
        IntPtr pSessionInfo = IntPtr.Zero;
        int sessionCount = 0;

        try
        {
            if (WTSEnumerateSessions((IntPtr)WTS_CURRENT_SERVER_HANDLE, 0, 1, ref pSessionInfo, ref sessionCount))
            {
                int dataSize = Marshal.SizeOf(typeof(WTS_SESSION_INFO));
                IntPtr currentSession = pSessionInfo;

                for (int i = 0; i < sessionCount; i++)
                {
                    var si = Marshal.PtrToStructure<WTS_SESSION_INFO>(currentSession);
                    
                    if (si.State == WTS_CONNECTSTATE_CLASS.WTSActive)
                    {
                        Log.Debug("Found active session: SessionId={SessionId}", si.SessionId);
                        return si.SessionId;
                    }

                    currentSession = IntPtr.Add(currentSession, dataSize);
                }
            }
        }
        finally
        {
            if (pSessionInfo != IntPtr.Zero)
            {
                WTSFreeMemory(pSessionInfo);
            }
        }

        Log.Warning("No active user session found");
        return -1;
    }

    /// <summary>
    /// Checks if the current process is running as a Windows Service (Session 0).
    /// </summary>
    public static bool IsRunningAsService()
    {
        using var process = Process.GetCurrentProcess();
        var sessionId = process.SessionId;
        Log.Debug("Current process session ID: {SessionId}", sessionId);
        return sessionId == 0;
    }

    /// <summary>
    /// Launches a process in the active user's interactive session.
    /// Use this when running as a Windows Service but need to start GUI applications.
    /// </summary>
    /// <param name="applicationPath">Full path to the executable.</param>
    /// <param name="arguments">Optional command-line arguments.</param>
    /// <param name="workingDirectory">Optional working directory. Defaults to application directory.</param>
    /// <returns>The Process object if successful, null otherwise.</returns>
    public static Process? LaunchInUserSession(string applicationPath, string? arguments = null, string? workingDirectory = null)
    {
        // If not running as a service, use normal process start
        if (!IsRunningAsService())
        {
            Log.Debug("Not running as service, using normal Process.Start for: {Path}", applicationPath);
            return LaunchNormally(applicationPath, arguments, workingDirectory);
        }

        Log.Information("Running as Windows Service (Session 0), launching process in user session: {Path}", applicationPath);

        int sessionId = GetActiveSessionId();
        if (sessionId < 0)
        {
            Log.Error("Cannot launch process: No active user session found");
            return null;
        }

        Log.Debug("Target session ID: {SessionId}", sessionId);

        IntPtr userToken = IntPtr.Zero;
        IntPtr duplicateToken = IntPtr.Zero;
        IntPtr environment = IntPtr.Zero;

        try
        {
            // Get the user token for the active session
            if (!WTSQueryUserToken(sessionId, ref userToken))
            {
                int error = Marshal.GetLastWin32Error();
                Log.Error("WTSQueryUserToken failed with error: {ErrorCode}", error);
                return null;
            }

            Log.Debug("Got user token for session {SessionId}", sessionId);

            // Duplicate the token
            if (!DuplicateTokenEx(userToken, MAXIMUM_ALLOWED, IntPtr.Zero, 
                SecurityIdentification, TokenPrimary, ref duplicateToken))
            {
                int error = Marshal.GetLastWin32Error();
                Log.Error("DuplicateTokenEx failed with error: {ErrorCode}", error);
                return null;
            }

            Log.Debug("Token duplicated successfully");

            // Create environment block
            if (!CreateEnvironmentBlock(ref environment, duplicateToken, false))
            {
                Log.Warning("CreateEnvironmentBlock failed, continuing without environment block");
                environment = IntPtr.Zero;
            }

            // Prepare startup info
            var startupInfo = new STARTUPINFO
            {
                cb = Marshal.SizeOf(typeof(STARTUPINFO)),
                lpDesktop = @"winsta0\default"  // Interactive desktop
            };

            // Build command line
            string commandLine = string.IsNullOrEmpty(arguments) 
                ? $"\"{applicationPath}\"" 
                : $"\"{applicationPath}\" {arguments}";

            string directory = workingDirectory ?? Path.GetDirectoryName(applicationPath) ?? "";

            Log.Debug("Launching with command: {CommandLine}", commandLine);
            Log.Debug("Working directory: {Directory}", directory);

            // Create the process
            uint creationFlags = CREATE_UNICODE_ENVIRONMENT;
            if (environment != IntPtr.Zero)
            {
                creationFlags |= CREATE_UNICODE_ENVIRONMENT;
            }

            if (!CreateProcessAsUser(
                duplicateToken,
                null,
                commandLine,
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                creationFlags,
                environment,
                directory,
                ref startupInfo,
                out PROCESS_INFORMATION processInfo))
            {
                int error = Marshal.GetLastWin32Error();
                Log.Error("CreateProcessAsUser failed with error: {ErrorCode} - {Message}", 
                    error, new Win32Exception(error).Message);
                return null;
            }

            Log.Information("Process launched successfully in user session: PID={ProcessId}", processInfo.dwProcessId);

            // Close thread handle
            CloseHandle(processInfo.hThread);

            // Return Process object
            try
            {
                return Process.GetProcessById(processInfo.dwProcessId);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not get Process object for PID={ProcessId}, but process was started", 
                    processInfo.dwProcessId);
                
                // Close process handle since we can't use it
                CloseHandle(processInfo.hProcess);
                return null;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to launch process in user session: {Path}", applicationPath);
            return null;
        }
        finally
        {
            if (environment != IntPtr.Zero)
            {
                DestroyEnvironmentBlock(environment);
            }
            if (duplicateToken != IntPtr.Zero)
            {
                CloseHandle(duplicateToken);
            }
            if (userToken != IntPtr.Zero)
            {
                CloseHandle(userToken);
            }
        }
    }

    /// <summary>
    /// Normal process launch (for non-service scenarios).
    /// </summary>
    private static Process? LaunchNormally(string applicationPath, string? arguments, string? workingDirectory)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = applicationPath,
                Arguments = arguments ?? "",
                UseShellExecute = true,
                WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(applicationPath) ?? ""
            };

            return Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to start process normally: {Path}", applicationPath);
            return null;
        }
    }
}
