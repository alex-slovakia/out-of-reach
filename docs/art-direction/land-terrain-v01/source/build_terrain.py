"""build_terrain - LAND-TERRAIN-V01 in Blender 5.3: LT_LandTerrain.fbx (3 mesh objects + 4 anchors), the build report,
the .blend and review renders.

Every object is its own mesh pivoted at the prologue origin (Unity metres, y up); anchors are empties. The FBX holds
meshes and empties only. Terrain vertices carry analytic normals from the height field (lt_height.normal), so the
inner/far seam and the flat apron shade identically; rocks use the a7kit smoothing (sharp above 50 degrees).
The preview adds the LAND kit (rebuilt from its own source), a vehicle and mast proxy, a sky and a sun; none of them is
exported. With --surroundings, LAND-SURROUNDINGS v02 (lss_kit_v02, re-seated on h) is added to the preview as well.

Run: blender -b --factory-startup --python build_terrain.py -- --out <dir> --textures <dir with LT_ maps>
     --land-textures <dir with LS_, A7_Paint_, A7_Brushed_ maps and ls_markings.json> --land-src <LAND source dir>
     [--render --samples 32] [--surroundings --lss-textures <dir with LSS_ maps>] [--views a,b]
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
import build_habitat as bh  # noqa: E402
import lt_height as H  # noqa: E402
import lt_kit  # noqa: E402

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
get = lambda k, d=None: ARGS[ARGS.index(k) + 1] if k in ARGS else d
OUT = os.path.abspath(get("--out", "."))
TEX = os.path.abspath(get("--textures", os.path.join(OUT, "textures")))
LAND_TEX = os.path.abspath(get("--land-textures", TEX))
LSS_TEX = os.path.abspath(get("--lss-textures", LAND_TEX))
LAND_SRC = get("--land-src")
RENDER = "--render" in ARGS
SURROUNDINGS = "--surroundings" in ARGS
SAMPLES = int(get("--samples", 32))
VIEWS = get("--views")
FBX = "LT_LandTerrain.fbx"
DETAIL_TILING = 1280.0
HAZE_M = 10000.0


# ----------------------------------------------------------------------------- materials
def terrain_material(name, detail):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = next(n for n in nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Roughness"].default_value = 0.9 if detail else 0.92
    coord = nodes.new("ShaderNodeTexCoord")
    macro = nodes.new("ShaderNodeTexImage")
    macro.image = bh.image(TEX, "LT_Terrain_Macro.png")
    macro.extension = "EXTEND"
    links.new(coord.outputs["UV"], macro.inputs["Vector"])
    if not detail:
        links.new(macro.outputs["Color"], bsdf.inputs["Base Color"])
        return mat
    mapping = nodes.new("ShaderNodeMapping")
    mapping.inputs["Scale"].default_value = (DETAIL_TILING, DETAIL_TILING, 1.0)
    links.new(coord.outputs["UV"], mapping.inputs["Vector"])
    albedo = nodes.new("ShaderNodeTexImage")
    albedo.image = bh.image(TEX, "LT_TerrainDetail_Albedo.png")
    links.new(mapping.outputs["Vector"], albedo.inputs["Vector"])
    mul = nodes.new("ShaderNodeMix")
    mul.data_type = "RGBA"
    mul.blend_type = "MULTIPLY"
    mul.inputs[0].default_value = 1.0
    links.new(macro.outputs["Color"], mul.inputs[6])
    links.new(albedo.outputs["Color"], mul.inputs[7])
    twice = nodes.new("ShaderNodeVectorMath")          # URP "Detail Albedo x2"
    twice.operation = "SCALE"
    twice.inputs["Scale"].default_value = 2.0
    links.new(mul.outputs[2], twice.inputs[0])
    links.new(twice.outputs[0], bsdf.inputs["Base Color"])
    ntex = nodes.new("ShaderNodeTexImage")
    ntex.image = bh.image(TEX, "LT_TerrainDetail_Normal.png", True)
    links.new(mapping.outputs["Vector"], ntex.inputs["Vector"])
    nmap = nodes.new("ShaderNodeNormalMap")
    nmap.uv_map = "UVMap"
    nmap.inputs["Strength"].default_value = 1.0
    links.new(ntex.outputs["Color"], nmap.inputs["Color"])
    links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
    return mat


def build_materials():
    specs = {m["name"]: m for m in json.load(open(os.path.join(HERE, "lt_materials.json"), encoding="utf-8"))["materials"]}
    return {"LT_Terrain": terrain_material("LT_Terrain", True),
            "LT_TerrainFar": terrain_material("LT_TerrainFar", False),
            "LT_Rock": bh.build_material("LT_Rock", specs["LT_Rock"], LAND_TEX)}


# ----------------------------------------------------------------------------- objects
def make_terrain_object(model, materials, collection):
    """One material, explicit per-corner UV0, analytic per-vertex normals from the height field."""
    (material,) = model.parts.keys()
    v, t, uv = model.arrays(material)
    n = H.normal(v[:, 0], v[:, 2])
    vb, tb = a7kit.to_blender(v, t)
    mesh = bpy.data.meshes.new(model.name)
    mesh.from_pydata(vb.tolist(), [], tb.tolist())
    mesh.polygons.foreach_set("use_smooth", np.ones(len(tb), dtype=bool))
    layer = mesh.uv_layers.new(name="UVMap")
    layer.data.foreach_set("uv", uv[:, ::-1, :].astype(np.float32).ravel())
    mesh.materials.append(materials[material])
    mesh.update(calc_edges=True)
    mesh.normals_split_custom_set_from_vertices(n[:, [0, 2, 1]].tolist())
    obj = bpy.data.objects.new(model.name, mesh)
    collection.objects.link(obj)
    return obj


def report_entry(model):
    lo, hi = model.bounds()
    return {"triangles": model.triangle_count(), "origin": [0.0, 0.0, 0.0],
            "bounds_min": [round(float(c), 5) for c in lo], "bounds_max": [round(float(c), 5) for c in hi],
            "per_material": {k: int(sum(len(t) for _, t, _ in v)) for k, v in model.parts.items()}}


def main():
    bh.clear_scene()
    materials = build_materials()
    coll = bpy.context.scene.collection
    models, anchors, info = lt_kit.build_kit()
    objects, report_objects = [], {}
    for model in models:
        if model.name == "LT_Rocks":
            obj, _ = bh.make_object(model.name, model, materials, coll)
        else:
            obj = make_terrain_object(model, materials, coll)
        objects.append(obj)
        report_objects[model.name] = report_entry(model)
    empties = [bh.make_anchor(name, p, coll) for name, p in anchors.items()]
    bh.export_fbx(objects + empties, os.path.join(OUT, FBX))
    report = {"units": {"LT_LandTerrain": {"fbx": FBX, "objects": report_objects,
                                           "anchors": {k: {"point": [round(float(c), 5) for c in p]} for k, p in anchors.items()}}},
              "triangles_total": sum(o["triangles"] for o in report_objects.values()),
              "kit_info": info, "height_field": H.describe()}
    json.dump(report, open(os.path.join(OUT, "lt_build_report.json"), "w", encoding="utf-8", newline="\n"), indent=1)
    bh.log("exported %s: %d objects, %d anchors, %d triangles" % (FBX, len(objects), len(empties), report["triangles_total"]))
    if RENDER:
        render(info)
    bpy.ops.file.make_paths_relative()
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "LT_LandTerrain.blend"), compress=True)
    bh.log("saved LT_LandTerrain.blend")


# ----------------------------------------------------------------------------- preview
def plain(name, colour):
    mat = bpy.data.materials.new("PV_" + name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = colour
    bsdf.inputs["Roughness"].default_value = 0.95
    return mat


def add_context(coll):
    sys.path.insert(0, os.path.abspath(LAND_SRC))
    import ls_kit  # noqa: E402
    land_specs = {m["name"]: m for m in json.load(open(os.path.join(LAND_SRC, "ls_materials.json"), encoding="utf-8"))["materials"]}
    land_mats = {n: (bpy.data.materials.get(n) or bh.build_material(n, s, LAND_TEX)) for n, s in land_specs.items()}
    atlas = json.load(open(os.path.join(LAND_TEX, "ls_markings.json"), encoding="utf-8"))
    for model in ls_kit.build_kit(atlas)[0]:
        bh.make_object("PV_" + model.name, model, land_mats, coll)
    v = a7kit.Model("Vehicle")
    v.cyl("P", (0, -30.6, 0), (0, -0.55, 0), 2.35, 24)
    v.cyl("P", (0, -0.55, 0), (0, 3.2, 0), 2.45, 24)
    v.cyl("P", (0, 3.2, 0), (0, 4.95, 0), 1.2, 24)
    bh.make_object("PV_Vehicle", v, {"P": plain("Vehicle", (0.82, 0.80, 0.76, 1))}, coll)
    s = a7kit.Model("Mast")
    s.box("P", (0, -16.595, -8.5), (3.5, 32.01, 3.5), 0.0)
    s.box("P", (0, -0.19, -9.9), (8.0, 0.42, 6.0), 0.0)
    bh.make_object("PV_Mast", s, {"P": plain("Mast", (0.62, 0.30, 0.20, 1))}, coll)
    if SURROUNDINGS:
        import lss_kit_v02  # noqa: E402
        lss_specs = {m["name"]: m for m in json.load(open(os.path.join(HERE, "lss_materials.json"), encoding="utf-8"))["materials"]}
        lss_mats = {}
        for n, sp in lss_specs.items():
            lss_mats[n] = bpy.data.materials.get(n) or bh.build_material(
                n, sp, LSS_TEX if sp.get("set", "").startswith("LSS_") else LAND_TEX)
        for model in lss_kit_v02.build_kit()[0]:
            bh.make_object("PV_" + model.name, model, lss_mats, coll)


def sky_world():
    world = bpy.data.worlds.new("PV_Sky")
    world.use_nodes = True
    nt = world.node_tree
    nodes, links = nt.nodes, nt.links
    out = nodes["World Output"]
    flat = nodes["Background"]
    flat.inputs[0].default_value = (0.45, 0.58, 0.76, 1.0)
    flat.inputs[1].default_value = 0.85
    coord = nodes.new("ShaderNodeTexCoord")
    sep = nodes.new("ShaderNodeSeparateXYZ")
    links.new(coord.outputs["Generated"], sep.inputs[0])
    rng = nodes.new("ShaderNodeMapRange")
    rng.inputs["From Min"].default_value = -0.25
    rng.inputs["From Max"].default_value = 1.0
    links.new(sep.outputs["Z"], rng.inputs["Value"])
    ramp = nodes.new("ShaderNodeValToRGB")
    els = ramp.color_ramp.elements
    els[0].position, els[0].color = 0.0, (0.55, 0.60, 0.62, 1.0)
    els[1].position, els[1].color = 1.0, (0.22, 0.38, 0.68, 1.0)
    for pos, col in ((0.2, (0.66, 0.74, 0.82, 1.0)), (0.45, (0.42, 0.56, 0.78, 1.0))):
        e = els.new(pos)
        e.color = col
    links.new(rng.outputs["Result"], ramp.inputs["Fac"])
    cam_bg = nodes.new("ShaderNodeBackground")
    links.new(ramp.outputs["Color"], cam_bg.inputs["Color"])
    cam_bg.inputs["Strength"].default_value = 1.0
    path = nodes.new("ShaderNodeLightPath")
    mixs = nodes.new("ShaderNodeMixShader")
    links.new(path.outputs["Is Camera Ray"], mixs.inputs[0])
    links.new(flat.outputs[0], mixs.inputs[1])
    links.new(cam_bg.outputs[0], mixs.inputs[2])
    links.new(mixs.outputs[0], out.inputs["Surface"])
    return world


def depth_material():
    mat = bpy.data.materials.new("PV_HazeDepth")
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    for n in list(nodes):
        if n.type != "OUTPUT_MATERIAL":
            nodes.remove(n)
    out = next(n for n in nodes if n.type == "OUTPUT_MATERIAL")
    cam = nodes.new("ShaderNodeCameraData")
    div = nodes.new("ShaderNodeMath")
    div.operation = "DIVIDE"
    div.inputs[1].default_value = -HAZE_M
    links.new(cam.outputs["View Distance"], div.inputs[0])
    ex = nodes.new("ShaderNodeMath")
    ex.operation = "EXPONENT"
    links.new(div.outputs[0], ex.inputs[0])
    sub = nodes.new("ShaderNodeMath")
    sub.operation = "SUBTRACT"
    sub.inputs[0].default_value = 1.0
    links.new(ex.outputs[0], sub.inputs[1])
    em = nodes.new("ShaderNodeEmission")
    links.new(sub.outputs[0], em.inputs["Color"])
    em.inputs["Strength"].default_value = 1.0
    links.new(em.outputs[0], out.inputs["Surface"])
    return mat


def view_list(info):
    gy = lambda x, z: float(H.ground_y(np.array([x]), np.array([z]))[0])
    # ground view: stand 22 m outside the rock cluster nearest to (330, 60), east of the pad, and look at the pad
    cx, cz = min(info["LT_Rocks"]["cluster_centres_xz"], key=lambda p: math.hypot(p[0] - 330.0, p[1] - 60.0))
    k = 22.0 / math.hypot(cx, cz)
    ex, ez = cx * (1 + k), cz * (1 + k)
    return {"deck_north": ((3.6, 1.65, -9.9), (0.0, -36.6, -960.0), 40),
            "deck_east": ((3.6, 1.65, -9.9), (700.0, gy(700.0, 260.0), 260.0), 50),
            "aerial": ((260.0, 210.0, 640.0), (0.0, -36.6, -160.0), 50),
            "ground": ((ex, gy(ex, ez) + 1.7, ez), (0.0, -18.0, 0.0), 45)}


def render(info):
    scene = bpy.context.scene
    coll = bpy.data.collections.new("PV_Context")
    scene.collection.children.link(coll)
    add_context(coll)
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
    scene.render.resolution_x, scene.render.resolution_y = 1280, 720
    sky = sky_world()
    black = bpy.data.worlds.new("PV_Black")
    black.use_nodes = True
    black.node_tree.nodes["Background"].inputs[1].default_value = 0.0
    sun = bpy.data.objects.new("PV_Sun", bpy.data.lights.new("PV_Sun", "SUN"))
    sun.data.energy = 3.4
    sun.data.angle = math.radians(0.6)
    sun.rotation_euler = (math.radians(58), math.radians(10), math.radians(32))
    coll.objects.link(sun)
    depth = depth_material()
    views = view_list(info)
    wanted = VIEWS.split(",") if VIEWS else list(views)
    os.makedirs(os.path.join(OUT, "renders"), exist_ok=True)
    prefix = "LT2" if SURROUNDINGS else "LT"
    used = {}
    for name in wanted:
        pos, target, fov = views[name]
        used[name] = {"eye": [round(c, 3) for c in pos], "target": [round(c, 3) for c in target], "fov_v_deg": fov}
        cam = bpy.data.objects.new("PV_Cam_" + name, bpy.data.cameras.new("PV_Cam_" + name))
        cam.data.sensor_fit = "VERTICAL"
        cam.data.angle_y = math.radians(fov)
        cam.data.clip_start, cam.data.clip_end = 0.05, 20000.0
        cam.location = bh.to_blender_point(pos)
        cam.rotation_euler = (bh.to_blender_point(target) - cam.location).to_track_quat("-Z", "Y").to_euler()
        coll.objects.link(cam)
        scene.camera = cam
        # beauty
        scene.world = sky
        scene.view_layers[0].material_override = None
        scene.render.film_transparent = False
        scene.view_settings.view_transform = "AgX"
        scene.cycles.samples = SAMPLES
        scene.cycles.use_denoising = True
        scene.render.image_settings.color_mode = "RGB"
        scene.render.filepath = os.path.join(OUT, "renders", "%s_%s_raw.png" % (prefix, name))
        bpy.ops.render.render(write_still=True)
        # haze fraction 1 - exp(-distance / 10 km), linear grey, alpha = coverage
        scene.world = black
        scene.view_layers[0].material_override = depth
        scene.render.film_transparent = True
        scene.view_settings.view_transform = "Standard"
        scene.cycles.samples = 8
        scene.cycles.use_denoising = False
        scene.render.image_settings.color_mode = "RGBA"
        scene.render.filepath = os.path.join(OUT, "renders", "%s_%s_depth.png" % (prefix, name))
        bpy.ops.render.render(write_still=True)
        bh.log("rendered", name)
    scene.world = sky
    scene.view_layers[0].material_override = None
    scene.render.film_transparent = False
    scene.view_settings.view_transform = "AgX"
    json.dump({"views": used, "samples": SAMPLES, "haze_m": HAZE_M},
              open(os.path.join(OUT, "renders", "%s_views.json" % prefix), "w", encoding="utf-8", newline="\n"), indent=1)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    main()
