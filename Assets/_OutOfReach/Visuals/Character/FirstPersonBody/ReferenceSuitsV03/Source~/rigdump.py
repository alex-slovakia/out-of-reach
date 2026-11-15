import bpy, sys, json
fbx, out = sys.argv[sys.argv.index("--") + 1:][:2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)
arm = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
d = {"scale": list(arm.scale), "matrix": [list(r) for r in arm.matrix_world], "bones": {}}
for b in arm.data.bones:
    d["bones"][b.name] = {"parent": b.parent.name if b.parent else None, "head": list(b.head_local), "tail": list(b.tail_local), "len": b.length,
                          "mat": [list(r) for r in b.matrix_local]}
json.dump(d, open(out, "w"))
