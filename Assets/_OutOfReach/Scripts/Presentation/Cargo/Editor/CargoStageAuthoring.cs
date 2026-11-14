using System;
using System.Collections.Generic;
using OutOfReach.Interaction;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OutOfReach.Presentation.Cargo.Editor
{
    /// <summary>Explicit Bootstrap authoring only; no gameplay discovery or runtime asset loading.</summary>
    public static class CargoStageAuthoring
    {
        public const string PropRoot = "Assets/_OutOfReach/Visuals/Props/PreflightCargo";
        public static readonly string[] EquipmentIds = { "oxygen-tank", "water-tank", "food-locker",
            "battery-module", "solar-wing", "spare-parts-kit", "spectrometer" };
        private static readonly string[] Names = { "OXYGEN TANK", "WATER TANK", "FOOD LOCKER",
            "BATTERY MODULE", "SOLAR WING", "SPARE PARTS KIT", "SPECTROMETER" };

        public sealed class Result
        {
            public CargoVisualPresenter Presenter;
            public PreflightCargoHandsPresenter Hands;
            public Transform Spawn;
            public InteractableTarget[] Targets;
        }

        public static Result Create(Transform parent, Transform gameplayCamera, Font labelFont)
        {
            if (!gameplayCamera || !labelFont) throw new ArgumentException("Bind the gameplay camera and project font.");
            GameObject casePrefab = PrepareCasePrefab();
            Transform root = Child("C05 physical cargo", parent);
            Transform supply = Child("Temporary preflight supply", root);
            Transform aboard = Child("Aboard manifest cases", root);
            var slots = new CargoVisualPresenter.Slot[EquipmentIds.Length];
            var targets = new List<InteractableTarget>();
            var colliders = new List<Collider>();
            for (int i = 0; i < EquipmentIds.Length; i++)
            {
                Vector3 source = SupplyPosition(i), destination = BayPosition(i);
                var slot = new CargoVisualPresenter.Slot { EquipmentId = EquipmentIds[i], DisplayName = Names[i] };
                slot.SupplyCase = CaseAt(casePrefab, supply, source, 90f);
                slot.AboardCase = CaseAt(casePrefab, aboard, destination, -90f);
                slot.SupplyLabel = Label(supply, Names[i] + "\nSUPPLY", source + new Vector3(.09f, .22f, 0), -90f, labelFont);
                slot.AboardLabel = Label(aboard, Names[i] + "\nABOARD", destination + new Vector3(-.09f, .22f, 0), 90f, labelFont);
                targets.Add(Target(supply, "cargo-supply:" + EquipmentIds[i], source, colliders));
                InteractableTarget bay = Target(aboard, "cargo-bay:" + EquipmentIds[i], destination, colliders);
                targets.Add(bay);
                slot.DestinationAnchor = bay.transform;
                slots[i] = slot;
            }
            Transform held = Child("Supported cargo view - no physics", gameplayCamera);
            held.localPosition = new Vector3(0f, -.44f, .90f);
            GameObject carriedCase = (GameObject)PrefabUtility.InstantiatePrefab(casePrefab, held);
            carriedCase.transform.localPosition = Vector3.zero;
            carriedCase.transform.localRotation = Quaternion.Euler(0, 180, 0);
            // Visible tray and rails support the case; this is a constrained handling view, not a free floating pickup.
            Material graphite = AssetDatabase.LoadAssetAtPath<Material>(PropRoot + "/Materials/MAT_Cargo_Graphite.mat");
            Box(held, "Handling cradle tray", new Vector3(0, -.025f, 0), new Vector3(.51f, .04f, .41f), graphite);
            Box(held, "Cradle rail L", new Vector3(-.25f, .06f, 0), new Vector3(.025f, .12f, .41f), graphite);
            Box(held, "Cradle rail R", new Vector3(.25f, .06f, 0), new Vector3(.025f, .12f, .41f), graphite);
            TextMesh heldLabel = Label(held, "SUPPORTED HANDLING / SIMPLIFIED", Vector3.zero, 0, labelFont);
            heldLabel.transform.localPosition = new Vector3(0, -.08f, -.23f);
            heldLabel.transform.localRotation = Quaternion.identity;
            heldLabel.characterSize = .0055f;
            PreflightCargoHandsPresenter cargoHands = PreflightCargoHandsAuthoring.Create(held, carriedCase.transform);
            var handPose = new SerializedObject(cargoHands);
            handPose.FindProperty("carryGripRollDegrees").floatValue = 42f;
            handPose.FindProperty("carryWristYawCorrectionDegrees").floatValue = 28f;
            handPose.ApplyModifiedPropertiesWithoutUndo();
            // All visuals are nonblocking and ignore interaction rays; only explicit station triggers can be hit.
            foreach (Transform item in held.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = 2;
            held.gameObject.SetActive(false);
            Transform spawn = Child("PreflightSpawn", root);
            spawn.SetPositionAndRotation(new Vector3(0, .05f, 13.5f), Quaternion.Euler(0, 180, 0));
            CargoVisualPresenter presenter = root.gameObject.AddComponent<CargoVisualPresenter>();
            presenter.ConfigureScene(supply.gameObject, held.gameObject, heldLabel, slots, colliders.ToArray());
            return new Result { Presenter = presenter, Hands = cargoHands, Spawn = spawn, Targets = targets.ToArray() };
        }

        public static Vector3 SupplyPosition(int index) => new Vector3(-1.44f, .5f + .5f * (index % 4), 11.55f + .6f * (index / 4));
        public static Vector3 BayPosition(int index) => new Vector3(1.44f, .5f + .5f * (index % 4), 15.75f + .6f * (index / 4));

        private static InteractableTarget Target(Transform parent, string id, Vector3 position, List<Collider> colliders)
        {
            Transform child = Child(id, parent);
            child.position = position;
            BoxCollider box = child.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(.12f, .36f, .42f);
            var target = child.gameObject.AddComponent<InteractableTarget>();
            target.InteractionId = id;
            colliders.Add(box);
            return target;
        }

        private static GameObject CaseAt(GameObject prefab, Transform parent, Vector3 target, float yaw)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            // Full-size case; front faces the aisle. Its floor is .145m below the interaction centre.
            instance.transform.SetPositionAndRotation(target + new Vector3(target.x < 0 ? -.16f : .16f, -.145f, 0), Quaternion.Euler(0, yaw, 0));
            PreflightCargoHandsAuthoring.AddSideHandles(instance.transform);
            return instance;
        }

        private static TextMesh Label(Transform parent, string text, Vector3 position, float yaw, Font font)
        {
            Transform child = Child("Manifest label", parent);
            child.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            TextMesh label = child.gameObject.AddComponent<TextMesh>();
            label.font = font;
            label.fontSize = 48;
            label.characterSize = .006f;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = new Color(.82f, .91f, .93f);
            label.text = text;
            Material depthLabel = AssetDatabase.LoadAssetAtPath<Material>("Assets/_OutOfReach/Visuals/Ship/Shaders/ShipDepthLabel.mat");
            if (!depthLabel) throw new InvalidOperationException("Cargo labels require the existing depth-tested label material.");
            child.gameObject.AddComponent<ShipLabelAtlasBinder>().Configure(label,
                child.GetComponent<MeshRenderer>(), font, depthLabel);
            Material plate = AssetDatabase.LoadAssetAtPath<Material>(PropRoot + "/Materials/MAT_Cargo_Graphite.mat");
            Box(child, "Printed manifest plate", new Vector3(0, 0, .008f), new Vector3(.46f, .13f, .012f), plate);
            return label;
        }

        private static Transform Child(string name, Transform parent)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static void Box(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = size;
            Object.DestroyImmediate(box.GetComponent<Collider>());
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        public static GameObject PrepareCasePrefab()
        {
            string fbxPath = PropRoot + "/Meshes/PreflightCargo.fbx";
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (!asset) throw new InvalidOperationException("Import the reviewed cargo FBX first: " + fbxPath);
            if (asset.GetComponentsInChildren<Collider>(true).Length != 0 || asset.GetComponentsInChildren<Rigidbody>(true).Length != 0)
                throw new InvalidOperationException("Cargo visual FBX must have no collider/Rigidbody. Disable Generate Colliders.");
            if (!AssetDatabase.IsValidFolder(PropRoot + "/Materials")) AssetDatabase.CreateFolder(PropRoot, "Materials");
            if (!AssetDatabase.IsValidFolder(PropRoot + "/Prefabs")) AssetDatabase.CreateFolder(PropRoot, "Prefabs");
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("Existing URP Lit shader unavailable.");
            string[] names = { "MAT_Cargo_OffWhite", "MAT_Cargo_Graphite", "MAT_Cargo_Aluminium", "MAT_Cargo_Amber" };
            string[] colors = { "#444944", "#262B2F", "#A6AEB5", "#CF8E23" };
            float[] metallic = { .05f, .05f, .82f, .05f }, smoothness = { .52f, .42f, .72f, .54f };
            var materials = new Dictionary<string, Material>(StringComparer.Ordinal);
            for (int i = 0; i < names.Length; i++)
            {
                string path = PropRoot + "/Materials/" + names[i] + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
                ColorUtility.TryParseHtmlString(colors[i], out Color color);
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Metallic", metallic[i]);
                material.SetFloat("_Smoothness", smoothness[i]);
                EditorUtility.SetDirty(material);
                materials.Add(names[i], material);
            }
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            try
            {
                instance.name = "PreflightCargo_Visual";
                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] assigned = renderer.sharedMaterials;
                    for (int i = 0; i < assigned.Length; i++)
                    {
                        string name = assigned[i] ? assigned[i].name : string.Empty;
                        if (!materials.TryGetValue(name, out Material mapped))
                            throw new InvalidOperationException("Unmapped cargo material: " + name + ". Keep FBX material names during import.");
                        assigned[i] = mapped;
                    }
                    renderer.sharedMaterials = assigned;
                }
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, PropRoot + "/Prefabs/PreflightCargo_Visual.prefab");
                AssetDatabase.SaveAssets();
                return prefab;
            }
            finally { Object.DestroyImmediate(instance); }
        }
    }
}
