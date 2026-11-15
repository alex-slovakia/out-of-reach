"""SUITS-V04 EVA: male MakeHuman CC0 body + CC0 garments, inflated into a layered EVA suit, with modelled hard goods
(PLSS, chest control pack, helmet, rings, harness), on the UNCHANGED FitV02 rig (164 bones).
blender -b --factory-startup --python build_v04_eva.py -- [--render out_prefix] [--export dir] [--save blend] [--samples n]"""
import bpy, bmesh, sys, os, json, math
sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import numpy as np
from mathutils import Vector, Matrix
from mathutils.kdtree import KDTree
from mathutils.bvhtree import BVHTree
import mhio

ROOT = os.path.dirname(HERE)
INP = os.path.join(ROOT, 'inputs')
SA = os.path.join(INP, 'mh_sa')
TEX = os.path.join(ROOT, 'out', 'tex')
argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []


def arg(k, d=None):
    return argv[argv.index(k) + 1] if k in argv else d


S, TY = 0.1065109473396234, 0.875583853
MORPH = json.loads(arg('--morph', '{"caucasian-male-young": 0.85, "universal-male-young-maxmuscle-averageweight": 0.30, "universal-male-young-averagemuscle-minweight": 0.30}'))
VARIANT = 'EVA'

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=os.path.join(INP, 'A7_BodyFit02_EVA.fbx'), automatic_bone_orientation=False)
rig = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
for o in [o for o in bpy.data.objects if o.type == 'MESH']:
    bpy.data.objects.remove(o, do_unlink=True)
rig.data.pose_position = 'REST'
BONES = [b.name for b in rig.data.bones]
print('RIG', rig.name, len(BONES), 'ACTIONS', [a.name for a in bpy.data.actions], 'MW_IDENTITY', rig.matrix_world == Matrix.Identity(4))

# ---------------- male body in the rig's bind (identical to WORK) ----------------
V0, UV0, F0 = mhio.read_obj(os.path.join(INP, 'base.obj'))
D = np.zeros_like(V0)
for name, w in MORPH.items():
    idx, d = mhio.read_target(os.path.join(INP, 'targets', name + '.target'))
    D[idx] += d * w
skel = json.load(open(os.path.join(INP, 'default.mhskel')))
mhw = mhio.read_mhw(os.path.join(INP, 'default_weights.mhw'))
W = mhio.weight_matrix(mhw, len(V0), BONES)
J0 = mhio.joints(V0, skel)
bones_mh = skel['bones']
ARM_PREFIX = ('upperarm', 'lowerarm', 'wrist', 'metacarpal', 'finger')
arm_cols = [i for i, bn in enumerate(BONES) if bn.startswith(ARM_PREFIX)]
arm_mask = np.clip(W[:, arm_cols].sum(1) * 1.4, 0, 1)
V = V0 + D * (1 - arm_mask)[:, None]
J1 = mhio.joints(V, skel)
dJ = np.zeros((len(BONES), 3))
for i, bn in enumerate(BONES):
    if bn in bones_mh:
        h = bones_mh[bn]['head']
        dJ[i] = J0[h] - J1[h]
V = V + W @ dJ
J2 = mhio.joints(V, skel)
errs = sorted(((float(np.linalg.norm(J2[k] - J0[k]) * S), k) for k in J0), reverse=True)
print('HAND_JOINT_MAX_M', round(max(e for e, k in errs if any(t in k for t in ('wrist', 'finger', 'metacarpal', 'hand', 'lowerarm'))), 4))

clothes = {}
for key, path in (('suit', 'clothes/male_casualsuit02/male_casualsuit02.mhclo'), ('boots', 'clothes/shoes03/shoes03.mhclo'),
                  ('hair', 'hair/short02/short02.mhclo'), ('eyes', 'eyes/high-poly/high-poly.mhclo')):
    clothes[key] = mhio.read_mhclo(os.path.join(SA, path))
delete = set()
for key in ('suit', 'boots'):
    delete |= clothes[key]['delete']


def mat(name, tex=None, normal=None, color=(0.5, 0.5, 0.5, 1), rough=0.7, metal=0.0, alpha=False, alpha_value=None,
        sss=0.0, tint=None, emit=None, emit_strength=0.0, emit_tex=False):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = color
    b.inputs['Roughness'].default_value = rough
    b.inputs['Metallic'].default_value = metal
    if sss:
        b.inputs['Subsurface Weight'].default_value = sss
        b.inputs['Subsurface Radius'].default_value = (1.0, 0.35, 0.2)
    t = None
    if tex:
        t = nt.nodes.new('ShaderNodeTexImage'); t.image = bpy.data.images.load(tex, check_existing=True)
        if tint:
            mix = nt.nodes.new('ShaderNodeMix'); mix.data_type = 'RGBA'; mix.blend_type = 'MULTIPLY'
            mix.inputs['Factor'].default_value = 1.0; mix.inputs[7].default_value = tint
            nt.links.new(t.outputs['Color'], mix.inputs[6]); nt.links.new(mix.outputs[2], b.inputs['Base Color'])
        else:
            nt.links.new(t.outputs['Color'], b.inputs['Base Color'])
        if alpha:
            nt.links.new(t.outputs['Alpha'], b.inputs['Alpha'])
    if alpha_value is not None:
        b.inputs['Alpha'].default_value = alpha_value
    if alpha or alpha_value is not None:
        if hasattr(m, 'blend_method'):
            m.blend_method = 'HASHED'
    if emit is not None:
        b.inputs['Emission Color'].default_value = emit
        b.inputs['Emission Strength'].default_value = emit_strength
        if emit_tex and t is not None:
            nt.links.new(t.outputs['Color'], b.inputs['Emission Color'])
    if normal:
        n = nt.nodes.new('ShaderNodeTexImage'); n.image = bpy.data.images.load(normal, check_existing=True)
        n.image.colorspace_settings.name = 'Non-Color'
        nm = nt.nodes.new('ShaderNodeNormalMap'); nm.inputs['Strength'].default_value = 0.8
        nt.links.new(n.outputs['Color'], nm.inputs['Color']); nt.links.new(nm.outputs['Normal'], b.inputs['Normal'])
    return m


def lin(h):
    h = h.lstrip('#')
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c) + (1.0,)


M_SKIN = mat('MAT_V04E_Skin', os.path.join(SA, 'skins/young_caucasian_male/young_lightskinned_male_diffuse.png'), rough=0.52, sss=0.12, tint=(0.80, 0.60, 0.48, 1))
M_SUIT = mat('MAT_V04E_Suit', os.path.join(TEX, 'V04E_Suit_BaseColor.png'), os.path.join(TEX, 'V04_Suit_Normal.png'), rough=0.84)
M_PANEL = mat('MAT_V04E_Panel', color=lin('#CAC4B8'), rough=0.82)
M_RED = mat('MAT_V04E_Red', color=lin('#B32A22'), rough=0.70)
M_WEB = mat('MAT_V04E_Webbing', color=lin('#A9AAA5'), rough=0.86)
M_TI = mat('MAT_V04E_Titanium', color=lin('#A4ADB3'), rough=0.33, metal=0.85)
M_FLEX = mat('MAT_V04E_FlexGrey', color=lin('#6A7379'), rough=0.84)
M_GLOVE = mat('MAT_V04E_Glove', color=lin('#D5D2C8'), rough=0.80)
M_RUBBER = mat('MAT_V04E_BootRubber', color=lin('#34393E'), rough=0.92)
M_HARD = mat('MAT_V04E_Hard', os.path.join(TEX, 'V04E_Hard_BaseColor.png'), rough=0.55)
M_DARK = mat('MAT_V04E_DarkPanel', color=lin('#3A4045'), rough=0.50, metal=0.2)
M_DISP = mat('MAT_V04E_Display', os.path.join(TEX, 'V04E_Display.png'), rough=0.25, emit=(1, 1, 1, 1), emit_strength=1.6, emit_tex=True)
M_YEL = mat('MAT_V04E_Yellow', color=lin('#D9A62B'), rough=0.40, metal=0.4)
M_GLASS = mat('MAT_V04E_Glass', color=lin('#E4ECEF'), rough=0.04, alpha_value=0.10)
M_VISOR = mat('MAT_V04E_Visor', color=lin('#D7A13F'), rough=0.16, metal=1.0)
M_LAMP = mat('MAT_V04E_Lamp', color=lin('#FFF4DC'), rough=0.2, emit=(1.0, 0.93, 0.80, 1), emit_strength=6.0)
M_HAIR = mat('MAT_V04E_Hair', os.path.join(SA, 'hair/short02/short02_diffuse.png'), rough=0.55, alpha=True, tint=(0.13, 0.10, 0.085, 1))
M_EYE = mat('MAT_V04E_Eyes', os.path.join(SA, 'eyes/materials/brown_eye.png'), rough=0.15, alpha=True, tint=(0.80, 0.78, 0.76, 1))


def finish(ob, smooth=True):
    for p in ob.data.polygons:
        p.use_smooth = smooth
    ob.parent = rig        # mesh coordinates are armature space (as WORK): no parent inverse
    m = ob.modifiers.new('Armature', 'ARMATURE'); m.object = rig
    return ob


def make_mesh(name, P_mh, uvs, faces, weightsW, material, keep=None):
    P = mhio.mh_to_blender(P_mh, S, TY)
    me = bpy.data.meshes.new(name)
    fl = [(f[0], f[1]) for f in faces if keep is None or keep(f)]
    me.from_pydata([tuple(p) for p in P], [], [f[0] for f in fl])
    me.update()
    uvl = me.uv_layers.new(name='UVMap')
    for poly, (vi, ti) in zip(me.polygons, fl):
        for k, loop in enumerate(poly.loop_indices):
            t = ti[k] if k < len(ti) else -1
            uvl.data[loop].uv = tuple(uvs[t]) if t >= 0 else (0.0, 0.0)
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    ob.data.materials.append(material)
    for i, b in enumerate(BONES):
        col = weightsW[:, i]
        nz = np.nonzero(col > 1e-4)[0]
        if len(nz) == 0:
            continue
        g = ob.vertex_groups.new(name=b)
        for vi in nz:
            g.add([int(vi)], float(col[vi]), 'REPLACE')
    bm = bmesh.new(); bm.from_mesh(me)
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context='VERTS'); bm.to_mesh(me); bm.free()
    return finish(ob)


def clothing(key, objrel, material, offset_scale=1.0, own_weights=None):
    clo = clothes[key]
    Vc, UVc, Fc = mhio.read_obj(os.path.join(SA, objrel))
    P, (i1, i2, i3, w) = mhio.fit_clothes(V, clo, offset_scale)
    if own_weights and os.path.exists(own_weights):
        Wc = mhio.weight_matrix(mhio.read_mhw(own_weights), len(P), BONES)
    else:
        Wc = W[i1] * w[:, :1] + W[i2] * w[:, 1:2] + W[i3] * w[:, 2:3]
        s = Wc.sum(1, keepdims=True); s[s == 0] = 1; Wc = Wc / s
    return make_mesh('V04E_' + key, P, UVc, Fc, Wc, material)


def coords(ob):
    n = len(ob.data.vertices)
    co = np.empty(n * 3, np.float32); ob.data.vertices.foreach_get('co', co)
    return co.reshape(-1, 3)


def normals(ob):
    n = len(ob.data.vertices)
    nr = np.empty(n * 3, np.float32); ob.data.vertices.foreach_get('normal', nr)
    return nr.reshape(-1, 3)


def set_coords(ob, co):
    ob.data.vertices.foreach_set('co', co.astype(np.float32).ravel()); ob.data.update()


def bone_weight_rows(ob):
    gi = {g.index: g.name for g in ob.vertex_groups}
    rows = []
    for v in ob.data.vertices:
        rows.append({gi[g.group]: g.weight for g in v.groups if g.weight > 1e-4})
    return rows


def outward_sign(co, nr, sel):
    c = co[sel].mean(0)
    return 1.0 if float(np.mean(np.sum(nr[sel] * (co[sel] - c), 1))) >= 0 else -1.0


def smooth01(x):
    x = np.clip(x, 0, 1)
    return x * x * (3 - 2 * x)


def bpos(name, tail=False):
    b = rig.data.bones[name]
    return Vector(b.tail_local if tail else b.head_local)


body = make_mesh('V04E_Body', V, UV0, F0, W, M_SKIN,
                 keep=lambda f: f[2] == 'body' and not any(v in delete for v in f[0]))
suit = clothing('suit', 'clothes/male_casualsuit02/male_casualsuit02.obj', M_SUIT, 1.0)
boots = clothing('boots', 'clothes/shoes03/shoes03.obj', M_HARD, 1.25, os.path.join(SA, 'clothes/shoes03/shoes03.mhw'))
hair = clothing('hair', 'hair/short02/short02.obj', M_HAIR, 1.0)
eyes = clothing('eyes', 'eyes/high-poly/high-poly.obj', M_EYE, 1.0)
co = coords(eyes)                     # MakeHuman eyes bulge out of the male-morphed lids: seat them 3 mm deeper, 10 % smaller
for sgn in (1.0, -1.0):
    sel = co[:, 0] * sgn > 0
    c = co[sel].mean(0)
    co[sel] = c + (co[sel] - c) * 0.90 + np.array([0, 0.003, 0], np.float32)
set_coords(eyes, co)

# ---------------- inflate the garment into a layered EVA suit (thickness blended by bone weights) ----------------
T_BONE = [(('upperarm', 'shoulder01'), 0.029), (('lowerarm',), 0.024), (('wrist', 'metacarpal', 'finger'), 0.008),
          (('spine', 'breast', 'clavicle', 'root', 'pelvis'), 0.030), (('neck',), 0.010), (('head',), 0.004),
          (('upperleg',), 0.029), (('lowerleg',), 0.025), (('foot', 'toe'), 0.012)]


def bone_t(bn):
    for pre, t in T_BONE:
        if bn.startswith(pre):
            return t
    return 0.012


co = coords(suit); nr = normals(suit)
rows = bone_weight_rows(suit)
t = np.array([sum(w * bone_t(b) for b, w in r.items()) / max(1e-6, sum(r.values())) for r in rows], np.float32)
for side, sgn in (('L', 1.0), ('R', -1.0)):          # sleeve narrows into the wrist ring
    wj = np.array(bpos('wrist.' + side)); el = np.array(bpos('lowerarm01.' + side))
    ax = (wj - el) / np.linalg.norm(wj - el)
    s = (co - wj) @ ax                                 # 0 at the wrist joint, negative toward the elbow
    near = (co[:, 0] * sgn > 0.15)
    f = 0.30 + 0.70 * smooth01((-s - 0.02) / 0.10)
    t = np.where(near, t * f, t)
t *= np.where(co[:, 2] < 0.16, 0.55 + 0.45 * smooth01((co[:, 2] - 0.06) / 0.10), 1.0)
torso = (co[:, 2] > 1.10) & (co[:, 2] < 1.40) & (np.abs(co[:, 0]) < 0.13)
sg = outward_sign(co, nr, torso)
set_coords(suit, co + nr * sg * t[:, None])
print('SUIT_INFLATED sign', sg, 'mean_t', round(float(t.mean()), 4))

# boots: bulkier shell, flat sole, colour zones
co = coords(boots); nr = normals(boots)
zmin = float(co[:, 2].min())
sg = outward_sign(co, nr, co[:, 2] > 0.06)
co2 = co + nr * sg * 0.011
co2[:, 2] = np.maximum(co2[:, 2], zmin)
ZTOP, ZFLOOR = 0.11, -0.014                # MakeHuman boot sole sits ~6 cm below the rig origin: compress the foot part
low = co2[:, 2] < ZTOP
co2[low, 2] = ZTOP - (ZTOP - co2[low, 2]) * (ZTOP - ZFLOOR) / (ZTOP - zmin)
zmin = ZFLOOR
set_coords(boots, co2)
boots.data.materials.append(M_RUBBER); boots.data.materials.append(M_FLEX)
for side, sgn in (('L', 1.0), ('R', -1.0)):
    sel = co2[:, 0] * sgn > 0
    ymin, ymax = float(co2[sel, 1].min()), float(co2[sel, 1].max())
    L = ymax - ymin
    for p in boots.data.polygons:
        c = p.center
        if c.x * sgn <= 0:
            continue
        if c.z < zmin + 0.026:
            p.material_index = 1
        elif c.z < zmin + 0.070 and (c.y < ymin + 0.22 * L or c.y > ymax - 0.12 * L):
            p.material_index = 2
print('BOOTS', zmin)

# gloves: hand skin becomes a glove shell (fingers 2.5 mm, hand 4 mm); palm side and fingertips flex grey
co = coords(body); nr = normals(body)
head_sel = co[:, 2] > 1.62
sgb = outward_sign(co, nr, head_sel)
rows = bone_weight_rows(body)
body.data.materials.append(M_GLOVE); body.data.materials.append(M_FLEX)
glove_v = np.zeros(len(co), bool)
palm_dirs = {}
for side, sgn in (('L', 1.0), ('R', -1.0)):
    wj = np.array(bpos('wrist.' + side)); el = np.array(bpos('lowerarm01.' + side))
    ax = (wj - el) / np.linalg.norm(wj - el)
    s = (co - wj) @ ax
    sel = (co[:, 0] * sgn > 0.15) & (s > -0.09) & (co[:, 2] < 1.30)
    glove_v |= sel
    a = (bpos('finger3-1.' + side) - bpos('wrist.' + side)).normalized()
    th = bpos('finger1-3.' + side, tail=True) - bpos('wrist.' + side)
    th = (th - a * th.dot(a)).normalized()
    palm_dirs[side] = (a.cross(th) * sgn).normalized()
tg = np.zeros(len(co), np.float32)
for i in np.nonzero(glove_v)[0]:
    r = rows[i]
    dom = max(r, key=r.get) if r else ''
    tg[i] = 0.0025 if dom.startswith('finger') else 0.004
set_coords(body, co + nr * sgb * tg[:, None])
for p in body.data.polygons:
    vs = list(p.vertices)
    if not all(glove_v[v] for v in vs):
        continue
    side = 'L' if p.center.x > 0 else 'R'
    doms = []
    for v in vs:
        r = rows[v]; doms.append(max(r, key=r.get) if r else '')
    tip = sum(1 for d in doms if d.startswith('finger') and d.split('.')[0].endswith('-3')) >= len(vs) - 1
    palm = (Vector(p.normal) * sgb).dot(palm_dirs[side]) > 0.25
    p.material_index = 2 if (palm or tip) else 1
print('GLOVES', int(glove_v.sum()), {k: tuple(round(x, 2) for x in v) for k, v in palm_dirs.items()})

# ---------------- surface details projected onto the suit ----------------
dg = bpy.context.evaluated_depsgraph_get()
bvh_suit = BVHTree.FromObject(suit, dg)
bvh_body = BVHTree.FromObject(body, dg)
kd_src = []
for src in (suit, body):
    gi = {g.index: g.name for g in src.vertex_groups}
    for v in src.data.vertices:
        kd_src.append((v.co.copy(), [(gi[g.group], g.weight) for g in v.groups]))
kd = KDTree(len(kd_src))
for i, (c, _) in enumerate(kd_src):
    kd.insert(c, i)
kd.balance()


def skin_like_suit(ob):
    for v in ob.data.vertices:
        _, idx, _ = kd.find(v.co)
        for name_g, wg in kd_src[idx][1]:
            vg = ob.vertex_groups.get(name_g) or ob.vertex_groups.new(name=name_g)
            vg.add([v.index], wg, 'REPLACE')


def ray_first(origin, d, trees):
    best = None
    for tr in trees:
        hit = tr.ray_cast(origin, d, 2.0)
        if hit[0] is not None and (best is None or hit[3] < best[3]):
            best = hit
    return best


def surface_patch(name, material, centre_fn, n_u, n_v, lift=0.004, solid=0.004, trees=None, closed_u=False):
    trees = trees or [bvh_suit]
    verts, dirs = [], []
    nu = n_u if closed_u else n_u + 1
    for j in range(n_v + 1):
        for i in range(nu):
            origin, target = centre_fn(i / n_u, j / n_v)
            d = (target - origin).normalized()
            hit = ray_first(origin, d, trees)
            p = hit[0] + hit[1] * lift if hit is not None else target
            verts.append(p); dirs.append(d)
    faces = []
    for j in range(n_v):
        for i in range(n_u):
            a = j * nu + i
            b = j * nu + (i + 1) % nu if closed_u else a + 1
            faces.append((a, b, b + nu, a + nu))
    me = bpy.data.meshes.new(name); me.from_pydata([tuple(v) for v in verts], [], faces); me.update()
    md = Vector((0, 0, 0))
    for d in dirs:
        md += d
    if me.polygons and sum((Vector(p.normal).dot(dirs[p.vertices[0]]) for p in me.polygons)) > 0:
        bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.reverse_faces(bm, faces=bm.faces[:]); bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
    ob.data.materials.append(material)
    if solid:
        sm = ob.modifiers.new('solid', 'SOLIDIFY'); sm.thickness = solid; sm.offset = 1.0
        with bpy.context.temp_override(object=ob, active_object=ob):
            bpy.ops.object.modifier_apply(modifier='solid')
    skin_like_suit(ob)
    return finish(ob)


def ring_basis(ax, ref=Vector((0, 0, 1))):
    sidev = ax.cross(ref).normalized(); upv = sidev.cross(ax).normalized()
    return sidev, upv


details = []
for side, sgn in (('L', 1.0), ('R', -1.0)):
    # red band on the outer upper arm (reference: short red stripe below the shoulder)
    h, tl = bpos('upperarm02.' + side), bpos('upperarm02.' + side, tail=True)
    ax = (tl - h).normalized()
    out = Vector((sgn, 0, 0)); out = (out - ax * out.dot(ax)).normalized()
    fwd = ax.cross(out).normalized()
    if fwd.y > 0:
        fwd = -fwd

    def band(u, v, h=h, ax=ax, out=out, fwd=fwd, L=(tl - h).length):
        ang = math.radians(-62 + 124 * u)
        c = h + ax * (L * (0.18 + 0.20 * v))
        d = out * math.cos(ang) + fwd * math.sin(ang)
        return c + d * 0.10, c
    details.append(surface_patch('V04E_armband_' + side, M_RED, band, 10, 2, lift=0.002, solid=0.0025))
    # elbow / forearm convolutes
    el, wj = bpos('lowerarm01.' + side), bpos('wrist.' + side)
    axf = (wj - el).normalized(); sv, uv = ring_basis(axf)
    for k, sd in enumerate((0.010, 0.035, 0.060)):
        def conv(u, v, sd=sd, el=el, axf=axf, sv=sv, uv=uv):
            ang = 2 * math.pi * u
            c = el + axf * (sd + 0.010 * v)
            d = sv * math.cos(ang) + uv * math.sin(ang)
            return c + d * 0.10, c
        details.append(surface_patch('V04E_elbowrib_%s%d' % (side, k), M_PANEL, conv, 28, 1, lift=0.0015, solid=0.005, closed_u=True))
    # titanium wrist disconnect ring over the sleeve end, then the glove gauntlet
    sv, uv = ring_basis((wj - bpos('lowerarm02.' + side)).normalized())
    axw = (wj - bpos('lowerarm02.' + side)).normalized()

    def wring(u, v, wj=wj, axw=axw, sv=sv, uv=uv):
        ang = 2 * math.pi * u
        c = wj - axw * (0.048 - 0.024 * v)
        d = sv * math.cos(ang) + uv * math.sin(ang)
        return c + d * 0.09, c
    details.append(surface_patch('V04E_wristring_' + side, M_TI, wring, 32, 2, lift=0.005, solid=0.008, trees=[bvh_suit, bvh_body], closed_u=True))

    def gaunt(u, v, wj=wj, axw=axw, sv=sv, uv=uv):
        ang = 2 * math.pi * u
        c = wj - axw * (0.030 - 0.048 * v)
        d = sv * math.cos(ang) + uv * math.sin(ang)
        return c + d * 0.09, c
    details.append(surface_patch('V04E_gauntlet_' + side, M_GLOVE, gaunt, 32, 4, lift=0.0035, solid=0.003, trees=[bvh_suit, bvh_body], closed_u=True))
    # legs: red stripe above the knee, knee convolutes, outer thigh pocket with flap, shin pocket
    kn = bpos('lowerleg01.' + side)
    hl, tl2 = bpos('upperleg02.' + side), kn
    axl = (tl2 - hl).normalized()

    def leg_c(z, hl=hl, axl=axl, kn=kn):
        if z >= kn.z:
            f = (hl.z - z) / (hl.z - kn.z)
            return hl.lerp(kn, max(0.0, min(1.0, f)))
        an = bpos('lowerleg02.' + side, tail=True)
        f = (kn.z - z) / (kn.z - an.z)
        return kn.lerp(an, max(0.0, min(1.0, f)))

    def front_strip(z0, z1, a0, a1, sgn=sgn):
        def fn(u, v):
            z = z0 + (z1 - z0) * v
            ang = math.radians(a0 + (a1 - a0) * u)
            c = leg_c(z)
            d = Vector((sgn * math.sin(ang), -math.cos(ang), 0))
            return c + d * 0.15, Vector((c.x, c.y, z))
        return fn
    details.append(surface_patch('V04E_kneered_' + side, M_RED, front_strip(kn.z + 0.070, kn.z + 0.092, -40, 40), 8, 1, lift=0.002, solid=0.0025))
    for k, dz in enumerate((0.040, 0.004, -0.032)):
        details.append(surface_patch('V04E_kneerib_%s%d' % (side, k), M_PANEL, front_strip(kn.z + dz, kn.z + dz + 0.012, -85, 85), 14, 1, lift=0.0015, solid=0.007))
    details.append(surface_patch('V04E_thighpocket_' + side, M_PANEL, front_strip(0.615, 0.755, 62, 112), 6, 6, lift=0.002, solid=0.014))
    details.append(surface_patch('V04E_thighflap_' + side, M_PANEL, front_strip(0.740, 0.790, 58, 116), 6, 2, lift=0.016, solid=0.008))
    details.append(surface_patch('V04E_thightab_' + side, M_FLEX, front_strip(0.700, 0.745, 82, 92), 2, 2, lift=0.017, solid=0.003))
    details.append(surface_patch('V04E_shinpocket_' + side, M_PANEL, front_strip(0.300, 0.400, 70, 112), 5, 4, lift=0.002, solid=0.010))
    details.append(surface_patch('V04E_ankleband_' + side, M_WEB, front_strip(0.165, 0.200, -180, 180), 32, 1, lift=0.003, solid=0.005))
    details.append(surface_patch('V04E_thighloop_' + side, M_WEB, front_strip(0.800, 0.835, -55, 235), 26, 1, lift=0.003, solid=0.005))

    def vstrap(u, v, sgn=sgn):
        z = 0.990 - 0.165 * v
        x = sgn * (0.040 + 0.075 * v + 0.034 * (u - 0.5))
        return Vector((x, -0.6, z)), Vector((x, 0.05, z))
    details.append(surface_patch('V04E_vstrap_' + side, M_WEB, vstrap, 2, 8, lift=0.003, solid=0.005))
    for k, fr in enumerate((0.58, 0.74)):
        def urib(u, v, h=h, ax=ax, out=out, fwd=fwd, L=(tl - h).length, fr=fr):
            ang = math.radians(-125 + 250 * u)
            c = h + ax * (L * (fr + 0.07 * v))
            d = out * math.cos(ang) + fwd * math.sin(ang)
            return c + d * 0.10, c
        details.append(surface_patch('V04E_upperrib_%s%d' % (side, k), M_PANEL, urib, 20, 1, lift=0.0015, solid=0.005))

# waist belt and harness straps (light webbing) with titanium buckles
belt_c = Vector((0, 0.0, 1.0))


def belt(u, v):
    ang = 2 * math.pi * u
    z = 0.995 + 0.040 * v
    c = Vector((0, 0.0, z))
    return c + Vector((math.cos(ang), math.sin(ang), 0)) * 0.35, c
details.append(surface_patch('V04E_belt', M_WEB, belt, 48, 2, lift=0.003, solid=0.006, closed_u=True))
for side, sgn in (('L', 1.0), ('R', -1.0)):
    def strap_front(u, v, sgn=sgn):
        z = 1.25 + 0.24 * v
        x = sgn * (0.085 + 0.035 * v + 0.036 * (u - 0.5))
        return Vector((x, -0.6, z)), Vector((x, 0.05, z))
    details.append(surface_patch('V04E_strap_' + side, M_WEB, strap_front, 2, 8, lift=0.003, solid=0.005))

    def strap_top(u, v, sgn=sgn):
        y = -0.06 + 0.20 * v
        x = sgn * (0.120 + 0.036 * (u - 0.5))
        return Vector((x, y, 2.0)), Vector((x, y, 1.30))
    details.append(surface_patch('V04E_strapsh_' + side, M_WEB, strap_top, 2, 6, lift=0.003, solid=0.005))
print('DETAILS', len(details))

# ---------------- hard goods (rigid, weighted to one bone) ----------------
HARD = []


def rigid(ob, bone, material, smooth=False):
    ob.data.materials.clear(); ob.data.materials.append(material)
    g = ob.vertex_groups.new(name=bone)
    g.add(list(range(len(ob.data.vertices))), 1.0, 'REPLACE')
    finish(ob, smooth)
    HARD.append(ob)
    return ob


def apply_all(ob):
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


def box(name, size, loc, material, bone, bevel=0.0, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc, rotation=rot)
    ob = bpy.context.active_object; ob.name = name; ob.scale = size
    apply_all(ob)
    if bevel:
        bm_ = ob.modifiers.new('bev', 'BEVEL'); bm_.width = bevel; bm_.segments = 2; bm_.limit_method = 'ANGLE'
        with bpy.context.temp_override(object=ob, active_object=ob):
            bpy.ops.object.modifier_apply(modifier='bev')
    return rigid(ob, bone, material)


def cyl(name, r, depth, loc, material, bone, rot=(0, 0, 0), n=24):
    bpy.ops.mesh.primitive_cylinder_add(vertices=n, radius=r, depth=depth, location=loc, rotation=rot)
    ob = bpy.context.active_object; ob.name = name
    apply_all(ob)
    return rigid(ob, bone, material, smooth=True)


def torus(name, R, r, loc, material, bone, rot=(0, 0, 0), nM=48, nm=10):
    bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=r, major_segments=nM, minor_segments=nm, location=loc, rotation=rot)
    ob = bpy.context.active_object; ob.name = name
    apply_all(ob)
    return rigid(ob, bone, material, smooth=True)


sc_ = coords(suit)


def extent(axis, fn, xr, zr):
    sel = (np.abs(sc_[:, 0]) < xr) & (sc_[:, 2] > zr[0]) & (sc_[:, 2] < zr[1])
    return float(fn(sc_[sel, axis]))


RX = math.radians(90)
# chest control pack (reference: box with display, keypad, knobs and a yellow connector ring above the belt)
yF = extent(1, np.min, 0.10, (1.09, 1.27))
cz = 1.180; dep = 0.072; py = yF - dep / 2 - 0.002
box('V04E_chestpack', (0.255, dep, 0.165), (0, py, cz), M_HARD, 'spine02', bevel=0.010)
fy = py - dep / 2
box('V04E_dispframe', (0.118, 0.004, 0.074), (-0.048, fy - 0.001, cz + 0.030), M_DARK, 'spine02')
box('V04E_display', (0.100, 0.003, 0.060), (-0.048, fy - 0.003, cz + 0.030), M_DISP, 'spine02')
for r_ in range(4):
    for c_ in range(2):
        box('V04E_key%d%d' % (r_, c_), (0.014, 0.007, 0.011), (0.040 + c_ * 0.022, fy - 0.003, cz + 0.060 - r_ * 0.019), M_DARK, 'spine02', bevel=0.002)
for k, x in enumerate((-0.090, -0.050, -0.010, 0.085)):
    cyl('V04E_knob%d' % k, 0.012, 0.014, (x, fy - 0.007, cz - 0.048), M_DARK, 'spine02', rot=(RX, 0, 0), n=16)
torus('V04E_yelring', 0.014, 0.0045, (0.040, fy - 0.006, cz - 0.048), M_YEL, 'spine02', rot=(RX, 0, 0), nM=24, nm=8)
box('V04E_packplate', (0.085, 0.004, 0.030), (0.0, fy - 0.001, cz - 0.073), M_TI, 'spine02')
yF2 = extent(1, np.min, 0.07, (1.28, 1.38))
box('V04E_chestpouch', (0.150, 0.055, 0.090), (0, yF2 - 0.030, 1.322), M_PANEL, 'spine01', bevel=0.012)
box('V04E_pouchflap', (0.154, 0.059, 0.026), (0, yF2 - 0.031, 1.360), M_PANEL, 'spine01', bevel=0.008)
for sgn in (1, -1):
    box('V04E_buckle%d' % sgn, (0.042, 0.010, 0.028), (sgn * 0.105, py + 0.012 - dep / 2 - 0.004, cz + 0.095), M_TI, 'spine02', bevel=0.002)
    box('V04E_beltbuckle%d' % sgn, (0.040, 0.010, 0.034), (sgn * 0.070, extent(1, np.min, 0.12, (0.99, 1.04)) - 0.009, 1.015), M_TI, 'spine04', bevel=0.003)
# PLSS backpack (reference: tall box with lid panel, handle, red stripe, connector plate, lower canister, hose, antenna)
yB = extent(1, np.max, 0.16, (1.07, 1.56))
pd = 0.200; pyB = yB + pd / 2 + 0.002; bz0, bz1 = 1.075, 1.605
box('V04E_plss', (0.405, pd, bz1 - bz0), (0, pyB, (bz0 + bz1) / 2), M_HARD, 'spine01', bevel=0.022)
by = pyB + pd / 2
box('V04E_plsslid', (0.320, 0.014, 0.300), (0, by + 0.005, 1.395), M_HARD, 'spine01', bevel=0.006)
box('V04E_plssred', (0.190, 0.004, 0.018), (0, by + 0.013, 1.330), M_RED, 'spine01')
box('V04E_handle', (0.120, 0.020, 0.018), (0, by + 0.032, 1.505), M_DARK, 'spine01', bevel=0.005)
for sgn in (1, -1):
    box('V04E_handlepost%d' % sgn, (0.016, 0.030, 0.016), (sgn * 0.052, by + 0.012, 1.505), M_DARK, 'spine01')
box('V04E_plssplate', (0.290, 0.010, 0.075), (0, by + 0.003, 1.150), M_PANEL, 'spine01', bevel=0.004)
box('V04E_plsssq', (0.060, 0.006, 0.050), (0.075, by + 0.009, 1.150), M_TI, 'spine01', bevel=0.003)
for k, x in enumerate((-0.060, -0.105)):
    cyl('V04E_conn%d' % k, 0.017, 0.016, (x, by + 0.012, 1.150), M_DARK, 'spine01', rot=(RX, 0, 0), n=20)
cyl('V04E_canister', 0.062, 0.300, (0, pyB + 0.010, bz0 - 0.064), M_HARD, 'spine01', rot=(0, RX, 0), n=32)
for sgn in (1, -1):
    cyl('V04E_canstrap%d' % sgn, 0.0655, 0.022, (sgn * 0.090, pyB + 0.010, bz0 - 0.064), M_DARK, 'spine01', rot=(0, RX, 0), n=32)
box('V04E_canred', (0.120, 0.004, 0.012), (0, pyB + 0.010 + 0.0625, bz0 - 0.050), M_RED, 'spine01')
box('V04E_radio', (0.050, 0.060, 0.095), (-0.150, pyB + 0.040, bz1 + 0.040), M_HARD, 'spine01', bevel=0.006)
cyl('V04E_antenna', 0.004, 0.230, (-0.150, pyB + 0.055, bz1 + 0.190), M_DARK, 'spine01', n=8)
cu = bpy.data.curves.new('hose', 'CURVE'); cu.dimensions = '3D'; cu.bevel_depth = 0.011; cu.bevel_resolution = 3; cu.resolution_u = 16
sp = cu.splines.new('BEZIER'); pts = [(0.205, pyB - 0.02, 1.17), (0.245, pyB - 0.06, 1.06), (0.215, pyB - 0.01, 0.985), (0.150, pyB + 0.01, bz0 - 0.064)]
sp.bezier_points.add(len(pts) - 1)
for bp, p in zip(sp.bezier_points, pts):
    bp.co = p; bp.handle_left_type = bp.handle_right_type = 'AUTO'
hob = bpy.data.objects.new('hose_c', cu); bpy.context.scene.collection.objects.link(hob)
dg = bpy.context.evaluated_depsgraph_get()
hme = bpy.data.meshes.new_from_object(hob.evaluated_get(dg))
bpy.data.objects.remove(hob, do_unlink=True)
hose = bpy.data.objects.new('V04E_hose', hme); bpy.context.scene.collection.objects.link(hose)
rigid(hose, 'spine01', M_TI, smooth=True)
# neck bearing ring + fabric neck dam (suit side, torso)
head_co = coords(body)[coords(body)[:, 2] > 1.58]
hair_co = coords(hair)
allh = np.vstack([head_co, hair_co])
top = float(allh[:, 2].max()); yc = float((allh[:, 1].min() + allh[:, 1].max()) / 2)
HR = 0.185
hc = Vector((0, yc - 0.020, top - HR + 0.045))
zc = hc.z - math.sqrt(HR ** 2 - 0.150 ** 2)
print('HELMET centre', tuple(round(x, 3) for x in hc), 'cut', round(zc, 3), 'head top', round(top, 3))
torus('V04E_neckbearing', 0.152, 0.013, (0, hc.y, zc - 0.022), M_TI, 'spine01')
bpy.ops.mesh.primitive_circle_add(vertices=48, radius=0.150, fill_type='NOTHING', location=(0, hc.y, zc - 0.018))
dam = bpy.context.active_object; dam.name = 'V04E_neckdam'
bm = bmesh.new(); bm.from_mesh(dam.data)
outer = bm.verts[:]
inner = [bm.verts.new((v.co.x * 0.50, v.co.y * 0.50, v.co.z - 0.006)) for v in outer]
n_ = len(outer)
for i in range(n_):
    bm.faces.new((outer[i], outer[(i + 1) % n_], inner[(i + 1) % n_], inner[i]))
bm.to_mesh(dam.data); bm.free()
apply_all(dam)
rigid(dam, 'spine01', M_PANEL, smooth=True)
# helmet (third person only; weighted to the head so the face never clips the bubble). The neck bearing and dam stay on
# the torso bone but travel with the Head part: in first person they would sit right under the eye camera.
HEAD_HARD = [bpy.data.objects['V04E_neckbearing'], dam]
for ob_ in HEAD_HARD:
    HARD.remove(ob_)


def helmet_sphere(name, radius, keep, material, pole_y=False):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=64, ring_count=32, radius=radius, location=hc,
                                         rotation=(math.pi / 2, 0, 0) if pole_y else (0, 0, 0))
    ob = bpy.context.active_object; ob.name = name
    apply_all(ob)
    bm = bmesh.new(); bm.from_mesh(ob.data); bm.faces.ensure_lookup_table()
    kill = [f for f in bm.faces if not keep((f.calc_center_median() - hc).normalized(), f.calc_center_median())]
    bmesh.ops.delete(bm, geom=kill, context='FACES')
    bm.to_mesh(ob.data); bm.free()
    if material is M_HARD:
        s_ = ob.modifiers.new('s', 'SOLIDIFY'); s_.thickness = 0.006; s_.offset = 1.0
        with bpy.context.temp_override(object=ob, active_object=ob):
            bpy.ops.object.modifier_apply(modifier='s')
    ob = rigid(ob, 'head', material, smooth=True)
    HEAD_HARD.append(ob); HARD.remove(ob)
    return ob


helmet_sphere('V04E_glass', HR, lambda d, c: c.z > zc, M_GLASS)
helmet_sphere('V04E_visor', HR + 0.004, lambda d, c: c.z > zc and d.z > 0.30 and d.y < 0.48, M_VISOR)
helmet_sphere('V04E_shell', HR + 0.008, lambda d, c: c.z > zc and d.y > 0.42, M_HARD, pole_y=True)
helmet_sphere('V04E_shellside', HR + 0.008, lambda d, c: c.z > zc and d.y <= 0.42 and d.y > 0.02 and abs(d.x) > 0.84, M_HARD)
for ob_ in [torus('V04E_helmetring', 0.152, 0.016, (0, hc.y, zc), M_RED, 'head')]:
    HEAD_HARD.append(ob_); HARD.remove(ob_)
for sgn in (1, -1):
    x = sgn * (HR + 0.016)
    for nm_, args in (('lamphouse', ((0.034, 0.062, 0.058), (x, hc.y - 0.035, hc.z + 0.035), M_HARD)),):
        ob_ = box('V04E_%s%d' % (nm_, sgn), args[0], args[1], args[2], 'head', bevel=0.006)
        HEAD_HARD.append(ob_); HARD.remove(ob_)
    for k, dz in enumerate((0.052, 0.018)):
        ob_ = cyl('V04E_lamp%d%d' % (sgn, k), 0.011, 0.010, (x, hc.y - 0.035 - 0.031, hc.z + dz), M_LAMP, 'head', rot=(RX, 0, 0), n=16)
        HEAD_HARD.append(ob_); HARD.remove(ob_)
print('HARD', len(HARD), 'HEAD_HARD', len(HEAD_HARD))

# join: details into the suit, hard goods into one torso object, helmet into one head object
with bpy.context.temp_override(active_object=suit, selected_editable_objects=[suit] + details, selected_objects=[suit] + details):
    bpy.ops.object.join()
hard = HARD[0]
with bpy.context.temp_override(active_object=hard, selected_editable_objects=HARD, selected_objects=HARD):
    bpy.ops.object.join()
hard.name = 'V04E_hard'
helmet = HEAD_HARD[0]
with bpy.context.temp_override(active_object=helmet, selected_editable_objects=HEAD_HARD, selected_objects=HEAD_HARD):
    bpy.ops.object.join()
helmet.name = 'V04E_helmet'
print('BUILT', {o.name: len(o.data.vertices) for o in (body, suit, boots, hair, eyes, hard, helmet)})

# ---------------- split like FitV02: A7_EVA_TorsoLegs / Arm_L / Arm_R (+ Head for third person) ----------------
ARM = ('shoulder01', 'upperarm', 'lowerarm', 'wrist', 'metacarpal', 'finger')
head_bones = set()


def _desc(bn):
    head_bones.add(bn.name)
    for c in bn.children:
        _desc(c)


_desc(rig.data.bones['head'])


def part_of_bone(name):
    if name in head_bones:
        return 'Head'
    if name.startswith(ARM):
        return 'Arm_L' if name.endswith('.L') else ('Arm_R' if name.endswith('.R') else 'TorsoLegs')
    return 'TorsoLegs'


SOURCES = (body, suit, boots, hair, eyes, hard, helmet)
pieces = {k: [] for k in ('TorsoLegs', 'Arm_L', 'Arm_R', 'Head')}
for src in SOURCES:
    gi = {g.index: g.name for g in src.vertex_groups}
    dom = []
    for v in src.data.vertices:
        best = max(v.groups, key=lambda g: g.weight, default=None)
        dom.append(part_of_bone(gi[best.group]) if best else 'TorsoLegs')
    if src in (hair, eyes, helmet):
        face_cls = ['Head'] * len(src.data.polygons)
    elif src is hard:
        face_cls = ['TorsoLegs'] * len(src.data.polygons)
    else:
        face_cls = []
        for poly in src.data.polygons:
            votes = {}
            for vi in poly.vertices:
                votes[dom[vi]] = votes.get(dom[vi], 0) + 1
            face_cls.append(max(votes, key=votes.get))
    for cls in pieces:
        if cls not in face_cls:
            continue
        ob = src.copy(); ob.data = src.data.copy(); ob.name = src.name + '_' + cls
        bpy.context.scene.collection.objects.link(ob)
        bm = bmesh.new(); bm.from_mesh(ob.data); bm.faces.ensure_lookup_table()
        kill = [f for f, c in zip(bm.faces, face_cls) if c != cls]
        bmesh.ops.delete(bm, geom=kill, context='FACES')
        loose = [v for v in bm.verts if not v.link_faces]
        bmesh.ops.delete(bm, geom=loose, context='VERTS')
        bm.to_mesh(ob.data); bm.free()
        pieces[cls].append(ob)
for src in SOURCES:
    bpy.data.objects.remove(src, do_unlink=True)
PARTS = {}
for cls, obs in pieces.items():
    if not obs:
        continue
    target = obs[0]
    if len(obs) > 1:
        with bpy.context.temp_override(active_object=target, selected_editable_objects=obs, selected_objects=obs):
            bpy.ops.object.join()
    target.name = 'A7_%s_%s' % (VARIANT, cls)
    target.data.name = target.name
    # drop material slots with no faces (Unity would otherwise create empty submeshes)
    names = [m.name for m in target.data.materials]
    if cls.startswith('Arm') and 'MAT_V04E_Skin' in names and 'MAT_V04E_Glove' in names:
        si, gi_ = names.index('MAT_V04E_Skin'), names.index('MAT_V04E_Glove')
        moved = 0
        for p in target.data.polygons:
            if p.material_index == si:
                p.material_index = gi_; moved += 1
        print('ARM_SKIN_TO_GLOVE', cls, moved)
    used = {p.material_index for p in target.data.polygons}
    for i in reversed(range(len(target.data.materials))):
        if i not in used:
            target.data.materials.pop(index=i)      # Blender shifts the face indices itself
    PARTS[cls] = target
print('PARTS', {k: (len(o.data.vertices), [m.name for m in o.data.materials]) for k, o in PARTS.items()})

if arg('--export') and arg('--render'):
    raise SystemExit('export strips texture nodes: run --export and --render separately')
if arg('--export'):
    exp = os.path.abspath(arg('--export')); os.makedirs(exp, exist_ok=True)
    for m in bpy.data.materials:
        if m.node_tree:
            for n in [n for n in m.node_tree.nodes if n.type == 'TEX_IMAGE']:
                m.node_tree.nodes.remove(n)
    for a in bpy.data.actions:
        a.name = a.name.split('|')[-1]
    rig.data.pose_position = 'REST'
    bpy.ops.object.select_all(action='DESELECT')
    rig.select_set(True)
    for o in PARTS.values():
        o.select_set(True)
    bpy.context.view_layer.objects.active = rig
    fbx_path = os.path.join(exp, 'A7_SuitV04_EVA.fbx')
    bpy.ops.export_scene.fbx(filepath=fbx_path, use_selection=True, object_types={'ARMATURE', 'MESH'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
        bake_space_transform=False, mesh_smooth_type='OFF', use_mesh_modifiers=False, add_leaf_bones=False,
        use_armature_deform_only=False, bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0, path_mode='STRIP', embed_textures=False)
    rep = dict(fbx='A7_SuitV04_EVA.fbx', bones=len(rig.data.bones), actions=sorted(a.name for a in bpy.data.actions), objects={})
    for k, o in PARTS.items():
        tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
        rep['objects'][o.name] = dict(verts=len(o.data.vertices), tris=tris, materials=[m.name for m in o.data.materials],
                                      groups=len(o.vertex_groups))
    rep['total_tris'] = sum(v['tris'] for v in rep['objects'].values())
    json.dump(rep, open(os.path.join(exp, 'A7_SuitV04_EVA_report.json'), 'w'), indent=1)
    print('EXPORTED', fbx_path, rep['total_tris'])

if arg('--save'):
    bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(arg('--save')), compress=False)

if arg('--render'):
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    cp = bpy.context.preferences.addons['cycles'].preferences
    try:
        cp.compute_device_type = 'OPTIX'; cp.get_devices()
        for d in cp.devices:
            d.use = d.type == 'OPTIX'
        sc.cycles.device = 'GPU'
    except Exception as e:
        print('GPU', e)
    sc.cycles.samples = int(arg('--samples', '64')); sc.cycles.use_denoising = True
    sc.render.resolution_x, sc.render.resolution_y = 768, 1024
    sc.view_settings.view_transform = 'AgX'
    sc.view_settings.exposure = float(arg('--exposure', '-1.5'))
    w = bpy.data.worlds.new('w'); sc.world = w; w.use_nodes = True
    w.node_tree.nodes['Background'].inputs['Color'].default_value = (0.40, 0.35, 0.33, 1)
    w.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.55
    for i, (loc, size, en) in enumerate([((2.2, -3.2, 3.0), 2.5, 900), ((-2.6, -2.4, 2.0), 3.0, 450), ((0, 3.5, 2.6), 3.0, 420)]):
        ld = bpy.data.lights.new('L%d' % i, 'AREA'); ld.size = size; ld.energy = en
        lo = bpy.data.objects.new('L%d' % i, ld); sc.collection.objects.link(lo); lo.location = loc
        lo.rotation_euler = (Vector((0, 0, 1.0)) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    bpy.ops.mesh.primitive_plane_add(size=20, location=(0, 0, 0))
    floor = bpy.context.active_object
    fm = bpy.data.materials.new('floor'); fm.use_nodes = True
    fm.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.40, 0.35, 0.33, 1)
    floor.data.materials.append(fm)
    out = arg('--render')
    views = {'front': ((0, -7.0, 0.98), (0, 0, 0.95)), 'back': ((0, 7.0, 0.98), (0, 0, 0.95))}
    for name, (loc, tgt) in views.items():
        cd = bpy.data.cameras.new(name); cd.lens = 85; cd.sensor_fit = 'VERTICAL'; cd.sensor_height = 24.0
        co_ = bpy.data.objects.new(name, cd); sc.collection.objects.link(co_)
        co_.location = loc; co_.rotation_euler = (Vector(tgt) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
        sc.camera = co_; sc.render.filepath = out + '_' + name + '.png'
        bpy.ops.render.render(write_still=True)
    PARTS['Head'].hide_render = True
    eye = Vector((0.0, -0.32, 1.60))
    cd = bpy.data.cameras.new('fp'); cd.sensor_fit = 'VERTICAL'; cd.sensor_height = 24.0
    cd.lens = 12.0 / math.tan(math.radians(34)); cd.clip_start = 0.02
    co_ = bpy.data.objects.new('fp', cd); sc.collection.objects.link(co_)
    co_.location = eye; co_.rotation_euler = (math.radians(18), 0, math.radians(180))
    sc.camera = co_; sc.render.resolution_x, sc.render.resolution_y = 1280, 720
    sc.render.filepath = out + '_lookdown.png'
    bpy.ops.render.render(write_still=True)
    # root's first-person fixture without the lean: eye 1.65 m, 0.2 m in front of the neck, 75 deg down
    co_.location = Vector((0.0, -0.20, 1.65)); co_.rotation_euler = (math.radians(15), 0, math.radians(180))
    sc.render.filepath = out + '_lookdown_fixture.png'
    bpy.ops.render.render(write_still=True)
    print('RENDERED')
