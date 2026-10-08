# Prepara el instalador de FleetManager:
#   1. Publica FleetManager y el generador del mapa en publicar\app, con .NET incluido
#      (el otro ordenador no necesita tener .NET instalado).
#   2. Crea publicar\FleetManager-Instalador-<versión>.exe con Inno Setup.
# Uso: powershell -ExecutionPolicy Bypass -File herramientas\publicar.ps1
$ErrorActionPreference = 'Stop'

$raiz = Split-Path $PSScriptRoot
$app = Join-Path $raiz 'publicar\app'

# La versión sale de FleetManager.csproj (<Version>).
$version = ([xml](Get-Content (Join-Path $raiz 'FleetManager\FleetManager.csproj'))).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
"Versión: $version"

if (Test-Path $app) { Remove-Item -Recurse -Force $app }

# Los dos programas en la misma carpeta; comparten los archivos de .NET.
foreach ($proyecto in 'FleetManager\FleetManager.csproj', 'GeneradorMapa\FleetManager.GeneradorMapa.csproj') {
    "Publicando $proyecto..."
    dotnet publish (Join-Path $raiz $proyecto) -c Release -r win-x64 --self-contained -p:DebugType=None -o $app --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "Falló la publicación de $proyecto" }
}

$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) { throw 'No se encuentra Inno Setup 6 (instálalo con: winget install JRSoftware.InnoSetup --scope user).' }

"Creando el instalador..."
& $iscc /Q "/DVersion=$version" "/DOrigen=$app" (Join-Path $raiz 'instalador\FleetManager.iss')
if ($LASTEXITCODE -ne 0) { throw 'Falló Inno Setup' }

$instalador = Join-Path $raiz "publicar\FleetManager-Instalador-$version.exe"
"Hecho: $instalador ({0:N0} MB)" -f ((Get-Item $instalador).Length / 1MB)
