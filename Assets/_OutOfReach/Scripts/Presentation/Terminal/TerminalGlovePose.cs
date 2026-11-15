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
        private Transform root, rightArm, leftArm, leftHand, rightHand;
        private TerminalReachFrame reach;
        private Camera view;
        public Vector3 SolvedElbow { get; private set; }
        public float WristAngleDegrees { get; private set; }
        public bool IsActive => snapshot != null && root && rightArm && rightHand;
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
                || !joints.TryGetValue("L_Hand", out leftHand) || !joints.TryGetValue("R_Hand",out rightHand) || joints.Count != 34) return false;
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

        public bool Begin(TerminalReachFrame actualReach, Camera camera)
        {
            if (!root || !rightArm || !leftArm || !leftHand || !rightHand || !actualReach.IsValid || !camera
                || rightHand.localPosition.sqrMagnitude>.0000001f) return false;
            reach=actualReach; view=camera;
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
            // No unverified physical left brace: this session uses the measured right touch hand only.
            leftArm.gameObject.SetActive(false);
            root.gameObject.SetActive(true);
            return true;
        }

        public bool Place(Vector3 fingertip, Quaternion displayRotation, bool apply = true)
        {
            if (!IsActive || !reach.IsValid || !view) return false;
            // Original Press fingers and hand surface stay unchanged. Only arm/wrist rotations change.
            Quaternion hand = displayRotation * Quaternion.Euler(0,-20,0) * Quaternion.Euler(-40,0,0)
                * Quaternion.Euler(0,0,-8) * press["R_Hand"];
            Vector3 tipInHand = Quaternion.Inverse(press["R_Hand"]) * new Vector3(-.0089f,-.0690f,.1864f);
            Vector3 wrist = fingertip-hand*tipInHand;
            Vector3 delta=wrist-reach.RightShoulder;
            float distance=delta.magnitude, upper=reach.UpperLength, lower=reach.LowerLength;
            if(distance>upper+lower-.002f || distance<Mathf.Abs(upper-lower)+.005f) return false;
            Vector3 axis=delta/distance;
            float along=(upper*upper-lower*lower+distance*distance)/(2*distance);
            float height=Mathf.Sqrt(Mathf.Max(0,upper*upper-along*along));
            Vector3 down=Vector3.ProjectOnPlane(Vector3.down,axis).normalized;
            if(down.sqrMagnitude<.9f) return false;
            Vector3 across=Vector3.Cross(axis,down);
            float best=float.PositiveInfinity,bestWrist=0; Vector3 bestElbow=default,bestForward=default;
            for(int i=0;i<72;i++)
            {
                float angle=i*5*Mathf.Deg2Rad;
                Vector3 elbow=reach.RightShoulder+axis*along+height*(Mathf.Cos(angle)*down+Mathf.Sin(angle)*across);
                Vector3 forward=(wrist-elbow).normalized;
                float bend=Vector3.Angle(forward,hand*Vector3.forward);
                if(bend>80 || Vector3.Dot(elbow-reach.ShoulderCentre,reach.BodyRight)<.060f
                    || Vector3.Dot(elbow-reach.RightShoulder,reach.BodyForward)<.080f
                    || elbow.y>wrist.y-.045f) continue;
                // The original 320 mm garment extends beyond the shorter anatomical elbow distance.
                // Check its entire axis against the actual eye; never shrink or stretch the source sleeve.
                Vector3 rear=wrist-forward*.320f;
                if(SegmentDistance(view.transform.position,rear,wrist)<.115f) continue;
                float cost=bend+200*Mathf.Max(0,elbow.y-reach.RightShoulder.y);
                if(cost>=best) continue;
                best=cost; bestElbow=elbow; bestForward=forward; bestWrist=bend;
            }
            if(float.IsPositiveInfinity(best)) return false;
            if(!apply) return true;
            Quaternion arm=Quaternion.FromToRotation(hand*Vector3.forward,bestForward)*hand;
            rightArm.SetPositionAndRotation(wrist,arm);
            rightHand.localRotation=Quaternion.Inverse(arm)*hand;
            SolvedElbow=bestElbow; WristAngleDegrees=bestWrist;
            return true;
        }
        private static float SegmentDistance(Vector3 p,Vector3 a,Vector3 b)
        {
            Vector3 d=b-a;
            return Vector3.Distance(p,a+d*Mathf.Clamp01(Vector3.Dot(p-a,d)/d.sqrMagnitude));
        }

        public void Restore()
        {
            var saved = snapshot; snapshot = null; view=null; reach=default;
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
