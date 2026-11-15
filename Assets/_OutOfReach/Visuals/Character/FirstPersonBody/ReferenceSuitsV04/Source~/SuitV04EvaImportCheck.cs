// Temporary editor check (never committed into Assets). Copy to <isolated project>/Assets/_ModelCheck/Editor/, run
//   Unity.exe -batchmode -nographics -quit -projectPath <project> -executeMethod SuitV04EvaImportCheck.Run
// Sets the FitV02-style importer (Generic, clips BODY_*, compression Off, scale 1), creates URP Lit materials for the
// EVA variant (incl. transparent helmet glass and emissive display/lamps), remaps them, then validates rig, skins,
// clips and bind bounds in Unity metres.
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class SuitV04EvaImportCheck
{
    const string Root = "Assets/_OutOfReach/Visuals/Character/FirstPersonBody/ReferenceSuitsV04";
    const string Fbx = Root + "/Meshes/A7_SuitV04_EVA.fbx";

    public static void Run()
    {
        try { Inner(); }
        catch (Exception e) { Debug.LogError("SUITV04 EVA FAIL " + e); EditorApplication.Exit(1); return; }
        EditorApplication.Exit(0);
    }

    static Texture2D Tex(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + n);

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

    static Material Mat(string name, string baseTex, Color tint, float smooth, float metal = 0f, string normal = null,
        bool clip = false, bool twoSided = false, bool transparent = false, string emissionTex = null, Color? emission = null)
    {
        string path = Root + "/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = m == null;
        if (isNew) m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.name = name;
        m.SetColor("_BaseColor", tint);
        if (baseTex != null) m.SetTexture("_BaseMap", Tex(baseTex));
        if (normal != null) { m.SetTexture("_BumpMap", Tex(normal)); m.SetFloat("_BumpScale", 0.8f); m.EnableKeyword("_NORMALMAP"); }
        m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
        if (clip) { m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON"); m.renderQueue = 2450; m.SetOverrideTag("RenderType", "TransparentCutout"); }
        if (twoSided) m.SetFloat("_Cull", 0f);
        if (transparent)
        {
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f); m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission.Value);
            if (emissionTex != null) m.SetTexture("_EmissionMap", Tex(emissionTex));
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        if (isNew) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
        return m;
    }

    static void Inner()
    {
        AssetDatabase.Refresh();
        foreach (var p in Directory.GetFiles(Root + "/Textures", "V04E_*.png"))
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(p.Replace('\\', '/'));
            ti.textureType = TextureImporterType.Default; ti.sRGBTexture = true; ti.mipmapEnabled = true; ti.maxTextureSize = 2048;
            ti.alphaSource = TextureImporterAlphaSource.None; ti.alphaIsTransparency = false;
            ti.SaveAndReimport();
        }
        Directory.CreateDirectory(Root + "/Materials");
        var glass = Hex("#E4ECEF"); glass.a = 0.10f;
        var mats = new[]
        {
            Mat("MAT_V04E_Suit", "V04E_Suit_BaseColor.png", Color.white, 0.16f, 0f, "V04_Suit_Normal.png"),
            Mat("MAT_V04E_Panel", null, Hex("#CAC4B8"), 0.18f),
            Mat("MAT_V04E_Red", null, Hex("#B32A22"), 0.30f),
            Mat("MAT_V04E_Webbing", null, Hex("#A9AAA5"), 0.14f),
            Mat("MAT_V04E_Titanium", null, Hex("#A4ADB3"), 0.67f, 0.85f),
            Mat("MAT_V04E_FlexGrey", null, Hex("#6A7379"), 0.16f),
            Mat("MAT_V04E_Glove", null, Hex("#D5D2C8"), 0.20f),
            Mat("MAT_V04E_BootRubber", null, Hex("#34393E"), 0.08f),
            Mat("MAT_V04E_Hard", "V04E_Hard_BaseColor.png", Color.white, 0.45f),
            Mat("MAT_V04E_DarkPanel", null, Hex("#3A4045"), 0.50f, 0.2f),
            Mat("MAT_V04E_Display", "V04E_Display.png", Color.white, 0.75f, 0f, null, false, false, false, "V04E_Display.png", Color.white * 1.6f),
            Mat("MAT_V04E_Yellow", null, Hex("#D9A62B"), 0.60f, 0.4f),
            Mat("MAT_V04E_Glass", null, glass, 0.96f, 0f, null, false, false, true),
            Mat("MAT_V04E_Visor", null, Hex("#D7A13F"), 0.84f, 1f),
            Mat("MAT_V04E_Lamp", null, Hex("#FFF4DC"), 0.80f, 0f, null, false, false, false, null, new Color(1f, 0.93f, 0.80f) * 4f),
            Mat("MAT_V04E_Skin", "young_lightskinned_male_diffuse.png", new Color(0.90f, 0.79f, 0.72f), 0.48f),
            Mat("MAT_V04E_Hair", "short02_diffuse.png", new Color(0.40f, 0.35f, 0.33f), 0.45f, 0f, null, true, true),
            Mat("MAT_V04E_Eyes", "brown_eye.png", new Color(0.91f, 0.90f, 0.89f), 0.85f, 0f, null, true),
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
        foreach (var name in new[] { "A7_EVA_TorsoLegs", "A7_EVA_Arm_L", "A7_EVA_Arm_R", "A7_EVA_Head" })
        {
            var t = inst.transform.Find(name);
            var smr = t ? t.GetComponent<SkinnedMeshRenderer>() : null;
            if (!smr) { sb.AppendLine("MISSING " + name); ok = false; continue; }
            bool matsOk = smr.sharedMaterials.All(m => m != null && AssetDatabase.GetAssetPath(m).StartsWith(Root + "/Materials/"));
            ok &= matsOk && smr.bones.Length == 164 && smr.sharedMesh.subMeshCount == smr.sharedMaterials.Length;
            var baked = new Mesh(); smr.BakeMesh(baked, true);
            var vs = baked.vertices; Vector3 mn = Vector3.one * 1e9f, mx = -mn;
            foreach (var v in vs) { var w = smr.transform.TransformPoint(v); mn = Vector3.Min(mn, w); mx = Vector3.Max(mx, w); }
            sb.AppendLine((matsOk ? "OK   " : "FAIL ") + name + " verts " + smr.sharedMesh.vertexCount + " tris " + smr.sharedMesh.triangles.Length / 3 + " bones " + smr.bones.Length
                + " submeshes " + smr.sharedMesh.subMeshCount + " mats " + string.Join(",", smr.sharedMaterials.Select(m => m ? m.name : "null"))
                + " bakedBoundsMin " + mn.ToString("F3") + " max " + mx.ToString("F3"));
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
        sb.AppendLine(ok ? "SUITV04 EVA IMPORT CHECK PASS" : "SUITV04 EVA IMPORT CHECK FAIL");
        File.WriteAllText(Root + "/Source~/unity_import_check_eva.txt", sb.ToString());
        Debug.Log(sb.ToString());
        UnityEngine.Object.DestroyImmediate(inst);
        if (!ok) throw new Exception("check failed");
    }
}
