using System;
using System.Collections.Generic;
using OutOfReach.Contracts;
using UnityEngine;

namespace OutOfReach.Presentation.Motion
{
    /// <summary>Passive feet/yaw body view. Poses the visible rig from the real player's movement.</summary>
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
        private CharacterController groundController;
        private Func<bool> groundedQuery, sprintQuery;
        private ModelCache workCache, evaCache;
        private Vector3 authoredRootPosition, authoredRootScale;
        private Quaternion authoredRootRotation;
        private bool bound, visible, activeEva, seatedRequested, wasPaused, cached;
        private BodyPose pose;
        private BodyArmVisibility armMask;
        private int bindingVersion;
        private bool notifyingReset;
        private bool offsetResetRequested;
        private Vector3 previousFeetPosition, gaitDirection;
        private bool hasFeetPosition;
        private float gaitPhase, gaitWeight, sprintWeight, walkingPace;
        private DepartureSeatRestraint departureSeat;

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
        public bool HasDepartureSeat => departureSeat != null;
        public bool IsDepartureSeatLatched => departureSeat != null && departureSeat.IsLatched;
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

        /// <summary>
        /// Optional grounded presentation. The session owner supplies the sole controller, its grounded mode
        /// permission and its accepted sprint state. Call after ConfigureSession on every session binding.
        /// </summary>
        public void ConfigureGroundLocomotion(CharacterController controller, Func<bool> groundedModeAllowed,
            Func<bool> isSprinting)
        {
            if (!bound || notifyingReset)
                throw new InvalidOperationException("Bind a body session before configuring grounded presentation.");
            if (!controller || controller.transform != playerFeet || groundedModeAllowed == null || isSprinting == null)
                throw new ArgumentException("Grounded presentation needs the bound player controller and both mode callbacks.");
            workCache.PrepareGroundRig();
            evaCache.PrepareGroundRig();
            ResetGroundMotion(true);
            if (visible && pose == BodyPose.Standing)
                (activeEva ? evaCache : workCache).Ground.CaptureStanding(visualRoot);
            groundController = controller;
            groundedQuery = groundedModeAllowed;
            sprintQuery = isSprinting;
        }

        /// <summary>Explicit visual pose request only. Never seats, teleports, rests or changes the player.</summary>
        public void SetSeatedPose(bool seated)
        {
            seatedRequested = seated;
            if (bound && isActiveAndEnabled) Refresh();
        }

        /// <summary>
        /// Atomically leases the same WORK skin at an explicit, metre-scale chair reference.
        /// The frame is the body origin (+Z forward, +Y vehicle-up), not the seat eye or player feet.
        /// Root releases other hand owners before acquiring and releases this lease on every departure exit.
        /// </summary>
        public bool TryBeginDepartureSeat(Transform seatBodyFrame)
        {
            if (!Application.isPlaying || !bound || !isActiveAndEnabled || notifyingReset || departureSeat != null
                || clock.IsPaused || suit.IsSuitOn || suit.IsOutside || !seatBodyFrame
                || !seatBodyFrame.gameObject.activeInHierarchy || !seatBodyFrame.parent
                || !visualRoot || !visualRoot.gameObject.activeInHierarchy || !playerFeet || !playerCamera
                || seatBodyFrame == visualRoot || seatBodyFrame.IsChildOf(visualRoot)
                || seatBodyFrame == playerFeet || seatBodyFrame.IsChildOf(playerFeet)
                || seatBodyFrame == playerCamera.transform || seatBodyFrame.IsChildOf(playerCamera.transform)
                || !Finite(seatBodyFrame.position) || (seatBodyFrame.lossyScale - Vector3.one).sqrMagnitude > .000001f
                || !work.modelRoot || !work.torsoLegs || !work.leftArm || !work.rightArm || !work.seated) return false;

            Shader lit = null;
            foreach (Material material in work.torsoLegs.sharedMaterials)
                if (material && material.shader && material.shader.name == "Universal Render Pipeline/Lit")
                { lit = material.shader; break; }
            if (!lit) return false;
            var lease = new DepartureSeatRestraint(visualRoot, seatBodyFrame, workCache.Nodes, lit);
            ResetOwnedVisuals();
            try
            {
                work.seated.SampleAnimation(work.modelRoot.gameObject, 0f);
                lease.Activate();
                departureSeat = lease;
                activeEva = false;
                pose = BodyPose.Seated;
                armMask = BodyArmVisibility.Both;
                work.torsoLegs.enabled = work.leftArm.enabled = work.rightArm.enabled = true;
                visible = true;
                return true;
            }
            catch
            {
                lease.Dispose();
                ResetOwnedVisuals();
                throw;
            }
        }

        /// <summary>Call after the actual vehicle/camera frame. The departure owner supplies its held playhead.</summary>
        public bool PresentDepartureSeat(float presentedSeconds)
        {
            if (!bound || !isActiveAndEnabled || departureSeat == null || notifyingReset) return false;
            if (!CanRetainDepartureSeat())
            {
                EndDepartureSeat();
                return false;
            }
            if (presentedSeconds < 0f || float.IsNaN(presentedSeconds) || float.IsInfinity(presentedSeconds)) return false;
            if (!clock.IsPaused) departureSeat.Present(presentedSeconds);
            return true;
        }

        /// <summary>Release before restoring gameplay camera controls or switching to an exterior shot.</summary>
        public void EndDepartureSeat()
        {
            if (notifyingReset || departureSeat == null) return;
            ReleaseDepartureSeat();
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
            groundController = null; groundedQuery = sprintQuery = null;
            DisposeDepartureSeat();
            ResetOwnedVisuals();
            seatedRequested = wasPaused = offsetResetRequested = false;
        }

        private void Awake() => HideRenderers();
        private void OnEnable() { if (bound) { Subscribe(); Refresh(); } else HideRenderers(); }
        private void OnDisable() => ClearBinding();
        private void OnDestroy() { ClearBinding(); PoseWillReset = null; }
        private void LateUpdate() { if (bound) Refresh(true); }

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

        private void Refresh(bool trackMovement = false)
        {
            if (!bound || !isActiveAndEnabled || !visualRoot || !playerFeet || !playerCamera
                || !work.modelRoot || !eva.modelRoot || !work.torsoLegs || !eva.torsoLegs
                || !work.leftArm || !work.rightArm || !eva.leftArm || !eva.rightArm)
            { if (bound) ClearBinding(); return; }
            if (departureSeat != null)
            {
                if (CanRetainDepartureSeat()) return;
                ReleaseDepartureSeat();
            }
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
            bool poseChanged = !visible || nextEva != activeEva || nextPose != pose;
            bool reset = poseChanged || nextArms != armMask
                || (paused && !wasPaused) || (offsetResetRequested && !paused);
            if (reset)
            {
                NotifyReset();
                HideRenderers();
                if (poseChanged || offsetResetRequested && !paused) ResetGroundMotion(false);
                workCache.Restore(); evaCache.Restore();
                Outfit selected = nextEva ? eva : work;
                AnimationClip clip = nextPose == BodyPose.Seated ? selected.seated
                    : nextPose == BodyPose.Floating ? selected.floating : selected.standing;
                // Held Generic transform curves are sampled once. Disabled Animator components never
                // overwrite contact-adapter bones later in the frame; no new animation clock is created.
                clip.SampleAnimation(selected.modelRoot.gameObject, 0f);
                if (nextPose == BodyPose.Standing)
                    (nextEva ? evaCache : workCache).Ground?.CaptureStanding(visualRoot);
                activeEva = nextEva; pose = nextPose; armMask = nextArms;
                selected.torsoLegs.enabled = true;
                selected.leftArm.enabled = (armMask & BodyArmVisibility.Left) != 0;
                selected.rightArm.enabled = (armMask & BodyArmVisibility.Right) != 0;
                visible = true; PoseRevision++;
                if (!paused) offsetResetRequested = false;
            }
            if (!paused || !wasVisible) FollowFeet();
            if (pose == BodyPose.Standing && groundController)
            {
                if (paused || Time.timeScale <= 0f)
                {
                    hasFeetPosition = false;
                    // The pause reset still releases presentation leases. Preserve the standing gait frame.
                    if (reset && gaitWeight > 0f) ApplyGroundPose();
                }
                else if (trackMovement) UpdateGroundMotion();
                else if (reset && gaitWeight > 0f) ApplyGroundPose();
            }
            wasPaused = paused;
        }

        private bool CanRetainDepartureSeat() => departureSeat.IsUsable && !suit.IsSuitOn && !suit.IsOutside;

        private void ReleaseDepartureSeat()
        {
            DisposeDepartureSeat();
            ResetOwnedVisuals();
        }

        private void DisposeDepartureSeat()
        {
            DepartureSeatRestraint lease = departureSeat;
            departureSeat = null;
            lease?.Dispose();
        }

        private void UpdateGroundMotion()
        {
            if (!groundController.enabled || !groundController.isGrounded || suit.IsOutside || !groundedQuery())
            {
                ResetGroundMotion(true);
                return;
            }

            Vector3 feetPosition = playerFeet.position;
            Vector3 displacement = Vector3.ProjectOnPlane(feetPosition - previousFeetPosition, Vector3.up);
            previousFeetPosition = feetPosition;
            if (!hasFeetPosition)
            {
                hasFeetPosition = true;
                return;
            }

            float elapsed = Time.deltaTime;
            float distance = displacement.magnitude;
            Vector3 controllerTravel = Vector3.ProjectOnPlane(groundController.velocity, Vector3.up) * elapsed;
            // A physics move and the observed feet delta must agree. Reject warps and hitches rather than cycle to catch up.
            if (elapsed <= 0f || elapsed > .25f || !Finite(displacement) || !Finite(controllerTravel)
                || distance > .65f || (displacement - controllerTravel).magnitude > .015f + distance * .2f)
            {
                ResetGroundMotion(true);
                return;
            }

            bool moved = distance > elapsed * .08f && Vector3.Dot(displacement, controllerTravel) > 0f;
            float previousWeight = gaitWeight;
            gaitWeight = Mathf.MoveTowards(gaitWeight, moved ? 1f : 0f, elapsed * (moved ? 9f : 11f));
            if (moved)
            {
                Vector3 nextDirection = Quaternion.Inverse(visualRoot.rotation) * (displacement / distance);
                gaitDirection = previousWeight <= 0f ? nextDirection
                    : Vector3.RotateTowards(gaitDirection, nextDirection, elapsed * 16f, 0f);
                sprintWeight = Mathf.MoveTowards(sprintWeight, sprintQuery() ? 1f : 0f, elapsed * 8f);
                walkingPace = Mathf.MoveTowards(walkingPace, Mathf.Clamp01(distance / (elapsed * 2.5f)), elapsed * 7f);
                gaitPhase = Mathf.Repeat(gaitPhase + distance / (2f * StepLengthMetres()), 1f);
            }
            if (gaitWeight > 0f) ApplyGroundPose();
            else
            {
                if (previousWeight > 0f) (activeEva ? evaCache : workCache).Ground.RestoreStanding();
                gaitPhase = sprintWeight = walkingPace = 0f;
            }
        }

        private float StepLengthMetres()
        {
            float forwardStep = Mathf.Lerp(Mathf.Lerp(.40f, .72f, walkingPace), .92f, sprintWeight);
            float sideStep = Mathf.Lerp(Mathf.Lerp(.34f, .49f, walkingPace), .60f, sprintWeight);
            return Mathf.Lerp(forwardStep, sideStep, Mathf.Abs(gaitDirection.x));
        }

        private void ApplyGroundPose()
        {
            GroundRig rig = (activeEva ? evaCache : workCache).Ground;
            float running = sprintWeight;
            float stepLength = StepLengthMetres();
            float footLift = Mathf.Lerp(Mathf.Lerp(.06f, .125f, walkingPace), .23f, running);
            float stance = Mathf.Lerp(.60f, .46f, running);
            float weight = Mathf.SmoothStep(0f, 1f, gaitWeight);
            rig.Apply(visualRoot, gaitDirection, gaitPhase, stepLength, footLift, stance, weight);
        }

        private void ResetGroundMotion(bool restoreStanding)
        {
            if (restoreStanding && gaitWeight > 0f)
                (activeEva ? evaCache : workCache).Ground.RestoreStanding();
            hasFeetPosition = false;
            gaitPhase = gaitWeight = sprintWeight = walkingPace = 0f;
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
            ResetGroundMotion(false);
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
                    throw new InvalidOperationException("The passive body requires disabled Animators without root motion or controllers.");
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
            public GroundRig Ground { get; private set; }
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

            public void PrepareGroundRig()
            {
                if (Ground == null) Ground = new GroundRig(Nodes);
            }
        }

        private sealed class GroundRig
        {
            private readonly Transform hips;
            private readonly GroundLeg left, right;
            private Vector3 standingHipsPosition;

            public GroundRig(Dictionary<string, Transform> nodes)
            {
                hips = Find(nodes, "root");
                left = new GroundLeg(nodes, "L", -1f);
                right = new GroundLeg(nodes, "R", 1f);
                if (!left.Thigh.IsChildOf(hips) || !right.Thigh.IsChildOf(hips))
                    throw new InvalidOperationException("The body's visual hips must own both leg chains.");
            }

            public void CaptureStanding(Transform reference)
            {
                standingHipsPosition = hips.localPosition;
                left.CaptureStanding(reference);
                right.CaptureStanding(reference);
            }

            public void RestoreStanding()
            {
                hips.localPosition = standingHipsPosition;
                left.RestoreStanding();
                right.RestoreStanding();
            }

            public void Apply(Transform reference, Vector3 direction, float phase,
                float stepLength, float footLift, float stance, float weight)
            {
                RestoreStanding();
                Vector3 leftTarget = left.FootTarget(direction, phase, stepLength, footLift, stance, weight, out float leftPitch);
                Vector3 rightTarget = right.FootTarget(direction, Mathf.Repeat(phase + .5f, 1f),
                    stepLength, footLift, stance, weight, out float rightPitch);

                leftTarget = reference.TransformPoint(leftTarget);
                rightTarget = reference.TransformPoint(rightTarget);
                // A longer stride lowers the visual hips only as far as needed to preserve the support foot.
                float drop = Mathf.Max(.021f * weight,
                    Mathf.Max(left.DropToReach(leftTarget), right.DropToReach(rightTarget)));
                hips.position -= Vector3.up * Mathf.Min(.18f, drop);
                left.Pose(reference, leftTarget, leftPitch, weight);
                right.Pose(reference, rightTarget, rightPitch, weight);
            }

            public static Transform Find(Dictionary<string, Transform> nodes, string name)
            {
                if (nodes.TryGetValue(name, out Transform bone)) return bone;
                throw new InvalidOperationException("Grounded body is missing imported joint " + name);
            }
        }

        private sealed class GroundLeg
        {
            private readonly Transform shin, ankle;
            private readonly float side;
            public readonly Transform Thigh;
            private Quaternion thighRotation, shinRotation, ankleRotation, standingAnkleOrientation;
            private Vector3 standingAnkle, standingKneePole;
            private float thighLength, shinLength;

            public GroundLeg(Dictionary<string, Transform> nodes, string suffix, float sideSign)
            {
                Thigh = GroundRig.Find(nodes, "upperleg01." + suffix);
                shin = GroundRig.Find(nodes, "lowerleg01." + suffix);
                ankle = GroundRig.Find(nodes, "foot." + suffix);
                side = sideSign;
                if (!shin.IsChildOf(Thigh) || !ankle.IsChildOf(shin))
                    throw new InvalidOperationException("Invalid grounded body leg chain for side " + suffix);
            }

            public void CaptureStanding(Transform reference)
            {
                thighRotation = Thigh.localRotation;
                shinRotation = shin.localRotation;
                ankleRotation = ankle.localRotation;
                standingAnkleOrientation = Quaternion.Inverse(reference.rotation) * ankle.rotation;
                standingAnkle = reference.InverseTransformPoint(ankle.position);
                Vector3 upper = shin.position - Thigh.position, lower = ankle.position - shin.position;
                thighLength = upper.magnitude;
                shinLength = lower.magnitude;
                standingKneePole = reference.InverseTransformDirection(Vector3.ProjectOnPlane(upper, upper + lower).normalized);
            }

            public void RestoreStanding()
            {
                Thigh.localRotation = thighRotation;
                shin.localRotation = shinRotation;
                ankle.localRotation = ankleRotation;
            }

            public Vector3 FootTarget(Vector3 direction, float phase, float stepLength, float lift,
                float stance, float weight, out float pitch)
            {
                float stanceReach = stepLength * stance;
                float foreAft, height = 0f;
                pitch = 0f;
                if (phase < stance)
                {
                    // In straight, steady travel the supporting sole moves opposite actual displacement.
                    foreAft = Mathf.Lerp(stanceReach, -stanceReach, phase / stance);
                }
                else
                {
                    float swing = (phase - stance) / (1f - stance);
                    float liftShape = 4f * swing * (1f - swing);
                    height = lift * liftShape * liftShape;
                    pitch = 13f * Mathf.Sin(2f * Mathf.PI * swing) * liftShape * liftShape * weight;
                    // Match the horizontal tangent at lift-off and touchdown so the ankle does not snap.
                    float stanceTangent = -2f * stepLength * (1f - stance);
                    foreAft = Mathf.LerpUnclamped(-stanceReach, stanceReach, swing * swing * (3f - 2f * swing))
                        + stanceTangent * swing * (1f - swing) * (1f - 2f * swing);
                }

                Vector3 target = standingAnkle + (direction * foreAft + Vector3.up * height) * weight;
                target.x += side * .06f * Mathf.Abs(direction.x) * weight;
                // Keep the inside edge of a strafe from crossing the other boot.
                target.x = side * Mathf.Max(.092f, side * target.x);
                return target;
            }

            public float DropToReach(Vector3 target)
            {
                Vector3 offset = Thigh.position - target;
                float reach = thighLength + shinLength - .006f;
                float heightSquared = reach * reach - offset.x * offset.x - offset.z * offset.z;
                return Mathf.Max(0f, offset.y - Mathf.Sqrt(Mathf.Max(0f, heightSquared)));
            }

            public void Pose(Transform reference, Vector3 target, float pitch, float weight)
            {
                Vector3 hip = Thigh.position;
                Vector3 toAnkle = target - hip;
                Vector3 axis = toAnkle.normalized;
                float length = Mathf.Clamp(toAnkle.magnitude, Mathf.Abs(thighLength - shinLength) + .001f,
                    thighLength + shinLength - .001f);
                float kneeAlong = (thighLength * thighLength - shinLength * shinLength + length * length) / (2f * length);
                float kneeOut = Mathf.Sqrt(Mathf.Max(0f, thighLength * thighLength - kneeAlong * kneeAlong));
                Vector3 localPole = Vector3.Slerp(standingKneePole, new Vector3(side * .35f, 0f, 1f).normalized, weight);
                Vector3 kneePole = Vector3.ProjectOnPlane(reference.TransformDirection(localPole), axis).normalized;
                Vector3 knee = hip + axis * kneeAlong + kneePole * kneeOut;
                Vector3 reachableAnkle = hip + axis * length;

                Thigh.rotation = Quaternion.FromToRotation(shin.position - hip, knee - hip) * Thigh.rotation;
                shin.rotation = Quaternion.FromToRotation(ankle.position - shin.position, reachableAnkle - shin.position) * shin.rotation;
                ankle.rotation = reference.rotation * Quaternion.AngleAxis(pitch, Vector3.right) * standingAnkleOrientation;
            }
        }
        private static bool Finite(Vector3 v) => !float.IsNaN(v.x) && !float.IsInfinity(v.x)
            && !float.IsNaN(v.y) && !float.IsInfinity(v.y) && !float.IsNaN(v.z) && !float.IsInfinity(v.z);
    }
}
