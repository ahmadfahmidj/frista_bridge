# Implementation Plan: BPJS Biometric Automation Agent

**Branch**: `001-bpjs-automation-agent` | **Date**: 2025-11-17 | **Spec**: [spec.md](spec.md)  
**Input**: Feature specification from `specs/001-bpjs-automation-agent/spec.md`

## Summary

Build a lightweight HTTP service that automates BPJS biometric applications (Frista.exe and Finger.exe) for hospital systems. The agent exposes REST endpoints that trigger UI automation workflows, eliminating manual operator intervention for patient biometric verification at registration desks, kiosks, and telemedicine platforms. Core capability: receive NOKA number via HTTP → launch BPJS app → auto-login → inject patient data → trigger verification → return status.

Technical approach: ASP.NET Core Minimal API for HTTP layer + FlaUI (UIA3) for deterministic Windows UI automation + Serilog for structured logging + JSON-based configuration. Single-process console application deployable as Windows Service for 24/7 kiosk operation.

## Technical Context

**Language/Version**: C# 12 / .NET 8 LTS  
**Primary Dependencies**: ASP.NET Core Minimal API 8.0, FlaUI 4.0+ (UIA3 provider), Serilog 3.1+, System.Text.Json  
**Storage**: JSON configuration files (config.json, UI element maps), file-based logging (no database required)  
**Testing**: xUnit 2.6+, FakeItEasy (mocking), manual integration testing against live BPJS apps  
**Target Platform**: Windows 10/11 (x64), Windows Server 2016+ for kiosk deployments  
**Project Type**: Single project (console application with optional Windows Service hosting)  
**Performance Goals**: <3 seconds end-to-end automation (launch to verification), <100ms HTTP response time (request validation), support 100+ consecutive requests without degradation  
**Constraints**: <200MB memory footprint (concurrent BPJS app automation), localhost-only networking (127.0.0.1), no external dependencies beyond .NET runtime  
**Scale/Scope**: 10-50 concurrent hospital workstations, 500-2000 daily automation requests per installation, ~3000 lines of code (excluding tests)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [ ] **Code Readability**: No files exceed 300 lines; no methods exceed 50 lines
- [ ] **Naming**: All identifiers use full words (no abbreviations except BPJS, NOKA, HTTP, API, UI)
- [ ] **Magic Values**: No hardcoded paths, credentials, window titles, or UI selectors
- [ ] **Documentation**: All public APIs have XML doc comments
- [ ] **Logging**: All automation steps emit structured logs with context
- [ ] **Error Handling**: All errors include actionable messages and proper logging
- [ ] **Configuration**: All environment-specific values in config.json
- [ ] **Consistency**: HTTP endpoints follow naming patterns; JSON responses follow schema
- [ ] **Explicitness**: No default parameters hiding behavior; all dependencies documented

*If violations exist, document in Complexity Tracking section with justification.*

## Project Structure

### Documentation (this feature)

```text
specs/001-bpjs-automation-agent/
├── plan.md              # This file
├── spec.md              # Feature specification (completed)
├── research.md          # Phase 0: Technology research and decisions
├── data-model.md        # Phase 1: Domain entities and state models
├── quickstart.md        # Phase 1: Developer setup and testing guide
├── contracts/           # Phase 1: HTTP API contracts
│   └── openapi.yaml     # OpenAPI 3.0 specification for all endpoints
└── checklists/          # Quality validation
    └── requirements.md  # Specification quality checklist (completed)
```

### Source Code (repository root)

```text
src/
├── BiometricAgent/                  # Main project
│   ├── Program.cs                   # Application entry point, WebApplication setup
│   ├── BiometricAgent.csproj        # Project file with dependencies
│   ├── appsettings.json             # ASP.NET Core app settings (port, logging levels)
│   │
│   ├── Configuration/               # Configuration management
│   │   ├── AgentConfiguration.cs    # Config schema model with validation
│   │   ├── ApplicationConfig.cs     # BPJS app settings (paths, windows)
│   │   ├── UIElementMap.cs          # UI element selector definitions
│   │   └── ConfigurationValidator.cs # Startup validation logic
│   │
│   ├── Endpoints/                   # HTTP endpoint handlers
│   │   ├── FristaEndpoints.cs       # /run_exe, /stop_exe
│   │   ├── FingerEndpoints.cs       # /run_finger_exe, /stop_finger_exe
│   │   └── HealthEndpoints.cs       # /health (optional monitoring)
│   │
│   ├── Automation/                  # UI automation core
│   │   ├── IAutomationWorkflow.cs   # Workflow abstraction
│   │   ├── FristaWorkflow.cs        # Frista.exe automation logic
│   │   ├── FingerWorkflow.cs        # Finger.exe automation logic
│   │   ├── ProcessManager.cs        # Launch, attach, terminate processes
│   │   ├── WindowManager.cs         # Find windows, focus management
│   │   ├── UIElementLocator.cs      # FlaUI element search with retry
│   │   └── AutomationException.cs   # Custom exception types
│   │
│   ├── Models/                      # Domain models
│   │   ├── AutomationRequest.cs     # HTTP request model with validation
│   │   ├── AutomationResult.cs      # Response model (success/error)
│   │   ├── ProcessState.cs          # Process lifecycle tracking
│   │   └── ErrorCode.cs             # Enumeration of error categories
│   │
│   └── Services/                    # Cross-cutting concerns
│       ├── CredentialEncryption.cs  # Windows DPAPI wrapper
│       └── RequestQueue.cs          # Sequential request processing
│
config/
├── config.example.json              # Example configuration with comments
└── config.json                      # Actual config (gitignored)

tests/
├── BiometricAgent.IntegrationTests/ # Integration tests (require BPJS apps)
│   ├── FristaWorkflowTests.cs       # End-to-end Frista automation
│   ├── FingerWorkflowTests.cs       # End-to-end Finger automation
│   └── EndpointTests.cs             # HTTP API integration tests
│
└── BiometricAgent.UnitTests/        # Unit tests
    ├── ConfigurationTests.cs        # Config validation logic
    ├── ProcessManagerTests.cs       # Process lifecycle (mocked)
    ├── UIElementLocatorTests.cs     # Element search logic (mocked)
    └── RequestQueueTests.cs         # Concurrency handling

docs/
├── README.md                        # Installation, configuration, deployment
└── TROUBLESHOOTING.md               # Common errors and solutions

.github/
└── workflows/
    └── build.yml                    # CI pipeline (build, unit tests)
```

**Structure Decision**: Single project structure selected. This is a standalone Windows service with no frontend, no database, and no mobile clients. All functionality encapsulated in one ASP.NET Core application with clear separation of concerns: Endpoints (HTTP layer) → Automation (business logic) → Models (domain) → Configuration (external settings). Tests separated by type (unit vs integration) to enable fast feedback loop without requiring BPJS apps installed.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

**Status**: ✅ No violations detected. All constitution principles can be satisfied with the proposed architecture and technology stack.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| _None_    | N/A        | N/A                                 |
