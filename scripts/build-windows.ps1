# Build a self-contained Windows FerryClip executable for offline use.
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = Join-Path $root 'windows\ClipSync.Windows\ClipSync.Windows.csproj'
$dist = Join-Path $root 'dist'
if (-not (Test-Path -LiteralPath $project)) { throw "Project not found at $project" }
if (-not (Test-Path -LiteralPath $dist)) { New-Item -ItemType Directory -Path $dist | Out-Null }
$version = ([xml](Get-Content -LiteralPath $project -Raw)).Project.PropertyGroup.Version | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($version)) { throw 'Windows project version is missing.' }
$publish = Join-Path $dist ("windows-singlefile-$version")
$target = Join-Path $dist 'FerryClip-win-x64.exe'

Write-Host "Publishing self-contained FerryClip $version (win-x64) ..." -ForegroundColor Cyan
$publishArgs = @('publish', $project, '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true', '-p:EnableCompressionInSingleFile=true', '-p:DebugType=None', '-p:DebugSymbols=false', '-o', $publish)
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "Windows publish failed with exit code $LASTEXITCODE" }

$source = Join-Path $publish 'FerryClip.exe'
if (-not (Test-Path -LiteralPath $source)) { throw "Published executable not found at $source" }
Copy-Item -LiteralPath $source -Destination $target -Force
$bytes = [IO.File]::ReadAllBytes($target)
if ($bytes.Length -lt 2 -or $bytes[0] -ne 0x4d -or $bytes[1] -ne 0x5a) { throw 'Generated FerryClip file is not a Windows executable.' }
$info = [Diagnostics.FileVersionInfo]::GetVersionInfo($target)
if (-not $info.ProductVersion.StartsWith($version)) { throw 'Executable version does not match the Windows project.' }
$hash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host "Self-contained FerryClip executable: $target" -ForegroundColor Green
Write-Host "Version: $version | Size: $([math]::Round($bytes.Length / 1MB, 2)) MB | SHA-256: $hash" -ForegroundColor Gray