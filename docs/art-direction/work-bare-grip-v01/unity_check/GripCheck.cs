using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// WORK-BARE-GRIP-01: imports both grip clips with V04's importer settings (Generic, preserveHierarchy, compression
// off, globalScale 1, useFileScale, no bake axis conversion) and logs the rig scale, the animated bone paths, the
// clip length and the closed-frame bone positions in model space.
public static class GripCheck
{
    static string F(Vector3 v)
    {
        return string.Format(CultureInfo.InvariantCulture, "{0:F6},{1:F6},{2:F6}", v.x, v.y, v.z);
    }

    public static void Run()
    {
        var sb = new StringBuilder();
        foreach (var side in new[] { "R", "L" })
        {
            string path = "Assets/Grip/BODY_WORK_GripRail_" + side + "_V01.fbx";
            var imp = (ModelImporter)AssetImporter.GetAtPath(path);
            imp.animationType = ModelImporterAnimationType.Generic;
            imp.preserveHierarchy = true;
            imp.animationCompression = ModelImporterAnimationCompression.Off;
            imp.globalScale = 1f;
            imp.useFileScale = true;
            imp.useFileUnits = true;
            imp.bakeAxisConversion = false;
            imp.importAnimation = true;
            imp.resampleCurves = true;
            imp.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToList();
            var go = (GameObject)Object.Instantiate(model);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            var rig = go.GetComponentsInChildren<Transform>(true).First(t => t.name == "A7_ContinuousBody_Rig");
            sb.AppendLine(side + "\tRIG\t" + F(rig.localScale) + "\tclips=" + clips.Count);
            foreach (var clip in clips)
            {
                var bindings = AnimationUtility.GetCurveBindings(clip);
                var paths = new HashSet<string>(bindings.Select(b => b.path));
                var leafs = paths.Select(p => p.Substring(p.LastIndexOf('/') + 1)).OrderBy(x => x).ToList();
                sb.AppendLine(side + "\tCLIP\t" + clip.name + "\t" + clip.length.ToString(CultureInfo.InvariantCulture) + "\t" +
                              clip.frameRate.ToString(CultureInfo.InvariantCulture) + "\t" + bindings.Length + "\t" + string.Join(",", leafs) + "\t" +
                              string.Join(",", bindings.Select(b => b.propertyName.Split('.')[0]).Distinct()));
                clip.SampleAnimation(go, clip.length);
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith("finger") || t.name.StartsWith("wrist"))
                        sb.AppendLine(side + "\tPOS\t" + t.name + "\t" + F(go.transform.InverseTransformPoint(t.position)));
            }
            Object.DestroyImmediate(go);
        }
        File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "grip_check.tsv"), sb.ToString());
        Debug.Log("GRIP_CHECK_DONE");
        EditorApplication.Exit(0);
    }
}
