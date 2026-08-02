param(
    [string]$Source,
    [string]$Destination
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot "artifacts"))

if ([string]::IsNullOrWhiteSpace($Source)) {
    $Source = Join-Path $env:LOCALAPPDATA "PingYi\models"
}
if ([string]::IsNullOrWhiteSpace($Destination)) {
    $Destination = Join-Path $artifactsRoot "offline-models"
}

$sourceRoot = [IO.Path]::GetFullPath($Source)
$destinationRoot = [IO.Path]::GetFullPath($Destination)
$artifactsPrefix = $artifactsRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not $destinationRoot.StartsWith($artifactsPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Offline model destination must stay inside $artifactsRoot"
}
if (-not (Test-Path -LiteralPath $sourceRoot -PathType Container)) {
    throw "Offline model source was not found: $sourceRoot"
}

if (Test-Path -LiteralPath $destinationRoot) {
    Remove-Item -LiteralPath $destinationRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $destinationRoot | Out-Null

function Copy-RequiredFile([string]$RelativePath) {
    $sourcePath = Join-Path $sourceRoot $RelativePath
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        throw "Required offline model file was not found: $sourcePath"
    }
    $targetPath = Join-Path $destinationRoot $RelativePath
    New-Item -ItemType Directory -Path (Split-Path -Parent $targetPath) -Force | Out-Null
    Copy-Item -LiteralPath $sourcePath -Destination $targetPath -Force
}

$detectionRelative = "paddle\official_models\PP-OCRv5_mobile_det_onnx\inference.onnx"
$recognitionRelative = "paddle\official_models\PP-OCRv5_mobile_rec_onnx\inference.onnx"
Copy-RequiredFile $detectionRelative
Copy-RequiredFile "paddle\official_models\PP-OCRv5_mobile_det_onnx\inference.yml"
Copy-RequiredFile $recognitionRelative
Copy-RequiredFile "paddle\official_models\PP-OCRv5_mobile_rec_onnx\inference.yml"

$translationHashes = [ordered]@{}
foreach ($pair in @(
    @{ Pattern = "translate-zh_en-*"; Key = "argos-installed-zh-en" },
    @{ Pattern = "translate-en_zh-*"; Key = "argos-installed-en-zh" }
)) {
    $package = Get-ChildItem -LiteralPath (Join-Path $sourceRoot "argos") -Directory -Filter $pair.Pattern |
        Sort-Object Name -Descending |
        Select-Object -First 1
    if ($null -eq $package) {
        throw "Required Argos package was not found: $($pair.Pattern)"
    }

    foreach ($relative in @(
        "metadata.json",
        "sentencepiece.model",
        "model\config.json",
        "model\model.bin",
        "model\shared_vocabulary.json"
    )) {
        Copy-RequiredFile ("argos\{0}\{1}" -f $package.Name, $relative)
    }
    $translationHashes[$pair.Key] = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $package.FullName "model\model.bin")).Hash.ToLowerInvariant()
}

$ocrManifest = [ordered]@{
    schemaVersion = 1
    sha256 = [ordered]@{
        "paddle-detection" = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $sourceRoot $detectionRelative)).Hash.ToLowerInvariant()
        "paddle-recognition" = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $sourceRoot $recognitionRelative)).Hash.ToLowerInvariant()
    }
}
$translationManifest = [ordered]@{
    schemaVersion = 1
    sha256 = $translationHashes
}
$utf8NoBom = [Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText(
    (Join-Path $destinationRoot "ocr-models.json"),
    ($ocrManifest | ConvertTo-Json -Depth 4),
    $utf8NoBom)
[IO.File]::WriteAllText(
    (Join-Path $destinationRoot "translation-models.json"),
    ($translationManifest | ConvertTo-Json -Depth 4),
    $utf8NoBom)

$bundleBytes = (Get-ChildItem -LiteralPath $destinationRoot -Recurse -File | Measure-Object Length -Sum).Sum
Write-Host ("Offline baseline models: {0} ({1:N1} MB)" -f $destinationRoot, ($bundleBytes / 1MB))
