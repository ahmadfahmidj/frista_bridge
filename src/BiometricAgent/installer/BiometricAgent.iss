; ============================================
; Biometric Agent - Inno Setup Installer Script
; ============================================
; This script creates an installer for the BiometricAgent application.
; Performs a CLEAN INSTALL - removes any existing service and processes.
; 
; Prerequisites:
; 1. Build the project in Release mode first:
;    dotnet publish -c Release -r win-x64 --self-contained true
;
; 2. Install Inno Setup from: https://jrsoftware.org/isinfo.php
;
; 3. Compile this script using Inno Setup Compiler (ISCC.exe)
;    or open it in Inno Setup and press Ctrl+F9 to compile.
; ============================================

#define MyAppName "Biometric Agent"
#define MyAppVersion "1.0.2"
#define MyAppPublisher "Frista"
#define MyAppURL "https://frista.id"
#define MyAppExeName "BiometricAgent.exe"
#define MyAppServiceName "BiometricAgent"

; Path to the publish output (relative to this .iss file)
#define PublishDir "..\bin\Release\net8.0\win-x64\publish"

[Setup]
; Application identity
AppId={{A8C7B5D3-9E4F-4A2B-8C1D-7E6F5A3B2C1D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

; Installation directories
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes

; Output settings
OutputDir=.\Output
OutputBaseFilename=BiometricAgent-Setup-{#MyAppVersion}
SetupIconFile=..\asset\heartbeat.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

; Compression
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes

; Privileges (requires admin for clean install)
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog

; UI settings
WizardStyle=modern

; Version info
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Installer
VersionInfoCopyright=Copyright (C) 2025 {#MyAppPublisher}
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}

; Allow running on Windows 10 and later
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon"; Description: "Start automatically with Windows"; GroupDescription: "Startup Options:"; Flags: unchecked
Name: "installservice"; Description: "Install as Windows Service (auto-start on boot)"; GroupDescription: "Service Options:"; Flags: unchecked

[Files]
; Main executable - use published self-contained build
Source: "{#PublishDir}\BiometricAgent.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\BiometricAgent.pdb"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

; If not self-contained, include all DLLs
Source: "{#PublishDir}\*.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "{#PublishDir}\*.json"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

; Runtime folders (for non-self-contained builds)
Source: "{#PublishDir}\runtimes\*"; DestDir: "{app}\runtimes"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

; Localization folders
Source: "{#PublishDir}\cs\*"; DestDir: "{app}\cs"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#PublishDir}\de\*"; DestDir: "{app}\de"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#PublishDir}\es\*"; DestDir: "{app}\es"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#PublishDir}\fr\*"; DestDir: "{app}\fr"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#PublishDir}\it\*"; DestDir: "{app}\it"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#PublishDir}\ja\*"; DestDir: "{app}\ja"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#PublishDir}\ko\*"; DestDir: "{app}\ko"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#PublishDir}\pl\*"; DestDir: "{app}\pl"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#PublishDir}\pt-BR\*"; DestDir: "{app}\pt-BR"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#PublishDir}\ru\*"; DestDir: "{app}\ru"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#PublishDir}\tr\*"; DestDir: "{app}\tr"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#PublishDir}\zh-Hans\*"; DestDir: "{app}\zh-Hans"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#PublishDir}\zh-Hant\*"; DestDir: "{app}\zh-Hant"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

; Native libraries
Source: "{#PublishDir}\win-x64\*"; DestDir: "{app}\win-x64"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

; Configuration files
Source: "..\config\config.example.json"; DestDir: "{app}\config"; Flags: ignoreversion
; Only copy config.json if it doesn't exist (don't overwrite user settings on upgrade)
Source: "..\config\config.json"; DestDir: "{app}\config"; DestName: "config.json"; Flags: onlyifdoesntexist

; Asset files (icons, etc.)
Source: "..\asset\*"; DestDir: "{app}\asset"; Flags: ignoreversion recursesubdirs createallsubdirs

[Dirs]
; Create logs directory with appropriate permissions
Name: "{app}\logs"; Permissions: users-modify

[Icons]
; Start Menu icons
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{group}\{#MyAppName} - Encrypt Password"; Filename: "{app}\{#MyAppExeName}"; Parameters: "--encrypt-password"; WorkingDir: "{app}"
Name: "{group}\Configuration"; Filename: "{app}\config\config.json"
Name: "{group}\View Logs"; Filename: "{app}\logs"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"

; Desktop icon (optional)
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

; Startup folder (optional - for auto-start as application)
Name: "{autostartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: startupicon

[Run]
; Option to run after installation
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

; Option to open configuration after installation
Filename: "notepad.exe"; Parameters: """{app}\config\config.json"""; Description: "Open configuration file"; Flags: nowait postinstall skipifsilent unchecked

[Code]

// Clean up existing installation - stop service, kill processes, delete service
procedure CleanupExistingInstallation();
var
  ResultCode: Integer;
begin
  Log('=== Starting clean installation ===');
  
  // Step 1: Stop the Windows Service if it exists
  Log('Stopping BiometricAgent service...');
  Exec(ExpandConstant('{sys}\sc.exe'), 'stop {#MyAppServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(2000);
  
  // Step 2: Delete the Windows Service if it exists
  Log('Deleting BiometricAgent service...');
  Exec(ExpandConstant('{sys}\sc.exe'), 'delete {#MyAppServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(1000);
  
  // Step 3: Kill any running BiometricAgent processes using taskkill
  Log('Killing any running BiometricAgent processes...');
  Exec('taskkill', '/F /IM BiometricAgent.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(1000);
  
  // Step 4: Kill any process using port 5001
  Log('Freeing port 5001...');
  Exec('powershell.exe', 
       '-NoProfile -ExecutionPolicy Bypass -Command "' +
       'Get-NetTCPConnection -LocalPort 5001 -ErrorAction SilentlyContinue | ' +
       'ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }"',
       '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(1000);
  
  Log('=== Cleanup completed ===');
end;

// Add Windows Firewall rule for the agent
procedure AddFirewallRule();
var
  ResultCode: Integer;
begin
  // Remove existing rule first (ignore errors)
  Exec('netsh', 'advfirewall firewall delete rule name="{#MyAppName}"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  
  // Add inbound rule for TCP port 5001
  if Exec('netsh', 'advfirewall firewall add rule name="{#MyAppName}" dir=in action=allow protocol=TCP localport=5001 program="' + ExpandConstant('{app}\{#MyAppExeName}') + '" enable=yes profile=any', 
          '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    Log('Windows Firewall rule added successfully for port 5001');
  end
  else
  begin
    Log('Failed to add Windows Firewall rule');
  end;
end;

// Add URL ACL reservation for HTTP.sys (required for Windows Service)
procedure AddUrlAcl();
var
  ResultCode: Integer;
begin
  // Remove port 5001 from Windows excluded port range if present (Hyper-V sometimes reserves it)
  Exec('netsh', 'int ipv4 delete excludedportrange protocol=tcp numberofports=1 startport=5001',
       '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Log('Attempted to remove port 5001 from excluded port range (safe to ignore if not present)');

  // Remove existing URL ACL first (ignore errors)
  Exec('netsh', 'http delete urlacl url=http://127.0.0.1:5001/', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('netsh', 'http delete urlacl url=http://+:5001/', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  
  // Add URL ACL for Everyone to allow binding
  if Exec('netsh', 'http add urlacl url=http://+:5001/ user=Everyone', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    Log('URL ACL reservation added for port 5001');
  end
  else
  begin
    Log('Failed to add URL ACL reservation');
  end;
end;

// Remove URL ACL reservation
procedure RemoveUrlAcl();
var
  ResultCode: Integer;
begin
  Exec('netsh', 'http delete urlacl url=http://+:5001/', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('netsh', 'http delete urlacl url=http://127.0.0.1:5001/', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Log('URL ACL reservation removed');
end;

// Install the Windows Service
procedure InstallWindowsService();
var
  ResultCode: Integer;
  ServicePath: String;
begin
  ServicePath := ExpandConstant('{app}\{#MyAppExeName}');
  
  // Create the Windows Service
  if Exec(ExpandConstant('{sys}\sc.exe'), 
          'create {#MyAppServiceName} binPath= "' + ServicePath + '" start= auto DisplayName= "{#MyAppName}"',
          '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    // Set service description
    Exec(ExpandConstant('{sys}\sc.exe'),
         'description {#MyAppServiceName} "Biometric Automation Agent for BPJS integration. Provides HTTP API for automating Frista and Finger applications."',
         '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    
    // Configure service recovery options (restart on failure): 5s, 10s, 30s
    Exec(ExpandConstant('{sys}\sc.exe'),
         'failure {#MyAppServiceName} reset= 86400 actions= restart/5000/restart/10000/restart/30000',
         '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    
    Log('Windows Service installed successfully');
    
    // Start the service
    Exec(ExpandConstant('{sys}\sc.exe'), 'start {#MyAppServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Log('Windows Service started');
  end
  else
  begin
    Log('Failed to install Windows Service');
  end;
end;

// Remove Windows Firewall rule
procedure RemoveFirewallRule();
var
  ResultCode: Integer;
begin
  Exec('netsh', 'advfirewall firewall delete rule name="{#MyAppName}"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Log('Windows Firewall rule removed');
end;

// Called before installation starts
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
  begin
    // Clean up any existing installation
    CleanupExistingInstallation();
  end
  else if CurStep = ssPostInstall then
  begin
    // Add Windows Firewall rule for HTTP API access
    AddFirewallRule();
    
    // Add URL ACL reservation (required for service to bind to HTTP)
    AddUrlAcl();
    
    // Install as Windows Service if selected
    if WizardIsTaskSelected('installservice') then
    begin
      InstallWindowsService();
    end;
  end;
end;

// Called before uninstallation
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    // Stop and delete service (in case it was manually installed)
    Exec(ExpandConstant('{sys}\sc.exe'), 'stop {#MyAppServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Sleep(2000);
    Exec(ExpandConstant('{sys}\sc.exe'), 'delete {#MyAppServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    
    // Kill any running processes
    Exec('taskkill', '/F /IM BiometricAgent.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    
    // Remove Windows Firewall rule
    RemoveFirewallRule();
    
    // Remove URL ACL reservation
    RemoveUrlAcl();
  end;
end;

// Initialize setup
function InitializeSetup(): Boolean;
begin
  Result := True;
end;
