using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace OutOfReach.Presentation.LaunchLandscape.Editor
{
    /// <summary>Original passive landscape; attaches to the stationary site in Edit mode. Root saves the scene.</summary>
    public static class LaunchLandscapeAuthoring
    {
        public const string AssetRoot = "Assets/_OutOfReach/Visuals/LaunchLandscapeV02";
        public const string GroupName = "Launch landscape V02 - ridges and dry scrub";
        private const string GroundRoot = "Assets/_OutOfReach/Visuals/Environment/GroundFinishV02/Textures/";
        public static string LastReport { get; private set; }

        public static GameObject Apply(Transform site)
        {
            if (!site || Application.isPlaying || site.name != "Launch site - stationary")
                throw new InvalidOperationException("Pass the existing stationary launch site in Edit mode.");
            if ((site.lossyScale - Vector3.one).sqrMagnitude > .0001f)
                throw new InvalidOperationException("Landscape needs metre-correct site scale.");
            Transform terrain = site.Find("Coastal relief and launch pad detail");
            if (!terrain || !terrain.gameObject.activeInHierarchy)
                throw new InvalidOperationException("Existing finished LandTerrainV01 must be active first.");
            var collidersBefore = site.GetComponentsInChildren<Collider>(true).Select(c => c.GetInstanceID()).OrderBy(i => i).ToArray();
            var sampler = new TerrainSampler(site, terrain);
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(AssetRoot + "/LandscapeSurface.shader");
            if (!shader || ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("Landscape shader must import without errors before authoring.");
            EnsureFolder(AssetRoot + "/Generated");
            Material ridge = Material(shader, "Warm eroded shale", false, new Color(.78f, .72f, .60f), .52f);
            Material grass = Material(shader, "Dry coastal grass", true, Color.white, 0f);
            Material scrub = Material(shader, "Muted sage scrub", true, Color.white, 0f);
            var previous = site.Find(GroupName);
            var group = new GameObject(GroupName);
            group.transform.SetParent(site, false);
            int ridgeTriangles = 0, detailTriangles = 0, grassCount = 0, scrubCount = 0;
            try
            {
                for (int sector = 0; sector < 8; sector++)
                {
                    Mesh mesh = RidgeMesh(sector);
                    ridgeTriangles += mesh.triangles.Length / 3;
                    Attach(group.transform, "Eroded ridgeline " + sector.ToString("00"), mesh, ridge, false);
                }
                var random = new System.Random(150026);
                // Cluster placement rather than a uniform scatter. All points outside the operational footprint.
                for (int sector = 0; sector < 12; sector++)
                {
                    var grassGeometry = new Geometry();
                    var scrubGeometry = new Geometry();
                    for (int cluster = 0; cluster < 13; cluster++)
                    {
                        float angle = (sector + Range(random, .06f, .94f)) * Mathf.PI / 6f;
                        float radius = Range(random, 87f, 265f);
                        Vector2 center = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                        float spread = Range(random, 3.4f, 11f);
                        for (int plant = 0; plant < 18; plant++)
                        {
                            float a = Range(random, 0, Mathf.PI * 2), r = Mathf.Sqrt(Range(random, 0, 1)) * spread;
                            Vector2 point = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                            if (Protected(point.x, point.y) || !sampler.Sample(point, out Vector3 p, out Vector3 n) || n.y < .86f) continue;
                            p.y -= .018f;
                            if (plant % 7 == 0) { Scrub(scrubGeometry, p, random); scrubCount++; }
                            else { Tussock(grassGeometry, p, random); grassCount++; }
                        }
                    }
                    detailTriangles += grassGeometry.Triangles.Count / 3 + scrubGeometry.Triangles.Count / 3;
                    if (grassGeometry.Vertices.Count > 0)
                        Attach(group.transform, "Dry grass patch " + sector.ToString("00"), grassGeometry.Mesh(), grass, true);
                    if (scrubGeometry.Vertices.Count > 0)
                        Attach(group.transform, "Sage scrub patch " + sector.ToString("00"), scrubGeometry.Mesh(), scrub, true);
                }
                if (grassCount < 300 || scrubCount < 40 || detailTriangles > 190000)
                    throw new InvalidOperationException("Landscape placement failed its bounded density check.");
                if (!collidersBefore.SequenceEqual(site.GetComponentsInChildren<Collider>(true).Select(c => c.GetInstanceID()).OrderBy(i => i)))
                    throw new InvalidOperationException("Landscape authoring changed gameplay colliders.");
                if (group.GetComponentsInChildren<MonoBehaviour>(true).Length != 0 || group.GetComponentsInChildren<Collider>(true).Length != 0)
                    throw new InvalidOperationException("Landscape must contain passive geometry only.");
                // Replace only this tool's preceding group after the new group validates successfully.
                if (previous) Object.DestroyImmediate(previous.gameObject);
                LastReport = "ridgeTriangles=" + ridgeTriangles + " detailTriangles=" + detailTriangles
                    + " grassTussocks=" + grassCount + " scrubPlants=" + scrubCount
                    + " renderers=" + group.GetComponentsInChildren<Renderer>().Length
                    + " collidersChanged=0 protectedFootprintPlants=0 seed=150026";
                Debug.Log("LAUNCH_LANDSCAPE_V02_READY " + LastReport);
                return group;
            }
            catch { Object.DestroyImmediate(group); throw; }
        }

        // Two irregular, continuous low ridgelines; they are fictional Earth launch-site scenery.
        // Exact shared angular endpoints avoid cracks between sectors. Outer/inner skirts sit below existing land.
        public static float RidgeHeight(float radius, float angle)
        {
            float x = Mathf.Cos(angle) * radius, z = Mathf.Sin(angle) * radius;
            // Broad asymmetric shoulders and world-space erosion; avoid repeated radial flutes.
            float warp = 190f * Mathf.Sin(angle * 2f + .4f) + 95f * Mathf.Sin(angle * 3f - .8f);
            float first = (radius - 2180f - warp) / 790f;
            float second = (radius - 3470f + warp * .6f) / 930f;
            float profile = 75f + 67f * Fractal(x*.00078f+19,z*.00078f+83);
            float prominence = .18f + .82f * Smooth(-.65f, .55f, Mathf.Sin(angle - .3f));
            float baseHeight = (profile * Mathf.Exp(-first * first * 1.25f)
                + (67f + 35f * Fractal(x*.0006f+53,z*.0006f+29)) * Mathf.Exp(-second * second * 1.45f)) * prominence;
            float detail = Fractal(x * .0031f + 27, z * .0031f + 91);
            float erosion = 15f * Mathf.Pow(1f-Fractal(x*.007f+311,z*.007f+117),2f);
            float weight = Smooth(1120f, 1510f, radius) * (1 - Smooth(4080f, 4750f, radius));
            return -49f + Mathf.Max(0, baseHeight * (.84f + detail * .28f) - erosion) * weight;
        }

        private static Mesh RidgeMesh(int sector)
        {
            const int angular = 96, radial = 36;
            var g = new Geometry();
            for (int r = 0; r <= radial; r++)
                for (int a = 0; a <= angular; a++)
                {
                    float angle = (sector + a / (float)angular) * Mathf.PI / 4;
                    float radius = Mathf.Lerp(1120, 4750, r / (float)radial);
                    float x = Mathf.Cos(angle) * radius, z = Mathf.Sin(angle) * radius;
                    float y = RidgeHeight(radius, angle);
                    float bands = .5f + .5f * Mathf.Sin(y * .12f + Fractal(x * .0013f, z * .0013f) * 3f);
                    Color color = Color.Lerp(new Color(.46f, .43f, .37f), new Color(.66f, .60f, .49f), bands * .55f + .18f);
                    g.Vertices.Add(new Vector3(x, y, z)); g.Colors.Add(color); g.Uv.Add(new Vector2(x, z) / 15f);
                }
            for (int r = 0; r < radial; r++)
                for (int a = 0; a < angular; a++)
                {
                    int i = r * (angular + 1) + a;
                    g.Triangles.AddRange(new[] { i, i + 1, i + angular + 1, i + 1, i + angular + 2, i + angular + 1 });
                }
            Mesh mesh = g.Mesh();
            var normals = new Vector3[g.Vertices.Count];
            // Analytic neighbouring samples also cross sector boundaries, removing hard lighting seams.
            for (int i = 0; i < normals.Length; i++)
            {
                Vector3 p = g.Vertices[i];
                float dx = (HeightXZ(p.x + 3, p.z) - HeightXZ(p.x - 3, p.z)) / 6;
                float dz = (HeightXZ(p.x, p.z + 3) - HeightXZ(p.x, p.z - 3)) / 6;
                normals[i] = new Vector3(-dx, 1, -dz).normalized;
                if (float.IsNaN(p.y) || p.y < -49.01f || p.y > 180f)
                    throw new InvalidOperationException("Non-finite or excessive landscape ridge elevation.");
            }
            mesh.normals = normals; mesh.RecalculateTangents();
            return mesh;
        }

        private static float HeightXZ(float x, float z) => RidgeHeight(Mathf.Sqrt(x * x + z * z), Mathf.Atan2(z, x));

        private static void Tussock(Geometry g, Vector3 origin, System.Random random)
        {
            float scale = Range(random, .64f, 1.32f);
            Color tint = Color.Lerp(new Color(.25f, .27f, .14f), new Color(.58f, .48f, .27f), Range(random, 0, 1));
            for (int blade = 0; blade < 9; blade++)
            {
                float a = Range(random, 0, Mathf.PI * 2), h = Range(random, .18f, .43f) * scale;
                Vector3 outward = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                Vector3 side = new Vector3(-outward.z, 0, outward.x);
                Vector3 root = origin + outward * Range(random, .015f, .10f);
                float bend = Range(random, .07f, .22f) * scale, width = Range(random, .012f, .024f) * scale;
                for (int s = 0; s < 3; s++)
                {
                    float t0 = s / 3f, t1 = (s + 1) / 3f;
                    Vector3 p0 = root + Vector3.up * h * t0 + outward * bend * t0 * t0;
                    Vector3 p1 = root + Vector3.up * h * t1 + outward * bend * t1 * t1;
                    Color c0 = tint * Mathf.Lerp(.52f, 1.16f, t0), c1 = tint * Mathf.Lerp(.52f, 1.16f, t1);
                    c0.a = c1.a = 1;
                    g.Quad(p0 - side * width * (1 - t0), p0 + side * width * (1 - t0),
                        p1 + side * width * (1 - t1 + .025f), p1 - side * width * (1 - t1 + .025f), c0, c1);
                }
            }
        }

        private static void Scrub(Geometry g, Vector3 origin, System.Random random)
        {
            float height = Range(random, .23f, .62f), angle = Range(random, 0, Mathf.PI * 2);
            Color leaves = Color.Lerp(new Color(.21f, .25f, .14f), new Color(.38f, .39f, .24f), Range(random, 0, 1));
            for (int stem = 0; stem < 7; stem++)
            {
                float a = angle + stem * 2.39996f;
                Vector3 direction = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                Vector3 tip = origin + direction * height * Range(random, .3f, .78f) + Vector3.up * height * Range(random, .55f, 1);
                Vector3 side = Vector3.Cross((tip - origin).normalized, Vector3.up).normalized * .008f;
                g.Quad(origin - side, origin + side, tip + side * .2f, tip - side * .2f,
                    new Color(.19f, .17f, .12f), new Color(.36f, .31f, .20f));
                for (int leaf = 0; leaf < 5; leaf++)
                {
                    Vector3 c = Vector3.Lerp(origin, tip, .36f + leaf * .14f);
                    float sign = leaf % 2 == 0 ? 1 : -1;
                    Vector3 sideLeaf = new Vector3(-direction.z, .25f, direction.x) * sign * Range(random, .035f, .075f);
                    Vector3 forward = direction * .026f + Vector3.up * .014f;
                    g.Quad(c - forward, c + sideLeaf * .52f - forward * .65f,
                        c + sideLeaf, c + sideLeaf * .52f + forward, leaves * .75f, leaves);
                }
            }
        }

        private static bool Protected(float x, float z)
        {
            // Existing apron, facility/crawler corridor, flame-trench/service outlet plus 9 m buffer.
            return InRect(x, z, -81, 81, -71, 81) || InRect(x, z, -64, 104, -1029, -41)
                || InRect(x, z, -44, 44, 51, 329);
        }
        private static bool InRect(float x, float z, float x0, float x1, float z0, float z1) => x >= x0 && x <= x1 && z >= z0 && z <= z1;
        private static float Range(System.Random random, float low, float high) => low + (float)random.NextDouble() * (high - low);
        private static float Smooth(float low, float high, float value) { float t = Mathf.Clamp01((value - low) / (high - low)); return t * t * (3 - 2 * t); }
        private static float Fractal(float x, float z) => .57f * Mathf.PerlinNoise(x, z) + .28f * Mathf.PerlinNoise(x * 2.13f + 43, z * 2.13f + 17) + .15f * Mathf.PerlinNoise(x * 4.37f + 71, z * 4.37f + 13);

        private static Material Material(Shader shader, string name, bool twoSided, Color tint, float textureWeight)
        {
            string path = AssetRoot + "/Generated/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(shader) { name = name }; AssetDatabase.CreateAsset(material, path); }
            material.shader = shader;
            material.SetColor("_BaseColor", tint); material.SetFloat("_TextureWeight", textureWeight);
            material.SetFloat("_Cull", twoSided ? 0 : 2); material.SetFloat("_Smoothness", .08f);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(GroundRoot + "Ground110_Color.jpg"));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(GroundRoot + "Ground110_NormalGL.jpg"));
            if (!material.GetTexture("_BaseMap") || !material.GetTexture("_BumpMap"))
                throw new InvalidOperationException("Reuse the already licensed ambientCG Ground110 textures; no fallback placeholders.");
            EditorUtility.SetDirty(material); return material;
        }

        private static void Attach(Transform parent, string name, Mesh generated, Material material, bool shadows)
        {
            string path = AssetRoot + "/Generated/" + name + ".asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh) { EditorUtility.CopySerialized(generated, mesh); Object.DestroyImmediate(generated); EditorUtility.SetDirty(mesh); }
            else { mesh = generated; mesh.name = name; AssetDatabase.CreateAsset(mesh, path); }
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.layer = 2;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.TwoSided : ShadowCastingMode.Off;
            renderer.receiveShadows = true; renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/'); string parent = path.Substring(0, slash);
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        private sealed class Geometry
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Color> Colors = new List<Color>();
            public readonly List<Vector2> Uv = new List<Vector2>();
            public readonly List<int> Triangles = new List<int>();
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color bottom, Color top)
            {
                int i = Vertices.Count; Vertices.AddRange(new[] { a, b, c, d }); Colors.AddRange(new[] { bottom, bottom, top, top });
                Uv.AddRange(new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up });
                Triangles.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }
            public Mesh Mesh()
            {
                var mesh = new Mesh { indexFormat = Vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.SetVertices(Vertices); mesh.SetColors(Colors); mesh.SetUVs(0, Uv); mesh.SetTriangles(Triangles, 0);
                mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds(); return mesh;
            }
        }

        /// <summary>Samples real current mesh triangles in site coordinates without adding even temporary colliders.</summary>
        private sealed class TerrainSampler
        {
            private const float Cell = 24f;
            private readonly Dictionary<Vector2Int, List<Triangle>> cells = new Dictionary<Vector2Int, List<Triangle>>();
            private readonly struct Triangle
            {
                public readonly Vector3 A, B, C, Normal;
                public Triangle(Vector3 a, Vector3 b, Vector3 c) { A = a; B = b; C = c; Normal = Vector3.Cross(b - a, c - a).normalized; }
            }
            public TerrainSampler(Transform site, Transform terrain)
            {
                foreach (MeshFilter filter in terrain.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!filter.gameObject.activeInHierarchy || !filter.name.Contains("Terrain") || !filter.sharedMesh) continue;
                    Matrix4x4 matrix = site.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    Vector3[] vertices = filter.sharedMesh.vertices; int[] triangles = filter.sharedMesh.triangles;
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        var t = new Triangle(matrix.MultiplyPoint3x4(vertices[triangles[i]]), matrix.MultiplyPoint3x4(vertices[triangles[i + 1]]), matrix.MultiplyPoint3x4(vertices[triangles[i + 2]]));
                        if (t.Normal.y < .2f) continue;
                        float minX = Mathf.Min(t.A.x, t.B.x, t.C.x), maxX = Mathf.Max(t.A.x, t.B.x, t.C.x);
                        float minZ = Mathf.Min(t.A.z, t.B.z, t.C.z), maxZ = Mathf.Max(t.A.z, t.B.z, t.C.z);
                        if (maxX < -300 || minX > 300 || maxZ < -300 || minZ > 300) continue;
                        for (int x = Mathf.FloorToInt(Mathf.Max(minX, -300) / Cell); x <= Mathf.FloorToInt(Mathf.Min(maxX, 300) / Cell); x++)
                            for (int z = Mathf.FloorToInt(Mathf.Max(minZ, -300) / Cell); z <= Mathf.FloorToInt(Mathf.Min(maxZ, 300) / Cell); z++)
                            {
                                var key = new Vector2Int(x, z);
                                if (!cells.TryGetValue(key, out var list)) { list = new List<Triangle>(); cells.Add(key, list); }
                                list.Add(t);
                            }
                    }
                }
                if (cells.Count == 0) throw new InvalidOperationException("No readable upward terrain triangles found.");
            }
            public bool Sample(Vector2 point, out Vector3 position, out Vector3 normal)
            {
                position = Vector3.zero; normal = Vector3.up; float highest = float.NegativeInfinity;
                if (!cells.TryGetValue(new Vector2Int(Mathf.FloorToInt(point.x / Cell), Mathf.FloorToInt(point.y / Cell)), out var list)) return false;
                foreach (Triangle t in list)
                {
                    float denominator = (t.B.z - t.C.z) * (t.A.x - t.C.x) + (t.C.x - t.B.x) * (t.A.z - t.C.z);
                    if (Mathf.Abs(denominator) < .000001f) continue;
                    float a = ((t.B.z - t.C.z) * (point.x - t.C.x) + (t.C.x - t.B.x) * (point.y - t.C.z)) / denominator;
                    float b = ((t.C.z - t.A.z) * (point.x - t.C.x) + (t.A.x - t.C.x) * (point.y - t.C.z)) / denominator;
                    float c = 1 - a - b;
                    if (a < -.0001f || b < -.0001f || c < -.0001f) continue;
                    float y = a * t.A.y + b * t.B.y + c * t.C.y;
                    if (y > highest) { highest = y; position = new Vector3(point.x, y, point.y); normal = t.Normal; }
                }
                return !float.IsNegativeInfinity(highest);
            }
        }
    }
}
