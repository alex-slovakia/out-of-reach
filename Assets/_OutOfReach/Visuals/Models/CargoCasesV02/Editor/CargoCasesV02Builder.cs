using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OutOfReach.Visuals.CargoCasesV02.Editor
{
    /// <summary>Builds the CC2 URP Lit materials, configures the FBX/texture importers and writes one prefab per
    /// container (LODGroup, three levels) plus the rack frame and the per-case rack cells. Idempotent.</summary>
    public static class CargoCasesV02Builder
    {
        public const string Root = "Assets/_OutOfReach/Visuals/Models/CargoCasesV02";

        public sealed class CaseDef
        {
            public string Key, EquipmentId, DisplayName, ShortName;
            public string ModelPath => Root + "/Models/CC2_" + Key + ".fbx";
            public string PrefabPath => Root + "/Prefabs/CC2_" + Key + ".prefab";
            public string CellModelPath => Root + "/Models/CC2_RackCell_" + Key + ".fbx";
            public string CellPrefabPath => Root + "/Prefabs/CC2_RackCell_" + Key + ".prefab";
        }

        // Canonical C05 equipment ids; display names as CargoVisualPresenter writes them, short names as CargoSignText.
        public static readonly CaseDef[] Cases =
        {
            new CaseDef { Key = "Oxygen", EquipmentId = "oxygen-tank", DisplayName = "OXYGEN TANK", ShortName = "OXYGEN" },
            new CaseDef { Key = "Water", EquipmentId = "water-tank", DisplayName = "WATER TANK", ShortName = "WATER" },
            new CaseDef { Key = "Food", EquipmentId = "food-locker", DisplayName = "FOOD LOCKER", ShortName = "FOOD" },
            new CaseDef { Key = "Battery", EquipmentId = "battery-module", DisplayName = "BATTERY MODULE", ShortName = "BATTERY" },
            new CaseDef { Key = "Solar", EquipmentId = "solar-wing", DisplayName = "SOLAR WING", ShortName = "SOLAR" },
            new CaseDef { Key = "Spares", EquipmentId = "spare-parts-kit", DisplayName = "SPARE PARTS KIT", ShortName = "SPARES" },
            new CaseDef { Key = "Science", EquipmentId = "spectrometer", DisplayName = "SPECTROMETER", ShortName = "SCIENCE" },
        };

        public const string RackKey = "RackFrame";
        public static string RackModelPath => Root + "/Models/CC2_" + RackKey + ".fbx";
        public static string RackPrefabPath => Root + "/Prefabs/CC2_" + RackKey + ".prefab";

        private struct Mat
        {
            public string Texture; public Color Tint; public float Metallic, Smoothness, Bump;
            public Mat(string texture, Color tint, float metallic, float smoothness, float bump = 1f)
            { Texture = texture; Tint = tint; Metallic = metallic; Smoothness = smoothness; Bump = bump; }
        }

        // Scalars only (no metallic/smoothness maps): a missing texture can never turn paint into a mirror.
        // Shell tints are CargoVisualPresenter.EquipmentColour divided by the textures' ~0.76 mean albedo.
        private static readonly Dictionary<string, Mat> Materials = new Dictionary<string, Mat>
        {
            ["CC2_Shell_Water"] = new Mat("CC2_Quilt", new Color(.10f, .60f, .78f), 0f, .28f, 1.1f),
            ["CC2_Shell_Food"] = new Mat("CC2_Polymer", new Color(.48f, .63f, .21f), 0f, .36f),
            ["CC2_Shell_Battery"] = new Mat("CC2_Painted", new Color(1f, .52f, .075f), 0f, .42f),
            ["CC2_Shell_Oxygen"] = new Mat("CC2_Painted", new Color(.93f, .96f, .96f), 0f, .45f),
            ["CC2_Shell_Solar"] = new Mat("CC2_Painted", new Color(.24f, .38f, .68f), .1f, .45f),
            ["CC2_Shell_Spares"] = new Mat("CC2_Polymer", new Color(.74f, .25f, .16f), 0f, .36f),
            ["CC2_Shell_Science"] = new Mat("CC2_Painted", new Color(.51f, .37f, .64f), .15f, .42f),
            ["CC2_Aluminium"] = new Mat("CC2_Aluminium", new Color(.88f, .89f, .9f), .9f, .55f, .6f),
            ["CC2_Rubber"] = new Mat("CC2_Rubber", new Color(.09f, .09f, .095f), 0f, .25f),
            ["CC2_Graphite"] = new Mat("CC2_Polymer", new Color(.2f, .21f, .23f), 0f, .35f),
            ["CC2_Webbing"] = new Mat("CC2_Webbing", new Color(.33f, .34f, .36f), 0f, .2f),
            ["CC2_WebbingAmber"] = new Mat("CC2_Webbing", new Color(1f, .62f, .12f), 0f, .2f),
            ["CC2_RedCap"] = new Mat("CC2_Polymer", new Color(.85f, .12f, .08f), 0f, .45f),
            ["CC2_Composite"] = new Mat("CC2_Composite", new Color(.95f, .95f, .92f), 0f, .5f, .8f),
            ["CC2_SolarCell"] = new Mat("CC2_SolarCell", Color.white, .3f, .75f, .8f),
            ["CC2_RackSteel"] = new Mat("CC2_Painted", new Color(.2f, .22f, .25f), .2f, .4f),
            ["CC2_Labels"] = new Mat("CC2_Labels", Color.white, 0f, .3f),
            ["CC2_LED"] = new Mat(null, new Color(.2f, 1f, .45f), 0f, .7f),
        };
        private static readonly Color LedEmission = new Color(.3f, 2.2f, .8f);

        [MenuItem("OUT OF REACH/Cargo Cases V02/Rebuild Materials and Prefabs", priority = 50)]
        public static void BuildAll()
        {
            var report = new StringBuilder();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureTextures(report);
            Dictionary<string, Material> materials = BuildMaterials(report);
            Directory.CreateDirectory(Root + "/Prefabs");
            foreach (CaseDef def in Cases)
            {
                BuildPrefab(def.ModelPath, def.PrefabPath, def.Key, materials, report);
                if (File.Exists(def.CellModelPath))
                    BuildPrefab(def.CellModelPath, def.CellPrefabPath, "RackCell_" + def.Key, materials, report);
            }
            if (File.Exists(RackModelPath)) BuildPrefab(RackModelPath, RackPrefabPath, RackKey, materials, report);
            AssetDatabase.SaveAssets();
            Debug.Log("CARGO_CASES_V02_BUILD OK\n" + report);
        }

        public static bool PrefabsReady()
        {
            foreach (CaseDef def in Cases)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(def.PrefabPath);
                if (!prefab) return false;
                foreach (MeshRenderer r in prefab.GetComponentsInChildren<MeshRenderer>(true))
                    foreach (Material m in r.sharedMaterials)
                        if (!m || (m.name != "CC2_LED" && !m.GetTexture("_BaseMap"))) return false; // e.g. un-smudged LFS
            }
            return true;
        }

        private static void ConfigureTextures(StringBuilder report)
        {
            foreach (string path in Directory.GetFiles(Root + "/Textures", "*.png").Select(p => p.Replace('\\', '/')))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path)
                    ?? throw new InvalidOperationException("Texture not imported: " + path);
                bool normal = path.EndsWith("_Normal.png", StringComparison.Ordinal);
                bool labels = path.Contains("CC2_Labels");
                bool changed = false;
                void Set<T>(T current, T wanted, Action<T> apply) { if (!EqualityComparer<T>.Default.Equals(current, wanted)) { apply(wanted); changed = true; } }
                Set(importer.textureType, normal ? TextureImporterType.NormalMap : TextureImporterType.Default, v => importer.textureType = v);
                Set(importer.sRGBTexture, !normal, v => importer.sRGBTexture = v);
                Set(importer.mipmapEnabled, true, v => importer.mipmapEnabled = v);
                Set(importer.anisoLevel, 8, v => importer.anisoLevel = v);
                Set(importer.wrapMode, labels ? TextureWrapMode.Clamp : TextureWrapMode.Repeat, v => importer.wrapMode = v);
                Set(importer.maxTextureSize, labels ? 2048 : 1024, v => importer.maxTextureSize = v);
                Set(importer.alphaSource, TextureImporterAlphaSource.FromInput, v => importer.alphaSource = v);
                Set(importer.alphaIsTransparency, labels, v => importer.alphaIsTransparency = v);
                if (labels) Set(importer.mipMapsPreserveCoverage, true, v => { importer.mipMapsPreserveCoverage = v; importer.alphaTestReferenceValue = .45f; });
                if (changed) importer.SaveAndReimport();
                report.AppendLine("texture " + Path.GetFileName(path) + (normal ? " normal" : " sRGB") + (changed ? " (set)" : ""));
            }
        }

        private static Texture2D Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + name + ".png");

        private static Dictionary<string, Material> BuildMaterials(StringBuilder report)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? throw new InvalidOperationException("URP Lit shader not found.");
            Directory.CreateDirectory(Root + "/Materials");
            var result = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, Mat> pair in Materials)
            {
                string name = pair.Key;
                Mat def = pair.Value;
                string path = Root + "/Materials/" + name + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!mat) { mat = new Material(lit) { name = name }; AssetDatabase.CreateAsset(mat, path); }
                mat.shader = lit;
                mat.SetFloat("_WorkflowMode", 1f);
                mat.SetFloat("_Surface", 0f);
                mat.SetFloat("_Cull", 2f);
                mat.SetFloat("_AlphaClip", 0f);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.DisableKeyword("_METALLICSPECGLOSSMAP");
                mat.SetTexture("_MetallicGlossMap", null);
                // One keyword set for every opaque CC2 material: a single URP Lit variant, no late-compiling stragglers.
                mat.EnableKeyword("_NORMALMAP");
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.black);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                mat.SetOverrideTag("RenderType", "Opaque");
                mat.renderQueue = -1;
                mat.SetColor("_BaseColor", def.Tint);
                mat.SetColor("_Color", def.Tint);
                mat.SetFloat("_Metallic", def.Metallic);
                mat.SetFloat("_Smoothness", def.Smoothness);
                mat.SetFloat("_SmoothnessTextureChannel", 0f);
                Texture2D baseMap = def.Texture == null ? null : Tex(def.Texture + "_BaseColor")
                    ?? throw new InvalidOperationException("Missing texture " + def.Texture + "_BaseColor.png (Git LFS pointer not smudged?)");
                mat.SetTexture("_BaseMap", baseMap);
                mat.SetTexture("_MainTex", baseMap);
                mat.SetTexture("_BumpMap", def.Texture == null ? null : Tex(def.Texture + "_Normal"));
                mat.SetFloat("_BumpScale", def.Bump);
                if (name == "CC2_Labels")
                {
                    mat.SetFloat("_AlphaClip", 1f);
                    mat.SetFloat("_Cutoff", .45f);
                    mat.EnableKeyword("_ALPHATEST_ON");
                    mat.SetOverrideTag("RenderType", "TransparentCutout");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                }
                if (name == "CC2_LED")
                {
                    mat.SetColor("_EmissionColor", LedEmission);
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                }
                EditorUtility.SetDirty(mat);
                result.Add(name, mat);
            }
            report.AppendLine("materials " + result.Count + " URP Lit");
            return result;
        }

        private static void BuildPrefab(string fbxPath, string prefabPath, string key, Dictionary<string, Material> materials, StringBuilder report)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbxPath) ?? throw new InvalidOperationException("FBX not imported: " + fbxPath);
            importer.bakeAxisConversion = true;
            importer.globalScale = 1f;
            importer.useFileScale = true;
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
            importer.addCollider = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            foreach (KeyValuePair<string, Material> pair in materials)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
            importer.SaveAndReimport();

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath) ?? throw new InvalidOperationException("Missing " + fbxPath);
            var root = new GameObject("CC2_" + key);
            try
            {
                var lods = new List<LOD>();
                float[] heights = { .22f, .07f, .012f };
                int tris = 0;
                for (int i = 0; i < 3; i++)
                {
                    Transform src = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == key + "_LOD" + i)
                        ?? throw new InvalidOperationException(fbxPath + " lacks " + key + "_LOD" + i);
                    var filter = src.GetComponent<MeshFilter>();
                    var srcRenderer = src.GetComponent<MeshRenderer>();
                    if (Quaternion.Angle(src.localRotation, Quaternion.identity) > .01f || (src.lossyScale - Vector3.one).sqrMagnitude > 1e-6f
                        || src.localPosition.sqrMagnitude > 1e-8f)
                        throw new InvalidOperationException("Imported axes differ for " + src.name + ": " + src.localRotation.eulerAngles + " " + src.lossyScale);
                    foreach (Material m in srcRenderer.sharedMaterials)
                        if (!m || !materials.ContainsKey(m.name)) throw new InvalidOperationException("Unmapped material on " + src.name + ": " + (m ? m.name : "null"));
                    var go = new GameObject(src.name);
                    go.transform.SetParent(root.transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                    var renderer = go.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = srcRenderer.sharedMaterials;
                    lods.Add(new LOD(heights[i], new Renderer[] { renderer }));
                    if (i == 0) for (int sub = 0; sub < filter.sharedMesh.subMeshCount; sub++) tris += (int)filter.sharedMesh.GetIndexCount(sub) / 3;
                }
                var group = root.AddComponent<LODGroup>();
                group.fadeMode = LODFadeMode.None;
                group.SetLODs(lods.ToArray());
                group.RecalculateBounds();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Bounds b = model.GetComponentsInChildren<MeshFilter>(true).First(f => f.name == key + "_LOD0").sharedMesh.bounds;
                report.AppendLine(Path.GetFileName(prefabPath) + " LOD0 tris " + tris + " bounds centre " + b.center.ToString("F3") + " size " + b.size.ToString("F3"));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
