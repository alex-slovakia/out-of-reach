"""standing_idle - STANDING-IDLE-01 (Blender 5.3): a planted-feet standing idle for the exact 164-bone WORK/EVA rig,
exported as an animation-only FBX that keeps the original bind, plus same-eye look-down evidence.

Pose: the reviewed BODY_StandRelaxed (upper body, arms low) with the recommended lower-body change from
WORK-LOOKDOWN-STUDY-01: knees about 11 degrees and hips 40 mm back. The root bone moves back 40 mm and down until the
knees reach the target; each leg reaches its unchanged StandRelaxed foot by IK (chain of 4, no pole) and keeps the
foot's rotation; the visual result is baked into plain keys on all 164 bones (frames 1-31, 30 fps), constraints
removed. Bind matrices, names, lengths and hierarchy are the imported ones; nothing is applied to the rest pose.

Run: blender -b --factory-startup --python standing_idle.py -- --body <WORK fbx> --eva <EVA fbx> --v04 <V04 fbx>
     --out <dir> [--knee 11 --back 0.040]
"""
import json
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lookdown as ld  # noqa: E402

ARGS = sys.argv[sys.argv.index("--") + 1:]
arg = ld.arg
BODY, EVA, OUT = os.path.abspath(arg("--body")), os.path.abspath(arg("--eva")), os.path.abspath(arg("--out"))
KNEE, BACK = float(arg("--knee", 11.0)), float(arg("--back", 0.040))
CLIP = "BODY_StandIdle_V01"
FPS, LAST = 30, 31
log = ld.log


def import_body(path, prefix):
    ld.clear()
    bpy.ops.import_scene.fbx(filepath=path)
    rig = bpy.data.objects["A7_ContinuousBody_Rig"]
    torso = next(o for o in bpy.data.objects if o.type == "MESH" and o.name.endswith("TorsoLegs"))
    for o in bpy.data.objects:
        if o.type == "MESH" and "_Arm_" in o.name:
            o.hide_render = True
    return rig, torso


def take(rig, suffix):
    act = next(a for a in bpy.data.actions if a.name.endswith(suffix))
    rig.animation_data_create()
    rig.animation_data.action = act
    if hasattr(rig.animation_data, "action_slot") and len(act.slots):
        rig.animation_data.action_slot = act.slots[0]
    bpy.context.scene.frame_set(1)
    for pb in rig.pose.bones:
        pb.rotation_mode = "QUATERNION"
    bpy.context.view_layer.update()
    frozen = {pb.name: (pb.location.copy(), pb.rotation_quaternion.copy(), pb.scale.copy()) for pb in rig.pose.bones}
    rig.animation_data.action = None
    for pb in rig.pose.bones:
        pb.location, pb.rotation_quaternion, pb.scale = frozen[pb.name]
    bpy.context.view_layer.update()
    return frozen, act.name


def knees(rig):
    out = []
    for side in ("L", "R"):
        a = rig.pose.bones["upperleg01." + side].head
        k = rig.pose.bones["lowerleg01." + side].head
        f = rig.pose.bones["foot." + side].head
        out.append(math.degrees((k - a).normalized().angle((f - k).normalized())))
    return out


def author(rig, frozen):
    base = ld.snapshot(rig)
    k0 = knees(rig)
    lo, hi = 0.0, 0.12                                      # root down (m): bisection for the knee target
    for _ in range(20):
        mid = (lo + hi) / 2
        ld.set_variant(rig, frozen, base, root_dy=BACK, root_dz=-mid)
        if sum(knees(rig)) / 2 < KNEE:
            lo = mid
        else:
            hi = mid
    ld.set_variant(rig, frozen, base, root_dy=BACK, root_dz=-(lo + hi) / 2)
    bpy.context.view_layer.update()
    # bake the visual pose into plain local values, then drop the constraints
    local = {pb.name: rig.convert_space(pose_bone=pb, matrix=pb.matrix, from_space="POSE", to_space="LOCAL")
             for pb in rig.pose.bones}
    for pb in rig.pose.bones:
        for c in list(pb.constraints):
            pb.constraints.remove(c)
    for pb in rig.pose.bones:
        pb.matrix_basis = local[pb.name]
    bpy.context.view_layer.update()
    drift = {s: round((rig.pose.bones["foot." + s].head - base["foot." + s].translation).length * 1000, 3) for s in ("L", "R")}
    rot = {s: round(math.degrees((rig.pose.bones["foot." + s].matrix.to_quaternion().rotation_difference(
        base["foot." + s].to_quaternion())).angle), 3) for s in ("L", "R")}
    info = {"knees_before_deg": [round(x, 2) for x in k0], "knees_after_deg": [round(x, 2) for x in knees(rig)],
            "root_shift_mm": [round(c * 1000, 1) for c in (rig.pose.bones["root"].head - base["root"].translation)],
            "foot_drift_mm": drift, "foot_rotation_change_deg": rot}
    log("authored", json.dumps(info))
    return info


REST_FRAME = 100          # a rest key outside the take: the exporter writes the static bone nodes from this frame


def key_clip(rig):
    """The idle on frames 0..30 (imported as 1..31, like the published takes); the bind pose keyed at REST_FRAME so
    the FBX's bone nodes stay the original bind and only the take carries the idle."""
    act = bpy.data.actions.new(CLIP)
    rig.animation_data_create()
    rig.animation_data.action = act
    idle = {pb.name: (pb.location.copy(), pb.rotation_quaternion.copy(), pb.scale.copy()) for pb in rig.pose.bones}
    for frame in (0, LAST - 1):
        for pb in rig.pose.bones:
            pb.location, pb.rotation_quaternion, pb.scale = idle[pb.name]
            pb.keyframe_insert("location", frame=frame)
            pb.keyframe_insert("rotation_quaternion", frame=frame)
            pb.keyframe_insert("scale", frame=frame)
    for pb in rig.pose.bones:
        pb.location, pb.rotation_quaternion, pb.scale = (0, 0, 0), (1, 0, 0, 0), (1, 1, 1)
        pb.keyframe_insert("location", frame=REST_FRAME)
        pb.keyframe_insert("rotation_quaternion", frame=REST_FRAME)
        pb.keyframe_insert("scale", frame=REST_FRAME)
    scene = bpy.context.scene
    scene.frame_start, scene.frame_end = 0, LAST - 1
    scene.render.fps = FPS
    scene.name = CLIP                                      # the FBX take is named after the scene
    scene.frame_set(0)
    return act


def export(rig, path):
    for o in bpy.data.objects:
        o.select_set(o == rig)
    bpy.context.view_layer.objects.active = rig
    bpy.context.scene.frame_set(REST_FRAME)              # bone nodes = the original bind
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"ARMATURE"}, add_leaf_bones=False,
                             bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
                             bake_anim_use_all_bones=True, bake_anim_force_startend_keying=True,
                             bake_anim_simplify_factor=0.0, axis_forward="-Z", axis_up="Y",
                             primary_bone_axis="Y", secondary_bone_axis="X", global_scale=1.0,
                             apply_unit_scale=True, use_armature_deform_only=False)


def evidence(rig, torso, outfit, tag, report):
    face_region = ld.regions(torso)
    cam = ld.camera()
    bpy.context.scene.camera = cam
    for setback in (0.33, 0.197):
        ld.EYE = Vector((0.0, -setback, 1.65))
        cam.location = ld.EYE
        bpy.context.view_layer.update()
        name = "%s_%s_setback%03d" % (outfit, tag, round(setback * 1000))
        st = ld.sight(torso, face_region, cam)
        share = ld.mask_render(torso, face_region, os.path.join(OUT, "renders", name + "_eye_regions.png"))
        ld.shaded_render(os.path.join(OUT, "renders", name + "_eye.png"))
        ld.side_render(torso, face_region, os.path.join(OUT, "renders", name + "_side.png"))
        clear = ld.eye_clearance(torso)
        report[name] = {"outfit": outfit, "pose": tag, "visual_setback_m": setback, "coverage_percent": share,
                        "boots_sight": st, "eye_to_nearest_skin_mm": clear, "knees_deg": [round(x, 2) for x in knees(rig)]}
        log(name, json.dumps(share), "boots %d/%d" % (st["visible"], st["boot_vertices"]), "eye-skin", clear)
    bpy.data.objects.remove(cam, do_unlink=True)


if __name__ == "__main__":
    os.makedirs(os.path.join(OUT, "renders"), exist_ok=True)
    report = {"clip": CLIP, "target": {"knee_deg": KNEE, "hips_back_m": BACK}, "evidence": {}}
    # 1. author on WORK from BODY_StandRelaxed, bake, export the animation-only FBX
    rig, torso = import_body(BODY, "WORK")
    frozen, src = take(rig, "BODY_StandRelaxed")
    report["source_take"] = src
    evidence(rig, torso, "WORK", "StandRelaxed", report["evidence"])     # the current standing pose, for reference
    report["authoring"] = author(rig, frozen)
    pose = {pb.name: [list(pb.head), list(pb.tail)] for pb in rig.pose.bones}
    json.dump(pose, open(os.path.join(OUT, "idle_pose_bone_heads_tails.json"), "w"))
    key_clip(rig)
    fbx = os.path.join(OUT, CLIP + ".fbx")
    export(rig, fbx)
    log("exported", fbx)
    bpy.context.scene.frame_set(0)
    evidence(rig, torso, "WORK", "StandIdle", report["evidence"])
    # 2. the same clip on EVA (same 164-bone rig)
    act = bpy.data.actions[CLIP]
    rig, torso = import_body(EVA, "EVA")
    rig.animation_data_clear()
    rig.animation_data_create()
    rig.animation_data.action = act
    if hasattr(rig.animation_data, "action_slot") and len(act.slots):
        rig.animation_data.action_slot = act.slots[0]
    bpy.context.scene.frame_set(0)
    evidence(rig, torso, "EVA", "StandIdle", report["evidence"])
    json.dump(report, open(os.path.join(OUT, "standing_idle_report.json"), "w"), indent=1)
    log("done")
