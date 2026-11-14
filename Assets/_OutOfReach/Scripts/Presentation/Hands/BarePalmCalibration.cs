using System;
using UnityEngine;

namespace OutOfReach.Presentation.Hands
{
    /// <summary>Existing WORK skeleton references. This is not another rig, renderer or wardrobe owner.</summary>
    public sealed class BareArmContactRig
    {
        public readonly Transform Upper, Lower, Wrist, Index, Middle, Little, Thumb;
        public readonly SkinnedMeshRenderer Renderer;
        public BareArmContactRig(Transform upper, Transform lower, Transform wrist, Transform index,
            Transform middle, Transform little, Transform thumb, SkinnedMeshRenderer renderer)
        {
            Upper = upper; Lower = lower; Wrist = wrist; Index = index; Middle = middle;
            Little = little; Thumb = thumb; Renderer = renderer;
        }
        internal bool IsUsable => Upper && Lower && Wrist && Index && Middle && Little && Thumb && Renderer
            && Lower.IsChildOf(Upper) && Wrist.IsChildOf(Lower) && Index.IsChildOf(Wrist)
            && Middle.IsChildOf(Wrist) && Little.IsChildOf(Wrist) && Thumb.IsChildOf(Wrist)
            && (Wrist.lossyScale - Vector3.one).sqrMagnitude < .000001f;
    }

    internal static class BarePalmCalibration
    {
        // Measures the IMPORTED skin, preserving FBX PreRotation axes. Does not apply source JSON matrices.
        internal static bool TryMeasure(BareArmContactRig rig, out Vector3 localPoint, out Quaternion localFrame)
        {
            localPoint = default; localFrame = Quaternion.identity;
            if (rig == null || !rig.IsUsable || !rig.Renderer.sharedMesh || !rig.Renderer.sharedMesh.isReadable) return false;
            Vector3 wrist = rig.Wrist.position;
            Vector3 forward = (rig.Middle.position - wrist).normalized;
            Vector3 normal = Vector3.Cross(forward, rig.Index.position - rig.Little.position).normalized;
            float thumbSide = Vector3.Dot(normal, rig.Thumb.position - wrist);
            if (Mathf.Abs(thumbSide) < .004f || normal.sqrMagnitude < .99f) return false;
            if (thumbSide < 0f) normal = -normal;
            Vector3 across = Vector3.Cross(forward, normal).normalized;
            var baked = new Mesh();
            try
            {
                rig.Renderer.BakeMesh(baked);
                Vector3[] vertices = baked.vertices;
                int[] triangles = baked.triangles;
                BoneWeight[] weights = rig.Renderer.sharedMesh.boneWeights;
                Transform[] bones = rig.Renderer.bones;
                if (vertices.Length != weights.Length) return false;
                var handBone = new bool[bones.Length];
                for (int i = 0; i < bones.Length; i++)
                    handBone[i] = bones[i] && (bones[i] == rig.Wrist || bones[i].IsChildOf(rig.Wrist));
                var handVertex = new bool[vertices.Length];
                for (int i = 0; i < vertices.Length; i++)
                {
                    BoneWeight w = weights[i];
                    handVertex[i] = Weight(handBone, w.boneIndex0, w.weight0) + Weight(handBone, w.boneIndex1, w.weight1)
                        + Weight(handBone, w.boneIndex2, w.weight2) + Weight(handBone, w.boneIndex3, w.weight3) > .999f;
                }
                float best = float.PositiveInfinity;
                Vector3 point = default, faceNormal = default;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int ia = triangles[i], ib = triangles[i + 1], ic = triangles[i + 2];
                    if (!handVertex[ia] || !handVertex[ib] || !handVertex[ic]) continue;
                    Vector3 a = rig.Renderer.transform.TransformPoint(vertices[ia]);
                    Vector3 b = rig.Renderer.transform.TransformPoint(vertices[ib]);
                    Vector3 c = rig.Renderer.transform.TransformPoint(vertices[ic]);
                    Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                    Vector3 p = (a + b + c) / 3f, delta = p - wrist;
                    float ahead = Vector3.Dot(delta, forward), side = Vector3.Dot(delta, across);
                    if (ahead < .035f || ahead > .075f || Mathf.Abs(side) > .018f || Vector3.Dot(n, normal) < .65f) continue;
                    float score = side * side + (ahead - .055f) * (ahead - .055f);
                    if (score >= best) continue;
                    best = score; point = p; faceNormal = n;
                }
                if (float.IsPositiveInfinity(best)) return false;
                localPoint = rig.Wrist.InverseTransformPoint(point);
                localFrame = Quaternion.Inverse(rig.Wrist.rotation)
                    * Quaternion.LookRotation(Vector3.ProjectOnPlane(forward, faceNormal), faceNormal);
                return true;
            }
            catch (Exception error) { Debug.LogException(error); return false; }
            finally { UnityEngine.Object.Destroy(baked); }
        }

        private static float Weight(bool[] hand, int index, float weight)
            => index >= 0 && index < hand.Length && hand[index] ? weight : 0f;
    }
}
