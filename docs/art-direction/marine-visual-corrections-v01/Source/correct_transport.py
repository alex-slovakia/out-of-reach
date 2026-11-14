"""Bounded repair of the exact delivered FBX; no Unity/project writes or geometry redesign."""
import bpy, json, pathlib, hashlib, math
from mathutils import Vector
ROOT=pathlib.Path(__file__).resolve().parents[1]
INPUT=ROOT.parents[1]/'i01-integration/Assets/_OutOfReach/Visuals/Models/MarinePrologue/Transport/Models/TransportLaunch.fbx'
EXPECTED='142c2053cda0e639781eae8e8b7973e1514fd385abfc399b5b621962572fcb62'
assert hashlib.sha256(INPUT.read_bytes()).hexdigest()==EXPECTED

def digest(value): return hashlib.sha256(json.dumps(value,sort_keys=True,separators=(',',':')).encode()).hexdigest()
def mesh_data(obj):
    m=obj.data;m.calc_normals_split()
    return dict(name=m.name,vertices=[list(v.co) for v in m.vertices],
        faces=[list(p.vertices) for p in m.polygons],smooth=[p.use_smooth for p in m.polygons],
        material_ids=[p.material_index for p in m.polygons],materials=[s.name for s in m.materials],
        uvs={u.name:[list(x.uv) for x in u.data] for u in m.uv_layers},
        normals=[list(l.normal) for l in m.loops],edges=[(list(e.vertices),e.use_edge_sharp,e.use_seam) for e in m.edges])
def state():
    return {o.name:dict(type=o.type,parent=o.parent.name if o.parent else None,
        matrix=[list(r) for r in o.matrix_world],data=mesh_data(o) if o.type=='MESH' else None) for o in bpy.data.objects}
def parts(mesh):
    adjacent=[[] for _ in mesh.vertices]
    for e in mesh.edges:
        a,b=e.vertices;adjacent[a].append(b);adjacent[b].append(a)
    todo=set(range(len(mesh.vertices)));result=[]
    while todo:
        found={todo.pop()};pending=list(found)
        while pending:
            for n in adjacent[pending.pop()]:
                if n in todo:todo.remove(n);found.add(n);pending.append(n)
        result.append(found)
    return result
def replace(obj,flip,remove):
    old=obj.data;data=mesh_data(obj);kept=[i for i in range(len(old.vertices)) if i not in remove]
    remap={v:i for i,v in enumerate(kept)};faces=[];uvs={n:[] for n in data['uvs']};normals=[];ids=[];smooth=[]
    for p in old.polygons:
        if any(i in remove for i in p.vertices):
            assert all(i in remove for i in p.vertices);continue
        loops=list(p.loop_indices)
        if p.index in flip:loops.reverse()
        faces.append([remap[old.loops[i].vertex_index] for i in loops]);ids.append(p.material_index);smooth.append(p.use_smooth)
        for i in loops:
            normals.append([c*(-1 if p.index in flip else 1) for c in data['normals'][i]])
            for n in uvs:uvs[n].append(data['uvs'][n][i])
    mesh=bpy.data.meshes.new(data['name']+'_corrected');mesh.from_pydata([data['vertices'][i] for i in kept],[],faces)
    for mat in old.materials:mesh.materials.append(mat)
    for p,mat,sm in zip(mesh.polygons,ids,smooth):p.material_index=mat;p.use_smooth=sm
    flags={tuple(sorted(remap[i] for i in e if i in remap)):(sharp,seam) for e,sharp,seam in data['edges'] if all(i in remap for i in e)}
    for e in mesh.edges:e.use_edge_sharp,e.use_seam=flags[tuple(sorted(e.vertices))]
    for n,values in uvs.items():
        layer=mesh.uv_layers.new(name=n)
        for u,value in zip(layer.data,values):u.uv=value
    mesh.use_auto_smooth=True;mesh.normals_split_custom_set(normals);mesh.update()
    obj.data=mesh;bpy.data.meshes.remove(old);mesh.name=data['name']
    return kept

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(INPUT),use_custom_normals=True)
before=state();assert sum(x['type']=='MESH' for x in before.values())==11
assert sum(x['type']=='EMPTY' for x in before.values())==16
booster=bpy.data.objects['BoosterStack'];m=booster.data;selected=[]
expected=[(146,288,-3.4,-.55),(96,96,-4.4,-3.4),(288,480,-29.4,-4.4),(97,144,-30.6,-29.4),(161,288,-31.4,-30.6)]
for vc,fc,lo,hi in expected:
    matches=[c for c in parts(m) if len(c)==vc and abs(min(m.vertices[i].co.z for i in c)-lo)<.00001 and abs(max(m.vertices[i].co.z for i in c)-hi)<.00001]
    assert len(matches)==1,(vc,lo,hi)
    faces=[p.index for p in m.polygons if p.vertices[0] in matches[0]];assert len(faces)==fc
    selected+=faces
assert len(selected)==1296
replace(booster,set(selected),set())
lamp=bpy.data.objects['DeckLamps'];orphan={v.index for v in lamp.data.vertices if v.co.z>4}
assert len(orphan)==24 and sum(all(i in orphan for i in p.vertices) for p in lamp.data.polygons)==44
retained=replace(lamp,set(),orphan)
after=state();assert set(before)==set(after)
report={'original_fbx_sha256':EXPECTED,'blender':bpy.app.version_string,'flipped_booster_faces':selected,
    'removed_lamp_vertex_indices':sorted(orphan),'removed_lamp_triangles':44,'unchanged_objects':{},'changed_meshes':{},'caps':[]}
for name in before:
    assert before[name]['matrix']==after[name]['matrix'] and before[name]['parent']==after[name]['parent']
    if name not in ['BoosterStack','DeckLamps']:
        a,b=before[name]['data'],after[name]['data']
        normal_error=0
        if a is not None:
            assert {k:v for k,v in a.items() if k!='normals'}=={k:v for k,v in b.items() if k!='normals'},name
            normal_error=max(abs(x-y) for an,bn in zip(a['normals'],b['normals']) for x,y in zip(an,bn))
            assert normal_error<.000001,(name,normal_error)
        report['unchanged_objects'][name]={'geometry_uv_material_transform_sha256':digest({**after[name],'data':{k:v for k,v in b.items() if k!='normals'} if b else None}),
            'normal_recalculation_max_error':normal_error}
    elif name=='BoosterStack':
        a,b=before[name]['data'],after[name]['data'];assert a['vertices']==b['vertices'] and a['materials']==b['materials']
        assert len(a['faces'])==len(b['faces'])==2276
        for i,(f,g) in enumerate(zip(a['faces'],b['faces'])):assert g==(list(reversed(f)) if i in selected else f)
        report['changed_meshes'][name]={'vertices':1316,'triangles':2276,'flipped_triangles':1296,'unchanged_triangles':980}
    else:report['changed_meshes'][name]={'vertices_before':120,'vertices_after':96,'triangles_before':220,'triangles_after':176}
mesh=booster.data;mesh.calc_normals_split()
# Validate every large exterior cylinder band, plus caps of each closed/recessed lathe section.
for lo,hi,r in [(-3.4,-.60,2.35),(-4.4,-3.4,2.30),(-8.6,-4.4,2.30),(-10.4,-8.6,2.304),(-29.4,-10.4,2.30),(-30.6,-29.4,2.30)]:
    found=[]
    for p in mesh.polygons:
        vs=[mesh.vertices[i].co for i in p.vertices]
        if max(abs(math.hypot(v.x,v.y)-r) for v in vs)<.00001 and abs(min(v.z for v in vs)-lo)<.00001 and abs(max(v.z for v in vs)-hi)<.00001:
            radial=Vector((p.center.x,p.center.y,0)).normalized();assert p.normal.dot(radial)>.99
            assert all(mesh.loops[i].normal.dot(radial)>.98 for i in p.loop_indices)
            found.append(p.index)
    assert len(found)==96,(lo,hi,len(found))
    report.setdefault('outward_bands',[]).append({'lo':lo,'hi':hi,'radius':r,'outward_triangles':len(found)})
for height,expected_z in [(-.55,1),(-3.4,-1),(-30.6,-1)]:
    caps=[p for p in mesh.polygons if p.index in selected and all(abs(mesh.vertices[i].co.z-height)<.00001 for i in p.vertices) and abs(p.normal.z)>.99]
    # At engine top -30.6 the bell inlet has its own upward-facing cap as well; distinguish by radius.
    caps=[p for p in caps if max(math.hypot(mesh.vertices[i].co.x,mesh.vertices[i].co.y) for i in p.vertices)>2]
    assert len(caps)==48 and all(p.normal.z*expected_z>.99 for p in caps),(height,len(caps))
    report['caps'].append({'height':height,'triangles':48,'normal_z':expected_z})

# Copy existing licensed maps unchanged into this source package so the .blend is inspectable standalone.
import shutil
texture_root=ROOT/'Source/textures';texture_root.mkdir(exist_ok=True)
source_textures=INPUT.parents[1]/'Textures'
for source in source_textures.glob('*.png'):shutil.copy2(source,texture_root/source.name)
for image in bpy.data.images:
    candidate=texture_root/pathlib.Path(image.filepath.replace('\\','/')).name
    if candidate.exists():image.filepath=str(candidate);image.reload();image.filepath='//textures/'+candidate.name
bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Source/TransportLaunch_Corrected.blend'))
# Final FBX is encoded by encode_exact_fbx.py using the same Blender-bundled FBX codec.
# Do not ordinary-re-export here: Blender3.6 rewrites mesh names and corrupts some untouched custom normals.
(ROOT/'Evidence/before-state.json').write_text(json.dumps(before))
(ROOT/'Evidence/after-state.json').write_text(json.dumps(after))
(ROOT/'Evidence/correction-report.json').write_text(json.dumps(report,indent=2))
print('CORRECTION_PASS',json.dumps({k:v for k,v in report.items() if k not in ['flipped_booster_faces','unchanged_objects']}))
