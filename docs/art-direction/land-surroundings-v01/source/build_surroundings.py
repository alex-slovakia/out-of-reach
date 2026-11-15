"""build_surroundings - LAND-SURROUNDINGS-V01 in Blender 5.3: one FBX (7 static groups + anchors), the build report,
the .blend and review views.

Every group is its own mesh object pivoted at the prologue origin (Unity metres, y up); anchors are empties. The FBX
holds meshes and empties only. The preview adds the LAND kit (rebuilt from its own source), a vehicle proxy, a flat
brown ground at y -36.6 like root's horizon, a sky and a sun; none of them is exported.

Run: blender -b --factory-startup --python build_surroundings.py -- --out <dir> --textures <dir with LSS_, LS_,
     A7_Paint_, A7_Brushed_ maps and ls_markings.json> --land-src <land-launch-site source dir> [--render --samples 32]
"""
import json
import math
import os
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.dont_write_bytecode = True
sys.path.insert(0, HERE)
import a7kit  # noqa: E402
import build_habitat as bh  # noqa: E402
import lss_kit  # noqa: E402

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
get = lambda k, d=None: ARGS[ARGS.index(k) + 1] if k in ARGS else d
OUT = os.path.abspath(get("--out"))
TEX = os.path.abspath(get("--textures"))
LAND_SRC = get("--land-src")
RENDER = "--render" in ARGS
SAMPLES = int(get("--samples", 32))
FBX = "LSS_LandSurroundings.fbx"


def main():
    bh.clear_scene()
    specs = {m["name"]: m for m in json.load(open(os.path.join(HERE, "lss_materials.json"), encoding="utf-8"))["materials"]}
    materials = {n: bh.build_material(n, s, TEX) for n, s in specs.items()}
    coll = bpy.context.scene.collection
    models, anchors = lss_kit.build_kit()
    objects, report_objects = [], {}
    for model in models:
        obj, _ = bh.make_object(model.name, model, materials, coll)
        objects.append(obj)
        lo, hi = model.bounds()
        report_objects[model.name] = {
            "triangles": model.triangle_count(), "origin": [0.0, 0.0, 0.0],
            "bounds_min": [round(float(c), 5) for c in lo], "bounds_max": [round(float(c), 5) for c in hi],
            "per_material": {k: int(sum(len(t) for _, t, _ in v)) for k, v in model.parts.items()}}
    empties = [bh.make_anchor(name, p, coll) for name, p in anchors.items()]
    bh.export_fbx(objects + empties, os.path.join(OUT, FBX))
    report = {"units": {"LSS_LandSurroundings": {"fbx": FBX, "objects": report_objects,
                                                 "anchors": {k: {"point": [round(float(c), 5) for c in p]} for k, p in anchors.items()}}},
              "triangles_total": sum(o["triangles"] for o in report_objects.values())}
    json.dump(report, open(os.path.join(OUT, "lss_build_report.json"), "w", encoding="utf-8", newline="\n"), indent=1)
    bh.log("exported %s: %d objects, %d anchors, %d triangles" % (FBX, len(objects), len(empties), report["triangles_total"]))
    if RENDER:
        render()
    bpy.ops.file.make_paths_relative()
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "LSS_LandSurroundings.blend"), compress=True)
    bh.log("saved LSS_LandSurroundings.blend")


def plain(name, colour):
    mat = bpy.data.materials.new("PV_" + name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = colour
    bsdf.inputs["Roughness"].default_value = 0.95
    return mat


def render():
    scene = bpy.context.scene
    coll = bpy.data.collections.new("PV_Context")
    scene.collection.children.link(coll)
    # context: the delivered LAND kit rebuilt from its own source, with its own materials
    sys.path.insert(0, os.path.abspath(LAND_SRC))
    import ls_kit  # noqa: E402
    land_specs = {m["name"]: m for m in json.load(open(os.path.join(LAND_SRC, "ls_materials.json"), encoding="utf-8"))["materials"]}
    land_mats = {n: (bpy.data.materials.get(n) or bh.build_material(n, s, TEX)) for n, s in land_specs.items()}
    atlas = json.load(open(os.path.join(TEX, "ls_markings.json"), encoding="utf-8"))
    for model in ls_kit.build_kit(atlas)[0]:
        obj, _ = bh.make_object("PV_" + model.name, model, land_mats, coll)
    v = a7kit.Model("Vehicle")
    v.cyl("P", (0, -30.6, 0), (0, -0.55, 0), 2.35, 24)
    v.cyl("P", (0, -0.55, 0), (0, 3.2, 0), 2.45, 24)
    v.cyl("P", (0, 3.2, 0), (0, 4.95, 0), 1.2, 24)
    bh.make_object("PV_Vehicle", v, {"P": plain("Vehicle", (0.82, 0.80, 0.76, 1))}, coll)
    s = a7kit.Model("Mast")
    s.box("P", (0, -16.595, -8.5), (3.5, 32.01, 3.5), 0.0)
    s.box("P", (0, -0.19, -9.9), (8.0, 0.42, 6.0), 0.0)
    bh.make_object("PV_Mast", s, {"P": plain("Mast", (0.62, 0.30, 0.20, 1))}, coll)
    g = a7kit.Model("Ground")
    g.box("P", (0, lss_kit.G - 0.25, 0), (16000, 0.5, 16000), 0.0)
    bh.make_object("PV_Ground", g, {"P": plain("Ground", (0.21, 0.16, 0.10, 1))}, coll)
    scene.render.engine = "CYCLES"
    prefs = bpy.context.preferences.addons["cycles"].preferences
    try:
        prefs.compute_device_type = "OPTIX"
        prefs.get_devices()
        for dev in prefs.devices:
            dev.use = dev.type == "OPTIX"
        scene.cycles.device = "GPU"
    except (TypeError, RuntimeError):
        pass
    scene.cycles.samples = SAMPLES
    scene.cycles.use_denoising = True
    scene.render.resolution_x, scene.render.resolution_y = 1280, 720
    scene.view_settings.view_transform = "AgX"
    world = bpy.data.worlds.new("PV_Sky")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.45, 0.58, 0.76, 1.0)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.85
    scene.world = world
    sun = bpy.data.objects.new("PV_Sun", bpy.data.lights.new("PV_Sun", "SUN"))
    sun.data.energy = 3.4
    sun.rotation_euler = (math.radians(58), math.radians(10), math.radians(32))
    coll.objects.link(sun)
    views = {"deck_north": ((3.6, 1.65, -9.9), (0, -36.6, -960), 40),
             "deck_east": ((3.6, 1.65, -9.9), (700, -36.6, 260), 50),
             "aerial": ((260, 210, 640), (0, -36.6, -160), 50),
             "ground_west": ((-260, -34.9, 140), (0, -18, 0), 45)}
    os.makedirs(os.path.join(OUT, "renders"), exist_ok=True)
    for name, (pos, target, fov) in views.items():
        cam = bpy.data.objects.new("PV_Cam_" + name, bpy.data.cameras.new("PV_Cam_" + name))
        cam.data.sensor_fit = "VERTICAL"
        cam.data.angle_y = math.radians(fov)
        cam.data.clip_start, cam.data.clip_end = 0.05, 9000.0
        cam.location = bh.to_blender_point(pos)
        cam.rotation_euler = (bh.to_blender_point(target) - cam.location).to_track_quat("-Z", "Y").to_euler()
        coll.objects.link(cam)
        scene.camera = cam
        scene.render.filepath = os.path.join(OUT, "renders", "LSS_%s.png" % name)
        bpy.ops.render.render(write_still=True)
        bh.log("rendered", name)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    main()
