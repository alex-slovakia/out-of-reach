using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using OutOfReach.Composition;
using OutOfReach.Presentation.Cinematics;
using OutOfReach.Presentation.UI;
using OutOfReach.Contracts;
using OutOfReach.Player;
using OutOfReach.Core;
using OutOfReach.Core.Runtime;
using OutOfReach.Interaction;
using OutOfReach.Presentation.Cargo;

using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;
using System.Reflection;
using OutOfReach.Presentation.MarineOcean;
using OutOfReach.Presentation.Hands;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Temporary read-only actual saved Bootstrap integration probe; no authoring helper or gameplay substitute.
[InitializeOnLoad]
public static class SurfacePushNativeQa
{
    const string Key = "OutOfReach.SurfacePushNativeQa.";
    public const string Bootstrap = "Assets/_OutOfReach/Scenes/Bootstrap.unity";
    public const string RigPath = "Assets/_OutOfReach/Scripts/Player/PlayerRig.prefab";
    public const string FixtureName = "SURFACE_PUSH_NATIVE_QA_UNSAVED";
    public const string MarineRootName = "I06 isolated marine prologue";
    public static readonly Vector3 Origin = new Vector3(10000, 100, 0);
    public static string Output => SessionState.GetString(Key + "output", Path.GetFullPath(".local/surface-push-native-qa/failed-start"));
    static string Stage => SessionState.GetString(Key + "stage", "");
    [Serializable] public sealed class CheckEntry { public string utc, stage, message; public bool pass; }
    [Serializable] public sealed class CheckList { public CheckEntry[] checks; }
    [Serializable] sealed class FileEntry { public string path, sha256; }
    [Serializable] sealed class Files { public FileEntry[] files; }
    [Serializable] sealed class Result { public bool pass; public string message, utc, scope; public int checks; }
#pragma warning disable 0649
    [Serializable] public sealed class Layout { public MaterialSpec[] materials; }
    [Serializable] public sealed class MaterialSpec
    { public string name, group, baseMap, normalMap, metallicMap, occlusionMap; public float[] color, emission; public float metallic, smoothness, normalStrength, emissionStrength; }
#pragma warning restore 0649
    static SurfacePushNativeQa()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
        EditorApplication.update += Watchdog;
        Application.logMessageReceived += OnLog;
    }
    [Serializable] public sealed class ConsoleBaseline { public string parentPath, cameraParentPath; public Vector3 position, scale; public Quaternion rotation; }
    public static T Read<T>(object host,string name)=>(T)host.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(host);
    public static string Hierarchy(Transform t)=>t ? (t.parent ? Hierarchy(t.parent)+"/" : "")+t.name : "<root>";
    public static void Run()
    {
        try
        {
            string project=Path.GetFullPath(Path.Combine(Application.dataPath,"..")).Replace('\\','/');
            if(!Application.isBatchMode || project.IndexOf("/.local/",StringComparison.OrdinalIgnoreCase)<0 || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Start in batch Edit mode in the isolated .local checkout only.");
            if(Application.unityVersion!="6000.3.28f1" || SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)
                throw new InvalidOperationException("Exact Unity6000.3.28f1 and real GPU required.");
            SessionState.SetString(Key+"output",Path.Combine(project,".local/surface-push-native-qa",DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ")));
            Directory.CreateDirectory(Output);SessionState.SetInt(Key+"checks",0);SessionState.SetString(Key+"errors","");
            SessionState.SetString(Key+"deadline",DateTime.UtcNow.AddSeconds(150).ToString("O",CultureInfo.InvariantCulture));SetStage("opening actual saved Bootstrap");
            SessionState.SetBool(Key+"async",ShaderUtil.allowAsyncCompilation);ShaderUtil.allowAsyncCompilation=false;
            File.WriteAllText(Path.Combine(Output,"scope.txt"),
                "Actual saved Bootstrap: real Practice UI, marine W boarding, E departure and normal arrival. Neutral Space from floor, passive actual ceiling collision, then level-view neutral Space via actual hand bridge.\n"
                +"No pose/rotation writes, direct push, force, grant, fake physics, time-scale/Core clock step, session replacement or full mission route. All existing Assets hashes protected.\n");
            SnapshotFiles("assets-before.json");
            EditorSceneManager.OpenScene(Bootstrap,OpenSceneMode.Single);
            Check(Object.FindFirstObjectByType<I01MissionComposition>(),"Saved root composition is present.");
            VerifyProtected();SetStage("entering");EditorApplication.isPlaying=true;
        }
        catch(Exception error){Finish(false,error.ToString());}
    }
    static void OnPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || Stage != "entering") return;
        try { SetStage("native actual Bootstrap"); new GameObject(FixtureName).AddComponent<SurfacePushNativeQaDriver>(); }
        catch (Exception error) { Finish(false, error.ToString()); }
    }
    static void Watchdog()
    {
        if (Stage.Length == 0) return;
        if (DateTime.TryParse(SessionState.GetString(Key + "deadline", ""), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime deadline)
            && DateTime.UtcNow > deadline) Finish(false, "150-second watchdog expired; see completed checks and captures.");
    }
    static void OnLog(string message, string stack, LogType type)
    {
        if (Stage.Length == 0 || (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)) return;
        string entry = type + ": " + message + "\n" + stack + "\n";
        if (type == LogType.Exception && message.StartsWith("ArgumentOutOfRangeException:", StringComparison.Ordinal)
            && stack.Contains("UnityEditor.Search.SearchDatabase") && stack.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup") && !stack.Contains("OutOfReach"))
        { File.AppendAllText(Path.Combine(Output, "editor-environment-errors.txt"), entry); return; }
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
        var entry = new CheckEntry { utc = DateTime.UtcNow.ToString("O"), stage = Stage, pass = condition, message = message };
        File.AppendAllText(Path.Combine(Output, "checks.jsonl"), JsonUtility.ToJson(entry) + "\n");
        File.AppendAllText(Path.Combine(Output, "checks.txt"), (condition ? "PASS " : "FAIL ") + Stage + " | " + message + "\n");
        SessionState.SetInt(Key + "checks", SessionState.GetInt(Key + "checks", 0) + 1);
        if (!condition) throw new InvalidOperationException(message);
    }
    public static void Note(string message) => File.AppendAllText(Path.Combine(Output, "observations.txt"), message + "\n");
    static IEnumerable<string> AssetPaths() => Directory.GetFiles("Assets", "*", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/')).OrderBy(p => p, StringComparer.Ordinal);
    static string Hash(string path) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }
    static void SnapshotFiles(string name)
    {
        File.WriteAllText(Path.Combine(Output, name), JsonUtility.ToJson(new Files { files = AssetPaths().Select(p => new FileEntry { path = p, sha256 = Hash(p) }).ToArray() }, true));
    }
    public static void VerifyProtected()
    {
        var before=JsonUtility.FromJson<Files>(File.ReadAllText(Path.Combine(Output,"assets-before.json"))).files;
        var current=AssetPaths().ToArray();var original=new HashSet<string>(before.Select(x=>x.path),StringComparer.Ordinal);
        foreach(var item in before) if(!File.Exists(item.path)||Hash(item.path)!=item.sha256) throw new InvalidOperationException("Read-only saved-scene probe changed an asset: "+item.path);
        if(current.Any(p=>!original.Contains(p)))throw new InvalidOperationException("Read-only probe created Assets files: "+string.Join(", ",current.Where(p=>!original.Contains(p))));
        Check(before.Any(f=>f.path==Bootstrap),"Saved Bootstrap and every pre-existing Assets file retain exact source hashes.");
    }
    public static void Finish(bool pass, string message)
    {
        try { if (File.Exists(Path.Combine(Output, "assets-before.json"))) VerifyProtected(); RequireNoErrors(); }
        catch (Exception error) { pass = false; message += "\n" + error; }
        SessionState.SetString(Key + "stage", ""); ShaderUtil.allowAsyncCompilation = SessionState.GetBool(Key + "async", true);
        Directory.CreateDirectory(Output);
        string checksPath = Path.Combine(Output, "checks.jsonl");
        var entries = File.Exists(checksPath) ? File.ReadAllLines(checksPath).Where(x => x.Length > 0).Select(JsonUtility.FromJson<CheckEntry>).ToArray() : Array.Empty<CheckEntry>();
        File.WriteAllText(Path.Combine(Output, "checks.json"), JsonUtility.ToJson(new CheckList { checks = entries }, true));
        File.WriteAllText(Path.Combine(Output, pass ? "PASS.txt" : "FAIL.txt"), message);
        File.WriteAllText(Path.Combine(Output, "result.json"), JsonUtility.ToJson(new Result { pass = pass, message = message,
            utc = DateTime.UtcNow.ToString("O"), checks = entries.Length, scope = "Actual saved Bootstrap neutral floor/ceiling Space smoke; no full mission acceptance. GPU images require visual review." }, true));
        Debug.Log((pass ? "SURFACE_PUSH_NATIVE_QA_PASS " : "SURFACE_PUSH_NATIVE_QA_FAIL ") + message + " output=" + Output);
        EditorApplication.Exit(pass ? 0 : 1);
    }
}



// Temporary bounded input driver. Physics queries select a candidate; only real input can act on it.
[DefaultExecutionOrder(600)]
public sealed class SurfacePushNativeQaDriver : MonoBehaviour
{
    I01MissionComposition composition;
    MissionRuntime runtime;
    FirstPersonController player;
    CharacterController capsule;
    InteractionResolver resolver;
    I06MicrogravityInputBindings router;
    I06MicrogravityBridge bridge;
    BareArmContactPresenter bare;
    I06MarineBindings marine;
    Camera view;
    UIDocument document;
    Keyboard keyboard;
    Mouse mouse;
    InputSettings originalSettings, qaSettings;
    HideFlags originalFlags;
    bool previousBackground;
    readonly List<InputDevice> disabledDevices = new List<InputDevice>();
    int interactionCount, pushes, grabs, releases, footRequests, transferred;
    int handCompletions, ceilingStopEvents;
    double lastActualUpwardPush=-1;
    Collider lastCeilingCollider;
    CeilingContactRecord lastCeilingStop;
    Vector3 lastPushVelocity;
    bool sawHandBusy;
    [Serializable] sealed class CeilingContactRecord
    {
        public string stage,collider,path,collisionFlags;
        public int frame; public double realTime; public float impact;
        public Vector3 feet,eye,postContactVelocity,measuredNormal,measuredPoint;
        public bool upwardRayHitsEventCollider,qualifies;
    }
    CommandResult lastInteraction;
    Vector3 lastFeet;
    bool tracing;
    double nextTrace;
    string stage = "startup";
    static string Output => SurfacePushNativeQa.Output;
    MissionSession Session => runtime.Session;
    PlayerInputActions Input => player.InputSource as PlayerInputActions;
    [Serializable] sealed class Sample
    {
        public string stage,phase,airlock,routerRefusal,bridgeRefusal,bareRefusal,bareStage,held,collisions,input,bridgeResult,footResult;
        public Vector3 feet,eye,velocity;
        public float pitch,routeDistance,skinError,stamina;
        public bool floating,onRoute,outside,suited,control,paused,lease,requested,stranded,grounded;
        public double realTime,simulationSeconds;
        public int pushes,grabs,releases,footRequests,handCompletions,ceilingStopEvents;
    }
    static T Read<T>(object host,string field) => SurfacePushNativeQa.Read<T>(host,field);
    void Check(bool pass,string message) => SurfacePushNativeQa.Check(pass,message);
    void Stage(string value) { stage=value; SurfacePushNativeQa.SetStage(value); Note(value); }
    void Note(string text) => SurfacePushNativeQa.Note(Time.realtimeSinceStartupAsDouble.ToString("F3")+" | "+text);

    IEnumerator Start()
    {
        previousBackground=Application.runInBackground;Application.runInBackground=true;
        originalSettings=InputSystem.settings;originalFlags=originalSettings.hideFlags;
        if(originalFlags==HideFlags.HideAndDontSave)originalSettings.hideFlags=HideFlags.DontSave;
        qaSettings=Object.Instantiate(originalSettings);qaSettings.hideFlags=HideFlags.DontSave;
        qaSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        qaSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings=qaSettings;
        foreach(var device in InputSystem.devices.ToArray())
            if(device.enabled&&(device is Mouse||device is Keyboard||device is Gamepad)){disabledDevices.Add(device);InputSystem.DisableDevice(device);}
        keyboard=InputSystem.AddDevice<Keyboard>("Bootstrap route QA keyboard");mouse=InputSystem.AddDevice<Mouse>("Bootstrap route QA mouse");
        var stack=new Stack<IEnumerator>();stack.Push(Exercise());Exception failed=null;
        while(stack.Count>0)
        {
            bool more=false;object yielded=null;
            try {SurfacePushNativeQa.RequireNoErrors();more=stack.Peek().MoveNext();if(more)yielded=stack.Peek().Current;}
            catch(Exception error){failed=error;}
            if(failed!=null){foreach(var step in stack)try{(step as IDisposable)?.Dispose();}catch{}break;}
            if(!more){stack.Pop();continue;}if(yielded is IEnumerator nested){stack.Push(nested);continue;}yield return yielded;
        }
        if(failed!=null)
        {
            Keys();Button(false,false);Dump("failure-state.json");
            // Preserve actual state/camera. No pausing, pose reposition or route continuation for the failure image.
            if(view)yield return Capture("FAIL-actual-trap");
            Cleanup();SurfacePushNativeQa.Finish(false,"Stage="+stage+"\n"+failed);yield break;
        }
        Cleanup();SurfacePushNativeQa.Finish(true,"Actual saved arrival, neutral floor Space, real ceiling stop and one level-view neutral Space hand push passed. No QA pose, force or time writes. Not full route acceptance.");
    }

    IEnumerator Exercise()
    {
        yield return WaitFor(()=>Object.FindFirstObjectByType<MissionRuntime>()?.Session!=null,15,"Actual saved session startup");
        composition=Object.FindFirstObjectByType<I01MissionComposition>();runtime=composition.Runtime;player=runtime.Player;
        capsule=player.GetComponent<CharacterController>();resolver=player.Interaction;router=Object.FindFirstObjectByType<I06MicrogravityInputBindings>();
        bridge=Object.FindFirstObjectByType<I06MicrogravityBridge>();bare=Object.FindFirstObjectByType<BareArmContactPresenter>();marine=Object.FindFirstObjectByType<I06MarineBindings>();
        view=Camera.main;document=Object.FindFirstObjectByType<MissionFlightBoard>().GetComponent<UIDocument>();
        Check(router&&bridge&&bare&&marine&&view&&Input!=null,"Saved production micro input, bridge, hands, marine and default input exist.");
        player.SendMessage("OnApplicationFocus",true,SendMessageOptions.RequireReceiver);
        Note("QA-only normal focus callback and cloned Input System IgnoreFocus; no policy/Player fields changed.");
        resolver.Interacted+=OnInteract;player.Pushed+=OnPush;player.Grabbed+=OnGrab;player.Released+=OnRelease;player.FloatContact+=OnFloatContact;marine.Transferred+=OnTransferred;bridge.HandPushCompleted+=OnHandPushCompleted;
        lastFeet=player.transform.position;tracing=true;
        Check(SceneManager.GetActiveScene().path==SurfacePushNativeQa.Bootstrap&&runtime.DriveFromUpdate,"Actual saved scene with normal frame-driven Core.");
        Check(!composition.ModeChosen&&Session.State.Phase==GamePhase.Planning&&!player.IsFloating,"Unchosen Planning begins without floating/input lease.");
        yield return WaitFor(()=>PracticeButton()!=null,5,"Actual Practice UI");Submit(PracticeButton());yield return Frames(3);
        Check(composition.ModeChosen&&!runtime.PlanningDeadline.IsEnabled&&player.ControlAllowed,"Actual Practice choice releases ordinary deck controls.");
        Stage("continuous marine boarding W");
        yield return WalkTo(marine.MarineRoot.TransformPoint(new Vector3(0,.035f,-12)),.13f,20);
        yield return WalkTo(marine.MarineRoot.TransformPoint(new Vector3(0,.035f,.7f)),.13f,25);
        yield return Capture("01-real-seat-approach");yield return Use(Target(InteractionIds.CockpitConsole));
        yield return WaitFor(()=>composition.IsTransportDeparturePlaying,4,"Real seat E starts departure");
        Check(!player.ControlAllowed&&!router.ContactRoutingRequested,"Departure retains its own camera/control lease.");
        yield return WaitFor(()=>transferred==1&&!marine.OwnsPresentation&&player.ControlAllowed,35,"Actual complete22-second departure/transfer");
        yield return WaitFor(()=>player.IsFloating&&router.HasInputLease,3,"Root enters float from actual support at arrival");
        Check(!player.IsOnRoute&&!runtime.EvaStatus.IsOutside&&bridge.ContactModeEnabled,"Actual arrival has interior float/contact ownership.");
        Check(RegisteredRailCount()==23,"Saved root registered exactly23 authored rail shafts.");
        yield return Capture("02-actual-arrival-float");

        Stage("neutral floor Space");
        Keys();Button(false,false);yield return Frames(3);
        Check(pushes==0&&player.FloatVelocity.magnitude<.01f,"Arrival starts without any fixture push or residual movement input.");
        yield return Aim(player.ViewPivot.position+player.transform.forward*3);
        int floorBefore=pushes;
        Keys(Key.Space);footRequests++;yield return Frames(2);Keys();
        yield return WaitFor(()=>pushes>floorBefore,3,"Neutral floor Space produces real support push");
        Check(pushes==floorBefore+1&&router.LastFootResult==FloatActionResult.Done&&lastPushVelocity.y>.05f,
            "One neutral Space gives one actual upward floor push.");
        Check(new Vector2(lastPushVelocity.x,lastPushVelocity.z).magnitude<.01f,"Neutral floor push has no requested lateral travel.");
        Stage("passive real ceiling contact");
        yield return WaitFor(QualifiedCeilingStop,15,"Actual cockpit ceiling FloatContact, downward normal and removed upward velocity");
        Check(pushes==floorBefore+1&&grabs==0,"Ceiling was reached passively without extra pushes or grabs.");
        File.WriteAllText(Path.Combine(Output,"ceiling-stop-qualified.json"),JsonUtility.ToJson(lastCeilingStop,true));
        yield return Aim(player.ViewPivot.position+player.transform.forward*3);
        Check(Mathf.Abs(player.PitchDegrees)<.3f&&QualifiedCeilingStop(),"View is level while physically stopped against the same real ceiling.");
        yield return Capture("03-real-ceiling-contact-level-view");
        Stage("neutral ceiling Space via visible hand contact");
        int beforePush=pushes,beforeHand=handCompletions;
        sawHandBusy=false;
        Keys(Key.Space);footRequests++;yield return Frames(2);Keys();
        yield return WaitFor(()=>handCompletions>beforeHand,3,"Ceiling Space must complete actual hand contact; see trace/refusals if absent");
        Check(sawHandBusy&&bridge.LastCommandResult==FloatActionResult.Done,"Ceiling Space passed through actual animated hand bridge to Done.");
        Check(pushes==beforePush+1&&handCompletions==beforeHand+1&&lastPushVelocity.y<-.05f,
            "One neutral ceiling Space produces exactly one verified hand push directed down.");
        Check(player.FloatVelocity.y<-.01f,"Real controller moves downward after ceiling push.");
        yield return Capture("04-real-ceiling-push-down");
        yield return Seconds(.7);
        Check(pushes==beforePush+1&&handCompletions==beforeHand+1,"Released Space does not repeat the ceiling impulse.");
        SurfacePushNativeQa.VerifyProtected();
    }
    bool QualifiedCeilingStop()
    {
        return lastCeilingStop!=null&&lastCeilingStop.realTime>=lastActualUpwardPush
            &&Mathf.Abs(player.FloatVelocity.y)<=.0001f&&lastCeilingCollider
            &&lastCeilingCollider.Raycast(new Ray(player.ViewPivot.position,Vector3.up),out RaycastHit hit,.85f)
            &&hit.normal.y<=-.95f;
    }
    IEnumerator Aim(Vector3 worldPoint)
    {
        double end=Now+2.5;
        while(true)
        {
            Vector3 delta=worldPoint-player.ViewPivot.position;if(delta.sqrMagnitude<.000001f)yield break;
            float yaw=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg,pitch=Mathf.Atan2(delta.y,new Vector2(delta.x,delta.z).magnitude)*Mathf.Rad2Deg;
            Vector2 degrees=new Vector2(Mathf.DeltaAngle(player.transform.eulerAngles.y,yaw),Mathf.Clamp(pitch,-80f,80f)-player.PitchDegrees);
            if(degrees.sqrMagnitude<.04f)break;if(Now>end)throw new TimeoutException("Actual mouse look could not converge; remaining="+degrees);
            degrees=Vector2.ClampMagnitude(degrees,70);InputSystem.QueueStateEvent(mouse,new MouseState{delta=degrees/Input.MouseDegreesPerPixel});yield return null;
        }
        InputSystem.QueueStateEvent(mouse,new MouseState());yield return Frames(1);
    }
    IEnumerator WalkTo(Vector3 goal,float radius,double seconds)
    {
        double end=Now+seconds;
        while(Horizontal(goal,player.transform.position)>radius)
        {
            if(Now>end)throw new TimeoutException("Actual marine W route stalled at "+player.transform.position);
            Vector3 flat=goal;flat.y=player.ViewPivot.position.y;yield return Aim(flat);Keys(Key.W);yield return Seconds(.08);Keys();yield return Frames(1);
        }
        Keys();Check(!player.IsFloating&&player.ControlAllowed,"Marine checkpoint reached by ordinary deck input.");
    }
    IEnumerator Use(InteractableTarget target)
    {
        Keys();yield return Aim(target.transform.position);yield return Frames(2);resolver.Refresh();
        Check(resolver.CurrentTarget==target,"Actual nearest interaction ray resolves "+target.InteractionId+"; actual="+(resolver.CurrentTarget?resolver.CurrentTarget.InteractionId:"none"));
        int before=interactionCount;Keys(Key.E);yield return Frames(2);Keys();yield return Frames(2);
        Check(interactionCount==before+1&&lastInteraction.Succeeded,"Single actual E succeeds: "+target.InteractionId+" | "+lastInteraction.Explanation);
    }
    void LateUpdate()
    {
        if(!tracing||!player||runtime?.Session==null)return;
        if(bridge.IsBusy)sawHandBusy=true;
        if(Now<nextTrace)return;nextTrace=Now+.05;
        File.AppendAllText(Path.Combine(Output,"trace.jsonl"),JsonUtility.ToJson(State())+"\n");
    }
    Sample State()=>new Sample{stage=stage,phase=Session.State.Phase.ToString(),airlock=runtime.EvaStatus.Airlock.ToString(),feet=player.transform.position,
        eye=player.ViewPivot.position,velocity=player.FloatVelocity,pitch=player.PitchDegrees,routeDistance=player.RouteDistanceMetres,stamina=player.ExertionRemaining01,
        floating=player.IsFloating,onRoute=player.IsOnRoute,outside=runtime.EvaStatus.IsOutside,suited=runtime.EvaStatus.IsSuitOn,control=player.ControlAllowed,paused=runtime.IsPaused,
        lease=router.HasInputLease,requested=router.ContactRoutingRequested,stranded=player.IsStranded,grounded=capsule.isGrounded,collisions=capsule.collisionFlags.ToString(),
        held=player.HeldHandhold?player.HeldHandhold.name:"",input=player.InputSource?.GetType().Name,bridgeResult=bridge.LastCommandResult?.ToString()??"none",footResult=router.LastFootResult?.ToString()??"none",handCompletions=handCompletions,ceilingStopEvents=ceilingStopEvents,routerRefusal=router.LastRefusal,bridgeRefusal=bridge.LastRefusal,
        bareRefusal=bare.LastRefusalReason,bareStage=bare.ContactStage.ToString(),skinError=bare.ContactErrorMetres,realTime=Now,simulationSeconds=runtime.Clock.ElapsedSimulationSeconds,
        pushes=pushes,grabs=grabs,releases=releases,footRequests=footRequests};
    void Dump(string name){if(player&&runtime?.Session!=null)File.WriteAllText(Path.Combine(Output,name),JsonUtility.ToJson(State(),true));}
    void AssertLiving(){if(Session.State.Outcome!=MissionOutcome.None||!player.ControlAllowed)throw new InvalidOperationException("Actual route lost playable mission/control: "+Session.State.Phase+" "+Session.State.Outcome);}
    int RegisteredRailCount()=>Object.FindObjectsByType<CapsuleCollider>(FindObjectsSortMode.None).Count(c=>player.IsHandhold(c));
    InteractableTarget Target(string id)=>runtime.InteractionTargets.Single(t=>t.InteractionId==id);
    Button PracticeButton()=>document.rootVisualElement.Query<Button>().ToList().FirstOrDefault(b=>b.text.IndexOf("Practice",StringComparison.OrdinalIgnoreCase)>=0&&b.enabledInHierarchy&&Visible(b));
    static void Submit(VisualElement e){e.Focus();using(var action=NavigationSubmitEvent.GetPooled()){action.target=e;e.SendEvent(action);}}
    static bool Visible(VisualElement e){for(var n=e;n!=null;n=n.parent)if(n.resolvedStyle.display==DisplayStyle.None||n.resolvedStyle.visibility!=Visibility.Visible)return false;return e?.panel!=null;}
    void Keys(params Key[] keys){if(keyboard!=null&&keyboard.added)InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));}
    void Button(bool left,bool right){if(mouse!=null&&mouse.added){var state=new MouseState();if(left)state=state.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left);if(right)state=state.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Right);InputSystem.QueueStateEvent(mouse,state);}}
    void OnInteract(CommandResult result){interactionCount++;lastInteraction=result;Note("INTERACTION "+result.Succeeded+" "+result.Explanation);}
    void OnPush(Vector3 velocity){pushes++;lastPushVelocity=velocity;if(velocity.y>.01f)lastActualUpwardPush=Now;Note("ACTUAL PUSH "+velocity.ToString("F4"));}
    void OnHandPushCompleted(){handCompletions++;Note("ACTUAL HAND DONE "+bridge.LastCommandResult+" velocity="+player.FloatVelocity.ToString("F4"));}
    void OnFloatContact(Collider collider,float impact)
    {
        if(!collider||!player)return;
        bool rayHit=collider.Raycast(new Ray(player.ViewPivot.position,Vector3.up),out RaycastHit measured,.85f);
        var record=new CeilingContactRecord{stage=stage,collider=collider.name,path=SurfacePushNativeQa.Hierarchy(collider.transform),
            frame=Time.frameCount,realTime=Now,impact=impact,feet=player.transform.position,eye=player.ViewPivot.position,
            postContactVelocity=player.FloatVelocity,collisionFlags=capsule.collisionFlags.ToString(),upwardRayHitsEventCollider=rayHit,
            measuredNormal=rayHit?measured.normal:Vector3.zero,measuredPoint=rayHit?measured.point:Vector3.zero};
        record.qualifies=player.IsFloating&&collider.name=="COL_Cockpit_Ceiling"&&impact>.01f
            &&rayHit&&measured.normal.y<=-.95f&&Mathf.Abs(record.postContactVelocity.y)<=.0001f;
        File.AppendAllText(Path.Combine(Output,"actual-float-contacts.jsonl"),JsonUtility.ToJson(record)+"\n");
        if(!record.qualifies)return;
        lastCeilingStop=record;lastCeilingCollider=collider;ceilingStopEvents++;
        Note("ACTUAL CEILING STOP impact="+impact.ToString("F6")+" normal="+measured.normal.ToString("F6")
            +" velocity="+player.FloatVelocity.ToString("F6")+" feet="+player.transform.position.ToString("F6"));
    }
    void OnGrab(Collider collider){grabs++;Note("ACTUAL GRIP "+collider.name);}
    void OnRelease(Collider collider){releases++;Note("ACTUAL RELEASE "+(collider?collider.name:"destroyed"));}
    void OnTransferred(){transferred++;Note("ACTUAL MARINE TRANSFER");}
    void Cleanup()
    {
        tracing=false;Keys();Button(false,false);
        if(resolver)resolver.Interacted-=OnInteract;if(player){player.Pushed-=OnPush;player.Grabbed-=OnGrab;player.Released-=OnRelease;player.FloatContact-=OnFloatContact;}if(marine)marine.Transferred-=OnTransferred;if(bridge)bridge.HandPushCompleted-=OnHandPushCompleted;
        if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);if(mouse!=null&&mouse.added)InputSystem.RemoveDevice(mouse);
        foreach(var device in disabledDevices)if(device.added)InputSystem.EnableDevice(device);
        if(originalSettings){InputSystem.settings=originalSettings;originalSettings.hideFlags=originalFlags;}if(qaSettings)Object.Destroy(qaSettings);
        Application.runInBackground=previousBackground;
    }
    static double Now=>Time.realtimeSinceStartupAsDouble;
    static float Horizontal(Vector3 a,Vector3 b)=>Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z));
    static string Safe(string text)=>new string(text.Select(c=>char.IsLetterOrDigit(c)?c:'_').ToArray());
    static IEnumerator Frames(int count){for(int i=0;i<count;i++)yield return null;}
    static IEnumerator Seconds(double seconds){double end=Now+seconds;while(Now<end)yield return null;}
    static IEnumerator WaitFor(Func<bool> ready,double seconds,string why){double end=Now+seconds;while(!ready()){if(Now>end)throw new TimeoutException(why);yield return null;}}
    IEnumerator Capture(string name)
    {
        var texture=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);texture.Create();var old=view.targetTexture;float aspect=view.aspect;
        var panels=Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None).Where(d=>d.isActiveAndEnabled&&d.panelSettings&&d.rootVisualElement?.panel!=null).Select(d=>d.panelSettings).Distinct().ToArray();
        var targets=panels.Select(p=>p.targetTexture).ToArray();var clears=panels.Select(p=>p.clearColor).ToArray();
        try
        {
            view.targetTexture=texture;view.aspect=16f/9;foreach(var panel in panels){panel.targetTexture=texture;panel.clearColor=false;}
            yield return Frames(6);var active=RenderTexture.active;var image=new Texture2D(1280,720,TextureFormat.RGBA32,false);
            try{RenderTexture.active=texture;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Output,name+".png"),image.EncodeToPNG());Dump(name+".json");}
            finally{RenderTexture.active=active;Object.Destroy(image);}
        }
        finally{view.targetTexture=old;view.aspect=aspect;for(int i=0;i<panels.Length;i++)if(panels[i]){panels[i].targetTexture=targets[i];panels[i].clearColor=clears[i];}texture.Release();Object.Destroy(texture);}
    }
}
