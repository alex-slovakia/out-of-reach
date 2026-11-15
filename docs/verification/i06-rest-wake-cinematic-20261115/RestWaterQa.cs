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
public static class RestWaterQa
{
    [Serializable] sealed class RunResult { public bool pass; public string message, utc; }
    const string Flag = "OutOfReach.RestWaterQa", OutputKey = Flag + ".Output", Deadline = Flag + ".Deadline", Errors = Flag + ".Errors";
    public const string ScenePath = "Assets/_OutOfReach/Scenes/Bootstrap.unity";
    public static string Output => SessionState.GetString(OutputKey, Path.GetFullPath(".local/rest-water-qa"));
    static RestWaterQa()
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
            SessionState.SetString(OutputKey, Path.Combine(project, ".local/rest-water-qa", DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ")));
            Directory.CreateDirectory(Output);
            SessionState.SetString(Errors, "");
            SessionState.SetString(Deadline, DateTime.UtcNow.AddSeconds(150).ToString("O", CultureInfo.InvariantCulture));
            SessionState.SetString(Flag, "entering");
            File.WriteAllText(Path.Combine(Output, "scope.txt"),
                "Saved Bootstrap, actual bound services and UI. Runtime opt-in + fresh StartSession; scene is not modified.\n"
                + "Direct real launch/repair/EVA/transmission commands and AdvanceSimulation set up the chapter. No player movement/E-key playthrough claim.\n"
                + "Initial Impact/FalseSignal use the real presenter's SkipActive acknowledgement. Transport uses public Skip; no ending sequence is exercised.\n"
                + "DriveFromUpdate is off for deterministic setup and snapshots; enabled during the paused-rest freeze check. No state/phase/resource grants.\n"
                + "Only the contact setup, water projection and rest/pause/Wake checks execute; no recovery/loss/full route. Projection uses a temporary test target, restored before captures.\n"
                + "Wake-title extension: actual Wake events/day snapshots, pause freeze, three-second fade, reduced actual rest/Wake and clean rebind. No direct Title.Begin or fake calendar state.\n"
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
        new GameObject("I06 presentation acceptance - QA only").AddComponent<RestWaterQaDriver>();
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
            .Concat(Directory.GetFiles("Assets/Editor", "RestWaterQa.cs", SearchOption.AllDirectories))
            .Concat(new[] { ScenePath, "ProjectSettings/ProjectVersion.txt" }).OrderBy(p => p, StringComparer.Ordinal);
        using (var sha = SHA256.Create())
            File.WriteAllLines(Path.Combine(Output, "source-sha256.txt"), paths.Select(p =>
                BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(p))).Replace("-", "").ToLowerInvariant() + "  " + p.Replace('\\', '/')));
    }
}

[DefaultExecutionOrder(300)]
public sealed class RestWaterQaDriver : MonoBehaviour
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
    int wakeEventCount, lastWakeDay;
    RestStopReason lastWakeReason;
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
            try { RestWaterQa.RequireNoErrors(); next = stack.Peek().MoveNext(); if (next) current = stack.Peek().Current; }
            catch (Exception error) { failure = error; }
            if (failure != null)
            {
                foreach (IEnumerator pending in stack) try { (pending as IDisposable)?.Dispose(); } catch { }
                AddResult(false, failure.ToString()); Cleanup();
                RestWaterQa.Finish(false, "Stage: " + stage + "\n" + failure); yield break;
            }
            if (!next) { stack.Pop(); continue; }
            if (current is IEnumerator child) { stack.Push(child); continue; }
            yield return current;
        }
        Cleanup();
        RestWaterQa.Finish(true, results.checks.Count + " assertions passed. Actual saved-scene presentation and real domain setup; see scope.txt for automation limits.");
    }

    IEnumerator Exercise()
    {
        yield return WaitFor(() => Object.FindFirstObjectByType<I01MissionComposition>()?.Runtime?.Session != null,15,"Saved Bootstrap");
        BindScene(); yield return Frames(4);
        yield return FreshSession(true);
        yield return ReachContact(); yield return Frames(5);
        Check(wakeEventCount==0,"Initial binding and contact setup do not invent a wake event");
        Check(Session.State.Phase==GamePhase.Survival,"Real contact starts survival for scoped presentation checks");
        var target=Object.FindFirstObjectByType<I06HabitatBindings>().GetPresentationAnchor(InteractionIds.DrinkWater);
        var locator=Root.Q<SurvivalWaterLocator>("survival-water-locator");
        Check(target&&locator!=null&&Visible(locator),"Actual serialized water station is bound and visible");
        Check(Math.Abs(locator.DistanceMetres-Vector3.Distance(cameraView.transform.position,target.position))<.05,"Locator distance matches actual water station");
        var probe=new GameObject("QA projection point");
        probe.transform.position=cameraView.transform.position+cameraView.transform.forward*3f;
        locator.Present(cameraView,probe.transform,true);
        Check(!locator.IsBehind&&!locator.IsEdgeMarker&&Mathf.Abs(locator.DistanceMetres-3f)<.01f,"Front marker projects to target and reports metric distance");
        probe.transform.position=cameraView.transform.position-cameraView.transform.forward*3f;
        locator.Present(cameraView,probe.transform,true);
        Check(locator.IsBehind&&locator.IsEdgeMarker,"Behind target becomes screen-edge arrow");
        locator.Present(cameraView,null,true);Check(locator.ClassListContains("survival-hidden"),"Absent target immediately requests hidden marker");
        Object.Destroy(probe);yield return Frames(3);
        yield return Capture("01-water-direction");
        yield return RestChecks();
        var eyes=Root.Q<SurvivalRestEyes>("survival-rest-eyes");
        yield return Seconds(1.5);Check(eyes.Closure01<.001f&&!Visible(eyes),"Wake fully reopens eyes");
        Check(Visible(locator),"Wake restores water guidance");
        var reduced=new SurvivalRestEyes();reduced.Observe(true,true);
        for(int i=0;i<25;i++)reduced.Advance(.1f,true);
        Check(reduced.Closure01>.999f,"Reduced motion reaches full closure");
        reduced.Observe(false,true);for(int i=0;i<15;i++)reduced.Advance(.1f,true);
        Check(reduced.Closure01<.001f,"Reduced motion wake clears darkness");
        yield return ReducedWakeChecks();
        int eventsBeforeRebind=wakeEventCount;
        var sessionBeforeRebind=Session;
        composition.enabled=false;yield return Frames(2);
        composition.enabled=true;yield return Frames(4);
        Check(ReferenceEquals(sessionBeforeRebind,Session)&&wakeEventCount==eventsBeforeRebind,"Composition rebind retains the actual session and emits no wake event");
        Check(!Root.Q<SurvivalWakeTitle>("survival-wake-title").IsPlaying,"Rebind does not invent a wake title");
        survivalUi.enabled=false;yield return Frames(2);
        Check(Root.Q("survival-presentation-layer")==null,"Disable removes owned overlay");
        survivalUi.enabled=true;yield return Frames(3);
        Check(Root.Query<VisualElement>("survival-presentation-layer").ToList().Count==1,"Re-enable creates one overlay");
        Check(wakeEventCount==eventsBeforeRebind,"Disable/re-enable emits no additional wake event");
    }

    void BindScene()
    {
        if(survivalUi)survivalUi.WakePresentationStarted-=OnWakePresentationStarted;
        if (composition && composition.Cinematics) composition.Cinematics.SequenceEnded -= OnSequenceEnded;
        composition = Object.FindFirstObjectByType<I01MissionComposition>();
        composition.Cinematics.SequenceEnded += OnSequenceEnded;
        runtime = composition.Runtime; player = runtime.Player;
        document = Object.FindFirstObjectByType<MissionFlightBoard>().GetComponent<UIDocument>();
        survivalUi = Object.FindFirstObjectByType<SurvivalChapterPresenter>();
        survivalUi.WakePresentationStarted+=OnWakePresentationStarted;
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
        yield return Frames(3);
        var marine=Object.FindFirstObjectByType<I06MarineBindings>();
        Check(marine&&marine.OwnsPresentation&&marine.Skip(),"Disclosed real transport skip for UI setup");
        yield return WaitFor(()=>!marine.OwnsPresentation,5,"Actual Aurora transfer");
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
        Check(Math.Abs(hunger.value - (1 - runtime.Survival.Hunger01)) < 0.00001 && Math.Abs(hydration.value - (1 - runtime.Survival.Thirst01)) < 0.00001,
            "Both displayed needs equal their authoritative services (remaining hunger satisfaction / remaining hydration).");
        Check(Root.Query<VisualElement>("survival-needs").ToList().Count == 1 && Root.Query<ProgressBar>("survival-hunger-bar").ToList().Count == 1,
            "One needs subtree and one hunger bar; no duplicate binding.");
    }
    IEnumerator RestChecks()
    {
        stage = "actual rest, pause and Wake";
        double awakeScale = Session.Clock.TimeScale;
        Command(runtime.SurvivalRecovery.TryBeginRest(2), "Begin a real two-hour rest");
        yield return Capture("02a-curved-rest-blink",()=>Root.Q<SurvivalRestEyes>("survival-rest-eyes") is SurvivalRestEyes blink&&blink.Closure01>.18f&&blink.Closure01<.90f);
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
        var eyes=Root.Q<SurvivalRestEyes>("survival-rest-eyes");
        Check(eyes!=null,"Rest eyelids attached");
        yield return WaitFor(()=>eyes.Closure01>.999f,12,"Rest eyelids reach closure");
        Check(eyes.Closure01>.999f,"Real rest reaches closed eyes");
        Check(!Visible(Root.Q("survival-water-locator")),"Rest hides water marker");
        Check(Root.Q("survival-rest").parent.IndexOf(Root.Q("survival-rest"))>eyes.parent.IndexOf(eyes),"Wake panel draws above eyelids");
        yield return Capture("02-rest");
        Check(Root.Q<Label>("survival-rest-day").resolvedStyle.fontSize>=80f,"Dark rest shows the larger genuine-font day heading");
        Check(wakeEventCount==0,"Rest entry and repeated service refreshes emit no wake event");
        var wake = Root.Q<Button>("survival-wake");
        runtime.SetPaused(true); runtime.DriveFromUpdate = true;
        double clock = Session.State.ElapsedSimulationSeconds, remaining = runtime.SurvivalRecovery.RestRemainingSimulationSeconds;
        food = Session.Resources.Snapshot.FoodKg; water = Session.Resources.Snapshot.WaterLitres;
        float closedBefore=eyes.Closure01;
        yield return Seconds(.25);
        Check(Mathf.Abs(eyes.Closure01-closedBefore)<.0001f,"Paused eyes freeze presentation progression");
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
        int actualWakeDay=runtime.SurvivalRecovery.MissionDay;
        yield return Submit(wake);
        Check(ReferenceEquals(restingSession, Session) && !runtime.SurvivalRecovery.IsResting && runtime.SurvivalRecovery.LastRestStop == RestStopReason.Woken,
            "One actual Wake button click wakes the same session.");
        Check(Session.Clock.TimeScale == awakeScale && player.ControlGate() && player.ControlAllowed && player.WantsCursorCaptured,
            "Wake restores the prior simulation scale and actual player control/cursor intent.");
        Check(!Visible(Root.Q("survival-rest")), "Wake immediately removes the rest overlay.");
        var title=Root.Q<SurvivalWakeTitle>("survival-wake-title");
        Check(wakeEventCount==1&&lastWakeDay==actualWakeDay&&lastWakeReason==RestStopReason.Woken,"One actual Wake emits one event with the true captured mission day and Woken reason");
        Check(title!=null&&title.IsPlaying&&title.DisplayedDay==actualWakeDay&&Root.Q<Label>("survival-wake-day").text=="DAY "+actualWakeDay,"Wake title uses actual exit-day snapshot without incrementing the day");
        yield return Capture("03-wake-title-opening",()=>title.IsPlaying&&eyes.Closure01>.001f&&eyes.Closure01<.999f);
        runtime.SetPaused(true);yield return Frames(1);
        float pausedTitleProgress=title.Progress01;
        yield return Seconds(.3);
        Check(Mathf.Abs(title.Progress01-pausedTitleProgress)<.0001f&&!Visible(title),"Actual pause freezes wake-title progress and hides it");
        runtime.SetPaused(false);yield return Frames(2);
        Check(title.IsPlaying&&Visible(title),"Resume continues the same wake-title presentation");
        yield return WaitFor(()=>!title.IsPlaying,8,"Actual wake title finishes its three-second presentation");
        Check(title.Progress01>=.999f&&!Visible(title),"Wake title fades and clears after its real presentation duration");
        Check(Visible(Root.Q(className:"hud-header")),"Existing left objective is visible after wake overlay clears");
        Check(wakeEventCount==1,"Title completion and ordinary UI refreshes do not duplicate wake event");
        yield return Capture("04-fully-awake");
        NeedBars();
    }

    IEnumerator ReducedWakeChecks()
    {
        stage="actual reduced-motion rest and wake title";
        bool previous=composition.Cinematics.ReducedEffects;
        try
        {
            composition.SetReducedEffects(true);
            int before=wakeEventCount;
            Command(runtime.SurvivalRecovery.TryBeginRest(.01d),"Begin real short rest for reduced-motion acceptance");
            yield return Frames(3);
            var eyes=Root.Q<SurvivalRestEyes>("survival-rest-eyes");
            yield return WaitFor(()=>eyes.Closure01>.999f,8,"Reduced actual rest closes by fade");
            int actualDay=runtime.SurvivalRecovery.MissionDay;
            yield return Submit(Root.Q<Button>("survival-wake"));
            var title=Root.Q<SurvivalWakeTitle>("survival-wake-title");
            Check(wakeEventCount==before+1&&lastWakeDay==actualDay&&title.DisplayedDay==actualDay,"Reduced actual Wake emits exactly one correctly dated event/title");
            yield return WaitFor(()=>title.Progress01>.2f,4,"Reduced wake title advances");
            yield return Frames(2);
            var dayLabel=Root.Q<Label>("survival-wake-day");
            float offset=dayLabel.style.top.value.value;
            float centerError=dayLabel.worldBound.center.y-title.worldBound.center.y;
            Note("Reduced wake layout: enabled="+composition.Cinematics.ReducedEffects+" playing="+title.IsPlaying
                +" inlineOffset="+offset+" resolvedTop="+dayLabel.resolvedStyle.top+" renderedCenterError="+centerError);
            Check(composition.Cinematics.ReducedEffects&&title.IsPlaying
                &&dayLabel.style.top.keyword==StyleKeyword.Undefined&&Mathf.Abs(offset)<.001f
                &&Mathf.Abs(centerError)<1f,"Reduced-motion wake title fades without any downward offset and stays visually centered");
            yield return Capture("05-reduced-wake-title");
            yield return WaitFor(()=>!title.IsPlaying,8,"Reduced wake title clears");
            Check(!Visible(title)&&eyes.Closure01<.001f,"Reduced wake clears title and eyes without persistent overlay");
        }
        finally{composition.SetReducedEffects(previous);}
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
    IEnumerator Capture(string name,Func<bool> stateReady=null)
    {
        var texture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32); texture.Create();
        RenderTexture oldCamera = cameraView.targetTexture, oldPanel = document.panelSettings.targetTexture;
        float oldAspect = cameraView.aspect; bool oldClear = document.panelSettings.clearColor;
        try
        {
            cameraView.targetTexture = texture; cameraView.aspect = 1280f / 720f;
            document.panelSettings.clearColor = false; document.panelSettings.targetTexture = texture;
            for (int i = 0; i < 8; i++) { Root.MarkDirtyRepaint(); yield return null; }
            if(stateReady!=null)yield return WaitFor(stateReady,4,"Capture actual transient state: "+name);
            Check(cameraView.targetTexture == texture && document.panelSettings.targetTexture == texture, "Capture uses the actual camera/document at 1280x720: " + name + ".");
            RenderTexture previous = RenderTexture.active; var image = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
            try { RenderTexture.active = texture; image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply(); File.WriteAllBytes(Path.Combine(RestWaterQa.Output, name + ".png"), image.EncodeToPNG()); }
            finally { RenderTexture.active = previous; Object.Destroy(image); }
            File.AppendAllText(Path.Combine(RestWaterQa.Output, "capture-manifest.txt"), name + ".png | 1280x720 | existing camera + UIDocument; overlay UGUI excluded | phase=" + Session.State.Phase + " outcome=" + Session.State.Outcome + " paused=" + runtime.IsPaused + "\n");
            File.WriteAllText(Path.Combine(RestWaterQa.Output, name + "-state.txt"), "phase=" + Session.State.Phase + "\noutcome=" + Session.State.Outcome
                + "\nsimulationSeconds=" + Session.State.ElapsedSimulationSeconds.ToString("R", CultureInfo.InvariantCulture)
                + "\nresting=" + (runtime.SurvivalRecovery?.IsResting ?? false) + "\ncontrolAllowed=" + player.ControlAllowed
                + "\nmissionDay=" + (runtime.SurvivalRecovery?.MissionDay??0) + "\nwakeEventCount=" + wakeEventCount
                + "\neyesClosure=" + (Root.Q<SurvivalRestEyes>("survival-rest-eyes")?.Closure01??0f).ToString("R",CultureInfo.InvariantCulture)
                + "\nwakeTitleProgress=" + (Root.Q<SurvivalWakeTitle>("survival-wake-title")?.Progress01??0f).ToString("R",CultureInfo.InvariantCulture)
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
        File.WriteAllText(Path.Combine(RestWaterQa.Output, "checks.json"), JsonUtility.ToJson(results, true));
        Note((pass ? "PASS " : "FAIL ") + message);
    }
    void Note(string message) { File.AppendAllText(Path.Combine(RestWaterQa.Output, "checks.txt"), stage + " | " + message + "\n"); Debug.Log("I06_QA " + message); }
    void OnSequenceEnded(CinematicFinished value) => endedSequences.Add(value);
    void OnWakePresentationStarted(int day,RestStopReason reason){wakeEventCount++;lastWakeDay=day;lastWakeReason=reason;}
    void Cleanup()
    {
        if (composition && composition.Cinematics) composition.Cinematics.SequenceEnded -= OnSequenceEnded;
        if(survivalUi)survivalUi.WakePresentationStarted-=OnWakePresentationStarted;
        Application.runInBackground = originalRunInBackground;
    }
}
