using System;
using System.Collections.Generic;
using System.Linq;
using OutOfReach.Presentation;
using OutOfReach.Presentation.Lighting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutOfReach.Composition.Editor
{
    /// <summary>
    /// R28 installer (integrator-run, not compiled while it sits in Integration~). Binds the red and amber
    /// status-LED material slots of the delivered AURORA-7 interior to the existing ShipLightingPresenter so
    /// they breathe while Power is damaged. Idempotent; refuses to run over unsaved scenes. Move this file to
    /// Scripts/Composition/Editor/ to use it, then run:
    /// Unity -executeMethod OutOfReach.Composition.Editor.R28ShipIndicatorInstaller.Apply
    /// </summary>
    public static class R28ShipIndicatorInstaller
    {
        private const string Materials = "Assets/_OutOfReach/Visuals/Models/Aurora7/Materials/";
        private static readonly string[] IndicatorMaterials = { "A7_LedRed.mat", "A7_LedAmber.mat" };

        public static void Apply()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save the open scenes before applying R28.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != I01BootstrapBuilder.ScenePath || SceneManager.sceneCount != 1)
                scene = EditorSceneManager.OpenScene(I01BootstrapBuilder.ScenePath, OpenSceneMode.Single);

            GameObject[] roots = scene.GetRootGameObjects();
            ShipLightingPresenter presenter = roots
                .SelectMany(root => root.GetComponentsInChildren<ShipLightingPresenter>(true)).Single();
            ShipVisualAnchors ship = roots
                .SelectMany(root => root.GetComponentsInChildren<ShipVisualAnchors>(true)).Single();

            var targets = new HashSet<Material>();
            foreach (string name in IndicatorMaterials)
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(Materials + name);
                if (!material) throw new InvalidOperationException("Missing indicator material " + name);
                targets.Add(material);
            }

            var slots = new List<(Renderer renderer, int index)>();
            foreach (Renderer renderer in ship.transform.GetComponentsInChildren<Renderer>(true))
            {
                Material[] shared = renderer.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                    if (shared[i] && targets.Contains(shared[i])) slots.Add((renderer, i));
            }
            if (slots.Count == 0) throw new InvalidOperationException("No LED material slots found under the ship visuals.");

            var serialized = new SerializedObject(presenter);
            SerializedProperty list = serialized.FindProperty("indicatorSlots");
            list.arraySize = slots.Count;
            for (int i = 0; i < slots.Count; i++)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("renderer").objectReferenceValue = slots[i].renderer;
                element.FindPropertyRelative("materialIndex").intValue = slots[i].index;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new System.IO.IOException("Could not save Bootstrap.");
            Debug.Log("R28_INDICATOR_INSTALLER SAVED slots=" + slots.Count);
        }
    }
}
