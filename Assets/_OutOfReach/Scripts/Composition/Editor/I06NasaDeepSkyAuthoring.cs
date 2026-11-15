using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace OutOfReach.Composition.Editor
{
    /// <summary>Imports a dated NASA catalogue sky. Does not assign scene or runtime state.</summary>
    public static class I06NasaDeepSkyAuthoring
    {
        public const string Root = "Assets/_OutOfReach/Visuals/Environment/NasaDeepSkyV01";
        public const string TexturePath = Root + "/starmap_2020_4k.exr";
        public const string MaterialPath = Root + "/NasaDeepSky.mat";
        public const string DeepViewMaterialPath = Root + "/NasaDeepSky_DeepView.mat";
        public const float BrightShipExposure = .15f;
        public const float DeepViewExposure = .45f;
        private const string SourceSha256 = "69b841cd048c2ef35543eb1c5819b322530e78aa4b0cab97ec1de3ef56a4ddcd";

        [MenuItem("OUT OF REACH/Presentation/Prepare NASA deep sky")]
        public static void EnsureAssets()
        {
            if (Application.isPlaying || Application.unityVersion != "6000.3.28f1")
                throw new InvalidOperationException("Prepare the sky in Edit mode with Unity 6000.3.28f1.");
            if (!File.Exists(TexturePath)) throw new FileNotFoundException("Committed NASA 4K source is missing.", TexturePath);
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(TexturePath))
            {
                string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                if (actual != SourceSha256) throw new InvalidDataException("NASA source SHA-256 mismatch: " + actual);
            }
            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (!importer) throw new InvalidDataException("NASA EXR texture importer unavailable.");
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = false; // Source half-float EXR is linear light.
            importer.maxTextureSize = 4096;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapModeU = TextureWrapMode.Repeat;
            importer.wrapModeV = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 1;
            importer.mipmapEnabled = true;
            importer.isReadable = false;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            var platform = importer.GetPlatformTextureSettings("Standalone");
            platform.name = "Standalone"; platform.overridden = true;
            platform.maxTextureSize = 4096; platform.format = TextureImporterFormat.BC6H;
            platform.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SetPlatformTextureSettings(platform);
            importer.SaveAndReimport();
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (!texture || texture.width != 4096 || texture.height != 2048)
                throw new InvalidDataException("Expected a 4096 x 2048 NASA Texture2D.");
            Shader shader = Shader.Find("Skybox/Panoramic");
            if (!shader) throw new InvalidOperationException("Built-in Skybox/Panoramic shader is unavailable.");
            BuildMaterial(MaterialPath, texture, shader, BrightShipExposure);
            BuildMaterial(DeepViewMaterialPath, texture, shader, DeepViewExposure);
            AssetDatabase.SaveAssets();
            Debug.Log("NASA_DEEP_SKY_ASSETS_PASS | 4096x2048 | SHA256=" + SourceSha256
                + " | static ICRF/J2000 reference | artistic exposure presets; no scene changed");
        }

        private static void BuildMaterial(string path, Texture2D texture, Shader shader, float exposure)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.shaderKeywords = Array.Empty<string>();
            material.SetTexture("_MainTex", texture);
            material.SetColor("_Tint", new Color(.5f, .5f, .5f, .5f));
            material.SetFloat("_Exposure", exposure);
            material.SetFloat("_Rotation", 0f);
            material.SetFloat("_Mapping", 1f); // Latitude-longitude, no constellation overlays.
            material.SetFloat("_ImageType", 0f); // Full 360 degrees.
            material.SetFloat("_MirrorOnBack", 0f);
            material.SetFloat("_Layout", 0f); // Mono; never stereo side-by-side.
            EditorUtility.SetDirty(material);
        }
    }
}
