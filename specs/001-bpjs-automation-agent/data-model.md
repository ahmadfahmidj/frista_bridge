# Data Model: BPJS Biometric Automation Agent

**Phase**: 1 - Design  
**Date**: 2025-11-17  
**Purpose**: Define domain entities, state models, and relationships without implementation details

## Core Entities

### 1. Automation Request

**Purpose**: Represents an incoming HTTP request to trigger biometric automation workflow.

**Attributes**:
- **CorrelationId** (string, required): Unique identifier for tracing request through logs (GUID format)
- **NoPeserta** (string, required): BPJS participant number (NOKA), 13-digit numeric string
- **WorkflowType** (enum, required): Target workflow - `Frista` or `Finger`
- **Timestamp** (datetime, required): Request received time (UTC)
- **ValidationState** (enum): `Valid`, `Invalid`, `Pending` - result of format validation

**Validation Rules**:
- NoPeserta MUST be exactly 13 characters
- NoPeserta MUST contain only digits (0-9)
- CorrelationId MUST be unique per request
- Timestamp MUST NOT be in the future

**Relationships**:
- Maps 1:1 to **Automation Result** (outcome of workflow execution)
- Enqueued into **Request Queue** if automation already in progress

**State Transitions**:
```
[Created] → [Validated] → [Queued] → [Executing] → [Completed/Failed]
```

---

### 2. Automation Result

**Purpose**: Represents the outcome of an automation workflow execution.

**Attributes**:
- **CorrelationId** (string, required): Matches originating Automation Request
- **IsSuccess** (boolean, required): Overall success indicator
- **ErrorCode** (enum, optional): Specific error category if `IsSuccess = false`
- **Message** (string, required): Human-readable description for hospital IT staff
- **Data** (object, optional): Workflow-specific output data (e.g., verification status, patient name)
- **StartTime** (datetime, required): Workflow execution start (UTC)
- **EndTime** (datetime, required): Workflow execution end (UTC)
- **Duration** (timespan, computed): `EndTime - StartTime`
- **ProcessId** (int, optional): ID of BPJS application process that was automated

**Error Code Enumeration**:
```
Configuration Errors (not retriable):
- CONFIG_MISSING_APPLICATION_PATH
- CONFIG_INVALID_CREDENTIALS
- CONFIG_MISSING_UI_ELEMENT_MAP
- CONFIG_SCHEMA_INVALID

Process Errors (retriable after delay):
- PROCESS_NOT_FOUND
- PROCESS_ALREADY_RUNNING
- PROCESS_LAUNCH_FAILED
- PROCESS_TERMINATED_UNEXPECTEDLY

Automation Errors (may require config update):
- AUTOMATION_WINDOW_NOT_FOUND
- AUTOMATION_ELEMENT_NOT_FOUND
- AUTOMATION_LOGIN_FAILED
- AUTOMATION_VERIFICATION_TIMEOUT

Validation Errors (not retriable):
- VALIDATION_INVALID_NOKA_FORMAT
- VALIDATION_MISSING_REQUIRED_FIELD

Timeout Errors (retriable):
- TIMEOUT_WINDOW_LOAD
- TIMEOUT_AUTOMATION_STEP
- TIMEOUT_OVERALL_WORKFLOW
```

**Relationships**:
- Created by **Automation Workflow** during execution
- Returned to HTTP client as JSON response

---

### 3. Process State

**Purpose**: Tracks lifecycle and metadata of a BPJS application process being automated.

**Attributes**:
- **ProcessId** (int, required): Windows process ID
- **ProcessName** (string, required): Executable name (`Frista.exe` or `Finger.exe`)
- **MainWindowHandle** (IntPtr, required): Handle to main application window
- **MainWindowTitle** (string, required): Title of main window (for validation)
- **State** (enum, required): Lifecycle state
- **LaunchTime** (datetime, required): When process was started
- **LastActivityTime** (datetime, required): Last successful UI interaction
- **IsResponsive** (boolean, computed): Whether window responds to messages

**State Enumeration**:
```
Launching → Loading → Ready → Executing → Completed → Terminated → Failed
```

**State Transition Rules**:
- **Launching**: Process started, waiting for main window to appear
- **Loading**: Main window detected, waiting for UI controls to load
- **Ready**: UI controls accessible, ready for automation commands
- **Executing**: Automation in progress (login, field population, etc.)
- **Completed**: Automation finished successfully, process can be terminated
- **Terminated**: Process closed (gracefully or forced)
- **Failed**: Unrecoverable error occurred during automation

**Relationships**:
- Managed by **Process Manager** service
- Referenced by **Automation Workflow** during execution
- Logged to **Structured Logs** at each state transition

---

### 4. UI Element Reference

**Purpose**: Represents a specific UI control in a BPJS application with selector strategies.

**Attributes**:
- **ElementName** (string, required): Logical name for logging (`LoginButton`, `NokaInputField`, `VerifyButton`)
- **AutomationId** (string, optional): Preferred selector - UI Automation AutomationId property
- **Name** (string, optional): Fallback selector - control's Name property
- **ControlType** (enum, optional): Last resort selector - type of control (`Button`, `Edit`, `Text`)
- **Index** (int, optional): Position in parent if multiple controls match (0-based)
- **IsRequired** (boolean, required): Whether workflow must find this element to proceed
- **RetryCount** (int, required): Number of search attempts before failing (default: 3)
- **RetryDelayMs** (int, required): Milliseconds to wait between attempts (default: 500)

**ControlType Enumeration**:
```
Button, Edit (text input), Text (label), ComboBox, CheckBox, Window, Pane, Custom
```

**Validation Rules**:
- At least one selector (AutomationId, Name, or ControlType) MUST be specified
- RetryCount MUST be >= 1 and <= 10
- RetryDelayMs MUST be >= 100 and <= 5000

**Relationships**:
- Loaded from **Agent Configuration** (UI element map JSON)
- Used by **UI Element Locator** service to find controls
- Logged when element not found (selector values aid troubleshooting)

---

### 5. Automation Workflow

**Purpose**: Represents a sequence of automation steps for a specific BPJS application.

**Attributes**:
- **WorkflowId** (string, required): Unique identifier (`FristaWorkflow`, `FingerWorkflow`)
- **ApplicationPath** (string, required): Full path to BPJS executable
- **Steps** (list, required): Ordered sequence of automation steps
- **TimeoutSeconds** (int, required): Maximum allowed execution time (default: 30)
- **CurrentStep** (int): Index of step currently executing (for logging)

**Step Definition** (sub-entity):
- **StepName** (string): Logical name (`LaunchApplication`, `WaitForWindow`, `Login`, `InjectNoka`, `TriggerVerification`)
- **StepType** (enum): `ProcessLaunch`, `WindowWait`, `ElementInteraction`, `Validation`
- **TargetElement** (UI Element Reference, optional): Element to interact with (if StepType = ElementInteraction)
- **InputValue** (string, optional): Data to send to element (e.g., username, NOKA)
- **ExpectedOutcome** (string, optional): Description of success criteria (for validation)

**StepType Enumeration**:
```
ProcessLaunch - Start BPJS application
WindowWait - Wait for main window to appear
ElementInteraction - Click button, fill field, etc.
Validation - Check UI state matches expectation
```

**State Transitions**:
```
[Initialized] → [Executing Step 1] → ... → [Executing Step N] → [Completed]
                                                               ↓
                                                          [Failed at Step X]
```

**Relationships**:
- Executes against **Process State** (launches and manages process)
- Uses **UI Element Locator** to find controls
- Produces **Automation Result** upon completion/failure
- Logged step-by-step to **Structured Logs**

---

### 6. Agent Configuration

**Purpose**: Represents the complete configuration schema loaded from `config.json`.

**Attributes**:

#### Application Settings
- **FristaPath** (string, required): Full path to `Frista.exe`
- **FingerPath** (string, required): Full path to `Finger.exe`
- **FristaWindowTitle** (string, required): Expected main window title pattern for Frista
- **FingerWindowTitle** (string, required): Expected main window title pattern for Finger
- **ProcessLaunchTimeoutSeconds** (int, required): Max time to wait for process start (default: 5)

#### Credentials (encrypted with DPAPI)
- **FristaUsername** (string, required): Encrypted login username for Frista
- **FristaPassword** (string, required): Encrypted login password for Frista
- **FingerUsername** (string, optional): Encrypted login username for Finger (if app requires login)
- **FingerPassword** (string, optional): Encrypted login password for Finger

#### UI Element Maps
- **FristaElements** (list of UI Element Reference): All UI controls for Frista automation
- **FingerElements** (list of UI Element Reference): All UI controls for Finger automation

#### Network Settings
- **ListenAddress** (string, required): IP address to bind (default: `127.0.0.1`)
- **ListenPort** (int, required): HTTP port (default: 5000)
- **EnableApiKeyAuth** (boolean, required): Whether to require X-API-Key header (default: false)
- **ApiKey** (string, optional): Expected API key value (if EnableApiKeyAuth = true)

#### Logging Settings
- **LogPath** (string, required): Directory for log files (default: `./logs`)
- **LogRetentionDays** (int, required): Number of days to keep logs (default: 31)
- **MinimumLogLevel** (enum, required): Minimum severity to log (`Debug`, `Information`, `Warning`, `Error`)

#### Operational Tuning
- **DefaultTimeoutSeconds** (int, required): Default workflow timeout (default: 30)
- **MaxConcurrentRequests** (int, required): Always 1 (sequential processing)
- **EnableHealthEndpoint** (boolean, required): Whether to expose `/health` (default: true)

**Validation Rules**:
- All file paths MUST exist and be accessible
- Encrypted credentials MUST be decryptable by current Windows user
- ListenPort MUST be 1-65535
- UI element maps MUST contain required elements (LoginButton, NokaInput, etc.)
- LogRetentionDays MUST be 1-365

**Relationships**:
- Loaded at **Agent Startup** by configuration validator
- Provides settings to **Process Manager**, **Workflow Executor**, **HTTP Endpoints**
- Changes require **Agent Restart** to take effect

**Example Structure** (JSON):
```json
{
  "applications": {
    "frista": {
      "path": "C:\\BPJS\\Frista.exe",
      "windowTitle": "FRISTA - BPJS Kesehatan",
      "username": "AQAAANCMnd8BFd...==",
      "password": "AQAAANCMnd8BFd...==",
      "elements": [
        {
          "name": "LoginButton",
          "automationId": "btnLogin",
          "controlType": "Button",
          "isRequired": true,
          "retryCount": 3,
          "retryDelayMs": 500
        }
      ]
    }
  },
  "network": {
    "listenAddress": "127.0.0.1",
    "listenPort": 5000,
    "enableApiKeyAuth": false
  },
  "logging": {
    "path": "./logs",
    "retentionDays": 31,
    "minimumLevel": "Information"
  }
}
```

---

### 7. Request Queue

**Purpose**: Manages serialization of automation requests when concurrent requests arrive.

**Attributes**:
- **QueuedRequests** (queue, FIFO): Pending automation requests waiting for execution
- **CurrentRequest** (Automation Request, optional): Request currently being processed
- **QueueDepth** (int, computed): Number of requests waiting
- **IsProcessing** (boolean): Whether automation is currently in progress

**Operations**:
- **Enqueue(request)**: Add request to queue if automation busy
- **Dequeue()**: Retrieve next request when automation available
- **Peek()**: View next request without removing (for wait time estimation)
- **Clear()**: Remove all queued requests (emergency reset)

**Queue Behavior**:
- **Capacity**: Unlimited (but warn if depth > 10)
- **Ordering**: FIFO (first-in, first-out)
- **Timeout**: Requests older than 5 minutes automatically fail with TIMEOUT_QUEUED error

**Relationships**:
- Receives requests from **HTTP Endpoints**
- Feeds requests to **Automation Workflow** executor
- Logged at each enqueue/dequeue operation

---

## Entity Relationships Diagram

```
┌─────────────────────┐
│  HTTP Request       │
└──────────┬──────────┘
           │ creates
           ▼
┌─────────────────────┐         ┌──────────────────┐
│ Automation Request  │────────>│ Request Queue    │
└──────────┬──────────┘ enqueues└────────┬─────────┘
           │ validates                    │ feeds
           ▼                              ▼
┌─────────────────────┐         ┌──────────────────┐
│ Validation Rules    │         │ Automation       │
└─────────────────────┘         │ Workflow         │
                                └────────┬─────────┘
                                         │ uses
                        ┌────────────────┼────────────────┐
                        ▼                ▼                ▼
                 ┌─────────────┐  ┌─────────────┐  ┌────────────┐
                 │ Process     │  │ UI Element  │  │ Agent      │
                 │ State       │  │ Locator     │  │ Config     │
                 └─────────────┘  └─────────────┘  └────────────┘
                        │                │
                        │                │ references
                        │                ▼
                        │         ┌─────────────────┐
                        │         │ UI Element      │
                        │         │ Reference       │
                        │         └─────────────────┘
                        │ produces
                        ▼
                 ┌─────────────────┐
                 │ Automation      │───────> HTTP Response
                 │ Result          │
                 └─────────────────┘
                        │ logs
                        ▼
                 ┌─────────────────┐
                 │ Structured      │
                 │ Logs (Serilog)  │
                 └─────────────────┘
```

---

## Data Flow: End-to-End Request

1. **HTTP Request** arrives at endpoint with NOKA parameter
2. **Automation Request** entity created, assigned correlation ID
3. **Validation Rules** applied (NOKA format check)
4. **Request Queue** checks if automation in progress
   - If busy: Enqueue and return 202 Accepted
   - If available: Proceed immediately
5. **Automation Workflow** initialized for target application (Frista/Finger)
6. **Agent Configuration** loaded to get application path, credentials, UI maps
7. **Process State** created, application launched
8. **Workflow Steps** execute sequentially:
   - Wait for window
   - Locate UI elements using **UI Element References**
   - Interact with controls (login, inject NOKA, trigger verification)
9. **Process State** updated at each step (for logging and monitoring)
10. **Automation Result** created with success/error status
11. **HTTP Response** returned with result JSON
12. **Structured Logs** written with full execution trace

---

## State Management Principles

### Stateless HTTP Layer
- No session state stored in HTTP endpoints
- Each request carries all necessary data (NOKA, correlation ID)
- Results returned synchronously (no polling required for MVP)

### Stateful Automation Layer
- **Process State** maintained during automation (current step, window handle, timestamps)
- **Request Queue** maintains FIFO order for concurrent requests
- State reset after each workflow completes (clean slate for next request)

### Configuration State
- **Agent Configuration** loaded once at startup
- Changes require restart (no hot-reload for MVP)
- Validation occurs at startup (fail-fast if invalid)

### Logging State
- **Correlation ID** threads request through all log entries
- **Contextual properties** attached per request (ProcessId, WorkflowType)
- Logs written asynchronously (don't block automation)

---

**Data Model Complete**: All entities defined with attributes, relationships, and state transitions. Ready for contract generation (Phase 1) and task breakdown (Phase 2).
