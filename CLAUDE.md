# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Project Does

**Biometric Automation Agent** — a Windows service that automates BPJS (Indonesian health insurance) desktop applications (Frista.exe and Finger.exe) via Windows UI Automation. It exposes a local HTTP REST API so hospital systems (SIMRS, kiosks, telemedicine) can trigger biometric verification without human interaction.

## Build & Run

```bash
# Development build
cd src/BiometricAgent
dotnet build

# Release (self-contained single .exe, ~82 MB)
dotnet publish -c Release
# Output: publish/BiometricAgent.exe

# Run in development
dotnet run
# Listens on http://127.0.0.1:5000
```

### Windows Service Installation

```powershell
sc.exe create BiometricAgent binPath= "C:\BiometricAgent\BiometricAgent.exe" start= auto
sc.exe start BiometricAgent
sc.exe stop BiometricAgent
sc.exe delete BiometricAgent
```

### Credential Encryption

```powershell
./BiometricAgent.exe --encrypt-password
```

Generates DPAPI-encrypted passwords to put in `config/config.json`. Encryption is user-account-specific (Windows DPAPI).

## Architecture

```
HTTP Client (hospital system)
    ↓ GET /run_exe?bpjs=...
RequestQueueService (FIFO, default concurrency=1)
    ↓
FristaWorkflow / FingerWorkflow
    ↓ FlaUI UIA3
[Frista.exe] / [Finger.exe]  (legacy BPJS desktop apps)
```

### Key Design Decisions

**Request serialization via `RequestQueueService`** — UI automation is inherently stateful, so all requests queue FIFO with configurable concurrency (default 1). This prevents two automations from fighting over the same window.

**`InteractiveProcessLauncher`** — When running as a Windows Service, the service runs as SYSTEM in session 0 (no UI). This class launches Frista/Finger in the actual logged-in user's session so the GUI appears.

**DPAPI encryption** — Credentials in config.json are encrypted with Windows DPAPI scoped to the user account that runs the service. Use `--encrypt-password` CLI mode to generate these values.

**State detection before action** — Both `FristaWorkflow` and `FingerWorkflow` detect whether the target app is already running and logged in, or needs a fresh launch+login. They handle the login flow only when necessary.

**Tray icon disabled as service** — `TrayIconService` is only initialized when not running as a Windows Service. The app detects this via `WindowsServiceHelpers.IsWindowsService()`.

### HTTP Endpoints

| Endpoint | Purpose |
|----------|---------|
| `GET /run_exe?bpjs={13-digit}` | Launch Frista, login, inject BPJS number, trigger verification |
| `GET /run_finger_exe?bpjs={13-digit}` | Launch Finger app, login, trigger fingerprint scan |
| `GET /stop_exe` | Kill Frista process |
| `GET /stop_finger_exe` | Kill Finger process |
| `GET /health` | Returns health status + queue depth + memory |

### Configuration (`config/config.json`)

Based on `config/config.example.json`. Key sections:
- `HttpServer.port` — default 5000
- `Applications.fristaBinPath` / `fingerBinPath` — full paths to the executables
- `Credentials` — DPAPI-encrypted username/password (generate with `--encrypt-password`)
- `UIAutomation.elementWaitTimeout` — how long to wait for UI elements before failing
- `Performance.maxConcurrentRequests` — queue concurrency (keep at 1 for UI automation)

## Technology Stack

- **.NET 8 LTS**, `net8.0`, `win-x64`, self-contained single-file publish
- **ASP.NET Core Minimal API** — HTTP layer
- **FlaUI v5.0 (UIA3)** — Windows UI Automation
- **Serilog** — structured logging to daily rolling files in `logs/`
- **Microsoft.Extensions.Hosting.WindowsServices** — Windows Service host

## Project Layout (src/BiometricAgent/)

```
Automation/       FristaWorkflow.cs, FingerWorkflow.cs — UI automation logic
Configuration/    AgentConfiguration.cs + ConfigurationLoader.cs
Endpoints/        One file per HTTP route
Models/           AutomationRequest, AutomationResult, ErrorCodes, ProcessState
Services/         AutomationEngine, ProcessManager, InteractiveProcessLauncher,
                  RequestQueueService, RetryHelper, TrayIconService,
                  CredentialEncryption, LoggingService
config/           config.json (runtime), config.example.json (template)
installer/        Inno Setup script + compiled installer
Program.cs        Wires everything together, maps routes
```

## Active Technology Guidelines

From `.github/agents/copilot-instructions.md`:
- Use **C# 12 / .NET 8 LTS** conventions
- UI automation via **FlaUI 4.0+** (not legacy `System.Windows.Automation` directly)
- Logging via **Serilog 3.1+** (structured, with correlation IDs)
- Follow existing patterns in `Endpoints/` for new routes (minimal API style)
