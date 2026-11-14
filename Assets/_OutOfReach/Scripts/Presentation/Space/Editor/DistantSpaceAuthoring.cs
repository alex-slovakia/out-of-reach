using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OutOfReach.Presentation.Space.Editor
{
    /// <summary>Original illustrative star field. A skybox has angular depth and no cabin-walking parallax.</summary>
    public static class DistantSpaceAuthoring
    {
        public const string Root = "Assets/_OutOfReach/Visuals/Space";
        public const string MaterialPath = Root + "/DistantStars.mat";

        [MenuItem("OUT OF REACH/Presentation/Build distant star sky")]
        public static void Build()
        {
            const int size = 1024;
            Directory.CreateDirectory(Root);
            AssetDatabase.Refresh();
            var rng = new System.Random(74031);
            string[] faces = { "Front", "Back", "Left", "Right", "Up", "Down" };
            Shader shader = Shader.Find("Skybox/6 Sided");
            if (!shader || !shader.isSupported) throw new InvalidOperationException("Supported six-sided sky shader required.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            bool create = !material;
            if (create) material = new Material(shader) { name = "Distant stars - angular background" };
            material.shader = shader;
            foreach (string face in faces)
            {
                var pixels = new Color32[size * size];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 0, 1, 255);
                for (int star = 0; star < 780; star++)
                {
                    float x = 2f + (float)rng.NextDouble() * (size - 4);
                    float y = 2f + (float)rng.NextDouble() * (size - 4);
                    float intensity = .17f + .8f * Mathf.Pow((float)rng.NextDouble(), 3.5f);
                    float temperature = (float)rng.NextDouble();
                    Color tint = Color.Lerp(new Color(1f, .87f, .72f), new Color(.76f, .87f, 1f), temperature);
                    int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
                    // Sub-pixel point spread, not glowing nearby particles or decorative star streaks.
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        float sx = ix + dx + .5f - x, sy = iy + dy + .5f - y;
                        float value = intensity * Mathf.Exp(-(sx * sx + sy * sy) / .40f);
                        int index = (iy + dy) * size + ix + dx;
                        Color old = pixels[index];
                        pixels[index] = new Color(Mathf.Max(old.r, tint.r * value),
                            Mathf.Max(old.g, tint.g * value), Mathf.Max(old.b, tint.b * value), 1f);
                    }
                }
                var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
                texture.SetPixels32(pixels);
                texture.Apply();
                string path = Root + "/Stars" + face + ".png";
                File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = size;
                importer.SaveAndReimport();
                material.SetTexture("_" + face + "Tex", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            }
            material.SetColor("_Tint", new Color(.5f, .5f, .5f));
            material.SetFloat("_Exposure", 1f);
            material.SetFloat("_Rotation", 0f);
            if (create) AssetDatabase.CreateAsset(material, MaterialPath);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            Debug.Log("DISTANT_STAR_SKY_READY " + MaterialPath + " | original illustrative sky; not a measured star catalog; no translation parallax");
        }
    }
}
