# C01 — Claude Opus 5.5 — mission foundation

Use Claude Code in Apoliak's separate clone on `feature/survival-simulation`. Select Claude Opus 5.5 before submitting this prompt.

You are the gameplay/simulation owner for OUT OF REACH, a Unity **6000.3.28f1** URP/C# Windows game. First read AGENTS.md, PRODUCT.md, ARCHITECTURE.md, INTEGRATION.md, NASA-DATA.md, TESTING.md, TASKS.md and every file in `Assets/_OutOfReach/Scripts/Contracts/`. Verify origin is `alex-slovakia/out-of-reach`, the baseline exists on main, and report your starting commit and clean/dirty status. Preserve existing work. Do not upgrade Unity/packages.

Implement **C01 only**. Allowed paths: `Assets/_OutOfReach/Scripts/Core/**`, `Simulation/**`, `Inventory/**`, `Tests/**` and their metadata. Do not edit Contracts, Bootstrap, root docs, packages, project settings or any Codex-owned file. If a shared contract is insufficient, report a precise change request instead of replacing it.

Deliver a minimal deterministic domain foundation:

1. Implement the existing `IMissionEventBus`, `IGameState`, `IMissionConfiguration`, `IMissionPlanner` and `IResourceSystem` contracts. Do not implement repairs, player movement, EVA or cinematics yet.
2. Implement guarded phase transitions. A domain transition happens once; invalid or repeated transitions do not duplicate effects. Use the contract's exact enum and event types. Core is authoritative; presentation only acknowledges the matching cinematic sequence. Handle stale/duplicate acknowledgement and a missing-listener timeout without requiring a scene.
3. Provide a small authoritative equipment catalog with at least two valid loadouts and genuine mass/budget/power trade-offs. Treat prices/rates as explicit game assumptions. `TrySetEquipment` edits the draft only in Planning; zero removes an item. `TryLaunch` validates, freezes a defensive copy and enters Intro once. Over-limit drafts may be displayed but cannot launch. Do not create a planner UI.
4. Implement finite, bounded quantities with the exact resource units. Consumption is all-or-nothing across the whole request, including duplicate resource IDs. Negative/NaN/infinite costs or time deltas are rejected. CO₂ is a concentration, not a currency. Use one explicit simulation clock with a defined pause behavior. Do not invent physiology precision; document assumptions in code/handoff for integrator review.
5. Add meaningful EditMode tests for invalid loadouts, launch freezing, invalid/duplicate actions, event unsubscribe, resource atomicity, boundaries, timestep consistency and pause/restart semantics. Use the existing Unity Test Framework. No new packages. Keep fixtures under your Tests folder.

Compile and run tests in exact Unity 6000.3.28f1, using a separate verification checkout if Unity already has this project open. Run repository/data verification commands from TESTING.md where available. Report actual evidence or exact blocker; never claim execution from inspection alone.

Commit only your owned files with a concise C01 commit. Do not merge main or edit task-status docs. At the end provide: commit SHA, changed paths, concrete implemented class names, a brief construction/binding example against contracts, test results/log paths, and remaining assumptions. Stop at this checkpoint; do not start C02 until C01 is reviewed.
