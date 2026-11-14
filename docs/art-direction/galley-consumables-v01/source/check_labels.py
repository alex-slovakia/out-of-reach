"""check_labels - every label of the galley props must read the right way round in Unity (none mirrored).

Compares the handedness of each label triangle's UV frame with a label built the AURORA-7 way (a front-view decal
placed with front_frame), which reads correctly in the ship's Unity captures. Plain Python.
Run: python check_labels.py <gc_labels.json>   (exit 1 if any label is mirrored)
"""
import json
import os
import sys

import numpy as np

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import a7kit  # noqa: E402
import gc_kit  # noqa: E402

atlas = json.load(open(sys.argv[1], encoding="utf-8"))


def signs(model):
    out = []
    for v, t, uv in model.parts.get(gc_kit.LABELS, []):
        for k, tri in enumerate(t):
            p, w = v[tri], uv[k]
            e1, e2 = p[1] - p[0], p[2] - p[0]
            du1, dv1 = w[1] - w[0]
            du2, dv2 = w[2] - w[0]
            det = du1 * dv2 - du2 * dv1
            if abs(det) < 1e-12:
                continue
            tangent = (e1 * dv2 - e2 * dv1) / det
            bitangent = (e2 * du1 - e1 * du2) / det
            out.append(float(np.sign(np.dot(np.cross(tangent, bitangent), np.cross(e1, e2)))))
    return out


reference = a7kit.Model("reference")
reference.decal(atlas, "fill", (0, 0, 0), None, material=gc_kit.LABELS)
placed = a7kit.Model("placed")
placed.instance(reference, gc_kit.PLACE)
good = signs(placed)[0]
total = mirrored = 0
for prop in gc_kit.build_kit(atlas).values():
    for name, model in prop["objects"].items():
        s = signs(model)
        total += len(s)
        bad = sum(1 for x in s if x != good)
        mirrored += bad
        if bad:
            print("mirrored label triangles in %s: %d" % (name, bad))
print("label triangles %d, mirrored %d" % (total, mirrored))
sys.exit(1 if mirrored else 0)
