param(
    [ValidateSet("win-x64", "linux-x64")]
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [string]$Version = "0.1.0",
    [string]$OfflineModelSource,
    [string]$InnoCompiler,
    [switch]$SkipEngine,
    [switch]$BuildInstaller
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
if ($Version.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0) {
    throw "Version contains characters that are invalid in a file name."
}
$output = Join-Path $projectRoot "artifacts\publish\$Runtime-$Version"
$engineOutput = Join-Path $projectRoot "artifacts\engine-host\$Runtime"
$engineExecutable = if ($Runtime -eq "win-x64") {
    Join-Path $engineOutput "pingyi-engine\pingyi-engine.exe"
} else {
    Join-Path $engineOutput "pingyi-engine\pingyi-engine"
}

if (-not $SkipEngine) {
    if ($Runtime -ne "win-x64") {
        throw "Build the Linux engine on Ubuntu with scripts/build-engine.sh before publishing linux-x64."
    }
    & (Join-Path $PSScriptRoot "build-engine.ps1")
    if ($LASTEXITCODE -ne 0) { throw "Failed to build the local engine." }
}
elseif (-not (Test-Path $engineExecutable)) {
    throw "The local engine is missing. Remove -SkipEngine or build it first."
}

$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot "artifacts"))
$outputFullPath = [IO.Path]::GetFullPath($output)
$artifactsPrefix = $artifactsRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not $outputFullPath.StartsWith($artifactsPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Publish output must stay inside $artifactsRoot"
}
if (Test-Path -LiteralPath $outputFullPath) {
    Remove-Item -LiteralPath $outputFullPath -Recurse -Force
}

dotnet publish (Join-Path $projectRoot "src\PingYi.App\PingYi.App.csproj") `
    -c $Configuration -r $Runtime --self-contained true -o $output `
    -p:Version=$Version `
    -p:PublishSingleFile=false `
    -p:PublishTrimmed=true `
    -p:TrimMode=partial `
    -p:PublishReadyToRun=false `
    -p:DebugType=None `
    -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw "Failed to publish PingYi." }

Get-ChildItem -LiteralPath $output -Recurse -File -Filter "*.pdb" | Remove-Item -Force

if (Test-Path $engineExecutable) {
    $engineTarget = Join-Path $output "engine-host"
    if (Test-Path $engineTarget) { Remove-Item -LiteralPath $engineTarget -Recurse -Force }
    New-Item -ItemType Directory -Path $engineTarget | Out-Null
    Copy-Item (Join-Path (Split-Path -Parent $engineExecutable) "*") $engineTarget -Recurse -Force
}

$offlineModelTarget = Join-Path $output "offline-models"
if ([string]::IsNullOrWhiteSpace($OfflineModelSource)) {
    & (Join-Path $PSScriptRoot "prepare-offline-models.ps1") -Destination $offlineModelTarget
} else {
    & (Join-Path $PSScriptRoot "prepare-offline-models.ps1") `
        -Source $OfflineModelSource `
        -Destination $offlineModelTarget
}
if ($LASTEXITCODE -ne 0) { throw "Failed to prepare offline baseline models." }

$archive = Join-Path $projectRoot "artifacts\PingYi-$Version-$Runtime.zip"
Compress-Archive -Path (Join-Path $output "*") -DestinationPath $archive -Force
$publishBytes = (Get-ChildItem -LiteralPath $output -Recurse -File | Measure-Object Length -Sum).Sum
$archiveBytes = (Get-Item -LiteralPath $archive).Length
Write-Host ("Release archive: {0} ({1:N1} MB; unpacked {2:N1} MB)" -f $archive, ($archiveBytes / 1MB), ($publishBytes / 1MB))

if ($BuildInstaller -and $Runtime -eq "win-x64") {
    $isccCandidates = @(
        $InnoCompiler,
        $env:PINGYI_ISCC,
        (Get-Command ISCC.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1),
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe")
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Leaf) }
    $isccPath = $isccCandidates | Select-Object -First 1
    if (-not $isccPath) {
        throw "ISCC.exe was not found. Set PINGYI_ISCC, install Inno Setup, or omit -BuildInstaller."
    }
    Write-Host "Inno Setup compiler: $isccPath"
    & $isccPath "/DMyAppVersion=$Version" "/DSourceDir=$output" `
        (Join-Path $projectRoot "packaging\windows\PingYi.iss")
    if ($LASTEXITCODE -ne 0) { throw "Failed to build the Windows installer." }
}
