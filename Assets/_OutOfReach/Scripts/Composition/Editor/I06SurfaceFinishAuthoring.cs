using System;
using UnityEditor;
using UnityEngine;

namespace OutOfReach.Composition.Editor
{
    /// <summary>Opt-in CC0 finishes for newly authored prop brackets/panels only.</summary>
    public static class I06SurfaceFinishAuthoring
    {
        public const string AssetRoot = "Assets/_OutOfReach/Visuals/Environment/SurfaceFinishV01";
        public const string BrushedSteelPath = AssetRoot + "/BrushedSteel.mat";
        public const string PowderCoatPath = AssetRoot + "/PowderCoat.mat";
        public static Material BrushedSteel => AssetDatabase.LoadAssetAtPath<Material>(BrushedSteelPath);
        public static Material PowderCoat => AssetDatabase.LoadAssetAtPath<Material>(PowderCoatPath);

        public static void EnsureMaterials()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Author surfaces in Edit mode.");
            Build("Metal009", BrushedSteelPath, "CC0 brushed steel", .35f);
            Build("Metal027", PowderCoatPath, "CC0 graphite powder coat", .22f);
        }

        /// <summary>Only Finish_Steel_* and Finish_Powder_* opt-in renderers are changed.</summary>
        public static void Apply(Transform newPropRoot)
        {
            if (!newPropRoot) throw new ArgumentNullException(nameof(newPropRoot));
            EnsureMaterials();
            int changed = 0;
            foreach (MeshRenderer renderer in newPropRoot.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material finish = renderer.name.StartsWith("Finish_Steel_", StringComparison.Ordinal) ? BrushedSteel
                    : renderer.name.StartsWith("Finish_Powder_", StringComparison.Ordinal) ? PowderCoat : null;
                if (!finish) continue;
                if (renderer.sharedMaterials.Length != 1)
                    throw new InvalidOperationException("Opt-in metal finish requires a single-slot new mesh: " + renderer.name);
                renderer.sharedMaterial = finish; changed++;
            }
            Debug.Log("I06_SURFACE_FINISH_READY explicitly named new renderers=" + changed);
        }

        private static void Build(string id, string path, string displayName, float normalStrength)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("URP Lit is required.");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.name = displayName; material.shader = shader;
            material.SetTexture("_BaseMap", Texture(id + "_Color.jpg", false, false));
            material.SetTexture("_BumpMap", Texture(id + "_NormalGL.jpg", true, false));
            material.SetTexture("_MetallicGlossMap", Texture(id + "_MetallicSmoothness.png", false, true));
            material.SetColor("_BaseColor", Color.white);
            material.SetTextureScale("_BaseMap", Vector2.one);
            material.SetFloat("_BumpScale", normalStrength); material.SetFloat("_Smoothness", 1);
            material.SetFloat("_Metallic", 1); material.SetFloat("_SmoothnessTextureChannel", 0);
            material.SetFloat("_Surface", 0); material.SetFloat("_Cull", 2); material.SetFloat("_ZWrite", 1);
            material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICSPECGLOSSMAP");
            EditorUtility.SetDirty(material);
        }

        private static Texture2D Texture(string filename, bool normal, bool linear)
        {
            string path = AssetRoot + "/Textures/" + filename;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new InvalidOperationException("Missing metal map " + path);
            TextureImporterType type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            bool srgb = !normal && !linear;
            if (importer.textureType != type || importer.sRGBTexture != srgb || importer.maxTextureSize != 1024
                || importer.wrapMode != TextureWrapMode.Repeat || importer.anisoLevel != 8 || !importer.mipmapEnabled)
            {
                importer.textureType = type; importer.sRGBTexture = srgb; importer.maxTextureSize = 1024;
                importer.wrapMode = TextureWrapMode.Repeat; importer.anisoLevel = 8; importer.mipmapEnabled = true;
                importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
