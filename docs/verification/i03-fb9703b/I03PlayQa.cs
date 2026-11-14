using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using OutOfReach.Composition;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Core.Runtime;
using OutOfReach.Interaction;
using OutOfReach.Player;
using OutOfReach.Presentation.UI;
using OutOfReach.Presentation.Hands;
using OutOfReach.Presentation.Motion;
using OutOfReach.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

// Temporary, Editor-only verification. Never copy this runner into the production project.
[InitializeOnLoad]
public static class I02PlayQa
{
    const string Flag = "OutOfReach.I02PlayQa.State";
    const string Deadline = "OutOfReach.I02PlayQa.Deadline";
    const string Errors = "OutOfReach.I02PlayQa.Errors";
    public const string ScenePath = "Assets/_OutOfReach/Scenes/Bootstrap.unity";
    public static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../.local/i02-play-qa"));

    static I02PlayQa()
    {
        EditorApplication.playModeStateChanged += Changed;
        EditorApplication.update += Watchdog;
        Application.logMessageReceived += OnLog;
    }

    public static void Run()
    {
        try
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
            if (project.IndexOf("/.local/", StringComparison.OrdinalIgnoreCase) < 0)
                throw new InvalidOperationException("Run only in an ignored .local verification project.");
            if (Application.unityVersion != "6000.3.28f1") throw new InvalidOperationException("Unity 6000.3.28f1 required.");
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start from Edit mode.");
            Directory.CreateDirectory(Output);
            SessionState.SetString(Errors, string.Empty);
            SessionState.SetString(Deadline, DateTime.UtcNow.AddSeconds(600).ToString("O", CultureInfo.InvariantCulture));
            SessionState.SetString(Flag, "entering");
            // Named runner outputs only. Images from an earlier run are never presented as current evidence.
            foreach (string name in new[] { "PASS.txt", "FAIL.txt", "checks.txt", "runtime-errors.txt", "capture-manifest.txt", "editor-environment-errors.txt" })
                File.WriteAllText(Path.Combine(Output, name), string.Empty);
            File.WriteAllText(Path.Combine(Output, "scope.txt"),
                "Automated saved-Bootstrap smoke, not a desktop/human play pass.\n"
                + "Actual UI submissions and Input System events; QA teleports explicitly recorded.\n"
                + "Runtime stepping suspended only around exact repair accounting and captures. No fabricated repair/outcome.\n"
                + "ScreenCapture is attempted; 1280x720 fallback uses the existing camera plus actual UIDocument.\n"
                + "Offscreen captures exclude ScreenSpaceOverlay UGUI and occur outside impact only.\n");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }

    static void Changed(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredPlayMode || SessionState.GetString(Flag, "") != "entering") return;
        SessionState.SetString(Flag, "running");
        new GameObject("I02 automated smoke runner - QA only").AddComponent<I02PlayQaDriver>();
    }
    static void Watchdog()
    {
        if (string.IsNullOrEmpty(SessionState.GetString(Flag, ""))) return;
        if (DateTime.TryParse(SessionState.GetString(Deadline, ""), CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out DateTime end) && DateTime.UtcNow > end)
            Finish(false, "I02 automated smoke exceeded its 600-second wall-clock deadline.");
    }
    static void OnLog(string message, string stack, LogType type)
    {
        if (string.IsNullOrEmpty(SessionState.GetString(Flag, ""))) return;
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        string entry = type + ": " + message + "\n" + stack + "\n";
        // Unity's editor-only search-index startup can throw before play begins in this cached
        // verification project. Preserve that exact environment failure separately; all other
        // exceptions, including any OutOfReach stack, still fail the smoke run.
        if (type == LogType.Exception && message.StartsWith("ArgumentOutOfRangeException:", StringComparison.Ordinal)
            && stack.Contains("UnityEditor.Search.SearchDatabase")
            && stack.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup")
            && !stack.Contains("OutOfReach"))
        {
            File.AppendAllText(Path.Combine(Output, "editor-environment-errors.txt"), entry);
            return;
        }
        SessionState.SetString(Errors, SessionState.GetString(Errors, "") + entry);
        File.AppendAllText(Path.Combine(Output, "runtime-errors.txt"), entry);
    }
    public static void RequireNoRuntimeErrors()
    {
        string errors = SessionState.GetString(Errors, "");
        if (!string.IsNullOrWhiteSpace(errors)) throw new InvalidOperationException("Runtime errors:\n" + errors);
    }
    public static void Finish(bool passed, string text)
    {
        SessionState.SetString(Flag, string.Empty);
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, passed ? "PASS.txt" : "FAIL.txt"), text);
        Debug.Log((passed ? "I02_PLAY_QA_PASS " : "I02_PLAY_QA_FAIL ") + text + " output=" + Output);
        EditorApplication.Exit(passed ? 0 : 1);
    }
}

[DefaultExecutionOrder(200)] // Observe after cinematics (100), motion (120) and hands (140).
public sealed class I02PlayQaDriver : MonoBehaviour
{
    readonly List<string> checks = new List<string>();
    MissionRuntime runtime;
    I01MissionComposition composition;
    FirstPersonController player;
    InteractionResolver resolver;
    MissionFlightBoard board;
    UIDocument document;
    Camera sceneCamera;
    FirstPersonMotionPresenter motion;
    AstronautHandsPresenter hands;
    Transform cinematicPivot, motionPivot, resolverOrigin;
    Renderer[] handRenderers;
    bool measureMotion, measureCinematic;
    int groundedMotionSamples, measuredMotionFrames, evaMotionSamples, evaMotionViolations, evaHiddenHandsSamples;
    float peakMotionMetres, peakMotionDegrees, peakAimSeparationMetres;
    float peakCinematicMetres, peakCinematicDegrees;
    Keyboard keyboard;
    InputSettings originalInputSettings, qaInputSettings;
    bool originalRunInBackground;
    MissionSession observedSession;
    int requests, finishes, requestedSequence, finishedSequence, attempts;
    CommandResult lastInteraction;
    Vector3 cinematicRestPosition;
    Quaternion cinematicRestRotation;
    float heldMovementSeconds;
    bool heldMovementTouchedWall;
    VisualElement Root => document.rootVisualElement;
    string Stage { get; set; } = "initialization";

    IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        // QA device delivery must not depend on editor window focus. Clone settings in memory only.
        originalInputSettings = InputSystem.settings;
        qaInputSettings = Object.Instantiate(originalInputSettings);
        qaInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        qaInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings = qaInputSettings;
        originalRunInBackground = Application.runInBackground;
        Application.runInBackground = true;
        keyboard = InputSystem.AddDevice<Keyboard>("I02 QA keyboard");
        var stack = new Stack<IEnumerator>();
        stack.Push(Exercise());
        while (stack.Count > 0)
        {
            bool next = false;
            object current = null;
            Exception failure = null;
            try
            {
                I02PlayQa.RequireNoRuntimeErrors();
                next = stack.Peek().MoveNext();
                if (next) current = stack.Peek().Current;
            }
            catch (Exception error) { failure = error; }
            if (failure != null)
            {
                // Dispose nested enumerators so capture settings and accounting windows can restore.
                foreach (IEnumerator pending in stack)
                    try { (pending as IDisposable)?.Dispose(); } catch { }
                CleanupInput();
                I02PlayQa.Finish(false, "Stage: " + Stage + "\n" + failure);
                yield break;
            }
            if (!next) { stack.Pop(); continue; }
            if (current is IEnumerator nested) { stack.Push(nested); continue; }
            yield return current;
        }
        CleanupInput();
        I02PlayQa.Finish(true, checks.Count + " checks passed.\n" + string.Join("\n", checks)
            + "\nAutomated runtime smoke only. QA teleports are not a walkthrough; no human-play claim.");
    }

    readonly Dictionary<int, CinematicId> cinematicRequests = new Dictionary<int, CinematicId>();
    readonly Dictionary<int, CinematicId> cinematicFinishes = new Dictionary<int, CinematicId>();

    IEnumerator Exercise()
    {
        yield return WaitFor(() => Object.FindFirstObjectByType<I01MissionComposition>() != null, 15, "Full Bootstrap composition starts");
        BindScene(); yield return Frames(10);
        Check(SceneManager.GetActiveScene().path == I02PlayQa.ScenePath, "Saved Bootstrap is the active full-mission scene.");
        Check(runtime.InteractionTargets.Count == 8 && runtime.InteractionTargets.All(x => x && x.IsBound), "All eight authored mission targets are bound.");
        Check(runtime.Gates.Count == 2 && runtime.EvaRoute && runtime.EvaRoute.IsValid, "Two authoritative gates and a valid real tether route are bound.");
        Check(ActiveObjects<Camera>().Length == 1 && ActiveObjects<AudioListener>().Length == 1 && ActiveObjects<MissionRuntime>().Length == 1,
            "One active camera, listener and authoritative runtime in the full scene.");
        Check(player.InputSource is PlayerInputActions && runtime.DriveFromUpdate, "Actual player Input System and runtime frame stepping remain in use.");
        CheckGates(true);
        CheckFirstPersonBindings();
        Check(!runtime.EvaStatus.IsSuitOn && hands.IsBound && !hands.IsVisible && !hands.ModelRoot.gameObject.activeSelf,
            "Hands are bound but their actual model is hidden in the unsuited Planning session.");
        CheckMotionNeutral("Planning");
        Stage = "success planning and launch";
        Submit(ButtonStarting("Practice")); yield return Frames(3);
        Check(composition.ModeChosen && !runtime.PlanningDeadline.IsEnabled, "Actual UI Practice selected.");
        Check(runtime.Planner.Draft.Validate().Succeeded && Root.Query<VisualElement>(className:"equipment-row").ToList().Count == 7,
            "Seven actual catalog controls and a valid success loadout.");
        int allRepairParts = runtime.Repairables.Sum(x => (int)x.RepairCosts.Where(c => c.Resource == ResourceId.SparePartsCount).Sum(c => c.Quantity));
        Check(runtime.Planner.Draft.InitialResources.SparePartsCount >= allRepairParts, "Success loadout covers all three authoritative repair costs.");
        yield return Capture("01-success-planning");
        Submit(Root.Q<Button>(className:"launch-button")); yield return Frames(4);
        Check(runtime.GameState.Phase == GamePhase.Intro && player.ControlAllowed, "Actual UI launch releases player into Intro.");
        yield return MovementAndCollision();
        yield return CockpitImpact();
        Stage = "success interior repairs";
        yield return Repair(InteractionIds.RepairPower, ShipSystemId.Power);
        yield return Repair(InteractionIds.RepairLifeSupport, ShipSystemId.LifeSupport);
        Check(runtime.GameState.Phase == GamePhase.Repair && composition.InteriorRepairsComplete, "Actual power and life support repaired; mission remains viable.");
        CheckHudResources();
        yield return Capture("02-interior-restored");

        Stage = "unsuited gate and suit preparation";
        PositionAt(InteractionIds.EvaExit); yield return Frames(2); CheckFocus(InteractionIds.EvaExit);
        runtime.DriveFromUpdate = false;
        ResourceSnapshot beforeUnsuited = runtime.Resources.Snapshot;
        yield return Interact(false, "Unsuited exit");
        Check(EqualResources(beforeUnsuited, runtime.Resources.Snapshot) && !runtime.EvaStatus.IsOutside && !runtime.EvaStatus.IsSuitOn,
            "Unsuited exit is rejected without resource charge or outside-state change.");
        runtime.DriveFromUpdate = true; CheckGates(true);
        PositionAt(InteractionIds.SuitLocker); yield return Frames(2); CheckFocus(InteractionIds.SuitLocker);
        Record("ACTUAL CORE TIME JUMP: suit prompt='" + resolver.CurrentPrompt + "'; preparation=" + runtime.Eva.Spec.SuitPreparationSeconds + " simulated seconds.");
        runtime.DriveFromUpdate = false;
        double beforeSuitTime = runtime.Clock.ElapsedSimulationSeconds;
        double beforeSuitOxygen = runtime.Resources.Snapshot.OxygenKg;
        yield return Interact(true, "Suit preparation");
        Check(Near(runtime.Clock.ElapsedSimulationSeconds - beforeSuitTime, runtime.Eva.Spec.SuitPreparationSeconds),
            "Actual suit command advances the disclosed preparation interval through the Core model.");
        Check(runtime.EvaStatus.IsSuitOn && !runtime.EvaStatus.IsSuitSealed && runtime.Eva.SuitPreparationCount == 1,
            "Actual suit is on once and remains unsealed inside the pressurized cabin.");
        Check(beforeSuitOxygen - runtime.Resources.Snapshot.OxygenKg >= runtime.Eva.Spec.SuitPreparationOxygenKg - 1e-8,
            "Core charged suit purge/tank oxygen plus modeled preparation consumption.");
        ResourceSnapshot prepared = runtime.Resources.Snapshot;
        yield return Interact(false, "Repeated suit preparation");
        Check(EqualResources(prepared, runtime.Resources.Snapshot) && runtime.Eva.SuitPreparationCount == 1, "Repeated suit action cannot charge twice.");
        runtime.DriveFromUpdate = true;
        yield return Frames(3);
        Check(runtime.EvaStatus.IsSuitOn && HandsRendered(), "Real suit preparation reveals all 34 neutral glove parts.");
        yield return KeyPress(Key.Escape); yield return Frames(2);
        Check(runtime.IsPaused && runtime.EvaStatus.IsSuitOn && !hands.IsVisible && !hands.ModelRoot.gameObject.activeSelf,
            "Actual Escape pause hides the worn gloves without changing authoritative suit state.");
        CheckMotionNeutral("Suited pause");
        yield return KeyPress(Key.Escape); yield return Frames(3);
        Check(!runtime.IsPaused && runtime.EvaStatus.IsSuitOn && HandsRendered(), "Actual Escape resume restores the still-worn gloves.");

        Stage = "airlock depressurization and authoritative exit";
        PositionAt(InteractionIds.AirlockControls); yield return Frames(2); CheckFocus(InteractionIds.AirlockControls);
        runtime.DriveFromUpdate = false;
        ResourceSnapshot beforeCycle = runtime.Resources.Snapshot;
        yield return Interact(true, "Depressurize airlock");
        Check(runtime.GameState.Phase == GamePhase.Eva && runtime.EvaStatus.Airlock == AirlockState.Depressurizing && runtime.EvaStatus.IsSuitSealed,
            "Airlock command seals suit/inner hatch and starts actual EVA depressurization.");
        Check(Near(beforeCycle.OxygenKg-runtime.Resources.Snapshot.OxygenKg, runtime.Eva.Spec.AirlockCycleOxygenLossKg)
            && Near(beforeCycle.BatteryKWh-runtime.Resources.Snapshot.BatteryKWh, runtime.Eva.Spec.AirlockCycleEnergyKWh),
            "Actual airlock loss and pump energy are charged exactly once.");
        runtime.DriveFromUpdate = true; yield return Frames(2); CheckGates(false);
        double depressStart = runtime.Clock.ElapsedSimulationSeconds;
        double depressRemaining = runtime.EvaStatus.AirlockTransitionRemainingSeconds;
        yield return WaitFor(() => runtime.EvaStatus.Airlock == AirlockState.OuterOpen, 90, "Actual airlock depressurization");
        Check(runtime.Clock.ElapsedSimulationSeconds - depressStart + 1e-8 >= depressRemaining,
            "Depressurization awaited actual clock advancement; no QA time advancement.");
        PositionAt(InteractionIds.EvaExit); yield return Frames(2); CheckFocus(InteractionIds.EvaExit);
        yield return Interact(true, "Clip tether and exit"); yield return Frames(3);
        Check(runtime.EvaStatus.IsOutside && player.IsOnRoute && ReferenceEquals(player.ActiveRoute, runtime.EvaRoute),
            "Actual exit command sets outside state and runtime attaches the real constrained route.");
        Check(!player.GetComponent<CharacterController>().enabled, "Actual tether traversal disables cabin walking physics.");
        Check(runtime.EvaStatus.IsSuitOn && HandsRendered(), "Actual EVA exit retains the worn glove model on the first-person view.");
        CheckMotionNeutral("EVA entry");
        CheckGates(false);
        yield return Capture("03-eva-start");

        Stage = "real tether traversal and antenna repair";
        yield return TraverseRoute(false);
        Vector3 routeEnd = runtime.EvaRoute.EndPosition;
        var routePoints = new SerializedObject(runtime.EvaRoute).FindProperty("waypoints");
        Vector3 beforeEnd = ((Transform)routePoints.GetArrayElementAtIndex(routePoints.arraySize - 2).objectReferenceValue).position;
        Vector3 endTangent = (routeEnd - beforeEnd).normalized;
        Vector3 outward = (endTangent + Vector3.Cross(endTangent, Vector3.up)).normalized;
        Record("QA AIM ONLY: pushing W beyond the actual route end and sideways to verify tether constraint. Direction="
            + outward + "; no position or outside-state assignment. Derived from last segment; world +X can point back along a bent route.");
        player.ViewPivot.rotation = Quaternion.LookRotation(outward);
        yield return HoldRealForward(.8f);
        Check(Vector3.Distance(runtime.EvaRoute.Constrain(player.transform.position), player.transform.position) < .002f
            && Vector3.Distance(player.transform.position, routeEnd) <= runtime.EvaRoute.LateralFreedomMetres + .05f,
            "Real outward input is clamped to the authored tether corridor/end.");
        yield return Repair(InteractionIds.RepairAntenna, ShipSystemId.Antenna);
        Check(runtime.Session.Systems.Antenna.RepairCount == 1 && runtime.EvaStatus.IsOutside, "Actual antenna repair succeeds once while outside.");
        yield return Capture("04-antenna-restored");
        Stage = "tether return and repressurization";
        yield return TraverseRoute(true);
        AimAt(InteractionIds.EvaReturn); yield return Frames(2); CheckFocus(InteractionIds.EvaReturn);
        runtime.DriveFromUpdate = false;
        ResourceSnapshot beforeReturn = runtime.Resources.Snapshot;
        yield return Interact(true, "Return to airlock"); yield return Frames(2);
        Check(!runtime.EvaStatus.IsOutside && !player.IsOnRoute && player.GetComponent<CharacterController>().enabled,
            "Actual return command restores cabin walking at authored reentry.");
        Check(EqualResources(beforeReturn, runtime.Resources.Snapshot), "Return action has no resource charge.");
        runtime.DriveFromUpdate = true;
        PositionAt(InteractionIds.AirlockControls); yield return Frames(2);
        yield return Interact(true, "Repressurize airlock");
        Check(runtime.EvaStatus.Airlock == AirlockState.Repressurizing, "Actual repressurization started."); CheckGates(false);
        double repressStart = runtime.Clock.ElapsedSimulationSeconds;
        double repressRemaining = runtime.EvaStatus.AirlockTransitionRemainingSeconds;
        yield return WaitFor(() => runtime.EvaStatus.Airlock == AirlockState.InnerOpen, 90, "Actual airlock repressurization");
        yield return Frames(3);
        Check(runtime.Clock.ElapsedSimulationSeconds - repressStart + 1e-8 >= repressRemaining,
            "Repressurization awaited the actual remaining simulation interval.");
        Check(runtime.GameState.Phase == GamePhase.Transmission && runtime.Eva.CompletedEvaCount == 1
            && !runtime.EvaStatus.IsSuitSealed && !runtime.EvaStatus.IsSuitOn, "Complete actual EVA returns to Transmission with suit released.");
        CheckGates(true);
        Check(!runtime.EvaStatus.IsSuitOn && !hands.IsVisible && !hands.ModelRoot.gameObject.activeSelf,
            "Actual completed repressurization removes the suit and hides the glove model.");
        CheckMotionNeutral("After repressurization");

        Stage = "local replay diagnostic and causal transmission";
        PositionAt(InteractionIds.CockpitConsole); yield return Frames(2); CheckFocus(InteractionIds.CockpitConsole);
        yield return Interact(true, "Local replay diagnostic");
        Check(runtime.Session.Director.LinkDiagnosticRequested && !runtime.CommunicationDetails.HasTransmitted,
            "Diagnostic requests the local false-signal beat without transmitting or fabricating an Earth reply.");
        yield return WaitFor(() => runtime.Session.Director.LinkDiagnosticCompleted && !runtime.Session.Cinematics.IsWaiting, 55, "Actual FalseSignal acknowledgement");
        CheckCinematic(CinematicId.FalseSignal);
        Check(runtime.Communications.State != CommunicationState.ReplyReceived, "Local replay is not a received Earth reply.");
        yield return Interact(true, "Transmit distress signal");
        var link = runtime.CommunicationDetails;
        Check(link.HasTransmitted && link.State == CommunicationState.AwaitingReply, "Actual console command sends one distress transmission.");
        double expectedDelay = 2 * link.OneWayLightSeconds + link.EarthProcessingSeconds;
        Check(link.TransmittedAtSimulationSeconds.HasValue && link.ReplyDueAtSimulationSeconds.HasValue
            && Near(link.ReplyDueAtSimulationSeconds.Value - link.TransmittedAtSimulationSeconds.Value, expectedDelay),
            "Actual reply due time equals outbound light time + ground processing + inbound light time.");
        Check(link.ReferenceDistanceKm > 0 && link.Reference.Count == 32 && !string.IsNullOrWhiteSpace(link.AstronomicalReferenceId),
            "Actual communication uses the packaged 32-row NASA reference dataset.");
        double due = link.ReplyDueAtSimulationSeconds.Value;
        runtime.DriveFromUpdate = false;
        ResourceSnapshot afterTransmit = runtime.Resources.Snapshot;
        yield return Interact(false, "Repeated transmission");
        Check(EqualResources(afterTransmit, runtime.Resources.Snapshot) && Near(due, link.ReplyDueAtSimulationSeconds.Value),
            "Repeated transmission neither charges again nor resets the reply due time.");
        runtime.DriveFromUpdate = true;
        yield return Capture("05-awaiting-earth-reply");
        double replyWaitStart = Time.realtimeSinceStartupAsDouble;
        double remaining = link.ReplyRemainingSimulationSeconds;
        double scale = runtime.Clock.TimeScale;
        double replyTimeout = Math.Max(90, remaining / scale * 4 + 30);
        while (link.State != CommunicationState.ReplyReceived)
        {
            CheckNoEarlyReply(link, due);
            if (Time.realtimeSinceStartupAsDouble - replyWaitStart > replyTimeout) throw new TimeoutException("Actual modeled Earth reply did not arrive.");
            yield return null;
        }
        Check(link.ReplyReceivedAtSimulationSeconds.HasValue && link.ReplyReceivedAtSimulationSeconds.Value + 1e-8 >= due,
            "Reply arrives no earlier than the complete modeled round trip and processing delay.");
        Check(Time.realtimeSinceStartupAsDouble - replyWaitStart >= Math.Max(0, remaining / scale - .5),
            "Reply waited through real runtime frames at the displayed scale; no fast-forward command used.");
        yield return WaitFor(() => runtime.GameState.Phase == GamePhase.Report, 120, "Actual restoration and ending cinematics into report");
        CheckCinematic(CinematicId.CommunicationRestored); CheckCinematic(CinematicId.EndingPullback);
        Check(runtime.Session.Cinematics.IgnoredAcknowledgements == 0, "All success cinematic acknowledgements matched exactly once.");
        CheckReport(MissionOutcome.RecoveryPossible);
        yield return Capture("06-success-report");

        Stage = "report restart and actual low-parts mission loss";
        yield return RestartFromReport();
        Submit(ButtonStarting("Practice")); yield return Frames(3);
        while (Quantity(runtime.Planner.Draft, "spare-parts-kit") > 1)
        { Submit(EquipmentButton("spare-parts-kit", "−")); yield return Frames(2); }
        Check(Quantity(runtime.Planner.Draft, "spare-parts-kit") == 1 && runtime.Planner.Draft.Validate().Succeeded,
            "Actual planner UI makes a valid low-parts loadout with one repair kit.");
        int interiorParts = runtime.Repairables.Where(x => x.SystemId != ShipSystemId.Antenna)
            .Sum(x => (int)x.RepairCosts.Where(c => c.Resource == ResourceId.SparePartsCount).Sum(c => c.Quantity));
        Check(runtime.Planner.Draft.InitialResources.SparePartsCount == interiorParts, "Low-parts loadout covers interior repairs but leaves no antenna parts.");
        Submit(Root.Q<Button>(className:"launch-button")); yield return Frames(4);
        yield return CockpitImpact();
        yield return Repair(InteractionIds.RepairPower, ShipSystemId.Power);
        yield return Repair(InteractionIds.RepairLifeSupport, ShipSystemId.LifeSupport, false);
        Check(runtime.GameState.Outcome == MissionOutcome.Lost && runtime.Session.Director.LossCause == SurvivalFailure.ContactImpossible,
            "Core decides objective loss after actual repairs exhaust antenna parts; no state mutation by QA.");
        yield return WaitFor(() => runtime.GameState.Phase == GamePhase.Report, 90, "Actual loss ending and report");
        CheckCinematic(CinematicId.EndingPullback);
        CheckReport(MissionOutcome.Lost);
        Check(runtime.Report.OutcomeReason.Contains("Mission objective unrecoverable"), "Loss report explains the actual antenna-parts decision.");
        yield return Capture("07-objective-loss-report");
        yield return RestartFromReport();
        Check(runtime.GameState.Phase == GamePhase.Planning && runtime.Report == null && !runtime.EvaStatus.IsOutside,
            "Report restart returns a fresh Planning session without a stale report or outside state.");
        I02PlayQa.RequireNoRuntimeErrors(); Check(true, "No game runtime Error/Exception/Assert during the full success and loss smoke passes.");
    }

    IEnumerator CockpitImpact()
    {
        Stage = "actual cockpit impact";
        PositionAt(InteractionIds.CockpitConsole); yield return Frames(2); CheckFocus(InteractionIds.CockpitConsole);
        // The actual serialized cinematic offset is above the new walk-motion pivot.
        cinematicRestPosition = cinematicPivot.localPosition;
        cinematicRestRotation = cinematicPivot.localRotation;
        peakCinematicMetres = peakCinematicDegrees = 0f;
        measureCinematic = true;
        yield return Interact(true, "Cockpit systems check");
        Check(runtime.Session.Director.AccidentTriggered && runtime.Session.Cinematics.IsWaiting && !player.ControlAllowed,
            "Actual cockpit command applies the accident and locks controls for its bound cinematic.");
        float configured = new SerializedObject(composition.Cinematics).FindProperty("impactSeconds").floatValue;
        Record("Actual bound impact duration=" + configured + " real seconds; no short hard-coded cinematic completion.");
        yield return WaitFor(() => !runtime.Session.Cinematics.IsWaiting, Math.Max(55, configured + 15), "Actual Impact acknowledgement");
        yield return Frames(3); CheckCinematic(CinematicId.Impact);
        measureCinematic = false;
        Check(peakCinematicMetres > .0001f || peakCinematicDegrees > .05f,
            "The actual serialized cinematic offset moved during the unreduced impact. Peak metres="
            + peakCinematicMetres.ToString("F6") + " degrees=" + peakCinematicDegrees.ToString("F4"));
        CheckMotionNeutral("After impact");
        Check(runtime.Session.Cinematics.LastCompletion.HasValue && !runtime.Session.Cinematics.LastCompletion.Value.TimedOut
            && runtime.GameState.Phase == GamePhase.Emergency && player.ControlAllowed,
            "Actual acknowledged impact enters Emergency and restores input without timeout.");
        Check(Vector3.Distance(cinematicPivot.localPosition, cinematicRestPosition) < .001f
            && Quaternion.Angle(cinematicPivot.localRotation, cinematicRestRotation) < .01f,
            "Impact camera offset returns to its actual resting pose.");
    }

    IEnumerator Interact(bool expectedSuccess, string label)
    {
        int previousAttempts = attempts;
        yield return KeyPress(Key.E);
        Check(attempts == previousAttempts + 1 && lastInteraction.Succeeded == expectedSuccess,
            label + " uses real E / resolver once; result=" + lastInteraction.Succeeded + ": " + lastInteraction.Explanation);
    }

    void CheckGates(bool innerOpen)
    {
        var inner = runtime.Gates.Single(x => x.GateId == GateIds.AirlockInner);
        var outer = runtime.Gates.Single(x => x.GateId == GateIds.AirlockOuter);
        Check(inner.IsOpen == innerOpen && inner.Blocker.enabled != innerOpen,
            "Inner physical gate agrees with authoritative airlock passability=" + innerOpen + ".");
        Check(!outer.IsOpen && outer.Blocker.enabled, "Outer physical gate remains closed to walking; only actual EVA commands cross it.");
    }

    IEnumerator TraverseRoute(bool returning)
    {
        Check(runtime.EvaStatus.IsOutside && player.IsOnRoute, "Route traversal starts from actual outside/tether state.");
        var source = new SerializedObject(runtime.EvaRoute).FindProperty("waypoints");
        var destinations = new List<Vector3>();
        for (int i=0; i<source.arraySize; i++) destinations.Add(((Transform)source.GetArrayElementAtIndex(i).objectReferenceValue).position);
        if (returning) destinations.Reverse();
        Record("QA AIM ONLY: steering view toward actual authored tether stations; traversal uses real W. No route teleport/outside-state assignment.");
        float beforeAlong = runtime.EvaRoute.DistanceAlong(player.transform.position);
        foreach (Vector3 targetFeet in destinations.Skip(1))
        {
            double end = Time.realtimeSinceStartupAsDouble + Math.Max(30, Vector3.Distance(player.transform.position,targetFeet) / runtime.EvaRoute.SpeedMetresPerSecond * 5);
            QueueKeys(Key.W);
            while (Vector3.Distance(player.transform.position,targetFeet) > .12f)
            {
                Vector3 direction = targetFeet-player.transform.position;
                player.transform.rotation = Quaternion.Euler(0, Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg, 0);
                player.ViewPivot.rotation = Quaternion.LookRotation(direction.normalized,Vector3.up);
                if (!runtime.EvaStatus.IsOutside || !player.IsOnRoute || !player.ControlAllowed)
                    throw new InvalidOperationException("Actual EVA traversal lost its state/control gate: " + runtime.GameState.Phase);
                if (Vector3.Distance(runtime.EvaRoute.Constrain(player.transform.position),player.transform.position)>.003f)
                    throw new InvalidOperationException("Player escaped the real route corridor.");
                if (Time.realtimeSinceStartupAsDouble>end) throw new TimeoutException("Real W could not reach authored tether station " + targetFeet + " " + MovementDiagnostic());
                yield return null;
            }
        }
        QueueKeys(); yield return Frames(2);
        float afterAlong = runtime.EvaRoute.DistanceAlong(player.transform.position);
        Check(returning ? afterAlong < .3f && beforeAlong > afterAlong : afterAlong > runtime.EvaRoute.LengthMetres-.3f && afterAlong>beforeAlong,
            returning ? "Real constrained input traverses back to the return station." : "Real constrained input traverses the route to the antenna station.");
        Check(runtime.EvaStatus.SuitOxygenKg > 0, "Actual suit oxygen remains positive after traversal.");
        Check(evaMotionSamples > 0 && evaMotionViolations == 0 && evaHiddenHandsSamples == 0,
            "Actual EVA traversal keeps gloves visible and both visual motion offsets neutral after LateUpdate. Samples="
            + evaMotionSamples + " motionViolations=" + evaMotionViolations + " hiddenHands=" + evaHiddenHandsSamples);
    }

    void AimAt(string id)
    {
        var target = runtime.InteractionTargets.Single(x=>x.InteractionId==id);
        Vector3 center=target.GetComponent<Collider>().bounds.center;
        Vector3 direction=center-player.ViewPivot.position;
        player.transform.rotation=Quaternion.Euler(0,Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg,0);
        player.ViewPivot.rotation=Quaternion.LookRotation(center-player.ViewPivot.position,Vector3.up);
        Physics.SyncTransforms(); resolver.Refresh();
    }

    void CheckCinematic(CinematicId id)
    {
        var requested=cinematicRequests.Where(x=>x.Value==id).ToList();
        Check(requested.Count==1 && cinematicFinishes.ContainsKey(requested[0].Key) && cinematicFinishes[requested[0].Key]==id,
            id + " has one actual request and its matching presentation acknowledgement.");
    }

    void CheckNoEarlyReply(OutOfReach.Simulation.CommunicationSystem link,double due)
    {
        if (runtime.GameState.Outcome==MissionOutcome.Lost) throw new InvalidOperationException("Mission lost before Earth reply: " + runtime.Report?.OutcomeReason);
        if (link.ReplyReceivedAtSimulationSeconds.HasValue && link.ReplyReceivedAtSimulationSeconds.Value+1e-8<due)
            throw new InvalidOperationException("Earth reply arrived before its physical delay.");
        if (link.State==CommunicationState.AwaitingReply && Math.Abs(link.ReplyRemainingSimulationSeconds-Math.Max(0,due-runtime.Clock.ElapsedSimulationSeconds))>1e-6)
            throw new InvalidOperationException("Communication countdown diverged from the authoritative simulation clock.");
    }

    void CheckReport(MissionOutcome outcome)
    {
        Check(runtime.GameState.Phase==GamePhase.Report && runtime.Report!=null && runtime.Report.IsFinal && runtime.Report.Outcome==outcome,
            "Actual final report outcome=" + outcome + ".");
        Check(MissionReportSections.Ordered.All(id=>runtime.Report.Sections.Any(section=>section.Id==id)),
            "Actual report includes mission design, resources, systems, communications and separate science/source/fiction/pacing sections.");
        Check(runtime.Report.Communication.DatasetSha256.Length==64 && runtime.Report.Communication.SampleCount==32,
            "Actual report carries the NASA dataset hash and 32-sample provenance.");
        Check(Root.Q<VisualElement>("mission-record-host").style.display.value==DisplayStyle.Flex
            && Root.Q<Button>("reportRestartButton")!=null, "Composition displays the actual final report and its real restart action.");
        File.WriteAllText(Path.Combine(I02PlayQa.Output,outcome+"-report.txt"),runtime.Report.ToPlainText());
    }

    IEnumerator RestartFromReport()
    {
        MissionSession old=runtime.Session;
        UnobserveSession();
        Submit(Root.Q<Button>("reportRestartButton"));
        yield return WaitFor(()=>{
            var owner=Object.FindFirstObjectByType<I01MissionComposition>();
            return owner && owner.Runtime && owner.Runtime.Session!=null && !ReferenceEquals(owner.Runtime.Session,old);
        },20,"Actual report restart");
        BindScene(); yield return Frames(8);
        Check(old.IsDisposed && runtime.GameState.Phase==GamePhase.Planning && !composition.ModeChosen,
            "Actual report restart disposes old state and opens a fresh planning mode choice.");
        Check(ActiveObjects<Camera>().Length==1 && ActiveObjects<AudioListener>().Length==1, "Restart preserves one active camera and listener.");
        CheckFirstPersonBindings();
        Check(hands.IsBound && !runtime.EvaStatus.IsSuitOn && !hands.IsVisible && !hands.ModelRoot.gameObject.activeSelf,
            "Actual report restart rebinds hands to the fresh unsuited session and leaves no old glove model visible.");
        CheckMotionNeutral("Restart");
    }



    void BindScene()
    {
        composition = Object.FindFirstObjectByType<I01MissionComposition>();
        runtime = composition.Runtime;
        player = runtime.Player;
        resolver = player.Interaction;
        board = Object.FindFirstObjectByType<MissionFlightBoard>();
        document = board.GetComponent<UIDocument>();
        sceneCamera = ActiveObjects<Camera>().Single();
        var compositionFields = new SerializedObject(composition);
        motion = compositionFields.FindProperty("firstPersonMotion").objectReferenceValue as FirstPersonMotionPresenter;
        hands = compositionFields.FindProperty("astronautHands").objectReferenceValue as AstronautHandsPresenter;
        motionPivot = compositionFields.FindProperty("firstPersonMotionPivot").objectReferenceValue as Transform;
        cinematicPivot = new SerializedObject(composition.Cinematics).FindProperty("cameraOffsetPivot").objectReferenceValue as Transform;
        resolverOrigin = new SerializedObject(resolver).FindProperty("viewOrigin").objectReferenceValue as Transform;
        if (!motion || !hands || !motionPivot || !cinematicPivot || !hands.ModelRoot || !hands.MotionMount)
            throw new InvalidOperationException("I03 first-person QA requires explicit motion, gloves and actual cinematic pivot bindings.");
        handRenderers = hands.ModelRoot.GetComponentsInChildren<Renderer>(true);
        measureMotion = measureCinematic = false;
        evaMotionSamples = evaMotionViolations = evaHiddenHandsSamples = 0;
        observedSession = runtime.Session;
        observedSession.Events.CinematicRequested += OnRequested;
        observedSession.Events.CinematicFinished += OnFinished;
        resolver.Interacted += OnInteracted;
        requests = finishes = requestedSequence = finishedSequence = attempts = 0;
        cinematicRequests.Clear(); cinematicFinishes.Clear();
    }
    void UnobserveSession()
    {
        if (observedSession != null)
        {
            observedSession.Events.CinematicRequested -= OnRequested;
            observedSession.Events.CinematicFinished -= OnFinished;
        }
        if (resolver) resolver.Interacted -= OnInteracted;
        observedSession = null;
    }
    void OnRequested(CinematicRequested request)
    {
        cinematicRequests.Add(request.SequenceId, request.Cinematic);
        if (request.Cinematic != CinematicId.Impact) return;
        requests++; requestedSequence = request.SequenceId;
    }
    void OnFinished(CinematicFinished finish)
    {
        cinematicFinishes.Add(finish.SequenceId, finish.Cinematic);
        if (finish.Cinematic != CinematicId.Impact) return;
        finishes++; finishedSequence = finish.SequenceId;
    }
    void OnInteracted(CommandResult result) { attempts++; lastInteraction = result; }

    IEnumerator MovementAndCollision()
    {
        Record("QA TELEPORT: feet (0, 0.05, 3.2), yaw 0, to isolate actual WASD movement. Not a walked route.");
        player.Teleport(new Vector3(0, .05f, 3.2f), 0); Physics.SyncTransforms(); yield return Frames(2);
        Vector3 start = player.transform.position;
        Record("Movement probe before W: " + MovementDiagnostic());
        Check(motion.IsBound && motion.MotionEnabled && !motion.ReducedMotion && !composition.Cinematics.ReducedEffects,
            "Normal real-W probe starts with enabled, unreduced walk presentation.");
        BeginMotionMeasurement();
        yield return HoldRealForward(.4f);
        measureMotion = false;
        Vector3 travel = player.transform.position - start;
        float expected = heldMovementSeconds * player.WalkSpeedMetresPerSecond;
        Record("Movement probe after W: travel=" + travel.ToString("F4") + " expectedForward=" + expected.ToString("F4")
            + " controllerFrameSeconds=" + heldMovementSeconds.ToString("F4") + " | " + MovementDiagnostic());
        Check(travel.z > expected * .75f && travel.z < expected * 1.25f && Mathf.Abs(travel.x) < .1f,
            "Injected W moves the actual CharacterController at its configured speed over measured controller frame time."
            + " Actual=" + travel.ToString("F4") + " expectedForward=" + expected.ToString("F4"));
        Check(groundedMotionSamples >= 2 && peakMotionMetres > .00005f && peakMotionMetres < .01f
            && peakMotionDegrees < .16f && peakAimSeparationMetres > .00005f,
            "Actual grounded W travel produces restrained camera-only motion with an unbobbed aim origin. Grounded samples="
            + groundedMotionSamples + " peakMetres=" + peakMotionMetres.ToString("F6")
            + " peakDegrees=" + peakMotionDegrees.ToString("F4") + " aimSeparation=" + peakAimSeparationMetres.ToString("F6"));
        yield return WaitFor(MotionIsNeutral, 3, "Walk offsets settle after released real W");
        CheckMotionNeutral("Standing after real W");
        yield return SetReducedThroughPause(true);
        Record("QA TELEPORT: feet (0, 0.05, 3.2), yaw 90, to test the existing corridor wall. Not a walked route.");
        player.Teleport(new Vector3(0, .05f, 3.2f), 90); Physics.SyncTransforms(); yield return Frames(2);
        start = player.transform.position;
        BeginMotionMeasurement();
        yield return HoldRealForward(1.8f);
        measureMotion = false;
        float distance = Vector3.ProjectOnPlane(player.transform.position - start, Vector3.up).magnitude;
        Record("Wall probe: distance=" + distance.ToString("F4") + " sideContact=" + heldMovementTouchedWall
            + " controllerFrameSeconds=" + heldMovementSeconds.ToString("F4") + " | " + MovementDiagnostic());
        Check(heldMovementTouchedWall && distance > .1f && distance < player.WalkSpeedMetresPerSecond * heldMovementSeconds * .8f,
            "Real movement meets an existing scene wall and is stopped by CharacterController collision.");
        Check(measuredMotionFrames > 0 && groundedMotionSamples > 0 && peakMotionMetres < .00001f && peakMotionDegrees < .001f,
            "Actual reduced-effects UI setting keeps camera neutral during the existing real-W wall traversal. Grounded samples="
            + groundedMotionSamples + " peakMetres=" + peakMotionMetres.ToString("F6") + " peakDegrees=" + peakMotionDegrees.ToString("F4"));
        CheckMotionNeutral("Reduced walking");
        yield return SetReducedThroughPause(false);
    }

    // Observations only: do not reset/pose the camera or set suit/phase/outcome to satisfy these checks.
    void LateUpdate()
    {
        if (!runtime || !player || !motionPivot || !hands || !hands.MotionMount) return;
        if (measureMotion)
        {
            measuredMotionFrames++;
            var body = player.GetComponent<CharacterController>();
            if (body.enabled && body.isGrounded && Vector3.ProjectOnPlane(body.velocity, Vector3.up).magnitude > .05f)
                groundedMotionSamples++;
            peakMotionMetres = Mathf.Max(peakMotionMetres, motionPivot.localPosition.magnitude);
            peakMotionDegrees = Mathf.Max(peakMotionDegrees, Quaternion.Angle(motionPivot.localRotation, Quaternion.identity));
            if (resolverOrigin && sceneCamera)
                peakAimSeparationMetres = Mathf.Max(peakAimSeparationMetres, Vector3.Distance(resolverOrigin.position, sceneCamera.transform.position));
        }
        if (measureCinematic && cinematicPivot && composition.Cinematics.IsPlaying)
        {
            peakCinematicMetres = Mathf.Max(peakCinematicMetres, Vector3.Distance(cinematicPivot.localPosition, cinematicRestPosition));
            peakCinematicDegrees = Mathf.Max(peakCinematicDegrees, Quaternion.Angle(cinematicPivot.localRotation, cinematicRestRotation));
        }
        if (runtime.EvaStatus != null && runtime.EvaStatus.IsOutside && player.IsOnRoute && player.ControlAllowed
            && !runtime.IsPaused && !composition.Cinematics.IsPlaying)
        {
            evaMotionSamples++;
            if (!MotionIsNeutral()) evaMotionViolations++;
            if (!HandsRendered()) evaHiddenHandsSamples++;
        }
    }

    void BeginMotionMeasurement()
    {
        groundedMotionSamples = measuredMotionFrames = 0;
        peakMotionMetres = peakMotionDegrees = peakAimSeparationMetres = 0;
        measureMotion = true;
    }

    bool HandsRendered() => hands && hands.IsVisible && handRenderers != null && handRenderers.Length == 34
        && handRenderers.All(part => part && part.enabled && part.gameObject.activeInHierarchy);

    bool MotionIsNeutral() => motionPivot && hands && hands.MotionMount
        && motionPivot.localPosition.sqrMagnitude < 1e-10f && Quaternion.Angle(motionPivot.localRotation, Quaternion.identity) < .001f
        && hands.MotionMount.localPosition.sqrMagnitude < 1e-10f && Quaternion.Angle(hands.MotionMount.localRotation, Quaternion.identity) < .001f;

    void CheckMotionNeutral(string context) => Check(MotionIsNeutral(), context + " leaves camera and hand-motion pivots neutral. Camera="
        + motionPivot.localPosition.ToString("F6") + " hands=" + hands.MotionMount.localPosition.ToString("F6"));

    void CheckFirstPersonBindings()
    {
        Transform actualOrigin = new SerializedObject(resolver).FindProperty("viewOrigin").objectReferenceValue as Transform;
        Check(motion.IsBound && hands.IsBound && motionPivot != cinematicPivot && motionPivot.parent == cinematicPivot
            && sceneCamera.transform.parent == motionPivot, "Actual scene has separate bound cinematic and walk-motion pivots above the sole camera.");
        Check(actualOrigin == cinematicPivot && actualOrigin != motionPivot && actualOrigin != sceneCamera.transform,
            "Actual resolver viewOrigin is the unbobbed serialized cinematic pivot, not the animated camera child.");
        Check(hands.MotionMount.parent == sceneCamera.transform && hands.ModelRoot.parent == hands.MotionMount,
            "Glove model is below its separate camera-child motion mount.");
    }

    IEnumerator SetReducedThroughPause(bool enabled)
    {
        yield return KeyPress(Key.Escape); yield return Frames(2);
        Check(runtime.IsPaused && !player.ControlAllowed, "Actual Escape opens pause before changing reduced effects.");
        CheckMotionNeutral("Pause");
        Toggle setting = Root.Query<Toggle>().ToList().Single(toggle => toggle.label == "Reduced camera motion and light effects");
        setting.value = enabled; // Actual registered UI ChangeEvent; no direct presenter field assignment.
        yield return Frames(2);
        Check(composition.Cinematics.ReducedEffects == enabled && motion.ReducedMotion == enabled,
            "Existing Reduced effects UI updates both cinematic and walk presentation to " + enabled + ".");
        yield return KeyPress(Key.Escape); yield return Frames(3);
        Check(!runtime.IsPaused && player.ControlAllowed, "Actual Escape resumes player control after the comfort setting.");
    }

    IEnumerator HoldRealForward(float controllerSeconds)
    {
        heldMovementSeconds = 0;
        heldMovementTouchedWall = false;
        bool receivedW = false;
        int frames = 0;
        double deadline = Time.realtimeSinceStartupAsDouble + 15;
        var body = player.GetComponent<CharacterController>();
        QueueKeys(Key.W);
        while (heldMovementSeconds < controllerSeconds)
        {
            yield return null;
            frames++;
            // The controller has already consumed this frame's real action and Time.deltaTime.
            bool rawW = keyboard.wKey.isPressed;
            Vector2 move = player.InputSource.Move;
            if (rawW && move.y > .9f && player.ControlAllowed)
            {
                receivedW = true;
                heldMovementSeconds += Time.deltaTime;
                heldMovementTouchedWall |= (body.collisionFlags & CollisionFlags.Sides) != 0;
            }
            if (frames == 2) Record("Movement probe during W: " + MovementDiagnostic());
            if (!receivedW && frames >= 4)
                throw new InvalidOperationException("QA W event did not reach an allowed real Move action after four frames. " + MovementDiagnostic());
            if (Time.realtimeSinceStartupAsDouble > deadline)
                throw new TimeoutException("Real movement did not receive enough positive controller frame time. " + MovementDiagnostic());
        }
        Record("Real W observed for " + frames + " frames / " + heldMovementSeconds.ToString("F4") + " controller seconds.");
        QueueKeys();
        yield return Frames(2);
    }

    string MovementDiagnostic()
    {
        var actions = player.InputSource as PlayerInputActions;
        var move = actions?.Map.FindAction("Move");
        var body = player.GetComponent<CharacterController>();
        return "keyboard=" + keyboard.deviceId + " added=" + keyboard.added + " enabled=" + keyboard.enabled
            + " rawW=" + keyboard.wKey.isPressed + " actionEnabled=" + move?.enabled
            + " Move=" + player.InputSource.Move.ToString("F4") + " activeControl=" + move?.activeControl?.path
            + " boundQaW=" + (move != null && move.controls.Any(control => ReferenceEquals(control, keyboard.wKey)))
            + " gate=" + player.ControlAllowed + " bodyEnabled=" + body.enabled + " collision=" + body.collisionFlags
            + " dt=" + Time.deltaTime.ToString("F6") + " unscaledDt=" + Time.unscaledDeltaTime.ToString("F6")
            + " timeScale=" + Time.timeScale.ToString("F3") + " updateMode=" + InputSystem.settings.updateMode
            + " inputUpdate=" + InputState.currentUpdateType + " feet=" + player.transform.position.ToString("F4")
            + " yaw=" + player.transform.eulerAngles.y.ToString("F3");
    }

    void PositionAt(string id)
    {
        if (runtime.EvaStatus.IsOutside)
        {
            if (id != InteractionIds.RepairAntenna && id != InteractionIds.EvaReturn)
                throw new InvalidOperationException("QA refuses to teleport from authoritative outside state.");
            AimAt(id); return;
        }
        Vector3 feet;
        switch (id)
        {
            case InteractionIds.CockpitConsole: feet=new Vector3(0,.05f,-.1f); break;
            case InteractionIds.RepairPower: feet=new Vector3(0,.05f,6.1f); break;
            case InteractionIds.RepairLifeSupport: feet=new Vector3(0,.05f,8.1f); break;
            case InteractionIds.SuitLocker: feet=new Vector3(.2f,.05f,19f); break;
            case InteractionIds.AirlockControls: feet=new Vector3(-.2f,.05f,19f); break;
            case InteractionIds.EvaExit: feet=new Vector3(0,.05f,19.8f); break;
            default: throw new InvalidOperationException("No authorized QA interior target position for "+id);
        }
        Record("QA TELEPORT inside cabin: "+id+" feet="+feet+". Not a walked navigation route.");
        player.Teleport(feet,0); AimAt(id);
    }
    void CheckFocus(string id)
    {
        resolver.Refresh();
        Check(resolver.CurrentTarget != null && resolver.CurrentTarget.InteractionId == id && resolver.Current != null,
            "Actual raycast resolver focuses " + id + " through its authored collider.");
    }

    IEnumerator Repair(string interactionId, ShipSystemId systemId, bool verifyRepeat = true)
    {
        PositionAt(interactionId); yield return Frames(2); CheckFocus(interactionId);
        IRepairable repairable = runtime.Repairables.Single(value => value.SystemId == systemId);
        Check(repairable.Condition01 < 1d && resolver.CurrentCanInteract, "Actual " + systemId + " repair is available before use.");
        bool priorDrive = runtime.DriveFromUpdate;
        runtime.DriveFromUpdate = false;
        Record("QA accounting window: suspended MissionRuntime.Update stepping around injected E for " + systemId + "; no resources or outcomes assigned.");
        ResourceSnapshot before = runtime.Resources.Snapshot;
        int beforeAttempts = attempts;
        yield return KeyPress(Key.E);
        ResourceSnapshot after = runtime.Resources.Snapshot;
        Check(attempts == beforeAttempts + 1 && lastInteraction.Succeeded && repairable.Condition01 >= 1d, "Injected E performs the real " + systemId + " repair once.");
        foreach (ResourceId id in Enum.GetValues(typeof(ResourceId)))
        {
            double charge = repairable.RepairCosts.Where(cost => cost.Resource == id).Sum(cost => cost.Quantity);
            Check(Near(Value(before, id) - Value(after, id), charge), systemId + " exact " + id
                + " debit equals its authoritative repair cost (" + charge.ToString("0.####", CultureInfo.InvariantCulture) + ").");
        }
        Check(Near(before.CarbonDioxidePpm, after.CarbonDioxidePpm), "Repair does not invent an immediate CO2 change.");
        if (verifyRepeat)
        {
            beforeAttempts = attempts; yield return KeyPress(Key.E);
            Check(attempts == beforeAttempts + 1 && !lastInteraction.Succeeded && EqualResources(after, runtime.Resources.Snapshot),
                "Repeated " + systemId + " interaction is rejected without a second debit.");
        }
        CheckHudResources();
        runtime.DriveFromUpdate = priorDrive; yield return Frames(2);
    }

    void CheckHudResources()
    {
        ResourceSnapshot snapshot = runtime.Resources.Snapshot;
        string[] expected = {
            snapshot.OxygenKg.ToString("0.0", CultureInfo.InvariantCulture) + " kg",
            snapshot.WaterLitres.ToString("0.0", CultureInfo.InvariantCulture) + " L",
            snapshot.FoodKg.ToString("0.0", CultureInfo.InvariantCulture) + " kg",
            snapshot.BatteryKWh.ToString("0.0", CultureInfo.InvariantCulture) + " kWh",
            snapshot.SparePartsCount.ToString(CultureInfo.InvariantCulture),
            snapshot.CarbonDioxidePpm.ToString("0", CultureInfo.InvariantCulture) + " ppm"
        };
        VisualElement readouts = Root.Q<VisualElement>(className: "hud-readouts");
        for (int i = 0; i < expected.Length; i++)
        {
            Label value = readouts.Q<VisualElement>("resource-" + i).Q<Label>(className: "resource-value");
            Check(value.text == expected[i], "HUD resource-" + i + " agrees with the current authoritative snapshot.");
        }
    }

    IEnumerator Capture(string name)
    {
        bool priorDrive = runtime.DriveFromUpdate;
        runtime.DriveFromUpdate = false;
        string screenPath = Path.Combine(I02PlayQa.Output, name + "-screen.png");
        // Truncate any old image so its existence cannot masquerade as a fresh capture.
        File.WriteAllBytes(screenPath, Array.Empty<byte>());
        int width = Screen.width, height = Screen.height;
        ScreenCapture.CaptureScreenshot(screenPath);
        double captureDeadline = Time.realtimeSinceStartupAsDouble + 2;
        while (new FileInfo(screenPath).Length == 0 && Time.realtimeSinceStartupAsDouble < captureDeadline) yield return null;
        if (new FileInfo(screenPath).Length > 0)
            CaptureRecord(name + "-screen.png", "ScreenCapture: actual GameView, scene camera, UGUI and UIDocument", width, height);
        else Record("ScreenCapture produced no image for " + name + " within 2 s; real-camera/UIDocument target follows.");

        // Existing real camera + existing UIDocument. No duplicate camera or fabricated background.
        var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
        target.name = "I02 QA actual scene capture";
        target.Create();
        RenderTexture oldCameraTarget = sceneCamera.targetTexture;
        PanelSettings panel = document.panelSettings;
        RenderTexture oldPanelTarget = panel.targetTexture;
        bool oldClear = panel.clearColor;
        float oldAspect = sceneCamera.aspect;
        try
        {
            sceneCamera.targetTexture = target;
            sceneCamera.aspect = 1280f / 720f;
            panel.clearColor = false;
            panel.targetTexture = target;
            for (int frame = 0; frame < 8; frame++) { Root.MarkDirtyRepaint(); yield return null; }
            Check(sceneCamera.targetTexture == target && panel.targetTexture == target,
                "Capture " + name + " uses the existing actual camera and actual UIDocument at 1280x720.");
            RenderTexture previous = RenderTexture.active;
            var shot = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = target;
                shot.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                shot.Apply();
                File.WriteAllBytes(Path.Combine(I02PlayQa.Output, name + "-1280.png"), shot.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; Object.Destroy(shot); }
            CaptureRecord(name + "-1280.png", "Existing scene camera + actual UIDocument shared target; excludes UGUI (outside impact only)", 1280, 720);
            File.WriteAllText(Path.Combine(I02PlayQa.Output, name + "-labels.txt"), string.Join("\n", Root.Query<Label>().ToList().Select(label => label.text)));
            File.WriteAllText(Path.Combine(I02PlayQa.Output, name + "-state.txt"),
                "phase=" + runtime.GameState.Phase + "\nelapsedSimulationSeconds=" + runtime.Clock.ElapsedSimulationSeconds.ToString("R", CultureInfo.InvariantCulture)
                + "\ncameraPosition=" + sceneCamera.transform.position + "\ncameraRotation=" + sceneCamera.transform.rotation
                + "\nscene=" + SceneManager.GetActiveScene().path
                + "\nsuitOn=" + runtime.EvaStatus.IsSuitOn + "\nhandsBound=" + hands.IsBound + "\nhandsVisible=" + HandsRendered()
                + "\nmotionBound=" + motion.IsBound + "\nreducedMotion=" + motion.ReducedMotion
                + "\nmotionLocalPosition=" + motionPivot.localPosition.ToString("F6")
                + "\nhandMountLocalPosition=" + hands.MotionMount.localPosition.ToString("F6")
                + "\nresolverUsesCinematicPivot=" + (new SerializedObject(resolver).FindProperty("viewOrigin").objectReferenceValue == cinematicPivot)
                + "\nQA teleport smoke, not a full player walkthrough.\n");
        }
        finally
        {
            sceneCamera.targetTexture = oldCameraTarget;
            sceneCamera.aspect = oldAspect;
            panel.targetTexture = oldPanelTarget;
            panel.clearColor = oldClear;
            target.Release(); Object.Destroy(target);
            runtime.DriveFromUpdate = priorDrive;
        }
        yield return Frames(2);
    }
    void CaptureRecord(string file, string method, int width, int height)
    {
        File.AppendAllText(Path.Combine(I02PlayQa.Output, "capture-manifest.txt"),
            file + " | " + width + "x" + height + " | " + method + " | " + runtime.GameState.Phase + "\n");
    }
    IEnumerator KeyPress(Key key)
    {
        QueueKeys(); yield return Frames(2);
        QueueKeys(key); yield return Frames(2);
        QueueKeys(); yield return Frames(2);
    }
    void QueueKeys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
    void CleanupInput()
    {
        UnobserveSession();
        if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        keyboard = null;
        if (originalInputSettings) InputSystem.settings = originalInputSettings;
        if (qaInputSettings) Object.Destroy(qaInputSettings);
        Application.runInBackground = originalRunInBackground;
    }
    static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
    static IEnumerator Seconds(double duration)
    {
        double end = Time.realtimeSinceStartupAsDouble + duration;
        while (Time.realtimeSinceStartupAsDouble < end) yield return null;
    }
    IEnumerator WaitFor(Func<bool> predicate, double timeout, string description, bool heartbeat = false)
    {
        double end = Time.realtimeSinceStartupAsDouble + timeout;
        double nextLog = Time.realtimeSinceStartupAsDouble + 10;
        while (!predicate())
        {
            if (Time.realtimeSinceStartupAsDouble >= end) throw new TimeoutException(description);
            if (heartbeat && Time.realtimeSinceStartupAsDouble >= nextLog)
            {
                Debug.Log("I02_QA_WAIT " + description + " remaining=" + runtime.PlanningDeadline.RemainingRealSeconds.ToString("0.0", CultureInfo.InvariantCulture));
                nextLog += 10;
            }
            yield return null;
        }
    }
    Button ButtonStarting(string prefix) => Root.Query<Button>().ToList()
        .Single(button => button.text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && button.enabledInHierarchy
            && button.resolvedStyle.display != DisplayStyle.None);
    Button EquipmentButton(string id, string text) => Root.Q<VisualElement>(id).Query<Button>().ToList().Single(button => button.text == text);
    static void Submit(VisualElement element)
    {
        if (element == null) throw new InvalidOperationException("Expected real UI control was missing.");
        element.Focus();
        using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = element; element.SendEvent(evt); }
    }
    void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        checks.Add(message); Record("PASS " + message);
    }
    void Record(string message)
    {
        Debug.Log("I02_QA " + message);
        File.AppendAllText(Path.Combine(I02PlayQa.Output, "checks.txt"), message + "\n");
    }
    static T[] ActiveObjects<T>() where T : Behaviour => Object.FindObjectsByType<T>(FindObjectsSortMode.None).Where(item => item.isActiveAndEnabled).ToArray();
    static int Quantity(IMissionConfiguration config, string id) => config.Equipment.Where(item => item.EquipmentId == id).Sum(item => item.Quantity);
    static bool SameEquipment(IMissionConfiguration left, IMissionConfiguration right)
    {
        var ids = left.Equipment.Select(item => item.EquipmentId).Union(right.Equipment.Select(item => item.EquipmentId));
        return ids.All(id => Quantity(left, id) == Quantity(right, id));
    }
    static bool Near(double left, double right) => Math.Abs(left - right) < 1e-8;
    static double Value(ResourceSnapshot snapshot, ResourceId id)
    {
        switch (id)
        {
            case ResourceId.OxygenKg: return snapshot.OxygenKg;
            case ResourceId.WaterLitres: return snapshot.WaterLitres;
            case ResourceId.FoodKg: return snapshot.FoodKg;
            case ResourceId.BatteryKWh: return snapshot.BatteryKWh;
            case ResourceId.SparePartsCount: return snapshot.SparePartsCount;
            default: throw new ArgumentOutOfRangeException(nameof(id));
        }
    }
    static bool EqualResources(ResourceSnapshot left, ResourceSnapshot right) =>
        Enum.GetValues(typeof(ResourceId)).Cast<ResourceId>().All(id => Near(Value(left, id), Value(right, id)))
        && Near(left.CarbonDioxidePpm, right.CarbonDioxidePpm);
}
