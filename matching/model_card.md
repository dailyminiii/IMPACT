# IMPACT TransformerAE model card

## Intended use

The model encodes an 89-frame, 21-joint global-position sequence into a
64-dimensional latent representation and retrieves the nearest expert record
within the selected stroke type. It is research software for inspecting the
IMPACT matching pipeline, not a clinical or autonomous coaching model.

## Architecture

- input: 63 position values per frame (21 joints × XYZ);
- sequence length: 89 frames;
- latent dimension: 64;
- Transformer encoder layers: 2;
- attention heads: 8;
- feed-forward dimension: 64;
- dropout: 0.3; and
- auxiliary expert classifier classes: 5.

## Training-data provenance

Both deployed checkpoints were trained exclusively from the public
MultiSenseBadminton collection
([DOI 10.6084/m9.figshare.c.6725706.v1](https://doi.org/10.6084/m9.figshare.c.6725706.v1)),
which is published under CC0. The N=12 intervention-study dataset is not used
to train these checkpoints and is not included in this artifact.

The released database is the five-expert derived reference set described in
`../reference_data/README.md`. The retained public build scripts can download
the official source and rebuild this database. The two checkpoints are released
for inspection and reuse; independently regenerating identical checkpoint bytes
requires the archived training environment and is not promised by this bundle.

## Checkpoint inspection

Each file loads with PyTorch's `weights_only=True` mode as an `OrderedDict`
with 98 state-dict entries. No optimizer state, raw trial, participant
identifier, or other dataset record is embedded in the checkpoint.

| Stroke | File | SHA-256 |
|---|---|---|
| Backhand | `backhand_transformer_ae.pth` | `9421ccb0a0714d82b9995abe5f76988ab18c6dd5c8a265cb3000935d8e825df4` |
| Forehand | `forehand_transformer_ae.pth` | `231e84b692e58c1e801f0220a75536b958982ed3d275b021f2b40c26b70bcb63` |

## Reproduction environment

The byte-identical verification used Python 3.9.20, PyTorch 2.5.1+cu118,
CUDA 11.8, cuDNN 9.1, and an NVIDIA RTX 3090. Other supported environments
should reproduce the reported metrics but are not guaranteed to reproduce
identical checkpoint bytes.

## Limitations

- Retrieval measures proximity within the retained reference database; it does
  not establish that a selected person or trial is an ideal pedagogical target.
- The historical runtime independently shuffled search and save arrays, so the
  displayed record could differ from the nearest latent record. The corrected
  public matcher fixes indexing deterministically and is explicitly marked as
  a post-study correction.
- Performance was evaluated for the documented badminton strokes and should
  not be generalized to rehabilitation or other movement domains.

## License

The two distributed model-weight files are covered by the repository's MIT
License. The training source remains available under its original CC0 terms.
See `../README.md`, `../LICENSE`, and
`../unity_project/THIRD_PARTY_NOTICES.md` for the boundary between
first-party materials, third-party dependencies, and excluded N=12 study
records.
