using System;
using System.Collections.Generic;
using UnityEngine;

namespace OutOfReach.Presentation.Motion
{
    /// <summary>Per-frame input to the grounded body rig, all in the body reference (yaw-only visual root) frame.</summary>
    internal struct ModelGaitFrame
    {
        public Vector3 Direction;   // unit horizontal direction of travel in the reference frame
        public float Phase;         // LEFT foot cycle phase, 0 = heel strike
        public float Speed;         // metres per second of the controller
        public float Weight;        // 0 standing .. 1 full gait
        public float Setback;       // pelvis setback (metres, backwards) for looking down
        public bool LeftArm, RightArm;
    }

    /// <summary>
    /// Two-bone leg IK, pelvis motion and opposing arm swing for the shared 164-bone rig. Targets are solved from the
    /// captured standing pose, so WORK and EVA use the same code. Only bone transforms are written; the caller
    /// restores them through its own ModelCache before every pose.
    /// </summary>
    internal sealed class ModelGaitRig
    {
        private readonly Transform hips;
        private readonly Leg left, right;
        private Arm leftArm, rightArm;
        private bool armsPosed;
        private Vector3 standingHipsPosition;

        public float LegLength { get; private set; } = ModelGait.ReferenceLegLength;

        public ModelGaitRig(Dictionary<string, Transform> nodes)
        {
            hips = Find(nodes, "root");
            left = new Leg(nodes, "L", -1f);
            right = new Leg(nodes, "R", 1f);
            if (!left.Thigh.IsChildOf(hips) || !right.Thigh.IsChildOf(hips))
                throw new InvalidOperationException("The body's visual hips must own both leg chains.");
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
            left.CaptureStanding(reference);
            right.CaptureStanding(reference);
            LegLength = Mathf.Clamp((left.Length + right.Length) * .5f, .55f, 1.3f);
            leftArm?.CaptureStanding();
            rightArm?.CaptureStanding();
            armsPosed = false;
        }

        public void RestoreStanding()
        {
            hips.localPosition = standingHipsPosition;
            left.RestoreStanding();
            right.RestoreStanding();
            if (armsPosed)
            {
                leftArm.RestoreStanding();
                rightArm.RestoreStanding();
                armsPosed = false;
            }
        }

        public void Apply(Transform reference, in ModelGaitFrame f)
        {
            RestoreStanding();
            float w = Mathf.SmoothStep(0f, 1f, f.Weight);
            float legLength = LegLength;
            float speed = Mathf.Max(f.Speed, .3f);
            float scale = legLength / ModelGait.ReferenceLegLength;
            float lateral = Mathf.Abs(f.Direction.x);
            float stride = ModelGait.StrideLength(speed, legLength, lateral);
            float stance = ModelGait.Stance(speed);
            float lift = ModelGait.Lift(speed, legLength);
            float leftPhase = Mathf.Repeat(f.Phase, 1f), rightPhase = Mathf.Repeat(f.Phase + .5f, 1f);

            Vector3 leftTarget = left.FootTarget(f.Direction, leftPhase, stride, stance, lift, speed, w, lateral, out float leftPitch);
            Vector3 rightTarget = right.FootTarget(f.Direction, rightPhase, stride, stance, lift, speed, w, lateral, out float rightPitch);
            leftTarget = reference.TransformPoint(leftTarget);
            rightTarget = reference.TransformPoint(rightTarget);

            float sway = ModelGait.PelvisSway(leftPhase, legLength, speed) * w;
            hips.position += reference.TransformDirection(new Vector3(sway, 0f, -f.Setback));
            float drop = SmoothHigher(.022f * scale * w, SmoothHigher(left.DropToReach(leftTarget), right.DropToReach(rightTarget), .02f * scale * w), .02f * scale * w);
            hips.position -= Vector3.up * Mathf.Min(.18f, drop);

            left.Pose(reference, leftTarget, leftPitch, w);
            right.Pose(reference, rightTarget, rightPitch, w);

            if (w > 0f && Mathf.Abs(f.Direction.z) > .01f && ((f.LeftArm && leftArm != null) || (f.RightArm && rightArm != null)))
            {
                float amplitude = ModelGait.ArmSwing(speed);
                float gain = Mathf.Abs(f.Direction.z) * w;
                if (f.LeftArm) leftArm.Apply(reference.right, leftPhase, f.Direction.z, amplitude, gain);
                if (f.RightArm) rightArm.Apply(reference.right, rightPhase, f.Direction.z, amplitude, gain);
                armsPosed = true;
            }
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

            public void Apply(Vector3 pitchAxis, float legPhase, float forwardTravel, float amplitude, float gain)
            {
                // The same-side arm moves back as its foot leads; the forearm flexes on the forward swing.
                float swing = Mathf.Cos(2f * Mathf.PI * (legPhase - .045f)) * Mathf.Sign(forwardTravel);
                upper.rotation = Quaternion.AngleAxis(swing * amplitude * gain, pitchAxis) * upper.rotation;
                float bend = Mathf.Lerp(3f, 6f + amplitude * .55f, .5f * (1f - swing));
                forearm.rotation = Quaternion.AngleAxis(-bend * gain, pitchAxis) * forearm.rotation;
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

            public float Length => thighLength + shinLength;
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

            public void Sway(Vector3 worldAxis, float thighDegrees, float shinDegrees)
            {
                Thigh.rotation = Quaternion.AngleAxis(thighDegrees, worldAxis) * Thigh.rotation;
                shin.rotation = Quaternion.AngleAxis(shinDegrees, worldAxis) * shin.rotation;
            }

            public void RestoreStanding()
            {
                Thigh.localRotation = thighRotation;
                shin.localRotation = shinRotation;
                ankle.localRotation = ankleRotation;
            }

            public Vector3 FootTarget(Vector3 direction, float phase, float stride, float stance, float lift,
                float speed, float weight, float lateral, out float pitch)
            {
                float foreAft = ModelGait.FootForeAft(phase, stride, stance);
                float height = ModelGait.FootHeight(phase, stance, lift);
                pitch = ModelGait.FootPitch(phase, stance, speed);
                Vector2 rocker = ModelGait.RockerAnkleShift(pitch, heelBack, ballForward, ankleHeight);
                Vector3 target = standingAnkle + (direction * foreAft + Vector3.up * (height + rocker.y) + Vector3.forward * rocker.x) * weight;
                target.x += side * .05f * lateral * weight;
                // Keep the inner edge of a strafe from crossing the other boot.
                target.x = side * Mathf.Max(Mathf.Abs(standingAnkle.x) * .9f, side * target.x);
                pitch *= weight;
                return target;
            }

            public float DropToReach(Vector3 target)
            {
                Vector3 offset = Thigh.position - target;
                float heightSquared = reachCap * reachCap - offset.x * offset.x - offset.z * offset.z;
                return Mathf.Max(0f, offset.y - Mathf.Sqrt(Mathf.Max(0f, heightSquared)));
            }

            public void Pose(Transform reference, Vector3 target, float pitchToesUp, float weight, Vector3? worldPole = null)
            {
                Vector3 hip = Thigh.position;
                Vector3 toAnkle = target - hip;
                Vector3 axis = toAnkle.normalized;
                float length = Mathf.Clamp(toAnkle.magnitude, Mathf.Abs(thighLength - shinLength) + .001f,
                    thighLength + shinLength - .001f);
                float kneeAlong = (thighLength * thighLength - shinLength * shinLength + length * length) / (2f * length);
                float kneeOut = Mathf.Sqrt(Mathf.Max(0f, thighLength * thighLength - kneeAlong * kneeAlong));
                Vector3 localPole = Vector3.Slerp(standingKneePole, new Vector3(side * .08f, 0f, 1f).normalized, weight);
                Vector3 kneePole = Vector3.ProjectOnPlane(worldPole ?? reference.TransformDirection(localPole), axis).normalized;
                Vector3 knee = hip + axis * kneeAlong + kneePole * kneeOut;
                Vector3 reachableAnkle = hip + axis * length;

                Thigh.rotation = Quaternion.FromToRotation(shin.position - hip, knee - hip) * Thigh.rotation;
                shin.rotation = Quaternion.FromToRotation(ankle.position - shin.position, reachableAnkle - shin.position) * shin.rotation;
                // AngleAxis about +x tips the toes DOWN, so a toes-up pitch is the negative angle.
                ankle.rotation = reference.rotation * Quaternion.AngleAxis(-pitchToesUp, Vector3.right) * standingAnkleOrientation;
            }
        }
    }
}
