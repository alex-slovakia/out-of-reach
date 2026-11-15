using System;
using System.IO;
using System.Linq;
using OutOfReach.Interaction;
using OutOfReach.Presentation.Cargo;
using OutOfReach.Presentation.Cargo.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OutOfReach.Composition.Editor
{
    public static class I06CargoPlacardAuthoring
    {
        private const string SupportsName = "Aligned supply cells V02";

        public static void Apply(CargoVisualPresenter presenter)
        {
            if (Application.isPlaying || !presenter || EditorUtility.IsPersistent(presenter))
                throw new InvalidOperationException("Apply only to an authored scene cargo presenter in Edit mode.");
            var data = new SerializedObject(presenter);
            var slots = data.FindProperty("slots");
            var supply = ((GameObject)data.FindProperty("temporarySupplyRoot").objectReferenceValue).transform;
            Transform dock = supply.parent;
            if (slots.arraySize != 7) throw new InvalidOperationException("Expected seven canonical cargo cells.");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(CargoStageAuthoring.PropRoot + "/Materials/MAT_Cargo_Graphite.mat");
            if (!material) throw new InvalidOperationException("Missing existing cargo material.");
            foreach (Transform child in supply.Cast<Transform>().ToArray())
                if (child.name == SupportsName || child.name.StartsWith("Upper supply cell ", StringComparison.Ordinal)
                    || child.name.StartsWith("Upper cell support ", StringComparison.Ordinal)) Object.DestroyImmediate(child.gameObject);
            var frame = new GameObject(SupportsName).transform;
            frame.SetParent(supply, false);
            var targets = supply.GetComponentsInChildren<InteractableTarget>(true);
            for (int i = 0; i < slots.arraySize; i++)
            {
                SerializedProperty slot = slots.GetArrayElementAtIndex(i);
                string id = slot.FindPropertyRelative("EquipmentId").stringValue;
                var box = (GameObject)slot.FindPropertyRelative("SupplyCase").objectReferenceValue;
                var label = (TextMesh)slot.FindPropertyRelative("SupplyLabel").objectReferenceValue;
                Transform target = targets.Single(t => t.InteractionId == "cargo-supply:" + id).transform;
                Vector3 marker = dock.InverseTransformPoint(target.position);
                Vector3 floor = marker + new Vector3(0, -.175f, .38f);
                box.transform.SetPositionAndRotation(dock.TransformPoint(floor), dock.rotation * Quaternion.Euler(0, 180, 0));
                label.transform.SetPositionAndRotation(dock.TransformPoint(marker + new Vector3(0, 0, -.067f)), dock.rotation);
                Box(frame, dock, id + " shelf", floor + new Vector3(0, -.012f, 0), new Vector3(.61f, .024f, .42f), material);
                foreach (float side in new[] { -.285f, .285f })
                {
                    Box(frame, dock, id + " label bracket", marker + new Vector3(side, 0, .13f), new Vector3(.012f, .014f, .40f), material);
                    Box(frame, dock, id + " shelf support", floor + new Vector3(side, -.20f, .18f), new Vector3(.016f, .40f, .016f), material);
                }
                Vector3 delta = dock.InverseTransformPoint(box.transform.position) - marker;
                if (Mathf.Abs(delta.x) > .001f || Mathf.Abs(delta.y + .175f) > .001f)
                    throw new InvalidOperationException("Cargo cell association mismatch: " + id);
            }
            Debug.Log("CARGO_PLACARD_V02: seven supply cases aligned behind their own unchanged interaction target and persistent label; full-size meshes and colours preserved.");
        }

        public static void ApplyToSavedBootstrap()
        {
            try
            {
                if (!Application.isBatchMode || !Application.dataPath.Replace('\\', '/').Contains("/.local/"))
                    throw new InvalidOperationException("Run in the isolated batch editor only.");
                var scene = EditorSceneManager.OpenScene(I01BootstrapBuilder.ScenePath, OpenSceneMode.Single);
                var presenter = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CargoVisualPresenter>(true)).Single();
                Apply(presenter);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Bootstrap save failed.");
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        private static void Box(Transform parent, Transform space, string name, Vector3 position, Vector3 size, Material material)
        {
            var value = GameObject.CreatePrimitive(PrimitiveType.Cube);
            value.name = name;
            value.transform.SetParent(parent, false);
            value.transform.SetPositionAndRotation(space.TransformPoint(position), space.rotation);
            value.transform.localScale = size;
            value.layer = 2;
            Object.DestroyImmediate(value.GetComponent<Collider>());
            value.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
