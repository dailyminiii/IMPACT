"""Build the 226-feature replay database aligned to the public matcher DB.

The AutoEncoder is intentionally searched on the original 79-feature
reference (EMG + global positions). Unity then needs the matching record's
local positions and local rotations to render the expert avatar. This script
copies only those 226 replay fields from an authorized/public-source full
reference and aligns them to the released matcher index using motion and the
record metadata.

The source is derived from the CC0 MultiSenseBadminton collection. It must
describe the same five experts and 1,529 unaugmented records as the public
matcher DB. No N=12 user-study material is read or written.
"""

from __future__ import annotations

import argparse
from pathlib import Path

import h5py
import numpy as np
from scipy.spatial import cKDTree


FEATURE_FRAMES = np.array([0, 22, 45, 67, 89])
FEATURE_COLUMNS = np.arange(0, 63, 3)
METADATA_KEYS = (
    "example_subject_ids",
    "example_label_indexes",
    "example_strokeNum",
    "example_skill_level",
)


def motion_fingerprints(matrices: np.ndarray) -> np.ndarray:
    positions = matrices[:, :, 16:79]
    return positions[:, FEATURE_FRAMES][:, :, FEATURE_COLUMNS].reshape(len(matrices), -1)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--matcher-db", required=True, type=Path)
    parser.add_argument("--full-source", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()

    with h5py.File(args.matcher_db, "r") as matcher, h5py.File(args.full_source, "r") as source:
        match_matrices = matcher["example_matrices"][:]
        source_matrices = source["example_matrices"][:]
        if match_matrices.shape != (1529, 90, 79):
            raise ValueError(f"Unexpected matcher database shape: {match_matrices.shape}")
        if source_matrices.shape != (1529, 90, 226):
            raise ValueError(f"Unexpected full-source shape: {source_matrices.shape}")

        distances, source_indices = cKDTree(motion_fingerprints(source_matrices)).query(
            motion_fingerprints(match_matrices), k=1
        )
        source_indices = np.asarray(source_indices, dtype=np.int64)
        if len(np.unique(source_indices)) != len(source_indices):
            raise ValueError("Motion alignment is not one-to-one")
        for key in METADATA_KEYS:
            if not np.array_equal(matcher[key][:], source[key][:][source_indices]):
                raise ValueError(f"Metadata mismatch after alignment: {key}")

        position_error = np.mean(
            np.abs(match_matrices[:, :, 16:79] - source_matrices[source_indices, :, 16:79]), axis=(1, 2)
        )
        if float(position_error.max()) > 1.0:
            raise ValueError(f"Aligned positions differ too much (max MAD {position_error.max():.4f} cm)")

        args.output.parent.mkdir(parents=True, exist_ok=True)
        with h5py.File(args.output, "w") as output:
            output.attrs["description"] = (
                "226-feature expert replay reference aligned one-to-one with expert_reference_database.hdf5"
            )
            output.attrs["source_license"] = "CC0 MultiSenseBadminton derived data"
            output.attrs["feature_order"] = (
                "lower_arm_emg_8,upper_arm_emg_8,global_position_63,local_position_63,local_quaternion_84"
            )
            output.attrs["alignment_max_position_mad_cm"] = float(position_error.max())
            output.attrs["alignment_median_position_mad_cm"] = float(np.median(position_error))
            output.create_dataset(
                "example_matrices",
                data=source_matrices[source_indices].astype(np.float32),
                chunks=(1, 90, 226),
                compression="gzip",
                compression_opts=4,
                shuffle=True,
            )
            output.create_dataset("source_replay_index", data=source_indices)
            for key in METADATA_KEYS:
                output.create_dataset(key, data=matcher[key][:])

    print(
        f"Wrote {args.output} with {len(source_indices)} aligned 226-feature records; "
        f"median/max position MAD = {np.median(position_error):.4f}/{position_error.max():.4f} cm"
    )


if __name__ == "__main__":
    main()
