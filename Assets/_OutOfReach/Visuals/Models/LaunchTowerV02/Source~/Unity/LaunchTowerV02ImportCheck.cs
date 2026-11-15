// Temporary editor script, never part of the game. Copy to <isolated project>/Assets/_ModelCheck/Editor/ for one run:
//   Unity.exe -batchmode -nographics -quit -projectPath <isolated> -executeMethod LaunchTowerV02ImportCheck.Run -logFile <log>
// It configures the importer (Bake Axis Conversion ON, scale 1), creates the URP Lit materials and textures settings,
// remaps the FBX materials, then validates the import in Unity metres against LaunchTowerV02_report.json.
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class LaunchTowerV02ImportCheck
{
    const string Root = "Assets/_OutOfReach/Visuals/Models/LaunchTowerV02";
    const string Fbx = Root + "/Models/LaunchTowerV02.fbx";

    [Serializable] class Item { public string name; public int tris; public int verts; public float[] origin_unity; public float[] bounds_min; public float[] bounds_max; }
    [Serializable] class Report { public Item[] items; public int total_tris; }

    public static void Run()
    {
        try { Inner(); }
        catch (Exception e) { Debug.LogError("LTV2 FAIL " + e); EditorApplication.Exit(1); return; }
        EditorApplication.Exit(0);
    }

    static Texture2D Tex(string n) { return AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + n); }

    static Material Mat(string name, string baseTex, string msTex, Color tint, float metallic, float smooth, bool cutout)
    {
        string path = Root + "/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = m == null;
        if (isNew) m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.name = name;
        m.SetColor("_BaseColor", tint);
        if (!string.IsNullOrEmpty(baseTex)) m.SetTexture("_BaseMap", Tex(baseTex));
        if (!string.IsNullOrEmpty(msTex))
        {
            m.SetTexture("_MetallicGlossMap", Tex(msTex));
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetFloat("_Smoothness", 1f);
            m.SetFloat("_Metallic", 1f);
        }
        else { m.SetFloat("_Metallic", metallic); m.SetFloat("_Smoothness", smooth); }
        m.SetFloat("_Cull", 2f);
        if (cutout)
        {
            m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "TransparentCutout"); m.renderQueue = 2450; m.SetFloat("_Cull", 0f);
            m.SetFloat("_AlphaToMask", 1f);
        }
        if (isNew) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
        return m;
    }

    static void Inner()
    {
        AssetDatabase.Refresh();
        foreach (var p in Directory.GetFiles(Root + "/Textures", "*.png"))
        {
            string path = p.Replace('\\', '/');
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            bool lin = path.Contains("MetallicSmoothness"), grat = path.Contains("Grating");
            ti.sRGBTexture = !lin; ti.mipmapEnabled = true; ti.filterMode = FilterMode.Bilinear; ti.anisoLevel = 4;
            ti.maxTextureSize = path.Contains("Labels") ? 2048 : 1024;
            ti.textureCompression = TextureImporterCompression.Compressed;
            ti.alphaSource = (grat || lin) ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            ti.alphaIsTransparency = grat;
            ti.wrapMode = path.Contains("Labels") ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            ti.SaveAndReimport();
        }
        Directory.CreateDirectory(Root + "/Materials");
        var mats = new[]
        {
            Mat("TW_SteelPainted", "TW_SteelPainted_BaseColor.png", "TW_SteelPainted_MetallicSmoothness.png", Color.white, 0, 0, false),
            Mat("TW_Galvanized", "TW_Galvanized_BaseColor.png", "TW_Galvanized_MetallicSmoothness.png", Color.white, 0, 0, false),
            Mat("TW_OchrePaint", null, null, new Color(0.60f, 0.38f, 0.07f, 1f), 0f, 0.45f, false),
            Mat("TW_HazardStripe", "TW_HazardStripe_BaseColor.png", null, Color.white, 0f, 0.4f, false),
            Mat("TW_PipeRed", "TW_PipeRed_BaseColor.png", "TW_PipeRed_MetallicSmoothness.png", Color.white, 0, 0, false),
            Mat("TW_Rubber", "TW_Rubber_BaseColor.png", null, Color.white, 0f, 0.25f, false),
            Mat("TW_Grating", "TW_Grating_BaseColor.png", null, Color.white, 0.8f, 0.45f, true),
            Mat("TW_Labels", "TW_Labels_BaseColor.png", null, Color.white, 0f, 0.35f, false),
        };
        AssetDatabase.SaveAssets();
        var mi = (ModelImporter)AssetImporter.GetAtPath(Fbx);
        mi.globalScale = 1f; mi.useFileScale = true; mi.bakeAxisConversion = true; mi.preserveHierarchy = true;
        mi.importCameras = false; mi.importLights = false; mi.importAnimation = false; mi.animationType = ModelImporterAnimationType.None;
        mi.addCollider = false; mi.isReadable = false; mi.meshCompression = ModelImporterMeshCompression.Off; mi.weldVertices = false;
        mi.importNormals = ModelImporterNormals.Import; mi.importTangents = ModelImporterTangents.None;
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        foreach (var m in mats) mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name), m);
        mi.SaveAndReimport();

        var rep = JsonUtility.FromJson<Report>(File.ReadAllText(Root + "/Models/LaunchTowerV02_report.json"));
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(go);
        var sb = new StringBuilder();
        bool ok = true;
        int tris = 0;
        sb.AppendLine("importer: bakeAxisConversion=" + mi.bakeAxisConversion + " globalScale=" + mi.globalScale + " useFileScale=" + mi.useFileScale + " preserveHierarchy=" + mi.preserveHierarchy);
        sb.AppendLine("instance scale " + inst.transform.lossyScale + " pos " + inst.transform.position + " euler " + inst.transform.eulerAngles);
        foreach (var it in rep.items)
        {
            var t = inst.transform.Find(it.name);
            if (t == null) { sb.AppendLine("MISSING " + it.name); ok = false; continue; }
            var mf = t.GetComponent<MeshFilter>(); var mr = t.GetComponent<MeshRenderer>();
            var mesh = mf.sharedMesh;
            int mt = mesh.triangles.Length / 3; tris += mt;
            var v = mesh.vertices;
            Vector3 mn = new Vector3(1e9f, 1e9f, 1e9f), mx = -mn;
            foreach (var p in v) { var w = t.TransformPoint(p); mn = Vector3.Min(mn, w); mx = Vector3.Max(mx, w); }
            float d = 0;
            for (int i = 0; i < 3; i++) d = Mathf.Max(d, Mathf.Abs(mn[i] - it.bounds_min[i]), Mathf.Abs(mx[i] - it.bounds_max[i]));
            var org = t.position;
            float od = Mathf.Max(Mathf.Abs(org.x - it.origin_unity[0]), Mathf.Abs(org.y - it.origin_unity[1]), Mathf.Abs(org.z - it.origin_unity[2]));
            bool good = d < 0.002f && od < 0.002f && t.lossyScale.x > 0.999f && t.lossyScale.x < 1.001f && mt == it.tris;
            ok &= good;
            sb.AppendLine((good ? "OK   " : "FAIL ") + it.name + " tris " + mt + "/" + it.tris + " maxBoundsDelta_m " + d.ToString("F5") + " originDelta_m " + od.ToString("F5")
                + " localEuler " + t.localEulerAngles + " lossyScale " + t.lossyScale + " submeshes " + mesh.subMeshCount + " materials " + mr.sharedMaterials.Length
                + " mats " + string.Join(",", mr.sharedMaterials.Select(m => m ? m.name : "null")));
            sb.AppendLine("     unity bounds min " + mn.ToString("F3") + " max " + mx.ToString("F3"));
        }
        sb.AppendLine("total triangles " + tris + " expected " + rep.total_tris);
        sb.AppendLine(ok ? "LTV2 IMPORT CHECK PASS" : "LTV2 IMPORT CHECK FAIL");
        File.WriteAllText(Root + "/Source~/unity_import_check.txt", sb.ToString());
        Debug.Log(sb.ToString());
        UnityEngine.Object.DestroyImmediate(inst);
        if (!ok) throw new Exception("check failed");
    }
}
