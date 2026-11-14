"""Re-import AntennaRepairMotion.fbx into an empty Blender scene and compare the imported clip with motion-samples.json.

    blender -b --factory-startup --python verify_motion_fbx.py -- <AntennaRepairMotion.fbx> <motion-samples.json> <report.json>
Positions are compared in Unity metres (Unity = (-x, z, -y) of Blender)."""
import json, re, sys
import bpy
from mathutils import Vector
fbx, samples_path, out = sys.argv[sys.argv.index("--") + 1:][:3]
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.fps = 30
bpy.ops.import_scene.fbx(filepath=fbx, anim_offset=0.0)
samples = json.load(open(samples_path, encoding="utf-8"))
objs = {o.name: o for o in scene.objects}
def unity(v):
    return Vector((-v.x, v.z, -v.y))
cable = objs["DamagedCable"]
keys = [k.name for k in cable.data.shape_keys.key_blocks] if cable.data.shape_keys else []
worst = {"position_mm": 0.0, "where": "", "cable_weight": 0.0}
first = {}
moving = set()
for f in range(0, samples["frames"]):
    scene.frame_set(f)
    for o in scene.objects:
        m = o.matrix_world.copy()
        if o.name not in first:
            first[o.name] = m
        elif max(abs(m[i][j] - first[o.name][i][j]) for i in range(4) for j in range(4)) > 1e-7:
            moving.add(o.name)
    for name in ("ServiceWrench", "DamagedNut", "DamagedPlugBody"):
        want = Vector(samples["objects"][name][f]["position"])
        got = unity(objs[name].matrix_world.translation)
        d = (got - want).length * 1000
        if d > worst["position_mm"]:
            worst["position_mm"], worst["where"] = round(d, 4), "%s f%d" % (name, f)
    if "Seated" in keys:
        w = abs(cable.data.shape_keys.key_blocks["Seated"].value - samples["objects"]["DamagedCable"][f]["weight_0_1"])
        worst["cable_weight"] = max(worst["cable_weight"], round(w, 5))
raw = open(fbx, "rb").read()
paths = sorted(set(re.findall(rb"[A-Za-z]:[\/][ -~]{3,}", raw)))
report = {"file": fbx.replace(chr(92), "/").split("/")[-1], "objects": sorted(objs), "cable_shape_keys": keys,
          "animated_objects": sorted(o.name for o in scene.objects if o.animation_data and o.animation_data.action),
          "moving_objects": sorted(moving),
          "cable_key_animated": bool(cable.data.shape_keys and cable.data.shape_keys.animation_data and cable.data.shape_keys.animation_data.action),
          "triangles": sum(len(o.data.polygons) for o in scene.objects if o.type == "MESH"),
          "frames_compared": samples["frames"], "worst": worst,
          "cameras_lights": [o.name for o in scene.objects if o.type in ("CAMERA", "LIGHT")],
          "drive_letter_paths": [p.decode("ascii", "replace") for p in paths][:10]}
json.dump(report, open(out, "w", encoding="utf-8", newline="\n"), indent=1)
print("VERIFY_MOTION_DONE", json.dumps(report["worst"]), "keys", keys, "paths", len(paths))
