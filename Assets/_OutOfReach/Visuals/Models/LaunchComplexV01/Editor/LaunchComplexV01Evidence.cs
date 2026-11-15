using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace OutOfReach.Visuals.LaunchComplexV01.Editor
{
    /// <summary>Evidence for LaunchComplexV01: opens Bootstrap, runs the installer in memory, checks that nothing the
    /// launch depends on moved, and renders PNGs from fixed cameras in the prologue root frame into Evidence~.
    /// Never saves a scene.</summary>
    public static class LaunchComplexV01Evidence
    {
        public const string EvidenceDir = "Assets/_OutOfReach/Visuals/Models/LaunchComplexV01/Evidence~";
        private const string ScenePath = "Assets/_OutOfReach/Scenes/Bootstrap.unity";
        private const string PrologueName = "I06 isolated marine prologue";
        private const string VehicleRootName = "Transport vehicle - root controls launch";
        private const string SiteName = "Launch site - stationary";

        // name, camera position, look-at point (prologue root frame, metres), vertical field of view.
        private static readonly (string name, Vector3 pos, Vector3 look, float fov)[] Shots =
        {
            ("boarding_platform", new Vector3(0f, 1.7f, -12.3f), new Vector3(0f, 1.2f, -2.5f), 70f),
            ("deck_edge_right", new Vector3(3.7f, 1.75f, -7.4f), new Vector3(14f, -32.6f, 22f), 75f),
            ("deck_edge_left", new Vector3(-3.7f, 1.75f, -7.4f), new Vector3(-14f, -32.6f, 22f), 75f),
            ("deck_look_back", new Vector3(3.6f, 1.75f, -12.4f), new Vector3(30f, -32.6f, -36f), 70f),
            ("wide_launch", new Vector3(-95f, -30.5f, 70f), new Vector3(0f, -12f, 0f), 40f),
            ("wide_launch_east", new Vector3(110f, -29f, -55f), new Vector3(0f, -12f, 0f), 38f),
            ("pad_low", new Vector3(22f, -30.9f, 26f), new Vector3(0f, -21f, 0f), 62f),
            ("engines", new Vector3(1.6f, -31.75f, 8.6f), new Vector3(0f, -30.9f, 0f), 55f),
            ("mount", new Vector3(8.5f, -31.2f, 10.5f), new Vector3(0f, -29.4f, 0f), 50f),
            ("vehicle_mid", new Vector3(-16f, -14f, 15f), new Vector3(0f, -15f, 0f), 60f),
            ("trench", new Vector3(9f, -29.5f, 46f), new Vector3(0f, -27f, 0f), 55f),
            ("tank_farm", new Vector3(14f, -25f, -4f), new Vector3(42f, -27f, -34f), 60f),
            ("aerial", new Vector3(70f, 38f, 70f), new Vector3(0f, -22f, 0f), 45f),
        };

        // unity run <project> -- -executeMethod OutOfReach.Visuals.LaunchComplexV01.Editor.LaunchComplexV01Evidence.CaptureBeforeBatch
        public static void CaptureBeforeBatch() => Batch("before", null);

        // unity run <project> -- -executeMethod OutOfReach.Visuals.LaunchComplexV01.Editor.LaunchComplexV01Evidence.InstallAndCaptureBatch
        public static void InstallAndCaptureBatch() => Batch("after", scene =>
        {
            var log = new StringBuilder();
            log.AppendLine("LaunchComplexV01 install check, Unity " + Application.unityVersion + ", scene " + scene.path + " (opened, not saved)");
            // LaunchTowerV03 is a separate delivery on the same branch: install it in memory first when present, so
            // the captures show the pad the way the integrator will see it.
            Type tower = Type.GetType("OutOfReach.Visuals.LaunchTowerV03.Editor.LaunchTowerV03Installer, OutOfReach.LaunchTowerV03.Editor");
            if (tower != null)
            {
                try { log.AppendLine("[tower V03 installer, in memory]\n" + tower.GetMethod("Install").Invoke(null, new object[] { scene })); }
                catch (Exception e) { log.AppendLine("tower V03 installer failed: " + (e.InnerException ?? e).Message); }
            }
            Transform prologue = Named(scene, PrologueName);
            Transform vehicleRoot = prologue.Find(VehicleRootName);
            Transform site = prologue.Find(SiteName);
            Dictionary<string, string> before = Poses(prologue);
            int collidersBefore = prologue.GetComponentsInChildren<Collider>(true).Length;

            LaunchComplexV01Builder.BuildAll();
            log.AppendLine("[installer]\n" + LaunchComplexV01Installer.Install(scene));
            string second = LaunchComplexV01Installer.Install(scene);

            bool ok = true;
            void Check(string what, bool pass, string detail = "")
            {
                ok &= pass;
                log.AppendLine((pass ? "PASS " : "FAIL ") + what + (detail.Length > 0 ? " : " + detail : ""));
            }
            Transform vehicle = vehicleRoot.Find(LaunchComplexV01Installer.VehicleInstanceName);
            Transform pad = site.Find(LaunchComplexV01Installer.PadInstanceName);
            Check("vehicle instance is a child of the launch root at identity", vehicle && vehicle.localPosition == Vector3.zero
                && vehicle.localRotation == Quaternion.identity && vehicle.localScale == Vector3.one);
            Check("pad dressing is a child of the stationary site at identity", pad && pad.localPosition == Vector3.zero
                && pad.localRotation == Quaternion.identity && pad.localScale == Vector3.one);
            Check("second install is a no-op", second.StartsWith("already installed", StringComparison.Ordinal), second);
            Transform booster = vehicleRoot.Find("BoosterStack");
            Check("old BoosterStack kept: active object, renderer off, COL_Booster enabled", booster && booster.gameObject.activeSelf
                && !booster.GetComponent<Renderer>().enabled && booster.Find("COL_Booster") && booster.Find("COL_Booster").GetComponent<Collider>().enabled);
            Transform emitter = vehicleRoot.Find("EngineEmitter");
            Check("EngineEmitter still at (0, -31.4, 0)", emitter && Vector3.Distance(prologue.InverseTransformPoint(emitter.position), new Vector3(0f, -31.4f, 0f)) < .001f,
                emitter ? prologue.InverseTransformPoint(emitter.position).ToString("F3") : "missing");
            Dictionary<string, string> after = Poses(prologue, vehicle, pad);
            string[] changed = before.Where(p => !after.TryGetValue(p.Key, out string v) || v != p.Value).Select(p => p.Key).ToArray();
            Check("every pre-existing transform under the prologue keeps its name, parent and pose (" + before.Count + " transforms)",
                changed.Length == 0 && after.Count == before.Count, string.Join(" | ", changed.Take(5)));
            Check("no collider added or removed (" + collidersBefore + ")", prologue.GetComponentsInChildren<Collider>(true).Length == collidersBefore
                && vehicle.GetComponentsInChildren<Collider>(true).Length == 0 && pad.GetComponentsInChildren<Collider>(true).Length == 0);
            Transform land = site.Cast<Transform>().FirstOrDefault(t => t.name.StartsWith("Land launch site", StringComparison.Ordinal));
            if (land)
            {
                Check("land kit still has 11 renderers and 5 enabled mesh colliders", land.GetComponentsInChildren<MeshRenderer>(true).Length == 11
                    && land.GetComponentsInChildren<MeshCollider>(true).Count(c => c.enabled) == 5);
                foreach (string name in new[] { "LS_LaunchMount", "LS_Umbilicals", "LS_WaterTower", "LS_FloodTowers" })
                    Check(name + " renderer off, object active", land.Find(name) && land.Find(name).gameObject.activeSelf && !land.Find(name).GetComponent<Renderer>().enabled);
                foreach (string name in new[] { "LS_FlameTrench", "LS_Barriers", "LS_Markings", "LS_LaunchFoundation", "LS_PadDeck_Optional" })
                    Check(name + " renderer still on", land.Find(name) && land.Find(name).GetComponent<Renderer>().enabled);
            }
            var renderers = vehicle.GetComponentsInChildren<MeshRenderer>(true).Concat(pad.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
            Check("all " + renderers.Length + " new renderers use LC1 URP Lit materials with their base maps", renderers.All(r => r.sharedMaterials.All(m =>
                m && m.name.StartsWith("LC1_", StringComparison.Ordinal) && m.shader.name == "Universal Render Pipeline/Lit"
                && (m.name.StartsWith("LC1_Lamp", StringComparison.Ordinal) || m.name == "LC1_GlassDark" || m.GetTexture("_BaseMap")))));
            Bounds vb = LocalBounds(prologue, vehicle.Find("LC1_Vehicle_LOD0").GetComponent<Renderer>());
            Check("vehicle LOD0 bounds stay inside the old booster envelope plus fins", vb.min.y > -31.7f && vb.max.y < -.5f
                && vb.max.x < 2.75f && vb.max.z < 2.75f && vb.min.x > -2.75f && vb.min.z > -2.75f, vb.min.ToString("F2") + " .. " + vb.max.ToString("F2"));
            var tris = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (MeshFilter f in vehicle.GetComponentsInChildren<MeshFilter>(true).Concat(pad.GetComponentsInChildren<MeshFilter>(true)))
                tris[f.name] = LaunchComplexV01Builder.TriangleCount(f.sharedMesh);
            log.AppendLine("triangles after Unity import: " + string.Join(", ", tris.Select(p => p.Key + "=" + p.Value)));
            foreach (string lod in new[] { "_LOD0", "_LOD1", "_LOD2" })
                log.AppendLine("  total " + lod + ": vehicle " + tris.Where(p => p.Key.StartsWith("LC1_Vehicle_", StringComparison.Ordinal) && p.Key.EndsWith(lod, StringComparison.Ordinal)).Sum(p => p.Value)
                    + ", pad " + tris.Where(p => !p.Key.StartsWith("LC1_Vehicle_", StringComparison.Ordinal) && p.Key.EndsWith(lod, StringComparison.Ordinal)).Sum(p => p.Value));
            log.AppendLine("RESULT " + (ok ? "PASS" : "FAIL"));
            Directory.CreateDirectory(EvidenceDir);
            File.WriteAllText(Path.Combine(EvidenceDir, "unity_install_check.txt"), log.ToString());
            Debug.Log("LC1_INSTALL_CHECK\n" + log);
        });

        public static void Batch(string tag, Action<Scene> prepare)
        {
            try
            {
                Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                prepare?.Invoke(scene);
                int shots = Capture(scene, tag);
                Debug.Log("LC1_EVIDENCE OK tag=" + tag + " shots=" + shots + " (scene not saved)");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("LC1_EVIDENCE FAIL " + e);
                EditorApplication.Exit(1);
            }
        }

        public static int Capture(Scene scene, string tag, int width = 1920, int height = 1080)
        {
            Transform prologue = Named(scene, PrologueName);
            Directory.CreateDirectory(EvidenceDir);
            var go = new GameObject("LC1 evidence camera") { hideFlags = HideFlags.HideAndDontSave };
            int count = 0;
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.nearClipPlane = .1f;
                cam.farClipPlane = 8000f;
                cam.allowHDR = true;
                var data = go.AddComponent<UniversalAdditionalCameraData>();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.renderShadows = true;
                foreach (var shot in Shots)
                {
                    go.transform.position = prologue.TransformPoint(shot.pos);
                    go.transform.rotation = Quaternion.LookRotation(prologue.TransformPoint(shot.look) - go.transform.position, Vector3.up);
                    cam.fieldOfView = shot.fov;
                    RenderTexture rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 1);
                    var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                    try
                    {
                        cam.targetTexture = rt;
                        cam.Render();
                        cam.Render();
                        RenderTexture.active = rt;
                        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                        tex.Apply();
                        File.WriteAllBytes(Path.Combine(EvidenceDir, "urp_" + tag + "_" + shot.name + ".png"), tex.EncodeToPNG());
                        count++;
                    }
                    finally
                    {
                        RenderTexture.active = null;
                        cam.targetTexture = null;
                        RenderTexture.ReleaseTemporary(rt);
                        Object.DestroyImmediate(tex);
                    }
                }
            }
            finally { Object.DestroyImmediate(go); }
            return count;
        }

        private static Transform Named(Scene scene, string name) =>
            scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).First(t => t.name == name);

        /// <summary>Path -> parent-relative pose of every transform under root, skipping the given subtrees.</summary>
        private static Dictionary<string, string> Poses(Transform root, params Transform[] skip)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (skip.Any(s => s && t.IsChildOf(s))) continue;
                var parts = new List<string>();
                for (Transform p = t; p && p != root.parent; p = p.parent) parts.Add(p.name);
                parts.Reverse();
                string path = string.Join("/", parts);
                counts.TryGetValue(path, out int n);
                counts[path] = n + 1;
                result[path + "#" + n] = t.localPosition.ToString("F4") + t.localRotation.eulerAngles.ToString("F3") + t.localScale.ToString("F4");
            }
            return result;
        }

        private static Bounds LocalBounds(Transform frame, Renderer renderer)
        {
            Bounds w = renderer.bounds;
            var b = new Bounds(frame.InverseTransformPoint(w.center), Vector3.zero);
            foreach (int sx in new[] { -1, 1 }) foreach (int sy in new[] { -1, 1 }) foreach (int sz in new[] { -1, 1 })
                b.Encapsulate(frame.InverseTransformPoint(w.center + Vector3.Scale(w.extents, new Vector3(sx, sy, sz))));
            return b;
        }
    }
}
