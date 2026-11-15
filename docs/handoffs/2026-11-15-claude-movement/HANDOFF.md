# Movement mechanics checkpoint — 2026-11-15

Baseline: live `codex/i01-integration`; exact existing-file SHA-256 values are in `baseline.json`. Source has NOT been applied to live Assets. `source/` contains the exact replacement/new files; `movement-mechanics.patch` is the unified diff; `staged-files.json` lists all seven file hashes. Existing `.meta` files are deliberately absent and must be preserved. New test folder, asmdef and test script include new metadata.

## Changed live paths on application

1. `Assets/_OutOfReach/Scripts/Composition/Runtime/I06MicrogravityInputBindings.cs`
2. `Assets/_OutOfReach/Scripts/Composition/Runtime/I06MicrogravityBridge.cs`
3. New `Assets/_OutOfReach/Tests/MovementIntegration/**` and folder `.meta`.

No Player, Core, Body, Hands, scene, UI, audio, existing test or package edit.

## Diagnosed behavior

Space routed only to `TryPushFromFootContact`; support must face upward (dot >=0.8). It therefore could never push off ceiling/wall. The hand bridge projected eye.up onto the contact plane: a level view against a ceiling produced zero tangent, refusing suited contact even when reachable. WASD intentionally supplied no free-flight thrust. Root configured the default push strength 0.35, giving only0.35m/s per push, with1.5m/s total cap,0.35s cooldown and no drag.

## New behavior

- Space uses actual floor support first; without it, an aimed or nearest reachable surface is selected for the existing visible hand contact bridge. It tries the other hand if the first refuses. Selection itself never applies an impulse: the same reach, occlusion, rig contact and Player gates still decide success.
- A level ceiling contact uses another view axis when eye.up cannot form a stable tangent. Existing bare-hand ceiling-specific pose direction remains intact.
- W/S and A/D aim the push relative to the view. They do not accelerate an unsupported body; momentum still persists with zero drag. This first patch deliberately adds no undisclosed thruster/air-steering force.
- Defaults become0.65 strength (0.65m/s per single push). Player cap remains1.5m/s; paired pushes still obey the canonical cap. Existing explicit nondefault callers remain respected.
- One Space press may wait up to0.4s across cooldown or contact return. It is consumed once. Holding Space cannot auto-repeat. Pause, control/contact denial, disabled map, disarm/fault, a missing keyboard or an explicit mouse contact command clears the queued request. A fresh release/repress is required after gate loss.
- Rail grip must first be released. `LastRefusal` supplies short contact/availability feedback; UI wiring remains root-owned.

## Verification and remaining limits

Static baseline/hash check passed; no live Assets changed and no Unity process launched by this agent. `git apply --check --ignore-space-change movement-mechanics.patch` passed; the flag handles existing mixed CRLF/LF source lines. Root may instead hash-check then copy the exact `source/` files. Native compile/tests are NOT yet claimed. New test filter: `OutOfReach.Tests.MovementIntegration.SurfacePushIntegrationTests` (10 NUnit cases). Run existing `OutOfReach.Tests.PlayMode.MicrogravityLocomotionPlayTests` as the Player regression set.

New tests use actual CharacterController/colliders and Input System keyboard edges. Controlled read-only phase/suit/clock fixture values isolate the adapter. Reflection exposes private adapter steps synchronously; it does not forge contact impulses. Wall/ceiling tests prove real hit/frame construction and target selection, not complete hand-mesh animation. Root still needs suited and bare-hand native rig acceptance, both push directions and pause during approach.

No unproven capsule fix was made: existing CharacterController movement sweeps already have floor/ceiling collision tests. The EVA route separately disables CharacterController and writes the route position directly; route clearance and posed body/arm penetration remain unresolved acceptance items. Do not claim all ship clipping fixed by this patch.

UI gap: `FlightBoardView.cs` currently offers only Move + mouse pictograms and a Ctrl+click rail hint. Suggested current copy: `Space · Push off a nearby surface   WASD · Aim the push`. Existing historical verification documents describing foot-only Space should remain historical evidence.

## Native follow-up 11:24 UTC

Root applied the pack. `TestResults/movement-contact-01.xml`: existing microgravity20/20 passed; new tests3/10 passed (geometry),7 key-input cases failed before receiving key edges. The new test fixture omitted hidden-Game-View input routing already used in SprintExertionPlayTests. Fixed ONLY the new test script: temporary InputSettings clone uses IgnoreFocus and AllDeviceInputAlwaysGoesToGameView, restores original settings identity/flags, normal fixture focus-regain notification, and asserts the injected Space device state before dispatch. Staged test copy/hash/diff now reflect the live correction. No production mechanics change from the applied pack. Root rerun pending; do not claim the new tests pass until its result exists.
