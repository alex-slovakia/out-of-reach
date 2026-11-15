using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// LAND-TERRAIN-V01 import check (adapted from LSAxisCheck / LSSCheck): imports the delivered FBX with the ship
// importer settings (Bake Axis Conversion ON) and writes every renderer's world bounds, triangle and vertex counts,
// every anchor's world position and, for LT_TerrainInner, the vertices on the launch-foundation footprint edge.
// Usage: Unity -batchmode -nographics -projectPath <p> -executeMethod LTCheck.Run -ltFbx Assets/LT/<file>.fbx -ltOut <tsv>
public static class LTCheck
{
    static string F(Vector3 v)
    {
        return string.Format(CultureInfo.InvariantCulture, "{0:F4},{1:F4},{2:F4}", v.x, v.y, v.z);
    }

    static string Arg(string name, string fallback)
    {
        var a = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
        return fallback;
    }

    public static void Run()
    {
        string path = Arg("-ltFbx", "Assets/LT/LT_LandTerrain.fbx");
        string outFile = Arg("-ltOut", "lt_check.tsv");
        var sb = new StringBuilder();
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        importer.bakeAxisConversion = true;
        importer.useFileUnits = true;
        importer.useFileScale = true;
        importer.globalScale = 1f;
        importer.importCameras = false;
        importer.importLights = false;
        importer.preserveHierarchy = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.SaveAndReimport();
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var go = (GameObject)Object.Instantiate(asset);
        go.transform.position = Vector3.zero;
        go.transform.rotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        sb.AppendLine("BakeOn\tROOT\t" + F(go.transform.localEulerAngles) + "\t" + F(go.transform.localScale));
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
        {
            if (t == go.transform) continue;
            var r = t.GetComponent<MeshRenderer>();
            string name = t.name;
            if (r == null)
            {
                sb.AppendLine("BakeOn\tANCHOR\t" + name + "\t" + F(t.position) + "\t" + F(t.localEulerAngles));
                continue;
            }
            sb.AppendLine("BakeOn\tMESH\t" + name + "\t" + F(r.bounds.min) + "\t" + F(r.bounds.max) + "\t" +
                          F(t.localEulerAngles) + "\t" + F(t.lossyScale));
            var mesh = t.GetComponent<MeshFilter>().sharedMesh;
            long tris = 0;
            for (int s = 0; s < mesh.subMeshCount; s++) tris += (long)mesh.GetIndexCount(s) / 3;
            var verts = mesh.vertices;
            int tiny = 0;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var idx = mesh.GetTriangles(s);
                for (int i = 0; i < idx.Length; i += 3)
                {
                    float area = 0.5f * Vector3.Cross(verts[idx[i + 1]] - verts[idx[i]], verts[idx[i + 2]] - verts[idx[i]]).magnitude;
                    if (area < 1e-6f) tiny++;
                }
            }
            sb.AppendLine("BakeOn\tTRIS\t" + name + "\t" + tris + "\t" + mesh.vertexCount + "\t" + tiny + "\t" + mesh.subMeshCount +
                          "\t" + mesh.indexFormat + "\t" + (mesh.normals.Length == mesh.vertexCount) + "\t" + (mesh.uv.Length == mesh.vertexCount));
            if (name == "LT_TerrainInner")
            {
                var keys = new SortedSet<string>();
                float maxDy = 0f;
                int onEdge = 0, inside = 0;
                foreach (var lv in verts)
                {
                    var w = t.TransformPoint(lv);
                    bool sideX = Mathf.Abs(Mathf.Abs(w.x) - 72f) < 1e-3f && w.z >= -62.001f && w.z <= 72.001f;
                    bool sideZ = (Mathf.Abs(w.z + 62f) < 1e-3f || Mathf.Abs(w.z - 72f) < 1e-3f) && Mathf.Abs(w.x) <= 72.001f;
                    if (sideX || sideZ)
                    {
                        onEdge++;
                        maxDy = Mathf.Max(maxDy, Mathf.Abs(w.y + 36.6f));
                        keys.Add(string.Format(CultureInfo.InvariantCulture, "{0:F3}:{1:F3}", w.x, w.z));
                    }
                    else if (w.x > -71.999f && w.x < 71.999f && w.z > -61.999f && w.z < 71.999f) inside++;
                }
                sb.AppendLine("BakeOn\tEDGE\t" + name + "\t" + onEdge + "\t" + keys.Count + "\t" +
                              maxDy.ToString("F6", CultureInfo.InvariantCulture) + "\t" + inside + "\t" + string.Join(" ", keys));
            }
        }
        Object.DestroyImmediate(go);
        File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), outFile), sb.ToString());
        Debug.Log("LT_CHECK_DONE");
        EditorApplication.Exit(0);
    }
}
