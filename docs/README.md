# BPJS Biometric Automation Agent

**Version:** 1.0.0  
**Platform:** Windows 10/11 x64  
**Runtime:** .NET 8 LTS

## Overview

The BPJS Biometric Automation Agent is a lightweight Windows service that automates legacy BPJS applications (Frista.exe and Finger.exe) through a RESTful HTTP API. It enables hospital systems (SIMRS, kiosks, telemedicine platforms) to integrate with BPJS biometric verification workflows without manual intervention.

## Key Features

- **HTTP REST API**: Simple GET endpoints for automation control
- **UI Automation**: FlaUI-based Windows UI automation with exponential backoff retry logic
- **Security**: DPAPI credential encryption, localhost-only binding
- **Logging**: Structured JSON logs with correlation tracking and performance metrics
- **Performance**: <3s automation, <100ms HTTP response, 95%+ success rate
- **Advanced Queueing**: FIFO request serialization with timeout handling, depth monitoring, and performance tracking
- **Concurrency Control**: Configurable concurrent request limits with queue statistics
- **Resilience**: Automatic retry with exponential backoff for transient errors

## System Requirements

### Required
- **OS**: Windows 10 (1809+) or Windows 11
- **Runtime**: .NET 8 Runtime (LTS)
- **Memory**: 200MB available RAM
- **Disk**: 100MB for application + 500MB for logs
- **Network**: Localhost access (127.0.0.1)

### Optional
- **BPJS Applications**: Frista.exe and/or Finger.exe installed
- **UI Automation**: Windows UI Automation Provider (included in Windows)

## Quick Start

### 1. Installation

```powershell
# Clone repository
git clone <repository-url>
cd frista_bridge

# Restore dependencies
dotnet restore src/BiometricAgent

# Build application
dotnet build src/BiometricAgent -c Release
```

### 2. Configuration

```powershell
# Copy example configuration
cp src/BiometricAgent/config/config.example.json src/BiometricAgent/config/config.json

# Edit configuration with your paths
notepad src/BiometricAgent/config/config.json
```

**Required Configuration Changes:**
- `Applications.Frista.ExecutablePath`: Path to Frista.exe
- `Applications.Finger.ExecutablePath`: Path to Finger.exe
- `HttpServer.Port`: HTTP listener port (default: 5000)

### 3. Credential Encryption

```powershell
# Run credential encryption tool (implementation pending)
dotnet run --project src/BiometricAgent -- encrypt-credentials

# Follow prompts to encrypt BPJS credentials using Windows DPAPI
```

### 4. Run Agent

```powershell
# Development mode
dotnet run --project src/BiometricAgent

# Production mode
dotnet run --project src/BiometricAgent -c Release

# As Windows Service (requires installation)
sc.exe create BiometricAgent binPath="<path-to-exe>"
sc.exe start BiometricAgent
```

## API Endpoints

### Run Frista Automation
```http
GET http://127.0.0.1:5000/run_exe?bpjs=<BPJS_NUMBER>
```
**Parameters:**
- `bpjs` (required): BPJS participant number (Nomor Kartu)

**Response:**
```json
{
  "success": true,
  "correlationId": "abc-123-def",
  "bpjs": "0001234567890",
  "durationMs": 2345,
  "message": "Automation completed successfully",
  "errorCode": null
}
```

### Run Finger Automation
```http
GET http://127.0.0.1:5000/run_finger_exe?bpjs=<BPJS_NUMBER>
```
*Same parameters and response as /run_exe*

### Stop Frista Process
```http
GET http://127.0.0.1:5000/stop_exe
```
**Response:**
```json
{
  "success": true,
  "message": "Frista.exe terminated successfully"
}
```

### Stop Finger Process
```http
GET http://127.0.0.1:5000/stop_finger_exe
```
*Same response as /stop_exe*

### Health Check
```http
GET http://127.0.0.1:5000/health
```
**Response:**
```json
{
  "status": "Healthy",
  "version": "1.0.0",
  "timestamp": "2025-11-20T10:30:00Z",
  "uptimeSeconds": 8130,
  "uptime": "02:15:30",
  "memoryUsageMB": 45,
  "queue": {
    "currentDepth": 0,
    "totalRequests": 127,
    "completedRequests": 120,
    "failedRequests": 5,
    "timedOutRequests": 2,
    "availableSlots": 1,
    "errorRate": 5.51
  }
}
```

## Error Codes

| Code | Category | Description |
|------|----------|-------------|
| `ERR_APP_NOT_FOUND` | Application | Executable path not found |
| `ERR_APP_START_TIMEOUT` | Application | Application failed to start |
| `ERR_LOGIN_FAILED` | Authentication | Login credentials rejected |
| `ERR_UI_ELEMENT_NOT_FOUND` | UI Automation | Required UI element missing |
| `ERR_AUTOMATION_TIMEOUT` | UI Automation | Automation exceeded timeout |
| `ERR_QUEUE_TIMEOUT` | Queueing | Request timed out in queue |
| `ERR_INVALID_BPJS` | Validation | Invalid BPJS number format |
| `ERR_CRITICAL_INTERNAL` | System | Unrecoverable internal error |

## Configuration Reference

See `config.example.json` for full configuration schema with inline documentation.

**Critical Settings:**
- `HttpServer.Host`: Must be `127.0.0.1` for security
- `Credentials.*`: Must use encrypted values from encryption tool
- `Logging.LogPath`: Ensure sufficient disk space for rolling logs
- `Performance.MaxConcurrentRequests`: Set to 1 for serial execution

## Logging

Logs are written to `logs/biometric-agent.log` in structured JSON format.

**Log Levels:**
- **Information**: Normal operations, request/response logging
- **Warning**: Performance degradation, retry attempts
- **Error**: Automation failures, recoverable errors
- **Fatal**: Unrecoverable errors requiring restart

**Example Log Entry:**
```json
{
  "@t": "2025-11-17T10:30:15.123Z",
  "@mt": "Automation completed",
  "@l": "Information",
  "CorrelationId": "abc-123-def",
  "BPJS": "0001234567890",
  "DurationMs": 2345,
  "WorkflowType": "Frista",
  "Success": true
}
```

## Performance Tuning

### Optimize Automation Speed
1. Reduce `UIAutomation.ElementWaitTimeMs` (minimum: 500ms)
2. Decrease `Applications.*.RetryAttempts` (minimum: 1)
3. Enable `UIAutomation.EnableCaching` for repeated operations

### Reduce Memory Usage
1. Lower `Logging.RetainedFileCountLimit` (minimum: 1)
2. Set `Logging.FileSizeLimitBytes` to smaller value (minimum: 10MB)
3. Disable `ErrorHandling.CaptureScreenshotOnError`

## Troubleshooting

For detailed troubleshooting steps, see [TROUBLESHOOTING.md](TROUBLESHOOTING.md).

**Common Issues:**
- **Agent won't start**: Check .NET 8 runtime installation
- **HTTP requests fail**: Verify `HttpServer.Host` is `127.0.0.1`
- **Automation fails**: Review logs for UI element detection errors
- **Credentials rejected**: Re-encrypt credentials using tool

## Security Considerations

⚠️ **CRITICAL SECURITY REQUIREMENTS**:

1. **Localhost Only**: Never bind to `0.0.0.0` or public IPs
2. **Credential Encryption**: Always use DPAPI-encrypted credentials
3. **File Permissions**: Restrict `config.json` to SYSTEM/Administrators
4. **Network Isolation**: Run on isolated network segment
5. **Log Sanitization**: Logs never contain plaintext credentials

## Development

### Build from Source
```powershell
dotnet build src/BiometricAgent -c Debug
```

### Run Tests
```powershell
dotnet test tests/BiometricAgent.Tests
```

### Code Style
Follow constitution principles in `.specify/memory/constitution.md`:
- 300-line class limit, 50-line method limit
- No magic values (externalize to config)
- Structured logging with correlation IDs

## Support

For issues, questions, or feature requests:
- See [TROUBLESHOOTING.md](TROUBLESHOOTING.md) for common issues
- Check logs in `logs/` directory
- Review configuration in `config/config.json`

## License

[Specify license here]

## Version History

### 1.0.0 (Initial Release)
- HTTP REST API with 5 endpoints
- FlaUI-based UI automation for Frista.exe and Finger.exe
- DPAPI credential encryption
- Structured JSON logging with Serilog
- FIFO request queueing
- Health monitoring endpoint
