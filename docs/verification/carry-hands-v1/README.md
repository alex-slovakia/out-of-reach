# CARRY-HANDS-V1 — the body's own arms hold the preflight crate

Alex's playtest (#7 6896664694): "hands floating in the air while carrying the crate".

**Cause (aec862b).** While a crate is carried, the composition hides the first-person body's arms
(`BodyArmMask()` returns `None` while `PreflightCargoHandsPresenter.IsVisible`). A separate pair of work gloves with
short sleeves then holds the crate 0.9 m in front of the eye, and their sleeves end in mid-air
(`frames/before-gloves-*.png`, captured in the same run).

**Change.** Presentation only, `Assets/_OutOfReach/Scripts/Presentation/Cargo/`:

- **`PreflightCargoHandsPresenter.cs`**
  - When the first-person body is shown, the presenter keeps the separate gloves hidden, so the existing arm mask shows
    the body's own arms (WORK or EVA).
  - After the body presenter runs, it places the crate level in front of the body's shoulders, inside the arms' reach,
    and both arms hold its side handles.
  - Without a visible body, the old gloves are used as before.
  - It finds the scene's `EmbodiedBodyPresenter` itself. `ConfigureBody(body)` is also available. **No Composition or
    scene change is needed.**
- **`CargoCarryArmsDriver.cs` (new)**
  - The driver is a component added at runtime with a LateUpdate at execution order 265, after the body presenter (200)
    and its gait (250).
  - `CargoCarryArms` holds a per-frame lease on both arms:
    - a two-bone reach with the elbows low and out;
    - an underhand side grip, with the fingers wrapped over the handle tube;
    - half of the wrist roll moved into the forearm.
  - Every bone is restored on release and on `PoseWillReset`, with the same pattern as `MealHandClasp`.
  - The first-person camera sits about 0.37 m in front of the body's shoulder joints. While carrying, the shoulder
    joints therefore reach 0.22 m forward. The stretch stays behind the camera; the visible forearms and hands keep their
    proportions.
- **`OutOfReach.Presentation.Cargo.asmdef`**: now references `OutOfReach.Presentation.Motion`.

Input, movement (including carry speed), physics, inventory and domain state are unchanged.

## Verification (native)

- Setup: Unity 6000.3.28f1, RTX 4070 (D3D12), own clone at `aec862b` + this change.
- Tool: `native-qa/CarryHandsNativeQa.cs`. Copy it with its asmdef to `Assets/_CarryQa/Editor/`, run
  `-executeMethod CarryHandsNativeQa.Run` without `-nographics`, then delete the folder.
- Sequence: the actual saved Bootstrap, Practice through the real UI, a walk to the water-tank pallet with real input,
  then E. After that: the old gloves for comparison, then the body's arms. The run looks 0/15/30/50° down and walks,
  first in WORK, then in EVA. For EVA the suit-on flag is set by reflection; that is a presentation check only.

`checks.txt` passes in both suits:

- The crate is shown and the separate gloves are hidden.
- Both body arms are visible.
- Both wrists reach their handle targets, with a shortfall of 0.000 m in every sample.
- No runtime errors.

`observations.txt` has the eye-local positions of the wrists, shoulders and grips for every frame.

Frames:

- `frames/work-*.png`, `frames/eva-*.png`: look views and a walking series.
- `frames/*-side.png`: a third-person check camera that shows the arms running from the body to the handles.
- `carry-hands-v1-sheet.png`: summary sheet.

## Limits (honest)

- The crate now sits closer to the camera, because it must be within the arms' reach. Looking straight ahead, its lid
  is at the bottom of the view. The hands show from about 15° down, and while walking with a slight downward look.
- In EVA the grey gloves sit under the lid edge and are hard to see from above. The side check camera shows that they
  are on the handles.
- The carried visual's dark cradle under the crate is unchanged.
- EditMode/PlayMode suites were not run. No test files changed; the preflight play tests do not use this presenter.
