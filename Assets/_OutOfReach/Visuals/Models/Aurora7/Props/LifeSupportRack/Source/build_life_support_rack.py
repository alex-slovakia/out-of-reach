"""
OUT OF REACH - life-support rack ARS-1 (original asset, built from code).

Builds the model, its label atlas and baked PBR textures, renders previews, saves the .blend
and exports the FBX. Everything is generated here: no external models, textures or logos.

    blender -b --factory-startup --python build_life_support_rack.py -- --stage full
    blender -b --factory-startup --python build_life_support_rack.py -- --stage geometry   (fast shape check)

Axes (Blender): X = width, Z = height, -Y = front (towards the viewer), +Y = wall side.
Model pivot: bottom centre of the bounding box (0, 0, 0). Units: metres.
The shared helpers come from the EPS-2 service panel script.
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
    print("[rack %6.1fs]" % (time.time() - T0), *parts, flush=True)


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
NAME = "LifeSupportRack"
W, H, D = 0.80, 1.40, 0.26
YB = D / 2.0                 # back face, against the wall
YF = -D / 2.0                # front-most plane
Y_PLATE = YB - 0.012         # front of the mounting plate
Y_FACE = -0.020              # painted front face of the enclosure
Y_SUB = Y_FACE - 0.004       # front of the graphite sub-panel
BEZEL_Y0, BEZEL_Y1 = -0.042, -0.012
BAR = 0.045                  # bezel bar width
ENC_X, ENC_Z0, ENC_Z1 = 0.375, 0.025, 1.375
IND_PLATE = (-0.345, -0.085, 1.150, 1.300)     # x0, x1, z0, z1
LAMP_X, LAMP_Z = (-0.290, -0.215, -0.140), 1.243
GAUGE_X, GAUGE_Z = 0.215, 1.225
NEEDLE_DEG = 48.0            # needle angle clockwise from the top (about 140 kPa)
CAN_X, CAN_Z = (-0.165, 0.165), 0.870
FLANGE = 0.125               # half-size of a socket mounting plate
SOCKET_Y = Y_FACE - 0.004    # front of a socket mounting plate
SOCKET_DEPTH = 0.040
CAN_BASE_Y = SOCKET_Y - 0.005                  # base plane of a seated canister
CAN_LEN = 0.096
FAN_X, FAN_Z, FAN_R = -0.165, 0.470, 0.120
DRAWER = (0.020, 0.335, 0.385, 0.555)          # x0, x1, z0, z1
DRAWER_Y = Y_FACE - 0.014
PIPE_Z, PIPE_Y, PIPE_R = 0.170, -0.050, 0.013
VALVE_X = (-0.220, 0.0, 0.220)
DROP_Z = 0.085

# ------------------------------------------------------------------ materials
MAT_KEYS = ["paint", "graphite", "steel", "dark", "connector", "interior", "copper", "insulator", "ceramic", "accent"]
PAINT, GRAPHITE, STEEL, DARK, CONNECTOR, INTERIOR, COPPER, INSULATOR, CERAMIC, ACCENT = range(10)
MAT_SPEC = {
    #             base sRGB  metal rough  wear  worn sRGB  worn metal  dirt
    "paint":     ("#C9CBC6", 0.00, 0.42, 0.60, "#8D8F92", 1.0, 0.70),
    "graphite":  ("#1C1E21", 0.30, 0.50, 0.50, "#5E6165", 0.7, 0.45),
    "steel":     ("#A9ABAE", 1.00, 0.34, 0.00, "#A9ABAE", 1.0, 0.55),
    "dark":      ("#0D0D0E", 0.00, 0.58, 0.00, "#0D0D0E", 0.0, 0.30),
    "connector": ("#73766C", 1.00, 0.44, 0.35, "#A3A5A0", 1.0, 0.50),
    "interior":  ("#2B2D30", 0.15, 0.62, 0.25, "#74777A", 1.0, 0.60),
    "copper":    ("#B9743C", 1.00, 0.36, 0.00, "#B9743C", 1.0, 0.50),
    "insulator": ("#B79A5C", 0.00, 0.62, 0.00, "#B79A5C", 0.0, 0.45),
    "ceramic":   ("#E3E0D6", 0.00, 0.36, 0.00, "#E3E0D6", 0.0, 0.30),
    "accent":    ("#D49A1C", 0.00, 0.46, 0.45, "#8D8F92", 1.0, 0.50),
}
INDICATORS = (("LS_Indicator_Cyan", "#19D3FF", 2.2), ("LS_Indicator_Amber", "#FFA61A", 1.8), ("LS_Indicator_Red", "#FF2A1A", 0.25))
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
    mat = bpy.data.materials.new("LS_" + key)
    nodes_on(mat)
    node = principled(mat)
    node.inputs["Base Color"].default_value = hex_rgba(base_hex)
    node.inputs["Metallic"].default_value = metal
    node.inputs["Roughness"].default_value = rough
    return mat


def make_proc_material(key):
    """Source material with restrained procedural wear; only used as the bake source."""
    base_hex, metal, rough, wear, wear_hex, wear_metal, dirt = MAT_SPEC[key]
    mat = bpy.data.materials.new("LSsrc_" + key)
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
    mesh = bpy.data.meshes.new("LifeSupportRack_" + name)
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


def quarter_turn(bm, x, z, y, angle):
    """Captive quarter-turn fastener with a slotted head."""
    lathe(bm, front(x, y, z), [(0.0120, 0.0), (0.0120, 0.0012), (0.0090, 0.0012), (0.0090, 0.0040), (0.0074, 0.0052)], 10, [STEEL] * 4, cap_mat=STEEL)
    slot = front(x, y, z) @ Matrix.Rotation(angle, 4, "Z") @ Matrix.Translation((0.0, 0.0, 0.0053)) @ Matrix.Diagonal((0.0018, 0.0128, 0.0004, 1.0))
    for face in {f for v in bmesh.ops.create_cube(bm, size=1.0, matrix=slot)["verts"] for f in v.link_faces}:
        face.material_index = DARK



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


def enclosure(bm):
    """Painted box. Its back face is always against the mounting plate, so it is removed."""
    matrix = Matrix.Translation((0.0, (Y_FACE + Y_PLATE) / 2, (ENC_Z0 + ENC_Z1) / 2)) @ Matrix.Diagonal((2 * ENC_X, Y_PLATE - Y_FACE, ENC_Z1 - ENC_Z0, 1.0))
    verts = bmesh.ops.create_cube(bm, size=1.0, matrix=matrix)["verts"]
    faces = {f for v in verts for f in v.link_faces}
    for face in faces:
        face.material_index = PAINT
    bm.faces.remove(max(faces, key=lambda f: f.calc_center_median().y))


def socket(bm, x):
    """Mounting plate and collar that a scrubber canister seats into."""
    box(bm, x - FLANGE, x + FLANGE, SOCKET_Y, Y_FACE, CAN_Z - FLANGE, CAN_Z + FLANGE, GRAPHITE, bevel=0.0015)
    for sx in (-1, 1):
        for sz in (-1, 1):
            bolt(bm, x + sx * (FLANGE - 0.016), CAN_Z + sz * (FLANGE - 0.016), SOCKET_Y)
    lathe(bm, front(x, SOCKET_Y, CAN_Z), [(0.118, 0.0), (0.118, SOCKET_DEPTH), (0.106, SOCKET_DEPTH), (0.106, 0.002)], 28, [GRAPHITE, GRAPHITE, DARK], cap_mat=DARK)


def fan(bm):
    """Cabin air inlet: housing and wire guard. The rotor is a separate object, see build_fan_rotor()."""
    base = front(FAN_X, Y_FACE, FAN_Z)
    lathe(bm, base, [(FAN_R + 0.012, 0.0), (FAN_R + 0.012, 0.030), (FAN_R, 0.030), (FAN_R, 0.004)], 24, [GRAPHITE, GRAPHITE, DARK], cap_mat=DARK)
    for radius in (0.040, 0.075, 0.108):
        ring(bm, base, radius, 0.0255, 0.0305, 0.0025, 24, STEEL)
    for k in range(6):
        spoke = base @ Matrix.Rotation(radians(60.0 * k + 30.0), 4, "Z") @ Matrix.Translation(((0.014 + FAN_R + 0.004) / 2, 0.0, 0.0245))
        box_m(bm, spoke, (FAN_R + 0.004 - 0.014, 0.005, 0.004), STEEL)
    lathe(bm, base, [(0.016, 0.0225), (0.016, 0.0320)], 10, [STEEL], cap_mat=STEEL)


def drawer(bm):
    """Front of the particulate filter drawer with its pull handle."""
    x0, x1, z0, z1 = DRAWER
    xc, zc = (x0 + x1) / 2, (z0 + z1) / 2
    box(bm, x0, x1, DRAWER_Y, Y_FACE, z0, z1, PAINT, bevel=0.003, segs=2)
    for x in (x0 + 0.022, x1 - 0.022):
        for z in (z0 + 0.022, z1 - 0.022):
            quarter_turn(bm, x, z, DRAWER_Y, radians(35.0 if x < xc else 120.0))
    for x in (xc - 0.070, xc + 0.070):
        lathe(bm, front(x, DRAWER_Y, zc - 0.023), [(0.0060, 0.0), (0.0060, 0.0190)], 8, [STEEL], cap_mat=STEEL)
    box(bm, xc - 0.090, xc + 0.090, DRAWER_Y - 0.034, DRAWER_Y - 0.018, zc - 0.031, zc - 0.015, GRAPHITE, bevel=0.004, segs=2)


def manifold(bm):
    """Supply manifold: one header tube, three lever valves and their drop lines into the enclosure."""
    lathe(bm, Matrix.Translation((-0.300, PIPE_Y, PIPE_Z)) @ RY90, [(PIPE_R, 0.0), (PIPE_R, 0.600)], 12, [STEEL])
    for x in (-0.300, 0.300):
        box(bm, x - 0.019, x + 0.019, PIPE_Y - 0.019, PIPE_Y + 0.019, PIPE_Z - 0.019, PIPE_Z + 0.019, CONNECTOR, bevel=0.003)
        lathe(bm, Matrix.Translation((x, PIPE_Y, PIPE_Z)) @ RXM90, [(PIPE_R, 0.0), (PIPE_R, Y_FACE - PIPE_Y)], 12, [STEEL])
        lathe(bm, front(x, Y_FACE, PIPE_Z), [(0.024, 0.0), (0.024, 0.004)], 12, [STEEL], cap_mat=STEEL)
    for x in (-0.110, 0.110):
        box(bm, x - 0.008, x + 0.008, PIPE_Y + 0.004, Y_FACE, PIPE_Z - 0.017, PIPE_Z + 0.017, GRAPHITE)
    for index, x in enumerate(VALVE_X):
        box(bm, x - 0.025, x + 0.025, PIPE_Y - 0.023, PIPE_Y + 0.023, PIPE_Z - 0.025, PIPE_Z + 0.025, CONNECTOR, bevel=0.003)
        stem = front(x, PIPE_Y - 0.023, PIPE_Z)
        lathe(bm, stem, [(0.0100, 0.0), (0.0100, 0.0100), (0.0060, 0.0100), (0.0060, 0.0160)], 8, [STEEL] * 3, cap_mat=STEEL)
        turn = radians(90.0) if index == 2 else 0.0               # V3, the cabin vent, is closed
        lever = stem @ Matrix.Rotation(turn, 4, "Z") @ Matrix.Translation((0.028, 0.0, 0.019))
        box_m(bm, lever, (0.080, 0.014, 0.006), ACCENT, bevel=0.002)
        lathe(bm, upright(x, PIPE_Y, DROP_Z), [(0.0100, 0.0), (0.0100, PIPE_Z - 0.025 - DROP_Z)], 10, [STEEL])
        box(bm, x - 0.015, x + 0.015, PIPE_Y - 0.015, PIPE_Y + 0.015, DROP_Z - 0.015, DROP_Z + 0.015, CONNECTOR, bevel=0.003)
        lathe(bm, Matrix.Translation((x, PIPE_Y, DROP_Z)) @ RXM90, [(0.0100, 0.0), (0.0100, Y_FACE - PIPE_Y)], 10, [STEEL])
        lathe(bm, front(x, Y_FACE, DROP_Z), [(0.020, 0.0), (0.020, 0.004)], 12, [STEEL], cap_mat=STEEL)


def gauge(bm):
    """Pressure gauge: bezel, needle and hub. The dial artwork is a label placard on the recess floor."""
    base = front(GAUGE_X, Y_FACE, GAUGE_Z)
    lathe(bm, base, [(0.060, 0.0), (0.060, 0.016), (0.053, 0.020), (0.0425, 0.020), (0.0425, 0.003)], 20, [GRAPHITE, GRAPHITE, STEEL, DARK], cap_mat=DARK)
    needle = base @ Matrix.Rotation(radians(-NEEDLE_DEG), 4, "Z") @ Matrix.Translation((0.0, 0.012, 0.0062))
    box_m(bm, needle, (0.0030, 0.034, 0.0012), DARK)
    lathe(bm, base @ Matrix.Translation((0.0, 0.0, 0.0050)), [(0.0050, 0.0), (0.0050, 0.0030)], 8, [STEEL], cap_mat=STEEL)


def build_body():
    bm = new_bm()
    box(bm, -W / 2, W / 2, Y_PLATE, YB, 0.0, H, GRAPHITE)                                            # mounting plate
    enclosure(bm)
    box(bm, -W / 2, W / 2, BEZEL_Y0, BEZEL_Y1, H - BAR, H, GRAPHITE, bevel=0.004, segs=2)           # bezel
    box(bm, -W / 2, W / 2, BEZEL_Y0, BEZEL_Y1, 0.0, BAR, GRAPHITE, bevel=0.004, segs=2)
    box(bm, -W / 2, -W / 2 + BAR, BEZEL_Y0, BEZEL_Y1, BAR, H - BAR, GRAPHITE, bevel=0.004, segs=2)
    box(bm, W / 2 - BAR, W / 2, BEZEL_Y0, BEZEL_Y1, BAR, H - BAR, GRAPHITE, bevel=0.004, segs=2)
    mid = W / 2 - BAR / 2
    for x in (-mid, mid):
        for z in (BAR / 2, 0.250, 0.480, 0.700, 0.920, 1.150, H - BAR / 2):
            bolt(bm, x, z, BEZEL_Y0)
    for x in (-0.130, 0.130):
        for z in (BAR / 2, H - BAR / 2):
            bolt(bm, x, z, BEZEL_Y0)

    x0, x1, z0, z1 = IND_PLATE                                                                       # lamp sub-panel
    box(bm, x0, x1, Y_SUB, Y_FACE, z0, z1, GRAPHITE, bevel=0.0015)
    for x in (x0 + 0.010, x1 - 0.010):
        for z in (z0 + 0.010, z1 - 0.010):
            small_screw(bm, x, z, Y_SUB)
    for x in LAMP_X:
        lathe(bm, front(x, Y_SUB, LAMP_Z), [(0.0150, 0.0), (0.0150, 0.0045), (0.0106, 0.0045), (0.0106, 0.0010)], 12, [STEEL, STEEL, DARK], cap_mat=DARK)

    gauge(bm)
    for x in CAN_X:
        socket(bm, x)
    fan(bm)
    drawer(bm)
    manifold(bm)
    for side in (-1, 1):                                                                             # cooling fins
        for i in range(8):
            z = 0.950 + i * 0.045
            xa, xb = sorted((side * ENC_X, side * (ENC_X + 0.020)))
            box(bm, xa, xb, 0.000, 0.090, z - 0.002, z + 0.002, GRAPHITE)
    return bm


def canister_origin(index):
    """Pivot of a canister: centre of its base, on its axis. Pull it out along the model's front direction."""
    return Vector((CAN_X[index], CAN_BASE_Y, CAN_Z))


def build_canister(index):
    """CO2 scrubber canister with a marked band and a fold-flat pull handle."""
    bm = new_bm()
    base = front(CAN_X[index], CAN_BASE_Y, CAN_Z)
    profile = [(0.094, 0.0), (0.100, 0.006), (0.100, 0.050), (0.097, 0.050), (0.097, 0.058), (0.100, 0.058), (0.100, 0.084),
               (0.103, 0.084), (0.103, CAN_LEN), (0.090, CAN_LEN), (0.090, 0.082)]
    mats = [STEEL, STEEL, STEEL, ACCENT, STEEL, STEEL, STEEL, STEEL, STEEL, GRAPHITE]
    lathe(bm, base, profile, 28, mats, cap_mat=PAINT, start_cap=True)
    for x in (-0.050, 0.050):
        lathe(bm, base @ Matrix.Translation((x, 0.0, 0.082)), [(0.0060, 0.0), (0.0060, 0.0110)], 8, [STEEL], cap_mat=STEEL)
    box_m(bm, base @ Matrix.Translation((0.0, 0.0, 0.096)), (0.124, 0.014, 0.010), ACCENT, bevel=0.003, segs=2)
    return bm


def fan_origin():
    """Pivot of the fan rotor: on the fan axis, at the enclosure face. It spins about the model's front axis."""
    return Vector((FAN_X, Y_FACE, FAN_Z))


def build_fan_rotor():
    """Hub and pitched blades of the inlet fan, kept apart from the body so that the fan can spin."""
    bm = new_bm()
    base = front(FAN_X, Y_FACE, FAN_Z)
    lathe(bm, base @ Matrix.Translation((0.0, 0.0, 0.004)), [(0.030, 0.0), (0.030, 0.014), (0.020, 0.019)], 12, [STEEL, STEEL], cap_mat=STEEL)
    for k in range(5):
        a = radians(72.0 * k)
        corners = ((0.030, a - radians(9.0), 0.007), (0.108, a - radians(15.0), 0.007), (0.108, a + radians(15.0), 0.019), (0.030, a + radians(9.0), 0.015))
        blade = bm.faces.new([bm.verts.new(base @ Vector((r * cos(t), r * sin(t), z))) for r, t, z in corners])
        blade.material_index = CONNECTOR
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
    scene = bpy.data.scenes.new("LS_LabelAtlas")
    made = []
    colors = {}
    font = load_label_font()

    def color_mat(hex_color):
        if hex_color not in colors:
            colors[hex_color] = emission_material("LSatlas_" + hex_color.lstrip("#"), hex_color)
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
            curve = bpy.data.curves.new("LSatlas_text", "FONT")
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
                obj = bpy.data.objects.new("LSatlas_text", curve)
                obj.scale = (0.01, 0.01, 0.01)
                obj.location = (x + w / 2 + dx + ox * shift, y + h / 2 + dz - size * TEXT_DROP + oy * shift, 0.002)
                scene.collection.objects.link(obj)
                made.append(obj)

    for hex_color, faces in quads.items():
        bm = bmesh.new()
        for corners in faces:
            bm.faces.new([bm.verts.new(c) for c in corners])
        mesh = bpy.data.meshes.new("LSatlas_fill")
        bm.to_mesh(mesh)
        bm.free()
        mesh.materials.append(color_mat(hex_color))
        obj = bpy.data.objects.new("LSatlas_fill", mesh)
        scene.collection.objects.link(obj)
        made.extend((obj, mesh))

    camera_data = bpy.data.cameras.new("LSatlas_cam")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = max(ATLAS_W, ATLAS_H) * px
    camera = bpy.data.objects.new("LSatlas_cam", camera_data)
    camera.location = (ATLAS_W * px / 2, ATLAS_H * px / 2, 1.0)
    scene.collection.objects.link(camera)
    scene.camera = camera
    made.extend((camera, camera_data))

    world = bpy.data.worlds.new("LSatlas_world")
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

    def add(key, host, cx, cz, w, h, y, style, texts, flip=False, shapes=()):
        items.append(dict(key=key, host=host, c=(cx, cz), s=(w, h), y=y, style=style, texts=texts, flip=flip, shapes=shapes))

    add("title", "Body", 0.0, 1.328, 0.44, 0.026, Y_FACE, "dark", [("ARS-1   AIR REVITALIZATION RACK", 0.0, 0.0, 0.0125)])
    add("hdr_status", "Body", -0.215, 1.286, 0.20, 0.016, Y_SUB, "dark", [("ATMOSPHERE STATUS", 0.0, 0.0, 0.0088)])
    for x, text in zip(LAMP_X, ("AIRFLOW", "CO2 HIGH", "FAULT")):
        add("lamp_" + text.replace(" ", "_"), "Body", x, 1.190, 0.070, 0.020, Y_SUB, "light", [(text, 0.0, 0.0, 0.0098)])
    add("id_plate", "Body", 0.035, 1.225, 0.20, 0.085, Y_FACE, "light",
        [("ARS-1", 0.0, 0.026, 0.017), ("AIR REVITALIZATION SYSTEM", 0.0, 0.006, 0.0078),
         ("S/N A7-L1-004     MASS 86 kg", 0.0, -0.012, 0.0072), ("28 V DC     310 W", 0.0, -0.028, 0.0072)])

    ticks = []
    for i in range(11):                                  # 0 to 200 kPa over 240 degrees
        a = radians(-120.0 + 24.0 * i)
        dx, dz, px, pz = sin(a), cos(a), cos(a), -sin(a)
        r0, r1, hw = (0.0290, 0.0380, 0.0009) if i % 5 == 0 else (0.0320, 0.0380, 0.0006)
        ticks.append(("#101113", [(dx * r0 - px * hw, dz * r0 - pz * hw), (dx * r0 + px * hw, dz * r0 + pz * hw),
                                  (dx * r1 + px * hw, dz * r1 + pz * hw), (dx * r1 - px * hw, dz * r1 - pz * hw)]))
    side = 0.0215
    dial_texts = [("0", sin(radians(-120.0)) * side, cos(radians(-120.0)) * side, 0.0070), ("100", 0.0, 0.0200, 0.0070),
                  ("200", sin(radians(120.0)) * side, cos(radians(120.0)) * side, 0.0070), ("kPa", 0.0, -0.0290, 0.0080)]
    add("dial", "Body", GAUGE_X, GAUGE_Z, 0.084, 0.084, Y_FACE - 0.0030, "dial", dial_texts, shapes=ticks)
    add("gauge_label", "Body", GAUGE_X, 1.148, 0.13, 0.018, Y_FACE, "light", [("O2 SUPPLY PRESSURE", 0.0, 0.0, 0.0085)])

    add("hazard", "Body", 0.0, 1.085, 0.48, 0.016, Y_FACE, "hazard", [("REPLACE SCRUBBER BED WHEN CO2 HIGH IS LIT", 0.0, 0.0, 0.0088)])
    for index, x in enumerate(CAN_X):
        letter = "AB"[index]
        add("bay_" + letter, "Body", x, 1.022, 0.20, 0.021, Y_FACE, "light", [("CO2 SCRUBBER   BED " + letter, 0.0, 0.0, 0.0105)])
        add("can_" + letter, "Canister" + letter, x, CAN_Z - 0.040, 0.110, 0.040, CAN_BASE_Y - 0.082, "light",
            [("BED " + letter, 0.0, 0.008, 0.018), ("LiOH CARTRIDGE", 0.0, -0.011, 0.0075)])
    add("caution", "Body", 0.1775, 0.650, 0.16, 0.085, Y_FACE, "caution",
        [("CAUTION", 0.0, 0.022, 0.021), ("ROTATING FAN", 0.0, -0.003, 0.0115), ("CLOSE V3 BEFORE SERVICE", 0.0, -0.024, 0.0078)])
    add("fan_id", "Body", FAN_X, 0.655, 0.15, 0.019, Y_FACE, "light", [("INLET FAN   FN-1", 0.0, 0.0, 0.0095)])
    add("fan_label", "Body", FAN_X, 0.318, 0.17, 0.019, Y_FACE, "light", [("CABIN AIR INLET", 0.0, 0.0, 0.0098)])
    add("drawer_label", "Body", (DRAWER[0] + DRAWER[1]) / 2, 0.525, 0.19, 0.019, DRAWER_Y, "light", [("PARTICULATE FILTER   F-1", 0.0, 0.0, 0.0092)])
    for x, text in zip(VALVE_X, ("V1 O2 SUPPLY", "V2 CONDENSATE", "V3 CABIN VENT")):
        add("valve_" + text[:2], "Body", x, 0.250, 0.125, 0.019, Y_FACE, "light", [(text, 0.0, 0.0, 0.0095)])
    return items



def build_label_objects(items, material):
    """One label mesh per host object; each is joined into its host afterwards."""
    result = {}
    for host in sorted({p["host"] for p in items}):
        bm = bmesh.new()
        uv = bm.loops.layers.uv.new("UVMap")
        for item in (p for p in items if p["host"] == host):
            (cx, cz), (w, h) = item["c"], item["s"]
            x, v, pw, ph = item["px"]
            u0, v0, u1, v1 = x / ATLAS_W, v / ATLAS_H, (x + pw) / ATLAS_W, (v + ph) / ATLAS_H
            if item["flip"]:
                y = item["y"] + 0.0004
                corners = [(cx + w / 2, cz - h / 2, u0, v0), (cx - w / 2, cz - h / 2, u1, v0), (cx - w / 2, cz + h / 2, u1, v1), (cx + w / 2, cz + h / 2, u0, v1)]
            else:
                y = item["y"] - 0.0004
                corners = [(cx - w / 2, cz - h / 2, u0, v0), (cx + w / 2, cz - h / 2, u1, v0), (cx + w / 2, cz + h / 2, u1, v1), (cx - w / 2, cz + h / 2, u0, v1)]
            face = bm.faces.new([bm.verts.new((px_, y, pz)) for px_, pz, _u, _v in corners])
            for loop, (_x, _z, u, vv) in zip(face.loops, corners):
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
    """The wall side of the mounting plate and its mostly covered front get a fraction of the texel density."""
    mesh = obj.data
    uv = mesh.uv_layers.active.data
    for polygon in mesh.polygons:
        if polygon.area < 0.5 or abs(polygon.normal.y) < 0.99:
            continue
        y = polygon.center.y + obj.location.y
        if y > Y_PLATE + 0.006:
            factor = 0.20
        elif abs(y - Y_PLATE) < 0.001:
            factor = 0.45
        else:
            continue
        loops = list(polygon.loop_indices)
        cu = sum(uv[i].uv.x for i in loops) / len(loops)
        cv = sum(uv[i].uv.y for i in loops) / len(loops)
        for i in loops:
            uv[i].uv = ((uv[i].uv.x - cu) * factor + cu, (uv[i].uv.y - cv) * factor + cv)


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
    collection = bpy.data.collections.new("LS_RenderSet")
    scene.collection.children.link(collection)

    wall_mesh = bpy.data.meshes.new("LS_Wall")
    bm = bmesh.new()
    bm.faces.new([bm.verts.new(p) for p in ((-3, YB + 0.0005, -2), (3, YB + 0.0005, -2), (3, YB + 0.0005, 3.8), (-3, YB + 0.0005, 3.8))])
    bm.to_mesh(wall_mesh)
    bm.free()
    wall_mat = bpy.data.materials.new("LS_RenderWall")
    nodes_on(wall_mat)
    principled(wall_mat).inputs["Base Color"].default_value = hex_rgba("#2A2D31")
    principled(wall_mat).inputs["Roughness"].default_value = 0.62
    principled(wall_mat).inputs["Metallic"].default_value = 0.2
    wall_mesh.materials.append(wall_mat)
    collection.objects.link(bpy.data.objects.new("LS_Wall", wall_mesh))

    def area(name, location, target, power, size, color):
        data = bpy.data.lights.new(name, "AREA")
        data.energy, data.size, data.color = power, size, color
        obj = bpy.data.objects.new(name, data)
        obj.location = location
        look_at(obj, target)
        collection.objects.link(obj)

    area("LS_Key", (-1.6, -2.5, 2.2), (0.0, 0.0, 0.75), 190.0, 1.5, (0.94, 0.97, 1.0))
    area("LS_Fill", (2.0, -2.0, 0.6), (0.0, 0.0, 0.65), 40.0, 2.3, (1.0, 0.95, 0.88))
    area("LS_Top", (0.2, -0.85, 2.8), (0.0, -0.05, 1.0), 70.0, 1.0, (1.0, 1.0, 1.0))

    cameras = {}

    def camera(key, location, target, lens=None, ortho=None):
        data = bpy.data.cameras.new("LS_Cam_" + key)
        if ortho is not None:
            data.type = "ORTHO"
            data.ortho_scale = ortho
        else:
            data.lens = lens
        obj = bpy.data.objects.new("LS_Cam_" + key, data)
        obj.location = location
        look_at(obj, target)
        collection.objects.link(obj)
        cameras[key] = obj

    camera("hero", (-1.30, -2.85, 1.18), (0.0, 0.0, 0.70), lens=70.0)
    camera("front", (0.0, -3.0, 0.70), (0.0, 0.0, 0.70), ortho=1.64)
    camera("detail", (-0.72, -1.12, 1.02), (-0.02, 0.0, 0.90), lens=55.0)
    camera("side", (1.10, -2.45, 1.10), (-0.05, 0.0, 0.72), lens=60.0)

    world = bpy.data.worlds.new("LS_World")
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
    mat = bpy.data.materials.new("LS_WireOverlay")
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
    lamps = [make_indicator_material(*spec) for spec in INDICATORS]

    body = finish(build_body(), "Body", surface)
    canisters = [finish(build_canister(i), "Canister" + "AB"[i], surface, origin=canister_origin(i)) for i in range(2)]
    rotor = finish(build_fan_rotor(), "FanRotor", surface, origin=fan_origin())
    indicators = finish(build_indicators(), "StatusIndicators", lamps)
    indicators.data.uv_layers.new(name="UVMap")      # every exported mesh carries UV0 (and so tangents) in Unity
    baked = [body] + canisters + [rotor]
    parts = baked + [indicators]
    hosts = {"Body": body, "CanisterA": canisters[0], "CanisterB": canisters[1]}
    report = {"blender": bpy.app.version_string, "stage": STAGE, "cycles_device": device}

    if full:
        items = placards()
        pack_atlas(items)
        labels_path = os.path.join(TEX_DIR, NAME + "_Labels.png")
        render_label_atlas(items, labels_path)
        log("label atlas rendered")

        unwrap(baked)
        color = new_image("LSbake_color", False)
        metal = new_image("LSbake_metal", True)
        rough = new_image("LSbake_rough", True)
        occlusion = new_image("LSbake_ao", True)
        together = [(obj, []) for obj in baked]
        bake_pass("color", color, together, 48)
        bake_pass("metal", metal, together, 48)
        bake_pass("rough", rough, together, 48)
        # Occlusion: each part on its own, so pulling a canister never reveals baked-in shadow. The rotor is
        # the exception: it keeps the body, whose housing and guard rings shade it alike at every angle.
        groups = [(obj, [other for other in parts if other is not obj and not (obj is rotor and other is body)]) for obj in baked]
        bake_pass("ao", occlusion, groups, 96)

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
        metal_smooth = bpy.data.images.new("LSbake_ms", TEX, TEX, alpha=True)
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

        final = bpy.data.materials.new("LS_LifeSupportRack")
        nodes_on(final)
        tree = final.node_tree
        for index, (key, socket_name, non_color) in enumerate((("BaseColor", "Base Color", False), ("Metallic", "Metallic", True), ("Roughness", "Roughness", True))):
            node = tree.nodes.new("ShaderNodeTexImage")
            node.image = load_image(paths[key], non_color)
            node.location = (-520, 320 - index * 300)
            tree.links.new(node.outputs["Color"], principled(final).inputs[socket_name])
        label_mat = bpy.data.materials.new("LS_Labels")
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
        report["textures"] = {key: os.path.relpath(value, OUT).replace("\\", "/") for key, value in paths.items()}
        report["texture_size"] = TEX
        report["label_font"] = LABEL_FONT["name"]
    else:
        for obj in baked:
            compact_materials(obj)

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
        "object_origins": {obj.name: [round(v, 4) for v in obj.location] for obj in parts},
        "materials": {obj.name: [mat.name for mat in obj.data.materials] for obj in parts},
    })
    log("dimensions", report["dimensions_m"], "triangles", triangles, "total", report["triangles_total"])

    cameras = build_render_set(scene)
    pulled = Vector((0.0, -0.14, 0.0))                 # canister A drawn out of its socket for one view
    turned = (0.0, radians(36.0), 0.0)                 # and the fan rotor turned half a blade pitch
    if full:
        render(scene, cameras["hero"], NAME + "_hero.png", (1500, 2000), SAMPLES)
        render(scene, cameras["front"], NAME + "_front.png", (1200, 2000), SAMPLES)
        render(scene, cameras["detail"], NAME + "_detail.png", (1800, 1200), SAMPLES)
        canisters[0].location = canisters[0].location + pulled
        rotor.rotation_euler = turned
        render(scene, cameras["side"], NAME + "_pulled.png", (1500, 2000), SAMPLES)
        canisters[0].location = canisters[0].location - pulled
        rotor.rotation_euler = (0.0, 0.0, 0.0)
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
        select_only(parts, active=body)
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
        canisters[0].location = canisters[0].location + pulled
        rotor.rotation_euler = turned
        render(scene, cameras["side"], "geometry_pulled.png", (900, 1200), 48)
        canisters[0].location = canisters[0].location - pulled
        rotor.rotation_euler = (0.0, 0.0, 0.0)

    with open(os.path.join(OUT, NAME + "_report.json" if full else "geometry_report.json"), "w", encoding="utf-8", newline="\n") as handle:
        json.dump(report, handle, indent=2)
    log("done")


main()
