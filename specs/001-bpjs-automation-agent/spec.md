# Feature Specification: BPJS Biometric Automation Agent

**Feature Branch**: `001-bpjs-automation-agent`  
**Created**: 2025-11-17  
**Status**: Draft  
**Input**: User description: "The Biometric Automation Agent is a lightweight, self-hosted automation service designed to integrate legacy BPJS biometric applications (Frista.exe and Finger.exe) into modern hospital systems such as SIMRS, APM, and telemedicine platforms. It exposes a secure local HTTP interface, orchestrates UI automation through FlaUI, and enables seamless, programmatic submission of BPJS participant numbers and biometric triggers without manual operator intervention."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - SIMRS Biometric Verification (Priority: P1) 🎯 MVP

Hospital registration desk staff need to verify BPJS patient eligibility through biometric checks without manually launching or interacting with the Frista application. When a patient arrives for registration, the SIMRS system should automatically trigger biometric verification by sending the patient's BPJS number to the automation agent.

**Why this priority**: Core use case that directly addresses the primary pain point - eliminating manual operator intervention for routine biometric checks at registration desks. Delivers immediate value by reducing registration time and operator workload.

**Independent Test**: Can be fully tested by sending an HTTP GET request with a valid BPJS number to the agent's endpoint. Success is verified when Frista launches, logs in automatically, populates the BPJS number field, and displays verification results without any manual interaction.

**Acceptance Scenarios**:

1. **Given** SIMRS has a patient with BPJS number "1234567890" ready for registration, **When** SIMRS sends `GET /run_exe?bpjs=1234567890`, **Then** Frista launches, auto-logs in, populates BPJS number field, triggers verification, and displays biometric data within 3 seconds
2. **Given** Frista is not running, **When** automation request is received, **Then** agent launches Frista from configured path, waits for main window to appear, and proceeds with automation
3. **Given** login credentials are configured, **When** Frista main window appears, **Then** agent automatically fills username and password fields, clicks login button, and validates successful login before proceeding
4. **Given** verification is complete, **When** SIMRS requests status, **Then** agent returns success response with execution time metadata

---

### User Story 2 - Self-Service Kiosk Fingerprint Verification (Priority: P2)

Patients using self-service kiosks (Anjungan Mandiri Pasien) need to complete fingerprint verification independently without staff assistance. The kiosk interface should trigger the fingerprint application, guide the patient through scanning, and capture results automatically.

**Why this priority**: Enables self-service workflows which reduce front-desk bottlenecks and improve patient experience. Secondary to SIMRS integration since it requires additional kiosk hardware and patient education.

**Independent Test**: Can be fully tested by calling the fingerprint endpoint from a kiosk application. Success is verified when Finger.exe launches, displays the fingerprint scanner interface, accepts input, and returns verification status to the kiosk system.

**Acceptance Scenarios**:

1. **Given** patient at kiosk has entered BPJS number "9876543210", **When** kiosk sends `GET /run_finger_exe?bpjs=9876543210`, **Then** Finger.exe launches, displays fingerprint scanner prompt, and waits for patient interaction
2. **Given** fingerprint scan is in progress, **When** patient places finger on scanner, **Then** application processes biometric data and displays verification result within 5 seconds
3. **Given** verification is successful, **When** kiosk polls for status, **Then** agent returns success indicator with patient verification data
4. **Given** verification fails (wrong finger or no match), **When** error occurs, **Then** agent returns user-friendly error message instructing patient to retry or seek staff assistance

---

### User Story 3 - Telemedicine Pre-Session Verification (Priority: P3)

Telemedicine platforms need to verify patient BPJS eligibility before starting a consultation session. The platform should automatically trigger biometric verification in the background while the patient waits in the virtual queue.

**Why this priority**: Enhances telemedicine experience but depends on network connectivity and requires integration with telemedicine scheduling systems. Lower priority since telemedicine volume is typically lower than in-person registration.

**Independent Test**: Can be fully tested by telemedicine scheduler sending verification requests 5-10 minutes before appointment time. Success is verified when verification completes silently in background and eligibility status is available before consultation starts.

**Acceptance Scenarios**:

1. **Given** patient has telemedicine appointment in 10 minutes, **When** scheduler sends verification request, **Then** agent queues request, executes verification during waiting period, and reports status before appointment time
2. **Given** multiple telemedicine appointments scheduled, **When** verification requests arrive simultaneously, **Then** agent processes them sequentially without conflicts or process interference
3. **Given** verification completes successfully, **When** telemedicine session starts, **Then** doctor sees patient eligibility status without delays
4. **Given** verification fails due to BPJS system unavailability, **When** error is detected, **Then** agent retries up to 3 times with exponential backoff before reporting failure to telemedicine platform

---

### User Story 4 - Emergency Process Termination (Priority: P4)

Operators need the ability to force-close stuck or frozen BPJS applications when automation fails or applications hang, without restarting the entire agent service or rebooting the workstation.

**Why this priority**: Critical for operational resilience but is a recovery mechanism rather than primary functionality. Only needed when errors occur, making it lower priority than the happy-path scenarios.

**Independent Test**: Can be fully tested by intentionally creating a stuck process (long-running operation or UI freeze) and calling the stop endpoint. Success is verified when the process terminates within 2 seconds and agent is ready for new requests.

**Acceptance Scenarios**:

1. **Given** Frista is running but frozen on a dialog, **When** operator sends `GET /stop_exe`, **Then** agent force-terminates Frista process within 2 seconds and logs termination reason
2. **Given** Finger.exe is hung waiting for hardware, **When** operator sends `GET /stop_finger_exe`, **Then** agent terminates process and clears any session state
3. **Given** automation is mid-execution, **When** stop command is received, **Then** agent safely aborts current operation, terminates process, and resets to ready state
4. **Given** no process is running, **When** stop command is received, **Then** agent returns success status indicating nothing to terminate

---

### Edge Cases

- **What happens when BPJS application UI changes** (updated version with different element identifiers)? Agent should detect element lookup failures, log detailed error with last known element structure, and return actionable error message indicating configuration update needed.

- **What happens when Windows is locked or screen is off** during automation? Agent should detect locked session state before attempting automation and return error indicating operator intervention required to unlock workstation.

- **What happens when concurrent requests arrive** for different BPJS numbers? Agent should detect existing automation in progress, queue subsequent requests, and process sequentially to prevent process conflicts.

- **What happens when config.json is missing or corrupted**? Agent should fail to start with clear error message listing specific missing or invalid configuration fields, preventing silent failures during runtime.

- **What happens when BPJS application crashes mid-automation**? Agent should detect process exit, log crash details with last successful step, return error response, and clean up any orphaned resources.

- **What happens when network connectivity is lost** during BPJS server communication? Agent should detect timeout, distinguish between local automation failure vs. BPJS server unavailability, and return appropriate error code.

- **What happens when credentials in config.json are incorrect**? Agent should detect login failure through UI state validation, log authentication failure, and return error indicating credential verification needed without exposing credential values.

- **What happens when patient BPJS number format is invalid** (wrong length, non-numeric)? Agent should validate BPJS number format before launching application, return immediate validation error with format requirements, preventing unnecessary application launches.

## Requirements *(mandatory)*

### Functional Requirements

#### HTTP API Interface

- **FR-001**: Agent MUST expose HTTP endpoint `GET /run_exe?bpjs={bpjs_number}` that launches Frista.exe, performs auto-login, injects BPJS number, and triggers verification workflow
- **FR-002**: Agent MUST expose HTTP endpoint `GET /run_finger_exe?bpjs={bpjs_number}` that launches Finger.exe, injects BPJS number, and initiates fingerprint verification workflow
- **FR-003**: Agent MUST expose HTTP endpoint `GET /stop_exe` that force-terminates any running Frista.exe process within 2 seconds
- **FR-004**: Agent MUST expose HTTP endpoint `GET /stop_finger_exe` that force-terminates any running Finger.exe process within 2 seconds
- **FR-005**: Agent MUST return JSON responses in consistent format: `{"status": "success|error", "code": "STATUS_CODE", "message": "description", "data": {...}}`
- **FR-006**: Agent MUST validate BPJS number parameter format (numeric, 13 digits standard for BPJS) before attempting automation and return validation errors immediately

#### Process & Window Management

- **FR-007**: Agent MUST launch BPJS applications using absolute file paths defined in config.json
- **FR-008**: Agent MUST detect main application window within configurable timeout (default 5 seconds) using window title patterns from config.json
- **FR-009**: Agent MUST handle cases where target application is already running by attaching to existing process or terminating and relaunching based on configuration policy
- **FR-010**: Agent MUST monitor process lifecycle and detect unexpected terminations during automation workflow
- **FR-011**: Agent MUST enforce timeout limits for each automation step (login, field population, verification trigger) to prevent infinite waits

#### UI Automation

- **FR-012**: Agent MUST identify UI elements using AutomationId, Name, or ControlType properties as defined in configuration
- **FR-013**: Agent MUST implement retry logic for UI element lookups (minimum 3 attempts with 500ms delays) to handle application load delays
- **FR-014**: Agent MUST validate successful login by checking for expected post-login UI elements before proceeding with BPJS number injection
- **FR-015**: Agent MUST inject BPJS number into designated input field using keyboard simulation with proper focus management
- **FR-016**: Agent MUST trigger search/verification buttons through UI automation (click or keyboard invoke)
- **FR-017**: Agent MUST wait for verification process completion by monitoring UI state changes or process status

#### Configuration Management

- **FR-018**: Agent MUST load all configuration from single config.json file containing executable paths, credentials, window titles, UI element selectors, network settings, and timeouts
- **FR-019**: Agent MUST validate configuration schema on startup and fail with specific error messages for each invalid or missing field
- **FR-020**: Agent MUST support encrypted credential storage using Windows DPAPI for username/password fields
- **FR-021**: Agent MUST allow configuration changes without recompilation (hot-reload on service restart)
- **FR-022**: Agent MUST provide example configuration file (config.example.json) with placeholder values and inline documentation

#### Logging & Diagnostics

- **FR-023**: Agent MUST log every automation step with structured data including: timestamp, action type, target element identifiers, execution duration, success/failure status
- **FR-024**: Agent MUST log full exception stack traces plus contextual information (process ID, window title, last successful step) when errors occur
- **FR-025**: Agent MUST log performance metrics for complete automation workflows (total execution time from request to completion)
- **FR-026**: Agent MUST use appropriate log levels: Error (automation failed), Warning (unexpected but recovered), Information (normal operations), Debug (detailed troubleshooting data)
- **FR-027**: Agent MUST write logs to configurable file path with automatic rotation to prevent disk space exhaustion

#### Error Handling

- **FR-028**: Agent MUST return user-friendly error messages that suggest corrective actions (e.g., "Login failed: Verify credentials in config.json" not "Exception at line 42")
- **FR-029**: Agent MUST distinguish between different error categories: application not found, element not found, timeout, login failure, process crash
- **FR-030**: Agent MUST assign unique error codes to each error category for programmatic error handling by calling systems
- **FR-031**: Agent MUST implement retry logic with exponential backoff for transient errors (timeouts, temporary UI delays)
- **FR-032**: Agent MUST fail fast for fatal errors (missing config, invalid credentials, application not installed) without retry attempts

#### Security & Access Control

- **FR-033**: Agent MUST listen on localhost (127.0.0.1) by default to prevent network exposure
- **FR-034**: Agent MUST support optional API key validation for requests when configured (X-API-Key header)
- **FR-035**: Agent MUST NOT store patient data (BPJS numbers, verification results) beyond request lifecycle
- **FR-036**: Agent MUST NOT expose sensitive configuration values (credentials, API keys) in logs or API responses
- **FR-037**: Agent MUST validate that BPJS application executables exist and are accessible before attempting automation

### Key Entities

- **Automation Request**: Represents incoming HTTP request containing BPJS number and target workflow (Frista or Finger). Includes validation state, timestamp, and correlation ID for tracing.

- **Application Process**: Represents running BPJS application instance (Frista.exe or Finger.exe). Tracks process ID, main window handle, lifecycle state (launching, ready, executing, completed, failed), and execution metrics.

- **UI Element Reference**: Represents specific UI control in BPJS application (login button, BPJS number input field, verification trigger). Contains selector strategy (AutomationId/Name/ControlType), retry configuration, and validation rules.

- **Automation Workflow**: Represents sequence of automation steps for specific task (login → inject BPJS number → trigger verification). Includes step definitions, timeout configurations, success criteria, and rollback procedures.

- **Configuration Schema**: Represents agent configuration loaded from config.json. Contains application paths, credentials, window patterns, UI selectors, network settings, timeout values, and logging preferences.

- **Execution Result**: Represents outcome of automation workflow. Contains success/failure status, error details, performance metrics (start time, end time, duration), and contextual data for debugging.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Hospital registration staff can complete patient biometric verification in under 5 seconds from SIMRS request to Frista displaying results, reducing average registration time by 60%

- **SC-002**: Agent achieves 95% or higher automation success rate for valid BPJS number requests under normal operating conditions (BPJS server available, correct configuration)

- **SC-003**: Kiosk patients can complete self-service fingerprint verification independently without staff assistance in 90% of cases, reducing front-desk support requests

- **SC-004**: Agent handles 100+ consecutive automation requests without memory leaks, process accumulation, or performance degradation, supporting full-day operation

- **SC-005**: Operator can diagnose and resolve automation failures within 60 seconds using log files, reducing mean time to resolution (MTTR) for production incidents

- **SC-006**: Configuration changes (new BPJS version, updated UI elements) can be deployed without code changes in under 10 minutes, eliminating emergency patching cycles

- **SC-007**: Agent recovers from stuck or crashed BPJS applications within 10 seconds using stop endpoints, maintaining service availability above 99%

- **SC-008**: Telemedicine verification requests complete within appointment waiting period (5-10 minutes) with 90% success rate, enabling seamless consultation starts

- **SC-009**: Zero patient data exposure incidents - no BPJS numbers or verification results logged or stored beyond request lifecycle, meeting healthcare privacy standards

- **SC-010**: Agent startup validation detects 100% of configuration errors (missing files, invalid credentials, wrong paths) before accepting requests, preventing runtime surprises
