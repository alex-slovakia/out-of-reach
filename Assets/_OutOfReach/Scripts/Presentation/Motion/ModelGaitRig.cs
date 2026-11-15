using System;
using System.Collections.Generic;
using UnityEngine;

namespace OutOfReach.Presentation.Motion
{
    /// <summary>Ground query for foot placement: a ray straight down from <paramref name="origin"/>.</summary>
    internal delegate bool ModelGaitGroundProbe(Vector3 origin, float distance, out float height, out Vector3 normal);

    /// <summary>Per-frame input to the grounded body rig.</summary>
    internal struct ModelGaitFrame
    {
        public Vector3 Direction;   // unit horizontal direction of travel in the reference (yaw-only visual root) frame
        public float Phase;         // LEFT foot cycle phase, 0 = heel strike
        public float Speed;         // metres per second of the controller
        public float Weight;        // 0 standing .. 1 full gait: pelvis, arms, foot roll and trunk motion
        public bool Moving;         // steps are timed by the gait cycle; otherwise the feet stay planted and step to settle
        public Vector3 Velocity;    // world horizontal velocity of the body
        public float YawRate;       // degrees per second of the reference
        public Vector3 Pivot;       // world point the reference turns about (the player's feet)
        public float DeltaTime;     // 0 re-poses without advancing any step
        public float Setback;       // pelvis setback (metres, backwards) for looking down
        public bool LeftArm, RightArm, Eva;
        public ModelGaitGroundProbe Ground;
    }

    /// <summary>
    /// World-anchored feet for the shared 164-bone rig. A planted foot is locked in the world until the gait cycle
    /// (walking) or a settle/turn step (standing) lifts it, so it never skates when the body turns, curves, slows or
    /// stops. Landings are predicted from the body velocity and turn rate and placed on the real ground. Pelvis sway,
    /// rotation and list, trunk lean and counter-rotation, opposing arm swing and the suit profile sit on top.
    /// Only bone transforms are written; the caller restores them through its own model cache.
    /// </summary>
    internal sealed class ModelGaitRig
    {
        private const float IdleStepLift = .055f, StepDistance = .11f, StepYaw = 28f;
        private readonly Transform hips, spineLow, spineHigh;
        private readonly Leg left, right;
        private Arm leftArm, rightArm;
        private bool armsPosed, gaitActive, squaring, turning;
        private Vector3 standingHipsPosition;
        private Quaternion standingHipsRotation, spineLowRotation, spineHighRotation;

        public float LegLength { get; private set; } = ModelGait.ReferenceLegLength;
        /// <summary>Pelvis crouch (share of leg length) the gait may need to reach a planted foot.</summary>
        internal const float PelvisDropBudget = .065f;
        /// <summary>Hard limit of the pelvis crouch (share of leg length).</summary>
        internal const float MaxPelvisDrop = .11f;
        /// <summary>Limit while standing: turning on the spot with a steep look-down needs a deeper knee bend.</summary>
        internal const float MaxStandingPelvisDrop = .17f;
        /// <summary>Largest fore/aft ankle offset from the hip a planted foot may need (measured on the standing rig).</summary>
        public float MaxHalfSpan { get; private set; } = .3f;
        internal bool HasTorso => spineLow != null;
        internal bool IsStepping => left.Stepping || right.Stepping;
        internal bool IsPlanted(bool leftFoot) => (leftFoot ? left : right).Planted;
        internal float ShownPitch(bool leftFoot) => (leftFoot ? left : right).ShownPitch;
        internal float SlotDistance(bool leftFoot) => (leftFoot ? left : right).SlotDistance;
        internal float YawOffset(bool leftFoot) => (leftFoot ? left : right).YawOffset;

        /// <summary>
        /// True when the LEFT foot is the one further behind its standing slot along a world travel direction: after a
        /// sharp change of direction that foot lifts first.
        /// </summary>
        internal bool LeftTrails(Transform reference, Vector3 worldDirection, bool eva)
        {
            if (!left.Valid || !right.Valid) return true;
            ModelGaitProfile profile = eva ? ModelGaitProfile.Eva : ModelGaitProfile.Work;
            float l = Vector3.Dot(left.Shown - reference.TransformPoint(left.StandingLocal(profile)), worldDirection);
            float r = Vector3.Dot(right.Shown - reference.TransformPoint(right.StandingLocal(profile)), worldDirection);
            return l <= r;
        }

        public ModelGaitRig(Dictionary<string, Transform> nodes)
        {
            hips = Find(nodes, "root");
            left = new Leg(nodes, "L", -1f);
            right = new Leg(nodes, "R", 1f);
            if (!left.Thigh.IsChildOf(hips) || !right.Thigh.IsChildOf(hips))
                throw new InvalidOperationException("The body's visual hips must own both leg chains.");
            // Optional trunk joints, used only in the expected parent order of the shared rig.
            nodes.TryGetValue("spine05", out Transform low);
            nodes.TryGetValue("spine02", out Transform high);
            if (low && low.parent == hips && high && high != low && high.IsChildOf(low)) { spineLow = low; spineHigh = high; }
        }

        public void PrepareArms(Dictionary<string, Transform> nodes)
        {
            if (leftArm != null) return;
            leftArm = new Arm(nodes, "L");
            rightArm = new Arm(nodes, "R");
        }

        public void CaptureStanding(Transform reference)
        {
            standingHipsPosition = hips.localPosition;
            standingHipsRotation = hips.localRotation;
            if (spineLow) { spineLowRotation = spineLow.localRotation; spineHighRotation = spineHigh.localRotation; }
            left.CaptureStanding(reference);
            right.CaptureStanding(reference);
            LegLength = Mathf.Clamp((left.Length + right.Length) * .5f, .55f, 1.3f);
            // R03: how far a planted ankle may sit ahead of / behind the hip with at most PelvisDropBudget of crouch.
            float budget = PelvisDropBudget * LegLength;
            MaxHalfSpan = Mathf.Clamp(Mathf.Min(left.HalfSpan(budget), right.HalfSpan(budget)), .12f * LegLength, .45f * LegLength);
            leftArm?.CaptureStanding();
            rightArm?.CaptureStanding();
            armsPosed = false;
        }

        public void RestoreStanding()
        {
            hips.localPosition = standingHipsPosition;
            hips.localRotation = standingHipsRotation;
            if (spineLow) { spineLow.localRotation = spineLowRotation; spineHigh.localRotation = spineHighRotation; }
            left.RestoreStanding();
            right.RestoreStanding();
            if (armsPosed)
            {
                leftArm.RestoreStanding();
                rightArm.RestoreStanding();
                armsPosed = false;
            }
        }

        /// <summary>Forget the planted feet (teleport, pose or mode change): the next pose starts at the standing slots.</summary>
        public void ResetFeet()
        {
            left.Valid = right.Valid = false;
            gaitActive = squaring = turning = false;
        }

        /// <summary>Cycle phase for the first step: the planted foot takes mid-stance, the other starts mid-swing.</summary>
        public float StartPhase(float stance)
        {
            bool leftInAir = left.Valid && !left.Planted;
            return Mathf.Repeat(stance * .5f + (leftInAir ? .5f : 0f), 1f);
        }

        public void Apply(Transform reference, in ModelGaitFrame f)
        {
            RestoreStanding();
            ModelGaitProfile profile = f.Eva ? ModelGaitProfile.Eva : ModelGaitProfile.Work;
            float w = Mathf.SmoothStep(0f, 1f, f.Weight);
            float legLength = LegLength;
            float speed = Mathf.Max(f.Speed, .3f);
            float scale = legLength / ModelGait.ReferenceLegLength;
            float stride = ModelGait.GaitStride(speed, legLength, f.Direction, MaxHalfSpan, profile.StrideScale);
            float stance = ModelGait.GaitStance(speed, legLength, f.Direction, MaxHalfSpan, profile.StrideScale);
            // Where a foot lands ahead of its standing slot: half the stance travel, but never further than the leg
            // reaches (the pelvis sits back by the look-down setback, which takes that much off the forward reach).
            float lead = Mathf.Min(stride * stance * .5f, Mathf.Max(.05f, MaxHalfSpan - f.Setback * Mathf.Max(0f, f.Direction.z)));
            float back = ModelGait.Backward01(f.Direction);
            // Backwards: lower swing and a toe-first landing (the heel-strike roll is mirrored and damped).
            float lift = ModelGait.Lift(speed, legLength) * profile.LiftScale * Mathf.Lerp(1f, .7f, back);
            float pitchScale = Mathf.Lerp(1f, -.45f, back);
            float leftPhase = Mathf.Repeat(f.Phase, 1f), rightPhase = Mathf.Repeat(f.Phase + .5f, 1f);
            float baseY = reference.position.y;
            float referenceYaw = reference.eulerAngles.y;

            Vector3 leftSlot = Snap(reference.TransformPoint(left.StandingLocal(profile)), f.Ground, baseY, out Vector3 leftSlotNormal);
            Vector3 rightSlot = Snap(reference.TransformPoint(right.StandingLocal(profile)), f.Ground, baseY, out Vector3 rightSlotNormal);
            if (!left.Valid || !right.Valid)
            {
                left.Reset(leftSlot, referenceYaw, leftSlotNormal);
                right.Reset(rightSlot, referenceYaw, rightSlotNormal);
                gaitActive = squaring = false;
            }

            Vector3 leftFlat, rightFlat, leftNormal, rightNormal;
            float leftYaw, rightYaw, leftPitch, rightPitch;
            if (f.Moving)
            {
                if (!gaitActive)
                {
                    // Each foot re-bases its first swing from where it is now (a settle step may be in the air).
                    gaitActive = true; squaring = false;
                    left.Stepping = right.Stepping = false;
                }
                GaitFoot(left, reference, f, profile, leftPhase, stride, stance, lead, lift, speed, referenceYaw, baseY, leftSlot,
                    out leftFlat, out leftYaw, out leftNormal, out leftPitch);
                GaitFoot(right, reference, f, profile, rightPhase, stride, stance, lead, lift, speed, referenceYaw, baseY, rightSlot,
                    out rightFlat, out rightYaw, out rightNormal, out rightPitch);
                leftPitch *= w * pitchScale; rightPitch *= w * pitchScale;
            }
            else
            {
                if (gaitActive)
                {
                    // Stop: planted feet stay put, a foot in the air finishes as a settle step; then square up.
                    gaitActive = false; squaring = true;
                    left.EndGait(); right.EndGait();
                }
                IdleFoot(left, f.DeltaTime, leftSlot, leftSlotNormal, referenceYaw, out leftFlat, out leftYaw, out leftNormal, out leftPitch);
                IdleFoot(right, f.DeltaTime, rightSlot, rightSlotNormal, referenceYaw, out rightFlat, out rightYaw, out rightNormal, out rightPitch);
                // When a turn ends, bring the feet together like a person does after turning round.
                float turnRate = Mathf.Abs(f.YawRate);
                if (turnRate > 30f) turning = true;
                else if (turning && turnRate < 10f) { turning = false; squaring = true; }
                ChooseIdleStep(f.DeltaTime, leftSlot, rightSlot, referenceYaw, turnRate);
            }
            left.Show(leftFlat, leftYaw, leftNormal, leftPitch, leftSlot, referenceYaw);
            right.Show(rightFlat, rightYaw, rightNormal, rightPitch, rightSlot, referenceYaw);
            Vector3 leftTarget = left.AnkleTarget(reference), rightTarget = right.AnkleTarget(reference);

            float sway = ModelGait.PelvisSway(leftPhase, legLength, speed) * w * profile.SwayScale;
            hips.position += reference.TransformDirection(new Vector3(sway, 0f, -f.Setback));
            if (spineLow && w > 0f)
            {
                float gate = w * Mathf.Clamp01(speed / 1.2f);
                float yaw = ModelGait.PelvisYaw(leftPhase, profile.PelvisYaw) * gate;
                float roll = ModelGait.PelvisRoll(leftPhase, profile.PelvisRoll) * gate;
                Quaternion pelvis = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(roll, reference.forward);
                hips.rotation = pelvis * hips.rotation;
                // The trunk keeps its heading (undo the pelvis at the first spine joint), leans with speed and the
                // shoulders counter-rotate a little against the pelvis.
                float lean = (ModelGait.ForwardLean(speed) + profile.Lean) * w;
                spineLow.rotation = Quaternion.AngleAxis(lean, reference.right) * Quaternion.Inverse(pelvis) * spineLow.rotation;
                spineHigh.rotation = Quaternion.AngleAxis(-.6f * yaw, Vector3.up) * spineHigh.rotation;
            }

            float blend = .02f * scale * Mathf.Max(w, .3f);
            // R03: only a foot on the ground pulls the pelvis down. A foot in the air fades out after lift-off and back in
            // before it lands, so the pelvis is highest in the flight of a jog instead of squatting to the swinging leg.
            float drop = SmoothHigher(.022f * scale * w, SmoothHigher(left.DropToReach(leftTarget) * left.ReachWeight,
                right.DropToReach(rightTarget) * right.ReachWeight, blend), .02f * scale * w);
            // R03: a bounded crouch. A foot left far away (a turn, a stop, a hitch) is stepped home; the body never squats to it.
            hips.position -= Vector3.up * Mathf.Min(Mathf.Lerp(MaxStandingPelvisDrop, MaxPelvisDrop, w) * legLength, drop);
            left.Pose(reference, leftTarget, w);
            right.Pose(reference, rightTarget, w);

            if (w > 0f && ((f.LeftArm && leftArm != null) || (f.RightArm && rightArm != null)))
            {
                float amplitude = ModelGait.ArmSwing(speed) * profile.ArmSwingScale;
                float gain = Mathf.Abs(f.Direction.z) * w;
                float abduction = profile.ArmAbduction * w;
                if (f.LeftArm && leftArm != null) leftArm.Apply(reference, leftPhase, f.Direction.z, amplitude, gain, -abduction);
                if (f.RightArm && rightArm != null) rightArm.Apply(reference, rightPhase, f.Direction.z, amplitude, gain, abduction);
                armsPosed = true;
            }
        }

        private static void GaitFoot(Leg foot, Transform reference, in ModelGaitFrame f, ModelGaitProfile profile, float phase,
            float stride, float stance, float lead, float lift, float speed, float referenceYaw, float baseY, Vector3 slot,
            out Vector3 flat, out float yaw, out Vector3 normal, out float pitch)
        {
            pitch = ModelGait.FootPitch(phase, stance, speed);
            if (phase < stance)
            {
                if (!foot.Planted) foot.Touchdown();
                // A plant the leg can no longer reach (a missed warp) is released to its slot instead of stretching.
                if (HorizontalDistance(foot.Plant, slot) > Mathf.Max(.8f, stride * .8f) || Mathf.Abs(foot.Plant.y - slot.y) > .35f)
                    foot.Land(slot, referenceYaw, Vector3.up);
                flat = foot.Plant; yaw = foot.PlantYaw; normal = foot.PlantNormal;
                foot.SwingU = 1f;
                return;
            }
            float s = (phase - stance) / (1f - stance);
            if (foot.Planted || !foot.Stepping) foot.Liftoff(s);
            float u = foot.FromS >= .999f ? 1f : Mathf.Clamp01((s - foot.FromS) / (1f - foot.FromS));
            foot.SwingU = u;
            float remaining = (1f - s) * (1f - stance) * stride / speed;
            float turn = Mathf.Clamp(f.YawRate * remaining, -75f, 75f);
            Quaternion turning = Quaternion.AngleAxis(turn, Vector3.up);
            Vector3 local = foot.StandingLocal(profile) + f.Direction * lead;
            local.x += foot.Side * .05f * Mathf.Abs(f.Direction.x);
            local.x = foot.Side * Mathf.Max(Mathf.Abs(foot.StandingAnkle.x) * .9f, foot.Side * local.x);
            Vector3 predictedRoot = f.Pivot + f.Velocity * remaining + turning * (reference.position - f.Pivot);
            Vector3 land = Snap(predictedRoot + turning * (reference.rotation * local), f.Ground, baseY, out Vector3 landNormal);
            float h = ModelGait.Ease(u);
            flat = Vector3.Lerp(foot.From, land, h) + Vector3.up * (ModelGait.StepBump(u) * lift);
            yaw = Mathf.LerpAngle(foot.FromYaw, referenceYaw + turn, h);
            normal = Vector3.Slerp(foot.FromNormal, landNormal, h);
        }

        private static void IdleFoot(Leg foot, float dt, Vector3 slot, Vector3 slotNormal, float referenceYaw,
            out Vector3 flat, out float yaw, out Vector3 normal, out float pitch)
        {
            pitch = Mathf.MoveTowards(foot.ShownPitch, 0f, dt * 160f);
            if (foot.Stepping)
            {
                if (dt > 0f) foot.StepT += dt / Mathf.Max(.05f, foot.StepDuration);
                float u = Mathf.Clamp01(foot.StepT), h = ModelGait.Ease(u);
                foot.SwingU = u;
                flat = Vector3.Lerp(foot.From, slot, h) + Vector3.up * (ModelGait.StepBump(u) * IdleStepLift);
                yaw = Mathf.LerpAngle(foot.FromYaw, referenceYaw, h);
                normal = Vector3.Slerp(foot.FromNormal, slotNormal, h);
                if (u >= 1f) foot.Land(slot, referenceYaw, slotNormal);
                return;
            }
            // A plant that is absurdly far (missed warp, a lift or carrier) jumps home instead of stretching the leg.
            if (HorizontalDistance(foot.Plant, slot) > .9f || Mathf.Abs(foot.Plant.y - slot.y) > .35f) foot.Land(slot, referenceYaw, slotNormal);
            flat = foot.Plant; yaw = foot.PlantYaw; normal = foot.PlantNormal;
            foot.SwingU = 1f;
        }

        // One foot steps at a time: the one furthest from its standing slot in distance and heading.
        private void ChooseIdleStep(float dt, Vector3 leftSlot, Vector3 rightSlot, float referenceYaw, float turnRate)
        {
            if (dt <= 0f || left.Stepping || right.Stepping) return;
            float leftError = Error(left, leftSlot, referenceYaw), rightError = Error(right, rightSlot, referenceYaw);
            float worst = Mathf.Max(leftError, rightError);
            if (worst < (squaring ? .3f : 1f)) { if (!turning) squaring = false; return; }
            bool moveLeft = leftError >= rightError;
            Leg mover = moveLeft ? left : right;
            float distance = HorizontalDistance(mover.Plant, moveLeft ? leftSlot : rightSlot);
            // Quicker steps when the body turns fast, so the planted foot does not twist too far behind.
            float hurry = Mathf.Lerp(1f, .7f, Mathf.Clamp01(turnRate / 200f));
            mover.BeginStep(Mathf.Lerp(.26f, .38f, Mathf.Clamp01(distance / .45f)) * hurry);
        }

        private static float Error(Leg foot, Vector3 slot, float referenceYaw)
        {
            float e = HorizontalDistance(foot.Plant, slot) / StepDistance
                + Mathf.Abs(Mathf.DeltaAngle(foot.PlantYaw, referenceYaw)) / StepYaw;
            return Mathf.Abs(foot.Plant.y - slot.y) > .04f ? e + 1f : e;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        // Moves a flat ankle position (ground + ankle height) onto the real ground below it, within a step's height.
        private static Vector3 Snap(Vector3 ankle, ModelGaitGroundProbe probe, float baseY, out Vector3 normal)
        {
            normal = Vector3.up;
            if (probe == null || !probe(new Vector3(ankle.x, baseY + .28f, ankle.z), .6f, out float ground, out Vector3 hitNormal))
                return ankle;
            normal = hitNormal;
            return new Vector3(ankle.x, ankle.y + Mathf.Clamp(ground - baseY, -.3f, .25f), ankle.z);
        }

        private static float SmoothHigher(float a, float b, float width)
        {
            float overlap = Mathf.Max(0f, width - Mathf.Abs(a - b));
            return Mathf.Max(a, b) + (width > 0f ? overlap * overlap / (4f * width) : 0f);
        }

        internal static Transform Find(Dictionary<string, Transform> nodes, string name)
        {
            if (nodes.TryGetValue(name, out Transform bone)) return bone;
            throw new InvalidOperationException("Grounded body is missing imported joint " + name);
        }

        private sealed class Arm
        {
            private readonly Transform upper, forearm;
            private Quaternion upperIdle, forearmIdle;

            public Arm(Dictionary<string, Transform> nodes, string suffix)
            {
                upper = Find(nodes, "upperarm01." + suffix);
                forearm = Find(nodes, "lowerarm01." + suffix);
                if (!forearm.IsChildOf(upper)) throw new InvalidOperationException("Invalid grounded body arm chain.");
            }

            public void CaptureStanding() { upperIdle = upper.localRotation; forearmIdle = forearm.localRotation; }
            public void RestoreStanding() { upper.localRotation = upperIdle; forearm.localRotation = forearmIdle; }

            public void Apply(Transform reference, float legPhase, float forwardTravel, float amplitude, float gain, float abduction)
            {
                // Held off the torso (suit), then the same-side arm moves back as its foot leads; the forearm flexes forward.
                if (abduction != 0f) upper.rotation = Quaternion.AngleAxis(abduction, reference.forward) * upper.rotation;
                if (Mathf.Abs(forwardTravel) <= .01f) return;
                float swing = Mathf.Cos(2f * Mathf.PI * (legPhase - .045f)) * Mathf.Sign(forwardTravel);
                upper.rotation = Quaternion.AngleAxis(swing * amplitude * gain, reference.right) * upper.rotation;
                float bend = Mathf.Lerp(3f, 6f + amplitude * .55f, .5f * (1f - swing));
                forearm.rotation = Quaternion.AngleAxis(-bend * gain, reference.right) * forearm.rotation;
            }
        }

        internal sealed class Leg
        {
            private readonly Transform shin, ankle;
            private readonly float side;
            public readonly Transform Thigh;
            private Quaternion thighRotation, shinRotation, ankleRotation, standingAnkleOrientation;
            private Vector3 standingAnkle, standingKneePole;
            private float thighLength, shinLength, reachCap, heelBack = .065f, ballForward = .14f, ankleHeight = .085f;

            // World-anchored foot state.
            public bool Valid, Planted, Stepping;
            /// <summary>Progress of the current step in the air (0 lift-off .. 1 landing); 1 while planted.</summary>
            public float SwingU = 1f;
            /// <summary>How much this foot may pull the pelvis down: fully while planted, fading out after lift-off and in before landing.</summary>
            public float ReachWeight => Planted ? 1f : Mathf.Max(1f - SwingU * 4f, Mathf.SmoothStep(0f, 1f, (SwingU - .5f) * 2f));
            public Vector3 Plant, From, Shown;
            public Vector3 PlantNormal = Vector3.up, FromNormal = Vector3.up, ShownNormal = Vector3.up;
            public float PlantYaw, FromYaw, ShownYaw, ShownPitch, FromS, StepT, StepDuration = .3f;
            public float SlotDistance, YawOffset;

            public float Side => side;
            public float Length => thighLength + shinLength;
            public float ThighLength => thighLength;
            public float ShinLength => shinLength;
            public Vector3 StandingAnkle => standingAnkle;
            public Vector3 HipPosition => Thigh.position;
            public Vector3 AnklePosition => ankle.position;

            public Leg(Dictionary<string, Transform> nodes, string suffix, float sideSign)
            {
                Thigh = Find(nodes, "upperleg01." + suffix);
                shin = Find(nodes, "lowerleg01." + suffix);
                ankle = Find(nodes, "foot." + suffix);
                side = sideSign;
                if (!shin.IsChildOf(Thigh) || !ankle.IsChildOf(shin))
                    throw new InvalidOperationException("Invalid grounded body leg chain for side " + suffix);
            }

            /// <summary>Any two-bone chain (for example upper arm, forearm, wrist) solved like a leg.</summary>
            public Leg(Transform upper, Transform middle, Transform end, float sideSign)
            {
                Thigh = upper; shin = middle; ankle = end; side = sideSign;
                if (!Thigh || !shin || !ankle || !shin.IsChildOf(Thigh) || !ankle.IsChildOf(shin))
                    throw new InvalidOperationException("Invalid two-bone limb chain.");
            }

            public Vector3 StandingLocal(ModelGaitProfile profile) => standingAnkle + new Vector3(side * profile.StanceWiden, 0f, 0f);

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
                // Never let the walking leg need to be longer than the captured standing leg, so a standing
                // body (gait weight 0) is not pulled down by the reach limit.
                float standingReach = (ankle.position - Thigh.position).magnitude;
                reachCap = Mathf.Min(thighLength + shinLength - .004f, Mathf.Max(standingReach + .004f, (thighLength + shinLength) * .975f));
                standingHeight = Thigh.position.y - ankle.position.y;
                ankleHeight = Mathf.Clamp(standingAnkle.y, .04f, .14f);
                float toes = 0f; int count = 0;
                foreach (Transform t in ankle.GetComponentsInChildren<Transform>(true))
                {
                    if (t == ankle || !t.name.StartsWith("toe", StringComparison.OrdinalIgnoreCase)) continue;
                    toes += reference.InverseTransformPoint(t.position).z - standingAnkle.z; count++;
                }
                if (count > 0) ballForward = Mathf.Clamp(toes / count, .08f, .22f);
                heelBack = Mathf.Clamp(ballForward * .5f, .04f, .09f);
            }

            /// <summary>Horizontal ankle offset from below the hip that the leg reaches with <paramref name="drop"/> of crouch.</summary>
            public float HalfSpan(float drop)
            {
                float height = Mathf.Max(.1f, standingHeight - drop);
                return Mathf.Sqrt(Mathf.Max(0f, reachCap * reachCap - height * height));
            }
            private float standingHeight;

            public void RestoreStanding()
            {
                Thigh.localRotation = thighRotation;
                shin.localRotation = shinRotation;
                ankle.localRotation = ankleRotation;
            }

            public void Reset(Vector3 slot, float yaw, Vector3 normal)
            {
                Valid = true; Planted = true; Stepping = false;
                Plant = From = Shown = slot;
                PlantYaw = FromYaw = ShownYaw = yaw; ShownPitch = 0f;
                PlantNormal = FromNormal = ShownNormal = normal;
            }

            public void Touchdown()
            {
                Planted = true; Stepping = false;
                Plant = Shown; PlantYaw = ShownYaw; PlantNormal = ShownNormal;
            }

            public void Liftoff(float s)
            {
                Planted = false; Stepping = true;
                From = Shown; FromYaw = ShownYaw; FromNormal = ShownNormal; FromS = s;
            }

            public void BeginStep(float duration)
            {
                Planted = false; Stepping = true; StepT = 0f; StepDuration = duration;
                From = Shown; FromYaw = ShownYaw; FromNormal = ShownNormal;
            }

            public void EndGait()
            {
                if (Planted) { Stepping = false; return; }
                BeginStep(.3f);
            }

            public void Land(Vector3 slot, float yaw, Vector3 normal)
            {
                Stepping = false; Planted = true;
                Plant = slot; PlantYaw = yaw; PlantNormal = normal;
            }

            public void Show(Vector3 flat, float yaw, Vector3 normal, float pitch, Vector3 slot, float referenceYaw)
            {
                Shown = flat; ShownYaw = yaw; ShownNormal = normal; ShownPitch = pitch;
                SlotDistance = new Vector2(flat.x - slot.x, flat.z - slot.z).magnitude;
                YawOffset = Mathf.DeltaAngle(referenceYaw, yaw);
            }

            // The ankle target: the flat foot position plus the heel/ball rocker for the shown pitch, in the foot's frame.
            public Vector3 AnkleTarget(Transform reference)
            {
                Vector2 rocker = ModelGait.RockerAnkleShift(ShownPitch, heelBack, ballForward, ankleHeight);
                return Shown + FootFrame(reference) * new Vector3(0f, rocker.y, rocker.x);
            }

            private Quaternion FootFrame(Transform reference) =>
                GroundAlign(ShownNormal) * Quaternion.AngleAxis(Mathf.DeltaAngle(reference.eulerAngles.y, ShownYaw), Vector3.up) * reference.rotation;

            public float DropToReach(Vector3 target)
            {
                Vector3 offset = Thigh.position - target;
                float heightSquared = reachCap * reachCap - offset.x * offset.x - offset.z * offset.z;
                return Mathf.Max(0f, offset.y - Mathf.Sqrt(Mathf.Max(0f, heightSquared)));
            }

            /// <summary>Grounded pose: knee towards the foot's heading, foot turned and tilted to its ground.</summary>
            public void Pose(Transform reference, Vector3 target, float poleWeight)
            {
                float yawOffset = Mathf.DeltaAngle(reference.eulerAngles.y, ShownYaw);
                Vector3 localPole = Vector3.Slerp(standingKneePole, new Vector3(side * .08f, 0f, 1f).normalized, poleWeight);
                Solve(target, Quaternion.AngleAxis(yawOffset * .5f, Vector3.up) * reference.TransformDirection(localPole));
                ankle.rotation = FootFrame(reference) * Quaternion.AngleAxis(-ShownPitch, Vector3.right) * standingAnkleOrientation;
            }

            /// <summary>Two-bone solve with an explicit world pole; the foot keeps its pose relative to the shin.</summary>
            public void PoseKeepFoot(Vector3 target, Vector3 worldPole) => Solve(target, worldPole);

            private void Solve(Vector3 target, Vector3 pole)
            {
                Vector3 hip = Thigh.position;
                Vector3 toAnkle = target - hip;
                Vector3 axis = toAnkle.normalized;
                float length = Mathf.Clamp(toAnkle.magnitude, Mathf.Abs(thighLength - shinLength) + .001f,
                    thighLength + shinLength - .001f);
                float kneeAlong = (thighLength * thighLength - shinLength * shinLength + length * length) / (2f * length);
                float kneeOut = Mathf.Sqrt(Mathf.Max(0f, thighLength * thighLength - kneeAlong * kneeAlong));
                Vector3 kneePole = Vector3.ProjectOnPlane(pole, axis).normalized;
                Vector3 knee = hip + axis * kneeAlong + kneePole * kneeOut;
                Vector3 reachableAnkle = hip + axis * length;
                Thigh.rotation = Quaternion.FromToRotation(shin.position - hip, knee - hip) * Thigh.rotation;
                shin.rotation = Quaternion.FromToRotation(ankle.position - shin.position, reachableAnkle - shin.position) * shin.rotation;
            }

            public void Sway(Vector3 worldAxis, float thighDegrees, float shinDegrees)
            {
                Thigh.rotation = Quaternion.AngleAxis(thighDegrees, worldAxis) * Thigh.rotation;
                shin.rotation = Quaternion.AngleAxis(shinDegrees, worldAxis) * shin.rotation;
            }

            public static Quaternion GroundAlign(Vector3 normal)
            {
                if (normal.sqrMagnitude < .5f) return Quaternion.identity;
                Vector3 limited = Vector3.RotateTowards(Vector3.up, normal.normalized, 22f * Mathf.Deg2Rad, 0f);
                return Quaternion.FromToRotation(Vector3.up, limited);
            }
        }
    }
}
