"""make_manifest - writes gc_manifest.json (props, variants, anchors, materials and maps) from the build report.
Plain Python. Run: python make_manifest.py <out dir>"""
import json
import os
import sys

OUT = os.path.abspath(sys.argv[1])
HERE = os.path.dirname(os.path.abspath(__file__))
report = json.load(open(os.path.join(OUT, "gc_build_report.json"), encoding="utf-8"))
roundtrip = json.load(open(os.path.join(OUT, "gc_build_roundtrip.json"), encoding="utf-8"))
specs = json.load(open(os.path.join(HERE, "gc_materials.json"), encoding="utf-8"))["materials"]
maps = set(os.listdir(os.path.join(OUT, "textures")))
materials = []
for spec in specs:
    entry = dict(spec)
    files = []
    if "set" in spec:
        files = [spec["set"] + s for s in ("_BaseColor.png", "_Normal.png", "_MODS.png")]
    if "atlas" in spec:
        files = [spec["atlas"] + ".png", "gc_labels.json"]
    missing = [f for f in files if f not in maps]
    if missing:
        raise SystemExit("material %s: missing maps %s" % (spec["name"], missing))
    entry["files"] = ["textures/" + f for f in files]
    entry["urp"] = ("URP/Lit, alpha clip 0.5" if spec.get("surface") == "cutout" else
                    "URP/Lit, transparent" if spec.get("surface") == "transparent" else "URP/Lit, opaque")
    materials.append(entry)
manifest = {
    "kit": "GALLEY-PROPS-01",
    "status": "authored Blender art kit; not integrated in Unity; no animation or gameplay",
    "coordinates": "Unity metres, y up. Label side faces +z; each prop's origin is its centre; the variants of one "
                   "prop share that origin and can be swapped in place. Blender (x, y, z) = Unity (x, z, y), exported "
                   "with bake_space_transform, axis_forward -Z, axis_up Y (the AURORA-7 pipeline).",
    "fbx_contents": "meshes and anchor empties only, flat hierarchy, no cameras, lights or texture records; "
                    "materials bind by name",
    "total_triangles": report["total_triangles"],
    "roundtrip": {"pass": roundtrip["pass"], "tolerance_m": roundtrip["tolerance_m"], "tool": "Blender " + roundtrip["blender"]},
    "props": {name: {"fbx": u["fbx"], "triangles": u["triangles"],
                     "variants": {o: {"triangles": d["triangles"], "bounds_min_m": d["bounds_min"], "bounds_max_m": d["bounds_max"],
                                      "materials": sorted(d["per_material"])} for o, d in u["objects"].items()},
                     "anchors": u["anchors"]} for name, u in report["units"].items()},
    "materials": materials,
}
with open(os.path.join(OUT, "gc_manifest.json"), "w", encoding="utf-8") as handle:
    json.dump(manifest, handle, indent=1)
print("gc_manifest.json:", len(manifest["props"]), "props,", sum(len(p["anchors"]) for p in manifest["props"].values()),
      "anchors,", len(materials), "materials,", report["total_triangles"], "triangles")
