# Biometric Automation Agent – Technical Specification

## 1. Executive Summary

The Biometric Automation Agent is a lightweight, self-hosted automation service designed to integrate legacy BPJS biometric applications (`Frista.exe` and `Finger.exe`) into modern hospital systems such as SIMRS, APM, and telemedicine platforms.

It exposes a secure local HTTP interface, orchestrates UI automation through FlaUI, and enables seamless, programmatic submission of BPJS participant numbers (NOKA) and biometric triggers without manual operator intervention. This specification outlines the product vision, system architecture, feature set, user journeys, and operational requirements to ensure predictable automation outcomes across hospital endpoints.

## 2. Product Goals

### 2.1 Business Objectives

- Streamline biometric verification workflows in hospitals.
- Decrease manual operator workload at registration desks and kiosks.
- Enable telemedicine and self-service modules to trigger biometric checks remotely.
- Standardize a reusable automation layer for all BPJS fingerprint processes.
- Reduce friction caused by the lack of official APIs or SDKs from BPJS applications.

### 2.2 Technical Objectives

- Provide a consistent HTTP-based automation endpoint.
- Ensure deterministic execution through FlaUI’s Windows UI Automation APIs.
- Implement configurable process launching, login flow, and form filling.
- Maintain isolation between automation logic and hospital core systems.
- Support clean extensibility for new BPJS modules or additional Windows apps.

## 3. System Architecture

### 3.1 High-Level System Diagram

```
Telemedicine / SIMRS / Kiosk
               │  (Local HTTP Request)
               ▼
-----------------------------------
| Biometric Automation Agent      |
| - ASP.NET Core Minimal API      |
| - FlaUI UI Automation Engine    |
| - Configurable App Launcher     |
| - Session & Error Management    |
-----------------------------------
               │  (UI Automation)
               ▼
[ FRISTA.exe ]   [ FINGER.exe ]
```

### 3.2 Core Architectural Components

**API Layer (ASP.NET Core)**

- Runs as a local HTTP service.
- Provides automation endpoints (`/run_exe`, `/run_finger_exe`, `/stop_*`).
- Handles request validation, session state, and error messaging.
- Optionally supports API key or localhost restriction.

**Automation Layer (FlaUI + UIA3)**

- Controls Windows applications deterministically using AutomationId, Name, and ControlType.
- Handles login, navigation, input fields, and button invocation.
- Provides retry logic, window focus management, and structured errors.

**Process Orchestration**

- Launches and attaches to target executables.
- Detects main windows using configurable titles or process names.
- Monitors process lifecycle and enforces timeouts.
- Provides clean process termination for stop commands.

**Configuration Layer**

- Uses a single `config.json` defining executable paths, credentials, window titles, networking, port settings, and operational tunables.

**Logging & Diagnostics (optional extension)**

- Employs Serilog-based structured logging.
- Writes local log files with timestamps and performance metrics.
- Optionally exposes an HTTP `/health` endpoint.

## 4. Core Features

### 4.1 Application Launch & Login Automation

- Start BPJS applications using paths defined in `config.json`.
- Detect the main window reliably with a configurable timeout.
- Automate login using securely stored credentials.
- Validate post-login UI state before continuing automation.

### 4.2 Participant Number Injection (NOKA)

- Identify the correct input field.
- Inject participant numbers with sanitized keystrokes.
- Trigger search or verification buttons.

### 4.3 Fingerprint Execution Workflow

- Launch the dedicated fingerprint application.
- Fill required data fields.
- Trigger verification actions via UI controls.
- Wait until the process completes.

### 4.4 Local HTTP Endpoint Interface

| Endpoint | Function |
| --- | --- |
| `GET /run_exe?no_peserta=xxxx` | Launch Frista and fill participant number |
| `GET /run_finger_exe?no_peserta=xxxx` | Launch Fingerprint app and trigger sequence |
| `GET /stop_exe` | Force-close Frista |
| `GET /stop_finger_exe` | Force-close Fingerprint app |

### 4.5 Process Management

- Gracefully terminate stuck applications.
- Detect already-running instances.
- Restart automatically when necessary.

### 4.6 Configuration-Driven Behavior

- Externalize app paths, credentials, window titles, and control identifiers.
- Avoid recompilation when environments change.

## 5. User Flow

### 5.1 Telemedicine or SIMRS Triggered Biometric Check

1. User enters a queue or telemedicine session.
2. SIMRS sends `GET http://127.0.0.1:5000/run_exe?no_peserta=1234567890`.
3. The Automation Agent launches Frista.
4. The agent auto-logs in.
5. The agent enters the NOKA.
6. The agent triggers verification.
7. Frista displays the biometric result (manual sensor interaction).
8. The hospital system polls status or waits for operator confirmation.

### 5.2 Anjungan Mandiri Pasien (Self-Service Kiosk)

1. The patient enters their BPJS number on the kiosk UI.
2. The kiosk calls `GET /run_finger_exe?no_peserta=xxxx`.
3. The agent opens the fingerprint tool.
4. The system prompts the patient to scan their fingerprint.
5. The result is stored back to the kiosk application.
6. The kiosk finalizes registration or appointment status.

### 5.3 Manual Operator Stop

1. The operator sees a frozen or stuck UI.
2. The operator triggers `GET /stop_exe`.
3. The Automation Agent terminates the process safely.
4. The kiosk or operator retries the process.

## 6. Technical Requirements

### 6.1 Runtime Environment

- Windows 10/11 or Windows Server 2016+.
- .NET 6 or .NET 8 runtime.
- Administrator permissions for the initial installation.
- BPJS Frista and Finger apps installed normally.

### 6.2 Development Stack

- C# and .NET.
- FlaUI (UIA3).
- ASP.NET Core Minimal API.
- Serilog (optional logging).
- JSON configuration storage.

### 6.3 Performance Criteria

- Launch-to-login automation within 1–2 seconds.
- End-to-end fingerprint flow manageable within BPJS app latency.
- Automation success rate target of 95% or higher per operation.

### 6.4 Security Requirements

- API defaults to listening on localhost.
- Optional API key for kiosk or telemedicine integration.
- No storage of sensitive patient data.
- Configurable credential encryption (DPAPI recommended).

## 7. Extensibility Plan

- Support additional BPJS executables.
- Provide event callbacks (webhooks) when fingerprint tasks complete.
- Introduce a queueing system to prevent concurrent requests.
- Enable multi-client access (SIMRS, kiosk, telemedicine concurrently).
- Support Windows Service deployment mode.

## 8. Error Handling Model

**Common error categories**

- Application not found.
- Wrong window title or updated UI layout.
- Element not found (AutomationId mismatch).
- Login failed.
- Timeout waiting for UI.
- Application hangs during processing.

**Error response format**

```json
{
   "status": "error",
   "code": "ELEMENT_NOT_FOUND",
   "message": "Login button could not be located in the UI tree."
}
```

## 9. Logging & Observability

**Logged events include**

- Process start and stop.
- UI window discovery.
- Input field resolution.
- Button invocation.
- Exception stack traces.
- End-to-end execution time.

**Optional monitoring**

- `/health` endpoint returning `{ "status": "ok" }`.

## 10. Deployment Model

- **Mode A — Console App:** Run via terminal or startup script.
- **Mode B — Windows Service:** Auto-start at boot; best for kiosks and RSUD front desk machines.
- **Mode C — Portable Executable:** Ship as a single EXE using self-contained publish.

## 11. Project File Structure (Recommended)

```
/agent
   /src
      Program.cs
      AutomationService.cs
      ConfigModels.cs
      /Automation
          FristaAutomation.cs
          FingerAutomation.cs
      /UIMaps
          FristaMap.json
          FingerMap.json
   /config
      config.json
   /logs
```

## 12. Acceptance Criteria

The implementation is complete when:

- All HTTP endpoints perform the expected automation routines.
- Frista and Finger apps can be launched, logged in, filled, and controlled without manual steps.
- Configuration changes do not require recompiling.
- Logs provide full traceability for debugging.
- The solution operates reliably for a minimum of 50 consecutive runs.

## 13. Conclusion

The Biometric Automation Agent unifies a fragmented legacy workflow into a consistent, API-driven automation service. It establishes a scalable integration surface for hospital information systems without requiring changes to the BPJS software itself. The architecture is future-focused, extension-ready, and engineered to support high reliability in clinical environments with growing automation demands.
