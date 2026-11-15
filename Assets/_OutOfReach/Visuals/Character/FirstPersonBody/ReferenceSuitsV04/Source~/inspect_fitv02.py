import bpy, sys, json
from mathutils import Vector
argv = sys.argv[sys.argv.index('--') + 1:]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=argv[0], automatic_bone_orientation=False)
out = {}
for o in bpy.data.objects:
    e = dict(type=o.type, parent=o.parent.name if o.parent else None)
    if o.type == 'MESH':
        me = o.data
        bb = [o.matrix_world @ Vector(c) for c in o.bound_box]
        e.update(verts=len(me.vertices), polys=len(me.polygons), mats=[m.name for m in me.materials if m],
                 groups=len(o.vertex_groups), mods=[m.type for m in o.modifiers],
                 bmin=[round(min(v[i] for v in bb), 3) for i in range(3)], bmax=[round(max(v[i] for v in bb), 3) for i in range(3)],
                 matcounts={me.materials[i].name if me.materials[i] else '?': sum(1 for p in me.polygons if p.material_index == i) for i in range(len(me.materials))})
    if o.type == 'ARMATURE':
        e.update(bones=len(o.data.bones), scale=list(o.scale), root=[b.name for b in o.data.bones if not b.parent],
                 sample=[b.name for b in o.data.bones][:12])
        heads = {b.name: [round(x, 3) for x in (o.matrix_world @ b.head_local)] for b in o.data.bones}
        e['head_y'] = {k: heads[k] for k in heads if k in ('root', 'pelvis.L', 'spine05', 'neck01', 'head', 'clavicle.L', 'upperarm01.L', 'lowerarm01.L', 'wrist.L', 'thigh.L', 'shin.L', 'foot.L', 'toe1-1.L')}
    out[o.name] = e
json.dump(out, open(argv[1], 'w'), indent=1)
print('INSPECTED', len(out))
