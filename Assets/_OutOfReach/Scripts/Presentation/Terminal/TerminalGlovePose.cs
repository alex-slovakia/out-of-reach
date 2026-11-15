using System;
using System.Collections.Generic;
using UnityEngine;

namespace OutOfReach.Presentation.Terminal
{
    // Operates only on an explicitly supplied, dedicated instance of AstronautHands.prefab.
    internal sealed class TerminalGlovePose
    {
        // JsonUtility populates these source-rig DTO fields at runtime.
#pragma warning disable CS0649
        [Serializable] private sealed class Rig { public Pose[] poses; }
        [Serializable] private sealed class Pose { public string name; public Rotation[] rotations; }
        [Serializable] private sealed class Rotation { public string name; public float[] rotation; }
#pragma warning restore CS0649
        private readonly Dictionary<string, Transform> joints = new Dictionary<string, Transform>();
        private readonly Dictionary<string, Quaternion> press = new Dictionary<string, Quaternion>();
        private readonly Dictionary<string, Quaternion> grip = new Dictionary<string, Quaternion>();
        private Transform root, rightArm, leftArm, leftHand;
        private Snapshot[] snapshot;
        private bool originalActive;
        private struct Snapshot
        {
            public Transform Target;
            public Vector3 Position, Scale;
            public Quaternion Rotation;
            public bool Active;
        }

        public bool Bind(Transform instance, TextAsset json)
        {
            Restore(); joints.Clear(); press.Clear(); grip.Clear(); root = null;
            if (!instance || !json) return false;
            var scale = instance.lossyScale;
            if ((scale - Vector3.one).sqrMagnitude > .00001f) return false;
            foreach (var component in instance.GetComponentsInChildren<Component>(true))
                if (component is MonoBehaviour || component is Animator || component is Rigidbody ||
                    component is Collider || component is Camera || component is AudioListener) return false;
            foreach (var joint in instance.GetComponentsInChildren<Transform>(true))
            {
                if (joint == instance) continue;
                if (joints.ContainsKey(joint.name)) return false;
                joints.Add(joint.name, joint);
            }
            if (!joints.TryGetValue("R_Arm", out rightArm) || !joints.TryGetValue("L_Arm", out leftArm)
                || !joints.TryGetValue("L_Hand", out leftHand) || joints.Count != 34) return false;
            Rig rig;
            try { rig = JsonUtility.FromJson<Rig>(json.text); }
            catch (Exception) { return false; }
            if (rig == null || rig.poses == null) return false;
            foreach (var pose in rig.poses)
            {
                var target = pose.name == "Press" ? press : pose.name == "Grip" ? grip : null;
                if (target == null || pose.rotations == null) continue;
                foreach (var entry in pose.rotations)
                {
                    if (entry == null || entry.rotation == null || entry.rotation.Length != 4 ||
                        !joints.ContainsKey(entry.name) || target.ContainsKey(entry.name)) return false;
                    var q = new Quaternion(entry.rotation[0], entry.rotation[1], entry.rotation[2], entry.rotation[3]);
                    if (!Finite(q.x) || !Finite(q.y) || !Finite(q.z) || !Finite(q.w) || Quaternion.Dot(q,q) < .5f) return false;
                    target.Add(entry.name, q.normalized);
                }
            }
            if (press.Count != 32 || grip.Count != 32) return false;
            root = instance;
            return true;
        }

        public bool Begin()
        {
            if (!root || !rightArm || !leftArm || !leftHand) return false;
            var transforms = root.GetComponentsInChildren<Transform>(true);
            snapshot = new Snapshot[transforms.Length]; originalActive = root.gameObject.activeSelf;
            for (int i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                snapshot[i] = new Snapshot {Target=t, Position=t.localPosition, Rotation=t.localRotation,
                    Scale=t.localScale, Active=t.gameObject.activeSelf};
            }
            foreach (var entry in press) if (entry.Key.StartsWith("R_", StringComparison.Ordinal)) joints[entry.Key].localRotation = entry.Value;
            foreach (var entry in grip) if (entry.Key.StartsWith("L_", StringComparison.Ordinal)) joints[entry.Key].localRotation = entry.Value;
            root.gameObject.SetActive(true);
            return true;
        }

        public bool Place(Vector3 fingertip, Quaternion orientation, Transform leftGrip)
        {
            if (snapshot == null || !root || !rightArm || !leftArm || !leftHand) return false;
            rightArm.rotation = orientation;
            // Calibrated by the delivered glove's Press pose; wrist + finger meshes are unchanged.
            rightArm.position = fingertip - orientation * new Vector3(-.0089f, -.0690f, .1864f);
            if (leftGrip)
            {
                leftArm.gameObject.SetActive(true);
                var rotation = leftGrip.rotation * Quaternion.Inverse(leftHand.localRotation);
                leftArm.rotation = rotation;
                leftArm.position = leftGrip.position - rotation * new Vector3(-.0149f, -.0635f, .0597f);
            }
            else leftArm.gameObject.SetActive(false);
            return true;
        }

        public void Restore()
        {
            var saved = snapshot; snapshot = null;
            if (saved == null) return;
            foreach (var s in saved)
                if (s.Target)
                {
                    s.Target.localPosition=s.Position; s.Target.localRotation=s.Rotation; s.Target.localScale=s.Scale;
                    s.Target.gameObject.SetActive(s.Active);
                }
            if (root) root.gameObject.SetActive(originalActive);
        }
        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
