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
        private bool groundPosed, gaitActive, hasYaw;
        private Vector3 gaitVelocity;
        private float yawRate, previousYaw, sinceMoved, ungroundedTime;
        private ModelGaitGroundProbe groundProbe;
        private readonly RaycastHit[] groundHits = new RaycastHit[8];
        private float? deltaOverride;
        private DepartureSeatRestraint departureSeat;
        private Func<bool> restQuery;
        private Func<RestStopReason> restReasonQuery;
        private Func<bool> restHold;
        private Transform berthExit;
        // SLEEP-SEQ wake destination: the live camera relative to the player feet (player yaw frame), measured at rest
        // start; whether the approach starts at the berth exit (a rest begun far away); whether the exit awaits its move.
        private Vector3 restEyeFromFeet = new Vector3(0f, 1.62f, 0f);
        private bool restFromExit, exitPending;
        private float exitPendingSince;
        private Transform berthFrame;
        private ModelRestRig restRig;
        private RestPhase restPhase;
        private BodyPose restNextPose;
        private float restTime, releaseChest, releaseHip;
        private bool restHazard, forcePoseReset;
        // Where the gameplay camera sits in the body frame during ordinary play, and the body's own eyes in the bag.
        private Vector3 normalCameraLocal = new Vector3(0f, 1.62f, .30f), restEyeLocal = new Vector3(0f, 1.65f, .09f);
        // SLEEP-SEQ: the camera lease asks for the view each frame (TryGetRestView); the body is placed from that same
        // view, so camera and body move as one first-person rig into the berth and back out.
        private BerthApproachPath.View restLive, restFrom, restView;
        private bool restHasLive, restViewValid, restFromHeld;
        private float restTurnSign = 1f, approachSeconds = 1.8f, exitStart, exitSeconds = 1.4f, unbuckleSeconds;
        private float restViewEye, restViewHandover, restFromEye, hazardSeconds = .5f;

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
        /// <summary>Berth rest presentation phase; None outside a rest. Core's IsResting stays the authority.</summary>
        public RestPhase RestPhase => restPhase;
        /// <summary>Seconds into the current rest phase (frozen while paused).</summary>
        public float RestPhaseSeconds => restTime;
        /// <summary>Raised on every rest phase change, including the immediate release on reset, stale session or disable.</summary>
        public event Action<RestPhase> RestPhaseChanged;
        /// <summary>True while a berth rest is configured and its frame is live: root keeps the body visible while resting.</summary>
        public bool PresentsRest => restQuery != null && berthFrame && berthFrame.gameObject.activeInHierarchy;
        /// <summary>
        /// SLEEP-SEQ: gameplay input waits while the berth sequence owns the body: the approach, straps and hold (Core
        /// rests anyway) and the ordinary wake (eyes open, unbuckle, float out). A hazard wake hands input back once
        /// the straps have sprung open (0.25 s); the quick float out to the live eye then finishes under player control.
        /// </summary>
        public bool RestBlocksControl => restPhase != RestPhase.None
            && !(restPhase == RestPhase.Releasing && restHazard && !berthExit && restTime >= BerthApproachPath.HazardControlSeconds);

        /// <summary>
        /// SLEEP-SEQ camera lease: where the actual camera must be this frame, given the live gameplay eye (where the
        /// camera would be without the lease). Approach: from the live eye to the berth mouth, turn round, back into the
        /// bag; straps and hold: the berth eye, looking down at the hands; wake: unbuckle in the berth, then out along
        /// the same path to the live eye. False when no berth sequence runs: the caller hands the view back.
        /// </summary>
        /// <summary>SLEEP-SEQ: the view has floated to the berth exit and waits for the caller to move the real controller there.</summary>
        public bool RestExitPending => exitPending && restPhase == RestPhase.Releasing;

        /// <summary>The authored berth exit: feet on the deck in front of SleepEntry, upright, facing into the cabin.</summary>
        public bool TryGetRestExit(out Vector3 feet, out float yaw)
        {
            feet = default; yaw = 0f;
            if (!berthExit || !berthExit.gameObject.activeInHierarchy) return false;
            Vector3 toBerth = Vector3.ProjectOnPlane(berthExit.forward, Vector3.up);
            if (toBerth.sqrMagnitude < 1e-6f) return false;
            toBerth.Normalize();
            feet = berthExit.position - toBerth * BerthApproachPath.ExitStandOff + Vector3.up * BerthApproachPath.ExitFeetLift;
            yaw = Quaternion.LookRotation(-toBerth, Vector3.up).eulerAngles.y;
            return true;
        }

        /// <summary>The caller has moved the real controller to the berth exit: the sequence ends now and nothing pops back.</summary>
        public void ConfirmRestExit()
        {
            if (!RestExitPending || notifyingReset) return;
            EndRest();
            if (bound && isActiveAndEnabled) Refresh();
        }

        // The view at the berth exit: the player's own eye over the exit feet, level, facing into the cabin.
        private BerthApproachPath.View ExitView()
        {
            TryGetRestExit(out Vector3 feet, out float yaw);
            return new BerthApproachPath.View { Position = feet + Quaternion.Euler(0f, yaw, 0f) * restEyeFromFeet, Yaw = yaw };
        }

        public bool TryGetRestView(Vector3 liveEyePosition, Quaternion liveEyeRotation, out Vector3 position, out Quaternion rotation)
        {
            position = liveEyePosition; rotation = liveEyeRotation;
            if (restPhase == RestPhase.None || restRig == null || !berthFrame) return false;
            restLive = BerthApproachPath.FromPose(liveEyePosition, liveEyeRotation);
            restHasLive = true;
            restView = EvaluateRestView(out restViewEye, out restViewHandover);
            restViewValid = true;
            position = restView.Position; rotation = restView.Rotation;
            return true;
        }
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

        /// <summary>
        /// BODY-REST-HOOK (root ACK #7 6895081311). Call after ConfigureSession on every session binding.
        /// <paramref name="isResting"/> is Core's authoritative ISurvivalRecovery.IsResting. <paramref name="berthBodyFrame"/>
        /// is the body origin in the bag: +Y towards the head, +Z out of the chest into the aisle, +X body right, metre
        /// scale, outside the body, player and camera. <paramref name="lastRestStop"/> (ISurvivalRecovery.LastRestStop)
        /// lets a hazard wake release within 0.3 s. Entering settles into the bag and draws the chest and hip straps;
        /// Held keeps them buckled; Releasing unclips and hands the body back. Pause freezes it; reset, stale session,
        /// disable or a hidden body release it at once. Never changes the player, controller, resources, input or camera.
        /// SLEEP-SEQ: <paramref name="holdRest"/> (optional, e.g. the chapter routine) keeps the body in the berth while it
        /// is true, across the routine's nights and waking hours: one entry, one release. <paramref name="berthExitAnchor"/>
        /// (optional: root's authored SleepEntry, feet on the deck, forward towards the berth) makes every exit end at the
        /// berth exit in front of the device: the view floats there, then <see cref="RestExitPending"/> asks the caller to
        /// move the real controller there (<see cref="TryGetRestExit"/>) and to call <see cref="ConfirmRestExit"/>.
        /// </summary>
        public void ConfigureRest(Func<bool> isResting, Transform berthBodyFrame, Func<RestStopReason> lastRestStop = null,
            Func<bool> holdRest = null, Transform berthExitAnchor = null)
        {
            if (!bound || notifyingReset)
                throw new InvalidOperationException("Bind a body session before configuring the berth rest.");
            if (isResting == null || !berthBodyFrame)
                throw new ArgumentException("The berth rest needs Core's IsResting and a berth body frame.");
            if (berthBodyFrame == visualRoot || berthBodyFrame.IsChildOf(visualRoot) || berthBodyFrame == playerFeet
                || berthBodyFrame.IsChildOf(playerFeet) || berthBodyFrame == playerCamera.transform
                || berthBodyFrame.IsChildOf(playerCamera.transform) || (berthBodyFrame.lossyScale - Vector3.one).sqrMagnitude > .000001f)
                throw new ArgumentException("The berth body frame must be a metre-scale scene anchor outside the body, player and camera.");
            if (berthExitAnchor && (berthExitAnchor == visualRoot || berthExitAnchor.IsChildOf(visualRoot) || berthExitAnchor == playerFeet
                || berthExitAnchor.IsChildOf(playerFeet) || berthExitAnchor.IsChildOf(playerCamera.transform)))
                throw new ArgumentException("The berth exit must be a scene anchor outside the body, player and camera.");
            CancelRest();
            restQuery = isResting; restReasonQuery = lastRestStop; berthFrame = berthBodyFrame;
            restHold = holdRest; berthExit = berthExitAnchor;
            if (isActiveAndEnabled) Refresh();
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
            if (!(Application.isPlaying || AllowEditModeDepartureSeat) || !bound || !isActiveAndEnabled || notifyingReset || departureSeat != null
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

        /// <summary>Edit-mode harness only: lets the departure seat lease run outside Play mode. Never set in the game.</summary>
        internal static bool AllowEditModeDepartureSeat;

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
            CancelRest();
            restQuery = null; restReasonQuery = null; berthFrame = null; restHold = null; berthExit = null;
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
            if (!permitted) { CancelRest(); if (visible) ResetOwnedVisuals(); return; }
            bool paused = clock.IsPaused;
            bool nextEva = suit.IsSuitOn;
            BodyPose nextPose = seatedRequested ? BodyPose.Seated : floatingQuery() ? BodyPose.Floating : BodyPose.Standing;
            if (version != bindingVersion || !bound) return;
            BodyArmVisibility nextArms = armsQuery() & BodyArmVisibility.Both;
            if (version != bindingVersion || !bound) return;
            // Berth rest: Core's IsResting drives a presentation that owns the body until it hands it back.
            if (visible && nextEva == activeEva && RestOwnsBody(paused, trackMovement, nextArms)) { wasPaused = paused; return; }
            if (restPhase != RestPhase.None) CancelRest();
            if (version != bindingVersion || !bound) return;
            // Existing pose/position freezes while paused. Outfit safety and visibility still reconcile.
            if (paused && visible && nextEva == activeEva) nextPose = pose;
            bool wasVisible = visible;
            bool poseChanged = !visible || nextEva != activeEva || nextPose != pose;
            bool reset = poseChanged || nextArms != armMask || forcePoseReset
                || (paused && !wasPaused) || (offsetResetRequested && !paused);
            if (reset)
            {
                NotifyReset();
                HideRenderers();
                if (poseChanged || forcePoseReset || offsetResetRequested && !paused) ResetGroundMotion(false);
                forcePoseReset = false;
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
            if (!paused) normalCameraLocal = Quaternion.Inverse(visualRoot.rotation) * (playerCamera.transform.position - visualRoot.position);
            if (pose == BodyPose.Floating) UpdateFloatingLegs(reset, paused, trackMovement);
            if (pose == BodyPose.Standing && groundController)
            {
                if (paused || Time.timeScale <= 0f)
                {
                    hasFeetPosition = false;
                    // The pause reset still releases presentation leases. Preserve the standing gait frame.
                    if (reset) ApplyGroundPose(0f, gaitActive);
                }
                else if (trackMovement) UpdateGroundMotion();
                else if (reset) ApplyGroundPose(0f, gaitActive);
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
            float yaw = visualRoot.eulerAngles.y;
            if (hasYaw && elapsed > 0f && elapsed <= .25f)
                yawRate = Mathf.Lerp(yawRate, Mathf.DeltaAngle(previousYaw, yaw) / elapsed, 1f - Mathf.Exp(-12f * elapsed));
            previousYaw = yaw; hasYaw = true;

            // AdvancePresentation (tests and frame capture) has no physics step: it trusts the supplied feet motion.
            bool synthetic = deltaOverride.HasValue;
            // CharacterController.isGrounded flickers on slopes and steps; a short flicker keeps the feet planted.
            ungroundedTime = synthetic || groundController.isGrounded ? 0f : ungroundedTime + Mathf.Max(0f, elapsed);
            bool walkable = groundController.enabled && ungroundedTime < .15f && !suit.IsOutside && groundedQuery();
            if (!walkable)
            {
                // No stepping outside the grounded mode: the feet stay on their standing slots.
                ResetGait();
                rig.ResetFeet();
                ApplyGroundPose(0f, false);
                return;
            }

            Vector3 feetPosition = playerFeet.position;
            Vector3 displacement = Vector3.ProjectOnPlane(feetPosition - previousFeetPosition, Vector3.up);
            previousFeetPosition = feetPosition;
            if (!hasFeetPosition)
            {
                hasFeetPosition = true;
                ApplyGroundPose(elapsed, false);
                return;
            }

            float distance = displacement.magnitude;
            Vector3 controllerTravel = synthetic ? displacement : Vector3.ProjectOnPlane(groundController.velocity, Vector3.up) * elapsed;
            // A warp or a hitch re-plants the feet at the standing slots instead of cycling to catch up.
            if (elapsed <= 0f || elapsed > .25f || !Finite(displacement) || !Finite(controllerTravel) || distance > .65f)
            {
                ResetGait();
                rig.ResetFeet();
                ApplyGroundPose(0f, false);
                return;
            }

            // Motion the controller did not make (a push, a carrier) does not drive the gait; planted feet stay put.
            bool consistent = (displacement - controllerTravel).magnitude <= .015f + distance * .2f;
            bool moved = consistent && distance > elapsed * .08f && Vector3.Dot(displacement, controllerTravel) > 0f;
            sinceMoved = moved ? 0f : sinceMoved + elapsed;
            // A single still frame inside a walk does not end the gait.
            bool moving = moved || (gaitActive && sinceMoved < .12f);
            gaitWeight = Mathf.MoveTowards(gaitWeight, moving ? 1f : 0f, elapsed * (moving ? 9f : 7f));
            if (moved)
            {
                Vector3 nextDirection = Quaternion.Inverse(visualRoot.rotation) * (displacement / distance);
                float measured = distance / elapsed;
                if (!gaitActive)
                {
                    gaitDirection = nextDirection;
                    gaitSpeed = measured;
                    gaitVelocity = displacement / elapsed;
                    gaitPhase = rig.StartPhase(ModelGait.Stance(Mathf.Max(gaitSpeed, .3f)));
                    gaitActive = true;
                }
                else
                {
                    gaitDirection = Vector3.RotateTowards(gaitDirection, nextDirection, elapsed * 16f, 0f);
                    gaitSpeed = Mathf.Lerp(gaitSpeed, measured, 1f - Mathf.Exp(-10f * elapsed));
                    gaitVelocity = Vector3.Lerp(gaitVelocity, displacement / elapsed, 1f - Mathf.Exp(-12f * elapsed));
                }
                ModelGaitProfile profile = activeEva ? ModelGaitProfile.Eva : ModelGaitProfile.Work;
                float stride = ModelGait.StrideLength(Mathf.Max(gaitSpeed, .3f), rig.LegLength, Mathf.Abs(gaitDirection.x)) * profile.StrideScale;
                gaitPhase = Mathf.Repeat(gaitPhase + distance / stride, 1f);
            }
            if (!moving)
            {
                gaitActive = false;
                gaitVelocity = Vector3.zero;
                if (gaitWeight <= 0f) gaitSpeed = 0f;
            }
            ApplyGroundPose(elapsed, gaitActive);
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
            hasFeetPosition = gaitActive = hasYaw = false;
            gaitVelocity = Vector3.zero;
            yawRate = sinceMoved = 0f;
        }

        // Poses the grounded rig every frame: planted feet, settle and turn steps, gait and the look-down setback.
        private void ApplyGroundPose(float deltaTime, bool moving)
        {
            ModelGaitRig rig = (activeEva ? evaCache : workCache).Ground;
            groundProbe ??= ProbeGround;
            var frame = new ModelGaitFrame
            {
                Direction = gaitDirection.sqrMagnitude > 1e-6f ? gaitDirection.normalized : Vector3.forward,
                Phase = gaitPhase,
                Speed = gaitSpeed,
                Weight = gaitWeight,
                Moving = moving,
                Velocity = gaitVelocity,
                YawRate = yawRate,
                Pivot = playerFeet.position,
                DeltaTime = deltaTime,
                Setback = setback,
                LeftArm = (armMask & BodyArmVisibility.Left) != 0,
                RightArm = (armMask & BodyArmVisibility.Right) != 0,
                Eva = activeEva,
                Ground = groundProbe
            };
            rig.Apply(visualRoot, frame);
            groundPosed = true;
        }

        // Nearest walkable surface straight below, ignoring the player capsule, triggers and the body itself.
        private bool ProbeGround(Vector3 origin, float distance, out float height, out Vector3 normal)
        {
            height = 0f; normal = Vector3.up;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, distance, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = groundHits[i];
                Collider surface = hit.collider;
                if (!surface || surface == groundController || hit.normal.y < .55f || hit.distance >= best) continue;
                Transform t = surface.transform;
                if ((playerFeet && (t == playerFeet || t.IsChildOf(playerFeet))) || (visualRoot && t.IsChildOf(visualRoot))) continue;
                best = hit.distance; height = hit.point.y; normal = hit.normal;
            }
            return best < float.MaxValue;
        }

        /// <summary>Test access to the active outfit grounded rig.</summary>
        internal ModelGaitRig ActiveGroundRig => (activeEva ? evaCache : workCache)?.Ground;

        private void ResetGroundMotion(bool restoreStanding)
        {
            if (restoreStanding && groundPosed)
                (activeEva ? evaCache : workCache).Ground.RestoreStanding();
            groundPosed = false;
            ResetGait();
            setback = 0f;
            workCache?.Ground?.ResetFeet();
            evaCache?.Ground?.ResetFeet();
        }

        // Returns true while the berth rest presents the body this frame (the normal pose flow is skipped).
        private bool RestOwnsBody(bool paused, bool trackMovement, BodyArmVisibility nextArms)
        {
            bool resting = false;
            if (PresentsRest && !seatedRequested)
            {
                int version = bindingVersion;
                try { resting = restQuery() || (restHold != null && restHold()); }
                catch (Exception error) { Debug.LogException(error, this); resting = false; }
                if (version != bindingVersion || !bound) return true;
            }
            if (restPhase == RestPhase.None)
            {
                if (!resting || paused || !BeginRest()) return false;
            }
            else if (!PresentsRest || seatedRequested) { CancelRest(); return false; }

            float dt = paused || !trackMovement ? 0f : Mathf.Clamp(deltaOverride ?? Time.unscaledDeltaTime, 0f, .1f);
            if (restPhase == RestPhase.Entering)
            {
                if (!resting) StartRelease();
                else if ((restTime += dt) >= EnterSeconds) { restTime = 0f; SetRestPhase(RestPhase.Held); }
            }
            else if (restPhase == RestPhase.Held)
            {
                if (!resting) StartRelease();
                else restTime += dt;
            }
            else if ((restTime += dt) >= ReleaseSeconds)
            {
                // With a berth exit the caller first moves the real controller there (next frame, before the camera lease
                // hands the view back); a caller that never confirms still gets the body back after half a second.
                if (berthExit && (!exitPending || restTime - exitPendingSince < .5f))
                {
                    if (!exitPending) { exitPending = true; exitPendingSince = restTime; }
                }
                else
                {
                    EndRest();
                    return false;
                }
            }
            if (restPhase == RestPhase.None || restRig == null) return false;
            Outfit selected = activeEva ? eva : work;
            selected.leftArm.enabled = (nextArms & BodyArmVisibility.Left) != 0;
            selected.rightArm.enabled = (nextArms & BodyArmVisibility.Right) != 0;
            ApplyRestPose();
            return true;
        }

        // SLEEP-SEQ timeline: approach (float in, turn, back into the bag) replaces the old in-place settle; the strap
        // timeline follows it. Ordinary wake: eyes open, unbuckle in the berth, float out. Hazard: spring out at once.
        private float EnterSeconds => approachSeconds + ModelRestTimeline.EnterDuration - ModelRestTimeline.SettleEnd;
        private float StrapTime(float t) => t - approachSeconds + ModelRestTimeline.SettleEnd;
        private float ReleaseSeconds => restHazard ? hazardSeconds : exitStart + exitSeconds;

        private bool BeginRest()
        {
            Outfit outfit = activeEva ? eva : work;
            ModelCache cache = activeEva ? evaCache : workCache;
            Shader lit = FindLit(outfit);
            if (!lit || !outfit.standing || !berthFrame) return false;
            // A rest that starts far from the berth (the routine, R pressed elsewhere) starts at the berth exit: a cut the
            // routine montage covers. Without an exit anchor it shows no walk-in through the ship.
            Vector3 eyeNow = playerCamera.transform.position;
            Vector3 berthEyeEstimate = berthFrame.TransformPoint(restEyeLocal);
            bool far = Vector3.Distance(eyeNow, berthEyeEstimate) > BerthApproachPath.MaxPresentDistance;
            if (far && !TryGetRestExit(out _, out _)) return false;
            NotifyReset();
            ResetGroundMotion(false);
            evaCache.Float?.CancelPush(); workCache.Float?.CancelPush();
            Vector3 fromPosition = visualRoot.position;
            Quaternion fromRotation = visualRoot.rotation;
            var rig = new ModelRestRig(cache.Transforms, cache.Nodes, visualRoot, lit);
            try
            {
                // From whatever the body shows now to the held standing sample, settled in the bag.
                rig.CaptureFrom();
                cache.Restore();
                outfit.standing.SampleAnimation(outfit.modelRoot.gameObject, 0f);
                visualRoot.SetPositionAndRotation(berthFrame.position, berthFrame.rotation);
                rig.CaptureTo();
                rig.Measure(visualRoot, new[] { outfit.torsoLegs, outfit.leftArm, outfit.rightArm }, cache.Nodes);
                if (cache.Nodes.TryGetValue("eye.L", out Transform eyeLeft) && cache.Nodes.TryGetValue("eye.R", out Transform eyeRight))
                    restEyeLocal = visualRoot.InverseTransformPoint((eyeLeft.position + eyeRight.position) * .5f);
                rig.Blend(0f);
                visualRoot.SetPositionAndRotation(fromPosition, fromRotation);
            }
            catch
            {
                rig.Dispose();
                throw;
            }
            restRig = rig;
            restTime = 0f; restHazard = false;
            // The approach starts at the live eye (the camera is not leased yet), or at the berth exit for a far start,
            // and turns the shorter way round.
            Quaternion feetYaw = Quaternion.Euler(0f, playerFeet.eulerAngles.y, 0f);
            restEyeFromFeet = Quaternion.Inverse(feetYaw) * (eyeNow - playerFeet.position);
            restEyeFromFeet.x = 0f; // no sideways lean at the exit: Teleport levels the view and clears the lean
            restFromExit = far; exitPending = false;
            Vector3 berthEye = berthFrame.TransformPoint(restEyeLocal);
            Vector3 mouth = BerthApproachPath.Mouth(berthEye, berthFrame.forward);
            BerthApproachPath.View start = restFromExit ? ExitView() : BerthApproachPath.FromPose(eyeNow, playerCamera.transform.rotation);
            approachSeconds = BerthApproachPath.ApproachSeconds(start.Position, mouth, berthEye);
            float turn = Mathf.DeltaAngle(start.Yaw, berthFrame.eulerAngles.y);
            restTurnSign = turn < 0f ? -1f : 1f;
            restLive = BerthApproachPath.FromPose(eyeNow, playerCamera.transform.rotation);
            restHasLive = false; restViewValid = false; restFromHeld = false;
            restViewEye = restViewHandover = 0f;
            PoseRevision++;
            SetRestPhase(RestPhase.Entering);
            return restPhase == RestPhase.Entering;
        }

        private void StartRelease()
        {
            RestStopReason reason = RestStopReason.None;
            if (restReasonQuery != null)
            {
                try { reason = restReasonQuery(); }
                catch (Exception error) { Debug.LogException(error, this); }
            }
            restHazard = ModelRestTimeline.IsHazard(reason);
            releaseChest = restRig.ChestClosure; releaseHip = restRig.HipClosure;
            // The exit starts exactly where the view is now (in the berth, or part-way in on an early wake).
            restFromHeld = restPhase == RestPhase.Held;
            restFrom = restViewValid ? restView : BerthApproachPath.FromPose(playerCamera.transform.position, playerCamera.transform.rotation);
            restFromEye = restViewValid ? restViewEye : RestDockWeight;
            unbuckleSeconds = releaseChest > .01f || releaseHip > .01f ? BerthApproachPath.UnbuckleSeconds : 0f;
            exitStart = (restFromHeld ? BerthApproachPath.WakeSeconds : 0f) + unbuckleSeconds;
            Vector3 berthEye = berthFrame.TransformPoint(restEyeLocal);
            Vector3 mouthNow = BerthApproachPath.Mouth(berthEye, berthFrame.forward);
            Vector3 liveNow = restHasLive ? restLive.Position : restFrom.Position;
            exitSeconds = BerthApproachPath.ExitSeconds(restFrom.Position, mouthNow, liveNow);
            hazardSeconds = BerthApproachPath.HazardExitSeconds(restFrom.Position, mouthNow, liveNow);
            Outfit outfit = activeEva ? eva : work;
            ModelCache cache = activeEva ? evaCache : workCache;
            restNextPose = seatedRequested ? BodyPose.Seated : floatingQuery() ? BodyPose.Floating : BodyPose.Standing;
            // From the bag pose on screen to the pose the body returns to.
            restRig.CaptureFrom();
            cache.Restore();
            AnimationClip next = restNextPose == BodyPose.Floating ? outfit.floating
                : restNextPose == BodyPose.Seated ? outfit.seated : outfit.standing;
            next.SampleAnimation(outfit.modelRoot.gameObject, 0f);
            restRig.CaptureTo();
            restRig.Blend(0f);
            restTime = 0f;
            SetRestPhase(RestPhase.Releasing);
        }

        private void EndRest()
        {
            ModelRestRig rig = restRig;
            restRig = null;
            rig?.Dispose();
            restTime = 0f;
            restViewValid = restHasLive = false;
            exitPending = restFromExit = false;
            forcePoseReset = true;
            SetRestPhase(RestPhase.None);
        }

        // Immediate release (reset, stale session, disable, hidden body): straps and hand gone at once.
        private void CancelRest()
        {
            ModelRestRig rig = restRig;
            restRig = null;
            rig?.Dispose();
            restTime = 0f;
            restViewValid = restHasLive = false;
            exitPending = restFromExit = false;
            if (restPhase == RestPhase.None) return;
            forcePoseReset = true;
            SetRestPhase(RestPhase.None);
        }

        private void SetRestPhase(RestPhase next)
        {
            if (restPhase == next) return;
            restPhase = next;
            try { RestPhaseChanged?.Invoke(next); }
            catch (Exception error) { Debug.LogException(error, this); }
        }

        // How far the actual camera has moved from the gameplay eye to the berth eye. The body goes where the eye is:
        // a camera cut into the berth docks it at once, a camera move carries it along; it never floats in front of the eye.
        private float RestDock(Vector3 followPosition, Quaternion followRotation)
        {
            Vector3 eye = playerCamera.transform.position;
            float toBerth = Vector3.Distance(eye, berthFrame.TransformPoint(restEyeLocal));
            float toPlayer = Vector3.Distance(eye, followPosition + followRotation * normalCameraLocal);
            float sum = toBerth + toPlayer;
            return sum < 1e-4f ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.15f, .85f, toPlayer / sum));
        }

        private void ApplyRestPose()
        {
            float t = restTime, blend, chest, hip, hand = 0f;
            bool onHip = false, atBuckle = false, releasing = restPhase == RestPhase.Releasing;
            Vector3 followPosition; Quaternion followRotation;
            if ((releasing || restFromExit) && TryGetRestExit(out Vector3 exitFeet, out float exitYaw))
            {
                // This end of the path is the berth exit, not wherever the player last floated.
                followRotation = Quaternion.Euler(0f, exitYaw, 0f);
                followPosition = exitFeet + followRotation * GetVisualFeetOffset(releasing ? restNextPose : pose);
            }
            else FollowPose(releasing ? restNextPose : pose, out followPosition, out followRotation);
            if (releasing)
            {
                if (restHazard)
                {
                    float open = ModelRestTimeline.HazardOpen(t);
                    chest = releaseChest * open; hip = releaseHip * open;
                    blend = BerthApproachPath.Ease(t / hazardSeconds);
                }
                else
                {
                    // Eyes open first (from a hold), then the hand unclips chest and hips; the body leaves the bag after.
                    float tt = t - (restFromHeld ? BerthApproachPath.WakeSeconds : 0f);
                    chest = releaseChest * ModelRestTimeline.ReleaseChest(tt);
                    hip = releaseHip * ModelRestTimeline.ReleaseHip(tt);
                    hand = releaseChest > .5f || releaseHip > .5f ? ModelRestTimeline.ReleaseHand(tt) : 0f;
                    onHip = ModelRestTimeline.ReleaseHandOnHip(tt) || releaseChest <= .5f;
                    atBuckle = true;
                    float exit = BerthApproachPath.Ease(Mathf.Clamp01((t - exitStart) / exitSeconds));
                    blend = BerthApproachPath.Ease(Mathf.InverseLerp(0f, .75f, exit));
                }
            }
            else
            {
                bool entering = restPhase == RestPhase.Entering;
                float st = StrapTime(t);
                // The limbs settle into the bag pose while the body backs in; the straps follow the approach.
                blend = entering ? BerthApproachPath.Ease(Mathf.InverseLerp(.35f, 1f, BerthApproachPath.Ease(t / approachSeconds))) : 1f;
                chest = entering ? ModelRestTimeline.ChestClose(st) : 1f;
                hip = entering ? ModelRestTimeline.HipClose(st) : 1f;
                if (entering) { hand = ModelRestTimeline.EnterHand(st); onHip = ModelRestTimeline.EnterHandOnHip(st); }
            }
            float dock;
            if (restViewValid && Vector3.Distance(playerCamera.transform.position, restView.Position) < .12f)
            {
                // The camera sits on the leased view: the body is its first-person rig (eyes at the camera, upright,
                // yaw only), handed over exactly to the follow pose at both ends of the path.
                Quaternion yaw = Quaternion.Euler(0f, restView.Yaw, 0f);
                Vector3 root = restView.Position - yaw * Vector3.Lerp(normalCameraLocal, restEyeLocal, restViewEye);
                visualRoot.SetPositionAndRotation(Vector3.Lerp(root, followPosition, restViewHandover),
                    Quaternion.Slerp(yaw, followRotation, restViewHandover));
                dock = restViewEye;
            }
            else
            {
                // No lease this frame (a camera cut, or no camera move at all): the body goes where the eye actually is.
                dock = RestDock(followPosition, followRotation);
                visualRoot.SetPositionAndRotation(Vector3.Lerp(followPosition, berthFrame.position, dock),
                    Quaternion.Slerp(followRotation, berthFrame.rotation, dock));
            }
            restRig.Blend(blend);
            // Straps and the strapping hand belong to the berth: shown only with the body in it.
            if (dock < .9f) { chest = hip = hand = 0f; }
            restRig.Present(visualRoot, chest, hip, hand, onHip, atBuckle);
            RestDockWeight = dock;
        }

        /// <summary>0 = body at the gameplay eye, 1 = docked in the berth (follows where root's camera actually is).</summary>
        public float RestDockWeight { get; private set; }

        // SLEEP-SEQ view for the current phase and time. eyeBlend: 0 = the gameplay camera offset in the body frame,
        // 1 = the body's own eyes in the bag. handover: 1 = the body sits exactly on its follow pose (path ends).
        private BerthApproachPath.View EvaluateRestView(out float eyeBlend, out float handover)
        {
            Vector3 berthEye = berthFrame.TransformPoint(restEyeLocal);
            float berthYaw = berthFrame.eulerAngles.y;
            Vector3 mouth = BerthApproachPath.Mouth(berthEye, berthFrame.forward);
            var berth = new BerthApproachPath.View { Position = berthEye, Yaw = berthYaw, Pitch = BerthApproachPath.StrapPitch };
            float t = restTime;
            eyeBlend = 1f; handover = 0f;
            switch (restPhase)
            {
                case RestPhase.Entering:
                {
                    if (t >= approachSeconds) return berth;
                    float u = Mathf.Clamp01(t / approachSeconds);
                    float p = BerthApproachPath.Ease(u);
                    eyeBlend = BerthApproachPath.Ease(Mathf.InverseLerp(.3f, 1f, p));
                    handover = 1f - BerthApproachPath.Ease(Mathf.InverseLerp(0f, .15f, p));
                    return BerthApproachPath.Approach(restFromExit ? ExitView() : restLive, mouth, berthEye, berthYaw, restTurnSign, u);
                }
                case RestPhase.Held:
                    berth.Pitch = Mathf.Lerp(BerthApproachPath.StrapPitch, BerthApproachPath.HeldPitch, BerthApproachPath.Ease(t / 1.2f));
                    return berth;
                case RestPhase.Releasing:
                {
                    float exitFrom = restHazard ? 0f : exitStart;
                    float exitDuration = restHazard ? hazardSeconds : exitSeconds;
                    BerthApproachPath.View from = restFrom;
                    if (!restHazard && exitStart > 0f)
                    {
                        // Eyes open, then the hand unbuckles: the view stays in the berth, looking down at the buckles.
                        from.Pitch = Mathf.Lerp(restFrom.Pitch, BerthApproachPath.UnbucklePitch, BerthApproachPath.Ease(
                            Mathf.InverseLerp(restFromHeld ? .1f : 0f, restFromHeld ? BerthApproachPath.WakeSeconds : .3f, t)));
                        if (t < exitFrom) { eyeBlend = restFromEye; return from; }
                    }
                    float e = Mathf.Clamp01((t - exitFrom) / exitDuration);
                    float q = BerthApproachPath.Ease(e);
                    eyeBlend = restFromEye * (1f - BerthApproachPath.Ease(Mathf.InverseLerp(0f, .75f, q)));
                    handover = BerthApproachPath.Ease(Mathf.InverseLerp(.8f, 1f, q));
                    return BerthApproachPath.Exit(from, berthExit ? ExitView() : restLive, mouth, berthEye, restTurnSign, e);
                }
            }
            return berth;
        }

        private void FollowPose(BodyPose forPose, out Vector3 position, out Quaternion rotation)
        {
            Vector3 forward = Vector3.ProjectOnPlane(playerFeet.forward, Vector3.up);
            rotation = forward.sqrMagnitude < 1e-8f ? visualRoot.rotation : Quaternion.LookRotation(forward.normalized, Vector3.up);
            position = playerFeet.position + rotation * GetVisualFeetOffset(forPose);
        }

        private static Shader FindLit(Outfit outfit)
        {
            if (outfit.torsoLegs)
                foreach (Material material in outfit.torsoLegs.sharedMaterials)
                    if (material && material.shader && material.shader.name == "Universal Render Pipeline/Lit") return material.shader;
            return Shader.Find("Universal Render Pipeline/Lit");
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
            CancelRest();
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
            public Transform[] Transforms => transforms;
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
