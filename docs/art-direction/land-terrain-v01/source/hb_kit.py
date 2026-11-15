"""hb_kit - geometry of the HABITAT-DETAILS-V1 prop kit for AURORA-7 (original work, script-generated).

Units: HB_CrewBerth, HB_Galley, HB_GrowCassette. Each is designed in a front view (a7kit convention: x to the
viewer's right, y up, z towards the viewer), with its back on the wall plane z = 0 and the deck at y = 0, and is
then placed with front_frame((0,0,0),(0,0,1)). In Unity the unit's forward (+z) therefore points into the room and
its origin sits on the wall plane at deck level, under the unit's centre line. Mount it by rotating about y only.

Movable parts are separate objects with their pivot on the hinge or slide line: HB_GalleyTable (hinge),
HB_GrowLid (hinge), HB_GrowTray (slide). HB_GrowSeedlings shares the tray's origin so it can ride with the tray
or be swapped/hidden. Anchors are points with a forward direction in the same unit space.

Sleep brief applied (Codex, 2026-11-14 16:19 UTC, from NASA OCHMO-TB-041 and the ESA rhythm article): secured
bag, quick-release restraints across chest, waist and knees only (no strap over eyes, face or neck), unobstructed
face, a visible ventilation path (supply fan above the face, side vents, return grille low), a dim warm berth
light, personal items retained. The head pad is optional support, not a required device.

pillow() and girth_band() are copied from a7_ship.py (AURORA-7 Source) unchanged.
Run "python hb_kit.py" for a triangle count without Blender.
"""
import math
import os
import sys

import numpy as np

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from a7kit import Model, at, compose, front_frame, rotation  # noqa: E402

WHITE, GRAY, GRAPHITE, AMBER = "A7_PaintWhite", "A7_PaintGray", "A7_PaintGraphite", "A7_PaintAmber"
PLASTIC, ANO, ANO_CLEAR, ALU = "A7_PlasticWhite", "A7_AnodizedBlack", "A7_AnodizedClear", "A7_Aluminum"
FABRIC, CANVAS, FABRIC_DARK, STRAP = "A7_FabricWhite", "A7_FabricCanvas", "A7_FabricDark", "A7_FabricStrap"
QUILT, GRATE, RUBBER, FOIL, GLASS = "A7_QuiltWhite", "A7_Grate", "A7_Rubber", "A7_MLISilver", "A7_Glass"
LED_CYAN, CABLE, TASK_LIGHT = "A7_LedCyan", "A7_CableBlue", "A7_LightPanel"
LABELS, LEAF, GROW_LIGHT, WATER, BAG = "HB_Labels", "HB_Leaf", "HB_GrowLight", "HB_Water", "HB_BagQuilt"
SCREEN, BERTH_LIGHT = "HB_ScreenOff", "HB_BerthLight"

PLACE = front_frame((0.0, 0.0, 0.0), (0.0, 0.0, 1.0))
FACING_PLUS_X = rotation((0, 1, 0), 90)     # inside a front view: a label or part facing the viewer's right
FACING_MINUS_X = rotation((0, 1, 0), -90)   # facing the viewer's left


def frame_at(x, y, z, turn=None):
    return compose(at(x, y, z), turn) if turn is not None else at(x, y, z)


def label(m, atlas, key, centre, frame=None, width=None, lift=0.0006):
    m.decal(atlas, key, centre, frame, 1.0, width, lift, material=LABELS)


def two_sided(m, material, v, t, uv=None):
    """Thin sheets (curtain, bellows, leaves) are seen from both sides: add the reverse winding too."""
    t = np.asarray(t, dtype=np.int64)
    m.add(material, v, t, None, uv)
    m.add(material, v, t[:, ::-1], None, None if uv is None else uv[:, ::-1, :])


def grid_sheet(points):
    """Triangles of a (rows, cols, 3) grid of points."""
    rows, cols = points.shape[:2]
    v = points.reshape(-1, 3)
    t = []
    for i in range(rows - 1):
        for j in range(cols - 1):
            a, b, c, d = i * cols + j, i * cols + j + 1, (i + 1) * cols + j + 1, (i + 1) * cols + j
            t += [(a, b, c), (a, c, d)]
    return v, np.asarray(t, dtype=np.int64)


# ----------------------------------------------------------------------------- soft goods (from a7_ship.py)
def pillow(size, strap_xs=(), pinch=0.014, sag=0.04, exponent=5.0, segs=8):
    """Pillowed canvas body of a soft bag: a rounded box whose faces bulge, drawn in where straps bind it.

    Centre at the origin, front towards +z. Returns (vertices, triangles, surface), where surface(cx, cy, cz)
    maps a point of the cube [-1, 1]^3 to the body, so straps and fittings can be laid on it."""
    half = np.asarray(size, dtype=np.float64) / 2.0

    def surface(cx, cy, cz):
        c = np.array([cx, cy, cz], dtype=np.float64)
        p = c / (np.abs(c) ** exponent).sum() ** (1.0 / exponent) * half
        if p[1] > 0:                                   # an under-filled bag sinks along its top
            p[1] *= 1.0 - sag * (1.0 - (p[0] / half[0]) ** 2)
        for sx in strap_xs:                            # straps draw the girth in by the same depth all round
            groove = pinch * math.exp(-((p[0] - sx) / 0.03) ** 2)
            p[1] *= 1.0 - groove / half[1]
            p[2] *= 1.0 - groove / half[2]
        return p

    index, verts, tris = {}, [], []

    def vertex(c):
        key = tuple(np.round(c, 6))
        if key not in index:
            index[key] = len(verts)
            verts.append(surface(*c))
        return index[key]

    uniform = np.linspace(-1.0, 1.0, segs + 1)
    grooves = [(sx + k * 0.045) / half[0] for sx in strap_xs for k in (-1, 0, 1)]
    along = [np.unique(np.round(np.concatenate([uniform, grooves]), 6)), uniform, uniform]
    for axis in range(3):
        for sign in (-1.0, 1.0):
            first, second = [a for a in range(3) if a != axis]
            for i in range(len(along[first]) - 1):
                for j in range(len(along[second]) - 1):
                    quad = []
                    for a, b in ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1)):
                        c = [0.0, 0.0, 0.0]
                        c[axis], c[first], c[second] = sign, along[first][a], along[second][b]
                        quad.append(vertex(c))
                    tris += [(quad[0], quad[1], quad[2]), (quad[0], quad[2], quad[3])]
    v = np.asarray(verts)
    t = np.asarray(tris, dtype=np.int64)
    normals = np.cross(v[t[:, 1]] - v[t[:, 0]], v[t[:, 2]] - v[t[:, 0]])
    inward = (normals * v[t].mean(axis=1)).sum(axis=1) < 0
    t[inward] = t[inward][:, ::-1]
    return v, t, surface


def girth_band(surface, x, width=0.036, lift=0.003, segs=32):
    """Webbing strap around the girth of a pillow at x: a closed band lying on the body. Returns (v, t)."""
    def cube_x(target, cy, cz):
        lo, hi = -1.0, 1.0
        for _ in range(24):
            mid = (lo + hi) / 2
            if surface(mid, cy, cz)[0] < target:
                lo = mid
            else:
                hi = mid
        return (lo + hi) / 2

    rings = ([], [])
    for k in range(segs):
        a = math.tau * k / segs
        cy, cz = math.cos(a), math.sin(a)
        scale = 1.0 / max(abs(cy), abs(cz))
        cy, cz = cy * scale, cz * scale
        for ring, target in zip(rings, (x - width / 2, x + width / 2)):
            p = surface(cube_x(target, cy, cz), cy, cz)
            radial = np.array([0.0, p[1], p[2]])
            ring.append(p + radial / np.linalg.norm(radial) * lift)
    v = np.asarray(rings[0] + rings[1])
    t = []
    for k in range(segs):
        k1 = (k + 1) % segs
        t += [(k, segs + k, segs + k1), (k, segs + k1, k1)]
    t = np.asarray(t, dtype=np.int64)
    normals = np.cross(v[t[:, 1]] - v[t[:, 0]], v[t[:, 2]] - v[t[:, 0]])
    centre = v[t].mean(axis=1)
    centre[:, 0] = 0.0
    inward = (normals * centre).sum(axis=1) < 0
    t[inward] = t[inward][:, ::-1]
    return v, t


def soft_pack(m, material, size, centre, frame=None, straps=(), strap_width=0.03, exponent=4.0):
    """A small soft bag at centre (size = width, height, depth), optional girth straps along its width."""
    v, t, surface = pillow(size, straps, 0.008, 0.0, exponent, 8)
    place = compose(frame if frame is not None else np.eye(4), at(*centre))
    m.add(material, v, t, place)
    for sx in straps:
        bv, bt = girth_band(surface, sx, strap_width, 0.002, 28)
        m.add(STRAP, bv, bt, place)
    return surface, place


def pouch(m, atlas, key, centre, size=(0.11, 0.15, 0.024), frame=None):
    """Foil ration pouch: pillowed body, crimped top seal, a printed label on the front."""
    w, h, d = size
    place = compose(frame if frame is not None else np.eye(4), at(*centre))
    v, t, _ = pillow((w, h, d), (), 0.0, 0.0, 6.0, 6)
    m.add(FOIL, v, t, place)
    m.box(FOIL, (0, h / 2 + 0.008, 0), (w * 0.98, 0.018, 0.003), 0.0008, place)
    label(m, atlas, key, (0, -h * 0.06, d / 2 + 0.0015), place, width=w * 0.86)


# ----------------------------------------------------------------------------- small hardware
def paddle_latch(m, x, y, z, frame=None):
    m.box(ANO, (x, y, z + 0.004), (0.11, 0.035, 0.008), 0.003, frame)
    m.box(ALU, (x, y - 0.004, z + 0.010), (0.09, 0.016, 0.006), 0.002, frame)


def hinge_strip(m, x, y0, y1, z, frame=None):
    m.box(ALU, (x, (y0 + y1) / 2, z), (0.012, y1 - y0, 0.010), 0.002, frame)


def vent(m, centre, size, frame=None):
    """Ventilation grille with an aluminium bezel; size = (width, height) in the frame's x/y plane."""
    w, h = size
    m.box(ALU, (centre[0], centre[1], centre[2] + 0.002), (w + 0.03, h + 0.03, 0.004), 0.002, frame)
    m.box(GRATE, (centre[0], centre[1], centre[2] + 0.005), (w, h, 0.004), 0.0, frame)


def quick_release(m, atlas, centre, frame):
    """Quick-release buckle on a restraint strap: clear-anodised body, steel bar, amber PULL tab."""
    m.box(ANO_CLEAR, (centre[0], centre[1], centre[2] + 0.005), (0.070, 0.050, 0.010), 0.003, frame)
    m.box(ALU, (centre[0], centre[1] + 0.012, centre[2] + 0.011), (0.056, 0.008, 0.004), 0.001, frame)
    m.box(AMBER, (centre[0] + 0.050, centre[1], centre[2] + 0.006), (0.050, 0.024, 0.004), 0.002, frame)
    label(m, atlas, "release", (centre[0] + 0.050, centre[1], centre[2] + 0.0083), frame, width=0.044)


def d_ring(m, centre, frame=None):
    m.tube(ALU, [(centre[0] - 0.018, centre[1], centre[2]), (centre[0] - 0.018, centre[1], centre[2] + 0.022),
                 (centre[0] + 0.018, centre[1], centre[2] + 0.022), (centre[0] + 0.018, centre[1], centre[2])],
           0.004, 6, 0.008, frame)
    m.box(ANO, (centre[0], centre[1], centre[2] + 0.002), (0.05, 0.03, 0.004), 0.001, frame)


# ----------------------------------------------------------------------------- crew berth
BERTH_D = 0.52
BAG_X = -0.22


def sleeping_bag(atlas):
    """Sleeping bag lying along x (head at +x) in its own space; the berth stands it up on the wall."""
    b = Model("bag")
    length, width, thick = 1.78, 0.72, 0.15
    straps = (-0.42, 0.02, 0.40)               # knees, waist, chest: nothing near the face or neck
    v, t, surface = pillow((length, width, thick), straps, 0.012, 0.0, 4.0, 10)
    b.add(BAG, v, t)
    for sx in straps:
        bv, bt = girth_band(surface, sx, 0.040, 0.003, 32)
        b.add(STRAP, bv, bt)
        front = surface(sx / (length / 2), -0.30, 1.0)
        # the buckle text reads level once the bag stands up (the bag frame turns +90 about z)
        quick_release(m=b, atlas=atlas, centre=(0.0, 0.0, 0.0), frame=compose(at(front[0], front[1], front[2] + 0.002),
                                                                               rotation((0, 0, 1), -90)))
        for side in (-1, 1):                    # tabs from the strap to the D-rings beside the bag
            edge = surface(sx / (length / 2), side * 1.0, -0.2)
            b.box(STRAP, (sx, edge[1] + side * 0.035, edge[2] - 0.01), (0.040, 0.07, 0.006), 0.001)
    # zip: a dark track along the front, following the bag's surface
    zip_path = []
    for s in np.linspace(-0.72, 0.62, 12):
        p = surface(s, 0.62, 1.0)
        zip_path.append((p[0], p[1], p[2] + 0.004))
    b.tube(RUBBER, zip_path, 0.006, 6)
    top = surface(0.62, 0.62, 1.0)
    b.box(ALU, (top[0] + 0.02, top[1], top[2] + 0.010), (0.04, 0.014, 0.006), 0.002)
    # open collar: a narrow roll of the ivory liner turned out at the head end
    v, t, _ = pillow((0.07, width * 0.90, 0.035), (), 0.0, 0.0, 3.0, 6)
    b.add(FABRIC, v, t, at(length / 2 - 0.045, 0.0, thick / 2 - 0.012))
    return b


def crew_berth(atlas):
    m = Model("HB_CrewBerth")
    d = BERTH_D
    # closed rear surface: a wall plate behind the whole unit
    m.box(GRAPHITE, (0, 1.20, 0.01), (1.44, 2.40, 0.02), 0.004)
    # plinth with the low return-air grille across the berth
    m.box(GRAPHITE, (0, 0.06, d / 2), (1.44, 0.12, d), 0.006)
    vent(m, (BAG_X, 0.062, d), (0.80, 0.05))
    # side panels and the divider between the berth and the locker column; soft bumpers on the front edges
    for x in (-0.705, 0.265, 0.705):
        m.box(WHITE, (x, 1.24, (0.02 + d) / 2), (0.03, 2.24, d - 0.02), 0.006)
        m.box(RUBBER, (x, 1.24, d + 0.004), (0.036, 2.20, 0.010), 0.004)
    m.box(WHITE, (0, 2.38, (0.02 + d) / 2), (1.44, 0.04, d - 0.02), 0.006)
    # quilted back pad of the berth
    m.box(QUILT, (BAG_X, 1.20, 0.04), (0.94, 2.04, 0.04), 0.012)
    # supply hood: fan grille above the face, name and airflow labels on its front
    m.box(WHITE, (BAG_X, 2.29, (0.02 + d) / 2), (0.94, 0.14, d - 0.02), 0.006)
    m.lathe(ALU, (BAG_X, 2.204, 0.30), (0, 1, 0), [(0.085, 0.0), (0.105, 0.0), (0.105, 0.016), (0.085, 0.016), (0.085, 0.0)],
            28, None, (False, False))
    m.cyl(GRATE, (BAG_X, 2.208, 0.30), (BAG_X, 2.218, 0.30), 0.086, 28, 0.0)
    m.cyl(ANO, (BAG_X, 2.212, 0.30), (BAG_X, 2.219, 0.30), 0.022, 16, 0.0)
    label(m, atlas, "berth_name", (BAG_X - 0.25, 2.29, d))
    label(m, atlas, "airflow", (BAG_X + 0.22, 2.29, d))
    # flexible supply duct from the cabin air loop in the wall into the hood
    duct = [(BAG_X + 0.20, 2.40, 0.32), (BAG_X + 0.20, 2.47, 0.30), (BAG_X + 0.20, 2.50, 0.18), (BAG_X + 0.20, 2.50, 0.0)]
    m.tube(FABRIC, duct, 0.045, 14, 0.05)
    for p in (duct[0], duct[-1]):
        m.cyl(ALU, (p[0], p[1] - 0.012 if p is duct[0] else p[1], p[2] if p is duct[0] else 0.02),
              (p[0], p[1] + 0.012 if p is duct[0] else p[1], p[2] if p is duct[0] else 0.045), 0.050, 16, 0.002)
    for k in range(1, 6):                     # reinforcing rings of the duct
        s = k / 6.0
        q = np.array(duct[1]) * (1 - s) + np.array(duct[2]) * s
        m.cyl(FABRIC, (q[0], q[1] - 0.004, q[2] - 0.004), (q[0], q[1] + 0.004, q[2] + 0.004), 0.048, 14, 0.001)
    # side vents into the berth, both sides
    for x, turn in ((-0.689, FACING_PLUS_X), (0.249, FACING_MINUS_X)):
        for y in (0.80, 1.78):
            vent(m, (0, 0, 0), (0.27, 0.22), frame_at(x, y, 0.30, turn))
    # reading light: gooseneck from the divider, dim warm lamp
    m.tube(ANO, [(0.25, 2.03, 0.40), (0.20, 2.10, 0.40), (0.12, 2.12, 0.39), (0.08, 2.10, 0.38)], 0.006, 8, 0.03)
    m.cyl(ALU, (0.075, 2.105, 0.38), (0.075, 2.065, 0.38), 0.026, 16, 0.003)
    m.cyl(BERTH_LIGHT, (0.075, 2.068, 0.38), (0.075, 2.062, 0.38), 0.020, 16, 0.0)
    # fan / light switch box on the divider, cyan status LED
    m.box(ANO, (0.240, 1.66, 0.40), (0.020, 0.13, 0.08), 0.004)
    for y, key in ((1.69, "fan"), (1.63, "light")):
        m.cyl(ALU, (0.229, y, 0.385), (0.214, y, 0.385), 0.006, 8, 0.001)
        label(m, atlas, key, (0, 0, 0), frame_at(0.2295, y, 0.425, FACING_MINUS_X), width=0.04)
    m.box(LED_CYAN, (0.2295, 1.72, 0.42), (0.003, 0.008, 0.008), 0.0)
    # sleeping bag on the back pad, D-rings beside it at the strap heights
    bag_place = compose(at(BAG_X, 1.20, 0.06 + 0.075 + 0.004), rotation((0, 0, 1), 90))
    m.instance(sleeping_bag(atlas), bag_place)
    for s in (-0.42, 0.02, 0.40):
        for side in (-1, 1):
            d_ring(m, (BAG_X + side * 0.43, 1.20 + s, 0.06))
    # optional head pad (support only; no strap over eyes, face or neck)
    soft_pack(m, CANVAS, (0.30, 0.13, 0.04), (BAG_X, 2.10, 0.082))
    # privacy curtain on a rail under the hood, gathered at the left and tied back
    m.tube(ALU, [(-0.69, 2.205, 0.47), (0.25, 2.205, 0.47)], 0.008, 8)
    folds = 8
    xs = np.linspace(-0.685, -0.53, 2 * folds + 1)
    zs = np.array([0.47 + (0.022 if k % 2 else -0.022) for k in range(len(xs))])
    ys = np.linspace(0.32, 2.19, 14)
    pts = np.array([[[x, y, z + 0.006 * math.sin(y * 7.0 + k)] for k, (x, z) in enumerate(zip(xs, zs))] for y in ys])
    v, t = grid_sheet(pts)
    two_sided(m, FABRIC, v, t)
    for x in xs[::2]:
        m.cyl(ALU, (x, 2.19, 0.462), (x, 2.19, 0.478), 0.010, 8, 0.0)
    m.box(STRAP, (-0.607, 1.12, 0.44), (0.17, 0.035, 0.005), 0.001)
    m.box(STRAP, (-0.607, 1.12, 0.50), (0.17, 0.035, 0.005), 0.001)
    m.box(STRAP, (-0.523, 1.12, 0.47), (0.005, 0.035, 0.064), 0.001)
    # personal items, all retained: tablet in a holder, photos under elastic, headset on a hook, toiletry kit
    m.box(ALU, (0.246, 1.30, 0.30), (0.008, 0.20, 0.27), 0.003)
    m.box(ANO, (0.237, 1.30, 0.30), (0.010, 0.17, 0.25), 0.006)
    m.box(SCREEN, (0.2315, 1.30, 0.30), (0.002, 0.15, 0.23), 0.0)
    m.box(STRAP, (0.229, 1.37, 0.30), (0.004, 0.012, 0.27), 0.001)
    for key, z, y in (("photo_sea", 0.17, 1.50), ("photo_hills", 0.28, 1.50), ("photo_forest", 0.40, 1.51)):
        label(m, atlas, key, (0, 0, 0), frame_at(-0.689, y, z, FACING_PLUS_X), lift=0.001)
    m.box(STRAP, (-0.6875, 1.47, 0.29), (0.004, 0.012, 0.36), 0.001)
    m.tube(ALU, [(-0.69, 1.98, 0.42), (-0.66, 1.98, 0.42), (-0.66, 2.00, 0.42)], 0.005, 6, 0.008)
    arc = [(-0.655, 1.885 + 0.075 * math.sin(a), 0.42 + 0.075 * math.cos(a)) for a in np.linspace(0, math.pi, 10)]
    m.tube(ANO, arc, 0.007, 6)
    for z in (0.345, 0.495):
        m.cyl(ANO, (-0.675, 1.885, z), (-0.640, 1.885, z), 0.038, 18, 0.004)
        m.cyl(RUBBER, (-0.640, 1.885, z), (-0.628, 1.885, z), 0.034, 18, 0.004)
    m.tube(RUBBER, [(-0.66, 1.86, 0.345), (-0.655, 1.76, 0.33), (-0.66, 1.66, 0.345)], 0.003, 5, 0.03)
    surface, place = soft_pack(m, CANVAS, (0.06, 0.13, 0.20), (0.215, 0.55, 0.30), None, ())
    label(m, atlas, "velcro_dark", (0, 0, 0), frame_at(0.2485, 0.62, 0.30, FACING_MINUS_X), width=0.05)
    # locker column: clothing, personal stowage, linen and spares
    for (y0, y1, key) in ((1.62, 2.34, "stow_clothing"), (0.90, 1.60, "stow_personal"), (0.14, 0.88, "stow_linen")):
        cy = (y0 + y1) / 2
        m.box(WHITE, (0.485, cy, d - 0.012), (0.40, y1 - y0 - 0.012, 0.02), 0.004)
        m.box(GRAPHITE, (0.485, y1 + 0.005, d - 0.03), (0.41, 0.012, 0.03), 0.001)
        hinge_strip(m, 0.295, y0 + 0.04, y1 - 0.04, d - 0.0)
        paddle_latch(m, 0.62, cy, d - 0.002)
        label(m, atlas, key, (0.48, y1 - 0.07, d - 0.0015))
    anchors = {
        "SleepEntry": ((BAG_X, 0.0, 0.80), (0.0, 0.0, -1.0)),
        "SleepCamera": ((BAG_X, 1.96, 0.24), (0.0, 0.0, 1.0)),
        "PersonalStowage": ((0.62, 1.25, d + 0.03), (0.0, 0.0, 1.0)),
    }
    return {"HB_CrewBerth": (m, (0.0, 0.0, 0.0), None)}, anchors


# ----------------------------------------------------------------------------- galley
GALLEY_D = 0.46


def galley(atlas):
    m = Model("HB_Galley")
    d = GALLEY_D
    m.box(GRAPHITE, (0, 1.20, 0.01), (1.44, 2.40, 0.02), 0.004)                 # closed rear
    m.box(GRAPHITE, (0, 0.06, d / 2), (1.44, 0.12, d), 0.006)                  # plinth
    for x in (-0.705, 0.705):
        m.box(WHITE, (x, 1.24, (0.02 + d) / 2), (0.03, 2.24, d - 0.02), 0.006)
        m.box(RUBBER, (x, 1.24, d + 0.004), (0.036, 2.20, 0.010), 0.004)
    m.box(WHITE, (0, 2.35, (0.02 + d) / 2), (1.44, 0.10, d - 0.02), 0.006)      # name fascia
    label(m, atlas, "galley_name", (0, 2.35, d))
    # upper ration stowage: shelf, centre divider, a closed locker on the right
    m.box(GRAPHITE, (0, 1.555, (0.02 + d) / 2), (1.38, 0.02, d - 0.02), 0.002)
    m.box(WHITE, (0, 1.93, (0.02 + d) / 2), (0.02, 0.74, d - 0.02), 0.002)
    m.box(WHITE, (0.355, 1.93, d - 0.012), (0.67, 0.72, 0.02), 0.004)
    hinge_strip(m, 0.03, 1.62, 2.24, d)
    paddle_latch(m, 0.62, 1.93, d - 0.002)
    label(m, atlas, "rations_drinks", (0.355, 2.20, d - 0.0015))
    # left: a closed upper drawer and an open shallow drawer of ration pouches
    m.box(WHITE, (-0.355, 2.11, d - 0.012), (0.67, 0.36, 0.02), 0.004)
    m.box(ALU, (-0.355, 2.03, d + 0.012), (0.24, 0.018, 0.016), 0.004)
    label(m, atlas, "rations_meals", (-0.355, 2.20, d - 0.0015))
    m.box(WHITE, (-0.355, 1.93, (0.02 + d) / 2), (0.67, 0.02, d - 0.02), 0.002)
    pull = 0.24
    m.box(GRAPHITE, (-0.355, 1.585, (0.04 + d + pull) / 2), (0.64, 0.012, d + pull - 0.04), 0.002)
    for x in (-0.672, -0.038):
        m.box(WHITE, (x, 1.625, (0.04 + d + pull) / 2), (0.012, 0.07, d + pull - 0.04), 0.002)
    m.box(WHITE, (-0.355, 1.635, d + pull - 0.01), (0.67, 0.10, 0.02), 0.004)
    m.box(ALU, (-0.355, 1.64, d + pull + 0.012), (0.24, 0.018, 0.016), 0.004)
    keys = ("pouch_meal", "pouch_breakfast", "pouch_meal", "pouch_snack", "pouch_breakfast")
    for row, z in enumerate((d + pull - 0.07, d + pull - 0.16)):
        for k, x in enumerate(np.linspace(-0.60, -0.11, 5)):
            pouch(m, atlas, keys[(k + row) % 5], (x, 1.67, z), (0.10, 0.15, 0.024),
                  compose(at(0, 0, 0), rotation((1, 0, 0), -12)))
    # work recess: task light, bungee cords holding pouches, Velcro patches, tethered utensils
    m.box(TASK_LIGHT, (-0.27, 1.538, 0.30), (0.70, 0.010, 0.04), 0.002)
    m.box(GRAPHITE, (-0.27, 1.25, 0.03), (0.86, 0.58, 0.02), 0.003)
    for k, (x, key) in enumerate(((-0.58, "pouch_breakfast"), (-0.44, "pouch_snack"), (-0.30, "pouch_meal"))):
        pouch(m, atlas, key, (x, 1.30, 0.058), (0.11, 0.15, 0.024))
    for y in (1.24, 1.37):
        m.tube(RUBBER, [(-0.66, y, 0.042), (-0.40, y, 0.080), (-0.18, y, 0.046)], 0.004, 6, 0.04)
        for x in (-0.66, -0.18):
            m.box(ALU, (x, y, 0.045), (0.016, 0.022, 0.012), 0.002)
    for x, y in ((-0.10, 1.12), (0.06, 1.12), (-0.66, 1.05)):
        label(m, atlas, "velcro_dark", (x, y, 0.0405), width=0.05)
    m.box(ALU, (0.04, 1.47, 0.045), (0.20, 0.016, 0.016), 0.003)
    for x, kind in ((-0.03, "spoon"), (0.04, "fork"), (0.11, "scissors")):
        m.tube(RUBBER, [(x, 1.462, 0.05), (x + 0.004, 1.42, 0.056), (x, 1.39, 0.058)], 0.0018, 4, 0.01)
        if kind == "spoon":
            m.box(ALU, (x, 1.31, 0.058), (0.012, 0.15, 0.004), 0.001)
            m.sphere(ALU, (x, 1.215, 0.060), 0.022, 12, 6, None, (0.75, 1.0, 0.25))
        elif kind == "fork":
            m.box(ALU, (x, 1.31, 0.058), (0.012, 0.15, 0.004), 0.001)
            for dx in (-0.007, 0.0, 0.007):
                m.box(ALU, (x + dx, 1.215, 0.058), (0.003, 0.05, 0.003), 0.0005)
        else:
            for tilt in (-8, 8):
                m.box(ALU, (0, -0.07, 0), (0.010, 0.13, 0.003), 0.0005, compose(at(x, 1.33, 0.058), rotation((0, 0, 1), tilt)))
            for dx in (-0.016, 0.016):
                m.tube(ANO, [(x + dx - 0.012, 1.36, 0.058), (x + dx, 1.38, 0.058), (x + dx + 0.012, 1.36, 0.058),
                             (x + dx, 1.345, 0.058)], 0.004, 6, 0.006, None, True)
    # potable water dispenser: control head above an open dispensing bay with the needle nozzle and drip pad
    m.box(WHITE, (0.445, 1.37, 0.20), (0.46, 0.34, 0.36), 0.008)
    for x in (0.222, 0.668):
        m.box(WHITE, (x, 1.09, 0.20), (0.016, 0.22, 0.36), 0.003)
    m.box(GRAPHITE, (0.445, 1.09, 0.03), (0.43, 0.22, 0.02), 0.002)
    m.box(ANO, (0.445, 0.985, 0.20), (0.43, 0.012, 0.36), 0.003)
    m.box(CANVAS, (0.445, 0.997, 0.27), (0.14, 0.014, 0.12), 0.004)
    m.box(ALU, (0.445, 1.185, 0.30), (0.07, 0.03, 0.07), 0.006)
    m.cyl(ALU, (0.445, 1.17, 0.30), (0.445, 1.13, 0.30), 0.006, 10, 0.001)
    m.box(GRAPHITE, (0.445, 1.385, 0.384), (0.42, 0.28, 0.010), 0.004)
    label(m, atlas, "potable", (0.445, 1.495, 0.3895))
    for x, key, mat in ((0.36, "hot", AMBER), (0.44, "amb", PLASTIC)):
        m.cyl(mat, (x, 1.425, 0.389), (x, 1.425, 0.399), 0.017, 16, 0.002)
        label(m, atlas, key, (x, 1.385, 0.3895), width=0.046)
    for k, x in enumerate((0.36, 0.40, 0.44)):
        m.box(ALU, (x, 1.33, 0.392), (0.028, 0.016, 0.008), 0.002)
    label(m, atlas, "volume", (0.40, 1.300, 0.3895))
    m.box(LED_CYAN, (0.58, 1.44, 0.390), (0.010, 0.010, 0.004), 0.0)
    label(m, atlas, "caution_hot", (0.585, 1.33, 0.3895), width=0.09)
    # a filled drink bag waiting in its clip at the back of the bay, clear of the nozzle
    m.box(ALU, (0.575, 1.155, 0.055), (0.05, 0.012, 0.03), 0.002)
    pouch(m, atlas, "pouch_drink", (0.575, 1.075, 0.065), (0.09, 0.14, 0.022))
    # lower: wet-trash bag on a rail, food warmer strapped in its bay
    m.box(ALU, (-0.40, 0.80, 0.05), (0.50, 0.025, 0.03), 0.004)
    for x in (-0.58, -0.22):
        m.tube(ALU, [(x, 0.80, 0.06), (x, 0.80, 0.10), (x, 0.76, 0.10)], 0.005, 6, 0.01)
    surface, place = soft_pack(m, FABRIC, (0.42, 0.46, 0.22), (-0.40, 0.52, 0.14), None, (-0.08,), 0.03)
    label(m, atlas, "wet_trash", (0.0, 0.10, 0.111), place)
    m.box(GRAY, (0.40, 0.42, 0.10), (0.46, 0.34, 0.12), 0.012)
    m.box(ALU, (0.40, 0.42, 0.162), (0.44, 0.012, 0.004), 0.0)
    for x in (0.24, 0.56):
        m.box(ALU, (x, 0.50, 0.165), (0.04, 0.05, 0.012), 0.003)
    m.cyl(ALU, (0.34, 0.61, 0.10), (0.46, 0.61, 0.10), 0.008, 8, 0.0)
    label(m, atlas, "food_warmer", (0.40, 0.33, 0.1615))
    for x in (0.27, 0.53):
        m.box(STRAP, (x, 0.42, 0.163), (0.035, 0.36, 0.004), 0.001)
    m.tube(CABLE, [(0.63, 0.45, 0.06), (0.67, 0.30, 0.05), (0.66, 0.20, 0.03), (0.66, 0.20, 0.0)], 0.005, 6, 0.04)
    # fold-down food preparation table: its own object, pivot on the hinge line
    hinge = (0.0, 0.97, d)
    table = Model("HB_GalleyTable")
    table.box(WHITE, (-0.28, 0.985, d + 0.17), (0.84, 0.025, 0.34), 0.004)
    table.tube(ALU, [(-0.70, 0.998, d + 0.336), (0.14, 0.998, d + 0.336)], 0.007, 8)
    for x in (-0.66, 0.10):
        table.cyl(ALU, (x, 0.972, d + 0.22), (x, 0.76, d + 0.01), 0.008, 8, 0.0)
        table.box(ALU, (x, 0.972, d + 0.22), (0.03, 0.01, 0.04), 0.002)
    table.box(GRAPHITE, (-0.36, 1.006, d + 0.17), (0.42, 0.016, 0.27), 0.004)
    table.box(GRAPHITE, (-0.36, 1.016, d + 0.17), (0.40, 0.008, 0.25), 0.003)
    pouch(table, atlas, "pouch_meal", (0.0, 0.0, 0.0), (0.11, 0.15, 0.024),
          compose(at(-0.44, 1.032, d + 0.17), rotation((1, 0, 0), -90)))
    pouch(table, atlas, "pouch_drink", (0.0, 0.0, 0.0), (0.09, 0.14, 0.022),
          compose(at(-0.27, 1.030, d + 0.17), rotation((1, 0, 0), -90)))
    for z in (d + 0.10, d + 0.24):
        table.tube(RUBBER, [(-0.57, 1.026, z), (-0.36, 1.050, z), (-0.15, 1.026, z)], 0.004, 6, 0.05)
    table.cyl(AMBER, (-0.27, 1.042, d + 0.255), (-0.27, 1.042, d + 0.275), 0.006, 8, 0.0)
    label(table, atlas, "velcro_light", (0.0, 0.0, 0.0), compose(at(0.02, 0.9985, d + 0.12), rotation((1, 0, 0), -90)), 0.05)
    anchors = {
        "RationPickup": ((-0.355, 1.72, d + pull - 0.04), (0.0, 0.0, 1.0)),
        "WaterDispenser": ((0.445, 1.13, 0.30), (0.0, 0.0, 1.0)),
    }
    return {"HB_Galley": (m, (0.0, 0.0, 0.0), None),
            "HB_GalleyTable": (table, hinge, {"type": "hinge", "axis": "x", "range_deg": [0, 90],
                                              "note": "modelled deployed; rotates about its local x axis to stow upright"})}, anchors


# ----------------------------------------------------------------------------- grow cassette
GROW_D = 0.50


def seedling(m, x, z, base_y, seed):
    """Young leafy green: six spoon-shaped leaves on a short stem. Leaf UVs fill the HB_Leaf square."""
    rng = np.random.default_rng(seed)
    m.cyl(LEAF, (x, base_y, z), (x, base_y + 0.018, z), 0.004, 6, 0.0)
    top = base_y + 0.016
    for k in range(6):
        a = math.tau * k / 6 + rng.random() * 0.5
        length = 0.06 + 0.04 * rng.random()
        width = 0.035 + 0.012 * rng.random()
        rise = 0.75 + 0.35 * rng.random()
        dh = np.array([math.cos(a), 0.0, math.sin(a)])
        side = np.array([-math.sin(a), 0.0, math.cos(a)])
        rows = []
        for s in np.linspace(0.0, 1.0, 7):
            centre = np.array([x, top, z]) + dh * (length * s) + np.array([0, 1, 0]) * (length * (rise * s - 0.55 * s * s))
            hw = 0.5 * width * math.sin(math.pi * min(1.0, 0.15 + 0.92 * s)) * (1.0 - 0.25 * s)
            row = []
            for c in (-1.0, -0.5, 0.0, 0.5, 1.0):
                cup = 0.010 * c * c * (0.3 + s)
                row.append(centre + side * (c * hw) + np.array([0, cup, 0]))
            rows.append(row)
        pts = np.array(rows)
        v, t = grid_sheet(pts)
        n_rows, n_cols = pts.shape[:2]
        uv_v = np.array([[(j / (n_cols - 1)), (i / (n_rows - 1))] for i in range(n_rows) for j in range(n_cols)])
        two_sided(m, LEAF, v, t, uv_v[t])


def grow_cassette(atlas):
    m = Model("HB_GrowCassette")
    d = GROW_D
    y0, y1 = 0.40, 1.98
    m.box(GRAPHITE, (0, (y0 + y1) / 2, 0.01), (1.00, y1 - y0, 0.02), 0.004)              # closed rear
    for x in (-0.485, 0.485):
        m.box(WHITE, (x, (y0 + y1) / 2, (0.02 + d) / 2), (0.03, y1 - y0, d - 0.02), 0.006)
        m.box(RUBBER, (x, (y0 + y1) / 2, d + 0.004), (0.036, y1 - y0 - 0.04, 0.010), 0.004)
    m.box(WHITE, (0, y0 + 0.02, (0.02 + d) / 2), (1.00, 0.04, d - 0.02), 0.006)
    # grow-light housing: emitter facing down, heat sink on top, fan grille and label at the front
    m.box(WHITE, (0, 1.80, (0.02 + d) / 2), (0.94, 0.28, d - 0.02), 0.006)
    m.box(GROW_LIGHT, (0, 1.6575, 0.25), (0.86, 0.006, 0.38), 0.0)
    m.box(ALU, (0, 1.651, 0.25), (0.90, 0.006, 0.42), 0.002)
    m.box(GRAPHITE, (0, 1.955, 0.25), (0.96, 0.03, 0.46), 0.004)
    for x in np.linspace(-0.42, 0.42, 15):
        m.box(ALU, (x, 2.00, 0.25), (0.004, 0.06, 0.40), 0.001)
    m.cyl(ALU, (0.33, 1.80, d - 0.006), (0.33, 1.80, d + 0.004), 0.062, 24, 0.002)
    m.cyl(GRATE, (0.33, 1.80, d + 0.002), (0.33, 1.80, d + 0.006), 0.052, 24, 0.0)
    label(m, atlas, "grow_light", (-0.12, 1.83, d))
    label(m, atlas, "light_cycle", (-0.12, 1.77, d))
    m.box(LED_CYAN, (0.17, 1.83, d + 0.002), (0.010, 0.010, 0.004), 0.0)
    m.tube(CABLE, [(0.44, 1.94, 0.06), (0.44, 2.03, 0.06), (0.44, 2.05, 0.0)], 0.005, 6, 0.02)
    # chamber: pleated reflective bellows on both sides, liner on the back, floor and tray rails
    for side in (-1, 1):
        ys = np.linspace(1.10, 1.645, 13)
        xs = np.array([side * (0.468 - (0.022 if k % 2 else 0.0)) for k in range(len(ys))])
        zs = np.linspace(0.04, 0.47, 6)
        pts = np.array([[[x, y, z] for z in zs] for x, y in zip(xs, ys)])
        v, t = grid_sheet(pts)
        two_sided(m, FABRIC, v, t)
    m.box(FABRIC, (0, 1.375, 0.026), (0.90, 0.54, 0.012), 0.003)
    m.box(GRAPHITE, (0, 1.08, (0.02 + d) / 2), (0.94, 0.04, d - 0.02), 0.004)
    for x in (-0.44, 0.44):
        m.box(ALU, (x, 1.105, 0.26), (0.02, 0.01, 0.42), 0.002)
    # root/water reservoir with a level window, fill port, drain valve, feed hose and retaining straps
    m.box(PLASTIC, (-0.12, 0.75, 0.20), (0.62, 0.50, 0.32), 0.02)
    m.box(GLASS, (0.10, 0.75, 0.3625), (0.05, 0.40, 0.005), 0.001)
    m.box(WATER, (0.10, 0.665, 0.3605), (0.044, 0.23, 0.004), 0.0)
    m.box(GRAPHITE, (0.10, 0.865, 0.3605), (0.044, 0.17, 0.004), 0.0)
    label(m, atlas, "level", (0.145, 0.75, 0.3605), width=0.03)
    m.cyl(ALU, (-0.30, 1.0, 0.30), (-0.30, 1.03, 0.30), 0.030, 16, 0.003)
    m.cyl(AMBER, (-0.30, 1.03, 0.30), (-0.30, 1.045, 0.30), 0.034, 16, 0.004)
    label(m, atlas, "fill", (-0.30, 0.95, 0.3605))
    m.cyl(ALU, (-0.30, 0.55, 0.36), (-0.30, 0.55, 0.40), 0.014, 12, 0.002)
    m.box(AMBER, (-0.30, 0.55, 0.405), (0.06, 0.012, 0.012), 0.002)
    label(m, atlas, "drain", (-0.18, 0.55, 0.3605))
    m.tube(RUBBER, [(0.12, 1.0, 0.08), (0.20, 1.04, 0.08), (0.40, 1.06, 0.08)], 0.008, 8, 0.03)
    for x in (-0.36, 0.12):
        m.box(STRAP, (x, 0.75, 0.362), (0.035, 0.50, 0.004), 0.001)
    # control panel: name, light cycle, two buttons, cyan status light
    m.box(GRAPHITE, (0.335, 0.76, 0.35), (0.25, 0.42, 0.03), 0.004)
    label(m, atlas, "grow_name", (0.335, 0.90, 0.3655), width=0.21)
    for k, x in enumerate((0.29, 0.38)):
        m.cyl(ALU, (x, 0.74, 0.365), (x, 0.74, 0.375), 0.016, 14, 0.002)
    m.box(LED_CYAN, (0.335, 0.64, 0.366), (0.012, 0.012, 0.004), 0.0)
    # hinged clear lid: its own object, pivot on the top hinge line
    lid = Model("HB_GrowLid")
    lid.frame(ALU, (0, 1.38, 0.485), 0.93, 0.55, 0.03, 0.02, 0.02)
    lid.box(GLASS, (0, 1.38, 0.494), (0.87, 0.49, 0.006), 0.0)
    lid.box(AMBER, (0, 1.125, 0.512), (0.30, 0.022, 0.022), 0.006)
    for x in (-0.13, 0.13):
        lid.box(ALU, (x, 1.125, 0.500), (0.02, 0.02, 0.02), 0.003)
    for x in (-0.38, 0.38):
        lid.cyl(ALU, (x - 0.03, 1.655, 0.49), (x + 0.03, 1.655, 0.49), 0.008, 8, 0.0)
    # slide-out grow tray with six root pillows; the seedlings are a separate object on the same pivot
    tray = Model("HB_GrowTray")
    tray.box(GRAPHITE, (0, 1.13, 0.26), (0.86, 0.04, 0.40), 0.004)
    tray.box(ALU, (0, 1.155, 0.26), (0.84, 0.01, 0.38), 0.002)
    tray.box(GRAPHITE, (0, 1.135, 0.465), (0.86, 0.05, 0.012), 0.003)
    tray.box(AMBER, (0, 1.135, 0.476), (0.24, 0.018, 0.012), 0.003)
    label(tray, atlas, "tray", (-0.27, 1.135, 0.4715))
    plants = Model("HB_GrowSeedlings")
    for row, z in enumerate((0.17, 0.35)):
        for k, x in enumerate((-0.27, 0.0, 0.27)):
            soft_pack(tray, CANVAS, (0.11, 0.045, 0.11), (x, 1.183, z), None, ())
            seedling(plants, x, z, 1.203, 100 + row * 3 + k)
    anchors = {
        "GrowInteraction": ((0.0, 1.125, 0.53), (0.0, 0.0, 1.0)),
        "GrowTray": ((0.0, 1.21, 0.26), (0.0, 1.0, 0.0)),
    }
    pivot_lid = (0.0, 1.655, 0.49)
    pivot_tray = (0.0, 1.11, 0.47)
    return {"HB_GrowCassette": (m, (0.0, 0.0, 0.0), None),
            "HB_GrowLid": (lid, pivot_lid, {"type": "hinge", "axis": "x", "range_deg": [0, 100],
                                            "note": "modelled closed; opens upward about the top hinge line"}),
            "HB_GrowTray": (tray, pivot_tray, {"type": "slide", "axis": "z", "range_m": [0.0, 0.30],
                                               "note": "modelled closed; slides out towards the room"}),
            "HB_GrowSeedlings": (plants, pivot_tray, {"type": "follows", "parent": "HB_GrowTray",
                                                      "note": "same origin as the tray; swap or hide per growth stage"})}, anchors


# ----------------------------------------------------------------------------- kit
UNITS = (("HB_CrewBerth", crew_berth), ("HB_Galley", galley), ("HB_GrowCassette", grow_cassette))


def place_point(p):
    return tuple(float(c) for c in (PLACE @ np.append(np.asarray(p, dtype=float), 1.0))[:3])


def place_dir(v):
    return tuple(float(c) for c in (PLACE[:3, :3] @ np.asarray(v, dtype=float)))


def build_kit(atlas):
    """{unit: {"objects": {name: (Model in unit space, origin, motion)}, "anchors": {name: (point, forward)}}}"""
    kit = {}
    for unit_name, builder in UNITS:
        objects, anchors = builder(atlas)
        placed = {}
        for name, (model, origin, motion) in objects.items():
            out = Model(name)
            out.instance(model, PLACE)
            placed[name] = (out, place_point(origin), motion)
        kit[unit_name] = {"objects": placed,
                          "anchors": {n: (place_point(p), place_dir(f)) for n, (p, f) in anchors.items()}}
    return kit


if __name__ == "__main__":
    import json
    here = os.path.dirname(os.path.abspath(__file__))
    atlas_path = sys.argv[1] if len(sys.argv) > 1 else os.path.join(here, "..", "out", "textures", "hb_labels.json")
    with open(atlas_path, encoding="utf-8") as handle:
        built = build_kit(json.load(handle))
    total = 0
    for unit_name, unit in built.items():
        for name, (model, origin, motion) in unit["objects"].items():
            lo, hi = model.bounds()
            print("%-18s %7d tris  origin %s  bounds %s .. %s" % (name, model.triangle_count(), np.round(origin, 3),
                                                                np.round(lo, 3), np.round(hi, 3)))
            total += model.triangle_count()
        for name, (p, f) in unit["anchors"].items():
            print("   anchor %-16s %s forward %s" % (name, np.round(p, 3), np.round(f, 3)))
    print("total triangles", total)
