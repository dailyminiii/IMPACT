# System reproduction boundary

This artifact releases the original, sanitised IMPACT AR scene configuration,
the first-party runtime and feedback source, the automatic AutoEncoder-based
expert matcher and released weights, and public expert reference data. It does
not contain N=12 intervention-study records or raw third-party Unity assets.

## What can be reproduced

1. Install Unity **2022.3.62f3** and follow
   [`unity_project/INSTALL_THIRD_PARTY_DEPENDENCIES.md`](unity_project/INSTALL_THIRD_PARTY_DEPENDENCIES.md).
   The instructions identify the exact external components that must be
   acquired by each user under their own licenses.
2. Run `powershell -ExecutionPolicy Bypass -File .\tools\install_noitom_sdk.ps1`
   to acquire the vendor-maintained Noitom Unity SDK at a pinned revision.
3. Run `powershell -ExecutionPolicy Bypass -File .\tools\verify_unity_project.ps1`.
   Warnings for dependencies not yet installed are expected; do not open the
   scene until the required chart and visual assets have also been installed.
4. Start `tools/start_matcher.ps1` for the desired stroke, then open
   `unity_project/Assets/Scenes/IMPACT_Main.unity`. The matcher takes the
   recorded user motion, embeds it with the released AutoEncoder, retrieves one
   public expert sequence, and writes the matched replay sequence locally.
   There is no manual expert selection.

The scene hierarchy, MRTK configuration, interaction flow, record/replay
controllers, MVC-normalised RMS logic, and automatic matching pipeline are
therefore inspectable and reusable. A public, author-recorded demonstration
motion and five public-expert MVC reference sets are included only to exercise
that pipeline; they are not N=12 study records.

## What cannot be reproduced from this repository alone

- the proprietary visual assets, chart package, motion-capture SDK, hardware,
  or capture software; and
- the IRB-restricted N=12 user-study data, analysis workspace, qualitative
  materials, or participant-level outcomes.

See [`THIRD_PARTY_ASSET_AUDIT.md`](THIRD_PARTY_ASSET_AUDIT.md) for the legal
boundary and [`APPARATUS_AND_STIMULI.md`](APPARATUS_AND_STIMULI.md) for the
hardware, signal-flow, UI, and procedure specification.
