"""lss_kit - LAND-SURROUNDINGS-V01: low static surroundings for the AURORA-7 land launch site, in prologue-local Unity
metres (y up), every object pivoted at the prologue origin. Original procedural geometry, fixed seeds.

Keeps clear of (with margins): the launch foundation x[-72,72] z[-62,72], the plume/outlet corridor x[-35,35]
z[60,320], the crawlerway corridor x[-55,95] z[-1020,-50] (it contains the facility). Nothing taller than 15 m;
no mountains, no water.

Groups (one object each):
  LSS_GroundBreakup   low, wide grass/sand mounds that break up the flat ground colour (0.15-0.45 m high)
  LSS_Scrub           coastal scrub clumps (0.6-1.7 m)
  LSS_TreeLines       broken tree lines and hammocks at 450-2600 m (7-15 m)
  LSS_Berms           low earth berms at 350-950 m (2-4 m)
  LSS_Drainage        riprap rock clusters along two drainage lines, concrete culvert headwalls
  LSS_Fence           perimeter security fence (posts and two rails) with a gate gap for the crawlerway
  LSS_UtilityPoles    concrete poles with crossarms and sagging wires east of the crawlerway corridor
"""
import math

import numpy as np

import a7kit
from a7kit import Model

G = -36.6
FOUND = (-72.0, 72.0, -62.0, 72.0)
PLUME = (-35.0, 35.0, 60.0, 320.0)
CORRIDOR = (-55.0, 95.0, -1020.0, -50.0)
FENCE = (-200.0, 200.0, -200.0, 330.0)        # x0, x1, z0, z1
GATE = (-62.0, 102.0)                          # north side gap for the crawlerway corridor
POLE_X, POLE_Z = 112.0, (-70.0, -910.0)


def inside(r, x, z, m=0.0):
    return r[0] - m <= x <= r[1] + m and r[2] - m <= z <= r[3] + m


def near_fence(x, z, m):
    x0, x1, z0, z1 = FENCE
    on_x = (abs(x - x0) < m or abs(x - x1) < m) and z0 - m <= z <= z1 + m
    on_z = (abs(z - z0) < m or abs(z - z1) < m) and x0 - m <= x <= x1 + m
    return on_x or on_z


def blocked(x, z, m):
    return (inside(FOUND, x, z, m) or inside(PLUME, x, z, m) or inside(CORRIDOR, x, z, m) or near_fence(x, z, 3.0 + m)
            or (abs(x - POLE_X) < 6.0 + m and POLE_Z[1] - 10 <= z <= POLE_Z[0] + 10))


def up_facing(v, t):
    """Flip triangles whose normal points down (Unity space, y up)."""
    a, b, c = v[t[:, 0]], v[t[:, 1]], v[t[:, 2]]
    n = np.cross(b - a, c - a)
    t = t.copy()
    flip = n[:, 1] < 0
    t[flip] = t[flip][:, ::-1]
    return t


def mound(m, mat, cx, cz, rx, rz, h, n, rng, spin=0.0):
    """Low irregular mound: centre, a mid ring and an outer ring on the ground (3n triangles)."""
    ang = spin + np.arange(n) * 2 * math.pi / n
    jo = 1.0 + 0.22 * (rng.random(n) - 0.5)
    jm = 1.0 + 0.18 * (rng.random(n) - 0.5)
    outer = np.column_stack([cx + rx * jo * np.cos(ang), np.full(n, G + 0.02), cz + rz * jo * np.sin(ang)])
    mid = np.column_stack([cx + 0.58 * rx * jm * np.cos(ang), G + h * (0.62 + 0.25 * rng.random(n)),
                           cz + 0.58 * rz * jm * np.sin(ang)])
    v = np.vstack([[[cx, G + h, cz]], mid, outer])
    t = []
    for i in range(n):
        j = (i + 1) % n
        t += [(0, 1 + i, 1 + j), (1 + i, 1 + n + i, 1 + n + j), (1 + i, 1 + n + j, 1 + j)]
    t = np.asarray(t)
    m.add(mat, v, up_facing(v, t))


def scatter(rng, count, r0, r1, size, pad, power=1.0):
    """Points (x, z, r) in the ring r0..r1 whose whole footprint (r plus pad) stays out of every blocked zone."""
    pts, tries = [], 0
    while len(pts) < count and tries < count * 400:
        tries += 1
        r = r0 + (r1 - r0) * rng.random() ** power
        a = rng.random() * 2 * math.pi
        x, z = r * math.cos(a), r * math.sin(a)
        rad = size(rng)
        if not blocked(x, z, rad + pad):
            pts.append((x, z, rad))
    return pts


def ground_breakup():
    m = Model("LSS_GroundBreakup")
    rng = np.random.default_rng(301)
    for x, z, r in scatter(rng, 80, 95.0, 1500.0, lambda g: 18.0 + 42.0 * g.random(), 4.0, 1.6):
        mound(m, "LSS_GrassDry", x, z, r, r * (0.45 + 0.5 * rng.random()), 0.15 + 0.3 * rng.random(), 10, rng,
              rng.random() * math.pi)
    return m


def scrub():
    m = Model("LSS_Scrub")
    rng = np.random.default_rng(302)
    for x, z, r in scatter(rng, 160, 80.0, 950.0, lambda g: 2.0 + 4.5 * g.random(), 3.0, 1.8):
        mound(m, "LSS_Scrub", x, z, r, r * (0.6 + 0.4 * rng.random()), 0.6 + 1.1 * rng.random(), 7, rng,
              rng.random() * math.pi)
    return m


def strip(m, mat, pts, rng, h0, h1, d0, d1):
    """Canopy band along a polyline: 5-vertex cross-sections with noisy height and depth."""
    pts = np.asarray(pts, float)
    secs = []
    for i, p in enumerate(pts):
        a = pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]
        a = a / (np.linalg.norm(a) + 1e-9)
        nrm = np.array([-a[1], a[0]])
        h = h0 + (h1 - h0) * rng.random()
        d = d0 + (d1 - d0) * rng.random()
        prof = [(-d / 2, G), (-0.42 * d, G + 0.62 * h), (0.0, G + h), (0.42 * d, G + 0.66 * h), (d / 2, G)]
        secs.append([(p[0] + nrm[0] * o, y, p[1] + nrm[1] * o) for o, y in prof])
    v = np.asarray(secs, float).reshape(-1, 3)
    k, n = 5, len(pts)
    hm = G + 0.3 * (h0 + h1) / 2
    spine = np.array([[p[0], hm, p[1]] for p in pts])
    t, ref = [], []
    for sct in range(n - 1):
        for j in range(k - 1):
            a0, a1, b0, b1 = sct * k + j, sct * k + j + 1, (sct + 1) * k + j, (sct + 1) * k + j + 1
            t += [(a0, b0, b1), (a0, b1, a1)]
            mid = (spine[sct] + spine[sct + 1]) / 2
            ref += [mid, mid]
    for sct, nb in ((0, 1), (n - 1, n - 2)):     # end caps face away from the neighbouring section
        base = sct * k
        t += [(base, base + 1, base + 2), (base, base + 2, base + 4), (base + 2, base + 3, base + 4)]
        ref += [spine[nb]] * 3
    t, ref = np.asarray(t), np.asarray(ref)
    a, b, c = v[t[:, 0]], v[t[:, 1]], v[t[:, 2]]
    flip = np.einsum("ij,ij->i", np.cross(b - a, c - a), (a + b + c) / 3.0 - ref) < 0
    t[flip] = t[flip][:, ::-1]
    m.add(mat, v, t)


def arc(cx, cz, r, a0, a1, step, rng, wobble):
    """Points along an arc (angles in degrees) every `step` metres, with a smooth radial meander plus small jitter."""
    n = max(2, int(math.radians(abs(a1 - a0)) * r / step))
    phase, k = rng.random() * 6.28, 3.0 + 4.0 * rng.random()
    out = []
    for i in range(n + 1):
        a = math.radians(a0 + (a1 - a0) * i / n)
        rr = r + wobble * math.sin(k * a + phase) + 0.25 * wobble * (rng.random() - 0.5)
        out.append((cx + rr * math.cos(a), cz + rr * math.sin(a)))
    return out


def tree_lines():
    m = Model("LSS_TreeLines")
    rng = np.random.default_rng(303)
    # broken outer tree lines (angles in the x-z plane: 0 = +x, 90 = +z); the line behind the facility (z -960) is at
    # 2.1 km and 8-15 m tall, so the 90 m building stays fully visible in front of it
    for r, a0, a1 in ((1900.0, -60.0, 10.0), (2300.0, 18.0, 95.0), (2000.0, 105.0, 170.0), (2500.0, 178.0, 238.0),
                      (2100.0, 244.0, 298.0), (2200.0, 304.0, 345.0), (1450.0, 200.0, 228.0), (1600.0, 312.0, 330.0)):
        strip(m, "LSS_Canopy", arc(0.0, 0.0, r, a0, a1, 34.0, rng, 60.0), rng, 10.0, 18.0, 12.0, 26.0)
    # a broken middle ring at about 1.1 km gives depth; the north sector towards the facility stays open
    for r, a0, a1 in ((1100.0, 22.0, 68.0), (1180.0, 112.0, 158.0), (1060.0, 192.0, 232.0), (1220.0, 306.0, 338.0)):
        strip(m, "LSS_Canopy", arc(0.0, 0.0, r, a0, a1, 30.0, rng, 45.0), rng, 8.0, 15.0, 10.0, 20.0)
    # nearer hammocks (short clumps) at 450-900 m; every point of a clump must be clear
    made = 0
    while made < 9:
        x, z, _ = scatter(rng, 1, 450.0, 900.0, lambda g: 0.0, 80.0)[0]
        a = math.radians(rng.random() * 360.0)
        L = 60.0 + 90.0 * rng.random()
        pts = [(x + (q - L / 2) * math.cos(a) + 6 * (rng.random() - 0.5), z + (q - L / 2) * math.sin(a) + 6 * (rng.random() - 0.5))
               for q in np.arange(0.0, L + 1.0, 10.0)]
        if any(blocked(px, pz, 12.0) for px, pz in pts):
            continue
        strip(m, "LSS_Canopy", pts, rng, 6.0, 11.0, 8.0, 16.0)
        made += 1
    return m


def berms():
    m = Model("LSS_Berms")
    rng = np.random.default_rng(304)
    for (x0, z0, x1, z1) in ((330.0, -260.0, 470.0, -120.0), (520.0, 60.0, 560.0, 240.0), (-480.0, -60.0, -340.0, 120.0),
                             (-260.0, 520.0, -60.0, 560.0), (160.0, 600.0, 330.0, 470.0), (-620.0, -420.0, -470.0, -300.0)):
        pts = [(x0 + (x1 - x0) * s, z0 + (z1 - z0) * s) for s in np.linspace(0.0, 1.0, 14)]
        strip(m, "LSS_GrassDry", pts, rng, 2.0, 4.0, 14.0, 20.0)
    return m


def rock(m, mat, c, r, rng):
    """Jittered icosahedron sunk 30 % into the ground (20 triangles, no slivers)."""
    p = (1 + 5 ** 0.5) / 2
    v = np.array([(-1, p, 0), (1, p, 0), (-1, -p, 0), (1, -p, 0), (0, -1, p), (0, 1, p), (0, -1, -p), (0, 1, -p),
                  (p, 0, -1), (p, 0, 1), (-p, 0, -1), (-p, 0, 1)], float)
    v /= np.linalg.norm(v, axis=1)[:, None]
    t = np.array([(0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11), (1, 5, 9), (5, 11, 4), (11, 10, 2),
                  (10, 7, 6), (7, 1, 8), (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9), (4, 9, 5), (2, 4, 11),
                  (6, 2, 10), (8, 6, 7), (9, 8, 1)])
    v = v * (1.0 + 0.28 * (rng.random((12, 1)) - 0.5)) * np.array([r, 0.62 * r, r * (0.7 + 0.5 * rng.random())])
    v = v + np.array([c[0], G + 0.3 * 0.62 * r, c[1]])
    m.add(mat, v, a7kit._outward_convex(v, t, np.array([c[0], G + 0.3 * 0.62 * r, c[1]])))


def drainage():
    m = Model("LSS_Drainage")
    rng = np.random.default_rng(305)
    lines = [((-100.0, -40.0), (-100.0, 250.0)), ((48.0, 150.0), (190.0, 150.0))]
    culverts = []
    for (ax, az), (bx, bz) in lines:
        L = math.hypot(bx - ax, bz - az)
        for s in np.arange(10.0, L - 5.0, 22.0):
            cx, cz = ax + (bx - ax) * s / L, az + (bz - az) * s / L
            for _ in range(2 + int(rng.random() * 3)):
                rock(m, "LSS_Rock", (cx + 3.0 * (rng.random() - 0.5), cz + 3.0 * (rng.random() - 0.5)),
                     0.35 + 0.6 * rng.random(), rng)
        culverts += [(ax, az, bx - ax, bz - az), (bx, bz, ax - bx, az - bz)]
    for x, z, dx, dz in culverts:
        d = np.array([dx, dz]) / math.hypot(dx, dz)
        yaw = math.atan2(d[0], d[1])
        frame = np.eye(4)
        cy, sy = math.cos(yaw), math.sin(yaw)
        frame[:3, :3] = np.array([[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]])
        frame[:3, 3] = (x, G, z)
        m.box("LS_Concrete", (0.0, 0.55, 0.0), (3.2, 1.1, 0.35), 0.0, m=frame)
        m.cyl("A7_PaintGraphite", (0.0, 0.5, -0.2), (0.0, 0.5, 0.2), 0.38, 10, m=frame)
    return m, [((ax + bx) / 2, (az + bz) / 2) for (ax, az), (bx, bz) in lines]


def fence():
    m = Model("LSS_Fence")
    x0, x1, z0, z1 = FENCE
    runs = [((x0, z0), (GATE[0], z0)), ((GATE[1], z0), (x1, z0)), ((x1, z0), (x1, z1)), ((x1, z1), (x0, z1)),
            ((x0, z1), (x0, z0))]
    for (ax, az), (bx, bz) in runs:
        L = math.hypot(bx - ax, bz - az)
        n = max(1, int(round(L / 8.0)))
        for i in range(n + 1):
            px, pz = ax + (bx - ax) * i / n, az + (bz - az) * i / n
            m.box("A7_Aluminum", (px, G + 1.25, pz), (0.09, 2.5, 0.09), 0.0)
        cx, cz = (ax + bx) / 2, (az + bz) / 2
        along_x = abs(bx - ax) > abs(bz - az)
        for y, th in ((2.38, 0.06), (1.2, 0.04)):
            size = (L, th, th) if along_x else (th, th, L)
            m.box("A7_Aluminum", (cx, G + y, cz), size, 0.0)
    return m


def utility_poles():
    m = Model("LSS_UtilityPoles")
    za, zb = POLE_Z
    zs = np.linspace(za, zb, int(abs(zb - za) // 60) + 1)
    tops = []
    for z in zs:
        m.cyl("LS_ConcreteBarrier", (POLE_X, G, z), (POLE_X, G + 11.0, z), 0.16, 6, caps=(False, True))
        m.box("A7_PaintGraphite", (POLE_X, G + 10.3, z), (2.4, 0.12, 0.12), 0.0)
        tops.append(z)
    for za_, zb_ in zip(tops, tops[1:]):
        for wx in (-1.05, 0.0, 1.05):
            y = G + 10.42 if wx else G + 10.42
            mid = (za_ + zb_) / 2
            for (p0, p1) in (((POLE_X + wx, y, za_), (POLE_X + wx, y - 0.9, mid)), ((POLE_X + wx, y - 0.9, mid), (POLE_X + wx, y, zb_))):
                m.cyl("A7_PaintGraphite", p0, p1, 0.016, 4, caps=(False, False))
    return m


def build_kit():
    d, drain_mid = drainage()
    models = [ground_breakup(), scrub(), tree_lines(), berms(), d, fence(), utility_poles()]
    anchors = {
        "LSS_FenceGate_North": ((GATE[0] + GATE[1]) / 2, G, FENCE[2]),
        "LSS_FenceCorner_SW": (FENCE[0], G, FENCE[3]),
        "LSS_FenceCorner_NE": (FENCE[1], G, FENCE[2]),
        "LSS_PoleLine_North": (POLE_X, G, POLE_Z[1]),
        "LSS_PoleLine_South": (POLE_X, G, POLE_Z[0]),
        "LSS_Drainage_West_Mid": (drain_mid[0][0], G, drain_mid[0][1]),
        "LSS_Drainage_South_Mid": (drain_mid[1][0], G, drain_mid[1][1]),
    }
    return models, anchors
