# SLEEP-SEQ: integration of the physical berth sleep sequence

This is for the integration assembler, the one Composition and scene writer.

**Scope:** composition only. This change has no Bootstrap edit, no new scene object and no Core change. The berth references are already in the scene from set 4: `SleepBodyFrame`, `sleepBodyFrame` and `sleepCameraAnchor`.

## Steps

### 1. Merge `feature/sleep-seq`

It touches only:
- `Scripts/Presentation/Motion/**` (presenter and the new `BerthApproachPath`)
- `Tests/BodyAnim/BodyFrameCaptureTests.cs`
- `docs/verification/sleep-seq/**`

### 2. Apply the composition change to `I01MissionComposition.cs`, either way

- **Patch:** `git apply --3way Assets/_OutOfReach/Scripts/Presentation/Motion/Integration~/I01MissionComposition.sleep-seq.patch`. It was generated against `claude/integration@d901fc5`.
- **Anchor script:** `python -I Assets/_OutOfReach/Scripts/Presentation/Motion/Integration~/apply_sleepseq_composition.py Assets/_OutOfReach/Scripts/Composition/Runtime/I01MissionComposition.cs`. It uses exact anchors and refuses to run twice.

### 3. Compile and run `OutOfReach.Tests.BodyAnim`

## What the composition change does

- **`UpdateRestCamera`:** instead of cutting the view onto `SleepCamera`, it passes the live gameplay eye (where the camera would be without the lease) to `EmbodiedBodyPresenter.TryGetRestView`. It then lands the actual camera on the returned view, keeping every offset below the pivot as before. The view glides along this path:
  1. from the eye to the berth mouth;
  2. turns round and backs into the bag;
  3. looks down at the hands strapping in;
  4. settles;
  5. after the wake it unbuckles in the berth and floats back out to the live eye.

  Pivot restore and the release triggers are unchanged: Releasing end, stop, session replacement, mission end, cinematics, unbind and disable.
- **`RestCameraWanted`:** returns `embodiedBody.RestPhase != RestPhase.None`, so the lease is held through `Releasing`. Before this change the view was handed back at the first frame of `Releasing`, so the unbuckling was never seen. All other gates are kept. `RestPitchDegrees` is removed because the pitch now lives in the presenter's path.
- **`CanUsePlanningControls` and `CanUseMicroContacts`:** each gets `&& !(embodiedBody && embodiedBody.RestBlocksControl)`. Gameplay input waits while the body is in the sequence:
  - **Ordinary wake:** about 3 s for eyes open, unbuckle and float out.
  - **Hazard wake:** the straps spring open and input is back after 0.25 s. The quick float out then finishes under player control.
- **Rest presentation owner:** the survival UI's eyelids, rest overlay and notice wait while `RestPhase == Entering`, so the approach and the strapping are seen. The chapter routine owner is kept, and the same hold is configured when there is no chapter UI.
- **`ConfigureRest`:** gets a fourth argument, `() => chapterUi && chapterUi.RoutineOwnsRestPresentation`. The routine montage's nightly rests then show no walk-in into the berth.

## Authority

Core `IsResting` / `LastRestStop` stay the only authority. Nothing here moves the controller, colliders or input bindings, and nothing simulates time or resources.
