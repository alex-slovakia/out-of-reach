"""Cycles renders of LaunchTowerV02 next to the existing structures (neutral stand-in materials for the existing meshes).
blender -b --factory-startup --python render_views.py -- <TransportLaunch.fbx> <MarineDock.fbx> <out_dir> [shot,...]"""
import bpy, sys, os, math, runpy
sys.dont_write_bytecode = True
from mathutils import Vector, Matrix
HERE = os.path.dirname(os.path.abspath(__file__))
a = sys.argv[sys.argv.index('--') + 1:]
TL, DOCK, OUTD = a[:3]
only = a[3].split(',') if len(a) > 3 else None
sys.argv = [sys.argv[0], '--']
runpy.run_path(os.path.join(HERE, 'build_tower.py'), run_name='built')
os.makedirs(OUTD, exist_ok=True)
import tw_kit
from tw_kit import B

def neutral(name, col, rough=0.55, met=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bs = m.node_tree.nodes['Principled BSDF']
    bs.inputs['Base Color'].default_value = (*col, 1)
    bs.inputs['Roughness'].default_value = rough
    bs.inputs['Metallic'].default_value = met
    return m
M_WHITE = neutral('ref_white', (0.74, 0.75, 0.77), 0.4)
M_CONC = neutral('ref_concrete', (0.36, 0.36, 0.37), 0.8)
M_DECK = neutral('ref_deck', (0.11, 0.115, 0.12), 0.6)
M_RAIL = neutral('ref_rail', (0.62, 0.64, 0.66), 0.5, 0.4)
M_DOCK = neutral('ref_dock', (0.3, 0.31, 0.33), 0.6)
M_GROUND = neutral('ref_ground', (0.30, 0.29, 0.27), 0.95)

def import_ref(path, off=(0, 0, 0)):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = set(bpy.data.objects) - before
    for ob in new:
        if ob.parent is None:
            ob.location = (ob.location[0] + off[0], ob.location[1] + off[1], ob.location[2] + off[2])
    return [o for o in new if o.type == 'MESH']

tl = import_ref(TL)
dock = import_ref(DOCK, (0, 9.9, -0.021))
hide = ('Support ', 'Marine fender', 'Fender rib', 'Fender chain', 'Boarding gate', 'BoardingSafety', 'ArrivalSafety', 'LaunchBarge', 'CapsuleInterior', 'AccessMastLanding')
for ob in tl + dock:
    n = ob.name
    if n.startswith(hide):
        ob.hide_render = True
        continue
    ob.data.materials.clear()
    if ob in tl:
        m = M_WHITE if n in ('CapsuleShell', 'BoosterStack', 'Hatch') else M_CONC
    else:
        low = n.lower()
        m = M_DECK if ('deck' in low or 'tread' in low) else (M_RAIL if any(k in low for k in ('rail', 'post', 'toe', 'lamp', 'mast')) else M_DOCK)
    ob.data.materials.append(m)

# ground at the pad level
bpy.ops.mesh.primitive_plane_add(size=400, location=(0, 0, -32.62))
g = bpy.context.active_object
g.data.materials.append(M_GROUND)

# arm retracted pose helper (Unity -90 deg about the pivot = +90 deg about Blender Z around B(pivot))
def retract():
    piv = B((-0.9, 0.0, -6.9))
    Mx = Matrix.Translation(piv) @ Matrix.Rotation(math.radians(90), 4, 'Z') @ Matrix.Translation(-piv)
    for ob in (bpy.data.objects['LTV2_ArmDetail'], bpy.data.objects['AccessArm']):
        ob.matrix_world = Mx @ ob.matrix_world

sc = bpy.context.scene
sc.render.engine = 'CYCLES'
cp = bpy.context.preferences.addons['cycles'].preferences
try:
    cp.compute_device_type = 'OPTIX'
    cp.get_devices()
    for d in cp.devices:
        d.use = d.type == 'OPTIX'
    sc.cycles.device = 'GPU'
except Exception as e:
    print('GPU setup failed', e)
sc.cycles.samples = 96
sc.cycles.use_denoising = True
sc.render.resolution_x, sc.render.resolution_y = 1920, 1080
sc.view_settings.view_transform = 'AgX'
sc.view_settings.exposure = float(os.environ.get('EXPO', '-2.6'))
try:
    sc.view_settings.look = 'AgX - Medium High Contrast'
except Exception as e:
    print('look', e)
# dawn sky
w = bpy.data.worlds.new('sky'); sc.world = w; w.use_nodes = True
nt = w.node_tree
sky = nt.nodes.new('ShaderNodeTexSky'); sky.sky_type = 'MULTIPLE_SCATTERING'
sky.sun_elevation = math.radians(14); sky.sun_rotation = math.radians(float(os.environ.get('SUN_ROT', '200')))
sky.air_density = 1.2
nt.links.new(sky.outputs['Color'], nt.nodes['Background'].inputs['Color'])
nt.nodes['Background'].inputs['Strength'].default_value = 1.0

# the scene authors the hatch open (localRotation 0,160,0): Unity +160 deg about Y = -160 deg about Blender Z
def open_hatch():
    piv = B((-0.72, 0.0, -2.4))
    Mx = Matrix.Translation(piv) @ Matrix.Rotation(math.radians(-160), 4, 'Z') @ Matrix.Translation(-piv)
    for n in ('Hatch', 'HatchStatusLamp'):
        if n in bpy.data.objects:
            ob = bpy.data.objects[n]
            ob.matrix_world = Mx @ ob.matrix_world
open_hatch()

SHOTS = {
    'walkway_to_hatch': ((0.0, 1.65, -7.0), (0.0, 1.30, -2.5), 26, False),
    'arm_rail_look_down': ((0.55, 1.65, -4.9), (2.6, -4.5, -7.2), 22, False),
    'pad_look_up': ((5.2, -32.0, -15.0), (0.4, -2.0, -8.4), 18, False),
    'distance_tower': ((38, -22, -52), (0, -15, -8), 34, False),
    'under_deck_arm': ((9.5, -7.0, -17.0), (0, -1.6, -8.0), 30, False),
    'rear_face_landings': ((7.0, -23.0, -20.0), (0, -22.0, -9.5), 30, False),
    'base_valve_ladder': ((10.5, -28.8, -2.5), (2.2, -30.2, -8.5), 30, False),
    'arm_retracted': ((-2.0, 9.0, 2.5), (-2.6, -0.5, -6.6), 26, True),
}
for name, (loc, tgt, lens, ret) in SHOTS.items():
    if only and name not in only:
        continue
    if ret:
        retract()
    cd = bpy.data.cameras.new(name); cd.lens = lens; cd.clip_start = 0.05; cd.clip_end = 3000
    co = bpy.data.objects.new(name, cd); sc.collection.objects.link(co)
    co.location = B(loc)
    d = B(tgt) - B(loc)
    co.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    sc.camera = co
    sc.render.filepath = os.path.join(OUTD, name + '.png')
    bpy.ops.render.render(write_still=True)
    print('RENDERED', name)
