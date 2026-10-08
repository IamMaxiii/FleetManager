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
; Si FleetManager está abierto, pide cerrarlo antes de instalar o desinstalar.
AppMutex=Local\FleetManager.InstanciaUnica

[Languages]
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "escritorio"; Description: "Crear un acceso directo en el escritorio"; GroupDescription: "Accesos directos:"

[Files]
Source: "{#Origen}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\FleetManager"; Filename: "{app}\FleetManager.exe"
Name: "{autodesktop}\FleetManager"; Filename: "{app}\FleetManager.exe"; Tasks: escritorio

[Run]
Filename: "{app}\FleetManager.exe"; Description: "Abrir FleetManager"; Flags: nowait postinstall skipifsilent
