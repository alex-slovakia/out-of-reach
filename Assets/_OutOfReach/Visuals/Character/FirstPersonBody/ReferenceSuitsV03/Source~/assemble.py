"""Assembly helpers for the CC0-proxy characters: tag faces by part, join, split into the FitV02 renderer layout
(Head / TorsoLegs / Arm_L / Arm_R) with identical seam normals, export."""
import bpy, bmesh
from mathutils import Vector

ARM_BONES = ("upperarm01", "upperarm02", "lowerarm01", "lowerarm02", "wrist", "metacarpal", "finger")


def tag_part(ob, part):
    """store a per-face integer attribute 'v03_part' (0 auto, 1 head-only asset)."""
    me = ob.data
    at = me.attributes.get("v03_part") or me.attributes.new("v03_part", "INT", "FACE")
    for i in range(len(me.polygons)):
        at.data[i].value = part


def apply_tweak(ob, fn):
    me = ob.data
    for v in me.vertices:
        v.co = v.co + fn(v.co)
    me.update()


def join_all(objs, name):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    ob.name = name
    ob.data.name = name
    return ob


def classify(ob, head_test):
    """returns per-face renderer key: 'Head', 'Arm_L', 'Arm_R' or 'TorsoLegs'."""
    me = ob.data
    names = {g.index: g.name for g in ob.vertex_groups}
    part = me.attributes.get("v03_part")
    out = []
    for p in me.polygons:
        if part is not None and part.data[p.index].value == 1:
            out.append("Head")
            continue
        pts = [me.vertices[i].co for i in p.vertices]
        if part is not None and part.data[p.index].value == 2 and all(head_test(c) for c in pts):
            out.append("Head")
            continue
        acc = {}
        for i in p.vertices:
            for g in me.vertices[i].groups:
                n = names[g.group]
                acc[n] = acc.get(n, 0.0) + g.weight
        dom = max(acc, key=acc.get) if acc else ""
        if dom.startswith(ARM_BONES) and dom.endswith((".L", ".R")):
            out.append("Arm_L" if dom.endswith(".L") else "Arm_R")
        else:
            out.append("TorsoLegs")
    return out


def split_by(ob, keys, prefix):
    """split a joined mesh into one object per key; vertex positions, weights, UVs and smooth normals of the seam are
    identical on both sides (custom normals taken from the joined mesh)."""
    me = ob.data
    me.calc_loop_triangles()
    # vertex normals of the joined surface (area weighted, ignoring sharp edges) -> custom normals after the split
    vn = [Vector() for _ in me.vertices]
    for p in me.polygons:
        for i in p.vertices:
            vn[i] += p.normal * p.area
    vn = [n.normalized() if n.length > 1e-12 else Vector((0, 0, 1)) for n in vn]
    me["_vn"] = [c for n in vn for c in n]
    out = {}
    for key in sorted(set(keys)):
        bpy.ops.object.select_all(action="DESELECT")
        dup = ob.copy()
        dup.data = ob.data.copy()
        bpy.context.scene.collection.objects.link(dup)
        bm = bmesh.new()
        bm.from_mesh(dup.data)
        bm.faces.ensure_lookup_table()
        bm.verts.ensure_lookup_table()
        orig_index = bm.verts.layers.int.new("orig")
        for v in bm.verts:
            v[orig_index] = v.index
        kill = [bm.faces[i] for i, k in enumerate(keys) if k != key]
        bmesh.ops.delete(bm, geom=kill, context="FACES")
        loose = [v for v in bm.verts if not v.link_faces]
        bmesh.ops.delete(bm, geom=loose, context="VERTS")
        orig = [v[orig_index] for v in bm.verts]
        bm.verts.layers.int.remove(orig_index)
        bm.to_mesh(dup.data)
        bm.free()
        m2 = dup.data
        loops_n = []
        for p in m2.polygons:
            for li in p.loop_indices:
                loops_n.append(vn[orig[m2.loops[li].vertex_index]])
        try:
            m2.normals_split_custom_set(loops_n)
        except Exception as ex:
            print("custom normals failed", key, ex)
        dup.name = prefix + key
        dup.data.name = prefix + key
        # drop vertex groups this part does not use (keeps the FBX clusters lean)
        used = set()
        for v in m2.vertices:
            for g in v.groups:
                if g.weight > 0:
                    used.add(g.group)
        drop = [g.name for g in dup.vertex_groups if g.index not in used]
        for n in drop:
            dup.vertex_groups.remove(dup.vertex_groups[n])
        out[key] = dup
    bpy.data.objects.remove(ob, do_unlink=True)
    return out
