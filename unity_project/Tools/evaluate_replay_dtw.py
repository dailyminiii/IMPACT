#!/usr/bin/env python3
"""Evaluate motion-only DTW candidates for the public IMPACT replay data.

The script intentionally does not use participant IDs beyond file names and does
not write any participant data. It compares candidate time-warps using the
motion records shipped with the runnable Unity demo. EMG is used only as an
independent, post-hoc temporal-consistency check; it is never part of a DTW
cost, preserving the replay's motion-derived alignment.
"""

from __future__ import annotations

import argparse
import csv
import glob
import math
import os
import re
from dataclasses import dataclass
from statistics import median
from typing import Iterable, Sequence


HIPS = 0
SPINE2 = 9
RIGHT_SHOULDER = 13
RIGHT_ARM = 14
RIGHT_FOREARM = 15
RIGHT_HAND = 16
JOINT_COUNT = 21
ARM_TRUNK_WEIGHTS = {
    SPINE2: 0.20,
    RIGHT_SHOULDER: 0.20,
    RIGHT_ARM: 0.20,
    RIGHT_FOREARM: 0.20,
    RIGHT_HAND: 0.20,
}


@dataclass(frozen=True)
class Candidate:
    name: str
    weights: dict[int, float]
    displacement_weight: float
    velocity_weight: float
    normalize_motion: bool
    feature_type: str = "position"
    active_window: bool = False


@dataclass
class Recording:
    positions: list[list[tuple[float, float, float]]]
    rotations: list[list[tuple[float, float, float, float]]]
    emg: list[list[float]]


CANDIDATES = (
    Candidate("legacy_full_body_raw_pose", {joint: 1.0 / 20.0 for joint in range(1, 21)}, 1.0, 0.0, False),
    Candidate("right_arm_trunk_raw_pose", ARM_TRUNK_WEIGHTS, 1.0, 0.0, False),
    Candidate("right_arm_trunk_displacement", ARM_TRUNK_WEIGHTS, 1.0, 0.0, True),
    Candidate("right_arm_trunk_velocity", ARM_TRUNK_WEIGHTS, 0.0, 1.0, True),
    Candidate("right_arm_trunk_hybrid_60p_40v", ARM_TRUNK_WEIGHTS, 0.60, 0.40, True),
    Candidate("right_arm_trunk_rotation", ARM_TRUNK_WEIGHTS, 1.0, 0.0, True, "rotation"),
    Candidate("right_arm_trunk_angular_velocity", ARM_TRUNK_WEIGHTS, 0.0, 1.0, True, "rotation"),
    Candidate("right_arm_trunk_rotation_hybrid_60p_40v", ARM_TRUNK_WEIGHTS, 0.60, 0.40, True, "rotation"),
    Candidate("right_arm_trunk_displacement_active_window", ARM_TRUNK_WEIGHTS, 1.0, 0.0, True, "position", True),
    Candidate("right_arm_trunk_velocity_active_window", ARM_TRUNK_WEIGHTS, 0.0, 1.0, True, "position", True),
    Candidate("right_arm_trunk_rotation_active_window", ARM_TRUNK_WEIGHTS, 1.0, 0.0, True, "rotation", True),
    Candidate("right_arm_trunk_rotation_hybrid_60p_40v_active_window", ARM_TRUNK_WEIGHTS, 0.60, 0.40, True, "rotation", True),
    Candidate("active_hand_emphasis", {SPINE2: 0.20, RIGHT_SHOULDER: 0.10, RIGHT_ARM: 0.15, RIGHT_FOREARM: 0.25, RIGHT_HAND: 0.30}, 1.0, 0.0, True, "position", True),
    Candidate("active_forearm_emphasis", {SPINE2: 0.20, RIGHT_SHOULDER: 0.10, RIGHT_ARM: 0.15, RIGHT_FOREARM: 0.35, RIGHT_HAND: 0.20}, 1.0, 0.0, True, "position", True),
    Candidate("active_arm_only", {RIGHT_SHOULDER: 0.15, RIGHT_ARM: 0.20, RIGHT_FOREARM: 0.30, RIGHT_HAND: 0.35}, 1.0, 0.0, True, "position", True),
    Candidate("active_trunk_30pct", {SPINE2: 0.30, RIGHT_SHOULDER: 0.175, RIGHT_ARM: 0.175, RIGHT_FOREARM: 0.175, RIGHT_HAND: 0.175}, 1.0, 0.0, True, "position", True),
)


def vector_sub(a: tuple[float, float, float], b: tuple[float, float, float]) -> tuple[float, float, float]:
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def vector_scale(a: tuple[float, float, float], scalar: float) -> tuple[float, float, float]:
    return (a[0] * scalar, a[1] * scalar, a[2] * scalar)


def vector_distance(a: tuple[float, float, float], b: tuple[float, float, float]) -> float:
    dx, dy, dz = a[0] - b[0], a[1] - b[1], a[2] - b[2]
    return math.sqrt(dx * dx + dy * dy + dz * dz)


def vector_norm(a: tuple[float, float, float]) -> float:
    return math.sqrt(a[0] * a[0] + a[1] * a[1] + a[2] * a[2])


def mean_vector(values: Sequence[tuple[float, float, float]]) -> tuple[float, float, float]:
    count = len(values)
    return (
        sum(value[0] for value in values) / count,
        sum(value[1] for value in values) / count,
        sum(value[2] for value in values) / count,
    )


def normalise_quaternion(value: tuple[float, float, float, float]) -> tuple[float, float, float, float]:
    magnitude = math.sqrt(sum(component * component for component in value))
    return tuple(component / magnitude for component in value) if magnitude > 1e-8 else (0.0, 0.0, 0.0, 1.0)


def quaternion_angle(first: tuple[float, float, float, float], second: tuple[float, float, float, float]) -> float:
    first, second = normalise_quaternion(first), normalise_quaternion(second)
    # q and -q represent the same orientation.
    dot = min(1.0, max(-1.0, abs(sum(a * b for a, b in zip(first, second)))))
    return 2.0 * math.acos(dot)


def read_recording(path: str, is_expert: bool) -> Recording:
    positions: list[list[tuple[float, float, float]]] = []
    rotations: list[list[tuple[float, float, float, float]]] = []
    emg: list[list[float]] = []
    with open(path, newline="", encoding="utf-8-sig") as handle:
        for row in csv.reader(handle):
            if len(row) < 142 + 4 * JOINT_COUNT:
                continue
            values = [float(value) for value in row]
            frame: list[tuple[float, float, float]] = []
            rotation_frame: list[tuple[float, float, float, float]] = []
            for joint in range(JOINT_COUNT):
                offset = 79 + 3 * joint
                x, y, z = values[offset], values[offset + 1], values[offset + 2]
                # Match the Unity reader: expert coordinates are mirrored and
                # centimetres are converted to metres; user coordinates are metres.
                if is_expert:
                    frame.append((-x / 100.0, y / 100.0, z / 100.0))
                else:
                    frame.append((x, y, z))
                rotation_offset = 142 + 4 * joint
                if is_expert:
                    rotation_frame.append(normalise_quaternion((
                        values[rotation_offset + 1],
                        -values[rotation_offset + 2],
                        -values[rotation_offset + 3],
                        values[rotation_offset],
                    )))
                else:
                    rotation_frame.append(normalise_quaternion((
                        values[rotation_offset],
                        values[rotation_offset + 1],
                        values[rotation_offset + 2],
                        values[rotation_offset + 3],
                    )))
            positions.append(frame)
            rotations.append(rotation_frame)
            emg.append(values[:16])
    return Recording(positions, rotations, emg)


def root_relative(recording: Recording) -> list[list[tuple[float, float, float]]]:
    return [[vector_sub(joint, frame[HIPS]) for joint in frame] for frame in recording.positions]


def movement_scale(frames: Sequence[Sequence[tuple[float, float, float]]]) -> float:
    distances = [
        vector_distance(frame[RIGHT_HAND], frame[RIGHT_SHOULDER])
        for frame in frames
        if len(frame) > RIGHT_HAND
    ]
    return max(median(distances), 1e-4)


def motion_descriptors(recording: Recording, normalize_motion: bool) -> tuple[list[list[tuple[float, float, float]]], list[list[tuple[float, float, float]]]]:
    relative = root_relative(recording)
    if not normalize_motion:
        zero = [[(0.0, 0.0, 0.0) for _ in range(JOINT_COUNT)] for _ in relative]
        return relative, zero

    baseline_count = min(10, len(relative))
    baseline = [mean_vector([relative[frame][joint] for frame in range(baseline_count)]) for joint in range(JOINT_COUNT)]
    scale = movement_scale(relative)
    displacement = [
        [vector_scale(vector_sub(frame[joint], baseline[joint]), 1.0 / scale) for joint in range(JOINT_COUNT)]
        for frame in relative
    ]
    velocity: list[list[tuple[float, float, float]]] = []
    for frame_index, frame in enumerate(displacement):
        previous = displacement[max(0, frame_index - 1)]
        following = displacement[min(len(displacement) - 1, frame_index + 1)]
        velocity.append([vector_scale(vector_sub(following[joint], previous[joint]), 0.5) for joint in range(JOINT_COUNT)])
    return displacement, velocity


def rotation_descriptors(recording: Recording) -> tuple[list[list[float]], list[list[float]]]:
    baseline_count = min(10, len(recording.rotations))
    # The first frame is used as a stable, subject-specific posture reference.
    # Using orientation change (rather than absolute quaternion components)
    # makes the comparison robust to the expert/user coordinate conventions.
    baseline = recording.rotations[0 if baseline_count else 0]
    displacement = [
        [quaternion_angle(frame[joint], baseline[joint]) for joint in range(JOINT_COUNT)]
        for frame in recording.rotations
    ]
    velocity: list[list[float]] = []
    for frame_index, frame in enumerate(recording.rotations):
        previous = recording.rotations[max(0, frame_index - 1)]
        following = recording.rotations[min(len(recording.rotations) - 1, frame_index + 1)]
        velocity.append([0.5 * quaternion_angle(following[joint], previous[joint]) for joint in range(JOINT_COUNT)])
    return displacement, velocity


def local_cost(
    expert_features: tuple[list[list[tuple[float, float, float]]], list[list[tuple[float, float, float]]]],
    user_features: tuple[list[list[tuple[float, float, float]]], list[list[tuple[float, float, float]]]],
    expert_index: int,
    user_index: int,
    candidate: Candidate,
) -> float:
    expert_displacement, expert_velocity = expert_features
    user_displacement, user_velocity = user_features
    total = 0.0
    for joint, weight in candidate.weights.items():
        displacement_distance = vector_distance(expert_displacement[expert_index][joint], user_displacement[user_index][joint])
        velocity_distance = vector_distance(expert_velocity[expert_index][joint], user_velocity[user_index][joint])
        total += weight * (candidate.displacement_weight * displacement_distance + candidate.velocity_weight * velocity_distance)
    return total


def rotation_local_cost(
    expert_features: tuple[list[list[float]], list[list[float]]],
    user_features: tuple[list[list[float]], list[list[float]]],
    expert_index: int,
    user_index: int,
    candidate: Candidate,
) -> float:
    expert_displacement, expert_velocity = expert_features
    user_displacement, user_velocity = user_features
    total = 0.0
    for joint, weight in candidate.weights.items():
        total += weight * (
            candidate.displacement_weight * abs(expert_displacement[expert_index][joint] - user_displacement[user_index][joint])
            + candidate.velocity_weight * abs(expert_velocity[expert_index][joint] - user_velocity[user_index][joint])
        )
    return total


def dtw_path(expert: Recording, user: Recording, candidate: Candidate) -> list[tuple[int, int]]:
    user_window, user_offset = select_active_window(user, len(expert.positions)) if candidate.active_window else (user, 0)
    if candidate.feature_type == "rotation":
        expert_features = rotation_descriptors(expert)
        user_features = rotation_descriptors(user_window)
        cost_function = rotation_local_cost
    else:
        expert_features = motion_descriptors(expert, candidate.normalize_motion)
        user_features = motion_descriptors(user_window, candidate.normalize_motion)
        cost_function = local_cost
    rows, columns = len(expert.positions), len(user_window.positions)
    cost = [[math.inf] * columns for _ in range(rows)]
    previous = [[0] * columns for _ in range(rows)]  # 0 diagonal, 1 up, 2 left
    for row in range(rows):
        for column in range(columns):
            local = cost_function(expert_features, user_features, row, column, candidate)
            if row == 0 and column == 0:
                cost[row][column] = local
                continue
            options: list[tuple[float, int]] = []
            if row and column:
                options.append((cost[row - 1][column - 1], 0))
            if row:
                options.append((cost[row - 1][column], 1))
            if column:
                options.append((cost[row][column - 1], 2))
            best_cost, step = min(options, key=lambda item: item[0])
            cost[row][column] = local + best_cost
            previous[row][column] = step

    row, column = rows - 1, columns - 1
    path = [(row, column)]
    while row or column:
        step = previous[row][column]
        if step == 0 and row and column:
            row, column = row - 1, column - 1
        elif step == 1 and row:
            row -= 1
        else:
            column -= 1
        path.append((row, column))
    path.reverse()
    return [(expert_index, user_index + user_offset) for expert_index, user_index in path]


def zscore(values: Sequence[float]) -> list[float]:
    centre = sum(values) / len(values)
    variance = sum((value - centre) ** 2 for value in values) / len(values)
    scale = math.sqrt(variance)
    return [(value - centre) / scale for value in values] if scale > 1e-8 else [0.0] * len(values)


def pearson(first: Sequence[float], second: Sequence[float]) -> float:
    first_z, second_z = zscore(first), zscore(second)
    return sum(a * b for a, b in zip(first_z, second_z)) / len(first_z)


def smooth(values: Sequence[float], radius: int = 3) -> list[float]:
    output = []
    for index in range(len(values)):
        low, high = max(0, index - radius), min(len(values), index + radius + 1)
        output.append(sum(values[low:high]) / (high - low))
    return output


def emg_envelopes(recording: Recording) -> list[list[float]]:
    # The four replayed functional groups. Standardisation happens per group and
    # recording, so this measures temporal shape rather than inter-person amplitude.
    groups = ((0,), (4,), (9, 14), (12,))
    return [smooth([sum(frame[channel] for channel in group) / len(group) for frame in recording.emg]) for group in groups]


def right_hand_speed(recording: Recording) -> list[float]:
    relative = root_relative(recording)
    values = []
    for index in range(len(relative)):
        before = relative[max(0, index - 1)][RIGHT_HAND]
        after = relative[min(len(relative) - 1, index + 1)][RIGHT_HAND]
        values.append(vector_distance(after, before) * 0.5)
    return smooth(values, 2)


def right_arm_rotation_energy(recording: Recording) -> list[float]:
    angular_velocity = rotation_descriptors(recording)[1]
    return [sum(frame[joint] for joint in ARM_TRUNK_WEIGHTS if joint != SPINE2) for frame in angular_velocity]


def select_active_window(recording: Recording, target_frames: int) -> tuple[Recording, int]:
    """Return a stroke-centred user window plus its original-frame offset.

    The reference motions contain one 90-frame stroke. Longer user recordings
    can include waiting time before or after the stroke, so endpoint-constrained
    DTW would otherwise stretch the reference across unrelated idle frames.
    """
    if len(recording.positions) <= target_frames:
        return recording, 0

    energy = right_arm_rotation_energy(recording)
    active = [index for index, value in enumerate(energy) if value >= max(energy) * 0.25]
    if not active:
        return recording, 0

    centre = (min(active) + max(active)) // 2
    start = max(0, min(len(recording.positions) - target_frames, centre - target_frames // 2))
    end = start + target_frames
    return Recording(recording.positions[start:end], recording.rotations[start:end], recording.emg[start:end]), start


def evaluate_path(expert: Recording, user: Recording, path: Sequence[tuple[int, int]]) -> tuple[float, float, float]:
    expert_emg, user_emg = emg_envelopes(expert), emg_envelopes(user)
    emg_correlations = [
        pearson([expert_emg[group][expert_index] for expert_index, _ in path], [user_emg[group][user_index] for _, user_index in path])
        for group in range(4)
    ]
    emg_score = sum(emg_correlations) / len(emg_correlations)

    expert_speed, user_speed = right_hand_speed(expert), right_hand_speed(user)
    expert_peak = max(range(len(expert_speed)), key=expert_speed.__getitem__)
    user_peak = max(range(len(user_speed)), key=user_speed.__getitem__)
    mapped_user = [user_index for expert_index, user_index in path if expert_index == expert_peak]
    mapped_expert = [expert_index for expert_index, user_index in path if user_index == user_peak]
    peak_errors = []
    if mapped_user:
        peak_errors.append(abs(median(mapped_user) / max(1, len(user_speed) - 1) - expert_peak / max(1, len(expert_speed) - 1)))
    if mapped_expert:
        peak_errors.append(abs(median(mapped_expert) / max(1, len(expert_speed) - 1) - user_peak / max(1, len(user_speed) - 1)))
    # A path that omits either motion peak cannot claim temporal alignment.
    peak_error = min(peak_errors) if peak_errors else 1.0

    diagonal_steps = sum(1 for previous, current in zip(path, path[1:]) if current[0] - previous[0] == 1 and current[1] - previous[1] == 1)
    diagonal_fraction = diagonal_steps / max(1, len(path) - 1)
    return emg_score, peak_error, diagonal_fraction


def matched_pairs(data_directory: str) -> Iterable[tuple[str, str, str]]:
    user_pattern = re.compile(r"^(Sample_Session\d+)\.csv$")
    for path in sorted(glob.glob(os.path.join(data_directory, "Sample_Session*.csv"))):
        name = os.path.basename(path)
        match = user_pattern.match(name)
        if not match:
            continue
        session = match.group(1)
        expert_paths = sorted(glob.glob(os.path.join(data_directory, f"{session}_expert_*.csv")))
        if expert_paths:
            yield session, path, expert_paths[0]


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--data-dir", default=os.path.join(os.path.dirname(__file__), "..", "Assets", "RecordedData", "Sample"))
    args = parser.parse_args()

    pairs = []
    for session, user_path, expert_path in matched_pairs(os.path.abspath(args.data_dir)):
        user, expert = read_recording(user_path, False), read_recording(expert_path, True)
        # Session 1 contains no recorded positional change, so no motion-only
        # DTW method can be evaluated on it.
        if max(right_hand_speed(user)) < 1e-5:
            continue
        pairs.append((session, user, expert))

    print(f"Evaluated {len(pairs)} non-static user/expert replay pairs.")
    print("session,user_frames,expert_frames,user_active_window_25pct_peak")
    for session, user, expert in pairs:
        energy = right_arm_rotation_energy(user)
        active = [index for index, value in enumerate(energy) if value >= max(energy) * 0.25]
        print(f"{session},{len(user.positions)},{len(expert.positions)},{min(active)}-{max(active)}")
    print("candidate,mean_emg_shape_correlation,mean_peak_error,mean_diagonal_fraction")
    results = []
    for candidate in CANDIDATES:
        metrics = []
        for session, user, expert in pairs:
            path = dtw_path(expert, user, candidate)
            metrics.append(evaluate_path(expert, user, path))
        emg_score = sum(metric[0] for metric in metrics) / len(metrics)
        peak_error = sum(metric[1] for metric in metrics) / len(metrics)
        diagonal_fraction = sum(metric[2] for metric in metrics) / len(metrics)
        results.append((candidate.name, emg_score, peak_error, diagonal_fraction))
        print(f"{candidate.name},{emg_score:.4f},{peak_error:.4f},{diagonal_fraction:.4f}")

    # A good replay warp should improve an independent EMG-timing check and
    # reduce the mismatch of the peak hand-speed phase. Use ranks, so metrics
    # with different units do not arbitrarily dominate the decision.
    emg_rank = {name: rank for rank, (name, *_rest) in enumerate(sorted(results, key=lambda row: row[1], reverse=True), 1)}
    peak_rank = {name: rank for rank, (name, *_rest) in enumerate(sorted(results, key=lambda row: row[2]), 1)}
    smoothness_rank = {name: rank for rank, (name, *_rest) in enumerate(sorted(results, key=lambda row: row[3], reverse=True), 1)}
    winner = min(results, key=lambda row: (emg_rank[row[0]] + peak_rank[row[0]] + smoothness_rank[row[0]], row[0]))
    print(f"recommended={winner[0]}")


if __name__ == "__main__":
    main()
