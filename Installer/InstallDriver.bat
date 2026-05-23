@echo off
REM Distributed Audio System - Test Mode Driver Installation
REM For development/testing without signed driver

echo ========================================
echo Distributed Audio System
echo Test Mode Driver Installation
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

REM Enable test signing mode
echo Enabling test signing mode...
bcdedit /set testsigning on
if %errorLevel% neq 0 (
    echo ERROR: Failed to enable test signing mode
    pause
    exit /b 1
)

echo.
echo Test signing mode enabled.
echo IMPORTANT: You must restart Windows for test signing to take effect.
echo.

REM Check if driver files exist
set DRIVER_DIR=%~dp0VirtualAudioDriver\build
if not exist "%DRIVER_DIR%\distributedaudio.sys" (
    echo ERROR: Driver files not found in %DRIVER_DIR%
    echo Please build the driver first.
    pause
    exit /b 1
)

REM Ask user if they want to restart now
echo.
set /p RESTART="Do you want to restart now? (Y/N): "
if /i "%RESTART%"=="Y" (
    echo Restarting in 10 seconds...
    shutdown /r /t 10 /c "Restarting to enable test signing for Distributed Audio driver"
    echo Cancel restart with: shutdown /a
    pause
) else (
    echo.
    echo Please restart Windows manually before continuing.
    pause
)

exit /b 0
