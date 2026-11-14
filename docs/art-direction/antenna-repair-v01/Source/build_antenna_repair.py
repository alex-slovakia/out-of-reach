"""
OUT OF REACH - antenna repair kit (ANTENNA-REPAIR-ART-01): a tethered service wrench, the HGA data connector in a
damaged state (backed out, scorched, frayed cable) and its repaired variant, on a shared bulkhead receptacle. Original asset
built from code in Blender 5.3; same frame and export convention as TERMINAL-ART-V2: Blender = (-X, -Z, Y) of Unity.
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
    print("[ar %6.1fs]" % (time.time() - T0), *parts, flush=True)


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

NAME = "AntennaRepair"
R180 = Matrix.Rotation(pi, 4, "Z")


def unity_to_blender(p):
    return Vector((-p[0], -p[2], p[1]))


def design(p):
    return Vector((p[0], p[2], p[1]))



# ------------------------------------------------------------------ kit layout (Unity metres; the wall is the plane Z = 0, the technician is on -Z)
FACE = 0.006                               # flange and grommet plate thickness; their back faces sit on the wall
PLUG_BACK = 0.012                          # the damaged plug sits this far backed out
WRENCH = dict(grip=(0.38, -0.03, -0.20), tip=(0.20, -0.03, -0.20))
ANCHORS = {
    "ConnectorMount": (0.0, 0.0, 0.0),
    "CableEntry": (0.0, 0.0, -0.156),
    "CableGrommet": (0.015, -0.240, 0.0),
    "PlugGrip": (0.0, 0.0, -0.045),
    "PlugGripBackedOut": (0.0, 0.0, -0.057),
    "WrenchGrip": WRENCH["grip"],
    "WrenchTip": WRENCH["tip"],
    "WrenchTether": (0.475, -0.03, -0.20),
}
DIRECTIONS = {"ConnectorMount": (0.0, 0.0, -1.0), "CableEntry": (0.0, 0.0, -1.0), "CableGrommet": (0.0, 0.0, -1.0),
              "PlugGrip": (0.0, 0.0, 1.0), "PlugGripBackedOut": (0.0, 0.0, 1.0),
              "WrenchGrip": (1.0, 0.0, 0.0), "WrenchTip": (0.0, 1.0, 0.0), "WrenchTether": (1.0, 0.0, 0.0)}
MAT_KEYS = ["panel", "alu", "steel", "rubber", "keys", "copper", "soot", "yellow", "dark", "white", "graphite"]
PANEL, ALU, STEEL, RUBBER, KEYCAP, COPPER, SOOT, YELLOW, DARK, WHITE, GRAPHITE = range(11)
MAT_SPEC = {
    "panel":    ("#D9D8D1", 0.00, 0.50, 0.45, "#9C9EA0", 1.0, 0.55),
    "alu":      ("#C3C6C9", 1.00, 0.28, 0.00, "#C3C6C9", 1.0, 0.40),
    "steel":    ("#A7A9AC", 1.00, 0.33, 0.00, "#A7A9AC", 1.0, 0.45),
    "rubber":   ("#1B1B1C", 0.00, 0.80, 0.00, "#1B1B1C", 0.0, 0.30),
    "keys":     ("#232528", 0.30, 0.46, 0.50, "#7A7E83", 0.9, 0.35),
    "copper":   ("#B9743C", 1.00, 0.36, 0.00, "#B9743C", 1.0, 0.50),
    "soot":     ("#1A1714", 0.00, 0.92, 0.00, "#1A1714", 0.0, 0.80),
    "yellow":   ("#D2A322", 0.00, 0.55, 0.40, "#7D7A70", 1.0, 0.50),
    "dark":     ("#0E0F10", 0.00, 0.58, 0.00, "#0E0F10", 0.0, 0.25),
    "white":    ("#E6E3DA", 0.00, 0.50, 0.00, "#E6E3DA", 0.0, 0.20),
    "graphite": ("#2B2B2C", 0.25, 0.50, 0.45, "#6B6E72", 0.8, 0.45),
}
SOCK = {}
PROC = []
CONTACTS = {}
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
    mat = bpy.data.materials.new("AR_" + key)
    nodes_on(mat)
    node = principled(mat)
    node.inputs["Base Color"].default_value = hex_rgba(base_hex)
    node.inputs["Metallic"].default_value = metal
    node.inputs["Roughness"].default_value = rough
    return mat


def make_proc_material(key):
    """Source material with restrained procedural wear; only used as the bake source."""
    base_hex, metal, rough, wear, wear_hex, wear_metal, dirt = MAT_SPEC[key]
    mat = bpy.data.materials.new("ARsrc_" + key)
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



# ------------------------------------------------------------------ antenna repair kit (design frame: x = Unity X, y = Unity Z, z = Unity Y)
def c_ring(bm, matrix, r_in, r_out, h, a0, a1, segs, mat, inner_mat=None):
    """Open ring around local Z from angle a0 to a1, height 0..h, with closed ends; the inner face gets inner_mat."""
    loops = []
    for k in range(segs + 1):
        a = a0 + (a1 - a0) * k / segs
        c, s = cos(a), sin(a)
        loops.append([bm.verts.new(matrix @ Vector((r * c, r * s, z))) for r, z in ((r_in, 0.0), (r_out, 0.0), (r_out, h), (r_in, h))])
    faces = []
    for k in range(segs):
        a, b = loops[k], loops[k + 1]
        for i in range(4):
            i1 = (i + 1) % 4
            face = bm.faces.new((a[i], b[i], b[i1], a[i1]))
            face.material_index = inner_mat if (i == 3 and inner_mat is not None) else mat
            faces.append(face)
    for loop in (loops[0], loops[-1]):
        face = bm.faces.new(tuple(loop))
        face.material_index = mat
        faces.append(face)
    bmesh.ops.recalc_face_normals(bm, faces=faces)


def receptacle():
    bm = new_bm()
    box(bm, -0.045, 0.045, -FACE, 0.0, -0.045, 0.045, PANEL, bevel=0.003, segs=2)
    for x in (-0.034, 0.034):
        for z in (-0.034, 0.034):
            lathe(bm, front(x, -FACE, z), [(0.0038, -0.0002), (0.0038, 0.0016), (0.0022, 0.0021)], 12, [STEEL, STEEL], cap_mat=DARK)
    lathe(bm, front(0.0, -FACE, 0.0), [(0.0215, 0.0), (0.0215, 0.026), (0.0175, 0.028), (0.0, 0.028)], 28, [ALU, ALU, DARK])
    box(bm, -0.0035, 0.0035, -FACE - 0.0285, -FACE - 0.010, 0.0195, 0.0225, ALU)
    box(bm, -0.0015, 0.0015, -FACE - 0.0006, -FACE, 0.036, 0.044, WHITE)
    for k in range(6):
        a = radians(90.0 + 72.0 * k)
        r = 0.0085 if k else 0.0
        tube(bm, (r * cos(a), -FACE - 0.0279, r * sin(a)), (r * cos(a), -FACE - 0.0315, r * sin(a)), 0.0011, COPPER, segs=8)
    gx, gz = ANCHORS["CableGrommet"][0], ANCHORS["CableGrommet"][1]
    box(bm, gx - 0.024, gx + 0.024, -FACE, 0.0, gz - 0.024, gz + 0.024, PANEL, bevel=0.003, segs=2)
    lathe(bm, front(gx, -FACE, gz), [(0.0150, 0.0), (0.0150, 0.004), (0.0120, 0.0065), (0.0086, 0.0065), (0.0086, 0.002)], 24, [RUBBER] * 4, cap_mat=DARK)
    for x, z in ((gx - 0.017, gz - 0.017), (gx + 0.017, gz + 0.017)):
        lathe(bm, front(x, -FACE, z), [(0.0030, -0.0002), (0.0030, 0.0014), (0.0018, 0.0018)], 10, [STEEL, STEEL], cap_mat=DARK)
    return bm


def plug(bm, back=0.0, turn=0.0, damaged=False):
    m = front(0.0, -(FACE + back), 0.0) @ Matrix.Rotation(radians(turn), 4, "Z")
    knurled(bm, m @ Matrix.Translation((0.0, 0.0, 0.026)), 0.0285, 0.0272, 0.0, 0.026, 24, SOOT if damaged else KEYCAP, cap_mat=KEYCAP, start_cap=True)
    lathe(bm, m, [(0.0272, 0.052), (0.0230, 0.056), (0.0230, 0.090), (0.0170, 0.102)], 28, [ALU, SOOT if damaged else ALU, ALU])
    if damaged:
        lathe(bm, m, [(0.0170, 0.102), (0.0158, 0.108), (0.0130, 0.124), (0.0118, 0.126)], 24, [SOOT, RUBBER, SOOT], cap_mat=SOOT)
    else:
        lathe(bm, m, [(0.0170, 0.102), (0.0150, 0.112), (0.0105, 0.150)], 24, [RUBBER, RUBBER], cap_mat=RUBBER)
    box_m(bm, m @ Matrix.Translation((0.0, 0.0287, 0.040)), (0.0030, 0.0012, 0.010), WHITE)


def cable(points, name, materials, radius=0.0085):
    curve = bpy.data.curves.new(NAME + "_" + name, "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 10
    curve.bevel_depth = radius
    curve.bevel_resolution = 3
    curve.use_fill_caps = True
    spline = curve.splines.new("BEZIER")
    spline.bezier_points.add(len(points) - 1)
    for point, co in zip(spline.bezier_points, points):
        point.co = R180 @ Vector(co)
        point.handle_left_type = point.handle_right_type = "AUTO"
    obj = bpy.data.objects.new(name, curve)
    bpy.context.scene.collection.objects.link(obj)
    select_only([obj], active=obj)
    bpy.ops.object.convert(target="MESH")
    obj = bpy.context.view_layer.objects.active
    obj.data.materials.clear()
    for mat in materials:
        obj.data.materials.append(mat)
    for polygon in obj.data.polygons:
        polygon.material_index = RUBBER
        polygon.use_smooth = True
    return obj


DAMAGED_Y = (-(FACE + PLUG_BACK + 0.126), -(FACE + PLUG_BACK + 0.126) - 0.066)      # melted boot end, resumed jacket
LOOP_TAIL = ((0.012, -0.120, -0.205), (0.014, -0.045, -0.232), (0.015, -0.010, -0.240))  # shared run into the grommet
REPAIRED_CABLE = ((0.0, -0.156, 0.0), (0.0, -0.196, -0.004), (0.004, -0.222, -0.066), (0.008, -0.198, -0.146)) + LOOP_TAIL
DAMAGED_CABLE = ((0.0, DAMAGED_Y[1] + 0.002, 0.0), (0.0, DAMAGED_Y[1] - 0.024, -0.006), (0.004, -0.250, -0.072), (0.008, -0.212, -0.150)) + LOOP_TAIL


def repaired_parts():
    bm = new_bm()
    plug(bm)
    lathe(bm, front(0.0, -0.160, 0.0), [(0.0085, 0.0), (0.0100, 0.0012), (0.0100, 0.0128), (0.0085, 0.014)], 20, [YELLOW] * 3)
    tube(bm, (0.026, -0.046, 0.010), (0.034, -FACE - 0.0019, 0.034), 0.0008, STEEL, segs=6)
    return bm


def damaged_parts():
    bm = new_bm()
    plug(bm, back=PLUG_BACK, turn=24.0, damaged=True)
    y0, y1 = DAMAGED_Y
    lathe(bm, front(0.0, y0, 0.0), [(0.0085, 0.0), (0.0085, 0.014), (0.0076, 0.018), (0.0050, 0.019)], 18, [RUBBER, SOOT, SOOT], cap_mat=COPPER)
    lathe(bm, front(0.0, y1 + 0.005, 0.0), [(0.0048, 0.0), (0.0078, 0.001), (0.0090, 0.005), (0.0090, 0.016)], 18, [SOOT, SOOT, RUBBER], start_cap=True)
    ya, yb = y0 - 0.017, y1 + 0.006
    for k in range(9):
        a = 2 * pi * k / 9
        pts = []
        for i in range(5):
            t = i / 4
            r = 0.0040 + (0.0110 + 0.0020 * sin(3 * a + 1.0)) * sin(pi * t)
            ang = a + 0.9 * t
            pts.append((r * cos(ang), ya + (yb - ya) * t, r * sin(ang) - 0.0015 * sin(pi * t)))
        for p, q in zip(pts, pts[1:]):
            tube(bm, p, q, 0.0012, COPPER, segs=6)
    for a, length in ((0.6, 0.020), (3.9, 0.016)):
        y = ya + (yb - ya) * 0.45
        tube(bm, (0.012 * cos(a), y, 0.012 * sin(a)), ((0.012 + length) * cos(a + 0.25), y - 0.010, (0.012 + length) * sin(a + 0.25)), 0.0010, COPPER, segs=6)
    sm = front(0.0, -FACE - 0.0003, 0.0)                      # blotchy scorch halo on the flange face
    inner, outer = [], []
    for k in range(48):
        a = 2 * pi * k / 48
        r = 0.0275 + 0.0045 * (1.0 + sin(3 * a + 0.7)) * (0.7 + 0.3 * sin(5 * a + 1.3))
        inner.append(bm.verts.new(sm @ Vector((0.0214 * cos(a), 0.0214 * sin(a), 0.0))))
        outer.append(bm.verts.new(sm @ Vector((r * cos(a), r * sin(a), 0.0))))
    for k in range(48):
        k1 = (k + 1) % 48
        bm.faces.new((outer[k], outer[k1], inner[k1], inner[k])).material_index = SOOT
    return bm


def wrench():
    bm = new_bm()
    g, t, e = Vector(design(WRENCH["grip"])), Vector(design(WRENCH["tip"])), Vector(design(ANCHORS["WrenchTether"]))
    y, z = g.y, g.z
    c_ring(bm, upright(t.x, t.y, t.z - 0.007), 0.0300, 0.0420, 0.014, radians(-145.0), radians(145.0), 40, STEEL, inner_mat=RUBBER)
    box(bm, t.x + 0.036, t.x + 0.064, y - 0.011, y + 0.011, z - 0.0065, z + 0.0065, STEEL, bevel=0.003, segs=2)
    box(bm, t.x + 0.060, e.x - 0.010, y - 0.008, y + 0.008, z - 0.005, z + 0.005, STEEL, bevel=0.003, segs=2)
    box(bm, t.x + 0.040, t.x + 0.058, y - 0.006, y + 0.006, z + 0.0065, z + 0.0105, KEYCAP, bevel=0.002, segs=1)
    lathe(bm, upright(t.x + 0.049, y, z + 0.0105), [(0.0035, 0.0), (0.0035, 0.002), (0.0, 0.003)], 12, [STEEL, STEEL])
    knurled(bm, aim((g.x - 0.060, y, z), (g.x + 0.060, y, z)), 0.0175, 0.0166, 0.0, 0.120, 26, RUBBER, cap_mat=RUBBER, start_cap=True)
    for x in (g.x - 0.063, g.x + 0.057):
        lathe(bm, aim((x, y, z), (x + 0.006, y, z)), [(0.0, 0.0), (0.0190, 0.0), (0.0190, 0.006), (0.0, 0.006)], 24, [ALU] * 3)
    lathe(bm, upright(e.x, e.y, e.z - 0.0025), [(0.0080, 0.0), (0.0135, 0.0), (0.0135, 0.005), (0.0080, 0.005), (0.0080, 0.0)], 24, [YELLOW] * 4)
    return bm


def build_anchors():
    result = []
    for name, p in ANCHORS.items():
        obj = bpy.data.objects.new(name, None)
        obj.empty_display_type = "ARROWS"
        obj.empty_display_size = 0.03
        obj.location = unity_to_blender(p)
        bpy.context.scene.collection.objects.link(obj)
        result.append(obj)
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


BS = chr(92)


def to_unity(v):
    return (-v[0], v[2], -v[1])


def unity_bounds(objs):
    lo, hi = world_bounds(objs)
    a, b = to_unity(lo), to_unity(hi)
    return [round(min(x, y), 4) for x, y in zip(a, b)], [round(max(x, y), 4) for x, y in zip(a, b)]


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


def engaged_matrix():
    """Wrench pose with WrenchTip on PlugGripBackedOut and the jaw axis along the connector axis (render only)."""
    tip = unity_to_blender(ANCHORS["WrenchTip"]) - unity_to_blender(ANCHORS["WrenchGrip"])
    target = unity_to_blender(ANCHORS["PlugGripBackedOut"])
    return Matrix.Translation(target) @ Matrix.Rotation(radians(-90.0), 4, "X") @ Matrix.Translation(-tip)


def bake_kit(parts):
    import numpy as np
    unwrap(parts)
    imgs = {k: new_image("ARbake_" + k, k != "color") for k in ("color", "metal", "rough", "ao")}
    together = [(o, []) for o in parts]
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
    ms = bpy.data.images.new("ARbake_ms", TEX, TEX, alpha=True)
    ms.colorspace_settings.name = "Non-Color"
    ms.alpha_mode = "CHANNEL_PACKED"
    ms.pixels.foreach_set(packed)
    paths = {"BaseColor": save_image(imgs["color"], NAME + "_BaseColor.png"),
             "MetallicSmoothness": save_image(ms, NAME + "_MetallicSmoothness.png"),
             "Occlusion": save_image(imgs["ao"], NAME + "_Occlusion.png")}
    for image in list(imgs.values()) + [ms]:
        bpy.data.images.remove(image)
    mat = bpy.data.materials.new("AR_Kit")
    nodes_on(mat)
    tree = mat.node_tree
    bsdf = principled(mat)
    base = tree.nodes.new("ShaderNodeTexImage")
    base.image = load_image(paths["BaseColor"], False)
    tree.links.new(base.outputs["Color"], bsdf.inputs["Base Color"])
    msn = tree.nodes.new("ShaderNodeTexImage")
    msn.image = load_image(paths["MetallicSmoothness"], True)
    msn.image.alpha_mode = "CHANNEL_PACKED"
    sep = tree.nodes.new("ShaderNodeSeparateColor")
    tree.links.new(msn.outputs["Color"], sep.inputs["Color"])
    tree.links.new(sep.outputs[0], bsdf.inputs["Metallic"])
    inv = tree.nodes.new("ShaderNodeMath")
    inv.operation = "SUBTRACT"
    inv.inputs[0].default_value = 1.0
    tree.links.new(msn.outputs["Alpha"], inv.inputs[1])
    tree.links.new(inv.outputs[0], bsdf.inputs["Roughness"])
    for obj in parts:
        obj.data.materials.clear()
        obj.data.materials.append(mat)
        for polygon in obj.data.polygons:
            polygon.material_index = 0
    log("atlas baked for", [o.name for o in parts])
    return paths


def build_render_set(scene, collection):
    world = bpy.data.worlds.new("AR_World")
    nodes_on(world)
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.030, 0.032, 0.036, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.0
    scene.world = world
    plate = bpy.data.materials.new("AR_PreviewBulkhead")
    nodes_on(plate)
    principled(plate).inputs["Base Color"].default_value = hex_rgba("#B5B3AE")
    principled(plate).inputs["Roughness"].default_value = 0.6
    bm = new_bm()
    box(bm, -0.70, 0.90, 0.0, 0.030, -0.80, 0.35, 0, bevel=0.004, segs=1)
    bmesh.ops.transform(bm, matrix=R180, verts=list(bm.verts))
    mesh = bpy.data.meshes.new("AR_PreviewBulkheadMesh")
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(plate)
    collection.objects.link(bpy.data.objects.new("PreviewOnly bulkhead", mesh))
    for name, loc, power, size in (("AR_Key", (0.55, 0.55, -0.85), 28.0, 0.6), ("AR_Fill", (-0.65, 0.05, -0.55), 9.0, 0.9),
                                   ("AR_Rim", (0.10, 0.70, -0.10), 6.0, 0.4)):
        data = bpy.data.lights.new(name, "AREA")
        data.energy, data.size = power, size
        light = bpy.data.objects.new(name, data)
        light.location = unity_to_blender(loc)
        look_at(light, unity_to_blender((0.15, -0.08, -0.08)))
        collection.objects.link(light)
    cams = {}
    for key, loc, target, lens in (("overview", (0.36, 0.14, -0.75), (0.20, -0.10, -0.10), 38.0),
                                   ("connector", (0.20, 0.10, -0.34), (0.0, -0.02, -0.10), 38.0),
                                   ("wrench", (0.32, 0.22, -0.50), (0.32, -0.03, -0.20), 35.0),
                                   ("engaged", (0.26, 0.16, -0.40), (0.06, -0.03, -0.09), 38.0)):
        data = bpy.data.cameras.new("AR_Cam_" + key)
        data.lens = lens
        data.clip_start = 0.005
        cam = bpy.data.objects.new("AR_Cam_" + key, data)
        cam.location = unity_to_blender(loc)
        look_at(cam, unity_to_blender(target))
        collection.objects.link(cam)
        cams[key] = cam
    return cams


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.render.engine = "CYCLES"
    device = enable_gpu(scene)
    full = STAGE == "full"
    log("Blender", bpy.app.version_string, "| stage", STAGE, "| device", device)
    kit = bpy.data.collections.new("AntennaRepair")
    scene.collection.children.link(kit)
    surface = [make_proc_material(k) if full else make_flat_material(k) for k in MAT_KEYS]
    if full:
        PROC.extend(surface)
    rec = finish(receptacle(), "ConnectorReceptacle", surface)
    variants = []
    for name, build, points in (("ConnectorRepaired", repaired_parts, REPAIRED_CABLE), ("ConnectorDamaged", damaged_parts, DAMAGED_CABLE)):
        obj = finish(build(), name, surface)
        wire = cable(points, "AR_Cable" + name, surface)
        select_only([wire, obj], active=obj)
        bpy.ops.object.join()
        variants.append(bpy.context.view_layer.objects.active)
    rep, dam = variants
    wr = finish(wrench(), "ServiceWrench", surface, origin=unity_to_blender(ANCHORS["WrenchGrip"]))
    parts = [rec, rep, dam, wr]
    anchors = build_anchors()
    report = {"blender": bpy.app.version_string, "stage": STAGE, "cycles_device": device}
    if full:
        paths = bake_kit(parts)
        for mat in PROC:
            bpy.data.materials.remove(mat)
        report["textures"] = {k: os.path.relpath(v, OUT).replace(BS, "/") for k, v in paths.items()}
        report["texture_size"] = TEX
    else:
        for obj in parts:
            compact_materials(obj)
    for obj in parts:
        weighted_normals(obj)
    for obj in parts + anchors:
        for col in list(obj.users_collection):
            col.objects.unlink(obj)
        kit.objects.link(obj)
    bpy.context.view_layer.update()
    triangles = {o.name: tri_count(o) for o in parts}
    report.update({
        "units": "metres; Unity axes: Y up; origin = ConnectorMount, the wall face the flange is screwed to (plane Z = 0); the technician is on -Z",
        "objects": {
            "ConnectorReceptacle": "static: bulkhead receptacle with flange and the cable grommet below it; origin ConnectorMount",
            "ConnectorRepaired": "state 2: plug seated and locked (white marks aligned), safety wire, yellow ID sleeve, cable looped into the grommet; origin ConnectorMount",
            "ConnectorDamaged": "state 1: plug backed out %.3f m and turned 24 deg (marks misaligned), scorched nut and backshell, melted boot, torn jacket with bird-caged and broken copper strands; origin ConnectorMount" % PLUG_BACK,
            "ServiceWrench": "tethered C-jaw connector wrench with rubber soft jaw, ratchet lever, 35 mm glove grip and tether ring; origin WrenchGrip",
        },
        "use": "Show exactly one of ConnectorDamaged / ConnectorRepaired over ConnectorReceptacle; all three share the origin, so the swap needs no offset. "
               "To engage the wrench, put WrenchTip on PlugGripBackedOut (damaged) or PlugGrip (seated) with the WrenchTip axis along the connector axis; "
               "the seating move is 0.012 m along +Z plus a 24 deg turn.",
        "dimensions_m": {"flange_square": 0.090, "flange_thickness": FACE, "coupling_nut_diameter": 0.057, "plug_tip_from_wall": FACE + 0.150,
                         "cable_diameter": 0.017, "grommet_plate_square": 0.048, "wrench_length": 0.3305, "wrench_jaw_inner_diameter": 0.060,
                         "wrench_jaw_opening": 0.0344, "wrench_grip_diameter": 0.035, "wrench_grip_length": 0.120,
                         "damaged_backout": PLUG_BACK, "damaged_turn_deg": 24.0},
        "anchors": {o.name: {"unity": [round(v, 4) for v in to_unity(o.matrix_world.translation)], "direction_unity": list(DIRECTIONS[o.name])} for o in anchors},
        "bounds_unity": {o.name: dict(zip(("min", "max"), unity_bounds([o]))) for o in parts},
        "triangles": triangles, "triangles_total": sum(triangles.values()),
        "materials": {o.name: [mm.name for mm in o.data.materials] for o in parts},
        "physical_assumptions": "Fictional hardware: a generic circular RF/data connector and an EVA-style tethered wrench, not a specific flight part; sizes are plausible for gloved hands.",
    })
    log("triangles", triangles, "total", report["triangles_total"])
    rs = bpy.data.collections.new("AR_RenderSet")
    scene.collection.children.link(rs)
    cams = build_render_set(scene, rs)
    pre = "" if full else "geo_"
    samples = SAMPLES if full else 48
    size = (1600, 1000)
    dam.hide_render = True
    render(scene, cams["overview"], pre + "01-overview-repaired.png", size, samples)
    render(scene, cams["connector"], pre + "03-connector-repaired.png", size, samples)
    dam.hide_render, rep.hide_render = False, True
    render(scene, cams["connector"], pre + "02-connector-damaged.png", size, samples)
    render(scene, cams["wrench"], pre + "04-service-wrench.png", size, samples)
    rest = wr.matrix_world.copy()
    wr.matrix_world = engaged_matrix()
    render(scene, cams["engaged"], pre + "05-wrench-on-backed-out-nut.png", size, samples)
    wr.matrix_world = rest
    rep.hide_render = False
    bpy.context.view_layer.update()
    if full:
        cut = []
        for mat in {slot for obj in parts for slot in obj.data.materials if slot is not None and slot.node_tree}:
            for link in list(mat.node_tree.links):
                if link.from_node.type == "TEX_IMAGE":
                    cut.append((mat.node_tree, link.from_socket, link.to_socket))
                    mat.node_tree.links.remove(link)
        select_only(parts + anchors, active=parts[0])
        bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, NAME + ".fbx"), use_selection=True, object_types={"MESH", "EMPTY"},
                                 apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
                                 use_mesh_modifiers=True, mesh_smooth_type="FACE", use_triangles=True, add_leaf_bones=False,
                                 bake_anim=False, path_mode="RELATIVE", embed_textures=False)
        for tree, source, target in cut:
            tree.links.new(source, target)
        for obj in list(rs.objects):
            bpy.data.objects.remove(obj, do_unlink=True)
        bpy.data.collections.remove(rs)
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
