using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace OutOfReach.Visuals.InteriorHeroV01.Editor
{
    /// <summary>Evidence for INTERIOR-HERO-V01: opens Bootstrap, renders every prop view from the player's eye height
    /// before and after the install, and writes the install log. Never saves the scene.</summary>
    public static class InteriorHeroV01Evidence
    {
        private const int Width = 1600, Height = 900;
        private const float GameFieldOfView = 68f;   // I01BootstrapBuilder: Player camera

        // Unity -batchmode -quit -executeMethod OutOfReach.Visuals.InteriorHeroV01.Editor.InteriorHeroV01Evidence.CaptureBootstrapBatch
        public static void CaptureBootstrapBatch()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/_OutOfReach/Scenes/Bootstrap.unity", OpenSceneMode.Single);
            string folder = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
                InteriorHeroV01Builder.Root.Replace('/', Path.DirectorySeparatorChar), "Evidence~");
            Directory.CreateDirectory(folder);
            var log = new StringBuilder();
            log.AppendLine("INTERIOR-HERO-V01 evidence, Unity " + Application.unityVersion + ", " + DateTime.UtcNow.ToString("u"));
            log.AppendLine("project " + Path.GetFullPath(Path.Combine(Application.dataPath, "..")));
            InteriorHeroV01Builder.BuildAll();
            List<InteriorHeroV01Builder.View> views = Views(InteriorHeroV01Builder.LoadReport());
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == "PlayerRig") root.SetActive(false);    // capture the room, not the first-person body
            string before = Survey(scene);
            log.AppendLine("--- install");
            log.AppendLine(InteriorHeroV01Installer.Install(scene).TrimEnd());
            foreach (InteriorHeroV01Builder.View view in views) Capture(view, Path.Combine(folder, "unity_after_" + view.name + ".png"));
            log.AppendLine("--- gameplay objects before == after: " + (before == Survey(scene) ? "IDENTICAL" : "DIFFERENT"));
            log.AppendLine(before.TrimEnd());
            log.AppendLine("--- presenter hooks on the new props (edit mode, through the game's own builders)");
            log.AppendLine(CropAndRations(scene, folder));
            log.AppendLine("--- uninstall");
            log.AppendLine(InteriorHeroV01Installer.Uninstall(scene).TrimEnd());
            // The "before" stills are taken after the uninstall: the same camera, the restored V01 props.
            foreach (InteriorHeroV01Builder.View view in views) Capture(view, Path.Combine(folder, "unity_before_" + view.name + ".png"));
            log.AppendLine("--- gameplay objects after uninstall: " + (before == Survey(scene) ? "IDENTICAL" : "DIFFERENT"));
            log.AppendLine("captures " + views.Count + " views x before/after at " + Width + "x" + Height + ", vertical FOV " + GameFieldOfView);
            File.WriteAllText(Path.Combine(folder, "unity_install_log.txt"), log.ToString().Replace("\r\n", "\n"));
            Debug.Log("INTERIOR_HERO_V01_EVIDENCE\n" + log);
        }

        private static List<InteriorHeroV01Builder.View> Views(InteriorHeroV01Builder.Report report)
        {
            var views = new List<InteriorHeroV01Builder.View>
            {
                new InteriorHeroV01Builder.View { name = "cockpit_from_spawn", eye = new[] { 0f, 1.65f, .8f }, target = new[] { 0f, 1.15f, -1.95f } },
                new InteriorHeroV01Builder.View { name = "cockpit_console_walkup", eye = new[] { 0f, 1.65f, -.75f }, target = new[] { 0f, 1.0f, -1.9f } },
            };
            foreach (InteriorHeroV01Builder.PropSpec prop in report.props)
                if (prop.views != null) views.AddRange(prop.views);
            return views;
        }

        private static void Capture(InteriorHeroV01Builder.View view, string path)
        {
            var cameraObject = new GameObject("InteriorHeroV01 evidence camera") { hideFlags = HideFlags.HideAndDontSave };
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            var readback = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                Vector3 eye = new Vector3(view.eye[0], view.eye[1], view.eye[2]), look = new Vector3(view.target[0], view.target[1], view.target[2]);
                camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye, Vector3.up));
                camera.fieldOfView = GameFieldOfView;
                camera.nearClipPlane = .05f;
                camera.farClipPlane = 100f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.005f, .008f, .015f);
                camera.allowHDR = true;
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                readback.Apply();
                RenderTexture.active = null;
                camera.targetTexture = null;
                File.WriteAllBytes(path, readback.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(readback);
            }
        }

        /// <summary>Every interaction target, collider and anchor-like transform with its pose and state, as text.</summary>
        private static string Survey(Scene scene)
        {
            var lines = new List<string>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == InteriorHeroV01Installer.InstanceName || t.GetComponentInParent<LODGroup>()) continue;
                    foreach (Collider collider in t.GetComponents<Collider>())
                        lines.Add("collider " + PathOf(t) + " " + collider.GetType().Name + " en=" + collider.enabled + " trig=" + collider.isTrigger
                            + " c=" + collider.bounds.center.ToString("F3") + " s=" + collider.bounds.size.ToString("F3"));
                    foreach (MonoBehaviour behaviour in t.GetComponents<MonoBehaviour>())
                    {
                        if (!behaviour) continue;
                        SerializedProperty id = new SerializedObject(behaviour).FindProperty("interactionId");
                        if (id != null && id.propertyType == SerializedPropertyType.String)
                            lines.Add("interactable " + id.stringValue + " " + PathOf(t) + " p=" + t.position.ToString("F3") + " r=" + t.eulerAngles.ToString("F1"));
                    }
                }
            }
            lines.Sort(StringComparer.Ordinal);
            return lines.Count + " entries\n" + string.Join("\n", lines.Where(l => l.StartsWith("interactable", StringComparison.Ordinal)));
        }

        /// <summary>R23 and R36 on the new props: asks the game's own HabitatMatureCrop / HabitatStoredRations builders for
        /// their displays in edit mode, renders them, then removes them again.</summary>
        private static string CropAndRations(Scene scene, string folder)
        {
            var log = new StringBuilder();
            Transform ship = scene.GetRootGameObjects().Single(r => r.name == InteriorHeroV01Installer.ShipModelRootName).transform;
            Transform[] all = ship.GetComponentsInChildren<Transform>(true);
            Transform seedlings = all.FirstOrDefault(t => t.name == "HB_GrowSeedlings");
            Transform habitat = all.FirstOrDefault(t => t.name == "I06 authored Storage habitat");
            try
            {
                Type cropType = FindType("OutOfReach.Presentation.Habitat.HabitatMatureCrop");
                if (cropType != null && seedlings)
                {
                    var crop = (GameObject)cropType.GetMethod("Build", BindingFlags.Public | BindingFlags.Static)
                        .Invoke(null, new object[] { seedlings.gameObject, Vector3.up });
                    if (crop)
                    {
                        crop.SetActive(true);
                        Bounds bounds = new Bounds(crop.transform.position, Vector3.zero);
                        foreach (Renderer r in crop.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
                        Capture(new InteriorHeroV01Builder.View { name = "r23", eye = new[] { 1.05f, 1.65f, 14.45f }, target = new[] { 2.12f, 1.32f, 14.75f } },
                            Path.Combine(folder, "unity_after_grow_unit_r23_mature_crop.png"));
                        log.AppendLine("R23 HabitatMatureCrop.Build: " + crop.transform.childCount + " plants, bounds min " + bounds.min.ToString("F3")
                            + " max " + bounds.max.ToString("F3") + " (rendered, then removed)");
                        Object.DestroyImmediate(crop);
                    }
                    else log.AppendLine("R23 HabitatMatureCrop.Build returned null");
                }
                else log.AppendLine("R23 not checked: type or seedlings missing");
                Type rationType = FindType("OutOfReach.Presentation.Habitat.HabitatStoredRations");
                if (rationType != null && habitat)
                {
                    object rations = rationType.GetMethod("Build", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { habitat });
                    string objectName = (string)rationType.GetField("ObjectName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
                    Transform display = objectName != null ? habitat.Find(objectName) : null;
                    if (rations != null && display)
                    {
                        display.gameObject.SetActive(true);
                        int pouches = 0;
                        foreach (Transform pouch in display) { pouch.gameObject.SetActive(true); pouches++; }
                        Capture(new InteriorHeroV01Builder.View { name = "r36", eye = new[] { 1.15f, 1.65f, 13.25f }, target = new[] { 2.3f, .5f, 13.25f } },
                            Path.Combine(folder, "unity_after_food_stowage_r36_eight_pouches.png"));
                        for (int i = 0; i < display.childCount; i += 2) display.GetChild(i).gameObject.SetActive(false);
                        Capture(new InteriorHeroV01Builder.View { name = "r36b", eye = new[] { 1.15f, 1.65f, 13.25f }, target = new[] { 2.3f, .5f, 13.25f } },
                            Path.Combine(folder, "unity_after_food_stowage_r36_four_pouches.png"));
                        log.AppendLine("R36 HabitatStoredRations.Build: " + pouches + " separate pouch objects at " + display.position.ToString("F3")
                            + " (rendered with eight and with every second one hidden, then removed)");
                        Object.DestroyImmediate(display.gameObject);
                    }
                    else log.AppendLine("R36 display is not buildable in edit mode (the sealed pouch source mesh only exists at run time)");
                }
                else log.AppendLine("R36 not checked: type or habitat root missing");
            }
            catch (Exception e) { log.AppendLine("presenter hook check failed: " + e.GetType().Name + " " + e.Message); }
            return log.ToString().TrimEnd();
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            return null;
        }

        private static string PathOf(Transform t)
        {
            string path = t.name;
            while (t.parent) { t = t.parent; path = t.name + "/" + path; }
            return path;
        }
    }
}
