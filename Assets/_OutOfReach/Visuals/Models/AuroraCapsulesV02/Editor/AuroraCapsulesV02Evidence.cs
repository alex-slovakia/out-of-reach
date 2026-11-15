using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace OutOfReach.Visuals.AuroraCapsulesV02.Editor
{
    /// <summary>Verification only. Opens Bootstrap, installs in memory, renders URP captures of both vehicles in
    /// their real scene context, uninstalls and proves the delivered meshes are back. Never saves the scene.
    ///   Unity -batchmode -projectPath . -executeMethod OutOfReach.Visuals.AuroraCapsulesV02.Editor.AuroraCapsulesV02Evidence.RunBatch</summary>
    public static class AuroraCapsulesV02Evidence
    {
        private const string ScenePath = "Assets/_OutOfReach/Scenes/Bootstrap.unity";

        public static void RunBatch()
        {
            var log = new StringBuilder();
            string outDir = Path.GetFullPath(AuroraCapsulesV02Builder.Root + "/Previews~");
            Directory.CreateDirectory(outDir);
            int code = 0;
            try
            {
                log.AppendLine("unity " + Application.unityVersion + " " + DateTime.Now.ToString("u"));
                AuroraCapsulesV02Builder.BuildAll();
                Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Transform[] all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
                Transform vehicle = all.First(t => t.name == AuroraCapsulesV02Installer.VehicleName);
                Transform courier = all.First(t => t.name == AuroraCapsulesV02Installer.ArrivalCourierName);
                Transform arrival = courier.parent;
                MeshFilter shell = vehicle.GetComponentsInChildren<MeshFilter>(true).First(f => f.name == "CapsuleShell");
                Mesh delivered = shell.sharedMesh;
                log.AppendLine("delivered shell mesh " + delivered.name + " " + delivered.bounds.size.ToString("F3"));

                Shots(outDir, "unity_before", vehicle, courier, arrival, all, log);
                log.AppendLine(AuroraCapsulesV02Installer.Install(scene, true, true, true));
                log.AppendLine("second install (idempotent):");
                log.AppendLine(AuroraCapsulesV02Installer.Install(scene, true, true, true));
                int extras = all.Length;
                var known = new System.Collections.Generic.HashSet<int>(all.Select(t => t.GetInstanceID()));
                Transform[] after = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
                log.AppendLine("objects before " + extras + " after " + after.Length + " (added " + (after.Length - extras) + ", all named '*" + AuroraCapsulesV02Installer.Tag + "': "
                    + after.Where(t => !known.Contains(t.GetInstanceID())).All(t => t.name.EndsWith(AuroraCapsulesV02Installer.Tag, StringComparison.Ordinal)) + ")");
                log.AppendLine("colliders under vehicle: " + vehicle.GetComponentsInChildren<Collider>(true).Length
                    + ", shell mesh now " + shell.sharedMesh.name + " " + shell.sharedMesh.bounds.size.ToString("F3"));
                Shots(outDir, "unity_after", vehicle, courier, arrival, all, log);

                log.AppendLine(AuroraCapsulesV02Installer.Uninstall(scene));
                Transform[] restored = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
                bool same = shell.sharedMesh == delivered && restored.Length == all.Length && !shell.GetComponent<LODGroup>();
                log.AppendLine("uninstall restores delivered mesh and object count: " + (same ? "PASS" : "FAIL"));
                if (!same) throw new InvalidOperationException("Uninstall did not restore the delivered state.");
                log.AppendLine("scene saved: no (isDirty=" + scene.isDirty + ")");
                log.AppendLine("RESULT PASS");
            }
            catch (Exception e)
            {
                log.AppendLine("RESULT FAIL " + e);
                code = 1;
            }
            File.WriteAllText(Path.Combine(outDir, "unity_check.txt"), log.ToString());
            Debug.Log("AC2_EVIDENCE\n" + log);
            EditorApplication.Exit(code);
        }

        private static void Shots(string dir, string prefix, Transform vehicle, Transform courier, Transform arrival, Transform[] all, StringBuilder log)
        {
            // crew transport in the launch-site context (vehicle frame: +Y nose, hatch on -Z)
            Shot(dir, prefix + "_ct_hatch_approach", vehicle, new Vector3(.35f, 1.62f, -5.6f), new Vector3(0f, 1.25f, -2.4f), 60f, false, log);
            Shot(dir, prefix + "_ct_hatch_side", vehicle, new Vector3(4.6f, 2.4f, -7.2f), new Vector3(0f, 1.6f, -2.0f), 42f, false, log);
            Shot(dir, prefix + "_ct_quarter", vehicle, new Vector3(-9.5f, 5.0f, -9.5f), new Vector3(0f, 2.3f, 0f), 38f, false, log);
            Shot(dir, prefix + "_ct_back", vehicle, new Vector3(8.5f, 5.2f, 9.5f), new Vector3(0f, 2.3f, 0f), 38f, false, log);
            Shot(dir, prefix + "_ct_dock", vehicle, new Vector3(1.9f, 7.1f, -2.7f), new Vector3(0f, 4.8f, 0f), 45f, false, log);
            Shot(dir, prefix + "_ct_studio", vehicle, new Vector3(-6.5f, 3.2f, -8.5f), new Vector3(0f, 2.3f, 0f), 38f, true, log);

            // rescue craft: the real cinematic framing, then close studio views
            bool wasActive = arrival.gameObject.activeSelf;
            arrival.gameObject.SetActive(true);
            try
            {
                Transform cameraStart = all.FirstOrDefault(t => t.name == "RecoveryArrival_CameraStart");
                Transform cameraEnd = all.FirstOrDefault(t => t.name == "RecoveryArrival_CameraEnd");
                Transform end = all.FirstOrDefault(t => t.name == "RecoveryArrival_CourierEnd");
                if (cameraStart) Pose(dir, prefix + "_rc_cinematic_start", cameraStart.position, cameraStart.rotation, 55f, false, log);
                if (cameraStart) Pose(dir, prefix + "_rc_cinematic_start_lit", cameraStart.position, cameraStart.rotation, 55f, true, log);
                if (cameraEnd && end)
                {
                    Vector3 p = courier.position; Quaternion q = courier.rotation;
                    courier.SetPositionAndRotation(end.position, end.rotation);
                    Pose(dir, prefix + "_rc_cinematic_end_lit", cameraEnd.position, cameraEnd.rotation, 55f, true, log);
                    courier.SetPositionAndRotation(p, q);
                }
                Shot(dir, prefix + "_rc_hero", courier, new Vector3(7.5f, 4.2f, 9.5f), new Vector3(0f, 1.4f, 0f), 38f, true, log);
                Shot(dir, prefix + "_rc_nose", courier, new Vector3(3.6f, 10.5f, 5.2f), new Vector3(0f, 3.6f, 0f), 38f, true, log);
                Shot(dir, prefix + "_rc_aft", courier, new Vector3(6.5f, -5.5f, 6.0f), new Vector3(0f, -.6f, 0f), 38f, true, log);
                Shot(dir, prefix + "_rc_far", courier, new Vector3(22f, 9f, 28f), new Vector3(0f, 1.4f, 0f), 30f, true, log);
            }
            finally
            {
                arrival.gameObject.SetActive(wasActive);
            }
        }

        private static void Shot(string dir, string name, Transform frame, Vector3 eye, Vector3 target, float fov, bool studio, StringBuilder log)
        {
            Vector3 position = frame.TransformPoint(eye);
            Pose(dir, name, position, Quaternion.LookRotation(frame.TransformPoint(target) - position, frame.up), fov, studio, log);
        }

        private static void Pose(string dir, string name, Vector3 position, Quaternion rotation, float fov, bool studio, StringBuilder log)
        {
            const int width = 1920, height = 1080;
            var go = new GameObject("AC2 evidence camera");
            GameObject key = null;
            RenderTexture rt = null;
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.fieldOfView = fov;
                cam.nearClipPlane = .08f;
                cam.farClipPlane = 6000f;
                cam.allowHDR = true;
                cam.clearFlags = studio ? CameraClearFlags.SolidColor : CameraClearFlags.Skybox;
                cam.backgroundColor = new Color(.015f, .018f, .026f);
                go.transform.SetPositionAndRotation(position, rotation);
                Type extra = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
                if (extra != null)
                {
                    Component data = go.AddComponent(extra);
                    extra.GetProperty("renderPostProcessing")?.SetValue(data, true);
                }
                if (studio)
                {
                    key = new GameObject("AC2 evidence key light");
                    var light = key.AddComponent<Light>();
                    light.type = LightType.Directional;
                    light.intensity = 1.6f;
                    light.color = new Color(1f, .97f, .92f);
                    light.shadows = LightShadows.Soft;
                    key.transform.rotation = rotation * Quaternion.Euler(28f, -38f, 0f);
                }
                rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
                Color32[] pixels = tex.GetPixels32();
                long sum = 0;
                for (int i = 0; i < pixels.Length; i += 97) sum += pixels[i].r + pixels[i].g + pixels[i].b;
                log.AppendLine("capture " + name + ".png mean " + (sum / (3.0 * (pixels.Length / 97))).ToString("F1"));
                Object.DestroyImmediate(tex);
                cam.targetTexture = null;
            }
            finally
            {
                RenderTexture.active = null;
                if (rt) Object.DestroyImmediate(rt);
                if (key) Object.DestroyImmediate(key);
                Object.DestroyImmediate(go);
            }
        }
    }
}
