# Claude gameplay domain — handoff

Branch `feature/survival-simulation`. Owner: Claude (Apoliak). This file is the running handoff for the
integrator; root documentation stays integrator-owned. Status and requests live on issue #7.

**Done:** C01 (mission foundation), C02 (first playable repair loop), C03 (suit, airlock, constrained EVA,
antenna repair) and C04 (NASA/JPL-driven reply delay, the one transmission, both outcomes, the ending sequence,
report data, restart), including the shared `IMissionReport` and `IEvaStatus` contracts, C05-A (physical
preflight cargo loading, switched off by default) and C05-B (payload salvage, CO₂ absorber cartridge and its
bay, offered only where their scene targets are bound). The gameplay loop is complete from planning to report.

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
| `IEvaStatus` | `OutOfReach.Core.EvaOperations` |
| `ICommunicationSystem` | `OutOfReach.Simulation.CommunicationSystem` |
| `IMissionReport` | `OutOfReach.Core.MissionReport` (sections use the contract's `MissionReportSection` / `MissionReportLine`) |
| `ICargoLoading` | `OutOfReach.Core.CargoLoading` |
| `IFieldModifications` | `OutOfReach.Core.FieldModifications` |
| `ISimulationClock` | `OutOfReach.Core.SimulationClockView` |
| `IPlanningDeadline` | `OutOfReach.Core.PlanningDeadline` |

Contracts added after the baseline come from the integrator's shared-only commits on
`feature/visuals-cinematics`, adopted here by cherry-pick: `39dfb92` (clock, planning deadline) as `50b2f8a`,
`84cbf64` (EVA status, mission report) as `3c4aa83`, `37f39a0` (field modifications) as `f0e9603` and `f8ceefe`
(cargo loading). The visuals branch itself was never merged;
`TEAM-COMMANDS.md` was not taken, so it arrives once from the integrator's branch.

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
  - → Outcome: only Core. Outcome → Report: Core, after the ending cinematics (presentation may also request
    it to skip them). Report is terminal; restart builds a new session.
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
  Emergency/Repair ×300 (1 s = 5 min), Eva ×60 and Transmission ×60 (1 s = 1 min). Maximum ×86,400.
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

On the route the player has no gravity and no free flight. **Traversal (fixed after desktop play, 2026-11-14):** the
feet stay exactly on the waypoint line and move by distance along it, so nothing can jump between segments.
W moves along the tether towards the end the player roughly faces (body heading only; looking up or down never
moves the player), S moves the other way, A and D do nothing. Vertical stretches need no looking down. Inside a
dead band (looking sideways, or along a vertical stretch) W keeps leading the way it led last. Both ends stop the
player (tether length = line length, 1 m/s, serialized on `TetheredRoute`). `FirstPersonController` exposes
`RouteDistanceMetres`, `RouteDirection`, `IsAtRouteStart` and `IsAtRouteEnd` for presentation. The last waypoint
must stay within 2 m of `AntennaRepair` so the repair is reachable from the end of the tether.

### EVA HUD data (`IEvaStatus`)

`MissionRuntime.EvaStatus` returns the shared `IEvaStatus`: `IsSuitOn`, `IsSuitSealed`, `SuitOxygenKg`,
`SuitOxygenCapacityKg`, `SuitOxygenSecondsRemaining`, `IsOutside`, `Airlock`, `AirlockTransitionRemainingSeconds`
and a `Changed` event. `MissionRuntime.Eva` is the same object with its commands, for code that references
Core. Airlock door visuals only need `AirlockStateChanged`.

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

## C04: communication, outcomes and the report

### What the player does

1. Back inside with the antenna repaired, the mission is in `Transmission`. The **cockpit console**
   (`cockpit-console`, the same target as the cockpit check) now reads "Run communications diagnostic".
2. **False signal, once.** That first use makes Core publish `CinematicRequested(FalseSignal)`. It is the damaged
   receive buffer replaying an old local recording: not a reply, no `CommunicationChanged`, no cost, no timer.
   Core waits for the matching `CinematicFinished` (skip allowed, 45 s fallback, immediate with no listener).
3. **The one transmission.** The console then reads, for example,
   `Transmit distress call: 0.5 kWh. Earth is 197,708,000 km away; earliest reply in 31 min 59 s.` Using it draws
   the energy, freezes the reference sample and starts the reply clock.
4. **The wait.** The console reads `Awaiting reply from Earth: 31 min 59 s (simulated)`. At the disclosed ×60 this
   is about 32 real seconds. Reserves keep draining; a failure during the wait ends the mission as `Lost`.
5. **Reply.** `RecoveryPossible`: contact and recovery planning, not a rescue. Then the ending sequence and Report.

### How the delay is computed (functional NASA/JPL integration)

- **Mission date** = mission clock T0 + elapsed simulated time (`MissionCalendar`). T0 is the first epoch of the
  packaged snapshot, 2026-11-14 08:00 UTC, unless a scenario sets another one inside the snapshot.
- **Sample** = the last row at or before the mission date (`EarthMarsReference.TrySelect`). No interpolation. A
  date outside the snapshot has no reviewed distance: transmitting is refused with that explanation.
- **Distance** = that row's `earth_mars_distance_km` (NASA/JPL data). **One-way light time** = distance /
  299,792.458 km/s (`SignalDelay`, derived at load; the file's own light-time columns are only cross-checked).
- **Earliest reply** = 2 × one-way + Earth processing. Processing is 600 s, a GAME ASSUMPTION in `CommunicationSpec`.
- A normal run transmits on mission day 1: 197,707,976 km, 659.483 s one way, 1,918.966 s to the reply.
  Nothing is tied to November: after three simulated days the Nov 17 row is used, and a scenario starting on
  2026-12-14 uses row 30 (17 min 39 s round trip). Both are tested.
- **Packaged copy:** `Scripts/Simulation/HorizonsEarthMars2026Snapshot.cs` holds the normalized CSV text line by
  line (no file or network access at runtime). `EarthMarsReference.Embedded` recomputes its SHA-256 and refuses to
  load unless it equals the manifest's `normalized_sha256` (`694f3606…e5af`). A test compares the text byte for
  byte with `docs/data/horizons/*.csv` and the IDs and hashes with `manifest.json`. The mission cannot start if
  `IMissionConfiguration.AstronomicalReferenceId` differs from the packaged dataset ID.

### Guarantees

- **Never early.** The reply is due at an absolute time on the mission clock, so pause, time scale and chapter
  jumps all act on it through the one clock. `ICommunicationSystem.AdvanceTime` is validated but cannot move the
  reply: only the mission clock does (a stray second call changes nothing).
- **One transmission.** A repeated send is `AlreadyComplete` and cannot restart the timer. A rejected send (wrong
  phase, damaged antenna or power, diagnostic not run, too little battery, date outside the snapshot) spends
  nothing and publishes nothing.
- **No skipped failure.** A jump is split at the reply time and at every failure boundary. Survival is evaluated
  before the reply at each split, so a loss at the same instant wins. A loss during the wait says how long the
  reply still needed.
- **Unpowered link** (cannot occur in this scenario once power is repaired, but implemented): the reply is not
  received while antenna or power is down; the state stays `AwaitingReply` and it is received when power
  returns (ASSUMPTION: the ground keeps repeating its answer).
- **Unrecoverable objective.** Back inside with power and life support running, the antenna still damaged and
  fewer spare parts than its repair needs: the mission ends as `Lost` with "Mission objective unrecoverable …
  The crew is alive and the ship is stable …" (`SurvivalFailure.ContactImpossible`). No death is claimed. Without
  this rule such a ship would idle for simulated weeks.

### Events the finale can rely on

| Moment | Events, in order |
|---|---|
| Impact | …, `SystemConditionChanged` ×3, `CommunicationChanged(Offline, 0)` |
| Antenna repaired (outside) | `ResourceChanged`, `SystemConditionChanged(Antenna, 1)`, `CommunicationChanged(Ready, 0)` |
| Diagnostic | `CinematicRequested(FalseSignal)` only |
| Transmission | `ResourceChanged`, `CommunicationChanged(AwaitingReply, replyDelay)` |
| Reply | `CommunicationChanged(ReplyReceived, 0)`, `PhaseChanged(Transmission → Outcome)`, `MissionEnded(RecoveryPossible, reason, reserves)`, `CinematicRequested(CommunicationRestored)` |
| After that cinematic | `CinematicRequested(EndingPullback)`, then `PhaseChanged(Outcome → Report)` |
| Any loss | `PhaseChanged(→ Outcome)`, `MissionEnded(Lost, reason, reserves)`, `CinematicRequested(EndingPullback)`, then `PhaseChanged(Outcome → Report)` |

`CommunicationChanged` is published on state changes only. For the countdown poll
`ICommunicationSystem.ReplyRemainingSimulationSeconds` and show `ISimulationClock.TimeScale` beside it. Each ending
cinematic has a 90 s fallback (`MissionScenario.EndingCinematicTimeoutRealSeconds`); an unfinished earlier
sequence is superseded and its late completion is ignored as stale.

### Bindings for V04 and I02 (`MissionRuntime`)

| Member | Use |
|---|---|
| `Communications` (`ICommunicationSystem`) | state, `ReferenceDistanceKm`, `OneWayLightSeconds`, `EarthProcessingSeconds`, `ReplyRemainingSimulationSeconds`, `AstronomicalReferenceId` |
| `CommunicationDetails` (`CommunicationSystem`) | not in the contract: `ReferenceEpochUtc`, `ReplyDelaySimulationSeconds`, `TransmitEnergyKWh`, `IsReferenceAvailable`, `CheckTransmit()`, transmission and reply times |
| `Calendar` (`MissionCalendar`) | `NowUtc`, `DayNumber` (1-based), `EpochAtStartUtc` for date and "mission day" labels |
| `Report` (`MissionReport`, implements `IMissionReport`) | null until an outcome exists; read it on `MissionEnded` or on `PhaseChanged(→ Report)` |
| `EvaStatus` (`IEvaStatus`) | suit and airlock state for the EVA HUD |
| `BuildReportPreview()` | the same report mid-mission (`IsFinal = false`) |
| `SkipSimulatedTime(seconds)` | chapter cards and "skip the wait": advances reserves and the reply clock together |
| `RestartMission()` | unchanged: a new session; nothing of the old mission survives |

No new scene anchor is needed: the transmission uses the existing `cockpit-console` target.

### Report (`MissionReport`)

`MissionReport` implements the shared `IMissionReport`: `Outcome`, `IsFinal`, `Headline`, `OutcomeReason`,
`ElapsedSimulationSeconds` and `Sections`. `Sections` is display-ready text (`Label`, `Value`) under stable IDs,
so a UI can render it without knowing the fields. Section IDs, in order (`MissionReportSections`): `outcome`, `mission-design`,
`resources`, `systems`, `science`, `communication`, `timeline`, `nasa-data`, `derived-physics`,
`fictional-scenario`, `time-compression`. The last four keep NASA/JPL data, derived physics, the fictional
scenario and gameplay time compression apart, as `NASA-DATA.md` requires. Runtime strings are plain ASCII.

Typed facts carry the same content: loadout name (preset ID or `custom`), totals and limits, the timed-planning
fallback flag, reserves at launch and at the end, expenditures by purpose (their sums equal launch minus end),
peak cabin CO₂, each system's damage and repair time, EVA counts, science objective and `ScienceDataReturned`
(payload carried and contact restored), `Communication` (dataset ID and SHA-256, sample epoch and row, distance,
light time, processing, earliest reply, transmission and reply times), `Holding` (ESTIMATE of days of reserves at
the final rates), `Timeline`, real play time and simulated time passed in chapter jumps.

### Communication assumptions (GAME ASSUMPTIONS)

| Value | Used | Basis |
|---|---|---|
| Earth processing | 600 simulated seconds | none; fictional turnaround |
| Transmit energy | 0.5 kWh, once | none; burst plus receiver for the reply window |
| Mission clock T0 | 2026-11-14 08:00 UTC | first epoch of the snapshot |
| Mars-centre range as the ship's range | labelled proxy | `NASA-DATA.md` scenario decision |

Not modelled: link availability, occultation, acquisition, message length, body motion during propagation.

## C05-A: physical preflight cargo loading (off by default)

### What it is
During Planning the player walks, carries **one crate at a time** from a type's supply pallet to that type's stack
in the cargo bay (or back), and launches from the cockpit console. It replaces clicking a list, not the planner:

- **The planner's draft is the only inventory.** A crate in a bay stack is one unit in `IMissionPlanner.Draft`.
  Loading and unloading call the existing `TrySetEquipment(id, quantity ± 1)`. Mass, budget, power and supply
  validation, the 60-second deadline, its last-valid fallback and the launch are untouched and identical to the list.
- **The crate in hands is the only extra state** (`CargoLoading.CarriedEquipmentId`). It is not aboard: at launch
  or at the deadline it stays behind and is never counted; the timeline says so.
- Supply left on a pallet is derived: the type's maximum minus what is loaded minus the crate in hands.
- Loading past a limit is allowed, as in the list: the draft shows the violation and the launch is refused.
- The bay starts with the default loadout aboard, exactly as the list planner starts with it. The player swaps
  crates; the last-valid fallback therefore always exists.
- **Carry speed** = walk speed × max(0.5, 1 − crate mass / 180 kg), from the authoritative unit mass: spare-parts
  kit 0.86, oxygen tank 0.83, food locker 0.81, water tank 0.73, battery module 0.69, solar wing 0.61,
  spectrometer 0.50. GAMEPLAY TUNING; nobody hand-carries 90 kg, so presentation should show a handling cart.

### Switch
`MissionRuntime` → **Preflight Walk** (serialized, default **off**) and **Preflight Spawn** (optional transform).
- Off: nothing changes. No player control during Planning, the console stays "on standby until launch".
- On: the player has control during Planning and starts at Preflight Spawn. `cockpit-console` during Planning reads
  `Begin mission: 546 of 600 kg, 720 of 800 credits` or the exact violation that blocks the launch, and launches.
  When the mission leaves Planning (console, UI button or deadline) the runtime moves the player to Player Spawn.
The cargo interactions exist in both modes; without player control nothing can reach them.

### Scene bindings (all Claude-owned components; no Codex prefab is edited)
| Target | ID | Prompt examples |
|---|---|---|
| Supply pallet of a type | `cargo-supply:<equipmentId>` | `Pick up oxygen tank: 30 kg, 40 credits, +12 kg oxygen (4 on the pallet)` · `Put the oxygen tank back on the pallet` · `Hands full: carrying a battery module` · `The only atmospheric spectrometer is already aboard` |
| Bay stack of a type | `cargo-bay:<equipmentId>` | `Load battery module: payload 546 -> 601 of 600 kg (over the limit)` · `Unload one spare parts kit (2 aboard)` · `No deployable solar wing aboard` |

Equipment IDs: `oxygen-tank`, `water-tank`, `food-locker`, `battery-module`, `solar-wing`, `spare-parts-kit`,
`spectrometer` (14 targets). Each needs a collider within 2 m of where the player stands, like every target. Add
them to `MissionRuntime` → Interaction Targets. After launch every cargo target reads
`The cargo bay is sealed after launch.`

### Presentation data
- `MissionRuntime.Cargo` (`CargoLoading`): `CarriedEquipmentId` (null with empty hands), `CarrySpeedFactor`,
  `GetSupplyRemaining(equipmentId)`, `GetLoaded(equipmentId)` and a `Changed` event for the crate in hands. The
  class implements the shared `ICargoLoading` (`GetLoaded` is an extra for code that references Core).
- Bay stacks: `IMissionPlanner.Draft.Equipment` (quantity per ID). One generic case model per crate is enough.
- Timer: `IPlanningDeadline.RemainingRealSeconds` after `MissionRuntime.BeginTimedPlanning()`.
- The runtime applies the carry speed to the player itself (`FirstPersonController.SpeedMultiplier`).

### C05-BOARDING-01: boarding window after the loading deadline (off by default)
`MissionRuntime` -> **Board After Loading Deadline** (serialized, default **off**; code: `ConfigureBoarding(bool)`,
effective with the next session). Only with Preflight Walk and timed planning; without Preflight Walk it is ignored.
- Off: nothing changes. At zero the deadline launches and the runtime moves the player to Player Spawn.
- On: at zero the deadline **seals instead of launching**. The valid draft, or the restored last valid loadout
  (`UsedLastValidLoadoutAtDeadline`), becomes the sealed manifest. `PlanningDeadline.IsAwaitingBoarding` and
  `MissionRuntime.AwaitingBoarding` are true, the phase stays Planning, and the player keeps control where they
  stand (no teleport). The window has no timer and no death rule; timed planning cannot be re-armed.
- While sealed, every cargo target, `IMissionPlanner.TrySetEquipment` and the public `TryLaunch` answer
  `PlanningDeadline.LoadingClosedMessage` (`Loading closed. Board and secure your seat.`). A crate in hands at the
  seal stays behind: not counted, back on its pallet, one journal line.
- `cockpit-console` (placed at the transport seat during Planning) reads
  `Secure your seat and launch: 546 of 600 kg, 720 of 800 credits` and launches the sealed manifest once; then the
  usual move to Player Spawn happens. Before the deadline it launches early exactly as before.
- Pause changes nothing in the window. A restart builds a fresh, unsealed session with the same switch.
- No Contracts change: presentation reads `MissionRuntime.AwaitingBoarding` (or `Session.PlanningDeadline`).

## REPAIR-PRESENTATION-GATE-01: visible timed work for one interaction (opt-in, off by default)

`OutOfReach.Interaction.TimedInteractionGate` wraps ONE real interactable (intended: the antenna repair) so that E
starts visible work and the real command runs once at the end. Nothing else changes: interactables that are not
wrapped stay instant, and Core repair/resource authority is untouched (no Contracts change).

- Wrap after the session's own binding: `antennaTarget.Bind(new TimedInteractionGate(real, 3.0, stillValid, paused))`.
  `stillValid` is your policy (current session, focus on the work site, control); `paused` freezes the time.
- E (`TryInteract`): idle and available -> success `Work started: 3.0 s` (nothing spent, no domain call, no
  `SystemConditionChanged`); unavailable -> the real refusal text, nothing called; working -> refused
  `Work already in progress`. `Prompt` reads `Working: 40%` meanwhile; `CanInteract` is false.
- Each frame the tool visual plays: `Advance(unpausedRealSeconds)`. At the end the real availability is checked again
  and `inner.TryInteract()` runs exactly once; `Finished(CommandResult)` carries the domain's own result.
- `Cancel(reason)` (look away, walk away, disable, restart, new session) and an invalid `stillValid` spend nothing; a
  new E starts from zero. Events: `Started`, `Progressed(progress01)`, `Finished(result)`, `Cancelled(reason)`.
- Show `Finished` yourself: the resolver's `InteractionAttempted` only reports the start of the work.

## QUIT-TO-DESKTOP-01: "Quit to desktop" in every menu (root GO 6885087868)

Written in root's `Scripts/Composition/Runtime/I01SessionControls.cs` on root's explicit GO (root does not edit it
until handoff). No scene, UI file, Core or Contracts change.

- The session menu gets a third action, **Quit to desktop**, after the two existing ones, with the same
  `ActionButton` (classes, hover and focus cue, keyboard/gamepad navigation). It shows in all three menu contexts:
  "Choose planning mode", "Paused" and "Mission ended".
- When the mission record replaces the menu, its end card gets the same action after **Plan again**
  (`AddRecordQuitButton`, found by the name `reportEndActions`, styled `recorder-button`; no UI file is changed).
- Two steps: the first press only arms it ("Click again to quit") for 3 real seconds (`QuitConfirmSeconds`,
  unscaled time, so it works while paused); a second press inside the window quits once ("Quitting…", disabled).
  The window passing, another menu context or a released menu disarms it.
- Action: `Application.Quit()`, plus leaving Play Mode under `UNITY_EDITOR`. `ConfigureQuitAction(action)` replaces
  it for native checks and tests (null restores the real one); `IsQuitArmed` and `QuitRequested` expose the state.
  UI Toolkit still delivers submit (Enter, gamepad South) to a focused button inside a hidden menu, so a press on a
  hidden quit button neither arms nor quits; the two existing menu actions got the same guard, so a stray Enter in
  play can no longer resume, start or restart through a closed menu. Follow-up (root GO 6885646676): Refresh blurs
  any focus held inside the menu when it hides it (`BlurFocusWithin`), and the reduced-effects toggle puts its
  shown value back when a change arrives while its menu is hidden (`SetValueWithoutNotify` from Refresh sends no
  change event and is unaffected).
- Pause help (root 6885223398), only in the existing Paused hint, chosen by `ControlsHintFor(floating, onRoute,
  suited)` from the actual player: walking "WASD move · Shift sprint"; tether route "W/S move along the tether";
  floating: click a surface in reach to push off, WASD only aims a push, Space foot push only on support, and for a
  suited player only, rail grip and release. No WASD thrust and no bare-hand grip is promised.
- Tests: new isolated PlayMode assembly `Tests/Composition` (`OutOfReach.Tests.Composition`): the confirmation
  logic with explicit times, the pause help per mode, and the menu and record buttons through real UI Toolkit
  submit events in a runtime panel.

## C05-B: payload salvage and the CO₂ absorber cartridge

Two field modifications. Both only convert what was carried; nothing is created. **Each is offered only where its
scene targets are bound** (`MissionRuntime` checks this when it binds a scene): a ship without them plays exactly
as before, including the "mission objective unrecoverable" rule.

| ID | Bound means | What it does | Cost (GAME ASSUMPTIONS) |
|---|---|---|---|
| `salvage-payload` | salvage offered | Dismantles the science instrument, once: **+3 spare parts**, its 300 W load leaves the bus exactly once, the **science objective is abandoned for good** | 0.2 kWh |
| `workbench` | cartridge offered, together with the bay | Builds one CO₂ absorber cartridge; it is carried until installed | 1 spare part + 0.2 kWh |
| `absorber-bay` | cartridge offered, together with the bench | Installs a carried cartridge; a working one is left alone, a spent one is replaced | 1 cartridge |

- **Where:** inside the ship in `Emergency`, `Repair` and `Transmission`. Never during an EVA, never before the impact.
- **Cartridge model.** While cabin CO₂ would rise, an installed cartridge takes up as much as the crew produces,
  so the concentration **stops rising** until the capacity is used: 0.505 kg, stated as 12 crew-hours at the cited
  1.01 kg per crew-day. It needs no power. It is **not** used up while the main scrubbers cope or the crew
  breathes suit oxygen. It does not stop the oxygen leak and is not a life-support repair: an EVA still needs the
  real repair. The capacity is a game assumption, not a rated lithium-hydroxide canister; the report says so.
- **Time.** The step loop splits exactly where a cartridge is used up. With one cartridge installed at the impact
  the CO₂ loss comes exactly 12 simulated hours later, in one jump or frame by frame.
- **Loss rule.** While the instrument can still be dismantled, its 3 parts count: the mission is "unrecoverable"
  only if parts in hand + 3 are fewer than the antenna needs. The bench prompt warns before a build that would
  make the antenna unrepairable (`… Warning: that leaves 2 spare parts; the antenna needs 3.`); the choice stays
  the player's.
- **Prompts** (examples): `Dismantle science instrument: +3 spare parts for 0.2 kWh. The science objective is
  abandoned for good.` · `Build CO2 absorber cartridge: 1 spare part, 0.2 kWh.` · `Install CO2 absorber cartridge:
  cabin CO2 stops rising for up to 12 h 00 min` · `CO2 absorber working: 11 h 40 min of capacity left` ·
  `Cartridge spent. Build a new one at the workbench.` · `No science payload aboard`.
- **HUD data:** `MissionRuntime.FieldModifications` (`IFieldModifications`): `SciencePayloadSalvaged`,
  `AbsorberCartridgesCarried`, `AbsorberInstalled`, `AbsorberRemainingSimulationSeconds`, `Changed`.
- **Report:** `resources` gets a "Field modifications" line, `systems` a "CO2 absorber" line with the disclosed
  assumption, `science` reads "Abandoned: the instrument was dismantled for spare parts at T+…", and
  `fictional-scenario` labels all of it as game assumptions. Expenditure sums still equal launch minus end (a
  salvage is a negative spare-parts cost). No section ID changed.
- **Live electrical load.** `IMissionConfiguration.RequiredPowerWatts` is the frozen launch loadout and still
  includes the instrument. The live figure after a salvage is `MissionDirector.PowerState.LoadWatts`.

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
| Contact impossible | Stable ship, antenna damaged, too few spare parts for it: unrecoverable objective, no death claimed |

## C06-ACCEPTANCE-AUDIT: the survival chapter in real frames (root GO 6885878756)

- `Tests/EditMode/SurvivalExtension/C06AcceptanceTests.cs` plays the post-contact chapter in real 0.1 s frames at the
  clock's own scales (300 awake, 3,600 resting) through the registered interactions: build, plant, harvest, beacon,
  eat and drink whenever offered, rest after a 15.5 h waking day.
- **No duplicated charge.** Every command changes the ledger by exactly its cost; a second E in the same frame, and
  the cockpit console after the beacon, change nothing. Food and water reconcile with the rations, drinks, planting,
  the irrigation of the growing days and the rests at the BVAD rate; awake time draws nothing else.
- **No softlock.** Simulated time moves on every frame until an outcome: Recovered 12 days after contact with the
  beacon, 15 after a missed window; an unattended crew is lost exactly at the thirst limit.
- The default loadout has no spare part left after the repairs, so the grow module is an explained refusal; three
  extra parts from planning give the full crop cycle, which takes its 8 growing days.
- **Fixed:** right after the beacon request woke the crew, one more E on the bed began a rest that slept through the
  whole 8 h window in 8 real seconds and slipped the rendezvous 3 days. `CheckRest` now refuses while the window is
  open ("No rest now: the recovery craft is waiting for the beacon; power it first (the window closes in …)"). Not
  powering it stays a choice: the window then passes awake in about 96 real seconds and rest is offered again.

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

1. **Player feel.** The first desktop play of I01 reported unresponsive interaction and unnatural movement
   (root cause not yet attributed). Walk speed, look sensitivity, reach and input bindings live in
   `Scripts/Player` and `Scripts/Interaction`; a confirmed reproduction is needed before changing them.
2. **Slow losses.** With life support repaired but power never repaired, the loss (battery, then CO₂) takes
   about 10 real minutes at ×300. `MissionRuntime.SkipSimulatedTime` or `MissionPacing` can shorten it.
3. **End of the snapshot.** After 2026-12-15 08:00 UTC of mission time no call can be sent. A normal run is
   weeks away from that; a run that idles that long ends by food a few days later.
4. **False signal lock.** Player input is locked while `FalseSignal` is active. A presentation that listens to
   `CinematicRequested` but never answers this ID holds the player for the 45 s fallback.
5. **Prompt contract.** The focus prompt is exposed through UnityEvents and properties, not a contract.
6. **PlayerRig camera.** The integrator parents the Bootstrap camera under `ViewPivot`; the rig never creates one.
7. **EVA anchors.** `airlock-controls` and `suit-locker` have no anchor in the visual prefab yet, and the route
   needs waypoint transforms along the hull from `EvaStart` to the antenna (V03).
8. **Citations.** The BVAD references and the communication assumptions above should be added to `NASA-DATA.md`
   by the integrator; its "runtime integration pending" status can now be updated.

## EVA traversal fix (2026-11-14, Unity 6000.3.28f1 batch mode)

- **EditMode 253 / 253, PlayMode 25 / 25.** New: 3 route-geometry tests (point at a distance, continuity and
  round trip, heading with the vertical fallback) and 5 PlayMode tests with a free-looking player: look-only
  and sideways input never move, W while looking straight down or up stays level and on the line round a corner,
  facing back returns, sideways keeps the sense, S reverses, a descending tether with a level view, clean stops
  at both ends, pause. Every frame is checked to stay on the line and move at most one frame's route speed.

## Evidence (C05-B, 2026-11-14, Apoliak's machine, Unity 6000.3.28f1 batch mode)

- **EditMode: 250 passed, 0 failed, 0 skipped.** 14 new for the field modifications: nothing offered by default,
  salvage once with one atomic event and 300 W removed exactly once, refusals (no payload, before the impact,
  during an EVA, too little energy), a starved ship that trades its science for contact, the loss rule after the
  instrument is gone and the bench warning, cartridge build and install rules, CO₂ held for exactly the capacity
  inside one jump without power, the CO₂ loss exactly one cartridge later, no use while the scrubbers cope,
  replacing a spent cartridge, report lines with sums that still add up, restart. 21 earlier ones cover C05-A.
- **PlayMode: 20 passed, 0 failed.** 2 new: bench, bay and instrument through their scene targets; a scene without
  the targets offers no modification. 3 earlier ones cover the physical preflight.
- The harness scene was regenerated (bench, instrument rack, absorber bay, 7 pallets, 7 bay stacks).
  `PlayerRig.prefab` is byte-identical.
- No compiler warnings or errors from `OutOfReach.*` code. `tools/verify_repository.py` and
  `tools/nasa/horizons_snapshot.py` PASS.
- Earlier checkpoints: C05-A at `9a6c2e4` 236 / 18, C04 at `6bb788b` 215 / 15, C03 at `e36343a` 152 / 13.
- **Not verified:** an independent run of this revision, integration with Codex's ship, UI and cinematics, a
  Windows build, a human play-through (carry speeds, whether a cartridge is ever worth its part), performance.
- Unity side effects left uncommitted: the `Assets/Settings/Mobile_RPAsset.asset` URP upgrade and a generated
  `ProjectSettings/SceneTemplateSettings.json`.
