"""CHAR-REF-01 shared helpers: Blender 5.3 headless. Re-dress FitV02 bodies on the unchanged 164-bone rig."""
import bpy, bmesh, sys, os, json, math, hashlib, time
from mathutils import Vector, Matrix
from mathutils.kdtree import KDTree

T0 = time.time()


def log(*a):
    print("[v03 %6.1fs]" % (time.time() - T0), *a, flush=True)


def srgb2lin(c):
    c = c / 255.0
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def hex2lin(h):
    h = h.lstrip("#")
    return tuple(srgb2lin(int(h[i:i + 2], 16)) for i in (0, 2, 4)) + (1.0,)


def load_fbx(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    scn = bpy.context.scene
    scn.render.fps = 30
    arm = next(o for o in scn.objects if o.type == "ARMATURE")
    arm.data.pose_position = "REST"
    if arm.animation_data:
        arm.animation_data_clear()
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)
    meshes = {o.name: o for o in scn.objects if o.type == "MESH"}
    return arm, meshes


def bone_hash(arm):
    return hashlib.sha256("\n".join(b.name for b in arm.data.bones).encode()).hexdigest()[:16]


def rig_signature(arm):
    return {b.name: (b.parent.name if b.parent else None,
                     [round(x, 6) for x in b.head_local], [round(x, 6) for x in b.tail_local], round(b.length, 6),
                     round(b.roll if hasattr(b, "roll") else 0.0, 6)) for b in arm.data.bones}


def new_material(name, hexcol, rough=0.8, metal=0.0, emission=None, emission_strength=0.0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = hex2lin(hexcol)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    if emission:
        b.inputs["Emission Color"].default_value = hex2lin(emission)
        b.inputs["Emission Strength"].default_value = emission_strength
    m.diffuse_color = hex2lin(hexcol)
    return m


class Mat:
    """Ordered material table -> slot index."""

    def __init__(self, specs):
        self.order = []
        self.mats = {}
        self.specs = {}
        for name, spec in specs:
            self.order.append(name)
            self.specs[name] = spec
            self.mats[name] = new_material(name, **spec)

    def idx(self, name):
        return self.order.index(name)

    def apply(self, obj):
        obj.data.materials.clear()
        for n in self.order:
            obj.data.materials.append(self.mats[n])


# ----------------------------------------------------------------------------- bmesh ops
def fcenter(f):
    return f.calc_center_median()


def vnormals_from_faces(faces):
    d = {}
    for f in faces:
        for v in f.verts:
            d.setdefault(v, Vector())
            d[v] += f.normal * f.calc_area()
    for v in d:
        if d[v].length > 1e-9:
            d[v].normalize()
    return d


def smooth_set(bm, verts, iters=10, factor=0.5):
    verts = list(verts)
    for _ in range(iters):
        newpos = {}
        for v in verts:
            nb = [e.other_vert(v).co for e in v.link_edges]
            if nb:
                avg = sum(nb, Vector()) / len(nb)
                newpos[v] = v.co.lerp(avg, factor)
        for v, p in newpos.items():
            v.co = p


def raise_region(bm, faces, dist, mat_idx=None, dist_fn=None, side_mat=None):
    """Extrude `faces` (list of BMFace) outward by dist (m), cap and walls get mat_idx. Returns cap faces."""
    faces = [f for f in faces if f.is_valid]
    if not faces:
        return []
    ret = bmesh.ops.extrude_face_region(bm, geom=faces)
    geom = ret["geom"]
    newv = [g for g in geom if isinstance(g, bmesh.types.BMVert)]
    newf = [g for g in geom if isinstance(g, bmesh.types.BMFace)]
    capset = set()
    # cap faces: faces all of whose verts are in newv and which are not walls (walls have 4 verts, two old two new)
    nvset = set(newv)
    caps = [f for f in newf if all(v in nvset for v in f.verts)]
    walls = [f for f in newf if f not in set(caps)]
    for f in caps:
        f.normal_update()
    nrm = {}
    for f in caps:
        for v in f.verts:
            nrm.setdefault(v, Vector())
            nrm[v] += f.normal * f.calc_area()
    for v, n in nrm.items():
        if n.length > 1e-9:
            n.normalize()
        d = dist_fn(v.co, n) if dist_fn else dist
        v.co = v.co + n * d
    if mat_idx is not None:
        for f in caps:
            f.material_index = mat_idx
        for f in walls:
            f.material_index = mat_idx if side_mat is None else side_mat
    for f in caps + walls:
        f.smooth = True
    return caps


def sel(bm, pred):
    return [f for f in bm.faces if pred(f)]


def set_edge_sharpness(bm, angle_deg=40.0):
    lim = math.radians(angle_deg)
    for e in bm.edges:
        if len(e.link_faces) == 2:
            try:
                a = e.calc_face_angle()
            except Exception:
                a = 0
            e.smooth = a < lim
        else:
            e.smooth = True


def finish_mesh(obj, bm, mats, angle=40.0):
    limit_influences(bm)
    for f in bm.faces:
        f.smooth = True
    set_edge_sharpness(bm, angle)
    bm.normal_update()
    mats.apply(obj)
    bm.to_mesh(obj.data)
    obj.data.update()
    # clear custom split normals so FBX writes plain smooth normals from sharp edges
    try:
        bpy.context.view_layer.objects.active = obj
        bpy.ops.mesh.customdata_custom_splitnormals_clear()
    except Exception as ex:
        log("split normal clear failed", ex)


def vg_weight_report(obj, arm):
    me = obj.data
    names = {g.index: g.name for g in obj.vertex_groups}
    bad, maxinf = 0, 0
    for v in me.vertices:
        ws = [g.weight for g in v.groups if g.weight > 0.0005]
        maxinf = max(maxinf, len(ws))
        if not ws or abs(sum(ws) - 1.0) > 0.02:
            bad += 1
    return {"verts": len(me.vertices), "bad_weight_sum": bad, "max_influences": maxinf, "groups": len(obj.vertex_groups)}


# -------------------------------------------------------------- new rigid / cloth parts
def make_part(name, arm, bone, mats, mat_name):
    """empty skinned mesh object weighted 100% to `bone`."""
    me = bpy.data.meshes.new(name)
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    ob.parent = arm
    mod = ob.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    ob.vertex_groups.new(name=bone)
    return ob


def finalize_part(ob, bm, mats, bone, angle=35.0):
    for f in bm.faces:
        f.smooth = True
    set_edge_sharpness(bm, angle)
    bm.normal_update()
    mats.apply(ob)
    bm.to_mesh(ob.data)
    ob.data.update()
    vg = ob.vertex_groups[bone]
    vg.add([v.index for v in ob.data.vertices], 1.0, "REPLACE")


def box(bm, cx, cy, cz, sx, sy, sz, mat, bevel=0.0, seg=2):
    r = bmesh.ops.create_cube(bm, size=1.0)
    verts = r["verts"]
    for v in verts:
        v.co = Vector((cx + v.co.x * sx, cy + v.co.y * sy, cz + v.co.z * sz))
    faces = list({f for v in verts for f in v.link_faces})
    for f in faces:
        f.material_index = mat
    if bevel > 0:
        edges = list({e for v in verts for e in v.link_edges})
        res = bmesh.ops.bevel(bm, geom=edges, offset=bevel, segments=seg, affect="EDGES", material=-1)
        for f in res["faces"]:
            f.material_index = mat
    return faces


def cylinder(bm, c, axis, radius, length, mat, seg=24, r2=None):
    """cylinder/cone along unit axis through centre c."""
    a = Vector(axis).normalized()
    up = Vector((0, 0, 1)) if abs(a.z) < 0.9 else Vector((1, 0, 0))
    u = a.cross(up).normalized()
    w = a.cross(u).normalized()
    r2 = radius if r2 is None else r2
    rings = []
    for k, (t, r) in enumerate(((-0.5, radius), (0.5, r2))):
        ring = []
        for i in range(seg):
            ang = 2 * math.pi * i / seg
            ring.append(bm.verts.new(Vector(c) + a * (t * length) + (u * math.cos(ang) + w * math.sin(ang)) * r))
        rings.append(ring)
    fs = []
    for i in range(seg):
        j = (i + 1) % seg
        fs.append(bm.faces.new((rings[0][i], rings[0][j], rings[1][j], rings[1][i])))
    for ring in rings:
        pass
    fs.append(bm.faces.new(list(reversed(rings[0]))))
    fs.append(bm.faces.new(rings[1]))
    for f in fs:
        f.material_index = mat
    bmesh.ops.recalc_face_normals(bm, faces=fs)
    return fs


def ellipsoid(bm, c, rx, ry, rz, mat, usegs=32, vsegs=18, z_min=-1.0, z_max=1.0):
    r = bmesh.ops.create_uvsphere(bm, u_segments=usegs, v_segments=vsegs, radius=1.0)
    verts = r["verts"]
    for v in verts:
        v.co = Vector((c[0] + v.co.x * rx, c[1] + v.co.y * ry, c[2] + v.co.z * rz))
    faces = list({f for v in verts for f in v.link_faces})
    for f in faces:
        f.material_index = mat
    return faces


def export_fbx(path, objs):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"ARMATURE", "MESH"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", global_scale=1.0,
        axis_forward="-Z", axis_up="Y", bake_space_transform=False, add_leaf_bones=False,
        use_armature_deform_only=False, bake_anim=False, mesh_smooth_type="FACE", use_mesh_modifiers=True,
        use_tspace=False, path_mode="STRIP", embed_textures=False, use_custom_props=False, primary_bone_axis="Y",
        secondary_bone_axis="X", armature_nodetype="NULL")


def cut(bm, co, no):
    """bisect all geometry with a plane (weights/UVs interpolate, material kept)."""
    geom = list(bm.verts) + list(bm.edges) + list(bm.faces)
    bmesh.ops.bisect_plane(bm, geom=geom, plane_co=Vector(co), plane_no=Vector(no), dist=1e-6)
    bm.faces.ensure_lookup_table()


def cutz(bm, z):
    cut(bm, (0, 0, z), (0, 0, 1))


def cuty(bm, y):
    cut(bm, (0, y, 0), (0, 1, 0))


def cutx(bm, x, sym=True):
    cut(bm, (x, 0, 0), (1, 0, 0))
    if sym and x != 0:
        cut(bm, (-x, 0, 0), (1, 0, 0))


def limit_influences(bm, maxinf=4):
    dl = bm.verts.layers.deform.verify()
    for v in bm.verts:
        d = v[dl]
        if len(d) > maxinf:
            keep = sorted(d.items(), key=lambda kv: -kv[1])[:maxinf]
            for k in list(d.keys()):
                del d[k]
            tot = sum(w for _, w in keep) or 1.0
            for k, w in keep:
                d[k] = w / tot
        else:
            tot = sum(d.values())
            if tot > 0 and abs(tot - 1.0) > 1e-6:
                for k in list(d.keys()):
                    d[k] = d[k] / tot


def _frame(axis):
    a = Vector(axis).normalized()
    up = Vector((0, 0, 1)) if abs(a.z) < 0.9 else Vector((1, 0, 0))
    u = a.cross(up).normalized()
    return a, u, a.cross(u).normalized()


def torus(bm, c, axis, R, r, mat, seg=28, rseg=8):
    a, u, w = _frame(axis)
    c = Vector(c)
    rings = []
    for i in range(seg):
        t = 2 * math.pi * i / seg
        cd = u * math.cos(t) + w * math.sin(t)
        ctr = c + cd * R
        rings.append([bm.verts.new(ctr + (cd * math.cos(2 * math.pi * k / rseg) + a * math.sin(2 * math.pi * k / rseg)) * r) for k in range(rseg)])
    fs = []
    for i in range(seg):
        j = (i + 1) % seg
        for k in range(rseg):
            l = (k + 1) % rseg
            fs.append(bm.faces.new((rings[i][k], rings[j][k], rings[j][l], rings[i][l])))
    for f in fs:
        f.material_index = mat
    bmesh.ops.recalc_face_normals(bm, faces=fs)
    return fs


def tube(bm, pts, r, mat, seg=10):
    pts = [Vector(p) for p in pts]
    rings = []
    prev_u = None
    for i, p in enumerate(pts):
        t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        if prev_u is None:
            _, prev_u, _ = _frame(t)
        u = (prev_u - t * prev_u.dot(t)).normalized()
        w = t.cross(u)
        prev_u = u
        rings.append([bm.verts.new(p + (u * math.cos(2 * math.pi * k / seg) + w * math.sin(2 * math.pi * k / seg)) * r) for k in range(seg)])
    fs = []
    for i in range(len(rings) - 1):
        for k in range(seg):
            l = (k + 1) % seg
            fs.append(bm.faces.new((rings[i][k], rings[i + 1][k], rings[i + 1][l], rings[i][l])))
    fs.append(bm.faces.new(list(reversed(rings[0]))))
    fs.append(bm.faces.new(rings[-1]))
    for f in fs:
        f.material_index = mat
    bmesh.ops.recalc_face_normals(bm, faces=fs)
    return fs


def uv_quad(bm, corners, mat):
    """corners in order bottom-left, bottom-right, top-right, top-left (seen from the front)."""
    uvl = bm.loops.layers.uv.verify()
    f = bm.faces.new([bm.verts.new(Vector(c)) for c in corners])
    for lp, uv in zip(f.loops, ((0, 0), (1, 0), (1, 1), (0, 1))):
        lp[uvl].uv = uv
    f.material_index = mat
    return f


def shell_from_ellipsoid(bm, c, rx, ry, rz, keep, mat, thickness=0.0, usegs=40, vsegs=24, cuts=()):
    """ellipsoid part kept by keep(rel_centre); cuts = planes (co, no) applied first so kept edges are straight."""
    pre = set(bm.faces)
    faces = ellipsoid(bm, c, rx, ry, rz, mat, usegs, vsegs)
    bmesh.ops.recalc_face_normals(bm, faces=faces)
    for co, no in cuts:
        cut(bm, co, no)
    ell = [f for f in bm.faces if f not in pre and f.material_index == mat]
    drop = [f for f in ell if not keep(f.calc_center_median() - Vector(c))]
    bmesh.ops.delete(bm, geom=drop, context="FACES")
    kept = [f for f in bm.faces if f.is_valid and f not in pre and f.material_index == mat]
    if thickness > 0:
        bmesh.ops.solidify(bm, geom=kept, thickness=thickness)
    return kept


def texture_material(mat, image_path, emission_strength=0.0):
    nt = mat.node_tree
    img = bpy.data.images.load(image_path)
    tn = nt.nodes.new("ShaderNodeTexImage")
    tn.image = img
    b = nt.nodes["Principled BSDF"]
    nt.links.new(tn.outputs["Color"], b.inputs["Base Color"])
    if emission_strength > 0:
        nt.links.new(tn.outputs["Color"], b.inputs["Emission Color"])
        b.inputs["Emission Strength"].default_value = emission_strength
    return img


def alpha_material(mat, alpha):
    b = mat.node_tree.nodes["Principled BSDF"]
    b.inputs["Alpha"].default_value = alpha
    try:
        mat.surface_render_method = "BLENDED"
    except Exception:
        pass
    try:
        mat.blend_method = "BLEND"
    except Exception:
        pass
    mat.diffuse_color = (mat.diffuse_color[0], mat.diffuse_color[1], mat.diffuse_color[2], alpha)


# ------------------------------------------------------------------ male shape (same skeleton)
def _smooth01(t):
    t = min(1.0, max(0.0, t))
    return t * t * (3 - 2 * t)


def bump(z, a, b, c, d):
    """0 below a, ramps to 1 on [b, c], back to 0 at d."""
    if z <= a or z >= d:
        return 0.0
    if z < b:
        return _smooth01((z - a) / (b - a))
    if z > c:
        return _smooth01((d - z) / (d - c))
    return 1.0


def male_tweak(co):
    """Reviewer-requested silhouette: narrower hip flare (crotch untouched), no waist pinch, broader shoulders,
    flat belly and crotch. Pure function of rest position, so shared torso/arm seam vertices stay coincident."""
    x, y, z = co.x, co.y, co.z
    ax = abs(x)
    s = 1.0 if x >= 0 else -1.0
    dx = dy = 0.0
    # hips/thighs: pull the outer flank in (about x = +-0.06), strongest at 0.84-0.94
    hb = bump(z, 0.66, 0.82, 0.95, 1.06)
    if ax > 0.06:
        dx -= s * 0.17 * (ax - 0.06) * hb
    # waist: fill the pinch at the sides
    wb = bump(z, 0.98, 1.05, 1.14, 1.24)
    if ax > 0.09:
        dx += s * 0.010 * wb * min(1.0, (ax - 0.09) / 0.05)
    # chest / shoulders: broader
    cb = bump(z, 1.22, 1.32, 1.46, 1.54)
    if ax > 0.10:
        dx += s * 0.008 * cb * min(1.0, (ax - 0.10) / 0.08)
    # belly flat (front), strongest just above the belt; chest slightly forward so it reads broader than the belly
    if y < -0.05:
        dy += 0.026 * bump(z, 0.90, 0.98, 1.18, 1.29) * min(1.0, (-0.05 - y) / 0.05)
        if ax < 0.15:
            dy -= 0.006 * bump(z, 1.27, 1.32, 1.40, 1.45) * min(1.0, (-0.05 - y) / 0.05)
    # crotch flatter (front centre)
    if ax < 0.08 and y < -0.04:
        dy += 0.012 * bump(z, 0.76, 0.82, 0.90, 0.95) * (1.0 - ax / 0.08) * min(1.0, (-0.04 - y) / 0.04)
    return Vector((dx, dy, 0.0))


class MaleField:
    """Displacement field from the skeleton-compensated MakeHuman male shape, sampled on the base body surface."""

    def __init__(self, mh_dir, delta_npy):
        import numpy as np
        S, TZ = 0.1065109505563943, 0.875583749
        V = [tuple(float(t) for t in l.split()[1:4]) for l in open(os.path.join(mh_dir, "base.obj")) if l.startswith("v ")]
        body, g = set(), None
        for l in open(os.path.join(mh_dir, "base.obj")):
            if l.startswith("g "):
                g = l.split()[1]
            elif l.startswith("f ") and g == "body":
                for tok in l.split()[1:]:
                    body.add(int(tok.split("/")[0]) - 1)
        self.delta = np.load(delta_npy)
        self.idx = sorted(body)
        self.kd = KDTree(len(self.idx))
        for k, i in enumerate(self.idx):
            x, y, zz = V[i]
            self.kd.insert(Vector((S * x, -S * zz, S * y + TZ)), k)
        self.kd.balance()

    def at(self, co, n=6):
        acc, ws = Vector(), 0.0
        for p, k, dist in self.kd.find_n(co, n):
            w = 1.0 / (dist * dist + 1e-6)
            acc += Vector(self.delta[self.idx[k]]) * w
            ws += w
        return acc / ws if ws else Vector()

    def vertex_delta(self, i):
        return Vector(self.delta[i])


def apply_male(obj, field, tweak=True):
    me = obj.data
    for v in me.vertices:
        d = field.at(v.co)
        if tweak:
            d = d + male_tweak(v.co)
        v.co = v.co + d
    me.update()


# ------------------------------------------------------------------ V03b helpers
def polar_profile(points, center, bins=48, mode="min"):
    """radius per angle bin around center (x, y) for a list of Vectors."""
    import math as _m
    rb = [[] for _ in range(bins)]
    for p in points:
        a = _m.atan2(p.y - center[1], p.x - center[0])
        k = int((a + _m.pi) / (2 * _m.pi) * bins) % bins
        rb[k].append(((p.x - center[0]) ** 2 + (p.y - center[1]) ** 2) ** 0.5)
    out = []
    for k in range(bins):
        if rb[k]:
            out.append(min(rb[k]) if mode == "min" else sum(rb[k]) / len(rb[k]))
        else:
            out.append(None)
    # fill gaps from neighbours
    for k in range(bins):
        if out[k] is None:
            for d in range(1, bins):
                c = out[(k - d) % bins] or out[(k + d) % bins]
                if c:
                    out[k] = c
                    break
    return out


def ring_between(name, arm, M, mat, outer, inner, center, z_out, z_in, weights, bins=48):
    """skinned annulus/cone between two polar profiles (used to close neck/collar gaps)."""
    import math as _m
    me = bpy.data.meshes.new(name)
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    ob.parent = arm
    ob.modifiers.new("Armature", "ARMATURE").object = arm
    bm = bmesh.new()
    vo, vi = [], []
    for k in range(bins):
        a = -_m.pi + (k + 0.5) * 2 * _m.pi / bins
        ca, sa = _m.cos(a), _m.sin(a)
        vo.append(bm.verts.new((center[0] + ca * outer[k], center[1] + sa * outer[k], z_out)))
        vi.append(bm.verts.new((center[0] + ca * inner[k], center[1] + sa * inner[k], z_in)))
    for k in range(bins):
        j = (k + 1) % bins
        f = bm.faces.new((vo[k], vo[j], vi[j], vi[k]))
        f.material_index = mat
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    for f in bm.faces:
        if f.normal.z < 0:
            f.normal_flip()
        f.smooth = True
    M.apply(ob)
    bm.to_mesh(me)
    bm.free()
    for bn, w in weights.items():
        ob.vertex_groups.new(name=bn).add([v.index for v in me.vertices], w, "REPLACE")
    return ob


def join_into(target, others):
    bpy.ops.object.select_all(action="DESELECT")
    for o in others:
        o.select_set(True)
    target.select_set(True)
    bpy.context.view_layer.objects.active = target
    bpy.ops.object.join()


def surface_point(bvh, z, ang_deg, off, center=(0.0, 0.0)):
    """point on a body surface at height z in direction ang (0 = +x, 90 = +y), pushed out by off."""
    import math as _m
    d = Vector((_m.cos(_m.radians(ang_deg)), _m.sin(_m.radians(ang_deg)), 0.0))
    o = Vector((center[0], center[1], z))
    hit = None
    loc, nrm, idx, dist = bvh.ray_cast(o + d * 0.6, -d, 0.7)        # from outside inwards: outermost surface
    if loc is not None:
        hit = loc + d * off
    return hit if hit is not None else o + d * (0.2 + off)


def reshape_shoe(bm, shoe_mats, foot_centers):
    """flat ground contact, a defined outsole rim and a slight toe spring on the existing boot geometry."""
    for v in bm.verts:
        if not any(f.material_index in shoe_mats for f in v.link_faces):
            continue
        fc = foot_centers[0] if v.co.x > 0 else foot_centers[1]
        if v.co.z < 0.014:
            v.co.z = 0.004 if v.co.z < 0.010 else v.co.z
        if v.co.z < 0.034:                    # outsole/midsole wall: push out 4 mm horizontally
            dvec = Vector((v.co.x - fc.x, v.co.y - fc.y, 0.0))
            if dvec.length > 1e-6:
                v.co += dvec.normalized() * 0.004
        if v.co.y < fc.y - 0.11 and v.co.z < 0.03:     # toe spring
            v.co.z += min(0.012, (fc.y - 0.11 - v.co.y) * 0.25)
