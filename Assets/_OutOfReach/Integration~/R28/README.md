# R28 / R09 integration hooks (issue #11)

Nothing here is compiled by Unity (`~` folders are ignored). All runtime changes already live in the owned
presentation paths; this folder only holds the root-owned pieces.

## R09 — Earth in the departure shot

`Scripts/Presentation/Cinematics/Preflight/DepartureTransitSky.shader` was rewritten in place. Same shader name and
`_DepartureAltitude` property, so `I06MarineBindings` and Bootstrap need no change. Optional new properties
(`_Exposure`, `_SunDirection`) have defaults; the shader reads URP's `_MainLightPosition` when present.
Cinematic shortcut, not an ephemeris: see the header comment for assumptions and the exposure budget.

## R28 — damaged-power flicker + LED breathing

`ShipLightingPresenter` gains `SetReducedEffects(bool)` (fail-safe default: reduced, so nothing flickers until the
composition publishes the player's setting), `ConfigureIndicators(...)` and a serialized `indicatorSlots` list.

1. Apply `I01MissionComposition.R28.patch` (two one-line calls; `git apply --check` passes on f589bb0). Without it the
   presenter behaves exactly as before.
2. Optional LED breathing: move `R28ShipIndicatorInstaller.cs` into `Scripts/Composition/Editor/` and run
   `OutOfReach.Composition.Editor.R28ShipIndicatorInstaller.Apply` (binds A7_LedRed and A7_LedAmber slots under
   `ShipVisualAnchors`). Without it only the lights flicker.
