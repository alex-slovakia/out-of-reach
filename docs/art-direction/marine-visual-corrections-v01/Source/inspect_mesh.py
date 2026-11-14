import bpy, json, pathlib
ROOT=pathlib.Path(__file__).resolve().parents[1]
INPUT=ROOT.parents[1]/'i01-integration/Assets/_OutOfReach/Visuals/Models/MarinePrologue/Transport/Models/TransportLaunch.fbx'
def components(mesh):
    adjacent=[[] for _ in mesh.vertices]
    for e in mesh.edges:
        a,b=e.vertices; adjacent[a].append(b); adjacent[b].append(a)
    todo=set(range(len(mesh.vertices))); result=[]
    while todo:
        found={todo.pop()}; pending=list(found)
        while pending:
            for n in adjacent[pending.pop()]:
                if n in todo: todo.remove(n);found.add(n);pending.append(n)
        result.append(found)
    return result
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(INPUT),use_custom_normals=True)
report={}
for name in ['BoosterStack','DeckLamps']:
    obj=bpy.data.objects[name];mesh=obj.data;rows=[]
    for indices in components(mesh):
        faces=[p.index for p in mesh.polygons if p.vertices[0] in indices]
        vertices=[mesh.vertices[i].co for i in indices]
        rows.append(dict(vertices=len(indices),polygons=len(faces),faces=faces,
            lo=[min(v[k] for v in vertices) for k in range(3)],hi=[max(v[k] for v in vertices) for k in range(3)]))
    report[name]=rows
(ROOT/'Evidence/components.json').write_text(json.dumps(report,indent=2))
print('COMPONENTS',json.dumps([{k:[{a:v for a,v in r.items() if a!='faces'} for r in rows]} for k,rows in report.items()]))
