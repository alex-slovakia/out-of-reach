using System;
using OutOfReach.Presentation.Motion;
using UnityEngine;

namespace OutOfReach.Presentation.Meal
{
    /// <summary>
    /// A presentation lease on the visible WORK body's LEFT arm while it tears the meal pouch open (R35): a two-bone
    /// reach of the existing upper arm and forearm with the elbow low and out, half the wrist roll moved into the forearm,
    /// and a thumb-and-index pinch built from the rig's own finger joints (index curled, thumb brought to its pad, the
    /// other fingers closed). Like <see cref="MealHandClasp"/> it writes rotations only, after the body presenter
    /// (execution order 260 &gt; 200/250), restores every bone it touched when it lets go, and gives the arm back at once
    /// on <see cref="EmbodiedBodyPresenter.PoseWillReset"/>. Never in the EVA suit; nothing in it changes movement,
    /// input, physics or domain state.
    /// </summary>
    internal sealed class MealOffHand : IDisposable
    {
        private const string UpperArm = "upperarm01.L", ForeArm = "lowerarm01.L", Wrist = "wrist.L";
        // Flexion in degrees about each finger's own curl axis, proximal to distal.
        private static readonly float[] IndexCurl = { 40f, 48f, 30f };
        private static readonly float[] OtherCurl = { 64f, 72f, 50f };
        private static readonly float[] ThumbCurl = { 14f, 16f, 12f };
        /// <summary>Distance of the thumb and index bone ends when their pads meet on the foil, metres.</summary>
        private const float PadGap = .013f;
        /// <summary>Largest correction of one thumb joint when it is brought to the index pad, degrees.</summary>
        private const float ThumbLimitDegrees = 42f;

        private readonly EmbodiedBodyPresenter body;
        private Transform upper, fore, wrist;
        private Transform[][] fingers = Array.Empty<Transform[]>();   // [0] thumb .. [4] little finger, joints 1..3
        private Vector3[] tipInJoint = Array.Empty<Vector3>();
        private Transform[] owned = Array.Empty<Transform>();
        private Quaternion[] baseline = Array.Empty<Quaternion>(), lastWritten = Array.Empty<Quaternion>(), closed = Array.Empty<Quaternion>();
        private Quaternion handInWrist = Quaternion.identity;
        private Vector3 pinchInWrist;
        private bool acquired, subscribed, written;

        public MealOffHand(EmbodiedBodyPresenter visibleBody) => body = visibleBody;

        public bool IsHolding => acquired;

        /// <summary>Distance of the arm's actual reach from the requested wrist target (0 when reachable).</summary>
        public float LastReachShortfallMetres { get; private set; } = float.NaN;

        /// <summary>How far the closed thumb and index pads are from touching (0 = they meet); diagnostic.</summary>
        public float PinchGapMetres { get; private set; } = float.NaN;

        /// <summary>The visible outfit is the bare-handed WORK body with its left arm shown.</summary>
        public bool CanHold => body && body.IsBound && body.IsVisible && !body.IsEvaVisible
            && (body.VisibleArms & EmbodiedBodyPresenter.BodyArmVisibility.Left) != 0;

        /// <summary>
        /// Puts the pinch (between the thumb and index pads) at <paramref name="target"/> with the hand frame
        /// <paramref name="handFrame"/> (forward along the fingers, up out of the palm), arm weight
        /// <paramref name="armWeight"/> and finger closure <paramref name="pinch"/>. Returns where the actual pinch is.
        /// </summary>
        public bool TryPinch(Vector3 target, Quaternion handFrame, float armWeight, float pinch, Quaternion eyeRotation, out Vector3 pinchPoint)
        {
            pinchPoint = target;
            if (!CanHold) { Release(); return false; }
            if (!acquired && !Acquire()) return false;
            if (!upper || !fore || !wrist) { Release(); return false; }

            // Stateless per frame: start from the pose the body presenter sampled, never from last frame's writes. A writer
            // that poses these bones every frame (the floating body) has replaced our last values: take its pose as the rest.
            for (int i = 0; i < owned.Length; i++)
            {
                if (written && owned[i].localRotation != lastWritten[i]) baseline[i] = owned[i].localRotation;
                owned[i].localRotation = baseline[i];
            }

            Quaternion wristWorld = handFrame * Quaternion.Inverse(handInWrist);
            SolveArm(target - wristWorld * pinchInWrist, wristWorld, eyeRotation);

            // Blend the arm chain from the sampled pose (parent first) so the reach and the release never pop.
            float w = Mathf.Clamp01(armWeight);
            if (w < .999f)
                for (int i = 0; i < 3; i++) owned[i].localRotation = Quaternion.Slerp(baseline[i], owned[i].localRotation, w);
            float close = Mathf.Clamp01(pinch) * w;
            for (int i = 3; i < owned.Length; i++) owned[i].localRotation = Quaternion.Slerp(baseline[i], closed[i], close);

            for (int i = 0; i < owned.Length; i++) lastWritten[i] = owned[i].localRotation;
            written = true;
            pinchPoint = wrist.position + wrist.rotation * pinchInWrist;
            return true;
        }

        /// <summary>Gives every touched bone back to the body presenter's sampled pose.</summary>
        public void Release()
        {
            if (acquired)
                for (int i = 0; i < owned.Length; i++) if (owned[i]) owned[i].localRotation = baseline[i];
            acquired = false; written = false;
            if (subscribed && body) body.PoseWillReset -= OnPoseWillReset;
            subscribed = false;
        }

        public void Dispose() => Release();

        private void OnPoseWillReset()
        {
            // Synchronous: the presenter is about to re-sample. Restore and re-acquire after it (next LateUpdate).
            if (acquired) for (int i = 0; i < owned.Length; i++) if (owned[i]) owned[i].localRotation = baseline[i];
            acquired = false; written = false;
        }

        private bool Acquire()
        {
            if (!Resolve()) return false;
            for (int i = 0; i < owned.Length; i++) baseline[i] = owned[i].localRotation;
            if (!BuildPinch()) return false;
            written = false;
            if (!subscribed) { body.PoseWillReset += OnPoseWillReset; subscribed = true; }
            acquired = true;
            return true;
        }

        private bool Resolve()
        {
            upper = body.GetVisibleBone(UpperArm);
            fore = body.GetVisibleBone(ForeArm);
            wrist = body.GetVisibleBone(Wrist);
            if (!upper || !fore || !wrist || !wrist.IsChildOf(fore) || !fore.IsChildOf(upper)) return false;
            var chains = new Transform[5][];
            for (int f = 0; f < 5; f++)
            {
                chains[f] = new Transform[3];
                for (int j = 0; j < 3; j++)
                {
                    string name = "finger" + (f + 1) + "-" + (j + 1) + ".L";
                    Transform bone = body.GetVisibleBone(name) ?? Find(wrist, name);
                    if (!bone || !bone.IsChildOf(wrist)) return false;
                    chains[f][j] = bone;
                }
            }
            fingers = chains;
            owned = new Transform[3 + 15];
            owned[0] = upper; owned[1] = fore; owned[2] = wrist;
            for (int f = 0; f < 5; f++) for (int j = 0; j < 3; j++) owned[3 + f * 3 + j] = fingers[f][j];
            baseline = new Quaternion[owned.Length];
            lastWritten = new Quaternion[owned.Length];
            closed = new Quaternion[owned.Length];
            tipInJoint = new Vector3[5];
            return true;
        }

        /// <summary>
        /// Builds the closed pinch once from the sampled pose: the hand frame, the finger rotations and where the pinch
        /// is relative to the wrist. The fingers are given back to the sampled pose before it returns.
        /// </summary>
        private bool BuildPinch()
        {
            // Anatomical hand frame as for the carried crate: wrist -> middle-finger base, index -> little finger across the palm.
            Vector3 axis = fingers[2][0].position - wrist.position;
            float handLength = axis.magnitude;
            Vector3 across = fingers[1][0].position - fingers[4][0].position;
            if (handLength < .02f || across.sqrMagnitude < 1e-6f) return false;
            axis /= handLength;
            Vector3 palm = -Vector3.Cross(axis, across.normalized);
            if (palm.sqrMagnitude < 1e-6f) return false;
            palm.Normalize();
            handInWrist = Quaternion.Inverse(wrist.rotation) * Quaternion.LookRotation(axis, palm);

            // The last finger segment has no end bone: take it as 0.8 of the middle segment, straight on in the sampled pose.
            for (int f = 0; f < 5; f++)
                tipInJoint[f] = Quaternion.Inverse(fingers[f][2].rotation) * ((fingers[f][2].position - fingers[f][1].position) * .8f);

            for (int f = 1; f < 5; f++) Curl(fingers[f], palm, f == 1 ? IndexCurl : OtherCurl);
            Curl(fingers[0], palm, ThumbCurl);

            // Bring the thumb's end to the index pad: a few damped passes over its three joints, each within a limit.
            Transform[] thumb = fingers[0];
            var start = new Quaternion[3];
            for (int j = 0; j < 3; j++) start[j] = thumb[j].localRotation;
            Vector3 indexTip = Tip(1);
            for (int pass = 0; pass < 10; pass++)
            {
                for (int j = 2; j >= 0; j--)
                {
                    Vector3 tip = Tip(0);
                    Vector3 apart = tip - indexTip;
                    if (apart.magnitude <= PadGap + .0005f) break;
                    Vector3 goal = indexTip + apart.normalized * PadGap;
                    Vector3 from = tip - thumb[j].position, to = goal - thumb[j].position;
                    if (from.sqrMagnitude < 1e-8f || to.sqrMagnitude < 1e-8f) continue;
                    thumb[j].rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(from, to), .6f) * thumb[j].rotation;
                    float angle = Quaternion.Angle(start[j], thumb[j].localRotation);
                    if (angle > ThumbLimitDegrees)
                        thumb[j].localRotation = Quaternion.Slerp(start[j], thumb[j].localRotation, ThumbLimitDegrees / angle);
                }
            }

            Vector3 thumbTip = Tip(0);
            PinchGapMetres = Mathf.Max(0f, Vector3.Distance(thumbTip, indexTip) - PadGap);
            pinchInWrist = Quaternion.Inverse(wrist.rotation) * ((thumbTip + indexTip) * .5f - wrist.position);
            for (int i = 3; i < owned.Length; i++)
            {
                closed[i] = owned[i].localRotation;
                owned[i].localRotation = baseline[i];
            }
            return true;
        }

        private Vector3 Tip(int finger) => fingers[finger][2].position + fingers[finger][2].rotation * tipInJoint[finger];

        /// <summary>Flexes a finger towards the palm about its own curl axis, proximal to distal.</summary>
        private static void Curl(Transform[] chain, Vector3 palm, float[] degrees)
        {
            Vector3 along = chain[1].position - chain[0].position;
            Vector3 axis = Vector3.Cross(along, palm);
            if (axis.sqrMagnitude < 1e-8f) return;
            axis.Normalize();
            float sign = Vector3.Dot(Quaternion.AngleAxis(10f, axis) * along, palm) > Vector3.Dot(along, palm) ? 1f : -1f;
            for (int j = 0; j < 3; j++) chain[j].rotation = Quaternion.AngleAxis(sign * degrees[j], axis) * chain[j].rotation;
        }

        /// <summary>
        /// Two-bone reach with the elbow kept low and out to the left, then the wrist set to its exact rotation with half
        /// of its roll about the forearm moved into the forearm, so the wrist skin does not wrap.
        /// </summary>
        private void SolveArm(Vector3 target, Quaternion wristWorld, Quaternion eye)
        {
            Vector3 s = upper.position, e = fore.position, w = wrist.position;
            float l1 = (e - s).magnitude, l2 = (w - e).magnitude;
            if (l1 < 1e-4f || l2 < 1e-4f) return;
            Vector3 toTarget = target - s;
            float requested = toTarget.magnitude;
            float d = Mathf.Clamp(requested, Mathf.Abs(l1 - l2) + .002f, l1 + l2 - .002f);
            LastReachShortfallMetres = Mathf.Max(0f, requested - d);
            Vector3 dir = requested > 1e-5f ? toTarget / requested : eye * Vector3.forward;
            Vector3 pole = eye * new Vector3(-.35f, -.85f, -.25f);
            Vector3 bend = pole - Vector3.Dot(pole, dir) * dir;
            if (bend.sqrMagnitude < 1e-6f) bend = eye * Vector3.down - Vector3.Dot(eye * Vector3.down, dir) * dir;
            bend.Normalize();
            float a = (l1 * l1 - l2 * l2 + d * d) / (2f * d);
            float h = Mathf.Sqrt(Mathf.Max(0f, l1 * l1 - a * a));
            Vector3 elbow = s + dir * a + bend * h;
            upper.rotation = Quaternion.FromToRotation(e - s, elbow - s) * upper.rotation;
            Vector3 e2 = fore.position, w2 = wrist.position;
            fore.rotation = Quaternion.FromToRotation(w2 - e2, (s + dir * d) - e2) * fore.rotation;

            Vector3 axis = (wrist.position - fore.position).normalized;
            float twist = TwistAngle(wristWorld * Quaternion.Inverse(wrist.rotation), axis);
            if (!float.IsNaN(twist)) fore.rotation = Quaternion.AngleAxis(twist * .5f, axis) * fore.rotation;
            wrist.rotation = wristWorld;
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

        private static Transform Find(Transform root, string name)
        {
            if (!root) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = Find(root.GetChild(i), name);
                if (found) return found;
            }
            return null;
        }
    }
}
