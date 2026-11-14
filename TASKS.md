# Execution plan and status

**Deadline:** November 15, 2026, 23:59 in the team creator's local timezone. Confirm that timezone. Suggested internal release cutoff: at least two hours earlier. All team planning below uses Europe/Bratislava unless explicitly labeled otherwise.

Status vocabulary: `in progress`, `ready`, `blocked`, `not started`, `verified`. An artifact existing is not proof that its tests pass.

Integrator: **Codex**, explicitly selected by Alex on 14 November 2026. Live cross-chat status,
writer claims and acknowledgements: [issue #7](https://github.com/alex-slovakia/out-of-reach/issues/7).
This table records verified checkpoints, not a claim that a stopped chat is still running.

| ID | Priority / owner | Task | Depends on | Acceptance checkpoint | Status |
|---|---|---|---|---|---|
| F00 | P0 / integrator | Audit existing Unity, pin 6000.3.28f1, repository, ignore rules and LFS | — | Remote/private access checked; source-only baseline; no changed original GUIDs | Verified foundation |
| F01 | P0 / integrator | Product, architecture, contracts, ownership and prompt pack | F00 | Eight interfaces including planner bridge compile; both developers can state their boundaries | Verified foundation |
| D01 | P0 / integrator | Reproducible NASA/JPL snapshot and assumptions | F00 | Raw source, request, epoch, units and derived distance/delay checked | Verified offline; integrated C04 communications timing passed I02 scripted scene checks at7e41699. |
| C01 | P0 / Claude | Mission phases, event bus, mission planner/configuration and resource accounting | F01 | Deterministic EditMode tests; invalid actions consume nothing; no visual dependencies | Delivered caee234, merged by Apoliak via PR #6; independent 77/77 EditMode pass; review fixes pending C02 |
| V01 | P0 / Codex | Modular interior and visual preview | F01 | Cockpit, engineering, storage, airlock; named anchors; compile and scene inspection | Verified fallback 0be6d6a; final modeling handed to Claude/Fable |
| V01A | P0 / Claude/Fable model writer | Remodel the fictional ship, preserve agreed gameplay anchor layout | V01 handoff / CODEX-006 | Unity-compatible models, licenses, genuine renders and placement checks | Delivered and integrated through `4ccc17c`: R02 engineering, A07 antenna, ARS-1 prop. Author reports Unity import checks; combined-scene checks in progress |
| C02 | P0 / Claude | Movement, interaction, power/life-support repair | C01 | Walk/interact in test fixture; repair costs atomic; repeated use safe | Delivered; I01 automated scene smoke passed 111 checks against C02 `91e6215`. User movement-feel feedback remains open |
| V02 | P0 / Codex | Planner/HUD views and accident presentation | V01, contracts | Readable constraints/status; event-driven blackout/alarms; input/camera restored | Interim HUD works but user rejected its density. Codex reclaimed UI after Claude confirmed NOT STARTED; one V03 writer active. Approved clean voices integrated. |
| I01 | P0 / Codex integrator | First connected playable: plan → accident → interior repairs | C02, V02 | Fresh loadout changes resources; gameplay and visuals agree; clean Console | Windows build and 111-check automated scene smoke passed; immutable I01-C02 candidate delivered. Independent desktop test stopped by user before full route; old fallback ship, not final art |
| C03 | P0 / Claude | Suit/airlock gates, constrained EVA, antenna repair | C02 | No unsuited exit; return possible; resource failure deterministic | Delivered6bb788b; I02 scripted actual-input EVA round trip, suit/airlock/repair/return passed at7e41699. |
| V03 | P0 / Codex | Airlock animation, exterior route and EVA presentation | V01 | Anchors/collisions readable; no duplicate player/camera; no vacuum air sounds | Eight interactions and two gates verified; batching/high-FPS movement/antenna endpoint fixed. Hands f72224c delivered separately, not integrated. |
| C04 | P0 / Claude | NASA data consumption, delayed transmission, outcomes and report data | C01, D01, C03 | Round-trip timing correct; success/failure/restart tests; source included | Delivered6bb788b; author215 EditMode +15 PlayMode. Integrator198 scene checks include delayed response, success/loss and restart at7e41699. |
| V04 | P0 / Codex | Communications finale and report views | V02, C04 contract readiness | Correct countdown/units, delayed voice, final camera, report and restart UI | Actual C04 communications/report and clean reply voice passed integrated scene checks. New UI remains in progress. |
| I02 | P0 / coordinated integrator | Complete mission integration | C03, C04, V03, V04 | One complete success and one failure playthrough from executable | 198 scripted scene checks + Windows development build PASS at7e41699; original project merge4784913 import/compile PASS. ZIP packaged with SHA-256; manual executable playthrough pending. See docs/verification/i02-7e41699. |
| C05-A | P1 / Claude | Physical preflight cargo loading | C04, ICargoLoading | One carried crate, one planner inventory, authoritative weight/movement, existing limits/deadline | Delivered9a6c2e4 via gameplay9044a00; author evidence only. Root binding review ready; opt-in physical loading not in frozen I02. |
| C05-B | P1 / Claude | Salvage and one crafted survival upgrade | C05-A, IFieldModifications | Atomic repeat-safe costs, explicit science loss, exact simulation-time accounting | Delivered9044a00; author250 EditMode +20 PlayMode for combined C05. Root scene integration and playability verification pending. |
| R01 | P0 / team | Polish, accessibility, performance and release | I02 | Windows x64 build, reproducible launch, credits/data, capture and upload buffer | Planned after I02. User Nov 14: do not publish or submit to organizers yet; internal private collaboration continues |

## Order and time budget

1. **Foundation now:** finish F00/F01/D01, push a verified baseline and onboard Claude.
2. **First parallel block:** Claude C01, Codex V01. Contract defects return to the integrator before either side adds workarounds.
3. **Interior checkpoint:** C02 and V02, then coordinated I01. Do this before extending the mission outdoors.
4. **Full loop:** C03/V03, then C04/V04 and I02. Prioritize two working outcomes over optional story embellishments.
5. **Submission day:** reserve the final quarter of development time for R01; feature freeze no later than four hours before the confirmed deadline; release candidate and recording at least two hours before it.

If time slips: cut the optional modular installation and false-hope replay first; retain planning consequences, three repairs, EVA, NASA-derived communications, two outcomes and report. Use clean geometry and controlled lighting before new art packages. Do not cut verification or the upload buffer.

## Integration evidence log

| Checkpoint | Commit | Editor / environment | Tests or inspection | Result |
|---|---|---|---|---|
| Initial audit | Pre-baseline | Existing 6000.3.28f1 project, URP | Editor/package/settings inspected | Version identified; not a build test |
| D01 snapshot | Pre-baseline | Python offline verifier | 32 samples, source/CSV hashes, vector ranges and light-time checks | Passed; not runtime gameplay integration |

Latest evidence: [I02 scripted mission and Windows build](docs/verification/i02-7e41699/README.md). Manual executable playthrough remains unverified.

## Checkpoint evidence — 14 November 2026

- **C01:** caee234b9bfa6e0eb5e672492f650601d25de592, merged to main as 43be436 via PR #6 by Apoliak. Codex independently ran its exact domain sources with presentation in the isolated Unity 6000.3.28f1 verification project: 77 passed, 0 failed/skipped, exit 0 (`c01-review-tests.xml`). This does not verify a playable Bootstrap. Pause/advance, finite arithmetic and outcome-boundary fixes are tracked for C02.
- **V01 fallback:** 0be6d6a, isolated Unity build/validator and fresh GPU captures exited 0 (`v01-lighting-build-1.log`, `v01-lighting-render-1.log`). Ten anchors, 14 bindings, 405 standing/floor samples, no missing scripts/materials; corridor/engineering/storage stills inspected. Detailed CC0 service prop, primitive structural fallback, some dim labels remain. No player/performance claim.
- **V02 UI:** local source import/compile and prefab generation exited 0 (`v02-ui-build.log`). Layout, domain-command interactions, timing display and actual scene binding remain unverified; not published as complete.
- **External technical build report:** Claude's verification chat reported a Windows build of main@43be436 plus visuals@55d4c36, tree 5eb251bc31255466a611d84ad65a8424ca6d884a, exit 0. Included only the visual preview scene, whose EditorOnly camera is stripped. This is explicitly not playable-game proof; see #7 for that machine's evidence.
- **Audio:** v1 clips/SFX were imported and decoded at 55d4c36. User acceptance supersedes its radio default: use new clean voice candidates; 01–10 regenerated locally, remaining new takes pending. Original mood loops generated and numerically checked, not yet integrated or approved by listening. No in-game playback claim.

GitHub milestones: **M0 Shared foundation**, **M1 Playable mission**, **M2 Cinematic submission build**. These group the work above; their existence is not completion evidence.

## Foundation verification ? 2026-11-14

- Unity **6000.3.28f1** batch import and C# compilation completed with exit code **0** in an isolated project copy. `OutOfReach.Contracts.dll` was emitted; all eight interfaces compile. Original open editor was not modified by the check.
- `python tools/verify_repository.py`: PASS (36 asset GUIDs, editor/version settings, JSON and generated-directory exclusions).
- `python tools/nasa/horizons_snapshot.py`: PASS (32 samples, raw/CSV hashes, vector ranges, light times and exact regeneration). Indexed raw/CSV bytes also match the verified source; raw JSON is exempt from Git newline conversion.
- GitHub repository is PRIVATE, owner has ADMIN, origin is the intended repository. Apoliak7777 has a pending write invitation. Git LFS is initialized.
- Headless import emitted package terrain/SpeedTree shader-dependency warnings. GPU rendering, gameplay tests, playable build and end-to-end flow are **not verified**. The Bootstrap scene is an empty integration placeholder.
- Local verification log: `.local/unity-baseline-check/editor.log` (ignored; contains machine-specific editor details).
- Source verification applies to this initial foundation commit; use `git rev-parse HEAD` after cloning to record its SHA.

Issue links: [C01](https://github.com/alex-slovakia/out-of-reach/issues/1), [V01](https://github.com/alex-slovakia/out-of-reach/issues/2), [I01](https://github.com/alex-slovakia/out-of-reach/issues/3), [C04](https://github.com/alex-slovakia/out-of-reach/issues/4), [R01](https://github.com/alex-slovakia/out-of-reach/issues/5).

## Current parallel work — 14 November 2026, 13:10 UTC

- Codex root: original-project synchronization, frozen I02 delivery, next C05/hands/celestial integration.
- Codex single UI writer: sparse in-scene HUD/planner redesign, exact UI path only; Claude confirmed no competing writer.
- Ready: verified GMAT star package at `docs/data/gmat-starfield-v01`; data preparation is not game rendering.
- Ready local handoffs: restrained first-person motion patch and C05 binding/collision review; Unity integration pending.
- Claude: cockpit6cd069d and handsf72224c delivered. Gameplay9044a00 delivered; sprint not started. Future model work depends on their capacity/owner.
- Capacity at13:07UTC: Codex99%weekly used,3 reset credits. Alex says he will activate one; completion not yet verified. Claude app at12:58UTC: Fable94%used,all-model49%used; resetNov15 18:00UTC.
