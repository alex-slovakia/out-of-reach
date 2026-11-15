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
