"""fit_check - GALLEY-USE-MOTION-01: measured glove/prop fit in the built GU_GalleyUse.blend (Blender 5.3).

Evaluates the scene at given frames and reports, per glove part and per prop material, the intersecting triangle
pairs (world-space BVH trees; back faces count) and the smallest vertex distance to each prop material, for every
visible glove part within 10 mm of a visible prop. An independent check of the keyed scene that build_motion saved.
Writes <out>/gu_fit_check.json.

Run: blender -b <out>/GU_GalleyUse.blend --python fit_check.py -- --out <out> --frames <f1,f2,...>
"""
import json
import os
import sys

import bpy
from mathutils.bvhtree import BVHTree

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = os.path.abspath(ARGS[ARGS.index("--out") + 1])
FRAMES = [int(f) for f in ARGS[ARGS.index("--frames") + 1].split(",")]


def world_tris(obj, material=None):
    """World-space vertices and triangles of an evaluated object, optionally only one material."""
    dg = bpy.context.evaluated_depsgraph_get()
    ev = obj.evaluated_get(dg)
    mesh = ev.to_mesh()
    mesh.calc_loop_triangles()
    mw = ev.matrix_world
    verts = [mw @ v.co for v in mesh.vertices]
    names = [s.material.name if s.material else "" for s in obj.material_slots]
    tris = [tuple(t.vertices) for t in mesh.loop_triangles
            if material is None or (names[t.material_index] if t.material_index < len(names) else "") == material]
    ev.to_mesh_clear()
    return verts, tris, names


def tree(verts, tris):
    used = sorted({i for t in tris for i in t})
    remap = {i: k for k, i in enumerate(used)}
    return BVHTree.FromPolygons([verts[i] for i in used], [tuple(remap[i] for i in t) for t in tris]), [verts[i] for i in used]


def visible_at(obj):
    return not obj.hide_render


def check(frame, gloves, props):
    bpy.context.scene.frame_set(frame)
    result = {"frame": frame, "parts": {}}
    prop_trees = {}
    for p in props:
        if not visible_at(p):
            continue
        _, _, names = world_tris(p)
        for mat in sorted(set(names)):
            verts, tris, _ = world_tris(p, mat)
            if tris:
                prop_trees[(p.name, mat)] = tree(verts, tris)[0]
    for g in gloves:
        if not visible_at(g):
            continue
        verts, tris, _ = world_tris(g)
        gt, gverts = tree(verts, tris)
        row = {}
        for (pname, mat), pt in prop_trees.items():
            hits = len(gt.overlap(pt))
            near = min(pt.find_nearest(v)[3] for v in gverts)
            if hits or near < 0.010:
                row[pname + "/" + mat] = {"intersecting_pairs": hits, "min_distance_mm": round(near * 1000, 1)}
        if row:
            result["parts"][g.name] = row
    result["props_visible"] = sorted({k[0] for k in prop_trees})
    return result


gloves = [o for o in bpy.data.objects if o.type == "MESH" and o.name.split(".")[0][:2] in ("R_", "L_")]
props = [o for o in bpy.data.objects if o.type == "MESH" and o.name.startswith("GC_")]
report = {"blend": os.path.basename(bpy.data.filepath), "checks": [check(f, gloves, props) for f in FRAMES]}
report["total_intersecting_pairs"] = sum(v["intersecting_pairs"] for c in report["checks"]
                                         for row in c["parts"].values() for v in row.values())
with open(os.path.join(OUT, "gu_fit_check.json"), "w", encoding="utf-8") as h:
    json.dump(report, h, indent=1)
print("FIT total intersecting pairs", report["total_intersecting_pairs"])
