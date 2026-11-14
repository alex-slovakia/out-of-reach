"""
OUT OF REACH - mission terminal V2 (TERMINAL-ART-V2): the physical touchscreen terminal of AURORA-7,
an original asset built from code in Blender 5.3. It replaces the art of TERM-V1 and keeps its interface:
the 0.64 x 0.40 m active display, the five integration anchors and every object name the V1 handoff
names (ActiveDisplay, Left handhold, Status lamp, DisplayFrame, LeftGripFrame, InteractionAnchor,
FocusCameraAnchor, WallMountAnchor).

Builds the model, its label atlas and baked PBR textures, renders previews, exports the FBX with the
V1 export settings and saves the .blend. No external models, textures, logos or insignia.

    blender -b --factory-startup --python build_mission_terminal_v2.py -- --stage full --out <folder>
    blender -b --factory-startup --python build_mission_terminal_v2.py -- --stage geometry --out <folder>

Unity frame (the V1 contract): origin = display centre, X = screen right, Y = up, +Z into the case,
the screen faces -Z. Parts are modelled in a design frame (x = Unity X, y = Unity Z, z = Unity Y:
the screen faces -y) and turned 180 degrees about Z on the way into the file, which gives exactly
the Blender frame of TERM-V1 (Blender = (-x, -y, z) of the design frame = (-X, -Z, Y) of Unity).
The shared helpers come from the ARS-1 and EPS-2 scripts of the same author.
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
    print("[term %6.1fs]" % (time.time() - T0), *parts, flush=True)


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

NAME = "MissionTerminal"
R180 = Matrix.Rotation(pi, 4, "Z")        # design frame -> Blender frame of TERM-V1


def unity_to_blender(p):
    """Unity (X, Y, Z) -> Blender, as pos() in TERM-V1's build_terminal.py."""
    return Vector((-p[0], -p[2], p[1]))


# ------------------------------------------------------------------ fixed interface (TERM-V1 handoff)
DISPLAY_W, DISPLAY_H = 0.640, 0.400       # active display, metres, centred on the origin, plane Z = 0
ANCHORS = {                               # Unity metres, identity rotation in the file
    "DisplayFrame": (0.0, 0.0, 0.0),
    "LeftGripFrame": (-0.426, 0.0, -0.040),
    "InteractionAnchor": (0.0, -0.100, -0.080),
    "FocusCameraAnchor": (0.012, 0.025, -0.660),
    "WallMountAnchor": (0.0, -0.038, 0.318),
}
GRIP_X, GRIP_Y, GRIP_R, GRIP_HALF = -0.426, -0.040, 0.017, 0.145   # design x, design y (Unity Z), radius, half length
LAMP = (0.300, -0.233)                    # status lamp centre, Unity X / Y, as TERM-V1

# ------------------------------------------------------------------ dimensions (design frame, metres)
GLASS_HX, GLASS_HZ = 0.326, 0.206         # cover glass edge (6 mm black mask beyond the active area)
FACE_Y = -0.012                           # bezel front face, 12 mm proud of the glass
OUT_HX, OUT_HZ = 0.383, 0.255             # bezel outline, the V1 envelope (0.766 x 0.510)
CHAMFER = 0.016                           # machined chamfer: from the gasket to the painted face
SHELL_HX, SHELL_HZ = 0.375, 0.247         # graphite rear shell
SHELL_Y0, SHELL_Y1 = 0.010, 0.080         # shell front (hidden) and back
SCREW_X = 0.364                           # bezel screw line (side bands)
SCREW_Z = 0.240                           # bezel screw line (top and bottom bands)
KEY_X = 0.364
KEYS = ((0.088, "DISP"), (0.052, "MODE"), (-0.052, "ACK"), (-0.118, "PWR"))
KNOB_Z = 0.150
MOUNT_Z = -0.038                          # tilt and wall hinges (Unity Y of the mount axis)
TILT_Y, WALL_Y = 0.110, 0.275             # hinge axes (Unity Z)
BASE_Y0, BASE_Y1 = 0.2945, 0.3175         # wall base plate (its back is the wall plane, WallMountAnchor)
J1_X, J2_X, J_Y = 0.255, 0.195, 0.045     # connectors on the underside of the shell

# ------------------------------------------------------------------ materials
MAT_KEYS = ["paint", "graphite", "alu", "steel", "rubber", "dark", "glass", "keys", "stripe", "white"]
PAINT, GRAPHITE, ALU, STEEL, RUBBER, DARK, GLASS, KEYCAP, STRIPE, WHITE = range(10)
MAT_SPEC = {
    #             base sRGB  metal rough  wear  worn sRGB  worn metal  dirt
    "paint":     ("#D6CFBC", 0.00, 0.48, 0.55, "#9C9EA0", 1.0, 0.60),   # warm ivory powder coat on aluminium
    "graphite":  ("#2B2B2C", 0.25, 0.50, 0.45, "#6B6E72", 0.8, 0.45),   # ship graphite paint (A7_PaintGraphite)
    "alu":       ("#C3C6C9", 1.00, 0.27, 0.00, "#C3C6C9", 1.0, 0.35),   # machined aluminium (A7_Aluminum)
    "steel":     ("#A7A9AC", 1.00, 0.33, 0.00, "#A7A9AC", 1.0, 0.45),   # stainless fasteners
    "rubber":    ("#1B1B1C", 0.00, 0.80, 0.00, "#1B1B1C", 0.0, 0.30),   # gaskets, grip sleeve, cable (A7_Rubber)
    "dark":      ("#0E0F10", 0.00, 0.58, 0.00, "#0E0F10", 0.0, 0.25),   # pockets and counterbores
    "glass":     ("#0A0C0D", 0.00, 0.30, 0.00, "#0A0C0D", 0.0, 0.10),   # black mask border of the cover glass
    "keys":      ("#232528", 0.30, 0.46, 0.50, "#7A7E83", 0.9, 0.35),   # black anodised keys and knobs (A7_AnodizedBlack)
    "stripe":    ("#C4502A", 0.00, 0.50, 0.40, "#9C9EA0", 1.0, 0.50),   # red-orange identity stripe
    "white":     ("#E6E3DA", 0.00, 0.50, 0.00, "#E6E3DA", 0.0, 0.20),   # knob pointer
}
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
    mat = bpy.data.materials.new("TM_" + key)
    nodes_on(mat)
    node = principled(mat)
    node.inputs["Base Color"].default_value = hex_rgba(base_hex)
    node.inputs["Metallic"].default_value = metal
    node.inputs["Roughness"].default_value = rough
    return mat


def make_proc_material(key):
    """Source material with restrained procedural wear; only used as the bake source."""
    base_hex, metal, rough, wear, wear_hex, wear_metal, dirt = MAT_SPEC[key]
    mat = bpy.data.materials.new("TMsrc_" + key)
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



# ------------------------------------------------------------------ terminal helpers (design frame)
RXD = Matrix.Rotation(pi, 4, "X")                   # local +Z -> world -Z (the underside)
RYM90 = Matrix.Rotation(radians(-90.0), 4, "Y")     # local +Z -> world -X


def rr_points(hx, hz, r, segs):
    """Closed rounded rectangle in the XZ plane, counter-clockwise as seen from the front (-Y)."""
    r = max(0.0008, min(r, hx - 1e-4, hz - 1e-4))
    pts = []
    for cx, cz, a0 in ((hx - r, hz - r, 0.0), (-(hx - r), hz - r, 90.0), (-(hx - r), -(hz - r), 180.0), (hx - r, -(hz - r), 270.0)):
        for k in range(segs + 1):
            a = radians(a0 + 90.0 * k / segs)
            pts.append((cx + r * cos(a), cz + r * sin(a)))
    return pts


def rr_loft(bm, specs, mats, segs=4, closed=False, caps=(None, None), fix_normals=True):
    """Joins rounded-rectangle loops (hx, hz, r, y) in order; band i gets mats[i]. closed=True joins
    the last loop back to the first (a frame with a closed cross-section). Returns the new faces."""
    loops = [[bm.verts.new((x, y, z)) for x, z in rr_points(hx, hz, r, segs)] for hx, hz, r, y in specs]
    faces = []
    count = len(loops) if closed else len(loops) - 1
    for i in range(count):
        a, b = loops[i], loops[(i + 1) % len(loops)]
        mat = mats[i] if isinstance(mats, (list, tuple)) else mats
        n = len(a)
        for k in range(n):
            k1 = (k + 1) % n
            face = bm.faces.new((a[k], a[k1], b[k1], b[k]))
            face.material_index = mat
            faces.append(face)
    if caps[0] is not None:
        face = bm.faces.new(tuple(loops[0]))
        face.material_index = caps[0]
        faces.append(face)
    if caps[1] is not None:
        face = bm.faces.new(tuple(loops[-1]))
        face.material_index = caps[1]
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
    """Prism with straight ridges along the local Z axis (alternating radii)."""
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
    """Socket-head (or hex-head) bolt standing on the local XY plane, axis local +Z."""
    if washer:
        lathe(bm, matrix, [(washer, 0.0), (washer, 0.0010)], 20, [STEEL], cap_mat=STEEL)
        matrix = matrix @ Matrix.Translation((0.0, 0.0, 0.0010))
    if hex_head:
        lathe(bm, matrix, [(r, 0.0), (r, h), (r * 0.82, h + 0.0008)], 6, [STEEL, STEEL], cap_mat=STEEL, smooth=False)
    else:
        lathe(bm, matrix, [(r, 0.0), (r, h), (r * 0.55, h), (r * 0.55, h - 0.0020)], 12, [STEEL, STEEL, DARK], cap_mat=DARK)


def quarter_turn_m(bm, matrix, angle):
    """Captive quarter-turn fastener with a slotted head."""
    lathe(bm, matrix, [(0.0110, 0.0), (0.0110, 0.0012), (0.0082, 0.0012), (0.0082, 0.0038), (0.0068, 0.0049)], 14, [STEEL] * 4, cap_mat=STEEL)
    slot = matrix @ Matrix.Rotation(angle, 4, "Z") @ Matrix.Translation((0.0, 0.0, 0.0050)) @ Matrix.Diagonal((0.0017, 0.0118, 0.0004, 1.0))
    for face in {f for v in bmesh.ops.create_cube(bm, size=1.0, matrix=slot)["verts"] for f in v.link_faces}:
        face.material_index = DARK


def bezel_screw(bm, x, z):
    """Captive bezel screw in a counterbore: dark shadow ring, stainless pan head, hex socket."""
    m = front(x, FACE_Y, z)
    lathe(bm, m, [(0.0064, -0.0001), (0.0064, 0.0004)], 18, [DARK], cap_mat=DARK)
    lathe(bm, m @ Matrix.Translation((0.0, 0.0, 0.0004)),
          [(0.0047, 0.0), (0.0047, 0.0011), (0.0040, 0.0020), (0.0024, 0.0024)], 18, [STEEL, STEEL, STEEL], cap_mat=DARK)


# ------------------------------------------------------------------ the terminal
def bezel(bm):
    gx, gz = GLASS_HX, GLASS_HZ
    # Black mask border of the cover glass, between the active area and the gasket.
    mask = rr_loft(bm, [(DISPLAY_W / 2, DISPLAY_H / 2, 0.0006, 0.0002), (gx + 0.0003, gz + 0.0003, 0.0045, 0.0002)], [GLASS], fix_normals=False)
    orient(mask, Vector((0.0, -1.0, 0.0)))
    # Rubber glass seal.
    g1x, g1z = gx + 0.007, gz + 0.007
    rr_loft(bm, [(gx, gz, 0.004, -0.0035), (g1x, g1z, 0.006, -0.0035), (g1x, g1z, 0.006, 0.004), (gx, gz, 0.004, 0.004)], RUBBER, closed=True)
    # Machined chamfer from the seal up to the painted face.
    cx, cz = g1x + CHAMFER, g1z + CHAMFER
    rr_loft(bm, [(g1x, g1z, 0.006, -0.0035), (cx, cz, 0.008, FACE_Y), (cx, cz, 0.008, 0.004), (g1x, g1z, 0.006, 0.004)], ALU, closed=True)
    # Painted bezel: flat face, rounded outer edge, deep sides.
    rr_loft(bm, [(cx, cz, 0.008, FACE_Y), (OUT_HX - 0.004, OUT_HZ - 0.004, 0.010, FACE_Y),
                 (OUT_HX - 0.0012, OUT_HZ - 0.0012, 0.0115, FACE_Y + 0.0012), (OUT_HX, OUT_HZ, 0.012, FACE_Y + 0.004),
                 (OUT_HX, OUT_HZ, 0.012, 0.014), (cx, cz, 0.008, 0.014)], PAINT, closed=True)
    # Perimeter seal between the bezel and the rear shell.
    rr_loft(bm, [(OUT_HX - 0.026, OUT_HZ - 0.026, 0.010, 0.014), (OUT_HX - 0.003, OUT_HZ - 0.003, 0.011, 0.014),
                 (OUT_HX - 0.003, OUT_HZ - 0.003, 0.011, 0.019), (OUT_HX - 0.026, OUT_HZ - 0.026, 0.010, 0.019)], RUBBER, closed=True)
    # Rear shell with a chamfered back edge.
    rr_loft(bm, [(SHELL_HX, SHELL_HZ, 0.014, SHELL_Y0), (SHELL_HX, SHELL_HZ, 0.014, SHELL_Y1 - 0.011),
                 (SHELL_HX - 0.009, SHELL_HZ - 0.009, 0.010, SHELL_Y1)], GRAPHITE, caps=(GRAPHITE, GRAPHITE))
    # Eight captive screws hold the bezel against the seal.
    for x, z in ((-SCREW_X, SCREW_Z), (0.0, SCREW_Z), (SCREW_X, SCREW_Z), (-SCREW_X, 0.0), (SCREW_X, 0.0),
                 (-SCREW_X, -SCREW_Z), (0.0, -SCREW_Z), (SCREW_X, -SCREW_Z)):
        bezel_screw(bm, x, z)


def controls(bm):
    """Right bezel band: dimmer knob and four keys in machined pockets; PWR has guard ribs."""
    m = front(KEY_X, FACE_Y, KNOB_Z)
    lathe(bm, m, [(0.0128, -0.0001), (0.0128, 0.0005)], 24, [DARK], cap_mat=DARK)
    knurled(bm, m, 0.0101, 0.0094, 0.0005, 0.0104, 22, KEYCAP, cap_mat=KEYCAP)
    lathe(bm, m, [(0.0094, 0.0104), (0.0094, 0.0108), (0.0080, 0.0124)], 22, [KEYCAP, KEYCAP], cap_mat=KEYCAP)
    box_m(bm, m @ Matrix.Translation((0.0, 0.0046, 0.0126)), (0.0013, 0.0060, 0.0005), WHITE)
    for z, legend in KEYS:
        box(bm, KEY_X - 0.0125, KEY_X + 0.0125, FACE_Y - 0.0005, FACE_Y + 0.0010, z - 0.0085, z + 0.0085, DARK)
        box(bm, KEY_X - 0.0105, KEY_X + 0.0105, FACE_Y - 0.0040, FACE_Y - 0.0004, z - 0.0066, z + 0.0066, KEYCAP, bevel=0.0010, segs=2)
        if legend == "PWR":
            for side in (-1.0, 1.0):
                zc = z + side * 0.0100
                box(bm, KEY_X - 0.0125, KEY_X + 0.0125, FACE_Y - 0.0074, FACE_Y + 0.0005, zc - 0.0016, zc + 0.0016, ALU, bevel=0.0006, segs=2)
    # Status lamp pocket (the lens is the separate object "Status lamp").
    box(bm, LAMP[0] - 0.0115, LAMP[0] + 0.0115, FACE_Y - 0.0005, FACE_Y + 0.0010, LAMP[1] - 0.0050, LAMP[1] + 0.0050, DARK)


def grip_mounts(bm):
    """Forged standoff arms and bolted feet of the left D-handle (the tube itself is "Left handhold")."""
    for side in (-1.0, 1.0):
        zc = side * 0.153
        box(bm, GRIP_X - 0.004, -0.393, GRIP_Y - 0.012, GRIP_Y + 0.012, zc - 0.011, zc + 0.011, ALU, bevel=0.003, segs=2)
        box(bm, -0.398, -0.374, GRIP_Y - 0.014, 0.052, zc - 0.017, zc + 0.017, GRAPHITE, bevel=0.003, segs=2)
        for y in (-0.024, 0.032):
            bolt_m(bm, Matrix.Translation((-0.398, y, zc)) @ RYM90, r=0.0050, h=0.0035)


def mount(bm):
    """VESA adapter, tilt yoke with friction knobs, arm, wall hinge and the bolted wall base."""
    y0 = SHELL_Y1
    box(bm, -0.090, 0.090, y0 - 0.001, y0 + 0.010, -0.084, 0.084, ALU, bevel=0.003, segs=2)
    for x in (-0.050, 0.050):
        for z in (-0.050, 0.050):
            bolt_m(bm, Matrix.Translation((x, y0 + 0.010, z)) @ RXM90, r=0.0055, h=0.0040)
    for side in (-1.0, 1.0):
        box(bm, side * 0.052, side * 0.064, y0 + 0.009, TILT_Y + 0.022, MOUNT_Z - 0.032, MOUNT_Z + 0.032, GRAPHITE, bevel=0.003, segs=2)
    lathe(bm, Matrix.Translation((-0.0525, TILT_Y, MOUNT_Z)) @ RY90, [(0.0, 0.0), (0.0185, 0.0), (0.0185, 0.105), (0.0, 0.105)], 24, [ALU, ALU, ALU])
    for side, rot in ((-1.0, RYM90), (1.0, RY90)):
        m = Matrix.Translation((side * 0.064, TILT_Y, MOUNT_Z)) @ rot
        knurled(bm, m, 0.0222, 0.0207, 0.0, 0.0150, 26, KEYCAP, cap_mat=KEYCAP, start_cap=True)
        lathe(bm, m, [(0.0207, 0.0150), (0.0170, 0.0190), (0.0, 0.0190)], 26, [KEYCAP, KEYCAP])
    # Arm with a knuckle round each hinge barrel.
    for y in (TILT_Y, WALL_Y):
        lathe(bm, Matrix.Translation((-0.030, y, MOUNT_Z)) @ RY90, [(0.0, 0.0), (0.0300, 0.0), (0.0300, 0.060), (0.0, 0.060)], 28, [GRAPHITE] * 3)
    box(bm, -0.030, 0.030, TILT_Y, WALL_Y, MOUNT_Z - 0.030, MOUNT_Z + 0.030, GRAPHITE, bevel=0.006, segs=2)
    lathe(bm, Matrix.Translation((-0.061, WALL_Y, MOUNT_Z)) @ RY90, [(0.0, 0.0), (0.0185, 0.0), (0.0185, 0.122), (0.0, 0.122)], 24, [ALU, ALU, ALU])
    for side, rot in ((-1.0, RYM90), (1.0, RY90)):
        box(bm, side * 0.034, side * 0.052, WALL_Y - 0.012, BASE_Y0 + 0.001, MOUNT_Z - 0.030, MOUNT_Z + 0.030, PAINT, bevel=0.003, segs=2)
        bolt_m(bm, Matrix.Translation((side * 0.061, WALL_Y, MOUNT_Z)) @ rot, r=0.0105, h=0.0055, hex_head=True)
    # Wall base: its back is the wall plane (WallMountAnchor).
    box(bm, -0.095, 0.095, BASE_Y0, BASE_Y1, MOUNT_Z - 0.120, MOUNT_Z + 0.120, PAINT, bevel=0.006, segs=2)
    for x in (-0.073, 0.073):
        for dz in (-0.089, 0.089):
            bolt_m(bm, front(x, BASE_Y0, MOUNT_Z + dz), r=0.0068, h=0.0045, washer=0.0098, hex_head=True)
    # Strain relief where the service cable enters the base.
    lathe(bm, Matrix.Translation((0.050, 0.306, MOUNT_Z - 0.120 + 0.001)) @ RXD, [(0.0, 0.0), (0.0098, 0.0), (0.0098, 0.007), (0.0068, 0.032)], 20, [RUBBER, RUBBER, RUBBER])


def rear(bm):
    """Heat sink, service cover with quarter-turn fasteners and the two connectors underneath."""
    y0 = SHELL_Y1
    box(bm, -0.200, 0.200, y0 - 0.001, y0 + 0.004, 0.095, 0.215, GRAPHITE, bevel=0.0015, segs=1)
    for k in range(13):
        x = -0.180 + 0.030 * k
        box(bm, x - 0.0016, x + 0.0016, y0 + 0.004, y0 + 0.018, 0.100, 0.210, GRAPHITE, bevel=0.0006, segs=1)
    box(bm, 0.150, 0.330, y0 - 0.001, y0 + 0.005, -0.215, -0.030, PAINT, bevel=0.002, segs=2)
    for (x, z), angle in zip(((0.165, -0.045), (0.315, -0.045), (0.165, -0.200), (0.315, -0.200)), (0.3, 1.1, 0.7, 1.4)):
        quarter_turn_m(bm, Matrix.Translation((x, y0 + 0.005, z)) @ RXM90, angle)
    zb = -SHELL_HZ
    for x in (J1_X, J2_X):
        box(bm, x - 0.016, x + 0.016, J_Y - 0.016, J_Y + 0.016, zb - 0.003, zb + 0.001, ALU, bevel=0.0012, segs=1)
        for dx in (-0.011, 0.011):
            for dy in (-0.011, 0.011):
                lathe(bm, Matrix.Translation((x + dx, J_Y + dy, zb - 0.003)) @ RXD, [(0.0020, 0.0), (0.0020, 0.0012)], 8, [STEEL], cap_mat=STEEL)
        m = Matrix.Translation((x, J_Y, zb - 0.003)) @ RXD
        lathe(bm, m, [(0.0105, 0.0), (0.0105, 0.010)], 24, [STEEL])
        knurled(bm, m, 0.0131, 0.0123, 0.010, 0.024, 20, ALU, cap_mat=ALU, start_cap=True)
        if x == J1_X:
            lathe(bm, m, [(0.0123, 0.024), (0.0100, 0.0245), (0.0100, 0.036), (0.0085, 0.044), (0.0078, 0.044), (0.0062, 0.062)],
                  24, [GRAPHITE, GRAPHITE, GRAPHITE, RUBBER, RUBBER])
        else:
            lathe(bm, m, [(0.0123, 0.024), (0.0136, 0.0245), (0.0136, 0.034), (0.0112, 0.037), (0.0, 0.037)], 24, [RUBBER] * 4)


CABLE_POINTS = ((J1_X, J_Y, -SHELL_HZ - 0.063), (J1_X - 0.010, J_Y + 0.030, -0.352), (0.150, 0.185, -0.335),
                (0.070, 0.282, -0.242), (0.050, 0.306, MOUNT_Z - 0.120 - 0.028))


def build_body():
    bm = new_bm()
    bezel(bm)
    controls(bm)
    grip_mounts(bm)
    mount(bm)
    rear(bm)
    return bm


def build_grip():
    """34 mm D-handle tube: ribbed rubber sleeve between machined end bosses (grip radius 17 mm)."""
    bm = new_bm()
    m = upright(GRIP_X, GRIP_Y, 0.0)
    prof = [(0.0, -GRIP_HALF - 0.013), (0.0172, -GRIP_HALF - 0.013), (0.0196, -GRIP_HALF - 0.010), (0.0196, -GRIP_HALF), (GRIP_R, -GRIP_HALF)]
    mats = [ALU, ALU, ALU, ALU]
    pitch, groove, depth = 0.018, 0.0028, 0.0008
    count = int(round(2 * GRIP_HALF / pitch))
    start = -GRIP_HALF + (2 * GRIP_HALF - (count - 1) * pitch) / 2.0
    for i in range(count - 1):
        h = start + i * pitch + pitch / 2.0 - groove / 2.0
        prof += [(GRIP_R, h), (GRIP_R - depth, h + 0.0006), (GRIP_R - depth, h + groove - 0.0006), (GRIP_R, h + groove)]
        mats += [RUBBER, RUBBER, RUBBER, RUBBER]
    prof += [(GRIP_R, GRIP_HALF), (0.0196, GRIP_HALF), (0.0196, GRIP_HALF + 0.010), (0.0172, GRIP_HALF + 0.013), (0.0, GRIP_HALF + 0.013)]
    mats += [RUBBER, ALU, ALU, ALU, ALU]
    lathe(bm, m, prof, 32, mats[:len(prof) - 1])
    return bm


def build_lamp():
    bm = new_bm()
    box(bm, LAMP[0] - 0.0085, LAMP[0] + 0.0085, FACE_Y - 0.0022, FACE_Y - 0.0004, LAMP[1] - 0.0030, LAMP[1] + 0.0030, 0, bevel=0.0006, segs=2)
    return bm


def build_cable(materials):
    curve = bpy.data.curves.new("MissionTerminal_ServiceCable", "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 10
    curve.bevel_depth = 0.0062
    curve.bevel_resolution = 3
    spline = curve.splines.new("BEZIER")
    spline.bezier_points.add(len(CABLE_POINTS) - 1)
    for point, co in zip(spline.bezier_points, CABLE_POINTS):
        point.co = R180 @ Vector(co)
        point.handle_left_type = "AUTO"
        point.handle_right_type = "AUTO"
    obj = bpy.data.objects.new("Service cable", curve)
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


def build_display(material):
    """ActiveDisplay exactly as TERM-V1: 0.64 x 0.40 m quad on Z = 0, UV 0..1 along screen right / up, facing -Z."""
    hw, hh = DISPLAY_W / 2, DISPLAY_H / 2
    verts = [tuple(unity_to_blender(p)) for p in ((-hw, -hh, 0.0), (hw, -hh, 0.0), (hw, hh, 0.0), (-hw, hh, 0.0))]
    mesh = bpy.data.meshes.new("ActiveDisplayMesh")
    mesh.from_pydata(verts, [], [(0, 1, 2, 3)])
    mesh.update()
    uv = mesh.uv_layers.new(name="UVMap")
    for loop, coords in zip(mesh.loops, ((0.0, 0.0), (1.0, 0.0), (1.0, 1.0), (0.0, 1.0))):
        uv.data[loop.index].uv = coords
    mesh.materials.append(material)
    obj = bpy.data.objects.new("ActiveDisplay", mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def build_anchors():
    result = []
    for name, p in ANCHORS.items():
        obj = bpy.data.objects.new(name, None)
        obj.empty_display_type = "ARROWS"
        obj.empty_display_size = 0.05
        obj.location = unity_to_blender(p)
        bpy.context.scene.collection.objects.link(obj)
        result.append(obj)
    return result


def make_screen_material():
    """Near-black matte cover glass. The runtime replaces it with the terminal display shader."""
    mat = bpy.data.materials.new("MT_Screen")
    nodes_on(mat)
    node = principled(mat)
    node.inputs["Base Color"].default_value = hex_rgba("#0B0D0E")
    node.inputs["Metallic"].default_value = 0.0
    node.inputs["Roughness"].default_value = 0.32
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
    scene = bpy.data.scenes.new("TM_LabelAtlas")
    made = []
    colors = {}
    font = load_label_font()

    def color_mat(hex_color):
        if hex_color not in colors:
            colors[hex_color] = emission_material("TMatlas_" + hex_color.lstrip("#"), hex_color)
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
            curve = bpy.data.curves.new("TMatlas_text", "FONT")
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
                obj = bpy.data.objects.new("TMatlas_text", curve)
                obj.scale = (0.01, 0.01, 0.01)
                obj.location = (x + w / 2 + dx + ox * shift, y + h / 2 + dz - size * TEXT_DROP + oy * shift, 0.002)
                scene.collection.objects.link(obj)
                made.append(obj)

    for hex_color, faces in quads.items():
        bm = bmesh.new()
        for corners in faces:
            bm.faces.new([bm.verts.new(c) for c in corners])
        mesh = bpy.data.meshes.new("TMatlas_fill")
        bm.to_mesh(mesh)
        bm.free()
        mesh.materials.append(color_mat(hex_color))
        obj = bpy.data.objects.new("TMatlas_fill", mesh)
        scene.collection.objects.link(obj)
        made.extend((obj, mesh))

    camera_data = bpy.data.cameras.new("TMatlas_cam")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = max(ATLAS_W, ATLAS_H) * px
    camera = bpy.data.objects.new("TMatlas_cam", camera_data)
    camera.location = (ATLAS_W * px / 2, ATLAS_H * px / 2, 1.0)
    scene.collection.objects.link(camera)
    scene.camera = camera
    made.extend((camera, camera_data))

    world = bpy.data.worlds.new("TMatlas_world")
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
    "keycap": dict(bg=MAT_SPEC["keys"][0], fg="#E6E3DA", line=None),
    "stripe": dict(bg=MAT_SPEC["stripe"][0], fg=MAT_SPEC["stripe"][0], line=None),
})


def placards():
    """Real words only: key legends, the unit plate, the ship name and the rear service labels."""
    items = []

    def add(key, host, cx, cz, w, h, y, style, texts, flip=False, shapes=()):
        items.append(dict(key=key, host=host, c=(cx, cz), s=(w, h), y=y, style=style, texts=texts, flip=flip, shapes=shapes))

    for z, legend in KEYS:
        add("key_" + legend, "Body", KEY_X, z, 0.0185, 0.0105, FACE_Y - 0.0040, "keycap", [(legend, 0.0, 0.0, 0.0048)])
    add("brt", "Body", KEY_X, KNOB_Z + 0.0192, 0.026, 0.0080, FACE_Y, "cream", [("BRT", 0.0, 0.0, 0.0047)])
    add("status", "Body", LAMP[0] - 0.034, LAMP[1], 0.040, 0.0080, FACE_Y, "cream", [("STATUS", 0.0, 0.0, 0.0047)])
    add("unit", "Body", -0.170, -SCREW_Z, 0.230, 0.0145, FACE_Y, "dark", [("DU-2   NAV / COMM DISPLAY UNIT", 0.0, 0.0, 0.0062)])
    add("ship", "Body", -0.255, SCREW_Z, 0.110, 0.0150, FACE_Y, "cream", [("AURORA-7", 0.0, 0.0, 0.0105)])
    add("stripe", "Body", -0.162, SCREW_Z, 0.060, 0.0050, FACE_Y, "stripe", [])
    add("rear_id", "Body", -0.240, -0.120, 0.170, 0.070, SHELL_Y1, "light",
        [("DU-2 DISPLAY UNIT", 0.0, 0.020, 0.0105), ("NAV / COMM   AURORA-7", 0.0, 0.003, 0.0064),
         ("S/N A7-C2-011", 0.0, -0.011, 0.0058), ("28 V DC   60 W", 0.0, -0.023, 0.0058)], flip=True)
    add("service", "Body", 0.240, -0.125, 0.140, 0.034, SHELL_Y1 + 0.005, "caution",
        [("SERVICE COVER", 0.0, 0.007, 0.0072), ("ISOLATE 28 V BEFORE OPENING", 0.0, -0.007, 0.0054)], flip=True)
    return items


def shrink_hidden_faces(obj):
    """Faces nobody sees get a fraction of the texel density: the wall side of the base plate, the shell
    front behind the bezel and the back of the bezel frame."""
    mesh = obj.data
    uv = mesh.uv_layers.active.data
    for polygon in mesh.polygons:
        if abs(polygon.normal.y) < 0.99:
            continue
        uz = -(polygon.center.y + obj.location.y)          # Unity Z of the face
        towards_wall = polygon.normal.y < 0.0              # Blender -Y is Unity +Z
        if towards_wall and uz > BASE_Y1 - 0.001:
            factor = 0.15
        elif not towards_wall and abs(uz - SHELL_Y0) < 0.0008 and polygon.area > 0.05:
            factor = 0.10
        elif towards_wall and abs(uz - 0.014) < 0.0008 and polygon.area > 0.004:
            factor = 0.25
        else:
            continue
        loops = list(polygon.loop_indices)
        cu = sum(uv[i].uv.x for i in loops) / len(loops)
        cv = sum(uv[i].uv.y for i in loops) / len(loops)
        for i in loops:
            uv[i].uv = ((uv[i].uv.x - cu) * factor + cu, (uv[i].uv.y - cv) * factor + cv)


def to_unity(v):
    return (-v[0], v[2], -v[1])


# ------------------------------------------------------------------ render set (not exported)
def build_render_set(scene, collection):
    def area(name, unity_location, unity_target, power, size, color):
        data = bpy.data.lights.new(name, "AREA")
        data.energy, data.size, data.color = power, size, color
        obj = bpy.data.objects.new(name, data)
        obj.location = unity_to_blender(unity_location)
        look_at(obj, unity_to_blender(unity_target))
        collection.objects.link(obj)

    area("MT_Key", (0.95, 0.85, -1.35), (0.0, 0.0, 0.0), 150.0, 1.3, (1.0, 0.97, 0.93))
    area("MT_Fill", (-1.25, 0.15, -0.85), (0.0, 0.0, 0.0), 40.0, 2.0, (0.92, 0.95, 1.0))
    area("MT_Rim", (-0.55, 0.95, 1.25), (0.0, 0.0, 0.10), 120.0, 1.0, (1.0, 1.0, 1.0))
    area("MT_Back", (0.95, 0.35, 1.45), (0.0, -0.05, 0.15), 70.0, 1.6, (1.0, 0.96, 0.9))

    cameras = {}

    def camera(key, unity_location, unity_target, lens):
        data = bpy.data.cameras.new("MT_Cam_" + key)
        data.lens = lens
        data.clip_start = 0.01
        obj = bpy.data.objects.new("MT_Cam_" + key, data)
        obj.location = unity_to_blender(unity_location)
        look_at(obj, unity_to_blender(unity_target))
        collection.objects.link(obj)
        cameras[key] = obj

    camera("front", (0.39, 0.18, -1.65), (0.0, 0.0, 0.04), 60.0)          # the framing of TERM-V1 01-front
    camera("rear", (-1.25, 0.65, 1.65), (0.0, 0.0, 0.12), 60.0)           # the framing of TERM-V1 02-rear
    camera("detail", (0.80, 0.10, -0.36), (0.364, -0.01, -0.005), 60.0)
    camera("grip", (-0.88, 0.16, -0.58), (-0.40, 0.0, 0.0), 55.0)

    world = bpy.data.worlds.new("MT_World")
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
    """Preview-only screen content, dim and drawn from scratch: a schematic globe outline and the
    TERM-V1 interface captions. It is not exported; the runtime draws the real display."""
    line = bpy.data.materials.new("MT_PreviewLines")
    nodes_on(line)
    tree = line.node_tree
    tree.nodes.clear()
    emission = tree.nodes.new("ShaderNodeEmission")
    emission.inputs["Color"].default_value = hex_rgba("#9AA2A5")
    emission.inputs["Strength"].default_value = 0.55
    output = tree.nodes.new("ShaderNodeOutputMaterial")
    tree.links.new(emission.outputs[0], output.inputs["Surface"])

    bm = bmesh.new()
    y = -0.0008                                   # design frame: just in front of the glass
    cx, cz, r, width = -0.150, -0.005, 0.135, 0.0011

    def strip(points):
        for (x0, z0), (x1, z1) in zip(points, points[1:]):
            dx, dz = x1 - x0, z1 - z0
            length = max(1e-9, (dx * dx + dz * dz) ** 0.5)
            nx, nz = -dz / length * width / 2, dx / length * width / 2
            bm.faces.new([bm.verts.new((x0 + nx, y, z0 + nz)), bm.verts.new((x0 - nx, y, z0 - nz)),
                          bm.verts.new((x1 - nx, y, z1 - nz)), bm.verts.new((x1 + nx, y, z1 + nz))])

    steps = 96
    strip([(cx + r * cos(2 * pi * k / steps), cz + r * sin(2 * pi * k / steps)) for k in range(steps + 1)])
    for lat in (-60, -30, 0, 30, 60):
        zc = cz + r * sin(radians(lat))
        half = r * cos(radians(lat))
        strip([(cx - half, zc), (cx + half, zc)])
    for lon in (-60, -30, 0, 30, 60):
        a = r * sin(radians(lon))
        strip([(cx + a * cos(t), cz + r * sin(t)) for t in [radians(-90 + 180 * k / 48) for k in range(49)]])
    bmesh.ops.transform(bm, matrix=R180, verts=list(bm.verts))
    mesh = bpy.data.meshes.new("MT_PreviewGlobe")
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(line)
    collection.objects.link(bpy.data.objects.new("PreviewOnly globe outline", mesh))

    font = load_label_font()
    for text, ux, uy, size in (("EARTH ARCHIVE", 0.060, 0.120, 0.016), ("SCHEMATIC DISC PROJECTION", 0.060, 0.080, 0.0105),
                               ("ARCHIVED OBSERVATION - NOT A LIVE FEED", 0.060, 0.050, 0.0085),
                               ("TOUCH / DRAG TO INSPECT", -0.290, -0.178, 0.0085)):
        curve = bpy.data.curves.new("MT_PreviewText", "FONT")
        curve.body = text
        if font is not None:
            curve.font = font
        curve.size = size
        curve.align_x = "LEFT"
        curve.materials.append(line)
        obj = bpy.data.objects.new("PreviewOnly " + text[:16], curve)
        obj.location = unity_to_blender((ux, uy, -0.0009))
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
    mat = bpy.data.materials.new("MT_WireOverlay")
    nodes_on(mat)
    g = Graph(mat)
    wire = g.nodes.new("ShaderNodeWireframe")
    wire.use_pixel_size = True
    wire.inputs["Size"].default_value = 1.2
    color = g.mix(wire.outputs[0], (0.62, 0.63, 0.64, 1.0), (0.02, 0.05, 0.10, 1.0))
    g.links.new(color, principled(mat).inputs["Base Color"])
    principled(mat).inputs["Roughness"].default_value = 0.7
    return mat


BS = chr(92)


def scrub_blend(path):
    """A saved .blend keeps the absolute path it was saved to, and the file browser keeps the last folder
    it showed. Blank every copy of this folder and of the home folder in the (uncompressed) file so no
    local path ships; the file stays loadable."""
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


# ------------------------------------------------------------------ main
def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = "CYCLES"
    device = enable_gpu(scene)
    log("Blender", bpy.app.version_string, "| stage", STAGE, "| device", device)
    prop = bpy.data.collections.new("MissionTerminal")
    scene.collection.children.link(prop)
    preview = bpy.data.collections.new("PreviewOnly")
    scene.collection.children.link(preview)

    full = STAGE == "full"
    surface = [make_proc_material(k) if full else make_flat_material(k) for k in MAT_KEYS]
    if full:
        PROC.extend(surface)
    screen_mat = make_screen_material()
    amber = make_indicator_material("MT_Amber", "#FFA61A", 1.2)

    body = finish(build_body(), "TerminalBody", surface)
    cable = build_cable(surface)
    select_only([cable, body], active=body)
    bpy.ops.object.join()
    body = bpy.context.view_layer.objects.active
    grip = finish(build_grip(), "Left handhold", surface, origin=unity_to_blender(ANCHORS["LeftGripFrame"]))
    lamp = finish(build_lamp(), "Status lamp", [amber], origin=unity_to_blender((LAMP[0], LAMP[1], FACE_Y - 0.0013)))
    lamp.data.uv_layers.new(name="UVMap")
    display = build_display(screen_mat)
    anchors = build_anchors()
    baked = [body, grip]
    parts = [body, grip, display, lamp]
    report = {"blender": bpy.app.version_string, "stage": STAGE, "cycles_device": device}

    if full:
        items = placards()
        pack_atlas(items)
        labels_path = os.path.join(TEX_DIR, NAME + "_Labels.png")
        render_label_atlas(items, labels_path)
        log("label atlas rendered")

        unwrap(baked)
        color = new_image("TMbake_color", False)
        metal = new_image("TMbake_metal", True)
        rough = new_image("TMbake_rough", True)
        occlusion = new_image("TMbake_ao", True)
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
        metal_smooth = bpy.data.images.new("TMbake_ms", TEX, TEX, alpha=True)
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

        final = bpy.data.materials.new("MT_TerminalBody")
        nodes_on(final)
        tree = final.node_tree
        for index, (key, socket_name, non_color) in enumerate((("BaseColor", "Base Color", False), ("Metallic", "Metallic", True), ("Roughness", "Roughness", True))):
            node = tree.nodes.new("ShaderNodeTexImage")
            node.image = load_image(paths[key], non_color)
            node.location = (-520, 320 - index * 300)
            tree.links.new(node.outputs["Color"], principled(final).inputs[socket_name])
        label_mat = bpy.data.materials.new("MT_Labels")
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
        for obj in label_objects.values():
            obj.data.transform(R180)
        select_only([label_objects["Body"], body], active=body)
        bpy.ops.object.join()
        body = bpy.context.view_layer.objects.active
        parts[0] = body
        report["textures"] = {key: os.path.relpath(value, OUT).replace("\\", "/") for key, value in paths.items()}
        report["texture_size"] = TEX
        report["label_font"] = LABEL_FONT["name"]
    else:
        for obj in baked:
            compact_materials(obj)

    for obj in (body, grip, lamp):
        weighted_normals(obj)
    for obj in parts + anchors:
        for collection in list(obj.users_collection):
            collection.objects.unlink(obj)
        prop.objects.link(obj)
    bpy.context.view_layer.update()

    lo, hi = world_bounds(parts)
    ulo = [min(a, b) for a, b in zip(to_unity(lo), to_unity(hi))]
    uhi = [max(a, b) for a, b in zip(to_unity(lo), to_unity(hi))]
    triangles = {obj.name: tri_count(obj) for obj in parts}
    anchor_check = {}
    for obj in anchors:
        got = [round(v, 6) for v in to_unity(obj.matrix_world.translation)]
        want = list(ANCHORS[obj.name])
        anchor_check[obj.name] = {"unity": got, "v1": want, "equal": all(abs(a - b) < 1e-6 for a, b in zip(got, want))}
    dlo, dhi = world_bounds([display])
    report.update({
        "units": "metres; Unity axes: X screen right, Y up, +Z into the case, the screen faces -Z",
        "dimensions_unity_m": {"x": round(uhi[0] - ulo[0], 4), "y": round(uhi[1] - ulo[1], 4), "z": round(uhi[2] - ulo[2], 4)},
        "bounds_unity_min": [round(v, 4) for v in ulo],
        "bounds_unity_max": [round(v, 4) for v in uhi],
        "active_display_m": [round(dhi.x - dlo.x, 6), round(dhi.z - dlo.z, 6)],
        "anchors": anchor_check,
        "triangles": triangles,
        "triangles_total": sum(triangles.values()),
        "materials": {obj.name: [mat.name for mat in obj.data.materials] for obj in parts},
    })
    log("dimensions", report["dimensions_unity_m"], "triangles", triangles, "total", report["triangles_total"])
    log("anchors equal:", all(v["equal"] for v in anchor_check.values()))

    render_set = bpy.data.collections.new("MT_RenderSet")
    scene.collection.children.link(render_set)
    cameras = build_render_set(scene, render_set)
    if full:
        build_screen_preview(preview)
        render(scene, cameras["front"], "01-front.png", (1400, 1000), SAMPLES)
        render(scene, cameras["rear"], "02-rear.png", (1400, 1000), SAMPLES)
        render(scene, cameras["detail"], "03-controls.png", (1400, 1000), SAMPLES)
        render(scene, cameras["grip"], "04-grip.png", (1400, 1000), SAMPLES)
        bpy.context.view_layer.material_override = wire_material()
        render(scene, cameras["front"], "05-wire.png", (1400, 1000), 32)
        bpy.context.view_layer.material_override = None

        # FBX first, while the scene is unsaved and its texture links are cut: the exporter writes the path of
        # the .blend and of every linked texture into the file. Export settings are TERM-V1's.
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

        # The editable .blend holds the terminal only (like TERM-V1's), with relative texture paths.
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
        build_screen_preview(preview)
        render(scene, cameras["front"], "geometry_front.png", (1050, 750), 48)
        render(scene, cameras["rear"], "geometry_rear.png", (1050, 750), 48)
        render(scene, cameras["detail"], "geometry_controls.png", (1050, 750), 48)
        render(scene, cameras["grip"], "geometry_grip.png", (1050, 750), 48)

    with open(os.path.join(OUT, NAME + "_report.json" if full else "geometry_report.json"), "w", encoding="utf-8", newline="\n") as handle:
        json.dump(report, handle, indent=2)
    log("done")


main()
