# I06 manual playtest delivery — 15 November 2026

Runtime checkpoint `ed1a4c5` was fast-forwarded into the user's original Unity project on `feature/visuals-cinematics`. The integration branch is `codex/i01-integration`. Unity remains 6000.3.28f1; packages and project settings were not changed. The user's local voice-cue edit was preserved and backed up with the prior Bootstrap scene.

## Delivered

- Grounded walking/sprint presentation and physical hand-push input routing.
- Shoulder-separated opaque hands, fingers upward on normal walls, diagonal side-entry ceiling contacts, continuous wrist/release motion and actual skin clearance checks.
- Explicit WORK/EVA avatar profiles and semantic bone bindings in Bootstrap. A new model still needs its own prepared clips, bone mapping and contact fitting; this is not arbitrary one-click retargeting.
- Launch FX V04: asymmetric flame, separated steam/dust banks, pressure front, raised cooling-water jets and stronger launch audio presentation.

Integration anchors: `I01MissionComposition`, `I06MicrogravityBridge`, `EmbodiedBodyPresenter`, `MarineDepartureFxPresenter` and `Bootstrap.unity`. The commit records the exact changed paths in Composition, Presentation, Audio, character profile assets and the departure shader.

## Verification and current policy

Alex explicitly requested immediate in-project delivery and will perform large gameplay tests himself. Only small, relevant checks should precede subsequent delivery. Do not run another extensive acceptance suite merely to delay a reviewable update.

Earlier isolated evidence: grounded gait V02 passed; hand candidate 5 passed 187 scoped assertions; avatar mapping/clip guards passed 17 checks; launch V03 passed 35 boarding/departure/arrival checks. These runs do not establish a complete playthrough or validate every later delta. Latest motion anticipation and launch V04 remain for manual review. Two V04 batch editor attempts crashed during editor startup before their execute method ran.

The final fast-forward and source diff check succeeded. Compilation/import of `ed1a4c5` in the user's live editor is **not yet confirmed**. The user stopped computer control with Escape; no further UI actions were taken. No new Windows executable was produced for this revision. The previous full-route run stalled after the Earth reply on the storage approach; see [the prior checkpoint](i06-next-steps-2026-11-15.md). Full success/loss/restart acceptance remains open.

## Pending, not included in this delivery

Exact cockpit diffuser/access-frame collision meshes, cargo knee-clearance refinement and subtle antenna visor condensation remain staged locally. The flat cockpit ceiling already aligns at 3.12 m; the missing geometry is the local 3.10 m diffuser/access frame. Do not globally lower the ceiling.

Continue the [approved 13-step walkthrough](../playtest-review/03-walkthrough.md), user feedback and eventual Windows packaging. No public release or submission is authorized.
