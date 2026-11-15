"""Render front/back of an FBX (or .blend objects) on a grey ground, like the reference boards.
blender -b --factory-startup --python render_fit.py -- <fbx> <out_prefix> [--mats materials.json] [--res 1024]
Colours come from materials.json by material name (MAT_ prefix stripped, .001 suffix stripped)."""
import bpy, sys, json, os, math
import numpy as np
from mathutils import Vector

A = sys.argv[sys.argv.index("--") + 1:]
ZOOM_ARG = A[A.index("--lens") + 1] if "--lens" in A else None
FBX, PREFIX = os.path.abspath(A[0]), os.path.abspath(A[1])
MATS = A[A.index("--mats") + 1] if "--mats" in A else None
RES = int(A[A.index("--res") + 1]) if "--res" in A else 1024
POSE = A[A.index("--pose") + 1] if "--pose" in A else None
DEBUG = "--debug" in A
KEEP = "--keepmats" in A
CAMS = A[A.index("--cams") + 1].split(",") if "--cams" in A else ["front", "back"]
SCALEL = float(A[A.index("--light") + 1]) if "--light" in A else 1.0


def srgb2lin(c):
    c = c / 255.0
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def hex2rgb(h):
    h = h.lstrip("#")
    return [srgb2lin(int(h[i:i + 2], 16)) for i in (0, 2, 4)] + [1.0]


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if FBX.endswith(".blend"):
        with bpy.data.libraries.load(FBX) as (src, dst):
            dst.objects = list(src.objects)
        for o in dst.objects:
            if o is not None:
                bpy.context.scene.collection.objects.link(o)
    else:
        bpy.ops.import_scene.fbx(filepath=FBX)
    scn = bpy.context.scene
    table = json.load(open(MATS)) if MATS else {}
    arm = next((o for o in scn.objects if o.type == "ARMATURE"), None)
    if arm and POSE:
        act = next((a for a in bpy.data.actions if a.name.endswith(POSE)), None)
        if act:
            arm.animation_data_create().action = act
            scn.frame_set(1)
    if arm:
        arm.animation_data_clear() if arm.animation_data else None
        arm.data.pose_position = "REST"
    seen = {}
    for o in scn.objects:
        if o.type != "MESH":
            continue
        for i, slot in enumerate(o.material_slots):
            m = slot.material
            if not m or (KEEP and not DEBUG):
                continue
            key = m.name
            if key in seen:
                slot.material = seen[key]
                continue
            nm = key
            if nm.startswith("MAT_"):
                nm = nm[4:]
            nm = nm.split(".")[0]
            nm2 = nm
            spec = table.get(nm2) or table.get(nm2.replace("_", "")) or {}
            nm = bpy.data.materials.new("R_" + key)
            nm.use_nodes = True
            bsdf = nm.node_tree.nodes["Principled BSDF"]
            col = hex2rgb(spec["base_color_srgb"]) if "base_color_srgb" in spec else [0.5, 0.5, 0.5, 1]
            bsdf.inputs["Base Color"].default_value = col
            bsdf.inputs["Roughness"].default_value = spec.get("roughness", 0.8)
            bsdf.inputs["Metallic"].default_value = spec.get("metallic", 0.0)
            if "emission" in spec:
                bsdf.inputs["Emission Color"].default_value = hex2rgb(spec["emission"])
                bsdf.inputs["Emission Strength"].default_value = spec.get("emission_strength", 2.0)
            if DEBUG:
                pal = [(1,0,0),(0,1,0),(0,0,1),(1,1,0),(1,0,1),(0,1,1),(1,.5,0),(.5,0,1),(.5,.5,.5),(1,1,1)]
                bsdf.inputs["Base Color"].default_value = list(pal[len(seen) % len(pal)]) + [1]
            slot.material = nm
            seen[key] = nm
    # world, lights
    w = bpy.data.worlds.new("W"); scn.world = w; w.use_nodes = True
    bg = w.node_tree.nodes["Background"]; bg.inputs[0].default_value = (0.60, 0.56, 0.55, 1); bg.inputs[1].default_value = 1.0
    ground = bpy.data.objects.new("G", bpy.data.meshes.new("G"))
    me = ground.data
    me.from_pydata([(-6, -6, 0), (6, -6, 0), (6, 6, 0), (-6, 6, 0)], [], [(0, 1, 2, 3)])
    gm = bpy.data.materials.new("GM"); gm.use_nodes = True
    gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.38, 0.35, 0.34, 1)
    gm.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 1.0
    me.materials.append(gm)
    scn.collection.objects.link(ground)
    for name, loc, energy in (("K", (2.5, -3.5, 4.0), 900), ("F", (-3, -2.5, 2.0), 350), ("B", (2, 4, 3), 500)):
        ld = bpy.data.lights.new(name, "AREA"); ld.energy = energy * SCALEL; ld.size = 3
        lo = bpy.data.objects.new(name, ld); lo.location = loc
        d = Vector((0, 0, 0.95)) - Vector(loc)
        lo.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
        scn.collection.objects.link(lo)
    cam = bpy.data.objects.new("C", bpy.data.cameras.new("C")); scn.collection.objects.link(cam); scn.camera = cam
    cam.data.lens = 85 if not ZOOM_ARG else float(ZOOM_ARG); cam.data.sensor_width = 36
    scn.render.engine = "BLENDER_EEVEE"
    try:
        scn.eevee.taa_render_samples = 32
    except Exception:
        pass
    scn.render.resolution_x = int(RES * 0.78); scn.render.resolution_y = RES
    scn.view_settings.view_transform = "Standard"
    outs = []
    ZOOM = A[A.index("--zoom") + 1].split(",") if "--zoom" in A else None   # z_center,dist
    zc, dist = (float(ZOOM[0]), float(ZOOM[1])) if ZOOM else (0.95, 5.6)
    azi = {"front": 0, "back": 180, "side": 90, "q3": 35, "q3b": 215}
    for tag in CAMS:
        a_ = math.radians(azi[tag])
        # azimuth 0 = camera on -Y looking +Y (sees the front, character faces -Y)
        cam.location = (math.sin(a_) * dist, -math.cos(a_) * dist, zc)
        d = Vector((0, 0, zc)) - cam.location
        cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
        p = PREFIX + "_" + tag + ".png"
        scn.render.filepath = p
        bpy.ops.render.render(write_still=True)
        outs.append(p)
    # stitch
    imgs = [bpy.data.images.load(p) for p in outs]
    arrs = [np.array(i.pixels[:], dtype=np.float32).reshape(i.size[1], i.size[0], 4) for i in imgs]
    both = np.concatenate(arrs, axis=1)
    out = bpy.data.images.new("both", both.shape[1], both.shape[0], alpha=True)
    out.pixels = both.ravel().tolist()
    out.filepath_raw = PREFIX + "_pair.png"; out.file_format = "PNG"; out.save()
    print("RENDERED", PREFIX)


main()
