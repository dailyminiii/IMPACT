# Third-party asset audit and redistribution boundary

**Audit date:** 2026-09-16  
**Release policy:** only first-party IMPACT code, released weights, public
reference data, and dependencies whose license is present and permits this
form of redistribution may be committed. A public repository must not contain
raw Unity Asset Store packages or assets with unverified redistribution rights.

## Direct scene dependencies removed from the public tree

| Asset family | Direct use in `IMPACT_Main` | Release treatment | Installation route |
|---|---|---|---|
| Noitom / Perception Neuron Unity SDK | live motion source and `NeuronInstance` base classes | Not redistributed: the referenced public repository has no explicit license file at the pinned revision. | `tools/install_noitom_sdk.ps1` clones the vendor-maintained public repository at `7045677fd1e9766fb7798b9bc44dbc8adb70403b`. Review the vendor terms before use. |
| Graph And Chart, BitSplash Interactive | replay area plot, RMS bars, chart prefabs and chart types | Not redistributed: Asset Store product source and its separate bundled notices do not license redistribution of the package itself. | Acquire/import a compatible Graph And Chart edition from the original provider/Unity Asset Store before opening the scene. |
| Friki Studio Human Male Anatomy | muscle overlay model and material | Not redistributed: commercial Unity Asset Store content. | Acquire the original *Human Male Anatomy* package from Friki Studio, or replace the missing object with a user-owned anatomy model. |
| Mixamo character and Perception Neuron stickman | replay-expert and user-avatar visuals | Not redistributed: public use of a character is not equivalent to redistributing its raw FBX in a source repository. | Obtain a compatible Mixamo avatar directly from <https://www.mixamo.com/#/?page=2&type=Character>; obtain the Noitom stickman through the Noitom installation route or replace it with a user-owned humanoid. |
| Badminton court, racket, and shuttle | contextual scene geometry and racket visual | Not redistributed: source and redistribution permission were not documented locally. | Replace with a user-owned court/racket/shuttle or obtain the original asset through its original store/provider. |
| MapleStory/TMP legacy asset folder | text-font assets referenced by UI labels | Not redistributed: it contains a non-default font and legacy resources without a release notice in this project. | Install TextMeshPro through Unity Package Manager and import a compatible, appropriately licensed font; the default Unity TMP font is sufficient for inspection if the original typography is not required. |

The Unity Asset Store terms make Asset Store content subject to the Asset Store
EULA or a provider-specific license; retaining raw store packages in this source
repository would not be an appropriate assumption. The direct asset folders
above are therefore excluded even though they were used in the private
experimental project.

## Retained dependencies

| Dependency | Reason retained | License/handling |
|---|---|---|
| Unity / TextMeshPro package references | required engine packages | resolved by the recipient's Unity installation; no package cache is committed. |
| MRTK/OpenXR package archives under `unity_project/Packages/MixedReality` | project configuration depends on pinned package archives | retain their embedded license files and do not sub-license them under the MIT license. |
| SimpleJSON | indirect dependency within the excluded chart package | not redistributed by IMPACT; its upstream MIT notice remains relevant only if a user installs the chart package. |
| MultiSenseBadminton-derived expert reference data | supports automatic matching and replay | retain the upstream dataset attribution and license in `reference_data/README.md`. |

## Verification checklist before publication

- [ ] The six excluded raw asset directories are absent from the Git index.
- [ ] No `.csproj`, `Library/`, `Logs/`, `Temp/`, or `UserSettings/` content is staged.
- [ ] `git check-ignore -v` confirms that local recordings and dependency folders are ignored.
- [ ] `tools/verify_unity_project.ps1` passes after a clean clone and documented dependency installation.
- [ ] The repository release includes a short video of the system operating with licensed assets; the video is demonstrative, not a redistribution of the raw assets.
- [ ] `CITATION.cff`, the paper availability statement, and the response letter contain the final GitHub URL and Zenodo DOI.
