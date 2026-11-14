using System;
using UnityEditor;
using UnityEngine;

namespace OutOfReach.Presentation.Space.Editor
{
    /// <summary>Validates the committed GMAT angular sky without replacing its catalogue data.</summary>
    public static class DistantSpaceAuthoring
    {
        public const string Root = "Assets/_OutOfReach/Visuals/Space";
        public const string MaterialPath = Root + "/DistantStars.mat";

        [MenuItem("OUT OF REACH/Presentation/Validate GMAT star sky")]
        public static void Build()
        {
            string[] faces = { "Front", "Back", "Left", "Right", "Up", "Down" };
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            var shader = Shader.Find("Skybox/6 Sided");
            if (!material || !shader || material.shader != shader)
                throw new InvalidOperationException("The existing six-sided GMAT sky material is required.");
            foreach (string face in faces)
            {
                string path = Root + "/Stars" + face + ".png";
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (!texture || texture.width != 1024 || texture.height != 1024)
                    throw new InvalidOperationException("Missing or unexpected GMAT sky face: " + path);
                if (material.GetTexture("_" + face + "Tex") != texture)
                    throw new InvalidOperationException("GMAT sky material face binding mismatch: " + face);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (!importer || !importer.sRGBTexture || importer.mipmapEnabled
                    || importer.wrapMode != TextureWrapMode.Clamp || importer.filterMode != FilterMode.Bilinear)
                    throw new InvalidOperationException("GMAT sky face import settings require review: " + path);
            }
            Debug.Log("GMAT_SKY_BINDINGS_VALID | six 1024 faces; source/hash checks recorded separately; render inspection still required");
        }
    }
}
