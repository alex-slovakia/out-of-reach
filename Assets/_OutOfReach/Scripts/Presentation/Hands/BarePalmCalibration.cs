using System;
using System.Collections.Generic;
using UnityEngine;

namespace OutOfReach.Presentation.Hands
{
    /// <summary>Existing WORK skeleton references. No duplicate rig, geometry or wardrobe ownership.</summary>
    public sealed class BareArmContactRig
    {
        public readonly Transform Upper, Lower, Wrist, Index, Middle, Little, Thumb;
        public readonly SkinnedMeshRenderer Renderer;
        public readonly ContactHand Hand;
        internal readonly int[] SkinSubmeshes;
        internal readonly Transform[] Fingers = new Transform[15];
        public BareArmContactRig(Transform upper, Transform lower, Transform wrist, Transform index,
            Transform middle, Transform little, Transform thumb, SkinnedMeshRenderer renderer)
        {
            Upper = upper; Lower = lower; Wrist = wrist; Index = index; Middle = middle;
            Little = little; Thumb = thumb; Renderer = renderer;
            Hand = wrist && wrist.name.EndsWith(".L", StringComparison.Ordinal) ? ContactHand.Left : ContactHand.Right;
            SkinSubmeshes = LegacySkinSubmeshes(renderer);
            if (!wrist) return;
            string side = wrist.name.EndsWith(".L", StringComparison.Ordinal) ? ".L" : ".R";
            Transform[] nodes = wrist.GetComponentsInChildren<Transform>(true);
            for (int digit = 0; digit < 5; digit++) for (int joint = 0; joint < 3; joint++)
            {
                string name = "finger" + (digit + 1) + "-" + (joint + 1) + side;
                foreach (Transform node in nodes) if (node.name == name) Fingers[digit * 3 + joint] = node;
            }
        }
        /// <summary>Explicit semantic mapping; fingers are thumb through little, proximal through distal.
        /// Null skin membership retains only the legacy material adapter; profiles provide explicit indices.</summary>
        public BareArmContactRig(ContactHand hand, Transform upper, Transform lower, Transform wrist,
            IReadOnlyList<Transform> fingers, SkinnedMeshRenderer renderer, IReadOnlyList<int> skinSubmeshes)
        {
            if (hand != ContactHand.Left && hand != ContactHand.Right)
                throw new ArgumentException("A mapped arm needs an explicit left/right hand.");
            if (fingers == null || fingers.Count != 15)
                throw new ArgumentException("Map all fifteen finger joints in semantic digit/joint order.");
            Hand = hand; Upper = upper; Lower = lower; Wrist = wrist; Renderer = renderer;
            var unique = new HashSet<Transform>();
            for (int i = 0; i < Fingers.Length; i++)
            {
                if (!fingers[i] || !unique.Add(fingers[i]))
                    throw new ArgumentException("Mapped finger joints must be present and distinct.");
                Fingers[i] = fingers[i];
            }
            Thumb = Fingers[0]; Index = Fingers[3]; Middle = Fingers[6]; Little = Fingers[12];
            if (skinSubmeshes == null) SkinSubmeshes = LegacySkinSubmeshes(renderer);
            else
            {
                if (!renderer || !renderer.sharedMesh || skinSubmeshes.Count == 0)
                    throw new ArgumentException("Explicit palm membership needs a mesh and nonempty skin submeshes.");
                SkinSubmeshes = new int[skinSubmeshes.Count];
                var uniqueSubmeshes = new HashSet<int>();
                for (int i = 0; i < SkinSubmeshes.Length; i++)
                {
                    int submesh = skinSubmeshes[i];
                    if (submesh < 0 || submesh >= renderer.sharedMesh.subMeshCount || !uniqueSubmeshes.Add(submesh))
                        throw new ArgumentException("Mapped skin submeshes must be distinct and inside the bound mesh.");
                    SkinSubmeshes[i] = submesh;
                }
            }
        }

        static int[] LegacySkinSubmeshes(SkinnedMeshRenderer renderer)
        {
            var found = new List<int>();
            if (!renderer || !renderer.sharedMesh) return found.ToArray();
            Material[] materials = renderer.sharedMaterials;
            for (int sub = 0; sub < renderer.sharedMesh.subMeshCount; sub++)
                if (sub < materials.Length && materials[sub] && materials[sub].name == "MAT_Cabin_BareHands") found.Add(sub);
            return found.ToArray();
        }

        internal bool IsUsable
        {
            get
            {
                if (!Upper || !Lower || !Wrist || !Index || !Middle || !Little || !Thumb || !Renderer
                    || !Lower.IsChildOf(Upper) || !Wrist.IsChildOf(Lower)
                    || (Wrist.lossyScale - Vector3.one).sqrMagnitude > .000001f) return false;
                if (!Renderer.sharedMesh || SkinSubmeshes == null || SkinSubmeshes.Length == 0) return false;
                foreach (int submesh in SkinSubmeshes)
                    if (submesh < 0 || submesh >= Renderer.sharedMesh.subMeshCount) return false;
                for (int i = 0; i < Fingers.Length; i++)
                    if (!Fingers[i] || !Fingers[i].IsChildOf(Wrist)
                        || (i % 3 != 0 && !Fingers[i].IsChildOf(Fingers[i - 1]))) return false;
                return true;
            }
        }
    }

    /// <summary>One lease's actual skin support and reusable native bake. Disposed on cancellation/return.</summary>
    internal sealed class BarePalmContactPose : IDisposable
    {
        internal readonly SkinnedMeshRenderer Renderer;
        internal readonly Mesh Bake;
        internal readonly List<Vector3> Vertices;
        internal readonly int[] SkinIndices, PatchIndices;
        internal readonly Quaternion[] OpenFingers;
        internal readonly Vector3 LocalPoint;
        internal readonly Quaternion LocalFrame;
        internal readonly int SupportIndex;
        internal BarePalmContactPose(SkinnedMeshRenderer renderer, Mesh bake, List<Vector3> vertices,
            int[] skinIndices, int[] patchIndices, Quaternion[] openFingers, int supportIndex, Vector3 localPoint, Quaternion localFrame)
        {
            Renderer = renderer; Bake = bake; Vertices = vertices; SkinIndices = skinIndices; PatchIndices = patchIndices;
            OpenFingers = openFingers; SupportIndex = supportIndex; LocalPoint = localPoint; LocalFrame = localFrame;
        }
        internal bool Measure(Vector3 point, Vector3 normal, out float clearance, out float residual, out float patchDistance)
        {
            clearance = float.PositiveInfinity; residual = patchDistance = float.NaN;
            if (!Renderer || !Bake || !BarePalmCalibration.Finite(point) || !BarePalmCalibration.Finite(normal)) return false;
            Renderer.BakeMesh(Bake); Bake.GetVertices(Vertices);
            if (SupportIndex >= Vertices.Count) return false;
            Matrix4x4 matrix = Renderer.transform.localToWorldMatrix;
            foreach (int index in SkinIndices)
            {
                if (index >= Vertices.Count) return false;
                float distance = Vector3.Dot(matrix.MultiplyPoint3x4(Vertices[index]) - point, normal);
                if (!BarePalmCalibration.Finite(distance)) return false;
                clearance = Mathf.Min(clearance, distance);
            }
            residual = Vector3.Distance(matrix.MultiplyPoint3x4(Vertices[SupportIndex]), point);
            patchDistance = 0f;
            foreach (int index in PatchIndices)
                patchDistance = Mathf.Max(patchDistance, Mathf.Abs(Vector3.Dot(matrix.MultiplyPoint3x4(Vertices[index]) - point, normal)));
            return BarePalmCalibration.Finite(clearance) && BarePalmCalibration.Finite(residual) && BarePalmCalibration.Finite(patchDistance);
        }
        public void Dispose() { if (Bake) UnityEngine.Object.Destroy(Bake); }
    }

    internal static class BarePalmCalibration
    {
        // Explicit submesh membership covers the entire bare skin, including its forearm/wrist blend. Weight thresholds
        // restrict the rigid support search ONLY, never the final plane-clearance check.
        internal static bool TryMeasure(BareArmContactRig rig, out BarePalmContactPose pose)
        {
            pose = null;
            if (rig == null || !rig.IsUsable || !rig.Renderer.sharedMesh || !rig.Renderer.sharedMesh.isReadable) return false;
            Vector3 wrist = rig.Wrist.position, forward = (rig.Middle.position - wrist).normalized;
            Vector3 normal = Vector3.Cross(forward, rig.Index.position - rig.Little.position).normalized;
            float thumbSide = Vector3.Dot(normal, rig.Thumb.position - wrist);
            if (Mathf.Abs(thumbSide) < .004f || normal.sqrMagnitude < .99f) return false;
            if (thumbSide < 0f) normal = -normal;
            Vector3 across = Vector3.Cross(forward, normal).normalized;
            forward = Vector3.Cross(normal, across).normalized;
            var saved = new Quaternion[10]; var opened = new Quaternion[10];
            for (int digit = 0; digit < 5; digit++) for (int joint = 0; joint < 2; joint++)
                saved[digit * 2 + joint] = rig.Fingers[digit * 3 + joint].localRotation;
            var bake = new Mesh { name = "WORK palm lease - reusable skin measurement" };
            bool retained = false;
            try
            {
                // Open proximal/middle joints from actual child directions; preserve positions/scales,
                // metacarpals and distal joints. No assumed imported bone axis or glove curl reuse.
                for (int digit = 0; digit < 5; digit++)
                {
                    Vector3 direction = Vector3.ProjectOnPlane(rig.Fingers[digit * 3 + 1].position - rig.Fingers[digit * 3].position, normal).normalized;
                    if (direction.sqrMagnitude < .99f) return false;
                    for (int joint = 0; joint < 2; joint++)
                    {
                        Transform bone = rig.Fingers[digit * 3 + joint], child = rig.Fingers[digit * 3 + joint + 1];
                        bone.rotation = Quaternion.FromToRotation(child.position - bone.position, direction) * bone.rotation;
                        int index = digit * 2 + joint; opened[index] = bone.localRotation;
                        if (Quaternion.Angle(saved[index], opened[index]) > 35f) return false;
                    }
                }
                rig.Renderer.BakeMesh(bake);
                var vertices = new List<Vector3>(bake.vertexCount); bake.GetVertices(vertices);
                Vector3[] normals = bake.normals; Mesh source = rig.Renderer.sharedMesh;
                if (source.vertexCount != vertices.Count || normals.Length != vertices.Count) return false;
                var skin = new bool[vertices.Count];
                foreach (int sub in rig.SkinSubmeshes)
                    foreach (int index in source.GetIndices(sub)) skin[index] = true;
                var allSkin = new List<int>();
                for (int i = 0; i < skin.Length; i++) if (skin[i]) allSkin.Add(i);
                if (allSkin.Count < 100) return false;
                BoneWeight[] weights = source.boneWeights; Transform[] bones = rig.Renderer.bones;
                if (weights.Length != vertices.Count) return false;
                var inHand = new bool[bones.Length];
                for (int i = 0; i < bones.Length; i++) inHand[i] = bones[i] && (bones[i] == rig.Wrist || bones[i].IsChildOf(rig.Wrist));
                Matrix4x4 matrix = rig.Renderer.transform.localToWorldMatrix, normalMatrix = matrix.inverse.transpose;
                var relative = new Vector3[vertices.Count]; var worldNormals = new Vector3[vertices.Count];
                var palm = new bool[vertices.Count]; var rigidHand = new List<int>();
                foreach (int i in allSkin)
                {
                    relative[i] = matrix.MultiplyPoint3x4(vertices[i]) - wrist;
                    worldNormals[i] = normalMatrix.MultiplyVector(normals[i]).normalized;
                    BoneWeight weight = weights[i];
                    float sum = Weight(inHand, weight.boneIndex0, weight.weight0) + Weight(inHand, weight.boneIndex1, weight.weight1)
                        + Weight(inHand, weight.boneIndex2, weight.weight2) + Weight(inHand, weight.boneIndex3, weight.weight3);
                    if (sum <= .999f) continue;
                    rigidHand.Add(i);
                    float ahead = Vector3.Dot(relative[i], forward), side = Vector3.Dot(relative[i], across);
                    palm[i] = ahead > -.005f && ahead < .105f && Mathf.Abs(side) < .055f && Vector3.Dot(worldNormals[i], normal) > .3f;
                }
                if (rigidHand.Count < 100) return false;
                float best = float.NegativeInfinity; int support = -1;
                Vector3 supportNormal = default, supportForward = default;
                var chosenPatch = new List<int>(); var patch = new List<int>();
                for (int tilt = -24; tilt <= 24; tilt += 3) for (int roll = -24; roll <= 24; roll += 3)
                {
                    Vector3 n = (normal + forward * Mathf.Tan(tilt * Mathf.Deg2Rad) + across * Mathf.Tan(roll * Mathf.Deg2Rad)).normalized;
                    float height = float.NegativeInfinity; int candidate = -1;
                    foreach (int i in rigidHand)
                    {
                        float projection = Vector3.Dot(relative[i], n);
                        if (projection > height) { height = projection; candidate = i; }
                    }
                    // Actual outermost hand skin must be PALM skin, not a fingertip or virtual stand-off.
                    if (candidate < 0 || !palm[candidate]) continue;
                    Vector3 f = Vector3.ProjectOnPlane(forward, n).normalized, x = Vector3.Cross(n, f).normalized;
                    patch.Clear(); float minU = float.PositiveInfinity, minV = minU, maxU = float.NegativeInfinity, maxV = maxU;
                    float sumU = 0, sumV = 0, sumUU = 0, sumVV = 0, sumUV = 0;
                    foreach (int i in rigidHand)
                    {
                        if (!palm[i] || height - Vector3.Dot(relative[i], n) >= .003f || Vector3.Dot(worldNormals[i], n) <= .35f) continue;
                        float u = Vector3.Dot(relative[i], f), v = Vector3.Dot(relative[i], x);
                        minU = Mathf.Min(minU, u); maxU = Mathf.Max(maxU, u); minV = Mathf.Min(minV, v); maxV = Mathf.Max(maxV, v);
                        sumU += u; sumV += v; sumUU += u * u; sumVV += v * v; sumUV += u * v; patch.Add(i);
                    }
                    if (patch.Count < 6 || maxU - minU < .012f || maxV - minV < .012f) continue;
                    float area = (maxU - minU) * (maxV - minV), count = patch.Count;
                    float varU = sumUU / count - sumU * sumU / (count * count), varV = sumVV / count - sumV * sumV / (count * count);
                    float cov = sumUV / count - sumU * sumV / (count * count);
                    float eigen = .5f * (varU + varV - Mathf.Sqrt((varU - varV) * (varU - varV) + 4f * cov * cov));
                    if (area < .0002f || eigen < .000004f) continue;
                    float score = area - (1f - Vector3.Dot(n, normal)) * .001f - Mathf.Abs(Vector3.Dot(relative[candidate], across)) * .002f;
                    if (score <= best) continue;
                    best = score; support = candidate; supportNormal = n; supportForward = f;
                    chosenPatch.Clear(); chosenPatch.AddRange(patch);
                }
                if (support < 0) return false;
                Vector3 localPoint = rig.Wrist.InverseTransformPoint(wrist + relative[support]);
                Quaternion localFrame = Quaternion.Inverse(rig.Wrist.rotation) * Quaternion.LookRotation(supportForward, supportNormal);
                pose = new BarePalmContactPose(rig.Renderer, bake, vertices, allSkin.ToArray(), chosenPatch.ToArray(),
                    opened, support, localPoint, localFrame);
                retained = true; return true;
            }
            catch (Exception error) { Debug.LogException(error); return false; }
            finally
            {
                for (int digit = 0; digit < 5; digit++) for (int joint = 0; joint < 2; joint++)
                    if (rig.Fingers[digit * 3 + joint]) rig.Fingers[digit * 3 + joint].localRotation = saved[digit * 2 + joint];
                if (!retained) UnityEngine.Object.Destroy(bake);
            }
        }
        static float Weight(bool[] hand, int index, float weight) => index >= 0 && index < hand.Length && hand[index] ? weight : 0f;
        internal static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        internal static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
    }
}
