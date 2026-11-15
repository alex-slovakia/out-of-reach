# Alex's morning playtest — 15 November 2026

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
