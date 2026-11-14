"""verify_idle - STANDING-IDLE-01: a fresh Blender check of the exported animation-only FBX.

1. Its armature against the published WORK body's: same 164 bone names, parents, lengths and bind (rest) matrices.
2. Its clip on the published WORK rig: posed bone heads and tails against the authored pose.
Writes <out>/idle_fbx_check.json.
Run: blender -b --factory-startup --python verify_idle.py -- --body <WORK fbx> --idle <BODY_StandIdle_V01.fbx>
     --pose <idle_pose_bone_heads_tails.json> --out <dir>
"""
import json
import os
import sys

import bpy

ARGS = sys.argv[sys.argv.index("--") + 1:]
get = lambda k: ARGS[ARGS.index(k) + 1]
BODY, IDLE, POSE, OUT = get("--body"), get("--idle"), get("--pose"), get("--out")

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
report = {"idle_objects": sorted((o.name, o.type) for o in new_objs), "idle_actions": [a.name for a in acts]}
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
    dev = max(dev, (pb.head - __import__("mathutils").Vector(h)).length, (pb.tail - __import__("mathutils").Vector(t)).length)
report["max_posed_joint_deviation_mm"] = round(dev * 1000, 4)
report["frame_range"] = list(act.frame_range)
fc = 0
for layer in getattr(act, "layers", []):
    for strip in layer.strips:
        for bag in strip.channelbags:
            fc += len(bag.fcurves)
report["fcurves"] = fc
json.dump(report, open(os.path.join(OUT, "idle_fbx_check.json"), "w"), indent=1)
print("[verify]", json.dumps(report))
