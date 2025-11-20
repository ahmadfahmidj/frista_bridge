# BPJS Biometric Automation Agent - Troubleshooting Guide

This guide provides solutions for common issues encountered when installing, configuring, and running the BPJS Biometric Automation Agent.

---

## Table of Contents

1. [Installation Issues](#installation-issues)
2. [Configuration Errors](#configuration-errors)
3. [Credential Problems](#credential-problems)
4. [HTTP API Issues](#http-api-issues)
5. [Request Queue Issues](#request-queue-issues)
6. [UI Automation Failures](#ui-automation-failures)
7. [Performance Problems](#performance-problems)
8. [Logging & Diagnostics](#logging--diagnostics)
9. [Application Crashes](#application-crashes)
10. [Windows Service Issues](#windows-service-issues)
11. [Advanced Debugging](#advanced-debugging)

---

## Installation Issues

### Error: ".NET 8 Runtime not found"

**Symptom**: Agent fails to start with error "You must install .NET 8 to run this application"

**Solution**:
```powershell
# Download .NET 8 Runtime from Microsoft
# https://dotnet.microsoft.com/download/dotnet/8.0

# Verify installation
dotnet --list-runtimes

# Expected output includes:
# Microsoft.NETCore.App 8.0.x
```

### Error: "Package restore failed"

**Symptom**: `dotnet restore` fails with NuGet errors

**Solution**:
```powershell
# Clear NuGet cache
dotnet nuget locals all --clear

# Restore with verbose output
dotnet restore src/BiometricAgent -v detailed

# Check NuGet.config for invalid sources
```

### Error: "FlaUI.UIA3 compatibility warning"

**Symptom**: Build warning about FlaUI package targeting older framework

**Solution**:
- This is expected—FlaUI 4.0 targets .NET Framework but works with .NET 8
- Warning can be ignored if build succeeds
- No action required

---

## Configuration Errors

### Error: "Configuration file not found"

**Symptom**: Agent fails to start with "config.json not found"

**Solution**:
```powershell
# Copy example configuration
cp src/BiometricAgent/config/config.example.json src/BiometricAgent/config/config.json

# Verify file exists
Test-Path src/BiometricAgent/config/config.json
```

### Error: "Invalid JSON in configuration"

**Symptom**: Agent crashes on startup with JSON parsing error

**Solution**:
```powershell
# Validate JSON syntax
Get-Content src/BiometricAgent/config/config.json | ConvertFrom-Json

# Common issues:
# - Missing commas between properties
# - Trailing commas before closing braces
# - Unescaped backslashes in paths (use \\ or /)
```

### Error: "Executable path not found"

**Symptom**: Log shows `ERR_APP_NOT_FOUND` error

**Solution**:
```json
// In config.json, verify paths exist:
"Applications": {
  "Frista": {
    "ExecutablePath": "C:\\Program Files\\BPJS\\Frista\\Frista.exe"
  }
}
```

**Check path:**
```powershell
# Verify executable exists
Test-Path "C:\Program Files\BPJS\Frista\Frista.exe"

# Use absolute paths, not relative
```

---

## Credential Problems

### Error: "Credential decryption failed"

**Symptom**: Log shows "Failed to decrypt credentials" on automation attempts

**Solution**:
```powershell
# Re-encrypt credentials using tool
dotnet run --project src/BiometricAgent -- encrypt-credentials

# Ensure encryption runs under same Windows user as agent
# DPAPI keys are user-specific
```

### Error: "Login failed - invalid credentials"

**Symptom**: Log shows `ERR_LOGIN_FAILED` error code

**Solution**:
1. **Verify credentials manually**:
   - Launch Frista.exe directly
   - Login with username/password
   - Confirm credentials work

2. **Re-encrypt credentials**:
   ```powershell
   dotnet run --project src/BiometricAgent -- encrypt-credentials
   ```

3. **Check config.json**:
   ```json
   "Credentials": {
     "FristaUsername": "<base64-encrypted-value>",
     "FristaPassword": "<base64-encrypted-value>"
   }
   ```

### Error: "Empty encrypted credentials"

**Symptom**: `config.json` has empty string values in Credentials section

**Solution**:
```json
// BAD - empty values:
"Credentials": {
  "FristaUsername": "",
  "FristaPassword": ""
}

// GOOD - after encryption:
"Credentials": {
  "FristaUsername": "AQAAANCMnd8BFdERjHoAwE/Cl+s...",
  "FristaPassword": "AQAAANCMnd8BFdERjHoAwE/Cl+s..."
}
```

Run credential encryption tool to populate.

---

## HTTP API Issues

### Error: "HTTP request times out"

**Symptom**: Requests to `http://127.0.0.1:5000/*` hang and timeout

**Solution**:
```powershell
# 1. Verify agent is running
Get-Process | Where-Object { $_.ProcessName -like '*BiometricAgent*' }

# 2. Check port binding
netstat -ano | findstr :5000

# 3. Test with curl
curl http://127.0.0.1:5000/health

# 4. Verify firewall (localhost should be allowed by default)
```

### Error: "Connection refused"

**Symptom**: `curl` returns "Failed to connect to 127.0.0.1 port 5000"

**Solution**:
```json
// In config.json, verify Host setting:
"HttpServer": {
  "Host": "127.0.0.1",  // NOT "localhost" or "0.0.0.0"
  "Port": 5000
}
```

**Restart agent** after config change.

### Error: "HTTP 400 - Missing NOKA parameter"

**Symptom**: `/run_exe` returns error about missing NOKA

**Solution**:
```http
# WRONG - missing query parameter:
GET http://127.0.0.1:5000/run_exe

# CORRECT - include NOKA:
GET http://127.0.0.1:5000/run_exe?noka=0001234567890
```

### Error: "HTTP 500 - Internal server error"

**Symptom**: All requests return 500 status code

**Solution**:
```powershell
# Check logs for exception details
Get-Content logs/biometric-agent.log -Tail 50

# Look for stack traces or exception messages
# Common causes:
# - Configuration errors
# - File permission issues
# - Missing dependencies
```

---

## Request Queue Issues

### Error: "Request timed out in queue"

**Symptom**: Response shows `ERR_QUEUE_TIMEOUT` with message about queue timeout

**Solution**:
```json
// Increase queue timeout in config.json:
"Performance": {
  "QueueTimeoutSeconds": 300,  // Increase from default 60
  "MaxConcurrentRequests": 1
}
```

**Check queue depth**:
```powershell
# Monitor queue status via health endpoint
curl http://127.0.0.1:5000/health | ConvertFrom-Json | Select-Object -ExpandProperty queue
```

### Warning: "High queue depth detected"

**Symptom**: Logs show warnings about queue depth exceeding 10 requests

**Solution**:
1. **Investigate slow automation**: Check why automations are taking longer than expected
2. **Increase concurrency** (if applications support it):
   ```json
   "Performance": {
     "MaxConcurrentRequests": 2  // Allow 2 parallel automations
   }
   ```
3. **Optimize automation performance**: Reduce wait times and retry delays

**Monitor queue statistics**:
```powershell
# Watch queue metrics in real-time
while ($true) {
  $health = curl http://127.0.0.1:5000/health | ConvertFrom-Json
  Write-Output "Queue Depth: $($health.queue.currentDepth) | Total: $($health.queue.totalRequests) | Error Rate: $($health.queue.errorRate)%"
  Start-Sleep -Seconds 5
}
```

### Issue: Requests processed out of order

**Symptom**: Requests not processed in FIFO order

**Solution**:
- The queue is **always FIFO** (First-In-First-Out)
- Check log timestamps and correlation IDs to verify order
- Ensure `MaxConcurrentRequests` is set to 1 for strict serial execution:
  ```json
  "Performance": {
    "MaxConcurrentRequests": 1
  }
  ```

### Issue: Failed requests accumulating

**Symptom**: Health endpoint shows high `failedRequests` count

**Solution**:
```powershell
# Check error distribution in logs
Get-Content logs/biometric-agent.log | ConvertFrom-Json | 
  Where-Object { $_.'@l' -eq 'Error' -or $_.'@l' -eq 'Warning' } |
  Group-Object -Property ErrorCode |
  Sort-Object -Property Count -Descending

# Common failure causes:
# - ERR_UI_ELEMENT_NOT_FOUND: Update UI selectors
# - ERR_AUTOMATION_TIMEOUT: Increase timeout values
# - ERR_LOGIN_FAILED: Re-encrypt credentials
```

### Issue: Queue not clearing after hours

**Symptom**: Queue depth remains high even when no new requests coming in

**Solution**:
```powershell
# Check for stuck automations
Get-Process | Where-Object { $_.ProcessName -in @('Frista', 'After') }

# Force stop stuck processes
curl http://127.0.0.1:5000/stop_exe
curl http://127.0.0.1:5000/stop_finger_exe

# Restart agent if queue corruption suspected
```

---

## UI Automation Failures

### Error: "UI element not found"

**Symptom**: Log shows `ERR_UI_ELEMENT_NOT_FOUND` with element selector details

**Solution**:
1. **Increase wait time**:
   ```json
   "UIAutomation": {
     "ElementWaitTimeMs": 2000  // Increase from default 1000
   }
   ```

2. **Enable retry logic**:
   ```json
   "Applications": {
     "Frista": {
       "RetryAttempts": 5,  // Increase from default 3
       "RetryDelayMs": 1000  // Increase from default 500
     }
   }
   ```

3. **Verify application state**:
   - Launch Frista.exe manually
   - Check if UI elements exist
   - Note AutomationId/Name values using Inspect.exe (Windows SDK)

### Error: "Automation timeout"

**Symptom**: Log shows `ERR_AUTOMATION_TIMEOUT` after 30 seconds

**Solution**:
```json
"Applications": {
  "Frista": {
    "AutomationTimeoutSeconds": 60  // Increase from default 30
  }
}
```

**Note**: Timeouts >60s indicate underlying performance issues—investigate application responsiveness.

### Error: "Process already running"

**Symptom**: Automation fails because Frista.exe is already open

**Solution**:
```powershell
# Option 1: Stop existing process manually
Stop-Process -Name Frista -Force

# Option 2: Call stop endpoint before automation
curl http://127.0.0.1:5000/stop_exe
curl http://127.0.0.1:5000/run_exe?noka=0001234567890
```

### Error: "Window not found after launch"

**Symptom**: Application launches but UI window doesn't appear

**Solution**:
1. **Increase startup timeout**:
   ```json
   "Applications": {
     "Frista": {
       "StartupTimeoutSeconds": 20  // Increase from default 10
     }
   }
   ```

2. **Check for splash screens**: Some apps show temporary splash screens before main window
3. **Verify window title**: Application may use dynamic window titles
4. **Check for UAC prompts**: Elevation prompts block automation

---

## Performance Problems

### Issue: Automation takes >5 seconds

**Symptom**: `/run_exe` responses show `durationMs` > 5000

**Solution**:
```json
// Optimize settings:
"UIAutomation": {
  "ElementWaitTimeMs": 500,  // Reduce from 1000
  "EnableCaching": true,
  "CacheExpirationSeconds": 120
},
"Applications": {
  "Frista": {
    "RetryAttempts": 1,  // Reduce from 3
    "RetryDelayMs": 200  // Reduce from 500
  }
}
```

**Warning**: Reducing retries may decrease reliability.

### Issue: High memory usage (>200MB)

**Symptom**: Task Manager shows BiometricAgent using excessive memory

**Solution**:
```json
// Reduce log retention:
"Logging": {
  "RetainedFileCountLimit": 3,  // Reduce from 7
  "FileSizeLimitBytes": 52428800  // 50MB instead of 100MB
},
"ErrorHandling": {
  "CaptureScreenshotOnError": false  // Disable screenshots
}
```

**Check for memory leaks**:
```powershell
# Monitor memory over time
while ($true) {
  Get-Process BiometricAgent | Select-Object WS
  Start-Sleep -Seconds 5
}
```

### Issue: Slow HTTP response times

**Symptom**: `/health` endpoint takes >100ms to respond

**Solution**:
```json
// Reduce health check overhead:
"Health": {
  "CheckIntervalSeconds": 300,  // Increase from 60
  "EnableHealthEndpoint": true
}
```

**Check for network issues**:
```powershell
# Measure response time
Measure-Command { curl http://127.0.0.1:5000/health }
```

---

## Logging & Diagnostics

### Issue: No logs generated

**Symptom**: `logs/` directory is empty or doesn't exist

**Solution**:
```powershell
# Verify log path configuration
cat src/BiometricAgent/config/config.json | Select-String -Pattern 'LogPath'

# Create logs directory manually
New-Item -ItemType Directory -Force -Path logs

# Check file permissions
icacls logs
```

### Issue: Logs filling disk space

**Symptom**: Log files consuming GBs of disk space

**Solution**:
```json
"Logging": {
  "RetainedFileCountLimit": 3,  // Keep only 3 days
  "FileSizeLimitBytes": 10485760,  // 10MB max per file
  "MinimumLevel": "Warning"  // Only log warnings and errors
}
```

**Manual cleanup**:
```powershell
# Delete old logs
Get-ChildItem logs/*.log | Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-7) } | Remove-Item
```

### Issue: Cannot read JSON logs

**Symptom**: Log files contain unformatted JSON

**Solution**:
```powershell
# Pretty-print JSON logs
Get-Content logs/biometric-agent.log | ConvertFrom-Json | ConvertTo-Json -Depth 10

# Filter by correlation ID
Get-Content logs/biometric-agent.log | ConvertFrom-Json | Where-Object { $_.CorrelationId -eq 'abc-123-def' }

# Show only errors
Get-Content logs/biometric-agent.log | ConvertFrom-Json | Where-Object { $_.'@l' -eq 'Error' }
```

---

## Application Crashes

### Error: "Unhandled exception - NullReferenceException"

**Symptom**: Agent crashes with stack trace showing NullReferenceException

**Solution**:
```powershell
# Check for missing configuration values
cat config.json | ConvertFrom-Json

# Common null reference causes:
# - Empty executable paths
# - Missing credential values
# - Null UI element references

# Enable verbose logging to identify source
```

### Error: "Access denied"

**Symptom**: Agent crashes with "Access to path denied" error

**Solution**:
```powershell
# Run as Administrator (only if required)
Start-Process powershell -Verb RunAs -ArgumentList "dotnet run --project src/BiometricAgent"

# Check file permissions
icacls src/BiometricAgent/config/config.json

# Verify log directory is writable
Test-Path -Path logs -IsValid
```

### Error: "DLL not found"

**Symptom**: Agent crashes with "Unable to load DLL" error

**Solution**:
```powershell
# Reinstall .NET 8 Runtime
# Download from: https://dotnet.microsoft.com/download/dotnet/8.0

# Verify all dependencies are restored
dotnet restore src/BiometricAgent

# Check for missing Visual C++ Redistributables (if FlaUI requires)
# Download from: https://aka.ms/vs/17/release/vc_redist.x64.exe
```

---

## Windows Service Issues

### Error: "Service failed to start"

**Symptom**: Windows Service Control Manager shows "The service did not respond to start request"

**Solution**:
```powershell
# Check service configuration
sc.exe query BiometricAgent

# View service logs
Get-EventLog -LogName Application -Source BiometricAgent -Newest 10

# Verify service executable path
sc.exe qc BiometricAgent

# Test executable manually first
dotnet run --project src/BiometricAgent -c Release
```

### Error: "Service crashes on startup"

**Symptom**: Service starts but stops immediately

**Solution**:
```powershell
# Enable service failure recovery
sc.exe failure BiometricAgent reset= 86400 actions= restart/5000/restart/10000/restart/30000

# Check Windows Event Viewer for crash details
# Event Viewer > Windows Logs > Application

# Run in console mode for debugging
dotnet run --project src/BiometricAgent -c Release
```

---

## Advanced Debugging

### Enable Verbose Logging

```json
"Logging": {
  "MinimumLevel": "Verbose",  // Most detailed logs
  "EnableConsoleLogging": true
}
```

**Warning**: Verbose logging significantly increases log volume.

### Capture Network Traffic

```powershell
# Use Windows packet capture
netsh trace start capture=yes tracefile=trace.etl

# Reproduce issue

# Stop capture
netsh trace stop

# Analyze with Message Analyzer or Wireshark
```

### Inspect UI Automation Tree

```powershell
# Download Windows SDK
# Run Inspect.exe tool (included in Windows SDK)

# Launch Frista.exe
# Use Inspect.exe to view UI Automation tree
# Note AutomationId, Name, ControlType values

# Update configuration with correct selectors
```

### Performance Profiling

```powershell
# Use dotnet-trace for performance analysis
dotnet tool install --global dotnet-trace

# Capture trace
dotnet-trace collect --process-id <BiometricAgent-PID>

# Reproduce performance issue

# Stop trace (Ctrl+C)

# Analyze with PerfView or Visual Studio
```

### Memory Leak Detection

```powershell
# Use dotnet-gcdump for memory analysis
dotnet tool install --global dotnet-gcdump

# Capture heap dump
dotnet-gcdump collect --process-id <BiometricAgent-PID>

# Analyze with Visual Studio or dotnet-gcdump
dotnet-gcdump report heap.gcdump
```

---

## Quick Diagnostic Checklist

When troubleshooting any issue, verify:

- [ ] .NET 8 Runtime installed (`dotnet --list-runtimes`)
- [ ] `config.json` exists and valid JSON
- [ ] Executable paths exist and accessible
- [ ] Credentials encrypted and non-empty
- [ ] Agent process running (`Get-Process BiometricAgent`)
- [ ] HTTP port 5000 listening (`netstat -ano | findstr :5000`)
- [ ] Logs directory exists and writable
- [ ] Recent log entries present (check timestamps)
- [ ] No other processes using Frista.exe/Finger.exe
- [ ] Windows UI Automation enabled (should be default)

---

## Getting Additional Help

If issues persist after following this guide:

1. **Collect diagnostics**:
   ```powershell
   # Export configuration (redact credentials)
   cat config.json

   # Export recent logs
   Get-Content logs/biometric-agent.log -Tail 100 > diagnostics.txt

   # Export system info
   systeminfo > system-info.txt
   ```

2. **Review documentation**:
   - Main README: `docs/README.md`
   - API Specification: `specs/001-bpjs-automation-agent/contracts/openapi.yaml`
   - Technical Plan: `specs/001-bpjs-automation-agent/plan.md`

3. **Contact support**: Provide diagnostics files and detailed reproduction steps

---

**Document Version:** 1.0.0  
**Last Updated:** 2025-11-17
