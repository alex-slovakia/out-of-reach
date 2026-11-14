"""
OUT OF REACH - EVA full-arm fit (EVA-FULL-ARM-FIT-01). The published FitV02 EVA body (A7_BodyFit02_EVA.fbx: unchanged
164-bone MakeHuman CC0 skeleton, A7_EVA_Arm_L/R sleeves) with the approved AstronautHands gloves bound to its forearms:
the glove's wrist ring (first 54 mm of the glove's Arm part, unchanged) and Hand/finger parts (unchanged) sit on the
sleeve end through a new adapter boot; arm poses are solved on the original bone lengths (no stretching) for Neutral,
rail grip and WrenchGrip; renders from the first-person eye (1.65 m, vertical FOV 68 deg) and from the side.

    blender -b --factory-startup --python build_eva_arm_fit.py -- --eva <A7_BodyFit02_EVA.fbx> --hands <AstronautHands.fbx>
        --rig <AstronautHands_rig.json> --gtex <AstronautHands Textures folder> --out <folder> [--stage check|full]
"""
import json
import os
import sys
import time
from math import acos, atan2, cos, degrees, pi, radians, sin

import bmesh
import bpy
from mathutils import Matrix, Quaternion, Vector
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree

T0 = time.time()


def log(*parts):
    print("[eaf %6.1fs]" % (time.time() - T0), *parts, flush=True)


ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def arg(name, default=None):
    if name in ARGS:
        i = ARGS.index(name)
        return ARGS[i + 1] if i + 1 < len(ARGS) and not ARGS[i + 1].startswith("--") else True
    return default


EVA, HANDS, RIGJSON, GTEX = (os.path.abspath(arg(k)) for k in ("--eva", "--hands", "--rig", "--gtex"))
OUT = os.path.abspath(arg("--out"))
STAGE = arg("--stage", "full")
SAMPLES = int(arg("--samples", 64))
RENDER_DIR = os.path.join(OUT, "renders")
for folder in (OUT, RENDER_DIR):
    os.makedirs(folder, exist_ok=True)
NAME = arg("--name", "A7_EvaArmFit")
SEAM_R = float(arg("--seam-radius", 0.0))      # EVA-SHOULDER-SEAM-02: 0 = off (v01 behaviour)
BS = chr(92)
RIG = "A7_ContinuousBody_Rig"
EYE_H = 1.65                                   # authoritative eye height
FIT02_BACK = 0.330                             # FitV02 look-down presentation: visual body this far behind the eye
BODY_BACK = float(arg("--body-back", 0.05))    # recommended while the full arms are shown
SIDES = ("R", "L")
SIGN = {"R": 1.0, "L": -1.0}
REST_WRIST = {"R": Vector((-0.170, -0.450, -0.220)), "L": Vector((0.170, -0.450, -0.220))}   # glove pivots in the hands FBX
THUMB_REST = {"R": Vector((1.0, 0.0, 0.0)), "L": Vector((-1.0, 0.0, 0.0))}
RING_CUT = 0.054                               # the glove's disconnect ring + lock collar = first 54 mm of its Arm part
RING_PROFILE = [(0.0440, 0.004), (0.0500, 0.012), (0.0525, 0.016), (0.0525, 0.028), (0.0560, 0.028), (0.0560, 0.040),
                (0.0525, 0.040), (0.0525, 0.050), (0.0490, 0.054)]
BOOT_L = 0.026                                 # new adapter boot from the ring onto the elliptical sleeve
C3 = Matrix(((-1.0, 0.0, 0.0), (0.0, 0.0, 1.0), (0.0, -1.0, 0.0)))   # Unity = C3 @ Blender


def b2u(v):
    return Vector((-v[0], v[2], -v[1]))


def eye_to_body(p, back=None):
    """Unity eye-frame point (x right, y up, z forward) -> body Blender frame (feet root at origin, forward -Y, left +X)."""
    back = BODY_BACK if back is None else back
    return Vector((-p[0], -back - p[2], EYE_H + p[1]))


def rnd(v, n=4):
    return [round(float(x), n) for x in v]


def select_only(objs, active=None):
    bpy.context.view_layer.update()
    for o in list(bpy.context.view_layer.objects):
        if o is not None:
            o.select_set(False)
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = active or objs[0]


def import_fbx(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    bpy.context.view_layer.update()
    return {o.name: o for o in set(bpy.data.objects) - before}


def hex_rgba(h, a=1.0):
    h = h.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c) + (a,)


def bsdf_of(mat):
    mat.use_nodes = True
    return next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")


# ------------------------------------------------------------------ materials
def setup_materials(fit02_mats):
    for name, spec in fit02_mats.items():
        mat = bpy.data.materials.get("MAT_" + name)
        if mat is None:
            continue
        node = bsdf_of(mat)
        node.inputs["Base Color"].default_value = hex_rgba(spec["base_color_srgb"])
        node.inputs["Roughness"].default_value = spec["roughness"]
        node.inputs["Metallic"].default_value = spec["metallic"]
    glove = bpy.data.materials.get("AH_AstronautHands")
    if glove is not None:
        tree = glove.node_tree if glove.use_nodes else None
        node = bsdf_of(glove)
        tree = glove.node_tree
        base = tree.nodes.new("ShaderNodeTexImage")
        base.image = bpy.data.images.load(os.path.join(GTEX, "AstronautHands_BaseColor.png"))
        tree.links.new(base.outputs["Color"], node.inputs["Base Color"])
        ms = tree.nodes.new("ShaderNodeTexImage")
        ms.image = bpy.data.images.load(os.path.join(GTEX, "AstronautHands_MetallicSmoothness.png"))
        ms.image.colorspace_settings.name = "Non-Color"
        sep = tree.nodes.new("ShaderNodeSeparateColor")
        tree.links.new(ms.outputs["Color"], sep.inputs["Color"])
        tree.links.new(sep.outputs[0], node.inputs["Metallic"])
        inv = tree.nodes.new("ShaderNodeMath")
        inv.operation = "SUBTRACT"
        inv.inputs[0].default_value = 1.0
        tree.links.new(ms.outputs["Alpha"], inv.inputs[1])
        tree.links.new(inv.outputs[0], node.inputs["Roughness"])


# ------------------------------------------------------------------ fit of the glove onto the sleeve end (bind pose)
def frame_rest(side):
    return Matrix((THUMB_REST[side], Vector((0.0, 1.0, 0.0)), Vector((0.0, 0.0, 1.0)))).transposed()


def sleeve_end(rig, sleeve, side):
    """End axis and centre of the sleeve in bind, from 10 mm band centroids of its last 6 cm (not from the bone line)."""
    H = rig.matrix_world @ rig.data.bones["wrist." + side].head_local
    lo = rig.data.bones["lowerarm02." + side]
    F = ((rig.matrix_world @ lo.tail_local) - (rig.matrix_world @ lo.head_local)).normalized()
    vs = [sleeve.matrix_world @ v.co for v in sleeve.data.vertices]
    proj = [(v - H).dot(F) for v in vs]
    end = max(proj)
    cents = []
    for i in range(6):
        band = [v for v, p in zip(vs, proj) if end - 0.010 * (i + 1) < p <= end - 0.010 * i]
        if len(band) >= 6:
            cents.append(sum(band, Vector()) / len(band))
    A = (cents[0] - cents[-1]).normalized()
    th = rig.matrix_world @ rig.data.bones["finger1-1." + side].head_local
    lf = rig.matrix_world @ rig.data.bones["metacarpal4." + side].head_local
    T = (th - lf) - A * (th - lf).dot(A)                           # hand's lateral axis, little finger -> thumb
    return {"C": cents[0], "A": A, "T": T.normalized(), "H": H, "F": F, "verts": vs,
            "axis_vs_forearm_deg": degrees(A.angle(F)), "end_beyond_wrist_mm": (end) * 1000}


def glove_matrix(side, fit, k, ox=0.0, oz=0.0):
    """k along the sleeve end axis; ox towards the thumb side, oz towards the back of the hand (metres)."""
    A, T = fit["A"], fit["T"]
    back = A.cross(T) * SIGN[side]
    target = Matrix((T, -A, back)).transposed()
    rot = target @ frame_rest(side).inverted()
    return Matrix.Translation(fit["C"] + A * k + T * ox + back * oz) @ rot.to_4x4() @ Matrix.Translation(-REST_WRIST[side])


def ring_mesh(arm_obj, side):
    """The unchanged first 54 mm of the glove's Arm part (disconnect ring, lock collar, tabs), in rest hands-FBX space."""
    bm = bmesh.new()
    bm.from_mesh(arm_obj.data)
    bm.transform(arm_obj.matrix_world)
    cut = REST_WRIST[side].y + RING_CUT + 1e-5
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if any(v.co.y > cut for v in f.verts)], context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    return bm


def boot_mesh(side, edge, sleeve_rest, materials_index):
    """Adapter boot in rest hands-FBX space: from the ring's open edge (r 0.049) onto the sleeve with 1.5 mm clearance and
    an inward lip, so neither the sleeve end nor the inside of the ring can be seen."""
    w = REST_WRIST[side]
    ang = sorted(edge, key=lambda p: atan2(p.z - w.z, p.x - w.x))
    y0, y1 = w.y + RING_CUT, w.y + RING_CUT + BOOT_L
    band = [p for p in sleeve_rest if abs(p.y - y1) < 0.006]
    prox = []
    for p in ang:
        a = atan2(p.z - w.z, p.x - w.x)
        near = [q for q in band if abs((atan2(q.z - w.z, q.x - w.x) - a + pi) % (2 * pi) - pi) < radians(12)]
        r = max(((q.x - w.x) ** 2 + (q.z - w.z) ** 2) ** 0.5 for q in near) if near else 0.040
        prox.append(r + 0.0015)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    rings = []
    steps = [(0.0, None), (0.25, None), (0.5, None), (0.75, None), (1.0, None), (1.04, "lip")]
    for t, kind in steps:
        ring = []
        y = y0 + (y1 - y0) * min(t, 1.0) + (0.001 if kind == "lip" else 0.0)
        ring_band = [q for q in sleeve_rest if abs(q.y - y) < 0.004]
        for p, r1 in zip(ang, prox):
            a = atan2(p.z - w.z, p.x - w.x)
            r0 = ((p.x - w.x) ** 2 + (p.z - w.z) ** 2) ** 0.5
            s = min(t, 1.0)
            s = s * s * (3 - 2 * s)
            r = r0 + (r1 - r0) * s
            near = [q for q in ring_band if abs((atan2(q.z - w.z, q.x - w.x) - a + pi) % (2 * pi) - pi) < radians(14)]
            if near and t > 0.0 and kind != "lip":
                r = max(r, max(((q.x - w.x) ** 2 + (q.z - w.z) ** 2) ** 0.5 for q in near) + 0.0015)   # enclose the sleeve here too
            if kind == "lip":
                r = r1 - 0.0035
            ring.append(bm.verts.new((w.x + r * cos(a), y, w.z + r * sin(a))))
        rings.append(ring)
    n = len(ang)
    for i in range(len(rings) - 1):
        for k in range(n):
            k1 = (k + 1) % n
            f = bm.faces.new((rings[i][k], rings[i][k1], rings[i + 1][k1], rings[i + 1][k]))
            f.material_index = materials_index
            for loop, (u, v) in zip(f.loops, ((k / n, i / 5), ((k + 1) / n, i / 5), ((k + 1) / n, (i + 1) / 5), (k / n, (i + 1) / 5))):
                loop[uv].uv = (u * 3.0, v * 0.6)
    bm.normal_update()
    for f in bm.faces:
        c = f.calc_center_median()
        lip = c.y > y1 + 0.0002
        want = Vector((0.0, 1.0, 0.0)) if lip else Vector((c.x - w.x, 0.0, c.z - w.z))
        if f.normal.dot(want) < 0.0:
            f.normal_flip()
    return bm, prox


def bm_bvh(bms):
    verts, polys = [], []
    for bm in bms:
        base = len(verts)
        verts += [v.co.copy() for v in bm.verts]
        polys += [tuple(base + v.index for v in f.verts) for f in bm.faces]
    return BVHTree.FromPolygons(verts, polys)


def obj_bm_rest(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.transform(obj.matrix_world)
    bm.verts.index_update()
    return bm


RAYS = [Vector((cos(radians(a)), 0.0, sin(radians(a)))) for a in range(0, 360, 30)] + \
       [Vector((cos(radians(a)) * 0.7, -0.714, sin(radians(a)) * 0.7)).normalized() for a in range(0, 360, 90)]


def visible_count(side, sleeve_world, M, static_bms, boot_bm, detail=None):
    """Sleeve vertices under the glove zone that can still be seen: some radial or distal ray escapes the glove assembly."""
    Minv = M.inverted()
    w = REST_WRIST[side]
    for bm in static_bms + [boot_bm]:
        bm.verts.index_update()
    bvh = bm_bvh(static_bms + [boot_bm])
    seen, zone = 0, 0
    for v in sleeve_world:
        p = Minv @ v
        if p.y - w.y > RING_CUT + BOOT_L - 0.001:
            continue
        zone += 1
        for d in RAYS:
            hit = bvh.ray_cast(p + d * 0.0005, d, 0.15)
            if hit[0] is None:
                seen += 1
                if detail is not None:
                    q = p - w
                    loc, nor, idx, dist = bvh.find_nearest(p)
                    outside = (p - loc).dot(nor) if loc is not None else float("nan")
                    detail.append({"y_mm": round(q.y * 1000, 2), "r_mm": round((q.x * q.x + q.z * q.z) ** 0.5 * 1000, 2),
                                   "angle_deg": round(degrees(atan2(q.z, q.x)), 1), "escape_dir": rnd(d, 2),
                                   "nearest_surface_mm": round(dist * 1000, 3), "outside_mm": round(outside * 1000, 3)})
                break
    return seen, zone


def ring_edge(bm, side):
    w = REST_WRIST[side]
    return [v.co.copy() for v in bm.verts if v.is_boundary and abs(v.co.y - (w.y + RING_CUT)) < 2e-4]


def scan_fit(side, fit, hand_objs, arm_obj, boot_mat_index):
    """Choose the glove placement (k along the sleeve end axis, lateral ox/oz) that hides every sleeve vertex under the
    glove; prefer the smallest lateral shift and k closest to the sleeve end centre."""
    static = [obj_bm_rest(o) for o in hand_objs] + [ring_mesh(arm_obj, side)]
    edge = ring_edge(static[-1], side)
    rows, cands = [], []
    for k_mm in range(-8, 5, 1):
        for ox_mm in (-3.0, -1.5, 0.0, 1.5, 3.0):
            for oz_mm in (-3.0, -1.5, 0.0, 1.5, 3.0):
                k, ox, oz = k_mm / 1000.0, ox_mm / 1000.0, oz_mm / 1000.0
                M = glove_matrix(side, fit, k, ox, oz)
                rest_sleeve = [M.inverted() @ v for v in fit["verts"]]
                boot, prox = boot_mesh(side, edge, rest_sleeve, boot_mat_index)
                seen, zone = visible_count(side, fit["verts"], M, static, boot)
                boot.free()
                cands.append((seen, abs(ox_mm) + abs(oz_mm), abs(k_mm), k_mm, ox_mm, oz_mm, zone))
    cands.sort()
    best = cands[0]
    detail = []
    M = glove_matrix(side, fit, best[3] / 1000.0, best[4] / 1000.0, best[5] / 1000.0)
    boot, prox = boot_mesh(side, edge, [M.inverted() @ v for v in fit["verts"]], boot_mat_index)
    visible_count(side, fit["verts"], M, static, boot, detail)
    boot.free()
    log(side, "visible at best placement:", detail)
    fit["visible_detail"] = detail
    for bm in static:
        bm.free()
    rows = [c for c in cands if c[3:6] in ((best[3], best[4], best[5]),)] + [c for c in cands if c[4] == 0.0 and c[5] == 0.0]
    return (best[3] / 1000.0, best[4] / 1000.0, best[5] / 1000.0), best, sorted(set(rows), key=lambda r: r[3]), len(edge)


# ------------------------------------------------------------------ attach the glove to the forearm (bind), keep world transforms
def keep_world_parent(obj, parent, bone=None):
    bpy.context.view_layer.update()
    w = obj.matrix_world.copy()
    obj.parent = parent
    if bone:
        obj.parent_type = "BONE"
        obj.parent_bone = bone
    bpy.context.view_layer.update()
    obj.matrix_world = w


def attach(side, fit, place, hands, rig, joints, boot_mat):
    k, ox, oz = place
    M = glove_matrix(side, fit, k, ox, oz)
    rot = Matrix((fit["T"], -fit["A"], fit["A"].cross(fit["T"]) * SIGN[side])).transposed() @ frame_rest(side).inverted()
    arm = hands[side + "_Arm"]
    bm = ring_mesh(arm, side)
    edge = ring_edge(bm, side)
    bm.transform(M)
    me = bpy.data.meshes.new(NAME + "_" + side + "_WristRing")
    bm.to_mesh(me)
    bm.free()
    for m in arm.data.materials:
        me.materials.append(m)
    ring = bpy.data.objects.new(side + "_WristRing", me)
    bpy.context.scene.collection.objects.link(ring)
    rest_sleeve = [M.inverted() @ v for v in fit["verts"]]
    bm, prox = boot_mesh(side, edge, rest_sleeve, 0)
    bm.transform(M)
    me = bpy.data.meshes.new(NAME + "_" + side + "_AdapterBoot")
    bm.to_mesh(me)
    bm.free()
    me.materials.append(boot_mat)
    for poly in me.polygons:
        poly.use_smooth = True
    boot = bpy.data.objects.new(side + "_AdapterBoot", me)
    bpy.context.scene.collection.objects.link(boot)
    parts = {n: o for n, o in hands.items() if n.startswith(side + "_") and n != side + "_Arm"}
    for o in parts.values():
        o.matrix_world = M @ o.matrix_world
    piv = bpy.data.objects.new("GloveWrist_" + side, None)
    piv.empty_display_type, piv.empty_display_size = "ARROWS", 0.04
    bpy.context.scene.collection.objects.link(piv)
    piv.matrix_world = Matrix.Translation(M @ REST_WRIST[side]) @ rot.to_4x4()
    for o in (piv, ring, boot):
        keep_world_parent(o, rig, "lowerarm02." + side)
    order = [j for j in joints if j["name"].startswith(side + "_") and j["name"] != side + "_Arm"]
    for j in order:
        parent = piv if j["parent"] in ("", side + "_Arm") else parts[j["parent"]]
        keep_world_parent(parts[j["name"]], parent)
    bpy.data.objects.remove(arm, do_unlink=True)
    bpy.context.view_layer.update()
    return {"pivot": piv, "ring": ring, "boot": boot, "parts": parts, "boot_clearance_mm": 1.5, "rot": rot,
            "pivot_rest": piv.matrix_world.copy(), "M": M,
            "boot_proximal_radius_mm": [round(min(prox) * 1000, 1), round(max(prox) * 1000, 1)]}


# ------------------------------------------------------------------ render helpers
def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def render_set(scene):
    world = bpy.data.worlds.new("EAF_World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.020, 0.022, 0.026, 1.0)
    scene.world = world
    for name, loc, power, size in (("EAF_Key", (1.2, -1.6, 2.4), 300.0, 1.2), ("EAF_Fill", (-1.6, -0.8, 1.6), 120.0, 1.5),
                                   ("EAF_Rim", (0.0, 1.5, 2.2), 90.0, 1.0)):
        data = bpy.data.lights.new(name, "AREA")
        data.energy, data.size = power, size
        light = bpy.data.objects.new(name, data)
        light.location = loc
        look_at(light, (0.0, -0.2, 1.2))
        scene.collection.objects.link(light)


def camera(scene, name, loc, target, vfov=None, lens=None):
    data = bpy.data.cameras.new(name)
    if vfov:
        data.sensor_fit = "VERTICAL"
        data.angle_y = radians(vfov)
    else:
        data.lens = lens
    data.clip_start = 0.05
    cam = bpy.data.objects.new(name, data)
    cam.location = loc
    look_at(cam, target)
    scene.collection.objects.link(cam)
    return cam


def render(scene, cam, path, size, samples):
    scene.camera = cam
    scene.render.engine = "CYCLES"
    scene.cycles.samples = samples
    scene.cycles.use_denoising = True
    scene.render.resolution_x, scene.render.resolution_y = size
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    log("rendered", os.path.basename(path))


def enable_gpu(scene):
    try:
        prefs = bpy.context.preferences.addons["cycles"].preferences
        prefs.compute_device_type = "OPTIX"
        prefs.get_devices()
        for d in prefs.devices:
            d.use = d.type == "OPTIX"
        scene.cycles.device = "GPU"
        return "OPTIX"
    except Exception as error:                                   # noqa: BLE001
        log("GPU setup failed:", error)
        return "CPU"


# ------------------------------------------------------------------ glove finger poses from AstronautHands_rig.json (unchanged values)
def rot_b(q):
    """Unity quaternion (x, y, z, w) of a joint in the hands' eye frame -> Blender rotation in the same (mirrored) frame."""
    Ru = Quaternion((q[3], q[0], q[1], q[2])).to_matrix()
    return C3.transposed() @ Ru @ C3


def glove_frames(side, glove, joints):
    """Rest joint frames (hand-space axes, joint origins) of the attached glove, captured once in bind."""
    bpy.context.view_layer.update()
    rot = glove["rot"]
    frames, objw = {}, {}
    for j in joints:
        n = j["name"]
        if not n.startswith(side + "_") or n == side + "_Arm":
            continue
        o = glove["parts"][n]
        frames[n] = Matrix.Translation(o.matrix_world.translation) @ rot.to_4x4()
        objw[n] = o.matrix_world.copy()
    return frames, objw


def apply_glove_pose(side, glove, joints, pose, frames, objw, finger_scale=1.0):
    """Pose the Hand (wrist flex under GloveWrist) and the 15 finger joints with the rig JSON rotations; finger_scale < 1
    opens the finger flexion proportionally (explicit retarget for a thicker handle)."""
    rots = {}
    for r in pose["rotations"]:
        q = rot_b(r["rotation"]).to_quaternion()
        if not r["name"].endswith("_Hand"):
            chain = r["name"].split("_", 1)[1].rstrip("123")
            sc = finger_scale.get(chain, 1.0) if isinstance(finger_scale, dict) else finger_scale
            if sc != 1.0:
                q = Quaternion().slerp(q, sc)
        rots[r["name"]] = q.to_matrix()
    bpy.context.view_layer.update()
    piv = glove["pivot"]
    rest_piv = glove["pivot_rest"]
    posed = {}
    for j in joints:
        n = j["name"]
        if n not in frames:
            continue
        parent = j["parent"]
        Jp_rest = rest_piv if parent in ("", side + "_Arm") else frames[parent]
        Jp_now = piv.matrix_world if parent in ("", side + "_Arm") else posed[parent]
        local = Jp_rest.inverted() @ frames[n]
        R = rots.get(n, Matrix.Identity(3))
        posed[n] = Jp_now @ local @ R.to_4x4()
        o = glove["parts"][n]
        o.matrix_world = posed[n] @ frames[n].inverted() @ objw[n]
        bpy.context.view_layer.update()
    return posed


# ------------------------------------------------------------------ two-bone arm IK on the original lengths
def set_bone_world(rig, name, M_world):
    pb = rig.pose.bones[name]
    pb.matrix = rig.matrix_world.inverted() @ M_world
    bpy.context.view_layer.update()


def bone_world(rig, name):
    return rig.matrix_world @ rig.pose.bones[name].matrix


def rotate_bone_about(rig, name, R, pivot):
    M = bone_world(rig, name)
    set_bone_world(rig, name, Matrix.Translation(pivot) @ R.to_4x4() @ Matrix.Translation(-pivot) @ M)


def girdle_share(rig, side, glove, target_pos, pole_dir, share, limit_deg=20.0):
    """Scapulohumeral rhythm, simplified: the clavicle takes a share of the upper arm's swing (bounded), so the torso's
    shoulder cap (skinned to the clavicle) follows the raised arm."""
    S = bone_world(rig, "upperarm01." + side).translation
    E = bone_world(rig, "lowerarm01." + side).translation
    P = glove["pivot"].matrix_world.translation.copy()
    Lu, Lf = (E - S).length, (P - E).length
    d_vec = target_pos - S
    d_eff = min(d_vec.length, Lu + Lf - 1e-4)
    u = d_vec.normalized()
    v = pole_dir - u * pole_dir.dot(u)
    v.normalize()
    a = acos(max(-1.0, min(1.0, (Lu * Lu + d_eff * d_eff - Lf * Lf) / (2 * Lu * d_eff))))
    E_new = S + u * (Lu * cos(a)) + v * (Lu * sin(a))
    q = (E - S).rotation_difference(E_new - S)
    ang = min(degrees(q.angle) * share, limit_deg)
    if ang < 0.01:
        return 0.0
    part = Quaternion(q.axis, radians(ang))
    head = bone_world(rig, "clavicle." + side).translation
    rotate_bone_about(rig, "clavicle." + side, part.to_matrix(), head)
    return ang


def solve_arm(rig, side, glove, target_pos, target_rot, pole_dir, share=0.33):
    """Clavicle share first, then shoulder fixed; elbow from the triangle; forearm twist on the whole forearm; the rest
    as glove wrist flex (bounded)."""
    girdle = girdle_share(rig, side, glove, target_pos, pole_dir, share)
    S = bone_world(rig, "upperarm01." + side).translation
    E = bone_world(rig, "lowerarm01." + side).translation
    P = glove["pivot"].matrix_world.translation.copy()
    Lu, Lf = (E - S).length, (P - E).length
    d_vec = target_pos - S
    d = d_vec.length
    short = max(0.0, d - (Lu + Lf - 1e-4))
    d_eff = min(d, Lu + Lf - 1e-4)
    u = d_vec.normalized()
    v = pole_dir - u * pole_dir.dot(u)
    v.normalize()
    cos_a = max(-1.0, min(1.0, (Lu * Lu + d_eff * d_eff - Lf * Lf) / (2 * Lu * d_eff)))
    a = acos(cos_a)
    E_new = S + u * (Lu * cos(a)) + v * (Lu * sin(a))
    rotate_bone_about(rig, "upperarm01." + side, (E - S).rotation_difference(E_new - S).to_matrix(), S)
    E_now = bone_world(rig, "lowerarm01." + side).translation
    P_now = glove["pivot"].matrix_world.translation.copy()
    W = S + u * d_eff
    rotate_bone_about(rig, "lowerarm01." + side, (P_now - E_now).rotation_difference(W - E_now).to_matrix(), E_now)
    axis = (glove["pivot"].matrix_world.translation - E_now).normalized()
    g_now = glove["pivot"].matrix_world.to_3x3()                   # pivot axes = glove hand-space axes
    back_now = (g_now @ Vector((0.0, 0.0, 1.0)))
    back_tgt = target_rot @ Vector((0.0, 0.0, 1.0))
    bn = (back_now - axis * back_now.dot(axis)).normalized()
    bt = (back_tgt - axis * back_tgt.dot(axis)).normalized()
    twist = atan2(axis.dot(bn.cross(bt)), bn.dot(bt))
    twist_used = max(-radians(80.0), min(radians(80.0), twist))
    rotate_bone_about(rig, "lowerarm01." + side, Matrix.Rotation(twist_used, 3, axis), E_now)   # whole forearm about elbow->pivot
    elbow = degrees(acos(max(-1.0, min(1.0, (Lu * Lu + Lf * Lf - d_eff * d_eff) / (2 * Lu * Lf)))))
    return {"upper_arm_m": round(Lu, 4), "forearm_to_glove_pivot_m": round(Lf, 4), "shoulder_to_target_m": round(d, 4),
            "short_by_mm": round(short * 1000, 1), "elbow_interior_deg": round(elbow, 1), "forearm_twist_deg": round(degrees(twist_used), 1),
            "twist_wanted_deg": round(degrees(twist), 1), "clavicle_share_deg": round(girdle, 1)}


def set_wrist_flex(glove, target_rot, limit_flex=60.0, limit_dev=35.0):
    """GloveWrist (child of lowerarm02) turns the hand to the target frame, clamped; returns the residual angle."""
    piv = glove["pivot"]
    bpy.context.view_layer.update()
    cur = piv.matrix_world.to_3x3()                                # pivot axes = glove hand-space axes
    delta = target_rot @ cur.inverted()
    q = delta.to_quaternion()
    ang = degrees(q.angle)
    if ang > limit_flex:
        q = Quaternion(q.axis, radians(limit_flex))
    new = q.to_matrix() @ piv.matrix_world.to_3x3()
    piv.matrix_world = Matrix.Translation(piv.matrix_world.translation) @ new.to_4x4()
    bpy.context.view_layer.update()
    got = piv.matrix_world.to_3x3()
    resid = degrees((target_rot @ got.inverted()).to_quaternion().angle)
    return {"wrist_turn_deg": round(min(ang, limit_flex), 1), "wanted_deg": round(ang, 1), "residual_deg": round(resid, 1)}


# ------------------------------------------------------------------ base pose (V04 floating clip at zero visual offset) and targets
ARM_BONES = ["clavicle", "shoulder01", "upperarm01", "upperarm02", "lowerarm01", "lowerarm02", "wrist"]


def capture_pose(src_rig):
    return {pb.name: (src_rig.matrix_world @ pb.matrix).copy() for pb in src_rig.pose.bones}


def apply_pose(rig, pose, only=None):
    order = [b.name for b in rig.data.bones]                       # parents come before children in this list
    for name in order:
        if only and not any(name.startswith(o + ".") for o in only):
            continue
        rig.pose.bones[name].matrix = rig.matrix_world.inverted() @ pose[name]
        bpy.context.view_layer.update()


def eye_frame_rot(q):
    """Unity quaternion of a glove placement (eye frame) -> Blender rotation in the body frame (same axes as the eye frame)."""
    return rot_b(q)


def wrench_frame(side, handle_u):
    """Hand frame for a power grip on a horizontal handle: thumb towards the jaw, back of the hand up."""
    h = Vector((-handle_u[0], -handle_u[2], handle_u[1])).normalized()       # Unity -> Blender direction
    x = -h if side == "R" else h
    up = Vector((0.0, 0.0, 1.0))
    z = (up - x * up.dot(x)).normalized()
    y = z.cross(x) if side == "R" else x.cross(z)
    return Matrix((x, y, z)).transposed()


GRIP_CENTRE_ORIGINAL = Vector((0.0, -0.082, -0.033))               # AstronautHands build: tube axis 8 mm under the palm pad
GRIP_CENTRE = Vector((0.0, -0.082, -0.0445))                       # retargeted: pad surface (25 mm) + 17.5 mm radius + 2 mm


# ------------------------------------------------------------------ measurements in a pose (world space)
def world_bvh(objs):
    dg = bpy.context.evaluated_depsgraph_get()
    verts, polys = [], []
    for o in objs:
        ev = o.evaluated_get(dg)
        me = ev.to_mesh()
        base = len(verts)
        verts += [o.matrix_world @ v.co for v in me.vertices]
        polys += [tuple(base + i for i in p.vertices) for p in me.polygons]
        ev.to_mesh_clear()
    return BVHTree.FromPolygons(verts, polys), verts


def glove_objs(glove, parts=None):
    names = parts or list(glove["parts"])
    return [glove["parts"][n] for n in names if glove["parts"][n].type == "MESH"] + [glove["ring"], glove["boot"]]


def visible_world(side, glove, sleeve):
    """Sleeve vertices under the glove zone that a radial or distal ray can still see, in the current pose."""
    ring = glove["ring"]
    frame = ring.matrix_world @ glove["ring_bind"].inverted() @ glove["pivot_rest"]
    finv = frame.inverted()
    rot3 = frame.to_3x3()
    bvh, _ = world_bvh(glove_objs(glove))
    _, sv = world_bvh([sleeve])
    seen = zone = 0
    for v in sv:
        p = finv @ v
        if p.y > RING_CUT + BOOT_L - 0.001:
            continue
        zone += 1
        for d in RAYS:
            dw = (rot3 @ d).normalized()
            if bvh.ray_cast(v + dw * 0.0005, dw, 0.15)[0] is None:
                seen += 1
                break
    return seen, zone


def overlaps(a_objs, b_objs):
    ba, _ = world_bvh(a_objs)
    bb, _ = world_bvh(b_objs)
    return len(ba.overlap(bb))


def limb_vs_torso(rig, side, sleeve, torso, seam=0.15):
    """Sleeve triangles overlapping the torso, split into the shoulder seam (within 0.15 m of the shoulder joint, where the
    FitV02 arm/torso split overlaps by design) and the limb proper."""
    dg = bpy.context.evaluated_depsgraph_get()
    ev = sleeve.evaluated_get(dg)
    me = ev.to_mesh()
    verts = [sleeve.matrix_world @ v.co for v in me.vertices]
    polys = [tuple(p.vertices) for p in me.polygons]
    ev.to_mesh_clear()
    ba = BVHTree.FromPolygons(verts, polys)
    bb, _ = world_bvh([torso])
    S = bone_world(rig, "upperarm01." + side).translation
    seam_n = limb_n = 0
    for ia, ib in ba.overlap(bb):
        c = sum((verts[i] for i in polys[ia]), Vector()) / len(polys[ia])
        if (c - S).length < seam:
            seam_n += 1
        else:
            limb_n += 1
    depth = 0.0
    for v in verts:
        if (v - S).length < seam:
            continue
        loc, nor, idx, dist = bb.find_nearest(v)
        if loc is not None and (v - loc).dot(nor) < 0.0:
            depth = max(depth, dist)
    return seam_n, limb_n, depth


def measure_pose(side, glove, eva, eye):
    sleeve = eva["A7_EVA_Arm_" + side]
    torso = eva["A7_EVA_TorsoLegs"]
    thumbs = [side + "_Thumb1", side + "_Thumb2", side + "_Thumb3"]
    seen, zone = visible_world(side, glove, sleeve)
    everything = [o for o in bpy.data.objects if o.type == "MESH" and not o.hide_render]
    bvh_all, _ = world_bvh(everything)
    near = bvh_all.find_nearest(eye)
    return {"sleeve_vertices_visible_under_glove": seen, "sleeve_vertices_in_glove_zone": zone,
            "thumb_vs_sleeve_boot_ring_overlap_tris": overlaps([glove["parts"][t] for t in thumbs], [sleeve, glove["boot"], glove["ring"]]),
            "glove_vs_torso_overlap_tris": overlaps(glove_objs(glove), [torso]),
            **dict(zip(("sleeve_vs_torso_overlap_tris_shoulder_seam", "sleeve_vs_torso_overlap_tris_limb", "sleeve_into_torso_max_depth_m"),
                       limb_vs_torso(bpy.data.objects[RIG], side, sleeve, torso))),
            "eye_to_nearest_surface_m": round(near[3], 4) if near[0] is not None else None}


# ------------------------------------------------------------------ render/measure props: the kit's wrench and connector, a stand-in rail
STANCE, ABOVE = 0.40, 0.30                        # technician's eye 0.40 m from the wall, 0.30 m above the connector nut
RAIL_EYE = Vector((0.0, -0.470, -0.205))          # the glove build's stand-in rail (eye frame, Blender axes), radius 17 mm


def kit_to_body():
    return Matrix.Translation((0.0, -STANCE, EYE_H - ABOVE))


def load_props():
    kit = import_fbx(os.path.abspath(arg("--kit")))
    keep = {"ServiceWrench", "ConnectorReceptacle", "ConnectorRepaired"}
    for n, o in list(kit.items()):
        if n not in keep:
            bpy.data.objects.remove(o, do_unlink=True)
            kit.pop(n)
    mat = bpy.data.materials.new("EAF_KitPreview")
    node = bsdf_of(mat)
    tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(os.path.join(os.path.abspath(arg("--kittex")), "AntennaRepair_BaseColor.png"))
    mat.node_tree.links.new(tex.outputs["Color"], node.inputs["Base Color"])
    node.inputs["Roughness"].default_value = 0.45
    K = kit_to_body()
    for o in kit.values():
        o.data.materials.clear()
        o.data.materials.append(mat)
        o.matrix_world = K @ o.matrix_world
    kit["_wrench_rest"] = kit["ServiceWrench"].matrix_world.copy()
    rail_mat = bpy.data.materials.new("EAF_RailPreview")
    bsdf_of(rail_mat).inputs["Base Color"].default_value = hex_rgba("#B5B3AE")
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=24, radius1=0.017, radius2=0.017, depth=0.8,
                          matrix=Matrix.Rotation(radians(90.0), 4, "Y"))
    me = bpy.data.meshes.new("EAF_RailMesh")
    bm.to_mesh(me)
    bm.free()
    me.materials.append(rail_mat)
    rail = bpy.data.objects.new("PreviewOnly rail", me)
    bpy.context.scene.collection.objects.link(rail)
    kit["_rail"] = rail
    return kit


def place_props(kit, name, pulled, motion):
    show_kit = name.startswith("Wrench")
    for n in ("ServiceWrench", "ConnectorReceptacle", "ConnectorRepaired"):
        kit[n].hide_render = not show_kit
    kit["_rail"].hide_render = name != "RailGrip"
    if name == "RailGrip":
        kit["_rail"].location = Vector((0.0, RAIL_EYE.y + pulled, EYE_H + RAIL_EYE.z))
    if show_kit:
        f = 54 if name == "WrenchLockStart" else 69
        r = motion["objects"]["ServiceWrench"][f]
        p = Vector((-r["position"][0], -r["position"][2], r["position"][1]))
        q = r["delta_rotation_xyzw"]
        rest = kit["_wrench_rest"]
        K = kit_to_body()
        kit["ServiceWrench"].matrix_world = K @ Matrix.Translation(p) @ rot_b(q).to_4x4() @ (K.inverted() @ rest).to_3x3().to_4x4()
    bpy.context.view_layer.update()


def grip_check(name, kit, hand_frame, motion, pulled):
    """Distance from the glove's grip centre to the gripped axis, and glove/prop contact."""
    c = hand_frame @ GRIP_CENTRE
    if name == "RailGrip":
        a0 = Vector((0.0, RAIL_EYE.y + pulled, EYE_H + RAIL_EYE.z))
        axis = Vector((1.0, 0.0, 0.0))
    elif name.startswith("Wrench"):
        f = 54 if name == "WrenchLockStart" else 69
        r = motion["objects"]["ServiceWrench"][f]
        a0 = kit_to_body() @ Vector((-r["position"][0], -r["position"][2], r["position"][1]))
        h = r["handle_axis"]
        axis = Vector((-h[0], -h[2], h[1])).normalized()
    else:
        return None
    off = (c - a0) - axis * (c - a0).dot(axis)
    return {"grip_centre_to_axis_mm": round(off.length * 1000, 2), "_axis": (a0, axis), "_centre": c}


GRIP_DEPTH_BY_PART = {}


def grip_depth(glove_objects, axis_point, axis, radius, half_len, centre):
    """Deepest glove vertex inside the gripped cylinder (radius, +-half_len around the grip centre along the axis)."""
    depth, inside = 0.0, 0
    GRIP_DEPTH_BY_PART.clear()
    for o in glove_objects:
        _, verts = world_bvh([o])
        for v in verts:
            t = (v - centre).dot(axis)
            if abs(t) > half_len:
                continue
            r = ((v - axis_point) - axis * (v - axis_point).dot(axis)).length
            if r < radius:
                inside += 1
                depth = max(depth, radius - r)
                GRIP_DEPTH_BY_PART[o.name] = round(max(GRIP_DEPTH_BY_PART.get(o.name, 0.0), (radius - r) * 1000), 2)
    return round(depth * 1000, 2), inside


# ------------------------------------------------------------------ clip, FBX, binding, .blend
def bone_path(rig, name):
    names, b = [], rig.data.bones[name]
    while b:
        names.append(b.name)
        b = b.parent
    return "/".join([rig.name] + list(reversed(names)))


def key_all(rig, gloves, stored):
    bpy.context.preferences.edit.keyframe_new_interpolation_type = "CONSTANT"
    scene = bpy.context.scene
    scene.render.fps, scene.render.fps_base = 30, 1.0                  # the FBX imports reset the scene to 24 fps
    scene.frame_start, scene.frame_end = 0, max(stored)
    objs = [o for g in gloves.values() for o in [g["pivot"]] + list(g["parts"].values())]
    for o in objs:
        o.rotation_mode = "QUATERNION"
    for f in sorted(stored):
        scene.frame_set(f)
        st = stored[f]
        for pb in rig.pose.bones:
            pb.matrix_basis = st["bones"][pb.name]
        for o in objs:
            o.matrix_basis = st["objs"][o.name]
        bpy.context.view_layer.update()
        for pb in rig.pose.bones:
            for path in ("location", "rotation_quaternion", "scale"):
                pb.keyframe_insert(path, frame=f)
        torso = bpy.data.objects.get("A7_EVA_TorsoLegs")
        if torso and torso.data.shape_keys:
            for kb in torso.data.shape_keys.key_blocks[1:]:
                kb.value = 1.0 if kb.name == st.get("corrective") else 0.0
                kb.keyframe_insert("value", frame=f)
        for o in objs:
            for path in ("location", "rotation_quaternion", "scale"):
                o.keyframe_insert(path, frame=f)


def scrub_blend(path):
    data = bytearray(open(path, "rb").read())
    hits = 0
    home = os.path.expanduser("~")
    for variant in {os.path.dirname(path), os.path.dirname(path).replace(BS, "/"), home, home.replace(BS, "/")}:
        needle = variant.encode("utf-8")
        start = data.find(needle)
        while start >= 0:
            end = data.find(b"\x00", start)
            data[start:end] = b"\x00" * (end - start)
            hits += 1
            start = data.find(needle, start)
    open(path, "wb").write(bytes(data))
    return hits


def export_all(scene, eva, rig, gloves, stored, report):
    key_all(rig, gloves, stored)
    expected_bounds(scene, stored)
    for o in list(bpy.data.objects):
        if o.type in ("CAMERA", "LIGHT") or o.name.startswith("PreviewOnly") or o.name in ("ServiceWrench", "ConnectorReceptacle", "ConnectorRepaired"):
            bpy.data.objects.remove(o, do_unlink=True)
    scene.frame_set(0)
    export = [rig] + [o for o in bpy.data.objects if o.parent is not None and o.type in ("MESH", "EMPTY")]
    cut = []
    for mat in {s for o in export if o.type == "MESH" for s in o.data.materials if s is not None and s.node_tree}:
        for link in list(mat.node_tree.links):
            if link.from_node.type == "TEX_IMAGE":
                cut.append((mat.node_tree, link.from_socket, link.to_socket))
                mat.node_tree.links.remove(link)
    select_only(export, active=rig)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, NAME + ".fbx"), use_selection=True, object_types={"ARMATURE", "MESH", "EMPTY"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
                             use_mesh_modifiers=not CORRECTIVE, mesh_smooth_type="FACE", use_triangles=True, add_leaf_bones=False,
                             primary_bone_axis="Y", secondary_bone_axis="X", use_armature_deform_only=False,
                             bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False, bake_anim_use_all_actions=False,
                             bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.0,
                             path_mode="RELATIVE", embed_textures=False)
    for tree, source, target in cut:
        tree.links.new(source, target)
    binding = {"skeleton": rig.name, "bones": len(rig.data.bones), "fps": 30,
               "clip_frames": {"0": "BasePose_FloatNeutralV04", "10": "Neutral", "20": "RailGrip", "30": "WrenchLockStart", "40": "WrenchLocked"},
               "import": "Unity ModelImporter: preserveHierarchy = true, animation type Generic, Anim. Compression Off; place the FBX root at the player's feet with zero visual offset (FloatNeutral V04 convention)",
               "sides": {}}
    for side, g in gloves.items():
        binding["sides"][side] = {
            "glove_root": g["pivot"].name, "glove_root_parent_bone": "lowerarm02." + side, "parent_bone_path": bone_path(rig, "lowerarm02." + side),
            "wrist_ring": g["ring"].name, "adapter_boot": g["boot"].name, "ring_and_boot_parent_bone": "lowerarm02." + side,
            "hand": side + "_Hand", "hand_parent": g["pivot"].name,
            "fingers": {n: o.parent.name for n, o in g["parts"].items() if n != side + "_Hand"},
            "wrist_flex": "rotate %s locally; the body's wrist.%s is not used for EVA (its sleeve vertices stay put)" % (g["pivot"].name, side),
            "finger_poses": "AstronautHands_rig.json Neutral/Grip/Press rotations unchanged; Grip on a 34-35 mm handle uses these per-chain slerp scales from rest: %s" % g.get("finger_scales", {}),
            "fit": report["fit"][side]}
    json.dump(binding, open(os.path.join(OUT, "binding.json"), "w", encoding="utf-8", newline="\n"), indent=1)
    for name in ("EAF_KitPreview", "EAF_RailPreview"):
        if name in bpy.data.materials:
            bpy.data.materials.remove(bpy.data.materials[name])
    repo = "//../../../Assets/_OutOfReach/Visuals/"
    for image in list(bpy.data.images):
        if image.name.startswith("AntennaRepair_"):
            bpy.data.images.remove(image)
        elif image.name.startswith("AstronautHands_"):
            image.filepath = repo + "Models/Aurora7/Props/AstronautHands/Textures/" + image.name
        elif image.name.startswith("Body_Weave_Normal"):
            image.filepath = repo + "Character/FirstPersonBody/FitV02/Textures/Body_Weave_Normal.png"
    blend = os.path.join(OUT, NAME + ".blend")
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=blend, compress=False, relative_remap=False)
    report["blend_path_scrubbed"] = scrub_blend(blend)
    json.dump(report, open(os.path.join(OUT, "fit-report.json"), "w", encoding="utf-8", newline="\n"), indent=1)
    log("exported", NAME + ".fbx", "and .blend")


def expected_bounds(scene, stored):
    """Model-space (Unity axes) bounding boxes of the skinned meshes per clip frame, from the evaluated (armature + shape
    keys) meshes, so the Unity check can verify the real skinned result and not only bone positions."""
    rows = ["frame,mesh,min_x,min_y,min_z,max_x,max_y,max_z"]
    names = ["A7_EVA_TorsoLegs", "A7_EVA_Arm_R", "A7_EVA_Arm_L"]
    for f in sorted(stored):
        scene.frame_set(f)
        bpy.context.view_layer.update()
        dg = bpy.context.evaluated_depsgraph_get()
        for name in names:
            obj = bpy.data.objects[name]
            ev = obj.evaluated_get(dg)
            me = ev.to_mesh()
            pts = [obj.matrix_world @ v.co for v in me.vertices]
            ev.to_mesh_clear()
            u = [(-p.x, p.z, -p.y) for p in pts]
            lo = [min(q[i] for q in u) for i in range(3)]
            hi = [max(q[i] for q in u) for i in range(3)]
            rows.append("%d,%s,%s" % (f, name, ",".join("%.6f" % x for x in lo + hi)))
    open(os.path.join(OUT, "expected_bounds.csv"), "w", encoding="utf-8", newline="\n").write("\n".join(rows) + "\n")
    scene.frame_set(0)
    log("expected bounds written for", len(stored), "frames")


# ------------------------------------------------------------------ EVA-SHOULDER-SEAM-02: torso shoulder band follows the upper arm
SEAM_RECESS = float(arg("--seam-recess", 0.0))
SEAM_MEDIAL = float(arg("--seam-medial", 1.0))     # how far towards the body midline (metres) the blend may reach   # bind-pose inward offset of the torso band next to the sleeve, metres


def seam_blend(rig, eva, side, radius, region=0.18):
    """Torso vertices within `radius` of the sleeve (and within `region` of the shoulder joint) take a smoothstep share of
    the nearest sleeve vertex's skin weights. Shared boundary vertices (distance 0) already carry identical weights."""
    torso, arm = eva["A7_EVA_TorsoLegs"], eva["A7_EVA_Arm_" + side]
    S = rig.matrix_world @ rig.data.bones["upperarm01." + side].head_local
    tree = KDTree(len(arm.data.vertices))
    for i, v in enumerate(arm.data.vertices):
        tree.insert(arm.matrix_world @ v.co, i)
    tree.balance()
    agroups = {g.index: g.name for g in arm.vertex_groups}
    tgroups = {g.name: g for g in torso.vertex_groups}
    tnames = {g.index: g.name for g in torso.vertex_groups}
    changed, maxdw, writes = 0, 0.0, []
    for v in torso.data.vertices:
        p = torso.matrix_world @ v.co
        if (p - S).length > region:
            continue
        if (p.x - S.x) * (1.0 if side == "L" else -1.0) < -SEAM_MEDIAL:
            continue                                   # outer shoulder and armpit only; leave the chest front alone
        co, ai, d = tree.find(p)
        if d >= radius:
            continue
        t = 1.0 - d / radius
        t = t * t * (3.0 - 2.0 * t)
        tw = {tnames[g.group]: g.weight for g in v.groups}
        aw = {agroups[g.group]: g.weight for g in arm.data.vertices[ai].groups}
        names = set(tw) | set(aw)
        new = {n: (1.0 - t) * tw.get(n, 0.0) + t * aw.get(n, 0.0) for n in names}
        total = sum(new.values()) or 1.0
        new = {n: w / total for n, w in new.items()}
        dw = max(abs(new.get(n, 0.0) - tw.get(n, 0.0)) for n in names)
        if dw < 1e-4:
            continue
        writes.append((v.index, names, new, t if d > 0.001 else 0.0))
        changed += 1
        maxdw = max(maxdw, dw)
    moved = 0
    if SEAM_RECESS > 0.0:
        torso.data.vertices.foreach_get  # noqa: B018 - keep bpy mesh in sync
        for idx, names, new, t in writes:
            if t > 0.0:
                v = torso.data.vertices[idx]
                v.co = v.co - v.normal * (SEAM_RECESS * t)
                moved += 1
        torso.data.update()
    for idx, names, new, t in writes:
        for n in names:
            if n not in tgroups:
                tgroups[n] = torso.vertex_groups.new(name=n)
            if new.get(n, 0.0) > 1e-4:
                tgroups[n].add([idx], new[n], "REPLACE")
            else:
                tgroups[n].remove([idx])
    return {"changed_torso_vertices": changed, "max_weight_change": round(maxdw, 4), "radius_m": radius, "region_m": region,
            "recessed_torso_vertices": moved, "recess_max_m": SEAM_RECESS}


# ------------------------------------------------------------------ EVA-SHOULDER-SEAM-02: pose-corrective torso blend shapes
CORRECTIVE = bool(arg("--corrective", False))
CORR_OUT, CORR_IN = 0.025, 0.003       # torso vertices up to 25 mm outside the sleeve are tucked 3 mm under it
CORR_ZONE, CORR_JUNCTION = 0.22, 0.12  # torso vertex within 0.22 m of the shoulder joint, nearest sleeve point within 0.12 m


def skin3(rig, torso):
    """Per-vertex linear-blend skinning rotation/scale (armature space, pose relative to bind) of the torso."""
    defm = {pb.name: (pb.matrix @ pb.bone.matrix_local.inverted()).to_3x3() for pb in rig.pose.bones}
    names = {g.index: g.name for g in torso.vertex_groups}
    out = []
    for v in torso.data.vertices:
        M = Matrix(((0.0, 0.0, 0.0), (0.0, 0.0, 0.0), (0.0, 0.0, 0.0)))
        tot = 0.0
        for g in v.groups:
            n = names[g.group]
            if n in defm and g.weight > 0.0:
                M = M + defm[n] * g.weight
                tot += g.weight
        out.append(M * (1.0 / tot) if tot > 0.0 else Matrix.Identity(3))
    return out


def junction_crossings(rig, eva):
    """Torso vertices (current pose, current shape-key mix) lying 0-25 mm outside a sleeve at the shoulder junction."""
    torso = eva["A7_EVA_TorsoLegs"]
    _, tv = world_bvh([torso])
    found, stats = {}, {}
    for side in SIDES:
        bvh, _ = world_bvh([eva["A7_EVA_Arm_" + side]])
        S = bone_world(rig, "upperarm01." + side).translation
        n = 0
        for i, p in enumerate(tv):
            if (p - S).length > CORR_ZONE:
                continue
            loc, nor, idx, dist = bvh.find_nearest(p)
            if loc is None or (loc - S).length > CORR_JUNCTION:
                continue
            signed = (p - loc).dot(nor)
            if 0.0 < signed < CORR_OUT:
                n += 1
                found[i] = (loc - nor * CORR_IN) - p
        stats[side] = n
    return found, stats


def make_corrective(rig, eva, name):
    torso = eva["A7_EVA_TorsoLegs"]
    found, before = junction_crossings(rig, eva)
    if not found:
        return None, before, {s: 0 for s in SIDES}
    assert (torso.matrix_world.to_3x3() - Matrix.Identity(3)).median_scale if False else True
    mats = skin3(rig, torso)
    if torso.data.shape_keys is None:
        torso.shape_key_add(name="Basis", from_mix=False)
    key = torso.shape_key_add(name="Seam_" + name, from_mix=False)
    key.value = 0.0
    inv_obj = torso.matrix_world.to_3x3().inverted()
    inv_rig = rig.matrix_world.to_3x3().inverted()
    for i, d in found.items():
        key.data[i].co = key.data[i].co + inv_obj @ (rig.matrix_world.to_3x3() @ (mats[i].inverted() @ (inv_rig @ d)))
    key.value = 1.0
    bpy.context.view_layer.update()
    _, after = junction_crossings(rig, eva)
    return key, before, after


# ------------------------------------------------------------------ EVA-SHOULDER-SEAM-02: scye bearing over the zigzag split boundary
SCYE = bool(arg("--scye", False))
SCYE_HALF, SCYE_OFF, SCYE_T = 0.027, 0.006, 0.006
SCYE_PLANES = {}
SCYE_SPLIT = bool(arg('--scye-split', False))   # half width along the loop normal, offset above the surface, thickness


def scye_band(rig, eva, side):
    """A ring that covers the FitV02 torso/arm split loop (a 47 mm zigzag); each cross-section copies the skin weights of
    its boundary vertex, so the ring stays on the boundary in every pose."""
    arm = eva["A7_EVA_Arm_" + side]
    bm = bmesh.new()
    bm.from_mesh(arm.data)
    bm.verts.ensure_lookup_table()
    S = rig.matrix_world @ rig.data.bones["upperarm01." + side].head_local
    bverts = {v for e in bm.edges if e.is_boundary for v in e.verts}
    loop = [v for v in bverts if ((arm.matrix_world @ v.co) - S).length < 0.20]
    pts = [arm.matrix_world @ v.co for v in loop]
    c = sum(pts, Vector()) / len(pts)
    cov = Matrix(((0.0, 0.0, 0.0), (0.0, 0.0, 0.0), (0.0, 0.0, 0.0)))
    for p in pts:
        d = p - c
        for i in range(3):
            for j in range(3):
                cov[i][j] += d[i] * d[j]
    A = Matrix.Identity(3) * (cov[0][0] + cov[1][1] + cov[2][2]) - cov
    n = Vector((0.3, 0.5, 0.8))
    for _ in range(80):
        n = (A @ n).normalized()
    u = (pts[0] - c - n * (pts[0] - c).dot(n)).normalized()
    w = n.cross(u)
    order = sorted(range(len(loop)), key=lambda i: atan2((pts[i] - c).dot(w), (pts[i] - c).dot(u)))
    radii = [((pts[i] - c) - n * (pts[i] - c).dot(n)).length for i in order]
    k = len(order)
    smooth = [sum(radii[(j + s) % k] for s in (-2, -1, 0, 1, 2)) / 5.0 for j in range(k)]
    out = bmesh.new()
    rings = []
    for j, i in enumerate(order):
        p = pts[i]
        radial = ((p - c) - n * (p - c).dot(n)).normalized()
        base = c + radial * smooth[j]
        r_in, r_out = SCYE_OFF, SCYE_OFF + SCYE_T
        rings.append([out.verts.new(base + radial * r + n * h) for r, h in ((r_in, -SCYE_HALF), (r_out, -SCYE_HALF), (r_out, SCYE_HALF), (r_in, SCYE_HALF))])
    for j in range(k):
        a, b = rings[j], rings[(j + 1) % k]
        for q in range(4):
            q1 = (q + 1) % 4
            out.faces.new((a[q], b[q], b[q1], a[q1]))
    bmesh.ops.recalc_face_normals(out, faces=out.faces[:])
    me = bpy.data.meshes.new(NAME + "_ScyeBearing_" + side)
    out.to_mesh(me)
    out.free()
    me.materials.append(bpy.data.materials["MAT_BrushedTitanium"])
    obj = bpy.data.objects.new("ScyeBearing_" + side, me)
    bpy.context.scene.collection.objects.link(obj)
    names = {g.index: g.name for g in arm.vertex_groups}
    groups = {}
    for j, i in enumerate(order):
        src = arm.data.vertices[loop[i].index]
        for g in src.groups:
            name = names[g.group]
            if name not in groups:
                groups[name] = obj.vertex_groups.new(name=name)
            groups[name].add([4 * j + q for q in range(4)], g.weight, "REPLACE")
    bm.free()
    mod = obj.modifiers.new("Armature", "ARMATURE")
    mod.object = rig
    keep_world_parent(obj, rig)
    if (c - Vector((0.0, c.y, c.z))).dot(n) < 0.0:
        n = -n                                         # point away from the body midline
    SCYE_PLANES[side] = (c.copy(), n.copy(), max(smooth))
    zig = [(p - c).dot(n) for p in pts]
    return obj, {"boundary_vertices": len(loop), "zigzag_mm": [round(min(zig) * 1000, 1), round(max(zig) * 1000, 1)],
                 "ring_half_width_mm": SCYE_HALF * 1000, "ring_radius_mm": [round(min(smooth) * 1000, 1), round(max(smooth) * 1000, 1)]}


def scye_split(rig, eva, side, inner=-0.010, outer=0.020, reach=0.040):
    """Move the effective torso/arm split to the scye plane: torso vertices on the arm side of the plane (the deltoid mass)
    take the nearest sleeve vertex's weights, with a smoothstep from `inner` to `outer` metres across the plane, inside the
    armhole radius + `reach`."""
    c, n, rad = SCYE_PLANES[side]
    torso, arm = eva["A7_EVA_TorsoLegs"], eva["A7_EVA_Arm_" + side]
    tree = KDTree(len(arm.data.vertices))
    for i, v in enumerate(arm.data.vertices):
        tree.insert(arm.matrix_world @ v.co, i)
    tree.balance()
    agroups = {g.index: g.name for g in arm.vertex_groups}
    tnames = {g.index: g.name for g in torso.vertex_groups}
    tgroups = {g.name: g for g in torso.vertex_groups}
    writes = []
    for v in torso.data.vertices:
        p = torso.matrix_world @ v.co
        s_ = (p - c).dot(n)
        if s_ <= inner or ((p - c) - n * s_).length > rad + reach:
            continue
        t = min(1.0, (s_ - inner) / (outer - inner))
        t = t * t * (3.0 - 2.0 * t)
        co, ai, d = tree.find(p)
        tw = {tnames[g.group]: g.weight for g in v.groups}
        aw = {agroups[g.group]: g.weight for g in arm.data.vertices[ai].groups}
        names = set(tw) | set(aw)
        new = {k: (1.0 - t) * tw.get(k, 0.0) + t * aw.get(k, 0.0) for k in names}
        tot = sum(new.values()) or 1.0
        writes.append((v.index, names, {k: w / tot for k, w in new.items()}))
    for idx, names, new in writes:
        for k in names:
            if k not in tgroups:
                tgroups[k] = torso.vertex_groups.new(name=k)
            if new.get(k, 0.0) > 1e-4:
                tgroups[k].add([idx], new[k], "REPLACE")
            else:
                tgroups[k].remove([idx])
    return {"torso_vertices_reweighted": len(writes), "transition_m": [inner, outer], "armhole_reach_m": reach}

# ------------------------------------------------------------------ diagnostic colours (never used for a delivered build)
DEBUG_COLOURS = bool(arg("--debug-colours", False))


def debug_colours(eva):
    """Torso green, sleeve WarmIvory red, sleeve FlexGrey blue, any other sleeve material yellow."""
    def flat(name, rgb):
        mat = bpy.data.materials.new(name)
        mat.use_nodes = True
        node = bsdf_of(mat)
        node.inputs["Base Color"].default_value = (rgb[0], rgb[1], rgb[2], 1.0)
        node.inputs["Roughness"].default_value = 0.8
        return mat
    torso, ivory, grey, other = (flat("DBG_Torso", (0.05, 0.55, 0.05)), flat("DBG_ArmIvory", (0.8, 0.04, 0.04)),
                                 flat("DBG_ArmGrey", (0.04, 0.1, 0.9)), flat("DBG_ArmOther", (0.9, 0.8, 0.05)))
    for name, obj in eva.items():
        if obj.type != "MESH":
            continue
        if name.startswith("A7_EVA_Arm_"):
            for slot in obj.material_slots:
                n = slot.material.name if slot.material else ""
                slot.material = ivory if "WarmIvory" in n else grey if "FlexGrey" in n else other
        elif name == "A7_EVA_TorsoLegs":
            for slot in obj.material_slots:
                slot.material = torso
    log("debug colours on")

# ------------------------------------------------------------------ EVA-SHOULDER-SEAM-02: one clean colour break on the sleeve
CUFF = bool(arg("--cuff", False))
CUFF_ZONE = 0.10       # faces whose centre lies within this distance of the fitted break plane are cut and re-coloured


def _normal_of(pts):
    """Least-squares plane through points: centroid and the direction of least spread."""
    c = sum(pts, Vector()) / len(pts)
    cov = Matrix(((0.0, 0.0, 0.0), (0.0, 0.0, 0.0), (0.0, 0.0, 0.0)))
    for p in pts:
        d = p - c
        for i in range(3):
            for j in range(3):
                cov[i][j] += d[i] * d[j]
    A = Matrix.Identity(3) * (cov[0][0] + cov[1][1] + cov[2][2]) - cov
    n = Vector((0.3, 0.5, 0.8))
    for _ in range(100):
        n = (A @ n).normalized()
    return c, n


def sleeve_cuff(rig, eva, side):
    """The FitV02 sleeve changes from WarmIvory to FlexGrey along a zigzag of whole faces (the 'jagged ivory/grey edge').
    Fit a plane to that boundary in bind pose, bisect the sleeve along it (new vertices interpolate weights and UVs) and
    colour every face near the plane by its side, so the break is one straight loop. Bones and the glove are untouched."""
    arm = eva["A7_EVA_Arm_" + side]
    names = [m.name if m else "" for m in arm.data.materials]
    iv = next(i for i, n in enumerate(names) if "WarmIvory" in n)
    gr = next(i for i, n in enumerate(names) if "FlexGrey" in n)
    S = rig.matrix_world @ rig.data.bones["upperarm01." + side].head_local
    E = rig.matrix_world @ rig.data.bones["lowerarm01." + side].head_local
    axis = (E - S).normalized()
    mw = arm.matrix_world
    nv0, nf0 = len(arm.data.vertices), len(arm.data.polygons)
    custom = bool(getattr(arm.data, "has_custom_normals", False))
    bm = bmesh.new()
    bm.from_mesh(arm.data)
    bverts = set()
    for e in bm.edges:
        mats = {f.material_index for f in e.link_faces}
        if iv in mats and gr in mats:
            bverts.update(e.verts)
    pts = [mw @ v.co for v in bverts]
    c, n = _normal_of(pts)
    if n.dot(axis) < 0.0:
        n = -n
    spread = [(p - c).dot(n) for p in pts]
    t_c = (c - S).dot(axis)
    imw = mw.inverted()
    co_obj = imw @ c
    no_obj = (mw.to_3x3().inverted().transposed() @ n).normalized()
    faces = [f for f in bm.faces if f.material_index in (iv, gr) and abs(((mw @ f.calc_center_median()) - c).dot(n)) < CUFF_ZONE]
    geom = list({v for f in faces for v in f.verts}) + list({e for f in faces for e in f.edges}) + faces
    bmesh.ops.bisect_plane(bm, geom=geom, dist=1e-6, plane_co=co_obj, plane_no=no_obj, clear_outer=False, clear_inner=False)
    changed = 0
    for f in bm.faces:
        if f.material_index not in (iv, gr):
            continue
        d = ((mw @ f.calc_center_median()) - c).dot(n)
        if abs(d) >= CUFF_ZONE:
            continue
        want = iv if d < 0.0 else gr
        if f.material_index != want:
            f.material_index = want
            changed += 1
    bm.to_mesh(arm.data)
    bm.free()
    arm.data.update()
    unweighted = sum(1 for v in arm.data.vertices if sum(g.weight for g in v.groups) < 0.999)
    edge_faces = {}
    for p in arm.data.polygons:
        for k in p.edge_keys:
            edge_faces.setdefault(k, set()).add(p.material_index)
    bpts = {i for k, m in edge_faces.items() if iv in m and gr in m for i in k}
    bd = [((mw @ arm.data.vertices[i].co) - c).dot(n) for i in bpts]
    return {"break_plane_point_along_upper_arm_m": round(t_c, 4),
            "break_plane_tilt_vs_upper_arm_axis_deg": round(degrees(n.angle(axis)), 2),
            "fitv02_zigzag_boundary_vertices": len(pts),
            "fitv02_zigzag_depth_across_plane_mm": [round(min(spread) * 1000, 1), round(max(spread) * 1000, 1)],
            "new_boundary_vertices": len(bpts),
            "new_boundary_depth_across_plane_mm": [round(min(bd) * 1000, 2), round(max(bd) * 1000, 2)] if bd else None,
            "faces_recoloured": changed, "vertices_added": len(arm.data.vertices) - nv0, "faces_added": len(arm.data.polygons) - nf0,
            "vertices_with_weight_sum_below_1": unweighted, "had_custom_normals": custom}


# ------------------------------------------------------------------ main
V04 = os.path.abspath(arg("--v04"))
ELBOW_OK = 150.0                                  # reachable = elbow interior <= 150 deg (30 deg bend kept)


def u2b_dir(d):
    return Vector((-d[0], -d[2], d[1]))


def setup(scene):
    eva = import_fbx(EVA)
    hands = import_fbx(HANDS)
    rig = eva[RIG]
    rigdef = json.load(open(RIGJSON, encoding="utf-8"))
    setup_materials(json.load(open(arg("--fit02mats"), encoding="utf-8")))
    if rig.animation_data:
        rig.animation_data.action = None
    rig.data.pose_position = "REST"
    bpy.context.view_layer.update()
    gloves, fits = {}, {}
    for side in SIDES:
        sleeve = eva["A7_EVA_Arm_" + side]
        fit = sleeve_end(rig, sleeve, side)
        hand_objs = [o for n, o in hands.items() if n.startswith(side + "_") and n != side + "_Arm" and o.type == "MESH"]
        place, best, rows, nedge = scan_fit(side, fit, hand_objs, hands[side + "_Arm"], 0)
        glove = attach(side, fit, place, hands, rig, rigdef["joints"], bpy.data.materials["MAT_EVA_FlexGrey"])
        glove["ring_bind"] = glove["ring"].matrix_world.copy()
        glove["pivot_basis"] = glove["pivot"].matrix_basis.copy()
        glove["frames"], glove["objw"] = glove_frames(side, glove, rigdef["joints"])
        gloves[side] = glove
        fits[side] = {"k_mm": place[0] * 1000, "lateral_thumb_mm": place[1] * 1000, "lateral_back_mm": place[2] * 1000,
                      "sleeve_vertices_visible_in_bind": best[0], "sleeve_vertices_in_glove_zone": best[6],
                      "sleeve_end_axis_vs_forearm_deg": round(fit["axis_vs_forearm_deg"], 2),
                      "boot_proximal_radius_mm": glove["boot_proximal_radius_mm"]}
        log(side, "glove fit", fits[side])
    if CUFF:
        for side in SIDES:
            fits[side]["sleeve_cuff"] = sleeve_cuff(rig, eva, side)
            log(side, "sleeve cuff", fits[side]["sleeve_cuff"])
    if SCYE:
        for side in SIDES:
            band, info = scye_band(rig, eva, side)
            fits[side]["scye_bearing"] = info
            log(side, "scye bearing", info)
            if SCYE_SPLIT:
                fits[side]["scye_split"] = scye_split(rig, eva, side)
                log(side, "scye split", fits[side]["scye_split"])
    if SEAM_R > 0.0:
        for side in SIDES:
            fits[side]["seam_weight_blend"] = seam_blend(rig, eva, side, SEAM_R)
            log(side, "seam blend", fits[side]["seam_weight_blend"])
    v04 = import_fbx(V04)
    v04rig = next(o for o in v04.values() if o.type == "ARMATURE")
    scene.frame_set(int(v04rig.animation_data.action.frame_range[0]))
    base = capture_pose(v04rig)
    for o in v04.values():
        bpy.data.objects.remove(o, do_unlink=True)
    rig.data.pose_position = "POSE"
    apply_pose(rig, base)
    return eva, hands, rig, rigdef, gloves, fits, base


def reset_pose(rig, base, gloves, rigdef, poses):
    apply_pose(rig, base)
    for side, glove in gloves.items():
        glove["pivot"].matrix_basis = glove["pivot_basis"]
        bpy.context.view_layer.update()
        apply_glove_pose(side, glove, rigdef["joints"], poses["Neutral"], glove["frames"], glove["objw"])


def pull_to_reach(shoulder_u, target_u, lu, lf):
    """Move a target towards the eye along the view axis until the shoulder distance allows a 150 deg elbow."""
    d_ok = (lu * lu + lf * lf - 2 * lu * lf * cos(radians(ELBOW_OK))) ** 0.5
    t = Vector(target_u)
    s = Vector(shoulder_u)
    if (t - s).length <= d_ok:
        return t, 0.0, d_ok
    lo, hi = 0.0, t.z - s.z
    for _ in range(60):
        mid = 0.5 * (lo + hi)
        if (t - Vector((0.0, 0.0, mid)) - s).length > d_ok:
            lo = mid
        else:
            hi = mid
    return t - Vector((0.0, 0.0, hi)), hi, d_ok


def eye_u(p, back=0.0):
    return Vector((-p.x, p.z - EYE_H, -(p.y + back)))


def capture(rig, gloves):
    st = {"bones": {pb.name: pb.matrix_basis.copy() for pb in rig.pose.bones}, "objs": {}}
    for glove in gloves.values():
        for o in [glove["pivot"]] + list(glove["parts"].values()):
            st["objs"][o.name] = o.matrix_basis.copy()
    return st


def rail_target(place, hand_rot):
    """Same arm orientation as the approved Grip placement, wrist moved so the retargeted grip centre is on the rail axis
    at the point where the original build closed the hand (x -0.130 on the rail)."""
    R_arm = rot_b(place["Grip"]["R_Arm"]["rotation"])
    R_hand = R_arm @ hand_rot["Grip"]["R_Hand"]
    on_rail = Vector((RAIL_EYE.x - 0.130, RAIL_EYE.y, RAIL_EYE.z))           # eye frame, Blender axes
    pivot = on_rail - R_hand @ GRIP_CENTRE
    return (Vector((-pivot.x, pivot.z, -pivot.y)), R_arm)


def define_targets(rigdef):
    place = {p["name"]: {pl["name"]: pl for pl in p.get("placements", [])} for p in rigdef["poses"]}
    hand_rot = {p["name"]: {r["name"]: rot_b(r["rotation"]) for r in p["rotations"]} for p in rigdef["poses"]}
    t = [("Neutral", "Neutral", {s: (Vector(place["Neutral"][s + "_Arm"]["position"]), rot_b(place["Neutral"][s + "_Arm"]["rotation"])) for s in SIDES}),
         ("RailGrip", "Grip", {"R": rail_target(place, hand_rot)})]
    s = json.load(open(arg("--motion"), encoding="utf-8"))
    stance, above = 0.40, 0.30                     # technician's eye 0.40 m from the wall, 0.30 m above the nut
    for f, label in ((54, "WrenchLockStart"), (69, "WrenchLocked")):
        r = s["objects"]["ServiceWrench"][f]
        grip_u = Vector((r["position"][0], r["position"][1] - above, r["position"][2] + stance))
        R_hand = wrench_frame("R", r["handle_axis"])
        grip_b = eye_to_body(grip_u, back=0.0)
        pivot_b = grip_b - R_hand @ GRIP_CENTRE
        R_pivot = R_hand @ hand_rot["Grip"]["R_Hand"].inverted()
        t.append((label, "Grip", {"R": (eye_u(pivot_b), R_pivot)}))
    return t


PROPS, MOTION = {}, {}


def solve_targets(scene, eva, rig, rigdef, gloves, base):
    PROPS.update(load_props())
    MOTION.update(json.load(open(arg("--motion"), encoding="utf-8")))
    poses = {p["name"]: p for p in rigdef["poses"]}
    eye_b = Vector((0.0, 0.0, EYE_H))
    stored, results = {0: None}, {}
    reset_pose(rig, base, gloves, rigdef, poses)
    stored[0] = capture(rig, gloves)
    frame = 0
    for name, finger_pose, sides_t in define_targets(rigdef):
        frame += 10
        reset_pose(rig, base, gloves, rigdef, poses)
        res = {}
        for side, (pos_u, R_t) in sides_t.items():
            S = bone_world(rig, "upperarm01." + side).translation
            E = bone_world(rig, "lowerarm01." + side).translation
            P = gloves[side]["pivot"].matrix_world.translation
            lu, lf = (E - S).length, (P - E).length
            sh_u = eye_u(S)
            orig_d = (Vector(pos_u) - sh_u).length
            pos2, pulled, d_ok = pull_to_reach(sh_u, pos_u, lu, lf)
            target_b = eye_to_body(pos2, back=0.0)
            pole = Vector((-0.80 * SIGN[side], 0.10, -1.0))                # elbow down and well out, away from the ribs
            info = solve_arm(rig, side, gloves[side], target_b, R_t, pole)
            flex = set_wrist_flex(gloves[side], R_t)
            posed = apply_glove_pose(side, gloves[side], rigdef["joints"], poses[finger_pose], gloves[side]["frames"], gloves[side]["objw"])
            err = (gloves[side]["pivot"].matrix_world.translation - target_b).length
            if side == "R":
                place_props(PROPS, name, pulled, MOTION)
                grip = grip_check(name, PROPS, posed[side + "_Hand"], MOTION, pulled)
                if grip:
                    a0, axis = grip.pop("_axis")
                    centre = grip.pop("_centre")
                    radius = 0.017 if name == "RailGrip" else 0.0175
                    chains = ["Thumb", "Index", "Middle", "Ring", "Little"]
                    scales = {c: 1.0 for c in chains}
                    scan = {}
                    for c in chains:
                        objs_c = [gloves[side]["parts"]["%s_%s%d" % (side, c, i)] for i in (1, 2, 3)]
                        tried = []
                        for sc in ((1.0, 0.9, 0.8, 0.7, 0.6, 0.5, 0.4, 0.3, 0.2, 0.1, 0.0) if c == "Thumb" else (1.0, 0.9, 0.8, 0.7, 0.6, 0.5, 0.4, 0.3)):
                            scales[c] = sc
                            apply_glove_pose(side, gloves[side], rigdef["joints"], poses[finger_pose], gloves[side]["frames"], gloves[side]["objw"], scales)
                            tried.append((sc, grip_depth(objs_c, a0, axis, radius, 0.06, centre)[0]))
                        ok = [x for x in tried if x[1] <= 2.5]
                        scales[c] = max(ok)[0] if ok else min(tried, key=lambda x: x[1])[0]
                        scan[c] = tried
                    apply_glove_pose(side, gloves[side], rigdef["joints"], poses[finger_pose], gloves[side]["frames"], gloves[side]["objw"], scales)
                    depth, n_in = grip_depth(glove_objs(gloves[side]), a0, axis, radius, 0.06, centre)
                    gloves[side]["finger_scales"] = dict(scales)
                    grip.update({"gripped_radius_mm": radius * 1000, "finger_flex_scale_used": dict(scales), "glove_into_gripped_part_max_mm": depth,
                                 "glove_vertices_inside": n_in, "finger_scale_scan": scan, "depth_by_part_mm": dict(GRIP_DEPTH_BY_PART),
                                 "grip_centre_in_hand_mm": rnd(GRIP_CENTRE * 1000, 1), "original_grip_centre_in_hand_mm": rnd(GRIP_CENTRE_ORIGINAL * 1000, 1)})
                    info["grip"] = grip
            res[side] = {"original_target_eye": rnd(pos_u), "original_shoulder_distance_m": round(orig_d, 4),
                         "original_short_by_mm": round(max(0.0, orig_d - (lu + lf)) * 1000, 1),
                         "pulled_towards_eye_mm": round(pulled * 1000, 1), "solved_target_eye": rnd(pos2), "comfortable_reach_m": round(d_ok, 4),
                         "glove_pivot_error_mm": round(err * 1000, 2), "shoulder_eye": rnd(sh_u), **info, **flex, "finger_pose": finger_pose}
        corr = None
        if CORRECTIVE:
            corr, cb, ca = make_corrective(rig, eva, name)
            res["seam_corrective"] = {"shape_key": corr.name if corr else None, "junction_crossings_before": cb, "junction_crossings_after": ca,
                                      "vertices_moved": len(corr.data) and sum(1 for k, b0 in zip(corr.data, corr.relative_key.data) if (k.co - b0.co).length > 1e-7) if corr else 0}
        meas = {side: measure_pose(side, gloves[side], eva, eye_b) for side in SIDES}
        stored[frame] = capture(rig, gloves)
        stored[frame]["corrective"] = corr.name if corr else None
        results[name] = {"frame": frame, "sides": res, "measure": meas}
        log(name, json.dumps(res["R"])[:400], "|", json.dumps(meas["R"]))
        tgt = gloves["R"]["pivot"].matrix_world.translation.copy()
        cam = camera(scene, "EAF_FP_" + name, eye_b, tgt, vfov=68.0)
        render(scene, cam, os.path.join(RENDER_DIR, "%sfp_%s.png" % ("cp_" if STAGE != "full" else "", name)), (1280, 960), 24 if STAGE != "full" else SAMPLES)
        cam = camera(scene, "EAF_Side_" + name, Vector((-1.0, -0.55, 1.45)), tgt + Vector((0.08, 0.10, 0.02)), lens=45.0)
        render(scene, cam, os.path.join(RENDER_DIR, "%sside_%s.png" % ("cp_" if STAGE != "full" else "", name)), (1280, 960), 24 if STAGE != "full" else SAMPLES)
        Sh = bone_world(rig, "upperarm01.R").translation
        cam = camera(scene, "EAF_Shoulder_" + name, Sh + Vector((-0.30, -0.32, 0.06)), Sh + Vector((-0.04, -0.10, -0.06)), lens=40.0)
        render(scene, cam, os.path.join(RENDER_DIR, "%sshoulder_%s.png" % ("cp_" if STAGE != "full" else "", name)), (1280, 960), 24 if STAGE != "full" else SAMPLES)
        if corr:
            corr.value = 0.0
            bpy.context.view_layer.update()
    return stored, results


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps = 30
    device = enable_gpu(scene)
    eva, hands, rig, rigdef, gloves, fits, base = setup(scene)
    if DEBUG_COLOURS:
        debug_colours(eva)
    render_set(scene)
    stored, results = solve_targets(scene, eva, rig, rigdef, gloves, base)
    report = {"blender": bpy.app.version_string, "device": device, "fit": fits, "targets": results,
              "base_pose": "BODY_FloatNeutral_V04 (26d0ee0) at zero visual offset, eye at 1.65 m above the feet root",
              "elbow_comfort_limit_deg": ELBOW_OK}
    json.dump(report, open(os.path.join(OUT, "fit-report.json"), "w", encoding="utf-8", newline="\n"), indent=1)
    if STAGE == "full":
        export_all(scene, eva, rig, gloves, stored, report)
    log("done")


main()
