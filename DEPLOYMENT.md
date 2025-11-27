# BPJS Biometric Automation Agent - Deployment Guide

## 📦 Release Package

**Version:** 1.0.0  
**Release Date:** November 20, 2025  
**Target Platform:** Windows x64 (Windows 10/11, Windows Server 2016+)  
**Runtime:** Self-contained (.NET 8.0 included, no external installation required)

## 🚀 Quick Deployment

### 1. Copy Files to Target Server

Copy the entire `publish/` folder to your target server:
```powershell
# Example deployment path
C:\Program Files\BiometricAgent\
```

**Package Contents:**
- `BiometricAgent.exe` - Main application executable
- `BiometricAgent.dll` - Core application library
- `config.example.json` - Example configuration template
- All required .NET runtime and dependencies (self-contained)
- `runtimes/` - Native dependencies for Windows

### 2. Configure the Application

1. **Create Configuration File:**
   ```powershell
   cd "C:\Program Files\BiometricAgent"
   Copy-Item config.example.json config.json
   ```

2. **Edit `config.json`:**
   ```json
   {
     "Host": "127.0.0.1",
     "Port": 5001,
     "FristaExePath": "D:\\Frista\\Frista.exe",
     "FingerExePath": "C:\\Program Files (x86)\\BPJS Kesehatan\\Aplikasi Sidik Jari BPJS Kesehatan\\After.exe",
     "Credentials": {
       "FristaUsername": "your_frista_username",
       "FristaPassword": "ENCRYPTED_PASSWORD_HERE",
       "FingerUsername": "your_finger_username",
       "FingerPassword": "ENCRYPTED_PASSWORD_HERE"
     },
     "Performance": {
       "MaxConcurrentRequests": 1,
       "QueueTimeoutSeconds": 60,
       "StartupTimeoutSeconds": 20
     }
   }
   ```

3. **Encrypt Credentials (REQUIRED):**
   ```powershell
   # Run as the same user account that will run the service
   .\BiometricAgent.exe --encrypt-password
   
   # Example:
   Enter password to encrypt: YourActualPassword
   Encrypted: AQAAANCMnd8BFdERjHoAwE/Cl+sB...
   
   # Copy encrypted string to config.json
   ```

4. **Create Logs Directory:**
   ```powershell
   New-Item -ItemType Directory -Path ".\logs" -Force
   ```

### 3. Test the Application

**Manual Test:**
```powershell
# Start the agent
.\BiometricAgent.exe

# In another terminal, test health endpoint
Invoke-RestMethod http://127.0.0.1:5001/health

# Test Frista automation (use valid BPJS number)
Invoke-RestMethod "http://127.0.0.1:5001/run_exe?bpjs=0001234567890"

# Stop agent with Ctrl+C
```

### 4. Install as Windows Service (Production)

**Option A: Using NSSM (Recommended)**

1. **Download NSSM:**
   - Download from https://nssm.cc/download
   - Extract `nssm.exe` (64-bit version)

2. **Install Service:**
   ```powershell
   # Run as Administrator
   nssm install BiometricAgent "C:\Program Files\BiometricAgent\BiometricAgent.exe"
   
   # Configure service
   nssm set BiometricAgent AppDirectory "C:\Program Files\BiometricAgent"
   nssm set BiometricAgent DisplayName "BPJS Biometric Automation Agent"
   nssm set BiometricAgent Description "Automates BPJS Frista and Finger biometric applications"
   nssm set BiometricAgent Start SERVICE_AUTO_START
   
   # Configure logging
   nssm set BiometricAgent AppStdout "C:\Program Files\BiometricAgent\logs\service-stdout.log"
   nssm set BiometricAgent AppStderr "C:\Program Files\BiometricAgent\logs\service-stderr.log"
   
   # Start service
   nssm start BiometricAgent
   ```

3. **Verify Service:**
   ```powershell
   Get-Service BiometricAgent
   Invoke-RestMethod http://127.0.0.1:5001/health
   ```

**Option B: Using sc.exe (Native Windows)**

```powershell
# Run as Administrator
sc.exe create BiometricAgent `
  binPath= "C:\Program Files\BiometricAgent\BiometricAgent.exe" `
  start= auto `
  DisplayName= "BPJS Biometric Automation Agent"

sc.exe description BiometricAgent "Automates BPJS Frista and Finger biometric applications"
sc.exe start BiometricAgent
```

### 5. Configure Firewall (If Remote Access Needed)

```powershell
# Allow inbound connections (Run as Administrator)
New-NetFirewallRule -DisplayName "BiometricAgent" `
  -Direction Inbound `
  -Protocol TCP `
  -LocalPort 5001 `
  -Action Allow `
  -Profile Domain,Private
```

⚠️ **Security Note:** Only open firewall if you need remote access. For localhost-only access (recommended), keep firewall closed.

## 🔧 Configuration Details

### Application Paths

- **Frista.exe:** Update `FristaExePath` with actual installation path
- **After.exe (Finger):** Update `FingerExePath` with actual installation path

### Performance Tuning

- **MaxConcurrentRequests:** Keep at `1` for serial execution (prevents UI conflicts)
- **QueueTimeoutSeconds:** Maximum time request waits in queue (default: 60s)
- **StartupTimeoutSeconds:** Time to wait for app window detection (increase to 20-30s if apps are slow to start)

### Logging

- **Location:** `logs/` directory (auto-created)
- **Format:** JSON structured logs with correlation IDs
- **Rotation:** Daily rotation (automatic)
- **Retention:** Configure via Serilog settings if needed

## 🏥 Health Monitoring

### Health Endpoint

```bash
GET http://127.0.0.1:5001/health
```

**Response:**
```json
{
  "status": "healthy",
  "version": "1.0.0.0",
  "timestamp": "2025-11-20T10:30:00Z",
  "uptimeSeconds": 3600,
  "memoryUsageMB": 85.3,
  "queue": {
    "currentDepth": 0,
    "totalRequests": 145,
    "completedRequests": 142,
    "failedRequests": 2,
    "timedOutRequests": 1,
    "availableSlots": 1,
    "errorRate": 0.021
  }
}
```

### Monitoring Script

```powershell
# health-check.ps1
$response = Invoke-RestMethod http://127.0.0.1:5001/health -TimeoutSec 5
if ($response.status -eq "healthy") {
    Write-Host "✓ Agent is healthy" -ForegroundColor Green
    Write-Host "Queue: $($response.queue.currentDepth) pending, $($response.queue.completedRequests) completed"
} else {
    Write-Host "✗ Agent is unhealthy" -ForegroundColor Red
    exit 1
}
```

## 🔒 Security Checklist

- [x] Credentials encrypted using DPAPI
- [x] BPJS number validation (13-digit format)
- [ ] API key authentication (future enhancement)
- [x] No credentials logged in plaintext
- [x] Service runs as specific user account (not SYSTEM)
- [x] Localhost-only binding (127.0.0.1)
- [x] HTTPS support (future enhancement - use reverse proxy if needed)

## 📊 API Endpoints

### 1. Frista Automation
```
GET /run_exe?bpjs={13-digit-bpjs}
```

### 2. Finger Automation
```
GET /run_finger_exe?bpjs={13-digit-bpjs}
```

### 3. Health Check
```
GET /health
```

### 4. Stop Frista Process
```
GET /stop_exe
```

### 5. Stop Finger Process
```
GET /stop_finger_exe
```

## 🛠️ Troubleshooting

### Service Won't Start

```powershell
# Check Windows Event Log
Get-EventLog -LogName Application -Source BiometricAgent -Newest 10

# Check service status
Get-Service BiometricAgent | Format-List *

# Test manually
cd "C:\Program Files\BiometricAgent"
.\BiometricAgent.exe
```

### Window Detection Timeout

**Solution:** Increase `StartupTimeoutSeconds` in `config.json`:
```json
"StartupTimeoutSeconds": 30
```

### Queue Backed Up

**Check queue depth:**
```powershell
$health = Invoke-RestMethod http://127.0.0.1:5001/health
$health.queue.currentDepth  # Should be < 10
```

**Solution:** Check logs for stuck automation workflows.

### Credentials Not Working

**Re-encrypt credentials:**
```powershell
# MUST run as the same user account that runs the service
.\BiometricAgent.exe --encrypt-password
```

## 📝 Maintenance

### Log Rotation

Logs auto-rotate daily. To manually clean old logs:
```powershell
# Delete logs older than 30 days
Get-ChildItem "C:\Program Files\BiometricAgent\logs\*.log" | 
  Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-30) } | 
  Remove-Item
```

### Service Management

```powershell
# Stop service
Stop-Service BiometricAgent

# Start service
Start-Service BiometricAgent

# Restart service
Restart-Service BiometricAgent

# Check logs
Get-Content "C:\Program Files\BiometricAgent\logs\biometric-agent*.log" -Tail 50
```

### Updating Application

1. Stop the service
2. Backup current `config.json`
3. Replace files in deployment directory
4. Restore `config.json`
5. Start the service
6. Verify health endpoint

```powershell
Stop-Service BiometricAgent
Copy-Item config.json config.backup.json
# Replace files...
Copy-Item config.backup.json config.json
Start-Service BiometricAgent
Invoke-RestMethod http://127.0.0.1:5001/health
```

## 🎯 Production Readiness Checklist

- [ ] Configuration file created with correct paths
- [ ] Credentials encrypted using DPAPI
- [ ] Service installed and auto-starts
- [ ] Health endpoint responds successfully
- [ ] Logs directory created with write permissions
- [ ] Test automation with valid BPJS number
- [ ] Firewall configured (if remote access needed)
- [ ] Monitoring script scheduled (optional)
- [ ] Documentation reviewed by operations team
- [ ] Rollback plan documented

## 📞 Support

For issues, check:
1. Application logs: `logs/biometric-agent*.log`
2. Service logs: `logs/service-*.log` (if using NSSM)
3. Windows Event Viewer: Application log
4. TESTING-GUIDE.md for detailed testing procedures
5. TROUBLESHOOTING.md for common issues

---

**Built with .NET 8.0 | Self-contained deployment | Production-ready** 🚀
