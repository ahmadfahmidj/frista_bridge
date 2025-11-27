# BPJS Biometric Agent - Testing Guide

**Date**: 2025-11-20  
**Status**: In Progress - Testing with Real BPJS Applications

## Overview

This guide documents the testing process with actual BPJS applications (Frista.exe and After.exe/Finger) to validate automation workflows.

---

## Prerequisites

### 1. BPJS Applications Installed
- ✅ **Frista.exe**: Located at `D:\Frista\Frista.exe`
- ✅ **After.exe (Finger)**: Located at `C:\Program Files (x86)\BPJS Kesehatan\Aplikasi Sidik Jari BPJS Kesehatan\After.exe`

### 2. Valid Credentials
- BPJS username and password configured in `config.json`
- **Current Status**: Using plaintext credentials (needs encryption)

### 3. Test Data Requirements
- **NOKA (Nomor Kartu)**: 13-digit BPJS participant number
- **Test NOKA Options**:
  - Use real participant numbers from hospital database
  - Use test/dummy numbers if available from BPJS
  - **Format**: Must be exactly 13 digits (e.g., `0001234567890`)

---

## Test Results - Session 1 (2025-11-20)

### Test 1: Agent Startup and Health Check

**Command**:
```powershell
dotnet run --project src/BiometricAgent/BiometricAgent.csproj
```

**Result**: ✅ **PASSED**
- Agent started successfully
- Listening on `http://127.0.0.1:5001`
- Health endpoint responding with status 200
- Queue service initialized with max 1 concurrent request

**Evidence**:
```
{"Timestamp":"2025-11-20T11:58:42.0447345+07:00","Level":"Information","MessageTemplate":"Host: {Host}:{Port}","Properties":{"Host":"127.0.0.1","Port":5001}}
{"Timestamp":"2025-11-20T11:58:42.1528354+07:00","Level":"Information","MessageTemplate":"RequestQueueService initialized with max {MaxConcurrent} concurrent requests","Properties":{"MaxConcurrent":1}}
```

---

### Test 2: Frista Process Launch

**Command**:
```powershell
Invoke-RestMethod -Uri "http://127.0.0.1:5001/run_exe?noka=0001234567890" -Method Get
```

**Result**: ⚠️ **PARTIAL SUCCESS**
- ✅ Request received and validated
- ✅ NOKA format validation passed
- ✅ Queue service processed request (queue wait: <1ms)
- ✅ Frista.exe process launched successfully (PID logged)
- ✅ Process detected: `frista` with window title "Login Frista (Face Recognition BPJS Kesehatan)"
- ❌ **Window detection timed out after 10 seconds**

**Response**:
```json
{
  "success": false,
  "correlationId": "929830c3-2c36-44e0-9502-9a3b6be940f4",
  "noka": "0001234567890",
  "durationMs": 11060,
  "message": "Workflow timed out: Frista main window did not appear within 10 seconds",
  "errorCode": "ERR_AUTOMATION_TIMEOUT"
}
```

**Log Evidence**:
```
11/20/2025 11:59:29 [Information] Launching Frista from D:\Frista\Frista.exe
11/20/2025 11:59:29 [Information] Frista process started with PID=28128
11/20/2025 11:59:40 [Error] Frista workflow timed out after 11060ms
```

**Process Verification**:
```
ProcessName    Id MainWindowTitle
-----------    -- ---------------
frista      28128 Login Frista (Face Recognition BPJS Kesehatan)
```

**Analysis**:
- Frista application is running and has a visible window
- Window title: "Login Frista (Face Recognition BPJS Kesehatan)"
- The automation framework (FlaUI) may need adjustment to detect the window properly
- Possible causes:
  1. Window takes longer than 10 seconds to become automation-ready
  2. FlaUI needs specific window properties to detect
  3. Window hierarchy or accessibility properties not matching expectations

---

### Test 3: Process Termination

**Command**:
```powershell
Invoke-RestMethod -Uri "http://127.0.0.1:5001/stop_exe" -Method Get
```

**Result**: ✅ **PASSED**
- Frista process terminated successfully
- Process no longer in task list after termination
- Stop endpoint responding correctly

---

## Next Steps

### Immediate Actions Needed

1. **Credential Encryption** (High Priority)
   - Current config has plaintext credentials
   - Need to run encryption utility:
     ```powershell
     dotnet run --project src/BiometricAgent -- --encrypt-password
     ```

2. **Window Detection Investigation** (High Priority)
   - Need to verify FlaUI can detect Frista window
   - Possible solutions:
     - Increase startup timeout beyond 10 seconds
     - Add additional wait for window to become "responsive"
     - Verify UI Automation is enabled on the machine
     - Use Windows Inspect.exe to verify automation properties

3. **UI Element Configuration** (Medium Priority)
   - Need to map actual UI elements from Frista application:
     - Login username field (AutomationId or Name)
     - Login password field (AutomationId or Name)
     - Login button (AutomationId or Name)
     - NOKA input field (AutomationId or Name)
     - Verification/Search button (AutomationId or Name)

### Investigation Tools

#### Use Windows Inspect.exe
```powershell
# Launch Inspect.exe from Windows SDK
# Typically located at:
# C:\Program Files (x86)\Windows Kits\10\bin\<version>\x64\inspect.exe

# OR download Accessibility Insights:
# https://accessibilityinsights.io/downloads/
```

**Steps**:
1. Launch Frista manually
2. Open Inspect.exe
3. Hover over UI elements to see their automation properties:
   - AutomationId
   - Name
   - ControlType
   - LocalizedControlType
4. Document the values for each interactive element

#### Test Window Detection Manually

Create a quick test to verify FlaUI can see Frista:

```powershell
# Add this test to a test file or run interactively
cd src/BiometricAgent
dotnet add package FlaUI.UIA3
dotnet fsi
```

```fsharp
#r "nuget: FlaUI.UIA3"
#r "nuget: FlaUI.Core"

open FlaUI.UIA3
open FlaUI.Core.AutomationElements

let automation = new UIA3Automation()
let desktop = automation.GetDesktop()

// Find all windows
let windows = desktop.FindAllChildren()
printfn "Found %d windows" windows.Length

// Find Frista specifically
windows 
|> Seq.iter (fun w -> 
    printfn "Window: %s (ProcessId: %d)" w.Name w.Properties.ProcessId.Value)
```

---

## Test Scenarios

### Scenario 1: Full Frista Automation (End-to-End)

**Prerequisites**:
- Valid BPJS credentials
- Valid NOKA number
- Frista.exe not running

**Test Steps**:
1. Call `/run_exe?noka=<valid-noka>`
2. Verify Frista launches
3. Verify automatic login
4. Verify NOKA injection
5. Verify search/verification triggered
6. Verify result returned with success

**Expected Result**:
```json
{
  "success": true,
  "correlationId": "<guid>",
  "noka": "<test-noka>",
  "durationMs": "<2000-3000>",
  "message": "Automation completed successfully",
  "errorCode": null
}
```

---

### Scenario 2: Concurrent Request Queueing

**Prerequisites**:
- Agent running
- Frista.exe not running

**Test Steps**:
1. Send 3 concurrent requests with different NOKAs
2. Verify requests are queued (check queue depth in logs)
3. Verify FIFO processing order
4. Verify all requests complete successfully

**Commands**:
```powershell
# Terminal 1
Invoke-RestMethod -Uri "http://127.0.0.1:5001/run_exe?noka=0001111111111"

# Terminal 2 (immediately after)
Invoke-RestMethod -Uri "http://127.0.0.1:5001/run_exe?noka=0002222222222"

# Terminal 3 (immediately after)
Invoke-RestMethod -Uri "http://127.0.0.1:5001/run_exe?noka=0003333333333"
```

**Expected**: Requests process sequentially, queue metrics logged

---

### Scenario 3: Error Recovery - Invalid NOKA

**Test**:
```powershell
# Too short
Invoke-RestMethod -Uri "http://127.0.0.1:5001/run_exe?noka=123"

# Non-numeric
Invoke-RestMethod -Uri "http://127.0.0.1:5001/run_exe?noka=ABC1234567890"

# Missing parameter
Invoke-RestMethod -Uri "http://127.0.0.1:5001/run_exe"
```

**Expected**: `ERR_INVALID_NOKA` or `ERR_MISSING_PARAMETER` error codes

---

### Scenario 4: Finger.exe Automation

**Test**:
```powershell
Invoke-RestMethod -Uri "http://127.0.0.1:5001/run_finger_exe?noka=0001234567890"
```

**Expected**: After.exe launches, NOKA populated, fingerprint scanner activated

---

## Performance Validation

### Metrics to Measure

1. **End-to-End Automation Time**: Target <3 seconds
2. **Queue Wait Time**: Should be minimal with serial execution
3. **HTTP Response Time**: Target <100ms for validation-only
4. **Memory Usage**: Should stabilize under 200MB

### Measurement Commands

```powershell
# Measure automation time
Measure-Command { 
  Invoke-RestMethod -Uri "http://127.0.0.1:5001/run_exe?noka=0001234567890" 
}

# Check memory usage
Get-Process | Where-Object { $_.ProcessName -like "*BiometricAgent*" } | 
  Select-Object ProcessName, @{N='MemoryMB';E={[math]::Round($_.WS / 1MB, 2)}}

# Monitor queue statistics
while ($true) {
  $health = Invoke-RestMethod -Uri "http://127.0.0.1:5001/health"
  Write-Output "Queue Depth: $($health.queue.currentDepth) | Total: $($health.queue.totalRequests) | Error Rate: $($health.queue.errorRate)%"
  Start-Sleep -Seconds 5
}
```

---

## Troubleshooting During Testing

### Issue: Window Not Detected

**Solution 1**: Increase timeout
```json
"Applications": {
  "Frista": {
    "StartupTimeoutSeconds": 20  // Increase from 10
  }
}
```

**Solution 2**: Add wait for window to be "responsive"
```csharp
// In FristaWorkflow.cs - wait for window to be ready
if (mainWindow != null) {
    Thread.Sleep(2000); // Wait 2 seconds for window to stabilize
}
```

### Issue: UI Elements Not Found

**Check**:
1. Use Inspect.exe to verify element properties
2. Update config.json with correct AutomationId/Name values
3. Try different selector strategies (Name, ControlType, ClassName)

---

## Test Data

### Sample NOKA Numbers

**Format**: 13 digits

- Test 1: `0001234567890`
- Test 2: `0009876543210`
- Test 3: `1234567890123`

**⚠️ Important**: Replace with actual valid BPJS participant numbers for real testing

---

## Configuration Changes Needed for Testing

### 1. Update Startup Timeout (if needed)

```json
"Applications": {
  "Frista": {
    "StartupTimeoutSeconds": 20  // Increase if window detection fails
  }
}
```

### 2. Enable Verbose Logging

```json
"Logging": {
  "MinimumLevel": "Verbose"  // More detailed logs for debugging
}
```

### 3. Disable Error Screenshots (for now)

```json
"ErrorHandling": {
  "CaptureScreenshotOnError": false
}
```

---

## Success Criteria

### Phase 1: Basic Functionality
- [X] Agent starts without errors
- [X] Health endpoint responds
- [X] Process launch succeeds
- [ ] Window detection works
- [ ] Login automation completes
- [ ] NOKA injection works
- [ ] Search/verification triggers

### Phase 2: Reliability
- [ ] 10 consecutive successful automations
- [ ] No memory leaks over 100 requests
- [ ] Queue handling works correctly
- [ ] Error recovery functions properly

### Phase 3: Performance
- [ ] Automation completes in <3 seconds
- [ ] HTTP responses <100ms
- [ ] Memory usage stable <200MB
- [ ] No race conditions with concurrent requests

---

## Current Status: ⚠️ IN PROGRESS

**Completed**:
- ✅ Agent startup and initialization
- ✅ Health endpoint validation
- ✅ Process launch capability
- ✅ Process termination
- ✅ Queue service integration

**Blocked/In Progress**:
- 🔄 Window detection needs investigation
- 🔄 UI element mapping required
- 🔄 Credential encryption needed
- 🔄 Full automation workflow untested

**Next Session Goals**:
1. Resolve window detection timeout
2. Map UI elements using Inspect.exe
3. Encrypt credentials properly
4. Complete end-to-end automation test
5. Test Finger.exe workflow

---

## Notes

- Testing performed on: Windows 10/11 x64
- Agent version: 1.0.0
- .NET Runtime: 8.0
- FlaUI version: 5.0.0

