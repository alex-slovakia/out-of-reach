# Current ownership — 14 November 2026, 16:40 CET

Alex explicitly instructed Codex to take over all unstarted Claude work. This supersedes the previous waiting assignment for the tasks below. Coordination: https://github.com/alex-slovakia/out-of-reach/issues/7#issuecomment-6879310092 . Claude's completed ship, gloves and EVA fix are retained.

| Writer | Exact scope | Delivery |
| --- | --- | --- |
| Codex root | Composition, UI, audio, shared documentation and integration | Verified builds and one shared revision |
| Codex movement agent | `Scripts/Player/**`; Sprint/Exertion tests, own `codex/c06-sprint` checkout | Held sprint, genuine bounded exertion, preserved EVA/collision/pause behavior |
| Codex survival agent | `Scripts/Core/**`, `Simulation/**`, `Inventory/**`; new `Tests/EditMode/SurvivalExtension/**`, own `codex/c06-survival` checkout | Opt-in post-contact survival, separate ration/drinking commands, one time-dependent grow module |
| Codex survival agent, exact root-approved contract exception | New `ISurvivalExtension`, `GrowModuleStage` and appended `GamePhase.Survival` only | Preserve existing interface/enum compatibility |
| Codex terminal agent | Staging for `Scripts/Presentation/Terminal/**` and `Visuals/Props/MissionTerminal/**` | Physical Blender terminal, finger/touch/globe presentation; root owns input lease |

Claude must coordinate before starting another writer on these paths. A posted request is not proof of remote acknowledgement. Nothing here controls Apoliak's computer or bypasses his local approvals.

## Current art writers and Claude return

Alex reports Claude/Apoliak is back and explicitly requests redistribution. Codex's sprint and survival agents have delivered their bounded code; their next tasks are disjoint art:

- Codex body: staging EmbodiedAvatarV01, eventual Visuals/Character/FirstPersonBody/**; torso/legs/workwear/EVA rig, retained existing gloves. Latest generated held-up hand pose is rejected.
- Codex dock: staging CoastalLoadingDockV01, eventual Visuals/Environment/CoastalDock/**; small sea-side loading base and carrier access, no rocket/whole AURORA launch model.
- Codex repair kit: staging EvaRepairKitV01, eventual Visuals/Props/EvaRepairKit/**; delivered original ratchet/hook/braided cable; Unity integration pending.
- Claude requested TERMINAL-ART-V2: only original model/export/material source under docs/art-direction/terminal-v02/, based on published V1 source. Preserve fixed anchors/active display dimensions. No UI, Player, Core, Contracts, scene or terminal presenter writes. Await explicit CLAIMED ACK. Root imports approved output. Physical radio-desk art is an optional subsequent assignment, not a second simultaneous writer.

Runtime terminal integration and the two independent-review fixes (single Escape authority and passive display time refresh) belong to Codex root.

## Approved next interactions

- E enters a physical Earth/navigation touchscreen, mouse guides the finger, click animates contact and blue response, drag rotates an appropriately labeled Earth view, Esc exits. Input/camera/cursor have one owner.
- E enters a seated communications workstation; smooth camera/body movement, physical handset, clean subtitles without a large background rectangle. Time/delays remain authoritative.
- Compact habitat/growing area with pots, planting/connection animation and a wall DAY N display from the mission calendar. Crop growth takes simulated time and resources.
- A bed/sleep action advances the same survival simulation and stops on danger; wake-up presentation shows the actual new mission day. This is a later bounded subtask, not in I04.
- Keep the currently approved scene lighting and head movement. Improve menu transitions and remove distracting labels behind the menu.
- Permit movement during the first cockpit disturbance where compatible with first-person staging. Keep necessary finale/pause locks separate.
- Normal visible work gloves are requested in addition to cargo/EVA hands. The concept is a visual target, not a claim of a finished matching model.

## I04 handoff boundary

Runtime `c7cfb8681cb3ecc30a188b1d6d9120cac4d0c434` is pushed on both `feature/visuals-cinematics` and `codex/i01-integration`; original project updated and Unity Bootstrap opened. It includes physical cargo/two-hand grip, real 60-active-second deadline, segmented resource bars, preflight score/ticks, grade, airlock digital panel and the delivered EVA fix. It does not contain C06 sprint/survival, the new terminal, sleep or a new rescue ending.
