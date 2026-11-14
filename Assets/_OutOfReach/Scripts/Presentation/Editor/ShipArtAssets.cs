using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace OutOfReach.Presentation.Editor
{
    /// <summary>Editor-only adaptation of the reviewed CC0 Power Box source; original binary bytes stay unchanged.</summary>
    internal static class ShipArtAssets
    {
        private const string AssetRoot = "Assets/_OutOfReach/Visuals/ThirdParty/PolyHaven/PowerBox01";
        private const string ModelPath = AssetRoot + "/power_box_01_2k.fbx";
        private const string TextureRoot = AssetRoot + "/textures/power_box_01_";
        private const string PackedPath = AssetRoot + "/power_box_01_metallic_smoothness.png";
        private const string MaterialPath = AssetRoot + "/PowerBox01_URP.mat";
        private static Material powerBoxMaterial;

        [Serializable]
        private sealed class SourceManifest { public SourceFile[] files; }
        [Serializable]
        private sealed class SourceFile { public string relative_name; public string sha256; public long actual_bytes; }

        public static void Prepare()
        {
            VerifySourceBytes();
            ConfigureModel();
            ConfigureTexture(TextureRoot + "diff_2k.jpg", true, false, false);
            ConfigureTexture(TextureRoot + "nor_gl_2k.exr", false, true, false);
            string metalPath = TextureRoot + "metal_2k.exr";
            string roughPath = TextureRoot + "rough_2k.exr";
            try
            {
                ConfigureTexture(metalPath, false, false, true);
                ConfigureTexture(roughPath, false, false, true);
                PackMetallicSmoothness(metalPath, roughPath);
            }
            finally
            {
                // CPU copies are only needed during packing, not in the saved presentation assets.
                ConfigureTexture(metalPath, false, false, false);
                ConfigureTexture(roughPath, false, false, false);
            }
            ConfigureTexture(PackedPath, false, false, false);
            MakeMaterial();
            Debug.Log("V01_ART_SOURCE_PASS PowerBox01 sourceFiles=5 SHA256=manifest-match CC0 originalBytesUnchanged=true");
        }

        private static void VerifySourceBytes()
        {
            var manifest = JsonUtility.FromJson<SourceManifest>(File.ReadAllText(AssetRoot + "/manifest.json"));
            string[] expected = { "power_box_01_2k.fbx", "textures/power_box_01_diff_2k.jpg", "textures/power_box_01_nor_gl_2k.exr", "textures/power_box_01_metal_2k.exr", "textures/power_box_01_rough_2k.exr" };
            if (manifest?.files == null || manifest.files.Length != expected.Length ||
                !manifest.files.Select(file => file.relative_name).OrderBy(path => path).SequenceEqual(expected.OrderBy(path => path)))
                throw new InvalidOperationException("Power Box manifest must describe exactly the reviewed model and four maps.");
            foreach (var file in manifest.files)
            {
                string path = AssetRoot + "/" + file.relative_name;
                using (var stream = File.OpenRead(path))
                using (var sha = SHA256.Create())
                {
                    if (stream.Length != file.actual_bytes) throw new InvalidOperationException("Power Box source size mismatch: " + path);
                    string hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                    if (!string.Equals(hash, file.sha256, StringComparison.Ordinal))
                        throw new InvalidOperationException("Power Box source SHA-256 mismatch: " + path);
                }
            }
        }

        private static void ConfigureModel()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (!importer) throw new InvalidOperationException("Reviewed Power Box FBX is not imported.");
            if (importer.materialImportMode == ModelImporterMaterialImportMode.None && !importer.importAnimation &&
                importer.animationType == ModelImporterAnimationType.None && !importer.addCollider &&
                importer.importTangents == ModelImporterTangents.CalculateMikk) return;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.addCollider = false;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.SaveAndReimport();
        }

        private static void ConfigureTexture(string path, bool srgb, bool normal, bool readable)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new InvalidOperationException("Power Box texture is not imported: " + path);
            TextureImporterType type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            var compression = readable ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
            if (importer.textureType == type && importer.sRGBTexture == srgb && importer.isReadable == readable &&
                importer.maxTextureSize == 2048 && importer.textureCompression == compression &&
                !importer.alphaIsTransparency && !importer.convertToNormalmap && !importer.flipGreenChannel) return;
            importer.textureType = type;
            importer.sRGBTexture = srgb;
            importer.isReadable = readable;
            importer.maxTextureSize = 2048;
            importer.textureCompression = compression;
            importer.alphaIsTransparency = false;
            importer.convertToNormalmap = false;
            // The source is explicitly OpenGL (+Y); Unity's normal-map importer accepts this convention.
            importer.flipGreenChannel = false;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }

        private static void PackMetallicSmoothness(string metalPath, string roughPath)
        {
            var metal = AssetDatabase.LoadAssetAtPath<Texture2D>(metalPath);
            var rough = AssetDatabase.LoadAssetAtPath<Texture2D>(roughPath);
            if (!metal || !rough || metal.width != rough.width || metal.height != rough.height)
                throw new InvalidOperationException("Metallic and roughness maps must have identical dimensions.");
            var metalPixels = metal.GetPixels32();
            var roughPixels = rough.GetPixels32();
            for (int i = 0; i < metalPixels.Length; i++)
                metalPixels[i] = new Color32(metalPixels[i].r, 0, 0, (byte)(255 - roughPixels[i].r));
            var packed = new Texture2D(metal.width, metal.height, TextureFormat.RGBA32, false, true);
            try
            {
                packed.SetPixels32(metalPixels);
                packed.Apply(false, false);
                byte[] bytes = packed.EncodeToPNG();
                if (!File.Exists(PackedPath) || !File.ReadAllBytes(PackedPath).SequenceEqual(bytes)) File.WriteAllBytes(PackedPath, bytes);
                AssetDatabase.ImportAsset(PackedPath, ImportAssetOptions.ForceSynchronousImport);
            }
            finally { UnityEngine.Object.DestroyImmediate(packed); }
        }

        private static void MakeMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("Existing URP/Lit shader is unavailable.");
            powerBoxMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (!powerBoxMaterial)
            {
                powerBoxMaterial = new Material(shader) { name = "Power Box 01 - CC0 URP adaptation" };
                AssetDatabase.CreateAsset(powerBoxMaterial, MaterialPath);
            }
            powerBoxMaterial.shader = shader;
            powerBoxMaterial.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TextureRoot + "diff_2k.jpg"));
            powerBoxMaterial.SetColor("_BaseColor", Color.white);
            powerBoxMaterial.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TextureRoot + "nor_gl_2k.exr"));
            powerBoxMaterial.SetFloat("_BumpScale", 1);
            powerBoxMaterial.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(PackedPath));
            powerBoxMaterial.SetFloat("_WorkflowMode", 1);
            powerBoxMaterial.SetFloat("_Metallic", 1);
            powerBoxMaterial.SetFloat("_Smoothness", 1);
            powerBoxMaterial.SetFloat("_SmoothnessTextureChannel", 0);
            powerBoxMaterial.EnableKeyword("_NORMALMAP");
            powerBoxMaterial.EnableKeyword("_METALLICSPECGLOSSMAP");
            powerBoxMaterial.enableInstancing = true;
            EditorUtility.SetDirty(powerBoxMaterial);
        }

        /// <summary>Mount on rack local -Z; rack orientation maps this front to world +X.</summary>
        public static Transform AddPowerBox(Transform rack)
        {
            if (!powerBoxMaterial) throw new InvalidOperationException("Prepare Power Box assets before authoring.");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (!model) throw new InvalidOperationException("Reviewed Power Box model is unavailable.");
            var mount = new GameObject("PowerBox01_Detailed").transform;
            mount.SetParent(rack, false);
            var instance = UnityEngine.Object.Instantiate(model, mount, false);
            instance.name = "Power Box 01 - static service assembly";
            instance.transform.localPosition = Vector3.zero;
            // FBX source front is -Y before its exported -90 degree X conversion: it imports facing +Z.
            instance.transform.localRotation = Quaternion.Euler(0, 180, 0);
            instance.transform.localScale = Vector3.one;
            foreach (var animator in instance.GetComponentsInChildren<Animator>(true)) UnityEngine.Object.DestroyImmediate(animator);
            foreach (var animation in instance.GetComponentsInChildren<Animation>(true)) UnityEngine.Object.DestroyImmediate(animation);
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            bool coverFound = false;
            foreach (var renderer in renderers)
            {
                renderer.sharedMaterials = Enumerable.Repeat(powerBoxMaterial, Mathf.Max(1, renderer.sharedMaterials.Length)).ToArray();
                // A visibly removed maintenance cover reveals the detailed breakers; no runtime animation or repair state.
                if (renderer.name == "power_box_01_door") { renderer.enabled = false; coverFound = true; }
            }
            if (!coverFound) throw new InvalidOperationException("Expected source maintenance cover was not found; inspect FBX hierarchy before adapting it.");
            Bounds bounds = LocalBounds(mount);
            if (bounds.size.y < 0.001f || float.IsNaN(bounds.size.y) || float.IsInfinity(bounds.size.y)) throw new InvalidOperationException("Power Box bounds are invalid.");
            float scale = 0.85f / bounds.size.y;
            mount.localScale = Vector3.one * scale;
            mount.localPosition = new Vector3(-bounds.center.x * scale, 1.25f - bounds.center.y * scale, -0.32f - bounds.min.z * scale);
            var placedBounds = LocalBounds(rack, mount);
            if (placedBounds.min.z < -0.325f || placedBounds.max.z > 0.27f || Mathf.Max(Mathf.Abs(placedBounds.min.x), Mathf.Abs(placedBounds.max.x)) > 0.7f)
                throw new InvalidOperationException("Power Box exceeds the existing repair cabinet envelope: " + placedBounds);
            Debug.Log("V01_ART_PLACEMENT PowerBox01 height=0.85m worldFrontX=-1.70m cover=visually-removed colliders=0 boundsInRack=" + placedBounds);
            return mount;
        }

        private static Bounds LocalBounds(Transform relativeTo, Transform contents = null)
        {
            var renderers = (contents ? contents : relativeTo).GetComponentsInChildren<Renderer>(true).Where(renderer => renderer.enabled).ToArray();
            if (renderers.Length == 0) throw new InvalidOperationException("Power Box has no enabled renderers.");
            Bounds bounds = default;
            bool first = true;
            foreach (var renderer in renderers)
            {
                Bounds local = renderer.localBounds;
                var matrix = relativeTo.worldToLocalMatrix * renderer.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                    if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                    else bounds.Encapsulate(point);
                }
            }
            return bounds;
        }
    }
}
