using System;
using System.Collections.Generic;
using OutOfReach.Contracts;
using UnityEngine;

namespace OutOfReach.Presentation.Hands
{
    /// <summary>
    /// Poses the delivered rigid glove rig from its JSON and shows it only while Core says the suit is worn.
    /// Owns model visibility and the 34 rig joints, never the camera, motion mount, player or an interaction.
    /// This checkpoint supplies a neutral first-person stance, not contact animation, full-body IK or skinning.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(140)]
    public sealed class AstronautHandsPresenter : MonoBehaviour
    {
        [SerializeField] private Transform modelRoot;
        [Tooltip("Dedicated parent below the first-person camera. Only an explicitly bound motion presenter may animate it.")]
        [SerializeField] private Transform motionMount;
        [SerializeField] private TextAsset rigDefinition;

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

        /// <summary>Restores only the delivered neutral joint pose. Does not move the model root or its mount.</summary>
        public void ResetToNeutral()
        {
            foreach (JointBinding binding in bindings)
                if (binding.Joint)
                    binding.Joint.SetLocalPositionAndRotation(binding.NeutralPosition, binding.NeutralRotation);
        }

        public void ClearBinding()
        {
            Unsubscribe();
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
        private void OnDisable() { Unsubscribe(); HideModel(); RestoreAuthoredPose(); }
        private void OnDestroy() => ClearBinding();
        private void LateUpdate() => RefreshPresentation();

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
            bool show = bound && isActiveAndEnabled && modelRoot && motionMount && suit.IsSuitOn
                && !clock.IsPaused && IsFirstPersonPhase(state.Phase)
                && firstPersonViewAllowed != null && firstPersonViewAllowed();
            if (!show)
            {
                HideModel();
                return;
            }
            if (visible && modelRoot.gameObject.activeSelf) return;
            foreach (JointBinding binding in bindings)
                if (!binding.Joint) { ClearBinding(); return; }
            ResetToNeutral(); // Apply the JSON pose before any renderer becomes visible.
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
                    AuthoredRotation = node.localRotation, NeutralPosition = rest, NeutralRotation = Quaternion.identity };
                if (!byName.TryAdd(joint.name, binding)) throw new InvalidOperationException("Duplicate JSON joint: " + joint.name);
                if (parent == modelRoot) arms.Add(joint.name);
            }
            if (!arms.SetEquals(new[] { "R_Arm", "L_Arm" }))
                throw new InvalidOperationException("Expected the two documented top-level arms.");

            RigPose neutral = null;
            foreach (RigPose pose in rig.poses)
                if (pose != null && pose.name == "Neutral")
                {
                    if (neutral != null) throw new InvalidOperationException("Duplicate Neutral pose.");
                    neutral = pose;
                }
            if (neutral == null || neutral.rotations == null || neutral.placements == null
                || neutral.rotations.Length != 32 || neutral.placements.Length != 2)
                throw new InvalidOperationException("Neutral must define all 32 wrist/finger rotations and both arm placements.");
            var assigned = new HashSet<string>(StringComparer.Ordinal);
            foreach (JointRotation rotation in neutral.rotations)
            {
                if (rotation == null || !byName.TryGetValue(rotation.name ?? string.Empty, out JointBinding binding)
                    || arms.Contains(rotation.name) || !assigned.Add(rotation.name))
                    throw new InvalidOperationException("Invalid or duplicate Neutral joint rotation.");
                binding.NeutralRotation = Rotation(rotation.rotation);
            }
            foreach (ArmPlacement placement in neutral.placements)
            {
                if (placement == null || !arms.Contains(placement.name ?? string.Empty) || !assigned.Add(placement.name))
                    throw new InvalidOperationException("Invalid or duplicate Neutral arm placement.");
                JointBinding binding = byName[placement.name];
                binding.NeutralPosition = Vector(placement.position);
                binding.NeutralRotation = Rotation(placement.rotation);
            }
            if (assigned.Count != 34) throw new InvalidOperationException("Neutral pose does not cover every documented joint.");
            var result = new JointBinding[34];
            for (int i = 0; i < rig.joints.Length; i++) result[i] = byName[rig.joints[i].name];
            return result;
        }

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
