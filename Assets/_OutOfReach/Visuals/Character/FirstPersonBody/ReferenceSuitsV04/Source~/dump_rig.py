import bpy, sys, json
argv = sys.argv[sys.argv.index('--') + 1:]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=argv[0], automatic_bone_orientation=False)
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
out = {}
for b in arm.data.bones:
    out[b.name] = dict(head=list(arm.matrix_world @ b.head_local), tail=list(arm.matrix_world @ b.tail_local),
                       parent=b.parent.name if b.parent else None)
json.dump(out, open(argv[1], 'w'))
print('RIG', len(out))
