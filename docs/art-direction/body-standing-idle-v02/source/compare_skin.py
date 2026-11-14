"""compare_skin - STANDING-IDLE-UNIT-02 (Blender 5.3 Python, numpy + mathutils): Unity's baked skin against Blender's.

Inputs: the Unity check output (StandingIdleUnitCheck.cs: skin/*.bin, bones/*.json, unity_check.json) and the Blender
reference (skin_reference.py). Per outfit (WORK, EVA) and skinned mesh (TorsoLegs, Arm_L, Arm_R):
1. Map every Unity vertex to the Blender vertex at the same bind position (Unity splits vertices at seams), using the
   Unity bind mesh; every Blender vertex within 0.01 mm counts, so coincident vertices cannot fake an error.
2. Per pose, the per-vertex distance between Unity's baked vertex and Blender's evaluated vertex: max, mean, p99 and
   counts above 2 mm and 5 mm. Unity: StandRelaxed (control, the published take), V02 (the fix) and V01 (the delivered
   v01 clip, which must reproduce root's finding). Blender: the published take and the re-imported V02 file.
3. Per pose, the world bounds of every mesh against Blender's, and the lowest TorsoLegs point (feet on the deck).
4. Same eye as STANDING-IDLE-01 (1.65 m above the feet root, 80 deg down, vertical FOV 68 deg, 16:9) at the setbacks
   0.33 m and 0.197 m, on Unity's own baked TorsoLegs triangles: nearest skin to the eye and which boot vertices the eye
   sees (inside the view and not hidden by another region), counted per Blender boot vertex.
5. Unity joints (bone heads) against the V02 joint expectations.
Writes <out>/unity_skin_check.json.

Run: blender -b --factory-startup --python compare_skin.py -- --unity <unity out dir> --ref <reference dir>
     --expect <idle_joint_expectations_unity.json> --out <dir>
"""
import json
import math
import os
import struct
import sys

import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree

ARGS = sys.argv[sys.argv.index("--") + 1:]
get = lambda k: ARGS[ARGS.index(k) + 1]
UNITY, REF, EXPECT, OUT = get("--unity"), get("--ref"), get("--expect"), get("--out")
PITCH, VFOV, ASPECT = 80.0, 68.0, 16.0 / 9.0
SETBACKS = (0.33, 0.197)
BOOTS = 3


def read_vec(path):
    with open(path, "rb") as f:
        n = struct.unpack("<i", f.read(4))[0]
        return np.frombuffer(f.read(n * 12), dtype="<f4").reshape(n, 3).astype(np.float64)


def read_int(path):
    with open(path, "rb") as f:
        n = struct.unpack("<i", f.read(4))[0]
        return np.frombuffer(f.read(n * 4), dtype="<i4").reshape(-1, 3)


def stats(d):
    return {"max_mm": round(float(d.max()) * 1000, 4), "mean_mm": round(float(d.mean()) * 1000, 4),
            "p99_mm": round(float(np.percentile(d, 99)) * 1000, 4), "over_2mm": int((d > 0.002).sum()),
            "over_5mm": int((d > 0.005).sum()), "vertices": int(len(d))}


def mapping(u_bind, b_rest):
    kd = KDTree(len(b_rest))
    for i, p in enumerate(b_rest):
        kd.insert(p, i)
    kd.balance()
    cands, dist = [], []
    for p in u_bind:
        co, idx, d = kd.find(p)
        near = [j for _, j, _ in kd.find_range(p, max(d, 0.0) + 1e-5)]
        cands.append(near if near else [idx])
        dist.append(d)
    return cands, np.array(dist)


def per_vertex(u_pose, b_pose, cands):
    return np.array([min(np.linalg.norm(b_pose[c] - u, axis=1)) for u, c in zip(u_pose, cands)])


def view_basis():
    a = math.radians(PITCH)
    f = Vector((0.0, -math.sin(a), math.cos(a)))
    up = Vector((0.0, math.cos(a), math.sin(a)))
    r = f.cross(up)
    return f, up, r


def look(u_pose, tris, vreg_u, cands, boot_set, setback):
    eye = Vector((0.0, 1.65, setback))
    verts = [tuple(p) for p in u_pose]
    tree = BVHTree.FromPolygons(verts, [tuple(int(i) for i in t) for t in tris])
    nearest = tree.find_nearest(eye, 2.0)
    treg = [int(np.bincount(vreg_u[t], minlength=4).argmax()) for t in tris]
    f, up, r = view_basis()
    half_v = math.radians(VFOV) / 2
    half_h = math.atan(math.tan(half_v) * ASPECT)
    seen, inside = set(), set()
    for i, p in enumerate(u_pose):
        if vreg_u[i] != BOOTS:
            continue
        key = min(cands[i])
        if key not in boot_set:
            continue
        v = Vector(p) - eye
        z = v.dot(f)
        if z <= 0 or abs(math.atan2(v.dot(r), z)) > half_h or abs(math.atan2(v.dot(up), z)) > half_v:
            continue
        inside.add(key)
        hit = tree.ray_cast(eye, v.normalized(), v.length - 0.002)
        if hit[0] is None or treg[hit[2]] == BOOTS:
            seen.add(key)
    return {"eye_unity": [0.0, 1.65, setback], "eye_to_nearest_skin_mm": round(nearest[3] * 1000, 1),
            "boot_vertices": len(boot_set), "boots_inside_view": len(inside), "boots_visible": len(seen)}


if __name__ == "__main__":
    index = json.load(open(os.path.join(REF, "index.json")))
    ucheck = json.load(open(os.path.join(UNITY, "unity_check.json")))
    exp = json.load(open(EXPECT))["bones"]
    report = {"method": __doc__.split("\n\n")[1], "outfits": {}}
    for outfit in ("WORK", "EVA"):
        res = {"meshes": {}, "joints": {}, "lookdown": {}}
        for mesh in sorted(k for k in index[outfit] if k.startswith("A7_")):
            u_bind = read_vec(os.path.join(UNITY, "skin", "%s_%s_bindmesh.bin" % (outfit, mesh)))
            b_rest = np.load(os.path.join(REF, "%s_Rest_%s.npy" % (outfit, mesh))).astype(np.float64)
            cands, mdist = mapping(u_bind, b_rest)
            covered = len({c for cs in cands for c in cs})
            m = {"unity_vertices": int(len(u_bind)), "blender_vertices": int(len(b_rest)),
                 "bind_mapping_max_mm": round(float(mdist.max()) * 1000, 5), "blender_vertices_covered": covered}
            for pose, ref_pose in (("StandRelaxed", "StandRelaxed"), ("V02", "V02"), ("V01", "V02")):
                u = read_vec(os.path.join(UNITY, "skin", "%s_%s_%s.bin" % (outfit, pose, mesh)))
                b = np.load(os.path.join(REF, "%s_%s_%s.npy" % (outfit, ref_pose, mesh))).astype(np.float64)
                e = stats(per_vertex(u, b, cands))
                e["against_blender"] = ref_pose
                e["unity_world_min"] = u.min(axis=0).round(5).tolist()
                e["unity_world_max"] = u.max(axis=0).round(5).tolist()
                e["unity_world_size"] = (u.max(axis=0) - u.min(axis=0)).round(5).tolist()
                e["blender_world_size"] = (b.max(axis=0) - b.min(axis=0)).round(5).tolist()
                e["bounds_size_diff_mm"] = ((u.max(axis=0) - u.min(axis=0)) - (b.max(axis=0) - b.min(axis=0))).__mul__(1000).round(3).tolist()
                e["bounds_min_diff_mm"] = ((u.min(axis=0) - b.min(axis=0)) * 1000).round(3).tolist()
                e["unity_baked_local_extents"] = ucheck["poses"]["%s_%s" % (outfit, pose)]["skins"][mesh]["baked_local_bounds_extents"]
                m[pose] = e
            res["meshes"][mesh] = m
            if mesh.endswith("TorsoLegs"):
                tris = read_int(os.path.join(UNITY, "skin", "%s_%s_tris.bin" % (outfit, mesh)))
                breg = np.load(os.path.join(REF, "%s_TorsoLegs_regions.npy" % outfit))
                vreg_u = np.array([breg[min(c)] for c in cands], dtype=np.int64)
                boot_set = set(np.nonzero(breg == BOOTS)[0].tolist())
                for pose in ("StandRelaxed", "V02"):
                    u = read_vec(os.path.join(UNITY, "skin", "%s_%s_%s.bin" % (outfit, pose, mesh)))
                    res["lookdown"][pose] = {"lowest_point_y_mm": round(float(u[:, 1].min()) * 1000, 3)}
                    for s in SETBACKS:
                        res["lookdown"][pose]["setback%03d" % round(s * 1000)] = look(u, tris, vreg_u, cands, boot_set, s)
        for pose in ("V02", "V01", "StandRelaxed"):
            ub = json.load(open(os.path.join(UNITY, "bones", "%s_%s.json" % (outfit, pose))))
            if pose == "StandRelaxed":
                bb = json.load(open(os.path.join(REF, "%s_StandRelaxed_bones.json" % outfit)))
                ref = {n: v for n, v in bb.items()}
            else:
                ref = {n: e["head"] for n, e in exp.items()}
            d = {n: (Vector(ub[n]) - Vector(ref[n])).length for n in ref}
            worst = max(d, key=d.get)
            res["joints"][pose] = {"against": "Blender StandRelaxed" if pose == "StandRelaxed" else "V02 joint expectations",
                                   "bones": len(d), "max_mm": round(d[worst] * 1000, 4), "worst_bone": worst,
                                   "within_2mm": all(x <= 0.002 for x in d.values())}
        for pose in ("StandRelaxed", "V04Float", "V01", "V02"):
            p = ucheck["poses"]["%s_%s" % (outfit, pose)]
            res.setdefault("unity_rig", {})[pose] = {k: p[k] for k in (
                "rig_localScale", "rig_localEuler", "root_localPosition", "upperleg01_L_localPosition", "root_lossyScale",
                "held_pose_max_joint_delta_first_vs_last_frame_m")}
        sr, v2 = "StandRelaxed", "V02"
        res["checks"] = {
            "rig_localScale_one_and_root_in_metres": res["unity_rig"][v2]["rig_localScale"] == [1, 1, 1]
            and abs(res["unity_rig"][v2]["root_localPosition"][1] - 0.9356452) < 1e-5,
            "every_skin_vertex_within_2mm_of_blender": all(m[v2]["over_2mm"] == 0 for m in res["meshes"].values()),
            "v02_max_error_not_above_published_take_mm": max(m[v2]["max_mm"] - m[sr]["max_mm"] for m in res["meshes"].values()) <= 0.01,
            "bounds_size_within_1mm_of_blender": all(max(abs(x) for x in m[v2]["bounds_size_diff_mm"]) <= 1.0 for m in res["meshes"].values()),
            "joints_within_2mm": res["joints"][v2]["within_2mm"],
            "feet_lowest_point_equals_published_take_within_0_1mm": abs(res["lookdown"][v2]["lowest_point_y_mm"] - res["lookdown"][sr]["lowest_point_y_mm"]) <= 0.1,
            "v01_reproduces_root_finding": res["unity_rig"]["V01"]["rig_localScale"] == [100, 100, 100]
            and all(m["V01"]["over_2mm"] == m["V01"]["vertices"] for m in res["meshes"].values()),
        }
        report["outfits"][outfit] = res
    json.dump(report, open(os.path.join(OUT, "unity_skin_check.json"), "w"), indent=1)
    for outfit, res in report["outfits"].items():
        for mesh, m in res["meshes"].items():
            print("[cmp]", outfit, mesh, "map %.5f mm" % m["bind_mapping_max_mm"],
                  " | ".join("%s max %.3f mean %.3f >2mm %d size %s" % (p, m[p]["max_mm"], m[p]["mean_mm"], m[p]["over_2mm"],
                                                                        m[p].get("unity_world_size", ""))
                             for p in ("StandRelaxed", "V02", "V01")))
        print("[cmp]", outfit, "joints", json.dumps(res["joints"]))
        print("[cmp]", outfit, "lookdown", json.dumps(res["lookdown"]))
        print("[cmp]", outfit, "checks", json.dumps(res["checks"]))
