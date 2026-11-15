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
    /// <summary>Diagnosis only: renders the cargo-bay view of the closed hatch in Bootstrap and lists every enabled
    /// renderer whose bounds reach into the door area. Never saves the scene.</summary>
    public static class AuroraCapsulesV02DoorProbe
    {
        public static void RunBatch()
        {
            var log = new StringBuilder();
            string dir = Path.GetFullPath(AuroraCapsulesV02Builder.Root + "/Previews~");
            Directory.CreateDirectory(dir);
            int code = 0;
            try
            {
                Scene scene = EditorSceneManager.OpenScene("Assets/_OutOfReach/Scenes/Bootstrap.unity", OpenSceneMode.Single);
                Transform[] all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
                Transform vehicle = all.First(t => t.name == AuroraCapsulesV02Installer.VehicleName);
                Transform hatchFrame = vehicle.Cast<Transform>().First(t => t.name.StartsWith("Hatch control", StringComparison.Ordinal));
                log.AppendLine("hatch frame rotation in scene: " + hatchFrame.localRotation.eulerAngles.ToString("F1"));
                Capture(dir, "probe_scene_pose_aisle", vehicle, new Vector3(.05f, 1.50f, .55f), new Vector3(-.15f, 1.25f, -2.4f), 60f);
                hatchFrame.localRotation = Quaternion.identity;
                Capture(dir, "probe_closed_aisle", vehicle, new Vector3(.05f, 1.50f, .55f), new Vector3(-.15f, 1.25f, -2.4f), 60f);
                Capture(dir, "probe_closed_seat", vehicle, new Vector3(0f, 1.40f, 1.40f), new Vector3(0f, 1.15f, -2.4f), 60f);
                Capture(dir, "probe_closed_right_edge", vehicle, new Vector3(.35f, 1.5f, -.9f), new Vector3(-.75f, 1.35f, -2.4f), 60f);

                // The tower arm swings about a pivot beside its centre line: reproduce the swing with the hatch shut.
                Transform arm = all.FirstOrDefault(t => t.name.StartsWith("Access arm control", StringComparison.Ordinal));
                if (arm)
                {
                    Quaternion armPose = arm.localRotation;
                    Vector3 eye = new Vector3(.05f, 1.50f, .55f), target = new Vector3(-.15f, 1.25f, -2.4f);
                    foreach (int angle in new[] { 12, 24, 40 })
                    {
                        arm.localRotation = Quaternion.Euler(0f, -angle, 0f);
                        Capture(dir, "probe_arm_" + angle + "_before", vehicle, eye, target, 60f);
                    }
                    arm.localRotation = Quaternion.Euler(0f, -24f, 0f);
                    bool hide = AuroraTransportCabinDresser.ShouldHide(vehicle, hatchFrame, vehicle.TransformPoint(eye));
                    log.AppendLine("dresser rule, aisle view, hatch shut: hide=" + hide);
                    Renderer[] armRenderers = arm.GetComponentsInChildren<Renderer>(true);
                    log.AppendLine("arm renderers: " + string.Join(", ", armRenderers.Select(r => r.name + (r.enabled && r.gameObject.activeInHierarchy ? "" : " (off)"))));
                    AuroraTransportCabinDresser.SetDrawn(armRenderers, !hide);
                    Capture(dir, "probe_arm_24_after", vehicle, eye, target, 60f);
                    AuroraTransportCabinDresser.SetDrawn(armRenderers, true);
                    log.AppendLine("dresser rule, view outside on the arm, hatch shut: hide="
                        + AuroraTransportCabinDresser.ShouldHide(vehicle, hatchFrame, vehicle.TransformPoint(new Vector3(0f, 1.6f, -6f))));
                    hatchFrame.localRotation = Quaternion.Euler(0f, 160f, 0f);
                    log.AppendLine("dresser rule, aisle view, hatch open 160: hide=" + AuroraTransportCabinDresser.ShouldHide(vehicle, hatchFrame, vehicle.TransformPoint(eye)));
                    hatchFrame.localRotation = Quaternion.identity;
                    arm.localRotation = armPose;
                }

                // Everything that reaches into the cabin near the door (vehicle frame: hatch on -Z, viewer's right = -X).
                Report(log, "RIGHT OF DOOR  x -1.25..-0.60, y 0..2.6, z -2.45..-1.6", vehicle, new Vector3(-.925f, 1.3f, -2.025f), new Vector3(.65f, 2.6f, .85f));
                Report(log, "DOOR AREA  x -1.3..1.3, y 0..2.6, z -2.6..-1.6", vehicle, new Vector3(0f, 1.3f, -2.1f), new Vector3(2.6f, 2.6f, 1.0f));
            }
            catch (Exception e)
            {
                log.AppendLine("PROBE FAIL " + e);
                code = 1;
            }
            File.WriteAllText(Path.Combine(dir, "probe_door.txt"), log.ToString());
            Debug.Log("AC2_DOOR_PROBE\n" + log);
            EditorApplication.Exit(code);
        }

        private static void Report(StringBuilder log, string title, Transform vehicle, Vector3 centre, Vector3 size)
        {
            log.AppendLine("== " + title);
            // probe box corners in world space -> world AABB (the prologue root only yaws, so this stays tight enough)
            var box = new Bounds(vehicle.TransformPoint(centre), Vector3.zero);
            for (int i = 0; i < 8; i++)
                box.Encapsulate(vehicle.TransformPoint(centre + Vector3.Scale(size * .5f, new Vector3((i & 1) * 2 - 1, (i >> 1 & 1) * 2 - 1, (i >> 2 & 1) * 2 - 1))));
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!r.enabled || !r.bounds.Intersects(box)) continue;
                Vector3 lo = vehicle.InverseTransformPoint(r.bounds.min), hi = vehicle.InverseTransformPoint(r.bounds.max);
                var filter = r.GetComponent<MeshFilter>();
                // does any vertex of the mesh actually lie inside the probe box?
                int inside = -1;
                if (filter && filter.sharedMesh)
                {
                    try
                    {
                        int count = 0;
                        foreach (Vector3 v in filter.sharedMesh.vertices)
                        {
                            Vector3 p = vehicle.InverseTransformPoint(r.transform.TransformPoint(v)) - centre;
                            if (Mathf.Abs(p.x) <= size.x * .5f && Mathf.Abs(p.y) <= size.y * .5f && Mathf.Abs(p.z) <= size.z * .5f) count++;
                        }
                        inside = count;
                    }
                    catch (Exception) { inside = -2; }
                }
                string path = r.name;
                for (Transform p = r.transform.parent; p; p = p.parent) path = p.name + "/" + path;
                log.AppendLine("  " + path + " | mesh " + (filter && filter.sharedMesh ? filter.sharedMesh.name : r.GetType().Name)
                    + " | verts in box " + inside + " | vehicle-frame bounds " + Vector3.Min(lo, hi).ToString("F2") + ".." + Vector3.Max(lo, hi).ToString("F2")
                    + " | mats " + string.Join(",", r.sharedMaterials.Select(m => m ? m.name : "null")));
            }
        }

        private static void Capture(string dir, string name, Transform frame, Vector3 eye, Vector3 target, float fov)
        {
            const int width = 1600, height = 1000;
            var go = new GameObject("AC2 probe camera");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = fov; cam.nearClipPlane = .05f; cam.farClipPlane = 6000f; cam.allowHDR = true;
            cam.clearFlags = CameraClearFlags.Skybox;
            go.transform.position = frame.TransformPoint(eye);
            go.transform.rotation = Quaternion.LookRotation(frame.TransformPoint(target) - go.transform.position, frame.up);
            Type extra = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (extra != null) extra.GetProperty("renderPostProcessing")?.SetValue(go.AddComponent(extra), true);
            // a neutral fill so dark-on-dark shapes can be told apart; it does not change the scene asset
            var fill = new GameObject("AC2 probe fill").AddComponent<Light>();
            fill.type = LightType.Point; fill.range = 6f; fill.intensity = 2.5f; fill.shadows = LightShadows.None;
            fill.transform.position = frame.TransformPoint(eye + new Vector3(0f, .2f, .1f));
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            RenderTexture.active = null; cam.targetTexture = null;
            Object.DestroyImmediate(tex); Object.DestroyImmediate(rt); Object.DestroyImmediate(fill.gameObject); Object.DestroyImmediate(go);
        }
    }
}
