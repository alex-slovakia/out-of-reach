"""Compares Unity's import of the DP02 FBX files (dp02_unity_check.tsv from DP02Check.cs) with the Blender build
report: importer settings, world bounds and pivots (1 mm), triangle counts (exact), sub-mesh count = material count,
anchor positions (1 mm), zero-area triangles (must be 0 when the TSV has the column), and lists the materials.
Prints one line per check and PASS/FAIL; exit code 1 on any mismatch.

Run: python -B dp02_unity_compare.py <dp02_unity_check.tsv> <dp02_build_report.json> [<out.json>]
"""
import json
import sys

tsv, report_path = sys.argv[1], sys.argv[2]
out_path = sys.argv[3] if len(sys.argv) > 3 else None
report = json.load(open(report_path, encoding="utf-8"))
vec = lambda s: [float(c) for c in s.split(",")]
got, importer = {}, {}
for line in open(tsv, encoding="utf-8"):
    parts = line.rstrip("\n").split("\t")
    fbx, kind = parts[0], parts[1]
    g = got.setdefault(fbx, {"mesh": {}, "anchor": {}})
    if kind == "IMPORTER":
        importer[fbx] = dict(p.split("=", 1) for p in parts[2:])
    elif kind == "MESH":
        g["mesh"][parts[2]] = {"min": vec(parts[3]), "max": vec(parts[4]), "pos": vec(parts[5]), "rot": vec(parts[6]),
                               "scale": vec(parts[7]), "tris": int(parts[8]), "submeshes": int(parts[9]),
                               "materials": [m for m in parts[10].split(";") if m],
                               "zero_area": int(parts[11].split("=", 1)[1]) if len(parts) > 11 else None}
    elif kind == "ANCHOR":
        g["anchor"][parts[2]] = vec(parts[3])
want_importer = {"bakeAxisConversion": "True", "useFileUnits": "True", "useFileScale": "True", "globalScale": "1",
                 "importCameras": "False", "importLights": "False"}
failures, lines, summary = [], [], {}
for unit_name, unit in report["units"].items():
    g = got.get(unit_name)
    if g is None:
        failures.append("%s: not imported" % unit_name)
        continue
    imp = importer.get(unit_name, {})
    bad = {k: imp.get(k) for k, v in want_importer.items() if imp.get(k) != v}
    if bad:
        failures.append("%s importer %s" % (unit_name, bad))
    err, tri_ok, unity_tris = 0.0, True, 0
    for name, o in unit["objects"].items():
        u = g["mesh"].get(name)
        if u is None:
            failures.append("%s/%s missing in Unity" % (unit_name, name))
            continue
        e = max(abs(a - b) for a, b in zip(u["min"] + u["max"] + u["pos"], o["bounds_min"] + o["bounds_max"] + o["origin"]))
        err = max(err, e)
        unity_tris += u["tris"]
        mats_ok = u["submeshes"] == len(o["per_material"]) and (not u["materials"] or "null" in u["materials"] or
                                                               sorted(u["materials"]) == sorted(o["per_material"]))
        zero_ok = u["zero_area"] in (None, 0)
        ok = e <= 0.001 and u["tris"] == o["triangles"] and mats_ok and zero_ok and max(abs(c) for c in u["rot"]) < 1e-3
        tri_ok &= u["tris"] == o["triangles"]
        lines.append("%-30s tris blender %5d unity %5d | bounds/pivot err %.5f m | submeshes %d/%d | zero-area %s | %s" % (
            name, o["triangles"], u["tris"], e, u["submeshes"], len(o["per_material"]), u["zero_area"],
            "ok" if ok else "MISMATCH"))
        if not ok:
            failures.append("%s/%s: %s vs %s" % (unit_name, name, u, {k: o[k] for k in ("triangles", "bounds_min", "bounds_max", "origin")}))
    for name, a in unit["anchors"].items():
        p = g["anchor"].get(name)
        e = max(abs(x - y) for x, y in zip(p, a["point"])) if p else float("inf")
        lines.append("%-30s anchor unity %s report %s err %.5f" % (name, p, a["point"], e))
        if e > 0.001:
            failures.append("%s/%s anchor %s vs %s" % (unit_name, name, p, a["point"]))
    extra = sorted(set(g["mesh"]) - set(unit["objects"])) + sorted(set(g["anchor"]) - set(unit["anchors"]))
    if extra:
        failures.append("%s unexpected objects %s" % (unit_name, extra))
    summary[unit_name] = {"importer": imp, "max_bounds_pivot_err_m": round(err, 6), "triangles_blender": unit["triangles"],
                          "triangles_unity": unity_tris, "objects": len(unit["objects"]), "anchors": len(unit["anchors"])}
    lines.append("%s: importer %s | max bounds/pivot error %.5f m | triangles blender %d unity %d" % (
        unit_name, {k: imp.get(k) for k in want_importer}, err, unit["triangles"], unity_tris))
for line in open(tsv, encoding="utf-8"):
    parts = line.rstrip("\n").split("\t")
    if len(parts) > 5 and parts[1] == "MATERIAL":
        lines.append("material %s | %s | %s | %s" % tuple(parts[2:6]))
print("\n".join(lines))
print("PASS" if not failures else "FAIL: " + "; ".join(failures[:6]))
if out_path:
    with open(out_path, "w", encoding="utf-8", newline="\n") as handle:
        json.dump({"pass": not failures, "units": summary, "failures": failures, "lines": lines}, handle, indent=1)
sys.exit(0 if not failures else 1)
