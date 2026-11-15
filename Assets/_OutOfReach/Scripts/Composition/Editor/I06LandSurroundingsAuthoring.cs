using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OutOfReach.Composition.Editor
{
    /// <summary>Exact passive d3cf841 surroundings. Creates one sibling of LandSite/Horizon, no gameplay owner.</summary>
    public static class I06LandSurroundingsAuthoring
    {
        public const string AssetRoot = "Assets/_OutOfReach/Visuals/Models/LandSurroundings";
        public const string ModelPath = AssetRoot + "/Models/LSS_LandSurroundings.fbx";
        public const string InstanceName = "Land surroundings - d3cf841";
        private const float PositionTolerance = .003f; // Float precision at 2.5km local / 10km scene origin.

        public sealed class Result
        {
            public GameObject Root;
            public Dictionary<string, Transform> Anchors;
        }
#pragma warning disable 0649
        [Serializable] private sealed class Layout
        {
            public string sourceCommit, sourceManifestSha256;
            public float groundY;
            public int triangleTotal;
            public Module[] modules;
            public Anchor[] anchors;
            public MaterialSpec[] materials;
            public FileSpec[] files, dependencies;
        }
        [Serializable] private sealed class Module { public string name; public int triangles; public float[] min, max; public string[] materials; }
        [Serializable] private sealed class Anchor { public string name; public float[] point; }
        [Serializable] private sealed class MaterialSpec
        { public string name, path, tint, BaseColor, Normal, MODS; public bool created; public float tiling, normal; }
        [Serializable] private sealed class FileSpec { public string path, sha256, guid; }
#pragma warning restore 0649

        /// <summary>Call after existing LandSite and LandHorizon, with their actual stationary SiteRoot.</summary>
        public static Result Create(Transform parentSiteRoot)
        {
            RequireSite(parentSiteRoot);
            if (parentSiteRoot.Find(InstanceName)) throw new InvalidOperationException("Surroundings already exist; do not stack copies.");
            Layout layout = ReadLayout();
            VerifyFiles(layout);
            PrepareOwnedImporters(layout);
            Dictionary<string, Material> materials = ReadMaterials(layout);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(Required<GameObject>(ModelPath), parentSiteRoot);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = InstanceName;
            instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;
            try
            {
                foreach (MeshRenderer renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
                {
                    Material[] slots = renderer.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++)
                    {
                        string sourceName = slots[i] ? slots[i].name : string.Empty;
                        if (sourceName.StartsWith("MAT_", StringComparison.Ordinal)) sourceName = sourceName.Substring(4);
                        if (!materials.TryGetValue(sourceName, out Material mapped))
                            throw new InvalidOperationException("Unknown authored surroundings material slot: " + sourceName);
                        slots[i] = mapped;
                    }
                    renderer.sharedMaterials = slots;
                }
                var result = new Result { Root = instance, Anchors = new Dictionary<string, Transform>(StringComparer.Ordinal) };
                Transform[] transforms = instance.GetComponentsInChildren<Transform>(true);
                foreach (Anchor anchor in layout.anchors)
                {
                    Transform[] matches = transforms.Where(t => t.name == anchor.name).ToArray();
                    Require(matches.Length == 1, "Expected one exact imported anchor: " + anchor.name);
                    result.Anchors.Add(anchor.name, matches[0]);
                }
                Validate(result, parentSiteRoot);
                Debug.Log("LAND_SURROUNDINGS_AUTHORED source=d3cf841 groups=7 triangles=17032 anchors=7; passive original art; native scene appearance requires review.");
                return result;
            }
            catch { Object.DestroyImmediate(instance); throw; }
        }

        /// <summary>Read-only instance/import/material validation; never repairs geometry or adjusts the site.</summary>
        public static void Validate(Result result, Transform parentSiteRoot)
        {
            RequireSite(parentSiteRoot);
            Require(result?.Root && result.Root.transform.parent == parentSiteRoot, "Surroundings must be a direct stationary SiteRoot child.");
            Layout layout = ReadLayout();
            VerifyFiles(layout);
            ValidateImporters(layout);
            Dictionary<string, Material> materials = ReadMaterials(layout);
            Transform root = result.Root.transform;
            Require(root.localPosition.sqrMagnitude < 1e-10f && Quaternion.Angle(root.localRotation, Quaternion.identity) < .001f
                && (root.localScale - Vector3.one).sqrMagnitude < 1e-10f, "Use exact prologue-local origin, identity rotation and metre scale; do not apply another -36.6 offset.");
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            Require(filters.Length == 7 && root.GetComponentsInChildren<Renderer>(true).Length == 7
                && root.GetComponentsInChildren<Transform>(true).Length == 15, "Exact seven mesh groups, seven anchors and one root required.");
            long total = 0;
            foreach (Module module in layout.modules)
            {
                MeshFilter filter = filters.Single(f => f.name == module.name);
                Mesh mesh = filter.sharedMesh; var renderer = filter.GetComponent<MeshRenderer>();
                Require(mesh && mesh.isReadable && AssetDatabase.GetAssetPath(mesh) == ModelPath && renderer && renderer.enabled,
                    "Exact readable FBX mesh and active renderer required: " + module.name);
                Require(filter.transform.localPosition.sqrMagnitude < 1e-10f
                    && Quaternion.Angle(filter.transform.localRotation, Quaternion.identity) < .001f
                    && (filter.transform.localScale - Vector3.one).sqrMagnitude < 1e-10f, "Authored origin pivot/axes/scale changed: " + module.name);
                long triangles = 0;
                for (int s = 0; s < mesh.subMeshCount; s++)
                { Require(mesh.GetTopology(s) == MeshTopology.Triangles, "Non-triangle submesh: " + module.name); triangles += (long)mesh.GetIndexCount(s) / 3; }
                Require(triangles == module.triangles, "Exact native triangle count differs: " + module.name + " actual=" + triangles);
                total += triangles;
                Vector3[] vertices = mesh.vertices;
                Require(vertices.Length > 0, "Empty module: " + module.name);
                Matrix4x4 matrix = parentSiteRoot.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                Bounds bounds = new Bounds(matrix.MultiplyPoint3x4(vertices[0]), Vector3.zero);
                foreach (Vector3 vertex in vertices) bounds.Encapsulate(matrix.MultiplyPoint3x4(vertex));
                Require(Vector3.Distance(bounds.min, V(module.min)) <= PositionTolerance && Vector3.Distance(bounds.max, V(module.max)) <= PositionTolerance,
                    "Metre/axis/bounds mismatch: " + module.name + " actual=" + bounds);
                Material[] slots = renderer.sharedMaterials;
                Require(slots.Length == module.materials.Length && slots.All(m => m && materials.TryGetValue(m.name, out Material expected) && m == expected)
                    && slots.Select(m => m.name).OrderBy(n => n).SequenceEqual(module.materials.OrderBy(n => n)),
                    "Exact shared material slots changed: " + module.name);
            }
            Require(total == 17032 && total == layout.triangleTotal && result.Anchors?.Count == 7, "Exact17032-triangle/seven-anchor budget changed.");
            foreach (Anchor anchor in layout.anchors)
            {
                Require(result.Anchors.TryGetValue(anchor.name, out Transform actual) && actual && actual.IsChildOf(root)
                    && Vector3.Distance(parentSiteRoot.InverseTransformPoint(actual.position), V(anchor.point)) <= PositionTolerance
                    && Quaternion.Angle(parentSiteRoot.rotation, actual.rotation) < .001f
                    && (actual.lossyScale - Vector3.one).sqrMagnitude < .000001f, "Actual anchor position/axis/scale differs: " + anchor.name);
            }
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
                Require(component && (component is Transform || component is MeshFilter || component is MeshRenderer),
                    "Passive surroundings cannot contain colliders, lights, camera, behaviour, particles or other components.");
        }

        private static void PrepareOwnedImporters(Layout layout)
        {
            foreach (FileSpec file in layout.files.Where(f => f.path.StartsWith("Textures/", StringComparison.Ordinal)))
            {
                string path = AssetRoot + "/" + file.path;
                var texture = AssetImporter.GetAtPath(path) as TextureImporter;
                Require(texture, "Missing owned texture importer: " + path);
                bool normal = path.EndsWith("_Normal.png", StringComparison.Ordinal), mods = path.EndsWith("_MODS.png", StringComparison.Ordinal);
                texture.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                texture.textureShape = TextureImporterShape.Texture2D; texture.sRGBTexture = !normal && !mods;
                texture.convertToNormalmap = false; texture.flipGreenChannel = false;
                texture.alphaSource = TextureImporterAlphaSource.FromInput; texture.alphaIsTransparency = false;
                texture.mipmapEnabled = true; texture.maxTextureSize = 1024; texture.npotScale = TextureImporterNPOTScale.None;
                texture.textureCompression = TextureImporterCompression.Uncompressed; texture.filterMode = FilterMode.Trilinear;
                texture.anisoLevel = 4; texture.wrapMode = TextureWrapMode.Repeat; texture.isReadable = false;
                texture.SaveAndReimport();
            }
            var model = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            Require(model, "Missing exact surroundings ModelImporter.");
            model.useFileUnits = true; model.useFileScale = true; model.globalScale = 1f;
            model.bakeAxisConversion = true; model.preserveHierarchy = false;
            model.animationType = ModelImporterAnimationType.None; model.importAnimation = false;
            model.importCameras = false; model.importLights = false; model.addCollider = false;
            model.isReadable = true; model.meshCompression = ModelImporterMeshCompression.Off; model.weldVertices = true;
            model.importNormals = ModelImporterNormals.Import; model.importTangents = ModelImporterTangents.CalculateMikk;
            model.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            model.materialLocation = ModelImporterMaterialLocation.InPrefab;
            model.SaveAndReimport();
            ValidateImporters(layout);
        }
        private static void ValidateImporters(Layout layout)
        {
            var model = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            Require(model && model.useFileUnits && model.useFileScale && model.globalScale == 1f && model.bakeAxisConversion
                && !model.preserveHierarchy && !model.importAnimation && !model.importCameras && !model.importLights && !model.addCollider
                && model.isReadable && model.weldVertices && model.meshCompression == ModelImporterMeshCompression.Off,
                "Exact source unit/axis/weld/readability settings are required.");
            foreach (FileSpec file in layout.files.Where(f => f.path.StartsWith("Textures/", StringComparison.Ordinal)))
            {
                string path = AssetRoot + "/" + file.path; var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Texture2D texture = Required<Texture2D>(path);
                bool normal = path.EndsWith("_Normal.png", StringComparison.Ordinal), mods = path.EndsWith("_MODS.png", StringComparison.Ordinal);
                Require(importer && texture.width == 1024 && texture.height == 1024 && importer.sRGBTexture == (!normal && !mods)
                    && importer.textureType == (normal ? TextureImporterType.NormalMap : TextureImporterType.Default)
                    && !importer.convertToNormalmap && !importer.flipGreenChannel && !importer.alphaIsTransparency,
                    "Authored1024 map/channel/normal convention changed: " + path);
            }
        }
        private static Dictionary<string, Material> ReadMaterials(Layout layout)
        {
            var result = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (MaterialSpec spec in layout.materials)
            {
                Material material = Required<Material>(spec.path);
                Require(material.name == spec.name && material.shader && material.shader.name == "Universal Render Pipeline/Lit", "Exact existing URP material required: " + spec.path);
                if (spec.created)
                {
                    Require(ColorUtility.TryParseHtmlString(spec.tint, out Color tint), "Invalid pinned tint.");
                    Require(Vector4.Distance(material.GetColor("_BaseColor"), tint) < .00001f
                        && Vector2.Distance(material.GetTextureScale("_BaseMap"), Vector2.one * spec.tiling) < .00001f
                        && Mathf.Abs(material.GetFloat("_BumpScale") - spec.normal) < .00001f, "Tint/tiling/normal differs: " + spec.name);
                    Require(material.GetTexture("_BaseMap") == Required<Texture2D>(spec.BaseColor)
                        && material.GetTexture("_BumpMap") == Required<Texture2D>(spec.Normal)
                        && material.GetTexture("_MetallicGlossMap") == Required<Texture2D>(spec.MODS)
                        && material.GetTexture("_OcclusionMap") == Required<Texture2D>(spec.MODS)
                        && material.IsKeywordEnabled("_NORMALMAP") && material.IsKeywordEnabled("_METALLICSPECGLOSSMAP")
                        && material.IsKeywordEnabled("_OCCLUSIONMAP") && material.GetFloat("_SmoothnessTextureChannel") == 0
                        && material.GetFloat("_Smoothness") == 1 && material.GetFloat("_OcclusionStrength") == 1,
                        "Use authored BaseColor/normal and linear MODS R-metallic/G-occlusion/A-smoothness: " + spec.name);
                    Require(material.GetFloat("_Surface") == 0 && material.GetFloat("_AlphaClip") == 0 && material.GetFloat("_ZWrite") == 1
                        && material.GetFloat("_SrcBlend") == 1 && material.GetFloat("_DstBlend") == 0 && material.renderQueue == 2000,
                        "Surroundings are opaque authored geometry, not new transparent cards: " + spec.name);
                }
                result.Add(spec.name, material);
            }
            return result;
        }
        private static void VerifyFiles(Layout layout)
        {
            foreach (FileSpec file in layout.files) VerifyHash(AssetRoot + "/" + file.path, file.sha256);
            foreach (FileSpec file in layout.dependencies)
            {
                VerifyHash(file.path, file.sha256);
                Require(AssetDatabase.AssetPathToGUID(file.path) == file.guid, "Existing shared asset GUID differs: " + file.path);
            }
        }
        private static void VerifyHash(string path, string expected)
        {
            using (SHA256 hash = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                Require(BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() == expected,
                    "Pinned source/dependency bytes differ; do not silently substitute: " + path);
        }
        private static Layout ReadLayout()
        {
            Layout layout = JsonUtility.FromJson<Layout>(Required<TextAsset>(AssetRoot + "/LandSurroundingsLayout.json").text);
            Require(layout != null && layout.sourceCommit == "d3cf841" && layout.modules?.Length == 7 && layout.anchors?.Length == 7
                && layout.materials?.Length == 8 && layout.materials.Count(m => m.created) == 4 && layout.files?.Length == 7
                && layout.dependencies?.Length == 7 && layout.triangleTotal == 17032 && layout.groundY == -36.6f,
                "Use exact delivered LAND-SURROUNDINGS-V01 integration layout.");
            VerifyHash(AssetRoot + "/Provenance/lss_manifest.json", layout.sourceManifestSha256);
            return layout;
        }
        private static void RequireSite(Transform site)
        {
            Require(site && !EditorUtility.IsPersistent(site.gameObject) && (site.lossyScale - Vector3.one).sqrMagnitude < .000001f
                && Vector3.Dot(site.up, Vector3.up) > .99999f, "Use the actual stationary metre-scale yaw-only SiteRoot.");
            Require(site.Find("Land launch site - 2f8dd22") && site.Find("I06 flat coastal land horizon"),
                "Call after existing LandSite and LandHorizon; surroundings are a sibling, never a child of either model or VehicleRoot.");
        }
        private static Vector3 V(float[] value) => new Vector3(value[0], value[1], value[2]);
        private static T Required<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path)
            ?? throw new InvalidOperationException("Missing materialized asset: " + path);
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
