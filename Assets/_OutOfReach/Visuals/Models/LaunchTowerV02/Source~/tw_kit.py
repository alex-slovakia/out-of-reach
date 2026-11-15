"""LaunchTowerV02 kit: bmesh primitives written in UNITY metres (x right, y up, z forward).
Vertices are converted to the Blender frame B = (-x, -z, y), the same frame in which the
Blender import of MarinePrologue/Transport/TransportLaunch.fbx shows the existing tower,
so the exported FBX lands in the same Unity frame as the existing transport model.
"""
import bpy, bmesh, math, random
from mathutils import Vector

MATS = ['TW_SteelPainted', 'TW_Galvanized', 'TW_OchrePaint', 'TW_HazardStripe',
        'TW_PipeRed', 'TW_Rubber', 'TW_Grating', 'TW_Labels']
PAINT, GALV, OCHRE, HAZARD, RED, RUBBER, GRATING, LABEL = range(8)
TILE = {PAINT: 2.0, GALV: 1.5, OCHRE: 2.0, HAZARD: 1.2, RED: 2.0, RUBBER: 1.0, GRATING: 0.5, LABEL: 1.0}
random.seed(7)


def B(v):
    return Vector((-v[0], -v[2], v[1]))


def V(*a):
    return Vector(a)


def basis_from(d, up=(0, 1, 0)):
    d = Vector(d).normalized()
    up = Vector(up)
    if abs(d.dot(up)) > 0.95:
        up = Vector((1, 0, 0)) if abs(d.x) < 0.9 else Vector((0, 0, 1))
    ey = (up - d * up.dot(d)).normalized()
    ez = d.cross(ey).normalized()
    return d, ey, ez


class Obj:
    def __init__(self, name, origin_u=(0, 0, 0)):
        self.name = name
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new('UVMap')
        self.auto = self.bm.faces.layers.int.new('auto')
        self.origin = Vector(origin_u)
        self.mat = {}
        self.nface = 0

    # ---- low level -----------------------------------------------------
    def poly(self, pts, mat, center=None, smooth=False, uvs=None, normal=None):
        vs = [self.bm.verts.new(B(p)) for p in pts]
        try:
            f = self.bm.faces.new(vs)
        except ValueError:
            return None
        if center is not None:
            fc = sum((v.co for v in vs), Vector()) / len(vs)
            if f.normal.dot(fc - B(center)) < 0:
                f.normal_flip()
        elif normal is not None:
            if f.normal.dot(B(normal) - B((0, 0, 0))) < 0:
                f.normal_flip()
        f.material_index = mat
        f.smooth = smooth
        if uvs is not None:
            for l, uv in zip(f.loops, uvs):
                l[self.uv].uv = uv
        else:
            f[self.auto] = 1
        self.nface += 1
        return f

    # ---- solids ----------------------------------------------------------
    def box(self, c, ax, hs, mat):
        c = Vector(c)
        ex, ey, ez = [Vector(a) for a in ax]
        hx, hy, hz = hs
        P = {}
        for sx in (-1, 1):
            for sy in (-1, 1):
                for sz in (-1, 1):
                    P[(sx, sy, sz)] = c + ex * (hx * sx) + ey * (hy * sy) + ez * (hz * sz)
        for axis in range(3):
            for s in (-1, 1):
                pts = []
                for a, b in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
                    k = [0, 0, 0]
                    k[axis] = s
                    k[(axis + 1) % 3] = a
                    k[(axis + 2) % 3] = b
                    pts.append(P[tuple(k)])
                self.poly(pts, mat, center=c)

    def aabb(self, mn, mx, mat):
        c = (Vector(mn) + Vector(mx)) / 2
        h = (Vector(mx) - Vector(mn)) / 2
        self.box(c, ((1, 0, 0), (0, 1, 0), (0, 0, 1)), (h.x, h.y, h.z), mat)

    def member(self, p0, p1, w, h, mat, up=(0, 1, 0)):
        p0, p1 = Vector(p0), Vector(p1)
        L = (p1 - p0).length
        if L < 1e-5:
            return
        ex, ey, ez = basis_from(p1 - p0, up)
        self.box((p0 + p1) / 2, (ex, ey, ez), (L / 2, h / 2, w / 2), mat)

    def tube(self, p0, p1, r, mat, seg=10, r1=None, caps=(True, True)):
        p0, p1 = Vector(p0), Vector(p1)
        if (p1 - p0).length < 1e-5:
            return
        d, u, v = basis_from(p1 - p0)
        r1 = r if r1 is None else r1
        ring0, ring1 = [], []
        for i in range(seg):
            a = 2 * math.pi * i / seg
            dirv = u * math.cos(a) + v * math.sin(a)
            ring0.append(self.bm.verts.new(B(p0 + dirv * r)))
            ring1.append(self.bm.verts.new(B(p1 + dirv * r1)))
        mid = (p0 + p1) / 2
        for i in range(seg):
            j = (i + 1) % seg
            try:
                f = self.bm.faces.new((ring0[i], ring0[j], ring1[j], ring1[i]))
            except ValueError:
                continue
            fc = sum((vv.co for vv in f.verts), Vector()) / 4
            ax_pt = B(mid)
            if f.normal.dot(fc - ax_pt) < 0 and False:
                f.normal_flip()
            # outward test against the axis line
            axis_b = (B(p1) - B(p0)).normalized()
            rel = fc - B(p0)
            radial = rel - axis_b * rel.dot(axis_b)
            if f.normal.dot(radial) < 0:
                f.normal_flip()
            f.smooth = True
            f.material_index = mat
            f[self.auto] = 1
            self.nface += 1
        for pt, rr, sgn, on in ((p0, r, -1, caps[0]), (p1, r1, 1, caps[1])):
            if not on or rr < 1e-4:
                continue
            pts = [pt + (u * math.cos(2 * math.pi * i / seg) + v * math.sin(2 * math.pi * i / seg)) * rr for i in range(seg)]
            self.poly(pts, mat, normal=d * sgn)

    def plate(self, origin, ux, vx, pts2d, thick, mat):
        """convex/simple polygon given in the (ux, vx) plane, extruded +-thick/2 along ux x vx."""
        origin, ux, vx = Vector(origin), Vector(ux).normalized(), Vector(vx).normalized()
        n = ux.cross(vx).normalized()
        top = [origin + ux * a + vx * b + n * (thick / 2) for a, b in pts2d]
        bot = [origin + ux * a + vx * b - n * (thick / 2) for a, b in pts2d]
        c = origin
        self.poly(top, mat, center=c + n * 10)
        self.poly(bot, mat, center=c - n * 10)
        k = len(pts2d)
        for i in range(k):
            j = (i + 1) % k
            self.poly([top[i], top[j], bot[j], bot[i]], mat, center=c)

    def rect_plate(self, origin, ux, vx, w, h, thick, mat):
        self.plate(origin, ux, vx, [(-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2), (-w / 2, h / 2)], thick, mat)

    def bolt(self, pos, n, r=0.017, h=0.012, mat=GALV):
        pos, n = Vector(pos), Vector(n).normalized()
        d, u, v = basis_from(n)
        base = [pos + (u * math.cos(math.pi / 3 * i) + v * math.sin(math.pi / 3 * i)) * r for i in range(6)]
        top = [p + n * h for p in base]
        for i in range(6):
            j = (i + 1) % 6
            self.poly([base[i], base[j], top[j], top[i]], mat, center=pos)
        self.poly(top, mat, normal=n)

    def cone(self, p0, p1, r0, r1, mat, seg=10):
        self.tube(p0, p1, r0, mat, seg=seg, r1=r1)

    def quad(self, c, right, up, w, h, mat, uvrect=None):
        """single sided label plate. Text reads left to right along `right` for a viewer in front of the plate;
        the front faces up x right (Unity coordinates, left handed; checked against a Blender back-face-culled render)."""
        c, right, up = Vector(c), Vector(right).normalized(), Vector(up).normalized()
        n = up.cross(right)
        pts = [c + right * (-w / 2) + up * (-h / 2), c + right * (w / 2) + up * (-h / 2),
               c + right * (w / 2) + up * (h / 2), c + right * (-w / 2) + up * (h / 2)]
        u0, v0, u1, v1 = uvrect or (0, 0, 1, 1)
        self.poly(pts, mat, normal=n, uvs=[(u0, v0), (u1, v0), (u1, v1), (u0, v1)])

    # ---- composite helpers -------------------------------------------------
    def tube_path(self, pts, r, mat, bend=0.25, seg=10, bend_segs=4):
        """polyline with rounded bends."""
        pts = [Vector(p) for p in pts]
        out = [pts[0]]
        for i in range(1, len(pts) - 1):
            a, b, c = pts[i - 1], pts[i], pts[i + 1]
            d1, d2 = (b - a).normalized(), (c - b).normalized()
            rad = min(bend, (b - a).length * 0.45, (c - b).length * 0.45)
            if d1.dot(d2) > 0.999:
                out.append(b)
                continue
            p_in, p_out = b - d1 * rad, b + d2 * rad
            out.append(p_in)
            for k in range(1, bend_segs):
                t = k / bend_segs
                q = (1 - t) ** 2 * p_in + 2 * (1 - t) * t * b + t * t * p_out
                out.append(q)
            out.append(p_out)
        out.append(pts[-1])
        for i in range(len(out) - 1):
            self.tube(out[i], out[i + 1], r, mat, seg=seg, caps=(i == 0, i == len(out) - 2))
        return out

    def flange(self, p, d, r, mat=RED, bolts=8, th=0.035):
        d = Vector(d).normalized()
        self.tube(Vector(p) - d * th / 2, Vector(p) + d * th / 2, r, mat, seg=12)
        dd, u, v = basis_from(d)
        for i in range(bolts):
            a = 2 * math.pi * i / bolts
            pos = Vector(p) + d * th / 2 + (u * math.cos(a) + v * math.sin(a)) * (r * 0.78)
            self.bolt(pos, d, r=0.014, h=0.012, mat=GALV)

    # ---- finalize ---------------------------------------------------------
    def finish(self):
        bm = self.bm
        lay = self.auto
        for f in bm.faces:
            if not f[lay]:
                continue
            n = f.normal
            ax = max(range(3), key=lambda i: abs(n[i]))
            t = TILE.get(f.material_index, 1.0)
            ia, ib = [(1, 2), (0, 2), (0, 1)][ax]
            for l in f.loops:
                c = l.vert.co
                l[self.uv].uv = (c[ia] / t, c[ib] / t)
        me = bpy.data.meshes.new(self.name)
        bm.to_mesh(me)
        bm.free()
        ob = bpy.data.objects.new(self.name, me)
        bpy.context.scene.collection.objects.link(ob)
        for m in MATS:
            me.materials.append(bpy.data.materials[m])
        ob.location = B(self.origin)
        if self.origin.length > 0:
            me.transform(__import__('mathutils').Matrix.Translation(-B(self.origin)))
        return ob


def tri_count(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)
