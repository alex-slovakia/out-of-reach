#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OutOfReach.Presentation.Cargo.Editor
{
    /// <summary>Additive scene instances only. Never changes the delivered cargo or glove prefab/FBX.</summary>
    public static class PreflightCargoHandsAuthoring
    {
        public const string HandsPrefabPath = "Assets/_OutOfReach/Visuals/Models/Aurora7/Props/AstronautHands/AstronautHands.prefab";
        public const string RigPath = "Assets/_OutOfReach/Visuals/Models/Aurora7/Props/AstronautHands/AstronautHands_rig.json";
        private const string HandleRootName = "Additive cargo side grips";
        public const float GripRadiusMetres = .017f;
        public const float HandleCentreX = .278f;
        public const float HandleCentreY = .175f;

        /// <param name="carriedRoot">Existing camera-child Supported cargo view - no physics, at (.24,-.48,.8).</param>
        /// <param name="caseInstance">The actual PreflightCargo_Visual child at zero position, yaw180, unit scale.</param>
        public static PreflightCargoHandsPresenter Create(Transform carriedRoot, Transform caseInstance)
        {
            if (!carriedRoot || !caseInstance || !carriedRoot.parent || caseInstance.parent != carriedRoot
                || EditorUtility.IsPersistent(carriedRoot) || EditorUtility.IsPersistent(caseInstance))
                throw new ArgumentException("Supply the existing carried scene assembly and its direct case instance.");
            if (caseInstance.localPosition.sqrMagnitude > 1e-8f
                || Quaternion.Angle(caseInstance.localRotation, Quaternion.Euler(0, 180, 0)) > .01f
                || (caseInstance.lossyScale - Vector3.one).sqrMagnitude > 1e-8f
                || Quaternion.Angle(carriedRoot.localRotation, Quaternion.identity) > .01f)
                throw new ArgumentException("Expected the reviewed full-size carried case at local zero/yaw180 and an unrotated carried root.");
            Transform camera = carriedRoot.parent;
            if (camera.Find("Preflight cargo settling") || camera.Find("Preflight work-glove presenter"))
                throw new InvalidOperationException("Cargo work gloves already exist; do not create a second writer.");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HandsPrefabPath);
            TextAsset rig = AssetDatabase.LoadAssetAtPath<TextAsset>(RigPath);
            if (!prefab || !rig) throw new InvalidOperationException("The delivered original glove prefab and rig must already be imported.");
            Transform handleRoot = AddSideHandles(caseInstance);
            Transform settling = Child("Preflight cargo settling", camera);
            Undo.SetTransformParent(carriedRoot, settling, "Wrap cargo visual in settling root");
            // The new parent is identity, so the existing camera-local position and case scale are preserved.
            GameObject gloveObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab, carriedRoot);
            gloveObject.name = "Preflight work gloves - separate instance";
            gloveObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            gloveObject.transform.localScale = Vector3.one;
            const string workGlovePath = CargoStageAuthoring.PropRoot + "/Materials/MAT_Cargo_WorkGloves.mat";
            Material workGloves = AssetDatabase.LoadAssetAtPath<Material>(workGlovePath);
            if (!workGloves)
            {
                Renderer sample = gloveObject.GetComponentInChildren<Renderer>(true);
                workGloves = new Material(sample.sharedMaterial) { name = "Cargo work gloves - graphite" };
                workGloves.SetColor("_BaseColor", new Color(.42f, .43f, .42f));
                AssetDatabase.CreateAsset(workGloves, workGlovePath);
            }
            foreach (Renderer part in gloveObject.GetComponentsInChildren<Renderer>(true))
                part.sharedMaterial = workGloves;
            foreach (Transform node in gloveObject.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 2;
            // Case yaw180 means its negative X side is the player's right.
            Transform right = Child("Right hand grip frame", handleRoot);
            right.position = caseInstance.TransformPoint(new Vector3(-HandleCentreX, HandleCentreY, 0));
            right.rotation = Quaternion.LookRotation(carriedRoot.up, carriedRoot.right);
            Transform left = Child("Left hand grip frame", handleRoot);
            left.position = caseInstance.TransformPoint(new Vector3(HandleCentreX, HandleCentreY, 0));
            left.rotation = Quaternion.LookRotation(carriedRoot.up, -carriedRoot.right);
            Transform host = Child("Preflight work-glove presenter", camera);
            var presenter = host.gameObject.AddComponent<PreflightCargoHandsPresenter>();
            presenter.ConfigureScene(gloveObject.transform, settling, left, right, rig);
            EditorUtility.SetDirty(presenter);
            return presenter;
        }

        /// <summary>Optional: call for every supply/aboard visual too, so pickup does not change the case's geometry.</summary>
        public static Transform AddSideHandles(Transform caseInstance)
        {
            if (!caseInstance || EditorUtility.IsPersistent(caseInstance)) throw new ArgumentException("Use a scene instance only.");
            Transform existing = caseInstance.Find(HandleRootName);
            if (existing) return existing;
            Material graphite = AssetDatabase.LoadAssetAtPath<Material>(CargoStageAuthoring.PropRoot + "/Materials/MAT_Cargo_Graphite.mat");
            Material metal = AssetDatabase.LoadAssetAtPath<Material>(CargoStageAuthoring.PropRoot + "/Materials/MAT_Cargo_Aluminium.mat");
            if (!graphite || !metal) throw new InvalidOperationException("Use the existing mapped cargo materials.");
            Transform root = Child(HandleRootName, caseInstance);
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * HandleCentreX;
                Cylinder(root, side < 0 ? "Port grip - 34 mm" : "Starboard grip - 34 mm",
                    new Vector3(x, HandleCentreY, -.065f), new Vector3(x, HandleCentreY, .065f), GripRadiusMetres, graphite);
                foreach (float z in new[] { -.065f, .065f })
                {
                    Cylinder(root, "Handle standoff", new Vector3(side * .222f, HandleCentreY, z),
                        new Vector3(x, HandleCentreY, z), .009f, metal);
                    GameObject plate = Primitive(root, PrimitiveType.Cube, "Side grip mounting plate", metal);
                    plate.transform.localPosition = new Vector3(side * .225f, HandleCentreY, z);
                    plate.transform.localScale = new Vector3(.012f, .05f, .032f);
                }
            }
            return root;
        }

        private static Transform Child(string name, Transform parent)
        {
            var node = new GameObject(name).transform;
            node.SetParent(parent, false);
            node.gameObject.layer = 2; // visual only; never intercept interaction rays
            return node;
        }

        private static GameObject Primitive(Transform parent, PrimitiveType type, string name, Material material)
        {
            GameObject node = GameObject.CreatePrimitive(type);
            node.name = name;
            node.transform.SetParent(parent, false);
            node.layer = 2;
            Object.DestroyImmediate(node.GetComponent<Collider>());
            node.GetComponent<MeshRenderer>().sharedMaterial = material;
            return node;
        }

        private static void Cylinder(Transform parent, string name, Vector3 start, Vector3 end, float radius, Material material)
        {
            GameObject node = Primitive(parent, PrimitiveType.Cylinder, name, material);
            node.transform.localPosition = (start + end) * .5f;
            node.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (end - start).normalized);
            node.transform.localScale = new Vector3(radius * 2f, Vector3.Distance(start, end) * .5f, radius * 2f);
        }
    }
}
#endif
