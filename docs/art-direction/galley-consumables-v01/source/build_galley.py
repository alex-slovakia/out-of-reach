"""build_galley - Blender 5.3 background build of GALLEY-PROPS-01 (original work).

Build: one FBX per prop (both variants share the prop's origin, plus anchor empties; meshes and empties only, flat
hierarchy, no cameras, lights or texture records), the editable GC_GalleyConsumables.blend (relative texture paths)
and gc_build_report.json (for roundtrip_habitat.py --report gc_build_report.json).
--render: a studio lineup with a 100 mm scale bar, and the props on the deployed table of the HABITAT galley unit
(hb_kit, feature/habitat-details-v1) for context. --habitat-out points at a habitat build folder (textures and its
label atlas); the A7 maps come from --ship-textures.

Run: blender -b --factory-startup --python build_galley.py -- --out <dir> [--render --habitat-out <dir>
     --ship-textures <Aurora7/Textures> --samples 128]
"""
import json
import math
import os
import sys

import bpy
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.dont_write_bytecode = True
sys.path.insert(0, HERE)
import a7kit  # noqa: E402
import build_habitat as bh  # noqa: E402   (shared material/mesh/FBX helpers; its own main does not run)
import gc_kit  # noqa: E402

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def arg(name, default=None):
    if name in ARGS:
        i = ARGS.index(name)
        return ARGS[i + 1] if i + 1 < len(ARGS) and not ARGS[i + 1].startswith("--") else True
    return default


OUT = os.path.abspath(arg("--out"))
TEXTURES = os.path.join(OUT, "textures")
RENDER = bool(arg("--render", False))
SAMPLES = int(arg("--samples", 128))
HABITAT_OUT = arg("--habitat-out")
SHIP_TEXTURES = arg("--ship-textures")


def atlas():
    with open(os.path.join(TEXTURES, "gc_labels.json"), encoding="utf-8") as handle:
        return json.load(handle)


def specs(path):
    with open(path, encoding="utf-8") as handle:
        return {e["name"]: e for e in json.load(handle)["materials"]}


def build():
    bh.clear_scene()
    kit = gc_kit.build_kit(atlas())
    gc_specs = specs(os.path.join(HERE, "gc_materials.json"))
    used = sorted({m for prop in kit.values() for model in prop["objects"].values() for m in model.parts})
    materials = {name: bh.build_material(name, gc_specs[name], TEXTURES) for name in used}
    report = {"units": {}, "total_triangles": 0, "materials": used,
              "convention": "Unity metres, y up; label side faces +z; each prop's origin is its centre; the "
                            "variants of a prop share that origin"}
    for prop_name, prop in kit.items():
        collection = bpy.data.collections.new(prop_name)
        bpy.context.scene.collection.children.link(collection)
        objects, info = [], {"objects": {}, "anchors": {}}
        for name, model in prop["objects"].items():
            obj, data = bh.make_object(name, model, materials, collection)
            info["objects"][name] = data
            objects.append(obj)
            report["total_triangles"] += data["triangles"]
        for name, (point, forward, up, note) in prop["anchors"].items():
            objects.append(bh.make_anchor(name, point, collection))
            info["anchors"][name] = {"point": [round(c, 5) for c in point], "forward": [round(c, 4) for c in forward],
                                     "up": [round(c, 4) for c in up], "note": note}
        info["fbx"] = prop_name + ".fbx"
        info["triangles"] = sum(o["triangles"] for o in info["objects"].values())
        bh.export_fbx(objects, os.path.join(OUT, info["fbx"]))
        report["units"][prop_name] = info
        bh.log("exported", info["fbx"], info["triangles"], "triangles,", len(info["anchors"]), "anchors")
    with open(os.path.join(OUT, "gc_build_report.json"), "w", encoding="utf-8") as handle:
        json.dump(report, handle, indent=1)
    bpy.ops.file.make_paths_relative()
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "GC_GalleyConsumables.blend"), compress=True)
    bh.log("saved GC_GalleyConsumables.blend; total", report["total_triangles"], "triangles")


# ----------------------------------------------------------------------------- renders
def setup(scene, world_rgb):
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
    scene.render.resolution_x, scene.render.resolution_y = 1600, 900
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = 0.3
    world = bpy.data.worlds.new("GC_World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = world_rgb + (1.0,)
    scene.world = world


def area(name, point, target, size, energy, colour=(1.0, 0.95, 0.88)):
    data = bpy.data.lights.new(name, "AREA")
    data.shape = "RECTANGLE"
    data.size, data.size_y = size
    data.energy, data.color = energy, colour
    light = bpy.data.objects.new(name, data)
    light.location = bh.to_blender_point(point)
    light.rotation_euler = (bh.to_blender_point(target) - light.location).to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.collection.objects.link(light)


def shoot(name, position, target, fov):
    scene = bpy.context.scene
    data = bpy.data.cameras.new("GC_Cam_" + name)
    data.sensor_fit = "HORIZONTAL"
    data.angle = math.radians(fov)
    data.clip_start = 0.01
    camera = bpy.data.objects.new("GC_Cam_" + name, data)
    camera.location = bh.to_blender_point(position)
    camera.rotation_euler = (bh.to_blender_point(target) - camera.location).to_track_quat("-Z", "Y").to_euler()
    scene.collection.objects.link(camera)
    scene.camera = camera
    scene.render.filepath = os.path.join(OUT, "renders", "GC_blender_%s.png" % name)
    bpy.ops.render.render(write_still=True)
    bh.log("rendered", name)


def studio(materials, kit):
    """The four variants standing on a grey table, a 100 mm scale bar in front."""
    bh.clear_scene()
    materials = {n: bh.build_material(n, s, TEXTURES) for n, s in specs(os.path.join(HERE, "gc_materials.json")).items()}
    materials["GC_Table"] = bh.build_material("GC_Table", {"tint": "#6E6F70", "smoothness": 0.25}, TEXTURES)
    coll = bpy.context.scene.collection
    table = a7kit.Model("GC_Table")
    table.box("GC_Table", (0, -0.01, 0), (1.2, 0.02, 0.6), 0.0)
    bh.make_object("GC_Table", table, materials, coll)
    # Unity's viewer looking along -z has +x on the left: meals left, drinks right in the picture
    order = [("GC_MealPouch", "GC_MealPouch_Sealed", 0.24), ("GC_MealPouch", "GC_MealPouch_Opened", 0.08),
             ("GC_DrinkPouch", "GC_DrinkPouch_Closed", -0.08), ("GC_DrinkPouch", "GC_DrinkPouch_Open", -0.24)]
    for prop, name, x in order:
        model = kit[prop]["objects"][name]
        lo, _ = model.bounds()
        placed = a7kit.Model(name)
        placed.instance(model, a7kit.at(x, -lo[1], 0.0))
        bh.make_object(name, placed, materials, coll)
    bar = a7kit.Model("GC_Scale")
    bar.decal(atlas(), "scale_100", (0, 0, 0), a7kit.compose(a7kit.at(0.0, 0.0006, 0.095), a7kit.rotation((1, 0, 0), -90)),
              material="GC_Labels")
    bh.make_object("GC_Scale", bar, materials, coll)
    setup(bpy.context.scene, (0.035, 0.037, 0.04))
    area("GC_Key", (-0.45, 0.55, 0.55), (0, 0.06, 0), (0.6, 0.6), 60.0)
    area("GC_Fill", (0.55, 0.30, 0.45), (0, 0.06, 0), (0.6, 0.4), 18.0, (0.85, 0.9, 1.0))
    area("GC_Rim", (0.0, 0.40, -0.55), (0, 0.08, 0), (0.9, 0.3), 35.0)
    shoot("studio_lineup", (0.0, 0.22, 0.80), (0.0, 0.10, 0.0), 44)
    shoot("studio_drink_detail", (-0.10, 0.28, 0.24), (-0.16, 0.20, 0.0), 40)
    shoot("studio_meal_open", (0.08, 0.32, 0.17), (0.08, 0.135, 0.0), 34)


def galley_context(kit):
    """The props on the deployed table of the habitat galley (unit space), first-person view."""
    bh.clear_scene()
    sys.path.insert(0, HERE)
    import hb_kit  # noqa: E402
    habitat_textures = os.path.join(os.path.abspath(HABITAT_OUT), "textures")
    with open(os.path.join(habitat_textures, "hb_labels.json"), encoding="utf-8") as handle:
        hb_atlas = json.load(handle)
    galley = hb_kit.build_kit(hb_atlas)["HB_Galley"]["objects"]
    hb_specs = specs(os.path.join(HERE, "hb_materials.json"))
    materials = {n: bh.build_material(n, s, habitat_textures) for n, s in hb_specs.items()}
    materials.update({n: bh.build_material(n, s, TEXTURES) for n, s in specs(os.path.join(HERE, "gc_materials.json")).items()})
    coll = bpy.context.scene.collection
    for name, (model, origin, motion) in galley.items():
        bh.make_object(name, model, materials, coll)
    backdrop = a7kit.Model("GC_Backdrop")
    backdrop.box("A7_PaintGray", (0, 1.3, -0.02), (3.0, 2.6, 0.02), 0.0)
    backdrop.box("A7_PaintGray", (0, -0.01, 1.0), (3.0, 0.02, 2.0), 0.0)
    bh.make_object("GC_Backdrop", backdrop, materials, coll)
    table_top = 0.9975
    props = [("GC_MealPouch", "GC_MealPouch_Opened", (0.638, table_top, 0.620), -90.0),
             ("GC_DrinkPouch", "GC_DrinkPouch_Closed", (0.035, table_top, 0.640), -90.0)]
    for prop, name, (x, y, z), tilt in props:
        model = kit[prop]["objects"][name]
        lo, hi = model.bounds()
        # lying on its back on the table (rotated about x), label up
        place = a7kit.compose(a7kit.at(x, y + (hi[2] - lo[2]) / 2 + 0.0005, z), a7kit.rotation((1, 0, 0), tilt))
        placed = a7kit.Model(name)
        placed.instance(model, place)
        bh.make_object(name, placed, materials, coll)
    setup(bpy.context.scene, (0.012, 0.014, 0.02))
    area("GC_GalleyTask", (0.27, 1.53, 0.40), (0.27, 0.9, 0.55), (0.68, 0.06), 14.0)
    area("GC_Ceiling", (0.3, 2.9, 1.2), (0.3, 0.9, 0.6), (1.2, 0.4), 90.0)
    shoot("galley_table_fp", (0.30, 1.50, 1.30), (0.33, 1.0, 0.62), 55)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    if RENDER:
        os.makedirs(os.path.join(OUT, "renders"), exist_ok=True)
        kit = gc_kit.build_kit(atlas())
        studio(None, kit)
        galley_context(kit)
    else:
        build()
    bh.log("done")
