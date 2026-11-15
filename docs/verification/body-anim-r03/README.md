# R03 walk / R06-R21 carry — CLAUDE-MAX body-anim lane

Branch `feature/claude-max-bodyanim`, base `f589bb0` (`origin/feature/visuals-cinematics`).
Built on Apoliak's delivered work (credit stays with Apoliak7777):

| Order | Commit here | Source | What |
|---|---|---|---|
| 1 | `c8d546f` | `feature/carry-hands-v1@9c80e53` | The body's own arms hold the preflight crate (WORK and EVA) |
| 2-4 | `8939f7f`, `e594b75`, `94e86e3` | `feature/reference-suits-v04@9c66ed2..f73c784` | V04 WORK/EVA suit assets (own folder only, **not wired**) |
| — | already in `f589bb0` | `feature/body-anim-v1@a7db527` | Gait, look-down, berth rest (Motion/ identical to a7db527) |

`d901fc5` is excluded. `33295a0` (Bootstrap unload in a test) was already in `f589bb0`.

## Tools

- `Assets/_OutOfReach/Tests/BodyAnim/GaitQualityCaptureTests.cs`: EditMode harness on the real WORK/EVA body
  (I06BodyAuthoring) with an uneven 35-145 fps frame time. It measures pelvis bounce, knee bend, planted-foot slide
  and steps per second for carry, walk, sprint, backward, strafe and diagonal. With `OOR_BODYANIM_OUT` set it writes
  `*_gait_quality.txt` and first-person / side frames.
- `native-qa/WalkNativeQa.cs`: real saved Bootstrap, Practice through the real UI, real keyboard and mouse input.
  It walks forward, back, strafe, sprint and stop, then picks up a crate and walks with it. Every frame it logs the
  body presenter's gait state and the world slide of each planted foot (`frames.tsv`) and captures eye and side frames.
  Copy it with its asmdef to `Assets/_WalkQa/Editor/`, run `-executeMethod WalkNativeQa.Run` without `-nographics`,
  then delete `Assets/_WalkQa`.

## Result (`8dc942d`, verified in an own Unity 6000.3.28f1 Editor on this worktree)

Cause found with the real-Bootstrap QA on `f589bb0`: the gait asked for steps longer than the leg reaches. The pelvis
crouched 16-22 cm, and once the drop cap was hit the IK fell short and the planted foot dragged (the skating).

| Real Bootstrap, real input | f589bb0 (+carry-hands) | after `8dc942d` |
|---|---|---|
| forward 2.5 m/s: worst planted slide | 307 mm | 0.9 mm |
| sprint: worst planted slide | 425 mm | 1.0 mm |
| carry walk: worst planted slide | (not reached) | 0.3 mm, wrist shortfall 0.000 m |
| pelvis range while walking | 16-22 cm | 7 cm |
| cadence forward / carry | — | 156 / 140 steps/min |

- `evidence/native-before-f589bb0.txt`, `evidence/native-after.txt`: per-pass summaries (the 1.6 m figure in the
  before strafe pass is a hitch frame from the QA's own captures; the after run measures with an undisturbed 60 fps loop).
- `evidence/{work,eva}_gait_quality.txt`: harness table (uneven 35-145 fps) for carry, walk, sprint, back, strafe,
  diagonal, reversals.
- `evidence/native-after-sheet.png`: side view walking, steep look-down walking, carry side and carry first person.
- EditMode `OutOfReach.Tests.BodyAnim`: 29/29 passed (includes Apoliak's 26 body tests).

## Known limits

- Strafe and backwards use a quicker step (200-260 steps/min at 2.5 m/s) because a sideways or backward leg reaches less.
- In the real run one strafe/curve pass still showed a single 24-34 mm planted-foot drag right after a sharp change
  of direction (forward, sprint, back, carry, turn in place: below 2 mm).
- "Two dark blobs" when looking down: at 65 deg the dark FitV02 WORK shoes show at the bottom edge of a dark deck.
  That is the suit material/lighting, not the animation. The V04 suits (lighter, textured) are cherry-picked as assets
  but are **not wired**: that needs the suit decision (Alex + Apoliak) and an AvatarPresentationProfile change.
- PlayMode suites and a full mission playtest were not run on this branch.
