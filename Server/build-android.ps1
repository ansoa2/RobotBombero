# Compila el APK de Android en Build\Android\RobotBombero.apk (fuera de git).
# Uso, desde cualquier carpeta, con Unity cerrado:
#   powershell -ExecutionPolicy Bypass -File Server\build-android.ps1 -Servidor 192.168.1.149:5080
# -Servidor es la dirección del ordenador con el servidor; queda fijada en la app al compilar
# (el jugador no la ve ni la cambia). Si cambia la dirección, hay que recompilar.
param(
    [string]$Servidor = "192.168.1.149:5080",
    [string]$UnityPath = ""
)
& (Join-Path $PSScriptRoot "build-native.ps1") -Plataforma Android -Servidor $Servidor -UnityPath $UnityPath
exit $LASTEXITCODE
