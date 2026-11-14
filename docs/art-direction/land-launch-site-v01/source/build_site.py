"""build_site - LAND-LAUNCH-SITE-01 in Blender 5.3: one modular FBX, the build report, the .blend and views.

Every module is its own mesh object with its pivot at the prologue origin (Unity metres, y up); anchors are empties.
The FBX holds meshes and empties only (no lights, cameras or collision). The preview scene adds grey proxies of the
existing transport, mast, arm and staging deck, a ground plane at y -36.6 and a sky only for the renders; none of them
is exported.

Run: blender -b --factory-startup --python build_site.py -- --out <dir> --ship-textures <dir with A7_* maps>
     [--render --samples 48]
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
sys.path.insert(0, HERE)
import a7kit  # noqa: E402
import build_habitat as bh  # noqa: E402
import ls_kit  # noqa: E402

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
get = lambda k, d=None: ARGS[ARGS.index(k) + 1] if k in ARGS else d
OUT = os.path.abspath(get("--out"))
SHIP_TEX = os.path.abspath(get("--ship-textures"))
RENDER = "--render" in ARGS
SAMPLES = int(get("--samples", 48))
TEX = os.path.join(OUT, "textures")
FBX = "LS_LandLaunchSite.fbx"


def main():
    bh.clear_scene()
    for f in os.listdir(SHIP_TEX):
        if f.startswith(("A7_Paint_", "A7_Brushed_")) and not os.path.exists(os.path.join(TEX, f)):
            shutil.copyfile(os.path.join(SHIP_TEX, f), os.path.join(TEX, f))
    atlas = json.load(open(os.path.join(TEX, "ls_markings.json"), encoding="utf-8"))
    specs = {m["name"]: m for m in json.load(open(os.path.join(HERE, "ls_materials.json"), encoding="utf-8"))["materials"]}
    materials = {n: bh.build_material(n, s, TEX) for n, s in specs.items()}
    for m in materials.values():
        if specs[m.name].get("surface") == "cutout":
            m.blend_method = "CLIP" if hasattr(m, "blend_method") else None
    coll = bpy.context.scene.collection
    models, anchors = ls_kit.build_kit(atlas)
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
    report = {"units": {"LS_LandLaunchSite": {"fbx": FBX, "objects": report_objects,
                                              "anchors": {k: {"point": [round(float(c), 5) for c in p]} for k, p in anchors.items()}}},
              "triangles_total": sum(o["triangles"] for o in report_objects.values())}
    json.dump(report, open(os.path.join(OUT, "ls_build_report.json"), "w", encoding="utf-8", newline="\n"), indent=1)
    bh.log("exported %s: %d objects, %d anchors, %d triangles" % (FBX, len(objects), len(empties), report["triangles_total"]))
    if RENDER:
        render(materials)
    bpy.ops.file.make_paths_relative()
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "LS_LandLaunchSite.blend"), compress=True)
    bh.log("saved LS_LandLaunchSite.blend")


def proxy(name, model, colour):
    mat = bpy.data.materials.new("PV_" + name)
    mat.use_nodes = True
    mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = colour
    obj, _ = bh.make_object("PV_" + name, model, {k: mat for k in model.parts}, bpy.context.scene.collection)
    return obj


def render(materials):
    scene = bpy.context.scene
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
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.42, 0.55, 0.72, 1.0)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.8
    scene.world = world
    sun = bpy.data.objects.new("PV_Sun", bpy.data.lights.new("PV_Sun", "SUN"))
    sun.data.energy = 3.2
    sun.rotation_euler = (math.radians(52), math.radians(12), math.radians(38))
    scene.collection.objects.link(sun)
    # context proxies of the existing scene (not exported)
    v = a7kit.Model("Vehicle")
    v.cyl("P", (0, -30.6, 0), (0, -0.55, 0), 2.35, 24)
    v.cyl("P", (0, -0.55, 0), (0, 3.2, 0), 2.45, 24)
    v.cyl("P", (0, 3.2, 0), (0, 4.95, 0), 1.2, 24)
    proxy("Vehicle", v, (0.80, 0.80, 0.78, 1))
    s = a7kit.Model("Mast")
    s.box("P", (0, -16.595, -8.5), (3.5, 32.01, 3.5), 0.0)
    s.box("P", (0, -0.19, -9.9), (8.0, 0.42, 6.0), 0.0)
    s.box("P", (0, -0.03, -4.725), (1.8, 0.06, 4.35), 0.0)
    proxy("Mast", s, (0.62, 0.30, 0.20, 1))
    g = a7kit.Model("Ground")
    g.box("P", (0, ls_kit.GROUND - 0.25, -400), (2600, 0.5, 2600), 0.0)
    proxy("Ground", g, (0.36, 0.35, 0.27, 1))
    views = {"overview": ((95, 30, 125), (0, -30, -5), 50),
             "crew_deck": ((3.6, 1.65, -9.9), (45, -32.6, 25), 68),
             "ground": ((-40, -30.6, 48), (0, -27, -2), 60),
             "towards_building": ((6, 1.65, -12.5), (0, -20, -960), 50)}
    os.makedirs(os.path.join(OUT, "renders"), exist_ok=True)
    for name, (pos, target, fov) in views.items():
        cam = bpy.data.objects.new("PV_Cam_" + name, bpy.data.cameras.new("PV_Cam_" + name))
        cam.data.sensor_fit = "VERTICAL"
        cam.data.angle_y = math.radians(fov * 0.75) if name != "crew_deck" else math.radians(fov)
        cam.data.clip_start, cam.data.clip_end = 0.05, 4000.0
        cam.location = bh.to_blender_point(pos)
        cam.rotation_euler = (bh.to_blender_point(target) - cam.location).to_track_quat("-Z", "Y").to_euler()
        scene.collection.objects.link(cam)
        scene.camera = cam
        scene.render.filepath = os.path.join(OUT, "renders", "LS_%s.png" % name)
        bpy.ops.render.render(write_still=True)
        bh.log("rendered", name)


if __name__ == "__main__":
    os.makedirs(TEX, exist_ok=True)
    main()
