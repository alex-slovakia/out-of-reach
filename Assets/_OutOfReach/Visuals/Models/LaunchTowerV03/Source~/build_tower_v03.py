"""LaunchTowerV03: original launch umbilical tower + crew access arm with white room.

Blender 3.6, background mode, reproducible:
  blender --background --factory-startup --python build_tower_v03.py -- <LaunchTowerV03 dir> [--export] [--render] [--save]

All layout numbers are written in UNITY metres (x right, y up, z towards the capsule) in the same frame as
MarinePrologue/Transport/TransportLaunch.fbx and MarineLayout.json (the "I06 isolated marine prologue" root).
P(x, y, z) converts to the Blender frame (x, z, y). With the FBX settings below (axis_forward -Z, axis_up Y,
bake_space_transform, Unity Bake Axis Conversion ON) Unity shows Blender (x, y, z) at Unity (x, z, y), so the
exported model lands at identity in the prologue frame, exactly like LaunchTowerV02.

Measured existing frame (from LaunchTowerV02/anchors.json and MarineLayout.json, read only):
  barge deck top y -32.6 (x +-18, z -14..10); staging/crew deck x +-4, z -12.9..-6.9, top y 0;
  access arm floor x +-0.98, z -6.9..-2.48, top y 0, pivot (-0.9, 0, -6.9), +Y 0 connected / -90 retracted;
  hatch pivot (-0.72, 0, -2.4); capsule shell radius 2.577 about the y axis; booster radius 2.895.
Keep-clear corridors held: arm walkway x +-0.93, y 0.02..2.2, z -6.9..-2.5; deck x +-3.9, y 0.05..2.5,
z -12.8..-7.0; hatch doorway x +-0.95, y 0.02..2.2, z -3.4..-2.3.
"""
import bpy, bmesh, math, os, sys, json, random
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
ROOT = os.path.abspath(argv[0]) if argv else os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
EXPORT = '--export' in argv
RENDER = '--render' in argv
SAVE = '--save' in argv
random.seed(3)

MATS = ['LT3_SteelGraphite', 'LT3_SafetyAmber', 'LT3_Galvanized', 'LT3_Grating', 'LT3_WhiteRoom',
        'LT3_WarningRed', 'LT3_Rubber', 'LT3_Hazard', 'LT3_LampWarm', 'LT3_LampRed', 'LT3_LampCyan', 'LT3_GlassDark']
STEEL, AMBER, GALV, GRATE, WHITE, RED, RUBBER, HAZARD, LWARM, LRED, LCYAN, GLASS = range(len(MATS))
# metres per texture tile
TILE = {STEEL: 2.0, AMBER: 2.0, GALV: 1.5, GRATE: 0.5, WHITE: 2.0, RED: 2.0, RUBBER: 1.0, HAZARD: 1.2,
        LWARM: 1.0, LRED: 1.0, LCYAN: 1.0, GLASS: 1.0}

PAD_Y = -32.6
PIVOT = (-0.9, 0.0, -6.9)


def P(x, y, z):
    return Vector((x, z, y))


def U(v):
    """Blender -> Unity tuple."""
    return (v.x, v.z, v.y)


class Mesh:
    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new('UVMap')

    # -- primitives (all inputs in Unity metres) -------------------------------------------------
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

    def box(self, c, s, mat):
        cx, cy, cz = c
        hx, hy, hz = s[0] / 2, s[1] / 2, s[2] / 2
        pts = [P(cx - hx, cy - hy, cz - hz), P(cx + hx, cy - hy, cz - hz), P(cx + hx, cy - hy, cz + hz), P(cx - hx, cy - hy, cz + hz),
               P(cx - hx, cy + hy, cz - hz), P(cx + hx, cy + hy, cz - hz), P(cx + hx, cy + hy, cz + hz), P(cx - hx, cy + hy, cz + hz)]
        return self._box8(pts, mat)

    def boxr(self, x0, x1, y0, y1, z0, z1, mat):
        return self.box(((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), (abs(x1 - x0), abs(y1 - y0), abs(z1 - z0)), mat)

    def beam(self, a, b, w, h, mat, up=(0, 1, 0), roll=0.0):
        """Rectangular member from a to b (Unity), width w (side axis), height h (up axis)."""
        A, Bv = P(*a), P(*b)
        d = Bv - A
        if d.length < 1e-5:
            return []
        d.normalize()
        upv = P(*up)
        if abs(d.dot(upv)) > 0.97:
            upv = P(1, 0, 0) if abs(d.dot(P(1, 0, 0))) < 0.9 else P(0, 0, 1)
        side = d.cross(upv).normalized()
        upv = side.cross(d).normalized()
        if roll:
            R = Matrix.Rotation(roll, 3, d)
            side = R @ side
            upv = R @ upv
        sw, sh = side * (w / 2), upv * (h / 2)
        pts = [A - sw - sh, A + sw - sh, A + sw + sh, A - sw + sh, Bv - sw - sh, Bv + sw - sh, Bv + sw + sh, Bv - sw + sh]
        return self._box8(pts, mat)

    def hbeam(self, a, b, depth, width, mat, up=(0, 1, 0), tf=0.03, tw=0.02):
        """Wide-flange member: web along `up`, flanges across."""
        A, Bv = Vector(a), Vector(b)
        d = (Bv - A).normalized()
        upv = Vector(up)
        if abs(d.dot(upv)) > 0.97:
            upv = Vector((1, 0, 0))
        side = d.cross(upv).normalized()
        upv = side.cross(d).normalized()
        off = upv * (depth / 2 - tf / 2)
        self.beam(tuple(A), tuple(Bv), tw, depth - 2 * tf, mat, up=tuple(upv))
        self.beam(tuple(A + off), tuple(Bv + off), width, tf, mat, up=tuple(upv))
        self.beam(tuple(A - off), tuple(Bv - off), width, tf, mat, up=tuple(upv))

    def tube(self, a, b, r, mat, seg=8, caps=True, r2=None):
        A, Bv = P(*a), P(*b)
        d = Bv - A
        if d.length < 1e-5:
            return
        dn = d.normalized()
        ref = P(0, 1, 0) if abs(dn.dot(P(0, 1, 0))) < 0.9 else P(1, 0, 0)
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

    def path(self, pts, r, mat, seg=6):
        for i in range(len(pts) - 1):
            self.tube(pts[i], pts[i + 1], r, mat, seg, caps=(i == 0 or i == len(pts) - 2))
        for p in pts[1:-1]:
            pass

    def plate(self, x0, x1, z0, z1, y, t, mat, frame_mat=None, frame=0.05):
        """Horizontal plate (top at y) with an optional edge frame (flat bar)."""
        self.boxr(x0, x1, y - t, y, z0, z1, mat)
        if frame_mat is not None:
            self.boxr(x0 - 0.01, x0 + 0.0, y - frame, y, z0, z1, frame_mat)
            self.boxr(x1, x1 + 0.01, y - frame, y, z0, z1, frame_mat)
            self.boxr(x0, x1, y - frame, y, z0 - 0.01, z0, frame_mat)
            self.boxr(x0, x1, y - frame, y, z1, z1 + 0.01, frame_mat)

    def rail_run(self, a, b, mat=AMBER, post_mat=AMBER, h=1.07, spacing=1.4, toe=True, posts=True):
        """Guard rail along the floor line a->b (Unity, y = walking surface): top + mid rail, posts, toe board."""
        ax, ay, az = a
        bx, by, bz = b
        L = math.dist(a, b)
        n = max(1, int(math.ceil(L / spacing)))
        self.tube((ax, ay + h, az), (bx, by + h, bz), 0.024, mat, 8)
        self.tube((ax, ay + h * 0.52, az), (bx, by + h * 0.52, bz), 0.019, mat, 6)
        if posts:
            for i in range(n + 1):
                t = i / n
                x, y, z = ax + (bx - ax) * t, ay + (by - ay) * t, az + (bz - az) * t
                self.beam((x, y, z), (x, y + h, z), 0.045, 0.045, post_mat, up=(1, 0, 0))
        if toe:
            self.beam((ax, ay + 0.06, az), (bx, by + 0.06, bz), 0.008, 0.12, post_mat)

    def lamp(self, c, facing, mat, size=0.32, housing=STEEL):
        """Floodlight: housing box + emissive lens facing `facing` (unit axis tuple)."""
        cx, cy, cz = c
        fx, fy, fz = facing
        d = 0.18
        self.box((cx - fx * d / 2, cy - fy * d / 2, cz - fz * d / 2), (size if fx == 0 else d, size * 0.8 if fy == 0 else d, size if fz == 0 else d), housing)
        lc = (cx + fx * 0.01, cy + fy * 0.01, cz + fz * 0.01)
        ls = (size * 0.8 if fx == 0 else 0.02, size * 0.6 if fy == 0 else 0.02, size * 0.8 if fz == 0 else 0.02)
        self.box(lc, ls, mat)

    # -- finish ----------------------------------------------------------------------------------
    def finish(self, origin_u=(0, 0, 0)):
        bm = self.bm
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0001)
        uv = self.uv
        for f in bm.faces:
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
        org = P(*origin_u)
        me.transform(Matrix.Translation(-org))
        for m in MATS:
            me.materials.append(bpy.data.materials[m])
        ob = bpy.data.objects.new(self.name, me)
        ob.location = org
        bpy.context.scene.collection.objects.link(ob)
        return ob


# =====================================================================================================
# materials (Blender preview only; Unity uses Materials/LT3_*.mat remapped by name)
# =====================================================================================================
TEX = os.path.join(ROOT, 'Textures')
FLAT = {LWARM: ((1.0, 0.78, 0.5), 18.0), LRED: ((1.0, 0.06, 0.03), 25.0), LCYAN: ((0.2, 0.9, 1.0), 12.0)}


def make_materials():
    for i, name in enumerate(MATS):
        m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        m.use_nodes = True
        nt = m.node_tree
        bsdf = nt.nodes.get('Principled BSDF')
        if i in FLAT:
            col, st = FLAT[i]
            bsdf.inputs['Base Color'].default_value = (*col, 1)
            bsdf.inputs['Emission'].default_value = (*col, 1)
            bsdf.inputs['Emission Strength'].default_value = st
            continue
        if i == GLASS:
            bsdf.inputs['Base Color'].default_value = (0.02, 0.025, 0.03, 1)
            bsdf.inputs['Roughness'].default_value = 0.05
            bsdf.inputs['Metallic'].default_value = 0.2
            continue
        base = os.path.join(TEX, name + '_BaseColor.png')
        ms = os.path.join(TEX, name + '_MetallicSmoothness.png')
        img = bpy.data.images.load(base, check_existing=True)
        tn = nt.nodes.new('ShaderNodeTexImage')
        tn.image = img
        nt.links.new(tn.outputs['Color'], bsdf.inputs['Base Color'])
        if i == GRATE:
            nt.links.new(tn.outputs['Alpha'], bsdf.inputs['Alpha'])
            m.blend_method = 'CLIP'
            m.shadow_method = 'CLIP'
            m.use_backface_culling = False
        mi = bpy.data.images.load(ms, check_existing=True)
        mi.colorspace_settings.name = 'Non-Color'
        mn = nt.nodes.new('ShaderNodeTexImage')
        mn.image = mi
        sep = nt.nodes.new('ShaderNodeSeparateColor')
        nt.links.new(mn.outputs['Color'], sep.inputs['Color'])
        nt.links.new(sep.outputs['Red'], bsdf.inputs['Metallic'])
        inv = nt.nodes.new('ShaderNodeMath')
        inv.operation = 'SUBTRACT'
        inv.inputs[0].default_value = 1.0
        nt.links.new(mn.outputs['Alpha'], inv.inputs[1])
        nt.links.new(inv.outputs[0], bsdf.inputs['Roughness'])
        npath = os.path.join(TEX, name + '_Normal.png')
        if os.path.exists(npath):
            ni = bpy.data.images.load(npath, check_existing=True)
            ni.colorspace_settings.name = 'Non-Color'
            nn = nt.nodes.new('ShaderNodeTexImage')
            nn.image = ni
            nm = nt.nodes.new('ShaderNodeNormalMap')
            nt.links.new(nn.outputs['Color'], nm.inputs['Color'])
            nt.links.new(nm.outputs['Normal'], bsdf.inputs['Normal'])


# =====================================================================================================
# TOWER (static)
# =====================================================================================================
TX = 2.5                    # legs at x +-2.5
TZ0, TZ1 = -13.4, -7.4      # rear / front leg lines
TOP = -0.68                 # top of the tower girders, directly under the crew deck structure
LEVELS = [PAD_Y + 0.35 + k * 2.88 for k in range(11)] + [TOP - 0.45]   # node levels (12)
LEGS = [(-TX, TZ0), (TX, TZ0), (TX, TZ1), (-TX, TZ1)]


def tower(m):
    y0 = PAD_Y
    # --- foundations on the barge deck: grout pads, base plates, stiffeners, anchor bolts
    for (x, z) in LEGS:
        m.boxr(x - 0.55, x + 0.55, y0, y0 + 0.22, z - 0.55, z + 0.55, GALV)
        m.boxr(x - 0.42, x + 0.42, y0 + 0.22, y0 + 0.27, z - 0.42, z + 0.42, STEEL)
        for sx in (-1, 1):
            m.boxr(min(x + sx * 0.12, x + sx * 0.4), max(x + sx * 0.12, x + sx * 0.4), y0 + 0.27, y0 + 0.55, z - 0.01, z + 0.01, STEEL)
            m.boxr(x - 0.01, x + 0.01, y0 + 0.27, y0 + 0.55, min(z + sx * 0.12, z + sx * 0.4), max(z + sx * 0.12, z + sx * 0.4), STEEL)
        for bx in (-0.32, 0.32):
            for bz in (-0.32, 0.32):
                m.tube((x + bx, y0 + 0.27, z + bz), (x + bx, y0 + 0.36, z + bz), 0.03, GALV, 6)
        # hazard guard collar at the foot of each leg
        m.boxr(x - 0.24, x + 0.24, y0 + 0.3, y0 + 1.5, z - 0.24, z + 0.24, HAZARD)
    # --- corner legs: wide-flange columns, splice plates at each level
    for (x, z) in LEGS:
        m.hbeam((x, y0 + 0.27, z), (x, TOP, z), 0.40, 0.38, STEEL, up=(1, 0, 0), tf=0.04, tw=0.03)
        for y in LEVELS[1:-1]:
            m.boxr(x - 0.23, x + 0.23, y + 0.9, y + 1.3, z - 0.215, z - 0.195, STEEL)
            m.boxr(x - 0.23, x + 0.23, y + 0.9, y + 1.3, z + 0.195, z + 0.215, STEEL)
    # --- faces: horizontals at every node level, X-bracing with gusset plates per panel
    faces = [((-TX, TZ0), (TX, TZ0)), ((TX, TZ0), (TX, TZ1)), ((TX, TZ1), (-TX, TZ1)), ((-TX, TZ1), (-TX, TZ0))]
    for fi, ((ax, az), (bx, bz)) in enumerate(faces):
        nx, nz = (bz - az), -(bx - ax)
        ln = math.hypot(nx, nz)
        nx, nz = nx / ln * 0.05, nz / ln * 0.05
        for k, y in enumerate(LEVELS):
            m.hbeam((ax, y, az), (bx, y, bz), 0.30, 0.18, STEEL, up=(0, 1, 0), tf=0.025, tw=0.018)
        for k in range(len(LEVELS) - 1):
            ya, yb = LEVELS[k] + 0.15, LEVELS[k + 1] - 0.15
            # the front face top panel stays open above the slew pedestal (retracted arm sweeps there)
            pa = (ax + nx, ya, az + nz)
            pb = (bx + nx, yb, bz + nz)
            pc = (bx + nx, ya, bz + nz)
            pd = (ax + nx, yb, az + nz)
            # double-angle diagonals (two thin flats each)
            for (s, e) in ((pa, pb), (pc, pd)):
                m.beam(s, e, 0.13, 0.10, STEEL, up=(nx, 0, nz))
            # gusset plates at the four panel corners and the crossing
            mid = ((ax + bx) / 2 + nx, (ya + yb) / 2, (az + bz) / 2 + nz)
            m.beam((mid[0] - (bx - ax) * 0.04, mid[1], mid[2] - (bz - az) * 0.04),
                   (mid[0] + (bx - ax) * 0.04, mid[1], mid[2] + (bz - az) * 0.04), 0.03, 0.34, STEEL, up=(0, 1, 0))
            for (cx, cy, cz) in (pa, pb, pc, pd):
                m.box((cx + (bx - ax) * (0.045 if abs(cx - (ax + nx)) < 1e-6 else -0.045), cy + (0.12 if cy == ya else -0.12), cz + (bz - az) * (0.045 if abs(cx - (ax + nx)) < 1e-6 and abs(cz - (az + nz)) < 1e-6 else -0.045)),
                      (0.45 if abs(bx - ax) > 0.1 else 0.03, 0.42, 0.45 if abs(bz - az) > 0.1 else 0.03), STEEL)
    # --- plan bracing every third level
    for k in range(0, len(LEVELS), 3):
        y = LEVELS[k] - 0.1
        m.beam((-TX, y, TZ0), (TX, y, TZ1), 0.12, 0.10, STEEL)
        m.beam((TX, y, TZ0), (-TX, y, TZ1), 0.12, 0.10, STEEL)
    # --- service platforms (grating) with toe boards and amber rails
    for k in (3, 6, 9):
        y = LEVELS[k] + 0.15
        m.plate(-TX + 0.2, TX - 0.2, TZ0 + 0.2, TZ1 - 0.2, y + 0.04, 0.04, GRATE, STEEL)
        # joists
        for z in (TZ0 + 1.5, (TZ0 + TZ1) / 2, TZ1 - 1.5):
            m.beam((-TX, y - 0.1, z), (TX, y - 0.1, z), 0.1, 0.18, STEEL)
        for (ax, az), (bx, bz) in faces:
            if ax == TX and bx == TX:
                # stair-side face: gate opening between z -10.6 and -9.4
                m.rail_run((ax - 0.25, y + 0.04, az + 0.25 if az < bz else az - 0.25), (ax - 0.25, y + 0.04, -10.6))
                m.rail_run((ax - 0.25, y + 0.04, -9.4), (bx - 0.25, y + 0.04, bz - 0.25 if bz > az else bz + 0.25))
                continue
            sx = -0.25 if ax > 0 else 0.25
            sz = -0.25 if az > -10.4 else 0.25
            ex = -0.25 if bx > 0 else 0.25
            ez = -0.25 if bz > -10.4 else 0.25
            m.rail_run((ax + sx, y + 0.04, az + sz), (bx + ex, y + 0.04, bz + ez))
        # bridge to the stair tower
        m.plate(TX - 0.2, 2.75, -10.6, -9.4, y + 0.04, 0.04, GRATE, STEEL)

    # --- tower head: deep girders carrying the crew deck (top at TOP), cantilever knee braces to the deck edges
    for z in (TZ0, -10.4, TZ1):
        m.hbeam((-4.0, TOP - 0.3, z), (4.0, TOP - 0.3, z), 0.60, 0.30, STEEL, tf=0.04, tw=0.025)
    for x in (-TX, TX, -3.85, 3.85):
        m.hbeam((x, TOP - 0.25, -12.9), (x, TOP - 0.25, TZ1 + 0.15), 0.50, 0.26, STEEL, tf=0.035, tw=0.02)
    for x in (-TX, TX):
        sx = 1 if x > 0 else -1
        for z in (TZ0 + 0.1, -10.4, TZ1):
            m.beam((x, LEVELS[-2] + 0.5, z), (x + sx * 1.35, TOP - 0.55, z), 0.2, 0.2, STEEL)
    # bearing pads under the crew deck
    for x in (-3.85, -TX, TX, 3.85):
        for z in (-12.6, -10.4, -7.6):
            m.boxr(x - 0.22, x + 0.22, TOP - 0.02, TOP + 0.03, z - 0.22, z + 0.22, GALV)

    # --- slew pedestal for the access arm (static half of the slewing bearing)
    px, _, pz = PIVOT
    m.boxr(px - 0.32, px + 0.32, TOP - 2.6, TOP - 0.06, pz - 0.32, pz + 0.32, STEEL)
    m.tube((px, TOP - 0.06, pz), (px, TOP + 0.02, pz), 0.46, GALV, 20)
    for i in range(16):
        a = 2 * math.pi * i / 16
        m.tube((px + 0.4 * math.cos(a), TOP + 0.02, pz + 0.4 * math.sin(a)), (px + 0.4 * math.cos(a), TOP + 0.06, pz + 0.4 * math.sin(a)), 0.022, GALV, 6)
    for x in (px - 0.25, px + 0.25):
        m.beam((x, TOP - 2.5, pz - 0.2), (x if abs(x) < TX else x, LEVELS[-3] + 0.6, TZ1), 0.16, 0.16, STEEL)
        m.beam((x, TOP - 1.2, pz - 0.2), (x, TOP - 0.6, TZ1), 0.16, 0.16, STEEL)
    m.beam((px, TOP - 2.55, pz), (-TX, LEVELS[-2], TZ1), 0.16, 0.16, STEEL)
    m.lamp((px, TOP - 2.7, pz), (0, -1, 0), LRED, 0.18)   # slew zone warning beacon

    # --- caged ladder (secondary egress) on the -x face
    lx, lz = -TX - 0.55, -12.2
    for sx in (-0.22, 0.22):
        m.beam((lx, PAD_Y + 0.3, lz + sx), (lx, TOP - 0.3, lz + sx), 0.06, 0.02, GALV, up=(1, 0, 0))
    y = PAD_Y + 0.6
    while y < TOP - 0.4:
        m.tube((lx, y, lz - 0.22), (lx, y, lz + 0.22), 0.014, GALV, 5, caps=False)
        y += 0.3
    for k in range(1, len(LEVELS)):
        y = LEVELS[k]
        m.beam((lx, y, lz - 0.22), (lx - 0.5, y, lz - 0.32), 0.04, 0.008, GALV)
        m.beam((lx, y, lz + 0.22), (lx - 0.5, y, lz + 0.32), 0.04, 0.008, GALV)
        m.beam((lx - 0.5, y, lz - 0.32), (lx - 0.5, y, lz + 0.32), 0.04, 0.008, GALV)
        m.beam((-TX, y, lz), (lx, y, lz), 0.06, 0.06, GALV)
    for dz in (-0.3, 0.0, 0.3):
        m.beam((lx - 0.5, LEVELS[1], lz + dz), (lx - 0.5, LEVELS[-1], lz + dz), 0.04, 0.008, GALV, up=(1, 0, 0))

    # --- cable trays: vertical ladder tray on the -x face, horizontal run under the deck to the arm pivot
    cx, cz = -TX - 0.45, -9.0
    for dz in (-0.3, 0.3):
        m.beam((cx, PAD_Y + 0.3, cz + dz), (cx, TOP - 0.6, cz + dz), 0.10, 0.03, GALV, up=(1, 0, 0))
    y = PAD_Y + 0.6
    while y < TOP - 0.6:
        m.beam((cx, y, cz - 0.3), (cx, y, cz + 0.3), 0.04, 0.02, GALV)
        y += 0.6
    for k in range(1, len(LEVELS)):
        m.beam((-TX, LEVELS[k] + 0.4, cz), (cx, LEVELS[k] + 0.4, cz), 0.08, 0.08, GALV)
    for i, (dx, dz, r, mat) in enumerate(((0.06, -0.15, 0.045, RUBBER), (0.06, 0.0, 0.038, RUBBER), (0.06, 0.14, 0.05, RED), (0.1, 0.07, 0.03, RUBBER))):
        m.tube((cx + dx, PAD_Y + 0.4, cz + dz), (cx + dx, TOP - 0.95, cz + dz), r, mat, 6, caps=False)
        m.tube((cx + dx, TOP - 0.95, cz + dz), (cx + 0.5 + dx, TOP - 0.85 + 0.0, cz + dz), r, mat, 6, caps=False)
        m.tube((cx + 0.5 + dx, TOP - 0.85, cz + dz), (PIVOT[0] - 0.3, TOP - 0.85, -7.25 + dz * 0.5), r, mat, 6, caps=False)
    m.boxr(-TX - 0.2, -TX + 0.35, TOP - 1.0, TOP - 0.7, -9.4, -8.6, GALV)          # junction box
    m.boxr(-TX + 0.35, PIVOT[0] - 0.2, TOP - 0.95, TOP - 0.92, -7.5, -7.05, GALV)  # horizontal tray

    # --- deluge / fire water riser (red) on the front-right leg, drencher header under the deck
    rx, rz = TX - 0.6, TZ1 + 0.35
    m.tube((rx, PAD_Y + 0.25, rz), (rx, TOP - 1.0, rz), 0.11, RED, 12, caps=False)
    for k in range(len(LEVELS) - 1):
        y = LEVELS[k] + 1.4
        m.tube((rx, y - 0.04, rz), (rx, y + 0.04, rz), 0.16, RED, 12)
        m.beam((rx, y + 0.3, rz), (rx, y + 0.3, TZ1), 0.06, 0.06, STEEL)
    m.tube((rx, TOP - 1.0, rz), (-3.6, TOP - 1.0, rz), 0.08, RED, 10)
    for i in range(9):
        x = -3.4 + i * 0.8
        m.tube((x, TOP - 1.0, rz), (x, TOP - 1.22, rz), 0.03, GALV, 6)
        m.tube((x, TOP - 1.22, rz), (x, TOP - 1.28, rz), 0.06, GALV, 8, r2=0.02)
    # valve station on the barge deck
    m.boxr(rx - 0.4, rx + 0.4, PAD_Y, PAD_Y + 0.15, rz - 0.4, rz + 0.4, GALV)
    m.tube((rx, PAD_Y + 0.9, rz), (rx + 0.45, PAD_Y + 0.9, rz), 0.09, RED, 10)
    m.tube((rx + 0.45, PAD_Y + 0.9, rz), (rx + 0.45, PAD_Y + 1.35, rz), 0.03, GALV, 6)
    m.tube((rx + 0.45, PAD_Y + 1.35, rz - 0.01), (rx + 0.45, PAD_Y + 1.35, rz + 0.01), 0.22, RED, 14)

    # --- umbilical swing arms (retracted pose, carrier plates parked 1.2 m off the booster skin)
    for uy in (LEVELS[4] + 0.6, LEVELS[8] + 0.6):
        x0 = 1.0
        zt = -4.3
        for dx in (-0.35, 0.35):
            for dy in (0.0, 0.7):
                m.beam((x0 + dx, uy + dy, TZ1), (x0 + dx, uy + dy, zt), 0.1, 0.1, STEEL)
        n = 4
        for i in range(n + 1):
            z = TZ1 + (zt - TZ1) * i / n
            for dx in (-0.35, 0.35):
                m.beam((x0 + dx, uy, z), (x0 + dx, uy + 0.7, z), 0.06, 0.06, STEEL, up=(1, 0, 0))
            m.beam((x0 - 0.35, uy + 0.7, z), (x0 + 0.35, uy + 0.7, z), 0.06, 0.06, STEEL)
            if i < n:
                z2 = TZ1 + (zt - TZ1) * (i + 1) / n
                for dx in (-0.35, 0.35):
                    m.beam((x0 + dx, uy, z), (x0 + dx, uy + 0.7, z2), 0.05, 0.05, STEEL, up=(1, 0, 0))
        m.boxr(x0 - 0.5, x0 + 0.5, uy - 0.25, uy + 0.95, zt - 0.06, zt, HAZARD)  # carrier plate
        m.hbeam((x0, uy - 0.6, TZ1 - 0.15), (x0, uy + 1.3, TZ1 - 0.15), 0.3, 0.3, STEEL, up=(1, 0, 0))  # hinge post
        m.tube((x0, uy - 0.6, TZ1 - 0.15), (x0, uy + 1.3, TZ1 - 0.15), 0.12, GALV, 10)
        # hoses: along the arm, drooping in a service loop below the carrier plate
        for j, (dx, r, mat) in enumerate(((-0.22, 0.07, RUBBER), (0.0, 0.06, RED), (0.22, 0.05, GALV))):
            pts = []
            for i in range(9):
                t = i / 8
                z = TZ1 + (zt + 0.05 - TZ1) * t
                sag = -0.75 * math.sin(math.pi * t) ** 1.2
                pts.append((x0 + dx, uy + 0.25 + sag, z))
            m.path(pts, r, mat, 6)
        m.lamp((x0, uy + 1.0, zt + 0.1), (0, 0, 1), LWARM, 0.22)

    # --- upper lighting / lightning mast behind the crew deck (silhouette), rear plane z TZ0 and TZ0-0.55
    mz0, mz1 = TZ0, TZ0 - 0.55
    top_y, top_w = 14.5, 0.75
    ys = [TOP + i * (top_y - TOP) / 6 for i in range(7)]

    def wx(y):
        return TX + (top_w - TX) * (y - TOP) / (top_y - TOP)
    for sx in (-1, 1):
        m.hbeam((sx * TX, TOP - 0.6, mz0), (sx * top_w, top_y, mz0), 0.32, 0.30, STEEL, up=(1, 0, 0), tf=0.03, tw=0.022)
        m.beam((sx * (TX - 0.3), TOP - 0.6, mz1), (sx * top_w, top_y, mz1), 0.16, 0.16, STEEL)
    for i, y in enumerate(ys):
        w = wx(y)
        m.beam((-w, y, mz0), (w, y, mz0), 0.16, 0.2, STEEL)
        m.beam((-w, y, mz1), (w, y, mz1), 0.10, 0.12, STEEL)
        for sx in (-1, 1):
            m.beam((sx * w, y, mz0), (sx * w, y, mz1), 0.08, 0.08, STEEL)
        if i < 6:
            y2 = ys[i + 1]
            w2 = wx(y2)
            m.beam((-w, y, mz0 - 0.08), (w2, y2, mz0 - 0.08), 0.1, 0.08, STEEL, up=(0, 0, 1))
            m.beam((w, y, mz0 - 0.08), (-w2, y2, mz0 - 0.08), 0.1, 0.08, STEEL, up=(0, 0, 1))
            for sx in (-1, 1):
                m.beam((sx * w, y, mz0), (sx * w2, y2, mz1), 0.07, 0.07, STEEL)
    # mast head: lightning rod, obstruction lights
    m.boxr(-1.0, 1.0, top_y, top_y + 0.25, mz1 - 0.2, mz0 + 0.2, STEEL)
    m.tube((0, top_y + 0.25, (mz0 + mz1) / 2), (0, top_y + 9.0, (mz0 + mz1) / 2), 0.16, GALV, 10, r2=0.04)
    for i in range(4):
        y = top_y + 0.25 + i * 2.0
        m.tube((0, y, (mz0 + mz1) / 2), (0, y + 0.12, (mz0 + mz1) / 2), 0.2 - i * 0.03, STEEL, 10)
    m.tube((0, top_y + 9.0, (mz0 + mz1) / 2), (0, top_y + 9.25, (mz0 + mz1) / 2), 0.09, LRED, 8)
    for sx in (-1, 1):
        m.tube((sx * 0.9, top_y + 0.25, (mz0 + mz1) / 2), (sx * 0.9, top_y + 0.45, (mz0 + mz1) / 2), 0.1, LRED, 8)
        m.tube((sx * wx(ys[3]) + sx * 0.15, ys[3] + 0.1, mz0), (sx * wx(ys[3]) + sx * 0.15, ys[3] + 0.3, mz0), 0.08, LRED, 8)
    # floodlight gallery facing the vehicle at y ~ +8
    gy = ys[4]
    gw = wx(gy)
    m.plate(-gw - 0.2, gw + 0.2, mz0 + 0.05, mz0 + 0.9, gy + 0.04, 0.04, GRATE, STEEL)
    m.rail_run((-gw - 0.2, gy + 0.04, mz0 + 0.9), (gw + 0.2, gy + 0.04, mz0 + 0.9), spacing=1.0)
    for x in (-gw + 0.1, -0.4, 0.4, gw - 0.1):
        m.lamp((x, gy + 0.55, mz0 + 0.75), (0, 0, 1), LWARM, 0.42)
    # floodlights on the tower head corners, aimed at the arm and the booster
    for x in (-3.7, 3.7):
        m.lamp((x, TOP - 0.85, -7.0), (0, 0, 1), LWARM, 0.36)
    # cable tray up the mast
    m.beam((1.4, TOP - 0.6, mz1 - 0.15), (0.55, top_y, mz1 - 0.15), 0.3, 0.05, GALV, up=(0, 0, 1))


def stair_tower(m):
    """Switchback stair tower on the +x face: 22 flights of 1.45 m, landings at both ends, own frame."""
    x0, x1 = 2.75, 4.55
    z0, z1 = -12.6, -8.2
    lane = [(2.8, 3.62), (3.68, 4.5)]
    za, zb = -11.6, -9.25      # flight run (2.35 m)
    rise = 1.45
    ybot = PAD_Y
    nfl = int((TOP - 0.5 - ybot) / rise)
    # frame posts and ties
    for (x, z) in ((x0, z0), (x1, z0), (x1, z1), (x0, z1)):
        m.beam((x, ybot, z), (x, TOP - 0.5, z), 0.16, 0.16, STEEL)
        m.boxr(x - 0.2, x + 0.2, ybot, ybot + 0.05, z - 0.2, z + 0.2, GALV)
    for i in range(nfl + 1):
        y = ybot + i * rise
        for (a, b) in (((x0, z0), (x1, z0)), ((x1, z0), (x1, z1)), ((x1, z1), (x0, z1))):
            m.beam((a[0], y - 0.05, a[1]), (b[0], y - 0.05, b[1]), 0.12, 0.14, STEEL)
        if i % 2 == 0 and i < nfl:
            y2 = y + 2 * rise
            m.beam((x1, y, z0), (x1, y2, z1), 0.08, 0.08, STEEL, up=(1, 0, 0))
            m.beam((x1, y, z1), (x1, y2, z0), 0.08, 0.08, STEEL, up=(1, 0, 0))
    for i in range(nfl):
        y = ybot + i * rise
        up_dir = i % 2 == 0
        lx0, lx1 = lane[0] if up_dir else lane[1]
        s, e = (za, zb) if up_dir else (zb, za)
        # landings at both ends of the flight
        if up_dir:
            m.plate(x0 + 0.05, x1 - 0.05, zb, z1 - 0.05, y + rise, 0.04, GRATE, STEEL)
        else:
            m.plate(x0 + 0.05, x1 - 0.05, z0 + 0.05, za, y + rise, 0.04, GRATE, STEEL)
        # stringers (channels)
        for x in (lx0, lx1):
            m.beam((x, y - 0.12, s), (x, y + rise - 0.12, e), 0.04, 0.26, STEEL)
        # treads
        nt = 8
        for t in range(1, nt):
            f = t / nt
            z = s + (e - s) * f
            yy = y + rise * f
            m.boxr(lx0 + 0.02, lx1 - 0.02, yy - 0.035, yy, z - 0.13, z + 0.13, GRATE)
            m.boxr(lx0 + 0.02, lx1 - 0.02, yy - 0.04, yy, z + (0.13 if e > s else -0.13) - 0.015, z + (0.13 if e > s else -0.13) + 0.015, AMBER)
        # sloped handrails on both stringers
        for x in (lx0 + 0.02, lx1 - 0.02):
            m.tube((x, y + 0.95, s), (x, y + rise + 0.95, e), 0.022, AMBER, 6, caps=False)
            m.tube((x, y + 0.5, s), (x, y + rise + 0.5, e), 0.018, AMBER, 5, caps=False)
            for f in (0.08, 0.5, 0.92):
                z = s + (e - s) * f
                m.beam((x, y + rise * f, z), (x, y + rise * f + 0.95, z), 0.04, 0.04, AMBER, up=(1, 0, 0))
    # landing guard rails on the outer face
    for i in range(1, nfl + 1):
        y = ybot + i * rise
        m.tube((x1 - 0.05, y + 1.0, z0 + 0.05), (x1 - 0.05, y + 1.0, z1 - 0.05), 0.022, AMBER, 6, caps=False)
    # top landing and roof canopy under the crew deck
    m.boxr(x0 - 0.1, x1 + 0.1, TOP + 0.05, TOP + 0.1, z0 - 0.1, z1 + 0.1, STEEL)
    m.lamp((x1 + 0.05, ybot + 3.0, (z0 + z1) / 2), (1, 0, 0), LWARM, 0.26)
    m.lamp((x1 + 0.05, ybot + 16.0, (z0 + z1) / 2), (1, 0, 0), LWARM, 0.26)


# =====================================================================================================
# CREW ACCESS ARM + WHITE ROOM (moves with "Access arm control"; origin = pivot)
# =====================================================================================================
AZ0, AZ1 = -6.9, -2.62       # arm rear (deck joint) / front (boot seal face)
WR0 = -4.75                  # white room rear wall
CH = 1.06                    # truss chord line x +-1.06
TOPC = 2.56                  # top chord


def access_arm(m):
    # floor: grating deck on cross beams, edge channels, non-slip plate in the white room
    m.plate(-0.96, 0.96, AZ0, WR0, 0.0, 0.035, GRATE)
    m.boxr(-0.96, 0.96, -0.035, 0.0, WR0, AZ1 - 0.02, GALV)
    m.boxr(-0.96, 0.96, -0.004, 0.003, AZ0, AZ0 + 0.16, HAZARD)          # threshold stripe at the deck joint
    m.boxr(-0.96, 0.96, -0.004, 0.003, AZ1 - 0.2, AZ1 - 0.02, HAZARD)     # threshold stripe at the hatch
    for sx in (-1, 1):
        m.boxr(sx * 0.96, sx * 1.02, -0.2, 0.0, AZ0, AZ1, STEEL)
    z = AZ0 + 0.1
    while z < AZ1:
        m.beam((-CH, -0.12, z), (CH, -0.12, z), 0.08, 0.16, STEEL)
        z += 0.62
    # bottom chords and plan bracing
    for sx in (-1, 1):
        m.hbeam((sx * CH, -0.48, AZ0), (sx * CH, -0.48, AZ1 - 0.1), 0.16, 0.12, STEEL)
    nb = 4
    zs = [AZ0 + (AZ1 - 0.1 - AZ0) * i / nb for i in range(nb + 1)]
    for i in range(nb):
        m.beam((-CH, -0.5, zs[i]), (CH, -0.5, zs[i + 1]), 0.06, 0.06, STEEL)
        m.beam((CH, -0.5, zs[i]), (-CH, -0.5, zs[i + 1]), 0.06, 0.06, STEEL)
    # side trusses (Pratt): verticals, diagonals, top chords, portal frames
    for sx in (-1, 1):
        x = sx * CH
        m.hbeam((x, TOPC, AZ0), (x, TOPC, AZ1 - 0.1), 0.16, 0.12, STEEL)
        for i, z in enumerate(zs):
            m.beam((x, -0.56, z), (x, TOPC + 0.08, z), 0.12, 0.12, STEEL, up=(1, 0, 0))
            if i < nb and zs[i + 1] <= WR0 + 0.05:
                m.beam((x, -0.4, z), (x, TOPC - 0.05, zs[i + 1]), 0.08, 0.08, STEEL, up=(1, 0, 0))
        # gussets at the joints
        for z in zs:
            for y in (-0.42, TOPC - 0.04):
                m.boxr(x - 0.012, x + 0.012, y - 0.14, y + 0.14, z - 0.16, z + 0.16, STEEL)
    for z in zs:
        m.beam((-CH, TOPC, z), (CH, TOPC, z), 0.1, 0.1, STEEL)
        # knee braces at the portal
        for sx in (-1, 1):
            m.beam((sx * CH, TOPC - 0.45, z), (sx * (CH - 0.4), TOPC, z), 0.06, 0.06, STEEL)
    # roof: standing-seam sheet over the whole arm, slight overhang
    m.boxr(-1.2, 1.2, TOPC + 0.08, TOPC + 0.12, AZ0 - 0.1, AZ1 - 0.12, STEEL)
    x = -1.15
    while x <= 1.16:
        m.boxr(x - 0.012, x + 0.012, TOPC + 0.12, TOPC + 0.16, AZ0 - 0.1, AZ1 - 0.12, STEEL)
        x += 0.3
    for sx in (-1, 1):
        m.boxr(sx * 1.2 - 0.02, sx * 1.2 + 0.02, TOPC - 0.05, TOPC + 0.12, AZ0 - 0.1, AZ1 - 0.12, AMBER)
    # handrails along the open walkway (inside the truss), collider-matching x +-0.955, top rail 1.07
    for sx in (-1, 1):
        m.rail_run((sx * 0.955, 0.0, AZ0 + 0.05), (sx * 0.955, 0.0, WR0 - 0.05), spacing=1.07)
    # walkway lights under the roof
    for z in (-6.3, -5.25):
        m.lamp((0, TOPC - 0.02, z), (0, -1, 0), LWARM, 0.3)

    # ---------------- white room (z WR0..AZ1) ----------------
    wx0, wx1 = 1.07, 1.15     # wall panel thickness outside the chord line
    zf = AZ1 - 0.12
    for sx in (-1, 1):
        xin, xout = sx * wx0, sx * wx1
        lo, hi = min(xin, xout), max(xin, xout)
        # side wall with a window
        m.boxr(lo, hi, -0.55, 0.95, WR0, zf, WHITE)
        m.boxr(lo, hi, 1.75, TOPC + 0.08, WR0, zf, WHITE)
        m.boxr(lo, hi, 0.95, 1.75, WR0, WR0 + 0.45, WHITE)
        m.boxr(lo, hi, 0.95, 1.75, zf - 0.35, zf, WHITE)
        m.boxr(lo + 0.02, hi - 0.02, 0.95, 1.75, WR0 + 0.45, zf - 0.35, GLASS)
        # window frame
        for (y0, y1, za, zb) in ((0.9, 0.95, WR0 + 0.4, zf - 0.3), (1.75, 1.8, WR0 + 0.4, zf - 0.3),
                                 (0.9, 1.8, WR0 + 0.4, WR0 + 0.45), (0.9, 1.8, zf - 0.35, zf - 0.3)):
            m.boxr(lo - 0.015 * (sx < 0), hi + 0.015 * (sx > 0), y0, y1, za, zb, STEEL)
        # amber hazard skirt band and corner trims
        m.boxr(lo - 0.006 * (sx < 0), hi + 0.006 * (sx > 0), -0.55, -0.25, WR0, zf, HAZARD)
        m.boxr(lo - 0.02 * (sx < 0), hi + 0.02 * (sx > 0), -0.55, TOPC + 0.1, WR0 - 0.04, WR0 + 0.04, STEEL)
        m.boxr(lo - 0.02 * (sx < 0), hi + 0.02 * (sx > 0), -0.55, TOPC + 0.1, zf - 0.04, zf + 0.04, STEEL)
    # rear wall with the walk-through door (opening x +-0.95, y 0..2.24)
    for sx in (-1, 1):
        m.boxr(sx * 0.95, sx * wx1, -0.05, TOPC + 0.08, WR0 - 0.04, WR0 + 0.04, WHITE) if sx > 0 else \
            m.boxr(-wx1, -0.95, -0.05, TOPC + 0.08, WR0 - 0.04, WR0 + 0.04, WHITE)
    m.boxr(-wx1, wx1, 2.24, TOPC + 0.08, WR0 - 0.04, WR0 + 0.04, WHITE)
    m.boxr(-0.95, 0.95, 2.24, 2.32, WR0 - 0.06, WR0 + 0.06, HAZARD)              # door header stripe
    m.lamp((0.0, 2.45, WR0 - 0.06), (0, 0, -1), LCYAN, 0.14)                      # status lamp over the door
    m.lamp((0.0, 2.45, WR0 + 0.06), (0, 0, 1), LCYAN, 0.14)
    # ceiling and interior light strip
    m.boxr(-wx0, wx0, 2.38, 2.42, WR0, zf, WHITE)
    m.boxr(-0.14, 0.14, 2.35, 2.38, WR0 + 0.25, zf - 0.25, LWARM)
    # interior grab bars (x +-1.0, off the walkway)
    for sx in (-1, 1):
        m.tube((sx * 1.02, 1.05, WR0 + 0.3), (sx * 1.02, 1.05, zf - 0.3), 0.017, AMBER, 6)
        for z in (WR0 + 0.3, zf - 0.3):
            m.beam((sx * 1.02, 1.05, z), (sx * 1.07, 1.05, z), 0.03, 0.03, AMBER)
    # boot seal at the hatch face: rubber bellows around the opening, stepped frame
    for i, (inset, y0) in enumerate(((0.0, -0.08), (0.04, -0.06), (0.08, -0.04))):
        zz0, zz1 = zf + i * 0.035, zf + (i + 1) * 0.035
        m.boxr(-wx1 + inset, -0.99, y0, TOPC - inset, zz0, zz1, RUBBER)
        m.boxr(0.99, wx1 - inset, y0, TOPC - inset, zz0, zz1, RUBBER)
        m.boxr(-wx1 + inset, wx1 - inset, 2.3, TOPC - inset, zz0, zz1, RUBBER)
    m.boxr(-wx1, wx1, 2.24, 2.3, zf - 0.02, zf + 0.1, HAZARD)

    # ---------------- slewing drive (rotating half), at the pivot under the floor ----------------
    px, _, pz = PIVOT
    m.tube((px, -0.62, pz), (px, -0.2, pz), 0.42, STEEL, 20)
    m.tube((px, -0.2, pz), (px, -0.16, pz), 0.46, GALV, 20)
    m.boxr(px + 0.35, px + 0.75, -0.55, -0.2, pz + 0.1, pz + 0.45, STEEL)           # drive motor
    m.tube((px + 0.55, -0.62, pz + 0.27), (px + 0.55, -0.55, pz + 0.27), 0.12, GALV, 10)
    m.beam((px, -0.4, pz), (-CH, -0.4, pz + 0.9), 0.14, 0.14, STEEL)
    m.beam((px, -0.4, pz), (CH, -0.48, pz + 0.3), 0.14, 0.14, STEEL)

    # ---------------- energy chain and service lines on the +x outside ----------------
    ex = CH + 0.2
    m.boxr(ex - 0.12, ex + 0.12, -0.6, -0.56, AZ0 + 0.1, WR0, GALV)                 # chain trough
    z = AZ0 + 0.15
    while z < WR0 - 0.1:
        m.boxr(ex - 0.09, ex + 0.09, -0.56, -0.44, z, z + 0.1, RUBBER)
        z += 0.13
    for j, (dy, r, mat) in enumerate(((0.05, 0.04, RED), (0.16, 0.035, GALV), (0.27, 0.03, RUBBER))):
        m.tube((ex + 0.02, dy, AZ0 + 0.1), (ex + 0.02, dy, zf - 0.2), r, mat, 6)
        m.tube((ex + 0.02, dy, AZ0 + 0.1), (ex + 0.02, -0.5, AZ0 + 0.1), r, mat, 6)
    for z in (AZ0 + 0.6, -5.6, WR0 + 0.4, -3.4):
        m.beam((CH, 0.15, z), (ex + 0.07, 0.15, z), 0.05, 0.3, STEEL)
    m.lamp((ex + 0.02, 0.6, AZ0 + 0.3), (1, 0, 0), LRED, 0.12)                      # arm-moving beacon


# =====================================================================================================
# stand-ins for previews only (never exported)
# =====================================================================================================
def standins():
    import bmesh as bm_
    col = bpy.data.collections.new('Standins')
    bpy.context.scene.collection.children.link(col)
    grey = bpy.data.materials.new('Standin')
    grey.use_nodes = True
    grey.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.55, 0.56, 0.58, 1)
    grey.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value = 0.6
    dark = bpy.data.materials.new('StandinDark')
    dark.use_nodes = True
    dark.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.08, 0.085, 0.09, 1)

    def add(mesh, mat):
        ob = bpy.data.objects.new(mesh.name, mesh)
        ob.data.materials.append(mat)
        col.objects.link(ob)
        return ob
    s = Mesh('SI_tmp')
    s.boxr(-18, 18, PAD_Y - 1.0, PAD_Y, -14, 10, 0)
    s.boxr(-4, 4, -0.6, 0.0, -12.9, -6.9, 0)
    for z in (-12.9, -6.9):
        for x0, x1 in ((-4, -1.0), (1.0, 4)) if z == -6.9 else ((-4, 4),):
            s.boxr(x0, x1, 1.05, 1.1, z - 0.03, z + 0.03, 0)
    for x in (-4, 4):
        s.boxr(x - 0.03, x + 0.03, 1.05, 1.1, -12.9, -6.9, 0)
    s.boxr(-2.5, -0.5, 0.0, 2.6, -12.4, -10.5, 0)   # cargo cabin hint on the deck
    me = bpy.data.meshes.new('SI_site')
    s.bm.to_mesh(me)
    s.bm.free()
    add(me, grey)
    # booster + capsule (cylinders about the y axis)
    bpy.ops.mesh.primitive_cylinder_add(vertices=48, radius=2.895, depth=31.0, location=P(0, PAD_Y + 15.5 + 1.5, 0))
    b = bpy.context.active_object
    b.data.materials.append(grey)
    if b.name not in col.objects:
        col.objects.link(b)
    bpy.ops.mesh.primitive_cone_add(vertices=48, radius1=2.577, radius2=0.9, depth=5.0, location=P(0, -0.5 + 2.5, 0))
    c = bpy.context.active_object
    c.data.materials.append(grey)
    if c.name not in col.objects:
        col.objects.link(c)
    return col


# =====================================================================================================
def build():
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob)
    make_materials()
    t = Mesh('LTV3_Tower')
    tower(t)
    stair_tower(t)
    tower_ob = t.finish((0, 0, 0))
    a = Mesh('LTV3_AccessArm')
    access_arm(a)
    arm_ob = a.finish(PIVOT)
    return [tower_ob, arm_ob]


def report(objs):
    rep = {'asset': 'LaunchTowerV03', 'frame': 'Unity metres, prologue root, identity', 'objects': {}, 'materials': MATS}
    total = 0
    for ob in objs:
        me = ob.data
        me.calc_loop_triangles()
        tris = len(me.loop_triangles)
        by = {}
        for tr in me.loop_triangles:
            n = MATS[tr.material_index]
            by[n] = by.get(n, 0) + 1
        ws = [ob.matrix_world @ v.co for v in me.vertices]
        mn = [min(U(v)[i] for v in ws) for i in range(3)]
        mx = [max(U(v)[i] for v in ws) for i in range(3)]
        rep['objects'][ob.name] = {'tris': tris, 'verts': len(me.vertices), 'origin': [round(c, 4) for c in U(ob.location)],
                                   'bounds_min': [round(c, 3) for c in mn], 'bounds_max': [round(c, 3) for c in mx],
                                   'tris_by_material': by}
        total += tris
    rep['total_triangles'] = total
    return rep


def corridor_check(arm_ob, tower_ob):
    """Vertices of the arm/tower inside the keep-clear corridors (connected pose)."""
    boxes = {'arm_walkway': ((-0.93, 0.93), (0.02, 2.2), (-6.9, -2.5)),
             'hatch_doorway': ((-0.95, 0.95), (0.02, 2.2), (-3.4, -2.3)),
             'deck': ((-3.9, 3.9), (0.05, 2.5), (-12.8, -7.0))}
    res = {}
    for name, (bx, by, bz) in boxes.items():
        hits = 0
        for ob in (arm_ob, tower_ob):
            for v in ob.data.vertices:
                x, y, z = U(ob.matrix_world @ v.co)
                if bx[0] < x < bx[1] and by[0] < y < by[1] and bz[0] < z < bz[1]:
                    hits += 1
        res[name] = hits
    return res


def render(objs, out_dir):
    sc = bpy.context.scene
    standins()
    sc.render.engine = 'BLENDER_EEVEE'
    sc.eevee.use_gtao = True
    sc.eevee.use_bloom = True
    sc.eevee.use_soft_shadows = True
    sc.eevee.shadow_cascade_size = '4096'
    sc.render.resolution_x, sc.render.resolution_y = 1600, 900
    sc.view_settings.view_transform = 'Filmic'
    sc.view_settings.look = 'Medium High Contrast'
    w = bpy.data.worlds.new('W')
    w.use_nodes = True
    w.node_tree.nodes['Background'].inputs['Color'].default_value = (0.32, 0.38, 0.47, 1)
    w.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.6
    sc.world = w
    sun = bpy.data.lights.new('Sun', 'SUN')
    sun.energy = 3.2
    sun.angle = 0.02
    so = bpy.data.objects.new('Sun', sun)
    so.rotation_euler = (math.radians(52), math.radians(8), math.radians(-38))
    sc.collection.objects.link(so)
    cam = bpy.data.cameras.new('C')
    co = bpy.data.objects.new('C', cam)
    sc.collection.objects.link(co)
    sc.camera = co
    views = {
        'overview': ((-30, -6, 28), (0, -12, -9), 35),
        'arm_walk': ((0.0, 1.62, -8.6), (0.0, 1.2, -2.4), 22),
        'arm_side': ((7.5, 2.8, -1.5), (-0.3, 0.5, -5.5), 30),
        'base': ((14, -29.5, -24), (0, -24, -9), 28),
        'whiteroom': ((0.3, 1.55, -5.6), (0.0, 1.1, -2.6), 18),
        'mast': ((9, 4, 6), (0, 4, -12), 26),
    }
    for name, (loc, tgt, lens) in views.items():
        cam.lens = lens
        cam.clip_end = 400
        co.location = P(*loc)
        d = P(*tgt) - P(*loc)
        co.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(out_dir, 'LTV3_%s.png' % name)
        bpy.ops.render.render(write_still=True)
        print('rendered', name)


def export(objs, out_dir):
    for m in bpy.data.materials:
        if m.node_tree:
            for n in [n for n in m.node_tree.nodes if n.type == 'TEX_IMAGE']:
                m.node_tree.nodes.remove(n)
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objs:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    fbx = os.path.join(out_dir, 'LaunchTowerV03.fbx')
    bpy.ops.export_scene.fbx(
        filepath=fbx, use_selection=True, object_types={'MESH'}, apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True, axis_forward='-Z', axis_up='Y',
        use_mesh_modifiers=False, mesh_smooth_type='FACE', use_triangles=True, add_leaf_bones=False,
        bake_anim=False, path_mode='STRIP', embed_textures=False, use_custom_props=False)
    print('exported', fbx)


if __name__ == '__main__':
    objs = build()
    rep = report(objs)
    rep['keep_clear_vertices'] = corridor_check(objs[1], objs[0])
    os.makedirs(os.path.join(ROOT, 'Models'), exist_ok=True)
    with open(os.path.join(ROOT, 'Models', 'LaunchTowerV03_report.json'), 'w') as f:
        json.dump(rep, f, indent=1)
    print(json.dumps({k: (v['tris'] if isinstance(v, dict) and 'tris' in v else v) for k, v in rep['objects'].items()}),
          rep['total_triangles'], rep['keep_clear_vertices'])
    if SAVE:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, 'Source~', 'LaunchTowerV03.blend'), relative_remap=True, compress=True)
    if RENDER:
        os.makedirs(os.path.join(ROOT, 'Previews~'), exist_ok=True)
        render(objs, os.path.join(ROOT, 'Previews~'))
    if EXPORT:
        export(objs, os.path.join(ROOT, 'Models'))
