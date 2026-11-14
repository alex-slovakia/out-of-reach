# C06 rest and recovery — agreed implementation boundary

Integrator decision, 14 November 2026. Implements the user-approved 13-beat mission, with contact as midpoint. This publishes an interface and ownership; it does not claim implemented or playable rest/rescue.

## Decisions

- Adopt Claude proposal in issue #7 comment 6880467396, refined by 6880545451: maximum **8.5 simulated hours per rest opportunity**, shorter finite positive intervals allowed. Rest is an accelerated run of the same clock, not an instant skip. NASA sleep-opportunity reference: https://www.nasa.gov/reference/6-0-medical-operations/ . Actual restful sleep is not guaranteed by the duration.
- Keep the beacon choice, not a hidden arbitrary delay. Recovery starts from a **fictional already-in-transit nearby craft**, not a rocket launched from Earth when contact is restored. Initial game tuning: arrival 12 days after contact; beacon request on day 6; a missed 2-hour beacon window delays arrival 3 days. These are authored scenario assumptions, not a trajectory derived from the NASA Earth–Mars range snapshot. Any inbound Earth message still respects actual signal-delay rules.
- Use separate appended `RecoveryArrival` and `MarsDeparture` cinematic IDs. Mars is a clearly later chapter/time passage, not simultaneous with the rendezvous. Codex owns the cinematic assets and staging.
- Preserve default legacy behavior until the complete optional chapter and reachable ending are integrated. No saved scene is opted in by this contract publication.

## Canonical interface and caller migration

`Scripts/Contracts/ISurvivalRecovery.cs` is canonical. It contains `RestStopReason`, `RecoveryStage`, read-only rest/calendar/recovery state, rest/wake commands and separate `CheckPowerBeacon`/`TryPowerBeacon`. Expose the service as `MissionSession.SurvivalRecovery` and `MissionRuntime.SurvivalRecovery` (null when disabled is acceptable and must be documented). UI must read that authority and issue commands; no parallel day, food, beacon or arrival timer.

Append `MissionOutcome.Recovered` and `CinematicId.RecoveryArrival`, `MarsDeparture` without changing prior enum ordinals. Report must distinguish actual recovered outcome from old `RecoveryPossible`. Existing source/serialized callers remain valid; optional new consumers must handle disabled/null service. Core interactions are `survival-sleep` and `recovery-beacon`. Codex will bind the physical anchors only after a tested delivery.

## Single-writer scope — Claude gameplay, R1 then R2

Use a separate branch/check-out based on this contract commit plus delivered `57fb879`. No Codex agent writes these paths during this assignment:

- `Assets/_OutOfReach/Scripts/Core/SurvivalExtension.cs`
- `Assets/_OutOfReach/Scripts/Core/MissionDirector.cs`
- `Assets/_OutOfReach/Scripts/Core/MissionSession.cs`
- `Assets/_OutOfReach/Scripts/Core/MissionReportBuilder.cs`
- `Assets/_OutOfReach/Scripts/Core/CockpitConsoleInteraction.cs`
- `Assets/_OutOfReach/Scripts/Core/Runtime/MissionRuntime.cs`
- `Assets/_OutOfReach/Scripts/Core/InteractionIds.cs`
- New `Assets/_OutOfReach/Scripts/Core/SurvivalRecovery.cs` and `.meta`
- New `Assets/_OutOfReach/Tests/EditMode/SurvivalExtension/SurvivalRecoveryTests.cs` and `.meta`

Inspect actual `InteractionIds` location before editing: if this named path is absent, report the canonical path for root coordination. No Contracts, Simulation, Player, presentation, scenes, packages or other tests are assigned. Request any necessary extra path with reason; do not duplicate another system under an owned folder. Root owns contract changes, UI, scene bindings, player/body lease, stamina on wake, audio and cinematics.

## R1 acceptance

- Only Survival, inside, unpaused and without a blocking cinematic permits beginning rest. Invalid/NaN/infinite duration and refused actions mutate nothing. Pause freezes clock and rest; wake restores the previous intended Survival time scale. Restart/disable/outcome cannot leave a stale rest lease.
- Real food/water ledger debit uses the existing BVAD crew-day rates during rest; O2/CO2/power continue normally. Do not double-charge the awake manual portions, create supplies or silently erase pre-existing hunger/thirst. Explain the exact accounting with tests.
- Split at the earliest actual danger/resource/crop/recovery boundary; do not step across a failure and then wake. Record stop reason and real mission-calendar day. Manual wake works, and repeated wake/begin requests cannot duplicate consumption/events.
- Exercise resource exhaustion, pause/resume/restart, repeated rests, boundary ties and equivalence of one long interval versus smaller ticks. Preserve default-disabled behavior.

## R2 acceptance

- One recovery authority; beacon energy is real and charged once; an unsatisfied beacon window causes one explicit delay, not repeated delay every frame. Paused time does not consume the window.
- Loss wins a tie at arrival; reaching arrival alive requests the authored recovery sequence once. Report opens after the acknowledged bounded sequence, including timeout/cancel recovery; no stuck mission or duplicate outcome.
- At least one real feasible default-loadout successful route and one shortage failure route must be demonstrated through real commands and clock advance. Rest/growing requirements cannot leave normal players waiting indefinitely.
- Record actual rest, supplies, beacon choice and outcomes, preserving NASA provenance and explicit fictional timing. No false successful rescue on mere radio contact.

Each step is a separate tested commit with exact evidence and remaining limitations posted to issue #7. Root integrates after review; no automatic main merge or organizer submission.
