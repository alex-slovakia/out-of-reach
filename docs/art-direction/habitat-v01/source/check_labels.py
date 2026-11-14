"""check_labels - every label of the kit must read the right way round in Unity (none mirrored).

Compares the handedness of each label quad's UV frame with a label built the ship's way (a front-view decal placed
with front_frame), which is known to read correctly in the AURORA-7 Unity captures. Plain Python.
Run: python check_labels.py <out dir>   (exit 1 if any label is mirrored)
"""
import json
import os
import sys

import numpy as np

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import a7kit  # noqa: E402
import hb_kit  # noqa: E402

atlas = json.load(open(os.path.join(sys.argv[1], "textures", "hb_labels.json"), encoding="utf-8"))


def signs(model):
    out = []
    for v, t, uv in model.parts.get(hb_kit.LABELS, []):
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
            out.append((float(np.sign(np.dot(np.cross(tangent, bitangent), np.cross(e1, e2)))), p.mean(axis=0)))
    return out


reference = a7kit.Model("reference")
reference.decal(atlas, "galley_name", (0, 0, 0), None, material=hb_kit.LABELS)
placed = a7kit.Model("placed")
placed.instance(reference, hb_kit.PLACE)
good = signs(placed)[0][0]
total = mirrored = 0
for unit in hb_kit.build_kit(atlas).values():
    for name, (model, _, _) in unit["objects"].items():
        for sign, centre in signs(model):
            total += 1
            if sign != good:
                mirrored += 1
                print("mirrored label in %s at %s" % (name, np.round(centre, 3)))
print("label triangles %d, mirrored %d" % (total, mirrored))
sys.exit(1 if mirrored else 0)
