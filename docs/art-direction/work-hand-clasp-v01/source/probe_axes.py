"""probe_axes - WORK-HAND-CLASP-01 diagnostic: the finger rotations the body author baked into each take, as axis and
angle in armature space, next to the flexion axis derived from the rest geometry. Prints only.
Run: blender -b --factory-startup --python probe_axes.py -- <fbx>
"""
import sys

import bpy
import numpy as np

FBX = sys.argv[sys.argv.index("--") + 1]
for o in list(bpy.data.objects):
    bpy.data.objects.remove(o, do_unlink=True)
bpy.ops.import_scene.fbx(filepath=FBX)
rig = bpy.data.objects["A7_ContinuousBody_Rig"]
REST = {b.name: np.array(b.matrix_local) for b in rig.data.bones}


def unit(v):
    return v / np.linalg.norm(v)


def palm_normal(s):
    d = unit(REST["finger2-1" + s][:3, 3] - REST["wrist" + s][:3, 3])
    across = unit(REST["finger5-1" + s][:3, 3] - REST["finger2-1" + s][:3, 3])
    n = unit(np.cross(d, across))
    thumb_tip = REST["finger1-3" + s][:3, 3] + REST["finger1-3" + s][:3, 1] * rig.data.bones["finger1-3" + s].length
    index_tip = REST["finger2-3" + s][:3, 3] + REST["finger2-3" + s][:3, 1] * rig.data.bones["finger2-3" + s].length
    return n if np.dot(thumb_tip - index_tip, n) > 0 else -n


for act in bpy.data.actions:
    rig.animation_data.action = act
    if hasattr(rig.animation_data, "action_slot") and act.slots:
        rig.animation_data.action_slot = act.slots[0]
    bpy.context.scene.frame_set(1)
    print("TAKE", act.name.split("|")[-1])
    for s in (".R", ".L"):
        n = palm_normal(s)
        print("  side", s, "palm normal", np.round(n, 3).tolist())
        for f in ("finger1-1", "finger1-2", "finger1-3", "finger2-1", "finger2-2", "finger2-3", "finger3-1", "finger5-1", "metacarpal1", "wrist"):
            pb = rig.pose.bones[f + s]
            q = np.array(pb.rotation_quaternion) if pb.rotation_mode == "QUATERNION" else np.array(pb.rotation_euler.to_quaternion())
            ang = 2 * np.degrees(np.arccos(min(1.0, abs(q[0]))))
            axis_local = q[1:] / (np.linalg.norm(q[1:]) or 1.0) * np.sign(q[0] or 1.0)
            axis_arm = REST[f + s][:3, :3] @ axis_local
            d = REST[f + s][:3, 1]
            flex = unit(np.cross(d, n))
            print("   %-12s angle %6.1f  axis(arm) %s  dot(flex axis) %5.2f  mode %s" % (
                f, ang, np.round(axis_arm, 2).tolist(), float(np.dot(axis_arm, flex)), pb.rotation_mode))
