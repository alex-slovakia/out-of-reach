# SLEEP-SEQ: physical berth sleep sequence and the berth exit (native evidence)

This answers Alex's requests on #7: 6896428491 (the physical sequence) and 6896954664 (wake at the berth, not at the R spot). In backlog #11 these are **R34** and the berth-exit part of **R18**.

## What happens now

| Phase | What the player sees |
|---|---|
| Entering: approach | The view floats from the live eye to the berth mouth (0.62 m in front of the berth eye), turns round and backs into the bag. Camera and body move as one first-person rig. A rest that starts far from the berth (R pressed elsewhere) starts at the berth exit instead, a cut that the routine montage covers. |
| Entering: strap | The view looks 60° down at the chest. The right hand reaches for the restraint and draws the chest strap, then the hip strap. The limbs settle into the supported bag pose. |
| Held | The view settles to 18° down. Only now do the survival UI eyelids close and the rest overlay appear. During the R routine the body stays held across its nights and waking hours, with one entry and one release. |
| Releasing: ordinary wake | The eyes open in the berth (0.5 s). The hand unclips the chest strap, then the hip strap (0.95 s), still inside the berth. The view then floats out along the berth axis to the berth exit. |
| Berth exit | The composition moves the real controller to 0.30 m in front of root's `SleepEntry`: upright, facing into the cabin, feet on the deck. It uses `Teleport` + `SyncTransforms` + `EnterFloatFromFootSupport`, so the player is back in float at rest with no gravity. The lease hands the view over in the same frame, so nothing pops back to where the rest or R began. Normal controls return. |
| Releasing: hazard wake | On CO2, O2, food, water, power or mission end, the straps spring open within 0.25 s and the view quickly floats out to the berth exit. |

Core `IsResting` / `LastRestStop` (plus `ChapterFlow.IsRoutineRunning` for the hold) stay the only authority. The sleep simulation, schedule and input bindings are unchanged. The controller is moved only once, at the very end, through its own API.

## Files

| File | Change |
|---|---|
| `Assets/_OutOfReach/Scripts/Presentation/Motion/BerthApproachPath.cs` | New. Pure path math. |
| `Assets/_OutOfReach/Scripts/Presentation/Motion/EmbodiedBodyPresenter.cs` | Adds `TryGetRestView`, `RestBlocksControl`, `RestExitPending`, `TryGetRestExit` and `ConfirmRestExit`. `ConfigureRest(…, holdRest, berthExitAnchor)`. |
| `Assets/_OutOfReach/Scripts/Presentation/Motion/Integration~/` | Composition change for the integrator: patch against `ae6380f`, anchor script and README. |
| `Assets/_OutOfReach/Tests/BodyAnim/BodyFrameCaptureTests.cs` | Lease-driven sequence, hazard, routine hold and berth-exit tests. |

## Evidence

### `run6-89414dd/`: native run at the delivered SHA

This run used the actual saved Bootstrap of `codex/nasa-2330-candidate` plus this branch, in Unity 6000.3.28f1 GPU batch mode with a real virtual keyboard and mouse. The route went through the real startup cards → Practice → boarding → departure → arrival → impact. `smoke/` holds the launch → arrival smoke frames.

**Rest A (ordinary rest)**
- A real pause during the approach froze the playhead, camera and body (0.4 mm).
- The camera never jumped: at most 0.92 m/s of sequence time and 150°/s, with the largest single-frame step 0.062 m.
- The body reached the berth frame (0.000 m), with both straps buckled during the hold.
- The view stayed in the berth while unbuckling, and the straps were still drawn there.
- **The player woke at the berth exit: 0.000 m and 0.0° from (−1.330, 0.030, 13.030), facing +X into the cabin, floating at rest with v = 0.000.** That is not where the rest began, (−1.28, 0.03, 13.90).
- Normal controls returned, and a real Space push carried the player 2.25 m away.

**Rest C (hazard)**
- Hazard trigger (disclosed): Core's own `StopRest(CarbonDioxideDanger)`, called by reflection during the hold.
- The straps opened in 0.14 s, and the quick float out to the berth exit ended at 0.45 s.
- Input returned with the move (0.45 s).
- The wake was exactly at the exit.

**Rest D (restart):** a restart from the real pause menu during the approach left no lease and no straps, an identity pivot and a session that is not resting.

**Totals:** 0 game errors. The 2 soft failures are disclosed: the QA teleport next to the berth after 16 s of real float pushes, and hazard input at 0.45 s instead of 0.3 s.

Files: `A-sequence-eye.jpg` / `A-sequence-aisle.jpg` (the whole of rest A), `C-hazard-*.jpg`, key frames, `checks.txt` and `shortcuts.txt`.

### `run1-aec862b/`: native run in the actual saved Bootstrap at `aec862b` (GPU batch, real virtual keyboard and mouse). The route was Practice → boarding → departure → arrival → impact, then a disclosed QA teleport next to the berth and a real E. This run predates the berth exit, so the exit there returns to the player's own eye.
- `A-sequence-eye.jpg` is the player's first-person view. `A-sequence-aisle.jpg` comes from an unsaved QA camera in the aisle that watches the body; the first-person body has no head by design.
- It shows the approach, strapping, hold with eyelids, real pause, wake, unbuckling in the berth, exit and push-away.
- `B-co2-danger-wake-aisle.jpg` shows a real CO2-danger wake before the repairs.
- `checks.txt` lists every check.
