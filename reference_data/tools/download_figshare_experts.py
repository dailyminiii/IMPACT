"""Selectively download MultiSenseBadminton records from public Figshare ZIPs.

The script uses only Python's standard library.  It treats each remote ZIP as a
seekable file backed by HTTP Range requests, so the three ~20 GB archives do not
need to be downloaded in full.  ``--scope experts`` preserves the original
five-expert download, while ``--scope all`` extracts every participant HDF5
needed to rebuild the paper's 30-Hz analysis dataset. Only the annotation
workbook is selected from the documentation archive.
"""

from __future__ import annotations

import argparse
import hashlib
import io
import json
import os
import re
import shutil
import sys
import tempfile
import urllib.request
import zipfile
from collections import OrderedDict
from dataclasses import asdict, dataclass
from pathlib import Path, PurePosixPath
from typing import Any


FIGSHARE_API = "https://api.figshare.com/v2/articles/{article_id}"
ARTICLE_IDS = (25383010, 25383001, 25383007, 25383004)
EXPERT_IDS = ("Sub11", "Sub14", "Sub19", "Sub20", "Sub24")
ALL_SUBJECT_IDS = tuple(f"Sub{index:02d}" for index in range(25) if index != 5)
USER_AGENT = "IMPACT-encoder-reproduction/1.0"


def request_json(url: str) -> dict[str, Any]:
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    with urllib.request.urlopen(request, timeout=120) as response:
        return json.load(response)


class HTTPRangeReader(io.RawIOBase):
    """Read-only, seekable HTTP object with a small LRU block cache."""

    def __init__(
        self,
        url: str,
        size: int,
        block_size: int = 8 * 1024 * 1024,
        max_cached_blocks: int = 8,
    ) -> None:
        self.url = url
        self.size = size
        self.block_size = block_size
        self.max_cached_blocks = max_cached_blocks
        self.position = 0
        self.cache: OrderedDict[int, bytes] = OrderedDict()

    def readable(self) -> bool:
        return True

    def seekable(self) -> bool:
        return True

    def tell(self) -> int:
        return self.position

    def seek(self, offset: int, whence: int = os.SEEK_SET) -> int:
        if whence == os.SEEK_SET:
            position = offset
        elif whence == os.SEEK_CUR:
            position = self.position + offset
        elif whence == os.SEEK_END:
            position = self.size + offset
        else:
            raise ValueError(f"unsupported whence: {whence}")
        if position < 0:
            raise ValueError("negative seek position")
        self.position = min(position, self.size)
        return self.position

    def _fetch_block(self, block_index: int) -> bytes:
        if block_index in self.cache:
            data = self.cache.pop(block_index)
            self.cache[block_index] = data
            return data

        start = block_index * self.block_size
        end = min(start + self.block_size, self.size) - 1
        request = urllib.request.Request(
            self.url,
            headers={
                "Range": f"bytes={start}-{end}",
                "User-Agent": USER_AGENT,
            },
        )
        with urllib.request.urlopen(request, timeout=180) as response:
            status = getattr(response, "status", None)
            content_range = response.headers.get("Content-Range")
            if status != 206 and not content_range:
                raise RuntimeError(
                    f"server did not honor Range request for {start}-{end}: "
                    f"status={status}"
                )
            data = response.read()
        expected = end - start + 1
        if len(data) != expected:
            raise IOError(
                f"short Range response for {start}-{end}: "
                f"expected {expected}, received {len(data)}"
            )
        self.cache[block_index] = data
        while len(self.cache) > self.max_cached_blocks:
            self.cache.popitem(last=False)
        return data

    def read(self, size: int = -1) -> bytes:
        if self.position >= self.size:
            return b""
        if size is None or size < 0:
            size = self.size - self.position
        size = min(size, self.size - self.position)
        if size == 0:
            return b""

        output = bytearray()
        remaining = size
        while remaining:
            block_index = self.position // self.block_size
            block_offset = self.position % self.block_size
            block = self._fetch_block(block_index)
            take = min(remaining, len(block) - block_offset)
            if take <= 0:
                raise IOError("invalid remote block boundary")
            output.extend(block[block_offset : block_offset + take])
            self.position += take
            remaining -= take
        return bytes(output)

    def readinto(self, buffer: bytearray) -> int:
        data = self.read(len(buffer))
        buffer[: len(data)] = data
        return len(data)


@dataclass
class SourceArchive:
    article_id: int
    article_doi: str
    article_title: str
    license: str
    file_id: int
    file_name: str
    size: int
    download_url: str
    supplied_md5: str


@dataclass
class ExtractedEntry:
    article_id: int
    outer_file_id: int
    zip_path: str
    uncompressed_size: int
    compressed_size: int
    zip_crc32: str
    local_path: str
    local_sha256: str


def load_archives() -> list[SourceArchive]:
    archives: list[SourceArchive] = []
    for article_id in ARTICLE_IDS:
        metadata = request_json(FIGSHARE_API.format(article_id=article_id))
        for file_metadata in metadata["files"]:
            archives.append(
                SourceArchive(
                    article_id=article_id,
                    article_doi=metadata["doi"],
                    article_title=metadata["title"],
                    license=metadata["license"]["name"],
                    file_id=int(file_metadata["id"]),
                    file_name=file_metadata["name"],
                    size=int(file_metadata["size"]),
                    download_url=file_metadata["download_url"],
                    supplied_md5=file_metadata["supplied_md5"],
                )
            )
    return archives


def has_expert_id(name: str) -> bool:
    return any(re.search(rf"(?<![A-Za-z0-9]){expert}(?![0-9])", name) for expert in EXPERT_IDS)


def has_analysis_subject_id(name: str) -> bool:
    return any(
        re.search(rf"(?<![A-Za-z0-9]){subject}(?![0-9])", name)
        for subject in ALL_SUBJECT_IDS
    )


def selected_entries(
    archive: SourceArchive,
    infos: list[zipfile.ZipInfo],
    scope: str,
) -> list[zipfile.ZipInfo]:
    is_documentation = "documentation" in archive.article_title.lower()
    if is_documentation:
        return [
            info
            for info in infos
            if not info.is_dir()
            and PurePosixPath(info.filename).name == "Annotation Data File.xlsx"
        ]
    return [
        info
        for info in infos
        if not info.is_dir()
        and (
            has_analysis_subject_id(info.filename)
            if scope == "all"
            else has_expert_id(info.filename)
        )
        and PurePosixPath(info.filename).suffix.lower() in {".hdf5", ".h5"}
    ]


def safe_output_path(root: Path, article_id: int, zip_name: str) -> Path:
    parts = [part for part in PurePosixPath(zip_name).parts if part not in ("", ".")]
    if not parts or any(part == ".." for part in parts):
        raise ValueError(f"unsafe ZIP path: {zip_name}")
    destination = root / str(article_id) / Path(*parts)
    destination.resolve().relative_to(root.resolve())
    return destination


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(4 * 1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def extract_entry(
    remote_zip: zipfile.ZipFile,
    archive: SourceArchive,
    info: zipfile.ZipInfo,
    output_root: Path,
) -> ExtractedEntry:
    destination = safe_output_path(output_root, archive.article_id, info.filename)
    destination.parent.mkdir(parents=True, exist_ok=True)
    if destination.exists() and destination.stat().st_size == info.file_size:
        local_hash = sha256_file(destination)
    else:
        with tempfile.NamedTemporaryFile(
            prefix=destination.name + ".",
            suffix=".part",
            dir=destination.parent,
            delete=False,
        ) as temporary:
            temporary_path = Path(temporary.name)
            try:
                with remote_zip.open(info, "r") as source:
                    shutil.copyfileobj(source, temporary, length=4 * 1024 * 1024)
            except Exception:
                temporary_path.unlink(missing_ok=True)
                raise
        if temporary_path.stat().st_size != info.file_size:
            temporary_path.unlink(missing_ok=True)
            raise IOError(f"size mismatch after extracting {info.filename}")
        temporary_path.replace(destination)
        local_hash = sha256_file(destination)

    return ExtractedEntry(
        article_id=archive.article_id,
        outer_file_id=archive.file_id,
        zip_path=info.filename,
        uncompressed_size=info.file_size,
        compressed_size=info.compress_size,
        zip_crc32=f"{info.CRC:08X}",
        local_path=str(destination),
        local_sha256=local_hash,
    )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, default=Path("data/source"))
    parser.add_argument(
        "--scope",
        choices=("experts", "all"),
        default="experts",
        help="Download five expert subjects or every participant HDF5.",
    )
    parser.add_argument("--list-only", action="store_true")
    parser.add_argument("--manifest", type=Path, default=Path("data/source_manifest.json"))
    args = parser.parse_args()

    archives = load_archives()
    manifest: dict[str, Any] = {
        "figshare_collection_doi": "10.6084/m9.figshare.c.6725706.v1",
        "scope": args.scope,
        "selected_subjects": list(EXPERT_IDS if args.scope == "experts" else ALL_SUBJECT_IDS),
        "expert_subjects": list(EXPERT_IDS),
        "archives": [asdict(archive) for archive in archives],
        "selected_entries": [],
        "extracted_entries": [],
    }

    for archive in archives:
        print(
            f"Inspecting {archive.article_title}: {archive.file_name} "
            f"({archive.size:,} bytes)",
            flush=True,
        )
        reader = HTTPRangeReader(archive.download_url, archive.size)
        with zipfile.ZipFile(reader) as remote_zip:
            chosen = selected_entries(archive, remote_zip.infolist(), args.scope)
            manifest["selected_entries"].extend(
                {
                    "article_id": archive.article_id,
                    "outer_file_id": archive.file_id,
                    "zip_path": info.filename,
                    "uncompressed_size": info.file_size,
                    "compressed_size": info.compress_size,
                    "zip_crc32": f"{info.CRC:08X}",
                }
                for info in chosen
            )
            print(
                f"  selected {len(chosen)} entries; "
                f"compressed bytes={sum(info.compress_size for info in chosen):,}",
                flush=True,
            )
            if not args.list_only:
                for index, info in enumerate(chosen, start=1):
                    print(f"  [{index}/{len(chosen)}] {info.filename}", flush=True)
                    extracted = extract_entry(remote_zip, archive, info, args.output)
                    manifest["extracted_entries"].append(asdict(extracted))

    args.manifest.parent.mkdir(parents=True, exist_ok=True)
    args.manifest.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(f"Wrote manifest: {args.manifest}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except KeyboardInterrupt:
        print("Interrupted", file=sys.stderr)
        raise SystemExit(130)
