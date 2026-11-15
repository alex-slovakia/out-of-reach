"""EVA suit (V03 detail pass): off-white layered suit with elbow/knee bellows, red bands, harness, front and side
pockets, gloves; chest control panel with display; detailed PLSS with canister and hose; bubble helmet with clear
visor, gold sun shade, lights; head (MakeHuman CC0) visible behind the visor.
blender -b --factory-startup --python build_eva.py -- <EVA fbx> <WORK fbx (hands)> <out dir> <mh_cc0 dir> <display png> [--export]"""
import sys, os
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from suitlib import *
from head import build_head
from suitlib import _frame, _smooth01

A = sys.argv[sys.argv.index("--") + 1:]
IN, INW, OUT, MHDIR, DISPLAY_PNG = (os.path.abspath(a) for a in A[:5])
EXPORT = "--export" in A
os.makedirs(OUT, exist_ok=True)

arm, meshes = load_fbx(IN)
HASH0 = bone_hash(arm)
bpy.ops.import_scene.fbx(filepath=INW)
wobjs = [o for o in bpy.context.scene.objects if o.name not in meshes and o is not arm and o.type in ("MESH", "ARMATURE")]
wmesh = {o.name: o for o in wobjs if o.type == "MESH"}
log("bones", len(arm.data.bones), HASH0)
MALE = MaleField(MHDIR, A[A.index("--male") + 1]) if "--male" in A else None
if MALE:
    for o in list(meshes.values()) + list(wmesh.values()):
        apply_male(o, MALE)
    log("male shape applied")

PAL = [
    ("MAT_V03_EvaSuit", dict(hexcol="#D6D2C8", rough=0.85)),
    ("MAT_V03_EvaPanel", dict(hexcol="#C7C2B6", rough=0.88)),
    ("MAT_V03_EvaBoot", dict(hexcol="#CBC6BA", rough=0.8)),
    ("MAT_V03_EvaBootGrey", dict(hexcol="#585C60", rough=0.85)),
    ("MAT_V03_EvaSole", dict(hexcol="#25272A", rough=0.9)),
    ("MAT_V03_EvaGlove", dict(hexcol="#CFCABF", rough=0.8)),
    ("MAT_V03_EvaGloveGrip", dict(hexcol="#4A4E52", rough=0.85)),
    ("MAT_V03_EvaStrap", dict(hexcol="#47494C", rough=0.88)),
    ("MAT_V03_EvaMetal", dict(hexcol="#A3A9AD", rough=0.55, metal=0.55)),
    ("MAT_V03_EvaRed", dict(hexcol="#A93A2F", rough=0.85)),
    ("MAT_V03_EvaDisplay", dict(hexcol="#FFFFFF", rough=0.3)),
    ("MAT_V03_EvaVisorGold", dict(hexcol="#C9A24A", rough=0.12, metal=1.0)),
    ("MAT_V03_EvaLight", dict(hexcol="#FFF2D0", rough=0.3, emission="#FFE9B0", emission_strength=6.0)),
    ("MAT_V03_EvaYellow", dict(hexcol="#E0B020", rough=0.5)),
    ("MAT_V03_EvaDark", dict(hexcol="#1B1C1E", rough=0.7)),
    ("MAT_V03_EvaVisorClear", dict(hexcol="#C8D6DE", rough=0.05)),
    ("MAT_V03_Skin", dict(hexcol="#A67A64", rough=0.65)),
    ("MAT_V03_Hair", dict(hexcol="#2A2019", rough=0.88)),
    ("MAT_V03_EyeWhite", dict(hexcol="#D8D3CA", rough=0.3)),
    ("MAT_V03_EyeIris", dict(hexcol="#3A2B22", rough=0.2)),
]
M = Mat(PAL)
(SUIT, PANEL, BOOT, BGREY, SOLE, GLOVE, GRIP, STRAP, METAL, RED, DISPLAY, VISOR, LIGHT, YELLOW, DARK, CLEAR,
 SKIN, HAIR, EYEW, IRIS) = (M.idx(n) for n, _ in PAL)
texture_material(M.mats["MAT_V03_EvaDisplay"], DISPLAY_PNG, 1.5)
alpha_material(M.mats["MAT_V03_EvaVisorClear"], 0.12)
SHOE_TOP = 0.092


def sgn(x):
    return 1.0 if x >= 0 else -1.0


# ------------------------------------------------------------------ TorsoLegs
tl = meshes["A7_EVA_TorsoLegs"]
bm = bmesh.new()
bm.from_mesh(tl.data)
bm.faces.ensure_lookup_table()
flat = set()
for f in bm.faces:
    om = f.material_index
    if om in (1, 4, 5, 6):
        flat.update(f.verts)
    f.material_index = SOLE if om == 3 else (BOOT if om == 2 else SUIT)
smooth_set(bm, [v for v in flat if v.co.z > 0.2], iters=22, factor=0.6)
bm.normal_update()
vn = {v: v.normal.copy() for v in bm.verts}


def loose(v):
    z, n = v.co.z, vn[v]
    if z < SHOE_TOP + 0.005:
        return n * 0.006
    if z > 0.92:
        d = 0.030 if z < 1.44 else (0.030 - 0.022 * min(1.0, (z - 1.44) / 0.06))
    else:
        d = 0.030 + 0.008 * min(1.0, max(0.0, (z - 0.2) / 0.6))
        inward = -sgn(v.co.x) * n.x
        if inward > 0:
            d *= (1.0 - 0.8 * min(1.0, inward))
    return n * d


for v in bm.verts:
    v.co = v.co + loose(v)
bm.normal_update()
ZC = (1.512, 1.45, 1.40, 1.30, 1.18, 1.175, 1.10, 1.095, 0.365, 0.36, 0.25, 0.245, 1.03, 0.985, 0.92, 0.86, 0.83, 0.80, 0.78, 0.76, 0.72, 0.64, 0.60, 0.585, 0.56, 0.55,
      0.52, 0.495, 0.47, 0.445, 0.42, 0.30, 0.17, 0.14, 0.08, SHOE_TOP, 0.06)
for z in ZC:
    cutz(bm, z)
for y in (-0.06, -0.065, 0.02, -0.12, -0.14):
    cuty(bm, y)
for x in (0.11, 0.125, 0.06, 0.095, 0.035, 0.16):
    cutx(bm, x)
bm.normal_update()
for f in bm.faces:
    c = fcenter(f)
    m = f.material_index
    if m == BOOT and c.z > SHOE_TOP + 0.04:
        f.material_index = SUIT
    elif m == BOOT and c.z < 0.075 and c.y < -0.14:
        f.material_index = BGREY
    elif m == SUIT and c.z >= 1.512:
        f.material_index = METAL
bm.faces.ensure_lookup_table()


def region(zlo, zhi, mat=SUIT, ylo=-9, yhi=9, xlo=-9, xhi=9, absx=False):
    out = []
    for f in bm.faces:
        if f.material_index != mat:
            continue
        c = fcenter(f)
        x = abs(c.x) if absx else c.x
        if zlo <= c.z <= zhi and ylo <= c.y <= yhi and xlo <= x <= xhi:
            out.append(f)
    return out


raise_region(bm, region(0.985, 1.03), 0.007, METAL)                                              # waist ring
raise_region(bm, region(1.30, 1.512, xlo=0.06, xhi=0.095, absx=True), 0.007, STRAP)             # harness straps over the shoulders
raise_region(bm, region(0.80, 0.83), 0.006, STRAP)                                               # thigh straps
raise_region(bm, region(0.60, 0.72, ylo=-0.065, yhi=0.02, xlo=0.125, absx=True), 0.010, PANEL)   # side pockets
fp = raise_region(bm, region(0.64, 0.78, yhi=-0.06, xlo=0.06, xhi=0.16, absx=True), 0.009, PANEL)  # front thigh pockets
raise_region(bm, [f for f in fp if f.is_valid and fcenter(f).z > 0.76], 0.004, PANEL)              # pocket flaps
raise_region(bm, region(0.42, 0.55, yhi=-0.06), 0.013, PANEL)                                    # knee pads on the knee
for lo_, hi_ in ((0.445, 0.47), (0.495, 0.52)):
    raise_region(bm, region(lo_, hi_, mat=PANEL, yhi=-0.06), 0.005, PANEL)                       # knee bellows ribs
raise_region(bm, region(0.30, 0.42, yhi=-0.06), 0.006, PANEL)                                    # shin panels
raise_region(bm, region(0.14, 0.17), 0.007, STRAP)                                               # ankle straps
raise_region(bm, region(0.56, 0.585, yhi=-0.06), 0.004, RED)                                     # red bands just above the knees
for lo_, hi_ in ((1.175, 1.18), (1.095, 1.10), (0.36, 0.365), (0.245, 0.25)):
    raise_region(bm, region(lo_, hi_), 0.0025, PANEL)                                           # layer seams (piping)
finish_mesh(tl, bm, M, 38.0)
log("TorsoLegs", len(tl.data.vertices), vg_weight_report(tl, arm))
bm.free()
TV = [tl.matrix_world @ v.co for v in tl.data.vertices]
CHEST_Y = min(p.y for p in TV if abs(p.x) < 0.05 and abs(p.z - 1.28) < 0.03)
BACK_Y = max(p.y for p in TV if abs(p.x) < 0.10 and 1.10 < p.z < 1.45)
log("chest front y", round(CHEST_Y, 4), "back y", round(BACK_Y, 4))


# ------------------------------------------------------------------ arms + gloves + elbow bellows
def copy_faces(src_obj, pred, dst_obj, dst_bm):
    sbm = bmesh.new()
    sbm.from_mesh(src_obj.data)
    sdl = sbm.verts.layers.deform.verify()
    suv = sbm.loops.layers.uv.active
    ddl = dst_bm.verts.layers.deform.verify()
    duv = dst_bm.loops.layers.uv.active
    smap = {g.index: g.name for g in src_obj.vertex_groups}
    dmap = {g.name: g.index for g in dst_obj.vertex_groups}
    vmap, newf = {}, []
    for f in sbm.faces:
        if not pred(f):
            continue
        vs = []
        for v in f.verts:
            if v not in vmap:
                nv = dst_bm.verts.new(v.co)
                for gi_, w in v[sdl].items():
                    nv[ddl][dmap[smap[gi_]]] = w
                vmap[v] = nv
            vs.append(vmap[v])
        try:
            nf = dst_bm.faces.new(vs)
        except ValueError:
            continue
        if suv and duv:
            for ls, ld in zip(f.loops, nf.loops):
                ld[duv].uv = ls[suv].uv
        newf.append(nf)
    sbm.free()
    return newf


for s in "LR":
    ob = meshes["A7_EVA_Arm_" + s]
    src = wmesh["A7_WORK_Arm_" + s]
    for g in src.vertex_groups:
        if g.name not in ob.vertex_groups:
            ob.vertex_groups.new(name=g.name)
    ua, la, wr = (arm.data.bones[n + "." + s] for n in ("upperarm02", "lowerarm01", "wrist"))
    d = (wr.head_local - arm.data.bones["lowerarm02." + s].head_local).normalized()
    elbow = la.head_local.copy()
    dup = (la.head_local - ua.head_local).normalized()
    dlo = (wr.head_local - la.head_local).normalized()
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bm.faces.ensure_lookup_table()
    s_end = max(fcenter(f).dot(d) for f in bm.faces)
    for f in bm.faces:
        f.material_index = SUIT
    vnn = vnormals_from_faces(bm.faces)
    for v, n in vnn.items():
        v.co = v.co + n * 0.020
    bm.normal_update()
    ribs = []
    for lo_, hi_ in ((-0.070, -0.062), (-0.056, -0.048), (-0.042, -0.034), (-0.028, -0.020)):
        ribs.append((dup, elbow.dot(dup) + lo_, elbow.dot(dup) + hi_))
    for lo_, hi_ in ((0.020, 0.028), (0.034, 0.042), (0.048, 0.056)):
        ribs.append((dlo, elbow.dot(dlo) + lo_, elbow.dot(dlo) + hi_))
    for ax, lo_, hi_ in ribs:
        cut(bm, ax * lo_, ax)
        cut(bm, ax * hi_, ax)
    cut(bm, d * (s_end - 0.05), d)
    cut(bm, d * (s_end - 0.012), d)
    cutz(bm, 1.36)
    cutz(bm, 1.40)
    bm.normal_update()
    for ax, lo_, hi_ in ribs:
        raise_region(bm, [f for f in bm.faces if f.material_index == SUIT and lo_ <= fcenter(f).dot(ax) <= hi_
                          and (fcenter(f) - elbow).length < 0.11], 0.004, SUIT)
    raise_region(bm, [f for f in bm.faces if s_end - 0.05 < fcenter(f).dot(d) <= s_end - 0.012], 0.010, METAL)
    raise_region(bm, [f for f in bm.faces if 1.36 <= fcenter(f).z <= 1.40 and f.material_index == SUIT
                      and fcenter(f).dot(d) < s_end - 0.2], 0.0035, RED)

    SLEEVE_END = [v.co.copy() for v in bm.verts if abs(v.co.dot(d) - (s_end - 0.03)) < 0.012]

    def is_hand(f, sd=d, se=s_end):
        return f.material_index == 5 and fcenter(f).dot(sd) > se - 0.035
    nf = copy_faces(src, is_hand, ob, bm)
    vh = vnormals_from_faces(nf)
    dl = bm.verts.layers.deform.verify()
    names = {g.index: g.name for g in ob.vertex_groups}
    for v, n in vh.items():
        ws = v[dl]
        dom = names[max(ws.keys(), key=lambda k: ws[k])] if len(ws) else ""
        v.co = v.co + n * (0.0065 if dom.startswith("finger") else 0.011)      # thick padded glove
    for f in nf:
        w = {}
        for v in f.verts:
            for gi_, ww in v[dl].items():
                w[names[gi_]] = w.get(names[gi_], 0) + ww
        dom = max(w, key=w.get) if w else ""
        f.material_index = GRIP if dom.startswith("finger") and not dom.startswith("finger1-1") else GLOVE
    bm.normal_update()
    finish_mesh(ob, bm, M, 40.0)
    bm.free()
    # gauntlet cuff: tapered tube from under the wrist ring over the wrist, skinned lowerarm02 -> wrist
    W0 = wr.head_local.copy()
    rs = sorted(((p - W0) - d * (p - W0).dot(d)).length for p in SLEEVE_END)
    r0 = min(0.066, rs[int(len(rs) * 0.85)] + 0.004) if rs else 0.055
    gob = bpy.data.objects.new("gauntlet_" + s, bpy.data.meshes.new("gauntlet_" + s))
    bpy.context.scene.collection.objects.link(gob)
    gbm = bmesh.new()
    t0, t1 = s_end - 0.020, s_end + 0.050
    _, uu, ww_ = _frame(d)
    rings_ = []
    for k, (t, r, m_) in enumerate(((t0, r0 * 0.97, GRIP), (t0 + 0.010, r0 * 1.02, GRIP), (t0 + 0.012, r0 * 1.03, GLOVE), (t1 - 0.012, r0 * 0.86, GLOVE), (t1, r0 * 0.80, GLOVE))):
        cpt = W0 + d * (t - W0.dot(d))
        rings_.append(([gbm.verts.new(cpt + (uu * math.cos(2 * math.pi * i / 32) + ww_ * math.sin(2 * math.pi * i / 32)) * r) for i in range(32)], m_, t))
    for k in range(len(rings_) - 1):
        (ra, ma, _), (rb, mb, _) = rings_[k], rings_[k + 1]
        for i in range(32):
            j = (i + 1) % 32
            f = gbm.faces.new((ra[i], ra[j], rb[j], rb[i]))
            f.material_index = mb
    bmesh.ops.recalc_face_normals(gbm, faces=list(gbm.faces))
    for f in gbm.faces:
        f.smooth = True
    M.apply(gob)
    gbm.to_mesh(gob.data)
    gbm.free()
    g1 = gob.vertex_groups.new(name="lowerarm02." + s)
    g2 = gob.vertex_groups.new(name="wrist." + s)
    for v in gob.data.vertices:
        tt = (v.co.dot(d) - t0) / (t1 - t0)
        w2 = _smooth01(tt)
        g1.add([v.index], 1.0 - w2, "REPLACE")
        g2.add([v.index], max(w2, 1e-4), "REPLACE")
    join_into(ob, [gob])
    log(ob.name, len(ob.data.vertices), "glove faces", len(nf), "gauntlet r0", round(r0, 4), vg_weight_report(ob, arm))


# ------------------------------------------------------------------ rigid parts (skinned 100 % to one bone)
def new_part(name, bone):
    ob = make_part(name, arm, bone, M, None)
    return ob, bmesh.new()


# chest control panel (spine02)
ob, bm = new_part("A7_SuitV03_EVA_ChestPanel", "spine02")
pb = CHEST_Y - 0.004
pf = pb - 0.050
cy = (pb + pf) / 2
box(bm, 0, pb - 0.006, 1.275, 0.228, 0.012, 0.162, BGREY, 0.004)
box(bm, 0, cy, 1.275, 0.214, 0.050, 0.150, PANEL, 0.009, 3)
box(bm, -0.020, pf - 0.002, 1.292, 0.138, 0.006, 0.074, DARK, 0.003)
uv_quad(bm, [(-0.080, pf - 0.0055, 1.2635), (0.040, pf - 0.0055, 1.2635), (0.040, pf - 0.0055, 1.3205), (-0.080, pf - 0.0055, 1.3205)], DISPLAY)
for i in range(2):
    for j in range(3):
        box(bm, 0.072 + 0.020 * i, pf - 0.004, 1.316 - 0.021 * j, 0.014, 0.008, 0.013, STRAP if (i + j) % 3 else RED, 0.002)
for x in (-0.075, -0.040, -0.005):
    cylinder(bm, (x, pf - 0.008, 1.222), (0, 1, 0), 0.0145, 0.016, DARK, 18)
    cylinder(bm, (x, pf - 0.0165, 1.222), (0, 1, 0), 0.007, 0.004, METAL, 12)
torus(bm, (0.058, pf - 0.006, 1.222), (0, 1, 0), 0.016, 0.0042, YELLOW, 24, 8)
box(bm, 0.058, pf - 0.006, 1.222, 0.010, 0.010, 0.010, METAL, 0.002)
for x in (-0.096, 0.096):
    for z in (1.209, 1.341):
        cylinder(bm, (x, pf - 0.002, z), (0, 1, 0), 0.004, 0.004, METAL, 8)
box(bm, 0, pb - 0.020, 1.398, 0.150, 0.042, 0.072, PANEL, 0.008, 3)
box(bm, 0, pb - 0.042, 1.425, 0.154, 0.006, 0.026, PANEL, 0.003)
box(bm, 0, pb - 0.0425, 1.398, 0.156, 0.005, 0.012, STRAP, 0.002)                  # pouch strap
for sx in (1, -1):                                                                 # harness buckles + adjusters on the straps
    box(bm, sx * 0.0775, CHEST_Y - 0.020, 1.455, 0.034, 0.012, 0.030, METAL, 0.003)
    box(bm, sx * 0.0775, CHEST_Y - 0.018, 1.395, 0.030, 0.010, 0.020, STRAP, 0.003)
    box(bm, sx * 0.112, pf + 0.010, 1.200, 0.020, 0.016, 0.022, METAL, 0.003)        # hose connectors
finalize_part(ob, bm, M, "spine02")
bm.free()
chest = ob
from mathutils.bvhtree import BVHTree
TLB = BVHTree.FromPolygons([v.co for v in tl.data.vertices], [p.vertices for p in tl.data.polygons])
hose_objs = []
for sx in (1, -1):
    A_ = lambda a: a if sx > 0 else 180.0 - a
    pts = [Vector((sx * 0.092, pf + 0.012, 1.206)), Vector((sx * 0.100, pf + 0.020, 1.178))]
    for z, ang, off in ((1.150, -68.0, 0.026), (1.105, -42.0, 0.030), (1.060, -16.0, 0.030), (1.035, 2.0, 0.026)):
        pts.append(surface_point(TLB, z, A_(ang), off))
    hob = bpy.data.objects.new("hose_%d" % sx, bpy.data.meshes.new("hose_%d" % sx))
    bpy.context.scene.collection.objects.link(hob)
    hbm = bmesh.new()
    tube(hbm, pts, 0.0125, STRAP, 14)
    for f in hbm.faces:
        f.smooth = True
    M.apply(hob)
    hbm.to_mesh(hob.data)
    hbm.free()
    ga, gb = hob.vertex_groups.new(name="spine02"), hob.vertex_groups.new(name="spine04")
    for v in hob.data.vertices:
        w = _smooth01((v.co.z - 1.03) / 0.14)
        ga.add([v.index], max(w, 1e-4), "REPLACE")
        gb.add([v.index], max(1.0 - w, 1e-4), "REPLACE")
    hose_objs.append(hob)
join_into(tl, hose_objs)
log("chest hoses joined into TorsoLegs")

# PLSS backpack (spine02)
ob, bm = new_part("A7_SuitV03_EVA_Plss", "spine02")
yb0 = BACK_Y + 0.006
yb = yb0 + 0.190
yc = (yb0 + yb) / 2
box(bm, 0, yc, 1.300, 0.350, 0.190, 0.400, SUIT, 0.026, 3)
box(bm, 0, yb + 0.002, 1.315, 0.290, 0.008, 0.300, PANEL, 0.004)
box(bm, 0, yb + 0.008, 1.250, 0.200, 0.006, 0.022, RED, 0.002)
box(bm, 0, yb + 0.032, 1.432, 0.130, 0.014, 0.018, METAL, 0.005)
for x in (-0.058, 0.058):
    box(bm, x, yb + 0.016, 1.432, 0.014, 0.030, 0.014, METAL, 0.003)
for x in (-0.178, 0.178):
    for z in (1.40, 1.25, 1.12):
        box(bm, x, yc + 0.005, z, 0.012, 0.050, 0.042, STRAP, 0.003)
        box(bm, x * 1.035, yc + 0.005, z, 0.006, 0.026, 0.020, METAL, 0.002)
box(bm, 0, yb0 + 0.080, 1.045, 0.330, 0.150, 0.110, PANEL, 0.016, 3)
for x in (0.075, 0.120):
    cylinder(bm, (x, yb0 + 0.159, 1.045), (0, 1, 0), 0.018, 0.012, DARK, 18)
    cylinder(bm, (x, yb0 + 0.166, 1.045), (0, 1, 0), 0.009, 0.004, METAL, 12)
box(bm, -0.070, yb0 + 0.157, 1.045, 0.075, 0.006, 0.052, METAL, 0.003)
ycan = yb0 + 0.085
cylinder(bm, (0, ycan, 0.930), (1, 0, 0), 0.058, 0.300, PANEL, 28)
for x in (-0.092, 0.092):
    cylinder(bm, (x, ycan, 0.930), (1, 0, 0), 0.0615, 0.022, STRAP, 28)
box(bm, 0, ycan + 0.059, 0.930, 0.150, 0.004, 0.012, RED, 0.001)
tube(bm, [(0.150, yb0 + 0.080, 1.120), (0.192, yb0 + 0.095, 1.075), (0.200, yb0 + 0.100, 1.005), (0.185, ycan + 0.02, 0.955),
          (0.152, ycan + 0.01, 0.935)], 0.011, METAL, 12)
box(bm, 0, yc, 1.505, 0.300, 0.150, 0.012, PANEL, 0.006)                            # top hatch
for x in (-0.105, 0.105):
    box(bm, x, yb + 0.004, 1.315, 0.010, 0.008, 0.330, PANEL, 0.002)                 # back rails
for z in (1.20, 1.36):
    for x in (-0.03, 0.0, 0.03):
        box(bm, x - 0.09, yb + 0.006, z, 0.016, 0.004, 0.040, DARK, 0.002)          # vents
box(bm, 0, yb + 0.010, 1.380, 0.060, 0.008, 0.040, METAL, 0.003)                  # hatch latch
box(bm, 0.140, yb0 + 0.120, 1.507, 0.030, 0.030, 0.016, METAL, 0.004)
cylinder(bm, (0.140, yb0 + 0.120, 1.605), (0, 0, 1), 0.004, 0.190, METAL, 8)
finalize_part(ob, bm, M, "spine02")
bm.free()
plss = ob

# head (CC0 MakeHuman) under the helmet
head = build_head("A7_SuitV03_EVA_Head", arm, M, MHDIR, SKIN, HAIR, EYEW, IRIS, SKIN, MALE)
HV = [head.matrix_world @ v.co for v in head.data.vertices if (head.matrix_world @ v.co).z > 1.58]
lo = Vector([min(p[i] for p in HV) for i in range(3)])
hi = Vector([max(p[i] for p in HV) for i in range(3)])
hc = Vector((0.0, (lo.y + hi.y) / 2 - 0.018, (lo.z + hi.z) / 2 + 0.012))
rx = max(0.172, (hi.x - lo.x) / 2 + 0.075)
ry = max(0.197, (hi.y - lo.y) / 2 + 0.085)
rz = max(0.192, (hi.z - lo.z) / 2 + 0.060)
log("head bbox", [round(x, 3) for x in lo], [round(x, 3) for x in hi], "helmet c", [round(x, 3) for x in hc], "r", round(rx, 3), round(ry, 3), round(rz, 3))
ZCUT = hc.z - 0.80 * rz
k = math.sqrt(1 - 0.80 ** 2)
rrx, rry = rx * k + 0.010, ry * k + 0.010

ob, bm = new_part("A7_SuitV03_EVA_Helmet", "head")
shell_from_ellipsoid(bm, hc, rx * 1.035, ry * 1.035, rz * 1.035,
                     lambda r: r.z > ZCUT - hc.z + 0.004 and (r.y > -0.045 or r.z > 0.72 * rz), SUIT, 0.010,
                     cuts=(((0, hc.y - 0.045, 0), (0, 1, 0)), ((0, 0, hc.z + 0.72 * rz), (0, 0, 1)), ((0, 0, ZCUT + 0.004), (0, 0, 1))))
ring = cylinder(bm, (0, hc.y, ZCUT - 0.012), (0, 0, 1), 1.0, 0.050, METAL, 36)
for v in {v for f in ring for v in f.verts}:
    v.co.x = v.co.x * rrx
    v.co.y = hc.y + (v.co.y - hc.y) * rry
rr = cylinder(bm, (0, hc.y, ZCUT + 0.018), (0, 0, 1), 1.0, 0.010, RED, 36)
for v in {v for f in rr for v in f.verts}:
    v.co.x = v.co.x * (rrx + 0.004)
    v.co.y = hc.y + (v.co.y - hc.y) * (rry + 0.004)
bmesh.ops.delete(bm, geom=[f for f in rr if len(f.verts) > 4], context="FACES")      # red stripe = open band
for sx in (1, -1):
    hx = sx * (rx * 1.035 + 0.030)
    box(bm, sx * (rx * 1.035 + 0.008), hc.y - 0.050, hc.z + 0.035, 0.026, 0.030, 0.030, METAL, 0.003)
    box(bm, hx, hc.y - 0.075, hc.z + 0.035, 0.040, 0.050, 0.075, STRAP, 0.006, 2)
    for dz in (0.017, -0.017):
        cylinder(bm, (hx, hc.y - 0.1015, hc.z + 0.035 + dz), (0, 1, 0), 0.0155, 0.004, DARK, 18)
        cylinder(bm, (hx, hc.y - 0.1040, hc.z + 0.035 + dz), (0, 1, 0), 0.0115, 0.004, LIGHT, 18)
    cylinder(bm, (sx * (rx * 1.07 + 0.006), hc.y - 0.010, hc.z + 0.005), (1, 0, 0), 0.022, 0.012, METAL, 18)
box(bm, 0.060, hc.y + 0.040, hc.z + rz * 1.035 - 0.004, 0.050, 0.070, 0.028, PANEL, 0.006, 2)
dam_o = [bm.verts.new((math.cos(2 * math.pi * i / 36) * rrx, hc.y + math.sin(2 * math.pi * i / 36) * rry, ZCUT - 0.030)) for i in range(36)]
dam_i = [bm.verts.new((math.cos(2 * math.pi * i / 36) * 0.072, -0.012 + math.sin(2 * math.pi * i / 36) * 0.082, ZCUT - 0.030)) for i in range(36)]
for i in range(36):
    j = (i + 1) % 36
    f = bm.faces.new((dam_i[i], dam_i[j], dam_o[j], dam_o[i]))
    f.material_index = METAL
finalize_part(ob, bm, M, "head")
bm.free()
helmet = ob

ob, bm = new_part("A7_SuitV03_EVA_Visor", "head")
shell_from_ellipsoid(bm, hc, rx, ry, rz, lambda r: r.z > ZCUT - hc.z and r.y < 0.03, CLEAR, 0.0,
                     cuts=(((0, 0, ZCUT), (0, 0, 1)), ((0, hc.y + 0.03, 0), (0, 1, 0))))
VX = 0.86 * rx * 1.07
shell_from_ellipsoid(bm, hc, rx * 1.07, ry * 1.07, rz * 1.07,
                     lambda r: r.y < -0.025 and r.z > -0.010 and abs(r.x) < VX, VISOR, 0.004,
                     cuts=(((VX, 0, 0), (1, 0, 0)), ((-VX, 0, 0), (1, 0, 0)), ((0, 0, hc.z - 0.010), (0, 0, 1)), ((0, hc.y - 0.025, 0), (0, 1, 0))))
finalize_part(ob, bm, M, "head")
bm.free()
visor = ob

# ------------------------------------------------------------------ naming, report, export
ren = {"A7_EVA_TorsoLegs": "A7_SuitV03_EVA_TorsoLegs", "A7_EVA_Arm_L": "A7_SuitV03_EVA_Arm_L", "A7_EVA_Arm_R": "A7_SuitV03_EVA_Arm_R"}
for o, n in ren.items():
    meshes[o].name = n
    meshes[o].data.name = n
for o in list(wobjs):
    bpy.data.objects.remove(o, do_unlink=True)
objs = [arm] + [meshes[o] for o in ren] + [head, chest, plss, helmet, visor]
assert bone_hash(arm) == HASH0
bpy.context.view_layer.update()
tri = 0
rep = {"hash": HASH0, "bones": len(arm.data.bones), "meshes": {}}
for o in objs[1:]:
    o.data.calc_loop_triangles()
    rep["meshes"][o.name] = {"verts": len(o.data.vertices), "tris": len(o.data.loop_triangles), "materials": [m.name for m in o.data.materials], "weights": vg_weight_report(o, arm)}
    tri += len(o.data.loop_triangles)
    xs = [(o.matrix_world @ v.co) for v in o.data.vertices]
    rep["meshes"][o.name]["bbox"] = [[round(min(p[i] for p in xs), 3) for i in range(3)], [round(max(p[i] for p in xs), 3) for i in range(3)]]
rep["tris_total"] = tri
json.dump(rep, open(os.path.join(OUT, "build_report.json"), "w"), indent=1)
log("tris", tri)
# the FBX must not carry this PC's texture path: export with the display texture unlinked (Unity's material has it)
dm = M.mats["MAT_V03_EvaDisplay"].node_tree
tex_links = [(l.from_socket, l.to_socket) for l in dm.links if l.from_node.type == "TEX_IMAGE"]
for l in [l for l in dm.links if l.from_node.type == "TEX_IMAGE"]:
    dm.links.remove(l)
if EXPORT:
    export_fbx(os.path.join(OUT, "A7_SuitV03_EVA.fbx"), objs)
    log("exported")
for a_, b_ in tex_links:
    dm.links.new(a_, b_)
for img in bpy.data.images:
    if img.name.startswith("T_V03_"):
        img.pack()
        img.filepath_raw = "//" + os.path.basename(img.filepath_raw)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "EVA.blend"))
