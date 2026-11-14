"""
OUT OF REACH - antenna repair motion (ANTENNA-REPAIR-MOTION-01). Opens the delivered kit AntennaRepair.blend
(ANTENNA-REPAIR-ART-01, 1c57692), splits ConnectorDamaged into nut, plug body, cable and scorch (same geometry, UVs and
AR_Kit maps), keys a 3.0 s repair motion at 30 fps (inspect, engage, seat 12 mm, lock 24 deg, return), measures it
frame by frame, renders real frames and exports an FBX with the baked clip plus sampled transforms in Unity metres.

    blender -b --factory-startup --python build_antenna_repair_motion.py -- --kit <AntennaRepair.blend> --out <folder> [--stage check|full]
"""
import json
import os
import subprocess
import sys
import time
from math import cos, radians, sin, pi, degrees, acos

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree

T0 = time.time()


def log(*parts):
    print("[arm %6.1fs]" % (time.time() - T0), *parts, flush=True)


ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def arg(name, default=None):
    if name in ARGS:
        i = ARGS.index(name)
        return ARGS[i + 1] if i + 1 < len(ARGS) and not ARGS[i + 1].startswith("--") else True
    return default


KIT = os.path.abspath(arg("--kit"))
OUT = os.path.abspath(arg("--out"))
STAGE = arg("--stage", "full")
SAMPLES = int(arg("--samples", 64))
RENDER_DIR = os.path.join(OUT, "renders")
FRAME_DIR = os.path.join(OUT, "_frames")
for folder in (OUT, RENDER_DIR, FRAME_DIR):
    os.makedirs(folder, exist_ok=True)
NAME = "AntennaRepairMotion"
FPS, END = 30, 90
BS = chr(92)

# ------------------------------------------------------------------ kit facts (Unity metres, from 1c57692)
NUT_BACKED = Vector((0.0, 0.0, -0.057))      # PlugGripBackedOut
SEAT = 0.012                                 # damaged plug backed out this far
TURN = 24.0                                  # and turned this far (counter-clockwise as seen by the technician)
JAW_IN, NUT_R = 0.030, 0.0285                # C-jaw inner radius, coupling-nut ridge radius
GRIP_OFFSET = 0.0013                         # jaw takes up the clearance: heel 0.2 mm from the ridges
JAW_ENTRY = Vector((0.0, 0.0, -0.152))       # jaw hooks onto the stub jacket here (backed-out state)
JAW_EXIT = Vector((0.0, 0.0, -0.140))        # and unhooks here (seated state)
READY = Vector((0.110, -0.030, -0.200))      # wrench in hand beside the cable, jaw axis already on Z
HOOK_CLEAR = 0.062                           # jaw centre this far from the cable before hooking
C3 = Matrix(((-1.0, 0.0, 0.0), (0.0, 0.0, 1.0), (0.0, -1.0, 0.0)))   # Unity = C3 @ Blender


def u2b(p):
    return Vector((-p[0], -p[2], p[1]))


def b2u(v):
    return Vector((-v[0], v[2], -v[1]))


def quat_u(m3):
    """Unity quaternion (x, y, z, w) of a Blender rotation matrix (handedness change included)."""
    q = (C3 @ m3 @ C3.transposed()).to_quaternion()
    return q


def rnd(v, n=5):
    return [round(float(x), n) for x in v]


def ease(t):
    t = min(1.0, max(0.0, t))
    return t * t * (3.0 - 2.0 * t)


def select_only(objs, active=None):
    bpy.context.view_layer.update()
    for o in list(bpy.context.view_layer.objects):
        if o is not None:
            o.select_set(False)
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = active or objs[0]


def bake_modifiers(obj):
    """WeightedNormal of the kit, then triangulate keeping its custom normals (shape keys need a modifier-free mesh)."""
    tri = obj.modifiers.new("Triangulate", "TRIANGULATE")
    tri.keep_custom_normals = True
    tri.quad_method = "FIXED"
    tri.ngon_method = "BEAUTY"
    select_only([obj])
    for modifier in list(obj.modifiers):
        bpy.ops.object.modifier_apply(modifier=modifier.name)


def world_verts(obj):
    return [b2u(obj.matrix_world @ v.co) for v in obj.data.vertices]


def split_damaged(src):
    """Loose islands of ConnectorDamaged -> nut, plug body, cable, scorch (classified by position in Unity metres)."""
    select_only([src])
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.separate(type="LOOSE")
    bpy.ops.object.mode_set(mode="OBJECT")
    islands = [o for o in bpy.data.objects if o.name.startswith(src.name)]
    cable_body = max(islands, key=lambda o: len(o.data.vertices))
    ring_tree = KDTree(len(cable_body.data.vertices))
    for i, p in enumerate(world_verts(cable_body)):
        ring_tree.insert(p, i)
    ring_tree.balance()
    groups = {"nut": [], "body": [], "cable": [], "scorch": []}
    for o in islands:
        vs = world_verts(o)
        z = [v.z for v in vs]
        r = [(v.x * v.x + v.y * v.y) ** 0.5 for v in vs]
        if o is cable_body or all(ring_tree.find(v)[2] < 1e-6 for v in vs):
            groups["cable"].append(o)
        elif max(abs(zz + 0.0063) for zz in z) < 3e-4:
            groups["scorch"].append(o)
        elif min(z) >= -0.0705 and max(z) <= -0.0435 and max(r) >= 0.0270:
            groups["nut"].append(o)
        else:
            groups["body"].append(o)
    counts = {k: len(v) for k, v in groups.items()}
    log("islands", len(islands), counts)
    assert counts == {"nut": 2, "body": 42, "cable": 3, "scorch": 1}, counts
    names = {"nut": "DamagedNut", "body": "DamagedPlugBody", "cable": "DamagedCable", "scorch": "DamagedScorch"}
    result = {}
    for key, objs in groups.items():
        main = objs[0] if key != "cable" else cable_body
        select_only(objs, active=main)
        if len(objs) > 1:
            bpy.ops.object.join()
        obj = bpy.context.view_layer.objects.active
        obj.name = names[key]
        obj.data.name = NAME + "_" + names[key]
        result[key] = obj
    return result, len(islands)


def set_origin(obj, point_b):
    obj.data.transform(Matrix.Translation(-(point_b - obj.matrix_world.translation)))
    obj.location = point_b


# ------------------------------------------------------------------ cable: the plug end follows the seat, the grommet end stays
RING = 10                                     # the kit's cable is 61 rings of 10 vertices (curve bevel), plus two cap islands


def cable_rings(cable):
    """Rings of the cable tube found by topology (BFS from the ring under the plug-end cap), validated, not assumed."""
    me = cable.data
    verts = [cable.matrix_world @ v.co for v in me.vertices]
    adj = [[] for _ in verts]
    for e in me.edges:
        a, b = e.vertices
        adj[a].append(b)
        adj[b].append(a)
    comp = [-1] * len(verts)
    comps = []
    for start in range(len(verts)):
        if comp[start] >= 0:
            continue
        stack, members = [start], []
        comp[start] = len(comps)
        while stack:
            v = stack.pop()
            members.append(v)
            for n in adj[v]:
                if comp[n] < 0:
                    comp[n] = len(comps)
                    stack.append(n)
        comps.append(members)
    body = max(comps, key=len)
    caps = [c for c in comps if c is not body]
    assert len(caps) == 2 and all(len(c) == RING for c in caps), [len(c) for c in comps]
    nut = u2b(NUT_BACKED)
    start_cap = min(caps, key=lambda c: (sum((verts[i] for i in c), Vector()) / RING - nut).length)
    tree = KDTree(len(body))
    for k, i in enumerate(body):
        tree.insert(verts[i], k)
    tree.balance()
    ring0 = {body[tree.find(verts[i])[1]] for i in start_cap}
    assert len(ring0) == RING, len(ring0)
    layer = {i: 0 for i in ring0}
    frontier = list(ring0)
    while frontier:
        nxt = []
        for v in frontier:
            for n in adj[v]:
                if n not in layer:
                    layer[n] = layer[v] + 1
                    nxt.append(n)
        frontier = nxt
    count = max(layer.values()) + 1
    rings = [[] for _ in range(count)]
    for i, l in layer.items():
        rings[l].append(i)
    centres = []
    for ring in rings:
        assert len(ring) == RING, ("ring size", len(ring))
        c = sum((verts[i] for i in ring), Vector()) / RING
        d = [(verts[i] - c).length for i in ring]
        assert max(d) - min(d) < 2e-4, ("not a cable ring", max(d) - min(d))
        centres.append(c)
    ctree = KDTree(count)
    for k, c in enumerate(centres):
        ctree.insert(c, k)
    ctree.balance()
    ring_of = [layer[i] if i in layer else ctree.find(verts[i])[1] for i in range(len(verts))]
    lengths = [0.0]
    for a, b in zip(centres, centres[1:]):
        lengths.append(lengths[-1] + (b - a).length)
    return centres, ring_of, lengths


def seat_weight(s, s0=0.12):
    """1 on the straight run behind the plug, cosine taper to 0 at the grommet (s = arc length / total)."""
    if s <= s0:
        return 1.0
    return 0.5 * (1.0 + cos(pi * (s - s0) / (1.0 - s0)))


def add_seat_shape(cable, s0=0.12):
    """Seated: the plug end moves +12 mm, the grommet end stays, and the loop bulges outwards just enough to keep the
    cable's centreline length (an inextensible cable taking up the slack); the bulge is found by bisection."""
    centres, ring_of, lengths = cable_rings(cable)
    total = lengths[-1]
    n = len(centres)
    forward = (centres[0] - u2b(NUT_BACKED)).length < (centres[-1] - u2b(NUT_BACKED)).length
    s_of = [(l / total) if forward else (1.0 - l / total) for l in lengths]
    push = u2b((0.0, 0.0, SEAT))
    middle = sum(centres, Vector()) / n
    normals = []
    for i, c in enumerate(centres):
        t = (centres[min(i + 1, n - 1)] - centres[max(i - 1, 0)]).normalized()
        out = c - middle
        normals.append((out - t * out.dot(t)).normalized())

    def disp(i, bulge):
        si = s_of[i]
        bump = sin(pi * (si - s0) / (1.0 - s0)) if si > s0 else 0.0
        return push * seat_weight(si, s0) + normals[i] * (bulge * bump)

    def length(bulge):
        moved = [c + disp(i, bulge) for i, c in enumerate(centres)]
        return sum((q - p).length for p, q in zip(moved, moved[1:]))

    lo, hi = 0.0, 0.03
    assert length(lo) < total < length(hi), (length(lo), total, length(hi))
    for _ in range(60):
        mid = 0.5 * (lo + hi)
        lo, hi = (mid, hi) if length(mid) < total else (lo, mid)
    bulge = 0.5 * (lo + hi)
    cable.shape_key_add(name="Basis", from_mix=False)
    key = cable.shape_key_add(name="Seated", from_mix=False)
    key.value = 0.0                                   # Blender 5.3 creates new keys at 1.0
    for i in range(len(cable.data.vertices)):
        key.data[i].co = cable.data.vertices[i].co + disp(ring_of[i], bulge)
    seated = length(bulge)
    return key, {"rings": n, "centreline_length_m": round(total, 5), "seated_centreline_length_m": round(seated, 5),
                 "length_change_mm": round((seated - total) * 1000, 3), "loop_bulge_mm": round(bulge * 1000, 3),
                 "linear_pull_only_length_change_mm": round((length(0.0) - total) * 1000, 3),
                 "start_ring_push_mm": round(disp(0, bulge).length * 1000, 3), "grommet_ring_push_mm": round(disp(n - 1, bulge).length * 1000, 3),
                 "straight_run_fraction": s0}


# ------------------------------------------------------------------ timeline (frames at 30 fps)
PHASES = [("inspect", 0, 9, "damaged plug backed out 12 mm and turned 24 deg; wrench ready beside the cable, jaw axis already on the connector axis"),
          ("approach", 9, 18, "jaw moves level with the stub jacket behind the melted boot, its 34 mm opening facing the 17 mm cable"),
          ("hook", 18, 24, "jaw slides sideways onto the stub jacket at JawEntry"),
          ("slide", 24, 36, "jaw slides along the axis over boot and backshell onto the coupling nut (concentric, 1.5 mm radial clearance)"),
          ("grip", 36, 40, "jaw takes up the clearance; its rubber heel bears on the nut ridges (within 0.03 mm, measured on the meshes)"),
          ("seat", 40, 54, "wrench, nut and plug body move +12 mm along +Z; the cable follows through the Seated blend shape"),
          ("lock", 54, 69, "wrench and nut turn 24 deg clockwise as seen by the technician about the connector axis; marks line up"),
          ("release", 69, 72, "jaw centres again, clear of the nut"),
          ("back_off", 72, 81, "jaw slides back along the axis to JawExit on the seated stub jacket"),
          ("unhook", 81, 90, "jaw slides off the cable through its opening along the handle direction; tool clear")]


def phase_t(name, f):
    for n, a, b, _ in PHASES:
        if n == name:
            return ease((f - a) / float(b - a))
    raise KeyError(name)


def state(f):
    """Seat fraction, lock angle (deg, Unity +Z axis), grip offset and jaw centre (Unity) at frame f."""
    seat = phase_t("seat", f)
    lock = -TURN * phase_t("lock", f)
    axis_z = NUT_BACKED.z + SEAT * seat
    grip = GRIP_OFFSET * (phase_t("grip", f) - phase_t("release", f))
    a = radians(lock)
    handle = Vector((cos(a), sin(a), 0.0))                    # Unity, tip -> grip
    if f <= 18:
        jaw = READY.lerp(JAW_ENTRY + Vector((HOOK_CLEAR, 0.0, 0.0)), phase_t("approach", f))
    elif f <= 24:
        jaw = (JAW_ENTRY + Vector((HOOK_CLEAR, 0.0, 0.0))).lerp(JAW_ENTRY, phase_t("hook", f))
    elif f <= 36:
        jaw = JAW_ENTRY.lerp(Vector((0.0, 0.0, axis_z)), phase_t("slide", f))
    elif f <= 72:
        jaw = Vector((0.0, 0.0, axis_z)) - handle * grip
    elif f <= 81:
        jaw = Vector((0.0, 0.0, axis_z)).lerp(JAW_EXIT, phase_t("back_off", f))
    else:
        jaw = JAW_EXIT + handle * (HOOK_CLEAR * phase_t("unhook", f))
    return seat, lock, grip, jaw, handle


def wrench_matrix(f, tip_local):
    seat, lock, grip, jaw, handle = state(f)
    rot = Matrix.Rotation(radians(lock), 4, "Y") @ Matrix.Rotation(radians(-90.0), 4, "X")   # Blender +Y = Unity +Z
    return Matrix.Translation(u2b(jaw)) @ rot @ Matrix.Translation(-tip_local)


def nut_matrix(f):
    seat, lock, _, _, _ = state(f)
    return Matrix.Translation(u2b(NUT_BACKED + Vector((0.0, 0.0, SEAT * seat)))) @ Matrix.Rotation(radians(lock), 4, "Y")


def body_matrix(f):
    seat = state(f)[0]
    return Matrix.Translation(u2b(NUT_BACKED + Vector((0.0, 0.0, SEAT * seat))))


def tune_grip(wr, nut, tip_local, wrench_rest):
    """Scan the jaw offset in 0.01 mm steps until the faceted jaw and nut first intersect; use 0.02 mm less (contact tolerance)."""
    global GRIP_OFFSET

    def probe(offset):
        global GRIP_OFFSET
        GRIP_OFFSET = offset
        wr.matrix_world = wrench_matrix(40, tip_local)
        nut.matrix_world = nut_matrix(40)
        bpy.context.view_layer.update()
        dg = bpy.context.evaluated_depsgraph_get()
        wb, wv = world_mesh(wr, dg)
        nb, nv = world_mesh(nut, dg)
        return bool(wb.overlap(nb)), nearest_gap(wb, wv, nb, nv)

    free, hit = None, None
    for k in range(0, 101):
        offset = 0.0012 + 0.00001 * k
        if probe(offset)[0]:
            hit = offset
            break
        free = offset
    assert free is not None and hit is not None, (free, hit)
    used = free - 0.00002
    overlap, gap = probe(used)
    assert not overlap
    GRIP_OFFSET = used
    wr.matrix_world = wrench_rest
    nut.matrix_world = Matrix.Translation(u2b(NUT_BACKED))
    bpy.context.view_layer.update()
    return used, gap, hit


def key_motion(objs, key, tip_local):
    wr, nut, body = objs["wrench"], objs["nut"], objs["body"]
    for obj in (wr, nut, body):
        obj.rotation_mode = "QUATERNION"
    prev = {}
    for f in range(0, END + 1):
        for obj, m in ((wr, wrench_matrix(f, tip_local)), (nut, nut_matrix(f)), (body, body_matrix(f))):
            loc, q, _ = m.decompose()
            if obj.name in prev and prev[obj.name].dot(q) < 0.0:
                q.negate()
            prev[obj.name] = q
            obj.location, obj.rotation_quaternion = loc, q
            obj.keyframe_insert("location", frame=f)
            obj.keyframe_insert("rotation_quaternion", frame=f)
        key.value = state(f)[0]
        key.keyframe_insert("value", frame=f)
    for action in [o.animation_data.action for o in (wr, nut, body)] + [key.id_data.animation_data.action]:
        for fc in getattr(action, "fcurves", []):
            for kp in fc.keyframe_points:
                kp.interpolation = "LINEAR"


# ------------------------------------------------------------------ measurements and samples
def world_mesh(obj, dg):
    ev = obj.evaluated_get(dg)
    me = ev.to_mesh()
    mw = obj.matrix_world.copy()
    verts = [mw @ v.co for v in me.vertices]
    polys = [tuple(p.vertices) for p in me.polygons]
    ev.to_mesh_clear()
    return BVHTree.FromPolygons(verts, polys), verts


def nearest_gap(bvh_a, verts_a, bvh_b, verts_b):
    d1 = min(bvh_b.find_nearest(v)[3] for v in verts_a)
    d2 = min(bvh_a.find_nearest(v)[3] for v in verts_b)
    return min(d1, d2)


def max_nearest(points, ref_points):
    tree = KDTree(len(ref_points))
    for i, p in enumerate(ref_points):
        tree.insert(p, i)
    tree.balance()
    return max(tree.find(p)[2] for p in points)


def measure_and_sample(objs, receptacle, tip_local, wrench_rest):
    scene = bpy.context.scene
    others = {"ConnectorReceptacle": receptacle, "DamagedNut": objs["nut"], "DamagedPlugBody": objs["body"],
              "DamagedCable": objs["cable"], "DamagedScorch": objs["scorch"]}
    rest = {k: Matrix.Translation(u2b(NUT_BACKED)) for k in ("nut", "body")}
    rest["wrench"] = wrench_rest
    frames, samples = [], {"ServiceWrench": [], "DamagedNut": [], "DamagedPlugBody": [], "DamagedCable": []}
    for f in range(0, END + 1):
        scene.frame_set(f)
        dg = bpy.context.evaluated_depsgraph_get()
        wb, wv = world_mesh(objs["wrench"], dg)
        row = {"f": f, "overlap_tris": {}, "clearance_mm": {}}
        for name, obj in others.items():
            ob, ov = world_mesh(obj, dg)
            row["overlap_tris"][name] = len(wb.overlap(ob))
            row["clearance_mm"][name] = round(nearest_gap(wb, wv, ob, ov) * 1000, 3)
        rb, rv = world_mesh(receptacle, dg)
        row["parts_vs_receptacle_overlap_tris"] = {k: len(world_mesh(objs[k], dg)[0].overlap(rb)) for k in ("nut", "body", "cable")}
        cb = world_mesh(objs["cable"], dg)[0]
        row["cable_vs_plug_overlap_tris"] = {k: len(world_mesh(objs[k], dg)[0].overlap(cb)) for k in ("nut", "body")}
        wm = objs["wrench"].matrix_world
        rot = wm.to_3x3() @ rest["wrench"].to_3x3().inverted()
        tip = b2u(wm @ tip_local)
        jaw_axis = b2u(wm.to_3x3() @ Vector((0.0, 0.0, 1.0)))
        handle = b2u(wm.to_3x3() @ Vector((-1.0, 0.0, 0.0)))
        nm = objs["nut"].matrix_world
        nut_rot = nm.to_3x3() @ rest["nut"].to_3x3().inverted()
        lock = degrees(quat_u(nut_rot).angle) * (1.0 if quat_u(nut_rot).axis.z >= 0 else -1.0)
        nut_c = b2u(nm.translation)
        row.update({"jaw_centre": rnd(tip), "jaw_off_axis_mm": round((tip.x ** 2 + tip.y ** 2) ** 0.5 * 1000, 3),
                    "nut_centre": rnd(nut_c), "nut_lock_deg": round(lock, 3), "cable_weight": round(state(f)[0], 5)})
        frames.append(row)
        t = round(f / FPS, 5)
        q = quat_u(rot)
        samples["ServiceWrench"].append({"f": f, "t": t, "position": rnd(b2u(wm.translation)), "delta_rotation_xyzw": rnd((q.x, q.y, q.z, q.w), 6),
                                          "WrenchTip": rnd(tip), "jaw_axis": rnd(jaw_axis, 6), "handle_axis": rnd(handle, 6)})
        q = quat_u(nut_rot)
        samples["DamagedNut"].append({"f": f, "t": t, "position": rnd(nut_c), "delta_rotation_xyzw": rnd((q.x, q.y, q.z, q.w), 6), "lock_deg": round(lock, 4)})
        samples["DamagedPlugBody"].append({"f": f, "t": t, "position": rnd(b2u(objs["body"].matrix_world.translation)), "delta_rotation_xyzw": [0.0, 0.0, 0.0, 1.0]})
        samples["DamagedCable"].append({"f": f, "t": t, "blend_shape": "Seated", "weight_0_1": round(state(f)[0], 6)})
    return frames, samples


def summary(frames):
    def worst(key, name, pick=max):
        return pick(r[key][name] for r in frames)
    names = list(frames[0]["overlap_tris"])
    seat_overlap = [r["f"] for r in frames if r["parts_vs_receptacle_overlap_tris"]["nut"] > 0]
    extra = {"nut_vs_receptacle_overlap_frames": [min(seat_overlap), max(seat_overlap)] if seat_overlap else [],
             "body_vs_receptacle_overlap_tris_total": sum(r["parts_vs_receptacle_overlap_tris"]["body"] for r in frames),
             "cable_vs_receptacle_overlap_tris_total": sum(r["parts_vs_receptacle_overlap_tris"]["cable"] for r in frames),
             "cable_vs_plug_overlap_tris_max": max(max(r["cable_vs_plug_overlap_tris"].values()) for r in frames),
             "cable_vs_receptacle_overlap_tris_at_frame_0": frames[0]["parts_vs_receptacle_overlap_tris"]["cable"],
             "cable_vs_plug_overlap_tris_at_frame_0": frames[0]["cable_vs_plug_overlap_tris"]["body"],
             "note": "all of these are joins inherited from the kit, hidden and unchanged by the motion: the cable end sits in the grommet hole and its start inside the torn jacket (both present at frame 0), and the kit models the coupling nut as a closed cylinder whose rear face sits inside the receptacle barrel once seated, exactly as in ConnectorRepaired. The wrench has no overlap with anything in any frame."}
    res = {"overlap_tris_total": {n: sum(r["overlap_tris"][n] for r in frames) for n in names},
           "min_clearance_mm": {n: worst("clearance_mm", n, min) for n in names}, "parts": extra}
    for name, a, b, _ in PHASES:
        rows = [r for r in frames if a <= r["f"] <= b]
        res.setdefault("phases", {})[name] = {"frames": [a, b], "min_jaw_to_nut_mm": min(r["clearance_mm"]["DamagedNut"] for r in rows),
                                              "min_jaw_to_cable_mm": min(r["clearance_mm"]["DamagedCable"] for r in rows),
                                              "min_jaw_to_plug_body_mm": min(r["clearance_mm"]["DamagedPlugBody"] for r in rows)}
    return res


# ------------------------------------------------------------------ render set (render-only; removed before saving)
def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def build_render_set(scene):
    rs = bpy.data.collections.new("ARM_RenderSet")
    scene.collection.children.link(rs)
    world = bpy.data.worlds.new("ARM_World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.030, 0.032, 0.036, 1.0)
    scene.world = world
    plate = bpy.data.materials.new("ARM_PreviewBulkhead")
    plate.use_nodes = True
    bsdf = next(n for n in plate.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (0.46, 0.45, 0.43, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.6
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0, matrix=Matrix.Translation(u2b((0.1, -0.2, 0.015))) @ Matrix.Diagonal((1.6, 0.03, 1.2, 1.0)))
    mesh = bpy.data.meshes.new("ARM_PreviewBulkheadMesh")
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(plate)
    rs.objects.link(bpy.data.objects.new("PreviewOnly bulkhead", mesh))
    for name, loc, power, size in (("ARM_Key", (0.55, 0.55, -0.85), 28.0, 0.6), ("ARM_Fill", (-0.65, 0.05, -0.55), 9.0, 0.9), ("ARM_Rim", (0.10, 0.70, -0.10), 6.0, 0.4)):
        data = bpy.data.lights.new(name, "AREA")
        data.energy, data.size = power, size
        light = bpy.data.objects.new(name, data)
        light.location = u2b(loc)
        look_at(light, u2b((0.12, -0.08, -0.08)))
        rs.objects.link(light)
    cams = {}
    for key, loc, target, lens in (("wide", (0.28, 0.16, -0.66), (0.14, -0.06, -0.12), 28.0), ("close", (0.16, 0.08, -0.27), (0.02, -0.02, -0.07), 40.0)):
        data = bpy.data.cameras.new("ARM_Cam_" + key)
        data.lens, data.clip_start = lens, 0.005
        cam = bpy.data.objects.new("ARM_Cam_" + key, data)
        cam.location = u2b(loc)
        look_at(cam, u2b(target))
        rs.objects.link(cam)
        cams[key] = cam
    return rs, cams


def render_still(scene, cam, f, path, size, samples):
    scene.frame_set(f)
    scene.camera = cam
    scene.cycles.samples = samples
    scene.cycles.use_denoising = True
    scene.render.resolution_x, scene.render.resolution_y = size
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGB"
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def enable_gpu(scene):
    scene.render.engine = "CYCLES"
    try:
        prefs = bpy.context.preferences.addons["cycles"].preferences
        for backend in ("OPTIX", "CUDA"):
            try:
                prefs.compute_device_type = backend
            except TypeError:
                continue
            prefs.get_devices()
            used = [d for d in prefs.devices if d.type == backend]
            if used:
                for d in prefs.devices:
                    d.use = d.type == backend
                scene.cycles.device = "GPU"
                return backend + ": " + used[0].name
    except Exception as error:                                   # noqa: BLE001
        log("GPU setup failed:", error)
    scene.cycles.device = "CPU"
    return "CPU"


def scrub_blend(path):
    data = bytearray(open(path, "rb").read())
    hits = 0
    home = os.path.expanduser("~")
    for variant in {os.path.dirname(path), os.path.dirname(path).replace(BS, "/"), os.path.dirname(KIT), os.path.dirname(KIT).replace(BS, "/"), home, home.replace(BS, "/")}:
        needle = variant.encode("utf-8")
        start = data.find(needle)
        while start >= 0:
            end = data.find(b"\x00", start)
            data[start:end] = b"\x00" * (end - start)
            hits += 1
            start = data.find(needle, start)
    open(path, "wb").write(bytes(data))
    return hits


# ------------------------------------------------------------------ main
KEY_FRAMES = [(0, "inspect"), (18, "approach-end"), (24, "hooked"), (36, "on-nut"), (40, "gripped"), (54, "seated"),
              (62, "locking"), (69, "locked"), (81, "backed-off"), (90, "clear")]


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)       # unsaved file: the FBX then records no path of this machine
    with bpy.data.libraries.load(KIT, link=False) as (src, dst):
        dst.objects = list(src.objects)
    scene = bpy.context.scene
    for o in dst.objects:
        scene.collection.objects.link(o)
    bpy.context.view_layer.update()                        # matrix_world of appended objects is stale until evaluated
    bpy.context.preferences.edit.keyframe_new_interpolation_type = "LINEAR"
    scene.render.fps, scene.frame_start, scene.frame_end = FPS, 0, END
    device = enable_gpu(scene)
    log("Blender", bpy.app.version_string, "| stage", STAGE, "| device", device)
    obj = bpy.data.objects
    rec, rep, dam, wr = obj["ConnectorReceptacle"], obj["ConnectorRepaired"], obj["ConnectorDamaged"], obj["ServiceWrench"]
    anchors = {o.name: o for o in obj if o.type == "EMPTY"}
    tip_local = wr.matrix_world.inverted() @ anchors["WrenchTip"].matrix_world.translation
    wrench_rest = wr.matrix_world.copy()
    ref = dam.copy()
    ref.data = dam.data.copy()
    ref.name = "RefDamaged"
    dam.users_collection[0].objects.link(ref)
    ref.hide_render = True
    for o in (dam, wr):
        bake_modifiers(o)
    parts, islands = split_damaged(dam)
    for key in ("nut", "body"):
        set_origin(parts[key], u2b(NUT_BACKED))
    key, cable_info = add_seat_shape(parts["cable"])
    dg = bpy.context.evaluated_depsgraph_get()
    f0_points = [p for k in ("nut", "body", "cable", "scorch") for p in world_mesh(parts[k], dg)[1]]
    ref_points = world_mesh(ref, dg)[1]
    identity_mm = round(max(max_nearest(f0_points, ref_points), max_nearest(ref_points, f0_points)) * 1000, 6)
    log("f0 parts vs ConnectorDamaged: max deviation mm", identity_mm)
    objs = dict(parts, wrench=wr)
    grip = tune_grip(wr, parts["nut"], tip_local, wrench_rest)
    log("grip offset mm", round(grip[0] * 1000, 3), "| first mesh contact at mm", round(grip[2] * 1000, 3), "| vertex-to-surface gap mm", round(grip[1] * 1000, 3))
    key_motion(objs, key, tip_local)
    frames, samples = measure_and_sample(objs, rec, tip_local, wrench_rest)
    res = summary(frames)
    scene.frame_set(69)
    dg = bpy.context.evaluated_depsgraph_get()
    rep_bvh, rep_points = world_mesh(rep, dg)
    nut69 = world_mesh(parts["nut"], dg)[1]
    shell69 = [p for p in world_mesh(parts["body"], dg)[1] if (p.x ** 2 + p.z ** 2) ** 0.5 >= 0.0165 and -0.1085 <= -p.y <= -0.0575]
    match = {"nut_and_mark_vertices_vs_ConnectorRepaired_vertices_max_mm": round(max_nearest(nut69, rep_points) * 1000, 6),
             "backshell_vertices_vs_ConnectorRepaired_surface_max_mm": round(max(rep_bvh.find_nearest(p)[3] for p in shell69) * 1000, 4),
             "backshell_vertices": len(shell69),
             "note": "the damaged backshell was tessellated 24 deg turned; the plug body does not turn (keyed), so it is compared surface to surface"}
    log("overlaps", res["overlap_tris_total"], "| min clearance mm", res["min_clearance_mm"], "| f69 match", match)
    bpy.data.objects.remove(ref, do_unlink=True)
    keep = ["ConnectorMount", "PlugGrip", "PlugGripBackedOut", "CableGrommet"]
    for name, p in (("JawEntry", JAW_ENTRY), ("JawExit", JAW_EXIT)):
        e = bpy.data.objects.new(name, None)
        e.empty_display_type, e.empty_display_size = "ARROWS", 0.02
        e.location = u2b(p)
        rec.users_collection[0].objects.link(e)
        anchors[name] = e
        keep.append(name)
    for name, e in list(anchors.items()):
        if name not in keep:
            bpy.data.objects.remove(e, do_unlink=True)
    motion = [wr, parts["nut"], parts["body"], parts["cable"], parts["scorch"]]
    rs, cams = build_render_set(scene)
    rep.hide_render = True
    stills = []
    full = STAGE == "full"
    shots = [(cams["wide"], f, "f%02d-%s.png" % (f, label)) for f, label in KEY_FRAMES] if full else []
    shots += [(cams["close"], f, ("close-f%02d-%s.png" if full else "check-close-f%02d-%s.png") % (f, label)) for f, label in ((40, "gripped"), (54, "seated"), (69, "locked"))]
    if not full:
        shots += [(cams["wide"], f, "check-f%02d-%s.png" % (f, label)) for f, label in ((0, "inspect"), (24, "hooked"), (90, "clear"))]
    size, samples_n = ((1600, 1000), SAMPLES) if full else ((1200, 750), 24)
    for cam, f, name in shots:
        render_still(scene, cam, f, os.path.join(RENDER_DIR, name), size, samples_n)
        stills.append(name)
    if full:
        for f in range(0, END + 1):
            render_still(scene, cams["wide"], f, os.path.join(FRAME_DIR, "frame_%03d.png" % f), (960, 600), 16)
    for o in motion:
        o.hide_render = True
    rep.hide_render = False
    name = "after-swap-f90.png" if full else "check-after-swap.png"
    render_still(scene, cams["wide"], END, os.path.join(RENDER_DIR, name), size, samples_n)
    stills.append(name)
    for o in motion:
        o.hide_render = False
    rep.hide_render = True
    log("rendered", len(stills), "stills")
    report = {"blender": bpy.app.version_string, "stage": STAGE, "cycles_device": device, "kit_islands": islands,
              "fps": FPS, "frames": END + 1, "duration_s": END / FPS,
              "phases": [{"name": n, "frames": [a, b], "seconds": [round(a / FPS, 3), round(b / FPS, 3)], "what": w} for n, a, b, w in PHASES],
              "f0_parts_vs_ConnectorDamaged_max_mm": identity_mm, "grip": {"jaw_offset_used_mm": round(grip[0] * 1000, 3), "first_jaw_nut_mesh_intersection_at_offset_mm": round(grip[2] * 1000, 3),
                       "vertex_to_surface_gap_at_used_offset_mm": round(grip[1] * 1000, 4),
                       "meaning": "the faceted jaw and nut first intersect 0.03 mm further in; the used offset leaves the rubber heel touching the ridges within that tolerance"}, "f69_locked_pose_match": match, "cable": cable_info,
              "summary": res, "per_frame": frames, "stills": stills,
              "triangles": {o.name: len(o.data.polygons) for o in motion}}
    finish(scene, motion, anchors, keep, rs, report, samples)


def finish(scene, motion, anchors, keep, rs, report, samples):
    scene.frame_set(0)
    for o in list(rs.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    bpy.data.collections.remove(rs)
    if STAGE == "full":
        cut = []
        for mat in {s for o in motion for s in o.data.materials if s is not None and s.node_tree}:
            for link in list(mat.node_tree.links):
                if link.from_node.type == "TEX_IMAGE":
                    cut.append((mat.node_tree, link.from_socket, link.to_socket))
                    mat.node_tree.links.remove(link)
        select_only(motion + [anchors[n] for n in keep], active=motion[0])
        bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, NAME + ".fbx"), use_selection=True, object_types={"MESH", "EMPTY"},
                                 apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
                                 use_mesh_modifiers=False, mesh_smooth_type="FACE", use_triangles=False, add_leaf_bones=False,
                                 bake_anim=True, bake_anim_use_all_bones=False, bake_anim_use_nla_strips=False,
                                 bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True, bake_anim_step=1.0,
                                 bake_anim_simplify_factor=0.0, path_mode="RELATIVE", embed_textures=False)
        for tree, source, target in cut:
            tree.links.new(source, target)
        for image in bpy.data.images:
            if image.filepath and "AntennaRepair_" in image.filepath:
                image.filepath = "//../antenna-repair-v01/textures/" + os.path.basename(image.filepath.replace(BS, "/"))
        blend_path = os.path.join(OUT, NAME + ".blend")
        bpy.context.preferences.filepaths.save_version = 0
        bpy.ops.wm.save_as_mainfile(filepath=blend_path, compress=False, relative_remap=False)
        report["blend_path_scrubbed"] = scrub_blend(blend_path)
        log("exported FBX and saved .blend")
    json.dump(report, open(os.path.join(OUT, "motion-report.json"), "w", encoding="utf-8", newline="\n"), indent=1)
    out = {"fps": FPS, "frames": END + 1, "duration_s": END / FPS,
           "units": "Unity metres, Y up; origin = ConnectorMount (wall face under the flange); the technician works from -Z",
           "conventions": "position = the object's pivot in kit space; delta_rotation_xyzw = Unity quaternion applied on top of the rotation the object has at import: rotation(f) = delta(f) * rotationAtImport. ServiceWrench rest = its pose in AntennaRepair.fbx of 1c57692; DamagedNut and DamagedPlugBody rest = their pose at frame 0.",
           "pivots": {"ServiceWrench": "WrenchGrip (palm target)", "DamagedNut": "coupling-nut centre on the connector axis",
                      "DamagedPlugBody": "same point as DamagedNut", "DamagedCable": "ConnectorMount (static object; blend shape Seated)"},
           "phases": report["phases"], "objects": samples,
           "swap": "Frames 0-90 show DamagedPlugBody + DamagedNut + DamagedCable + DamagedScorch in place of ConnectorDamaged (identical at frame 0). From frame 69 the nut and backshell are in ConnectorRepaired's pose. After frame 90, with the tool clear, swap the four parts for ConnectorRepaired; the swap stands for the cable re-termination, cleaning and safety wire, which this motion does not show. No sparks or welding."}
    json.dump(out, open(os.path.join(OUT, "motion-samples.json"), "w", encoding="utf-8", newline="\n"), indent=1)
    log("done")


main()
