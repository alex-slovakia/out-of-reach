using System;
using OutOfReach.Presentation.Motion;
using UnityEngine;

namespace OutOfReach.Presentation.Cargo
{
    /// <summary>
    /// Runs after the body presenter (200) and its gait (250): places the carried crate in front of the visible body's
    /// own shoulders and lets both of the body's arms hold its side handles. Added at runtime next to
    /// <see cref="PreflightCargoHandsPresenter"/>; presentation only (no input, movement, physics or domain state).
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(265)]
    public sealed class CargoCarryArmsDriver : MonoBehaviour
    {
        private PreflightCargoHandsPresenter presenter;

        internal void Bind(PreflightCargoHandsPresenter owner) => presenter = owner;

        private void LateUpdate()
        {
            if (presenter) presenter.LateBodyCarry();
        }
    }

    /// <summary>
    /// A presentation lease on BOTH visible body arms (WORK or EVA: the same 164-bone rig) while a preflight crate is
    /// carried: a two-bone reach of the existing upper arm and forearm to each side handle with the elbows low and out,
    /// the wrist turned so the palm hooks the handle from outside, and a finger wrap around the tube. Rotations only;
    /// every touched bone is given back on release and on <see cref="EmbodiedBodyPresenter.PoseWillReset"/>.
    /// </summary>
    internal sealed class CargoCarryArms : IDisposable
    {
        internal const float GripAlongHand = .80f;     // tube across the palm near the finger bases (fraction of hand length)
        internal const float PalmToTubeCentre = .034f;  // palm surface 17 mm in front of the bone line + 17 mm tube radius
        private static readonly float[] FingerCurl = { 48f, 62f, 44f };
        private static readonly float[] ThumbCurl = { 12f, 22f, 26f };
        /// <summary>
        /// The first-person camera sits about 0.37 m ahead of the body's shoulder joints, so a crate within natural reach
        /// would sit under the camera. While carrying, each shoulder joint reaches forward (yaw frame, metres); the stretch
        /// stays at the shoulder, behind the camera, and the visible forearms and hands keep their real proportions.
        /// </summary>
        private static readonly Vector3 ShoulderReach = new Vector3(0f, -.03f, .22f);

        private sealed class Arm
        {
            public bool Left;
            public Transform Upper, Fore, Wrist;
            public Transform[][] Fingers;      // [0] thumb .. [4] pinky, joints 1..3
            public Quaternion HandInWrist;     // LookRotation(hand axis, palm normal) relative to the wrist rotation
            public float HandLength;
            public Transform[] Owned;
            public Quaternion[] Baseline, LastWritten;
            public Vector3 ShoulderBase, ShoulderLast;
        }

        private readonly EmbodiedBodyPresenter body;
        private Arm left, right;
        private bool acquired, subscribed, written;

        public CargoCarryArms(EmbodiedBodyPresenter visibleBody) => body = visibleBody;

        public bool IsHolding => acquired;
        /// <summary>Largest distance of a wrist from its solved target after the reach (0 when both handles are in reach).</summary>
        public float LastReachShortfallMetres { get; private set; } = float.NaN;
        public float ArmLengthMetres { get; private set; } = float.NaN;
        public float HandLengthMetres => left != null ? left.HandLength : float.NaN;

        /// <summary>The body is shown and its arm chains can be resolved.</summary>
        public bool CanHold => body && body.IsBound && body.IsVisible;

        /// <summary>
        /// Starts a carry frame from the pose the body presenter sampled and reaches both shoulder joints forward. Returns
        /// the reached shoulder positions the crate is placed from; <see cref="Hold"/> then solves the arms.
        /// </summary>
        public bool Begin(Quaternion yawFrame, float weight, out Vector3 leftShoulder, out Vector3 rightShoulder)
        {
            leftShoulder = rightShoulder = Vector3.zero;
            if (!CanHold) { Release(); return false; }
            if (!acquired && !Acquire()) return false;
            float w = Mathf.Clamp01(weight);
            foreach (Arm arm in new[] { left, right })
            {
                // Stateless per frame: a writer that re-sampled these bones since our last write defines the new rest.
                for (int i = 0; i < arm.Owned.Length; i++)
                {
                    if (written && arm.Owned[i].localRotation != arm.LastWritten[i]) arm.Baseline[i] = arm.Owned[i].localRotation;
                    arm.Owned[i].localRotation = arm.Baseline[i];
                }
                if (written && arm.Upper.localPosition != arm.ShoulderLast) arm.ShoulderBase = arm.Upper.localPosition;
                arm.Upper.localPosition = arm.ShoulderBase;
                arm.Upper.position += yawFrame * ShoulderReach * w;
                arm.ShoulderLast = arm.Upper.localPosition;
            }
            written = true;
            leftShoulder = left.Upper.position; rightShoulder = right.Upper.position;
            ArmLengthMetres = Vector3.Distance(left.Upper.position, left.Fore.position) + Vector3.Distance(left.Fore.position, left.Wrist.position);
            return true;
        }

        /// <summary>Poses both arms on the handle frames (X tube axis, Y away from the crate, Z up its side).</summary>
        public bool Hold(Transform leftGrip, Transform rightGrip, Quaternion yawFrame, float weight)
        {
            if (!acquired || !CanHold) return false;
            float shortfall = 0f;
            float w = Mathf.Clamp01(weight);
            foreach (Arm arm in new[] { left, right })
            {
                shortfall = Mathf.Max(shortfall, Solve(arm, arm.Left ? leftGrip : rightGrip, yawFrame));
                if (w < .999f)
                    for (int i = 0; i < arm.Owned.Length; i++)
                        arm.Owned[i].localRotation = Quaternion.Slerp(arm.Baseline[i], arm.Owned[i].localRotation, w);
                for (int i = 0; i < arm.Owned.Length; i++) arm.LastWritten[i] = arm.Owned[i].localRotation;
            }
            LastReachShortfallMetres = shortfall;
            return true;
        }

        public void Release()
        {
            if (acquired)
                foreach (Arm arm in new[] { left, right })
                    if (arm != null) Restore(arm);
            acquired = false; written = false;
            if (subscribed && body) body.PoseWillReset -= OnPoseWillReset;
            subscribed = false;
        }

        public void Dispose() => Release();

        private void OnPoseWillReset()
        {
            // Synchronous, before the presenter re-samples: give the bones back and re-acquire on the next carry frame.
            if (acquired)
                foreach (Arm arm in new[] { left, right })
                    if (arm != null) Restore(arm);
            acquired = false; written = false;
        }

        private static void Restore(Arm arm)
        {
            for (int i = 0; i < arm.Owned.Length; i++) if (arm.Owned[i]) arm.Owned[i].localRotation = arm.Baseline[i];
            if (arm.Upper) arm.Upper.localPosition = arm.ShoulderBase;
        }

        private bool Acquire()
        {
            left = Resolve(true); right = Resolve(false);
            if (left == null || right == null) return false;
            if (!subscribed) { body.PoseWillReset += OnPoseWillReset; subscribed = true; }
            acquired = true; written = false;
            return true;
        }

        private Arm Resolve(bool isLeft)
        {
            string s = isLeft ? ".L" : ".R";
            var arm = new Arm
            {
                Left = isLeft,
                Upper = body.GetVisibleBone("upperarm01" + s), Fore = body.GetVisibleBone("lowerarm01" + s), Wrist = body.GetVisibleBone("wrist" + s),
                Fingers = new Transform[5][]
            };
            if (!arm.Upper || !arm.Fore || !arm.Wrist || !arm.Wrist.IsChildOf(arm.Fore) || !arm.Fore.IsChildOf(arm.Upper)) return null;
            for (int f = 0; f < 5; f++)
            {
                arm.Fingers[f] = new Transform[3];
                for (int j = 0; j < 3; j++)
                {
                    Transform bone = body.GetVisibleBone("finger" + (f + 1) + "-" + (j + 1) + s);
                    if (!bone || !bone.IsChildOf(arm.Wrist)) return null;
                    arm.Fingers[f][j] = bone;
                }
            }
            // Anatomical hand frame from the sampled pose: wrist -> middle-finger base, index -> pinky across the palm.
            Vector3 a = arm.Fingers[2][0].position - arm.Wrist.position;
            arm.HandLength = a.magnitude;
            Vector3 across = arm.Fingers[1][0].position - arm.Fingers[4][0].position;
            if (arm.HandLength < .02f || across.sqrMagnitude < 1e-6f) return null;
            a /= arm.HandLength;
            Vector3 palm = (isLeft ? -1f : 1f) * Vector3.Cross(a, across.normalized);
            if (palm.sqrMagnitude < 1e-6f) return null;
            arm.HandInWrist = Quaternion.Inverse(arm.Wrist.rotation) * Quaternion.LookRotation(a, palm.normalized);
            arm.Owned = new Transform[3 + 15];
            arm.Owned[0] = arm.Upper; arm.Owned[1] = arm.Fore; arm.Owned[2] = arm.Wrist;
            for (int f = 0; f < 5; f++) for (int j = 0; j < 3; j++) arm.Owned[3 + f * 3 + j] = arm.Fingers[f][j];
            arm.Baseline = new Quaternion[arm.Owned.Length];
            arm.LastWritten = new Quaternion[arm.Owned.Length];
            for (int i = 0; i < arm.Owned.Length; i++) arm.Baseline[i] = arm.Owned[i].localRotation;
            arm.ShoulderBase = arm.ShoulderLast = arm.Upper.localPosition;
            return arm;
        }

        private float Solve(Arm arm, Transform grip, Quaternion yaw)
        {
            Vector3 handle = grip.position;
            // Underhand side grip: fingers point up the crate's side and wrap over the tube, palm toward the crate.
            Vector3 handAxis = grip.forward.normalized;
            Vector3 palm = -grip.up.normalized;
            Quaternion handFrame = Quaternion.LookRotation(handAxis, palm);
            Quaternion wristWorld = handFrame * Quaternion.Inverse(arm.HandInWrist);
            Vector3 wristTarget = handle - handAxis * (GripAlongHand * arm.HandLength) - palm * PalmToTubeCentre;

            // Two-bone reach, elbow low, out and slightly back.
            Vector3 s = arm.Upper.position, e = arm.Fore.position, w = arm.Wrist.position;
            float l1 = (e - s).magnitude, l2 = (w - e).magnitude;
            if (l1 < 1e-4f || l2 < 1e-4f) return float.NaN;
            Vector3 toTarget = wristTarget - s;
            float requested = toTarget.magnitude;
            float d = Mathf.Clamp(requested, Mathf.Abs(l1 - l2) + .002f, l1 + l2 - .002f);
            Vector3 dir = requested > 1e-5f ? toTarget / requested : yaw * Vector3.forward;
            Vector3 pole = yaw * new Vector3(arm.Left ? -.55f : .55f, -.75f, -.30f);
            Vector3 bend = pole - Vector3.Dot(pole, dir) * dir;
            if (bend.sqrMagnitude < 1e-6f) bend = yaw * Vector3.down - Vector3.Dot(yaw * Vector3.down, dir) * dir;
            bend.Normalize();
            float along = (l1 * l1 - l2 * l2 + d * d) / (2f * d);
            float h = Mathf.Sqrt(Mathf.Max(0f, l1 * l1 - along * along));
            Vector3 elbow = s + dir * along + bend * h;
            arm.Upper.rotation = Quaternion.FromToRotation(e - s, elbow - s) * arm.Upper.rotation;
            Vector3 e2 = arm.Fore.position, w2 = arm.Wrist.position;
            arm.Fore.rotation = Quaternion.FromToRotation(w2 - e2, (s + dir * d) - e2) * arm.Fore.rotation;
            // Half of the wrist roll about the forearm goes into the forearm so the wrist skin does not wrap.
            Vector3 axis = (arm.Wrist.position - arm.Fore.position).normalized;
            float twist = TwistAngle(wristWorld * Quaternion.Inverse(arm.Wrist.rotation), axis);
            if (!float.IsNaN(twist)) arm.Fore.rotation = Quaternion.AngleAxis(twist * .5f, axis) * arm.Fore.rotation;
            arm.Wrist.rotation = wristWorld;

            // Finger wrap toward the palm about each finger's own curl axis, proximal to distal.
            for (int fi = 0; fi < 5; fi++)
            {
                Transform[] chain = arm.Fingers[fi];
                Vector3 dirF = chain[1].position - chain[0].position;
                Vector3 k = Vector3.Cross(dirF, palm);
                if (k.sqrMagnitude < 1e-8f) continue;
                k.Normalize();
                float sign = Vector3.Dot(Quaternion.AngleAxis(10f, k) * dirF, palm) > Vector3.Dot(dirF, palm) ? 1f : -1f;
                float[] curl = fi == 0 ? ThumbCurl : FingerCurl;
                for (int j = 0; j < 3; j++) chain[j].rotation = Quaternion.AngleAxis(sign * curl[j], k) * chain[j].rotation;
            }
            return Mathf.Max(0f, requested - d);
        }

        private static float TwistAngle(Quaternion q, Vector3 axis)
        {
            Vector3 v = new Vector3(q.x, q.y, q.z);
            Vector3 p = Vector3.Dot(v, axis) * axis;
            var twist = new Quaternion(p.x, p.y, p.z, q.w);
            float m = Mathf.Sqrt(twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w);
            if (m < 1e-6f) return 0f;
            twist = new Quaternion(twist.x / m, twist.y / m, twist.z / m, twist.w / m);
            twist.ToAngleAxis(out float angle, out Vector3 twistAxis);
            if (angle > 180f) angle -= 360f;
            return Vector3.Dot(twistAxis, axis) < 0f ? -angle : angle;
        }
    }
}
