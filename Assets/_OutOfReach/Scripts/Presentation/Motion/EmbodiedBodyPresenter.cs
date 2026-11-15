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
        public struct SemanticBinding
        {
            public string role;
            public string path;
            public SemanticBinding(string semanticRole, string relativePath) { role = semanticRole; path = relativePath; }
        }

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
            // Null/empty preserves the original strict unique-name cache for existing serialized scenes/callers.
            public SemanticBinding[] semanticBindings;
            // Null means the legacy material discovery adapter; explicit profile bindings supply nonempty indices.
            public int[] leftPalmSubmeshes;
            public int[] rightPalmSubmeshes;
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
        private Func<bool> groundedQuery, sprintQuery, preflightGaitQuery;
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
        private float gaitPhase, gaitWeight, gaitSpeed, setback;
        private bool groundPosed;
        private float? deltaOverride;
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
        public IReadOnlyList<int> WorkLeftPalmSubmeshes => PalmMembership(work.leftPalmSubmeshes);
        public IReadOnlyList<int> WorkRightPalmSubmeshes => PalmMembership(work.rightPalmSubmeshes);
        private IReadOnlyList<int> PalmMembership(int[] indices)
        {
            // Unity loads newly added array fields in old scenes as empty arrays, not only null.
            // Only unmapped legacy outfits may use material-based discovery; mapped profiles must supply membership.
            bool legacy = work.semanticBindings == null || work.semanticBindings.Length == 0;
            return legacy && (indices == null || indices.Length == 0) ? null : indices ?? Array.Empty<int>();
        }
        public Transform WorkLeftWrist => GetWorkBone("wrist.L");
        public Transform WorkRightWrist => GetWorkBone("wrist.R");
        public Transform WorkLeftUpperArm => GetWorkBone("upperarm01.L");
        public Transform WorkRightUpperArm => GetWorkBone("upperarm01.R");

        /// <summary>Resolved EVA semantic alias; available before visibility/session binding for explicit composition.</summary>
        public Transform GetEvaBone(string semanticRole) => evaCache != null && semanticRole != null
            && evaCache.Nodes.TryGetValue(semanticRole, out Transform node) ? node : null;

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
        /// permission and its accepted sprint state. The optional preflight policy only selects WORK gait art.
        /// Call after ConfigureSession on every session binding.
        /// </summary>
        public void ConfigureGroundLocomotion(CharacterController controller, Func<bool> groundedModeAllowed,
            Func<bool> isSprinting, Func<bool> usePreflightGait = null)
        {
            if (!bound || notifyingReset)
                throw new InvalidOperationException("Bind a body session before configuring grounded presentation.");
            if (!controller || controller.transform != playerFeet || groundedModeAllowed == null || isSprinting == null)
                throw new ArgumentException("Grounded presentation needs the bound player controller and both mode callbacks.");
            // Both outfits share the 164-bone rig and the same gait model; the preflight policy no longer selects art.
            workCache.PrepareGroundRig();
            evaCache.PrepareGroundRig();
            ResetGroundMotion(true);
            if (visible && pose == BodyPose.Standing)
                (activeEva ? evaCache : workCache).Ground.CaptureStanding(visualRoot);
            groundController = controller;
            groundedQuery = groundedModeAllowed;
            sprintQuery = isSprinting;
            preflightGaitQuery = usePreflightGait;
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

        /// <summary>
        /// Deterministic presentation step for tests and frame capture: behaves like one LateUpdate with the given
        /// frame time instead of Time.deltaTime. Gameplay never calls this.
        /// </summary>
        public void AdvancePresentation(float deltaTime)
        {
            if (!bound || !isActiveAndEnabled || !(deltaTime > 0f)) return;
            deltaOverride = deltaTime;
            try { Refresh(true); } finally { deltaOverride = null; }
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
            groundController = null; groundedQuery = sprintQuery = preflightGaitQuery = null;
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
                groundPosed = false;
                Outfit selected = nextEva ? eva : work;
                AnimationClip clip = nextPose == BodyPose.Seated ? selected.seated
                    : nextPose == BodyPose.Floating ? selected.floating : selected.standing;
                // Held Generic transform curves are sampled once. Disabled Animator components never
                // overwrite contact-adapter bones later in the frame; no new animation clock is created.
                clip.SampleAnimation(selected.modelRoot.gameObject, 0f);
                if (nextPose == BodyPose.Standing)
                    (nextEva ? evaCache : workCache).Ground?.CaptureStanding(visualRoot);
                evaCache.Float?.CancelPush(); workCache.Float?.CancelPush();
                activeEva = nextEva; pose = nextPose; armMask = nextArms;
                selected.torsoLegs.enabled = true;
                selected.leftArm.enabled = (armMask & BodyArmVisibility.Left) != 0;
                selected.rightArm.enabled = (armMask & BodyArmVisibility.Right) != 0;
                visible = true; PoseRevision++;
                if (!paused) offsetResetRequested = false;
            }
            if (!paused || !wasVisible) FollowFeet();
            if (pose == BodyPose.Floating) UpdateFloatingLegs(reset, paused, trackMovement);
            if (pose == BodyPose.Standing && groundController)
            {
                if (paused || Time.timeScale <= 0f)
                {
                    hasFeetPosition = false;
                    // The pause reset still releases presentation leases. Preserve the standing gait frame.
                    if (reset) ApplyGroundPose();
                }
                else if (trackMovement) UpdateGroundMotion();
                else if (reset) ApplyGroundPose();
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
            ModelGaitRig rig = (activeEva ? evaCache : workCache).Ground;
            float elapsed = deltaOverride ?? Time.deltaTime;
            UpdateLookDownSetback(rig, elapsed);

            // AdvancePresentation (tests and frame capture) has no physics step: it trusts the supplied feet motion.
            bool synthetic = deltaOverride.HasValue;
            bool walkable = groundController.enabled && (synthetic || groundController.isGrounded) && !suit.IsOutside && groundedQuery();
            if (!walkable)
            {
                ResetGait();
                ApplyGroundPose();
                return;
            }

            Vector3 feetPosition = playerFeet.position;
            Vector3 displacement = Vector3.ProjectOnPlane(feetPosition - previousFeetPosition, Vector3.up);
            previousFeetPosition = feetPosition;
            if (!hasFeetPosition)
            {
                hasFeetPosition = true;
                ApplyGroundPose();
                return;
            }

            float distance = displacement.magnitude;
            Vector3 controllerTravel = synthetic ? displacement : Vector3.ProjectOnPlane(groundController.velocity, Vector3.up) * elapsed;
            // A physics move and the observed feet delta must agree. Reject warps and hitches rather than cycle to catch up.
            if (elapsed <= 0f || elapsed > .25f || !Finite(displacement) || !Finite(controllerTravel)
                || distance > .65f || (displacement - controllerTravel).magnitude > .015f + distance * .2f)
            {
                ResetGait();
                ApplyGroundPose();
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
                float measured = distance / elapsed;
                gaitSpeed = previousWeight <= 0f ? measured
                    : Mathf.Lerp(gaitSpeed, measured, 1f - Mathf.Exp(-10f * elapsed));
                float stride = ModelGait.StrideLength(gaitSpeed, rig.LegLength, Mathf.Abs(gaitDirection.x));
                gaitPhase = Mathf.Repeat(gaitPhase + distance / stride, 1f);
            }
            else if (gaitWeight <= 0f)
            {
                gaitPhase = gaitSpeed = 0f;
            }
            ApplyGroundPose();
        }

        private void UpdateFloatingLegs(bool reset, bool paused, bool trackMovement)
        {
            ModelFloatRig rig = (activeEva ? evaCache : workCache).Float;
            if (rig == null) return;
            if (reset) rig.Capture(visualRoot);
            float dt = deltaOverride ?? Time.deltaTime;
            if (trackMovement && !paused && Time.timeScale > 0f) rig.Apply(visualRoot, Mathf.Clamp(dt, 0f, .1f));
            else if (reset) rig.Apply(visualRoot, 0f);
        }

        /// <summary>
        /// Presentation only: the legs plant on the deck behind the push, tuck, extend and release (about .7 s).
        /// Ignored unless the body is visible, floating and not paused. Never changes velocity, cooldown or state.
        /// </summary>
        public void PlayLegPush(Vector3 worldDirection)
        {
            if (!bound || !isActiveAndEnabled || !visible || pose != BodyPose.Floating || clock == null || clock.IsPaused
                || !Finite(worldDirection)) return;
            (activeEva ? evaCache : workCache).Float?.BeginPush(worldDirection);
        }

        private void UpdateLookDownSetback(ModelGaitRig rig, float elapsed)
        {
            float pitchDown = Mathf.Asin(Mathf.Clamp(-playerCamera.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
            float target = ModelGait.LookDownSetback(pitchDown, rig.LegLength);
            setback = elapsed > 0f && elapsed <= .25f ? Mathf.Lerp(setback, target, 1f - Mathf.Exp(-9f * elapsed)) : target;
        }

        private void ResetGait()
        {
            gaitWeight = 0f;
            gaitPhase = gaitSpeed = 0f;
            hasFeetPosition = false;
        }

        // Poses the grounded rig from the current gait + posture state, or restores the standing sample when idle.
        private void ApplyGroundPose()
        {
            ModelGaitRig rig = (activeEva ? evaCache : workCache).Ground;
            if (gaitWeight <= 0f && setback < .002f)
            {
                if (groundPosed) rig.RestoreStanding();
                groundPosed = false;
                return;
            }
            var frame = new ModelGaitFrame
            {
                Direction = gaitDirection.sqrMagnitude > 1e-6f ? gaitDirection.normalized : Vector3.forward,
                Phase = gaitPhase,
                Speed = gaitSpeed,
                Weight = gaitWeight,
                Setback = setback,
                LeftArm = (armMask & BodyArmVisibility.Left) != 0,
                RightArm = (armMask & BodyArmVisibility.Right) != 0
            };
            rig.Apply(visualRoot, frame);
            groundPosed = true;
        }

        private void ResetGroundMotion(bool restoreStanding)
        {
            if (restoreStanding && groundPosed)
                (activeEva ? evaCache : workCache).Ground.RestoreStanding();
            groundPosed = false;
            ResetGait();
            setback = 0f;
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
            var cache = new ModelCache(outfit.modelRoot, outfit.semanticBindings);
            cache.PrepareFloatRig();
            foreach (string name in new[] { "CharacterRoot", "upperarm01.L", "upperarm01.R", "lowerarm01.L", "lowerarm01.R", "wrist.L", "wrist.R" })
                if (!cache.Nodes.ContainsKey(name)) throw new InvalidOperationException("Body is missing imported joint " + name);
            return cache;
        }

        /// <summary>Resolve complete semantic aliases. No global leaf-name fallback in mapped mode.</summary>
        public static Dictionary<string, Transform> ResolveSemanticBindings(Transform root, SemanticBinding[] bindings)
        {
            if (!root || bindings == null || bindings.Length == 0)
                throw new ArgumentException("Supply explicit avatar semantic bindings and their model root.");
            var result = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (SemanticBinding binding in bindings)
            {
                if (string.IsNullOrWhiteSpace(binding.role) || binding.role != binding.role.Trim())
                    throw new InvalidOperationException("Avatar semantic role must be nonempty without surrounding whitespace.");
                if (!result.TryAdd(binding.role, ResolveModelPath(root, binding.path)))
                    throw new InvalidOperationException("Duplicate avatar semantic role " + binding.role);
            }
            return result;
        }

        /// <summary>Exact slash-separated relative path. Empty means root; ambiguous siblings are rejected.</summary>
        public static Transform ResolveModelPath(Transform root, string path)
        {
            if (!root || path == null) throw new ArgumentException("Supply a model root and an explicit relative path.");
            if (path.Length == 0) return root;
            Transform current = root;
            foreach (string segment in path.Split('/'))
            {
                if (segment.Length == 0 || segment == "." || segment == ".." || segment.IndexOf('\\') >= 0)
                    throw new InvalidOperationException("Malformed avatar relative path " + path);
                Transform next = null;
                for (int i = 0; i < current.childCount; i++)
                {
                    Transform child = current.GetChild(i);
                    if (!string.Equals(child.name, segment, StringComparison.Ordinal)) continue;
                    if (next) throw new InvalidOperationException("Ambiguous avatar path " + path);
                    next = child;
                }
                if (!next) throw new InvalidOperationException("Missing avatar path " + path);
                current = next;
            }
            return current;
        }

        private sealed class ModelCache
        {
            public readonly Dictionary<string, Transform> Nodes = new Dictionary<string, Transform>(StringComparer.Ordinal);
            public ModelGaitRig Ground { get; private set; }
            public ModelFloatRig Float { get; private set; }
            public void PrepareFloatRig()
            {
                if (Float != null) return;
                try { Float = new ModelFloatRig(Nodes); }
                catch (InvalidOperationException) { Float = null; }   // a rig without leg joints just has no float legs
            }
            private readonly Transform[] transforms;
            private readonly Vector3[] positions, scales;
            private readonly Quaternion[] rotations;
            public ModelCache(Transform root, SemanticBinding[] bindings)
            {
                transforms = root.GetComponentsInChildren<Transform>(true);
                bool mapped = bindings != null && bindings.Length > 0;
                if (mapped)
                    foreach (var entry in ResolveSemanticBindings(root, bindings)) Nodes.Add(entry.Key, entry.Value);
                positions = new Vector3[transforms.Length]; scales = new Vector3[transforms.Length]; rotations = new Quaternion[transforms.Length];
                for (int i = 0; i < transforms.Length; i++)
                {
                    Transform t = transforms[i];
                    if (!mapped && !Nodes.TryAdd(t.name, t)) throw new InvalidOperationException("Duplicate body node name " + t.name);
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
                if (Ground == null) Ground = new ModelGaitRig(Nodes);
                Ground.PrepareArms(Nodes);
            }
        }

        private static bool Finite(Vector3 v) => !float.IsNaN(v.x) && !float.IsInfinity(v.x)
            && !float.IsNaN(v.y) && !float.IsInfinity(v.y) && !float.IsNaN(v.z) && !float.IsInfinity(v.z);
    }
}
