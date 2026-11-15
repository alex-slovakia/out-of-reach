"""SUITS-V04 WORK: male MakeHuman CC0 body + CC0 clothes fitted on the UNCHANGED FitV02 rig (164 bones).
blender -b --factory-startup --python build_v04.py -- [--render out_prefix] [--export dir] [--save blend]"""
import bpy, bmesh, sys, os, json, math
sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import numpy as np
from mathutils import Vector, Matrix
from mathutils.kdtree import KDTree
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
SHIRT_OFFSET = float(arg('--shirt-offset', '0.75'))

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=os.path.join(INP, 'A7_BodyFit02_WORK.fbx'), automatic_bone_orientation=False)
rig = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
for o in [o for o in bpy.data.objects if o.type == 'MESH']:
    bpy.data.objects.remove(o, do_unlink=True)
rig.data.pose_position = 'REST'
BONES = [b.name for b in rig.data.bones]

# ---------------- male body in the rig's bind ----------------
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
# Arms and hands keep the exact base geometry (the game's grip and contact calibration uses these hands);
# the male shape fades in through the shoulder.
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
V = V + W @ dJ        # one translation pass: the morphed surface follows the rig's own joints
J2 = mhio.joints(V, skel)
errs = sorted(((float(np.linalg.norm(J2[k] - J0[k]) * S), k) for k in J0), reverse=True)
print('JOINT_ANCHOR_TOP_ERR_M', [(k, round(e, 4)) for e, k in errs[:8]])
used = {bones_mh[b]['head'] for b in BONES if b in bones_mh}
main = [(e, k) for e, k in errs if k in used and not any(t in k for t in ('eye', 'tongue', 'jaw', 'toe', 'mouth', 'brow', 'cheek', 'nose', 'lip', 'ear'))]
print('JOINT_ANCHOR_MAIN_MAX_M', main[:5])
print('HAND_JOINT_MAX_M', round(max(e for e, k in errs if any(t in k for t in ('wrist', 'finger', 'metacarpal', 'hand', 'lowerarm'))), 4))

clothes = {}
for key, path in (('suit', 'clothes/male_casualsuit02/male_casualsuit02.mhclo'), ('shoes', 'clothes/shoes02/shoes02.mhclo'),
                  ('hair', 'hair/short02/short02.mhclo'), ('eyes', 'eyes/high-poly/high-poly.mhclo')):
    clothes[key] = mhio.read_mhclo(os.path.join(SA, path))
delete = set()
for key in ('suit', 'shoes'):
    delete |= clothes[key]['delete']


def mat(name, tex=None, normal=None, color=(0.5, 0.5, 0.5, 1), rough=0.7, alpha=False, sss=0.0, tint=None):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = color
    b.inputs['Roughness'].default_value = rough
    if sss:
        b.inputs['Subsurface Weight'].default_value = sss
        b.inputs['Subsurface Radius'].default_value = (1.0, 0.35, 0.2)
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
    if normal:
        n = nt.nodes.new('ShaderNodeTexImage'); n.image = bpy.data.images.load(normal, check_existing=True)
        n.image.colorspace_settings.name = 'Non-Color'
        nm = nt.nodes.new('ShaderNodeNormalMap'); nm.inputs['Strength'].default_value = 0.9
        nt.links.new(n.outputs['Color'], nm.inputs['Color']); nt.links.new(nm.outputs['Normal'], b.inputs['Normal'])
    return m


M_SKIN = mat('MAT_Cabin_BareHands', os.path.join(SA, 'skins/young_caucasian_male/young_lightskinned_male_diffuse.png'), rough=0.52, sss=0.12, tint=(0.82, 0.64, 0.52, 1))
M_SUIT = mat('MAT_V04_Suit', os.path.join(TEX, 'V04_Suit_BaseColor.png'), os.path.join(TEX, 'V04_Suit_Normal.png'), rough=0.86)
M_SHOE = mat('MAT_V04_Shoes', os.path.join(TEX, 'V04_Shoes_BaseColor.png'), rough=0.72)
M_HAIR = mat('MAT_V04_Hair', os.path.join(SA, 'hair/short02/short02_diffuse.png'), rough=0.55, alpha=True, tint=(0.30, 0.22, 0.17, 1))
M_HAIR.blend_method = 'HASHED' if hasattr(M_HAIR, 'blend_method') else None


def make_mesh(name, P_mh, uvs, faces, weightsW, material, keep=None):
    P = mhio.mh_to_blender(P_mh, S, TY)
    me = bpy.data.meshes.new(name)
    fl = [(f[0], f[1]) for f in faces if keep is None or keep(f)]
    me.from_pydata([tuple(p) for p in P], [], [f[0] for f in fl])
    me.update()
    uvl = me.uv_layers.new(name='UVMap')
    li = 0
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
    for poly in me.polygons:
        poly.use_smooth = True
    # drop vertices without faces (helpers, deleted body under clothes)
    bm = bmesh.new(); bm.from_mesh(me)
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context='VERTS'); bm.to_mesh(me); bm.free()
    ob.parent = rig
    m = ob.modifiers.new('Armature', 'ARMATURE'); m.object = rig
    return ob


body = make_mesh('V04_Body', V, UV0, F0, W, M_SKIN,
                 keep=lambda f: f[2] == 'body' and not any(v in delete for v in f[0]))


def pants_vertex_mask(UVc, Fc, n):
    parent = list(range(len(UVc)))
    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]; a = parent[a]
        return a
    for vi, ti, g in Fc:
        for t in ti[1:]:
            ra, rb = find(ti[0]), find(t)
            if ra != rb: parent[ra] = rb
    isl = {}
    for k in range(len(UVc)):
        isl.setdefault(find(k), []).append(k)
    cls = {r: (UVc[ks].mean(0)[1] < .58 and UVc[ks].mean(0)[0] < .74) for r, ks in isl.items()}
    m = np.zeros(n, bool)
    for vi, ti, g in Fc:
        if cls[find(ti[0])]:
            m[vi] = True
    return m


def clothing(key, objrel, material, offset_scale=1.0, own_weights=None):
    clo = clothes[key]
    Vc, UVc, Fc = mhio.read_obj(os.path.join(SA, objrel))
    if key == 'suit':
        P1, _ = mhio.fit_clothes(V, clo, 1.0)
        zb = P1[:, 1] * S + TY
        pm = pants_vertex_mask(UVc, Fc, len(P1))
        t = np.clip((0.50 - zb) / 0.38, 0, 1)
        flare = 1.0 + 0.22 * t * t * (3 - 2 * t)
        offset_scale = np.where(pm, flare, offset_scale)[:, None]
    P, (i1, i2, i3, w) = mhio.fit_clothes(V, clo, offset_scale)
    if own_weights and os.path.exists(own_weights):
        Wc = mhio.weight_matrix(mhio.read_mhw(own_weights), len(P), BONES)
    else:
        Wc = W[i1] * w[:, :1] + W[i2] * w[:, 1:2] + W[i3] * w[:, 2:3]
        s = Wc.sum(1, keepdims=True); s[s == 0] = 1; Wc = Wc / s
    return make_mesh('V04_' + key, P, UVc, Fc, Wc, material)


suit = clothing('suit', 'clothes/male_casualsuit02/male_casualsuit02.obj', M_SUIT, SHIRT_OFFSET)
shoes = clothing('shoes', 'clothes/shoes02/shoes02.obj', M_SHOE, 1.0, os.path.join(SA, 'clothes/shoes02/shoes02.mhw'))
hair = clothing('hair', 'hair/short02/short02.obj', M_HAIR, 1.0)
M_EYE = mat('MAT_V04_Eyes', os.path.join(SA, 'eyes/materials/brown_eye.png'), rough=0.15, alpha=True, tint=(0.80, 0.78, 0.76, 1))
if hasattr(M_EYE, 'blend_method'):
    M_EYE.blend_method = 'HASHED'
eyes = clothing('eyes', 'eyes/high-poly/high-poly.obj', M_EYE, 1.0)
# ---------------- cargo details on the trousers: side zip pockets, knee seams, back pockets ----------------
from mathutils.bvhtree import BVHTree
M_TRIM = mat('MAT_V04_TrouserTrim', color=(0.020, 0.021, 0.024, 1), rough=0.9)
M_ZIP = mat('MAT_V04_Zip', color=(0.05, 0.055, 0.06, 1), rough=0.35)
M_ZIP.node_tree.nodes['Principled BSDF'].inputs['Metallic'].default_value = 0.7
dg = bpy.context.evaluated_depsgraph_get()
suit_bvh = BVHTree.FromObject(suit, dg)
kd = KDTree(len(suit.data.vertices))
for v in suit.data.vertices:
    kd.insert(v.co, v.index)
kd.balance()
suit_groups = {g.index: g.name for g in suit.vertex_groups}


def bone_axis(name):
    bn = rig.data.bones[name]
    return Vector(bn.head_local), Vector(bn.tail_local)


def surface_patch(name, material, centre_fn, n_u, n_v, lift=0.004, solid=0.004):
    """grid patch projected onto the trousers by rays toward the leg/hip axis; skinned like the nearest trouser vertex."""
    verts, faces = [], []
    for j in range(n_v + 1):
        for i in range(n_u + 1):
            origin, target = centre_fn(i / n_u, j / n_v)
            d = (target - origin).normalized()
            hit = suit_bvh.ray_cast(origin, d, 2.0)
            p = hit[0] + hit[1] * lift if hit[0] is not None else target
            verts.append(p)
    for j in range(n_v):
        for i in range(n_u):
            a = j * (n_u + 1) + i
            faces.append((a, a + 1, a + n_u + 2, a + n_u + 1))
    me = bpy.data.meshes.new(name); me.from_pydata([tuple(v) for v in verts], [], faces); me.update()
    ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
    ob.data.materials.append(material)
    if solid:
        sm = ob.modifiers.new('solid', 'SOLIDIFY'); sm.thickness = solid; sm.offset = 1.0
        with bpy.context.temp_override(object=ob, active_object=ob):
            bpy.ops.object.modifier_apply(modifier='solid')
    for v in ob.data.vertices:
        _, idx, _ = kd.find(v.co)
        for g in suit.data.vertices[idx].groups:
            name_g = suit_groups[g.group]
            vg = ob.vertex_groups.get(name_g) or ob.vertex_groups.new(name=name_g)
            vg.add([v.index], g.weight, 'REPLACE')
    for p in ob.data.polygons:
        p.use_smooth = True
    ob.parent = rig
    m = ob.modifiers.new('Armature', 'ARMATURE'); m.object = rig
    return ob


details = []
for side, sgn in (('L', 1.0), ('R', -1.0)):
    h, t = bone_axis('upperleg01.' + side)
    h2, t2 = bone_axis('upperleg02.' + side)
    axis_top, axis_bot = h, t2
    def leg_axis(z):
        f = (axis_top.z - z) / (axis_top.z - axis_bot.z)
        return axis_top.lerp(axis_bot, max(0.0, min(1.0, f)))
    # side zip pocket on the outer front of the thigh (reference: vertical zip at the pocket's front edge)
    def pocket(u, v, sgn=sgn):
        z = 0.80 - 0.20 * v
        ang = math.radians(48 + 38 * u)            # from front (0) toward the outside (90)
        dirv = Vector((sgn * math.sin(ang), -math.cos(ang), 0))
        c = leg_axis(z)
        return c + dirv * 0.4, c
    details.append(surface_patch('V04_pocket_' + side, M_TRIM, pocket, 6, 8, lift=0.003, solid=0.004))
    def zipper(u, v, sgn=sgn):
        z = 0.79 - 0.18 * v
        ang = math.radians(52 + 3 * u)
        dirv = Vector((sgn * math.sin(ang), -math.cos(ang), 0))
        c = leg_axis(z)
        return c + dirv * 0.4, c
    details.append(surface_patch('V04_zip_' + side, M_ZIP, zipper, 1, 10, lift=0.0085, solid=0.003))
    # knee seams: two thin strips across the front of the knee
    hk, tk = bone_axis('lowerleg01.' + side)
    for zk in (hk.z + 0.07, hk.z - 0.05):
        def seam(u, v, zk=zk, sgn=sgn, hk=hk):
            ang = math.radians(-70 + 140 * u)
            z = zk + 0.008 * (v - 0.5)
            dirv = Vector((sgn * math.sin(ang), -math.cos(ang), 0))
            c = Vector((hk.x, hk.y, z))
            return c + dirv * 0.4, c
        details.append(surface_patch('V04_kneeseam_%s_%d' % (side, int(zk * 100)), M_TRIM, seam, 10, 1, lift=0.0025, solid=0.002))
    # back patch pocket on the seat
    pel = Vector(rig.data.bones['pelvis.' + side].head_local)
    def back(u, v, sgn=sgn, pel=pel):
        x = sgn * (0.035 + 0.115 * u)
        z = pel.z - 0.02 - 0.16 * v
        c = Vector((x, pel.y - 0.05, z))
        return c + Vector((0, 0.45, 0)), c
    details.append(surface_patch('V04_backpocket_' + side, M_TRIM, back, 5, 6, lift=0.003, solid=0.003))
M_RIB = mat('MAT_V04_ShirtRib', color=(0.011, 0.016, 0.034, 1), rough=0.9)
for side, sgn in (('L', 1.0), ('R', -1.0)):
    wh = Vector(rig.data.bones['wrist.' + side].head_local)
    la = Vector(rig.data.bones['lowerarm02.' + side].head_local)
    ax = (wh - la).normalized()
    up = Vector((0, 0, 1)); sidev = ax.cross(up).normalized(); upv = sidev.cross(ax).normalized()
    def cuff(u, v, wh=wh, ax=ax, sidev=sidev, upv=upv):
        ang = 2 * math.pi * u
        c = wh - ax * (0.020 + 0.045 * v)
        d = sidev * math.cos(ang) + upv * math.sin(ang)
        return c + d * 0.25, c
    details.append(surface_patch('V04_cuff_' + side, M_RIB, cuff, 24, 3, lift=0.0025, solid=0.003))
hem_z = min(v.co.z for v in suit.data.vertices if v.co.z > 0.86 and abs(v.co.x) < 0.10 and v.co.y < 0) 
pelvis_c = Vector((0, -0.01, hem_z))
def hem(u, v):
    ang = 2 * math.pi * u
    c = Vector((0, -0.01, hem_z + 0.012 + 0.045 * v))
    return c + Vector((math.cos(ang), math.sin(ang), 0)) * 0.5, c
details.append(surface_patch('V04_hem', M_RIB, hem, 40, 3, lift=0.003, solid=0.003))
with bpy.context.temp_override(active_object=suit, selected_editable_objects=[suit] + details, selected_objects=[suit] + details):
    bpy.ops.object.join()
print('CARGO_DETAILS', len(details))

print('BUILT', {o.name: len(o.data.vertices) for o in (body, suit, shoes, hair, eyes)})

# ---------------- split like FitV02: A7_WORK_TorsoLegs / Arm_L / Arm_R (+ Head for third person) ----------------
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


pieces = {k: [] for k in ('TorsoLegs', 'Arm_L', 'Arm_R', 'Head')}
for src in (body, suit, shoes, hair, eyes):
    gi = {g.index: g.name for g in src.vertex_groups}
    dom = []
    for v in src.data.vertices:
        best = max(v.groups, key=lambda g: g.weight, default=None)
        dom.append(part_of_bone(gi[best.group]) if best else 'TorsoLegs')
    if src in (hair, eyes):
        face_cls = ['Head'] * len(src.data.polygons)
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
for src in (body, suit, shoes, hair, eyes):
    bpy.data.objects.remove(src, do_unlink=True)
PARTS = {}
for cls, obs in pieces.items():
    if not obs:
        continue
    target = obs[0]
    if len(obs) > 1:
        with bpy.context.temp_override(active_object=target, selected_editable_objects=obs, selected_objects=obs):
            bpy.ops.object.join()
    target.name = 'A7_WORK_' + cls
    target.data.name = 'A7_WORK_' + cls
    PARTS[cls] = target
print('PARTS', {k: (len(o.data.vertices), [m.name for m in o.data.materials]) for k, o in PARTS.items()})
hair = eyes = None

if arg('--export'):
    exp = os.path.abspath(arg('--export')); os.makedirs(exp, exist_ok=True)
    for m in bpy.data.materials:                    # no texture links in the FBX: the exporter writes absolute paths
        if m.node_tree:
            for n in [n for n in m.node_tree.nodes if n.type == 'TEX_IMAGE']:
                m.node_tree.nodes.remove(n)
    for a in bpy.data.actions:                      # same take names as FitV02 (Unity clips BODY_*)
        a.name = a.name.split('|')[-1]
    rig.data.pose_position = 'REST'
    bpy.ops.object.select_all(action='DESELECT')
    rig.select_set(True)
    for o in PARTS.values():
        o.select_set(True)
    bpy.context.view_layer.objects.active = rig
    fbx_path = os.path.join(exp, 'A7_SuitV04_WORK.fbx')
    bpy.ops.export_scene.fbx(filepath=fbx_path, use_selection=True, object_types={'ARMATURE', 'MESH'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
        bake_space_transform=False, mesh_smooth_type='OFF', use_mesh_modifiers=False, add_leaf_bones=False,
        use_armature_deform_only=False, bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0, path_mode='STRIP', embed_textures=False)
    rep = dict(fbx='A7_SuitV04_WORK.fbx', bones=len(rig.data.bones), actions=sorted(a.name for a in bpy.data.actions), objects={})
    for k, o in PARTS.items():
        tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
        rep['objects'][o.name] = dict(verts=len(o.data.vertices), tris=tris, materials=[m.name for m in o.data.materials],
                                      groups=len(o.vertex_groups))
    rep['total_tris'] = sum(v['tris'] for v in rep['objects'].values())
    json.dump(rep, open(os.path.join(exp, 'A7_SuitV04_WORK_report.json'), 'w'), indent=1)
    print('EXPORTED', fbx_path, rep['total_tris'])

if arg('--save'):
    bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(arg('--save')), compress=False)

# ---------------- proof renders ----------------
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
    sc.cycles.samples = 64; sc.cycles.use_denoising = True
    sc.render.resolution_x, sc.render.resolution_y = 768, 1024
    sc.view_settings.view_transform = 'AgX'
    sc.view_settings.exposure = -1.0
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
    views = {'front': ((0, -7.0, 0.95), (0, 0, 0.92)), 'back': ((0, 7.0, 0.95), (0, 0, 0.92))}
    for name, (loc, tgt) in views.items():
        cd = bpy.data.cameras.new(name); cd.lens = 85; cd.sensor_fit = 'VERTICAL'; cd.sensor_height = 24.0
        co = bpy.data.objects.new(name, cd); sc.collection.objects.link(co)
        co.location = loc; co.rotation_euler = (Vector(tgt) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
        sc.camera = co; sc.render.filepath = out + '_' + name + '.png'
        bpy.ops.render.render(write_still=True)
    # first-person look-down from the eye (head joint + offset), hide head/hair
    PARTS['Head'].hide_render = True
    # root's first-person fixture: eye 1.65 m above the feet, about 0.2 m in front of the neck, 75 deg down, vFOV 68
    eye = Vector((0.0, -0.32, 1.60))   # leaning first-person preview so forearms, hands, legs and shoes are in view
    cd = bpy.data.cameras.new('fp'); cd.sensor_fit = 'VERTICAL'; cd.sensor_height = 24.0
    cd.lens = 12.0 / math.tan(math.radians(34)); cd.clip_start = 0.02
    co = bpy.data.objects.new('fp', cd); sc.collection.objects.link(co)
    co.location = eye; co.rotation_euler = (math.radians(18), 0, math.radians(180))   # faces -Y, 72 deg below horizon
    sc.camera = co; sc.render.resolution_x, sc.render.resolution_y = 1280, 720
    sc.render.filepath = out + '_lookdown.png'
    bpy.ops.render.render(write_still=True)
    print('RENDERED')
