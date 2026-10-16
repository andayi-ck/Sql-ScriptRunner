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
;  CURRENT CONFIG: localhost (for local testing on your own PC)
;  WHEN MOVING TO A CENTRAL SERVER, change the three #define lines below.
; ============================================================================

#define MyAppName "Kabarak Script Runner"
#define MyAppVersion "2.0.0"
#define MyAppPublisher "Kabarak University"
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

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce

[Files]
; Copy the whole publish folder (contains the .exe + all DLLs)
Source: "{#MyAppSource}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
// ============================================================================
//  DEPENDENCY CHECK: .NET Desktop Runtime 10
//  Automatically downloads and installs if missing. Skips if already present.
// ============================================================================
function InitializeSetup(): Boolean;
begin
  // Only proceed if the user has permission (installer requires admin).
  // InnoDependencyInstaller handles the download + silent install of .NET.
  Dependency_AddDotNet100Desktop;
  Result := True;
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
  end;
end;