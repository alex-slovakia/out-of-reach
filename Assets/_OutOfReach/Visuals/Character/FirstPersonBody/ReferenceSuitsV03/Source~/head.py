"""Head for CHAR-REF-01 from the MakeHuman CC0 base mesh (same pinned revision as FitV02).
FitV02's skeleton IS the unmodified MakeHuman default skeleton: Blender = S * (x, -z, y) + (0, 0, TZ), residual 0
over all 163 bones (out/mh_align.json). So the base-mesh head drops onto the rig without any deformation and takes
MakeHuman's own default weights (bone names identical)."""
import os, json, math
import bpy, bmesh
from mathutils import Vector
from suitlib import raise_region, cut, cutz, cutx, limit_influences, set_edge_sharpness, log

S = 0.1065109505563943
TZ = 0.875583749
NECK_CUT = 1.495


def _mh(x, y, z):
    return Vector((S * x, -S * z, S * y + TZ))


def load_mh(folder):
    V, VT, F = [], [], []
    g = None
    for line in open(os.path.join(folder, "base.obj")):
        if line.startswith("v "):
            V.append(_mh(*[float(t) for t in line.split()[1:4]]))
        elif line.startswith("vt "):
            VT.append(tuple(float(t) for t in line.split()[1:3]))
        elif line.startswith("g "):
            g = line.split()[1]
        elif line.startswith("f "):
            idx = []
            for tok in line.split()[1:]:
                p = tok.split("/")
                idx.append((int(p[0]) - 1, int(p[1]) - 1 if len(p) > 1 and p[1] else -1))
            F.append((g, idx))
    W = json.load(open(os.path.join(folder, "default_weights.mhw")))["weights"]
    per = {}
    for bone, lst in W.items():
        for vi, w in lst:
            per.setdefault(vi, {})[bone] = w
    return V, VT, F, per


def _head_vert(p):
    return p.z > NECK_CUT - 0.01 and (abs(p.x) < 0.085 or p.z > 1.56)


def build_head(name, arm, M, mh_folder, skin, hair, eyewhite, iris, stubble, male=None):
    """Creates a skinned head object. M = Mat table (suitlib.Mat); material args are slot indices in M."""
    V, VT, F, per = load_mh(mh_folder)
    if male is not None:
        V = [V[i] + male.at(V[i]) for i in range(len(V))]   # eyes/lashes follow the face (helpers have no own delta)
    me = bpy.data.meshes.new(name)
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    ob.parent = arm
    mod = ob.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    bones = set(b.name for b in arm.data.bones)
    bm = bmesh.new()
    dl = bm.verts.layers.deform.verify()
    uvl = bm.loops.layers.uv.new("UV0")
    vmap = {}
    gidx = {}

    def gi(bname):
        if bname not in gidx:
            gidx[bname] = ob.vertex_groups.new(name=bname).index
        return gidx[bname]

    def vert(i, fallback):
        if i in vmap:
            return vmap[i]
        v = bm.verts.new(V[i])
        ws = {b: w for b, w in per.get(i, {}).items() if b in bones and w > 0}
        if not ws:
            ws = {fallback: 1.0}
        for b, w in ws.items():
            v[dl][gi(b)] = w
        vmap[i] = v
        return v

    kinds = {}
    for g, idx in F:
        if g == "body":
            if not all(_head_vert(V[i]) for i, _ in idx):
                continue
            kind, fb = "skin", "head"
        elif g in ("helper-l-eye", "helper-r-eye"):
            kind, fb = "eye", ("eye.L" if g == "helper-l-eye" else "eye.R")
        elif g and g.startswith("helper-") and "eyelashes" in g:
            kind, fb = "lash", "head"
        else:
            continue
        try:
            f = bm.faces.new([vert(i, fb) for i, _ in idx])
        except ValueError:
            continue
        for lp, (_, ti) in zip(f.loops, idx):
            if ti >= 0:
                lp[uvl].uv = VT[ti]
        kinds[f] = kind
    bm.normal_update()
    # materials: skin, eyes (iris = faces looking forward out of the eyeball), lashes = hair colour
    eye_c = {}
    for sd, grp in (("L", "eye.L"), ("R", "eye.R")):
        pts = [v.co for f, k in kinds.items() if k == "eye" for v in f.verts if (v.co.x > 0) == (sd == "L")]
        eye_c[sd] = sum(pts, Vector()) / max(1, len(pts))
    for f, k in kinds.items():
        if k == "skin":
            f.material_index = skin
        elif k == "lash":
            f.material_index = hair
        else:
            c = f.calc_center_median()
            ec = eye_c["L"] if c.x > 0 else eye_c["R"]
            dirv = (c - ec).normalized()
            f.material_index = iris if dirv.dot(Vector((0, -1, 0))) > 0.90 else eyewhite
    # clean neck edge
    cutz(bm, NECK_CUT)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.calc_center_median().z < NECK_CUT and f.material_index == skin], context="FACES")
    # hairline = intersection of three planes (front/nape + two sides): every boundary is a straight cut
    planes = [((0, -0.10, 1.706), (0, 0.90, 1.0)), ((0.075, 0, 1.652), (-0.55, 0.60, 1.0)), ((-0.075, 0, 1.652), (0.55, 0.60, 1.0))]
    for co, no in planes:
        cut(bm, co, no)
    # arched brows (outer end lower), stubble line, lip box
    brow = []
    for sx in (1, -1):
        top = ((sx * 0.033, 0, 1.6790), (-sx * 0.10, 0, 1.0))
        bot = ((sx * 0.033, 0, 1.6726), (-sx * 0.10, 0, 1.0))
        brow.append((sx, top, bot))
        cut(bm, *top)
        cut(bm, *bot)
    for x in (0.016, 0.052, 0.024):
        cutx(bm, x)
    for z in (1.612, 1.592):
        cutz(bm, z)
    bm.normal_update()

    def above(c):
        return all((c - Vector(co)).dot(Vector(no)) > 0 for co, no in planes)

    hair_faces = [f for f in bm.faces if f.material_index == skin and above(f.calc_center_median())]
    hset = set(hair_faces)
    for f in hair_faces:
        f.material_index = hair
    for v in {v for f in hair_faces for v in f.verts}:
        if any(f not in hset for f in v.link_faces):
            continue                                   # hairline vertices stay on the skin: tapered, no ledge
        jit = 0.0015 * math.sin(v.co.x * 211.0 + v.co.y * 157.0) * math.cos(v.co.z * 173.0)     # tousled, not a cap
        top = min(1.0, max(0.0, (v.co.z - 1.690) / 0.080)) ** 1.2 * min(1.0, max(0.0, 1.0 - abs(v.co.x) / 0.085))
        v.co = v.co + v.normal * (0.0022 + 0.0170 * top + jit)          # short sides, volume on top and front
    bm.normal_update()
    brows = []
    for f in bm.faces:
        if f.material_index != skin:
            continue
        c = f.calc_center_median()
        for sx, top, bot in brow:
            if sx * c.x > 0 and 0.016 <= abs(c.x) <= 0.052 and c.y < -0.07                     and (c - Vector(top[0])).dot(Vector(top[1])) < 0 and (c - Vector(bot[0])).dot(Vector(bot[1])) > 0:
                brows.append(f)
    raise_region(bm, brows, 0.0012, hair)
    for f in bm.faces:
        if f.material_index != skin:
            continue
        c = f.calc_center_median()
        lip = abs(c.x) < 0.024 and 1.592 < c.z < 1.612 and c.y < -0.14
        if 1.540 < c.z < 1.612 and c.y < -0.035 and not lip:
            f.material_index = stubble
    limit_influences(bm)
    for f in bm.faces:
        f.smooth = True
    set_edge_sharpness(bm, 60.0)
    M.apply(ob)
    bm.to_mesh(me)
    me.update()
    bm.free()
    log(name, "verts", len(me.vertices), "faces", len(me.polygons), "groups", len(ob.vertex_groups),
        "hair faces", len(hair_faces), "brow faces", len(brows))
    return ob
