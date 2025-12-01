@echo off
REM ============================================
REM Biometric Agent - Build and Package Script
REM ============================================
REM This script builds the application and creates the installer.
REM
REM Prerequisites:
REM   1. .NET 8.0 SDK installed
REM   2. Inno Setup 6 installed (https://jrsoftware.org/isinfo.php)
REM
REM Usage:
REM   build-installer.bat [--skip-build]
REM ============================================

setlocal enabledelayedexpansion

echo.
echo ============================================
echo  Biometric Agent - Build and Package
echo ============================================
echo.

REM Check for skip-build flag
set SKIP_BUILD=0
if "%1"=="--skip-build" set SKIP_BUILD=1

REM Set paths
set PROJECT_DIR=%~dp0..
set PUBLISH_DIR=%PROJECT_DIR%\bin\Release\net8.0\win-x64\publish
set INSTALLER_DIR=%~dp0
set OUTPUT_DIR=%INSTALLER_DIR%Output

REM Check if Inno Setup is installed
set ISCC_PATH=
if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" (
    set ISCC_PATH=C:\Program Files ^(x86^)\Inno Setup 6\ISCC.exe
) else if exist "C:\Program Files\Inno Setup 6\ISCC.exe" (
    set ISCC_PATH=C:\Program Files\Inno Setup 6\ISCC.exe
) else (
    echo [ERROR] Inno Setup 6 not found!
    echo Please install Inno Setup from: https://jrsoftware.org/isinfo.php
    echo.
    pause
    exit /b 1
)

echo [INFO] Using Inno Setup: %ISCC_PATH%
echo.

REM Step 1: Build and Publish the application
if %SKIP_BUILD%==0 (
    echo [STEP 1/3] Building and publishing application...
    echo.
    
    pushd "%PROJECT_DIR%"
    
    REM Clean previous build
    echo Cleaning previous build...
    dotnet clean -c Release -v q
    if errorlevel 1 (
        echo [ERROR] Clean failed!
        popd
        pause
        exit /b 1
    )
    
    REM Publish self-contained application
    echo Publishing self-contained application...
    dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
    if errorlevel 1 (
        echo [ERROR] Publish failed!
        popd
        pause
        exit /b 1
    )
    
    popd
    
    echo [OK] Application published successfully!
    echo     Output: %PUBLISH_DIR%
    echo.
) else (
    echo [STEP 1/3] Skipping build (--skip-build flag set)
    echo.
)

REM Verify publish output exists
if not exist "%PUBLISH_DIR%\BiometricAgent.exe" (
    echo [ERROR] Published executable not found!
    echo Expected: %PUBLISH_DIR%\BiometricAgent.exe
    echo.
    echo Please run without --skip-build flag first.
    pause
    exit /b 1
)

REM Step 2: Create output directory
echo [STEP 2/3] Preparing output directory...
if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"
echo [OK] Output directory ready: %OUTPUT_DIR%
echo.

REM Step 3: Compile installer
echo [STEP 3/3] Compiling installer...
echo.

"%ISCC_PATH%" "%INSTALLER_DIR%BiometricAgent.iss"
if errorlevel 1 (
    echo.
    echo [ERROR] Installer compilation failed!
    pause
    exit /b 1
)

echo.
echo ============================================
echo  Build Complete!
echo ============================================
echo.
echo Installer created at:
dir /b "%OUTPUT_DIR%\*.exe" 2>nul
echo.
echo Full path: %OUTPUT_DIR%
echo.

REM Open output folder
explorer "%OUTPUT_DIR%"

pause
exit /b 0
