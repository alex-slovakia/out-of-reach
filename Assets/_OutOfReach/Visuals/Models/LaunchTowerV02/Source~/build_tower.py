"""LaunchTowerV02 builder.  blender -b --factory-startup --python build_tower.py -- [--textures DIR] [--out DIR] [--export]
Builds four objects in the TransportLaunch frame (see tw_kit.py):
  LTV2_TowerFacade (static), LTV2_DeckSupport (static), LTV2_Deluge (static), LTV2_ArmDetail (origin = arm pivot)."""
import bpy, sys, os, json, math
sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import tw_kit, tw_truss, tw_parts, tw_layout
from tw_kit import *

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
def arg(name, default=None):
    return argv[argv.index(name) + 1] if name in argv else default
TEX = arg('--textures', os.path.join(HERE, '..', 'Textures'))
OUT = arg('--out', os.path.join(HERE, '..', 'Models'))
EXPORT = '--export' in argv

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.scene.unit_settings.system = 'METRIC'
bpy.context.scene.unit_settings.scale_length = 1.0

# ---- Blender materials (preview only; the FBX is exported without texture links) ----
BASE = {
    'TW_SteelPainted': ((0.50, 0.53, 0.56, 1), 0.1, 0.55, 'TW_SteelPainted_BaseColor.png'),
    'TW_Galvanized': ((0.60, 0.62, 0.63, 1), 0.85, 0.45, 'TW_Galvanized_BaseColor.png'),
    'TW_OchrePaint': ((0.62, 0.40, 0.08, 1), 0.0, 0.55, None),
    'TW_HazardStripe': ((0.62, 0.40, 0.08, 1), 0.0, 0.55, 'TW_HazardStripe_BaseColor.png'),
    'TW_PipeRed': ((0.42, 0.07, 0.05, 1), 0.2, 0.5, 'TW_PipeRed_BaseColor.png'),
    'TW_Rubber': ((0.04, 0.04, 0.045, 1), 0.0, 0.3, None),
    'TW_Grating': ((0.45, 0.47, 0.48, 1), 0.85, 0.4, 'TW_Grating_BaseColor.png'),
    'TW_Labels': ((0.5, 0.5, 0.5, 1), 0.0, 0.5, 'TW_Labels_BaseColor.png'),
}
def make_materials(textured=True):
    for name, (col, met, rough, tex) in BASE.items():
        m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        m.use_nodes = True
        m.use_backface_culling = True
        nt = m.node_tree
        for n in list(nt.nodes):
            if n.type != 'OUTPUT_MATERIAL' and n.type != 'BSDF_PRINCIPLED':
                nt.nodes.remove(n)
        bsdf = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
        bsdf.inputs['Base Color'].default_value = col
        bsdf.inputs['Metallic'].default_value = met
        bsdf.inputs['Roughness'].default_value = rough
        p = os.path.join(TEX, tex) if tex else None
        msp = os.path.join(TEX, tex.replace('BaseColor', 'MetallicSmoothness')) if tex else None
        if textured and msp and os.path.exists(msp):
            img2 = bpy.data.images.load(msp, check_existing=True)
            img2.colorspace_settings.name = 'Non-Color'
            t2 = nt.nodes.new('ShaderNodeTexImage'); t2.image = img2
            sep = nt.nodes.new('ShaderNodeSeparateColor')
            inv = nt.nodes.new('ShaderNodeMath'); inv.operation = 'SUBTRACT'; inv.inputs[0].default_value = 1.0
            nt.links.new(t2.outputs['Color'], sep.inputs['Color'])
            nt.links.new(sep.outputs['Red'], bsdf.inputs['Metallic'])
            nt.links.new(t2.outputs['Alpha'], inv.inputs[1])
            nt.links.new(inv.outputs['Value'], bsdf.inputs['Roughness'])
        if textured and p and os.path.exists(p):
            img = bpy.data.images.load(p, check_existing=True)
            tn = nt.nodes.new('ShaderNodeTexImage'); tn.image = img
            nt.links.new(tn.outputs['Color'], bsdf.inputs['Base Color'])
            if name == 'TW_Grating':
                nt.links.new(tn.outputs['Alpha'], bsdf.inputs['Alpha'])
                m.blend_method = 'HASHED' if hasattr(m, 'blend_method') else None
make_materials(True)

# ---- geometry ----
tower = Obj('LTV2_TowerFacade')
tw_truss.build_truss(tower)
tw_truss.build_ladder_and_landings(tower)
tw_truss.build_cladding(tower)
tw_parts.build_cable_trays(tower)
tw_parts.build_markings(tower, None)
deck = Obj('LTV2_DeckSupport')
tw_parts.build_deck_support(deck)
delu = Obj('LTV2_Deluge')
tw_parts.build_deluge(delu)
arm = Obj('LTV2_ArmDetail', origin_u=ARM_PIVOT if False else tw_layout.ARM_PIVOT)
tw_parts.build_arm(arm)
objs = [o.finish() for o in (tower, deck, delu, arm)]
stats = {ob.name: dict(tris=tri_count(ob), verts=len(ob.data.vertices)) for ob in objs}
print('STATS', json.dumps(stats))

if '--render' in argv:
    from mathutils import Vector
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sc.display.shading.light = 'STUDIO'
    sc.display.shading.color_type = 'TEXTURE'
    sc.render.resolution_x, sc.render.resolution_y = 1600, 1000
    sc.world = bpy.data.worlds.new('w'); sc.world.color = (0.5, 0.6, 0.75)
    cams = eval(arg('--cams'))
    fbxs = arg('--ref', '')
    for f in [x for x in fbxs.split(',') if x]:
        off = (0, 0, 0)
        if '@' in f:
            f, o_ = f.split('@'); off = tuple(float(v) for v in o_.split(':'))
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=f)
        for ob in set(bpy.data.objects) - before:
            if ob.parent is None:
                ob.location = (ob.location[0] + off[0], ob.location[1] + off[1], ob.location[2] + off[2])
    for ob in list(bpy.data.objects):
        if ob.name.startswith(('Support ', 'Marine fender', 'Fender rib', 'Fender chain', 'Boarding gate', 'BoardingSafety', 'ArrivalSafety')):
            ob.hide_render = True      # root disables these sea-level pieces (ApplyElevatedDeckVariant)
    for i, (loc, tgt, lens, ortho) in enumerate(cams):
        cd = bpy.data.cameras.new('c%d' % i)
        if ortho:
            cd.type = 'ORTHO'; cd.ortho_scale = ortho
        else:
            cd.lens = lens
        cd.clip_end = 3000; cd.clip_start = 0.05
        co = bpy.data.objects.new('c%d' % i, cd); sc.collection.objects.link(co)
        co.location = B(loc)
        d = B(tgt) - B(loc)
        co.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
        sc.camera = co
        sc.render.filepath = '%s_%d.png' % (arg('--render'), i)
        bpy.ops.render.render(write_still=True)

if EXPORT:
    from mathutils import Vector
    # export without texture links: the Blender exporter writes absolute paths of this machine otherwise
    for m in bpy.data.materials:
        if m.node_tree:
            for n in [n for n in m.node_tree.nodes if n.type == 'TEX_IMAGE']:
                m.node_tree.nodes.remove(n)
    # Unity (Bake Axis Conversion ON) shows an exported Blender point (x, y, z) at Unity (x, z, y).  The existing
    # TransportLaunch frame is Unity = (-bx, bz, -by) of this builder's Blender frame, so rotate 180 deg about Z.
    from mathutils import Matrix
    R = Matrix.Rotation(math.pi, 4, 'Z')
    for ob in objs:
        ob.data.transform(R)
        ob.location = R @ ob.location
    bpy.context.view_layer.update()
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objs:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    fbx = os.path.join(OUT, 'LaunchTowerV02.fbx')
    bpy.ops.export_scene.fbx(
        filepath=fbx, use_selection=True, object_types={'MESH'}, apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True, axis_forward='-Z', axis_up='Y',
        use_mesh_modifiers=False, mesh_smooth_type='OFF', use_triangles=True, add_leaf_bones=False,
        bake_anim=False, path_mode='STRIP', embed_textures=False, use_custom_props=False)
    # report in Unity metres of the exported frame (x = Bx, y = Bz, z = By)
    rep = {'fbx': 'LaunchTowerV02.fbx', 'objects': {}, 'materials': MATS}
    for ob in objs:
        vs = [ob.matrix_world @ v.co for v in ob.data.vertices]
        u = [(v.x, v.z, v.y) for v in vs]
        rep['objects'][ob.name] = dict(
            tris=tri_count(ob), verts=len(vs),
            origin_unity=[round(ob.location.x, 4), round(ob.location.z, 4), round(ob.location.y, 4)],
            bounds_min=[round(min(p[i] for p in u), 4) for i in range(3)],
            bounds_max=[round(max(p[i] for p in u), 4) for i in range(3)],
            tris_by_material={MATS[i]: sum(len(p.vertices) - 2 for p in ob.data.polygons if p.material_index == i) for i in range(len(MATS))})
    rep['items'] = [dict(name=k, tris=v['tris'], verts=v['verts'], origin_unity=v['origin_unity'], bounds_min=v['bounds_min'], bounds_max=v['bounds_max']) for k, v in rep['objects'].items()]
    rep['total_tris'] = sum(v['tris'] for v in rep['objects'].values())
    json.dump(rep, open(os.path.join(OUT, 'LaunchTowerV02_report.json'), 'w'), indent=1)
    print('EXPORTED', fbx, rep['total_tris'])
