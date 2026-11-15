using System;
using System.Collections.Generic;
using System.Linq;
using OutOfReach.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace OutOfReach.Presentation.Meal.Editor
{
    /// <summary>
    /// MEAL-PRES-01 scene installer for the integration assembler (claude/integration). It adds one MealPresenter next to
    /// the existing habitat anchors of the open scene, points it at the existing survival-ration target, the ration drawer
    /// and water dispenser surfaces, the imported meal pouch and the measured clasp, and assigns it to the composition's
    /// <c>meal</c> field when the composition patch (Integration~/composition-meal.patch) is applied. Idempotent: running it
    /// again updates the same object. It never edits Core, the habitat, the targets or any other component.
    /// </summary>
    public static class MealPresentationInstaller
    {
        public const string PresenterName = "Meal presentation (MEAL-PRES-01)";
        public const string ScenePath = "Assets/_OutOfReach/Scenes/Bootstrap.unity";
        public const string PouchPath = "Assets/_OutOfReach/Visuals/Props/GalleyConsumablesV01/GC_MealPouch.fbx";
        public const string ClaspPath = "Assets/_OutOfReach/Visuals/Props/GalleyConsumablesV01/WORK_Clasp_Meal_R.json";

        [MenuItem("OUT OF REACH/Integration/Install meal presentation (MEAL-PRES-01)")]
        public static void InstallInActiveScene()
        {
            MealPresenter presenter = Install(SceneManager.GetActiveScene(), true, out string report);
            Debug.Log(report, presenter);
        }

        /// <summary>Batch entry: -executeMethod OutOfReach.Presentation.Meal.Editor.MealPresentationInstaller.InstallBootstrapBatch</summary>
        public static void InstallBootstrapBatch()
        {
            try
            {
                Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Install(scene, true, out string report);
                Debug.Log("[MEAL-PRES-01] " + report);
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogError("[MEAL-PRES-01] install failed: " + error);
                EditorApplication.Exit(1);
            }
        }

        public static MealPresenter Install(Scene scene, bool save, out string report)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before installing the meal.");
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Open the integrated Bootstrap first.");
            List<Transform> all = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToList();
            InteractableTarget[] rations = all.Select(t => t.GetComponent<InteractableTarget>())
                .Where(t => t && t.InteractionId == MealPresenter.RationInteractionId).ToArray();
            if (rations.Length != 1) throw new InvalidOperationException("Expected one survival-ration target, found " + rations.Length + ".");
            Transform drawer = Single(all, "RationInteractionSurface");
            Transform water = Single(all, "WaterInteractionSurface");
            if (drawer.parent != water.parent || !drawer.parent || !drawer.parent.parent)
                throw new InvalidOperationException("The ration drawer and water surfaces must share the habitat anchor root.");
            GameObject pouch = AssetDatabase.LoadAssetAtPath<GameObject>(PouchPath);
            TextAsset clasp = AssetDatabase.LoadAssetAtPath<TextAsset>(ClaspPath);
            if (!pouch) throw new InvalidOperationException("Missing imported meal pouch " + PouchPath);
            if (!clasp || MealClaspData.Parse(clasp.text) == null) throw new InvalidOperationException("Missing or invalid clasp " + ClaspPath);
            var renderers = pouch.GetComponentsInChildren<Renderer>(true);
            if (!renderers.Any(r => r.name == "GC_MealPouch_Sealed") || !renderers.Any(r => r.name == "GC_MealPouch_Opened"))
                throw new InvalidOperationException("The meal pouch must contain GC_MealPouch_Sealed and GC_MealPouch_Opened.");

            Transform habitatRoot = drawer.parent.parent;
            Transform existing = habitatRoot.Find(PresenterName);
            GameObject host = existing ? existing.gameObject : new GameObject(PresenterName);
            if (!existing) { Undo.RegisterCreatedObjectUndo(host, "Install meal presentation"); host.transform.SetParent(habitatRoot, false); }
            host.transform.localPosition = Vector3.zero; host.transform.localRotation = Quaternion.identity; host.transform.localScale = Vector3.one;
            MealPresenter presenter = host.GetComponent<MealPresenter>();
            if (!presenter) presenter = Undo.AddComponent<MealPresenter>(host);
            var so = new SerializedObject(presenter);
            Set(so, "rationTarget", rations[0]);
            Set(so, "drawerFrame", drawer);
            Set(so, "waterFrame", water);
            Set(so, "pouchModel", pouch);
            Set(so, "claspData", clasp);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(presenter);

            string composition = "composition patch not applied: set I01MissionComposition.meal after applying Integration~/composition-meal.patch";
            MonoBehaviour root = all.SelectMany(t => t.GetComponents<MonoBehaviour>())
                .FirstOrDefault(b => b && b.GetType().Name == "I01MissionComposition");
            if (root)
            {
                var rootSo = new SerializedObject(root);
                SerializedProperty field = rootSo.FindProperty("meal");
                if (field != null)
                {
                    field.objectReferenceValue = presenter;
                    rootSo.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(root);
                    composition = "assigned to " + root.name + ".meal";
                }
            }
            else composition = "no I01MissionComposition in the scene";

            EditorSceneManager.MarkSceneDirty(scene);
            if (save && !EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save " + scene.path);
            report = string.Format("installed '{0}' under '{1}': target {2}, drawer {3}, water {4}, pouch {5}, clasp {6}; {7}; saved={8}",
                PresenterName, habitatRoot.name, rations[0].name, drawer.name, water.name, pouch.name, clasp.name, composition, save);
            return presenter;
        }

        private static Transform Single(List<Transform> all, string name)
        {
            Transform[] found = all.Where(t => t.name == name).ToArray();
            if (found.Length != 1) throw new InvalidOperationException("Expected one " + name + ", found " + found.Length + ".");
            return found[0];
        }

        private static void Set(SerializedObject so, string field, Object value)
        {
            SerializedProperty property = so.FindProperty(field) ?? throw new InvalidOperationException("Missing MealPresenter field " + field);
            property.objectReferenceValue = value;
        }
    }
}
