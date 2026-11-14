# I06 body and physical habitat candidate — 14 November 2026

This is an integration candidate, not a promoted playable release. The user's original Unity project remains on its last verified checkpoint. The isolated Bootstrap now enables the already-delivered C06 survival chapter and contains its real physical interaction points.

## Implemented wiring

- Imported two original WORK/EVA body FBXs, held standing/floating/seated clips, explicit outfit/session/pause bindings. Grounded camera bob and deck footsteps do not run while Player.IsFloating.
- Imported the original habitat from Claude 5b0f1ca. Replaced only the old storage visuals and two locker boxes, retaining the rest of the ship and original prefab assets.
- Food, water, rest, a single grow-module target that follows actual Core stage, and a cockpit recovery beacon bind to the actual existing commands. No second resource, crop, day or outcome counter. Before Survival those five target colliders are disabled.
- Science-payload salvage is placed at the existing engineering science bay, allowing the default loadout to obtain the part for grow-module construction at the permanent cost of the scientific objective.
- Bootstrap has 28 explicit targets: 8 original mission, 14 cargo, 5 habitat/recovery and 1 salvage. All 28 passed edit-time 2m unobstructed ray checks on the generated candidate scene. One camera/listener/runtime/player; existing scene GUID preserved.

## Evidence actually run

- Unity 6000.3.28f1 source regression after integrated microgravity correction 4a34a6b: 309 EditMode and 50 PlayMode tests, zero failed/skipped. This predates physical habitat wiring.
- Native body fixture: 102 assertions, zero failures, eight actual GPU captures. Controlled readonly suit/clock fixture using the actual ship lighting; not a full gameplay run. The files here preserve its exact source/scene hashes. WORK/EVA standing views are visually usable; floating view is explicitly rejected below.
- Native real-Player contact fixture: second run 179 assertions and six callback-frame contact captures plus two look-down views. Real TryPushOff/TryGrab and exact bare skin/glove pad contact checks. The first run failed static right-hand held presentation; that failure is preserved. Production code was unchanged between runs; a diagnostic-only test change was made. A slow-frame cancellation is being investigated and fixed with a deterministic regression; this is not a clean acceptance yet.
- Habitat BuildAndValidate: exit0, 28 bound targets with edit-time physics queries. Native physical consumption/growth/rest/restart validation remains pending.

## Explicit remaining limitations

Floating is still default-off and has no production input/entry wiring. Its current -0.100m body fit exposes too much neck/chest; the included floating capture is a rejected diagnostic, not approved game art. Current gloves show a rigid forearm cut end at extended grip, and a palm-point residual does not prove all fingers avoid the wall. Corrections are in progress. Body clips are held poses, not locomotion animations.

The habitat currently exposes actions and state; detailed snack/drink/repair animations are separate deliveries. The ocean dock/transport is staged but is not yet this scene's prologue. Recovery and Mars presentation are not a final cinematic sequence. No updated original-editor session, Windows build, full manual start-to-end playthrough or AAA-quality claim is made by this checkpoint.