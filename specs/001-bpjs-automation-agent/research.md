# Research: BPJS Biometric Automation Agent

**Phase**: 0 - Technology Research & Decisions  
**Date**: 2025-11-17  
**Purpose**: Resolve technical unknowns, document technology choices, and establish implementation patterns

## Research Questions Resolved

### 1. FlaUI UI Automation Strategy

**Question**: How to reliably locate and interact with UI elements in legacy BPJS applications that may not have consistent AutomationId properties?

**Decision**: Use FlaUI's UIA3 provider with fallback selector strategy: AutomationId (primary) → Name → ControlType + Index (last resort).

**Rationale**:
- UIA3 (UI Automation 3.0) is the modern Windows automation API with better performance than legacy MSAA/UIA2
- FlaUI provides strongly-typed, fluent API that aligns with constitution's "Explicit Over Implicit" principle
- Multi-level selector strategy handles UI variations across BPJS versions without brittle XPath-style selectors
- Retry logic (3 attempts, 500ms delays) handles asynchronous UI loading common in legacy Windows apps

**Alternatives Considered**:
- **Selenium WebDriver**: Rejected - only works for web UIs, not native Windows applications
- **Windows PowerShell UI Automation**: Rejected - less type-safe, harder to maintain, no retry abstractions
- **Raw UIA COM APIs**: Rejected - complex, verbose code violates readability principles

**Implementation Pattern**:
```csharp
public AutomationElement FindElement(ElementSelector selector)
{
    for (int attempt = 1; attempt <= 3; attempt++)
    {
        if (!string.IsNullOrEmpty(selector.AutomationId))
        {
            var element = window.FindFirstDescendant(cf => cf.ByAutomationId(selector.AutomationId));
            if (element != null) return element;
        }
        
        if (!string.IsNullOrEmpty(selector.Name))
        {
            var element = window.FindFirstDescendant(cf => cf.ByName(selector.Name));
            if (element != null) return element;
        }
        
        Thread.Sleep(500); // Wait for async UI load
    }
    throw new ElementNotFoundException(selector);
}
```

---

### 2. Concurrency & Request Queueing

**Question**: How to handle concurrent HTTP requests when only one BPJS application instance can be automated at a time?

**Decision**: Implement in-memory FIFO queue with SemaphoreSlim (concurrency = 1) to serialize automation workflows.

**Rationale**:
- BPJS applications are single-instance - launching multiple copies causes process conflicts
- Queue ensures predictable execution order (first request processed first)
- SemaphoreSlim is lightweight, async-friendly, and built into .NET BCL (no external dependencies)
- HTTP endpoints return 202 Accepted for queued requests with estimated wait time
- Aligns with constitution's "Consistent User Experience" - predictable queueing behavior

**Alternatives Considered**:
- **Reject concurrent requests with 429 Too Many Requests**: Rejected - poor UX, requires client retry logic
- **External message queue (RabbitMQ, Redis)**: Rejected - violates "no external dependencies" constraint, overkill for single-machine deployment
- **Background Task Queue (IHostedService)**: Considered but unnecessary complexity - SemaphoreSlim sufficient for synchronous workflows

**Implementation Pattern**:
```csharp
private static readonly SemaphoreSlim _automationLock = new(1, 1);

public async Task<AutomationResult> ExecuteAsync(AutomationRequest request)
{
    await _automationLock.WaitAsync(); // Queue here if automation in progress
    try
    {
        return await PerformAutomation(request);
    }
    finally
    {
        _automationLock.Release();
    }
}
```

---

### 3. Configuration Encryption (DPAPI)

**Question**: How to securely store BPJS application credentials in config.json without plaintext exposure?

**Decision**: Use Windows Data Protection API (DPAPI) with user-scope encryption for credential fields.

**Rationale**:
- DPAPI is built into Windows, no additional NuGet packages required
- User-scope encryption ties credentials to the Windows account running the agent (typically a service account)
- No need to manage encryption keys - Windows handles key storage automatically
- Aligns with constitution's "Configuration-Driven Behavior" and "Security" principles
- Simple API: `ProtectedData.Protect()` and `ProtectedData.Unprotect()`

**Alternatives Considered**:
- **Azure Key Vault / HashiCorp Vault**: Rejected - external dependencies, network calls, overkill for local-only service
- **Environment variables**: Rejected - harder to manage, still visible in process listings
- **AES with hardcoded key**: Rejected - security theater, key must be in code

**Implementation Pattern**:
```csharp
public static string EncryptCredential(string plaintext)
{
    var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
    var encryptedBytes = ProtectedData.Protect(plaintextBytes, null, DataProtectionScope.CurrentUser);
    return Convert.ToBase64String(encryptedBytes);
}

public static string DecryptCredential(string ciphertext)
{
    var encryptedBytes = Convert.FromBase64String(ciphertext);
    var plaintextBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
    return Encoding.UTF8.GetString(plaintextBytes);
}
```

**Setup Workflow**: Provide CLI command `BiometricAgent.exe --encrypt-password` to help administrators generate encrypted values for config.json.

---

### 4. Structured Logging with Serilog

**Question**: What logging configuration best supports troubleshooting in production hospital environments?

**Decision**: Serilog with file sink (rolling daily), structured JSON format, contextual enrichment (correlation IDs, process IDs, automation step names).

**Rationale**:
- JSON-structured logs enable log aggregation tools (Seq, ELK stack) if hospitals want centralized logging later
- Rolling daily files prevent disk space exhaustion (default: 31 days retention)
- Correlation IDs trace single automation request across multiple log entries
- Contextual properties (ProcessId, WindowTitle, ElementSelector) aid debugging without verbose stack traces
- Aligns with constitution's "Structured Logging & Observability" principle

**Alternatives Considered**:
- **Plain text logs**: Rejected - harder to parse programmatically, no structured queries
- **Windows Event Log**: Rejected - hospital IT staff prefer file-based logs for quick access
- **Database logging**: Rejected - adds unnecessary dependency and complexity

**Implementation Pattern**:
```csharp
Log.Logger = new LoggerConfiguration()
    .Enrich.WithProperty("Application", "BiometricAgent")
    .WriteTo.File(
        path: "logs/agent-.json",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 31,
        formatter: new JsonFormatter())
    .CreateLogger();

// Usage with context
using (LogContext.PushProperty("CorrelationId", request.CorrelationId))
using (LogContext.PushProperty("ProcessId", process.Id))
{
    Log.Information("Automation step: {Step} completed in {Duration}ms", 
        "Login", stopwatch.ElapsedMilliseconds);
}
```

---

### 5. HTTP Endpoint Design Patterns

**Question**: Should endpoints be synchronous (wait for automation to complete) or asynchronous (return immediately, poll for status)?

**Decision**: Synchronous endpoints with configurable timeout (default 30 seconds). Automation workflows complete within seconds, making async unnecessary.

**Rationale**:
- 95% of automation requests complete in <5 seconds (based on tech-spec performance goals)
- Synchronous pattern simpler for integrators - single HTTP call, no polling logic needed
- Configurable timeout protects against hung BPJS applications (returns 504 Gateway Timeout)
- Aligns with constitution's "Consistent User Experience" - predictable request/response pattern
- Can add async endpoints later if telemedicine use case requires long-running verification (15+ minutes)

**Alternatives Considered**:
- **Async with polling endpoint**: Rejected - adds complexity, requires state storage, longer integration time for hospital developers
- **WebSockets for real-time updates**: Rejected - overkill, most clients (SIMRS, kiosks) expect REST
- **Server-Sent Events (SSE)**: Rejected - browser-centric technology, not needed for server-to-server integration

**Implementation Pattern**:
```csharp
app.MapGet("/run_exe", async (string no_peserta, IAutomationService automation) =>
{
    var request = new AutomationRequest { NoPeserta = no_peserta };
    var result = await automation.ExecuteFristaWorkflowAsync(request); // Blocks until complete
    return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
})
.WithTimeout(TimeSpan.FromSeconds(30)); // Configurable global timeout
```

---

### 6. Error Code Taxonomy

**Question**: How to categorize errors for programmatic handling by calling systems (SIMRS, kiosks, telemedicine)?

**Decision**: Define enum with 5 top-level categories, each with specific sub-codes. Categories: Configuration, Process, Automation, Validation, Timeout.

**Rationale**:
- Calling systems need to distinguish retriable errors (timeout, process busy) from permanent failures (invalid config, missing application)
- Top-level category enables generic error handling; sub-codes enable specific diagnostics
- Aligns with constitution's "Consistent User Experience" - predictable error contract
- Error messages include suggested corrective actions for hospital IT staff

**Error Code Structure**:
```
[CATEGORY]_[SPECIFIC_ERROR]

Examples:
- CONFIG_MISSING_APPLICATION_PATH
- CONFIG_INVALID_CREDENTIALS
- PROCESS_NOT_FOUND
- PROCESS_ALREADY_RUNNING
- AUTOMATION_ELEMENT_NOT_FOUND
- AUTOMATION_LOGIN_FAILED
- AUTOMATION_WINDOW_NOT_FOUND
- VALIDATION_INVALID_BPJS_FORMAT
- TIMEOUT_WINDOW_LOAD
- TIMEOUT_AUTOMATION_STEP
```

**Retry Guidance**:
- **Retriable**: TIMEOUT_*, PROCESS_ALREADY_RUNNING (retry after 5 seconds)
- **Not Retriable**: CONFIG_*, VALIDATION_*, AUTOMATION_LOGIN_FAILED (requires admin intervention)

---

### 7. Testing Strategy Without Live BPJS Apps

**Question**: How to enable CI/CD and developer testing without requiring BPJS applications installed on every machine?

**Decision**: Three-tier testing approach: Unit tests (mocked automation), integration tests (real FlaUI, fake UI), contract tests (HTTP endpoints, mocked workflows).

**Rationale**:
- Unit tests verify business logic (request validation, error handling, queueing) without FlaUI - runs on any machine
- Integration tests use FlaUI against simple test WPF application (not BPJS) - validates UI automation mechanics
- Contract tests verify HTTP API shape and error responses - ensures API compatibility
- Manual acceptance testing against real BPJS apps happens pre-release on dedicated test machine
- Aligns with constitution's testing requirements while maintaining fast feedback loop

**Test Structure**:
```
Unit Tests (90% coverage, <1 second execution):
- ConfigurationValidator logic
- Request queue ordering
- Error code mapping
- BPJS number validation rules

Integration Tests (70% coverage, <10 seconds):
- FlaUI element location strategies
- Process launch and termination
- Window management and focus

Contract Tests (100% endpoint coverage, <5 seconds):
- HTTP request/response schemas
- Error response formats
- Status code correctness

Manual Acceptance (pre-release):
- End-to-end automation against real Frista.exe
- End-to-end automation against real Finger.exe
- 50 consecutive request stress test
```

---

## Technology Stack Summary

| Component | Technology | Version | Justification |
|-----------|-----------|---------|---------------|
| **Runtime** | .NET | 8.0 LTS | Long-term support (Nov 2026), best Windows performance |
| **HTTP Framework** | ASP.NET Core Minimal API | 8.0 | Lightweight, no MVC overhead, explicit routing |
| **UI Automation** | FlaUI | 4.0+ | Type-safe UIA3 wrapper, active maintenance, good docs |
| **Logging** | Serilog | 3.1+ | Structured logging leader, rich sink ecosystem |
| **JSON Parsing** | System.Text.Json | 8.0 (BCL) | Built-in, high performance, no extra dependency |
| **Testing** | xUnit | 2.6+ | .NET community standard, parallel execution |
| **Mocking** | FakeItEasy | 8.0+ | Readable syntax, better for complex mocks |
| **Encryption** | Windows DPAPI | Built-in | No external dependencies, OS-level key management |

**Total NuGet Dependencies**: 3 (FlaUI.UIA3, Serilog.Sinks.File, Serilog.Formatting.Json)

---

## Best Practices Established

### FlaUI Automation
- Always use `try-finally` to release automation resources (prevent memory leaks)
- Cache `AutomationElement` references within workflow scope (avoid redundant searches)
- Log element selector attempts before throwing `ElementNotFoundException`
- Use `WaitUntilResponsive()` after launching processes before interacting with UI

### Configuration Management
- Validate configuration schema at startup, fail fast with clear error messages
- Provide `--validate-config` CLI flag for troubleshooting without starting HTTP service
- Document every config field with inline JSON comments in `config.example.json`
- Never log decrypted credentials - log presence/absence only

### Error Handling
- Use custom exception types (`AutomationException`, `ConfigurationException`) with error codes
- Include actionable messages: "Check that X exists" not "Failed to Y"
- Log exceptions at ERROR level with full context (correlation ID, process state, last step)
- Return user-friendly error responses (for hospital IT staff) vs. internal logs (for developers)

### Performance Optimization
- Reuse FlaUI `AutomationElement` caches within workflow (don't re-query DOM)
- Use `FindFirstDescendant()` instead of `FindAllDescendants()` when single element expected
- Set explicit timeouts on all waits (no infinite loops)
- Monitor memory after 100+ requests in integration tests

---

## Open Questions / Future Research

**Q1**: Should agent support webhook callbacks when automation completes (async pattern)?  
**Status**: Deferred to extensibility phase. Current sync pattern meets all P1-P4 user stories.

**Q2**: Can multiple BPJS application versions coexist with different UI element selectors?  
**Status**: Deferred. Recommend single BPJS version per agent installation. Multi-version support requires UI map versioning (future enhancement).

**Q3**: Should agent expose Prometheus metrics endpoint for hospital monitoring systems?  
**Status**: Deferred. `/health` endpoint sufficient for MVP. Prometheus integration can be added if hospitals request it.

---

**Research Phase Complete**: All technical unknowns resolved. Ready to proceed to Phase 1 (Design).
