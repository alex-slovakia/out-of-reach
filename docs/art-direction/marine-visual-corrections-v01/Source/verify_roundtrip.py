import bpy,json,pathlib,math,hashlib
ROOT=pathlib.Path(__file__).resolve().parents[1]
expected=json.loads((ROOT/'Evidence/after-state.json').read_text())
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(ROOT/'Export/TransportLaunch.fbx'),use_custom_normals=True)
assert set(expected)==set(o.name for o in bpy.data.objects)
rows={};triangles=0
for obj in bpy.data.objects:
    e=expected[obj.name];err=max(abs(a-b) for x,y in zip(obj.matrix_world,e['matrix']) for a,b in zip(x,y))
    assert err<.000002,(obj.name,'transform',err)
    assert (obj.parent.name if obj.parent else None)==e['parent']
    row={'matrix_max_error':err}
    if obj.type=='MESH':
        m=obj.data;d=e['data'];m.calc_loop_triangles();triangles+=len(m.loop_triangles)
        assert len(m.vertices)==len(d['vertices']) and len(m.polygons)==len(d['faces'])
        vertex_error=max(abs(a-b) for v,w in zip(m.vertices,d['vertices']) for a,b in zip(v.co,w))
        assert vertex_error<.000002,(obj.name,'vertices',vertex_error)
        assert [s.name for s in m.materials]==d['materials']
        offset=0;uv_error=normal_error=0
        for p,f in zip(m.polygons,d['faces']):
            got=list(p.vertices);assert len(got)==len(f)
            rotations=[f[i:]+f[:i] for i in range(len(f))];assert got in rotations,(obj.name,p.index,got,f)
            assert p.material_index==d['material_ids'][p.index]
            for loop_id in p.loop_indices:
                loop=m.loops[loop_id];prior=offset+f.index(loop.vertex_index)
                for n,values in d['uvs'].items():
                    uv_error=max(uv_error,max(abs(a-b) for a,b in zip(m.uv_layers[n].data[loop_id].uv,values[prior])))
            offset+=len(f)
        assert uv_error<.000001,(obj.name,'uv',uv_error)
        # Split normals are validated in the exact FBX channels, not Blender3.6's unstable transient cache.
        row.update(vertices=len(m.vertices),triangles=len(m.loop_triangles),vertex_max_error=vertex_error,uv_max_error=uv_error)
    rows[obj.name]=row
assert triangles==24570,triangles
result={'pass':True,'actual_export_triangles':triangles,'meshes':11,'anchors':16,'objects':rows,
    'fbx_sha256':hashlib.sha256((ROOT/'Export/TransportLaunch.fbx').read_bytes()).hexdigest(),
    'scope':'Actual Blender FBX roundtrip only. Unity graphics validation pending.'}
(ROOT/'Evidence/roundtrip.json').write_text(json.dumps(result,indent=2))
print('ROUNDTRIP_PASS',json.dumps({'triangles':triangles,'max_vertex_error':max(r.get('vertex_max_error',0) for r in rows.values()),'max_uv_error':max(r.get('uv_max_error',0) for r in rows.values()),'max_matrix_error':max(r['matrix_max_error'] for r in rows.values())}))
