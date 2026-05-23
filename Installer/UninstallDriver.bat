@echo off
REM Distributed Audio System - Uninstall Driver

echo ========================================
echo Distributed Audio System
echo Driver Uninstallation
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

REM Stop the audio service first
echo Stopping audio capture service...
sc stop DistributedAudioService 2>nul
sc delete DistributedAudioService 2>nul

REM Find and uninstall the driver
echo Uninstalling driver...

REM Get list of installed drivers
pnputil /enum-drivers > %TEMP%\drivers.txt

REM Search for our driver
findstr /C:"distributedaudio.inf" %TEMP%\drivers.txt >nul
if %errorLevel% neq 0 (
    echo Driver not found in system. It may already be uninstalled.
    goto cleanup
)

REM Extract the published name (original name)
for /f "tokens=2 delims=:" %%a in ('findstr /C:"distributedaudio.inf" %TEMP%\drivers.txt') do (
    set INF_LINE=%%a
)

REM Parse the published name from enum output
for /f "tokens=*" %%a in ('pnputil /enum-drivers ^| findstr /C:"distributedaudio.inf"') do (
    set LINE=%%a
    goto found
)

:found
REM The next line after finding the INF should contain the published name
for /f "tokens=2 delims=:" %%a in ('pnputil /enum-drivers ^| findstr /C:"Published Name" ^| findstr /V "oem"') do (
    set PUBLISHED_NAME=%%a
    set PUBLISHED_NAME=!PUBLISHED_NAME: =!
)

REM Alternative approach: list all oem*.inf files and find ours
for /f %%i in ('pnputil /enum-drivers ^| findstr /C:"distributedaudio"') do (
    echo Found driver entry: %%i
)

REM Try to uninstall using pnputil with wildcard
echo Attempting to uninstall driver...
pnputil /uninstall distributedaudio.inf /uninstall
if %errorLevel% equ 0 (
    goto success
)

REM If that failed, try to find the oem number
for /f "tokens=1" %%a in ('pnputil /enum-drivers ^| findstr /C:"distributedaudio.inf"') do (
    set OEM_NUMBER=%%a
    goto try_oem
)

:try_oem
if defined OEM_NUMBER (
    echo Uninstalling driver package: !OEM_NUMBER!
    pnputil /delete-driver !OEM_NUMBER! /uninstall
    if %errorLevel% equ 0 goto success
)

echo.
echo WARNING: Automatic uninstall had issues.
echo You may need to manually remove the driver from Device Manager.
echo.
goto end

:success
echo.
echo ========================================
echo Driver uninstalled successfully!
echo ========================================
echo.

REM Ask if user wants to disable test signing
echo.
set /p DISABLE_TEST="Disable test signing mode? (Y/N): "
if /i "%DISABLE_TEST%"=="Y" (
    echo Disabling test signing mode...
    bcdedit /set testsigning off
    echo.
    echo You should restart Windows for changes to take effect.
)

goto end

:cleanup
del %TEMP%\drivers.txt 2>nul

:end
pause
