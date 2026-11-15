"""CARGO-CASES-V02 mesh helpers for Blender 3.6 (bmesh only, no operators that need UI context)."""
import math

import bmesh
import bpy
from mathutils import Matrix, Vector

X, Y, Z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
LABELS = "CC2_Labels"


class Part:
    """Accumulates the geometry of one exported object at one LOD."""

    def __init__(self, name, lod, atlas):
        self.name, self.lod, self.atlas = name, lod, atlas
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")
        self.mats = []

    def slot(self, mat):
        if mat not in self.mats:
            self.mats.append(mat)
        return self.mats.index(mat)

    def n(self, count):
        """Radial segment count for this LOD."""
        if self.lod == 0:
            return count
        return max(6, (count * 2) // 3) if self.lod == 1 else max(5, count // 2)

    def add(self, tmp, mat, tile=.25, smooth=True, keep_uv=False):
        idx = self.slot(mat)
        tmp.normal_update()
        tuv = tmp.loops.layers.uv.active
        vmap = {v: self.bm.verts.new(v.co) for v in tmp.verts}
        for f in tmp.faces:
            try:
                nf = self.bm.faces.new([vmap[v] for v in f.verts])
            except ValueError:
                continue
            nf.material_index = idx
            nf.smooth = smooth
            nrm = f.normal
            ax = max(range(3), key=lambda i: abs(nrm[i]))
            for ls, ld in zip(f.loops, nf.loops):
                if keep_uv and tuv:
                    ld[self.uv].uv = ls[tuv].uv
                else:
                    c = ls.vert.co
                    uv = (c.y, c.z) if ax == 0 else (c.x, c.z) if ax == 1 else (c.x, c.y)
                    ld[self.uv].uv = (uv[0] / tile, uv[1] / tile)
        tmp.free()

    def tris(self):
        return sum(len(f.verts) - 2 for f in self.bm.faces)


def _bevel(bm, edges, offset, segments):
    if offset > 0 and edges:
        bmesh.ops.bevel(bm, geom=edges, offset=offset, offset_type='OFFSET', segments=segments, profile=.5,
                        affect='EDGES', clamp_overlap=True)


def _segs(part, seg, radius):
    if part.lod == 0:
        return seg
    if part.lod == 1:
        return max(1, seg - 1)
    return 1 if radius >= .012 else 0


def box(part, c, s, mat, bevel=0.0, seg=2, rot=None, tile=.25, smooth=True):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(s), verts=bm.verts)
    sg = _segs(part, seg, bevel)
    if bevel > 0 and sg > 0:
        _bevel(bm, list(bm.edges), bevel, sg)
    m = Matrix.Translation(Vector(c)) @ (rot.to_4x4() if rot is not None else Matrix.Identity(4))
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    part.add(bm, mat, tile, smooth)


def rbox(part, c, s, mat, rv=.02, rh=.008, segv=3, segh=2, axis=2, tile=.25, rot=None):
    """Box with radius rv on the edges parallel to `axis` and rh on the two end perimeters."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(s), verts=bm.verts)
    sv, sh = _segs(part, segv, rv), _segs(part, segh, rh)
    if rv > 0 and sv > 0:
        ev = [e for e in bm.edges if abs((e.verts[0].co - e.verts[1].co).normalized()[axis]) > .99]
        _bevel(bm, ev, rv, sv)
    if rh > 0 and sh > 0:
        h = s[axis] / 2
        eh = [e for e in bm.edges if abs(abs(e.verts[0].co[axis]) - h) < 1e-6 and abs(e.verts[0].co[axis] - e.verts[1].co[axis]) < 1e-6]
        _bevel(bm, eh, rh, sh)
    m = Matrix.Translation(Vector(c)) @ (rot.to_4x4() if rot is not None else Matrix.Identity(4))
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    part.add(bm, mat, tile)


def frame(n, up):
    """Rotation whose local X/Y/Z are right/up/normal (right = up x n, so text reads left to right from outside)."""
    n, up = Vector(n).normalized(), Vector(up).normalized()
    right = up.cross(n).normalized()
    return Matrix((right, up, n)).transposed()


def obox(part, c, n, up, size, mat, bevel=0.0, seg=1, tile=.25):
    box(part, c, size, mat, bevel, seg, frame(n, up), tile)


def cyl(part, p0, p1, r, mat, n=16, r2=None, caps=True, tile=.25, smooth=True):
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=caps, cap_tris=False, segments=part.n(n), radius1=r,
                          radius2=r if r2 is None else r2, depth=d.length)
    m = Matrix.Translation((p0 + p1) / 2) @ Z.rotation_difference(d.normalized()).to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    part.add(bm, mat, tile, smooth)


def lathe(part, origin, axis, prof, mat, n=20, tile=.25, smooth=True):
    """Revolve (distance along axis, radius) pairs around axis through origin."""
    a, o = Vector(axis).normalized(), Vector(origin)
    ref = Z if abs(a.z) < .9 else X
    u = a.cross(ref).normalized()
    v = a.cross(u)
    n = part.n(n)
    bm = bmesh.new()
    rings = []
    for d, r in prof:
        if r < 1e-6:
            rings.append([bm.verts.new(o + a * d)])
        else:
            rings.append([bm.verts.new(o + a * d + (u * math.cos(2 * math.pi * k / n) + v * math.sin(2 * math.pi * k / n)) * r)
                          for k in range(n)])
    for A, B in zip(rings, rings[1:]):
        if len(A) == 1 and len(B) == 1:
            continue
        for k in range(n):
            k2 = (k + 1) % n
            if len(A) == 1:
                bm.faces.new((A[0], B[k2], B[k]))
            elif len(B) == 1:
                bm.faces.new((A[k], A[k2], B[0]))
            else:
                bm.faces.new((A[k], A[k2], B[k2], B[k]))
    if len(rings[0]) > 1:
        bm.faces.new(list(reversed(rings[0])))
    if len(rings[-1]) > 1:
        bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    part.add(bm, mat, tile, smooth)


def fillet(pts, r, steps=4, closed=False):
    pts = [Vector(p) for p in pts]
    m = len(pts)
    out = [] if closed else [pts[0]]
    rng = range(m) if closed else range(1, m - 1)
    for i in rng:
        p0, p1, p2 = pts[i - 1], pts[i], pts[(i + 1) % m]
        d0, d1 = (p0 - p1).normalized(), (p2 - p1).normalized()
        ang = d0.angle(d1)
        t = r / math.tan(ang / 2)
        centre = p1 + (d0 + d1).normalized() * (r / math.sin(ang / 2))
        va, vb = p1 + d0 * t - centre, p1 + d1 * t - centre
        th = va.angle(vb)
        for k in range(steps + 1):
            f = k / steps
            out.append(centre + (va * math.sin((1 - f) * th) + vb * math.sin(f * th)) / math.sin(th))
    if not closed:
        out.append(pts[-1])
    return out


def tube(part, pts, r, mat, n=10, tile=.1, closed=False, caps=True, smooth=True):
    pts = [Vector(p) for p in pts]
    m = len(pts)
    n = part.n(n)
    bm = bmesh.new()
    tang = []
    for i in range(m):
        a = pts[i - 1] if (i > 0 or closed) else pts[i]
        b = pts[(i + 1) % m] if (i < m - 1 or closed) else pts[i]
        tang.append((b - a).normalized())
    ref = Z if abs(tang[0].z) < .9 else X
    nrm = tang[0].cross(ref).normalized()
    rings = []
    for i in range(m):
        t = tang[i]
        if i > 0:
            nrm = tang[i - 1].rotation_difference(t) @ nrm
            nrm = (nrm - t * nrm.dot(t)).normalized()
        b = t.cross(nrm)
        rings.append([bm.verts.new(pts[i] + (nrm * math.cos(2 * math.pi * k / n) + b * math.sin(2 * math.pi * k / n)) * r)
                      for k in range(n)])
    for i in range(m if closed else m - 1):
        A, B = rings[i], rings[(i + 1) % m]
        for k in range(n):
            bm.faces.new((A[k], A[(k + 1) % n], B[(k + 1) % n], B[k]))
    if caps and not closed:
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    part.add(bm, mat, tile, smooth)


def ribbon(part, pts, wdir, w, t, mat, closed=False, tile=.152):
    """Flat strap swept along pts; explicit UVs: u along the strap, one strap = a quarter tile in v."""
    pts = [Vector(p) for p in pts]
    wd = Vector(wdir).normalized()
    m = len(pts)
    bm = bmesh.new()
    uvl = bm.loops.layers.uv.new("UVMap")
    rings, dist, s = [], [], 0.0
    for i in range(m):
        a = pts[i - 1] if (i > 0 or closed) else pts[i]
        b = pts[(i + 1) % m] if (i < m - 1 or closed) else pts[i]
        nr = (b - a).normalized().cross(wd).normalized()
        p = pts[i]
        if i > 0:
            s += (pts[i] - pts[i - 1]).length
        dist.append(s)
        rings.append([bm.verts.new(p + wd * w / 2 + nr * t / 2), bm.verts.new(p - wd * w / 2 + nr * t / 2),
                      bm.verts.new(p - wd * w / 2 - nr * t / 2), bm.verts.new(p + wd * w / 2 - nr * t / 2)])
    vcoord = {0: 0.0, 1: .25, 2: .25, 3: 0.0}
    total = s + (pts[0] - pts[-1]).length
    for i in range(m if closed else m - 1):
        j = (i + 1) % m
        A, B = rings[i], rings[j]
        sa, sb = dist[i], (dist[j] if j > i else total)
        for k in range(4):
            k2 = (k + 1) % 4
            f = bm.faces.new((A[k], A[k2], B[k2], B[k]))
            for loop, (sv, kk) in zip(f.loops, ((sa, k), (sa, k2), (sb, k2), (sb, k))):
                loop[uvl].uv = (sv / tile, vcoord[kk])
    if not closed:
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    part.add(bm, mat, tile, smooth=False, keep_uv=True)


def decal(part, key, c, n, up, w=None, h=None, off=.0009, mat=LABELS):
    """Alpha-clipped label quad from the atlas; give w or h (the other follows the art's aspect)."""
    r = part.atlas[key]
    if w is None:
        w = h * r["aspect"]
    elif h is None:
        h = w / r["aspect"]
    n, up = Vector(n).normalized(), Vector(up).normalized()
    right = up.cross(n).normalized()
    c = Vector(c) + n * off
    bm = bmesh.new()
    uvl = bm.loops.layers.uv.new("UVMap")
    vs = [bm.verts.new(c + right * sx * w / 2 + up * sy * h / 2) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    f = bm.faces.new(vs)
    for loop, uv in zip(f.loops, ((r["u0"], r["v0"]), (r["u1"], r["v0"]), (r["u1"], r["v1"]), (r["u0"], r["v1"]))):
        loop[uvl].uv = uv
    part.add(bm, mat, smooth=False, keep_uv=True)


def cyl_patch(part, mat, xc, w, radius, ang_c, arc_len, axis_y, axis_z, uv_rect=None, uv_tile=None, off=.0009, steps=8):
    """Patch wrapped on a cylinder whose axis is X through (y, z) = (axis_y, axis_z).
    ang_c in degrees from the front (-Y) up towards +Z; arc_len in metres along the surface."""
    steps = max(2, steps // (part.lod + 1))
    rr = radius + off
    half = arc_len / rr / 2
    a0 = math.radians(ang_c) - half
    bm = bmesh.new()
    uvl = bm.loops.layers.uv.new("UVMap")
    rows = []
    for i in range(steps + 1):
        a = a0 + 2 * half * i / steps
        y, z = axis_y - rr * math.cos(a), axis_z + rr * math.sin(a)
        rows.append((bm.verts.new((xc - w / 2, y, z)), bm.verts.new((xc + w / 2, y, z)), i / steps))
    for (l0, r0, t0), (l1, r1, t1) in zip(rows, rows[1:]):
        f = bm.faces.new((l0, r0, r1, l1))
        for loop, (su, tv) in zip(f.loops, ((0, t0), (1, t0), (1, t1), (0, t1))):
            if uv_rect:
                loop[uvl].uv = (uv_rect["u0"] + (uv_rect["u1"] - uv_rect["u0"]) * su, uv_rect["v0"] + (uv_rect["v1"] - uv_rect["v0"]) * tv)
            else:
                loop[uvl].uv = (su * w / uv_tile, tv * arc_len / uv_tile)
    part.add(bm, mat, smooth=True, keep_uv=True)


def cyl_decal(part, key, xc, radius, ang_c, axis_y, axis_z, w=None, h=None):
    r = part.atlas[key]
    if w is None:
        w = h * r["aspect"]
    elif h is None:
        h = w / r["aspect"]
    cyl_patch(part, LABELS, xc, w, radius, ang_c, h, axis_y, axis_z, uv_rect=r)


def bolt(part, c, n, r=.0045, h=.003, mat="CC2_Aluminium"):
    if part.lod > 0:
        return
    lathe(part, c, n, [(0, r), (h, r), (h, 0)], mat, n=6, smooth=False)


def build_object(part, materials, collection):
    me = bpy.data.meshes.new(part.name)
    bmesh.ops.remove_doubles(part.bm, verts=part.bm.verts, dist=1e-6)
    part.bm.to_mesh(me)
    part.bm.free()
    me.use_auto_smooth = True
    me.auto_smooth_angle = math.radians(40)
    for m in part.mats:
        me.materials.append(materials(m))
    ob = bpy.data.objects.new(part.name, me)
    collection.objects.link(ob)
    wn = ob.modifiers.new("WeightedNormal", 'WEIGHTED_NORMAL')
    wn.keep_sharp = True
    wn.weight = 60
    return ob
