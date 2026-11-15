"""Writes anchors.json (Unity metres, I06 prologue root frame) from the build report, the clearance scan and tw_layout constants.
python -I make_anchors.py <LaunchTowerV02 folder>"""
import json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import tw_layout as L

root = sys.argv[1]
rep = json.load(open(os.path.join(root, 'Models', 'LaunchTowerV02_report.json')))
clr = json.load(open(os.path.join(root, 'Models', 'LaunchTowerV02_clearance.json')))
Y0 = L.PAD_Y
rx, rz = L.LX + 0.34, L.CZ + 0.9
zt = L.CZ - L.LZ - L.LEG / 2


def pt(x, y, z, fwd=(0, 0, 1), note=''):
    return dict(point=[round(x, 3), round(y, 3), round(z, 3)], forward=list(fwd), note=note)


anchors = {
    'ArmPivot': pt(*L.ARM_PIVOT, (0, 1, 0), 'AccessArmPivot of TransportLaunch; LTV2_ArmDetail origin. Parent LTV2_ArmDetail under the "Access arm control" frame; it rotates with the arm (+Y 0 connected, -90 retracted).'),
    'ArmHoodCentre': pt(0, 1.0, -3.36, (0, 0, 1), 'crew connection hood around the arm head; clear opening x +-1.0, y -0.62..2.26 (boot rings) and the hatch is never covered'),
    'ArmChainStart': pt(1.14, 0.12, -6.45, (0, 0, 1), 'energy chain and fluid lines on the +x outside of the arm'),
    'DelugeRiserBase': pt(rx, Y0 + 0.45, rz, (0, 1, 0), 'DN200 riser foot on the pad; valve V-07 at +x'),
    'DelugeValveV07': pt(rx + 1.9, Y0 + 0.45, rz, (1, 0, 0), 'isolation valve with handwheel'),
    'DrencherHeaderCorner': pt(3.55, -0.98, -12.45, (0, 0, 1), 'drencher header under the deck, 6 + 8 downward heads'),
    'ScupperPlusX': pt(3.25, -0.58, -12.35, (0, -1, 0), 'deck scupper, downpipe to the pad catch basin'),
    'ScupperMinusX': pt(-3.25, -0.58, -12.35, (0, -1, 0), 'deck scupper, downpipe to the pad catch basin'),
    'CatchBasinPlusX': pt(L.LX + 0.38, Y0, L.CZ - L.LZ - 0.38, (0, 1, 0), 'grated catch basin at the foot of the downpipe'),
    'CatchBasinMinusX': pt(-(L.LX + 0.38), Y0, L.CZ - L.LZ - 0.38, (0, 1, 0), 'grated catch basin at the foot of the downpipe'),
    'LadderFoot': pt(-(L.LX + L.LEG / 2 + 0.30), Y0 + 0.25, L.CZ, (-1, 0, 0), 'caged service ladder on the -x face, passive'),
    'RearLandingL4': pt(0, Y0 + 12.0, zt - 0.55, (0, 0, -1), 'grated landing, 2.5 x 1.1 m, ochre rails'),
    'RearLandingL7': pt(0, Y0 + 21.0, zt - 0.55, (0, 0, -1), 'grated landing'),
    'RearLandingL10': pt(0, Y0 + 30.0, zt - 0.55, (0, 0, -1), 'grated landing under the deck overhang'),
    'CableTrayFootMinusX': pt(-1.7, Y0 + 1.1, zt - 0.16, (0, 0, -1), 'cable tray C-1, vertical, to a junction box under the deck'),
    'CableTrayFootPlusX': pt(1.7, Y0 + 1.1, zt - 0.16, (0, 0, -1), 'cable tray, vertical'),
    'TowerSignPlate': pt(0, -4.3, zt - 0.012, (0, 0, -1), 'AURORA-7 / CREW ACCESS TOWER plate, 2.6 m wide'),
}
out = {
    'asset': 'LaunchTowerV02', 'task': 'TOWER/BOARDING-BRIDGE-V02', 'kind': 'PASSIVE exterior detail, no colliders, no scripts, no scene edits',
    'frame': 'Unity metres, y up, root = the I06 prologue root (the same frame as MarinePrologue/Transport/TransportLaunch.fbx and MarineLayout.json). Instantiate the FBX at identity next to the TransportLaunch instance.',
    'importer': {'bakeAxisConversion': True, 'globalScale': 1, 'useFileScale': True, 'preserveHierarchy': True, 'importAnimation': False,
                 'materials': 'TW_*.mat remapped through externalObjects (materialImportMode ImportViaMaterialDescription)'},
    'placement': {
        'static': ['LTV2_TowerFacade', 'LTV2_DeckSupport', 'LTV2_Deluge'],
        'moving_with_arm': 'LTV2_ArmDetail: origin = AccessArmPivot; make it a child of "Access arm control +Y 0 connected to -90 retracted" so it swings with AccessArm',
        'colliders': 'none. Existing COL_* boxes stay the only collision; nothing here is a gameplay surface',
        'disabled_existing_pieces_assumed': 'the 72 sea-level Support*/Marine fender*/Fender* dock pieces stay disabled as in I06MarineAuthoring.ApplyElevatedDeckVariant',
    },
    'measured_existing': {
        'pad_top_y': Y0, 'shaft': {'x': [-L.SHAFT_X, L.SHAFT_X], 'z': [L.SHAFT_Z0, L.SHAFT_Z1], 'ribs_every_m': 3.0, 'top_slab_y': [-0.67, 0.2]},
        'deck': {'x': [-L.DECK_X, L.DECK_X], 'z': [L.DECK_Z0, L.DECK_Z1], 'top_y': 0.0, 'dock_root': [0, -0.021, -9.9]},
        'arm': {'x': [-L.ARM_X, L.ARM_X], 'z': [L.ARM_Z0, L.ARM_Z1], 'pivot': list(L.ARM_PIVOT), 'floor_underside_y': -0.55, 'rail_top_y': 1.128},
        'hatch': {'pivot': list(L.HATCH_PIVOT), 'open_deg': 160, 'size': [1.43, 2.11]},
        'capsule_shell_radius': 2.577, 'booster_radius': 2.895,
    },
    'design_rules_kept': {
        'facade_offset_from_shaft_m': 0.5, 'facade_min_gap_to_booster_m': '>=3.0 (clearance scan cap)',
        'arm_walkway_keep_clear': 'x +-0.93, y 0.02..2.2, z -6.9..-2.5', 'deck_keep_clear': 'x +-3.9, y 0.05..2.5, z -12.8..-7.0',
        'hatch_doorway_keep_clear': 'x +-0.95, y 0.02..2.2, z -3.4..-2.3',
        'rocket_side_legs_stop_at_y': L.Y_TOP_LEG_FRONT, 'why': 'the arm truss sweeps through them when the arm retracts (-90 deg)',
        'fluid_lines_and_chain_side': '+x, so a retracted arm does not push them into the deck edge rails',
    },
    'objects': {k: dict(tris=v['tris'], verts=v['verts'], origin=v['origin_unity'], bounds_min=v['bounds_min'], bounds_max=v['bounds_max'], tris_by_material=v['tris_by_material'])
                for k, v in rep['objects'].items()},
    'total_triangles': rep['total_tris'],
    'anchors': anchors,
    'clearance_scan': {'file': 'Models/LaunchTowerV02_clearance.json', 'poses': clr['poses'],
                       'corridor_violations': sum(c['vertices_inside'] + c['surfaces_crossing'] for c in clr['corridors']),
                       'note': 'overlapping triangle pairs listed there are by-design attachment contacts (seat plates, flanges, bearing ring, hanger rods, cladding battens on the shaft ribs); no walking corridor is touched'},
    'labels': 'fictional AURORA-7 plates only; no agency logos',
}
json.dump(out, open(os.path.join(root, 'anchors.json'), 'w'), indent=1)
print('anchors.json written', len(anchors), 'anchors, corridor violations', out['clearance_scan']['corridor_violations'])
