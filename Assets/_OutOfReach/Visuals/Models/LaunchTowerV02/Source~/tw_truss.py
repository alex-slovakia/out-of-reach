"""Tower facade truss: corner legs, ring beams, Warren diagonals, gussets, splices, base plates, ladder, landings."""
import math
from mathutils import Vector
from tw_kit import *
from tw_layout import *

LX, LZ, CZ = 2.15, 2.2, SHAFT_CZ
LEG = 0.18
Y0 = PAD_Y
LEVELS_Y = [Y0 + 0.5] + [Y0 + 3.0 * k for k in range(1, 11)]     # ring levels, top = -2.6
Y_TOP_LEG = -0.67                                                  # leg tops under the deck girders
FACES = [  # name, outward normal, tangent, offset from tower axis, half length
    ('+x', (1, 0, 0), (0, 0, 1), LX, LZ),
    ('-x', (-1, 0, 0), (0, 0, 1), LX, LZ),
    ('+z', (0, 0, 1), (1, 0, 0), LZ, LX),
    ('-z', (0, 0, -1), (1, 0, 0), LZ, LX),
]


def face_pt(f, s, y, extra=0.0):
    _, n, t, off, half = f
    n, t = Vector(n), Vector(t)
    return Vector((0, y, CZ)) + n * (off + extra) + t * s


def gusset(o, f, s_node, y, diag_dir):
    """plate on the outside of a face covering a leg node; diag_dir = +-1 which way the diagonal leaves."""
    _, n, t, off, half = f
    n, t = Vector(n), Vector(t)
    inward = -1 if s_node > 0 else 1
    c = face_pt(f, s_node + inward * 0.09, y, LEG / 2 + 0.008)
    w, h = 0.44, 0.40
    pts = [(-w / 2 + 0.09, -h / 2), (w / 2 - 0.09, -h / 2), (w / 2, -h / 2 + 0.09), (w / 2, h / 2 - 0.09),
           (w / 2 - 0.09, h / 2), (-w / 2 + 0.09, h / 2), (-w / 2, h / 2 - 0.09), (-w / 2, -h / 2 + 0.09)]
    o.plate(c, t, Vector((0, 1, 0)), pts, 0.014, PAINT)
    top = n * 0.007
    for ix in (-1, 1):
        for iy in (-1, 0, 1):
            o.bolt(c + t * (ix * 0.14) + Vector((0, iy * 0.12, 0)) + top, n, 0.016, 0.011)


def build_truss(o):
    leg_pos = [(sx * LX, CZ + sz * LZ) for sx in (-1, 1) for sz in (-1, 1)]
    # legs, base plates, anchor bolts, splices
    for (x, z) in leg_pos:
        o.member((x, Y0 + 0.03, z), (x, Y_TOP_LEG, z), LEG, LEG, PAINT, up=(1, 0, 0))
        o.aabb((x - 0.3, Y0, z - 0.3), (x + 0.3, Y0 + 0.04, z + 0.3), PAINT)
        for ax in (-1, 1):
            for az in (-1, 1):
                p = (x + ax * 0.22, Y0 + 0.04, z + az * 0.22)
                o.tube(p, (p[0], p[1] + 0.07, p[2]), 0.022, GALV, seg=6)
                o.bolt((p[0], p[1] + 0.07, p[2]), (0, 1, 0), 0.03, 0.025)
        # foot stiffeners
        for (dx, dz) in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            o.plate((x + dx * 0.15, Y0 + 0.25, z + dz * 0.15),
                    (dz, 0, -dx) if dx == 0 else (0, 0, 1), (0, 1, 0),
                    [(-0.11, -0.21), (0.11, -0.21), (0.03, 0.21), (-0.03, 0.21)], 0.016, PAINT)
        # leg splices
        for ys in (Y0 + 9.0, Y0 + 18.0, Y0 + 27.0):
            for sx_, sz_ in ((1 if x > 0 else -1, 0), (0, 1 if z > CZ else -1)):
                n = Vector((sx_, 0, sz_))
                t = Vector((0, 0, 1)) if sx_ else Vector((1, 0, 0))
                c = Vector((x, ys, z)) + n * (LEG / 2 + 0.008)
                o.plate(c, t, (0, 1, 0), [(-0.14, -0.45), (0.14, -0.45), (0.14, 0.45), (-0.14, 0.45)], 0.016, PAINT)
                for ix in (-1, 1):
                    for iy in range(-3, 4):
                        o.bolt(c + t * (ix * 0.075) + Vector((0, iy * 0.12, 0)) + n * 0.008, n, 0.014, 0.01)
        # seat plate under the deck girders
        o.aabb((x - 0.25, Y_TOP_LEG - 0.03, z - 0.25), (x + 0.25, Y_TOP_LEG, z + 0.25), GALV)
    # ring beams, diagonals, gussets
    for fi, f in enumerate(FACES):
        half = f[4]
        for li, y in enumerate(LEVELS_Y):
            p0, p1 = face_pt(f, -half, y), face_pt(f, half, y)
            o.member(p0, p1, LEG, 0.14, PAINT, up=(0, 1, 0))
            for s_node in (-half, half):
                gusset(o, f, s_node, y, 1)
        for k in range(len(LEVELS_Y) - 1):
            y0, y1 = LEVELS_Y[k], LEVELS_Y[k + 1]
            flip = (k + fi) % 2 == 0
            a = face_pt(f, -half if flip else half, y0)
            b = face_pt(f, half if flip else -half, y1)
            o.member(a, b, 0.12, 0.12, PAINT, up=Vector(f[1]))
            # mid-bay horizontal strut for the long bays: a short lacing member crossing the diagonal
            mid = (a + b) / 2
            o.member(mid + Vector(f[2]) * -0.45, mid + Vector(f[2]) * 0.45, 0.07, 0.07, GALV, up=(0, 1, 0))


def build_ladder_and_landings(o):
    """ladder with safety cage on the -x face; grated landings with ochre handrails on the -z (rear) face."""
    xs = -(LX + LEG / 2 + 0.30)
    zl = CZ
    ytop = LEVELS_Y[-1] + 0.4
    ybot = Y0 + 0.25
    for dz in (-0.22, 0.22):
        o.member((xs, ybot, zl + dz), (xs, ytop, zl + dz), 0.05, 0.10, GALV, up=(1, 0, 0))
    y = ybot + 0.2
    while y < ytop:
        o.tube((xs, y, zl - 0.22), (xs, y, zl + 0.22), 0.013, GALV, seg=6)
        y += 0.30
    # stand-off brackets to the ring beams
    for y in LEVELS_Y[1:]:
        for dz in (-0.22, 0.22):
            o.member((-(LX + 0.09), y, zl + dz), (xs, y, zl + dz), 0.05, 0.05, GALV)
    # cage
    y = Y0 + 2.4
    cx = xs - 0.30
    while y < ytop - 0.2:
        ring = []
        for i in range(13):
            a = math.radians(-135 + i * 22.5)
            ring.append((cx + 0.0 + math.cos(a) * 0.38 * -1, y, zl + math.sin(a) * 0.38))
        # arc opens toward the tower (x+): use points on the outer side
        for i in range(len(ring) - 1):
            o.member(ring[i], ring[i + 1], 0.035, 0.035, GALV, up=(0, 1, 0))
        y += 0.9
    for i in range(7):
        a = math.radians(-135 + i * 45)
        px, pz = cx + math.cos(a) * -0.38, zl + math.sin(a) * 0.38
        o.member((px, Y0 + 2.4, pz), (px, ytop - 0.3, pz), 0.03, 0.03, GALV, up=(1, 0, 0))
    # rear landings
    for k in (4, 7, 10):
        y = Y0 + 3.0 * k
        zf = CZ - LZ - LEG / 2
        depth = 1.1
        cxm, w = 0.0, 2.5
        o.box((cxm, y + 0.02, zf - depth / 2), ((1, 0, 0), (0, 1, 0), (0, 0, 1)), (w / 2, 0.02, depth / 2), GRATING)
        for sx in (-1, 1):
            o.member((sx * w / 2, y - 0.02, zf - 0.02), (sx * w / 2, y - 0.02, zf - depth), 0.05, 0.12, GALV)
        o.member((-w / 2, y - 0.02, zf - depth), (w / 2, y - 0.02, zf - depth), 0.05, 0.12, GALV)
        for xo in (-w / 2 + 0.12, w / 2 - 0.12):   # triangular brackets back to the rear ring beam
            # plate in the (z, y) plane: ux = -z direction (away from tower), vx = up
            o.plate((xo, y - 0.35, zf), (0, 0, -1), (0, 1, 0),
                    [(0.0, 0.33), (depth - 0.05, 0.33), (0.0, -0.1)], 0.014, PAINT)
            for (bz, by) in ((0.1, 0.25), (0.3, 0.2)):
                o.bolt((xo + 0.008, y - 0.35 + by, zf - bz), (1, 0, 0), 0.013, 0.01)
        # handrails on 3 sides
        post_pts = [(-w / 2, zf - depth), (-w / 2 + 0.0, zf - depth / 2), (-w / 2, zf), (w / 2, zf - depth), (w / 2, zf - depth / 2), (w / 2, zf),
                    (-w / 4, zf - depth), (0.0, zf - depth), (w / 4, zf - depth)]
        for (px, pz) in post_pts:
            o.member((px, y + 0.04, pz), (px, y + 1.1, pz), 0.045, 0.045, OCHRE, up=(1, 0, 0))
        def rail(a, b, yy, r=0.022):
            o.tube((a[0], yy, a[1]), (b[0], yy, b[1]), r, OCHRE, seg=8)
        for yy in (y + 1.1, y + 0.55):
            rail((-w / 2, zf - depth), (w / 2, zf - depth), yy)
            rail((-w / 2, zf), (-w / 2, zf - depth), yy)
            rail((w / 2, zf), (w / 2, zf - depth), yy)
        # toe boards
        o.member((-w / 2, y + 0.09, zf - depth), (w / 2, y + 0.09, zf - depth), 0.012, 0.15, GALV)
        for sx in (-1, 1):
            o.member((sx * w / 2, y + 0.09, zf), (sx * w / 2, y + 0.09, zf - depth), 0.012, 0.15, GALV)


def build_cladding(o):
    """bolted batten strips over the existing shaft panels (flush 12 mm proud of the measured shaft faces)."""
    faces = [((1, 0, 0), (0, 0, 1), SHAFT_X, 1.55), ((-1, 0, 0), (0, 0, 1), SHAFT_X, 1.55),
             ((0, 0, 1), (1, 0, 0), SHAFT_Z1 - CZ, 1.55), ((0, 0, -1), (1, 0, 0), CZ - SHAFT_Z0, 1.55)]
    for n, t, off, half in faces:
        n, t = Vector(n), Vector(t)
        offv = abs(off) if not isinstance(off, tuple) else off
        for k in range(10):
            y0 = Y0 + 3.0 * k + (0.9 if k == 0 else 0.15)
            y1 = Y0 + 3.0 * (k + 1) - 0.15
            for s in (-0.55, 0.55):
                c0 = Vector((0, y0, CZ)) + n * (abs(off) + 0.012) + t * s
                c1 = Vector((0, y1, CZ)) + n * (abs(off) + 0.012) + t * s
                o.member(c0, c1, 0.09, 0.024, PAINT, up=n)
                yb = y0 + 0.25
                while yb < y1 - 0.1:
                    o.bolt(Vector((0, yb, CZ)) + n * (abs(off) + 0.024) + t * s, n, 0.012, 0.008)
                    yb += 0.5
