using System;
using System.Linq;
using OutOfReach.Composition;
using OutOfReach.Presentation.Cinematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OutOfReach.Presentation.LaunchVfxMax.Editor
{
    /// <summary>Adds one LaunchVfxMaxPresenter to the open Bootstrap scene, bound to the authored marine anchors.
    /// Idempotent. Never touches the departure timeline, LaunchFxV03, the vehicle or the camera.</summary>
    public static class LaunchVfxMaxInstaller
    {
        public const string ObjectName = "Launch VFX MAX — Particle Pack (sampled, no authority)";
        private const string Pack = "Assets/UnityTechnologies/ParticlePack/EffectExamples/";
        private const string PreviewType = "OutOfReach.Presentation.ParticleLaunchPreview.ParticleLaunchPreviewPresenter";

        [MenuItem("Out of Reach/Launch/Install Launch VFX MAX (Particle Pack)")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before installing launch VFX.");
            var candidates = UnityEngine.Object.FindObjectsByType<I06MarineBindings>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (candidates.Length != 1) throw new InvalidOperationException("Open Bootstrap with exactly one I06MarineBindings.");
            var binding = candidates[0];
            var serialized = new SerializedObject(binding);
            var timeline = Read<TransportDeparturePresenter>(serialized, "departure", true);
            var nozzle = Read<Transform>(serialized, "nozzleFrame", true);
            var pad = Read<Transform>(serialized, "launchPad", true);
            var deflector = Read<Transform>(serialized, "launchDeflector", false);
            var outlet = Read<Transform>(serialized, "launchTrenchExit", false);

            var flame = Prefab("Fire & Explosion Effects/Prefabs/FlameStream.prefab", true);
            var smoke = Prefab("Smoke & Steam Effects/Prefabs/RocketTrail.prefab", true);
            var dust = Prefab("Smoke & Steam Effects/Prefabs/DustStorm.prefab", true);
            var sparks = Prefab("Misc Effects/Prefabs/SparksEffect.prefab", false);
            var heat = Prefab("Smoke & Steam Effects/Prefabs/HeatDistortion.prefab", false);
            var steam = Prefab("Smoke & Steam Effects/Prefabs/PressurisedSteam.prefab", false);

            var scene = binding.gameObject.scene;
            var existing = UnityEngine.Object.FindObjectsByType<LaunchVfxMaxPresenter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (existing.Length > 1) throw new InvalidOperationException("Duplicate Launch VFX MAX owners; remove extras first.");
            var presenter = existing.SingleOrDefault();
            if (!presenter)
            {
                var go = new GameObject(ObjectName);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
                Undo.RegisterCreatedObjectUndo(go, "Install Launch VFX MAX");
                presenter = Undo.AddComponent<LaunchVfxMaxPresenter>(go);
            }
            Undo.RecordObject(presenter, "Bind Launch VFX MAX");
            presenter.Configure(timeline, nozzle, deflector, pad, outlet, flame, smoke, dust, sparks, heat, steam);
            EditorUtility.SetDirty(presenter);

            int retired = RetirePreview();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"LAUNCH_VFX_MAX_INSTALLED {LaunchVfxMaxPresenter.Version}: fire+embers, smoke column, flank dust/debris/sand, "
                + $"sparks={(sparks ? "on" : "missing")}, heat={(heat ? "on (needs URP opaque texture)" : "missing")}, "
                + $"trench jet={(steam ? "on" : "missing")}, plume light. Retired ParticleLaunchPreview owners: {retired}. "
                + "LaunchFxV03 plume/steam/pad light kept. Timeline, camera shake and grass gust untouched.");
        }

        [MenuItem("Out of Reach/Launch/Remove Launch VFX MAX")]
        public static void Remove()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before removing launch VFX.");
            foreach (var presenter in UnityEngine.Object.FindObjectsByType<LaunchVfxMaxPresenter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var scene = presenter.gameObject.scene;
                Undo.DestroyObjectImmediate(presenter.gameObject);
                EditorSceneManager.MarkSceneDirty(scene);
            }
            Debug.Log("LAUNCH_VFX_MAX_REMOVED (any retired ParticleLaunchPreview object stays inactive; re-enable it by hand if wanted).");
        }

        /// <summary>Alex's uncommitted preview doubled V03's trench steam; it is superseded, not deleted.
        /// Matched by type name because that assembly is not in the repository.</summary>
        private static int RetirePreview()
        {
            int count = 0;
            foreach (var behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!behaviour || behaviour.GetType().FullName != PreviewType) continue;
                Undo.RecordObject(behaviour, "Retire particle launch preview");
                behaviour.enabled = false;
                Undo.RecordObject(behaviour.gameObject, "Retire particle launch preview");
                behaviour.gameObject.SetActive(false);
                EditorUtility.SetDirty(behaviour.gameObject);
                count++;
            }
            return count;
        }

        private static T Read<T>(SerializedObject source, string name, bool required) where T : UnityEngine.Object
        {
            var property = source.FindProperty(name);
            var value = property == null ? null : property.objectReferenceValue as T;
            if (value || !required) return value;
            throw new InvalidOperationException("Missing authored I06MarineBindings reference: " + name);
        }

        private static GameObject Prefab(string relative, bool required)
        {
            string path = Pack + relative;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            bool valid = prefab && prefab.GetComponentsInChildren<ParticleSystem>(true).Length > 0
                && prefab.GetComponentsInChildren<ParticleSystemRenderer>(true).All(r => r.sharedMaterial && r.sharedMaterial.shader);
            if (valid) return prefab;
            if (required) throw new InvalidOperationException("Import the Unity Particle Pack (Assets/UnityTechnologies/ParticlePack): " + path);
            Debug.LogWarning("Launch VFX MAX: optional Particle Pack prefab missing, layer skipped: " + path);
            return null;
        }
    }
}
