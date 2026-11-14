"""inspect_work - WORK-HAND-CLASP-01: read the published WORK body FBX (85b0994) and report what a clasp needs.

Run: blender -b --factory-startup --python inspect_work.py -- <fbx> <out json>
Reports objects, the armature's bones (parent, head, tail, length, rest matrix), the hand chains of both sides,
the skinned meshes with their vertex groups, and the actions (baked takes).
"""
import json
import sys

import bpy

ARGS = sys.argv[sys.argv.index("--") + 1:]
FBX, OUT = ARGS[0], ARGS[1]
for o in list(bpy.data.objects):
    bpy.data.objects.remove(o, do_unlink=True)
bpy.ops.import_scene.fbx(filepath=FBX)
report = {"objects": [], "bones": {}, "actions": [], "meshes": {}}
arm = None
for o in bpy.data.objects:
    report["objects"].append({"name": o.name, "type": o.type, "parent": o.parent.name if o.parent else None,
                              "matrix_world": [list(r) for r in o.matrix_world]})
    if o.type == "ARMATURE":
        arm = o
for b in arm.data.bones:
    report["bones"][b.name] = {"parent": b.parent.name if b.parent else None,
                               "head": list(b.head_local), "tail": list(b.tail_local), "length": b.length,
                               "matrix_local": [list(r) for r in b.matrix_local]}
for o in bpy.data.objects:
    if o.type == "MESH":
        mod = next((m for m in o.modifiers if m.type == "ARMATURE"), None)
        report["meshes"][o.name] = {"vertices": len(o.data.vertices), "polygons": len(o.data.polygons),
                                    "armature_modifier": bool(mod), "groups": [g.name for g in o.vertex_groups],
                                    "materials": [s.material.name if s.material else "" for s in o.material_slots]}
for a in bpy.data.actions:
    report["actions"].append({"name": a.name, "frame_range": list(a.frame_range)})
json.dump(report, open(OUT, "w", encoding="utf-8"), indent=1)
hand = [n for n in report["bones"] if any(k in n.lower() for k in ("hand", "wrist", "finger", "thumb", "index",
                                                                    "middle", "ring", "pinky", "metacarpal", "palm"))]
print("WORK objects", [(o["name"], o["type"]) for o in report["objects"]])
print("WORK bones", len(report["bones"]), "armature", arm.name)
print("WORK hand bones", hand)
print("WORK meshes", {k: (v["vertices"], v["armature_modifier"], len(v["groups"]), v["materials"]) for k, v in report["meshes"].items()})
print("WORK actions", report["actions"])
