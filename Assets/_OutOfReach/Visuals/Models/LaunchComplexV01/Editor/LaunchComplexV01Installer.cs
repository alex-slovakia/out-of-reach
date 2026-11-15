using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace OutOfReach.Visuals.LaunchComplexV01.Editor
{
    /// <summary>Adds the LaunchComplexV01 launch vehicle and pad dressing to the OPEN scene (normally Bootstrap).
    /// Additive and reversible. The vehicle prefab goes under "Transport vehicle - root controls launch" at identity,
    /// so it launches with the existing root; only the MeshRenderer of the old BoosterStack is disabled (the object,
    /// its COL_Booster and every anchor stay). The pad dressing goes under "Launch site - stationary" at identity;
    /// only the renderers of the four land-kit modules it replaces are disabled, their colliders, anchors and names
    /// stay. Nothing is deleted, moved or renamed. The scene is marked dirty, never saved: the integrator saves.</summary>
    public static class LaunchComplexV01Installer
    {
        private const string PrologueName = "I06 isolated marine prologue";
        private const string VehicleRootName = "Transport vehicle - root controls launch";
        private const string SiteName = "Launch site - stationary";
        private const string OldBooster = "BoosterStack";
        private const string BootstrapPath = "Assets/_OutOfReach/Scenes/Bootstrap.unity";
        // Land-kit modules whose visuals LaunchComplexV01 replaces in the same place.
        private static readonly string[] ReplacedSiteVisuals = { "LS_LaunchMount", "LS_Umbilicals", "LS_WaterTower", "LS_FloodTowers" };
        public const string VehicleInstanceName = "Launch vehicle V01 - AURORA booster stack";
        public const string PadInstanceName = "Launch pad dressing V01 - mount, trench, deluge, tank farm, lights, lightning";

        [MenuItem("OUT OF REACH/Launch Complex V01/Install into open scene", priority = 51)]
        public static void InstallMenu()
        {
            try { Debug.Log("LAUNCH_COMPLEX_V01_INSTALL\n" + Install(SceneManager.GetActiveScene())); }
            catch (Exception e) { EditorUtility.DisplayDialog("Launch Complex V01", e.Message, "OK"); throw; }
        }

        [MenuItem("OUT OF REACH/Launch Complex V01/Install launch vehicle only into open scene", priority = 51)]
        public static void InstallVehicleOnlyMenu()
        {
            try { Debug.Log("LAUNCH_COMPLEX_V01_INSTALL\n" + Install(SceneManager.GetActiveScene(), false)); }
            catch (Exception e) { EditorUtility.DisplayDialog("Launch Complex V01", e.Message, "OK"); throw; }
        }

        // Batch entry points for the release coordinator. They open Bootstrap, install and SAVE the scene.
        // Unity -batchmode -quit -projectPath <project> -executeMethod OutOfReach.Visuals.LaunchComplexV01.Editor.LaunchComplexV01Installer.InstallIntoBootstrap
        /// <summary>Launch vehicle (booster stack) only. Opens Bootstrap, installs, saves.</summary>
        public static void InstallIntoBootstrap() => InstallBatch(false);

        // ... -executeMethod OutOfReach.Visuals.LaunchComplexV01.Editor.LaunchComplexV01Installer.InstallWithPadDressingIntoBootstrap
        /// <summary>Launch vehicle and the pad dressing. Opens Bootstrap, installs, saves.</summary>
        public static void InstallWithPadDressingIntoBootstrap() => InstallBatch(true);

        private static void InstallBatch(bool padDressing)
        {
            Scene scene = EditorSceneManager.OpenScene(BootstrapPath, OpenSceneMode.Single);
            Debug.Log("LAUNCH_COMPLEX_V01_INSTALL padDressing=" + padDressing + "\n" + Install(scene, padDressing));
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save " + BootstrapPath);
            Debug.Log("LAUNCH_COMPLEX_V01_INSTALLED padDressing=" + padDressing + " scene saved: " + BootstrapPath);
        }

        [MenuItem("OUT OF REACH/Launch Complex V01/Uninstall from open scene (restore old visuals)", priority = 52)]
        public static void UninstallMenu()
        {
            Debug.Log("LAUNCH_COMPLEX_V01_UNINSTALL\n" + Uninstall(SceneManager.GetActiveScene()));
        }

        public static string Install(Scene scene, bool padDressing = true)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play mode first.");
            var report = new StringBuilder();
            Transform prologue = Find(scene, PrologueName);
            Transform vehicleRoot = Child(prologue, VehicleRootName);
            Transform site = Child(prologue, SiteName);
            bool hasVehicle = Under(prologue, VehicleInstanceName).Any();
            bool hasPad = Under(prologue, PadInstanceName).Any();
            if (hasVehicle && (hasPad || !padDressing))
                return "already installed under " + PathOf(prologue) + " (nothing changed)";
            if (LaunchComplexV01Builder.NeedsBuild()) LaunchComplexV01Builder.BuildAll();

            Undo.SetCurrentGroupName("Install Launch Complex V01");
            int group = Undo.GetCurrentGroup();
            // Hide what the new visuals replace: renderers only. Objects, colliders, anchors and names are untouched.
            if (!hasVehicle)
            {
                GameObject vehicle = Place(LaunchComplexV01Builder.VehiclePrefab, vehicleRoot, VehicleInstanceName);
                report.AppendLine("vehicle under " + PathOf(vehicleRoot) + " at " + prologue.InverseTransformPoint(vehicle.transform.position).ToString("F3"));
                foreach (Transform old in vehicleRoot.Cast<Transform>().Where(t => t.name == OldBooster))
                    DisableRenderers(old, vehicle.transform, report);
            }
            if (padDressing && !hasPad)
            {
                GameObject pad = Place(LaunchComplexV01Builder.PadPrefab, site, PadInstanceName);
                report.AppendLine("pad dressing under " + PathOf(site) + " at " + prologue.InverseTransformPoint(pad.transform.position).ToString("F3"));
                foreach (string name in ReplacedSiteVisuals)
                    foreach (Transform old in Under(site, name))
                        DisableRenderers(old, pad.transform, report);
            }
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            report.AppendLine("scene marked dirty, not saved");
            return report.ToString();
        }

        public static string Uninstall(Scene scene)
        {
            var report = new StringBuilder();
            Transform prologue = Find(scene, PrologueName);
            Transform vehicleRoot = Child(prologue, VehicleRootName);
            Transform site = Child(prologue, SiteName);
            foreach (Transform t in Under(prologue, VehicleInstanceName).Concat(Under(prologue, PadInstanceName)).ToList())
            {
                report.AppendLine("removed " + PathOf(t));
                Undo.DestroyObjectImmediate(t.gameObject);
            }
            foreach (Transform old in vehicleRoot.Cast<Transform>().Where(t => t.name == OldBooster)
                .Concat(ReplacedSiteVisuals.SelectMany(n => Under(site, n))))
                foreach (Renderer r in old.GetComponents<Renderer>().Where(r => !r.enabled))
                {
                    Undo.RecordObject(r, "Uninstall Launch Complex V01");
                    r.enabled = true;
                    report.AppendLine("renderer on " + PathOf(r.transform));
                }
            EditorSceneManager.MarkSceneDirty(scene);
            return report.ToString();
        }

        private static GameObject Place(string prefabPath, Transform parent, string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)
                ?? throw new InvalidOperationException("Prefab build failed: " + prefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            Undo.RegisterCreatedObjectUndo(instance, "Install Launch Complex V01");
            instance.name = name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            instance.layer = parent.gameObject.layer;
            foreach (Transform t in instance.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = parent.gameObject.layer;
            return instance;
        }

        private static void DisableRenderers(Transform old, Transform ours, StringBuilder report)
        {
            foreach (Renderer r in old.GetComponents<Renderer>().Where(r => r.enabled && !r.transform.IsChildOf(ours)))
            {
                Undo.RecordObject(r, "Install Launch Complex V01");
                r.enabled = false;
                report.AppendLine("renderer off " + PathOf(r.transform) + " (object, colliders and children kept)");
            }
        }

        private static Transform Find(Scene scene, string name)
        {
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Open the Bootstrap scene first.");
            Transform[] found = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Where(t => t.name == name).ToArray();
            if (found.Length != 1) throw new InvalidOperationException("Expected exactly one '" + name + "' in " + scene.name + ", found " + found.Length);
            return found[0];
        }

        private static Transform Child(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++) if (root.GetChild(i).name == name) return root.GetChild(i);
            throw new InvalidOperationException("Missing child '" + name + "' under " + root.name);
        }

        private static IEnumerable<Transform> Under(Transform root, string name) =>
            root.GetComponentsInChildren<Transform>(true).Where(t => t.name == name);

        private static string PathOf(Transform t)
        {
            var parts = new List<string>();
            for (; t; t = t.parent) parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
