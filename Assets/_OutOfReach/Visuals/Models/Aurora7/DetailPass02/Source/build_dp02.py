"""build_dp02 - Blender 5.3 background build of SHIP-DETAIL-02 (AURORA-7 interior detail pass; original work).

Modes (all paths are found relative to this file; nothing machine-specific is stored):
  build  (default)  every room into the .blend and the report; one FBX per --export room (default: --rooms) in ../Meshes (meshes and anchor empties only), the editable
                    ../Source~/DP02.blend (relative texture paths, saved before the export) and
                    ../Source~/reports/dp02_build_report.json (triangles, pivots, bounds, materials, anchors).
  --check           rebuilds the real ship from its generator (../../Source) plus the integrated habitat FBX
                    (../../../Habitat/Meshes) and measures overlap (BVH in world space from matrix_world @ v.co),
                    mount gaps and clearances to rails, controls, the airlock door sweep and the corridor.
                    Writes ../Source~/reports/dp02_check.json.
  --render          the real ship with its textures, the detail pass, Cycles stills into ../Previews~.
Run:
  blender -b --factory-startup --python build_dp02.py -- [--rooms Engineering,Habitat] [--check | --render]
Geometry is generated in Unity space (x, y, z; metres, y up) and written to Blender as (x, z, y), exactly like the
AURORA-7 ship pipeline (a7kit.to_blender); the FBX export bakes the axis conversion.
"""
import json
import math
import os
import sys
import time

import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)                                   # DetailPass02
SHIP_SOURCE = os.path.normpath(os.path.join(ROOT, "..", "Source"))
SHIP_TEXTURES = os.path.normpath(os.path.join(ROOT, "..", "Textures"))
HABITAT = os.path.normpath(os.path.join(ROOT, "..", "..", "Habitat"))
sys.dont_write_bytecode = True
sys.path.insert(0, HERE)
sys.path.insert(1, SHIP_SOURCE)
import a7kit  # noqa: E402
import dp02_layout  # noqa: E402

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
T0 = time.time()


def arg(name, default=None):
    if name in ARGS:
        i = ARGS.index(name)
        if i + 1 < len(ARGS) and not ARGS[i + 1].startswith("--"):
            return ARGS[i + 1]
        return True
    return default


ROOMS = arg("--rooms", ",".join(dp02_layout.ROOMS)).split(",")
EXPORT = arg("--export", ",".join(ROOMS)).split(",")     # build mode: rooms whose FBX is (re)written
SAMPLES = int(arg("--samples", 128))
REPORTS = os.path.join(ROOT, "Source~", "reports")
HB_SPECS = [  # render/blend specs of the habitat materials that the detail pass reuses (same values as the habitat kit)
    {"name": "HB_Labels", "atlas": "HB_Labels", "surface": "cutout", "metallic": 0.0, "smoothness": 0.25},
    {"name": "HB_BagQuilt", "set": "A7_Quilt", "tint": "#56606F", "tiling": 2.5, "normal": 1.0},
]


def log(*parts):
    print("[dp02 %6.1fs]" % (time.time() - T0), *parts, flush=True)


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
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
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


def all_materials():
    with open(os.path.join(SHIP_SOURCE, "a7_materials.json"), encoding="utf-8") as handle:
        specs = json.load(handle)["materials"]
    materials = {}
    for spec in specs:
        materials[spec["name"]] = build_material(spec["name"], spec, SHIP_TEXTURES)
    for spec in HB_SPECS:
        folder = os.path.join(HABITAT, "Textures") if "atlas" in spec else SHIP_TEXTURES
        materials[spec["name"]] = build_material(spec["name"], spec, folder)
    return materials


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
        mesh.materials.append(materials[material] if material in materials else
                              materials.setdefault(material, bpy.data.materials.new(material)))
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


def atlases():
    with open(os.path.join(SHIP_TEXTURES, "a7_decals.json"), encoding="utf-8") as handle:
        decals = json.load(handle)
    with open(os.path.join(SHIP_TEXTURES, "a7_screens.json"), encoding="utf-8") as handle:
        screens = json.load(handle)
    return decals, screens


_CONTEXT = {}


def context():
    """Atlases and the ship's Art (soft bags) shared by the cluster builders."""
    if not _CONTEXT:
        import a7_ship
        decals, screens = atlases()
        with open(os.path.join(HERE, "hb_labels_map.json"), encoding="utf-8") as handle:
            hb = json.load(handle)
        _CONTEXT.update({"decals": decals, "hb": hb, "art": a7_ship.Art(decals, screens, 11)})
    return _CONTEXT


def detail_models(room_name, decals=None):
    """[(name, placed model in ship space, pivot, cluster)] for one room."""
    out = []
    for cluster in dp02_layout.ROOMS[room_name]["clusters"]:
        local = cluster["build"](context())
        placed = a7kit.Model(cluster["name"])
        placed.instance(local, dp02_layout.frame_of(cluster))
        out.append((cluster["name"], placed, cluster["pivot"], cluster))
    return out


# ----------------------------------------------------------------------------- build mode
def build():
    clear_scene()
    decals, _ = atlases()
    materials = all_materials()
    os.makedirs(REPORTS, exist_ok=True)
    meshes_dir = os.path.join(ROOT, "Meshes")
    os.makedirs(meshes_dir, exist_ok=True)
    report_path = os.path.join(REPORTS, "dp02_build_report.json")
    report = {"units": {}, "total_triangles": 0,
              "convention": "Unity space, metres, y up; objects are placed in AURORA-7 ship coordinates; each pivot is "
                            "the origin of the cluster's mount frame (host surface, deck level); drop the FBX at the "
                            "ship origin with identity rotation"}
    if os.path.exists(report_path):     # keep the rooms that are not rebuilt this run
        with open(report_path, encoding="utf-8") as handle:
            report["units"] = json.load(handle).get("units", {})
    exports = []
    for room_name in dp02_layout.ROOMS:      # every room goes into the .blend; only EXPORT rooms are re-exported
        room = dp02_layout.ROOMS[room_name]
        collection = bpy.data.collections.new(room["fbx"])
        bpy.context.scene.collection.children.link(collection)
        objects, info = [], {"room": room_name, "objects": {}, "anchors": {}}
        for name, placed, pivot, cluster in detail_models(room_name, decals):
            obj, data = make_object(name, placed, materials, collection, pivot)
            data["host"] = cluster["host"]
            data["mount_out"] = [float(c) for c in cluster["out"]]
            info["objects"][name] = data
            objects.append(obj)
        review = room["review"]
        objects.append(make_anchor(review["anchor"], review["eye"], collection))
        forward = np.asarray(review["target"], dtype=float) - np.asarray(review["eye"], dtype=float)
        info["anchors"][review["anchor"]] = {"point": [round(float(c), 4) for c in review["eye"]],
                                             "forward": [round(float(c), 4) for c in forward / np.linalg.norm(forward)],
                                             "role": "review eye of the preview render (not gameplay)"}
        lo = np.min([o["bounds_min"] for o in info["objects"].values()], axis=0)
        hi = np.max([o["bounds_max"] for o in info["objects"].values()], axis=0)
        info["bounds_min"], info["bounds_max"] = [round(float(c), 4) for c in lo], [round(float(c), 4) for c in hi]
        info["triangles"] = sum(o["triangles"] for o in info["objects"].values())
        info["materials"] = sorted({m for o in info["objects"].values() for m in o["per_material"]})
        info["fbx"] = "Meshes/%s.fbx" % room["fbx"]
        report["units"][room["fbx"]] = info
        if room_name in EXPORT:
            exports.append((objects, os.path.join(ROOT, info["fbx"])))
        log(room_name, info["triangles"], "triangles,", len(info["objects"]), "objects,", len(info["materials"]), "materials")
    report["total_triangles"] = sum(u["triangles"] for u in report["units"].values())
    blend_dir = os.path.join(ROOT, "Source~")
    os.makedirs(blend_dir, exist_ok=True)
    # saved as a copy (with relative texture paths) so the session stays unsaved and the FBX header does not record
    # this machine's .blend path as Original|ApplicationNativeFile
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(blend_dir, "DP02.blend"), compress=True, relative_remap=True,
                                copy=True)
    log("saved Source~/DP02.blend before the export")
    for objects, path in exports:
        export_fbx(objects, path)
        log("exported", os.path.relpath(path, ROOT))
    with open(report_path, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(report, handle, indent=1)
    log("report: total", report["total_triangles"], "triangles")


# ----------------------------------------------------------------------------- ship scene (check and render)
CONTROL_FUNCTIONS = ("switch_panel", "display", "gauge", "breaker")
CONTROL_MODELS = ("console", "seat")


def ship_models(tag_controls):
    """The ship from its own generator. With tag_controls, every control's triangles get the material TAG_CONTROL."""
    import a7_ship
    if tag_controls:
        def wrap_fn(fn):
            def wrapped(m, *args, **kwargs):
                tmp = a7kit.Model("tmp")
                result = fn(tmp, *args, **kwargs)
                for chunks in tmp.parts.values():
                    for v, t, _ in chunks:
                        m.parts.setdefault("TAG_CONTROL", []).append((v, t, None))
                return result
            return wrapped

        def wrap_model(fn):
            def wrapped(*args, **kwargs):
                result = fn(*args, **kwargs)
                model = result[0] if isinstance(result, tuple) else result
                merged = [(v, t, None) for chunks in model.parts.values() for v, t, _ in chunks]
                model.parts = {"TAG_CONTROL": merged}
                return result
            return wrapped
        for name in CONTROL_FUNCTIONS:
            setattr(a7_ship, name, wrap_fn(getattr(a7_ship, name)))
        for name in CONTROL_MODELS:
            setattr(a7_ship, name, wrap_model(getattr(a7_ship, name)))
    decals, screens = atlases()
    models, _ = a7_ship.build_all(decals, screens)
    scene = {}
    for name, model in models.items():
        if name in ("Exterior", "Storage") or name.startswith("Antenna") or name.startswith("Exterior"):
            continue     # Storage is replaced in the game by the integrated habitat (imported below)
        group = getattr(model, "group", "Room")
        scene["A7_" + name] = (model, group)
    return scene, dict(a7_ship.ANCHORS), a7_ship


def import_habitat(materials, collection):
    """The integrated habitat room (A7_Storage_Habitat) and its placed kit, as delivered in the repo."""
    objects = []
    for fbx in ("A7_Storage_Habitat.fbx", "HB_Habitat_Placed.fbx"):
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=os.path.join(HABITAT, "Meshes", fbx), bake_space_transform=True)
        for obj in set(bpy.data.objects) - before:
            if obj.type != "MESH":
                continue
            for slot in obj.material_slots:
                if slot.material is not None:
                    base = slot.material.name.split(".")[0]
                    if base in materials:
                        slot.material = materials[base]
            for coll in list(obj.users_collection):
                coll.objects.unlink(obj)
            collection.objects.link(obj)
            objects.append(obj)
    return objects


def world_triangles(obj, material_filter=None):
    """World-space triangles (Blender coordinates) from matrix_world @ v.co."""
    mw = obj.matrix_world
    mesh = obj.data
    verts = [mw @ v.co for v in mesh.vertices]
    names = [s.material.name.split(".")[0] if s.material else "" for s in obj.material_slots]
    polys = []
    for p in mesh.polygons:
        if material_filter is not None and (p.material_index >= len(names) or names[p.material_index] not in material_filter):
            continue
        idx = list(p.vertices)
        for i in range(1, len(idx) - 1):
            polys.append((idx[0], idx[i], idx[i + 1]))
    return verts, polys


def bvh_of(objects, material_filter=None):
    verts, polys = [], []
    for obj in objects:
        v, p = world_triangles(obj, material_filter)
        base = len(verts)
        verts.extend(v)
        polys.extend([(a + base, b + base, c + base) for a, b, c in p])
    if not polys:
        return None, 0
    used = sorted({i for tri in polys for i in tri})
    remap = {old: new for new, old in enumerate(used)}
    return BVHTree.FromPolygons([verts[i] for i in used], [tuple(remap[i] for i in tri) for tri in polys],
                                all_triangles=True), len(polys)


def nearest(tree, points):
    best = (float("inf"), None, None)
    for p in points:
        hit = tree.find_nearest(p)
        if hit[0] is not None and hit[3] < best[0]:
            best = (hit[3], p, hit[0])
    return best


def unity(p):
    return [round(float(p[0]), 4), round(float(p[2]), 4), round(float(p[1]), 4)]


def box_distance(p, lo, hi):
    d = [max(lo[i] - p[i], 0.0, p[i] - hi[i]) for i in range(3)]
    return math.sqrt(sum(c * c for c in d))


def check():
    clear_scene()
    materials = {}
    for name in ("TAG_CONTROL",):
        materials[name] = bpy.data.materials.new(name)
    collection = bpy.context.scene.collection
    scene, anchors, a7_ship = ship_models(True)
    ship_objects, parts_objects = [], []
    for name, (model, group) in scene.items():
        obj, _ = make_object(name, model, materials, collection)
        ship_objects.append(obj)
        if group != "Room" and not name.startswith("A7_DoorLeaf"):
            parts_objects.append(obj)
    habitat_objects = import_habitat(materials, collection)
    ship_objects += habitat_objects
    bpy.context.view_layer.update()
    log("ship objects", len(ship_objects))
    ship_tree, ship_tris = bvh_of(ship_objects)
    rail_tree, rail_tris = bvh_of(ship_objects, {"A7_PaintAmber"})
    control_tree, control_tris = bvh_of(ship_objects, {"TAG_CONTROL"})
    parts_tree, _ = bvh_of(parts_objects)
    # airlock door sweep: each leaf's box swept between its closed (|x| 0.45) and open (|x| 1.4) stations
    sweep_lo, sweep_hi = [1e9] * 3, [-1e9] * 3
    for name, (model, group) in scene.items():
        if "DoorLeaf" in name:
            lo, hi = model.bounds()
            for shift in (0.0, 0.95, -0.95):
                for i in range(3):
                    sweep_lo[i] = min(sweep_lo[i], lo[i] + (shift if i == 0 else 0.0))
                    sweep_hi[i] = max(sweep_hi[i], hi[i] + (shift if i == 0 else 0.0))
    sweep_lo = [max(sweep_lo[0], -2.4), sweep_lo[1], sweep_lo[2]]
    sweep_hi = [min(sweep_hi[0], 2.4), sweep_hi[1], sweep_hi[2]]
    corridor_lo, corridor_hi = (-0.9, 0.0, -3.0), (0.9, 2.5, 21.0)
    habitat_points = {}
    layout_path = os.path.join(HABITAT, "HabitatLayout.json")
    with open(layout_path, encoding="utf-8") as handle:
        hab = json.load(handle)
    for a in hab["anchors"]:
        habitat_points[a["name"]] = a["point"]
    hab_boxes = [(c["name"], [c["centre"][i] - c["size"][i] / 2 for i in range(3)],
                  [c["centre"][i] + c["size"][i] / 2 for i in range(3)]) for c in hab["colliders"]]
    decals, _ = atlases()
    result = {"ship_triangles": ship_tris, "rail_triangles": rail_tris, "control_triangles": control_tris,
              "door_sweep_box": {"min": [round(c, 3) for c in sweep_lo], "max": [round(c, 3) for c in sweep_hi]},
              "corridor_box": {"min": corridor_lo, "max": corridor_hi, "centreline": "x = 0, y = 1.25, z -3 .. 21"},
              "definitions": {
                  "rails": "all A7_PaintAmber triangles of the ship and habitat (handrails/translation aids; also the "
                           "amber hatch-land stripe, so the number is conservative)",
                  "controls": "a7_ship switch_panel, display, gauge, breaker, console and seat geometry, the separately "
                              "addressable gameplay parts, ship ANCHORS and the habitat anchors and colliders",
                  "overlap": "BVHTree.overlap triangle pairs between each detail object and all ship and habitat "
                             "geometry, both built in world space from matrix_world @ v.co",
                  "mount_gap": "smallest distance from the object to any ship/habitat surface (seated, not floating)",
                  "max_standoff": "largest distance from any vertex of the object to the nearest ship/habitat surface"},
              "rooms": {}}
    for room_name in ROOMS:
        room_result = {}
        collection_room = bpy.data.collections.new("DP02_check_" + room_name)
        bpy.context.scene.collection.children.link(collection_room)
        for name, placed, pivot, cluster in detail_models(room_name, decals):
            obj, data = make_object(name, placed, materials, collection_room, pivot)
            bpy.context.view_layer.update()
            verts, polys = world_triangles(obj)
            mine = BVHTree.FromPolygons(verts, polys, all_triangles=True)
            overlaps = 0
            for other in ship_objects:
                v2, p2 = world_triangles(other)
                if not p2:
                    continue
                lo_o = [min(c[i] for c in v2) for i in range(3)]
                hi_o = [max(c[i] for c in v2) for i in range(3)]
                lo_m = [min(c[i] for c in verts) for i in range(3)]
                hi_m = [max(c[i] for c in verts) for i in range(3)]
                if any(lo_m[i] > hi_o[i] + 0.01 or lo_o[i] > hi_m[i] + 0.01 for i in range(3)):
                    continue
                overlaps += len(mine.overlap(BVHTree.FromPolygons(v2, p2, all_triangles=True)))
            gap = nearest(ship_tree, verts)
            standoff = max(ship_tree.find_nearest(p)[3] for p in verts)
            rails = nearest(rail_tree, verts) if rail_tree else (float("inf"), None, None)
            controls = nearest(control_tree, verts) if control_tree else (float("inf"), None, None)
            parts = nearest(parts_tree, verts) if parts_tree else (float("inf"), None, None)
            uverts = [(p[0], p[2], p[1]) for p in verts]
            anchor_d = min((math.dist(u, pt), n) for n, (_, pt) in anchors.items() for u in uverts[::3]) if anchors else (None, None)
            hab_d = min((math.dist(u, pt), n) for n, pt in habitat_points.items() for u in uverts[::3])
            hab_box_d = min((box_distance(u, lo, hi), n) for n, lo, hi in hab_boxes for u in uverts[::3])
            sweep_d = min(box_distance(u, sweep_lo, sweep_hi) for u in uverts)
            corridor_d = min(box_distance(u, corridor_lo, corridor_hi) for u in uverts)
            centre_d = min(math.hypot(u[0], u[1] - 1.25) for u in uverts)
            room_result[name] = {
                "triangles": data["triangles"], "overlap_pairs": overlaps,
                "mount_gap_m": round(gap[0], 4), "mount_point": unity(gap[1]), "max_standoff_m": round(standoff, 4),
                "min_dist_rails_m": round(rails[0], 4), "nearest_rail_point": unity(rails[2]) if rails[2] else None,
                "min_dist_controls_m": round(controls[0], 4),
                "nearest_control_point": unity(controls[2]) if controls[2] else None,
                "min_dist_gameplay_parts_m": round(parts[0], 4),
                "min_dist_ship_anchor_m": [round(anchor_d[0], 4), anchor_d[1]],
                "min_dist_habitat_anchor_m": [round(hab_d[0], 4), hab_d[1]],
                "min_dist_habitat_collider_m": [round(hab_box_d[0], 4), hab_box_d[1]],
                "min_dist_airlock_door_sweep_m": round(sweep_d, 4),
                "min_dist_corridor_box_m": round(corridor_d, 4), "min_dist_corridor_centreline_m": round(centre_d, 4)}
            log(name, room_result[name])
        result["rooms"][room_name] = room_result
    result["ship_anchor_names"] = sorted(anchors)
    os.makedirs(REPORTS, exist_ok=True)
    path = os.path.join(REPORTS, "dp02_check.json")
    if os.path.exists(path):
        with open(path, encoding="utf-8") as handle:
            previous = json.load(handle).get("rooms", {})
        for name, value in previous.items():
            result["rooms"].setdefault(name, value)
    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(result, handle, indent=1)
    log("check written")


# ----------------------------------------------------------------------------- render mode
def render():
    clear_scene()
    materials = all_materials()
    collection = bpy.context.scene.collection
    scene_models, _, a7_ship = ship_models(False)
    for name, (model, group) in scene_models.items():
        make_object(name, model, materials, collection)
    import_habitat(materials, collection)
    decals, _ = atlases()
    for room_name in ROOMS:
        for name, placed, pivot, cluster in detail_models(room_name, decals):
            make_object(name, placed, materials, collection, pivot)
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
    world = bpy.data.worlds.new("DP02_PreviewWorld")
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
            area("DP02_Ceiling_%s_%.1f" % (room_name, z), (0.0, a7_ship.CEILING - 0.135, z), (1.1, 0.28), 70.0,
                 (1.0, 0.92, 0.81))
    out_dir = os.path.join(ROOT, "Previews~")
    os.makedirs(out_dir, exist_ok=True)
    grow = (2.38 - 0.25, 1.6575 - 0.01, 14.75)
    area("DP02_GrowLight", grow, (0.84, 0.36), 22.0, (1.0, 0.86, 0.95))
    views = []
    for room_name in ROOMS:
        review = dict(dp02_layout.ROOMS[room_name]["review"], name=room_name)
        views.append(review)
        views += dp02_layout.ROOMS[room_name].get("extra_views", [])
    wanted = arg("--views", None)
    if isinstance(wanted, str):
        views = [v for v in views if v["name"] in wanted.split(",")]
    for review in views:
        room_name = review["name"]
        data = bpy.data.cameras.new("DP02_Cam_" + room_name)
        data.sensor_fit = "HORIZONTAL"
        data.angle = math.radians(review["fov"])
        data.clip_start = 0.05
        camera = bpy.data.objects.new("DP02_Cam_" + room_name, data)
        camera.location = to_blender_point(review["eye"])
        camera.rotation_euler = (to_blender_point(review["target"]) - camera.location).to_track_quat("-Z", "Y").to_euler()
        collection.objects.link(camera)
        scene.camera = camera
        scene.render.filepath = os.path.join(out_dir, "DP02_%s.png" % room_name)
        bpy.ops.render.render(write_still=True)
        log("rendered", room_name)


if __name__ == "__main__":
    if arg("--check", False):
        check()
    elif arg("--render", False):
        render()
    else:
        build()
    log("done")
