using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class SuitV03Check
{
    const string V03 = "Assets/ReferenceSuitsV03/Meshes/";
    const string FIT = "Assets/FitV02/Meshes/";

    static Dictionary<string, Transform> Map(Transform root)
    {
        var d = new Dictionary<string, Transform>();
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (!d.ContainsKey(t.name)) d[t.name] = t;
        return d;
    }

    public static void Run()
    {
        var sb = new StringBuilder();
        bool allOk = true;
        foreach (var kind in new[] { "WORK", "EVA" })
        {
            var newPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(V03 + "A7_SuitV03_" + kind + ".fbx");
            var oldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FIT + "A7_BodyFit02_" + kind + ".fbx");
            if (newPrefab == null || oldPrefab == null) { sb.AppendLine(kind + " MISSING prefab"); allOk = false; continue; }
            var n = (GameObject)PrefabUtility.InstantiatePrefab(newPrefab);
            var o = (GameObject)PrefabUtility.InstantiatePrefab(oldPrefab);
            n.transform.position = Vector3.zero; o.transform.position = Vector3.zero;
            var nm = Map(n.transform); var om = Map(o.transform);
            var nroot = nm["CharacterRoot"]; var oroot = om["CharacterRoot"];
            var bonesN = nroot.GetComponentsInChildren<Transform>(true).Length;
            var bonesO = oroot.GetComponentsInChildren<Transform>(true).Length;
            float maxPos = 0, maxRot = 0; int missing = 0, parentBad = 0; float maxScaleDev = 0;
            foreach (var kv in om)
            {
                if (kv.Key == o.name) continue;
                if (!nm.TryGetValue(kv.Key, out var t)) { if (kv.Value.GetComponentInParent<Transform>() != null && IsBone(kv.Value, oroot)) missing++; continue; }
                if (!IsBone(kv.Value, oroot)) continue;
                maxPos = Mathf.Max(maxPos, Vector3.Distance(kv.Value.position, t.position));
                maxRot = Mathf.Max(maxRot, Quaternion.Angle(kv.Value.rotation, t.rotation));
                maxScaleDev = Mathf.Max(maxScaleDev, (t.localScale - kv.Value.localScale).magnitude);
                if ((kv.Value.parent == null) != (t.parent == null) || (kv.Value.parent != null && kv.Value.parent.name != t.parent.name)) parentBad++;
            }
            sb.AppendLine($"[{kind}] bones new={bonesN} fitv02={bonesO} missingInNew={missing} parentMismatch={parentBad} maxBonePosErr_m={maxPos:G4} maxBoneRotErr_deg={maxRot:G4} maxLocalScaleDev={maxScaleDev:G4}");
            sb.AppendLine($"[{kind}] CharacterRoot localScale new={nroot.localScale} fit={oroot.localScale} lossy new={nroot.lossyScale} ; rig(armature node) lossyScale={n.transform.GetChild(0).lossyScale}");
            bool ok = bonesN == 164 + 0 || bonesN == bonesO; // transforms under CharacterRoot incl. root
            ok &= missing == 0 && parentBad == 0 && maxScaleDev < 1e-4f;
            sb.AppendLine($"[{kind}] transforms under CharacterRoot: new={bonesN} fit={bonesO}");

            // bind-pose compare by bone name (TorsoLegs vs FitV02 TorsoLegs)
            var nTL = n.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(x => x.name.EndsWith("TorsoLegs"));
            var oTL = o.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(x => x.name.EndsWith("TorsoLegs"));
            var ob = new Dictionary<string, Matrix4x4>();
            for (int i = 0; i < oTL.bones.Length; i++) ob[oTL.bones[i].name] = oTL.sharedMesh.bindposes[i];
            float maxBind = 0; int bindMissing = 0;
            for (int i = 0; i < nTL.bones.Length; i++)
            {
                if (!ob.TryGetValue(nTL.bones[i].name, out var mo)) { bindMissing++; continue; }
                var mn = nTL.sharedMesh.bindposes[i];
                for (int k = 0; k < 16; k++) maxBind = Mathf.Max(maxBind, Mathf.Abs(mn[k] - mo[k]));
            }
            sb.AppendLine($"[{kind}] BINDPOSE compare vs FitV02: bones={nTL.bones.Length} missing={bindMissing} maxElementDiff={maxBind:G4}");
            // pose transfer: copy FitV02 local pose onto the new rig, bake, compare bounds with FitV02
            foreach (var kv in om) if (nm.TryGetValue(kv.Key, out var tt) && IsBone(kv.Value, oroot)) { tt.localPosition = kv.Value.localPosition; tt.localRotation = kv.Value.localRotation; tt.localScale = kv.Value.localScale; }
            var bn = new Mesh(); nTL.BakeMesh(bn, true); var bo = new Mesh(); oTL.BakeMesh(bo, true);
            sb.AppendLine($"[{kind}] POSE-TRANSFER TorsoLegs baked bounds new center={bn.bounds.center:F3} size={bn.bounds.size:F3} | FitV02 center={bo.bounds.center:F3} size={bo.bounds.size:F3}");
            ok &= maxBind < 1e-4f && bindMissing == 0;
            long tris = 0;
            foreach (var smr in n.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = smr.sharedMesh;
                var baked = new Mesh();
                smr.BakeMesh(baked, true);
                var mats = string.Join(",", smr.sharedMaterials.Select(m => m == null ? "NULL" : m.name));
                long tr = mesh.triangles.Length / 3; tris += tr;
                sb.AppendLine($"[{kind}] SMR {smr.name} verts={mesh.vertexCount} tris={tr} bindposes={mesh.bindposes.Length} smrBones={smr.bones.Length} bounds.center={mesh.bounds.center:F3} size={mesh.bounds.size:F3} baked.size={baked.bounds.size:F3} mats=[{mats}]");
                ok &= smr.bones.Length == 164 || smr.bones.Length == bonesN - 1 || smr.bones.Length == bonesN;
                if (mesh.bounds.size.magnitude > 10f || mesh.bounds.size.magnitude < 0.05f) { ok = false; sb.AppendLine("  BAD SIZE"); }
                UnityEngine.Object.DestroyImmediate(baked);
            }
            foreach (var smr in o.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var baked = new Mesh(); smr.BakeMesh(baked, true);
                sb.AppendLine($"[{kind}] FITV02 SMR {smr.name} baked.size={baked.bounds.size:F3} center={baked.bounds.center:F3}");
                UnityEngine.Object.DestroyImmediate(baked);
            }
            sb.AppendLine($"[{kind}] TOTAL triangles (unity) = {tris} => {(ok ? "PASS" : "FAIL")}");
            allOk &= ok;
            UnityEngine.Object.DestroyImmediate(n); UnityEngine.Object.DestroyImmediate(o);
        }
        sb.AppendLine(allOk ? "RESULT PASS" : "RESULT FAIL");
        var path = Environment.GetEnvironmentVariable("SUITCHECK_OUT") ?? "suitcheck.txt";
        File.WriteAllText(path, sb.ToString());
        EditorApplication.Exit(allOk ? 0 : 1);
    }

    static bool IsBone(Transform t, Transform root)
    {
        for (var p = t; p != null; p = p.parent) if (p == root) return true;
        return false;
    }
}
