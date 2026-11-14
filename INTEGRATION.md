# Integration contracts and merge process

## Designated integrator

On 14 November 2026 Alex explicitly selected **Codex** to connect and verify the whole game.
The exact shared clock/deadline additions were agreed by both feature owners in [coordination issue #7](https://github.com/alex-slovakia/out-of-reach/issues/7).
Codex coordinates Bootstrap composition, shared contracts and integration evidence. This does not
transfer Claude-owned gameplay source or permit automatic merges to main. Routine task allocation
is autonomous, prioritizing the visual/gameplay result and avoiding duplicated agent work.

## Canonical contract surface

The C# files in `Assets/_OutOfReach/Scripts/Contracts/` are authoritative. Namespace: **`OutOfReach.Contracts`**. Read those files before implementing an adapter; this document describes responsibilities rather than maintaining a second copy of signatures.

| Contract | Implemented by | Used by | Boundary |
|---|---|---|---|
| `IGameState` | Claude Core | Presentation, interaction | Authoritative phase and transitions |
| `IMissionConfiguration` | Claude Inventory/Core | Planner, simulation, report | Validated equipment and engineering constraints |
| `IMissionPlanner` | Claude Core/Inventory | Codex planner view | Authoritative equipment catalog, editable draft and validated launch |
| `ISimulationClock` | Claude Core | Codex HUD and presentation | Read-only pause, simulation scale and elapsed authoritative time |
| `IPlanningDeadline` | Claude Core | Codex planner view | Read-only active-real-time deadline and immutable last-valid fallback |
| `IResourceSystem` | Claude Simulation | Gameplay commands, HUD | Quantities, snapshots and atomic consumption |
| `IInteractable` | Claude Interaction | Player raycast, prompt view | Availability, prompt and validated action |
| `IRepairable` | Claude Simulation/Interaction | Repair interaction, diagnostic view | System condition, requirements and repair result |
| `ICommunicationSystem` | Claude Simulation | Console view, finale | Connection state, distance and simulated signal timing |
| `IMissionEventBus` | Claude Core | Simulation publishers and presentation listeners | Typed events; effects observe confirmed domain changes |

The shared types and events in the contract assembly define exact names and payload units. If a task requires a missing member, submit a change request instead of adding a private competing interface. Keep contracts free of `MonoBehaviour`, scene references and presentation assemblies.

Initial contract event names: `PhaseChanged`, `ResourceChanged`, `SystemConditionChanged`, `CommunicationChanged`, `CinematicRequested`, `CinematicFinished`, `AirlockStateChanged`, `MissionEnded`. Phases: `Planning`, `Intro`, `Emergency`, `Repair`, `Eva`, `Transmission`, `Outcome`, `Report`. Outcomes: `None`, `RecoveryPossible`, `Lost`.

`IMissionPlanner` exposes `Catalog`, `Draft`, `TrySetEquipment(string equipmentId, int quantity)` and `TryLaunch()`. The planner view reads authoritative item costs and submits commands; it does not invent prices or mutate the loadout. Zero quantity removes equipment. Invalid IDs and changes outside Planning are rejected. An over-budget draft may remain visible for explanation, but launch validates constraints and freezes a defensive copy before Intro.

## Units and clock

- Distances: kilometres in astronomical data, metres in Unity scene geometry.
- Mass: kilograms. Water: litres. Power: watts. Stored energy: kWh. Time: seconds.
- Spendable resources are `OxygenKg`, `WaterLitres`, `FoodKg`, `BatteryKWh`, `SparePartsCount`. `CarbonDioxidePpm` is a concentration in the snapshot, not a spendable resource. Never label oxygen inventory as cabin oxygen percentage.
- Budget uses explicitly fictional planning credits unless a sourced currency model is introduced.
- One simulation clock drives consumption, repairs and signals. The HUD labels simulation-time acceleration. Pause freezes this clock; cinematic animation may continue on real time.
- `ISimulationClock.TimeScale` is simulated seconds per real second and retains its value while paused; elapsed time delegates to mission state, without a second counter. Presentation may choose to freeze effects on the same pause view.
- Timed planning is 60 active, unpaused real seconds; Practice disables it before planning starts. Core owns deadline advancement and the atomic launch race. On expiry launch the valid current draft, otherwise the disclosed immutable last-valid configuration, and record fallback use. Never silently substitute an undisclosed preset. UI reads `IPlanningDeadline`; it does not run another countdown. The Practice/Timed command is a public runtime method bound through the composition root, with its exact signature supplied in C02's handoff.

Compatibility of the 14 November additions: these are two new read-only interfaces; no existing member
changes. Claude implements them on its already matching clock/deadline views. Codex injects them
explicitly into the UI/presentation. Callers without these optional views remain usable for development,
but accelerated/timed gameplay is not release-ready without the corresponding visible indicators.
- Outbound light travel = documented distance / speed of light. An answer requires outbound travel, processing delay and inbound travel. Frozen-reference geometry and any compression must be disclosed.

## Event flow

1. Input or a view requests an action through an existing contract.
2. Claude's authoritative system validates prerequisites and costs, then mutates once.
3. It publishes the corresponding typed event from the shared contract surface.
4. Codex's view/effect listener updates the display or plays the appropriate presentation.

Subscribe when enabled and unsubscribe when disabled/disposed. Initial views read current snapshots before listening so loading a scene mid-mission does not show defaults. Do not publish from a rendering callback. A rejected command may show its reason, but must not play the successful repair or transmission effect.

Accident presentation does not itself damage resources. The mission controller applies the accident and publishes the phase/system changes. Presentation may publish `CinematicFinished` with the matching `SequenceId` and `WasSkipped`. Core ignores duplicate/stale acknowledgements and supplies a timeout fallback so a missing effect cannot strand gameplay. Other authoritative events are published by Core/Simulation. Any new callback requires coordination before changing shared types.

## Integration anchors

Visual prefabs provide the transforms listed in `ARCHITECTURE.md`. Claude provides compatible components and configuration requirements without editing those prefabs. The integrator binds them in Bootstrap. Doors have separate visual moving parts and gameplay gates; animating an airlock open does not authorize an unsuited player to exit.

At each handoff provide exact asset paths, component type names, required serialized fields, event subscriptions, collider expectations, camera/audio-listener behavior, tested commit, and known limitations. For preview-only materials, mocks or cameras, explicitly say how to exclude them from the release scene.

## First baseline and branch protocol

Codex assembles the initial baseline on `main`, verifies it, pushes it and records its SHA. Only then do feature owners branch. Claude uses `feature/survival-simulation`; Codex uses `feature/visuals-cinematics`. Work in separate local checkouts.

After baseline, shared files are locked by process. Coordinate an integrator, exact paths and intended change before editing them. The coordinator records both owners' agreement and a short compatibility note. No owner may resolve a conflict by overwriting the other branch's file.

## Merge checkpoint

1. Each owner finishes a small task, commits only owned files, runs applicable verification and supplies a handoff.
2. The designated integrator compares changed paths with ownership, checks contract compatibility and confirms the target/base commits.
3. The integrator obtains explicit merge coordination. An instruction to implement a feature is not permission to merge the teammate's branch.
4. Integrate into a dedicated integration branch, resolve conflicts with the affected owner and wire serialized references.
5. Run compile/tests, scene validation and the checkpoint's player flow. Record exact results and remaining risks.
6. Merge to main only after coordinated review of those results. Tag release candidates only when the executable is tested.

No test branch is automatically promoted. No force-push, history rewrite or deletion of teammate work.
