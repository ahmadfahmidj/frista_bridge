# Quickstart Guide: BPJS Biometric Automation Agent

**Purpose**: Get developers up and running with local development, testing, and validation  
**Audience**: Software engineers implementing the automation agent  
**Prerequisites**: Windows 10/11, .NET 8 SDK, Visual Studio 2022 or VS Code, BPJS apps installed (for integration testing)

---

## 1. Initial Setup (5 minutes)

### Clone Repository & Restore Dependencies

```powershell
# Clone repository
git clone <repository-url>
cd frista_bridge

# Checkout feature branch
git checkout 001-bpjs-automation-agent

# Restore NuGet packages
dotnet restore src/BiometricAgent/BiometricAgent.csproj

# Verify build succeeds
dotnet build src/BiometricAgent/BiometricAgent.csproj
```

**Expected Output**:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

---

## 2. Configuration Setup (10 minutes)

### Create `config.json` from Example

```powershell
# Copy example config
Copy-Item config/config.example.json config/config.json

# Edit with your paths
notepad config/config.json
```

### Update Paths and Credentials

```json
{
  "applications": {
    "frista": {
      "path": "C:\\Path\\To\\Your\\Frista.exe",  // ← Update this
      "windowTitle": "FRISTA - BPJS Kesehatan",
      "username": "ENCRYPT_ME",  // ← Temporary plaintext (see step 3)
      "password": "ENCRYPT_ME"
    },
    "finger": {
      "path": "C:\\Path\\To\\Your\\Finger.exe",  // ← Update this
      "windowTitle": "BPJS Fingerprint",
      "username": "",
      "password": ""
    }
  },
  "network": {
    "listenAddress": "127.0.0.1",
    "listenPort": 5000,
    "enableApiKeyAuth": false
  },
  "logging": {
    "path": "./logs",
    "retentionDays": 7,  // ← Shorter retention for dev
    "minimumLevel": "Debug"  // ← Verbose logging for dev
  },
  "operational": {
    "defaultTimeoutSeconds": 30,
    "enableHealthEndpoint": true
  }
}
```

### Encrypt Credentials (DPAPI)

```powershell
# Run encryption utility (to be built in Phase 2)
dotnet run --project src/BiometricAgent -- --encrypt-password

# Enter password: ******
# Encrypted value: AQAAANCMnd8BFd...==

# Copy encrypted value to config.json
# Replace "ENCRYPT_ME" with the encrypted string
```

---

## 3. Run Agent Locally (Console Mode)

```powershell
# Start agent in development mode
dotnet run --project src/BiometricAgent

# Expected console output:
# [12:00:00 INF] Starting Biometric Automation Agent v1.0.0
# [12:00:00 INF] Configuration loaded from: config/config.json
# [12:00:00 INF] Validating configuration...
# [12:00:00 INF] ✓ Frista path exists: C:\Path\To\Frista.exe
# [12:00:00 INF] ✓ Credentials decrypted successfully
# [12:00:00 INF] ✓ UI element maps loaded (12 elements)
# [12:00:00 INF] HTTP server listening on http://127.0.0.1:5000
# [12:00:00 INF] Agent ready. Press Ctrl+C to stop.
```

**Keep this terminal open** - logs will appear here as requests are processed.

---

## 4. Verify Agent is Running

Open a **new PowerShell window**:

```powershell
# Test health endpoint
Invoke-RestMethod http://127.0.0.1:5000/health

# Expected response:
# status    : ok
# timestamp : 2025-11-17T12:00:00.123Z
# version   : 1.0.0
```

---

## 5. Test Automation (First Request)

### Manual Test: Frista Automation

```powershell
# Send automation request (replace NOKA with valid test number)
Invoke-RestMethod "http://127.0.0.1:5000/run_exe?no_peserta=1234567890123"

# Watch agent console for logs:
# [12:01:00 INF] Request received: CorrelationId=abc123, WorkflowType=Frista, NOKA=1234567890123
# [12:01:00 INF] Launching process: C:\Path\To\Frista.exe
# [12:01:01 INF] Window found: "FRISTA - BPJS Kesehatan" (PID 12345)
# [12:01:01 INF] Step: Login - Locating element: LoginButton
# [12:01:01 INF] Step: Login - Element found (AutomationId: btnLogin)
# [12:01:02 INF] Step: InjectNoka - Populating field with NOKA
# [12:01:02 INF] Step: TriggerVerification - Clicking verify button
# [12:01:03 INF] Automation completed in 2.345s

# Expected JSON response:
{
  "status": "success",
  "code": "AUTOMATION_COMPLETED",
  "message": "Frista automation completed successfully",
  "data": {
    "correlationId": "abc123-def456-...",
    "duration": 2.345,
    "processId": 12345,
    "verificationStatus": "Verified"
  }
}
```

### Manual Test: Stop Process

```powershell
# Terminate running Frista process
Invoke-RestMethod http://127.0.0.1:5000/stop_exe

# Expected response:
{
  "status": "success",
  "code": "PROCESS_TERMINATED",
  "message": "Frista.exe (PID 12345) terminated successfully",
  "data": {
    "processId": 12345,
    "wasRunning": true
  }
}
```

---

## 6. Run Unit Tests (Fast Feedback Loop)

```powershell
# Run all unit tests (no BPJS apps required)
dotnet test tests/BiometricAgent.UnitTests

# Expected output:
# Passed:   42
# Failed:   0
# Skipped:  0
# Total:    42
# Duration: 1.2s
```

**Unit Test Coverage**:
- ✓ Configuration validation logic
- ✓ Request queue ordering (FIFO)
- ✓ NOKA format validation
- ✓ Error code mapping
- ✓ Credential encryption/decryption

---

## 7. Run Integration Tests (Requires BPJS Apps)

**⚠️ Prerequisites**:
- Frista.exe installed and configured in `config.json`
- Finger.exe installed and configured in `config.json`
- Valid test credentials configured

```powershell
# Run integration tests (slower, requires real apps)
dotnet test tests/BiometricAgent.IntegrationTests

# Expected output:
# Passed:   8
# Failed:   0
# Skipped:  0
# Total:    8
# Duration: 25.6s (includes automation execution time)
```

**Integration Test Coverage**:
- ✓ End-to-end Frista workflow (launch → login → inject → verify)
- ✓ End-to-end Finger workflow (launch → inject → scan)
- ✓ Process termination behavior
- ✓ Timeout handling (long-running automation)
- ✓ Concurrent request queueing
- ✓ Error recovery (element not found, login failure)

---

## 8. Debugging Automation Failures

### Enable Verbose Logging

Edit `config.json`:
```json
{
  "logging": {
    "minimumLevel": "Debug"  // ← Shows element search attempts
  }
}
```

Restart agent to apply.

### Check Logs

```powershell
# View latest log file
Get-Content logs/agent-20251117.json -Tail 50

# Search for errors
Select-String -Path logs/*.json -Pattern '"Level":"Error"'

# Find specific correlation ID
Select-String -Path logs/*.json -Pattern "abc123-def456"
```

### Common Issues & Solutions

| Symptom | Likely Cause | Solution |
|---------|--------------|----------|
| `AUTOMATION_WINDOW_NOT_FOUND` | Wrong window title pattern | Check `windowTitle` in config matches actual window |
| `AUTOMATION_ELEMENT_NOT_FOUND` | UI changed or wrong selector | Inspect app with Inspect.exe, update `elements` in config |
| `AUTOMATION_LOGIN_FAILED` | Wrong credentials or UI changed | Verify credentials, check login button selector |
| `TIMEOUT_WINDOW_LOAD` | App slow to start or crashed | Increase `processLaunchTimeoutSeconds` in config |
| `PROCESS_ALREADY_RUNNING` | Previous run didn't clean up | Call `/stop_exe` to force-terminate |

### Inspect UI Elements

Use **Inspect.exe** (Windows SDK tool) to find AutomationId values:

```powershell
# Launch Inspect.exe
& "C:\Program Files (x86)\Windows Kits\10\bin\x64\inspect.exe"

# 1. Click "Focus Tracking" button
# 2. Hover over Frista UI elements
# 3. Note "AutomationId" property in Inspect.exe window
# 4. Update config.json with correct AutomationId values
```

---

## 9. Validate Against OpenAPI Contract

```powershell
# Install Spectral (OpenAPI linter)
npm install -g @stoplight/spectral-cli

# Validate contract
spectral lint specs/001-bpjs-automation-agent/contracts/openapi.yaml

# Expected output:
# No results with a severity of 'error' or higher found!
```

### Test Contract Compliance

```powershell
# Send requests and verify response schemas match OpenAPI spec

# Valid request (should return 200)
Invoke-RestMethod "http://127.0.0.1:5000/run_exe?no_peserta=1234567890123"

# Invalid NOKA (should return 400)
Invoke-RestMethod "http://127.0.0.1:5000/run_exe?no_peserta=12345" -ErrorAction SilentlyContinue

# Missing parameter (should return 400)
Invoke-RestMethod "http://127.0.0.1:5000/run_exe" -ErrorAction SilentlyContinue
```

---

## 10. Performance Validation

### Measure Automation Speed

```powershell
# Time a single automation request
Measure-Command {
    Invoke-RestMethod "http://127.0.0.1:5000/run_exe?no_peserta=1234567890123"
}

# Expected: 2-5 seconds (under "3 seconds" success criteria)
```

### Stress Test (100 Consecutive Requests)

```powershell
# Run 100 automation requests sequentially
1..100 | ForEach-Object {
    Write-Host "Request $_"
    Invoke-RestMethod "http://127.0.0.1:5000/run_exe?no_peserta=1234567890123"
}

# Check agent logs for memory growth or errors
# Expected: No memory leaks, all requests succeed
```

---

## 11. Deploy as Windows Service (Optional)

**For production/kiosk deployments**, agent should run as Windows Service:

```powershell
# Publish self-contained executable
dotnet publish src/BiometricAgent -c Release -r win-x64 --self-contained -o publish

# Install as Windows Service (requires admin)
sc.exe create "BPJSAutomationAgent" binPath="C:\Path\To\publish\BiometricAgent.exe"

# Start service
sc.exe start BPJSAutomationAgent

# Check status
sc.exe query BPJSAutomationAgent

# Service logs go to configured log path (not console)
```

---

## 12. Validate Success Criteria

Before marking implementation complete, verify:

- [ ] **SC-001**: Automation completes in <5 seconds (measure with `Measure-Command`)
- [ ] **SC-002**: 95%+ success rate (run 100 requests, count successes)
- [ ] **SC-004**: No memory leaks after 100+ requests (check Task Manager)
- [ ] **SC-005**: Operator can diagnose failures from logs in <60 seconds
- [ ] **SC-006**: Config changes work without recompilation (edit config, restart, verify)
- [ ] **SC-007**: Process termination via `/stop_exe` works within 10 seconds
- [ ] **SC-010**: Startup validation detects all config errors (try invalid config)

---

## Quick Reference: Common Commands

```powershell
# Development
dotnet build src/BiometricAgent
dotnet run --project src/BiometricAgent
dotnet test

# Testing
Invoke-RestMethod http://127.0.0.1:5000/health
Invoke-RestMethod "http://127.0.0.1:5000/run_exe?no_peserta=1234567890123"
Invoke-RestMethod http://127.0.0.1:5000/stop_exe

# Debugging
Get-Content logs/agent-$(Get-Date -Format yyyyMMdd).json -Tail 50
Select-String -Path logs/*.json -Pattern '"Level":"Error"'

# Production
dotnet publish -c Release -r win-x64 --self-contained
sc.exe create BPJSAutomationAgent binPath="..."
sc.exe start BPJSAutomationAgent
```

---

## Next Steps

Once quickstart validation completes:

1. **Run `/speckit.tasks`** to generate detailed implementation task breakdown
2. **Begin Phase 1 (Setup)**: Create project structure, configure dependencies
3. **Complete Phase 2 (Foundational)**: Build configuration, HTTP endpoints, process management
4. **Implement User Stories**: P1 (SIMRS) → P2 (Kiosk) → P3 (Telemedicine) → P4 (Recovery)
5. **Final Validation**: Run all tests, measure performance, deploy to test kiosk

**Questions?** Check `specs/001-bpjs-automation-agent/plan.md` for architecture details.
