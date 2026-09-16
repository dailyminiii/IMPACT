# Public expert reference database

`expert_reference_database.hdf5` is the released reference database used by
the public matcher. It contains 1,529 90-frame examples from five expert
participants and the two strokes used in IMPACT (Forehand Clear and Backhand
Drive). Each frame has 79 features in this order:

```text
lower-arm EMG (8), upper-arm EMG (8), global joint positions (21 × XYZ)
```

The HDF5 file contains the example matrices, stroke labels, source stroke
numbers, public-source subject labels, skill labels, and source annotation
indices needed to retrieve and audit a reference example. It does not contain
any data from the N=12 intervention study. It excludes the source collection's
video, audio, gaze, pressure, questionnaire, and interview materials.

## Provenance

The database is derived from the public CC0
[MultiSenseBadminton collection](https://doi.org/10.6084/m9.figshare.c.6725706.v1).
The source dataset paper is [Seong et al. (2024)](https://www.nature.com/articles/s41597-024-03144-z).
The derived release is included here to make the matching configuration
directly inspectable; it is not a replacement for the complete source
collection.

SHA-256: `1abc34e49a34c719f9199736e4b5ba4354f6ad6671c072988514899981f2d2dc`

## Replay companion database

`expert_replay_database.hdf5` contains the same 1,529 records in exactly the
same order as `expert_reference_database.hdf5`, but each frame has the 226
fields required by Unity replay:

```text
lower-arm EMG (8), upper-arm EMG (8), global joint positions (21 × XYZ),
local joint positions (21 × XYZ), local joint rotations (21 × XYZW)
```

The AutoEncoder searches the original 79-feature database. After it selects a
record, the matcher retrieves the same-index 226-feature sequence from this
companion database. This preserves motion-driven retrieval without a manual
expert selector.

## Rebuilding from the official source

The scripts in `tools/` download only the five source experts and reconstruct
the historical encoder-training input from public data. Use a separate local
working directory for the multi-gigabyte source downloads and do not commit
them. The builder requires Python with `h5py`, `numpy`, `pandas`, `scipy`, and
`openpyxl`.

```powershell
python tools/download_figshare_experts.py --scope experts --output <local-source-directory>
python tools/build_expert_dataset.py `
  --source-root <local-source-directory> `
  --output <local-training-input.hdf5> `
  --manifest <local-manifest.json> `
  --overwrite
```

The committed matcher and replay databases are the citable runtime artifacts.
The public-source builder is provided to expose the preprocessing logic, but a
rebuild should be compared against the committed SHA-256 and metadata before it
is substituted for the released runtime files. Read each script's `--help`
before running it; no intervention-study material is required.
