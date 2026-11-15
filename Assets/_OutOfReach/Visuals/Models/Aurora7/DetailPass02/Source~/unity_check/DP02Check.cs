using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Imports every delivered DP02_*.fbx under Assets/DP02 with its delivered .meta (the importer settings are only
// read, never changed) and writes the importer settings, each renderer's world bounds, triangle and sub-mesh counts,
// material slot names and each anchor's world position to dp02_unity_check.tsv in the project folder.
public static class DP02Check
{
    static string F(Vector3 v)
    {
        return string.Format(CultureInfo.InvariantCulture, "{0:F4},{1:F4},{2:F4}", v.x, v.y, v.z);
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
                                  mesh.subMeshCount + "\t" + names);
                }
                else
                    sb.AppendLine(file + "\tANCHOR\t" + t.name + "\t" + F(t.position) + "\t" + F(t.localEulerAngles));
            }
            Object.DestroyImmediate(go);
        }
        File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "dp02_unity_check.tsv"), sb.ToString());
        Debug.Log("DP02_CHECK_DONE");
        EditorApplication.Exit(0);
    }
}
