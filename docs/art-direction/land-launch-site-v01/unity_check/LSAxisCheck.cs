using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Imports the delivered LS_LandLaunchSite.fbx twice (Bake Axis Conversion on / off, otherwise the ship's
// importer settings) and writes every renderer's world bounds and every anchor's world position.
public static class LSAxisCheck
{
    static string F(Vector3 v)
    {
        return string.Format(CultureInfo.InvariantCulture, "{0:F4},{1:F4},{2:F4}", v.x, v.y, v.z);
    }

    public static void Run()
    {
        var sb = new StringBuilder();
        foreach (var variant in new[] { "BakeOn", "BakeOff" })
        {
            string path = "Assets/LS/LS_" + variant + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.bakeAxisConversion = variant == "BakeOn";
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
            sb.AppendLine(variant + "\tROOT\t" + F(go.transform.localEulerAngles) + "\t" + F(go.transform.localScale));
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                if (t == go.transform) continue;
                var r = t.GetComponent<MeshRenderer>();
                string name = t.name;
                if (r != null)
                {
                    sb.AppendLine(variant + "\tMESH\t" + name + "\t" + F(r.bounds.min) + "\t" + F(r.bounds.max) + "\t" +
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
                    sb.AppendLine(variant + "\tTRIS\t" + name + "\t" + tris + "\t" + mesh.vertexCount + "\t" + tiny + "\t" + mesh.subMeshCount);
                }
                else
                    sb.AppendLine(variant + "\tANCHOR\t" + name + "\t" + F(t.position) + "\t" + F(t.localEulerAngles));
            }
            Object.DestroyImmediate(go);
        }
        File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "ls_axis_check.tsv"), sb.ToString());
        Debug.Log("LS_AXIS_CHECK_DONE");
        EditorApplication.Exit(0);
    }
}
