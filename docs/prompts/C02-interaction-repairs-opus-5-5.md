# C02 — Claude Opus 5.5 — movement, interaction and interior repair

Continue in the existing Claude Code gameplay chat on `feature/survival-simulation` using Unity **6000.3.28f1**. Read the project rules, actual contracts and C01 handoff. Verify C01 tests pass before editing. If starting a new chat, also read the root master prompt.

Implement **C02 only** in Claude-owned Core/Simulation/Interaction/Player/Inventory/Tests paths. Do not edit Codex prefabs/scenes/UI or shared files. Use a plain test fixture under Tests to prove behavior. The integrator attaches your components to the visual anchors later.

Deliver:

- First-person mouse-look and movement using the existing Input System. Keep camera ownership explicit; no second camera/listener in production. Support pause, restored cursor, bounded look and clear speed units. Input actions/configuration must remain within your owned folder; don't overwrite the template's root input asset.
- A short-range interaction resolver against the exact `IInteractable` contract. Block actions outside range, through occluding walls or when unavailable. Expose prompt state for Codex's view without authoring UI.
- Power and life-support repair gameplay implementing `IRepairable`. Requirements come from the domain model; charge costs atomically, update condition once and publish the contract events. An unsuccessful repair changes nothing. Repeated interaction cannot charge again or duplicate a success event.
- One consistent emergency state with gameplay consequences from chosen equipment. Oxygen supply, battery energy, CO₂ removal and repair state remain internally consistent; the presentation does not change these values.
- Meaningful tests for line-of-sight/range gates, insufficient costs, successful/repeated repair, depletion, pause and restart. Verify scene fixtures have valid references and no missing scripts.

Do not add EVA, antenna transmission, new packages, complex inventory UI or crafting trees. Do not alter the shared contract to make integration convenient; coordinate a change request if needed.

Run Unity compile and relevant EditMode/PlayMode tests. Commit only owned files. Report exact class names, serialized bindings needed for `PlayerSpawn`, `PowerRepair` and `LifeSupportRepair`, camera expectations, tests, commit and known limitations. Stop for **I01: plan → accident → interior repair** integration. Do not merge the visual branch yourself.
