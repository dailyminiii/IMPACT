"""Write reproducible synthetic EMG into the public motion-only sample.

This script changes only the first 16 EMG columns of the public sample and
its two MVC files. It preserves all 210 motion/rotation columns. The values
are derived from the AutoEncoder-selected expert replay with fixed gains and
phase offsets; they are a labelled demo signal, never participant data.
"""

from __future__ import annotations

import argparse
import csv
import json
from pathlib import Path

import numpy as np


GAINS = np.array(
    [0.88, 1.04, 0.92, 1.07, 0.84, 1.02, 0.90, 1.06,
     0.91, 1.05, 0.87, 1.03, 0.94, 1.00, 0.89, 1.08],
    dtype=np.float64,
)
AMPLITUDE_COMPENSATION = 1.4


def read_mvc(path: Path) -> np.ndarray:
    values = []
    for line in path.read_text(encoding="utf-8-sig").splitlines():
        line = line.strip()
        if line and not line.lower().startswith("channel"):
            values.append(float(line.split(",")[-1]))
    if len(values) != 8 or any(value <= 0 for value in values):
        raise ValueError(f"Expected eight positive MVC values in {path}")
    return np.asarray(values, dtype=np.float64)


def write_mvc(path: Path, values: np.ndarray) -> None:
    lines = ["Channel, MVC Value"]
    lines.extend(f"Channel {index + 1},{value:.6f}" for index, value in enumerate(values))
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--sample", required=True, type=Path)
    parser.add_argument("--expert", required=True, type=Path)
    parser.add_argument("--expert-id", required=True)
    parser.add_argument("--expert-mvc-dir", required=True, type=Path)
    parser.add_argument("--sample-forearm-mvc", required=True, type=Path)
    parser.add_argument("--sample-arm-mvc", required=True, type=Path)
    parser.add_argument("--metadata", required=True, type=Path)
    args = parser.parse_args()

    sample = np.loadtxt(args.sample, delimiter=",", dtype=np.float64)
    expert = np.loadtxt(args.expert, delimiter=",", dtype=np.float64)
    # Some original recorder exports retain the 84 global-quaternion fields
    # after the 226 fields consumed by replay. Keep those fields untouched.
    if sample.ndim != 2 or sample.shape[1] < 226:
        raise ValueError(f"Expected sample shape (*, at least 226), got {sample.shape}")
    if expert.ndim != 2 or expert.shape[1] != 226:
        raise ValueError(f"Expected expert shape (*, 226), got {expert.shape}")

    forearm_mvc = read_mvc(args.expert_mvc_dir / f"{args.expert_id}Forearm.csv")
    arm_mvc = read_mvc(args.expert_mvc_dir / f"{args.expert_id}Arm.csv")
    mvc = np.concatenate((forearm_mvc, arm_mvc))

    source_time = np.linspace(0.0, 1.0, expert.shape[0])
    target_time = np.linspace(0.0, 1.0, sample.shape[0])
    expert_emg = np.stack(
        [np.interp(target_time, source_time, expert[:, channel]) for channel in range(16)], axis=1
    )
    phases = target_time[:, None]
    channels = np.arange(16)[None, :]
    modulation_normalized = 0.018 * np.sin(phases * 2 * np.pi * (1.2 + channels * 0.03) + channels)
    # Resampling the 90-frame expert clip to the 131-frame sample attenuates
    # its 10--20 Hz band-pass energy. Compensate at raw-signal level so the
    # demo user trace remains readable after the unchanged Unity pipeline.
    synthetic_emg = np.clip(
        expert_emg * GAINS * AMPLITUDE_COMPENSATION + modulation_normalized * mvc,
        0.0,
        None,
    )

    sample[:, :16] = synthetic_emg
    np.savetxt(args.sample, sample, delimiter=",", fmt="%.6f")
    write_mvc(args.sample_forearm_mvc, forearm_mvc)
    write_mvc(args.sample_arm_mvc, arm_mvc)
    args.metadata.write_text(json.dumps({
        "data_status": "synthetic public demo EMG; not participant data",
        "source": f"AutoEncoder-selected expert replay {args.expert.name}",
        "expert_id": args.expert_id,
        "emg_columns_overwritten": list(range(16)),
        "motion_rotation_columns_preserved": {
            "start_column": 16,
            "end_column_inclusive": sample.shape[1] - 1,
            "count": sample.shape[1] - 16,
        },
        "fixed_gains": GAINS.tolist(),
        "raw_amplitude_compensation": AMPLITUDE_COMPENSATION,
        "normalized_modulation_amplitude": 0.018,
    }, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote synthetic EMG into {args.sample.name}; motion/rotation columns were preserved.")


if __name__ == "__main__":
    main()
