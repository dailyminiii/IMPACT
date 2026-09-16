# Expert MVC: public replay calibration reference

These eight-row files are the channel-wise calibration reference used by the
public Unity replay prototype. This is the sole expert MVC calibration set
included in the public demo.

## Extraction protocol

For each of the five experts (Sub11, Sub14, Sub19, Sub20, and Sub24), values
were derived only from recorded calibration events:

- lower arm: the recorded `Lower arm inward` and `Lower arm outward` trials;
- upper arm: the recorded `Upper arm inward` trials.

Trials with fewer than two raw samples are excluded as acquisition gaps rather
than interpolated as physiological data. This affects one of Sub24's four
labelled upper-arm trials; its remaining three valid trials are retained.

Each trial was interpolated to the replay processing rate (50 Hz), passed
through the same causal 10–20 Hz second-order Butterworth filter as the public
replay, full-wave rectified, and converted to a trailing 200 ms RMS envelope.
The first and last 0.5 s of each trial were excluded. For every channel, the
largest trial-level 95th-percentile RMS was selected. This prevents a single
sample from determining the display scale while retaining the strongest
sustained calibration response.

The source HDF5 recordings are consent-restricted and are not included in this
release. `tools/rebuild_expert_mvc.py` regenerates these values locally when
authorized source recordings are available.
