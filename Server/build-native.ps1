# Compila las apps nativas (Android y/o Windows). Lo usan build-android.ps1 y build-windows.ps1.
# No lo ejecutes directamente salvo que quieras ambas plataformas:
#   powershell -ExecutionPolicy Bypass -File Server\build-native.ps1 -Plataforma Ambas -Servidor 192.168.1.149:5080

param(
    [ValidateSet("Android", "Windows", "Ambas")]
    [string]$Plataforma = "Ambas",
    # Dirección del servidor, fijada en la app al compilar (el jugador no la ve ni la cambia).
    [string]$Servidor = "192.168.1.149:5080",
    [string]$UnityPath = ""
)

$ErrorActionPreference = "Stop"

function Fallar([string]$mensaje) {
    Write-Host ""
    Write-Host "ERROR: $mensaje" -ForegroundColor Red
    exit 1
}

$proyecto = Split-Path -Parent $PSScriptRoot

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

function Compilar([string]$nombre, [string]$metodo, [string]$salida) {
    $registro = Join-Path $env:TEMP "robotbombero-build-$nombre.log"
    Write-Host "Compilando para $nombre (puede tardar varios minutos)..."
    Write-Host "Registro de Unity: $registro"
    $argumentos = @(
        '-batchmode', '-quit',
        '-projectPath', "`"$proyecto`"",
        '-executeMethod', $metodo,
        '-logFile', "`"$registro`""
    )
    # Sin -buildTarget: la compilación cambia de plataforma y al terminar deja activa la de antes.
    if ($Servidor) { $argumentos += @('-rbServer', "`"$Servidor`"") }
    $unity = Start-Process -FilePath $UnityPath -PassThru -ArgumentList $argumentos
    $unity.WaitForExit()
    if ($unity.ExitCode -ne 0) {
        Fallar "La compilación para $nombre ha fallado (código $($unity.ExitCode)). Mira las últimas líneas de $registro"
    }
    if (-not (Test-Path (Join-Path $proyecto $salida))) {
        Fallar "Unity ha terminado pero no existe $salida. Mira $registro"
    }
    Write-Host "Listo: $(Join-Path $proyecto $salida)" -ForegroundColor Green
}

if ($Plataforma -in @("Android", "Ambas")) { Compilar "Android" "BuildAndroid.Build" "Build\Android\RobotBombero.apk" }
if ($Plataforma -in @("Windows", "Ambas")) { Compilar "Windows" "BuildWindows.Build" "Build\Windows\RobotBombero.exe" }
exit 0
