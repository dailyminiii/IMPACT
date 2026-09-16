# Install third-party Unity dependencies

This repository intentionally does **not** distribute raw third-party Unity
assets. The original experimental scene is retained so that its structure and
the first-party IMPACT implementation can be inspected. A clean clone will
show missing references until the following components are acquired locally.

## Required to compile the original scene

1. Install Unity **2022.3.62f3** through Unity Hub.
2. From the repository root, run:

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\tools\install_noitom_sdk.ps1
   ```

   This retrieves the pinned, vendor-maintained Noitom Unity SDK into
   `Assets/Noitom/`. It does not install Axis Studio or connect to hardware.
   Read and accept Noitom's terms before use.
3. Acquire/import a compatible **Graph And Chart** package from BitSplash
   Interactive / Unity Asset Store. Import it so that its root folder is
   `Assets/Chart And Graph/`. The scene uses its `GraphChart` and
   `CanvasBarChart` components for replay and RMS visualisation.
4. Open the project once, allow Package Manager to resolve the included MRTK
   package references, then use **Window > TextMeshPro > Import TMP Essential
   Resources** if Unity prompts for TMP resources.

Run the preflight only after those steps:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\verify_unity_project.ps1
```

## Required for the original visual appearance

The scene's original visual objects used a Mixamo character, a Noitom
stickman, Friki Studio's *Human Male Anatomy*, and a separate badminton
court/racket asset. These assets are deliberately absent. You may import the
same assets under your own licenses or replace them with user-owned humanoid,
anatomy, court, and racket assets. A replacement must be wired to the existing
scene objects in the Inspector; it does not alter the released matching,
record/replay, or EMG-processing code.

The original typography used a legacy TMP asset with a MapleStory font. For
inspection, replacing missing font references with Unity's default TMP font is
sufficient. Do not add the original font to the public repository without a
separate redistribution review.

## Hardware boundary

The sample replay is offline. Noitom Axis Studio and the gForcePro+ SDK are
needed only for live capture. Configure hardware addresses and device-specific
settings locally; the public source contains only non-routable defaults and no
laboratory network address.

## Expected clean-clone result

After steps 1--4, `IMPACT_Main.unity` should compile and show the original
scene configuration. The public demonstration data can exercise replay and
automatic expert matching. It cannot reproduce the IRB-restricted N=12 study
or restore visual assets that the recipient has not separately obtained.
