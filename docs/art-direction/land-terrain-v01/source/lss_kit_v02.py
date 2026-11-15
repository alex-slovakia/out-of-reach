"""lss_kit_v02 - LAND-SURROUNDINGS v02: the LAND-SURROUNDINGS v01 kit (same seeds, same layout, same groups) re-seated
on the LAND-TERRAIN-V01 ground, in prologue-local Unity metres (y up), every object pivoted at the prologue origin.

v01 stood everything on the constant datum G = -36.6. v02 takes the ground from the terrain instead: lt_surface.Surface,
the piecewise-linear surface of the delivered terrain meshes (the analytic h deviates from the mesh between vertices).
Every random draw happens in the same order as in v01, so every mound, clump, tree line, berm, rock, post and pole is
where v01 put it; only heights change:
  LSS_GroundBreakup_v02  mounds follow the surface: 4 rings, the v01 segments subdivided 2x (jitter interpolated),
                         0.15 m higher than v01 (0.30-0.60 m) so they clear the terrain mesh, outer ring 0.12 m below
                         the ground so the edge cuts cleanly into it. Material LT_Terrain with the terrain's macro UV0
                         (v01 LSS_GrassDry read as dark stains on the new ground), so they are low hummocks in the
                         ground's own colour;
  LSS_Scrub_v02          v01 clump shape on the surface, outer ring 0.15 m below the ground;
  LSS_TreeLines_v02      every cross-section vertex on the surface; the two bottom vertices 0.8 m below it;
  LSS_Berms_v02          the same on the surface, bottoms 0.3 m below it;
  LSS_Drainage_v02       riprap rocks seated on the lowest ground under each rock and sunk 35 %; culvert headwalls
                         0.15 m below the lowest ground under them (the terrain has 0.7 m ditches on these lines);
  LSS_Fence_v02          posts from 0.3 m below the ground to 2.5 m above it; the two rails run post to post at
                         2.38 m and 1.2 m above the ground (square 4-sided tubes, 6 cm and 4 cm);
  LSS_UtilityPoles_v02   poles from 0.5 m below the ground to 11 m above it, crossarms at 10.3 m, wires sag 0.9 m
                         between crossarms at their own heights.
Anchors keep the v01 names plus _v02 and sit on the ground.
"""
import math

import numpy as np

import a7kit
from a7kit import Model
import lt_height as H
import lt_surface

SUFFIX = "_v02"
FOUND = (-72.0, 72.0, -62.0, 72.0)
PLUME = (-35.0, 35.0, 60.0, 320.0)
CORRIDOR = (-55.0, 95.0, -1020.0, -50.0)
FENCE = (-200.0, 200.0, -200.0, 330.0)
GATE = (-62.0, 102.0)
POLE_X, POLE_Z = 112.0, (-70.0, -910.0)
BASES = {}


def S():
    return lt_surface.surface()


def gy(x, z):
    return S().y(x, z)


def base(name, x, y, z):
    BASES.setdefault(name, []).append((float(x), float(y), float(z)))


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
    a, b, c = v[t[:, 0]], v[t[:, 1]], v[t[:, 2]]
    n = np.cross(b - a, c - a)
    t = t.copy()
    flip = n[:, 1] < 0
    t[flip] = t[flip][:, ::-1]
    return t


def _periodic(values, sub):
    n = len(values)
    k = np.arange(n * sub) / sub
    i0 = np.floor(k).astype(int) % n
    f = k - np.floor(k)
    return values[i0] * (1 - f) + values[(i0 + 1) % n] * f


def mound(m, mat, cx, cz, rx, rz, h, n, rng, spin=0.0, sub=1, bury=0.12, lift=0.0, macro_uv=False):
    """v01 mound draws (outer jitter, mid jitter, mid heights), re-seated on the terrain-mesh ground. With macro_uv
    the UV0 is the terrain's macro mapping (lt_height.uv_axis), for the LT_Terrain material."""
    jo = 1.0 + 0.22 * (rng.random(n) - 0.5)
    jm = 1.0 + 0.18 * (rng.random(n) - 0.5)
    mf = 0.62 + 0.25 * rng.random(n)
    if sub > 1:
        jo, jm, mf = _periodic(jo, sub), _periodic(jm, sub), _periodic(mf, sub)
        n *= sub
    hh = h + lift
    ang = spin + np.arange(n) * 2 * math.pi / n
    if sub > 1:
        rings = [(0.30, jm, 0.5 * (1.0 + mf) * hh), (0.58, jm, mf * hh), (0.80, 0.5 * (jm + jo), 0.35 * mf * hh - 0.4 * bury),
                 (1.00, jo, np.full(n, -bury))]
    else:
        rings = [(0.58, jm, mf * hh), (1.00, jo, np.full(n, -bury))]
    pts = [np.array([[cx, gy(cx, cz) + hh, cz]])]
    for rho, jit, hgt in rings:
        x = cx + rx * rho * jit * np.cos(ang)
        z = cz + rz * rho * jit * np.sin(ang)
        pts.append(np.column_stack([x, gy(x, z) + hgt, z]))
    v = np.vstack(pts)
    t = [(0, 1 + i, 1 + (i + 1) % n) for i in range(n)]
    for k in range(len(rings) - 1):
        o0, o1 = 1 + k * n, 1 + (k + 1) * n
        for i in range(n):
            j = (i + 1) % n
            t += [(o0 + i, o1 + i, o1 + j), (o0 + i, o1 + j, o0 + j)]
    t = up_facing(v, np.asarray(t))
    uv = np.column_stack([H.uv_axis(v[:, 0]), H.uv_axis(v[:, 2])])[t] if macro_uv else None
    m.add(mat, v, t, uv=uv)
    for p in pts[-1]:
        base(m.name, *p)


def scatter(rng, count, r0, r1, size, pad, power=1.0):
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
    m = Model("LSS_GroundBreakup" + SUFFIX)
    rng = np.random.default_rng(301)
    for x, z, r in scatter(rng, 80, 95.0, 1500.0, lambda g: 18.0 + 42.0 * g.random(), 4.0, 1.6):
        mound(m, "LT_Terrain", x, z, r, r * (0.45 + 0.5 * rng.random()), 0.15 + 0.3 * rng.random(), 10, rng,
              rng.random() * math.pi, sub=2, bury=0.12, lift=0.15, macro_uv=True)
    return m


def scrub():
    m = Model("LSS_Scrub" + SUFFIX)
    rng = np.random.default_rng(302)
    for x, z, r in scatter(rng, 160, 80.0, 950.0, lambda g: 2.0 + 4.5 * g.random(), 3.0, 1.8):
        mound(m, "LSS_Scrub", x, z, r, r * (0.6 + 0.4 * rng.random()), 0.6 + 1.1 * rng.random(), 7, rng,
              rng.random() * math.pi, sub=1, bury=0.15)
    return m


def strip(m, mat, pts, rng, h0, h1, d0, d1, bury):
    """v01 canopy/berm band (same draws), every cross-section vertex on the ground, bottoms `bury` m below it."""
    pts = np.asarray(pts, float)
    secs, spine = [], []
    for i, p in enumerate(pts):
        a = pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]
        a = a / (np.linalg.norm(a) + 1e-9)
        nrm = np.array([-a[1], a[0]])
        h = h0 + (h1 - h0) * rng.random()
        d = d0 + (d1 - d0) * rng.random()
        prof = [(-d / 2, -bury), (-0.42 * d, 0.62 * h), (0.0, h), (0.42 * d, 0.66 * h), (d / 2, -bury)]
        sec = []
        for o, y in prof:
            x, z = p[0] + nrm[0] * o, p[1] + nrm[1] * o
            sec.append((x, gy(x, z) + y, z))
        secs.append(sec)
        base(m.name, *sec[0])
        base(m.name, *sec[-1])
        spine.append((p[0], gy(p[0], p[1]) + 0.3 * (h0 + h1) / 2, p[1]))
    v = np.asarray(secs, float).reshape(-1, 3)
    spine = np.asarray(spine)
    k, n = 5, len(pts)
    t, ref = [], []
    for sct in range(n - 1):
        for j in range(k - 1):
            a0, a1, b0, b1 = sct * k + j, sct * k + j + 1, (sct + 1) * k + j, (sct + 1) * k + j + 1
            t += [(a0, b0, b1), (a0, b1, a1)]
            mid = (spine[sct] + spine[sct + 1]) / 2
            ref += [mid, mid]
    for sct, nb in ((0, 1), (n - 1, n - 2)):
        b = sct * k
        t += [(b, b + 1, b + 2), (b, b + 2, b + 4), (b + 2, b + 3, b + 4)]
        ref += [spine[nb]] * 3
    t, ref = np.asarray(t), np.asarray(ref)
    a, b, c = v[t[:, 0]], v[t[:, 1]], v[t[:, 2]]
    flip = np.einsum("ij,ij->i", np.cross(b - a, c - a), (a + b + c) / 3.0 - ref) < 0
    t[flip] = t[flip][:, ::-1]
    m.add(mat, v, t)


def arc(cx, cz, r, a0, a1, step, rng, wobble):
    n = max(2, int(math.radians(abs(a1 - a0)) * r / step))
    phase, k = rng.random() * 6.28, 3.0 + 4.0 * rng.random()
    out = []
    for i in range(n + 1):
        a = math.radians(a0 + (a1 - a0) * i / n)
        rr = r + wobble * math.sin(k * a + phase) + 0.25 * wobble * (rng.random() - 0.5)
        out.append((cx + rr * math.cos(a), cz + rr * math.sin(a)))
    return out


def tree_lines():
    m = Model("LSS_TreeLines" + SUFFIX)
    rng = np.random.default_rng(303)
    for r, a0, a1 in ((1900.0, -60.0, 10.0), (2300.0, 18.0, 95.0), (2000.0, 105.0, 170.0), (2500.0, 178.0, 238.0),
                      (2100.0, 244.0, 298.0), (2200.0, 304.0, 345.0), (1450.0, 200.0, 228.0), (1600.0, 312.0, 330.0)):
        strip(m, "LSS_Canopy", arc(0.0, 0.0, r, a0, a1, 34.0, rng, 60.0), rng, 10.0, 18.0, 12.0, 26.0, 0.8)
    for r, a0, a1 in ((1100.0, 22.0, 68.0), (1180.0, 112.0, 158.0), (1060.0, 192.0, 232.0), (1220.0, 306.0, 338.0)):
        strip(m, "LSS_Canopy", arc(0.0, 0.0, r, a0, a1, 30.0, rng, 45.0), rng, 8.0, 15.0, 10.0, 20.0, 0.8)
    made = 0
    while made < 9:
        x, z, _ = scatter(rng, 1, 450.0, 900.0, lambda g: 0.0, 80.0)[0]
        a = math.radians(rng.random() * 360.0)
        L = 60.0 + 90.0 * rng.random()
        pts = [(x + (q - L / 2) * math.cos(a) + 6 * (rng.random() - 0.5), z + (q - L / 2) * math.sin(a) + 6 * (rng.random() - 0.5))
               for q in np.arange(0.0, L + 1.0, 10.0)]
        if any(blocked(px, pz, 12.0) for px, pz in pts):
            continue
        strip(m, "LSS_Canopy", pts, rng, 6.0, 11.0, 8.0, 16.0, 0.8)
        made += 1
    return m


def berms():
    m = Model("LSS_Berms" + SUFFIX)
    rng = np.random.default_rng(304)
    for (x0, z0, x1, z1) in ((330.0, -260.0, 470.0, -120.0), (520.0, 60.0, 560.0, 240.0), (-480.0, -60.0, -340.0, 120.0),
                             (-260.0, 520.0, -60.0, 560.0), (160.0, 600.0, 330.0, 470.0), (-620.0, -420.0, -470.0, -300.0)):
        pts = [(x0 + (x1 - x0) * s, z0 + (z1 - z0) * s) for s in np.linspace(0.0, 1.0, 14)]
        strip(m, "LSS_GrassDry", pts, rng, 2.0, 4.0, 14.0, 20.0, 0.3)
    return m


def rock(m, mat, c, r, rng):
    """v01 riprap rock (same draws): 20 triangles, sunk 35 % below the lowest ground under it."""
    p = (1 + 5 ** 0.5) / 2
    v = np.array([(-1, p, 0), (1, p, 0), (-1, -p, 0), (1, -p, 0), (0, -1, p), (0, 1, p), (0, -1, -p), (0, 1, -p),
                  (p, 0, -1), (p, 0, 1), (-p, 0, -1), (-p, 0, 1)], float)
    v /= np.linalg.norm(v, axis=1)[:, None]
    t = np.array([(0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11), (1, 5, 9), (5, 11, 4), (11, 10, 2),
                  (10, 7, 6), (7, 1, 8), (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9), (4, 9, 5), (2, 4, 11),
                  (6, 2, 10), (8, 6, 7), (9, 8, 1)])
    v = v * (1.0 + 0.28 * (rng.random((12, 1)) - 0.5)) * np.array([r, 0.62 * r, r * (0.7 + 0.5 * rng.random())])
    seat = S().seat(c[0], c[1], r)
    centre = np.array([c[0], seat + 0.3 * 0.62 * r, c[1]])
    v = v + centre
    m.add(mat, v, a7kit._outward_convex(v, t, centre))
    low = v[np.argmin(v[:, 1])]
    base(m.name, *low)


def drainage():
    m = Model("LSS_Drainage" + SUFFIX)
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
        y0 = S().seat(x, z, 1.7) - 0.15
        frame[:3, 3] = (x, y0, z)
        m.box("LS_Concrete", (0.0, 0.55, 0.0), (3.2, 1.1, 0.35), 0.0, m=frame)
        m.cyl("A7_PaintGraphite", (0.0, 0.5, -0.2), (0.0, 0.5, 0.2), 0.38, 10, m=frame)
        base(m.name, x, y0, z)
    return m, [((ax + bx) / 2, (az + bz) / 2) for (ax, az), (bx, bz) in lines]


def fence():
    m = Model("LSS_Fence" + SUFFIX)
    x0, x1, z0, z1 = FENCE
    runs = [((x0, z0), (GATE[0], z0)), ((GATE[1], z0), (x1, z0)), ((x1, z0), (x1, z1)), ((x1, z1), (x0, z1)),
            ((x0, z1), (x0, z0))]
    for (ax, az), (bx, bz) in runs:
        L = math.hypot(bx - ax, bz - az)
        n = max(1, int(round(L / 8.0)))
        posts = []
        for i in range(n + 1):
            px, pz = ax + (bx - ax) * i / n, az + (bz - az) * i / n
            g = gy(px, pz)
            m.box("A7_Aluminum", (px, g - 0.3 + 1.4, pz), (0.09, 2.8, 0.09), 0.0)
            base(m.name, px, g - 0.3, pz)
            posts.append((px, g, pz))
        for (pa, ga, za), (pb, gb, zb) in zip(posts, posts[1:]):
            for y, rad in ((2.38, 0.042), (1.2, 0.028)):
                m.cyl("A7_Aluminum", (pa, ga + y, za), (pb, gb + y, zb), rad, 4, caps=(False, False))
    return m


def utility_poles():
    m = Model("LSS_UtilityPoles" + SUFFIX)
    za, zb = POLE_Z
    zs = np.linspace(za, zb, int(abs(zb - za) // 60) + 1)
    tops = []
    for z in zs:
        g = gy(POLE_X, z)
        m.cyl("LS_ConcreteBarrier", (POLE_X, g - 0.5, z), (POLE_X, g + 11.0, z), 0.16, 6, caps=(False, True))
        m.box("A7_PaintGraphite", (POLE_X, g + 10.3, z), (2.4, 0.12, 0.12), 0.0)
        base(m.name, POLE_X, g - 0.5, z)
        tops.append((z, g + 10.42))
    for (za_, ya), (zb_, yb) in zip(tops, tops[1:]):
        for wx in (-1.05, 0.0, 1.05):
            mid = ((za_ + zb_) / 2, (ya + yb) / 2 - 0.9)
            for (p0, p1) in (((POLE_X + wx, ya, za_), (POLE_X + wx, mid[1], mid[0])),
                             ((POLE_X + wx, mid[1], mid[0]), (POLE_X + wx, yb, zb_))):
                m.cyl("A7_PaintGraphite", p0, p1, 0.016, 4, caps=(False, False))
    return m


def build_kit():
    BASES.clear()
    d, drain_mid = drainage()
    models = [ground_breakup(), scrub(), tree_lines(), berms(), d, fence(), utility_poles()]
    pts = {
        "LSS_FenceGate_North": ((GATE[0] + GATE[1]) / 2, FENCE[2]),
        "LSS_FenceCorner_SW": (FENCE[0], FENCE[3]),
        "LSS_FenceCorner_NE": (FENCE[1], FENCE[2]),
        "LSS_PoleLine_North": (POLE_X, POLE_Z[1]),
        "LSS_PoleLine_South": (POLE_X, POLE_Z[0]),
        "LSS_Drainage_West_Mid": drain_mid[0],
        "LSS_Drainage_South_Mid": drain_mid[1],
    }
    anchors = {k + SUFFIX: (x, gy(x, z), z) for k, (x, z) in pts.items()}
    return models, anchors


def seating_report():
    """Every recorded base point (mound and canopy edges, rock bottoms, post, pole and headwall feet) against the
    terrain-mesh ground at the same x, z: depth below the ground (positive = embedded)."""
    out = {}
    for name, pts in BASES.items():
        p = np.asarray(pts)
        depth = gy(p[:, 0], p[:, 2]) - p[:, 1]
        out[name] = {"base_points": int(len(p)), "min_depth_m": round(float(depth.min()), 4),
                     "max_depth_m": round(float(depth.max()), 4), "floating": int((depth < 0).sum())}
    return out


if __name__ == "__main__":
    import time
    t0 = time.time()
    models, anchors = build_kit()
    for mdl in models:
        lo, hi = mdl.bounds()
        print("%-24s %6d" % (mdl.name, mdl.triangle_count()), np.round(lo, 2), np.round(hi, 2))
    print("total", sum(x.triangle_count() for x in models), "%.1fs" % (time.time() - t0))
    for k, v in seating_report().items():
        print(k, v)
    print(anchors)
