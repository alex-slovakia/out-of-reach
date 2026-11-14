# Execution plan and status

**Deadline:** November 15, 2026, 23:59 in the team creator's local timezone. Confirm that timezone. Suggested internal release cutoff: at least two hours earlier. All team planning below uses Europe/Bratislava unless explicitly labeled otherwise.

Status vocabulary: `in progress`, `ready`, `blocked`, `not started`, `verified`. An artifact existing is not proof that its tests pass.

| ID | Priority / owner | Task | Depends on | Acceptance checkpoint | Status |
|---|---|---|---|---|---|
| F00 | P0 / integrator | Audit existing Unity, pin 6000.3.28f1, repository, ignore rules and LFS | — | Remote/private access checked; source-only baseline; no changed original GUIDs | Verified foundation |
| F01 | P0 / integrator | Product, architecture, contracts, ownership and prompt pack | F00 | Eight interfaces including planner bridge compile; both developers can state their boundaries | Verified foundation |
| D01 | P0 / integrator | Reproducible NASA/JPL snapshot and assumptions | F00 | Raw source, request, epoch, units and derived distance/delay checked | Verified offline; runtime integration remains C04 |
| C01 | P0 / Claude | Mission phases, event bus, mission planner/configuration and resource accounting | F01 | Deterministic EditMode tests; invalid actions consume nothing; no visual dependencies | Not started |
| V01 | P0 / Codex | Modular interior and visual preview | F01 | Cockpit, engineering, storage, airlock; named anchors; compile and scene inspection | Not started |
| C02 | P0 / Claude | Movement, interaction, power/life-support repair | C01 | Walk/interact in test fixture; repair costs atomic; repeated use safe | Not started |
| V02 | P0 / Codex | Planner/HUD views and accident presentation | V01, contracts | Readable constraints/status; event-driven blackout/alarms; input/camera restored | Not started |
| I01 | P0 / coordinated integrator | First connected playable: plan → accident → interior repairs | C02, V02 | Fresh loadout changes resources; gameplay and visuals agree; clean Console | Not started |
| C03 | P0 / Claude | Suit/airlock gates, constrained EVA, antenna repair | C02 | No unsuited exit; return possible; resource failure deterministic | Not started |
| V03 | P0 / Codex | Airlock animation, exterior route and EVA presentation | V01 | Anchors/collisions readable; no duplicate player/camera; no vacuum air sounds | Not started |
| C04 | P0 / Claude | NASA data consumption, delayed transmission, outcomes and report data | C01, D01, C03 | Round-trip timing correct; success/failure/restart tests; source included | Not started |
| V04 | P0 / Codex | Communications finale and report views | V02, C04 contract readiness | Correct countdown/units, delayed voice, final camera, report and restart UI | Not started |
| I02 | P0 / coordinated integrator | Complete mission integration | C03, C04, V03, V04 | One complete success and one failure playthrough from executable | Not started |
| R01 | P0 / team | Polish, accessibility, performance and release | I02 | Windows x64 build, reproducible launch, credits/data, capture and upload buffer | Not started |

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

The integrator adds measured evidence here. No gameplay, build or end-to-end result is recorded yet.

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
