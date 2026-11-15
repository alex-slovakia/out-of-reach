// Temporary editor check (never committed into Assets). Copy to <isolated project>/Assets/_ModelCheck/Editor/, run
//   Unity.exe -batchmode -nographics -quit -projectPath <project> -executeMethod SuitV04ImportCheck.Run
// Sets the FitV02-style importer (Generic, clips BODY_*, compression Off, scale 1), creates URP Lit materials,
// remaps them, then validates rig, skins, clips and bind bounds in Unity metres.
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class SuitV04ImportCheck
{
    const string Root = "Assets/_OutOfReach/Visuals/Character/FirstPersonBody/ReferenceSuitsV04";
    const string Fbx = Root + "/Meshes/A7_SuitV04_WORK.fbx";

    public static void Run()
    {
        try { Inner(); }
        catch (Exception e) { Debug.LogError("SUITV04 FAIL " + e); EditorApplication.Exit(1); return; }
        EditorApplication.Exit(0);
    }

    static Texture2D Tex(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + n);

    static Material Mat(string name, string baseTex, Color tint, float smooth, float metal = 0f, string normal = null, bool clip = false, bool twoSided = false)
    {
        string path = Root + "/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = m == null;
        if (isNew) m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.name = name;
        m.SetColor("_BaseColor", tint);
        if (baseTex != null) m.SetTexture("_BaseMap", Tex(baseTex));
        if (normal != null) { m.SetTexture("_BumpMap", Tex(normal)); m.SetFloat("_BumpScale", 0.9f); m.EnableKeyword("_NORMALMAP"); }
        m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
        if (clip) { m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON"); m.renderQueue = 2450; m.SetOverrideTag("RenderType", "TransparentCutout"); }
        if (twoSided) m.SetFloat("_Cull", 0f);
        if (isNew) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
        return m;
    }

    static void Inner()
    {
        AssetDatabase.Refresh();
        foreach (var p in Directory.GetFiles(Root + "/Textures", "*.png"))
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(p.Replace('\\', '/'));
            bool nrm = p.Contains("Normal");
            ti.textureType = nrm ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = !nrm; ti.mipmapEnabled = true; ti.maxTextureSize = 2048;
            ti.alphaSource = (p.Contains("short02") || p.Contains("eye")) ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            ti.alphaIsTransparency = p.Contains("short02") || p.Contains("eye");
            ti.SaveAndReimport();
        }
        Directory.CreateDirectory(Root + "/Materials");
        var mats = new[]
        {
            Mat("MAT_V04_Suit", "V04_Suit_BaseColor.png", Color.white, 0.14f, 0f, "V04_Suit_Normal.png"),
            Mat("MAT_V04_TrouserTrim", null, new Color(0.149f, 0.153f, 0.165f), 0.10f),
            Mat("MAT_V04_Zip", null, new Color(0.227f, 0.239f, 0.251f), 0.65f, 0.7f),
            Mat("MAT_V04_ShirtRib", null, new Color(0.106f, 0.133f, 0.220f), 0.10f),
            Mat("MAT_V04_Shoes", "V04_Shoes_BaseColor.png", Color.white, 0.28f),
            Mat("MAT_Cabin_BareHands", "young_lightskinned_male_diffuse.png", new Color(0.92f, 0.82f, 0.75f), 0.45f),
            Mat("MAT_V04_Hair", "short02_diffuse.png", new Color(0.58f, 0.51f, 0.45f), 0.40f, 0f, null, true, true),
            Mat("MAT_V04_Eyes", "brown_eye.png", new Color(0.91f, 0.90f, 0.89f), 0.85f, 0f, null, true),
        };
        AssetDatabase.SaveAssets();

        var mi = (ModelImporter)AssetImporter.GetAtPath(Fbx);
        mi.globalScale = 1f; mi.useFileScale = true; mi.bakeAxisConversion = false; mi.preserveHierarchy = false;
        mi.animationType = ModelImporterAnimationType.Generic; mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.importAnimation = true; mi.animationCompression = ModelImporterAnimationCompression.Off;
        mi.importCameras = false; mi.importLights = false; mi.addCollider = false;
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        foreach (var m in mats) mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name), m);
        mi.SaveAndReimport();
        var clips = mi.defaultClipAnimations;
        foreach (var c in clips) c.name = c.takeName.Split('|').Last();
        mi.clipAnimations = clips;
        mi.SaveAndReimport();

        var sb = new StringBuilder();
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(go);
        bool ok = true;
        var rig = inst.transform.Find("A7_ContinuousBody_Rig");
        ok &= rig != null;
        int bones = rig ? rig.GetComponentsInChildren<Transform>(true).Length - 1 : 0;
        sb.AppendLine("rig " + (rig ? rig.name : "MISSING") + " lossyScale " + (rig ? rig.lossyScale.ToString("F4") : "-") + " bone transforms under rig " + bones);
        ok &= bones == 164 && rig.lossyScale == Vector3.one;
        foreach (var name in new[] { "A7_WORK_TorsoLegs", "A7_WORK_Arm_L", "A7_WORK_Arm_R", "A7_WORK_Head" })
        {
            var t = inst.transform.Find(name);
            var smr = t ? t.GetComponent<SkinnedMeshRenderer>() : null;
            if (!smr) { sb.AppendLine("MISSING " + name); ok = false; continue; }
            var baked = new Mesh(); smr.BakeMesh(baked, true);
            var vs = baked.vertices; Vector3 mn = Vector3.one * 1e9f, mx = -mn;
            foreach (var v in vs) { var w = smr.transform.TransformPoint(v); mn = Vector3.Min(mn, w); mx = Vector3.Max(mx, w); }
            sb.AppendLine("OK   " + name + " verts " + smr.sharedMesh.vertexCount + " tris " + smr.sharedMesh.triangles.Length / 3 + " bones " + smr.bones.Length
                + " mats " + string.Join(",", smr.sharedMaterials.Select(m => m ? m.name : "null")) + " bakedBoundsMin " + mn.ToString("F3") + " max " + mx.ToString("F3"));
            UnityEngine.Object.DestroyImmediate(baked);
        }
        var clipAssets = AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
        foreach (var want in new[] { "BODY_StandRelaxed", "BODY_FloatNeutral", "BODY_SeatedFit" })
        {
            var c = clipAssets.FirstOrDefault(x => x.name == want);
            bool good = c != null && !c.legacy && !c.humanMotion && c.length > 0f;
            ok &= good;
            sb.AppendLine((good ? "OK   clip " : "FAIL clip ") + want + (c ? " length " + c.length.ToString("F3") : ""));
        }
        sb.AppendLine(ok ? "SUITV04 IMPORT CHECK PASS" : "SUITV04 IMPORT CHECK FAIL");
        File.WriteAllText(Root + "/Source~/unity_import_check.txt", sb.ToString());
        Debug.Log(sb.ToString());
        UnityEngine.Object.DestroyImmediate(inst);
        if (!ok) throw new Exception("check failed");
    }
}
