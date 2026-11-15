"""Deck underside support, service ledge, pivot pedestal, cable trays, deluge pipework, drainage, markings, arm details."""
import math
from mathutils import Vector
from tw_kit import *
from tw_layout import *
from tw_truss import LX, LZ, CZ, LEG, Y0, LEVELS_Y, Y_TOP_LEG, Y_TOP_LEG_FRONT

AXES = ((1, 0, 0), (0, 1, 0), (0, 0, 1))


def tri_gusset(o, c, ux, vx, size, mat=PAINT, th=0.014, bolts=3, out=None):
    pts = [(-size / 2, -size / 2), (size / 2, -size / 2), (size / 2, size / 2 - 0.12), (size / 2 - 0.12, size / 2), (-size / 2 + 0.12, size / 2), (-size / 2, size / 2 - 0.12)]
    o.plate(c, ux, vx, pts, th, mat)
    ux, vx = Vector(ux).normalized(), Vector(vx).normalized()
    n = ux.cross(vx).normalized()
    if out is not None and n.dot(Vector(out)) < 0:
        n = -n
    for i in range(bolts):
        for j in range(bolts):
            p = Vector(c) + ux * ((i - (bolts - 1) / 2) * 0.11) + vx * ((j - (bolts - 1) / 2) * 0.11) + n * (th / 2)
            o.bolt(p, n, 0.014, 0.01)


def build_deck_support(o):
    """knee braces under the deck, service ledge outside the existing rails, pivot pedestal, drencher header."""
    ytop, ylow = -0.66, -2.6
    # knee braces: +x and -x sides (two each), -z side (two)
    braces = []
    for sx in (-1, 1):
        for zo in (-1.1, 1.1):
            braces.append(((sx * (LX + 0.09), ylow, CZ + zo), (sx * 3.75, ytop, CZ + zo), (sx, 0, 0)))
    for sx in (-1, 1):
        braces.append(((sx * 1.1, ylow, CZ - LZ - 0.09), (sx * 1.1, ytop, -12.62), (0, 0, -1)))
    for a, b, n in braces:
        o.member(a, b, 0.14, 0.18, PAINT, up=(0, 1, 0) if n[0] == 0 else (0, 0, 1))
        t = Vector((0, 0, 1)) if n[0] else Vector((1, 0, 0))
        for p in (a, b):
            for side in (-1, 1):
                tri_gusset(o, Vector(p) + t * (side * 0.078), Vector(n), (0, 1, 0), 0.36, PAINT, 0.012, 3, out=t * side)
        # flange plate bolted to the girder / ring beam
        o.aabb((b[0] - 0.2, b[1] - 0.025, b[2] - 0.2), (b[0] + 0.2, b[1] + 0.0, b[2] + 0.2), GALV)
    # service ledge: +x, -x and -z edges, bracketed to the existing perimeter box girders
    def ledge(p0, p1, d_out, y=-0.44, w=0.62):
        p0, p1, d = Vector(p0), Vector(p1), Vector(d_out)
        L = (p1 - p0).length
        t = (p1 - p0).normalized()
        c = (p0 + p1) / 2 + d * (w / 2)
        ex, ey, ez = t, Vector((0, 1, 0)), d
        o.box(c + Vector((0, 0.01, 0)), (ex, ey, ez), (L / 2, 0.02, w / 2), GRATING)
        o.member(p0 + d * w, p1 + d * w, 0.06, 0.12, GALV, up=(0, 1, 0))
        o.member(p0 + d * 0.02, p1 + d * 0.02, 0.05, 0.10, GALV, up=(0, 1, 0))
        n_posts = max(2, int(L / 1.15) + 1)
        for i in range(n_posts):
            s = i / (n_posts - 1)
            q = p0 + (p1 - p0) * s + d * (w - 0.03)
            o.member(q + Vector((0, 0.03, 0)), q + Vector((0, 1.1, 0)), 0.045, 0.045, OCHRE, up=t)
            # bracket under the ledge
            bq = p0 + (p1 - p0) * s
            o.plate(bq + Vector((0, -0.2, 0)) + d * 0.3, d, (0, 1, 0),
                    [(-0.30, 0.17), (0.30, 0.17), (-0.30, -0.15)], 0.014, PAINT)
        for yy in (1.1, 0.55):
            o.tube(p0 + d * (w - 0.03) + Vector((0, yy, 0)), p1 + d * (w - 0.03) + Vector((0, yy, 0)), 0.022, OCHRE, seg=8)
        o.member(p0 + d * (w - 0.03) + Vector((0, 0.08, 0)), p1 + d * (w - 0.03) + Vector((0, 0.08, 0)), 0.012, 0.15, GALV, up=(0, 1, 0))
    ledge((4.0, -0.44, -12.78), (4.0, -0.44, -7.05), (1, 0, 0))
    ledge((-4.0, -0.44, -12.78), (-4.0, -0.44, -7.35), (-1, 0, 0))
    ledge((-3.9, -0.44, -12.9), (3.9, -0.44, -12.9), (0, 0, -1))
    # slew pedestal under the arm root (static lower ring)
    px, py, pz = ARM_PIVOT
    o.tube((px, -1.55, pz), (px, -1.04, pz), 0.5, GALV, seg=24)
    o.tube((px, -1.04, pz), (px, -1.0, pz), 0.58, PAINT, seg=24)
    for i in range(24):
        a = 2 * math.pi * i / 24
        o.bolt((px + math.cos(a) * 0.54, -1.0, pz + math.sin(a) * 0.54), (0, 1, 0), 0.018, 0.016)
    o.member((-LX, ylow, CZ + LZ), (px - 0.38, -1.55, pz + 0.05), 0.16, 0.2, PAINT)
    o.member((0.0, ylow, CZ + LZ), (px + 0.38, -1.55, pz + 0.05), 0.16, 0.2, PAINT)
    o.member((px - 0.4, -1.55, pz), (px + 0.4, -1.55, pz), 0.2, 0.1, PAINT)


def build_cable_trays(o):
    """two vertical cable trays on the rear face, junction boxes under the deck, brackets to the ring beams."""
    zt = CZ - LZ - LEG / 2 - 0.16
    for xt in (-1.7, 1.7):
        y0, y1 = Y0 + 1.1, -1.15
        for dx in (-0.2, 0.2):
            o.member((xt + dx, y0, zt), (xt + dx, y1, zt), 0.045, 0.10, GALV, up=(0, 0, 1))
        y = y0 + 0.15
        while y < y1:
            o.member((xt - 0.2, y, zt), (xt + 0.2, y, zt), 0.02, 0.025, GALV, up=(0, 0, 1))
            y += 0.30
        for dx in (-0.09, 0.0, 0.09):    # cable bundles
            o.tube((xt + dx, y0, zt - 0.025 + (0.0 if dx else 0.03)), (xt + dx, y1 - 0.1, zt - 0.025 + (0.0 if dx else 0.03)), 0.024, RUBBER, seg=8)
        y = y0 + 0.9
        while y < y1:   # cable clamps
            o.member((xt - 0.12, y, zt - 0.05), (xt + 0.12, y, zt - 0.05), 0.02, 0.05, GALV, up=(0, 0, 1))
            y += 1.5
        for y in [Y0 + 3.0 * k + 0.6 for k in range(0, 10)]:     # bracket arms to the ring beams
            for dx in (-0.2, 0.2):
                o.member((xt + dx, y, zt + 0.0), (xt + dx, y, CZ - LZ - 0.09), 0.05, 0.05, GALV)
        # junction box under the deck
        o.aabb((xt - 0.3, y1 - 0.02, zt - 0.2), (xt + 0.3, y1 + 0.38, zt + 0.15), PAINT)
        for ix in (-1, 1):
            for iy in (-1, 1):
                o.bolt((xt + ix * 0.25, y1 + 0.19 + iy * 0.12, zt - 0.2), (0, 0, -1), 0.014, 0.01)


def build_deluge(o):
    """riser DN200 on the +x face, base valve station, drencher header under the deck, scupper downpipes."""
    rx, rz = LX + 0.34, CZ + 0.9
    pr = 0.11
    ybase = Y0 + 0.45
    ytopr = -1.35
    # riser with flanged joints and standoffs
    o.tube_path([(rx + 3.2, ybase, rz), (rx, ybase, rz), (rx, ytopr, rz), (rx + 1.15, ytopr, rz)], pr, RED, bend=0.35, seg=14)
    for y in [Y0 + 3.0 * k for k in range(1, 11)]:
        o.flange((rx, y, rz), (0, 1, 0), pr * 1.55, RED, 8)
    for y in [Y0 + 3.0 * k - 1.5 for k in range(1, 11)]:     # clamps + standoffs
        o.member((LX + 0.09, y, rz), (rx, y, rz), 0.06, 0.06, GALV)
        o.aabb((rx - pr - 0.03, y - 0.04, rz - pr - 0.03), (rx + pr + 0.03, y + 0.04, rz + pr + 0.03), GALV)
    # base valve station on the run towards +x
    vx_ = rx + 1.9
    o.tube((vx_, ybase, rz - 0.0), (vx_, ybase + 0.8, rz), 0.045, GALV, seg=8)               # stem
    o.aabb((vx_ - 0.17, ybase - 0.17, rz - 0.17), (vx_ + 0.17, ybase + 0.17, rz + 0.17), RED)
    o.tube((vx_, ybase + 0.17, rz), (vx_, ybase + 0.55, rz), 0.09, RED, seg=10)
    o.tube((vx_ - 0.3, ybase + 0.82, rz), (vx_ + 0.3, ybase + 0.82, rz), 0.02, RED, seg=8)
    o.tube((vx_, ybase + 0.82, rz - 0.3), (vx_, ybase + 0.82, rz + 0.3), 0.02, RED, seg=8)
    o.flange((vx_ - 0.22, ybase, rz), (1, 0, 0), pr * 1.55, RED, 8)
    o.flange((vx_ + 0.22, ybase, rz), (1, 0, 0), pr * 1.55, RED, 8)
    o.tube((rx + 3.2, ybase, rz), (rx + 3.26, ybase, rz), pr * 1.6, RED, seg=14)
    # pressure gauge stub
    o.tube((rx + 0.9, ybase + 0.1, rz), (rx + 0.9, ybase + 0.4, rz), 0.015, GALV, seg=6)
    o.tube((rx + 0.9, ybase + 0.4, rz), (rx + 0.9, ybase + 0.4, rz - 0.04), 0.05, GALV, seg=14)
    # saddles / supports under the run
    for xs in (rx + 0.6, rx + 1.3, rx + 2.6):
        o.aabb((xs - 0.08, Y0 + 0.0, rz - 0.18), (xs + 0.08, ybase - pr, rz + 0.18), PAINT)
    # drencher header under the deck: +x edge then -z edge
    yh = -0.98
    hx, hz0, hz1 = 3.55, -12.45, -7.3
    o.tube_path([(rx + 1.15, ytopr, rz), (hx, ytopr, rz), (hx, yh, rz), (hx, yh, hz1)], 0.06, RED, bend=0.25, seg=10)
    o.tube((hx, yh, hz1), (hx, yh, hz0), 0.06, RED, seg=10)
    o.tube_path([(hx, yh, hz0), (-3.55, yh, hz0)], 0.06, RED, bend=0.2, seg=10)
    # drencher heads
    def head(p):
        p = Vector(p)
        o.tube(p, p + Vector((0, -0.07, 0)), 0.018, GALV, seg=6)
        o.cone(p + Vector((0, -0.07, 0)), p + Vector((0, -0.14, 0)), 0.018, 0.045, GALV, seg=8)
        o.tube(p + Vector((0, -0.145, 0)), p + Vector((0, -0.155, 0)), 0.05, GALV, seg=8)
    n = 0
    z = hz1 - 0.3
    while z > hz0 + 0.2:
        head((hx, yh - 0.06, z)); z -= 0.85; n += 1
    x = hx - 0.45
    while x > -3.5:
        head((x, yh - 0.06, hz0)); x -= 0.85; n += 1
    # hanger rods
    for (xx, zz) in [(hx, hz1 - 0.2), (hx, -9.9), (hx, hz0 + 0.2), (1.5, hz0), (-1.5, hz0), (-3.4, hz0)]:
        o.tube((xx, yh + 0.06, zz), (xx, -0.62, zz), 0.011, GALV, seg=6)
        o.aabb((xx - 0.07, yh - 0.0, zz - 0.07), (xx + 0.07, yh + 0.08, zz + 0.07), GALV)
    # scupper downpipes: deck corners -> tower corner legs -> pad catch basins
    for sx in (-1, 1):
        sc = (sx * 3.25, -0.58, -12.35)
        o.aabb((sc[0] - 0.2, sc[1] - 0.1, sc[2] - 0.2), (sc[0] + 0.2, sc[1] + 0.05, sc[2] + 0.2), GALV)
        px_, pz_ = sx * (LX + 0.38), CZ - LZ - 0.38
        pts = [(sc[0], sc[1] - 0.1, sc[2]), (sc[0], -1.7, sc[2]), (px_, -1.7, pz_), (px_, Y0 + 0.25, pz_)]
        o.tube_path(pts, 0.065, GALV, bend=0.3, seg=10)
        for y in [Y0 + 3.0 * k - 1.0 for k in range(1, 11)]:
            o.member((sx * (LX + 0.09), y, CZ - LZ - 0.09), (px_, y, pz_), 0.05, 0.05, GALV)
            o.aabb((px_ - 0.08, y - 0.04, pz_ - 0.08), (px_ + 0.08, y + 0.04, pz_ + 0.08), GALV)
        # catch basin with grated lid
        o.aabb((px_ - 0.45, Y0, pz_ - 0.45), (px_ + 0.45, Y0 + 0.07, pz_ + 0.45), PAINT)
        o.aabb((px_ - 0.36, Y0 + 0.07, pz_ - 0.36), (px_ + 0.36, Y0 + 0.095, pz_ + 0.36), GRATING)
        o.tube((px_, Y0 + 0.25, pz_), (px_, Y0 + 0.07, pz_), 0.065, GALV, seg=10)


def build_markings(o, labels):
    """ochre guards, hazard stripes, painted bands and AURORA-7 plates."""
    # leg foot guards (hazard)
    for sx in (-1, 1):
        for sz in (-1, 1):
            x, z = sx * LX, CZ + sz * LZ
            o.aabb((x - 0.14, Y0 + 0.04, z - 0.14), (x + 0.14, Y0 + 1.1, z + 0.14), HAZARD)
    # ochre bands on the top ring beams
    zt = CZ - LZ - LEG / 2
    for (nm, n, t, off, half) in [('+x', (1, 0, 0), (0, 0, 1), LX, LZ), ('-x', (-1, 0, 0), (0, 0, 1), LX, LZ),
                                  ('-z', (0, 0, -1), (1, 0, 0), LZ, LX), ('+z', (0, 0, 1), (1, 0, 0), LZ, LX)]:
        n, t = Vector(n), Vector(t)
        c = Vector((0, LEVELS_Y[-1] - 0.0, CZ)) + n * (off + LEG / 2 + 0.004)
        o.rect_plate(c, t, (0, 1, 0), half * 2 - 0.5, 0.11, 0.006, OCHRE)
    # label plates
    def plate_label(key, c, right, up, w, mat_bg=True):
        r = LABELS[key]
        h = w * (r[3] - r[1]) * 2048 / ((r[2] - r[0]) * 2048) * 1024 / 2048 * 1.0
        # aspect from atlas pixels
        pw = (r[2] - r[0]) * ATLAS_W
        ph = (r[3] - r[1]) * ATLAS_H
        h = w * ph / pw
        o.quad(c, right, up, w, h, LABEL, uvrect=r)
        return h
    zt_plate = CZ - LZ - LEG / 2 - 0.012
    plate_label('tower_main', (0, -4.3, zt_plate), (1, 0, 0), (0, 1, 0), 2.6)
    rx, rz = LX + 0.34, CZ + 0.9
    plate_label('deluge', (LX + LEG / 2 + 0.012, Y0 + 5.5, CZ - 0.9), (0, 0, 1), (0, 1, 0), 0.9)
    plate_label('cable', (-1.7 + 0.0, Y0 + 4.0, CZ - LZ - LEG / 2 - 0.34), (1, 0, 0), (0, 1, 0), 0.7)
    plate_label('valve', (rx + 1.9, Y0 + 0.62, rz - 0.19), (1, 0, 0), (0, 1, 0), 0.45)
    for i in range(1, 11):
        y = Y0 + 3.0 * i + 0.9 if i < 10 else Y0 + 3.0 * i - 0.9
        r = LEVELS[i]
        o.quad((-(LX + LEG / 2 + 0.012), y, CZ + LZ), (0, 0, -1), (0, 1, 0), 0.30, 0.30 * (0.25 * ATLAS_H) / (0.1 * ATLAS_W), LABEL, uvrect=r)


def build_arm(o):
    """details on the retractable crew arm (object origin = arm pivot so the root can rotate it with the arm)."""
    px, py, pz = ARM_PIVOT
    z0, z1 = -6.55, -3.25
    # --- slew housing at the pivot (moving upper ring) ---
    o.tube((px, -1.0, pz), (px, -0.58, pz), 0.52, PAINT, seg=24)
    o.tube((px, -1.02, pz), (px, -0.97, pz), 0.58, GALV, seg=24)
    for i in range(24):
        a = 2 * math.pi * i / 24
        o.bolt((px + math.cos(a) * 0.55, -0.97, pz + math.sin(a) * 0.55), (0, -1, 0), 0.018, 0.016)
    o.aabb((px - 0.62, -0.95, pz - 0.2), (px - 0.42, -0.62, pz + 0.2), RUBBER)     # drive motor
    # --- two side trusses under the arm floor ---
    ytop_c, ybot_c = -0.60, -1.12
    nb = 4
    zs = [z0 + (z1 - z0) * i / nb for i in range(nb + 1)]
    for xs in (-0.80, 0.80):
        o.member((xs, ytop_c, z0), (xs, ytop_c, z1), 0.07, 0.10, PAINT, up=(1, 0, 0))
        o.member((xs, ybot_c, z0 + 0.25), (xs, ybot_c, z1 - 0.25), 0.07, 0.10, PAINT, up=(1, 0, 0))
        for i, z in enumerate(zs):
            zb = min(max(z, z0 + 0.25), z1 - 0.25)
            o.member((xs, ytop_c, z), (xs, ybot_c, zb), 0.07, 0.09, PAINT)
        for i in range(nb):
            za, zb = zs[i], zs[i + 1]
            za_b, zb_b = min(max(za, z0 + 0.25), z1 - 0.25), min(max(zb, z0 + 0.25), z1 - 0.25)
            if i % 2 == 0:
                o.member((xs, ytop_c, za), (xs, ybot_c, zb_b), 0.06, 0.08, PAINT)
            else:
                o.member((xs, ybot_c, za_b), (xs, ytop_c, zb), 0.06, 0.08, PAINT)
        sgn = 1 if xs > 0 else -1
        for z in zs:
            for yy in (ytop_c, ybot_c):
                zc = min(max(z, z0 + 0.25), z1 - 0.25) if yy == ybot_c else z
                c = Vector((xs + sgn * 0.045, yy, zc))
                tri_gusset(o, c, (0, 0, 1), (0, 1, 0), 0.30, PAINT, 0.012, 2, out=(sgn, 0, 0))
    for i in range(nb):     # plan bracing between the trusses
        za, zb = zs[i], zs[i + 1]
        o.member((-0.8, ybot_c, za), (0.8, ybot_c, zb), 0.05, 0.05, GALV)
        o.member((0.8, ybot_c, za), (-0.8, ybot_c, zb), 0.05, 0.05, GALV)
        o.member((-0.8, ybot_c, za), (0.8, ybot_c, za), 0.06, 0.06, GALV)
    # --- energy chain on +x outside the rail, with trough and end boxes ---
    xc, yc = 1.14, 0.12
    o.member((xc, yc - 0.1, z0 + 0.1), (xc, yc - 0.1, z1 - 0.1), 0.20, 0.025, GALV, up=(0, 1, 0))   # trough bottom
    z = z0 + 0.2
    while z < z1 - 0.15:
        o.aabb((xc - 0.075, yc - 0.06, z), (xc + 0.075, yc + 0.07, z + 0.040), RUBBER)
        o.aabb((xc - 0.085, yc - 0.06, z), (xc - 0.07, yc + 0.07, z + 0.040), GALV)
        o.aabb((xc + 0.07, yc - 0.06, z), (xc + 0.085, yc + 0.07, z + 0.040), GALV)
        z += 0.048
    for zz in (z0 + 0.1, (z0 + z1) / 2, z1 - 0.1):
        o.member((0.98, yc - 0.06, zz), (xc, yc - 0.06, zz), 0.05, 0.05, GALV)   # brackets to the arm side
    o.aabb((xc - 0.14, yc - 0.12, z0 - 0.12), (xc + 0.14, yc + 0.16, z0 + 0.12), PAINT)
    o.aabb((xc - 0.14, yc - 0.12, z1 - 0.12), (xc + 0.14, yc + 0.16, z1 + 0.12), PAINT)
    # --- fluid lines on +x above the energy chain (keeps the -x side clear of the deck rails when the arm retracts) ---
    xl = 1.14
    o.tube((xl, 0.34, z0 + 0.05), (xl, 0.34, z1 - 0.25), 0.04, GALV, seg=10)
    o.tube((xl, 0.48, z0 + 0.05), (xl, 0.48, z1 - 0.25), 0.032, RED, seg=10)
    o.tube((xl, 0.34, z1 - 0.25), (xl, 0.34, z1 - 0.05), 0.055, HAZARD, seg=10)
    for z in [z0 + 0.4 + 0.75 * i for i in range(5)]:
        o.member((0.98, 0.4, z), (xl, 0.4, z), 0.05, 0.05, GALV)
        o.aabb((xl - 0.06, 0.28, z - 0.025), (xl + 0.06, 0.55, z + 0.025), GALV)
    # --- crew-connection hood (portal around the arm head), boot rings and sill ---
    zh0, zh1 = -3.55, -3.17
    for s in (-1, 1):
        o.aabb((s * 1.06 - 0.08, -0.72, zh0), (s * 1.06 + 0.08, 2.42, zh1), PAINT)
        for yy in (0.2, 0.9, 1.6):
            o.bolt((s * 1.06 + s * 0.08, yy + 0.0, (zh0 + zh1) / 2), (s, 0, 0), 0.02, 0.014)
            o.bolt((s * 1.06 + s * 0.08, yy + 0.35, (zh0 + zh1) / 2), (s, 0, 0), 0.02, 0.014)
    o.aabb((-1.14, 2.26, zh0 - 0.04), (1.14, 2.50, zh1 + 0.04), PAINT)          # header
    o.aabb((-1.14, -0.76, zh0), (1.14, -0.62, zh1), PAINT)                         # sill under the arm
    o.rect_plate((0, 2.33, zh1 + 0.045), (1, 0, 0), (0, 1, 0), 2.2, 0.10, 0.008, HAZARD)
    # boot (rubber bellows rings) on the capsule side of the hood
    for i in range(3):
        zb = zh1 + 0.02 + i * 0.045
        e = 0.045 if i % 2 == 0 else 0.0
        o.aabb((-1.08 - e, -0.70 - e, zb), (1.08 + e, -0.62, zb + 0.035), RUBBER)
        o.aabb((-1.08 - e, 2.26, zb), (1.08 + e, 2.34 + e, zb + 0.035), RUBBER)
        for s in (-1, 1):
            o.aabb((s * 1.0 + (0 if s < 0 else 0) - (0.08 + e if s < 0 else 0), -0.70, zb), (s * 1.0 + (0.08 + e if s > 0 else 0), 2.34, zb + 0.035), RUBBER)
    # label above the header (rear side so the player sees it when walking onto the arm)
    r = LABELS['crew_hood']
    pw = (r[2] - r[0]) * ATLAS_W
    ph = (r[3] - r[1]) * ATLAS_H
    w = 1.5
    o.quad((0, 2.38, zh0 - 0.046), (1, 0, 0), (0, 1, 0), w, w * ph / pw, LABEL, uvrect=r)
    # diagonal hood stiffeners back to the arm rail line
    for s in (-1, 1):
        o.member((s * 1.06, 1.9, (zh0 + zh1) / 2), (s * 1.06, 2.3, (zh0 + zh1) / 2), 0.06, 0.06, GALV)
