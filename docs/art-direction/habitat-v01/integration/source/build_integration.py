"""build_integration - HABITAT-FIT-02: a derivative AURORA-7 Storage shell with the habitat kit placed (original work).

Codex asked on 14 November 2026, 17:42 UTC (#7 comment 6881635025) for a physically placeable layout without
touching the live ship: the Storage room rebuilt by the ship's own generator with the combined soft and hard
stowage walls removed, bays left [blanket, -, -, blanket] and right [blanket, blanket, -, blanket], and the three
habitat units at fixed ship-space positions. The live Aurora7 assets are not changed.

The derivative is made by swapping a7_ship.build_storage for one call of the generator, so the shared label
sequence (deck IDs, signs) is consumed exactly as in the live build; every other ship model stays identical.

Outputs in <out>: A7_Storage_Habitat.fbx (derivative shell), HB_Habitat_Placed.fbx (the 7 kit objects and 7 anchor
empties in ship space, pivots on their hinge or slide lines), HB_Habitat_Collision.fbx (added collision boxes),
HB_StorageHabitat.blend (all of it, textures relative in ./textures), hb_integration_report.json (for the round
trip) and hb_integration_manifest.json (placements, anchors with forward directions, collider changes, clearances).
--render writes Cycles stills of this exact layout into <out>/renders.

Run: blender -b --factory-startup --python build_integration.py -- --out <dir> --kit <habitat out dir>
     --ship-source <Aurora7/Source at ba8b87f> --ship-textures <Aurora7/Textures> [--render] [--samples 96]
"""
import json
import math
import os
import shutil
import sys

import bpy
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.dont_write_bytecode = True
KIT_SOURCE = os.path.normpath(os.path.join(HERE, "..", "..", "source"))   # in the repo: habitat-v01/source
for folder in (KIT_SOURCE, HERE):
    if os.path.isdir(folder):
        sys.path.insert(0, folder)
import a7kit  # noqa: E402
import build_habitat as bh  # noqa: E402  (helpers only; its main does not run on import)
import hb_kit  # noqa: E402

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def arg(name, default=None):
    if name in ARGS:
        i = ARGS.index(name)
        return ARGS[i + 1] if i + 1 < len(ARGS) and not ARGS[i + 1].startswith("--") else True
    return default


OUT = os.path.abspath(arg("--out"))
KIT = os.path.abspath(arg("--kit"))
SHIP_SOURCE = os.path.abspath(arg("--ship-source"))
SHIP_TEXTURES = os.path.abspath(arg("--ship-textures"))
RENDER = bool(arg("--render", False))
SAMPLES = int(arg("--samples", 96))
TEXTURES = os.path.join(OUT, "textures")

PLACEMENT = {   # Codex, 17:42 UTC: ship space (= Aurora7 prefab space), yaw about +y in degrees
    "HB_CrewBerth": ((-2.38, 0.0, 13.25), 90.0),
    "HB_Galley": ((-2.38, 0.0, 14.75), 90.0),
    "HB_GrowCassette": ((2.38, 0.0, 14.75), -90.0),
}
LEFT_BAYS = ("blanket", None, None, "blanket")      # bay centres z 11.75, 13.25, 14.75, 16.25 on the -x wall
RIGHT_BAYS = ("blanket", "blanket", None, "blanket")
REMOVED_COLLIDERS = ("COL_Storage_LockersL", "COL_Storage_LockersR")

sys.path.insert(0, SHIP_SOURCE)
import a7_ship  # noqa: E402


def placement_matrix(unit_name):
    (x, y, z), yaw = PLACEMENT[unit_name]
    return a7kit.compose(a7kit.at(x, y, z), a7kit.rotation((0, 1, 0), yaw))


def to_ship(matrix, p):
    return tuple(float(round(c, 5)) for c in (matrix @ np.append(np.asarray(p, dtype=float), 1.0))[:3])


def dir_to_ship(matrix, v):
    return tuple(float(round(c, 5)) + 0.0 for c in (matrix[:3, :3] @ np.asarray(v, dtype=float)))


def aabb(matrix, lo, hi):
    corners = np.array([[x, y, z] for x in (lo[0], hi[0]) for y in (lo[1], hi[1]) for z in (lo[2], hi[2])])
    pts = np.array([to_ship(matrix, c) for c in corners])
    a, b = pts.min(axis=0), pts.max(axis=0)
    return [round(float(c), 4) for c in (a + b) / 2], [round(float(c), 4) for c in b - a]


def ship_models():
    with open(os.path.join(SHIP_TEXTURES, "a7_decals.json"), encoding="utf-8") as handle:
        decals = json.load(handle)
    with open(os.path.join(SHIP_TEXTURES, "a7_screens.json"), encoding="utf-8") as handle:
        screens = json.load(handle)
    original = a7_ship.build_storage

    def build_storage(art, bays):
        m = a7kit.Model("A7_Storage_Habitat")
        a7_ship.build_shell(m, art, "Storage", [bays[n] if n else None for n in LEFT_BAYS],
                            [bays[n] if n else None for n in RIGHT_BAYS], "sign_logistics")
        return m

    a7_ship.build_storage = build_storage
    try:
        models, boxes = a7_ship.build_all(decals, screens)
    finally:
        a7_ship.build_storage = original
    return models, boxes


def kit_atlas():
    with open(os.path.join(KIT, "textures", "hb_labels.json"), encoding="utf-8") as handle:
        return json.load(handle)


def collect_textures(names, ship_specs, kit_specs):
    """Copies every map the scene uses into <out>/textures, so the .blend is complete with relative paths."""
    os.makedirs(TEXTURES, exist_ok=True)
    copied = []
    for name in sorted(names):
        spec = kit_specs.get(name) or ship_specs.get(name)
        files = []
        if "set" in spec:
            files = [spec["set"] + s for s in ("_BaseColor.png", "_Normal.png", "_MODS.png")]
        if "atlas" in spec:
            files = [spec["atlas"] + ".png"]
        for f in files:
            src = os.path.join(KIT, "textures", f) if f.startswith("HB_") else os.path.join(SHIP_TEXTURES, f)
            if not os.path.exists(os.path.join(TEXTURES, f)):
                shutil.copyfile(src, os.path.join(TEXTURES, f))
                copied.append(f)
    return copied


def box_object(name, centre, size, material, collection):
    v, t = a7kit.box(size, 0.0)
    m = a7kit.Model(name)
    m.add("A7_Collision", v + np.asarray(centre), t)
    obj, info = bh.make_object(name, m, {"A7_Collision": material}, collection, centre)
    obj.display_type = "WIRE"
    obj.hide_render = True
    return obj, info


def build():
    bh.clear_scene()
    models, live_boxes = ship_models()
    shell = models["Storage"]
    kit = hb_kit.build_kit(kit_atlas())
    with open(os.path.join(SHIP_SOURCE, "a7_materials.json"), encoding="utf-8") as handle:
        ship_specs = {e["name"]: e for e in json.load(handle)["materials"]}
    spec_folder = KIT_SOURCE if os.path.exists(os.path.join(KIT_SOURCE, "hb_materials.json")) else HERE
    with open(os.path.join(spec_folder, "hb_materials.json"), encoding="utf-8") as handle:
        kit_specs = {e["name"]: e for e in json.load(handle)["materials"]}
    used = set(shell.parts) | {"A7_Collision"}
    for unit in kit.values():
        for model, _, _ in unit["objects"].values():
            used |= set(model.parts)
    copied = collect_textures(used - {"A7_Collision"}, ship_specs, kit_specs)
    materials = {}
    for name in sorted(used):
        materials[name] = bh.build_material(name, kit_specs.get(name) or ship_specs[name], TEXTURES)

    report = {"units": {}}
    manifest = {"task": "HABITAT-FIT-02", "base": "AURORA-7 ship generator at ba8b87f (the ship inside 33f474f)",
                "space": "ship space = Aurora7_Models prefab space: Unity metres, y up, +z aft", "units": {},
                "anchors": {}, "colliders": {}, "clearance_m": {}}
    # 1. derivative shell
    coll = bpy.data.collections.new("A7_Storage_Habitat")
    bpy.context.scene.collection.children.link(coll)
    obj, info = bh.make_object("A7_Storage_Habitat", shell, materials, coll)
    bh.export_fbx([obj], os.path.join(OUT, "A7_Storage_Habitat.fbx"))
    report["units"]["A7_Storage_Habitat"] = {"fbx": "A7_Storage_Habitat.fbx", "objects": {"A7_Storage_Habitat": info},
                                             "anchors": {}}
    manifest["storage_shell"] = {
        "fbx": "A7_Storage_Habitat.fbx", "object": "A7_Storage_Habitat", "replaces": "A7_Storage (live, unchanged)",
        "triangles": info["triangles"], "bays_left": list(LEFT_BAYS), "bays_right": list(RIGHT_BAYS),
        "removed": ["soft stowage wall on the -x side (soft bags, transit cases, lockers; z 11.75-16.25)",
                    "hard stowage wall on the +x side (lockers and hard cases; z 11.75-16.25)"],
        "kept": "deck, ceiling, side walls, ring frames, blanket bays as listed, the z 11 portal and its signs"}
    # 2. placed kit objects and anchors
    coll = bpy.data.collections.new("HB_Habitat_Placed")
    bpy.context.scene.collection.children.link(coll)
    placed_objects, placed_info, anchors_info = [], {}, {}
    for unit_name, unit in kit.items():
        matrix = placement_matrix(unit_name)
        (pos, yaw) = PLACEMENT[unit_name]
        manifest["units"][unit_name] = {"position": list(pos), "yaw_deg": yaw, "fbx_unit_space": "../%s.fbx" % unit_name,
                                        "objects": {}}
        for name, (model, origin, motion) in unit["objects"].items():
            m = a7kit.Model(name)
            m.instance(model, matrix)
            pivot = to_ship(matrix, origin)
            obj, data = bh.make_object(name, m, materials, coll, pivot)
            if motion:
                motion = dict(motion)
                if motion.get("axis") in ("x", "z"):
                    local = {"x": (1, 0, 0), "z": (0, 0, 1)}[motion["axis"]]
                    motion["axis_ship"] = [round(c, 4) + 0.0 for c in dir_to_ship(matrix, local)]
                data["motion"] = motion
            placed_info[name] = data
            placed_objects.append(obj)
            manifest["units"][unit_name]["objects"][name] = {"pivot_ship": list(pivot), "triangles": data["triangles"],
                                                             "motion": data.get("motion")}
        for name, (point, forward) in unit["anchors"].items():
            p = to_ship(matrix, point)
            f = [round(c, 4) + 0.0 for c in dir_to_ship(matrix, forward)]
            placed_objects.append(bh.make_anchor(name, p, coll))
            anchors_info[name] = {"point": [round(c, 4) for c in p], "forward": f}
            manifest["anchors"][name] = {"unit": unit_name, "point": [round(c, 4) for c in p], "forward": f}
    bh.export_fbx(placed_objects, os.path.join(OUT, "HB_Habitat_Placed.fbx"))
    report["units"]["HB_Habitat_Placed"] = {"fbx": "HB_Habitat_Placed.fbx", "objects": placed_info, "anchors": anchors_info}
    # 3. colliders: the two locker boxes go, habitat boxes come (unit-local envelopes from the delivered kit)
    envelopes = {   # unit space after placement in the room: x across, y up, z out of the wall
        "COL_Habitat_Berth": ("HB_CrewBerth", (-0.723, 0.0, 0.0), (0.723, 2.55, 0.535)),
        "COL_Habitat_Galley": ("HB_Galley", (-0.723, 0.0, 0.0), (0.723, 2.40, 0.47)),
        "COL_Habitat_GalleyDrawerOpen": ("HB_Galley", (0.02, 1.575, 0.47), (0.69, 1.775, 0.72)),
        "COL_Habitat_GalleyTable": ("HB_Galley", (-0.14, 0.754, 0.46), (0.70, 1.051, 0.803)),
        "COL_Habitat_GrowCassette": ("HB_GrowCassette", (-0.503, 0.40, 0.0), (0.503, 2.054, 0.525)),
    }
    coll = bpy.data.collections.new("HB_Habitat_Collision")
    bpy.context.scene.collection.children.link(coll)
    col_objects, col_info, added = [], {}, []
    for name, (unit_name, lo, hi) in envelopes.items():
        centre, size = aabb(placement_matrix(unit_name), lo, hi)
        obj, data = box_object(name, centre, size, materials["A7_Collision"], coll)
        col_objects.append(obj)
        col_info[name] = data
        added.append({"name": name, "unit": unit_name, "centre": centre, "size": size})
    bh.export_fbx(col_objects, os.path.join(OUT, "HB_Habitat_Collision.fbx"))
    report["units"]["HB_Habitat_Collision"] = {"fbx": "HB_Habitat_Collision.fbx", "objects": col_info, "anchors": {}}
    removed = [{"name": n, "centre": list(c), "size": list(s)} for n, c, s in live_boxes if n in REMOVED_COLLIDERS]
    manifest["colliders"] = {
        "remove": removed, "add": added,
        "notes": "Boxes cover each unit's envelope; the galley table and the open ration drawer are separate boxes "
                 "so root can disable them when the table is stowed or the drawer closed. All other live colliders "
                 "(Storage floor, ceiling, walls, portals) stay as they are."}
    # 4. clearances: narrowest free width across the room between left and right units
    def span(name):
        entry = next(a for a in added if a["name"] == name)
        c, s = np.array(entry["centre"]), np.array(entry["size"])
        return c - s / 2, c + s / 2
    berth, galley, table, drawer, grow = (span(n) for n in ("COL_Habitat_Berth", "COL_Habitat_Galley",
                                                            "COL_Habitat_GalleyTable", "COL_Habitat_GalleyDrawerOpen",
                                                            "COL_Habitat_GrowCassette"))
    manifest["clearance_m"] = {
        "galley_to_grow_stowed": round(float(grow[0][0] - galley[1][0]), 3),
        "galley_table_deployed_to_grow": round(float(grow[0][0] - table[1][0]), 3),
        "galley_table_deployed_to_grow_tray_out_0.30": round(float(grow[0][0] - 0.30 - table[1][0]), 3),
        "berth_to_right_wall_blanket_bay": round(float(2.38 - berth[1][0]), 3),
        "portal_wings_z": [10.91, 11.09, 16.91, 17.09],
        "units_z_extent": [round(float(berth[0][2]), 3), round(float(galley[1][2]), 3)],
        "ring_frame_flanges": "the graphite ring-frame flanges at z 12.5, 14.0 and 15.5 (0.07 m deep, centred on each rib) "
                              "enter the berth and galley side panels by 8 mm; hidden inside the panels, no collider involved",
    }
    # 5. the editable scene
    with open(os.path.join(OUT, "hb_integration_report.json"), "w", encoding="utf-8") as handle:
        json.dump(report, handle, indent=1)
    with open(os.path.join(OUT, "hb_integration_manifest.json"), "w", encoding="utf-8") as handle:
        json.dump(manifest, handle, indent=1)
    bpy.ops.file.make_paths_relative()
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "HB_StorageHabitat.blend"), compress=True)
    bh.log("built: shell %d tris, placed %d objects + %d anchors, %d colliders, %d maps copied" % (
        report["units"]["A7_Storage_Habitat"]["objects"]["A7_Storage_Habitat"]["triangles"], len(placed_info),
        len(anchors_info), len(col_info), len(copied)))
    return models, kit, materials


CAMERAS = {
    "fit_wide": ((1.30, 1.65, 11.35), (-1.40, 1.20, 15.20), 90),
    "fit_left_wall": ((1.25, 1.65, 14.0), (-2.38, 1.25, 14.0), 84),
    "fit_grow": ((-0.70, 1.55, 14.75), (2.38, 1.20, 14.75), 70),
    "fit_sleep_camera": None,
}


def render(models, kit, materials):
    collection = bpy.context.scene.collection
    for name, model in models.items():
        if name in ("Engineering", "Airlock", "Cockpit") or name.startswith("DoorLeaf"):
            for material in model.parts:
                if material not in materials:
                    with open(os.path.join(SHIP_SOURCE, "a7_materials.json"), encoding="utf-8") as handle:
                        specs = {e["name"]: e for e in json.load(handle)["materials"]}
                    materials[material] = bh.build_material(material, specs[material], SHIP_TEXTURES)
            bh.make_object("A7_" + name, model, materials, collection)
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    prefs = bpy.context.preferences.addons["cycles"].preferences
    for kind in ("OPTIX", "CUDA"):
        try:
            prefs.compute_device_type = kind
            prefs.get_devices()
            if any(d.type == kind for d in prefs.devices):
                for dev in prefs.devices:
                    dev.use = dev.type == kind
                scene.cycles.device = "GPU"
                break
        except (TypeError, RuntimeError):
            continue
    scene.cycles.samples = SAMPLES
    scene.cycles.use_denoising = True
    scene.cycles.max_bounces = 6
    scene.render.resolution_x, scene.render.resolution_y = 1600, 900
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = 0.6
    world = bpy.data.worlds.new("HB_FitWorld")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.012, 0.014, 0.02, 1.0)
    scene.world = world

    def light(kind, name, point, energy, colour, size=None, facing=(0.0, -1.0, 0.0)):
        data = bpy.data.lights.new(name, kind)
        data.energy, data.color = energy, colour
        if kind == "AREA":
            data.shape = "RECTANGLE"
            data.size, data.size_y = size
        else:
            data.shadow_soft_size = 0.02
        obj = bpy.data.objects.new(name, data)
        obj.location = bh.to_blender_point(point)
        obj.rotation_euler = bh.to_blender_point(facing).to_track_quat("-Z", "Y").to_euler()
        collection.objects.link(obj)

    for room_name, room in a7_ship.ROOMS.items():
        for z in room["fixtures"]:
            light("AREA", "HB_Ceiling_%s_%.1f" % (room_name, z), (0.0, a7_ship.CEILING - 0.135, z), 70.0,
                  (1.0, 0.92, 0.81), (1.1, 0.28))
    light("POINT", "HB_BerthLamp", to_ship(placement_matrix("HB_CrewBerth"), (-0.075, 2.05, 0.38)), 4.0, (1.0, 0.72, 0.45))
    light("AREA", "HB_GrowLight", to_ship(placement_matrix("HB_GrowCassette"), (0.0, 1.645, 0.25)), 22.0,
          (1.0, 0.86, 0.95), (0.36, 0.84))
    light("AREA", "HB_GalleyTask", to_ship(placement_matrix("HB_Galley"), (0.27, 1.53, 0.30)), 12.0,
          (1.0, 0.95, 0.88), (0.04, 0.68))
    eye, look = kit["HB_CrewBerth"]["anchors"]["SleepCamera"]
    m = placement_matrix("HB_CrewBerth")
    p = np.array(to_ship(m, eye))
    CAMERAS["fit_sleep_camera"] = (tuple(p), tuple(p + np.array(dir_to_ship(m, look))), 90)
    out_dir = os.path.join(OUT, "renders")
    os.makedirs(out_dir, exist_ok=True)
    for name, (position, target, fov) in CAMERAS.items():
        data = bpy.data.cameras.new("HB_Cam_" + name)
        data.sensor_fit = "HORIZONTAL"
        data.angle = math.radians(fov)
        data.clip_start = 0.05
        camera = bpy.data.objects.new("HB_Cam_" + name, data)
        camera.location = bh.to_blender_point(position)
        camera.rotation_euler = (bh.to_blender_point(target) - camera.location).to_track_quat("-Z", "Y").to_euler()
        collection.objects.link(camera)
        scene.camera = camera
        scene.render.filepath = os.path.join(out_dir, "HB_fit_%s.png" % name)
        bpy.ops.render.render(write_still=True)
        bh.log("rendered", name)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    built = build()
    if RENDER:
        render(*built)
    bh.log("done")
