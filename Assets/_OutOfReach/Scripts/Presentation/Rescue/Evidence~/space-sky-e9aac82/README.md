# SPACE-SKY evidence: NASA stars during the EVA and in the Earth-orbit shot

Code commit: `2251766` on `feature/apx-space-sky-2315` (base `feature/claude-max-final@5479e87`).
The native run and the first PlayMode run were made on `e9aac82`; `2251766` is that commit plus the README section
and the lane's author line (`git diff e9aac82 2251766` touches `Rescue/README.md` only). The second PlayMode run is
on `2251766` itself. See `tests.txt`.

## How the pairs were made

One native run of the full mission (QA-E2E driver, GPU batch mode, saved Bootstrap, 1280x720). At each listed
capture the driver set `SpaceSkyKeeper.Enabled = false`, waited two frames, saved `BEFORE`, set it back to `true`,
waited two frames and saved `AFTER`. `BEFORE` is therefore exactly what the release candidate shows (saved sky at
exposure 0.15); `AFTER` is this commit (runtime copy at 0.45). The two frames of a pair are about ten frames apart,
so anything that moves (hands, the capsule, alarm lamps, the mission clock) differs slightly between them.

- `overview-before-after.jpg`: orbit shot and four EVA points, before on the left, after on the right.
- `pair-*.jpg`: every pair at full size. `pairs.txt`: star-like dots and pixel differences per pair.
- `sky.txt`: at every capture the active skybox, its exposure, the keeper state, whether the rescue ending holds a
  copy of its own, and the ambient / reflection / fog / sun settings with the ambient probe.

## Result

Run verdict (`run-verdict.txt`, `checks.txt`): **167 PASS / 4 SOFT-FAIL / 0 FAIL, 0 game errors**, 93 captures,
12 min 42 s. The driver writes its verdict file as FAIL whenever a soft check failed; the four soft checks are the
known ones of the QA-E2E baseline (three navigation legs done by QA teleport, one hand push refused at
`COL_Airlock_SuitStation`) and none concerns the sky.

| Frame | Star-like dots before | after |
|---|---|---|
| Earth-orbit shot (`08-orbit`) | 1 | 318 |
| EVA, just outside the hatch (`18-eva-outside`) | 6 | 389 |
| EVA, end of the rope route (`19-eva-route-end`) | 2 | 345 |
| EVA, at the antenna (`20-antenna-work`): the panel fills the frame, no sky in view | 3 | 3 |

- Launch site on Earth: the skybox is `MAT_LaunchSky_RockyRidge` (0.65) before and after, the keeper does not hold
  (`00-menu`, `02-boarding-walk`, `04-departure-countdown`: 0.00 % changed pixels, `07-climb`).
- Interior, static frames: `16-suit-on` 0.02 %, `23-after-eva` 0.02 %, `10-arrival-float` 0.18 % changed pixels
  (the two cockpit windows and the mission clock). Lighting of the cabin and of the hull is the same in each pair.
- Ambient mode, ambient colours, ambient intensity, reflection intensity, fog, sun and the ambient probe read the
  same before and after in every pair (`sky.txt`).
- Rescue ending (R42): all 16 frames and the following `MarsDeparture` show the keeper's copy at 0.45, the
  presenter's own copy is never created (`endingRaised=False`), every R42 check passes, then Report "SURVIVED".
- Restart ("Plan again"): launch site sky again, keeper not holding, `80-restart-menu`.

## Not covered

- A Windows player build was not made in this lane.
- Mid-rope: the driver captures the hatch, the end of the rope route and the antenna, not a point halfway.
- `engine-errors.txt` is one Unity Search indexing exception of the editor at startup, not game code.
