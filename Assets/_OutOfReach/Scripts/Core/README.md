# Claude gameplay domain — handoff

Branch `feature/survival-simulation`. Owner: Claude (Apoliak). This file is the running handoff for the
integrator; root documentation stays integrator-owned.

## C01 — mission foundation (status: see "Evidence")

Pure C# domain with no `UnityEngine` reference. It contains no repairs, player, EVA, communications or MonoBehaviours yet.

| Assembly | Path | References | Engine |
|---|---|---|---|
| `OutOfReach.Inventory` | `Scripts/Inventory` | Contracts | none (`noEngineReferences`) |
| `OutOfReach.Simulation` | `Scripts/Simulation` | Contracts | none |
| `OutOfReach.Core` | `Scripts/Core` | Contracts, Inventory, Simulation | none |
| `OutOfReach.Tests.EditMode` | `Tests/EditMode` | the above + Unity Test Framework | Editor only |

| Shared contract | Implementation |
|---|---|
| `IMissionEventBus` | `OutOfReach.Core.MissionEventBus` |
| `IGameState` | `OutOfReach.Core.MissionGameState` |
| `IMissionPlanner` | `OutOfReach.Core.MissionPlanner` |
| `IMissionConfiguration` | `OutOfReach.Inventory.MissionConfiguration` |
| `IResourceSystem` | `OutOfReach.Simulation.ResourceLedger` |
| `IInteractable`, `IRepairable` | C02/C03 (not yet) |
| `ICommunicationSystem` | C04 (not yet) |

`MissionSession` (Core) composes one mission: bus, state, planner, ledger, `SimulationClock`, `CinematicCoordinator`.
It is the only driver of simulated time.

## Binding recipe for the integrator

```csharp
// Composition (C02 adds the MonoBehaviour driver that does this inside Claude-owned paths).
var session = new MissionSession(AuroraMission.CreateDefinition(), UnityEngine.Debug.LogException);

// Presentation receives contract interfaces only:
IMissionPlanner planner = session.Planner;      // planner view: Catalog, Draft, TrySetEquipment, TryLaunch
IGameState state = session.State;               // phase / outcome / elapsed simulation seconds
IResourceSystem resources = session.Resources;  // HUD: read Snapshot first, then listen to ResourceChanged
IMissionEventBus events = session.Events;       // all typed events; CinematicFinished replies

// Once per frame, unscaled real seconds (clamp hitches so alt-tab cannot cause a catch-up drain):
session.Tick(Math.Min(Time.unscaledDeltaTime, 0.25));

// Restart: dispose and rebuild (or reload Bootstrap). Nothing carries over.
session.Dispose();
session = new MissionSession(AuroraMission.CreateDefinition(), UnityEngine.Debug.LogException);
```

## Behaviour presentation can rely on

- **Events after commit.** Every event is published after its mutation is committed. Launch delivers
  `ResourceChanged` and then `PhaseChanged(Planning → Intro)`.
- **Ordering.** A message published while another is being delivered is queued, first in, first out.
  Subscribers therefore always see commit order.
- **Isolation.** A throwing subscriber is reported to the error sink. Later subscribers still run, and
  gameplay is never rolled back.
- **Subscriptions.** Subscribing the same delegate twice is ignored, so a doubled `OnEnable` cannot duplicate a
  listener. Still unsubscribe in `OnDisable`. `Dispose()` on the session clears every subscription.
- **Phases.** The graph is Planning → Intro → Emergency → Repair ⇄ Eva → Transmission, with Repair → Transmission
  also allowed, and then Outcome → Report.
  - Any running phase may go to Outcome, but only through Core (`TryEndMission`). `TryTransition(Outcome)` is rejected.
  - Planning → Intro happens only through `IMissionPlanner.TryLaunch`.
  - Report is terminal; restart creates a new session.
  - A rejected or repeated transition publishes nothing.
- **Planner.**
  - `Catalog` lists one unit per entry with authoritative mass, power and credits, and is read-only.
  - `TrySetEquipment` replaces the count. Zero removes the item. Unknown or case-mismatched IDs, negative
    counts and counts above an item's limit are rejected. Edits outside Planning are rejected.
  - An over-limit draft stays visible. `Draft.Validate()` explains each violated constraint with units, for example
    `Mass 626.0 kg exceeds the 600.0 kg launch limit by 26.0 kg.`
  - `TryLaunch` validates and freezes a fresh copy. A second launch returns `AlreadyComplete`.
- **Resources.**
  - `TryConsume` validates every entry and sums duplicate resources before checking affordability.
  - Spending is all-or-nothing, and each successful spend publishes one event.
  - Negative, NaN or infinite amounts, fractional spare parts and unknown resources are rejected without
    mutation.
  - Cabin CO₂ is `Snapshot.CarbonDioxidePpm`. It is never spendable.
- **Clock.**
  - Simulation time runs only in Intro…Transmission.
  - `Clock.Pause()` discards real time, so resuming causes no catch-up drain. Paused time also stops the
    cinematic fallback timer.
  - `Clock.TrySetTimeScale(x)` sets simulated seconds per real second. The HUD must disclose any value other than 1×.
  - `AdvanceSimulation(seconds)` is the chapter-jump entry point. It splits exactly where a reserve reaches zero,
    so one long jump equals many frames.
- **Cinematic handshake.**
  - Core publishes `CinematicRequested(id, sequenceId)`. Sequence IDs are unique across sessions.
  - Presentation replies once with `CinematicFinished(sameId, sameSequenceId, wasSkipped)`.
  - Stale, duplicate and mismatched replies are ignored.
  - Without a reply, Core completes after 45 s of unpaused real time. With no presentation listener, it
    completes on the next tick.
  - Replying from inside the request handler (instant skip) is safe.

## AURORA-7 catalog and limits (GAMEPLAY ASSUMPTIONS — fictional vessel, fictional credits)

Launch limits: payload ≤ 600 kg, budget ≤ 800 credits, load ≤ generation, and supplies for 30 crew-days
(O₂ ≥ 25.2 kg, water ≥ 90 L, food ≥ 54 kg).

Vessel baseline: generation 1,300 W, load 1,100 W. Reserves: O₂ 10 kg, water 20 L, food 10 kg, battery 8 kWh,
1 spare part. Cabin CO₂ starts at 1,000 ppm.

| ID | Mass kg | Load W | Credits | Max | Adds |
|---|---:|---:|---:|---:|---|
| `oxygen-tank` | 30 | 0 | 40 | 6 | +12 kg O₂ |
| `water-tank` | 48 | 0 | 20 | 6 | +40 L water |
| `food-locker` | 35 | 0 | 25 | 6 | +27 kg food |
| `battery-module` | 55 | 0 | 60 | 4 | +10 kWh (launches full) |
| `solar-wing` | 70 | 0 | 120 | 3 | +600 W generation |
| `spare-parts-kit` | 25 | 0 | 45 | 5 | +3 spare parts |
| `spectrometer` | 90 | 300 | 220 | 1 | science objective |

| Preset | Mass | Credits | Load / generation | O₂ / battery / parts | Science |
|---|---:|---:|---:|---:|---|
| `science-focus` (default) | 546 kg | 720 | 1,400 / 1,900 W | 34 kg / 28 kWh / 7 | yes |
| `redundancy` | 591 kg | 690 | 1,100 / 1,900 W | 46 kg / 38 kWh / 13 | no |

The trade-off: the spectrometer needs a solar wing to stay within power, and together they consume mass and budget
that would otherwise buy spare parts, batteries and oxygen for the emergency. Repairs in C02 will cost parts
and energy, so the loadout decides which repairs are affordable.

## Assumptions to review

- `CrewMetabolicAssumptions` (0.84 kg O₂, 3.0 L water and 1.8 kg food per crew-day; one crew member) is
  **gameplay tuning, not sourced physiology**. Verify any citation (for example NASA BVAD) before it appears in the report.
- With working scrubbers, nominal cabin CO₂ is held constant. The emergency model (C02) changes this.
- Batteries launch full; capacity equals initial charge. Generation above load recharges up to capacity.

## Contract questions for the integrator (no shared files were edited)

1. **Clock visibility.** The HUD must disclose acceleration and pause, but no contract exposes them. Proposed:
   `ISimulationClock { bool IsPaused; double TimeScale; double ElapsedSimulationSeconds; }`. Read-only;
   `MissionSession.Clock` would implement it.
2. **Restart command.** No contract carries a restart request. Proposed: wire the report's Restart button
   (UnityEvent) to the Claude-owned runtime component's `RestartMission()` in C02/C04, or reload Bootstrap. No
   contract change is needed if this is acceptable.
3. **Equipment text.** `EquipmentSelection` has no display name or effect text. Either Codex keeps an ID → text
   table (IDs above are stable), or an `EquipmentDescriptor` contract is added. `EquipmentDefinition` already
   holds `DisplayName`/`Summary`.
4. **Third ending.** The concept infographic shows rescue / survive longer / failure. The contract has
   `RecoveryPossible` / `Lost`, and PRODUCT.md says a survivable holding state lives inside the success ending.
   Plan: "survive longer" becomes a report variant of `RecoveryPossible`, unless the integrator adds an enum value.
5. **Report data (C04)** will need a small read-only report contract; it will be proposed before C04 starts.

## Evidence (C01, 2026-11-14, Apoliak's machine)

- **Unity 6000.3.28f1 (8f082d2fe476), Windows 11.** Batch mode `-runTests -testPlatform EditMode` (TESTING.md
  command, no `-quit`) on exactly these sources:
  - **77 passed, 0 failed, 0 skipped**; exit code 0; NUnit 3.5.
  - Per fixture: CinematicCoordinator 9, EventBus 7, GameState 18, Planner 11, ResourceLedger 23, SessionClock 9.
  - Results and log stay local (`TestResults/`, ignored).
- **Compiler.** No compiler warnings or errors from the `OutOfReach.*` assemblies in the Unity log. A standalone
  compile with Unity's bundled Roslyn (C# 9, netstandard 2.1, warning level 4) was also clean.
- **Repository checks.** `python tools/verify_repository.py` PASS (76 asset GUIDs);
  `python tools/nasa/horizons_snapshot.py` PASS.
- **Import side effect, not committed.** On first import, Unity/URP upgrades the shared
  `Assets/Settings/Mobile_RPAsset.asset` from asset version 12 to 13. The integrator may want to commit the upgraded
  asset on `main` so clean clones stay clean.
- **Not verified yet.** PlayMode, scenes, build and gameplay: C01 has none of them.
- **Machine note.** Unity Hub 3.22.6 from winget (MSIX) ships Licensing Client 1.18.3. That client rejects editor
  6000.3.28f1's protocol 1.19.0, and the MSIX Hub stores the license in its private package folder, so batch mode
  reported "No valid Unity Editor license".
  - Workaround used: copy `UnityEntitlementLicense.xml` from
    `%LOCALAPPDATA%\Packages\UnityTechnologies.UnityHub_*\LocalCache\Local\Unity\licenses\` to
    `%LOCALAPPDATA%\Unity\licenses\`.
  - Launch Unity without a shared console (no `-NoNewWindow`), or the licensing client's Ctrl-C ends the run.
