using System;
using System.Collections.Generic;
using OutOfReach.Contracts;
using UnityEngine;

namespace OutOfReach.Presentation.Motion
{
    /// <summary>Passive feet/yaw body view. Samples authored held poses; never moves the player or camera.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(200)]
    public sealed class EmbodiedBodyPresenter : MonoBehaviour
    {
        [Flags] public enum BodyArmVisibility { None = 0, Left = 1, Right = 2, Both = Left | Right }
        public enum BodyPose { Standing, Floating, Seated }

        [Serializable]
        public sealed class Outfit
        {
            public Transform modelRoot;
            public SkinnedMeshRenderer torsoLegs;
            public SkinnedMeshRenderer leftArm;
            public SkinnedMeshRenderer rightArm;
            public AnimationClip standing;
            public AnimationClip floating;
            public AnimationClip seated;
        }

        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform playerFeet;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Vector3 feetVisualOffset = new Vector3(0f, 0f, -.330f);
        [SerializeField] private Vector3 floatingFeetVisualOffset = new Vector3(0f, 0f, -.330f);
        [SerializeField] private Vector3 seatedFeetVisualOffset = new Vector3(0f, 0f, -.330f);
        [SerializeField] private Outfit work = new Outfit();
        [SerializeField] private Outfit eva = new Outfit();

        private IEvaStatus suit, subscribedSuit;
        private ISimulationClock clock;
        private Func<bool> visibleQuery, floatingQuery;
        private Func<BodyArmVisibility> armsQuery;
        private ModelCache workCache, evaCache;
        private Vector3 authoredRootPosition, authoredRootScale;
        private Quaternion authoredRootRotation;
        private bool bound, visible, activeEva, seatedRequested, wasPaused, cached;
        private BodyPose pose;
        private BodyArmVisibility armMask;
        private int bindingVersion;
        private bool notifyingReset;
        private bool offsetResetRequested;

        /// <summary>
        /// Synchronous lease cancellation before sampling, restoration, hide, clear or disable.
        /// A contact adapter must restore its bones here, without configuring this presenter from the callback.
        /// </summary>
        public event Action PoseWillReset;
        public int PoseRevision { get; private set; }
        public bool IsBound => bound;
        public bool IsVisible => visible && visualRoot && visualRoot.gameObject.activeInHierarchy;
        public bool IsEvaVisible => IsVisible && activeEva;
        public BodyPose CurrentPose => pose;
        public BodyArmVisibility VisibleArms => visible ? armMask : BodyArmVisibility.None;
        public Transform VisualRoot => visualRoot;
        public Transform PlayerFeet => playerFeet;
        public Camera PlayerCamera => playerCamera;
        public Vector3 FeetVisualOffset => feetVisualOffset;
        public SkinnedMeshRenderer WorkLeftArmRenderer => work.leftArm;
        public SkinnedMeshRenderer WorkRightArmRenderer => work.rightArm;
        public Transform WorkLeftWrist => GetWorkBone("wrist.L");
        public Transform WorkRightWrist => GetWorkBone("wrist.R");
        public Transform WorkLeftUpperArm => GetWorkBone("upperarm01.L");
        public Transform WorkRightUpperArm => GetWorkBone("upperarm01.R");

        public Transform GetWorkBone(string boneName) => workCache != null && boneName != null
            && workCache.Nodes.TryGetValue(boneName, out Transform node) ? node : null;

        /// <summary>Read the actual visible outfit before a presentation lease hides or resets it.</summary>
        public Transform GetVisibleBone(string boneName)
        {
            ModelCache selected = activeEva ? evaCache : workCache;
            return IsVisible && selected != null && boneName != null
                && selected.Nodes.TryGetValue(boneName, out Transform node) ? node : null;
        }

        public Vector3 GetVisualFeetOffset(BodyPose forPose) => forPose == BodyPose.Floating ? floatingFeetVisualOffset
            : forPose == BodyPose.Seated ? seatedFeetVisualOffset : feetVisualOffset;

        /// <summary>Root-owned fit calibration only; never alters camera/player transforms or arm lengths.</summary>
        public void SetVisualFeetOffset(BodyPose forPose, Vector3 offset)
        {
            if (!Finite(offset) || offset.magnitude > 1f) throw new ArgumentException("Use a finite visual fit offset within one metre.");
            if (forPose == BodyPose.Floating) floatingFeetVisualOffset = offset;
            else if (forPose == BodyPose.Seated) seatedFeetVisualOffset = offset;
            else feetVisualOffset = offset;
            offsetResetRequested = true;
            if (bound && isActiveAndEnabled) Refresh();
        }

        /// <summary>Composition/editor setup only. Supply new body instances, not the original glove hierarchy.</summary>
        public void ConfigureScene(Transform dedicatedVisualRoot, Transform authoritativeFeet, Camera soleCamera,
            Outfit workOutfit, Outfit evaOutfit, Vector3 visualFeetOffset)
        {
            if (notifyingReset) throw new InvalidOperationException("Do not reconfigure body from PoseWillReset.");
            ClearBinding();
            if (!dedicatedVisualRoot || !authoritativeFeet || !soleCamera || workOutfit == null || evaOutfit == null)
                throw new ArgumentException("Body needs explicit visual root, feet, camera and both outfits.");
            if (dedicatedVisualRoot == authoritativeFeet || authoritativeFeet.IsChildOf(dedicatedVisualRoot)
                || soleCamera.transform.IsChildOf(dedicatedVisualRoot)
                || dedicatedVisualRoot.IsChildOf(soleCamera.transform)
                || transform == dedicatedVisualRoot || transform.IsChildOf(dedicatedVisualRoot))
                throw new ArgumentException("Keep the presenter, authoritative player and camera outside the owned visual subtree.");
            if (!Finite(visualFeetOffset) || visualFeetOffset.magnitude > 1f)
                throw new ArgumentException("Visual feet offset must be a finite fit offset within one metre.");
            visualRoot = dedicatedVisualRoot; playerFeet = authoritativeFeet; playerCamera = soleCamera;
            work = workOutfit; eva = evaOutfit; feetVisualOffset = visualFeetOffset;
            floatingFeetVisualOffset = seatedFeetVisualOffset = visualFeetOffset;
            cached = false; workCache = evaCache = null;
            PrepareModels();
            HideRenderers();
        }

        /// <summary>Root supplies read-only session and presentation gates; no phase, movement or input is inferred.</summary>
        public void ConfigureSession(IEvaStatus evaStatus, ISimulationClock simulationClock,
            Func<bool> isVisible, Func<bool> isFloating, Func<BodyArmVisibility> visibleBodyArms)
        {
            if (notifyingReset) throw new InvalidOperationException("Do not bind body from PoseWillReset.");
            ClearBinding();
            if (evaStatus == null || simulationClock == null || isVisible == null || isFloating == null || visibleBodyArms == null)
                throw new ArgumentNullException("Body needs suit, clock and all three explicit root policy callbacks.");
            PrepareModels();
            suit = evaStatus; clock = simulationClock;
            visibleQuery = isVisible; floatingQuery = isFloating; armsQuery = visibleBodyArms;
            bound = true; bindingVersion++;
            if (isActiveAndEnabled) { Subscribe(); Refresh(); }
        }

        /// <summary>Explicit visual pose request only. Never seats, teleports, rests or changes the player.</summary>
        public void SetSeatedPose(bool seated)
        {
            seatedRequested = seated;
            if (bound && isActiveAndEnabled) Refresh();
        }

        /// <summary>Root may request immediate gate/state reconciliation; otherwise LateUpdate does this.</summary>
        public void RefreshPresentation()
        {
            if (bound && isActiveAndEnabled) Refresh();
        }

        public void ClearBinding()
        {
            if (notifyingReset) throw new InvalidOperationException("Cancel contact only; do not clear body inside PoseWillReset.");
            bindingVersion++;
            Unsubscribe();
            bound = false; suit = null; clock = null;
            visibleQuery = floatingQuery = null; armsQuery = null;
            ResetOwnedVisuals();
            seatedRequested = wasPaused = offsetResetRequested = false;
        }

        private void Awake() => HideRenderers();
        private void OnEnable() { if (bound) { Subscribe(); Refresh(); } else HideRenderers(); }
        private void OnDisable() => ClearBinding();
        private void OnDestroy() { ClearBinding(); PoseWillReset = null; }
        private void LateUpdate() { if (bound) Refresh(); }

        private void Subscribe()
        {
            if (subscribedSuit != null || suit == null) return;
            subscribedSuit = suit; subscribedSuit.Changed += RefreshPresentation;
        }
        private void Unsubscribe()
        {
            if (subscribedSuit == null) return;
            subscribedSuit.Changed -= RefreshPresentation; subscribedSuit = null;
        }

        private void Refresh()
        {
            if (!bound || !isActiveAndEnabled || !visualRoot || !playerFeet || !playerCamera
                || !work.modelRoot || !eva.modelRoot || !work.torsoLegs || !eva.torsoLegs
                || !work.leftArm || !work.rightArm || !eva.leftArm || !eva.rightArm)
            { if (bound) ClearBinding(); return; }
            int version = bindingVersion;
            bool permitted = visibleQuery();
            if (version != bindingVersion || !bound) return;
            if (!permitted) { if (visible) ResetOwnedVisuals(); return; }
            bool paused = clock.IsPaused;
            bool nextEva = suit.IsSuitOn;
            BodyPose nextPose = seatedRequested ? BodyPose.Seated : floatingQuery() ? BodyPose.Floating : BodyPose.Standing;
            if (version != bindingVersion || !bound) return;
            BodyArmVisibility nextArms = armsQuery() & BodyArmVisibility.Both;
            if (version != bindingVersion || !bound) return;
            // Existing pose/position freezes while paused. Outfit safety and visibility still reconcile.
            if (paused && visible && nextEva == activeEva) nextPose = pose;
            bool wasVisible = visible;
            bool reset = !visible || nextEva != activeEva || nextPose != pose || nextArms != armMask
                || (paused && !wasPaused) || (offsetResetRequested && !paused);
            if (reset)
            {
                NotifyReset();
                HideRenderers();
                workCache.Restore(); evaCache.Restore();
                Outfit selected = nextEva ? eva : work;
                AnimationClip clip = nextPose == BodyPose.Seated ? selected.seated
                    : nextPose == BodyPose.Floating ? selected.floating : selected.standing;
                // Held Generic transform curves are sampled once. Disabled Animator components never
                // overwrite contact-adapter bones later in the frame; no new animation clock is created.
                clip.SampleAnimation(selected.modelRoot.gameObject, 0f);
                activeEva = nextEva; pose = nextPose; armMask = nextArms;
                selected.torsoLegs.enabled = true;
                selected.leftArm.enabled = (armMask & BodyArmVisibility.Left) != 0;
                selected.rightArm.enabled = (armMask & BodyArmVisibility.Right) != 0;
                visible = true; PoseRevision++;
                if (!paused) offsetResetRequested = false;
            }
            if (!paused || !wasVisible) FollowFeet();
            wasPaused = paused;
        }

        private void FollowFeet()
        {
            Vector3 forward = Vector3.ProjectOnPlane(playerFeet.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-8f) return;
            Quaternion yaw = Quaternion.LookRotation(forward.normalized, Vector3.up);
            visualRoot.SetPositionAndRotation(playerFeet.position + yaw * GetVisualFeetOffset(pose), yaw);
        }

        private void NotifyReset()
        {
            if (notifyingReset) return;
            notifyingReset = true;
            try { PoseWillReset?.Invoke(); }
            catch (Exception error) { Debug.LogException(error, this); }
            finally { notifyingReset = false; }
        }

        private void ResetOwnedVisuals()
        {
            if (cached)
            {
                NotifyReset();
                workCache?.Restore(); evaCache?.Restore();
                if (visualRoot)
                {
                    visualRoot.SetLocalPositionAndRotation(authoredRootPosition, authoredRootRotation);
                    visualRoot.localScale = authoredRootScale;
                }
                PoseRevision++;
            }
            HideRenderers(); visible = false; armMask = BodyArmVisibility.None;
        }

        private void HideRenderers()
        {
            SetHidden(work); SetHidden(eva);
        }
        private static void SetHidden(Outfit outfit)
        {
            if (outfit == null) return;
            if (outfit.torsoLegs) outfit.torsoLegs.enabled = false;
            if (outfit.leftArm) outfit.leftArm.enabled = false;
            if (outfit.rightArm) outfit.rightArm.enabled = false;
        }

        private void PrepareModels()
        {
            if (cached) return;
            if (!visualRoot || !playerFeet || !playerCamera)
                throw new InvalidOperationException("Configure body scene references before binding a session.");
            if ((visualRoot.lossyScale - Vector3.one).sqrMagnitude > .000001f
                || (playerFeet.lossyScale - Vector3.one).sqrMagnitude > .000001f)
                throw new InvalidOperationException("Body fit requires metre-scale player and visual parents.");
            workCache = ReadModel(work, "WORK"); evaCache = ReadModel(eva, "EVA");
            if (work.modelRoot == eva.modelRoot || work.modelRoot.IsChildOf(eva.modelRoot) || eva.modelRoot.IsChildOf(work.modelRoot))
                throw new InvalidOperationException("Outfits must be separate sibling model instances.");
            authoredRootPosition = visualRoot.localPosition; authoredRootRotation = visualRoot.localRotation;
            authoredRootScale = visualRoot.localScale; cached = true;
        }

        private ModelCache ReadModel(Outfit outfit, string variant)
        {
            if (outfit == null || !outfit.modelRoot || !outfit.modelRoot.IsChildOf(visualRoot)
                || !outfit.torsoLegs || !outfit.leftArm || !outfit.rightArm
                || !outfit.standing || !outfit.floating || !outfit.seated)
                throw new InvalidOperationException("Incomplete explicit " + variant + " body references.");
            SkinnedMeshRenderer[] renderers = outfit.modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (renderers.Length != 3 || outfit.torsoLegs == outfit.leftArm || outfit.torsoLegs == outfit.rightArm || outfit.leftArm == outfit.rightArm)
                throw new InvalidOperationException("Each body outfit must have exactly its three reviewed skinned renderers.");
            foreach (SkinnedMeshRenderer renderer in renderers)
                if (!renderer.sharedMesh || !renderer.rootBone || renderer.bones.Length == 0
                    || (renderer != outfit.torsoLegs && renderer != outfit.leftArm && renderer != outfit.rightArm))
                    throw new InvalidOperationException("Body skin references are missing or mismatched.");
            foreach (Animator animator in outfit.modelRoot.GetComponentsInChildren<Animator>(true))
                if (animator.enabled || animator.runtimeAnimatorController || animator.applyRootMotion)
                    throw new InvalidOperationException("Body uses explicit held-pose sampling: disable Animator/root motion/controllers in authoring.");
            if (outfit.modelRoot.GetComponentsInChildren<MonoBehaviour>(true).Length != 0
                || outfit.modelRoot.GetComponentsInChildren<Camera>(true).Length != 0
                || outfit.modelRoot.GetComponentsInChildren<AudioListener>(true).Length != 0
                || outfit.modelRoot.GetComponentsInChildren<Collider>(true).Length != 0
                || outfit.modelRoot.GetComponentsInChildren<Rigidbody>(true).Length != 0
                || outfit.modelRoot.GetComponentsInChildren<Animation>(true).Length != 0)
                throw new InvalidOperationException("Body subtree must contain only passive model/skin references.");
            foreach (AnimationClip clip in new[] { outfit.standing, outfit.floating, outfit.seated })
                if (clip.legacy || clip.humanMotion || clip.length <= 0f)
                    throw new InvalidOperationException("Use the reviewed non-Humanoid Generic held clips.");
            var cache = new ModelCache(outfit.modelRoot);
            foreach (string name in new[] { "CharacterRoot", "upperarm01.L", "upperarm01.R", "lowerarm01.L", "lowerarm01.R", "wrist.L", "wrist.R" })
                if (!cache.Nodes.ContainsKey(name)) throw new InvalidOperationException("Body is missing imported joint " + name);
            return cache;
        }

        private sealed class ModelCache
        {
            public readonly Dictionary<string, Transform> Nodes = new Dictionary<string, Transform>(StringComparer.Ordinal);
            private readonly Transform[] transforms;
            private readonly Vector3[] positions, scales;
            private readonly Quaternion[] rotations;
            public ModelCache(Transform root)
            {
                transforms = root.GetComponentsInChildren<Transform>(true);
                positions = new Vector3[transforms.Length]; scales = new Vector3[transforms.Length]; rotations = new Quaternion[transforms.Length];
                for (int i = 0; i < transforms.Length; i++)
                {
                    Transform t = transforms[i];
                    if (!Nodes.TryAdd(t.name, t)) throw new InvalidOperationException("Duplicate body node name " + t.name);
                    positions[i] = t.localPosition; rotations[i] = t.localRotation; scales[i] = t.localScale;
                }
            }
            public void Restore()
            {
                for (int i = 0; i < transforms.Length; i++)
                    if (transforms[i])
                    {
                        transforms[i].SetLocalPositionAndRotation(positions[i], rotations[i]);
                        transforms[i].localScale = scales[i];
                    }
            }
        }
        private static bool Finite(Vector3 v) => !float.IsNaN(v.x) && !float.IsInfinity(v.x)
            && !float.IsNaN(v.y) && !float.IsInfinity(v.y) && !float.IsNaN(v.z) && !float.IsInfinity(v.z);
    }
}
