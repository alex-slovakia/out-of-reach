# Page 1 — Complete feedback and implementation plan
Consolidated from Alex's playtest messages and accepted design requests. Updated 14 November 2026, approximately 16:40 CET. IDs are stable for follow-up reports.

## Bugs and presentation problems

| ID | Report | Required change / acceptance |
|---|---|---|
| B01 | Doors or invisible barriers block passage; EVA movement jumps when looking up/down | Retest the delivered distance-along-route fix in the real scene, every bend and return; repair collision/route alignment. Free look alone must never move the player. |
| B02 | First cockpit disturbance locks walking | Opt-in movement during Impact only; retain necessary pause/finale gates. Domain implementation delivered, scene opt-in pending. |
| B03 | Sprint missing | Hold Shift / gamepad stick; genuine exertion, bounded recovery, no cargo/EVA sprint and no collision bypass. Implemented and tested independently; not yet in original. |
| B04 | Side movement feels good but forward movement has no bob | Preserve lateral tilt; enable distance-driven preflight bob, modest vertical amplitude. Planning was excluded from the presenter; two-line fix delivered, serialized scene update pending. |
| B05 | Signs/text visible through walls; reversed hatch signage | Depth-tested labels, inspect one-sided decal placement and hatch geometry. Do not hide a collision/render bug with extra UI. |
| B06 | Picked-up crate leaves its sign behind | Hide labels when their corresponding stock/case is absent; no stale target sign. |
| B07 | Loading boxes remain during later in-flight gameplay | Move preflight stock to the separate dock; remove temporary loading representation after departure. Persist actual carried inventory in the ledger. |
| B08 | Player cannot tell where to bring cargo | One objective, a restrained destination chevron and a visible secured loading bay. E secures cargo inside its valid region; arbitrary discarded cargo does not count as aboard. |
| B09 | Hands vanish on Escape | Freeze the current valid hands/body presentation while paused; restore reliably after resume/restart. |
| B10 | Hands float in an unnatural claw pose | Use earlier approved reference, relaxed low arms at idle; raise hands for actual actions. Latest review-04 hand pose is explicitly rejected. |
| B11 | Cargo lettering and airlock countdown glow too much | Matte/depth-correct labels and subdued display luminance, crisp numerals. Keep the working physical countdown. |
| B12 | Orange status text and repeated notifications look rough | Neutral contextual prompts, important state changes only, remove redundant completed-action prompts. No spoken “Nothing to interact with.” |
| B13 | Subtitles have a large dark rectangle | Clean readable subtitles with minimal text outline only; no large shadow panel. Test against both bright and dark scenes. |
| B14 | Pause/menu lacks hover feedback and smooth transitions | Audible restrained hover/confirm cues, slight scale/translation/highlight, smooth entry/exit. Menu sound must work while gameplay audio is paused. |
| B15 | Pause is static | Blur the actual camera view at the pause location; subtle presentation motion only. Resource, deadline and mission clocks remain frozen. No replacement background picture. |
| B16 | Labels, edges and lighting artefacts distract behind the opening menu | Hide irrelevant world labels while the menu is open, fix clipping/render artefacts. Preserve the interior lighting Alex now likes. |
| B17 | Nominal interior initially overbright | Keep the latest approved interior look; locally fix overexposed screens/readouts rather than globally darkening the ship. |
| B18 | Exterior platform, hull and antenna look visually inconsistent or too dark | Coherent directional sun, readable work lights, believable materials, consistent shadows across the whole exterior. |
| B19 | Report is packed, repetitive and ends gameplay too early | Contact becomes a midpoint. Move technical detail to an optional flight log. A later rescue/death sequence is the actual ending. |
| B20 | E interactions are instant and visually weak | Larger readable cue, one clear action, physical hand/tool/switch response, real progress where action timing exists. No fake progress after an instant repair. |
| B21 | Reference HUD bars do not correspond to real quantities | Bind separate exertion, ship battery, food/hydration, cabin/suit oxygen states. Show only relevant bars; precise units remain available in systems detail. Never copy reference meter values. |
| B22 | Terminal model looks generic and too smooth | Claude-assigned hardware revision: industrial proportions, recesses, seals, fasteners, tactile controls, mounting and plausible material variation, preserving integration anchors. Awaiting Claude ACK. |
| B23 | Visible gap beside an interior wall/hatch; player fears falling outside | Priority P1 containment report. Inspect both hull mesh continuity and colliders; screenshot establishes visible gap, not yet a reproduced escape. Close the real seam and verify the player/camera cannot cross the pressure hull during cockpit-check phase. Independent read-only diagnosis assigned. |
| B24 | Looking at a spot above the airlock timer fills the 3D view with cyan while HUD remains | Priority P1 rendering report. Inspect display/label surface dimensions, material/shader, backfaces and camera near-plane at that view. Screenshot is evidence of the symptom; cause is not yet reproduced. Fix before cosmetic additions. |

## Additions and accepted direction

| ID | Addition | Bounded implementation |
|---|---|---|
| A01 | Separate physical loading prologue | Small coastal/sea-side base with one playable dock/loading deck, supply rack and transfer-carrier access. No giant city, detailed road network or whole spaceport. |
| A02 | One-minute urgent cargo selection | Large white countdown, red during final ten seconds, restrained beeps; two hands, movement slowed by mass, real 600 kg/budget/load constraints and meaningful chosen reserves. Practice mode remains. |
| A03 | Clear carry/place loop | E to pick up; visible destination; E to secure within a marked cargo zone. A dropped/unsecured case must not silently add inventory. |
| A04 | Realistic boarding/launch/departure | Selection minute is not the final real launch minute. Time-compressed hatch/seat/checklist/rocket ascent, orbital docking with already-assembled AURORA-7, then departure. Fictional transport, no claim of NASA-validated payload/trajectory. |
| A05 | Calm opening turning into crisis | Original or compatible licensed light cabin music, routine exchange, music cut, missing response, alarm/blackout, then regain control. Deep-space Earth messages keep signal delay. |
| A06 | Playable cinematic accident | Existing camera remains usable where safe; physical switch response, restrained shake, flicker, alarm escalation and short SURVIVE objective. Reduced-effects mode must remain usable. |
| A07 | Visible body when looking down | Torso/hips/legs/boots with workwear and EVA outfit variants, one body rig following the existing player, no second controller; head/neck hidden from own camera. |
| A08 | Better hands and optional left/right gesture | Earlier approved cloth/seams/glove silhouette; left mouse selects left hand, right mouse right hand in ordinary gesture context. Terminal/menu input takes priority. Natural idle. |
| A09 | Detailed repairs | Visible damaged part first, draw a ratchet/tool, brace with other hand, roughly three-second valid repair, actual success/failure/progress and visible restored component. No magical or excessive sparks. |
| A10 | Tether/climbing presentation | Braided textured tether, attached locking hook, hand-over-hand contact along stable traversal, meaningful foot/hand supports. No loose orange line or camera teleport. |
| A11 | Suit interaction | Donning/checking cuffs/helmet, clothing rustle and sealing feedback. Body appearance must reflect actual suit state. |
| A12 | Cinematic airlock | Keep working two-door interlock and pressure model; animate door hardware, motors, latch/thump, pressure panel and suit breathing. Do not vent the whole cabin for spectacle. |
| A13 | Physical digital status displays | Actual pressure/repressurization countdown, restrained brightness and readable digits; changing DAY N from mission time. |
| A14 | Dramatic male voice performances | Approved voice with controlled tension, natural crisis and exhausted relief, matched to scene; preserve intelligibility rather than heavy radio filtering. |
| A15 | Adaptive music and small effects | Calm / escalation / exhausted hope, voice ducking, hover/confirm, door mechanisms, switches, beeps, breathing, suit cloth and contact-transmitted EVA sounds. |
| A16 | Gradual stress/exhaustion consequences | Real exertion decline while sprinting/carrying/traversing, gradual hunger/hydration consequences, restrained breath/visor/camera feedback. Effects must not invent extra gameplay depletion. |
| A17 | Convincing space | Catalogue stars and correct view/reference directions, appropriate planet apparent scale, distant backdrop without walking parallax. No routine atmospheric twinkle/meteor streaks in vacuum; rare near debris only where authored. |
| A18 | Physical Earth/navigation touchscreen | E enters; mouse guides finger, click presses with small blue response, drag rotates labeled Earth view, Esc exits. Single input/camera owner. Dated NASA image, mission time, observation age and honest reference coordinates. |
| A19 | Weather/data display | Use available documented NASA observations, explicitly dated; no claim of live weather from an archived EPIC image and no fabricated spacecraft GPS. |
| A20 | Physical radio workstation | E smoothly sits/positions player at desk, handset/controls and clean received-message subtitles, actual communications state and light delay. |
| A21 | Continued survival after Earth responds | Rations/drinking, separate need and stock, food/water/battery/oxygen management with meaningful loadout consequences. No premature success report. |
| A22 | Crafting/growing module | One bounded expandable rack/module with parts, water and power costs, physical assembly/planting/harvest interactions. No open-world base builder in this slice. |
| A23 | Real growth and food | No instant potatoes. Initial bounded crop uses explicitly assumed pre-grown seedlings and eight simulated growth days; a longer potato crop needs its own researched timing. Food pouches/rations plus one modest harvest, not full nutrition from a decorative plant. |
| A24 | Bed, sleep and days | Physical berth, sleep/wake animation, actual DAY N; sleep advances the same resources/needs/events and stops on danger or important radio events. |
| A25 | Rescue setback | A provisional nearby fictional rescue plan may fail; update ETA honestly. Never assert an Earth-launched craft reaches Mars in 15 days based only on distance/c. Exact schedule awaits scenario design. |
| A26 | Actual successful finale | Survive until a time-separated rendezvous/rescue, then a later Mars arrival/departure chapter cut. Final relief moment is visual; not an instant result of radio contact. |
| A27 | Failure ending | Gradual legible danger and a coherent death/failure sequence from the actual failed system, then concise recovery/restart choices. |
| A28 | Scrolling mission record / credits | Skippable, pausable scroll after the finale: selected supplies, key actions/outcomes, NASA data actually used, derived calculations and fictional assumptions; sources clickable/openable. Optional detailed log, no information wall. |
| A29 | Real Blender assets and compatible free resources | Editable .blend, UV/PBR, sensible geometry/LOD/collisions, FBX and actual Unity validation. Reuse delivered ship/crates. Verify source license and keep per-asset provenance; do not assume any publicly downloadable model is commercially usable. |
| A30 | Accessibility and readable UI | Larger prompts/buttons, safe text contrast, reduced camera/flashing, readable phone-scale captures, adjustable audio/subtitles; avoid excessive overlapping readouts. |
| A31 | Active progress and QA | One writer per scope, GitHub checklist/reference/evidence, separate independent test review, actual success/failure/restart and fresh-build checks. Never call a remote machine synced without its ACK. |
| A32 | Hand-driven microgravity after launch | Normal walking on the sea deck; seat/harness during launch; in orbital AURORA-7 use reachable handles and left/right-hand push-offs with bounded momentum and collision. No walking bob/steps in free float, no distant-handle teleport or click-anywhere thrust. Preserve EVA tether, pause/rest/cinematic gates and a safe way to recover when away from a handle. Optional prototype first; not yet playable. |

## Deliberate constraints
The design is approved in principle; the newest hand pose and illustrative status-bar mappings are not. The earlier approved glove reference wins. Keep the user's current interior lighting. No speculative new gameplay systems beyond this one mission. A rendering reference is not evidence that the corresponding model, animation or state is already implemented.

## Current implementation evidence
- Original playable project: I04 `c7cfb86`, 187 cargo checks, 274 scripted scene checks and Windows Development build. These are not a claim of flawless human play.
- Sprint/exertion source `0e1aeab`: 266 EditMode + 34 PlayMode pass in its separate checkout.
- Impact + first survival/growing domain `c57553f` / `e9ce5c1`: 263 standalone NUnit checks and Unity-reference C# compile; not a Unity scene playtest.
- Forward/preflight bob `70ea6ef`: narrow source change/reference compile, scene serialization pending.
- Terminal V1: actual Blender source/FBX, 182 model checks and reference compile; hardware revision requested and Unity integration pending.
- Root has uncommitted presentation fixes in the isolated checkout. They are not counted as passed or deployed.

