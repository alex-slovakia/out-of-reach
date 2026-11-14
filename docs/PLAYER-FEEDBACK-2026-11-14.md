# Alex's live playtest and requested next version

Recorded by Codex integrator, November 14, 2026. Explicit user requests supersede the original narrow ending in PRODUCT.md. This is an implementation queue, not proof of delivered features.

## Actual checkpoints

- Original project now at `9b2286a` (runtime `fb9703b`), imported/compiled in its own Unity6000.3.28f1 with exit0: latest R01–R05 ship, suit gloves, subtle walking motion, sparse HUD and GMAT stars. Preserved local voice metadata. I03 evidence:240 scripted scene checks; no human/standalone walkthrough claim.
- I04 in isolated integration: physical cargo, larger prompts, opening drift, redesigned pause, background blur, lower nominal lights. Cargo flow144 checks passed, including actual60-second deadline/pause/601kg/fallback/restart. Visual inspection found oversized overlapping crate labels, now being corrected; this correction needs a fresh capture.
- Claude urgent EVA fix `3bde63b` is merged into I04, not yet checked in root's full scene. Author reports253EditMode/25PlayMode from writer and clean clone. Root must verify free look and full mission.

## Accepted feedback

| Request | Owner / implementation state |
|---|---|
| Physical preflight items, not list selection | Codex integration of Claude C05. First stage uses the ship storage module; separate launch-site prologue still absent. Default valid cargo starts aboard and can be changed. |
| Sprint | Claude Player owner; NOT STARTED, awaiting Apoliak's go. Keep cargo slow-down, collisions, pause/cinematic/EVA restrictions. |
| Head tilt / turn response | Codex staged/integrating modest lean and turn response, reduced-effects option. No second movement authority. |
| Visible torso/legs and suit changes | Requested from Claude model owner; not delivered. Existing gloves are not a full body. |
| Larger things | User clarified: readable prompts, labels and buttons, NOT bigger ship geometry. |
| AAA-style E interactions | Larger key/verb hierarchy now; next visible fault, specific hand/tool action and visible repaired result. Do not fake progress after an instantaneous authoritative command. |
| Less flat/bright nominal lighting | Codex reduced practical intensity and narrowed cones; inspect actual scene. Emergency state already distinct. |
| Stress music, beeps, vibration, emotional voice | Existing state-driven3music beds/7SFX; review audible balance. User approved3new Orus audition treatments: controlled tension, crisis, exhaustion/relief. Credentials currently being located; no generated audition claim. |
| Moving opening and pause background | Codex own visual pivot, unscaled presentation only; reduced-effects respected. Oxygen/hazards/deadlines remain frozen when paused. |
| Pause should not be a utility box | Open left typography, blurred/dimmed world, clear actions, hide gameplay HUD; actual initial capture inspected. |
| EVA natural traversal | P0 free-look pitch/corridor defect; Claude owns Player fix, Codex verifies. Do not call old precisely aimed QA proof of natural controls. |
| Cinematic airlock | User subsequently confirmed chamber isolation works. Improve hatch motion/latch audio/state clarity; do not rewrite working pressure gates on a false bug assumption. |
| Countdown physically on asset | Original seven-segment display, actualIEvaStatus timer/enum, no inventedkPa. Current contract automatically opens the outer hatch: truthful SAFE TO EXIT/OUTER OPEN, no invented separate SAFE TO OPEN state. |
| Exterior life | Stars steady; occasional nearby debris glints/contact feedback acceptable. No atmospheric twinkle or constant cinematic meteor shower represented as real vacuum. |
| Repair, grip and suit animations | First authored Grip/Press hand-pose gestures integrating; these are local acknowledgements, not yet spatially aligned tool/rail contact or full suit-donning. |
| Report | User likes accuracy/content, wants concise initial outcome and optional deeper sections/sources. Redesign pending; preserve provenance and actual numbers. |

## Approved story extension: contact is the midpoint

After the real delayed Earth reply, continue into a bounded survival chapter. Player manages food/water/oxygen, collects actual ration items, uses a workbench, and installs one growing module. Crops consume water and power and require meaningful simulated growing time. Any chapter jump must advance the same resource and survival authority or be clearly narrative; no instantaneous potatoes or replenishment. Depleted food must have a coherently modeled consequence, not a fake HUD bar.

End states requested: death from failed survival, or a recovery craft arriving after a defensible elapsed-time passage, a rescue cutscene and onward journey to Mars. Rescue timing/trajectory is an authored fictional scenario, not a promise derived from the Earth–Mars range dataset. This expands Core state/outcomes, report, scene interactions, assets and animation; it is NOT in currentI03/I04.

Root to coordinate exact contracts/migration with Claude gameplay owner after urgent EVA work. Retain one authority and preserve existing scientific labels. Build one grow module and bounded recovery sequence before considering a large construction tree.

## Communication / time

## Approved reference and further drama,15:30CET

Alex approved the two-frame HUD/cargo concept and explicitly requires an implementation closely matching it: white countdown turning red at10seconds, real two-hand crate carry, sparse pictogram/segmented resource bars, cinematic practical lighting/vignette. Exact values belong in detail, except cargo mass/constraints needed to make choices. See `art-direction/approved-hud-02/README.md`. The reference is not a gameplay screenshot.

New gameplay request: stamina/exertion for sprint, carried mass and sustained tether movement, recovery on rest; distinct from battery/food/oxygen stores. Low-food survival consequences and appropriate fatigue/breathing feedback must come from real state, not a fake presentation meter. No arbitrary coughing on any low bar or instant starvation. Wood was an example of supplies: spacecraft-appropriate repair feedstock/spares/growing substrate are the proposed bounded material vocabulary. Claude's writer retains the domain scope; request posted to issue7comment6878791333. Last confirmed sprint status remains NOT STARTED pending Apoliak's go.

Current exact checkpoint375c535 passed273 full-scene checks and25 PlayMode tests. Subsequent HUD/lighting/cargo-hand changes need their own checks. Bounded Retell/Obelix search found0usable Gemini credentials in8PDFs including OCR of80relevant pages;0APIcalls/0newauditions. Existing approved voice clips remain intact.

## Communication / time (continued)

User finished the current playtest and explicitly authorized updating and reopening original Unity for another feedback pass. Next playable target communicated:15:30–16:00CET, with physical cargo/EVA/presentation work. The expanded survival/rescue chapter estimate was6–10additionalhours plus testing, expressly uncertain; do not describe that as a guaranteed complete-game delivery. Internal private GitHub work is authorized; no submission/publication to organizers.
