"""
OUT OF REACH - radio desk kit (RADIO-DESK-V1): the AURORA-7 communications desk, an original asset built from
code in Blender 5.3. A standalone kit for root to place in the cockpit: graphite desk, cream rack units, sloped
control shelf with physical toggles and amber indicators, transceiver with knobs and signal meter, monitor
with a passive screen quad, speaker grille, handset with coiled cord on its cradle, warm desk lamp and a small
restrained seat (separate object, occupied and stowed positions documented).

    blender -b --factory-startup --python build_radio_desk.py -- --stage full --out <folder>
    blender -b --factory-startup --python build_radio_desk.py -- --stage geometry --out <folder>

Unity frame (Codex's 16:28 UTC brief): origin = floor under the desk centre, X = width (1.60 m), Y = up
(worktop at 0.75 m), Z = depth (0.65 m); the seated player is on the -Z side facing +Z. Parts are modelled in
a design frame (x = Unity X, y = Unity Z, z = Unity Y) and turned 180 degrees about Z on the way into the file,
which gives the Blender frame of TERM-V1 and TERMINAL-ART-V2: Blender = (-X, -Z, Y) of Unity. The FBX uses
the same export settings as those two deliveries. The shared helpers come from the ARS-1 and terminal scripts.
"""

import json
import math
import os
import sys
import time
from math import atan2, cos, pi, radians, sin

import bmesh
import bpy
from mathutils import Matrix, Vector

T0 = time.time()


def log(*parts):
    print("[desk %6.1fs]" % (time.time() - T0), *parts, flush=True)


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
SAMPLES = int(arg("--samples", 160))      # beauty render samples
TEX_DIR = os.path.join(OUT, "textures")
RENDER_DIR = os.path.join(OUT, "renders")
for folder in (OUT, TEX_DIR, RENDER_DIR):
    os.makedirs(folder, exist_ok=True)

NAME = "RadioDesk"
R180 = Matrix.Rotation(pi, 4, "Z")        # design frame -> Blender frame (TERM-V1 convention)


def unity_to_blender(p):
    return Vector((-p[0], -p[2], p[1]))


def design(p):
    """Unity (X, Y, Z) -> design frame (x, y, z) = (X, Z, Y)."""
    return Vector((p[0], p[2], p[1]))


# ------------------------------------------------------------------ fixed interface (Codex 16:28 UTC)
DESK_HX, DESK_Y0, DESK_Y1, TOP_Z = 0.800, -0.325, 0.325, 0.750      # width/2, depth front/back (Unity Z), worktop height
SCREEN_C, SCREEN_W, SCREEN_H = (0.20, 1.10, 0.12), 0.44, 0.26        # Unity centre, size; faces -Z
ANCHORS = {                                                          # Unity metres, identity rotation in the file
    "SeatEntry": (0.0, 0.0, -1.00),
    "SeatedCamera": (0.0, 1.20, -0.70),
    "HandsetRest": (-0.52, 0.83, -0.04),
    "HandsetGrip": (-0.52, 0.86, -0.04),
    "ScreenCenter": SCREEN_C,
}

# ------------------------------------------------------------------ layout (design frame: x, y = Unity Z, z = Unity Y)
PED_X0 = 0.470                            # pedestals from |x| 0.47 to 0.80 (leg room 0.94 m)
SHELF = dict(x0=-0.330, x1=0.560, y0=-0.060, z0=0.765, y1=0.080, z1=0.830, back=0.105)
SHELF_ANGLE = atan2(SHELF["z1"] - SHELF["z0"], SHELF["y1"] - SHELF["y0"])
M1 = dict(x0=-0.725, x1=-0.345, y0=0.080, y1=0.320, z1=1.050)        # handset unit
M3 = dict(x0=-0.330, x1=-0.070, y0=0.080, y1=0.320, z1=1.190)        # transceiver
M2 = dict(x0=-0.065, x1=0.465, y0=0.105, y1=0.320, z1=1.305)         # screen unit
M4 = dict(x0=0.475, x1=0.725, y0=0.100, y1=0.320, z1=1.190)          # speaker
TOGGLES = ((-0.220, "POWER", True), (-0.100, "TX", False), (0.020, "MON", False), (0.140, "LAMP", False))
INDICATORS = ((-0.220, "PWR"), (-0.100, "TX"))
PTT_X, DISTRESS_X = 0.300, 0.440
KNOBS = (("Volume", -0.265, 0.895, 0.0105, "VOLUME"), ("Squelch", -0.135, 0.895, 0.0105, "SQUELCH"),
         ("Channel", -0.200, 1.005, 0.0150, "CHANNEL"))
DIAL = (-0.200, 1.1075, 0.110, 0.085)    # signal meter window: centre x, z, width, height
LAMP_BASE = Vector((0.640, 0.230, M4["z1"]))
LAMP_ELBOW = Vector((0.610, 0.150, 1.440))
LAMP_HEAD = Vector((0.500, 0.010, 1.345))
LAMP_TARGET = Vector((0.250, -0.090, TOP_Z))
SEAT_Y = -0.660                           # seat post (occupied); stowed is +0.22 m towards the desk
SEAT_STOW = 0.220

# ------------------------------------------------------------------ materials
MAT_KEYS = ["paint", "graphite", "alu", "steel", "rubber", "dark", "glass", "keys", "stripe", "white", "desk", "fabric", "strap", "red"]
PAINT, GRAPHITE, ALU, STEEL, RUBBER, DARK, GLASS, KEYCAP, STRIPE, WHITE, DESK, FABRIC, STRAP, RED = range(14)
MAT_SPEC = {
    #             base sRGB  metal rough  wear  worn sRGB  worn metal  dirt
    "paint":     ("#D6CFBC", 0.00, 0.48, 0.55, "#9C9EA0", 1.0, 0.60),   # cream rack units (as TERMINAL-ART-V2)
    "graphite":  ("#2B2B2C", 0.25, 0.50, 0.45, "#6B6E72", 0.8, 0.45),   # A7_PaintGraphite
    "alu":       ("#C3C6C9", 1.00, 0.27, 0.00, "#C3C6C9", 1.0, 0.35),   # A7_Aluminum
    "steel":     ("#A7A9AC", 1.00, 0.33, 0.00, "#A7A9AC", 1.0, 0.45),
    "rubber":    ("#1B1B1C", 0.00, 0.80, 0.00, "#1B1B1C", 0.0, 0.30),   # A7_Rubber
    "dark":      ("#0E0F10", 0.00, 0.58, 0.00, "#0E0F10", 0.0, 0.25),
    "glass":     ("#0A0C0D", 0.00, 0.30, 0.00, "#0A0C0D", 0.0, 0.10),
    "keys":      ("#232528", 0.30, 0.46, 0.50, "#7A7E83", 0.9, 0.35),   # A7_AnodizedBlack
    "stripe":    ("#C4502A", 0.00, 0.50, 0.40, "#9C9EA0", 1.0, 0.50),
    "white":     ("#E6E3DA", 0.00, 0.50, 0.00, "#E6E3DA", 0.0, 0.20),
    "desk":      ("#2B2B2C", 0.30, 0.46, 0.95, "#74777B", 0.9, 0.70),   # scratched graphite worktop
    "fabric":    ("#2C313B", 0.00, 0.86, 0.10, "#4B505A", 0.0, 0.55),   # A7_FabricDark
    "strap":     ("#4B4F53", 0.00, 0.80, 0.10, "#5E6266", 0.0, 0.40),   # A7_FabricStrap
    "red":       ("#B32A22", 0.00, 0.45, 0.35, "#9C9EA0", 1.0, 0.40),   # A7_PaintRed: danger only (distress cover)
}
SOCK = {}
PROC = []

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
    mat = bpy.data.materials.new("RD_" + key)
    nodes_on(mat)
    node = principled(mat)
    node.inputs["Base Color"].default_value = hex_rgba(base_hex)
    node.inputs["Metallic"].default_value = metal
    node.inputs["Roughness"].default_value = rough
    return mat


def make_proc_material(key):
    """Source material with restrained procedural wear; only used as the bake source."""
    base_hex, metal, rough, wear, wear_hex, wear_metal, dirt = MAT_SPEC[key]
    mat = bpy.data.materials.new("RDsrc_" + key)
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
    bmesh.ops.transform(bm, matrix=R180, verts=list(bm.verts))      # design frame -> TERM-V1 frame
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



# ------------------------------------------------------------------ helpers (design frame)
RXD = Matrix.Rotation(pi, 4, "X")                   # local +Z -> world -Z
RYM90 = Matrix.Rotation(radians(-90.0), 4, "Y")     # local +Z -> world -X
CONTACTS = {}                                       # anchor name -> (design point, design direction)


def rr_points(hx, hz, r, segs, cx=0.0, cz=0.0):
    r = max(0.0008, min(r, hx - 1e-4, hz - 1e-4))
    pts = []
    for qx, qz, a0 in ((hx - r, hz - r, 0.0), (-(hx - r), hz - r, 90.0), (-(hx - r), -(hz - r), 180.0), (hx - r, -(hz - r), 270.0)):
        for k in range(segs + 1):
            a = radians(a0 + 90.0 * k / segs)
            pts.append((cx + qx + r * cos(a), cz + qz + r * sin(a)))
    return pts


def rr_loft(bm, specs, mats, segs=4, closed=False, caps=(None, None), fix_normals=True, center=(0.0, 0.0)):
    """Joins rounded-rectangle loops (hx, hz, r, y) around center (x, z); band i gets mats[i]."""
    loops = [[bm.verts.new((x, y, z)) for x, z in rr_points(hx, hz, r, segs, center[0], center[1])] for hx, hz, r, y in specs]
    faces = []
    count = len(loops) if closed else len(loops) - 1
    for i in range(count):
        a, b = loops[i], loops[(i + 1) % len(loops)]
        mat = mats[i] if isinstance(mats, (list, tuple)) else mats
        for k in range(len(a)):
            k1 = (k + 1) % len(a)
            face = bm.faces.new((a[k], a[k1], b[k1], b[k]))
            face.material_index = mat
            faces.append(face)
    for index, cap in ((0, caps[0]), (-1, caps[1])):
        if cap is not None:
            face = bm.faces.new(tuple(loops[index]))
            face.material_index = cap
            faces.append(face)
    if fix_normals:
        bmesh.ops.recalc_face_normals(bm, faces=faces)
    return faces


def orient(faces, direction):
    for face in faces:
        face.normal_update()
        if face.normal.dot(direction) < 0.0:
            face.normal_flip()


def knurled(bm, matrix, r_out, r_in, z0, z1, ridges, mat, cap_mat=None, start_cap=False):
    count = 2 * ridges
    pts = [((r_out if k % 2 == 0 else r_in), 2 * pi * k / count) for k in range(count)]
    low = [bm.verts.new(matrix @ Vector((r * cos(a), r * sin(a), z0))) for r, a in pts]
    high = [bm.verts.new(matrix @ Vector((r * cos(a), r * sin(a), z1))) for r, a in pts]
    for k in range(count):
        k1 = (k + 1) % count
        bm.faces.new((low[k], low[k1], high[k1], high[k])).material_index = mat
    if cap_mat is not None:
        bm.faces.new(tuple(high)).material_index = cap_mat
    if start_cap:
        bm.faces.new(tuple(reversed(low))).material_index = mat


def bolt_m(bm, matrix, r=0.0055, h=0.0040, washer=0.0, hex_head=False):
    if washer:
        lathe(bm, matrix, [(washer, 0.0), (washer, 0.0010)], 20, [STEEL], cap_mat=STEEL)
        matrix = matrix @ Matrix.Translation((0.0, 0.0, 0.0010))
    if hex_head:
        lathe(bm, matrix, [(r, 0.0), (r, h), (r * 0.82, h + 0.0008)], 6, [STEEL, STEEL], cap_mat=STEEL, smooth=False)
    else:
        lathe(bm, matrix, [(r, 0.0), (r, h), (r * 0.55, h), (r * 0.55, h - 0.0020)], 12, [STEEL, STEEL, DARK], cap_mat=DARK)


def quarter_turn_m(bm, matrix, angle):
    lathe(bm, matrix, [(0.0100, 0.0), (0.0100, 0.0012), (0.0075, 0.0012), (0.0075, 0.0034), (0.0062, 0.0044)], 14, [STEEL] * 4, cap_mat=STEEL)
    slot = matrix @ Matrix.Rotation(angle, 4, "Z") @ Matrix.Translation((0.0, 0.0, 0.0045)) @ Matrix.Diagonal((0.0016, 0.0105, 0.0004, 1.0))
    for face in {f for v in bmesh.ops.create_cube(bm, size=1.0, matrix=slot)["verts"] for f in v.link_faces}:
        face.material_index = DARK


def prism_x(bm, x0, x1, poly, mat):
    """Extrudes a closed (y, z) polygon along x; caps on both ends."""
    a = [bm.verts.new((x0, y, z)) for y, z in poly]
    b = [bm.verts.new((x1, y, z)) for y, z in poly]
    faces = []
    for k in range(len(poly)):
        k1 = (k + 1) % len(poly)
        faces.append(bm.faces.new((a[k], a[k1], b[k1], b[k])))
    faces.append(bm.faces.new(tuple(a)))
    faces.append(bm.faces.new(tuple(reversed(b))))
    for face in faces:
        face.material_index = mat
    bmesh.ops.recalc_face_normals(bm, faces=faces)


def aim(p0, p1):
    """Frame at p0 whose local +Z points at p1."""
    d = Vector(p1) - Vector(p0)
    return Matrix.Translation(p0) @ d.to_track_quat("Z", "Y").to_matrix().to_4x4()


def tube(bm, p0, p1, r, mat, segs=16, caps=True):
    length = (Vector(p1) - Vector(p0)).length
    prof = [(0.0, 0.0), (r, 0.0), (r, length), (0.0, length)] if caps else [(r, 0.0), (r, length)]
    lathe(bm, aim(p0, p1), prof, segs, [mat] * (len(prof) - 1))


def shelf_frame(x, s, lift=0.0):
    """Frame on the sloped control shelf, s = 0 at its front edge, 1 at its back; local +Z = surface normal."""
    y = SHELF["y0"] + s * (SHELF["y1"] - SHELF["y0"])
    z = SHELF["z0"] + s * (SHELF["z1"] - SHELF["z0"])
    return Matrix.Translation((x, y, z)) @ Matrix.Rotation(SHELF_ANGLE, 4, "X") @ Matrix.Translation((0.0, 0.0, lift))


def contact(name, matrix, local=(0.0, 0.0, 0.0), direction=(0.0, 0.0, 1.0)):
    p = matrix @ Vector(local)
    d = (matrix.to_3x3() @ Vector(direction)).normalized()
    CONTACTS[name] = (p, d)


# ------------------------------------------------------------------ desk
def desk(bm):
    box(bm, -DESK_HX, DESK_HX, DESK_Y0, DESK_Y1, TOP_Z - 0.038, TOP_Z, DESK, bevel=0.004, segs=2)
    box(bm, -DESK_HX - 0.004, DESK_HX + 0.004, DESK_Y0 - 0.012, DESK_Y0 + 0.010, TOP_Z - 0.044, TOP_Z + 0.004, RUBBER, bevel=0.007, segs=3)
    for side in (-1.0, 1.0):
        x0, x1 = sorted((side * PED_X0, side * DESK_HX))
        box(bm, x0 + 0.004, x1 - 0.004, -0.300, 0.300, 0.060, TOP_Z - 0.038, PAINT, bevel=0.004, segs=2)
        box(bm, x0 + 0.020, x1 - 0.020, -0.285, 0.290, 0.000, 0.062, GRAPHITE, bevel=0.003, segs=1)
        for z0, z1, label in ((0.385, 0.690, None), (0.080, 0.370, None)):
            box(bm, x0 + 0.018, x1 - 0.018, -0.306, -0.298, z0 + 0.006, z1 - 0.006, PAINT, bevel=0.003, segs=2)
            zc = z1 - 0.045
            for dx in (-0.075, 0.075):
                box(bm, (x0 + x1) / 2 + dx - 0.007, (x0 + x1) / 2 + dx + 0.007, -0.330, -0.306, zc - 0.006, zc + 0.006, ALU, bevel=0.002, segs=1)
            box(bm, (x0 + x1) / 2 - 0.082, (x0 + x1) / 2 + 0.082, -0.336, -0.326, zc - 0.007, zc + 0.007, ALU, bevel=0.004, segs=2)
            for qx in (x0 + 0.034, x1 - 0.034):
                quarter_turn_m(bm, front(qx, -0.306, z0 + 0.030), 0.6)
    box(bm, -PED_X0, PED_X0, 0.282, 0.300, 0.120, TOP_Z - 0.038, GRAPHITE, bevel=0.003, segs=1)
    for x in (-0.300, 0.300):
        box(bm, x - 0.020, x + 0.020, 0.200, 0.282, TOP_Z - 0.090, TOP_Z - 0.038, GRAPHITE, bevel=0.003, segs=1)


# ------------------------------------------------------------------ console
def module(bm, m, z0=TOP_Z):
    box(bm, m["x0"], m["x1"], m["y0"], m["y1"], z0, m["z1"], PAINT, bevel=0.005, segs=2)
    for x in (m["x0"] + 0.014, m["x1"] - 0.014):
        for z in (z0 + 0.022, m["z1"] - 0.016):
            lathe(bm, front(x, m["y0"], z), [(0.0040, -0.0001), (0.0040, 0.0011), (0.0024, 0.0016)], 12, [STEEL, STEEL], cap_mat=DARK)


def shelf(bm):
    s = SHELF
    prism_x(bm, s["x0"], s["x1"], [(s["y0"], TOP_Z), (s["y0"], s["z0"]), (s["y1"], s["z1"]), (s["back"], s["z1"]), (s["back"], TOP_Z)], GRAPHITE)
    for x, name, guarded in TOGGLES:
        m = shelf_frame(x, 0.50, 0.0)
        lathe(bm, m, [(0.0095, -0.0002), (0.0095, 0.0008)], 6, [STEEL], cap_mat=STEEL, smooth=False)
        lathe(bm, m, [(0.0062, 0.0008), (0.0062, 0.0042), (0.0048, 0.0050)], 6, [STEEL, STEEL], cap_mat=STEEL, smooth=False)
        lathe(bm, m, [(0.0040, 0.0050), (0.0040, 0.0105), (0.0030, 0.0110)], 14, [STEEL, STEEL], cap_mat=STEEL)
        lever = m @ Matrix.Translation((0.0, 0.0, 0.0105)) @ Matrix.Rotation(radians(-18.0), 4, "X")
        lathe(bm, lever, [(0.0, -0.001), (0.0027, -0.001), (0.0021, 0.021), (0.0034, 0.0225), (0.0034, 0.0255), (0.0, 0.0270)], 14,
              [STEEL, STEEL, STEEL, STEEL, STEEL])
        contact("ControlContact_" + {"POWER": "Power", "TX": "Transmit", "MON": "Monitor", "LAMP": "Lamp"}[name], lever, (0.0, 0.0, 0.027))
        if guarded:
            for side in (-1.0, 1.0):
                box_m(bm, m @ Matrix.Translation((side * 0.0135, 0.0, 0.013)), (0.0030, 0.034, 0.026), ALU, bevel=0.0010, segs=2)
    for x, name in INDICATORS:
        m = shelf_frame(x, 0.84, 0.0)
        lathe(bm, m, [(0.0070, -0.0002), (0.0070, 0.0020), (0.0055, 0.0024)], 16, [STEEL, STEEL], cap_mat=DARK)
    m = shelf_frame(PTT_X, 0.50, 0.0)
    lathe(bm, m, [(0.0175, -0.0002), (0.0175, 0.0030), (0.0140, 0.0036)], 6, [STEEL, STEEL], cap_mat=DARK, smooth=False)
    lathe(bm, m, [(0.0, 0.0030), (0.0120, 0.0030), (0.0120, 0.0090), (0.0100, 0.0112), (0.0, 0.0118)], 24, [KEYCAP] * 4)
    contact("ControlContact_PTT", m, (0.0, 0.0, 0.0118))
    m = shelf_frame(DISTRESS_X, 0.50, 0.0)
    box_m(bm, m @ Matrix.Translation((0.0, 0.0, 0.0020)), (0.036, 0.046, 0.0045), DARK, bevel=0.0015, segs=1)
    lathe(bm, m, [(0.0040, 0.0040), (0.0040, 0.0100), (0.0030, 0.0105)], 12, [STEEL, STEEL], cap_mat=STEEL)
    box_m(bm, m @ Matrix.Translation((0.0, -0.001, 0.0130)), (0.034, 0.042, 0.0150), RED, bevel=0.0022, segs=2)
    lathe(bm, m @ Matrix.Translation((-0.017, 0.021, 0.0055)) @ RY90, [(0.0, 0.0), (0.0035, 0.0), (0.0035, 0.034), (0.0, 0.034)], 12, [STEEL] * 3)
    contact("ControlContact_Distress", m, (0.0, -0.001, 0.0205))


def screen_unit(bm):
    cx, cz = SCREEN_C[0], SCREEN_C[1]
    hx, hz = SCREEN_W / 2, SCREEN_H / 2
    yg = SCREEN_C[2]                                 # screen plane (design y)
    yf = M2["y0"]                                    # module front
    mask = rr_loft(bm, [(hx, hz, 0.0005, yg + 0.0002), (hx + 0.006, hz + 0.006, 0.003, yg + 0.0002)], [GLASS], fix_normals=False, center=(cx, cz))
    orient(mask, Vector((0.0, -1.0, 0.0)))
    g0x, g0z = hx + 0.006, hz + 0.006
    g1x, g1z = g0x + 0.006, g0z + 0.006
    rr_loft(bm, [(g0x, g0z, 0.003, yg - 0.004), (g1x, g1z, 0.005, yg - 0.004), (g1x, g1z, 0.005, yg + 0.004), (g0x, g0z, 0.003, yg + 0.004)], RUBBER, closed=True, center=(cx, cz))
    c1x, c1z = g1x + 0.012, g1z + 0.012
    rr_loft(bm, [(g1x, g1z, 0.005, yg - 0.004), (c1x, c1z, 0.007, yf), (c1x, c1z, 0.007, yg + 0.006), (g1x, g1z, 0.005, yg + 0.006)], ALU, closed=True, center=(cx, cz))
    ox, oz_top = (M2["x1"] - M2["x0"]) / 2, M2["z1"] - cz
    oz = min(oz_top, cz - 0.905)
    rr_loft(bm, [(c1x, c1z, 0.007, yf), (ox, oz, 0.006, yf), (ox, oz, 0.006, yg + 0.006), (c1x, c1z, 0.007, yg + 0.006)], PAINT, closed=True, center=(cx, cz))
    box(bm, M2["x0"], M2["x1"], yg + 0.005, M2["y1"], cz - oz, M2["z1"], PAINT, bevel=0.005, segs=2)
    box(bm, M2["x0"], M2["x1"], yf, M2["y1"], TOP_Z, cz - oz, PAINT, bevel=0.005, segs=2)
    for x in (M2["x0"] + 0.016, M2["x1"] - 0.016):
        for z in (cz - oz + 0.016, cz + oz - 0.016):
            lathe(bm, front(x, yf, z), [(0.0040, -0.0001), (0.0040, 0.0011), (0.0024, 0.0016)], 12, [STEEL, STEEL], cap_mat=DARK)


def transceiver(bm):
    module(bm, M3)
    y = M3["y0"]
    for name, x, z, r, label in KNOBS:
        m = front(x, y, z)
        lathe(bm, m, [(r + 0.0035, -0.0001), (r + 0.0035, 0.0010)], 24, [DARK], cap_mat=DARK)
        knurled(bm, m, r, r * 0.93, 0.0010, 0.0150 if name == "Channel" else 0.0130, 22, KEYCAP, cap_mat=KEYCAP)
        top = 0.0150 if name == "Channel" else 0.0130
        lathe(bm, m, [(r * 0.93, top), (r * 0.80, top + 0.0022)], 22, [KEYCAP], cap_mat=KEYCAP)
        box_m(bm, m @ Matrix.Translation((0.0, r * 0.50, top + 0.0023)), (0.0014, r * 0.70, 0.0005), WHITE)
        contact("ControlContact_" + name, m, (0.0, 0.0, top + 0.0023))
    box(bm, DIAL[0] - DIAL[2] / 2, DIAL[0] + DIAL[2] / 2, y - 0.002, y + 0.004, DIAL[1] - DIAL[3] / 2, DIAL[1] + DIAL[3] / 2, DARK, bevel=0.003, segs=1)


def handset_unit(bm):
    module(bm, M1)
    lathe(bm, front(-0.450, M1["y0"], 0.820), [(0.0130, -0.0002), (0.0130, 0.0030), (0.0085, 0.0040), (0.0060, 0.0010), (0.0, 0.0010)], 20, [RUBBER] * 4)


def speaker(bm):
    module(bm, M4)
    y = M4["y0"]
    xc = (M4["x0"] + M4["x1"]) / 2
    box(bm, xc - 0.095, xc + 0.095, y - 0.003, y + 0.002, 0.805, 1.120, GRAPHITE, bevel=0.003, segs=2)
    for k in range(13):
        z = 0.825 + 0.0225 * k
        box(bm, xc - 0.080, xc + 0.080, y - 0.0035, y - 0.0025, z, z + 0.0085, DARK, bevel=0.0, segs=1)


def cradle(bm):
    box(bm, -0.645, -0.395, -0.080, 0.000, TOP_Z, TOP_Z + 0.024, PAINT, bevel=0.006, segs=2)
    for x in (-0.600, -0.440):
        box(bm, x - 0.022, x + 0.022, -0.072, -0.008, TOP_Z + 0.022, TOP_Z + 0.050, GRAPHITE, bevel=0.006, segs=2)
        box(bm, x - 0.012, x + 0.012, -0.056, -0.024, TOP_Z + 0.048, TOP_Z + 0.056, RUBBER, bevel=0.003, segs=1)
    lathe(bm, upright(-0.520, -0.040, TOP_Z + 0.024), [(0.0, 0.0), (0.0055, 0.0), (0.0055, 0.0120), (0.0, 0.0140)], 14, [STEEL] * 3)


def lamp(bm):
    lathe(bm, upright(*LAMP_BASE), [(0.0, 0.0), (0.042, 0.0), (0.042, 0.010), (0.030, 0.018), (0.0, 0.018)], 28, [GRAPHITE] * 4)
    hinge = LAMP_BASE + Vector((0.0, 0.0, 0.030))
    lathe(bm, upright(*LAMP_BASE), [(0.012, 0.018), (0.012, 0.030), (0.0, 0.030)], 16, [ALU, ALU])
    for offset in (-0.010, 0.010):
        shift = Vector((offset, 0.0, 0.0))
        tube(bm, hinge + shift, LAMP_ELBOW + shift, 0.0050, ALU, segs=12)
    knurled(bm, aim(LAMP_ELBOW - Vector((0.020, 0.0, 0.0)), LAMP_ELBOW + Vector((0.020, 0.0, 0.0))), 0.0115, 0.0105, 0.0, 0.040, 14, KEYCAP, cap_mat=KEYCAP, start_cap=True)
    tube(bm, LAMP_ELBOW, LAMP_HEAD, 0.0060, ALU, segs=12)
    head = aim(LAMP_HEAD, LAMP_TARGET)
    lathe(bm, head, [(0.0, -0.030), (0.020, -0.030), (0.026, -0.010), (0.046, 0.026), (0.048, 0.030), (0.043, 0.030), (0.040, 0.022), (0.0, 0.022)],
          28, [PAINT, PAINT, PAINT, PAINT, DARK, DARK, DARK])
    for side in (-1.0, 1.0):
        box_m(bm, head @ Matrix.Translation((side * 0.030, 0.0, -0.022)), (0.006, 0.014, 0.014), GRAPHITE, bevel=0.002, segs=1)
    CONTACTS["LampEmitter"] = (head @ Vector((0.0, 0.0, 0.024)), (head.to_3x3() @ Vector((0.0, 0.0, 1.0))).normalized())


def build_body():
    bm = new_bm()
    desk(bm)
    shelf(bm)
    handset_unit(bm)
    transceiver(bm)
    screen_unit(bm)
    speaker(bm)
    cradle(bm)
    lamp(bm)
    return bm


# ------------------------------------------------------------------ separate objects
def build_handset():
    """Handset resting on the cradle along X; the grip is the top of the handle."""
    bm = new_bm()
    rest = design(ANCHORS["HandsetRest"])
    box(bm, -0.582, -0.458, rest.y - 0.016, rest.y + 0.016, rest.z + 0.008, rest.z + 0.030, GRAPHITE, bevel=0.009, segs=3)
    for x, length in ((-0.600, 0.026), (-0.440, 0.030)):
        m = Matrix.Translation((x, rest.y, rest.z + 0.020)) @ RXD
        lathe(bm, m, [(0.0, -0.004), (0.016, -0.004), (0.024, 0.010), (0.026, length - 0.006), (0.024, length), (0.0, length)], 24,
              [GRAPHITE, GRAPHITE, GRAPHITE, GRAPHITE, DARK])
    for k in range(5):
        a = 2 * pi * k / 5
        box_m(bm, Matrix.Translation((-0.600 + 0.010 * cos(a), rest.y + 0.010 * sin(a), rest.z - 0.0105)), (0.003, 0.003, 0.0006), DARK)
    return bm


CORD_SPINE = ((-0.420, -0.040, 0.808), (-0.398, -0.020, 0.772), (-0.400, 0.025, 0.762), (-0.430, 0.062, 0.780), (-0.450, 0.074, 0.820))


def spine_points(points, count):
    pts = [Vector(p) for p in points]
    ext = [pts[0] * 2 - pts[1]] + pts + [pts[-1] * 2 - pts[-2]]
    dense = []
    for i in range(1, len(ext) - 2):
        p0, p1, p2, p3 = ext[i - 1], ext[i], ext[i + 1], ext[i + 2]
        for k in range(40):
            t = k / 40.0
            dense.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t * t * t))
    dense.append(pts[-1])
    lengths = [0.0]
    for a, b in zip(dense, dense[1:]):
        lengths.append(lengths[-1] + (b - a).length)
    total = lengths[-1]
    result, j = [], 0
    for i in range(count):
        target = total * i / (count - 1)
        while j < len(lengths) - 2 and lengths[j + 1] < target:
            j += 1
        span = max(1e-9, lengths[j + 1] - lengths[j])
        result.append(dense[j].lerp(dense[j + 1], (target - lengths[j]) / span))
    return result, total


def build_cord(materials):
    """Coiled handset cord: a helix around a hanging spine, from the mouthpiece to the cord port."""
    turns_per_m, radius = 170.0, 0.0068
    spine, length = spine_points(CORD_SPINE, 900)
    turns = length * turns_per_m
    normal = Vector((0.0, 0.0, 1.0))
    points = []
    for i, p in enumerate(spine):
        t = (spine[min(i + 1, len(spine) - 1)] - spine[max(i - 1, 0)]).normalized()
        normal = (normal - t * normal.dot(t)).normalized()
        binormal = t.cross(normal)
        a = 2 * pi * turns * i / (len(spine) - 1)
        points.append(p + radius * (cos(a) * normal + sin(a) * binormal))
    curve = bpy.data.curves.new(NAME + "_HandsetCord", "CURVE")
    curve.dimensions = "3D"
    curve.bevel_depth = 0.0021
    curve.bevel_resolution = 1
    spline = curve.splines.new("POLY")
    spline.points.add(len(points) - 1)
    for sp, p in zip(spline.points, points):
        q = R180 @ p
        sp.co = (q.x, q.y, q.z, 1.0)
    obj = bpy.data.objects.new("HandsetCord", curve)
    bpy.context.scene.collection.objects.link(obj)
    select_only([obj])
    bpy.ops.object.convert(target="MESH")
    obj = bpy.context.view_layer.objects.active
    obj.data.materials.clear()
    for mat in materials:
        obj.data.materials.append(mat)
    for polygon in obj.data.polygons:
        polygon.material_index = RUBBER
        polygon.use_smooth = True
    return obj


def build_seat():
    """Small restrained seat on a floor plate, occupied position; its origin is the plate centre."""
    bm = new_bm()
    y = SEAT_Y
    box(bm, -0.170, 0.170, y - 0.170, y + 0.170, 0.000, 0.020, GRAPHITE, bevel=0.004, segs=1)
    for x in (-0.130, 0.130):
        for dy in (-0.130, 0.130):
            bolt_m(bm, upright(x, y + dy, 0.020), r=0.0060, h=0.0040, hex_head=True)
    lathe(bm, upright(0.0, y, 0.020), [(0.0, 0.0), (0.046, 0.0), (0.046, 0.170), (0.036, 0.176), (0.032, 0.176), (0.032, 0.380), (0.0, 0.380)],
          24, [GRAPHITE, GRAPHITE, GRAPHITE, GRAPHITE, ALU, ALU])
    box(bm, -0.210, 0.210, y - 0.205, y + 0.200, 0.380, 0.420, GRAPHITE, bevel=0.006, segs=2)
    box(bm, -0.200, 0.200, y - 0.195, y + 0.190, 0.418, 0.472, FABRIC, bevel=0.018, segs=3)
    back = new_bm()
    for x in (-0.170, 0.170):
        box(back, x - 0.012, x + 0.012, y - 0.222, y - 0.198, 0.400, 0.960, ALU, bevel=0.004, segs=2)
    box(back, -0.195, 0.195, y - 0.262, y - 0.212, 0.540, 0.950, FABRIC, bevel=0.018, segs=3)
    for z in (0.620, 0.860):
        box(back, -0.200, 0.200, y - 0.266, y - 0.258, z - 0.018, z + 0.018, STRAP, bevel=0.002, segs=1)
    bmesh.ops.rotate(back, cent=(0.0, y - 0.205, 0.420), matrix=Matrix.Rotation(radians(-8.0), 3, "X"), verts=list(back.verts))
    mesh = bpy.data.meshes.new("tmp_back")
    back.to_mesh(mesh)
    back.free()
    bm.from_mesh(mesh)
    bpy.data.meshes.remove(mesh)
    for side in (-1.0, 1.0):
        box(bm, side * 0.218 - 0.012, side * 0.218 + 0.012, y - 0.050, y + 0.050, 0.392, 0.430, ALU, bevel=0.004, segs=1)
        box(bm, side * 0.205 - 0.004, side * 0.205 + 0.004, y - 0.060, y + 0.030, 0.300, 0.470, STRAP, bevel=0.002, segs=1)
    box(bm, -0.250, -0.205, y - 0.045, y + 0.020, 0.300, 0.330, STEEL, bevel=0.004, segs=2)
    return bm


def build_indicator(x):
    bm = new_bm()
    m = shelf_frame(x, 0.84, 0.0)
    lathe(bm, m, [(0.0, 0.0020), (0.0050, 0.0020), (0.0050, 0.0036), (0.0030, 0.0056), (0.0, 0.0060)], 18, [0, 0, 0, 0])
    return bm


def indicator_centre(x):
    return shelf_frame(x, 0.84, 0.0) @ Vector((0.0, 0.0, 0.0040))


def build_lamp_lens():
    bm = new_bm()
    head = aim(LAMP_HEAD, LAMP_TARGET)
    lathe(bm, head, [(0.0, 0.0210), (0.0400, 0.0210), (0.0400, 0.0230), (0.0, 0.0245)], 28, [0, 0, 0])
    return bm


def build_screen(material):
    """ScreenSurface: 0.44 x 0.26 m quad on Z = 0.12 (Unity), centred on ScreenCenter, facing -Z, UV 0..1."""
    cx, cy, cz = SCREEN_C
    hw, hh = SCREEN_W / 2, SCREEN_H / 2
    corners = [(cx - hw, cy - hh, cz), (cx + hw, cy - hh, cz), (cx + hw, cy + hh, cz), (cx - hw, cy + hh, cz)]
    mesh = bpy.data.meshes.new("ScreenSurfaceMesh")
    mesh.from_pydata([tuple(unity_to_blender(p)) for p in corners], [], [(0, 1, 2, 3)])
    mesh.update()
    uv = mesh.uv_layers.new(name="UVMap")
    for loop, coords in zip(mesh.loops, ((0.0, 0.0), (1.0, 0.0), (1.0, 1.0), (0.0, 1.0))):
        uv.data[loop.index].uv = coords
    mesh.materials.append(material)
    obj = bpy.data.objects.new("ScreenSurface", mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj, corners


def build_anchors():
    result = []
    points = dict(ANCHORS)
    for name, (p, _d) in CONTACTS.items():
        points[name] = (round(p.x, 5), round(p.z, 5), round(p.y, 5))
    for name, p in points.items():
        obj = bpy.data.objects.new(name, None)
        obj.empty_display_type = "ARROWS"
        obj.empty_display_size = 0.04
        obj.location = unity_to_blender(p)
        bpy.context.scene.collection.objects.link(obj)
        result.append(obj)
    return result, points


def make_flat(name, hex_color, rough, emission=None, strength=0.0):
    mat = bpy.data.materials.new(name)
    nodes_on(mat)
    node = principled(mat)
    node.inputs["Base Color"].default_value = hex_rgba(hex_color)
    node.inputs["Metallic"].default_value = 0.0
    node.inputs["Roughness"].default_value = rough
    if emission:
        set_input(node, ("Emission Color", "Emission"), hex_rgba(emission))
        set_input(node, ("Emission Strength",), strength)
    return mat


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
    scene = bpy.data.scenes.new("RD_LabelAtlas")
    made = []
    colors = {}
    font = load_label_font()

    def color_mat(hex_color):
        if hex_color not in colors:
            colors[hex_color] = emission_material("RDatlas_" + hex_color.lstrip("#"), hex_color)
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
            curve = bpy.data.curves.new("RDatlas_text", "FONT")
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
                obj = bpy.data.objects.new("RDatlas_text", curve)
                obj.scale = (0.01, 0.01, 0.01)
                obj.location = (x + w / 2 + dx + ox * shift, y + h / 2 + dz - size * TEXT_DROP + oy * shift, 0.002)
                scene.collection.objects.link(obj)
                made.append(obj)

    for hex_color, faces in quads.items():
        bm = bmesh.new()
        for corners in faces:
            bm.faces.new([bm.verts.new(c) for c in corners])
        mesh = bpy.data.meshes.new("RDatlas_fill")
        bm.to_mesh(mesh)
        bm.free()
        mesh.materials.append(color_mat(hex_color))
        obj = bpy.data.objects.new("RDatlas_fill", mesh)
        scene.collection.objects.link(obj)
        made.extend((obj, mesh))

    camera_data = bpy.data.cameras.new("RDatlas_cam")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = max(ATLAS_W, ATLAS_H) * px
    camera = bpy.data.objects.new("RDatlas_cam", camera_data)
    camera.location = (ATLAS_W * px / 2, ATLAS_H * px / 2, 1.0)
    scene.collection.objects.link(camera)
    scene.camera = camera
    made.extend((camera, camera_data))

    world = bpy.data.worlds.new("RDatlas_world")
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


STYLES.update({
    "cream": dict(bg=MAT_SPEC["paint"][0], fg="#2B2B2C", line=None),
    "graphite": dict(bg=MAT_SPEC["graphite"][0], fg="#E6E3DA", line=None),
    "stripe": dict(bg=MAT_SPEC["stripe"][0], fg=MAT_SPEC["stripe"][0], line=None),
})


def placards():
    """Real words only: control legends, unit plates and drawer contents."""
    items = []

    def add(key, cx, cz, w, h, y, style, texts, shapes=()):
        items.append(dict(key=key, host="Body", c=(cx, cz), s=(w, h), y=y, style=style, texts=texts, flip=False, shapes=shapes))

    lip = (TOP_Z + SHELF["z0"]) / 2
    for x, name, _guarded in TOGGLES:
        add("tg_" + name, x, lip, 0.052, 0.0105, SHELF["y0"], "graphite", [(name, 0.0, 0.0, 0.0064)])
    add("tg_PTT", PTT_X, lip, 0.052, 0.0105, SHELF["y0"], "graphite", [("PTT", 0.0, 0.0, 0.0064)])
    add("tg_DISTRESS", DISTRESS_X, lip, 0.062, 0.0105, SHELF["y0"], "graphite", [("DISTRESS", 0.0, 0.0, 0.0060)])
    for x, name in INDICATORS:
        add("ind_" + name, x, 0.840, 0.034, 0.0095, M3["y0"], "cream", [(name, 0.0, 0.0, 0.0058)])
    for name, x, z, r, label in KNOBS:
        add("kn_" + name, x, z + r + 0.0115, 0.070, 0.0095, M3["y0"], "cream", [(label, 0.0, 0.0, 0.0056)])
    ticks = []
    for i in range(11):
        a = radians(-50.0 + 10.0 * i)
        dx, dz, px, pz = sin(a), cos(a), cos(a), -sin(a)
        r0, r1, hw = (0.0300, 0.0400, 0.0009) if i % 5 == 0 else (0.0330, 0.0400, 0.0006)
        ticks.append(("#101113", [(dx * r0 - px * hw, dz * r0 - pz * hw - 0.026), (dx * r0 + px * hw, dz * r0 + pz * hw - 0.026),
                                  (dx * r1 + px * hw, dz * r1 + pz * hw - 0.026), (dx * r1 - px * hw, dz * r1 - pz * hw - 0.026)]))
    ticks.append(("#B32A22", [(-0.0006, -0.024), (0.0006, -0.024), (0.0222, 0.0110), (0.0210, 0.0116)]))
    add("dial", DIAL[0], DIAL[1], DIAL[2] - 0.010, DIAL[3] - 0.012, M3["y0"] - 0.002, "dial",
        [("SIGNAL", 0.0, -0.016, 0.0072), ("0", -0.026, 0.002, 0.0050), ("5", 0.0, 0.010, 0.0050), ("10", 0.027, 0.002, 0.0050)], shapes=ticks)
    add("unit", 0.170, 0.868, 0.300, 0.0160, M2["y0"], "dark", [("COMM UNIT  CU-1    AURORA-7", 0.0, 0.0, 0.0072)])
    add("stripe", 0.375, 0.868, 0.060, 0.0050, M2["y0"], "stripe", [])
    add("handset", -0.535, 0.990, 0.110, 0.0150, M1["y0"], "cream", [("HANDSET", 0.0, 0.0, 0.0090)])
    add("speaker", 0.600, 1.152, 0.110, 0.0130, M4["y0"], "cream", [("SPEAKER", 0.0, 0.0, 0.0080)])
    for side, top, bottom in ((-1.0, "SPARES", "MANUALS"), (1.0, "CABLES", "TOOLS")):
        xc = side * (PED_X0 + DESK_HX) / 2
        add("dr_" + top, xc, 0.690 - 0.090, 0.120, 0.0190, -0.306, "dark", [(top, 0.0, 0.0, 0.0090)])
        add("dr_" + bottom, xc, 0.370 - 0.090, 0.120, 0.0190, -0.306, "dark", [(bottom, 0.0, 0.0, 0.0090)])
    return items


def shrink_hidden_faces(obj):
    """Faces nobody sees get a fraction of the texel density: the wall side and the undersides."""
    mesh = obj.data
    uv = mesh.uv_layers.active.data
    for polygon in mesh.polygons:
        n = polygon.normal
        if polygon.area < 0.002:
            continue
        uz = -(polygon.center.y + obj.location.y)
        if n.y < -0.99 and uz > 0.28:
            factor = 0.20
        elif n.z < -0.99:
            factor = 0.30
        else:
            continue
        loops = list(polygon.loop_indices)
        cu = sum(uv[i].uv.x for i in loops) / len(loops)
        cv = sum(uv[i].uv.y for i in loops) / len(loops)
        for i in loops:
            uv[i].uv = ((uv[i].uv.x - cu) * factor + cu, (uv[i].uv.y - cv) * factor + cv)


def to_unity(v):
    return (-v[0], v[2], -v[1])


def design_to_unity(p):
    return (p[0], p[2], p[1])


# ------------------------------------------------------------------ render set (not exported)
def build_render_set(scene, collection):
    def area(name, unity_location, unity_target, power, size, color):
        data = bpy.data.lights.new(name, "AREA")
        data.energy, data.size, data.color = power, size, color
        obj = bpy.data.objects.new(name, data)
        obj.location = unity_to_blender(unity_location)
        look_at(obj, unity_to_blender(unity_target))
        collection.objects.link(obj)

    area("RD_Key", (1.6, 2.4, -2.2), (0.0, 0.8, 0.0), 260.0, 2.0, (1.0, 0.97, 0.93))
    area("RD_Fill", (-2.0, 1.4, -1.6), (0.0, 0.8, 0.0), 70.0, 2.5, (0.90, 0.94, 1.0))
    area("RD_Top", (0.0, 2.8, -0.2), (0.0, 0.8, 0.0), 90.0, 2.0, (1.0, 1.0, 1.0))
    lamp_point, lamp_dir = CONTACTS["LampEmitter"]
    spot = bpy.data.lights.new("RD_DeskLamp", "SPOT")
    spot.energy, spot.color, spot.spot_size, spot.spot_blend, spot.shadow_soft_size = 14.0, (1.0, 0.80, 0.55), radians(85.0), 0.6, 0.03
    obj = bpy.data.objects.new("RD_DeskLamp", spot)
    obj.location = R180 @ (lamp_point + lamp_dir * 0.004)
    obj.rotation_euler = (R180.to_3x3() @ lamp_dir).to_track_quat("-Z", "Y").to_euler()
    collection.objects.link(obj)

    floor_mat = bpy.data.materials.new("RD_RenderFloor")
    nodes_on(floor_mat)
    principled(floor_mat).inputs["Base Color"].default_value = hex_rgba("#3A3B3D")
    principled(floor_mat).inputs["Roughness"].default_value = 0.7
    bm = bmesh.new()
    bm.faces.new([bm.verts.new(p) for p in ((-3.0, -3.0, 0.0), (3.0, -3.0, 0.0), (3.0, 3.0, 0.0), (-3.0, 3.0, 0.0))])
    mesh = bpy.data.meshes.new("RD_Floor")
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(floor_mat)
    collection.objects.link(bpy.data.objects.new("RD_Floor", mesh))

    cameras = {}

    def camera(key, unity_location, unity_target, lens=None, fov=None):
        data = bpy.data.cameras.new("RD_Cam_" + key)
        if fov is not None:
            data.sensor_fit = "VERTICAL"
            data.angle_y = radians(fov)
        else:
            data.lens = lens
        data.clip_start = 0.01
        obj = bpy.data.objects.new("RD_Cam_" + key, data)
        obj.location = unity_to_blender(unity_location)
        look_at(obj, unity_to_blender(unity_target))
        collection.objects.link(obj)
        cameras[key] = obj

    camera("wide", (1.35, 1.65, -2.05), (0.0, 0.82, -0.18), lens=30.0)
    camera("seated", ANCHORS["SeatedCamera"], (0.08, 0.98, 0.12), fov=68.0)
    camera("controls", (0.30, 1.06, -0.45), (-0.08, 0.88, 0.04), lens=42.0)
    camera("handset", (-0.22, 1.02, -0.44), (-0.50, 0.81, -0.01), lens=48.0)
    camera("rear", (-1.55, 1.75, 1.75), (0.0, 0.80, 0.0), lens=32.0)

    world = bpy.data.worlds.new("RD_World")
    nodes_on(world)
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.030, 0.031, 0.033, 1.0)
    scene.world = world
    for look in ("AgX - Medium High Contrast", "Medium High Contrast"):
        try:
            scene.view_settings.look = look
            break
        except TypeError:
            continue
    return cameras


def build_screen_preview(collection):
    """Preview-only, dim screen content drawn from scratch (a waveform line and two captions). Not exported."""
    line = bpy.data.materials.new("RD_PreviewLines")
    nodes_on(line)
    tree = line.node_tree
    tree.nodes.clear()
    emission = tree.nodes.new("ShaderNodeEmission")
    emission.inputs["Color"].default_value = hex_rgba("#C9A46A")
    emission.inputs["Strength"].default_value = 0.6
    output = tree.nodes.new("ShaderNodeOutputMaterial")
    tree.links.new(emission.outputs[0], output.inputs["Surface"])
    cx, cy, cz = SCREEN_C
    y = cz - 0.0008
    bm = bmesh.new()
    width = 0.0012
    pts = []
    for k in range(121):
        t = k / 120.0
        x = cx - 0.18 + 0.36 * t
        amp = 0.020 * math.exp(-((t - 0.35) ** 2) / 0.01) + 0.012 * math.exp(-((t - 0.62) ** 2) / 0.004)
        pts.append((x, cy - 0.020 + amp * sin(t * 90.0)))
    for (x0, z0), (x1, z1) in zip(pts, pts[1:]):
        dx, dz = x1 - x0, z1 - z0
        length = max(1e-9, (dx * dx + dz * dz) ** 0.5)
        nx, nz = -dz / length * width / 2, dx / length * width / 2
        bm.faces.new([bm.verts.new((x0 + nx, y, z0 + nz)), bm.verts.new((x0 - nx, y, z0 - nz)),
                      bm.verts.new((x1 - nx, y, z1 - nz)), bm.verts.new((x1 + nx, y, z1 + nz))])
    bmesh.ops.transform(bm, matrix=R180, verts=list(bm.verts))
    mesh = bpy.data.meshes.new("RD_PreviewWave")
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(line)
    collection.objects.link(bpy.data.objects.new("PreviewOnly waveform", mesh))
    font = load_label_font()
    for text, ux, uy, size in (("AWAITING REPLY", cx - 0.18, cy + 0.075, 0.016), ("DISTRESS CALL SENT", cx - 0.18, cy + 0.045, 0.009)):
        curve = bpy.data.curves.new("RD_PreviewText", "FONT")
        curve.body = text
        if font is not None:
            curve.font = font
        curve.size = size
        curve.materials.append(line)
        obj = bpy.data.objects.new("PreviewOnly " + text[:14], curve)
        obj.location = unity_to_blender((ux, uy, cz - 0.0009))
        obj.rotation_euler = (math.pi / 2, 0.0, math.pi)
        collection.objects.link(obj)


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
    mat = bpy.data.materials.new("RD_WireOverlay")
    nodes_on(mat)
    g = Graph(mat)
    wire = g.nodes.new("ShaderNodeWireframe")
    wire.use_pixel_size = True
    wire.inputs["Size"].default_value = 1.1
    color = g.mix(wire.outputs[0], (0.62, 0.63, 0.64, 1.0), (0.02, 0.05, 0.10, 1.0))
    g.links.new(color, principled(mat).inputs["Base Color"])
    principled(mat).inputs["Roughness"].default_value = 0.7
    return mat


BS = chr(92)


def scrub_blend(path):
    """Blank every copy of this folder and of the home folder in the (uncompressed) .blend."""
    data = bytearray(open(path, "rb").read())
    hits = 0
    home = os.path.expanduser("~")
    for variant in {os.path.dirname(path), os.path.dirname(path).replace(BS, "/"), home, home.replace(BS, "/")}:
        needle = variant.encode("utf-8")
        start = data.find(needle)
        while start >= 0:
            end = data.find(b"\x00", start)
            data[start:end] = b"\x00" * (end - start)
            hits += 1
            start = data.find(needle, start)
    open(path, "wb").write(bytes(data))
    return hits


def unity_bounds(objs):
    lo, hi = world_bounds(objs)
    a, b = to_unity(lo), to_unity(hi)
    return [round(min(x, y), 4) for x, y in zip(a, b)], [round(max(x, y), 4) for x, y in zip(a, b)]


# ------------------------------------------------------------------ main
def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = "CYCLES"
    device = enable_gpu(scene)
    log("Blender", bpy.app.version_string, "| stage", STAGE, "| device", device)
    kit = bpy.data.collections.new("RadioDesk")
    scene.collection.children.link(kit)
    preview = bpy.data.collections.new("PreviewOnly")
    scene.collection.children.link(preview)

    full = STAGE == "full"
    surface = [make_proc_material(k) if full else make_flat_material(k) for k in MAT_KEYS]
    if full:
        PROC.extend(surface)
    screen_mat = make_flat("RD_Screen", "#0B0D0E", 0.32)
    amber = make_indicator_material("RD_Amber", "#FFA61A", 1.2)
    warm = make_flat("RD_LampWarm", "#F3E2C4", 0.25, emission="#FFD9A6", strength=3.0)

    body = finish(build_body(), "DeskBody", surface)
    handset = finish(build_handset(), "Handset", surface, origin=unity_to_blender(ANCHORS["HandsetGrip"]))
    cord = build_cord(surface)
    seat = finish(build_seat(), "Seat", surface, origin=unity_to_blender((0.0, 0.0, SEAT_Y)))
    lamps = []
    for x, name in INDICATORS:
        lamp_obj = finish(build_indicator(x), "Indicator_" + name, [amber], origin=R180 @ indicator_centre(x))
        lamp_obj.data.uv_layers.new(name="UVMap")
        lamps.append(lamp_obj)
    lamp_point = CONTACTS["LampEmitter"][0]
    lens = finish(build_lamp_lens(), "LampLens", [warm], origin=R180 @ lamp_point)
    lens.data.uv_layers.new(name="UVMap")
    screen, corners = build_screen(screen_mat)
    anchors, anchor_points = build_anchors()
    baked = [body, handset, cord, seat]
    parts = baked + lamps + [lens, screen]
    report = {"blender": bpy.app.version_string, "stage": STAGE, "cycles_device": device}

    if full:
        items = placards()
        pack_atlas(items)
        labels_path = os.path.join(TEX_DIR, NAME + "_Labels.png")
        render_label_atlas(items, labels_path)
        log("label atlas rendered")
        unwrap(baked)
        color = new_image("RDbake_color", False)
        metal = new_image("RDbake_metal", True)
        rough = new_image("RDbake_rough", True)
        occlusion = new_image("RDbake_ao", True)
        together = [(obj, []) for obj in baked]
        bake_pass("color", color, together, 48)
        bake_pass("metal", metal, together, 48)
        bake_pass("rough", rough, together, 48)
        bake_pass("ao", occlusion, together, 96)
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
        metal_smooth = bpy.data.images.new("RDbake_ms", TEX, TEX, alpha=True)
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
        final = bpy.data.materials.new("RD_DeskKit")
        nodes_on(final)
        tree = final.node_tree
        for index, (key, socket_name, non_color) in enumerate((("BaseColor", "Base Color", False), ("Metallic", "Metallic", True), ("Roughness", "Roughness", True))):
            node = tree.nodes.new("ShaderNodeTexImage")
            node.image = load_image(paths[key], non_color)
            node.location = (-520, 320 - index * 300)
            tree.links.new(node.outputs["Color"], principled(final).inputs[socket_name])
        label_mat = bpy.data.materials.new("RD_Labels")
        nodes_on(label_mat)
        label_node = label_mat.node_tree.nodes.new("ShaderNodeTexImage")
        label_node.image = load_image(labels_path, False)
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
        for obj in label_objects.values():
            obj.data.transform(R180)
        select_only([label_objects["Body"], body], active=body)
        bpy.ops.object.join()
        body = bpy.context.view_layer.objects.active
        parts[0] = body
        report["textures"] = {key: os.path.relpath(value, OUT).replace(BS, "/") for key, value in paths.items()}
        report["texture_size"] = TEX
        report["label_font"] = LABEL_FONT["name"]
    else:
        for obj in baked:
            compact_materials(obj)

    for obj in [body, handset, seat, lens] + lamps:
        weighted_normals(obj)
    for obj in parts + anchors:
        for collection in list(obj.users_collection):
            collection.objects.unlink(obj)
        kit.objects.link(obj)
    bpy.context.view_layer.update()

    triangles = {obj.name: tri_count(obj) for obj in parts}
    desk_lo, desk_hi = unity_bounds([body])
    seat_lo, seat_hi = unity_bounds([seat])
    anchor_report = {}
    for obj in anchors:
        got = [round(v, 5) for v in to_unity(obj.matrix_world.translation)]
        entry = {"unity": got}
        if obj.name in ANCHORS:
            entry["brief"] = list(ANCHORS[obj.name])
            entry["equal"] = all(abs(a - b) < 1e-5 for a, b in zip(got, ANCHORS[obj.name]))
        if obj.name in CONTACTS:
            entry["direction_unity"] = [round(v, 4) for v in design_to_unity(CONTACTS[obj.name][1])]
        anchor_report[obj.name] = entry
    report.update({
        "units": "metres; Unity axes: X width, Y up, Z depth; the seated player is on -Z facing +Z; origin = floor under the desk centre",
        "desk_bounds_unity": {"min": desk_lo, "max": desk_hi},
        "seat_bounds_unity_occupied": {"min": seat_lo, "max": seat_hi},
        "seat_bounds_unity_stowed": {"min": [seat_lo[0], seat_lo[1], round(seat_lo[2] + SEAT_STOW, 4)], "max": [seat_hi[0], seat_hi[1], round(seat_hi[2] + SEAT_STOW, 4)]},
        "seat_stow_offset_unity": [0.0, 0.0, SEAT_STOW],
        "clearances_m": {
            "leg_room_width": round(2 * PED_X0, 3),
            "under_worktop_height": round(TOP_Z - 0.038, 3),
            "leg_room_depth_from_front_edge": round(0.282 - DESK_Y0, 3),
            "seat_front_to_desk_bumper_occupied": round((DESK_Y0 - 0.012) - seat_hi[2], 3),
            "seat_entry_behind_seat_occupied": round(seat_lo[2] - ANCHORS["SeatEntry"][2], 3),
            "eye_to_screen_centre": round((Vector(ANCHORS["SeatedCamera"]) - Vector(SCREEN_C)).length, 3),
        },
        "screen": {"corners_unity": [list(c) for c in corners], "normal_unity": [0.0, 0.0, -1.0], "size_m": [SCREEN_W, SCREEN_H],
                   "uv": "(0,0) at the bottom-left corner, (1,1) at the top-right, as seen from the seat"},
        "anchors": anchor_report,
        "triangles": triangles,
        "triangles_total": sum(triangles.values()),
        "materials": {obj.name: [mat.name for mat in obj.data.materials] for obj in parts},
    })
    log("desk", desk_lo, desk_hi, "seat", seat_lo, seat_hi, "tris", report["triangles_total"])
    log("anchors equal:", all(v.get("equal", True) for v in anchor_report.values()))

    render_set = bpy.data.collections.new("RD_RenderSet")
    scene.collection.children.link(render_set)
    cameras = build_render_set(scene, render_set)
    build_screen_preview(preview)
    if full:
        render(scene, cameras["wide"], "01-wide.png", (1600, 1000), SAMPLES)
        render(scene, cameras["seated"], "02-seated-view.png", (1600, 1000), SAMPLES)
        render(scene, cameras["controls"], "03-controls.png", (1600, 1000), SAMPLES)
        render(scene, cameras["handset"], "04-handset.png", (1600, 1000), SAMPLES)
        render(scene, cameras["rear"], "05-rear.png", (1600, 1000), SAMPLES)
        bpy.context.view_layer.material_override = wire_material()
        render(scene, cameras["wide"], "06-wire.png", (1600, 1000), 32)
        bpy.context.view_layer.material_override = None
        cut = []
        for mat in {slot for obj in parts for slot in obj.data.materials if slot is not None and slot.node_tree}:
            for link in list(mat.node_tree.links):
                if link.from_node.type == "TEX_IMAGE":
                    cut.append((mat.node_tree, link.from_socket, link.to_socket))
                    mat.node_tree.links.remove(link)
        select_only(parts + anchors, active=body)
        bpy.ops.export_scene.fbx(
            filepath=os.path.join(OUT, NAME + ".fbx"), use_selection=True, object_types={"MESH", "EMPTY"},
            apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
            use_mesh_modifiers=True, mesh_smooth_type="FACE", use_triangles=True, add_leaf_bones=False,
            bake_anim=False, path_mode="RELATIVE", embed_textures=False)
        for tree, source, target in cut:
            tree.links.new(source, target)
        for collection in (render_set, preview):
            for obj in list(collection.objects):
                bpy.data.objects.remove(obj, do_unlink=True)
            bpy.data.collections.remove(collection)
        blend_path = os.path.join(OUT, NAME + ".blend")
        bpy.context.preferences.filepaths.save_version = 0
        bpy.ops.wm.save_as_mainfile(filepath=blend_path, compress=False)
        bpy.ops.file.make_paths_relative()
        bpy.ops.wm.save_mainfile(compress=False)
        report["blend_path_scrubbed"] = scrub_blend(blend_path)
        log("exported .fbx and saved .blend")
    else:
        render(scene, cameras["wide"], "geometry_wide.png", (1200, 750), 48)
        render(scene, cameras["seated"], "geometry_seated.png", (1200, 750), 48)
        render(scene, cameras["controls"], "geometry_controls.png", (1200, 750), 48)
        render(scene, cameras["handset"], "geometry_handset.png", (1200, 750), 48)
    with open(os.path.join(OUT, NAME + "_report.json" if full else "geometry_report.json"), "w", encoding="utf-8", newline="\n") as handle:
        json.dump(report, handle, indent=2)
    log("done")


main()
