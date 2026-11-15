"""Re-import the exported FBX and compare the rig with FitV02 (names, parents, bind) and the takes.
blender -b --factory-startup --python verify_export.py -- <exported fbx> <rig_fitv02.json> <out json>"""
import bpy, sys, json
a = sys.argv[sys.argv.index('--') + 1:]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=a[0], automatic_bone_orientation=False)
rig = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
ref = json.load(open(a[1]))
mx = 0.0; parent_mismatch = []
for b in rig.data.bones:
    r = ref.get(b.name)
    if r is None:
        parent_mismatch.append('extra ' + b.name); continue
    h = rig.matrix_world @ b.head_local; t = rig.matrix_world @ b.tail_local
    mx = max(mx, max(abs(h[i] - r['head'][i]) for i in range(3)), max(abs(t[i] - r['tail'][i]) for i in range(3)))
    if (b.parent.name if b.parent else None) != r['parent']:
        parent_mismatch.append(b.name)
missing = [n for n in ref if n not in rig.data.bones]
meshes = {o.name: dict(verts=len(o.data.vertices), parent=o.parent.name if o.parent else None,
                       arm_mod=any(m.type == 'ARMATURE' for m in o.modifiers)) for o in bpy.data.objects if o.type == 'MESH'}
out = dict(bones=len(rig.data.bones), rig_name=rig.name, rig_scale=list(rig.scale), max_bind_diff_m=mx,
           parent_mismatch=parent_mismatch, missing=missing, meshes=meshes,
           actions=sorted(x.name for x in bpy.data.actions))
json.dump(out, open(a[2], 'w'), indent=1)
print('VERIFY', json.dumps(out)[:900])
