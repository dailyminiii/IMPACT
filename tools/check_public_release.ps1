<#
.SYNOPSIS
Checks the release tree for expected public-release boundaries.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$releaseRoot = Split-Path -Parent $PSScriptRoot
$projectRoot = Join-Path $releaseRoot 'unity_project'
$assetsRoot = Join-Path $projectRoot 'Assets'

$forbidden = @(
    'Avatar',
    'badminton court',
    'Chart And Graph',
    'FRIKI_STUDIO',
    'Noitom',
    'TextMesh Pro'
) | ForEach-Object { Join-Path $assetsRoot $_ }

$present = $forbidden | Where-Object { Test-Path -LiteralPath $_ }
if ($present) {
    throw "Non-redistributable asset folders are still present: $($present -join ', ')"
}

$generated = @('.vs', 'Library', 'Logs', 'obj', 'Temp', 'UserSettings') |
    ForEach-Object { Join-Path $projectRoot $_ } |
    Where-Object { Test-Path -LiteralPath $_ }
if ($generated) {
    throw "Generated Unity/IDE files are still present: $($generated -join ', ')"
}

$audioAssets = Get-ChildItem -LiteralPath $assetsRoot -Recurse -File -Include '*.mp3', '*.wav', '*.ogg' -ErrorAction SilentlyContinue
if ($audioAssets) {
    throw "Audio assets are intentionally excluded from this public replay prototype: $($audioAssets.FullName -join ', ')"
}

$requiredProjectPaths = @(
    'Assets/Scenes/IMPACT_Main.unity',
    'Assets/Scripts/IMPACTFeedback.cs',
    'Assets/Scripts/IMPACTCSV.cs',
    'Assets/RecordedData/Sample/README.md',
    'Assets/RecordedData/ExpertMVC/README.md'
) | ForEach-Object { Join-Path $projectRoot $_ }
$requiredReleasePaths = @(
    'reference_data/expert_reference_database.hdf5',
    'reference_data/expert_replay_database.hdf5',
    'THIRD_PARTY_ASSET_AUDIT.md',
    'unity_project/INSTALL_THIRD_PARTY_DEPENDENCIES.md'
) | ForEach-Object { Join-Path $releaseRoot $_ }
$required = @($requiredProjectPaths + $requiredReleasePaths)
$missing = $required | Where-Object { -not (Test-Path -LiteralPath $_) }
if ($missing) {
    throw "Required public-release files are missing: $($missing -join ', ')"
}

$privateNetwork = rg -l '192\.168\.0\.(39|32)' $releaseRoot -g '!_PRIVATE_DO_NOT_PUBLISH/**' -g '!*.meta'
if ($privateNetwork) {
    throw "Private laboratory device addresses remain in: $($privateNetwork -join ', ')"
}

Write-Host 'Public-release boundary check: passed.'
