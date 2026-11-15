"""make_manifest - writes DetailPass02Layout.json (placement manifest) and SOURCE.json (file hashes) for SHIP-DETAIL-02
from the Blender build report, the clearance check and the Unity import comparison (original tooling).

Run: python -B make_manifest.py
"""
import hashlib
import json
import os
import subprocess
import sys

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
REPORTS = os.path.join(ROOT, "Source~", "reports")


def load(name):
    path = os.path.join(REPORTS, name)
    return json.load(open(path, encoding="utf-8")) if os.path.exists(path) else {}


def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for block in iter(lambda: handle.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()


def main():
    build, check, unity, roundtrip = (load("dp02_build_report.json"), load("dp02_check.json"),
                                      load("dp02_unity_compare.json"), load("dp02_build_roundtrip.json"))
    rooms = {}
    for unit_name, unit in build["units"].items():
        room = unit["room"]
        measured = check.get("rooms", {}).get(room, {})
        groups = {}
        for name, o in unit["objects"].items():
            c = measured.get(name, {})
            groups[name] = {
                "room": room, "host": o["host"], "pivot": o["origin"], "mount_normal": o["mount_out"],
                "bounds_min": o["bounds_min"], "bounds_max": o["bounds_max"], "triangles": o["triangles"],
                "materials": o["per_material"],
                "clearance": {k: c[k] for k in ("overlap_pairs", "mount_gap_m", "max_standoff_m", "min_dist_rails_m",
                                                "min_dist_controls_m", "min_dist_gameplay_parts_m",
                                                "min_dist_airlock_door_sweep_m", "min_dist_corridor_box_m",
                                                "min_dist_corridor_centreline_m", "min_dist_habitat_anchor_m",
                                                "min_dist_habitat_collider_m") if k in c}}
        rooms[room] = {"fbx": unit["fbx"], "triangles": unit["triangles"], "materials": unit["materials"],
                       "bounds_min": unit["bounds_min"], "bounds_max": unit["bounds_max"], "groups": groups,
                       "anchors": unit["anchors"],
                       "unity_import": unity.get("units", {}).get(unit_name),
                       "blender_roundtrip_pass": bool(roundtrip.get("pass")) and unit_name in roundtrip.get("units", {})}
    manifest = {
        "package": "SHIP-DETAIL-02 (AURORA-7 interior detail pass), first bounded package",
        "coordinates": "AURORA-7 ship space: Unity metres, x right, y up (deck y = 0), z along the ship (cockpit at "
                       "-z); drop each FBX at the ship origin with identity rotation and scale 1",
        "pivot_rule": "each object's pivot is the origin of its mount frame: a point on its host surface at deck "
                      "level under the cluster; mount_normal points from the host into the room",
        "importer": "Bake Axis Conversion on, file units and scale, global scale 1, no cameras or lights; materials "
                    "remapped by name onto the existing A7_*/HB_* material assets (see each FBX .meta)",
        "gameplay": "static dressing only: no colliders, no interactive parts, no new switches or anchors for "
                    "gameplay; the *_ReviewEye empties mark the preview camera",
        "clearance_definitions": check.get("definitions"),
        "airlock_door_sweep_box": check.get("door_sweep_box"), "corridor_box": check.get("corridor_box"),
        "total_triangles": sum(r["triangles"] for r in rooms.values()),
        "rooms": rooms}
    with open(os.path.join(ROOT, "DetailPass02Layout.json"), "w", encoding="utf-8", newline="\n") as handle:
        json.dump(manifest, handle, indent=1)
    try:
        commit = subprocess.run(["git", "rev-parse", "HEAD"], cwd=ROOT, capture_output=True, text=True).stdout.strip()
    except OSError:
        commit = None
    records = []
    for folder in ("Meshes", "Previews~"):
        base = os.path.join(ROOT, folder)
        for name in sorted(os.listdir(base)) if os.path.isdir(base) else []:
            if name.endswith(".meta"):
                continue
            path = os.path.join(base, name)
            records.append({"path": folder + "/" + name, "sha256": sha256(path), "bytes": os.path.getsize(path)})
    source = {"package": "SHIP-DETAIL-02", "parent_commit": commit,
              "rights": "Original procedural team work made with AI (Claude Opus 5.5) for Apoliak7777's team; see "
                        "ATTRIBUTION.md. No downloads, scans, photographs, generated images or new packages.",
              "materials": "No new materials or textures: every slot is an existing A7_* or HB_* material, remapped by "
                           "name to its existing asset GUID.",
              "generator": "Source/build_dp02.py with Source/dp02_kit.py and Source/dp02_layout.py, on top of the "
                           "AURORA-7 generator in ../Source (a7kit.py, a7_ship.py)",
              "records": records}
    with open(os.path.join(ROOT, "SOURCE.json"), "w", encoding="utf-8", newline="\n") as handle:
        json.dump(source, handle, indent=1)
    print("manifest", manifest["total_triangles"], "triangles,", len(rooms), "rooms;", len(records), "hashed files")


if __name__ == "__main__":
    main()
