# Build FerryClip for Windows, then compile its Inno Setup installer.
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = Join-Path $root 'windows\ClipSync.Windows\ClipSync.Windows.csproj'
$buildScript = Join-Path $PSScriptRoot 'build-windows.ps1'
$installerScript = Join-Path $root 'windows\installer\FerryClip.iss'
$version = ([xml](Get-Content -LiteralPath $project -Raw)).Project.PropertyGroup.Version | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($version)) { throw 'Windows project version is missing.' }
& $buildScript
if ($LASTEXITCODE -ne 0) { throw "Windows app build failed with exit code $LASTEXITCODE" }
$exe = Join-Path $root 'dist\FerryClip-win-x64.exe'
$outDir = Join-Path $root 'dist'
$isccPath = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source
if (-not $isccPath) {
  foreach ($candidate in @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe", "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe")) {
    if (Test-Path -LiteralPath $candidate) { $isccPath = $candidate; break }
  }
}
if (-not $isccPath) { throw 'Inno Setup compiler ISCC.exe was not found. Install Inno Setup 6, then rerun this script.' }
& $isccPath "/DAppVersion=$version" "/DSourceExe=$exe" "/DOutputDir=$outDir" $installerScript
if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed with exit code $LASTEXITCODE" }
Write-Host "Windows release installer: $(Join-Path $outDir 'FerryClip-Windows-Setup.exe')" -ForegroundColor Green
