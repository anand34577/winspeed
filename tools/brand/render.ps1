# Renders the social preview (1280x640) and thumbnail (1280x720) into docs/images using Microsoft Edge headless.
$edge = @("${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe", "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe") |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $edge) { throw "Microsoft Edge not found" }
$images = (Resolve-Path (Join-Path $PSScriptRoot "..\..\docs\images")).Path

function Render([string]$html, [string]$out, [int]$w, [int]$h) {
    $src = "file:///" + ((Resolve-Path (Join-Path $PSScriptRoot $html)).Path -replace '\\', '/')
    $dst = Join-Path $images $out
    if (Test-Path $dst) { Remove-Item $dst }
    # Edge reports progress on stderr; keep it from being treated as an error
    & $edge --headless=new --disable-gpu --hide-scrollbars --force-device-scale-factor=1 "--user-data-dir=$env:TEMP\winspeed-brand-$w-$h" `
        "--window-size=$w,$h" "--screenshot=$dst" $src 2>&1 | Out-Null
    if (-not (Test-Path $dst)) { throw "Failed to render $out" }
    Write-Host "rendered $out"
}

Render "social-preview.html" "social-preview.png" 1280 640
Render "thumbnail.html" "thumbnail.png" 1280 720
