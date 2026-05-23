; Distributed Audio System - Inno Setup Installer
; This creates a standalone installer without requiring WiX

[Setup]
AppName=Distributed Audio System
AppVersion=1.0.0
AppPublisher=Kezry
AppPublisherURL=https://github.com/Kezry
AppSupportURL=https://github.com/Kezry/distributedAudio/issues
AppUpdatesURL=https://github.com/Kezry/distributedAudio/releases
DefaultDirName={pf}\DistributedAudio
DefaultGroupName=Distributed Audio System
AllowNoIcons=yes
OutputDir=installer-output
OutputBaseFilename=DistributedAudio-Setup
Compression=lzma2
SolidCompression=yes
; Require admin privileges
PrivilegesRequired=admin
; Uncomment for digital signing
; SignTool=signtool
; SignedUninstaller=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Messages]
english.WelcomeLabel1=Welcome to the Distributed Audio System Setup Wizard
english.WelcomeLabel2=This will install Distributed Audio System on your computer.%n%nDistributed Audio System allows you to stream audio from your PC to multiple Android devices over WiFi.
chinesesimplified.WelcomeLabel1=欢迎使用分布式音频系统安装向导
chinesesimplified.WelcomeLabel2=这将在您的计算机上安装分布式音频系统。%n%n分布式音频系统允许您通过WiFi将电脑音频流式传输到多个安卓设备。

[Tasks]
Name: "desktopicon"; Description: "Create a desktop icon"; GroupDescription: "Additional icons:"; Flags: unchecked
Name: "quicklaunchicon"; Description: "Create a quick launch icon"; GroupDescription: "Additional icons:"; Flags: unchecked; OnlyBelowVersion: 6.1
Name: "driver"; Description: "Install virtual audio driver (requires restart)"; GroupDescription: "Components:"; Flags: unchecked
Name: "service"; Description: "Install audio capture service"; GroupDescription: "Components:"; Flags: unchecked

[Files]
; Main application
Source: "WindowsSound\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "WindowsSound\bin\Release\net8.0-windows\win-x64\publish\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "WindowsSound\bin\Release\net8.0-windows\win-x64\publish\*.exe"; DestDir: "{app}"; Flags: ignoreversion

; Virtual audio driver (optional)
Source: "VirtualAudioDriver\build\distributedaudio.sys"; DestDir: "{app}\driver"; Flags: ignoreversion; Tasks: driver
Source: "VirtualAudioDriver\install\distributedaudio.inf"; DestDir: "{app}\driver"; Flags: ignoreversion; Tasks: driver
Source: "VirtualAudioDriver\build\distributedaudio.cat"; DestDir: "{app}\driver"; Flags: ignoreversion; Tasks: driver

; Audio capture service
Source: "VirtualAudioDriver\service\AudioCaptureService.exe"; DestDir: "{app}"; Flags: ignoreversion; Tasks: service

; Documentation
Source: "README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "Protocol\AudioStreamingProtocol.md"; DestDir: "{app}\docs"; Flags: ignoreversion

[Icons]
Name: "{group}\Distributed Audio"; Filename: "{app}\DistributedAudio.exe"
Name: "{group}\Documentation"; Filename: "{app}\docs\AudioStreamingProtocol.md"
Name: "{group}\Uninstall Distributed Audio"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Distributed Audio"; Filename: "{app}\DistributedAudio.exe"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\Distributed Audio"; Filename: "{app}\DistributedAudio.exe"; Tasks: quicklaunchicon

[Run]
; Main application
Filename: "{app}\DistributedAudio.exe"; Description: "Launch Distributed Audio"; Flags: nowait postinstall skipifsilent

; Audio service (if selected)
Filename: "{app}\AudioCaptureService.exe"; Parameters: "-install"; StatusMsg: "Installing audio capture service..."; Tasks: service; Flags: runhidden

; Driver installation script (if selected)
Filename: "{app}\InstallDriver.bat"; Description: "Install virtual audio driver"; StatusMsg: "Preparing driver installation..."; Tasks: driver; Flags: skipifsilent runascurrentuser

[UninstallRun]
; Stop service
Filename: "{app}\AudioCaptureService.exe"; Parameters: "-stop"; RunOnceId: "StopService"; Flags: runhidden
Filename: "{app}\AudioCaptureService.exe"; Parameters: "-uninstall"; RunOnceId: "UninstallService"; Flags: runhidden

[UninstallDelete]
Type: filesandordirs; Name: "{app}\logs"
Type: filesandordirs; Name: "{app}\config"

[Registry]
; File associations
Root: HKCR; Subkey: ".dla"; ValueType: string; ValueData: "DistributedAudio.Config"; Flags: uninsdeletekey
Root: HKCR; Subkey: "DistributedAudio.Config"; ValueType: string; ValueData: "Distributed Audio Configuration"; Flags: uninsdeletekey
Root: HKCR; Subkey: "DistributedAudio.Config\DefaultIcon"; ValueType: string; ValueData: "{app}\DistributedAudio.exe,0"
Root: HKCR; Subkey: "DistributedAudio.Config\shell\open\command"; ValueType: string; ValueData: """{app}\DistributedAudio.exe"" ""%1"""

; AutoRun option
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "DistributedAudio"; ValueData: "{app}\DistributedAudio.exe -minimized"; Flags: uninsdeletevalue

[Code]
// Pascal script for custom installer logic

function IsWindows64: Boolean;
var
  Version: TWindowsVersion;
begin
  GetWindowsVersionEx(Version);
  Result := (Version.ProductType = VER_NT_WORKSTATION) and (Version.Major >= 6);
end;

function IsTestSigningEnabled: Boolean;
var
  ResultCode: Integer;
  OutputText: AnsiString;
begin
  // Check if test signing is enabled
  if ExecAndHideOutput('bcdedit', '/enum {current}', OutputText, ResultCode) then
    Result := Pos('testsigning', LowerCase(OutputText)) > 0
  else
    Result := False;
end;

function NeedTestSigning: Boolean;
begin
  // Check if driver installation is selected and test signing is not enabled
  Result := IsTaskSelected('driver') and not IsTestSigningEnabled;
end;

function InitializeSetup(): Boolean;
var
  Version: TWindowsVersion;
begin
  // Check Windows version
  GetWindowsVersionEx(Version);
  if Version.Major < 6 then
  begin
    MsgBox('This application requires Windows 7 or later.', mbError, MB_OK);
    Result := False;
    Exit;
  end;

  // Check if .NET 8.0 is installed
  if not IsNetCoreInstalled('Microsoft.WindowsDesktop.App', '8.0') then
  begin
    if MsgBox('This application requires .NET 8.0 Desktop Runtime.' + #13#10 +
              'Would you like to download it now?', mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/8.0', '', '', SW_SHOWNORMAL, ewNoWait, 0);
    end;
    Result := False;
    Exit;
  end;

  Result := True;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  // Show warning if driver is selected but test signing is not enabled
  if (CurPageID = wpSelectTasks) and NeedTestSigning then
  begin
    MsgBox('NOTE: Installing the virtual audio driver requires Windows test signing mode.' + #13#10 +
           'The installer will enable test signing and you will need to restart your computer.',
           mbInformation, MB_OK);
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;

  // Before installing files, enable test signing if driver is selected
  if (CurPageID = wpSelectTasks) and IsTaskSelected('driver') and not IsTestSigningEnabled then
  begin
    if MsgBox('The installer will now enable test signing mode.' + #13#10 +
              'You will need to restart your computer before the driver can be installed.' + #13#10 +
              'Continue?', mbConfirmation, MB_YESNO) = IDYES then
    begin
      // Enable test signing
      if ExecAsAdminAndLog('bcdedit', '/set testsigning on', 'Enabling test signing', ResultCode) then
      begin
        MsgBox('Test signing has been enabled.' + #13#10 +
               'Please restart Windows and run this installer again to complete the driver installation.',
               mbInformation, MB_OK);
        // Abort installation - user needs to restart first
        Result := False;
      end
      else
      begin
        MsgBox('Failed to enable test signing mode. Please run this installer as administrator.',
               mbError, MB_OK);
        Result := False;
      end;
    end
    else
    begin
      // Driver installation optional - continue without it
      Result := True;
    end;
  end;
end;

function ExecAndHideOutput(const Filename, Parameters: String; var OutputText: AnsiString; var ResultCode: Integer): Boolean;
var
  StdOutText: AnsiString;
begin
  Result := ExecWithRedirect(Filename, Parameters, '', '', SW_HIDE, ewWaitUntilTerminated, ResultCode, StdOutText, OutputText);
end;

function ExecAsAdminAndLog(const Filename, Parameters, LogName: String; var ResultCode: Integer): Boolean;
var
  OutputText: AnsiString;
begin
  Result := ExecAndHideOutput(Filename, Parameters, OutputText, ResultCode);
  if Result and (ResultCode = 0) then
    LogFmt('%s completed successfully', [LogName])
  else
    LogFmt('%s failed with code %d', [LogName, ResultCode]);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  // Before uninstalling files, remove the driver
  if CurUninstallStep = usUninstall then
  begin
    if FileExists(ExpandConstant('{app}\UninstallDriver.bat')) then
    begin
      Exec(ExpandConstant('{app}\UninstallDriver.bat'), '', '', SW_HIDE, ewWaitUntilTerminated, 0);
    end;
  end;
end;
