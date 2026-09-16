# IMPACT research artifact

IMPACT is an augmented-reality badminton feedback system that integrates
motion replay with muscle-activity feedback. This repository releases the
inspectable and reusable system implementation while separating it from the
IRB-restricted N=12 intervention-study records.

![Released expert-matching architecture](assets/figures/matching-architecture.png)

*Released expert-matching architecture. The public artifact includes the
encoder weights, reference databases, deterministic retrieval service, and
Unity-side replay integration represented in this diagram.*

## Included

| Directory | Released material |
|---|---|
| `unity_project/` | Sanitised original experimental scene, first-party runtime/feedback source, project settings, and MRTK package references. |
| `matching/` | AutoEncoder matcher, deterministic retrieval server, tests, configuration, and deployed forehand/backhand weights. |
| `reference_data/` | Five-expert reference/matching databases and their public-source builders. |
| `tools/` | Reproducible dependency installation, project preflight, matcher launch, and public-sample preparation tools. |

The public `Assets/RecordedData/Sample/` motion and `ExpertMVC/` calibration
files exist solely as a non-study replay demonstration. They contain neither
N=12 participant records nor participant identifiers.

## Before opening Unity

The repository intentionally excludes raw third-party Unity assets. Follow
[`unity_project/INSTALL_THIRD_PARTY_DEPENDENCIES.md`](unity_project/INSTALL_THIRD_PARTY_DEPENDENCIES.md), then run:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\install_noitom_sdk.ps1
powershell -ExecutionPolicy Bypass -File .\tools\verify_unity_project.ps1
```

Open `unity_project/` in Unity **2022.3.62f3**, allow package resolution, and
then open `Assets/Scenes/IMPACT_Main.unity`. To exercise automatic expert
matching, start the appropriate local matcher using `tools/start_matcher.ps1`
before entering Play mode.

## Deliberately excluded

- every N=12 intervention-study record, including motion, EMG, landing,
  questionnaires, surveys, sessions, interview, audio, video, transcript, and
  participant-level derivative;
- user-study analysis scripts/JASP projects and qualitative coding materials;
- raw third-party avatar, anatomy, court/racket, font, chart, and device-SDK
  assets; and
- Unity caches, local paths, credentials, generated outputs, and hardware
  network settings.

The five-expert reference database is distinct from the N=12 study and is
derived only from the public CC0 MultiSenseBadminton collection. See
[`reference_data/README.md`](reference_data/README.md).

## License and citation

First-party source code and the distributed model weights are under the MIT
License. Third-party dependencies retain their own licenses and are never
sub-licensed by this repository; see
[`THIRD_PARTY_ASSET_AUDIT.md`](THIRD_PARTY_ASSET_AUDIT.md).

Use `CITATION.cff` to cite the release. Its repository URL and Zenodo DOI will
be added when the public `v1.0.0` release is archived.
