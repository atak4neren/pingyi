param(
    [string]$Python = "py"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$engineVenv = Join-Path $projectRoot ".venv-engine"

if ($Python -eq "py") {
    & $Python -3 -m venv $engineVenv
} else {
    & $Python -m venv $engineVenv
}
if ($LASTEXITCODE -ne 0) { throw "Failed to create the Python virtual environment." }

$enginePython = Join-Path $engineVenv "Scripts\python.exe"
& $enginePython -m pip install --upgrade pip
if ($LASTEXITCODE -ne 0) { throw "Failed to upgrade pip." }
& $enginePython -m pip install -r (Join-Path $projectRoot "engine_host\requirements.txt")
if ($LASTEXITCODE -ne 0) { throw "Failed to install local engine dependencies." }

Write-Host "Local engine dependencies installed: $engineVenv"
Write-Host "Optional environment override: PINGYI_ENGINE_PYTHON=$enginePython"
