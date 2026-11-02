# Convierte los SVG de Assets/Game/Art a PNG con Edge sin ventana.
# Uso: powershell -File Tools/arte/Renderizar.ps1 [-Solo fondo-vertical,robot]
param([string[]]$Solo)

$ErrorActionPreference = 'Stop'
# Con -File, una lista separada por comas llega como un único texto.
if ($Solo) { $Solo = $Solo -split ',' }

$edge = 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'
$arte = Join-Path $PSScriptRoot '..\..\Assets\Game\Art' | Resolve-Path
$perfil = Join-Path $env:TEMP 'rb-arte-edge'

$trabajos = @(
    @{ Nombre = 'fondo-vertical';   W = 1080; H = 1920; Transparente = $false },
    @{ Nombre = 'fondo-horizontal'; W = 1920; H = 1080; Transparente = $false },
    @{ Nombre = 'robot';            W = 512;  H = 512;  Transparente = $true },
    @{ Nombre = 'fuego';            W = 512;  H = 512;  Transparente = $true }
)

Add-Type -AssemblyName System.Drawing
foreach ($t in $trabajos) {
    if ($Solo -and ($Solo -notcontains $t.Nombre)) { continue }
    $svg = Join-Path $arte ($t.Nombre + '.svg')
    $png = Join-Path $arte ($t.Nombre + '.png')

    # Una página mínima: el SVG suelto no rellena la ventana igual en todas las versiones de Edge.
    $html = Join-Path $env:TEMP ("rb-arte-" + $t.Nombre + ".html")
    $cuerpo = [IO.File]::ReadAllText($svg)
    $fondo = if ($t.Transparente) { 'transparent' } else { '#090C16' }
    [IO.File]::WriteAllText($html, "<!doctype html><meta charset=utf-8><style>html,body{margin:0;padding:0;background:$fondo;overflow:hidden}svg{display:block}</style>$cuerpo")

    $argumentos = @('--headless', '--disable-gpu', '--hide-scrollbars', "--user-data-dir=`"$perfil`"",
        "--window-size=$($t.W),$($t.H)", "--screenshot=`"$png`"")
    if ($t.Transparente) { $argumentos += '--default-background-color=00000000' }
    $argumentos += "`"file:///$($html -replace '\\','/')`""

    $p = Start-Process -FilePath $edge -ArgumentList $argumentos -PassThru -WindowStyle Hidden
    if (-not $p.WaitForExit(120000)) { $p.Kill(); throw "Edge no terminó con $($t.Nombre)" }

    $img = [System.Drawing.Image]::FromFile($png)
    Write-Output ("{0}: {1}x{2}, {3:N0} KB" -f $t.Nombre, $img.Width, $img.Height, ((Get-Item $png).Length / 1KB))
    $img.Dispose()
    Remove-Item $html
}
