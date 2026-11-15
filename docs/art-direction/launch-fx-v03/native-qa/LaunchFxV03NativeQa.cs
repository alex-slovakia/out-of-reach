// LAUNCH-FX-V03 native QA (temporary editor tool, never part of the game).
// Copy this file and its asmdef to <own clone>/Assets/_LaunchFxQa/Editor/ for a run, delete afterwards.
// Unity.exe -batchmode -projectPath <own clone> -executeMethod LaunchFxV03NativeQa.RunFull   (GPU required, no -nographics)
//                                                          ... LaunchFxV03NativeQa.RunReducedSkip
// It opens the actual saved Bootstrap, chooses Practice through the real UI, walks to the transport seat with
// real input, presses E and samples the real departure. The only non-player action is binding the V03 presenter
// through the integration patch method I06MarineBindings.ConfigureLaunchFxV03 (what root's builder would do).
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
using OutOfReach.Presentation.LaunchFXV03;
using OutOfReach.Presentation.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class LaunchFxV03NativeQa
{
    const string Key = "OutOfReach.LaunchFxV03NativeQa.";
    public const string Bootstrap = "Assets/_OutOfReach/Scenes/Bootstrap.unity";
    public const string Prefab = "Assets/_OutOfReach/Visuals/LaunchFX_V03/LaunchFX_V03.prefab";
    public static string Output => SessionState.GetString(Key + "output", "");
    public static string Mode => SessionState.GetString(Key + "mode", "");
    static string Stage => SessionState.GetString(Key + "stage", "");
    static LaunchFxV03NativeQa()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
        EditorApplication.update += Watchdog;
        Application.logMessageReceived += OnLog;
    }
    public static void RunFull() => Run("full");
    public static void RunReducedSkip() => Run("reduced-skip");
    static void Run(string mode)
    {
        try
        {
            if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Batch edit mode only.");
            if (Application.unityVersion != "6000.3.28f1" || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Unity 6000.3.28f1 with a real GPU is required (no -nographics).");
            string root = Environment.GetEnvironmentVariable("LTFX_QA_OUT");
            if (string.IsNullOrEmpty(root)) root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../out-of-reach-tower-logs/fxqa"));
            SessionState.SetString(Key + "output", Path.Combine(root, mode + "-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture)));
            SessionState.SetString(Key + "mode", mode);
            Directory.CreateDirectory(Output);
            SessionState.SetString(Key + "deadline", DateTime.UtcNow.AddSeconds(240).ToString("O", CultureInfo.InvariantCulture));
            SessionState.SetBool(Key + "async", ShaderUtil.allowAsyncCompilation); ShaderUtil.allowAsyncCompilation = false;
            Note("GPU " + SystemInfo.graphicsDeviceName + " | " + SystemInfo.graphicsDeviceType + " | mode " + mode);
            SetStage("opening actual saved Bootstrap");
            EditorSceneManager.OpenScene(Bootstrap, OpenSceneMode.Single);
            SetStage("entering"); EditorApplication.isPlaying = true;
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }
    static void OnPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || Stage != "entering") return;
        try { SetStage("play"); new GameObject("LAUNCH_FX_V03_NATIVE_QA_UNSAVED").AddComponent<LaunchFxV03NativeQaDriver>(); }
        catch (Exception error) { Finish(false, error.ToString()); }
    }
    static void Watchdog()
    {
        if (Stage.Length == 0) return;
        if (DateTime.TryParse(SessionState.GetString(Key + "deadline", ""), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime deadline)
            && DateTime.UtcNow > deadline) Finish(false, "240 s watchdog expired at stage " + Stage);
    }
    static void OnLog(string message, string stack, LogType type)
    {
        if (Stage.Length == 0 || (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)) return;
        string entry = type + ": " + message + "\n" + stack + "\n";
        if (stack.Contains("UnityEditor.Search")) { File.AppendAllText(Path.Combine(Output, "editor-environment-errors.txt"), entry); return; }
        SessionState.SetString(Key + "errors", SessionState.GetString(Key + "errors", "") + entry);
        File.AppendAllText(Path.Combine(Output, "runtime-errors.txt"), entry);
    }
    public static void RequireNoErrors()
    {
        string errors = SessionState.GetString(Key + "errors", ""); if (errors.Length > 0) throw new InvalidOperationException(errors);
    }
    public static void SetStage(string value) => SessionState.SetString(Key + "stage", value);
    public static void Check(bool condition, string message)
    {
        File.AppendAllText(Path.Combine(Output, "checks.txt"), (condition ? "PASS " : "FAIL ") + Stage + " | " + message + "\n");
        if (!condition) throw new InvalidOperationException(message);
    }
    public static void Note(string message) => File.AppendAllText(Path.Combine(Output, "observations.txt"), message + "\n");
    public static void Finish(bool pass, string message)
    {
        try { RequireNoErrors(); } catch (Exception error) { pass = false; message += "\n" + error.Message; }
        SessionState.SetString(Key + "stage", ""); ShaderUtil.allowAsyncCompilation = SessionState.GetBool(Key + "async", true);
        if (Output.Length > 0) { Directory.CreateDirectory(Output); File.WriteAllText(Path.Combine(Output, pass ? "PASS.txt" : "FAIL.txt"), message); }
        Debug.Log((pass ? "LAUNCH_FX_V03_QA_PASS " : "LAUNCH_FX_V03_QA_FAIL ") + message + " output=" + Output);
        EditorApplication.Exit(pass ? 0 : 1);
    }
}

[DefaultExecutionOrder(600)]
public sealed class LaunchFxV03NativeQaDriver : MonoBehaviour
{
    I01MissionComposition composition; MissionRuntime runtime; FirstPersonController player; InteractionResolver resolver;
    I06MarineBindings marine; TransportDeparturePresenter departure; LaunchFxV03Presenter fx; Camera view; UIDocument document;
    Keyboard keyboard; Mouse mouse; InputSettings originalSettings, qaSettings; HideFlags originalFlags;
    readonly List<InputDevice> disabledDevices = new List<InputDevice>();
    int interactions; CommandResult lastInteraction; bool tracing; ParticleSystem steam; Light practical;
    MeshRenderer envelope, core; string stage = "startup";
    static string Output => LaunchFxV03NativeQa.Output;
    static double Now => Time.realtimeSinceStartupAsDouble;
    static T Read<T>(object host, string field) => (T)host.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(host);
    void Check(bool pass, string message) => LaunchFxV03NativeQa.Check(pass, message);
    void Note(string text) => LaunchFxV03NativeQa.Note(Now.ToString("F3", CultureInfo.InvariantCulture) + " | " + text);
    void Stage(string value) { stage = value; LaunchFxV03NativeQa.SetStage(value); Note(value); }

    IEnumerator Start()
    {
        Application.runInBackground = true;
        originalSettings = InputSystem.settings; originalFlags = originalSettings.hideFlags;
        if (originalFlags == HideFlags.HideAndDontSave) originalSettings.hideFlags = HideFlags.DontSave;
        qaSettings = Object.Instantiate(originalSettings); qaSettings.hideFlags = HideFlags.DontSave;
        qaSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        qaSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings = qaSettings;
        foreach (var device in InputSystem.devices.ToArray())
            if (device.enabled && (device is Mouse || device is Keyboard || device is Gamepad)) { disabledDevices.Add(device); InputSystem.DisableDevice(device); }
        keyboard = InputSystem.AddDevice<Keyboard>("Launch QA keyboard"); mouse = InputSystem.AddDevice<Mouse>("Launch QA mouse");
        var stack = new Stack<IEnumerator>(); stack.Push(Exercise()); Exception failed = null;
        while (stack.Count > 0)
        {
            bool more = false; object yielded = null;
            try { LaunchFxV03NativeQa.RequireNoErrors(); more = stack.Peek().MoveNext(); if (more) yielded = stack.Peek().Current; }
            catch (Exception error) { failed = error; }
            if (failed != null) break;
            if (!more) { stack.Pop(); continue; }
            if (yielded is IEnumerator nested) { stack.Push(nested); continue; }
            yield return yielded;
        }
        Cleanup();
        LaunchFxV03NativeQa.Finish(failed == null, failed == null ? "LAUNCH-FX-V03 native run passed (" + LaunchFxV03NativeQa.Mode + ")." : "Stage=" + stage + "\n" + failed);
    }

    IEnumerator Exercise()
    {
        yield return WaitFor(() => Object.FindFirstObjectByType<MissionRuntime>()?.Session != null, 20, "session startup");
        composition = Object.FindFirstObjectByType<I01MissionComposition>(); runtime = composition.Runtime; player = runtime.Player;
        resolver = player.Interaction; marine = Object.FindFirstObjectByType<I06MarineBindings>();
        departure = Read<TransportDeparturePresenter>(marine, "departure");
        view = Camera.main; document = Object.FindFirstObjectByType<MissionFlightBoard>().GetComponent<UIDocument>();
        Check(marine && departure && view, "Actual Bootstrap marine bindings, departure presenter and camera exist.");
        player.SendMessage("OnApplicationFocus", true, SendMessageOptions.RequireReceiver);
        resolver.Interacted += OnInteract;

        Stage("bind V03 through ConfigureLaunchFxV03");
        Transform nozzle = Read<Transform>(marine, "nozzleFrame"), deflector = Read<Transform>(marine, "deckPlane"), pad = Read<Transform>(marine, "dustPlane");
        Transform exit = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == "LS_TrenchExit_Ground");
        Check(nozzle && deflector && pad && exit, "Anchors found: nozzle=" + Path(nozzle) + " deflector=" + Path(deflector) + " pad=" + Path(pad) + " exit=" + Path(exit));
        Vector3 local = marine.MarineRoot.InverseTransformPoint(pad.position);
        Note("pad plane local y " + local.y.ToString("F3") + " (expect -32.6); deflector world y " + deflector.position.y.ToString("F3")
            + "; nozzle world " + nozzle.position.ToString("F3") + " fwd " + nozzle.forward.ToString("F3") + "; exit " + exit.position.ToString("F2"));
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LaunchFxV03NativeQa.Prefab);
        Check(prefab, "LaunchFX_V03.prefab exists.");
        var owner = Object.Instantiate(prefab); owner.name = "Launch FX V03 (QA instance)";
        fx = owner.GetComponent<LaunchFxV03Presenter>();
        marine.ConfigureLaunchFxV03(fx, deflector, pad, exit);
        Check(fx.IsBound, "Patched I06MarineBindings bound V03 to the actual anchors.");
        var views = GameObject.Find("Launch FX V03 — sampled views");
        Check(views, "V03 owned views exist.");
        steam = views.GetComponentInChildren<ParticleSystem>(true); practical = views.GetComponentInChildren<Light>(true);
        var renderers = views.GetComponentsInChildren<MeshRenderer>(true);
        envelope = renderers.First(r => r.name.Contains("amber")); core = renderers.First(r => r.name.Contains("core"));
        if (LaunchFxV03NativeQa.Mode == "reduced-skip") marine.SetReducedMotion(true);

        Stage("practice and walk to the seat");
        yield return WaitFor(() => PracticeButton() != null, 8, "Practice UI");
        Submit(PracticeButton()); yield return Frames(3);
        yield return WalkTo(marine.MarineRoot.TransformPoint(new Vector3(0, .035f, -12)), .13f, 25);
        yield return WalkTo(marine.MarineRoot.TransformPoint(new Vector3(0, .035f, .7f)), .13f, 25);
        yield return Use(runtime.InteractionTargets.Single(t => t.InteractionId == InteractionIds.CockpitConsole));
        yield return WaitFor(() => marine.IsDeparturePlaying, 5, "E starts the departure");
        tracing = true;

        if (LaunchFxV03NativeQa.Mode == "full")
        {
            Stage("departure captures");
            foreach (float t in new[] { 8.8f, 9.9f, 10.6f })
            { yield return WaitFor(() => departure.PresentedSeconds >= t, 20, "playhead " + t); yield return Capture(t); }
            yield return WaitFor(() => departure.PresentedSeconds >= 11.0f, 20, "playhead 11.0");
            Stage("pause hold");
            runtime.SetPaused(true); yield return Frames(2);
            float held = departure.PresentedSeconds; float plume = fx.PresentedPlumeLength; int count = steam.particleCount; string hash = ParticleHash();
            Check(Mathf.Approximately(held, fx.PresentedSeconds), "FX sample equals the playhead when paused: " + held.ToString("F3"));
            yield return Seconds(1.2);
            Check(Mathf.Approximately(held, departure.PresentedSeconds) && Mathf.Approximately(held, fx.PresentedSeconds)
                && Mathf.Approximately(plume, fx.PresentedPlumeLength) && count == steam.particleCount && hash == ParticleHash(),
                "Paused 1.2 s: playhead, plume, particle count and every particle position held (" + count + " particles).");
            yield return Capture("paused-11.0");
            runtime.SetPaused(false); yield return Frames(3);
            Check(departure.PresentedSeconds - held < .35f, "Resume continues without catch-up: " + held.ToString("F3") + " -> " + departure.PresentedSeconds.ToString("F3"));
            Stage("departure captures 2");
            foreach (float t in new[] { 11.6f, 12.6f, 14.0f, 16.0f, 19.5f, 21.7f })
            { yield return WaitFor(() => departure.PresentedSeconds >= t, 25, "playhead " + t); yield return Capture(t); }
            yield return WaitFor(() => departure.PresentedSeconds >= 22.2f, 10, "past orbit");
            yield return Frames(2);
            Check(fx.PresentedSteamCount == 0 && steam.particleCount == 0 && !envelope.enabled && !core.enabled && !practical.enabled,
                "After orbit 22 s every V03 particle, plume and light is cleared.");
            yield return WaitFor(() => !marine.IsDeparturePlaying, 20, "departure end");
            yield return Frames(3);
            Check(fx.PresentedSteamCount == 0 && steam.particleCount == 0 && !envelope.enabled && !core.enabled && !practical.enabled,
                "After the departure ends nothing lingers.");
        }
        else
        {
            Stage("reduced effects + skip");
            yield return WaitFor(() => departure.PresentedSeconds >= 11.6f, 25, "playhead 11.6");
            yield return Capture(11.6f);
            Check(fx.PresentedSteamCount <= 40 && !practical.enabled, "Reduced effects: " + fx.PresentedSteamCount + " steam particles (<= 40), no light.");
            yield return WaitFor(() => departure.PresentedSeconds >= 12.2f, 10, "playhead 12.2");
            Check(marine.Skip(), "Skip accepted.");
            yield return Frames(3);
            Check(fx.PresentedSteamCount == 0 && steam.particleCount == 0 && !envelope.enabled && !core.enabled && !practical.enabled,
                "Skip at 12.2 s leaves zero particles, no plume and no light.");
        }
    }

    void LateUpdate()
    {
        if (!tracing || !fx) return;
        File.AppendAllText(System.IO.Path.Combine(Output, "trace.csv"), string.Format(CultureInfo.InvariantCulture,
            "{0:F3},{1:F3},{2:F3},{3},{4},{5},{6:F2},{7}\n", Now, departure ? departure.PresentedSeconds : -1, fx.PresentedPlumeLength,
            fx.PresentedSteamCount, steam ? steam.particleCount : -1, envelope && envelope.enabled, practical ? practical.intensity : -1, runtime.IsPaused));
    }
    string ParticleHash()
    {
        var buffer = new ParticleSystem.Particle[steam.particleCount]; int n = steam.GetParticles(buffer);
        double sum = 0; for (int i = 0; i < n; i++) sum += buffer[i].position.x * 1.3 + buffer[i].position.y * 7.1 + buffer[i].position.z * 3.7 + buffer[i].GetCurrentSize3D(steam).x;
        return n + ":" + sum.ToString("R", CultureInfo.InvariantCulture);
    }
    IEnumerator Capture(float t) => Capture("t" + t.ToString("00.0", CultureInfo.InvariantCulture));
    IEnumerator Capture(string name)
    {
        var texture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32); texture.Create(); var old = view.targetTexture; float aspect = view.aspect;
        var panels = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None).Where(d => d.isActiveAndEnabled && d.panelSettings && d.rootVisualElement?.panel != null).Select(d => d.panelSettings).Distinct().ToArray();
        var targets = panels.Select(p => p.targetTexture).ToArray(); var clears = panels.Select(p => p.clearColor).ToArray();
        try
        {
            view.targetTexture = texture; view.aspect = 16f / 9; foreach (var panel in panels) { panel.targetTexture = texture; panel.clearColor = false; }
            yield return Frames(2);
            float at = departure.PresentedSeconds; var active = RenderTexture.active; var image = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = texture; image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
                File.WriteAllBytes(System.IO.Path.Combine(Output, name + ".png"), image.EncodeToPNG());
                Note(string.Format(CultureInfo.InvariantCulture, "CAPTURE {0} at playhead {1:F3} plume {2:F2} m steam {3} light {4:F2} paused {5}",
                    name, at, fx.PresentedPlumeLength, fx.PresentedSteamCount, practical.enabled ? practical.intensity : 0f, runtime.IsPaused));
            }
            finally { RenderTexture.active = active; Object.Destroy(image); }
        }
        finally { view.targetTexture = old; view.aspect = aspect; for (int i = 0; i < panels.Length; i++) if (panels[i]) { panels[i].targetTexture = targets[i]; panels[i].clearColor = clears[i]; } texture.Release(); Object.Destroy(texture); }
    }
    IEnumerator Aim(Vector3 worldPoint)
    {
        double end = Now + 2.5;
        while (true)
        {
            Vector3 delta = worldPoint - player.ViewPivot.position; if (delta.sqrMagnitude < .000001f) yield break;
            float yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg, pitch = Mathf.Atan2(delta.y, new Vector2(delta.x, delta.z).magnitude) * Mathf.Rad2Deg;
            Vector2 degrees = new Vector2(Mathf.DeltaAngle(player.transform.eulerAngles.y, yaw), Mathf.Clamp(pitch, -80f, 80f) - player.PitchDegrees);
            if (degrees.sqrMagnitude < .04f) break; if (Now > end) throw new TimeoutException("Mouse look did not converge " + degrees);
            degrees = Vector2.ClampMagnitude(degrees, 70); InputSystem.QueueStateEvent(mouse, new MouseState { delta = degrees / ((PlayerInputActions)player.InputSource).MouseDegreesPerPixel }); yield return null;
        }
        InputSystem.QueueStateEvent(mouse, new MouseState()); yield return Frames(1);
    }
    IEnumerator WalkTo(Vector3 goal, float radius, double seconds)
    {
        double end = Now + seconds;
        while (Horizontal(goal, player.transform.position) > radius)
        {
            if (Now > end) throw new TimeoutException("Walk stalled at " + player.transform.position);
            Vector3 flat = goal; flat.y = player.ViewPivot.position.y; yield return Aim(flat); Keys(Key.W); yield return Seconds(.08); Keys(); yield return Frames(1);
        }
        Keys();
    }
    IEnumerator Use(InteractableTarget target)
    {
        Keys(); yield return Aim(target.transform.position); yield return Frames(2); resolver.Refresh();
        Check(resolver.CurrentTarget == target, "Interaction ray resolves " + target.InteractionId);
        int before = interactions; Keys(Key.E); yield return Frames(2); Keys(); yield return Frames(2);
        Check(interactions == before + 1 && lastInteraction.Succeeded, "E succeeds: " + lastInteraction.Explanation);
    }
    void OnInteract(CommandResult result) { interactions++; lastInteraction = result; }
    void Cleanup()
    {
        tracing = false; Keys();
        if (resolver) resolver.Interacted -= OnInteract;
        if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard); if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
        foreach (var device in disabledDevices) if (device.added) InputSystem.EnableDevice(device);
        if (originalSettings) { InputSystem.settings = originalSettings; originalSettings.hideFlags = originalFlags; } if (qaSettings) Object.Destroy(qaSettings);
    }
    Button PracticeButton() => document.rootVisualElement.Query<Button>().ToList().FirstOrDefault(b => b.text.IndexOf("Practice", StringComparison.OrdinalIgnoreCase) >= 0 && b.enabledInHierarchy && Visible(b));
    static void Submit(VisualElement e) { e.Focus(); using (var action = NavigationSubmitEvent.GetPooled()) { action.target = e; e.SendEvent(action); } }
    static bool Visible(VisualElement e) { for (var n = e; n != null; n = n.parent) if (n.resolvedStyle.display == DisplayStyle.None || n.resolvedStyle.visibility != Visibility.Visible) return false; return e?.panel != null; }
    void Keys(params Key[] keys) { if (keyboard != null && keyboard.added) InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); }
    static float Horizontal(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    static string Path(Transform t) => t ? (t.parent ? Path(t.parent) + "/" : "") + t.name : "<null>";
    static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
    static IEnumerator Seconds(double seconds) { double end = Now + seconds; while (Now < end) yield return null; }
    static IEnumerator WaitFor(Func<bool> ready, double seconds, string why) { double end = Now + seconds; while (!ready()) { if (Now > end) throw new TimeoutException(why); yield return null; } }
}
