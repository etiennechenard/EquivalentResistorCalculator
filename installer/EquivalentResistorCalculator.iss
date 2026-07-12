; Inno Setup script for Equivalent Resistor Calculator (Native AOT build).
; Bundles the published native exe + its SDL2/ImGui native DLLs.
;
; Compile with the Inno Setup compiler (ISCC.exe). The repo-root build-release.bat
; publishes the app first, then invokes ISCC with the args below.
;
; The publish (staging) directory is passed in as /DPublishDir=... ; if omitted
; it defaults to the repo's top-level "publish" folder. The app version can be
; overridden with /DAppVersion=1.2.3 ; otherwise it is read from the exe.

#ifndef PublishDir
  #define PublishDir "..\publish"
#endif

#define AppName    "Equivalent Resistor Calculator"
#define AppExe     "EquivalentResistorCalculator.exe"
#define AppPublish "Etienne Chenard"

; Version: use /DAppVersion=... if supplied, otherwise read it from the
; published exe's embedded Win32 version resource (set by <Version> in the csproj).
#ifndef AppVersion
  #define AppVersion GetVersionNumbersString(AddBackslash(PublishDir) + AppExe)
#endif

[Setup]
; AppId uniquely identifies this app for upgrades/uninstall — do not change it.
AppId={{62E94DDB-59E8-437E-A36F-8CAFBE6819F1}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublish}
DefaultDirName={autopf}\EquivalentResistorCalculator
DefaultGroupName={#AppName}
UninstallDisplayIcon={app}\{#AppExe}
OutputDir=output
OutputBaseFilename=EquivalentResistorCalculator-Installer-{#AppVersion}
SetupIconFile=..\src\EquivalentResistorCalculator.Gui\EquivalentResistorCalculator.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; Native AOT build is x64-only.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Per-machine install into Program Files (prompts for elevation).
PrivilegesRequired=admin

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Main native executable.
Source: "{#PublishDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
; Native runtime DLLs that ship alongside the AOT exe.
Source: "{#PublishDir}\SDL2.dll";          DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\cimgui.dll";        DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\ImGuiImpl.dll";     DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\ImGuiImplSDL2.dll"; DestDir: "{app}"; Flags: ignoreversion
; NOTE: *.pdb debug symbols are intentionally NOT shipped.

[Icons]
Name: "{group}\{#AppName}";        Filename: "{app}\{#AppExe}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";  Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

; No [UninstallDelete] entries for %APPDATA%\EquivalentResistorCalculator\ — user data
; (settings.json, stocks\, imgui.ini) must survive uninstall/reinstall (TDD §7.3).
