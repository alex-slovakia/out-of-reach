"""LaunchComplexV01: original AURORA launch vehicle (booster stack under the crew capsule) and launch pad dressing.

Blender 3.6, background mode, reproducible:
  blender --background --factory-startup --python build_launch_complex_v01.py -- <LaunchComplexV01 dir> [--export] [--render] [--save]

All layout numbers are UNITY metres (x right, y up, z from the tower towards the flame trench) in the frame of the
"I06 isolated marine prologue" root, the same frame as TransportLaunch.fbx, LS_LandLaunchSite.fbx and LaunchTowerV03.
P(x, y, z) converts to the Blender frame (x, z, y); with the FBX settings below and Unity "Bake Axis Conversion" the
models land at identity in that frame.

Measured existing frame (read only): capsule floor y 0, capsule base ring y -0.55 r 2.40; old BoosterStack r 2.30,
y -31.7..-0.55, EngineEmitter (0, -31.4, 0); pad top y -32.6; launch mount plinths x +-(3.1..4.7), z +-4.6, top y -31.0,
hold-down arms x +-(1.8..3.3), z +-(1.2..1.6), y -31.0..-30.6; flame hole x +-3, z -3..5.5, trench to z 72;
LaunchTowerV03 bounds x -4.07..5.25, z -13.95..-4.18; cable tray (-23,-33)->(-1.4,-33)->(-1.4,-10.6) y -31.3;
pipe rack (34,-38)->(34,-24)->(1.4,-24)->(1.4,-10.6) y -30.35; LOX sphere centre (42,-23.6,-42); water tower base
(-46,-38); flood towers (+-46, 45.7), lamp anchors y -4.
"""
import bpy, bmesh, math, os, sys, json, traceback
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
ROOT = os.path.abspath(argv[0]) if argv else os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
EXPORT = '--export' in argv
RENDER = '--render' in argv
SAVE = '--save' in argv

MATS = ['LC1_HullWhite', 'LC1_HullGraphite', 'LC1_Amber', 'LC1_Red', 'LC1_Steel', 'LC1_Galvanized', 'LC1_Concrete',
        'LC1_ConcreteSoot', 'LC1_Hazard', 'LC1_Nozzle', 'LC1_Foil', 'LC1_Rubber', 'LC1_MarkWhite', 'LC1_GlassDark',
        'LC1_LampWarm', 'LC1_LampRed', 'LC1_LampCyan']
WHITE, GRAPH, AMBER, RED, STEEL, GALV, CONC, SOOT, HAZ, NOZ, FOIL, RUBBER, MARK, GLASS, LWARM, LRED, LCYAN = range(len(MATS))
TILE = {WHITE: 4.0, GRAPH: 2.0, AMBER: 2.0, RED: 2.0, STEEL: 2.0, GALV: 1.5, CONC: 4.0, SOOT: 2.0, HAZ: 1.2, NOZ: 1.0,
        FOIL: 1.5, RUBBER: 1.0, MARK: 2.0, GLASS: 1.0, LWARM: 1.0, LRED: 1.0, LCYAN: 1.0}
PREVIEW = {WHITE: (0.80, 0.80, 0.77), GRAPH: (0.10, 0.105, 0.12), AMBER: (0.80, 0.42, 0.04), RED: (0.45, 0.04, 0.03),
           STEEL: (0.20, 0.215, 0.24), GALV: (0.42, 0.43, 0.44), CONC: (0.36, 0.355, 0.34), SOOT: (0.05, 0.045, 0.04),
           HAZ: (0.5, 0.3, 0.05), NOZ: (0.22, 0.2, 0.26), FOIL: (0.75, 0.5, 0.12), RUBBER: (0.02, 0.02, 0.02),
           MARK: (0.72, 0.72, 0.70), GLASS: (0.02, 0.025, 0.03)}
EMIT = {LWARM: ((1.0, 0.8, 0.55), 12.0), LRED: ((1.0, 0.06, 0.03), 18.0), LCYAN: ((0.2, 0.9, 1.0), 9.0)}

PAD = -32.6
YM = PAD + 0.045      # height of painted markings (clear of LS_Markings at -32.59 and the pad detail decals)
VR = 2.30             # core radius of the launch vehicle


def P(x, y, z):
    return Vector((x, z, y))


def U(v):
    return (v.x, v.z, v.y)


_GLYPHS = {}


def glyph_tris(s, step, ystep=None):
    """Triangles of the text `s` (Blender bundled font, cap height about 0.7 at size 1), centred, optionally cut every
    `step` along x so the letters can follow a curved surface."""
    key = (s, step, ystep)
    if key in _GLYPHS:
        return _GLYPHS[key]
    cu = bpy.data.curves.new('lc1_txt', 'FONT')
    cu.body = s
    cu.size = 1.0
    cu.align_x = 'CENTER'
    cu.align_y = 'CENTER'
    cu.resolution_u = 3
    ob = bpy.data.objects.new('lc1_txt', cu)
    bpy.context.scene.collection.objects.link(ob)
    bpy.context.view_layer.update()
    me = bpy.data.meshes.new_from_object(ob.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    bm = bmesh.new()
    bm.from_mesh(me)
    if step:
        xs = [v.co.x for v in bm.verts]
        x = min(xs) + step
        while x < max(xs):
            bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(x, 0, 0), plane_no=(1, 0, 0))
            x += step
    if ystep:
        ys = [v.co.y for v in bm.verts]
        y = min(ys) + ystep
        while y < max(ys):
            bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(0, y, 0), plane_no=(0, 1, 0))
            y += ystep
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    tris = [[(l.vert.co.x, l.vert.co.y) for l in f.loops] for f in bm.faces]
    bm.free()
    bpy.data.objects.remove(ob)
    bpy.data.meshes.remove(me)
    bpy.data.curves.remove(cu)
    _GLYPHS[key] = tris
    return tris


class Mesh:
    def __init__(self, name, lod=0):
        self.name = name
        self.lod = lod
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new('UVMap')
        self.fuv = self.bm.faces.layers.int.new('uvset')
        self.xf = None

    def sg(self, n, lo=6):
        """Segment count of a round section at this LOD."""
        return max(lo, int(n) >> self.lod)

    def at(self, x=0.0, y=0.0, z=0.0, yaw=0.0):
        """Local frame: origin (x, y, z), local +z turned `yaw` degrees from world +z towards +x."""
        h = math.radians(yaw)
        self.xf = (x, y, z, math.cos(h), math.sin(h))
        return self

    def world(self):
        self.xf = None

    def pt(self, x, y, z):
        if self.xf:
            ox, oy, oz, c, s = self.xf
            x, y, z = ox + x * c + z * s, oy + y, oz - x * s + z * c
        return Vector((x, z, y))

    def dr(self, x, y, z):
        if self.xf:
            _, _, _, c, s = self.xf
            x, z = x * c + z * s, -x * s + z * c
        return Vector((x, z, y))

    # -- primitives (inputs in Unity metres, in the current local frame) --------------------------------------
    def face(self, pts, mat, want=None, smooth=False, uvs=None):
        try:
            f = self.bm.faces.new([self.bm.verts.new(p) for p in pts])
        except ValueError:
            return None
        f.material_index = mat
        f.smooth = smooth
        if want is not None:
            f.normal_update()
            if f.normal.dot(want) < 0:
                f.normal_flip()
        if uvs:
            f[self.fuv] = 1
            for l, t in zip(f.loops, uvs):
                l[self.uv].uv = t
        return f

    def _box8(self, corners, mat):
        vs = [self.bm.verts.new(c) for c in corners]
        idx = [(0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]
        fs = []
        for q in idx:
            try:
                f = self.bm.faces.new([vs[i] for i in q])
                f.material_index = mat
                fs.append(f)
            except ValueError:
                pass
        bmesh.ops.recalc_face_normals(self.bm, faces=fs)
        return fs

    def boxr(self, x0, x1, y0, y1, z0, z1, mat):
        x0, x1 = min(x0, x1), max(x0, x1)
        y0, y1 = min(y0, y1), max(y0, y1)
        z0, z1 = min(z0, z1), max(z0, z1)
        p = self.pt
        return self._box8([p(x0, y0, z0), p(x1, y0, z0), p(x1, y0, z1), p(x0, y0, z1),
                           p(x0, y1, z0), p(x1, y1, z0), p(x1, y1, z1), p(x0, y1, z1)], mat)

    def box(self, c, s, mat):
        return self.boxr(c[0] - s[0] / 2, c[0] + s[0] / 2, c[1] - s[1] / 2, c[1] + s[1] / 2, c[2] - s[2] / 2, c[2] + s[2] / 2, mat)

    def taper(self, c0, s0, c1, s1, mat):
        """Frustum box: bottom rectangle centre c0 size s0 (x, z), top rectangle centre c1 size s1."""
        p = self.pt
        (x0, y0, z0), (a0, b0) = c0, (s0[0] / 2, s0[1] / 2)
        (x1, y1, z1), (a1, b1) = c1, (s1[0] / 2, s1[1] / 2)
        return self._box8([p(x0 - a0, y0, z0 - b0), p(x0 + a0, y0, z0 - b0), p(x0 + a0, y0, z0 + b0), p(x0 - a0, y0, z0 + b0),
                           p(x1 - a1, y1, z1 - b1), p(x1 + a1, y1, z1 - b1), p(x1 + a1, y1, z1 + b1), p(x1 - a1, y1, z1 + b1)], mat)

    def beam(self, a, b, w, h, mat, up=(0, 1, 0)):
        """Rectangular member from a to b: width w across, height h along `up`."""
        A, Bv = self.pt(*a), self.pt(*b)
        d = Bv - A
        if d.length < 1e-5:
            return []
        d.normalize()
        upv = self.dr(*up)
        if abs(d.dot(upv)) > 0.97:
            upv = Vector((1, 0, 0)) if abs(d.x) < 0.9 else Vector((0, 1, 0))
        side = d.cross(upv).normalized()
        upv = side.cross(d).normalized()
        sw, sh = side * (w / 2), upv * (h / 2)
        return self._box8([A - sw - sh, A + sw - sh, A + sw + sh, A - sw + sh, Bv - sw - sh, Bv + sw - sh, Bv + sw + sh, Bv - sw + sh], mat)

    def tube(self, a, b, r, mat, seg=8, caps=True, r2=None):
        A, Bv = self.pt(*a), self.pt(*b)
        d = Bv - A
        if d.length < 1e-5:
            return
        dn = d.normalized()
        ref = Vector((0, 0, 1)) if abs(dn.z) < 0.9 else Vector((1, 0, 0))
        e1 = dn.cross(ref).normalized()
        e2 = dn.cross(e1).normalized()
        r2 = r if r2 is None else r2
        ra, rb = [], []
        for i in range(seg):
            t = 2 * math.pi * i / seg
            o = e1 * math.cos(t) + e2 * math.sin(t)
            ra.append(self.bm.verts.new(A + o * r))
            rb.append(self.bm.verts.new(Bv + o * r2))
        fs = []
        for i in range(seg):
            j = (i + 1) % seg
            f = self.bm.faces.new([ra[i], ra[j], rb[j], rb[i]])
            f.material_index = mat
            f.smooth = True
            fs.append(f)
        if caps:
            for ring in (ra, rb):
                f = self.bm.faces.new(ring)
                f.material_index = mat
                fs.append(f)
        bmesh.ops.recalc_face_normals(self.bm, faces=fs)

    def path(self, pts, r, mat, seg=8):
        """Pipe / cable run through pts with ball joints at the bends."""
        for i in range(len(pts) - 1):
            self.tube(pts[i], pts[i + 1], r, mat, seg, caps=(i == 0 or i == len(pts) - 2))
        if self.lod == 0:
            for p in pts[1:-1]:
                self.ball(p, r * 1.04, mat, seg)

    def lathe(self, prof, seg, o=(0, 0, 0), ax=(0, 1, 0), uv='cyl', flip=False, a0=None, a1=None, urep=None):
        """Surface of revolution about `ax` through `o`. prof = [(r, h, mat), ...]; mat belongs to the segment that
        starts at that point (None = no faces). Normals: a profile that climbs along the axis faces outwards, one that
        runs outwards faces -axis, inwards faces +axis. a0/a1 (radians, x->z) make a partial arc."""
        O = self.pt(*o)
        A = self.dr(*ax).normalized()
        e1 = self.dr(1, 0, 0) if abs(A.dot(self.dr(1, 0, 0))) < 0.9 else self.dr(0, 1, 0)
        e1 = (e1 - A * e1.dot(A)).normalized()
        e2 = A.cross(e1)
        full = a0 is None
        n = seg if full else seg + 1
        angs = [(2 * math.pi * i / seg) if full else (a0 + (a1 - a0) * i / seg) for i in range(n)]
        rings = []
        for (r, h, _m) in prof:
            if r < 1e-6:
                rings.append([self.bm.verts.new(O + A * h)])
            else:
                rings.append([self.bm.verts.new(O + A * h + (e1 * math.cos(t) + e2 * math.sin(t)) * r) for t in angs])
        s = [0.0]
        for k in range(1, len(prof)):
            s.append(s[-1] + math.hypot(prof[k][0] - prof[k - 1][0], prof[k][1] - prof[k - 1][1]))
        rmax = max(p[0] for p in prof)
        for k in range(len(prof) - 1):
            mat = prof[k][2]
            ra, rb = rings[k], rings[k + 1]
            if mat is None or (len(ra) == 1 and len(rb) == 1):
                continue
            nu = urep if urep else max(1, round(2 * math.pi * rmax / TILE[mat]))
            if uv == 'unit':
                v0, v1 = s[k] / s[-1], s[k + 1] / s[-1]
            else:
                v0, v1 = s[k] / TILE[mat], s[k + 1] / TILE[mat]
            for i in range(seg):
                j = (i + 1) % n
                u0, u1 = nu * i / seg, nu * (i + 1) / seg
                if len(ra) == 1:
                    vs, uvs = [ra[0], rb[j], rb[i]], [((u0 + u1) / 2, v0), (u1, v1), (u0, v1)]
                elif len(rb) == 1:
                    vs, uvs = [ra[i], ra[j], rb[0]], [(u0, v0), (u1, v0), ((u0 + u1) / 2, v1)]
                else:
                    vs, uvs = [ra[i], ra[j], rb[j], rb[i]], [(u0, v0), (u1, v0), (u1, v1), (u0, v1)]
                if flip:
                    vs, uvs = vs[::-1], uvs[::-1]
                try:
                    f = self.bm.faces.new(vs)
                except ValueError:
                    continue
                f.material_index = mat
                f.smooth = True
                f[self.fuv] = 1
                for l, t in zip(f.loops, uvs):
                    l[self.uv].uv = t

    def ball(self, c, r, mat, seg=8):
        self.lathe([(0, -r, mat), (r * 0.71, -r * 0.71, mat), (r, 0, mat), (r * 0.71, r * 0.71, mat), (0, r, mat)], seg, o=c)

    def ringbar(self, o, r0, r1, y0, y1, mat, seg, ax=(0, 1, 0)):
        """Closed rectangular ring (flange, girder, walkway)."""
        self.lathe([(r0, y0, mat), (r1, y0, mat), (r1, y1, mat), (r0, y1, mat), (r0, y0, mat)], seg, o=o, ax=ax)

    def text(self, s, fn, nf, mat, scale=1.0, step=None, ystep=None):
        """Lettering as geometry. fn(gx, gy) -> Blender point, nf(point) -> wanted outward normal."""
        for tri in glyph_tris(s, (step / scale) if step else None, (ystep / scale) if ystep else None):
            pts = [fn(x * scale, y * scale) for (x, y) in tri]
            c = (pts[0] + pts[1] + pts[2]) / 3
            self.face(pts, mat, want=nf(c))

    def gquad(self, x0, x1, z0, z1, mat, y=YM):
        p = self.pt
        self.face([p(x0, y, z0), p(x1, y, z0), p(x1, y, z1), p(x0, y, z1)], mat, want=Vector((0, 0, 1)))

    def gline(self, a, b, w, mat, y=YM):
        """Painted line on the pad from a=(x, z) to b=(x, z)."""
        dx, dz = b[0] - a[0], b[1] - a[1]
        L = math.hypot(dx, dz)
        nx, nz = -dz / L * w / 2, dx / L * w / 2
        p = self.pt
        self.face([p(a[0] - nx, y, a[1] - nz), p(a[0] + nx, y, a[1] + nz), p(b[0] + nx, y, b[1] + nz), p(b[0] - nx, y, b[1] - nz)],
                  mat, want=Vector((0, 0, 1)))

    def gdash(self, a, b, w, mat, dash=1.2, gap=0.8):
        L = math.hypot(b[0] - a[0], b[1] - a[1])
        n = max(1, int(L / (dash + gap)))
        for i in range(n):
            t0, t1 = i * (dash + gap) / L, min(1.0, (i * (dash + gap) + dash) / L)
            self.gline((a[0] + (b[0] - a[0]) * t0, a[1] + (b[1] - a[1]) * t0), (a[0] + (b[0] - a[0]) * t1, a[1] + (b[1] - a[1]) * t1), w, mat)

    def garc(self, c, r0, r1, a0, a1, mat, seg, y=YM):
        p = self.pt
        for i in range(seg):
            t0, t1 = a0 + (a1 - a0) * i / seg, a0 + (a1 - a0) * (i + 1) / seg
            self.face([p(c[0] + r0 * math.cos(t0), y, c[1] + r0 * math.sin(t0)), p(c[0] + r1 * math.cos(t0), y, c[1] + r1 * math.sin(t0)),
                       p(c[0] + r1 * math.cos(t1), y, c[1] + r1 * math.sin(t1)), p(c[0] + r0 * math.cos(t1), y, c[1] + r0 * math.sin(t1))],
                      mat, want=Vector((0, 0, 1)))

    def rail(self, a, b, mat=AMBER, h=1.07, spacing=1.5):
        """Guard rail along the floor line a->b: top and mid rail, posts, toe board at LOD0."""
        L = math.dist(a, b)
        n = max(1, int(math.ceil(L / spacing)))
        self.tube((a[0], a[1] + h, a[2]), (b[0], b[1] + h, b[2]), 0.026, mat, 6 if self.lod == 0 else 4)
        if self.lod == 0:
            self.tube((a[0], a[1] + h * 0.52, a[2]), (b[0], b[1] + h * 0.52, b[2]), 0.02, mat, 5)
            self.beam((a[0], a[1] + 0.06, a[2]), (b[0], b[1] + 0.06, b[2]), 0.01, 0.12, mat)
        for i in range(n + 1 if self.lod == 0 else 2):
            t = i / n if self.lod == 0 else float(i)
            x, y, z = a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t
            self.beam((x, y, z), (x, y + h, z), 0.045, 0.045, mat, up=(1, 0, 0))

    def lattice(self, cx, cz, y0, y1, w0, w1, panels, leg, brace, mat, cross=False, bands=None):
        """Square tapered lattice mast: 4 legs, zig-zag (or X) diagonals and horizontals on every face.
        bands = ((matA, matB), panels_per_band) paints aviation bands instead of one colour."""
        def corner(k, y):
            w = (w0 + (w1 - w0) * (y - y0) / (y1 - y0)) / 2
            sx, sz = [(-1, -1), (1, -1), (1, 1), (-1, 1)][k % 4]
            return (cx + sx * w, y, cz + sz * w)
        ys = [y0 + (y1 - y0) * i / panels for i in range(panels + 1)]

        def pm(i):
            return mat if not bands else bands[0][(i // bands[1]) % len(bands[0])]
        for k in range(4):
            if bands:
                for i0 in range(0, panels, bands[1]):
                    self.beam(corner(k, ys[i0]), corner(k, ys[min(panels, i0 + bands[1])]), leg, leg, pm(i0), up=(1, 0, 0))
            else:
                self.beam(corner(k, y0), corner(k, y1), leg, leg, mat, up=(1, 0, 0))
            for i in range(panels):
                lo, hi = ys[i], ys[i + 1]
                a, b = (corner(k, lo), corner(k + 1, hi)) if (i + k) % 2 == 0 else (corner(k + 1, lo), corner(k, hi))
                self.beam(a, b, brace, brace, pm(i))
                if cross and self.lod == 0:
                    a, b = (corner(k + 1, lo), corner(k, hi)) if (i + k) % 2 == 0 else (corner(k, lo), corner(k + 1, hi))
                    self.beam(a, b, brace, brace, pm(i))
                if self.lod == 0 or i % 2 == 1:
                    self.beam(corner(k, hi), corner(k + 1, hi), brace, brace, pm(i))

    # -- finish ------------------------------------------------------------------------------------------------
    def finish(self):
        bm = self.bm
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0001)
        uv, fuv = self.uv, self.fuv
        for f in bm.faces:
            if f[fuv]:
                continue
            n = f.normal
            ax = max(range(3), key=lambda i: abs(n[i]))
            t = TILE[f.material_index]
            for l in f.loops:
                co = l.vert.co
                if ax == 0:
                    u, v = co.y, co.z
                elif ax == 1:
                    u, v = co.x, co.z
                else:
                    u, v = co.x, co.y
                l[uv].uv = (u / t, v / t)
        me = bpy.data.meshes.new(self.name)
        bm.to_mesh(me)
        bm.free()
        me.use_auto_smooth = True
        me.auto_smooth_angle = math.radians(40)
        for m in MATS:
            me.materials.append(bpy.data.materials[m])
        ob = bpy.data.objects.new(self.name, me)
        bpy.context.scene.collection.objects.link(ob)
        return ob


def make_materials():
    for i, name in enumerate(MATS):
        m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        m.use_nodes = True
        bsdf = m.node_tree.nodes.get('Principled BSDF')
        if i in EMIT:
            col, st = EMIT[i]
            bsdf.inputs['Base Color'].default_value = (*col, 1)
            bsdf.inputs['Emission'].default_value = (*col, 1)
            bsdf.inputs['Emission Strength'].default_value = st
            continue
        bsdf.inputs['Base Color'].default_value = (*PREVIEW[i], 1)
        bsdf.inputs['Roughness'].default_value = 0.35 if i in (NOZ, FOIL, GLASS) else 0.6
        bsdf.inputs['Metallic'].default_value = 0.7 if i in (NOZ, FOIL, GALV) else 0.0


# =====================================================================================================================
# LAUNCH VEHICLE (moves with "Transport vehicle - root controls launch")
# =====================================================================================================================
def cyl_map(theta0, yc, r):
    def fn(gx, gy):
        t = theta0 + gx / r
        return P(r * math.cos(t), yc + gy, r * math.sin(t))

    def nf(p):
        return Vector((p.x, p.y, 0))
    return fn, nf


def nozzle(m, x, z, re, seg, inner):
    y_t, y_e, rt = -29.95, -31.62, 0.17

    def r_at(y):
        return rt + (re - rt) * ((y_t - y) / (y_t - y_e)) ** 0.6
    ys = [-31.62, -31.42, -31.18, -30.94, -30.70, -30.46] if m.lod < 2 else [-31.62, -31.1, -30.46]
    m.lathe([(r_at(y), y, NOZ) for y in ys], seg, o=(x, 0, z), uv='unit', urep=4)
    if inner:
        m.lathe([(r_at(y) - 0.03, y, NOZ) for y in ys], seg, o=(x, 0, z), uv='unit', urep=4, flip=True)
        m.lathe([(r_at(ys[0]) - 0.03, ys[0], NOZ), (r_at(ys[0]), ys[0], NOZ)], seg, o=(x, 0, z), uv='unit', urep=4)
        m.lathe([(0.0, -30.53, RUBBER), (r_at(-30.46), -30.53, RUBBER)], seg, o=(x, 0, z))
    if m.lod == 0:
        for yb in (-31.5, -31.05, -30.7):
            m.ringbar((x, 0, z), r_at(yb) - 0.005, r_at(yb) + 0.035, yb - 0.03, yb + 0.03, GALV, seg)


def vehicle(m):
    D = m.lod == 0
    S = m.sg(48, 12)
    prof = [
        (0.00, -30.50, FOIL), (2.37, -30.50, STEEL), (2.37, -30.20, STEEL), (2.32, -30.12, GRAPH),      # base, hold-down ring
        (2.32, -27.72, AMBER), (2.345, -27.68, AMBER), (2.345, -27.44, AMBER), (2.30, -27.40, WHITE),   # aft skirt, amber ring
        (2.30, -20.60, GRAPH), (2.325, -20.56, GRAPH), (2.325, -18.44, GRAPH), (2.30, -18.40, WHITE),   # fuel tank, intertank
        (2.30, -12.30, GRAPH), (2.325, -12.26, GRAPH), (2.325, -9.96, AMBER),                            # LOX tank, interstage
        (2.35, -9.92, AMBER), (2.35, -9.72, AMBER), (2.30, -9.68, WHITE),                                # separation ring
        (2.30, -4.62, GRAPH), (2.318, -4.58, FOIL), (2.318, -3.74, GRAPH), (2.318, -3.50, WHITE),           # second stage, bay
        (2.30, -3.46, WHITE), (2.54, -2.98, WHITE), (2.54, -1.02, GRAPH), (2.54, -0.78, GRAPH),          # spacecraft adapter
        (2.40, -0.55, GRAPH), (0.00, -0.55, None)]
    if m.lod == 2:
        prof = [p for i, p in enumerate(prof) if i in (0, 1, 3, 4, 7, 8, 11, 12, 14, 17, 18, 22, 23, 24, 26, 27)]
    m.lathe(prof, S)

    # engines: centre + four on a 1.38 m circle
    ns = m.sg(24, 8)
    nozzle(m, 0, 0, 0.62, ns, m.lod < 2)
    for k in range(4):
        a = math.radians(45 + 90 * k)
        nozzle(m, 1.38 * math.cos(a), 1.38 * math.sin(a), 0.60, ns, m.lod < 2)
        if D:   # turbine exhaust duct beside each outer engine
            b = a + 0.42
            m.tube((1.9 * math.cos(b), -30.5, 1.9 * math.sin(b)), (1.98 * math.cos(b), -31.25, 1.98 * math.sin(b)), 0.085, NOZ, 8)

    # fins on the diagonals
    for k in range(4):
        a = math.radians(45 + 90 * k)
        cr, sr = math.cos(a), math.sin(a)

        def q(rad, y, t):
            return P(rad * cr - t * sr, y, rad * sr + t * cr)
        m._box8([q(2.29, -30.30, -0.09), q(3.74, -30.30, -0.035), q(3.74, -30.30, 0.035), q(2.29, -30.30, 0.09),
                 q(2.29, -26.85, -0.07), q(3.74, -29.05, -0.03), q(3.74, -29.05, 0.03), q(2.29, -26.85, 0.07)], GRAPH)
        if m.lod < 2:
            m._box8([q(3.70, -30.32, -0.05), q(3.80, -30.32, -0.045), q(3.80, -30.32, 0.045), q(3.70, -30.32, 0.05),
                     q(3.70, -28.98, -0.045), q(3.80, -29.12, -0.04), q(3.80, -29.12, 0.04), q(3.70, -28.98, 0.045)], AMBER)
    if m.lod == 2:
        return

    # systems tunnels (tower side and trench side), split at the separation plane
    for sz in (-1, 1):
        zc = sz * (VR + 0.06)
        for (y0, y1) in ((-29.9, -10.05), (-9.6, -3.6)):
            m.beam((0, y0, zc), (0, y1, zc), 0.36, 0.13, WHITE, up=(0, 0, 1))
            if D:
                m.taper((0, y1, zc), (0.36, 0.13), (0, y1 + 0.3, zc - sz * 0.05), (0.2, 0.03), WHITE)
                for i in range(int((y1 - y0) / 2.4)):
                    yy = y0 + 1.2 + i * 2.4
                    m.boxr(-0.2, 0.2, yy - 0.03, yy + 0.03, zc - 0.075, zc + 0.075, GRAPH)
    # LOX feedline on the trench side with brackets
    th = math.radians(118)
    fx, fz = (VR + 0.27) * math.cos(th), (VR + 0.27) * math.sin(th)
    hx, hz = (VR - 0.02) * math.cos(th), (VR - 0.02) * math.sin(th)
    m.path([(hx, -18.9, hz), (fx, -19.25, fz), (fx, -27.2, fz), (hx, -27.6, hz)], 0.17, FOIL, m.sg(12, 6))
    if D:
        for i in range(6):
            yy = -20.2 - i * 1.3
            m.beam((hx, yy, hz), (fx, yy, fz), 0.42, 0.06, GALV)

    # roll marks on the fuel tank, vents on the interstage
    for a0 in (20, 200):
        m.lathe([(VR + 0.008, -27.3, GRAPH), (VR + 0.008, -26.1, GRAPH)], m.sg(8, 3), a0=math.radians(a0), a1=math.radians(a0 + 50))
    for k in range(8):
        a = math.radians(22.5 + 45 * k)
        m.lathe([(2.332, -11.75, RUBBER), (2.332, -10.6, RUBBER)], 2, a0=a - 0.11, a1=a + 0.11)

    # lettering: AURORA down the LOX tank and a 7 on the fuel tank, both flanks
    for th0 in (0.0, math.pi):
        for i, ch in enumerate('AURORA'):
            m.text(ch, *cyl_map(th0, -12.98 - i * 0.95, VR + (0.012 if D else 0.03)), GRAPH, scale=1.12, step=0.22 if D else 0.45)
        m.text('7', *cyl_map(th0, -23.9, VR + (0.012 if D else 0.03)), GRAPH, scale=3.0, step=0.22 if D else 0.45)

    # hold-down lugs under the base ring (they sit on the pad's hold-down arms)
    for sx in (-1, 1):
        for sz in (-1, 1):
            m.boxr(sx * 1.74, sx * 2.06, -30.56, -30.3, sz * 1.4 - 0.2, sz * 1.4 + 0.2, STEEL)
    if not D:
        return

    # close-up dressing near the crew access arm: RCS pods, umbilical plates, doors, separation fittings
    for k in range(4):
        a = math.radians(90 * k)
        cr, sr = math.cos(a), math.sin(a)
        m.at(VR * cr, 0, VR * sr, math.degrees(math.atan2(cr, sr)))      # local +z = outwards
        m.boxr(-0.3, 0.3, -4.2, -3.82, -0.05, 0.16, GRAPH)
        for dx in (-0.17, 0.0, 0.17):
            m.tube((dx, -4.01, 0.16), (dx, -4.01, 0.25), 0.035, NOZ, 6, r2=0.055)
        m.tube((0, -4.2, 0.08), (0, -4.32, 0.1), 0.035, NOZ, 6, r2=0.055)
        m.world()
    for (deg, y) in ((-78, -5.4), (-102, -5.4), (-78, -8.2), (-102, -16.8), (0, -29.3), (180, -29.3)):
        a = math.radians(deg)
        cr, sr = math.cos(a), math.sin(a)
        m.at((VR + 0.005) * cr, y, (VR + 0.005) * sr, math.degrees(math.atan2(cr, sr)))
        m.boxr(-0.3, 0.3, -0.4, 0.4, -0.03, 0.03, STEEL)
        for (x0, x1, y0, y1) in ((-0.36, 0.36, 0.4, 0.46), (-0.36, 0.36, -0.46, -0.4), (-0.36, -0.3, -0.4, 0.4), (0.3, 0.36, -0.4, 0.4)):
            m.boxr(x0, x1, y0, y1, -0.03, 0.045, HAZ)
        for (cx, cy, rr) in ((-0.13, 0.18, 0.07), (0.13, 0.18, 0.07), (0.0, -0.12, 0.1)):
            m.tube((cx, cy, 0.03), (cx, cy, 0.09), rr, GALV, 8)
        m.boxr(-0.2, 0.2, 0.52, 0.56, 0.0, 0.03, LCYAN)
        m.world()
    for (deg, y, w, h) in ((40, -6.4, 0.7, 1.0), (140, -6.4, 0.7, 1.0), (-140, -15.2, 0.8, 1.1), (60, -25.0, 0.8, 1.1), (-60, -28.9, 0.9, 0.8)):
        a = math.radians(deg)
        for (w2, h2, off, mat) in ((w, h, 0.004, GRAPH), (w - 0.07, h - 0.07, 0.009, WHITE)):
            m.lathe([(VR + off + (0.02 if y < -27.7 else 0.0), y - h2 / 2, mat), (VR + off + (0.02 if y < -27.7 else 0.0), y + h2 / 2, mat)], 3,
                    a0=a - w2 / 2 / VR, a1=a + w2 / 2 / VR)
    for k in range(8):                       # separation fittings + adapter panel seams
        a = math.radians(22.5 + 45 * k)
        cr, sr = math.cos(a), math.sin(a)
        m.at(2.54 * cr, 0, 2.54 * sr, math.degrees(math.atan2(cr, sr)))
        m.taper((0, -1.32, 0.0), (0.26, 0.02), (0, -1.02, 0.03), (0.2, 0.1), AMBER)
        m.boxr(-0.012, 0.012, -2.9, -1.34, -0.004, 0.008, GRAPH)
        m.world()
    for (deg, y) in ((25, -2.0), (155, -2.0)):   # antenna blisters
        a = math.radians(deg)
        m.ball(((2.54 + 0.02) * math.cos(a), y, (2.54 + 0.02) * math.sin(a)), 0.16, WHITE, 8)


# =====================================================================================================================
# PAD DRESSING (stationary, under "Launch site - stationary")
# =====================================================================================================================
def mount(m):
    D = m.lod == 0
    for sx in (-1, 1):
        m.boxr(sx * 3.08, sx * 4.72, PAD, -31.0, -4.62, 4.62, CONC)
        m.boxr(sx * 3.06, sx * 4.74, -31.0, -30.955, -4.64, 4.64, STEEL)
        m.boxr(sx * 3.02, sx * 3.2, -30.99, -30.94, -3.4, 4.64, HAZ)
        m.boxr(sx * 2.93, sx * 3.0, -36.6, -31.0, -3.0, 4.62, SOOT)                   # scorched lining of the flame hole
        m.boxr(sx * 2.93, sx * 3.0, -36.6, PAD - 0.04, 4.62, 6.0, SOOT)
        for sz in (-1, 1):
            zc = sz * 1.4
            m.boxr(sx * 1.74, sx * 3.45, -31.0, -30.6, zc - 0.27, zc + 0.27, STEEL)   # hold-down arm
            m.boxr(sx * 2.46, sx * 2.9, -30.6, -30.1, zc - 0.21, zc + 0.21, AMBER)    # clamp housing outside the base ring
            m._box8([m.pt(sx * 1.9, -31.0, zc - 0.2), m.pt(sx * 3.05, -31.0, zc - 0.2), m.pt(sx * 3.05, -31.0, zc + 0.2), m.pt(sx * 1.9, -31.0, zc + 0.2),
                     m.pt(sx * 2.95, -31.9, zc - 0.2), m.pt(sx * 3.05, -31.9, zc - 0.2), m.pt(sx * 3.05, -31.9, zc + 0.2), m.pt(sx * 2.95, -31.9, zc + 0.2)], STEEL)
            if D:
                m.tube((sx * 3.42, -30.74, zc), (sx * 2.9, -30.36, zc), 0.075, GALV, 8)
                m.boxr(sx * 3.3, sx * 3.62, -30.955, -30.6, zc - 0.17, zc + 0.17, GRAPH)
        # tail service mast with its umbilical carrier (0.3 m off the aft skirt)
        m.taper((sx * 3.96, -30.955, 0.0), (1.1, 1.3), (sx * 3.9, -27.95, 0.0), (0.84, 1.0), STEEL)
        m.boxr(sx * 3.34, sx * 4.58, -30.955, -30.7, -0.74, 0.74, AMBER)
        m.boxr(sx * 3.44, sx * 4.36, -27.95, -27.82, -0.56, 0.56, AMBER)
        m.boxr(sx * 2.72, sx * 3.5, -29.72, -29.05, -0.4, 0.4, GRAPH)
        m.boxr(sx * 2.65, sx * 2.72, -29.78, -29.0, -0.46, 0.46, HAZ)
        if D:
            for zc in (-0.24, 0.0, 0.24):
                m.path([(sx * 3.48, -28.25, zc), (sx * 3.1, -28.55, zc), (sx * 2.9, -29.0, zc), (sx * 2.74, -29.3, zc)], 0.05, RUBBER, 6)
            m.boxr(sx * 3.41, sx * 3.44, -28.9, -28.7, -0.3, 0.3, LCYAN)
            m.boxr(sx * 3.8, sx * 4.1, -27.82, -27.62, -0.12, 0.12, LRED)
        # rainbird deluge heads aimed at the flame hole
        for zr in (3.7, -2.6):
            sg = m.sg(10, 6)
            m.path([(sx * 4.15, -30.96, zr), (sx * 4.15, -29.9, zr), (sx * 3.6, -29.55, zr * 0.82)], 0.15, RED, sg)
            m.tube((sx * 3.6, -29.55, zr * 0.82), (sx * 3.28, -29.38, zr * 0.74), 0.15, RED, sg, r2=0.34)
            if D:
                m.ringbar((sx * 4.15, 0, zr), 0.15, 0.24, -30.6, -30.52, GALV, sg)
        # guard rail, landing and stair on the outer side
        xo = sx * 4.7
        m.rail((xo, -30.955, -4.6), (xo, -30.955, 1.0))
        m.rail((xo, -30.955, 2.2), (xo, -30.955, 4.6))
        m.rail((sx * 3.2, -30.955, 4.6), (xo, -30.955, 4.6))
        m.boxr(xo, xo + sx * 0.95, -31.0, -30.955, 1.0, 2.2, GALV)
        m.rail((xo + sx * 0.93, -30.955, 1.0), (xo + sx * 0.93, -30.955, 2.2))
        m.rail((xo, -30.955, 1.0), (xo + sx * 0.93, -30.955, 1.0))
        m.beam((xo + sx * 0.04, -31.06, 2.2), (xo + sx * 0.04, PAD + 0.02, 4.75), 0.05, 0.24, STEEL)
        m.beam((xo + sx * 0.91, -31.06, 2.2), (xo + sx * 0.91, PAD + 0.02, 4.75), 0.05, 0.24, STEEL)
        if D:
            for i in range(8):
                yy = -30.955 - (i + 1) * (1.645 / 9)
                m.boxr(xo + sx * 0.06, xo + sx * 0.89, yy - 0.04, yy, 2.2 + i * 0.3, 2.2 + i * 0.3 + 0.27, GALV)
            m.tube((xo + sx * 0.93, -29.95, 2.2), (xo + sx * 0.93, PAD + 1.0, 4.7), 0.026, AMBER, 6)
            for t in (0.33, 0.66, 1.0):
                m.beam((xo + sx * 0.93, -30.955 - 1.645 * t, 2.2 + 2.5 * t), (xo + sx * 0.93, -29.95 - 1.645 * t, 2.2 + 2.5 * t), 0.045, 0.045, AMBER, up=(1, 0, 0))
    # back wall, fire-water header
    m.boxr(-4.72, 4.72, PAD, -31.5, -4.62, -3.4, CONC)
    m.boxr(-4.74, 4.74, -31.5, -31.46, -4.64, -3.38, STEEL)
    m.boxr(-3.0, 3.0, -35.0, -31.5, -3.06, -2.99, SOOT)
    m.tube((-5.0, -31.22, -4.0), (4.5, -31.22, -4.0), 0.2, RED, m.sg(12, 6))
    if D:
        for xx in (-3.6, -1.2, 1.2, 3.6):
            m.ringbar((xx, -31.22, -4.0), 0.2, 0.29, -0.05, 0.05, GALV, 12, ax=(1, 0, 0))
            m.boxr(xx - 0.12, xx + 0.12, -31.46, -31.36, -4.2, -3.8, STEEL)
        for sx in (-1, 1):
            m.path([(sx * 4.15, -31.22, -4.0), (sx * 4.15, -31.22, -2.6), (sx * 4.15, -30.96, -2.6)], 0.11, RED, 8)
            m.path([(sx * 4.4, -31.22, -4.0), (sx * 4.4, -31.1, -3.2), (sx * 4.4, -31.1, 3.7), (sx * 4.15, -31.1, 3.7), (sx * 4.15, -30.96, 3.7)], 0.09, RED, 6)


def trench_floor(z):
    return -39.6 if z <= 6 else -39.6 + (z - 6) * 3.0 / 66.0


def trench(m):
    D = m.lod == 0
    for sx in (-1, 1):
        for z in range(10, 71, 4 if D else 8):
            m.boxr(sx * 2.99, sx * 2.76, trench_floor(z) + 0.02, PAD - 0.03, z - 0.24, z + 0.24, CONC)
        m.boxr(sx * 3.0, sx * 3.32, PAD, PAD + 0.14, 6.0, 70.0, STEEL)
        m.boxr(sx * 2.94, sx * 3.0, -38.0, PAD - 0.04, 6.0, 24.0, SOOT)
        if D:
            m.path([(sx * 3.2, PAD + 0.2, 6.6), (sx * 2.84, PAD + 0.2, 6.6), (sx * 2.84, -33.5, 6.6), (sx * 2.84, -33.5, 46.0)], 0.09, RED, 8)
            for i in range(16):
                z = 8.0 + i * 2.5
                m.tube((sx * 2.84, -33.5, z), (sx * 2.58, -33.66, z), 0.035, GALV, 6, r2=0.07)
    p = m.pt
    m.face([p(-2.96, -34.86, -2.97), p(2.96, -34.86, -2.97), p(2.96, -39.52, 5.46), p(-2.96, -39.52, 5.46)], SOOT, want=Vector((0, 0.5, 1)))
    # service bridge beyond the last barrier
    m.boxr(-4.7, 4.7, PAD + 0.02, PAD + 0.1, 61.0, 63.0, GALV)
    for z in (61.06, 62.94):
        m.boxr(-4.7, 4.7, PAD - 0.5, PAD + 0.02, z - 0.07, z + 0.07, STEEL)
        m.rail((-4.7, PAD + 0.1, z), (4.7, PAD + 0.1, z))
    if D:
        for x in (-3.0, -1.5, 0.0, 1.5, 3.0):
            m.boxr(x - 0.05, x + 0.05, PAD - 0.3, PAD + 0.02, 61.1, 62.9, STEEL)


def deluge(m):
    D = m.lod == 0
    cx, cz = -46.0, -38.0
    legs = [(cx - 3.6, cz - 3.6), (cx + 3.6, cz - 3.6), (cx + 3.6, cz + 3.6), (cx - 3.6, cz + 3.6)]
    for (x, z) in legs:
        m.tube((x, PAD + 0.3, z), (x, 14.4, z), 0.5, STEEL, m.sg(12, 6))
        m.boxr(x - 0.95, x + 0.95, PAD, PAD + 0.36, z - 0.95, z + 0.95, CONC)
    levels = [-24.6, -15.6, -6.6, 2.4, 11.4]
    bays = [PAD + 0.5] + levels
    for k in range(4):
        a, b = legs[k], legs[(k + 1) % 4]
        for y in levels:
            m.tube((a[0], y, a[1]), (b[0], y, b[1]), 0.17, STEEL, 6, caps=False)
        for i in range(len(bays) - 1):
            m.tube((a[0], bays[i], a[1]), (b[0], bays[i + 1], b[1]), 0.06, GALV, 4, caps=False)
            if D:
                m.tube((b[0], bays[i], b[1]), (a[0], bays[i + 1], a[1]), 0.06, GALV, 4, caps=False)
    m.tube((cx, PAD, cz), (cx, 13.4, cz), 0.5, RED, m.sg(12, 6))
    S = m.sg(32, 10)
    m.lathe([(0.0, 12.9, WHITE), (2.3, 13.2, WHITE), (4.4, 14.3, WHITE), (5.2, 16.0, WHITE), (5.2, 18.4, AMBER), (5.2, 19.5, WHITE),
             (5.2, 23.0, WHITE), (4.85, 24.3, WHITE), (3.6, 25.4, WHITE), (1.7, 26.05, WHITE), (0.0, 26.2, None)], S, o=(cx, 0, cz))
    m.ringbar((cx, 0, cz), 5.15, 6.35, 15.0, 15.1, GALV, S)
    n = 24 if D else 12
    for i in range(n):
        a0, a1 = 2 * math.pi * i / n, 2 * math.pi * (i + 1) / n
        p0 = (cx + 6.3 * math.cos(a0), 15.1, cz + 6.3 * math.sin(a0))
        p1 = (cx + 6.3 * math.cos(a1), 15.1, cz + 6.3 * math.sin(a1))
        m.tube((p0[0], 16.15, p0[2]), (p1[0], 16.15, p1[2]), 0.03, AMBER, 4, caps=False)
        if D:
            m.tube((p0[0], 15.65, p0[2]), (p1[0], 15.65, p1[2]), 0.022, AMBER, 4, caps=False)
            m.beam(p0, (p0[0], 16.15, p0[2]), 0.05, 0.05, AMBER, up=(1, 0, 0))
            if i % 3 == 0:
                m.beam((cx + 5.1 * math.cos(a0), 14.3, cz + 5.1 * math.sin(a0)), (cx + 6.3 * math.cos(a0), 15.0, cz + 6.3 * math.sin(a0)), 0.08, 0.12, STEEL)
    m.tube((cx, 26.1, cz), (cx, 26.9, cz), 0.4, STEEL, m.sg(10, 6))
    for dx in (-1.6, 1.6):
        m.boxr(cx + dx - 0.12, cx + dx + 0.12, 25.9, 26.35, cz - 0.12, cz + 0.12, LRED)
    if D:
        x, z = legs[0]
        for dz in (-0.22, 0.22):
            m.beam((x - 0.72, PAD, z + dz), (x - 0.72, 15.1, z + dz), 0.05, 0.05, GALV, up=(1, 0, 0))
        for i in range(52):
            yy = PAD + 0.5 + i * 0.9
            m.beam((x - 0.72, yy, z - 0.22), (x - 0.72, yy, z + 0.22), 0.03, 0.03, GALV)
    # pump skid and the ground main to the launch mount header
    m.boxr(cx + 5.6, cx + 9.0, PAD, PAD + 0.25, cz - 1.4, cz + 1.4, CONC)
    m.boxr(cx + 6.0, cx + 8.6, PAD + 0.25, PAD + 1.9, cz - 1.0, cz + 1.0, STEEL)
    m.boxr(cx + 6.0, cx + 8.6, PAD + 1.9, PAD + 2.0, cz - 1.1, cz + 1.1, AMBER)
    main = [(cx, PAD + 0.5, cz), (-34.0, PAD + 0.5, cz), (-34.0, PAD + 0.5, -4.0), (-5.0, PAD + 0.5, -4.0), (-5.0, -31.22, -4.0)]
    m.path(main, 0.3, RED, m.sg(12, 6))
    for (a, b) in zip(main[1:3], main[2:4]):
        L = math.dist(a, b)
        for i in range(int(L / 5.0)):
            t = (i + 0.5) * 5.0 / L
            x, z = a[0] + (b[0] - a[0]) * t, a[2] + (b[2] - a[2]) * t
            m.boxr(x - 0.5, x + 0.5, PAD, PAD + 0.2, z - 0.5, z + 0.5, CONC)
            if D and i % 2 == 0:
                ax = (0, 0, 1) if a[0] == b[0] else (1, 0, 0)
                m.ringbar((x, PAD + 0.5, z), 0.3, 0.42, -0.06, 0.06, GALV, 12, ax=ax)
    if D:   # isolation valve with hand wheel
        m.boxr(-34.45, -33.55, PAD + 0.1, PAD + 1.0, -20.5, -19.6, RED)
        m.tube((-34.0, PAD + 1.0, -20.05), (-34.0, PAD + 1.5, -20.05), 0.05, GALV, 6)
        m.ringbar((-34.0, 0, -20.05), 0.3, 0.38, PAD + 1.46, PAD + 1.52, AMBER, 12)


def offset_path(pts, d):
    """Offset an axis-aligned plan polyline [(x, z), ...] by d to its left (mitred)."""
    out = []
    ns = []
    for i in range(len(pts) - 1):
        dx, dz = pts[i + 1][0] - pts[i][0], pts[i + 1][1] - pts[i][1]
        L = math.hypot(dx, dz)
        ns.append((-dz / L, dx / L))
    for i, p in enumerate(pts):
        if i == 0:
            n = ns[0]
        elif i == len(pts) - 1:
            n = ns[-1]
        else:
            n = (ns[i - 1][0] + ns[i][0], ns[i - 1][1] + ns[i][1])
        out.append((p[0] + n[0] * d, p[1] + n[1] * d))
    return out


def tank_farm(m):
    D = m.lod == 0
    # ---- LOX sphere on its ring of legs
    sc = (42.0, -23.6, -42.0)
    r = 6.4
    S = m.sg(32, 12)
    rings = 12 if D else 6
    prof = [(r * math.sin(math.pi * i / rings), -r * math.cos(math.pi * i / rings), WHITE) for i in range(rings + 1)]
    prof[0] = (0.0, -r, WHITE)
    prof[-1] = (0.0, r, None)
    m.lathe(prof, S, o=sc)
    m.ringbar(sc, r - 0.05, r + 0.38, -0.3, 0.3, STEEL, S)
    m.lathe([(r * 0.87 + 0.012, -r * 0.5 + 0.4, AMBER), (r * 0.94 + 0.012, -r * 0.36 + 0.4, AMBER)], S, o=sc)
    for k in range(8):
        a = math.radians(22.5 + 45 * k)
        a2 = math.radians(22.5 + 45 * (k + 1))
        lx, lz = sc[0] + 6.62 * math.cos(a), sc[2] + 6.62 * math.sin(a)
        nx, nz = sc[0] + 6.62 * math.cos(a2), sc[2] + 6.62 * math.sin(a2)
        m.tube((lx, PAD + 0.3, lz), (lx, sc[1] + 0.3, lz), 0.27, STEEL, m.sg(10, 5))
        m.boxr(lx - 0.6, lx + 0.6, PAD, PAD + 0.32, lz - 0.6, lz + 0.6, CONC)
        m.tube((lx, PAD + 0.6, lz), (nx, sc[1] - 0.6, nz), 0.05, GALV, 4, caps=False)
        if D:
            m.tube((nx, PAD + 0.6, nz), (lx, sc[1] - 0.6, lz), 0.05, GALV, 4, caps=False)
    m.ringbar(sc, 0.0001, 1.9, r - 0.05, r + 0.05, GALV, m.sg(16, 8))
    m.tube((sc[0], sc[1] + r, sc[2]), (sc[0], sc[1] + r + 0.7, sc[2]), 0.45, STEEL, m.sg(10, 6))
    m.path([(sc[0] + 1.2, sc[1] + r - 0.2, sc[2]), (sc[0] + 1.2, sc[1] + r + 5.5, sc[2])], 0.12, GALV, 6)
    m.boxr(sc[0] + 1.05, sc[0] + 1.35, sc[1] + r + 5.5, sc[1] + r + 5.8, sc[2] - 0.15, sc[2] + 0.15, LRED)
    if D:
        for i in range(12):
            a0, a1 = 2 * math.pi * i / 12, 2 * math.pi * (i + 1) / 12
            m.tube((sc[0] + 1.85 * math.cos(a0), sc[1] + r + 1.1, sc[2] + 1.85 * math.sin(a0)),
                   (sc[0] + 1.85 * math.cos(a1), sc[1] + r + 1.1, sc[2] + 1.85 * math.sin(a1)), 0.028, AMBER, 4, caps=False)
            m.beam((sc[0] + 1.85 * math.cos(a0), sc[1] + r, sc[2] + 1.85 * math.sin(a0)),
                   (sc[0] + 1.85 * math.cos(a0), sc[1] + r + 1.1, sc[2] + 1.85 * math.sin(a0)), 0.045, 0.045, AMBER, up=(1, 0, 0))
        # stair spiralling up the sphere from the pad to the top platform
        prev = None
        n = 46
        for i in range(n + 1):
            t = i / n
            y = PAD + 0.2 + t * (sc[1] + r - PAD - 0.2)
            dy = y - sc[1]
            rs = max(math.sqrt(max(r * r - dy * dy, 0.0)) + 0.55, 1.7) if dy > -r else 1.7
            if dy < 0:
                rs = max(rs, r + 0.9)
            a = math.radians(200) + t * math.radians(300)
            pnt = (sc[0] + rs * math.cos(a), y, sc[2] + rs * math.sin(a))
            if prev:
                m.beam(prev, pnt, 0.8, 0.05, GALV)
                m.tube((prev[0], prev[1] + 1.0, prev[2]), (pnt[0], pnt[1] + 1.0, pnt[2]), 0.025, AMBER, 4, caps=False)
                if i % 3 == 0:
                    m.beam(pnt, (pnt[0], pnt[1] + 1.0, pnt[2]), 0.04, 0.04, AMBER, up=(1, 0, 0))
            prev = pnt
        # LOX lettering towards the pad
        th0 = math.radians(135)

        def sph(gx, gy):
            t, ph = th0 + gx / r, gy / r + 0.42
            rr = r + 0.02
            return P(sc[0] + rr * math.cos(ph) * math.cos(t), sc[1] + rr * math.sin(ph), sc[2] + rr * math.cos(ph) * math.sin(t))
        m.text('LOX', sph, lambda q: q - P(*sc), GRAPH, scale=2.3, step=0.4, ystep=0.4)
    # ---- two horizontal fuel bullets inside a containment wall
    for xc in (48.6, 54.0):
        m.lathe([(0.0, -6.5, WHITE), (1.35, -6.3, WHITE), (2.0, -5.75, WHITE), (2.1, -5.2, WHITE), (2.1, -1.0, AMBER), (2.1, 0.0, WHITE),
                 (2.1, 5.2, WHITE), (2.0, 5.75, WHITE), (1.35, 6.3, WHITE), (0.0, 6.5, None)], m.sg(24, 10), o=(xc, -29.55, -20.0), ax=(0, 0, 1))
        for zs in (-24.2, -15.8):
            m.taper((xc, PAD, zs), (3.4, 0.9), (xc, -30.7, zs), (3.0, 0.7), CONC)
        m.tube((xc, -27.5, -22.0), (xc, -27.0, -22.0), 0.35, STEEL, m.sg(10, 6))
        m.path([(xc, -27.5, -17.0), (xc, -26.2, -17.0)], 0.07, GALV, 6)
        m.path([(xc, -31.4, -14.6), (xc, PAD + 0.3, -14.6), (xc, PAD + 0.3, -12.2)], 0.11, GALV, 6)
    m.path([(54.0, PAD + 0.3, -12.2), (35.6, PAD + 0.3, -12.2), (35.6, PAD + 0.3, -22.6), (34.5, -30.35, -22.6), (34.5, -30.35, -23.6)], 0.11, GALV, 6)
    for (x0, x1, z0, z1) in ((45.3, 57.3, -28.5, -28.2), (45.3, 57.3, -11.7, -11.4), (45.3, 45.6, -28.5, -11.4), (57.0, 57.3, -28.5, -11.4)):
        m.boxr(x0, x1, PAD, PAD + 0.75, z0, z1, CONC)
    if D:
        m.gquad(45.6, 57.0, -28.2, -11.7, CONC, y=PAD + 0.03)

        def bul(gx, gy):
            ph = gy / 2.1 + 0.1
            return P(48.6 - 2.115 * math.cos(ph), -29.55 + 2.115 * math.sin(ph), -20.0 + 2.6 - gx)
        m.text('LCH4', bul, lambda q: Vector((-1, 0, 0)), GRAPH, scale=1.5, ystep=0.16)
    # ---- pipe rack from the farm to the tower base
    rack = [(34.0, -38.0), (34.0, -24.0), (1.4, -24.0), (1.4, -13.95)]
    for (d, mat, rr) in ((-0.45, FOIL, 0.16), (0.0, GALV, 0.12), (0.45, STEEL, 0.12)):
        m.path([(x, -30.35 + rr - 0.12, z) for (x, z) in offset_path(rack, d)], rr, mat, m.sg(8, 5))
    m.path([(34.0 - 0.45, -30.31, -38.0), (34.0 - 0.45, -30.31, -40.2), (36.8, -30.31, -40.2)], 0.16, FOIL, m.sg(8, 5))
    for i in range(len(rack) - 1):
        (xa, za), (xb, zb) = rack[i], rack[i + 1]
        L = math.hypot(xb - xa, zb - za)
        n = max(2, int(L / 5.4) + 1)
        for j in range(n):
            t = j / (n - 1) * 0.94 + 0.03
            x, z = xa + (xb - xa) * t, za + (zb - za) * t
            if xa == xb:
                m.beam((x - 0.85, PAD, z), (x - 0.85, -30.5, z), 0.14, 0.14, STEEL, up=(1, 0, 0))
                m.beam((x + 0.85, PAD, z), (x + 0.85, -30.5, z), 0.14, 0.14, STEEL, up=(1, 0, 0))
                m.beam((x - 1.0, -30.57, z), (x + 1.0, -30.57, z), 0.14, 0.16, STEEL)
            else:
                m.beam((x, PAD, z - 0.85), (x, -30.5, z - 0.85), 0.14, 0.14, STEEL, up=(1, 0, 0))
                m.beam((x, PAD, z + 0.85), (x, -30.5, z + 0.85), 0.14, 0.14, STEEL, up=(1, 0, 0))
                m.beam((x, -30.57, z - 1.0), (x, -30.57, z + 1.0), 0.14, 0.16, STEEL)
    # ---- pad terminal building and the cable tray to the tower
    m.boxr(-29.0, -23.0, PAD, -29.5, -35.0, -31.0, CONC)
    m.boxr(-29.25, -22.75, -29.5, -29.32, -35.25, -30.6, STEEL)
    m.boxr(-26.7, -25.3, PAD, -30.4, -31.02, -30.96, GRAPH)
    m.boxr(-29.02, -22.98, -30.2, -29.9, -35.02, -30.98, AMBER)
    m.boxr(-26.5, -25.5, -30.25, -30.15, -31.0, -30.85, LWARM)
    if D:
        for xx in (-28.0, -24.2):
            m.boxr(xx - 0.6, xx + 0.6, -29.32, -28.6, -34.2, -33.0, GALV)
            m.boxr(xx - 0.5, xx + 0.5, -29.2, -28.72, -32.98, -32.94, GRAPH)
        m.boxr(-28.6, -27.4, -31.4, -30.6, -31.02, -30.97, GLASS)
    tray = [(-23.0, -33.0), (-1.4, -33.0), (-1.4, -13.95)]
    for i in range(len(tray) - 1):
        (xa, za), (xb, zb) = tray[i], tray[i + 1]
        alongx = za == zb
        a, b = (xa, -31.33, za), (xb, -31.33, zb)
        m.beam(a, b, 0.62, 0.03, GALV)
        m.beam((xa, -31.25, za), (xb, -31.25, zb), 0.42, 0.11, RUBBER)
        for s in (-0.31, 0.31):
            m.beam((xa + (0 if alongx else s), -31.27, za + (s if alongx else 0)), (xb + (0 if alongx else s), -31.27, zb + (s if alongx else 0)), 0.02, 0.14, GALV)
        L = math.hypot(xb - xa, zb - za)
        n = int(L / 3.1) + 1
        for j in range(n):
            t = j / (n - 1) * 0.96 + 0.02
            x, z = xa + (xb - xa) * t, za + (zb - za) * t
            m.beam((x, PAD, z), (x, -31.36, z), 0.1, 0.1, STEEL, up=(1, 0, 0))
            if D:
                if alongx:
                    m.beam((x, -31.38, z - 0.36), (x, -31.38, z + 0.36), 0.08, 0.06, STEEL)
                else:
                    m.beam((x - 0.36, -31.38, z), (x + 0.36, -31.38, z), 0.08, 0.06, STEEL)


def flood_towers(m):
    D = m.lod == 0
    for sx in (-1, 1):
        cx, cz = sx * 46.0, 45.7
        m.lattice(cx, cz, PAD + 0.3, -5.3, 3.4, 1.8, 8 if D else 4, 0.17, 0.075, STEEL, cross=True)
        for (dx, dz) in ((-1.7, -1.7), (1.7, -1.7), (1.7, 1.7), (-1.7, 1.7)):
            m.boxr(cx + dx - 0.5, cx + dx + 0.5, PAD, PAD + 0.32, cz + dz - 0.5, cz + dz + 0.5, CONC)
        m.boxr(cx - 1.6, cx + 1.6, -5.3, -5.2, cz - 1.6, cz + 1.6, GALV)
        for (a, b) in (((-1.6, -1.6), (1.6, -1.6)), ((1.6, -1.6), (1.6, 1.6)), ((1.6, 1.6), (-1.6, 1.6)), ((-1.6, 1.6), (-1.6, -1.6))):
            m.rail((cx + a[0], -5.2, cz + a[1]), (cx + b[0], -5.2, cz + b[1]))
        m.tube((cx, -5.2, cz), (cx, -1.6, cz), 0.09, STEEL, 6)
        m.boxr(cx - 0.12, cx + 0.12, -1.6, -1.3, cz - 0.12, cz + 0.12, LRED)
        if D:
            for dz in (-0.2, 0.2):
                m.beam((cx - sx * 1.78, PAD + 0.3, cz + dz), (cx - sx * 0.98, -5.3, cz + dz), 0.045, 0.045, GALV, up=(1, 0, 0))
            for i in range(30):
                t = i / 30
                x, y = cx - sx * (1.78 - 0.8 * t), PAD + 0.6 + t * 26.6
                m.beam((x, y, cz - 0.2), (x, y, cz + 0.2), 0.03, 0.03, GALV)
        # lamp bank facing the vehicle
        yaw = math.degrees(math.atan2(-cx, -cz))
        m.at(cx, 0, cz, yaw)
        for y in (-4.75, -3.95, -3.15):
            m.beam((-2.1, y, 1.05), (2.1, y, 1.05), 0.08, 0.08, STEEL)
        for x in (-2.1, 0.0, 2.1):
            m.beam((x, -5.2, 1.05), (x, -2.9, 1.05), 0.08, 0.08, STEEL, up=(1, 0, 0))
            m.beam((x, -5.15, 0.0), (x, -4.0, 1.05), 0.06, 0.06, STEEL)
        for row, y in enumerate((-4.35, -3.55)):
            for i in range(6 if D else 3):
                x = (-1.75 + i * 0.7) if D else (-1.4 + i * 1.4)
                w = 0.56 if D else 1.2
                m.boxr(x - w / 2, x + w / 2, y - 0.24, y + 0.24, 1.08, 1.34, GRAPH)
                m.boxr(x - w / 2 + 0.04, x + w / 2 - 0.04, y - 0.2, y + 0.2, 1.34, 1.36, LWARM)
        m.world()


def lightning(m):
    D = m.lod == 0
    masts = [(-38.0, 30.0), (38.0, 30.0), (38.0, -26.0), (-38.0, -26.0)]
    top = 46.0
    for (cx, cz) in masts:
        m.lattice(cx, cz, PAD + 0.3, 40.0, 3.2, 0.9, 18 if D else 12, 0.15, 0.065, STEEL, bands=((AMBER, MARK), 3 if D else 2))
        for (dx, dz) in ((-1.6, -1.6), (1.6, -1.6), (1.6, 1.6), (-1.6, 1.6)):
            m.boxr(cx + dx - 0.55, cx + dx + 0.55, PAD, PAD + 0.32, cz + dz - 0.55, cz + dz + 0.55, CONC)
        m.boxr(cx - 0.55, cx + 0.55, 40.0, 40.12, cz - 0.55, cz + 0.55, GALV)
        m.tube((cx, 40.1, cz), (cx, top, cz), 0.2, WHITE, m.sg(10, 5), r2=0.14)
        m.tube((cx, top, cz), (cx, top + 2.4, cz), 0.03, GALV, 4)
        m.ringbar((cx, 0, cz), 0.14, 0.34, top - 0.08, top + 0.06, GALV, m.sg(10, 5))
        for y in (40.2, 4.0):
            m.boxr(cx - 0.14, cx + 0.14, y, y + 0.32, cz - 0.14, cz + 0.14, LRED)
    n = 10 if D else 5
    for i in range(4):
        (ax, az), (bx, bz) = masts[i], masts[(i + 1) % 4]
        prev = None
        for j in range(n + 1):
            t = j / n
            pnt = (ax + (bx - ax) * t, top - 3.2 * 4 * t * (1 - t), az + (bz - az) * t)
            if prev:
                m.tube(prev, pnt, 0.035, GALV, 3 if not D else 4, caps=False)
            prev = pnt
        for t in (0.25, 0.5, 0.75):      # aviation marker balls on the catenary
            m.ball((ax + (bx - ax) * t, top - 3.2 * 4 * t * (1 - t), az + (bz - az) * t), 0.42, AMBER, 8 if D else 6)


def truck(m, kind, body):
    """Service vehicle in its own frame: origin on the ground under the centre, +z forward."""
    D = m.lod == 0
    van = kind == 'van'
    W = 2.1 if van else 2.5
    L = {'tanker': 10.4, 'van': 5.8, 'crane': 8.8}[kind]
    wr = 0.42 if van else 0.54
    front = L / 2
    axles = [front - 1.2, -front + 1.4] if van else [front - 1.5, -front + 1.3, -front + 2.6]
    for za in axles:
        for sx in (-1, 1):
            m.tube((sx * (W / 2 - 0.34), wr, za), (sx * (W / 2 - 0.02), wr, za), wr, RUBBER, m.sg(16, 8))
            m.tube((sx * (W / 2 - 0.03), wr, za), (sx * (W / 2 + 0.01), wr, za), wr * 0.55, GALV, m.sg(10, 6))
        m.tube((-W / 2 + 0.3, wr, za), (W / 2 - 0.3, wr, za), 0.08, STEEL, 6)
    if van:
        m.boxr(-W / 2, W / 2, 0.42, 1.25, -front, front - 0.05, body)
        m._box8([m.pt(-W / 2, 1.25, -front), m.pt(W / 2, 1.25, -front), m.pt(W / 2, 1.25, front - 0.05), m.pt(-W / 2, 1.25, front - 0.05),
                 m.pt(-W / 2 + 0.08, 2.35, -front + 0.05), m.pt(W / 2 - 0.08, 2.35, -front + 0.05), m.pt(W / 2 - 0.08, 2.35, front - 1.25), m.pt(-W / 2 + 0.08, 2.35, front - 1.25)], body)
        m.boxr(-W / 2 - 0.01, W / 2 + 0.01, 0.95, 1.12, -front + 0.02, front - 0.07, AMBER)
        m.face([m.pt(-W / 2 + 0.14, 1.32, front - 0.11), m.pt(W / 2 - 0.14, 1.32, front - 0.11), m.pt(W / 2 - 0.2, 2.24, front - 1.17), m.pt(-W / 2 + 0.2, 2.24, front - 1.17)],
               GLASS, want=m.dr(0, 0.5, 1))
        for sx in (-1, 1):
            m.boxr(sx * (W / 2 - 0.03), sx * (W / 2 - 0.015), 1.4, 2.15, -front + 0.5, front - 1.5, GLASS)
        m.boxr(-0.6, 0.6, 2.35, 2.47, front - 2.2, front - 1.9, LCYAN)
        m.boxr(-W / 2 + 0.1, -W / 2 + 0.45, 0.6, 0.78, front - 0.05, front - 0.02, LWARM)
        m.boxr(W / 2 - 0.45, W / 2 - 0.1, 0.6, 0.78, front - 0.05, front - 0.02, LWARM)
        return
    # frame and cab
    m.boxr(-0.5, 0.5, 0.62, 0.9, -front, front - 0.3, STEEL)
    m.boxr(-W / 2, W / 2, 0.75, 1.75, front - 2.3, front, body)
    m._box8([m.pt(-W / 2, 1.75, front - 2.3), m.pt(W / 2, 1.75, front - 2.3), m.pt(W / 2, 1.75, front), m.pt(-W / 2, 1.75, front),
             m.pt(-W / 2 + 0.06, 2.85, front - 2.25), m.pt(W / 2 - 0.06, 2.85, front - 2.25), m.pt(W / 2 - 0.06, 2.85, front - 0.55), m.pt(-W / 2 + 0.06, 2.85, front - 0.55)], body)
    m.face([m.pt(-W / 2 + 0.14, 1.82, front - 0.03), m.pt(W / 2 - 0.14, 1.82, front - 0.03), m.pt(W / 2 - 0.2, 2.76, front - 0.52), m.pt(-W / 2 + 0.2, 2.76, front - 0.52)],
           GLASS, want=m.dr(0, 0.5, 1))
    for sx in (-1, 1):
        m.boxr(sx * (W / 2 - 0.025), sx * (W / 2 - 0.01), 1.85, 2.7, front - 2.0, front - 0.8, GLASS)
        m.boxr(sx * (W / 2 - 0.42), sx * (W / 2 - 0.08), 0.95, 1.15, front - 0.03, front + 0.01, LWARM)
    m.boxr(-W / 2, W / 2, 0.55, 0.8, front - 0.12, front + 0.1, STEEL)
    m.boxr(-0.55, 0.55, 2.85, 2.97, front - 1.6, front - 1.3, AMBER if body != RED else LCYAN)
    back0, back1 = -front + 0.15, front - 2.55
    if kind == 'tanker':
        zc, hl = (back0 + back1) / 2, (back1 - back0) / 2
        m.lathe([(0.0, -hl, WHITE if body != RED else RED), (0.85, -hl + 0.12, WHITE if body != RED else RED), (1.15, -hl + 0.5, AMBER), (1.15, -hl + 1.1, WHITE if body != RED else RED),
                 (1.15, hl - 0.5, WHITE if body != RED else RED), (0.85, hl - 0.12, WHITE if body != RED else RED), (0.0, hl, None)], m.sg(20, 8), o=(0, 2.1, zc), ax=(0, 0, 1))
        m.boxr(-0.95, 0.95, 0.9, 1.1, back0, back1, STEEL)
        m.boxr(-0.35, 0.35, 3.25, 3.33, back0 + 0.6, back1 - 0.6, GALV)
        for z in (zc - 1.6, zc + 1.2):
            m.tube((0, 3.2, z), (0, 3.42, z), 0.28, GALV, m.sg(10, 6))
        if D:
            m.boxr(-W / 2 + 0.02, -W / 2 + 0.5, 0.95, 1.6, back0 + 0.2, back0 + 1.4, STEEL)
            m.path([(-W / 2 + 0.2, 1.3, back0 + 0.8), (-W / 2 - 0.25, 1.1, back0 + 0.8), (-W / 2 - 0.3, 0.12, back0 + 1.6), (-W / 2 - 0.3, 0.1, back0 + 3.6)], 0.06, RUBBER, 6)
            for sx in (-1, 1):
                m.boxr(sx * 1.2, sx * 1.26, 0.95, 1.0, back0, back1, HAZ)
    else:   # knuckle-boom service truck with a flat bed
        m.boxr(-W / 2, W / 2, 0.9, 1.05, back0, back1, GALV)
        for sx in (-1, 1):
            m.boxr(sx * (W / 2 - 0.04), sx * W / 2, 1.05, 1.45, back0, back1 - 1.3, body)
        m.boxr(-0.7, 0.7, 1.05, 1.55, back1 - 1.2, back1 - 0.1, STEEL)
        m.tube((0, 1.55, back1 - 0.65), (0, 2.5, back1 - 0.65), 0.2, AMBER, m.sg(10, 6))
        m.beam((0, 2.5, back1 - 0.65), (0, 4.3, back1 - 3.2), 0.26, 0.3, AMBER)
        m.beam((0, 4.3, back1 - 3.2), (0, 3.3, back1 - 5.0), 0.2, 0.24, AMBER)
        m.tube((0, 3.3, back1 - 5.0), (0, 2.6, back1 - 5.0), 0.02, GALV, 4)
        m.boxr(-0.5, 0.5, 1.05, 1.95, back0 + 0.3, back0 + 1.5, GALV)
        m.boxr(0.25, 1.05, 1.05, 1.75, back0 + 1.8, back0 + 2.9, WHITE)
        m.tube((-0.6, 1.5, back0 + 2.4), (-0.6, 1.5, back0 + 3.1), 0.45, RUBBER, m.sg(12, 6))
        for sx in (-1, 1):
            m.boxr(sx * W / 2, sx * (W / 2 + 0.75), 0.72, 0.86, back1 - 0.9, back1 - 0.7, STEEL)
            m.boxr(sx * (W / 2 + 0.6), sx * (W / 2 + 0.74), 0.06, 0.74, back1 - 0.87, back1 - 0.73, GALV)
            m.boxr(sx * (W / 2 + 0.42), sx * (W / 2 + 0.92), 0.0, 0.06, back1 - 1.05, back1 - 0.55, HAZ)


PARKED = [('tanker', WHITE, 40.0, -6.0, 0.0), ('van', WHITE, -18.0, -21.0, 90.0), ('crane', AMBER, -13.0, 22.0, 70.0), ('tanker', RED, -24.0, 9.0, 100.0)]


def light_cart(m):
    """Portable light tower on a two-wheel trailer (own frame, +z = drawbar)."""
    D = m.lod == 0
    m.boxr(-0.7, 0.7, 0.45, 1.25, -1.0, 1.0, AMBER)
    m.boxr(-0.72, 0.72, 1.25, 1.31, -1.02, 1.02, GRAPH)
    m.beam((0, 0.6, 1.0), (0, 0.5, 2.1), 0.08, 0.08, STEEL)
    for sx in (-1, 1):
        m.tube((sx * 0.72, 0.36, -0.2), (sx * 0.92, 0.36, -0.2), 0.36, RUBBER, m.sg(12, 6))
    m.tube((0, 1.3, -0.6), (0, 6.4, -0.6), 0.07, GALV, 6)
    m.beam((-0.9, 6.3, -0.6), (0.9, 6.3, -0.6), 0.06, 0.06, STEEL)
    for x in (-0.65, 0.0, 0.65):
        m.boxr(x - 0.26, x + 0.26, 6.38, 6.78, -0.72, -0.5, GRAPH)
        m.boxr(x - 0.22, x + 0.22, 6.42, 6.74, -0.5, -0.48, LWARM)
    if D:
        m.beam((0, 0.0, 2.05), (0, 0.5, 2.05), 0.05, 0.05, GALV, up=(1, 0, 0))
        for sx in (-1, 1):
            m.beam((sx * 0.7, 0.5, -0.9), (sx * 1.3, 0.02, -1.2), 0.05, 0.05, GALV)


def support_gear(m):
    D = m.lod == 0
    for (x, z, yaw) in ((13.6, 13.4, 225.0), (-13.6, 13.4, 135.0), (10.5, -17.5, -30.0)):
        m.at(x, PAD, z, yaw)
        light_cart(m)
        m.world()
    # gas bottle rack and tool cabinets beside the mount stairs
    m.at(7.6, PAD, 0.6, 90.0)
    m.boxr(-1.0, 1.0, 0.0, 0.12, -0.45, 0.45, STEEL)
    for i in range(6):
        x = -0.75 + (i % 3) * 0.75 * 1.0
        z = -0.2 if i < 3 else 0.2
        m.tube((x, 0.12, z), (x, 1.55, z), 0.12, WHITE if i % 2 else AMBER, m.sg(8, 5))
        if D:
            m.tube((x, 1.55, z), (x, 1.72, z), 0.04, GALV, 5)
    for x in (-1.0, 1.0):
        m.beam((x, 0.12, -0.45), (x, 1.9, -0.45), 0.05, 0.05, STEEL, up=(1, 0, 0))
        m.beam((x, 0.12, 0.45), (x, 1.9, 0.45), 0.05, 0.05, STEEL, up=(1, 0, 0))
    m.beam((-1.0, 1.9, -0.45), (1.0, 1.9, -0.45), 0.05, 0.05, STEEL)
    m.beam((-1.0, 1.9, 0.45), (1.0, 1.9, 0.45), 0.05, 0.05, STEEL)
    m.world()
    for (x, z, w, h, d, mat) in ((7.4, 3.4, 1.2, 1.5, 0.6, RED), (7.4, 4.9, 1.6, 1.0, 0.8, GRAPH), (-7.5, 3.0, 1.2, 1.3, 1.2, GALV),
                                 (-7.5, 4.7, 1.0, 0.8, 1.0, AMBER), (-9.6, -26.4, 1.4, 1.1, 1.0, GALV)):
        m.boxr(x - d / 2, x + d / 2, PAD, PAD + h, z - w / 2, z + w / 2, mat)
        m.boxr(x - d / 2 - 0.02, x + d / 2 + 0.02, PAD + h, PAD + h + 0.04, z - w / 2 - 0.02, z + w / 2 + 0.02, STEEL)
    # cable reels by the tray
    for (x, z) in ((-8.0, -29.6), (-10.2, -29.6)):
        for sx in (-0.42, 0.42):
            m.tube((x + sx, PAD + 0.85, z), (x + sx + (0.06 if sx > 0 else -0.06), PAD + 0.85, z), 0.85, GALV, m.sg(16, 8))
        m.tube((x - 0.42, PAD + 0.85, z), (x + 0.42, PAD + 0.85, z), 0.5, RUBBER, m.sg(12, 6))


def vehicles(m):
    for (kind, body, x, z, yaw) in PARKED:
        m.at(x, PAD, z, yaw)
        truck(m, kind, body)
        m.world()
    support_gear(m)


def jersey(m, x, z, yaw, L=2.4):
    m.at(x, PAD, z, yaw)
    m.boxr(-0.3, 0.3, 0.0, 0.2, -L / 2, L / 2, CONC)
    m.taper((0, 0.2, 0), (0.56, L), (0, 0.82, 0), (0.2, L - 0.04), CONC)
    m.world()


def markings(m):
    # keep-clear ring round the launch mount (open over the flame trench)
    for i in range(44):
        a0 = math.radians(109 + i * 7.3)
        m.garc((0, 0), 15.6, 16.05, a0, a0 + math.radians(4.6), AMBER, 3)
    for k in range(12):
        a = math.radians(120 + k * 27.2)
        m.gline((16.4 * math.cos(a), 16.4 * math.sin(a)), (18.6 * math.cos(a), 18.6 * math.sin(a)), 0.3, AMBER)
    # parking bays under the service vehicles
    for (kind, body, x, z, yaw) in PARKED:
        L, W = {'tanker': 12.4, 'van': 7.6, 'crane': 11.0}[kind], 4.2
        m.at(x, 0, z, yaw)
        for (a, b) in (((-W / 2, -L / 2), (W / 2, -L / 2)), ((W / 2, -L / 2), (W / 2, L / 2)), ((W / 2, L / 2), (-W / 2, L / 2)), ((-W / 2, L / 2), (-W / 2, -L / 2))):
            m.gline(a, b, 0.14, MARK)
        for i in range(5):
            m.gline((-W / 2 + 0.3 + i * 0.5, -L / 2 - 0.9), (-W / 2 + 0.8 + i * 0.5, -L / 2 - 0.25), 0.16, AMBER)
        m.world()
    # service lanes: tank farm to the pad, and the crawlerway approach edge lines
    m.gdash((30.0, -8.0), (20.0, -8.0), 0.18, MARK, 2.0, 1.4)
    m.gdash((20.0, -8.0), (20.0, 14.0), 0.18, MARK, 2.0, 1.4)
    for sx in (-1, 1):
        m.gdash((sx * 9.5, -49.0), (sx * 9.5, -36.0), 0.22, MARK, 2.4, 1.6)
    # hazard hatch where the rack and the tray cross the service corridor behind the tower
    for i in range(9):
        m.gline((-6.0 + i * 1.5, -26.0), (-4.6 + i * 1.5, -22.0), 0.22, AMBER)
    m.gline((-6.3, -26.0), (7.7, -26.0), 0.16, AMBER)
    m.gline((-6.3, -22.0), (7.7, -22.0), 0.16, AMBER)
    # impact barriers along the pipe rack and the cable tray, chicane at the crawlerway gate
    for i in range(5):
        jersey(m, 9.0 + i * 5.2, -21.6, 90.0)
        jersey(m, -20.0 + i * 3.6, -31.3, 90.0)
    for i in range(8):
        sx = -1 if i < 4 else 1
        m.at(sx * 10.6, PAD, -47.5 + (i % 4) * 3.1, 0.0)
        mat = AMBER if i % 2 == 0 else MARK
        m.taper((0, 0, 0), (0.6, 1.9), (0, 0.9, 0), (0.3, 1.8), mat)
        m.boxr(-0.2, 0.2, 0.9, 0.98, -0.2, 0.2, mat)
        m.world()
    # bollards round the sphere and the terminal door
    spots = [(42.0 + 9.0 * math.cos(math.radians(a)), -42.0 + 9.0 * math.sin(math.radians(a))) for a in range(100, 261, 32)]
    spots += [(-27.4, -29.6), (-24.6, -29.6), (-27.4, -27.8), (-24.6, -27.8), (6.2, -13.0), (-6.4, -13.0)]
    for (x, z) in spots:
        m.tube((x, PAD, z), (x, PAD + 1.0, z), 0.1, HAZ, 8)
        m.ball((x, PAD + 1.0, z), 0.1, AMBER, 8)
    # cyan marker lights on the keep-clear ring, warm edge lights on the crawlerway gate
    for k in range(18):
        a = math.radians(115 + k * 18.2)
        x, z = 17.0 * math.cos(a), 17.0 * math.sin(a)
        m.boxr(x - 0.11, x + 0.11, PAD, PAD + 0.16, z - 0.11, z + 0.11, GRAPH)
        m.boxr(x - 0.07, x + 0.07, PAD + 0.16, PAD + 0.24, z - 0.07, z + 0.07, LCYAN)
    for sx in (-1, 1):
        for i in range(5):
            x, z = sx * 12.0, -48.0 + i * 3.4
            m.tube((x, PAD, z), (x, PAD + 0.9, z), 0.05, GRAPH, 6)
            m.boxr(x - 0.09, x + 0.09, PAD + 0.9, PAD + 1.06, z - 0.09, z + 0.09, LWARM)


# =====================================================================================================================
MODULES = [   # (fbx, object, function, lods)
    ('LC1_LaunchVehicle', 'LC1_Vehicle', vehicle, 3),
    ('LC1_PadDressing', 'LC1_Mount', mount, 2),
    ('LC1_PadDressing', 'LC1_Trench', trench, 2),
    ('LC1_PadDressing', 'LC1_Deluge', deluge, 2),
    ('LC1_PadDressing', 'LC1_TankFarm', tank_farm, 2),
    ('LC1_PadDressing', 'LC1_FloodTowers', flood_towers, 2),
    ('LC1_PadDressing', 'LC1_Lightning', lightning, 2),
    ('LC1_PadDressing', 'LC1_Vehicles', vehicles, 2),
    ('LC1_PadDressing', 'LC1_Markings', markings, 1),
]


def build():
    make_materials()
    out = {}
    for (fbx, name, fn, lods) in MODULES:
        for lod in range(lods):
            m = Mesh('%s_LOD%d' % (name, lod), lod)
            try:
                fn(m)
            except Exception:
                traceback.print_exc()
                print('MODULE FAILED', name, lod)
            out.setdefault(fbx, []).append(m.finish())
    return out


def report(groups):
    rep = {'asset': 'LaunchComplexV01', 'frame': 'Unity metres, prologue root, identity', 'files': {}}
    for fbx, objs in groups.items():
        entry = {'objects': {}, 'triangles_by_lod': {}}
        for ob in objs:
            me = ob.data
            me.calc_loop_triangles()
            tris = len(me.loop_triangles)
            by = {}
            for tr in me.loop_triangles:
                n = MATS[tr.material_index]
                by[n] = by.get(n, 0) + 1
            ws = [U(v.co) for v in me.vertices]
            mn = [round(min(w[i] for w in ws), 3) for i in range(3)] if ws else [0, 0, 0]
            mx = [round(max(w[i] for w in ws), 3) for i in range(3)] if ws else [0, 0, 0]
            entry['objects'][ob.name] = {'tris': tris, 'verts': len(me.vertices), 'bounds_min': mn, 'bounds_max': mx, 'tris_by_material': by}
            lod = ob.name[-4:]
            entry['triangles_by_lod'][lod] = entry['triangles_by_lod'].get(lod, 0) + tris
        rep['files'][fbx] = entry
    return rep


KEEP_CLEAR = {   # Unity boxes that must stay empty: ((x0, x1), (y0, y1), (z0, z1))
    'crew_deck_arm_and_hatch_walkway': ((-4.2, 4.2), (-0.5, 3.0), (-13.0, -2.3)),
    'launch_tower_v03_base_and_stairs': ((-3.2, 5.3), (PAD + 0.4, 23.6), (-13.9, -6.95)),
    'tower_swing_arm_zone': ((-1.6, 1.6), (-26.0, -1.2), (-6.95, -2.75)),
    'capsule_volume': ((-2.6, 2.6), (-0.5, 5.2), (-2.6, 2.6)),
    'crawlerway_gate_lane': ((-9.0, 9.0), (PAD + 0.3, 60.0), (-50.0, -34.0)),
}


def keep_clear(groups):
    res = {}
    for name, (bx, by, bz) in KEEP_CLEAR.items():
        hits = 0
        for objs in groups.values():
            for ob in objs:
                for v in ob.data.vertices:
                    x, y, z = U(v.co)
                    if bx[0] < x < bx[1] and by[0] < y < by[1] and bz[0] < z < bz[1]:
                        hits += 1
        res[name] = hits
    return res


def standins():
    """Grey stand-ins of what is already in the scene (not exported)."""
    mat = bpy.data.materials.new('standin')
    mat.use_nodes = True
    mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.33, 0.33, 0.32, 1)
    m = Mesh('standin')
    m.boxr(-60, 60, PAD - 4, PAD, -50, 60, CONC)
    m.boxr(-2.6, 2.6, PAD, -0.7, -13.4, -7.4, CONC)
    m.boxr(-4, 4, -0.7, 0, -12.9, -6.9, CONC)
    m.boxr(-1, 1, -0.2, 0, -6.9, -2.6, CONC)
    m.lathe([(2.4, -0.55, CONC), (2.58, 0.0, CONC), (2.58, 2.4, CONC), (1.0, 4.8, CONC), (0.0, 5.0, None)], 32)
    ob = m.finish()
    ob.data.materials.clear()
    ob.data.materials.append(mat)
    for p in ob.data.polygons:
        p.material_index = 0
    return ob


def render(groups, out_dir):
    sc = bpy.context.scene
    standins()
    for objs in groups.values():
        for ob in objs:
            if not ob.name.endswith('_LOD0'):
                ob.hide_render = True
    sc.render.engine = 'BLENDER_EEVEE'
    sc.eevee.use_gtao = True
    sc.eevee.use_soft_shadows = True
    sc.eevee.shadow_cascade_size = '4096'
    sc.render.resolution_x, sc.render.resolution_y = 1600, 900
    sc.view_settings.view_transform = 'Filmic'
    w = bpy.data.worlds.new('W')
    w.use_nodes = True
    w.node_tree.nodes['Background'].inputs['Color'].default_value = (0.32, 0.38, 0.47, 1)
    w.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.7
    sc.world = w
    sun = bpy.data.lights.new('Sun', 'SUN')
    sun.energy = 3.4
    sun.angle = 0.02
    so = bpy.data.objects.new('Sun', sun)
    so.rotation_euler = (math.radians(55), math.radians(6), math.radians(140))
    sc.collection.objects.link(so)
    cam = bpy.data.cameras.new('C')
    co = bpy.data.objects.new('C', cam)
    sc.collection.objects.link(co)
    sc.camera = co
    views = {
        'wide': ((-95, -28, 70), (0, -8, 0), 30),
        'vehicle': ((-17, -20, 16), (0, -16, 0), 24),
        'engines': ((7.5, -31.3, 9.5), (0, -29.6, 0), 30),
        'deck_down': ((3.7, 1.75, -7.4), (14, -32.6, 22), 16),
        'adapter': ((-4.2, 1.6, -6.2), (0.5, -3.5, -1.0), 22),
        'aerial': ((70, 38, 70), (0, -22, 0), 28),
        'farm': ((14, -24, -62), (40, -28, -30), 28),
    }
    for name, (loc, tgt, lens) in views.items():
        cam.lens = lens
        cam.clip_end = 2000
        co.location = P(*loc)
        co.rotation_euler = (P(*tgt) - P(*loc)).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(out_dir, 'LC1_%s.png' % name)
        bpy.ops.render.render(write_still=True)
        print('rendered', name)


def export(groups, out_dir):
    for fbx, objs in groups.items():
        bpy.ops.object.select_all(action='DESELECT')
        for ob in objs:
            ob.hide_render = False
            ob.select_set(True)
        bpy.context.view_layer.objects.active = objs[0]
        path = os.path.join(out_dir, fbx + '.fbx')
        bpy.ops.export_scene.fbx(
            filepath=path, use_selection=True, object_types={'MESH'}, apply_unit_scale=True,
            apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True, axis_forward='-Z', axis_up='Y',
            use_mesh_modifiers=False, mesh_smooth_type='FACE', use_triangles=True, add_leaf_bones=False,
            bake_anim=False, path_mode='STRIP', embed_textures=False, use_custom_props=False)
        print('exported', path)


if __name__ == '__main__':
    groups = build()
    rep = report(groups)
    rep['keep_clear_vertices'] = keep_clear(groups)
    print('KEEP_CLEAR', rep['keep_clear_vertices'])
    os.makedirs(os.path.join(ROOT, 'Models'), exist_ok=True)
    with open(os.path.join(ROOT, 'Models', 'LaunchComplexV01_report.json'), 'w') as f:
        json.dump(rep, f, indent=1)
    for fbx, entry in rep['files'].items():
        print('TRIS', fbx, entry['triangles_by_lod'], {k: v['tris'] for k, v in entry['objects'].items()})
    if SAVE:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, 'Source~', 'LaunchComplexV01.blend'), relative_remap=True, compress=True)
    if RENDER:
        os.makedirs(os.path.join(ROOT, 'Previews~'), exist_ok=True)
        render(groups, os.path.join(ROOT, 'Previews~'))
    if EXPORT:
        export(groups, os.path.join(ROOT, 'Models'))
