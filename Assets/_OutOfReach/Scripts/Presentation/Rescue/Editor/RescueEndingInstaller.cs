using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace OutOfReach.Presentation.Rescue.Editor
{
    /// <summary>
    /// Integrator-invoked additive scene authoring for RESCUE-ENDING-01 (R42). Adds one owned object tree under the
    /// existing cinematic presenter's parent; existing meshes, materials, anchors and the camera chain are only read.
    /// Idempotent: a previous install of this tree is replaced. Run AFTER the composition patch so the composition's
    /// <c>rescueEnding</c> field can be bound (otherwise the install still works but stays unbound and inert).
    /// </summary>
    public static class RescueEndingInstaller
    {
        public const string ScenePath = "Assets/_OutOfReach/Scenes/Bootstrap.unity";
        public const string RootName = "Rescue ending (RESCUE-ENDING-01)";
        public const string PropFolder = "Assets/_OutOfReach/Visuals/Props/RescueCraftV01";
        public const string MaterialFolder = PropFolder + "/Materials";
        public const string ApproachVoicePath = "Assets/_OutOfReach/Audio/Voices/Story02/vo24a_rescue_approach.wav";
        public const string DockedVoicePath = "Assets/_OutOfReach/Audio/Voices/Story02/vo24b_rescue_docked.wav";

        [MenuItem("OUT OF REACH/Integration/Install rescue ending (RESCUE-ENDING-01)")]
        public static void InstallInActiveScene()
        {
            Install(SceneManager.GetActiveScene(), false, out string report);
            Debug.Log(report);
        }

        /// <summary>Batch: -executeMethod OutOfReach.Presentation.Rescue.Editor.RescueEndingInstaller.InstallBootstrapBatch</summary>
        public static void InstallBootstrapBatch()
        {
            try
            {
                Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Install(scene, true, out string report);
                Debug.Log(report);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static RescueEndingPresenter Install(Scene scene, bool save, out string report)
        {
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Open the integrated Bootstrap first.");
            GameObject[] roots = scene.GetRootGameObjects();
            MonoBehaviour cinematics = Single(roots, "MissionCinematicPresenter");
            MonoBehaviour anchors = Single(roots, "ShipVisualAnchors");
            var cinematicFields = new SerializedObject(cinematics);
            var pivot = cinematicFields.FindProperty("cameraOffsetPivot").objectReferenceValue as Transform;
            var camera = cinematicFields.FindProperty("presentationCamera").objectReferenceValue as Camera;
            var blackout = cinematicFields.FindProperty("blackoutOverlay").objectReferenceValue as CanvasGroup;
            if (!pivot) throw new InvalidOperationException("MissionCinematicPresenter has no cameraOffsetPivot; the rescue view needs the same pivot.");
            if (!camera) camera = pivot.GetComponentsInChildren<Camera>(true).FirstOrDefault(c => c.CompareTag("MainCamera")) ?? pivot.GetComponentInChildren<Camera>(true);
            if (!camera || !camera.transform.IsChildOf(pivot)) throw new InvalidOperationException("No gameplay camera below the cinematic pivot.");
            Transform ship = anchors.transform;
            var anchorFields = new SerializedObject(anchors);
            Transform airlockOuter = anchorFields.FindProperty("AirlockOuter").objectReferenceValue as Transform;
            Transform cockpit = anchorFields.FindProperty("CockpitConsole").objectReferenceValue as Transform;
            if (!airlockOuter || !cockpit) throw new InvalidOperationException("Ship anchors AirlockOuter/CockpitConsole are missing.");

            Transform parent = cinematics.transform.parent ? cinematics.transform.parent : cinematics.transform;
            foreach (Transform old in parent.Cast<Transform>().Where(t => t.name == RootName).ToArray()) Object.DestroyImmediate(old.gameObject);

            Materials m = EnsureMaterials();
            AssetDatabase.SaveAssets();
            var root = new GameObject(RootName);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var presenter = root.AddComponent<RescueEndingPresenter>();
            AudioSource voice = Source(root, "voice", 1f), sfx = Source(root, "sfx", 1f);

            // Ship-space authoring: the ship runs along +Z, the cockpit windows face -Z, the airlock hatch faces +Z.
            Transform markers = Child("Markers (ship space)", root.transform);
            Vector3 windowEye = P(ship, .86f, 2.2f, -2.42f);
            Transform windowView = Marker("View_Window", markers, windowEye, Look(windowEye, P(ship, .95f, 2.27f, -3.4f), ship));
            // External camera above and beside the aft end (clear of the hull blankets), looking aft and down along the
            // docking axis: the last metres of the approach and the contact at the hatch are both in frame.
            Vector3 dockEye = P(ship, 4.2f, 4.6f, 22.6f);
            Transform dockView = Marker("View_AftHullCamera", markers, dockEye, Look(dockEye, P(ship, 0f, 1.45f, 25.5f), ship));
            Vector3 hatchEye = P(ship, 0f, 1.62f, 17.6f);
            Transform hatchView = Marker("View_AirlockHatch", markers, hatchEye, Look(hatchEye, P(ship, 0f, 1.32f, 21f), ship));
            Vector3 far = P(ship, 16f, 9f, -260f), near = P(ship, 3.6f, 1.7f, -26f), aside = P(ship, 18f, 3f, -4f);
            Transform craftFar = Marker("Craft_Far", markers, far, Look(far, windowEye, ship));
            // Near the window it turns to show its flank (3/4 view), then keeps turning as it slides aft.
            Transform craftNear = Marker("Craft_Near", markers, near, Quaternion.LookRotation(ship.TransformDirection(new Vector3(1f, 0f, .75f)), ship.up));
            Transform craftAside = Marker("Craft_Aside", markers, aside, Quaternion.LookRotation(ship.TransformDirection(new Vector3(.6f, 0f, 1f)), ship.up));
            float dockedZ = 23.05f + RescueCraftV01.CollarFrontZ;
            Transform craftDockStart = Marker("Craft_DockStart", markers, P(ship, .55f, 1.95f, dockedZ + 24f), Quaternion.LookRotation(ship.TransformDirection(Vector3.back), ship.up));
            Transform craftDocked = Marker("Craft_Docked", markers, P(ship, 0f, 1.45f, dockedZ), Quaternion.LookRotation(ship.TransformDirection(Vector3.back), ship.up));

            var visuals = new GameObject("Visuals (active only during the ending)");
            visuals.transform.SetParent(root.transform, false);
            var craftObject = new GameObject("RescueCraftV01");
            craftObject.transform.SetParent(visuals.transform, false);
            craftObject.transform.SetPositionAndRotation(craftFar.position, craftFar.rotation);
            var craft = craftObject.AddComponent<RescueCraftV01>();
            craft.ConfigureMaterials(m.Hull, m.HullLight, m.Dark, m.Amber, m.Cyan, m.Plume, m.Window);

            GameObject glow = Primitive(PrimitiveType.Cylinder, "HatchLight_CraftHatch", visuals.transform, m.Glow);
            // The rescue craft's own open hatch at its docked collar face: light from inside the craft, seen through Aurora's opening leaves.
            glow.transform.SetPositionAndRotation(P(ship, 0f, 1.45f, 23.02f), Quaternion.LookRotation(ship.TransformDirection(Vector3.forward), ship.up) * Quaternion.Euler(90f, 0f, 0f));
            var glowPivot = new GameObject("HatchLight_Width").transform;
            glowPivot.SetParent(visuals.transform, false);
            glowPivot.SetPositionAndRotation(glow.transform.position, glow.transform.rotation);
            glow.transform.SetParent(glowPivot, true);
            glow.transform.localScale = new Vector3(1.18f, .01f, 1.18f);
            glow.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var lightObject = new GameObject("HatchLight_Flood");
            lightObject.transform.SetParent(visuals.transform, false);
            lightObject.transform.SetPositionAndRotation(P(ship, 0f, 1.45f, 22.85f), Quaternion.LookRotation(ship.TransformDirection(Vector3.back), ship.up));
            var flood = lightObject.AddComponent<Light>();
            flood.type = LightType.Spot; flood.spotAngle = 120f; flood.range = 16f; flood.color = new Color(1f, .92f, .8f);
            flood.shadows = LightShadows.None; flood.intensity = 0f; flood.enabled = false;
            Light approach = SpotLight("AuroraNoseFloodlight", visuals.transform, P(ship, 0f, 1.7f, -6.2f), near, ship, 46f, 95f, new Color(.86f, .93f, 1f));
            Light dockingFlood = SpotLight("AftPortFloodlight", visuals.transform, P(ship, 1.6f, 2.9f, 21.2f), P(ship, 0f, 1.45f, 30f), ship, 58f, 45f, new Color(.9f, .95f, 1f));
            Renderer[] lamps = new[] { new Vector3(-.78f, 2.22f, 20.86f), new Vector3(0f, 2.34f, 20.86f), new Vector3(.78f, 2.22f, 20.86f) }
                .Select((p, i) =>
                {
                    GameObject lamp = Primitive(PrimitiveType.Cube, "LatchLamp_" + (i + 1), visuals.transform, m.Amber);
                    lamp.transform.SetPositionAndRotation(P(ship, p.x, p.y, p.z), ship.rotation);
                    lamp.transform.localScale = new Vector3(.09f, .05f, .05f);
                    return lamp.GetComponent<Renderer>();
                }).ToArray();

            // Docking-camera reticle: thin cyan bars authored for 0.5 m in front of the camera; range readout.
            var reticle = new GameObject("DockingCameraReticle");
            reticle.transform.SetParent(visuals.transform, false);
            Bar(reticle.transform, m.Reticle, new Vector2(0f, 0f), new Vector2(.05f, .0016f));
            Bar(reticle.transform, m.Reticle, new Vector2(0f, 0f), new Vector2(.0016f, .05f));
            foreach (int sx in new[] { -1, 1 })
                foreach (int sy in new[] { -1, 1 })
                {
                    Bar(reticle.transform, m.Reticle, new Vector2(sx * .135f, sy * .1f), new Vector2(.03f, .0016f), new Vector2(-sx * .015f, 0f));
                    Bar(reticle.transform, m.Reticle, new Vector2(sx * .15f, sy * .085f), new Vector2(.0016f, .03f), new Vector2(0f, -sy * .015f));
                }
            var readoutObject = new GameObject("RangeReadout");
            readoutObject.transform.SetParent(reticle.transform, false);
            readoutObject.transform.localPosition = new Vector3(-.19f, -.155f, 0f);
            var readout = readoutObject.AddComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            readout.font = font; readout.fontSize = 48; readout.characterSize = .0035f; readout.anchor = TextAnchor.LowerLeft;
            readout.color = new Color(.45f, .95f, 1f); readout.text = "RANGE";
            readoutObject.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            var label = Object.Instantiate(readoutObject, reticle.transform);
            label.name = "CameraLabel";
            label.transform.localPosition = new Vector3(-.19f, .14f, 0f);
            label.GetComponent<TextMesh>().text = "AFT HULL CAMERA · DOCKING PORT";
            reticle.SetActive(false);
            visuals.SetActive(false);

            var fields = new SerializedObject(presenter);
            Set(fields, "cameraPivot", pivot); Set(fields, "viewCamera", camera);
            Set(fields, "windowView", windowView); Set(fields, "dockingCameraView", dockView); Set(fields, "hatchView", hatchView);
            Set(fields, "visualSet", visuals); Set(fields, "craft", craft);
            Set(fields, "craftFar", craftFar); Set(fields, "craftNear", craftNear); Set(fields, "craftAside", craftAside);
            Set(fields, "craftDockStart", craftDockStart); Set(fields, "craftDocked", craftDocked);
            Set(fields, "hatchGlow", glowPivot); Set(fields, "hatchGlowRenderer", glow.GetComponent<Renderer>()); Set(fields, "hatchLight", flood);
            SerializedProperty lampArray = fields.FindProperty("latchLamps");
            lampArray.arraySize = lamps.Length;
            for (int i = 0; i < lamps.Length; i++) lampArray.GetArrayElementAtIndex(i).objectReferenceValue = lamps[i];
            Set(fields, "lampAmber", m.Amber); Set(fields, "lampCyan", m.Cyan);
            Transform leafLeft = anchorFields.FindProperty("OuterDoorLeft").objectReferenceValue as Transform;
            Transform leafRight = anchorFields.FindProperty("OuterDoorRight").objectReferenceValue as Transform;
            SerializedProperty leaves = fields.FindProperty("hatchLeaves");
            leaves.arraySize = leafLeft && leafRight ? 2 : 0;
            if (leafLeft && leafRight) { leaves.GetArrayElementAtIndex(0).objectReferenceValue = leafLeft; leaves.GetArrayElementAtIndex(1).objectReferenceValue = leafRight; }
            Set(fields, "approachLight", approach); Set(fields, "dockingLight", dockingFlood);
            Set(fields, "reticle", reticle.transform); Set(fields, "reticleReadout", readout);
            Set(fields, "blackout", blackout);
            Set(fields, "voiceSource", voice); Set(fields, "sfxSource", sfx);
            Set(fields, "approachVoice", AssetDatabase.LoadAssetAtPath<AudioClip>(ApproachVoicePath));
            Set(fields, "dockedVoice", AssetDatabase.LoadAssetAtPath<AudioClip>(DockedVoicePath));
            fields.ApplyModifiedPropertiesWithoutUndo();

            string composition = BindComposition(roots, presenter);
            EditorSceneManager.MarkSceneDirty(scene);
            if (save && !EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save " + scene.path);
            report = "RESCUE_ENDING_INSTALL_PASS root=" + RootName + " validViews=" + presenter.HasValidViews
                + " pivot=" + pivot.name + " camera=" + camera.name + " blackout=" + (blackout ? blackout.name : "none")
                + " vo24a=" + (AssetDatabase.LoadAssetAtPath<AudioClip>(ApproachVoicePath) != null)
                + " vo24b=" + (AssetDatabase.LoadAssetAtPath<AudioClip>(DockedVoicePath) != null)
                + " composition=" + composition + " saved=" + save;
            return presenter;
        }

        private static string BindComposition(GameObject[] roots, RescueEndingPresenter presenter)
        {
            MonoBehaviour composition = roots.SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true))
                .FirstOrDefault(b => b && b.GetType().Name == "I01MissionComposition");
            if (!composition) return "absent";
            var so = new SerializedObject(composition);
            SerializedProperty property = so.FindProperty("rescueEnding");
            if (property == null) return "unpatched(field rescueEnding missing; apply Integration~/composition-rescue.patch first)";
            property.objectReferenceValue = presenter;
            so.ApplyModifiedPropertiesWithoutUndo();
            return "bound";
        }

        private static MonoBehaviour Single(GameObject[] roots, string typeName)
        {
            MonoBehaviour[] found = roots.SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true))
                .Where(b => b && b.GetType().Name == typeName).ToArray();
            if (found.Length != 1) throw new InvalidOperationException("Expected exactly one " + typeName + ", found " + found.Length + ".");
            return found[0];
        }

        private static Vector3 P(Transform ship, float x, float y, float z) => ship.TransformPoint(new Vector3(x, y, z));

        private static Light SpotLight(string name, Transform parent, Vector3 position, Vector3 target, Transform ship, float angle, float range, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Look(position, target, ship));
            var light = go.AddComponent<Light>();
            light.type = LightType.Spot; light.spotAngle = angle; light.range = range; light.color = color;
            light.shadows = LightShadows.None; light.intensity = 0f; light.enabled = false;
            return light;
        }

        private static Quaternion Look(Vector3 from, Vector3 to, Transform ship) => Quaternion.LookRotation(to - from, ship.up);

        private static Transform Child(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static Transform Marker(string name, Transform parent, Vector3 position, Quaternion rotation)
        {
            Transform marker = Child(name, parent);
            marker.SetPositionAndRotation(position, rotation);
            return marker;
        }

        private static AudioSource Source(GameObject owner, string name, float volume)
        {
            var go = new GameObject("Audio_" + name);
            go.transform.SetParent(owner.transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = false; source.spatialBlend = 0f; source.volume = volume;
            source.ignoreListenerPause = false; source.priority = 32;
            return source;
        }

        private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go;
        }

        private static void Bar(Transform parent, Material material, Vector2 centre, Vector2 size, Vector2 offset = default)
        {
            GameObject bar = Primitive(PrimitiveType.Quad, "ReticleBar", parent, material);
            bar.transform.localPosition = new Vector3(centre.x + offset.x, centre.y + offset.y, 0f);
            bar.transform.localScale = new Vector3(size.x, size.y, 1f);
            bar.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        private static void Set(SerializedObject so, string field, Object value)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null) throw new InvalidOperationException("Missing serialized field " + field);
            property.objectReferenceValue = value;
        }

        private sealed class Materials
        {
            public Material Hull, HullLight, Dark, Amber, Cyan, Plume, Window, Glow, Reticle;
        }

        /// <summary>Creates or updates the owned prop materials (graphite/amber/cyan palette).</summary>
        private static Materials EnsureMaterials()
        {
            if (!AssetDatabase.IsValidFolder(PropFolder)) AssetDatabase.CreateFolder("Assets/_OutOfReach/Visuals/Props", "RescueCraftV01");
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder(PropFolder, "Materials");
            Shader lit = Shader.Find("Universal Render Pipeline/Lit"), unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (!lit || !unlit) throw new InvalidOperationException("URP Lit/Unlit shaders are required.");
            return new Materials
            {
                Hull = Lit("RC_Hull", lit, new Color(.3f, .31f, .33f), .45f, .5f),
                HullLight = Lit("RC_HullLight", lit, new Color(.62f, .64f, .66f), .25f, .55f),
                Dark = Lit("RC_Dark", lit, new Color(.06f, .065f, .07f), .6f, .35f),
                // Lamps and windows are self-lit (Unlit, HDR colour for bloom): no shader keyword to lose on import.
                Amber = Unlit("RC_Amber", unlit, new Color(1f, .55f, .1f) * 2.6f),
                Cyan = Unlit("RC_Cyan", unlit, new Color(.25f, .9f, 1f) * 2.6f),
                Window = Unlit("RC_Window", unlit, new Color(.12f, .42f, .52f)),
                Plume = Unlit("RC_Plume", unlit, new Color(.7f, .9f, 1f) * 3.4f),
                Glow = Unlit("RC_HatchLight", unlit, new Color(1f, .9f, .74f) * 1.2f),
                Reticle = Unlit("RC_Reticle", unlit, new Color(.3f, 1f, 1.1f) * 1.4f),
            };
        }

        private static Material Lit(string name, Shader shader, Color baseColor, float metallic, float smoothness, Color? emission = null)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = !material;
            if (created) material = new Material(shader) { name = name };
            material.shader = shader;
            material.SetColor("_BaseColor", baseColor);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            if (created) AssetDatabase.CreateAsset(material, path); else EditorUtility.SetDirty(material);
            return material;
        }

        private static Material Unlit(string name, Shader shader, Color color)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = !material;
            if (created) material = new Material(shader) { name = name };
            material.shader = shader;
            material.SetColor("_BaseColor", color);
            if (created) AssetDatabase.CreateAsset(material, path); else EditorUtility.SetDirty(material);
            return material;
        }
    }
}
