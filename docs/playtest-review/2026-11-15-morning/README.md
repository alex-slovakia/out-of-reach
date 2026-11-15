# Alex's morning playtest — 15 November 2026

## Current deadline and delivery status

**User's binding deadline: TODAY 15 November 2026, target 19:00 CET, hard deadline 23:59 CET.** The previous estimate extending work into later days is superseded. Prioritize a complete playable hackathon build, blockers and clear controls, then presentation. This deadline is a delivery constraint, not evidence that every art ambition can be achieved today. Do not call this AAA quality or declare every catalog row complete without evidence.

The full catalog is **24 bugs and 32 additions**, not 25/31. All 56 are NOT complete. Last strict historical acceptance was 4/24 bugs + 3/32 additions on the previous original build. Additional individual feature checkpoints exist; these must not be added into a current-build total without a fresh combined review. See [row-by-row audit](../../verification/i06-status-audit-0140.md) and [original catalog](../01-feedback.md). Latest original playable revision is `710a99b`; staged work below is not yet in Alex's game.

Next working estimates: 06:45–07:15 CET partial checkpoint; 07:30–08:30 movement/recovery test; 10:00–12:00 launch/audio integration checkpoint. These are estimates, not passing-build claims. Protect the 19:00 target with a content freeze and complete-path verification; surface any remaining must-fix issue instead of concealing it.

## Complete morning feedback, including user corrections

Seventeen observations: sixteen active requirements and one withdrawn audio report. Some overlap the existing 56 rows; do not inflate completion counts by duplicating them.

| ID | Observation / requested change | Actual state / next action | Owner |
|---|---|---|---|
| AM01 | Body has no visible grounded walk/sprint animation. | Candidate staged in GroundBodyV01; compiled and Blender poses checked. Native playtest and integration pending. | Codex body presentation |
| AM02 | Menu hover/click sounds missing. | **Withdrawn by user:** volume was low; menu ticks are audible. Preserve existing feedback. Do not claim a new fix. | No active repair |
| AM03 | Flat brown empty launch terrain looks unfinished. | Open art rejection. Lighting alone does not fix geometry/materials. Coordinate bounded terrain pass with Claude. | Claude art; Codex integration |
| AM04 | First departure spoken cues absent from “Secure for departure,” including sealing/hatch secured. | Source confirmed cue path has text/SFX but no corresponding voice. Generate/bind concise approved-voice takes and verify audible playback. | Codex audio/integration |
| AM05 | Music too loud; opening needs emotional dramatic music behind speech. | DepartureAudioV02 has staged trim/ducking; commercial-use track researched, not imported. Listening approval pending. | Codex audio |
| AM06 | Launch lacks convincing engine/ignition/cabin vibration sound. | Four original SFX staged in DepartureAudioV02, not bound. Exterior vacuum remains silent apart from score/radio. | Codex audio |
| AM07 | Launch shake is too weak. | DeparturePolishV02 candidate; native feel review pending. Preserve reduced-effects mode. | Codex cinematics |
| AM08 | Visible seated body and hand animation fastening seatbelt. | Open. Existing cinematic presentation hides body. Requires actual harness/pose integration, not only a buckle sound. | Codex presentation; coordinate model scope |
| AM09 | Launch shot needs real plume/dust/ascent/atmosphere transition. | DeparturePolishV02 staged and compiled. Not yet bound or native-verified. | Codex cinematics |
| AM10 | Orbit approach, docking and astronaut transfer to AURORA-7. Grounded rocket cannot carry “Later aboard” caption. | Candidate shot sequence staged; receiver/collar/camera anchors and transition remain integration work. | Codex integrator |
| AM11 | Apparent post-cutscene movement lock. | User corrected: forgot the new microgravity controls. Add contextual onboarding; this does not prove input is disabled. Ceiling failure is separate AM15. | Codex UI/input presentation |
| AM12 | Look-down torso is oversized/obstructive; body/boots should read naturally. | Standing reframe measured in GroundBodyV01. Floating reframe unresolved; do not break arm contact by shifting body indiscriminately. | Codex body presentation |
| AM13 | Jagged railing/cabin shadows. | Open actual render issue; inspect quality/material/light configuration without broad package changes. | Codex visuals |
| AM14 | Stronger forward walking view bob. | Open tuning/integration. Grounded motion only; microgravity should react to actual pushes, without walking bob/footsteps. | Codex presentation |
| AM15 | Stuck on ceiling; push away from reachable walls, floor and ceiling using the corresponding hand. | Independently reproduced in continuous run3, FAIL at ceiling. Ceiling01 bounded correction staged; native validation pending. | Codex contact presentation; Claude player authority |
| AM16 | Both mouse buttons give one stronger coordinated two-hand push. | Open. Existing input selects one hand; player cooldown prevents two sequential ordinary calls. Needs atomic validated pair API and two real animated contacts. | Claude Player API; Codex hands/input |
| AM17 | Obvious reachable grip affordance and strong grab/push animation. | Open usability/presentation requirement. Distinguish push from hold clearly; no remote grabbing or invisible force fallback. | Codex UI/hands |

## Evidence and accepted handoffs at 06:20 CET

- Continuous saved-scene mission run3 ended **FAIL** at 05:11 UTC after 1118 checks: WORK ceiling target outside anatomical reach, no measured approach for 18 s. A test assertion count is not a count of fixed bugs. Ceiling01 candidate is staged separately and has not fixed the playable game yet.
- Claude engineering detail package delivered: `38cac1f591c06f938616e5a52628144fa55f76d0`, `feature/ship-detail-pass-02`. Existing-material cable looms, clamps, junction boxes, access panels; author reports Blender round trip and isolated Unity import pass. Our scene integration/material verification pending. Habitat follows, then cockpit.
- Codex staged: GroundBodyV01, DeparturePolishV02, DepartureAudioV02, MicroHumanPlayabilityV01/Ceiling01. Source compilation/geometric checks are not a native game acceptance.

## Additional original user screenshots

![Departure cabin: missing spoken cues and visible harness request](04-departure-cabin.png)
![Rejected grounded-rocket shot and incorrect orbital-transition presentation](05-departure-exterior.png)
![Look-down framing and unclear transition to microgravity](06-post-departure-body.png)
![Jagged railing shadows; music volume feedback](07-shadows-and-mix.png)
![Player stranded near the ceiling](08-ceiling-stuck.png)

## Historical first capture below

The first three reports below are retained for provenance. AM02's original open status is superseded by the explicit withdrawal above.

Playable source checkpoint: `710a99b`, promoted into Alex's original project from `33f474f` at approximately05:49CET. The original production voice-script edit was backed up and preserved byte-for-byte; it is not a new runtime voice change. Original import/compile log contains no C# errors at05:51CET. Alex is actively testing; do not modify/restart that running project until his playtest ends. Latest isolated candidates are not automatically in this checkpoint.

| ID | User observation | Status and next action | Owner |
|---|---|---|---|
| AM01 | No visible walking animation while the grounded character moves; models themselves are acceptable. | Open. Inspect grounded body animation binding and provide a real locomotion cycle tied to movement, separate from float mode. Preserve the body mesh. A paused still is not motion evidence; use actual moving capture for acceptance. | Codex presentation; coordinate new authored clips with model owner only if needed. |
| AM02 | Menu hover/click feedback has disappeared, observed in pause menu. | Open regression. Audit actual pointer/focus event, cue binding and audible paused output. Menu sound must remain separate from paused gameplay. Do not infer removal or cause from the screenshot. | Codex audio/UI; one staged lightweight reviewer. |
| AM03 | Launch terrain looks flat, brown, sparse and unfinished. | Open art rejection. Claude requested to remodel after the currently started engineering package: bounded visible landscape, believable relief, embedded rocks, varied original ground material and coherent horizon. Root's lighting/haze change is a separate candidate and does not complete this geometry/material request. | Claude model writer; Codex environment integration. |

## User screenshots

![Grounded body shown during pause; motion complaint supplied by Alex](01-grounded-body.png)

![Pause-menu hover sound regression report](02-menu-feedback.png)

![Rejected current launch terrain](03-launch-terrain.png)

## Pending art handoffs, not completed work

Claude acknowledged SHIP-DETAIL-02 in issue7 at04:49UTC. Author estimates (CET): engineering07:10, habitat08:00, cockpit09:00. Each room is independently committed/pushed with real Blender preview, metadata, placement manifest and RESUME.md. Account report89%weekly used. Low quota requires a labeled WIP handoff; no waiting for Apoliak to save finished work. These are author estimates, not guaranteed integration times or proof of completion.
