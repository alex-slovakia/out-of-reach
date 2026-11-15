"""EVA suit: off-white layered suit, red bands, chest panel, harness, PLSS, helmet+gold visor, gloves.
blender -b --factory-startup --python build_eva.py -- <in EVA fbx> <in WORK fbx (hands source)> <out dir> [--export]"""
import sys, os
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from suitlib import *

A = sys.argv[sys.argv.index("--") + 1:]
IN, INW, OUT = (os.path.abspath(a) for a in A[:3])
EXPORT = "--export" in A
os.makedirs(OUT, exist_ok=True)

arm, meshes = load_fbx(IN)
HASH0 = bone_hash(arm)
bpy.ops.import_scene.fbx(filepath=INW)
wobjs = [o for o in bpy.context.scene.objects if o.name not in meshes and o is not arm and o.name != arm.name and o.type in ("MESH", "ARMATURE")]
warm = next(o for o in wobjs if o.type == "ARMATURE")
wmesh = {o.name: o for o in wobjs if o.type == "MESH"}
log("bones", len(arm.data.bones), HASH0, "hands source", list(wmesh))

PAL = [
    ("MAT_V03_EvaSuit", dict(hexcol="#D6D2C8", rough=0.85)),
    ("MAT_V03_EvaPanel", dict(hexcol="#C7C2B6", rough=0.88)),
    ("MAT_V03_EvaBoot", dict(hexcol="#CBC6BA", rough=0.8)),
    ("MAT_V03_EvaBootGrey", dict(hexcol="#585C60", rough=0.85)),
    ("MAT_V03_EvaSole", dict(hexcol="#25272A", rough=0.9)),
    ("MAT_V03_EvaGlove", dict(hexcol="#CFCABF", rough=0.8)),
    ("MAT_V03_EvaGloveGrip", dict(hexcol="#4A4E52", rough=0.85)),
    ("MAT_V03_EvaStrap", dict(hexcol="#47494C", rough=0.88)),
    ("MAT_V03_EvaMetal", dict(hexcol="#9AA2A8", rough=0.4, metal=0.8)),
    ("MAT_V03_EvaRed", dict(hexcol="#B5382C", rough=0.75)),
    ("MAT_V03_EvaDisplay", dict(hexcol="#0B1620", rough=0.3, emission="#2DA8C8", emission_strength=1.2)),
    ("MAT_V03_EvaVisorGold", dict(hexcol="#C9A24A", rough=0.12, metal=1.0)),
    ("MAT_V03_EvaLight", dict(hexcol="#FFF2D0", rough=0.3, emission="#FFE9B0", emission_strength=6.0)),
    ("MAT_V03_EvaYellow", dict(hexcol="#E0B020", rough=0.5)),
    ("MAT_V03_EvaDark", dict(hexcol="#1B1C1E", rough=0.7)),
]
M = Mat(PAL)
SUIT, PANEL, BOOT, BGREY, SOLE, GLOVE, GRIP, STRAP, METAL, RED, DISPLAY, VISOR, LIGHT, YELLOW, DARK = (M.idx(n) for n, _ in PAL)
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
        d = 0.016 if z < 1.46 else 0.006
    else:
        d = 0.030 + 0.008 * min(1.0, max(0.0, (z - 0.2) / 0.6))
        inward = -sgn(v.co.x) * n.x
        if inward > 0:
            d *= (1.0 - 0.8 * min(1.0, inward))
    return n * d


for v in bm.verts:
    v.co = v.co + loose(v)
bm.normal_update()
for z in (1.512, 1.45, 0.83, 0.635, 1.40, 1.30, 1.03, 0.985, 0.92, 0.86, 0.80, 0.72, 0.60, 0.52, 0.47, 0.35, 0.17, 0.14, 0.08, SHOE_TOP, 0.06):
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


raise_region(bm, region(0.985, 1.03), 0.007, METAL)                          # waist ring
raise_region(bm, region(1.45, 1.512, xhi=0.11, absx=True), 0.008, METAL)                          # neck seal ring (lower)
raise_region(bm, region(1.30, 1.45, ylo=-9, yhi=-0.12, xlo=0.06, xhi=0.095, absx=True), 0.007, STRAP)   # shoulder-to-chest straps
raise_region(bm, region(0.80, 0.83), 0.006, STRAP)                           # thigh straps
raise_region(bm, region(0.60, 0.72, ylo=-0.065, yhi=0.02, xlo=0.125, absx=True), 0.010, PANEL)   # side thigh pockets
raise_region(bm, region(0.47, 0.60, ylo=-9, yhi=-0.06), 0.012, PANEL)       # knee pads
kr = region(0.52, 0.55, mat=PANEL, ylo=-9, yhi=-0.06)
raise_region(bm, region(0.60, 0.72, ylo=-9, yhi=-0.065, mat=SUIT, xlo=0.0, xhi=9), 0.0, SUIT) if False else None
raise_region(bm, region(0.35, 0.47, ylo=-9, yhi=-0.06), 0.006, PANEL)       # shin panel
raise_region(bm, region(0.14, 0.17), 0.007, STRAP)                           # boot-top ankle strap
raise_region(bm, region(0.60, 0.72, ylo=0.02, xlo=0.0, xhi=9), 0.0, SUIT) if False else None
# red band above the knees (front), clean planar strip
raise_region(bm, region(0.60, 0.635, ylo=-9, yhi=-0.06), 0.0035, RED)
finish_mesh(tl, bm, M, 38.0)
log("TorsoLegs", len(tl.data.vertices), vg_weight_report(tl, arm))
bm.free()

# ------------------------------------------------------------------ arms + gloves
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
                for gi, w in v[sdl].items():
                    nv[ddl][dmap[smap[gi]]] = w
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
    la, wr = arm.data.bones["lowerarm02." + s], arm.data.bones["wrist." + s]
    d = (wr.head_local - la.head_local).normalized()
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bm.faces.ensure_lookup_table()
    s_end = max(fcenter(f).dot(d) for f in bm.faces)
    for f in bm.faces:
        f.material_index = SUIT
    vnn = vnormals_from_faces(bm.faces)
    for v, n in vnn.items():
        v.co = v.co + n * 0.012
    bm.normal_update()
    # sleeve bands: upper arm red patch band + metal wrist ring + elbow pad
    for z in (0.0,):
        pass
    cut(bm, d * (s_end - 0.05), d)
    cut(bm, d * (s_end - 0.012), d)
    bm.normal_update()
    raise_region(bm, [f for f in bm.faces if fcenter(f).dot(d) > s_end - 0.05 and fcenter(f).dot(d) <= s_end - 0.012], 0.010, METAL)
    # red band on the upper arm (planar band across the arm at fixed z)
    cutz(bm, 1.36); cutz(bm, 1.40)
    raise_region(bm, [f for f in bm.faces if 1.36 <= fcenter(f).z <= 1.40 and f.material_index == SUIT and fcenter(f).dot(d) < s_end - 0.2], 0.0035, RED)
    # gloves from the WORK hands
    sd = d
    def is_hand(f, src=src, sd=sd, s_end=s_end):
        return f.material_index == 5 and fcenter(f).dot(sd) > s_end - 0.035
    nf = copy_faces(src, is_hand, ob, bm)
    vh = vnormals_from_faces(nf)
    for v, n in vh.items():
        v.co = v.co + n * 0.007
    dl = bm.verts.layers.deform.verify()
    names = {g.index: g.name for g in ob.vertex_groups}
    for f in nf:
        w = {}
        for v in f.verts:
            for gi, ww in v[dl].items():
                w[names[gi]] = w.get(names[gi], 0) + ww
        dom = max(w, key=w.get) if w else ""
        f.material_index = GRIP if dom.startswith("finger") and not dom.startswith("finger1-1") else GLOVE
    bm.normal_update()
    finish_mesh(ob, bm, M, 40.0)
    log(ob.name, len(ob.data.vertices), "glove faces", len(nf), vg_weight_report(ob, arm))
    bm.free()

# ------------------------------------------------------------------ separate rigid parts (all skinned 100 % to one bone)
def new_part(name, bone):
    ob = make_part(name, arm, bone, M, None)
    bm = bmesh.new()
    return ob, bm


# chest control panel (spine02)
ob, bm = new_part("A7_SuitV03_EVA_ChestPanel", "spine02")
cy = -0.222
box(bm, 0, cy, 1.275, 0.215, 0.050, 0.150, BGREY, 0.006)
box(bm, 0, cy - 0.022, 1.282, 0.125, 0.006, 0.058, DISPLAY, 0.002)       # display
for i in range(3):
    for j in range(2):
        box(bm, 0.085 + 0.018 * i, cy - 0.024, 1.292 - 0.022 * j, 0.012, 0.008, 0.012, STRAP, 0.002)   # buttons (right)
for k, x in enumerate((-0.075, -0.035, 0.005)):
    cylinder(bm, (x, cy - 0.026, 1.215), (0, 1, 0), 0.016, 0.014, STRAP, 16)       # knobs
cylinder(bm, (0.062, cy - 0.027, 1.215), (0, 1, 0), 0.0155, 0.012, YELLOW, 20)    # yellow ring
box(bm, 0, cy + 0.012, 1.395, 0.15, 0.040, 0.07, PANEL, 0.006)                  # upper chest pocket
finalize_part(ob, bm, M, "spine02")
bm.free()
chest = ob

# PLSS backpack (spine02)
ob, bm = new_part("A7_SuitV03_EVA_Plss", "spine02")
py = 0.250
box(bm, 0, py, 1.255, 0.350, 0.190, 0.460, SUIT, 0.020, 3)
box(bm, 0, py + 0.098, 1.255, 0.24, 0.006, 0.040, RED, 0.002)                    # red band, back face
box(bm, 0, py + 0.099, 1.34, 0.12, 0.008, 0.026, METAL, 0.004)                   # handle
box(bm, 0, py + 0.05, 1.012, 0.30, 0.150, 0.080, PANEL, 0.015, 3)                # base module
cylinder(bm, (0, py + 0.005, 0.925), (1, 0, 0), 0.058, 0.300, PANEL, 24)          # lower canister
box(bm, 0.135, py + 0.10, 1.12, 0.04, 0.012, 0.09, STRAP, 0.003)
box(bm, -0.135, py + 0.10, 1.12, 0.04, 0.012, 0.09, STRAP, 0.003)
cylinder(bm, (0.15, py + 0.05, 1.56), (0, 0, 1), 0.006, 0.18, METAL, 8)           # antenna
finalize_part(ob, bm, M, "spine02")
bm.free()
plss = ob

# helmet shell + neck ring (head)
hc = (0.0, -0.022, 1.700)
ob, bm = new_part("A7_SuitV03_EVA_Helmet", "head")
faces = ellipsoid(bm, hc, 0.175, 0.200, 0.195, SUIT, 36, 20)
bmesh.ops.recalc_face_normals(bm, faces=faces)
bm.normal_update()
for f in faces:
    c = f.calc_center_median()
    if c.y < hc[1] - 0.07 and abs(c.x) < 0.14 and hc[2] - 0.08 < c.z < hc[2] + 0.12:
        f.material_index = DARK
cylinder(bm, (0, -0.012, 1.538), (0, 0, 1), 0.112, 0.050, METAL, 28)
cylinder(bm, (0, -0.012, 1.566), (0, 0, 1), 0.116, 0.010, RED, 28)
for sx in (1, -1):
    box(bm, sx * 0.205, -0.085, 1.735, 0.030, 0.060, 0.050, STRAP, 0.006)   # side light pods
    cylinder(bm, (sx * 0.222, -0.085, 1.735), (1, 0, 0), 0.018, 0.012, LIGHT, 16)
finalize_part(ob, bm, M, "head")
bm.free()
helmet = ob

# visor (gold) = outer shell copy of the dark front faces, raised
ob, bm = new_part("A7_SuitV03_EVA_Visor", "head")
faces = ellipsoid(bm, hc, 0.175, 0.200, 0.195, VISOR, 36, 20)
bmesh.ops.recalc_face_normals(bm, faces=faces)
bm.normal_update()
keep = [f for f in bm.faces if (lambda c: c.y < hc[1] - 0.07 and abs(c.x) < 0.14 and hc[2] - 0.08 < c.z < hc[2] + 0.12)(f.calc_center_median())]
rest = [f for f in bm.faces if f not in set(keep)]
bmesh.ops.delete(bm, geom=rest, context="FACES")
for v in bm.verts:
    d0 = (v.co - Vector(hc))
    v.co = Vector(hc) + Vector((d0.x * 1.045, d0.y * 1.05, d0.z * 1.045))
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
objs = [arm] + [meshes[o] for o in ren] + [chest, plss, helmet, visor]
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
if EXPORT:
    export_fbx(os.path.join(OUT, "A7_SuitV03_EVA.fbx"), objs)
    log("exported")

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "EVA.blend"))
