param(
    [Parameter(Mandatory = $true)]
    [string]$ModelDirectory
)

$ErrorActionPreference = "Stop"
$resolvedModelDirectory = (Resolve-Path -LiteralPath $ModelDirectory).Path
$env:PINGYI_MODEL_DIR = $resolvedModelDirectory
$env:PINGYI_RUN_MODEL_TESTS = "1"

dotnet test `
    (Join-Path $PSScriptRoot "..\tests\PingYi.Core.Tests\PingYi.Core.Tests.csproj") `
    --filter "Category=LocalModels" `
    --logger "console;verbosity=normal"

if ($LASTEXITCODE -ne 0) {
    throw "PingYi quality baseline failed."
}
