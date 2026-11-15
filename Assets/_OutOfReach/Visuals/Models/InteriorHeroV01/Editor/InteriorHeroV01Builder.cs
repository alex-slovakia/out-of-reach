using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace OutOfReach.Visuals.InteriorHeroV01.Editor
{
    /// <summary>Editor-only build of the INTERIOR-HERO-V01 assets: texture and model import settings, URP Lit materials
    /// from Models/InteriorHeroV01_materials.json, and one prefab with a LODGroup per prop. Touches only this folder.</summary>
    public static class InteriorHeroV01Builder
    {
        public const string Root = "Assets/_OutOfReach/Visuals/Models/InteriorHeroV01";
        public const string FbxPath = Root + "/Models/InteriorHeroV01.fbx";
        public const string ReportPath = Root + "/Models/InteriorHeroV01_report.json";
        public const string MaterialSpecPath = Root + "/Models/InteriorHeroV01_materials.json";
        public const string PrefabPath = Root + "/Prefabs/InteriorHeroV01.prefab";
        public const string PrefabRootName = "InteriorHeroV01";

#pragma warning disable 0649
        [Serializable] public sealed class Report { public PropSpec[] props; public int triangles_lod0; }
        [Serializable] public sealed class PropSpec
        {
            public string name; public float[] origin; public string[] hide; public Carve[] carve; public Lod[] lods; public View[] views;
        }
        [Serializable] public sealed class Carve { public string target; public float[] min, max; }
        [Serializable] public sealed class Lod { public int triangles; public float[] bounds_min, bounds_max; }
        [Serializable] public sealed class View { public string name; public float[] eye, target; public float fov; }
        [Serializable] private sealed class MaterialFile { public MaterialSpec[] materials; }
        [Serializable] private sealed class MaterialSpec
        {
            public string name, set, @base, atlas, tint, surface, emission;
            public float tiling, normal, alpha, metallic, smoothness, emission_strength;
            public bool emission_map, no_mods;
        }
#pragma warning restore 0649

        [MenuItem("OUT OF REACH/Interior Hero V01/Rebuild materials and prefab", priority = 60)]
        public static void BuildAllMenu() => Debug.Log("INTERIOR_HERO_V01_BUILD\n" + BuildAll());

        public static Report LoadReport()
        {
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(ReportPath);
            if (!text) throw new InvalidOperationException("Missing " + ReportPath + " (run Source~/build_interior_hero.py).");
            Report report = JsonUtility.FromJson<Report>(text.text);
            if (report?.props == null || report.props.Length == 0) throw new InvalidOperationException("Empty prop report.");
            return report;
        }

        public static string BuildAll()
        {
            var log = new StringBuilder();
            AssetDatabase.Refresh();
            ConfigureTextures(log);
            Dictionary<string, Material> materials = BuildMaterials(log);
            ConfigureModel(materials);
            BuildPrefab(LoadReport(), log);
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        private static void ConfigureTextures(StringBuilder log)
        {
            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/Textures" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                bool isNormal = name.EndsWith("_Normal", StringComparison.Ordinal);
                bool isMask = name.EndsWith("_MODS", StringComparison.Ordinal);
                bool isAtlas = name == "IH_Labels" || name == "IH_Screens";
                var type = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                int size = name == "IH_Labels" ? 4096 : name == "IH_Screens" ? 4096 : 1024;
                var wrap = isAtlas ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                bool ok = importer.textureType == type && importer.sRGBTexture == (!isMask && !isNormal) && importer.mipmapEnabled
                    && importer.anisoLevel == 8 && importer.wrapMode == wrap && importer.maxTextureSize == size
                    && importer.alphaIsTransparency == (name == "IH_Labels") && importer.mipMapsPreserveCoverage == (name == "IH_Labels")
                    && importer.textureCompression == TextureImporterCompression.CompressedHQ;
                if (ok) continue;
                importer.textureType = type;
                if (!isNormal) importer.sRGBTexture = !isMask;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = name == "IH_Labels";
                importer.mipmapEnabled = true;
                importer.mipMapsPreserveCoverage = name == "IH_Labels";
                importer.alphaTestReferenceValue = 0.5f;
                importer.anisoLevel = 8;
                importer.filterMode = FilterMode.Trilinear;
                importer.wrapMode = wrap;
                importer.maxTextureSize = size;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
                changed++;
            }
            log.AppendLine("textures configured: " + changed);
        }

        private static Dictionary<string, Material> BuildMaterials(StringBuilder log)
        {
            var file = AssetDatabase.LoadAssetAtPath<TextAsset>(MaterialSpecPath);
            if (!file) throw new InvalidOperationException("Missing " + MaterialSpecPath);
            MaterialSpec[] specs = JsonUtility.FromJson<MaterialFile>(file.text).materials;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("URP Lit shader not found.");
            if (!AssetDatabase.IsValidFolder(Root + "/Materials")) AssetDatabase.CreateFolder(Root, "Materials");
            var result = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (MaterialSpec spec in specs)
            {
                string path = Root + "/Materials/" + spec.name + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!material)
                {
                    material = new Material(shader) { name = spec.name };
                    AssetDatabase.CreateAsset(material, path);
                }
                material.shader = shader;
                material.shaderKeywords = Array.Empty<string>();
                Color tint = Hex(string.IsNullOrEmpty(spec.tint) ? "#FFFFFF" : spec.tint);
                material.SetOverrideTag("RenderType", "Opaque");
                material.renderQueue = -1;
                material.SetFloat("_Surface", 0); material.SetFloat("_AlphaClip", 0); material.SetFloat("_ZWrite", 1);
                material.SetFloat("_SrcBlend", (float)BlendMode.One); material.SetFloat("_DstBlend", (float)BlendMode.Zero);
                material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); material.SetFloat("_DstBlendAlpha", (float)BlendMode.Zero);
                material.SetFloat("_WorkflowMode", 1); material.SetFloat("_SmoothnessTextureChannel", 0); material.SetFloat("_Cull", 2);
                material.SetFloat("_Metallic", Mathf.Max(0f, spec.metallic));
                material.SetFloat("_Smoothness", spec.smoothness > 0f ? spec.smoothness : 0.5f);
                material.SetTexture("_BaseMap", null); material.SetTexture("_BumpMap", null);
                material.SetTexture("_MetallicGlossMap", null); material.SetTexture("_OcclusionMap", null);
                material.SetTexture("_EmissionMap", null); material.SetColor("_EmissionColor", Color.black);
                material.SetShaderPassEnabled("ShadowCaster", true);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                float tiling = spec.tiling > 0f ? spec.tiling : 1f;
                material.SetTextureScale("_BaseMap", Vector2.one * tiling);
                if (!string.IsNullOrEmpty(spec.set))
                {
                    material.SetTexture("_BaseMap", Texture(string.IsNullOrEmpty(spec.@base) ? spec.set + "_BaseColor" : spec.@base));
                    material.SetTexture("_BumpMap", Texture(spec.set + "_Normal"));
                    material.SetFloat("_BumpScale", spec.normal > 0f ? spec.normal : 1f);
                    material.EnableKeyword("_NORMALMAP");
                    if (!spec.no_mods)
                    {
                        Texture2D mask = Texture(spec.set + "_MODS");
                        material.SetTexture("_MetallicGlossMap", mask); material.SetTexture("_OcclusionMap", mask);
                        material.SetFloat("_Metallic", 1f); material.SetFloat("_Smoothness", 1f); material.SetFloat("_OcclusionStrength", 1f);
                        material.EnableKeyword("_METALLICSPECGLOSSMAP"); material.EnableKeyword("_OCCLUSIONMAP");
                    }
                }
                if (!string.IsNullOrEmpty(spec.atlas)) material.SetTexture("_BaseMap", Texture(spec.atlas));
                if (!string.IsNullOrEmpty(spec.emission))
                {
                    material.SetColor("_EmissionColor", Hex(spec.emission).linear * spec.emission_strength);
                    if (spec.emission_map) material.SetTexture("_EmissionMap", Texture(spec.atlas));
                    material.EnableKeyword("_EMISSION");
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                }
                if (spec.surface == "cutout")
                {
                    material.SetFloat("_AlphaClip", 1); material.SetFloat("_Cutoff", .5f); material.EnableKeyword("_ALPHATEST_ON");
                    material.SetOverrideTag("RenderType", "TransparentCutout"); material.renderQueue = (int)RenderQueue.AlphaTest;
                }
                if (spec.surface == "transparent")
                {
                    tint.a = spec.alpha; material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0);
                    material.SetFloat("_BlendModePreserveSpecular", 1);
                    material.SetFloat("_SrcBlend", (float)BlendMode.One); material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha); material.SetFloat("_ZWrite", 0);
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.SetOverrideTag("RenderType", "Transparent");
                    material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                    material.renderQueue = (int)RenderQueue.Transparent; material.SetShaderPassEnabled("ShadowCaster", false);
                }
                material.SetColor("_BaseColor", tint);
                EditorUtility.SetDirty(material);
                result.Add(spec.name, material);
            }
            AssetDatabase.SaveAssets();
            log.AppendLine("materials: " + result.Count);
            return result;
        }

        private static void ConfigureModel(Dictionary<string, Material> materials)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(FbxPath);
            if (!importer) throw new InvalidOperationException("Missing " + FbxPath);
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importBlendShapes = false;
            importer.importVisibility = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = false;
            importer.weldVertices = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.generateSecondaryUV = false;
            importer.addCollider = false;
            importer.indexFormat = ModelImporterIndexFormat.Auto;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.SaveAndReimport();
            importer = (ModelImporter)AssetImporter.GetAtPath(FbxPath);
            foreach (Material embedded in AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<Material>())
            {
                if (!materials.TryGetValue(embedded.name, out Material material))
                    throw new InvalidOperationException(FbxPath + " uses material '" + embedded.name + "' with no entry in the material spec.");
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name), material);
            }
            importer.SaveAndReimport();
        }

        private static void BuildPrefab(Report report, StringBuilder log)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (!model) throw new InvalidOperationException("Model import failed: " + FbxPath);
            var sources = model.GetComponentsInChildren<MeshFilter>(true).ToDictionary(f => f.name, f => f, StringComparer.Ordinal);
            var root = new GameObject(PrefabRootName);
            try
            {
                long total = 0;
                foreach (PropSpec prop in report.props)
                {
                    var holder = new GameObject(prop.name);
                    holder.transform.SetParent(root.transform, false);
                    holder.transform.localPosition = new Vector3(prop.origin[0], prop.origin[1], prop.origin[2]);
                    var lods = new List<LOD>();
                    float[] heights = { .30f, .10f, .012f };
                    for (int i = 0; i < 3; i++)
                    {
                        string name = prop.name + "_L" + i;
                        if (!sources.TryGetValue(name, out MeshFilter source)) throw new InvalidOperationException("FBX lacks " + name);
                        if (Vector3.Distance(source.transform.position, holder.transform.localPosition) > .002f
                            || Quaternion.Angle(source.transform.rotation, Quaternion.identity) > .05f
                            || (source.transform.lossyScale - Vector3.one).sqrMagnitude > 1e-6f)
                            throw new InvalidOperationException(name + " imported off its ship-space origin: " + source.transform.position.ToString("F4")
                                + " rot " + source.transform.eulerAngles.ToString("F2") + " scale " + source.transform.lossyScale.ToString("F4"));
                        var child = new GameObject(name);
                        child.transform.SetParent(holder.transform, false);
                        child.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                        var renderer = child.AddComponent<MeshRenderer>();
                        renderer.sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials;
                        if (renderer.sharedMaterials.Any(m => !m || !m.name.StartsWith("IH_", StringComparison.Ordinal)))
                            throw new InvalidOperationException(name + " has an unbound material slot.");
                        renderer.shadowCastingMode = i == 2 ? ShadowCastingMode.Off : ShadowCastingMode.On;
                        long triangles = 0;
                        for (int s = 0; s < source.sharedMesh.subMeshCount; s++) triangles += (long)source.sharedMesh.GetIndexCount(s) / 3;
                        if (prop.lods != null && i < prop.lods.Length && prop.lods[i].triangles != triangles)
                            log.AppendLine("NOTE " + name + " imported " + triangles + " triangles, the Blender report says " + prop.lods[i].triangles);
                        if (i == 0) total += triangles;
                        lods.Add(new LOD(heights[i], new Renderer[] { renderer }));
                        log.AppendLine(name + " triangles " + triangles + " slots " + renderer.sharedMaterials.Length);
                    }
                    LODGroup group = holder.AddComponent<LODGroup>();
                    group.fadeMode = LODFadeMode.None;
                    group.SetLODs(lods.ToArray());
                    group.RecalculateBounds();
                }
                if (!AssetDatabase.IsValidFolder(Root + "/Prefabs")) AssetDatabase.CreateFolder(Root, "Prefabs");
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
                if (!saved) throw new InvalidOperationException("Could not save " + PrefabPath);
                log.AppendLine("prefab " + PrefabPath + " props " + report.props.Length + " LOD0 triangles " + total);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static Texture2D Texture(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + name + ".png")
            ?? throw new InvalidOperationException("Missing texture " + name);
        private static Color Hex(string value) => ColorUtility.TryParseHtmlString(value, out Color color) ? color
            : throw new InvalidOperationException("Invalid colour " + value);
    }
}
