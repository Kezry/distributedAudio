@echo off
REM Distributed Audio System - Build Script
REM Builds all components of the project

setlocal EnableDelayedExpansion

echo ========================================
echo Distributed Audio System
echo Build Script
echo ========================================
echo.

REM Parse command line arguments
set CONFIG=%1
if "%CONFIG%"=="" set CONFIG=Release

set CLEAN=%2
if "%CLEAN%"=="" set CLEAN=false

echo Configuration: %CONFIG%
echo Clean build: %CLEAN%
echo.

REM Check prerequisites
echo Checking prerequisites...

REM Check for .NET SDK
where dotnet >nul 2>&1
if %errorLevel% neq 0 (
    echo ERROR: .NET SDK not found
    echo Please install .NET 8.0 SDK from https://dotnet.microsoft.com/download
    exit /b 1
)

REM Check for MSBuild (for driver)
where msbuild >nul 2>&1
if %errorLevel% neq 0 (
    echo WARNING: MSBuild not found
    echo Driver build will be skipped
    set BUILD_DRIVER=false
) else (
    set BUILD_DRIVER=true
)

REM Check for Java (for Android)
where javac >nul 2>&1
if %errorLevel% neq 0 (
    echo WARNING: Java not found
    echo Android build will be skipped
    set BUILD_ANDROID=false
) else (
    set BUILD_ANDROID=true
)

echo.

REM Build Windows发送端
echo ========================================
echo Building WindowsSound (Windows发送端)
echo ========================================

cd WindowsSound
if "%CLEAN%"=="true" dotnet clean
dotnet restore
dotnet build -c %CONFIG%
if %errorLevel% neq 0 (
    echo ERROR: WindowsSound build failed
    exit /b 1
)

echo Publishing WindowsSound...
dotnet publish -c %CONFIG% -r win-x64 --self-contained -p:PublishSingleFile=false
cd ..

echo.
echo ========================================
echo Building VirtualAudioDriver (虚拟声卡驱动)
echo ========================================

if "%BUILD_DRIVER%"=="true" (
    REM Note: Driver build requires Windows Driver Kit
    REM This is a placeholder for the actual build process
    echo Driver build requires WDK and proper build environment
    echo Skipping driver build in this script
    echo Please use Visual Studio with WDK to build the driver
) else (
    echo Skipping driver build (MSBuild not found)
)

echo.
echo ========================================
echo Building Android Projects
echo ========================================

if "%BUILD_ANDROID%"=="true" (
    echo Building AndroidSoundPlayer...
    cd AndroidSoundPlayer
    if "%CLEAN%"=="true" gradlew clean
    call gradlew assembleRelease
    if %errorLevel% neq 0 (
        echo WARNING: AndroidSoundPlayer build failed
    )
    cd ..

    echo Building AndroidController...
    cd AndroidController
    if "%CLEAN%"=="true" gradlew clean
    call gradlew assembleRelease
    if %errorLevel% neq 0 (
        echo WARNING: AndroidController build failed
    )
    cd ..
) else (
    echo Skipping Android builds (Java not found)
)

echo.
echo ========================================
echo Build Summary
echo ========================================

echo Windows发送端: WindowsSound\bin\%CONFIG%\net8.0-windows\win-x64\publish\
echo Android声音端: AndroidSoundPlayer\app\build\outputs\apk\release\
echo Android配置端: AndroidController\app\build\outputs\apk\release\

if "%BUILD_DRIVER%"=="true" (
    echo 虚拟声卡驱动: VirtualAudioDriver\build\
)

echo.
echo ========================================
echo Creating Release Package
echo ========================================

set PACKAGE_DIR=Release-Package
set VERSION=1.0.0
set PACKAGE_NAME=DistributedAudio-%VERSION%

if exist %PACKAGE_DIR% rd /s /q %PACKAGE_DIR%
mkdir %PACKAGE_DIR%\%PACKAGE_NAME%

REM Copy Windows application
mkdir %PACKAGE_DIR%\%PACKAGE_NAME%\Windows
xcopy /E /I WindowsSound\bin\%CONFIG%\net8.0-windows\win-x64\publish\ %PACKAGE_DIR%\%PACKAGE_NAME%\Windows\

REM Copy Android APKs
if exist AndroidSoundPlayer\app\build\outputs\apk\release\app-release.apk (
    copy AndroidSoundPlayer\app\build\outputs\apk\release\app-release.apk %PACKAGE_DIR%\%PACKAGE_NAME%\AndroidSoundPlayer.apk
)

if exist AndroidController\app\build\outputs\apk\release\app-release.apk (
    copy AndroidController\app\build\outputs\apk\release\app-release.apk %PACKAGE_DIR%\%PACKAGE_NAME%\AndroidController.apk
)

REM Copy documentation
copy README.md %PACKAGE_DIR%\%PACKAGE_NAME%\
copy INSTALL.md %PACKAGE_DIR%\%PACKAGE_NAME%\
copy LICENSE %PACKAGE_DIR%\%PACKAGE_NAME%\

REM Copy installer scripts
mkdir %PACKAGE_DIR%\%PACKAGE_NAME%\Installer
copy Installer\*.bat %PACKAGE_DIR%\%PACKAGE_NAME%\Installer\

REM Create archive
echo Creating ZIP archive...
powershell Compress-Archive -Path %PACKAGE_DIR%\%PACKAGE_NAME% -DestinationPath %PACKAGE_DIR%.zip -Force

echo.
echo ========================================
echo Build Complete!
echo ========================================
echo.
echo Release package created: %PACKAGE_DIR%.zip
echo.
echo To install:
echo 1. Extract the ZIP file
echo 2. Run Installer\InstallDriver.bat (for virtual driver)
echo 3. Run DistributedAudio.exe from Windows folder
echo 4. Install APK files on Android devices
echo.

pause
