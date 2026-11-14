using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using OutOfReach.Composition;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Core.Runtime;
using OutOfReach.Player;
using OutOfReach.Presentation.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using Cursor = UnityEngine.Cursor;

// Temporary saved-scene acceptance runner. No test/domain substitution and no scene writes.
[InitializeOnLoad]
public static class I06PresentationQa
{
    [Serializable] sealed class RunResult { public bool pass; public string message, utc; }
    const string Flag = "OutOfReach.I06PresentationQa", OutputKey = Flag + ".Output", Deadline = Flag + ".Deadline", Errors = Flag + ".Errors";
    public const string ScenePath = "Assets/_OutOfReach/Scenes/Bootstrap.unity";
    public static string Output => SessionState.GetString(OutputKey, Path.GetFullPath(".local/i06-presentation-qa"));
    static I06PresentationQa()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
        EditorApplication.update += Watchdog;
        Application.logMessageReceived += OnLog;
    }
    public static void Run()
    {
        try
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
            if (project.IndexOf("/.local/", StringComparison.OrdinalIgnoreCase) < 0) throw new InvalidOperationException("Run only in the isolated .local project.");
            if (Application.unityVersion != "6000.3.28f1") throw new InvalidOperationException("Unity 6000.3.28f1 required.");
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start in Edit mode.");
            SessionState.SetString(OutputKey, Path.Combine(project, ".local/i06-presentation-qa", DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ")));
            Directory.CreateDirectory(Output);
            SessionState.SetString(Errors, "");
            SessionState.SetString(Deadline, DateTime.UtcNow.AddSeconds(150).ToString("O", CultureInfo.InvariantCulture));
            SessionState.SetString(Flag, "entering");
            File.WriteAllText(Path.Combine(Output, "scope.txt"),
                "Saved Bootstrap, actual bound services and UI. Runtime opt-in + fresh StartSession; scene is not modified.\n"
                + "Direct real launch/repair/EVA/transmission commands and AdvanceSimulation set up the chapter. No player movement/E-key playthrough claim.\n"
                + "Initial Impact/FalseSignal use the real presenter's SkipActive acknowledgement. Ending sequences run their authored duration.\n"
                + "DriveFromUpdate is off for deterministic setup and snapshots; enabled during the paused-rest freeze check. No state/phase/resource grants.\n"
                + "Recovery uses real bounded rests and beacon command; Lost uses actual unmet-water accounting; legacy uses a fresh opt-out session.\n"
                + "Buttons receive one real NavigationSubmit event. Hidden batch focus is established through the controller's normal focus-regain callback.\n"
                + "Captures: 1280x720 existing camera + actual UIDocument; excludes overlay UGUI. No final capsule/Mars art acceptance claim.\n"
                + "150-second wall watchdog. Existing full mission/movement smoke remains separate.\n");
            WriteManifest();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }
    static void OnPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || SessionState.GetString(Flag, "") != "entering") return;
        SessionState.SetString(Flag, "running");
        new GameObject("I06 presentation acceptance - QA only").AddComponent<I06PresentationQaDriver>();
    }
    static void Watchdog()
    {
        if (string.IsNullOrEmpty(SessionState.GetString(Flag, ""))) return;
        if (DateTime.TryParse(SessionState.GetString(Deadline, ""), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime deadline) && DateTime.UtcNow > deadline)
            Finish(false, "150-second watchdog expired. Last completed assertion is in checks.json/checks.txt.");
    }
    static void OnLog(string message, string stack, LogType type)
    {
        if (string.IsNullOrEmpty(SessionState.GetString(Flag, "")) || (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)) return;
        string entry = type + ": " + message + "\n" + stack + "\n";
        // Preserve the same narrowly identified Editor Search startup issue separately; never ignore game errors.
        if (type == LogType.Exception && message.StartsWith("ArgumentOutOfRangeException:", StringComparison.Ordinal)
            && stack.Contains("UnityEditor.Search.SearchDatabase") && stack.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup") && !stack.Contains("OutOfReach"))
        { File.AppendAllText(Path.Combine(Output, "editor-environment-errors.txt"), entry); return; }
        SessionState.SetString(Errors, SessionState.GetString(Errors, "") + entry);
        File.AppendAllText(Path.Combine(Output, "runtime-errors.txt"), entry);
    }
    public static void RequireNoErrors()
    {
        string errors = SessionState.GetString(Errors, "");
        if (!string.IsNullOrEmpty(errors)) throw new InvalidOperationException("Runtime errors:\n" + errors);
    }
    public static void Finish(bool pass, string message)
    {
        SessionState.SetString(Flag, "");
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, pass ? "PASS.txt" : "FAIL.txt"), message);
        File.WriteAllText(Path.Combine(Output, "result.json"), JsonUtility.ToJson(new RunResult { pass = pass, message = message, utc = DateTime.UtcNow.ToString("O") }, true));
        Debug.Log((pass ? "I06_PRESENTATION_QA_PASS " : "I06_PRESENTATION_QA_FAIL ") + message + " output=" + Output);
        EditorApplication.Exit(pass ? 0 : 1);
    }
    static void WriteManifest()
    {
        var paths = Directory.GetFiles("Assets/_OutOfReach", "*", SearchOption.AllDirectories)
            .Where(p => p.EndsWith(".cs") || p.EndsWith(".uss") || p.EndsWith(".asmdef"))
            .Concat(Directory.GetFiles("Assets/Editor", "I06PresentationQa.cs", SearchOption.AllDirectories))
            .Concat(new[] { ScenePath, "ProjectSettings/ProjectVersion.txt" }).OrderBy(p => p, StringComparer.Ordinal);
        using (var sha = SHA256.Create())
            File.WriteAllLines(Path.Combine(Output, "source-sha256.txt"), paths.Select(p =>
                BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(p))).Replace("-", "").ToLowerInvariant() + "  " + p.Replace('\\', '/')));
    }
}

[DefaultExecutionOrder(300)]
public sealed class I06PresentationQaDriver : MonoBehaviour
{
    [Serializable] sealed class Entry { public string utc, stage, message; public bool pass; public double simulationSeconds; }
    [Serializable] sealed class Results { public List<Entry> checks = new List<Entry>(); }
    readonly Results results = new Results();
    readonly List<CinematicFinished> endedSequences = new List<CinematicFinished>();
    MissionRuntime runtime;
    I01MissionComposition composition;
    SurvivalChapterPresenter survivalUi;
    FirstPersonController player;
    UIDocument document;
    Camera cameraView;
    bool originalRunInBackground;
    string stage = "saved scene";
    VisualElement Root => document.rootVisualElement;
    MissionSession Session => runtime.Session;

    IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        originalRunInBackground = Application.runInBackground;
        Application.runInBackground = true;
        var stack = new Stack<IEnumerator>(); stack.Push(Exercise());
        while (stack.Count != 0)
        {
            bool next = false; object current = null; Exception failure = null;
            try { I06PresentationQa.RequireNoErrors(); next = stack.Peek().MoveNext(); if (next) current = stack.Peek().Current; }
            catch (Exception error) { failure = error; }
            if (failure != null)
            {
                foreach (IEnumerator pending in stack) try { (pending as IDisposable)?.Dispose(); } catch { }
                AddResult(false, failure.ToString()); Cleanup();
                I06PresentationQa.Finish(false, "Stage: " + stage + "\n" + failure); yield break;
            }
            if (!next) { stack.Pop(); continue; }
            if (current is IEnumerator child) { stack.Push(child); continue; }
            yield return current;
        }
        Cleanup();
        I06PresentationQa.Finish(true, results.checks.Count + " assertions passed. Actual saved-scene presentation and real domain setup; see scope.txt for automation limits.");
    }

    IEnumerator Exercise()
    {
        yield return WaitFor(() => Object.FindFirstObjectByType<I01MissionComposition>()?.Runtime?.Session != null, 15, "Saved Bootstrap session");
        BindScene(); yield return Frames(6);
        Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path == I06PresentationQa.ScenePath, "Saved Bootstrap loaded, no generated replacement scene.");
        Check(survivalUi && survivalUi.isActiveAndEnabled, "Saved builder has an enabled SurvivalChapterPresenter.");
        Check(!Visible(Root.Q("survival-needs")) && !Visible(Root.Q("survival-rest")), "Planning/menu hides survival widgets.");
        yield return FreshSession(true);
        yield return ReachContact();
        Check(Session.State.Phase == GamePhase.Survival && Session.State.Outcome == MissionOutcome.None, "Restored contact enters Survival with no final outcome.");
        Check(Session.Report == null && !Session.Cinematics.IsWaiting && !composition.Cinematics.IsPlaying, "Contact has no final report or ending sequence.");
        Check(!Visible(Root.Q("flightRecorder")) && !Visible(Root.Q("mission-record-host")), "Contact does not open the report UI.");
        Check(runtime.Survival.Enabled && runtime.Survival.IsActive && runtime.SurvivalRecovery.Stage == RecoveryStage.Inbound, "Real opt-in services are active and recovery is inbound.");
        NeedBars();
        Check(!Visible(Root.Q("survival-recovery-detail")), "Recovery detail is absent from the default compact HUD.");
        yield return Submit(Root.Q<Button>(className: "systems-button"));
        Check(Visible(Root.Q("survival-recovery-detail")) && Root.Q<Label>(className: "survival-recovery-title").text == "Recovery craft inbound",
            "Actual Systems button exposes the real inbound recovery stage only on demand.");
        yield return Submit(Root.Q<Button>(className: "systems-button"));
        Check(!Visible(Root.Q("survival-recovery-detail")), "Closing Systems returns to the compact view.");
        Command(runtime.SkipSimulatedTime(3600), "Advance one real simulated hour through all services");
        yield return Frames(3); NeedBars();
        Check(runtime.Survival.Hunger01 > 0 && runtime.Survival.Thirst01 > 0, "Actual unmet needs increased, so bars are not constant placeholders.");
        yield return Capture("01-survival");
        yield return RestChecks();
        yield return LifecycleChecks();
        yield return RecoveryJourney();
        yield return ReportChecks(MissionOutcome.Recovered, "You made it.", "03-recovered");
        yield return RestartFromCard();

        stage = "legacy contact end card";
        yield return FreshSession(false);
        yield return ReachContact();
        Check(Session.State.Outcome == MissionOutcome.RecoveryPossible && runtime.SurvivalRecovery == null, "Fresh opt-out session preserves the legacy Contact restored outcome.");
        yield return AwaitReport();
        yield return ReportChecks(MissionOutcome.RecoveryPossible, "A voice from Earth.", "04-contact-restored-legacy");
        yield return RestartFromCard();

        stage = "real survival loss end card";
        yield return FreshSession(true);
        yield return ReachContact();
        Command(runtime.SkipSimulatedTime(Session.Survival.Spec.ThirstLimitSeconds + 1), "Advance awake without drinking, through the actual viability limit");
        Check(Session.State.Outcome == MissionOutcome.Lost && Session.Director.LossCause == OutOfReach.Simulation.SurvivalFailure.ThirstLimit,
            "Real unmet-water accounting produces Lost; no forced outcome or fake phase.");
        yield return AwaitReport();
        yield return ReportChecks(MissionOutcome.Lost, "Out of time.", "05-lost");
        yield return RestartFromCard();
        Check(!Visible(Root.Q("flightRecorder")) && !Visible(Root.Q("survival-needs")) && !Visible(Root.Q("survival-rest")), "Final restart leaves no stale report, needs or rest panel.");
    }

    void BindScene()
    {
        if (composition && composition.Cinematics) composition.Cinematics.SequenceEnded -= OnSequenceEnded;
        composition = Object.FindFirstObjectByType<I01MissionComposition>();
        composition.Cinematics.SequenceEnded += OnSequenceEnded;
        runtime = composition.Runtime; player = runtime.Player;
        document = Object.FindFirstObjectByType<MissionFlightBoard>().GetComponent<UIDocument>();
        survivalUi = Object.FindFirstObjectByType<SurvivalChapterPresenter>();
        cameraView = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Single(c => c.isActiveAndEnabled);
        player.SendMessage("OnApplicationFocus", true, SendMessageOptions.RequireReceiver);
        Note("Hidden batch focus setup: real controller focus-regain callback; no OS foreground/input play claim.");
    }
    IEnumerator FreshSession(bool survival)
    {
        stage = survival ? "opt-in session setup" : "opt-out session setup";
        runtime.DriveFromUpdate = false;
        var previous = Session;
        runtime.ConfigureSurvivalExtension(survival); runtime.StartSession();
        yield return Frames(5);
        endedSequences.Clear();
        Check(previous.IsDisposed && !ReferenceEquals(previous, Session), "StartSession disposes the old authority and binds a fresh one.");
        Check(Session.State.Phase == GamePhase.Planning && !composition.ModeChosen && !player.ControlAllowed, "New session starts behind the real mode-choice control gate.");
        Check(!Visible(Root.Q("survival-needs")) && !Visible(Root.Q("survival-rest")), "Fresh planning session contains no visible old survival state.");
        yield return Submit(Root.Query<Button>().ToList().Single(b => b.text.StartsWith("Practice", StringComparison.Ordinal) && Visible(b)));
        Check(composition.ModeChosen && !runtime.PlanningDeadline.IsEnabled && player.ControlAllowed, "Actual Practice button releases the composed gate without a deadline.");
    }
    IEnumerator ReachContact()
    {
        stage = "disclosed domain-command journey";
        Command(Session.Planner.TryLaunch(), "Launch actual valid planner loadout");
        Command(Session.Director.TryCompleteCockpitCheck(), "Actual cockpit check");
        SkipSetupCinematic(CinematicId.Impact);
        Command(Session.Systems.Power.TryRepair(Session.Resources), "Actual power repair costs");
        Command(Session.Systems.LifeSupport.TryRepair(Session.Resources), "Actual life-support repair costs");
        Command(Session.Eva.TryPrepareSuit(), "Actual suit preparation");
        Command(Session.Eva.TryBeginEva(), "Actual airlock cycle");
        Command(Session.AdvanceSimulation(Session.Eva.Spec.DepressurizationSeconds), "Real depressurization time and resource accounting");
        Command(Session.Eva.TryGoOutside(), "Actual exterior domain command (no movement claim)");
        Command(Session.Systems.Antenna.TryRepair(Session.Resources), "Actual antenna repair costs");
        Command(Session.Eva.TryReturnInside(), "Actual return-inside domain command");
        Command(Session.Eva.TryEndEva(), "Actual repressurization command");
        Command(Session.AdvanceSimulation(Session.Eva.Spec.RepressurizationSeconds), "Real repressurization time and resource accounting");
        Command(Session.Director.TryRunLinkDiagnostic(), "Actual communications diagnostic");
        SkipSetupCinematic(CinematicId.FalseSignal);
        Command(Session.Communications.TryTransmit(), "Actual distress transmission");
        Check(Session.Communications.State == CommunicationState.AwaitingReply, "Reply is pending after transmission, not instant.");
        Command(runtime.SkipSimulatedTime(Session.Communications.ReplyRemainingSimulationSeconds), "Advance the actual outbound/return reply interval");
        yield return Frames(5);
        Check(Session.Communications.State == CommunicationState.ReplyReceived, "Actual communication service reports the received reply.");
    }
    void SkipSetupCinematic(CinematicId expected)
    {
        Check(Session.Cinematics.IsWaiting && Session.Cinematics.ActiveCinematic == expected && composition.Cinematics.IsPlaying,
            "Real saved presentation receives " + expected + ".");
        Check(composition.Cinematics.SkipActive() && !Session.Cinematics.IsWaiting, "Disclosed setup skip acknowledges " + expected + " through its real presenter.");
    }
    void NeedBars()
    {
        var hunger = Root.Q<ProgressBar>("survival-hunger-bar"); var hydration = Root.Q<ProgressBar>("survival-hydration-bar");
        Check(Visible(hunger) && Visible(hydration), "Compact hunger/hydration bars are attached and visible in actual Survival.");
        Check(Math.Abs(hunger.value - runtime.Survival.Hunger01) < 0.00001 && Math.Abs(hydration.value - (1 - runtime.Survival.Thirst01)) < 0.00001,
            "Both displayed needs equal their authoritative services (hunger / remaining hydration).");
        Check(Root.Query<VisualElement>("survival-needs").ToList().Count == 1 && Root.Query<ProgressBar>("survival-hunger-bar").ToList().Count == 1,
            "One needs subtree and one hunger bar; no duplicate binding.");
    }
    IEnumerator RestChecks()
    {
        stage = "actual rest, pause and Wake";
        double awakeScale = Session.Clock.TimeScale;
        Command(runtime.SurvivalRecovery.TryBeginRest(2), "Begin a real two-hour rest");
        yield return Frames(4);
        Check(Session.SurvivalRecovery.IsResting && Visible(Root.Q("survival-rest")), "Rest UI appears only while the real service is resting.");
        Check(!player.ControlAllowed && player.ControlGate != null && !player.ControlGate() && !player.WantsCursorCaptured,
            "Composition blocks actual player controls/cursor capture during rest after frames.");
        Check(Cursor.lockState == CursorLockMode.None && Cursor.visible, "Rest releases the actual cursor for Wake.");
        Check(Root.Q<Label>(className: "survival-rest-day").text == "DAY " + runtime.SurvivalRecovery.MissionDay.ToString(CultureInfo.InvariantCulture), "Rest day matches the authoritative mission calendar.");
        Check(Root.Q<Label>(className: "survival-rest-time").text == "Resting · 2h 0m remaining (simulated)", "Rest countdown reflects the service's exact initial two hours.");
        double food = Session.Resources.Snapshot.FoodKg, water = Session.Resources.Snapshot.WaterLitres;
        Command(runtime.SkipSimulatedTime(600), "Advance ten simulated rest minutes through the same resource model");
        yield return Frames(3);
        Check(Math.Abs(runtime.SurvivalRecovery.RestRemainingSimulationSeconds - 6600) < 1e-6 && Session.Resources.Snapshot.FoodKg < food && Session.Resources.Snapshot.WaterLitres < water,
            "Rest remaining time and food/water ledger advance together.");
        Check(Root.Q<Label>(className: "survival-rest-time").text == "Resting · 1h 50m remaining (simulated)", "Rest UI follows the actual remaining time after advancement.");
        yield return Capture("02-rest");
        var wake = Root.Q<Button>("survival-wake");
        runtime.SetPaused(true); runtime.DriveFromUpdate = true;
        double clock = Session.State.ElapsedSimulationSeconds, remaining = runtime.SurvivalRecovery.RestRemainingSimulationSeconds;
        food = Session.Resources.Snapshot.FoodKg; water = Session.Resources.Snapshot.WaterLitres;
        yield return Seconds(.25);
        Check(runtime.IsPaused && Session.State.ElapsedSimulationSeconds == clock && runtime.SurvivalRecovery.RestRemainingSimulationSeconds == remaining
            && Session.Resources.Snapshot.FoodKg == food && Session.Resources.Snapshot.WaterLitres == water,
            "Real runtime Update while paused freezes mission clock, rest and food/water consumption.");
        Check(!Visible(Root.Q("survival-rest")) && !Visible(Root.Q("survival-needs")) && !wake.enabledInHierarchy, "Pause hides survival HUD and disables Wake.");
        using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = wake; wake.SendEvent(evt); }
        Check(!runtime.SurvivalRecovery.TryWake().Succeeded && runtime.SurvivalRecovery.IsResting, "Paused UI submission and authoritative Wake guard cannot wake the crew.");
        yield return Capture("02b-paused-rest");
        runtime.DriveFromUpdate = false; runtime.SetPaused(false); yield return Frames(4);
        Check(runtime.SurvivalRecovery.IsResting && !player.ControlAllowed && Visible(wake), "Resume retains the real rest and its player lock.");
        var restingSession = Session;
        yield return Submit(wake);
        Check(ReferenceEquals(restingSession, Session) && !runtime.SurvivalRecovery.IsResting && runtime.SurvivalRecovery.LastRestStop == RestStopReason.Woken,
            "One actual Wake button click wakes the same session.");
        Check(Session.Clock.TimeScale == awakeScale && player.ControlGate() && player.ControlAllowed && player.WantsCursorCaptured,
            "Wake restores the prior simulation scale and actual player control/cursor intent.");
        Check(!Visible(Root.Q("survival-rest")), "Wake immediately removes the rest overlay.");
        NeedBars();
    }
    IEnumerator LifecycleChecks()
    {
        stage = "survival UI lifecycle";
        VisualElement old = Root.Q("survival-needs"); var same = Session;
        survivalUi.enabled = false; yield return Frames(2);
        Check(old.panel == null && Root.Q("survival-needs") == null && Root.Q("survival-rest") == null, "Presenter disable removes owned widgets from the live panel.");
        Command(runtime.SkipSimulatedTime(60), "Authoritative state changes while the presenter is disabled");
        Check(Root.Q("survival-needs") == null, "Disabled service events do not recreate UI.");
        survivalUi.enabled = true; yield return Frames(3); NeedBars();
        old = Root.Q("survival-needs");
        composition.enabled = false; yield return Frames(2);
        Check(old.panel == null && Root.Q("survival-needs") == null, "Composition teardown removes survival views.");
        composition.enabled = true; yield return Frames(5);
        Check(ReferenceEquals(same, Session) && !same.IsDisposed && player.ControlAllowed, "Composition rebind keeps the same live session and restores its actual gate.");
        NeedBars();
    }
    IEnumerator RecoveryJourney()
    {
        stage = "real rest/beacon recovery journey";
        int rests = 0;
        while (Session.State.Phase == GamePhase.Survival)
        {
            if (++rests > 80) throw new InvalidOperationException("Recovery exceeded 80 bounded rests.");
            if (runtime.SurvivalRecovery.Stage == RecoveryStage.BeaconRequested)
            {
                yield return Frames(3);
                yield return Submit(Root.Q<Button>(className: "systems-button"));
                string[] detail = Root.Q("survival-recovery-detail").Query<Label>().ToList().Select(l => l.text).ToArray();
                Check(Visible(Root.Q("survival-recovery-detail")) && detail.Contains("Beacon requested")
                    && runtime.SurvivalRecovery.BeaconWindowEndsAtSimulationSeconds > Session.State.ElapsedSimulationSeconds
                    && detail.Any(t => t.StartsWith("Beacon window:", StringComparison.Ordinal)),
                    "Optional detail shows the real requested stage and live beacon window.");
                yield return Submit(Root.Q<Button>(className: "systems-button"));
                Command(runtime.SurvivalRecovery.TryPowerBeacon(), "Power the requested recovery beacon through the real ledger");
            }
            RequireCommand(runtime.SurvivalRecovery.TryBeginRest(SurvivalRecovery.MaximumRestHours), "Begin legitimate recovery rest " + rests);
            // Small steps stop at the exact milestone before any next rest; no arbitrary clock/state writes.
            for (int guard = 0; runtime.SurvivalRecovery.IsResting; guard++)
            {
                if (guard >= 20) throw new InvalidOperationException("A maximum 8.5-hour rest did not stop.");
                RequireCommand(Session.AdvanceSimulation(Math.Min(1800, runtime.SurvivalRecovery.RestRemainingSimulationSeconds)), "Advance rest ledger chunk");
            }
            Note("Rest " + rests + " stopped: " + runtime.SurvivalRecovery.LastRestStop + "; day=" + runtime.SurvivalRecovery.MissionDay);
            if (rests % 6 == 0) yield return null;
        }
        runtime.SyncScene();
        Check(Session.State.Outcome == MissionOutcome.Recovered && runtime.SurvivalRecovery.Stage == RecoveryStage.Arrived,
            "Actual repeated rests and paid beacon reach Recovered/Arrived without inventory grants.");
        Check(Session.Cinematics.IsWaiting && Session.Cinematics.ActiveCinematic == CinematicId.RecoveryArrival && composition.Cinematics.IsPlaying,
            "Real RecoveryArrival is playing before the report.");
        Check(!Visible(Root.Q("flightRecorder")), "The end card does not appear before recovery cinematics finish.");
        yield return AwaitReport();
    }
    IEnumerator AwaitReport()
    {
        Note("Waiting for actual authored ending presentation(s); no ending skip, timeout tick or phase transition injection.");
        yield return WaitFor(() => Session.State.Phase == GamePhase.Report && !Session.Cinematics.IsWaiting && !composition.Cinematics.IsPlaying, 30, "Authored ending presentation to Report");
        yield return Frames(4);
        Check(!Session.Cinematics.LastCompletion.Value.TimedOut && !Session.Cinematics.LastCompletion.Value.WasSkipped,
            "Last ending completed normally, without fallback or skip.");
    }
    IEnumerator ReportChecks(MissionOutcome expected, string headline, string capture)
    {
        stage = expected + " report navigation";
        Check(Session.Report != null && Session.Report.IsFinal && Session.Report.Outcome == expected, "Displayed report comes from the real final outcome " + expected + ".");
        var endingIds = expected == MissionOutcome.Recovered ? new[] { CinematicId.RecoveryArrival, CinematicId.MarsDeparture }
            : expected == MissionOutcome.RecoveryPossible ? new[] { CinematicId.CommunicationRestored, CinematicId.EndingPullback }
            : new[] { CinematicId.EndingPullback };
        foreach (CinematicId id in endingIds)
            Check(endedSequences.Count(e => e.Cinematic == id && !e.WasSkipped) == 1 && endedSequences.Count(e => e.Cinematic == id) == 1,
                "Exactly one real, unskipped ending completion: " + id + ".");
        Check(Visible(Root.Q("reportEndCard")) && !Visible(Root.Q("reportDetail")) && !Visible(Root.Q("missionRecord")) && !Visible(Root.Q("reportSources")), "Report opens on its short first view with all technical detail hidden.");
        Check(Root.Q<Label>(className: "recorder-end-headline").text == headline, "End-card headline: " + headline + ".");
        string text = Root.Q<Label>(className: "recorder-end-summary").text;
        Check(!string.IsNullOrWhiteSpace(text) && text.Length <= 240, "First-view summary is present and short (" + text.Length + " characters).");
        Check(Root.Q("reportEndCard").Query<Label>().ToList().Count <= 7 && Root.Q("reportEndActions").Query<Button>().ToList().Count == 3,
            "First view stays compact with mission identity, outcome, elapsed time and three optional actions.");
        Check(!Visible(Root.Q("survival-needs")) && !Visible(Root.Q("survival-rest")) && !player.ControlAllowed, "Final report hides survival HUD and blocks gameplay controls.");
        if (expected == MissionOutcome.RecoveryPossible) Check(text == "Contact is restored. Rescue has not arrived.", "Legacy contact explicitly states rescue has not arrived.");
        if (expected == MissionOutcome.Recovered) Check(text == "Rescue reached Aurora-7. Your next destination: Mars.", "Real Recovered outcome confirms rescue and names Mars as the next destination, without claiming arrival there.");
        if (expected == MissionOutcome.Lost)
        {
            string cause = Session.Report.Sections.Single(s => s.Id == "outcome").Lines.Single(l => l.Label == "Cause").Value;
            Check(text == cause, "Lost first view preserves the exact authoritative cause.");
        }
        yield return new WaitForSecondsRealtime(1f);
        yield return Capture(capture);
        yield return Submit(Root.Q<Button>("missionRecordButton"));
        Check(Visible(Root.Q("missionRecord")) && !Visible(Root.Q("reportEndCard")) && !Visible(Root.Q("reportSources")), "Mission record opens only after its actual button submission.");
        foreach (var section in Session.Report.Sections)
        {
            VisualElement rendered = Root.Q("missionRecord").Q(section.Id);
            Check(rendered != null && rendered.Query<Label>(className: "recorder-value").ToList().Select(l => l.text).SequenceEqual(section.Lines.Select(l => l.Value)),
                "Record preserves supplied values for section " + section.Id + ".");
        }
        yield return Submit(Root.Q<Button>("reportBackButton"));
        Check(Visible(Root.Q("reportEndCard")) && !Visible(Root.Q("reportDetail")), "Back returns from record to the short card.");
        yield return Submit(Root.Q<Button>("reportSourcesButton"));
        Check(Visible(Root.Q("reportSources")) && !Visible(Root.Q("missionRecord")) && !Visible(Root.Q("reportEndCard")), "Sources opens its own optional view.");
        var ids = new[] { "nasa-data", "derived-physics", "fictional-scenario", "time-compression" };
        Check(Root.Q("reportSources").Query<VisualElement>(className: "recorder-section").ToList().Select(e => e.name).OrderBy(x => x)
            .SequenceEqual(Session.Report.Sections.Where(s => ids.Contains(s.Id)).Select(s => s.Id).OrderBy(x => x)), "Sources contains exactly the report's NASA/derived/fiction/time-compression sections.");
        yield return Submit(Root.Q<Button>("reportBackButton"));
        Check(Visible(Root.Q("reportEndCard")) && !Visible(Root.Q("reportDetail")), "Back returns from Sources to the short card.");
    }
    IEnumerator RestartFromCard()
    {
        stage = "actual Plan again restart";
        var old = Session; VisualElement oldCard = Root.Q("reportEndCard");
        yield return Submit(Root.Q<Button>("reportRestartButton"));
        yield return WaitFor(() => Object.FindFirstObjectByType<I01MissionComposition>()?.Runtime?.Session != null
            && !ReferenceEquals(Object.FindFirstObjectByType<I01MissionComposition>().Runtime.Session, old), 10, "Actual restart from end card");
        BindScene(); runtime.DriveFromUpdate = false; yield return Frames(5);
        Check(old.IsDisposed && Session.State.Phase == GamePhase.Planning && Session.State.Outcome == MissionOutcome.None, "Plan again disposes the ended session and returns to clean planning.");
        Check(oldCard.panel == null || !Visible(oldCard), "Old end card is detached or hidden after restart.");
        Check(!composition.ModeChosen && !player.ControlAllowed && !Visible(Root.Q("flightRecorder")), "Restart restores mode choice and hides the previous report.");
    }

    IEnumerator Submit(Button button)
    {
        if (button == null) throw new InvalidOperationException("Expected real UI button is missing.");
        Check(button.enabledInHierarchy && Visible(button), "Submit enabled, visible real button: " + button.text + ".");
        int clicks = 0; Action observe = () => clicks++;
        button.clicked += observe;
        try
        {
            button.Focus();
            using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
            yield return Frames(3);
            Check(clicks == 1, "One NavigationSubmit produces exactly one actual click (" + button.name + ").");
        }
        finally { button.clicked -= observe; }
    }
    IEnumerator Capture(string name)
    {
        var texture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32); texture.Create();
        RenderTexture oldCamera = cameraView.targetTexture, oldPanel = document.panelSettings.targetTexture;
        float oldAspect = cameraView.aspect; bool oldClear = document.panelSettings.clearColor;
        try
        {
            cameraView.targetTexture = texture; cameraView.aspect = 1280f / 720f;
            document.panelSettings.clearColor = false; document.panelSettings.targetTexture = texture;
            for (int i = 0; i < 8; i++) { Root.MarkDirtyRepaint(); yield return null; }
            Check(cameraView.targetTexture == texture && document.panelSettings.targetTexture == texture, "Capture uses the actual camera/document at 1280x720: " + name + ".");
            RenderTexture previous = RenderTexture.active; var image = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
            try { RenderTexture.active = texture; image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply(); File.WriteAllBytes(Path.Combine(I06PresentationQa.Output, name + ".png"), image.EncodeToPNG()); }
            finally { RenderTexture.active = previous; Object.Destroy(image); }
            File.AppendAllText(Path.Combine(I06PresentationQa.Output, "capture-manifest.txt"), name + ".png | 1280x720 | existing camera + UIDocument; overlay UGUI excluded | phase=" + Session.State.Phase + " outcome=" + Session.State.Outcome + " paused=" + runtime.IsPaused + "\n");
            File.WriteAllText(Path.Combine(I06PresentationQa.Output, name + "-state.txt"), "phase=" + Session.State.Phase + "\noutcome=" + Session.State.Outcome
                + "\nsimulationSeconds=" + Session.State.ElapsedSimulationSeconds.ToString("R", CultureInfo.InvariantCulture)
                + "\nresting=" + (runtime.SurvivalRecovery?.IsResting ?? false) + "\ncontrolAllowed=" + player.ControlAllowed
                + "\ncursor=" + Cursor.lockState + "\nvisibleLabels=\n" + string.Join("\n", Root.Query<Label>().ToList().Where(Visible).Select(l => l.text)));
        }
        finally { cameraView.targetTexture = oldCamera; cameraView.aspect = oldAspect; document.panelSettings.targetTexture = oldPanel; document.panelSettings.clearColor = oldClear; texture.Release(); Object.Destroy(texture); }
        yield return Frames(2);
    }
    static bool Visible(VisualElement element)
    {
        if (element == null || element.panel == null) return false;
        for (VisualElement node = element; node != null; node = node.parent)
            if (node.resolvedStyle.display == DisplayStyle.None || node.resolvedStyle.visibility != Visibility.Visible) return false;
        return true;
    }
    static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
    static IEnumerator Seconds(double seconds) { double end = Time.realtimeSinceStartupAsDouble + seconds; while (Time.realtimeSinceStartupAsDouble < end) yield return null; }
    static IEnumerator WaitFor(Func<bool> predicate, double seconds, string message)
    {
        double end = Time.realtimeSinceStartupAsDouble + seconds;
        while (!predicate()) { if (Time.realtimeSinceStartupAsDouble > end) throw new TimeoutException(message); yield return null; }
    }
    void Command(CommandResult result, string message) => Check(result.Succeeded, message + " | " + result.Explanation);
    static void RequireCommand(CommandResult result, string message) { if (!result.Succeeded) throw new InvalidOperationException(message + " | " + result.Explanation); }
    void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); AddResult(true, message); }
    void AddResult(bool pass, string message)
    {
        results.checks.Add(new Entry { utc = DateTime.UtcNow.ToString("O"), stage = stage, message = message, pass = pass, simulationSeconds = runtime && Session != null ? Session.State.ElapsedSimulationSeconds : 0 });
        File.WriteAllText(Path.Combine(I06PresentationQa.Output, "checks.json"), JsonUtility.ToJson(results, true));
        Note((pass ? "PASS " : "FAIL ") + message);
    }
    void Note(string message) { File.AppendAllText(Path.Combine(I06PresentationQa.Output, "checks.txt"), stage + " | " + message + "\n"); Debug.Log("I06_QA " + message); }
    void OnSequenceEnded(CinematicFinished value) => endedSequences.Add(value);
    void Cleanup()
    {
        if (composition && composition.Cinematics) composition.Cinematics.SequenceEnded -= OnSequenceEnded;
        Application.runInBackground = originalRunInBackground;
    }
}
