# Tasks: BPJS Biometric Automation Agent

**Input**: Design documents from `specs/001-bpjs-automation-agent/`  
**Prerequisites**: plan.md ✓, spec.md ✓, research.md ✓, data-model.md ✓, contracts/ ✓

**Tests**: Not explicitly requested in specification - tasks focus on implementation only.

**Organization**: Tasks grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3, US4)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [X] T001 Create .NET solution and BiometricAgent project in `src/BiometricAgent/BiometricAgent.csproj`
- [X] T002 Add NuGet packages: FlaUI.UIA3 4.0+, Serilog.Sinks.File 3.1+, Serilog.Formatting.Json 3.1+
- [X] T003 [P] Create config example file `config/config.example.json` with inline documentation
- [X] T004 [P] Add config.json to .gitignore
- [X] T005 [P] Create README.md in `docs/` with installation and configuration overview
- [X] T006 [P] Create TROUBLESHOOTING.md in `docs/` with common error scenarios

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Configuration & Validation

- [X] T007 Create AgentConfiguration model in `src/BiometricAgent/Configuration/AgentConfiguration.cs`
- [X] T008 Create ApplicationConfig model in `src/BiometricAgent/Configuration/ApplicationConfig.cs`
- [X] T009 Create UIElementMap model in `src/BiometricAgent/Configuration/UIElementMap.cs`
- [X] T010 Implement ConfigurationValidator in `src/BiometricAgent/Configuration/ConfigurationValidator.cs`
- [X] T011 Add config loading and validation in Program.cs startup

### Security & Encryption

- [X] T012 [P] Implement CredentialEncryption service using DPAPI in `src/BiometricAgent/Services/CredentialEncryption.cs`
- [X] T013 [P] Add CLI command `--encrypt-password` in Program.cs for credential encryption utility

### Domain Models

- [X] T014 [P] Create AutomationRequest model in `src/BiometricAgent/Models/AutomationRequest.cs`
- [X] T015 [P] Create AutomationResult model in `src/BiometricAgent/Models/AutomationResult.cs`
- [X] T016 [P] Create ErrorCode enum in `src/BiometricAgent/Models/ErrorCode.cs`
- [X] T017 [P] Create ProcessState model in `src/BiometricAgent/Models/ProcessState.cs`

### Logging Infrastructure

- [X] T018 Configure Serilog in Program.cs with file sink, JSON formatter, rolling daily logs
- [X] T019 [P] Add structured logging enrichers (CorrelationId, ProcessId, Application context)

### HTTP Framework

- [X] T020 Setup ASP.NET Core Minimal API in Program.cs with Kestrel configuration (localhost:5000)
- [X] T021 [P] Add request correlation ID middleware
- [X] T022 [P] Add optional API key authentication middleware

### Process Management

- [X] T023 Implement ProcessManager in `src/BiometricAgent/Automation/ProcessManager.cs` (launch, attach, terminate)
- [X] T024 Implement WindowManager in `src/BiometricAgent/Automation/WindowManager.cs` (find windows, focus management)

### UI Automation Core

- [X] T025 Create IAutomationWorkflow interface in `src/BiometricAgent/Automation/IAutomationWorkflow.cs`
- [X] T026 Implement UIElementLocator in `src/BiometricAgent/Automation/UIElementLocator.cs` (FlaUI element search with retry)
- [X] T027 [P] Create AutomationException custom exception in `src/BiometricAgent/Automation/AutomationException.cs`

### Concurrency Control

- [X] T028 Implement RequestQueue service in `src/BiometricAgent/Services/RequestQueue.cs` with SemaphoreSlim

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - SIMRS Biometric Verification (Priority: P1) 🎯 MVP

**Goal**: Enable SIMRS to trigger Frista automation via HTTP endpoint, eliminating manual operator intervention for biometric verification at registration desks.

**Independent Test**: Send `GET /run_exe?no_peserta=1234567890123` → Frista launches, logs in, populates NOKA, displays verification → Returns success JSON

### Implementation for User Story 1

- [X] T029 [US1] Create FristaWorkflow class in `src/BiometricAgent/Automation/FristaWorkflow.cs` implementing IAutomationWorkflow
- [X] T030 [US1] Implement LaunchFrista step in FristaWorkflow (process launch + window detection)
- [X] T031 [US1] Implement AutoLogin step in FristaWorkflow (locate username/password fields, fill, click login, validate post-login UI)
- [X] T032 [US1] Implement InjectNoka step in FristaWorkflow (locate NOKA input field, inject participant number)
- [X] T033 [US1] Implement TriggerVerification step in FristaWorkflow (locate verify button, click, wait for result)
- [X] T034 [US1] Add timeout handling and error recovery in FristaWorkflow (30 second overall timeout, step-level timeouts)
- [X] T035 [US1] Create FristaEndpoints in `src/BiometricAgent/Endpoints/FristaEndpoints.cs`
- [X] T036 [US1] Implement GET /run_exe endpoint with NOKA validation and workflow execution
- [X] T037 [US1] Add structured logging for all Frista workflow steps (step name, duration, element selectors, outcomes)
- [X] T038 [US1] Add error response mapping (ErrorCode enum → JSON response with actionable messages)

**Checkpoint**: User Story 1 fully functional - SIMRS can automate Frista biometric verification end-to-end

---

## Phase 4: User Story 2 - Self-Service Kiosk Fingerprint Verification (Priority: P2)

**Goal**: Enable kiosks to trigger Finger.exe automation for patient self-service fingerprint verification.

**Independent Test**: Send `GET /run_finger_exe?no_peserta=9876543210987` → Finger.exe launches, displays scanner UI, waits for patient scan → Returns verification result JSON

### Implementation for User Story 2

- [X] T039 [P] [US2] Create FingerWorkflow class in `src/BiometricAgent/Automation/FingerWorkflow.cs` implementing IAutomationWorkflow
- [X] T040 [US2] Implement LaunchFinger step in FingerWorkflow (process launch + window detection)
- [X] T041 [US2] Implement InjectNoka step in FingerWorkflow (locate NOKA input, populate field)
- [X] T042 [US2] Implement InitiateFingerprint step in FingerWorkflow (trigger scanner UI, wait for scan completion)
- [X] T043 [US2] Add timeout and error handling in FingerWorkflow (handle scanner hardware errors, no-match scenarios)
- [X] T044 [US2] Create FingerEndpoints in `src/BiometricAgent/Endpoints/FingerEndpoints.cs`
- [X] T045 [US2] Implement GET /run_finger_exe endpoint with NOKA validation and workflow execution
- [X] T046 [US2] Add structured logging for all Finger workflow steps
- [X] T047 [US2] Add user-friendly error messages for kiosk display (scanner not ready, no match, retry instructions)

**Checkpoint**: User Story 2 fully functional - Kiosks can automate fingerprint verification independently

---

## Phase 5: User Story 3 - Telemedicine Pre-Session Verification (Priority: P3)

**Goal**: Enable telemedicine platforms to queue biometric verification requests during patient waiting periods.

**Independent Test**: Send multiple concurrent requests → Agent queues and processes sequentially → All requests complete with correct correlation IDs

### Implementation for User Story 3

- [X] T048 [US3] Add request queueing logic to both FristaEndpoints and FingerEndpoints (detect in-progress automation)
- [X] T049 [US3] Implement queue depth monitoring and logging (warn if queue > 10 requests)
- [X] T050 [US3] Add exponential backoff retry logic in workflows (3 retries with 1s, 2s, 4s delays for transient errors)
- [X] T051 [US3] Implement queue timeout handling (requests older than 5 minutes auto-fail with TIMEOUT_QUEUED)
- [X] T052 [US3] Add correlation ID to all log entries for request tracing across queued requests
- [X] T053 [US3] Add performance metrics logging (queue wait time, total execution time per request)

**Checkpoint**: User Story 3 fully functional - Telemedicine can queue verification requests safely

---

## Phase 6: User Story 4 - Emergency Process Termination (Priority: P4)

**Goal**: Provide operators with endpoints to force-terminate stuck BPJS applications for operational recovery.

**Independent Test**: Start Frista → Call `GET /stop_exe` → Process terminates within 2 seconds → Agent ready for new requests

### Implementation for User Story 4

- [X] T054 [P] [US4] Implement GET /stop_exe endpoint in FristaEndpoints (force-terminate Frista process)
- [X] T055 [P] [US4] Implement GET /stop_finger_exe endpoint in FingerEndpoints (force-terminate Finger process)
- [X] T056 [US4] Add process cleanup logic (clear queue, reset SemaphoreSlim, log termination reason)
- [X] T057 [US4] Add safety checks (handle "not running" case gracefully, return appropriate status)
- [X] T058 [US4] Add structured logging for all termination events (process ID, termination reason, cleanup actions)

**Checkpoint**: User Story 4 fully functional - Operators can recover from stuck processes

---

## Phase 7: Health & Monitoring (Cross-Cutting)

**Purpose**: Operational monitoring and health checks

- [X] T059 [P] Create HealthEndpoints in `src/BiometricAgent/Endpoints/HealthEndpoints.cs`
- [X] T060 [P] Implement GET /health endpoint (return OK status with version and timestamp)
- [X] T061 [P] Add configuration flag to enable/disable health endpoint

---

## Phase 8: Testing Infrastructure

**Purpose**: Automated testing for code quality and regression prevention

### Unit Tests

- [ ] T062 [P] Create BiometricAgent.UnitTests project with xUnit and FakeItEasy
- [ ] T063 [P] Write ConfigurationValidator tests (valid config, missing fields, invalid paths, credential decryption)
- [ ] T064 [P] Write RequestQueue tests (FIFO ordering, concurrent enqueue, timeout handling)
- [ ] T065 [P] Write AutomationRequest validation tests (NOKA format, correlation ID uniqueness)
- [ ] T066 [P] Write ErrorCode mapping tests (all error codes have messages, codes are unique)
- [ ] T067 [P] Write CredentialEncryption tests (encrypt/decrypt round-trip, invalid ciphertext handling)

### Integration Tests

- [ ] T068 [P] Create BiometricAgent.IntegrationTests project
- [ ] T069 [P] Write FristaWorkflow integration test (end-to-end with test WPF app, not real Frista)
- [ ] T070 [P] Write FingerWorkflow integration test (end-to-end with test WPF app)
- [ ] T071 [P] Write HTTP endpoint tests (request/response schemas, error status codes)
- [ ] T072 [P] Write process termination tests (launch process, call stop endpoint, verify termination)
- [ ] T073 [P] Write concurrency test (send 10 concurrent requests, verify queue behavior)

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Final improvements and production readiness

### Documentation

- [X] T074 [P] Complete README.md with prerequisites, installation steps, configuration guide
- [X] T075 [P] Complete TROUBLESHOOTING.md with all error codes and resolution steps
- [ ] T076 [P] Add XML doc comments to all public classes and methods
- [ ] T077 [P] Create deployment guide for Windows Service mode in docs/

### Code Quality

- [X] T078 [P] Run code formatter (dotnet format) on entire solution
- [X] T079 [P] Verify no files exceed 300 lines (constitution check)
- [ ] T080 [P] Verify no methods exceed 50 lines (constitution check)
- [ ] T081 [P] Verify no magic values (all constants named, all config externalized)
- [ ] T082 [P] Add EditorConfig for consistent code style

### Performance Validation

- [ ] T083 Measure end-to-end automation time (verify <3 seconds for Frista workflow)
- [ ] T084 Run 100 consecutive requests and verify no memory leaks (stable memory usage)
- [ ] T085 Verify HTTP response time for validation-only requests (<100ms)

### Security Hardening

- [ ] T086 [P] Verify credentials never logged in plaintext
- [ ] T087 [P] Verify NOKA values not persisted beyond request lifecycle
- [ ] T088 [P] Verify API key authentication works when enabled

### Operational Readiness

- [ ] T089 Test Windows Service deployment mode (install, start, stop, uninstall)
- [ ] T090 Verify log rotation works correctly (daily rolling, 31-day retention)
- [ ] T091 Run quickstart.md validation (all steps work for new developer)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phases 3-6)**: All depend on Foundational phase completion
  - User stories can proceed in parallel if staffed
  - Or sequentially in priority order (P1 → P2 → P3 → P4)
- **Health & Monitoring (Phase 7)**: Can start after Foundational, independent of user stories
- **Testing (Phase 8)**: Can start in parallel with user story implementation
- **Polish (Phase 9)**: Depends on all user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Foundational phase complete → No dependencies on other stories
- **User Story 2 (P2)**: Foundational phase complete → Independent of US1 (different workflow, endpoints)
- **User Story 3 (P3)**: Depends on US1 OR US2 (needs at least one workflow to queue)
- **User Story 4 (P4)**: Depends on US1 OR US2 (needs processes to terminate)

### Within Each User Story

**User Story 1 (Frista)**:
1. FristaWorkflow class created (T029)
2. All workflow steps implemented in order (T030 → T031 → T032 → T033)
3. Timeout/error handling added (T034)
4. HTTP endpoints created (T035 → T036)
5. Logging and error mapping (T037 → T038)

**User Story 2 (Finger)**:
- T039-T041 can run in parallel (different files)
- T042 depends on T040-T041 (needs NOKA injection before fingerprint)
- T043-T047 follow sequentially

**User Story 3 (Queueing)**:
- All tasks (T048-T053) modify existing endpoints/workflows
- Must complete US1 or US2 first

**User Story 4 (Termination)**:
- T054-T055 can run in parallel (different endpoints)
- T056-T058 sequential (build on termination logic)

### Parallel Opportunities

**Setup Phase** (can all run in parallel):
- T003, T004, T005, T006

**Foundational Phase** (parallel batches):
- Batch 1: T007, T008, T009, T012, T013, T014, T015, T016, T017, T019, T021, T022, T027 (all independent files)
- Batch 2: T010, T011 (depends on T007-T009)
- Batch 3: T018, T020 (Program.cs modifications - sequential)
- Batch 4: T023, T024, T025, T026, T028 (automation core)

**User Story 1**:
- T037, T038 can run in parallel (logging vs error mapping)

**User Story 2**:
- T039, T040, T041 can start in parallel
- T046, T047 can run in parallel

**User Story 4**:
- T054, T055 can run in parallel (different files)

**Testing Phase** (all can run in parallel):
- T062-T067 (unit tests, different test files)
- T068-T073 (integration tests, different test files)

**Polish Phase** (most can run in parallel):
- T074-T082 (documentation and code quality checks)

---

## Parallel Example: Foundational Phase

```bash
# Launch all independent model classes together:
Task: "Create AutomationRequest model in src/BiometricAgent/Models/AutomationRequest.cs"
Task: "Create AutomationResult model in src/BiometricAgent/Models/AutomationResult.cs"
Task: "Create ErrorCode enum in src/BiometricAgent/Models/ErrorCode.cs"
Task: "Create ProcessState model in src/BiometricAgent/Models/ProcessState.cs"

# After models complete, start configuration:
Task: "Create AgentConfiguration model in src/BiometricAgent/Configuration/AgentConfiguration.cs"
Task: "Create ApplicationConfig model in src/BiometricAgent/Configuration/ApplicationConfig.cs"
Task: "Create UIElementMap model in src/BiometricAgent/Configuration/UIElementMap.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL - blocks all stories)
3. Complete Phase 3: User Story 1 (SIMRS integration)
4. **STOP and VALIDATE**: Test with real Frista.exe
5. Deploy to test workstation for SIMRS integration testing

**Estimated LOC for MVP**: ~1500 lines (models + config + HTTP + Frista workflow + logging)

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready (~800 LOC)
2. Add User Story 1 → Test independently → Deploy/Demo (MVP!) (~1500 LOC total)
3. Add User Story 2 → Test independently → Deploy to kiosk (~2000 LOC)
4. Add User Story 3 → Test queueing → Enable telemedicine (~2200 LOC)
5. Add User Story 4 → Test recovery → Production hardening (~2400 LOC)
6. Add Testing + Polish → Full quality gates (~3000 LOC total)

### Parallel Team Strategy

With 2 developers:

1. **Developer A**: Setup + Foundational (T001-T028)
2. Once Foundational done:
   - **Developer A**: User Story 1 (Frista - T029-T038)
   - **Developer B**: User Story 2 (Finger - T039-T047)
3. **Developer A**: User Story 3 (Queueing - T048-T053)
4. **Developer B**: User Story 4 (Termination - T054-T058)
5. **Both**: Testing + Polish (T062-T091 split between them)

---

## Notes

- **[P] tasks** = different files, no dependencies, can run in parallel
- **[Story] label** maps task to specific user story for traceability
- Each user story delivers independently testable value
- Foundational phase is critical path - prioritize completion
- Constitution checks embedded in Polish phase (T079-T082)
- Integration tests require real or simulated BPJS apps
- Manual acceptance testing with live Frista/Finger apps happens in Phase 9

---

## Task Statistics

- **Total Tasks**: 91
- **Setup Phase**: 6 tasks
- **Foundational Phase**: 22 tasks (critical path)
- **User Story 1 (P1)**: 10 tasks
- **User Story 2 (P2)**: 9 tasks
- **User Story 3 (P3)**: 6 tasks
- **User Story 4 (P4)**: 5 tasks
- **Health & Monitoring**: 3 tasks
- **Testing**: 12 tasks
- **Polish**: 18 tasks

**Parallelizable Tasks**: 47 tasks (52% can run in parallel)

**Estimated MVP (US1 only)**: 38 tasks (Setup + Foundational + US1)  
**Estimated Full Implementation**: 91 tasks
