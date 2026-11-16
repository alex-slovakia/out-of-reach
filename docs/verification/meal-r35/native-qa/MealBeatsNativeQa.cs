// R35 MEAL native QA (temporary editor tool, never part of the game; copy with its asmdef to Assets/_MealQa/Editor/,
// run, then delete Assets/_MealQa). Adapted from the MEAL-PRES-01 writer's MealNativeQa and the R03 WalkNativeQa.
// Unity.exe -batchmode -projectPath <own clone> -executeMethod MealBeatsNativeQa.Run   (GPU, no -nographics)
// Opens the actual saved Bootstrap and chooses Practice through the real UI. EXPLICIT SHORTCUTS, all logged: a teleport
// to the cockpit console, the real E there, hold-Space skip of the departure, domain commands to the chapter where the
// ration is allowed, a teleport to the galley aisle, simulated waits until a ration is due. The meal itself starts only
// through a real E press on the real survival-ration target. Meals 1 and 3 are stepped with a scripted presentation
// clock (MealPresenter.Advance) so every frame is taken at an exact meal time; meal 2 runs in real time.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using OutOfReach.Composition;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Core.Runtime;
using OutOfReach.Interaction;
using OutOfReach.Player;
using OutOfReach.Presentation.Cinematics;
using OutOfReach.Presentation.Meal;
using OutOfReach.Presentation.Motion;
using OutOfReach.Presentation.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class MealBeatsNativeQa
{
    const string Key = "OutOfReach.MealBeatsNativeQa.";
    public const string Bootstrap = "Assets/_OutOfReach/Scenes/Bootstrap.unity";
    public static string Output => SessionState.GetString(Key + "output", "");
    static string Stage => SessionState.GetString(Key + "stage", "");
    /// <summary>Set by the optional "after" part: subscribes a logger to the reworked presenter's beat events.</summary>
    public static Action<MealPresenter, Action<string>> HookBeats;
    static MealBeatsNativeQa()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
        EditorApplication.update += Watchdog;
        Application.logMessageReceived += OnLog;
    }
    public static void Run()
    {
        try
        {
            if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Batch edit mode only.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new InvalidOperationException("A real GPU is required (no -nographics).");
            string root = Environment.GetEnvironmentVariable("MEAL_QA_OUT");
            if (string.IsNullOrEmpty(root)) throw new InvalidOperationException("Set MEAL_QA_OUT.");
            SessionState.SetString(Key + "output", root.Replace('\\', '/'));
            Directory.CreateDirectory(Output);
            SessionState.SetString(Key + "errors", "");
            SessionState.SetString(Key + "deadline", DateTime.UtcNow.AddSeconds(600).ToString("O", CultureInfo.InvariantCulture));
            SessionState.SetBool(Key + "async", ShaderUtil.allowAsyncCompilation); ShaderUtil.allowAsyncCompilation = false;
            Note("GPU " + SystemInfo.graphicsDeviceName + " | " + SystemInfo.graphicsDeviceType);
            SetStage("opening actual saved Bootstrap");
            EditorSceneManager.OpenScene(Bootstrap, OpenSceneMode.Single);
            Check(Object.FindFirstObjectByType<MealPresenter>(), "The saved scene has the installed meal presenter.");
            SetStage("entering"); EditorApplication.isPlaying = true;
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }
    static void OnPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || Stage != "entering") return;
        try { SetStage("play"); new GameObject("MEAL_NATIVE_QA_UNSAVED").AddComponent<MealBeatsNativeQaDriver>(); }
        catch (Exception error) { Finish(false, error.ToString()); }
    }
    static void Watchdog()
    {
        if (Stage.Length == 0) return;
        if (DateTime.TryParse(SessionState.GetString(Key + "deadline", ""), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime deadline)
            && DateTime.UtcNow > deadline) Finish(false, "600-second watchdog expired at stage " + Stage);
    }
    static void OnLog(string message, string stack, LogType type)
    {
        if (Stage.Length == 0 || (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)) return;
        string entry = type + ": " + message + "\n" + stack + "\n";
        // Every error is recorded; only errors of the meal itself stop the run (other systems are not under test here).
        File.AppendAllText(Path.Combine(Output, "runtime-errors.txt"), entry);
        if (stack.Contains("Presentation.Meal") || stack.Contains("MealBeatsNativeQa"))
            SessionState.SetString(Key + "errors", SessionState.GetString(Key + "errors", "") + entry);
    }
    public static void RequireNoErrors() { string e = SessionState.GetString(Key + "errors", ""); if (e.Length > 0) throw new InvalidOperationException(e); }
    public static void SetStage(string value) => SessionState.SetString(Key + "stage", value);
    public static void Check(bool ok, string message)
    {
        File.AppendAllText(Path.Combine(Output, "checks.txt"), (ok ? "PASS " : "FAIL ") + Stage + " | " + message + "\n");
        if (!ok) throw new InvalidOperationException(message);
    }
    public static bool Soft(bool ok, string message)
    {
        File.AppendAllText(Path.Combine(Output, "checks.txt"), (ok ? "PASS " : "SOFT-FAIL ") + Stage + " | " + message + "\n");
        return ok;
    }
    public static void Note(string message) => File.AppendAllText(Path.Combine(Output, "observations.txt"), message + "\n");
    public static void Finish(bool pass, string message)
    {
        try { RequireNoErrors(); } catch (Exception error) { pass = false; message += "\n" + error; }
        SessionState.SetString(Key + "stage", ""); ShaderUtil.allowAsyncCompilation = SessionState.GetBool(Key + "async", true);
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, pass ? "PASS.txt" : "FAIL.txt"), message);
        Debug.Log((pass ? "MEAL_BEATS_QA_PASS " : "MEAL_BEATS_QA_FAIL ") + message + " output=" + Output);
        EditorApplication.Exit(pass ? 0 : 1);
    }
}

[DefaultExecutionOrder(600)]
public sealed class MealBeatsNativeQaDriver : MonoBehaviour
{
    I01MissionComposition composition;
    MissionRuntime runtime;
    FirstPersonController player;
    InteractionResolver resolver;
    I06MarineBindings marine;
    MissionCinematicPresenter cinematics;
    MealPresenter meal;
    EmbodiedBodyPresenter body;
    Camera view;
    UIDocument document;
    Keyboard keyboard;
    Mouse mouse;
    InputSettings originalSettings, qaSettings;
    HideFlags originalFlags;
    bool previousBackground;
    readonly List<InputDevice> disabledDevices = new List<InputDevice>();
    int interactions, transferred;
    CommandResult lastInteraction;
    string stage = "startup";
    readonly List<string> beats = new List<string>();
    double mealStartedAt;
    MissionSession Session => runtime.Session;
    PlayerInputActions Input => player.InputSource as PlayerInputActions;
    static string Output => MealBeatsNativeQa.Output;
    void Check(bool ok, string message) => MealBeatsNativeQa.Check(ok, message);
    bool Soft(bool ok, string message) => MealBeatsNativeQa.Soft(ok, message);
    void Stage(string value) { stage = value; MealBeatsNativeQa.SetStage(value); Note("== " + value); }
    void Note(string text) => MealBeatsNativeQa.Note(Time.realtimeSinceStartupAsDouble.ToString("F3", CultureInfo.InvariantCulture) + " | " + text);
    static string F(double value, string format = "F2") => value.ToString(format, CultureInfo.InvariantCulture);

    IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        Application.targetFrameRate = 60;
        previousBackground = Application.runInBackground; Application.runInBackground = true;
        originalSettings = InputSystem.settings; originalFlags = originalSettings.hideFlags;
        if (originalFlags == HideFlags.HideAndDontSave) originalSettings.hideFlags = HideFlags.DontSave;
        qaSettings = Object.Instantiate(originalSettings); qaSettings.hideFlags = HideFlags.DontSave;
        qaSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        qaSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings = qaSettings;
        foreach (var device in InputSystem.devices.ToArray())
            if (device.enabled && (device is Mouse || device is Keyboard || device is Gamepad)) { disabledDevices.Add(device); InputSystem.DisableDevice(device); }
        keyboard = InputSystem.AddDevice<Keyboard>("Meal QA keyboard"); mouse = InputSystem.AddDevice<Mouse>("Meal QA mouse");
        var stack = new Stack<IEnumerator>(); stack.Push(Exercise()); Exception failed = null;
        while (stack.Count > 0)
        {
            bool more = false; object yielded = null;
            try { MealBeatsNativeQa.RequireNoErrors(); more = stack.Peek().MoveNext(); if (more) yielded = stack.Peek().Current; }
            catch (Exception error) { failed = error; }
            if (failed != null) break;
            if (!more) { stack.Pop(); continue; }
            if (yielded is IEnumerator nested) { stack.Push(nested); continue; }
            yield return yielded;
        }
        if (failed != null)
        {
            Keys();
            if (view) yield return Capture("FAIL-actual-state", true);
            Cleanup(); MealBeatsNativeQa.Finish(false, "Stage=" + stage + "\n" + failed); yield break;
        }
        Cleanup(); MealBeatsNativeQa.Finish(true, "R35 meal native run completed; see checks, observations and frames.");
    }

    IEnumerator Exercise()
    {
        yield return WaitFor(() => Object.FindFirstObjectByType<MissionRuntime>()?.Session != null, 30, "Actual saved session startup");
        composition = Object.FindFirstObjectByType<I01MissionComposition>(); runtime = composition.Runtime; player = runtime.Player;
        resolver = player.Interaction; marine = Object.FindFirstObjectByType<I06MarineBindings>();
        cinematics = Object.FindFirstObjectByType<MissionCinematicPresenter>(); meal = Object.FindFirstObjectByType<MealPresenter>();
        body = Object.FindFirstObjectByType<EmbodiedBodyPresenter>(FindObjectsInactive.Include);
        view = Camera.main; document = Object.FindFirstObjectByType<MissionFlightBoard>().GetComponent<UIDocument>();
        Check(marine && cinematics && meal && body && view && Input != null, "Saved marine, cinematics, meal, body, camera and default input exist.");
        Check(composition.Meal == meal, "The composition's meal field points at the installed presenter.");
        player.SendMessage("OnApplicationFocus", true, SendMessageOptions.RequireReceiver);
        meal.SendMessage("OnApplicationFocus", true, SendMessageOptions.DontRequireReceiver);
        resolver.Interacted += OnInteract; marine.Transferred += OnTransferred;
        meal.StatusChanged += line => Note("MEAL STATUS " + line);
        MealBeatsNativeQa.HookBeats?.Invoke(meal, text =>
        {
            string line = F(Time.realtimeSinceStartupAsDouble - mealStartedAt) + " s (meal " + F(meal.Sequence.ElapsedActiveSeconds) + ") " + text;
            beats.Add(line); Note("BEAT " + line);
        });

        Stage("practice");
        double practiceEnd = Now + 60;
        while (!(composition.ModeChosen && player.ControlAllowed))
        {
            if (Now > practiceEnd) throw new TimeoutException("Practice UI never released the controls.");
            SkipStartupCards();
            var button = PracticeButton();
            if (button != null) { Submit(button); yield return Frames(4); }
            yield return Seconds(.25);
        }
        Check(composition.ModeChosen && player.ControlAllowed, "Actual Practice choice releases ordinary deck controls.");

        Stage("SHORTCUT teleport to the cockpit console, real E");
        InteractableTarget console = Target(InteractionIds.CockpitConsole);
        Vector3 stand = marine.MarineRoot.TransformPoint(new Vector3(0, .035f, .7f));
        Vector3 toward = console.transform.position - stand; toward.y = 0f;
        player.Teleport(stand, Mathf.Atan2(toward.x, toward.z) * Mathf.Rad2Deg); Physics.SyncTransforms();
        yield return Seconds(.5);
        yield return Aim(console.transform.position); yield return Frames(2); resolver.Refresh();
        Note("console ray: " + (resolver.CurrentTarget ? resolver.CurrentTarget.InteractionId : "none") + " prompt='" + resolver.CurrentPrompt + "' can=" + resolver.CurrentCanInteract);
        if (resolver.CurrentTarget == console)
        {
            int before = interactions; Keys(Key.E); yield return Frames(2); Keys(); yield return Frames(2);
            Note("console E: interactions " + (interactions - before) + " ok=" + lastInteraction.Succeeded + " " + lastInteraction.Explanation);
        }
        if (Session.State.Phase == GamePhase.Planning)
        {
            CommandResult launch = console.Interactable.TryInteract();
            Note("SHORTCUT direct console command: " + launch.Succeeded + " " + launch.Explanation);
            Check(launch.Succeeded, "The launch command succeeds: " + launch.Explanation);
        }
        yield return Frames(5);

        Stage("departure (hold Space to skip)");
        double departureEnd = Now + 150;
        while (!(transferred >= 1 && !marine.OwnsPresentation && player.ControlAllowed))
        {
            if (Now > departureEnd) throw new TimeoutException("Departure/transfer did not complete: transferred=" + transferred + " owns=" + marine.OwnsPresentation
                + " control=" + player.ControlAllowed + " phase=" + Session.State.Phase + " cine=" + cinematics.IsPlaying);
            Keys(); yield return Seconds(.25);
            Keys(Key.Space); yield return Seconds(.9);
            if (Session.Cinematics.IsWaiting && cinematics.IsPlaying) cinematics.SkipActive();
        }
        Keys(); yield return Frames(5);
        Note("Arrival phase " + Session.State.Phase + "; floating " + player.IsFloating + " survival active " + Session.Survival.IsActive);

        Stage("SHORTCUT domain commands until the ration is allowed");
        yield return ShortcutToSurvival();
        Check(Session.Survival.IsActive, "Survival is active (phase " + Session.State.Phase + ").");

        Stage("SHORTCUT teleport to the galley aisle");
        Transform drawer = FindNamed("RationInteractionSurface");
        Check(drawer, "The actual habitat ration drawer surface exists.");
        yield return StandAtGalley(drawer);
        Check(meal.IsBound && meal.RationTarget && meal.RationTarget.Interactable is MealSequence, "The real survival-ration target is decorated by the meal.");
        yield return Capture("00-galley-before-meal", true);
        meal.Cancelled += (reason, eaten) => { cancelled = reason + " (eaten " + eaten + ")"; Note("MEAL CANCELLED " + cancelled); };

        // Meal 1 as the player meets it: the first packaged meal after the impact, in the emergency light.
        yield return SteppedMeal(1, drawer);

        Stage("SHORTCUT domain repairs (cabin light back on)");
        MissionSession s = Session;
        if (!s.Systems.Power.IsOperational) Try(s.Systems.Power.TryRepair(s.Resources), "power repair");
        if (!s.Systems.LifeSupport.IsOperational) Try(s.Systems.LifeSupport.TryRepair(s.Resources), "life-support repair");
        yield return Settle();
        yield return Seconds(1.5);
        yield return StandAtGalley(drawer);
        yield return Capture("00-galley-after-repairs", true);

        yield return RealTimeMeal(2, drawer, false);
        yield return SteppedMeal(3, drawer);
        // Meal 4 (the repeated menu with its label look) with the game's own Reduced effects setting switched on.
        yield return RealTimeMeal(4, drawer, true);
    }
    string cancelled;

    static readonly (MealStage stage, double p)[] Shots =
    {
        (MealStage.Take, .30), (MealStage.Take, .50), (MealStage.Take, .80),
        (MealStage.Prepare, .50), (MealStage.Prepare, .80), (MealStage.Prepare, .97),
        (MealStage.Open, .08), (MealStage.Open, .20), (MealStage.Open, .30), (MealStage.Open, .40), (MealStage.Open, .48),
        (MealStage.Open, .56), (MealStage.Open, .64), (MealStage.Open, .72), (MealStage.Open, .82), (MealStage.Open, .94),
        (MealStage.Eat, .04), (MealStage.Eat, .10), (MealStage.Eat, .16), (MealStage.Eat, .22), (MealStage.Eat, .28), (MealStage.Eat, .34),
        (MealStage.Eat, .40), (MealStage.Eat, .46), (MealStage.Eat, .52), (MealStage.Eat, .58), (MealStage.Eat, .64), (MealStage.Eat, .70),
        (MealStage.Eat, .76), (MealStage.Eat, .82), (MealStage.Eat, .90), (MealStage.Eat, .97),
        (MealStage.Stow, .25), (MealStage.Stow, .55), (MealStage.Stow, .80),
    };

    /// <summary>One meal with a scripted presentation clock: every frame is taken at an exact meal time.</summary>
    IEnumerator SteppedMeal(int index, Transform drawer)
    {
        Stage("meal " + index + " stepped");
        yield return DueRation();
        yield return Aim(drawer.position); yield return Frames(2);
        double food = Session.Resources.Snapshot.FoodKg, ration = Session.Survival.RationFoodKg;
        int commands = meal.Sequence.CommandsIssued;
        meal.DriveFromUpdate = false;
        yield return PressE("meal " + index);
        Check(meal.IsEating, "E starts meal " + index + " as " + meal.Sequence.Preparation);
        MealPreparation kind = meal.Sequence.Preparation;
        Check(!player.ControlAllowed, "The composition's movement lock holds during the meal.");
        MealTimeline line = meal.Sequence.Timeline;
        Note("meal " + index + " " + kind + " timeline take " + F(line.Take) + " prepare " + F(line.Prepare) + " open " + F(line.Open) + " eat " + F(line.Eat) + " stow " + F(line.Stow)
            + " commit " + F(line.CommitAt) + " total " + F(line.Total) + " labelCheck " + meal.Sequence.IsLabelCheck);
        string p = index.ToString("00");
        int shot = 0;
        foreach (var (shotStage, progress) in Shots)
        {
            double at = line.StageStart(shotStage) + progress * line.StageLength(shotStage);
            while (meal.IsEating && meal.Sequence.ElapsedActiveSeconds < at - 1e-6)
            {
                meal.Advance(Math.Min(1d / 60d, at - meal.Sequence.ElapsedActiveSeconds));
                yield return null;
            }
            if (!meal.IsEating)
            {
                // The game itself may end a meal (a chapter event, a hazard): recorded, not a failure of the capture.
                Soft(false, "meal " + index + " was ended by the game before " + shotStage + " " + F(progress) + ": " + (cancelled ?? "no reason reported"));
                meal.DriveFromUpdate = true;
                yield return WaitFor(() => player.ControlAllowed, 20, "control returns after the interrupted meal");
                yield break;
            }
            yield return Frames(2);
            string name = p + "-" + (shot++).ToString("00") + "-" + shotStage.ToString().ToLowerInvariant() + "-" + ((int)Math.Round(progress * 100)).ToString("00") + "-" + kind;
            Note(name + ": t=" + F(meal.Sequence.ElapsedActiveSeconds) + " stage=" + meal.Stage + " pouch=" + (meal.Pouch ? meal.Pouch.position.ToString("F3") : "none")
                + " visible=" + meal.PouchVisible + " opened=" + meal.PouchOpened + " hand=" + meal.IsHandHolding
                + " reachShort=" + F(meal.HandReachShortfallMetres * 1000f, "F0") + "mm"
                + " eye-pouch=" + (meal.Pouch ? F(Vector3.Distance(view.transform.position, meal.Pouch.position)) : "-")
                + " camPitch=" + F(Mathf.DeltaAngle(0f, view.transform.eulerAngles.x)) + Extra());
            if (shotStage == MealStage.Eat && progress <= .05)
                Check(Math.Abs((food - Session.Resources.Snapshot.FoodKg) - ration) < 1e-9, "The first bite debits exactly one ration (" + F(ration, "F4") + " kg).");
            if (shotStage == MealStage.Open && progress >= .9)
                Check(Math.Abs(Session.Resources.Snapshot.FoodKg - food) < 1e-12, "Taking, preparing and opening grant nothing.");
            yield return Capture(name, true);
            yield return Capture(name + "-clean", false);
        }
        while (meal.IsEating) { meal.Advance(1d / 60d); yield return null; }
        yield return Frames(3);
        meal.DriveFromUpdate = true;
        Check(meal.Sequence.CommandsIssued == commands + 1, "One real command for meal " + index + ".");
        Check(!meal.PouchVisible && player.ControlAllowed, "After meal " + index + " the pouch is stowed and control returns.");
        Check(Math.Abs((food - Session.Resources.Snapshot.FoodKg) - ration) < 1e-9, "Exactly one ration debited by meal " + index + ".");
        yield return Capture(p + "-99-after-" + kind, true);
    }

    /// <summary>One meal driven by the game's own real-time update: stage times and (after the rework) the beat times.</summary>
    IEnumerator RealTimeMeal(int index, Transform drawer, bool reducedMotion)
    {
        Stage("meal " + index + " real time" + (reducedMotion ? ", reduced effects" : ""));
        if (reducedMotion)
        {
            // The game's own setting path (the menu's Reduced effects toggle calls the same method).
            composition.SetReducedEffects(true);
            Note("Reduced effects ON through I01MissionComposition.SetReducedEffects" + Extra());
        }
        yield return DueRation();
        yield return Aim(drawer.position); yield return Frames(2);
        double food = Session.Resources.Snapshot.FoodKg, ration = Session.Survival.RationFoodKg;
        int commands = meal.Sequence.CommandsIssued;
        beats.Clear();
        Quaternion cam0 = view.transform.rotation;
        mealStartedAt = Now;
        yield return PressE("meal " + index);
        Check(meal.IsEating, "E starts meal " + index + " as " + meal.Sequence.Preparation);
        MealTimeline line = meal.Sequence.Timeline;
        float worstRight = 0f, worstNod = 0f;
        while (meal.IsEating)
        {
            if (meal.IsHandHolding && !float.IsNaN(meal.HandReachShortfallMetres)) worstRight = Mathf.Max(worstRight, meal.HandReachShortfallMetres);
            worstNod = Mathf.Max(worstNod, Quaternion.Angle(cam0, view.transform.rotation));
            if (Now - mealStartedAt > line.Total + 8) throw new TimeoutException("meal " + index + " did not finish in real time");
            yield return null;
        }
        double took = Now - mealStartedAt;
        yield return Frames(3);
        Note("meal " + index + " " + meal.Sequence.Preparation + " real time: took " + F(took) + " s for a " + F(line.Total)
            + " s timeline; worst right reach shortfall " + F(worstRight * 1000f, "F0") + " mm; largest camera deviation " + F(worstNod) + " deg"
            + "; camera back at rest within " + F(Quaternion.Angle(cam0, view.transform.rotation), "F3") + " deg");
        Note("meal " + index + " beats: " + (beats.Count == 0 ? "(none reported)" : string.Join(" | ", beats)));
        Check(meal.Sequence.CommandsIssued == commands + 1, "One real command for meal " + index + ".");
        Check(Math.Abs((food - Session.Resources.Snapshot.FoodKg) - ration) < 1e-9, "Exactly one ration debited by meal " + index + ".");
        Check(!meal.PouchVisible && player.ControlAllowed, "After meal " + index + " the pouch is stowed and control returns.");
        Soft(Quaternion.Angle(cam0, view.transform.rotation) < .01f, "The camera is back at its rest pose after meal " + index + ".");
        if (reducedMotion)
        {
            Soft(worstNod < .01f, "Reduced effects: the view never moved during meal " + index + " (" + F(worstNod, "F3") + " deg).");
            composition.SetReducedEffects(false);
        }
        else if (MealBeatsNativeQa.HookBeats != null)
            Soft(worstNod > 1f && worstNod < 3f, "The head nod of meal " + index + " is small (" + F(worstNod) + " deg).");
    }

    /// <summary>Optional diagnostics of the reworked presenter, read by name so the same tool runs on the base.</summary>
    string Extra()
    {
        string text = "";
        foreach (string name in new[] { "TearProgress", "StripVisible", "IsOffHandHolding", "OffHandReachShortfallMetres", "OffHandPinchGapMetres", "StripPinchErrorMetres", "Squeeze", "NodDegrees", "BeatsPlayed", "ReducedMotion" })
        {
            PropertyInfo property = typeof(MealPresenter).GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property == null) continue;
            object value = property.GetValue(meal);
            text += " " + name + "=" + (value is float f ? F(f, "F3") : Convert.ToString(value, CultureInfo.InvariantCulture));
        }
        return text;
    }

    IEnumerator ShortcutToSurvival()
    {
        MissionSession s = Session;
        Note("SHORTCUT start phase=" + s.State.Phase + " survival=" + s.Survival.IsActive);
        yield return Settle();
        if (s.Survival.IsActive) yield break;
        if (s.State.Phase == GamePhase.Intro) Try(s.Director.TryCompleteCockpitCheck(), "impact");
        yield return Settle();
        if (s.Survival.IsActive) yield break;
        if (!s.Systems.Power.IsOperational) Try(s.Systems.Power.TryRepair(s.Resources), "power repair");
        if (!s.Systems.LifeSupport.IsOperational) Try(s.Systems.LifeSupport.TryRepair(s.Resources), "life-support repair");
        yield return Settle();
        if (s.Survival.IsActive) yield break;
        Try(s.Eva.TryPrepareSuit(), "suit");
        Try(s.Eva.TryBeginEva(), "begin EVA");
        Try(s.AdvanceSimulation(s.Eva.Spec.DepressurizationSeconds), "depressurize");
        Try(s.Eva.TryGoOutside(), "go outside");
        Try(s.Systems.Antenna.TryRepair(s.Resources), "antenna repair");
        Try(s.Eva.TryReturnInside(), "return inside");
        Try(s.Eva.TryEndEva(), "end EVA");
        Try(s.AdvanceSimulation(s.Eva.Spec.RepressurizationSeconds), "repressurize");
        yield return Settle();
        if (s.Survival.IsActive) yield break;
        Try(s.Director.TryRunLinkDiagnostic(), "link diagnostic");
        yield return Settle();
        Try(s.Communications.TryTransmit(), "transmit");
        Try(s.AdvanceSimulation(s.Communications.ReplyDelaySimulationSeconds), "reply");
        yield return Settle();
        Note("SHORTCUT end phase=" + s.State.Phase + " suit=" + runtime.EvaStatus.IsSuitOn + " outside=" + runtime.EvaStatus.IsOutside + " survival=" + s.Survival.IsActive);
    }

    IEnumerator Settle()
    {
        double end = Now + 25;
        while (Now < end)
        {
            MissionSession s = Session;
            if (s.Cinematics.IsWaiting && cinematics.IsPlaying) cinematics.SkipActive();
            else if (s.Cinematics.IsWaiting) s.Events.Publish(new CinematicFinished(s.Cinematics.ActiveCinematic, s.Cinematics.ActiveSequenceId, false));
            if (!s.Cinematics.IsWaiting && !cinematics.IsPlaying) { yield return Frames(3); if (!s.Cinematics.IsWaiting && !cinematics.IsPlaying) yield break; }
            yield return null;
        }
        Note("Settle timed out: waiting=" + Session.Cinematics.IsWaiting + " playing=" + cinematics.IsPlaying);
    }

    IEnumerator DueRation()
    {
        MissionSession s = Session;
        for (int i = 0; i < 12 && !s.Survival.CheckConsumeRation().Succeeded; i++)
        {
            if (s.Survival.CheckDrinkWater().Succeeded) Try(s.Survival.TryDrinkWater(), "SHORTCUT domain water portion (thirst would otherwise end the run)");
            Try(s.AdvanceSimulation(s.Survival.Spec.PortionSeconds * .35d), "SHORTCUT simulated wait until a ration is due");
            yield return Settle();
        }
        if (s.Survival.CheckDrinkWater().Succeeded) Try(s.Survival.TryDrinkWater(), "SHORTCUT domain water portion");
        yield return Frames(4);
        Check(s.Survival.CheckConsumeRation().Succeeded, "A ration is due: " + s.Survival.CheckConsumeRation().Explanation);
        yield return WaitFor(() => player.ControlAllowed, 20, "control returns before the meal");
    }

    IEnumerator StandAtGalley(Transform drawer)
    {
        Vector3 aisle = drawer.position + Flat(drawer.forward) * .56f;
        Check(Physics.Raycast(aisle, Vector3.down, out RaycastHit floor, 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), "Deck below the galley aisle.");
        Vector3 feet = floor.point + Vector3.up * .02f;
        Vector3 toward = drawer.position - feet; toward.y = 0f;
        player.Teleport(feet, Mathf.Atan2(toward.x, toward.z) * Mathf.Rad2Deg);
        Physics.SyncTransforms();
        yield return Seconds(.6);
        yield return Aim(drawer.position);
        yield return Seconds(.3);
        Note("At galley: feet=" + player.transform.position.ToString("F2") + " eye=" + view.transform.position.ToString("F2") + " floating=" + player.IsFloating
            + " body visible=" + body.IsVisible + " eva=" + body.IsEvaVisible + " arms=" + body.VisibleArms + " pose=" + body.CurrentPose);
    }

    IEnumerator PressE(string what)
    {
        resolver.Refresh();
        Note("Prompt before E: '" + resolver.CurrentPrompt + "' can=" + resolver.CurrentCanInteract + " target=" + (resolver.CurrentTarget ? resolver.CurrentTarget.name : "none"));
        Check(resolver.CurrentTarget == meal.RationTarget, what + ": the actual interaction ray resolves the ration drawer (" + (resolver.CurrentTarget ? resolver.CurrentTarget.name : "none") + ")");
        int before = interactions;
        Keys(Key.E); yield return Frames(2); Keys(); yield return Frames(2);
        Check(interactions == before + 1 && lastInteraction.Succeeded, what + ": single actual E starts the meal | " + lastInteraction.Explanation);
    }

    void SkipStartupCards()
    {
        foreach (UIDocument doc in Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
        {
            VisualElement root = doc.rootVisualElement?.panel?.visualTree;
            if (root == null) continue;
            foreach (VisualElement element in root.Query<VisualElement>().ToList())
            {
                if (element.GetType().Name != "StartupCardPresenter") continue;
                element.GetType().GetMethod("RequestSkip")?.Invoke(element, null);
            }
        }
    }

    void Try(CommandResult result, string step) { Note("domain " + step + ": " + result.Succeeded + " " + result.Explanation); }
    InteractableTarget Target(string id) => runtime.InteractionTargets.Single(t => t.InteractionId == id);
    static Transform FindNamed(string name) => Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).SingleOrDefault(t => t.name == name);
    UnityEngine.UIElements.Button PracticeButton() => document.rootVisualElement.Query<UnityEngine.UIElements.Button>().ToList()
        .FirstOrDefault(b => b.text.IndexOf("Practice", StringComparison.OrdinalIgnoreCase) >= 0 && b.enabledInHierarchy && Visible(b));
    static void Submit(VisualElement e) { e.Focus(); using (var action = NavigationSubmitEvent.GetPooled()) { action.target = e; e.SendEvent(action); } }
    static bool Visible(VisualElement e) { for (var n = e; n != null; n = n.parent) if (n.resolvedStyle.display == DisplayStyle.None || n.resolvedStyle.visibility != Visibility.Visible) return false; return e?.panel != null; }
    void Keys(params Key[] keys) { if (keyboard != null && keyboard.added) InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); }
    static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward; }
    void OnInteract(CommandResult result) { interactions++; lastInteraction = result; Note("INTERACTION " + result.Succeeded + " " + result.Explanation); }
    void OnTransferred() { transferred++; Note("ACTUAL MARINE TRANSFER"); }
    void Cleanup()
    {
        Keys();
        if (resolver) resolver.Interacted -= OnInteract;
        if (marine) marine.Transferred -= OnTransferred;
        if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard); if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
        foreach (var device in disabledDevices) if (device.added) InputSystem.EnableDevice(device);
        if (originalSettings) { InputSystem.settings = originalSettings; originalSettings.hideFlags = originalFlags; }
        if (qaSettings) Object.Destroy(qaSettings);
        Application.runInBackground = previousBackground;
    }
    IEnumerator Aim(Vector3 worldPoint)
    {
        double end = Now + 2.5;
        while (true)
        {
            Vector3 delta = worldPoint - player.ViewPivot.position; if (delta.sqrMagnitude < .000001f) yield break;
            float yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg, pitch = Mathf.Atan2(delta.y, new Vector2(delta.x, delta.z).magnitude) * Mathf.Rad2Deg;
            Vector2 degrees = new Vector2(Mathf.DeltaAngle(player.transform.eulerAngles.y, yaw), Mathf.Clamp(pitch, -80f, 80f) - player.PitchDegrees);
            if (degrees.sqrMagnitude < .04f) break; if (Now > end) { Note("Aim did not converge; remaining=" + degrees); break; }
            degrees = Vector2.ClampMagnitude(degrees, 70); InputSystem.QueueStateEvent(mouse, new MouseState { delta = degrees / Input.MouseDegreesPerPixel }); yield return null;
        }
        InputSystem.QueueStateEvent(mouse, new MouseState()); yield return Frames(1);
    }
    static double Now => Time.realtimeSinceStartupAsDouble;
    static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
    static IEnumerator Seconds(double seconds) { double end = Now + seconds; while (Now < end) yield return null; }
    static IEnumerator WaitFor(Func<bool> ready, double seconds, string why) { double end = Now + seconds; while (!ready()) { if (Now > end) throw new TimeoutException(why); yield return null; } }
    IEnumerator Capture(string name, bool withUi)
    {
        var texture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32); texture.Create(); var old = view.targetTexture; float aspect = view.aspect;
        var panels = withUi ? Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None).Where(d => d.isActiveAndEnabled && d.panelSettings && d.rootVisualElement?.panel != null).Select(d => d.panelSettings).Distinct().ToArray() : new PanelSettings[0];
        var targets = panels.Select(p => p.targetTexture).ToArray(); var clears = panels.Select(p => p.clearColor).ToArray();
        try
        {
            view.targetTexture = texture; view.aspect = 16f / 9; foreach (var panel in panels) { panel.targetTexture = texture; panel.clearColor = false; }
            yield return Frames(2); var active = RenderTexture.active; var image = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
            try { RenderTexture.active = texture; image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply(); File.WriteAllBytes(Path.Combine(Output, name + ".png"), image.EncodeToPNG()); }
            finally { RenderTexture.active = active; Object.Destroy(image); }
        }
        finally { view.targetTexture = old; view.aspect = aspect; for (int i = 0; i < panels.Length; i++) if (panels[i]) { panels[i].targetTexture = targets[i]; panels[i].clearColor = clears[i]; } texture.Release(); Object.Destroy(texture); }
    }
}
