@echo off
REM Distributed Audio System - Install Driver (After Reboot)
REM Run this after Windows restarts with test signing enabled

echo ========================================
echo Distributed Audio System
echo Driver Installation
echo ========================================
echo.

REM Check administrator privileges
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo ERROR: This script requires administrator privileges.
    echo Please run as administrator.
    pause
    exit /b 1
)

REM Check test signing is enabled
bcdedit | findstr /C:"testsigning Yes" >nul
if %errorLevel% neq 0 (
    echo WARNING: Test signing mode does not appear to be enabled.
    echo This installation may fail.
    echo.
    echo Please run InstallDriver.bat first and restart Windows.
    pause
)

REM Install driver using pnputil
set DRIVER_DIR=%~dp0VirtualAudioDriver\build
set INF_FILE=%DRIVER_DIR%\distributedaudio.inf

if not exist "%INF_FILE%" (
    echo ERROR: Driver INF not found: %INF_FILE%
    pause
    exit /b 1
)

echo Installing driver...
pnputil /add-driver "%INF_FILE%" /install

if %errorLevel% equ 0 (
    echo.
    echo ========================================
    echo Driver installed successfully!
    echo ========================================
    echo.
    echo You can now select "DistributedAudio Virtual Speaker"
    echo as your audio output device in Windows sound settings.
    echo.
) else (
    echo.
    echo ========================================
    echo Driver installation FAILED!
    echo ========================================
    echo.
    echo Please check the error messages above.
    echo.
    echo Common issues:
    echo 1. Test signing not enabled - run InstallDriver.bat
    echo 2. Driver not built - build the solution first
    echo 3. Windows version not supported - requires Windows 7+
    echo.
)

pause
