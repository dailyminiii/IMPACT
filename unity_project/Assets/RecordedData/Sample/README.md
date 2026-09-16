# Public recorded-swing sample

`Sample_Session100.csv` is an example recorded swing for checking the replay
pipeline. Its motion and rotation fields are retained from the public sample;
its first 16 EMG fields and two MVC files are labelled synthetic public-demo
values derived from the AutoEncoder-selected expert replay. They are not
participant data. `Sample_Session100_synthetic_emg.json` records the exact
generation rule.

When the feedback button is pressed, the stroke-specific AutoEncoder service
searches the released five-expert reference database from this swing's motion
and writes exactly one matched `Sample_Session100_expert_SubXX.csv` file here.
Unity then replays that newly selected sequence.

Any generated `*_expert_SubXX.csv` file is a local retrieval output, not a
fixed scene asset and not a user-study record.
