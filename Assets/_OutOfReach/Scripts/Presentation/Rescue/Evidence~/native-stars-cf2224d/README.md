# R20 finale stars on the R42 ending: native evidence

Base: `codex/nasa-2330-staging@cf2224d`; code under test: `edac5fa` (one commit on top). The saved Bootstrap is used
exactly as committed in the base: the rescue ending is already installed, the installer was **not** re-run and nothing
was patched. Unity 6000.3.28f1, `-batchmode -force-device-index 1`, real GPU, 1280x720 captures.

Run `20261115T203153Z-stars-cf2224d`: **59/59 checks PASS, 0 not passed, runtime errors: none, verdict PASS.**
`editor-environment-errors.txt`, when present, holds `UnityEditor.Search` indexing exceptions from editor start-up;
they have no OutOfReach frame and are not part of the game. `tests.txt` has the EditMode and PlayMode summaries.

Shortcuts, as in the R42 evidence: real Practice UI, marine W boarding, seat E, real departure and arrival; then the
domain impact and repairs, then Core's own `Director.TryEndMission(Recovered)`. After that only the real Core chain
runs: `RecoveryArrival` (rescue presenter) -> `MarsDeparture` (existing presenter) -> Report.

## What the captures show

| Capture | Sky in frame | Star-like dots >= 24/255 |
|---|---|---|
| `01`..`04` cabin window | yes, through the porthole | 93, 105, 64, 82 (R42 evidence at 0.15: 9 and 7 on `01` and `03`) |
| `05`..`08` aft docking camera | yes, most of the frame | 494, 332, 338, 325 (R42 evidence at 0.15: 9 on `05`) |
| `09`..`12` airlock | **no**: an interior with no opening to space | not applicable (the counts there are lit interior detail) |
| `13` MarsDeparture | yes | 325, against 1 in `13b` (same moment, saved exposure 0.15) |
| `14` report | no (cabin behind the report) | the saved material is back at 0.15 |

`sweep-docking-camera-exposure-*.png` is the docking-camera framing rendered with a QA-only probe copy of the saved
material: 0.15 -> 10, 0.30 -> 207, 0.45 -> 459, 0.60 -> 744, 0.80 -> 931. 0.15 is what the build showed before; 0.45 is the existing `NasaDeepSky_DeepView.mat` preset that
the presenter uses.

`star-dots.txt` has every count; `star_dots.py.txt` is the counter. It counts isolated 1-3 px local maxima on a dark
surround. It is a comparison between captures of the same framing, not photometry: the few dots it finds at 0.15 are
lamps and interface specks, not stars.

## What was checked about the sky itself

- Before the ending: `RenderSettings.skybox` is the saved `NasaDeepSky.mat` at exposure 0.15, and the camera clears to
  the skybox.
- First frame of the ending: a runtime copy with the same shader and the same `starmap_2020_4k` texture is up.
- Every shot from 3.0 s on: exposure 0.45, including after a real pause and resume.
- MarsDeparture: still 0.45.
- Report: the saved material asset is the skybox again and still reads 0.15.
