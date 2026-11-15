using System;
using OutOfReach.Contracts;
using UnityEngine;

namespace OutOfReach.Presentation.Hands
{
    /// <summary>Optional WORK palm push on the existing skin. Rotations only; root owns visibility, input and physics.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(250)]
    public sealed class BareArmContactPresenter : MonoBehaviour
    {
        [SerializeField, Range(.15f, .25f)] private float reachSeconds = .20f;
        [SerializeField, Range(.15f, .35f)] private float returnSeconds = .24f;
        [SerializeField] private BareRailGripPose gripPose;
        public float GripAxialRotationDegrees => grip != null ? gripAxialDegrees : 0f;
        public bool GripPoseConfigured => gripPose && gripPose.IsValid;
        public int GripMeasuredVertexCount => grip != null ? grip.VertexCount : 0;
        public float GripMinimumClearanceMetres => grip != null ? grip.MinimumClearance : float.NaN;
        public float GripMaximumRegionGapMetres => grip != null ? grip.MaximumRegionGap : float.NaN;
        public float GripRegionGapMetres(int region) => grip != null && region >= 0 && region < 6 ? grip.RegionGap(region) : float.NaN;
        public void ConfigureGripPose(BareRailGripPose pose) { CancelImmediately(); gripPose = pose; }
        private BareArmContactRig left, right;
        private Transform eye;
        private ISimulationClock clock;
        private Func<ContactHand, bool> leaseAllowed;
        private Func<int> readRevision;
        private Func<HandContactSample, bool> validate;
        private int generation;
        private const float OverheadForwardArc = .12f;
        // Two leases on the existing left/right skeleton. The solver below runs against one
        // record at a time; paired dispatch waits until BOTH visible poses pass their original proof.
        private sealed class ArmState
        {
            public BareRailGripLease grip;
            public Func<HandContactTarget?> readHeldGrip;
            public float gripClose;
            public float gripReturnClose;
            public float gripAxialDegrees;
            public BareArmContactRig active;
            public Func<HandContactSample, bool> action;
            public HandContactTarget target;
            public BarePalmContactPose calibration;
            public HandContactSample lastSample;
            public ContactHand hand;
            public int poseRevision;
            public float elapsed;
            public float lastBlend;
            public float returnFromBlend;
            public Vector3 lastFinalWrist;
            public Quaternion lastFinalRotation;
            public Vector3 palmPoint;
            public Vector3 startWrist;
            public Vector3 elbowPole;
            public Vector3 previousPoint;
            public Vector3 previousEye;
            public Quaternion palmFrame;
            public Quaternion startWristRotation;
            public bool overheadReach;
            public float overheadPoleSign;
            public Quaternion[] baseline = new Quaternion[13];
            public Quaternion[] previousPose = new Quaternion[13];
            public Vector3 diagnosticPoint;
            public Vector3 diagnosticNormal;
            public Vector3 diagnosticTangent;
            public Vector3 diagnosticEyeLocal;
            public Vector3 diagnosticWrist;
            public float diagnosticBlend;
            public float diagnosticWristAngle;
            public bool diagnosticHasTarget;
            public bool diagnosticHasWrist;
            public HandContactStage WorkingStage;
            public float ContactErrorMetres = float.NaN;
            public float MinimumHandPlaneClearanceMetres = float.NaN;
            public float PalmPatchPlaneDistanceMetres = float.NaN;
            public float MeasuredWaypointCorrectionMetres;
            public int SafeSolveAttempts;
            public string LastRefusalReason = string.Empty;
            public string LastRefusalDiagnostic = string.Empty;
        }
        private readonly ArmState leftState = new ArmState(), rightState = new ArmState();
        private ArmState selectedState;
        private ArmState Current => selectedState ?? leftState;
        private bool pairedPush;
        private Func<HandContactSample, bool> pairAction;
        public HandContactStage ContactStage => leftState.WorkingStage != HandContactStage.None
            ? leftState.WorkingStage : rightState.WorkingStage;
        public HandContactStage ContactStageFor(ContactHand selected) => StateFor(selected)?.WorkingStage ?? HandContactStage.None;
        public float ContactErrorFor(ContactHand selected) => StateFor(selected)?.ContactErrorMetres ?? float.NaN;
        private ArmState StateFor(ContactHand selected) => selected == ContactHand.Left ? leftState
            : selected == ContactHand.Right ? rightState : null;
        private BareRailGripLease grip { get => Current.grip; set => Current.grip = value; }
        private Func<HandContactTarget?> readHeldGrip { get => Current.readHeldGrip; set => Current.readHeldGrip = value; }
        private float gripClose { get => Current.gripClose; set => Current.gripClose = value; }
        private float gripReturnClose { get => Current.gripReturnClose; set => Current.gripReturnClose = value; }
        private float gripAxialDegrees { get => Current.gripAxialDegrees; set => Current.gripAxialDegrees = value; }
        private BareArmContactRig active { get => Current.active; set => Current.active = value; }
        private Func<HandContactSample, bool> action { get => Current.action; set => Current.action = value; }
        private HandContactTarget target { get => Current.target; set => Current.target = value; }
        private BarePalmContactPose calibration { get => Current.calibration; set => Current.calibration = value; }
        private HandContactSample lastSample { get => Current.lastSample; set => Current.lastSample = value; }
        private ContactHand hand { get => Current.hand; set => Current.hand = value; }
        private int poseRevision { get => Current.poseRevision; set => Current.poseRevision = value; }
        private float elapsed { get => Current.elapsed; set => Current.elapsed = value; }
        private float lastBlend { get => Current.lastBlend; set => Current.lastBlend = value; }
        private float returnFromBlend { get => Current.returnFromBlend; set => Current.returnFromBlend = value; }
        private Vector3 lastFinalWrist { get => Current.lastFinalWrist; set => Current.lastFinalWrist = value; }
        private Quaternion lastFinalRotation { get => Current.lastFinalRotation; set => Current.lastFinalRotation = value; }
        private Vector3 palmPoint { get => Current.palmPoint; set => Current.palmPoint = value; }
        private Vector3 startWrist { get => Current.startWrist; set => Current.startWrist = value; }
        private Vector3 elbowPole { get => Current.elbowPole; set => Current.elbowPole = value; }
        private Vector3 previousPoint { get => Current.previousPoint; set => Current.previousPoint = value; }
        private Vector3 previousEye { get => Current.previousEye; set => Current.previousEye = value; }
        private Quaternion palmFrame { get => Current.palmFrame; set => Current.palmFrame = value; }
        private Quaternion startWristRotation { get => Current.startWristRotation; set => Current.startWristRotation = value; }
        private bool overheadReach { get => Current.overheadReach; set => Current.overheadReach = value; }
        private float overheadPoleSign { get => Current.overheadPoleSign; set => Current.overheadPoleSign = value; }
        private Quaternion[] baseline { get => Current.baseline; set => Current.baseline = value; }
        private Quaternion[] previousPose { get => Current.previousPose; set => Current.previousPose = value; }
        private Vector3 diagnosticPoint { get => Current.diagnosticPoint; set => Current.diagnosticPoint = value; }
        private Vector3 diagnosticNormal { get => Current.diagnosticNormal; set => Current.diagnosticNormal = value; }
        private Vector3 diagnosticTangent { get => Current.diagnosticTangent; set => Current.diagnosticTangent = value; }
        private Vector3 diagnosticEyeLocal { get => Current.diagnosticEyeLocal; set => Current.diagnosticEyeLocal = value; }
        private Vector3 diagnosticWrist { get => Current.diagnosticWrist; set => Current.diagnosticWrist = value; }
        private float diagnosticBlend { get => Current.diagnosticBlend; set => Current.diagnosticBlend = value; }
        private float diagnosticWristAngle { get => Current.diagnosticWristAngle; set => Current.diagnosticWristAngle = value; }
        private bool diagnosticHasTarget { get => Current.diagnosticHasTarget; set => Current.diagnosticHasTarget = value; }
        private bool diagnosticHasWrist { get => Current.diagnosticHasWrist; set => Current.diagnosticHasWrist = value; }

        private const float PlaneTolerance = .0001f;
        private HandContactStage WorkingStage { get => Current.WorkingStage; set => Current.WorkingStage = value; }
        public float ContactErrorMetres { get => Current.ContactErrorMetres; private set => Current.ContactErrorMetres = value; }
        public float MinimumHandPlaneClearanceMetres { get => Current.MinimumHandPlaneClearanceMetres; private set => Current.MinimumHandPlaneClearanceMetres = value; }
        public float PalmPatchPlaneDistanceMetres { get => Current.PalmPatchPlaneDistanceMetres; private set => Current.PalmPatchPlaneDistanceMetres = value; }
        public float MeasuredWaypointCorrectionMetres { get => Current.MeasuredWaypointCorrectionMetres; private set => Current.MeasuredWaypointCorrectionMetres = value; }
        public int SafeSolveAttempts { get => Current.SafeSolveAttempts; private set => Current.SafeSolveAttempts = value; }
        public string LastRefusalReason { get => Current.LastRefusalReason; private set => Current.LastRefusalReason = value; }
        public int ContactSupportVertexIndex => calibration?.SupportIndex ?? -1;
        public int ContactHandVertexCount => calibration?.SkinIndices.Length ?? 0;
        public int ContactPatchVertexCount => calibration?.PatchIndices.Length ?? 0;
        public int GetContactPatchVertexIndex(int index) => calibration != null && index >= 0 && index < calibration.PatchIndices.Length
            ? calibration.PatchIndices[index] : -1;

        /// <summary>Root must subscribe CancelImmediately to EmbodiedBodyPresenter.PoseWillReset before use.</summary>
        public void Configure(BareArmContactRig leftArm, BareArmContactRig rightArm, Transform unbobbedEye,
            ISimulationClock simulationClock, Func<ContactHand, bool> isWorkArmLeaseAllowed,
            Func<int> currentBodyPoseRevision, Func<HandContactSample, bool> validateActualContact)
        {
            ClearBinding();
            if (leftArm == null || rightArm == null || !leftArm.IsUsable || !rightArm.IsUsable || !unbobbedEye
                || simulationClock == null || isWorkArmLeaseAllowed == null || currentBodyPoseRevision == null
                || validateActualContact == null) throw new ArgumentException("Bind the existing WORK arms and body/contact authority.");
            left = leftArm; right = rightArm; eye = unbobbedEye; clock = simulationClock;
            leaseAllowed = isWorkArmLeaseAllowed; readRevision = currentBodyPoseRevision; validate = validateActualContact;
        }

        /// <summary>Bare Grip is intentionally unavailable until a real authored/calibrated bare-hand curl is delivered.</summary>
        public bool TryReachPush(ContactHand requestedHand, HandContactTarget actualSurface,
            Func<HandContactSample, bool> tryPushAtContact)
        {
            if (ContactStage != HandContactStage.None) return RefuseDiagnostic("request.busy:" + ContactStage);
            if (StateFor(requestedHand) == null) return RefuseDiagnostic("request.invalidHand");
            selectedState = StateFor(requestedHand);
            return PreparePush(requestedHand, actualSurface, tryPushAtContact);
        }

        private bool PreparePush(ContactHand requestedHand, HandContactTarget actualSurface,
            Func<HandContactSample, bool> tryPushAtContact)
        {
            LastRefusalReason = LastRefusalDiagnostic = string.Empty;
            diagnosticPoint = diagnosticNormal = diagnosticTangent = diagnosticEyeLocal = diagnosticWrist = Vector3.zero;
            diagnosticBlend = diagnosticWristAngle = float.NaN;
            diagnosticHasTarget = diagnosticHasWrist = false;
            if (!isActiveAndEnabled) return RefuseDiagnostic("request.componentDisabled");
            if (clock == null) return RefuseDiagnostic("request.unboundClock");
            if (clock.IsPaused) return RefuseDiagnostic("request.paused");
            if (WorkingStage != HandContactStage.None) return RefuseDiagnostic("request.busy:" + WorkingStage);
            if (requestedHand != ContactHand.Left && requestedHand != ContactHand.Right) return RefuseDiagnostic("request.invalidHand");
            if (tryPushAtContact == null) return RefuseDiagnostic("request.missingAction");
            if (!leaseAllowed(requestedHand)) return RefuseDiagnostic("request.visualLeaseDenied:" + requestedHand);
            active = requestedHand == ContactHand.Left ? left : right; hand = requestedHand;
            if (!active.IsUsable || !active.Renderer.enabled || !active.Renderer.gameObject.activeInHierarchy)
            { RefuseDiagnostic("request.rigOrRendererUnavailable"); Forget(); return false; }
            poseRevision = readRevision(); target = actualSurface;
            CapturePose(baseline);
            if (!BarePalmCalibration.TryMeasure(active, out BarePalmContactPose measured)) { LastRefusalReason = "No valid anatomical palm support patch."; Forget(); return false; }
            calibration = measured;
            palmPoint = calibration.LocalPoint; palmFrame = calibration.LocalFrame;
            startWrist = active.Upper.parent.InverseTransformPoint(active.Wrist.position);
            startWristRotation = Quaternion.Inverse(active.Upper.parent.rotation) * active.Wrist.rotation;
            elbowPole = active.Upper.parent.InverseTransformVector(active.Lower.position - active.Upper.position);
            previousPoint = actualSurface.Surface ? actualSurface.Surface.TransformPoint(actualSurface.LocalPoint) : Vector3.zero;
            previousEye = eye.position;
            if (!TryTarget(out HandContactSample sample, out Vector3 point, out Quaternion rotation) || !CanReach(point)) { Forget(); return false; }
            if (!MeasureSkin(sample) || MinimumHandPlaneClearanceMetres < -PlaneTolerance)
            { LastRefusalReason = "Authored starting hand skin already crosses the target plane."; Forget(); return false; }
            overheadReach = Vector3.Dot(sample.Normal, Vector3.down) > .95f;
            if (overheadReach)
            {
                // Choose the outward bend once; do not let a camera turn flip the elbow midway.
                Vector3 middle = Vector3.Lerp(active.Wrist.position, point, .5f) + sample.Tangent * OverheadForwardArc;
                Vector3 axis = (middle - active.Upper.position).normalized;
                Vector3 pole = Vector3.ProjectOnPlane(active.Lower.position - active.Upper.position, axis);
                Vector3 outward = eye.right * (hand == ContactHand.Left ? -1f : 1f);
                overheadPoleSign = Vector3.Dot(Vector3.Cross(axis, pole), outward) >= 0f ? 1f : -1f;
            }
            lastSample = sample; lastFinalWrist = point; lastFinalRotation = rotation; lastBlend = 0f;
            action = tryPushAtContact; elapsed = 0f; WorkingStage = HandContactStage.Reaching;
            return true;
        }

        /// <summary>Acquire both real arm leases before any reach. Callback order is Left, Right in one frame.
        /// Caller must treat Left as provisional and commit an atomic domain command only on Right.</summary>
        public bool TryReachPushPair(HandContactTarget leftTarget, HandContactTarget rightTarget,
            Func<HandContactSample, bool> onContact)
        {
            if (ContactStage != HandContactStage.None || onContact == null) return false;
            selectedState = leftState;
            if (!PreparePush(ContactHand.Left, leftTarget, onContact)) return false;
            int token = generation;
            selectedState = rightState;
            if (!PreparePush(ContactHand.Right, rightTarget, onContact) || token != generation)
            { CancelImmediately(); return false; }
            pairedPush = true; pairAction = onContact;
            return true;
        }

        private void Both(Action operation)
        {
            ArmState previous = selectedState;
            try { selectedState = leftState; operation(); selectedState = rightState; operation(); }
            finally { selectedState = previous; }
        }

        private void DispatchPair()
        {
            // Re-prove both skin envelopes after both rigs have been posed. No standalone action fires.
            int token = generation;
            bool proven = true;
            Both(() =>
            {
                if (!proven || token != generation) return;
                proven = TryTarget(out HandContactSample fresh, out _, out _) && MeasureSkin(fresh)
                    && ContactErrorMetres <= .003f && PalmPatchPlaneDistanceMetres <= .003f
                    && MinimumHandPlaneClearanceMetres >= -PlaneTolerance;
                if (proven) lastSample = fresh;
            });
            if (token != generation) return;
            if (!proven) { CancelContact(); return; }
            var callback = pairAction; pairAction = null;
            bool accepted;
            try
            {
                accepted = callback != null && callback(leftState.lastSample);
                if (token != generation || !pairedPush) return;
                if (!accepted || clock == null || clock.IsPaused || readRevision == null
                    || readRevision() != leftState.poseRevision || readRevision() != rightState.poseRevision
                    || !leaseAllowed(ContactHand.Left) || !leaseAllowed(ContactHand.Right))
                { CancelContact(); return; }
                accepted = callback(rightState.lastSample);
            }
            catch (Exception error) { Debug.LogException(error, this); accepted = false; }
            if (token == generation && !accepted) CancelContact();
        }

        public Vector3 OrientGripAxis(ContactHand selected, Vector3 axis, Vector3 normal)
        {
            BareArmContactRig rig = selected == ContactHand.Left ? left : right;
            if (!GripPoseConfigured || rig == null || !rig.IsUsable) return axis;
            var pose = gripPose.For(selected);
            Quaternion inverse = Quaternion.Inverse(Quaternion.LookRotation(pose.railAxis, pose.palmNormal));
            Quaternion a = Quaternion.LookRotation(axis, -normal) * inverse;
            Quaternion b = Quaternion.LookRotation(-axis, -normal) * inverse;
            return Quaternion.Angle(rig.Wrist.rotation, b) < Quaternion.Angle(rig.Wrist.rotation, a) ? -axis : axis;
        }

        /// <summary>Starts one real bare rail grip. Physics can run only after the whole authored close and skin proof.</summary>
        public bool TryReachGrip(ContactHand requestedHand, HandContactTarget actualSurface,
            Func<HandContactSample, bool> tryGrabAtContact, Func<HandContactTarget?> heldPoint)
        {
            if (ContactStage != HandContactStage.None || StateFor(requestedHand) == null) return false;
            selectedState = StateFor(requestedHand);
            LastRefusalReason = LastRefusalDiagnostic = string.Empty;
            if (!isActiveAndEnabled || clock == null || clock.IsPaused || WorkingStage != HandContactStage.None
                || !GripPoseConfigured || tryGrabAtContact == null || heldPoint == null
                || (requestedHand != ContactHand.Left && requestedHand != ContactHand.Right)
                || !leaseAllowed(requestedHand) || Mathf.Abs(actualSurface.GripRadiusMetres - .015f) > .00001f)
                return RefuseDiagnostic("grip.requestOrPoseUnavailable");
            active = requestedHand == ContactHand.Left ? left : right; hand = requestedHand;
            if (active == null || !active.IsUsable || !active.Renderer.enabled || !active.Renderer.gameObject.activeInHierarchy)
            { RefuseDiagnostic("grip.rigUnavailable"); Forget(); return false; }
            poseRevision = readRevision(); target = actualSurface; CapturePose(baseline);
            try { grip = new BareRailGripLease(active, gripPose.For(hand)); }
            catch (ArgumentException) { RefuseDiagnostic("grip.skinOrPoseUnavailable"); Forget(); return false; }
            palmFrame = Quaternion.LookRotation(grip.Pose.railAxis, grip.Pose.palmNormal);
            palmPoint = grip.Pose.railPoint - grip.Pose.palmNormal * actualSurface.GripRadiusMetres;
            startWrist = active.Upper.parent.InverseTransformPoint(active.Wrist.position);
            startWristRotation = Quaternion.Inverse(active.Upper.parent.rotation) * active.Wrist.rotation;
            elbowPole = active.Upper.parent.InverseTransformVector(active.Lower.position - active.Upper.position);
            previousPoint = actualSurface.Surface ? actualSurface.Surface.TransformPoint(actualSurface.LocalPoint) : Vector3.zero;
            previousEye = eye.position; overheadReach = false;
            if (!TryTarget(out HandContactSample sample, out Vector3 point, out Quaternion rotation)
                || !grip.Measure(sample) || grip.MinimumClearance < -PlaneTolerance)
            { if (string.IsNullOrEmpty(LastRefusalReason)) RefuseDiagnostic("grip.startingSkinIntersectsRail"); Forget(); return false; }
            // A cylinder has real rotational freedom around its axis. Pick one bounded anatomical
            // endpoint now; never change that choice during reach/hold/return or dispatch physics here.
            if (!SelectGripAxialPose() || !TryTarget(out sample, out point, out rotation)) { Forget(); return false; }
            lastSample = sample; lastFinalWrist = point; lastFinalRotation = rotation;
            action = tryGrabAtContact; readHeldGrip = heldPoint; gripClose = lastBlend = elapsed = 0f;
            WorkingStage = HandContactStage.Reaching; return true;
        }

        bool SelectGripAxialPose()
        {
            bool found = false; float bestAngle = 0f, bestWrist = float.PositiveInfinity;
            int token = generation;
            try
            {
                // Same actual centre and authored fingers; finite search only chooses cylinder roll.
                // Every candidate uses original reach/elbow/wrist/path and complete native skin gates.
                for (int degrees = -90; degrees <= 90; degrees += 15)
                {
                    Restore(baseline, 1f); grip.Restore(); gripAxialDegrees = degrees;
                    if (!TryTarget(out HandContactSample sample, out Vector3 wrist, out Quaternion rotation))
                    { if (generation != token || grip == null) return false; continue; }
                    diagnosticBlend = 1f;
                    grip.Apply(1f, 1f);
                    if (!SolveArm(wrist, rotation, 1f) || !grip.Measure(sample)
                        || grip.MinimumClearance < -PlaneTolerance || grip.MaximumRegionGap > .002f
                        || grip.AxisError > .003f || grip.AxisAlignment < .999f) continue;
                    float bend = Quaternion.Angle(active.Wrist.localRotation, baseline[2]);
                    if (bend < bestWrist - .01f || (Mathf.Abs(bend - bestWrist) <= .01f && Mathf.Abs(degrees) < Mathf.Abs(bestAngle)))
                    { found = true; bestWrist = bend; bestAngle = degrees; }
                }
            }
            finally { Restore(baseline, 1f); grip?.Restore(); }
            if (generation != token || grip == null) return false;
            if (!found) return RefuseDiagnostic("grip.noFeasibleAxialPose");
            gripAxialDegrees = bestAngle; diagnosticBlend = 0f;
            LastRefusalReason = LastRefusalDiagnostic = string.Empty;
            return true;
        }

        void TickGrip()
        {
            if (WorkingStage == HandContactStage.Holding)
            {
                int token = generation; HandContactTarget? held;
                try { held = readHeldGrip?.Invoke(); }
                catch (Exception error) { Debug.LogException(error, this); held = null; }
                if (token != generation || grip == null) return;
                if (!held.HasValue || held.Value.Surface != target.Surface) { CancelContact(); return; }
                target = held.Value;
            }
            if (!TryTarget(out HandContactSample sample, out Vector3 wrist, out Quaternion rotation))
            { CancelImmediately(); return; }
            float arm, close;
            if (WorkingStage == HandContactStage.Returning)
            {
                float openingSeconds = gripReturnClose * BareRailGripPose.Duration;
                close = openingSeconds > 0f ? gripReturnClose * (1f - Mathf.Clamp01(elapsed / openingSeconds)) : 0f;
                float retract = Mathf.Clamp01((elapsed - openingSeconds) / Mathf.Clamp(returnSeconds, .15f, .35f));
                arm = returnFromBlend * (1f - Mathf.SmoothStep(0f, 1f, retract));
                if (!TryApplyGripPose(arm, close, wrist, rotation, sample)) { CancelImmediately(); return; }
                gripClose = close;
                if (retract >= 1f) { Restore(baseline, 1f); grip.Restore(); Forget(); }
                return;
            }
            arm = WorkingStage == HandContactStage.Holding ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / reachSeconds));
            close = WorkingStage == HandContactStage.Holding ? 1f : Mathf.Clamp01((elapsed - reachSeconds) / BareRailGripPose.Duration);
            if (!TryApplyGripPose(arm, close, wrist, rotation, sample)) { CancelImmediately(); return; }
            gripClose = close; lastBlend = arm; lastSample = sample; lastFinalWrist = wrist; lastFinalRotation = rotation;
            if (close < 1f) return;
            if (ContactErrorMetres > .003f || grip.MaximumRegionGap > .002f
                || grip.MinimumClearance < -PlaneTolerance || grip.AxisAlignment < .999f)
            { RefuseDiagnostic("grip.closedSkinOrAxisNotProven"); CancelContact(); return; }
            if (WorkingStage == HandContactStage.Holding) return;
            var callback = action; action = null; int generationBefore = generation;
            WorkingStage = HandContactStage.Holding; elapsed = 0f;
            bool accepted;
            try { accepted = callback != null && callback(sample); }
            catch (Exception error) { Debug.LogException(error, this); accepted = false; }
            if (generationBefore != generation) return;
            if (!accepted) CancelContact();
        }

        bool TryApplyGripPose(float arm, float close, Vector3 finalWrist, Quaternion finalRotation, HandContactSample sample)
        {
            diagnosticBlend = arm;
            CapturePose(previousPose); grip.CapturePrevious();
            Vector3 current = active.Wrist.position;
            Vector3 wrist = Vector3.Lerp(active.Upper.parent.TransformPoint(startWrist), finalWrist, arm);
            Quaternion rotation = Quaternion.Slerp(active.Upper.parent.rotation * startWristRotation, finalRotation, arm);
            var path = new HandContactSample(hand, HandContactKind.Grip, target, sample.Point, sample.Normal, sample.Tangent, current, wrist);
            int token = generation; bool valid;
            try { valid = validate(path); }
            catch (Exception error) { Debug.LogException(error, this); valid = false; }
            if (token != generation || grip == null) return false;
            if (!valid || readRevision() != poseRevision) return RefuseDiagnostic("grip.authorityOrWristPathRejected");
            Restore(baseline, 1f); grip.Apply(arm, close);
            if (!SolveArm(wrist, rotation, arm) || !grip.Measure(sample) || grip.MinimumClearance < -PlaneTolerance)
            {
                Restore(previousPose, 0f); grip.RestorePrevious();
                if (string.IsNullOrEmpty(LastRefusalReason)) RefuseDiagnostic("grip.actualSkinIntersectsRail");
                return false;
            }
            ContactErrorMetres = grip.AxisError;
            MinimumHandPlaneClearanceMetres = grip.MinimumClearance;
            PalmPatchPlaneDistanceMetres = grip.MaximumRegionGap;
            return true;
        }

        /// <summary>Synchronous lease return before the body owner samples, hides or restores its own skeleton.</summary>
        public void CancelImmediately()
        {
            pairAction = null;
            Both(CancelOneImmediately);
            pairedPush = false;
        }

        private void CancelOneImmediately()
        {
            if (active != null && WorkingStage != HandContactStage.None && readRevision != null && readRevision() == poseRevision)
            { Restore(baseline, 1f); grip?.Restore(); }
            Forget();
        }

        public void CancelContact()
        {
            pairAction = null;
            Both(CancelOneContact);
        }

        private void CancelOneContact()
        {
            if (active == null || WorkingStage == HandContactStage.None || WorkingStage == HandContactStage.Returning) return;
            returnFromBlend = lastBlend; gripReturnClose = gripClose;
            generation++; action = null; elapsed = 0f;
            ContactErrorMetres = float.NaN; WorkingStage = HandContactStage.Returning;
        }

        public void ClearBinding()
        {
            CancelImmediately(); left = right = null; eye = null; clock = null;
            leaseAllowed = null; readRevision = null; validate = null;
        }
        private void OnDisable() => CancelImmediately();
        private void OnDestroy() => ClearBinding();
        private void Forget()
        {
            calibration?.Dispose(); calibration = null;
            grip?.Dispose(); grip = null; readHeldGrip = null; gripClose = gripReturnClose = gripAxialDegrees = 0f;
            generation++; active = null; action = null; target = default;
            WorkingStage = HandContactStage.None; elapsed = 0f; ContactErrorMetres = float.NaN; overheadReach = false; overheadPoleSign = 0f;
            MinimumHandPlaneClearanceMetres = PalmPatchPlaneDistanceMetres = float.NaN;
            MeasuredWaypointCorrectionMetres = 0f; SafeSolveAttempts = 0; lastBlend = returnFromBlend = 0f;
        }

        private void LateUpdate()
        {
            if (!pairedPush) { TickContact(); return; }
            Both(TickContact);
            if (ContactStage == HandContactStage.None) { pairedPush = false; pairAction = null; return; }
            if (pairAction == null) return;
            if (leftState.WorkingStage == HandContactStage.Returning || rightState.WorkingStage == HandContactStage.Returning
                || leftState.WorkingStage == HandContactStage.None || rightState.WorkingStage == HandContactStage.None)
            { CancelContact(); return; }
            if (leftState.WorkingStage != HandContactStage.PushContact || rightState.WorkingStage != HandContactStage.PushContact) return;
            DispatchPair();
        }

        private void TickContact()
        {
            if (WorkingStage == HandContactStage.None) return;
            if (active == null || !active.IsUsable || readRevision() != poseRevision) { Forget(); return; }
            if (clock.IsPaused) { CancelContact(); return; } // Freeze visible pose; no queued action survives resume.
            if (!leaseAllowed(hand) || !active.Renderer.enabled || !active.Renderer.gameObject.activeInHierarchy)
            { CancelImmediately(); return; }
            float dt = Time.unscaledDeltaTime;
            if (!Finite(dt) || dt < 0f) { CancelContact(); return; }
            // Slow rendering alone must neither abandon a valid reach nor fast-forward its action.
            // Revalidate the real target/lease below while holding the animation at its prior time.
            if (dt > .1f) dt = 0f;
            elapsed += dt;
            if (grip != null) { TickGrip(); return; }
            if (WorkingStage == HandContactStage.Returning)
            {
                float t = Mathf.Clamp01(elapsed / Mathf.Clamp(returnSeconds, .15f, .35f));
                // Reverse the same measured path; direct quaternion return can sweep fingers through a wall.
                float reverse = returnFromBlend * (1f - Mathf.SmoothStep(0f, 1f, t));
                if (!TryApplySafePose(reverse, lastFinalWrist, lastFinalRotation, lastSample))
                { CancelImmediately(); return; }
                if (t >= 1f) { Restore(baseline, 1f); Forget(); }
                return;
            }
            if (!TryTarget(out HandContactSample sample, out Vector3 finalWrist, out Quaternion finalRotation))
            { CancelContact(); return; }
            float reach = Mathf.Clamp01(elapsed / Mathf.Clamp(reachSeconds, .15f, .25f));
            float blend = WorkingStage == HandContactStage.Reaching ? Mathf.SmoothStep(0f, 1f, reach) : 1f;
            if (!TryApplySafePose(blend, finalWrist, finalRotation, sample)) { CancelContact(); return; }
            lastSample = sample; lastFinalWrist = finalWrist; lastFinalRotation = finalRotation; lastBlend = blend;
            if (WorkingStage == HandContactStage.PushContact)
            { if (elapsed >= .06f) CancelContact(); return; }
            if (reach < 1f) return;
            if (ContactErrorMetres > .003f || PalmPatchPlaneDistanceMetres > .003f
                || MinimumHandPlaneClearanceMetres < -PlaneTolerance) { CancelContact(); return; }
            var callback = action; action = null;
            WorkingStage = HandContactStage.PushContact; elapsed = 0f;
            if (pairedPush) return;
            int token = generation;
            bool accepted;
            try { accepted = callback != null && callback(sample); }
            catch (Exception error) { Debug.LogException(error, this); accepted = false; }
            if (token != generation) return;
            if (!accepted) CancelContact();
        }

        private void Restore(Quaternion[] from, float blend)
        {
            if (active == null) return;
            if (active.Upper) active.Upper.localRotation = Quaternion.Slerp(from[0], baseline[0], blend);
            if (active.Lower) active.Lower.localRotation = Quaternion.Slerp(from[1], baseline[1], blend);
            if (active.Wrist) active.Wrist.localRotation = Quaternion.Slerp(from[2], baseline[2], blend);
            for (int digit = 0; digit < 5; digit++) for (int joint = 0; joint < 2; joint++)
            {
                int index = 3 + digit * 2 + joint;
                Transform bone = active.Fingers[digit * 3 + joint];
                if (bone) bone.localRotation = Quaternion.Slerp(from[index], baseline[index], blend);
            }
        }

        private void CapturePose(Quaternion[] destination)
        {
            destination[0] = active.Upper.localRotation; destination[1] = active.Lower.localRotation; destination[2] = active.Wrist.localRotation;
            for (int digit = 0; digit < 5; digit++) for (int joint = 0; joint < 2; joint++)
                destination[3 + digit * 2 + joint] = active.Fingers[digit * 3 + joint].localRotation;
        }

        private bool MeasureSkin(HandContactSample sample)
        {
            if (calibration == null || !calibration.Measure(sample.Point, sample.Normal, out float clearance, out float error, out float patch)) return false;
            MinimumHandPlaneClearanceMetres = clearance; ContactErrorMetres = error; PalmPatchPlaneDistanceMetres = patch;
            return true;
        }

        private bool TryApplySafePose(float blend, Vector3 finalWrist, Quaternion finalRotation, HandContactSample sample)
        {
            diagnosticBlend = blend;
            if (active == null || calibration == null) return RefuseDiagnostic("solve.missingActiveRigOrCalibration");
            CapturePose(previousPose);
            Vector3 currentWrist = active.Wrist.position;
            Vector3 desired = Vector3.Lerp(active.Upper.parent.TransformPoint(startWrist), finalWrist, blend);
            // A real wrist path around the chest, with unchanged start/end/contact point. Every waypoint
            // still proves reach, scene clearance and the complete current skinned-hand plane envelope.
            if (overheadReach) desired += sample.Tangent * (OverheadForwardArc * Mathf.Sin(Mathf.PI * blend));
            Quaternion rotation = Quaternion.Slerp(active.Upper.parent.rotation * startWristRotation, finalRotation, blend);
            float correction = 0f; int token = generation;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                Vector3 waypoint = desired + sample.Normal * correction;
                var path = new HandContactSample(hand, HandContactKind.Push, target, sample.Point, sample.Normal,
                    sample.Tangent, currentWrist, waypoint);
                bool clear;
                try { clear = validate != null && validate(path); }
                catch (Exception error) { Debug.LogException(error, this); clear = false; }
                if (token != generation || active == null || readRevision() != poseRevision) return false;
                if (!clear) { RefuseDiagnostic("waypoint.authorityOrWristPathRejected"); break; }
                if (!CanReach(waypoint)) break;
                Restore(baseline, 1f);
                for (int digit = 0; digit < 5; digit++) for (int joint = 0; joint < 2; joint++)
                {
                    int index = digit * 2 + joint;
                    active.Fingers[digit * 3 + joint].localRotation = Quaternion.Slerp(baseline[3 + index], calibration.OpenFingers[index], blend);
                }
                if (!SolveArm(waypoint, rotation, blend)) break;
                if (!MeasureSkin(sample)) { RefuseDiagnostic("solve.skinMeasurementFailed"); break; }
                SafeSolveAttempts = attempt + 1; MeasuredWaypointCorrectionMetres = correction;
                if (MinimumHandPlaneClearanceMetres >= -PlaneTolerance) return true;
                // Only the measured intermediate skin violation moves this waypoint. The actual palm
                // support point remains the endpoint; large endpoint stand-off cannot pass its residual gate.
                correction += -MinimumHandPlaneClearanceMetres + PlaneTolerance;
                if (correction > .06f) { LastRefusalReason = "Required plane avoidance exceeds the 60mm presentation bound."; break; }
            }
            if (active != null && readRevision() == poseRevision)
            {
                Restore(previousPose, 0f);
                MeasureSkin(sample); // Diagnostics describe the retained visible pose, not a rejected trial.
            }
            return false;
        }

        private bool TryTarget(out HandContactSample sample, out Vector3 wrist, out Quaternion rotation)
        {
            sample = default; wrist = default; rotation = Quaternion.identity;
            Transform surface = target.Surface;
            if (!surface || !surface.gameObject.activeInHierarchy || !eye) return RefuseDiagnostic("target.surfaceOrEyeUnavailable");
            Vector3 p = surface.TransformPoint(target.LocalPoint);
            Vector3 n = surface.localToWorldMatrix.inverse.transpose.MultiplyVector(target.LocalNormal).normalized;
            Vector3 tangent = Vector3.ProjectOnPlane(surface.TransformVector(target.LocalTangent), n).normalized;
            Vector3 local = Quaternion.Inverse(eye.rotation) * (p - eye.position);
            float side = hand == ContactHand.Left ? -1f : 1f;
            diagnosticPoint = p; diagnosticNormal = n; diagnosticTangent = tangent; diagnosticEyeLocal = local; diagnosticHasTarget = true;
            if (!FiniteVector(p) || !FiniteVector(n) || !FiniteVector(tangent)) return RefuseDiagnostic("target.nonFiniteFrame");
            if (n.sqrMagnitude < .99f || tangent.sqrMagnitude < .99f) return RefuseDiagnostic("target.degenerateFrame");
            if (local.magnitude > .8f) return RefuseDiagnostic("target.eyeDistanceOver0.8");
            if (local.z < .10f) return RefuseDiagnostic("target.eyeForwardUnder0.10");
            if (local.x * side < -.15f) return RefuseDiagnostic("target.crossBodySideOver0.15");
            if (Vector3.Dot(n, (eye.position - p).normalized) < .15f) return RefuseDiagnostic("target.facingDotUnder0.15");
            if (Vector3.Distance(previousPoint, p) > .12f) return RefuseDiagnostic("target.surfaceMovedOver0.12");
            if (Vector3.Distance(previousEye, eye.position) > .25f) return RefuseDiagnostic("target.eyeMovedOver0.25");
            rotation = Quaternion.LookRotation(tangent, -n) * Quaternion.Inverse(palmFrame);
            if (grip != null)
            {
                rotation = Quaternion.AngleAxis(gripAxialDegrees, tangent) * rotation;
                // The centre stays on the REAL cylinder axis as the hand rotates around it. The
                // near-face ray normal remains unchanged; it is not an artificial palm-plane normal.
                wrist = p - n * target.GripRadiusMetres - rotation * grip.Pose.railPoint;
            }
            else wrist = p - rotation * palmPoint;
            if (!CanReach(wrist)) return false;
            sample = new HandContactSample(hand, grip != null ? HandContactKind.Grip : HandContactKind.Push, target, p, n, tangent, active.Wrist.position, wrist);
            int token = generation;
            try
            {
                if (!validate(sample)) return RefuseDiagnostic("target.authorityOrWristPathRejected");
                if (token != generation) return RefuseDiagnostic("target.generationChanged");
                if (readRevision() != poseRevision) return RefuseDiagnostic("target.bodyPoseChanged");
                previousPoint = p; previousEye = eye.position; return true;
            }
            catch (Exception error) { Debug.LogException(error, this); return false; }
        }

        private bool CanReach(Vector3 wrist)
        {
            float a = Vector3.Distance(active.Upper.position, active.Lower.position);
            float b = Vector3.Distance(active.Lower.position, active.Wrist.position);
            float d = Vector3.Distance(active.Upper.position, wrist);
            diagnosticWrist = wrist; diagnosticHasWrist = true;
            if (a < .15f || b < .15f || a > .4f || b > .4f || !Finite(d) || d <= .001f)
                return RefuseDiagnostic("reach.segmentLengthOrDistanceInvalid");
            float cosine = (a * a + b * b - d * d) / (2f * a * b);
            // Interior elbow angle 25â€“175 degrees: no stretching, folded-through arm or locked straight elbow.
            bool reachable = cosine >= Mathf.Cos(175f * Mathf.Deg2Rad) && cosine <= Mathf.Cos(25f * Mathf.Deg2Rad);
            return reachable || RefuseDiagnostic("reach.elbowOutside25to175");
        }

        private bool SolveArm(Vector3 wrist, Quaternion rotation, float blend)
        {
            if (!CanReach(wrist)) return false;
            Vector3 shoulder = active.Upper.position, direction = (wrist - shoulder).normalized;
            float a = Vector3.Distance(shoulder, active.Lower.position);
            float b = Vector3.Distance(active.Lower.position, active.Wrist.position), d = Vector3.Distance(shoulder, wrist);
            Vector3 pole = Vector3.ProjectOnPlane(active.Upper.parent.TransformVector(elbowPole), direction).normalized;
            if (pole.sqrMagnitude < .99f) return RefuseDiagnostic("solve.degenerateElbowPole");
            if (overheadReach) pole = Quaternion.AngleAxis(overheadPoleSign * 180f * blend, direction) * pole;
            float along = (a * a - b * b + d * d) / (2f * d);
            Vector3 elbow = shoulder + direction * along + pole * Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
            active.Upper.rotation = Quaternion.FromToRotation(active.Lower.position - shoulder, elbow - shoulder) * active.Upper.rotation;
            active.Lower.rotation = Quaternion.FromToRotation(active.Wrist.position - active.Lower.position,
                wrist - active.Lower.position) * active.Lower.rotation;
            Vector3 axis = (wrist - active.Lower.position).normalized;
            Vector3 currentNormal = Vector3.ProjectOnPlane(active.Wrist.rotation * (palmFrame * Vector3.up), axis);
            Vector3 desiredNormal = Vector3.ProjectOnPlane(rotation * (palmFrame * Vector3.up), axis);
            if (overheadReach)
            {
                // Continuous bounded pronation avoids the +/-180 SignedAngle branch flipping a
                // clamped forearm by150 degrees near full reach. The old wall solve stays unchanged.
                active.Lower.rotation = Quaternion.AngleAxis(-overheadPoleSign * 75f * blend, axis) * active.Lower.rotation;
            }
            else if (currentNormal.sqrMagnitude > .01f && desiredNormal.sqrMagnitude > .01f)
            {
                float roll = Mathf.Clamp(Vector3.SignedAngle(currentNormal, desiredNormal, axis), -75f, 75f);
                active.Lower.rotation = Quaternion.AngleAxis(roll, axis) * active.Lower.rotation;
            }
            Quaternion wristLocal = Quaternion.Inverse(active.Wrist.parent.rotation) * rotation;
            // Keep articulation local while lifting: an independent world-space wrist slerp over-twists
            // the folded arm. At full contact this is the exact original required palm orientation.
            if (overheadReach) wristLocal = Quaternion.Slerp(baseline[2], wristLocal, Mathf.Pow(blend, 6f));
            diagnosticWristAngle = Quaternion.Angle(wristLocal, baseline[2]);
            if (diagnosticWristAngle > 80f) { RefuseDiagnostic("solve.wristOver80"); Restore(baseline, 1f); return false; }
            active.Wrist.localRotation = wristLocal;
            return Vector3.Distance(active.Wrist.position, wrist) <= .002f || RefuseDiagnostic("solve.wristResidualOver0.002");
        }

        // Diagnostics only; retained through Forget/Cancel, never consulted by an acceptance gate.
        public string LastRefusalDiagnostic { get => Current.LastRefusalDiagnostic; private set => Current.LastRefusalDiagnostic = value; }
        private bool RefuseDiagnostic(string reason)
        {
            LastRefusalReason = reason;
            float a = active != null && active.Upper && active.Lower ? Vector3.Distance(active.Upper.position, active.Lower.position) : float.NaN;
            float b = active != null && active.Lower && active.Wrist ? Vector3.Distance(active.Lower.position, active.Wrist.position) : float.NaN;
            float d = active != null && active.Upper ? Vector3.Distance(active.Upper.position, diagnosticWrist) : float.NaN;
            LastRefusalDiagnostic = reason + " hand=" + hand + " stage=" + WorkingStage
                + " hasTarget=" + diagnosticHasTarget + " hasWrist=" + diagnosticHasWrist
                + " blend=" + diagnosticBlend.ToString("R") + " a=" + a.ToString("R") + " b=" + b.ToString("R") + " d=" + d.ToString("R")
                + " elbowCos=" + ((a*a+b*b-d*d)/(2f*a*b)).ToString("R") + " wristAngle=" + diagnosticWristAngle.ToString("R")
                + " eye=" + (eye ? eye.position.ToString("R") : "null") + " point=" + diagnosticPoint.ToString("R")
                + " eyeLocal=" + diagnosticEyeLocal.ToString("R") + " normal=" + diagnosticNormal.ToString("R") + " tangent=" + diagnosticTangent.ToString("R")
                + " desiredWrist=" + diagnosticWrist.ToString("R")
                + " shoulder=" + (active != null && active.Upper ? active.Upper.position.ToString("R") : "null")
                + " elbow=" + (active != null && active.Lower ? active.Lower.position.ToString("R") : "null")
                + " wrist=" + (active != null && active.Wrist ? active.Wrist.position.ToString("R") : "null")
                + " skin=" + MinimumHandPlaneClearanceMetres.ToString("R") + " support=" + ContactSupportVertexIndex
                + " skinVertices=" + ContactHandVertexCount + " patchVertices=" + ContactPatchVertexCount;
            return false;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool FiniteVector(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    }
}
