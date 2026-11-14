"""probe_pads - WORK-HAND-CLASP-01 diagnostic: which way each distal pad faces on the published skin.

For the distal segment of every digit, the skin vertices dominated by that bone are measured around the bone axis
(40-80 % of its length); the direction of the largest radial offset (the fleshy pulp) and the mean offset are
printed next to the palm normal and the bend of the published thumb. Prints only.
Run: blender -b --factory-startup --python probe_pads.py -- <fbx>
"""
import sys

import bpy
import numpy as np

FBX = sys.argv[sys.argv.index("--") + 1]
for o in list(bpy.data.objects):
    bpy.data.objects.remove(o, do_unlink=True)
bpy.ops.import_scene.fbx(filepath=FBX)
rig = bpy.data.objects["A7_ContinuousBody_Rig"]
rig.animation_data_clear()
R = {b.name: np.array(b.matrix_local) for b in rig.data.bones}
L = {b.name: b.length for b in rig.data.bones}


def unit(v):
    return v / np.linalg.norm(v)


for side in ("R", "L"):
    s = "." + side
    obj = bpy.data.objects["A7_WORK_Arm_" + side]
    names = [g.name for g in obj.vertex_groups]
    co = np.array([v.co[:] for v in obj.data.vertices])
    dom = np.array([names[max(v.groups, key=lambda g: g.weight).group] if len(v.groups) else "" for v in obj.data.vertices])
    d = unit(R["finger2-1" + s][:3, 3] - R["wrist" + s][:3, 3])
    across = unit(R["finger5-1" + s][:3, 3] - R["finger2-1" + s][:3, 3])
    n = unit(np.cross(d, across))
    tip = lambda b: R[b + s][:3, 3] + R[b + s][:3, 1] * L[b + s]
    if np.dot(tip("finger1-3") - tip("finger2-3"), n) < 0:
        n = -n
    print("SIDE", side, "palm n", np.round(n, 3).tolist())
    for b in ("finger1-3", "finger2-3", "finger3-3", "finger4-3", "finger5-3", "finger1-2"):
        bone = b + s
        head, ax = R[bone][:3, 3], R[bone][:3, 1]
        v = co[dom == bone]
        t = (v - head) @ ax
        v = v[(t > 0.4 * L[bone]) & (t < 0.8 * L[bone])]
        t = (v - head) @ ax
        off = v - head - np.outer(t, ax)
        r = np.linalg.norm(off, axis=1)
        far = unit(off[np.argmax(r)])
        mean = unit(off.mean(axis=0))
        # 8 sectors around the axis: radius per sector, to see the flat (nail) and the round (pulp) side
        e1 = unit(np.cross(ax, n)) if abs(np.dot(ax, n)) < 0.95 else unit(np.cross(ax, across))
        e2 = np.cross(ax, e1)
        ang = np.degrees(np.arctan2(off @ e2, off @ e1)) % 360
        sectors = [round(float(r[(ang >= k * 45) & (ang < (k + 1) * 45)].max() * 1000), 1) if np.any((ang >= k * 45) & (ang < (k + 1) * 45)) else None for k in range(8)]
        print("  %-10s verts %3d  far %s  mean %s  dot(far,n) %5.2f  sectors(mm, from e1=cross(ax,n) towards e2) %s" % (
            b, len(v), np.round(far, 2).tolist(), np.round(mean, 2).tolist(), float(np.dot(far, n)), sectors))
    print("  e2 for finger1-3 =", np.round(np.cross(R["finger1-3" + s][:3, 1], unit(np.cross(R["finger1-3" + s][:3, 1], n))), 2).tolist())
