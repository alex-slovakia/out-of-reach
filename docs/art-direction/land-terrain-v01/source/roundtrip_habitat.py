"""roundtrip_habitat - re-imports every unit FBX into an empty Blender scene and checks it against the build report.

Per object: name, triangle count, pivot and bounds (in Unity space, 1 mm tolerance) and material slot names; per
anchor: name and position. Writes <out>/hb_roundtrip.json and exits with 1 on any mismatch.

Run: blender -b --factory-startup --python roundtrip_habitat.py -- --out <out dir> [--report <name>_report.json]
"""
import json
import os
import re
import sys

import bpy

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = os.path.abspath(ARGS[ARGS.index("--out") + 1])
REPORT = ARGS[ARGS.index("--report") + 1] if "--report" in ARGS else "hb_build_report.json"
RESULT = REPORT.replace("_report.json", "_roundtrip.json") if "--report" in ARGS else "hb_roundtrip.json"
TOL = 0.001


def unity(v):
    return [v[0], v[2], v[1]]


def base(name):
    return re.sub(r"\.\d{3}$", "", name)


def close(a, b):
    return all(abs(x - y) <= TOL for x, y in zip(a, b))


report = json.load(open(os.path.join(OUT, REPORT), encoding="utf-8"))
results, failures = {}, []
for unit_name, info in report["units"].items():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(OUT, info["fbx"]), bake_space_transform=True)
    got = {}
    for obj in bpy.data.objects:
        if obj.type == "MESH":
            mw = obj.matrix_world
            pts = [unity(mw @ v.co) for v in obj.data.vertices]
            got[base(obj.name)] = {
                "type": "MESH",
                "triangles": sum(len(p.vertices) - 2 for p in obj.data.polygons),
                "origin": unity(mw.translation),
                "bounds_min": [min(p[i] for p in pts) for i in range(3)],
                "bounds_max": [max(p[i] for p in pts) for i in range(3)],
                "materials": sorted(base(slot.material.name) for slot in obj.material_slots if slot.material),
            }
        elif obj.type == "EMPTY":
            got[base(obj.name)] = {"type": "EMPTY", "point": unity(obj.matrix_world.translation)}
        else:
            failures.append("%s: unexpected %s object %s in the FBX" % (unit_name, obj.type, obj.name))
    checks = []
    for name, want in info["objects"].items():
        have = got.get(name)
        ok = (have is not None and have["type"] == "MESH" and have["triangles"] == want["triangles"]
              and close(have["origin"], want["origin"]) and close(have["bounds_min"], want["bounds_min"])
              and close(have["bounds_max"], want["bounds_max"]) and have["materials"] == sorted(want["per_material"]))
        checks.append({"object": name, "ok": ok, "triangles": have and have["triangles"], "expected": want["triangles"]})
        if not ok:
            failures.append("%s/%s: %s vs %s" % (unit_name, name, have, want))
    for name, want in info["anchors"].items():
        have = got.get(name)
        ok = have is not None and have["type"] == "EMPTY" and close(have["point"], want["point"])
        checks.append({"anchor": name, "ok": ok, "point": have and [round(c, 4) for c in have["point"]]})
        if not ok:
            failures.append("%s/%s: %s vs %s" % (unit_name, name, have, want))
    extra = sorted(set(got) - set(info["objects"]) - set(info["anchors"]))
    if extra:
        failures.append("%s: unexpected objects %s" % (unit_name, extra))
    results[unit_name] = {"fbx": info["fbx"], "objects_in_file": len(got), "checks": checks}
    print("[roundtrip] %-16s %d objects, %d checks, %d failed" % (unit_name, len(got), len(checks),
                                                                  sum(not c["ok"] for c in checks)), flush=True)
summary = {"pass": not failures, "tolerance_m": TOL, "units": results, "failures": failures,
           "blender": bpy.app.version_string}
with open(os.path.join(OUT, RESULT), "w", encoding="utf-8") as handle:
    json.dump(summary, handle, indent=1)
print("[roundtrip] %s" % ("PASS" if not failures else "FAIL: " + "; ".join(failures[:5])), flush=True)
sys.exit(0 if not failures else 1)
