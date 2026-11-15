using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace OutOfReach.Composition.Editor
{
    /// <summary>Imports the curated CC0 pack into URP assets. Never opens, saves or places a scene.</summary>
    public static class PolyHavenAssetAuthoring
    {
        public const string Root = "Assets/_OutOfReach/Visuals/ThirdParty/PolyHaven";
        [Serializable] private sealed class Manifest { public Asset[] assets; }
        [Serializable] private sealed class Asset
        {
            public string id, kind, model, prefab, resolution;
            public int triangles;
            public float[] dimensions_m_unity_xyz;
            public Surface[] materials;
        }
        [Serializable] private sealed class Surface
        {
            public string key, baseColor, normal, metalSmooth, emission, alpha;
            public bool ground, glass;
        }
        [Serializable] private sealed class Evidence { public string editor; public Result[] assets; }
        [Serializable] private sealed class Result
        {
            public string id, prefab; public Vector3 centreMetres, sizeMetres;
            public int triangles, materialCount; public bool metreBoundsMatch;
        }

        [MenuItem("OUT OF REACH/Import curated Poly Haven assets")]
        public static void BuildAssets()
        {
            if (Application.isPlaying || Application.unityVersion != "6000.3.28f1")
                throw new InvalidOperationException("Run in Edit mode with Unity6000.3.28f1.");
            string manifestPath = Root + "/prepared-manifest.json";
            if (!File.Exists(manifestPath)) throw new FileNotFoundException("Run the bounded download/Blender preparation first.", manifestPath);
            Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
            if (manifest?.assets == null || manifest.assets.Length == 0) throw new InvalidDataException("Prepared manifest has no assets.");
            EnsureFolder(Root + "/Materials"); EnsureFolder(Root + "/Prefabs");
            var results = new List<Result>();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("Existing URP Lit shader is required.");
            foreach (Asset asset in manifest.assets)
            {
                if (string.IsNullOrEmpty(asset.id) || asset.id.IndexOfAny(new[] {'/', '\\', '.'}) >= 0)
                    throw new InvalidDataException("Invalid curated asset ID.");
                var materials = new Dictionary<string, Material>(StringComparer.Ordinal);
                int maxSize = asset.resolution == "2k" ? 2048 : 1024;
                foreach (Surface surface in asset.materials)
                {
                    Texture2D colour = Texture(surface.baseColor, false, true, maxSize);
                    Texture2D normal = Texture(surface.normal, true, false, maxSize);
                    Texture2D packed = Texture(surface.metalSmooth, false, false, maxSize);
                    Texture2D emission = Texture(surface.emission, false, true, maxSize);
                    string path = Root + "/Materials/PH_" + surface.key + ".mat";
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
                    material.shader = shader; material.shaderKeywords = Array.Empty<string>();
                    material.SetTexture("_BaseMap", colour);
                    material.SetColor("_BaseColor", new Color(1f, 1f, 1f, surface.glass ? .72f : 1f));
                    material.SetTexture("_BumpMap", normal); material.SetFloat("_BumpScale", 1f);
                    material.SetTexture("_MetallicGlossMap", packed); material.SetFloat("_Smoothness", 1f);
                    material.SetFloat("_Metallic", 0f); material.SetFloat("_SmoothnessTextureChannel", 0f);
                    material.SetFloat("_WorkflowMode", 1f); material.SetFloat("_Cull", 2f);
                    if (normal) material.EnableKeyword("_NORMALMAP");
                    if (packed) material.EnableKeyword("_METALLICSPECGLOSSMAP");
                    bool cutout = !surface.glass && !string.IsNullOrEmpty(surface.alpha);
                    material.SetFloat("_AlphaClip", cutout ? 1f : 0f); material.SetFloat("_Cutoff", .35f);
                    if (cutout) material.EnableKeyword("_ALPHATEST_ON");
                    material.SetFloat("_Surface", surface.glass ? 1f : 0f);
                    material.SetFloat("_Blend", 0f);
                    material.SetFloat("_SrcBlend", (float)(surface.glass ? BlendMode.SrcAlpha : BlendMode.One));
                    material.SetFloat("_DstBlend", (float)(surface.glass ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
                    material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                    material.SetFloat("_DstBlendAlpha", (float)(surface.glass ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
                    material.SetFloat("_ZWrite", surface.glass ? 0f : 1f);
                    material.SetOverrideTag("RenderType", surface.glass ? "Transparent" : cutout ? "TransparentCutout" : "Opaque");
                    material.renderQueue = surface.glass ? (int)RenderQueue.Transparent : cutout ? (int)RenderQueue.AlphaTest : -1;
                    if (surface.glass) material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.SetTexture("_EmissionMap", emission);
                    material.SetColor("_EmissionColor", emission ? Color.white : Color.black);
                    if (emission) material.EnableKeyword("_EMISSION");
                    material.globalIlluminationFlags = emission ? MaterialGlobalIlluminationFlags.BakedEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                    EditorUtility.SetDirty(material); materials.Add(surface.key, material);
                }
                if (asset.kind == "ground")
                {
                    results.Add(new Result { id = asset.id, materialCount = materials.Count, metreBoundsMatch = true });
                    continue;
                }
                RequireOwned(asset.model); RequireOwned(asset.prefab);
                AssetDatabase.ImportAsset(asset.model, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(asset.model) as ModelImporter;
                if (!importer) throw new InvalidDataException("Missing FBX importer: " + asset.model);
                importer.globalScale = 1f; importer.useFileScale = true;
                importer.importAnimation = false; importer.importCameras = false; importer.importLights = false;
                importer.addCollider = false; importer.isReadable = false;
                importer.importNormals = ModelImporterNormals.Import;
                importer.importTangents = ModelImporterTangents.CalculateMikk;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                importer.SaveAndReimport();
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(asset.model);
                if (!source) throw new InvalidDataException("FBX did not import: " + asset.model);
                GameObject root = new GameObject("PH_" + asset.id);
                try
                {
                    GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
                    // Preserve the imported FBX axis/unit conversion. With one mesh Unity can
                    // place that conversion on this root; with several meshes it is on children.
                    // Resetting the root rotates single-mesh rocks onto their side.
                    Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length == 0) throw new InvalidDataException("No rendered geometry: " + asset.id);
                    Bounds bounds = renderers[0].bounds;
                    foreach (Renderer renderer in renderers)
                    {
                        Material[] assigned = renderer.sharedMaterials;
                        for (int i = 0; i < assigned.Length; i++)
                        {
                            bool glass = assigned[i] && assigned[i].name.IndexOf("glass", StringComparison.OrdinalIgnoreCase) >= 0;
                            string key = glass && materials.ContainsKey(asset.id + "_glass") ? asset.id + "_glass" : asset.id;
                            assigned[i] = materials[key];
                        }
                        renderer.sharedMaterials = assigned; bounds.Encapsulate(renderer.bounds);
                    }
                    int triangles = 0;
                    foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                        if (filter.sharedMesh)
                            for (int i = 0; i < filter.sharedMesh.subMeshCount; i++) triangles += (int)filter.sharedMesh.GetIndexCount(i) / 3;
                    var box = root.AddComponent<BoxCollider>(); box.center = bounds.center; box.size = bounds.size;
                    Vector3 expected = new Vector3(asset.dimensions_m_unity_xyz[0], asset.dimensions_m_unity_xyz[1], asset.dimensions_m_unity_xyz[2]);
                    bool sizeMatches = (bounds.size - expected).magnitude <= Mathf.Max(.002f, expected.magnitude * .01f);
                    if (!sizeMatches) throw new InvalidDataException("Unexpected FBX metre scale for " + asset.id + ": " + bounds.size + " vs " + expected);
                    if (Mathf.Abs(bounds.min.y) > .01f) throw new InvalidDataException("Bottom-centre pivot moved on import: " + asset.id);
                    PrefabUtility.SaveAsPrefabAsset(root, asset.prefab);
                    results.Add(new Result { id=asset.id, prefab=asset.prefab, centreMetres=bounds.center, sizeMetres=bounds.size,
                        triangles=triangles, materialCount=materials.Count, metreBoundsMatch=sizeMatches });
                }
                finally { Object.DestroyImmediate(root); }
            }
            AssetDatabase.SaveAssets();
            string evidencePath = "docs/art-direction/polyhaven-import-20261115/unity-import-evidence.json";
            Directory.CreateDirectory(Path.GetDirectoryName(evidencePath));
            File.WriteAllText(evidencePath, JsonUtility.ToJson(new Evidence { editor=Application.unityVersion, assets=results.ToArray() }, true));
            Debug.Log("POLY_HAVEN_IMPORT_PASS " + results.Count + " curated CC0 model/material entries. No scene changed. " + evidencePath);
        }

        private static Texture2D Texture(string path, bool normal, bool srgb, int maxSize)
        {
            if (string.IsNullOrEmpty(path)) return null;
            RequireOwned(path); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new InvalidDataException("Texture importer unavailable: " + path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            // Prepared files are flat UV maps, never cubemaps (including square maps).
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = srgb; importer.convertToNormalmap = false;
            importer.maxTextureSize = maxSize; importer.mipmapEnabled = true; importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.wrapMode = TextureWrapMode.Repeat; importer.anisoLevel = 4;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.SaveAndReimport();
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (!texture)
                throw new InvalidDataException("Texture2D import failed: " + path + "; main asset type="
                    + AssetDatabase.GetMainAssetTypeAtPath(path) + "; shape=" + importer.textureShape);
            if (texture.width > maxSize || texture.height > maxSize)
                throw new InvalidDataException("Texture size validation failed: " + path + "; imported="
                    + texture.width + "x" + texture.height + "; limit=" + maxSize);
            return texture;
        }
        private static void RequireOwned(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith(Root + "/", StringComparison.Ordinal) || path.Contains(".."))
                throw new InvalidDataException("Asset path must stay in the curated Poly Haven folder.");
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\','/');
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
