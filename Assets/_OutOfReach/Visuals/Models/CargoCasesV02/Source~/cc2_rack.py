"""CARGO-CASES-V02 supply rack: one welded frame plus one cell (cradle, tie-down straps, sign stand) per container.

Frame coordinates are the dock frame of Bootstrap shifted to the rack centre: x = dock x + 2.655, y = dock z + 1.10
(front, the aisle side, is -Y), z = dock y. The frame stands on the modelled cargo plinth (top at dock y 0.20) between
the dock's orange guard fences (inner faces at dock x -3.30 and -2.00).
A cell uses its container's own frame (floor centre origin, front -Y) and is parented under that supply case, so the
cradle, its straps and the sign stand leave with the crate (R02).
"""
import math

from mathutils import Matrix, Vector

from cc2_lib import X, Y, Z, bolt, box, cyl, decal, fillet, obox, ribbon

STEEL, ALU, RUB, GRA, WEBA = "CC2_RackSteel", "CC2_Aluminium", "CC2_Rubber", "CC2_Graphite", "CC2_WebbingAmber"
COLS = (-.315, .315)                      # cell centres (dock x -2.97 / -2.34)
FLOORS = (.195, .595, .995, 1.395)        # crate floors (I06CargoPlacardAuthoring rows)
PLINTH, TOP = .20, 1.86
PX, PY = .632, .235                       # post centres
ANCHOR_Y = PY + .027                      # strap hooks on the cargo track of the beam's outer face


def frame(p):
    for x in (-PX, 0, PX):
        w = .03 if x else .036
        for y in (-PY, PY):
            box(p, (x, y, (PLINTH + TOP) / 2), (w, .045, TOP - PLINTH), STEEL, bevel=.004, seg=1, tile=.3)
            box(p, (x, y, PLINTH + .006), (.05 if x else .09, .13, .012), STEEL, bevel=.003, seg=1, tile=.3)
            for dy in (-.048, .048):
                bolt(p, (x, y + dy, PLINTH + .012), Z, r=.007, h=.006)
            if p.lod < 2:
                box(p, (x, y, TOP + .004), (w + .008, .053, .008), RUB, bevel=.002, seg=1, tile=.08)   # post cap
    # sill on the plinth, three crate levels, top frame
    levels = [PLINTH + .015] + [f - .045 for f in FLOORS[1:]] + [TOP - .03]
    for i, zc in enumerate(levels):
        h = .03 if i == 0 else .05
        for y in (-PY, PY):
            box(p, (0, y, zc), (2 * PX, .04, h), STEEL, bevel=.004, seg=1, tile=.3)
            if 0 < i < len(levels) - 1 or i == 0:
                box(p, (0, y + math.copysign(.023, y), zc), (2 * PX - .09, .007, min(h, .03) * .8), ALU, tile=.3)  # cargo track
        if i > 0:
            for x in (-PX, 0, PX):
                box(p, (x, 0, zc), (.03, 2 * PY, .04), STEEL, bevel=.003, seg=1, tile=.3)
    for f in FLOORS[1:]:                                                              # two support rails per cell
        for cx in COLS:
            for dx in (-.15, .15):
                box(p, (cx + dx, 0, f - .03), (.03, 2 * PY + .04, .02), STEEL, tile=.3)
    # end frames: zigzag flat-bar bracing on the inside of the outer posts
    for sx in (-1, 1):
        for i in range(len(levels) - 1):
            z0, z1 = levels[i] + .02, levels[i + 1] - .02
            s = 1 if i % 2 == 0 else -1
            a, b = Vector((sx * (PX - .02), -PY * s * .9, z0)), Vector((sx * (PX - .02), PY * s * .9, z1))
            d = b - a
            rot = Matrix.Rotation(math.atan2(d.z, d.y), 3, 'X')
            box(p, (a + b) / 2, (.006, d.length, .03), STEEL, rot=rot, tile=.3)
            if p.lod == 0:
                for q in (a, b):
                    bolt(p, (q.x - sx * .003, q.y, q.z), (-sx, 0, 0), r=.006, h=.005)
    # rear cross bracing with turnbuckles
    for k, s in enumerate((1, -1)):
        y = PY + .03 + k * .016
        a, b = Vector((-PX * s, y, PLINTH + .06)), Vector((PX * s, y, TOP - .07))
        cyl(p, a, b, .006, ALU, n=8, tile=.3)
        m, u = (a + b) / 2, (b - a).normalized()
        if p.lod < 2:
            cyl(p, m - u * .05, m + u * .05, .012, STEEL, n=8, tile=.3)
    # header rail on three cantilever arms: carries the "SUPPLY RACK" plate PreflightRackDressing adds at runtime
    hz, hy = 1.664, -.355
    box(p, (0, hy, hz), (2 * PX + .03, .03, .03), STEEL, bevel=.003, seg=1, tile=.3)
    for x in (-PX, 0, PX):
        y0 = -PY - .0225
        box(p, (x, (y0 + hy + .015) / 2, hz), (.026, abs(hy + .015 - y0), .026), STEEL, tile=.3)
        a, b = Vector((x, y0, hz - .075)), Vector((x, hy + .02, hz - .01))
        d = b - a
        box(p, (a + b) / 2, (.006, d.length, .022), STEEL, rot=Matrix.Rotation(math.atan2(d.z, d.y), 3, 'X'), tile=.3)
    for cx in COLS:
        decal(p, "stencil:twohand", (cx, -PY - .0202, TOP - .03), -Y, Z, h=.026, off=.0006)


CELLS = {
    # sx: strap centre |x|; w: strap width; prof: top profile (y, z) between the two anchors; ground: plinth row
    "Water": dict(sx=.196, w=.025, prof=[(-.1515, .2565), (.1515, .2565)], r=.034, ground=True),
    "Oxygen": dict(sx=.176, w=.03, prof=[(-.1, .2655), (.1, .2655)], r=.014, ground=True),
    "Food": dict(sx=.19, w=.032, prof=[(-.164, .301), (.16, .301)], r=.022),
    "Battery": dict(sx=.155, w=.032, prof=[(-.16, .2455), (.018, .268), (.162, .268)], r=.008),
    "Solar": dict(sx=.167, w=.03, round=(.118, .158)),
    "Spares": dict(sx=.19, w=.032, prof=[(-.166, .2615), (.166, .2615)], r=.016),
    "Science": dict(sx=.17, w=.03, prof=[(-.162, .2975), (.162, .2975)], r=.008),
}


def strap_path(c, za, steps):
    a, d = Vector((0, -ANCHOR_Y, za)), Vector((0, ANCHOR_Y, za))
    if "round" in c:
        r, az = c["round"][0] + .004, c["round"][1]
        ta = math.atan2(a.z - az, a.y)
        phi = math.acos(r / math.hypot(a.y, a.z - az))
        t0 = ta - phi                                   # front tangent point (upper front of the canister)
        t0 = t0 % (2 * math.pi)
        t1 = math.pi - t0
        pts = [a]
        n = max(4, steps * 3)
        for k in range(n + 1):
            t = t0 + (t1 - t0) * k / n
            pts.append(Vector((0, r * math.cos(t), az + r * math.sin(t))))
        return pts + [d]
    mids = [Vector((0, y, z)) for y, z in c["prof"]]
    return fillet([a] + mids + [d], c["r"], steps)


def cell(p, key):
    c = CELLS[key]
    ground = c.get("ground", False)
    za = .02 if ground else -.045
    if not ground:                                                                    # cradle runners with pads
        for x in (-.15, .15):
            box(p, (x, 0, -.012), (.05, 2 * PY + .02, .016), STEEL, bevel=.003, seg=1, tile=.3)
            box(p, (x, 0, -.002), (.042, .4, .004), RUB, tile=.08)
    zs, hs = (.02, .03) if ground else (0, .04)
    for y in (-.213, .192):                                                           # stop angles front and rear
        box(p, (0, y, zs), (.42, .006, hs), ALU, bevel=.0015, seg=1, tile=.3)
    steps = 5 if p.lod == 0 else 3 if p.lod == 1 else 2
    path = strap_path(c, za, steps)
    for sx in (-c["sx"], c["sx"]):
        pts = [Vector((sx, q.y, q.z)) for q in path]
        ribbon(p, pts, X, c["w"], .003, WEBA)
        for q in (pts[0], pts[-1]):                                                   # flat hooks in the cargo track
            box(p, (sx, q.y, q.z), (c["w"] * .8, .012, .03), ALU, bevel=.002, seg=1, tile=.3)
        # ratchet buckle on the front run
        u = (pts[1] - pts[0]).normalized()
        n = Vector((0, -u.z, u.y))
        m = pts[0] + u * (pts[1] - pts[0]).length * .45
        obox(p, m + n * .009, n, u, (c["w"] + .014, .07, .016), ALU, bevel=.003)
        if p.lod < 2:
            obox(p, m + n * .021 + u * .012, n, u, (c["w"] * .85, .052, .006), GRA, bevel=.002)
            cyl(p, m + n * .012 - X * (c["w"] / 2 + .01) - u * .018, m + n * .012 + X * (c["w"] / 2 + .01) - u * .018, .007, STEEL, n=8, tile=.3)
    # sign stand: two arms from the front stop and posts behind the manifest plate (plate back at y -0.433)
    for x in (-.25, .25):
        box(p, (x, (-.21 - .431) / 2, .05), (.02, .221, .02), STEEL, bevel=.002, seg=1, tile=.3)
        box(p, (x, -.427, .1125), (.02, .012, .135), STEEL, bevel=.002, seg=1, tile=.3)
        if p.lod == 0:
            bolt(p, (x, -.421, .15), Y, r=.005, h=.004)
        a, b = Vector((x, -.3, .05)), Vector((x, -.415, .12))
        d = b - a
        box(p, (a + b) / 2, (.005, d.length, .016), STEEL, rot=Matrix.Rotation(math.atan2(d.z, d.y), 3, 'X'), tile=.3)


def _cell(key):
    return lambda p: cell(p, key)


PARTS = [("RackFrame", "rack-frame", frame)] + [("RackCell_" + k, "rack-cell:" + k, _cell(k)) for k in CELLS]
