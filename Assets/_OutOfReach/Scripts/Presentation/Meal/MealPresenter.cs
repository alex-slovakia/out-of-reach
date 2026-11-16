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
    /// <para>
    /// R35 (the readable first-person meal): the visible WORK left hand pinches the top strip and tears it off
    /// (<see cref="MealOffHand"/>, <see cref="MealTearStrip"/>), the pouch is tipped to the mouth for three squeezed sips
    /// with a small head nod (<see cref="MealViewNod"/>, none with reduced motion) and each beat has its own contact
    /// sound (<see cref="MealFoley"/>). All of it follows the one unchanged meal timer; none of it issues a command.
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
        private MealOffHand offHand;
        private MealClaspData clasp;
        private MealChoreography choreography;
        private MealStations stations;
        private GameObject pouch;
        private MealTearStrip strip;
        private MealPouchShape shape = MealPouchShape.Default;
        private readonly MealViewNod nod = new MealViewNod();
        private readonly MealFoley foley = new MealFoley();
        private FirstPersonMotionPresenter motion;
        private MealBeatMark[] marks;
        private int nextBeat;
        private Renderer[] sealedRenderers = Array.Empty<Renderer>(), openedRenderers = Array.Empty<Renderer>();
        private Material[] importedLabels = Array.Empty<Material>();
        private readonly Dictionary<ShipSystemId, double> conditions = new Dictionary<ShipSystemId, double>();
        private bool focused = true, pouchVisible, eyeIsCamera, stripFree;
        private Quaternion stripHand = Quaternion.identity;
        private Vector3 stripAlong = Vector3.right, stripHeight = Vector3.up;

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

        /// <summary>0..1 how far the top strip is torn off the pouch that is shown now.</summary>
        public float TearProgress { get; private set; }
        /// <summary>The torn strip is shown (on the pouch while it tears, then in the left hand).</summary>
        public bool StripVisible => strip != null && strip.IsVisible;
        public Transform Strip => strip != null ? strip.Transform : null;
        /// <summary>The visible WORK left hand holds the strip (or waits beside it).</summary>
        public bool IsOffHandHolding => offHand != null && offHand.IsHolding;
        public float OffHandReachShortfallMetres => offHand != null ? offHand.LastReachShortfallMetres : float.NaN;
        /// <summary>How far the closed left thumb and index pads are from touching (0 = they meet).</summary>
        public float OffHandPinchGapMetres => offHand != null ? offHand.PinchGapMetres : float.NaN;
        /// <summary>Distance of the strip's held point from the left fingers (0 = in the fingers).</summary>
        public float StripPinchErrorMetres => strip != null ? strip.LastPinchErrorMetres : float.NaN;
        /// <summary>0..1 squeeze of the pouch by the holding hand right now.</summary>
        public float Squeeze { get; private set; }
        /// <summary>The head nod on the view right now, degrees (0 with reduced motion).</summary>
        public float NodDegrees => nod.Degrees;
        /// <summary>Beats of the current meal that have played so far.</summary>
        public int BeatsPlayed { get; private set; }

        /// <summary>
        /// Comfort setting: true keeps the view still during the meal (no head nod). Null (default) follows the mission's
        /// first-person motion setting (<see cref="FirstPersonMotionPresenter.ReducedMotion"/> or motion switched off).
        /// </summary>
        public Func<bool> ReducedMotionQuery { get; set; }
        public bool ReducedMotion => ReducedMotionQuery != null ? ReducedMotionQuery() : motion && (motion.ReducedMotion || !motion.MotionEnabled);

        public event Action<MealPreparation> Started;
        public event Action<string> StatusChanged;
        public event Action<CommandResult> Eaten;
        public event Action<CommandResult> Finished;
        public event Action<string, bool> Cancelled;
        /// <summary>A beat of the meal was reached (informational). The flag says whether its sound actually played.</summary>
        public event Action<MealBeatMark, bool> BeatPlayed;

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
            // Only a camera's own transform nods: a look pivot belongs to the player controller.
            eyeIsCamera = eye.GetComponent<Camera>();
            motion = FindAnyObjectByType<FirstPersonMotionPresenter>();
            if (clasp == null && claspData) clasp = MealClaspData.Parse(claspData.text);
            hand = body && clasp != null ? new MealHandClasp(body, clasp) : null;
            offHand = body ? new MealOffHand(body) : null;
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

        /// <summary>Ends any meal (nothing more is spent), restores the arms and the view, hides the pouch and unbinds the session.</summary>
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
            nod.Release();
            sequence = null; inner = null; clock = null; state = null; suit = null; events = null; eye = null; body = null; context = null;
            motion = null; eyeIsCamera = false;
            choreography = null; marks = null;
            hand?.Dispose(); hand = null;
            offHand?.Dispose(); offHand = null;
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
            PlayBeats();
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
            PlayBeats();
        }

        private void LateUpdate()
        {
            if (sequence == null || !sequence.IsWorking || choreography == null)
            {
                if (pouchVisible) HidePouch();
                hand?.Release(); offHand?.Release(); nod.Release(); strip?.Hide();
                return;
            }
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
            PresentOpening(shown, frame);
            Squeeze = frame.Squeeze;
            // The head meets the pouch and tips back while squeezing; never with reduced motion, never on a look pivot.
            nod.Apply(eyeIsCamera ? eye : null, ReducedMotion ? 0f : frame.NodDegrees);
            PlayBeats();
        }

        /// <summary>The left hand and the strip it tears off, on the pouch as it is actually shown.</summary>
        private void PresentOpening(Pose shown, MealPouchFrame frame)
        {
            Vector3 target = Vector3.Lerp(shown.position + shown.rotation * frame.OffHandLocal, frame.OffHandAway, frame.OffHandAwayBlend);
            Vector3 pinch = target;
            bool pinched = false;
            if (offHand != null && frame.OffHandWeight > .001f)
                pinched = offHand.TryPinch(target, frame.OffHandRotation, frame.OffHandWeight, frame.OffHandPinch, stations.EyeRotation, out pinch);
            else offHand?.Release();

            bool free = frame.Tear >= 1f;
            // Without a visible left hand the strip never peels or floats: it stays on the cut and goes when the tear ends.
            TearProgress = !frame.Visible ? 0f : pinched || free ? frame.Tear : 0f;
            if (strip == null || !frame.Visible || !frame.StripVisible || (free && !pinched)) { strip?.Hide(); stripFree = false; return; }
            if (!free)
            {
                strip.Present(shown, pinched ? frame.Tear : 0f, pinch);
                stripFree = false;
                return;
            }
            if (!stripFree)
            {
                // The strip came free: from now on it is carried rigidly in the fingers.
                stripFree = true;
                stripHand = frame.OffHandRotation;
                stripAlong = strip.LastAlong;
                stripHeight = strip.LastHeight;
            }
            Quaternion turned = frame.OffHandRotation * Quaternion.Inverse(stripHand);
            strip.PresentFree(pinch, turned * stripAlong, turned * stripHeight);
        }

        /// <summary>
        /// Plays every beat the meal timer has reached, in order (called whenever the timer moved, and with the pose).
        /// A sip is only heard after the real first bite. Beats still ahead when the meal ends are never played.
        /// </summary>
        private void PlayBeats()
        {
            while (marks != null && sequence != null && sequence.IsWorking && nextBeat < marks.Length
                && sequence.ElapsedActiveSeconds >= marks[nextBeat].SecondsIn(sequence.Timeline))
            {
                MealBeatMark mark = marks[nextBeat++];
                if ((mark.Beat == MealBeat.Sip || mark.Beat == MealBeat.Swallow) && !sequence.HasEaten) continue;
                bool sounded = foley.Play(mark);
                BeatsPlayed++;
                BeatPlayed?.Invoke(mark, sounded);
            }
        }

        private void OnStarted(MealPreparation kind)
        {
            stations = CaptureStations();
            choreography = new MealChoreography(stations, kind, sequence.IsLabelCheck, shape);
            marks = MealBeats.For(kind, sequence.IsLabelCheck);
            nextBeat = 0;
            BeatsPlayed = 0;
            stripFree = false;
            ApplyLabel(kind);
            Started?.Invoke(kind);
        }

        private void OnEnded()
        {
            choreography = null;
            marks = null;
            hand?.Release();
            offHand?.Release();
            nod.Release();
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
            Camera view = eye.GetComponent<Camera>();
            if (view && !view.orthographic)
            {
                result.ViewTanY = Mathf.Tan(view.fieldOfView * .5f * Mathf.Deg2Rad);
                result.ViewTanX = result.ViewTanY * Mathf.Max(.5f, view.aspect);
            }
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
            Material foil = null;
            foreach (Renderer renderer in pouch.GetComponentsInChildren<Renderer>(true))
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (!material) continue;
                    if (material.name.StartsWith("GC_Labels", StringComparison.Ordinal) && !labels.Contains(material)) labels.Add(material);
                    if (!foil && material.name.StartsWith("GC_Foil", StringComparison.Ordinal)) foil = material;
                }
            importedLabels = labels.ToArray();
            if (!foil && sealedRenderers.Length > 0) foil = sealedRenderers[0].sharedMaterial;
            // The strip that comes off is the band between the opened variant's cut and the sealed variant's top.
            shape = MeasureShape();
            strip = new MealTearStrip(transform, foil, shape);
            pouch.SetActive(false);
            pouchVisible = false;
        }

        private MealPouchShape MeasureShape()
        {
            if (!TryBoundsInPouch(sealedRenderers, out Bounds whole) || !TryBoundsInPouch(openedRenderers, out Bounds cut)) return MealPouchShape.Default;
            // The walls at the cut stand a little closer than the pouch is thick at its middle (0.83 for the kit's pouch).
            float walls = cut.extents.z * .83f;
            var measured = new MealPouchShape(whole.extents.x, cut.max.y, whole.max.y, walls);
            // A pouch without a separate cut (a primitive stand-in) tears at the kit's proportion.
            if (measured.StripHeight < .008f) measured = new MealPouchShape(whole.extents.x, whole.max.y - whole.size.y * .23f, whole.max.y, walls);
            return measured.IsValid ? measured : MealPouchShape.Default;
        }

        private bool TryBoundsInPouch(Renderer[] renderers, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            Matrix4x4 toPouch = pouch.transform.worldToLocalMatrix;
            foreach (Renderer renderer in renderers)
            {
                MeshFilter filter = renderer ? renderer.GetComponent<MeshFilter>() : null;
                if (!filter || !filter.sharedMesh) continue;
                Bounds mesh = filter.sharedMesh.bounds;
                Matrix4x4 matrix = toPouch * filter.transform.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = matrix.MultiplyPoint3x4(mesh.center + Vector3.Scale(mesh.extents,
                        new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f)));
                    if (!any) { bounds = new Bounds(point, Vector3.zero); any = true; }
                    else bounds.Encapsulate(point);
                }
            }
            return any;
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
            // Both variants share the prop origin: the tear swaps them in place (the strip covers the cut while it holds).
            PouchOpened = frame.Opened;
            foreach (Renderer r in sealedRenderers) if (r && r.enabled == frame.Opened) r.enabled = !frame.Opened;
            foreach (Renderer r in openedRenderers) if (r && r.enabled != frame.Opened) r.enabled = frame.Opened;
        }

        private void HidePouch()
        {
            if (pouch && pouch.activeSelf) pouch.SetActive(false);
            strip?.Hide();
            pouchVisible = false;
            PouchOpened = false;
            stripFree = false;
            TearProgress = 0f;
            Squeeze = 0f;
        }

        private void DestroyPouch()
        {
            strip?.Dispose(); strip = null;
            if (!pouch) return;
            if (Application.isPlaying) Destroy(pouch); else DestroyImmediate(pouch);
            pouch = null; pouchVisible = false;
            sealedRenderers = openedRenderers = Array.Empty<Renderer>();
            importedLabels = Array.Empty<Material>();
        }

        private static Pose Blend(Pose a, Pose b, float t) => new Pose(Vector3.Lerp(a.position, b.position, t), Quaternion.Slerp(a.rotation, b.rotation, t));
    }
}
