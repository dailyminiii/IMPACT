<#
Installs the externally distributed Noitom Unity SDK needed by the IMPACT
project. The SDK itself is not committed to this repository. Review and accept
Noitom's applicable license before running this script.
#>
[CmdletBinding()]
param(
    [string]$Repository = 'https://github.com/pnmocap/Neuron_Mocap_Live_Unity.git',
    [string]$Revision = '7045677fd1e9766fb7798b9bc44dbc8adb70403b'
)

$releaseRoot = Split-Path -Parent $PSScriptRoot
$target = Join-Path $releaseRoot 'unity_project\Assets\Noitom'

if (Test-Path -LiteralPath $target) {
    throw "Target already exists: $target`nRemove it manually only if you intend to reinstall the Noitom SDK."
}
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw 'Git is required to retrieve the Noitom SDK. Install Git and rerun this script.'
}

$checkout = Join-Path ([IO.Path]::GetTempPath()) ("impact-noitom-" + [guid]::NewGuid().ToString('N'))
try {
    git clone $Repository $checkout
    if ($LASTEXITCODE -ne 0) { throw 'Could not clone the Noitom SDK repository.' }

    git -C $checkout checkout $Revision
    if ($LASTEXITCODE -ne 0) { throw "Could not check out Noitom SDK revision $Revision." }

    $sdkSource = Join-Path $checkout 'Assets\Noitom'
    if (-not (Test-Path -LiteralPath $sdkSource)) {
        throw 'The downloaded repository does not contain Assets\Noitom.'
    }

    Copy-Item -LiteralPath $sdkSource -Destination $target -Recurse

    # The public scene serialises the transform component with the GUID used
    # at experiment time. Keep the SDK's own source code, but set its local
    # metadata GUID so Unity reconnects that scene reference after install.
    $transformMeta = Join-Path $target 'Scripts\Mocap\NeuronTransformsInstance.cs.meta'
    if (-not (Test-Path -LiteralPath $transformMeta)) {
        throw 'The downloaded Noitom SDK does not contain NeuronTransformsInstance.cs.meta.'
    }
    $metaText = Get-Content -LiteralPath $transformMeta -Raw
    $metaText = $metaText -replace '(?m)^guid: [0-9a-f]+\r?$', 'guid: abc25884aae421e4cab8879d1215ebc9'
    Set-Content -LiteralPath $transformMeta -Value $metaText -NoNewline

    Write-Host "Installed Noitom SDK source at $target"
    Write-Host 'Next, import Graph And Chart and the visual dependencies listed in unity_project/INSTALL_THIRD_PARTY_DEPENDENCIES.md.'
    Write-Host 'Reopen Unity and confirm that the project compiles before configuring hardware.'
}
finally {
    if (Test-Path -LiteralPath $checkout) {
        Remove-Item -LiteralPath $checkout -Recurse -Force
    }
}
