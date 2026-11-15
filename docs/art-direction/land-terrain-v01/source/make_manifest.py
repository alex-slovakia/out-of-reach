"""make_manifest - writes lt_manifest.json for the LAND-TERRAIN-V01 package folder: objects, bounds, triangles, materials,
the height-field description, the zones kept flat, the verification results and the SHA-256 of every file.
RESUME.md (a living checkpoint note) and the manifest itself are not hashed.

Run: python make_manifest.py <package dir>
"""
import hashlib
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.dont_write_bytecode = True
sys.path.insert(0, HERE)
import lt_height as H  # noqa: E402

PKG = os.path.abspath(sys.argv[1])
load = lambda name: json.load(open(os.path.join(PKG, name), encoding="utf-8"))


def sha(path):
    h = hashlib.sha256()
    with open(path, "rb") as fh:
        for block in iter(lambda: fh.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest()


def unity_lines(name):
    path = os.path.join(PKG, "unity_check", name)
    return [ln.rstrip("\n") for ln in open(path, encoding="utf-8")] if os.path.exists(path) else None


def unit_block(report, roundtrip, compare, fbx_unit):
    u = report["units"][fbx_unit]
    return {"fbx": u["fbx"], "objects": u["objects"], "anchors": {k: v["point"] for k, v in u["anchors"].items()},
            "triangles_total": report["triangles_total"],
            "verification": {"blender_fbx_roundtrip": {"pass": roundtrip["pass"], "tolerance_m": roundtrip["tolerance_m"],
                                                        "checks": len(roundtrip["units"][fbx_unit]["checks"]),
                                                        "failed": len(roundtrip["failures"]),
                                                        "blender": roundtrip["blender"]},
                             "unity_6000_3_28f1_import": compare}}


def main():
    mats = load("source/lt_materials.json")["materials"]
    terrain = unit_block(load("lt_build_report.json"), load("lt_build_roundtrip.json"), unity_lines("lt_compare.txt"),
                         "LT_LandTerrain")
    terrain["verification"]["footprint_boundary_and_seam"] = load("lt_boundary_check.json")
    terrain["kit_info"] = {k: {kk: vv for kk, vv in v.items() if kk != "cluster_centres_xz"}
                           for k, v in load("lt_build_report.json")["kit_info"].items()}
    man = {
        "task": "LAND-TERRAIN-01",
        "status": "original art package for root's native check; not imported into the game scene by this package",
        "space": "Unity metres, y up, local to the prologue origin; every object's pivot is the prologue origin. FBX "
                 "exported like the ship kit: bake_space_transform, -Z forward, Y up, FBX_SCALE_ALL. Import with Bake "
                 "Axis Conversion ON (bakeAxisConversion 1, useFileUnits 1, useFileScale 1, globalScale 1, no "
                 "cameras or lights); with it off everything lands at (-x, y, -z).",
        "terrain": terrain,
        "materials": mats,
        "textures": {"LT_Terrain_Macro.png": "2048 x 2048 sRGB, UV0, Clamp", "LT_TerrainDetail_Albedo.png":
                     "1024 x 1024 sRGB, tileable 5 m, URP Detail Albedo x2 (neutral mean linear 0.5)",
                     "LT_TerrainDetail_Normal.png": "1024 x 1024 normal map, OpenGL (+Y), tileable 5 m",
                     "LT_TerrainTile_BaseColor.png": "1024 x 1024 sRGB, tileable 16 m, fallback material only"},
        "height_field": H.describe(),
        "kept_flat": {"footprint": {"x": [-72, 72], "z": [-62, 72], "edge_y": H.DATUM},
                      "crawlerway_facility_corridor": {"x": [-55, 95], "z": [-1020, -50]},
                      "plume_outlet_corridor": {"x": [-35, 35], "z": [60, 320]},
                      "h_zero_within_m_of_every_edge": H.FLAT, "blend_margin_m": H.BLEND,
                      "rocks_clear_of_zones_m": "10 + rock size; also 6 m off the fence line, 9 m off the pole line, 7 m off the ditches"},
        "boundary_vertices_xz": [list(p) for p in load("lt_boundary_check.json")["listed_boundary_vertices_xz"]],
        "budgets": {"triangles_total": terrain["triangles_total"], "triangle_limit": 80000,
                    "textures": "one 2048^2 and three 1024^2 (one of them fallback only)"},
        "excluded": "no lights, cameras, colliders, water or mountains; PV_* preview objects (LAND kit, vehicle and mast "
                    "proxies, sun, cameras) exist only in the .blend",
        "provenance": {"author": "Claude Opus 5.5 (AI) for Apoliak7777's team", "ai_generated": True,
                       "method": "procedural Python (NumPy, Pillow, Blender 5.3 bpy); fixed seeds", "third_party": "none",
                       "branding": "none"},
    }
    if os.path.exists(os.path.join(PKG, "lss_v02_build_report.json")):
        man["surroundings_v02"] = unit_block(load("lss_v02_build_report.json"), load("lss_v02_build_roundtrip.json"),
                                             unity_lines("lss_v02_compare.txt"), "LT_Surroundings_v02")
        man["surroundings_v02"]["seating"] = load("lss_v02_build_report.json").get("seating")
    files = {}
    for root, _, names in os.walk(PKG):
        for n in sorted(names):
            rel = os.path.relpath(os.path.join(root, n), PKG).replace(os.sep, "/")
            if rel in ("lt_manifest.json", "RESUME.md") or "__pycache__" in rel:
                continue
            files[rel] = {"sha256": sha(os.path.join(root, n)), "bytes": os.path.getsize(os.path.join(root, n))}
    man["files"] = dict(sorted(files.items()))
    with open(os.path.join(PKG, "lt_manifest.json"), "w", encoding="utf-8", newline="\n") as fh:
        json.dump(man, fh, indent=1, ensure_ascii=False)
        fh.write("\n")
    print("manifest:", len(files), "files hashed")


if __name__ == "__main__":
    main()
