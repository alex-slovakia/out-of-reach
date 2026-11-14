# V02 planner and HUD redesign handoff

Owner: Codex UI writer. Task: V02 visual replacement. Branch: `feature/visuals-cinematics`.
Base commit: `656d96265c4b79f2164ced6ffbac46d95540d0e2`; this redesign is an uncommitted source handoff.
The integrator owns the final revision, Unity verification, scene composition and commit.

## Delivered design

The planner now leaves the scene visible behind a restrained mission title and one 472 logical-pixel
payload surface at the right, inset 24 pixels. Seven compact equipment rows show authoritative unit
mass, fictional credits and power load with persistent +/- controls. Current/limit mass, budget and
load/generation are compact; one ivory launch button remains pinned below validation and rejected
command explanations. The `Reserves & launch details` disclosure contains all six initial reserves,
the timed/practice rule, all last-valid counts (including zero), and the fictional/simplified assumptions.
Expanding details scrolls this content; the launch action stays visible. No ship illustration is drawn.

HUD: phase-based purpose at top left, actual elapsed mission time and simulation scale/pause at top
right, then unframed oxygen reserve / battery / cabin CO2 instruments at bottom left. `Systems` is a
real disclosure button; holding Tab also reveals water, food and spare parts. Every value continues
updating while hidden. No UI resource counters or danger thresholds are invented. The six readings
remain under `.hud-readouts`; each cell has `resource-0` through `resource-5` matching the canonical
snapshot order (oxygen, water, food, battery, parts, CO2). Visual order puts oxygen/battery/CO2 first.

Captions use white 20-pixel text, a 14-pixel speaker label, a 760-pixel maximum measure and minimal
backing. Their position follows actual instrument height to preserve a gap when telemetry expands.
The rest of the HUD ignores picking; the Systems button alone accepts pointer input.

Typography uses the integrator-supplied Source Sans 3 Regular/Semibold assets under `UI/Typography`.
Those fonts, licenses and metadata are owned by the integrator. Secondary text is at least 14 logical
pixels. Panel reference resolution remains 1280x720. Metadata and generated prefab/settings are
unchanged by this writer.

## Files changed by this writer

- `Runtime/FlightBoardView.cs`
- `Runtime/MissionFlightBoard.cs`
- `Runtime/OutOfReach.UI.asmdef`
- `Styles/MissionFlightBoard.uss`
- `HANDOFF-V02.md`

No Core, simulation, inventory, player, interaction, contract, scene, package or project settings changes.
`Unity.InputSystem` is now an assembly reference for the already installed Input System package; no
new package was introduced. The UI still consumes only `OutOfReach.Contracts` for domain behavior.

## Integration and API

Existing public signatures are retained. Composition still explicitly calls:

```csharp
flightBoard.Configure(planner, resources, gameState, eventBus);
flightBoard.ConfigureTiming(clock, planningDeadline);
```

Type: `OutOfReach.Presentation.UI.MissionFlightBoard`. The existing generated prefab still references
the presenter and stylesheet. `MissionUiBuilder.Build` remains the optional UI-only regeneration entry.
No scene lookup, camera, listener, cursor lock, input action ownership or service locator is introduced.

New presentation-only public methods on BOTH MissionFlightBoard and FlightBoardView:

```csharp
ShowInteractionPrompt(string text, bool available);
ClearInteractionPrompt();
```

The player/composition adapter supplies the exact prompt including any desired control hint. The UI
shows it and marks unavailable wording amber; it never invokes an interaction or invents a binding.
Empty text clears the prompt. Configure clears an old session's prompt. A prompt supplied before
OnEnable is retained as presentation state and applied when the view is created.

New public FlightBoardView methods used by the presenter:

```csharp
SetSimulationElapsedTime(bool available, double elapsedSimulationSeconds);
SetSystemsHeld(bool held);
```

Elapsed text formats the injected `ISimulationClock.ElapsedSimulationSeconds` directly. LateUpdate
reads Core timing, then reads `Keyboard.current.tabKey.isPressed` only for non-Planning telemetry
presentation. It advances no clock and consumes no input command. The Systems button pins/unpins
telemetry; releasing Tab closes it only when not pinned. Entering Planning resets that disclosure.

All original lifecycle rules remain: snapshots before subscription, PhaseChanged/ResourceChanged/
MissionEnded listeners detached on disable/disposal/rebind, no domain publication, only
TrySetEquipment and TryLaunch commands. Replacement state clears prior timing references and
requires ConfigureTiming again. Subtitle replacement/clear remains owned by the audio presenter.

Timed planning still reads IPlanningDeadline. Countdown uses ceil and unpaused real seconds, with
amber urgency at 15 seconds. The rule and immutable last-valid counts are available in launch
details. Post-launch fallback remains visible. There is no mode selector until the composition root
binds the actual supported runtime choice; this change does not invent a mode-changing contract.

## Evidence and required verification

Source checks on 14 November 2026:

- Both rejected baseline captures were inspected before replacement.
- Canonical contracts, all seven mandatory project documents and the supplied craft floor were read.
- `git diff --check`: passed.
- `python tools/verify_repository.py`: passed, 246 asset GUIDs at this checkpoint.
- `python tools/nasa/horizons_snapshot.py`: passed, 32 samples with source/hash/vector/light-time checks.
- This writer launched no Unity process and claims no compile, render, player build or playtest pass.

The earlier 24-check integration harness and baseline captures establish behavior only for the old
layout. They do not verify this replacement. The integrator must inspect actual in-scene captures at
1280x720 and 1920x1080: all seven rows, explicit limits and costs, open reserves/fallback details,
invalid loadout and disabled launch, visible rejected maximum, keyboard focus retained after edits,
real launch, live resources, Systems click and hold/release Tab, pause/scale/elapsed display, prompt
available/unavailable/clear, wrapped subtitles, and lifecycle/rebind. Inspect USS import warnings and
confirm the two real fonts are applied. Confirm the planner camera presents authentic ship geometry
through the transparent majority of the view. No visual acceptance or complete-mission claim yet.
