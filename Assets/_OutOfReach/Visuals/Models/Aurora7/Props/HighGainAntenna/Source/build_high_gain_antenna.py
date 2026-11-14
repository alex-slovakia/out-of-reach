"""
OUT OF REACH - high-gain antenna assembly HGA-1 (original asset, built from code).

Builds the model, its label atlas and baked PBR textures, renders previews, exports the FBX
and saves the .blend. Everything is generated here: no external models, textures or logos.

    blender -b --factory-startup --python build_high_gain_antenna.py -- --stage full
    blender -b --factory-startup --python build_high_gain_antenna.py -- --stage geometry   (fast shape check)

Axes (Blender): X = width, Z = along the boom (up), -Y = front = boresight of the dish in the neutral pose.
Model pivot: centre of the underside of the base plate (0, 0, 0). Units: metres.
Objects: Base | Azimuth (turns about Z) with its child Elevation (turns about X) | ElectronicsBox | StatusIndicators.
The shared helpers come from the EPS-2 service panel and ARS-1 unit scripts.
"""


import json
import math
import os
import sys
import time
from math import cos, pi, radians, sin

import bmesh
import bpy
from mathutils import Matrix, Vector

T0 = time.time()


def log(*parts):
    print("[antenna %6.1fs]" % (time.time() - T0), *parts, flush=True)


# ------------------------------------------------------------------ arguments
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def arg(name, default=None):
    if name in ARGS:
        i = ARGS.index(name)
        if i + 1 < len(ARGS) and not ARGS[i + 1].startswith("--"):
            return ARGS[i + 1]
        return True
    return default


OUT = os.path.abspath(arg("--out", os.path.dirname(os.path.abspath(__file__))))
STAGE = arg("--stage", "full")            # geometry | full
TEX = int(arg("--tex", 2048))             # baked texture size
SAMPLES = int(arg("--samples", 192))      # beauty render samples
TEX_DIR = os.path.join(OUT, "textures")
RENDER_DIR = os.path.join(OUT, "renders")
for folder in (OUT, TEX_DIR, RENDER_DIR):
    os.makedirs(folder, exist_ok=True)


# ------------------------------------------------------------------ dimensions (metres)
NAME = "HighGainAntenna"
PLATE, PLATE_T = 0.420, 0.020                    # square base plate that sits on the hull pad
PED_R0, PED_R1, PED_Z = 0.095, 0.072, 0.120      # tapered pedestal: foot radius, neck radius, top height
BOOM_R, BOOM_Z1 = 0.045, 0.700
AZ_R, AZ_Z1 = 0.092, 0.800                       # azimuth drive housing on the boom (does not turn)
TURN_R, TURN_Z1 = 0.100, 0.835                   # turntable: the first part of the azimuth stage
FORK_X, FORK_Y, FORK_Z1 = 0.215, 0.075, 0.930    # half-size of the fork base block, and its top
ARM_X, ARM_T, ARM_D = 0.190, 0.050, 0.110        # fork arm: centre in x, thickness, depth
ARM_IN = ARM_X - ARM_T / 2                       # inner face of an arm
EL_Z = 1.400                                     # height of the elevation axis
BOSS_R = 0.075                                   # arm head around the elevation axis
MOTOR_R, MOTOR_L = 0.062, 0.085                  # elevation drive housing outside the +x arm
DISH_R, DISH_F, DISH_T = 0.790, 0.560, 0.012     # reflector radius, focal length, shell thickness
RIM_R = 0.800
VERTEX_Y = -0.170                                # reflector vertex, in front of the elevation axis
PLATE_Y0, PLATE_Y1 = -0.085, -0.070              # bracket back plate: front and back plane
BRACKET_X, BRACKET_Z0, BRACKET_Z1 = 0.200, 0.100, 0.560
CLAMP_Z = (0.140, 0.500)                         # the two clamps that hold the bracket on the boom
SHELF_Z = 0.120                                  # top of the shelf under the electronics box
BOX_X, BOX_Y1, BOX_Z1 = 0.160, -0.300, 0.365     # electronics box: half-width, front plane, top
BOX_Y0, BOX_Z0 = PLATE_Y0 - 0.005, SHELF_Z + 0.005
SUB = (-0.185, -0.015, 0.405, 0.530)             # status sub-panel: x0, x1, z0, z1
Y_SUB = PLATE_Y0 - 0.004                         # front of the status sub-panel
LAMP_X, LAMP_Z = (-0.155, -0.100, -0.045), 0.481
RAIL_X = -0.260                                  # EVA handrail beside the boom, clear of the bracket
TETHER = (0.150, 0.090)                          # tether staple on the base plate: x, y
SLEW_AZ, SLEW_EL, SLEW_BOX = 35.0, 40.0, 0.25    # pose of the "slewed" preview: degrees, degrees, metres


# ------------------------------------------------------------------ materials
MAT_KEYS = ["paint", "graphite", "steel", "dark", "connector", "gold", "dish"]
PAINT, GRAPHITE, STEEL, DARK, CONNECTOR, GOLD, DISH = range(7)
MAT_SPEC = {
    #             base sRGB  metal rough  wear  worn sRGB  worn metal  dirt
    "paint":     ("#B5B3AE", 0.00, 0.42, 0.60, "#8D8F92", 1.0, 0.60),     # the ship's warm putty grey
    "graphite":  ("#2B2B2C", 0.30, 0.50, 0.50, "#5E6165", 0.7, 0.45),
    "steel":     ("#B6B9BC", 1.00, 0.38, 0.00, "#B6B9BC", 1.0, 0.50),     # clear anodised aluminium
    "dark":      ("#0D0D0E", 0.00, 0.58, 0.00, "#0D0D0E", 0.0, 0.30),
    "connector": ("#73766C", 1.00, 0.44, 0.35, "#A3A5A0", 1.0, 0.50),
    "gold":      ("#C39B3B", 1.00, 0.36, 0.25, "#B6B9BC", 1.0, 0.35),     # the ship's anodised gold for EVA aids
    "dish":      ("#D9D8D1", 0.00, 0.50, 0.30, "#9A9C9E", 1.0, 0.40),     # warm white thermal paint
}
INDICATORS = (("HG_Indicator_Cyan", "#19D3FF", 2.2), ("HG_Indicator_Amber", "#FFA61A", 1.8), ("HG_Indicator_Red", "#FF2A1A", 0.25))
SOCK = {}        # material name -> bake source sockets
PROC = []        # procedural source materials


def srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def hex_rgba(text, alpha=1.0):
    text = text.lstrip("#")
    return tuple(srgb_to_linear(int(text[i:i + 2], 16) / 255.0) for i in (0, 2, 4)) + (alpha,)


def principled(mat):
    return mat.node_tree.nodes["Principled BSDF"]


def nodes_on(block):
    """Materials and worlds already use nodes in Blender 5; the old switch only warns there."""
    import warnings
    with warnings.catch_warnings():
        warnings.simplefilter("ignore")
        block.use_nodes = True


def set_input(node, names, value):
    for name in names:
        if name in node.inputs:
            node.inputs[name].default_value = value
            return True
    return False


class Graph:
    """Small helper for building shader node graphs."""

    def __init__(self, mat):
        self.nt = mat.node_tree
        self.nodes = self.nt.nodes
        self.links = self.nt.links

    def put(self, socket, value):
        if isinstance(value, bpy.types.NodeSocket):
            self.links.new(value, socket)
        else:
            socket.default_value = value

    def math(self, op, a, b=None, clamp=False):
        node = self.nodes.new("ShaderNodeMath")
        node.operation = op
        node.use_clamp = clamp
        self.put(node.inputs[0], a)
        if b is not None:
            self.put(node.inputs[1], b)
        return node.outputs[0]

    def remap(self, value, lo, hi):
        node = self.nodes.new("ShaderNodeMapRange")
        node.clamp = True
        self.put(node.inputs["Value"], value)
        node.inputs["From Min"].default_value = lo
        node.inputs["From Max"].default_value = hi
        return node.outputs["Result"]

    def mix(self, factor, a, b):
        node = self.nodes.new("ShaderNodeMix")
        node.data_type = "RGBA"
        node.blend_type = "MIX"
        ins = {s.identifier: s for s in node.inputs}
        outs = {s.identifier: s for s in node.outputs}
        self.put(ins["Factor_Float"], factor)
        self.put(ins["A_Color"], a)
        self.put(ins["B_Color"], b)
        return outs["Result_Color"]

    def noise(self, vector, scale, detail):
        node = self.nodes.new("ShaderNodeTexNoise")
        self.links.new(vector, node.inputs["Vector"])
        node.inputs["Scale"].default_value = scale
        node.inputs["Detail"].default_value = detail
        return node.outputs[0]


def make_flat_material(key):
    base_hex, metal, rough = MAT_SPEC[key][:3]
    mat = bpy.data.materials.new("HG_" + key)
    nodes_on(mat)
    node = principled(mat)
    node.inputs["Base Color"].default_value = hex_rgba(base_hex)
    node.inputs["Metallic"].default_value = metal
    node.inputs["Roughness"].default_value = rough
    return mat


def make_proc_material(key):
    """Source material with restrained procedural wear; only used as the bake source."""
    base_hex, metal, rough, wear, wear_hex, wear_metal, dirt = MAT_SPEC[key]
    mat = bpy.data.materials.new("HGsrc_" + key)
    nodes_on(mat)
    g = Graph(mat)
    bsdf = principled(mat)
    base = hex_rgba(base_hex)
    shade = tuple(c * 0.84 for c in base[:3]) + (1.0,)

    coords = g.nodes.new("ShaderNodeTexCoord").outputs["Object"]
    n_large = g.noise(coords, 6.0, 5.0)
    n_mid = g.noise(coords, 55.0, 4.0)
    n_fine = g.noise(coords, 420.0, 2.0)

    geometry = g.nodes.new("ShaderNodeNewGeometry")
    bevel = g.nodes.new("ShaderNodeBevel")
    bevel.samples = 8
    bevel.inputs["Radius"].default_value = 0.006
    dot = g.nodes.new("ShaderNodeVectorMath")
    dot.operation = "DOT_PRODUCT"
    g.links.new(bevel.outputs["Normal"], dot.inputs[0])
    g.links.new(geometry.outputs["True Normal"], dot.inputs[1])
    edge = g.math("SUBTRACT", 1.0, dot.outputs["Value"], clamp=True)
    edge_mask = g.math("MULTIPLY", g.math("MULTIPLY", g.remap(edge, 0.035, 0.18), g.remap(n_mid, 0.50, 0.66)), wear)

    occlusion = g.nodes.new("ShaderNodeAmbientOcclusion")
    occlusion.samples = 8
    occlusion.only_local = True
    occlusion.inputs["Distance"].default_value = 0.03
    dirt_mask = g.math("MULTIPLY", g.math("MULTIPLY", g.remap(occlusion.outputs["AO"], 1.0, 0.55), g.remap(n_large, 0.25, 0.75)), dirt)

    color = g.mix(g.remap(n_large, 0.30, 0.70), base, shade)
    color = g.mix(edge_mask, color, hex_rgba(wear_hex))
    color = g.mix(g.math("MULTIPLY", dirt_mask, 0.55), color, hex_rgba("#2B2622"))
    metallic = g.math("ADD", metal, g.math("MULTIPLY", edge_mask, wear_metal - metal), clamp=True)
    roughness = g.math("ADD", rough, g.math("MULTIPLY", g.math("SUBTRACT", n_fine, 0.5), 0.10))
    roughness = g.math("ADD", roughness, g.math("MULTIPLY", g.math("SUBTRACT", n_large, 0.5), 0.10))
    roughness = g.math("ADD", roughness, g.math("MULTIPLY", dirt_mask, 0.25))
    roughness = g.math("SUBTRACT", roughness, g.math("MULTIPLY", edge_mask, 0.12), clamp=True)

    contact = g.nodes.new("ShaderNodeAmbientOcclusion")
    contact.samples = 16
    contact.only_local = False
    contact.inputs["Distance"].default_value = 0.10

    g.links.new(color, bsdf.inputs["Base Color"])
    g.links.new(metallic, bsdf.inputs["Metallic"])
    g.links.new(roughness, bsdf.inputs["Roughness"])
    g.nodes.new("ShaderNodeEmission").name = "BAKE_EMIT"
    g.nodes.new("ShaderNodeTexImage").name = "BAKE_TARGET"
    SOCK[mat.name] = {"color": color, "metal": metallic, "rough": roughness, "ao": contact.outputs["AO"]}
    return mat


def make_indicator_material(name, hex_color, strength):
    mat = bpy.data.materials.new(name)
    nodes_on(mat)
    node = principled(mat)
    color = hex_rgba(hex_color)
    node.inputs["Base Color"].default_value = tuple(c * 0.30 for c in color[:3]) + (1.0,)
    node.inputs["Roughness"].default_value = 0.18
    set_input(node, ("Emission Color", "Emission"), color)
    set_input(node, ("Emission Strength",), strength)
    return mat


# ------------------------------------------------------------------ mesh helpers
RX90 = Matrix.Rotation(radians(90.0), 4, "X")     # local +Z -> world -Y (out of the panel front)


def front(x, y, z):
    """Frame at a point on a front surface; its local +Z points towards the viewer."""
    return Matrix.Translation((x, y, z)) @ RX90


def upright(x, y, z):
    return Matrix.Translation((x, y, z))


def new_bm():
    bm = bmesh.new()
    bm.edges.layers.int.new("force_smooth")
    return bm


def box(bm, x0, x1, y0, y1, z0, z1, mat, bevel=0.0, segs=1):
    matrix = Matrix.Translation(((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2)) @ Matrix.Diagonal((abs(x1 - x0), abs(y1 - y0), abs(z1 - z0), 1.0))
    verts = bmesh.ops.create_cube(bm, size=1.0, matrix=matrix)["verts"]
    for face in {f for v in verts for f in v.link_faces}:
        face.material_index = mat
    if bevel > 0.0:
        edges = list({e for v in verts for e in v.link_edges})
        # material=-1 keeps the part's own material on the new faces; the default (0) would paint them.
        bmesh.ops.bevel(bm, geom=edges, offset=bevel, offset_type="OFFSET", segments=segs, profile=0.5, affect="EDGES", clamp_overlap=True, material=-1)


def lathe(bm, matrix, profile, segs, mats, cap_mat=None, start_cap=False, smooth=True):
    """Revolve (radius, height) points around the local Z axis. Faces wind outwards for a profile
    that runs up the outside and then inwards (and down again for a recess)."""
    layer = bm.edges.layers.int["force_smooth"]
    rings = []
    for radius, height in profile:
        if radius <= 1e-9:
            rings.append([bm.verts.new(matrix @ Vector((0.0, 0.0, height)))])
        else:
            rings.append([bm.verts.new(matrix @ Vector((radius * cos(2 * pi * k / segs), radius * sin(2 * pi * k / segs), height))) for k in range(segs)])
    for i in range(len(rings) - 1):
        a, b = rings[i], rings[i + 1]
        mat = mats[i] if isinstance(mats, (list, tuple)) else mats
        for k in range(segs):
            k1 = (k + 1) % segs
            if len(a) > 1 and len(b) > 1:
                face = bm.faces.new((a[k], a[k1], b[k1], b[k]))
                if smooth:
                    bm.edges.get((a[k], b[k]))[layer] = 1
            elif len(a) > 1:
                face = bm.faces.new((a[k], a[k1], b[0]))
            elif len(b) > 1:
                face = bm.faces.new((a[0], b[k1], b[k]))
            else:
                continue
            face.material_index = mat
    first_mat = mats[0] if isinstance(mats, (list, tuple)) else mats
    if start_cap and len(rings[0]) > 1:
        bm.faces.new(tuple(reversed(rings[0]))).material_index = first_mat
    if cap_mat is not None and len(rings[-1]) > 1:
        bm.faces.new(tuple(rings[-1])).material_index = cap_mat


def finish(bm, name, materials, origin=None):
    """Tag sharp edges, write the mesh and create the object."""
    layer = bm.edges.layers.int["force_smooth"]
    bm.normal_update()
    limit = radians(40.0)
    for face in bm.faces:
        face.smooth = True
    for edge in bm.edges:
        if edge[layer]:
            edge.smooth = True
        elif len(edge.link_faces) == 2:
            edge.smooth = edge.calc_face_angle(0.0) < limit
        else:
            edge.smooth = False
    bm.edges.layers.int.remove(layer)
    mesh = bpy.data.meshes.new(NAME + "_" + name)
    bm.to_mesh(mesh)
    bm.free()
    for mat in materials:
        mesh.materials.append(mat)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    if origin is not None:
        mesh.transform(Matrix.Translation(-origin))
        obj.location = origin
    return obj


# ------------------------------------------------------------------ parts
def bolt(bm, x, z, y):
    """Socket-head frame bolt."""
    r, h = 0.0070, 0.0050
    lathe(bm, front(x, y, z), [(r, 0.0), (r, h), (r * 0.52, h), (r * 0.52, h - 0.0024)], 8, [STEEL, STEEL, DARK], cap_mat=DARK)


def small_screw(bm, x, z, y, r=0.0035):
    lathe(bm, front(x, y, z), [(r, 0.0), (r, 0.0018)], 6, [STEEL], cap_mat=STEEL, smooth=False)


RY90 = Matrix.Rotation(radians(90.0), 4, "Y")       # local +Z -> world +X
RXM90 = Matrix.Rotation(radians(-90.0), 4, "X")     # local +Z -> world +Y (into the wall)


def box_m(bm, matrix, size, mat, bevel=0.0, segs=1):
    """Box of the given size centred on an arbitrary frame."""
    verts = bmesh.ops.create_cube(bm, size=1.0, matrix=matrix @ Matrix.Diagonal((size[0], size[1], size[2], 1.0)))["verts"]
    for face in {f for v in verts for f in v.link_faces}:
        face.material_index = mat
    if bevel > 0.0:
        edges = list({e for v in verts for e in v.link_edges})
        bmesh.ops.bevel(bm, geom=edges, offset=bevel, offset_type="OFFSET", segments=segs, profile=0.5, affect="EDGES", clamp_overlap=True, material=-1)


def ring(bm, matrix, radius, z0, z1, half, segs, mat):
    """Thin guard ring with a square section, open underneath."""
    lathe(bm, matrix, [(radius + half, z0), (radius + half, z1), (radius - half, z1), (radius - half, z0)], segs, [mat, mat, mat])


RYM90 = Matrix.Rotation(radians(-90.0), 4, "Y")     # local +Z -> world -X


def tube(bm, p0, p1, radius, segs, mat, caps=True):
    """Round bar between two points."""
    p0, p1 = Vector(p0), Vector(p1)
    axis = p1 - p0
    matrix = Matrix.Translation(p0) @ axis.to_track_quat("Z", "Y").to_matrix().to_4x4()
    lathe(bm, matrix, [(radius, 0.0), (radius, axis.length)], segs, [mat], cap_mat=mat if caps else None, start_cap=caps)


def hull(bm, points, mat):
    """Convex solid through the given points."""
    verts = [bm.verts.new(p) for p in points]
    for item in bmesh.ops.convex_hull(bm, input=verts)["geom"]:
        if isinstance(item, bmesh.types.BMFace):
            item.material_index = mat


def bolt_m(bm, matrix, r=0.0105, h=0.0075):
    """Hex-head bolt on a washer, standing on the local XY plane of the given frame."""
    lathe(bm, matrix, [(r * 1.45, 0.0), (r * 1.45, 0.0016)], 12, [STEEL], cap_mat=STEEL)
    lathe(bm, matrix @ Matrix.Translation((0.0, 0.0, 0.0016)), [(r, 0.0), (r, h)], 6, [STEEL], cap_mat=STEEL, smooth=False)


def captive(bm, x, z, y):
    """Captive EVA fastener: retaining collar and a tall hex head."""
    base = front(x, y, z)
    lathe(bm, base, [(0.0175, 0.0), (0.0175, 0.0050), (0.0125, 0.0050)], 14, [STEEL, STEEL])
    lathe(bm, base @ Matrix.Translation((0.0, 0.0, 0.0050)), [(0.0110, 0.0), (0.0110, 0.0130)], 6, [CONNECTOR], cap_mat=CONNECTOR, smooth=False)


def add_dish_seams(mat):
    """Panel seams of the reflector, drawn into the bake source: twelve gores and one ring.

    The reflector's axis passes through the object origin along Y, so the x and z of the object
    coordinates give the angle around the axis and the distance from it."""
    g = Graph(mat)
    split = g.nodes.new("ShaderNodeSeparateXYZ")
    g.links.new(g.nodes.new("ShaderNodeTexCoord").outputs["Object"], split.inputs[0])
    x, z = split.outputs["X"], split.outputs["Z"]
    radius = g.math("SQRT", g.math("ADD", g.math("MULTIPLY", x, x), g.math("MULTIPLY", z, z)))
    arc = g.math("MULTIPLY", g.math("PINGPONG", g.math("ARCTAN2", z, x), pi / 12), radius)      # arc length to the nearest gore seam
    half = 0.0024
    gore = g.math("MULTIPLY", g.math("LESS_THAN", arc, half), g.math("GREATER_THAN", radius, 0.085))
    ring = g.math("LESS_THAN", g.math("ABSOLUTE", g.math("SUBTRACT", radius, 0.430)), half)
    seam = g.math("MULTIPLY", g.math("MAXIMUM", gore, ring), 0.62)
    color = g.mix(seam, SOCK[mat.name]["color"], hex_rgba("#6F6E69"))
    g.links.new(color, principled(mat).inputs["Base Color"])
    SOCK[mat.name]["color"] = color


def dish_frame():
    """Frame of the reflector: origin at the vertex, local +Z along the boresight (towards the viewer)."""
    return front(0.0, VERTEX_Y, EL_Z)


def sag(r):
    """Depth of the paraboloid at radius r."""
    return r * r / (4.0 * DISH_F)


def build_base():
    """Fixed part: base plate, pedestal, boom, azimuth drive housing, box bracket, status plate and EVA aids."""
    bm = new_bm()
    half = PLATE / 2
    box(bm, -half, half, -half, half, 0.0, PLATE_T, GRAPHITE, bevel=0.003)
    for x in (-0.175, 0.0, 0.175):
        for y in (-0.175, 0.0, 0.175):
            if x or y:
                bolt_m(bm, upright(x, y, PLATE_T))
    top = PED_Z - PLATE_T
    lathe(bm, upright(0.0, 0.0, PLATE_T), [(PED_R0, 0.0), (PED_R0, 0.012), (PED_R1, top - 0.015), (PED_R1, top), (BOOM_R, top)], 28, [STEEL] * 4)
    lathe(bm, upright(0.0, 0.0, PED_Z), [(BOOM_R, 0.0), (BOOM_R, BOOM_Z1 - PED_Z)], 24, [PAINT])
    lathe(bm, upright(0.0, 0.0, BOOM_Z1), [(BOOM_R, 0.0), (0.106, 0.0), (0.106, 0.014), (AZ_R, 0.014), (AZ_R, AZ_Z1 - BOOM_Z1)], 28,
          [STEEL, STEEL, STEEL, GRAPHITE], cap_mat=GRAPHITE)
    box(bm, -0.050, 0.050, AZ_R - 0.012, AZ_R + 0.045, BOOM_Z1 + 0.022, AZ_Z1 - 0.012, GRAPHITE, bevel=0.004)      # drive electronics
    cable_y = BOOM_R + 0.013
    tube(bm, (0.0, cable_y, PED_Z + 0.010), (0.0, cable_y, BOOM_Z1 + 0.030), 0.011, 8, DARK)                       # harness up the boom
    for z in (0.260, 0.380, 0.620):
        box(bm, -0.020, 0.020, BOOM_R - 0.002, BOOM_R + 0.028, z - 0.009, z + 0.009, STEEL)

    box(bm, -BRACKET_X, BRACKET_X, PLATE_Y0, PLATE_Y1, BRACKET_Z0, BRACKET_Z1, PAINT, bevel=0.003)                 # bracket back plate
    for z in CLAMP_Z:
        box(bm, -0.075, 0.075, PLATE_Y1 - 0.002, 0.046, z - 0.030, z + 0.030, STEEL, bevel=0.004)                  # boom clamp
    for x in (-0.185, 0.185):
        bolt(bm, x, BRACKET_Z1 - 0.014, PLATE_Y0)
    box(bm, -0.190, 0.190, BOX_Y1 - 0.006, PLATE_Y0 + 0.002, SHELF_Z - 0.015, SHELF_Z, PAINT, bevel=0.002)         # shelf
    for side in (-1, 1):
        xa, xb = sorted((side * 0.164, side * 0.176))
        box(bm, xa, xb, BOX_Y1 + 0.010, PLATE_Y0, SHELF_Z, SHELF_Z + 0.010, STEEL)                                 # guide rail
        tube(bm, (side * 0.150, BOX_Y1 + 0.020, SHELF_Z - 0.013), (side * 0.150, -half + 0.020, PLATE_T), 0.008, 8, STEEL, caps=False)   # shelf strut

    x0, x1, z0, z1 = SUB                                                                                           # status sub-panel
    box(bm, x0, x1, Y_SUB, PLATE_Y0, z0, z1, GRAPHITE, bevel=0.0015)
    for x in (x0 + 0.009, x1 - 0.009):
        for z in (z0 + 0.009, z1 - 0.009):
            small_screw(bm, x, z, Y_SUB)
    for x in LAMP_X:
        lathe(bm, front(x, Y_SUB, LAMP_Z), [(0.0150, 0.0), (0.0150, 0.0045), (0.0106, 0.0045), (0.0106, 0.0010)], 12, [STEEL, STEEL, DARK], cap_mat=DARK)

    for z in CLAMP_Z:                                                                                              # EVA handrail
        tube(bm, (-0.073, 0.0, z), (RAIL_X, 0.0, z), 0.017, 10, GOLD, caps=False)
    tube(bm, (RAIL_X, 0.0, CLAMP_Z[0] - 0.017), (RAIL_X, 0.0, CLAMP_Z[1] + 0.017), 0.017, 10, GOLD)

    tx, ty = TETHER                                                                                                # tether staple
    box(bm, tx - 0.036, tx + 0.036, ty - 0.014, ty + 0.014, PLATE_T, PLATE_T + 0.004, STEEL)
    for x in (tx - 0.022, tx + 0.022):
        tube(bm, (x, ty, PLATE_T + 0.004), (x, ty, PLATE_T + 0.048), 0.0065, 8, STEEL, caps=False)
    tube(bm, (tx - 0.0285, ty, PLATE_T + 0.048), (tx + 0.0285, ty, PLATE_T + 0.048), 0.0065, 8, STEEL)
    return bm


def build_azimuth():
    """Azimuth stage: turntable, fork and the elevation drive and encoder housings."""
    bm = new_bm()
    lathe(bm, upright(0.0, 0.0, AZ_Z1), [(TURN_R, 0.0), (TURN_R, TURN_Z1 - AZ_Z1)], 28, [STEEL], cap_mat=STEEL, start_cap=True)
    box(bm, -FORK_X, FORK_X, -FORK_Y, FORK_Y, TURN_Z1, FORK_Z1, PAINT, bevel=0.006, segs=2)
    for side in (-1, 1):
        xa, xb = sorted((side * ARM_IN, side * (ARM_IN + ARM_T)))
        box(bm, xa, xb, -ARM_D / 2, ARM_D / 2, FORK_Z1 - 0.004, EL_Z, PAINT, bevel=0.004)
        lathe(bm, Matrix.Translation((xa, 0.0, EL_Z)) @ RY90, [(BOSS_R, 0.0), (BOSS_R, ARM_T)], 24, [PAINT], cap_mat=PAINT, start_cap=True)
    lathe(bm, Matrix.Translation((ARM_IN + ARM_T, 0.0, EL_Z)) @ RY90, [(0.070, 0.0), (0.070, 0.012), (MOTOR_R, 0.012), (MOTOR_R, MOTOR_L)], 20,
          [STEEL, STEEL, GRAPHITE], cap_mat=GRAPHITE)                                                              # drive
    lathe(bm, Matrix.Translation((-ARM_IN - ARM_T, 0.0, EL_Z)) @ RYM90, [(0.070, 0.0), (0.070, 0.012), (0.048, 0.012), (0.048, 0.045)], 20,
          [STEEL, STEEL, GRAPHITE], cap_mat=GRAPHITE)                                                              # encoder
    for x in (-0.185, 0.185):
        for z in (TURN_Z1 + 0.020, FORK_Z1 - 0.020):
            bolt(bm, x, z, -FORK_Y)
    return bm


def build_elevation():
    """Elevation stage: trunnion, hub, reflector with its back structure, tripod and feed horn."""
    bm = new_bm()
    d = dish_frame()
    rings = 8
    lip = DISH_R - 0.012
    edge = sag(DISH_R)
    back = [(0.0, -DISH_T)] + [(DISH_R * i / rings, sag(DISH_R * i / rings) - DISH_T) for i in range(1, rings + 1)]
    rim = [(RIM_R, edge - DISH_T), (RIM_R, edge + 0.008), (lip, edge + 0.008)]
    inner = [(lip * i / rings, sag(lip * i / rings)) for i in range(rings, 0, -1)] + [(0.0, 0.0)]
    profile = back + rim + inner
    lathe(bm, d, profile, 64, [DISH] * (len(profile) - 1))                                                         # reflector shell
    lathe(bm, d, [(0.075, sag(0.075) - 0.001), (0.075, 0.0065), (0.0, 0.0065)], 24, [STEEL, STEEL])                # vertex plate

    def rib_depth(r):
        return 0.070 - 0.052 * (r - 0.130) / 0.620

    for k in range(8):                                                                                             # radial ribs, tapering outwards
        spin = d @ Matrix.Rotation(radians(45.0 * k + 22.5), 4, "Z")
        stations = (0.130, 0.330, 0.540, 0.750)
        for r0, r1 in zip(stations[:-1], stations[1:]):
            corners = []
            for r in (r0, r1):
                top = sag(r) - DISH_T + 0.004
                corners += [spin @ Vector((r, side, z)) for side in (-0.006, 0.006) for z in (top, top - rib_depth(r))]
            hull(bm, corners, STEEL)
    r_ring = 0.470                                                                                                 # stiffening ring
    zt = sag(r_ring) - DISH_T + 0.004
    lathe(bm, d, [(r_ring - 0.010, zt), (r_ring - 0.010, zt - 0.050), (r_ring + 0.010, zt - 0.050), (r_ring + 0.010, zt)], 48, [STEEL] * 3)

    lathe(bm, d, [(0.086, -0.150), (0.086, -0.034), (0.150, -0.034), (0.150, -DISH_T + 0.003)], 28, [STEEL] * 3, start_cap=True)    # hub
    box(bm, -0.100, 0.100, -0.075, 0.055, EL_Z - 0.090, EL_Z + 0.090, PAINT, bevel=0.006)                          # yoke block
    lathe(bm, Matrix.Translation((-ARM_IN, 0.0, EL_Z)) @ RY90, [(0.052, 0.0), (0.052, 2 * ARM_IN)], 20, [STEEL])   # trunnion

    feed = d @ Matrix.Translation((0.0, 0.0, DISH_F))
    for k in range(3):                                                                                             # tripod
        a = radians(90.0 + 120.0 * k)
        foot = d @ Vector(((DISH_R - 0.001) * cos(a), (DISH_R - 0.001) * sin(a), edge + 0.006))
        head = feed @ Vector((0.040 * cos(a), 0.040 * sin(a), 0.075))
        tube(bm, foot, head, 0.0085, 8, STEEL, caps=False)
    lathe(bm, feed, [(0.050, -0.040), (0.062, -0.040), (0.026, 0.045), (0.044, 0.045), (0.044, 0.115)], 20,
          [DARK, STEEL, GRAPHITE, GRAPHITE], cap_mat=GRAPHITE, start_cap=True)                                     # feed horn
    return bm


def box_origin():
    """Pivot of the electronics box: centre of its back edge at shelf level. It slides out along the model's front."""
    return Vector((0.0, BOX_Y0, BOX_Z0))


def build_box():
    """Replaceable electronics unit: two EVA handles, four captive bolts, a test port, connectors and cooling fins."""
    bm = new_bm()
    box(bm, -BOX_X, BOX_X, BOX_Y1, BOX_Y0, BOX_Z0, BOX_Z1, PAINT, bevel=0.006, segs=2)
    for side in (-1, 1):
        x = side * 0.092
        for z in (0.178, 0.312):
            lathe(bm, front(x, BOX_Y1, z), [(0.0080, 0.0), (0.0080, 0.046)], 8, [GOLD])
        tube(bm, (x, BOX_Y1 - 0.046, 0.166), (x, BOX_Y1 - 0.046, 0.324), 0.0095, 10, GOLD)
        for z in (BOX_Z0 + 0.024, BOX_Z1 - 0.024):
            captive(bm, side * 0.136, z, BOX_Y1)
        for i in range(5):                                                                                         # cooling fins
            z = 0.170 + i * 0.040
            xa, xb = sorted((side * BOX_X, side * (BOX_X + 0.011)))
            box(bm, xa, xb, BOX_Y1 + 0.022, BOX_Y0 - 0.022, z - 0.0025, z + 0.0025, PAINT)
    lathe(bm, front(0.0, BOX_Y1, 0.232), [(0.0190, 0.0), (0.0190, 0.0060), (0.0150, 0.0060), (0.0150, 0.0120)], 14, [CONNECTOR] * 3, cap_mat=DARK)   # test port
    for x in (-0.070, 0.070):                                                                                      # connectors on top
        lathe(bm, upright(x, (BOX_Y0 + BOX_Y1) / 2 + 0.040, BOX_Z1), [(0.0200, 0.0), (0.0200, 0.0130), (0.0160, 0.0130), (0.0160, 0.0220)], 14,
              [CONNECTOR] * 3, cap_mat=DARK)
    return bm


def build_indicators():
    bm = new_bm()
    for index, x in enumerate(LAMP_X):
        lathe(bm, front(x, Y_SUB - 0.0010, LAMP_Z), [(0.0099, 0.0), (0.0099, 0.0030), (0.0074, 0.0064), (0.0, 0.0078)], 12, [index] * 3)
    return bm


# ------------------------------------------------------------------ labels
PPM = 4096.0                    # atlas pixels per metre
ATLAS_W, ATLAS_H = 2048, 2048
STYLES = {
    "dark": dict(bg="#16181B", fg="#E6E3DA", line="#8A8D90"),
    "light": dict(bg="#D8D6CC", fg="#101113", line="#6A6D70"),
    "caution": dict(bg="#F0A81C", fg="#111214", line="#111214"),
    "hazard": dict(bg="#111214", fg="#F0A81C", line=None),
    "dial": dict(bg="#E9E7DF", fg="#101113", line=None),
}




def pack_atlas(items, pad=8):
    """Guillotine packing in pixels, origin bottom-left."""
    free = [(0, 0, ATLAS_W, ATLAS_H)]
    for item in sorted(items, key=lambda p: (-p["s"][1], -p["s"][0])):
        w = int(round(item["s"][0] * PPM)) + 2 * pad
        h = int(round(item["s"][1] * PPM)) + 2 * pad
        fits = [r for r in free if r[2] >= w and r[3] >= h]
        if not fits:
            raise RuntimeError("Label atlas is full: " + item["key"])
        fx, fy, fw, fh = min(fits, key=lambda r: (r[3] - h, r[2] - w))
        free.remove((fx, fy, fw, fh))
        item["px"] = (fx + pad, fy + pad, w - 2 * pad, h - 2 * pad)
        if fw - w > 0:
            free.append((fx + w, fy, fw - w, h))
        if fh - h > 0:
            free.append((fx, fy + h, fw, fh - h))


def emission_material(name, hex_color):
    mat = bpy.data.materials.new(name)
    nodes_on(mat)
    tree = mat.node_tree
    tree.nodes.clear()
    emission = tree.nodes.new("ShaderNodeEmission")
    emission.inputs["Color"].default_value = hex_rgba(hex_color)
    output = tree.nodes.new("ShaderNodeOutputMaterial")
    tree.links.new(emission.outputs[0], output.inputs["Surface"])
    return mat


LABEL_FONT = {"name": "Blender built-in (Bfont)"}


def load_label_font():
    """Inter ships with Blender under the SIL Open Font License 1.1; its digit 1 is unambiguous."""
    folder = os.path.join(bpy.utils.system_resource("DATAFILES"), "fonts")
    for filename, label in (("Inter.woff2", "Inter (SIL OFL 1.1, bundled with Blender)"),
                            ("DejaVuSansMono.woff2", "DejaVu Sans Mono (Bitstream Vera license, bundled with Blender)")):
        path = os.path.join(folder, filename)
        if os.path.exists(path):
            try:
                font = bpy.data.fonts.load(path)
                LABEL_FONT["name"] = label
                return font
            except RuntimeError as error:
                log("font", filename, "not usable:", error)
    return None


def render_label_atlas(items, path):
    """Draws every placard (flat colours + text) and renders it to one PNG."""
    scene = bpy.data.scenes.new("HG_LabelAtlas")
    made = []
    colors = {}
    font = load_label_font()

    def color_mat(hex_color):
        if hex_color not in colors:
            colors[hex_color] = emission_material("HGatlas_" + hex_color.lstrip("#"), hex_color)
            made.append(colors[hex_color])
        return colors[hex_color]

    quads = {}

    def quad(hex_color, corners, z):
        quads.setdefault(hex_color, []).append([(x, y, z) for x, y in corners])

    def rect(hex_color, x0, y0, x1, y1, z):
        quad(hex_color, [(x0, y0), (x1, y0), (x1, y1), (x0, y1)], z)

    px = 1.0 / PPM
    for item in items:
        style = STYLES[item["style"]]
        x, y, w, h = (v * px for v in item["px"])
        bleed = 3 * px
        rect(style["bg"], x - bleed, y - bleed, x + w + bleed, y + h + bleed, 0.0)
        if style["line"]:
            inset = {"caution": 0.0030, "light": 0.0007}.get(item["style"], 0.0012)
            thick = 0.0012 if item["style"] == "caution" else 0.0004
            x0, y0, x1, y1 = x + inset, y + inset, x + w - inset, y + h - inset
            rect(style["line"], x0, y0, x1, y0 + thick, 0.001)
            rect(style["line"], x0, y1 - thick, x1, y1, 0.001)
            rect(style["line"], x0, y0, x0 + thick, y1, 0.001)
            rect(style["line"], x1 - thick, y0, x1, y1, 0.001)
        if item["style"] == "hazard":
            stripe, zone = h * 0.55, w * 0.21
            for zone_x0 in (x, x + w - zone):
                start = zone_x0
                while start + stripe + h <= zone_x0 + zone + 1e-9:
                    quad(style["fg"], [(start, y), (start + stripe, y), (start + stripe + h, y + h), (start + h, y + h)], 0.001)
                    start += 2 * stripe
        for hex_color, corners in item.get("shapes", ()):          # extra artwork, e.g. dial ticks
            quad(hex_color, [(x + w / 2 + sx, y + h / 2 + sy) for sx, sy in corners], 0.0015)
        for text, dx, dz, size in item["texts"]:
            # Bolder strokes come from overprinting shifted copies. Blender's curve offset is not used:
            # it self-intersects at the sharp apex of "A" and the crossbar disappears.
            # Glyphs are built 100x larger and scaled down so the fill keeps thin parts at millimetre sizes.
            curve = bpy.data.curves.new("HGatlas_text", "FONT")
            curve.body = text
            if font is not None:
                curve.font = font
            curve.size = size * 100.0
            curve.align_x = "CENTER"
            curve.align_y = "CENTER"
            curve.materials.append(color_mat(style["fg"]))
            made.append(curve)
            shift = size * TEXT_BOLD
            for ox, oy in ((0, 0), (1, 0), (-1, 0), (0, 1), (0, -1), (0.7, 0.7), (-0.7, 0.7), (0.7, -0.7), (-0.7, -0.7)):
                obj = bpy.data.objects.new("HGatlas_text", curve)
                obj.scale = (0.01, 0.01, 0.01)
                obj.location = (x + w / 2 + dx + ox * shift, y + h / 2 + dz - size * TEXT_DROP + oy * shift, 0.002)
                scene.collection.objects.link(obj)
                made.append(obj)

    for hex_color, faces in quads.items():
        bm = bmesh.new()
        for corners in faces:
            bm.faces.new([bm.verts.new(c) for c in corners])
        mesh = bpy.data.meshes.new("HGatlas_fill")
        bm.to_mesh(mesh)
        bm.free()
        mesh.materials.append(color_mat(hex_color))
        obj = bpy.data.objects.new("HGatlas_fill", mesh)
        scene.collection.objects.link(obj)
        made.extend((obj, mesh))

    camera_data = bpy.data.cameras.new("HGatlas_cam")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = max(ATLAS_W, ATLAS_H) * px
    camera = bpy.data.objects.new("HGatlas_cam", camera_data)
    camera.location = (ATLAS_W * px / 2, ATLAS_H * px / 2, 1.0)
    scene.collection.objects.link(camera)
    scene.camera = camera
    made.extend((camera, camera_data))

    world = bpy.data.worlds.new("HGatlas_world")
    nodes_on(world)
    world.node_tree.nodes["Background"].inputs["Color"].default_value = hex_rgba("#3A3C3E")
    scene.world = world
    made.append(world)

    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 24
    scene.cycles.use_denoising = False
    scene.render.resolution_x, scene.render.resolution_y, scene.render.resolution_percentage = ATLAS_W, ATLAS_H, 100
    scene.view_settings.view_transform = "Standard"
    try:
        scene.view_settings.look = "None"
    except TypeError:
        pass
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGB"
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True, scene=scene.name)

    bpy.data.scenes.remove(scene)
    for block in made:
        try:
            {bpy.types.Object: bpy.data.objects, bpy.types.Mesh: bpy.data.meshes, bpy.types.TextCurve: bpy.data.curves,
             bpy.types.Material: bpy.data.materials, bpy.types.Camera: bpy.data.cameras, bpy.types.World: bpy.data.worlds}[type(block)].remove(block)
        except (KeyError, ReferenceError, RuntimeError):
            pass
    if font is not None:
        bpy.data.fonts.remove(font)


TEXT_DROP = 0.0     # vertical correction for the font's visual centre, as a fraction of the size
TEXT_BOLD = 0.019   # stroke growth (overprint shift) as a fraction of the size


def placards():
    items = []

    def add(key, host, cx, cz, w, h, y, style, texts, flip=False, shapes=(), top=False):
        items.append(dict(key=key, host=host, c=(cx, cz), s=(w, h), y=y, style=style, texts=texts, flip=flip, shapes=shapes, top=top))

    add("title", "Azimuth", 0.0, (TURN_Z1 + FORK_Z1) / 2, 0.30, 0.026, -FORK_Y, "dark", [("HGA-1   HIGH GAIN ANTENNA", 0.0, 0.0, 0.0125)])
    x0, x1, z0, z1 = SUB
    add("hdr_status", "Base", (x0 + x1) / 2, z1 - 0.013, 0.14, 0.015, Y_SUB, "dark", [("HGA-1 STATUS", 0.0, 0.0, 0.0085)])
    for x, text in zip(LAMP_X, ("PWR", "NO LINK", "FAULT")):
        add("lamp_" + text.replace(" ", "_"), "Base", x, z0 + 0.027, 0.050, 0.018, Y_SUB, "light", [(text, 0.0, 0.0, 0.0086)])
    add("caution", "Base", 0.100, 0.467, 0.16, 0.085, PLATE_Y0, "caution",
        [("CAUTION", 0.0, 0.022, 0.021), ("RF RADIATION", 0.0, -0.003, 0.0115), ("KEEP CLEAR OF DISH", 0.0, -0.024, 0.0078)])
    add("tether", "Base", TETHER[0], TETHER[1] - 0.045, 0.085, 0.016, PLATE_T, "light", [("TETHER POINT", 0.0, 0.0, 0.0085)], top=True)
    add("box_id", "ElectronicsBox", 0.0, 0.292, 0.13, 0.070, BOX_Y1, "light",
        [("EU-1", 0.0, 0.020, 0.016), ("HGA ELECTRONICS UNIT", 0.0, 0.002, 0.0075),
         ("S/N A7-C2-011     MASS 9.4 kg", 0.0, -0.012, 0.0062), ("ORU   EVA REPLACEABLE", 0.0, -0.025, 0.0062)])
    add("box_release", "ElectronicsBox", 0.0, 0.172, 0.15, 0.016, BOX_Y1, "hazard", [("4 CAPTIVE BOLTS", 0.0, 0.0, 0.0080)])
    return items


def build_label_objects(items, material):
    """One label mesh per host object; each is joined into its host afterwards.

    A placard stands in the XZ plane and faces the front. With top=True it lies on a horizontal surface and
    reads from the front; its second centre coordinate is then y and its plane value is the height."""
    result = {}
    for host in sorted({p["host"] for p in items}):
        bm = bmesh.new()
        uv = bm.loops.layers.uv.new("UVMap")
        for item in (p for p in items if p["host"] == host):
            (cx, cz), (w, h) = item["c"], item["s"]
            x, v, pw, ph = item["px"]
            u0, v0, u1, v1 = x / ATLAS_W, v / ATLAS_H, (x + pw) / ATLAS_W, (v + ph) / ATLAS_H
            if item.get("top"):
                z = item["y"] + 0.0004
                corners = [((cx - w / 2, cz - h / 2, z), u0, v0), ((cx + w / 2, cz - h / 2, z), u1, v0),
                           ((cx + w / 2, cz + h / 2, z), u1, v1), ((cx - w / 2, cz + h / 2, z), u0, v1)]
            elif item["flip"]:
                y = item["y"] + 0.0004
                corners = [((cx + w / 2, y, cz - h / 2), u0, v0), ((cx - w / 2, y, cz - h / 2), u1, v0),
                           ((cx - w / 2, y, cz + h / 2), u1, v1), ((cx + w / 2, y, cz + h / 2), u0, v1)]
            else:
                y = item["y"] - 0.0004
                corners = [((cx - w / 2, y, cz - h / 2), u0, v0), ((cx + w / 2, y, cz - h / 2), u1, v0),
                           ((cx + w / 2, y, cz + h / 2), u1, v1), ((cx - w / 2, y, cz + h / 2), u0, v1)]
            face = bm.faces.new([bm.verts.new(position) for position, _u, _v in corners])
            for loop, (_position, u, vv) in zip(face.loops, corners):
                loop[uv].uv = (u, vv)
        mesh = bpy.data.meshes.new(NAME + "_Labels" + host)
        bm.to_mesh(mesh)
        bm.free()
        mesh.materials.append(material)
        obj = bpy.data.objects.new("Labels" + host, mesh)
        bpy.context.scene.collection.objects.link(obj)
        result[host] = obj
    return result


# ------------------------------------------------------------------ scene utilities
def select_only(objs, active=None):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objs:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = active or objs[0]


def tri_count(obj):
    evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    mesh = evaluated.to_mesh()
    mesh.calc_loop_triangles()
    count = len(mesh.loop_triangles)
    evaluated.to_mesh_clear()
    return count


def world_bounds(objs):
    lo, hi = Vector((1e9, 1e9, 1e9)), Vector((-1e9, -1e9, -1e9))
    depsgraph = bpy.context.evaluated_depsgraph_get()
    for obj in objs:
        evaluated = obj.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        for vertex in mesh.vertices:
            p = obj.matrix_world @ vertex.co
            lo = Vector((min(lo.x, p.x), min(lo.y, p.y), min(lo.z, p.z)))
            hi = Vector((max(hi.x, p.x), max(hi.y, p.y), max(hi.z, p.z)))
        evaluated.to_mesh_clear()
    return lo, hi


def enable_gpu(scene):
    try:
        prefs = bpy.context.preferences.addons["cycles"].preferences
        for kind in ("OPTIX", "CUDA"):
            try:
                prefs.compute_device_type = kind
            except TypeError:
                continue
            if hasattr(prefs, "refresh_devices"):
                prefs.refresh_devices()
            else:
                prefs.get_devices()
            names = [d.name for d in prefs.devices if d.type == kind]
            if names:
                for device in prefs.devices:
                    device.use = device.type == kind
                scene.cycles.device = "GPU"
                return kind + ": " + ", ".join(names)
    except Exception as error:                                   # noqa: BLE001 - fall back to CPU
        log("GPU setup failed:", error)
    scene.cycles.device = "CPU"
    return "CPU"


def unwrap(objs):
    for obj in objs:
        if not obj.data.uv_layers:
            obj.data.uv_layers.new(name="UVMap")
    select_only(objs)
    bpy.context.scene.tool_settings.use_uv_select_sync = True
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=radians(66.0), island_margin=0.0)
    try:
        bpy.ops.uv.average_islands_scale()
    except Exception as error:                                   # noqa: BLE001
        log("UV average scale skipped:", error)
    bpy.ops.object.mode_set(mode="OBJECT")
    for obj in objs:
        shrink_hidden_faces(obj)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    try:
        bpy.ops.uv.pack_islands(rotate=True, margin_method="FRACTION", margin=0.003)
    except TypeError as error:
        log("UV pack fell back:", error)
        bpy.ops.uv.pack_islands(rotate=True, margin=0.003)
    bpy.ops.object.mode_set(mode="OBJECT")


def shrink_hidden_faces(obj):
    """The underside of the base plate sits on the hull pad and gets a fraction of the texel density."""
    mesh = obj.data
    uv = mesh.uv_layers.active.data
    for polygon in mesh.polygons:
        if polygon.area < 0.05 or polygon.normal.z > -0.99 or polygon.center.z + obj.location.z > 0.001:
            continue
        loops = list(polygon.loop_indices)
        cu = sum(uv[i].uv.x for i in loops) / len(loops)
        cv = sum(uv[i].uv.y for i in loops) / len(loops)
        for i in loops:
            uv[i].uv = ((uv[i].uv.x - cu) * 0.20 + cu, (uv[i].uv.y - cv) * 0.20 + cv)


def new_image(name, non_color):
    image = bpy.data.images.new(name, TEX, TEX, alpha=False)
    if non_color:
        image.colorspace_settings.name = "Non-Color"
    return image


def bake_pass(key, image, groups, samples):
    scene = bpy.context.scene
    scene.cycles.samples = samples
    for mat in PROC:
        tree = mat.node_tree
        emit, output, target = tree.nodes["BAKE_EMIT"], tree.nodes["Material Output"], tree.nodes["BAKE_TARGET"]
        for link in list(emit.inputs["Color"].links):
            tree.links.remove(link)
        tree.links.new(SOCK[mat.name][key], emit.inputs["Color"])
        tree.links.new(emit.outputs[0], output.inputs["Surface"])
        target.image = image
        for node in tree.nodes:
            node.select = False
        target.select = True
        tree.nodes.active = target
    clear = True
    for obj, hidden in groups:
        for other in hidden:
            other.hide_render = True
        select_only([obj])
        bpy.ops.object.bake(type="EMIT", margin=8, use_clear=clear)
        for other in hidden:
            other.hide_render = False
        clear = False
    log("baked", key)


def save_image(image, filename):
    path = os.path.join(TEX_DIR, filename)
    image.filepath_raw = path
    image.file_format = "PNG"
    image.save()
    return path


def load_image(path, non_color):
    image = bpy.data.images.load(path)
    if non_color:
        image.colorspace_settings.name = "Non-Color"
    return image


def weighted_normals(obj):
    modifier = obj.modifiers.new("WeightedNormal", "WEIGHTED_NORMAL")
    modifier.keep_sharp = True
    modifier.weight = 50
    return modifier


def compact_materials(obj):
    """Drop unused slots. materials.clear() resets every face to slot 0, so indices are rewritten after it."""
    mesh = obj.data
    indices = [p.material_index for p in mesh.polygons]
    used = sorted(set(indices))
    keep = [mesh.materials[i] for i in used]
    remap = {old: new for new, old in enumerate(used)}
    mesh.materials.clear()
    for mat in keep:
        mesh.materials.append(mat)
    mesh.polygons.foreach_set("material_index", [remap[i] for i in indices])
    mesh.update()


# ------------------------------------------------------------------ render set
def build_render_set(scene):
    """Lights, cameras and two stand-ins that are not part of the model: a slab the size of the hull pad and a floor."""
    collection = bpy.data.collections.new("HG_RenderSet")
    scene.collection.children.link(collection)

    def slab(name, size, top, hex_color, metallic, roughness):
        mesh = bpy.data.meshes.new(name)
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0, matrix=Matrix.Translation((0.0, 0.0, top - size[2] / 2)) @ Matrix.Diagonal((size[0], size[1], size[2], 1.0)))
        bm.to_mesh(mesh)
        bm.free()
        mat = bpy.data.materials.new(name)
        nodes_on(mat)
        principled(mat).inputs["Base Color"].default_value = hex_rgba(hex_color)
        principled(mat).inputs["Roughness"].default_value = roughness
        principled(mat).inputs["Metallic"].default_value = metallic
        mesh.materials.append(mat)
        collection.objects.link(bpy.data.objects.new(name, mesh))

    slab("HG_RenderPad", (0.90, 1.10, 0.06), -0.0005, "#9A9DA0", 0.9, 0.45)
    slab("HG_RenderFloor", (16.0, 16.0, 0.02), -0.0605, "#2A2D31", 0.2, 0.62)

    def area(name, location, target, power, size, color):
        data = bpy.data.lights.new(name, "AREA")
        data.energy, data.size, data.color = power, size, color
        obj = bpy.data.objects.new(name, data)
        obj.location = location
        look_at(obj, target)
        collection.objects.link(obj)

    area("HG_Key", (-3.6, -5.2, 4.6), (0.0, -0.2, 1.15), 1150.0, 3.2, (0.94, 0.97, 1.0))
    area("HG_Fill", (4.2, -3.6, 1.3), (0.0, -0.2, 1.0), 300.0, 4.5, (1.0, 0.95, 0.88))
    area("HG_Rim", (0.8, 3.2, 4.4), (0.0, 0.0, 1.3), 520.0, 2.4, (1.0, 1.0, 1.0))

    cameras = {}

    def camera(key, location, target, lens=None, ortho=None):
        data = bpy.data.cameras.new("HG_Cam_" + key)
        if ortho is not None:
            data.type = "ORTHO"
            data.ortho_scale = ortho
        else:
            data.lens = lens
        obj = bpy.data.objects.new("HG_Cam_" + key, data)
        obj.location = location
        look_at(obj, target)
        collection.objects.link(obj)
        cameras[key] = obj

    camera("hero", (-4.3, -5.0, 1.55), (0.0, -0.20, 1.12), lens=70.0)
    camera("back", (2.9, 5.4, 2.0), (0.0, -0.10, 1.15), lens=62.0)
    camera("detail", (-0.95, -1.55, 0.62), (-0.01, -0.15, 0.33), lens=45.0)
    camera("side", (3.3, -5.4, 1.9), (0.0, -0.15, 1.15), lens=62.0)

    world = bpy.data.worlds.new("HG_World")
    nodes_on(world)
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.020, 0.023, 0.028, 1.0)
    scene.world = world
    scene.camera = cameras["hero"]
    for look in ("AgX - Medium High Contrast", "Medium High Contrast"):
        try:
            scene.view_settings.look = look
            break
        except TypeError:
            continue
    return cameras


def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def render(scene, camera, filename, size, samples):
    scene.camera = camera
    scene.cycles.samples = samples
    scene.cycles.use_denoising = True
    scene.render.resolution_x, scene.render.resolution_y = size
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGB"
    scene.render.filepath = os.path.join(RENDER_DIR, filename)
    bpy.ops.render.render(write_still=True)
    log("rendered", filename)


def wire_material():
    mat = bpy.data.materials.new("HG_WireOverlay")
    nodes_on(mat)
    g = Graph(mat)
    wire = g.nodes.new("ShaderNodeWireframe")
    wire.use_pixel_size = True
    wire.inputs["Size"].default_value = 1.3
    color = g.mix(wire.outputs[0], (0.62, 0.63, 0.64, 1.0), (0.02, 0.05, 0.10, 1.0))
    g.links.new(color, principled(mat).inputs["Base Color"])
    principled(mat).inputs["Roughness"].default_value = 0.7
    return mat


# ------------------------------------------------------------------ main
def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = "CYCLES"
    device = enable_gpu(scene)
    log("Blender", bpy.app.version_string, "| stage", STAGE, "| device", device)

    if STAGE == "atlas":
        items = placards()
        pack_atlas(items)
        render_label_atlas(items, os.path.join(TEX_DIR, NAME + "_Labels.png"))
        log("label atlas rendered")
        return
    full = STAGE == "full"
    surface = [make_proc_material(k) if full else make_flat_material(k) for k in MAT_KEYS]
    if full:
        PROC.extend(surface)
        add_dish_seams(surface[DISH])
    lamps = [make_indicator_material(*spec) for spec in INDICATORS]

    base = finish(build_base(), "Base", surface)
    azimuth = finish(build_azimuth(), "Azimuth", surface, origin=Vector((0.0, 0.0, AZ_Z1)))
    elevation = finish(build_elevation(), "Elevation", surface, origin=Vector((0.0, 0.0, EL_Z)))
    unit = finish(build_box(), "ElectronicsBox", surface, origin=box_origin())
    indicators = finish(build_indicators(), "StatusIndicators", lamps)
    indicators.data.uv_layers.new(name="UVMap")      # every exported mesh carries UV0 (and so tangents) in Unity
    baked = [base, azimuth, elevation, unit]
    parts = baked + [indicators]
    hosts = {"Base": base, "Azimuth": azimuth, "ElectronicsBox": unit}
    report = {"blender": bpy.app.version_string, "stage": STAGE, "cycles_device": device}

    if full:
        items = placards()
        pack_atlas(items)
        labels_path = os.path.join(TEX_DIR, NAME + "_Labels.png")
        render_label_atlas(items, labels_path)
        log("label atlas rendered")

        unwrap(baked)
        color = new_image("HGbake_color", False)
        metal = new_image("HGbake_metal", True)
        rough = new_image("HGbake_rough", True)
        occlusion = new_image("HGbake_ao", True)
        together = [(obj, []) for obj in baked]
        bake_pass("color", color, together, 48)
        bake_pass("metal", metal, together, 48)
        bake_pass("rough", rough, together, 48)
        # Occlusion: each part on its own, because every part moves against the others.
        bake_pass("ao", occlusion, [(obj, [other for other in parts if other is not obj]) for obj in baked], 96)

        import numpy as np
        count = TEX * TEX * 4
        m = np.empty(count, dtype=np.float32)
        r = np.empty(count, dtype=np.float32)
        metal.pixels.foreach_get(m)
        rough.pixels.foreach_get(r)
        packed = np.empty(count, dtype=np.float32)
        packed[0::4] = m[0::4]
        packed[1::4] = m[0::4]
        packed[2::4] = m[0::4]
        packed[3::4] = 1.0 - r[0::4]
        metal_smooth = bpy.data.images.new("HGbake_ms", TEX, TEX, alpha=True)
        metal_smooth.colorspace_settings.name = "Non-Color"
        metal_smooth.alpha_mode = "CHANNEL_PACKED"
        metal_smooth.pixels.foreach_set(packed)

        paths = {
            "BaseColor": save_image(color, NAME + "_BaseColor.png"),
            "Metallic": save_image(metal, NAME + "_Metallic.png"),
            "Roughness": save_image(rough, NAME + "_Roughness.png"),
            "Occlusion": save_image(occlusion, NAME + "_Occlusion.png"),
            "MetallicSmoothness": save_image(metal_smooth, NAME + "_MetallicSmoothness.png"),
            "Labels": labels_path,
        }
        for image in (color, metal, rough, occlusion, metal_smooth):
            bpy.data.images.remove(image)

        final = bpy.data.materials.new("HG_" + NAME)
        nodes_on(final)
        tree = final.node_tree
        for index, (key, socket_name, non_color) in enumerate((("BaseColor", "Base Color", False), ("Metallic", "Metallic", True), ("Roughness", "Roughness", True))):
            node = tree.nodes.new("ShaderNodeTexImage")
            node.image = load_image(paths[key], non_color)
            node.location = (-520, 320 - index * 300)
            tree.links.new(node.outputs["Color"], principled(final).inputs[socket_name])
        label_mat = bpy.data.materials.new("HG_Labels")
        nodes_on(label_mat)
        label_node = label_mat.node_tree.nodes.new("ShaderNodeTexImage")
        label_node.image = load_image(labels_path, False)
        label_node.location = (-520, 320)
        label_mat.node_tree.links.new(label_node.outputs["Color"], principled(label_mat).inputs["Base Color"])
        principled(label_mat).inputs["Roughness"].default_value = 0.55

        for obj in baked:
            obj.data.materials.clear()
            obj.data.materials.append(final)
            for polygon in obj.data.polygons:
                polygon.material_index = 0
        for mat in PROC:
            bpy.data.materials.remove(mat)
        label_objects = build_label_objects(items, label_mat)
        for host, target in hosts.items():
            if host in label_objects:
                select_only([label_objects[host], target], active=target)
                bpy.ops.object.join()
        report["textures"] = {key: os.path.relpath(value, OUT).replace(os.sep, "/") for key, value in paths.items()}
        report["texture_size"] = TEX
        report["label_font"] = LABEL_FONT["name"]
    else:
        for obj in baked:
            compact_materials(obj)

    # The elevation stage rides on the azimuth stage: a plain offset, no rotation and no parent-inverse matrix.
    elevation.parent = azimuth
    elevation.location = Vector((0.0, 0.0, EL_Z - AZ_Z1))
    for obj in parts:
        weighted_normals(obj)
    bpy.context.view_layer.update()

    lo, hi = world_bounds(parts)
    triangles = {obj.name: tri_count(obj) for obj in parts}
    report.update({
        "dimensions_m": {"width_x": round(hi.x - lo.x, 4), "depth_y": round(hi.y - lo.y, 4), "height_z": round(hi.z - lo.z, 4)},
        "bounds_min": [round(v, 4) for v in lo],
        "bounds_max": [round(v, 4) for v in hi],
        "triangles": triangles,
        "triangles_total": sum(triangles.values()),
        "object_origins": {obj.name: [round(v, 4) for v in obj.matrix_world.translation] for obj in parts},
        "hierarchy": {obj.name: obj.parent.name for obj in parts if obj.parent},
        "materials": {obj.name: [mat.name for mat in obj.data.materials] for obj in parts},
    })
    log("dimensions", report["dimensions_m"], "triangles", triangles, "total", report["triangles_total"])

    cameras = build_render_set(scene)

    def pose(slewed):
        """Neutral pose, or the dish turned and raised with the electronics box drawn out."""
        azimuth.rotation_euler = (0.0, 0.0, radians(-SLEW_AZ) if slewed else 0.0)
        elevation.rotation_euler = (radians(-SLEW_EL) if slewed else 0.0, 0.0, 0.0)
        unit.location = box_origin() + Vector((0.0, -SLEW_BOX if slewed else 0.0, 0.0))

    if full:
        render(scene, cameras["hero"], NAME + "_hero.png", (1500, 2000), SAMPLES)
        render(scene, cameras["back"], NAME + "_back.png", (1500, 2000), SAMPLES)
        render(scene, cameras["detail"], NAME + "_detail.png", (1800, 1200), SAMPLES)
        pose(True)
        render(scene, cameras["side"], NAME + "_slewed.png", (1500, 2000), SAMPLES)
        pose(False)
        bpy.context.view_layer.material_override = wire_material()
        render(scene, cameras["hero"], NAME + "_wire.png", (1500, 2000), 48)
        bpy.context.view_layer.material_override = None
        scene.camera = cameras["hero"]

        # FBX first, while the scene is still unsaved and its texture links are cut: the exporter writes the
        # path of the .blend and of every linked texture into the file, and those are paths on this machine.
        cut = []
        for mat in {slot for obj in parts for slot in obj.data.materials if slot is not None and slot.node_tree}:
            for link in list(mat.node_tree.links):
                if link.from_node.type == "TEX_IMAGE":
                    cut.append((mat.node_tree, link.from_socket, link.to_socket))
                    mat.node_tree.links.remove(link)
        select_only(parts, active=base)
        bpy.ops.export_scene.fbx(
            filepath=os.path.join(OUT, NAME + ".fbx"), use_selection=True, object_types={"MESH"},
            apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
            bake_space_transform=True, use_mesh_modifiers=True, mesh_smooth_type="OFF", use_triangles=True,
            add_leaf_bones=False, bake_anim=False, path_mode="RELATIVE", embed_textures=False)
        for tree, source, target in cut:
            tree.links.new(source, target)

        blend_path = os.path.join(OUT, NAME + ".blend")
        scene.render.filepath = "//renders/"
        bpy.context.preferences.filepaths.save_version = 0
        bpy.ops.wm.save_as_mainfile(filepath=blend_path)
        bpy.ops.file.make_paths_relative()
        bpy.ops.wm.save_mainfile()
        log("exported .fbx and saved .blend")
    else:
        render(scene, cameras["hero"], "geometry_hero.png", (900, 1200), 48)
        render(scene, cameras["back"], "geometry_back.png", (900, 1200), 48)
        render(scene, cameras["detail"], "geometry_detail.png", (1200, 800), 48)
        pose(True)
        render(scene, cameras["side"], "geometry_slewed.png", (900, 1200), 48)
        pose(False)

    with open(os.path.join(OUT, NAME + "_report.json" if full else "geometry_report.json"), "w", encoding="utf-8", newline=chr(10)) as handle:
        json.dump(report, handle, indent=2)
    log("done")


main()
