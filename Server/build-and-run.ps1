# Compila el juego para navegador en Server/RobotBombero.Server/wwwroot y arranca el servidor.
# Uso, desde cualquier carpeta:
#   powershell -ExecutionPolicy Bypass -File Server\build-and-run.ps1
#   powershell -ExecutionPolicy Bypass -File Server\build-and-run.ps1 -SoloCompilar
#   powershell -ExecutionPolicy Bypass -File Server\build-and-run.ps1 -UnityPath "D:\Unity\Editor\Unity.exe"

param(
    [string]$UnityPath = "",
    [switch]$SoloCompilar
)

$ErrorActionPreference = "Stop"

function Fallar([string]$mensaje) {
    Write-Host ""
    Write-Host "ERROR: $mensaje" -ForegroundColor Red
    exit 1
}

$proyecto = Split-Path -Parent $PSScriptRoot
$wwwroot = Join-Path $proyecto "Server\RobotBombero.Server\wwwroot"

# Unity no admite dos instancias sobre el mismo proyecto: mientras está abierto, tiene bloqueado este archivo.
$candado = Join-Path $proyecto "Temp\UnityLockfile"
if (Test-Path $candado) {
    try {
        [System.IO.File]::Open($candado, 'Open', 'ReadWrite', 'None').Close()
    }
    catch {
        Fallar "Unity tiene este proyecto abierto. Guarda tus cambios, cierra el editor y vuelve a ejecutar este script."
    }
}

if (-not $UnityPath) {
    $version = (Select-String -Path (Join-Path $proyecto "ProjectSettings\ProjectVersion.txt") -Pattern '^m_EditorVersion:\s*(\S+)').Matches[0].Groups[1].Value
    $UnityPath = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
}
if (-not (Test-Path $UnityPath)) {
    Fallar "No se encuentra Unity en '$UnityPath'. Indica la ruta con -UnityPath."
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Fallar "No se encuentra dotnet. Instala el SDK de .NET 10."
}

$registro = Join-Path $env:TEMP "robotbombero-build.log"
Write-Host "Compilando para navegador (puede tardar varios minutos)..."
Write-Host "Registro de Unity: $registro"
$unity = Start-Process -FilePath $UnityPath -PassThru -ArgumentList @(
    '-batchmode', '-quit',
    '-projectPath', "`"$proyecto`"",
    '-buildTarget', 'WebGL',
    '-executeMethod', 'BuildWebGL.Build',
    '-logFile', "`"$registro`""
)
$unity.WaitForExit()
if ($unity.ExitCode -ne 0) {
    Fallar "La compilación ha fallado (código $($unity.ExitCode)). Mira las últimas líneas de $registro"
}
if (-not (Test-Path (Join-Path $wwwroot "index.html"))) {
    Fallar "Unity ha terminado pero no hay index.html en $wwwroot. Mira $registro"
}
Write-Host "Compilación lista en $wwwroot" -ForegroundColor Green

if ($SoloCompilar) { exit 0 }

Write-Host "Arrancando el servidor. Para pararlo pulsa Ctrl+C."
dotnet run --project (Join-Path $proyecto "Server\RobotBombero.Server")
exit $LASTEXITCODE
