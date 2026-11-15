using System;
using OutOfReach.Presentation.Motion;
using UnityEngine;

namespace OutOfReach.Presentation.Meal
{
    /// <summary>
    /// The measured WORK-HAND-CLASP-01 edge clasp for the meal pouch, right hand (feature/work-hand-clasp-v1@7e9ee5c,
    /// <c>WORK_Clasp_Meal_R</c>): finger-local model-space deltas at bind, the pouch frame relative to the wrist, and the
    /// joint head targets for the 2 mm check. Values are in the Unity model basis of the 164-bone WORK rig, in metres.
    /// </summary>
    [Serializable]
    public sealed class MealClaspData
    {
        [Serializable]
        public sealed class Bone
        {
            public string name;
            /// <summary>Model-space rotation delta at bind, xyzw.</summary>
            public float[] delta;
            /// <summary>Joint head target from the wrist head at bind, model basis, metres.</summary>
            public float[] head;
        }

        public string pose;
        public string source;
        public string armature;
        /// <summary>Pouch prop origin from the wrist head at bind, model basis, metres.</summary>
        public float[] pouchOrigin;
        /// <summary>Pouch prop axes at bind in the model basis, xyzw.</summary>
        public float[] pouchRotation;
        /// <summary>Bones in parent-before-child order.</summary>
        public Bone[] bones = Array.Empty<Bone>();

        public bool IsValid
        {
            get
            {
                if (!Vector(pouchOrigin, 3) || !Vector(pouchRotation, 4) || bones == null || bones.Length == 0) return false;
                foreach (Bone bone in bones)
                    if (bone == null || string.IsNullOrEmpty(bone.name) || !Vector(bone.delta, 4) || !Vector(bone.head, 3)) return false;
                return true;
            }
        }

        public static MealClaspData Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            MealClaspData data = JsonUtility.FromJson<MealClaspData>(json);
            return data != null && data.IsValid ? data : null;
        }

        internal static Vector3 V(float[] v) => new Vector3(v[0], v[1], v[2]);
        internal static Quaternion Q(float[] q)
        {
            var r = new Quaternion(q[0], q[1], q[2], q[3]);
            float m = Mathf.Sqrt(r.x * r.x + r.y * r.y + r.z * r.z + r.w * r.w);
            return m < 1e-6f ? Quaternion.identity : new Quaternion(r.x / m, r.y / m, r.z / m, r.w / m);
        }

        private static bool Vector(float[] v, int length)
        {
            if (v == null || v.Length != length) return false;
            foreach (float f in v) if (float.IsNaN(f) || float.IsInfinity(f)) return false;
            return true;
        }
    }

    /// <summary>
    /// A presentation lease on the visible WORK body's right arm while it holds the meal pouch: a two-bone reach of the
    /// existing upper arm and forearm, half the wrist roll moved into the forearm, and the measured finger clasp. It writes
    /// rotations only, after the body presenter (execution order 260 &gt; 200/250), restores every bone it touched when the
    /// meal ends, and gives the arm back at once on <see cref="EmbodiedBodyPresenter.PoseWillReset"/> (re-sampling,
    /// pause, outfit change). Never in the EVA suit; nothing in it changes movement, input, physics or domain state.
    /// </summary>
    internal sealed class MealHandClasp : IDisposable
    {
        private const string UpperArm = "upperarm01.R", ForeArm = "lowerarm01.R", Wrist = "wrist.R";
        private readonly EmbodiedBodyPresenter body;
        private readonly MealClaspData data;
        private Transform modelRoot, upper, fore, wrist;
        private Transform[] fingers = Array.Empty<Transform>();
        private Quaternion[] fingerDelta = Array.Empty<Quaternion>(), fingerBind = Array.Empty<Quaternion>();
        private Vector3[] fingerHead = Array.Empty<Vector3>();
        private Quaternion wristBind, pouchBind;
        private Vector3 pouchOrigin;
        private Quaternion[] baseline = Array.Empty<Quaternion>(), lastWritten = Array.Empty<Quaternion>();
        private Transform[] owned = Array.Empty<Transform>();
        private bool acquired, subscribed, written;

        public MealHandClasp(EmbodiedBodyPresenter visibleBody, MealClaspData clasp)
        {
            body = visibleBody;
            data = clasp;
        }

        public bool IsHolding => acquired;

        /// <summary>Largest distance of a clasped joint head from its measured target at the current wrist (diagnostic).</summary>
        public float LastHeadErrorMetres { get; private set; } = float.NaN;

        /// <summary>Distance of the arm's actual reach from the requested wrist target (0 when reachable).</summary>
        public float LastReachShortfallMetres { get; private set; } = float.NaN;

        /// <summary>The visible outfit is the bare-handed WORK body with its right arm shown.</summary>
        public bool CanHold => body && data != null && body.IsBound && body.IsVisible && !body.IsEvaVisible
            && (body.VisibleArms & EmbodiedBodyPresenter.BodyArmVisibility.Right) != 0;

        /// <summary>
        /// Holds the pouch at <paramref name="pouch"/> with arm weight <paramref name="armWeight"/> and clasp closure
        /// <paramref name="clasp"/>. Returns the pouch pose the actual hand now carries (equal to the request when reachable).
        /// </summary>
        public bool TryHold(Pose pouch, float armWeight, float clasp, Quaternion eyeRotation, out Pose held)
        {
            held = pouch;
            if (!CanHold) { Release(); return false; }
            if (!acquired && !Acquire()) return false;
            if (!upper || !fore || !wrist || !modelRoot) { Release(); return false; }

            // Stateless per frame: start from the pose the body presenter sampled, never from last frame's writes. A writer
            // that poses these bones every frame (the standing gait) has replaced our last values: take its pose as the rest.
            for (int i = 0; i < owned.Length; i++)
            {
                if (written && owned[i].localRotation != lastWritten[i]) baseline[i] = owned[i].localRotation;
                owned[i].localRotation = baseline[i];
            }

            // Full-weight solve: the wrist pose that puts the measured clasp on the requested pouch frame.
            Quaternion wristWorld = pouch.rotation * Quaternion.Inverse(pouchBind) * wristBind;
            Vector3 wristTarget = pouch.position - wristWorld * Quaternion.Inverse(wristBind) * pouchOrigin;
            SolveArm(wristTarget, wristWorld, eyeRotation);
            Quaternion fullWrist = wrist.rotation;
            for (int i = 0; i < fingers.Length; i++)
            {
                Quaternion solved = fullWrist * Quaternion.Inverse(wristBind) * fingerDelta[i] * fingerBind[i];
                Quaternion rest = fingers[i].localRotation;
                fingers[i].rotation = solved;
                fingers[i].localRotation = Quaternion.Slerp(rest, fingers[i].localRotation, Mathf.Clamp01(clasp));
            }
            MeasureHeads(fullWrist);

            // Blend the arm chain from the sampled pose (parent first) so reach-in and release never pop.
            float w = Mathf.Clamp01(armWeight);
            if (w < .999f)
            {
                for (int i = 0; i < 3; i++)
                {
                    Quaternion solvedLocal = owned[i].localRotation;
                    owned[i].localRotation = Quaternion.Slerp(baseline[i], solvedLocal, w);
                }
            }

            for (int i = 0; i < owned.Length; i++) lastWritten[i] = owned[i].localRotation;
            written = true;
            Quaternion actualWrist = wrist.rotation;
            held = new Pose(wrist.position + actualWrist * Quaternion.Inverse(wristBind) * pouchOrigin,
                actualWrist * Quaternion.Inverse(wristBind) * pouchBind);
            return true;
        }

        /// <summary>Gives every touched bone back to the body presenter's sampled pose.</summary>
        public void Release()
        {
            if (acquired)
                for (int i = 0; i < owned.Length; i++) if (owned[i]) owned[i].localRotation = baseline[i];
            acquired = false;
            if (subscribed && body) body.PoseWillReset -= OnPoseWillReset;
            subscribed = false;
        }

        public void Dispose() => Release();

        private void OnPoseWillReset()
        {
            // Synchronous: the presenter is about to re-sample. Restore and re-acquire after it (next LateUpdate).
            if (acquired) for (int i = 0; i < owned.Length; i++) if (owned[i]) owned[i].localRotation = baseline[i];
            acquired = false;
        }

        private bool Acquire()
        {
            if (!Resolve()) return false;
            for (int i = 0; i < owned.Length; i++) baseline[i] = owned[i].localRotation;
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
            modelRoot = null;
            for (Transform t = wrist; t; t = t.parent)
                if (t.name == (string.IsNullOrEmpty(data.armature) ? "A7_ContinuousBody_Rig" : data.armature)) { modelRoot = t.parent; break; }
            SkinnedMeshRenderer arm = body.WorkRightArmRenderer;
            if (!modelRoot || !arm || !arm.sharedMesh) return false;
            if (!TryBind(arm, wrist, out wristBind)) return false;

            var foundFingers = new Transform[data.bones.Length];
            var deltas = new Quaternion[data.bones.Length];
            var binds = new Quaternion[data.bones.Length];
            var heads = new Vector3[data.bones.Length];
            for (int i = 0; i < data.bones.Length; i++)
            {
                Transform bone = body.GetVisibleBone(data.bones[i].name) ?? Find(wrist, data.bones[i].name);
                if (!bone || !bone.IsChildOf(wrist) || !TryBind(arm, bone, out binds[i])) return false;
                foundFingers[i] = bone;
                deltas[i] = MealClaspData.Q(data.bones[i].delta);
                heads[i] = MealClaspData.V(data.bones[i].head);
            }

            fingers = foundFingers; fingerDelta = deltas; fingerBind = binds; fingerHead = heads;
            pouchOrigin = MealClaspData.V(data.pouchOrigin);
            pouchBind = MealClaspData.Q(data.pouchRotation);
            // Model-basis values become world values through the model root's rotation (the rig is shown at real size).
            Quaternion model = modelRoot.rotation;
            wristBind = model * wristBind;
            pouchBind = model * pouchBind;
            pouchOrigin = model * pouchOrigin;
            for (int i = 0; i < fingers.Length; i++) { fingerDelta[i] = model * fingerDelta[i] * Quaternion.Inverse(model); fingerBind[i] = model * fingerBind[i]; fingerHead[i] = model * fingerHead[i]; }

            owned = new Transform[3 + fingers.Length];
            owned[0] = upper; owned[1] = fore; owned[2] = wrist;
            Array.Copy(fingers, 0, owned, 3, fingers.Length);
            baseline = new Quaternion[owned.Length];
            lastWritten = new Quaternion[owned.Length];
            return true;
        }

        /// <summary>
        /// Two-bone reach with the elbow kept low and out, then the wrist set to its exact clasp rotation with half of its
        /// roll about the forearm moved into the forearm, so the wrist skin does not wrap.
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
            Vector3 pole = eye * new Vector3(.35f, -.85f, -.25f);
            Vector3 bend = pole - Vector3.Dot(pole, dir) * dir;
            if (bend.sqrMagnitude < 1e-6f) bend = eye * Vector3.down - Vector3.Dot(eye * Vector3.down, dir) * dir;
            bend.Normalize();
            float a = (l1 * l1 - l2 * l2 + d * d) / (2f * d);
            float h = Mathf.Sqrt(Mathf.Max(0f, l1 * l1 - a * a));
            Vector3 elbow = s + dir * a + bend * h;
            upper.rotation = Quaternion.FromToRotation(e - s, elbow - s) * upper.rotation;
            Vector3 e2 = fore.position, w2 = wrist.position;
            fore.rotation = Quaternion.FromToRotation(w2 - e2, (s + dir * d) - e2) * fore.rotation;

            // Move half of the wrist's roll about the forearm axis into the forearm (pronation), then pin the wrist.
            Vector3 axis = (wrist.position - fore.position).normalized;
            Quaternion delta = wristWorld * Quaternion.Inverse(wrist.rotation);
            float twist = TwistAngle(delta, axis);
            if (!float.IsNaN(twist)) fore.rotation = Quaternion.AngleAxis(twist * .5f, axis) * fore.rotation;
            wrist.rotation = wristWorld;
        }

        private void MeasureHeads(Quaternion wristWorld)
        {
            float worst = 0f;
            Quaternion fromBind = wristWorld * Quaternion.Inverse(wristBind);
            for (int i = 0; i < fingers.Length; i++)
            {
                Vector3 expected = wrist.position + fromBind * fingerHead[i];
                worst = Mathf.Max(worst, Vector3.Distance(expected, fingers[i].position));
            }
            LastHeadErrorMetres = worst;
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

        private static bool TryBind(SkinnedMeshRenderer renderer, Transform bone, out Quaternion modelRotation)
        {
            modelRotation = Quaternion.identity;
            Transform[] bones = renderer.bones;
            Matrix4x4[] binds = renderer.sharedMesh.bindposes;
            if (bones == null || binds == null || binds.Length != bones.Length) return false;
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] != bone) continue;
                Matrix4x4 boneInRenderer = binds[i].inverse;
                Vector3 forward = boneInRenderer.GetColumn(2), up = boneInRenderer.GetColumn(1);
                if (forward.sqrMagnitude < 1e-10f || up.sqrMagnitude < 1e-10f) return false;
                Quaternion inRenderer = Quaternion.LookRotation(forward, up);
                Transform root = FindModelRoot(bone);
                if (!root) return false;
                modelRotation = Quaternion.Inverse(root.rotation) * renderer.transform.rotation * inRenderer;
                return true;
            }
            return false;
        }

        private static Transform FindModelRoot(Transform bone)
        {
            for (Transform t = bone; t; t = t.parent)
                if (t.name == "A7_ContinuousBody_Rig") return t.parent;
            return null;
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
