using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutOfReach.Presentation.CelestialSky.Editor
{
    /// <summary>
    /// Integrator-run installer. Builds the celestial sky material from the existing NASA deep-sky material
    /// (same texture, tint and exposure, so the look is unchanged apart from orientation), then, only in the
    /// open Bootstrap scene, swaps RenderSettings.skybox from NasaDeepSky.mat to it and adds one presenter.
    /// It marks the scene dirty and never saves; the integrator reviews and saves.
    /// </summary>
    public static class CelestialSkyInstaller
    {
        public const string SkyFolder = "Assets/_OutOfReach/Visuals/Environment/NasaDeepSkyV01";
        public const string SourceMaterialPath = SkyFolder + "/NasaDeepSky.mat";
        public const string CelestialMaterialPath = SkyFolder + "/NasaDeepSky_Celestial.mat";
        public const string ShaderName = "OutOfReach/Skybox/Celestial Panoramic";
        public const string PresenterObjectName = "Celestial sky orientation - Horizons Sun + NASA SVS stars";

        [MenuItem("OUT OF REACH/Presentation/Celestial sky/Build material only")]
        public static Material EnsureMaterial()
        {
            Shader shader = Shader.Find(ShaderName);
            if (!shader) throw new InvalidOperationException("Shader not found: " + ShaderName);
            Material source = AssetDatabase.LoadAssetAtPath<Material>(SourceMaterialPath);
            if (!source) throw new InvalidOperationException("Source sky material missing: " + SourceMaterialPath);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(CelestialMaterialPath);
            bool created = !material;
            if (created)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, CelestialMaterialPath);
            }
            material.shader = shader;
            material.shaderKeywords = Array.Empty<string>();
            material.SetTexture("_MainTex", source.GetTexture("_MainTex"));
            material.SetColor("_Tint", source.GetColor("_Tint"));
            if (created) material.SetFloat("_Exposure", source.GetFloat("_Exposure")); // later art choices are kept
            material.SetVector("_SkyRotation", CelestialSkyFrame.ToShaderVector(CelestialEphemeris2026.DefaultSkyFromWorld));
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            Debug.Log("CELESTIAL_SKY_MATERIAL_PASS | " + CelestialMaterialPath + " | exposure "
                + material.GetFloat("_Exposure") + (created ? " copied from NasaDeepSky.mat" : " kept") + " | static day-0 rotation "
                + CelestialEphemeris2026.DefaultSkyFromWorld.ToString("F6"));
            return material;
        }

        /// <summary>
        /// Proposed, NOT applied by default: [Gamma] slider 0.60 (linear x0.319 vs x0.0196 at 0.15). Estimated
        /// display: Milky Way band median ~8/255, 90th percentile ~27/255, dark sky stays 0, brightest stars
        /// ~153/255 with HDR below the 1.25 bloom threshold. Static: no animation, flicker or adaptation, so it
        /// is unaffected by reduced effects. Adopting it requires the credit to say "exposure-adjusted".
        /// </summary>
        public const float ProposedExposure = .60f;

        [MenuItem("OUT OF REACH/Presentation/Celestial sky/Apply proposed Milky Way exposure (0.60)")]
        public static void ApplyProposedExposure()
        {
            Material material = EnsureMaterial();
            material.SetFloat("_Exposure", ProposedExposure);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            Debug.Log("CELESTIAL_SKY_EXPOSURE | NasaDeepSky_Celestial.mat exposure " + ProposedExposure
                + " (exposure-adjusted; update the sky credit). NasaDeepSky.mat is unchanged.");
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
    }
}
