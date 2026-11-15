using System;
using System.Collections.Generic;
using OutOfReach.Contracts;
using UnityEngine;

namespace OutOfReach.Presentation.Cargo
{
    /// <summary>Separate work-glove instance holding actual preflight cargo. No suit, inventory or input authority.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(180)]
    public sealed class PreflightCargoHandsPresenter : MonoBehaviour
    {
        [SerializeField] private Transform modelRoot;
        [SerializeField] private Transform settlingRoot;
        [SerializeField] private Transform leftGripFrame, rightGripFrame;
        [SerializeField] private TextAsset rigDefinition;
        [SerializeField, Range(.1f, .5f)] private float pickupSeconds = .24f;
        [Tooltip("Roll around each real handle tube so both forearms approach from the lower outer view.")]
        [SerializeField, Range(0f, 50f)] private float carryGripRollDegrees = 42f;
        [Tooltip("Mirrored wrist adjustment aims the sleeves toward the carrier while fingers keep their authored grip.")]
        [SerializeField, Range(0f, 35f)] private float carryWristYawCorrectionDegrees = 28f;
        private const float FollowEyeDownDegrees = 20f;
        private const float MaximumCargoDownDegrees = 28f;
        private static readonly Vector3 GripPointInHand = new Vector3(0f, -.033f, .082f);
        private static readonly Vector3 PickupOffset = new Vector3(0f, -.018f, -.025f);
        private ICargoLoading cargo, subscribedCargo;
        private IGameState state;
        private ISimulationClock clock;
        private Func<bool> viewAllowed;
        private JointBinding[] joints = Array.Empty<JointBinding>();
        private JointBinding leftArm, rightArm, leftHand, rightHand;
        private Vector3 neutralPosition;
        private Quaternion neutralRotation;
        private string observedHeld;
        private float pickupElapsed;
        private bool bound;
        private bool reducedMotion;

        public void SetReducedMotion(bool reduced) => reducedMotion = reduced;

        public bool IsBound => bound;
        public bool IsVisible => bound && modelRoot && modelRoot.gameObject.activeInHierarchy;
        public float PickupElapsedSeconds => pickupElapsed;
        public Transform ModelRoot => modelRoot;
        public Transform SettlingRoot => settlingRoot;
        public float MaximumGripErrorMetres => !bound ? float.NaN : Mathf.Max(
            Vector3.Distance(leftHand.Node.TransformPoint(GripPointInHand), leftGripFrame.position),
            Vector3.Distance(rightHand.Node.TransformPoint(GripPointInHand), rightGripFrame.position));

#pragma warning disable 0649
        [Serializable] private sealed class RigFile { public RigJoint[] joints; public RigPose[] poses; }
        [Serializable] private sealed class RigJoint { public string name, parent; public float[] position; }
        [Serializable] private sealed class RigPose { public string name; public RigRotation[] rotations; }
        [Serializable] private sealed class RigRotation { public string name; public float[] rotation; }
#pragma warning restore 0649
        private sealed class JointBinding
        {
            public Transform Node;
            public Vector3 RestPosition;
            public Quaternion RestRotation, GripRotation;
        }

        /// <summary>Both grip frames: X is tube axis, Y points away from the crate, Z points up its side.</summary>
        public void ConfigureScene(Transform separateGloveInstance, Transform sharedCargoSettlingRoot,
            Transform leftHandleFrame, Transform rightHandleFrame, TextAsset deliveredRig)
        {
            ClearBinding();
            if (!separateGloveInstance || !sharedCargoSettlingRoot || !leftHandleFrame || !rightHandleFrame || !deliveredRig)
                throw new ArgumentException("Bind the separate gloves, shared visual root, two handle frames and delivered rig.");
            if (transform == sharedCargoSettlingRoot || transform.IsChildOf(sharedCargoSettlingRoot)
                || !separateGloveInstance.IsChildOf(sharedCargoSettlingRoot)
                || !leftHandleFrame.IsChildOf(sharedCargoSettlingRoot) || !rightHandleFrame.IsChildOf(sharedCargoSettlingRoot))
                throw new ArgumentException("Keep the presenter outside its dedicated visibility root; crate, grips and gloves must share that root.");
            modelRoot = separateGloveInstance;
            settlingRoot = sharedCargoSettlingRoot;
            leftGripFrame = leftHandleFrame;
            rightGripFrame = rightHandleFrame;
            rigDefinition = deliveredRig;
            Hide();
        }

        public void Configure(ICargoLoading cargoView, IGameState gameState, ISimulationClock simulationClock,
            Func<bool> isCurrentFirstPersonViewAllowed)
        {
            ClearBinding();
            if (cargoView == null || gameState == null || simulationClock == null || isCurrentFirstPersonViewAllowed == null)
                throw new ArgumentNullException("Bind current cargo, phase, clock and control/menu/cinematic gate.");
            ValidateAndReadRig();
            neutralPosition = settlingRoot.localPosition;
            neutralRotation = settlingRoot.localRotation;
            cargo = cargoView; state = gameState; clock = simulationClock; viewAllowed = isCurrentFirstPersonViewAllowed;
            bound = true;
            if (isActiveAndEnabled) { Subscribe(); Refresh(); }
        }

        public void ClearBinding()
        {
            Unsubscribe();
            Hide();
            if (bound && settlingRoot) settlingRoot.SetLocalPositionAndRotation(neutralPosition, neutralRotation);
            foreach (JointBinding joint in joints)
                if (joint.Node) joint.Node.SetLocalPositionAndRotation(joint.RestPosition, joint.RestRotation);
            joints = Array.Empty<JointBinding>();
            leftArm = rightArm = leftHand = rightHand = null;
            cargo = null; state = null; clock = null; viewAllowed = null;
            observedHeld = null; pickupElapsed = 0f; bound = false;
        }

        private void Awake() => Hide();
        private void OnEnable() { if (bound) { Subscribe(); Refresh(); } else Hide(); }
        private void OnDisable() { Unsubscribe(); Hide(); }
        private void OnDestroy() => ClearBinding();
        private void Subscribe()
        {
            if (subscribedCargo != null || cargo == null) return;
            subscribedCargo = cargo; subscribedCargo.Changed += Refresh;
        }
        private void Unsubscribe()
        {
            if (subscribedCargo != null) subscribedCargo.Changed -= Refresh;
            subscribedCargo = null;
        }

        private void LateUpdate()
        {
            Refresh();
            if (!IsVisible || clock.IsPaused) return;
            pickupElapsed = Mathf.Min(Mathf.Max(.1f, pickupSeconds), pickupElapsed + Mathf.Max(0f, Time.unscaledDeltaTime));
            ApplyPose();
        }

        // Cargo's event hides immediately on a real stow/return; polling also covers pause, phase and gate changes.
        public void Refresh()
        {
            if (!bound || !isActiveAndEnabled) { Hide(); return; }
            string held = state.Phase == GamePhase.Planning ? cargo.CarriedEquipmentId : null;
            if (!string.Equals(held, observedHeld, StringComparison.Ordinal))
            {
                observedHeld = held; pickupElapsed = 0f;
                if (string.IsNullOrEmpty(held)) settlingRoot.SetLocalPositionAndRotation(neutralPosition, neutralRotation);
            }
            if (string.IsNullOrEmpty(held)) { Hide(); return; }
            if (clock.IsPaused) return; // Preserve the last visible pose under the pause menu.
            if (!viewAllowed()) { Hide(); return; }
            // Place joints before enabling renderers; the original cargo presenter still owns its child visibility.
            ApplyPose();
            modelRoot.gameObject.SetActive(true);
            settlingRoot.gameObject.SetActive(true);
        }

        private void ApplyPose()
        {
            float progress = reducedMotion ? 1f : Mathf.Clamp01(pickupElapsed / Mathf.Max(.1f, pickupSeconds));
            float remainder = 1f - Mathf.SmoothStep(0f, 1f, progress);
            Quaternion correction = CarryPitchCorrection(settlingRoot.parent);
            // Bend the whole assembly about the eye so the near edge of the cradle stays ahead of the legs.
            settlingRoot.SetLocalPositionAndRotation(correction * (neutralPosition + PickupOffset * remainder),
                correction * neutralRotation);
            foreach (JointBinding joint in joints)
                joint.Node.SetLocalPositionAndRotation(joint.RestPosition, joint.GripRotation);
            // The mirrored frames share local X as their tube axis. The same roll
            // fans both forearms outward without tilting the fingers off the bar.
            Quaternion carryRoll = Quaternion.AngleAxis(Mathf.Clamp(carryGripRollDegrees, 0f, 50f), Vector3.right);
            float wristYaw = Mathf.Clamp(carryWristYawCorrectionDegrees, 0f, 35f);
            SeatArm(leftArm, leftHand, leftGripFrame, carryRoll, wristYaw);
            SeatArm(rightArm, rightHand, rightGripFrame, carryRoll, -wristYaw);
        }

        private static Quaternion CarryPitchCorrection(Transform eye)
        {
            if (!eye) return Quaternion.identity;
            Vector3 forward = eye.forward;
            Vector3 horizonRight = Vector3.Cross(Vector3.up, forward);
            float lookDown = Mathf.Atan2(-forward.y, horizonRight.magnitude) * Mathf.Rad2Deg;
            if (lookDown <= FollowEyeDownDegrees) return Quaternion.identity;

            // Match the initial eye motion and ease to a fixed carry angle as the view continues down.
            float range = MaximumCargoDownDegrees - FollowEyeDownDegrees;
            float t = Mathf.Clamp01((lookDown - FollowEyeDownDegrees) / (2f * range));
            float cargoPitch = FollowEyeDownDegrees + range * t * (2f - t);
            if (horizonRight.sqrMagnitude < 1e-8f) horizonRight = eye.right;
            return Quaternion.AngleAxis(cargoPitch - lookDown, eye.InverseTransformDirection(horizonRight.normalized));
        }

        private static void SeatArm(JointBinding arm, JointBinding hand, Transform grip, Quaternion carryRoll, float wristYaw)
        {
            // Grip's camera-facing wrist pose originally points the sleeve into
            // the crate. Adjust only the wrist; preserve all finger rotations.
            hand.Node.localRotation = hand.GripRotation * Quaternion.AngleAxis(wristYaw, Vector3.up);
            Quaternion handRotation = grip.rotation * carryRoll;
            Quaternion armRotation = handRotation * Quaternion.Inverse(hand.Node.localRotation);
            // Solve the wrist around the unchanged contact point, rather than
            // translating a posed glove away from its actual handle to expose it.
            arm.Node.SetPositionAndRotation(grip.position - handRotation * GripPointInHand, armRotation);
        }

        private void Hide()
        {
            if (modelRoot) modelRoot.gameObject.SetActive(false);
            if (settlingRoot && transform != settlingRoot && !transform.IsChildOf(settlingRoot)) settlingRoot.gameObject.SetActive(false);
        }

        private void ValidateAndReadRig()
        {
            if (!modelRoot || !settlingRoot || !leftGripFrame || !rightGripFrame || !rigDefinition)
                throw new InvalidOperationException("Missing cargo glove scene references.");
            if (modelRoot.localPosition != Vector3.zero || Quaternion.Angle(modelRoot.localRotation, Quaternion.identity) > .001f
                || (modelRoot.lossyScale - Vector3.one).sqrMagnitude > 1e-8f)
                throw new InvalidOperationException("Use the delivered separate prefab at zero/identity with unit world scale.");
            if (modelRoot.GetComponentsInChildren<MonoBehaviour>(true).Length != 0
                || modelRoot.GetComponentsInChildren<Collider>(true).Length != 0
                || modelRoot.GetComponentsInChildren<Rigidbody>(true).Length != 0
                || modelRoot.GetComponentsInChildren<Animator>(true).Length != 0
                || modelRoot.GetComponentsInChildren<Animation>(true).Length != 0
                || modelRoot.GetComponentsInChildren<Camera>(true).Length != 0
                || modelRoot.GetComponentsInChildren<AudioListener>(true).Length != 0)
                throw new InvalidOperationException("The work gloves must remain a visual-only, separately owned prefab instance.");
            RigFile rig = JsonUtility.FromJson<RigFile>(rigDefinition.text);
            if (rig?.joints == null || rig.joints.Length != 34 || rig.poses == null)
                throw new InvalidOperationException("Expected the delivered 34-joint rig.");
            var nodes = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (Transform node in modelRoot.GetComponentsInChildren<Transform>(true))
                if (node != modelRoot && !nodes.TryAdd(node.name, node)) throw new InvalidOperationException("Duplicate glove joint.");
            if (nodes.Count != 34) throw new InvalidOperationException("Expected exactly 34 glove transforms.");
            var parsed = new Dictionary<string, JointBinding>(StringComparer.Ordinal);
            foreach (RigJoint joint in rig.joints)
            {
                if (!nodes.TryGetValue(joint.name ?? string.Empty, out Transform node)) throw new InvalidOperationException("Missing glove joint.");
                Transform parent = modelRoot;
                if (!string.IsNullOrEmpty(joint.parent) && !nodes.TryGetValue(joint.parent, out parent))
                    throw new InvalidOperationException("Missing glove parent.");
                Vector3 position = ReadVector(joint.position);
                if (node.parent != parent || (node.localPosition - position).sqrMagnitude > 1e-8f
                    || Quaternion.Angle(node.localRotation, Quaternion.identity) > .01f || node.localScale != Vector3.one)
                    throw new InvalidOperationException("Delivered glove rest transform differs at " + joint.name);
                if (!parsed.TryAdd(joint.name, new JointBinding { Node = node, RestPosition = node.localPosition,
                    RestRotation = node.localRotation, GripRotation = Quaternion.identity })) throw new InvalidOperationException("Duplicate rig joint.");
            }
            RigPose grip = null;
            foreach (RigPose pose in rig.poses)
                if (pose != null && pose.name == "Grip") { if (grip != null) throw new InvalidOperationException("Duplicate Grip pose."); grip = pose; }
            if (grip?.rotations == null || grip.rotations.Length != 32) throw new InvalidOperationException("Grip must define both wrists and all fingers.");
            var assigned = new HashSet<string>(StringComparer.Ordinal);
            foreach (RigRotation rotation in grip.rotations)
            {
                if (rotation == null || !parsed.TryGetValue(rotation.name ?? string.Empty, out JointBinding joint)
                    || rotation.name.EndsWith("_Arm", StringComparison.Ordinal) || !assigned.Add(rotation.name))
                    throw new InvalidOperationException("Invalid Grip joint.");
                float[] q = rotation.rotation;
                if (q == null || q.Length != 4 || !Finite(q[0]) || !Finite(q[1]) || !Finite(q[2]) || !Finite(q[3]))
                    throw new InvalidOperationException("Invalid Grip quaternion.");
                Quaternion value = new Quaternion(q[0], q[1], q[2], q[3]);
                if (Quaternion.Dot(value, value) < .99f) throw new InvalidOperationException("Degenerate Grip quaternion.");
                joint.GripRotation = value.normalized;
            }
            leftArm = parsed["L_Arm"]; rightArm = parsed["R_Arm"];
            leftHand = parsed["L_Hand"]; rightHand = parsed["R_Hand"];
            if (leftHand.RestPosition != Vector3.zero || rightHand.RestPosition != Vector3.zero)
                throw new InvalidOperationException("The documented grip solve requires coincident arm/wrist origins.");
            joints = new List<JointBinding>(parsed.Values).ToArray();
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static Vector3 ReadVector(float[] value)
        {
            if (value == null || value.Length != 3 || !Finite(value[0]) || !Finite(value[1]) || !Finite(value[2]))
                throw new InvalidOperationException("Invalid rig position.");
            return new Vector3(value[0], value[1], value[2]);
        }
    }
}
