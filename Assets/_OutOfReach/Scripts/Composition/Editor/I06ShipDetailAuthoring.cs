using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OutOfReach.Composition.Editor
{
    /// <summary>Mounts delivered static set dressing at the actual ship origin, without gameplay components.</summary>
    public static class I06ShipDetailAuthoring
    {
        private const string ModelFolder = "Assets/_OutOfReach/Visuals/Models/Aurora7/DetailPass02/Meshes/";
        private const string MountName = "Aurora-7 engineering, habitat and cockpit detail";
        private static readonly string[] Rooms = { "Engineering", "Habitat", "Cockpit" };

        public static void ApplyToSavedBootstrap()
        {
            if (!System.IO.Path.GetFullPath(Application.dataPath).Replace('\\', '/').Contains("/.local/"))
                throw new InvalidOperationException("Apply in the isolated integration checkout.");
            var scene = EditorSceneManager.OpenScene(I01BootstrapBuilder.ScenePath, OpenSceneMode.Single);
            var ship = scene.GetRootGameObjects().Single(o => o.name == "AURORA-7 interior");
            AttachToShip(ship.transform);
            I01BootstrapBuilder.ValidateScene(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Bootstrap save failed.");
        }

        public static void AttachToShip(Transform ship)
        {
            GameObject[] models = Rooms.Select(room =>
            {
                string path = ModelFolder + "DP02_" + room + ".fbx";
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!model) throw new InvalidOperationException("Missing delivered detail mesh " + path);
                ValidateModel(model);
                return model;
            }).ToArray();

            Transform mount = ship.Find(MountName);
            if (!mount)
            {
                var group = new GameObject(MountName);
                mount = group.transform;
                mount.SetParent(ship, false);
            }
            foreach (GameObject model in models)
            {
                Transform child = mount.Find(model.name);
                if (!child)
                    child = ((GameObject)PrefabUtility.InstantiatePrefab(model, mount)).transform;
                if (PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject) != model)
                    throw new InvalidOperationException("Unexpected asset already at " + child.name);
                child.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                child.localScale = Vector3.one;
                ValidateModel(child.gameObject);
            }
            Debug.Log("I06_SHIP_DETAIL_APPLIED exact imported models, preserved materials, no gameplay components");
        }

        private static void ValidateModel(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length == 0 || renderers.Any(renderer => renderer.sharedMaterials.Length == 0
                || renderer.sharedMaterials.Any(material => !material || material.shader.name == "Hidden/InternalErrorShader")))
                throw new InvalidOperationException("Missing intended detail material on " + model.name);
            if (model.GetComponentsInChildren<Collider>(true).Length != 0
                || model.GetComponentsInChildren<Camera>(true).Length != 0
                || model.GetComponentsInChildren<Light>(true).Length != 0
                || model.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                throw new InvalidOperationException("A static detail model contains active scene components.");
        }
    }
}

