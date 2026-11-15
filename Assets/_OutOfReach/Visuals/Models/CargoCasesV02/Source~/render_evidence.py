"""CARGO-CASES-V02 evidence renders (Cycles CPU, textures as shipped; no Unity needed).

    blender --background Source~/CargoCasesV02.blend --python Source~/render_evidence.py

Writes Evidence~/blender_crate_<Key>.png for the seven containers and Evidence~/blender_rack_assembled.png (frame,
cells and containers at the authored cell positions). Evidence~/ is excluded locally (.git/info/exclude), so the
committed copies live in Previews~/. Material scalars mirror CargoCasesV02Builder.
"""
import math
import os

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, ".."))
TEX = os.path.join(ROOT, "Textures")
OUT = os.path.join(ROOT, "Evidence~")

# name: (texture, sRGB tint, metallic, smoothness)
MATS = {
    "CC2_Shell_Water": ("CC2_Quilt", (.10, .60, .78), 0, .28), "CC2_Shell_Food": ("CC2_Polymer", (.48, .63, .21), 0, .36),
    "CC2_Shell_Battery": ("CC2_Painted", (1, .52, .075), 0, .42), "CC2_Shell_Oxygen": ("CC2_Painted", (.93, .96, .96), 0, .45),
    "CC2_Shell_Solar": ("CC2_Painted", (.24, .38, .68), .1, .45), "CC2_Shell_Spares": ("CC2_Polymer", (.74, .25, .16), 0, .36),
    "CC2_Shell_Science": ("CC2_Painted", (.51, .37, .64), .15, .42), "CC2_Aluminium": ("CC2_Aluminium", (.88, .89, .9), .9, .55),
    "CC2_Rubber": ("CC2_Rubber", (.09, .09, .095), 0, .25), "CC2_Graphite": ("CC2_Polymer", (.2, .21, .23), 0, .35),
    "CC2_Webbing": ("CC2_Webbing", (.33, .34, .36), 0, .2), "CC2_WebbingAmber": ("CC2_Webbing", (1, .62, .12), 0, .2),
    "CC2_RedCap": ("CC2_Polymer", (.85, .12, .08), 0, .45), "CC2_Composite": ("CC2_Composite", (.95, .95, .92), 0, .5),
    "CC2_SolarCell": ("CC2_SolarCell", (1, 1, 1), .3, .75), "CC2_RackSteel": ("CC2_Painted", (.2, .22, .25), .2, .4),
    "CC2_Labels": ("CC2_Labels", (1, 1, 1), 0, .3), "CC2_LED": (None, (.2, 1, .45), 0, .7),
}
CASES = ["Water", "Oxygen", "Food", "Battery", "Solar", "Spares", "Science"]
CELLS = {"Oxygen": (-.315, 0), "Water": (.315, 0), "Food": (-.315, 1), "Battery": (.315, 1), "Solar": (-.315, 2),
         "Spares": (.315, 2), "Science": (-.315, 3)}


def lin(c):
    return tuple(v ** 2.2 for v in c) + (1,)


def setup_material(name):
    mat = bpy.data.materials.get(name)
    if not mat or name not in MATS:
        return
    tex, tint, metallic, smooth = MATS[name]
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = 1 - smooth
    if tex is None:
        bsdf.inputs["Base Color"].default_value = lin(tint)
        bsdf.inputs["Emission"].default_value = lin(tint)
        bsdf.inputs["Emission Strength"].default_value = 3
        return
    img = nt.nodes.new("ShaderNodeTexImage")
    img.image = bpy.data.images.load(os.path.join(TEX, tex + "_BaseColor.png"), check_existing=True)
    mul = nt.nodes.new("ShaderNodeMixRGB")
    mul.blend_type = 'MULTIPLY'
    mul.inputs["Fac"].default_value = 1
    mul.inputs["Color2"].default_value = lin(tint)
    nt.links.new(img.outputs["Color"], mul.inputs["Color1"])
    nt.links.new(mul.outputs["Color"], bsdf.inputs["Base Color"])
    if name == "CC2_Labels":
        nt.links.new(img.outputs["Alpha"], bsdf.inputs["Alpha"])
        mat.blend_method = 'CLIP'
        return
    npath = os.path.join(TEX, tex + "_Normal.png")
    if os.path.exists(npath):
        nimg = nt.nodes.new("ShaderNodeTexImage")
        nimg.image = bpy.data.images.load(npath, check_existing=True)
        nimg.image.colorspace_settings.name = 'Non-Color'
        nmap = nt.nodes.new("ShaderNodeNormalMap")
        nmap.inputs["Strength"].default_value = .8
        nt.links.new(nimg.outputs["Color"], nmap.inputs["Color"])
        nt.links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])


def look_at(cam, pos, target):
    cam.location = pos
    cam.rotation_euler = (Vector(target) - Vector(pos)).to_track_quat('-Z', 'Y').to_euler()


def main():
    os.makedirs(OUT, exist_ok=True)
    scene = bpy.context.scene
    for name in MATS:
        setup_material(name)
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = 40
    scene.cycles.use_denoising = True
    scene.cycles.max_bounces = 4
    scene.render.resolution_x, scene.render.resolution_y = 1152, 720
    scene.render.image_settings.file_format = 'PNG'
    scene.view_settings.look = 'Medium High Contrast'
    world = bpy.data.worlds.new("CC2_Evidence")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (.55, .62, .72, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = .55
    scene.world = world
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", 'SUN'))
    sun.data.energy = 3.6
    sun.data.angle = math.radians(4)
    sun.rotation_euler = (math.radians(52), 0, math.radians(-38))
    scene.collection.objects.link(sun)
    floor_me = bpy.data.meshes.new("Floor")
    floor_me.from_pydata([(-4, -4, 0), (4, -4, 0), (4, 4, 0), (-4, 4, 0)], [], [(0, 1, 2, 3)])
    floor_mat = bpy.data.materials.new("CC2_EvidenceFloor")
    floor_mat.use_nodes = True
    fb = floor_mat.node_tree.nodes["Principled BSDF"]
    fb.inputs["Base Color"].default_value = (.045, .05, .055, 1)
    fb.inputs["Roughness"].default_value = .7
    floor_me.materials.append(floor_mat)
    floor = bpy.data.objects.new("Floor", floor_me)
    scene.collection.objects.link(floor)
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    cam.data.lens = 50
    scene.collection.objects.link(cam)
    scene.camera = cam

    models = {o.name: o for o in bpy.data.objects if o.type == 'MESH' and o.name.endswith("_LOD0")}
    for o in bpy.data.objects:
        if o.type == 'MESH' and o is not floor:
            o.hide_render = True
    for key in CASES:
        ob = models[key + "_LOD0"]
        ob.hide_render = False
        home = ob.location.copy()
        ob.location = (0, 0, 0)
        look_at(cam, (-.62, -1.02, .6), (0, 0, .13))
        scene.render.filepath = os.path.join(OUT, "blender_crate_%s.png" % key)
        bpy.ops.render.render(write_still=True)
        ob.location = home
        ob.hide_render = True

    # assembled rack: frame at the origin (its own z is the dock height), cells and containers on their cells
    plinth_me = bpy.data.meshes.new("Plinth")
    w, d, h = .75, .66, .2
    plinth_me.from_pydata([(-w, -d + .1, 0), (w, -d + .1, 0), (w, d + .6, 0), (-w, d + .6, 0), (-w, -d + .1, h), (w, -d + .1, h), (w, d + .6, h), (-w, d + .6, h)],
                          [], [(0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7), (4, 5, 6, 7)])
    plinth_me.materials.append(floor_mat)
    plinth = bpy.data.objects.new("Plinth", plinth_me)
    scene.collection.objects.link(plinth)
    frame = models["RackFrame_LOD0"]
    frame.hide_render = False
    frame.location = (0, 0, 0)
    for key, (x, row) in CELLS.items():
        for name in (key + "_LOD0", "RackCell_" + key + "_LOD0"):
            ob = models[name]
            ob.hide_render = False
            ob.location = (x, 0, .195 + .4 * row)
    cam.data.lens = 35
    look_at(cam, (1.55, -2.75, 1.5), (0, -.1, 1.0))
    scene.render.resolution_x, scene.render.resolution_y = 1152, 1152
    scene.render.filepath = os.path.join(OUT, "blender_rack_assembled.png")
    bpy.ops.render.render(write_still=True)
    print("CC2_EVIDENCE_DONE")


if __name__ == "__main__":
    main()
