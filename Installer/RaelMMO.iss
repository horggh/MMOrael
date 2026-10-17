; Instalador do RaelMMO para Windows (Inno Setup 6).
; Gerado pelo menu RaelMMO > Gerar jogo e instalador para Windows (Assets/Editor/BuildWindows.cs),
; que antes gera o jogo em Builds\Windows.

#define AppName "RaelMMO"
#define AppVersion "0.1.0"
#define AppExe "RaelMMO.exe"

[Setup]
AppId={{6F1C2B7A-4E8D-4B4E-9C2A-52A1E0D3B7F1}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=RaelMMO
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; Instala só para o usuário atual: não pede administrador.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\Builds\Instalador
OutputBaseFilename=RaelMMO-Instalador-{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#AppExe}

[Languages]
Name: "ptbr"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "..\Builds\Windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
