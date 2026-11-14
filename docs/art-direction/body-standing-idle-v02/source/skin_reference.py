"""skin_reference - STANDING-IDLE-UNIT-02 (Blender 5.3): the reference skin for the native Unity check.

For the canonical WORK and EVA bodies (read only) it evaluates every skinned mesh (TorsoLegs, Arm_L, Arm_R) through
Blender's own armature deform in three states and writes the world vertices in the Unity basis (-x, z, -y):
  Rest          the rest/bind pose,
  StandRelaxed  the published BODY_StandRelaxed take of the same file,
  V02           the exported animation-only BODY_StandIdle_V02.fbx (re-imported from the file, not the authoring
                scene) applied to the canonical rig.
Plus the posed joints (bone heads) of each state and the look-down region of every TorsoLegs vertex (the
STANDING-IDLE-01 definition from lookdown.regions: boot material first, then the dominant bone).
Writes <out>/<OUTFIT>_<state>_<mesh>.npy, <OUTFIT>_TorsoLegs_regions.npy, <OUTFIT>_<state>_bones.json, index.json.

Run: blender -b --factory-startup --python skin_reference.py -- --body <WORK fbx> --eva <EVA fbx> --v04 <V04 fbx>
     --idle <BODY_StandIdle_V02.fbx> --out <dir>
"""
import json
import os
import sys

import bpy
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lookdown as ld  # noqa: E402

BODY, EVA, IDLE, OUT = (os.path.abspath(ld.arg(k)) for k in ("--body", "--eva", "--idle", "--out"))
REGION_CODE = {"torso": 0, "hips": 1, "legs": 2, "boots": 3}
to_unity = lambda co: np.stack([-co[:, 0], co[:, 2], -co[:, 1]], axis=1)


def set_action(rig, act):
    rig.animation_data_create()
    rig.animation_data.action = act
    if hasattr(rig.animation_data, "action_slot") and len(act.slots):
        rig.animation_data.action_slot = act.slots[0]
    bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()


def dump(outfit, state, rig, meshes, index):
    for o in meshes:
        co, tris, _ = ld.evaluated(o)
        u = to_unity(co).astype(np.float32)
        np.save(os.path.join(OUT, "%s_%s_%s.npy" % (outfit, state, o.name)), u)
        index.setdefault(outfit, {}).setdefault(o.name, {})[state] = {
            "vertices": int(len(u)), "min": u.min(axis=0).round(6).tolist(), "max": u.max(axis=0).round(6).tolist()}
    mw = rig.matrix_world
    bones = {pb.name: [round(-v[0], 6), round(v[2], 6), round(-v[1], 6)] for pb in rig.pose.bones for v in [mw @ pb.head]}
    json.dump(bones, open(os.path.join(OUT, "%s_%s_bones.json" % (outfit, state)), "w"))


def vertex_regions(torso):
    face = ld.regions(torso)
    out = {}
    for f, r in zip(torso.data.polygons, face):
        for vi in f.vertices:
            out.setdefault(vi, REGION_CODE[r])
    return np.array([out.get(i, 0) for i in range(len(torso.data.vertices))], dtype=np.int8)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    index = {"basis": "Unity world = (-x, z, -y) of Blender world; model at the origin, feet root at 0",
             "regions": REGION_CODE}
    for outfit, path in (("WORK", BODY), ("EVA", EVA)):
        ld.clear()
        before_acts = set(bpy.data.actions)
        bpy.ops.import_scene.fbx(filepath=path)
        rig = bpy.data.objects["A7_ContinuousBody_Rig"]
        own = [a for a in bpy.data.actions if a not in before_acts]
        meshes = sorted((o for o in bpy.data.objects if o.type == "MESH"), key=lambda o: o.name)
        rig.animation_data_clear()
        rig.data.pose_position = "REST"
        bpy.context.view_layer.update()
        dump(outfit, "Rest", rig, meshes, index)
        rig.data.pose_position = "POSE"
        relaxed = next(a for a in own if "BODY_StandRelaxed" in a.name)   # the second file's actions get .001
        set_action(rig, relaxed)
        dump(outfit, "StandRelaxed", rig, meshes, index)
        before_objs, before_acts = set(bpy.data.objects), set(bpy.data.actions)
        bpy.ops.import_scene.fbx(filepath=IDLE)
        idle_act = next(a for a in bpy.data.actions if a not in before_acts)
        for o in set(bpy.data.objects) - before_objs:
            bpy.data.objects.remove(o, do_unlink=True)
        set_action(rig, idle_act)
        dump(outfit, "V02", rig, meshes, index)
        torso = next(o for o in meshes if o.name.endswith("TorsoLegs"))
        reg = vertex_regions(torso)
        np.save(os.path.join(OUT, "%s_TorsoLegs_regions.npy" % outfit), reg)
        index[outfit]["actions"] = {"StandRelaxed": relaxed.name, "V02": idle_act.name}
        index[outfit]["boot_vertices"] = int((reg == REGION_CODE["boots"]).sum())
        ld.log(outfit, "meshes", [o.name for o in meshes], "boots", index[outfit]["boot_vertices"])
    json.dump(index, open(os.path.join(OUT, "index.json"), "w"), indent=1)
    ld.log("reference done")
