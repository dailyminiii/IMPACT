"""Deterministic expert-record selection shared by search and exemplar saving."""

from __future__ import annotations

import random
from collections import defaultdict
from typing import Hashable, Iterable, Sequence


STROKE_TO_LABEL = {"backhand": 0, "forehand": 1}


def select_expert_indices(
    subject_ids: Sequence[Hashable],
    labels: Sequence[int],
    stroke: str,
    *,
    max_per_subject: int = 3000,
    seed: int = 2025,
) -> list[int]:
    """Return one deterministic index list used for both search and retrieval.

    The historical implementation independently shuffled the search and save
    arrays. This function removes that mismatch. Records are grouped by expert,
    filtered by stroke label, sampled deterministically when necessary, and
    returned in stable source-index order.
    """

    if stroke not in STROKE_TO_LABEL:
        raise ValueError(f"Unsupported stroke: {stroke!r}")
    if len(subject_ids) != len(labels):
        raise ValueError("subject_ids and labels must have equal length")
    if max_per_subject <= 0:
        raise ValueError("max_per_subject must be positive")

    target_label = STROKE_TO_LABEL[stroke]
    grouped: dict[Hashable, list[int]] = defaultdict(list)
    for index, (subject_id, label) in enumerate(zip(subject_ids, labels)):
        if int(label) == target_label:
            grouped[subject_id].append(index)

    rng = random.Random(seed)
    selected: list[int] = []
    for subject_id in sorted(grouped, key=lambda value: str(value)):
        candidates = grouped[subject_id]
        if len(candidates) > max_per_subject:
            candidates = sorted(rng.sample(candidates, max_per_subject))
        selected.extend(candidates)

    if not selected:
        raise ValueError(f"No expert records found for stroke {stroke!r}")
    return selected

