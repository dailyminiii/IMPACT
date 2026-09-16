[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('forehand', 'backhand')]
    [string]$Stroke,
    [string]$Python
)

$ErrorActionPreference = 'Stop'
$releaseRoot = Split-Path -Parent $PSScriptRoot
$server = Join-Path $releaseRoot 'matching\src\impact_matching_server.py'
$database = Join-Path $releaseRoot 'reference_data\expert_reference_database.hdf5'
$replayDatabase = Join-Path $releaseRoot 'reference_data\expert_replay_database.hdf5'
$weights = Join-Path $releaseRoot "matching\weights\$($Stroke)_transformer_ae.pth"
$outputRoot = Join-Path $releaseRoot 'unity_project\Assets\RecordedData'

foreach ($path in @($server, $database, $replayDatabase, $weights)) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Required matcher file is missing: $path" }
}
$environmentPython = Join-Path $releaseRoot 'matching\.venv\Scripts\python.exe'
if (-not $Python -and (Test-Path -LiteralPath $environmentPython)) {
    $Python = $environmentPython
}
if (-not $Python) { $Python = 'python' }
if (-not (Get-Command $Python -ErrorAction SilentlyContinue)) {
    throw "Python command '$Python' was not found. Install Python and the packages in matching\requirements.txt."
}

New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
Write-Host "Starting the $Stroke matcher on 127.0.0.1:5000."
Write-Host 'Keep this window open while Unity records a stroke and requests expert retrieval.'
& $Python $server --stroke $Stroke --expert-db $database --replay-db $replayDatabase --model $weights --output-root $outputRoot --host 127.0.0.1 --port 5000
