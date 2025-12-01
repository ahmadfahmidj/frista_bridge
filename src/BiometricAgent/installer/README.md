# Biometric Agent Installer

This folder contains the Inno Setup script and build tools for creating the BiometricAgent Windows installer.

## Prerequisites

1. **.NET 8.0 SDK** - Required for building the application
   - Download: https://dotnet.microsoft.com/download/dotnet/8.0

2. **Inno Setup 6** - Required for compiling the installer
   - Download: https://jrsoftware.org/isinfo.php
   - Install to the default location (`C:\Program Files (x86)\Inno Setup 6\`)

## Quick Start

### Option 1: Using the Build Script (Recommended)

Simply run the batch script:

```batch
cd installer
build-installer.bat
```

This will:
1. Clean and build the project in Release mode
2. Publish as a self-contained single-file executable
3. Compile the Inno Setup installer
4. Open the output folder with the completed installer

### Option 2: Manual Build

1. **Build and publish the application:**

   ```batch
   cd src\BiometricAgent
   dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
   ```

2. **Compile the installer:**
   
   Open `BiometricAgent.iss` in Inno Setup Compiler and press `Ctrl+F9` to compile.
   
   Or use command line:
   ```batch
   "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" BiometricAgent.iss
   ```

3. **Find the output:**
   
   The installer will be created in the `installer\Output\` folder.

## Installer Features

The installer provides:

- **Installation Types:**
  - **Full**: Application + Windows Service registration
  - **Compact**: Application only (run manually or via startup)
  - **Custom**: Choose components

- **Components:**
  - Main application executable
  - Configuration files (`config.example.json` and `config.json`)
  - Asset files (icons)
  - Logs directory

- **Optional Features:**
  - Desktop shortcut
  - Auto-start with Windows (as application)
  - Windows Service installation with auto-recovery

- **Start Menu Shortcuts:**
  - Biometric Agent (launch application)
  - Encrypt Password utility
  - Open Configuration file
  - View Logs folder
  - Uninstall

## Windows Service

When installed as a Windows Service:

- **Service Name:** `BiometricAgent`
- **Display Name:** `Biometric Agent`
- **Start Type:** Automatic
- **Recovery:** Restarts automatically on failure (after 60 seconds)

### Managing the Service

```batch
# Start the service
sc start BiometricAgent

# Stop the service
sc stop BiometricAgent

# Check service status
sc query BiometricAgent

# View service configuration
sc qc BiometricAgent
```

## Configuration

After installation, configure the application by editing:

```
C:\Program Files\Biometric Agent\config\config.json
```

**Important:** Use the "Encrypt Password" utility from the Start Menu to encrypt credentials before adding them to the configuration file.

## File Structure After Installation

```
C:\Program Files\Biometric Agent\
├── BiometricAgent.exe          # Main executable
├── config\
│   ├── config.json             # User configuration
│   └── config.example.json     # Reference configuration
├── asset\
│   └── heartbeat.ico           # Application icon
└── logs\                       # Log files directory
```

## Customization

### Changing the Version

Edit the version number in `BiometricAgent.iss`:

```iss
#define MyAppVersion "1.0.0"
```

### Changing the Publisher Info

Edit the publisher constants:

```iss
#define MyAppPublisher "Frista"
#define MyAppURL "https://frista.id"
```

### Adding Files

Add new files to the `[Files]` section:

```iss
Source: "path\to\file"; DestDir: "{app}\destination"; Flags: ignoreversion
```

## Troubleshooting

### Build Errors

1. **"Inno Setup not found"** - Install Inno Setup 6 to the default location
2. **"dotnet command not found"** - Install .NET 8.0 SDK and restart terminal
3. **"Access denied"** - Run as Administrator for service installation

### Runtime Issues

1. **Service won't start** - Check `config.json` for valid configuration
2. **Port already in use** - Change the port in `config.json`
3. **Permission errors** - Ensure the service user has access to configured paths

## License

Copyright (C) 2025 Frista. All rights reserved.
