using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Imports every delivered DP02_*.fbx under Assets/DP02 with its delivered .meta (the importer settings are only
// read, never changed) and writes the importer settings, each renderer's world bounds, triangle and sub-mesh counts,
// material slot names, zero-area triangles (area < 1e-12 m2, world space) and each anchor's world position to
// dp02_unity_check.tsv in the project folder. Materials found under Assets/DP02 (the CKP-REFINE set, if copied in)
// are listed with their shader and texture names.
public static class DP02Check
{
    static string F(Vector3 v)
    {
        return string.Format(CultureInfo.InvariantCulture, "{0:F4},{1:F4},{2:F4}", v.x, v.y, v.z);
    }

    static int ZeroArea(Mesh mesh, Transform t)
    {
        var verts = mesh.vertices;
        int zero = 0;
        for (int s = 0; s < mesh.subMeshCount; s++)
        {
            var idx = mesh.GetTriangles(s);
            for (int i = 0; i + 2 < idx.Length; i += 3)
            {
                Vector3 a = t.TransformPoint(verts[idx[i]]), b = t.TransformPoint(verts[idx[i + 1]]), c = t.TransformPoint(verts[idx[i + 2]]);
                if (Vector3.Cross(b - a, c - a).magnitude * 0.5f < 1e-12f) zero++;
            }
        }
        return zero;
    }

    public static void Run()
    {
        var sb = new StringBuilder();
        foreach (var guid in AssetDatabase.FindAssets("DP02_ t:Model", new[] { "Assets/DP02" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string file = Path.GetFileNameWithoutExtension(path);
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            sb.AppendLine(string.Join("\t", new[] { file, "IMPORTER", "guid=" + guid,
                "bakeAxisConversion=" + importer.bakeAxisConversion, "useFileUnits=" + importer.useFileUnits,
                "useFileScale=" + importer.useFileScale,
                "globalScale=" + importer.globalScale.ToString(CultureInfo.InvariantCulture),
                "importCameras=" + importer.importCameras, "importLights=" + importer.importLights,
                "materialImportMode=" + importer.materialImportMode, "preserveHierarchy=" + importer.preserveHierarchy,
                "externalMaterialMaps=" + importer.GetExternalObjectMap().Count }));
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var go = (GameObject)Object.Instantiate(asset);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            sb.AppendLine(file + "\tROOT\t" + F(go.transform.localEulerAngles) + "\t" + F(go.transform.localScale));
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                if (t == go.transform) continue;
                var r = t.GetComponent<MeshRenderer>();
                if (r != null)
                {
                    var mesh = t.GetComponent<MeshFilter>().sharedMesh;
                    long tris = 0;
                    for (int s = 0; s < mesh.subMeshCount; s++) tris += (long)mesh.GetIndexCount(s) / 3;
                    var names = new StringBuilder();
                    foreach (var m in r.sharedMaterials) names.Append(m != null ? m.name : "null").Append(';');
                    sb.AppendLine(file + "\tMESH\t" + t.name + "\t" + F(r.bounds.min) + "\t" + F(r.bounds.max) + "\t" +
                                  F(t.position) + "\t" + F(t.localEulerAngles) + "\t" + F(t.lossyScale) + "\t" + tris + "\t" +
                                  mesh.subMeshCount + "\t" + names + "\tzeroArea=" + ZeroArea(mesh, t));
                }
                else
                    sb.AppendLine(file + "\tANCHOR\t" + t.name + "\t" + F(t.position) + "\t" + F(t.localEulerAngles));
            }
            Object.DestroyImmediate(go);
        }
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/DP02" }))
        {
            string matPath = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            var tex = new StringBuilder("textures=");
            foreach (var dep in AssetDatabase.GetDependencies(matPath, false))      // serialized references, any shader
                if (dep.EndsWith(".png")) tex.Append(Path.GetFileNameWithoutExtension(dep)).Append(';');
            sb.AppendLine("DP02_MATERIALS\tMATERIAL\t" + mat.name + "\tguid=" + guid + "\tshader=" + mat.shader.name + "\t" + tex);
        }
        File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "dp02_unity_check.tsv"), sb.ToString());
        Debug.Log("DP02_CHECK_DONE");
        EditorApplication.Exit(0);
    }
}
