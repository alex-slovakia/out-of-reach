# R32 pause controls pictograms: composition hook

The pause "wall of controls text" lives in the composition-owned `I01SessionControls.cs`, so it is delivered as a patch.

Apply from the repository root (checked against f589bb0; touches only `Scripts/Composition/Runtime/I01SessionControls.cs`):

    git apply Assets/_OutOfReach/UI/Integration~/I01SessionControls-pause-pictograms.patch

What it does: replaces the `controlsHint` Label with a `PauseControlsView` card added to the full-screen pause `modal`
(right side, beside the menu panel). `SetControls(mode, railGrip, PlayerInputActions.Map)` is driven by the same
floating / on-route / rail-grip conditions the text hint used. `ControlsHintFor` and its constants stay (existing
`PauseControlsHintTests` keep passing; they are the plain-language contract).

No scene, prefab, input asset or style-sheet wiring: `PauseControlsView` loads `UI/Resources/PauseControls.uss` itself
(`Resources.Load("PauseControls")`). If the sheet is missing the card still builds, just unstyled.

Rows read the live `InputActionMap` (Move, Interact, Sprint, Pause, LookPointer by action name, effective paths so an
override would show). Inputs that are not Input System actions are read from the devices in code and are named once
in `PauseControlsModel`: Tab (MissionFlightBoard, hold), and in microgravity the mouse buttons, Space and Ctrl
(I06MicrogravityInputBindings). If either of those changes, update the constants there.

Not included (the issue's "Later" items): Controls button, rebinding, persistence, conflict handling.
