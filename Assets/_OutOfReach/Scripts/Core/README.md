# Claude gameplay domain — handoff

Branch `feature/survival-simulation`. Owner: Claude (Apoliak). This file is the running handoff for the
integrator; root documentation stays integrator-owned. Status and requests live on issue #7.

**Done:** C01 (mission foundation), C02 (first playable repair loop) and C03 (suit, airlock, constrained EVA,
antenna repair). **Not yet:** communications, outcomes for success, report data (C04).

## Assemblies

| Assembly | Path | References | Engine |
|---|---|---|---|
| `OutOfReach.Inventory` | `Scripts/Inventory` | Contracts | none (`noEngineReferences`) |
| `OutOfReach.Simulation` | `Scripts/Simulation` | Contracts | none |
| `OutOfReach.Core` | `Scripts/Core` | Contracts, Inventory, Simulation | none |
| `OutOfReach.Interaction` | `Scripts/Interaction` | Contracts | Unity |
| `OutOfReach.Player` | `Scripts/Player` | Contracts, Interaction, Unity.InputSystem | Unity |
| `OutOfReach.Core.Runtime` | `Scripts/Core/Runtime` | all of the above | Unity |
| `OutOfReach.Tests.EditMode` | `Tests/EditMode` | pure assemblies + Test Framework | Editor |
| `OutOfReach.Tests.PlayMode` | `Tests/PlayMode` | all + Test Framework | tests only |
| `OutOfReach.Tests.Editor` | `Tests/Editor` | PlayMode tests + UnityEditor | Editor |

| Shared contract | Implementation |
|---|---|
| `IMissionEventBus` | `OutOfReach.Core.MissionEventBus` |
| `IGameState` | `OutOfReach.Core.MissionGameState` |
| `IMissionPlanner` | `OutOfReach.Core.MissionPlanner` |
| `IMissionConfiguration` | `OutOfReach.Inventory.MissionConfiguration` |
| `IResourceSystem` | `OutOfReach.Simulation.ResourceLedger` |
| `IRepairable` | `OutOfReach.Simulation.ShipSystem` (Power, LifeSupport, Antenna) |
| `IInteractable` | `OutOfReach.Core.CockpitConsoleInteraction`, `RepairInteraction`, `DelegatedInteraction` |
| `AirlockState` / `AirlockStateChanged` | published by `OutOfReach.Core.EvaOperations` |
| `ICommunicationSystem` | C04 (not yet) |
| `ISimulationClock` | `OutOfReach.Core.SimulationClockView` |
| `IPlanningDeadline` | `OutOfReach.Core.PlanningDeadline` |

The two newest contracts come from the integrator's shared-only commit `39dfb92` on `feature/visuals-cinematics`,
adopted here by cherry-pick as `50b2f8a` (the visuals branch itself was not merged; `TEAM-COMMANDS.md` was not
taken, so it arrives once from the integrator's branch).

## Scene integration (I01: plan → accident → interior repair)

Everything below is Claude-owned and is bound in Bootstrap by the integrator. No Codex prefab is edited.

1. **Mission root.** Add `OutOfReach.Core.Runtime.MissionRuntime` to one GameObject. It creates the session in
   `Awake`, is the only simulation driver (one step per frame) and must exist exactly once (a second instance
   disables itself with an error).
2. **Player.** Place `Assets/_OutOfReach/Scripts/Player/PlayerRig.prefab` at `PlayerSpawn`.
   - The rig has a `CharacterController` (1.8 m tall, 0.3 m radius), `FirstPersonController` and a `ViewPivot`
     child at 1.65 m with the `InteractionResolver` (reach 2 m).
   - It has **no camera and no audio listener**. Parent the single Bootstrap gameplay camera under `ViewPivot`.
3. **Interaction points.** On a gameplay child at each anchor add a collider (a trigger works and does not block
   walking) and `OutOfReach.Interaction.InteractableTarget` with one of these IDs:

   | Anchor | `interactionId` | What it does |
   |---|---|---|
   | `CockpitConsole` | `cockpit-console` | Intro: "Run cockpit systems check" triggers the impact once |
   | `PowerRepair` | `repair-power` | Repairs power distribution: 2 spare parts |
   | `LifeSupportRepair` | `repair-life-support` | Repairs life support: 2 spare parts + 1 kWh |

4. **Wire `MissionRuntime` in the Inspector:** `Player` = the rig, `Player Spawn` = the `PlayerSpawn` anchor,
   `Interaction Targets` = the three targets. Unknown IDs are reported as errors at start.
5. **Presentation gets contracts only.** From the runtime component: `Events`, `GameState`, `Planner`,
   `Resources`, `Configuration`, `Repairables`, `Clock`, `PlanningDeadline`, `TryGetInteractable(id, out ...)`.
   - Views read current state first, then subscribe in `OnEnable` and unsubscribe in `OnDisable`.
   - `Tests/PlayMode/Harness/MissionStatusListener.cs` is a working example of this pattern.
6. **No-code wiring (UnityEvents and public methods):**
   - `InteractionResolver.PromptChanged(string prompt, bool canInteract)`: prompt view. Empty text = nothing in focus.
   - `InteractionResolver.InteractionAttempted(bool succeeded, string explanation)`: feedback line.
   - `MissionRuntime.PauseChangedEvent(bool)` and `MissionRuntime.TogglePause()` / `SetPaused(bool)`: pause menu.
   - `public void MissionRuntime.BeginTimedPlanning()`: the Practice/Timed entry point.
     - Lifecycle: every new session starts in Planning with the deadline disabled (practice).
     - Call it once, before or during planning, to arm the deadline (`Planning Deadline Real Seconds`, default 60).
       It cannot be disarmed afterwards and needs a valid loadout to fall back to.
     - Never calling it keeps practice planning. Read the countdown from `MissionRuntime.PlanningDeadline`.
   - `MissionRuntime.RestartMission()`: report Restart button. Disposes the session and reloads the active scene.
     In a scene outside the build list it rebuilds the session in place and logs a warning.

Controls are defined in code (`PlayerInputActions`; the shared input asset is untouched): WASD or arrows, mouse
look, E or left mouse to interact, Esc to pause; gamepad sticks, south button, Start.

**Try it by hand:** open `Assets/_OutOfReach/Tests/Harness/RepairLoopHarness.unity` (test fixture, not in the
build list) and press Play. Enter launches the default loadout, R restarts. Regenerate the scene and the prefab
with the menu `OUT OF REACH/Tests/Rebuild repair-loop harness scene and PlayerRig prefab`.

## Behaviour presentation can rely on

- **Events after commit, in commit order.** A message published during a dispatch is queued. A throwing
  subscriber is reported and never rolls gameplay back. Subscribing the same delegate twice is ignored.
- **Phases.** Planning → Intro → Emergency → Repair ⇄ Eva → Transmission → Outcome → Report (Repair →
  Transmission also legal). A legal edge still needs its mission condition, whoever asks:
  - Planning → Intro: only `IMissionPlanner.TryLaunch` (or the planning deadline).
  - Intro → Emergency: only after the impact sequence finished.
  - Emergency → Repair: automatic on the first interior repair.
  - → Eva: power and life support operational (C03 adds suit and airlock).
  - → Outcome: only Core. Report is terminal; restart builds a new session.
- **Accident.** The cockpit check triggers it once. Order of events: `CinematicRequested(Impact)`, then
  `SystemConditionChanged` for Power (0.2), LifeSupport (0) and Antenna (0), then after the cinematic
  `CinematicFinished` and `PhaseChanged(Intro → Emergency)`.
  - Presentation replies once with `CinematicFinished(sameId, sameSequenceId, wasSkipped)`.
  - Without a reply Core continues after 45 s of unpaused real time; with no listener, on the next frame.
  - Player input is locked while the cinematic is active and always returns.
- **Repairs.** Costs come from the domain. A repair debits all costs and restores the condition in one
  transaction: `ResourceChanged`, `SystemConditionChanged(id, 1)`, then possibly `PhaseChanged`. A rejected or
  repeated repair spends nothing and publishes nothing. The antenna is repairable only in the Eva phase.
- **Prompts** always state the real cost or the exact reason, for example
  `Repair life support: 2 spare parts, 1 kWh.` or
  `Cannot repair power distribution: needs 2 spare parts, 1 spare part available.`
- **Interaction focus.** One ray from the view pivot, reach 2 m. A solid collider in front of the target blocks
  it; other trigger volumes are ignored. Nothing is targetable while the player has no control.
- **Clock.** Runs only in Intro…Transmission. `Pause` discards real time and blocks both `Tick` and
  `AdvanceSimulation`. Time scale per phase (GAMEPLAY TUNING, must be shown on the HUD): Intro ×1,
  Emergency/Repair ×300 (1 s = 5 min), Eva ×60 and Transmission ×60 (placeholders until C03/C04). Maximum ×86,400.
- **Chapter jumps.** `MissionSession.AdvanceSimulation(seconds)` splits exactly at every reserve depletion and
  at the CO₂ limit, re-evaluates the rates and applies the survival rules synchronously after each part. A
  jump that crosses a fatal boundary ends the mission at that exact time and discards the rest.
- **Planning deadline.** Counted in unpaused real seconds, off unless started. At zero it launches the current
  draft if valid, otherwise the last valid loadout (`UsedLastValidLoadoutAtDeadline = true`). Once, atomically,
  and the launch frame spends no simulated time. Starting it without any valid loadout fails explicitly.

## C03: suit, airlock and the constrained EVA

### What the player does

1. **Suit locker** (`suit-locker`): puts the suit on. Needs power and life support repaired. Costs 0.84 kg of
   stored oxygen and jumps simulated time by 2 h 20 min of preparation.
2. **Airlock controls** (`airlock-controls`, inside the airlock): "Seal inner hatch and depressurize". Costs
   0.11 kg oxygen and 0.2 kWh, seals the suit and enters the Eva phase. After 10 simulated minutes the outer
   hatch is open.
3. **Outer hatch, inside** (`eva-exit`): "Clip tether and go outside". The player is moved onto the route.
4. **Route** to the antenna work site, then `repair-antenna`: 3 spare parts and 0.5 kWh, only while outside.
5. **Outer hatch, outside** (`eva-return`): "Enter the airlock". Always possible, costs nothing.
6. **Airlock controls** again: "Close outer hatch and repressurize". Refused while the player is outside. After
   10 simulated minutes the inner hatch opens, the suit comes off and the mission goes to Transmission if the
   antenna works, otherwise back to Repair (a second EVA pays the suit and airlock costs again).

### Guarantees

- **Hatches are never open together.** They are distinct values of the shared `AirlockState`; events arrive in the
  order `Sealed`, `Depressurizing`, `OuterOpen`, `Repressurizing`, `InnerOpen`.
- **No unsuited or unsafe exit.** Going outside needs a sealed suit and `OuterOpen`. Direct phase requests cannot
  bypass the airlock. A door animation changes nothing: the gameplay gates below decide.
- **Suit oxygen.** While sealed, the crew breathes the suit tank (0.30 kg at 0.1 kg/h = 3 h), not the ship reserve,
  and puts no CO2 into the cabin. If it runs out before repressurization completes, the mission is `Lost` at
  that exact time, with the reason saying where ("outside the ship" or "in the airlock").
- **Interrupted repair.** An antenna repair without enough parts or energy spends nothing; the return still works.
- **Pause and chapter jumps** apply to the suit and the airlock timers like to everything else.

### Scene bindings for V03 and I02 (all Claude-owned; no Codex prefab is edited)

| Where | Component and ID | Notes |
|---|---|---|
| `AirlockInner` | `GameplayGate`, `airlock-inner`, with a collider filling the doorway | Walkable only while the airlock is `InnerOpen` |
| `AirlockOuter` | `GameplayGate`, `airlock-outer`, with a collider filling the hatch | Never walkable: the hatch is crossed only through the two interactions below |
| `AirlockOuter`, inside face | `InteractableTarget`, `eva-exit` | |
| `EvaReturn` | `InteractableTarget`, `eva-return` | Outside, within 2 m of the first waypoint |
| `AntennaRepair` | `InteractableTarget`, `repair-antenna` | Within 2 m of the last waypoint |
| Inside the airlock | `InteractableTarget`, `airlock-controls` | No anchor exists yet: place it on a wall between the hatches |
| Cabin or airlock | `InteractableTarget`, `suit-locker` | No anchor exists yet: place it at the suit stowage |
| `EvaStart` ... work site | `OutOfReach.Player.TetheredRoute` with waypoint transforms | First waypoint = `EvaStart` (feet position); last one about 1.2 m short of the antenna |
| Inside by the outer hatch | any transform as `Eva Reentry Point` | Feet position and heading after re-entry |

`MissionRuntime` Inspector fields: `Gates` (both gates), `Eva Route`, `Eva Reentry Point`, plus the five new targets
in `Interaction Targets`. The runtime opens and closes the gates every frame and clips the player to the route
exactly while the mission says the crew is outside.

On the route the player has no gravity and no free flight: movement follows the view direction, is clamped to
0.5 m around the waypoint line and cannot pass either end (tether length = line length, 1 m/s). These limits are
serialized on `TetheredRoute`.

### EVA HUD data (no contract yet)

`MissionRuntime.Eva` returns `OutOfReach.Core.EvaOperations`: `IsSuitOn`, `IsSuitSealed`, `SuitOxygenKg`,
`SuitOxygenCapacityKg`, `SuitOxygenSecondsRemaining`, `IsOutside`, `Airlock`, `AirlockTransitionRemainingSeconds`
and a `Changed` event. Proposal for the integrator: a read-only `IEvaStatus` contract with exactly these members.
Until it exists, the integrator can adapt the properties. Airlock door visuals only need `AirlockStateChanged`.

### EVA assumptions (GAME ASSUMPTIONS for a fictional suit and airlock)

| Value | Used | Reference it follows (other designs, not a universal minimum) |
|---|---|---|
| Preparation time | 2 h 20 min | ISS EMU exercise prebreathe, 80 + 60 min (BVAD REV2 section 4.6.3) |
| Suit purge oxygen | 0.54 kg per EVA | xEMU prediction (BVAD REV2 section 4.6.2) |
| Suit tank / use | 0.30 kg / 0.1 kg per hour | xEMU prediction of 0.1 kg/h |
| Airlock loss | 10 % of the air in 4 m³ = 0.11 kg O2 per cycle | about 10 % per cycle (BVAD REV2 section 4.6.2) |
| Pump energy | 0.2 kWh per cycle | none |
| Depressurize / repressurize | 10 min each | none |

A solo EVA is itself a fictional emergency: the cited protocol requires two crew members. The report (C04)
should say so. Time scale in the Eva phase is ×60 (placeholder; tuning in `MissionPacing`).

## Emergency model (one model for nominal flight and the accident)

| Input | Effect |
|---|---|
| Power condition | Generation reaching the bus = configured generation × condition (20 % after the impact) |
| Life-support condition | CO₂ removal, its 300 W load and the oxygen leak scale with it (0 after the impact) |
| Battery empty and load > generation | Brownout: no stored energy, scrubbers stop even if repaired |

- Cabin CO₂ rises by the crew's output with no scrubbing: about 0.32 ppm/s (1,157 ppm per hour) in the 20 m³
  cabin, derived with the ideal gas law. Running scrubbers draw it back down to the cabin baseline.
- A failed life-support loop leaks 2 kg of stored oxygen per day (GAMEPLAY TUNING).
- After the impact the science-focus loadout reaches brownout in about 38.9 h, the redundancy loadout in
  about 90.5 h (more battery, no 300 W spectrometer).
- The three repairs cost 7 spare parts: exactly what science-focus carries; redundancy keeps 6 in hand.

**Endings in C02 (all `Lost`, with a causal reason in `MissionEnded.Reason`):**

| Cause | Rule |
|---|---|
| Cabin CO₂ | Reaches 2.67 kPa (about 26,350 ppm): about 20.5 h after scrubbers stop |
| Stored oxygen | Not instant: the cabin's own air stays above 18.6 kPa p[O₂] for about 19.4 h, then lost |
| Water or food | Reserve exhausted (simplification) |
| Battery | Never an ending by itself; it stops the scrubbers, then CO₂ decides |
| Suit oxygen | A sealed suit runs empty before repressurization completes |

## Sources and labels

- **NASA reference values** (BVAD REV2, NASA/TP-2015-218570/REV2, checked by the science review SCIENCE-01):
  O₂ 0.84 and CO₂ 1.01 kg per crew-day (Table 3-23), water 2.5 kg (Table 4-20), packaged food 1.83 kg
  (Table 4-40). Cabin baseline p[CO₂] 0.267 kPa and p[O₂] 21.2 / 18.6 kPa (Table 4-1). ISS flight-rule levels
  1.01 kPa (action) and 2.67 kPa (emergency) from section 4.1.1.
- **Derived physics:** ppm from CO₂ mass and the oxygen buffer, both for the assumed cabin.
- **Game assumptions:** one crew member, 20 m³ cabin at 101.325 kPa and 295 K, all catalog values, damage
  levels, repair costs, leak rate, scrubber drawdown, time scales. The CO₂ and O₂ endings use cited operational
  levels as game endings; they are not medical claims.
- These citations should be added to `NASA-DATA.md` by the integrator before the report shows them.

## AURORA-7 catalog and limits (GAME ASSUMPTIONS — fictional vessel, fictional credits)

Launch limits: payload ≤ 600 kg, budget ≤ 800 credits, load ≤ generation, and supplies for 30 crew-days
(O₂ ≥ 25.2 kg, water ≥ 75 L, food ≥ 54.9 kg).

Vessel baseline: generation 1,300 W, load 1,100 W. Reserves: O₂ 10 kg, water 20 L, food 10 kg, battery 8 kWh,
1 spare part. Cabin CO₂ starts at the 2,635 ppm baseline.

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

## Requests and review findings handled in C02

| ID | Result |
|---|---|
| C01-PAUSE | Fixed. `AdvanceSimulation` is refused while paused; regression covers `Tick` and `AdvanceSimulation` |
| C02-BOUNDARY | Built in. Survival rules run synchronously after every split of an advance, not from a listener |
| C01-FINITE | Fixed. CO₂ capped at 1,000,000 ppm, time scale at ×86,400, elapsed-time overflow rejected before mutation |
| COORD-DECISIONS-01 | `ISimulationClock` and `IPlanningDeadline` adopted and implemented; restart reloads the scene; a manual launch and the expiry can never both launch (tested both orders) |
| Verifier findings 1–8 on `caee234` | Fixed: callback isolation on the timeout path, disposed sessions reject commands, one clock gate for ledger and state, mission end reads the ledger snapshot, two weak tests rewritten |

## Open points

1. **Wait-to-fail pacing.** A player who cannot repair anything waits for CO₂ (about 4 real minutes at ×300).
   Options for I01: a "rest" interaction that jumps time, or a faster scale. Tuning lives in `MissionPacing`.
2. **Prompt contract.** The focus prompt is exposed through UnityEvents and properties, not a contract. A small
   read-only contract can be proposed if Codex prefers code binding.
3. **Report data (C04)** still needs the small read-only report contract.
4. **PlayerRig camera.** The integrator parents the Bootstrap camera under `ViewPivot`; the rig never creates one.
5. **Suit-up time jump.** The 2 h 20 min preparation is a real jump of simulated time with no event of its own.
   A chapter card can key on the `suit-locker` interaction result or on `EvaOperations.Changed`.
6. **EVA anchors.** `airlock-controls` and `suit-locker` have no anchor in the visual prefab yet, and the route
   needs waypoint transforms along the hull from `EvaStart` to the antenna (V03).

## Evidence (C03, 2026-11-14, Apoliak's machine, Unity 6000.3.28f1 batch mode)

- **EditMode: 152 passed, 0 failed.** 145 domain tests (18 new for the suit and the airlock), 5 route geometry
  tests and 2 fixture checks of the regenerated scene and prefab.
- **PlayMode: 13 passed, 0 failed.** The 8 repair-loop tests plus 5 for the EVA: gates following the airlock,
  refused exit without the airlock cycle, a full EVA driven through the interactions (suit, depressurize, exit,
  traverse, repair, return, repressurize), suit running empty outside, and restart while outside.
- No compiler warnings or errors from `OutOfReach.*` code. `tools/verify_repository.py` and
  `tools/nasa/horizons_snapshot.py` PASS.
- Earlier checkpoints: C02 at `91e6215` was 129 EditMode / 8 PlayMode, also verified independently in a separate clone.
- **Not verified:** integration with Codex's ship, exterior and UI, a Windows build, a human play-through of the
  harness scene (generated and validated in batch mode only), performance.
- Unity side effects left uncommitted: the `Assets/Settings/Mobile_RPAsset.asset` URP upgrade and a generated
  `ProjectSettings/SceneTemplateSettings.json`.
