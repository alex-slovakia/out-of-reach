# Cinematic hold-to-skip

Hold **Space** or **Enter** for 0.6 s to skip the departure flight. Same gesture and length as the rescue ending.

New files only. No installer, no scene change, no composition change: `CinematicSkipInput` installs itself with
`RuntimeInitializeOnLoadMethod`, finds the scene's own `I06MarineBindings` and calls the `Skip()` it already has.

| Scene | Call | Covered |
| --- | --- | --- |
| Transport departure and transfer flight (about 34 s) | `I06MarineBindings.Skip()` | yes |
| Rescue ending (`RecoveryArrival`) | its own hold in `RescueEndingPresenter` | untouched |
| Core `Impact` | none | no: the player keeps control during it, Space is push-off |
| `FalseSignal`, `CommunicationRestored`, `MarsDeparture`, `EndingPullback` | none | no, see below |

## When it is live

Only while the departure plays **and** the player's control is locked (`FirstPersonController.ControlAllowed` is
false). Never in gameplay (Space is push-off there), never while the mission clock is paused, never before a mode is
chosen (startup cards, mode menu), never during the rescue ending.

A hold must start inside the cinematic: a key that is already down when the scene starts (held from gameplay, or the
same hold that just skipped another sequence) has to be released first. One hold skips one sequence.

The label "Hold Space to skip" with a thin progress line sits in the flight board's subtitle layer, bottom right. It
shows for the first 4 s of the departure and again while a key is held.

## Fail open

- No `MissionRuntime` in the scene: it waits quietly.
- No marine bindings found: one warning, then it stays off for that runtime.
- Any exception: one warning, the component disables itself, the departure plays to its end as before.
- No subtitle layer: the hold still works, there is just no label.
- `CinematicSkipInput.Enabled = false` turns everything off.

## Not covered

- `Impact` (4.5 s): in the native run the player has control while it plays, so a held Space there is gameplay input.
  Skipping it would process the same key twice.
- `FalseSignal`, `CommunicationRestored`, `MarsDeparture`, `EndingPullback`: not checked in a native mission run
  before the freeze. `MissionCinematicPresenter.SkipActive()` exists for them and still has no input caller.
