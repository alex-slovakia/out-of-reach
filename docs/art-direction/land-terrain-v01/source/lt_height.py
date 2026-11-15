"""lt_height - LAND-TERRAIN-V01 height field h(x, z) and macro UV mapping, prologue-local Unity metres (y up).

Ground surface: y = DATUM + h(x, z), with h = M(d) * R(x, z).
  d     smooth distance to the union of the protected rectangles: the launch-foundation footprint x[-72, 72]
        z[-62, 72], the crawlerway/facility corridor x[-55, 95] z[-1020, -50] and the plume/outlet corridor
        x[-35, 35] z[60, 320] (polynomial smooth-min, k = 24 m); 0 inside them.
  M(d)  smootherstep((d - 6) / 64): exactly 0 inside the rectangles and within 6 m of them, 1 beyond 70 m.
  R     coastal relief (metres):
          dunes    shore-parallel ridged noise (ridges run along z, about 60 m apart), up to about 4 m on the coast
                   side (+x) and about 2 m inland (-x);
          swales   shallow depressions down to -1.3 m;
          swells   mid-scale undulation (220 m cells), about +-1 m near the site, +-4 m from 1.3 km;
          rolling  broad fractal ground whose amplitude grows from about 1.5 m near the site to 10-20 m at
                   2-3 km, easing to about 11 m beyond 6 km, plus broader swells from 2.6 km;
          rim      a low ridge band at 6.6-9 km (16-32 m) that closes the horizon seen from the crew deck;
          ditches  two 0.7 m drainage ditches on the LAND-SURROUNDINGS drainage lines (x -100, z -40..250 and
                   z 150, x 48..190), with their own 6 m flat apron and a 24 m blend.
        Inside the security fence (x[-200, 200] z[-200, 330]) dunes and swales are graded down to 75 %.
Pure NumPy with a fixed integer hash (no RNG state): Blender, the texture script and LAND-SURROUNDINGS v02
evaluate exactly the same surface. Everything is vectorised; scalars work too.

UV0 (macro map): u = U(x), v = U(z) with U(c) = 0.5 + c / 6400 for |c| <= 3000 (planar, 3.125 m per texel on
2048 px); from 3000 to 9000 m the far ring continues into the outer 3.125 % band of the map (compressed 30x).
"""
import math

import numpy as np

DATUM = -36.6
FOOTPRINT = (-72.0, 72.0, -62.0, 72.0)                       # x0, x1, z0, z1
CORRIDORS = (("crawlerway_facility", (-55.0, 95.0, -1020.0, -50.0)),
             ("plume_outlet", (-35.0, 35.0, 60.0, 320.0)))
FENCE = (-200.0, 200.0, -200.0, 330.0)
DITCHES = (((-100.0, -40.0), (-100.0, 250.0)), ((48.0, 150.0), (190.0, 150.0)))
FLAT, BLEND, SMIN_K = 6.0, 64.0, 24.0
UV_INNER, UV_SPAN, UV_FAR = 3000.0, 6400.0, 9000.0
UV_BAND = 0.5 - UV_INNER / UV_SPAN


# ----------------------------------------------------------------------------- helpers
def smoothstep(e0, e1, x):
    t = np.clip((np.asarray(x, float) - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def smootherstep(t):
    t = np.clip(t, 0.0, 1.0)
    return t * t * t * (t * (t * 6.0 - 15.0) + 10.0)


def rect_distance(x, z, r):
    x0, x1, z0, z1 = r
    dx = np.maximum(np.maximum(x0 - x, x - x1), 0.0)
    dz = np.maximum(np.maximum(z0 - z, z - z1), 0.0)
    return np.hypot(dx, dz)


def rect_signed(x, z, r):
    """Signed distance to a rectangle: negative inside."""
    x0, x1, z0, z1 = r
    qx = np.maximum(x0 - x, x - x1)
    qz = np.maximum(z0 - z, z - z1)
    out = np.hypot(np.maximum(qx, 0.0), np.maximum(qz, 0.0))
    return out + np.minimum(np.maximum(qx, qz), 0.0)


def _smin(a, b, k):
    h = np.maximum(k - np.abs(a - b), 0.0) / k
    return np.minimum(a, b) - h * h * k * 0.25


def protected_distance(x, z):
    x, z = np.asarray(x, float), np.asarray(z, float)
    d = rect_distance(x, z, FOOTPRINT)
    for _, r in CORRIDORS:
        d = _smin(d, rect_distance(x, z, r), SMIN_K)
    return np.maximum(d, 0.0)


def segment_distance(x, z, a, b):
    ax, az = a
    bx, bz = b
    vx, vz = bx - ax, bz - az
    t = np.clip(((x - ax) * vx + (z - az) * vz) / (vx * vx + vz * vz), 0.0, 1.0)
    return np.hypot(x - (ax + t * vx), z - (az + t * vz)), t


# ----------------------------------------------------------------------------- deterministic noise
_M32 = np.uint64(0xFFFFFFFF)


def _hash(ix, iz, seed):
    h = (ix.astype(np.uint64) * np.uint64(0x9E3779B1) + iz.astype(np.uint64) * np.uint64(0x85EBCA77)
         + np.uint64(seed) * np.uint64(0xC2B2AE3D)) & _M32
    h ^= h >> np.uint64(15)
    h = (h * np.uint64(0x2C1B3C6D)) & _M32
    h ^= h >> np.uint64(12)
    h = (h * np.uint64(0x297A2D39)) & _M32
    h ^= h >> np.uint64(15)
    return h.astype(np.float64) / 4294967296.0


def vnoise(x, z, cell_x, cell_z, seed, angle=0.0):
    """Value noise in [0, 1): quintic interpolation of hashed lattice values; the plane is rotated by `angle`
    (radians) and the lattice is cell_x by cell_z metres."""
    x, z = np.asarray(x, float), np.asarray(z, float)
    c, s = math.cos(angle), math.sin(angle)
    gx = (c * x - s * z) / cell_x
    gz = (s * x + c * z) / cell_z
    fx0, fz0 = np.floor(gx), np.floor(gz)
    fx, fz = gx - fx0, gz - fz0
    ix, iz = fx0.astype(np.int64), fz0.astype(np.int64)
    ux = fx * fx * fx * (fx * (fx * 6.0 - 15.0) + 10.0)
    uz = fz * fz * fz * (fz * (fz * 6.0 - 15.0) + 10.0)
    a, b = _hash(ix, iz, seed), _hash(ix + 1, iz, seed)
    cc, d = _hash(ix, iz + 1, seed), _hash(ix + 1, iz + 1, seed)
    top = a + (b - a) * ux
    bottom = cc + (d - cc) * ux
    return top + (bottom - top) * uz


def fbm(x, z, cell, octaves, seed, gain=0.5):
    """Signed fractal value noise, about -0.6..0.6 (zero mean)."""
    total, amp, norm = 0.0, 1.0, 0.0
    for o in range(octaves):
        c = cell / 2.0 ** o
        total = total + amp * (2.0 * vnoise(x, z, c, c, seed + 101 * o, 0.61 * o + 0.3) - 1.0)
        norm += amp
        amp *= gain
    return total / norm


def _ridge(n):
    q = 2.0 * n - 1.0
    return (1.0 - np.sqrt(q * q + 0.02)) / (1.0 - math.sqrt(0.02))


# ----------------------------------------------------------------------------- relief
def components(x, z):
    """Every term of h at (x, z); `h` is the height above the datum."""
    x, z = np.asarray(x, float), np.asarray(z, float)
    r = np.hypot(x, z)
    d = protected_distance(x, z)
    mask = smootherstep((d - FLAT) / BLEND)
    # domain warp shared by the dune field
    xw = x + 150.0 * fbm(x, z, 800.0, 2, 3) + 0.16 * z
    zw = z + 150.0 * fbm(x, z, 800.0, 2, 4)
    # shore-parallel dune ridges (anisotropic ridged noise, ridges along z)
    # the fine octave fades out by 1 km and the whole field by 3 km, where the mesh can no longer resolve them
    dune = (_ridge(vnoise(xw, zw, 60.0, 260.0, 20, 0.10)) ** 2
            + 0.5 * smoothstep(1000.0, 350.0, r) * _ridge(vnoise(xw, zw, 30.0, 100.0, 21, 0.18)) ** 2) / 1.5
    dune = dune * smoothstep(3000.0, 1400.0, r)
    coast = smoothstep(-1600.0, 2400.0, x)
    hummock = 0.55 + 0.45 * vnoise(x, z, 380.0, 380.0, 22)
    fence_in = smoothstep(6.0, -30.0, rect_signed(x, z, FENCE))
    graded = 1.0 - 0.25 * fence_in
    dunes = 4.8 * (0.5 + 0.5 * coast) * hummock * dune * graded
    swale_f = smoothstep(0.06, 0.34, fbm(x, z, 190.0, 2, 31))
    swales = -1.3 * swale_f * graded
    a_roll = 1.3 + 14.7 * smoothstep(300.0, 2500.0, r) - 5.0 * smoothstep(3200.0, 6500.0, r)
    rolling = a_roll * (1.7 * fbm(x, z, 620.0, 3, 41) + 0.22)
    swells = (1.2 + 4.3 * smoothstep(200.0, 1300.0, r)) * 1.7 * fbm(x, z, 220.0, 2, 42)
    far = 8.0 * smoothstep(2600.0, 4200.0, r) * (1.6 * fbm(x, z, 2300.0, 2, 51))
    rim = smoothstep(6600.0, 8800.0, r) * (16.0 + 16.0 * vnoise(x, z, 1700.0, 1700.0, 61))
    relief = dunes + swales + rolling + swells + far + rim
    ditch = np.zeros_like(x)
    for a, b in DITCHES:
        dist, t = segment_distance(x, z, a, b)
        ends = smoothstep(0.0, 0.04, t) * smoothstep(1.0, 0.96, t)
        ditch = np.maximum(ditch, np.exp(-(dist / 3.6) ** 2) * (0.35 + 0.65 * ends))
    ditch_mask = smootherstep((d - FLAT) / 24.0)
    h = mask * relief - 0.7 * ditch * ditch_mask
    return {"h": h, "mask": mask, "d": d, "r": r, "dune": dune, "dunes": dunes, "coast": coast,
            "swale": swale_f, "rolling": rolling, "swells": swells, "far": far, "rim": rim, "ditch": ditch * ditch_mask,
            "fence_in": fence_in}


def height(x, z):
    return components(x, z)["h"]


def ground_y(x, z):
    return DATUM + height(x, z)


def mesh_eps(x, z):
    """Half the terrain mesh spacing at (x, z): normals are taken at the scale the mesh resolves."""
    return np.clip(0.5 * (5.5 + 0.023 * np.hypot(x, z)), 1.0, 120.0)


def normal(x, z, eps=None):
    """Unit surface normal (Unity space) from central differences of h, by default over mesh_eps."""
    x, z = np.asarray(x, float), np.asarray(z, float)
    eps = mesh_eps(x, z) if eps is None else eps
    hx = (height(x + eps, z) - height(x - eps, z)) / (2.0 * eps)
    hz = (height(x, z + eps) - height(x, z - eps)) / (2.0 * eps)
    n = np.stack([-hx, np.ones_like(hx), -hz], axis=-1)
    return n / np.linalg.norm(n, axis=-1, keepdims=True)


def seat_y(x, z, radius):
    """Lowest ground under a footprint of `radius` (centre and 8 points on the rim), so bases never float."""
    x, z = np.asarray(x, float), np.asarray(z, float)
    ys = [ground_y(x, z)]
    for k in range(8):
        a = k * math.pi / 4.0
        ys.append(ground_y(x + radius * math.cos(a), z + radius * math.sin(a)))
    return np.min(np.stack(ys), axis=0)


# ----------------------------------------------------------------------------- UV0 of the macro map
def uv_axis(c):
    c = np.asarray(c, float)
    a = np.abs(c)
    inner = a / UV_SPAN
    outer = UV_INNER / UV_SPAN + UV_BAND * np.clip((a - UV_INNER) / (UV_FAR - UV_INNER), 0.0, 1.0)
    return 0.5 + np.sign(c) * np.where(a <= UV_INNER, inner, outer)


def uv_axis_inverse(u):
    s = np.asarray(u, float) - 0.5
    a = np.abs(s)
    edge = UV_INNER / UV_SPAN
    inner = a * UV_SPAN
    outer = UV_INNER + (a - edge) / UV_BAND * (UV_FAR - UV_INNER)
    return np.sign(s) * np.where(a <= edge, inner, outer)


def describe():
    return {
        "surface": "y = datum + h(x, z); h = M(d) * R(x, z) - 0.7 * ditch(x, z) * M24(d)",
        "datum_y": DATUM,
        "d": "smooth distance (polynomial smooth-min, k %.0f m) to the union of the footprint and both corridors; "
             "0 inside" % SMIN_K,
        "M": "smootherstep((d - %.0f) / %.0f): 0 inside and within %.0f m of every protected edge, 1 beyond %.0f m"
             % (FLAT, BLEND, FLAT, FLAT + BLEND),
        "R": {
            "dunes": "shore-parallel ridged value noise (cells 60 x 260 m and 30 x 100 m, domain-warped; the fine octave fades out "
                     "over 350..1000 m and the field over 1400..3000 m), "
                     "4.8 m * dune(0..1) * (0.5 + 0.5 coast(x)) * hummock(0.55..1); coast = smoothstep(-1600, 2400, x)",
            "swales": "down to -1.3 m (fbm, 190 m cells)",
            "swells": "(1.2 + 4.3 smoothstep(200, 1300, r)) * 1.7 fbm(220 m, 2 octaves)",
            "rolling": "a(r) * (1.7 fbm(620 m, 3 octaves) + 0.22), a(r) = 1.3 + 14.7 smoothstep(300, 2500, r) - 5 smoothstep(3200, 6500, r)",
            "far": "8 m * smoothstep(2600, 4200, r) * 1.6 fbm(2300 m, 2 octaves)",
            "rim": "smoothstep(6600, 8800, r) * (16 + 16 vnoise(1700 m)) m",
            "graded_inside_fence": "dunes and swales * 0.75 inside x[-200, 200] z[-200, 330]",
        },
        "ditches": "0.7 m deep Gaussian (sigma about 2.5 m) along x -100 (z -40..250) and z 150 (x 48..190); "
                   "fades in over d 6..30 m",
        "normals": "vertex normals = normalised (-dh/dx, 1, -dh/dz) by central differences over eps = clip(0.5 (5.5 + 0.023 r), 1, 120) m (half the local mesh spacing)",
        "noise": "value noise, quintic interpolation, 32-bit integer lattice hash, fixed seeds; see source/lt_height.py",
        "flat_zones": {"footprint": FOOTPRINT, "corridors": dict(CORRIDORS), "flat_apron_m": FLAT,
                       "blend_margin_m": BLEND},
        "uv0": "u = U(x), v = U(z); U(c) = 0.5 + c / %.0f for |c| <= %.0f (planar); %.0f..%.0f m continue into "
               "the outer %.5f band of the map" % (UV_SPAN, UV_INNER, UV_INNER, UV_FAR, UV_BAND),
    }


if __name__ == "__main__":
    rng = np.random.default_rng(1)
    for r0, r1 in ((80, 300), (300, 800), (800, 1500), (1500, 2200), (2200, 3000), (3000, 6000), (6000, 9000)):
        rr = r0 + (r1 - r0) * rng.random(200000)
        aa = rng.random(200000) * 2 * math.pi
        x, z = rr * np.cos(aa), rr * np.sin(aa)
        c = components(x, z)
        keep = c["d"] > 70
        h = c["h"][keep]
        n = normal(x[keep][:20000], z[keep][:20000])
        slope = np.degrees(np.arccos(n[:, 1]))
        print("r %5d-%5d  h p1 %6.2f p50 %6.2f p99 %6.2f  max %6.2f  dunes p99 %5.2f  slope p99 %4.1f max %4.1f" % (
            r0, r1, np.percentile(h, 1), np.percentile(h, 50), np.percentile(h, 99), h.max(),
            np.percentile(c["dunes"][keep], 99), np.percentile(slope, 99), slope.max()))
    print("footprint corners", height(np.array([-72.0, 72.0, 72.0, -72.0]), np.array([-62.0, -62.0, 72.0, 72.0])))
    print("uv", uv_axis(np.array([-9000.0, -3000.0, 0.0, 3000.0, 9000.0])),
          uv_axis_inverse(uv_axis(np.array([-8000.0, -2999.0, 17.0, 4500.0]))))
