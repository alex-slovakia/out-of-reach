"""ls_tri_match - matches every triangle Unity imported for LS_Umbilicals and LS_WaterTower (ls_tri_detail.tsv,
written by LSTriangleDetail.cs with the ship importer settings) against the source triangles from ls_kit.

Source triangles under 1 mm2 are the lathe's pole slivers (the a7kit sphere closes each pole on a 1 um ring). The
check passes when Unity's triangles equal the source minus those slivers, one to one.

Run: python ls_tri_match.py <package source dir> <ls_tri_detail.tsv> <out json>
"""
import itertools
import json
import os
import sys
from collections import Counter

import numpy as np

SRC, TSV, OUT = (os.path.abspath(a) for a in sys.argv[1:4])
sys.path.insert(0, SRC)
import ls_kit  # noqa: E402

atlas = json.load(open(os.path.join(SRC, "..", "textures", "ls_markings.json"), encoding="utf-8"))
models = {m.name: m for m in ls_kit.build_kit(atlas)[0]}
unity = {"LS_Umbilicals": [], "LS_WaterTower": []}
for line in open(TSV, encoding="utf-8"):
    p = line.rstrip("\r\n").split("\t")
    if p[1] == "TRI":
        unity[p[2]].append(np.array([[float(c) for c in q.split(",")] for q in p[3:6]]))


def key(tri, q=1e-3):
    return tuple(sorted(tuple(int(round(c / q)) for c in v) for v in tri))


def area(x):
    return 0.5 * float(np.linalg.norm(np.cross(x[1] - x[0], x[2] - x[0])))


report = {}
for name, utris in unity.items():
    m = models[name]
    kept, sliver = [], []
    for mat in sorted(m.parts):
        v, t, _ = m.arrays(mat)
        for tri in t:
            (sliver if area(v[tri]) < 1e-6 else kept).append(v[tri])
    cs, cu = Counter(key(x) for x in kept), Counter(key(x) for x in utris)
    only_s, only_u = cs - cu, cu - cs
    left_s = [x for x in kept if key(x) in only_s]
    left_u = [x for x in utris if key(x) in only_u]
    used, worst, unmatched = set(), 0.0, 0
    for a in left_u:                      # rounding-boundary leftovers: match within 2 mm, any vertex order
        best = None
        for j, b in enumerate(left_s):
            if j in used:
                continue
            for perm in itertools.permutations(range(3)):
                d = float(np.abs(a - b[list(perm)]).max())
                if best is None or d < best[0]:
                    best = (d, j)
        if best and best[0] < 0.002:
            used.add(best[1])
            worst = max(worst, best[0])
        else:
            unmatched += 1
    report[name] = {
        "source_triangles": len(kept) + len(sliver), "pole_slivers": len(sliver), "source_minus_slivers": len(kept),
        "unity_triangles": len(utris), "matched_exact_1mm": sum((cs & cu).values()), "matched_within_2mm": len(used),
        "max_deviation_m": worst, "unmatched_unity": unmatched, "unmatched_source": len(left_s) - len(used),
        "sliver_area_max_m2": max(area(x) for x in sliver),
        "sliver_shortest_edge_max_m": max(min(float(np.linalg.norm(x[i] - x[(i + 1) % 3])) for i in range(3)) for x in sliver),
        "pass": len(utris) == len(kept) and unmatched == 0 and len(left_s) == len(used)}
    print(name, report[name])
json.dump(report, open(OUT, "w", encoding="utf-8", newline="\n"), indent=1)
