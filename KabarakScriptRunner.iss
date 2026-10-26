; ============================================================================
;  Kabarak Script Runner — Inno Setup Script
;  Produces: Installer\KabarakScriptRunnerSetup.exe
;
;  This installer:
;    1. Ensures .NET Desktop Runtime 10 is installed (via InnoDependencyInstaller)
;    2. Copies the published app folder
;    3. Writes the correct appsettings.json for the SQL Server
;    4. Creates shortcuts and registers an uninstaller
;
;  CURRENT CONFIG: localhost (for local testing A===========================================================

#define MyAppName "Script Runner"
#define MyAppVersion "3.0.0"
#define MyAppPublisher "E.M Solutions"
#define MyAppExeName "KabarakScriptRunnerApp.exe"
#define MyAppSource "KabarakScriptRunnerApp\bin\Release\net10.0-windows\win-x64\publish"

; ---------------------------------------------------------------------------
;  CONNECTION SETTINGS
;  ---------------------------------------------------------------------------
;  For LOCAL TESTING (localhost):
;      ServerAddress  = "localhost"
;      UseWindowsAuth = "True"     (uses Integrated Security)
;
;  For CENTRAL SERVER (SQL Auth):
;      ServerAddress  = "192.168.1.42,1433"
;      UseWindowsAuth = "False"
;      ServerLogin    = "kabarak_app"
;      ServerPassword = "YourStrongPassword"
; ---------------------------------------------------------------------------

#define ServerAddress     "localhost"
#define ServerDatabase    "KabarakScriptRunner"
#define UseWindowsAuth    "True"
#define ServerLogin       "kabarak_app"
#define ServerPassword    "Kab@rak#2026!App"

; ---------------------------------------------------------------------------
;  Include the InnoDependencyInstaller library
;  (CodeDependencies.iss must be in the same folder as this .iss file.)
; ---------------------------------------------------------------------------
#include "CodeDependencies.iss"

[Setup]
AppId={{5A1B2C3D-4E5F-6789-ABCD-EF0123456789}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=Installer
OutputBaseFilename=KabarakScriptRunnerSetup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=KabarakScriptRunnerApp\Resources\app.ico

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

; Note: The [Tasks] section has been removed because we are handling 
; the desktop shortcut with a custom checkbox on the configuration page.

[Files]
; Copy the whole publish folder (contains the .exe + all DLLs)
Source: "{#MyAppSource}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Start Menu icons only. The Desktop icon is created dynamically in [Code].
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
var
  ConfigPage: TWizardPage;
  ChkDesktopIcon: TNewCheckBox;
  ChkDotNet: TNewCheckBox;
  IsDotNetMissing: Boolean;

// ============================================================================
//  .NET DETECTION LOGIC
//  Checks the registry for the .NET Desktop Runtime 10.x
// ============================================================================
function IsDotNetDesktopRuntimeInstalled(): Boolean;
var
  Names: TArrayOfString;
  I: Integer;
  RegPath: String;
begin
  Result := False;
  // Standard registry path for .NET Desktop Runtime versions
  RegPath := 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App';
  
  if RegGetValueNames(HKLM32, RegPath, Names) then
  begin
    for I := 0 to GetArrayLength(Names) - 1 do
    begin
      // Check if a version starting with "10." is installed
      if Pos('10.', Names[I]) = 1 then
      begin
        Result := True;
        Break;
      end;
    end;
  end;
end;

// ============================================================================
//  CUSTOM PAGE CREATION
// ============================================================================
procedure InitializeWizard();
begin
  // Create a custom page right before the "Ready to Install" page
  ConfigPage := CreateCustomPage(wpSelectDir, 'Configuration', 'Choose additional options before installing.');
  
  // Check if .NET is missing
  IsDotNetMissing := not IsDotNetDesktopRuntimeInstalled();

  // 1. Desktop Shortcut Checkbox
  ChkDesktopIcon := TNewCheckBox.Create(ConfigPage);
  ChkDesktopIcon.Parent := ConfigPage.Surface;
  ChkDesktopIcon.Left := 0;
  ChkDesktopIcon.Top := 0;
  ChkDesktopIcon.Width := ConfigPage.SurfaceWidth;
  ChkDesktopIcon.Caption := 'Create a desktop shortcut';
  ChkDesktopIcon.Checked := True; // Checked by default

  // 2. .NET Runtime Checkbox
  ChkDotNet := TNewCheckBox.Create(ConfigPage);
  ChkDotNet.Parent := ConfigPage.Surface;
  ChkDotNet.Left := 0;
  ChkDotNet.Top := ChkDesktopIcon.Top + ChkDesktopIcon.Height + 10;
  ChkDotNet.Width := ConfigPage.SurfaceWidth;

  if IsDotNetMissing then
  begin
    ChkDotNet.Caption := '.NET Desktop Runtime 10 (Required - Not Detected)';
    ChkDotNet.Checked := True; // Pre-check if missing
    ChkDotNet.Enabled := True;
    
    // Register the dependency. It will download during the Install phase.
    Dependency_AddDotNet100Desktop;
  end
  else
  begin
    ChkDotNet.Caption := '.NET Desktop Runtime 10 (Already Detected)';
    ChkDotNet.Checked := False;
    ChkDotNet.Enabled := False; // Disable if already installed
  end;
end;

// ============================================================================
//  PAGE VALIDATION
//  Ensures the user doesn't skip the .NET install if it's required.
// ============================================================================
function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  
  if CurPageID = ConfigPage.ID then
  begin
    // If .NET is missing but the user unchecked the box, stop them.
    if IsDotNetMissing and (not ChkDotNet.Checked) then
    begin
      MsgBox('The .NET Desktop Runtime is required to run this application. Please check the box to continue.', mbError, MB_OK);
      Result := False;
      Exit;
    end;
  end;
end;

// ============================================================================
//  WRITE APPSETTINGS.JSON
//  Overwrites the connection string with the values from this .iss file.
// ============================================================================
procedure WriteAppSettings();
var
  SettingsFile: String;
  Json: String;
  ConnectionString: String;
begin
  SettingsFile := ExpandConstant('{app}\appsettings.json');

  ConnectionString := 'Server={#ServerAddress};Database={#ServerDatabase};';

  if '{#UseWindowsAuth}' = 'True' then
    ConnectionString := ConnectionString + 'Integrated Security=True;'
  else
    ConnectionString := ConnectionString + 'User Id={#ServerLogin};Password={#ServerPassword};';

  ConnectionString := ConnectionString +
    'TrustServerCertificate=True;Encrypt=True;Application Name=KabarakScriptRunner;';

  Json := '{' + #13#10 +
          '  "ConnectionStrings": {' + #13#10 +
          '    "DefaultConnection": "' + ConnectionString + '"' + #13#10 +
          '  }' + #13#10 +
          '}';

  if not SaveStringToFile(SettingsFile, Json, False) then
    MsgBox('Warning: could not write appsettings.json. You may need to configure the connection string manually.',
           mbError, MB_OK);
end;

// ============================================================================
//  POST-INSTALL STEPS
// ============================================================================
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    WriteAppSettings();
    
    // Create the desktop shortcut if the user checked the box
    if ChkDesktopIcon.Checked then
    begin
      CreateShellLink(ExpandConstant('{autodesktop}\{#MyAppName}.lnk'), '', ExpandConstant('{app}\{#MyAppExeName}'), '', ExpandConstant('{app}'), '', 0, SW_SHOWNORMAL);
    end;
  end;
end;