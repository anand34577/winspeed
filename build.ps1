# Builds WinSpeed and produces:
#   dist\app\WinSpeed.exe                  (the app)
#   dist\WinSpeed-Portable-<ver>.zip       (portable: unzip & run, data kept beside the exe)
#   dist\WinSpeed-Setup-<ver>.exe          (installer, if Inno Setup 6 is installed)
param([string]$Version = "")
if (-not $Version) {
    # default: the version in WinSpeed.csproj
    $Version = ([xml](Get-Content (Join-Path $PSScriptRoot "WinSpeed.csproj"))).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
}
$Version = $Version.TrimStart("v")
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$dist = Join-Path $root "dist"
$app = Join-Path $dist "app"

if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
New-Item -ItemType Directory -Force $app | Out-Null

Write-Host "Building WinSpeed $Version..." -ForegroundColor Cyan
dotnet build "$root\WinSpeed.csproj" -c Release -p:Version=$Version -p:FileVersion=$Version.0 -o "$root\bin\dist-build" --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Build failed" }
Copy-Item "$root\bin\dist-build\WinSpeed.exe" $app
if (Test-Path "$root\bin\dist-build\WinSpeed.exe.config") { Copy-Item "$root\bin\dist-build\WinSpeed.exe.config" $app }

# portable zip
$portable = Join-Path $dist "portable"
New-Item -ItemType Directory -Force $portable | Out-Null
Copy-Item "$app\*" $portable
Set-Content -Path "$portable\portable" -Value "Delete this file to store settings in %APPDATA%\WinSpeed instead of the Data folder."
Copy-Item "$root\LICENSE" $portable -ErrorAction SilentlyContinue
Compress-Archive -Path "$portable\*" -DestinationPath (Join-Path $dist "WinSpeed-Portable-$Version.zip")
Remove-Item $portable -Recurse -Force

# installer
$iscc = @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe", "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe") |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if ($iscc) {
    & $iscc /Q "/DAppVersion=$Version" "$root\installer\WinSpeed.iss"
    if ($LASTEXITCODE -ne 0) { throw "Installer build failed" }
} else {
    Write-Warning "Inno Setup 6 not found - skipping installer (get it from https://jrsoftware.org/isdl.php)."
}

# checksums for the release page
Get-ChildItem $dist -File | ForEach-Object { "{0}  {1}" -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower(), $_.Name } |
    Set-Content (Join-Path $dist "SHA256SUMS.txt") -Encoding ascii

Write-Host "`nOutput:" -ForegroundColor Green
Get-ChildItem $dist -File | ForEach-Object { "{0,-40} {1,10:N0} bytes" -f $_.Name, $_.Length }
