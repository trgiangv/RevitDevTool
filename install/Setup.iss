; ==============================================================================
; RevitDevTool Installer
; ==============================================================================

#define AppName        "RevitDevTool"
#define AppVersion     "1.0.0"
#define AppVersionBase "1.0.0"
#define AppPublisher   "Inspexel"
#define AppURL         "https://github.com/trgiangv/RevitDevTool"
#define AppId          "B2BC2881-A08A-41D8-B1B3-424045E529DB"

; ==============================================================================
; SETUP
; ==============================================================================

[Setup]
AppId={{{#AppId}}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
DefaultDirName={userappdata}\Autodesk\ApplicationPlugins\{#AppName}.bundle
DefaultGroupName={#AppName}
OutputBaseFilename={#AppName}-Setup
Compression=lzma2/ultra64
SolidCompression=yes
PrivilegesRequired=lowest
DirExistsWarning=no
DisableWelcomePage=no
DisableProgramGroupPage=yes
DisableReadyPage=no
DisableFinishedPage=no
DisableDirPage=yes
SetupIconFile=Resources\Icons\ShellIcon.ico
WizardImageFile=Resources\Icons\BackgroundImage.png
WizardSmallImageFile=Resources\Icons\BannerImage.png
WizardStyle=modern windows11
UninstallDisplayIcon={uninstallexe}
UninstallFilesDir={app}\Uninstall
VersionInfoVersion={#AppVersionBase}.0
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName}
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersionBase}.0

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"

; ==============================================================================
; INSTALL TYPE (single iscustom)
;
; One custom type only hides the Full/Compact/Custom combobox on the Select
; Components page; all listed components default to checked (user can uncheck).
; See: https://stackoverflow.com/q/28731933
; ==============================================================================

[Types]
Name: "custom"; Description: "Custom installation"; Flags: iscustom

; ==============================================================================
; COMPONENTS
;
; Parent rows: uncheck to disable the whole product tree.
; Year rows: drive which Contents\YEAR\ folders are installed.
; ==============================================================================

[Components]
Name: "revit";        Description: "Autodesk Revit";   Types: custom
Name: "revit\2022";   Description: "2022";              Types: custom
Name: "revit\2023";   Description: "2023";              Types: custom
Name: "revit\2024";   Description: "2024";              Types: custom
Name: "revit\2025";   Description: "2025";              Types: custom
Name: "revit\2026";   Description: "2026";              Types: custom
Name: "revit\2027";   Description: "2027";              Types: custom

Name: "autocad";      Description: "Autodesk AutoCAD"; Types: custom
Name: "autocad\2022"; Description: "2022";             Types: custom
Name: "autocad\2023"; Description: "2023";             Types: custom
Name: "autocad\2024"; Description: "2024";             Types: custom
Name: "autocad\2025"; Description: "2025";             Types: custom
Name: "autocad\2026"; Description: "2026";             Types: custom
Name: "autocad\2027"; Description: "2027";             Types: custom

; ==============================================================================
; FILES
;
; Each year folder is shared between Revit and AutoCAD — both apps place their
; binaries under the same Contents\YEAR\ directory:
;   RevitDevTool.addin   — loaded by Revit
;   AcadDevTool.dll      — loaded by AutoCAD
;
; A year folder is installed when either app for that year is selected.
; ==============================================================================

[Files]
; Core
Source: "PackageContents.xml";    DestDir: "{app}";          Flags: ignoreversion
Source: "Contents\MCPServer.exe"; DestDir: "{app}\Contents"; Flags: ignoreversion

; Shared year folders
Source: "Contents\2022\*"; DestDir: "{app}\Contents\2022"; Flags: ignoreversion recursesubdirs; Components: revit\2022 or autocad\2022
Source: "Contents\2023\*"; DestDir: "{app}\Contents\2023"; Flags: ignoreversion recursesubdirs; Components: revit\2023 or autocad\2023
Source: "Contents\2024\*"; DestDir: "{app}\Contents\2024"; Flags: ignoreversion recursesubdirs; Components: revit\2024 or autocad\2024
Source: "Contents\2025\*"; DestDir: "{app}\Contents\2025"; Flags: ignoreversion recursesubdirs; Components: revit\2025 or autocad\2025
Source: "Contents\2026\*"; DestDir: "{app}\Contents\2026"; Flags: ignoreversion recursesubdirs; Components: revit\2026 or autocad\2026
Source: "Contents\2027\*"; DestDir: "{app}\Contents\2027"; Flags: ignoreversion recursesubdirs; Components: revit\2027 or autocad\2027

[Icons]
Name: "{autoprograms}\Uninstall {#AppName}"; Filename: "{uninstallexe}"

; ==============================================================================
; CODE — Pascal split across install\includes\*.inc (order matters)
;   Registry.inc  — APP_COUNT, years, component/XML description helpers
;   Processes.inc — monitored EXEs; line = name - file version - PID: n; extend MONITORED_EXE_COUNT
;   XmlFilter.inc — PackageContents.xml pruning
;   Register.inc  — OnPostInstall, CurStepChanged
;   Hooks.inc     — InitializeSetup / InitializeUninstall
; ==============================================================================

[Code]
#include "includes\Registry.inc"
#include "includes\Processes.inc"
#include "includes\XmlFilter.inc"
#include "includes\Register.inc"
#include "includes\Hooks.inc"
