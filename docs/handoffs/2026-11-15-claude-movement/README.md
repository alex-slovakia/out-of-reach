# Active handoff: movement back to Claude, 15 November 2026

## Latest user decision (supersedes earlier Codex movement ownership)
The user still finds movement broken and explicitly requests Claude fix it. Codex stops further movement edits/tests now and hands integration/light presentation work to a new chat. No movement completion is claimed. The current mechanics experiment is UNVERIFIED and is not promoted into the user's open Unity checkout.

## Movement behavior to implement and verify
1. Ground/prologue: familiar WASD walking, readable cargo carrying, reliable collisions.
2. Microgravity: Space pushes from any reachable floor, wall or ceiling, without requiring W+Space. Nearby contact is selected predictably and shown before acting. No contact => short helpful feedback, no silent failure.
3. WASD selects camera-relative departure direction while pushing; contact geometry constrains physically possible impulses. Default neutral Space goes away from the contacted surface. Looking sideways at a ceiling must still allow pushing downward.
4. Unsupported movement preserves inertia. A/D does not create unexplained acceleration. If optional assisted steering is implemented, it must explicitly use suit jets with sensible limits; do not introduce hidden free-flight force.
5. Proposed unified E grip/release for nearby handles or tether, with contextual HUD; resolve conflicts with existing E repair/boarding/seat interactions centrally. While holding, stabilize and use W/S for hand-over-hand traversal; visible alternating grip, reach, pull and release, no walking along a rope. Current bindings are not claimed to implement this proposal.
6. Fast, simple, forgiving control: brief buffered press; no repeated pushes from held Space; no stale push after pause/menu/focus loss. Tune speed by actual novice route playtest, not just numerical tests.
7. Visible hands contact the actual surface before impulse; natural body and legs when looking down in WORK and EVA, no torso/breast/stomach distortion, near-camera intersection or limbs through hull. Capsule, posed mesh and tether-route clearance all need checks.
8. Acceptance: real user input in both suits; floor/wall/ceiling, neutral Space and A/D, catch/release, rope traversal, pause during reach, rapid repeated input, narrow corners, cargo carrying, full EVA out/repair/return. Record before/after video plus exact revision/logs. Unit tests alone do not prove feel or rig animation.

## Ownership and priorities
- Claude gameplay: movement mechanics + body/hand animations first. Root Codex releases its two composition movement adapters for a coordinated Claude proposal (separate branch; integrator reviews). Avoid two writers on the same file. No merge/promotion without exact-revision checks.
- Claude character work: use ORIGINAL male references only: docs/art-direction/astronaut-reference-v01/cabin-front-back.png and eva-front-back.png. Preserve agreed rig/164-bone contract; coordinate body owner. No substitute random female character or rejected EVA concept. Sitting, fastening/unfastening harness, resting and wake body animations are still needed.
- Claude art/VFX: user explicitly requests parallel work on launch water deluge, steam, engine exhaust, ignition/ramp-up, failed-launch rocket explosion and cinematic effects matching supplied reference direction. Split disjoint folders/sessions; movement priority remains. Earth launch audio can be atmospheric; external space sound only suit/contact/radio or score. Overload explosion must follow authoritative mission result, never a parallel presentation outcome.
- Tower/access bridge brief: convincing bolted steel truss, gussets, grated service decks, safety rails, cable trays, pipe runs, retractable crew connection, deluge pipework and drainage. Clear player access and spacecraft clearance; restrained ochre markings/wear. New LaunchTowerV02 folder proposed; no shared Bootstrap/old tower edits without integrator. Review against existing ship/launch references, not arbitrary greebles.
- Suggested efficient routing: strongest available Claude model for movement/rig diagnosis and final review; Sonnet-class authoring for bounded tower/props/VFX, small model for inventories/docs only. Claude must confirm actual available models and quota; do not launch many expensive sessions blindly.

## Confirmed Claude status (snapshot, not live telemetry)
Issue #7 comments 6893572400 and 6893569129, 11:22 UTC: gameplay ONLINE (self-reported Opus 5.5), 95% USED / 5% remaining, reset 17:59:58 UTC = 18:59:58 CET. Gameplay was working on body fit/gait, estimated first review 13:30 UTC / 14:30 CET. Art DP02 cockpit ETA 12:15 UTC / 13:15 CET. These are their estimates, not completion guarantees. Notify user only on new confirmed state. The earlier before-14:00 target is not a reliable promise.

## Delivered baseline
3d47c04 on codex/i01-integration and original feature/visuals-cinematics; pushed to GitHub. Includes 13 licensed Poly Haven models / 20 placed instances, ambientCG ground relief/materials and metals, NASA deep-sky source, cinematic wake title/eyelids/sound. See committed free-assets-review-20261115.md, polyhaven-import-20261115 receipts, and docs/verification/i06-rest-wake-cinematic-20261115. Rest native check passed 92 assertions. Native asset import passed. This is not a claim that scenery reached the approved concept quality or body/harness animations exist.

## Unverified movement experiment, preserve without promotion
Local integration workspace has modified I06MicrogravityInputBindings.cs and I06MicrogravityBridge.cs, plus new Tests/MovementIntegration. Included movement-mechanics.patch and source metadata preserve the agent experiment; current-live-tracked.patch also includes help text and one existing test expectation. Do not apply both overlapping patches blindly.
Diagnosed floor-only Space and degenerate ceiling contact frame. Experiment routes nearby contacts through hand bridge, increases default push .35 -> .65, and buffers .4 seconds. Existing speed cap 1.5 remains. It does NOT fix tether collision bypass: EVA route disables CharacterController and directly writes transform position.
TestResults/movement-contact-01.xml: 30 total, 23 pass, 7 fail; existing 20 passed, new geometry 3 passed, new input 7 failed. Agent subsequently fixed hidden Game View test input routing only. That fixture correction has NOT been rerun. No successful native floor/ceiling smoke yet.
Included SurfacePushNativeQa fixture is reference-compiled only, not run. It uses actual Practice -> board -> launch -> floor Space -> passive ceiling collision -> ceiling Space, with screenshots/traces; 150-second watchdog. Copy into Assets/Editor only when conducting isolated QA, then invoke SurfacePushNativeQa.Run.
Claude earlier candidates: feature/move-response-01@821ade0 (carry/traversal floors); wip/move-input-claude@c753176 UNVERIFIED; feature/quit-tests-hydration@b257de4 tests. Review deliberately, do not blindly combine.

## Codex continuation tasks (small, one at a time)
- Move DAY N to upper-middle, currently centered; validate screenshot. Eyelids/wake are present, actual sitting/belt animation is NOT.
- Latest screenshot clarifies earlier 'stains': user meant BLACK CARGO SIGNS, mismatched/detached around rack, not stains/decal textures. Align each placard to correct crate bay, keep associations legible when crates are removed; prevent crossing foreground clutter. Preserve approved crate colors. Reference screenshot copied beside this report.
- Prologue music: three new dramatic previews for user approval, no unwanted drum ending; louder and continuous for full loading minute plus transition until cutscene, no premature track change. A/C approved earlier; B rejected. Not created yet.
- Loaded-mass HUD visibility and real cargo resource consequences audit; above 600kg attempted launch should trigger clearly warned failed launch/explosion/death cinematic per user's fictional gameplay rule. New failure path NOT implemented; coordinate domain authority with Claude.
- Approved landscape concept: docs/art-direction/prologue-landscape-target-v01/landscape-target.png. Latest correction: mountains yes; sea and road unnecessary. Improve terrain layers, small unevenness, rock/vegetation clustering and service details; existing pass still insufficient. Do not claim image is implemented scenery.
- Keep Sources without planet/orbit graphics (user dropped idea), approved end screens, water locator, antenna move-closer hint. Trailer HyperFrames is later issue #10.

## Workspace and safety
Original C:/Users/alexk/Documents/Projects/OUT-OF-REACH-NASA-HACKATHON remains user-operated Unity 6000.3.28f1. Do not stop it, take UI control, or launch another editor on it.
Integration is .local/i01-integration; only run isolated hidden Unity there. Preserve all existing GUIDs and unrelated settings, avatar QA work, tools/verify_repository.py, and voice-cues.en.json. Current Unity imports also dirtied material serialization in both checkouts: do not blanket stage or reset. Stage exact owned paths only.
User reports Codex 41% weekly remaining, not independently verified. Below30% only small tasks; below15% reserve for final integration/compilation and delegate heavy work. No unnecessary polling, broad research, repeated whole-suite testing or extra agents. Claude quota is separate.

## Suggested skills
handoff; unity-csharp-scripting for gameplay review; ui-uitk for DAY placement; unity-export and hard-surface for coordinated models. Read mandatory project docs before edits.
