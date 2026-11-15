using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OutOfReach.Visuals.LaunchComplexV01.Editor
{
    /// <summary>Rebuilds the LaunchComplexV01 texture/model import settings, the URP Lit materials and the two drop-in
    /// prefabs (launch vehicle, pad dressing) from the Blender export. Idempotent. Never touches a scene.</summary>
    public static class LaunchComplexV01Builder
    {
        public const string Root = "Assets/_OutOfReach/Visuals/Models/LaunchComplexV01";
        public const string VehicleFbx = Root + "/Models/LC1_LaunchVehicle.fbx";
        public const string PadFbx = Root + "/Models/LC1_PadDressing.fbx";
        public const string VehiclePrefab = Root + "/Prefabs/LC1_LaunchVehicle.prefab";
        public const string PadPrefab = Root + "/Prefabs/LC1_PadDressing.prefab";
        public const string VehicleModule = "LC1_Vehicle";
        public static readonly string[] PadModules =
            { "LC1_Mount", "LC1_Trench", "LC1_Deluge", "LC1_TankFarm", "LC1_FloodTowers", "LC1_Lightning", "LC1_Vehicles", "LC1_Markings" };

        // name -> (metallic, smoothness, normal strength). Scalars on purpose: no metallic/smoothness map, so a missing
        // or mis-imported texture can never turn a painted surface into a mirror of the sky. Metallic stays moderate
        // because the prologue has no reflection probe.
        private static readonly Dictionary<string, Vector3> Textured = new Dictionary<string, Vector3>
        {
            ["LC1_HullWhite"] = new Vector3(0f, .42f, .9f), ["LC1_HullGraphite"] = new Vector3(0f, .40f, 1f),
            ["LC1_Amber"] = new Vector3(0f, .40f, .8f), ["LC1_Red"] = new Vector3(0f, .40f, .8f),
            ["LC1_Steel"] = new Vector3(0f, .36f, .8f), ["LC1_Galvanized"] = new Vector3(.5f, .36f, .6f),
            ["LC1_Concrete"] = new Vector3(0f, .16f, 1f), ["LC1_ConcreteSoot"] = new Vector3(0f, .12f, 1f),
            ["LC1_Hazard"] = new Vector3(0f, .35f, 0f), ["LC1_Nozzle"] = new Vector3(.55f, .52f, 1f),
            ["LC1_Foil"] = new Vector3(.25f, .62f, 1.2f), ["LC1_Rubber"] = new Vector3(0f, .2f, .8f),
            ["LC1_MarkWhite"] = new Vector3(0f, .25f, .6f),
        };
        private static readonly Dictionary<string, Color> Lamps = new Dictionary<string, Color>
        {
            ["LC1_LampWarm"] = new Color(1f, .8f, .55f) * 3f,
            ["LC1_LampRed"] = new Color(1f, .06f, .03f) * 4f,
            ["LC1_LampCyan"] = new Color(.2f, .9f, 1f) * 2.5f,
        };
        private const string Glass = "LC1_GlassDark";

        [MenuItem("OUT OF REACH/Launch Complex V01/Rebuild Materials and Prefabs", priority = 50)]
        public static void BuildAll()
        {
            var report = new StringBuilder();
            ConfigureTextures(report);
            Dictionary<string, Material> materials = BuildMaterials(report);
            ConfigureModel(VehicleFbx, materials, report);
            ConfigureModel(PadFbx, materials, report);
            BuildVehiclePrefab(report);
            BuildPadPrefab(report);
            AssetDatabase.SaveAssets();
            Debug.Log("LAUNCH_COMPLEX_V01_BUILD OK\n" + report);
        }

        /// <summary>True when a prefab is missing or one of its textured materials lost its base map (un-smudged LFS).</summary>
        public static bool NeedsBuild()
        {
            foreach (string path in new[] { VehiclePrefab, PadPrefab })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!prefab) return true;
                foreach (MeshRenderer r in prefab.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var filter = r.GetComponent<MeshFilter>();
                    if (!filter || !filter.sharedMesh) return true;
                    foreach (Material m in r.sharedMaterials)
                        if (!m || (Textured.ContainsKey(m.name) && !m.GetTexture("_BaseMap"))) return true;
                }
            }
            return false;
        }

        private static void ConfigureTextures(StringBuilder report)
        {
            int count = 0;
            foreach (string path in Directory.GetFiles(Root + "/Textures", "*.png").Select(p => p.Replace('\\', '/')))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path)
                    ?? throw new InvalidOperationException("Texture not imported: " + path);
                bool normal = path.EndsWith("_Normal.png", StringComparison.Ordinal);
                bool changed = false;
                void Set<T>(T current, T wanted, Action<T> apply) { if (!EqualityComparer<T>.Default.Equals(current, wanted)) { apply(wanted); changed = true; } }
                Set(importer.textureType, normal ? TextureImporterType.NormalMap : TextureImporterType.Default, v => importer.textureType = v);
                Set(importer.sRGBTexture, !normal, v => importer.sRGBTexture = v);
                Set(importer.mipmapEnabled, true, v => importer.mipmapEnabled = v);
                Set(importer.anisoLevel, 6, v => importer.anisoLevel = v);
                Set(importer.wrapMode, TextureWrapMode.Repeat, v => importer.wrapMode = v);
                Set(importer.maxTextureSize, 1024, v => importer.maxTextureSize = v);
                if (changed) importer.SaveAndReimport();
                count++;
            }
            report.AppendLine("textures configured: " + count);
        }

        private static Texture2D Tex(string name) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + name + ".png");

        private static Dictionary<string, Material> BuildMaterials(StringBuilder report)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit")
                ?? throw new InvalidOperationException("URP Lit shader not found.");
            Directory.CreateDirectory(Root + "/Materials");
            var result = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (string name in Textured.Keys.Concat(Lamps.Keys).Concat(new[] { Glass }))
            {
                string path = Root + "/Materials/" + name + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!mat)
                {
                    mat = new Material(lit) { name = name };
                    AssetDatabase.CreateAsset(mat, path);
                }
                mat.shader = lit;
                mat.SetFloat("_WorkflowMode", 1f);
                mat.SetFloat("_Surface", 0f);
                mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_Cull", 2f);
                mat.SetFloat("_AlphaClip", 0f);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.DisableKeyword("_METALLICSPECGLOSSMAP");
                mat.SetTexture("_MetallicGlossMap", null);
                // One keyword set (_NORMALMAP + _EMISSION) for every LC1 material: a single URP Lit variant.
                mat.EnableKeyword("_NORMALMAP");
                mat.EnableKeyword("_EMISSION");
                mat.SetTexture("_BumpMap", null);
                mat.SetColor("_EmissionColor", Color.black);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                mat.renderQueue = -1;
                if (Textured.TryGetValue(name, out Vector3 scalars))
                {
                    Texture2D baseMap = Tex(name + "_BaseColor") ?? throw new InvalidOperationException("Missing texture " + name
                        + "_BaseColor.png (Git LFS pointer not smudged?)");
                    mat.SetTexture("_BaseMap", baseMap);
                    mat.SetTexture("_MainTex", baseMap);
                    mat.SetFloat("_Metallic", scalars.x);
                    mat.SetFloat("_Smoothness", scalars.y);
                    mat.SetFloat("_SmoothnessTextureChannel", 0f);
                    mat.SetTexture("_BumpMap", Tex(name + "_Normal"));
                    mat.SetFloat("_BumpScale", scalars.z);
                }
                else if (Lamps.TryGetValue(name, out Color emission))
                {
                    mat.SetColor("_BaseColor", emission / emission.maxColorComponent);
                    mat.SetFloat("_Metallic", 0f);
                    mat.SetFloat("_Smoothness", .85f);
                    mat.SetColor("_EmissionColor", emission);
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                }
                else
                {
                    mat.SetColor("_BaseColor", new Color(.02f, .025f, .03f, 1f));
                    mat.SetFloat("_Metallic", 0f);
                    mat.SetFloat("_Smoothness", .7f);
                }
                EditorUtility.SetDirty(mat);
                result.Add(name, mat);
            }
            report.AppendLine("materials " + result.Count + " URP Lit");
            return result;
        }

        private static void ConfigureModel(string fbx, Dictionary<string, Material> materials, StringBuilder report)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbx)
                ?? throw new InvalidOperationException("FBX not imported: " + fbx);
            importer.bakeAxisConversion = true;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.preserveHierarchy = true;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.importVisibility = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.weldVertices = true;
            importer.generateSecondaryUV = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            foreach (var pair in materials)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
            importer.SaveAndReimport();
            report.AppendLine("model importer " + Path.GetFileName(fbx) + ": bakeAxisConversion, scale 1, " + materials.Count + " material remaps");
        }

        private static void BuildVehiclePrefab(StringBuilder report)
        {
            GameObject model = Model(VehicleFbx);
            Directory.CreateDirectory(Root + "/Prefabs");
            var root = new GameObject("LC1_LaunchVehicle");
            try
            {
                Transform[] lods = Lods(model, VehicleModule, 3, root.transform);
                // Same frame as the old BoosterStack: identity under "Transport vehicle - root controls launch".
                AddLodGroup(root, lods, new[] { .18f, .05f, .004f });
                Anchor("Anchor_EngineEmitter", root.transform, new Vector3(0f, -31.4f, 0f));
                Anchor("Anchor_NozzleExitPlane", root.transform, new Vector3(0f, -31.62f, 0f));
                Anchor("Anchor_CapsuleInterface", root.transform, new Vector3(0f, -.55f, 0f));
                Save(root, VehiclePrefab, report);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void BuildPadPrefab(StringBuilder report)
        {
            GameObject model = Model(PadFbx);
            var root = new GameObject("LC1_PadDressing");
            try
            {
                foreach (string module in PadModules)
                {
                    var holder = new GameObject(module);
                    holder.transform.SetParent(root.transform, false);
                    bool single = module == "LC1_Markings";
                    Transform[] lods = Lods(model, module, single ? 1 : 2, holder.transform);
                    if (!single) AddLodGroup(holder, lods, new[] { .09f, .0015f });
                }
                Anchor("Anchor_FloodLamp_East", root.transform, new Vector3(46f, -4f, 45.7f));
                Anchor("Anchor_FloodLamp_West", root.transform, new Vector3(-46f, -4f, 45.7f));
                Anchor("Anchor_DelugeHeader", root.transform, new Vector3(0f, -31.22f, -4f));
                Anchor("Anchor_CatenaryCentre", root.transform, new Vector3(0f, 43f, 2f));
                Save(root, PadPrefab, report);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static GameObject Model(string path) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? throw new InvalidOperationException("Missing " + path);

        private static Transform[] Lods(GameObject model, string module, int count, Transform parent)
        {
            var result = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                string name = module + "_LOD" + i;
                Transform source = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name)
                    ?? throw new InvalidOperationException("FBX lacks " + name);
                Vector3 p = model.transform.InverseTransformPoint(source.position);
                if (p.sqrMagnitude > 1e-6f || Quaternion.Angle(source.rotation, model.transform.rotation) > .01f
                    || (source.lossyScale - Vector3.one).sqrMagnitude > 1e-6f)
                    throw new InvalidOperationException("Imported axes/scale differ for " + name + ": " + p.ToString("F4")
                        + " " + source.rotation.eulerAngles.ToString("F2") + " " + source.lossyScale.ToString("F4"));
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                if (renderer.sharedMaterials.Any(m => !m || !m.name.StartsWith("LC1_", StringComparison.Ordinal)))
                    throw new InvalidOperationException(name + " has a missing or non-LC1 material.");
                result[i] = go.transform;
            }
            return result;
        }

        private static void AddLodGroup(GameObject holder, Transform[] lods, float[] heights)
        {
            var group = holder.AddComponent<LODGroup>();
            var array = new LOD[lods.Length];
            for (int i = 0; i < lods.Length; i++)
                array[i] = new LOD(heights[i], new Renderer[] { lods[i].GetComponent<Renderer>() });
            group.fadeMode = LODFadeMode.None;
            group.SetLODs(array);
            group.RecalculateBounds();
        }

        private static void Anchor(string name, Transform parent, Vector3 prologuePoint)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = prologuePoint;
        }

        private static void Save(GameObject root, string path, StringBuilder report)
        {
            var parts = new List<string>();
            foreach (MeshFilter f in root.GetComponentsInChildren<MeshFilter>(true))
                parts.Add(f.name + "=" + TriangleCount(f.sharedMesh));
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool ok);
            if (!ok) throw new InvalidOperationException("Could not save " + path);
            report.AppendLine("prefab " + path + " triangles: " + string.Join(", ", parts));
        }

        public static int TriangleCount(Mesh mesh)
        {
            long count = 0;
            for (int i = 0; i < mesh.subMeshCount; i++) count += mesh.GetIndexCount(i) / 3;
            return (int)count;
        }
    }
}
