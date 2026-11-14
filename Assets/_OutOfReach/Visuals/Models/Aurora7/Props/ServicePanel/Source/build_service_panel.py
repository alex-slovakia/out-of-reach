"""
OUT OF REACH - electrical service panel (original asset, built from code).

Builds the model, its label atlas and baked PBR textures, renders previews, saves the .blend
and exports the FBX. Everything is generated here: no external models, textures or logos.

    blender -b --factory-startup --python build_service_panel.py -- --stage full
    blender -b --factory-startup --python build_service_panel.py -- --stage geometry   (fast shape check)

Axes (Blender): X = width, Z = height, -Y = front (towards the viewer), +Y = wall side.
Model pivot: bottom centre of the bounding box (0, 0, 0). Units: metres.
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
    print("[panel %6.1fs]" % (time.time() - T0), *parts, flush=True)


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
W, H, D = 0.70, 1.20, 0.18
YB = D / 2.0                 # back face, against the wall
Y_PLATE = YB - 0.012         # front of the mounting plate
Y_FACE = -0.030              # painted front face of the enclosure
Y_SUB = Y_FACE - 0.004       # front of the graphite sub-panels
BEZEL_Y0, BEZEL_Y1 = -0.052, -0.022
BAR = 0.045                  # bezel bar width
ENC_X, ENC_Z0, ENC_Z1 = 0.325, 0.025, 1.175
CAV_X, CAV_Z0, CAV_Z1, CAV_Y = 0.235, 0.255, 0.745, 0.030
COVER_X, COVER_Z0, COVER_Z1 = 0.250, 0.240, 0.760
COVER_Y0, COVER_Y1 = -0.0405, -0.0305          # cover front / back
HINGE_X, HINGE_Y = -0.2575, -0.046
HINGE_Z = (0.34, 0.66)
COVER_ORIGIN = Vector((HINGE_X, HINGE_Y, COVER_Z0))   # ServiceCover pivot: on the hinge axis
LED_X, LED_Z = -0.245, (1.055, 0.975, 0.895)
IND_PLATE = (-0.280, -0.130, 0.835, 1.115)     # x0, x1, z0, z1
BRK_PLATE = (-0.115, 0.285, 0.835, 1.115)
BRK_X = (-0.0425, 0.0425, 0.1275, 0.2125)
BRK_Z = (1.040, 0.912)
TRIPPED = (1, 1)                               # row, column of the tripped breaker (CB6)
CONN_Z = 0.125
CONNECTORS = ((-0.205, 0.024, True), (-0.075, 0.024, True), (0.045, 0.016, False), (0.145, 0.016, True))
GND = (0.250, 0.125)

# ------------------------------------------------------------------ materials
MAT_KEYS = ["paint", "graphite", "steel", "dark", "connector", "interior", "copper", "insulator", "ceramic"]
PAINT, GRAPHITE, STEEL, DARK, CONNECTOR, INTERIOR, COPPER, INSULATOR, CERAMIC = range(9)
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
}
INDICATORS = (("SP_Indicator_Cyan", "#19D3FF", 2.2), ("SP_Indicator_Amber", "#FFA61A", 1.8), ("SP_Indicator_Red", "#FF2A1A", 0.25))
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
    mat = bpy.data.materials.new("SP_" + key)
    nodes_on(mat)
    node = principled(mat)
    node.inputs["Base Color"].default_value = hex_rgba(base_hex)
    node.inputs["Metallic"].default_value = metal
    node.inputs["Roughness"].default_value = rough
    return mat


def make_proc_material(key):
    """Source material with restrained procedural wear; only used as the bake source."""
    base_hex, metal, rough, wear, wear_hex, wear_metal, dirt = MAT_SPEC[key]
    mat = bpy.data.materials.new("SPsrc_" + key)
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
    mesh = bpy.data.meshes.new("ServicePanel_" + name)
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


def connector(bm, x, z, y, shell, mated):
    flange = shell * 1.45
    box(bm, x - flange, x + flange, y - 0.004, y, z - flange, z + flange, CONNECTOR, bevel=0.0015)
    for sx in (-1, 1):
        for sz in (-1, 1):
            small_screw(bm, x + sx * flange * 0.78, z + sz * flange * 0.78, y - 0.004, r=0.003)
    base = front(x, y - 0.004, z)
    if mated:      # receptacle, coupling ring, backshell and strain-relief taper
        profile = [(shell, 0.0), (shell, 0.010), (shell * 1.22, 0.010), (shell * 1.22, 0.030), (shell * 0.95, 0.034), (shell * 0.95, 0.050), (shell * 0.60, 0.056)]
        lathe(bm, base, profile, 14, [CONNECTOR, CONNECTOR, GRAPHITE, GRAPHITE, CONNECTOR, CONNECTOR], cap_mat=DARK)
    else:          # receptacle with a dust cap
        profile = [(shell, 0.0), (shell, 0.016), (shell * 1.14, 0.016), (shell * 1.14, 0.027), (shell * 0.90, 0.031)]
        lathe(bm, base, profile, 14, [CONNECTOR, CONNECTOR, DARK, DARK], cap_mat=DARK)


def breaker(bm, x, z, y, tripped):
    base = front(x, y, z)
    lathe(bm, base, [(0.0115, 0.0), (0.0115, 0.0045), (0.0072, 0.0045), (0.0072, 0.0075)], 6, [STEEL] * 3, cap_mat=STEEL, smooth=False)
    tilt = radians(26.0 if tripped else -26.0)       # negative tilts the lever up (ON)
    lever = base @ Matrix.Translation((0.0, 0.0, 0.0068)) @ Matrix.Rotation(tilt, 4, "X")
    lathe(bm, lever, [(0.0040, 0.0), (0.0044, 0.0085), (0.0058, 0.0085), (0.0058, 0.0195), (0.0046, 0.0215)], 8, [STEEL, DARK, DARK, DARK], cap_mat=DARK)


def enclosure(bm):
    """Painted box with a real service cavity behind the cover (closed shell, back face removed)."""
    xa, xb, za, zb, yf, yb = -ENC_X, ENC_X, ENC_Z0, ENC_Z1, Y_FACE, Y_PLATE

    def vert(x, y, z):
        return bm.verts.new((x, y, z))

    f = [vert(xa, yf, za), vert(xb, yf, za), vert(xb, yf, zb), vert(xa, yf, zb)]
    b = [vert(xa, yb, za), vert(xb, yb, za), vert(xb, yb, zb), vert(xa, yb, zb)]
    o = [vert(-CAV_X, yf, CAV_Z0), vert(CAV_X, yf, CAV_Z0), vert(CAV_X, yf, CAV_Z1), vert(-CAV_X, yf, CAV_Z1)]
    c = [vert(-CAV_X, CAV_Y, CAV_Z0), vert(CAV_X, CAV_Y, CAV_Z0), vert(CAV_X, CAV_Y, CAV_Z1), vert(-CAV_X, CAV_Y, CAV_Z1)]
    faces = []

    def face(verts, mat):
        new = bm.faces.new(verts)
        new.material_index = mat
        faces.append(new)
        return new

    for i in range(4):
        j = (i + 1) % 4
        face((f[i], f[j], o[j], o[i]), PAINT)          # front ring around the opening
        face((f[j], f[i], b[i], b[j]), PAINT)          # outer walls
        face((o[i], o[j], c[j], c[i]), INTERIOR)       # cavity walls
    face((c[0], c[1], c[2], c[3]), INTERIOR)           # cavity floor
    back = face((b[0], b[1], b[2], b[3]), PAINT)
    bmesh.ops.recalc_face_normals(bm, faces=faces)
    bm.faces.remove(back)                              # always hidden by the mounting plate


def interior(bm):
    """Simple but readable contents of the service cavity: bus bars, fuses and a terminal block."""
    box(bm, -0.200, 0.200, 0.024, CAV_Y, 0.275, 0.725, INSULATOR)
    box(bm, -0.160, 0.160, 0.004, 0.024, 0.285, 0.315, DARK, bevel=0.0015)
    for i in range(6):
        small_screw(bm, -0.125 + i * 0.050, 0.300, 0.004, r=0.0045)
    for x in (-0.100, 0.0, 0.100):
        box(bm, x - 0.012, x + 0.012, 0.014, 0.020, 0.330, 0.600, COPPER)
        for z in (0.355, 0.575):
            box(bm, x - 0.017, x + 0.017, 0.020, 0.024, z - 0.010, z + 0.010, DARK)
        profile = [(0.0115, 0.0), (0.0115, 0.012), (0.0100, 0.012), (0.0100, 0.058), (0.0115, 0.058), (0.0115, 0.070)]
        lathe(bm, upright(x, 0.008, 0.615), profile, 10, [STEEL, STEEL, CERAMIC, STEEL, STEEL], cap_mat=STEEL, start_cap=True)
        box(bm, x - 0.012, x + 0.012, 0.008, 0.020, 0.598, 0.618, COPPER)      # tab from the bar to the fuse


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
        for z in (BAR / 2, 0.255, 0.485, 0.715, 0.945, H - BAR / 2):
            bolt(bm, x, z, BEZEL_Y0)
    for x in (-0.11, 0.11):
        for z in (BAR / 2, H - BAR / 2):
            bolt(bm, x, z, BEZEL_Y0)

    for x0, x1, z0, z1 in (IND_PLATE, BRK_PLATE):                                                    # sub-panels
        box(bm, x0, x1, Y_SUB, Y_FACE, z0, z1, GRAPHITE, bevel=0.0015)
        for x in (x0 + 0.012, x1 - 0.012):
            for z in (z0 + 0.012, z1 - 0.012):
                small_screw(bm, x, z, Y_SUB)
    for z in LED_Z:                                                                                  # indicator bezels
        lathe(bm, front(LED_X, Y_SUB, z), [(0.0150, 0.0), (0.0150, 0.0045), (0.0106, 0.0045), (0.0106, 0.0010)], 12, [STEEL, STEEL, DARK], cap_mat=DARK)
    for row, z in enumerate(BRK_Z):                                                                  # breakers and guards
        for col, x in enumerate(BRK_X):
            breaker(bm, x, z, Y_SUB, (row, col) == TRIPPED)
        for i in range(5):
            x = BRK_X[0] - 0.0425 + i * 0.085
            box(bm, x - 0.0015, x + 0.0015, Y_SUB - 0.028, Y_SUB, z - 0.030, z + 0.035, GRAPHITE)

    for x, shell, mated in CONNECTORS:
        connector(bm, x, CONN_Z, Y_FACE, shell, mated)
    lathe(bm, front(GND[0], Y_FACE, GND[1]), [(0.0080, 0.0), (0.0080, 0.0050), (0.0040, 0.0050), (0.0040, 0.0120)], 6, [COPPER, COPPER, STEEL], cap_mat=STEEL, smooth=False)

    for zc in HINGE_Z:                                                                               # hinge, fixed half
        box(bm, HINGE_X - 0.032, HINGE_X - 0.004, -0.044, Y_FACE, zc - 0.036, zc + 0.036, STEEL, bevel=0.002)
        for dz in (-0.022, 0.022):
            small_screw(bm, HINGE_X - 0.020, zc + dz, -0.044)
        for z0, z1 in ((zc - 0.036, zc - 0.0155), (zc + 0.0155, zc + 0.036)):
            lathe(bm, upright(HINGE_X, HINGE_Y, z0), [(0.0062, 0.0), (0.0062, z1 - z0)], 10, [STEEL], cap_mat=STEEL, start_cap=True)

    for side in (-1, 1):                                                                             # cooling fins
        for i in range(8):
            z = 0.800 + i * 0.045
            x0, x1 = sorted((side * ENC_X, side * (ENC_X + 0.020)))
            box(bm, x0, x1, -0.010, 0.060, z - 0.002, z + 0.002, GRAPHITE)
    interior(bm)
    return bm


def build_cover():
    bm = new_bm()
    box(bm, -COVER_X, COVER_X, COVER_Y0, COVER_Y1, COVER_Z0, COVER_Z1, PAINT, bevel=0.0025, segs=2)
    spots = [(-0.17, 0.740), (0.0, 0.740), (0.17, 0.740), (-0.17, 0.260), (0.0, 0.260), (0.17, 0.260), (0.230, 0.400), (0.230, 0.600)]
    for i, (x, z) in enumerate(spots):
        quarter_turn(bm, x, z, COVER_Y0, radians((i * 37) % 180))
    for z in (0.440, 0.560):                                                                         # handle
        lathe(bm, front(0.185, COVER_Y0, z), [(0.0060, 0.0), (0.0060, 0.0190)], 8, [STEEL], cap_mat=STEEL)
    box(bm, 0.177, 0.193, COVER_Y0 - 0.034, COVER_Y0 - 0.018, 0.420, 0.580, GRAPHITE, bevel=0.004, segs=2)
    for i in range(6):                                                                               # louvres
        louver(bm, -0.160, 0.060, 0.310 + i * 0.022, 0.322 + i * 0.022, COVER_Y0)
    # Inside of the cover, seen when it swings open: seal strips and two stiffeners.
    seal = 0.2425
    box(bm, -seal - 0.004, seal + 0.004, COVER_Y1, COVER_Y1 + 0.0005, COVER_Z1 - 0.0115, COVER_Z1 - 0.0035, DARK)
    box(bm, -seal - 0.004, seal + 0.004, COVER_Y1, COVER_Y1 + 0.0005, COVER_Z0 + 0.0035, COVER_Z0 + 0.0115, DARK)
    for x in (-seal, seal):
        box(bm, x - 0.004, x + 0.004, COVER_Y1, COVER_Y1 + 0.0005, COVER_Z0 + 0.0115, COVER_Z1 - 0.0115, DARK)
    for z in (0.335, 0.665):
        box(bm, -0.200, 0.200, COVER_Y1, COVER_Y1 + 0.008, z - 0.009, z + 0.009, PAINT, bevel=0.002)
    for zc in HINGE_Z:                                                                               # hinge, moving half
        box(bm, HINGE_X + 0.003, HINGE_X + 0.034, COVER_Y0 - 0.003, COVER_Y0, zc - 0.030, zc + 0.030, STEEL, bevel=0.001)
        for dz in (-0.018, 0.018):
            small_screw(bm, HINGE_X + 0.022, zc + dz, COVER_Y0 - 0.003)
        lathe(bm, upright(HINGE_X, HINGE_Y, zc - 0.0145), [(0.0062, 0.0), (0.0062, 0.029)], 10, [STEEL], cap_mat=STEEL, start_cap=True)
    return bm


def louver(bm, x0, x1, z0, z1, y, proud=0.006):
    """Downward-facing hood: flush at the top, proud at the bottom, dark underneath."""
    a0, a1 = bm.verts.new((x0, y, z1)), bm.verts.new((x1, y, z1))
    b0, b1 = bm.verts.new((x0, y - proud, z0)), bm.verts.new((x1, y - proud, z0))
    c0, c1 = bm.verts.new((x0, y, z0)), bm.verts.new((x1, y, z0))
    bm.faces.new((a0, b0, b1, a1)).material_index = PAINT
    bm.faces.new((c0, c1, b1, b0)).material_index = DARK
    bm.faces.new((a0, c0, b0)).material_index = PAINT
    bm.faces.new((a1, b1, c1)).material_index = PAINT


def build_indicators():
    bm = new_bm()
    for index, z in enumerate(LED_Z):
        lathe(bm, front(LED_X, Y_SUB - 0.0010, z), [(0.0099, 0.0), (0.0099, 0.0030), (0.0074, 0.0064), (0.0, 0.0078)], 12, [index] * 3)
    return bm


# ------------------------------------------------------------------ labels
PPM = 4096.0                    # atlas pixels per metre
ATLAS_W, ATLAS_H = 2048, 2048
STYLES = {
    "dark": dict(bg="#16181B", fg="#E6E3DA", line="#8A8D90"),
    "light": dict(bg="#D8D6CC", fg="#101113", line="#6A6D70"),
    "caution": dict(bg="#F0A81C", fg="#111214", line="#111214"),
    "hazard": dict(bg="#111214", fg="#F0A81C", line=None),
}


def placards():
    items = []

    def add(key, host, cx, cz, w, h, y, style, texts, flip=False):
        items.append(dict(key=key, host=host, c=(cx, cz), s=(w, h), y=y, style=style, texts=texts, flip=flip))

    add("title", "Body", 0.0, 1.135, 0.40, 0.026, Y_FACE, "dark", [("EPS-2   POWER DISTRIBUTION PANEL", 0.0, 0.0, 0.0125)])
    add("hazard", "Body", 0.0, 0.7975, 0.46, 0.016, Y_FACE, "hazard", [("HIGH VOLTAGE  -  AUTHORIZED SERVICE ONLY", 0.0, 0.0, 0.0095)])
    add("hdr_status", "Body", -0.205, 1.099, 0.12, 0.016, Y_SUB, "dark", [("BUS STATUS", 0.0, 0.0, 0.0088)])
    add("hdr_cb", "Body", 0.085, 1.099, 0.30, 0.016, Y_SUB, "dark", [("CIRCUIT PROTECTION   120 V DC", 0.0, 0.0, 0.0088)])
    for z, text in zip(LED_Z, ("BUS OK", "CAUTION", "FAULT")):
        add("led_" + text, "Body", -0.172, z, 0.078, 0.022, Y_SUB, "light", [(text, 0.0, 0.0, 0.0122)])
    rows = (("CB1 LIFE SUP", "CB2 AVIONICS", "CB3 THERMAL", "CB4 COMMS"), ("CB5 PUMP A", "CB6 PUMP B", "CB7 SCIENCE", "CB8 SPARE"))
    for index, (z, names) in enumerate(zip(BRK_Z, rows)):
        add("cb_row%d" % index, "Body", 0.085, z - 0.048, 0.34, 0.019, Y_SUB, "light", [(name, x - 0.085, 0.0, 0.0088) for name, x in zip(names, BRK_X)])
    for (x, _shell, _mated), text in zip(CONNECTORS, ("J1 PWR IN", "J2 PWR OUT", "J3 DATA A", "J4 DATA B")):
        add("conn_" + text[:2], "Body", x, 0.198, 0.096, 0.021, Y_FACE, "light", [(text, 0.0, 0.0, 0.0110)])
    add("gnd", "Body", GND[0], 0.160, 0.050, 0.018, Y_FACE, "light", [("GND", 0.0, 0.0, 0.0100)])
    add("caution", "Cover", -0.035, 0.600, 0.18, 0.10, COVER_Y0, "caution",
        [("CAUTION", 0.0, 0.026, 0.024), ("120 V DC BUS", 0.0, -0.004, 0.013), ("OPEN CB1-CB8 BEFORE SERVICE", 0.0, -0.027, 0.0090)])
    add("cover_id", "Cover", -0.115, 0.705, 0.17, 0.024, COVER_Y0, "dark", [("SERVICE ACCESS   A7-E2", 0.0, 0.0, 0.0105)])
    table = [("FUSE SCHEDULE", 0.0, 0.040, 0.0125), ("REPLACE WITH SAME RATING ONLY", 0.0, -0.045, 0.0082)]
    for row, (fuse, bus, rating) in enumerate((("F1", "BUS A", "30 A"), ("F2", "BUS B", "30 A"), ("F3", "AUX", "15 A"))):
        dz = 0.016 - row * 0.018
        table += [(fuse, -0.070, dz, 0.0105), (bus, -0.005, dz, 0.0105), (rating, 0.065, dz, 0.0105)]
    add("fuse_table", "Cover", 0.0, 0.500, 0.22, 0.12, COVER_Y1, "light", table, flip=True)     # inside of the cover
    return items


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
    scene = bpy.data.scenes.new("SP_LabelAtlas")
    made = []
    colors = {}
    font = load_label_font()

    def color_mat(hex_color):
        if hex_color not in colors:
            colors[hex_color] = emission_material("SPatlas_" + hex_color.lstrip("#"), hex_color)
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
        for text, dx, dz, size in item["texts"]:
            # Bolder strokes come from overprinting shifted copies. Blender's curve offset is not used:
            # it self-intersects at the sharp apex of "A" and the crossbar disappears.
            # Glyphs are built 100x larger and scaled down so the fill keeps thin parts at millimetre sizes.
            curve = bpy.data.curves.new("SPatlas_text", "FONT")
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
                obj = bpy.data.objects.new("SPatlas_text", curve)
                obj.scale = (0.01, 0.01, 0.01)
                obj.location = (x + w / 2 + dx + ox * shift, y + h / 2 + dz - size * TEXT_DROP + oy * shift, 0.002)
                scene.collection.objects.link(obj)
                made.append(obj)

    for hex_color, faces in quads.items():
        bm = bmesh.new()
        for corners in faces:
            bm.faces.new([bm.verts.new(c) for c in corners])
        mesh = bpy.data.meshes.new("SPatlas_fill")
        bm.to_mesh(mesh)
        bm.free()
        mesh.materials.append(color_mat(hex_color))
        obj = bpy.data.objects.new("SPatlas_fill", mesh)
        scene.collection.objects.link(obj)
        made.extend((obj, mesh))

    camera_data = bpy.data.cameras.new("SPatlas_cam")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = max(ATLAS_W, ATLAS_H) * px
    camera = bpy.data.objects.new("SPatlas_cam", camera_data)
    camera.location = (ATLAS_W * px / 2, ATLAS_H * px / 2, 1.0)
    scene.collection.objects.link(camera)
    scene.camera = camera
    made.extend((camera, camera_data))

    world = bpy.data.worlds.new("SPatlas_world")
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


def build_label_objects(items, material):
    result = {}
    for host in ("Body", "Cover"):
        bm = bmesh.new()
        uv = bm.loops.layers.uv.new("UVMap")
        for item in (p for p in items if p["host"] == host):
            (cx, cz), (w, h) = item["c"], item["s"]
            x, v, pw, ph = item["px"]
            u0, v0, u1, v1 = x / ATLAS_W, v / ATLAS_H, (x + pw) / ATLAS_W, (v + ph) / ATLAS_H
            if item["flip"]:      # faces +Y (read from behind), so the text runs towards -X
                y = item["y"] + 0.0004
                corners = [(cx + w / 2, cz - h / 2, u0, v0), (cx - w / 2, cz - h / 2, u1, v0), (cx - w / 2, cz + h / 2, u1, v1), (cx + w / 2, cz + h / 2, u0, v1)]
            else:
                y = item["y"] - 0.0004
                corners = [(cx - w / 2, cz - h / 2, u0, v0), (cx + w / 2, cz - h / 2, u1, v0), (cx + w / 2, cz + h / 2, u1, v1), (cx - w / 2, cz + h / 2, u0, v1)]
            face = bm.faces.new([bm.verts.new((px_, y, pz)) for px_, pz, _u, _v in corners])
            for loop, (_x, _z, u, vv) in zip(face.loops, corners):
                loop[uv].uv = (u, vv)
        mesh = bpy.data.meshes.new("ServicePanel_Labels" + host)
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
def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def build_render_set(scene):
    collection = bpy.data.collections.new("SP_RenderSet")
    scene.collection.children.link(collection)

    wall_mesh = bpy.data.meshes.new("SP_Wall")
    bm = bmesh.new()
    bm.faces.new([bm.verts.new(p) for p in ((-3, YB + 0.0005, -2), (3, YB + 0.0005, -2), (3, YB + 0.0005, 3.5), (-3, YB + 0.0005, 3.5))])
    bm.to_mesh(wall_mesh)
    bm.free()
    wall_mat = bpy.data.materials.new("SP_RenderWall")
    nodes_on(wall_mat)
    principled(wall_mat).inputs["Base Color"].default_value = hex_rgba("#2A2D31")
    principled(wall_mat).inputs["Roughness"].default_value = 0.62
    principled(wall_mat).inputs["Metallic"].default_value = 0.2
    wall_mesh.materials.append(wall_mat)
    collection.objects.link(bpy.data.objects.new("SP_Wall", wall_mesh))

    def area(name, location, target, power, size, color):
        data = bpy.data.lights.new(name, "AREA")
        data.energy, data.size, data.color = power, size, color
        obj = bpy.data.objects.new(name, data)
        obj.location = location
        look_at(obj, target)
        collection.objects.link(obj)

    area("SP_Key", (-1.5, -2.3, 2.0), (0.0, 0.0, 0.65), 150.0, 1.4, (0.94, 0.97, 1.0))
    area("SP_Fill", (1.9, -1.9, 0.5), (0.0, 0.0, 0.55), 32.0, 2.2, (1.0, 0.95, 0.88))
    area("SP_Top", (0.2, -0.75, 2.5), (0.0, -0.05, 0.9), 55.0, 0.9, (1.0, 1.0, 1.0))

    cameras = {}
    hero_data = bpy.data.cameras.new("SP_CamHero")
    hero_data.lens = 70.0
    hero = bpy.data.objects.new("SP_CamHero", hero_data)
    hero.location = (-1.15, -2.45, 1.02)
    look_at(hero, (0.0, 0.0, 0.60))
    collection.objects.link(hero)
    cameras["hero"] = hero

    front_data = bpy.data.cameras.new("SP_CamFront")
    front_data.type = "ORTHO"
    front_data.ortho_scale = 1.42
    front_cam = bpy.data.objects.new("SP_CamFront", front_data)
    front_cam.location = (0.0, -3.0, 0.60)
    look_at(front_cam, (0.0, 0.0, 0.60))
    collection.objects.link(front_cam)
    cameras["front"] = front_cam

    open_data = bpy.data.cameras.new("SP_CamOpen")
    open_data.lens = 60.0
    open_cam = bpy.data.objects.new("SP_CamOpen", open_data)
    open_cam.location = (0.95, -2.15, 0.95)
    look_at(open_cam, (-0.05, 0.0, 0.58))
    collection.objects.link(open_cam)
    cameras["open"] = open_cam

    detail_data = bpy.data.cameras.new("SP_CamDetail")
    detail_data.lens = 55.0
    detail = bpy.data.objects.new("SP_CamDetail", detail_data)
    detail.location = (-0.62, -0.95, 1.06)
    look_at(detail, (-0.02, 0.0, 0.93))
    collection.objects.link(detail)
    cameras["detail"] = detail

    world = bpy.data.worlds.new("SP_World")
    nodes_on(world)
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.020, 0.023, 0.028, 1.0)
    scene.world = world
    scene.camera = hero
    for look in ("AgX - Medium High Contrast", "Medium High Contrast"):
        try:
            scene.view_settings.look = look
            break
        except TypeError:
            continue
    return cameras


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
    mat = bpy.data.materials.new("SP_WireOverlay")
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
        render_label_atlas(items, os.path.join(TEX_DIR, "ServicePanel_Labels.png"))
        log("label atlas rendered")
        return
    full = STAGE == "full"
    surface = [make_proc_material(k) if full else make_flat_material(k) for k in MAT_KEYS]
    if full:
        PROC.extend(surface)
    lamps = [make_indicator_material(*spec) for spec in INDICATORS]

    body = finish(build_body(), "Body", surface)
    cover = finish(build_cover(), "ServiceCover", surface, origin=COVER_ORIGIN)
    indicators = finish(build_indicators(), "StatusIndicators", lamps)
    indicators.data.uv_layers.new(name="UVMap")      # every exported mesh carries UV0 (and so tangents) in Unity
    parts = [body, cover, indicators]
    report = {"blender": bpy.app.version_string, "stage": STAGE, "cycles_device": device}

    if full:
        items = placards()
        pack_atlas(items)
        labels_path = os.path.join(TEX_DIR, "ServicePanel_Labels.png")
        render_label_atlas(items, labels_path)
        log("label atlas rendered")

        unwrap([body, cover])
        color = new_image("SPbake_color", False)
        metal = new_image("SPbake_metal", True)
        rough = new_image("SPbake_rough", True)
        occlusion = new_image("SPbake_ao", True)
        together = [(body, []), (cover, [])]
        bake_pass("color", color, together, 48)
        bake_pass("metal", metal, together, 48)
        bake_pass("rough", rough, together, 48)
        # Occlusion: each part on its own, so opening the cover never reveals baked-in shadow.
        bake_pass("ao", occlusion, [(body, [cover]), (cover, [body, indicators])], 96)

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
        metal_smooth = bpy.data.images.new("SPbake_ms", TEX, TEX, alpha=True)
        metal_smooth.colorspace_settings.name = "Non-Color"
        metal_smooth.alpha_mode = "CHANNEL_PACKED"
        metal_smooth.pixels.foreach_set(packed)

        paths = {
            "BaseColor": save_image(color, "ServicePanel_BaseColor.png"),
            "Metallic": save_image(metal, "ServicePanel_Metallic.png"),
            "Roughness": save_image(rough, "ServicePanel_Roughness.png"),
            "Occlusion": save_image(occlusion, "ServicePanel_Occlusion.png"),
            "MetallicSmoothness": save_image(metal_smooth, "ServicePanel_MetallicSmoothness.png"),
            "Labels": labels_path,
        }
        for image in (color, metal, rough, occlusion, metal_smooth):
            bpy.data.images.remove(image)

        final = bpy.data.materials.new("SP_ServicePanel")
        nodes_on(final)
        tree = final.node_tree
        for index, (key, socket, non_color) in enumerate((("BaseColor", "Base Color", False), ("Metallic", "Metallic", True), ("Roughness", "Roughness", True))):
            node = tree.nodes.new("ShaderNodeTexImage")
            node.image = load_image(paths[key], non_color)
            node.location = (-520, 320 - index * 300)
            tree.links.new(node.outputs["Color"], principled(final).inputs[socket])
        label_mat = bpy.data.materials.new("SP_Labels")
        nodes_on(label_mat)
        label_node = label_mat.node_tree.nodes.new("ShaderNodeTexImage")
        label_node.image = load_image(labels_path, False)
        label_node.location = (-520, 320)
        label_mat.node_tree.links.new(label_node.outputs["Color"], principled(label_mat).inputs["Base Color"])
        principled(label_mat).inputs["Roughness"].default_value = 0.55

        for obj in (body, cover):
            obj.data.materials.clear()
            obj.data.materials.append(final)
            for polygon in obj.data.polygons:
                polygon.material_index = 0
        for mat in PROC:
            bpy.data.materials.remove(mat)
        label_objects = build_label_objects(items, label_mat)
        for host, target in (("Body", body), ("Cover", cover)):
            select_only([label_objects[host], target], active=target)
            bpy.ops.object.join()
        report["textures"] = {key: os.path.relpath(value, OUT).replace("\\", "/") for key, value in paths.items()}
        report["texture_size"] = TEX
        report["label_font"] = LABEL_FONT["name"]
    else:
        for obj in (body, cover):
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
        "materials": {obj.name: [m.name for m in obj.data.materials] for obj in parts},
    })
    log("dimensions", report["dimensions_m"], "triangles", triangles, "total", report["triangles_total"])

    cameras = build_render_set(scene)
    if full:
        render(scene, cameras["hero"], "ServicePanel_hero.png", (1500, 2000), SAMPLES)
        render(scene, cameras["front"], "ServicePanel_front.png", (1200, 2000), SAMPLES)
        render(scene, cameras["detail"], "ServicePanel_detail.png", (1800, 1200), SAMPLES)
        cover.rotation_euler = (0.0, 0.0, radians(-100.0))
        render(scene, cameras["open"], "ServicePanel_open.png", (1500, 2000), SAMPLES)
        cover.rotation_euler = (0.0, 0.0, 0.0)
        bpy.context.view_layer.material_override = wire_material()
        render(scene, cameras["hero"], "ServicePanel_wire.png", (1500, 2000), 48)
        bpy.context.view_layer.material_override = None
        scene.camera = cameras["hero"]

        blend_path = os.path.join(OUT, "ServicePanel.blend")
        bpy.context.preferences.filepaths.save_version = 0
        bpy.ops.wm.save_as_mainfile(filepath=blend_path)
        bpy.ops.file.make_paths_relative()
        bpy.ops.wm.save_mainfile()
        select_only(parts, active=body)
        bpy.ops.export_scene.fbx(
            filepath=os.path.join(OUT, "ServicePanel.fbx"), use_selection=True, object_types={"MESH"},
            apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
            bake_space_transform=True, use_mesh_modifiers=True, mesh_smooth_type="OFF", use_triangles=True,
            add_leaf_bones=False, bake_anim=False, path_mode="RELATIVE", embed_textures=False)
        log("saved .blend and exported .fbx")
    else:
        render(scene, cameras["hero"], "geometry_hero.png", (900, 1200), 48)
        cover.rotation_euler = (0.0, 0.0, radians(-100.0))
        render(scene, cameras["open"], "geometry_open.png", (900, 1200), 48)
        cover.rotation_euler = (0.0, 0.0, 0.0)

    with open(os.path.join(OUT, "ServicePanel_report.json" if full else "geometry_report.json"), "w", encoding="utf-8") as handle:
        json.dump(report, handle, indent=2)
    log("done")


main()
