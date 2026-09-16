[CmdletBinding()]
param(
    [string]$Python = 'python'
)

$ErrorActionPreference = 'Stop'
$releaseRoot = Split-Path -Parent $PSScriptRoot
$requirements = Join-Path $releaseRoot 'matching\requirements.txt'
$environment = Join-Path $releaseRoot 'matching\.venv'

if (-not (Get-Command $Python -ErrorAction SilentlyContinue)) {
    throw "Python command '$Python' was not found. Install Python 3.9 or later and rerun this script."
}
if (-not (Test-Path -LiteralPath $environment)) {
    & $Python -m venv $environment
}

$environmentPython = Join-Path $environment 'Scripts\python.exe'
& $environmentPython -m pip install --upgrade pip
& $environmentPython -m pip install -r $requirements

Write-Host "Matcher environment ready: $environment"
Write-Host 'Start a local forehand or backhand matcher with .\tools\start_matcher.ps1.'
