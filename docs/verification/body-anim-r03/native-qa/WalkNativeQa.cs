// R03 WALK native QA (temporary editor tool, never part of the game; copy with its asmdef to Assets/_WalkQa/Editor/,
// run, then delete Assets/_WalkQa). Based on Apoliak's CarryHandsNativeQa (carry-hands-v1).
// Unity.exe -batchmode -projectPath <own clone> -executeMethod WalkNativeQa.Run   (GPU, no -nographics)
// Opens the actual saved Bootstrap, chooses Practice through the real UI and walks with real keyboard input:
// forward, backward, strafe, sprint, then picks up a crate and walks with it. Every frame it records the body
// presenter's real gait state and the world slide of each planted foot; it captures first-person and side frames.
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
using OutOfReach.Presentation.Cargo;
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
public static class WalkNativeQa
{
    const string Key = "OutOfReach.WalkNativeQa.";
    public const string Bootstrap = "Assets/_OutOfReach/Scenes/Bootstrap.unity";
    public static string Output => SessionState.GetString(Key + "output", "");
    static string Stage => SessionState.GetString(Key + "stage", "");
    static WalkNativeQa()
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
            string root = Environment.GetEnvironmentVariable("WALK_QA_OUT");
            if (string.IsNullOrEmpty(root)) root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../out-of-reach-walk-logs"));
            SessionState.SetString(Key + "output", root);
            Directory.CreateDirectory(Output);
            SessionState.SetString(Key + "deadline", DateTime.UtcNow.AddSeconds(420).ToString("O", CultureInfo.InvariantCulture));
            SessionState.SetBool(Key + "async", ShaderUtil.allowAsyncCompilation); ShaderUtil.allowAsyncCompilation = false;
            Note("GPU " + SystemInfo.graphicsDeviceName + " | " + SystemInfo.graphicsDeviceType);
            SetStage("opening actual saved Bootstrap");
            EditorSceneManager.OpenScene(Bootstrap, OpenSceneMode.Single);
            SetStage("entering"); EditorApplication.isPlaying = true;
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }
    static void OnPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || Stage != "entering") return;
        try { SetStage("play"); new GameObject("WALK_NATIVE_QA_UNSAVED").AddComponent<WalkNativeQaDriver>(); }
        catch (Exception error) { Finish(false, error.ToString()); }
    }
    static void Watchdog()
    {
        if (Stage.Length == 0) return;
        if (DateTime.TryParse(SessionState.GetString(Key + "deadline", ""), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime deadline)
            && DateTime.UtcNow > deadline) Finish(false, "300 s watchdog expired at stage " + Stage);
    }
    static void OnLog(string message, string stack, LogType type)
    {
        if (Stage.Length == 0 || (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)) return;
        string entry = type + ": " + message + "\n" + stack + "\n";
        if (stack.Contains("UnityEditor.Search")) return;
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
        Debug.Log((pass ? "WALK_QA_PASS " : "WALK_QA_FAIL ") + message + " output=" + Output);
        EditorApplication.Exit(pass ? 0 : 1);
    }
}

[DefaultExecutionOrder(600)]
public sealed class WalkNativeQaDriver : MonoBehaviour
{
    I01MissionComposition composition; MissionRuntime runtime; FirstPersonController player; InteractionResolver resolver;
    PreflightCargoHandsPresenter cargoHands; EmbodiedBodyPresenter body; Camera view; UIDocument document;
    Keyboard keyboard; Mouse mouse; InputSettings originalSettings, qaSettings; HideFlags originalFlags;
    readonly List<InputDevice> disabledDevices = new List<InputDevice>();
    int interactions; CommandResult lastInteraction; string stage = "startup"; bool skipCarry;
    static double Now => Time.realtimeSinceStartupAsDouble;
    void Check(bool pass, string message) => WalkNativeQa.Check(pass, message);
    void Note(string text) => WalkNativeQa.Note(Now.ToString("F3", CultureInfo.InvariantCulture) + " | " + text);
    void Stage(string value) { stage = value; WalkNativeQa.SetStage(value); Note(value); }

    // Per-frame sampling (LateUpdate at order 600, after the body presenter at 200 and the carry arms at 265).
    bool sampling; string sampleTag; int frames, gaitFrames, stillFrames, hitchFrames; float cycles, sampleSeconds, lastPhase; float slideL, slideR, runL, runR, hipMin, hipMax, worstSlide;
    bool hadL, hadR; Vector3 lastL, lastR; readonly List<string> rows = new List<string>();

    IEnumerator Start()
    {
        Application.runInBackground = true;
        QualitySettings.vSyncCount = 0; Application.targetFrameRate = 60;   // a real game frame time, not batch mode's 1000 fps
        originalSettings = InputSystem.settings; originalFlags = originalSettings.hideFlags;
        if (originalFlags == HideFlags.HideAndDontSave) originalSettings.hideFlags = HideFlags.DontSave;
        qaSettings = Object.Instantiate(originalSettings); qaSettings.hideFlags = HideFlags.DontSave;
        qaSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        qaSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings = qaSettings;
        foreach (var device in InputSystem.devices.ToArray())
            if (device.enabled && (device is Mouse || device is Keyboard || device is Gamepad)) { disabledDevices.Add(device); InputSystem.DisableDevice(device); }
        keyboard = InputSystem.AddDevice<Keyboard>("Walk QA keyboard"); mouse = InputSystem.AddDevice<Mouse>("Walk QA mouse");
        var stack = new Stack<IEnumerator>(); stack.Push(Exercise()); Exception failed = null;
        while (stack.Count > 0)
        {
            bool more = false; object yielded = null;
            try { WalkNativeQa.RequireNoErrors(); more = stack.Peek().MoveNext(); if (more) yielded = stack.Peek().Current; }
            catch (Exception error) { failed = error; }
            if (failed != null) break;
            if (!more) { stack.Pop(); continue; }
            if (yielded is IEnumerator nested) { stack.Push(nested); continue; }
            yield return yielded;
        }
        Cleanup();
        File.WriteAllLines(Path.Combine(WalkNativeQa.Output, "frames.tsv"), rows);
        WalkNativeQa.Finish(failed == null, failed == null ? "R03 walk native run passed." : "Stage=" + stage + "\n" + failed);
    }

    IEnumerator Exercise()
    {
        yield return WaitFor(() => Object.FindFirstObjectByType<MissionRuntime>()?.Session != null, 25, "session startup");
        composition = Object.FindFirstObjectByType<I01MissionComposition>(); runtime = composition.Runtime; player = runtime.Player;
        resolver = player.Interaction;
        cargoHands = Object.FindFirstObjectByType<PreflightCargoHandsPresenter>(FindObjectsInactive.Include);
        body = Object.FindFirstObjectByType<EmbodiedBodyPresenter>(FindObjectsInactive.Include);
        view = Camera.main; document = Object.FindFirstObjectByType<MissionFlightBoard>().GetComponent<UIDocument>();
        Check(body && view, "Actual Bootstrap body presenter and camera exist.");
        player.SendMessage("OnApplicationFocus", true, SendMessageOptions.RequireReceiver);
        resolver.Interacted += OnInteract;
        rows.Add("tag\tt\tdt\tspeed\tgaitActive\tgaitWeight\tgaitSpeed\tgaitPhase\tplantedL\tplantedR\tslideRunL_mm\tslideRunR_mm\thipY\tgrounded\tcamLocalY");

        Stage("practice");
        yield return WaitFor(() => PracticeButton() != null, 10, "Practice UI");
        Submit(PracticeButton()); yield return Frames(5);
        yield return WaitFor(() => runtime.GameState.Phase == GamePhase.Planning && player.ControlAllowed, 15, "planning control");
        yield return Seconds(.5);
        Check(body.IsVisible, "body visible in planning");

        Vector3 start = player.transform.position;
        // Open floor: face away from the nearest wall by turning towards the longest clear ray.
        yield return FaceOpenFloor();
        yield return Pitch(-35f);
        yield return Pass("forward", new[] { Key.W }, 1.6);
        yield return Pass("backward", new[] { Key.S }, 1.2);
        yield return Pass("strafe", new[] { Key.D }, 1.2);
        yield return FaceOpenFloor(); yield return Pitch(-35f);
        yield return Pass("sprint", new[] { Key.W, Key.LeftShift }, 1.2);
        yield return Pass("stop", new Key[0], .8);
        yield return Pass("turn-in-place", new Key[0], 1.5, 120f);
        yield return Pass("settle", new Key[0], 1.0);
        yield return FaceOpenFloor(); yield return Pitch(-35f);
        yield return Pass("walk-curve", new[] { Key.W }, 1.5, 60f);
        yield return Pass("stop2", new Key[0], .8);
        // What the player sees of their own legs: a steep look-down, standing and walking.
        yield return FaceOpenFloor(); yield return Pitch(-65f);
        yield return Capture("lookdown65-standing");
        yield return Pass("lookdown-walk", new[] { Key.W }, 1.4);
        yield return Pass("stop3", new Key[0], .8);
        yield return Capture("lookdown65-after-stop");

        if (cargoHands)
        {
            Stage("carry");
            yield return Pitch(0f);
            yield return WalkTo(start, .3f, 30);
            var supplies = runtime.InteractionTargets.Where(t => t.InteractionId.StartsWith(InteractionIds.CargoSupplyPrefix, StringComparison.Ordinal)).ToArray();
            if (supplies.Length > 0)
            {
                var pallet = supplies.OrderBy(t => Horizontal(t.transform.position, player.transform.position)).First();
                Vector3 away = player.transform.position - pallet.transform.position; away.y = 0;
                if (away.sqrMagnitude < .01f) away = Vector3.back;
                yield return WalkTo(pallet.transform.position + away.normalized * 1.15f, .18f, 40);
                yield return Use(pallet);
                if (skipCarry) yield break;
                yield return WaitFor(() => !string.IsNullOrEmpty(runtime.Cargo.CarriedEquipmentId), 3, "carrying");
                yield return Seconds(.6);
                Note("carry: crate shown " + cargoHands.IsCarryShown + " body arms " + cargoHands.UsesBodyArms + " gloves " + cargoHands.IsVisible
                     + " arms " + body.VisibleArms);
                yield return FaceOpenFloor();
                foreach (float p in new[] { -15f, -35f }) { yield return Pitch(p); yield return Frames(3); yield return Capture("carry-look" + Mathf.Abs(p).ToString("00")); }
                yield return Pass("carry-walk", new[] { Key.W }, 1.4);
                Note("carry shortfall m " + cargoHands.BodyReachShortfallMetres.ToString("F4"));
            }
        }
    }

    IEnumerator Pass(string tag, Key[] keys, double seconds, float yawDegreesPerSecond = 0f)
    {
        Stage("pass " + tag);
        BeginSample(tag);
        Keys(keys);
        // Measure first with an undisturbed 60 fps loop; the captures stall the loop, so they come afterwards.
        double end = Now + seconds;
        while (Now < end)
        {
            if (yawDegreesPerSecond != 0f)
                InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(yawDegreesPerSecond * Time.deltaTime, 0f) / ((PlayerInputActions)player.InputSource).MouseDegreesPerPixel });
            yield return null;
        }
        if (yawDegreesPerSecond != 0f) InputSystem.QueueStateEvent(mouse, new MouseState());
        sampling = false;
        if (keys.Length > 0)
            for (int shot = 0; shot < 4; shot++)
            {
                yield return Seconds(.3 + shot * .07);
                yield return Capture(tag + "-eye" + shot); yield return SideView(tag + "-side" + shot);
                yield return Frames(3);
            }
        Keys(); yield return Frames(2);
        string summary = string.Format(CultureInfo.InvariantCulture,
            "SUMMARY {0}: frames {1} gaitActive {2} still {3} hitch {7} | worst planted slide mm L {4:F1} R {5:F1} | pelvis range cm {6:F1} | steps/min {8:F0}",
            tag, frames, gaitFrames, stillFrames, slideL * 1000f, slideR * 1000f, (hipMax - hipMin) * 100f, hitchFrames, sampleSeconds > 0f ? 120f * cycles / sampleSeconds : 0f);
        Note(summary);
        worstSlide = Mathf.Max(worstSlide, Mathf.Max(slideL, slideR));
    }

    void BeginSample(string tag)
    {
        sampleTag = tag; sampling = true; frames = gaitFrames = stillFrames = hitchFrames = 0; cycles = sampleSeconds = 0f; lastPhase = float.NaN; slideL = slideR = runL = runR = 0f; hadL = hadR = false;
        hipMin = float.MaxValue; hipMax = float.MinValue;
    }

    static readonly BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static T Field<T>(object o, string name) { var f = o.GetType().GetField(name, Any); return f == null ? default : (T)f.GetValue(o); }

    void LateUpdate()
    {
        if (!sampling || !body || !body.IsVisible) return;
        // A capture frame of this tool stalls the loop; the presenter re-plants after a stall, so do not count it as slide.
        if (Time.deltaTime > .05f) { hadL = hadR = false; hitchFrames++; return; }
        frames++;
        ModelGaitRigProbe(out bool plantedL, out bool plantedR, out float pitchL, out float pitchR);
        Transform fl = body.GetVisibleBone("foot.L"), fr = body.GetVisibleBone("foot.R"), hip = body.GetVisibleBone("root");
        Track(fl, plantedL && Mathf.Abs(pitchL) < .5f, ref hadL, ref lastL, ref runL, ref slideL);
        Track(fr, plantedR && Mathf.Abs(pitchR) < .5f, ref hadR, ref lastR, ref runR, ref slideR);
        float phase = body.GaitPhase;
        if (!float.IsNaN(phase) && !float.IsNaN(lastPhase)) cycles += Mathf.Repeat(phase - lastPhase, 1f);
        lastPhase = phase; sampleSeconds += Time.deltaTime;
        bool active = Field<bool>(body, "gaitActive");
        if (active) gaitFrames++;
        var cc = player.GetComponent<CharacterController>();
        Vector3 v = cc.velocity; v.y = 0f;
        if (v.magnitude > .2f && !active) stillFrames++;
        if (hip) { hipMin = Mathf.Min(hipMin, hip.position.y - player.transform.position.y); hipMax = Mathf.Max(hipMax, hip.position.y - player.transform.position.y); }
        rows.Add(string.Join("\t", sampleTag, Now.ToString("F3", CultureInfo.InvariantCulture), Time.deltaTime.ToString("F4", CultureInfo.InvariantCulture),
            v.magnitude.ToString("F2", CultureInfo.InvariantCulture), active ? "1" : "0",
            Field<float>(body, "gaitWeight").ToString("F2", CultureInfo.InvariantCulture), Field<float>(body, "gaitSpeed").ToString("F2", CultureInfo.InvariantCulture),
            Field<float>(body, "gaitPhase").ToString("F3", CultureInfo.InvariantCulture), plantedL ? "1" : "0", plantedR ? "1" : "0",
            (runL * 1000f).ToString("F1", CultureInfo.InvariantCulture), (runR * 1000f).ToString("F1", CultureInfo.InvariantCulture),
            hip ? (hip.position.y - player.transform.position.y).ToString("F3", CultureInfo.InvariantCulture) : "",
            cc.isGrounded ? "1" : "0", view.transform.localPosition.y.ToString("F4", CultureInfo.InvariantCulture)));
    }

    void ModelGaitRigProbe(out bool plantedL, out bool plantedR, out float pitchL, out float pitchR)
    {
        plantedL = plantedR = false; pitchL = pitchR = 0f;
        object rig = typeof(EmbodiedBodyPresenter).GetProperty("ActiveGroundRig", Any)?.GetValue(body);
        if (rig == null) return;
        MethodInfo planted = rig.GetType().GetMethod("IsPlanted", Any), pitch = rig.GetType().GetMethod("ShownPitch", Any);
        plantedL = (bool)planted.Invoke(rig, new object[] { true }); plantedR = (bool)planted.Invoke(rig, new object[] { false });
        pitchL = (float)pitch.Invoke(rig, new object[] { true }); pitchR = (float)pitch.Invoke(rig, new object[] { false });
    }

    static void Track(Transform bone, bool flat, ref bool had, ref Vector3 last, ref float run, ref float worst)
    {
        if (!bone) return;
        Vector3 p = bone.position;
        if (flat && had) { run += new Vector2(p.x - last.x, p.z - last.z).magnitude; worst = Mathf.Max(worst, run); }
        else run = 0f;
        had = flat; last = p;
    }

    IEnumerator FaceOpenFloor()
    {
        Vector3 origin = player.transform.position + Vector3.up * .5f;
        float best = -1f; Vector3 bestDir = player.transform.forward;
        for (int i = 0; i < 24; i++)
        {
            Vector3 d = Quaternion.AngleAxis(i * 15f, Vector3.up) * Vector3.forward;
            float dist = Physics.SphereCast(origin, .3f, d, out RaycastHit hit, 12f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ? hit.distance : 12f;
            if (dist > best) { best = dist; bestDir = d; }
        }
        Note("open floor " + best.ToString("F1") + " m towards " + bestDir.ToString("F2"));
        Vector3 aim = player.ViewPivot.position + bestDir * 5f;
        yield return Aim(aim);
    }

    IEnumerator SideView(string name)
    {
        var go = new GameObject("QA side camera"); var cam = go.AddComponent<Camera>();
        cam.fieldOfView = 40f; cam.nearClipPlane = .05f; cam.cullingMask = ~0;
        Vector3 hips = player.transform.position + Vector3.up * .8f;
        go.transform.position = hips + player.transform.right * 2.6f + Vector3.up * .2f;
        go.transform.rotation = Quaternion.LookRotation(hips - go.transform.position, Vector3.up);
        var texture = new RenderTexture(640, 360, 24, RenderTextureFormat.ARGB32); texture.Create();
        cam.targetTexture = texture; cam.aspect = 16f / 9;
        cam.Render();
        var active = RenderTexture.active; var image = new Texture2D(640, 360, TextureFormat.RGBA32, false);
        RenderTexture.active = texture; image.ReadPixels(new Rect(0, 0, 640, 360), 0, 0); image.Apply(); RenderTexture.active = active;
        File.WriteAllBytes(Path.Combine(WalkNativeQa.Output, name + ".png"), image.EncodeToPNG());
        Object.Destroy(image); cam.targetTexture = null; texture.Release(); Object.Destroy(texture); Object.Destroy(go);
        yield break;
    }
    IEnumerator Pitch(float degrees)
    {
        double end = Now + 2.5;
        while (true)
        {
            float delta = degrees - player.PitchDegrees;
            if (Mathf.Abs(delta) < .25f) break; if (Now > end) throw new TimeoutException("Pitch did not converge " + delta);
            float step = Mathf.Clamp(delta, -60f, 60f);
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(0, step) / ((PlayerInputActions)player.InputSource).MouseDegreesPerPixel }); yield return null;
        }
        InputSystem.QueueStateEvent(mouse, new MouseState()); yield return Frames(1);
    }
    IEnumerator Capture(string name)
    {
        var texture = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32); texture.Create(); var old = view.targetTexture; float aspect = view.aspect;
        try
        {
            view.targetTexture = texture; view.aspect = 16f / 9;
            view.Render();
            var active = RenderTexture.active; var image = new Texture2D(960, 540, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = texture; image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(WalkNativeQa.Output, name + ".png"), image.EncodeToPNG());
            }
            finally { RenderTexture.active = active; Object.Destroy(image); }
        }
        finally { view.targetTexture = old; view.aspect = aspect; texture.Release(); Object.Destroy(texture); }
        yield break;
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
        if (resolver.CurrentTarget != target) { Note("SKIP carry: interaction ray resolves " + (resolver.CurrentTarget ? resolver.CurrentTarget.InteractionId : "none")); skipCarry = true; yield break; }
        int before = interactions; Keys(Key.E); yield return Frames(2); Keys(); yield return Frames(2);
        Check(interactions == before + 1 && lastInteraction.Succeeded, "E succeeds: " + lastInteraction.Explanation);
    }
    void OnInteract(CommandResult result) { interactions++; lastInteraction = result; }
    void Cleanup()
    {
        Keys();
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
    static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
    static IEnumerator Seconds(double seconds) { double end = Now + seconds; while (Now < end) yield return null; }
    static IEnumerator WaitFor(Func<bool> ready, double seconds, string why) { double end = Now + seconds; while (!ready()) { if (Now > end) throw new TimeoutException(why); yield return null; } }
}
