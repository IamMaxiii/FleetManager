; Instalador de FleetManager (Inno Setup 6). No se compila a mano: lo hace
; herramientas\publicar.ps1, que antes publica la app en publicar\app.
;
; Se instala solo para el usuario (sin permiso de administrador) en
; %LOCALAPPDATA%\Programs\FleetManager. Los datos del usuario están aparte, en
; %LOCALAPPDATA%\FleetManager, y ni el instalador ni el desinstalador los tocan.

#ifndef Version
  #define Version "1.0.0"
#endif
#ifndef Origen
  #define Origen "..\publicar\app"
#endif

[Setup]
; Identificador fijo: con él, una versión nueva se instala encima de la anterior.
AppId={{5A24AF27-F3FC-4077-AA6B-369FC9B01473}
AppName=FleetManager
AppVersion={#Version}
AppVerName=FleetManager {#Version}
AppPublisher=IamMaxiii
VersionInfoVersion={#Version}
PrivilegesRequired=lowest
DefaultDirName={autopf}\FleetManager
DisableProgramGroupPage=yes
OutputDir=..\publicar
OutputBaseFilename=FleetManager-Instalador-{#Version}
SetupIconFile=..\FleetManager\Recursos\FleetManager.ico
UninstallDisplayIcon={app}\FleetManager.exe
UninstallDisplayName=FleetManager
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; Idioma del instalador: el de Windows; solo pregunta si no es uno de los 9.
ShowLanguageDialog=auto
; Si FleetManager está abierto, pide cerrarlo antes de instalar o desinstalar.
AppMutex=Local\FleetManager.InstanciaUnica

; Los mismos 9 idiomas que FleetManager. El instalador elige el de Windows (o inglés,
; el primero, si no es ninguno de estos). Los textos son los de Inno Setup.
[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "de"; MessagesFile: "compiler:Languages\German.isl"
Name: "fr"; MessagesFile: "compiler:Languages\French.isl"
Name: "it"; MessagesFile: "compiler:Languages\Italian.isl"
Name: "pt"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "pl"; MessagesFile: "compiler:Languages\Polish.isl"
Name: "nl"; MessagesFile: "compiler:Languages\Dutch.isl"
Name: "tr"; MessagesFile: "compiler:Languages\Turkish.isl"

[Tasks]
Name: "escritorio"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#Origen}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\FleetManager"; Filename: "{app}\FleetManager.exe"
Name: "{autodesktop}\FleetManager"; Filename: "{app}\FleetManager.exe"; Tasks: escritorio

[Run]
Filename: "{app}\FleetManager.exe"; Description: "{cm:LaunchProgram,FleetManager}"; Flags: nowait postinstall skipifsilent
