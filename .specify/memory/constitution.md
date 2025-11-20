<!--
SYNC IMPACT REPORT - Constitution v1.0.0

VERSION CHANGE: Initial → 1.0.0
RATIONALE: First constitution version establishing foundational principles for
the Frista Bridge biometric automation agent.

PRINCIPLES ADDED:
- I. Code Readability First
- II. Consistent User Experience
- III. Explicit Over Implicit
- IV. Structured Logging & Observability
- V. Configuration-Driven Behavior

SECTIONS ADDED:
- Core Principles (5 principles focused on readability & UX consistency)
- Quality Standards (code style, documentation, error handling requirements)
- Development Workflow (review gates, testing requirements, deployment policies)
- Governance (amendment procedures, compliance verification)

TEMPLATES STATUS:
✅ plan-template.md - Reviewed, aligned with observability and testing principles
✅ spec-template.md - Reviewed, aligned with user-centric design and testability
✅ tasks-template.md - Reviewed, aligned with incremental delivery principles

FOLLOW-UP TODOS: None
-->

# Frista Bridge Constitution

## Core Principles

### I. Code Readability First

Code MUST prioritize human comprehension over clever optimizations.

**Non-Negotiable Rules:**

- Variable and method names MUST be self-documenting and use full words (no
  abbreviations except industry-standard acronyms like HTTP, API, BPJS, NOKA).
- Complex logic MUST be broken into named methods with clear single
  responsibilities.
- Magic numbers and strings are FORBIDDEN—use named constants with descriptive
  names.
- Classes MUST NOT exceed 300 lines; methods MUST NOT exceed 50 lines.
- All public APIs MUST include XML documentation comments explaining purpose,
  parameters, return values, and exceptions.

**Rationale:** The Frista Bridge agent automates critical healthcare workflows.
Code maintainability directly impacts patient care reliability. Future
developers (or the original author six months later) must understand intent
immediately without mental gymnastics.

### II. Consistent User Experience

All interaction surfaces (HTTP endpoints, configuration files, logs, error
messages) MUST follow consistent patterns.

**Non-Negotiable Rules:**

- HTTP endpoints MUST use consistent naming conventions (`/run_exe`,
  `/run_finger_exe`, `/stop_exe`, `/stop_finger_exe`).
- Error responses MUST follow a single JSON schema: `{ "status": "error",
  "code": "ERROR_CODE", "message": "Human-readable description" }`.
- Success responses MUST follow: `{ "status": "success", "data": {...} }`.
- Configuration schema MUST be validated on startup with clear error messages
  for missing or invalid values.
- All user-facing messages (logs, errors, API responses) MUST be in English and
  written for hospital IT staff, not developers.

**Rationale:** Hospital operators and integration developers need predictable
behavior. Inconsistent patterns cause integration errors, debugging delays, and
production incidents in clinical environments.

### III. Explicit Over Implicit

All behavior, dependencies, and assumptions MUST be explicitly declared—no
hidden magic.

**Non-Negotiable Rules:**

- Application paths, credentials, window titles, and UI element identifiers
  MUST be in `config.json`, never hardcoded.
- All external dependencies (BPJS app locations, .NET runtime requirements,
  Windows version) MUST be documented in README.
- Method signatures MUST explicitly declare all parameters—no default
  parameters that hide behavior.
- Startup validation MUST check all preconditions (files exist, processes
  accessible) and fail fast with actionable messages.
- No reflection-based magic or runtime code generation unless absolutely
  unavoidable and explicitly justified in code comments.

**Rationale:** Healthcare automation must be auditable and debuggable. Implicit
behavior creates "works on my machine" problems that are unacceptable when
patient registration depends on the system.

### IV. Structured Logging & Observability

Every significant action MUST emit structured logs; failures MUST be traceable.

**Non-Negotiable Rules:**

- Use Serilog with structured logging—log objects, not concatenated strings.
- Every automation step MUST log: timestamp, action, target element
  (AutomationId/Name), duration, outcome.
- Exceptions MUST log full stack traces plus contextual data (process ID,
  window title, last successful step).
- Performance metrics MUST be logged for each complete automation flow (launch
  to completion time).
- Log levels MUST be used correctly: Error (automation failed), Warning
  (unexpected but handled), Information (normal operations), Debug (internal
  details).

**Rationale:** When a biometric check fails at 3 AM in a hospital emergency
department, logs must answer "what failed" and "why" in under 60 seconds. Poor
logging turns a 5-minute fix into a 2-hour investigation.

### V. Configuration-Driven Behavior

All environment-specific values MUST be externalized to configuration files.

**Non-Negotiable Rules:**

- Application paths, network settings, timeouts, window titles MUST be in
  `config.json`.
- Credentials MUST be encrypted using Windows DPAPI, never plain text.
- Configuration schema MUST be documented in README with examples.
- Changes to configuration MUST NOT require recompilation or redeployment of
  binaries.
- Config validation MUST occur at startup with specific error messages for each
  invalid field.

**Rationale:** The agent deploys to diverse hospital environments (different
BPJS versions, varying network policies, multiple kiosk configurations).
Hardcoded values cause deployment failures and emergency patching cycles.

## Quality Standards

### Code Style & Formatting

- MUST follow C# coding conventions (PascalCase for public members, camelCase
  for private fields).
- MUST use EditorConfig to enforce consistent indentation (4 spaces, no tabs).
- MUST run code formatter before every commit.
- MUST pass static analysis (no warnings) before merge.

### Documentation Requirements

- README MUST include: system requirements, installation steps, configuration
  guide, troubleshooting section.
- Each public class MUST have XML doc comment explaining its responsibility.
- Complex algorithms MUST include inline comments explaining the "why," not the
  "what."
- `config.json` MUST be accompanied by `config.example.json` with sample
  values.

### Error Handling Standards

- NEVER swallow exceptions silently—log and handle or propagate.
- User-facing error messages MUST suggest corrective actions (e.g., "Login
  failed: Verify credentials in config.json").
- Transient errors (process not ready, window not found) MUST retry with
  exponential backoff, max 3 attempts.
- Fatal errors (config invalid, BPJS app not installed) MUST fail fast at
  startup.

## Development Workflow

### Pre-Commit Requirements

1. All changes MUST pass local build with zero errors.
2. Code MUST be formatted using project style guide.
3. New public methods MUST include XML documentation.

### Code Review Gates

1. Reviewer MUST verify compliance with all Core Principles.
2. Changes to automation logic MUST include justification for element selector
   choices (why AutomationId vs. Name).
3. Configuration changes MUST update `config.example.json`.
4. Error handling additions MUST include corresponding log statements.

### Testing Requirements

- Critical paths (launch app, login, inject NOKA, trigger fingerprint) MUST be
  manually tested on target BPJS versions before merge.
- Breaking changes to HTTP API MUST be documented in CHANGELOG.
- Configuration schema changes MUST include migration notes.

### Deployment Policies

- Releases MUST follow semantic versioning: MAJOR.MINOR.PATCH.
- Release notes MUST include: new features, breaking changes, bug fixes,
  configuration changes.
- Production deployments MUST be tested on a staging kiosk first.

## Governance

### Amendment Procedure

1. Proposed changes MUST be documented with rationale and impact assessment.
2. Breaking changes to principles REQUIRE major version bump (e.g., 1.x.x →
   2.0.0).
3. New principles or expanded guidance REQUIRE minor version bump (e.g., 1.0.x
   → 1.1.0).
4. Clarifications and typo fixes REQUIRE patch version bump (e.g., 1.0.0 →
   1.0.1).

### Compliance Verification

- All pull requests MUST include a "Constitution Compliance" checklist.
- Reviewers MUST explicitly verify no violations exist or that complexity is
  justified.
- Quarterly audits SHOULD review codebase for drift from principles.

### Complexity Justification

Violations are permitted ONLY when:

1. Explicitly documented in code comments with rationale.
2. Included in plan.md "Complexity Tracking" table.
3. Simpler alternatives are documented and explained why they were rejected.

**Version**: 1.0.0 | **Ratified**: 2025-11-17 | **Last Amended**: 2025-11-17
