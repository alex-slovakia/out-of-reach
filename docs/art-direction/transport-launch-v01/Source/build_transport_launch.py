"""
OUT OF REACH - transport and launch kit (TRANSPORT-LAUNCH-ART-V1): the small Earth-to-orbit transport the player
loads and boards in the prologue, on its booster and launch barge with an access mast and swing arm. Original
asset built from code in Blender 5.3. A fictional visual launch vehicle: proportions are kept plausible and
coherent (the interior is the inside of the same hull), but nothing here is validated launch engineering.

    blender -b --factory-startup --python build_transport_launch.py -- --stage geometry --out <folder>
    blender -b --factory-startup --python build_transport_launch.py -- --stage full --out <folder>

Unity frame (Codex's 17:01 UTC brief): metres, Y up, origin on the capsule floor at the capsule axis; the player
boards through the side hatch from -Z and looks +Z. Parts are modelled in a design frame (x = Unity X,
y = Unity Z, z = Unity Y) and turned 180 degrees about Z on the way into the file, as in TERMINAL-ART-V2 and
RADIO-DESK-V1: Blender = (-X, -Z, Y) of Unity.
"""

import json
import math
import os
import sys
import time
from math import asin, atan2, cos, pi, radians, sin, sqrt

import bmesh
import bpy
from mathutils import Matrix, Vector

T0 = time.time()


def log(*parts):
    print("[tl %6.1fs]" % (time.time() - T0), *parts, flush=True)


ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def arg(name, default=None):
    if name in ARGS:
        i = ARGS.index(name)
        if i + 1 < len(ARGS) and not ARGS[i + 1].startswith("--"):
            return ARGS[i + 1]
        return True
    return default


OUT = os.path.abspath(arg("--out", os.path.dirname(os.path.abspath(__file__))))
STAGE = arg("--stage", "geometry")
TEX = int(arg("--tex", 2048))
SAMPLES = int(arg("--samples", 128))
TEX_DIR = os.path.join(OUT, "textures")
RENDER_DIR = os.path.join(OUT, "renders")
for folder in (OUT, TEX_DIR, RENDER_DIR):
    os.makedirs(folder, exist_ok=True)

NAME = "TransportLaunch"
R180 = Matrix.Rotation(pi, 4, "Z")


def unity_to_blender(p):
    return Vector((-p[0], -p[2], p[1]))


def design(p):
    return Vector((p[0], p[2], p[1]))


# ------------------------------------------------------------------ capsule (Unity metres; floor y = 0 on the axis)
R_OUT, R_IN = 2.50, 2.38                  # cylinder outer / inner radius (5.0 m diameter)
CYL_TOP = 2.60                            # cylinder outer top; the cone starts here
CONE_TOP = 4.55                           # cone outer top (radius 1.05)
NOSE_R, DOCK_R, DOCK_TOP = 1.05, 0.82, 4.95
TUNNEL_R = 0.55
HATCH_W, HATCH_H, HATCH_CORNER = 1.40, 2.15, 0.16
CORRIDOR = 0.65                           # clear half width of the central route
RACK_X0, RACK_X1 = 0.72, 1.25             # rack fronts and backs (both sides)
CELL = 0.55                               # cell pitch along Z
LEFT_CELLS = ("oxygen", "water", "food", "battery")       # -X side, from the hatch inwards
RIGHT_CELLS = ("solar", "spares", "science")              # +X side
CELL_Z0 = -1.30
SHELVES = (0.10, 0.46, 0.82)              # slot floors (case height 0.319 m with handle)
SEAT_Z = 1.45                             # seat pedestal centre (the seat faces -Z, towards the hatch)
# ------------------------------------------------------------------ stack and barge
SERVICE_TOP, SERVICE_BOTTOM, SERVICE_R = -0.55, -3.40, 2.35
INTER_BOTTOM, BOOSTER_R = -4.40, 2.30
BOOSTER_BOTTOM = -29.40
ENGINE_BOTTOM, BELL_EXIT = -30.60, -31.40
DECK_Y = -32.60
BARGE = dict(x0=-18.0, x1=18.0, z0=-14.0, z1=10.0, bottom=-36.0)
MAST = dict(cx=0.0, cz=-8.50, half=1.60, top=3.80)
ARM_Z0, ARM_Z1, ARM_HALF = -6.90, -2.55, 0.90

ANCHORS = {
    "BoardingThreshold": (0.0, 0.0, -2.50),
    "SeatEntry": (0.0, 0.0, 0.80),               # 0.34 m in front of the seat pan: room for a 0.30 m player radius
    "SeatedCamera": (0.0, 1.16, 1.88),
    "HatchPivot": (-0.72, 0.0, -2.40),
    "HatchLatch": (0.58, 1.05, -2.33),
    "DockingPort": (0.0, DOCK_TOP, 0.0),
    "EngineEmitter": (0.0, BELL_EXIT, 0.0),
    "AccessArmPivot": (-ARM_HALF, 0.0, ARM_Z0),
}
for i, name in enumerate(LEFT_CELLS):
    ANCHORS["CargoBay_" + name] = (-(RACK_X0 + RACK_X1) / 2, SHELVES[0], CELL_Z0 + CELL * (i + 0.5))
for i, name in enumerate(RIGHT_CELLS):
    ANCHORS["CargoBay_" + name] = ((RACK_X0 + RACK_X1) / 2, SHELVES[0], CELL_Z0 + CELL * (i + 0.5))
DIRECTIONS = {
    "BoardingThreshold": (0.0, 0.0, 1.0), "SeatEntry": (0.0, 0.0, -1.0), "SeatedCamera": (0.0, -0.17, -0.985),
    "HatchPivot": (0.0, 1.0, 0.0), "HatchLatch": (0.0, 0.0, 1.0), "DockingPort": (0.0, 1.0, 0.0),
    "EngineEmitter": (0.0, -1.0, 0.0), "AccessArmPivot": (0.0, 1.0, 0.0),
}
for name in LEFT_CELLS:
    DIRECTIONS["CargoBay_" + name] = (1.0, 0.0, 0.0)      # the case's labelled face (+Z of the case) turns to the corridor
for name in RIGHT_CELLS:
    DIRECTIONS["CargoBay_" + name] = (-1.0, 0.0, 0.0)

# ------------------------------------------------------------------ materials
MAT_KEYS = ["hull", "panel", "graphite", "alu", "steel", "rubber", "dark", "glass", "keys", "stripe", "white",
            "yellow", "black", "deck", "barge", "fabric", "strap", "light"]
(HULL, PANEL, GRAPHITE, ALU, STEEL, RUBBER, DARK, GLASS, KEYCAP, STRIPE, WHITE,
 YELLOW, BLACK, DECKM, BARGEM, FABRIC, STRAP, LIGHT) = range(18)
MAT_SPEC = {
    #             base sRGB  metal rough  wear  worn sRGB  worn metal  dirt
    "hull":      ("#D9D8D1", 0.00, 0.50, 0.45, "#9C9EA0", 1.0, 0.55),   # off-white thermal paint (A7_HullBlanket tone)
    "panel":     ("#B5B3AE", 0.00, 0.52, 0.45, "#8A8884", 1.0, 0.55),   # A7_PaintWhite, darker panels
    "graphite":  ("#2B2B2C", 0.25, 0.50, 0.45, "#6B6E72", 0.8, 0.45),
    "alu":       ("#C3C6C9", 1.00, 0.30, 0.00, "#C3C6C9", 1.0, 0.40),
    "steel":     ("#A7A9AC", 1.00, 0.34, 0.00, "#A7A9AC", 1.0, 0.45),
    "rubber":    ("#1B1B1C", 0.00, 0.82, 0.00, "#1B1B1C", 0.0, 0.30),
    "dark":      ("#0E0F10", 0.00, 0.58, 0.00, "#0E0F10", 0.0, 0.25),
    "glass":     ("#141B20", 0.00, 0.08, 0.00, "#141B20", 0.0, 0.10),
    "keys":      ("#232528", 0.30, 0.46, 0.50, "#7A7E83", 0.9, 0.35),
    "stripe":    ("#C4502A", 0.00, 0.50, 0.40, "#9C9EA0", 1.0, 0.50),
    "white":     ("#E6E3DA", 0.00, 0.50, 0.00, "#E6E3DA", 0.0, 0.20),
    "yellow":    ("#D2A322", 0.00, 0.55, 0.50, "#7D7A70", 1.0, 0.50),   # rails, straps, edge marks (A7_CableYellow)
    "black":     ("#1E1F21", 0.10, 0.55, 0.30, "#5A5C5F", 0.6, 0.40),   # booster band, engine section
    "deck":      ("#4A4C4E", 0.60, 0.62, 0.80, "#8C8E90", 1.0, 0.75),   # wet non-slip steel deck
    "barge":     ("#3A2C27", 0.30, 0.70, 0.70, "#7A5A48", 0.7, 0.80),   # painted steel hull, weathered
    "fabric":    ("#2C313B", 0.00, 0.86, 0.10, "#4B505A", 0.0, 0.55),   # A7_FabricDark
    "strap":     ("#4B4F53", 0.00, 0.80, 0.10, "#5E6266", 0.0, 0.40),   # A7_FabricStrap
    "light":     ("#F3EDE0", 0.00, 0.30, 0.00, "#F3EDE0", 0.0, 0.10),   # light panel lenses (A7_LightPanel)
}
SOCK = {}
PROC = []
CONTACTS = {}
ATLASES = {"Exterior": ["CapsuleShell", "Hatch", "AccessArm"], "Interior": ["CapsuleInterior"],
           "Site": ["BoosterStack", "LaunchBarge", "AccessMast"]}
EMISSIVE = {"TL_LightPanel": ("#F3EDE0", "#FFE7C4", 3.0), "TL_StatusGreen": ("#12331A", "#39E06A", 2.5), "TL_DeckLamp": ("#3A2408", "#FFB060", 5.0)}
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
    mat = bpy.data.materials.new("TL_" + key)
    nodes_on(mat)
    node = principled(mat)
    node.inputs["Base Color"].default_value = hex_rgba(base_hex)
    node.inputs["Metallic"].default_value = metal
    node.inputs["Roughness"].default_value = rough
    return mat


def make_proc_material(key):
    """Source material with restrained procedural wear; only used as the bake source."""
    base_hex, metal, rough, wear, wear_hex, wear_metal, dirt = MAT_SPEC[key]
    mat = bpy.data.materials.new("TLsrc_" + key)
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



# ------------------------------------------------------------------ transport parts (design frame: x, y = Unity Z, z = Unity Y)
def az_frame(az_deg, radius, height):
    """Frame on the hull at an azimuth measured from +Z (Unity) towards +X; local +Y points radially outwards."""
    a = radians(az_deg)
    return Matrix.Translation((radius * sin(a), radius * cos(a), height)) @ Matrix.Rotation(-a, 4, "Z")


def capsule_shell():
    bm = new_bm()
    prof = [(0.0, -0.55), (2.10, -0.55), (2.40, -0.48), (R_OUT, -0.36), (R_OUT, CYL_TOP), (NOSE_R, CONE_TOP), (0.96, 4.70),
            (DOCK_R, 4.70), (DOCK_R, DOCK_TOP), (TUNNEL_R, DOCK_TOP), (TUNNEL_R, 4.50), (0.93, 4.42), (R_IN, 2.52), (R_IN, 0.0), (0.0, 0.0)]
    mats = [HULL, HULL, HULL, HULL, HULL, HULL, ALU, ALU, ALU, PANEL, PANEL, PANEL, PANEL, GRAPHITE]
    lathe(bm, upright(0.0, 0.0, 0.0), prof, 64, mats)
    return bm


def shell_cutters():
    bm = new_bm()
    box(bm, -HATCH_W / 2, HATCH_W / 2, -R_OUT - 0.40, -R_IN + 0.15, -0.004, HATCH_H, HULL, bevel=HATCH_CORNER, segs=4)
    for az in (60.0, -60.0):
        box_m(bm, az_frame(az, (R_OUT + R_IN) / 2, 1.25), (0.46, 0.60, 0.60), HULL, bevel=0.08, segs=3)
    for az in (40.0, -40.0, 140.0, -140.0):
        box_m(bm, az_frame(az, 1.78, 3.35), (0.46, 0.90, 0.55), HULL, bevel=0.08, segs=3)
    return bm


def shell_details():
    """Exterior and opening trims added after the cut: hatch frame, window glass and frames, RCS quads, rails."""
    bm = new_bm()
    hw, hh = HATCH_W / 2, HATCH_H
    for x0, x1, z0, z1 in ((-hw - 0.10, -hw, -0.02, hh + 0.10), (hw, hw + 0.10, -0.02, hh + 0.10), (-hw - 0.10, hw + 0.10, hh, hh + 0.10)):
        box(bm, x0, x1, -R_OUT - 0.05, -R_IN + 0.02, z0, z1, ALU, bevel=0.012, segs=1)
    box(bm, -hw, hw, -R_OUT - 0.06, -R_IN + 0.02, -0.03, 0.0, STEEL, bevel=0.004, segs=1)
    for az in (60.0, -60.0):
        box_m(bm, az_frame(az, R_IN + 0.03, 1.25), (0.40, 0.02, 0.54), GLASS, bevel=0.0, segs=1)
        box_m(bm, az_frame(az, R_OUT + 0.01, 1.25), (0.54, 0.04, 0.68), PANEL, bevel=0.02, segs=2)
    for az in (40.0, -40.0, 140.0, -140.0):
        box_m(bm, az_frame(az, 1.80, 3.35), (0.40, 0.04, 0.48), GLASS, bevel=0.0, segs=1)
    for az in (20.0, 110.0, 200.0, 290.0):
        m = az_frame(az, 2.06, 3.05)
        box_m(bm, m, (0.30, 0.22, 0.30), PANEL, bevel=0.03, segs=2)
        for dx in (-0.08, 0.08):
            lathe(bm, m @ Matrix.Translation((dx, 0.11, 0.0)) @ RXM90.inverted() @ RX90.inverted() @ Matrix.Rotation(radians(-90.0), 4, "X"),
                  [(0.0, 0.0), (0.035, 0.0), (0.05, 0.06), (0.0, 0.06)], 12, [DARK, DARK, DARK])
    for side in (-1.0, 1.0):
        x = side * (hw + 0.30)
        tube(bm, (x, -R_OUT - 0.10, 0.70), (x, -R_OUT - 0.10, 1.90), 0.022, YELLOW)
        for z in (0.70, 1.90):
            tube(bm, (x, -R_OUT - 0.10, z), (x, -R_OUT + 0.02, z), 0.018, YELLOW)
    lathe(bm, upright(0.0, 0.0, 4.60), [(0.0, 0.0), (TUNNEL_R + 0.01, 0.0), (TUNNEL_R + 0.01, 0.04), (0.0, 0.04)], 32, [ALU, ALU, ALU])
    return bm


def hatch_door():
    """Plug door following the cylinder; hinge on the left jamb (Unity -X) at HatchPivot."""
    bm = new_bm()
    outline = rr_points(HATCH_W / 2 - 0.02, HATCH_H / 2 - 0.02, HATCH_CORNER - 0.02, 4, 0.0, HATCH_H / 2)
    layers = []
    for r in (R_IN - 0.02, R_OUT + 0.035):
        layers.append([bm.verts.new((x, -sqrt(max(r * r - x * x, 0.0)), z)) for x, z in outline])
    inner, outer = layers
    faces = [bm.faces.new(tuple(inner)), bm.faces.new(tuple(reversed(outer)))]
    n = len(outline)
    for k in range(n):
        k1 = (k + 1) % n
        faces.append(bm.faces.new((inner[k], inner[k1], outer[k1], outer[k])))
    for face in faces:
        face.material_index = HULL
    bmesh.ops.recalc_face_normals(bm, faces=faces)
    faces[0].material_index = PANEL
    y_out = -R_OUT - 0.035
    tube(bm, (0.45, y_out - 0.08, 0.80), (0.45, y_out - 0.08, 1.30), 0.020, YELLOW)
    for z in (0.80, 1.30):
        tube(bm, (0.45, y_out - 0.08, z), (0.45, y_out + 0.01, z), 0.016, YELLOW)
    y_in = -R_IN + 0.02
    box(bm, 0.50, 0.66, y_in - 0.002, y_in + 0.05, 0.98, 1.12, STEEL, bevel=0.008, segs=1)
    tube(bm, (0.58, y_in + 0.05, 1.05), (0.30, y_in + 0.05, 1.05), 0.016, YELLOW)
    box(bm, -0.55, 0.40, y_in - 0.002, y_in + 0.012, 1.55, 1.90, PANEL, bevel=0.01, segs=1)
    for z in (0.35, 1.075, 1.80):
        lathe(bm, upright(-HATCH_W / 2 - 0.02, -R_OUT - 0.02, z - 0.10), [(0.0, 0.0), (0.035, 0.0), (0.035, 0.20), (0.0, 0.20)], 16, [STEEL] * 3)
    return bm


def cargo_cell(bm, side, z0, label):
    x0, x1 = sorted((side * RACK_X0, side * RACK_X1))
    front = side * RACK_X0
    for zz in (z0 + 0.02, z0 + CELL - 0.02):
        for xx in (x0 + 0.02, x1 - 0.02):
            box(bm, xx - 0.02, xx + 0.02, zz - 0.02, zz + 0.02, 0.0, 1.22, ALU, bevel=0.004, segs=1)
    for h in SHELVES + (1.20,):
        box(bm, x0, x1, z0 + 0.01, z0 + CELL - 0.01, h - 0.025, h, GRAPHITE, bevel=0.004, segs=1)
    box(bm, (x1 if side < 0 else x0) - 0.012, (x1 if side < 0 else x0) + 0.012, z0 + 0.01, z0 + CELL - 0.01, 0.0, 1.22, PANEL, bevel=0.0, segs=1) if False else None
    back = x0 if side < 0 else x1
    box(bm, back - 0.012, back + 0.012, z0, z0 + CELL, 0.0, 1.22, PANEL, bevel=0.003, segs=1)
    for dz in (0.14, CELL - 0.14):
        box(bm, front - 0.006, front + 0.006, z0 + dz - 0.022, z0 + dz + 0.022, 0.10, 1.17, STRAP if label is None else YELLOW, bevel=0.0, segs=1)
    box(bm, front - 0.007, front + 0.007, z0 + 0.03, z0 + CELL - 0.03, 0.62, 0.66, YELLOW, bevel=0.0, segs=1)


def interior():
    bm = new_bm()
    for k in range(-4, 4):
        z0 = k * 0.60
        if z0 + 0.60 < -R_IN or z0 > R_IN:
            continue
        box(bm, -CORRIDOR + 0.02, CORRIDOR - 0.02, max(z0 + 0.01, -2.30), min(z0 + 0.59, 2.10), 0.0, 0.012, DECKM, bevel=0.003, segs=1)
    for side in (-1.0, 1.0):
        x = side * (CORRIDOR + 0.01)
        box(bm, x - 0.02, x + 0.02, -2.30, 1.05, 0.0, 0.014, YELLOW, bevel=0.0, segs=1)
    for i, name in enumerate(LEFT_CELLS):
        cargo_cell(bm, -1.0, CELL_Z0 + CELL * i, name)
    for i, name in enumerate(RIGHT_CELLS):
        cargo_cell(bm, 1.0, CELL_Z0 + CELL * i, name)
    for side, z_end in ((-1.0, CELL_Z0 + CELL * 4), (1.0, CELL_Z0 + CELL * 3)):
        x0, x1 = sorted((side * RACK_X1, side * 2.05))
        box(bm, x0, x1, CELL_Z0 + 0.25, z_end - 0.05, 0.0, 1.55, PANEL, bevel=0.02, segs=2)
        tube(bm, (side * 0.70, -2.10, 1.55), (side * 0.70, CELL_Z0, 1.55), 0.020, YELLOW, caps=True)
    seat(bm)
    return bm


def seat(bm):
    zc = SEAT_Z
    lathe(bm, upright(0.0, zc, 0.0), [(0.0, 0.0), (0.30, 0.0), (0.30, 0.03), (0.09, 0.05), (0.09, 0.40), (0.0, 0.40)], 24, [GRAPHITE] * 5)
    pan = Matrix.Translation((0.0, zc - 0.05, 0.52)) @ Matrix.Rotation(radians(-8.0), 4, "X")
    box_m(bm, pan, (0.56, 0.52, 0.10), FABRIC, bevel=0.03, segs=3)
    back = Matrix.Translation((0.0, zc + 0.20, 0.55)) @ Matrix.Rotation(radians(-30.0), 4, "X") @ Matrix.Translation((0.0, 0.0, 0.42))
    box_m(bm, back, (0.54, 0.10, 0.84), FABRIC, bevel=0.03, segs=3)
    box_m(bm, back @ Matrix.Translation((0.0, 0.07, 0.0)), (0.58, 0.05, 0.88), GRAPHITE, bevel=0.015, segs=2)
    box_m(bm, back @ Matrix.Translation((0.0, -0.01, 0.50)), (0.30, 0.12, 0.20), FABRIC, bevel=0.03, segs=3)
    for side in (-1.0, 1.0):
        box(bm, side * 0.30 - 0.03, side * 0.30 + 0.03, zc - 0.28, zc + 0.20, 0.60, 0.66, GRAPHITE, bevel=0.01, segs=2)
        for local in ((side * 0.15, -0.06, 0.40), (side * 0.11, -0.06, 0.10)):
            box_m(bm, back @ Matrix.Translation((local[0], -0.06, local[2])), (0.05, 0.012, 0.32), STRAP, bevel=0.0, segs=1)
    box_m(bm, pan @ Matrix.Translation((0.0, -0.20, 0.06)), (0.10, 0.03, 0.10), STEEL, bevel=0.01, segs=1)
    box_m(bm, pan @ Matrix.Translation((0.0, -0.215, 0.06)), (0.05, 0.01, 0.05), STRIPE, bevel=0.0, segs=1)


def stack():
    """Service module, interstage, booster with bands and grid fins, engine section, legs and engine bell."""
    bm = new_bm()
    o = upright(0.0, 0.0, 0.0)
    lathe(bm, o, [(0.0, SERVICE_TOP), (SERVICE_R - 0.05, SERVICE_TOP), (SERVICE_R, SERVICE_TOP - 0.05), (SERVICE_R, SERVICE_BOTTOM), (0.0, SERVICE_BOTTOM)], 48,
          [PANEL, PANEL, PANEL, PANEL])
    lathe(bm, o, [(BOOSTER_R, SERVICE_BOTTOM), (BOOSTER_R, INTER_BOTTOM)], 48, [BLACK])
    lathe(bm, o, [(BOOSTER_R, INTER_BOTTOM), (BOOSTER_R, -8.60), (BOOSTER_R + 0.004, -8.60), (BOOSTER_R + 0.004, -10.40), (BOOSTER_R, -10.40),
                  (BOOSTER_R, BOOSTER_BOTTOM)], 48, [HULL, HULL, BLACK, HULL, HULL])
    lathe(bm, o, [(BOOSTER_R, BOOSTER_BOTTOM), (BOOSTER_R, ENGINE_BOTTOM), (0.0, ENGINE_BOTTOM)], 48, [BLACK, BLACK])
    lathe(bm, o, [(0.0, ENGINE_BOTTOM), (0.42, ENGINE_BOTTOM), (0.46, ENGINE_BOTTOM - 0.10), (0.95, BELL_EXIT), (0.90, BELL_EXIT), (0.42, ENGINE_BOTTOM - 0.14)], 32,
          [STEEL, STEEL, STEEL, DARK, DARK])
    for side in (-1.0, 1.0):
        box(bm, side * (SERVICE_R - 0.02) - 0.14, side * (SERVICE_R - 0.02) + 0.14, -0.60, 0.60, -3.10, -0.80, PANEL, bevel=0.02, segs=1)
    box(bm, BOOSTER_R - 0.02, BOOSTER_R + 0.16, -0.20, 0.20, BOOSTER_BOTTOM + 0.6, INTER_BOTTOM - 0.4, PANEL, bevel=0.03, segs=1)
    for az in (45.0, 135.0, 225.0, 315.0):
        m = az_frame(az, BOOSTER_R, -5.20)
        box_m(bm, m @ Matrix.Translation((0.0, 0.30, 0.0)), (0.70, 0.55, 0.06), GRAPHITE, bevel=0.01, segs=1)
        top = az_frame(az, BOOSTER_R - 0.10, BOOSTER_BOTTOM + 1.6).translation
        foot = az_frame(az, 3.50, DECK_Y + 1.00).translation
        tube(bm, top, foot, 0.13, BLACK, segs=12)
        tube(bm, az_frame(az, BOOSTER_R - 0.05, ENGINE_BOTTOM + 0.2).translation, foot, 0.09, GRAPHITE, segs=10)
        lathe(bm, upright(foot.x, foot.y, foot.z - 0.10), [(0.0, 0.0), (0.42, 0.0), (0.42, 0.10), (0.0, 0.12)], 20, [BLACK, BLACK, BLACK])
    return bm


def barge():
    bm = new_bm()
    b = BARGE
    box(bm, b["x0"], b["x1"], b["z0"], b["z1"], b["bottom"], DECK_Y - 0.05, BARGEM, bevel=0.35, segs=2)
    box(bm, b["x0"] + 0.25, b["x1"] - 0.25, b["z0"] + 0.25, b["z1"] - 0.25, DECK_Y - 0.06, DECK_Y, DECKM, bevel=0.02, segs=1)
    for az in (45.0, 135.0, 225.0, 315.0):
        p = az_frame(az, 3.50, DECK_Y).translation
        box(bm, p.x - 0.45, p.x + 0.45, p.y - 0.45, p.y + 0.45, DECK_Y, DECK_Y + 0.88, STEEL, bevel=0.03, segs=1)
    prism_x(bm, -1.8, 1.8, [(-1.8, DECK_Y), (0.0, DECK_Y + 0.9), (1.8, DECK_Y)], DARK)
    xs = [b["x0"] + 0.6 + 2.0 * k for k in range(int((b["x1"] - b["x0"] - 1.2) / 2.0) + 1)]
    zs = [b["z0"] + 0.6 + 2.0 * k for k in range(int((b["z1"] - b["z0"] - 1.2) / 2.0) + 1)]
    edge = []
    for x in xs:
        edge += [(x, b["z0"] + 0.6), (x, b["z1"] - 0.6)]
    for z in zs:
        edge += [(b["x0"] + 0.6, z), (b["x1"] - 0.6, z)]
    for x, z in edge:
        tube(bm, (x, z, DECK_Y), (x, z, DECK_Y + 1.10), 0.030, YELLOW, segs=8)
    for (x0, z0), (x1, z1) in (((b["x0"] + 0.6, b["z0"] + 0.6), (b["x1"] - 0.6, b["z0"] + 0.6)), ((b["x0"] + 0.6, b["z1"] - 0.6), (b["x1"] - 0.6, b["z1"] - 0.6)),
                               ((b["x0"] + 0.6, b["z0"] + 0.6), (b["x0"] + 0.6, b["z1"] - 0.6)), ((b["x1"] - 0.6, b["z0"] + 0.6), (b["x1"] - 0.6, b["z1"] - 0.6))):
        for h in (0.55, 1.10):
            tube(bm, (x0, z0, DECK_Y + h), (x1, z1, DECK_Y + h), 0.028, YELLOW, segs=8)
    for x, z in ((b["x0"] + 1.6, b["z0"] + 1.6), (b["x1"] - 1.6, b["z0"] + 1.6), (b["x0"] + 1.6, b["z1"] - 1.6), (b["x1"] - 1.6, b["z1"] - 1.6)):
        lathe(bm, upright(x, z, DECK_Y), [(0.0, 0.0), (0.30, 0.0), (0.30, 0.06), (0.18, 0.10), (0.18, 0.45), (0.26, 0.52), (0.0, 0.54)], 16, [STEEL] * 6)
    for k in range(7):
        x = b["x0"] + 3.0 + k * 5.0
        for z, sign in ((b["z0"] - 0.05, -1.0), (b["z1"] + 0.05, 1.0)):
            m = Matrix.Translation((x, z + sign * 0.30, DECK_Y - 1.4)) @ RXM90
            lathe(bm, m, [(0.0, -0.22), (0.45, -0.22), (0.55, -0.12), (0.55, 0.12), (0.45, 0.22), (0.0, 0.22)], 20, [RUBBER] * 5)
    for x, z in ((b["x0"] + 3.0, b["z0"] + 1.4), (b["x1"] - 3.0, b["z0"] + 1.4), (b["x0"] + 3.0, b["z1"] - 1.4), (b["x1"] - 3.0, b["z1"] - 1.4)):
        tube(bm, (x, z, DECK_Y), (x, z, DECK_Y + 3.2), 0.06, STEEL, segs=10)
        box(bm, x - 0.18, x + 0.18, z - 0.18, z + 0.18, DECK_Y + 3.2, DECK_Y + 3.45, GRAPHITE, bevel=0.02, segs=1)
    box(bm, 6.0, 7.4, -9.0, -8.2, DECK_Y, DECK_Y + 1.3, STRIPE, bevel=0.04, segs=1)
    box(bm, 4.4, 5.6, -9.0, -8.3, DECK_Y, DECK_Y + 1.0, GRAPHITE, bevel=0.04, segs=1)
    return bm


def mast():
    bm = new_bm()
    cx, cz, h = MAST["cx"], MAST["cz"], MAST["half"]
    for dx in (-h, h):
        for dz in (-h, h):
            box(bm, cx + dx - 0.16, cx + dx + 0.16, cz + dz - 0.16, cz + dz + 0.16, DECK_Y, MAST["top"], STEEL, bevel=0.02, segs=1)
    box(bm, cx - h + 0.10, cx + h - 0.10, cz - h + 0.10, cz + h - 0.10, DECK_Y, -0.30, PANEL, bevel=0.03, segs=1)
    y = DECK_Y + 3.0
    while y < -0.5:
        box(bm, cx - h - 0.05, cx + h + 0.05, cz - h - 0.05, cz + h + 0.05, y, y + 0.18, GRAPHITE, bevel=0.01, segs=1)
        y += 3.0
    box(bm, cx - 0.55, cx + 0.55, cz - h - 0.02, cz - h + 0.06, DECK_Y, DECK_Y + 2.1, GRAPHITE, bevel=0.02, segs=1)
    box(bm, -2.2, 2.2, -11.2, ARM_Z0, -0.30, 0.0, DECKM, bevel=0.02, segs=1)
    for x0, z0, x1, z1 in ((-2.2, -11.2, -2.2, ARM_Z0), (2.2, -11.2, 2.2, ARM_Z0), (-2.2, -11.2, -1.1, -11.2), (1.1, -11.2, 2.2, -11.2),
                           (-2.2, ARM_Z0, -ARM_HALF - 0.15, ARM_Z0), (ARM_HALF + 0.15, ARM_Z0, 2.2, ARM_Z0)):
        for hh in (0.55, 1.10):
            tube(bm, (x0, z0, hh), (x1, z1, hh), 0.028, YELLOW, segs=8)
        for x, z in ((x0, z0), (x1, z1)):
            tube(bm, (x, z, 0.0), (x, z, 1.10), 0.030, YELLOW, segs=8)
    box(bm, cx - h - 0.1, cx + h + 0.1, cz - h - 0.1, cz + h + 0.1, MAST["top"] - 0.25, MAST["top"], GRAPHITE, bevel=0.03, segs=1)
    lathe(bm, upright(cx + h, cz + h, MAST["top"]), [(0.0, 0.0), (0.04, 0.0), (0.04, 0.80), (0.0, 0.80)], 10, [STEEL] * 3)
    box(bm, cx + h - 0.15, cx + h + 0.15, cz + h - 0.15, cz + h + 0.15, MAST["top"] + 0.80, MAST["top"] + 1.05, GRAPHITE, bevel=0.02, segs=1)
    return bm


def access_arm():
    """Swing walkway from the mast to the hatch; floor level with the capsule floor. Pivot: AccessArmPivot."""
    bm = new_bm()
    throat = -3.10
    box(bm, -ARM_HALF, ARM_HALF, ARM_Z0, throat, -0.06, 0.0, DECKM, bevel=0.01, segs=1)
    box(bm, -0.75, 0.75, throat, ARM_Z1, -0.06, 0.0, DECKM, bevel=0.01, segs=1)
    box(bm, -0.70, 0.70, ARM_Z1 - 0.02, -R_OUT + 0.02, -0.03, 0.0, STEEL, bevel=0.004, segs=1)
    for side in (-1.0, 1.0):
        x = side * ARM_HALF
        box(bm, x - 0.08, x + 0.08, ARM_Z0, throat, -0.55, -0.06, STEEL, bevel=0.01, segs=1)
        for z in [ARM_Z0 + 0.05 + 0.95 * k for k in range(5)]:
            tube(bm, (x, z, 0.0), (x, z, 1.10), 0.030, YELLOW, segs=8)
        for hh in (0.55, 1.10):
            tube(bm, (x, ARM_Z0, hh), (x, throat, hh), 0.028, YELLOW, segs=8)
        for k in range(4):
            za = ARM_Z0 + 0.15 + k * 0.95
            tube(bm, (x, za, -0.50), (x, za + 0.95, -0.08), 0.03, STEEL, segs=8)
    for k in range(3):
        z = ARM_Z0 + 0.4 + k * 1.3
        lathe(bm, upright(-ARM_HALF - 0.02, ARM_Z0 + 0.05, -0.55 + k * 0.6), [(0.0, 0.0), (0.06, 0.0), (0.06, 0.30), (0.0, 0.30)], 14, [STEEL] * 3)
    for x in (-0.55, 0.0, 0.55):
        box(bm, x - 0.18, x + 0.18, ARM_Z0 + 0.30, ARM_Z0 + 0.36, 0.0, 0.004, YELLOW, bevel=0.0, segs=1)
    return bm


def build_anchors():
    result = []
    for name, p in ANCHORS.items():
        obj = bpy.data.objects.new(name, None)
        obj.empty_display_type = "ARROWS"
        obj.empty_display_size = 0.25
        obj.location = unity_to_blender(p)
        bpy.context.scene.collection.objects.link(obj)
        result.append(obj)
    return result


def apply_cut(target, cutter):
    mod = target.modifiers.new("hatch_and_windows", "BOOLEAN")
    mod.operation = "DIFFERENCE"
    mod.solver = "EXACT"
    mod.object = cutter
    select_only([target])
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(cutter, do_unlink=True)


# ------------------------------------------------------------------ detail pass (design frame)
CONE_TILT = atan2(R_OUT - NOSE_R, CONE_TOP - CYL_TOP)          # cone wall angle from vertical


def cone_radius(h):
    return R_OUT + (NOSE_R - R_OUT) * (h - CYL_TOP) / (CONE_TOP - CYL_TOP)


def hull_details():
    bm = new_bm()
    o = upright(0.0, 0.0, 0.0)
    lathe(bm, o, [(R_OUT + 0.004, 2.40), (R_OUT + 0.004, 2.52)], 64, [STRIPE])
    for h in (2.36, 2.56):
        lathe(bm, o, [(R_OUT + 0.003, h), (R_OUT + 0.003, h + 0.012)], 64, [DARK])
    for h in (2.95, 3.90):
        r0, r1 = cone_radius(h) + 0.004, cone_radius(h + 0.014) + 0.004
        lathe(bm, o, [(r0, h), (r1, h + 0.014)], 64, [DARK])
    for az in (40.0, -40.0, 140.0, -140.0):
        m = az_frame(az, cone_radius(3.35) + 0.01, 3.35) @ Matrix.Rotation(CONE_TILT, 4, "X")
        for x, z, sx, sz in ((0.0, 0.37, 0.62, 0.06), (0.0, -0.37, 0.62, 0.06), (0.27, 0.0, 0.06, 0.80), (-0.27, 0.0, 0.06, 0.80)):
            box_m(bm, m @ Matrix.Translation((x, 0.0, z)), (sx, 0.035, sz), PANEL, bevel=0.008, segs=1)
    for az in (0.0, 120.0, 240.0):
        box_m(bm, az_frame(az, 0.70, DOCK_TOP + 0.07), (0.20, 0.06, 0.16), ALU, bevel=0.01, segs=1)
    for az in (95.0, 265.0):
        m = az_frame(az, R_OUT, 1.10)
        box_m(bm, m @ Matrix.Translation((0.0, 0.03, 0.0)), (0.34, 0.06, 0.24), PANEL, bevel=0.02, segs=1)
        for dx in (-0.12, 0.12):
            box_m(bm, m @ Matrix.Translation((dx, 0.065, 0.0)), (0.025, 0.02, 0.18), DARK, bevel=0.0, segs=1)
    return bm


def interior_details():
    bm = new_bm()
    for az in range(0, 360, 30):
        if 150 <= az <= 210:
            continue
        box_m(bm, az_frame(float(az), R_IN - 0.02, 1.27), (0.08, 0.04, 2.48), ALU, bevel=0.006, segs=1)
    for x in (-0.95, 0.95):
        box(bm, x - 0.12, x + 0.12, -1.95, 1.55, 2.40, 2.46, GRAPHITE, bevel=0.006, segs=1)
    for az in (90.0, 270.0):
        m = az_frame(az, R_IN - 0.03, 2.15)
        box_m(bm, m, (0.50, 0.04, 0.22), GRAPHITE, bevel=0.01, segs=1)
        for k in range(5):
            box_m(bm, m @ Matrix.Translation((0.0, -0.022, -0.08 + 0.04 * k)), (0.42, 0.006, 0.018), DARK, bevel=0.0, segs=1)
    m = az_frame(205.0, R_IN - 0.10, 0.0)
    box_m(bm, m @ Matrix.Translation((0.0, 0.06, 1.12)), (0.18, 0.04, 0.10), GRAPHITE, bevel=0.01, segs=1)
    lathe(bm, m @ Matrix.Translation((0.0, -0.02, 0.88)), [(0.0, 0.0), (0.075, 0.0), (0.075, 0.38), (0.05, 0.44), (0.0, 0.45)], 18, [STRIPE] * 4)
    lathe(bm, m @ Matrix.Translation((0.0, -0.02, 1.33)), [(0.0, 0.0), (0.02, 0.0), (0.02, 0.06), (0.0, 0.06)], 10, [DARK] * 3)
    status = az_frame(160.0, R_IN - 0.01, 1.40)
    box_m(bm, status, (0.24, 0.03, 0.22), DARK, bevel=0.01, segs=1)
    for side in (-1.0, 1.0):
        box(bm, side * 0.36 - 0.03, side * 0.36 + 0.03, SEAT_Z - 0.32, SEAT_Z - 0.22, 0.66, 0.70, KEYCAP, bevel=0.006, segs=1)
    return bm


def light_panels():
    bm = new_bm()
    for z in (-1.4, -0.4, 0.6):
        box(bm, -0.45, 0.45, z - 0.18, z + 0.18, 2.47, 2.50, 0, bevel=0.01, segs=1)
    for az in (60.0, -60.0):
        box_m(bm, az_frame(az, R_IN - 0.02, 2.10), (0.30, 0.03, 0.08), 0, bevel=0.005, segs=1)
    return bm


def status_lamp():
    bm = new_bm()
    m = az_frame(160.0, R_IN - 0.03, 1.36) @ Matrix.Rotation(pi, 4, "Z")
    lathe(bm, m @ RXM90, [(0.0, 0.0), (0.018, 0.0), (0.018, 0.008), (0.010, 0.014), (0.0, 0.015)], 14, [0, 0, 0, 0])
    return bm


def deck_lamp_lenses():
    bm = new_bm()
    for x, z in ((BARGE["x0"] + 3.0, BARGE["z0"] + 1.4), (BARGE["x1"] - 3.0, BARGE["z0"] + 1.4), (BARGE["x0"] + 3.0, BARGE["z1"] - 1.4),
                 (BARGE["x1"] - 3.0, BARGE["z1"] - 1.4)):
        box(bm, x - 0.15, x + 0.15, z - 0.15, z + 0.15, DECK_Y + 3.15, DECK_Y + 3.21, 0, bevel=0.01, segs=1)
    cx, cz, h = MAST["cx"], MAST["cz"], MAST["half"]
    box(bm, cx + h - 0.12, cx + h + 0.12, cz + h - 0.12, cz + h + 0.12, MAST["top"] + 0.74, MAST["top"] + 0.80, 0, bevel=0.01, segs=1)
    return bm


# ------------------------------------------------------------------ labels on arbitrary frames
LABELS = []


def label(key, host, centre, normal, w, h, style, texts, up=(0.0, 0.0, 1.0), scale=1.0):
    """scale < 1 stores a big label smaller in the atlas; the quad keeps its real size (q)."""
    texts = [(t, dx * scale, dz * scale, size * scale) for t, dx, dz, size in texts]
    LABELS.append(dict(key=key, host=host, c=Vector(centre), n=Vector(normal).normalized(), u=Vector(up).normalized(),
                       s=(w * scale, h * scale), q=(w, h), style=style, texts=texts, shapes=()))


def label_items():
    LABELS.clear()
    names = {"oxygen": "OXYGEN", "water": "WATER", "food": "FOOD", "battery": "BATTERY", "solar": "SOLAR", "spares": "SPARES", "science": "SCIENCE"}
    for side, cells in ((-1.0, LEFT_CELLS), (1.0, RIGHT_CELLS)):
        for i, name in enumerate(cells):
            zc = CELL_Z0 + CELL * (i + 0.5)
            label("bay_" + name, "CapsuleInterior", (side * (RACK_X0 - 0.008), zc, 1.135), (-side, 0.0, 0.0), 0.40, 0.050, "dark", [(names[name], 0.0, 0.0, 0.024)], scale=0.5)
    y_in = -R_IN + 0.02 + 0.012
    label("hatch_in", "Hatch", (-0.075, y_in, 1.725), (0.0, 1.0, 0.0), 0.86, 0.26, "light",
          [("HATCH", 0.0, 0.055, 0.060), ("LIFT HANDLE AND PULL TO SECURE", 0.0, -0.055, 0.030)], scale=0.35)
    p = az_frame(160.0, R_IN - 0.026, 1.465).translation
    label("hatch_secure", "CapsuleInterior", p, -(az_frame(160.0, 1.0, 0.0).to_3x3() @ Vector((0.0, 1.0, 0.0))), 0.20, 0.045, "dark", [("HATCH SECURE", 0.0, 0.0, 0.020)])
    label("release", "Hatch", (0.45, -R_OUT - 0.045, 1.52), (0.0, -1.0, 0.0), 0.34, 0.11, "caution",
          [("EMERGENCY", 0.0, 0.022, 0.028), ("HATCH RELEASE", 0.0, -0.022, 0.026)], scale=0.6)
    p = az_frame(208.0, R_OUT + 0.006, 2.10).translation
    label("tv1", "CapsuleShell", p, az_frame(208.0, 1.0, 0.0).to_3x3() @ Vector((0.0, 1.0, 0.0)), 0.60, 0.20, "light", [("TV-1", 0.0, 0.0, 0.14)], scale=0.25)
    label("keep_clear", "LaunchBarge", (0.0, -5.6, DECK_Y + 0.002), (0.0, 0.0, 1.0), 3.0, 0.50, "caution", [("KEEP CLEAR OF LAUNCH MOUNT", 0.0, 0.0, 0.22)], up=(0.0, 1.0, 0.0), scale=0.0625)
    return LABELS


def build_labels_m(items, material):
    """Quads on arbitrary frames: right = up x normal, the face looks along the normal."""
    result = {}
    for host in sorted({p["host"] for p in items}):
        bm = bmesh.new()
        uv = bm.loops.layers.uv.new("UVMap")
        for item in (p for p in items if p["host"] == host):
            n, u = item["n"], item["u"]
            r = u.cross(n).normalized()
            u = n.cross(r).normalized()
            c = item["c"] + n * 0.0008
            w, h = item.get("q", item["s"])
            x, v, pw, ph = item["px"]
            u0, v0, u1, v1 = x / ATLAS_W, v / ATLAS_H, (x + pw) / ATLAS_W, (v + ph) / ATLAS_H
            corners = [(c - r * w / 2 - u * h / 2, (u0, v0)), (c + r * w / 2 - u * h / 2, (u1, v0)),
                       (c + r * w / 2 + u * h / 2, (u1, v1)), (c - r * w / 2 + u * h / 2, (u0, v1))]
            face = bm.faces.new([bm.verts.new(p) for p, _ in corners])
            for loop, (_p, coords) in zip(face.loops, corners):
                loop[uv].uv = coords
        bmesh.ops.transform(bm, matrix=R180, verts=list(bm.verts))
        mesh = bpy.data.meshes.new(NAME + "_Labels" + host)
        bm.to_mesh(mesh)
        bm.free()
        mesh.materials.append(material)
        obj = bpy.data.objects.new("Labels" + host, mesh)
        bpy.context.scene.collection.objects.link(obj)
        result[host] = obj
    return result


# ------------------------------------------------------------------ collider proxies (manifest only; Unity metres)
def collider_proxies():
    boxes = []

    def add(name, parent, centre, size, yaw=0.0):
        boxes.append({"name": name, "parent": parent, "center": [round(v, 4) for v in centre], "size": [round(v, 4) for v in size], "yaw_deg": round(yaw, 2)})

    add("CabinFloor", "CapsuleInterior", (0.0, -0.05, 0.0), (4.6, 0.10, 4.6))
    for az in range(0, 360, 20):
        if 160 <= az <= 200:
            continue
        a = radians(az)
        add("CabinWall_%03d" % az, "CapsuleShell", ((R_IN + 0.06) * sin(a), 1.30, (R_IN + 0.06) * cos(a)), (0.86, 2.60, 0.12), az)
    for side in (-1.0, 1.0):
        x = side * (HATCH_W / 2 + 0.25)
        add("HatchJamb" + ("L" if side < 0 else "R"), "CapsuleShell", (x, 1.30, -R_IN - 0.06), (0.50, 2.60, 0.14))
    add("HatchHeader", "CapsuleShell", (0.0, HATCH_H + 0.20, -R_IN - 0.06), (HATCH_W, 0.40, 0.14))
    add("CabinCeiling", "CapsuleShell", (0.0, 2.55, 0.0), (4.6, 0.10, 4.6))
    add("RackLeft", "CapsuleInterior", (-(RACK_X0 + 2.05) / 2, 0.78, CELL_Z0 + CELL * 2), (2.05 - RACK_X0, 1.56, CELL * 4))
    add("RackRight", "CapsuleInterior", ((RACK_X0 + 2.05) / 2, 0.78, CELL_Z0 + CELL * 1.5), (2.05 - RACK_X0, 1.56, CELL * 3))
    add("Seat", "CapsuleInterior", (0.0, 0.60, SEAT_Z + 0.20), (0.64, 1.20, 0.70))
    add("HatchDoor", "Hatch", (0.0, HATCH_H / 2, -(R_IN + R_OUT) / 2), (HATCH_W - 0.04, HATCH_H - 0.04, 0.16))
    add("ArmFloor", "AccessArm", (0.0, -0.03, (ARM_Z0 + ARM_Z1) / 2), (1.80, 0.06, ARM_Z1 - ARM_Z0))
    for side in (-1.0, 1.0):
        add("ArmRail" + ("L" if side < 0 else "R"), "AccessArm", (side * ARM_HALF, 0.60, (ARM_Z0 - 3.10) / 2), (0.08, 1.20, -3.10 - ARM_Z0))
    add("MastLanding", "AccessMast", (0.0, -0.15, (-11.2 + ARM_Z0) / 2), (4.4, 0.30, ARM_Z0 + 11.2))
    add("MastTower", "AccessMast", (MAST["cx"], (DECK_Y - 0.30) / 2, MAST["cz"]), (2 * MAST["half"] + 0.3, -0.30 - DECK_Y, 2 * MAST["half"] + 0.3))
    add("Booster", "BoosterStack", (0.0, (SERVICE_TOP + ENGINE_BOTTOM) / 2, 0.0), (2 * SERVICE_R, SERVICE_TOP - ENGINE_BOTTOM, 2 * SERVICE_R))
    add("BargeDeck", "LaunchBarge", (0.0, DECK_Y - 0.5, (BARGE["z0"] + BARGE["z1"]) / 2), (BARGE["x1"] - BARGE["x0"], 1.0, BARGE["z1"] - BARGE["z0"]))
    return boxes


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
    scene = bpy.data.scenes.new("TL_LabelAtlas")
    made = []
    colors = {}
    font = load_label_font()

    def color_mat(hex_color):
        if hex_color not in colors:
            colors[hex_color] = emission_material("TLatlas_" + hex_color.lstrip("#"), hex_color)
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
            curve = bpy.data.curves.new("TLatlas_text", "FONT")
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
                obj = bpy.data.objects.new("TLatlas_text", curve)
                obj.scale = (0.01, 0.01, 0.01)
                obj.location = (x + w / 2 + dx + ox * shift, y + h / 2 + dz - size * TEXT_DROP + oy * shift, 0.002)
                scene.collection.objects.link(obj)
                made.append(obj)

    for hex_color, faces in quads.items():
        bm = bmesh.new()
        for corners in faces:
            bm.faces.new([bm.verts.new(c) for c in corners])
        mesh = bpy.data.meshes.new("TLatlas_fill")
        bm.to_mesh(mesh)
        bm.free()
        mesh.materials.append(color_mat(hex_color))
        obj = bpy.data.objects.new("TLatlas_fill", mesh)
        scene.collection.objects.link(obj)
        made.extend((obj, mesh))

    camera_data = bpy.data.cameras.new("TLatlas_cam")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = max(ATLAS_W, ATLAS_H) * px
    camera = bpy.data.objects.new("TLatlas_cam", camera_data)
    camera.location = (ATLAS_W * px / 2, ATLAS_H * px / 2, 1.0)
    scene.collection.objects.link(camera)
    scene.camera = camera
    made.extend((camera, camera_data))

    world = bpy.data.worlds.new("TLatlas_world")
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



def shrink_hidden_faces(obj):
    return None


STYLES.update({"cream": dict(bg="#D9D8D1", fg="#2B2B2C", line=None)})
BS = chr(92)


def to_unity(v):
    return (-v[0], v[2], -v[1])


def unity_bounds(objs):
    lo, hi = world_bounds(objs)
    a, b = to_unity(lo), to_unity(hi)
    return [round(min(x, y), 3) for x, y in zip(a, b)], [round(max(x, y), 3) for x, y in zip(a, b)]


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


def make_emissive(name, base_hex, emit_hex, strength):
    mat = bpy.data.materials.new(name)
    nodes_on(mat)
    node = principled(mat)
    node.inputs["Base Color"].default_value = hex_rgba(base_hex)
    node.inputs["Roughness"].default_value = 0.30
    set_input(node, ("Emission Color", "Emission"), hex_rgba(emit_hex))
    set_input(node, ("Emission Strength",), strength)
    return mat


def final_material(atlas, paths):
    """Blender preview of the delivered maps: BaseColor, and metallic / roughness read back from MetallicSmoothness."""
    mat = bpy.data.materials.new("TL_" + atlas)
    nodes_on(mat)
    tree = mat.node_tree
    bsdf = principled(mat)
    base = tree.nodes.new("ShaderNodeTexImage")
    base.image = load_image(paths["BaseColor"], False)
    tree.links.new(base.outputs["Color"], bsdf.inputs["Base Color"])
    ms = tree.nodes.new("ShaderNodeTexImage")
    ms.image = load_image(paths["MetallicSmoothness"], True)
    ms.image.alpha_mode = "CHANNEL_PACKED"
    sep = tree.nodes.new("ShaderNodeSeparateColor")
    tree.links.new(ms.outputs["Color"], sep.inputs["Color"])
    tree.links.new(sep.outputs[0], bsdf.inputs["Metallic"])
    inv = tree.nodes.new("ShaderNodeMath")
    inv.operation = "SUBTRACT"
    inv.inputs[0].default_value = 1.0
    tree.links.new(ms.outputs["Alpha"], inv.inputs[1])
    tree.links.new(inv.outputs[0], bsdf.inputs["Roughness"])
    return mat


def bake_atlas(atlas, objs):
    import numpy as np
    unwrap(objs)
    imgs = {k: new_image("TLbake_%s_%s" % (atlas, k), k != "color") for k in ("color", "metal", "rough", "ao")}
    together = [(o, []) for o in objs]
    for key, samples in (("color", 48), ("metal", 32), ("rough", 32), ("ao", 96)):
        bake_pass(key, imgs[key], together, samples)
    count = TEX * TEX * 4
    m = np.empty(count, dtype=np.float32)
    r = np.empty(count, dtype=np.float32)
    imgs["metal"].pixels.foreach_get(m)
    imgs["rough"].pixels.foreach_get(r)
    packed = np.empty(count, dtype=np.float32)
    packed[0::4] = m[0::4]
    packed[1::4] = m[0::4]
    packed[2::4] = m[0::4]
    packed[3::4] = 1.0 - r[0::4]
    ms = bpy.data.images.new("TLbake_%s_ms" % atlas, TEX, TEX, alpha=True)
    ms.colorspace_settings.name = "Non-Color"
    ms.alpha_mode = "CHANNEL_PACKED"
    ms.pixels.foreach_set(packed)
    paths = {"BaseColor": save_image(imgs["color"], "%s_%s_BaseColor.png" % (NAME, atlas)),
             "MetallicSmoothness": save_image(ms, "%s_%s_MetallicSmoothness.png" % (NAME, atlas)),
             "Occlusion": save_image(imgs["ao"], "%s_%s_Occlusion.png" % (NAME, atlas))}
    for image in list(imgs.values()) + [ms]:
        bpy.data.images.remove(image)
    log("atlas", atlas, "baked for", [o.name for o in objs])
    return paths


def scrub_blend(path):
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


def build_render_set(scene, collection):
    """Render-only: dusk sky, a sea plane at the waterline, sun, cabin and deck lights. Not exported."""
    world = bpy.data.worlds.new("TL_World")
    nodes_on(world)
    tree = world.node_tree
    sky = tree.nodes.new("ShaderNodeTexSky")
    try:
        sky.sky_type = "NISHITA"
        sky.sun_elevation = radians(3.0)
        sky.sun_rotation = radians(200.0)
        sky.air_density = 1.6
        sky.dust_density = 2.0
    except (AttributeError, TypeError):
        pass
    tree.links.new(sky.outputs["Color"], tree.nodes["Background"].inputs["Color"])
    tree.nodes["Background"].inputs["Strength"].default_value = 0.16
    scene.world = world
    sea = bpy.data.materials.new("TL_Sea")
    nodes_on(sea)
    principled(sea).inputs["Base Color"].default_value = hex_rgba("#0D2230")
    principled(sea).inputs["Roughness"].default_value = 0.12
    g = Graph(sea)
    noise = g.noise(g.nodes.new("ShaderNodeTexCoord").outputs["Object"], 1.2, 6.0)
    bump = g.nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.35
    g.links.new(noise, bump.inputs["Height"])
    g.links.new(bump.outputs["Normal"], principled(sea).inputs["Normal"])
    bm = bmesh.new()
    y = BARGE["bottom"] + 1.4
    bm.faces.new([bm.verts.new(p) for p in ((-400.0, -400.0, y), (400.0, -400.0, y), (400.0, 400.0, y), (-400.0, 400.0, y))])
    mesh = bpy.data.meshes.new("TL_SeaMesh")
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(sea)
    collection.objects.link(bpy.data.objects.new("TL_Sea", mesh))
    sun = bpy.data.lights.new("TL_Sun", "SUN")
    sun.energy, sun.color, sun.angle = 2.6, (1.0, 0.78, 0.55), radians(2.0)
    obj = bpy.data.objects.new("TL_Sun", sun)
    obj.rotation_euler = (radians(78.0), 0.0, radians(205.0))
    collection.objects.link(obj)

    def point(name, unity_location, power, color, radius=0.10):
        data = bpy.data.lights.new(name, "POINT")
        data.energy, data.color, data.shadow_soft_size = power, color, radius
        light = bpy.data.objects.new(name, data)
        light.location = unity_to_blender(unity_location)
        collection.objects.link(light)

    for z in (-1.4, -0.4, 0.6):
        point("TL_Cabin%.1f" % z, (0.0, 2.30, z), 60.0, (1.0, 0.93, 0.82), 0.3)
    point("TL_Seat", (0.0, 2.2, 1.5), 35.0, (1.0, 0.92, 0.80), 0.3)
    for x, z in ((BARGE["x0"] + 3.0, BARGE["z0"] + 1.4), (BARGE["x1"] - 3.0, BARGE["z0"] + 1.4), (BARGE["x0"] + 3.0, BARGE["z1"] - 1.4),
                 (BARGE["x1"] - 3.0, BARGE["z1"] - 1.4)):
        point("TL_Deck", (x, DECK_Y + 3.0, z), 400.0, (1.0, 0.80, 0.55), 0.2)
    point("TL_Arm", (1.2, 2.6, -4.6), 120.0, (1.0, 0.85, 0.65), 0.2)
    point("TL_MastTop", (MAST["cx"] + MAST["half"], MAST["top"] + 1.2, MAST["cz"] + MAST["half"]), 300.0, (1.0, 0.80, 0.55), 0.2)
    cargo = arg("--cargo")
    if cargo and os.path.exists(cargo):
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=cargo)
        case_col = bpy.data.collections.new("TL_PreviewCargoCase")
        for o in [o for o in bpy.data.objects if o not in before]:
            for c in list(o.users_collection):
                c.objects.unlink(o)
            case_col.objects.link(o)
        count = 0
        for name, p in ANCHORS.items():
            if not name.startswith("CargoBay_"):
                continue
            turn = radians(-90.0) if p[0] < 0 else radians(90.0)
            for slot in SHELVES[:2]:
                inst = bpy.data.objects.new("PreviewOnly case " + name, None)
                inst.instance_type = "COLLECTION"
                inst.instance_collection = case_col
                inst.location = unity_to_blender((p[0], slot, p[2]))
                inst.rotation_euler = (0.0, 0.0, turn)
                collection.objects.link(inst)
                count += 1
        log("preview cases placed:", count)
    cameras = {}

    def camera(key, unity_location, unity_target, lens=None, fov=None):
        data = bpy.data.cameras.new("TL_Cam_" + key)
        if fov is not None:
            data.sensor_fit = "VERTICAL"
            data.angle_y = radians(fov)
        else:
            data.lens = lens
        data.clip_start = 0.02
        data.clip_end = 1500.0
        cam = bpy.data.objects.new("TL_Cam_" + key, data)
        cam.location = unity_to_blender(unity_location)
        look_at(cam, unity_to_blender(unity_target))
        collection.objects.link(cam)
        cameras[key] = cam

    camera("boarding", (0.0, 1.65, -5.9), (0.0, 1.15, 0.0), fov=68.0)
    camera("exterior", (48.0, -6.0, -78.0), (0.0, -15.0, -2.0), lens=32.0)
    camera("interior", (0.0, 1.65, -2.05), (0.0, 0.95, 1.9), fov=68.0)
    camera("seated", ANCHORS["SeatedCamera"], (0.0, 0.85, -2.2), fov=68.0)
    camera("cargo", (0.40, 1.35, -1.05), (-0.98, 0.65, -0.15), lens=30.0)
    camera("ready", (13.0, 4.0, -17.0), (0.0, -1.5, -2.0), lens=40.0)
    for look in ("AgX - Medium High Contrast", "Medium High Contrast"):
        try:
            scene.view_settings.look = look
            break
        except TypeError:
            continue
    return cameras


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.render.engine = "CYCLES"
    device = enable_gpu(scene)
    full = STAGE == "full"
    log("Blender", bpy.app.version_string, "| stage", STAGE, "| device", device)
    kit = bpy.data.collections.new("TransportLaunch")
    scene.collection.children.link(kit)
    surface = [make_proc_material(k) if full else make_flat_material(k) for k in MAT_KEYS]
    if full:
        PROC.extend(surface)
    emissive = {name: make_emissive(name, *spec) for name, spec in EMISSIVE.items()}

    shell = finish(capsule_shell(), "CapsuleShell", surface)
    apply_cut(shell, finish(shell_cutters(), "TL_Cutter", surface))
    for extra in (shell_details(), hull_details()):
        part = finish(extra, "TL_ShellPart", surface)
        select_only([part, shell], active=shell)
        bpy.ops.object.join()
        shell = bpy.context.view_layer.objects.active
    inside = finish(interior(), "CapsuleInterior", surface)
    part = finish(interior_details(), "TL_InteriorPart", surface)
    select_only([part, inside], active=inside)
    bpy.ops.object.join()
    inside = bpy.context.view_layer.objects.active
    hatch = finish(hatch_door(), "Hatch", surface, origin=unity_to_blender(ANCHORS["HatchPivot"]))
    stack_obj = finish(stack(), "BoosterStack", surface)
    barge_obj = finish(barge(), "LaunchBarge", surface)
    mast_obj = finish(mast(), "AccessMast", surface)
    arm = finish(access_arm(), "AccessArm", surface, origin=unity_to_blender(ANCHORS["AccessArmPivot"]))
    lights = finish(light_panels(), "CabinLights", [emissive["TL_LightPanel"]])
    lamp = finish(status_lamp(), "HatchStatusLamp", [emissive["TL_StatusGreen"]])
    deck_lamps = finish(deck_lamp_lenses(), "DeckLamps", [emissive["TL_DeckLamp"]])
    for obj in (lights, lamp, deck_lamps):
        obj.data.uv_layers.new(name="UVMap")
    objects = {o.name: o for o in (shell, inside, hatch, stack_obj, barge_obj, mast_obj, arm)}
    anchors = build_anchors()
    report = {"blender": bpy.app.version_string, "stage": STAGE, "cycles_device": device}

    if full:
        items = label_items()
        pack_atlas(items)
        labels_path = os.path.join(TEX_DIR, NAME + "_Labels.png")
        render_label_atlas(items, labels_path)
        textures = {"Labels": labels_path}
        finals = {}
        for atlas, names in ATLASES.items():
            paths = bake_atlas(atlas, [objects[n] for n in names])
            textures.update({atlas + "_" + k: v for k, v in paths.items()})
            finals[atlas] = final_material(atlas, paths)
        label_mat = bpy.data.materials.new("TL_Labels")
        nodes_on(label_mat)
        node = label_mat.node_tree.nodes.new("ShaderNodeTexImage")
        node.image = load_image(labels_path, False)
        label_mat.node_tree.links.new(node.outputs["Color"], principled(label_mat).inputs["Base Color"])
        principled(label_mat).inputs["Roughness"].default_value = 0.55
        for atlas, names in ATLASES.items():
            for n in names:
                obj = objects[n]
                obj.data.materials.clear()
                obj.data.materials.append(finals[atlas])
                for polygon in obj.data.polygons:
                    polygon.material_index = 0
        for mat in PROC:
            bpy.data.materials.remove(mat)
        for host, label_obj in build_labels_m(items, label_mat).items():
            select_only([label_obj, objects[host]], active=objects[host])
            bpy.ops.object.join()
            objects[host] = bpy.context.view_layer.objects.active
        report["textures"] = {k: os.path.relpath(v, OUT).replace(BS, "/") for k, v in textures.items()}
        report["texture_size"] = TEX
        report["label_font"] = LABEL_FONT["name"]
    else:
        for obj in objects.values():
            compact_materials(obj)

    parts = list(objects.values()) + [lights, lamp, deck_lamps]
    for obj in parts:
        weighted_normals(obj)
    for obj in parts + anchors:
        for collection in list(obj.users_collection):
            collection.objects.unlink(obj)
        kit.objects.link(obj)
    bpy.context.view_layer.update()

    triangles = {obj.name: tri_count(obj) for obj in parts}
    bounds = {obj.name: dict(zip(("min", "max"), unity_bounds([obj]))) for obj in parts}
    anchor_report = {o.name: {"unity": [round(v, 4) for v in to_unity(o.matrix_world.translation)],
                              "direction_unity": list(DIRECTIONS.get(o.name, (0.0, 0.0, 1.0)))} for o in anchors}
    report.update({
        "units": "metres; Unity axes: Y up, origin on the capsule floor at the capsule axis; the player boards from -Z looking +Z",
        "dimensions_m": {
            "capsule_outer_diameter": 2 * R_OUT, "capsule_inner_diameter": 2 * R_IN, "floor_to_docking_port": DOCK_TOP,
            "hatch_clear_width": HATCH_W, "hatch_clear_height": HATCH_H, "hatch_corner_radius": HATCH_CORNER,
            "central_route_clear_width": round(2 * CORRIDOR, 3), "min_ceiling_over_route": 2.47,
            "rack_cell_pitch": CELL, "rack_slot_floors_y": list(SHELVES), "seat_entry_to_seat_pan_front": 0.34,
            "deck_to_docking_port": round(DOCK_TOP - DECK_Y, 2), "booster_diameter": 2 * BOOSTER_R,
            "barge_size_x_z": [BARGE["x1"] - BARGE["x0"], BARGE["z1"] - BARGE["z0"]], "deck_level_y": DECK_Y,
            "access_arm_length": round(ARM_Z1 - ARM_Z0, 2), "access_arm_clear_width": 2 * ARM_HALF - 0.1,
        },
        "moving_parts": {
            "Hatch": {"pivot": "HatchPivot", "axis_unity": [0, 1, 0], "closed_deg": 0, "open_deg": 160,
                      "sense": "the free edge moves out towards -Z, then towards -X; at 160 deg the door lies against the hull and clears the arm rails"},
            "AccessArm": {"pivot": "AccessArmPivot", "axis_unity": [0, 1, 0], "connected_deg": 0, "retracted_deg": 90,
                          "sense": "the hatch end swings towards -X; no ignition while it is connected"},
        },
        "colliders": collider_proxies(),
        "bounds_unity": bounds, "anchors": anchor_report, "triangles": triangles, "triangles_total": sum(triangles.values()),
        "materials": {o.name: [m.name for m in o.data.materials] for o in parts},
        "physical_assumptions": "Fictional visual launch vehicle. Proportions are kept plausible and the interior is the inside of the same hull; masses, structure, propulsion and launch clearances are not engineered.",
    })
    log("triangles", triangles, "total", report["triangles_total"])

    render_set = bpy.data.collections.new("TL_RenderSet")
    scene.collection.children.link(render_set)
    cameras = build_render_set(scene, render_set)
    prefix = "" if full else "cp_"
    hatch.rotation_euler = (0.0, 0.0, radians(-160.0))
    render(scene, cameras["boarding"], prefix + "01-boarding-pov.png", (1600, 900), SAMPLES if full else 64)
    render(scene, cameras["exterior"], prefix + "02-exterior.png", (1600, 900), SAMPLES if full else 64)
    render(scene, cameras["interior"], prefix + "03-interior.png", (1600, 900), SAMPLES if full else 64)
    render(scene, cameras["cargo"], prefix + "05-cargo-bays.png", (1600, 900), SAMPLES if full else 64)
    hatch.rotation_euler = (0.0, 0.0, 0.0)
    render(scene, cameras["seated"], prefix + "04-seated-hatch-closed.png", (1600, 900), SAMPLES if full else 64)
    arm.rotation_euler = (0.0, 0.0, radians(90.0))
    render(scene, cameras["ready"], prefix + "06-launch-ready-arm-retracted.png", (1600, 900), SAMPLES if full else 64)
    arm.rotation_euler = (0.0, 0.0, 0.0)
    if full:
        cut = []
        for mat in {slot for obj in parts for slot in obj.data.materials if slot is not None and slot.node_tree}:
            for link in list(mat.node_tree.links):
                if link.from_node.type == "TEX_IMAGE":
                    cut.append((mat.node_tree, link.from_socket, link.to_socket))
                    mat.node_tree.links.remove(link)
        select_only(parts + anchors, active=parts[0])
        bpy.ops.export_scene.fbx(
            filepath=os.path.join(OUT, NAME + ".fbx"), use_selection=True, object_types={"MESH", "EMPTY"},
            apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
            use_mesh_modifiers=True, mesh_smooth_type="FACE", use_triangles=True, add_leaf_bones=False,
            bake_anim=False, path_mode="RELATIVE", embed_textures=False)
        for tree, source, target in cut:
            tree.links.new(source, target)
        for obj in list(render_set.objects):
            bpy.data.objects.remove(obj, do_unlink=True)
        bpy.data.collections.remove(render_set)
        blend_path = os.path.join(OUT, NAME + ".blend")
        bpy.context.preferences.filepaths.save_version = 0
        bpy.ops.wm.save_as_mainfile(filepath=blend_path, compress=False)
        bpy.ops.file.make_paths_relative()
        bpy.ops.wm.save_mainfile(compress=False)
        report["blend_path_scrubbed"] = scrub_blend(blend_path)
        log("exported .fbx and saved .blend")
    with open(os.path.join(OUT, NAME + "_report.json" if full else "geometry_report.json"), "w", encoding="utf-8", newline="\n") as handle:
        json.dump(report, handle, indent=2)
    log("done")


main()
