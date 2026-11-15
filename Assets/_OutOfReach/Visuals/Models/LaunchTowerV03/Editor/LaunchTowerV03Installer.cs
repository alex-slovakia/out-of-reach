using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace OutOfReach.Visuals.LaunchTowerV03.Editor
{
    /// <summary>Swaps the launch tower / crew access arm visuals in the OPEN scene (normally Bootstrap) for LaunchTowerV03.
    /// Additive and reversible: the V02 detail is deactivated (not deleted), the TransportLaunch AccessMastTower and
    /// AccessArm blockout meshes only get their renderers disabled, and every existing COL_* collider, anchor, control
    /// frame and binding stays exactly where it is. The scene is marked dirty, never saved: the integrator saves.</summary>
    public static class LaunchTowerV03Installer
    {
        private const string PrologueName = "I06 isolated marine prologue";
        private const string SiteName = "Launch site - stationary";
        private const string ArmControlName = "Access arm control +Y 0 connected to -90 retracted";
        private const string V02InstanceName = "Launch tower V02 detail - b330226";
        private const string V02ArmDetailName = "LTV2_ArmDetail";
        private static readonly string[] BlockoutVisuals = { "AccessMastTower", "AccessArm" };
        public const string InstanceName = "Launch tower V03 - umbilical tower and crew access arm";
        public const string ArmInstanceName = "LTV3_AccessArm";

        [MenuItem("OUT OF REACH/Launch Tower V03/Install into open scene", priority = 41)]
        public static void InstallMenu()
        {
            try { Debug.Log("LAUNCH_TOWER_V03_INSTALL\n" + Install(SceneManager.GetActiveScene())); }
            catch (Exception e) { EditorUtility.DisplayDialog("Launch Tower V03", e.Message, "OK"); throw; }
        }

        [MenuItem("OUT OF REACH/Launch Tower V03/Uninstall from open scene (restore V02)", priority = 42)]
        public static void UninstallMenu()
        {
            Debug.Log("LAUNCH_TOWER_V03_UNINSTALL\n" + Uninstall(SceneManager.GetActiveScene()));
        }

        // Batch: Unity -batchmode -quit -projectPath <clone> -executeMethod OutOfReach.Visuals.LaunchTowerV03.Editor.LaunchTowerV03Installer.InstallIntoBootstrapBatch
        public static void InstallIntoBootstrapBatch()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/_OutOfReach/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            Debug.Log("LAUNCH_TOWER_V03_INSTALL\n" + Install(scene));
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save Bootstrap.");
        }

        public static string Install(Scene scene)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play mode first.");
            var report = new StringBuilder();
            Transform prologue = Find(scene, PrologueName);
            Transform site = Child(prologue, SiteName);
            Transform armControl = Under(site, ArmControlName).SingleOrDefault()
                ?? throw new InvalidOperationException("Missing '" + ArmControlName + "' under the launch site.");
            if (Under(site, InstanceName).Any())
                return "already installed under " + PathOf(site) + " (nothing changed)";
            if (Quaternion.Angle(armControl.localRotation, Quaternion.identity) > .25f)
                throw new InvalidOperationException("Save the access arm in its connected pose (+Y 0) before installing.");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LaunchTowerV03Builder.PrefabPath);
            if (!prefab)
            {
                LaunchTowerV03Builder.BuildAll();
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LaunchTowerV03Builder.PrefabPath)
                    ?? throw new InvalidOperationException("Prefab build failed: " + LaunchTowerV03Builder.PrefabPath);
            }

            Undo.SetCurrentGroupName("Install Launch Tower V03");
            int group = Undo.GetCurrentGroup();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, prologue);
            Undo.RegisterCreatedObjectUndo(instance, "Install Launch Tower V03");
            // The arm must live under the arm control frame, so the instance cannot stay a connected prefab instance.
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = InstanceName;
            // Same frame as TransportLaunch.fbx and LaunchTowerV02: identity under the prologue root, kept with the site.
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            Undo.SetTransformParent(instance.transform, site, "Install Launch Tower V03");
            Transform arm = Under(instance.transform, LaunchTowerV03Builder.ArmName).Single();
            Undo.SetTransformParent(arm, armControl, "Install Launch Tower V03");
            arm.name = ArmInstanceName;
            Vector3 pivot = prologue.InverseTransformPoint(arm.position);
            if (Vector3.Distance(pivot, LaunchTowerV03Builder.ArmPivot) > .001f)
                throw new InvalidOperationException("V03 arm pivot landed at " + pivot.ToString("F4"));
            report.AppendLine("V03 instantiated under " + PathOf(site) + "; arm under " + PathOf(armControl) + " at pivot " + pivot.ToString("F3"));

            // Match the walking colliders' layer to the existing arm floor so player/interaction masks agree.
            Collider existingFloor = armControl.GetComponentsInChildren<Collider>(true).FirstOrDefault(c => c.name == "COL_ArmFloor");
            if (existingFloor)
                foreach (Collider c in arm.GetComponentsInChildren<Collider>(true)) c.gameObject.layer = existingFloor.gameObject.layer;
            report.AppendLine("walk colliders: " + arm.GetComponentsInChildren<Collider>(true).Length + " on layer "
                + LayerMask.LayerToName(existingFloor ? existingFloor.gameObject.layer : 0));

            // Hide what V03 replaces. Deactivate V02 (kept for its own validator), disable blockout renderers only.
            foreach (Transform t in Under(site, V02InstanceName).Concat(Under(armControl, V02ArmDetailName)))
            {
                Undo.RecordObject(t.gameObject, "Install Launch Tower V03");
                t.gameObject.SetActive(false);
                report.AppendLine("deactivated " + PathOf(t));
            }
            foreach (string name in BlockoutVisuals)
                foreach (Transform part in Under(site, name))
                    foreach (Renderer r in part.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && !IsV03(r.transform)))
                    {
                        Undo.RecordObject(r, "Install Launch Tower V03");
                        r.enabled = false;
                        report.AppendLine("renderer off " + PathOf(r.transform));
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
            Transform site = Child(prologue, SiteName);
            foreach (Transform t in Under(site, InstanceName).Concat(Under(site, ArmInstanceName)).ToList())
            {
                report.AppendLine("removed " + PathOf(t));
                Undo.DestroyObjectImmediate(t.gameObject);
            }
            foreach (Transform t in Under(site, V02InstanceName).Concat(Under(site, V02ArmDetailName)))
            {
                Undo.RecordObject(t.gameObject, "Uninstall Launch Tower V03");
                t.gameObject.SetActive(true);
                report.AppendLine("reactivated " + PathOf(t));
            }
            foreach (string name in BlockoutVisuals)
                foreach (Transform part in Under(site, name))
                    foreach (Renderer r in part.GetComponentsInChildren<Renderer>(true).Where(r => !r.enabled))
                    {
                        Undo.RecordObject(r, "Uninstall Launch Tower V03");
                        r.enabled = true;
                        report.AppendLine("renderer on " + PathOf(r.transform));
                    }
            EditorSceneManager.MarkSceneDirty(scene);
            return report.ToString();
        }

        private static bool IsV03(Transform t)
        {
            for (; t; t = t.parent) if (t.name == ArmInstanceName || t.name == InstanceName) return true;
            return false;
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
