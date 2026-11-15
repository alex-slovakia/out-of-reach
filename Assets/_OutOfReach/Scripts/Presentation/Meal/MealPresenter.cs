using System;
using System.Collections.Generic;
using OutOfReach.Contracts;
using OutOfReach.Interaction;
using OutOfReach.Presentation.Motion;
using UnityEngine;

namespace OutOfReach.Presentation.Meal
{
    /// <summary>
    /// MEAL-PRES-01: the physical meal at the galley. It wraps the existing <c>survival-ration</c> target in a
    /// <see cref="MealSequence"/>: E takes a real stored pouch from the ration drawer, prepares it by type (ready to eat,
    /// warmed at the food warmer, rehydrated at the water dispenser needle), opens it and eats. Core's own ration
    /// command runs exactly once, at the first bite. No resource, need, time or phase authority lives here.
    /// <para>
    /// The root supplies the current session's real interactable, read-only state and a presentation policy, and adds
    /// <see cref="BlocksMovement"/> to the player's control gate (the meal owns a temporary movement lock, like the antenna
    /// work). Pause freezes the meal; restart, a new session, a phase change, a cinematic, a hazard (a ship system losing
    /// condition), the suit, the mission end or a revoked policy end it safely: the pouch is hidden and every arm bone is
    /// given back. The visible WORK right hand carries the pouch with the measured edge clasp.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(260)]
    public sealed class MealPresenter : MonoBehaviour
    {
        /// <summary>The existing Core interaction ID this presentation decorates.</summary>
        public const string RationInteractionId = "survival-ration";
        public const string MoveCloserMessage = "Too far from the galley food drawer. Move closer and press E to eat.";
        public const string HazardMessage = "Alarm: the meal was put aside.";

        // Galley kit offsets in the WaterInteractionSurface frame (x = ship -Z, y up, z = out of the galley), measured from
        // hb_kit.py galley() (habitat kit @5b0f1ca, placed at ship (-2.38, 0, 14.75) +90 deg) and the surface 0.19 m in front
        // of the WaterDispenser anchor (I06HabitatAuthoring).
        public static readonly Vector3 NozzleInWaterFrame = new Vector3(0f, 0f, -.19f);
        public static readonly Vector3 WarmerInWaterFrame = new Vector3(.045f, -.65f, -.32f);
        public static readonly Vector3 TrashInWaterFrame = new Vector3(.845f, -.43f, -.24f);

        [SerializeField] private InteractableTarget rationTarget;
        [Tooltip("RationInteractionSurface: the open ration drawer, forward out into the aisle.")]
        [SerializeField] private Transform drawerFrame;
        [Tooltip("WaterInteractionSurface: the potable water dispenser bay, forward out into the aisle.")]
        [SerializeField] private Transform waterFrame;
        [Tooltip("GC_MealPouch (GALLEY-PROPS-01): GC_MealPouch_Sealed and GC_MealPouch_Opened at one origin.")]
        [SerializeField] private GameObject pouchModel;
        [Tooltip("WORK_Clasp_Meal_R from WORK-HAND-CLASP-01.")]
        [SerializeField] private TextAsset claspData;
        [Tooltip("Optional label material per preparation: ReadyToEat, Warmed, Rehydrated. Empty keeps the imported label.")]
        [SerializeField] private Material[] labelByPreparation = Array.Empty<Material>();
        [SerializeField, Range(.5f, 1.6f)] private float maximumDrawerDistance = 1.05f;
        [Tooltip("Pouch reach from the shoulder when no visible WORK arm carries it.")]
        [SerializeField, Range(.45f, .9f)] private float fallbackReachMetres = .68f;

        private MealSequence sequence;
        private IInteractable inner;
        private ISimulationClock clock;
        private IGameState state;
        private IEvaStatus suit;
        private IMissionEventBus events;
        private Transform eye;
        private EmbodiedBodyPresenter body;
        private Func<bool> context;
        private MealHandClasp hand;
        private MealClaspData clasp;
        private MealChoreography choreography;
        private MealStations stations;
        private GameObject pouch;
        private Renderer[] sealedRenderers = Array.Empty<Renderer>(), openedRenderers = Array.Empty<Renderer>();
        private Material[] importedLabels = Array.Empty<Material>();
        private readonly Dictionary<ShipSystemId, double> conditions = new Dictionary<ShipSystemId, double>();
        private bool focused = true, pouchVisible;

        public bool IsBound => sequence != null;
        public bool IsEating => sequence != null && sequence.IsWorking;
        /// <summary>The root adds this to the player's control gate while a meal runs.</summary>
        public bool BlocksMovement => IsEating;
        public MealSequence Sequence => sequence;
        public MealStage Stage => sequence != null ? sequence.Stage : MealStage.Idle;
        public string StatusLine => IsEating ? sequence.StatusLine : string.Empty;
        public double Progress01 => sequence != null ? sequence.Progress01 : 0d;
        public Transform Pouch => pouch ? pouch.transform : null;
        public bool PouchVisible => pouchVisible;
        public bool PouchOpened { get; private set; }
        public bool IsHandHolding => hand != null && hand.IsHolding;
        public float HandHeadErrorMetres => hand != null ? hand.LastHeadErrorMetres : float.NaN;
        public float HandReachShortfallMetres => hand != null ? hand.LastReachShortfallMetres : float.NaN;
        public InteractableTarget RationTarget => rationTarget;

        public event Action<MealPreparation> Started;
        public event Action<string> StatusChanged;
        public event Action<CommandResult> Eaten;
        public event Action<CommandResult> Finished;
        public event Action<string, bool> Cancelled;

        /// <summary>Scene setup (installer or tests). Call outside an active meal.</summary>
        public void ConfigureViews(InteractableTarget physicalRationTarget, Transform rationDrawer, Transform waterDispenser,
            GameObject mealPouch, TextAsset handClasp, Material[] labels = null)
        {
            ClearBinding();
            rationTarget = physicalRationTarget; drawerFrame = rationDrawer; waterFrame = waterDispenser;
            pouchModel = mealPouch; claspData = handClasp; labelByPreparation = labels ?? Array.Empty<Material>();
            DestroyPouch();
            clasp = null;
        }

        /// <summary>
        /// Binds the current session. Call after the habitat bound its targets (it binds the raw ration action) and after
        /// the body presenter bound its session. <paramref name="presentationContext"/> must exclude cinematics, Earth
        /// viewer, rest and stale sessions, but allow the same valid context while paused. Do not feed
        /// Player.ControlAllowed: the meal deliberately owns a temporary movement lock.
        /// </summary>
        public bool ConfigureSession(IInteractable realRation, ISimulationClock simulationClock, IGameState gameState,
            IEvaStatus evaStatus, IMissionEventBus missionEvents, Transform interactionEye, EmbodiedBodyPresenter visibleBody,
            Func<bool> presentationContext)
        {
            ClearBinding();
            if (!isActiveAndEnabled || realRation == null || realRation.InteractionId != RationInteractionId || realRation is MealSequence
                || !rationTarget || rationTarget.InteractionId != RationInteractionId || !drawerFrame || !pouchModel
                || simulationClock == null || gameState == null || evaStatus == null || missionEvents == null || !interactionEye
                || presentationContext == null) return false;
            inner = realRation; clock = simulationClock; state = gameState; suit = evaStatus; events = missionEvents;
            eye = interactionEye; body = visibleBody; context = presentationContext;
            if (clasp == null && claspData) clasp = MealClaspData.Parse(claspData.text);
            hand = body && clasp != null ? new MealHandClasp(body, clasp) : null;
            EnsurePouch();

            var created = new MealSequence(inner, IsMealStillValid, () => clock != null && clock.IsPaused, DrawerBlocker,
                null, index => index >= 3);
            sequence = created;
            created.Started += kind => { if (ReferenceEquals(sequence, created)) OnStarted(kind); };
            created.StageChanged += stage => { if (ReferenceEquals(sequence, created)) StatusChanged?.Invoke(created.StatusLine); };
            created.Eaten += result => { if (ReferenceEquals(sequence, created)) Eaten?.Invoke(result); };
            created.Finished += result => { if (ReferenceEquals(sequence, created)) OnEnded(); if (ReferenceEquals(sequence, created)) Finished?.Invoke(result); };
            created.Cancelled += (reason, eaten) => { if (ReferenceEquals(sequence, created)) { OnEnded(); Cancelled?.Invoke(reason, eaten); } };
            rationTarget.Bind(created);
            events.PhaseChanged += OnPhaseChanged;
            events.CinematicRequested += OnCinematicRequested;
            events.MissionEnded += OnMissionEnded;
            events.SystemConditionChanged += OnSystemConditionChanged;
            suit.Changed += OnSuitChanged;
            return true;
        }

        /// <summary>Ends any meal (nothing more is spent), restores the arm, hides the pouch and unbinds the session.</summary>
        public void ClearBinding()
        {
            MealSequence current = sequence;
            if (current != null && current.IsWorking) current.Cancel(current.HasEaten ? MealSequence.InterruptedAfterEatingMessage : MealSequence.InterruptedMessage);
            if (events != null)
            {
                events.PhaseChanged -= OnPhaseChanged;
                events.CinematicRequested -= OnCinematicRequested;
                events.MissionEnded -= OnMissionEnded;
                events.SystemConditionChanged -= OnSystemConditionChanged;
            }
            if (suit != null) suit.Changed -= OnSuitChanged;
            // Give the target back the raw action it had before this decorator, never a stale wrapper.
            if (current != null && rationTarget && ReferenceEquals(rationTarget.Interactable, current)) rationTarget.Bind(current.Inner);
            sequence = null; inner = null; clock = null; state = null; suit = null; events = null; eye = null; body = null; context = null;
            choreography = null;
            hand?.Dispose(); hand = null;
            conditions.Clear();
            HidePouch();
        }

        /// <summary>True (default): unpaused real frame time advances the meal. Tests may drive <see cref="Advance"/> instead.</summary>
        public bool DriveFromUpdate { get; set; } = true;

        /// <summary>Advances the current meal by unpaused real seconds (the sequence clamps, checks and commits once).</summary>
        public void Advance(double realSeconds)
        {
            if (sequence == null || !sequence.IsWorking) return;
            sequence.Advance(Math.Max(0d, realSeconds));
        }

        private void Update()
        {
            if (sequence == null) return;
            if (rationTarget && !ReferenceEquals(rationTarget.Interactable, sequence) && ReferenceEquals(rationTarget.Interactable, inner)
                && !sequence.IsWorking)
                rationTarget.Bind(sequence); // the habitat refreshed its raw binding for the same session: decorate it again
            if (!sequence.IsWorking) return;
            // One authoritative presentation timer: the sequence. Discard large visual hitches. A stale binding cancels
            // even when nothing advances it (Advance(0) only checks).
            if (DriveFromUpdate) sequence.Advance(Math.Min(.1d, Math.Max(0d, Time.unscaledDeltaTime)));
            else sequence.Advance(0d);
        }

        private void LateUpdate()
        {
            if (sequence == null || !sequence.IsWorking || choreography == null) { if (pouchVisible) HidePouch(); hand?.Release(); return; }
            MealPouchFrame frame = choreography.Sample(sequence.Stage, sequence.StageProgress01);
            var ideal = new Pose(frame.Position, frame.Rotation);
            Pose shown = ideal;
            bool carried = false;
            if (hand != null && frame.HandWeight > .001f)
            {
                if (hand.TryHold(ideal, frame.HandWeight, frame.Clasp, stations.EyeRotation, out Pose held))
                {
                    carried = true;
                    shown = Blend(ideal, held, frame.Attach);
                }
            }
            else hand?.Release();
            if (!carried && frame.Attach > 0f)
            {
                // No visible WORK arm: the pouch stays where an arm could hold it.
                Vector3 shoulder = stations.EyePosition + stations.EyeRotation * MealChoreography.ShoulderKey;
                shown = Blend(ideal, new Pose(MealChoreography.ClampReach(ideal.position, shoulder, fallbackReachMetres), ideal.rotation), frame.Attach);
            }
            ApplyPouch(shown, frame);
        }

        private void OnStarted(MealPreparation kind)
        {
            stations = CaptureStations();
            choreography = new MealChoreography(stations, kind, sequence.IsLabelCheck);
            ApplyLabel(kind);
            Started?.Invoke(kind);
        }

        private void OnEnded()
        {
            choreography = null;
            hand?.Release();
            HidePouch();
        }

        private MealStations CaptureStations()
        {
            var result = new MealStations
            {
                EyePosition = eye.position,
                EyeRotation = eye.rotation,
                DrawerPosition = drawerFrame.position,
                DrawerRotation = drawerFrame.rotation,
                Up = Vector3.up,
                IntoGalley = -drawerFrame.forward,
            };
            if (waterFrame)
            {
                result.Nozzle = waterFrame.TransformPoint(NozzleInWaterFrame);
                result.Warmer = waterFrame.TransformPoint(WarmerInWaterFrame);
                result.Trash = waterFrame.TransformPoint(TrashInWaterFrame);
            }
            return result;
        }

        private string DrawerBlocker()
        {
            if (!eye || !drawerFrame) return null;
            return Vector3.Distance(eye.position, drawerFrame.position) > maximumDrawerDistance ? MoveCloserMessage : null;
        }

        private bool IsMealStillValid()
        {
            if (sequence == null || !isActiveAndEnabled || !focused || !eye || !rationTarget || !drawerFrame
                || !ReferenceEquals(rationTarget.Interactable, sequence) || suit == null || state == null || context == null) return false;
            if (suit.IsSuitOn || suit.IsOutside) return false;
            GamePhase phase = state.Phase;
            if (phase == GamePhase.Planning || phase == GamePhase.Intro || phase == GamePhase.Outcome || phase == GamePhase.Report) return false;
            try { return context(); }
            catch (Exception error) { Debug.LogException(error, this); return false; }
        }

        private void OnPhaseChanged(PhaseChanged change) => sequence?.Cancel(Interrupted("The mission phase changed."));
        private void OnCinematicRequested(CinematicRequested request) => sequence?.Cancel(Interrupted(null));
        private void OnMissionEnded(MissionEnded ended) => sequence?.Cancel(Interrupted(null));
        private void OnSuitChanged() { if (suit != null && (suit.IsSuitOn || suit.IsOutside)) sequence?.Cancel(Interrupted(null)); }

        private void OnSystemConditionChanged(SystemConditionChanged change)
        {
            // A hazard is a ship system LOSING condition while the crew member eats. Repairs and first reports only update.
            bool known = conditions.TryGetValue(change.SystemId, out double previous);
            conditions[change.SystemId] = change.Condition01;
            if (sequence != null && sequence.IsWorking && ((known && change.Condition01 < previous - 1e-6) || (!known && change.Condition01 <= 0d)))
                sequence.Cancel(sequence.HasEaten ? HazardMessage + " The ration was already eaten." : HazardMessage + " Nothing was eaten.");
        }

        private string Interrupted(string prefix)
        {
            string tail = sequence != null && sequence.HasEaten ? MealSequence.InterruptedAfterEatingMessage : MealSequence.InterruptedMessage;
            return string.IsNullOrEmpty(prefix) ? tail : prefix + " " + tail;
        }

        // Like the player's own focus gate: only the focus event changes it (batch players report no focus at startup).
        private void OnApplicationFocus(bool hasFocus)
        {
            focused = hasFocus;
            if (!hasFocus) sequence?.Cancel(Interrupted("The game lost focus."));
        }
        private void OnDisable() => ClearBinding();
        private void OnDestroy() { ClearBinding(); DestroyPouch(); }

        private void EnsurePouch()
        {
            if (pouch || !pouchModel) return;
            pouch = Instantiate(pouchModel, transform);
            pouch.name = "Meal pouch (presentation only)";
            var sealedList = new List<Renderer>();
            var openedList = new List<Renderer>();
            foreach (Renderer renderer in pouch.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.name.IndexOf("Opened", StringComparison.OrdinalIgnoreCase) >= 0) openedList.Add(renderer);
                else sealedList.Add(renderer);
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            foreach (Collider collider in pouch.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            sealedRenderers = sealedList.ToArray();
            openedRenderers = openedList.ToArray();
            var labels = new List<Material>();
            foreach (Renderer renderer in pouch.GetComponentsInChildren<Renderer>(true))
                foreach (Material material in renderer.sharedMaterials)
                    if (material && material.name.StartsWith("GC_Labels", StringComparison.Ordinal) && !labels.Contains(material)) labels.Add(material);
            importedLabels = labels.ToArray();
            pouch.SetActive(false);
            pouchVisible = false;
        }

        private void ApplyLabel(MealPreparation kind)
        {
            if (!pouch) return;
            int index = (int)kind;
            Material wanted = labelByPreparation != null && index < labelByPreparation.Length ? labelByPreparation[index] : null;
            foreach (Renderer renderer in pouch.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    bool isLabel = materials[i] && (Array.IndexOf(importedLabels, materials[i]) >= 0
                        || (labelByPreparation != null && Array.IndexOf(labelByPreparation, materials[i]) >= 0));
                    if (!isLabel) continue;
                    Material next = wanted ? wanted : importedLabels.Length > 0 ? importedLabels[0] : materials[i];
                    if (materials[i] != next) { materials[i] = next; changed = true; }
                }
                if (changed) renderer.sharedMaterials = materials;
            }
        }

        private void ApplyPouch(Pose pose, MealPouchFrame frame)
        {
            if (!pouch) return;
            if (!frame.Visible) { HidePouch(); return; }
            Transform t = pouch.transform;
            t.SetPositionAndRotation(pose.position, pose.rotation);
            t.localScale = new Vector3(1f, 1f, Mathf.Clamp(frame.Thickness, .5f, 1.2f));
            if (!pouchVisible) { pouch.SetActive(true); pouchVisible = true; }
            // Both variants share the prop origin: the cut swaps them in place.
            PouchOpened = frame.Opened;
            foreach (Renderer r in sealedRenderers) if (r && r.enabled == frame.Opened) r.enabled = !frame.Opened;
            foreach (Renderer r in openedRenderers) if (r && r.enabled != frame.Opened) r.enabled = frame.Opened;
        }

        private void HidePouch()
        {
            if (pouch && pouch.activeSelf) pouch.SetActive(false);
            pouchVisible = false;
            PouchOpened = false;
        }

        private void DestroyPouch()
        {
            if (!pouch) return;
            if (Application.isPlaying) Destroy(pouch); else DestroyImmediate(pouch);
            pouch = null; pouchVisible = false;
            sealedRenderers = openedRenderers = Array.Empty<Renderer>();
            importedLabels = Array.Empty<Material>();
        }

        private static Pose Blend(Pose a, Pose b, float t) => new Pose(Vector3.Lerp(a.position, b.position, t), Quaternion.Slerp(a.rotation, b.rotation, t));
    }
}
