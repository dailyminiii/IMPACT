import sys
import unittest
from pathlib import Path


sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "src"))

from indexing import select_expert_indices


class ExpertIndexingTests(unittest.TestCase):
    def test_same_seed_returns_same_indices(self):
        subjects = ["A"] * 5 + ["B"] * 5
        labels = [0, 0, 0, 1, 1, 0, 0, 1, 1, 1]
        first = select_expert_indices(subjects, labels, "backhand", max_per_subject=2, seed=7)
        second = select_expert_indices(subjects, labels, "backhand", max_per_subject=2, seed=7)
        self.assertEqual(first, second)

    def test_indices_can_address_search_and_saved_records_identically(self):
        subjects = ["ExpertA", "ExpertA", "ExpertB", "ExpertB"]
        labels = [1, 0, 1, 0]
        records = ["forehand-A", "backhand-A", "forehand-B", "backhand-B"]
        indices = select_expert_indices(subjects, labels, "forehand")
        search_records = [records[index] for index in indices]
        saved_records = [records[index] for index in indices]
        self.assertEqual(search_records, saved_records)
        self.assertEqual(search_records, ["forehand-A", "forehand-B"])

    def test_rejects_unknown_stroke(self):
        with self.assertRaises(ValueError):
            select_expert_indices(["A"], [0], "smash")


if __name__ == "__main__":
    unittest.main()

