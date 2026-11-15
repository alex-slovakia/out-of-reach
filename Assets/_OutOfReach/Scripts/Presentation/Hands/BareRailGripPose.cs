using System;
using UnityEngine;

namespace OutOfReach.Presentation.Hands
{
    /// <summary>Imported Unity-local finger samples and measured bind-space rail frame. No animation authority.</summary>
    public sealed class BareRailGripPose : ScriptableObject
    {
        public const float RailRadius = .015f, Duration = 20f / 30f;
        public const int Frames = 21, Joints = 15;
        [Serializable] public sealed class HandPose
        {
            public Vector3 railPoint, railAxis, palmNormal;
            public Quaternion[] rotations = new Quaternion[Frames * Joints];
            public bool IsValid
            {
                get
                {
                    if (rotations == null || rotations.Length != Frames * Joints || !Finite(railPoint)
                        || !Finite(railAxis) || !Finite(palmNormal) || railPoint.magnitude > .15f
                        || Mathf.Abs(railAxis.magnitude - 1f) > .001f || Mathf.Abs(palmNormal.magnitude - 1f) > .001f
                        || Mathf.Abs(Vector3.Dot(railAxis, palmNormal)) > .01f) return false;
                    foreach (Quaternion q in rotations)
                        if (!Finite(q.x) || !Finite(q.y) || !Finite(q.z) || !Finite(q.w)
                            || Mathf.Abs(Quaternion.Dot(q, q) - 1f) > .001f) return false;
                    return true;
                }
            }
            public Quaternion Sample(int joint, float progress)
            {
                float frame = Mathf.Clamp01(progress) * (Frames - 1);
                int lower = Mathf.FloorToInt(frame), upper = Mathf.Min(Frames - 1, lower + 1);
                return Quaternion.Slerp(rotations[lower * Joints + joint], rotations[upper * Joints + joint], frame - lower);
            }
        }
        public HandPose left = new HandPose(), right = new HandPose();
        public string sourceLeftSha256, sourceRightSha256, sourceBodySha256;
        public bool IsValid => left != null && right != null && left.IsValid && right.IsValid
            && sourceLeftSha256 == "bb78f64bb81eb29e28a63ea315d52996883f4e455662d0c946f143937183e789"
            && sourceRightSha256 == "aec77cdf575a2d958ab95e0203de6de31d6af9eea80c3d1431349fbffc5e90e2"
            && sourceBodySha256 == "46b3051a592f2e2fe3a0e7579ad281411415a6eb71ad4546c2899e47cf5eec90";
        public HandPose For(ContactHand hand) => hand == ContactHand.Left ? left : right;
        internal static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        internal static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
    }
}
