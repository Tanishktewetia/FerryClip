param(
    [switch]$ExeOnly,
    [switch]$PortableOnly
)

# Build a self-contained Windows FerryClip executable and/or portable archive.
$ErrorActionPreference = 'Stop'
if ($ExeOnly -and $PortableOnly) { throw 'Choose at most one of -ExeOnly or -PortableOnly.' }
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = Join-Path $root 'windows\ClipSync.Windows\ClipSync.Windows.csproj'
$dist = Join-Path $root 'dist'
if (-not (Test-Path -LiteralPath $project)) { throw "Project not found at $project" }
if (-not (Test-Path -LiteralPath $dist)) { New-Item -ItemType Directory -Path $dist | Out-Null }
$version = ([xml](Get-Content -LiteralPath $project -Raw)).Project.PropertyGroup.Version | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($version)) { throw 'Windows project version is missing.' }
$publish = Join-Path $root ("windows\ClipSync.Windows\obj\ferryclip-singlefile-$version")
$target = Join-Path $dist 'FerryClip-win-x64.exe'
$license = Join-Path $root 'LICENSE'
$notices = Join-Path $root 'THIRD_PARTY_NOTICES.md'

if (-not $PortableOnly) {
    Write-Host "Publishing self-contained FerryClip $version (win-x64) ..." -ForegroundColor Cyan
    $publishArgs = @('publish', $project, '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true', '-p:EnableCompressionInSingleFile=true', '-p:DebugType=None', '-p:DebugSymbols=false', '-o', $publish)
    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) { throw "Windows publish failed with exit code $LASTEXITCODE" }
    $source = Join-Path $publish 'FerryClip.exe'
    if (-not (Test-Path -LiteralPath $source)) { throw "Published executable not found at $source" }
    Copy-Item -LiteralPath $source -Destination $target -Force
} elseif (-not (Test-Path -LiteralPath $target)) {
    throw "Portable packaging needs an existing executable at $target."
}

foreach ($required in @($license, $notices)) { if (-not (Test-Path -LiteralPath $required)) { throw "Required release file not found: $required" } }
Copy-Item -LiteralPath $license -Destination (Join-Path $dist 'LICENSE') -Force
Copy-Item -LiteralPath $notices -Destination (Join-Path $dist 'THIRD_PARTY_NOTICES.md') -Force
$bytes = [IO.File]::ReadAllBytes($target)
if ($bytes.Length -lt 2 -or $bytes[0] -ne 0x4d -or $bytes[1] -ne 0x5a) { throw 'Generated FerryClip file is not a Windows executable.' }
$info = [Diagnostics.FileVersionInfo]::GetVersionInfo($target)
if ($info.ProductName -ne 'FerryClip') { throw "Executable product metadata is incorrect: $($info.ProductName)" }
if ($info.FileVersion -notlike "$version*") { throw "Executable file version does not match project version $version (got $($info.FileVersion))." }
if ($info.ProductVersion -notlike "$version*") { throw "Executable product version does not match project version $version (got $($info.ProductVersion))." }

if (-not $ExeOnly) {
    $portableStage = Join-Path $root ("windows\ClipSync.Windows\obj\ferryclip-portable-$version")
    if (-not (Test-Path -LiteralPath $portableStage)) { New-Item -ItemType Directory -Path $portableStage | Out-Null }
    Copy-Item -LiteralPath $target -Destination (Join-Path $portableStage 'FerryClip.exe') -Force
    Copy-Item -LiteralPath $license -Destination (Join-Path $portableStage 'LICENSE') -Force
    Copy-Item -LiteralPath $notices -Destination (Join-Path $portableStage 'THIRD_PARTY_NOTICES.md') -Force
    $portableZip = Join-Path $dist 'FerryClip-Windows-Portable.zip'
    Compress-Archive -Path (Join-Path $portableStage '*') -DestinationPath $portableZip -Force
    Write-Host "Portable archive: $portableZip" -ForegroundColor Green
}

$hash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host "Self-contained FerryClip executable: $target" -ForegroundColor Green
Write-Host "Version: $version | Size: $([math]::Round($bytes.Length / 1MB, 2)) MB | SHA-256: $hash" -ForegroundColor Gray
