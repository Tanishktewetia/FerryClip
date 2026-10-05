# run-windows.ps1 — Build and run the current Windows app, not an old Debug binary.
param([ValidateSet("Debug", "Release")][string]$Configuration = "Release")
$ErrorActionPreference = "Stop"

$projectDir = Join-Path $PSScriptRoot "..\windows\ClipSync.Windows"
$project = Join-Path $projectDir "ClipSync.Windows.csproj"
if (-not (Test-Path -LiteralPath $project)) { throw "Project not found at $project" }

# Never kill a user's running app or overwrite a locked executable.
if ((Get-Process -Name "FerryClip" -ErrorAction SilentlyContinue) -or (Get-Process -Name "ClipSync" -ErrorAction SilentlyContinue)) {
    throw "FerryClip is already running. Choose Quit FerryClip in its tray menu, then run this script again to launch the current $Configuration build."
}
Write-Host "Building and running FerryClip Windows ($Configuration)..." -ForegroundColor Cyan
$artifacts = Join-Path $PSScriptRoot "..\dist\windows-current"
dotnet run --project $project --configuration $Configuration --artifacts-path $artifacts
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
