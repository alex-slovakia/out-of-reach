using System;
using System.Collections.Generic;
using UnityEngine;

namespace OutOfReach.Presentation.Hands
{
    /// <summary>One selected arm's finger lease and actual baked cylinder clearance. No Update, input or physics command.</summary>
    internal sealed class BareRailGripLease : IDisposable
    {
        internal readonly BareArmContactRig Rig;
        internal readonly BareRailGripPose.HandPose Pose;
        readonly Quaternion[] baseline = new Quaternion[15], previous = new Quaternion[15];
        readonly Mesh bake;
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly int[] region;
        readonly float[] gaps = new float[6];
        internal float MinimumClearance { get; private set; }
        internal float MaximumRegionGap { get; private set; }
        internal float AxisError { get; private set; }
        internal float AxisAlignment { get; private set; }
        internal int VertexCount => region.Length;
        internal float RegionGap(int index) => gaps[index];
        internal BareRailGripLease(BareArmContactRig rig, BareRailGripPose.HandPose pose)
        {
            if (rig == null || !rig.IsUsable || pose == null || !pose.IsValid || !rig.Renderer.sharedMesh
                || !rig.Renderer.sharedMesh.isReadable) throw new ArgumentException("Exact readable WORK skin and imported grip pose required.");
            Rig = rig; Pose = pose;
            for (int i = 0; i < 15; i++) baseline[i] = rig.Fingers[i].localRotation;
            Mesh source = rig.Renderer.sharedMesh; BoneWeight[] weights = source.boneWeights;
            if (weights.Length != source.vertexCount) throw new ArgumentException("Readable original skin weights required.");
            region = new int[weights.Length]; Transform[] bones = rig.Renderer.bones;
            for (int i = 0; i < region.Length; i++)
            {
                BoneWeight w = weights[i]; int b = w.boneIndex0; float best = w.weight0;
                if (w.weight1 > best) { best = w.weight1; b = w.boneIndex1; }
                if (w.weight2 > best) { best = w.weight2; b = w.boneIndex2; }
                if (w.weight3 > best) b = w.boneIndex3;
                string name = b >= 0 && b < bones.Length && bones[b] ? bones[b].name : string.Empty;
                region[i] = name.StartsWith("wrist.", StringComparison.Ordinal) || name.StartsWith("metacarpal", StringComparison.Ordinal) ? 0 : -1;
                for (int digit = 1; digit <= 5; digit++)
                    if (name.StartsWith("finger" + digit + "-", StringComparison.Ordinal)) region[i] = digit;
            }
            bake = new Mesh { name = "WORK rail grip - actual skin measurement" };
        }
        internal void CapturePrevious() { for (int i = 0; i < 15; i++) previous[i] = Rig.Fingers[i].localRotation; }
        internal void RestorePrevious() { for (int i = 0; i < 15; i++) Rig.Fingers[i].localRotation = previous[i]; }
        internal void Restore() { for (int i = 0; i < 15; i++) if (Rig.Fingers[i]) Rig.Fingers[i].localRotation = baseline[i]; }
        internal void Apply(float armBlend, float close)
        { for (int i = 0; i < 15; i++) Rig.Fingers[i].localRotation = Quaternion.Slerp(baseline[i], Pose.Sample(i, close), armBlend); }
        internal bool Measure(HandContactSample sample)
        {
            if (!sample.Surface || sample.Kind != HandContactKind.Grip || Mathf.Abs(sample.GripRadiusMetres - .015f) > .00001f) return false;
            CapsuleCollider rail = sample.Surface.GetComponent<CapsuleCollider>();
            if (!rail || !rail.enabled || rail.isTrigger || !rail.gameObject.activeInHierarchy) return false;
            Vector3 localAxis = rail.direction == 0 ? Vector3.right : rail.direction == 1 ? Vector3.up : Vector3.forward;
            Vector3 axisVector = rail.transform.TransformVector(localAxis), axis = axisVector.normalized;
            Vector3 scale = rail.transform.lossyScale;
            float transverse = rail.direction == 0 ? Mathf.Abs(scale.y) : Mathf.Abs(scale.x);
            float other = rail.direction == 2 ? Mathf.Abs(scale.y) : Mathf.Abs(scale.z);
            float radius = rail.radius * transverse;
            if (Mathf.Abs(radius - .015f) > .00001f || Mathf.Abs(transverse - other) > .00001f || axisVector.sqrMagnitude < .000001f) return false;
            float half = Mathf.Max(0, rail.height * axisVector.magnitude * .5f - radius);
            Vector3 centre = rail.transform.TransformPoint(rail.center);
            Rig.Renderer.BakeMesh(bake); bake.GetVertices(vertices);
            if (vertices.Count != region.Length) return false;
            for (int i = 0; i < gaps.Length; i++) gaps[i] = float.PositiveInfinity;
            MinimumClearance = float.PositiveInfinity;
            Matrix4x4 matrix = Rig.Renderer.transform.localToWorldMatrix;
            // Every actual arm vertex, including sleeve/wrist transition: not a hand-weight-only exclusion.
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 world = matrix.MultiplyPoint3x4(vertices[i]);
                Vector3 closest = centre + axis * Mathf.Clamp(Vector3.Dot(world - centre, axis), -half, half);
                float gap = Vector3.Distance(world, closest) - radius;
                if (!BareRailGripPose.Finite(gap)) return false;
                MinimumClearance = Mathf.Min(MinimumClearance, gap);
                if (region[i] >= 0) gaps[region[i]] = Mathf.Min(gaps[region[i]], gap);
            }
            MaximumRegionGap = 0;
            foreach (float gap in gaps)
            { if (!BareRailGripPose.Finite(gap)) return false; MaximumRegionGap = Mathf.Max(MaximumRegionGap, gap); }
            Vector3 actualCentre = Rig.Wrist.TransformPoint(Pose.railPoint);
            Vector3 targetCentre = sample.Point - sample.Normal * sample.GripRadiusMetres;
            AxisError = Vector3.Distance(actualCentre, targetCentre);
            AxisAlignment = Mathf.Abs(Vector3.Dot(Rig.Wrist.TransformDirection(Pose.railAxis).normalized, sample.Tangent));
            return BareRailGripPose.Finite(AxisError) && BareRailGripPose.Finite(AxisAlignment);
        }
        public void Dispose() { if (bake) UnityEngine.Object.Destroy(bake); }
    }
}
