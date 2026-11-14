// StandingIdleUnitCheck - STANDING-IDLE-UNIT-02: native Unity 6000.3.28f1 check of the corrected animation-only clip on
// the canonical WORK and EVA bodies, in an isolated throw-away project (not the game project, no scene, no prefab).
//
// Expects in Assets/UnitCheck/: A7_BodyFit02_WORK.fbx and A7_BodyFit02_EVA.fbx with their committed .meta files,
// BODY_FloatNeutral_V04.fbx with its committed .meta, and BODY_StandIdle_V01.fbx / BODY_StandIdle_V02.fbx with V04's
// importer settings (only the guid and the take name differ). Run:
//   Unity -batchmode -projectPath <project> -executeMethod StandingIdleUnitCheck.Run -logFile <log>
// with UNITCHECK_OUT=<dir>. Writes unity_check.json, skin/*.bin (baked world vertices, float32 xyz; triangles int32),
// bones/*.json (world joint positions) and captures/*.png (same-eye and side views).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class StandingIdleUnitCheck
{
    const string Dir = "Assets/UnitCheck/";
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string outDir;
    static readonly float[] Setbacks = { 0.33f, 0.197f };

    public static void Run()
    {
        outDir = Environment.GetEnvironmentVariable("UNITCHECK_OUT");
        if (string.IsNullOrEmpty(outDir)) outDir = Path.GetFullPath("UnitCheckOut");
        foreach (var sub in new[] { "", "skin", "bones", "captures" }) Directory.CreateDirectory(Path.Combine(outDir, sub));
        ShaderUtil.allowAsyncCompilation = false;
        try
        {
            RunInner();
            Debug.Log("[unitcheck] DONE");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError("[unitcheck] FAILED " + e);
            File.WriteAllText(Path.Combine(outDir, "error.txt"), e.ToString());
            EditorApplication.Exit(1);
        }
    }

    static string F(float v) { return v.ToString("0.#########", Inv); }
    static string V(Vector3 v) { return "[" + F(v.x) + "," + F(v.y) + "," + F(v.z) + "]"; }
    static string Q(string s) { return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""; }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t)
        {
            var r = FindDeep(c, name);
            if (r != null) return r;
        }
        return null;
    }

    static AnimationClip Clip(string fbx, string contains)
    {
        return AssetDatabase.LoadAllAssetsAtPath(Dir + fbx).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__") && c.name.Contains(contains)).OrderBy(c => c.name.Length).First();
    }

    static string ImporterJson(string fbx)
    {
        var imp = (ModelImporter)AssetImporter.GetAtPath(Dir + fbx);
        var clips = AssetDatabase.LoadAllAssetsAtPath(Dir + fbx).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__")).Select(c => Q(c.name));
        return "{\"animationType\":" + Q(imp.animationType.ToString()) + ",\"preserveHierarchy\":" + (imp.preserveHierarchy ? "true" : "false")
            + ",\"animationCompression\":" + Q(imp.animationCompression.ToString()) + ",\"globalScale\":" + F(imp.globalScale)
            + ",\"useFileScale\":" + (imp.useFileScale ? "true" : "false") + ",\"fileScale\":" + F(imp.fileScale)
            + ",\"optimizeGameObjects\":" + (imp.optimizeGameObjects ? "true" : "false")
            + ",\"skinWeights\":" + Q(imp.skinWeights.ToString()) + ",\"maxBonesPerVertex\":" + imp.maxBonesPerVertex
            + ",\"clips\":[" + string.Join(",", clips) + "]}";
    }

    // the clip's own keys, independent of sampling
    static string CurveJson(AnimationClip clip)
    {
        var bindings = AnimationUtility.GetCurveBindings(clip);
        var sb = new StringBuilder("{\"length_s\":" + F(clip.length) + ",\"frameRate\":" + F(clip.frameRate)
            + ",\"curve_count\":" + bindings.Length + ",\"transform_paths\":" + bindings.Select(b => b.path).Distinct().Count());
        foreach (var target in new[] { "A7_ContinuousBody_Rig", "root", "upperleg01.L" })
        {
            foreach (var prop in new[] { "m_LocalScale", "m_LocalPosition" })
            {
                var axes = new List<string>();
                foreach (var ax in new[] { "x", "y", "z" })
                {
                    var b = bindings.FirstOrDefault(x => (x.path == target || x.path.EndsWith("/" + target)) && x.propertyName == prop + "." + ax);
                    if (b.propertyName == null) { axes.Add("null"); continue; }
                    var keys = AnimationUtility.GetEditorCurve(clip, b).keys;
                    axes.Add("{\"first\":" + F(keys[0].value) + ",\"min\":" + F(keys.Min(k => k.value)) + ",\"max\":" + F(keys.Max(k => k.value)) + ",\"keys\":" + keys.Length + "}");
                }
                sb.Append(",\"" + target + " " + prop + "\":[" + string.Join(",", axes) + "]");
            }
        }
        return sb.Append("}").ToString();
    }

    static void WriteFloats(string path, Vector3[] v)
    {
        using (var w = new BinaryWriter(File.Create(path)))
        {
            w.Write(v.Length);
            foreach (var p in v) { w.Write(p.x); w.Write(p.y); w.Write(p.z); }
        }
    }

    static void WriteInts(string path, int[] t)
    {
        using (var w = new BinaryWriter(File.Create(path)))
        {
            w.Write(t.Length);
            foreach (var i in t) w.Write(i);
        }
    }

    static Material boots, cloth, deckMat, helper;

    static void Materials()
    {
        var sh = Shader.Find("Standard");
        boots = new Material(sh) { color = new Color(0.15f, 0.55f, 0.28f) };
        cloth = new Material(sh) { color = new Color(0.72f, 0.72f, 0.70f) };
        deckMat = new Material(sh) { color = new Color(0.30f, 0.32f, 0.36f) };
        helper = new Material(sh) { color = new Color(0.08f, 0.08f, 0.08f) };
    }

    static void Paint(GameObject go)
    {
        foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var mats = r.sharedMaterials.Select(m => m != null && m.name.Contains("Boot") ? boots : cloth).ToArray();
            r.sharedMaterials = mats;
        }
    }

    static void Capture(Camera cam, string file)
    {
        var rt = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 8 };
        cam.targetTexture = rt;
        cam.aspect = 960f / 540f;
        cam.Render();
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
        tex.Apply();
        File.WriteAllBytes(Path.Combine(outDir, "captures", file), tex.EncodeToPNG());
        RenderTexture.active = null;
        cam.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(rt);
        UnityEngine.Object.DestroyImmediate(tex);
    }

    static List<GameObject> SideHelpers(float setback)
    {
        var list = new List<GameObject>();
        var eye = new Vector3(0f, 1.65f, setback);
        var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        s.transform.position = eye;
        s.transform.localScale = Vector3.one * 0.04f;
        s.GetComponent<Renderer>().sharedMaterial = helper;
        list.Add(s);
        foreach (var ang in new[] { 80f - 34f, 80f, 80f + 34f })
        {
            var a = ang * Mathf.Deg2Rad;
            var d = new Vector3(0f, -Mathf.Sin(a), Mathf.Cos(a));
            var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            c.transform.position = eye + d * 1.1f;
            c.transform.rotation = Quaternion.FromToRotation(Vector3.up, d);
            c.transform.localScale = new Vector3(0.008f, 1.1f, 0.008f);
            c.GetComponent<Renderer>().sharedMaterial = helper;
            list.Add(c);
        }
        return list;
    }

    static void RunInner()
    {
        Materials();
        var sb = new StringBuilder("{\n\"unity\":" + Q(Application.unityVersion) + ",\n\"skinWeightsQuality\":" + Q(QualitySettings.skinWeights.ToString()));
        var files = new[] { "A7_BodyFit02_WORK.fbx", "A7_BodyFit02_EVA.fbx", "BODY_FloatNeutral_V04.fbx", "BODY_StandIdle_V01.fbx", "BODY_StandIdle_V02.fbx" };
        sb.Append(",\n\"importers\":{");
        sb.Append(string.Join(",", files.Select(f => "\n " + Q(f) + ":" + ImporterJson(f))));
        sb.Append("}");

        var clipV04 = Clip("BODY_FloatNeutral_V04.fbx", "FloatNeutral");
        var clipV01 = Clip("BODY_StandIdle_V01.fbx", "StandIdle");
        var clipV02 = Clip("BODY_StandIdle_V02.fbx", "StandIdle");
        var outfits = new[] { "WORK", "EVA" };
        var relaxed = new Dictionary<string, AnimationClip>
        {
            { "WORK", Clip("A7_BodyFit02_WORK.fbx", "StandRelaxed") },
            { "EVA", Clip("A7_BodyFit02_EVA.fbx", "StandRelaxed") },
        };
        sb.Append(",\n\"clip_keys\":{");
        sb.Append("\n \"V04 " + clipV04.name + "\":" + CurveJson(clipV04));
        sb.Append(",\n \"WORK " + relaxed["WORK"].name + "\":" + CurveJson(relaxed["WORK"]));
        sb.Append(",\n \"V01 " + clipV01.name + "\":" + CurveJson(clipV01));
        sb.Append(",\n \"V02 " + clipV02.name + "\":" + CurveJson(clipV02));
        sb.Append("}");

        // deck and light for the captures
        var deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
        deck.name = "Deck";
        deck.transform.position = new Vector3(0f, -0.01f, 0f);
        deck.transform.localScale = new Vector3(4f, 0.02f, 4f);
        deck.GetComponent<Renderer>().sharedMaterial = deckMat;
        var lightGo = new GameObject("Sun");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.45f);
        var camGo = new GameObject("Cam");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.82f, 0.85f, 0.88f);
        cam.enabled = false;

        sb.Append(",\n\"poses\":{");
        bool firstPose = true;
        foreach (var outfit in outfits)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + "A7_BodyFit02_" + outfit + ".fbx");
            var poses = new List<KeyValuePair<string, AnimationClip>>
            {
                new KeyValuePair<string, AnimationClip>("Rest", null),
                new KeyValuePair<string, AnimationClip>("StandRelaxed", relaxed[outfit]),
                new KeyValuePair<string, AnimationClip>("V04Float", clipV04),
                new KeyValuePair<string, AnimationClip>("V01", clipV01),
                new KeyValuePair<string, AnimationClip>("V02", clipV02),
            };
            foreach (var pose in poses)
            {
                var go = (GameObject)UnityEngine.Object.Instantiate(model);
                go.name = outfit + "_" + pose.Key;
                go.transform.position = Vector3.zero;
                go.transform.rotation = Quaternion.identity;
                var rig = FindDeep(go.transform, "A7_ContinuousBody_Rig");
                var root = FindDeep(go.transform, "root");
                var upleg = FindDeep(go.transform, "upperleg01.L");
                var bones = rig.GetComponentsInChildren<Transform>(true).Where(t => t != rig).ToArray();
                float heldDelta = 0f;
                if (pose.Value != null)
                {
                    pose.Value.SampleAnimation(go, pose.Value.length);
                    var end = bones.Select(b => b.position).ToArray();
                    pose.Value.SampleAnimation(go, 0f);
                    for (int i = 0; i < bones.Length; i++) heldDelta = Mathf.Max(heldDelta, (bones[i].position - end[i]).magnitude);
                }
                var tag = outfit + "_" + pose.Key;
                sb.Append(firstPose ? "\n " : ",\n ");
                firstPose = false;
                sb.Append(Q(tag) + ":{\"clip\":" + Q(pose.Value != null ? pose.Value.name : "") + ",\"bone_count\":" + bones.Length
                    + ",\"rig_localScale\":" + V(rig.localScale) + ",\"rig_localEuler\":" + V(rig.localEulerAngles)
                    + ",\"rig_localPosition\":" + V(rig.localPosition) + ",\"root_localPosition\":" + V(root.localPosition)
                    + ",\"upperleg01_L_localPosition\":" + V(upleg.localPosition) + ",\"root_lossyScale\":" + V(root.lossyScale)
                    + ",\"held_pose_max_joint_delta_first_vs_last_frame_m\":" + F(heldDelta));
                // joints
                var bj = new StringBuilder("{");
                bj.Append(string.Join(",", bones.Select(b => Q(b.name) + ":" + V(b.position))));
                File.WriteAllText(Path.Combine(outDir, "bones", tag + ".json"), bj.Append("}").ToString());
                // skins
                sb.Append(",\"skins\":{");
                bool firstSkin = true;
                foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderBy(r => r.name))
                {
                    var baked = new Mesh();
                    smr.BakeMesh(baked, true);
                    baked.RecalculateBounds();
                    var local = baked.vertices;
                    var m = smr.transform.localToWorldMatrix;
                    var world = local.Select(p => m.MultiplyPoint3x4(p)).ToArray();
                    var mn = new Vector3(world.Min(p => p.x), world.Min(p => p.y), world.Min(p => p.z));
                    var mx = new Vector3(world.Max(p => p.x), world.Max(p => p.y), world.Max(p => p.z));
                    bool finite = world.All(p => !(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) || float.IsInfinity(p.x) || float.IsInfinity(p.y) || float.IsInfinity(p.z)));
                    WriteFloats(Path.Combine(outDir, "skin", tag + "_" + smr.name + ".bin"), world);
                    if (pose.Key == "Rest")
                    {
                        WriteInts(Path.Combine(outDir, "skin", outfit + "_" + smr.name + "_tris.bin"), smr.sharedMesh.triangles);
                        WriteFloats(Path.Combine(outDir, "skin", outfit + "_" + smr.name + "_bindmesh.bin"), smr.sharedMesh.vertices.Select(p => m.MultiplyPoint3x4(p)).ToArray());
                    }
                    sb.Append((firstSkin ? "" : ",") + Q(smr.name) + ":{\"vertices\":" + world.Length + ",\"finite\":" + (finite ? "true" : "false")
                        + ",\"bones\":" + smr.bones.Length + ",\"renderer_lossyScale\":" + V(smr.transform.lossyScale)
                        + ",\"baked_local_bounds_extents\":" + V(baked.bounds.extents) + ",\"baked_local_bounds_center\":" + V(baked.bounds.center)
                        + ",\"world_min\":" + V(mn) + ",\"world_max\":" + V(mx) + ",\"world_size\":" + V(mx - mn) + "}");
                    firstSkin = false;
                    UnityEngine.Object.DestroyImmediate(baked);
                }
                sb.Append("}");
                // captures: the same eye as the Blender evidence (1.65 m, 80 deg down, vertical FOV 68, 16:9, near 0.05)
                bool capture = pose.Key == "V02" || (outfit == "WORK" && (pose.Key == "StandRelaxed" || pose.Key == "V01"));
                if (capture)
                {
                    Paint(go);
                    var shots = new List<string>();
                    foreach (var s in Setbacks)
                    {
                        if (pose.Key == "V01" && s < 0.3f) continue;
                        var name = outfit + "_" + pose.Key + "_setback" + Mathf.RoundToInt(s * 1000f).ToString("000", Inv);
                        cam.orthographic = false;
                        cam.fieldOfView = 68f;
                        cam.nearClipPlane = 0.05f;
                        cam.farClipPlane = 200f;
                        cam.transform.position = new Vector3(0f, 1.65f, s);
                        cam.transform.rotation = Quaternion.Euler(80f, 0f, 0f);
                        Capture(cam, name + "_eye_unity.png");
                        var helpers = SideHelpers(s);
                        cam.orthographic = true;
                        cam.orthographicSize = pose.Key == "V01" ? 40f : 1.097f;
                        cam.nearClipPlane = 0.05f;
                        cam.transform.position = pose.Key == "V01" ? new Vector3(-100f, 0f, 0f) : new Vector3(-3f, 0.95f, 0.35f);
                        cam.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                        Capture(cam, name + "_side_unity.png");
                        foreach (var h in helpers) UnityEngine.Object.DestroyImmediate(h);
                        shots.Add(Q(name));
                    }
                    sb.Append(",\"captures\":[" + string.Join(",", shots) + "]");
                }
                sb.Append("}");
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
        sb.Append("\n}\n}\n");
        File.WriteAllText(Path.Combine(outDir, "unity_check.json"), sb.ToString());
    }
}
