; Star Fire Installer (upgrade-safe)
; Build:
;   iscc /DMyAppVersion=1.0.5 /DPublishDir="D:\path\to\publish" installer\StarFire.iss

#ifndef MyAppVersion
  #define MyAppVersion "1.0.5"
#endif

#ifndef PublishDir
  #error "PublishDir não informado. Use /DPublishDir=..."
#endif

#define MyAppName "Star Fire"
#define MyAppPublisher "Star Fire"
#define MyAppExeName "ExtintorCrm.App.exe"

[Setup]
AppId={{A41246C6-30E5-4D4F-8D32-0A95D03EF261}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Star Fire
DefaultGroupName=Star Fire
DisableProgramGroupPage=yes
OutputDir=..\artifacts\installer
OutputBaseFilename=StarFire-Setup-{#MyAppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=..\ExtintorCrm.App\Assets\AppIcon\startfire.ico
WizardImageFile=..\installer\assets\WizardImage.bmp
WizardSmallImageFile=..\installer\assets\WizardSmallImage.bmp
DisableDirPage=no
InfoBeforeFile=..\installer\UpgradeNotice.txt

[Languages]
Name: "portuguesebrazilian"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na área de trabalho"; GroupDescription: "Atalhos:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\Star Fire"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\Star Fire"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Executar Star Fire"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Remove apenas pasta de instalação.
; Dados do cliente ficam em %LocalAppData%\StarFire\data e NÃO são removidos.
Type: filesandordirs; Name: "{app}"

