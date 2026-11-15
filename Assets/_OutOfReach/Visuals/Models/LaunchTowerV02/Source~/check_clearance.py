"""Clearance scan (Blender BVH) of LaunchTowerV02 against the measured existing structures.
blender -b --factory-startup --python check_clearance.py -- <TransportLaunch.fbx> <MarineDock.fbx> <out.json>
All geometry is compared in Unity metres of the prologue root frame. Existing pieces use the poses root applies:
dock deck at (0, -0.021, -9.9), hatch open 160 deg about HatchPivot, arm connected (0) or retracted (-90 deg about AccessArmPivot);
the 72 sea-level pieces (Support*, Marine fender*, Fender*) are disabled like in ApplyElevatedDeckVariant."""
import bpy, sys, os, json, math, runpy
sys.dont_write_bytecode = True
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
TL, DOCK, OUT = sys.argv[sys.argv.index('--') + 1:][:3]
sys.argv = [sys.argv[0], '--']      # the builder must not export or render
glb = runpy.run_path(os.path.join(HERE, 'build_tower.py'), run_name='built')
mine = {o.name: o for o in bpy.data.objects if o.name.startswith('LTV2_')}


def U(co):           # Blender frame of this builder / of a Blender import of TransportLaunch.fbx -> Unity
    return Vector((-co.x, co.z, -co.y))


def soup(ob, off=(0, 0, 0)):
    me = ob.data
    me.calc_loop_triangles()
    vs = [U(ob.matrix_world @ v.co) + Vector(off) for v in me.vertices]
    tr = [tuple(t.vertices) for t in me.loop_triangles]
    return vs, tr


def rot_y(vs, pivot, deg):
    a = math.radians(deg)
    c, s = math.cos(a), math.sin(a)
    pv = Vector(pivot)
    out = []
    for v in vs:
        d = v - pv
        out.append(pv + Vector((d.x * c + d.z * s, d.y, -d.x * s + d.z * c)))
    return out


def bvh(vs, tr):
    return BVHTree.FromPolygons([tuple(v) for v in vs], tr, epsilon=0.0)


before = set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=TL)
tl = {o.name: o for o in set(bpy.data.objects) - before if o.type == 'MESH'}
before = set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=DOCK)
dock = {o.name: o for o in set(bpy.data.objects) - before if o.type == 'MESH'
        and not o.name.startswith(('Support ', 'Marine fender', 'Fender rib', 'Fender chain'))}
bpy.context.view_layer.update()
DOCK_OFF = (0, -0.021, -9.9)
ARM_PIV = (-0.9, 0.0, -6.9)
HATCH_PIV = (-0.72, 0.0, -2.4)

existing = {}
existing['AccessMastTower'] = soup(tl['AccessMastTower'])
existing['CapsuleShell'] = soup(tl['CapsuleShell'])
existing['BoosterStack'] = soup(tl['BoosterStack'])
v, t = soup(tl['Hatch'])
existing['HatchOpen160'] = (rot_y(v, HATCH_PIV, 160), t)
existing['HatchClosed'] = (v, t)
v, t = soup(tl['AccessArm'])
existing['AccessArmConnected'] = (v, t)
existing['AccessArmRetracted'] = (rot_y(v, ARM_PIV, -90), t)
dv, dt = [], []
for o in dock.values():
    v, t = soup(o, DOCK_OFF)
    base = len(dv)
    dv += v
    dt += [tuple(i + base for i in tri) for tri in t]
existing['DockDeckModule'] = (dv, dt)
# the deck module's parked gates: Boarding gate assembly at 90 deg about its control frame, ignore (root-owned, inside the deck)

mine_s = {k: soup(o) for k, o in mine.items()}
arm_v, arm_t = mine_s['LTV2_ArmDetail']
mine_s['LTV2_ArmDetail@connected'] = (arm_v, arm_t)
mine_s['LTV2_ArmDetail@retracted'] = (rot_y(arm_v, ARM_PIV, -90), arm_t)
del mine_s['LTV2_ArmDetail']

trees = {k: bvh(*v) for k, v in existing.items()}
mtrees = {k: bvh(*v) for k, v in mine_s.items()}


def mindist(vs, tree, cap=3.0):
    best = cap
    for p in vs:
        r = tree.find_nearest(p, cap)
        if r[0] is not None and r[3] < best:
            best = r[3]
    return best


# which existing structure a part of mine may legitimately touch (attached by design) -> reported, not hidden
tests = []
for mk in ('LTV2_TowerFacade', 'LTV2_DeckSupport', 'LTV2_Deluge'):
    for ek in ('AccessMastTower', 'CapsuleShell', 'BoosterStack', 'HatchOpen160', 'HatchClosed', 'AccessArmConnected', 'AccessArmRetracted', 'DockDeckModule'):
        tests.append((mk, ek))
for ek in ('AccessMastTower', 'CapsuleShell', 'BoosterStack', 'HatchOpen160', 'HatchClosed', 'AccessArmConnected', 'DockDeckModule'):
    tests.append(('LTV2_ArmDetail@connected', ek))
for ek in ('AccessMastTower', 'AccessArmRetracted', 'DockDeckModule', 'CapsuleShell'):
    tests.append(('LTV2_ArmDetail@retracted', ek))
for mk in ('LTV2_TowerFacade', 'LTV2_DeckSupport', 'LTV2_Deluge'):
    tests.append((mk, 'ArmDetail@retracted'))
    tests.append((mk, 'ArmDetail@connected'))

res = []
for mk, ek in tests:
    if ek.startswith('ArmDetail@'):
        other = mtrees['LTV2_' + ek]
        ov = mtrees[mk].overlap(other)
        md = mindist(mine_s[mk][0], other)
    else:
        ov = mtrees[mk].overlap(trees[ek])
        md = mindist(mine_s[mk][0], trees[ek])
    cells = {}
    mv, mt_ = mine_s[mk]
    for a, b in ov:
        c = sum((mv[i] for i in mt_[a]), Vector()) / 3
        key = (round(c.x / 1.5), round(c.y / 1.5), round(c.z / 1.5))
        cells[key] = cells.get(key, 0) + 1
    top = sorted(cells.items(), key=lambda kv: -kv[1])[:4]
    top = [dict(center_m=[round(k[0] * 1.5, 1), round(k[1] * 1.5, 1), round(k[2] * 1.5, 1)], pairs=n) for k, n in top]
    res.append(dict(mine=mk, existing=ek, overlapping_triangle_pairs=len(ov), min_vertex_distance_m=round(md, 4), where=top))
    if top:
        print('   at', top)
    print('%-28s %-22s overlaps %6d  min vertex dist %.4f m' % (mk, ek, len(ov), md))
# keep-clear corridors (Unity metres, root frame): nothing of mine may be inside
CORR = {
    'arm_walkway_x+-0.93_y0.02..2.2_z-6.9..-2.5': ((-0.93, 0.02, -6.9), (0.93, 2.2, -2.5)),
    'deck_walking_area_x+-3.9_y0.05..2.5_z-12.8..-7.0': ((-3.9, 0.05, -12.8), (3.9, 2.5, -7.0)),
    'hatch_doorway_x+-0.95_y0.02..2.2_z-3.4..-2.3': ((-0.95, 0.02, -3.4), (0.95, 2.2, -2.3)),
}
corr = []
for name, (mn, mx) in CORR.items():
    for mk in ('LTV2_TowerFacade', 'LTV2_DeckSupport', 'LTV2_Deluge', 'LTV2_ArmDetail@connected'):
        n = sum(1 for p in mine_s[mk][0] if mn[0] < p.x < mx[0] and mn[1] < p.y < mx[1] and mn[2] < p.z < mx[2])
        bx = [Vector((x, y, z)) for x in (mn[0], mx[0]) for y in (mn[1], mx[1]) for z in (mn[2], mx[2])]
        quads = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
        cb = BVHTree.FromPolygons([tuple(v) for v in bx], [t for q in quads for t in ((q[0], q[1], q[2]), (q[0], q[2], q[3]))])
        crossing = len(mtrees[mk].overlap(cb))
        corr.append(dict(corridor=name, object=mk, vertices_inside=n, surfaces_crossing=crossing))
        print('CORRIDOR %-52s %-26s vertices inside: %d  surfaces crossing: %d' % (name, mk, n, crossing))
out = dict(poses='dock at (0,-0.021,-9.9); hatch open 160 deg about (-0.72,0,-2.4); arm retracted = -90 deg about (-0.9,0,-6.9); sea-level dock pieces disabled',
           pairs=res, corridors=corr)
json.dump(out, open(OUT, 'w'), indent=1)
