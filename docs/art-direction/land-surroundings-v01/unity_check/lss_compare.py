"""Compare Unity's imported bounds/anchors (ls_axis_check.tsv) with the kit manifest; find which signed axis
permutation maps the manifest onto each import variant."""
import itertools
import json
import sys

tsv, manifest_path = sys.argv[1], sys.argv[2]
man = json.load(open(manifest_path, encoding="utf-8"))
vec = lambda s: [float(c) for c in s.split(",")]
got = {}
for line in open(tsv, encoding="utf-8"):
    parts = line.rstrip("\n").split("\t")
    variant, kind = parts[0], parts[1]
    g = got.setdefault(variant, {"mesh": {}, "anchor": {}, "root": None, "tris": {}})
    if kind == "TRIS":
        g["tris"][parts[2]] = (int(parts[3]), int(parts[4]), int(parts[5]))
    elif kind == "ROOT":
        g["root"] = parts[2:]
    elif kind == "MESH":
        g["mesh"][parts[2]] = (vec(parts[3]), vec(parts[4]), parts[5], parts[6])
    else:
        g["anchor"][parts[2]] = vec(parts[3])


def apply(perm, signs, lo, hi):
    a = [signs[i] * lo[perm[i]] for i in range(3)]
    b = [signs[i] * hi[perm[i]] for i in range(3)]
    return [min(x, y) for x, y in zip(a, b)], [max(x, y) for x, y in zip(a, b)]


for variant, g in got.items():
    best = None
    for perm in itertools.permutations(range(3)):
        for signs in itertools.product((1, -1), repeat=3):
            err = 0.0
            for name, o in man["objects"].items():
                if name not in g["mesh"]:
                    err = float("inf")
                    break
                lo, hi = apply(perm, signs, o["bounds_min"], o["bounds_max"])
                ulo, uhi = g["mesh"][name][0], g["mesh"][name][1]
                err = max(err, max(abs(x - y) for x, y in zip(lo + hi, ulo + uhi)))
            for name, p in man["anchors"].items():
                if name not in g["anchor"]:
                    err = float("inf")
                    break
                q = [signs[i] * p[perm[i]] for i in range(3)]
                err = max(err, max(abs(x - y) for x, y in zip(q, g["anchor"][name])))
            if best is None or err < best[0]:
                best = (err, perm, signs)
    err, perm, signs = best
    axes = "".join(("-" if s < 0 else "+") + "xyz"[p] for p, s in zip(perm, signs))
    ident = best[1] == (0, 1, 2) and best[2] == (1, 1, 1)
    print("%s: root %s | meshes %d anchors %d | best map Unity=(%s) max err %.4f m%s" % (
        variant, g["root"], len(g["mesh"]), len(g["anchor"]), axes, err, "  <- identity, matches the manifest" if ident and err < 0.002 else ""))
    for name in ("LS_AssemblyBuilding", "LS_FlameTrench"):
        if name in g["mesh"]:
            print("   %-20s Unity bounds %s .. %s, rot %s, scale %s" % ((name,) + tuple(g["mesh"][name])))
    for name, o in man["objects"].items():
        if name in g["tris"] and g["tris"][name][0] != o["triangles"]:
            print("   triangles %-20s manifest %5d, Unity %5d (%+d); Unity zero-area left: %d" % (
                name, o["triangles"], g["tris"][name][0], g["tris"][name][0] - o["triangles"], g["tris"][name][2]))
    if g["tris"]:
        print("   triangles total: manifest %d, Unity %d" % (man["triangles_total"], sum(v[0] for v in g["tris"].values())))
    if "LS_TrenchExit_Ground" in g["anchor"]:
        print("   LS_TrenchExit_Ground Unity %s (manifest %s)" % (g["anchor"]["LS_TrenchExit_Ground"], man["anchors"]["LS_TrenchExit_Ground"]))
