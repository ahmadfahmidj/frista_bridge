## Plan: Publish BiometricAgent as Windows Service EXE

Package the BiometricAgent as a single-file Windows executable that runs as a native Windows Service in the background on KIOSK machines.

### Steps

1. **Add Windows Service hosting package** in [BiometricAgent.csproj](src/BiometricAgent/BiometricAgent.csproj) — Add `Microsoft.Extensions.Hosting.WindowsServices` NuGet reference to enable native Windows Service support.

2. **Configure Windows Service hosting** in [Program.cs](src/BiometricAgent/Program.cs) — Add `builder.Host.UseWindowsService(options => { options.ServiceName = "BiometricAgent"; })` before `builder.Build()` to enable service mode.

3. **Add single-file publish settings** to [BiometricAgent.csproj](src/BiometricAgent/BiometricAgent.csproj) — Configure `<PublishSingleFile>`, `<SelfContained>`, `<RuntimeIdentifier>win-x64</RuntimeIdentifier>`, and `<IncludeNativeLibrariesForSelfExtract>` for a clean single EXE output.

4. **Publish the application** — Run `dotnet publish -c Release -r win-x64 --self-contained true` to generate the standalone executable.

5. **Install as Windows Service on KIOSK** — Use `sc.exe create BiometricAgent binPath= "C:\path\BiometricAgent.exe" start= auto` to register and auto-start the service on boot.

### Further Considerations

1. **Service recovery options?** Configure automatic restart on failure via `sc.exe failure BiometricAgent reset= 86400 actions= restart/60000` for KIOSK reliability.

2. **Config file path for service mode?** When running as a service, the working directory differs — may need to update `ConfigurationLoader.cs` to resolve paths relative to the executable location.

3. **Logging directory permissions?** Ensure the service account (LocalSystem/NetworkService) has write access to the logs folder on the KIOSK machine.
