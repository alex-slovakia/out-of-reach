"""make_manifest - WORK-HAND-CLASP-01: every delivered file with its size and SHA-256, plus the inputs.

Run: python make_manifest.py <package folder>
Text files are hashed as stored (LF line endings), so the hashes match a checkout of the repository.
"""
import hashlib
import json
import os
import sys

ROOT = os.path.abspath(sys.argv[1])
TEXT = (".md", ".json", ".py", ".txt")
files = []
for base, dirs, names in os.walk(ROOT):
    dirs[:] = sorted(d for d in dirs if d != "__pycache__")
    for name in sorted(names):
        if name == "wc_manifest.json":
            continue
        path = os.path.join(base, name)
        data = open(path, "rb").read()
        if name.endswith(TEXT):
            data = data.replace(b"\r\n", b"\n")
        files.append({"path": os.path.relpath(path, ROOT).replace(os.sep, "/"), "bytes": len(data),
                      "sha256": hashlib.sha256(data).hexdigest()})
report = json.load(open(os.path.join(ROOT, "work_hand_clasp.json"), encoding="utf-8"))
summary = {}
for name, p in report["poses"].items():
    summary[name] = {"aperture_mm": p["aperture_mm"], "thumb_opposes_bone": p["thumb_opposes_bone"],
                     "clasp_point_prop_space_unity": p["clasp_point_prop_space_unity"],
                     "intersecting_pairs": {v: r["intersecting_pairs"] for v, r in p["clearance_by_region"].items()}}
manifest = {
    "task": "WORK-HAND-CLASP-01",
    "status": "Blender study for review: finger-local clasp poses of the published WORK bare hands; not imported in Unity",
    "inputs": {
        "body": "Assets/_OutOfReach/Visuals/Character/FirstPersonBody/FitV02/Meshes/A7_BodyFit02_WORK.fbx at 85b0994 "
                "(LFS sha256 46b3051a592f2e2fe3a0e7579ad281411415a6eb71ad4546c2899e47cf5eec90), read only",
        "pouches": "feature/galley-consumables-v1@12100dd (gc_kit.py, gc_materials.json); helpers a7kit.py, hb_kit.py, "
                   "build_habitat.py, hb_materials.json from feature/habitat-details-v1@5b0f1ca, copied unchanged",
        "base": "codex/i01-integration@26d0ee0"},
    "poses": summary,
    "files": files}
with open(os.path.join(ROOT, "wc_manifest.json"), "w", encoding="utf-8", newline="\n") as h:
    json.dump(manifest, h, indent=1)
    h.write("\n")
print("manifest: %d files" % len(files))
