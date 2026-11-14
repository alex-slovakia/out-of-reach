# C03 then C04 — Claude Opus 5.5 — exterior and communications

Use the existing gameplay Claude Code chat on `feature/survival-simulation`, Unity **6000.3.28f1**. Read all current project rules, contracts, NASA-DATA.md and the latest integration handoff. These are **two sequential tasks**, not authorization to generate both at once.

## C03: start only after I01 passes

Stay in Claude-owned paths. Implement suit/airlock prerequisites, a short constrained EVA movement route, antenna repair and safe return. Support a deterministic depletion/failure path. Unsuitable suit state, unsafe door state or missing prerequisites prevent exit. Visual animation never overrides the gameplay gate. Avoid a full free-flight simulator or tether physics system; use a documented constrained route and explicit limits.

Use existing `IInteractable`, `IRepairable` and typed airlock/mission events. Airlock loss and suit reserves follow clear units and tuning assumptions. Ensure insufficient resources/repeated actions cannot consume costs twice. Test entry/exit safety, repair, safe return, pause and failure. Use test fixtures under Tests; do not edit Codex exterior/door prefabs.

Compile/test, commit and hand off serialized bindings for `AirlockInner`, `AirlockOuter`, `EvaStart`, `AntennaRepair` and `EvaReturn`. Stop at C03. Continue to C04 only after this result is reviewed and integrated with the visual EVA.

## C04: start only after C03 and D01 are verified

Implement the existing `ICommunicationSystem` plus the domain outcome/report data using the committed dataset:

- Read `docs/data/horizons/manifest.json` and `earth-mars-2026-11-14--2026-12-15.csv`. Verify the snapshot offline with `python tools/nasa/horizons_snapshot.py`.
- Package an immutable runtime copy/configuration **within an owned Simulation path** only after checking the existing data handoff. Preserve its reference ID, epoch, source and units. Do not edit the canonical shared dataset or make runtime network calls.
- Use frozen geometric Earth–Mars range as an explicitly labeled Mars-vicinity proxy. Distance decreases in this dataset; do not write dialogue that claims these rows show Earth receding. Do not hardcode the illustrative 18-minute story delay.
- Enforce antenna/power requirements and one validated transmission. Reply timing includes outbound light travel, Earth processing and inbound travel. Show real physical time separately from any simulation acceleration; pause and chapter skips use the same simulation clock.
- Implement `RecoveryPossible` and `Lost`, followed by report data and clean restart. Success means contact and recovery planning, not an instantly arriving rescue craft.
- Include report facts: loadout, science objective/return, repair costs, remaining resources, failure cause or recovery conditions, NASA reference/epoch, simplifications and time compression. Coordinate any missing report contract before adding shared types.

Test known data rows, units, early-reply prevention, pause, rejected/repeated transmission, insufficient power, both outcomes and restart. If any clock advance jumps across failure or reply thresholds, resolve events deterministically without silently skipping resource failure.

Commit only owned files. Report tests, exact interface implementations, reference ID and UI binding requirements. Stop before branch merge; request I02 coordination. Do not implement Codex's transmission animation, voice playback or report UI.
