#!/usr/bin/env python3
"""Reconstruct the expert encoder-training HDF5 from public data.

This script intentionally preserves the behavior of the historical IMPACT /
MuscleMinder preprocessing program, including its asymmetric Forehand negative
augmentation expression.  The historical expression is odd, but changing it
would change the reference samples and would therefore defeat reproduction.

Only public MultiSenseBadminton inputs are required:

* the ten expert HDF5 recordings downloaded by download_figshare_experts.py;
* ``Documentations/Annotation Data File.xlsx`` from the same Figshare record.

The produced HDF5 contains the three streams used by the retained encoder
training input, in this order: lower-arm EMG (8), upper-arm EMG (8), and PNS
global joint positions (63).  The resulting shape is expected to be
``(27497, 90, 79)`` for Figshare collection version 1.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import platform
import sys
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable

import h5py
import numpy as np
import pandas as pd
import scipy
from scipy import interpolate
from scipy.signal import butter, filtfilt


EXPERT_SUBJECTS = ("Sub11", "Sub14", "Sub19", "Sub20", "Sub24")
STREAMS = (
    ("gforce-lowerarm-emg", "emg-values", 8, True),
    ("gforce-upperarm-emg", "emg-values", 8, True),
    ("pns-joint", "global-position", 63, False),
)
STROKES = (
    ("Forehand Clear", 1),
    ("Backhand Driving", 0),
)

RESAMPLED_HZ = 30
SEGMENT_DURATION_S = 3
SEGMENT_LENGTH = RESAMPLED_HZ * SEGMENT_DURATION_S
AUGMENT_NUM = 10
EMG_BANDPASS_HZ = (25.0, 40.0)
EMG_ENVELOPE_LOWPASS_HZ = 12.0
FILTER_ORDER = 4

COL_SUBJECT = "Subject Number"
COL_START = "Annotation Start Time"
COL_STOP = "Annotation Stop Time"
COL_STROKE_NUMBER = "Stroke Num"
COL_STROKE = "Annotation Level 1\n(Stroke Type)"
COL_SKILL = "Annotation Level 2\n(Skill Level)"
COL_HORIZONTAL = "Annotation Level 3\n(Landing Location - Horizontal)"
COL_VERTICAL = "Annotation Level 3\n(Landing Location - Vertical)"
COL_HIT_LOCATION = "Annotation Level 4\n(Hitting Location, major voting)"
COL_HIT_SOUND = "Annotation Level 5\n(Hitting Sound, major voting)"
OUTCOME_COLUMNS = (COL_HORIZONTAL, COL_VERTICAL, COL_HIT_LOCATION, COL_HIT_SOUND)


def parse_args() -> argparse.Namespace:
    script_dir = Path(__file__).resolve().parent
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--source-root",
        type=Path,
        default=script_dir / "data" / "source",
        help="Root containing the selectively downloaded Figshare files.",
    )
    parser.add_argument(
        "--annotation",
        type=Path,
        default=None,
        help="Public annotation workbook (auto-discovered when omitted).",
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=script_dir
        / "data"
        / "processed"
        / "MuslceMinder_ExpertData_PNS_Augment_reconstructed.hdf5",
    )
    parser.add_argument(
        "--manifest",
        type=Path,
        default=script_dir / "data" / "processed" / "build_manifest.json",
    )
    parser.add_argument(
        "--overwrite",
        action="store_true",
        help="Replace the exact output and manifest paths if they already exist.",
    )
    parser.add_argument(
        "--skip-source-hashes",
        action="store_true",
        help="Skip SHA-256 calculation (faster, but weaker provenance).",
    )
    return parser.parse_args()


def sha256_file(path: Path, block_size: int = 8 * 1024 * 1024) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        while block := handle.read(block_size):
            digest.update(block)
    return digest.hexdigest()


def discover_inputs(source_root: Path, annotation: Path | None) -> tuple[dict[str, list[Path]], Path]:
    if not source_root.is_dir():
        raise FileNotFoundError(f"Source root does not exist: {source_root}")

    files_by_subject: dict[str, list[Path]] = {}
    all_hdf5 = sorted(source_root.rglob("*.hdf5"), key=lambda path: path.name)
    for subject in EXPERT_SUBJECTS:
        subject_files = [path for path in all_hdf5 if subject in path.parts or subject in path.name]
        if len(subject_files) != 2:
            raise RuntimeError(
                f"Expected exactly two public recordings for {subject}; found {len(subject_files)}: "
                f"{subject_files}"
            )
        files_by_subject[subject] = subject_files

    if annotation is None:
        candidates = list(source_root.rglob("Annotation Data File.xlsx"))
        if len(candidates) != 1:
            raise RuntimeError(
                "Could not uniquely auto-discover 'Annotation Data File.xlsx': "
                f"{candidates}"
            )
        annotation = candidates[0]
    annotation = annotation.resolve()
    if not annotation.is_file():
        raise FileNotFoundError(f"Annotation workbook does not exist: {annotation}")
    return files_by_subject, annotation


def normalized_token(value: Any) -> str:
    if pd.isna(value):
        return ""
    if isinstance(value, (int, np.integer)):
        return str(int(value))
    if isinstance(value, (float, np.floating)) and float(value).is_integer():
        return str(int(value))
    return str(value).strip()


def encode_skill(value: Any) -> int:
    # The public workbook stores text labels, while the retained refined copy
    # stores the same categories as 0/1/2.  Accept both representations so the
    # preprocessing route is invariant to that non-semantic workbook edit.
    mapping = {
        "Beginner": 0,
        "Intermediate": 1,
        "Expert": 2,
        "0": 0,
        "1": 1,
        "2": 2,
    }
    token = normalized_token(value)
    if token not in mapping:
        raise ValueError(f"Unknown skill value: {value!r}")
    return mapping[token]


def encode_horizontal(value: Any) -> int:
    mapping = {"-2": 0, "-1": 0, "0": 1, "Contact": 1, "1": 2, "2": 2}
    token = normalized_token(value)
    if token not in mapping:
        raise ValueError(f"Unknown horizontal landing value: {value!r}")
    return mapping[token]


def encode_vertical(value: Any) -> int:
    mapping = {str(index): index for index in range(6)}
    mapping["Contact"] = 3
    token = normalized_token(value)
    if token not in mapping:
        raise ValueError(f"Unknown vertical landing value: {value!r}")
    return mapping[token]


def encode_hit_location(value: Any) -> int:
    mapping = {"Back": 0, "Front": 1, "0": 0, "1": 1}
    token = normalized_token(value)
    if token not in mapping:
        raise ValueError(f"Unknown hit-location value: {value!r}")
    return mapping[token]


def encode_hit_sound(value: Any) -> int:
    mapping = {"Maybe": 0, "Good": 1, "0": 0, "1": 1}
    token = normalized_token(value)
    if token not in mapping:
        raise ValueError(f"Unknown hit-sound value: {value!r}")
    return mapping[token]


def read_annotations(annotation_path: Path) -> pd.DataFrame:
    frame = pd.read_excel(annotation_path)
    required = {
        COL_SUBJECT,
        COL_START,
        COL_STOP,
        COL_STROKE_NUMBER,
        COL_STROKE,
        COL_SKILL,
        *OUTCOME_COLUMNS,
    }
    missing = sorted(required.difference(frame.columns))
    if missing:
        raise RuntimeError(f"Annotation workbook is missing columns: {missing}")

    invalid = frame.loc[:, OUTCOME_COLUMNS].apply(
        lambda column: column.map(normalized_token).isin(("NoVid", "Not Contact"))
    ).any(axis=1)
    frame = frame.loc[~invalid].copy()
    frame["_source_row_index"] = frame.index.astype(np.int32)
    frame["_skill"] = frame[COL_SKILL].map(encode_skill).astype(np.int32)
    frame["_horizontal"] = frame[COL_HORIZONTAL].map(encode_horizontal).astype(np.int32)
    frame["_vertical"] = frame[COL_VERTICAL].map(encode_vertical).astype(np.int32)
    frame["_hit_location"] = frame[COL_HIT_LOCATION].map(encode_hit_location).astype(np.int32)
    frame["_hit_sound"] = frame[COL_HIT_SOUND].map(encode_hit_sound).astype(np.int32)
    return frame


def preprocess_emg(time_s: np.ndarray, data: np.ndarray) -> tuple[np.ndarray, float]:
    """Apply the historical bandpass -> rectify -> lowpass EMG pipeline.

    ``zeros_like`` and channel-wise assignment are preserved deliberately.  The
    public raw arrays are float32, so the historical implementation rounded the
    output of each filtering stage back to float32.
    """

    sampling_hz = (time_s.size - 1) / (float(time_s[-1]) - float(time_s[0]))
    nyquist = 0.5 * sampling_hz
    band_b, band_a = butter(
        FILTER_ORDER,
        [EMG_BANDPASS_HZ[0] / nyquist, EMG_BANDPASS_HZ[1] / nyquist],
        btype="band",
    )
    bandpassed = np.zeros_like(data)
    for channel in range(data.shape[1]):
        bandpassed[:, channel] = filtfilt(band_b, band_a, data[:, channel])

    rectified = np.abs(bandpassed)
    low_b, low_a = butter(
        FILTER_ORDER,
        EMG_ENVELOPE_LOWPASS_HZ / nyquist,
        btype="low",
    )
    envelope = np.zeros_like(rectified)
    for channel in range(rectified.shape[1]):
        envelope[:, channel] = filtfilt(low_b, low_a, rectified[:, channel])
    return envelope, sampling_hz


def augmentation_intervals(
    stroke_name: str, start: float, stop: float
) -> Iterable[tuple[str, int, float, float, float, float]]:
    """Yield condition bounds and target bounds in historical sample order."""

    for step_zero_based in range(AUGMENT_NUM - 1):
        step = step_zero_based + 1
        linear_shift = AUGMENT_NUM / RESAMPLED_HZ * step
        if stroke_name == "Forehand Clear":
            # Historical implementation: the condition's lower bound used a
            # linear shift, while its upper bound and both target bounds used
            # exponentiation.  Preserve this apparent bug for reproduction.
            target_shift = AUGMENT_NUM / (RESAMPLED_HZ**step)
            yield (
                "negative",
                step,
                start - linear_shift,
                stop - target_shift,
                start - target_shift,
                stop - target_shift,
            )
        else:
            yield (
                "negative",
                step,
                start - linear_shift,
                stop - linear_shift,
                start - linear_shift,
                stop - linear_shift,
            )

    for step_zero_based in range(AUGMENT_NUM - 1):
        step = step_zero_based + 1
        shift = AUGMENT_NUM / RESAMPLED_HZ * step
        yield ("positive", step, start + shift, stop + shift, start + shift, stop + shift)


def interval_has_sample(time_s: np.ndarray, lower: float, upper: float) -> bool:
    left = int(np.searchsorted(time_s, lower, side="left"))
    right = int(np.searchsorted(time_s, upper, side="right"))
    return right > left


def record_for_annotation(
    subject: str,
    stroke_name: str,
    label_index: int,
    stroke_index_within_type: int,
    annotation_row: pd.Series,
    direction: str,
    step: int,
) -> dict[str, Any]:
    return {
        "example_labels": stroke_name,
        "example_label_indexes": np.int32(label_index),
        "example_subject_ids": subject,
        "example_skill_level": np.int32(annotation_row["_skill"]),
        # Historical code stored j (the row's position within one stroke type),
        # not the workbook's globally supplied Stroke Num.
        "example_strokeNum": np.int32(stroke_index_within_type),
        "example_score_annot_3_hori": np.int32(annotation_row["_horizontal"]),
        "example_score_annot_3_ver": np.int32(annotation_row["_vertical"]),
        "example_score_annot_4": np.int32(annotation_row["_hit_location"]),
        "example_score_annot_5": np.int32(annotation_row["_hit_sound"]),
        "source_annotation_row": np.int32(annotation_row["_source_row_index"]),
        "source_stroke_number": np.int32(annotation_row[COL_STROKE_NUMBER]),
        "augmentation_direction": direction,
        "augmentation_step": np.int8(step),
    }


def build_stream_examples(
    subject: str,
    time_s: np.ndarray,
    data: np.ndarray,
    subject_annotations: pd.DataFrame,
) -> tuple[np.ndarray, list[dict[str, Any]]]:
    unique_time_s, unique_indices = np.unique(time_s, return_index=True)
    data = data[unique_indices]
    time_s = unique_time_s
    interpolator = interpolate.interp1d(
        time_s,
        data,
        axis=0,
        kind="slinear",
        fill_value="extrapolate",
    )

    examples: list[np.ndarray] = []
    records: list[dict[str, Any]] = []
    for stroke_name, label_index in STROKES:
        stroke_rows = subject_annotations.loc[subject_annotations[COL_STROKE] == stroke_name]
        for stroke_index, (_, row) in enumerate(stroke_rows.iterrows()):
            start = float(row[COL_START])
            stop = float(row[COL_STOP])
            for direction, step, cond_low, cond_high, target_low, target_high in augmentation_intervals(
                stroke_name, start, stop
            ):
                if not interval_has_sample(time_s, cond_low, cond_high):
                    continue
                target_time_s = np.linspace(
                    target_low,
                    target_high,
                    num=SEGMENT_LENGTH,
                    endpoint=True,
                )
                examples.append(interpolator(target_time_s))
                records.append(
                    record_for_annotation(
                        subject,
                        stroke_name,
                        label_index,
                        stroke_index,
                        row,
                        direction,
                        step,
                    )
                )

    if not examples:
        return np.empty((0, SEGMENT_LENGTH, data.shape[1]), dtype=np.float64), records
    return np.stack(examples, axis=0), records


def record_identity(record: dict[str, Any]) -> tuple[int, str, int]:
    direction = str(record["augmentation_direction"])
    return (
        int(record["source_annotation_row"]),
        direction,
        int(record["augmentation_step"]),
    )


HISTORICAL_DATASETS: dict[str, Any] = {
    "example_labels": h5py.string_dtype(encoding="utf-8"),
    "example_label_indexes": np.int32,
    "example_subject_ids": h5py.string_dtype(encoding="utf-8"),
    "example_skill_level": np.int32,
    "example_strokeNum": np.int32,
    "example_score_annot_3_hori": np.int32,
    "example_score_annot_3_ver": np.int32,
    "example_score_annot_4": np.int32,
    "example_score_annot_5": np.int32,
}
EXTRA_DATASETS: dict[str, Any] = {
    "source_annotation_row": np.int32,
    "source_stroke_number": np.int32,
    "augmentation_direction": h5py.string_dtype(encoding="utf-8"),
    "augmentation_step": np.int8,
}


def create_output_datasets(handle: h5py.File) -> dict[str, h5py.Dataset]:
    datasets: dict[str, h5py.Dataset] = {
        "example_matrices": handle.create_dataset(
            "example_matrices",
            shape=(0, SEGMENT_LENGTH, sum(stream[2] for stream in STREAMS)),
            maxshape=(None, SEGMENT_LENGTH, sum(stream[2] for stream in STREAMS)),
            chunks=(16, SEGMENT_LENGTH, sum(stream[2] for stream in STREAMS)),
            dtype=np.float64,
        )
    }
    for name, dtype in {**HISTORICAL_DATASETS, **EXTRA_DATASETS}.items():
        datasets[name] = handle.create_dataset(
            name,
            shape=(0,),
            maxshape=(None,),
            chunks=(1024,),
            dtype=dtype,
        )
    return datasets


def append_batch(
    datasets: dict[str, h5py.Dataset],
    matrices: np.ndarray,
    records: list[dict[str, Any]],
) -> tuple[int, int]:
    if matrices.shape[0] != len(records):
        raise RuntimeError(f"Matrix/metadata mismatch: {matrices.shape[0]} vs {len(records)}")
    start = int(datasets["example_matrices"].shape[0])
    stop = start + int(matrices.shape[0])
    for dataset in datasets.values():
        dataset.resize((stop, *dataset.shape[1:]))
    datasets["example_matrices"][start:stop] = matrices
    for name in {**HISTORICAL_DATASETS, **EXTRA_DATASETS}:
        datasets[name][start:stop] = np.asarray([record[name] for record in records])
    return start, stop


def output_attributes() -> dict[str, str]:
    return {
        "activities_to_classify": str(["None", "Overhead Clear", "Backhand Driving"]),
        "data_folders_bySubject": str(list(EXPERT_SUBJECTS)),
        "data_root_dir": "public Figshare MultiSenseBadminton collection v1",
        "device_streams_for_features": str(
            [(device, stream, channels) for device, stream, channels, _ in STREAMS]
        ),
        "resampled_Fs": str(RESAMPLED_HZ),
        "segment_length": str(SEGMENT_LENGTH),
        "segment_duration_s": str(SEGMENT_DURATION_S),
        # Retain the old nominal attribute and add the effective filters below.
        "filter_cutoff_emg_Hz": "15",
        "filter_cutoff_pressure_Hz": "5",
        "filter_cutoff_gaze_Hz": "5",
        "effective_emg_bandpass_Hz": str(EMG_BANDPASS_HZ),
        "effective_emg_envelope_lowpass_Hz": str(EMG_ENVELOPE_LOWPASS_HZ),
        "augmentation_mode": "historical-reconstruction",
        "forehand_negative_expression": "preserved historical asymmetric expression",
        "figshare_collection_doi": "10.6084/m9.figshare.c.6725706.v1",
    }


def package_versions() -> dict[str, str]:
    return {
        "python": platform.python_version(),
        "numpy": np.__version__,
        "pandas": pd.__version__,
        "scipy": scipy.__version__,
        "h5py": h5py.__version__,
    }


def main() -> int:
    args = parse_args()
    source_root = args.source_root.resolve()
    output_path = args.output.resolve()
    manifest_path = args.manifest.resolve()
    files_by_subject, annotation_path = discover_inputs(source_root, args.annotation)

    for path in (output_path, manifest_path):
        if path.exists() and not args.overwrite:
            raise FileExistsError(f"Refusing to replace existing path without --overwrite: {path}")
    output_path.parent.mkdir(parents=True, exist_ok=True)
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    if args.overwrite:
        for path in (output_path, manifest_path):
            if path.exists():
                path.unlink()

    print(f"Reading public annotations: {annotation_path}", flush=True)
    annotations = read_annotations(annotation_path)
    expert_annotations = annotations.loc[annotations[COL_SUBJECT].isin(EXPERT_SUBJECTS)]
    print(
        f"Valid expert annotations: {len(expert_annotations)} "
        f"({Counter(expert_annotations[COL_STROKE].tolist())})",
        flush=True,
    )

    manifest: dict[str, Any] = {
        "created_utc": datetime.now(timezone.utc).isoformat(),
        "builder": str(Path(__file__).resolve()),
        "source_root": str(source_root),
        "annotation": str(annotation_path),
        "output": str(output_path),
        "versions": package_versions(),
        "configuration": {
            "expert_subjects": list(EXPERT_SUBJECTS),
            "streams": [
                {
                    "device": device,
                    "stream": stream,
                    "channels": channels,
                    "emg_filter": is_emg,
                }
                for device, stream, channels, is_emg in STREAMS
            ],
            "resampled_hz": RESAMPLED_HZ,
            "segment_duration_s": SEGMENT_DURATION_S,
            "segment_length": SEGMENT_LENGTH,
            "augment_num": AUGMENT_NUM,
            "augmentation_variants_per_stroke": 2 * (AUGMENT_NUM - 1),
            "emg_bandpass_hz": list(EMG_BANDPASS_HZ),
            "emg_envelope_lowpass_hz": EMG_ENVELOPE_LOWPASS_HZ,
            "filter_order": FILTER_ORDER,
            "historical_forehand_negative_expression_preserved": True,
        },
        "inputs": [],
        "batches": [],
    }

    input_paths = [annotation_path] + [
        path for subject in EXPERT_SUBJECTS for path in files_by_subject[subject]
    ]
    for input_path in input_paths:
        item = {
            "path": str(input_path),
            "size_bytes": input_path.stat().st_size,
        }
        if not args.skip_source_hashes:
            print(f"Hashing source: {input_path.name}", flush=True)
            item["sha256"] = sha256_file(input_path)
        manifest["inputs"].append(item)

    total_by_label: Counter[str] = Counter()
    total_by_subject: Counter[str] = Counter()
    with h5py.File(output_path, "w") as output_handle:
        datasets = create_output_datasets(output_handle)
        output_handle.attrs.update(output_attributes())

        for subject in EXPERT_SUBJECTS:
            subject_annotations = expert_annotations.loc[expert_annotations[COL_SUBJECT] == subject]
            for raw_path in files_by_subject[subject]:
                print(f"Processing {subject}: {raw_path.name}", flush=True)
                stream_examples: list[np.ndarray] = []
                stream_records: list[list[dict[str, Any]]] = []
                stream_stats: list[dict[str, Any]] = []
                with h5py.File(raw_path, "r") as raw_handle:
                    for device, stream, expected_channels, is_emg in STREAMS:
                        group = raw_handle[device][stream]
                        time_s = np.squeeze(group["time_s"][:])
                        data = np.squeeze(group["data"][:])
                        if data.ndim != 2 or data.shape[1] != expected_channels:
                            raise RuntimeError(
                                f"Unexpected shape for {raw_path.name} {device}/{stream}: {data.shape}"
                            )
                        sampling_hz = (time_s.size - 1) / (float(time_s[-1]) - float(time_s[0]))
                        if is_emg:
                            print(f"  Filtering {device} ({data.shape[0]} x {data.shape[1]})", flush=True)
                            data, sampling_hz = preprocess_emg(time_s, data)
                        examples, records = build_stream_examples(
                            subject, time_s, data, subject_annotations
                        )
                        stream_examples.append(examples)
                        stream_records.append(records)
                        stream_stats.append(
                            {
                                "device": device,
                                "stream": stream,
                                "raw_shape": [int(value) for value in data.shape],
                                "sampling_hz": float(sampling_hz),
                                "examples": int(examples.shape[0]),
                            }
                        )
                        print(f"  {device}/{stream}: {examples.shape}", flush=True)

                min_count = min(examples.shape[0] for examples in stream_examples)
                if min_count == 0:
                    print("  No overlapping annotations; skipping file", flush=True)
                    continue

                reference_ids = [record_identity(record) for record in stream_records[-1][:min_count]]
                alignment = []
                for records in stream_records:
                    ids = [record_identity(record) for record in records[:min_count]]
                    alignment.append(ids == reference_ids)
                if not all(alignment):
                    print(
                        "  WARNING: stream sample identities differ; preserving historical prefix truncation",
                        flush=True,
                    )

                combined = np.concatenate(
                    [examples[:min_count] for examples in stream_examples], axis=2
                )
                retained_records = stream_records[-1][:min_count]
                start, stop = append_batch(datasets, combined, retained_records)
                labels = [str(record["example_labels"]) for record in retained_records]
                total_by_label.update(labels)
                total_by_subject.update([subject] * min_count)
                batch = {
                    "subject": subject,
                    "source": str(raw_path),
                    "output_range": [start, stop],
                    "examples": min_count,
                    "label_counts": dict(Counter(labels)),
                    "stream_prefix_alignment": alignment,
                    "streams": stream_stats,
                }
                manifest["batches"].append(batch)
                print(f"  Appended {min_count} examples at [{start}:{stop})", flush=True)

        final_shape = [int(value) for value in datasets["example_matrices"].shape]
        manifest["output_shape"] = final_shape
        manifest["label_counts"] = dict(total_by_label)
        manifest["subject_counts"] = dict(total_by_subject)
        print(f"Final output shape: {tuple(final_shape)}", flush=True)
        print(f"Labels: {dict(total_by_label)}", flush=True)
        print(f"Subjects: {dict(total_by_subject)}", flush=True)

    manifest["output_size_bytes"] = output_path.stat().st_size
    print("Hashing output HDF5", flush=True)
    manifest["output_sha256"] = sha256_file(output_path)
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(f"Wrote dataset: {output_path}", flush=True)
    print(f"Wrote manifest: {manifest_path}", flush=True)

    expected_shape = [27497, 90, 79]
    if manifest["output_shape"] != expected_shape:
        print(
            f"WARNING: expected public-v1 shape {expected_shape}, got {manifest['output_shape']}",
            file=sys.stderr,
        )
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
