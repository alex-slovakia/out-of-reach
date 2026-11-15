"""CHAR-REF-01 V03c: characters assembled from MakeHuman CC0 system assets on the unchanged FitV02 rig.
blender -b --factory-startup --python build_mh.py -- --suit WORK|EVA --fit <FitV02 fbx> --mh <mh_cc0> --sa <system assets>
        --delta <male delta npy> --tex <prepared textures dir> --out <dir> [--display <png>] [--export]"""
import sys, os, json, math
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from suitlib import (log, load_fbx, bone_hash, raise_region, cut, cutz, cuty, cutx, box, cylinder, torus, tube, uv_quad,
                     shell_from_ellipsoid, limit_influences, set_edge_sharpness, export_fbx, male_tweak, surface_point,
                     vg_weight_report, hex2lin, _frame, _smooth01, polar_profile, ring_between, join_into)
from mhproxy import MaleBase, build_proxy, build_body, parse_mhclo
from assemble import tag_part, apply_tweak, join_all, classify, split_by

A = sys.argv[sys.argv.index("--") + 1:]


def arg(k, d=None):
    return A[A.index(k) + 1] if k in A else d


SUIT = arg("--suit", "WORK")
FIT, MH, SA, DELTA, TEX, OUT = (os.path.abspath(arg(k)) for k in ("--fit", "--mh", "--sa", "--delta", "--tex", "--out"))
DISPLAY = arg("--display")
EXPORT = "--export" in A
os.makedirs(OUT, exist_ok=True)
SPEC = {}


def smat(name, base="#FFFFFF", rough=0.8, metal=0.0, diffuse=None, normal=None, alpha_clip=False, transparent=None,
         emission=None, normal_strength=1.0):
    """Blender material + packaging spec (Unity URP Lit gets the same: base tint x texture, normal, cutout/transparent)."""
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes["Principled BSDF"]
    tint = hex2lin(base)
    col = None
    if diffuse:
        img = bpy.data.images.load(diffuse, check_existing=True)
        tn = nt.nodes.new("ShaderNodeTexImage")
        tn.image = img
        mix = nt.nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        mix.blend_type = "MULTIPLY"
        mix.inputs["Factor"].default_value = 1.0
        nt.links.new(tn.outputs["Color"], mix.inputs[6])
        mix.inputs[7].default_value = tint
        nt.links.new(mix.outputs[2], b.inputs["Base Color"])
        if alpha_clip:
            nt.links.new(tn.outputs["Alpha"], b.inputs["Alpha"])
            try:
                m.surface_render_method = "DITHERED"
            except Exception:
                pass
        if emission:
            nt.links.new(mix.outputs[2], b.inputs["Emission Color"])
            b.inputs["Emission Strength"].default_value = emission[1]
    else:
        b.inputs["Base Color"].default_value = tint
        if emission:
            b.inputs["Emission Color"].default_value = hex2lin(emission[0])
            b.inputs["Emission Strength"].default_value = emission[1]
    if normal:
        img = bpy.data.images.load(normal, check_existing=True)
        img.colorspace_settings.name = "Non-Color"
        tn = nt.nodes.new("ShaderNodeTexImage")
        tn.image = img
        nm = nt.nodes.new("ShaderNodeNormalMap")
        nm.inputs["Strength"].default_value = normal_strength
        nt.links.new(tn.outputs["Color"], nm.inputs["Color"])
        nt.links.new(nm.outputs["Normal"], b.inputs["Normal"])
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    if transparent is not None:
        b.inputs["Alpha"].default_value = transparent
        try:
            m.surface_render_method = "BLENDED"
        except Exception:
            pass
    m.diffuse_color = tint
    SPEC[name] = {"base": base, "rough": rough, "metal": metal, "diffuse": diffuse, "normal": normal,
                  "alpha_clip": alpha_clip, "transparent": transparent, "emission": emission, "normal_strength": normal_strength}
    return m


def slot(ob, mat):
    mats = ob.data.materials
    for i, m in enumerate(mats):
        if m == mat:
            return i
    mats.append(mat)
    return len(mats) - 1


def asset(*p):
    return os.path.join(SA, *p)


# ------------------------------------------------------------------ rig + male base
arm, fitm = load_fbx(FIT)
HASH0 = bone_hash(arm)
for o in list(fitm.values()):
    bpy.data.objects.remove(o, do_unlink=True)
mb = MaleBase(MH, DELTA)
log("rig", len(arm.data.bones), HASH0)

# ------------------------------------------------------------------ materials
CS = asset("clothes", "male_casualsuit02")
SKIN = smat("MAT_V03_Skin", "#FFFFFF", 0.55, diffuse=asset("skins", "middleage_caucasian_male", "middleage_lightskinned_male_diffuse.png"))
HAIR = smat("MAT_V03_Hair", "#8C7A6E", 0.7, diffuse=asset("hair", "short01", "short01_diffuse.png"), alpha_clip=True)
BROW = smat("MAT_V03_Brows", "#7A6A60", 0.8, diffuse=asset("eyebrows", "eyebrow001", "eyebrow001.png"), alpha_clip=True)
LASH = smat("MAT_V03_Lashes", "#FFFFFF", 0.8, diffuse=asset("eyelashes", "eyelashes01", "eyelashes01.png"), alpha_clip=True)
EYE = smat("MAT_V03_Eyes", "#FFFFFF", 0.15, diffuse=asset("eyes", "materials", "brown_eye.png"), alpha_clip=True)
if SUIT == "WORK":
    CLOTH_A = smat("MAT_V03_Shirt", "#30406A", 0.92, diffuse=os.path.join(TEX, "cs02_gray.png"), normal=os.path.join(CS, "male_casualsuit02_normal.png"))
    CLOTH_B = smat("MAT_V03_Trousers", "#4C4E52", 0.92, diffuse=os.path.join(TEX, "cs02_gray.png"), normal=os.path.join(CS, "male_casualsuit02_normal.png"))
    PANEL = smat("MAT_V03_TrousersPanel", "#43454A", 0.94, diffuse=os.path.join(TEX, "cs02_gray.png"), normal=os.path.join(CS, "male_casualsuit02_normal.png"))
    TRIM = smat("MAT_V03_Trim", "#1B1C1E", 0.6, metal=0.2)
    SHOE = smat("MAT_V03_Shoe", "#4A4E53", 0.85, diffuse=os.path.join(TEX, "shoes06_gray.png"))
else:
    CLOTH_A = CLOTH_B = smat("MAT_V03_EvaSuit", "#DCD5C7", 0.9, diffuse=os.path.join(TEX, "cs02_gray_eva.png"), normal=os.path.join(CS, "male_casualsuit02_normal.png"), normal_strength=1.8)
    PANEL = smat("MAT_V03_EvaPanel", "#D6D0C3", 0.9, diffuse=os.path.join(TEX, "cs02_gray_eva.png"), normal=os.path.join(CS, "male_casualsuit02_normal.png"))
    SHOE = smat("MAT_V03_EvaBoot", "#F2EEE6", 0.85, diffuse=os.path.join(TEX, "shoes03_gray.png"))
    STRAP = smat("MAT_V03_EvaStrap", "#4A4D51", 0.88)
    METAL = smat("MAT_V03_EvaMetal", "#A6ACB0", 0.5, metal=0.55)
    RED = smat("MAT_V03_EvaRed", "#A63A30", 0.85)
    GLOVE = smat("MAT_V03_EvaGlove", "#D4CFC4", 0.85)
    GRIP = smat("MAT_V03_EvaGloveGrip", "#4B4F53", 0.9)
    DARK = smat("MAT_V03_EvaDark", "#1B1C1E", 0.6)
    YELLOW = smat("MAT_V03_EvaYellow", "#E0B020", 0.5)
    LIGHT = smat("MAT_V03_EvaLight", "#FFF2D0", 0.3, emission=("#FFE9B0", 6.0))
    VISOR = smat("MAT_V03_EvaVisorGold", "#C9A24A", 0.15, metal=1.0)
    CLEAR = smat("MAT_V03_EvaVisorClear", "#C8D6DE", 0.05, transparent=0.10)
    DISPLAYM = smat("MAT_V03_EvaDisplay", "#FFFFFF", 0.3, diffuse=os.path.abspath(DISPLAY), emission=("#FFFFFF", 1.5))
    PANELHW = smat("MAT_V03_EvaHardware", "#CEC8BC", 0.75)
    GREYHW = smat("MAT_V03_EvaBootGrey", "#5A5E62", 0.8)
    SHAFT = smat("MAT_V03_EvaBootShaft", "#CDC6B8", 0.82)

# ------------------------------------------------------------------ fit the CC0 proxies
suit, suit_clo = build_proxy("suit", arm, mb, CS, mat=CLOTH_A)
shoes_name = "shoes06" if SUIT == "WORK" else "shoes03"
shoes, shoes_clo = build_proxy("shoes", arm, mb, asset("clothes", shoes_name), mat=SHOE)
hair, _ = build_proxy("hair", arm, mb, asset("hair", "short01"), mat=HAIR)
brows, _ = build_proxy("brows", arm, mb, asset("eyebrows", "eyebrow001"), mat=BROW)
lashes, _ = build_proxy("lashes", arm, mb, asset("eyelashes", "eyelashes01"), mat=LASH)
eyes, _ = build_proxy("eyes", arm, mb, asset("eyes", "high-poly"), mat=EYE)
body = build_body("body", arm, mb, suit_clo["delete"] | shoes_clo["delete"], SKIN)
deform = [suit, shoes, hair, brows, lashes, eyes, body]
for o in deform:
    apply_tweak(o, male_tweak)
log("proxies fitted", [(o.name, len(o.data.vertices)) for o in deform])

# shirt / trousers split of the shared atlas (WORK): jeans live at u < 0.73, v < 0.59
if SUIT == "WORK":
    ib = slot(suit, CLOTH_B)
    uv = suit.data.uv_layers[0].data
    for p in suit.data.polygons:
        us = [uv[li].uv for li in p.loop_indices]
        u = sum(x[0] for x in us) / len(us)
        v = sum(x[1] for x in us) / len(us)
        cz = sum((suit.data.vertices[i].co for i in p.vertices), Vector()).z / len(p.vertices)
        p.material_index = ib if ((u < 0.73 and v < 0.603) or cz < 0.90) else 0


def gname(ob):
    return {g.index: g.name for g in ob.vertex_groups}


def dominant(ob, v, names):
    if not v.groups:
        return ""
    g = max(v.groups, key=lambda g: g.weight)
    return names[g.group]


ARMB = ("upperarm", "lowerarm", "wrist", "metacarpal", "finger")


def sgn(x):
    return 1.0 if x >= 0 else -1.0


# ------------------------------------------------------------------ garment shaping + details
bm = bmesh.new()
bm.from_mesh(suit.data)
dl = bm.verts.layers.deform.verify()
names = gname(suit)
bm.normal_update()
vn = {v: v.normal.copy() for v in bm.verts}
MA, MB = 0, slot(suit, CLOTH_B)


def vdom(v):
    d = v[dl]
    return names[max(d.keys(), key=lambda k: d[k])] if len(d) else ""


for v in bm.verts:
    z, n = v.co.z, vn[v]
    isarm = vdom(v).startswith(ARMB)
    if SUIT == "WORK":
        if any(f.material_index == MB for f in v.link_faces) and z < 0.90:      # relaxed straight trouser legs
            d = 0.003 if z > 0.86 else (0.006 if z > 0.60 else 0.008 + 0.006 * (0.60 - min(z, 0.60)) / 0.45)
            inward = -sgn(v.co.x) * n.x
            if inward > 0:
                d *= 1.0 - 0.8 * min(1.0, inward)
            v.co += n * d
    else:                                                                       # bulky pressure-suit layers
        if isarm:
            d = 0.020
        elif z > 0.92:
            d = 0.030 if z < 1.44 else max(0.008, 0.030 - 0.022 * (z - 1.44) / 0.06)
        else:
            d = 0.022 + 0.012 * min(1.0, max(0.0, (z - 0.2) / 0.6))       # slimmer towards the boots: longer-looking legs
            inward = -sgn(v.co.x) * n.x
            if inward > 0:
                d *= 1.0 - 0.8 * min(1.0, inward)
        v.co += n * d
bm.normal_update()


def region(zlo, zhi, mats, ylo=-9, yhi=9, xlo=-9, xhi=9, absx=False, noarm=True):
    out = []
    for f in bm.faces:
        if f.material_index not in mats:
            continue
        c = f.calc_center_median()
        x = abs(c.x) if absx else c.x
        if not (zlo <= c.z <= zhi and ylo <= c.y <= yhi and xlo <= x <= xhi):
            continue
        if noarm and any(vdom(v).startswith(ARMB) for v in f.verts):
            continue
        out.append(f)
    return out


if SUIT == "WORK":
    TR, PN, TRM = MB, slot(suit, PANEL), slot(suit, TRIM)
    for z in (0.60, 0.80, 0.775, 0.40, 0.57):
        cutz(bm, z)
    for y in (-0.065, 0.02, -0.06):
        cuty(bm, y)
    cutx(bm, 0.125)
    bm.normal_update()
    caps = raise_region(bm, region(0.60, 0.80, {TR}, ylo=-0.065, yhi=0.02, xlo=0.125, absx=True), 0.008, PN)   # cargo pockets
    if caps:
        raise_region(bm, [f for f in caps if f.calc_center_median().z > 0.775], 0.004, PN)                     # flaps
        ymin = min(f.calc_center_median().y for f in caps)
        raise_region(bm, [f for f in caps if f.calc_center_median().y < ymin + 0.016 and f.calc_center_median().z < 0.775], 0.0015, TRM)
    raise_region(bm, region(0.40, 0.57, {TR}, yhi=-0.06), 0.006, PN)                                           # knee panels
else:
    SU, PN, ST, ME, RD = 0, slot(suit, PANEL), slot(suit, STRAP), slot(suit, METAL), slot(suit, RED)
    ZC = (1.512, 1.45, 1.40, 1.36, 1.33, 1.30, 1.18, 1.175, 1.10, 1.095, 1.085, 1.04, 1.03, 0.985, 0.92, 0.86, 0.83, 0.80, 0.78, 0.76, 0.72,
          0.64, 0.60, 0.585, 0.56, 0.55, 0.42, 0.365, 0.36, 0.30, 0.25, 0.245, 0.19, 0.18, 0.165)
    for z in ZC:
        cutz(bm, z)
    for y in (-0.06, -0.065, 0.02):
        cuty(bm, y)
    for x in (0.125, 0.06, 0.095, 0.16):
        cutx(bm, x)
    bm.normal_update()
    S_ = {SU}
    raise_region(bm, region(1.04, 1.085, S_), 0.008, ME)                                                       # waist ring at the waist
    raise_region(bm, region(1.30, 1.512, S_, xlo=0.06, xhi=0.095, absx=True), 0.007, ST)                      # harness straps
    raise_region(bm, region(0.80, 0.83, S_), 0.006, ST)                                                        # thigh straps
    raise_region(bm, region(0.60, 0.72, S_, ylo=-0.065, yhi=0.02, xlo=0.125, absx=True), 0.010, PN)           # side pockets
    fp = raise_region(bm, region(0.64, 0.78, S_, yhi=-0.06, xlo=0.06, xhi=0.16, absx=True), 0.009, PN)        # front pockets
    raise_region(bm, [f for f in fp if f.is_valid and f.calc_center_median().z > 0.76], 0.004, PN)
    raise_region(bm, region(0.42, 0.55, S_, yhi=-0.06), 0.007, PN)                                           # knee pads (smooth)
    raise_region(bm, region(0.30, 0.42, S_, yhi=-0.06), 0.0035, PN)                                          # shin panels
    SH = slot(suit, SHAFT)
    raise_region(bm, region(0.0, 0.19, S_, noarm=False), 0.008, SH)                                          # boot shafts
    raise_region(bm, region(0.165, 0.18, {SH}, noarm=False), 0.004, ST)                                      # boot strap
    raise_region(bm, region(0.56, 0.585, S_, yhi=-0.06), 0.004, RD)                                          # red bands at the knees
    for lo_, hi_ in ((1.175, 1.18), (1.095, 1.10), (0.36, 0.365), (0.245, 0.25)):
        raise_region(bm, region(lo_, hi_, S_), 0.0025, PN)                                                     # layer seams
    # arms: upper-arm red bands, elbow bellows, wrist rings (cuts limited to the arm faces)
    for s in "LR":
        ua, la, wr = (arm.data.bones[n + "." + s] for n in ("upperarm02", "lowerarm01", "wrist"))
        elbow = la.head_local.copy()
        dup = (la.head_local - ua.head_local).normalized()
        dlo = (wr.head_local - la.head_local).normalized()
        d = (wr.head_local - arm.data.bones["lowerarm02." + s].head_local).normalized()
        armf = [f for f in bm.faces if f.material_index == SU and sum(1 for v in f.verts if vdom(v).startswith(ARMB) and vdom(v).endswith("." + s)) >= 2]
        if not armf:
            continue
        s_end = max(f.calc_center_median().dot(d) for f in armf)
        planes = [(dup * (elbow.dot(dup) + o), dup) for o in (-0.070, -0.062, -0.056, -0.048, -0.042, -0.034, -0.028, -0.020)]
        planes += [(dlo * (elbow.dot(dlo) + o), dlo) for o in (0.020, 0.028, 0.034, 0.042, 0.048, 0.056)]
        planes += [(d * (s_end - 0.05), d), (d * (s_end - 0.012), d)]
        for co, no in planes:
            geom = list({e for f in armf if f.is_valid for e in f.edges}) + [f for f in armf if f.is_valid] + list({v for f in armf if f.is_valid for v in f.verts})
            bmesh.ops.bisect_plane(bm, geom=geom, plane_co=co, plane_no=no, dist=1e-6)
            armf = [f for f in bm.faces if f.material_index == SU and sum(1 for v in f.verts if vdom(v).startswith(ARMB) and vdom(v).endswith("." + s)) >= 2]
        bm.normal_update()
        for o1, o2, ax in ((-0.070, -0.062, dup), (-0.056, -0.048, dup), (-0.042, -0.034, dup), (-0.028, -0.020, dup),
                           (0.020, 0.028, dlo), (0.034, 0.042, dlo), (0.048, 0.056, dlo)):
            lo_, hi_ = elbow.dot(ax) + o1, elbow.dot(ax) + o2
            raise_region(bm, [f for f in armf if f.is_valid and f.material_index == SU and lo_ <= f.calc_center_median().dot(ax) <= hi_
                              and (f.calc_center_median() - elbow).length < 0.11], 0.004, SU)
        armf = [f for f in armf if f.is_valid]
        raise_region(bm, [f for f in armf if s_end - 0.05 < f.calc_center_median().dot(d) <= s_end - 0.012], 0.010, ME)
        for co, no in (((0, -0.035, 0), (0, 1, 0)), ((0, 0.035, 0), (0, 1, 0))):
            armf = [f for f in armf if f.is_valid]
            geom = list({e for f in armf for e in f.edges}) + armf + list({v for f in armf for v in f.verts})
            bmesh.ops.bisect_plane(bm, geom=geom, plane_co=co, plane_no=no, dist=1e-6)
        armf = [f for f in bm.faces if f.material_index == SU and sum(1 for v in f.verts if vdom(v).startswith(ARMB) and vdom(v).endswith("." + s)) >= 2]
        bm.normal_update()
        raise_region(bm, [f for f in armf if 1.33 <= f.calc_center_median().z <= 1.40 and -0.035 <= f.calc_center_median().y <= 0.035
                          and f.calc_center_median().dot(d) < s_end - 0.2 and sgn(f.calc_center_median().x) * f.normal.x > 0.35], 0.0035, RD)
limit_influences(bm)
bm.to_mesh(suit.data)
suit.data.update()
bm.free()
if SUIT == "EVA":
    bm = bmesh.new()
    bm.from_mesh(shoes.data)
    bm.normal_update()
    for v in bm.verts:
        v.co += v.normal * 0.012                                                 # chunky EVA boots
    ig = slot(shoes, GREYHW)
    fcs = {s_: arm.data.bones["foot." + s_].head_local for s_ in "LR"}
    for f in bm.faces:
        c = f.calc_center_median()
        fc = fcs["L" if c.x > 0 else "R"]
        if c.z < 0.028 or (c.z < 0.07 and c.y < fc.y - 0.15):                   # grey sole and toe cap
            f.material_index = ig
    bm.to_mesh(shoes.data)
    bm.free()

# ------------------------------------------------------------------ bigger, natural hands (scaled about the wrist joint)
bn0 = gname(body)
for v in body.data.vertices:
    if not v.groups:
        continue
    gnm = bn0[max(v.groups, key=lambda g: g.weight).group]
    if gnm.startswith(("wrist", "metacarpal", "finger")) and gnm.endswith((".L", ".R")):
        W0 = arm.data.bones["wrist." + gnm[-1]].head_local
        v.co = W0 + (v.co - W0) * 1.08
body.data.update()

# ------------------------------------------------------------------ EVA gloves from the body hands (+ gauntlet cuffs)
gauntlets = []
if SUIT == "EVA":
    bm = bmesh.new()
    bm.from_mesh(body.data)
    dlb = bm.verts.layers.deform.verify()
    bn = gname(body)
    ig, ip = slot(body, GLOVE), slot(body, GRIP)

    def bdom(v):
        d_ = v[dlb]
        return bn[max(d_.keys(), key=lambda k: d_[k])] if len(d_) else ""

    hand = [f for f in bm.faces if all(bdom(v).startswith(("wrist", "metacarpal", "finger")) for v in f.verts)]
    hv = {}
    for f in hand:
        for v in f.verts:
            hv.setdefault(v, Vector())
            hv[v] += f.normal * f.calc_area()
    for v, n in hv.items():
        v.co += n.normalized() * (0.009 if bdom(v).startswith("finger") else 0.015)
    for f in hand:
        dom = bdom(max(f.verts, key=lambda v: 0))
        f.material_index = ip if any(bdom(v).startswith("finger") and not bdom(v).startswith("finger1-1") for v in f.verts) else ig
    bm.normal_update()
    bm.to_mesh(body.data)
    bm.free()
    # gauntlet: tapered cuff from under the wrist ring over the wrist
    for s in "LR":
        wr = arm.data.bones["wrist." + s]
        d = (wr.head_local - arm.data.bones["lowerarm02." + s].head_local).normalized()
        W0 = wr.head_local.copy()
        sv = [suit.data.vertices[i].co for i in range(len(suit.data.vertices))]
        names_s = gname(suit)
        arm_pts = [v.co.copy() for v in suit.data.vertices if v.groups and names_s[max(v.groups, key=lambda g: g.weight).group] in ("lowerarm02." + s, "wrist." + s)]
        if not arm_pts:
            continue
        s_end = max(p.dot(d) for p in arm_pts)
        ring = [p for p in arm_pts if abs(p.dot(d) - (s_end - 0.03)) < 0.012]
        rs = sorted(((p - W0) - d * (p - W0).dot(d)).length for p in ring)
        r0 = min(0.056, rs[int(len(rs) * 0.85)] + 0.004) if rs else 0.050
        gob = bpy.data.objects.new("gauntlet_" + s, bpy.data.meshes.new("gauntlet_" + s))
        bpy.context.scene.collection.objects.link(gob)
        gob.parent = arm
        gob.modifiers.new("Armature", "ARMATURE").object = arm
        for m in (GRIP, GLOVE):
            gob.data.materials.append(m)
        gbm = bmesh.new()
        _, uu, ww_ = _frame(d)
        t0, t1 = s_end - 0.020, s_end + 0.060
        rings_ = []
        for t, r, mi in ((t0, r0 * 0.97, 0), (t0 + 0.010, r0 * 1.02, 0), (t0 + 0.012, r0 * 1.03, 1), (t1 - 0.012, r0 * 0.86, 1), (t1, r0 * 0.80, 1)):
            cpt = W0 + d * (t - W0.dot(d))
            rings_.append(([gbm.verts.new(cpt + (uu * math.cos(2 * math.pi * i / 32) + ww_ * math.sin(2 * math.pi * i / 32)) * r) for i in range(32)], mi))
        for k in range(len(rings_) - 1):
            (ra, _), (rb, mb_) = rings_[k], rings_[k + 1]
            for i in range(32):
                j = (i + 1) % 32
                f = gbm.faces.new((ra[i], ra[j], rb[j], rb[i]))
                f.material_index = mb_
                f.smooth = True
        bmesh.ops.recalc_face_normals(gbm, faces=list(gbm.faces))
        gbm.to_mesh(gob.data)
        gbm.free()
        g1, g2 = gob.vertex_groups.new(name="lowerarm02." + s), gob.vertex_groups.new(name="wrist." + s)
        for v in gob.data.vertices:
            w2 = _smooth01((v.co.dot(d) - t0) / (t1 - t0))
            g1.add([v.index], max(1.0 - w2, 1e-4), "REPLACE")
            g2.add([v.index], max(w2, 1e-4), "REPLACE")
        gauntlets.append(gob)
        log("gauntlet", s, "r0", round(r0, 4))

# ------------------------------------------------------------------ assemble renderers (FitV02 layout + Head)
for o in (hair, brows, lashes, eyes):
    tag_part(o, 1)
tag_part(body, 2)
for o in [suit, shoes] + gauntlets:
    tag_part(o, 0)


def head_test(c):
    return c.z > 1.495 and (abs(c.x) < 0.085 or c.z > 1.56)


# torso surface measurements for the rigid EVA parts (before joining)
SV = [v.co.copy() for v in suit.data.vertices]
CHEST_Y = min(p.y for p in SV if abs(p.x) < 0.05 and abs(p.z - 1.28) < 0.03)
BACK_Y = max(p.y for p in SV if abs(p.x) < 0.10 and 1.10 < p.z < 1.45)
TLB = BVHTree.FromPolygons([v.co for v in suit.data.vertices], [p.vertices for p in suit.data.polygons])
log("chest y", round(CHEST_Y, 4), "back y", round(BACK_Y, 4))

allobj = join_all([suit, shoes, body, hair, brows, lashes, eyes] + gauntlets, "ALL")
keys = classify(allobj, head_test)
parts = split_by(allobj, keys, "A7_SuitV03_%s_" % SUIT)
log("parts", {k: len(o.data.polygons) for k, o in parts.items()})

# ------------------------------------------------------------------ EVA rigid parts (skinned 100 % to one bone)
rigid = []
if SUIT == "EVA":
    from suitlib import make_part, finalize_part, Mat

    class Slots:
        """minimal Mat-like wrapper over smat materials for finalize_part()"""

        def __init__(self, mats):
            self.order = mats

        def idx(self, m):
            return self.order.index(m)

        def apply(self, ob):
            ob.data.materials.clear()
            for m in self.order:
                ob.data.materials.append(m)

    MS = Slots([PANELHW, DISPLAYM, GREYHW, STRAP, METAL, RED, DARK, YELLOW, LIGHT, VISOR, CLEAR, CLOTH_A])
    I = MS.idx
    # chest control panel
    ob = make_part("A7_SuitV03_EVA_ChestPanel", arm, "spine02", MS, None)
    bm = bmesh.new()
    pb = CHEST_Y - 0.004
    pf = pb - 0.050
    cy = (pb + pf) / 2
    ZP = 1.245                                    # smaller and lower than V03c: less of the first-person view
    pf = pb - 0.036
    cy = (pb + pf) / 2
    box(bm, 0, pb - 0.005, ZP, 0.192, 0.010, 0.136, I(GREYHW), 0.004)
    box(bm, 0, cy, ZP, 0.180, 0.036, 0.125, I(PANELHW), 0.008, 3)
    box(bm, -0.016, pf - 0.002, ZP + 0.014, 0.112, 0.006, 0.060, I(DARK), 0.003)
    uv_quad(bm, [(-0.064, pf - 0.0055, ZP - 0.0085), (0.032, pf - 0.0055, ZP - 0.0085), (0.032, pf - 0.0055, ZP + 0.0365), (-0.064, pf - 0.0055, ZP + 0.0365)], I(DISPLAYM))
    for i in range(2):
        for j in range(3):
            box(bm, 0.060 + 0.017 * i, pf - 0.004, ZP + 0.034 - 0.018 * j, 0.012, 0.007, 0.011, I(STRAP) if (i + j) % 3 else I(RED), 0.002)
    for x in (-0.062, -0.033, -0.004):
        cylinder(bm, (x, pf - 0.007, ZP - 0.040), (0, 1, 0), 0.012, 0.014, I(DARK), 18)
        cylinder(bm, (x, pf - 0.0145, ZP - 0.040), (0, 1, 0), 0.006, 0.004, I(METAL), 12)
    torus(bm, (0.048, pf - 0.006, ZP - 0.040), (0, 1, 0), 0.0135, 0.0036, I(YELLOW), 24, 8)
    box(bm, 0.048, pf - 0.006, ZP - 0.040, 0.009, 0.009, 0.009, I(METAL), 0.002)
    for x in (-0.080, 0.080):
        for z in (ZP - 0.052, ZP + 0.052):
            cylinder(bm, (x, pf - 0.002, z), (0, 1, 0), 0.0035, 0.004, I(METAL), 8)
    box(bm, 0, pb - 0.016, 1.365, 0.128, 0.032, 0.058, I(PANELHW), 0.007, 3)
    box(bm, 0, pb - 0.033, 1.386, 0.132, 0.005, 0.020, I(PANELHW), 0.003)
    box(bm, 0, pb - 0.034, 1.365, 0.134, 0.005, 0.010, I(STRAP), 0.002)
    for sx in (1, -1):
        box(bm, sx * 0.0775, CHEST_Y - 0.020, 1.455, 0.034, 0.012, 0.030, I(METAL), 0.003)
        box(bm, sx * 0.0775, CHEST_Y - 0.018, 1.405, 0.030, 0.010, 0.020, I(STRAP), 0.003)
        box(bm, sx * 0.096, pf + 0.010, ZP - 0.062, 0.018, 0.014, 0.020, I(METAL), 0.003)
    finalize_part(ob, bm, MS, "spine02")
    bm.free()
    rigid.append(ob)
    # chest hoses (joined into TorsoLegs, blended spine02 -> spine04)
    hose_objs = []
    for sx in (1, -1):
        A_ = (lambda a: a) if sx > 0 else (lambda a: 180.0 - a)
        pts = [Vector((sx * 0.096, pf + 0.012, ZP - 0.064)), Vector((sx * 0.104, pf + 0.020, ZP - 0.090))]
        for z, ang, off in ((1.150, -68.0, 0.026), (1.105, -42.0, 0.030), (1.060, -16.0, 0.030), (1.035, 2.0, 0.026)):
            pts.append(surface_point(TLB, z, A_(ang), off))
        hob = bpy.data.objects.new("hose_%d" % sx, bpy.data.meshes.new("hose_%d" % sx))
        bpy.context.scene.collection.objects.link(hob)
        hbm = bmesh.new()
        tube(hbm, pts, 0.0125, 0, 14)
        for f in hbm.faces:
            f.smooth = True
        hob.data.materials.append(STRAP)
        hbm.to_mesh(hob.data)
        hbm.free()
        ga, gb = hob.vertex_groups.new(name="spine02"), hob.vertex_groups.new(name="spine04")
        for v in hob.data.vertices:
            w = _smooth01((v.co.z - 1.03) / 0.14)
            ga.add([v.index], max(w, 1e-4), "REPLACE")
            gb.add([v.index], max(1.0 - w, 1e-4), "REPLACE")
        hose_objs.append(hob)
    join_into(parts["TorsoLegs"], hose_objs)
    # PLSS
    ob = make_part("A7_SuitV03_EVA_Plss", arm, "spine02", MS, None)
    bm = bmesh.new()
    yb0 = BACK_Y + 0.006
    yb = yb0 + 0.190
    yc = (yb0 + yb) / 2
    box(bm, 0, yc, 1.300, 0.350, 0.190, 0.400, I(PANELHW), 0.026, 3)
    box(bm, 0, yb + 0.002, 1.315, 0.290, 0.008, 0.300, I(PANELHW), 0.004)
    box(bm, 0, yb + 0.008, 1.250, 0.200, 0.006, 0.022, I(RED), 0.002)
    box(bm, 0, yb + 0.032, 1.432, 0.130, 0.014, 0.018, I(METAL), 0.005)
    for x in (-0.058, 0.058):
        box(bm, x, yb + 0.016, 1.432, 0.014, 0.030, 0.014, I(METAL), 0.003)
    for x in (-0.178, 0.178):
        for z in (1.40, 1.25, 1.12):
            box(bm, x, yc + 0.005, z, 0.012, 0.050, 0.042, I(STRAP), 0.003)
            box(bm, x * 1.035, yc + 0.005, z, 0.006, 0.026, 0.020, I(METAL), 0.002)
    box(bm, 0, yb0 + 0.080, 1.045, 0.330, 0.150, 0.110, I(PANELHW), 0.016, 3)
    for x in (0.075, 0.120):
        cylinder(bm, (x, yb0 + 0.159, 1.045), (0, 1, 0), 0.018, 0.012, I(DARK), 18)
        cylinder(bm, (x, yb0 + 0.166, 1.045), (0, 1, 0), 0.009, 0.004, I(METAL), 12)
    box(bm, -0.070, yb0 + 0.157, 1.045, 0.075, 0.006, 0.052, I(METAL), 0.003)
    ycan = yb0 + 0.085
    cylinder(bm, (0, ycan, 0.930), (1, 0, 0), 0.058, 0.300, I(PANELHW), 28)
    for x in (-0.092, 0.092):
        cylinder(bm, (x, ycan, 0.930), (1, 0, 0), 0.0615, 0.022, I(STRAP), 28)
    box(bm, 0, ycan + 0.059, 0.930, 0.150, 0.004, 0.012, I(RED), 0.001)
    tube(bm, [(0.150, yb0 + 0.080, 1.120), (0.192, yb0 + 0.095, 1.075), (0.200, yb0 + 0.100, 1.005), (0.185, ycan + 0.02, 0.955),
              (0.152, ycan + 0.01, 0.935)], 0.011, I(METAL), 12)
    box(bm, 0, yc, 1.505, 0.300, 0.150, 0.012, I(PANELHW), 0.006)
    for x in (-0.105, 0.105):
        box(bm, x, yb + 0.004, 1.315, 0.010, 0.008, 0.330, I(PANELHW), 0.002)
    for z in (1.20, 1.36):
        for x in (-0.03, 0.0, 0.03):
            box(bm, x - 0.09, yb + 0.006, z, 0.016, 0.004, 0.040, I(DARK), 0.002)
    box(bm, 0, yb + 0.010, 1.380, 0.060, 0.008, 0.040, I(METAL), 0.003)
    box(bm, 0.140, yb0 + 0.120, 1.507, 0.030, 0.030, 0.016, I(METAL), 0.004)
    cylinder(bm, (0.140, yb0 + 0.120, 1.605), (0, 0, 1), 0.004, 0.190, I(METAL), 8)
    finalize_part(ob, bm, MS, "spine02")
    bm.free()
    rigid.append(ob)
    # helmet around the head
    HV = [v.co for v in parts["Head"].data.vertices if v.co.z > 1.58]
    lo = Vector([min(p[i] for p in HV) for i in range(3)])
    hi = Vector([max(p[i] for p in HV) for i in range(3)])
    hc = Vector((0.0, (lo.y + hi.y) / 2 - 0.018, (lo.z + hi.z) / 2 + 0.012))
    rx = max(0.152, (hi.x - lo.x) / 2 + 0.055)
    ry = max(0.176, (hi.y - lo.y) / 2 + 0.055)
    rz = max(0.170, (hi.z - lo.z) / 2 + 0.045)
    log("head bbox", [round(x, 3) for x in lo], [round(x, 3) for x in hi], "helmet", [round(x, 3) for x in hc], round(rx, 3), round(ry, 3), round(rz, 3))
    ZCUT = hc.z - 0.80 * rz
    k = math.sqrt(1 - 0.80 ** 2)
    rrx, rry = rx * k + 0.010, ry * k + 0.010
    ob = make_part("A7_SuitV03_EVA_Helmet", arm, "head", MS, None)
    bm = bmesh.new()
    shell_from_ellipsoid(bm, hc, rx * 1.035, ry * 1.035, rz * 1.035,
                         lambda r: r.z > ZCUT - hc.z + 0.004 and (r.y > -0.045 or r.z > 0.72 * rz), I(PANELHW), 0.010,
                         cuts=(((0, hc.y - 0.045, 0), (0, 1, 0)), ((0, 0, hc.z + 0.72 * rz), (0, 0, 1)), ((0, 0, ZCUT + 0.004), (0, 0, 1))))
    ring = cylinder(bm, (0, hc.y, ZCUT - 0.012), (0, 0, 1), 1.0, 0.050, I(METAL), 36)
    for v in {v for f in ring for v in f.verts}:
        v.co.x *= rrx
        v.co.y = hc.y + (v.co.y - hc.y) * rry
    rr = cylinder(bm, (0, hc.y, ZCUT + 0.018), (0, 0, 1), 1.0, 0.010, I(RED), 36)
    for v in {v for f in rr for v in f.verts}:
        v.co.x *= (rrx + 0.004)
        v.co.y = hc.y + (v.co.y - hc.y) * (rry + 0.004)
    bmesh.ops.delete(bm, geom=[f for f in rr if len(f.verts) > 4], context="FACES")
    for sx in (1, -1):
        hx = sx * (rx * 1.035 + 0.030)
        box(bm, sx * (rx * 1.035 + 0.008), hc.y - 0.050, hc.z + 0.035, 0.026, 0.030, 0.030, I(METAL), 0.003)
        box(bm, hx, hc.y - 0.075, hc.z + 0.035, 0.040, 0.050, 0.075, I(STRAP), 0.006, 2)
        for dz in (0.017, -0.017):
            cylinder(bm, (hx, hc.y - 0.1015, hc.z + 0.035 + dz), (0, 1, 0), 0.0155, 0.004, I(DARK), 18)
            cylinder(bm, (hx, hc.y - 0.1040, hc.z + 0.035 + dz), (0, 1, 0), 0.0115, 0.004, I(LIGHT), 18)
        cylinder(bm, (sx * (rx * 1.07 + 0.006), hc.y - 0.010, hc.z + 0.005), (1, 0, 0), 0.022, 0.012, I(METAL), 18)
    box(bm, 0.060, hc.y + 0.040, hc.z + rz * 1.035 - 0.004, 0.050, 0.070, 0.028, I(PANELHW), 0.006, 2)
    dam_o = [bm.verts.new((math.cos(2 * math.pi * i / 36) * rrx, hc.y + math.sin(2 * math.pi * i / 36) * rry, ZCUT - 0.030)) for i in range(36)]
    dam_i = [bm.verts.new((math.cos(2 * math.pi * i / 36) * 0.072, -0.012 + math.sin(2 * math.pi * i / 36) * 0.082, ZCUT - 0.030)) for i in range(36)]
    for i in range(36):
        j = (i + 1) % 36
        f = bm.faces.new((dam_i[i], dam_i[j], dam_o[j], dam_o[i]))
        f.material_index = I(METAL)
    finalize_part(ob, bm, MS, "head")
    bm.free()
    rigid.append(ob)
    ob = make_part("A7_SuitV03_EVA_Visor", arm, "head", MS, None)
    bm = bmesh.new()
    shell_from_ellipsoid(bm, hc, rx, ry, rz, lambda r: r.z > ZCUT - hc.z and r.y < 0.03, I(CLEAR), 0.0,
                         cuts=(((0, 0, ZCUT), (0, 0, 1)), ((0, hc.y + 0.03, 0), (0, 1, 0))))
    VX = 0.86 * rx * 1.07
    shell_from_ellipsoid(bm, hc, rx * 1.07, ry * 1.07, rz * 1.07,
                         lambda r: r.y < -0.025 and r.z > -0.010 and abs(r.x) < VX, I(VISOR), 0.004,
                         cuts=(((VX, 0, 0), (1, 0, 0)), ((-VX, 0, 0), (1, 0, 0)), ((0, 0, hc.z - 0.010), (0, 0, 1)), ((0, hc.y - 0.025, 0), (0, 1, 0))))
    finalize_part(ob, bm, MS, "head")
    bm.free()
    rigid.append(ob)
    for o in rigid:                                      # drop unused material slots
        used = sorted({p.material_index for p in o.data.polygons})
        keep = [o.data.materials[i] for i in used]
        remap = {old: new for new, old in enumerate(used)}
        idx = [remap[p.material_index] for p in o.data.polygons]
        o.data.materials.clear()
        for m in keep:
            o.data.materials.append(m)
        for p, i in zip(o.data.polygons, idx):
            p.material_index = i

# ------------------------------------------------------------------ report + export
objs = [arm] + [parts[k] for k in sorted(parts)] + rigid
assert bone_hash(arm) == HASH0
bpy.context.view_layer.update()
rep = {"hash": HASH0, "bones": len(arm.data.bones), "suit": SUIT, "meshes": {}}
tri = 0
for o in objs[1:]:
    o.data.calc_loop_triangles()
    xs = [o.matrix_world @ v.co for v in o.data.vertices]
    rep["meshes"][o.name] = {"verts": len(o.data.vertices), "tris": len(o.data.loop_triangles),
                             "materials": [m.name for m in o.data.materials], "weights": vg_weight_report(o, arm),
                             "bbox": [[round(min(p[i] for p in xs), 3) for i in range(3)], [round(max(p[i] for p in xs), 3) for i in range(3)]]}
    tri += len(o.data.loop_triangles)
rep["tris_total"] = tri
json.dump(rep, open(os.path.join(OUT, "build_report.json"), "w"), indent=1)
used = {m.name for o in objs[1:] for m in o.data.materials if m}
json.dump({k: v for k, v in SPEC.items() if k in used}, open(os.path.join(OUT, "materials.json"), "w"), indent=1)
log("tris", tri, "materials", sorted(used))
# no machine paths in the FBX: unlink every image node before export, relink after
links = []
for m in bpy.data.materials:
    if m.use_nodes:
        for l in list(m.node_tree.links):
            if l.from_node.type == "TEX_IMAGE":
                links.append((m, l.from_socket, l.to_socket))
                m.node_tree.links.remove(l)
if EXPORT:
    export_fbx(os.path.join(OUT, "A7_SuitV03_%s.fbx" % SUIT), objs)
    log("exported")
for m, a_, b_ in links:
    m.node_tree.links.new(a_, b_)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "%s.blend" % SUIT))
