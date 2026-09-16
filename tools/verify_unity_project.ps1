[CmdletBinding()]
param(
    [string]$UnityEditor = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe'
)

$ErrorActionPreference = 'Stop'

$releaseRoot = Split-Path -Parent $PSScriptRoot
$projectRoot = Join-Path $releaseRoot 'unity_project'
$requiredPaths = @(
    'Assets/Scenes/IMPACT_Main.unity',
    'Assets/IMPACTReplay.cs',
    'Assets/Scripts/IMPACTFeedback.cs',
    'Assets/Scripts/IMPACTCSV.cs',
    'Packages/manifest.json',
    'ProjectSettings/ProjectVersion.txt'
)

$missing = $requiredPaths | Where-Object {
    -not (Test-Path -LiteralPath (Join-Path $projectRoot $_))
}
if ($missing) {
    throw "The public project is incomplete. Missing: $($missing -join ', ')"
}

$versionLine = Select-String -LiteralPath (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt') `
    -Pattern '^m_EditorVersion:' | Select-Object -First 1
if ($versionLine.Line -notmatch '2022\.3\.62f3') {
    throw "Expected Unity 2022.3.62f3, found '$($versionLine.Line)'."
}

$noitomRoot = Join-Path $projectRoot 'Assets/Noitom'
$noitomRequired = @(
    'Scripts/Mocap/NeuronInstance.cs',
    'Scripts/Mocap/NeuronActor.cs',
    'Scripts/Mocap/NeuronTransformsPhysicalReference.cs'
)
$noitomMissing = $noitomRequired | Where-Object {
    -not (Test-Path -LiteralPath (Join-Path $noitomRoot $_))
}

Write-Host "Unity project source check: passed (Unity 2022.3.62f3)."
if ($noitomMissing) {
    Write-Warning "Noitom SDK is not installed. Run .\install_noitom_sdk.ps1 before opening the scene."
} else {
    Write-Host "Noitom SDK source check: passed."
}

foreach ($chartFile in @('Assets\Chart And Graph\Script\GraphChart\GraphChart.cs', 'Assets\Chart And Graph\Script\BarChart\CanvasBarChart.cs')) {
    if (-not (Test-Path -LiteralPath (Join-Path $projectRoot $chartFile))) {
        Write-Warning "Graph And Chart is not installed. Follow unity_project/INSTALL_THIRD_PARTY_DEPENDENCIES.md before opening the scene."
        break
    }
}
if (Test-Path -LiteralPath (Join-Path $projectRoot 'Assets\Chart And Graph\Script\GraphChart\GraphChart.cs')) {
    Write-Host 'Graph And Chart source check: passed.'
}

if (-not (Test-Path -LiteralPath $UnityEditor)) {
    Write-Warning "Unity Editor was not found at '$UnityEditor'. Open the project in Unity Hub after installing the specified version."
    exit 0
}

Write-Host "Unity Editor found at '$UnityEditor'."
Write-Host 'Open unity_project in Unity Hub, allow package resolution, and inspect the Console before entering Play mode.'
