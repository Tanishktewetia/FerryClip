[CmdletBinding()]
param(
    [switch]$InstallerOnly,
    [string]$SourceExe,
    [string]$Version
)

# Build the app artifacts and/or compile the Inno Setup installer.
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = Join-Path $root 'windows\ClipSync.Windows\ClipSync.Windows.csproj'
$buildScript = Join-Path $PSScriptRoot 'build-windows.ps1'
$installerScript = Join-Path $root 'windows\installer\FerryClip.iss'
$projectVersion = ([xml](Get-Content -LiteralPath $project -Raw)).Project.PropertyGroup.Version | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($projectVersion)) { throw 'Windows project version is missing.' }
if ([string]::IsNullOrWhiteSpace($Version)) { $Version = $projectVersion }
if ($Version -cne $projectVersion) { throw "Installer version '$Version' does not match Windows project version '$projectVersion'." }
$outDir = Join-Path $root 'dist'
if (-not (Test-Path -LiteralPath $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }
if ([string]::IsNullOrWhiteSpace($SourceExe)) { $exe = Join-Path $outDir 'FerryClip-win-x64.exe' }
elseif ([IO.Path]::IsPathRooted($SourceExe)) { $exe = [IO.Path]::GetFullPath($SourceExe) }
else { $exe = [IO.Path]::GetFullPath((Join-Path $root $SourceExe)) }

if (-not $InstallerOnly) {
    & $buildScript
    if ($LASTEXITCODE -ne 0) { throw "Windows app build failed with exit code $LASTEXITCODE" }
    $exe = Join-Path $outDir 'FerryClip-win-x64.exe'
} elseif (-not (Test-Path -LiteralPath $exe)) {
    throw "Installer compilation needs an existing executable at $exe."
}
$info = [Diagnostics.FileVersionInfo]::GetVersionInfo($exe)
if ($info.ProductName -ne 'FerryClip' -or $info.FileVersion -notlike "$Version*" -or $info.ProductVersion -notlike "$Version*") {
    throw "Source executable metadata does not match FerryClip version $Version."
}

$isccPath = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source
if (-not $isccPath) {
    foreach ($candidate in @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe", "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe")) {
        if (Test-Path -LiteralPath $candidate) { $isccPath = $candidate; break }
    }
}
if (-not $isccPath) { throw 'Inno Setup compiler ISCC.exe was not found. Install Inno Setup 6, then rerun this script.' }
& $isccPath "/DAppVersion=$Version" "/DSourceExe=$exe" "/DOutputDir=$outDir" $installerScript
if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed with exit code $LASTEXITCODE" }
Write-Host "Windows release installer: $(Join-Path $outDir 'FerryClip-Windows-Setup.exe')" -ForegroundColor Green
