using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OutOfReach.Visuals.AuroraCapsulesV02.Editor
{
    /// <summary>Rebuilds texture/model import settings, the seven URP Lit materials and the two reference prefabs
    /// of the AURORA capsules from the Blender export. Idempotent. Never touches a scene.</summary>
    public static class AuroraCapsulesV02Builder
    {
        public const string Root = "Assets/_OutOfReach/Visuals/Models/AuroraCapsulesV02";
        public const string RescueFbx = Root + "/Models/AuroraRescueCraft.fbx";
        public const string TransportFbx = Root + "/Models/AuroraCrewTransport.fbx";
        public const string RescuePrefab = Root + "/Prefabs/AuroraRescueCraft.prefab";
        public const string TransportPrefab = Root + "/Prefabs/AuroraCrewTransport.prefab";
        public const string OriginalFbx = "Assets/_OutOfReach/Visuals/Models/MarinePrologue/Transport/Models/TransportLaunch.fbx";

        private static readonly string[] RescueMaterials = { "RC_Hull", "RC_HeatShield", "CAP_Metal", "CAP_Graphite", "CAP_Kit" };
        private static readonly string[] TransportMaterials = { "CT_Hull", "CT_HeatShield", "CAP_Metal", "CAP_Graphite", "CAP_Kit" };

        [MenuItem("OUT OF REACH/Aurora Capsules V02/Rebuild Materials and Prefabs", priority = 60)]
        public static void BuildAll()
        {
            var report = new StringBuilder();
            ConfigureTextures(report);
            Dictionary<string, Material> materials = BuildMaterials(report);
            ConfigureModel(RescueFbx, RescueMaterials, materials, report);
            ConfigureModel(TransportFbx, TransportMaterials, materials, report);
            VerifyFrame(report);
            BuildPrefab(RescuePrefab, "AuroraRescueCraft", true, report);
            BuildPrefab(TransportPrefab, "AuroraCrewTransport", false, report);
            AssetDatabase.SaveAssets();
            Debug.Log("AURORA_CAPSULES_V02_BUILD OK\n" + report);
        }

        private static void ConfigureTextures(StringBuilder report)
        {
            foreach (string path in Directory.GetFiles(Root + "/Textures", "*.png").Select(p => p.Replace('\\', '/')))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path)
                    ?? throw new InvalidOperationException("Texture not imported: " + path);
                string file = Path.GetFileName(path);
                bool normal = file.EndsWith("_Normal.png", StringComparison.Ordinal);
                bool linear = normal || file.EndsWith("_Mask.png", StringComparison.Ordinal);
                bool kit = file.StartsWith("CAP_Kit", StringComparison.Ordinal);
                bool hull = file.Contains("_Hull_");
                int size = kit ? 256 : hull ? 4096 : file.Contains("HeatShield") ? 2048 : 1024;
                bool changed = false;
                void Set<T>(T current, T wanted, Action<T> apply)
                {
                    if (!EqualityComparer<T>.Default.Equals(current, wanted)) { apply(wanted); changed = true; }
                }
                Set(importer.textureType, normal ? TextureImporterType.NormalMap : TextureImporterType.Default, v => importer.textureType = v);
                Set(importer.sRGBTexture, !linear, v => importer.sRGBTexture = v);
                Set(importer.mipmapEnabled, !kit, v => importer.mipmapEnabled = v);
                Set(importer.filterMode, kit ? FilterMode.Point : FilterMode.Trilinear, v => importer.filterMode = v);
                Set(importer.anisoLevel, kit ? 0 : hull ? 8 : 4, v => importer.anisoLevel = v);
                Set(importer.wrapMode, kit ? TextureWrapMode.Clamp : TextureWrapMode.Repeat, v => importer.wrapMode = v);
                Set(importer.maxTextureSize, size, v => importer.maxTextureSize = v);
                Set(importer.alphaSource, TextureImporterAlphaSource.FromInput, v => importer.alphaSource = v);
                Set(importer.alphaIsTransparency, false, v => importer.alphaIsTransparency = v);
                Set(importer.npotScale, TextureImporterNPOTScale.None, v => importer.npotScale = v);
                Set(importer.textureCompression, kit ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ,
                    v => importer.textureCompression = v);
                if (changed) importer.SaveAndReimport();
                report.AppendLine("texture " + file + (normal ? " normal" : linear ? " linear" : " sRGB") + " max " + size + (changed ? " (set)" : ""));
            }
        }

        private static Texture2D Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + name + ".png");

        private static Dictionary<string, Material> BuildMaterials(StringBuilder report)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? throw new InvalidOperationException("URP Lit shader not found.");
            Directory.CreateDirectory(Root + "/Materials");
            var result = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (string name in RescueMaterials.Concat(TransportMaterials).Distinct())
            {
                Texture2D baseMap = Tex(name + "_BaseColor"), maskMap = Tex(name + "_Mask");
                if (!baseMap || !maskMap)
                    throw new InvalidOperationException("Missing " + name + " BaseColor/Mask texture (Git LFS pointer not smudged?).");
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
                mat.SetFloat("_Cull", 2f);
                mat.SetFloat("_AlphaClip", 0f);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.SetColor("_BaseColor", Color.white);
                mat.SetTexture("_BaseMap", baseMap);
                mat.SetTexture("_MainTex", baseMap);
                // One keyword set for all seven materials, so they share a single URP Lit variant.
                mat.EnableKeyword("_NORMALMAP");
                mat.SetTexture("_BumpMap", Tex(name + "_Normal"));
                mat.SetFloat("_BumpScale", 1f);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                mat.SetTexture("_MetallicGlossMap", maskMap);            // R metallic, A smoothness
                mat.SetFloat("_Metallic", 1f);
                mat.SetFloat("_Smoothness", 1f);
                mat.SetFloat("_SmoothnessTextureChannel", 0f);
                mat.EnableKeyword("_OCCLUSIONMAP");
                mat.SetTexture("_OcclusionMap", maskMap);                // G occlusion
                mat.SetFloat("_OcclusionStrength", 1f);
                mat.EnableKeyword("_EMISSION");
                Texture2D emission = Tex(name + "_Emission");
                mat.SetTexture("_EmissionMap", emission);
                mat.SetColor("_EmissionColor", emission ? new Color(4f, 4f, 4f, 1f) : Color.black);
                mat.globalIlluminationFlags = emission ? MaterialGlobalIlluminationFlags.None : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                mat.SetFloat("_SpecularHighlights", 1f);
                mat.SetFloat("_EnvironmentReflections", 1f);
                mat.renderQueue = -1;
                EditorUtility.SetDirty(mat);
                result[name] = mat;
                report.AppendLine("material " + name + (emission ? " +emission" : ""));
            }
            return result;
        }

        private static void ConfigureModel(string fbx, string[] names, Dictionary<string, Material> materials, StringBuilder report)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbx) ?? throw new InvalidOperationException("FBX not imported: " + fbx);
            // Same axis/scale handling as the delivered TransportLaunch.fbx, so the meshes share its local frame.
            importer.bakeAxisConversion = false;
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
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            foreach (string name in names)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), materials[name]);
            importer.SaveAndReimport();
            report.AppendLine("model " + Path.GetFileName(fbx) + ": " + names.Length + " material remaps");
        }

        public static Dictionary<string, Mesh> Meshes(string fbx) =>
            AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Mesh>().GroupBy(m => m.name).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        public static Transform FindDeep(Transform root, string name) =>
            root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        /// <summary>The swap only works if the new meshes live in the same local frame as the delivered ones.</summary>
        private static void VerifyFrame(StringBuilder report)
        {
            Dictionary<string, Mesh> original = Meshes(OriginalFbx);
            GameObject originalModel = AssetDatabase.LoadAssetAtPath<GameObject>(OriginalFbx);
            foreach (string fbx in new[] { RescueFbx, TransportFbx })
            {
                Dictionary<string, Mesh> fresh = Meshes(fbx);
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
                foreach (string part in new[] { "CapsuleShell", "Hatch" })
                {
                    Bounds a = original[part].bounds, b = fresh[part + "_LOD0"].bounds;
                    Transform ta = FindDeep(originalModel.transform, part), tb = FindDeep(model.transform, part + "_LOD0");
                    report.AppendLine(Path.GetFileNameWithoutExtension(fbx) + " " + part + ": delivered " + a.min.ToString("F3") + ".." + a.max.ToString("F3")
                        + " new " + b.min.ToString("F3") + ".." + b.max.ToString("F3")
                        + " | node rot " + ta.localRotation.eulerAngles.ToString("F1") + " / " + tb.localRotation.eulerAngles.ToString("F1")
                        + " scale " + ta.localScale.ToString("F3") + " / " + tb.localScale.ToString("F3"));
                    if (Quaternion.Angle(ta.localRotation, tb.localRotation) > .05f || (ta.localScale - tb.localScale).sqrMagnitude > 1e-6f
                        || Vector3.Distance(ta.localPosition, tb.localPosition) > .002f)
                        throw new InvalidOperationException(part + ": imported node transform differs from the delivered FBX: "
                            + ta.localPosition.ToString("F4") + " vs " + tb.localPosition.ToString("F4"));
                    float tolerance = part == "CapsuleShell" ? .16f : .12f;
                    if (Vector3.Distance(a.center, b.center) > tolerance || (a.size - b.size).magnitude > tolerance * 2f)
                        throw new InvalidOperationException(part + ": new mesh is not in the delivered mesh frame.");
                }
                // The docking datum check in LeadDepartureAuthoring needs the nose 0.15 m past the port (z 4.95 in mesh space).
                Bounds shell = fresh["CapsuleShell_LOD0"].bounds, old = original["CapsuleShell"].bounds;
                int axis = 0;
                for (int k = 1; k < 3; k++) if (Mathf.Abs(old.max[k] - 5.10f) < Mathf.Abs(old.max[axis] - 5.10f)) axis = k;
                if (Mathf.Abs(old.max[axis] - 5.10f) > .01f || Mathf.Abs(shell.max[axis] - old.max[axis]) > .004f)
                    throw new InvalidOperationException("Nose datum moved: delivered " + old.max[axis].ToString("F4") + " new " + shell.max[axis].ToString("F4"));
                report.AppendLine("  nose datum axis " + "xyz"[axis] + " = " + shell.max[axis].ToString("F4") + " (delivered " + old.max[axis].ToString("F4") + ")");
            }
        }

        private static void BuildPrefab(string path, string name, bool rescue, StringBuilder report)
        {
            GameObject original = AssetDatabase.LoadAssetAtPath<GameObject>(OriginalFbx);
            Transform srcShell = FindDeep(original.transform, "CapsuleShell"), srcHatch = FindDeep(original.transform, "Hatch");
            Directory.CreateDirectory(Root + "/Prefabs");
            var root = new GameObject(name);
            try
            {
                GameObject shell = Part(root.transform, srcShell), hatch = Part(root.transform, srcHatch);
                AuroraCapsulesV02Installer.Apply(shell, hatch, rescue, rescue, false, report);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                int triangles = root.GetComponentsInChildren<MeshFilter>(true).Sum(f => (int)(f.sharedMesh.GetIndexCount(0) / 3)
                    + Enumerable.Range(1, f.sharedMesh.subMeshCount - 1).Sum(s => (int)(f.sharedMesh.GetIndexCount(s) / 3)));
                report.AppendLine("prefab " + name + ": all-LOD triangles " + triangles);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject Part(Transform parent, Transform source)
        {
            var go = new GameObject(source.name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = source.localPosition;
            go.transform.localRotation = source.localRotation;
            go.transform.localScale = source.localScale;
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            return go;
        }
    }
}
