"""build_habitat - Blender 5.3 background build of the HABITAT-DETAILS-V1 prop kit (original work).

Build mode (default): materials from hb_materials.json and <out>/textures, one FBX per unit (meshes and anchor empties
only: no cameras, lights or deep hierarchy), the editable HB_HabitatKit.blend with relative texture paths, and
hb_build_report.json (triangles per object and material, bounds, pivots, motions, anchors in Unity space).
Render mode (--render): the kit placed in AURORA-7's Storage room as one *proposed* layout, built from the ship's
own generator (--ship-source, --ship-textures), and Cycles stills into <out>/renders. Nothing is exported then.

Coordinates: geometry is generated in Unity space (x, y, z; metres, y up). The exporter maps Blender (x, y, z) to
Unity (x, z, y) with bake_space_transform, exactly like the AURORA-7 ship pipeline (a7kit.to_blender).

Run:
  blender -b --factory-startup --python build_habitat.py -- --out <out dir>
  blender -b --factory-startup --python build_habitat.py -- --out <out dir> --render --ship-source <Aurora7/Source>
          --ship-textures <Aurora7/Textures> [--samples 96] [--views a,b]
"""
import json
import math
import os
import sys
import time

import bpy
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.dont_write_bytecode = True
sys.path.insert(0, HERE)
import a7kit  # noqa: E402
import hb_kit  # noqa: E402

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
T0 = time.time()


def arg(name, default=None):
    if name in ARGS:
        i = ARGS.index(name)
        if i + 1 < len(ARGS) and not ARGS[i + 1].startswith("--"):
            return ARGS[i + 1]
        return True
    return default


OUT = os.path.abspath(arg("--out", os.path.join(HERE, "..", "out")))
TEXTURES = os.path.join(OUT, "textures")
RENDER = bool(arg("--render", False))
SAMPLES = int(arg("--samples", 96))
VIEWS = arg("--views", None)
SHIP_SOURCE = arg("--ship-source", None)
SHIP_TEXTURES = arg("--ship-textures", None)


def log(*parts):
    print("[hb %6.1fs]" % (time.time() - T0), *parts, flush=True)


def srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def hex_linear(text, alpha=1.0):
    text = text.lstrip("#")
    return tuple(srgb_to_linear(int(text[i:i + 2], 16) / 255.0) for i in (0, 2, 4)) + (alpha,)


def to_blender_point(p):
    return Vector((p[0], p[2], p[1]))


# ----------------------------------------------------------------------------- materials (same graph as the ship)
_IMAGES = {}


def image(folder, name, non_color=False, alpha_mode=None):
    key = (folder, name, non_color)
    if key not in _IMAGES:
        img = bpy.data.images.load(os.path.join(folder, name), check_existing=True)
        if non_color:
            img.colorspace_settings.name = "Non-Color"
        if alpha_mode:
            img.alpha_mode = alpha_mode
        _IMAGES[key] = img
    return _IMAGES[key]


def set_input(node, names, value):
    for name in names:
        if name in node.inputs:
            node.inputs[name].default_value = value
            return True
    return False


def build_material(name, spec, folder):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    tree = mat.node_tree
    nodes, links = tree.nodes, tree.links
    bsdf = next(n for n in nodes if n.type == "BSDF_PRINCIPLED")
    tint = hex_linear(spec.get("tint", "#FFFFFF"))
    set_input(bsdf, ["Base Color"], tint)
    set_input(bsdf, ["Metallic"], float(spec.get("metallic", 0.0)))
    set_input(bsdf, ["Roughness"], 1.0 - float(spec.get("smoothness", 0.5)))
    if "set" in spec:
        coord = nodes.new("ShaderNodeTexCoord")
        mapping = nodes.new("ShaderNodeMapping")
        tiling = float(spec.get("tiling", 1.0))
        mapping.inputs["Scale"].default_value = (tiling, tiling, tiling)
        links.new(coord.outputs["UV"], mapping.inputs["Vector"])
        base = nodes.new("ShaderNodeTexImage")
        base.image = image(folder, spec["set"] + "_BaseColor.png")
        links.new(mapping.outputs["Vector"], base.inputs["Vector"])
        mix = nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        mix.blend_type = "MULTIPLY"
        mix.inputs[0].default_value = 1.0
        links.new(base.outputs["Color"], mix.inputs[6])
        mix.inputs[7].default_value = tint
        links.new(mix.outputs[2], bsdf.inputs["Base Color"])
        mods = nodes.new("ShaderNodeTexImage")
        mods.image = image(folder, spec["set"] + "_MODS.png", True, "CHANNEL_PACKED")
        links.new(mapping.outputs["Vector"], mods.inputs["Vector"])
        separate = nodes.new("ShaderNodeSeparateColor")
        links.new(mods.outputs["Color"], separate.inputs[0])
        links.new(separate.outputs[0], bsdf.inputs["Metallic"])
        invert = nodes.new("ShaderNodeMath")
        invert.operation = "SUBTRACT"
        invert.inputs[0].default_value = 1.0
        links.new(mods.outputs["Alpha"], invert.inputs[1])
        links.new(invert.outputs[0], bsdf.inputs["Roughness"])
        normal_tex = nodes.new("ShaderNodeTexImage")
        normal_tex.image = image(folder, spec["set"] + "_Normal.png", True)
        links.new(mapping.outputs["Vector"], normal_tex.inputs["Vector"])
        normal_map = nodes.new("ShaderNodeNormalMap")
        normal_map.inputs["Strength"].default_value = float(spec.get("normal", 1.0))
        links.new(normal_tex.outputs["Color"], normal_map.inputs["Color"])
        links.new(normal_map.outputs["Normal"], bsdf.inputs["Normal"])
    if "atlas" in spec:
        tex = nodes.new("ShaderNodeTexImage")
        tex.image = image(folder, spec["atlas"] + ".png", False, "STRAIGHT")
        if spec.get("emission_from_atlas"):
            set_input(bsdf, ["Base Color"], (0.0, 0.0, 0.0, 1.0))
            links.new(tex.outputs["Color"], bsdf.inputs["Emission Color"])
            set_input(bsdf, ["Emission Strength"], float(spec.get("emission_strength", 1.0)))
        else:
            links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
            links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
    if "emission" in spec:
        set_input(bsdf, ["Emission Color"], hex_linear(spec["emission"]))
        set_input(bsdf, ["Emission Strength"], float(spec.get("emission_strength", 1.0)))
    if spec.get("surface") == "transparent":
        set_input(bsdf, ["Transmission Weight", "Transmission"], 1.0)
        set_input(bsdf, ["Roughness"], 0.03)
        set_input(bsdf, ["IOR"], 1.45 if "Glass" in name else 1.33)
    return mat


def load_materials(spec_path, folder, names=None, existing=None):
    with open(spec_path, encoding="utf-8") as handle:
        specs = {entry["name"]: entry for entry in json.load(handle)["materials"]}
    materials = dict(existing or {})
    for name, spec in specs.items():
        if (names is None or name in names) and name not in materials:
            materials[name] = build_material(name, spec, folder)
    return materials, specs


# ----------------------------------------------------------------------------- meshes and anchors
def make_object(name, model, materials, collection, origin=(0.0, 0.0, 0.0)):
    names = sorted(model.parts.keys())
    vs, ts, uvs, indices = [], [], [], []
    offset = 0
    counts = {}
    for index, material in enumerate(names):
        v, t, uv = model.arrays(material)
        vs.append(v - np.asarray(origin))
        ts.append(t + offset)
        uvs.append(uv)
        indices.append(np.full(len(t), index, dtype=np.int32))
        counts[material] = int(len(t))
        offset += len(v)
    v = np.concatenate(vs, axis=0)
    t = np.concatenate(ts, axis=0)
    uv = np.concatenate(uvs, axis=0)
    material_index = np.concatenate(indices, axis=0)
    vb, tb = a7kit.to_blender(v, t)
    uvb = uv[:, ::-1, :]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vb.tolist(), [], tb.tolist())
    mesh.polygons.foreach_set("material_index", material_index)
    mesh.polygons.foreach_set("use_smooth", np.ones(len(tb), dtype=bool))
    layer = mesh.uv_layers.new(name="UVMap")
    layer.data.foreach_set("uv", uvb.astype(np.float32).ravel())
    for material in names:
        mesh.materials.append(materials[material])
    mesh.update(calc_edges=True)
    mesh.set_sharp_from_angle(angle=math.radians(50.0))
    obj = bpy.data.objects.new(name, mesh)
    obj.location = to_blender_point(origin)
    collection.objects.link(obj)
    weighted = obj.modifiers.new("WeightedNormal", "WEIGHTED_NORMAL")
    weighted.keep_sharp = True
    weighted.weight = 50
    lo, hi = v.min(axis=0) + np.asarray(origin), v.max(axis=0) + np.asarray(origin)
    return obj, {"triangles": int(len(t)), "per_material": counts, "bounds_min": [round(float(x), 4) for x in lo],
                 "bounds_max": [round(float(x), 4) for x in hi], "origin": [round(float(x), 4) for x in origin]}


def make_anchor(name, point, collection):
    empty = bpy.data.objects.new(name, None)
    empty.empty_display_type = "PLAIN_AXES"
    empty.empty_display_size = 0.08
    empty.location = to_blender_point(point)
    collection.objects.link(empty)
    return empty


def export_fbx(objects, path):
    """Meshes and empties only, no texture records (Unity binds the materials by name; this also keeps the
    build machine's absolute texture paths out of the file)."""
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.hide_set(False)
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    detached = []
    for material in bpy.data.materials:
        if material.use_nodes:
            for node in material.node_tree.nodes:
                if node.type == "TEX_IMAGE" and node.image is not None:
                    detached.append((node, node.image))
                    node.image = None
    try:
        bpy.ops.export_scene.fbx(
            filepath=path, use_selection=True, object_types={"MESH", "EMPTY"}, use_mesh_modifiers=True,
            mesh_smooth_type="OFF", apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
            bake_space_transform=True, axis_forward="-Z", axis_up="Y", add_leaf_bones=False, bake_anim=False,
            path_mode="STRIP", embed_textures=False, use_tspace=False)
    finally:
        for node, picture in detached:
            node.image = picture


def clear_scene():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        for item in list(block):
            block.remove(item)


def load_atlas():
    with open(os.path.join(TEXTURES, "hb_labels.json"), encoding="utf-8") as handle:
        return json.load(handle)


# ----------------------------------------------------------------------------- build mode
def build():
    clear_scene()
    kit = hb_kit.build_kit(load_atlas())
    used = sorted({mat for unit in kit.values() for (model, _, _) in unit["objects"].values() for mat in model.parts})
    materials, specs = load_materials(os.path.join(HERE, "hb_materials.json"), TEXTURES, set(used))
    missing = [m for m in used if m not in materials]
    if missing:
        raise SystemExit("materials without a spec: %s" % missing)
    report = {"units": {}, "materials": used, "total_triangles": 0,
              "convention": "Unity space, metres, y up; unit forward +z points into the room; origin on the wall "
                            "plane at deck level under the unit centre line; mount by rotating about y only"}
    for unit_name, unit in kit.items():
        collection = bpy.data.collections.new(unit_name)
        bpy.context.scene.collection.children.link(collection)
        objects, info = [], {"objects": {}, "anchors": {}}
        for name, (model, origin, motion) in unit["objects"].items():
            obj, data = make_object(name, model, materials, collection, origin)
            if motion:
                data["motion"] = motion
            info["objects"][name] = data
            objects.append(obj)
            report["total_triangles"] += data["triangles"]
        for name, (point, forward) in unit["anchors"].items():
            objects.append(make_anchor(name, point, collection))
            info["anchors"][name] = {"point": [round(c, 4) for c in point], "forward": [round(c, 4) for c in forward]}
        lo = np.min([o["bounds_min"] for o in info["objects"].values()], axis=0)
        hi = np.max([o["bounds_max"] for o in info["objects"].values()], axis=0)
        info["bounds_min"], info["bounds_max"] = [round(float(c), 4) for c in lo], [round(float(c), 4) for c in hi]
        info["triangles"] = sum(o["triangles"] for o in info["objects"].values())
        path = os.path.join(OUT, unit_name + ".fbx")
        export_fbx(objects, path)
        info["fbx"] = os.path.basename(path)
        report["units"][unit_name] = info
        log("exported", os.path.basename(path), info["triangles"], "triangles,", len(info["anchors"]), "anchors")
    with open(os.path.join(OUT, "hb_build_report.json"), "w", encoding="utf-8") as handle:
        json.dump(report, handle, indent=1)
    bpy.ops.file.make_paths_relative()
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "HB_HabitatKit.blend"), compress=True)
    log("saved HB_HabitatKit.blend; total", report["total_triangles"], "triangles")


# ----------------------------------------------------------------------------- render mode (proposed placement)
LAYOUT = {   # ship space; the kit is not part of the ship, this is one possible arrangement for review
    "HB_CrewBerth": ((-2.38, 0.0, 14.75), 90.0),
    "HB_Galley": ((-2.38, 0.0, 16.25), 90.0),
    "HB_GrowCassette": ((2.38, 0.0, 14.75), -90.0),
}
CAMERAS = {
    "habitat_wide": ((0.75, 1.65, 11.35), (-0.55, 1.25, 16.6), 80),
    "habitat_galley_fp": ((-1.10, 1.62, 15.45), (-2.25, 1.22, 16.35), 70),
    "habitat_berth_fp": ((-0.80, 1.62, 14.45), (-2.38, 1.40, 14.62), 76),
    "habitat_grow_fp": ((1.25, 1.50, 14.05), (2.32, 1.15, 14.80), 70),
    "habitat_sleep_camera": None,   # from the SleepCamera anchor, looking along its forward
}
STILL_SIZE = {"habitat_sleep_camera": 90}


def render():
    clear_scene()
    sys.path.insert(0, os.path.abspath(SHIP_SOURCE))
    import a7_ship  # noqa: E402
    with open(os.path.join(SHIP_TEXTURES, "a7_decals.json"), encoding="utf-8") as handle:
        decals = json.load(handle)
    with open(os.path.join(SHIP_TEXTURES, "a7_screens.json"), encoding="utf-8") as handle:
        screens = json.load(handle)
    ship_models, _ = a7_ship.build_all(decals, screens)
    art = a7_ship.Art(decals, screens)
    blanket = a7_ship.bay_blanket(art)
    shell = a7kit.Model("Storage_ProposedLayout")
    # bays left empty where a kit unit is mounted (bay centres 11.75, 13.25, 14.75, 16.25)
    a7_ship.build_shell(shell, art, "Storage", [blanket, blanket, None, None], [blanket, blanket, None, blanket],
                        "sign_logistics")
    scene_models = {"Storage_ProposedLayout": shell}
    for name, model in ship_models.items():
        if name in ("Engineering", "Airlock", "Cockpit") or name.startswith("DoorLeaf"):
            scene_models["A7_" + name] = model
    kit = hb_kit.build_kit(load_atlas())
    for unit_name, unit in kit.items():
        (x, y, z), turn = LAYOUT[unit_name]
        place = a7kit.compose(a7kit.at(x, y, z), a7kit.rotation((0, 1, 0), turn))
        for name, (model, _, _) in unit["objects"].items():
            placed = a7kit.Model(name)
            placed.instance(model, place)
            scene_models[name] = placed
    materials, _ = load_materials(os.path.join(SHIP_SOURCE, "a7_materials.json"), SHIP_TEXTURES)
    materials, _ = load_materials(os.path.join(HERE, "hb_materials.json"), TEXTURES, None, materials)
    collection = bpy.context.scene.collection
    for name, model in scene_models.items():
        make_object(name, model, materials, collection)
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    device = "CPU"
    try:
        prefs = bpy.context.preferences.addons["cycles"].preferences
        for kind in ("OPTIX", "CUDA"):
            try:
                prefs.compute_device_type = kind
                prefs.get_devices()
                usable = [d for d in prefs.devices if d.type == kind]
                if usable:
                    for dev in prefs.devices:
                        dev.use = dev.type == kind
                    scene.cycles.device = "GPU"
                    device = kind + ": " + usable[0].name
                    break
            except (TypeError, RuntimeError):
                continue
    except KeyError:
        pass
    log("render device", device)
    scene.cycles.samples = SAMPLES
    scene.cycles.use_denoising = True
    scene.cycles.max_bounces = 6
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 900
    scene.render.image_settings.file_format = "PNG"
    try:
        scene.view_settings.view_transform = "AgX"
        scene.view_settings.look = "AgX - Medium High Contrast"
    except TypeError:
        pass
    scene.view_settings.exposure = 0.6
    world = bpy.data.worlds.new("HB_PreviewWorld")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.012, 0.014, 0.02, 1.0)
    scene.world = world

    def area(name, point, size, energy, colour, facing=(0.0, -1.0, 0.0)):
        data = bpy.data.lights.new(name, "AREA")
        data.shape = "RECTANGLE"
        data.size, data.size_y = size
        data.energy = energy
        data.color = colour
        light = bpy.data.objects.new(name, data)
        light.location = to_blender_point(point)
        light.rotation_euler = to_blender_point(facing).to_track_quat("-Z", "Y").to_euler()
        collection.objects.link(light)

    for room_name, room in a7_ship.ROOMS.items():
        for z in room["fixtures"]:
            area("HB_Ceiling_%s_%.1f" % (room_name, z), (0.0, a7_ship.CEILING - 0.135, z), (1.1, 0.28), 70.0,
                 (1.0, 0.92, 0.81))
    for unit_name, unit in kit.items():
        (x, y, z), turn = LAYOUT[unit_name]
        place = a7kit.compose(a7kit.at(x, y, z), a7kit.rotation((0, 1, 0), turn))

        def world(p):
            return tuple((place @ np.append(np.asarray(p, dtype=float), 1.0))[:3])
        if unit_name == "HB_CrewBerth":
            data = bpy.data.lights.new("HB_BerthLamp", "POINT")
            data.energy, data.color, data.shadow_soft_size = 4.0, (1.0, 0.72, 0.45), 0.02
            lamp = bpy.data.objects.new("HB_BerthLamp", data)
            lamp.location = to_blender_point(world((-0.075, 2.05, 0.38)))
            collection.objects.link(lamp)
        if unit_name == "HB_GrowCassette":
            area("HB_GrowLight", world((0.0, 1.645, 0.25)), (0.84, 0.36), 22.0, (1.0, 0.86, 0.95))
        if unit_name == "HB_Galley":
            area("HB_GalleyTask", world((0.27, 1.53, 0.30)), (0.68, 0.04), 12.0, (1.0, 0.95, 0.88))
    out_dir = os.path.join(OUT, "renders")
    os.makedirs(out_dir, exist_ok=True)
    wanted = VIEWS.split(",") if isinstance(VIEWS, str) else list(CAMERAS)
    (bx, by, bz), bturn = LAYOUT["HB_CrewBerth"]
    berth_place = a7kit.compose(a7kit.at(bx, by, bz), a7kit.rotation((0, 1, 0), bturn))
    eye, look = kit["HB_CrewBerth"]["anchors"]["SleepCamera"]
    eye_world = (berth_place @ np.append(np.asarray(eye), 1.0))[:3]
    look_world = berth_place[:3, :3] @ np.asarray(look)
    CAMERAS["habitat_sleep_camera"] = (tuple(eye_world), tuple(eye_world + look_world), STILL_SIZE["habitat_sleep_camera"])
    for name in wanted:
        position, target, fov = CAMERAS[name]
        data = bpy.data.cameras.new("HB_Cam_" + name)
        data.sensor_fit = "HORIZONTAL"
        data.angle = math.radians(fov)
        data.clip_start = 0.05
        camera = bpy.data.objects.new("HB_Cam_" + name, data)
        camera.location = to_blender_point(position)
        camera.rotation_euler = (to_blender_point(target) - camera.location).to_track_quat("-Z", "Y").to_euler()
        collection.objects.link(camera)
        scene.camera = camera
        scene.render.filepath = os.path.join(out_dir, "HB_blender_%s.png" % name)
        bpy.ops.render.render(write_still=True)
        log("rendered", name)


if __name__ == "__main__":
    if RENDER:
        render()
    else:
        build()
    log("done")
