"""Use Blender's bundled FBX codec; preserve original native IDs and all unrelated FBX channels.
The ordinary Blender3.6 geometry exporter produced invalid normals on untouched imported geometry.
This bounded compatibility adapter applies the already proven Blender face correction directly.
"""
import pathlib,copy,array,hashlib,json,math,bpy,numpy as np
from io_scene_fbx import parse_fbx,encode_bin
ROOT=pathlib.Path(__file__).resolve().parents[1]
SOURCE=ROOT.parents[1]/'i01-integration/Assets/_OutOfReach/Visuals/Models/MarinePrologue/Transport/Models/TransportLaunch.fbx'
assert hashlib.sha256(SOURCE.read_bytes()).hexdigest()=='142c2053cda0e639781eae8e8b7973e1514fd385abfc399b5b621962572fcb62'
tree,version=parse_fbx.parse(str(SOURCE));original=copy.deepcopy(tree)
def child(n,key):return next(c for c in n.elems if c.id==key)
def val(n,key):return child(n,key).props[0]
objects=child(tree,b'Objects')
booster=next(g for g in objects.elems if g.id==b'Geometry' and b'BoosterStack' in g.props[1])
lamp=next(g for g in objects.elems if g.id==b'Geometry' and b'DeckLamps' in g.props[1])
polys=val(booster,b'PolygonVertexIndex');edges=val(booster,b'Edges')
rawverts=val(booster,b'Vertices');parents=list(range(len(rawverts)//3))
def find(i):
    while parents[i]!=i:parents[i]=parents[parents[i]];i=parents[i]
    return i
faces=[]
for i in range(0,len(polys),3):
    f=[polys[i],polys[i+1],-polys[i+2]-1];assert min(f)>=0
    faces.append(f)
    for v in f[1:]:parents[find(v)]=find(f[0])
groups={}
for i in range(len(parents)):groups.setdefault(find(i),set()).add(i)
selected_vertices=set()
for vc,lo,hi in [(146,-3.4,-.55),(96,-4.4,-3.4),(288,-29.4,-4.4),(97,-30.6,-29.4),(161,-31.4,-30.6)]:
    match=[g for g in groups.values() if len(g)==vc and abs(min(rawverts[i*3+2] for i in g)-lo)<.00001 and abs(max(rawverts[i*3+2] for i in g)-hi)<.00001]
    assert len(match)==1;selected_vertices.update(match[0])
flip={i for i,f in enumerate(faces) if f[0] in selected_vertices};assert len(flip)==1296
norm=child(booster,b'LayerElementNormal');normal_values=val(norm,b'Normals');normal_indices=val(norm,b'NormalsIndex')
uv=child(booster,b'LayerElementUV');uv_indices=val(uv,b'UVIndex')
inverted={}
for face in sorted(flip):
    start=face*3;values=list(polys[start:start+3]);assert values[2]<0 and values[0]>=0 and values[1]>=0
    vertices=[values[0],values[1],-values[2]-1];polys[start:start+3]=array.array(polys.typecode,[vertices[2],vertices[1],-vertices[0]-1])
    uv_indices[start:start+3]=array.array(uv_indices.typecode,reversed(uv_indices[start:start+3]))
    ns=[]
    for old in reversed(normal_indices[start:start+3]):
        if old not in inverted:
            inverted[old]=len(normal_values)//3
            normal_values.extend(-c for c in normal_values[old*3:old*3+3])
        ns.append(inverted[old])
    normal_indices[start:start+3]=array.array(normal_indices.typecode,ns)
for i,corner in enumerate(edges):
    face,j=divmod(corner,3)
    if face in flip:edges[i]=face*3+(1-j)%3

# Original island is the last24 vertices; its44 triangles are interspersed, not a tail range.
lv=val(lamp,b'Vertices');lp=val(lamp,b'PolygonVertexIndex')
assert len(lv)==360 and min(lv[96*3+2::3])>4 and max(lv[2:96*3:3])<0
keepfaces=[i for i in range(220) if lp[i*3]<96];assert len(keepfaces)==176
corners=[i*3+j for i in keepfaces for j in range(3)];corner_map={old:new for new,old in enumerate(corners)}
del lv[96*3:];lp[:]=array.array(lp.typecode,[lp[i] for i in corners])
le=val(lamp,b'Edges');le[:]=array.array(le.typecode,[corner_map[v] for v in le if v in corner_map])
for layer,field in [(b'LayerElementNormal',b'NormalsIndex'),(b'LayerElementUV',b'UVIndex')]:
    values=val(child(lamp,layer),field);values[:]=array.array(values.typecode,[values[i] for i in corners])
values=val(child(lamp,b'LayerElementSmoothing'),b'Smoothing');values[:]=array.array(values.typecode,[values[i] for i in keepfaces])

methods={ord(k):v for k,v in {'C':'add_bool','B':'add_int8','Y':'add_int16','I':'add_int32','L':'add_int64','F':'add_float32','D':'add_float64','R':'add_bytes','S':'add_string','i':'add_int32_array','l':'add_int64_array','f':'add_float32_array','d':'add_float64_array','b':'add_bool_array','c':'add_byte_array'}.items()}
def encode(node):
    e=encode_bin.FBXElem(node.id)
    for typ,value in zip(node.props_type,node.props):getattr(e,methods[typ])(value)
    e.elems=[encode(c) for c in node.elems];return e
out=ROOT/'Export/TransportLaunch.fbx';encode_bin.write(str(out),encode(tree),version)
decoded,_=parse_fbx.parse(str(out))
assert decoded==tree,'Encoder must preserve the exact corrected parse tree.'
oldobj=child(original,b'Objects');newobj=child(decoded,b'Objects');changes=[]
for a,b in zip(oldobj.elems,newobj.elems):
    if a!=b:
        assert a.id==b.id==b'Geometry' and a.props[0]==b.props[0] and a.props[1]==b.props[1]
        changes.append(a.props[1].split(b'\0')[0].decode())
assert changes==[booster.props[1].split(b'\0')[0].decode(),lamp.props[1].split(b'\0')[0].decode()]
for a,b in zip(original.elems,decoded.elems):
    if a.id!=b'Objects':assert a==b,a.id
report={'pass':True,'changed_objects':changes,'unchanged_objects_byte_semantics':len(oldobj.elems)-2,
 'original_native_ids_preserved':True,'all_other_top_level_nodes_preserved':True,
 'vertices_unchanged_booster':val(booster,b'Vertices')==val(next(g for g in oldobj.elems if g.id==b'Geometry' and b'BoosterStack' in g.props[1]),b'Vertices'),
 'fbx_sha256':hashlib.sha256(out.read_bytes()).hexdigest(),'bytes':out.stat().st_size,'scope':'Blender bundled FBX parse/encode, actual source channels; native Unity render pending.'}
vv=np.asarray(val(booster,b'Vertices')).reshape(-1,3);pi=np.asarray(val(booster,b'PolygonVertexIndex')).reshape(-1,3).copy();pi[:,2]=-pi[:,2]-1
nn=np.asarray(val(norm,b'Normals')).reshape(-1,3);ni=np.asarray(val(norm,b'NormalsIndex')).reshape(-1,3)
assert np.isfinite(nn).all()
report['outward_band_checks']=[]
for lo,hi,r in [(-3.4,-.60,2.35),(-4.4,-3.4,2.30),(-8.6,-4.4,2.30),(-10.4,-8.6,2.304),(-29.4,-10.4,2.30),(-30.6,-29.4,2.30)]:
    count=0;minimum=1.
    for i,face in enumerate(pi):
        points=vv[face]
        if np.max(np.abs(np.linalg.norm(points[:,:2],axis=1)-r))>.00001 or abs(points[:,2].min()-lo)>.00001 or abs(points[:,2].max()-hi)>.00001:continue
        radial=points.mean(axis=0);radial[2]=0;radial/=np.linalg.norm(radial)
        geometric=np.cross(points[1]-points[0],points[2]-points[0]);geometric/=np.linalg.norm(geometric)
        assert geometric@radial>.99
        d=float(np.min(nn[ni[i]]@radial));assert d>.98;minimum=min(minimum,d);count+=1
    assert count==96
    report['outward_band_checks'].append({'lo':lo,'hi':hi,'triangles':count,'minimum_corner_normal_radial_dot':minimum})
report['caps']=[]
for height,sign in [(-.55,1),(-3.4,-1),(-30.6,-1)]:
    found=[]
    for i,face in enumerate(pi):
        points=vv[face]
        if i not in flip or np.max(np.abs(points[:,2]-height))>.00001 or np.linalg.norm(points[:,:2],axis=1).max()<2:continue
        n=np.cross(points[1]-points[0],points[2]-points[0]);length=np.linalg.norm(n)
        if length<1e-12:continue
        assert n[2]/length*sign>.99;found.append(i)
    assert len(found)==48
    report['caps'].append({'height':height,'correctly_oriented_triangles':len(found),'sign_z':sign})
(ROOT/'Evidence/exact-fbx-report.json').write_text(json.dumps(report,indent=2))
print('EXACT_FBX_PASS',json.dumps(report))
