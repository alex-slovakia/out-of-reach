"""build_surroundings_v02 - LAND-SURROUNDINGS v02 in Blender 5.3: LT_Surroundings_v02.fbx (7 static groups + 7 anchors,
v01 names with the suffix _v02), its build report with the seating report, and LT_Surroundings_v02.blend.

The kit is LAND-SURROUNDINGS v01 re-seated on the LAND-TERRAIN-V01 terrain mesh (lss_kit_v02, lt_surface). Every group
is its own mesh pivoted at the prologue origin (Unity metres, y up); anchors are empties. Materials keep the v01 names
(LSS_* maps from LAND-SURROUNDINGS v01, LS_* / A7_* from the LAND kit and the ship); the ground-breakup
hummocks use this package's LT_Terrain. The combined review renders come
from build_terrain.py --surroundings.

Run: blender -b --factory-startup --python build_surroundings_v02.py -- --out <dir> --textures <dir with LSS_, LS_,
     A7_Paint_, A7_Brushed_ maps> --lt-textures <dir with the LT_ maps>
"""
import json
import os
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.dont_write_bytecode = True
sys.path.insert(0, HERE)
import build_habitat as bh  # noqa: E402
import build_terrain as bt  # noqa: E402
import lss_kit_v02  # noqa: E402

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
get = lambda k, d=None: ARGS[ARGS.index(k) + 1] if k in ARGS else d
OUT = os.path.abspath(get("--out", "."))
TEX = os.path.abspath(get("--textures"))
LT_TEX = os.path.abspath(get("--lt-textures", os.path.join(OUT, "textures")))
FBX = "LT_Surroundings_v02.fbx"


def main():
    bh.clear_scene()
    specs = {m["name"]: m for m in json.load(open(os.path.join(HERE, "lss_materials.json"), encoding="utf-8"))["materials"]}
    materials = {n: bh.build_material(n, s, TEX) for n, s in specs.items()}
    bt.TEX = LT_TEX
    materials["LT_Terrain"] = bt.terrain_material("LT_Terrain", True)
    coll = bpy.context.scene.collection
    models, anchors = lss_kit_v02.build_kit()
    objects, report_objects = [], {}
    for model in models:
        obj, _ = bh.make_object(model.name, model, materials, coll)
        objects.append(obj)
        lo, hi = model.bounds()
        report_objects[model.name] = {
            "triangles": model.triangle_count(), "origin": [0.0, 0.0, 0.0],
            "bounds_min": [round(float(c), 5) for c in lo], "bounds_max": [round(float(c), 5) for c in hi],
            "per_material": {k: int(sum(len(t) for _, t, _ in v)) for k, v in model.parts.items()}}
    empties = [bh.make_anchor(name, p, coll) for name, p in anchors.items()]
    bh.export_fbx(objects + empties, os.path.join(OUT, FBX))
    report = {"units": {"LT_Surroundings_v02": {"fbx": FBX, "objects": report_objects,
                                                "anchors": {k: {"point": [round(float(c), 5) for c in p]} for k, p in anchors.items()}}},
              "triangles_total": sum(o["triangles"] for o in report_objects.values()),
              "seating": lss_kit_v02.seating_report()}
    json.dump(report, open(os.path.join(OUT, "lss_v02_build_report.json"), "w", encoding="utf-8", newline="\n"), indent=1)
    bh.log("exported %s: %d objects, %d anchors, %d triangles" % (FBX, len(objects), len(empties), report["triangles_total"]))
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "LT_Surroundings_v02.blend"), compress=True)
    bpy.ops.file.make_paths_relative()
    bpy.ops.wm.save_mainfile()
    bh.log("saved LT_Surroundings_v02.blend")


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    main()
