# CLAUDE-GAMEPLAY-REVIEW-03: movement, input and the storage approach

Read-only review for root's request on issue #7 (comment 6892010880), 15 November 2026, about 09:10 UTC. Reviewed
revision: `codex/i01-integration` @ `cb80965` (runtime `ed1a4c5`). No runtime, scene or test file was changed. Line
numbers refer to that revision.

## Verdict

**No Player or Core defect was identified on the route repair → EVA → reply → storage/survival.** The storage stall in
issue #9 (native run `20261115T065840359Z`) matches root's own evidence in `i06-next-steps-2026-11-15.md`:
releasing WASD and the mouse together discarded the steering during the two-button recognition window. That is in
the input bindings, which root owns; `8056815` addresses it. Nothing in Player or Core blocks the storage approach.

## What was checked

| Contract | Where | Result |
|---|---|---|
| Left/right hand push | `FirstPersonController.TryPushOff` (line 398) | Solid non-trigger collider, reach 0.85 m from the eye, line of sight, direction away from the hit normal by at least `MinimumAwayDot`; one shared cooldown. Correct. |
| Both hands | `TryPushOffPair` (imported as `dccbf61`, `5d8dcaf`) | Atomic: both contacts are verified before any change; the sum is capped to one full push; one cooldown and one `Pushed`. Root's two-contact wiring is still pending. |
| Space, foot push from the deck | `TryPushFromFootContact` | Needs real support within 0.05 m under the feet with normal·up ≥ 0.8, and the direction away from it. It works from the deck only, by design; a foot lifted off the deck refuses, which is correct. |
| Entering float | composition `MaintainMicrogravity` → `EnterFloatFromFootSupport` | Entry only from real foot support; this covers the spawn and the EVA return (feet in the airlock). |
| Teleports | `Teleport` (line 252) always ends float; `MissionRuntime` lines 602 and 629 | Teleports happen only at session start and restart, followed by entry from the deck. Nothing teleports mid-route. |
| Control leases | `CanUseMicroContacts` (composition line 223), `CanUsePlanningControls` (line 429) | Pause, menus, route, outside, cinematics, rest, carried cargo, outcome and Planning gate the contacts; nothing gates Survival. |
| Earth terminal lease | `I06EarthTerminalBindings` lines 157–162 | The terminal exits, releasing input, on pause, cinematic, **phase change** and mission end. The reply's Transmission → Survival change therefore releases it before the survival leg. |
| Interaction reach | `InteractionResolver` (line 27): 2 m from the camera | A target becomes usable once the floating player stops within 2 m and looks at it. The push reach (0.85 m) is separate. |

## Latent risk, not a defect today

`FirstPersonController.ClearMotion` (line 384) stops the drift without checking that any surface is within reach. In
float there is no thrust, and `IsStranded` is diagnostic only, so a call mid-air would strand the player. At
`ed1a4c5` **nothing calls it** (no runtime caller outside Player). If a future rest, body lease or cinematic hand-over
needs it, call it only where a surface is in reach, or keep the drift. Until then no change is proposed.

## Not covered

The saved Bootstrap geometry around the storage targets, root's input bindings, bridge and hand presentation, and any
live-editor run. These are root-owned or need a native run.
