"""verify_idle - STANDING-IDLE-UNIT-02: a fresh Blender check of the exported animation-only FBX.

1. Its armature against the published WORK body's: same 164 bone names, parents, lengths and bind (rest) matrices.
2. Its clip on the published WORK rig: posed bone heads and tails against the authored pose, and against the
   delivered STANDING-IDLE-01 joint expectations (the approved pose must not move).
3. Unit representation (raw FBX nodes, Blender's own parser, no import) against the published WORK body and V04:
   FBX UnitScaleFactor, axes, the A7_ContinuousBody_Rig node's static and keyed Lcl Scaling and Lcl Rotation, and the
   keyed local translations of the bones in metres.
Writes <out>/idle_fbx_check.json and <out>/idle_joint_expectations_unity.json.
Run: blender -b --factory-startup --python verify_idle.py -- --body <WORK fbx> --v04 <V04 fbx>
     --idle <BODY_StandIdle_V02.fbx> --pose <idle_pose_bone_heads_tails.json>
     --expect <v01 idle_joint_expectations_unity.json> --out <dir>
"""
import json
import os
import sys

import bpy
from io_scene_fbx import parse_fbx
from mathutils import Vector

ARGS = sys.argv[sys.argv.index("--") + 1:]
get = lambda k: ARGS[ARGS.index(k) + 1]
BODY, V04, IDLE, POSE, EXPECT, OUT = (get("--body"), get("--v04"), get("--idle"), get("--pose"), get("--expect"),
                                      get("--out"))
WATCH = ("A7_ContinuousBody_Rig", "root", "spine05", "upperleg01.L", "lowerleg01.L", "foot.L", "head")


# ----------------------------------------------------------------------------- 3. raw FBX nodes
def _s(v):
    if isinstance(v, bytes):
        return v.decode("utf-8", "replace").replace("\x00\x01", "::")
    return v


def _child(e, name):
    return next((c for c in e.elems if c.id == name), None)


def _p70(e):
    p = _child(e, b"Properties70")
    return {} if p is None else {_s(c.props[0]): [_s(x) for x in c.props[4:]] for c in p.elems}


def fbx_nodes(path):
    root, _ = parse_fbx.parse(path)
    g = _p70(_child(root, b"GlobalSettings"))
    out = {"UnitScaleFactor": g["UnitScaleFactor"][0], "OriginalUnitScaleFactor": g["OriginalUnitScaleFactor"][0],
           "axes": {k: g[k][0] for k in ("UpAxis", "UpAxisSign", "FrontAxis", "FrontAxisSign", "CoordAxis",
                                         "CoordAxisSign")},
           "takes": [_s(t.props[0]) for t in _child(root, b"Takes").elems if t.id == b"Take"], "nodes": {}}
    objs = _child(root, b"Objects")
    models, cnodes, curves = {}, {}, {}
    for o in objs.elems:
        if o.id == b"Model":
            models[o.props[0]] = (_s(o.props[1]).split("::")[0], _p70(o))
        elif o.id == b"AnimationCurveNode":
            cnodes[o.props[0]] = True
        elif o.id == b"AnimationCurve":
            kv = _child(o, b"KeyValueFloat")
            curves[o.props[0]] = list(kv.props[0]) if kv else []
    target, chans = {}, {}
    for c in _child(root, b"Connections").elems:
        if _s(c.props[0]) == "OP":
            a, b, prop = c.props[1], c.props[2], _s(c.props[3])
            if a in cnodes and b in models:
                target[a] = (b, prop)
            elif a in curves and b in cnodes:
                chans.setdefault(b, {})[prop] = curves[a]
    out["model_count"] = len(models)
    for uid, (name, p) in models.items():
        if name not in WATCH:
            continue
        e = {k: [round(float(x), 7) for x in p[k]] for k in ("Lcl Translation", "Lcl Rotation", "Lcl Scaling") if k in p}
        for cn, (tgt, prop) in target.items():
            if tgt == uid:
                ch = chans.get(cn, {})
                e["keyed " + prop] = {
                    "first": [round(float(ch[k][0]), 7) for k in ("d|X", "d|Y", "d|Z") if k in ch and ch[k]],
                    "min": [round(float(min(ch[k])), 7) for k in ("d|X", "d|Y", "d|Z") if k in ch and ch[k]],
                    "max": [round(float(max(ch[k])), 7) for k in ("d|X", "d|Y", "d|Z") if k in ch and ch[k]],
                    "keys": len(ch.get("d|X", []))}
        out["nodes"][name] = e
    return out


def units_check():
    idle, v04, work = fbx_nodes(IDLE), fbx_nodes(V04), fbx_nodes(BODY)
    rig = idle["nodes"]["A7_ContinuousBody_Rig"]
    ref = v04["nodes"]["A7_ContinuousBody_Rig"]
    res = {"idle": idle, "v04": v04, "work_body": {k: work[k] for k in ("UnitScaleFactor", "OriginalUnitScaleFactor",
                                                                         "axes", "takes")}}
    res["work_body"]["rig"] = work["nodes"]["A7_ContinuousBody_Rig"]
    keyed_scale = rig["keyed Lcl Scaling"]
    res["checks"] = {
        "unit_scale_factor_equals_v04_and_work": idle["UnitScaleFactor"] == v04["UnitScaleFactor"] == work["UnitScaleFactor"] == 100.0,
        "axes_equal_v04": idle["axes"] == v04["axes"],
        "rig_static_scale_is_one": all(abs(x - 1.0) < 1e-6 for x in rig["Lcl Scaling"]),
        "rig_keyed_scale_is_one_on_every_key": all(abs(x - 1.0) < 1e-6 for x in keyed_scale["min"] + keyed_scale["max"]),
        "rig_static_rotation_equals_v04_within_1e-4_deg": all(abs(a - b) < 1e-4 for a, b in zip(rig["Lcl Rotation"], ref["Lcl Rotation"])),
        "rig_static_translation_is_zero": all(abs(x) < 1e-9 for x in rig["Lcl Translation"]),
        "root_rest_translation_equals_v04_m": all(abs(a - b) < 1e-6 for a, b in zip(
            idle["nodes"]["root"]["Lcl Translation"], v04["nodes"]["root"]["Lcl Translation"])),
        "keyed_root_translation_m": idle["nodes"]["root"]["keyed Lcl Translation"]["first"],
        "keyed_upperleg01_L_translation_m": idle["nodes"]["upperleg01.L"]["keyed Lcl Translation"]["first"],
    }
    return res


# ----------------------------------------------------------------------------- 1.-2. import check
for o in list(bpy.data.objects):
    bpy.data.objects.remove(o, do_unlink=True)
bpy.ops.import_scene.fbx(filepath=BODY)
body = bpy.data.objects["A7_ContinuousBody_Rig"]
body.animation_data_clear()
before_objs, before_acts = set(bpy.data.objects), set(bpy.data.actions)
bpy.ops.import_scene.fbx(filepath=IDLE)
new_objs = set(bpy.data.objects) - before_objs
idle = next(o for o in new_objs if o.type == "ARMATURE")
acts = [a for a in bpy.data.actions if a not in before_acts]
report = {"idle_objects": sorted((o.name, o.type) for o in new_objs), "idle_actions": [a.name for a in acts],
          "idle_object_scale_after_blender_import": [round(x, 6) for x in idle.scale],
          "body_object_scale_after_blender_import": [round(x, 6) for x in body.scale]}
A, B = body.data.bones, idle.data.bones
report["bone_count"] = [len(A), len(B)]
report["names_equal"] = sorted(b.name for b in A) == sorted(b.name for b in B)
report["parents_equal"] = all((A[b.name].parent.name if A[b.name].parent else None) ==
                              (b.parent.name if b.parent else None) for b in B)
report["max_length_diff_mm"] = round(max(abs(A[b.name].length - b.length) for b in B) * 1000, 6)
diff = 0.0
for b in B:
    ma, mb = A[b.name].matrix_local, b.matrix_local
    diff = max(diff, max(abs(ma[i][j] - mb[i][j]) for i in range(4) for j in range(4)))
report["max_bind_matrix_diff"] = diff
# the clip on the published rig
act = acts[0]
body.animation_data_create()
body.animation_data.action = act
if hasattr(body.animation_data, "action_slot") and len(act.slots):
    body.animation_data.action_slot = act.slots[0]
bpy.context.scene.frame_set(int(act.frame_range[0]))
bpy.context.view_layer.update()
pose = json.load(open(POSE))
dev = 0.0
for name, (h, t) in pose.items():
    pb = body.pose.bones[name]
    dev = max(dev, (pb.head - Vector(h)).length, (pb.tail - Vector(t)).length)
report["max_posed_joint_deviation_mm"] = round(dev * 1000, 4)
# the same posed joints in the Unity model basis, against the delivered v01 expectations (the approved pose)
to_u = lambda v: [round(-v[0], 6), round(v[2], 6), round(-v[1], 6)]
bones = {pb.name: {"head": to_u(pb.head), "tail": to_u(pb.tail)} for pb in body.pose.bones}
exp = json.load(open(EXPECT))["bones"]
dv = max(max((Vector(bones[n]["head"]) - Vector(e["head"])).length, (Vector(bones[n]["tail"]) - Vector(e["tail"])).length)
         for n, e in exp.items())
report["v01_expectation_bones"] = len(exp)
report["max_joint_deviation_from_v01_expectations_mm"] = round(dv * 1000, 4)
report["frame_range"] = list(act.frame_range)
fc = 0
for layer in getattr(act, "layers", []):
    for strip in layer.strips:
        for bag in strip.channelbags:
            fc += len(bag.fcurves)
report["fcurves"] = fc
report["units"] = units_check()
json.dump(report, open(os.path.join(OUT, "idle_fbx_check.json"), "w"), indent=1)
json.dump({"source": "BODY_StandIdle_V02 (STANDING-IDLE-UNIT-02) re-imported in Blender 5.3 and applied to the published WORK "
                     "rig (A7_BodyFit02_WORK.fbx); the pose is the approved STANDING-IDLE-01 pose",
           "basis": "(-x, z, -y) of Blender armature space = the Unity model basis root verified for this rig; posed bone "
                    "heads and tails; zero visual root (CharacterRoot at the feet)",
           "units": "metres", "tolerance_metres": 0.002, "bones": dict(sorted(bones.items()))},
          open(os.path.join(OUT, "idle_joint_expectations_unity.json"), "w"), indent=1)
print("[verify]", json.dumps({k: v for k, v in report.items() if k != "units"}))
print("[verify] units checks", json.dumps(report["units"]["checks"]))
