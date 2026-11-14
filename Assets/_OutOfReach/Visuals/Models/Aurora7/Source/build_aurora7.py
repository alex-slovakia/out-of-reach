"""
OUT OF REACH - AURORA-7 model build (Blender 5.3 entry point). Original work, generated from code.

    blender -b --factory-startup --python build_aurora7.py -- [--work DIR] [--render] [--views a,b] [--samples N] [--no-export] [--hide a,b]

Reads   ../Textures/a7_decals.json, a7_screens.json and a7_materials.json (run a7_textures.py first)
Writes  ../Meshes/A7_<Module>.fbx          Unity-ready static meshes (metres, Y up after import, normals included)
        ../Meshes/A7_<Group>.fbx           separately addressable objects with their own pivots (Doors, Parts) and helper points
        ../Meshes/a7_model_report.json     triangle / material counts and bounds, in Unity coordinates
        <work>/Aurora7.blend and <work>/renders/*.png   (outside the Unity project; --render only)

Nothing is downloaded and no third-party model or texture is used.
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
sys.path.insert(0, HERE)
sys.dont_write_bytecode = True  # keep __pycache__ out of the Unity project
import a7kit  # noqa: E402
import a7_ship  # noqa: E402

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
T0 = time.time()


def arg(name, default=None):
    if name in ARGS:
        i = ARGS.index(name)
        if i + 1 < len(ARGS) and not ARGS[i + 1].startswith("--"):
            return ARGS[i + 1]
        return True
    return default


TEXTURES = os.path.normpath(os.path.join(HERE, "..", "Textures"))
MESHES = os.path.normpath(os.path.join(HERE, "..", "Meshes"))
WORK = os.path.abspath(arg("--work", os.path.join(HERE, "..", "..", "..", "..", "..", "..", ".local", "aurora7-work")))
RENDER = bool(arg("--render", False))
EXPORT = not bool(arg("--no-export", False))   # --no-export renders stills without rewriting the FBX files
SAMPLES = int(arg("--samples", 96))
VIEWS = arg("--views", None)
HIDE = arg("--hide", "PowerCabinet_Cover")   # objects left out of the stills; the prefab keeps the cover inactive too


def log(*parts):
    print("[a7 %6.1fs]" % (time.time() - T0), *parts, flush=True)


def srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def hex_linear(text, alpha=1.0):
    text = text.lstrip("#")
    return tuple(srgb_to_linear(int(text[i:i + 2], 16) / 255.0) for i in (0, 2, 4)) + (alpha,)


def to_blender_point(p):
    return Vector((p[0], p[2], p[1]))


# ----------------------------------------------------------------------------- materials (preview quality)
_IMAGES = {}


def image(name, non_color=False, alpha_mode=None):
    key = (name, non_color)
    if key not in _IMAGES:
        img = bpy.data.images.load(os.path.join(TEXTURES, name), check_existing=True)
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


def build_material(name, spec):
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
        base.image = image(spec["set"] + "_BaseColor.png")
        links.new(mapping.outputs["Vector"], base.inputs["Vector"])
        mix = nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        mix.blend_type = "MULTIPLY"
        mix.inputs[0].default_value = 1.0
        links.new(base.outputs["Color"], mix.inputs[6])
        mix.inputs[7].default_value = tint
        links.new(mix.outputs[2], bsdf.inputs["Base Color"])
        mods = nodes.new("ShaderNodeTexImage")
        mods.image = image(spec["set"] + "_MODS.png", True, "CHANNEL_PACKED")
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
        normal_tex.image = image(spec["set"] + "_Normal.png", True)
        links.new(mapping.outputs["Vector"], normal_tex.inputs["Vector"])
        normal_map = nodes.new("ShaderNodeNormalMap")
        normal_map.inputs["Strength"].default_value = float(spec.get("normal", 1.0))
        links.new(normal_tex.outputs["Color"], normal_map.inputs["Color"])
        links.new(normal_map.outputs["Normal"], bsdf.inputs["Normal"])
    if "atlas" in spec:
        tex = nodes.new("ShaderNodeTexImage")
        tex.image = image(spec["atlas"] + ".png", False, "STRAIGHT")
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
        set_input(bsdf, ["IOR"], 1.45)
    return mat


# ----------------------------------------------------------------------------- meshes
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
                 "bounds_max": [round(float(x), 4) for x in hi], "origin": [float(x) for x in origin]}


def make_collision(boxes, material, collection):
    objects = []
    for name, centre, size in boxes:
        v, t = a7kit.box(size, 0.0)
        vb, tb = a7kit.to_blender(v, t)
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata(vb.tolist(), [], tb.tolist())
        mesh.materials.append(material)
        mesh.update(calc_edges=True)
        obj = bpy.data.objects.new(name, mesh)
        obj.location = to_blender_point(centre)
        collection.objects.link(obj)
        objects.append(obj)
    return objects


def export_fbx(objects, path):
    """Exports objects without texture records. Unity binds the external URP materials by name, and the
    exporter would otherwise write the build machine's absolute texture paths into the file."""
    if not EXPORT:
        return
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


# ----------------------------------------------------------------------------- preview renders
CAMERAS = {
    "cockpit_from_spawn": ((0.0, 1.65, 0.8), (0.0, 1.55, -3.0), 78),
    "cockpit_three_quarter": ((2.2, 1.95, 2.3), (-0.6, 1.15, -2.2), 70),
    "cockpit_console": ((0.0, 1.45, -0.75), (0.0, 1.25, -2.4), 74),
    "engineering_corridor": ((0.25, 1.65, 3.6), (0.0, 1.35, 11.0), 76),
    "engineering_r02": ((0.6, 1.65, 3.4), (-0.9, 1.30, 11.0), 90),   # review-01 R02 framing at 1.65 m eye height
    "engineering_power": ((-0.25, 1.55, 6.75), (-2.0, 1.2, 6.05), 72),
    "engineering_eclss": ((0.25, 1.55, 7.5), (2.0, 1.2, 8.15), 72),
    "storage": ((0.0, 1.65, 11.5), (0.0, 1.3, 17.0), 76),
    "airlock": ((0.2, 1.65, 17.35), (-0.2, 1.3, 21.0), 80),
    "exterior_finale": ((9.0, 5.0, 27.0), (0.5, 1.6, 12.0), 58),
    "exterior_forward": ((-9.5, 4.5, -11.0), (0.0, 1.6, 4.0), 55),
    "exterior_eva": ((2.2, 1.6, 24.6), (1.2, 2.2, 14.0), 72),
    "exterior_antenna": ((3.514, 2.431, 14.25), (3.434, 2.462, 12.9), 90),   # towards review-01 R06 (Unity rolls this view to the hull normal)
}


def enable_gpu(scene):
    scene.render.engine = "CYCLES"
    try:
        prefs = bpy.context.preferences.addons["cycles"].preferences
        for kind in ("OPTIX", "CUDA"):
            try:
                prefs.compute_device_type = kind
                prefs.get_devices()
                usable = [d for d in prefs.devices if d.type == kind]
                if usable:
                    for device in prefs.devices:
                        device.use = device.type == kind
                    scene.cycles.device = "GPU"
                    return kind + ": " + usable[0].name
            except (TypeError, RuntimeError):
                continue
    except KeyError:
        pass
    scene.cycles.device = "CPU"
    return "CPU"


def setup_render(scene):
    device = enable_gpu(scene)
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
    world = bpy.data.worlds.new("A7_PreviewWorld")
    world.use_nodes = True
    background = world.node_tree.nodes["Background"]
    background.inputs[0].default_value = (0.012, 0.014, 0.02, 1.0)
    background.inputs[1].default_value = 1.0
    scene.world = world
    for name, room in a7_ship.ROOMS.items():
        for z in room["fixtures"]:
            data = bpy.data.lights.new("A7_PreviewLight", "AREA")
            data.shape = "RECTANGLE"
            data.size = 1.1
            data.size_y = 0.28
            data.energy = 70.0
            data.color = (1.0, 0.92, 0.81)
            light = bpy.data.objects.new("A7_PreviewLight_%s_%.1f" % (name, z), data)
            light.location = to_blender_point((0.0, a7_ship.CEILING - 0.135, z))
            scene.collection.objects.link(light)
    for name, (group, point) in a7_ship.ANCHORS.items():
        if name == "LightAnchor_PowerCabinetWork":
            data = bpy.data.lights.new("A7_PreviewWorkLight", "POINT")
            data.energy = 9.0
            data.color = (1.0, 0.55, 0.16)
            data.shadow_soft_size = 0.05
            light = bpy.data.objects.new("A7_Preview_" + name, data)
            light.location = to_blender_point(point)
            scene.collection.objects.link(light)
    # hard sunlight for the exterior views; inside, the hull and bulkheads block it
    sun_data = bpy.data.lights.new("A7_PreviewSun", "SUN")
    sun_data.energy = 4.5
    sun_data.angle = math.radians(0.5)
    sun = bpy.data.objects.new("A7_PreviewSun", sun_data)
    direction = to_blender_point((-0.45, -0.62, -0.64))
    sun.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    scene.collection.objects.link(sun)
    return device


def render_views(scene, out_dir):
    os.makedirs(out_dir, exist_ok=True)
    wanted = VIEWS.split(",") if isinstance(VIEWS, str) else list(CAMERAS)
    for name in wanted:
        position, target, fov = CAMERAS[name]
        data = bpy.data.cameras.new("A7_Cam_" + name)
        data.sensor_fit = "HORIZONTAL"
        data.angle = math.radians(fov)
        data.clip_start = 0.05
        data.clip_end = 200.0
        camera = bpy.data.objects.new("A7_Cam_" + name, data)
        camera.location = to_blender_point(position)
        direction = to_blender_point(target) - camera.location
        camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
        scene.collection.objects.link(camera)
        scene.camera = camera
        scene.render.filepath = os.path.join(out_dir, "A7_blender_%s.png" % name)
        bpy.ops.render.render(write_still=True)
        log("rendered", name)


# ----------------------------------------------------------------------------- main
def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    with open(os.path.join(TEXTURES, "a7_decals.json"), encoding="utf-8") as handle:
        decals = json.load(handle)
    with open(os.path.join(TEXTURES, "a7_screens.json"), encoding="utf-8") as handle:
        screens = json.load(handle)
    with open(os.path.join(HERE, "a7_materials.json"), encoding="utf-8") as handle:
        specs = {entry["name"]: entry for entry in json.load(handle)["materials"]}
    materials = {name: build_material(name, spec) for name, spec in specs.items()}
    log("materials", len(materials))

    models, proxies = a7_ship.build_all(decals, screens)
    used = sorted({material for model in models.values() for material in model.parts})
    missing = [name for name in used if name not in materials]
    if missing:
        raise RuntimeError("materials without a spec: %s" % missing)

    os.makedirs(MESHES, exist_ok=True)
    report = {"blender": bpy.app.version_string, "units": "metres, Unity axes (x right, y up, z forward)", "modules": {}}
    # Static modules get one FBX each. Movable or switchable objects keep their own origin and are
    # exported together per group (Doors, Parts), so the integrator can address them separately.
    groups = {}
    for name, model in models.items():
        collection = bpy.data.collections.new("A7_" + name)
        scene.collection.children.link(collection)
        origin = getattr(model, "origin", (0.0, 0.0, 0.0))
        group = getattr(model, "group", None)
        obj, info = make_object(name if group else "A7_" + name, model, materials, collection, origin)
        report["modules"][name] = info
        log(name, info["triangles"], "triangles,", len(info["per_material"]), "materials")
        if group:
            info["group"] = group
            groups.setdefault(group, []).append(obj)
        else:
            export_fbx([obj], os.path.join(MESHES, "A7_%s.fbx" % name))
    if a7_ship.ANCHORS:
        collection = bpy.data.collections.new("A7_Anchors")
        scene.collection.children.link(collection)
        for name, (group, point) in sorted(a7_ship.ANCHORS.items()):
            empty = bpy.data.objects.new(name, None)
            empty.empty_display_size = 0.1
            empty.location = to_blender_point(point)
            collection.objects.link(empty)
            groups.setdefault(group, []).append(empty)
        report["anchors"] = {name: {"group": group, "point": [round(float(c), 4) for c in point]}
                             for name, (group, point) in sorted(a7_ship.ANCHORS.items())}
    for group, objects in groups.items():
        export_fbx(objects, os.path.join(MESHES, "A7_%s.fbx" % group))
    collection = bpy.data.collections.new("A7_Collision")
    scene.collection.children.link(collection)
    collision_objects = make_collision(proxies, materials["A7_Collision"], collection)
    export_fbx(collision_objects, os.path.join(MESHES, "A7_Collision.fbx"))
    for obj in collision_objects:
        obj.hide_render = True
        obj.hide_set(True)
    report["collision_proxies"] = [{"name": n, "centre": list(c), "size": list(s)} for n, c, s in proxies]
    report["triangles_total"] = sum(info["triangles"] for info in report["modules"].values())
    report["materials_used"] = used
    if EXPORT:
        with open(os.path.join(MESHES, "a7_model_report.json"), "w", encoding="utf-8") as handle:
            json.dump(report, handle, indent=1)
    log("exported", len(models), "modules,", report["triangles_total"], "triangles,", len(proxies), "collision proxies")

    if RENDER:
        os.makedirs(WORK, exist_ok=True)
        for name in (HIDE.split(",") if isinstance(HIDE, str) else []):
            if name in bpy.data.objects:
                bpy.data.objects[name].hide_render = True
                log("left out of the stills:", name)
        device = setup_render(scene)
        log("render device", device)
        render_views(scene, os.path.join(WORK, "renders"))
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(WORK, "Aurora7.blend"))
        log("saved", os.path.join(WORK, "Aurora7.blend"))
    log("done")


main()
