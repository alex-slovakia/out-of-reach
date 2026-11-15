"""WORK suit: navy long-sleeve shirt, charcoal cargo trousers, dark low shoes, existing bare hands.
blender -b --factory-startup --python build_work.py -- <in FitV02 WORK fbx> <out dir> [--export]"""
import sys, os
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from suitlib import *
from head import build_head

A = sys.argv[sys.argv.index("--") + 1:]
IN, OUT, MHDIR = os.path.abspath(A[0]), os.path.abspath(A[1]), os.path.abspath(A[2])
EXPORT = "--export" in A
os.makedirs(OUT, exist_ok=True)

arm, meshes = load_fbx(IN)
HASH0 = bone_hash(arm)
log("bones", len(arm.data.bones), HASH0)

PAL = [
    ("MAT_V03_Shirt", dict(hexcol="#26324F", rough=0.88)),
    ("MAT_V03_Trousers", dict(hexcol="#3A3B3D", rough=0.9)),
    ("MAT_V03_TrousersPanel", dict(hexcol="#333437", rough=0.92)),
    ("MAT_V03_Shoe", dict(hexcol="#2A2C2F", rough=0.8)),
    ("MAT_V03_Sole", dict(hexcol="#16181A", rough=0.9)),
    ("MAT_V03_Skin", dict(hexcol="#A67A64", rough=0.65)),
    ("MAT_V03_Trim", dict(hexcol="#1B1C1E", rough=0.7)),
    ("MAT_V03_Hair", dict(hexcol="#2B211B", rough=0.75)),
    ("MAT_V03_EyeWhite", dict(hexcol="#D8D3CA", rough=0.3)),
    ("MAT_V03_EyeIris", dict(hexcol="#3A2B22", rough=0.2)),
    ("MAT_V03_ShirtSeam", dict(hexcol="#1E2843", rough=0.9)),
    ("MAT_V03_ShoeMid", dict(hexcol="#4A4D52", rough=0.85)),
]
M = Mat(PAL)
SHIRT, TROUS, PANEL, SHOE, SOLE, SKIN, TRIM, HAIR, EYEW, IRIS, SEAM, SHOEMID = (M.idx(n) for n, _ in PAL)
HEM_Z = 0.925
SHOE_TOP = 0.092
NECK_Z = 1.512
COLLAR_Z = 1.484


def sgn(x):
    return 1.0 if x >= 0 else -1.0


tl = meshes["A7_WORK_TorsoLegs"]
bm = bmesh.new()
bm.from_mesh(tl.data)
bm.faces.ensure_lookup_table()
flat = set()
for f in bm.faces:
    om = f.material_index
    if om == 3:
        nm = SOLE
    elif om == 2:
        nm = SHOE
    else:
        nm = SHIRT
    if om in (1, 4, 6, 7):
        flat.update(f.verts)
    f.material_index = nm
smooth_set(bm, [v for v in flat if v.co.z > 0.2], iters=45, factor=0.6)
bm.normal_update()
vn = {v: v.normal.copy() for v in bm.verts}


def loose(v):
    z, n = v.co.z, vn[v]
    if z < SHOE_TOP + 0.005:
        return n * 0.004
    if z >= HEM_Z - 0.04:                          # shirt
        if z > 1.46:
            d = 0.004
        else:
            t = min(1.0, max(0.0, (z - 1.38) / 0.08))   # chest (0.006) -> waist/hem (0.016)
            d = 0.011 - 0.005 * (z - 0.885) / (1.38 - 0.885) if z < 1.38 else 0.006
            d = max(0.006, d) if z < 1.38 else 0.006
    else:                                          # straight-cut trousers: slim at the hips, fuller calves
        if z >= 0.86:
            d = 0.012
        elif z >= 0.62:
            d = 0.026 - 0.014 * (z - 0.62) / 0.24
        elif z >= 0.30:
            d = 0.036 - 0.010 * (z - 0.30) / 0.32
        elif z >= 0.15:
            d = 0.038
        else:
            d = 0.040
        inward = -sgn(v.co.x) * n.x
        if inward > 0:
            d *= (1.0 - 0.8 * min(1.0, inward))
    return n * d


for v in bm.verts:
    v.co = v.co + loose(v)
bm.normal_update()

# planar cuts: every region boundary is a clean line
for z in (1.400, 1.404, 0.022, NECK_Z, COLLAR_Z, HEM_Z - 0.03, HEM_Z, HEM_Z + 0.032, SHOE_TOP, 0.60, 0.80, 0.40, 0.57, 0.835, 0.915):
    cutz(bm, z)
for y in (-0.065, 0.02, -0.06, 0.06):
    cuty(bm, y)
for x in (0.125, 0.035, 0.16):
    cutx(bm, x)
bm.normal_update()

for f in bm.faces:
    c = fcenter(f)
    m = f.material_index
    if m == SHIRT:
        if c.z >= NECK_Z:
            f.material_index = SKIN
        elif c.z >= HEM_Z:
            f.material_index = SHIRT
        else:
            f.material_index = TROUS
    elif m == SHOE:
        if c.z > SHOE_TOP:
            f.material_index = TROUS
for f in bm.faces:
    if f.material_index == SOLE and fcenter(f).z > 0.022:
        f.material_index = SHOEMID
# the skin neck stub is replaced by the real head/neck (A7_SuitV03_WORK_Head)
bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.material_index == SKIN], context="FACES")
bm.faces.ensure_lookup_table()


def region(zlo, zhi, extra=None, mat=TROUS, ylo=-9, yhi=9, xlo=-9, xhi=9, absx=False):
    out = []
    for f in bm.faces:
        if f.material_index != mat:
            continue
        c = fcenter(f)
        x = abs(c.x) if absx else c.x
        if zlo <= c.z <= zhi and ylo <= c.y <= yhi and xlo <= x <= xhi and (extra is None or extra(f, c)):
            out.append(f)
    return out


# --- shirt: hem band, collar ring
raise_region(bm, region(HEM_Z, HEM_Z + 0.032, mat=SHIRT), 0.004, SHIRT)
raise_region(bm, region(COLLAR_Z, NECK_Z, mat=SHIRT, ylo=-0.2, yhi=0.2), 0.0055, SHIRT)
# back yoke seam
raise_region(bm, region(1.400, 1.404, mat=SHIRT, ylo=0.03), 0.0012, SEAM)
# --- side cargo pockets (outer thigh) with flap and zip
pk = region(0.60, 0.80, ylo=-0.065, yhi=0.02, xlo=0.125, absx=True)
caps = raise_region(bm, pk, 0.008, PANEL)
raise_region(bm, [f for f in caps if fcenter(f).z > 0.775], 0.004, PANEL)
ymin = min(fcenter(g).y for g in caps)
raise_region(bm, [f for f in caps if fcenter(f).y < ymin + 0.016 and fcenter(f).z < 0.775], 0.0015, TRIM)
# --- knee panels, back pockets
raise_region(bm, region(0.40, 0.57, ylo=-9, yhi=-0.06), 0.006, PANEL)
raise_region(bm, region(0.835, 0.915, ylo=0.06, xlo=0.035, xhi=0.16, absx=True), 0.004, PANEL)
# waistband under the hem

finish_mesh(tl, bm, M, 38.0)
log("TorsoLegs verts", len(tl.data.vertices), vg_weight_report(tl, arm))
bm.free()

# --------------------------------------------------------------------- arms: planar sleeve end
for s in "LR":
    ob = meshes["A7_WORK_Arm_" + s]
    la, wr = arm.data.bones["lowerarm02." + s], arm.data.bones["wrist." + s]
    d = (wr.head_local - la.head_local).normalized()
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bm.faces.ensure_lookup_table()
    s_end = max(fcenter(f).dot(d) for f in bm.faces if f.material_index == 0)
    log("sleeve end", s, round(s_end, 4), "wrist", round(wr.head_local.dot(d), 4))
    for f in bm.faces:
        f.material_index = SHIRT if f.material_index == 0 else SKIN
    vnn = vnormals_from_faces(bm.faces)
    for v, n in vnn.items():
        if all(f.material_index == SHIRT for f in v.link_faces):
            t = min(1.0, max(0.0, (v.co.z - 1.15) / 0.3))
            v.co = v.co + n * (0.0035 + 0.0035 * t)
    bm.normal_update()
    cut(bm, d * (s_end - 0.045), d)
    bm.normal_update()
    cf = [f for f in bm.faces if f.material_index == SHIRT and fcenter(f).dot(d) > s_end - 0.045]
    raise_region(bm, cf, 0.0045, SHIRT)
    finish_mesh(ob, bm, M, 40.0)
    log(ob.name, "verts", len(ob.data.vertices), vg_weight_report(ob, arm))
    bm.free()

head = build_head("A7_SuitV03_WORK_Head", arm, M, MHDIR, SKIN, HAIR, EYEW, IRIS, SKIN)
ren = {"A7_WORK_TorsoLegs": "A7_SuitV03_WORK_TorsoLegs", "A7_WORK_Arm_L": "A7_SuitV03_WORK_Arm_L", "A7_WORK_Arm_R": "A7_SuitV03_WORK_Arm_R"}
for o, n in ren.items():
    meshes[o].name = n
    meshes[o].data.name = n
objs = [arm] + [meshes[o] for o in ren] + [head]
assert bone_hash(arm) == HASH0
bpy.context.view_layer.update()
tri = 0
rep = {"hash": HASH0, "bones": len(arm.data.bones), "meshes": {}}
for o in objs[1:]:
    o.data.calc_loop_triangles()
    rep["meshes"][o.name] = {"verts": len(o.data.vertices), "tris": len(o.data.loop_triangles), "materials": [m.name for m in o.data.materials], "weights": vg_weight_report(o, arm)}
    tri += len(o.data.loop_triangles)
rep["tris_total"] = tri
json.dump(rep, open(os.path.join(OUT, "build_report.json"), "w"), indent=1)
log("tris", tri)
if EXPORT:
    export_fbx(os.path.join(OUT, "A7_SuitV03_WORK.fbx"), objs)
    log("exported")

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "WORK.blend"))
