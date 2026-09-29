# nightreign-launcher build script
# Requires Windows 10/11 (ships .NET Framework 4, includes csc.exe)
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File .\build.ps1
#   powershell -ExecutionPolicy Bypass -File .\build.ps1 -ExtractFrom "D:\...\nightreign.exe"
#   powershell -ExecutionPolicy Bypass -File .\build.ps1 -Icon "D:\...\app.ico"
param(
    [string]$Icon = "",
    [string]$ExtractFrom = "",
    [string]$OutDir = ".\bin",
    [switch]$Test
)

$ErrorActionPreference = "Stop"

$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (!(Test-Path $csc)) {
    Write-Error "csc.exe not found. .NET Framework 4 is required (built into Windows 10/11)."
    exit 1
}

New-Item -ItemType Directory -Force $OutDir | Out-Null

# --- Resolve icon (optional) ---
$iconFile = $Icon
if (!$iconFile -and (Test-Path ".\app.ico")) { $iconFile = ".\app.ico" }
if (!$iconFile -and $ExtractFrom) {
    if (!(Test-Path $ExtractFrom)) {
        Write-Error "Source exe not found: $ExtractFrom"
        exit 1
    }
    Write-Host "Extracting icon from $ExtractFrom ..."
    & $csc -nologo "-out:$env:TEMP\NightreignExtractIcon.exe" ".\tools\ExtractIcon.cs"
    if ($LASTEXITCODE -ne 0) { exit 1 }
    & "$env:TEMP\NightreignExtractIcon.exe" $ExtractFrom ".\app.ico"
    if (Test-Path ".\app.ico") { $iconFile = ".\app.ico" }
}

$iconArgs = @()
if ($iconFile) {
    $iconArgs = @("-win32icon:$iconFile")
    Write-Host "Icon: $iconFile"
} else {
    Write-Warning "No icon provided; default icon will be used. Pass -ExtractFrom <game.exe> to extract the game icon."
}

$common = @(
    "-nologo", "-codepage:65001", "-target:winexe",
    "-r:System.Windows.Forms.dll", "-r:System.Drawing.dll"
) + $iconArgs

Write-Host "Building LaunchNightreign.exe ..."
& $csc @common "-out:$OutDir\LaunchNightreign.exe" ".\src\LauncherCore.cs" ".\src\LauncherMain.cs"
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "Building LaunchNightreignSettings.exe ..."
& $csc @common "-out:$OutDir\LaunchNightreignSettings.exe" ".\src\LauncherCore.cs" ".\src\SettingsMain.cs"
if ($LASTEXITCODE -ne 0) { exit 1 }

if ($Test) {
    Write-Host "Running config tests ..."
    & $csc -nologo -codepage:65001 -target:exe -r:System.Windows.Forms.dll -r:System.Drawing.dll `
        "-out:$OutDir\cfgtest.exe" ".\src\LauncherCore.cs" ".\tests\ConfigTest.cs"
    if ($LASTEXITCODE -ne 0) { exit 1 }
    & "$OutDir\cfgtest.exe"
    if ($LASTEXITCODE -ne 0) { Write-Error "Config tests FAILED"; exit 1 }
    Remove-Item "$OutDir\cfgtest.exe" -Force
}

Write-Host ""
Write-Host "Build OK -> $OutDir"
Write-Host "  LaunchNightreign.exe         : double-click to launch both programs"
Write-Host "  LaunchNightreignSettings.exe : open the settings window"
