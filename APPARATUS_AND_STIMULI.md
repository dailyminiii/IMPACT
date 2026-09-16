# IMPACT apparatus and stimuli specification

This document records the apparatus, feedback stimuli, and public-release
boundary for the IMPACT static-stance badminton drill. It is derived from the
reported system and study methods and from the included Unity project. It does
not disclose any N=12 participant record, calibration file, recording, or
participant-level configuration.

## Experimental apparatus

| Component | Experimental role | Public-release status |
|---|---|---|
| Microsoft HoloLens 2 | Optical see-through display for the AR interface | Hardware not supplied; Unity/OpenXR project configuration is included. |
| Perception Neuron Studio IMUs | Full-body motion capture; 21-joint global positions were supplied to matching and replay | Device SDK is not redistributed. |
| gForcePro+ EMG bands | Upper-limb EMG recording from the dominant forearm and upper arm | Device SDK, pairing information, and MVC records are not redistributed. |
| Laptop | Signal preprocessing, expert retrieval, and AR rendering | Device model and operating-system specification were not recorded in this release. |
| Holographic Remoting over Wi-Fi | Streamed rendered AR content from laptop to HoloLens 2 | Network addresses and laboratory configuration were removed. |
| Shuttlecock launcher and target zones | Delivered shuttlecocks from a fixed position toward predefined forehand-clear and backhand-drive target areas | Launcher model and physical layout dimensions are not recorded in this release. |

Participants performed a static-stance posture-with-impact drill using a
forehand clear and a backhand drive. The static stance deliberately retains
physical racket--shuttle contact while excluding footwork and tactical
decision-making.

## System and signal flow

1. Perception Neuron Studio and gForcePro+ transmit motion and EMG to the
   laptop.
2. A recorded stroke is represented by 21 three-dimensional joint positions;
   the stroke-specific matcher retrieves a kinematically proximate public-data
   expert trial.
3. The retrieved trial supplies the expert avatar reference in both AR
   configurations and its paired EMG signal in Motion+Muscle.
4. EMG is converted to RMS envelopes, normalized using the corresponding MVC
   value, and delivered in the interface described below.
5. The laptop streams the rendered views to the HoloLens 2 via Holographic
   Remoting.

The released matching implementation, configs, weights, and public expert
database are in `matching/` and `reference_data/`. The Unity scene and
first-party runtime source are in `unity_project/`.

## Software specification

| Software component | Recorded version or source |
|---|---|
| Unity | 2022.3.62f3 (`unity_project/ProjectSettings/ProjectVersion.txt`) |
| Unity XR Management | 4.5.0 (`unity_project/Packages/manifest.json`) |
| Unity OpenXR | 1.10.0 (`unity_project/Packages/manifest.json`) |
| MRTK / Graphics Tools / Windows TTS | Exact local package archives in `unity_project/Packages/MixedReality/`; versions and licenses are listed in `unity_project/THIRD_PARTY_NOTICES.md`. |
| First-party scene and runtime | `unity_project/Assets/Scenes/IMPACT_Main.unity` and `unity_project/Assets/` |
| First-party synchronized feedback controller | `unity_project/Assets/Scripts/IMPACTFeedback.cs` |

No claim is made that this bundle is a zero-configuration hardware build.
Noitom and gForcePro+ software, avatars, court/racket models, and fonts must
be independently licensed and installed; see
`unity_project/THIRD_PARTY_NOTICES.md`. Audio assets are deliberately not
included in this public replay prototype.

## Feedback stimuli and interaction

The experiment used three conditions.

| Condition | During-stroke display | Post-stroke reflection |
|---|---|---|
| No Feedback | No AR visualization or overlay | No AR replay |
| Motion-Only | User and retrieved-expert avatar presentation without EMG overlays | Side-by-side motion replay |
| Motion+Muscle | Motion-Only components plus a head-locked peripheral EMG HUD | Side-by-side motion replay plus synchronized user/expert EMG envelopes |

The Motion+Muscle HUD showed four labeled vertical bars on a common
MVC-normalized scale: Forearm Flexion, Forearm Extension, Arm Flexion, and Arm
Extension. Gray encoded flexion and green encoded extension, with text labels
and fixed order providing the non-color mapping. For each channel, the runtime
uses a 10-sample RMS window, MVC normalization, functional-group averaging,
and an exponential moving average with alpha = 0.2. A bar updates only when
the smoothed value differs by at least 0.05 MVC-normalized units.

After every tenth stroke, both AR conditions presented the user stroke and its
retrieved expert trial in a side-by-side replay. Motion+Muscle additionally
showed MVC-normalized RMS envelopes for Forearm Extension in forehand clears
and Arm Extension in backhand drives. Motion and EMG shared a timeline with an
impact-frame marker. Users could scrub the timeline, set playback speed from
0.25x to 1.0x, and adjust avatar scale.

The real-time HUD and replay-based EMG display were bundled in the
Motion+Muscle condition. This specification therefore documents a system
configuration; it does not claim to isolate either display's causal effect.

## Procedure-relevant parameters

- Each participant completed the forehand-clear and backhand-drive conditions
  under all three feedback configurations.
- Each condition used a 20-stroke no-feedback pre-test, 15-minute practice,
  and a 20-stroke no-feedback post-test.
- The AR conditions supported on-demand replay and instructed participants to
  review replay once every ten strokes.
- MVC measurements were collected at the start of each session for EMG
  normalization.

## Public-release exclusions

The repository excludes all N=12 recordings, raw/derived motion and EMG,
MVC values, shuttle-landing records, questionnaires, interviews, transcripts,
replay CSVs, participant labels, local IP addresses, and hardware pairing
settings. Any new local recordings are written below
`unity_project/Assets/IMPACTData/` and must not be committed or published.
