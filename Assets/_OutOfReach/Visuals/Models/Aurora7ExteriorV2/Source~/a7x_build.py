"""AURORA-7 exterior V2: reproducible Blender 3.6 build of the fictional long-duration crew vehicle.

Run (textures first, see a7x_textures.py):
  blender --background --python a7x_build.py -- [--render] [--no-export]

Writes ../Meshes/A7X_Exterior.fbx (three LODs), ../Meshes/A7X_DockAdapter.fbx, a7x_materials.json,
a7x_report.json, A7X_Exterior.blend and, with --render, ../Evidence~/blender_*.png.

Axes. Blender (x, y, z) is Unity (x, z, y): x to the side, Blender y = Unity z = aft, Blender z = Unity y = up.
That is the mapping the delivered AURORA-7 pipeline measured for this FBX export plus Unity's Bake Axis
Conversion. Origin, scale and the docking datum are those of the delivered ship: deck datum at up = 0, bow at
aft = -5.1, docking adapter base at (0, up 1.28, aft 21.16) and contact plane at aft = 25.0.
The hull is coaxial with the docking adapter (axis at up = 1.28).
"""
import json
import math
import os
import random
import sys

import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, ".."))
TEX = os.path.join(ROOT, "Textures")
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
PI = math.pi
TAU = 2.0 * PI
AX = 1.28                     # hull and docking axis height
AXC = AX                      # axis height used by hp(); 0 while the adapter is built in its own frame
LOD = 0
MARK = json.load(open(os.path.join(HERE, "markings.json")))
ADAPTER_ORIGIN = (0.0, 21.16, 1.28)
YAFT = Vector((0.0, 1.0, 0.0))


def S(n, lo=6):
    return max(lo, int(round(n * (1.0, 0.5, 0.25)[LOD])))


def rad(d):
    return math.radians(d)


# ----------------------------------------------------------------------------- geometry kit
def g_box(sx, sy, sz):
    x, y, z = sx / 2, sy / 2, sz / 2
    V = [(-x, -y, -z), (x, -y, -z), (x, y, -z), (-x, y, -z), (-x, -y, z), (x, -y, z), (x, y, z), (-x, y, z)]
    F = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
    ax = [(0, 1), (0, 1), (0, 2), (1, 2), (0, 2), (1, 2)]
    U = [[(V[i][a], V[i][b]) for i in f] for f, (a, b) in zip(F, ax)]
    return V, F, U


def g_lathe(profile, segs, a0=0.0, a1=TAU, uv="m", urep=1.0, vflip=False):
    """Revolve (r, y) points about local y; theta 0 is local +z, theta 90 deg is local +x.
    A profile that runs towards +y has its outside on the +r side."""
    full = abs((a1 - a0) - TAU) < 1e-6
    V, rings, cum = [], [], [0.0]
    for i in range(1, len(profile)):
        cum.append(cum[-1] + math.hypot(profile[i][0] - profile[i - 1][0], profile[i][1] - profile[i - 1][1]))
    rmax = max(p[0] for p in profile)
    utiles = max(1.0, round(TAU * rmax)) if full else (a1 - a0) * rmax
    for (r, y) in profile:
        if r < 1e-7:
            rings.append([len(V)] * (segs + 1))
            V.append((0.0, y, 0.0))
            continue
        idx = []
        for j in range(segs + (0 if full else 1)):
            a = a0 + (a1 - a0) * j / segs
            idx.append(len(V))
            V.append((r * math.sin(a), y, r * math.cos(a)))
        if full:
            idx.append(idx[0])
        rings.append(idx)

    def uvof(i, j):
        if uv == "m":
            return (j / segs * utiles, cum[i])
        if uv == "n":
            t = cum[i] / max(cum[-1], 1e-9)
            return (j / segs * urep, 1.0 - t if vflip else t)
        r = profile[i][0]
        a = a0 + (a1 - a0) * j / segs
        return (r * math.sin(a), r * math.cos(a))

    F, U = [], []
    for i in range(len(profile) - 1):
        z0 = profile[i][0] < 1e-7
        z1 = profile[i + 1][0] < 1e-7
        if z0 and z1:
            continue
        for j in range(segs):
            A, B, C, D = rings[i][j], rings[i][j + 1], rings[i + 1][j + 1], rings[i + 1][j]
            ua, ub, uc, ud = uvof(i, j), uvof(i, j + 1), uvof(i + 1, j + 1), uvof(i + 1, j)
            if z0:
                F.append((A, C, D)); U.append([ua, uc, ud])
            elif z1:
                F.append((A, B, C)); U.append([ua, ub, uc])
            else:
                F.append((A, B, C, D)); U.append([ua, ub, uc, ud])
    return V, F, U


def frame(o, xd, yd):
    x = Vector(xd).normalized()
    y = Vector(yd)
    y = (y - x * y.dot(x)).normalized()
    z = x.cross(y)
    return Matrix(((x.x, y.x, z.x, o[0]), (x.y, y.y, z.y, o[1]), (x.z, y.z, z.z, o[2]), (0, 0, 0, 1)))


def align_y(o, d):
    y = Vector(d).normalized()
    h = Vector((1, 0, 0)) if abs(y.x) < 0.9 else Vector((0, 0, 1))
    z = h.cross(y).normalized()
    x = y.cross(z)
    return Matrix(((x.x, y.x, z.x, o[0]), (x.y, y.y, z.y, o[1]), (x.z, y.z, z.z, o[2]), (0, 0, 0, 1)))


def T(x, y, z):
    return Matrix.Translation((x, y, z))


def hp(r, th, y):
    return Vector((r * math.sin(th), y, AXC + r * math.cos(th)))


def radial(th):
    return Vector((math.sin(th), 0.0, math.cos(th)))


def tangent(th):
    return Vector((math.cos(th), 0.0, -math.sin(th)))


def hull_frame(r, th, y):
    """Local x = tangent (theta increasing), local y = radial out, local z = towards the bow."""
    return frame(hp(r, th, y), tangent(th), radial(th))


def axis_m():
    return T(0, 0, AXC)


def mrect(key, inset=1.5 / 2048.0):
    u0, v0, u1, v1, asp = MARK[key]
    return (u0 + inset, v0 + inset, u1 - inset, v1 - inset), asp


class Part:
    def __init__(self, name):
        self.name = name
        self.v, self.f, self.uv, self.mat, self.sm = [], [], [], [], []

    def add(self, verts, faces, uvs, mat, M=None, smooth=True):
        base = len(self.v)
        flip = False
        if M is not None:
            verts = [M @ Vector(p) for p in verts]
            flip = M.to_3x3().determinant() < 0
        else:
            verts = [Vector(p) for p in verts]
        self.v.extend(verts)
        for f, uv in zip(faces, uvs):
            if flip:
                f = tuple(reversed(f))
                uv = list(reversed(uv))
            self.f.append(tuple(base + i for i in f))
            self.uv.append(list(uv))
            self.mat.append(mat)
            self.sm.append(smooth)

    def quad_out(self, pts, uvs, outward, mat, smooth=False):
        pts = [Vector(p) for p in pts]
        n = (pts[1] - pts[0]).cross(pts[2] - pts[0])
        uvs = list(uvs)
        if n.dot(Vector(outward)) < 0:
            pts = pts[::-1]
            uvs = uvs[::-1]
        self.add(pts, [tuple(range(len(pts)))], [uvs], mat, None, smooth)

    def tris(self):
        return sum(len(f) - 2 for f in self.f)

    # ---- primitives
    def box(self, M, sx, sy, sz, mat, smooth=False):
        self.add(*g_box(sx, sy, sz), mat, M, smooth)

    def lathe(self, M, profile, segs, mat, smooth=True, **kw):
        self.add(*g_lathe(profile, segs, **kw), mat, M, smooth)

    def cyl(self, p0, p1, r, segs, mat, caps=True, smooth=True, r1=None):
        p0, p1 = Vector(p0), Vector(p1)
        L = (p1 - p0).length
        r1 = r if r1 is None else r1
        prof = [(0, 0), (r, 0), (r1, L), (0, L)] if caps else [(r, 0), (r1, L)]
        self.lathe(align_y(p0, p1 - p0), prof, segs, mat, smooth)

    def ring(self, M, r0, r1, y0, y1, segs, mat):
        """Rectangular-section ring about local y (front, outer and rear faces)."""
        self.lathe(M, [(r0, y0), (r1, y0), (r1, y1), (r0, y1)], segs, mat)

    def panel(self, r, t, th0, th1, y0, y1, nseg, mat, rect=None, sides=True, smooth=True):
        """Curved panel on the hull axis: outer skin plus four edge walls."""
        ths = [th0 + (th1 - th0) * j / nseg for j in range(nseg + 1)]

        def uvo(th, y):
            if rect:
                return (rect[0] + (y - y0) / (y1 - y0) * (rect[2] - rect[0]),
                        rect[1] + (th1 - th) / (th1 - th0) * (rect[3] - rect[1]))
            return (th * r, y)

        for j in range(nseg):
            a, b = ths[j], ths[j + 1]
            self.add([hp(r, a, y0), hp(r, b, y0), hp(r, b, y1), hp(r, a, y1)], [(0, 1, 2, 3)],
                     [[uvo(a, y0), uvo(b, y0), uvo(b, y1), uvo(a, y1)]], mat, None, smooth)
        if not sides:
            return
        e = (rect[0] + 0.004, rect[1] + 0.004) if rect else None
        ri = r - t
        for j in range(nseg):
            a, b = ths[j], ths[j + 1]
            for y, out in ((y0, (0, -1, 0)), (y1, (0, 1, 0))):
                uv = [e] * 4 if rect else [(a * r, 0), (b * r, 0), (b * r, t), (a * r, t)]
                self.quad_out([hp(r, a, y), hp(r, b, y), hp(ri, b, y), hp(ri, a, y)], uv, out, mat)
        for th, sgn in ((th0, -1.0), (th1, 1.0)):
            uv = [e] * 4 if rect else [(y0, 0), (y1, 0), (y1, t), (y0, t)]
            self.quad_out([hp(r, th, y0), hp(r, th, y1), hp(ri, th, y1), hp(ri, th, y0)], uv, tangent(th) * sgn, mat)

    def frame_ring(self, outer, inner, n, h, g, mat_frame, mat_glass):
        """Raised window frame around a glazed opening (both given as four coplanar points)."""
        n = Vector(n).normalized()
        o0 = [Vector(p) for p in outer]
        i0 = [Vector(p) for p in inner]
        o1 = [p + n * h for p in o0]
        i1 = [p + n * h for p in i0]
        ig = [p + n * g for p in i0]
        c = sum(o0, Vector()) / 4.0
        for k in range(4):
            k2 = (k + 1) % 4
            outv = (o0[k] + o0[k2]) / 2.0 - c
            L = (o0[k2] - o0[k]).length
            uv = [(0, 0), (L, 0), (L, 0.1), (0, 0.1)]
            self.quad_out([o1[k], o1[k2], i1[k2], i1[k]], uv, n, mat_frame)
            self.quad_out([o0[k], o0[k2], o1[k2], o1[k]], uv, outv, mat_frame)
            self.quad_out([ig[k], ig[k2], i1[k2], i1[k]], uv, -outv, mat_frame)
        self.quad_out(ig, [(0, 0), (1, 0), (1, 1), (0, 1)], n, mat_glass)

    # ---- markings
    def decal_cyl(self, r, thc, yc, h, key, rdir, up, w=None, nseg=4):
        """Marking wrapped on the hull, reading along the axis. rdir: +1 reads towards aft. up: sign of theta."""
        (u0, v0, u1, v1), asp = mrect(key)
        w = h * asp if w is None else w
        ya, yb = yc - rdir * w / 2, yc + rdir * w / 2
        for i in range(nseg):
            a0 = thc + up * (-h / 2 + h * i / nseg) / r
            a1 = thc + up * (-h / 2 + h * (i + 1) / nseg) / r
            va = v0 + (v1 - v0) * i / nseg
            vb = v0 + (v1 - v0) * (i + 1) / nseg
            self.quad_out([hp(r, a0, ya), hp(r, a0, yb), hp(r, a1, yb), hp(r, a1, ya)],
                          [(u0, va), (u1, va), (u1, vb), (u0, vb)], radial(thc), "A7X_Markings", True)

    def decal_ring(self, r, yc, h, key, nrep, nsub=8, th_off=0.0):
        (u0, v0, u1, v1), asp = mrect(key)
        for k in range(nrep):
            for i in range(nsub):
                a0 = th_off + TAU * (k + i / nsub) / nrep
                a1 = th_off + TAU * (k + (i + 1) / nsub) / nrep
                ua = u0 + (u1 - u0) * i / nsub
                ub = u0 + (u1 - u0) * (i + 1) / nsub
                self.quad_out([hp(r, a0, yc - h / 2), hp(r, a1, yc - h / 2), hp(r, a1, yc + h / 2), hp(r, a0, yc + h / 2)],
                              [(ua, v0), (ub, v0), (ub, v1), (ua, v1)], radial((a0 + a1) / 2), "A7X_Markings", True)

    def decal_flat(self, M, h, key, w=None):
        (u0, v0, u1, v1), asp = mrect(key)
        w = h * asp if w is None else w
        self.add([(-w / 2, -h / 2, 0), (w / 2, -h / 2, 0), (w / 2, h / 2, 0), (-w / 2, h / 2, 0)], [(0, 1, 2, 3)],
                 [[(u0, v0), (u1, v0), (u1, v1), (u0, v1)]], "A7X_Markings", M, False)


# ----------------------------------------------------------------------------- reusable assemblies
def handrail(H, r, th, y0, y1):
    if LOD >= 2:
        return
    rr = r + 0.09
    H.cyl(hp(rr, th, y0 + 0.02), hp(rr, th, y1 - 0.02), 0.018, S(8, 5), "A7X_Amber")
    for y in (y0 + 0.07, y1 - 0.07):
        H.box(frame(hp(r + 0.04, th, y), tangent(th), radial(th)), 0.034, 0.10, 0.05, "A7X_Amber")


def handrail_arc(H, r, th0, th1, y):
    """Circumferential handrail."""
    if LOD >= 2:
        return
    rr = r + 0.09
    n = 4
    for i in range(n):
        a = th0 + (th1 - th0) * i / n
        b = th0 + (th1 - th0) * (i + 1) / n
        H.cyl(hp(rr, a, y), hp(rr, b, y), 0.018, S(8, 5), "A7X_Amber", caps=(i in (0, n - 1)))
    for th in (th0 + 0.02, th1 - 0.02):
        H.box(frame(hp(r + 0.04, th, y), tangent(th), radial(th)), 0.05, 0.10, 0.034, "A7X_Amber")


def foot_restraint(H, r, th, y, yaw=0.0):
    """Articulating foot restraint on a socket: post, knuckle, plate, toe bars and heel lip."""
    if LOD >= 1:
        return
    M = hull_frame(r, th, y) @ Matrix.Rotation(yaw, 4, "Y")
    H.lathe(M, [(0, 0), (0.085, 0), (0.085, 0.035), (0.045, 0.05), (0, 0.05)], 12, "A7X_DarkMetal")
    H.cyl(M @ Vector((0, 0.05, 0)), M @ Vector((0, 0.30, 0)), 0.026, 8, "A7X_Aluminium")
    H.box(M @ T(0, 0.32, 0), 0.08, 0.08, 0.08, "A7X_DarkMetal")
    P = M @ T(0, 0.37, 0) @ Matrix.Rotation(rad(12), 4, "X")
    H.box(P, 0.42, 0.025, 0.32, "A7X_Aluminium")
    for sx in (-0.10, 0.10):
        H.box(P @ T(sx, 0.07, -0.06), 0.13, 0.018, 0.022, "A7X_Amber")
        for ex in (-0.06, 0.06):
            H.box(P @ T(sx + ex, 0.04, -0.06), 0.018, 0.06, 0.022, "A7X_Amber")
    H.box(P @ T(0, 0.035, 0.15), 0.36, 0.05, 0.018, "A7X_Amber")


def porthole(H, th, y, r=3.20):
    M = hull_frame(r - 0.03, th, y)
    seg = S(28, 10)
    H.lathe(M, [(0.38, 0.0), (0.38, 0.065), (0.345, 0.095), (0.275, 0.095), (0.255, 0.060)], seg, "A7X_Aluminium")
    H.lathe(M, [(0.255, 0.060), (0.0, 0.060)], seg, "A7X_Glass", uv="p")
    if LOD == 0:
        for k in range(12):
            a = TAU * k / 12
            H.cyl(M @ Vector((0.312 * math.sin(a), 0.095, 0.312 * math.cos(a))),
                  M @ Vector((0.312 * math.sin(a), 0.112, 0.312 * math.cos(a))), 0.016, 6, "A7X_DarkMetal")


def nozzle(PR, o, d, scale=1.0):
    s = scale
    prof = [(0.045 * s, 0), (0.035 * s, 0.05 * s), (0.085 * s, 0.20 * s), (0.075 * s, 0.20 * s), (0.028 * s, 0.06 * s), (0, 0.05 * s)]
    PR.lathe(align_y(o, d), prof, S(14, 6), "A7X_Engine", uv="n", urep=1.0)


def rcs_quad(H, PR, MK, r, th, y):
    M = hull_frame(r, th, y)
    H.box(M @ T(0, 0.10, 0), 0.34, 0.24, 0.46, "A7X_Graphite")
    H.box(M @ T(0, 0.31, 0), 0.52, 0.22, 0.52, "A7X_DarkMetal")
    if LOD < 2:
        H.box(M @ T(0, 0.425, 0), 0.42, 0.012, 0.42, "A7X_PaintRed")
        for sx in (-1, 1):
            H.box(M @ T(sx * 0.215, 0.10, 0), 0.03, 0.20, 0.50, "A7X_Aluminium")
    o = M @ Vector((0, 0.31, 0))
    for d in (tangent(th), -tangent(th), YAFT, -YAFT):
        nozzle(PR, o + Vector(d) * 0.25, d)


def lamp(H, LP, M, mat, r=0.06):
    seg = S(12, 6)
    if LOD < 2:
        H.lathe(M, [(r * 1.6, 0), (r * 1.6, 0.03), (r * 1.15, 0.045), (r, 0.045)], seg, "A7X_DarkMetal")
    LP.lathe(M, [(r, 0.03), (r * 0.87, 0.03 + r * 0.5), (r * 0.5, 0.03 + r * 0.87), (0, 0.03 + r)], seg, mat)


def main_engine(PR, th):
    c = hp(2.2, th, 21.12)
    d = (YAFT * math.cos(rad(6)) + radial(th) * math.sin(rad(6))).normalized()
    M = align_y(c, d)
    seg = S(40, 10)
    PR.lathe(align_y(c, YAFT), [(0.64, 0.0), (0.64, 0.018), (0.33, 0.018)], seg, "A7X_MLIGold", uv="p")
    PR.lathe(M, [(0.34, -0.03), (0.34, 0.08), (0.25, 0.13), (0.25, 0.21), (0.20, 0.21)], seg, "A7X_DarkMetal")
    outer = [(0.20, 0.21), (0.225, 0.27), (0.225, 0.40), (0.14, 0.53), (0.20, 0.68), (0.30, 0.92), (0.40, 1.20),
             (0.48, 1.48), (0.535, 1.74), (0.55, 1.86)]
    PR.lathe(M, outer, seg, "A7X_Engine", uv="n", urep=2.0)
    inner = [(0.55, 1.86), (0.53, 1.86), (0.515, 1.74), (0.46, 1.48), (0.38, 1.20), (0.28, 0.92), (0.18, 0.68),
             (0.12, 0.53), (0.0, 0.47)]
    PR.lathe(M, inner, seg, "A7X_Engine", uv="n", urep=2.0, vflip=True)
    for (rr, yy) in ((0.405, 1.20), (0.552, 1.84)):
        PR.ring(M, rr, rr + 0.022, yy - 0.016, yy + 0.016, seg, "A7X_DarkMetal")
    if LOD < 2:
        t = tangent(th)
        rd = radial(th)
        for a, b in ((c + t * 0.56, c + d * 0.42 + t * 0.21), (c - t * 0.56, c + d * 0.42 - t * 0.21),
                     (c + rd * 0.52, c + d * 0.42 + rd * 0.21)):
            PR.cyl(a, b, 0.036, S(10, 6), "A7X_DarkMetal")
            PR.cyl(a + (b - a) * 0.45, b, 0.022, S(8, 6), "A7X_Aluminium")
        PR.cyl(c - rd * 0.50, c + d * 0.30 - rd * 0.215, 0.032, S(10, 6), "A7X_MLIGold")
        PR.cyl(c - rd * 0.50 + t * 0.12, c + d * 0.30 - rd * 0.2 + t * 0.09, 0.022, S(8, 6), "A7X_Amber")


# ----------------------------------------------------------------------------- the vehicle

def sphere_profile(R, n):
    return [(R * math.sin(PI * k / n), -R * math.cos(PI * k / n)) for k in range(n + 1)]


def build_details(H, PR, MK, LP):
    """Mid-scale equipment: tanks, thrust struts, raceways, louvers, antennas, star trackers, access panels."""
    # pressurant tanks on the aft bulkhead, port and starboard
    for sx in (-1, 1):
        c = Vector((sx * 2.30, 21.12 + 0.20, AXC))
        M = align_y(c, YAFT)
        H.lathe(M, sphere_profile(0.40, S(12, 5)), S(24, 8), "A7X_PaintWhite")
        H.ring(M, 0.40, 0.418, -0.035, 0.035, S(24, 8), "A7X_DarkMetal")
        if LOD < 2:
            H.cyl(c + Vector((0, 0.38, 0)), c + Vector((0, 0.47, 0)), 0.05, 8, "A7X_Aluminium")
            H.cyl(c + Vector((0, 0.44, 0)), c + Vector((-sx * 0.42, 0.44, 0.30)), 0.018, 6, "A7X_Amber")
            H.cyl(c + Vector((-sx * 0.42, 0.44, 0.30)), Vector((sx * 1.86, 21.15, AXC + 0.30)), 0.018, 6, "A7X_Amber")
    if LOD < 2:
        # thrust struts between the engine mounts and the docking ring
        for k in range(4):
            th = TAU * (k + 0.5) / 4
            for off in (-1, 1):
                a = hp(1.74, th + off * rad(16), 21.17)
                b = hp(2.2, th, 21.20) + tangent(th) * off * 0.30 - radial(th) * 0.10
                PR.cyl(a, b, 0.032, S(8, 6), "A7X_Aluminium")
    # cable raceways with clamps
    if LOD < 2:
        for th, r, y0, y1 in ((0.0, 3.20, 3.62, 14.95), (PI, 3.20, 3.62, 14.95), (rad(16), 3.0, 15.66, 20.56),
                              (rad(-16), 3.0, 15.66, 20.56), (rad(164), 3.0, 15.66, 20.56), (rad(-164), 3.0, 15.66, 20.56)):
            w = 0.17 if r > 3.1 else 0.12
            H.box(hull_frame(r, th, (y0 + y1) / 2) @ T(0, 0.028, 0), w, 0.056, y1 - y0, "A7X_Aluminium")
            if LOD == 0:
                n = int((y1 - y0) / 1.45) + 1
                for i in range(n + 1):
                    y = y0 + 0.12 + (y1 - y0 - 0.24) * i / n
                    H.box(hull_frame(r, th, y) @ T(0, 0.036, 0), w + 0.07, 0.072, 0.05, "A7X_DarkMetal")
    # thermal louvers, dorsal and ventral, aft of the antenna pedestal
    for thc in (0.0, PI):
        H.panel(3.03, 0.03, thc - rad(9.2), thc + rad(9.2), 17.62, 19.42, S(4, 2), "A7X_Aluminium", sides=LOD < 2)
        if LOD < 2:
            for i in range(9):
                y = 17.74 + i * 0.195
                H.panel(3.055, 0.02, thc - rad(8.2), thc + rad(8.2), y, y + 0.15, 2, "A7X_PaintWhite")
    # low-gain antennas on the command module
    for th in (rad(10), rad(-10), rad(170), rad(-170)):
        M = hull_frame(3.55, th, -0.55)
        H.box(M @ T(0, 0.04, 0), 0.18, 0.08, 0.18, "A7X_DarkMetal")
        H.cyl(M @ Vector((0, 0.08, 0)), M @ Vector((0, 0.50, 0)), 0.02, 6, "A7X_Aluminium")
        H.lathe(M @ T(0, 0.50, 0), [(0, 0), (0.10, 0), (0.10, 0.012), (0.075, 0.012), (0.022, 0.24), (0, 0.24)], S(14, 6), "A7X_PaintWhite")
    # star trackers
    if LOD < 2:
        for th in (rad(40), rad(-40)):
            M = hull_frame(3.55, th, 2.15)
            H.box(M @ T(0, 0.06, 0), 0.24, 0.12, 0.24, "A7X_DarkMetal")
            d = (radial(th) * 0.8 - YAFT * 0.6).normalized()
            H.lathe(align_y(M @ Vector((0, 0.10, 0)), d), [(0.065, 0), (0.115, 0.26), (0.103, 0.26), (0.055, 0.03), (0, 0.03)], S(14, 6), "A7X_DarkMetal")
    # amber-edged service access panels on the command module flanks
    for sgn in (1, -1):
        thc = sgn * rad(90)
        H.panel(3.572, 0.022, thc - rad(9.6), thc + rad(9.6), 0.30, 1.40, S(4, 2), "A7X_PaintAmber", sides=LOD < 2)
        H.panel(3.584, 0.02, thc - rad(8.9), thc + rad(8.9), 0.345, 1.355, S(4, 2), "A7X_PaintWhite", sides=LOD < 2)
        if LOD < 2:
            handrail(H, 3.584, thc, 0.55, 1.15)
            for a in (-7.9, 7.9):
                for y in (0.42, 0.85, 1.28):
                    Mf = hull_frame(3.584, thc + rad(a), y)
                    H.lathe(Mf, [(0.028, 0), (0.028, 0.012), (0, 0.012)], 8, "A7X_DarkMetal")
            MK.decal_cyl(3.5852, thc + sgn * rad(4.6), 0.85, 0.07, "srv_dark", sgn, -sgn, nseg=1)
    # grapple fixture on the habitat
    if LOD < 2:
        for th, y in ((rad(40), 10.05), (rad(-40), 10.05)):
            M = hull_frame(3.20, th, y)
            H.lathe(M, [(0.30, 0), (0.30, 0.035), (0.26, 0.05), (0, 0.05)], S(20, 8), "A7X_Aluminium")
            H.cyl(M @ Vector((0, 0.05, 0)), M @ Vector((0, 0.30, 0)), 0.028, 8, "A7X_DarkMetal")
            H.lathe(M @ T(0, 0.30, 0), sphere_profile(0.05, 4), 8, "A7X_DarkMetal")
            for k in range(3):
                a = TAU * k / 3
                H.box(M @ Matrix.Rotation(a, 4, "Y") @ T(0.19, 0.075, 0), 0.16, 0.05, 0.05, "A7X_DarkMetal")
            H.box(M @ T(0.0, 0.03, 0.42), 0.20, 0.06, 0.16, "A7X_DarkMetal")
            H.box(M @ T(0.0, 0.062, 0.42), 0.10, 0.004, 0.10, "A7X_PaintWhite")

def build_hull(H, MK, LP, PR):
    seg = S(64, 16)
    A = axis_m()
    # --- command module: sensor nose, faceted cone, blanketed barrel
    H.lathe(A, [(0, -5.10), (0.55, -5.07), (0.98, -4.96), (1.22, -4.80)], S(32, 12), "A7X_PaintWhite", uv="p")
    H.lathe(A, [(1.22, -4.80), (1.36, -4.76), (1.42, -4.66)], S(32, 12), "A7X_Aluminium")
    ra, ya, rb, yb = 1.42, -4.66, 3.34, -1.50
    H.lathe(A, [(ra, ya), (rb, yb)], 4, "A7X_Graphite", smooth=False, a0=rad(-45), a1=rad(45))      # visor facets
    H.lathe(A, [(ra, ya), (rb, yb)], 12, "A7X_Blanket", smooth=False, a0=rad(45), a1=rad(315))
    if LOD < 2:
        for k in range(16):                                    # battens on the facet edges
            th = TAU * k / 16
            H.cyl(hp(ra + 0.012, th, ya), hp(rb + 0.012, th, yb), 0.022, 6, "A7X_DarkMetal", caps=False)
    # forward sensor suite on the nose plate
    nose = Vector((0.0, -5.085, AXC))
    H.cyl(nose + Vector((0, 0.02, 0)), nose + Vector((0, -0.13, 0)), 0.24, S(24, 8), "A7X_DarkMetal")
    H.lathe(align_y(nose + Vector((0, -0.131, 0)), -YAFT), [(0.19, 0.0), (0.0, 0.0)], S(24, 8), "A7X_Glass", uv="p")
    H.ring(align_y(nose, -YAFT), 0.42, 0.50, -0.01, 0.035, S(32, 10), "A7X_Aluminium")
    for sx in (-1, 1):
        lamp(H, LP, align_y(nose + Vector((sx * 0.70, 0.05, 0.0)), -YAFT), "A7X_LampWhite", 0.075)
        if LOD < 2:
            H.box(T(sx * 0.33, -5.10, AXC + 0.62), 0.16, 0.10, 0.16, "A7X_DarkMetal")
            H.box(T(sx * 0.33, -5.10, AXC - 0.62), 0.20, 0.07, 0.12, "A7X_Aluminium")
    H.ring(A, 1.30, 1.47, -4.71, -4.60, S(32, 12), "A7X_Aluminium")
    H.ring(A, 3.20, 3.42, -1.58, -1.43, seg, "A7X_Aluminium")
    H.lathe(A, [(3.34, -1.44), (3.50, -1.30), (3.55, -1.05)], seg, "A7X_Graphite")
    H.lathe(A, [(3.55, -1.05), (3.55, 2.70)], seg, "A7X_Blanket")
    for y in (-0.99, 2.64):
        H.ring(A, 3.55, 3.61, y - 0.055, y + 0.055, seg, "A7X_Aluminium")
    for y in (0.20, 1.50):
        H.ring(A, 3.55, 3.564, y - 0.035, y + 0.035, seg, "A7X_Graphite")
    if LOD < 2:
        for k in range(8):
            th = TAU * (k + 0.5) / 8
            H.panel(3.562, 0.012, th - 0.012, th + 0.012, -0.93, 2.585, 1, "A7X_Graphite")
    H.lathe(A, [(3.55, 2.70), (3.42, 2.92), (3.14, 3.22), (3.08, 3.30)], seg, "A7X_Graphite")

    def cone_pt(th, y):
        return hp(ra + (y - ya) * (rb - ra) / (yb - ya), th, y)

    for k in (-2, -1, 0, 1):                                   # four flight-deck windows on the upper facets
        tha, thb = rad(22.5 * k), rad(22.5 * (k + 1))

        def fp(s, y):
            return cone_pt(tha, y).lerp(cone_pt(thb, y), s)

        n = (fp(1, -3.0) - fp(0, -3.0)).cross(fp(0, -2.0) - fp(0, -3.0)).normalized()
        if n.dot(radial((tha + thb) / 2)) < 0:
            n = -n
        outer = [fp(0.09, -3.98), fp(0.91, -3.98), fp(0.91, -2.42), fp(0.09, -2.42)]
        inner = [fp(0.20, -3.82), fp(0.80, -3.82), fp(0.80, -2.58), fp(0.20, -2.58)]
        H.frame_ring(outer, inner, n, 0.075, 0.022, "A7X_DarkMetal", "A7X_Glass")
    # eyebrow sun shade above the windows
    for k in (-2, -1, 0, 1):
        tha, thb = rad(22.5 * k), rad(22.5 * (k + 1))
        a, b = cone_pt(tha, -2.30), cone_pt(thb, -2.30)
        mid = (a + b) / 2
        nrm = radial((tha + thb) / 2)
        H.box(frame(mid + nrm * 0.09, b - a, nrm), (b - a).length * 0.96, 0.18, 0.05, "A7X_DarkMetal")

    # --- habitat: pressure shell, frame rings, MMOD shield panels
    H.lathe(A, [(3.08, 3.30), (3.08, 15.22)], S(48, 16), "A7X_Graphite")
    for y in (3.37, 15.15):
        H.ring(A, 3.08, 3.27, y - 0.07, y + 0.07, seg, "A7X_Aluminium")
    rng = random.Random(7)
    rows, cols, gap = 8, 16, 0.032
    ys, ye = 3.47, 15.05
    for i in range(rows):
        y0 = ys + (ye - ys) * i / rows + gap / 2
        y1 = ys + (ye - ys) * (i + 1) / rows - gap / 2
        for j in range(cols):
            th0 = TAU * (j + 0.5) / cols + gap / 2 / 3.2
            th1 = TAU * (j + 1.5) / cols - gap / 2 / 3.2
            cx, cy = rng.randrange(4), rng.randrange(4)
            m = 0.0012
            rect = (cx / 4 + m, 1 - (cy + 1) / 4 + m, (cx + 1) / 4 - m, 1 - cy / 4 - m)
            H.panel(3.20, 0.075, th0, th1, y0, y1, S(6, 2), "A7X_Whipple", rect=rect, sides=LOD < 2)
    # portholes just above the array line, both sides
    for sgn in (1, -1):
        for y in (4.85, 6.75, 11.55, 13.45):
            porthole(H, sgn * rad(79), y)
    # handrails: two dorsal and two ventral lines, plus cross-overs
    row_len = (ye - ys) / rows
    for th in (rad(28), rad(-28), rad(152), rad(-152)):
        for i in range(rows):
            y0 = ys + row_len * i
            handrail(H, 3.20, th, y0 + 0.18, y0 + row_len - 0.18)
    for y in (4.2, 9.2, 14.3):
        handrail_arc(H, 3.20, rad(-22), rad(-4), y)
        handrail_arc(H, 3.20, rad(4), rad(22), y)
    for th in (rad(28), rad(-28)):
        handrail(H, 3.55, th, -0.75, 0.10)
        handrail(H, 3.55, th, 0.35, 1.35)
        handrail(H, 3.55, th, 1.65, 2.50)
    foot_restraint(H, 3.20, rad(12), 8.3)
    foot_restraint(H, 3.20, rad(-40), 12.6, rad(90))
    foot_restraint(H, 3.20, rad(64), 10.9, rad(180))

    # --- service module: graphite barrel, foil-wrapped bays, aft bulkhead
    H.lathe(A, [(3.08, 15.22), (3.00, 15.40), (3.00, 20.92), (2.86, 21.12)], seg, "A7X_Graphite")
    H.lathe(A, [(2.86, 21.12), (0.0, 21.12)], seg, "A7X_Graphite", uv="p")
    H.ring(A, 3.00, 3.045, 20.62, 20.72, seg, "A7X_Aluminium")
    H.ring(A, 3.00, 3.045, 15.52, 15.62, seg, "A7X_Aluminium")
    for k in range(4):
        thc = TAU * (k + 0.5) / 4
        H.panel(3.035, 0.035, thc - rad(23), thc + rad(23), 16.05, 19.35, S(10, 3), "A7X_MLIGold", sides=LOD < 2)
        if LOD < 2:                                         # bay frame battens
            for th in (thc - rad(23.6), thc + rad(23.6)):
                H.panel(3.05, 0.05, th - 0.012, th + 0.012, 16.0, 19.4, 1, "A7X_Aluminium")
            for y in (16.0, 19.4):
                H.panel(3.05, 0.05, thc - rad(23.6), thc + rad(23.6), y - 0.035, y + 0.035, S(10, 3), "A7X_Aluminium")
    # docking mount ring on the bulkhead (the adapter's base flange lands on it)
    H.lathe(A, [(1.78, 21.12), (1.78, 21.148), (1.20, 21.148)], seg, "A7X_Aluminium", uv="p")
    if LOD == 0:
        for k in range(24):
            a = TAU * (k + 0.5) / 24
            H.cyl(hp(1.70, a, 21.148), hp(1.70, a, 21.172), 0.024, 6, "A7X_DarkMetal")
    for k in range(4):
        main_engine(PR, TAU * (k + 0.5) / 4)
    build_details(H, PR, MK, LP)
    # reaction-control quads: four aft, four forward
    for k in range(4):
        rcs_quad(H, PR, MK, 3.00, TAU * k / 4, 20.20)
        rcs_quad(H, PR, MK, 3.55, TAU * (k + 0.5) / 4, 0.85)
    # docking floodlights and the aft strobe
    for sx in (-1, 1):
        o = Vector((sx * 1.12, 21.12, AXC + 2.36))
        H.box(T(*o) @ T(0, 0.10, 0), 0.30, 0.20, 0.22, "A7X_DarkMetal")
        H.box(T(*o) @ T(0, 0.215, 0.13), 0.34, 0.06, 0.03, "A7X_DarkMetal")
        LP.quad_out([o + Vector((-0.12, 0.202, -0.08)), o + Vector((0.12, 0.202, -0.08)),
                     o + Vector((0.12, 0.202, 0.08)), o + Vector((-0.12, 0.202, 0.08))],
                    [(0, 0), (1, 0), (1, 1), (0, 1)], YAFT, "A7X_LampWhite")
    # navigation and formation lamps
    lamp(H, LP, hull_frame(3.55, rad(90), 1.95), "A7X_LampRed", 0.07)
    lamp(H, LP, hull_frame(3.55, rad(-90), 1.95), "A7X_LampCyan", 0.07)
    lamp(H, LP, hull_frame(3.00, rad(45), 20.80), "A7X_LampWhite", 0.05)
    lamp(H, LP, hull_frame(3.00, rad(-135), 20.80), "A7X_LampWhite", 0.05)
    for th in (rad(45), rad(135), rad(-45), rad(-135)):
        lamp(H, LP, hull_frame(3.27, th, 3.37), "A7X_LampAmber", 0.04)
        lamp(H, LP, hull_frame(3.27, th, 15.15), "A7X_LampAmber", 0.04)

    # --- markings (insignia-free)
    for sgn in (1, -1):
        MK.decal_cyl(3.211, sgn * rad(53), 6.55, 0.92, "name_dark", sgn, -sgn, nseg=S(6, 2))
        MK.decal_cyl(3.211, sgn * rad(53), 12.55, 0.30, "reg_dark", sgn, -sgn, nseg=2)
        MK.decal_cyl(3.561, sgn * rad(64), 0.85, 0.34, "reg_dark", sgn, -sgn, nseg=2)
        if LOD < 2:
            MK.decal_cyl(3.211, sgn * rad(20), 5.4, 0.11, "eva", sgn, -sgn, nseg=1)
            MK.decal_cyl(3.211, sgn * rad(20), 11.2, 0.11, "eva", sgn, -sgn, nseg=1)
            MK.decal_cyl(3.211, sgn * rad(36), 7.6, 0.16, "chev_cyan", sgn, -sgn, nseg=1)
            MK.decal_cyl(3.211, sgn * rad(36), 13.6, 0.16, "chev_cyan", sgn, -sgn, nseg=1)
            MK.decal_cyl(3.008, sgn * rad(12), 19.55, 0.13, "rcs", sgn, -sgn, nseg=1)
            MK.decal_cyl(3.008, sgn * rad(78), 19.72, 0.13, "rcs", sgn, -sgn, nseg=1)
            MK.decal_cyl(3.008, sgn * rad(102), 16.6, 0.12, "srv", sgn, -sgn, nseg=1)
    MK.decal_ring(3.006, 20.83, 0.15, "hazard_amber", 6, S(10, 4))
    MK.decal_ring(3.557, -1.18, 0.07, "bar_amber", 1, S(64, 16))
    # bulkhead legends (read from aft: right is -x)
    MK.decal_flat(frame((0.0, 21.126, AXC + 2.33), (-1, 0, 0), (0, 0, 1)), 0.13, "dockl")
    MK.decal_flat(frame((0.0, 21.126, AXC - 2.30), (-1, 0, 0), (0, 0, 1)), 0.56, "target")
    if LOD < 2:
        for sx in (-1, 1):
            MK.decal_cyl(3.008, sx * rad(45), 20.42, 0.105, "eng", sx, -sx, nseg=1)


def build_array(Aq, s):
    """Roll-out solar wing: drive, mast, yoke, root canister, two slit-tube booms, blanket, tip mandrel."""
    phi = rad(-57.5)                                              # blanket normal faces the departure sun
    M = T(0, 9.2, AXC) @ Matrix.Rotation(phi, 4, "X")

    def W(x, y, z):
        return M @ Vector((s * x, y, z))

    seg = S(24, 8)
    Aq.cyl(W(3.02, 0, 0), W(3.80, 0, 0), 0.34, S(32, 10), "A7X_Graphite")
    for x in (3.24, 3.74):
        Aq.cyl(W(x, 0, 0), W(x + 0.06, 0, 0), 0.40, S(32, 10), "A7X_Aluminium")
    Aq.cyl(W(3.80, 0, 0), W(4.88, 0, 0), 0.10, seg, "A7X_Aluminium")
    Aq.cyl(W(4.80, 0, 0), W(4.92, 0, 0), 0.15, seg, "A7X_DarkMetal")
    for sy in (-1, 1):
        Aq.cyl(W(4.15, 0, 0), W(4.98, sy * 1.95, 0), 0.04, S(10, 6), "A7X_Aluminium")
        Aq.cyl(W(5.05, sy * 2.14, 0), W(12.20, sy * 2.14, 0), 0.06, S(12, 6), "A7X_DarkMetal")
        Aq.box(frame(W(5.0, sy * 2.40, 0), M.to_3x3() @ Vector((s, 0, 0)), M.to_3x3() @ Vector((0, 1, 0))), 0.42, 0.07, 0.42, "A7X_Aluminium")
        Aq.box(frame(W(12.2, sy * 2.34, 0), M.to_3x3() @ Vector((s, 0, 0)), M.to_3x3() @ Vector((0, 1, 0))), 0.30, 0.06, 0.30, "A7X_Aluminium")
    Aq.cyl(W(5.0, -2.36, 0), W(5.0, 2.36, 0), 0.17, seg, "A7X_Graphite")
    Aq.cyl(W(12.2, -2.30, 0), W(12.2, 2.30, 0), 0.12, seg, "A7X_Graphite")
    # blanket: 7.0 x 4.0 m, cells on the sun side, polyimide harness side behind
    nx, ny = S(28, 6), S(4, 1)
    x0, x1, hw = 5.14, 12.14, 2.0
    nrm = M.to_3x3() @ Vector((0, 0, 1))

    def bz(x, y):
        return 0.014 * math.sin(TAU * (x - x0) / 1.75) + 0.03 * (1 - (y / hw) ** 2) * math.sin(PI * (x - x0) / (x1 - x0))

    for i in range(nx):
        for j in range(ny):
            xa, xb = x0 + (x1 - x0) * i / nx, x0 + (x1 - x0) * (i + 1) / nx
            ya, yb = -hw + 2 * hw * j / ny, -hw + 2 * hw * (j + 1) / ny
            c = [(xa, ya), (xb, ya), (xb, yb), (xa, yb)]
            uv = [(x - x0, y + hw) for x, y in c]
            Aq.quad_out([W(x, y, bz(x, y)) for x, y in c], uv, nrm, "A7X_SolarCells", True)
            Aq.quad_out([W(x, y, bz(x, y) - 0.008) for x, y in c], uv, -nrm, "A7X_SolarBack", True)
    if LOD < 2:                                                    # back-side battens
        for k in range(1, 7):
            x = x0 + k * 1.0
            Aq.box(frame(W(x, 0, bz(x, 0) - 0.03), M.to_3x3() @ Vector((s, 0, 0)), M.to_3x3() @ Vector((0, 1, 0))), 0.035, 4.22, 0.028, "A7X_DarkMetal")


def build_radiators(R, MK):
    """Two deployable radiator wings on the service module, canted 28 deg below the array plane."""
    for s in (1, -1):
        e = Vector((s * math.cos(rad(28)), 0, -math.sin(rad(28))))
        ntop = e.cross(YAFT) * s
        o = Vector((s * 3.03, 17.6, AXC))
        F = frame(o, e, YAFT)                                       # local x along the wing, y aft
        R.box(F @ T(0.11, 0, 0), 0.26, 3.74, 0.12, "A7X_Aluminium")
        for (a, b) in ((0.28, 1.98), (2.04, 3.74)):
            R.box(F @ T((a + b) / 2, 0, 0), b - a, 3.60, 0.036, "A7X_Radiator")
            if LOD < 2:
                for x in (a, b):
                    R.box(F @ T(x, 0, 0), 0.03, 3.64, 0.056, "A7X_Aluminium")
                for y in (-1.81, 1.81):
                    R.box(F @ T((a + b) / 2, y, 0), b - a, 0.03, 0.056, "A7X_Aluminium")
        if LOD < 2:
            for y in (-1.3, 0, 1.3):
                R.cyl(F @ Vector((2.01, y - 0.17, 0)), F @ Vector((2.01, y + 0.17, 0)), 0.034, S(10, 6), "A7X_DarkMetal")
            for y in (-1.55, 1.55):
                R.cyl(Vector((s * 2.98, 17.6 + y, AXC + 0.95)), F @ Vector((1.90, y, 0.0)), 0.024, S(8, 6), "A7X_Aluminium")
            for dz in (0.085, -0.085):
                R.cyl(F @ Vector((0.11, -1.9, dz)), F @ Vector((0.11, 1.9, dz)), 0.028, S(8, 6), "A7X_Amber")
            MK.decal_flat(frame(o + e * 1.10 + ntop * 0.021 + YAFT * 0.0, (-e).cross(ntop), -e), 0.085, "rad")
            MK.decal_flat(frame(o + e * 2.9 + ntop * 0.021 + YAFT * 1.25, (-e).cross(ntop), -e), 0.085, "nostep")


def build_hga(G, MK):
    """High-gain antenna: pedestal, boom, two-axis gimbal, 2.5 m reflector with tripod feed."""
    base = hp(3.0, 0.0, 16.75)
    G.box(hull_frame(3.0, 0.0, 16.75) @ T(0, 0.13, 0), 0.62, 0.30, 0.62, "A7X_DarkMetal")
    G.box(hull_frame(3.0, 0.0, 16.75) @ T(0, 0.30, 0), 0.44, 0.06, 0.44, "A7X_Aluminium")
    d = (radial(0.0) + Vector((0, 0.16, 0))).normalized()
    p0 = base + radial(0.0) * 0.30
    p1 = p0 + d * 2.9
    seg = S(16, 6)
    G.cyl(p0, p1, 0.085, seg, "A7X_Aluminium")
    G.cyl(p0 + d * 1.28, p0 + d * 1.42, 0.125, seg, "A7X_DarkMetal")
    if LOD < 2:
        side = Vector((1, 0, 0))
        G.cyl(p0 + side * 0.11, p1 + side * 0.11, 0.02, 6, "A7X_Amber")
    G.cyl(p1, p1 + d * 0.26, 0.17, S(20, 8), "A7X_DarkMetal")
    p2 = p1 + d * 0.26
    f = Vector((0.42, 0.62, 0.66)).normalized()                     # boresight
    a = d.cross(f).normalized()                                     # elevation axis
    for sg in (-1, 1):
        G.box(frame(p2 + a * sg * 0.31 + d * 0.24, a, d), 0.06, 0.56, 0.13, "A7X_Aluminium")
    G.box(frame(p2 + d * 0.03, a, d), 0.68, 0.06, 0.16, "A7X_Aluminium")
    hub = p2 + d * 0.46
    G.cyl(hub - a * 0.34, hub + a * 0.34, 0.05, S(12, 6), "A7X_DarkMetal")
    v0 = hub + f * 0.24
    G.cyl(hub - f * 0.10, v0, 0.16, S(20, 8), "A7X_DarkMetal")
    Fd = 1.0
    Rd = 1.25
    n = S(10, 4)
    back = [(0.0, -0.035)] + [(Rd * k / n, (Rd * k / n) ** 2 / (4 * Fd) - 0.035) for k in range(1, n + 1)]
    rim_y = Rd * Rd / (4 * Fd)
    front = [(Rd * k / n, (Rd * k / n) ** 2 / (4 * Fd)) for k in range(n, 0, -1)] + [(0.0, 0.0)]
    prof = back + [(Rd + 0.03, rim_y - 0.03), (Rd + 0.03, rim_y + 0.012)] + front
    Md = align_y(v0, f)
    G.lathe(Md, prof, S(48, 12), "A7X_PaintWhite", uv="p")
    yb = 0.7 ** 2 / (4 * Fd) - 0.035
    G.lathe(Md, [(0.68, yb - 0.005), (0.68, yb - 0.075), (0.72, yb - 0.075), (0.72, yb + 0.002)], S(32, 10), "A7X_Aluminium")
    if LOD < 2:
        for k in range(8):
            an = TAU * k / 8
            pa = Vector((0.2 * math.sin(an), 0.2 ** 2 / 4 - 0.035, 0.2 * math.cos(an)))
            pb = Vector((1.22 * math.sin(an), 1.22 ** 2 / 4 - 0.035, 1.22 * math.cos(an)))
            G.box(Md @ frame((pa + pb) / 2 + Vector((0, -0.04, 0)), pb - pa, (0, -1, 0)), (pb - pa).length, 0.075, 0.02, "A7X_Aluminium")
        for k in range(3):
            an = TAU * k / 3 + 0.3
            G.cyl(Md @ Vector((1.20 * math.sin(an), rim_y - 0.03, 1.20 * math.cos(an))), Md @ Vector((0.06 * math.sin(an), 0.96, 0.06 * math.cos(an))), 0.017, 6, "A7X_Aluminium")
    G.cyl(Md @ Vector((0, 1.06, 0)), Md @ Vector((0, 0.88, 0)), 0.065, S(14, 6), "A7X_DarkMetal", r1=0.105)
    G.cyl(Md @ Vector((0, 1.06, 0)), Md @ Vector((0, 1.13, 0)), 0.09, S(14, 6), "A7X_Aluminium")
    if LOD < 2:
        MK.decal_flat(frame(base + Vector((0.312, 0.0, 0.14)), (0, 1, 0), (0, 0, 1)), 0.075, "hga")
        MK.decal_flat(frame(base + Vector((-0.312, 0.0, 0.14)), (0, -1, 0), (0, 0, 1)), 0.075, "hga")


def build_adapter(D, MK, LP):
    """Aft docking adapter in its own frame: origin on the bulkhead, local y along the docking axis.
    Envelope and mating interface are those of the reviewed collar: base flange r 1.61, barrel r 1.28,
    mating flange 0.90-1.34 at 3.51-3.77, contact seal 0.75-0.97 ending on the contact plane at 3.84."""
    A = T(0, 0, 0)
    seg = S(64, 16)
    D.lathe(A, [(1.61, -0.012), (1.61, 0.20), (1.57, 0.24), (1.34, 0.24)], seg, "A7X_Aluminium")
    D.lathe(A, [(1.34, 0.24), (1.28, 0.30), (1.28, 3.50)], seg, "A7X_Graphite")
    for y in (0.95, 1.75):
        D.ring(A, 1.28, 1.325, y - 0.035, y + 0.035, seg, "A7X_DarkMetal")
    D.ring(A, 1.28, 1.39, 2.4244, 2.6444, seg, "A7X_Aluminium")
    D.lathe(A, [(1.28, 3.495), (1.34, 3.51), (1.34, 3.77), (0.97, 3.77)], seg, "A7X_PaintWhite")
    D.lathe(A, [(0.97, 3.77), (0.97, 3.84), (0.75, 3.84), (0.75, 3.30)], seg, "A7X_Rubber")
    D.lathe(A, [(0.75, 3.30), (0.0, 3.30)], seg, "A7X_PaintWhite", uv="p")
    # hatch furniture, well clear of the capsule's guide lugs (0.15 m past the contact plane)
    D.lathe(A, [(0.17, 3.30), (0.17, 3.325), (0.13, 3.335), (0.0, 3.335)], S(20, 8), "A7X_DarkMetal")
    D.lathe(A, [(0.115, 3.336), (0.0, 3.336)], S(20, 8), "A7X_Glass", uv="p")
    D.ring(A, 0.60, 0.66, 3.30, 3.33, S(48, 12), "A7X_Aluminium")
    LP.lathe(A, [(0.70, 3.302), (0.70, 3.318), (0.675, 3.318)], S(48, 12), "A7X_LampCyan")
    if LOD < 2:
        for k in range(8):
            a = TAU * k / 8
            D.box(frame(hp(0.63, a, 3.325), tangent(a), radial(a)), 0.11, 0.07, 0.045, "A7X_DarkMetal")
        D.box(T(0.0, 3.33, -0.36), 0.34, 0.035, 0.04, "A7X_Amber")
        for sx in (-0.15, 0.15):
            D.box(T(sx, 3.318, -0.36), 0.03, 0.03, 0.04, "A7X_Amber")
    for k in range(4):
        th = -rad(45 + 90 * k)
        D.box(frame(hp(1.31, th, 1.79), tangent(th), radial(th)), 0.18, 0.15, 3.24, "A7X_Aluminium")
        latch = hp(1.20, th, 3.76)
        D.box(frame(latch, tangent(th), radial(th)), 0.35, 0.24, 0.13, "A7X_PaintAmber")
        LP.box(frame(latch + tangent(th) * 0.26, tangent(th), radial(th)), 0.14, 0.08, 0.11, "A7X_LampCyan")
        if LOD < 2:
            D.box(frame(hp(1.215, th, 3.60), tangent(th), radial(th)), 0.22, 0.20, 0.18, "A7X_DarkMetal")
            D.cyl(hp(1.36, th, 3.52), hp(1.36, th, 3.83), 0.03, 8, "A7X_Aluminium")
    if LOD < 2:
        for k in range(4):                                         # proximity sensors and retro-reflectors
            th = -rad(90 * k)
            D.box(frame(hp(1.14, th, 3.795), tangent(th), radial(th)), 0.16, 0.12, 0.05, "A7X_DarkMetal")
            LP.box(frame(hp(1.14, th, 3.823), tangent(th), radial(th)), 0.07, 0.05, 0.006,
                   "A7X_LampAmber" if k % 2 else "A7X_LampWhite")
    if LOD == 0:
        for k in range(24):
            a = TAU * (k + 0.5) / 24
            D.cyl(hp(1.475, a, 0.24), hp(1.475, a, 0.268), 0.026, 6, "A7X_DarkMetal")
        for k in range(16):
            a = TAU * (k + 0.5) / 16
            D.cyl(hp(1.155, a, 3.77), hp(1.155, a, 3.79), 0.02, 6, "A7X_DarkMetal")
    for th in (0.0, rad(90), rad(180), rad(-90)):
        handrail(D, 1.28, th, 0.42, 0.90)
        handrail(D, 1.28, th, 1.02, 1.70)
        handrail(D, 1.28, th, 1.82, 2.40)
        handrail(D, 1.28, th, 2.68, 3.46)
    if LOD < 2:
        for th in (rad(22), rad(-22), rad(158), rad(-158)):
            D.cyl(hp(1.305, th, 0.30), hp(1.305, th, 3.47), 0.024, 8, "A7X_Amber" if abs(th) < 1 else "A7X_DarkMetal")
        foot_restraint(D, 1.28, rad(68), 1.38, rad(90))
    MK.decal_ring(1.3425, 3.64, 0.11, "hazard_amber", 3, S(16, 6))
    for sgn in (1, -1):
        MK.decal_cyl(1.2845, sgn * rad(66), 1.36, 0.15, "dockl", sgn, -sgn, nseg=2)
        MK.decal_cyl(1.2845, sgn * rad(66), 3.02, 0.10, "latch", sgn, -sgn, nseg=1)


# ----------------------------------------------------------------------------- materials
MATS = {
    "A7X_Whipple": dict(tex="Whipple"),
    "A7X_Blanket": dict(tex="Blanket"),
    "A7X_Graphite": dict(tex="Graphite"),
    "A7X_Radiator": dict(tex="Radiator"),
    "A7X_SolarCells": dict(tex="SolarCells"),
    "A7X_SolarBack": dict(tex="SolarBack"),
    "A7X_MLIGold": dict(tex="MLIGold"),
    "A7X_Engine": dict(tex="Engine"),
    "A7X_Aluminium": dict(tex="Metal", tint=(0.93, 0.94, 0.96)),
    "A7X_Amber": dict(tex="Metal", tint=(1.0, 0.56, 0.11)),
    "A7X_DarkMetal": dict(tex="Metal", tint=(0.13, 0.135, 0.15)),
    "A7X_PaintWhite": dict(color=(0.70, 0.70, 0.67), smooth=0.42),
    "A7X_PaintRed": dict(color=(0.48, 0.045, 0.035), smooth=0.45),
    "A7X_PaintAmber": dict(color=(0.78, 0.40, 0.035), smooth=0.45),
    "A7X_Rubber": dict(color=(0.022, 0.022, 0.026), smooth=0.22),
    "A7X_Glass": dict(color=(0.012, 0.018, 0.026), smooth=0.96, emission=(1.0, 0.55, 0.22), estr=0.07),
    "A7X_LampRed": dict(color=(0.25, 0.02, 0.02), smooth=0.8, emission=(1.0, 0.07, 0.04), estr=5.0),
    "A7X_LampCyan": dict(color=(0.02, 0.18, 0.22), smooth=0.8, emission=(0.10, 0.85, 1.0), estr=5.0),
    "A7X_LampWhite": dict(color=(0.5, 0.5, 0.5), smooth=0.8, emission=(1.0, 0.96, 0.88), estr=7.0),
    "A7X_LampAmber": dict(color=(0.3, 0.15, 0.02), smooth=0.8, emission=(1.0, 0.55, 0.10), estr=5.0),
    "A7X_Markings": dict(base="A7X_Markings.png", alpha=True, smooth=0.35),
}


def load_img(name, colorspace):
    img = bpy.data.images.load(os.path.join(TEX, name), check_existing=True)
    img.colorspace_settings.name = colorspace
    if colorspace != "sRGB":
        img.alpha_mode = "CHANNEL_PACKED"
    return img


def make_materials():
    for name, spec in MATS.items():
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        nt = m.node_tree
        b = nt.nodes["Principled BSDF"]
        tint = spec.get("tint", (1, 1, 1))
        base = spec.get("base") or (("A7X_%s_BaseColor.png" % spec["tex"]) if "tex" in spec else None)
        if base:
            t = nt.nodes.new("ShaderNodeTexImage")
            t.image = load_img(base, "sRGB")
            mix = nt.nodes.new("ShaderNodeMixRGB")
            mix.blend_type = "MULTIPLY"
            mix.inputs[0].default_value = 1.0
            mix.inputs[2].default_value = (*tint, 1)
            nt.links.new(t.outputs["Color"], mix.inputs[1])
            col = mix.outputs[0]
            if spec.get("alpha"):
                nt.links.new(t.outputs["Alpha"], b.inputs["Alpha"])
                m.blend_method = "CLIP"
                m.alpha_threshold = 0.5
                m.shadow_method = "NONE"
        else:
            col = None
            b.inputs["Base Color"].default_value = (*spec["color"], 1)
        if "tex" in spec:
            mk = nt.nodes.new("ShaderNodeTexImage")
            mk.image = load_img("A7X_%s_Mask.png" % spec["tex"], "Non-Color")
            sep = nt.nodes.new("ShaderNodeSeparateRGB")
            nt.links.new(mk.outputs["Color"], sep.inputs[0])
            nt.links.new(sep.outputs[0], b.inputs["Metallic"])
            inv = nt.nodes.new("ShaderNodeMath")
            inv.operation = "SUBTRACT"
            inv.inputs[0].default_value = 1.0
            nt.links.new(mk.outputs["Alpha"], inv.inputs[1])
            nt.links.new(inv.outputs[0], b.inputs["Roughness"])
            ao = nt.nodes.new("ShaderNodeMixRGB")
            ao.blend_type = "MULTIPLY"
            ao.inputs[0].default_value = 0.85
            nt.links.new(col, ao.inputs[1])
            nt.links.new(sep.outputs[1], ao.inputs[2])
            col = ao.outputs[0]
            nm = nt.nodes.new("ShaderNodeTexImage")
            nm.image = load_img("A7X_%s_Normal.png" % spec["tex"], "Non-Color")
            nmap = nt.nodes.new("ShaderNodeNormalMap")
            nt.links.new(nm.outputs["Color"], nmap.inputs["Color"])
            nt.links.new(nmap.outputs["Normal"], b.inputs["Normal"])
        else:
            b.inputs["Metallic"].default_value = spec.get("metallic", 0.0)
            b.inputs["Roughness"].default_value = 1.0 - spec.get("smooth", 0.5)
        if col is not None:
            nt.links.new(col, b.inputs["Base Color"])
        if "emission" in spec:
            b.inputs["Emission"].default_value = (*spec["emission"], 1)
            b.inputs["Emission Strength"].default_value = spec["estr"]
    out = []
    for name, spec in MATS.items():
        e = {"name": name, "tex": spec.get("tex", ""), "baseMap": spec.get("base", ""),
             "tint": list(spec.get("tint", [])), "color": list(spec.get("color", [])),
             "emission": list(spec.get("emission", [])), "estr": spec.get("estr", 0.0),
             "smooth": spec.get("smooth", 0.5), "metallic": spec.get("metallic", 0.0), "alpha": bool(spec.get("alpha", False))}
        out.append(e)
    with open(os.path.join(HERE, "a7x_materials.json"), "w") as f:
        json.dump({"note": "colours are linear RGB; texture sets are A7X_<tex>_BaseColor/_Normal/_Mask.png; "
                           "mask = R metallic, G occlusion, A smoothness", "materials": out}, f, indent=1)


# ----------------------------------------------------------------------------- scene assembly
def to_object(part, name, coll):
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(v) for v in part.v], [], part.f)
    used = sorted(set(part.mat))
    for m in used:
        me.materials.append(bpy.data.materials[m])
    idx = {m: i for i, m in enumerate(used)}
    me.polygons.foreach_set("material_index", [idx[m] for m in part.mat])
    me.polygons.foreach_set("use_smooth", part.sm)
    uvl = me.uv_layers.new(name="UV0")
    uvl.data.foreach_set("uv", [c for uv in part.uv for p in uv for c in p])
    me.use_auto_smooth = True
    me.auto_smooth_angle = rad(50)
    me.update()
    ob = bpy.data.objects.new(name, me)
    coll.objects.link(ob)
    return ob


def build_all():
    global LOD, AXC
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for blk in (bpy.data.meshes, bpy.data.materials, bpy.data.images, bpy.data.cameras, bpy.data.lights):
        for d in list(blk):
            blk.remove(d)
    make_materials()
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    report = {"units": "metres", "axes": "Blender (x, y, z) = Unity (x, z, y)", "lods": {}}
    objects = {}
    for lod in (0, 1, 2):
        LOD = lod
        AXC = AX
        P = {k: Part(k) for k in ("Hull", "Markings", "Lamps", "Propulsion", "ArrayPort", "ArrayStbd", "Radiators", "HGA")}
        build_hull(P["Hull"], P["Markings"], P["Lamps"], P["Propulsion"])
        build_array(P["ArrayPort"], 1)
        build_array(P["ArrayStbd"], -1)
        build_radiators(P["Radiators"], P["Markings"])
        build_hga(P["HGA"], P["Markings"])
        coll = bpy.data.collections.new("LOD%d" % lod)
        scene.collection.children.link(coll)
        parent = bpy.data.objects.new("LOD%d" % lod, None)
        coll.objects.link(parent)
        rep = {}
        for k, part in P.items():
            ob = to_object(part, "A7X_%s_L%d" % (k, lod), coll)
            ob.parent = parent
            rep[k] = part.tris()
            objects[(k, lod)] = ob
        rep["total"] = sum(rep.values())
        allv = [v for part in P.values() for v in part.v]
        rep["bounds_unity_min"] = [round(min(v.x for v in allv), 3), round(min(v.z for v in allv), 3), round(min(v.y for v in allv), 3)]
        rep["bounds_unity_max"] = [round(max(v.x for v in allv), 3), round(max(v.z for v in allv), 3), round(max(v.y for v in allv), 3)]
        report["lods"]["LOD%d" % lod] = rep
        if lod == 0:
            AXC = 0.0
            Q = {k: Part(k) for k in ("DockAdapter", "DockMarkings", "DockLamps")}
            build_adapter(Q["DockAdapter"], Q["DockMarkings"], Q["DockLamps"])
            AXC = AX
            dcoll = bpy.data.collections.new("DockAdapter")
            scene.collection.children.link(dcoll)
            dparent = bpy.data.objects.new("DockAdapter", None)
            dcoll.objects.link(dparent)
            dparent.location = ADAPTER_ORIGIN
            drep = {}
            for k, part in Q.items():
                ob = to_object(part, "A7X_%s" % k, dcoll)
                ob.parent = dparent
                drep[k] = part.tris()
                objects[(k, 0)] = ob
            drep["total"] = sum(drep.values())
            report["adapter"] = drep
    report["hero_total_lod0_with_adapter"] = report["lods"]["LOD0"]["total"] + report["adapter"]["total"]
    with open(os.path.join(HERE, "a7x_report.json"), "w") as f:
        json.dump(report, f, indent=1, sort_keys=True)
    print("A7X_REPORT", json.dumps(report))
    return objects


def export_fbx(path, objs):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.hide_set(False)
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"MESH", "EMPTY"}, use_mesh_modifiers=True,
        mesh_smooth_type="OFF", apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
        bake_space_transform=True, axis_forward="-Z", axis_up="Y", add_leaf_bones=False, bake_anim=False,
        path_mode="STRIP", embed_textures=False, use_tspace=False)
    print("exported", path)


def export_all():
    os.makedirs(os.path.join(ROOT, "Meshes"), exist_ok=True)
    hull = [o for o in bpy.data.objects if o.name.startswith("LOD") or "_L" in o.name[-4:]]
    export_fbx(os.path.join(ROOT, "Meshes", "A7X_Exterior.fbx"), hull)
    dp = bpy.data.objects["DockAdapter"]
    dp.location = (0, 0, 0)
    bpy.context.view_layer.update()
    export_fbx(os.path.join(ROOT, "Meshes", "A7X_DockAdapter.fbx"), [dp] + list(dp.children))
    dp.location = ADAPTER_ORIGIN
    bpy.context.view_layer.update()


# ----------------------------------------------------------------------------- evidence renders
VIEWS = {
    # name: (camera, target, vertical fov) in Blender axes. The first two are the authored Bootstrap poses.
    "approach_pose": ((37.0, 47.0, 10.58), (0.0, 26.5, 1.68), 47.0),
    "collar_pose": ((9.5, 29.2, 4.08), (0.0, 26.9, 1.33), 41.0),
    "port_quarter": ((27.0, -15.0, 11.0), (0.0, 8.5, 1.3), 38.0),
    "aft_close": ((6.5, 30.5, 4.6), (0.3, 21.6, 1.2), 44.0),
    "habitat_detail": ((9.5, 3.5, 7.5), (2.2, 9.5, 2.6), 40.0),
    "profile": ((46.0, 9.5, 6.0), (0.0, 9.5, 1.3), 34.0),
}


def render_all():
    scene = bpy.context.scene
    for lod in (1, 2):
        for o in bpy.data.collections["LOD%d" % lod].objects:
            o.hide_render = True
    scene.render.engine = "CYCLES"
    prefs = bpy.context.preferences.addons["cycles"].preferences
    for kind in ("OPTIX", "CUDA"):
        try:
            prefs.compute_device_type = kind
            prefs.get_devices()
            if any(d.type == kind for d in prefs.devices):
                for d in prefs.devices:
                    d.use = d.type == kind
                scene.cycles.device = "GPU"
                print("cycles device", kind)
                break
        except Exception as ex:                                    # falls back to CPU
            print("no", kind, ex)
    scene.cycles.samples = 96
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 1920
    scene.render.resolution_y = 1080
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.view_transform = "Filmic"
    scene.view_settings.look = "Medium High Contrast"
    world = bpy.data.worlds.new("Orbit")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.012, 0.016, 0.026, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 1.0
    scene.world = world

    def sun(name, direction, strength, color):
        l = bpy.data.lights.new(name, "SUN")
        l.energy = strength
        l.color = color
        l.angle = rad(0.53)
        o = bpy.data.objects.new(name, l)
        scene.collection.objects.link(o)
        o.rotation_euler = Vector(direction).to_track_quat("-Z", "Y").to_euler()

    # Bootstrap orbit light: Unity Euler(26, 220, 0) travels along Unity (-0.578, -0.438, -0.689).
    sun("Sun", (-0.578, -0.689, -0.438), 5.0, (1.0, 0.96, 0.89))
    sun("Earthshine", (0.25, 0.35, 0.90), 0.55, (0.45, 0.62, 1.0))
    cam = bpy.data.cameras.new("Cam")
    cam.sensor_fit = "VERTICAL"
    cam.clip_start = 0.1
    cam.clip_end = 400
    co = bpy.data.objects.new("Cam", cam)
    scene.collection.objects.link(co)
    scene.camera = co
    out = os.path.join(ROOT, "Evidence~")
    os.makedirs(out, exist_ok=True)
    only = [a.split("=")[1] for a in ARGS if a.startswith("--view=")]
    for name, (pos, tgt, fov) in VIEWS.items():
        if only and name not in only:
            continue
        co.location = pos
        co.rotation_euler = (Vector(tgt) - Vector(pos)).to_track_quat("-Z", "Y").to_euler()
        cam.angle_y = rad(fov)
        scene.render.filepath = os.path.join(out, "blender_%s.png" % name)
        bpy.ops.render.render(write_still=True)
        print("rendered", name)


if __name__ == "__main__":
    build_all()
    if "--no-export" not in ARGS:
        export_all()
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "A7X_Exterior.blend"), check_existing=False, compress=True)
    if "--render" in ARGS:
        render_all()
