"""quick_geo - WORK-HAND-CLASP-01 diagnostic: where the solved pinch's joints sit in the pouch frame (x along the
distal fingers, y index side, z towards the thumb; origin at the finger pads). Prints only.
Run: blender -b --factory-startup --python quick_geo.py -- --fbx <fbx> --gc <dir> --out <dir>
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import clasp_solve as cs  # noqa: E402

body = cs.Body()
for side in ("R",):
    hand = cs.Hand(body, side)
    q, err = hand.solve(0.025 + 2 * cs.EPS)
    d = hand.deltas(q, {"ring": 0.0, "little": 0.0})
    x_c, y_c, z_c, F, tp, edge = hand.frame(d)
    s = "." + side
    names = ["wrist", "finger1-1", "finger1-2", "finger1-3", "finger2-1", "finger2-2", "finger2-3", "finger3-1",
             "finger3-2", "finger3-3", "finger4-1", "finger5-1"]
    P = body.fk(d, [n + s for n in names])
    print("GEO side", side, "cost %.1f" % err, "q deg", np.round(np.degrees(q), 1).tolist(), "edge", edge)
    for n in names:
        m = P[n + s]
        for label, p in (("head", m[:3, 3]), ("tail", m[:3, 3] + m[:3, 1] * body.length[n + s])):
            r = p - F
            print("GEO %-10s %s  x %7.1f  y %7.1f  z %7.1f mm" % (n, label, r @ x_c * 1000, r @ y_c * 1000, r @ z_c * 1000))
    r = tp - F
    print("GEO thumb pad       x %7.1f  y %7.1f  z %7.1f mm" % (r @ x_c * 1000, r @ y_c * 1000, r @ z_c * 1000))
    # skin: where the index and middle skin crosses the pouch mid-plane (z = aperture / 2), in x and y
    body.apply(d)
    v = body.skin(side)
    reg = body.region[side]
    for digit in ("index", "middle", "thumb", "palm"):
        bones = hand.fb[digit] if digit != "palm" else ["metacarpal%d%s" % (i, s) for i in range(1, 5)] + ["wrist" + s]
        w = v[np.isin(reg, bones)] - F
        z = w @ z_c
        mid = w[np.abs(z - 0.0125) < 0.002]
        if len(mid):
            print("GEO %-7s skin at mid-plane: x %.1f..%.1f  y %.1f..%.1f mm (%d verts)" % (
                digit, (mid @ x_c).min() * 1000, (mid @ x_c).max() * 1000, (mid @ y_c).min() * 1000, (mid @ y_c).max() * 1000, len(mid)))
        print("GEO %-7s skin z range %.1f..%.1f mm" % (digit, z.min() * 1000, z.max() * 1000))
