"""AURORA capsules V02: original, fully scripted hero vehicles for OUT OF REACH.

  RC  rescue craft          (flown, subtle soot and scorch)   -> AuroraRescueCraft.fbx
  CT  crew transport capsule (pad-fresh)                       -> AuroraCrewTransport.fbx

Both are drop-in replacements for the delivered TransportLaunch.fbx parts `CapsuleShell` and `Hatch`:
same Blender frame (metres, Z up, hatch on +Y, floor at z 0), same hatch hinge origin (0.72, 2.4, 0),
same door opening (x +-0.70, z 0..2.15), same docking datum (z 4.95) and the same top of bounds (z 5.10).

  blender --background --python build_capsules.py -- --out <AuroraCapsulesV02 dir> [--variant RC|CT|all]
                                                     [--texscale 1.0] [--notex] [--norender]

The fictional craft carry AURORA markings only. Lettering is Barlow Condensed Bold (SIL OFL 1.1, already
shipped in Assets/_OutOfReach/UI/Typography), converted to flat mesh.
"""
import json
import math
import os
import sys
import time

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import ac_tex  # noqa: E402

TAU = 2.0 * math.pi
D = math.radians

# ----------------------------------------------------------------------------- shared frame
R_CYL, R_TOP = 2.50, 0.93
Z_SHO, Z_TOP = 2.60, 4.42
S_CYL = Z_SHO
CONE_LEN = math.hypot(R_CYL - R_TOP, Z_TOP - Z_SHO)
S_TOT = S_CYL + CONE_LEN
CN_R, CN_Z = (Z_TOP - Z_SHO) / CONE_LEN, (R_CYL - R_TOP) / CONE_LEN
TH_U0 = D(67.5)                       # a gore seam; the hatch gore is centred on the hatch
HATCH = D(90.0)
TH_DH = math.asin(0.70 / R_CYL)       # half angle of the door opening
TH_DOOR = TH_DH - 0.003 / R_CYL       # half angle of the door leaf: 3 mm reveal, so no light slot beside it
DOOR_Z1 = 2.15
R_JAMB = 2.365
DOOR_T = R_CYL - R_JAMB               # leaf thickness
HINGE = Vector((0.72, 2.4, 0.0))
R_SHIELD = 2.6                        # planar heat-shield map half size
G = dict(S_CYL=S_CYL, CONE_LEN=CONE_LEN, S_TOT=S_TOT, R_CYL=R_CYL, R_TOP=R_TOP, TH_U0=TH_U0)

HULL, SHIELD, METAL, GRAPH, KIT = range(5)
K = ac_tex.kit_uv
FONT_PATH = os.path.normpath(os.path.join(HERE, '..', '..', '..', '..', 'UI', 'Typography', 'BarlowCondensed-Bold.ttf'))


def hull_pn(th, s):
    if s <= S_CYL:
        r, z, nr, nz = R_CYL, s, 1.0, 0.0
    else:
        t = (s - S_CYL) / CONE_LEN
        r, z, nr, nz = R_CYL + (R_TOP - R_CYL) * t, Z_SHO + (Z_TOP - Z_SHO) * t, CN_R, CN_Z
    c, sn = math.cos(th), math.sin(th)
    return Vector((r * c, r * sn, z)), Vector((nr * c, nr * sn, nz)), r


def hp(th, s, h=0.0):
    p, n, _ = hull_pn(th, s)
    return p + n * h


def frame(o, x, y, z):
    return Matrix(((x.x, y.x, z.x, o.x), (x.y, y.y, z.y, o.y), (x.z, y.z, z.z, o.z), (0, 0, 0, 1)))


def hframe(th, s, h=0.0):
    """x = towards increasing azimuth (viewer's right from outside), y = up the profile, z = outward."""
    p, n, _ = hull_pn(th, s)
    t = Vector((-math.sin(th), math.cos(th), 0.0))
    return frame(p + n * h, t, n.cross(t), n)


def hframe_in(th, s, depth):
    """Same point pushed `depth` inside, facing the cabin."""
    return hframe(th, s, -depth) @ Matrix.Rotation(math.pi, 4, 'Y')


def rframe(r, th, z):
    """x = tangent, y = up, z = radially outward (for parts on rings about the axis)."""
    c, sn = math.cos(th), math.sin(th)
    return frame(Vector((r * c, r * sn, z)), Vector((-sn, c, 0)), Vector((0, 0, 1)), Vector((c, sn, 0)))


def upframe(p, th=0.0):
    """Axis-aligned frame at p: x = tangent at azimuth th, y = inward radial, z = up."""
    c, sn = math.cos(th), math.sin(th)
    return frame(p, Vector((-sn, c, 0)), Vector((-c, -sn, 0)), Vector((0, 0, 1)))


# ----------------------------------------------------------------------------- mesh builder
class MB:
    def __init__(self):
        self.v, self.f, self.m, self.uv = [], [], [], []

    def vert(self, p):
        self.v.append((p[0], p[1], p[2]))
        return len(self.v) - 1

    def face(self, idx, mat, uv=None):
        self.f.append(tuple(idx))
        self.m.append(mat)
        self.uv.append(uv)

    def poly(self, pts, mat, uv=None, toward=None):
        """Polygon from points; with `toward`, wound so that its normal faces that point."""
        if toward is not None:
            n = Vector((0, 0, 0))
            for i in range(len(pts)):
                a, b = pts[i], pts[(i + 1) % len(pts)]
                n += Vector(((a.y - b.y) * (a.z + b.z), (a.z - b.z) * (a.x + b.x), (a.x - b.x) * (a.y + b.y)))
            c = sum(pts, Vector((0, 0, 0))) / len(pts)
            if n.dot(toward - c) < 0:
                pts = pts[::-1]
                if isinstance(uv, list):
                    uv = uv[::-1]
        self.face([self.vert(p) for p in pts], mat, uv)

    def tris(self):
        return sum(len(f) - 2 for f in self.f)


def lathe(mb, prof, mat, uv=None, segs=24, O=None, ax=None, e1=None, thetas=None, skip=None):
    """Revolve (radius, axial) points. Walking along the profile, the surface faces the right-hand side
    (so bottom-to-top gives an outward wall, top-to-bottom an inward one, outward along a floor faces down)."""
    O = Vector((0, 0, 0)) if O is None else O
    ax = Vector((0, 0, 1)) if ax is None else ax.normalized()
    e1 = Vector((1, 0, 0)) if (e1 is None and abs(ax.z) > 0.999) else (ax.orthogonal() if e1 is None else e1)
    e1 = (e1 - ax * e1.dot(ax)).normalized()
    e2 = ax.cross(e1)
    if thetas is None:
        thetas = [TAU * i / segs for i in range(segs + 1)]
    cs = [(math.cos(t), math.sin(t)) for t in thetas]
    rows = []
    for (r, a) in prof:
        base = O + ax * a
        rows.append([mb.vert(base + (e1 * c + e2 * s) * r) for (c, s) in cs])
    call = callable(uv)
    for j in range(len(prof) - 1):
        r0, r1 = prof[j][0], prof[j + 1][0]
        if r0 < 1e-7 and r1 < 1e-7:
            continue
        for i in range(len(thetas) - 1):
            if skip and skip(j, i):
                continue
            a, b, c, d = rows[j][i], rows[j][i + 1], rows[j + 1][i + 1], rows[j + 1][i]
            q = [uv(thetas[i], j), uv(thetas[i + 1], j), uv(thetas[i + 1], j + 1), uv(thetas[i], j + 1)] if call else None
            if r0 < 1e-7:
                mb.face((a, c, d), mat, [q[0], q[2], q[3]] if call else uv)
            elif r1 < 1e-7:
                mb.face((a, b, c), mat, [q[0], q[1], q[2]] if call else uv)
            else:
                mb.face((a, b, c, d), mat, q if call else uv)


def obox(mb, M, x0, x1, y0, y1, z0, z1, mat, uv=None, bottom=False, taper=None):
    cx, cy = (x0 + x1) * 0.5, (y0 + y1) * 0.5
    co = ((x0, y0), (x1, y0), (x1, y1), (x0, y1))
    b = [mb.vert(M @ Vector((x, y, z0))) for (x, y) in co]
    if taper:
        t = [mb.vert(M @ Vector((cx + (x - cx) * taper[0], cy + (y - cy) * taper[1], z1))) for (x, y) in co]
    else:
        t = [mb.vert(M @ Vector((x, y, z1))) for (x, y) in co]
    mb.face(t, mat, uv)
    for k in range(4):
        mb.face((b[k], b[(k + 1) % 4], t[(k + 1) % 4], t[k]), mat, uv)
    if bottom:
        mb.face(b[::-1], mat, uv)


def oprism(mb, M, poly, z0, z1, mat, uv=None, bottom=False, top=True, side_uv=None):
    n = len(poly)
    b = [mb.vert(M @ Vector((x, y, z0))) for (x, y) in poly]
    t = [mb.vert(M @ Vector((x, y, z1))) for (x, y) in poly]
    if top:
        mb.face(t, mat, uv)
    for k in range(n):
        mb.face((b[k], b[(k + 1) % n], t[(k + 1) % n], t[k]), mat, side_uv if side_uv is not None else uv)
    if bottom:
        mb.face(b[::-1], mat, uv)


def ocyl(mb, M, r, z0, z1, mat, uv=None, segs=12, r1=None, cap1=True, cap0=False):
    prof = ([(0.0, z0)] if cap0 else []) + [(r, z0), (r if r1 is None else r1, z1)] + ([(0.0, z1)] if cap1 else [])
    lathe(mb, prof, mat, uv, segs, O=M.translation, ax=M.col[2].xyz, e1=M.col[0].xyz)


def dome(mb, M, r, mat, uv=None, segs=10, z=0.0, flat=1.0):
    prof = [(r * math.cos(D(a)), z + r * flat * math.sin(D(a))) for a in (0, 30, 60, 90)]
    lathe(mb, prof, mat, uv, segs, O=M.translation, ax=M.col[2].xyz, e1=M.col[0].xyz)


def tube(mb, pts, rad, mat, uv=None, segs=8, caps=True):
    n = len(pts)
    nrm = None
    rings = []
    for i in range(n):
        t = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized()
        nrm = t.orthogonal() if nrm is None else nrm - t * nrm.dot(t)
        if nrm.length < 1e-6:
            nrm = t.orthogonal()
        nrm.normalize()
        b = t.cross(nrm)
        rings.append([mb.vert(pts[i] + (nrm * math.cos(TAU * k / segs) + b * math.sin(TAU * k / segs)) * rad) for k in range(segs)])
    for i in range(n - 1):
        for k in range(segs):
            k1 = (k + 1) % segs
            mb.face((rings[i][k], rings[i][k1], rings[i + 1][k1], rings[i + 1][k]), mat, uv)
    if caps:
        mb.face(rings[0][::-1], mat, uv)
        mb.face(rings[-1], mat, uv)


def bez(a, c, b, n=4):
    return [a * (1 - t) ** 2 + c * 2 * t * (1 - t) + b * t * t for t in (i / n for i in range(n + 1))]


def rail(mb, A, nA, B, nB, standoff, rad, uv, segs=8, bend=0.04, base=True, lod=0):
    """Handrail from hull point A to hull point B on round standoffs."""
    P1, P2 = A + nA * standoff, B + nB * standoff
    d = (P2 - P1).normalized()
    nb = 4 if lod == 0 else 2
    pts = [A - nA * 0.012] + bez(P1 - nA * bend, P1, P1 + d * bend, nb) + bez(P2 - d * bend, P2, P2 - nB * bend, nb) + [B - nB * 0.012]
    tube(mb, pts, rad, KIT, uv, segs, caps=False)
    if base and lod == 0:
        for (P, n) in ((A, nA), (B, nB)):
            x = n.orthogonal().normalized()
            ocyl(mb, frame(P, x, n.cross(x), n), rad * 2.0, -0.004, 0.008, METAL, None, 8)


def patch(mb, pf, th0, th1, s0, s1, h0, h1, mat, uv=None, dth=D(3.0), walls=True, wall_mat=None, wall_uv=None,
          back_mat=None, splits=(), inward=False, top=True):
    """Slab that follows a surface: pf(th, s, h) -> point. Faces outward (or inward) between h0 and h1."""
    nth = max(1, int(math.ceil(abs(th1 - th0) / dth - 1e-6)))
    ths = [th0 + (th1 - th0) * i / nth for i in range(nth + 1)]
    ss = [s0] + [x for x in splits if s0 + 1e-6 < x < s1 - 1e-6] + [s1]
    hull_uv = uv if callable(uv) else None
    grid = [[mb.vert(pf(t, s, h1)) for t in ths] for s in ss]

    def put(idx, m, u):
        mb.face(idx[::-1] if inward else idx, m, u)

    if top:
        for j in range(len(ss) - 1):
            for i in range(nth):
                idx = (grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i])
                if hull_uv:
                    q = [hull_uv(ths[i], ss[j]), hull_uv(ths[i + 1], ss[j]), hull_uv(ths[i + 1], ss[j + 1]), hull_uv(ths[i], ss[j + 1])]
                    put(idx, mat, q[::-1] if inward else q)
                else:
                    put(idx, mat, uv)
    per = [(ths[i], ss[0]) for i in range(nth)] + [(ths[-1], s) for s in ss[:-1]] \
        + [(ths[i], ss[-1]) for i in range(nth, 0, -1)] + [(ths[0], s) for s in ss[:0:-1]]
    if walls:
        lo = [mb.vert(pf(t, s, h0)) for (t, s) in per]
        hi = [mb.vert(pf(t, s, h1)) for (t, s) in per]
        n = len(per)
        wm = mat if wall_mat is None else wall_mat
        wu = (None if hull_uv else uv) if wall_uv is None else wall_uv
        for k in range(n):
            put((lo[k], lo[(k + 1) % n], hi[(k + 1) % n], hi[k]), wm, wu)
    if back_mat is not None:
        g2 = [[mb.vert(pf(t, s, h0)) for t in ths] for s in ss]
        for j in range(len(ss) - 1):
            for i in range(nth):
                idx = (g2[j][i], g2[j + 1][i], g2[j + 1][i + 1], g2[j][i + 1])
                put(idx, back_mat, None)


def hpf(th, s, h):
    return hp(th, s, h)


def cylpf(R):
    return lambda th, z, h: Vector(((R + h) * math.cos(th), (R + h) * math.sin(th), z))


def frame_bars(mb, pf, th0, th1, s0, s1, w, h0, h1, mat, uv=None, r_ref=R_CYL):
    """Four conformal bars forming a rectangular border of width w (metres) just outside th0..th1 / s0..s1."""
    a = w / r_ref
    patch(mb, pf, th0 - a, th0, s0 - w, s1 + w, h0, h1, mat, uv, splits=(S_CYL,))
    patch(mb, pf, th1, th1 + a, s0 - w, s1 + w, h0, h1, mat, uv, splits=(S_CYL,))
    patch(mb, pf, th0, th1, s0 - w, s0, h0, h1, mat, uv)
    patch(mb, pf, th0, th1, s1, s1 + w, h0, h1, mat, uv)


# ----------------------------------------------------------------------------- lettering
_FONT = {}
_LODK = [1.0]                       # decals sit higher on the far LODs so they never z-fight


def text_on(mb, pf, rfun, string, th_c, s_c, cap, uv, h=0.004, res=3, track=1.0):
    """Flat lettering wrapped onto a surface. cap = capital height in metres."""
    if 'f' not in _FONT:
        _FONT['f'] = bpy.data.fonts.load(FONT_PATH) if os.path.exists(FONT_PATH) else None
    cu = bpy.data.curves.new('txt', 'FONT')
    cu.body = string
    if _FONT['f']:
        cu.font = _FONT['f']
    cu.size = 1.0
    cu.resolution_u = res
    cu.space_character = track
    ob = bpy.data.objects.new('txt', cu)
    bpy.context.scene.collection.objects.link(ob)
    dg = bpy.context.evaluated_depsgraph_get()
    dg.update()
    me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
    bm = bmesh.new()
    bm.from_mesh(me)
    bpy.data.objects.remove(ob)
    bpy.data.curves.remove(cu)
    bpy.data.meshes.remove(me)
    if not bm.verts:
        bm.free()
        return
    xs = [v.co.x for v in bm.verts]
    ys = [v.co.y for v in bm.verts]
    sc = cap / max(1e-6, (max(ys) - min(ys)))
    cx, cy = (max(xs) + min(xs)) * 0.5, (max(ys) + min(ys)) * 0.5
    for v in bm.verts:
        v.co.x = (v.co.x - cx) * sc
        v.co.y = (v.co.y - cy) * sc
        v.co.z = 0.0
    half = (max(xs) - min(xs)) * 0.5 * sc
    step = 0.075
    x = -half + step
    while x < half:
        bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(x, 0, 0), plane_no=(1, 0, 0))
        x += step
    bm.verts.ensure_lookup_table()
    bm.verts.index_update()
    bm.normal_update()
    base = len(mb.v)
    for v in bm.verts:
        s = s_c + v.co.y
        mb.vert(pf(th_c + v.co.x / rfun(s), s, h * _LODK[0]))
    for f in bm.faces:
        idx = [base + v.index for v in f.verts]
        if f.normal.z < 0:
            idx.reverse()
        mb.face(idx, KIT, uv)
    bm.free()


def hull_r(s):
    return hull_pn(0.0, s)[2]


# ----------------------------------------------------------------------------- parts
def nozzle(mb, O, ax, scale=1.0, segs=12):
    p_out = [(0.030, 0.015), (0.036, 0.070), (0.050, 0.125), (0.060, 0.160), (0.053, 0.160)]
    p_in = [(0.053, 0.160), (0.030, 0.078), (0.0, 0.072)]
    lathe(mb, [(r * scale, a * scale) for (r, a) in p_out], KIT, K('nozzle'), segs, O=O, ax=ax)
    lathe(mb, [(r * scale, a * scale) for (r, a) in p_in], KIT, K('nozzle_hot'), segs, O=O, ax=ax)


def rcs_pod(mb, pf, fr, th, s, r_loc, L, jets_out):
    """Four-jet reaction control quad on a conformal housing."""
    a = 0.205 / r_loc
    patch(mb, pf, th - a, th + a, s - 0.24, s + 0.24, -0.01, 0.055, GRAPH, splits=(S_CYL,))
    if L <= 1:
        patch(mb, pf, th - a * 0.72, th + a * 0.72, s - 0.17, s + 0.17, 0.055, 0.063, METAL)
        frame_bars(mb, pf, th - a - 0.045 / r_loc, th + a + 0.045 / r_loc, s - 0.285, s + 0.285, 0.020, -0.002, 0.0025 * (1 + L), KIT, K('amber'), r_loc)
    M = fr(th, s, 0.028)
    for (dx, dy) in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        o = M @ Vector((dx * 0.150, dy * 0.185, 0.0))
        ax = (M.to_3x3() @ Vector((dx, dy, 0.50))).normalized()
        if L <= 1:
            nozzle(mb, o, ax, 1.0, 12 if L == 0 else 8)
        else:
            lathe(mb, [(0.03, 0.02), (0.058, 0.16), (0.0, 0.16)], KIT, K('nozzle'), 6, O=o, ax=ax)
        jets_out.append((th + dx * 0.20 / r_loc, s + dy * 0.26, float(dx), float(dy), 0.85))
    if L == 0:
        for (x, y) in ((-0.17, -0.20), (0.17, -0.20), (0.17, 0.20), (-0.17, 0.20)):
            ocyl(mb, fr(th + x / r_loc, s + y, 0.055), 0.011, 0.0, 0.006, METAL, None, 6)


def lamp(mb, M, uvname, r=0.032, L=0):
    ocyl(mb, M, r * 1.45, -0.012, 0.016, METAL, None, 10 if L == 0 else 6)
    dome(mb, M, r, KIT, K(uvname), 10 if L == 0 else 6, z=0.016, flat=0.8)


def build(variant, L):
    rc = variant == 'RC'
    UREP = 1 if rc else 8

    def huv(th, s):
        return ((th - TH_U0) / TAU * UREP, s / S_TOT)

    sh, dr, sm = MB(), MB(), MB()
    _LODK[0] = 1.0 + L
    NTH = (128, 64, 32)[L]
    NC = (16, 8, 4)[L]
    jets = []

    def sg(n, lo=6):
        return max(lo, n >> L)

    ths = [-math.pi + TAU * i / NTH for i in range(NTH + 1)]
    for edge in (HATCH - TH_DH, HATCH + TH_DH):
        k = min(range(len(ths)), key=lambda i: abs(ths[i] - edge))
        ths[k] = edge

    def in_door(i):
        return ths[i] >= HATCH - TH_DH - 1e-6 and ths[i + 1] <= HATCH + TH_DH + 1e-6

    # ---- 1 ablative base and its toroidal shoulder, 2 shoulder ring
    lathe(sh, [(0.0, -0.55), (1.20, -0.55), (2.10, -0.55), (2.30, -0.505), (2.43, -0.43), (2.50, -0.33)], SHIELD, None, thetas=ths)
    lathe(sh, [(2.50, -0.33), (2.528, -0.305), (2.528, -0.030), (2.50, 0.0)], METAL, None, thetas=ths)
    if L == 0:
        for i in range(48):
            ocyl(sh, rframe(2.528, TAU * (i + 0.5) / 48, -0.17), 0.013, 0.0, 0.007, METAL, None, 6)

    # ---- 3 tiled back shell (cylinder + cone), door opening left out
    ring_s = [0.0, DOOR_Z1, S_CYL] + [S_CYL + CONE_LEN * k / NC for k in range(1, NC + 1)]
    prof = [(hull_r(s), hull_pn(0, s)[0].z) for s in ring_s]
    lathe(sh, prof, HULL, lambda th, j: huv(th, ring_s[j]), thetas=ths, skip=lambda j, i: j == 0 and in_door(i))
    centre = Vector((0.0, 2.43, 1.07))
    for th in (HATCH - TH_DH, HATCH + TH_DH):
        c, s = math.cos(th), math.sin(th)
        sh.poly([Vector((R_CYL * c, R_CYL * s, 0.0)), Vector((R_JAMB * c, R_JAMB * s, 0.0)),
                 Vector((R_JAMB * c, R_JAMB * s, DOOR_Z1)), Vector((R_CYL * c, R_CYL * s, DOOR_Z1))], GRAPH, None, toward=centre)
    dts = [t for t in ths if HATCH - TH_DH - 1e-6 <= t <= HATCH + TH_DH + 1e-6]
    for (z, m) in ((0.0, METAL), (DOOR_Z1, GRAPH)):
        for i in range(len(dts) - 1):
            a, b = dts[i], dts[i + 1]
            sh.poly([Vector((R_CYL * math.cos(a), R_CYL * math.sin(a), z)), Vector((R_CYL * math.cos(b), R_CYL * math.sin(b), z)),
                     Vector((R_JAMB * math.cos(b), R_JAMB * math.sin(b), z)), Vector((R_JAMB * math.cos(a), R_JAMB * math.sin(a), z))],
                    m, None, toward=centre)

    # ---- inner cabin skin. The delivered CapsuleShell also carried the cabin wall, sub-floor and ceiling
    # (r 2.38, z 0..2.52); without them the cabin is see-through wherever CapsuleInterior has no panel.
    R_IN = 2.38
    lin = K('liner')
    lathe(sh, [(R_IN, 2.52), (R_IN, DOOR_Z1), (R_IN, 0.0)], KIT, lin, thetas=ths, skip=lambda j, i: j == 1 and in_door(i))
    lathe(sh, [(0.0, 2.52), (R_IN, 2.52)], KIT, lin, thetas=ths)
    lathe(sh, [(R_IN, 0.0), (0.0, 0.0)], KIT, lin, thetas=ths)
    ipf = cylpf(R_IN)
    ca = 0.07 / R_IN
    for (t0, t1, s0, s1) in ((HATCH - TH_DH - ca, HATCH - TH_DH, 0.0, DOOR_Z1 + 0.07), (HATCH + TH_DH, HATCH + TH_DH + ca, 0.0, DOOR_Z1 + 0.07),
                             (HATCH - TH_DH, HATCH + TH_DH, DOOR_Z1, DOOR_Z1 + 0.07)):
        patch(sh, ipf, t0, t1, s0, s1, 0.0, -0.014, METAL, inward=True)

    # ---- 4 hatch surround: casing, step plate, closeout ring at the cylinder/cone break
    fw = 0.09 / R_CYL
    patch(sh, hpf, HATCH - TH_DH - fw, HATCH - TH_DH, 0.0, DOOR_Z1 + 0.09, 0.0, 0.035, GRAPH)
    patch(sh, hpf, HATCH + TH_DH, HATCH + TH_DH + fw, 0.0, DOOR_Z1 + 0.09, 0.0, 0.035, GRAPH)
    patch(sh, hpf, HATCH - TH_DH, HATCH + TH_DH, DOOR_Z1, DOOR_Z1 + 0.09, 0.0, 0.035, GRAPH)
    patch(sh, hpf, HATCH - TH_DH - fw, HATCH + TH_DH + fw, -0.055, 0.0, 0.0, 0.042, METAL)
    if L == 0:
        for sgn in (-1, 1):
            for z in (0.25, 0.95, 1.65, 2.195):
                ocyl(sh, hframe(HATCH + sgn * (TH_DH + fw * 0.5), z, 0.035), 0.012, 0.0, 0.007, METAL, None, 6)
        text_on(sh, hpf, hull_r, 'CREW HATCH', HATCH, DOOR_Z1 + 0.045, 0.040, K('white'), h=0.038, res=2)
    lathe(sh, [(2.50, 2.560), (2.517, 2.572), (2.517, 2.610), (2.4526, 2.655)], METAL, None, thetas=ths)

    # ---- 5 grab bars beside the hatch, EVA rails up the cone
    if L <= 1:
        ts = 8 if L == 0 else 5
        for sgn in (-1, 1):
            th = HATCH + sgn * D(20.5)
            n = hull_pn(th, 1.0)[1]
            rail(sh, hp(th, 0.70), n, hp(th, 1.90), n, 0.075, 0.017, K('yellow'), ts, lod=L)
        for thd in (56, 124, 236, 304):
            for (s0, s1) in ((2.80, 3.36), (3.52, 4.08), (4.24, 4.78)):
                n = hull_pn(D(thd), 3.0)[1]
                rail(sh, hp(D(thd), s0), n, hp(D(thd), s1), n, 0.065, 0.015, K('yellow'), ts, lod=L)

    # ---- 6 rendezvous windows in a dark surround panel, two side portholes
    patch(sh, hpf, D(62), D(118), 3.02, 3.86, -0.004, 0.005, GRAPH)
    if L <= 1:
        frame_bars(sh, hpf, D(62), D(118), 3.02, 3.86, 0.028, -0.004, 0.010, METAL, None, hull_r(3.44))
    if L == 0:
        for thd in (64, 77, 90, 103, 116):
            for sv in (3.05, 3.83):
                ocyl(sh, hframe(D(thd), sv, 0.005), 0.009, 0.0, 0.004, METAL, None, 6)
        # star tracker apertures
        for thd in (200, 340):
            Mt = hframe(D(thd), 3.62)
            ocyl(sh, Mt, 0.070, -0.015, 0.022, GRAPH, None, 14)
            ocyl(sh, Mt, 0.046, 0.022, 0.025, KIT, K('glass'), 14)
    for sgn in (-1, 1):
        M = hframe(HATCH + sgn * D(15.5), 3.44)
        outer = [(-0.25, -0.23), (-0.20, -0.28), (0.20, -0.28), (0.25, -0.23), (0.215, 0.23), (0.165, 0.28), (-0.165, 0.28), (-0.215, 0.23)]
        inner = [(x * 0.78, y * 0.80) for (x, y) in outer]
        n = len(outer)
        for k in range(n):
            k1 = (k + 1) % n
            oprism(sh, M, [outer[k], outer[k1], inner[k1], inner[k]], -0.02, 0.034, METAL)
        oprism(sh, M, inner, -0.02, 0.010, KIT, K('glass'))
        if L == 0:
            for k in range(n):
                k1 = (k + 1) % n
                for t in (0.25, 0.75):
                    x = (outer[k][0] * (1 - t) + outer[k1][0] * t) * 0.89
                    y = (outer[k][1] * (1 - t) + outer[k1][1] * t) * 0.90
                    ocyl(sh, M @ Matrix.Translation((x, y, 0.034)), 0.009, 0.0, 0.005, GRAPH, None, 6)
    for thd in (0, 180):
        M = hframe(D(thd), 1.55)
        o, ax, e1 = M.translation, M.col[2].xyz, M.col[0].xyz
        lathe(sh, [(0.235, -0.020), (0.235, 0.030), (0.180, 0.030), (0.180, 0.010)], METAL, None, sg(28, 10), O=o, ax=ax, e1=e1)
        lathe(sh, [(0.180, 0.010), (0.0, 0.010)], KIT, K('glass'), sg(28, 10), O=o, ax=ax, e1=e1)
        if L == 0:
            for i in range(12):
                a = TAU * i / 12
                ocyl(sh, M @ Matrix.Translation((0.208 * math.cos(a), 0.208 * math.sin(a), 0.030)), 0.008, 0.0, 0.005, GRAPH, None, 6)

    # ---- 7 reaction control quads
    for thd in (45, 135, 225, 315):
        rcs_pod(sh, hpf, hframe, D(thd), 3.20, hull_r(3.20), L, jets)

    # ---- 8 umbilical plate
    patch(sh, hpf, D(130.5), D(145.5), 0.30, 0.92, 0.0, 0.028, GRAPH)
    patch(sh, hpf, D(131.6), D(144.4), 0.35, 0.87, 0.028, 0.034, METAL)
    M = hframe(D(138), 0.61, 0.034)
    for y in (0.11, -0.12):
        ocyl(sh, M @ Matrix.Translation((-0.17, y, 0)), 0.064, -0.02, 0.050, METAL, None, sg(18))
        ocyl(sh, M @ Matrix.Translation((-0.17, y, 0)), 0.076, -0.02, 0.012, KIT, K('amber'), sg(18))
        ocyl(sh, M @ Matrix.Translation((-0.17, y, 0)), 0.044, 0.050, 0.053, KIT, K('black'), sg(18))
    obox(sh, M, 0.02, 0.25, 0.03, 0.18, -0.012, 0.046, METAL)
    obox(sh, M, 0.035, 0.235, 0.045, 0.165, 0.046, 0.049, KIT, K('black'))
    obox(sh, M, 0.02, 0.15, -0.18, -0.07, -0.012, 0.040, METAL)
    obox(sh, M, 0.032, 0.138, -0.168, -0.082, 0.040, 0.043, KIT, K('black'))
    if L <= 1:
        frame_bars(sh, hpf, D(130.5) - 0.03 / R_CYL, D(145.5) + 0.03 / R_CYL, 0.27, 0.95, 0.022, -0.002, 0.0025 * (1 + L), KIT, K('amber'))
    if L == 0:
        for (x, y) in ((-0.29, -0.27), (0.29, -0.27), (0.29, 0.27), (-0.29, 0.27)):
            ocyl(sh, hframe(D(138) + x / R_CYL, 0.61 + y, 0.028), 0.014, 0.0, 0.008, METAL, None, 6)
        text_on(sh, hpf, hull_r, 'UMBILICAL', D(138), 1.035, 0.050, K('marking'), res=2)

    # cable raceway from the shoulder ring to the cone break
    if L <= 1:
        patch(sh, hpf, D(168) - 0.05 / R_CYL, D(168) + 0.05 / R_CYL, 0.0, 2.558, 0.0, 0.030, METAL)
        for zc in (0.22, 0.70, 1.18, 1.66, 2.14):
            patch(sh, hpf, D(168) - 0.064 / R_CYL, D(168) + 0.064 / R_CYL, zc - 0.022, zc + 0.022, 0.0, 0.037, GRAPH)

    # ---- 9 antennas, lamps, forward-bay lids
    for thd in (0, 180):
        Mh = hframe(D(thd), 4.05)
        F = Mh @ Matrix(((0, 0, 1, 0), (1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
        oprism(sh, F, [(-0.075, -0.01), (0.075, -0.01), (0.020, 0.115), (-0.050, 0.115)], -0.007, 0.007, KIT, K('white'), bottom=True)
        obox(sh, Mh, -0.022, 0.022, -0.09, 0.09, -0.01, 0.010, METAL)
    for thd in (22, 158, 202, 338):
        Mh = hframe(D(thd), 3.92)
        obox(sh, Mh, -0.085, 0.085, -0.085, 0.085, -0.012, 0.012, GRAPH)
        obox(sh, Mh, -0.068, 0.068, -0.068, 0.068, 0.012, 0.017, KIT, K('white'))
    Mh = hframe(D(270), 4.25)
    ocyl(sh, Mh, 0.075, -0.012, 0.012, METAL, None, sg(14))
    dome(sh, Mh, 0.058, KIT, K('white'), sg(14), z=0.012)
    lamp(sh, hframe(D(180), 2.50), 'lamp_red', L=L)
    lamp(sh, hframe(D(0), 2.50), 'lamp_green', L=L)
    lamp(sh, hframe(D(270), 2.50), 'lamp_white', L=L)
    lamp(sh, hframe(D(90), 2.50), 'lamp_amber', 0.026, L=L)
    for thd in (30, 150):
        ocyl(sh, hframe(D(thd), 4.55), 0.105, -0.012, 0.014, METAL, None, sg(18))
        if L == 0:
            for i in range(6):
                a = TAU * i / 6
                ocyl(sh, hframe(D(thd), 4.55) @ Matrix.Translation((0.085 * math.cos(a), 0.085 * math.sin(a), 0.014)), 0.008, 0.0, 0.004, GRAPH, None, 6)
    light_az = (32, 148, 212, 328) if rc else (32, 148)
    for thd in light_az:
        p = hp(D(thd), S_TOT - 0.16)
        Mu = upframe(p, D(thd))
        ocyl(sh, Mu, 0.058, -0.05, 0.095, GRAPH, None, sg(14))
        ocyl(sh, Mu, 0.047, 0.095, 0.099, KIT, K('lamp_white'), sg(14))
    Mh = hframe(D(270), S_TOT - 0.15)
    obox(sh, Mh, -0.07, 0.07, -0.055, 0.055, -0.01, 0.085, GRAPH, taper=(0.85, 0.85))
    ocyl(sh, upframe(hp(D(270), S_TOT - 0.15, 0.03), D(270)), 0.03, 0.0, 0.075, KIT, K('glass'), sg(10))

    # ---- 10 docking system: deck, tunnel, hard-dock ring with hooks, soft-capture ring with three petals
    ds = sg(96, 24)
    lathe(sh, [(R_TOP, Z_TOP), (0.90, 4.455), (0.80, 4.455)], GRAPH, None, ds)
    lathe(sh, [(0.80, 4.455), (0.80, 4.78), (0.95, 4.78), (0.95, 4.84), (0.90, 4.84), (0.90, 4.95), (0.70, 4.95), (0.70, 4.885),
               (0.665, 4.885), (0.665, 4.93), (0.565, 4.93), (0.565, 4.90)], METAL, None, ds)
    lathe(sh, [(0.565, 4.90), (0.565, 4.60)], GRAPH, None, ds)
    lathe(sh, [(0.565, 4.60), (0.30, 4.60), (0.30, 4.618), (0.135, 4.618)], METAL, None, ds)
    lathe(sh, [(0.135, 4.618), (0.0, 4.618)], KIT, K('glass'), ds)
    for (r0, r1) in ((0.845, 0.865), (0.725, 0.745)):
        lathe(sh, [(r1, 4.95), (r1, 4.955), (r0, 4.955), (r0, 4.95)], KIT, K('seal'), ds)
    lathe(sh, [(0.86 + 0.018 * math.cos(D(a)), 4.50 + 0.018 * math.sin(D(a))) for a in (-90, -30, 30, 90, 150, 210, 270)], KIT, K('black'), ds)
    for i in range(12):
        th = TAU * i / 12
        Mr = upframe(Vector((0.795 * math.cos(th), 0.795 * math.sin(th), 4.95)), th)
        obox(sh, Mr, -0.030, 0.030, -0.022, 0.022, 0.0, 0.046, GRAPH)
        if L <= 1:
            obox(sh, Mr, -0.030, 0.030, -0.052, -0.016, 0.030, 0.046, METAL, bottom=True)
    if L <= 1:
        for thd in (15, 195):
            Mr = upframe(Vector((0.795 * math.cos(D(thd)), 0.795 * math.sin(D(thd)), 4.95)), D(thd))
            ocyl(sh, Mr, 0.018, 0.0, 0.060, METAL, None, 8, r1=0.009)
        for i in range(24):
            th = TAU * (i + 0.5) / 24
            ocyl(sh, upframe(Vector((0.925 * math.cos(th), 0.925 * math.sin(th), 4.84)), th), 0.011, 0.0, 0.006, GRAPH, None, 6)
        for i in range(8):
            th = TAU * (i + 0.5) / 8
            ocyl(sh, upframe(Vector((0.44 * math.cos(th), 0.44 * math.sin(th), 4.60)), th), 0.014, 0.0, 0.008, GRAPH, None, 6)
        # hatch handle ring and target cross
        lathe(sh, [(0.215 + 0.011 * math.cos(D(a)), 4.652 + 0.011 * math.sin(D(a))) for a in (-90, 0, 90, 180, 270)], KIT, K('yellow'), sg(32, 12))
        for thd in (45, 135, 225, 315):
            ocyl(sh, upframe(Vector((0.215 * math.cos(D(thd)), 0.215 * math.sin(D(thd)), 4.618)), 0), 0.008, 0.0, 0.034, METAL, None, 6)
    # three guide petals: the only parts above the docking datum, tips exactly at z 5.10
    ta = D(62.0)
    lp = (5.10 - 4.93 - 0.009 * math.cos(ta)) / math.sin(ta)      # thickness included: top corner at exactly 5.10
    for thd in (90, 210, 330):
        th = D(thd)
        c, s = math.cos(th), math.sin(th)
        T, Rv = Vector((-s, c, 0)), Vector((c, s, 0))
        y = (-Rv * math.cos(ta) + Vector((0, 0, 1)) * math.sin(ta))
        F = frame(Vector((0.632 * c, 0.632 * s, 4.93)), T, y, T.cross(y))
        petal = [(-0.215, 0.0), (0.215, 0.0), (0.150, lp - 0.022), (0.118, lp), (-0.118, lp), (-0.150, lp - 0.022)]
        oprism(sh, F, petal, -0.009, 0.009, METAL, None, bottom=True)
        if L <= 1:
            pad = [(x * 0.74, 0.020 + yy * 0.70) for (x, yy) in petal]
            oprism(sh, F, pad, 0.009, 0.013, GRAPH, None)
            oprism(sh, F, pad[::-1], -0.009, -0.013, GRAPH, None)
            Mr = upframe(Vector((0.615 * c, 0.615 * s, 4.93)), th)
            for x0 in (-0.235, 0.185):
                obox(sh, Mr, x0, x0 + 0.05, -0.040, 0.040, 0.0, 0.034, GRAPH, taper=(0.9, 0.8))
            if L == 0:
                for x in (-0.11, 0.0, 0.11):
                    ocyl(sh, F @ Matrix.Translation((x * 0.9, 0.022, 0.013)), 0.008, 0.0, 0.004, METAL, None, 6)
    for thd in (30, 150, 270):
        th = D(thd)
        Mr = upframe(Vector((0.615 * math.cos(th), 0.615 * math.sin(th), 4.93)), th)
        obox(sh, Mr, -0.060, 0.060, -0.034, 0.034, 0.0, 0.046, GRAPH, taper=(0.8, 0.8))
        obox(sh, Mr, -0.030, 0.030, -0.010, 0.050, 0.020, 0.058, METAL, bottom=True)
        lathe(sh, [(0.903, 4.872), (0.903, 4.918)], KIT, K('lamp_cyan'), 1, thetas=[th - D(13) + D(26) * i / 6 for i in range(7)])
    for i in range(6):
        th = TAU * i / 6
        Mr = rframe(0.80, th, 4.62)
        obox(sh, Mr, -0.07, 0.07, -0.11, 0.11, -0.01, 0.075, GRAPH, taper=(0.8, 0.86))
        if L <= 1:
            ocyl(sh, Mr @ Matrix.Translation((0, 0, 0.075)), 0.030, 0.0, 0.012, METAL, None, 8)

    # ---- 11 markings
    mk, wt = K('marking'), K('white')
    tr = (3, 2, 1)[L]
    cpf = hpf
    if rc:
        text_on(sh, cpf, hull_r, 'AURORA', D(270), 1.27, 0.56, mk, 0.005, tr)
        for thd in (270, 150, 30):
            text_on(sh, cpf, hull_r, 'RESCUE', D(thd), 2.22, 0.33, wt, 0.005, tr, 1.06)
        if L <= 1:
            text_on(sh, cpf, hull_r, 'RV-02', D(270), 0.72, 0.17, mk, 0.005, tr)
            for thd in (138, 42):
                text_on(sh, cpf, hull_r, 'AURORA', D(thd), 1.62, 0.30, mk, 0.005, tr)
                text_on(sh, cpf, hull_r, 'RV-02', D(thd), 1.36, 0.10, mk, 0.005, tr)
    else:
        text_on(sh, cpf, hull_r, 'AURORA', D(270), 1.52, 0.46, mk, 0.004, tr)
        for thd in (138, 42):
            text_on(sh, cpf, hull_r, 'AURORA', D(thd), 1.62, 0.30, mk, 0.004, tr)
        if L <= 1:
            text_on(sh, cpf, hull_r, 'CREW TRANSPORT  CT-07', D(270), 1.12, 0.10, mk, 0.004, tr)
            for thd in (138, 42):
                text_on(sh, cpf, hull_r, 'CT-07', D(thd), 1.36, 0.10, mk, 0.004, tr)

    # ---- hatch leaf (own object, origin on the hinge)
    d0, d1 = HATCH - TH_DOOR, HATCH + TH_DOOR
    z0, z1 = 0.006, 2.144
    dth = D(2.9) if L == 0 else D(6.5)
    patch(dr, hpf, d0, d1, z0, z1, -DOOR_T, 0.0, HULL, huv, dth=dth, wall_mat=GRAPH, back_mat=METAL)
    if L == 0:
        frame_bars(dr, hpf, d0 + 0.022 / R_CYL, d1 - 0.022 / R_CYL, z0 + 0.022, z1 - 0.022, 0.020, -0.002, 0.003, KIT, K('seal'))
    # porthole, both faces
    for (M, a_g, a_t) in ((hframe(HATCH, 1.52), 0.006, 0.022), (hframe_in(HATCH, 1.52, DOOR_T), 0.012, 0.026)):
        o, ax, e1 = M.translation, M.col[2].xyz, M.col[0].xyz
        lathe(dr, [(0.215, -0.014), (0.215, a_t), (0.165, a_t), (0.165, a_g)], METAL, None, sg(28, 10), O=o, ax=ax, e1=e1)
        lathe(dr, [(0.165, a_g), (0.0, a_g)], KIT, K('glass'), sg(28, 10), O=o, ax=ax, e1=e1)
        if L == 0:
            for i in range(12):
                a = TAU * i / 12
                ocyl(dr, M @ Matrix.Translation((0.190 * math.cos(a), 0.190 * math.sin(a), a_t)), 0.008, 0.0, 0.005, GRAPH, None, 6)
    # outer emergency release
    thh = HATCH + D(9.5)
    Mo = hframe(thh, 1.05)
    obox(dr, Mo, -0.12, 0.12, -0.075, 0.075, -0.005, 0.007, GRAPH)
    if L == 0:
        hb = [Mo @ Vector((-0.075, 0.0, 0.0)), Mo @ Vector((-0.075, 0.0, 0.038)), Mo @ Vector((0.075, 0.0, 0.038)), Mo @ Vector((0.075, 0.0, 0.0))]
        pts = [hb[0]] + bez(hb[1] - Mo.col[2].xyz * 0.015, hb[1], hb[1] + Mo.col[0].xyz * 0.015, 3) \
            + bez(hb[2] - Mo.col[0].xyz * 0.015, hb[2], hb[2] - Mo.col[2].xyz * 0.015, 3) + [hb[3]]
        tube(dr, pts, 0.011, KIT, K('red'), 8, caps=False)
        text_on(dr, hpf, hull_r, 'PULL', thh, 1.165, 0.036, K('red'), res=2)
        text_on(dr, hpf, hull_r, 'EMERGENCY RELEASE', thh, 0.945, 0.022, K('red'), res=2)
    # hinges: barrels on the pivot axis, leaves on the door skin
    for zc in (0.36, 1.075, 1.79):
        Mk = frame(Vector((HINGE.x, HINGE.y, zc)), Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1)))
        ocyl(dr, Mk, 0.046, -0.085, 0.085, METAL, None, sg(14, 8), cap0=True)
        patch(dr, hpf, d0 + D(0.2), d0 + D(5.0), zc - 0.060, zc + 0.060, 0.0, 0.014, METAL)
    # inner face: perimeter stiffener, ribs, latch lever at the delivered HatchLatch anchor, dogs
    di = -DOOR_T
    ib = 0.07 / R_JAMB
    patch(dr, hpf, d0, d0 + ib, z0, z1, di, di - 0.030, GRAPH, inward=True)
    patch(dr, hpf, d1 - ib, d1, z0, z1, di, di - 0.030, GRAPH, inward=True)
    patch(dr, hpf, d0 + ib, d1 - ib, z0, z0 + 0.07, di, di - 0.030, GRAPH, inward=True)
    patch(dr, hpf, d0 + ib, d1 - ib, z1 - 0.07, z1, di, di - 0.030, GRAPH, inward=True)
    for zc in (0.62, 1.12, 1.94):
        patch(dr, hpf, d0 + ib, d1 - ib, zc - 0.035, zc + 0.035, di, di - 0.022, GRAPH, inward=True)
    th_l = HATCH + math.asin(0.58 / R_JAMB)
    Mi = hframe_in(th_l, 1.05, DOOR_T)
    ocyl(dr, Mi, 0.050, -0.01, 0.045, METAL, None, sg(16, 8))
    Ml = Mi @ Matrix.Rotation(D(-35), 4, 'Z')
    obox(dr, Ml, -0.02, 0.25, -0.020, 0.020, 0.045, 0.068, KIT, K('amber'), bottom=True)
    ocyl(dr, Ml @ Matrix.Translation((0.235, 0, 0.068)), 0.024, 0.0, 0.050, KIT, K('black'), sg(12, 6))
    if L == 0:
        for sgn in (-1, 1):
            for zc in (0.34, 1.07, 1.80):
                Md = hframe_in(HATCH + sgn * (TH_DOOR - D(1.9)), zc, DOOR_T + 0.030)
                obox(dr, Md, -0.035, 0.035, -0.055, 0.055, -0.005, 0.022, METAL, taper=(0.8, 0.85))
        Mv = hframe_in(HATCH - D(8.5), 1.05, DOOR_T)
        ocyl(dr, Mv, 0.034, -0.01, 0.030, METAL, None, 12)
        ocyl(dr, Mv, 0.022, 0.030, 0.046, KIT, K('red'), 12)
        Mp = hframe_in(HATCH + D(6.5), 1.33, DOOR_T)
        obox(dr, Mp, -0.09, 0.09, -0.045, 0.045, -0.006, 0.004, KIT, K('white'))
        obox(dr, Mp, -0.09, 0.09, 0.022, 0.045, 0.004, 0.0055, KIT, K('amber'))

    # ---- rescue craft only: service and propulsion module (separate, optional object)
    if rc:
        RS = 2.18
        spf = cylpf(RS)
        ss = sg(96, 24)
        lathe(sm, [(0.0, -1.99), (0.70, -1.99), (2.02, -1.80), (RS, -1.72), (RS, -0.585), (2.30, -0.505)], METAL, None, ss)
        for i in range(8):
            th0 = TAU * i / 8 + D(4.0)
            th1 = TAU * (i + 1) / 8 - D(4.0)
            if i % 2 == 0:
                patch(sm, spf, th0, th1, -1.62, -0.72, 0.0, 0.016, KIT, K('radiator'), dth=D(6) if L else D(3))
                if L == 0:
                    for k in range(1, 6):
                        zz = -1.62 + 0.15 * k
                        patch(sm, spf, th0 + D(1), th1 - D(1), zz - 0.004, zz + 0.004, 0.016, 0.019, GRAPH, dth=D(6))
            else:
                patch(sm, spf, th0, th1, -1.62, -0.72, 0.0, 0.010, KIT, K('gold'), dth=D(6) if L else D(3))
                if L <= 1:
                    for zz in (-1.32, -1.02):
                        patch(sm, spf, th0, th1, zz - 0.012, zz + 0.012, 0.010, 0.016, KIT, K('silver'), dth=D(6))
        for zz in (-1.69, -0.65):
            lathe(sm, [(RS, zz - 0.035), (RS + 0.022, zz - 0.025), (RS + 0.022, zz + 0.025), (RS, zz + 0.035)], GRAPH, None, ss)
        sjets = []
        for thd in (0, 90, 180, 270):
            rcs_pod(sm, spf, lambda t, z, h=0.0: rframe(RS + h, t, z), D(thd), -1.17, RS, L, sjets)
        lathe(sm, [(0.215, -2.075), (0.46, -2.075), (0.72, -1.995)], GRAPH, None, ss)
        if L <= 1:
            for i in range(8):
                th = TAU * (i + 0.5) / 8
                c, s = math.cos(th), math.sin(th)
                Fr = frame(Vector((0, 0, 0)), Vector((c, s, 0)), Vector((0, 0, 1)), Vector((s, -c, 0)))
                oprism(sm, Fr, [(0.74, -2.045), (2.00, -1.855), (2.00, -1.795), (0.74, -1.985)], -0.016, 0.016, GRAPH, None, bottom=True)
        for thd in (0, 90, 180, 270):
            c, s = math.cos(D(thd)), math.sin(D(thd))
            tk = [(0.33 * math.sin(D(a)), -0.33 * math.cos(D(a))) for a in (0, 20, 40, 60, 80, 100, 118)]
            lathe(sm, tk, KIT, K('gold'), sg(20, 8), O=Vector((1.22 * c, 1.22 * s, -1.80)))
            if L <= 1:
                lathe(sm, [(0.335 * math.sin(D(a)), -0.335 * math.cos(D(a))) for a in (84, 90, 96)], KIT, K('silver'), sg(20, 8),
                      O=Vector((1.22 * c, 1.22 * s, -1.80)))
        # main engine and four vernier bells
        es = sg(40, 12)
        lathe(sm, [(0.0, -2.03), (0.19, -2.03), (0.25, -2.14), (0.46, -2.52), (0.60, -2.86), (0.625, -2.86)], KIT, K('nozzle_hot'), es)
        lathe(sm, [(0.625, -2.86), (0.485, -2.52), (0.275, -2.14), (0.215, -1.99)], KIT, K('nozzle'), es)
        if L <= 1:
            for zz in (-2.30, -2.62):
                rr = 0.275 + (0.625 - 0.275) * (-2.14 - zz) / 0.72
                lathe(sm, [(rr, zz - 0.012), (rr + 0.014, zz - 0.006), (rr + 0.014, zz + 0.006), (rr - 0.006, zz + 0.012)], METAL, None, es)
        for thd in (45, 135, 225, 315):
            c, s = math.cos(D(thd)), math.sin(D(thd))
            nozzle(sm, Vector((1.42 * c, 1.42 * s, -1.87)), Vector((0, 0, -1)), 2.6, sg(16, 8))
            if L <= 1:
                ocyl(sm, frame(Vector((1.42 * c, 1.42 * s, -1.93)), Vector((1, 0, 0)), Vector((0, -1, 0)), Vector((0, 0, -1))), 0.13, -0.08, 0.02, GRAPH, None, sg(14, 8))
        # high-gain dish on a short boom
        thd = D(205)
        c, s = math.cos(thd), math.sin(thd)
        root = Vector((RS * c, RS * s, -1.05))
        axd = (Vector((c, s, 0)) * 0.72 + Vector((0, 0, -0.70))).normalized()
        hub = root + Vector((c, s, 0)) * 0.55 + Vector((0, 0, -0.12))
        tube(sm, [root - Vector((c, s, 0)) * 0.02, root + Vector((c, s, 0)) * 0.30, hub], 0.028, METAL, None, sg(10, 6))
        dsn = sg(36, 12)
        front = [(0.50 * t, 0.16 * t * t) for t in (1.0, 0.8, 0.6, 0.4, 0.2, 0.0)]
        back = [(0.50 * t, 0.16 * t * t - 0.012) for t in (0.0, 0.2, 0.4, 0.6, 0.8, 1.0)]
        lathe(sm, front, KIT, K('white'), dsn, O=hub, ax=axd)
        lathe(sm, back + [(0.50, 0.16)], METAL, None, dsn, O=hub, ax=axd)
        e1 = axd.orthogonal().normalized()
        e2 = axd.cross(e1)
        feed = hub + axd * 0.36
        ocyl(sm, frame(feed, e1, e2, axd), 0.035, -0.03, 0.03, GRAPH, None, 8, cap0=True)
        if L <= 1:
            for k in range(3):
                a = TAU * k / 3
                tube(sm, [hub + (e1 * math.cos(a) + e2 * math.sin(a)) * 0.40 + axd * 0.10, feed], 0.007, METAL, None, 5, caps=False)
        if L <= 1:
            text_on(sm, spf, lambda s_: RS, 'AURORA RESCUE', D(270), -0.795, 0.11, K('marking'), 0.018, 2)
    return sh, dr, sm, jets


# ----------------------------------------------------------------------------- Blender objects
def finalize(mb, name, mats, origin=Vector((0, 0, 0))):
    me = bpy.data.meshes.new(name)
    me.from_pydata([(x - origin.x, y - origin.y, z - origin.z) for (x, y, z) in mb.v], [], mb.f)
    for m in mats:
        me.materials.append(m)
    me.polygons.foreach_set('material_index', mb.m)
    me.update()
    uvl = me.uv_layers.new(name='UVMap').data
    vs = me.vertices
    for p, spec in zip(me.polygons, mb.uv):
        li = p.loop_indices
        if spec is None:
            n = p.normal
            ax = max(range(3), key=lambda k: abs(n[k]))
            planar = p.material_index == SHIELD
            for l, vi in zip(li, p.vertices):
                co = vs[vi].co + origin
                if planar:
                    uvl[l].uv = (co.x / (2 * R_SHIELD) + 0.5, co.y / (2 * R_SHIELD) + 0.5)
                elif ax == 0:
                    uvl[l].uv = (co.y, co.z)
                elif ax == 1:
                    uvl[l].uv = (co.x, co.z)
                else:
                    uvl[l].uv = (co.x, co.y)
        elif isinstance(spec, tuple):
            for l in li:
                uvl[l].uv = spec
        else:
            for l, q in zip(li, spec):
                uvl[l].uv = q
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0003)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True
    me.use_auto_smooth = True
    me.auto_smooth_angle = D(38)
    me.update()
    ob = bpy.data.objects.new(name, me)
    ob.location = origin
    bpy.context.scene.collection.objects.link(ob)
    me.calc_loop_triangles()
    return ob, len(me.loop_triangles)


def bl_material(name, base, normal, mask_img, emission=None):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes['Principled BSDF']
    tb = nt.nodes.new('ShaderNodeTexImage')
    tb.image = base
    nt.links.new(tb.outputs['Color'], bsdf.inputs['Base Color'])
    tm = nt.nodes.new('ShaderNodeTexImage')
    tm.image = mask_img
    sep = nt.nodes.new('ShaderNodeSeparateColor')
    nt.links.new(tm.outputs['Color'], sep.inputs['Color'])
    nt.links.new(sep.outputs['Red'], bsdf.inputs['Metallic'])
    inv = nt.nodes.new('ShaderNodeMath')
    inv.operation = 'SUBTRACT'
    inv.inputs[0].default_value = 1.0
    nt.links.new(tm.outputs['Alpha'], inv.inputs[1])
    nt.links.new(inv.outputs[0], bsdf.inputs['Roughness'])
    if normal is not None:
        tn = nt.nodes.new('ShaderNodeTexImage')
        tn.image = normal
        nm = nt.nodes.new('ShaderNodeNormalMap')
        nt.links.new(tn.outputs['Color'], nm.inputs['Color'])
        nt.links.new(nm.outputs['Normal'], bsdf.inputs['Normal'])
    if emission is not None:
        te = nt.nodes.new('ShaderNodeTexImage')
        te.image = emission
        nt.links.new(te.outputs['Color'], bsdf.inputs['Emission'])
        bsdf.inputs['Emission Strength'].default_value = 6.0
    return m


def write_set(tex_dir, name, maps, images):
    b, n, m = maps
    images[name] = (ac_tex.save(os.path.join(tex_dir, name + '_BaseColor.png'), b, True),
                    ac_tex.save(os.path.join(tex_dir, name + '_Normal.png'), n, False) if n is not None else None,
                    ac_tex.save(os.path.join(tex_dir, name + '_Mask.png'), m, False))


def load_set(tex_dir, name, images):
    def ld(suffix, srgb):
        p = os.path.join(tex_dir, name + suffix)
        if not os.path.exists(p):
            return None
        img = bpy.data.images.load(p, check_existing=True)
        img.colorspace_settings.name = 'sRGB' if srgb else 'Non-Color'
        img.alpha_mode = 'CHANNEL_PACKED'
        return img
    images[name] = (ld('_BaseColor.png', True), ld('_Normal.png', False), ld('_Mask.png', False))


def look_at(cam, pos, tgt):
    cam.location = pos
    cam.rotation_euler = (Vector(tgt) - Vector(pos)).to_track_quat('-Z', 'Y').to_euler()


def render_views(out_dir, prefix, views, space, res=(1600, 1000)):
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'
    sc.eevee.taa_render_samples = 48
    sc.eevee.use_gtao = True
    sc.eevee.gtao_distance = 0.6
    sc.eevee.use_ssr = True
    sc.eevee.use_bloom = True
    sc.eevee.bloom_intensity = 0.04
    sc.eevee.shadow_cascade_size = '4096'
    sc.eevee.use_soft_shadows = True
    sc.view_settings.view_transform = 'Filmic'
    sc.view_settings.look = 'Medium High Contrast'
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.film_transparent = False
    world = bpy.data.worlds.get('ACW') or bpy.data.worlds.new('ACW')
    sc.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes['Background']
    bg.inputs[0].default_value = (0.012, 0.014, 0.020, 1) if space else (0.42, 0.52, 0.66, 1)
    bg.inputs[1].default_value = 1.0 if space else 1.0
    for n in ('ACSun', 'ACFill', 'ACCam'):
        o = bpy.data.objects.get(n)
        if o:
            bpy.data.objects.remove(o)
    sun = bpy.data.objects.new('ACSun', bpy.data.lights.new('ACSun', 'SUN'))
    sun.data.energy = 6.0 if space else 4.2
    sun.data.angle = D(1.0)
    sun.rotation_euler = (D(58), 0, D(200)) if space else (D(52), 0, D(150))
    sc.collection.objects.link(sun)
    fill = bpy.data.objects.new('ACFill', bpy.data.lights.new('ACFill', 'SUN'))
    fill.data.energy = 1.1 if space else 0.8
    fill.data.color = (0.55, 0.70, 1.0)
    fill.rotation_euler = (D(118), 0, D(35)) if space else (D(110), 0, D(20))
    sc.collection.objects.link(fill)
    cam = bpy.data.objects.new('ACCam', bpy.data.cameras.new('ACCam'))
    sc.collection.objects.link(cam)
    sc.camera = cam
    cam.data.clip_start, cam.data.clip_end = 0.05, 500
    paths = []
    for (name, pos, tgt, lens) in views:
        cam.data.lens = lens
        look_at(cam, pos, tgt)
        sc.render.filepath = os.path.join(out_dir, '%s_%s.png' % (prefix, name))
        bpy.ops.render.render(write_still=True)
        paths.append(sc.render.filepath)
    return paths


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []

    def arg(name, default=None):
        return argv[argv.index(name) + 1] if name in argv else default

    out = os.path.abspath(arg('--out', os.path.join(HERE, '..')))
    which = arg('--variant', 'all')
    ts = float(arg('--texscale', '1.0'))
    notex, norender = '--notex' in argv, '--norender' in argv
    tex_dir, mdl_dir, prev_dir = os.path.join(out, 'Textures'), os.path.join(out, 'Models'), os.path.join(out, 'Previews~')
    for d in (tex_dir, mdl_dir, prev_dir):
        os.makedirs(d, exist_ok=True)
    report = {}
    rp = os.path.join(mdl_dir, 'AuroraCapsulesV02_report.json')
    if os.path.exists(rp):
        report = json.load(open(rp))
    for variant in (('RC', 'CT') if which == 'all' else (which,)):
        t0 = time.time()
        bpy.ops.wm.read_factory_settings(use_empty=True)
        _FONT.clear()
        rc = variant == 'RC'
        fbx_name = 'AuroraRescueCraft' if rc else 'AuroraCrewTransport'
        # geometry first: the rescue soot is painted around the real jet positions
        built = [build(variant, L) for L in range(3)]
        jets = built[0][3]
        images = {}
        if notex:
            for n in (variant + '_Hull', variant + '_HeatShield', 'CAP_Metal', 'CAP_Graphite'):
                load_set(tex_dir, n, images)
        else:
            if rc:
                W, H = int(4096 * ts), int(2048 * ts)
                write_set(tex_dir, 'RC_Hull', ac_tex.paint_hull(G, W, H, TAU, 'RC', jets), images)
            else:
                W, H = int(2048 * ts), int(4096 * ts)
                write_set(tex_dir, 'CT_Hull', ac_tex.paint_hull(G, W, H, TAU / 8, 'CT'), images)
            write_set(tex_dir, variant + '_HeatShield', ac_tex.paint_heatshield(int((2048 if rc else 1024) * ts), variant, R_SHIELD), images)
            write_set(tex_dir, 'CAP_Metal', ac_tex.paint_metal(int(1024 * ts)), images)
            write_set(tex_dir, 'CAP_Graphite', ac_tex.paint_graphite(int(1024 * ts)), images)
        kb, km, ke = ac_tex.paint_kit(256)
        write_set(tex_dir, 'CAP_Kit', (kb, None, km), images)
        ac_tex.save(os.path.join(tex_dir, 'CAP_Kit_Emission.png'), ke, True)
        kit_emi = bpy.data.images.load(os.path.join(tex_dir, 'CAP_Kit_Emission.png'), check_existing=True)
        mats = [bl_material(variant + '_Hull', *images[variant + '_Hull']),
                bl_material(variant + '_HeatShield', *images[variant + '_HeatShield']),
                bl_material('CAP_Metal', *images['CAP_Metal']),
                bl_material('CAP_Graphite', *images['CAP_Graphite']),
                bl_material('CAP_Kit', images['CAP_Kit'][0], None, images['CAP_Kit'][2], kit_emi)]
        for m in mats:
            for n in m.node_tree.nodes:
                if n.type == 'TEX_IMAGE' and n.image and n.image.name.startswith('CAP_Kit'):
                    n.interpolation = 'Closest'
        objs, tris = [], {}
        for L, (sh, dr, sm, _) in enumerate(built):
            o, n = finalize(sh, 'CapsuleShell_LOD%d' % L, mats)
            objs.append(o)
            tris[o.name] = n
            if L <= 1:
                o, n = finalize(dr, 'Hatch_LOD%d' % L, mats, HINGE)
                objs.append(o)
                tris[o.name] = n
            if rc:
                o, n = finalize(sm, 'ServiceModule_LOD%d' % L, mats)
                objs.append(o)
                tris[o.name] = n
        bounds = {}
        for o in objs:
            ws = [o.matrix_world @ v.co for v in o.data.vertices]
            bounds[o.name] = [[round(min(v[k] for v in ws), 4) for k in range(3)], [round(max(v[k] for v in ws), 4) for k in range(3)]]
        report[fbx_name] = {'triangles': tris, 'bounds_blender_m': bounds,
                            'lod_totals': {'LOD%d' % L: sum(n for k, n in tris.items() if k.endswith('LOD%d' % L)) for L in range(3)}}
        bpy.ops.object.select_all(action='DESELECT')
        for o in objs:
            o.select_set(True)
        bpy.context.view_layer.objects.active = objs[0]
        bpy.ops.export_scene.fbx(
            filepath=os.path.join(mdl_dir, fbx_name + '.fbx'), use_selection=True, object_types={'MESH'},
            use_mesh_modifiers=True, mesh_smooth_type='OFF', apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
            bake_space_transform=False, axis_forward='-Z', axis_up='Y', add_leaf_bones=False, bake_anim=False,
            path_mode='STRIP', embed_textures=False, use_tspace=False)
        print('[AC2] %s exported: %s  (%.1fs)' % (fbx_name, json.dumps(report[fbx_name]['lod_totals']), time.time() - t0))
        if not norender:
            for o in objs:
                o.hide_render = not o.name.endswith('LOD0')
            if rc:
                views = [('hero', (-7.5, -9.5, 4.2), (0, 0, 1.4), 50), ('nose', (-3.6, -5.2, 10.5), (0, 0, 3.6), 50),
                         ('hatch', (4.2, 8.6, 2.6), (0, 0.6, 1.6), 50), ('aft', (-6.5, -6.0, -5.5), (0, 0, -0.6), 50),
                         ('dock_close', (-1.5, -2.0, 6.6), (0, 0, 4.75), 45), ('pod_close', (-3.4, -3.4, 4.2), (-1.55, -1.55, 3.0), 55)]
            else:
                views = [('hero', (5.5, 9.0, 3.4), (0, 0, 2.2), 50), ('back', (-7.0, -8.0, 4.6), (0, 0, 2.2), 50),
                         ('hatch_close', (0.9, 5.4, 1.55), (0.0, 2.4, 1.25), 35), ('door_detail', (-1.5, 4.1, 1.5), (-0.25, 2.5, 1.2), 50),
                         ('dock_close', (1.5, 2.2, 6.5), (0, 0, 4.75), 45), ('window_close', (1.6, 4.6, 4.2), (0.2, 1.9, 3.3), 50),
                         ('umbilical', (-3.6, 3.9, 1.0), (-1.75, 1.9, 0.75), 50),
                         ('door_inside', (0.25, 0.35, 1.35), (0.0, 2.36, 1.15), 24)]
            render_views(prev_dir, fbx_name, views, rc)
        json.dump(report, open(rp, 'w'), indent=1)
    print('[AC2] DONE')


if __name__ == '__main__':
    main()
