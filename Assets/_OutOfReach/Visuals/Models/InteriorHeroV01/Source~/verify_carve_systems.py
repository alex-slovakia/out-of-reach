"""Scratch check for ih_systems: carve the real ship meshes with the prop's boxes, add the prop, render from eye height."""
import json, math, os, sys
import bpy, bmesh
import numpy as np

W = r"C:/Users/alexk/Documents/Projects/OUT-OF-REACH-NASA-HACKATHON/.claude/worktrees/claude-max-interior-models-9d5185"
SRC = W + "/Assets/_OutOfReach/Visuals/Models/InteriorHeroV01/Source~"
A7 = W + "/Assets/_OutOfReach/Visuals/Models/Aurora7"
OUT = os.path.dirname(os.path.abspath(__file__))
extra = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
MODE = extra[0] if extra else "all"
sys.argv = sys.argv[:1] + ["--", "--render", "--no-export", "--modules", "ih_systems"]
BUILD = SRC + "/build_interior_hero.py"
src = open(BUILD).read()
assert src.rstrip().endswith("main()")
src = src.rstrip()[:-len("main()")]
g = {"__file__": BUILD, "__name__": "ihbuild"}
exec(compile(src, BUILD, "exec"), g)
g["PREVIEWS"] = OUT

bpy.ops.wm.read_factory_settings(use_empty=True)
with open(SRC + "/ih_materials.json") as f:
    specs = json.load(f)["materials"]
materials = {s["name"]: g["build_material"](s) for s in specs}
a7 = {s["name"]: s for s in json.load(open(A7 + "/Source/a7_materials.json"))["materials"]} if os.path.exists(A7 + "/Source/a7_materials.json") else {}

def unity(obj, co):
    w = obj.matrix_world @ co
    return (w.x, w.z, w.y)

for name in ("A7_Engineering", "A7_Parts", "A7_Airlock", "A7_AirlockParts"):
    bpy.ops.import_scene.fbx(filepath=A7 + "/Meshes/" + name + ".fbx")
for mat in bpy.data.materials:
    if mat.name.startswith("A7_"):
        spec = a7.get(mat.name, {})
        mat.use_nodes = True
        bsdf = next((n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
        if bsdf is not None:
            tint = spec.get("tint")
            if tint:
                bsdf.inputs["Base Color"].default_value = g["hex_linear"](tint)
            bsdf.inputs["Roughness"].default_value = 0.6
            if spec.get("emission"):
                bsdf.inputs["Emission"].default_value = g["hex_linear"](spec["emission"])
for obj in bpy.data.objects:
    if obj.type == "MESH" and obj.name in ("A7_Engineering", "A7_Airlock"):
        pts = [unity(obj, v.co) for v in list(obj.data.vertices)[::200]]
        a = np.array(pts)
        print("BOUNDS", obj.name, a.min(axis=0).round(2), a.max(axis=0).round(2), len(obj.data.polygons))

import importlib
module = importlib.import_module("ih_systems")
collection = bpy.data.collections.new("IH")
bpy.context.scene.collection.children.link(collection)
props, views = [], []
for spec in module.props():
    for box in spec.get("carve", []):
        obj = bpy.data.objects[box["target"]]
        lo, hi = np.array(box["min"]), np.array(box["max"])
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        doomed, by_mat = [], {}
        for face in bm.faces:
            inside = True
            for v in face.verts:
                p = np.array(unity(obj, v.co))
                if (p < lo).any() or (p > hi).any():
                    inside = False
                    break
            if inside:
                doomed.append(face)
                tris = len(face.verts) - 2
                key = obj.data.materials[face.material_index].name
                by_mat[key] = by_mat.get(key, 0) + tris
        total = sum(by_mat.values())
        print("CARVE", spec["name"], box["target"], box["min"], box["max"], "removed triangles", total, json.dumps(by_mat, sort_keys=True))
        if MODE != "nocarve":
            bmesh.ops.delete(bm, geom=doomed, context="FACES")
            bm.to_mesh(obj.data)
        bm.free()
    obj, stats = g["make_object"](spec["name"] + "_L0", spec["build"](0), materials, collection, spec["origin"])
    props.append(obj)
    print("PROP", spec["name"], stats["triangles"], stats["bounds_min"], stats["bounds_max"])

check = [
    ("carve_eclss_far", (-0.6, 1.6, 8.1), (1.9, 1.15, 8.1), 75, [((0.0, 2.7, 8.1), 600, 2.5), ((0.5, 1.4, 9.4), 120, 1.0)]),
    ("carve_eclss_aft", (0.3, 1.6, 10.2), (1.9, 1.1, 8.1), 70, [((0.0, 2.7, 9.2), 600, 2.5), ((0.4, 1.4, 7.0), 150, 1.0)]),
    ("carve_eclss_fwd", (0.3, 1.6, 6.0), (1.9, 1.2, 8.0), 70, [((0.0, 2.7, 7.0), 600, 2.5), ((0.4, 1.4, 9.0), 150, 1.0)]),
    ("carve_eclss_bay", (0.95, 1.6, 8.35), (1.95, 1.1, 8.0), 70, [((0.5, 2.6, 8.2), 350, 1.5)]),
    ("carve_airlock", (1.35, 1.6, 19.2), (1.55, 1.42, 20.92), 62, [((1.2, 2.6, 19.6), 350, 1.5), ((0.3, 1.6, 20.0), 80, 0.8)]),
    ("carve_airlock_side", (0.35, 1.6, 19.7), (1.6, 1.3, 20.92), 62, [((0.8, 2.6, 19.7), 350, 1.5)]),
    ("carve_airlock_low", (1.2, 1.2, 20.0), (1.45, 0.5, 20.92), 62, [((1.0, 2.2, 19.8), 300, 1.5)]),
]
if MODE == "bare":
    for obj in props:
        obj.hide_render = True
    check = [(n + "_bare", e, t, f, l) for n, e, t, f, l in check]
if MODE == "nocarve":
    for obj in props:
        obj.hide_render = True
    check = [(n + "_before", e, t, f, l) for n, e, t, f, l in check]
g["render_views"](check)
