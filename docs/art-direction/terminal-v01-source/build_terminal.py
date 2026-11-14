"""Original AURORA-7 touchscreen. Run with Blender 3.6 --background --python.
All coordinates supplied to helpers are intended Unity metres. No third-party geometry.
The export includes the prop only; studio, lights, cameras and preview text are excluded.
"""
import bpy, math, json, hashlib
from mathutils import Vector
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ASSET = ROOT / 'Assets/_OutOfReach/Visuals/Props/MissionTerminal'
MODEL = ASSET / 'Models'
MODEL.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
bpy.context.preferences.filepaths.save_version=0
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'; scene.unit_settings.scale_length = 1.0
prop = bpy.data.collections.new('MissionTerminal'); scene.collection.children.link(prop)
studio = bpy.data.collections.new('PreviewOnly'); scene.collection.children.link(studio)

def pos(p): return Vector((-p[0], -p[2], p[1]))
def move_collection(o, col):
    for old in list(o.users_collection): old.objects.unlink(o)
    col.objects.link(o)
def mat(name, color, metallic, rough):
    m = bpy.data.materials.new(name); m.use_nodes = True; m.diffuse_color = (*color, 1)
    n = m.node_tree.nodes.get('Principled BSDF'); n.inputs['Base Color'].default_value=(*color,1)
    n.inputs['Metallic'].default_value=metallic; n.inputs['Roughness'].default_value=rough
    return m
materials = {
 'MT_Enamel':mat('MT_Enamel',(0.38,0.41,0.42),.55,.37),
 'MT_Aluminium':mat('MT_Aluminium',(.55,.59,.61),.9,.29),
 'MT_Graphite':mat('MT_Graphite',(.025,.034,.042),.5,.44),
 'MT_Rubber':mat('MT_Rubber',(.012,.018,.022),0,.76),
 'MT_Screen':mat('MT_Screen',(.008,.018,.022),0,.6),
 'MT_Amber':mat('MT_Amber',(.65,.26,.04),.1,.4),
}
def finish(o, name, material, bevel=0):
    o.name=name; move_collection(o,prop)
    bpy.context.view_layer.objects.active=o
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    o.data.materials.append(materials[material])
    if bevel:
        b=o.modifiers.new('Manufactured edge radius','BEVEL'); b.width=bevel; b.segments=3
        b.limit_method='ANGLE'
        w=o.modifiers.new('Weighted surface normals','WEIGHTED_NORMAL'); w.keep_sharp=True
    o.data.use_auto_smooth=True
    for p in o.data.polygons: p.use_smooth=True
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.018)
    bpy.ops.object.mode_set(mode='OBJECT')
    return o
def box(name, center, size, material, bevel=.002):
    bpy.ops.mesh.primitive_cube_add(size=1,location=pos(center)); o=bpy.context.object
    o.scale=(size[0],size[2],size[1]); return finish(o,name,material,bevel)
def cylinder(name, a, b, radius, material, vertices=24):
    a,b=pos(a),pos(b); vec=b-a
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=radius,depth=vec.length,location=(a+b)/2)
    o=bpy.context.object; o.rotation_euler=vec.to_track_quat('Z','Y').to_euler()
    return finish(o,name,material,.0007)
def empty(name, p):
    o=bpy.data.objects.new(name,None); o.location=pos(p); prop.objects.link(o); return o
def ring(name, outer, inner, z1,z2, material, bevel):
    # Four continuous frame rails, true hole through the display, no intersecting slabs.
    verts=[]
    for z in (z1,z2):
        for width,height in (outer,inner):
            for x,y in ((-width/2,-height/2),(width/2,-height/2),(width/2,height/2),(-width/2,height/2)):
                verts.append(pos((x,y,z)))
    faces=[]
    for i in range(4):
        j=(i+1)%4
        faces += [(i,j,4+j,4+i),(8+i,12+i,12+j,8+j),(i,8+i,8+j,j),(4+i,4+j,12+j,12+i)]
    mesh=bpy.data.meshes.new(name); mesh.from_pydata(verts,[],faces); mesh.update()
    o=bpy.data.objects.new(name,mesh); prop.objects.link(o)
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active=o
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.mesh.normals_make_consistent(inside=False); bpy.ops.object.mode_set(mode='OBJECT')
    return finish(o,name,material,bevel)

case=box('Rear sealed enclosure',(0,0,.044),(.746,.492,.070),'MT_Graphite',.012)
ring('Main machined bezel',(.766,.510),(.650,.410),-.008,.020,'MT_Enamel',.006)
ring('Screen perimeter gasket',(.658,.418),(.640,.400),-.007,.004,'MT_Rubber',.001)
ring('Bright machined reveal',(.674,.434),(.660,.420),-.010,-.006,'MT_Aluminium',.0008)

# Front screen uses UV 0..1 in display-frame X/Y and faces the viewer (-Z in Unity).
verts=[pos(p) for p in ((-.32,-.20,0),(.32,-.20,0),(.32,.20,0),(-.32,.20,0))]
mesh=bpy.data.meshes.new('ActiveDisplayMesh'); mesh.from_pydata(verts,[],[(0,1,2,3)]); mesh.update()
screen=bpy.data.objects.new('ActiveDisplay',mesh); prop.objects.link(screen)
screen.data.materials.append(materials['MT_Screen']); uv=mesh.uv_layers.new(name='UVMap')
uvs=[(0,0),(1,0),(1,1),(0,1)]
for p in mesh.polygons:
    for li in p.loop_indices: uv.data[li].uv=uvs[mesh.loops[li].vertex_index]

# Captive screw pattern and visibly inset dark hex drives (shared repeated meshes).
screw_template=None; drive_template=None
for i,(x,y) in enumerate(((-.359,-.230),(.359,-.230),(-.359,.230),(.359,.230),(-.359,0),(.359,0))):
    for label,radius,z1,z2,material,verts in [('Captive screw',.0058,-.012,-.007,'MT_Aluminium',24),('Hex drive',.0024,-.0124,-.012,'MT_Graphite',6)]:
        o=cylinder(f'{label} {i+1}',(x,y,z1),(x,y,z2),radius,material,verts)
        key='screw_template' if label=='Captive screw' else 'drive_template'
        existing=globals()[key]
        if existing: o.data=existing.data
        else: globals()[key]=o

# A side handhold sized to the supplied 34 mm glove grip and isolated from touch area.
cylinder('Left handhold',(-.426,-.145,-.040),(-.426,.145,-.040),.017,'MT_Rubber',32)
for y in (-.145,.145):
    cylinder('Grip standoff',(-.426,y,-.040),(-.365,y,.034),.013,'MT_Aluminium')
    box('Grip mount lug',(-.367,y,.028),(.035,.044,.034),'MT_Graphite',.006)
# Articulated rear arm with real hinge pivots; an adjacent wall is deliberately not part of this asset.
box('VESA adapter',(0,0,.093),(.180,.168,.025),'MT_Aluminium',.006)
box('Mount arm',(0,-.038,.183),(.067,.074,.175),'MT_Graphite',.012)
cylinder('Tilt hinge',(-.110,-.038,.105),(.110,-.038,.105),.030,'MT_Aluminium',32)
cylinder('Wall hinge',(-.070,-.038,.275),(.070,-.038,.275),.033,'MT_Aluminium',32)
box('Wall base',(0,-.038,.306),(.190,.240,.023),'MT_Enamel',.008)
for x in (-.073,.073):
    for y in (-.127,.051): cylinder('Wall captive bolt',(x,y,.287),(x,y,.297),.006,'MT_Graphite')
box('Service cover',(.184,-.045,.083),(.170,.210,.009),'MT_Enamel',.004)
for y in (-.12,-.09,-.06,-.03,0,.03):
    box('Passive cooling slit',(.184,y,.089),(.116,.004,.002),'MT_Graphite',.0005)

# Cable flex loop is a single low-segment curve, strain-relieved at both ends.
curve=bpy.data.curves.new('Power data flex loop','CURVE'); curve.dimensions='3D'; curve.resolution_u=8
curve.bevel_depth=.008; curve.bevel_resolution=2
spline=curve.splines.new('BEZIER'); spline.bezier_points.add(4)
for p,co in zip(spline.bezier_points,[(.23,-.19,.076),(.23,-.25,.14),(.15,-.25,.255),(.06,-.17,.286),(.035,-.13,.293)]):
    p.co=pos(co); p.handle_left_type='AUTO'; p.handle_right_type='AUTO'
o=bpy.data.objects.new('Service cable',curve); prop.objects.link(o); curve.materials.append(materials['MT_Rubber'])
bpy.context.view_layer.objects.active=o; bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.ops.object.convert(target='MESH')
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.uv.smart_project(island_margin=.02); bpy.ops.object.mode_set(mode='OBJECT')
cylinder('Strain relief',(.23,-.16,.076),(.23,-.21,.076),.012,'MT_Graphite')
box('Status lamp',(.300,-.233,-.012),(.017,.004,.002),'MT_Amber',.0005)

anchors={
 'DisplayFrame':(0,0,0),'LeftGripFrame':(-.426,0,-.040),
 'InteractionAnchor':(0,-.10,-.08),'FocusCameraAnchor':(.012,.025,-.66),
 'WallMountAnchor':(0,-.038,.318)}
for name,p in anchors.items(): empty(name,p)
# Grip frame +X runs along the tube; the importer sets the documented rotation explicitly.

mat_spec={name:{'baseColorLinear':list(m.diffuse_color[:3]),'metallic':m.node_tree.nodes['Principled BSDF'].inputs['Metallic'].default_value,'roughness':m.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value} for name,m in materials.items()}
(ASSET/'materials.json').write_text(json.dumps(mat_spec,indent=2))
scene.render.engine='CYCLES'; scene.cycles.samples=48
scene.view_settings.view_transform='Filmic'
scene.view_settings.look='Medium High Contrast'; scene.view_settings.exposure=0; scene.view_settings.gamma=1
scene.world.color=(.06,.06,.06)

# Save authorable source before adding preview-only screen illustration.
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Source/MissionTerminal.blend'))
bpy.ops.object.select_all(action='DESELECT')
for o in prop.objects: o.select_set(True)
bpy.context.view_layer.objects.active=case
bpy.ops.export_scene.fbx(filepath=str(MODEL/'MissionTerminal.fbx'),use_selection=True,object_types={'MESH','EMPTY'},
    apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',
    use_mesh_modifiers=True,mesh_smooth_type='FACE',add_leaf_bones=False,bake_anim=False,path_mode='AUTO')

# Studio renders demonstrate the physical prop, not an in-game Unity result.
def preview_text(text,unitypos,size,color=(.55,.75,.80)):
    font=bpy.data.curves.new('Preview type','FONT'); font.body=text; font.size=size; font.extrude=0; font.align_x='LEFT'
    o=bpy.data.objects.new('PreviewOnly '+text[:16],font); studio.objects.link(o)
    o.location=pos(unitypos); o.rotation_euler=(math.pi/2,0,math.pi)
    m=mat('Preview lettering',color,0,.8); m.node_tree.nodes.get('Principled BSDF').inputs['Emission'].default_value=(*color,1)
    m.node_tree.nodes.get('Principled BSDF').inputs['Emission Strength'].default_value=.6
    font.materials.append(m); return o
preview_text('AURORA  /  EARTH ARCHIVE',(-.296,.174,-.002),.014)
preview_text('NASA EPIC / DSCOVR',(.045,.106,-.002),.011)
preview_text('OBSERVED UTC',(.045,.063,-.002),.009)
preview_text('2026-11-11 11:01:17',(.045,.043,-.002),.011,(.8,.85,.85))
preview_text('ARCHIVED OBSERVATION',(.045,-.006,-.002),.009)
preview_text('Not a live feed.\nNot vessel telemetry.',(.045,-.031,-.002),.010)
preview_text('Image: NASA EPIC Team\n/ NOAA DSCOVR',(.045,-.115,-.002),.008)
preview_text('TOUCH / DRAG TO INSPECT',(-.296,-.179,-.002),.009)
preview_text('AURORA-7   /   OBS-01',(-.316,-.240,-.014),.008,(.64,.70,.71))
# Literal planar EPIC disc, correct square image ratio, no fabricated weather.
imagepath=ASSET/'Data/epic_1b_20261111110606.jpg'
image=bpy.data.images.load(str(imagepath))
im=mat('Preview original NASA EPIC',(.1,.1,.1),0,1)
nodes=im.node_tree.nodes; nodes.clear(); out=nodes.new('ShaderNodeOutputMaterial'); emit=nodes.new('ShaderNodeEmission'); tex=nodes.new('ShaderNodeTexImage'); tex.image=image
im.node_tree.links.new(tex.outputs['Color'],emit.inputs['Color']); emit.inputs['Strength'].default_value=.8; im.node_tree.links.new(emit.outputs['Emission'],out.inputs['Surface'])
bpy.ops.mesh.primitive_plane_add(size=1,location=pos((-.15,-.005,-.001)))
ip=bpy.context.object; move_collection(ip,studio); ip.name='PreviewOnly literal EPIC observation'
ip.rotation_euler=(math.pi/2,0,math.pi); ip.scale=(.28,.28,.28); ip.data.materials.append(im)
def area(name,p,power,size):
    bpy.ops.object.light_add(type='AREA',location=pos(p)); o=bpy.context.object; move_collection(o,studio); o.name=name
    o.data.energy=power; o.data.shape='DISK'; o.data.size=size; o.rotation_euler=(-o.location).to_track_quat('-Z','Y').to_euler()
area('Soft key',(.25,.8,-.65),85,1.2); area('Edge light',(-.9,.2,.5),95,.75); area('Front fill',(-.2,-.5,-1.0),30,1.0)
bpy.ops.object.camera_add(); camera=bpy.context.object; move_collection(camera,studio); scene.camera=camera
camera.data.type='PERSP'; camera.data.lens=60
scene.render.resolution_x=1400; scene.render.resolution_y=1000; scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'; scene.render.film_transparent=False
for name,p,target in [('01-front',(.39,.18,-1.65),(0,0,.04)),('02-rear',(-1.25,.65,1.65),(0,0,.12))]:
    camera.location=pos(p); camera.rotation_euler=(pos(target)-camera.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(ROOT/'Renders'/f'{name}.png'); bpy.ops.render.render(write_still=True)
evaluated=bpy.context.evaluated_depsgraph_get(); tris=0
for o in prop.objects:
    if o.type=='MESH':
        mesh=o.evaluated_get(evaluated).to_mesh(); mesh.calc_loop_triangles(); tris+=len(mesh.loop_triangles); o.evaluated_get(evaluated).to_mesh_clear()
report={'units':'metres','activeDisplay':[.64,.40],'frontNormalUnity':[0,0,-1],'anchorsUnity':anchors,'triangles':tris,
 'meshes':sum(o.type=='MESH' for o in prop.objects),'meshUvMissing':[o.name for o in prop.objects if o.type=='MESH' and not o.data.uv_layers],
 'source':'Original procedural geometry authored for OUT OF REACH; no third-party model','note':'Blender render only; Unity import/hand contact not yet verified.'}
(ROOT/'model-report.json').write_text(json.dumps(report,indent=2))
print('TERMINAL_MODEL_COMPLETE '+json.dumps(report))
