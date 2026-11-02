# Compila la app de Windows en Build\Windows\ (fuera de git).
# Uso, con Unity cerrado:
#   powershell -ExecutionPolicy Bypass -File Server\build-windows.ps1 -Servidor 127.0.0.1:5080
param(
    [string]$Servidor = "192.168.1.149:5080",
    [string]$UnityPath = ""
)
& (Join-Path $PSScriptRoot "build-native.ps1") -Plataforma Windows -Servidor $Servidor -UnityPath $UnityPath
exit $LASTEXITCODE
