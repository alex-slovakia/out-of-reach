"""Compare Unity's import (lt_check.tsv from LTCheck.cs) with the build report: world bounds, anchors, triangle
counts, rotation/scale, and (for the terrain) the footprint-edge vertex set of LT_TerrainInner. Adapted from LAND's
ls_axis_compare.py.

Run: python lt_compare.py <lt_check.tsv> <build report json> [unit name]
"""
import json
import sys

tsv, report_path = sys.argv[1], sys.argv[2]
rep = json.load(open(report_path, encoding="utf-8"))
unit = sys.argv[3] if len(sys.argv) > 3 else next(iter(rep["units"]))
man = rep["units"][unit]
vec = lambda s: [float(c) for c in s.split(",")]
mesh, anchor, tris, edge, root = {}, {}, {}, None, None
for line in open(tsv, encoding="utf-8"):
    p = line.rstrip("\n").split("\t")
    kind = p[1]
    if kind == "ROOT":
        root = p[2:]
    elif kind == "MESH":
        mesh[p[2]] = (vec(p[3]), vec(p[4]), p[5], p[6])
    elif kind == "TRIS":
        tris[p[2]] = {"triangles": int(p[3]), "vertices": int(p[4]), "zero_area": int(p[5]), "submeshes": int(p[6]),
                      "index_format": p[7], "normals": p[8], "uv0": p[9]}
    elif kind == "ANCHOR":
        anchor[p[2]] = vec(p[3])
    elif kind == "EDGE":
        edge = {"on_edge": int(p[3]), "unique": int(p[4]), "max_abs_dy": float(p[5]), "inside": int(p[6]),
                "keys": sorted(tuple(float(c) for c in k.split(":")) for k in p[7].split(" "))}
err_b = 0.0
for name, o in man["objects"].items():
    lo, hi = mesh[name][0], mesh[name][1]
    err_b = max(err_b, max(abs(a - b) for a, b in zip(o["bounds_min"] + o["bounds_max"], lo + hi)))
err_a = max([max(abs(a - b) for a, b in zip(v["point"], anchor[k])) for k, v in man["anchors"].items()] or [0.0])
tri_ok = all(tris[k]["triangles"] == o["triangles"] for k, o in man["objects"].items())
rot_ok = all(m[2] == "0.0000,0.0000,0.0000" and m[3] == "1.0000,1.0000,1.0000" for m in mesh.values())
total_u, total_m = sum(t["triangles"] for t in tris.values()), rep["triangles_total"]
ok = err_b <= 0.002 and err_a <= 0.002 and tri_ok and rot_ok and total_u == total_m and len(mesh) == len(man["objects"])
print("root", root, "| meshes", len(mesh), "anchors", len(anchor))
print("max world-bounds deviation %.4f m, max anchor deviation %.4f m, rotation 0 / scale 1 on every node: %s" % (
    err_b, err_a, rot_ok))
for k, t in tris.items():
    print("  %-22s Unity triangles %6d (report %6d), vertices %6d, zero-area %d, submeshes %d, %s, normals %s, uv0 %s" % (
        k, t["triangles"], man["objects"][k]["triangles"], t["vertices"], t["zero_area"], t["submeshes"],
        t["index_format"], t["normals"], t["uv0"]))
print("triangles total: report %d, Unity %d" % (total_m, total_u))
if edge is not None:
    listed = sorted({(x, z) for x in (-72.0, 72.0) for z in (-62.0, -50.0, 60.0, 72.0)}
                    | {(x, -62.0) for x in (-72.0, -60.0, 60.0, 72.0)}
                    | {(x, 72.0) for x in (-72.0, -60.0, -4.0, -3.0, 3.0, 4.0, 60.0, 72.0)})
    edge_ok = edge["keys"] == listed and edge["max_abs_dy"] <= 1e-4 and edge["inside"] == 0
    ok = ok and edge_ok
    print("footprint edge: %d vertices (%d unique positions), max |y + 36.6| %.6f m, vertices inside the footprint %d, "
          "set equals the 16 listed: %s" % (edge["on_edge"], edge["unique"], edge["max_abs_dy"], edge["inside"],
                                            edge["keys"] == listed))
print("RESULT", "PASS" if ok else "FAIL")
sys.exit(0 if ok else 1)
