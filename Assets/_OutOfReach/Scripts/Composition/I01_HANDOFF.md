# Complete-mission composition handoff (C03/C04 upgrade)

Task: I01/I02 integration. Writer scope remained `.local/verification/I01Composition/**` only. Gameplay API source is the exact C04 tree staged from `6bb788b` in `.local/c04-source`. The parent is the sole scene writer, Unity owner and integrator on `codex/i01-integration`. This supersedes the earlier C02-only handoff.

## Adopt and run

Copy only `Runtime/`, `Editor/`, their metadata, this handoff and its metadata into `Assets/_OutOfReach/Scripts/Composition/` in `.local/i01-integration`. Do not copy `.checks/` compiler outputs into Assets. Preserve all supplied metadata. The existing parent `Composition.meta` should remain unchanged.

Prerequisites now include the actual C04 gameplay, the new HUD implementation, `UI/Report/Runtime` (`OutOfReach.UI.Report`), and `Scripts/Presentation/Lighting` (`OutOfReach.Presentation.Lighting`). The Editor assembly references the actual installed `UnityEngine.UI` assembly. The report stylesheet is optional at `Assets/_OutOfReach/UI/Report/Runtime/FlightRecorder.uss`; its assembly is required. A missing stylesheet falls back to the simple final-state restart panel. The builder uses `Assets/_OutOfReach/Visuals/Space/DistantStars.mat` when available and otherwise keeps a black background.

Entry points (Unity 6000.3.28f1):

- `OutOfReach.Composition.Editor.I01BootstrapBuilder.BuildAndValidate`: composes and saves Bootstrap in an ignored `.local` project only; preserves its GUID and does not modify source prefabs, engine/package/project/build settings.
- `...BuildAndCapture`: builds/validates, then renders four actual Bootstrap interior views.
- `...CaptureIntegrated`: opens existing Bootstrap and captures cockpit, engineering corridor, power worksite and airlock at 1.65 m eye height. Images are in that project's `.local/integrated-captures`. It uses the one actual camera and current scene geometry/lights, hides UI during capture, restores temporary changes, and does not save capture poses. These are nominal edit-time renders, explicitly not gameplay evidence.
- `...ValidateSaved`: structural and reach inspection only.
- `...BuildWindows`: explicit Bootstrap-only Development Windows x64 build to `Builds/I01/OutOfReach.exe`; no executable play claim.

## Real mission bindings

`MissionRuntime` remains the only session and simulation driver. It starts the actual science-focus loadout. Timed planning is armed by the visible primary `Start timed Â· 60 s` action using `BeginTimedPlanning`; Practice leaves the new deadline unarmed. No hidden deadline or loadout substitute exists.

The board receives Planner/Resources/GameState/Events, Clock/PlanningDeadline, `ConfigureEva(runtime.EvaStatus)`, `ConfigureCo2Scale(SurvivalRules.CarbonDioxideActionLevelPpm, SurvivalRules.CarbonDioxideLossLimitPpm)`, and the actual launched `Configuration.InitialResources` via `ConfigureResourceReference` at Intro (or initial binding into an already launched session).

The eight `InteractableTarget` IDs are the actual C04 constants: `cockpit-console`, `repair-power`, `repair-life-support`, `suit-locker`, `airlock-controls`, `eva-exit`, `eva-return`, `repair-antenna`. Every target has a separate trigger collider; reach remains 2 m and targets sit in front of visual collision. Validation checks all eight from representative standing/route positions. Empty-space failed E presses are suppressed by checking `InteractionResolver.CurrentTarget`, while meaningful blocked-target explanations remain as compact 14px feedback.

The obsolete static inner-door boundary is removed. The inner and outer `GameplayGate` components use `GateIds.AirlockInner` and `GateIds.AirlockOuter`; `runtime.ConfigureEva(gates, route, reentry)` binds them. Only Core opens the inner gate when InnerOpen; the outer gate is always solid and crossed only by authoritative eva-exit/eva-return commands. Reentry feet are `(0, .05, 19.9)`, heading 180 degrees.

`I01AirlockDoorView` only moves the actual four delivered model leaves from read-only `IEvaStatus.Airlock`. Inner leaves open only for InnerOpen; outer leaves open only for OuterOpen. It never opens a collider or issues a mission command. Positions are prebound, animation uses real time and respects the authoritative pause clock.

## Exterior route and geometry

The parent-provided `AttachDeliveredModels` implementation is preserved: it instantiates `Visuals/Models/Aurora7/Prefabs/Aurora7_Models.prefab`, disables fallback mesh renderers and colliders in the scene, retains anchors, and binds the delivered four door leaves. If no delivered model asset exists it still permits the original fallback; confirm the `I01_MODEL_DELIVERY_ATTACHED` log before showing this build to the user.

TetheredRoute feet waypoints in metres:

1. Actual EvaStart `(0, 0, 22)`.
2. `(0, 1.2, 22.8)` to rise over porch railing outside the aft cap.
3. `(3.8, 1.2, 22.8)` outboard, still aft of the cap.
4. `(4.35, .9, 21.7)` outside the starboard hull.
5. `(4.35, .9, 13)` at the antenna worksite.

Traverse speed 1 m/s; lateral freedom .25 m. The existing controller disables gravity/collision on the authoritative route and constrains movement. The route's outboard segment stays beyond the delivered radius-3 m aft hull, including the permitted drift. A thin amber guide uses an existing original material. This is a disclosed constrained route, not tether physics. The antenna trigger is shifted .12 m outboard from its unchanged anchor, and the final eye position reaches it within 2 m. EvaReturn is at eye-accessible height on the unchanged return anchor and is immediately reachable from route start. Actual traversal, visual clearance and endpoint reach still require root play verification.

## Lighting, communications and report

Eight warm working spotlights at the delivered fixture stations `[-1.6, 1.4, 4.4, 7.4, 10.4, 12.4, 15.4, 18.4]` use the model preview's 13-intensity, 7.5 m range, 150/110 degree spread. Eight separate low amber path lights form the emergency group. All unbound old fallback lights are disabled as scene overrides. `ShipLightingPresenter.Configure(events, state, authoritativePowerRepairable, workingLights, emergencyLights, clock)` controls these groups. Cinematic fill and system-status lights stay outside both groups. No source model/material is changed.

During Transmission a compact readout polls actual `runtime.Communications` and `runtime.Calendar`: mission UTC, rounded Mars-vicinity reference distance, one-way light time, separate ground processing, and simulated remaining reply time. It never advances communications or invents reply state. Existing HUD discloses simulation scale.

The existing `FlightRecorderPresenter` is constructed in a bounded host in the same UIDocument only when Core reaches Report and provides `runtime.Report`. It renders that exact immutable report. Restart calls `runtime.RestartMission`. Outcome does not display an early blocking modal, so Core's CommunicationRestored/EndingPullback handshake can finish. The old interior-checkpoint completion message and EVA/report restrictions are removed. No success, loss, phase or report is manufactured.

One camera/listener remains under ViewPivot -> Planning framing -> Cinematic offset. Only Planning changes its framing parent; launch restores local identity. The existing presenter owns the dedicated cinematic child and exact-once completion acknowledgement.

Audio remains driven by confirmed events and pause. Optional explicit clean cue fields now include `recordedBriefing`, `impactWarning`, `powerRestored`, `lifeSupportRestored`, `antennaRestored`, `falseSignal`, `transmissionSent`, `earthReply`, `outerHatchOpen`. The builder now binds the user-approved Clean masters 01, 02, 03, 04, 06, 07, 09, 10 and 11, with each exact matching TXT as subtitles; 05/08/12 are not bound. Major confirmed cues queue once per session; impact, local-buffer diagnosis and the received reply take priority over an older line. vo06 only follows the actual OuterOpen event. The builder extends impact/false-signal/reply cinematic duration to at least its clip length + .3 seconds, within 40 seconds. FalseSignal is labeled a local archived buffer; Earth reply is only triggered by Core's CommunicationRestored request after the real delayed outcome. No rejected Radio/Production fallback is used. Subtitle priority and clean unsubscribe/rebind behavior are retained.

## Verification actually run

- Standalone Unity 6000.3.28f1 bundled Roslyn compilation of 87 actual C04/current HUD/report/lighting/audio/cinematic/composition source files together: exit 0, C# 9, warnings as errors with only normal Inspector-assigned-field CS0649 suppressed. Response and source hashes are under `.checks/`. This verifies signatures/syntax against actual source, not Unity asmdef import or runtime behavior.
- All staged asmdef JSON parsed; nine staged metadata GUIDs are unique. Existing metadata was retained.
- No Git command, production edit, Unity process, import, scene build, actual scene capture, test runner, Windows build or manual playthrough was run by this writer for this upgrade.

Root must run the actual Unity compile/scene validation, inspect first model captures, and then verify planning -> cockpit -> impact -> both repairs -> suit -> cycle -> exterior traverse -> antenna -> return -> repressurize -> communications diagnostic -> transmit -> causally delayed reply -> Report -> restart, including pause and a real failure. Confirm one camera/listener/runtime, actual new model attachment, no missing materials/scripts, report scrolling, subtitle timing, and gates enforcing Core state.

Staged file lock released when this handoff is delivered. Parent owns all copy, scene generation, verification and commits.
