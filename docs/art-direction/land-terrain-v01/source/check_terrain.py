"""check_terrain - boundary and seam check of the exported LT_LandTerrain.fbx against the LAND kit's footprint.

Imports the terrain FBX (bake_space_transform, like the round trip) and, separately, the LAND kit FBX. In Unity space:
  1. every LT_TerrainInner vertex on the footprint rectangle's edges (1 mm) is at y -36.6 (0.1 mm);
  2. those vertices are exactly the 16 listed boundary vertices, and exactly the set the LAND kit FBX has on the same
     edges at the datum (so no T-junction against the foundation);
  3. no terrain vertex and no triangle centroid lies inside the footprint (no overlap);
  4. the mesh's inner boundary loop (edges used by one triangle) is exactly the footprint perimeter: 16 edges, 556 m
     (no gap);
  5. the inner terrain's outer boundary vertices equal the far ring's inner boundary vertices (seam, 1 mm), and the
     far ring has no other open edge than its outer rim;
  6. no LT_Rocks vertex inside the footprint or either corridor; every rock's lowest vertex is below the lowest
     terrain-mesh ground under its footprint and its top above the ground at its centre (lt_surface);
     no zero-area triangle in any object.
Writes <out>/lt_boundary_check.json, exits 1 on failure.

Run: blender -b --factory-startup --python check_terrain.py -- --out <dir> --land-fbx <LS_LandLaunchSite.fbx>
"""
import json
import os
import sys
sys.dont_write_bytecode = True
from collections import Counter

import bpy
import numpy as np

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
get = lambda k, d=None: ARGS[ARGS.index(k) + 1] if k in ARGS else d
OUT = os.path.abspath(get("--out", "."))
LAND_FBX = os.path.abspath(get("--land-fbx"))
DATUM, X0, X1, Z0, Z1 = -36.6, -72.0, 72.0, -62.0, 72.0
LISTED = sorted({(x, z) for x in (-72.0, 72.0) for z in (-62.0, -50.0, 60.0, 72.0)}
                | {(x, -62.0) for x in (-72.0, -60.0, 60.0, 72.0)}
                | {(x, 72.0) for x in (-72.0, -60.0, -4.0, -3.0, 3.0, 4.0, 60.0, 72.0)})
CORRIDORS = ((-55.0, 95.0, -1020.0, -50.0), (-35.0, 35.0, 60.0, 320.0))


def load(path):
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.ops.import_scene.fbx(filepath=path, bake_space_transform=True)
    meshes = {}
    for obj in bpy.data.objects:
        if obj.type != "MESH":
            continue
        mw = np.array(obj.matrix_world)
        co = np.array([v.co for v in obj.data.vertices])
        w = co @ mw[:3, :3].T + mw[:3, 3]
        unity = w[:, [0, 2, 1]]
        tris = [tuple(p.vertices) for p in obj.data.polygons]
        meshes[obj.name.split(".")[0]] = (unity, tris)
    return meshes


def on_edge(p, tol=1e-3):
    x, z = p[:, 0], p[:, 2]
    side_x = (np.abs(np.abs(x) - 72.0) < tol) & (z >= Z0 - tol) & (z <= Z1 + tol)
    side_z = ((np.abs(z - Z0) < tol) | (np.abs(z - Z1) < tol)) & (np.abs(x) <= 72.0 + tol)
    return side_x | side_z


def boundary_edges(tris):
    count = Counter()
    for t in tris:
        for a, b in ((t[0], t[1]), (t[1], t[2]), (t[2], t[0])):
            count[(min(a, b), max(a, b))] += 1
    return [e for e, c in count.items() if c == 1]


def keyset(p):
    return sorted({(round(float(a), 3), round(float(b), 3)) for a, b in zip(p[:, 0], p[:, 2])})


def main():
    res, fails = {}, []
    terrain = load(os.path.join(OUT, "LT_LandTerrain.fbx"))
    v, tris = terrain["LT_TerrainInner"]
    edge = on_edge(v)
    pe = v[edge]
    res["inner_vertices_on_footprint_edge"] = int(edge.sum())
    res["max_abs_y_minus_datum_on_edge_m"] = float(np.abs(pe[:, 1] - DATUM).max())
    got = keyset(pe)
    res["edge_set_equals_listed_16"] = got == [(round(a, 3), round(b, 3)) for a, b in LISTED]
    inside_v = (v[:, 0] > X0 + 1e-3) & (v[:, 0] < X1 - 1e-3) & (v[:, 2] > Z0 + 1e-3) & (v[:, 2] < Z1 - 1e-3)
    t = np.asarray(tris)
    cen = v[t].mean(axis=1)
    inside_t = (cen[:, 0] > X0) & (cen[:, 0] < X1) & (cen[:, 2] > Z0) & (cen[:, 2] < Z1)
    res["inner_vertices_inside_footprint"] = int(inside_v.sum())
    res["inner_triangles_inside_footprint"] = int(inside_t.sum())
    bnd = boundary_edges(tris)
    inner_loop = [e for e in bnd if edge[e[0]] and edge[e[1]]]
    res["inner_loop_edges"] = len(inner_loop)
    res["inner_loop_length_m"] = round(float(sum(np.linalg.norm(v[a] - v[b]) for a, b in inner_loop)), 4)
    outer_ids = sorted({i for e in bnd if not (edge[e[0]] and edge[e[1]]) for i in e})
    vf, tf = terrain["LT_TerrainFar"]
    bf = boundary_edges(tf)
    rf = np.hypot(vf[:, 0], vf[:, 2])
    far_inner_ids = sorted({i for e in bf for i in e if rf[i] < 4000.0})
    far_outer_ids = sorted({i for e in bf for i in e if rf[i] >= 4000.0})
    res["inner_outer_boundary_vertices"] = len(outer_ids)
    res["far_inner_boundary_vertices"] = len(far_inner_ids)
    res["far_outer_rim_vertices"] = len(far_outer_ids)
    a, b = v[outer_ids], vf[far_inner_ids]
    if len(a) == len(b):
        dist = np.array([np.min(np.linalg.norm(b - p, axis=1)) for p in a])
        res["seam_max_distance_m"] = float(dist.max())
    else:
        res["seam_max_distance_m"] = None
    res["far_outer_rim_min_radius_m"] = round(float(rf[far_outer_ids].min()), 2)
    vr, tr = terrain["LT_Rocks"]
    in_zone = (vr[:, 0] > X0) & (vr[:, 0] < X1) & (vr[:, 2] > Z0) & (vr[:, 2] < Z1)
    for c in CORRIDORS:
        in_zone |= (vr[:, 0] > c[0]) & (vr[:, 0] < c[1]) & (vr[:, 2] > c[2]) & (vr[:, 2] < c[3])
    res["rock_vertices_in_footprint_or_corridors"] = int(in_zone.sum())
    # every rock (12 vertices each, in build order): its lowest vertex is below the lowest terrain-mesh ground under its
    # footprint (so it never floats) and its top is above the ground at its centre (so it shows)
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import lt_surface  # noqa: E402
    surf = lt_surface.surface()
    rk = vr.reshape(-1, 12, 3)
    sunk = shows = 0
    margins = []
    for rock in rk:
        cx, cz = rock[:, 0].mean(), rock[:, 2].mean()
        rad = float(np.hypot(rock[:, 0] - cx, rock[:, 2] - cz).max())
        seat = surf.seat(cx, cz, rad)
        margins.append(seat - rock[:, 1].min())
        sunk += rock[:, 1].min() < seat
        shows += rock[:, 1].max() > surf.y(cx, cz)
    res["rocks"] = {"count": int(len(rk)), "bottom_below_lowest_ground_under_footprint": int(sunk),
                    "top_above_ground_at_centre": int(shows), "min_burial_m": round(float(min(margins)), 4),
                    "max_burial_m": round(float(max(margins)), 4)}
    tiny = {}
    for name, (pv, pt) in terrain.items():
        tt = np.asarray(pt)
        area = 0.5 * np.linalg.norm(np.cross(pv[tt[:, 1]] - pv[tt[:, 0]], pv[tt[:, 2]] - pv[tt[:, 0]]), axis=1)
        tiny[name] = {"triangles": len(pt), "min_area_m2": float(area.min()), "below_1e-4_m2": int((area < 1e-4).sum())}
    res["triangle_areas"] = tiny
    land = load(LAND_FBX)
    land_pts, per_obj = [], {}
    for name, (pv, _) in land.items():
        m = on_edge(pv) & (np.abs(pv[:, 1] - DATUM) < 1e-3)
        if m.any():
            per_obj[name] = len(keyset(pv[m]))
            land_pts.append(pv[m])
    land_set = keyset(np.concatenate(land_pts))
    res["land_fbx_edge_vertices_at_datum"] = len(land_set)
    res["land_fbx_edge_vertices_by_object"] = per_obj
    res["edge_set_equals_land_fbx"] = got == land_set
    checks = {
        "edge_y_on_datum": res["max_abs_y_minus_datum_on_edge_m"] <= 1e-4,
        "edge_set_listed": res["edge_set_equals_listed_16"],
        "edge_set_land": res["edge_set_equals_land_fbx"],
        "no_overlap": res["inner_vertices_inside_footprint"] == 0 and res["inner_triangles_inside_footprint"] == 0,
        "closed_inner_loop": res["inner_loop_edges"] == 16 and abs(res["inner_loop_length_m"] - 556.0) < 1e-3,
        "seam": res["seam_max_distance_m"] is not None and res["seam_max_distance_m"] <= 1e-3,
        "far_open_edges_only_seam_and_rim": len(far_inner_ids) + len(far_outer_ids) == len(set(i for e in bf for i in e)),
        "rocks_clear": res["rock_vertices_in_footprint_or_corridors"] == 0,
        "rocks_seated": res["rocks"]["bottom_below_lowest_ground_under_footprint"] == res["rocks"]["count"]
                        and res["rocks"]["top_above_ground_at_centre"] == res["rocks"]["count"],
        "no_tiny_triangles": all(x["below_1e-4_m2"] == 0 for x in tiny.values()),
    }
    fails = [k for k, ok in checks.items() if not ok]
    summary = {"pass": not fails, "checks": checks, "results": res, "failures": fails,
               "listed_boundary_vertices_xz": LISTED, "blender": bpy.app.version_string}
    with open(os.path.join(OUT, "lt_boundary_check.json"), "w", encoding="utf-8", newline="\n") as fh:
        json.dump(summary, fh, indent=1)
    print("[boundary]", json.dumps(checks), flush=True)
    print("[boundary] %s" % ("PASS" if not fails else "FAIL: " + ", ".join(fails)), flush=True)
    sys.exit(0 if not fails else 1)


main()
