using System;
using System.Collections.Generic;
using OutOfReach.Contracts;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OutOfReach.Presentation.Motion
{
    /// <summary>Presentation phase of the berth rest. Core's IsResting / LastRestStop stay the only authority.</summary>
    public enum RestPhase { None, Entering, Held, Releasing }

    /// <summary>Pure timeline of the berth sequence, in seconds of unscaled presentation time (frozen while paused).</summary>
    public static class ModelRestTimeline
    {
        public const float SettleEnd = .9f, ChestStart = .95f, ChestEnd = 1.65f, HipStart = 1.8f, HipEnd = 2.5f, EnterDuration = 2.65f;
        public const float ReleaseDuration = 1.35f, ExitStart = .85f, HazardReleaseDuration = .25f;

        /// <summary>Entering: 0 = where the body was, 1 = settled in the bag.</summary>
        public static float Settle(float t) => ModelGait.Ease(Mathf.InverseLerp(0f, SettleEnd, t));
        /// <summary>Entering: closure (0..1) of the chest strap as the hand draws it across.</summary>
        public static float ChestClose(float t) => ModelGait.Ease(Mathf.InverseLerp(ChestStart + .1f, ChestEnd - .05f, t));
        /// <summary>Entering: closure (0..1) of the hip strap.</summary>
        public static float HipClose(float t) => ModelGait.Ease(Mathf.InverseLerp(HipStart + .1f, HipEnd - .05f, t));
        /// <summary>Entering: how much the right hand follows the strap tongue (reach, draw, return).</summary>
        public static float EnterHand(float t) => Mathf.Max(Window(t, ChestStart - .12f, ChestStart + .08f, ChestEnd - .02f, ChestEnd + .14f),
            Window(t, HipStart - .12f, HipStart + .08f, HipEnd - .02f, HipEnd + .14f));
        /// <summary>Entering: true while the hand works the hip strap (else the chest strap).</summary>
        public static bool EnterHandOnHip(float t) => t >= (ChestEnd + HipStart) * .5f;

        /// <summary>Releasing (ordinary wake): chest unclips first, then the hips; then the body leaves the bag.</summary>
        public static float ReleaseChest(float t) => 1f - ModelGait.Ease(Mathf.InverseLerp(.12f, .48f, t));
        public static float ReleaseHip(float t) => 1f - ModelGait.Ease(Mathf.InverseLerp(.5f, .82f, t));
        public static float ReleaseHand(float t) => Window(t, 0f, .12f, .78f, .92f);
        public static bool ReleaseHandOnHip(float t) => t >= .49f;
        public static float Exit(float t) => ModelGait.Ease(Mathf.InverseLerp(ExitStart, ReleaseDuration, t));
        /// <summary>Hazard wake: everything springs open and the body leaves at once.</summary>
        public static float HazardOpen(float t) => 1f - ModelGait.Ease(Mathf.Clamp01(t / HazardReleaseDuration));

        /// <summary>Wake reasons that must not wait for an unhurried unstrap.</summary>
        public static bool IsHazard(RestStopReason reason) =>
            reason == RestStopReason.FoodStoreEmpty || reason == RestStopReason.WaterStoreEmpty || reason == RestStopReason.OxygenStoreEmpty
            || reason == RestStopReason.PowerLost || reason == RestStopReason.CarbonDioxideDanger || reason == RestStopReason.MissionEnded;

        private static float Window(float t, float a, float b, float c, float d) =>
            ModelGait.Ease(Mathf.InverseLerp(a, b, t)) * (1f - ModelGait.Ease(Mathf.InverseLerp(c, d, t)));
    }

    /// <summary>
    /// One berth rest on the shared rig: a pose cross-fade into the bag, two webbing straps whose path is measured on
    /// the actual baked skin, and the right hand that draws and later unclips them. Props live under the visual root
    /// (never saved, never colliding) and are destroyed with the rig. No Update, input, physics or state of its own.
    /// </summary>
    internal sealed class ModelRestRig : IDisposable
    {
        private const float WebbingWidth = .046f, WebbingThickness = .004f, Clearance = .014f;
        private readonly Transform[] bones;
        private readonly Vector3[] fromPosition, toPosition;
        private readonly Quaternion[] fromRotation, toRotation;
        private readonly ModelGaitRig.Leg hand;
        private readonly Transform wrist;
        private readonly GameObject props;
        private readonly Strap chest, hip;
        private readonly Material webbing, housing;
        private readonly Mesh box;

        public float ChestStrapHeight => chest.Height;
        public float HipStrapHeight => hip.Height;
        public bool StrapsMeasured { get; private set; }
        public float ChestClosure => chest.Closure;
        public float HipClosure => hip.Closure;

        public ModelRestRig(Transform[] modelTransforms, Dictionary<string, Transform> nodes, Transform visualRoot, Shader lit)
        {
            bones = modelTransforms;
            fromPosition = new Vector3[bones.Length]; toPosition = new Vector3[bones.Length];
            fromRotation = new Quaternion[bones.Length]; toRotation = new Quaternion[bones.Length];
            wrist = ModelGaitRig.Find(nodes, "wrist.R");
            hand = new ModelGaitRig.Leg(ModelGaitRig.Find(nodes, "upperarm01.R"), ModelGaitRig.Find(nodes, "lowerarm01.R"), wrist, 1f);
            props = new GameObject("Berth restraint") { hideFlags = HideFlags.DontSave };
            props.SetActive(false);
            props.transform.SetParent(visualRoot, false);
            try
            {
                webbing = Finish(lit, "Berth webbing", new Color(.43f, .162f, .052f), .12f, 0f);
                housing = Finish(lit, "Berth buckle", new Color(.105f, .12f, .13f), .28f, .35f);
                box = MakeBox();
                chest = new Strap("Chest strap", props.transform, webbing, housing, box);
                hip = new Strap("Hip strap", props.transform, webbing, housing, box);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void CaptureFrom() => Capture(fromPosition, fromRotation);
        public void CaptureTo() => Capture(toPosition, toRotation);

        /// <summary>0 = the captured "from" pose, 1 = the captured "to" pose.</summary>
        public void Blend(float t)
        {
            for (int i = 0; i < bones.Length; i++)
                if (bones[i])
                    bones[i].SetLocalPositionAndRotation(Vector3.Lerp(fromPosition[i], toPosition[i], t),
                        Quaternion.Slerp(fromRotation[i], toRotation[i], t));
        }

        /// <summary>
        /// With the body in its bag pose at the berth frame: measures where the straps must run (around the actual
        /// skin of torso and arms at chest and hip height) and the arm lengths for the strapping hand.
        /// </summary>
        public void Measure(Transform frame, IReadOnlyList<SkinnedMeshRenderer> skins, Dictionary<string, Transform> nodes)
        {
            hand.CaptureStanding(frame);
            float chestHeight = .5f * (frame.InverseTransformPoint(ModelGaitRig.Find(nodes, "upperarm01.R").position).y
                + frame.InverseTransformPoint(ModelGaitRig.Find(nodes, "lowerarm01.R").position).y);
            float hipHeight = frame.InverseTransformPoint(wrist.position).y + .07f;
            var points = new List<Vector3>(4096);
            var mesh = new Mesh();
            try
            {
                foreach (SkinnedMeshRenderer skin in skins)
                {
                    if (!skin || !skin.enabled) continue;
                    skin.BakeMesh(mesh, true);
                    Matrix4x4 toWorld = Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
                    foreach (Vector3 v in mesh.vertices) points.Add(frame.InverseTransformPoint(toWorld.MultiplyPoint3x4(v)));
                }
            }
            finally { Object.DestroyImmediate(mesh); }
            StrapsMeasured = chest.Measure(points, chestHeight) & hip.Measure(points, hipHeight);
        }

        /// <summary>Strap closures (0 open .. 1 buckled) and the strapping hand; call after the body pose for this frame.</summary>
        public void Present(Transform frame, float chestClosure, float hipClosure, float handWeight, bool handOnHip, bool handAtBuckle)
        {
            chest.Present(frame, chestClosure);
            hip.Present(frame, hipClosure);
            props.SetActive(chestClosure > 0f || hipClosure > 0f || handWeight > 0f);
            if (handWeight <= .001f) return;
            Strap strap = handOnHip ? hip : chest;
            Vector3 target = handAtBuckle ? strap.BuckleHold(frame) : strap.TongueHold(frame);
            Vector3 rest = wrist.position;
            Vector3 pole = frame.right * .7f - frame.up * .55f - frame.forward * .25f;
            hand.PoseKeepFoot(Vector3.Lerp(rest, target, handWeight), pole);
        }

        public void Dispose()
        {
            if (props) { props.SetActive(false); Kill(props); }
            chest?.Dispose(); hip?.Dispose();
            if (box) Kill(box);
            if (webbing) Kill(webbing);
            if (housing) Kill(housing);
        }

        // Edit-mode harnesses cannot defer destruction; play mode must not destroy immediately.
        internal static void Kill(Object target)
        {
            if (!target) return;
            if (Application.isPlaying) Object.Destroy(target); else Object.DestroyImmediate(target);
        }

        private void Capture(Vector3[] positions, Quaternion[] rotations)
        {
            for (int i = 0; i < bones.Length; i++)
                if (bones[i]) { positions[i] = bones[i].localPosition; rotations[i] = bones[i].localRotation; }
        }

        private static Material Finish(Shader lit, string name, Color color, float smoothness, float metallic)
        {
            var material = new Material(lit) { name = name, hideFlags = HideFlags.DontSave };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            return material;
        }

        private static Mesh MakeBox()
        {
            var mesh = new Mesh { name = "Berth buckle box", hideFlags = HideFlags.DontSave };
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
            Vector3[] axes = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (Vector3 normal in axes)
            {
                Vector3 a = Vector3.Cross(normal, Mathf.Abs(normal.y) > .5f ? Vector3.forward : Vector3.up);
                Vector3 b = Vector3.Cross(normal, a);
                int start = v.Count;
                v.Add((normal - a - b) * .5f); v.Add((normal - a + b) * .5f); v.Add((normal + a + b) * .5f); v.Add((normal + a - b) * .5f);
                for (int k = 0; k < 4; k++) n.Add(normal);
                t.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetTriangles(t, 0); mesh.RecalculateBounds();
            // Make every face wind outward whatever the cross products produced.
            int[] tris = mesh.triangles; Vector3[] verts = mesh.vertices; Vector3[] norms = mesh.normals;
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 face = Vector3.Cross(verts[tris[i + 1]] - verts[tris[i]], verts[tris[i + 2]] - verts[tris[i]]);
                if (Vector3.Dot(face, norms[tris[i]]) < 0f) { int s = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = s; }
            }
            mesh.triangles = tris;
            return mesh;
        }

        /// <summary>One webbing strap: from the right wall anchor around the front of the body to the left buckle.</summary>
        private sealed class Strap : IDisposable
        {
            private readonly Vector3[] path = new Vector3[5];   // berth-frame points: right anchor .. left buckle
            private readonly float[] along = new float[5];
            private readonly Mesh ribbon;
            private readonly Transform ribbonNode, receiver, tongue, stub;
            private readonly Mesh stubMesh;
            private float length;
            public float Height { get; private set; }
            public float Closure { get; private set; }

            public Strap(string name, Transform parent, Material webbing, Material housing, Mesh box)
            {
                ribbon = new Mesh { name = name + " webbing", hideFlags = HideFlags.DontSave };
                ribbon.MarkDynamic();
                ribbonNode = Node(name, parent, ribbon, webbing);
                stubMesh = new Mesh { name = name + " anchor stub", hideFlags = HideFlags.DontSave };
                stub = Node(name + " stub", parent, stubMesh, webbing);
                receiver = Node(name + " receiver", parent, box, housing);
                receiver.localScale = new Vector3(.03f, .052f, .056f);
                tongue = Node(name + " tongue", parent, box, housing);
                tongue.localScale = new Vector3(.022f, .04f, .042f);
            }

            public bool Measure(List<Vector3> points, float height)
            {
                Height = height;
                float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
                int count = 0;
                foreach (Vector3 p in points)
                {
                    if (Mathf.Abs(p.y - height) > .03f) continue;
                    minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                    minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z);
                    count++;
                }
                bool measured = count >= 20;
                if (!measured) { minX = -.25f; maxX = .25f; minZ = -.12f; maxZ = .12f; }
                float front = maxZ + Clearance, middle = .5f * (minZ + maxZ);
                path[0] = new Vector3(maxX + .05f, height, minZ - .01f);                  // right wall anchor
                path[1] = new Vector3(maxX + Clearance, height, middle);                   // right flank
                path[2] = new Vector3(.45f * maxX, height, front);                          // across the front
                path[3] = new Vector3(.45f * minX, height, front);
                path[4] = new Vector3(minX - Clearance, height, middle + .02f);             // left buckle
                along[0] = 0f;
                for (int i = 1; i < path.Length; i++) along[i] = along[i - 1] + Vector3.Distance(path[i - 1], path[i]);
                length = along[path.Length - 1];
                BuildStub(minX, minZ, height, middle);
                return measured;
            }

            public void Present(Transform frame, float closure)
            {
                Closure = Mathf.Clamp01(closure);
                bool shown = Closure > 0f;
                ribbonNode.gameObject.SetActive(shown);
                tongue.gameObject.SetActive(shown);
                stub.gameObject.SetActive(true);
                receiver.gameObject.SetActive(true);
                receiver.localPosition = path[4];
                receiver.localRotation = Quaternion.LookRotation(path[4] - path[3], Vector3.up);
                if (!shown) return;
                BuildRibbon(Closure * length, out Vector3 tip, out Vector3 tangent);
                tongue.localPosition = tip - tangent * .02f;
                tongue.localRotation = Quaternion.LookRotation(tangent, Vector3.up);
            }

            /// <summary>Where the wrist holds the moving tongue (behind the tip, slightly off the webbing).</summary>
            public Vector3 TongueHold(Transform frame)
            {
                Sample(Closure * length, out Vector3 tip, out Vector3 tangent);
                return frame.TransformPoint(tip - tangent * .06f + Outward(tangent) * .035f);
            }

            /// <summary>Where the wrist rests to press the buckle release.</summary>
            public Vector3 BuckleHold(Transform frame)
            {
                Vector3 tangent = (path[4] - path[3]).normalized;
                return frame.TransformPoint(path[4] - tangent * .07f + Outward(tangent) * .04f);
            }

            private static Vector3 Outward(Vector3 tangent) => Vector3.Cross(Vector3.up, tangent).normalized;

            private void Sample(float distance, out Vector3 point, out Vector3 tangent)
            {
                distance = Mathf.Clamp(distance, 0f, length);
                for (int i = 1; i < path.Length; i++)
                {
                    if (distance > along[i] && i < path.Length - 1) continue;
                    float span = Mathf.Max(1e-5f, along[i] - along[i - 1]);
                    tangent = (path[i] - path[i - 1]) / span;
                    point = Vector3.Lerp(path[i - 1], path[i], Mathf.Clamp01((distance - along[i - 1]) / span));
                    return;
                }
                tangent = Vector3.forward; point = path[0];
            }

            private void BuildRibbon(float distance, out Vector3 tip, out Vector3 tangent)
            {
                var line = new List<Vector3> { path[0] };
                for (int i = 1; i < path.Length && along[i] < distance; i++) line.Add(path[i]);
                Sample(distance, out tip, out tangent);
                if ((tip - line[line.Count - 1]).sqrMagnitude > 1e-8f) line.Add(tip);
                if (line.Count < 2) line.Add(path[0] + tangent * .001f);
                FillBand(ribbon, line);
            }

            private void BuildStub(float minX, float minZ, float height, float middle)
            {
                // Short webbing from the left wall anchor to the receiver: the buckle is visibly tethered to the berth.
                FillBand(stubMesh, new List<Vector3> { new Vector3(minX - .05f, height, minZ - .01f), path[4] });
            }

            // A flat band of webbing along a berth-frame polyline: width vertical, both faces, thickness offset.
            private static void FillBand(Mesh mesh, List<Vector3> line)
            {
                var vertices = new List<Vector3>(line.Count * 4);
                var normals = new List<Vector3>(line.Count * 4);
                var triangles = new List<int>((line.Count - 1) * 12);
                Vector3 up = Vector3.up * (WebbingWidth * .5f);
                for (int i = 0; i < line.Count; i++)
                {
                    Vector3 tangent = (line[Mathf.Min(i + 1, line.Count - 1)] - line[Mathf.Max(i - 1, 0)]).normalized;
                    Vector3 outward = Vector3.Cross(Vector3.up, tangent).normalized;
                    Vector3 offset = outward * (WebbingThickness * .5f);
                    vertices.Add(line[i] + offset - up); vertices.Add(line[i] + offset + up);
                    vertices.Add(line[i] - offset - up); vertices.Add(line[i] - offset + up);
                    normals.Add(outward); normals.Add(outward); normals.Add(-outward); normals.Add(-outward);
                }
                for (int i = 0; i < line.Count - 1; i++)
                {
                    int a = i * 4, b = a + 4;
                    triangles.AddRange(new[] { a, b, b + 1, a, b + 1, a + 1 });             // outer face
                    triangles.AddRange(new[] { a + 2, a + 3, b + 3, a + 2, b + 3, b + 2 }); // inner face
                }
                mesh.Clear();
                mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTriangles(triangles, 0);
                // Wind each face along its own normal.
                int[] tris = mesh.triangles;
                for (int i = 0; i < tris.Length; i += 3)
                {
                    Vector3 face = Vector3.Cross(vertices[tris[i + 1]] - vertices[tris[i]], vertices[tris[i + 2]] - vertices[tris[i]]);
                    if (Vector3.Dot(face, normals[tris[i]]) < 0f) { int s = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = s; }
                }
                mesh.triangles = tris;
                mesh.RecalculateBounds();
            }

            private static Transform Node(string name, Transform parent, Mesh mesh, Material material)
            {
                var node = new GameObject(name) { hideFlags = HideFlags.DontSave };
                node.transform.SetParent(parent, false);
                node.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = node.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                return node.transform;
            }

            public void Dispose()
            {
                if (ribbon) Kill(ribbon);
                if (stubMesh) Kill(stubMesh);
            }
        }
    }
}
