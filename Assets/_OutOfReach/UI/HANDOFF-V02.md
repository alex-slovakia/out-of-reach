# V02 planner and resource HUD

Owner: Codex. Branch: `feature/visuals-cinematics`. Scope: UI only; accident presentation is separate.

## Composition

Run `Tools > Out of Reach > UI > Build Planner and HUD Prefab`, or invoke
`OutOfReach.Presentation.UI.Editor.MissionUiBuilder.Build` in the verification project.
It creates `Generated/MissionPanelSettings.asset` and `Generated/MissionFlightBoard.prefab` here.
It does not save the open scene or shared project settings, or flush unrelated dirty assets.

The coordinated integrator instantiates that prefab in Bootstrap and explicitly calls:

```csharp
flightBoard.Configure(planner, resources, gameState, eventBus);
flightBoard.ConfigureTiming(clock, planningDeadline); // Optional read-only contracts; either may be null.
```

The component type is `OutOfReach.Presentation.UI.MissionFlightBoard`. The four arguments are
`IMissionPlanner`, `IResourceSystem`, `IGameState`, `IMissionEventBus`, in that order. The prefab
contains only UIDocument, its panel reference and the presenter with the owned stylesheet.
Runtime assembly `OutOfReach.UI` references only `OutOfReach.Contracts` as a domain dependency.
No Core, Inventory or Simulation types are consumed.

Configure may be called before or after OnEnable. Reconfiguration detaches the old bus first;
OnDisable and Dispose detach all listeners. Re-enabling reads current snapshots and subscribes
again. Dispose is terminal; instantiate a fresh view or simply disable/reconfigure the existing
view for a session restart. The composition root must configure the replacement session explicitly.
When replacing IGameState, Configure clears the previous clock/deadline and subtitle; call
ConfigureTiming after Configure for each new session. Null timing inputs hide unavailable values.

Subscriptions: PhaseChanged, ResourceChanged, MissionEnded. No authoritative events are published.
Only TrySetEquipment and TryLaunch commands are issued from user input. Every command is followed
by a fresh Draft read; validation controls launch availability and failures display Explanation.
No clock, deadline, physics, resource consumption, outcome selection or phase transition is driven here.
LateUpdate only rereads the optional timing contracts; there is no local elapsed counter or countdown.

## Interaction and display

- Planning shows catalog names, qualitative purposes, authoritative unit mass/load/credits, +/- counts,
  draft totals/limits and draft initial reserves. Maxima and numeric equipment effects are not duplicated.
- +/- can reach the Core-owned maximum and receive its explanation; the contract does not expose maxima.
- English text maps the seven stable C01 equipment IDs; unknown IDs remain visible with generic text.
- All non-Planning phases show a compact HUD. The center remains clear and the HUD ignores pointer picking.
- The player adapter/composition root owns cursor unlock and movement/input gating while Planning.
  The UI does not change Cursor.lockState or player controls.
- Readouts: stored oxygen kg, water L, food kg, battery kWh, integer spare parts, cabin CO2 ppm.
  No oxygen percentage or guessed safety threshold is shown.
- Optional ISimulationClock supplies the visible HUD simulation scale and PAUSED state. Its values
  are read directly each frame; presentation never advances time or assumes 1x when unbound.
- Optional IPlanningDeadline supplies timed planning: ceil(RemainingRealSeconds), calm amber urgency
  at 15 seconds, and an explicit automatic-launch rule. The right-hand review scroll includes every
  catalog item's last-valid count (including zero), plus any additional selected ID. It reads the
  immutable LastValidConfiguration; an invalid current draft is never silently reset by this UI.
- UsedLastValidLoadoutAtDeadline produces a visible post-launch notice. When the deadline is disabled,
  the planner says Untimed Planning. No mode-changing command or Practice/Timed selector is invented:
  the pre-start choice awaits Claude's exact runtime method and integration.
- Future report/communications views are outside this change. A late-bound outcome can read the enum,
  but the reason is available only from a MissionEnded event, not the current state snapshot.

## Subtitle bridge

```csharp
flightBoard.ShowSubtitle(exactEnglishCueText, speakerLabel);
flightBoard.ClearSubtitle();
```

These methods are presentation-only and start no timer. Audio/cinematic presentation owns cue duration,
replacement, skip and stop, and must call ClearSubtitle when a cue ends. Empty text clears it. Labels
do not interpret rich text, wrap within the caption panel, and retain the supplied wording. Caption
text uses 22 logical pixels; the speaker uses 16. The panel is centered above the measured resource
HUD, ignores picking and adds no player input or camera control. Text can be supplied before OnEnable;
it remains presentation state until cleared. Restart binding and Dispose clear it.

The panel reference resolution is 1280x720; text is at least 16 logical pixels, scaling at 1920x1080.
Both planner columns scroll independently when needed; launch and its validation stay visible.
Colors use graphite, warm white, cyan
readings and amber constraints. No external fonts, images or packages are required.

## Preview and verification

### Integrator verification — 14 November 2026

The commit containing this handoff includes the generated panel settings/prefab, preserving their metadata.
Unity 6000.3.28f1 imported and compiled the UI, generated its assets, and ran a temporary Play-mode
integration harness against exact C01 `caee234` sources plus the new canonical timing interfaces.
The harness stays under the ignored verification project; it does not replace Claude-owned tests or Bootstrap.

**24 checks passed, process exit 0** (`.local/unity-baseline-check/v02-ui-play-qa-3.log`): actual catalog,
valid launch availability, Core-rejected quantity limit with visible explanation, successful quantity
change, stable control identity, invalid draft/disabled launch, corrected draft, real Planning→Intro
launch, live reserve update, real Core pause display, disable/enable without duplicate resource listeners,
replacement session with the actual redundancy preset, old-bus detachment and final disposal.
The harness uses a read-only wrapper of the existing C01 clock; no alternate elapsed-time model.

Actual runtime UI render-texture captures are under that project's `.local/v02-ui-qa/`: planner at
1280x720 and 1920x1080, invalid draft, HUD, and wrapped subtitles above the HUD. Planner and HUD/subtitle
images were inspected. The capture target explicitly clears between frames; this avoids retaining the
previous planner pixels when switching to a transparent HUD. Scrollbars follow the graphite/cyan theme.

This proves contract interaction and these display/lifecycle cases, **not a complete mission or a Windows
player build**. Physical mouse/gamepad play, player input gating, timed expiry/fallback with C02's actual
deadline, Practice/Timed selection and final Bootstrap integration remain pending. The 24 checks do not
establish the deadline owner's correctness or final visual quality.

`Tools > Out of Reach > UI > Open Read-only Layout Preview` opens an editor-only window with an
explicit static-preview banner and disabled mission buttons. Its two layout tabs only change the view.
It implements no fake domain interfaces, creates no scene and is excluded from player builds.
Preview numbers are authoring examples, never production defaults. Production remains unbound with
dash readouts until Configure supplies actual services.

Source handoff verification: original and timing contract signatures inspected; no Unity editor/process was launched by
the UI writer. Repository verifier passed (209 asset GUIDs at the time of the check); NASA offline
verification passed (32 samples, hashes and exact regeneration). The integrator must record the
actual revision and results after compiling, generating
assets, checking USS imports and reviewing 1280x720 / 1920x1080. Verify invalid draft, rejected + at
item limit, valid launch, live reserve changes, disable/enable, reconfiguration and restart. Keyboard
focus must remain on the same +/- control after a valid edit, and gameplay input must be restored by
the owning runtime adapter. For timing, verify ceil rounding, pause, multiple simulation scales,
expired-invalid fallback disclosure, full last-valid counts, null timing inputs and fresh-session
rebinding. Check short and wrapped subtitles, replacement, clear and visible gap above the HUD.
The UI writer's source-only assessment below is superseded for the specific verified cases above;
unlisted cases remain pending. No complete-mission or Windows-build claim is made here.

Timing/subtitle enhancement source check: UI metadata and JSON remain valid. A later whole-repository
verification encountered missing metadata in concurrently authored Audio/Runtime and
Scripts/Presentation/Cinematics files, outside UI ownership; those were not modified by this writer.
Re-run the repository check after the parallel writers finish.
