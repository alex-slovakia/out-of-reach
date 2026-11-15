using System;
using System.Collections.Generic;
using OutOfReach.Contracts;
using UnityEngine;

namespace OutOfReach.Presentation.Hands
{
    /// <summary>
    /// Poses the delivered rigid glove rig from its JSON and shows it only while Core says the suit is worn.
    /// Owns model visibility and the 34 rig joints, never the camera, motion mount, player or an interaction.
    /// Short local glove gestures acknowledge real actions; they do not assert contact, full-body IK or skinning.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(140)]
    public sealed class AstronautHandsPresenter : MonoBehaviour
    {
        [SerializeField] private Transform modelRoot;
        [Tooltip("Dedicated parent below the first-person camera. Only an explicitly bound motion presenter may animate it.")]
        [SerializeField] private Transform motionMount;
        [SerializeField] private TextAsset rigDefinition;

        [Header("Local gesture timing")]
        [SerializeField, Range(.35f, 1.15f)] private float pressSeconds = .72f;
        [SerializeField, Range(.35f, 1.15f)] private float gripSeconds = .88f;

        private enum Gesture { None, Press, Grip }
        private Gesture gesture;
        private float gestureElapsed;
        private float gestureDuration;
        private float gestureBlend;
        private bool gestureLeft;
        private Func<float> routeProgress;
        private float previousRouteDistance = float.NaN;
        private float travelSinceGrip;
        private bool nextGripLeft;

        private IEvaStatus suit;
        private IEvaStatus subscribedSuit;
        private IGameState state;
        private ISimulationClock clock;
        private Func<bool> firstPersonViewAllowed;
        private JointBinding[] bindings = Array.Empty<JointBinding>();
        private bool bound;
        private bool visible;

        public Transform ModelRoot => modelRoot;
        public Transform MotionMount => motionMount;
        public TextAsset RigDefinition => rigDefinition;
        public bool IsBound => bound;
        public bool IsGesturePlaying => gesture != Gesture.None;
        public float GestureBlend => gestureBlend;
        public float GestureElapsedSeconds => gestureElapsed;
        public bool IsVisible => visible && modelRoot && modelRoot.gameObject.activeInHierarchy;

        [Serializable] private sealed class RigFile
        {
            public RigJoint[] joints = Array.Empty<RigJoint>();
            public RigPose[] poses = Array.Empty<RigPose>();
        }
        [Serializable] private sealed class RigJoint
        {
            public string name = string.Empty;
            public string parent = string.Empty;
            public float[] position = Array.Empty<float>();
        }
        [Serializable] private sealed class RigPose
        {
            public string name = string.Empty;
            public JointRotation[] rotations = Array.Empty<JointRotation>();
            public ArmPlacement[] placements = Array.Empty<ArmPlacement>();
        }
        [Serializable] private sealed class JointRotation
        {
            public string name = string.Empty;
            public float[] rotation = Array.Empty<float>();
        }
        [Serializable] private sealed class ArmPlacement
        {
            public string name = string.Empty;
            public float[] position = Array.Empty<float>();
            public float[] rotation = Array.Empty<float>();
        }
        private sealed class JointBinding
        {
            public Transform Joint;
            public Vector3 AuthoredPosition;
            public Quaternion AuthoredRotation;
            public Vector3 NeutralPosition;
            public Quaternion NeutralRotation;
            public Vector3 PressPosition;
            public Quaternion PressRotation;
            public Vector3 GripPosition;
            public Quaternion GripRotation;
            public bool Left;
            public Vector3 GestureStartPosition;
            public Quaternion GestureStartRotation;
        }

        /// <summary>Editor/composition wiring only. Supply a scene instance of the prefab, never its FBX root.</summary>
        public void ConfigureModel(Transform prefabInstanceRoot, Transform dedicatedMotionMount, TextAsset rigJson)
        {
            if (Application.isPlaying && bound)
                throw new InvalidOperationException("Clear the hand session before replacing its model references.");
            if (!prefabInstanceRoot || !dedicatedMotionMount || !rigJson)
                throw new ArgumentException("Hands need an explicit prefab instance, separate mount and rig JSON.");
            if (prefabInstanceRoot.parent != dedicatedMotionMount || prefabInstanceRoot == transform
                || transform.IsChildOf(prefabInstanceRoot))
                throw new ArgumentException("The model must be a dedicated child of the mount and cannot contain this presenter.");
            ClearBinding();
            modelRoot = prefabInstanceRoot;
            motionMount = dedicatedMotionMount;
            rigDefinition = rigJson;
            HideModel();
        }

        /// <summary>
        /// Bind the real read-only suit/session views. The query must allow only the active first-person view,
        /// including EVA, and exclude menus/cinematics/control locks. Returns false with hidden hands on bad rig data.
        /// </summary>
        public bool ConfigureSession(IEvaStatus evaStatus, IGameState gameState,
            ISimulationClock simulationClock, Func<bool> isFirstPersonViewAllowed)
        {
            ClearBinding();
            if (evaStatus == null || gameState == null || simulationClock == null || isFirstPersonViewAllowed == null)
                throw new ArgumentNullException("Hands need the current suit, phase, clock and view-authority query.");
            try
            {
                bindings = ReadNeutralBindings();
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException)
            {
                Debug.LogError("Astronaut hands remain hidden: " + error.Message, this);
                return false;
            }
            suit = evaStatus;
            state = gameState;
            clock = simulationClock;
            firstPersonViewAllowed = isFirstPersonViewAllowed;
            bound = true;
            Subscribe();
            RefreshPresentation();
            return true;
        }

        /// <summary>Local confirmation only. Caller must first receive a successful real action.</summary>
        public bool TryPlayPress(bool leftHand = false) => TryStartGesture(Gesture.Press, leftHand);

        /// <summary>A short glove-flex check; it does not imply attachment to an object or rail.</summary>
        public bool TryPlayGrip(bool leftHand = false) => TryStartGesture(Gesture.Grip, leftHand);

        /// <summary>
        /// Optional actual EVA arc-distance query; return NaN when not on the route. No input/Player dependency.
        /// Each 0.65 m of real travel may start one alternating glove flex, never a claimed contact grip.
        /// </summary>
        public void ConfigureRouteProgress(Func<float> readRouteDistanceMetres)
        {
            routeProgress = readRouteDistanceMetres;
            ResetTravelSample();
        }

        /// <summary>Cancels the gesture and restores neutral joints, without moving the model root or mount.</summary>
        public void ResetToNeutral()
        {
            ForgetToolHold();
            ForgetContact();
            gesture = Gesture.None;
            gestureElapsed = gestureBlend = 0f;
            ResetTravelSample();
            ApplyGesturePose();
        }

        private bool TryStartGesture(Gesture requested, bool left)
        {
            RefreshPresentation();
            if (!IsVisible || clock.IsPaused || toolHoldActive || ContactStage != HandContactStage.None
                || (IsGesturePlaying && !(requested == Gesture.Press && gesture == Gesture.Grip)))
                return false; // Press may smoothly replace a glove flex; no queue or delayed feedback.
            foreach (JointBinding binding in bindings)
            {
                if (!binding.Joint) { ClearBinding(); return false; }
                binding.GestureStartPosition = binding.Joint.localPosition;
                binding.GestureStartRotation = binding.Joint.localRotation;
            }
            gesture = requested;
            gestureLeft = left;
            gestureElapsed = gestureBlend = 0f;
            gestureDuration = Mathf.Clamp(requested == Gesture.Press ? pressSeconds : gripSeconds, .35f, 1.15f);
            return true;
        }

        private void AdvanceGesture(float deltaSeconds)
        {
            if (!IsGesturePlaying) return;
            gestureElapsed = Mathf.Min(gestureDuration, gestureElapsed + Mathf.Max(0f, deltaSeconds));
            float t = gestureElapsed / gestureDuration;
            // Smooth approach (32%), brief apex (12%), smooth return (56%). No overshoot or residual pose.
            float ramp = t < .32f ? t / .32f : t < .44f ? 1f : (1f - t) / .56f;
            gestureBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(ramp));
            if (gestureElapsed >= gestureDuration) { gesture = Gesture.None; gestureBlend = 0f; }
            ApplyGesturePose();
        }

        private void ApplyGesturePose()
        {
            foreach (JointBinding binding in bindings)
            {
                if (!binding.Joint) { ClearBinding(); return; }
                bool activeHand = IsGesturePlaying && binding.Left == gestureLeft;
                Vector3 position = activeHand
                    ? (gesture == Gesture.Press ? binding.PressPosition : binding.GripPosition) : binding.NeutralPosition;
                Quaternion rotation = activeHand
                    ? (gesture == Gesture.Press ? binding.PressRotation : binding.GripRotation) : binding.NeutralRotation;
                bool approaching = IsGesturePlaying && gestureElapsed < gestureDuration * .32f;
                binding.Joint.SetLocalPositionAndRotation(
                    Vector3.Lerp(approaching ? binding.GestureStartPosition : binding.NeutralPosition, position, gestureBlend),
                    Quaternion.Slerp(approaching ? binding.GestureStartRotation : binding.NeutralRotation, rotation, gestureBlend));
            }
        }

        private void SampleRouteTravel()
        {
            if (routeProgress == null || !suit.IsOutside) { ResetTravelSample(); return; }
            float distance = routeProgress();
            if (!Finite(distance)) { ResetTravelSample(); return; }
            float delta = Finite(previousRouteDistance) ? Mathf.Abs(distance - previousRouteDistance) : 0f;
            previousRouteDistance = distance;
            if (delta > .5f) { travelSinceGrip = 0f; return; } // Reposition/re-entry is not a hand stroke.
            if (delta <= .0001f) return;
            travelSinceGrip = Mathf.Min(.65f, travelSinceGrip + delta);
            if (travelSinceGrip < .65f || IsGesturePlaying) return;
            if (TryPlayGrip(nextGripLeft))
            {
                nextGripLeft = !nextGripLeft;
                travelSinceGrip = 0f;
            }
        }

        private void ResetTravelSample()
        {
            previousRouteDistance = float.NaN;
            travelSinceGrip = 0f;
            nextGripLeft = false;
        }

        public void ClearBinding()
        {
            ForgetToolHold();
            ForgetContact();
            contactOrigin = null;
            contactModeAllowed = null;
            validateContact = null;
            Unsubscribe();
            gesture = Gesture.None;
            gestureElapsed = gestureBlend = 0f;
            routeProgress = null;
            ResetTravelSample();
            HideModel();
            RestoreAuthoredPose();
            bindings = Array.Empty<JointBinding>();
            bound = false;
            suit = null;
            state = null;
            clock = null;
            firstPersonViewAllowed = null;
        }

        private void Awake() => HideModel();
        private void OnEnable() { Subscribe(); RefreshPresentation(); }
        private void OnDisable() { Unsubscribe(); ResetToNeutral(); HideModel(); RestoreAuthoredPose(); }
        private void OnDestroy() => ClearBinding();
        private void LateUpdate()
        {
            RefreshPresentation();
            if (!IsVisible || clock.IsPaused) return; // Keep the last visible pose; never advance while paused.
            if (toolHoldActive) { AdvanceToolHold(Time.unscaledDeltaTime); return; }
            if (ContactStage != HandContactStage.None) { AdvanceContact(Time.unscaledDeltaTime); return; }
            SampleRouteTravel();
            AdvanceGesture(Time.unscaledDeltaTime);
        }

        private void Subscribe()
        {
            if (!bound || !isActiveAndEnabled || subscribedSuit != null) return;
            subscribedSuit = suit;
            subscribedSuit.Changed += RefreshPresentation;
        }

        private void Unsubscribe()
        {
            if (subscribedSuit == null) return;
            subscribedSuit.Changed -= RefreshPresentation;
            subscribedSuit = null;
        }

        private void RefreshPresentation()
        {
            bool validSession = bound && isActiveAndEnabled && modelRoot && motionMount
                && suit.IsSuitOn && IsFirstPersonPhase(state.Phase);
            if (!validSession)
            {
                ResetToNeutral();
                HideModel();
                return;
            }
            if (toolHoldActive && !TryToolPose(out _, out _, out _)) ResetToNeutral();
            if (clock.IsPaused) { CancelContact(); return; } // Cancel pending actions; freeze the visible pose until resume.
            if (!toolHoldActive && (firstPersonViewAllowed == null || !firstPersonViewAllowed()))
            {
                ResetToNeutral(); // Cinematic/menu/control lock cancels rather than queues a hidden gesture.
                HideModel();
                return;
            }
            if (visible && modelRoot.gameObject.activeSelf) return;
            foreach (JointBinding binding in bindings)
                if (!binding.Joint) { ClearBinding(); return; }
            ApplyGesturePose(); // On pause resume, preserve the exact frozen pose before showing the model.
            modelRoot.gameObject.SetActive(true);
            visible = true;
        }

        private void HideModel()
        {
            visible = false;
            // Never deactivate a parent of this component if a serialized reference was wired incorrectly.
            if (modelRoot && modelRoot != transform && !transform.IsChildOf(modelRoot))
                modelRoot.gameObject.SetActive(false);
        }

        private void RestoreAuthoredPose()
        {
            foreach (JointBinding binding in bindings)
                if (binding.Joint)
                    binding.Joint.SetLocalPositionAndRotation(binding.AuthoredPosition, binding.AuthoredRotation);
        }

        private JointBinding[] ReadNeutralBindings()
        {
            if (!modelRoot || !motionMount || !rigDefinition || modelRoot.parent != motionMount)
                throw new InvalidOperationException("Missing or mismatched model/mount/rig reference.");
            if (modelRoot == transform || transform.IsChildOf(modelRoot)
                || modelRoot.localPosition != Vector3.zero || modelRoot.localRotation != Quaternion.identity
                || modelRoot.localScale != Vector3.one)
                throw new InvalidOperationException("Use a separate prefab instance at its documented zero/identity/unit mount pose.");
            if (modelRoot.GetComponentsInChildren<MonoBehaviour>(true).Length != 0
                || modelRoot.GetComponentsInChildren<Camera>(true).Length != 0
                || modelRoot.GetComponentsInChildren<Collider>(true).Length != 0
                || modelRoot.GetComponentsInChildren<Rigidbody>(true).Length != 0
                || modelRoot.GetComponentsInChildren<Animator>(true).Length != 0
                || modelRoot.GetComponentsInChildren<Animation>(true).Length != 0
                || modelRoot.GetComponentsInChildren<AudioListener>(true).Length != 0)
                throw new InvalidOperationException("The delivered hand subtree must contain only its visual rig, without other writers or gameplay.");

            RigFile rig = JsonUtility.FromJson<RigFile>(rigDefinition.text);
            if (rig == null || rig.joints == null || rig.joints.Length != 34 || rig.poses == null)
                throw new InvalidOperationException("Expected the delivered 34-joint rig JSON.");
            // One scoped lookup of the explicitly supplied model; no scene searches or service discovery.
            var transforms = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (Transform node in modelRoot.GetComponentsInChildren<Transform>(true))
                if (node != modelRoot && !transforms.TryAdd(node.name, node))
                    throw new InvalidOperationException("Duplicate joint name: " + node.name);
            if (transforms.Count != 34) throw new InvalidOperationException("The prefab instance must have exactly 34 rig transforms.");

            var byName = new Dictionary<string, JointBinding>(StringComparer.Ordinal);
            var arms = new HashSet<string>(StringComparer.Ordinal);
            foreach (RigJoint joint in rig.joints)
            {
                if (joint == null || string.IsNullOrEmpty(joint.name) || !transforms.TryGetValue(joint.name, out Transform node))
                    throw new InvalidOperationException("A documented rig joint is missing from the prefab instance.");
                Transform parent = modelRoot;
                if (!string.IsNullOrEmpty(joint.parent) && !transforms.TryGetValue(joint.parent, out parent))
                    throw new InvalidOperationException("Missing documented parent for " + joint.name);
                Vector3 rest = Vector(joint.position);
                if (node.parent != parent || (node.localPosition - rest).sqrMagnitude > .000001f
                    || Quaternion.Angle(node.localRotation, Quaternion.identity) > .01f || node.localScale != Vector3.one)
                    throw new InvalidOperationException("Rig rest pose or parent differs from the delivered JSON at " + joint.name);
                var binding = new JointBinding { Joint = node, AuthoredPosition = node.localPosition,
                    AuthoredRotation = node.localRotation, NeutralPosition = rest, NeutralRotation = Quaternion.identity,
                    Left = joint.name.StartsWith("L_", StringComparison.Ordinal) };
                if (!byName.TryAdd(joint.name, binding)) throw new InvalidOperationException("Duplicate JSON joint: " + joint.name);
                if (parent == modelRoot) arms.Add(joint.name);
            }
            if (!arms.SetEquals(new[] { "R_Arm", "L_Arm" }))
                throw new InvalidOperationException("Expected the two documented top-level arms.");

            ReadPose(rig, byName, arms, "Neutral", (binding, position, rotation) =>
                { binding.NeutralPosition = position; binding.NeutralRotation = rotation; });
            ReadPose(rig, byName, arms, "Press", (binding, position, rotation) =>
                { binding.PressPosition = position; binding.PressRotation = rotation; });
            ReadPose(rig, byName, arms, "Grip", (binding, position, rotation) =>
                { binding.GripPosition = position; binding.GripRotation = rotation; });
            var result = new JointBinding[34];
            for (int i = 0; i < rig.joints.Length; i++) result[i] = byName[rig.joints[i].name];
            return result;
        }

        private static void ReadPose(RigFile rig, Dictionary<string, JointBinding> byName,
            HashSet<string> arms, string poseName, Action<JointBinding, Vector3, Quaternion> assign)
        {
            RigPose selected = null;
            foreach (RigPose pose in rig.poses)
                if (pose != null && pose.name == poseName)
                {
                    if (selected != null) throw new InvalidOperationException("Duplicate " + poseName + " pose.");
                    selected = pose;
                }
            if (selected == null || selected.rotations == null || selected.placements == null
                || selected.rotations.Length != 32 || selected.placements.Length != 2)
                throw new InvalidOperationException(poseName + " must define 32 rotations and both arm placements.");
            var assigned = new HashSet<string>(StringComparer.Ordinal);
            foreach (JointRotation rotation in selected.rotations)
            {
                if (rotation == null || !byName.TryGetValue(rotation.name ?? string.Empty, out JointBinding binding)
                    || arms.Contains(rotation.name) || !assigned.Add(rotation.name))
                    throw new InvalidOperationException("Invalid or duplicate " + poseName + " joint rotation.");
                assign(binding, binding.AuthoredPosition, Rotation(rotation.rotation));
            }
            foreach (ArmPlacement placement in selected.placements)
            {
                if (placement == null || !arms.Contains(placement.name ?? string.Empty) || !assigned.Add(placement.name))
                    throw new InvalidOperationException("Invalid or duplicate " + poseName + " arm placement.");
                assign(byName[placement.name], Vector(placement.position), Rotation(placement.rotation));
            }
            if (assigned.Count != 34) throw new InvalidOperationException(poseName + " does not cover every joint.");
        }

        [Header("Validated world contact (optional)")]
        [SerializeField, Range(.15f, .25f)] private float contactReachSeconds = .20f;
        [SerializeField, Range(.15f, .35f)] private float contactReturnSeconds = .24f;
        [SerializeField, Range(.35f, .8f)] private float contactReachMetres = .8f;
        private static readonly Vector3 PalmContactInHand = new Vector3(0f, -.025f, .055f);
        private static readonly Vector3 TubeCentreInHand = new Vector3(0f, -.033f, .082f);
        private Transform contactOrigin;
        private Func<bool> contactModeAllowed;
        private Func<HandContactSample, bool> validateContact, performContact;
        private Func<HandContactTarget?> readHeldContact;
        private HandContactTarget contactTarget;
        private ContactHand contactHand;
        private HandContactKind contactKind;
        private JointBinding contactArm, contactWrist;
        private float contactElapsed;
        private Vector3 previousContactPoint, previousContactOrigin;
        private bool hasContactSample;
        private int contactVersion;
        public HandContactStage ContactStage { get; private set; }
        public bool IsHoldingWorldContact => ContactStage == HandContactStage.Holding;
        public float ContactErrorMetres { get; private set; } = float.NaN;

        /// <summary>
        /// Optional root binding after ConfigureSession. Origin is the unbobbed eye/reach origin.
        /// Mode gate MUST exclude cargo, route, rest, menus, cinematics and all non-contact movement modes.
        /// Validation MUST confirm the actual collider, reach and unobstructed eye AND wrist-to-contact path;
        /// for Grip it must also verify a registered rail, its axis/radius and available hand authority.
        /// It is called throughout the reach/hold, not just on the initiating click.
        /// </summary>
        public void ConfigureContacts(Transform unbobbedReachOrigin, Func<bool> isContactModeAllowed,
            Func<HandContactSample, bool> isContactStillValid)
        {
            ResetToNeutral();
            if (!bound || !unbobbedReachOrigin || isContactModeAllowed == null || isContactStillValid == null)
                throw new ArgumentException("Bind the hand session, unbobbed origin and live contact validation first.");
            contactOrigin = unbobbedReachOrigin;
            contactModeAllowed = isContactModeAllowed;
            validateContact = isContactStillValid;
        }

        /// <summary>
        /// Starts anticipation, NOT a physics action. One selected hand at a time matches the current single-grip owner.
        /// The action is invoked once only after the rendered rig reaches the freshly validated real contact.
        /// Grip additionally requires an authoritative anchor query; null means released/invalid.
        /// Queries must be read-only. Action may reject; it must never partially succeed then return false.
        /// </summary>
        public bool TryReachContact(ContactHand hand, HandContactKind kind, HandContactTarget target,
            Func<HandContactSample, bool> tryActionAtContact, Func<HandContactTarget?> readAuthoritativeGrip = null)
        {
            RefreshPresentation();
            if (!CanPresentContact() || toolHoldActive || ContactStage != HandContactStage.None || tryActionAtContact == null
                || (hand != ContactHand.Left && hand != ContactHand.Right)
                || (kind != HandContactKind.Push && kind != HandContactKind.Grip)
                || (kind == HandContactKind.Grip && readAuthoritativeGrip == null)) return false;
            ResetToNeutral();
            contactHand = hand; contactKind = kind; contactTarget = target;
            foreach (JointBinding joint in bindings)
            {
                if (!joint.Joint) { ClearBinding(); return false; }
                if (joint.Left != (hand == ContactHand.Left)) continue;
                if (joint.Joint.name.EndsWith("_Arm", StringComparison.Ordinal)) contactArm = joint;
                if (joint.Joint.name.EndsWith("_Hand", StringComparison.Ordinal)) contactWrist = joint;
            }
            if (contactArm == null || contactWrist == null || contactWrist.AuthoredPosition != Vector3.zero
                || !TryContactPose(out _, out _, out _, out _)) { ForgetContact(); return false; }
            CaptureContactPose();
            performContact = tryActionAtContact; readHeldContact = readAuthoritativeGrip;
            contactElapsed = 0f;
            ContactStage = HandContactStage.Reaching;
            return true;
        }

        /// <summary>Presentation cancellation only: never issues Player.Release or another gameplay command.</summary>
        public void CancelContact()
        {
            if (ContactStage == HandContactStage.None || ContactStage == HandContactStage.Returning) return;
            CaptureContactPose();
            contactVersion++;
            performContact = null; readHeldContact = null; contactTarget = default;
            ContactErrorMetres = float.NaN;
            contactElapsed = 0f;
            ContactStage = HandContactStage.Returning;
        }

        private void ForgetContact()
        {
            contactVersion++;
            ContactStage = HandContactStage.None;
            performContact = null; readHeldContact = null; contactTarget = default;
            contactArm = contactWrist = null;
            hasContactSample = false;
            contactElapsed = 0f; ContactErrorMetres = float.NaN;
        }

        private bool CanPresentContact()
        {
            if (!IsVisible || clock == null || clock.IsPaused || !contactOrigin || contactModeAllowed == null
                || validateContact == null || suit.IsOutside || state.Phase == GamePhase.Eva) return false;
            // A real route sample is an independent veto even if the composition gate was misconfigured.
            try { return (routeProgress == null || !Finite(routeProgress())) && contactModeAllowed(); }
            catch (Exception error) { Debug.LogException(error, this); return false; }
        }

        private void CaptureContactPose()
        {
            foreach (JointBinding joint in bindings)
            {
                if (!joint.Joint) continue;
                joint.GestureStartPosition = joint.Joint.localPosition;
                joint.GestureStartRotation = joint.Joint.localRotation;
            }
        }

        private void AdvanceContact(float seconds)
        {
            if (!CanPresentContact()) { ResetToNeutral(); return; }
            if (!Finite(seconds) || seconds < 0f) { CancelContact(); return; }
            // A rendering hitch is not a lost contact. Discard its animation time (no catch-up/action
            // jump), but still run the normal authoritative hold, geometry and movement-jump checks.
            if (seconds > .1f) seconds = 0f;
            contactElapsed += seconds;
            if (ContactStage == HandContactStage.Returning)
            {
                float t = Mathf.Clamp01(contactElapsed / Mathf.Clamp(contactReturnSeconds, .15f, .35f));
                if (!BlendContactJoints(Mathf.SmoothStep(0f, 1f, t), true)) return;
                if (t >= 1f) ResetToNeutral();
                return;
            }
            if (ContactStage == HandContactStage.Holding)
            {
                HandContactTarget? held;
                int version = contactVersion;
                try { held = readHeldContact?.Invoke(); }
                catch (Exception error) { Debug.LogException(error, this); CancelContact(); return; }
                if (version != contactVersion) return;
                if (!held.HasValue || held.Value.Surface != contactTarget.Surface) { CancelContact(); return; }
                contactTarget = held.Value;
            }
            if (!TryContactPose(out HandContactSample sample, out Vector3 wristPosition,
                out Quaternion armRotation, out Quaternion handRotation)) { CancelContact(); return; }
            float reach = Mathf.Clamp01(contactElapsed / Mathf.Clamp(contactReachSeconds, .15f, .25f));
            float blend = ContactStage == HandContactStage.Reaching ? Mathf.SmoothStep(0f, 1f, reach) : 1f;
            if (!BlendContactJoints(blend, false)) return;
            contactArm.Joint.SetLocalPositionAndRotation(
                Vector3.Lerp(contactArm.GestureStartPosition, modelRoot.InverseTransformPoint(wristPosition), blend),
                Quaternion.Slerp(contactArm.GestureStartRotation, Quaternion.Inverse(modelRoot.rotation) * armRotation, blend));
            contactWrist.Joint.localRotation = Quaternion.Slerp(contactWrist.GestureStartRotation,
                Quaternion.Inverse(armRotation) * handRotation, blend);
            Vector3 reference = contactWrist.Joint.TransformPoint(contactKind == HandContactKind.Grip
                ? TubeCentreInHand : PalmContactInHand);
            if (contactKind == HandContactKind.Grip) reference += sample.Normal * sample.GripRadiusMetres;
            ContactErrorMetres = Vector3.Distance(reference, sample.Point);
            if (ContactStage == HandContactStage.PushContact)
            {
                if (contactElapsed >= .06f) CancelContact();
                return;
            }
            if (ContactStage != HandContactStage.Reaching || reach < 1f) return;
            if (ContactErrorMetres > .002f) { CancelContact(); return; }
            // Install the next state before outward code: a callback can teardown/rebind this same presenter.
            Func<HandContactSample, bool> action = performContact;
            performContact = null;
            ContactStage = HandContactStage.PushContact;
            contactElapsed = 0f;
            int actionVersion = contactVersion;
            bool accepted;
            try { accepted = action != null && action(sample); }
            catch (Exception error) { Debug.LogException(error, this); accepted = false; }
            if (actionVersion != contactVersion) return;
            if (!accepted) { CancelContact(); return; }
            if (contactKind == HandContactKind.Grip)
            {
                ContactStage = HandContactStage.Holding;
                // Verify success immediately; an accepted command alone does not establish a displayed grip.
                AdvanceContact(0f);
            }
        }

        private bool BlendContactJoints(float blend, bool returning)
        {
            foreach (JointBinding joint in bindings)
            {
                if (!joint.Joint) { ClearBinding(); return false; }
                if (joint.Left != (contactHand == ContactHand.Left)) continue;
                Quaternion target = returning ? joint.NeutralRotation
                    : contactKind == HandContactKind.Grip ? joint.GripRotation : joint.AuthoredRotation;
                Vector3 position = returning ? joint.NeutralPosition : joint.AuthoredPosition;
                joint.Joint.SetLocalPositionAndRotation(Vector3.Lerp(joint.GestureStartPosition, position, blend),
                    Quaternion.Slerp(joint.GestureStartRotation, target, blend));
            }
            return true;
        }

        private bool TryContactPose(out HandContactSample sample, out Vector3 wristPosition,
            out Quaternion armRotation, out Quaternion handRotation)
        {
            sample = default; wristPosition = default; armRotation = handRotation = Quaternion.identity;
            Transform surface = contactTarget.Surface;
            if (!surface || !surface.gameObject.activeInHierarchy || !contactOrigin || !modelRoot
                || contactArm == null || !contactArm.Joint || contactWrist == null || !contactWrist.Joint
                || (modelRoot.lossyScale - Vector3.one).sqrMagnitude > .000001f
                || !FiniteVector(contactTarget.LocalPoint) || !FiniteVector(contactTarget.LocalNormal)
                || !FiniteVector(contactTarget.LocalTangent)) return false;
            Vector3 point = surface.TransformPoint(contactTarget.LocalPoint);
            Vector3 normal = surface.localToWorldMatrix.inverse.transpose.MultiplyVector(contactTarget.LocalNormal).normalized;
            Vector3 tangent = Vector3.ProjectOnPlane(surface.TransformVector(contactTarget.LocalTangent), normal).normalized;
            if (!FiniteVector(point) || !FiniteVector(normal) || !FiniteVector(tangent)
                || normal.sqrMagnitude < .99f || tangent.sqrMagnitude < .99f) return false;
            if (hasContactSample && (Vector3.Distance(previousContactPoint, point) > .12f
                || Vector3.Distance(previousContactOrigin, contactOrigin.position) > .25f)) return false;
            Vector3 eyeToPoint = point - contactOrigin.position;
            Vector3 local = Quaternion.Inverse(contactOrigin.rotation) * eyeToPoint;
            float sign = contactHand == ContactHand.Left ? -1f : 1f;
            if (eyeToPoint.magnitude > Mathf.Clamp(contactReachMetres, .35f, .8f) || local.z < .12f
                || Mathf.Abs(local.x) > .55f || local.y < -.55f || local.y > .45f || local.x * sign < -.18f
                || Vector3.Dot(normal, -eyeToPoint.normalized) < .15f) return false;
            Vector3 reference = PalmContactInHand;
            Vector3 anchor = point;
            if (contactKind == HandContactKind.Grip)
            {
                if (!Finite(contactTarget.GripRadiusMetres) || contactTarget.GripRadiusMetres < .012f
                    || contactTarget.GripRadiusMetres > .017f) return false;
                reference = TubeCentreInHand;
                anchor -= normal * contactTarget.GripRadiusMetres;
                handRotation = Quaternion.LookRotation(Vector3.Cross(tangent, normal), normal);
            }
            else handRotation = Quaternion.LookRotation(tangent, normal);
            wristPosition = anchor - handRotation * reference;
            // This is bounded rigid forearm placement, not a shoulder/elbow IK chain.
            Vector3 shoulderProxy = contactOrigin.position + contactOrigin.rotation * new Vector3(sign * .18f, -.28f, -.08f);
            Vector3 forearmForward = wristPosition - shoulderProxy;
            if (forearmForward.magnitude < .18f || forearmForward.magnitude > .72f) return false;
            Vector3 armUp = Vector3.ProjectOnPlane(normal, forearmForward.normalized);
            if (armUp.sqrMagnitude < .01f) armUp = Vector3.ProjectOnPlane(contactOrigin.up, forearmForward.normalized);
            if (armUp.sqrMagnitude < .01f) return false;
            armRotation = Quaternion.LookRotation(forearmForward, armUp);
            if (Quaternion.Angle(armRotation, handRotation) > 80f) return false;
            sample = new HandContactSample(contactHand, contactKind, contactTarget, point, normal, tangent,
                contactWrist.Joint.position, wristPosition);
            int version = contactVersion;
            try
            {
                if (validateContact == null || !validateContact(sample) || version != contactVersion) return false;
                previousContactPoint = point; previousContactOrigin = contactOrigin.position; hasContactSample = true;
                return true;
            }
            catch (Exception error) { Debug.LogException(error, this); return false; }
        }

        // Tool-only calibration of the original rigid rig against the actual 35 mm antenna wrench.
        // No ordinary Grip/Press/Neutral data is changed. The root owns the tool animation and repair command.
        private static readonly Vector3 ToolGripCentre = new Vector3(0f, -.0426f, .086f);
        private const float ToolReachSeconds = .20f;
        private Transform toolGripFrame;
        private Func<bool> toolLeaseStillValid;
        [SerializeField, Tooltip("Actual EVA upperarm01.R, read only. Optional fixed 350/320 mm view-arm fit, not a copy of the shorter body skeleton.")]
        private Transform toolSleeveShoulder;
        [SerializeField] private EvaToolSleevePresenter toolSleeveVisual;
        [SerializeField] private bool useToolSleeve;
        public const float ToolUpperSleeveMetres = .35f;
        public const float ToolForearmMetres = .32f;
        public Transform ToolSleeveShoulder => toolSleeveShoulder;
        public Vector3 ToolElbowWorldPosition => toolArm != null && toolArm.Joint
            ? toolArm.Joint.TransformPoint(new Vector3(0f, 0f, -ToolForearmMetres)) : Vector3.zero;
        public Vector3 ToolWristWorldPosition => toolWrist != null && toolWrist.Joint ? toolWrist.Joint.position : Vector3.zero;
        public bool IsToolHoldPaused => clock != null && clock.IsPaused;
        private Vector3 previousToolShoulder;

        /// <summary>Explicit scene binding; never discovers or writes body bones. Rebinding cancels an old visual lease.</summary>
        public void ConfigureToolSleeveShoulder(Transform actualEvaShoulder, EvaToolSleevePresenter sleeve)
        {
            EndToolHold();
            toolSleeveShoulder = actualEvaShoulder; toolSleeveVisual = sleeve; useToolSleeve = true;
        }

        private JointBinding toolArm, toolWrist;
        private int toolLeaseVersion;
        private float toolReachElapsed;
        private bool toolHoldActive, hasToolSample;
        private Vector3 previousToolPoint, previousToolOrigin;
        private Quaternion previousToolRotation;
        public bool IsToolHoldActive => toolHoldActive;
        public bool IsToolHoldSettled => toolHoldActive && toolReachElapsed >= ToolReachSeconds && ToolHoldReferenceErrorMetres <= .002f;
        public float ToolHoldReferenceErrorMetres { get; private set; } = float.NaN;
        public static Vector3 ToolGripCentreInHand => ToolGripCentre;

        /// <summary>
        /// Begins a right-hand visual lease, not a repair. Bind before applying the root movement lock.
        /// Frame position is the actual 35 mm tube centre; +X is its axis, +Z calibrated hand forward.
        /// Hold the tool at its initial frame until IsToolHoldSettled, then animate the real source.
        /// Read-only validity must exclude menus/cinematics/session changes but remain true on valid pause.
        /// It must validate the live worksite/reach/collider paths; this presenter owns no physics command.
        /// </summary>
        public bool TryBeginToolHold(Transform gripFrame, Func<bool> leaseStillValid)
        {
            RefreshPresentation();
            LastToolHoldRejection = string.Empty;
            if (!IsVisible || clock == null || clock.IsPaused || toolHoldActive || ContactStage != HandContactStage.None
                || !gripFrame || leaseStillValid == null || state.Phase != GamePhase.Eva || !suit.IsOutside) return RejectToolHold("Acquisition precondition; visible=" + IsVisible + ", contact=" + ContactStage + ", existingLease=" + toolHoldActive);
            foreach (JointBinding joint in bindings)
            {
                if (!joint.Joint) { ClearBinding(); return RejectToolHold("Missing original joint."); }
                if (joint.Joint.name == "R_Arm") toolArm = joint;
                if (joint.Joint.name == "R_Hand") toolWrist = joint;
                joint.GestureStartPosition = joint.Joint.localPosition;
                joint.GestureStartRotation = joint.Joint.localRotation;
            }
            if (toolArm == null || toolWrist == null || toolWrist.AuthoredPosition != Vector3.zero) return RejectToolHold("Invalid original arm/wrist binding.");
            toolGripFrame = gripFrame; toolLeaseStillValid = leaseStillValid;
            toolHoldActive = true; hasToolSample = false; toolReachElapsed = 0f; toolLeaseVersion++;
            if (!TryToolPose(out _, out _, out _)) { ForgetToolHold(); return false; }
            gesture = Gesture.None; gestureElapsed = gestureBlend = 0f; ResetTravelSample();
            return true;
        }

        /// <summary>
        /// Applies a newly sampled tool frame through this same owner, without advancing reach or work time.
        /// The caller must already hold a settled lease. Pause freezes; a final stale/invalid contact cannot succeed.
        /// </summary>
        public bool RefreshToolHoldFrame()
        {
            if (!toolHoldActive || !IsToolHoldSettled || !IsVisible || clock == null || clock.IsPaused) return false;
            int version = toolLeaseVersion;
            if (!TryToolPose(out _, out _, out _)) { EndToolHold(); return false; }
            AdvanceToolHold(0f);
            if (version != toolLeaseVersion || !toolHoldActive || !IsToolHoldSettled) return false;
            if (!TryToolPose(out _, out _, out _)) { EndToolHold(); return false; }
            if (version != toolLeaseVersion || !toolHoldActive || !toolWrist.Joint || !toolGripFrame) return false;
            float actual = Vector3.Distance(toolWrist.Joint.TransformPoint(ToolGripCentre), toolGripFrame.position);
            ToolHoldReferenceErrorMetres = actual;
            if (!Finite(actual) || actual > .002f) { EndToolHold(); return false; }
            return true;
        }

        /// <summary>Idempotent visual cancellation. Never repairs, consumes, releases Player or completes an interaction.</summary>
        public void EndToolHold()
        {
            if (!toolHoldActive) return;
            ResetToNeutral();
        }

        private void ForgetToolHold()
        {
            toolLeaseVersion++; toolHoldActive = false; toolGripFrame = null; toolLeaseStillValid = null;
            toolArm = toolWrist = null; toolReachElapsed = 0f; hasToolSample = false;
            ToolHoldReferenceErrorMetres = float.NaN;
        }

        // Read-only last rejection, retained after cancellation so native QA can identify the actual guard.
        public string LastToolHoldRejection { get; private set; } = string.Empty;
        private bool RejectToolHold(string reason) { LastToolHoldRejection = reason; return false; }

        private bool TryToolPose(out Vector3 wristPosition, out Quaternion armRotation, out Quaternion handRotation)
        {
            wristPosition = default; armRotation = handRotation = Quaternion.identity;
            if (!toolHoldActive || !bound || !isActiveAndEnabled || !toolGripFrame || !toolGripFrame.gameObject.activeInHierarchy
                || !motionMount || !modelRoot || toolArm == null || !toolArm.Joint || toolWrist == null || !toolWrist.Joint
                || toolGripFrame == modelRoot || toolGripFrame.IsChildOf(modelRoot)
                || state == null || state.Phase != GamePhase.Eva || suit == null || !suit.IsSuitOn || !suit.IsOutside
                || (toolGripFrame.lossyScale - Vector3.one).sqrMagnitude > .000001f
                || (modelRoot.lossyScale - Vector3.one).sqrMagnitude > .000001f) return RejectToolHold("Session, source, rig or scale invalid.");
            int version = toolLeaseVersion; bool valid;
            try { valid = toolLeaseStillValid != null && toolLeaseStillValid(); }
            catch (Exception error) { Debug.LogException(error, this); return RejectToolHold("Root validity callback threw."); }
            if (!valid || version != toolLeaseVersion || !toolHoldActive || !toolGripFrame) return RejectToolHold("Root validity callback or lease identity rejected.");
            Vector3 point = toolGripFrame.position, origin = motionMount.position;
            handRotation = toolGripFrame.rotation;
            if (!FiniteVector(point) || !FiniteVector(origin) || !Finite(handRotation.x) || !Finite(handRotation.y)
                || !Finite(handRotation.z) || !Finite(handRotation.w)) return RejectToolHold("Non-finite source frame.");
            if (hasToolSample && (Vector3.Distance(previousToolPoint, point) > .12f || Vector3.Distance(previousToolOrigin, origin) > .25f)) return RejectToolHold("Source or view origin jumped.");
            if (clock.IsPaused && hasToolSample && (Vector3.Distance(previousToolPoint, point) > .00002f
                || Quaternion.Angle(previousToolRotation, handRotation) > .02f)) return RejectToolHold("Tool frame changed during pause.");
            Vector3 delta = point - origin, local = Quaternion.Inverse(motionMount.rotation) * delta;
            if (delta.magnitude > .8f || local.z < .12f || Mathf.Abs(local.x) > .55f || local.y < -.55f || local.y > .45f) return RejectToolHold("Eye envelope rejected: distance=" + delta.magnitude + ", local=" + local + "");
            wristPosition = point - handRotation * ToolGripCentre;
            Vector3 shoulder = origin + motionMount.rotation * new Vector3(.18f, -.28f, -.08f);
            Vector3 forearm = wristPosition - shoulder;
            if (forearm.magnitude < .18f || forearm.magnitude > .72f) return RejectToolHold("Original camera-shoulder reach rejected: " + forearm.magnitude + "");
            // Preserve the existing camera/reach and wrist-angle guards. Only this explicit view-arm fit adds
            // a real bent upper segment; neither the original hand nor its 320 mm forearm is scaled.
            if (useToolSleeve)
            {
                if (!toolSleeveShoulder || !toolSleeveVisual || !toolSleeveVisual.IsReady || !toolSleeveShoulder.gameObject.activeInHierarchy || !FiniteVector(toolSleeveShoulder.position)) return RejectToolHold("Explicit sleeve/shoulder is unavailable or inactive.");
                Vector3 actualShoulder = toolSleeveShoulder.position;
                if (clock.IsPaused && hasToolSample && Vector3.Distance(previousToolShoulder, actualShoulder) > .00002f) return RejectToolHold("Actual shoulder changed during pause.");
                Vector3 axis = wristPosition - actualShoulder;
                float distance = axis.magnitude;
                if (distance < .10f || distance >= ToolUpperSleeveMetres + ToolForearmMetres) return RejectToolHold("Fixed350/320 reach rejected: " + distance + "");
                axis /= distance;
                // A predominantly downward elbow avoids the old outward-heavy pole's 84-degree initial wrist twist.
                // Original fixed lengths, grip and 80-degree wrist guard are unchanged.
                Vector3 bend = Vector3.ProjectOnPlane(motionMount.right * .10f - motionMount.up * .9949874f, axis);
                if (bend.sqrMagnitude < .01f) return RejectToolHold("Elbow pole projection degenerate.");
                float along = (ToolUpperSleeveMetres * ToolUpperSleeveMetres - ToolForearmMetres * ToolForearmMetres
                    + distance * distance) / (2f * distance);
                float heightSquared = ToolUpperSleeveMetres * ToolUpperSleeveMetres - along * along;
                if (heightSquared <= 0f) return RejectToolHold("Fixed-segment elbow has no real solution.");
                Vector3 elbow = actualShoulder + axis * along + bend.normalized * Mathf.Sqrt(heightSquared);
                forearm = wristPosition - elbow;
                if (!clock.IsPaused) previousToolShoulder = actualShoulder;
            }
            Vector3 up = Vector3.ProjectOnPlane(handRotation * Vector3.up, forearm.normalized);
            if (up.sqrMagnitude < .01f) return RejectToolHold("Forearm up projection degenerate.");
            armRotation = Quaternion.LookRotation(forearm, up);
            if (Quaternion.Angle(armRotation, handRotation) > 80f) return RejectToolHold("Wrist angle exceeds unchanged80deg: " + Quaternion.Angle(armRotation, handRotation) + "");
            if (!clock.IsPaused)
            {
                previousToolPoint = point; previousToolOrigin = origin; previousToolRotation = handRotation; hasToolSample = true;
            }
            return true;
        }

        private void AdvanceToolHold(float seconds)
        {
            if (!TryToolPose(out Vector3 wristPosition, out Quaternion armRotation, out Quaternion handRotation)) { EndToolHold(); return; }
            if (!Finite(seconds) || seconds < 0f) { EndToolHold(); return; }
            // Ignore animation time from a hitch; keep the live geometric lease rather than skipping its reach.
            if (seconds <= .1f) toolReachElapsed = Mathf.Min(ToolReachSeconds, toolReachElapsed + seconds);
            float blend = Mathf.SmoothStep(0f, 1f, toolReachElapsed / ToolReachSeconds);
            foreach (JointBinding joint in bindings)
            {
                if (!joint.Joint) { ClearBinding(); return; }
                if (joint.Left)
                {
                    joint.Joint.SetLocalPositionAndRotation(Vector3.Lerp(joint.GestureStartPosition, joint.NeutralPosition, blend),
                        Quaternion.Slerp(joint.GestureStartRotation, joint.NeutralRotation, blend));
                    continue;
                }
                if (joint == toolArm || joint == toolWrist) continue;
                joint.Joint.SetLocalPositionAndRotation(Vector3.Lerp(joint.GestureStartPosition, joint.AuthoredPosition, blend),
                    Quaternion.Slerp(joint.GestureStartRotation, ToolFingerRotation(joint), blend));
            }
            toolArm.Joint.SetLocalPositionAndRotation(Vector3.Lerp(toolArm.GestureStartPosition, modelRoot.InverseTransformPoint(wristPosition), blend),
                Quaternion.Slerp(toolArm.GestureStartRotation, Quaternion.Inverse(modelRoot.rotation) * armRotation, blend));
            toolWrist.Joint.SetLocalPositionAndRotation(toolWrist.AuthoredPosition,
                Quaternion.Slerp(toolWrist.GestureStartRotation, Quaternion.Inverse(armRotation) * handRotation, blend));
            ToolHoldReferenceErrorMetres = Vector3.Distance(toolWrist.Joint.TransformPoint(ToolGripCentre), toolGripFrame.position);
            // Progress may update this rig in Update, and pause may begin before the passive sleeve's
            // LateUpdate. Commit the same settled frame now so its strict pause guard sees no stale elbow.
            // This follows the already-applied pose; it advances no animation/work time and writes no bones.
            if (useToolSleeve && toolSleeveVisual && IsToolHoldSettled) toolSleeveVisual.RefreshVisual();
        }

        private static Quaternion ToolFingerRotation(JointBinding joint)
        {
            switch (joint.Joint.name)
            {
                case "R_Thumb1": return Quaternion.AngleAxis(23f, new Vector3(0.47499f, 0.54297f, 0.69251f)) * joint.GripRotation;
                case "R_Thumb2": return Quaternion.AngleAxis(16f, new Vector3(0.47499f, 0.54297f, 0.69251f)) * joint.GripRotation;
                case "R_Thumb3": return Quaternion.AngleAxis(-13.25f, new Vector3(0.47499f, 0.54297f, 0.69251f)) * joint.GripRotation;
                case "R_Index1": return Quaternion.AngleAxis(-8f, new Vector3(0.99756f, 0f, 0.06976f)) * joint.GripRotation;
                case "R_Index2": return Quaternion.AngleAxis(-15f, new Vector3(0.99756f, 0f, 0.06976f)) * joint.GripRotation;
                case "R_Middle1": return Quaternion.AngleAxis(-16f, new Vector3(0.99985f, 0f, 0.01745f)) * joint.GripRotation;
                case "R_Ring1": return Quaternion.AngleAxis(-10f, new Vector3(0.99939f, 0f, -0.0349f)) * joint.GripRotation;
                case "R_Ring2": return Quaternion.AngleAxis(-15.5f, new Vector3(0.99939f, 0f, -0.0349f)) * joint.GripRotation;
                case "R_Little1": return Quaternion.AngleAxis(-32f, new Vector3(0.99452f, 0f, -0.10453f)) * joint.GripRotation;
                case "R_Little2": return Quaternion.AngleAxis(-16f, new Vector3(0.99452f, 0f, -0.10453f)) * joint.GripRotation;
                case "R_Little3": return Quaternion.AngleAxis(-14f, new Vector3(0.99452f, 0f, -0.10453f)) * joint.GripRotation;
                default: return joint.GripRotation;
            }
        }

        private static bool FiniteVector(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);

        private static Vector3 Vector(float[] values)
        {
            if (values == null || values.Length != 3 || !Finite(values[0]) || !Finite(values[1]) || !Finite(values[2]))
                throw new InvalidOperationException("Invalid rig position.");
            return new Vector3(values[0], values[1], values[2]);
        }

        private static Quaternion Rotation(float[] values)
        {
            if (values == null || values.Length != 4 || !Finite(values[0]) || !Finite(values[1])
                || !Finite(values[2]) || !Finite(values[3])) throw new InvalidOperationException("Invalid rig quaternion.");
            var rotation = new Quaternion(values[0], values[1], values[2], values[3]);
            float norm = Quaternion.Dot(rotation, rotation);
            if (Mathf.Abs(norm - 1f) > .02f) throw new InvalidOperationException("Rig quaternion must have unit length.");
            return Quaternion.Normalize(rotation);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsFirstPersonPhase(GamePhase phase) => phase == GamePhase.Intro
            || phase == GamePhase.Emergency || phase == GamePhase.Repair
            || phase == GamePhase.Eva || phase == GamePhase.Transmission;
    }
}
