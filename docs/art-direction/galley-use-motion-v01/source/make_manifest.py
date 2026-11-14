"""make_manifest - GALLEY-USE-MOTION-01: every delivered file with its size and SHA-256, plus the inputs.

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
        if name == "gu_manifest.json":
            continue
        path = os.path.join(base, name)
        data = open(path, "rb").read()
        if name.endswith(TEXT):
            data = data.replace(b"\r\n", b"\n")
        files.append({"path": os.path.relpath(path, ROOT).replace(os.sep, "/"), "bytes": len(data),
                      "sha256": hashlib.sha256(data).hexdigest()})

clips = json.load(open(os.path.join(ROOT, "gu_motion_clips.json"), encoding="utf-8"))
fit = json.load(open(os.path.join(ROOT, "gu_fit.json"), encoding="utf-8"))
check = json.load(open(os.path.join(ROOT, "gu_fit_check.json"), encoding="utf-8"))
manifest = {
    "task": "GALLEY-USE-MOTION-01",
    "status": "authored Blender animation set for review; not integrated in Unity; no hand carries a pouch (missing "
              "Clasp pose, targets per frame); left index Press on the drink clamp proven",
    "inputs": {
        "habitat": "feature/habitat-details-v1@5b0f1ca (hb_kit.py, build_habitat.py, a7kit.py, hb_materials.json, 28 textures)",
        "consumables": "feature/galley-consumables-v1@12100dd (gc_kit.py, gc_materials.json, GC_Labels.png)",
        "gloves": "Assets/_OutOfReach/Visuals/Models/Aurora7/Props/AstronautHands (FBX, rig, base colour; same blobs at "
                  "33f474f, 4a34a6b and 85b0994, last changed in 610ed02)",
        "base": "codex/i01-integration@85b0994"},
    "clips": {k: {"frames_blender": v["frames_blender"], "duration_s": v["duration_s"], "frames": len(v["frames"])}
              for k, v in clips["clips"].items()},
    "suit_state": clips["suit_state"],
    "timing": clips["timing"],
    "missing_anchor": clips["carry"]["missing_anchor"],
    "fit": {"press_closed": fit["press_GC_DrinkPouch_Closed"], "press_open": fit["press_GC_DrinkPouch_Open"],
            "press_window_max_intersecting_pairs": fit["press_window_max_intersecting_pairs"],
            "saved_blend_check_total_pairs": check["total_intersecting_pairs"],
            "rejected_adapter_fist_pairs": sum(n for row in fit["rejected_adapter_fist"]["intersecting_pairs"].values()
                                               for n in row.values())},
    "files": files}
with open(os.path.join(ROOT, "gu_manifest.json"), "w", encoding="utf-8", newline="\n") as h:
    json.dump(manifest, h, indent=1)
    h.write("\n")
print("manifest: %d files" % len(files))
