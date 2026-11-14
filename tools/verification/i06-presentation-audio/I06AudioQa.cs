using Cursor = UnityEngine.Cursor;
using Object = UnityEngine.Object;
using OutOfReach.Composition;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Core.Runtime;
using OutOfReach.Player;
using OutOfReach.Presentation.Audio;
using OutOfReach.Presentation.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;

// Temporary saved-scene acceptance runner. No test/domain substitution and no scene writes.
[InitializeOnLoad]
public static class I06AudioQa
{
    [Serializable] sealed class RunResult { public bool pass; public string message, utc; }
    const string Flag = "OutOfReach.I06AudioQa", OutputKey = Flag + ".Output", Deadline = Flag + ".Deadline", Errors = Flag + ".Errors";
    public const string ScenePath = "Assets/_OutOfReach/Scenes/Bootstrap.unity";
    public static string Output => SessionState.GetString(OutputKey, Path.GetFullPath(".local/i06-audio-qa"));
    static I06AudioQa()
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
            SessionState.SetString(OutputKey, Path.Combine(project, ".local/i06-audio-qa", DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ")));
            Directory.CreateDirectory(Output);
            SessionState.SetString(Errors, "");
            SessionState.SetString(Deadline, DateTime.UtcNow.AddSeconds(120).ToString("O", CultureInfo.InvariantCulture));
            SessionState.SetString(Flag, "entering");
            File.WriteAllText(Path.Combine(Output, "scope.txt"),
                "Saved Bootstrap/native audio verification. No production/scene writes.\n"
                + "Actual W input on documented QA lane; explicit teleports also test discontinuity and airborne suppression. No navigation walkthrough claim.\n"
                + "Real domain commands and simulation advancement reach EVA/contact/recovery/loss; cinematics use the real SkipActive handshake to bound this audio test.\n"
                + "Native source flags/weights, listener PCM observations, and human audibility/listening acceptance are distinct. No new audio source/listener or test signal.\n"
                + "A read-only audio filter tap temporarily observes the existing listener; GetOutputData is a separate short-history sample, not a chronological recording.\n"
                + "Zero/no callbacks are recorded as unverified output, never proof of an audible mix or proof that hardware is absent. 120-second wall watchdog.\n");
            I06ExpectedAudioImports.Verify();
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
        new GameObject("I06 audio verification - QA only").AddComponent<I06AudioQaDriver>();
    }
    static void Watchdog()
    {
        if (string.IsNullOrEmpty(SessionState.GetString(Flag, ""))) return;
        if (DateTime.TryParse(SessionState.GetString(Deadline, ""), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime deadline) && DateTime.UtcNow > deadline)
            Finish(false, "120-second watchdog expired. Last completed assertion is in checks.json/checks.txt.");
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
        Debug.Log((pass ? "I06_AUDIO_QA_PASS " : "I06_AUDIO_QA_FAIL ") + message + " output=" + Output);
        EditorApplication.Exit(pass ? 0 : 1);
    }
    static void WriteManifest()
    {
        var paths = Directory.GetFiles("Assets/_OutOfReach", "*", SearchOption.AllDirectories)
            .Where(p => p.EndsWith(".cs") || p.EndsWith(".uss") || p.EndsWith(".asmdef") || p.EndsWith(".wav"))
            .Concat(Directory.GetFiles("Assets/Editor", "I06*.cs", SearchOption.AllDirectories))
            .Concat(Directory.GetFiles("Assets/Editor", "OutOfReach.I06AudioQa.Editor.asmdef", SearchOption.AllDirectories))
            .Concat(new[] { ScenePath, "ProjectSettings/ProjectVersion.txt" }).OrderBy(p => p, StringComparer.Ordinal);
        using (var sha = SHA256.Create())
            File.WriteAllLines(Path.Combine(Output, "source-sha256.txt"), paths.Select(p =>
                BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(p))).Replace("-", "").ToLowerInvariant() + "  " + p.Replace('\\', '/')));
    }
}



[DefaultExecutionOrder(300)]
public sealed class I06AudioQaDriver : MonoBehaviour
{
    [Serializable] sealed class CheckEntry { public string stage, message; public bool pass; }
    [Serializable] sealed class Checks { public List<CheckEntry> checks = new List<CheckEntry>(); }
    [Serializable] sealed class Channel
    {
        public string name, clip, path; public bool playing, started, loop, mute; public float weight, volume; public int timeSamples;
    }
    [Serializable] sealed class Mix
    {
        public string phase, mood, subtitleCue, lastRejection; public bool paused, listenerPaused; public float listenerVolume, fadeElapsed, fadeDuration, duckGain;
        public Channel[] music; public string[] queuedDialogue; public int steps;
    }
    [Serializable] sealed class Signal
    {
        public string phase, nativeOutputStatus; public double dspElapsed; public int sampleRate, callbacks, channels, frames, clipped;
        public float callbackPeak, historyPeak, sfxHistoryPeak; public double callbackRms; public bool editorAudioMuted, listenerPaused; public float listenerVolume;
    }
    [Serializable] sealed class Signals { public List<Signal> observations = new List<Signal>(); public string listeningAcceptance = "NOT_ASSESSED"; }
    readonly Checks checks = new Checks(); readonly Signals signals = new Signals();
    MissionRuntime runtime; I01MissionComposition composition; FirstPersonController player;
    MissionAudioPresenter audioOwner; FootstepAudioPresenter footsteps; I06ListenerPcmTap tap;
    UIDocument document; Keyboard keyboard; Mouse mouse; InputSettings originalInput, qaInput;
    bool originalBackground; string stage = "saved scene";
    AudioSource[] Music => Read<AudioSource[]>(audioOwner, "musicSources");
    float[] Weights => Read<float[]>(audioOwner, "musicWeights");
    MissionSession Session => runtime.Session;
    VisualElement Root => document.rootVisualElement;
    Queue<KeyValuePair<DialogueCueBinding,string>> DialogueQueue => Read<Queue<KeyValuePair<DialogueCueBinding,string>>>(composition, "dialogueQueue");

    IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        originalBackground = Application.runInBackground; Application.runInBackground = true;
        originalInput = InputSystem.settings; qaInput = Object.Instantiate(originalInput);
        qaInput.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        qaInput.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus; InputSystem.settings = qaInput;
        keyboard = InputSystem.AddDevice<Keyboard>("I06 audio QA keyboard"); mouse = InputSystem.AddDevice<Mouse>("I06 audio QA mouse");
        var work = new Stack<IEnumerator>(); work.Push(Exercise());
        while (work.Count > 0)
        {
            Exception failure = null; bool next = false; object current = null;
            try { I06AudioQa.RequireNoErrors(); next = work.Peek().MoveNext(); if (next) current = work.Peek().Current; }
            catch (Exception error) { failure = error; }
            if (failure != null)
            {
                foreach (var pending in work) try { (pending as IDisposable)?.Dispose(); } catch { }
                Record(false, failure.ToString()); SnapshotSafe("failure"); Cleanup();
                I06AudioQa.Finish(false, stage + "\n" + failure + "\nListener signal status is separate; inspect native-output.json and environment.txt."); yield break;
            }
            if (!next) { work.Pop(); continue; }
            if (current is IEnumerator nested) { work.Push(nested); continue; }
            yield return current;
        }
        Cleanup();
        int nonzero = signals.observations.Count(x => x.nativeOutputStatus == "NONZERO_LISTENER_PCM");
        I06AudioQa.Finish(true, checks.checks.Count + " source/control/physics assertions passed. Nonzero listener PCM observed in " + nonzero
            + "/" + signals.observations.Count + " windows (silence expected in some). This is not device/speaker audibility or listening acceptance. See native-output.json.");
    }

    IEnumerator Exercise()
    {
        yield return WaitFor(() => Object.FindFirstObjectByType<I01MissionComposition>()?.Runtime?.Session != null, 15, "Saved scene session");
        BindScene(); yield return Frames(5);
        Check(Object.FindObjectsByType<MissionAudioPresenter>(FindObjectsSortMode.None).Length == 1 && audioOwner.ValidateConfiguration(out _), "Saved scene has one valid audio owner.");
        Check(Music.Length == 4 && Music.All(x => x) && Music.Distinct().Count() == 4, "Four distinct native score channels are bound.");
        string[] fields = { "isolationMusic", "tensionMusic", "emergencyMusic", "recoveryMusic" };
        for (int i = 0; i < fields.Length; i++)
        {
            var clip = Read<AudioClip>(audioOwner, fields[i]);
            string expected = "Assets/_OutOfReach/Audio/Music/mx0" + (i + 1) + "_" + new[] { "isolation", "tension", "emergency", "recovery" }[i] + ".wav";
            Check(clip && UnityEditor.AssetDatabase.GetAssetPath(clip) == expected, "Saved exact score binding: " + expected);
        }
        Check(footsteps && footsteps.IsBound && Read<AudioClip[]>(footsteps, "deckSteps").Length == 3, "Saved footstep presenter is bound to three real contact clips.");
        Check(footsteps.GetComponentsInChildren<AudioSource>(true).Length == 0, "Footsteps delegates to the single audio owner; no private AudioSource.");
        Check(Math.Abs(Read<float>(audioOwner, "musicVolume") - .8f) < 1e-6 && Read<float>(audioOwner, "dialogueMusicDuckDb") == -8,
            "Saved AudioMixV02 music gain .8 and dialogue duck -8 dB.");
        runtime.DriveFromUpdate = false;
        // Record native output even if the first SetMood was rejected by the editor/audio environment.
        yield return Observe("00-initial-native-output");
        yield return Settle(MusicMood.Isolation, "Planning"); yield return Observe("01-planning-calm");
        int count = footsteps.PlayedStepCount; Vector3 position = player.transform.position;
        Keys(Key.W); yield return Seconds(.25); Keys(); yield return Frames(2);
        Check(!composition.ModeChosen && !player.ControlAllowed && footsteps.PlayedStepCount == count && Vector3.Distance(position, player.transform.position) < .05f,
            "Mode menu blocks actual W movement and contact cues.");
        yield return Submit(Root.Query<Button>().ToList().Single(x => x.text.StartsWith("Practice", StringComparison.Ordinal) && Visible(x)));
        Check(composition.ModeChosen && player.ControlAllowed && !runtime.PlanningDeadline.IsEnabled, "Actual Practice button enables grounded movement.");
        yield return FootstepChecks();
        yield return PauseChecks();
        yield return LifecycleChecks();

        stage = "Intro and actual impact hard cut";
        Cmd(Session.Planner.TryLaunch()); yield return Settle(MusicMood.Isolation, "Intro"); yield return Observe("03-intro-calm");
        Cmd(Session.Director.TryCompleteCockpitCheck());
        Check(audioOwner.CurrentMood == MusicMood.Emergency && Weights[0] == 0 && Weights[1] == 0 && Weights[2] == 1 && Weights[3] == 0,
            "Actual impact immediately hard-cuts all calm/tension/recovery weight, without a residual crossfade.");
        Check(Music[0].volume == 0 && !Music[0].isPlaying && !Read<bool[]>(audioOwner,"musicStarted")[0], "Hard cut stops the calm native source and clears its started flag.");
        yield return Observe("04-impact-emergency"); Skip(CinematicId.Impact);
        Check(Session.State.Phase == GamePhase.Emergency && audioOwner.CurrentMood == MusicMood.Emergency && Weights[0] == 0, "Emergency phase keeps the immediate crisis cut.");
        Cmd(Session.Systems.Power.TryRepair(Session.Resources)); yield return Settle(MusicMood.Tension, "partial Repair");
        Cmd(Session.Systems.LifeSupport.TryRepair(Session.Resources)); yield return Settle(MusicMood.Isolation, "stabilized Repair");
        Cmd(Session.Eva.TryPrepareSuit()); Cmd(Session.Eva.TryBeginEva());
        Cmd(Session.AdvanceSimulation(Session.Eva.Spec.DepressurizationSeconds)); Cmd(Session.Eva.TryGoOutside()); runtime.SyncScene();
        yield return Settle(MusicMood.Tension, "EVA");
        // Prior cockpit framing leaves the fixture facing back into the ship. W at
        // the tether start correctly cannot travel farther inward. Face the actual
        // outward tangent for this bounded movement/audio probe, without moving feet.
        Vector3 outwardHeading = runtime.EvaRoute.HeadingAt(0);
        Check(outwardHeading.sqrMagnitude > .01f, "Actual EVA start has an outward horizontal heading.");
        player.transform.rotation = Quaternion.LookRotation(outwardHeading, Vector3.up);
        Note("QA FACING SETUP: face actual outward route tangent; feet and route distance unchanged.");
        int exteriorSteps = footsteps.PlayedStepCount; Vector3 exteriorStart = player.transform.position;
        Note("EVA BEFORE: outside=" + Session.Eva.IsOutside + " route=" + player.IsOnRoute + " allowed=" + player.ControlAllowed
            + " domain=" + Session.IsPlayerControlAllowed + " cinematic=" + Session.Cinematics.ActiveCinematic + " waiting=" + Session.Cinematics.IsWaiting
            + " position=" + exteriorStart + " forward=" + player.transform.forward + " heading=" + runtime.EvaRoute.HeadingAt(0) + " timeScale=" + Time.timeScale);
        Keys(Key.W); yield return Seconds(.65); Keys(); yield return Frames(3);
        Note("EVA AFTER: position=" + player.transform.position + " delta=" + Vector3.Distance(exteriorStart, player.transform.position)
            + " allowed=" + player.ControlAllowed + " route=" + player.IsOnRoute + " input=" + player.InputSource.Move);
        Check(Session.Eva.IsOutside && player.IsOnRoute && Vector3.Distance(exteriorStart, player.transform.position) > .1f,
            "Real W moved the bound exterior route for the no-footsteps probe (not a full EVA traversal).");
        Check(footsteps.PlayedStepCount == exteriorSteps, "Exterior/EVA movement emits no grounded-deck contacts.");
        yield return Observe("05-eva-tension");
        Cmd(Session.Systems.Antenna.TryRepair(Session.Resources)); Cmd(Session.Eva.TryReturnInside()); Cmd(Session.Eva.TryEndEva());
        Cmd(Session.AdvanceSimulation(Session.Eva.Spec.RepressurizationSeconds)); runtime.SyncScene();
        yield return Settle(MusicMood.Tension, "Transmission");
        yield return ReceiveReply();
        yield return Settle(MusicMood.Isolation, "Survival"); yield return Observe("06-survival-calm-and-reply");
        yield return Recovery();
        yield return Settle(MusicMood.Recovery, "Recovered"); yield return Observe("07-recovered-hopeful");
        Check(Session.State.Outcome == MissionOutcome.Recovered, "Recovery music follows genuine Recovered, not merely contact.");
        FinishCinematics(); yield return Frames(4);
        yield return Settle(MusicMood.Recovery, "Recovered report");
        yield return Restart();
        yield return Settle(MusicMood.Isolation, "fresh Planning");
        Check(footsteps.PlayedStepCount == 0 && !audioOwner.IsPresentationPaused, "Restart clears contact cadence and audio pause state.");

        stage = "fresh actual survival loss";
        yield return NewOptIn();
        QuickTransmission(); yield return ReceiveReply();
        Cmd(runtime.SkipSimulatedTime(Session.Survival.Spec.ThirstLimitSeconds + 1));
        Check(Session.State.Outcome == MissionOutcome.Lost, "Real unmet-water accounting produced Lost.");
        yield return Settle(MusicMood.Silent, "Lost");
        Check(!audioOwner.IsDialoguePlaying && DialogueQueue.Count == 0, "Lost clears obsolete dialogue and queued lines as well as the score.");
        yield return Observe("08-lost-silence");
        FinishCinematics(); yield return Frames(4); yield return Restart();
        yield return Settle(MusicMood.Isolation, "final restart Planning");
        Check(footsteps.PlayedStepCount == 0 && Music.Count(x => x.isPlaying) == 1, "Final restart has no footstep backlog and only the requested calm score source playing.");
    }

    void BindScene()
    {
        composition = Object.FindFirstObjectByType<I01MissionComposition>(); runtime = composition.Runtime; player = runtime.Player;
        audioOwner = Object.FindFirstObjectByType<MissionAudioPresenter>(); footsteps = Object.FindFirstObjectByType<FootstepAudioPresenter>();
        document = Object.FindFirstObjectByType<MissionFlightBoard>().GetComponent<UIDocument>();
        player.SendMessage("OnApplicationFocus", true, SendMessageOptions.RequireReceiver);
        var listeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Where(x => x.isActiveAndEnabled).ToArray();
        Check(listeners.Length == 1, "Exactly one active existing AudioListener.");
        tap = listeners[0].gameObject.AddComponent<I06ListenerPcmTap>();
        var config = AudioSettings.GetConfiguration();
        File.AppendAllText(Path.Combine(I06AudioQa.Output,"environment.txt"), "unity=" + Application.unityVersion + " sampleRate=" + config.sampleRate
            + " speakerMode=" + config.speakerMode + " dspBufferSize=" + config.dspBufferSize + " listenerVolume=" + AudioListener.volume
            + " listenerPause=" + AudioListener.pause + " editorMute=" + UnityEditor.EditorUtility.audioMasterMute
            + " noaudioArg=" + Environment.GetCommandLineArgs().Contains("-noaudio") + "\n");
        Note("QA synthetic focus-regain callback; no operating-system focus/audibility claim.");
    }
    IEnumerator NewOptIn()
    {
        runtime.DriveFromUpdate = false; runtime.ConfigureSurvivalExtension(true); runtime.StartSession(); yield return Frames(4);
        yield return Submit(Root.Query<Button>().ToList().Single(x => x.text.StartsWith("Practice", StringComparison.Ordinal) && Visible(x)));
    }
    IEnumerator FootstepChecks()
    {
        stage = "actual grounded contacts";
        yield return Lane();
        int idle = footsteps.PlayedStepCount; yield return Seconds(.2);
        Check(footsteps.PlayedStepCount == idle, "Grounded idle emits no contacts.");
        yield return Walk(2.6f, true); yield return Observe("02-grounded-contact-mix");
        int before = footsteps.PlayedStepCount;
        Teleport(new Vector3(0,.05f,12.7f), "explicit discontinuity after walking; no ResetTracking call");
        yield return Frames(5); yield return Seconds(.15);
        Check(footsteps.PlayedStepCount == before, "Teleport displacement and subsequent idle produce no contact/backlog.");
        yield return Walk(.2f, false);
        Check(footsteps.PlayedStepCount == before, "Sub-step actual movement after teleport cannot replay the old partial distance.");
        // A deliberately elevated QA start provides >one stride of truly airborne travel, not a new game command.
        Teleport(new Vector3(0,12,12.7f), "airborne suppression fixture above the hull; not a reachable player spawn");
        yield return Frames(1);
        var body = player.GetComponent<CharacterController>(); Check(!body.isGrounded, "Native CharacterController is genuinely airborne at the QA fixture.");
        before = footsteps.PlayedStepCount; Vector3 start = player.transform.position; double end = Time.realtimeSinceStartupAsDouble + 5;
        Keys(Key.W);
        while (!body.isGrounded)
        {
            if (Time.realtimeSinceStartupAsDouble > end) throw new TimeoutException("Airborne fixture did not land.");
            if (footsteps.PlayedStepCount != before) throw new InvalidOperationException("A contact emitted while the native controller was not grounded.");
            yield return null;
        }
        Keys(); yield return Frames(2);
        Check(Vector3.ProjectOnPlane(player.transform.position - start,Vector3.up).magnitude > 1.1f, "Airborne probe travelled farther than a full stride, so grounding suppression was substantive.");
        yield return Lane();
    }
    IEnumerator Walk(float metres, bool expectContacts)
    {
        int count = footsteps.PlayedStepCount; Vector3 previous = player.transform.position; float distance = 0, maxDelta = 0;
        double end = Time.realtimeSinceStartupAsDouble + 6; Keys(Key.W);
        var body = player.GetComponent<CharacterController>();
        while (distance < metres)
        {
            if (Time.realtimeSinceStartupAsDouble > end) throw new TimeoutException("Actual W did not travel the requested distance.");
            yield return null;
            if (!keyboard.wKey.isPressed || player.InputSource.Move.y < .9f) { previous = player.transform.position; continue; }
            if (!player.ControlAllowed || !body.isGrounded || (body.collisionFlags & CollisionFlags.Sides) != 0)
                throw new InvalidOperationException("Measured W ceased to be allowed, grounded and unobstructed.");
            distance += Vector3.ProjectOnPlane(player.transform.position - previous, Vector3.up).magnitude;
            maxDelta = Mathf.Max(maxDelta, Time.unscaledDeltaTime); previous = player.transform.position;
        }
        Keys(); yield return Frames(2); int emitted = footsteps.PlayedStepCount - count;
        Check(maxDelta <= .25f, "Walk sampling stays within the production cadence's 250 ms hitch limit; max=" + maxDelta);
        Check(expectContacts ? emitted >= 1 && emitted <= Math.Ceiling(distance / Read<float>(footsteps,"metresPerStep")) + 1 : emitted == 0,
            "Actual grounded distance=" + distance + " m emitted=" + emitted + " contacts; expectedContacts=" + expectContacts);
    }
    IEnumerator PauseChecks()
    {
        stage = "pause/resume audio and cadence";
        yield return Lane(); yield return Walk(.3f, false);
        runtime.SetPaused(true); yield return Frames(4);
        float[] weights = (float[])Weights.Clone(); int[] samples = Music.Select(x => x.clip ? x.timeSamples : -1).ToArray();
        float fade = Read<float>(audioOwner,"fadeElapsed"), duck = Read<float>(audioOwner,"duckGain"); int steps = footsteps.PlayedStepCount;
        Vector3 pos = player.transform.position; Keys(Key.W); yield return Seconds(.3); Keys();
        Check(audioOwner.IsPresentationPaused && Weights.SequenceEqual(weights) && Music.Select(x => x.clip ? x.timeSamples : -1).SequenceEqual(samples)
            && Read<float>(audioOwner,"fadeElapsed") == fade && Read<float>(audioOwner,"duckGain") == duck, "Pause freezes native source positions, score weights, fade and duck state.");
        Check(footsteps.PlayedStepCount == steps && Vector3.Distance(pos,player.transform.position) < .02f, "Paused W neither moves nor emits contacts.");
        Snapshot("paused"); yield return Observe("02b-paused-audio");
        runtime.SetPaused(false); yield return Frames(4);
        Check(!audioOwner.IsPresentationPaused && player.ControlAllowed, "Real resume restores audio and player control.");
        yield return Seconds(.2); Check(footsteps.PlayedStepCount == steps, "Resume without movement does not replay a footstep backlog.");
        yield return Walk(.2f, false); Check(footsteps.PlayedStepCount == steps, "Resumed sub-stride movement starts clean after suppressed partial distance.");
        yield return Settle(MusicMood.Isolation,"resumed Planning");
    }
    IEnumerator LifecycleChecks()
    {
        stage = "composition teardown/rebind"; var same = Session;
        composition.enabled = false; yield return Frames(3);
        Check(audioOwner.CurrentMood == MusicMood.Silent && Weights.All(w => w == 0) && Music.All(s => !s.isPlaying) && !audioOwner.IsDialoguePlaying,
            "Composition disable stops all score/dialogue and clears weights.");
        Check(!footsteps.IsBound && footsteps.PlayedStepCount == 0, "Composition disable releases footstep services and cadence.");
        composition.enabled = true; yield return Frames(4);
        Check(ReferenceEquals(same, Session) && footsteps.IsBound, "Rebind uses the same real session and reconnects contacts.");
        // Practice is not a persistent deadline; re-enable correctly restores the saved mode choice menu.
        if (!composition.ModeChosen) yield return Submit(Root.Query<Button>().ToList().Single(x => x.text.StartsWith("Practice",StringComparison.Ordinal) && Visible(x)));
        yield return Settle(MusicMood.Isolation,"rebound Planning");
        yield return NewOptIn();
    }
    IEnumerator ReceiveReply()
    {
        stage = "actual receipt replaces diagnostic dialogue";
        Cmd(Session.Director.TryRunLinkDiagnostic()); Skip(CinematicId.FalseSignal); yield return Frames(2);
        Cmd(Session.Communications.TryTransmit());
        Check(Session.Communications.State == CommunicationState.AwaitingReply && audioOwner.ActiveCueId != "vo11_mc_reply_received",
            "Earth reply cue is absent before the actual inbound/outbound interval completes.");
        Cmd(runtime.SkipSimulatedTime(Session.Communications.ReplyRemainingSimulationSeconds));
        yield return WaitFor(() => audioOwner.IsDialoguePlaying && audioOwner.CurrentSubtitle.CueId == "vo11_mc_reply_received", 3, "Real received reply cue replaces obsolete diagnostics");
        var obsolete = Read<DialogueCueBinding>(composition,"falseSignal");
        Check(Session.Communications.State == CommunicationState.ReplyReceived && Session.State.Phase == GamePhase.Survival && Session.State.Outcome == MissionOutcome.None,
            "Required reply cue is gated by real receipt and active Survival, not an ending or timer shortcut.");
        Check(audioOwner.CurrentSubtitle.CueId == "vo11_mc_reply_received" && (obsolete == null || audioOwner.ActiveCueId != obsolete.CueId)
            && DialogueQueue.All(q => obsolete == null || q.Key.CueId != obsolete.CueId), "Current and queued false-signal dialogue are absent after receipt; current cue is vo11_mc_reply_received.");
        Snapshot("real-reply-received");
    }
    void QuickTransmission()
    {
        Cmd(Session.Planner.TryLaunch()); Cmd(Session.Director.TryCompleteCockpitCheck()); Skip(CinematicId.Impact);
        Cmd(Session.Systems.Power.TryRepair(Session.Resources)); Cmd(Session.Systems.LifeSupport.TryRepair(Session.Resources));
        Cmd(Session.Eva.TryPrepareSuit()); Cmd(Session.Eva.TryBeginEva()); Cmd(Session.AdvanceSimulation(Session.Eva.Spec.DepressurizationSeconds));
        Cmd(Session.Eva.TryGoOutside()); Cmd(Session.Systems.Antenna.TryRepair(Session.Resources)); Cmd(Session.Eva.TryReturnInside()); Cmd(Session.Eva.TryEndEva());
        Cmd(Session.AdvanceSimulation(Session.Eva.Spec.RepressurizationSeconds)); runtime.SyncScene();
    }
    IEnumerator Recovery()
    {
        stage = "real recovery timeline";
        for (int rests = 0; Session.State.Phase == GamePhase.Survival; rests++)
        {
            if (rests >= 80) throw new InvalidOperationException("Recovery exceeded bounded legitimate rests.");
            if (runtime.SurvivalRecovery.Stage == RecoveryStage.BeaconRequested) Cmd(runtime.SurvivalRecovery.TryPowerBeacon());
            Cmd(runtime.SurvivalRecovery.TryBeginRest(SurvivalRecovery.MaximumRestHours));
            for (int chunks = 0; runtime.SurvivalRecovery.IsResting; chunks++)
            {
                if (chunks >= 20) throw new InvalidOperationException("Rest did not stop.");
                Cmd(Session.AdvanceSimulation(Math.Min(1800,runtime.SurvivalRecovery.RestRemainingSimulationSeconds)));
            }
            if (rests % 8 == 0) yield return null;
        }
        runtime.SyncScene();
    }
    IEnumerator Settle(MusicMood mood, string context)
    {
        stage = context + " source mix";
        // SmoothStep enters the weight tolerance before the fade actually ends.
        // Native sources are only stopped at completion, so wait for the owner's clock too.
        yield return WaitFor(() => audioOwner.CurrentMood == mood
            && Read<float>(audioOwner,"fadeElapsed") >= Read<float>(audioOwner,"fadeDuration")
            && Weights.Select((w,i) => Math.Abs(w - ((int)mood - 1 == i ? 1 : 0)) < .00001f).All(x => x), 4,
            context + " did not reach " + mood + "; native rejection=" + audioOwner.LastRejection);
        yield return null; // Allow the next Update before asserting terminal source state.
        Check(audioOwner.CurrentMood == mood
            && Read<float>(audioOwner,"fadeElapsed") >= Read<float>(audioOwner,"fadeDuration")
            && Weights.Select((w,i) => w == ((int)mood - 1 == i ? 1f : 0f)).All(x => x),
            context + ": actual fade completed with exact final weights.");
        for (int i=0;i<Music.Length;i++)
        {
            bool selected = i == (int)mood - 1;
            Check(Music[i].isPlaying == selected && Read<bool[]>(audioOwner,"musicStarted")[i] == selected,
                context + ": native channel " + i + " playing/started matches selection (source state, not audibility).");
            float expected = Read<float>(audioOwner,"masterVolume") * Read<float>(audioOwner,"musicVolume") * Weights[i] * Read<float>(audioOwner,"duckGain");
            Check(Math.Abs(Music[i].volume - expected) < .00001f, context + ": native channel volume matches actual weight and duck gain.");
        }
        Snapshot(context.Replace(' ','-'));
    }
    IEnumerator Restart()
    {
        stage = "actual report restart"; Keys(); var old = Session;
        yield return Submit(Root.Q<Button>("reportRestartButton"));
        yield return WaitFor(() => Object.FindFirstObjectByType<I01MissionComposition>()?.Runtime?.Session != null
            && !ReferenceEquals(Object.FindFirstObjectByType<I01MissionComposition>().Runtime.Session,old), 10,"Report restart");
        if(tap) Object.Destroy(tap); BindScene(); runtime.DriveFromUpdate = false; yield return Frames(4);
        Check(old.IsDisposed && Session.State.Phase == GamePhase.Planning && !composition.ModeChosen, "Real Plan again disposes the old session and restores the menu.");
    }
    void Skip(CinematicId id)
    {
        Check(Session.Cinematics.IsWaiting && Session.Cinematics.ActiveCinematic == id && composition.Cinematics.SkipActive(), "Disclosed real presentation skip: " + id);
    }
    void FinishCinematics()
    {
        for(int i=0;Session.Cinematics.IsWaiting;i++) { if(i>=4) throw new InvalidOperationException("Unbounded ending chain."); Skip(Session.Cinematics.ActiveCinematic); }
    }
    IEnumerator Lane() { Keys(); Teleport(new Vector3(0,.05f,12.7f),"known clear storage lane"); yield return WaitFor(() => player.GetComponent<CharacterController>().isGrounded,3,"Grounded lane"); yield return Frames(3); }
    void Teleport(Vector3 feet,string reason) { Note("QA TELEPORT: "+reason+"; "+feet); player.Teleport(feet,0); Physics.SyncTransforms(); }
    void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
    IEnumerator Submit(Button button)
    {
        Check(button != null && button.enabledInHierarchy && Visible(button),"Submit actual visible button: "+button?.text);
        int clicks=0; Action observe=()=>clicks++; button.clicked+=observe;
        try { button.Focus(); using(var evt=NavigationSubmitEvent.GetPooled()){evt.target=button;button.SendEvent(evt);} yield return Frames(3); Check(clicks==1,"Exactly one native UI callback."); }
        finally {button.clicked-=observe;}
    }
    IEnumerator Observe(string name)
    {
        float[] history=new float[2048], sfx=new float[1024]; AudioListener.GetOutputData(history,0);
        AudioSource contact=Read<AudioSource>(audioOwner,"sfxSource"); contact.GetOutputData(sfx,0);
        yield return Seconds(.08); tap.Begin(AudioSettings.outputSampleRate); double dsp=AudioSettings.dspTime;
        float peak=0, contactPeak=0; double end=Time.realtimeSinceStartupAsDouble+1;
        while(Time.realtimeSinceStartupAsDouble<end)
        {
            AudioListener.GetOutputData(history,0); contact.GetOutputData(sfx,0);
            foreach(float value in history) peak=Math.Max(peak,Math.Abs(value));
            foreach(float value in sfx) contactPeak=Math.Max(contactPeak,Math.Abs(value));
            yield return null;
        }
        var captured=tap.End(); var signal=new Signal {phase=name,dspElapsed=AudioSettings.dspTime-dsp,sampleRate=captured.rate,
            callbacks=captured.callbacks,channels=captured.channels,frames=captured.channels>0?captured.samples.Length/captured.channels:0,
            historyPeak=peak,sfxHistoryPeak=contactPeak,listenerPaused=AudioListener.pause,listenerVolume=AudioListener.volume,editorAudioMuted=UnityEditor.EditorUtility.audioMasterMute};
        double square=0;
        foreach(float value in captured.samples) { signal.callbackPeak=Math.Max(signal.callbackPeak,Math.Abs(value)); square+=(double)value*value; if(Math.Abs(value)>=1)signal.clipped++; }
        signal.callbackRms=captured.samples.Length>0?Math.Sqrt(square/captured.samples.Length):0;
        signal.nativeOutputStatus=signal.callbackPeak>1e-6 || peak>1e-6 ? "NONZERO_LISTENER_PCM"
            : captured.callbacks>0 ? "ZERO_LISTENER_PCM" : "NO_CALLBACK_OR_NONZERO_HISTORY_DEVICE_UNDETERMINED";
        signals.observations.Add(signal); File.WriteAllText(Path.Combine(I06AudioQa.Output,"native-output.json"),JsonUtility.ToJson(signals,true));
        if(captured.samples.Length>0) I06ListenerPcmTap.WriteWav(Path.Combine(I06AudioQa.Output,name+"-listener.wav"),captured);
        Note(name+": "+signal.nativeOutputStatus+" callbacks="+signal.callbacks+" peak="+signal.callbackPeak+" historyPeak="+peak+" (not speaker audibility)");
        if(captured.samples.Length>0)
            Check(captured.samples.All(v => !float.IsNaN(v) && !float.IsInfinity(v)) && signal.clipped == 0,
                "Observed listener PCM is finite and contains no full-scale/over-range samples in " + name + ".");
        Snapshot(name);
    }
    void Snapshot(string name)
    {
        var mix=new Mix {phase=Session.State.Phase.ToString(),mood=audioOwner.CurrentMood.ToString(),subtitleCue=audioOwner.ActiveCueId,lastRejection=audioOwner.LastRejection,
            paused=audioOwner.IsPresentationPaused,listenerPaused=AudioListener.pause,listenerVolume=AudioListener.volume,fadeElapsed=Read<float>(audioOwner,"fadeElapsed"),fadeDuration=Read<float>(audioOwner,"fadeDuration"),duckGain=Read<float>(audioOwner,"duckGain"),
            steps=footsteps.PlayedStepCount,queuedDialogue=DialogueQueue.Select(q=>q.Key.CueId).ToArray(),
            music=Music.Select((s,i)=>new Channel{name=s.name,clip=s.clip?s.clip.name:"",path=s.clip?UnityEditor.AssetDatabase.GetAssetPath(s.clip):"",playing=s.isPlaying,
                started=Read<bool[]>(audioOwner,"musicStarted")[i],loop=s.loop,mute=s.mute,weight=Weights[i],volume=s.volume,timeSamples=s.clip?s.timeSamples:-1}).ToArray()};
        File.WriteAllText(Path.Combine(I06AudioQa.Output,name+"-mix.json"),JsonUtility.ToJson(mix,true));
    }
    void SnapshotSafe(string name) {try {if(audioOwner&&runtime&&Session!=null)Snapshot(name);}catch{} }
    static T Read<T>(object target,string field)
    {
        var found=target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic);
        if(found==null) throw new MissingFieldException(target.GetType().Name,field);
        return (T)found.GetValue(target);
    }
    static bool Visible(VisualElement element) {if(element==null||element.panel==null)return false;for(var node=element;node!=null;node=node.parent)if(node.resolvedStyle.display==DisplayStyle.None||node.resolvedStyle.visibility!=Visibility.Visible)return false;return true;}
    static IEnumerator Frames(int count){for(int i=0;i<count;i++)yield return null;}
    static IEnumerator Seconds(double seconds){double end=Time.realtimeSinceStartupAsDouble+seconds;while(Time.realtimeSinceStartupAsDouble<end)yield return null;}
    static IEnumerator WaitFor(Func<bool> predicate,double seconds,string reason){double end=Time.realtimeSinceStartupAsDouble+seconds;while(!predicate()){if(Time.realtimeSinceStartupAsDouble>end)throw new TimeoutException(reason);yield return null;}}
    static void Cmd(CommandResult result){if(!result.Succeeded)throw new InvalidOperationException(result.Explanation);}
    void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);Record(true,message);}
    void Record(bool pass,string message){checks.checks.Add(new CheckEntry{stage=stage,message=message,pass=pass});File.WriteAllText(Path.Combine(I06AudioQa.Output,"checks.json"),JsonUtility.ToJson(checks,true));Note((pass?"PASS ":"FAIL ")+message);}
    void Note(string message){File.AppendAllText(Path.Combine(I06AudioQa.Output,"checks.txt"),stage+" | "+message+"\n");Debug.Log("I06_AUDIO_QA "+message);}
    void Cleanup()
    {
        if(tap){tap.End();Object.Destroy(tap);}
        if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);if(mouse!=null&&mouse.added)InputSystem.RemoveDevice(mouse);
        if(originalInput)InputSystem.settings=originalInput;if(qaInput)Object.Destroy(qaInput);Application.runInBackground=originalBackground;
    }
}


// Temporary, read-only filter on the EXISTING listener. Never changes samples or generates sound.
public sealed class I06ListenerPcmTap : MonoBehaviour
{
    public sealed class Capture { public float[] samples; public int channels, rate, callbacks; }
    readonly object gate=new object(); float[] buffer; int count, channels, rate, callbacks; bool recording;
    public void Begin(int sampleRate)
    {
        lock(gate){rate=sampleRate;buffer=new float[Math.Max(8000,sampleRate)*8*3];count=channels=callbacks=0;recording=true;}
    }
    void OnAudioFilterRead(float[] data,int channelCount)
    {
        // Audio thread: bounded copy only. No allocation, Unity calls, file IO or logging.
        lock(gate)
        {
            if(!recording)return;
            if(channels==0)channels=channelCount;
            if(channelCount!=channels)return;
            int length=Math.Min(data.Length,buffer.Length-count);length-=length%Math.Max(1,channels);
            if(length>0){Array.Copy(data,0,buffer,count,length);count+=length;}
            callbacks++;
        }
    }
    public Capture End()
    {
        lock(gate){recording=false;var copy=new float[count];if(count>0)Array.Copy(buffer,copy,count);return new Capture{samples=copy,channels=channels,rate=rate,callbacks=callbacks};}
    }
    void OnDisable(){lock(gate)recording=false;}
    public static void WriteWav(string path,Capture capture)
    {
        // IEEE float WAV preserves the observed samples (including any over-range data), without normalization/clipping.
        int bytes=capture.samples.Length*4;
        using(var writer=new BinaryWriter(File.Create(path)))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(48+bytes);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);writer.Write((short)3);writer.Write((short)capture.channels);writer.Write(capture.rate);
            writer.Write(capture.rate*capture.channels*4);writer.Write((short)(capture.channels*4));writer.Write((short)32);
            writer.Write(Encoding.ASCII.GetBytes("fact"));writer.Write(4);writer.Write(capture.samples.Length/capture.channels);
            writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(bytes);foreach(float sample in capture.samples)writer.Write(sample);
        }
    }
}
