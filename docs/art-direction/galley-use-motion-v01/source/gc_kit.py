"""gc_kit - GALLEY-PROPS-01 geometry: a meal pouch and a drink pouch for AURORA-7's galley (original work).

Shapes follow NASA's public descriptions of flight food packaging (cited in the README): thermostabilised food in a
flexible aluminium-laminate retort pouch, opened with scissors; beverages in a foil-laminate pouch with a rigid
low-density polyethylene septum adapter, a silicone septum through which water is injected and a polyethylene straw
is inserted, and a clamp that closes the straw when not in use. The tethered amber cap on the straw tip is this
kit's own addition, so the resealed straw reads at a glance.

Each pouch is a front and a back wall that bulge in the middle and meet on the mid-plane at flat heat seals; an
opened meal pouch ends its walls at the scissors cut, so the mouth is a real opening with the food inside. Labels
follow the curved wall. Each prop is designed in a front view (a7kit convention: x to the viewer's right, y up,
z towards the viewer; label side +z) around its own centre and placed with front_frame((0,0,0),(0,0,1)), so in Unity
the label faces +z and the prop's origin is its centre. Variants of one prop share that origin, so they can be
swapped in place: GC_MealPouch_Sealed / GC_MealPouch_Opened and GC_DrinkPouch_Closed / GC_DrinkPouch_Open.
Run "python gc_kit.py <gc_labels.json>" for triangle counts without Blender.
"""
import os
import sys

import numpy as np

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from a7kit import Model, front_frame, rotation  # noqa: E402

FOIL, PLASTIC, AMBER, CORD = "GC_Foil", "A7_PlasticWhite", "GC_AmberPlastic", "A7_Rubber"
SILICONE, STRAW, FOOD, LABELS = "GC_Silicone", "GC_StrawPE", "GC_Food", "GC_Labels"
PLACE = front_frame((0.0, 0.0, 0.0), (0.0, 0.0, 1.0))
BACK = rotation((0, 1, 0), 180)   # inside a front view: a label on the back face


def taper(t, p=3.0):
    """Wall bulge across the pouch: 1 in the middle, 0 at the seal."""
    return float(max(0.0, 1.0 - abs(t) ** p)) ** (1.0 / p)


def grid(points):
    rows, cols = points.shape[:2]
    v = points.reshape(-1, 3)
    t = []
    for i in range(rows - 1):
        for j in range(cols - 1):
            a, b, c, d = i * cols + j, i * cols + j + 1, (i + 1) * cols + j + 1, (i + 1) * cols + j
            t += [(a, b, c), (a, c, d)]
    return v, np.asarray(t, dtype=np.int64)


def label(m, atlas, key, centre, frame=None, width=None, lift=0.0006):
    m.decal(atlas, key, centre, frame, 1.0, width, lift, material=LABELS)


class Pouch:
    """Laminate pouch made of a front and a back wall that meet on the mid-plane at the side and bottom seals.
    v_top < 1 ends the walls below the top seal line: an opened pouch, with the walls apart at the cut."""

    def __init__(self, width, height, depth, bottom, v_top=1.0):
        self.w, self.h, self.d, self.bottom, self.v_top = width, height, depth, bottom, v_top
        self.cy = bottom + height / 2

    def z(self, x, y):
        return self.d / 2 * taper(x / (self.w / 2)) * taper((y - self.cy) / (self.h / 2))

    def top(self):
        return self.cy + self.v_top * self.h / 2

    def walls(self, m, material, inside_visible, nu=14, nv=18):
        us = np.linspace(-1.0, 1.0, nu + 1)
        vs = np.linspace(-1.0, self.v_top, nv + 1)
        for side in (1.0, -1.0):
            pts = np.array([[(u * self.w / 2, self.cy + v * self.h / 2,
                              side * self.z(u * self.w / 2, self.cy + v * self.h / 2)) for u in us] for v in vs])
            verts, tris = grid(pts)
            n = np.cross(verts[tris[:, 1]] - verts[tris[:, 0]], verts[tris[:, 2]] - verts[tris[:, 0]])
            if (n[:, 2] * side).sum() < 0:      # outer face looks away from the mid-plane
                tris = tris[:, ::-1]
            m.add(material, verts, tris)
            if inside_visible:                 # the inner foil shows through an opened mouth
                m.add(material, verts - np.array([0.0, 0.0, side * 0.0004]), tris[:, ::-1])

    def label(self, m, atlas, key, cx, cy, width, back=False, nx=8, ny=6):
        """Label printed on the curved wall: a grid that follows the bulge, 0.5 mm off the foil."""
        entry = atlas["entries"][key]
        h = width * entry["h"] / entry["w"]
        pts, uvs = [], []
        for j in range(ny + 1):
            for i in range(nx + 1):
                x = cx - width / 2 + width * i / nx
                y = cy - h / 2 + h * j / ny
                pts.append((x, y, self.z(x, y) + 0.0005))
                uvs.append((entry["u0"] + (entry["u1"] - entry["u0"]) * i / nx,
                            entry["v0"] + (entry["v1"] - entry["v0"]) * j / ny))
        pts, uvs = np.array(pts), np.array(uvs)
        t = []
        for j in range(ny):
            for i in range(nx):
                a, b = j * (nx + 1) + i, j * (nx + 1) + i + 1
                c, d = (j + 1) * (nx + 1) + i + 1, (j + 1) * (nx + 1) + i
                t += [(a, b, c), (a, c, d)]
        t = np.array(t, dtype=np.int64)
        m.add(LABELS, pts, t, BACK if back else None, uvs[t])


def flanges(m, pouch, top_band):
    """Flat heat seals on the mid-plane: sides and bottom, and the crimped top band of a sealed pouch."""
    w, bottom, top = pouch.w, pouch.bottom, pouch.top()
    m.box(FOIL, (0, bottom - 0.004, 0), (w + 0.012, 0.012, 0.0012), 0.0003)
    for s in (-1, 1):
        m.box(FOIL, (s * (w / 2 + 0.002), (bottom - 0.002 + top) / 2, 0), (0.012, top - bottom + 0.002, 0.0012), 0.0003)
    if top_band:
        m.box(FOIL, (0, top + 0.008, 0), (w + 0.012, 0.020, 0.0012), 0.0003)
        for k in range(3):
            for side in (-1, 1):
                m.box(FOIL, (0, top + 0.005 + 0.005 * k, side * 0.0008), (w + 0.010, 0.0012, 0.0005), 0.0002)


# ----------------------------------------------------------------------------- meal pouch
MEAL_W, MEAL_H, MEAL_T, MEAL_BOTTOM = 0.125, 0.150, 0.028, -0.0750
MEAL_CUT = 0.72        # opened: the scissors cut runs at 86 % of the body height


def meal_pouch(atlas, opened):
    m = Model("meal")
    pouch = Pouch(MEAL_W, MEAL_H, MEAL_T, MEAL_BOTTOM, MEAL_CUT if opened else 1.0)
    pouch.walls(m, FOIL, opened)
    flanges(m, pouch, not opened)
    if opened:
        # stew surface inside the open mouth, a little below the cut, lumpy
        y_food = pouch.top() - 0.010
        rng = np.random.default_rng(5)
        us, ws = np.linspace(-0.96, 0.96, 13), np.linspace(-0.96, 0.96, 7)
        pts = np.array([[(u * MEAL_W / 2, y_food + 0.0015 * rng.standard_normal(),
                          w * 0.92 * pouch.z(u * MEAL_W / 2, y_food)) for u in us] for w in ws])
        verts, tris = grid(pts)
        n = np.cross(verts[tris[:, 1]] - verts[tris[:, 0]], verts[tris[:, 2]] - verts[tris[:, 0]])
        if n[:, 1].sum() < 0:                  # the food surface faces up, out of the mouth
            tris = tris[:, ::-1]
        m.add(FOOD, verts, tris)
    else:
        pouch.label(m, atlas, "meal_cut", 0.0, pouch.top() - 0.012, 0.108)
    pouch.label(m, atlas, "meal_front", 0.0, -0.014, 0.092)
    pouch.label(m, atlas, "velcro", 0.0, -0.020, 0.040, back=True)
    return m


# ----------------------------------------------------------------------------- drink pouch
DRINK_W, DRINK_H, DRINK_T, DRINK_BOTTOM = 0.095, 0.150, 0.024, -0.085
STRAW_PATH = [(0.0, 0.086, 0.0), (0.0, 0.118, 0.0), (0.004, 0.158, 0.006)]
STRAW_TIP = (0.004, 0.160, 0.006)


def drink_pouch(atlas, opened):
    m = Model("drink")
    pouch = Pouch(DRINK_W, DRINK_H, DRINK_T, DRINK_BOTTOM)
    pouch.walls(m, FOIL, False)
    flanges(m, pouch, False)
    top = pouch.top()
    m.box(FOIL, (0, top + 0.006, 0), (DRINK_W + 0.012, 0.012, 0.0012), 0.0003)          # seal round the adapter
    # rigid septum adapter, boss and silicone septum; the straw goes in through the same septum
    m.box(PLASTIC, (0, 0.078, 0), (0.042, 0.026, 0.017), 0.003)
    m.cyl(PLASTIC, (0, 0.091, 0), (0, 0.097, 0), 0.0085, 20, 0.001)
    m.cyl(SILICONE, (0, 0.097, 0), (0, 0.0985, 0), 0.0066, 20, 0.0004)
    label(m, atlas, "fill", (0, 0.074, 0.0085), width=0.024)
    m.tube(STRAW, STRAW_PATH, 0.0032, 10, 0.02)
    # pinch clamp on the straw: squeezed shut when closed, slid down and sprung open when open
    clamp_y, gap = (0.106, 0.0068) if opened else (0.126, 0.0036)
    for side in (-1, 1):
        m.box(AMBER, (0.002, clamp_y, side * gap), (0.020, 0.010, 0.0030), 0.0008)
    m.cyl(AMBER, (0.012, clamp_y, -gap - 0.0015), (0.012, clamp_y, gap + 0.0015), 0.0028, 10, 0.0004)
    # tethered resealable cap: on the straw tip when closed, hanging on its tether beside the adapter when open
    anchor = (0.021, 0.084, 0.004)
    if opened:
        cap_top, cap_bottom = (0.034, 0.081, 0.006), (0.034, 0.065, 0.006)
        m.tube(CORD, [anchor, (0.030, 0.090, 0.006), (0.034, 0.083, 0.006)], 0.0011, 5, 0.006)
    else:
        tip = np.array(STRAW_TIP)
        direction = np.array(STRAW_PATH[-1]) - np.array(STRAW_PATH[-2])
        direction = direction / np.linalg.norm(direction)
        cap_bottom, cap_top = tuple(tip - direction * 0.010), tuple(tip + direction * 0.004)
        m.tube(CORD, [anchor, (0.026, 0.110, 0.008), (0.012, 0.148, 0.007),
                      tuple(tip - direction * 0.006 + np.array([0.004, 0, 0]))], 0.0011, 5, 0.01)
    m.cyl(AMBER, cap_bottom, cap_top, 0.0049, 16, 0.0008)
    pouch.label(m, atlas, "drink_front", 0.0, -0.016, 0.074)
    pouch.label(m, atlas, "velcro", 0.0, -0.028, 0.040, back=True)
    return m


# ----------------------------------------------------------------------------- kit
def place_point(p):
    return tuple(float(c) for c in (PLACE @ np.append(np.asarray(p, dtype=float), 1.0))[:3])


def place_dir(v):
    return tuple(float(c) + 0.0 for c in (PLACE[:3, :3] @ np.asarray(v, dtype=float)))


def build_kit(atlas):
    """{prop: {"objects": {name: Model in prop space}, "anchors": {name: (point, forward, up, note)}}}"""
    mouth_y = MEAL_BOTTOM + MEAL_H / 2 + MEAL_CUT * MEAL_H / 2 + 0.004
    props = {
        "GC_MealPouch": {
            "objects": {"GC_MealPouch_Sealed": meal_pouch(atlas, False), "GC_MealPouch_Opened": meal_pouch(atlas, True)},
            "anchors": {
                "MealGrip": ((0.0, -0.020, 0.0), (0, 0, 1), (0, 1, 0), "hand grip at the centre of the lower body"),
                "MealMouth": ((0.0, mouth_y, 0.0), (0, 1, 0), (0, 0, 1), "centre of the cut opening of the Opened variant"),
            }},
        "GC_DrinkPouch": {
            "objects": {"GC_DrinkPouch_Closed": drink_pouch(atlas, False), "GC_DrinkPouch_Open": drink_pouch(atlas, True)},
            "anchors": {
                "DrinkGrip": ((0.0, -0.020, 0.0), (0, 0, 1), (0, 1, 0), "hand grip at the centre of the lower body"),
                "DrinkStrawTip": (STRAW_TIP, (0.10, 0.92, 0.37), (0, 0, 1), "open straw end (Open variant; capped when Closed)"),
                "DrinkFillPort": ((0.0, 0.0985, 0.0), (0, 1, 0), (0, 0, 1),
                                  "silicone septum where the dispenser needle injects water before the straw goes in"),
            }},
    }
    out = {}
    for name, prop in props.items():
        objects = {}
        for obj_name, model in prop["objects"].items():
            placed = Model(obj_name)
            placed.instance(model, PLACE)
            objects[obj_name] = placed
        anchors = {}
        for a, (p, f, u, note) in prop["anchors"].items():
            f = np.asarray(f, dtype=float)
            anchors[a] = (place_point(p), place_dir(f / np.linalg.norm(f)), place_dir(u), note)
        out[name] = {"objects": objects, "anchors": anchors}
    return out


if __name__ == "__main__":
    import json
    with open(sys.argv[1], encoding="utf-8") as handle:
        kit = build_kit(json.load(handle))
    for name, prop in kit.items():
        for obj_name, model in prop["objects"].items():
            lo, hi = model.bounds()
            print("%-22s %5d tris  bounds %s .. %s" % (obj_name, model.triangle_count(), np.round(lo, 4), np.round(hi, 4)))
        for a, (p, f, u, note) in prop["anchors"].items():
            print("   anchor %-14s %s forward %s" % (a, np.round(p, 4), np.round(f, 3)))
