"""lookdown - WORK-LOOKDOWN-STUDY-01 (Blender 5.3): why the first-person look-down view of the WORK body shows mostly
shirt and no boots, and which minimal idle adjustment fixes it.

Inputs, read only: A7_BodyFit02_WORK.fbx (85b0994) and BODY_FloatNeutral_V04.fbx (26d0ee0, applied at zero offset).
Camera as documented (BODY-FIT-02 README, V04 checks): Blender (0, -0.330, 1.651) relative to the feet root, pitch
down 80 degrees, vertical FOV 68 degrees, 16:9, near clip 0.05 m. Only TorsoLegs is shown (arms off, as with the
separate camera hands).

Per pose the script reports the share of the screen covered by torso, hips, legs and boots (flat region colours,
no anti-aliasing, pixel count) and, for every boot vertex, whether it is inside the view and what the sight line from
the eye hits first. Variants change only spine, pelvis and leg bones; the feet stay planted (two-bone IK per leg onto
the unchanged ankle, foot rotation restored) and the eye and the player capsule never move.

Run: blender -b --factory-startup --python lookdown.py -- --body <WORK fbx> --v04 <V04 fbx> --out <dir> [--variants]
"""
import json
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector
from mathutils.bvhtree import BVHTree

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def arg(name, default=None):
    if name in ARGS:
        i = ARGS.index(name)
        return ARGS[i + 1] if i + 1 < len(ARGS) and not ARGS[i + 1].startswith("--") else True
    return default


BODY, V04, OUT = os.path.abspath(arg("--body")), os.path.abspath(arg("--v04")), os.path.abspath(arg("--out"))
EYE = Vector((0.0, 0.0, 1.65))     # V04 fixture: source feet + ViewPivot eye height 1.65 m, zero setback
PITCH_DOWN, VFOV, RES = 80.0, 68.0, (960, 540)
REGION_COLOURS = {"torso": (0.85, 0.20, 0.20), "hips": (0.95, 0.75, 0.10), "legs": (0.20, 0.55, 0.95),
                  "boots": (0.15, 0.80, 0.30)}


def log(*a):
    print("[look]", *a, flush=True)


def clear():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)


def load():
    clear()
    bpy.ops.import_scene.fbx(filepath=BODY)
    rig = bpy.data.objects["A7_ContinuousBody_Rig"]
    rig.animation_data_clear()
    before_actions = set(bpy.data.actions)
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=V04)
    new_objs = set(bpy.data.objects) - before
    acts = [a for a in bpy.data.actions if a not in before_actions]
    act = next(a for a in acts if "Float" in a.name) if any("Float" in a.name for a in acts) else acts[0]
    rig.animation_data_create()
    rig.animation_data.action = act
    if hasattr(rig.animation_data, "action_slot") and len(act.slots):
        rig.animation_data.action_slot = act.slots[0]
    bpy.context.scene.frame_set(1)
    for o in new_objs:
        bpy.data.objects.remove(o, do_unlink=True)
    # freeze the V04 pose as plain pose-bone values, then drop the action so variants can edit bones
    for pb in rig.pose.bones:
        pb.rotation_mode = "QUATERNION"
    bpy.context.view_layer.update()
    frozen = {pb.name: (pb.location.copy(), pb.rotation_quaternion.copy(), pb.scale.copy()) for pb in rig.pose.bones}
    rig.animation_data.action = None
    for pb in rig.pose.bones:
        pb.location, pb.rotation_quaternion, pb.scale = frozen[pb.name]
    bpy.context.view_layer.update()
    torso = bpy.data.objects["A7_WORK_TorsoLegs"]
    for side in ("L", "R"):
        bpy.data.objects["A7_WORK_Arm_" + side].hide_render = True
    log("V04 action", act.name, "CharacterRoot head", tuple(round(c, 4) for c in rig.pose.bones["CharacterRoot"].head))
    return rig, torso, frozen, act.name


def regions(torso):
    """Region per face: boot material first, then the dominant bone of the face's vertices."""
    names = [g.name for g in torso.vertex_groups]
    dom = []
    for v in torso.data.vertices:
        best = max(v.groups, key=lambda g: g.weight, default=None)
        dom.append(names[best.group] if best is not None else "")
    mats = [s.material.name if s.material else "" for s in torso.material_slots]
    out = []
    for f in torso.data.polygons:
        if "Boot" in mats[f.material_index]:
            out.append("boots")
            continue
        votes = {}
        for vi in f.vertices:
            b = dom[vi]
            r = ("boots" if ("foot" in b or "toe" in b) else "legs" if ("upperleg" in b or "lowerleg" in b)
                 else "hips" if (b.startswith("pelvis") or b == "root") else "torso")
            votes[r] = votes.get(r, 0) + 1
        out.append(max(votes, key=votes.get))
    return out


def camera():
    cam = bpy.data.objects.new("EyeCam", bpy.data.cameras.new("EyeCam"))
    cam.data.sensor_fit = "VERTICAL"
    cam.data.angle_y = math.radians(VFOV)
    cam.data.clip_start = 0.05
    cam.location = EYE
    cam.rotation_euler = (math.radians(90.0 - PITCH_DOWN), 0.0, math.radians(180.0))
    bpy.context.scene.collection.objects.link(cam)
    return cam


def evaluated(obj):
    dg = bpy.context.evaluated_depsgraph_get()
    ev = obj.evaluated_get(dg)
    me = ev.to_mesh()
    me.calc_loop_triangles()
    co = np.zeros(len(me.vertices) * 3)
    me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    mw = np.array(obj.matrix_world)
    co = (mw[:3, :3] @ co.T).T + mw[:3, 3]
    tris = np.zeros(len(me.loop_triangles) * 3, dtype=np.int32)
    me.loop_triangles.foreach_get("vertices", tris)
    poly = np.zeros(len(me.loop_triangles), dtype=np.int32)
    me.loop_triangles.foreach_get("polygon_index", poly)
    ev.to_mesh_clear()
    return co, tris.reshape(-1, 3), poly


def sight(torso, face_region, cam):
    """For every boot vertex: inside the view? what does the eye ray hit first?"""
    co, tris, poly = evaluated(torso)
    tree = BVHTree.FromPolygons([tuple(v) for v in co], [tuple(t) for t in tris])
    tri_region = [face_region[p] for p in poly]
    vreg = {}
    for t, r in zip(tris, tri_region):
        for vi in t:
            vreg.setdefault(int(vi), r)
    boots = [i for i, r in vreg.items() if r == "boots"]
    cm = cam.matrix_world
    inv = cm.inverted()
    half_v = math.radians(VFOV) / 2
    half_h = math.atan(math.tan(half_v) * RES[0] / RES[1])
    stats = {"boot_vertices": len(boots), "outside_view": 0, "visible": 0, "hidden_by": {}}
    for i in boots:
        p = Vector(co[i])
        local = inv @ p
        if local.z >= 0 or abs(math.atan2(local.x, -local.z)) > half_h or abs(math.atan2(local.y, -local.z)) > half_v:
            stats["outside_view"] += 1
            continue
        d = (p - EYE)
        dist = d.length
        hit = tree.ray_cast(EYE, d.normalized(), dist - 0.002)
        if hit[0] is None:
            stats["visible"] += 1
        else:
            r = tri_region[hit[2]]
            if r == "boots":
                stats["visible"] += 1
            else:
                stats["hidden_by"][r] = stats["hidden_by"].get(r, 0) + 1
    # where the boots sit relative to the view: angle below the horizon from the eye
    bz = co[boots]
    ang = [math.degrees(math.atan2(-(v[2] - EYE.z), -(v[1] - EYE.y))) for v in bz]
    stats["boots_angle_below_horizon_deg"] = [round(min(ang), 1), round(max(ang), 1)]
    stats["view_spans_deg_below_horizon"] = [round(PITCH_DOWN - VFOV / 2, 1), round(PITCH_DOWN + VFOV / 2, 1)]
    return stats


def mask_render(torso, face_region, path):
    """Flat region colours, no anti-aliasing; returns the share of the screen per region."""
    scene = bpy.context.scene
    saved = [s.material for s in torso.material_slots]
    mats = {}
    for r, c in REGION_COLOURS.items():
        m = bpy.data.materials.get("MASK_" + r) or bpy.data.materials.new("MASK_" + r)
        m.diffuse_color = (*c, 1.0)
        mats[r] = m
    order = list(REGION_COLOURS)
    me = torso.data
    saved_idx = [f.material_index for f in me.polygons]
    me.materials.clear()
    for r in order:
        me.materials.append(mats[r])
    for f, r in zip(me.polygons, face_region):
        f.material_index = order.index(r)
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "FLAT"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.render_aa = "OFF"
    scene.render.resolution_x, scene.render.resolution_y = RES
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.view_transform = "Standard"
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    img = bpy.data.images.load(path, check_existing=False)
    px = np.array(img.pixels[:]).reshape(-1, 4)
    bpy.data.images.remove(img)
    me.materials.clear()
    for m in saved:
        me.materials.append(m)
    for f, i in zip(me.polygons, saved_idx):
        f.material_index = i
    total = len(px)
    share = {"empty": round(float(np.sum(px[:, 3] < 0.5)) / total * 100, 2)}
    lin = lambda c: np.array([x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c])
    cols = {r: np.array(c) for r, c in REGION_COLOURS.items()}
    body = px[px[:, 3] >= 0.5][:, :3]
    if len(body):
        d = np.stack([np.linalg.norm(body - c, axis=1) for c in cols.values()], axis=1)
        k = np.argmin(d, axis=1)
        for j, r in enumerate(cols):
            share[r] = round(float(np.sum(k == j)) / total * 100, 2)
    return share


def shaded_render(path, size=RES):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.render_aa = "8"
    scene.render.film_transparent = False
    scene.render.resolution_x, scene.render.resolution_y = size
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    rig, torso, frozen, act_name = load()
    face_region = regions(torso)
    counts = {r: face_region.count(r) for r in REGION_COLOURS}
    log("faces per region", counts, "torso groups", len(torso.vertex_groups))
    cam = camera()
    bpy.context.scene.camera = cam
    st = sight(torso, face_region, cam)
    log("baseline sight", json.dumps(st))
    share = mask_render(torso, face_region, os.path.join(OUT, "baseline_eye_mask.png"))
    log("baseline coverage %", json.dumps(share))
    shaded_render(os.path.join(OUT, "baseline_eye.png"))
    json.dump({"action": act_name, "sight": st, "coverage_percent": share, "faces": counts},
              open(os.path.join(OUT, "baseline.json"), "w"), indent=1)


# ----------------------------------------------------------------------------- variants (feet planted, eye fixed)
LEG = ("upperleg01", "upperleg02", "lowerleg01", "lowerleg02")
SPINE = ("spine05", "spine04", "spine03", "spine02", "spine01")


def snapshot(rig):
    bpy.context.view_layer.update()
    return {pb.name: pb.matrix.copy() for pb in rig.pose.bones}


def set_variant(rig, frozen, base, root_dy=0.0, root_dz=0.0, spine_ext_deg=0.0):
    """Start from the frozen V04 pose; move the root (hips) by dy, dz; extend the spine; legs reach the unchanged
    ankles (IK) and the feet keep their V04 world rotation."""
    for pb in rig.pose.bones:
        pb.location, pb.rotation_quaternion, pb.scale = frozen[pb.name]
        for c in list(pb.constraints):
            pb.constraints.remove(c)
    bpy.context.view_layer.update()
    root = rig.pose.bones["root"]
    m = base["root"].copy()
    m.translation = m.translation + Vector((0.0, root_dy, root_dz))
    root.matrix = m
    bpy.context.view_layer.update()
    # spine extension about the body's lateral axis, spread over the five spine bones
    for name in SPINE:
        pb = rig.pose.bones[name]
        axis = Vector((1.0, 0.0, 0.0))                        # +X = anatomical left; a negative angle leans back
        rot = Matrix.Rotation(-math.radians(spine_ext_deg / len(SPINE)), 4, axis)
        mm = pb.matrix.copy()
        loc = mm.translation.copy()
        mm = rot @ mm
        mm.translation = loc
        pb.matrix = mm
        bpy.context.view_layer.update()
    for side in ("L", "R"):
        target = bpy.data.objects.get("IK_" + side) or bpy.data.objects.new("IK_" + side, None)
        if target.name not in bpy.context.scene.collection.objects:
            bpy.context.scene.collection.objects.link(target)
        foot = base["foot." + side]
        target.matrix_world = rig.matrix_world @ foot
        ik = rig.pose.bones["lowerleg02." + side].constraints.new("IK")
        ik.target, ik.chain_count = target, 4                 # no pole: the knee keeps its own plane
        cr = rig.pose.bones["foot." + side].constraints.new("COPY_ROTATION")
        cr.target = target
    bpy.context.view_layer.update()


def joint_report(rig, base):
    bpy.context.view_layer.update()
    out = {}
    for side in ("L", "R"):
        P = {n: rig.pose.bones[n + "." + side] for n in ("upperleg01", "lowerleg01", "foot")}
        thigh = (P["lowerleg01"].head - P["upperleg01"].head).normalized()
        shin = (rig.pose.bones["foot." + side].head - P["lowerleg01"].head).normalized()
        out["knee_flexion_deg_" + side] = round(math.degrees(thigh.angle(shin)), 1)
        out["thigh_forward_of_vertical_deg_" + side] = round(math.degrees(math.atan2(-thigh.y, -thigh.z)), 1)
        out["foot_drift_mm_" + side] = round((rig.pose.bones["foot." + side].head - base["foot." + side].translation).length * 1000, 2)
    spine = (rig.pose.bones["spine01"].head - rig.pose.bones["spine05"].head).normalized()
    out["trunk_back_of_vertical_deg"] = round(math.degrees(math.atan2(spine.y, spine.z)), 1)
    out["root_shift_mm"] = [round(c * 1000, 1) for c in (rig.pose.bones["root"].head - base["root"].translation)]
    head = rig.pose.bones.get("head")
    if head:
        out["body_head_to_eye_mm"] = round((head.head - EYE).length * 1000, 1)
    return out


def side_render(torso, face_region, path):
    scene = bpy.context.scene
    cam = bpy.data.objects.get("SideCam") or bpy.data.objects.new("SideCam", bpy.data.cameras.new("SideCam"))
    if cam.name not in scene.collection.objects:
        scene.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 3.9
    cam.location = Vector((3.0, -0.35, 0.95))
    cam.rotation_euler = (math.radians(90), 0, math.radians(90))
    rays = []
    for name, ang in (("top", PITCH_DOWN - VFOV / 2), ("centre", PITCH_DOWN), ("bottom", PITCH_DOWN + VFOV / 2)):
        d = Vector((0.0, -math.cos(math.radians(ang)), -math.sin(math.radians(ang))))
        length = 2.2
        bpy.ops.mesh.primitive_cylinder_add(radius=0.004, depth=length, location=EYE + d * (length / 2))
        o = bpy.context.active_object
        o.rotation_mode = "QUATERNION"
        o.rotation_quaternion = d.to_track_quat("Z", "Y")
        m = bpy.data.materials.get("RAY") or bpy.data.materials.new("RAY")
        m.diffuse_color = (0.1, 0.1, 0.1, 1)
        o.data.materials.append(m)
        rays.append(o)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.02, location=EYE)
    eye = bpy.context.active_object
    rays.append(eye)
    old = scene.camera
    scene.camera = cam
    mask_render(torso, face_region, path)
    scene.camera = old
    for o in rays:
        bpy.data.objects.remove(o, do_unlink=True)


def eye_clearance(torso):
    co, tris, _ = evaluated(torso)
    tree = BVHTree.FromPolygons([tuple(v) for v in co], [tuple(t) for t in tris])
    hit = tree.find_nearest(EYE, 1.0)
    return round(hit[3] * 1000, 1) if hit[0] is not None else None


if __name__ == "__main__" and "--variants" in ARGS:
    rig, torso, frozen, act_name = load()
    face_region = regions(torso)
    cam = camera()
    bpy.context.scene.camera = cam
    base = snapshot(rig)
    grid = [(dz, dy, ext) for dz in (0.0, 0.04, 0.08, 0.12) for dy in (0.0, 0.04, 0.08, 0.12) for ext in (0.0, 6.0, 12.0)]
    report = {"action": act_name, "eye": list(EYE), "pitch_down_deg": PITCH_DOWN, "vfov_deg": VFOV, "variants": {}}
    for dz, dy, ext in grid:
        name = "up%03d_back%03d_lean%02d" % (round(dz * 1000), round(dy * 1000), ext)
        if dz == dy == ext == 0.0:
            for pb in rig.pose.bones:
                pb.location, pb.rotation_quaternion, pb.scale = frozen[pb.name]
                for c in list(pb.constraints):
                    pb.constraints.remove(c)
        else:
            set_variant(rig, frozen, base, root_dy=dy, root_dz=dz, spine_ext_deg=ext)
        bpy.context.view_layer.update()
        st = sight(torso, face_region, cam)
        share = mask_render(torso, face_region, os.path.join(OUT, "grid_%s_mask.png" % name))
        jr = joint_report(rig, base)
        jr["eye_to_nearest_body_skin_mm"] = eye_clearance(torso)
        report["variants"][name] = {"params": {"hips_up_m": dz, "hips_back_m": dy, "trunk_lean_back_deg": ext},
                                    "coverage_percent": share, "boots_sight": st, "joints": jr}
        log(name, "boots %.2f%% visible %d/%d" % (share.get("boots", 0), st["visible"], st["boot_vertices"]),
            "torso %.1f legs %.1f hips %.2f" % (share.get("torso", 0), share.get("legs", 0), share.get("hips", 0)),
            "knee %.0f/%.0f trunk %.1f eye-skin %s" % (jr["knee_flexion_deg_L"], jr["knee_flexion_deg_R"],
                                                       jr["trunk_back_of_vertical_deg"], jr["eye_to_nearest_body_skin_mm"]),
            "drift %.1f/%.1f" % (jr["foot_drift_mm_L"], jr["foot_drift_mm_R"]))
    json.dump(report, open(os.path.join(OUT, "grid.json"), "w"), indent=1)


if __name__ == "__main__" and "--setbacks" in ARGS:
    rig, torso, frozen, act_name = load()
    face_region = regions(torso)
    cam = camera()
    bpy.context.scene.camera = cam
    base = snapshot(rig)
    if "--stand" in ARGS:
        poses = {"up080": dict(root_dz=0.08), "up120": dict(root_dz=0.12), "up080_back40": dict(root_dz=0.08, root_dy=0.04),
                 "up120_back40": dict(root_dz=0.12, root_dy=0.04), "up150_back40": dict(root_dz=0.15, root_dy=0.04)}
        setbacks = (0.197, 0.33)
    else:
        poses = {"V04": None, "hips_back40": dict(root_dy=0.04), "lean6": dict(spine_ext_deg=6.0),
                 "hips_back40_lean6": dict(root_dy=0.04, spine_ext_deg=6.0)}
        setbacks = (0.0, 0.10, 0.197, 0.33)
    report = {"action": act_name, "variants": {}}
    for setback in setbacks:
        EYE = Vector((0.0, -setback, 1.65))
        globals()["EYE"] = EYE
        cam.location = EYE
        for pname, kw in poses.items():
            if kw is None:
                for pb in rig.pose.bones:
                    pb.location, pb.rotation_quaternion, pb.scale = frozen[pb.name]
                    for c in list(pb.constraints):
                        pb.constraints.remove(c)
            else:
                set_variant(rig, frozen, base, **kw)
            bpy.context.view_layer.update()
            name = "setback%03d_%s" % (round(setback * 1000), pname)
            st = sight(torso, face_region, cam)
            share = mask_render(torso, face_region, os.path.join(OUT, "%s_mask.png" % name))
            jr = joint_report(rig, base)
            jr["eye_to_nearest_body_skin_mm"] = eye_clearance(torso)
            report["variants"][name] = {"visual_setback_m": setback, "pose": pname, "params": kw or {},
                                        "coverage_percent": share, "boots_sight": st, "joints": jr}
            log(name, "boots %.2f%% visible %d/%d hidden %s" % (share.get("boots", 0), st["visible"], st["boot_vertices"], json.dumps(st["hidden_by"])),
                "torso %.1f legs %.1f hips %.2f" % (share.get("torso", 0), share.get("legs", 0), share.get("hips", 0)),
                "boots at %s deg" % st["boots_angle_below_horizon_deg"], "eye-skin %s" % jr["eye_to_nearest_body_skin_mm"],
                "knee %.0f drift %.1f" % (jr["knee_flexion_deg_L"], jr["foot_drift_mm_L"]))
    json.dump(report, open(os.path.join(OUT, "stand.json" if "--stand" in ARGS else "setbacks.json"), "w"), indent=1)


if __name__ == "__main__" and "--final" in ARGS:
    rig, torso, frozen, act_name = load()
    face_region = regions(torso)
    cam = camera()
    bpy.context.scene.camera = cam
    base = snapshot(rig)
    cases = {"A_V04_zero_setback": (0.0, None),
             "B_stand_setback197": (0.197, dict(root_dz=0.15, root_dy=0.04)),
             "C_stand_setback330": (0.33, dict(root_dz=0.15, root_dy=0.04))}
    report = {"action": act_name, "cases": {}}
    for name, (setback, kw) in cases.items():
        globals()["EYE"] = Vector((0.0, -setback, 1.65))
        cam.location = globals()["EYE"]
        if kw is None:
            for pb in rig.pose.bones:
                pb.location, pb.rotation_quaternion, pb.scale = frozen[pb.name]
                for c in list(pb.constraints):
                    pb.constraints.remove(c)
        else:
            set_variant(rig, frozen, base, **kw)
        bpy.context.view_layer.update()
        st = sight(torso, face_region, cam)
        share = mask_render(torso, face_region, os.path.join(OUT, "%s_eye_regions.png" % name))
        shaded_render(os.path.join(OUT, "%s_eye.png" % name))
        side_render(torso, face_region, os.path.join(OUT, "%s_side.png" % name))
        jr = joint_report(rig, base)
        jr["eye_to_nearest_body_skin_mm"] = eye_clearance(torso)
        report["cases"][name] = {"visual_setback_m": setback, "pose_params": kw or {}, "coverage_percent": share,
                                 "boots_sight": st, "joints": jr}
        log(name, json.dumps(share), "boots visible %d/%d" % (st["visible"], st["boot_vertices"]), json.dumps(jr))
    json.dump(report, open(os.path.join(OUT, "cases.json"), "w"), indent=1)
