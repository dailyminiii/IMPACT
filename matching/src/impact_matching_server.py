"""Deterministic public matcher compatible with the IMPACT Unity CSV protocol.

This is a post-study correction. It uses one expert-record index list for
latent search, subject metadata, and saved exemplar data.
"""

from __future__ import annotations

import argparse
import csv
import json
import socket
from pathlib import Path
from typing import Any

import h5py
import numpy as np
import torch
from torch import nn

from indexing import select_expert_indices


class TransformerAE(nn.Module):
    def __init__(
        self,
        input_channels: int = 63,
        seq_length: int = 89,
        latent_dim: int = 64,
        num_experts: int = 5,
    ) -> None:
        super().__init__()
        self.seq_length = seq_length
        self.input_projection = nn.Linear(input_channels, latent_dim)
        # Keep these two layers registered as named submodules.  The study
        # training class retained both the template layer and its deep-copied
        # Transformer stack in the checkpoint, so the public runtime must use
        # the same state-dict schema to load the deployed weights strictly.
        self.encoder_layer = nn.TransformerEncoderLayer(
            d_model=latent_dim,
            nhead=8,
            dim_feedforward=64,
            dropout=0.3,
            batch_first=True,
        )
        self.encoder = nn.TransformerEncoder(self.encoder_layer, num_layers=2)
        self.decoder_layer = nn.TransformerDecoderLayer(
            d_model=latent_dim,
            nhead=8,
            dim_feedforward=64,
            dropout=0.3,
            batch_first=True,
        )
        self.decoder = nn.TransformerDecoder(self.decoder_layer, num_layers=2)
        self.reconstruction_layer = nn.Linear(latent_dim, input_channels)
        self.classifier = nn.Sequential(
            nn.Linear(latent_dim, 32), nn.ReLU(), nn.Linear(32, num_experts)
        )

    def encode(self, value: torch.Tensor) -> torch.Tensor:
        return self.encoder(self.input_projection(value)).mean(dim=1)

    def forward(
        self, value: torch.Tensor
    ) -> tuple[torch.Tensor, torch.Tensor, torch.Tensor]:
        latent = self.encode(value)
        expanded = latent.unsqueeze(1).expand(-1, self.seq_length, -1)
        reconstructed = self.reconstruction_layer(self.decoder(expanded, expanded))
        return latent, reconstructed, self.classifier(latent)


def normalize_positions(value: np.ndarray) -> torch.Tensor:
    tensor = torch.as_tensor(np.array(value, copy=True), dtype=torch.float32)
    if tensor.ndim == 2:
        tensor = tensor.unsqueeze(0)
    if tensor.ndim != 3 or tensor.shape[-1] != 63:
        raise ValueError(f"Expected (*, frames, 63) positions, got {tuple(tensor.shape)}")
    reshaped = tensor.reshape(tensor.shape[0], tensor.shape[1], 21, 3)
    origin = reshaped[:, 0, 0, :].clone()
    return ((reshaped - origin[:, None, None, :]) / 100.0).reshape(tensor.shape)


class ExpertRepository:
    def __init__(self, database_path: Path, replay_database_path: Path, stroke: str, seed: int) -> None:
        with h5py.File(database_path, "r") as handle:
            matrices = handle["example_matrices"][:]
            subject_ids = handle["example_subject_ids"][:]
            labels = handle["example_label_indexes"][:]
        with h5py.File(replay_database_path, "r") as handle:
            replay_matrices = handle["example_matrices"][:]
            replay_subject_ids = handle["example_subject_ids"][:]
            replay_labels = handle["example_label_indexes"][:]

        if matrices.shape[:2] != replay_matrices.shape[:2] or replay_matrices.shape[2] != 226:
            raise ValueError(
                "Replay reference must align with the matching reference and contain 226 features; "
                f"got matching {matrices.shape}, replay {replay_matrices.shape}"
            )
        if not np.array_equal(subject_ids, replay_subject_ids) or not np.array_equal(labels, replay_labels):
            raise ValueError("Matching and replay reference metadata do not describe the same records")

        source_indices = select_expert_indices(subject_ids, labels, stroke, seed=seed)
        self.source_indices = np.asarray(source_indices, dtype=np.int64)
        self.full_records = replay_matrices[self.source_indices]
        self.subject_ids = subject_ids[self.source_indices]
        self.positions = normalize_positions(self.full_records[:, :, 16:79])


class Matcher:
    def __init__(self, repository: ExpertRepository, model_path: Path, device: str) -> None:
        self.repository = repository
        self.device = torch.device(device)
        self.model = TransformerAE().to(self.device)
        state = torch.load(model_path, map_location=self.device, weights_only=True)
        self.model.load_state_dict(state)
        self.model.eval()
        with torch.no_grad():
            self.expert_latents = self.model.encode(
                repository.positions.to(self.device)
            )

    def match(self, positions: np.ndarray) -> dict[str, Any]:
        normalized = normalize_positions(positions).to(self.device)
        with torch.no_grad():
            user_latent = self.model.encode(normalized)
            distances = torch.linalg.vector_norm(
                self.expert_latents - user_latent[0], dim=1
            )
        local_index = int(torch.argmin(distances).item())
        subject_value = self.repository.subject_ids[local_index]
        if isinstance(subject_value, bytes):
            subject_value = subject_value.decode("utf-8")
        return {
            "local_index": local_index,
            "source_index": int(self.repository.source_indices[local_index]),
            "distance": float(distances[local_index].item()),
            "subject_id": str(subject_value),
            "record": self.repository.full_records[local_index],
        }


def positions_from_unity_csv(csv_bytes: bytes) -> np.ndarray:
    """Read the recorded Unity payload used by the replay engine.

    The deployed recorder stores 90 rows with 226 columns: lower-arm EMG
    (8), upper-arm EMG (8), global positions (63), local positions (63),
    and local quaternions (84). Older development exports used a header plus
    interleaved position/quaternion values, so that format remains accepted.
    """
    text = csv_bytes.decode("utf-8-sig")
    raw_rows = [row for row in csv.reader(text.splitlines()) if row]
    if not raw_rows:
        raise ValueError("Unity CSV is empty")

    try:
        numeric_rows = np.asarray([[float(value) for value in row] for row in raw_rows], dtype=np.float32)
    except ValueError:
        # Legacy rows include one header. Positions were timestamp + (XYZ,
        # quaternion) for each of 21 joints.
        data_rows = raw_rows[1:]
        required = [column for joint in range(21) for column in (1 + 7 * joint, 2 + 7 * joint, 3 + 7 * joint)]
        numeric_rows = np.asarray(
            [[float(row[column]) for column in required] for row in data_rows],
            dtype=np.float32,
        )
    else:
        if numeric_rows.ndim != 2 or numeric_rows.shape[1] < 79:
            raise ValueError(f"Expected at least 79 values per Unity frame, got {numeric_rows.shape}")
        # 226-column replay payload: columns 16--78 are 21 global XYZ joints.
        numeric_rows = numeric_rows[:, 16:79]

    if numeric_rows.ndim != 2 or numeric_rows.shape[1] != 63 or numeric_rows.shape[0] < 2:
        raise ValueError(f"Expected at least two frames by 63 global positions, got {numeric_rows.shape}")

    # The Unity recorder reports metres; the released reference database and
    # the AutoEncoder training inputs use centimetres. Detect metres from the
    # root-relative body extent (a human skeleton is well below 5 metres) and
    # convert only at this matcher boundary, before the training-time /100
    # normalization in normalize_positions().
    joints = numeric_rows.reshape(numeric_rows.shape[0], 21, 3)
    body_extent = float(np.percentile(np.abs(joints - joints[:, :1, :]), 95))
    if not np.isfinite(body_extent) or body_extent <= 0:
        raise ValueError("Unity positions are not finite, non-degenerate coordinates")
    if body_extent < 5.0:
        numeric_rows = numeric_rows * 100.0
    elif body_extent > 500.0:
        raise ValueError(
            f"Unexpected Unity body extent {body_extent:.2f}; expected metres or centimetres"
        )

    # The deployed AutoEncoder was trained on 90 normalized time steps. The
    # recorder is allowed to save a longer or shorter replay clip, so resample
    # only the retrieval input; retain the source clip unchanged for replay.
    if numeric_rows.shape[0] != 90:
        source_time = np.linspace(0.0, 1.0, numeric_rows.shape[0])
        target_time = np.linspace(0.0, 1.0, 90)
        numeric_rows = np.stack(
            [np.interp(target_time, source_time, numeric_rows[:, column]) for column in range(63)],
            axis=1,
        ).astype(np.float32)
    return numeric_rows


def save_result(result: dict[str, Any], output_root: Path, subject: str, session: int) -> None:
    safe_subject = "".join(character for character in subject if character.isalnum() or character in "-_")
    if not safe_subject or safe_subject != subject:
        raise ValueError("Subject identifier may contain only letters, digits, '-' and '_'")
    output_dir = output_root / safe_subject
    output_dir.mkdir(parents=True, exist_ok=True)
    # A result must be unambiguous. Leaving an older expert result beside a
    # newly selected one would let Unity replay whichever filename happened to
    # sort first, silently defeating motion-driven retrieval.
    for stale_path in output_dir.glob(f"{safe_subject}_Session{session}_expert_*.csv"):
        stale_path.unlink()
    expert_path = output_dir / f"{safe_subject}_Session{session}_expert_{result['subject_id']}.csv"
    np.savetxt(expert_path, result["record"], delimiter=",", fmt="%.6f")
    meta = {key: value for key, value in result.items() if key != "record"}
    (output_dir / f"{safe_subject}_Session{session}_meta.json").write_text(
        json.dumps(meta, indent=2), encoding="utf-8"
    )


def receive_unity_request(connection: socket.socket) -> tuple[str, int, bytes]:
    payload = bytearray()
    while b"\nEND\n" not in payload and not payload.endswith(b"\nEND"):
        chunk = connection.recv(65536)
        if not chunk:
            break
        payload.extend(chunk)
    first_line, separator, remainder = bytes(payload).partition(b"\n")
    if not separator:
        raise ValueError("Missing START line")
    fields = first_line.decode("utf-8").strip().split("|")
    if len(fields) != 4 or fields[0] != "START":
        raise ValueError(f"Invalid START line: {first_line!r}")
    csv_payload, separator, _ = remainder.rpartition(b"\nEND")
    if not separator:
        raise ValueError("Missing END line")
    return fields[1], int(fields[2]), csv_payload


def serve(matcher: Matcher, host: str, port: int, output_root: Path) -> None:
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as server:
        server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        server.bind((host, port))
        server.listen(1)
        print(f"IMPACT matcher listening on {host}:{port}")
        while True:
            connection, address = server.accept()
            with connection:
                try:
                    subject, session, csv_payload = receive_unity_request(connection)
                    result = matcher.match(positions_from_unity_csv(csv_payload))
                    save_result(result, output_root, subject, session)
                    response = {key: value for key, value in result.items() if key != "record"}
                    connection.sendall((json.dumps(response) + "\n").encode("utf-8"))
                except Exception as error:  # Keep the service available but expose the failure.
                    connection.sendall((json.dumps({"error": str(error)}) + "\n").encode("utf-8"))
                    print(f"Request from {address} failed: {error}")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--stroke", required=True, choices=("backhand", "forehand"))
    parser.add_argument("--expert-db", required=True, type=Path)
    parser.add_argument(
        "--replay-db",
        required=True,
        type=Path,
        help="Aligned 226-feature expert sequences used only after latent retrieval.",
    )
    parser.add_argument("--model", required=True, type=Path)
    parser.add_argument("--output-root", type=Path, default=Path("outputs"))
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=5000)
    parser.add_argument("--seed", type=int, default=2025)
    parser.add_argument("--device", default="cuda:0" if torch.cuda.is_available() else "cpu")
    parser.add_argument("--match-file", type=Path, help="Match one Unity CSV and exit")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    repository = ExpertRepository(args.expert_db, args.replay_db, args.stroke, args.seed)
    matcher = Matcher(repository, args.model, args.device)
    if args.match_file:
        result = matcher.match(positions_from_unity_csv(args.match_file.read_bytes()))
        print(json.dumps({key: value for key, value in result.items() if key != "record"}, indent=2))
        return
    serve(matcher, args.host, args.port, args.output_root)


if __name__ == "__main__":
    main()
