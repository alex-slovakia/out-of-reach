"""
build_interior_hero - Blender 3.6 build of the AURORA-7 interior hero props (INTERIOR-HERO-V01, original work).

    blender -b --python build_interior_hero.py -- [--render] [--only IH_Console,IH_SeatCdr] [--modules ih_cockpit] [--no-export]

Reads   ih_cockpit.py / ih_habitat.py / ih_systems.py (prop builders on ihkit + the ship's a7kit),
        ../Textures/ih_labels.json, ih_screens.json (written by ih_textures.py), ih_materials.json
Writes  ../Models/InteriorHeroV01.fbx           one object per prop and level of detail: <Prop>_L0/1/2 (not _LODn: Unity would auto-group)
        ../Models/InteriorHeroV01_report.json   triangles, bounds, origins and swap targets, in Unity coordinates
        ../Previews~/blender_<prop>.png         with --render (EEVEE, preview materials)

Axes follow a7kit: kit coordinates are Unity's (x right, y up, z forward, metres); to_blender swaps y/z and flips
the winding, and the FBX is exported with bake_space_transform so Unity (Bake Axis Conversion) reads it back unchanged.
"""
import importlib
import json
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.dont_write_bytecode = True
sys.path.insert(0, HERE)
import ihkit  # noqa: E402
import a7kit  # noqa: E402

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
RENDER = "--render" in ARGS
EXPORT = "--no-export" not in ARGS
ONLY = ARGS[ARGS.index("--only") + 1].split(",") if "--only" in ARGS else None
MODELS = os.path.normpath(os.path.join(HERE, "..", "Models"))
TEXTURES = os.path.normpath(os.path.join(HERE, "..", "Textures"))
PREVIEWS = os.path.normpath(os.path.join(HERE, "..", "Previews~"))
MODULES = ARGS[ARGS.index("--modules") + 1].split(",") if "--modules" in ARGS else ["ih_cockpit", "ih_habitat", "ih_systems"]


def srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def hex_linear(text, alpha=1.0):
    text = text.lstrip("#")
    return tuple(srgb_to_linear(int(text[i:i + 2], 16) / 255.0) for i in (0, 2, 4)) + (alpha,)


def to_blender_point(p):
    return Vector((p[0], p[2], p[1]))


def image(name, non_color=False):
    path = os.path.join(TEXTURES, name)
    if not os.path.exists(path):
        return None
    img = bpy.data.images.load(path, check_existing=True)
    if non_color:
        img.colorspace_settings.name = "Non-Color"
    return img


def build_material(spec):
    """Preview material for the EEVEE stills; Unity builds its own URP materials from the same JSON."""
    mat = bpy.data.materials.new(spec["name"])
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = next(n for n in nodes if n.type == "BSDF_PRINCIPLED")
    tint = hex_linear(spec.get("tint", "#FFFFFF"))
    bsdf.inputs["Base Color"].default_value = tint
    bsdf.inputs["Metallic"].default_value = float(spec.get("metallic", 0.0))
    bsdf.inputs["Roughness"].default_value = 1.0 - float(spec.get("smoothness", 0.5))
    base_name = spec.get("base") or (spec["set"] + "_BaseColor" if spec.get("set") else spec.get("atlas"))
    if RENDER and base_name:
        coord = nodes.new("ShaderNodeTexCoord")
        mapping = nodes.new("ShaderNodeMapping")
        tiling = float(spec.get("tiling", 1.0))
        mapping.inputs["Scale"].default_value = (tiling, tiling, tiling)
        links.new(coord.outputs["UV"], mapping.inputs["Vector"])
        tex = nodes.new("ShaderNodeTexImage")
        tex.image = image(base_name + ".png")
        links.new(mapping.outputs["Vector"], tex.inputs["Vector"])
        mix = nodes.new("ShaderNodeMixRGB")
        mix.blend_type = "MULTIPLY"
        mix.inputs["Fac"].default_value = 1.0
        mix.inputs["Color2"].default_value = tint
        links.new(tex.outputs["Color"], mix.inputs["Color1"])
        links.new(mix.outputs["Color"], bsdf.inputs["Base Color"])
        if spec.get("surface") == "cutout":
            links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
            mat.blend_method = "CLIP"
            mat.shadow_method = "CLIP"
        if spec.get("emission_map"):
            links.new(tex.outputs["Color"], bsdf.inputs["Emission"])
            bsdf.inputs["Emission Strength"].default_value = float(spec.get("emission_strength", 1.0))
        if spec.get("set"):
            normal = image(spec["set"] + "_Normal.png", True)
            if normal is not None:
                ntex = nodes.new("ShaderNodeTexImage")
                ntex.image = normal
                links.new(mapping.outputs["Vector"], ntex.inputs["Vector"])
                nmap = nodes.new("ShaderNodeNormalMap")
                nmap.inputs["Strength"].default_value = float(spec.get("normal", 1.0))
                links.new(ntex.outputs["Color"], nmap.inputs["Color"])
                links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
            if "Metal" in spec["set"]:
                bsdf.inputs["Metallic"].default_value = 1.0
                bsdf.inputs["Roughness"].default_value = 0.38
    if spec.get("emission") and not spec.get("emission_map"):
        bsdf.inputs["Emission"].default_value = hex_linear(spec["emission"])
        bsdf.inputs["Emission Strength"].default_value = float(spec.get("emission_strength", 1.0))
    if spec.get("surface") == "transparent":
        bsdf.inputs["Alpha"].default_value = float(spec.get("alpha", 0.3))
        mat.blend_method = "BLEND"
        mat.shadow_method = "NONE"
    return mat


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
    mesh.use_auto_smooth = True                       # Blender 3.6: split normals by angle
    mesh.auto_smooth_angle = math.radians(42.0)
    obj = bpy.data.objects.new(name, mesh)
    obj.location = to_blender_point(origin)
    collection.objects.link(obj)
    weighted = obj.modifiers.new("WeightedNormal", "WEIGHTED_NORMAL")
    weighted.keep_sharp = True
    weighted.weight = 50
    lo, hi = v.min(axis=0) + np.asarray(origin), v.max(axis=0) + np.asarray(origin)
    return obj, {"triangles": int(len(t)), "per_material": counts, "bounds_min": [round(float(x), 4) for x in lo],
                 "bounds_max": [round(float(x), 4) for x in hi]}


def export_fbx(objects, path):
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


def render_views(views):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.eevee.use_gtao = True
    scene.eevee.gtao_distance = 0.25
    scene.eevee.use_ssr = True
    scene.eevee.taa_render_samples = 32
    scene.render.resolution_x, scene.render.resolution_y = 1600, 900
    scene.view_settings.view_transform = "Filmic"
    world = bpy.data.worlds.new("IH_World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.10, 0.105, 0.115, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.0
    scene.world = world
    cam_data = bpy.data.cameras.new("IH_Camera")
    cam = bpy.data.objects.new("IH_Camera", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    os.makedirs(PREVIEWS, exist_ok=True)
    lights = []
    for name, eye, target, fov, lamps in views:
        for light in lights:
            bpy.data.objects.remove(light, do_unlink=True)
        lights = []
        for i, (pos, watts, size) in enumerate(lamps):
            data = bpy.data.lights.new("IH_Light%d" % i, "AREA")
            data.energy = watts
            data.size = size
            data.color = (1.0, 0.95, 0.88)
            light = bpy.data.objects.new("IH_Light%d" % i, data)
            light.location = to_blender_point(pos)
            direction = to_blender_point(target) - light.location
            light.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
            scene.collection.objects.link(light)
            lights.append(light)
        cam.location = to_blender_point(eye)
        direction = to_blender_point(target) - cam.location
        cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
        cam_data.sensor_fit = "HORIZONTAL"
        cam_data.angle = math.radians(fov)
        cam_data.clip_start = 0.03
        scene.render.filepath = os.path.join(PREVIEWS, "blender_%s.png" % name)
        bpy.ops.render.render(write_still=True)
        print("RENDERED", scene.render.filepath)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    with open(os.path.join(HERE, "ih_materials.json")) as f:
        specs = json.load(f)["materials"]
    materials = {spec["name"]: build_material(spec) for spec in specs}
    missing = [name for name in ihkit.ALL_MATERIALS if name not in materials]
    if missing:
        raise RuntimeError("ih_materials.json lacks " + ", ".join(missing))
    collection = bpy.data.collections.new("InteriorHeroV01")
    bpy.context.scene.collection.children.link(collection)
    report = {"blender": bpy.app.version_string, "units": "metres, Unity axes (x right, y up, z forward)", "props": []}
    objects, views = [], []
    for module_name in MODULES:
        if not os.path.exists(os.path.join(HERE, module_name + ".py")):
            continue
        try:
            module = importlib.import_module(module_name)
            specs_of_module = module.props()
        except Exception as error:                       # another writer may be mid-edit: only fatal for a full build
            if not ONLY:
                raise
            print("SKIPPED", module_name, repr(error))
            continue
        for spec in specs_of_module:
            if ONLY and spec["name"] not in ONLY:
                continue
            entry = {"name": spec["name"], "origin": [float(x) for x in spec["origin"]], "module": module_name,
                     "hide": list(spec.get("hide", [])), "carve": list(spec.get("carve", [])), "lods": [],
                     "views": [{"name": v[0], "eye": list(v[1]), "target": list(v[2]), "fov": v[3]} for v in spec.get("views", [])]}
            for lod in range(3):
                model = spec["build"](lod)
                unknown = [m for m in model.parts if m not in materials]
                if unknown:
                    raise RuntimeError(spec["name"] + " uses unknown materials " + ", ".join(unknown))
                obj, stats = make_object("%s_L%d" % (spec["name"], lod), model, materials, collection, spec["origin"])
                if lod > 0:
                    obj.hide_render = True
                objects.append(obj)
                entry["lods"].append(stats)
            report["props"].append(entry)
            for view in spec.get("views", []):
                views.append(view)
            print("BUILT", spec["name"], [lod["triangles"] for lod in entry["lods"]])
    report["triangles_lod0"] = sum(p["lods"][0]["triangles"] for p in report["props"])
    if EXPORT and not ONLY:
        os.makedirs(MODELS, exist_ok=True)
        export_fbx(objects, os.path.join(MODELS, "InteriorHeroV01.fbx"))
        with open(os.path.join(MODELS, "InteriorHeroV01_report.json"), "w") as f:
            json.dump(report, f, indent=1)
        with open(os.path.join(HERE, "ih_materials.json")) as src, open(os.path.join(MODELS, "InteriorHeroV01_materials.json"), "w", newline=chr(10)) as dst:
            dst.write(src.read())
        print("EXPORTED", len(objects), "objects,", report["triangles_lod0"], "triangles at LOD0")
    if RENDER:
        render_views(views)


main()
