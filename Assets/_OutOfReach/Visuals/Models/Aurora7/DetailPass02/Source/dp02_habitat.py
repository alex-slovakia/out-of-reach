"""
dp02_habitat - SHIP-DETAIL-02 crew-habitat pieces and clusters (original, procedural work).

Same conventions as dp02_kit: front-view mount frame, host surface z = 0, every piece GAP above it.
Labels come from the existing atlases: A7_Decals (ship) and HB_Labels (habitat: rations, drinks, photo).
"""
import math
import sys

import numpy as np

sys.dont_write_bytecode = True
from a7kit import Model, at, basis, compose, fillet_path, front_frame, signed_volume, unit, vec  # noqa: E402
from dp02_kit import (ALU, ANO, ANO_BLUE, C_BLUE, C_YELLOW, FAB, FAB_DARK, GAP, GRAPHITE, HB_LABELS,  # noqa: E402
                      PLASTIC, RUBBER, STRAP, cable_tie, frames_along, loom, p_clamp, ring_band, round_connector,
                      solid_lathe)

FOIL, HB_BAG = "A7_MLISilver", "HB_BagQuilt"


# ----------------------------------------------------------------------------- straps and stowage
def strap(m, points, width=0.025, thick=0.0022, across=(0.0, 1.0, 0.0), mat=STRAP, frame=None):
    """Flat webbing along a polyline; `across` gives the direction of the strap's width."""
    pts = [vec(p) for p in points]
    for a, b in zip(pts[:-1], pts[1:]):
        d = b - a
        length = float(np.linalg.norm(d))
        if length < 1e-6:
            continue
        x = d / length
        y = unit(vec(across) - x * np.dot(vec(across), x))
        z = np.cross(x, y)
        place = compose(frame if frame is not None else np.eye(4), basis((a + b) / 2, x, y, z))
        m.box(mat, (0, 0, 0), (length + thick, width, thick), 0.0, place)


def cam_buckle(m, centre, along, normal, width=0.025, frame=None):
    """Cam buckle lying on a strap: aluminium body, black cam lever, spring pin."""
    x = unit(along)
    z = unit(vec(normal) - x * np.dot(vec(normal), x))
    y = np.cross(z, x)
    place = compose(frame if frame is not None else np.eye(4), basis(vec(centre), x, y, z))
    m.box(ALU, (0, 0, 0.0035), (0.042, width + 0.012, 0.007), 0.0015, place)
    m.box(ANO, (0.006, 0, 0.0085), (0.024, width + 0.004, 0.004), 0.001, place)
    m.cyl(ALU, (-0.015, -(width / 2 + 0.007), 0.008), (-0.015, width / 2 + 0.007, 0.008), 0.0025, 6, 0.0, place)


def strap_anchor(m, x, y, z0=GAP, frame=None):
    """Tie-down point: plate with two rivets and a standing D-ring whose bar runs along local y."""
    m.box(ALU, (x, y, z0 + 0.002), (0.032, 0.040, 0.004), 0.001, frame)
    for k in (-1, 1):
        m.cyl(ANO, (x + 0.010 * k, y, z0 + 0.004), (x + 0.010 * k, y, z0 + 0.0062), 0.0035, 6, 0.0005, frame)
    a, b = vec(x, y - 0.013, z0 + 0.004), vec(x, y + 0.013, z0 + 0.004)
    m.tube(ALU, [a, a + vec(0, 0, 0.012), b + vec(0, 0, 0.012), b], 0.0026, 6, 0.005, frame)


def pouch(m, hb_atlas, key, centre, size=(0.11, 0.15, 0.024), frame=None, mat=FOIL, cap=False):
    """Ration pouch or drink pack: chamfered soft body, crimped top seal, printed label; drink packs get a cap."""
    cx, cy, cz = centre
    w, h, d = size
    m.box(mat, centre, size, min(0.008, d * 0.4), frame)
    m.box(mat, (cx, cy + h / 2 + 0.007, cz - d * 0.2), (w * 0.96, 0.016, 0.003), 0.0008, frame)
    m.decal(hb_atlas, key, (cx, cy - h * 0.08, cz + d / 2), frame, width=w * 0.82, material=HB_LABELS)
    if cap:
        m.cyl(ANO_BLUE, (cx + w * 0.28, cy + h / 2 - 0.004, cz), (cx + w * 0.28, cy + h / 2 + 0.018, cz), 0.0075, 10,
              0.0015, frame)


def elastic(m, points, frame=None, mat=C_BLUE, radius=0.0028):
    m.tube(mat, fillet_path([vec(p) for p in points], 0.012, 4), radius, 6, 0.0, frame)


def placard(m, atlas, key, x, y, width, frame=None, material=HB_LABELS):
    entry = atlas["entries"][key]
    h = width * entry["h"] / entry["w"]
    m.box(PLASTIC, (x, y, GAP + 0.001), (width + 0.012, h + 0.010, 0.002), 0.0006, frame)
    m.decal(atlas, key, (x, y, GAP + 0.002), frame, width=width, material=material)


def notebook(m, x, y, frame=None, w=0.10, h=0.14, t=0.012):
    """Hardcover notebook on a hook-and-loop patch: covers, spine, page block, elastic closure, tethered pen."""
    m.box(FAB, (x, y, GAP + 0.001), (w + 0.024, h + 0.024, 0.002), 0.0006, frame)
    z0 = GAP + 0.002
    m.box(FAB_DARK, (x, y, z0 + 0.00075), (w, h, 0.0015), 0.0004, frame)
    m.box(FAB_DARK, (x, y, z0 + t - 0.00075), (w, h, 0.0015), 0.0004, frame)
    m.box(FAB_DARK, (x - w / 2 + 0.0015, y, z0 + t / 2), (0.003, h, t), 0.0005, frame)
    m.box(PLASTIC, (x + 0.001, y, z0 + t / 2), (w - 0.006, h - 0.006, t - 0.0032), 0.0, frame)
    ex = x + w * 0.30
    m.box(RUBBER, (ex, y, z0 + t + 0.0006), (0.007, h + 0.0024, 0.0012), 0.0, frame)
    for s in (-1, 1):
        m.box(RUBBER, (ex, y + s * (h / 2 + 0.0006), z0 + t / 2), (0.007, 0.0012, t + 0.0024), 0.0, frame)
    px = x - w / 2 - 0.009
    m.cyl(ANO, (px, y - 0.055, z0 + 0.0055), (px, y + 0.060, z0 + 0.0055), 0.0045, 8, 0.001, frame)
    solid_lathe(m, ALU, (px, y - 0.055, z0 + 0.0055), (0, -1, 0), [(0.0045, 0.0), (0.0012, 0.014)], 8, frame)
    m.box(ALU, (px, y + 0.045, z0 + 0.0105), (0.004, 0.026, 0.0014), 0.0004, frame)
    m.tube(C_BLUE, [(px, y + 0.060, z0 + 0.0055), (px, y + 0.085, z0 + 0.010), (px + 0.012, y + 0.105, z0 + 0.006),
                    (px + 0.03, y + 0.112, z0 + 0.004)], 0.0012, 5, 0.0, frame)
    m.cyl(ALU, (px + 0.032, y + 0.112, GAP), (px + 0.032, y + 0.112, GAP + 0.004), 0.005, 8, 0.0, frame)


def photo(m, hb_atlas, key, x, y, frame=None):
    """Printed photo on a card, held by an elastic strap between two rivets."""
    e = hb_atlas["entries"][key]
    w, h = e["w"], e["h"]
    m.box(PLASTIC, (x, y, GAP + 0.0006), (w + 0.006, h + 0.006, 0.0012), 0.0, frame)
    m.decal(hb_atlas, key, (x, y, GAP + 0.0012), frame, width=w, lift=0.0005, material=HB_LABELS)
    sy = y - h * 0.32
    m.box(STRAP, (x, sy, GAP + 0.0022), (w + 0.044, 0.010, 0.0012), 0.0, frame)
    for s in (-1, 1):
        m.cyl(ALU, (x + s * (w / 2 + 0.017), sy, GAP), (x + s * (w / 2 + 0.017), sy, GAP + 0.0042), 0.0042, 8, 0.0008,
              frame)


def headphones(m, x, y, frame=None, R=0.082):
    """Over-ear headphones lying flat, hung from a hook-and-loop strap: band, yokes, cups with cushions, cable."""
    zc = GAP + 0.026
    a0 = math.radians(14.0)
    arc = [(x + R * math.cos(a), y + R * math.sin(a), zc) for a in np.linspace(a0, math.pi - a0, 13)]
    m.tube(ANO, arc, 0.0062, 8, 0.0, frame)
    pad = [(x + (R + 0.002) * math.cos(a), y + (R + 0.002) * math.sin(a), zc)
           for a in np.linspace(math.radians(55), math.radians(125), 7)]
    m.tube(FAB_DARK, pad, 0.0095, 8, 0.0, frame)
    for s in (-1, 1):
        ex, ey = x + s * R * math.cos(a0), y + R * math.sin(a0)
        cx, cy = x + s * R, y - 0.030
        m.tube(ALU, [(ex, ey, zc), (cx, cy + 0.036, zc)], 0.0035, 6, 0.0, frame)
        m.cyl(RUBBER, (cx, cy, GAP), (cx, cy, GAP + 0.012), 0.037, 16, 0.004, frame)
        m.cyl(ANO, (cx, cy, GAP + 0.012), (cx, cy, GAP + 0.038), 0.040, 16, 0.006, frame)
        m.cyl(GRAPHITE, (cx, cy, GAP + 0.038), (cx, cy, GAP + 0.0395), 0.026, 16, 0.0, frame)
    lx = x - R
    m.tube(RUBBER, [(lx, y - 0.068, GAP + 0.016), (lx + 0.004, y - 0.10, GAP + 0.006), (x - 0.04, y - 0.135, GAP + 0.003),
                    (x + 0.03, y - 0.125, GAP + 0.003), (x + 0.05, y - 0.105, GAP + 0.003)], 0.0018, 5, 0.012, frame)
    m.cyl(ALU, (x + 0.05, y - 0.105, GAP + 0.003), (x + 0.07, y - 0.09, GAP + 0.003), 0.0028, 8, 0.0005, frame)
    top = y + R
    strap(m, [(x, top + 0.07, GAP + 0.0012), (x, top + 0.012, zc + 0.008), (x, top - 0.016, zc + 0.008),
              (x, top - 0.05, GAP + 0.0012)], 0.022, 0.002, (1, 0, 0), STRAP, frame)
    m.box(FAB_DARK, (x, top + 0.075, GAP + 0.0015), (0.034, 0.03, 0.003), 0.0008, frame)
    m.box(FAB_DARK, (x, top - 0.055, GAP + 0.0015), (0.034, 0.03, 0.003), 0.0008, frame)


def rolled_bag(m, centre, r, length, frame=None):
    """Spare sleeping bag rolled up along local x: rounded roll, dark roll ends and two keeper bands."""
    cx, cy, cz = centre
    prof = [(r * 0.50, 0.0), (r * 0.82, 0.010), (r * 0.97, 0.035), (r, 0.065), (r, length - 0.065),
            (r * 0.97, length - 0.035), (r * 0.82, length - 0.010), (r * 0.50, length)]
    solid_lathe(m, HB_BAG, (cx - length / 2, cy, cz), (1, 0, 0), prof, 18, frame)
    for end in (-1, 1):
        ring_band(m, FAB_DARK, (cx + end * (length / 2 + 0.0006), cy, cz), (1, 0, 0), r * 0.18, r * 0.50, 0.0012, 14, frame)
    for k in (-1, 1):
        ring_band(m, STRAP, (cx + k * length * 0.36, cy, cz), (1, 0, 0), r + 0.0004, r + 0.0034, 0.022, 18, frame)


def wrap_strap(m, sx, cy, cz, r, y_top, y_bottom, frame=None, width=0.03):
    """Tie-down strap at x = sx from a top anchor rail, around the front of a roll (axis x) to a bottom rail."""
    R = r + 0.0045
    pts = [(sx, y_top, GAP + 0.0072)]
    for a in np.radians(np.linspace(68, -68, 9)):
        pts.append((sx, cy + R * math.sin(a), cz + R * math.cos(a)))
    pts.append((sx, y_bottom, GAP + 0.0072))
    strap(m, pts, width, 0.0024, (1, 0, 0), STRAP, frame)
    cam_buckle(m, (sx, cy, cz + R + 0.0012), (0, 1, 0), (0, 0, 1), width, frame)


# ----------------------------------------------------------------------------- plumbing
def sweep(m, mat, path, radii, segs=10, ref=(0.0, 0.0, 1.0), frame=None):
    """Closed tube along a dense path with a radius per ring (corrugated hoses)."""
    path = np.asarray(path, dtype=np.float64)
    radii = np.asarray(radii, dtype=np.float64)
    tangents, normals, binormals = frames_along(path, ref)
    ang = np.linspace(0.0, math.tau, segs, endpoint=False)
    rings = [path[i] + radii[i] * (np.outer(np.cos(ang), normals[i]) + np.outer(np.sin(ang), binormals[i]))
             for i in range(len(path))]
    v = np.concatenate(rings + [path[:1], path[-1:]], axis=0)
    n = len(path)
    c0, c1 = n * segs, n * segs + 1
    tris = []
    for i in range(n - 1):
        for k in range(segs):
            k1 = (k + 1) % segs
            a, b, c, d = i * segs + k, i * segs + k1, (i + 1) * segs + k1, (i + 1) * segs + k
            tris += [(a, b, c), (a, c, d)]
    for k in range(segs):
        k1 = (k + 1) % segs
        tris += [(c0, k1, k), (c1, (n - 1) * segs + k, (n - 1) * segs + k1)]
    t = np.asarray(tris, dtype=np.int64)
    if signed_volume(v, t) < 0:
        t = t[:, ::-1]
    m.add(mat, v, t, frame)


def resample(points, step):
    pts = np.asarray(points, dtype=np.float64)
    seg = np.linalg.norm(np.diff(pts, axis=0), axis=1)
    s = np.concatenate([[0.0], np.cumsum(seg)])
    count = max(2, int(math.ceil(s[-1] / step)) + 1)
    u = np.linspace(0.0, s[-1], count)
    return np.column_stack([np.interp(u, s, pts[:, i]) for i in range(3)])


def flex_hose(m, points, r=0.0085, fillet=0.04, pitch=0.007, frame=None, mat=RUBBER):
    """Corrugated flexible hose with crimped ferrules at both ends."""
    path = resample(fillet_path([vec(p) for p in points], fillet, 8), pitch / 2)
    radii = [r if i % 2 == 0 else r * 0.86 for i in range(len(path))]
    radii[0] = radii[-1] = r * 0.9
    sweep(m, mat, path, radii, 10, (0.0, 0.0, 1.0), frame)
    for end, nxt in ((path[0], path[1]), (path[-1], path[-2])):
        axis = unit(end - nxt)
        m.cyl(ALU, end - axis * 0.016, end + axis * 0.002, r + 0.0022, 10, 0.0008, frame)


def hex_union(m, centre, axis, r=0.0075, frame=None):
    axis = unit(axis)
    c = vec(centre)
    m.cyl(ALU, c - axis * 0.011, c + axis * 0.011, r + 0.0055, 6, 0.001, frame)
    m.cyl(ALU, c - axis * 0.017, c - axis * 0.011, r + 0.0030, 10, 0.0, frame)


def capped_valve(m, centre, frame=None, r=0.0075, out=(0.0, 0.0, 1.0)):
    """Small inline needle valve on a vertical pipe: hex body, bonnet, lock-wired knurled cap (no lever)."""
    c = vec(centre)
    out = unit(out)
    m.cyl(ALU, c - vec(0, 0.026, 0), c + vec(0, 0.026, 0), r + 0.0065, 6, 0.0012, frame)
    m.cyl(ALU, c + out * 0.008, c + out * 0.030, 0.0072, 10, 0.0008, frame)
    m.cyl(ANO, c + out * 0.030, c + out * 0.043, 0.0095, 12, 0.0015, frame)
    m.tube(ALU, [c + out * 0.036 + vec(0.0095, 0, 0), c + out * 0.036 + vec(0.016, -0.008, 0),
                 c + out * 0.012 + vec(r + 0.006, -0.018, 0)], 0.0006, 4, 0.0, frame)


def twin_clamp(m, xs, y, z, frame=None, r=0.0075):
    """Two-piece twin-pipe clamp block on a wall bracket: rubber collars, split line, two cap screws."""
    x0, x1 = min(xs) - 0.027, max(xs) + 0.027
    cx, w = (x0 + x1) / 2, x1 - x0
    m.box(ALU, (cx, y, GAP + 0.002), (w + 0.02, 0.034, 0.004), 0.001, frame)
    web_lo, web_hi = GAP + 0.0036, z - 0.0185 + 0.0004
    m.box(ALU, (cx, y, (web_lo + web_hi) / 2), (w - 0.02, 0.012, web_hi - web_lo), 0.0, frame)
    m.box(GRAPHITE, (cx, y, z - 0.0095), (w, 0.024, 0.0180), 0.002, frame)
    m.box(GRAPHITE, (cx, y, z + 0.0095), (w, 0.024, 0.0180), 0.002, frame)
    for x in xs:
        for s in (-1, 1):
            ring_band(m, RUBBER, (x, y + s * 0.0128, z), (0, 1, 0), r + 0.0002, r + 0.0030, 0.0016, 10, frame)
    for x in (x0 + 0.012, x1 - 0.012):
        m.cyl(ANO, (x, y, z + 0.0185), (x, y, z + 0.0225), 0.0042, 6, 0.0005, frame)
    for x in (x0 - 0.002, x1 + 0.002):
        m.cyl(ANO, (x, y, GAP + 0.004), (x, y, GAP + 0.0065), 0.004, 6, 0.0005, frame)


def bulkhead_fitting(m, base, axis, r=0.0085, frame=None):
    """Hose fitting on a panel face: hex flange, nipple, swivel nut. Returns the hose attachment point."""
    base, axis = vec(base), unit(axis)
    m.cyl(ALU, base, base + axis * 0.004, r + 0.010, 6, 0.0008, frame)
    m.cyl(ALU, base + axis * 0.004, base + axis * 0.016, r + 0.0025, 12, 0.0, frame)
    m.cyl(ALU, base + axis * 0.016, base + axis * 0.028, r + 0.0055, 6, 0.0010, frame)
    return base + axis * 0.028


# ----------------------------------------------------------------------------- habitat clusters
def stowage_cluster(atlas, hb_atlas, art):
    """Blanket bay opposite the berth: two soft bags under two cargo straps with cam buckles to the edge-strip
    tie-downs (kept above the bay handrail), and below the rail labelled ration pouches and drink packs held by
    elastic cords. The host plane is 3.5 mm in front of the quilt, clear of its hook-and-loop decals."""
    import a7_ship
    m = Model("StrapStow")
    front = 0.0
    for cx, cy, w, h, d, label in ((-0.33, 1.86, 0.58, 0.72, 0.20, "bag_05"), (0.33, 1.85, 0.58, 0.70, 0.23, "bag_09")):
        bag = a7_ship.soft_bag(art, (w, h, d), label, 0.03)
        placed = Model("bag")
        placed.instance(bag, at(cx, cy, GAP + d / 2 + 0.003))
        front = max(front, float(placed.bounds()[1][2]))
        m.instance(placed)
    for y in (1.60, 2.12):
        f = front + 0.003
        strap(m, [(-0.708, y, 0.0075), (-0.66, y, f * 0.55), (-0.628, y, f), (0.628, y, f), (0.66, y, f * 0.55),
                  (0.708, y, 0.0075)], 0.025, 0.0022, (0, 1, 0))
        cam_buckle(m, (0.0, y, f + 0.0011), (1, 0, 0), (0, 0, 1))
        for x in (-0.708, 0.708):
            strap_anchor(m, x, y, 0.001)
    groups = ((-0.39, ("pouch_breakfast", "pouch_meal", "pouch_snack"), "rations_meals", FOIL, False),
              (0.39, ("pouch_drink", "pouch_drink", "pouch_drink"), "rations_drinks", PLASTIC, True))
    for gx, keys, header, mat, cap in groups:
        for i, key in enumerate(keys):
            pouch(m, hb_atlas, key, (gx + (i - 1) * 0.13, 0.62, GAP + 0.012), (0.11, 0.15, 0.024), None, mat, cap)
        placard(m, hb_atlas, header, gx, 0.79, 0.20)
        xa, xb = gx - 0.245, gx + 0.245
        for x in (xa, xb):
            strap_anchor(m, x, 0.625, GAP)
        for yc in (0.585, 0.665):
            elastic(m, [(xa, 0.625, GAP + 0.017), (xa + 0.035, yc, GAP + 0.0275), (xb - 0.035, yc, GAP + 0.0275),
                        (xb, 0.625, GAP + 0.017)])
    return m


def crew_kit_cluster(atlas, hb_atlas):
    """Blanket bay beside the berth entrance: a spare sleeping bag rolled and held by two tie-down straps with cam
    buckles between anchor rails, and below it a personal board: notebook with tethered pen on hook-and-loop,
    a photo under an elastic strap and headphones hung from a hook-and-loop strap (all clear of the bay rail)."""
    m = Model("CrewKit")
    r, cy = 0.105, 2.12
    cz = GAP + r + 0.001
    rolled_bag(m, (0.0, cy, cz), r, 0.64)
    for y in (2.44, 1.80):
        m.box(ALU, (0.0, y, GAP + 0.003), (0.62, 0.026, 0.006), 0.0015)
        for x in (-0.27, 0.0, 0.27):
            m.cyl(ANO, (x, y, GAP + 0.006), (x, y, GAP + 0.0085), 0.0045, 6, 0.0005)
    for sx in (-0.19, 0.19):
        wrap_strap(m, sx, cy, cz, r, 2.44, 1.80)
    notebook(m, -0.45, 1.52)
    photo(m, hb_atlas, "photo_hills", -0.21, 1.55)
    headphones(m, 0.36, 1.52)
    return m


def grow_plumbing_cluster(atlas, hb_atlas):
    """Wall strip between the grow cassette (its side face at local x = -0.105) and the ring frame: potable supply
    and return lines from the deck trough, twin clamp blocks on brackets, capped needle valves, unions and
    corrugated flex hoses to bulkhead fittings on the cassette's side face; a 'potable' placard."""
    m = Model("GrowPlumbing")
    face = -0.105 + GAP
    lines = ((-0.035, 0.80, 0.85, 0.10, 0.24, ANO_BLUE, 0.42), (0.045, 0.70, 0.75, 0.16, 0.33, ALU, 0.36))
    for x, y_bend, y_union, z_union, z_fit, mat, y_valve in lines:
        m.tube(mat, fillet_path([vec(x, 0.1215, 0.040), vec(x, y_bend, 0.040), vec(x, y_union, z_union - 0.017)], 0.03, 6),
               0.0075, 10)
        ring_band(m, RUBBER, (x, 0.1235, 0.040), (0, 1, 0), 0.0078, 0.013, 0.006, 12)
        capped_valve(m, (x, y_valve, 0.040))
        hex_union(m, (x, y_union, z_union), (0, 0, 1))
        end = bulkhead_fitting(m, (face, y_union, z_fit), (1, 0, 0))
        flex_hose(m, [vec(x, y_union, z_union + 0.012), vec(x, y_union, z_fit - 0.02), vec(x - 0.03, y_union, z_fit),
                      end + vec(0.002, 0, 0)], 0.0085, 0.035)
    twin_clamp(m, (-0.035, 0.045), 0.22, 0.040)
    twin_clamp(m, (-0.035, 0.045), 0.62, 0.040)
    placard(m, hb_atlas, "potable", 0.005, 1.02, 0.15)
    return m


TWIN = [(0.0, 0.0062, 0.0060, RUBBER), (0.0, -0.0062, 0.0050, C_YELLOW)]


def grow_cable_cluster(atlas, hb_atlas):
    """Wall strip aft of the grow cassette (its side face at local x = +0.105): the grow-light feed, two cables in
    cushioned P-clamps on standoffs from the deck trough up to two plug connectors with backshells on the
    cassette's side face, with ties and connector labels."""
    m = Model("GrowCables")
    lx, lz, bundle_r = 0.0, 0.0335, 0.0125
    top = 1.66
    loom(m, [(lx, 0.1215, lz), (lx, top, lz)], TWIN, 0.0, (0.0, 0.0, 1.0))
    ring_band(m, RUBBER, (lx, 0.1235, lz), (0, 1, 0), bundle_r + 0.0003, bundle_r + 0.006, 0.006, 12)
    for y in (0.45, 0.85, 1.25):
        p_clamp(m, y, lx, lz, bundle_r, None, -1)
    for y in (0.65, 1.05, 1.45):
        cable_tie(m, (lx, y, lz), (0, 1, 0), bundle_r)
    face = 0.105 - GAP
    _, normals, binormals = frames_along([(lx, 0.1215, lz), (lx, top, lz)], (0.0, 0.0, 1.0))
    for (on, ob, radius, mat), zc, label in zip(TWIN, (0.050, 0.098), (14, 15)):
        end = round_connector(m, (face, 1.80, zc), (-1, 0, 0), None, radius)
        start = vec(lx, top, lz) + on * normals[-1] + ob * binormals[-1]
        m.tube(mat, fillet_path([start, start + vec(0, 0.06, 0), end + vec(-0.035, 0, 0), end], 0.035, 6), radius, 7)
        m.decal(atlas, "conn_%02d" % label, (0.0, 0.0, 0.0), front_frame((face, 1.80 + 0.03, zc), (-1, 0, 0)), width=0.024)
    return m
