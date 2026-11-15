// CARRY-HANDS native QA (temporary editor tool, never part of the game; delete Assets/_CarryQa after a run).
// Unity.exe -batchmode -force-device-index 1 -projectPath <own clone> -executeMethod CarryHandsNativeQa.Run  (GPU, no -nographics)
// Opens the actual saved Bootstrap, chooses Practice through the real UI, walks to a cargo pallet with real input,
// presses E, then samples the carry (look ahead / down, walking) in WORK and, by setting the suit-on flag through
// reflection (presentation check only), in EVA.
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
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class CarryHandsNativeQa
{
    const string Key = "OutOfReach.CarryHandsNativeQa.";
    public const string Bootstrap = "Assets/_OutOfReach/Scenes/Bootstrap.unity";
    public static string Output => SessionState.GetString(Key + "output", "");
    static string Stage => SessionState.GetString(Key + "stage", "");
    static CarryHandsNativeQa()
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
            string root = Environment.GetEnvironmentVariable("CARRY_QA_OUT");
            if (string.IsNullOrEmpty(root)) root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../out-of-reach-carry-logs"));
            SessionState.SetString(Key + "output", Path.Combine(root, "run-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture)));
            Directory.CreateDirectory(Output);
            SessionState.SetString(Key + "deadline", DateTime.UtcNow.AddSeconds(300).ToString("O", CultureInfo.InvariantCulture));
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
        try { SetStage("play"); new GameObject("CARRY_HANDS_NATIVE_QA_UNSAVED").AddComponent<CarryHandsNativeQaDriver>(); }
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
        Debug.Log((pass ? "CARRY_QA_PASS " : "CARRY_QA_FAIL ") + message + " output=" + Output);
        EditorApplication.Exit(pass ? 0 : 1);
    }
}

[DefaultExecutionOrder(600)]
public sealed class CarryHandsNativeQaDriver : MonoBehaviour
{
    I01MissionComposition composition; MissionRuntime runtime; FirstPersonController player; InteractionResolver resolver;
    PreflightCargoHandsPresenter cargoHands; EmbodiedBodyPresenter body; Camera view; UIDocument document;
    Keyboard keyboard; Mouse mouse; InputSettings originalSettings, qaSettings; HideFlags originalFlags;
    readonly List<InputDevice> disabledDevices = new List<InputDevice>();
    int interactions; CommandResult lastInteraction; string stage = "startup"; string suit = "WORK";
    static string Output => CarryHandsNativeQa.Output;
    static double Now => Time.realtimeSinceStartupAsDouble;
    void Check(bool pass, string message) => CarryHandsNativeQa.Check(pass, message);
    void Note(string text) => CarryHandsNativeQa.Note(Now.ToString("F3", CultureInfo.InvariantCulture) + " | " + text);
    void Stage(string value) { stage = value; CarryHandsNativeQa.SetStage(value); Note(value); }

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
        keyboard = InputSystem.AddDevice<Keyboard>("Carry QA keyboard"); mouse = InputSystem.AddDevice<Mouse>("Carry QA mouse");
        var stack = new Stack<IEnumerator>(); stack.Push(Exercise()); Exception failed = null;
        while (stack.Count > 0)
        {
            bool more = false; object yielded = null;
            try { CarryHandsNativeQa.RequireNoErrors(); more = stack.Peek().MoveNext(); if (more) yielded = stack.Peek().Current; }
            catch (Exception error) { failed = error; }
            if (failed != null) break;
            if (!more) { stack.Pop(); continue; }
            if (yielded is IEnumerator nested) { stack.Push(nested); continue; }
            yield return yielded;
        }
        Cleanup();
        CarryHandsNativeQa.Finish(failed == null, failed == null ? "CARRY-HANDS native run passed." : "Stage=" + stage + "\n" + failed);
    }

    IEnumerator Exercise()
    {
        yield return WaitFor(() => Object.FindFirstObjectByType<MissionRuntime>()?.Session != null, 25, "session startup");
        composition = Object.FindFirstObjectByType<I01MissionComposition>(); runtime = composition.Runtime; player = runtime.Player;
        resolver = player.Interaction;
        cargoHands = Object.FindFirstObjectByType<PreflightCargoHandsPresenter>(FindObjectsInactive.Include);
        body = Object.FindFirstObjectByType<EmbodiedBodyPresenter>(FindObjectsInactive.Include);
        view = Camera.main; document = Object.FindFirstObjectByType<MissionFlightBoard>().GetComponent<UIDocument>();
        Check(cargoHands && body && view, "Actual Bootstrap cargo presenter, body presenter and camera exist.");
        Note("camera fov " + view.fieldOfView.ToString("F1") + " near " + view.nearClipPlane.ToString("F3"));
        player.SendMessage("OnApplicationFocus", true, SendMessageOptions.RequireReceiver);
        resolver.Interacted += OnInteract;

        Stage("practice");
        yield return SkipStartupCards();
        yield return WaitFor(() => PracticeButton() != null, 10, "Practice UI");
        Submit(PracticeButton()); yield return Frames(5);
        yield return SkipStartupCards();
        yield return WaitFor(() => runtime.GameState.Phase == GamePhase.Planning && player.ControlAllowed, 15, "planning control");

        Stage("walk to a cargo pallet");
        var supplies = runtime.InteractionTargets.Where(t => t.InteractionId.StartsWith(InteractionIds.CargoSupplyPrefix, StringComparison.Ordinal)).ToArray();
        Check(supplies.Length > 0, "Cargo supply targets: " + supplies.Length);
        var pallet = supplies.OrderBy(t => Horizontal(t.transform.position, player.transform.position)).First();
        Vector3 away = player.transform.position - pallet.transform.position; away.y = 0;
        if (away.sqrMagnitude < .01f) away = Vector3.back;
        Vector3 goal = pallet.transform.position + away.normalized * 1.15f;
        Note("pallet " + pallet.InteractionId + " at " + pallet.transform.position.ToString("F2") + " goal " + goal.ToString("F2"));
        yield return WalkTo(goal, .18f, 40);
        // BEFORE: the previous behaviour (separate work gloves, body arms hidden), by clearing the body lease.
        FieldInfo arms = typeof(PreflightCargoHandsPresenter).GetField("carryArms", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(arms != null, "carryArms field found");
        arms.SetValue(cargoHands, null);
        yield return Use(pallet);
        yield return WaitFor(() => !string.IsNullOrEmpty(runtime.Cargo.CarriedEquipmentId), 3, "carrying");
        yield return Seconds(.6);
        Check(cargoHands.IsVisible && !cargoHands.UsesBodyArms, "before: separate gloves shown.");
        foreach (float pitch in new[] { 0f, -20f }) { yield return Pitch(pitch); yield return Frames(3); yield return Capture("before-gloves-look" + Mathf.Abs(pitch).ToString("00", CultureInfo.InvariantCulture)); }
        // AFTER: the body's own arms (what Configure binds in the game).
        cargoHands.ConfigureBody(body);
        yield return Frames(3);

        yield return Sample("work");

        Stage("drop and regrab (R06)");
        Keys(); yield return Frames(2);
        Vector3 back = pallet.transform.position + away.normalized * 1.15f;
        yield return WalkTo(back, .18f, 30);
        yield return Use(pallet);
        yield return WaitFor(() => string.IsNullOrEmpty(runtime.Cargo.CarriedEquipmentId), 3, "dropped");
        yield return Frames(4);
        Check(!cargoHands.IsCarryShown && !cargoHands.IsVisible && float.IsNaN(cargoHands.BodyReachShortfallMetres),
            "drop: crate, gloves and arm lease all released (no floating parts).");
        yield return Pitch(-30f); yield return Frames(3); yield return Capture("work-dropped-look30");
        yield return Use(pallet);
        yield return WaitFor(() => !string.IsNullOrEmpty(runtime.Cargo.CarriedEquipmentId), 3, "regrabbed");
        yield return Seconds(.6);
        Check(cargoHands.UsesBodyArms && !cargoHands.IsVisible && cargoHands.BodyReachShortfallMetres < .01f,
            "regrab: the body's arms hold the crate again (shortfall " + cargoHands.BodyReachShortfallMetres.ToString("F4") + " m).");
        yield return Pitch(-30f); yield return Frames(3); yield return Capture("work-regrab-look30");

        Stage("EVA outfit (presentation check: suit-on flag set by reflection)");
        object eva = runtime.EvaStatus;
        PropertyInfo prop = eva.GetType().GetProperty("IsSuitOn", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Check(prop != null && prop.GetSetMethod(true) != null, "EvaOperations.IsSuitOn setter found on " + eva.GetType().Name);
        prop.GetSetMethod(true).Invoke(eva, new object[] { true });
        suit = "EVA";
        yield return WaitFor(() => body.IsEvaVisible, 3, "EVA body visible");
        yield return Seconds(.4);
        yield return Sample("eva");
    }

    IEnumerator Sample(string tag)
    {
        Stage("sample " + tag);
        Check(cargoHands.IsCarryShown, tag + ": the crate is shown.");
        Check(cargoHands.UsesBodyArms && !cargoHands.IsVisible, tag + ": the body's arms hold it; separate gloves hidden.");
        Check(body.VisibleArms == EmbodiedBodyPresenter.BodyArmVisibility.Both, tag + ": both body arms visible (" + body.VisibleArms + ").");
        foreach (float pitch in new[] { 0f, -15f, -30f, -50f })
        {
            yield return Pitch(pitch); yield return Frames(3);
            Measure(tag + " pitch " + pitch);
            yield return Capture(tag + "-look" + Mathf.Abs(pitch).ToString("00", CultureInfo.InvariantCulture));
        }
        yield return Pitch(-20f); yield return Frames(3);
        yield return SideView(tag + "-side");
        Keys(Key.W);
        for (int i = 0; i < 6; i++) { yield return Seconds(.22); Measure(tag + " walk " + i); yield return Capture(tag + "-walk" + i); }
        Keys(); yield return Frames(2);
        Check(cargoHands.BodyReachShortfallMetres < .01f, tag + ": both wrists reach their handle targets (shortfall "
            + cargoHands.BodyReachShortfallMetres.ToString("F4") + " m).");
    }

    void Measure(string label)
    {
        Transform lw = body.GetVisibleBone("wrist.L"), rw = body.GetVisibleBone("wrist.R");
        Transform ls = body.GetVisibleBone("upperarm01.L"), rs = body.GetVisibleBone("upperarm01.R");
        var grips = cargoHands.SettlingRoot.GetComponentsInChildren<Transform>(true).Where(t => t.name.EndsWith("hand grip frame")).ToArray();
        string g = string.Join(" ", grips.Select(t => t.name.Split(' ')[0] + "=" + view.transform.InverseTransformPoint(t.position).ToString("F3")));
        Note(string.Format(CultureInfo.InvariantCulture, "{0}: suit {1} shortfall {2:F4} m | eye-local wristL {3} wristR {4} shoulderL {5} shoulderR {6} | grips {7}",
            label, suit, cargoHands.BodyReachShortfallMetres, view.transform.InverseTransformPoint(lw.position).ToString("F3"),
            view.transform.InverseTransformPoint(rw.position).ToString("F3"), view.transform.InverseTransformPoint(ls.position).ToString("F3"),
            view.transform.InverseTransformPoint(rs.position).ToString("F3"), g));
    }

    /// <summary>R01 startup cards (VisualElement "startup-cards") cover the menu first; like a player, wait and press Space.</summary>
    IEnumerator SkipStartupCards()
    {
        double begin = Now; bool pressed = false;
        while (StartupCardsShown() && Now - begin < 40)
        {
            if (!pressed && Now - begin > 1.0) { pressed = true; Keys(Key.Space); yield return Frames(2); Keys(); Note("startup cards shown; pressed Space"); }
            yield return null;
        }
        if (StartupCardsShown()) throw new TimeoutException("startup cards still shown");
    }
    bool StartupCardsShown()
    {
        foreach (var doc in Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            if (doc && doc.isActiveAndEnabled && doc.rootVisualElement != null)
            {
                var e = doc.rootVisualElement.Q<VisualElement>("startup-cards");
                if (e != null && Visible(e)) return true;
            }
        return false;
    }
    IEnumerator SideView(string name)
    {
        var go = new GameObject("QA side camera"); var cam = go.AddComponent<Camera>();
        cam.fieldOfView = 40f; cam.nearClipPlane = .05f; cam.cullingMask = ~0;
        Vector3 chest = player.transform.position + Vector3.up * 1.15f + player.transform.forward * .25f;
        go.transform.position = chest + player.transform.right * 1.9f + player.transform.forward * .9f + Vector3.up * .35f;
        go.transform.rotation = Quaternion.LookRotation(chest - go.transform.position, Vector3.up);
        var texture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32); texture.Create();
        cam.targetTexture = texture; cam.aspect = 16f / 9;
        yield return Frames(2);
        cam.Render();
        var active = RenderTexture.active; var image = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
        RenderTexture.active = texture; image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply(); RenderTexture.active = active;
        File.WriteAllBytes(System.IO.Path.Combine(Output, name + ".png"), image.EncodeToPNG());
        Object.Destroy(image); cam.targetTexture = null; texture.Release(); Object.Destroy(texture); Object.Destroy(go);
        Note("CAPTURE " + name + " (third-person side check camera)");
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
        var texture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32); texture.Create(); var old = view.targetTexture; float aspect = view.aspect;
        var panels = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None).Where(d => d.isActiveAndEnabled && d.panelSettings && d.rootVisualElement?.panel != null).Select(d => d.panelSettings).Distinct().ToArray();
        var targets = panels.Select(p => p.targetTexture).ToArray(); var clears = panels.Select(p => p.clearColor).ToArray();
        try
        {
            view.targetTexture = texture; view.aspect = 16f / 9; foreach (var panel in panels) { panel.targetTexture = texture; panel.clearColor = false; }
            yield return Frames(2);
            var active = RenderTexture.active; var image = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = texture; image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
                File.WriteAllBytes(System.IO.Path.Combine(Output, name + ".png"), image.EncodeToPNG());
                Note("CAPTURE " + name + " pitch " + player.PitchDegrees.ToString("F1") + " pos " + player.transform.position.ToString("F2"));
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
        Check(resolver.CurrentTarget == target, "Interaction ray resolves " + target.InteractionId + " (current " + (resolver.CurrentTarget ? resolver.CurrentTarget.InteractionId : "none") + ")");
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
    Button PracticeButton()
    {
        foreach (var doc in Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
        {
            if (!doc || !doc.isActiveAndEnabled || doc.rootVisualElement == null) continue;
            var found = doc.rootVisualElement.Query<Button>().ToList().FirstOrDefault(x => x.text != null && x.text.IndexOf("Practice", StringComparison.OrdinalIgnoreCase) >= 0 && x.enabledInHierarchy && Visible(x));
            if (found != null) return found;
        }
        return null;
    }
    static void Submit(VisualElement e) { e.Focus(); using (var action = NavigationSubmitEvent.GetPooled()) { action.target = e; e.SendEvent(action); } }
    static bool Visible(VisualElement e) { for (var n = e; n != null; n = n.parent) if (n.resolvedStyle.display == DisplayStyle.None || n.resolvedStyle.visibility != Visibility.Visible) return false; return e?.panel != null; }
    void Keys(params Key[] keys) { if (keyboard != null && keyboard.added) InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); }
    static float Horizontal(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
    static IEnumerator Seconds(double seconds) { double end = Now + seconds; while (Now < end) yield return null; }
    static IEnumerator WaitFor(Func<bool> ready, double seconds, string why) { double end = Now + seconds; while (!ready()) { if (Now > end) throw new TimeoutException(why); yield return null; } }
}
