# Public expert reference database

`expert_reference_database.hdf5` is the released reference database used by the public matcher. It contains 1,529 90-frame examples from five expert participants and the two stroke types used in IMPACT: Forehand Clear and Backhand Drive. Each example is a `90 × 79` matrix. Each frame contains 79 features in the following order:

```text
lower-arm EMG (8), upper-arm EMG (8), global joint positions (21 × XYZ)
```

The HDF5 file contains the example matrices, stroke labels, source stroke identifiers, public-source participant labels, skill labels, and source annotation indices needed to retrieve and audit a reference example.

## Provenance

The database is derived from the public CC0 [MultiSenseBadminton collection](https://doi.org/10.6084/m9.figshare.c.6725706.v1). The source dataset paper is [Seong et al. (2024)](https://www.nature.com/articles/s41597-024-03144-z).


## Replay companion database

`expert_replay_database.hdf5` contains the same 1,529 records as `expert_reference_database.hdf5`, in the same order. Each record is a `90 × 226` sequence containing the fields required for Unity replay:

```text
lower-arm EMG (8), upper-arm EMG (8), global joint positions (21 × XYZ),
local joint positions (21 × XYZ), local joint rotations (21 × XYZW)
```

The autoencoder searches the original 79-feature reference database. After selecting a reference record, the matcher retrieves the sequence at the corresponding index from the 226-feature replay companion database. This index-aligned structure preserves motion-driven retrieval without requiring a manual expert selector.

## Rebuilding from the official source

The scripts in `tools/` download only recordings from the five source experts and reconstruct the public-source inputs used to build the matcher database.

The builder requires Python with `h5py`, `numpy`, `pandas`, `scipy`, and `openpyxl`.

```powershell
python tools/download_figshare_experts.py --scope experts --output <local-source-directory>
python tools/build_expert_dataset.py `
  --source-root <local-source-directory> `
  --output <local-training-input.hdf5> `
  --manifest <local-manifest.json> `
  --overwrite
```

The committed matcher and replay databases are the runtime files included in this release. The public-source builder exposes the preprocessing logic, but a rebuilt database should be compared with the committed SHA-256 and metadata before replacing the released runtime files.

Read each script's `--help` before running it. No intervention-study material is required.
