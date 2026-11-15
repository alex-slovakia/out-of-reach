using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutOfReach.Presentation.CelestialSky.Editor
{
    /// <summary>
    /// Integrator-run installer. Verifies the NASA SVS 8K star map, builds the celestial sky material (true
    /// orientation, real-size Sun, Earth point), then, only in the open Bootstrap scene, swaps RenderSettings.skybox
    /// from NasaDeepSky.mat to it and adds one presenter. It marks the scene dirty and never saves.
    /// </summary>
    public static class CelestialSkyInstaller
    {
        public const string SkyFolder = "Assets/_OutOfReach/Visuals/Environment/NasaDeepSkyV01";
        public const string SourceMaterialPath = SkyFolder + "/NasaDeepSky.mat";
        public const string CelestialMaterialPath = SkyFolder + "/NasaDeepSky_Celestial.mat";
        public const string StarMap8kPath = SkyFolder + "/starmap_2020_8k.exr";
        public const string StarMap8kSha256 = "dc6c4f413e85707a29a25a9451148154554ecca2c996f84fa8f47b65ef9ff7c4";
        public const string ShaderName = "OutOfReach/Skybox/Celestial Panoramic";
        public const string PresenterObjectName = "Celestial sky orientation - Horizons Sun + NASA SVS stars";
        /// <summary>[Gamma] 1.0 = linear 1.0: the NASA map's own pixel values, unscaled (the camera grade still applies).</summary>
        public const float NativeExposure = 1f;
        public const float SunRadiance = 60f;

        [MenuItem("OUT OF REACH/Presentation/Celestial sky/Build material only")]
        public static Material EnsureMaterial()
        {
            Shader shader = Shader.Find(ShaderName);
            if (!shader) throw new InvalidOperationException("Shader not found: " + ShaderName);
            Texture2D map = VerifiedStarMap();
            Material material = AssetDatabase.LoadAssetAtPath<Material>(CelestialMaterialPath);
            bool created = !material;
            if (created)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, CelestialMaterialPath);
            }
            material.shader = shader;
            material.shaderKeywords = Array.Empty<string>();
            material.SetTexture("_MainTex", map);
            material.SetColor("_Tint", new Color(.5f, .5f, .5f, .5f));
            if (created)
            {
                material.SetFloat("_Exposure", NativeExposure); // later art choices are kept
                material.SetFloat("_SunRadiance", SunRadiance);
                material.SetVector("_EarthTint", new Vector4(.90f, 1f, 1.22f, 0f));
            }
            material.SetVector("_SkyRotation", CelestialSkyFrame.ToShaderVector(CelestialEphemeris2026.DefaultSkyFromWorld));
            material.SetVector("_SunSkyDirection", CelestialSkyFrame.SunShaderVector(0));
            material.SetVector("_EarthSkyDirection", CelestialSkyFrame.EarthShaderVector(0));
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            Debug.Log("CELESTIAL_SKY_MATERIAL_PASS | " + CelestialMaterialPath + " | 8K SHA-256 verified | exposure "
                + material.GetFloat("_Exposure") + " | day-0 rotation " + CelestialEphemeris2026.DefaultSkyFromWorld.ToString("F6"));
            return material;
        }

        [MenuItem("OUT OF REACH/Presentation/Celestial sky/Apply native map exposure (1.0)")]
        public static void ApplyNativeExposure()
        {
            Material material = EnsureMaterial();
            material.SetFloat("_Exposure", NativeExposure);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            Debug.Log("CELESTIAL_SKY_EXPOSURE | NasaDeepSky_Celestial.mat exposure " + NativeExposure + " (native map values).");
        }

        [MenuItem("OUT OF REACH/Presentation/Celestial sky/Install in open Bootstrap scene")]
        public static void InstallInOpenScene()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Install in Edit mode.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != "Bootstrap") throw new InvalidOperationException("Open Bootstrap.unity first.");
            Material celestial = EnsureMaterial();
            Material source = AssetDatabase.LoadAssetAtPath<Material>(SourceMaterialPath);
            if (RenderSettings.skybox == source) RenderSettings.skybox = celestial;
            else if (RenderSettings.skybox != celestial)
                Debug.LogWarning("CELESTIAL_SKY | RenderSettings.skybox is neither NasaDeepSky.mat nor the celestial material; left unchanged.");

            CelestialSkyPresenter presenter = UnityEngine.Object.FindFirstObjectByType<CelestialSkyPresenter>(FindObjectsInactive.Include);
            if (!presenter)
            {
                var go = new GameObject(PresenterObjectName);
                SceneManager.MoveGameObjectToScene(go, scene);
                presenter = go.AddComponent<CelestialSkyPresenter>();
                Undo.RegisterCreatedObjectUndo(go, "Install celestial sky");
            }
            if (RenderSettings.sun) presenter.ConfigureSun(RenderSettings.sun);
            EditorUtility.SetDirty(presenter);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("CELESTIAL_SKY_INSTALL_PASS | skybox=" + (RenderSettings.skybox ? RenderSettings.skybox.name : "none")
                + " | presenter on '" + presenter.gameObject.name + "' | sun=" + (RenderSettings.sun ? RenderSettings.sun.name : "none")
                + " | scene marked dirty, NOT saved. Optional: bind presenter.Configure(runtime.Clock); see Integration~.");
        }

        private static Texture2D VerifiedStarMap()
        {
            if (!File.Exists(StarMap8kPath)) throw new FileNotFoundException("NASA SVS 8K star map missing (git lfs pull?).", StarMap8kPath);
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(StarMap8kPath))
            {
                string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                if (actual != StarMap8kSha256) throw new InvalidDataException("NASA 8K star map SHA-256 mismatch: " + actual);
            }
            Texture2D map = AssetDatabase.LoadAssetAtPath<Texture2D>(StarMap8kPath);
            if (!map || map.width != 8192 || map.height != 4096)
                throw new InvalidDataException("Expected an 8192 x 4096 Texture2D (check the import max size is 8192).");
            return map;
        }
    }
}
