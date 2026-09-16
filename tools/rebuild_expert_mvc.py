"""Rebuild consent-restricted expert MVC reference files.

This script expects authorized local HDF5 recordings. It does not download,
export, or publish participant recordings.

Usage:
  python tools/rebuild_expert_mvc.py --archive-root <Data_Archive>
"""

from __future__ import annotations

import argparse
from pathlib import Path

import h5py
import numpy as np
from scipy.signal import lfilter


SUBJECTS = ("Sub11", "Sub14", "Sub19", "Sub20", "Sub24")
REPLAY_FS_HZ = 50
RMS_WINDOW_S = 0.2
EXCLUDE_EDGE_S = 0.5
PRIME_SAMPLES = 128
B = np.asarray((0.2066, 0.0, -0.4131, 0.0, 0.2066))
A = np.asarray((1.0, 0.9051, 0.5979, 0.2907, 0.1958))


def decoded(value: bytes) -> str:
    return value.decode("utf-8")


def event_intervals(events, event_times, labels):
    intervals = []
    for label in labels:
        starts = [event_times[i] for i, row in enumerate(events)
                  if decoded(row[0]) == "Start" and decoded(row[1]) == label]
        stops = [event_times[i] for i, row in enumerate(events)
                 if decoded(row[0]) == "Stop" and decoded(row[1]) == label]
        if len(starts) != len(stops):
            raise ValueError(f"Unpaired calibration events for {label}")
        intervals.extend(zip(starts, stops))
    return intervals


def replay_envelope(samples: np.ndarray) -> np.ndarray:
    """The prototype's causal filter and trailing ten-sample RMS."""
    primed = np.vstack((np.repeat(samples[:1], PRIME_SAMPLES, axis=0), samples))
    filtered = lfilter(B, A, primed, axis=0)[PRIME_SAMPLES:]
    squared = filtered * filtered
    window = round(REPLAY_FS_HZ * RMS_WINDOW_S)
    kernel = np.ones(window) / window
    rms = np.sqrt(np.stack([
        np.convolve(squared[:, channel], kernel, mode="full")[: len(samples)]
        for channel in range(samples.shape[1])
    ], axis=1))
    rms[: window - 1] = 0.0
    return rms


def reference_for_device(h5_file, device_name: str, labels: tuple[str, ...]):
    stream = h5_file[f"{device_name}/emg-values"]
    signal = np.asarray(stream["data"][:], dtype=float)
    timestamps = np.asarray(stream["time_s"][:]).reshape(-1)
    calibration = h5_file["experiment-calibration/calibration"]
    events = calibration["data"][:]
    event_times = np.asarray(calibration["time_s"][:]).reshape(-1)

    trial_percentiles = []
    for start, stop in event_intervals(events, event_times, labels):
        mask = (timestamps >= start) & (timestamps <= stop)
        if mask.sum() < 2:
            # A labelled calibration event can occasionally fall in a wireless
            # recording gap. It must not be interpolated as though it were data.
            print(f"Skipping empty {device_name} calibration trial: {start}-{stop}")
            continue
        replay_times = np.arange(start, stop, 1 / REPLAY_FS_HZ)
        resampled = np.stack([
            np.interp(replay_times, timestamps[mask], signal[mask, channel])
            for channel in range(signal.shape[1])
        ], axis=1)
        envelope = replay_envelope(resampled)
        edge = round(EXCLUDE_EDGE_S * REPLAY_FS_HZ)
        stable = envelope[edge:-edge]
        if len(stable) == 0:
            raise ValueError("Calibration trial is too short after edge exclusion")
        trial_percentiles.append(np.percentile(stable, 95, axis=0))

    if not trial_percentiles:
        raise ValueError(f"No usable {device_name} calibration trials")
    return np.max(np.stack(trial_percentiles), axis=0)


def write_values(path: Path, values: np.ndarray):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("".join(f"{value:.5f}\n" for value in values), encoding="utf-8")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--archive-root", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path,
                        default=Path(__file__).parents[1] / "unity_project" / "Assets" /
                                "RecordedData" / "ExpertMVC")
    args = parser.parse_args()

    for subject in SUBJECTS:
        recordings = sorted((args.archive_root / subject).glob("*.hdf5"))
        if not recordings:
            raise FileNotFoundError(f"No HDF5 recording found for {subject}")
        with h5py.File(recordings[0], "r") as h5_file:
            forearm = reference_for_device(
                h5_file, "gforce-lowerarm-emg", ("Lower arm inward", "Lower arm outward"))
            arm = reference_for_device(h5_file, "gforce-upperarm-emg", ("Upper arm inward",))
        write_values(args.output_dir / f"{subject}Forearm.csv", forearm)
        write_values(args.output_dir / f"{subject}Arm.csv", arm)
        print(f"Wrote MVC values for {subject}")


if __name__ == "__main__":
    main()
