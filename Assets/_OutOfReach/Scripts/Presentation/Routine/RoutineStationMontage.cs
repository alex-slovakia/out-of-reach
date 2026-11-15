using System;
using System.Collections.Generic;
using System.Globalization;
using OutOfReach.Contracts;
using OutOfReach.Core;
using OutOfReach.Core.Runtime;
using OutOfReach.Interaction;
using OutOfReach.Presentation.Cinematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace OutOfReach.Presentation.Routine
{
    /// <summary>
    /// R18 routine montage stations. While the chapter routine runs, the existing montage card gets a live backdrop
    /// that cuts between the stations whose actions Core really executed (grow module, galley, water, berth), with a
    /// small caption. Presentation only and fail open:
    /// - it binds itself to the scene's MissionRuntime and reads Core; it issues no command and advances no time;
    /// - the player's camera, body and rest are never touched (the picture comes from its own camera into a texture),
    ///   so the view is exactly as it was the moment the routine stops;
    /// - the backdrop lives inside the montage card, so pause, menus and cinematics hide it with the card;
    /// - reduced effects: hard cuts, longer shots, no camera drift;
    /// - a missing anchor, card or free camera pose shows nothing instead of failing.
    /// </summary>
    [DefaultExecutionOrder(280)]
    public sealed class RoutineStationMontage : MonoBehaviour
    {
        public const string HostClass = "chapter-montage";
        public const string BackdropName = "routine-station-backdrop";
        public const string CaptionName = "routine-station-caption";

        private const float BackdropOpacity = .5f, FadeSeconds = .28f, RescanSeconds = .5f, HostScanSeconds = .25f;
        private const float FieldOfView = 62f;

        /// <summary>Kill switch for the integrator and for tests: false shows nothing and releases everything.</summary>
        public static bool Enabled = true;

        public static RoutineStationMontage Instance { get; private set; }

        private readonly RoutineStationSchedule schedule = new RoutineStationSchedule();
        private readonly Dictionary<RoutineStation, Transform> anchors = new Dictionary<RoutineStation, Transform>();
        private readonly Dictionary<RoutineStation, Pose> poses = new Dictionary<RoutineStation, Pose>();
        private readonly HashSet<RoutineStation> unsolvable = new HashSet<RoutineStation>();
        private readonly int[] shots = new int[5];

        private MissionRuntime runtime;
        private MissionCinematicPresenter cinematics;
        private VisualElement host;
        private Image backdrop;
        private Label caption;
        private Camera stationCamera;
        private RenderTexture texture;
        private float rescanIn, hostScanIn, opacity, driftSeconds;
        private int cutsSeen;
        private bool bound, wasRunning, anchorsResolved, faulted, textureFailed;

        /// <summary>Station whose picture or caption is on screen now (None when nothing is shown).</summary>
        public RoutineStation ShownStation { get; private set; }
        /// <summary>True while the station camera renders into the card's backdrop.</summary>
        public bool PictureVisible => stationCamera && stationCamera.enabled && opacity > 0f;
        public string ShownCaption => ShownStation == RoutineStation.None ? string.Empty : schedule.Caption;
        public RoutineStationSchedule Schedule => schedule;
        public int ShotsOf(RoutineStation station) => shots[(int)station];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Instance || !Enabled) return;
            var go = new GameObject("RoutineStationMontage (R18)");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<RoutineStationMontage>();
        }

        private void OnDisable() => Release();

        private void OnDestroy()
        {
            Release();
            DetachElements();
            if (stationCamera) Destroy(stationCamera.gameObject);
            if (Instance == this) Instance = null;
        }

        private void LateUpdate()
        {
            if (faulted) return;
            try
            {
                Tick(Mathf.Clamp(Time.unscaledDeltaTime, 0f, .1f));
            }
            catch (Exception exception)
            {
                // Fail open: the montage card keeps working without station pictures.
                faulted = true;
                Debug.LogWarning("[RoutineStationMontage] disabled after an unexpected failure: " + exception.Message);
                try { Release(); } catch (Exception) { /* nothing left to release safely */ }
                enabled = false;
            }
        }

        /// <summary>One presentation frame. Public so a test or a native check can drive it with explicit real seconds.</summary>
        public void Tick(float realSeconds)
        {
            if (!Enabled)
            {
                Unbind();
                return;
            }

            if (!runtime)
            {
                if (bound) Unbind();
                rescanIn -= realSeconds;
                if (rescanIn > 0f) return;
                rescanIn = RescanSeconds;
                runtime = FindFirstObjectByType<MissionRuntime>();
                if (!runtime) return;
                bound = true;
                cinematics = FindFirstObjectByType<MissionCinematicPresenter>();
            }

            IMissionChapterFlow flow = runtime.ChapterFlow;
            bool running = flow != null && flow.Enabled && flow.IsRoutineRunning;
            if (!running)
            {
                if (wasRunning)
                {
                    wasRunning = false;
                    LogSegment();
                }

                schedule.Reset();
                Release();
                return;
            }

            if (!wasRunning)
            {
                wasRunning = true;
                Array.Clear(shots, 0, shots.Length);
                poses.Clear();
                unsolvable.Clear();
                anchorsResolved = false;
                cutsSeen = schedule.Cuts;
            }

            ISurvivalExtension survival = runtime.Survival;
            ISurvivalRecovery recovery = runtime.SurvivalRecovery;
            IGameState state = runtime.GameState;
            double now = state != null ? state.ElapsedSimulationSeconds : 0d;
            var facts = new RoutineFacts(true, flow.RoutineStartedAtSimulationSeconds ?? double.NaN,
                flow.RoutineRations, flow.RoutineDrinks, flow.RoutineRests, recovery != null && recovery.IsResting,
                survival != null && survival.Enabled && survival.GrowStage == GrowModuleStage.Growing,
                survival != null ? survival.CropRemainingSimulationSeconds : double.NaN,
                (int)Math.Floor(now / 86400d) + 1);
            bool paused = runtime.IsPaused;
            bool reduced = cinematics && cinematics.ReducedEffects;
            schedule.Observe(facts, realSeconds, paused, reduced);

            if (!EnsureHost(realSeconds))
            {
                Release();
                return;
            }

            bool cardVisible = host.panel != null && host.resolvedStyle.display != DisplayStyle.None;
            RoutineStation station = cardVisible ? schedule.Current : RoutineStation.None;
            Pose pose = default;
            // Without a graphics device (headless test runs) or a backdrop texture there is no picture: caption only.
            bool posed = station != RoutineStation.None && PicturesSupported && TryPose(station, out pose) && EnsureCamera();
            if (schedule.Cuts != cutsSeen || station != ShownStation)
            {
                cutsSeen = schedule.Cuts;
                if (station != RoutineStation.None && station != ShownStation) shots[(int)station]++;
                ShownStation = station;
                driftSeconds = 0f;
                if (!reduced) opacity = 0f;
            }

            float target = posed ? BackdropOpacity : 0f;
            if (reduced) opacity = target;
            else if (!paused) opacity = Mathf.MoveTowards(opacity, target, BackdropOpacity * realSeconds / FadeSeconds);

            if (posed)
            {
                if (!reduced && !paused) driftSeconds += realSeconds;
                // A slow, small drift keeps the shot alive; reduced effects hold it still.
                Quaternion sway = Quaternion.AngleAxis(Mathf.Sin(driftSeconds * .9f) * 1.1f, Vector3.up);
                stationCamera.transform.SetPositionAndRotation(
                    pose.position + (pose.rotation * Vector3.forward * Mathf.Min(driftSeconds * .03f, .08f)),
                    sway * pose.rotation);
                stationCamera.enabled = true;
                backdrop.image = texture;
            }
            else if (stationCamera)
            {
                stationCamera.enabled = false;
            }

            backdrop.style.opacity = opacity;
            backdrop.style.display = posed && opacity > .001f ? DisplayStyle.Flex : DisplayStyle.None;
            if (posed) backdrop.MarkDirtyRepaint();
            caption.text = station != RoutineStation.None ? schedule.Caption : string.Empty;
            caption.style.display = station != RoutineStation.None ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void Unbind()
        {
            bound = false;
            wasRunning = false;
            runtime = null;
            cinematics = null;
            anchors.Clear();
            poses.Clear();
            unsolvable.Clear();
            anchorsResolved = false;
            schedule.Reset();
            Release();
        }

        /// <summary>Nothing on screen, no camera rendering, no texture held: the state before the routine.</summary>
        private void Release()
        {
            ShownStation = RoutineStation.None;
            opacity = 0f;
            if (stationCamera)
            {
                stationCamera.enabled = false;
                stationCamera.targetTexture = null;
            }

            if (backdrop != null)
            {
                backdrop.image = null;
                backdrop.style.opacity = 0f;
                backdrop.style.display = DisplayStyle.None;
            }

            if (caption != null) caption.style.display = DisplayStyle.None;
            if (texture)
            {
                texture.Release();
                Destroy(texture);
                texture = null;
            }
        }

        private bool EnsureHost(float realSeconds)
        {
            if (host != null && host.panel != null && backdrop != null && backdrop.parent == host && caption != null && caption.parent == host)
                return true;
            hostScanIn -= realSeconds;
            if (hostScanIn > 0f) return false;
            hostScanIn = HostScanSeconds;
            DetachElements();
            foreach (UIDocument document in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                VisualElement root = document ? document.rootVisualElement : null;
                VisualElement found = root?.Q<VisualElement>(null, HostClass);
                if (found == null) continue;
                host = found;
                BuildElements();
                return true;
            }

            return false;
        }

        private void BuildElements()
        {
            backdrop = new Image { name = BackdropName, pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.ScaleAndCrop };
            backdrop.style.position = Position.Absolute;
            backdrop.style.left = 0f;
            backdrop.style.top = 0f;
            backdrop.style.right = 0f;
            backdrop.style.bottom = 0f;
            backdrop.style.opacity = 0f;
            backdrop.style.display = DisplayStyle.None;
            // First child: behind the card's own text, above its dark background.
            host.Insert(0, backdrop);

            caption = new Label { name = CaptionName, pickingMode = PickingMode.Ignore, enableRichText = false };
            caption.style.position = Position.Absolute;
            caption.style.right = 64f;
            caption.style.top = 30f;
            caption.style.paddingLeft = 12f;
            caption.style.paddingRight = 12f;
            caption.style.paddingTop = 5f;
            caption.style.paddingBottom = 5f;
            caption.style.fontSize = 15f;
            caption.style.letterSpacing = 2f;
            caption.style.color = new Color(155f / 255f, 229f / 255f, 232f / 255f, 1f);
            caption.style.backgroundColor = new Color(7f / 255f, 9f / 255f, 9f / 255f, .78f);
            caption.style.borderLeftWidth = 2f;
            caption.style.borderLeftColor = new Color(155f / 255f, 229f / 255f, 232f / 255f, .8f);
            caption.style.unityTextAlign = TextAnchor.MiddleRight;
            caption.style.display = DisplayStyle.None;
            host.Add(caption);
        }

        private void DetachElements()
        {
            backdrop?.RemoveFromHierarchy();
            caption?.RemoveFromHierarchy();
            backdrop = null;
            caption = null;
            host = null;
        }

        /// <summary>False where nothing can be rendered (a null graphics device) or the backdrop texture once failed.</summary>
        private bool PicturesSupported => !textureFailed && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;

        private bool EnsureCamera()
        {
            if (!texture)
            {
                // Two thirds of the screen is enough for a dimmed backdrop and keeps the extra camera cheap.
                int height = Mathf.Clamp(Screen.height * 2 / 3, 540, 720);
                int width = Mathf.Clamp(Mathf.RoundToInt(height * (Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f)), 640, 1720);
                texture = new RenderTexture(width, height, 24, RenderTextureFormat.Default)
                {
                    name = "RoutineStationBackdrop (R18)",
                    hideFlags = HideFlags.DontSave
                };
                if (!texture.Create())
                {
                    // No backdrop for this session; the card and the captions keep working.
                    textureFailed = true;
                    Destroy(texture);
                    texture = null;
                    return false;
                }
            }

            if (!stationCamera)
            {
                var go = new GameObject("RoutineStationCamera (R18)");
                go.transform.SetParent(transform, false);
                stationCamera = go.AddComponent<Camera>();
                Camera main = Camera.main;
                if (main) stationCamera.CopyFrom(main);
                go.tag = "Untagged";
                stationCamera.depth = (main ? main.depth : 0f) - 5f;
                stationCamera.fieldOfView = FieldOfView;
                stationCamera.nearClipPlane = .05f;
                stationCamera.enabled = false;
            }

            if (stationCamera.targetTexture != texture)
            {
                stationCamera.targetTexture = texture;
                // The aspect follows the backdrop texture, whatever the copied camera had set.
                stationCamera.ResetAspect();
            }

            return true;
        }

        private bool TryPose(RoutineStation station, out Pose pose)
        {
            if (poses.TryGetValue(station, out pose)) return true;
            if (unsolvable.Contains(station)) return false;
            ResolveAnchors();
            if (anchors.TryGetValue(station, out Transform anchor) && anchor
                && RoutineStationFraming.TrySolve(anchor.position, CabinFocus(station, anchor),
                    runtime && runtime.Player ? runtime.Player.transform : null, out pose))
            {
                poses[station] = pose;
                return true;
            }

            unsolvable.Add(station);
            pose = default;
            return false;
        }

        /// <summary>The free cabin lies between the stations: look at a station from the side of the others.</summary>
        private Vector3 CabinFocus(RoutineStation station, Transform anchor)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (KeyValuePair<RoutineStation, Transform> other in anchors)
            {
                if (other.Key == station || !other.Value) continue;
                sum += other.Value.position;
                count++;
            }

            Camera main = Camera.main;
            if (main)
            {
                sum += main.transform.position;
                count++;
            }

            return count > 0 ? sum / count : anchor.position + anchor.forward;
        }

        /// <summary>Existing interaction anchors only: the same targets the player uses at each station.</summary>
        private void ResolveAnchors()
        {
            if (anchorsResolved) return;
            anchorsResolved = true;
            anchors.Clear();
            foreach (InteractableTarget target in FindObjectsByType<InteractableTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!target) continue;
                RoutineStation station = StationOf(target.InteractionId);
                if (station != RoutineStation.None && !anchors.ContainsKey(station)) anchors[station] = target.transform;
            }
        }

        public static RoutineStation StationOf(string interactionId)
        {
            switch (interactionId)
            {
                case InteractionIds.ConsumeRation: return RoutineStation.Pantry;
                case InteractionIds.DrinkWater: return RoutineStation.Water;
                case InteractionIds.SurvivalSleep: return RoutineStation.Berth;
                case InteractionIds.HarvestCrop:
                case InteractionIds.PlantSeedlings:
                case InteractionIds.BuildGrowModule: return RoutineStation.Crops;
                default: return RoutineStation.None;
            }
        }

        private void LogSegment()
        {
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[RoutineStationMontage] segment shown: crops x{0}, galley x{1}, water x{2}, berth x{3}; pictures {4}/{5} stations framed",
                shots[(int)RoutineStation.Crops], shots[(int)RoutineStation.Pantry], shots[(int)RoutineStation.Water],
                shots[(int)RoutineStation.Berth], poses.Count, poses.Count + unsolvable.Count));
        }
    }
}
