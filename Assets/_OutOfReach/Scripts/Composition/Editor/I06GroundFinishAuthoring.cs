using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace OutOfReach.Composition.Editor
{
    /// <summary>Editor-only finish of the existing terrain. No saved scene or collider writes.</summary>
    public static class I06GroundFinishAuthoring
    {
        public const string AssetRoot = "Assets/_OutOfReach/Visuals/Environment/GroundFinishV02";
        private const string SourceMeshPath = "Assets/_OutOfReach/Visuals/Models/LandTerrainV01/LT_LandTerrain.fbx";
        public static string LastReport { get; private set; }

        public static void Apply(Transform site)
        {
            if (!site || Application.isPlaying) throw new InvalidOperationException("Supply the stationary land site in Edit mode.");
            Transform group = site.Find("Coastal relief and launch pad detail");
            if (!group || !group.gameObject.activeInHierarchy) throw new InvalidOperationException("Active authored terrain is required.");
            var inner = group.GetComponentsInChildren<MeshFilter>(true).Single(f => f.name == "LT_TerrainInner");
            var sourceRoot = AssetDatabase.LoadAssetAtPath<GameObject>(SourceMeshPath);
            if (!sourceRoot) throw new InvalidOperationException("Original LandTerrainV01 source is missing.");
            Mesh source = sourceRoot.GetComponentsInChildren<MeshFilter>(true).Single(f => f.name == "LT_TerrainInner").sharedMesh;
            var collidersBefore = site.GetComponentsInChildren<Collider>(true).Select(c => c.GetInstanceID()).OrderBy(i => i).ToArray();
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(AssetRoot + "/GroundFinishV02.shader");
            if (!shader || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Ground finish shader is missing or has compile errors.");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(AssetRoot + "/GroundFinishV02.mat");
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, AssetRoot + "/GroundFinishV02.mat"); }
            material.shader = shader;
            Bind(material, "_Soil", "Ground109"); Bind(material, "_Gravel", "Ground110");
            material.SetTexture("_MacroMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_OutOfReach/Visuals/Models/LandTerrainV01/Textures/LT_Terrain_Macro.png"));
            material.SetVector("_SiteOriginWS", site.position);
            material.SetVector("_TileMetres", new Vector4(3.4f, 2.6f, 0, 0));
            material.SetFloat("_NormalStrength", .75f);
            EditorUtility.SetDirty(material);
            Mesh clone = Bake(source, inner.transform, site);
            string meshPath = AssetRoot + "/LT_TerrainInner_Microrelief.asset";
            Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (saved) { EditorUtility.CopySerialized(clone, saved); UnityEngine.Object.DestroyImmediate(clone); EditorUtility.SetDirty(saved); }
            else { saved = clone; AssetDatabase.CreateAsset(saved, meshPath); }
            inner.sharedMesh = saved;
            inner.GetComponent<MeshRenderer>().sharedMaterial = material;
            // Existing low hummocks use the same macro UVs and should merge into the surface.
            foreach (MeshRenderer renderer in group.GetComponentsInChildren<MeshRenderer>(true))
                if (renderer.name == "LSS_GroundBreakup_v02") renderer.sharedMaterial = material;
            if (!collidersBefore.SequenceEqual(site.GetComponentsInChildren<Collider>(true).Select(c => c.GetInstanceID()).OrderBy(i => i)))
                throw new InvalidOperationException("Collider identity changed during visual-only authoring.");
            Debug.Log("I06_GROUND_FINISH_READY " + LastReport);
            // Root owns AssetDatabase.SaveAssets and saving the actual scene.
        }

        private static void Bind(Material material, string prefix, string id)
        {
            material.SetTexture(prefix + "Color", Texture(id + "_Color.jpg", false, false));
            material.SetTexture(prefix + "Normal", Texture(id + "_NormalGL.jpg", true, false));
            material.SetTexture(prefix + "Mask", Texture(id + "_Mask.png", false, true));
        }

        private static Texture2D Texture(string name, bool normal, bool linear)
        {
            string path = AssetRoot + "/Textures/" + name;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new InvalidOperationException("Missing imported texture " + path);
            TextureImporterType type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            bool srgb = !normal && !linear;
            if (importer.textureType != type || importer.sRGBTexture != srgb || importer.wrapMode != TextureWrapMode.Repeat
                || importer.maxTextureSize != 2048 || importer.anisoLevel != 8 || !importer.mipmapEnabled)
            {
                importer.textureType = type; importer.sRGBTexture = srgb; importer.wrapMode = TextureWrapMode.Repeat;
                importer.maxTextureSize = 2048; importer.anisoLevel = 8; importer.mipmapEnabled = true;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Mesh Bake(Mesh source, Transform meshTransform, Transform site)
        {
            var vertices = new List<Vector3>(source.vertices);
            var normals = new List<Vector3>(source.normals);
            var uv = new List<Vector2>(source.uv);
            var triangles = new List<int>(source.triangles);
            if (source.subMeshCount != 1 || normals.Count != vertices.Count || uv.Count != vertices.Count)
                throw new InvalidOperationException("Expected the original single-material terrain with normals and macro UVs.");
            Matrix4x4 toSite = site.worldToLocalMatrix * meshTransform.localToWorldMatrix;
            Matrix4x4 toMesh = toSite.inverse;
            if ((toSite.lossyScale - Vector3.one).sqrMagnitude > .000001f)
                throw new InvalidOperationException("Terrain must retain metre-correct scale.");
            for (int round = 0; round < 3; round++)
            {
                var split = new Dictionary<ulong, int>();
                for (int t = 0; t < triangles.Count; t += 3)
                    for (int e = 0; e < 3; e++)
                    {
                        int a = triangles[t + e], b = triangles[t + (e + 1) % 3];
                        ulong key = Edge(a, b); if (split.ContainsKey(key)) continue;
                        Vector3 p = toSite.MultiplyPoint3x4((vertices[a] + vertices[b]) * .5f);
                        if (new Vector2(p.x, p.z).magnitude > 230f || ProtectedDistance(p.x, p.z) < 6f
                            || (vertices[a] - vertices[b]).sqrMagnitude <= 9f) continue;
                        split[key] = vertices.Count;
                        vertices.Add((vertices[a] + vertices[b]) * .5f);
                        normals.Add((normals[a] + normals[b]).normalized);
                        uv.Add((uv[a] + uv[b]) * .5f);
                    }
                if (split.Count == 0) break;
                var next = new List<int>(triangles.Count + split.Count * 6);
                for (int t = 0; t < triangles.Count; t += 3)
                {
                    int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                    bool ab = split.TryGetValue(Edge(a, b), out int x), bc = split.TryGetValue(Edge(b, c), out int y), ca = split.TryGetValue(Edge(c, a), out int z);
                    int count = (ab ? 1 : 0) + (bc ? 1 : 0) + (ca ? 1 : 0);
                    if (count == 0) Tri(next, a, b, c);
                    else if (count == 3) { Tri(next, a, x, z); Tri(next, x, b, y); Tri(next, z, y, c); Tri(next, x, y, z); }
                    else if (count == 1)
                    {
                        if (ab) { Tri(next, a, x, c); Tri(next, x, b, c); }
                        else if (bc) { Tri(next, a, b, y); Tri(next, a, y, c); }
                        else { Tri(next, a, b, z); Tri(next, b, c, z); }
                    }
                    else if (!ca) { Tri(next, x, b, y); Tri(next, a, x, c); Tri(next, x, y, c); }
                    else if (!ab) { Tri(next, y, c, z); Tri(next, a, b, z); Tri(next, b, y, z); }
                    else { Tri(next, z, a, x); Tri(next, x, b, c); Tri(next, x, c, z); }
                }
                triangles = next;
            }
            if (triangles.Count / 3 > 160000) throw new InvalidOperationException("Bounded ground refinement exceeded 160k triangles.");
            float maximum = 0f; int changed = 0, protectedChanged = 0;
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 p = toSite.MultiplyPoint3x4(vertices[i]);
                float height = Microrelief(p.x, p.z);
                if (Mathf.Abs(height) < .000001f) continue;
                maximum = Mathf.Max(maximum, Mathf.Abs(height)); changed++;
                if (ProtectedDistance(p.x, p.z) <= 10f) protectedChanged++;
                vertices[i] += toMesh.MultiplyVector(Vector3.up * height);
                const float epsilon = .15f;
                float dx = (Microrelief(p.x + epsilon, p.z) - Microrelief(p.x - epsilon, p.z)) / (2 * epsilon);
                float dz = (Microrelief(p.x, p.z + epsilon) - Microrelief(p.x, p.z - epsilon)) / (2 * epsilon);
                Vector3 n = toSite.MultiplyVector(normals[i]).normalized;
                normals[i] = toMesh.MultiplyVector(new Vector3(n.x - dx * n.y, n.y, n.z - dz * n.y).normalized).normalized;
            }
            if (maximum > .02501f || protectedChanged != 0) throw new InvalidOperationException("Protected ground or displacement limit violated.");
            var mesh = new Mesh { name = "LT_TerrainInner_Microrelief", indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            LastReport = JsonUtility.ToJson(new Report { originalVertices = source.vertexCount, vertices = vertices.Count,
                triangles = triangles.Count / 3, displacedVertices = changed, maximumDisplacementMetres = maximum, protectedVerticesDisplaced = protectedChanged });
            return mesh;
        }

        /// <summary>Visual offset only, in site metres. Add to the original terrain, not to a gameplay floor.</summary>
        public static float Microrelief(float x, float z)
        {
            float protectedWeight = Smooth(10, 19, ProtectedDistance(x, z));
            float range = 1 - Smooth(180, 225, new Vector2(x, z).magnitude);
            float pattern = .6f * Mathf.Sin(x * 1.17f + .8f) * Mathf.Sin(z * .93f - .4f)
                + .4f * (2 * Mathf.PerlinNoise(x * .41f + 173, z * .41f + 217) - 1);
            return .025f * Mathf.Clamp(pattern, -1, 1) * protectedWeight * range;
        }
        public static float ProtectedDistance(float x, float z) => Mathf.Min(RectDistance(x, z, -72, 72, -62, 72),
            Mathf.Min(RectDistance(x, z, -55, 95, -1020, -50), RectDistance(x, z, -35, 35, 60, 320)));
        private static float RectDistance(float x, float z, float x0, float x1, float z0, float z1) =>
            new Vector2(Mathf.Max(x0 - x, 0, x - x1), Mathf.Max(z0 - z, 0, z - z1)).magnitude;
        private static float Smooth(float low, float high, float value) { float t = Mathf.Clamp01((value - low) / (high - low)); return t * t * (3 - 2 * t); }
        private static ulong Edge(int a, int b) => ((ulong)(uint)Mathf.Min(a, b) << 32) | (uint)Mathf.Max(a, b);
        private static void Tri(List<int> list, int a, int b, int c) { list.Add(a); list.Add(b); list.Add(c); }
        [Serializable] private sealed class Report { public int originalVertices, vertices, triangles, displacedVertices, protectedVerticesDisplaced; public float maximumDisplacementMetres; }
    }
}
