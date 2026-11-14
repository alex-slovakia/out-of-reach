using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace OutOfReach.Presentation.Editor
{
    /// <summary>V01 authored geometry. Run in an isolated project copy; never changes Bootstrap or build settings.</summary>
    public static class ShipVisualBuilder
    {
        private const string ShipPath = "Assets/_OutOfReach/Visuals/Ship";
        public const string PrefabPath = ShipPath + "/Aurora7Interior.prefab";
        public const string ScenePath = "Assets/_OutOfReach/Scenes/Visual/Aurora7VisualPreview.unity";
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        private static Font labelFont;
        private static Material labelMaterial;

        [MenuItem("OUT OF REACH/Visuals/Build V01 interior and preview")]
        public static void BuildAndValidate()
        {
            if (Application.unityVersion != "6000.3.28f1")
                throw new InvalidOperationException("V01 requires Unity 6000.3.28f1.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureFolder(ShipPath + "/Materials");
            EnsureFolder(ShipPath + "/Modules");
            EnsureFolder("Assets/_OutOfReach/Scenes/Visual");
            ShipArtAssets.Prepare();
            MakePalette();
            labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (!labelFont) throw new InvalidOperationException("Unity's built-in label font was not available.");
            MakeLabelMaterial();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ConfigurePreviewEnvironment();

            var ship = new GameObject("AURORA-7 | Interior visual assembly");
            var cockpit = NewGroup("01_Cockpit", ship.transform, Vector3.zero);
            Room(cockpit, 6f, 6f, "01 / FLIGHT DECK", "AURORA-7", "cyan", false);
            Cockpit(cockpit);
            SaveModule(cockpit, "Cockpit");

            var engineering = NewGroup("02_Engineering", ship.transform, new Vector3(0, 0, 7));
            Room(engineering, 4.8f, 8f, "02 / ENGINEERING", "POWER + LIFE SUPPORT", "amber", true, 1);
            Engineering(engineering);
            SaveModule(engineering, "Engineering");

            var storage = NewGroup("03_Storage", ship.transform, new Vector3(0, 0, 14));
            Room(storage, 4.8f, 6f, "03 / LOGISTICS", "RESERVES + EQUIPMENT", "cyan");
            Storage(storage);
            SaveModule(storage, "Storage");

            var airlock = NewGroup("04_Airlock", ship.transform, new Vector3(0, 0, 19));
            Room(airlock, 4.8f, 4f, "04 / AIRLOCK", "SUIT CHECK REQUIRED", "amber", true, 0);
            Airlock(airlock);
            SaveModule(airlock, "Airlock");

            var anchors = ship.AddComponent<ShipVisualAnchors>();
            var anchorRoot = NewGroup("IntegrationAnchors", ship.transform, Vector3.zero);
            anchors.PlayerSpawn = Anchor(anchorRoot, "PlayerSpawn", new Vector3(0, 0.05f, 0.8f), 180);
            anchors.CockpitConsole = Anchor(anchorRoot, "CockpitConsole", new Vector3(0, 1.1f, -1.72f), 0);
            anchors.PowerRepair = Anchor(anchorRoot, "PowerRepair", new Vector3(-1.65f, 1.15f, 6.1f), 90);
            anchors.LifeSupportRepair = Anchor(anchorRoot, "LifeSupportRepair", new Vector3(1.65f, 1.15f, 8.1f), -90);
            anchors.AirlockInner = Anchor(anchorRoot, "AirlockInner", new Vector3(0, 0, 17f), 0);
            anchors.AirlockOuter = Anchor(anchorRoot, "AirlockOuter", new Vector3(0, 0, 21f), 0);
            anchors.EvaStart = Anchor(anchorRoot, "EvaStart", new Vector3(0, 0, 22f), 0);
            anchors.AntennaRepair = Anchor(anchorRoot, "AntennaRepair", new Vector3(3.2f, 2.4f, 13f), -90);
            anchors.EvaReturn = Anchor(anchorRoot, "EvaReturn", new Vector3(0, 0, 21.7f), 180);
            anchors.FinaleCameraAnchor = Anchor(anchorRoot, "FinaleCameraAnchor", new Vector3(9, 5, 27f), 205);
            anchors.InnerDoorLeft = airlock.Find("InnerDoorVisuals/LeftLeaf");
            anchors.InnerDoorRight = airlock.Find("InnerDoorVisuals/RightLeaf");
            anchors.OuterDoorLeft = airlock.Find("OuterDoorVisuals/LeftLeaf");
            anchors.OuterDoorRight = airlock.Find("OuterDoorVisuals/RightLeaf");
            if (!PrefabUtility.SaveAsPrefabAssetAndConnect(ship, PrefabPath, InteractionMode.AutomatedAction))
                throw new InvalidOperationException("Failed to save assembled visual prefab.");

            var preview = new GameObject("PREVIEW ONLY - exclude from Bootstrap") { tag = "EditorOnly" };
            var camera = new GameObject("PreviewCamera").AddComponent<Camera>();
            camera.transform.SetParent(preview.transform);
            camera.transform.position = new Vector3(0.45f, 1.65f, 1.3f);
            camera.transform.LookAt(new Vector3(0, 1.35f, 11));
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.003f, 0.006f, 0.012f);
            camera.fieldOfView = 68;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 100;
            camera.allowHDR = true;
            camera.allowMSAA = true;
            // No AudioListener: this V01 preview has no audio. Bootstrap owns its sole gameplay listener.
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
            AssetDatabase.SaveAssets();
            ValidateExisting();
            Debug.Log("V01_BUILD_COMPLETE " + ScenePath + " | " + PrefabPath);
        }

        private static void ConfigurePreviewEnvironment()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.18f, 0.20f, 0.23f);
            RenderSettings.ambientIntensity = 1;
            RenderSettings.reflectionIntensity = 0.25f;
            RenderSettings.fog = false;
        }

        private static void Room(Transform parent, float width, float length, string title, string subtitle, string accent, bool entrancePortal = true, int shadowFixtureIndex = -1)
        {
            var structure = NewGroup("Structure", parent, Vector3.zero);
            Box("Pressure floor", structure, new Vector3(0, -0.12f, 0), new Vector3(width, 0.24f, length), "hull", true);
            Box("Pressure ceiling", structure, new Vector3(0, 3.22f, 0), new Vector3(width, 0.2f, length), "ceiling", true);
            for (var side = -1; side <= 1; side += 2)
            {
                Box("Pressure wall", structure, new Vector3(side * (width / 2 + 0.06f), 1.5f, 0), new Vector3(0.12f, 3.2f, length), "hull", true);
                for (float z = -length / 2 + 0.8f; z < length / 2; z += 1.6f)
                {
                    Box("Wall panel", structure, new Vector3(side * (width / 2 - 0.035f), 1.5f, z), new Vector3(0.07f, 2.3f, 1.5f), "panel");
                    Box("Frame rib", structure, new Vector3(side * (width / 2 - 0.12f), 1.45f, z - 0.78f), new Vector3(0.18f, 2.9f, 0.09f), "edge");
                    Box("Ceiling rib", structure, new Vector3(0, 3.06f, z - 0.78f), new Vector3(width, 0.14f, 0.09f), "edge");
                    var bevel = Box("Upper shoulder", structure, new Vector3(side * (width / 2 - 0.22f), 2.9f, z), new Vector3(0.65f, 0.12f, 1.5f), "panel");
                    bevel.localRotation = Quaternion.Euler(0, 0, side * 45);
                }
                Box("Painted walkway edge", parent, new Vector3(side * 0.77f, 0.012f, 0), new Vector3(0.025f, 0.014f, length - 0.15f), "ivory");
                Box("Skirting", structure, new Vector3(side * (width / 2 - 0.06f), 0.17f, 0), new Vector3(0.1f, 0.3f, length), "edge");
                Box("Ceiling utility run", parent, new Vector3(side * 0.9f, 3f, 0), new Vector3(0.2f, 0.1f, length), "steel");
            }
            for (float z = -length / 2 + 0.5f; z < length / 2; z += 1)
            {
                Box("Walkway plate", parent, new Vector3(0, 0.005f, z), new Vector3(1.48f, 0.01f, 0.94f), "deck");
                for (var i = 0; i < 5; i++)
                    Box("Grip inset", parent, new Vector3(-0.52f + i * 0.26f, 0.013f, z), new Vector3(0.09f, 0.01f, 0.7f), "hull");
            }
            int fixtureIndex = 0;
            for (float z = -length / 2 + 1.4f; z < length / 2; z += 3)
            {
                Box("Ceiling fixture", parent, new Vector3(0, 3.03f, z), new Vector3(1.3f, 0.12f, 0.42f), "steel");
                Box("Ceiling diffuser", parent, new Vector3(0, 2.96f, z), new Vector3(1.12f, 0.015f, 0.28f), "warm");
                PracticalLight(parent, new Vector3(0, 2.86f, z), fixtureIndex == shadowFixtureIndex);
                fixtureIndex++;
            }
            if (entrancePortal) Portal(parent, -length / 2, width, title, subtitle, accent);
        }

        private static void Portal(Transform parent, float z, float roomWidth, string title, string subtitle, string accent)
        {
            var frame = NewGroup("Pressure frame " + title, parent, new Vector3(0, 0, z));
            // Opening width 1.8 m, head clearance 2.5 m. No floor lip or animated gate authority.
            float wing = (roomWidth - 1.8f) / 2;
            for (var side = -1; side <= 1; side += 2)
            {
                Box("Bulkhead wing", frame, new Vector3(side * (0.9f + wing / 2), 1.55f, 0), new Vector3(wing, 3.1f, 0.18f), "panel", true);
                Box("Portal jamb", frame, new Vector3(side * 0.95f, 1.25f, 0), new Vector3(0.1f, 2.5f, 0.32f), "steel");
                Box("Guidance marker", frame, new Vector3(side * 1.07f, 1.3f, -0.105f), new Vector3(0.055f, 0.7f, 0.014f), accent);
            }
            Box("Overhead lintel", frame, new Vector3(0, 2.82f, 0), new Vector3(roomWidth, 0.64f, 0.2f), "hull", true);
            Label(frame, title, new Vector3(0, 2.91f, -0.115f), Quaternion.identity, 0.105f, "ivory");
            Label(frame, subtitle, new Vector3(0, 2.66f, -0.115f), Quaternion.identity, 0.046f, accent);
            Label(frame, title, new Vector3(0, 2.91f, 0.115f), Quaternion.Euler(0, 180, 0), 0.105f, "ivory");
        }

        private static void Cockpit(Transform parent)
        {
            // The front window is a collision boundary. It contains a sparse star reference, no nearby Earth.
            Box("Forward bulkhead", parent, new Vector3(0, 1.55f, -3.05f), new Vector3(6, 3.1f, 0.15f), "hull", true);
            Box("Observation aperture", parent, new Vector3(0, 2f, -2.95f), new Vector3(4.5f, 1.55f, 0.04f), "space");
            for (var side = -1; side <= 1; side += 2)
            {
                Box("Window side frame", parent, new Vector3(side * 2.3f, 2, -2.9f), new Vector3(0.14f, 1.75f, 0.12f), "steel");
                Box("Window cross brace", parent, new Vector3(side * 0.8f, 2, -2.88f), new Vector3(0.065f, 1.55f, 0.16f), "steel");
                var chair = NewGroup("Flight seat", parent, new Vector3(side * 1.15f, 0, -0.45f));
                Box("Seat pedestal", chair, new Vector3(0, 0.3f, 0), new Vector3(0.4f, 0.6f, 0.5f), "steel", true);
                Box("Seat", chair, new Vector3(0, 0.6f, 0), new Vector3(0.62f, 0.15f, 0.65f), "padding", true);
                Box("Backrest", chair, new Vector3(0, 1.05f, 0.28f), new Vector3(0.61f, 0.9f, 0.14f), "padding", true);
                Box("Headrest", chair, new Vector3(0, 1.63f, 0.29f), new Vector3(0.38f, 0.28f, 0.16f), "padding");
                Box("Harness", chair, new Vector3(0, 1.05f, 0.2f), new Vector3(0.1f, 0.85f, 0.02f), "amberPaint");
            }
            Box("Window upper edge", parent, new Vector3(0, 2.82f, -2.9f), new Vector3(4.7f, 0.13f, 0.12f), "steel");
            Box("Window lower edge", parent, new Vector3(0, 1.2f, -2.9f), new Vector3(4.7f, 0.13f, 0.12f), "steel");
            var stars = new System.Random(7101);
            for (var i = 0; i < 32; i++)
            {
                float x = (float)stars.NextDouble() * 4.2f - 2.1f;
                float y = 1.3f + (float)stars.NextDouble() * 1.38f;
                float size = i % 5 == 0 ? 0.012f : 0.006f;
                Box("Distant star", parent, new Vector3(x, y, -2.915f), new Vector3(size, size, 0.008f), "star");
            }
            var console = NewGroup("Flight console", parent, new Vector3(0, 0, -1.95f));
            Box("Console plinth", console, new Vector3(0, 0.46f, 0), new Vector3(2.9f, 0.92f, 0.7f), "hull", true);
            Box("Console top", console, new Vector3(0, 0.96f, 0), new Vector3(3.1f, 0.12f, 0.85f), "steel", true);
            for (var i = -1; i <= 1; i++)
            {
                var screen = NewGroup("Instrument " + i, console, new Vector3(i * 0.95f, 1.15f, -0.11f));
                screen.localRotation = Quaternion.Euler(-18, 180, 0);
                Display(screen, 0.82f, 0.44f, i == -1 ? "NAVIGATION" : i == 0 ? "AURORA-7" : "COMMUNICATIONS", "VISUAL REFERENCE / UNBOUND", "cyan");
            }
            Label(parent, "AURORA-7", new Vector3(0, 2.97f, -2.8f), Quaternion.Euler(0, 180, 0), 0.14f, "ivory");
            Label(parent, "DEEP-SPACE RESEARCH VESSEL", new Vector3(0, 1.04f, -2.83f), Quaternion.Euler(0, 180, 0), 0.045f, "cyan");
            PointLight(parent, "Instrument bounce", new Vector3(0, 1.8f, -1.9f), new Color(0.55f, 0.8f, 1), 0.2f, 2.5f);
        }

        private static void Engineering(Transform parent)
        {
            EquipmentRack(parent, new Vector3(-2.02f, 0, -0.9f), -90, "PWR / 01", "POWER DISTRIBUTION", "amber", true);
            EquipmentRack(parent, new Vector3(2.02f, 0, 1.1f), 90, "ECLSS / 02", "LIFE SUPPORT", "cyan");
            for (var side = -1; side <= 1; side += 2)
            {
                for (var i = 0; i < 3; i++)
                {
                    var pipe = Cylinder("Closed service conduit", parent, new Vector3(side * 2.18f, 2.6f + i * 0.12f, 0), new Vector3(0.045f, 3.5f, 0.045f), "steel");
                    pipe.localRotation = Quaternion.Euler(90, 0, 0);
                }
            }
            var tankRack = NewGroup("Oxygen and water hardware", parent, new Vector3(1.99f, 0, -2.15f));
            for (var i = 0; i < 2; i++)
            {
                Cylinder("Reserve tank", tankRack, new Vector3(0, 1.1f, i * 0.58f), new Vector3(0.38f, 0.78f, 0.38f), "steel", true);
                Cylinder("Tank neck", tankRack, new Vector3(0, 1.97f, i * 0.58f), new Vector3(0.11f, 0.1f, 0.11f), "amberPaint");
                Box("Tank restraint", tankRack, new Vector3(-0.2f, 1.05f, i * 0.58f), new Vector3(0.03f, 0.12f, 0.42f), "amberPaint");
            }
            Label(parent, "STORED OXYGEN / kg", new Vector3(1.74f, 2.22f, -1.88f), Quaternion.Euler(0, 90, 0), 0.055f, "ivory");
            var service = NewGroup("Maintenance tools", parent, new Vector3(-2.1f, 0, 2.4f));
            Box("Tool backing", service, new Vector3(0, 1.4f, 0), new Vector3(0.15f, 1.45f, 1.25f), "panel");
            for (var i = 0; i < 4; i++)
            {
                Box("Stored tool", service, new Vector3(0.12f, 1.4f, -0.4f + i * 0.27f), new Vector3(0.06f, 0.48f, 0.08f), "steel");
                Box("Tool grip", service, new Vector3(0.15f, 1.25f, -0.4f + i * 0.27f), new Vector3(0.08f, 0.2f, 0.09f), "amberPaint");
            }
        }

        private static void EquipmentRack(Transform parent, Vector3 position, float yaw, string id, string title, string accent, bool detailedPowerBox = false)
        {
            var rack = NewGroup(title, parent, position);
            rack.localRotation = Quaternion.Euler(0, yaw, 0);
            var body = Box("Cabinet body", rack, new Vector3(0, 1.15f, 0), new Vector3(1.6f, 2.3f, 0.58f), "hull", true);
            if (detailedPowerBox)
            {
                // Keep the exact original collision envelope, but open its visual face for the service assembly.
                body.GetComponent<Renderer>().enabled = false;
                Box("Power rack backing", rack, new Vector3(0, 1.15f, 0.23f), new Vector3(1.6f, 2.3f, 0.12f), "hull");
                for (int side = -1; side <= 1; side += 2)
                    Box("Power rack side", rack, new Vector3(side * 0.75f, 1.15f, 0), new Vector3(0.1f, 2.3f, 0.58f), "hull");
                Box("Power rack lower panel", rack, new Vector3(0, 0.4f, -0.31f), new Vector3(1.45f, 0.7f, 0.05f), "panel");
                Box("Power rack header", rack, new Vector3(0, 1.96f, -0.31f), new Vector3(1.45f, 0.35f, 0.05f), "panel");
            }
            else Box("Access face", rack, new Vector3(0, 1.15f, -0.31f), new Vector3(1.45f, 2.12f, 0.05f), "panel");
            Box("Identification stripe", rack, new Vector3(-0.67f, 1.15f, -0.345f), new Vector3(0.035f, 2.05f, 0.015f), accent);
            Label(rack, id, new Vector3(0, 2.03f, -0.35f), Quaternion.identity, 0.11f, "ink");
            Label(rack, title, new Vector3(0, 1.82f, -0.35f), Quaternion.identity, 0.05f, "ink");
            Box("Recessed service lamp", rack, new Vector3(0, 1.76f, -0.39f), new Vector3(0.75f, 0.025f, 0.06f), "warm");
            var serviceLight = NewGroup("Service task light", rack, new Vector3(0, 1.76f, -0.52f)).gameObject.AddComponent<Light>();
            serviceLight.type = LightType.Spot;
            serviceLight.transform.localRotation = Quaternion.LookRotation(new Vector3(0, -0.8f, 0.32f));
            serviceLight.color = new Color(1, 0.93f, 0.82f);
            serviceLight.intensity = 0.55f;
            serviceLight.range = 1.7f;
            serviceLight.spotAngle = 100;
            serviceLight.innerSpotAngle = 65;
            serviceLight.shadows = LightShadows.None;
            if (detailedPowerBox)
            {
                ShipArtAssets.AddPowerBox(rack);
                return;
            }
            var screen = NewGroup("Unbound diagnostic display", rack, new Vector3(0, 1.45f, -0.36f));
            Display(screen, 1.1f, 0.42f, "DIAGNOSTICS", "CONNECT DOMAIN AT INTEGRATION", accent);
            for (var i = 0; i < 6; i++)
            {
                Box("Vent", rack, new Vector3(0, 0.42f + i * 0.055f, -0.345f), new Vector3(0.95f, 0.02f, 0.015f), "space");
            }
            for (var side = -1; side <= 1; side += 2)
                Box("Service handle", rack, new Vector3(side * 0.55f, 0.98f, -0.39f), new Vector3(0.045f, 0.25f, 0.07f), "steel");
            PointLight(rack, "Display bounce", new Vector3(0, 1.6f, -0.8f), accent == "cyan" ? new Color(0.55f, 0.8f, 1) : new Color(1, 0.8f, 0.55f), 0.12f, 1.8f);
        }

        private static void Storage(Transform parent)
        {
            for (var side = -1; side <= 1; side += 2)
            {
                var rack = NewGroup(side < 0 ? "Supply lockers" : "Equipment lockers", parent, new Vector3(side * 1.95f, 0, 0));
                Box("Locker back", rack, new Vector3(0, 1.3f, 0), new Vector3(0.65f, 2.6f, 4.5f), "hull", true);
                for (var i = 0; i < 3; i++)
                {
                    float z = -1.5f + i * 1.5f;
                    Box("Locker door", rack, new Vector3(-side * 0.34f, 1.3f, z), new Vector3(0.05f, 2.35f, 1.36f), "panel");
                    Box("Locker latch", rack, new Vector3(-side * 0.4f, 1.1f, z - 0.4f), new Vector3(0.07f, 0.25f, 0.06f), "steel");
                    Box("Restraint strap", rack, new Vector3(-side * 0.38f, 0.65f, z), new Vector3(0.02f, 0.1f, 1.3f), "amberPaint");
                    string name = side < 0 ? new[] { "WATER", "RATIONS", "SPARE PARTS" }[i] : new[] { "EVA KIT", "SCIENCE", "SERVICE" }[i];
                    Label(rack, name, new Vector3(-side * 0.39f, 1.9f, z), Quaternion.Euler(0, side > 0 ? 90 : -90, 0), 0.1f, "ink");
                    Label(rack, "STOWED / ACCESS AT CONSOLE", new Vector3(-side * 0.39f, 1.65f, z), Quaternion.Euler(0, side > 0 ? 90 : -90, 0), 0.035f, "ink");
                }
            }
        }

        private static void Airlock(Transform parent)
        {
            var inner = NewGroup("InnerDoorVisuals", parent, new Vector3(0, 0, -2));
            DoorLeaf(inner, "LeftLeaf", -1.4f, "cyan");
            DoorLeaf(inner, "RightLeaf", 1.4f, "cyan");
            var outer = NewGroup("OuterDoorVisuals", parent, new Vector3(0, 0, 2));
            DoorLeaf(outer, "LeftLeaf", -0.45f, "amber");
            DoorLeaf(outer, "RightLeaf", 0.45f, "amber");
            Portal(parent, 2, 4.8f, "EXTERIOR / EVA", "PRESSURE BOUNDARY", "amber");
            Label(parent, "CHECK SUIT + TETHER", new Vector3(0, 2.17f, 1.85f), Quaternion.identity, 0.08f, "ivory");
            EquipmentRack(parent, new Vector3(-2.02f, 0, 0), -90, "AIRLOCK", "PRESSURE CONTROL", "amber");
            var suit = NewGroup("Suit stowage silhouette", parent, new Vector3(1.95f, 0, 0));
            Box("Suit mount", suit, new Vector3(0, 1.25f, 0), new Vector3(0.3f, 2.4f, 1.25f), "hull");
            var helmet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            helmet.name = "Stowed helmet";
            helmet.transform.SetParent(suit, false);
            helmet.transform.localPosition = new Vector3(-0.18f, 1.95f, 0);
            helmet.transform.localScale = Vector3.one * 0.4f;
            helmet.GetComponent<Renderer>().sharedMaterial = Materials["ivory"];
            UnityEngine.Object.DestroyImmediate(helmet.GetComponent<Collider>());
            Box("Packed torso", suit, new Vector3(-0.2f, 1.43f, 0), new Vector3(0.35f, 0.6f, 0.52f), "padding");
            for (var side = -1; side <= 1; side += 2)
                Box("Packed suit leg", suit, new Vector3(-0.2f, 0.8f, side * 0.17f), new Vector3(0.3f, 0.65f, 0.24f), "padding");
            Label(parent, "EVA / EQUIPMENT", new Vector3(1.68f, 2.37f, 0), Quaternion.Euler(0, 90, 0), 0.07f, "ivory");
            for (var i = 0; i < 8; i++)
            {
                var stripe = Box("Threshold caution marking", parent, new Vector3(-0.7f + i * 0.2f, 0.021f, 1.48f), new Vector3(0.1f, 0.015f, 0.22f), "amberPaint");
                stripe.localRotation = Quaternion.Euler(0, 30, 0);
            }
        }

        private static void DoorLeaf(Transform parent, string name, float x, string accent)
        {
            var leaf = NewGroup(name, parent, new Vector3(x, 0, 0));
            // Intentionally no gate collider: the domain owner must add independent authoritative gates in Bootstrap.
            Box("Leaf body", leaf, new Vector3(0, 1.23f, 0), new Vector3(0.88f, 2.46f, 0.12f), "panel");
            Box("Leaf recess", leaf, new Vector3(0, 1.23f, -0.07f), new Vector3(0.68f, 2.1f, 0.03f), "hull");
            Box("Door identification", leaf, new Vector3(0, 1.4f, -0.1f), new Vector3(0.5f, 0.035f, 0.015f), accent);
            Box("Pressure seal", leaf, new Vector3(0.42f, 1.23f, -0.08f), new Vector3(0.025f, 2.4f, 0.02f), "steel");
        }

        private static void Display(Transform parent, float width, float height, string title, string subtitle, string accent)
        {
            Box("Screen bezel", parent, Vector3.zero, new Vector3(width + 0.06f, height + 0.06f, 0.045f), "steel");
            Box("Screen glass", parent, new Vector3(0, 0, -0.028f), new Vector3(width, height, 0.01f), "screen");
            Label(parent, title, new Vector3(0, height * 0.27f, -0.04f), Quaternion.identity, height * 0.13f, accent);
            Label(parent, subtitle, new Vector3(0, -height * 0.3f, -0.04f), Quaternion.identity, height * 0.052f, "ivory");
            for (var i = 0; i < 7; i++)
                Box("Unbound diagram trace", parent, new Vector3(-width * 0.35f + i * width * 0.11f, -0.005f, -0.04f), new Vector3(width * 0.06f, height * (0.05f + (i % 3) * 0.04f), 0.008f), accent);
        }

        private static Transform NewGroup(string name, Transform parent, Vector3 position)
        {
            var transform = new GameObject(name).transform;
            transform.SetParent(parent, false);
            transform.localPosition = position;
            return transform;
        }

        private static Transform Anchor(Transform parent, string name, Vector3 position, float yaw)
        {
            var anchor = NewGroup(name, parent, position);
            anchor.localRotation = Quaternion.Euler(0, yaw, 0);
            return anchor;
        }

        private static Transform Box(string name, Transform parent, Vector3 position, Vector3 scale, string material, bool collision = false)
        {
            return Primitive(PrimitiveType.Cube, name, parent, position, scale, material, collision);
        }

        private static Transform Cylinder(string name, Transform parent, Vector3 position, Vector3 scale, string material, bool collision = false)
        {
            return Primitive(PrimitiveType.Cylinder, name, parent, position, scale, material, collision);
        }

        private static Transform Primitive(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, string material, bool collision)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = Materials[material];
            if (!collision) UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
            return obj.transform;
        }

        private static void Label(Transform parent, string text, Vector3 position, Quaternion rotation, float height, string color)
        {
            var label = NewGroup("Label | " + text, parent, position);
            label.localRotation = rotation;
            var mesh = label.gameObject.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.font = labelFont;
            mesh.fontSize = 64;
            mesh.characterSize = height * 0.13f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = Materials[color].GetColor("_BaseColor");
            var renderer = label.GetComponent<MeshRenderer>();
            label.gameObject.AddComponent<ShipLabelAtlasBinder>().Configure(mesh, renderer, labelFont, labelMaterial);
        }

        private static void PointLight(Transform parent, string name, Vector3 position, Color color, float intensity, float range)
        {
            var light = NewGroup(name, parent, position).gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
        }

        private static void PracticalLight(Transform parent, Vector3 position, bool castsShadow)
        {
            var light = NewGroup("Directed practical", parent, position).gameObject.AddComponent<Light>();
            light.type = LightType.Spot;
            light.transform.localRotation = Quaternion.Euler(90, 0, 0);
            light.color = new Color(0.9f, 0.95f, 1);
            light.intensity = 3.0f;
            light.range = 4.5f;
            light.spotAngle = 115;
            light.innerSpotAngle = 80;
            light.shadows = castsShadow ? LightShadows.Soft : LightShadows.None;
            light.shadowResolution = LightShadowResolution.Medium;
            light.shadowBias = 0.04f;
            light.shadowNormalBias = 0.15f;
            light.renderMode = LightRenderMode.ForcePixel;
            // A restrained indirect-light approximation keeps working-height panels readable without
            // the former point source immediately under the ceiling and its clipped upward hotspot.
            PointLight(parent, "Practical indirect fill", new Vector3(position.x, 1.9f, position.z),
                new Color(0.82f, 0.88f, 0.94f), 0.65f, 4.3f);
        }

        private static void SaveModule(Transform room, string fileName)
        {
            // Save module at local origin; restore the assembly placement without modifying existing metadata.
            var oldPosition = room.localPosition;
            room.localPosition = Vector3.zero;
            if (!PrefabUtility.SaveAsPrefabAsset(room.gameObject, ShipPath + "/Modules/" + fileName + ".prefab"))
                throw new InvalidOperationException("Failed to save " + fileName);
            room.localPosition = oldPosition;
        }

        private static void MakePalette()
        {
            Materials.Clear();
            MakeMaterial("hull", new Color(0.08f, 0.1f, 0.12f), 0.3f, 0.35f);
            MakeMaterial("panel", new Color(0.58f, 0.59f, 0.56f), 0.05f, 0.22f);
            MakeMaterial("ceiling", new Color(0.42f, 0.44f, 0.43f), 0, 0.18f);
            MakeMaterial("edge", new Color(0.25f, 0.29f, 0.3f), 0.6f, 0.4f);
            MakeMaterial("deck", new Color(0.12f, 0.15f, 0.16f), 0.4f, 0.25f);
            MakeMaterial("steel", new Color(0.39f, 0.44f, 0.46f), 0.65f, 0.48f);
            MakeMaterial("padding", new Color(0.15f, 0.19f, 0.2f), 0, 0.15f);
            MakeMaterial("amberPaint", new Color(0.85f, 0.49f, 0.12f), 0.1f, 0.3f);
            MakeMaterial("ivory", new Color(0.82f, 0.88f, 0.88f), 0.1f, 0.3f);
            MakeMaterial("ink", new Color(0.055f, 0.075f, 0.09f), 0, 0.15f);
            MakeMaterial("cyan", new Color(0.22f, 0.68f, 0.78f), 0, 0.15f, 0.5f);
            MakeMaterial("amber", new Color(0.9f, 0.56f, 0.2f), 0, 0.15f, 0.4f);
            MakeMaterial("red", new Color(0.95f, 0.18f, 0.12f), 0.1f, 0.4f, 1.5f);
            MakeMaterial("warm", new Color(0.9f, 0.95f, 1), 0, 0.15f, 0.65f);
            MakeMaterial("screen", new Color(0.012f, 0.025f, 0.035f), 0, 0.05f, 0.12f);
            Materials["screen"].SetFloat("_SpecularHighlights", 0);
            Materials["screen"].EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            Materials["screen"].SetFloat("_EnvironmentReflections", 0);
            Materials["screen"].EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            MakeMaterial("space", new Color(0.001f, 0.003f, 0.008f), 0, 0);
            MakeMaterial("star", new Color(0.7f, 0.84f, 1), 0, 0, 2f);
        }

        private static void MakeLabelMaterial()
        {
            const string materialPath = ShipPath + "/Shaders/ShipDepthLabel.mat";
            var shader = Shader.Find("OutOfReach/Ship Depth Label");
            if (!shader) throw new InvalidOperationException("Owned depth-tested label shader is missing.");
            labelMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!labelMaterial)
            {
                labelMaterial = new Material(shader) { name = "Ship depth-tested labels" };
                AssetDatabase.CreateAsset(labelMaterial, materialPath);
            }
            else labelMaterial.shader = shader;
            // The dynamic atlas is assigned per renderer by ShipLabelAtlasBinder, never stored on this asset.
            EditorUtility.SetDirty(labelMaterial);
        }

        private static void MakeMaterial(string name, Color color, float metal, float smoothness, float emission = 0)
        {
            string path = ShipPath + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (!shader) throw new InvalidOperationException("Existing URP/Lit shader not available.");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metal);
            material.SetFloat("_Smoothness", smoothness);
            material.SetColor("_EmissionColor", color * emission);
            // URP's import validator derives the emission keyword from these flags. Without an emissive
            // flag it clears _EMISSION on the next import, extinguishing fixtures in a fresh process.
            material.globalIlluminationFlags = emission > 0 ? MaterialGlobalIlluminationFlags.BakedEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            if (emission > 0) material.EnableKeyword("_EMISSION");
            else material.DisableKeyword("_EMISSION");
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            Materials.Add(name, material);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        [MenuItem("OUT OF REACH/Visuals/Validate saved V01 interior")]
        public static void ValidateExisting()
        {
            if (!TryOpenPreview()) return;
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            var views = roots.SelectMany(root => root.GetComponentsInChildren<ShipVisualAnchors>(true)).ToArray();
            Require(views.Length == 1, "Exactly one visual anchor component required.");
            var view = views[0];
            var expected = new[] { "PlayerSpawn", "CockpitConsole", "PowerRepair", "LifeSupportRepair", "AirlockInner", "AirlockOuter", "EvaStart", "AntennaRepair", "EvaReturn", "FinaleCameraAnchor" };
            var transforms = view.GetComponentsInChildren<Transform>(true);
            foreach (string name in expected)
                Require(transforms.Count(t => t.name == name) == 1, "Anchor missing or duplicated: " + name);
            var serialized = new SerializedObject(view);
            foreach (string name in expected.Concat(new[] { "InnerDoorLeft", "InnerDoorRight", "OuterDoorLeft", "OuterDoorRight" }))
                Require(serialized.FindProperty(name).objectReferenceValue != null, "Unbound anchor: " + name);
            Require(view.GetComponentsInChildren<Camera>(true).Length == 0, "Integrated visual prefab contains a camera.");
            Require(view.GetComponentsInChildren<AudioListener>(true).Length == 0, "Integrated visual prefab contains a listener.");
            foreach (MonoBehaviour component in view.GetComponentsInChildren<MonoBehaviour>(true))
                Require(component is ShipVisualAnchors || component is ShipLabelAtlasBinder, "Unexpected runtime script in visual assembly.");
            foreach (TextMesh label in view.GetComponentsInChildren<TextMesh>(true))
            {
                var binder = label.GetComponent<ShipLabelAtlasBinder>();
                Require(binder && binder.IsConfigured, "Unbound font atlas: " + label.name);
                Require(label.GetComponent<MeshRenderer>().sharedMaterial.shader.name == "OutOfReach/Ship Depth Label", "Label must use depth-tested material: " + label.name);
            }
            foreach (Transform t in transforms)
                Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0, "Missing script: " + t.name);
            foreach (Renderer renderer in view.GetComponentsInChildren<Renderer>(true))
                foreach (Material material in renderer.sharedMaterials)
                {
                    Require(material && material.shader && material.shader.name != "Hidden/InternalErrorShader", "Missing material or shader: " + renderer.name);
                    if (material.HasProperty("_EmissionColor") && material.GetColor("_EmissionColor").maxColorComponent > 0.01f)
                        Require(material.IsKeywordEnabled("_EMISSION"), "Emission was lost during import: " + material.name);
                }
            Require(roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true)).Count() == 1, "Preview must have one camera.");
            Require(!roots.SelectMany(root => root.GetComponentsInChildren<AudioListener>(true)).Any(), "Silent visual preview must not contain an audio listener.");
            Require(roots.Single(root => root.name.StartsWith("PREVIEW ONLY", StringComparison.Ordinal)).CompareTag("EditorOnly"), "Preview rig must be EditorOnly.");
            Require(view.transform.position == Vector3.zero && view.transform.lossyScale == Vector3.one, "Assembly must be metre-scale at origin.");
            Physics.SyncTransforms();
            int samples = 0;
            // 1.2 m clear corridor: five parallel capsule tracks span x=-0.6..0.6 m including body radius.
            for (float z = 0; z <= 20; z += 0.25f)
            {
                for (float x = -0.4f; x < 0.41f; x += 0.2f)
                {
                    var foot = new Vector3(x, 0, z);
                    Require(Physics.Raycast(foot + Vector3.up * 0.15f, Vector3.down, out var floor, 0.25f, ~0, QueryTriggerInteraction.Ignore), "No supporting floor at " + foot);
                    Require(Mathf.Abs(floor.point.y) < 0.035f, "Floor step at " + foot);
                    Require(!Physics.CheckCapsule(foot + Vector3.up * 0.23f, foot + Vector3.up * 1.6f, 0.2f, ~0, QueryTriggerInteraction.Ignore), "Blocked 1.8 m standing envelope at " + foot);
                    samples++;
                }
            }
            foreach (Vector3 foot in new[] { new Vector3(-1.1f, 0, 6.1f), new Vector3(1.1f, 0, 8.1f), view.PlayerSpawn.position })
                Require(!Physics.CheckCapsule(foot + Vector3.up * 0.3f, foot + Vector3.up * 1.5f, 0.25f, ~0, QueryTriggerInteraction.Ignore), "Repair/spawn standing area blocked: " + foot);
            foreach (Transform leaf in new[] { view.InnerDoorLeft, view.InnerDoorRight, view.OuterDoorLeft, view.OuterDoorRight })
                Require(leaf.GetComponentsInChildren<Collider>(true).Length == 0, "Visual leaves must not define authoritative airlock gates.");
            Debug.Log("V01_VALIDATION_PASS anchors=10 serializedBindings=14 corridorCapsules=" + samples + " interiorClearWidth>=1.2m headroom>=1.8m previewCameras=1 prefabCameras=0 audioListeners=0 missingScripts=0");
            Debug.Log("V01_LIMITATIONS: authoring preview only; no player/controller/domain/UI; EVA anchors reserved for V03; no exterior walkable route; visual airlock leaves are not safety gates; screen traces explicitly unbound.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("V01 validation: " + message);
        }

        private static bool TryOpenPreview()
        {
            if (SceneManager.GetActiveScene().path == ScenePath) return true;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            EditorSceneManager.OpenScene(ScenePath);
            return true;
        }

        [MenuItem("OUT OF REACH/Visuals/Capture V01 preview stills")]
        public static void CapturePreview()
        {
            if (!TryOpenPreview()) return;
            ValidateExisting();
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../.local/v01-captures"));
            Directory.CreateDirectory(output);
            var camera = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>()).Single();
            Vector3 oldPosition = camera.transform.position;
            Quaternion oldRotation = camera.transform.rotation;
            var positions = new[] { new Vector3(0.3f, 1.65f, 1.3f), new Vector3(0, 1.65f, 1.5f), new Vector3(0.3f, 1.65f, 5.8f), new Vector3(0, 1.65f, 12f), new Vector3(0.25f, 1.65f, 18f) };
            var targets = new[] { new Vector3(0, 1.4f, 14), new Vector3(0, 1.6f, -2.6f), new Vector3(-1.8f, 1.3f, 6.1f), new Vector3(0, 1.3f, 17f), new Vector3(0, 1.35f, 21) };
            var names = new[] { "01-interior", "02-cockpit", "03-engineering", "04-storage", "05-airlock" };
            var target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            try
            {
                target.Create();
                camera.targetTexture = target;
                for (int i = 0; i < positions.Length; i++)
                {
                    camera.transform.position = positions[i];
                    camera.transform.LookAt(targets[i]);
                    // Initialise SRP light/shadow buffers for this camera before reading the still. The
                    // first synchronous render after a cold scene load is not a stable capture frame.
                    for (int warmup = 0; warmup < 3; warmup++)
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target;
                    var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                    image.Apply();
                    File.WriteAllBytes(Path.Combine(output, names[i] + ".png"), image.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(image);
                }
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                camera.transform.SetPositionAndRotation(oldPosition, oldRotation);
            }
            Debug.Log("V01_CAPTURE_COMPLETE " + output);
        }
    }
}
