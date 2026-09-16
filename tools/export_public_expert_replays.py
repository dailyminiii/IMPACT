"""Export the two additional public expert replay exemplars from authorized data.

The public release keeps only the resulting 90-frame CSV exemplars. This tool
requires the consent-restricted HDF5 recordings and annotation workbook held
locally by the study team; it never downloads or publishes them.

Usage:
  python tools/export_public_expert_replays.py \
      --archive-root <Data_Archive> --annotation <Annotation Data File.xlsx>
"""

from __future__ import annotations

import argparse
from pathlib import Path

import h5py
import numpy as np
import pandas as pd


FRAME_COUNT = 90
# First validated forehand exemplar for each additional expert, identified by
# source_annotation_row in the authorized expert-reference database.
EXEMPLAR_ROWS = {"Sub19": 5950, "Sub24": 7611}
STREAMS = (
    ("gforce-lowerarm-emg", "emg-values"),
    ("gforce-upperarm-emg", "emg-values"),
    ("pns-joint", "global-position"),
    ("pns-joint", "local-position"),
    ("pns-joint", "quaternion"),
)


def locate_recording(subject_dir: Path, start: float, stop: float) -> Path:
    """Return the recording whose motion stream covers the annotated stroke."""
    for recording in sorted(subject_dir.glob("*.hdf5")):
        with h5py.File(recording, "r") as h5_file:
            times = np.asarray(h5_file["pns-joint/quaternion/time_s"][:]).reshape(-1)
            if times[0] <= start and times[-1] >= stop:
                return recording
    raise FileNotFoundError(f"No recording covers {start}–{stop} for {subject_dir.name}")


def resample(h5_file, device: str, stream: str, start: float, stop: float):
    group = h5_file[f"{device}/{stream}"]
    timestamps = np.asarray(group["time_s"][:]).reshape(-1)
    values = np.asarray(group["data"][:], dtype=float)
    mask = (timestamps >= start) & (timestamps <= stop)
    if mask.sum() < 2:
        raise ValueError(f"Insufficient samples in {device}/{stream}")
    target_times = np.linspace(start, stop, FRAME_COUNT)
    return np.stack([
        np.interp(target_times, timestamps[mask], values[mask, column])
        for column in range(values.shape[1])
    ], axis=1)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--archive-root", type=Path, required=True)
    parser.add_argument("--annotation", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path,
                        default=Path(__file__).parents[1] / "unity_project" / "Assets" /
                                "RecordedData" / "Sample")
    args = parser.parse_args()

    annotations = pd.read_excel(args.annotation)
    for subject, annotation_row in EXEMPLAR_ROWS.items():
        row = annotations.iloc[annotation_row]
        if row["Subject Number"] != subject or row["Annotation Level 1\n(Stroke Type)"] != "Forehand Clear":
            raise ValueError(f"Unexpected annotation at row {annotation_row} for {subject}")
        start = float(row["Annotation Start Time"])
        stop = float(row["Annotation Stop Time"])
        recording = locate_recording(args.archive_root / subject, start, stop)
        with h5py.File(recording, "r") as h5_file:
            arrays = [resample(h5_file, *stream, start, stop) for stream in STREAMS]
        output = np.concatenate(arrays, axis=1)
        if output.shape != (FRAME_COUNT, 226):
            raise ValueError(f"Expected (90, 226); received {output.shape}")
        path = args.output_dir / f"Sample_Session100_expert_{subject}.csv"
        path.parent.mkdir(parents=True, exist_ok=True)
        np.savetxt(path, output, delimiter=",", fmt="%.6f")
        print(f"Wrote {path.name} from {recording.name}")


if __name__ == "__main__":
    main()
