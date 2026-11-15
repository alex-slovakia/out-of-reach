using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace OutOfReach.Presentation.LaunchLandscape.Editor
{
    /// <summary>
    /// TERRAIN-V03 (15 Nov 2026, user direction on issue #7 comment 6895002547 item 4): simpler launch-site
    /// land. One continuous ground material on the existing near terrain, a far land ring stitched exactly to
    /// its outer edge that rises into a mountain range at 6-10 km, and the scattered clutter switched off.
    /// Edit mode only; it never opens or saves a scene and never adds, removes or toggles a gameplay collider.
    /// The caller saves assets and the scene. Re-running replaces only this tool's own group and meshes.
    /// </summary>
    public static class LaunchTerrainV03Installer
    {
        public const string GroupName = "Launch terrain V03 - far land and mountains";
        public const string MeshFolder = "Assets/_OutOfReach/Visuals/LaunchLandscapeV02/Generated";
        public const string GroundMaterialPath = "Assets/_OutOfReach/Visuals/Environment/GroundFinishV02/GroundFinishV02.mat";
        public const string GroundShaderPath = "Assets/_OutOfReach/Visuals/Environment/GroundFinishV02/GroundFinishV02.shader";
        private const string GroundTextures = "Assets/_OutOfReach/Visuals/Environment/GroundFinishV02/Textures/";
        private const string Relief = "Coastal relief and launch pad detail";
        private const float Datum = -36.6f;
        private const int SectorCount = 16;
        private static readonly Vector2 Centre = new Vector2(0f, 5f);

        // Dense meadow (Alex's golden/olive grass reference, #7 6895376530): shell grass over the near field.
        public const string GrassGroupName = "Launch grass V03 - dense meadow";
        public const string GrassFolder = "Assets/_OutOfReach/Visuals/LaunchVegetationV03";
        public const string GrassShaderPath = GrassFolder + "/GrassShellsV03.shader";
        public const string GrassMaterialPath = GrassFolder + "/GrassShellsV03.mat";
        public const string GrassMeshFolder = GrassFolder + "/Generated";
        public const int ShellLayers = 24;
        public const float ShellHeight = .95f;
        public const float ShellRadius = 440f;
        private const int GrassSectorCount = 8;

        /// <summary>Site-relative paths that are switched off (replaced or clutter). Nothing is deleted.</summary>
        public static readonly string[] DeactivatedPaths =
        {
            Relief + "/LT_LandTerrain/LT_TerrainFar",            // flat green far ring, replaced by the V03 far land
            Relief + "/LT_LandTerrain/LT_Rocks",                 // 470 scattered rocks
            Relief + "/LT_Surroundings_v02",                     // berms, drainage riprap, culverts, hummocks, fence, poles, dark tree lines
            "Launch landscape V02 - ridges and dry scrub",       // sand-dune ridgelines (grass/scrub children already off)
            "Launch vegetation V03 - photographed foreground",   // 16 patches of tufts, shrubs and pebbles
        };
        /// <summary>Loose decorative rocks inside root's Poly Haven group (their colliders are already disabled).</summary>
        public static readonly string[] DeactivatedRockPrefixes = { "PH coast_land_rocks_04", "PH namaqualand_boulder_02" };

        public static string LastReport { get; private set; }

        public const string BootstrapPath = "Assets/_OutOfReach/Scenes/Bootstrap.unity";

        /// <summary>
        /// Integrator entry point (batch or menu): opens Bootstrap, applies V03 to its one stationary launch site,
        /// saves the generated assets and the scene. Batch: -executeMethod
        /// OutOfReach.Presentation.LaunchLandscape.Editor.LaunchTerrainV03Installer.InstallIntoBootstrap -quit
        /// </summary>
        public static void InstallIntoBootstrap()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(BootstrapPath, UnityEditor.SceneManagement.OpenSceneMode.Single);
            Apply(FindSite(scene));
            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Bootstrap could not be saved.");
            Debug.Log("LAUNCH_TERRAIN_V03_INSTALLED " + BootstrapPath + " " + LastReport);
        }

        /// <summary>Manual path for a person in the editor: applies to the open scene; they review and save it.</summary>
        [MenuItem("Tools/OUT OF REACH/Terrain V03 - apply to open scene (no save)")]
        public static void ApplyToOpenSceneMenu()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            Apply(FindSite(scene));
            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            EditorUtility.DisplayDialog("Terrain V03", "Applied. Review the launch site, then save the scene.\n\n" + LastReport, "OK");
        }

        private static Transform FindSite(UnityEngine.SceneManagement.Scene scene)
        {
            var sites = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Where(t => t.name == "Launch site - stationary").ToArray();
            if (sites.Length != 1) throw new InvalidOperationException("Expected exactly one 'Launch site - stationary' in " + scene.path + ", found " + sites.Length);
            return sites[0];
        }

        public static GameObject Apply(Transform site)
        {
            if (!site || Application.isPlaying || site.name != "Launch site - stationary")
                throw new InvalidOperationException("Pass the existing 'Launch site - stationary' Transform in Edit mode.");
            if ((site.lossyScale - Vector3.one).sqrMagnitude > .0001f)
                throw new InvalidOperationException("The launch site must keep metre scale 1.");
            Transform innerNode = site.Find(Relief + "/LT_LandTerrain/LT_TerrainInner");
            MeshFilter inner = innerNode ? innerNode.GetComponent<MeshFilter>() : null;
            if (!inner || !inner.sharedMesh || !inner.gameObject.activeInHierarchy)
                throw new InvalidOperationException("The active LT_TerrainInner ground mesh is required.");
            var collidersBefore = ColliderState(site);

            Material ground = PrepareGroundMaterial(site);
            inner.GetComponent<MeshRenderer>().sharedMaterial = ground;

            BoundaryRing boundary = OuterBoundary(inner, site);
            List<Ring> rings = BuildRings(boundary);
            BuildMesh(rings, boundary, out List<Vector3> vertices, out List<Vector3> normals, out List<int> triangles);
            Mesh[] sectors = SaveSectors(vertices, normals, triangles);
            Material grass = PrepareGrassMaterial(site);
            Mesh[] shells = SaveGrassShells(inner, site, out long shellTriangles);

            Transform previous = site.Find(GroupName);
            Transform previousGrass = site.Find(GrassGroupName);
            var group = new GameObject(GroupName);
            group.transform.SetParent(site, false);
            var grassGroup = new GameObject(GrassGroupName);
            grassGroup.transform.SetParent(site, false);
            try
            {
                foreach (Mesh shell in shells)
                {
                    var go = new GameObject(shell.name);
                    go.transform.SetParent(grassGroup.transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = shell;
                    var renderer = go.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = grass;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = true;
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                }
                for (int s = 0; s < sectors.Length; s++)
                {
                    var go = new GameObject(sectors[s].name);
                    go.transform.SetParent(group.transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = sectors[s];
                    var renderer = go.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = ground;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = true;
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                }
                var switchedOff = new List<string>();
                foreach (string path in DeactivatedPaths)
                {
                    Transform t = site.Find(path);
                    if (t && t.gameObject.activeSelf) { t.gameObject.SetActive(false); switchedOff.Add(path); }
                }
                Transform details = site.Find("Curated CC0 service details");
                if (details)
                    foreach (Transform child in details)
                        if (DeactivatedRockPrefixes.Any(p => child.name.StartsWith(p, StringComparison.Ordinal)) && child.gameObject.activeSelf)
                        {
                            if (child.GetComponentsInChildren<Collider>(true).Any(c => c.enabled))
                                throw new InvalidOperationException("Decorative rock " + child.name + " has an enabled collider; not touching it.");
                            child.gameObject.SetActive(false); switchedOff.Add("Curated CC0 service details/" + child.name);
                        }
                if (group.GetComponentsInChildren<Collider>(true).Length != 0 || group.GetComponentsInChildren<MonoBehaviour>(true).Length != 0
                    || grassGroup.GetComponentsInChildren<Collider>(true).Length != 0 || grassGroup.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                    throw new InvalidOperationException("The far land and the meadow must be passive geometry only.");
                if (!collidersBefore.SequenceEqual(ColliderState(site)))
                    throw new InvalidOperationException("Installer changed the set of active gameplay colliders.");
                if (previous) Object.DestroyImmediate(previous.gameObject);
                if (previousGrass) Object.DestroyImmediate(previousGrass.gameObject);
                long farTriangles = sectors.Sum(m => (long)m.GetIndexCount(0) / 3);
                float maxHeight = vertices.Max(v => v.y) - Datum;
                LastReport = "farTriangles=" + farTriangles + " farVertices=" + vertices.Count + " sectors=" + sectors.Length
                    + " seamVertices=" + boundary.Points.Count + " maxHeightAboveDatum=" + maxHeight.ToString("F0")
                    + " outerRadius=" + rings[rings.Count - 1].Radius.ToString("F0")
                    + " innerMaterial=" + ground.name + " meadowShells=" + ShellLayers + "x" + shells.Length + " meadowTriangles=" + shellTriangles
                    + " deactivated=" + switchedOff.Count + " [" + string.Join("; ", switchedOff) + "]"
                    + " collidersChanged=0";
                Debug.Log("LAUNCH_TERRAIN_V03_READY " + LastReport);
                return group;
            }
            catch { Object.DestroyImmediate(group); Object.DestroyImmediate(grassGroup); throw; }
        }

        // ---------- material ----------

        private static Material PrepareGroundMaterial(Transform site)
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(GroundShaderPath);
            if (!shader || ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("Launch Ground V03 shader is missing or has compile errors.");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
            if (!material) { material = new Material(shader) { name = "GroundFinishV02" }; AssetDatabase.CreateAsset(material, GroundMaterialPath); }
            material.shader = shader;
            Bind(material, "_SoilColor", "Ground109_Color.jpg");
            Bind(material, "_SoilNormal", "Ground109_NormalGL.jpg");
            Bind(material, "_SoilMask", "Ground109_Mask.png");
            material.SetVector("_SiteOriginWS", site.position);
            material.SetFloat("_DatumY", Datum);
            material.SetVector("_TileMetres", new Vector4(3.1f, 11.7f, 0, 0));
            material.SetFloat("_NormalStrength", .7f);
            foreach (var entry in Palette) material.SetColor(entry.Key, entry.Value);
            foreach (var entry in Scalars) material.SetFloat(entry.Key, entry.Value);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>sRGB palette and tuning of the V03 ground; the material is the single source for root.</summary>
        public static readonly KeyValuePair<string, Color>[] Palette =
        {
            new KeyValuePair<string, Color>("_GrassDry", new Color(.72f, .60f, .36f)),
            new KeyValuePair<string, Color>("_GrassGreen", new Color(.55f, .52f, .30f)),
            new KeyValuePair<string, Color>("_GrassLush", new Color(.44f, .45f, .24f)),
            new KeyValuePair<string, Color>("_Scrub", new Color(.30f, .31f, .19f)),
            new KeyValuePair<string, Color>("_Rock", new Color(.58f, .45f, .34f)),
            new KeyValuePair<string, Color>("_RockDark", new Color(.35f, .28f, .23f)),
            new KeyValuePair<string, Color>("_HazeColor", new Color(.42f, .48f, .58f)),
            new KeyValuePair<string, Color>("_HazeSunColor", new Color(.85f, .80f, .72f)),
        };
        public static readonly KeyValuePair<string, float>[] Scalars =
        {
            new KeyValuePair<string, float>("_DetailStrength", .7f),
            new KeyValuePair<string, float>("_DetailHue", .12f),
            new KeyValuePair<string, float>("_GreenAmount", .5f),
            new KeyValuePair<string, float>("_HazeDensity", .00009f),
            new KeyValuePair<string, float>("_HazeFloor", 1400f),
            new KeyValuePair<string, float>("_Sheen", .55f),
        };

        private static void Bind(Material material, string property, string file)
        {
            if (material.GetTexture(property)) return;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(GroundTextures + file);
            if (!texture) throw new InvalidOperationException("Missing licensed ambientCG texture " + GroundTextures + file);
            material.SetTexture(property, texture);
        }

        // ---------- dense meadow ----------

        /// <summary>sRGB colours and tuning of the shell grass (golden/olive reference).</summary>
        public static readonly KeyValuePair<string, Color>[] GrassPalette =
        {
            new KeyValuePair<string, Color>("_BladeBase", new Color(.25f, .21f, .12f)),
            new KeyValuePair<string, Color>("_BladeMid", new Color(.56f, .46f, .25f)),
            new KeyValuePair<string, Color>("_BladeTip", new Color(.83f, .68f, .40f)),
            new KeyValuePair<string, Color>("_BladeOlive", new Color(.47f, .47f, .25f)),
            new KeyValuePair<string, Color>("_HazeColor", new Color(.42f, .48f, .58f)),
            new KeyValuePair<string, Color>("_HazeSunColor", new Color(.85f, .80f, .72f)),
        };
        public static readonly KeyValuePair<string, float>[] GrassScalars =
        {
            new KeyValuePair<string, float>("_OliveAmount", .35f),
            new KeyValuePair<string, float>("_Translucency", 2.0f),
            new KeyValuePair<string, float>("_WindStrength", .16f),
            new KeyValuePair<string, float>("_ShellRange", 46f),
            new KeyValuePair<string, float>("_HazeDensity", .00009f),
            new KeyValuePair<string, float>("_HazeFloor", 1400f),
        };

        private static Material PrepareGrassMaterial(Transform site)
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(GrassShaderPath);
            if (!shader || ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("Launch Grass Shells V03 shader is missing or has compile errors.");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(GrassMaterialPath);
            if (!material) { material = new Material(shader) { name = "GrassShellsV03" }; AssetDatabase.CreateAsset(material, GrassMaterialPath); }
            material.shader = shader;
            material.SetVector("_SiteOriginWS", site.position);
            material.SetFloat("_DatumY", Datum);
            material.SetFloat("_ShellHeight", ShellHeight);
            material.SetVector("_WindDir", new Vector4(.82f, .57f, 0, 0));
            material.SetVector("_OuterFade", new Vector4(ShellRadius - 110f, ShellRadius - 10f, 0, 0));
            foreach (var entry in GrassPalette) material.SetColor(entry.Key, entry.Value);
            foreach (var entry in GrassScalars) material.SetFloat(entry.Key, entry.Value);
            material.renderQueue = (int)RenderQueue.AlphaTest;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static bool InsideRect(Vector3 p, float x0, float x1, float z0, float z1) => p.x >= x0 && p.x <= x1 && p.z >= z0 && p.z <= z1;

        /// <summary>Distance from the site centre to the apron footprint edge along an angle.</summary>
        private static float ApronRadius(float angle)
        {
            float c = Mathf.Abs(Mathf.Cos(angle)), s = Mathf.Abs(Mathf.Sin(angle));
            return Mathf.Min(c > 1e-5f ? 72f / c : float.MaxValue, s > 1e-5f ? 67f / s : float.MaxValue);
        }

        /// <summary>
        /// Shell base grid: rings that start on the apron edge and widen to ShellRadius, spacing 3.5 m near the apron
        /// growing to 14 m, zipped by angle; heights and normals are sampled from the real LT_TerrainInner mesh.
        /// </summary>
        private static void BuildShellBase(MeshFilter inner, Transform site, out Vector3[] sitePos, out Vector3[] siteNrm, out int[] tri)
        {
            var sampler = new SurfaceSampler(inner, site, ShellRadius + 120f);
            var rings = new List<Vector3[]>();
            float d = 0f;
            while (true)
            {
                float spacing = Mathf.Clamp(3.5f + .035f * d, 3.5f, 14f);
                int count = Mathf.Max(96, Mathf.RoundToInt(Mathf.PI * 2f * (80f + d) / spacing));
                var ring = new Vector3[count];
                for (int j = 0; j < count; j++)
                {
                    float a = -Mathf.PI + (j + .5f) * Mathf.PI * 2f / count;
                    float r = ApronRadius(a) + d;
                    ring[j] = new Vector3(Centre.x + Mathf.Cos(a) * r, Datum, Centre.y + Mathf.Sin(a) * r);
                }
                rings.Add(ring);
                if (d >= ShellRadius - 60f) break;
                d += spacing;
            }
            var positions = new List<Vector3>(); var normals = new List<Vector3>(); var triangles = new List<int>();
            var start = new int[rings.Count];
            for (int k = 0; k < rings.Count; k++)
            {
                start[k] = positions.Count;
                foreach (Vector3 p in rings[k])
                {
                    if (sampler.Sample(new Vector2(p.x, p.z), out float y, out Vector3 n)) { positions.Add(new Vector3(p.x, y, p.z)); normals.Add(n); }
                    else { positions.Add(p); normals.Add(Vector3.up); }
                }
            }
            for (int k = 0; k + 1 < rings.Count; k++)
            {
                var a = new Vector3[rings[k].Length]; var b = new Vector3[rings[k + 1].Length];
                for (int i = 0; i < a.Length; i++) a[i] = positions[start[k] + i];
                for (int i = 0; i < b.Length; i++) b[i] = positions[start[k + 1] + i];
                Zip(a, start[k], b, start[k + 1], triangles);
            }
            sitePos = positions.ToArray(); siteNrm = normals.ToArray(); tri = triangles.ToArray();
        }

        /// <summary>Height and smooth normal of the actual near terrain (site space), bucketed in 12 m cells.</summary>
        private sealed class SurfaceSampler
        {
            private const float Cell = 12f;
            private readonly Vector3[] p; private readonly Vector3[] n;
            private readonly Dictionary<long, List<int>> cells = new Dictionary<long, List<int>>();
            private readonly int[] tri;
            public SurfaceSampler(MeshFilter inner, Transform site, float radius)
            {
                Mesh mesh = inner.sharedMesh;
                Matrix4x4 toSite = site.worldToLocalMatrix * inner.transform.localToWorldMatrix;
                p = mesh.vertices.Select(v => toSite.MultiplyPoint3x4(v)).ToArray();
                Vector3[] normals = mesh.normals;
                n = normals != null && normals.Length == p.Length ? normals.Select(v => toSite.MultiplyVector(v).normalized).ToArray() : p.Select(_ => Vector3.up).ToArray();
                tri = mesh.triangles;
                for (int i = 0; i < tri.Length; i += 3)
                {
                    Vector3 a = p[tri[i]], b = p[tri[i + 1]], c = p[tri[i + 2]];
                    if (Flat((a + b + c) / 3f).magnitude > radius) continue;
                    int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x, c.x) / Cell), x1 = Mathf.FloorToInt(Mathf.Max(a.x, b.x, c.x) / Cell);
                    int z0 = Mathf.FloorToInt(Mathf.Min(a.z, b.z, c.z) / Cell), z1 = Mathf.FloorToInt(Mathf.Max(a.z, b.z, c.z) / Cell);
                    for (int x = x0; x <= x1; x++)
                        for (int z = z0; z <= z1; z++)
                        {
                            long key = ((long)x << 32) ^ (uint)z;
                            if (!cells.TryGetValue(key, out var list)) { list = new List<int>(); cells.Add(key, list); }
                            list.Add(i);
                        }
                }
            }
            public bool Sample(Vector2 q, out float y, out Vector3 normal)
            {
                y = 0; normal = Vector3.up;
                long key = ((long)Mathf.FloorToInt(q.x / Cell) << 32) ^ (uint)Mathf.FloorToInt(q.y / Cell);
                if (!cells.TryGetValue(key, out var list)) return false;
                bool found = false; float best = float.NegativeInfinity;
                foreach (int i in list)
                {
                    Vector3 a = p[tri[i]], b = p[tri[i + 1]], c = p[tri[i + 2]];
                    float den = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                    if (Mathf.Abs(den) < 1e-7f) continue;
                    float wa = ((b.z - c.z) * (q.x - c.x) + (c.x - b.x) * (q.y - c.z)) / den;
                    float wb = ((c.z - a.z) * (q.x - c.x) + (a.x - c.x) * (q.y - c.z)) / den;
                    float wc = 1 - wa - wb;
                    if (wa < -1e-4f || wb < -1e-4f || wc < -1e-4f) continue;
                    float h = wa * a.y + wb * b.y + wc * c.y;
                    if (h > best) { best = h; found = true; normal = (n[tri[i]] * wa + n[tri[i + 1]] * wb + n[tri[i + 2]] * wc).normalized; }
                }
                y = best;
                return found;
            }
        }

        /// <summary>
        /// Raises the shell base grid into ShellLayers layers (UV0.x = layer height fraction), split into angular
        /// sectors. Triangles wholly on the crawlerway or in the plume corridor are left out; the shader decides coverage.
        /// </summary>
        private static Mesh[] SaveGrassShells(MeshFilter inner, Transform site, out long triangleCount)
        {
            BuildShellBase(inner, site, out Vector3[] sitePos, out Vector3[] siteNrm, out int[] tri);
            var sectorTris = new List<int>[GrassSectorCount];
            for (int s = 0; s < GrassSectorCount; s++) sectorTris[s] = new List<int>();
            for (int i = 0; i < tri.Length; i += 3)
            {
                Vector3 a = sitePos[tri[i]], b = sitePos[tri[i + 1]], c = sitePos[tri[i + 2]];
                Vector3 centroid = (a + b + c) / 3f;
                if (Flat(centroid).magnitude > ShellRadius) continue;
                bool road = InsideRect(a, -24, 24, -915, -55) && InsideRect(b, -24, 24, -915, -55) && InsideRect(c, -24, 24, -915, -55);
                bool plume = InsideRect(a, -30, 30, 62, 335) && InsideRect(b, -30, 30, 62, 335) && InsideRect(c, -30, 30, 62, 335);
                if (road || plume) continue;
                int sector = Mathf.Clamp((int)((AngleOf(centroid) + Mathf.PI) / (Mathf.PI * 2f) * GrassSectorCount), 0, GrassSectorCount - 1);
                sectorTris[sector].Add(tri[i]); sectorTris[sector].Add(tri[i + 1]); sectorTris[sector].Add(tri[i + 2]);
            }
            EnsureFolder(GrassMeshFolder);
            var result = new Mesh[GrassSectorCount];
            triangleCount = 0;
            for (int s = 0; s < GrassSectorCount; s++)
            {
                var remap = new Dictionary<int, int>();
                var used = new List<int>();
                foreach (int index in sectorTris[s]) if (!remap.ContainsKey(index)) { remap.Add(index, used.Count); used.Add(index); }
                var packed = new List<ShellVertex>(used.Count * ShellLayers);
                var indices = new List<int>(sectorTris[s].Count * ShellLayers);
                Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
                for (int layer = 1; layer <= ShellLayers; layer++)
                {
                    float t = layer / (float)ShellLayers;
                    int baseIndex = packed.Count;
                    foreach (int index in used)
                    {
                        Vector3 p = sitePos[index] + Vector3.up * (t * ShellHeight);
                        Vector3 n = siteNrm[index];
                        packed.Add(new ShellVertex
                        {
                            Position = p,
                            Nx = (short)Mathf.RoundToInt(Mathf.Clamp(n.x, -1, 1) * 32767f),
                            Ny = (short)Mathf.RoundToInt(Mathf.Clamp(n.y, -1, 1) * 32767f),
                            Nz = (short)Mathf.RoundToInt(Mathf.Clamp(n.z, -1, 1) * 32767f),
                            U = Mathf.FloatToHalf(t), V = 0,
                        });
                        min = Vector3.Min(min, p); max = Vector3.Max(max, p);
                    }
                    foreach (int index in sectorTris[s]) indices.Add(baseIndex + remap[index]);
                }
                string name = "Grass shells V03 sector " + s.ToString("00");
                string path = GrassMeshFolder + "/" + name + ".asset";
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                bool created = !mesh;
                if (created) mesh = new Mesh();
                mesh.Clear();
                mesh.name = name;
                mesh.SetVertexBufferParams(packed.Count,
                    new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                    new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.SNorm16, 4),
                    new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, 2));
                mesh.SetVertexBufferData(packed, 0, 0, packed.Count, 0, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds);
                IndexFormat format = packed.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.SetIndexBufferParams(indices.Count, format);
                if (format == IndexFormat.UInt16) mesh.SetIndexBufferData(indices.Select(i => (ushort)i).ToArray(), 0, 0, indices.Count, MeshUpdateFlags.DontRecalculateBounds);
                else mesh.SetIndexBufferData(indices, 0, 0, indices.Count, MeshUpdateFlags.DontRecalculateBounds);
                var bounds = packed.Count > 0 ? new Bounds((min + max) * .5f, max - min) : new Bounds();
                mesh.subMeshCount = 1;
                mesh.SetSubMesh(0, new SubMeshDescriptor(0, indices.Count) { bounds = bounds, firstVertex = 0, vertexCount = packed.Count }, MeshUpdateFlags.DontRecalculateBounds);
                mesh.bounds = bounds;
                mesh.UploadMeshData(false);
                if (created) AssetDatabase.CreateAsset(mesh, path); else EditorUtility.SetDirty(mesh);
                result[s] = mesh;
                triangleCount += indices.Count / 3;
            }
            return result;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct ShellVertex
        {
            public Vector3 Position;
            public short Nx, Ny, Nz, Nw;
            public ushort U, V;
        }

        // ---------- colliders ----------

        private static List<string> ColliderState(Transform site) =>
            site.GetComponentsInChildren<Collider>(true)
                .Select(c => c.GetInstanceID() + ":" + (c.enabled && c.gameObject.activeInHierarchy))
                .OrderBy(s => s, StringComparer.Ordinal).ToList();

        // ---------- boundary of the existing near terrain ----------

        private sealed class BoundaryRing
        {
            public readonly List<Vector3> Points = new List<Vector3>();   // site-local, sorted by angle
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<float> Angles = new List<float>();
            public float RadiusAt(float angle) => Interpolate(angle, i => Flat(Points[i]).magnitude);
            public float HeightAt(float angle) => Interpolate(angle, i => Points[i].y);
            private float Interpolate(float angle, Func<int, float> value)
            {
                int n = Angles.Count;
                int hi = Angles.BinarySearch(angle);
                if (hi >= 0) return value(hi);
                hi = ~hi;
                int lo = (hi - 1 + n) % n; hi %= n;
                float a0 = Angles[lo], a1 = Angles[hi];
                if (a1 <= a0) a1 += Mathf.PI * 2;
                float a = angle < a0 ? angle + Mathf.PI * 2 : angle;
                float t = Mathf.Clamp01((a - a0) / Mathf.Max(1e-6f, a1 - a0));
                return Mathf.Lerp(value(lo), value(hi), t);
            }
        }

        private static Vector2 Flat(Vector3 p) => new Vector2(p.x - Centre.x, p.z - Centre.y);
        private static float AngleOf(Vector3 p) { Vector2 q = Flat(p); return Mathf.Atan2(q.y, q.x); }

        private static BoundaryRing OuterBoundary(MeshFilter inner, Transform site)
        {
            Mesh mesh = inner.sharedMesh;
            if (!mesh.isReadable) throw new InvalidOperationException("LT_TerrainInner mesh must be readable.");
            Vector3[] v = mesh.vertices; Vector3[] nrm = mesh.normals; int[] tri = mesh.triangles;
            Matrix4x4 toSite = site.worldToLocalMatrix * inner.transform.localToWorldMatrix;
            var weldIndex = new Dictionary<long, int>();
            var welded = new int[v.Length];
            var pos = new List<Vector3>(); var nor = new List<Vector3>();
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 p = toSite.MultiplyPoint3x4(v[i]);
                long key = ((long)Mathf.RoundToInt(p.x * 50f) << 32) ^ (uint)Mathf.RoundToInt(p.z * 50f);
                if (!weldIndex.TryGetValue(key, out int w)) { w = pos.Count; weldIndex.Add(key, w); pos.Add(p); nor.Add(Vector3.zero); }
                welded[i] = w;
                if (nrm != null && nrm.Length == v.Length) nor[w] += toSite.MultiplyVector(nrm[i]);
            }
            var edgeCount = new Dictionary<long, int>();
            for (int i = 0; i < tri.Length; i += 3)
                for (int e = 0; e < 3; e++)
                {
                    int a = welded[tri[i + e]], b = welded[tri[i + (e + 1) % 3]];
                    if (a == b) continue;
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    edgeCount[key] = edgeCount.TryGetValue(key, out int c) ? c + 1 : 1;
                }
            var boundaryVertices = new HashSet<int>();
            foreach (var pair in edgeCount)
                if (pair.Value == 1) { boundaryVertices.Add((int)(pair.Key >> 32)); boundaryVertices.Add((int)(pair.Key & 0xffffffff)); }
            // The outer loop is kilometres out; the footprint hole loop lies within ~100 m of the centre.
            float maxRadius = boundaryVertices.Max(i => Flat(pos[i]).magnitude);
            var outer = boundaryVertices.Where(i => Flat(pos[i]).magnitude > maxRadius * .5f)
                .OrderBy(i => AngleOf(pos[i])).ToList();
            if (outer.Count < 64 || maxRadius < 2500f)
                throw new InvalidOperationException("Unexpected LT_TerrainInner outer boundary: " + outer.Count + " vertices, r=" + maxRadius);
            var ring = new BoundaryRing();
            foreach (int i in outer)
            {
                ring.Points.Add(pos[i]);
                Vector3 n = nor[i].sqrMagnitude > 1e-8f ? nor[i].normalized : Vector3.up;
                ring.Normals.Add(n);
                ring.Angles.Add(AngleOf(pos[i]));
            }
            for (int i = 1; i < ring.Angles.Count; i++)
                if (ring.Angles[i] <= ring.Angles[i - 1]) throw new InvalidOperationException("Duplicate boundary angle at " + i);
            return ring;
        }

        // ---------- rings and heights ----------

        private sealed class Ring
        {
            public Vector3[] Points;
            public float Radius;     // nominal radius (circle rings) or 0 for the seam
            public bool IsSeam;
        }

        private static List<Ring> BuildRings(BoundaryRing boundary)
        {
            var rings = new List<Ring> { new Ring { Points = boundary.Points.ToArray(), Radius = 0, IsSeam = true } };
            const float morphRadius = 4700f;
            const int morphRings = 12;
            for (int k = 1; k <= morphRings; k++)
            {
                float t = k / (float)morphRings;
                rings.Add(CircleLike(640, boundary, a => Mathf.Lerp(boundary.RadiusAt(a), morphRadius, t), Mathf.Lerp(3000f, morphRadius, t)));
            }
            for (float r = morphRadius + 100f; r <= 6000f; r += 100f) rings.Add(CircleLike(640, boundary, a => r, r));
            for (float r = 6060f; r <= 10200f; r += 60f) rings.Add(CircleLike(1280, boundary, a => r, r));
            return rings;
        }

        private static Ring CircleLike(int count, BoundaryRing boundary, Func<float, float> radiusAt, float nominal)
        {
            var points = new Vector3[count];
            for (int j = 0; j < count; j++)
            {
                float a = -Mathf.PI + (j + .5f) * Mathf.PI * 2f / count;
                float r = radiusAt(a);
                float x = Centre.x + Mathf.Cos(a) * r, z = Centre.y + Mathf.Sin(a) * r;
                float seamR = boundary.RadiusAt(a);
                float blend = Smooth(0f, 750f, r - seamR);
                float y = Mathf.Lerp(boundary.HeightAt(a), Datum + Height(x, z), blend);
                points[j] = new Vector3(x, y, z);
            }
            return new Ring { Points = points, Radius = nominal };
        }

        /// <summary>Height above the datum of the far land (site-local metres). Fictional Earth scenery.</summary>
        public static float Height(float x, float z)
        {
            float qx = x - Centre.x, qz = z - Centre.y;
            float r = Mathf.Sqrt(qx * qx + qz * qz);
            float angle = Mathf.Atan2(qz, qx);
            // Rolling grassland foothills that rise gently away from the site.
            float rolling = (Fbm(x / 1500f + 37.1f, z / 1500f + 11.9f, 4) - .5f) * 2f;
            float foothills = Mathf.Lerp(2f, 46f, Smooth(3000f, 6800f, r)) + rolling * Mathf.Lerp(7f, 62f, Smooth(3000f, 6800f, r));
            // Mountain range: angle-varying envelope (massifs and lower passes), ridged multifractal relief.
            float ca = Mathf.Cos(angle), sa = Mathf.Sin(angle);
            float rangeNoise = Fbm(ca * 1.6f + 71.3f, sa * 1.6f + 23.7f, 3);
            float rangeHeight = Mathf.Lerp(200f, 900f, Smooth(.25f, .80f, rangeNoise));
            float front = 6200f + (Fbm(ca * 2.3f + 5.1f, sa * 2.3f + 91.4f, 2) - .5f) * 1400f;
            float rise = Smooth(front - 1600f, front + 1400f, r);
            float fall = 1f - .45f * Smooth(9400f, 10300f, r);
            float wx = x + (Mathf.PerlinNoise(x / 5200f + 13.3f, z / 5200f + 7.7f) - .5f) * 1900f;
            float wz = z + (Mathf.PerlinNoise(x / 5200f + 41.9f, z / 5200f + 63.1f) - .5f) * 1900f;
            float ridges = RidgedFbm(wx / 3600f + 101.7f, wz / 3600f + 55.3f, 6);
            float mountains = rise * fall * rangeHeight * (.12f + .88f * ridges);
            return Mathf.Max(foothills + mountains, -3f);
        }

        private static float Fbm(float x, float z, int octaves)
        {
            float sum = 0, amp = .5f, norm = 0;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * Mathf.PerlinNoise(x, z); norm += amp;
                float nx = x * 1.62f - z * 1.18f + 19.3f, nz = x * 1.18f + z * 1.62f + 7.1f;
                x = nx; z = nz; amp *= .5f;
            }
            return sum / norm;
        }

        private static float RidgedFbm(float x, float z, int octaves)
        {
            float sum = 0, amp = .5f, norm = 0, weight = 1f;
            for (int i = 0; i < octaves; i++)
            {
                float n = Mathf.PerlinNoise(x, z);
                float s = 1f - Mathf.Abs(2f * n - 1f);
                s = s * s * weight;
                weight = Mathf.Clamp01(s * 1.8f);
                sum += s * amp; norm += amp;
                float nx = x * 2.07f - z * .31f + 29.7f, nz = x * .31f + z * 2.07f + 3.9f;
                x = nx; z = nz; amp *= .52f;
            }
            return Mathf.Clamp01(sum / norm * 1.35f);
        }

        private static float Smooth(float low, float high, float value)
        {
            float t = Mathf.Clamp01((value - low) / (high - low));
            return t * t * (3f - 2f * t);
        }

        // ---------- mesh ----------

        private static void BuildMesh(List<Ring> rings, BoundaryRing boundary, out List<Vector3> vertices, out List<Vector3> normals, out List<int> triangles)
        {
            vertices = new List<Vector3>(); triangles = new List<int>();
            var start = new int[rings.Count];
            for (int k = 0; k < rings.Count; k++) { start[k] = vertices.Count; vertices.AddRange(rings[k].Points); }
            for (int k = 0; k + 1 < rings.Count; k++)
                Zip(rings[k].Points, start[k], rings[k + 1].Points, start[k + 1], triangles);
            // Smooth per-vertex normals from the actual surface, then the seam takes the near terrain's normals.
            var accum = new Vector3[vertices.Count];
            for (int i = 0; i < triangles.Count; i += 3)
            {
                Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                Vector3 face = Vector3.Cross(b - a, c - a);
                if (face.y <= 0) throw new InvalidOperationException("Far land triangle faces down at " + a);
                accum[triangles[i]] += face; accum[triangles[i + 1]] += face; accum[triangles[i + 2]] += face;
            }
            normals = accum.Select(n => n.normalized).ToList();
            for (int i = 0; i < boundary.Normals.Count; i++) normals[i] = boundary.Normals[i];
        }

        /// <summary>Stitches two closed, angle-sorted rings (inner A, outer B) with upward-facing triangles.</summary>
        private static void Zip(Vector3[] a, int aStart, Vector3[] b, int bStart, List<int> triangles)
        {
            int na = a.Length, nb = b.Length;
            const float Tau = Mathf.PI * 2f;
            float a0 = AngleOf(a[0]);                     // a is sorted ascending, so a[0] has the smallest angle
            float Unwrap(float angle) { float d = angle - a0; while (d < 0f) d += Tau; while (d >= Tau) d -= Tau; return d; }
            // Outer start: the first vertex at or after a[0] going counter-clockwise.
            int jb = 0; float best = float.MaxValue;
            for (int j = 0; j < nb; j++) { float d = Unwrap(AngleOf(b[j])); if (d < best) { best = d; jb = j; } }
            var ua = new float[na + 1];
            for (int i = 0; i < na; i++) ua[i] = Unwrap(AngleOf(a[i]));
            ua[0] = 0f; ua[na] = Tau;
            var ub = new float[nb + 1];
            for (int k = 0; k < nb; k++) ub[k] = Unwrap(AngleOf(b[(jb + k) % nb]));
            ub[nb] = ub[0] + Tau;
            for (int i = 1; i <= na; i++) if (ua[i] < ua[i - 1]) throw new InvalidOperationException("Inner ring is not angle-sorted.");
            for (int k = 1; k <= nb; k++) if (ub[k] < ub[k - 1]) throw new InvalidOperationException("Outer ring is not angle-sorted.");
            int ia = 0, ib = 0;
            while (ia < na || ib < nb)
            {
                bool advanceA = ib >= nb || (ia < na && ua[ia + 1] <= ub[ib + 1]);
                int ca = aStart + ia % na, cb = bStart + (jb + ib) % nb;
                if (advanceA)
                {
                    triangles.Add(ca); triangles.Add(aStart + (ia + 1) % na); triangles.Add(cb);
                    ia++;
                }
                else
                {
                    triangles.Add(ca); triangles.Add(bStart + (jb + ib + 1) % nb); triangles.Add(cb);
                    ib++;
                }
            }
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct PackedVertex
        {
            public Vector3 Position;
            public short Nx, Ny, Nz, Nw;
        }

        private static Mesh[] SaveSectors(List<Vector3> vertices, List<Vector3> normals, List<int> triangles)
        {
            var sectorTriangles = new List<int>[SectorCount];
            for (int s = 0; s < SectorCount; s++) sectorTriangles[s] = new List<int>();
            for (int i = 0; i < triangles.Count; i += 3)
            {
                Vector3 c = (vertices[triangles[i]] + vertices[triangles[i + 1]] + vertices[triangles[i + 2]]) / 3f;
                float a = AngleOf(c) + Mathf.PI;
                int s = Mathf.Clamp((int)(a / (Mathf.PI * 2f) * SectorCount), 0, SectorCount - 1);
                sectorTriangles[s].Add(triangles[i]); sectorTriangles[s].Add(triangles[i + 1]); sectorTriangles[s].Add(triangles[i + 2]);
            }
            EnsureFolder(MeshFolder);
            var result = new Mesh[SectorCount];
            for (int s = 0; s < SectorCount; s++)
            {
                var remap = new Dictionary<int, int>();
                var packed = new List<PackedVertex>();
                var indices = new List<int>(sectorTriangles[s].Count);
                Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
                foreach (int index in sectorTriangles[s])
                {
                    if (!remap.TryGetValue(index, out int local))
                    {
                        local = packed.Count; remap.Add(index, local);
                        Vector3 n = normals[index];
                        packed.Add(new PackedVertex
                        {
                            Position = vertices[index],
                            Nx = (short)Mathf.RoundToInt(Mathf.Clamp(n.x, -1, 1) * 32767f),
                            Ny = (short)Mathf.RoundToInt(Mathf.Clamp(n.y, -1, 1) * 32767f),
                            Nz = (short)Mathf.RoundToInt(Mathf.Clamp(n.z, -1, 1) * 32767f),
                        });
                        min = Vector3.Min(min, vertices[index]); max = Vector3.Max(max, vertices[index]);
                    }
                    indices.Add(local);
                }
                string name = "Far land V03 sector " + s.ToString("00");
                string path = MeshFolder + "/" + name + ".asset";
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                bool created = !mesh;
                if (created) mesh = new Mesh();
                mesh.Clear();
                mesh.name = name;
                mesh.SetVertexBufferParams(packed.Count,
                    new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                    new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.SNorm16, 4));
                mesh.SetVertexBufferData(packed, 0, 0, packed.Count, 0, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds);
                IndexFormat format = packed.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.SetIndexBufferParams(indices.Count, format);
                if (format == IndexFormat.UInt16) mesh.SetIndexBufferData(indices.Select(i => (ushort)i).ToArray(), 0, 0, indices.Count, MeshUpdateFlags.DontRecalculateBounds);
                else mesh.SetIndexBufferData(indices, 0, 0, indices.Count, MeshUpdateFlags.DontRecalculateBounds);
                var bounds = new Bounds((min + max) * .5f, max - min);
                mesh.subMeshCount = 1;
                mesh.SetSubMesh(0, new SubMeshDescriptor(0, indices.Count) { bounds = bounds, firstVertex = 0, vertexCount = packed.Count }, MeshUpdateFlags.DontRecalculateBounds);
                mesh.bounds = bounds;
                mesh.UploadMeshData(false);
                if (created) AssetDatabase.CreateAsset(mesh, path); else EditorUtility.SetDirty(mesh);
                result[s] = mesh;
            }
            return result;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
