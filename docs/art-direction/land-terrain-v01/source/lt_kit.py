"""lt_kit - LAND-TERRAIN-V01 geometry in prologue-local Unity metres (y up); every object is pivoted at the prologue
origin. Original procedural geometry, fixed seeds.

Objects:
  LT_TerrainInner  from the launch-foundation footprint out to an offset of 2928 m (|x|, |z| <= 3000). Rings of the
                   footprint rectangle offset outwards (rounded rectangles), ring gap and vertex spacing growing from
                   5.5 m to about 73 m, zipped together. Ring 0 is exactly the 16 footprint boundary vertices of the
                   LAND kit, so the hole is the footprint rectangle with no gap, overlap or T-junction.
  LT_TerrainFar    from that last ring out to a 9 km circle: 16 rings of 192 vertices, gap growing by 7 %.
  LT_Rocks         clusters of jittered, partly buried icosahedra (0.3-2 m) seated on the surface, outside the
                   footprint, both corridors (with margins), the fence line, the pole line and the drainage ditches.
Anchors: the four footprint corners at the datum.
"""
import math

import numpy as np

import a7kit
from a7kit import Model
import lt_height as H

FOOT_RING = np.array([(-72, -62), (-60, -62), (60, -62), (72, -62), (72, -50), (72, 60), (72, 72), (60, 72), (4, 72),
                      (3, 72), (-3, 72), (-4, 72), (-60, 72), (-72, 72), (-72, 60), (-72, -50)], float)
T_INNER = 2928.0
FAR_RINGS, FAR_N, FAR_R = 16, 192, 9000.0
TERRAIN_MAT, FAR_MAT, ROCK_MAT = "LT_Terrain", "LT_TerrainFar", "LT_Rock"


def spacing(t):
    return 5.5 + 0.023 * t


def offset_ring(t, step):
    """Points on the footprint rectangle offset by t, counter-clockwise in the x-z plane (angle atan2(z, x)
    increasing), about `step` apart."""
    x0, x1, z0, z1 = H.FOOTPRINT
    w, d = x1 - x0, z1 - z0
    pieces = [("L", (x0, z0 - t), (x1, z0 - t), w), ("A", (x1, z0), -90.0, 0.0),
              ("L", (x1 + t, z0), (x1 + t, z1), d), ("A", (x1, z1), 0.0, 90.0),
              ("L", (x1, z1 + t), (x0, z1 + t), w), ("A", (x0, z1), 90.0, 180.0),
              ("L", (x0 - t, z1), (x0 - t, z0), d), ("A", (x0, z0), 180.0, 270.0)]
    lengths = [p[3] if p[0] == "L" else math.pi * t / 2.0 for p in pieces]
    total = sum(lengths)
    n = max(16, int(round(total / step)))
    out = []
    for k in range(n):
        s = (k + 0.5) * total / n
        for p, length in zip(pieces, lengths):
            if s <= length or p is pieces[-1]:
                f = min(s / length, 1.0) if length > 0 else 0.0
                if p[0] == "L":
                    out.append((p[1][0] + (p[2][0] - p[1][0]) * f, p[1][1] + (p[2][1] - p[1][1]) * f))
                else:
                    a = math.radians(p[2] + (p[3] - p[2]) * f)
                    out.append((p[1][0] + t * math.cos(a), p[1][1] + t * math.sin(a)))
                break
            s -= length
    return np.asarray(out)


def _orient(p, q, r):
    return (q[0] - p[0]) * (r[1] - p[1]) - (q[1] - p[1]) * (r[0] - p[0])


def zip_rings(A, B, ia, ib):
    """Triangle strip between closed counter-clockwise rings A (inner) and B (outer): shorter-diagonal rule with an
    orientation guard. Every triangle has a negative x-z orientation, i.e. faces up (+y) in Unity space."""
    n, m = len(A), len(B)
    angA = np.arctan2(A[:, 1], A[:, 0])
    angB = np.arctan2(B[:, 1], B[:, 0])
    j0 = int(np.argmin(np.abs(np.angle(np.exp(1j * (angB - angA[0]))))))
    i = j = 0
    tris = []
    while i < n or j < m:
        a0, a1 = A[i % n], A[(i + 1) % n]
        b0, b1 = B[(j0 + j) % m], B[(j0 + j + 1) % m]
        if i < n and j < m:
            choose_a = np.hypot(*(a1 - b0)) <= np.hypot(*(b1 - a0))
            if choose_a and _orient(a0, a1, b0) >= 0:
                choose_a = False
            elif not choose_a and _orient(a0, b1, b0) >= 0:
                choose_a = True
        else:
            choose_a = i < n
        if choose_a:
            tris.append((ia[i % n], ia[(i + 1) % n], ib[(j0 + j) % m]))
            i += 1
        else:
            tris.append((ia[i % n], ib[(j0 + j + 1) % m], ib[(j0 + j) % m]))
            j += 1
    return tris


def _area(poly):
    x, z = poly[:, 0], poly[:, 1]
    return 0.5 * float(np.dot(x, np.roll(z, -1)) - np.dot(z, np.roll(x, -1)))


def _check_annulus(xz, tris, inner, outer):
    p = xz[np.asarray(tris)]
    o = (p[:, 1, 0] - p[:, 0, 0]) * (p[:, 2, 1] - p[:, 0, 1]) - (p[:, 1, 1] - p[:, 0, 1]) * (p[:, 2, 0] - p[:, 0, 0])
    want = _area(outer) - _area(inner)
    got = float(-o.sum()) / 2.0
    assert (o < 0).all(), "folded triangle"
    assert abs(got - want) <= 1e-6 * want, (got, want)
    return float(-o.max() / 2.0), float(-o.min() / 2.0)


def _surface_model(name, material, rings):
    xz = np.concatenate(rings, axis=0)
    starts = np.cumsum([0] + [len(r) for r in rings])
    tris = []
    for k in range(len(rings) - 1):
        ia = np.arange(starts[k], starts[k + 1])
        ib = np.arange(starts[k + 1], starts[k + 2])
        part = zip_rings(rings[k], rings[k + 1], ia, ib)
        _check_annulus(xz, part, rings[k], rings[k + 1])
        tris += part
    xz = xz.astype(np.float32).astype(np.float64)
    y = (H.DATUM + H.height(xz[:, 0], xz[:, 1])).astype(np.float32).astype(np.float64)
    v = np.column_stack([xz[:, 0], y, xz[:, 1]])
    t = np.asarray(tris, dtype=np.int64)
    uvv = np.column_stack([H.uv_axis(v[:, 0]), H.uv_axis(v[:, 2])])
    m = Model(name)
    m.add(material, v, t, uv=uvv[t])
    return m, {"rings": len(rings), "vertices": int(len(v)), "ring_sizes": [int(len(r)) for r in rings]}


def inner_rings():
    rings, ts = [FOOT_RING.copy()], [0.0]
    t = 6.0
    while True:
        rings.append(offset_ring(t, spacing(t)))
        ts.append(t)
        if t >= T_INNER:
            break
        nxt = t + spacing(t)
        t = T_INNER if nxt > T_INNER - 0.5 * spacing(nxt) else nxt
    return rings, ts


def far_rings(seam):
    r0 = float(np.hypot(seam[:, 0], seam[:, 1]).max()) + 95.0
    radii = r0 * (FAR_R / r0) ** (np.arange(FAR_RINGS) / (FAR_RINGS - 1.0))
    rings = [seam]
    for k, r in enumerate(radii):
        a = 2.0 * math.pi * (np.arange(FAR_N) + 0.5 * (k % 2)) / FAR_N - math.pi
        rings.append(np.column_stack([r * np.cos(a), r * np.sin(a)]))
    return rings, [float(x) for x in radii]


# ----------------------------------------------------------------------------- rocks
_ICO_P = (1 + 5 ** 0.5) / 2
_ICO_V = np.array([(-1, _ICO_P, 0), (1, _ICO_P, 0), (-1, -_ICO_P, 0), (1, -_ICO_P, 0), (0, -1, _ICO_P),
                   (0, 1, _ICO_P), (0, -1, -_ICO_P), (0, 1, -_ICO_P), (_ICO_P, 0, -1), (_ICO_P, 0, 1),
                   (-_ICO_P, 0, -1), (-_ICO_P, 0, 1)], float)
_ICO_V /= np.linalg.norm(_ICO_V, axis=1)[:, None]
_ICO_T = np.array([(0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11), (1, 5, 9), (5, 11, 4), (11, 10, 2),
                   (10, 7, 6), (7, 1, 8), (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9), (4, 9, 5),
                   (2, 4, 11), (6, 2, 10), (8, 6, 7), (9, 8, 1)])


def rock(m, x, z, size, rng):
    """Jittered icosahedron (20 triangles), `size` m across, squashed to 62 % height, random yaw and a small tilt,
    sunk so about 35 % of its height is below the lowest ground under it."""
    r = size / 2.0
    v = _ICO_V * (1.0 + 0.28 * (rng.random((12, 1)) - 0.5)) * np.array([r, 0.62 * r, r * (0.7 + 0.5 * rng.random())])
    yaw, tilt = rng.random() * 2 * math.pi, math.radians(10.0 * (rng.random() - 0.5))
    cy, sy, ct, st = math.cos(yaw), math.sin(yaw), math.cos(tilt), math.sin(tilt)
    rot = np.array([[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]]) @ np.array([[1, 0, 0], [0, ct, -st], [0, st, ct]])
    base = float(H.seat_y(np.array([x]), np.array([z]), r)[0])
    centre = np.array([x, base + 0.3 * 0.62 * r, z])
    v = v @ rot.T + centre
    m.add(ROCK_MAT, v, a7kit._outward_convex(v, _ICO_T, centre))


def rock_clear(x, z, size):
    d = float(H.protected_distance(np.array([x]), np.array([z]))[0])
    if d < 10.0 + size:
        return False
    fx0, fx1, fz0, fz1 = H.FENCE
    on_x = (abs(x - fx0) < 6 or abs(x - fx1) < 6) and fz0 - 6 <= z <= fz1 + 6
    on_z = (abs(z - fz0) < 6 or abs(z - fz1) < 6) and fx0 - 6 <= x <= fx1 + 6
    if on_x or on_z:
        return False
    if abs(x - 112.0) < 9.0 and -930.0 <= z <= -50.0:
        return False
    for a, b in H.DITCHES:
        dist, _ = H.segment_distance(np.array([x]), np.array([z]), a, b)
        if float(dist[0]) < 7.0:
            return False
    return True


def rocks():
    m = Model("LT_Rocks")
    rng = np.random.default_rng(4207)
    clusters, count, tries, centres = 0, 0, 0, []
    while clusters < 62 and tries < 20000:
        tries += 1
        rr = 95.0 + 1300.0 * rng.random() ** 1.5
        a = rng.random() * 2 * math.pi
        cx, cz = rr * math.cos(a), rr * math.sin(a)
        if not rock_clear(cx, cz, 9.0):
            continue
        clusters += 1
        centres.append((round(cx, 2), round(cz, 2)))
        if rng.random() < 0.65:
            rock(m, cx, cz, 1.2 + 0.8 * rng.random(), rng)
            count += 1
        for _ in range(3 + int(rng.random() * 9)):
            q = rng.random() * 2 * math.pi
            dist = 1.5 + 6.0 * rng.random() ** 0.8
            x, z = cx + dist * math.cos(q), cz + dist * 0.7 * math.sin(q)
            size = 0.3 + 0.9 * rng.random() ** 1.6
            if rock_clear(x, z, size):
                rock(m, x, z, size, rng)
                count += 1
    return m, {"clusters": clusters, "rocks": count, "cluster_centres_xz": centres}


def build_kit():
    rings, ts = inner_rings()
    inner, info_in = _surface_model("LT_TerrainInner", TERRAIN_MAT, rings)
    frings, radii = far_rings(rings[-1])
    far, info_far = _surface_model("LT_TerrainFar", FAR_MAT, frings)
    rk, info_rk = rocks()
    x0, x1, z0, z1 = H.FOOTPRINT
    anchors = {"LT_Footprint_NW": (x0, H.DATUM, z0), "LT_Footprint_NE": (x1, H.DATUM, z0),
               "LT_Footprint_SE": (x1, H.DATUM, z1), "LT_Footprint_SW": (x0, H.DATUM, z1)}
    info = {"LT_TerrainInner": dict(info_in, ring_offsets_m=[round(t, 2) for t in ts]),
            "LT_TerrainFar": dict(info_far, ring_radii_m=[round(r, 1) for r in radii]), "LT_Rocks": info_rk}
    return [inner, far, rk], anchors, info


if __name__ == "__main__":
    import time
    t0 = time.time()
    models, anchors, info = build_kit()
    for mdl in models:
        lo, hi = mdl.bounds()
        print(mdl.name, mdl.triangle_count(), np.round(lo, 2), np.round(hi, 2))
    print({k: {kk: vv for kk, vv in v.items() if kk not in ("ring_offsets_m", "ring_radii_m", "ring_sizes")}
           for k, v in info.items()})
    print("total", sum(mm.triangle_count() for mm in models), "%.1fs" % (time.time() - t0))
    print("inner ring sizes", info["LT_TerrainInner"]["ring_sizes"][:6], info["LT_TerrainInner"]["ring_sizes"][-3:])
