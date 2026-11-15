// Scratch-project check for LaunchTowerV03 (lives in Source~, so the game project never compiles it).
// Copy into <scratch>/Assets/Editor/ of an EMPTY Unity 6000.3.28f1 URP project that holds only the LaunchTowerV03 folder, then:
//   Unity -batchmode -quit -projectPath <scratch> -executeMethod LaunchTowerV03ScratchCheck.Run -logFile <log>
// It runs the real builder, then the real installer against a stand-in hierarchy that copies the Bootstrap names,
// checks pivots, colliders, both arm poses and the keep-clear corridors, and writes URP captures.
using System;
using System.IO;
using System.Linq;
using System.Text;
using OutOfReach.Visuals.LaunchTowerV03.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public static class LaunchTowerV03ScratchCheck
{
    public static void Run()
    {
        var log = new StringBuilder();
        string outDir = Path.GetFullPath("Assets/../ScratchOut");
        Directory.CreateDirectory(outDir);
        try
        {
            log.AppendLine("unity " + Application.unityVersion);
            LaunchTowerV03Builder.BuildAll();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LaunchTowerV03Builder.PrefabPath);
            int tris = prefab.GetComponentsInChildren<MeshFilter>(true).Sum(f => f.sharedMesh.triangles.Length / 3);
            log.AppendLine("prefab tris=" + tris + " renderers=" + prefab.GetComponentsInChildren<MeshRenderer>(true).Length
                + " colliders=" + prefab.GetComponentsInChildren<Collider>(true).Length);
            foreach (var f in prefab.GetComponentsInChildren<MeshFilter>(true))
                log.AppendLine("  " + f.name + " pos=" + f.transform.localPosition.ToString("F4") + " tris=" + f.sharedMesh.triangles.Length / 3
                    + " bounds=" + f.sharedMesh.bounds.min.ToString("F3") + ".." + f.sharedMesh.bounds.max.ToString("F3")
                    + " mats=" + string.Join(",", f.GetComponent<MeshRenderer>().sharedMaterials.Select(m => m ? m.name + ":" + m.shader.name : "NULL")));

            // ---- stand-in Bootstrap hierarchy with the real names ----
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prologue = new GameObject("I06 isolated marine prologue").transform;
            prologue.position = new Vector3(100f, 40f, -50f);           // non-trivial world frame
            prologue.rotation = Quaternion.Euler(0f, 30f, 0f);
            var site = new GameObject("Launch site - stationary").transform; site.SetParent(prologue, false);
            var armControl = new GameObject("Access arm control +Y 0 connected to -90 retracted").transform;
            armControl.SetParent(site, false); armControl.localPosition = LaunchTowerV03Builder.ArmPivot;
            var blockArm = GameObject.CreatePrimitive(PrimitiveType.Cube); blockArm.name = "AccessArm";
            blockArm.transform.SetParent(armControl, false); blockArm.transform.localPosition = new Vector3(.9f, -.3f, 2.2f);
            Object.DestroyImmediate(blockArm.GetComponent<Collider>());
            var colFloor = new GameObject("COL_ArmFloor"); colFloor.layer = 3; colFloor.transform.SetParent(armControl, false);
            var floorBox = colFloor.AddComponent<BoxCollider>();   // the MarineLayout ArmFloor box, pivot-relative
            floorBox.center = new Vector3(0f, -.03f, -4.725f) - LaunchTowerV03Builder.ArmPivot; floorBox.size = new Vector3(1.8f, .06f, 4.35f);
            var blockTower = GameObject.CreatePrimitive(PrimitiveType.Cube); blockTower.name = "AccessMastTower";
            blockTower.transform.SetParent(site, false); Object.DestroyImmediate(blockTower.GetComponent<Collider>());
            var v02 = new GameObject("Launch tower V02 detail - b330226"); v02.transform.SetParent(site, false);
            var v02arm = new GameObject("LTV2_ArmDetail"); v02arm.transform.SetParent(armControl, false);

            string install = LaunchTowerV03Installer.Install(scene);
            log.AppendLine("install:\n" + install);
            string again = LaunchTowerV03Installer.Install(scene);
            log.AppendLine("second install: " + again.Trim());
            Transform arm = armControl.Find(LaunchTowerV03Installer.ArmInstanceName);
            Check(log, "arm parent is control", arm && arm.parent == armControl);
            Check(log, "arm pivot", Vector3.Distance(prologue.InverseTransformPoint(arm.position), LaunchTowerV03Builder.ArmPivot) < .001f);
            Check(log, "V02 inactive", !v02.activeSelf && !v02arm.activeSelf);
            Check(log, "blockout renderers off", !blockArm.GetComponent<Renderer>().enabled && !blockTower.GetComponent<Renderer>().enabled);
            Check(log, "collider layer copied", arm.GetComponentsInChildren<Collider>().All(c => c.gameObject.layer == 3));
            Transform tower = site.Find(LaunchTowerV03Installer.InstanceName);
            Check(log, "tower identity in prologue", Vector3.Distance(prologue.InverseTransformPoint(tower.position), Vector3.zero) < 1e-4f
                && Quaternion.Angle(Quaternion.Inverse(prologue.rotation) * tower.rotation, Quaternion.identity) < .01f);

            // floor collider top must be y = 0 in the prologue frame, walkway must be free
            Physics.SyncTransforms();
            Vector3 probe = prologue.TransformPoint(new Vector3(0f, .5f, -4.5f));
            bool hitFloor = Physics.Raycast(probe, -prologue.up, out RaycastHit hit, 2f, ~0, QueryTriggerInteraction.Ignore);
            Check(log, "floor top y=0 (" + (hitFloor ? prologue.InverseTransformPoint(hit.point).y.ToString("F4") : "miss") + ")",
                hitFloor && Mathf.Abs(prologue.InverseTransformPoint(hit.point).y) < .002f);
            Collider[] inCorridor = Physics.OverlapBox(prologue.TransformPoint(new Vector3(0f, 1.11f, -4.7f)), new Vector3(.9f, 1.07f, 2.18f),
                prologue.rotation, ~0, QueryTriggerInteraction.Ignore);
            bool corridorBlocked = inCorridor.Length > 0;
            foreach (Collider c in inCorridor) log.AppendLine("  in corridor: " + c.name);
            Check(log, "walkway corridor x+-0.9 y0.04..2.18 z-6.88..-2.52 free of colliders", !corridorBlocked);

            // ---- captures ----
            SetupUrp(log);
            Standins(prologue);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 2.2f; sun.shadows = LightShadows.Soft;
            sun.transform.rotation = prologue.rotation * Quaternion.Euler(38f, 140f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.36f, .4f, .47f);
            Capture(prologue, outDir, "urp_overview", new Vector3(-34f, -4f, 26f), new Vector3(0f, -10f, -9f), 38f);
            Capture(prologue, outDir, "urp_arm_walk", new Vector3(0f, 1.62f, -8.6f), new Vector3(0f, 1.2f, -2.4f), 62f);
            Capture(prologue, outDir, "urp_arm_side", new Vector3(7.5f, 2.8f, -1.5f), new Vector3(-.3f, .5f, -5.5f), 50f);
            armControl.localRotation = Quaternion.Euler(0f, -90f, 0f);
            Capture(prologue, outDir, "urp_arm_retracted", new Vector3(-9f, 6f, 4f), new Vector3(-2.5f, 0f, -7f), 50f);
            armControl.localRotation = Quaternion.identity;
            log.AppendLine("RESULT PASS");
        }
        catch (Exception e)
        {
            log.AppendLine("RESULT FAIL " + e);
        }
        File.WriteAllText(Path.Combine(outDir, "scratch_check.txt"), log.ToString());
        Debug.Log("LTV3_SCRATCH_CHECK\n" + log);
    }

    private static void Check(StringBuilder log, string what, bool ok)
    {
        log.AppendLine((ok ? "PASS " : "FAIL ") + what);
        if (!ok) throw new InvalidOperationException("check failed: " + what);
    }

    private static void SetupUrp(StringBuilder log)
    {
        if (GraphicsSettings.defaultRenderPipeline) return;
        var data = ScriptableObject.CreateInstance<UniversalRendererData>();
        AssetDatabase.CreateAsset(data, "Assets/ScratchURP_Renderer.asset");
        var asset = UniversalRenderPipelineAsset.Create(data);
        asset.shadowDistance = 120f;
        AssetDatabase.CreateAsset(asset, "Assets/ScratchURP.asset");
        GraphicsSettings.defaultRenderPipeline = asset;
        QualitySettings.renderPipeline = asset;
        log.AppendLine("scratch URP asset assigned");
    }

    private static void Standins(Transform prologue)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); mat.SetColor("_BaseColor", new Color(.55f, .56f, .58f));
        GameObject Prim(PrimitiveType t, Vector3 c, Vector3 s)
        {
            var g = GameObject.CreatePrimitive(t); g.transform.SetParent(prologue, false);
            g.transform.localPosition = c; g.transform.localScale = s; g.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(g.GetComponent<Collider>()); return g;
        }
        Prim(PrimitiveType.Cube, new Vector3(0, -33.1f, -2f), new Vector3(36, 1, 24));                   // barge deck
        Prim(PrimitiveType.Cube, new Vector3(0, -.3f, -9.9f), new Vector3(8, .6f, 6));                    // crew deck module
        Prim(PrimitiveType.Cylinder, new Vector3(0, -16f, 0), new Vector3(5.79f, 15.5f, 5.79f));         // booster
        Prim(PrimitiveType.Cylinder, new Vector3(0, 1.2f, 0), new Vector3(5.15f, 1.7f, 5.15f));          // capsule
    }

    private static void Capture(Transform prologue, string dir, string name, Vector3 eye, Vector3 target, float fov)
    {
        var go = new GameObject("cam " + name);
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = fov; cam.farClipPlane = 600f; cam.nearClipPlane = .05f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.42f, .48f, .56f);
        go.transform.position = prologue.TransformPoint(eye);
        go.transform.rotation = Quaternion.LookRotation(prologue.TransformPoint(target) - go.transform.position, Vector3.up);
        var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
        File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
        RenderTexture.active = null; cam.targetTexture = null;
        Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
    }
}
