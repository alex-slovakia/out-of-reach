using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Triangle-level evidence for the LAND count difference: per-mesh counts with welding on (ship settings) and off,
// plus every world-space triangle of LS_Umbilicals and LS_WaterTower as imported with the ship settings.
public static class LSTriangleDetail
{
    static readonly string[] Focus = { "LS_Umbilicals", "LS_WaterTower" };

    static string F(Vector3 v)
    {
        return string.Format(CultureInfo.InvariantCulture, "{0:F5},{1:F5},{2:F5}", v.x, v.y, v.z);
    }

    static GameObject Import(string path, bool weld)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        importer.bakeAxisConversion = true;
        importer.useFileUnits = true;
        importer.useFileScale = true;
        importer.globalScale = 1f;
        importer.importCameras = false;
        importer.importLights = false;
        importer.preserveHierarchy = false;
        importer.weldVertices = weld;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.SaveAndReimport();
        var go = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        go.transform.position = Vector3.zero;
        go.transform.rotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go;
    }

    public static void Run()
    {
        var sb = new StringBuilder();
        foreach (var variant in new[] { "WeldOn", "WeldOff" })
        {
            var go = Import(variant == "WeldOn" ? "Assets/LS/LS_BakeOn.fbx" : "Assets/LS/LS_WeldOff.fbx", variant == "WeldOn");
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                long tris = 0;
                for (int s = 0; s < mesh.subMeshCount; s++) tris += (long)mesh.GetIndexCount(s) / 3;
                sb.AppendLine(variant + "\tCOUNT\t" + mf.name + "\t" + tris + "\t" + mesh.vertexCount);
                if (variant != "WeldOn" || System.Array.IndexOf(Focus, mf.name) < 0) continue;
                var verts = mesh.vertices;
                var m = mf.transform.localToWorldMatrix;
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    var idx = mesh.GetTriangles(s);
                    for (int i = 0; i < idx.Length; i += 3)
                        sb.AppendLine(variant + "\tTRI\t" + mf.name + "\t" + F(m.MultiplyPoint3x4(verts[idx[i]])) + "\t" +
                                      F(m.MultiplyPoint3x4(verts[idx[i + 1]])) + "\t" + F(m.MultiplyPoint3x4(verts[idx[i + 2]])));
                }
            }
            Object.DestroyImmediate(go);
        }
        File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "ls_tri_detail.tsv"), sb.ToString());
        Debug.Log("LS_TRI_DETAIL_DONE");
        EditorApplication.Exit(0);
    }
}
