"""
OUT OF REACH - astronaut gloved hands and forearms for the first-person view (original asset, built from code).

Builds both hands as a hierarchy of rigid, padded parts (no skinning), bakes one PBR set that both hands share,
renders previews, writes the rig and the poses neutral / grip / press as JSON, exports the FBX and saves the .blend.
Everything is generated here: no external models, textures or logos.

The FBX holds the 34 parts side by side at their rest places, without parents: Blender's exporter converts the axes
of a hierarchy deeper than one level wrongly for Unity. The hierarchy is in AstronautHands_rig.json (and in the
Unity prefab that is built from it); the .blend keeps it too.

    blender -b --factory-startup --python build_astronaut_hands.py -- --stage full
    blender -b --factory-startup --python build_astronaut_hands.py -- --stage geometry   (fast shape and pose check)

Axes (Blender): the eye is the origin and looks along -Y; +Z is up; the viewer's right is -X. Units: metres.
Rest pose: both hands flat, palms down, fingers forward, every object at identity rotation.
Per hand: Arm (pivot on the wrist) > Hand > Thumb1-3, Index1-3, Middle1-3, Ring1-3, Little1-3; "R_" and "L_" prefixes.
The shared helpers come from the HGA-1 antenna script.
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
    print("[hands %6.1fs]" % (time.time() - T0), *parts, flush=True)


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
NAME = "AstronautHands"
WRIST = Vector((-0.170, -0.450, -0.220))         # right wrist in the rest pose, seen from the eye; the left one mirrors it
UP = Vector((0.0, 0.0, 1.0))                     # back of the hand in the rest pose
PALM_L, PALM_W, PALM_T = 0.100, 0.050, 0.021     # palm: length to the knuckle line, half-width, half-thickness
ARM_L = 0.320                                    # visible forearm behind the wrist
RAIL_R = 0.017                                   # the ship's handrail tube, for the grip pose
# right hand, in hand space (origin on the wrist, fingers along -y, thumb towards +x, back of the hand +z)
FINGERS = (
    #  name      knuckle (x, y, z)            splay   segment lengths          radius
    ("Index",  (0.0345, -0.100, 0.000),    4.0, (0.044, 0.027, 0.024), 0.0125),
    ("Middle", (0.0115, -0.104, 0.001),    1.0, (0.049, 0.030, 0.025), 0.0125),
    ("Ring",   (-0.0115, -0.100, 0.000),  -2.0, (0.045, 0.028, 0.024), 0.0120),
    ("Little", (-0.0340, -0.092, -0.001), -6.0, (0.035, 0.022, 0.021), 0.0110),
)
THUMB_BASE = (0.040, -0.030, -0.006)
THUMB_DIR = Vector((0.766, -0.643, -0.150)).normalized()        # about 50 degrees out from the fingers, slightly down
THUMB_CURL = Vector((-0.55, -0.25, -0.80))                      # where the tip goes when the thumb flexes
THUMB_LEN, THUMB_RAD = (0.040, 0.032, 0.028), (0.0150, 0.0135, 0.0125)
# poses, degrees. Finger: ((knuckle flex, spread), middle joint, end joint). Thumb: ((flex, spread, opposition), middle, end).
# Hand: (wrist flex towards the palm, sideways). Flexing closes the hand.
POSES = {
    "Neutral": {"Hand": (-4, 0), "Thumb": ((8, 0, 14), 14, 16), "Index": ((16, 3), 26, 14), "Middle": ((22, 0), 30, 16),
                "Ring": ((26, -2), 32, 18), "Little": ((30, -5), 32, 18)},
    "Grip":    {"Hand": (24, -14), "Thumb": ((26, -12, 64), 30, 40), "Index": ((70, 2), 70, 30), "Middle": ((72, 0), 72, 32),
                "Ring": ((73, -1), 72, 32), "Little": ((74, -3), 72, 32)},
    "Press":   {"Hand": (18, -10), "Thumb": ((30, -20, 44), 36, 40), "Index": ((3, 0), 4, 2), "Middle": ((72, 0), 92, 52),
                "Ring": ((74, -1), 94, 52), "Little": ((76, -3), 94, 52)},
}
GRIP_CENTRE = Vector((0.0, -0.082, -0.033))      # axis of a gripped handrail tube, in hand space
RAIL = Vector((0.0, -0.470, -0.205))             # centre of the stand-in handrail in the grip preview
BUTTON = Vector((-0.062, -0.612, -0.128))        # stand-in button in the press preview
# suggested placements of the right arm for the first-person view. "turn": yaw about +Z (positive turns the hand
# inwards), pitch about +X (negative raises the fingers and drops the elbow), roll about the forearm, in degrees.
# The wrist position is given from the eye, or follows from what the hand holds or touches.
PLACEMENTS = {
    "Neutral": {"turn": (14.0, -14.0, 30.0), "wrist": (-0.190, -0.400, -0.215)},
    "Grip":    {"turn": (14.0, -28.0, 0.0), "grip": (-0.130, 0.0, 0.0)},         # where on the rail the hand closes
    "Press":   {"turn": (12.0, -22.0, 6.0), "touch": (0.0, 0.006, 0.0)},         # fingertip on the button face
}


# ------------------------------------------------------------------ materials
MAT_KEYS = ["fabric", "pad", "gray", "graphite", "steel", "dark", "red", "ring"]
FABRIC, PAD, GRAY, GRAPHITE, STEEL, DARK, RED, RING = range(8)
MAT_SPEC = {
    #             base sRGB  metal rough  wear  worn sRGB  worn metal  dirt
    "fabric":    ("#D5D3CB", 0.00, 0.80, 0.18, "#BDBBB3", 0.0, 0.55),     # white suit fabric
    "pad":       ("#59636E", 0.00, 0.60, 0.25, "#7A848E", 0.0, 0.45),     # silicone grip pads
    "gray":      ("#8A8884", 0.00, 0.72, 0.22, "#727069", 0.0, 0.50),     # knuckle guard, the ship's paint grey
    "graphite":  ("#2B2B2C", 0.20, 0.55, 0.40, "#5E6165", 0.5, 0.45),     # strap
    "steel":     ("#B6B9BC", 1.00, 0.38, 0.00, "#B6B9BC", 1.0, 0.50),     # lock collar, buckle
    "dark":      ("#0D0D0E", 0.00, 0.58, 0.00, "#0D0D0E", 0.0, 0.30),
    "red":       ("#B32A22", 0.00, 0.72, 0.18, "#8E2019", 0.0, 0.40),     # suit identification stripe, the ship's red
    "ring":      ("#2B2B2C", 0.60, 0.42, 0.45, "#8D8F92", 1.0, 0.40),     # anodised wrist ring
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
    mat = bpy.data.materials.new("AH_" + key)
    nodes_on(mat)
    node = principled(mat)
    node.inputs["Base Color"].default_value = hex_rgba(base_hex)
    node.inputs["Metallic"].default_value = metal
    node.inputs["Roughness"].default_value = rough
    return mat


def make_proc_material(key):
    """Source material with restrained procedural wear; only used as the bake source."""
    base_hex, metal, rough, wear, wear_hex, wear_metal, dirt = MAT_SPEC[key]
    mat = bpy.data.materials.new("AHsrc_" + key)
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


MIRROR = Matrix.Diagonal((-1.0, 1.0, 1.0, 1.0))
TO_UNITY = Matrix(((-1.0, 0.0, 0.0), (0.0, 0.0, 1.0), (0.0, -1.0, 0.0)))     # Blender (x, y, z) -> Unity (-x, z, -y)


def finger_dir(splay):
    """Rest direction of a finger in hand space; a positive splay leans it towards the thumb."""
    return Vector((sin(radians(splay)), -cos(radians(splay)), 0.0))


def flex_axis(direction, towards=None):
    """Axis about which a positive turn carries `direction` towards the palm (or towards the given vector)."""
    goal = -UP if towards is None else Vector(towards)
    goal = (goal - goal.dot(direction) * direction).normalized()
    return direction.cross(goal).normalized()


def segment(bm, joint, direction, length, radius, tip):
    """One padded finger part: a capsule from its joint along a direction, with a grip pad on the palm side."""
    frame = Matrix.Translation(joint) @ direction.to_track_quat("Z", "Y").to_matrix().to_4x4()
    r = radius
    profile = [(0.0, -0.78 * r), (0.52 * r, -0.60 * r), (0.88 * r, -0.26 * r), (r, 0.12 * r)]
    if tip:
        profile += [(r, length * 0.55), (0.92 * r, length - 0.62 * r), (0.70 * r, length - 0.24 * r), (0.36 * r, length - 0.04 * r), (0.0, length + 0.02 * r)]
    else:
        profile += [(0.97 * r, length * 0.60), (0.93 * r, length)]
    before = set(bm.faces)
    lathe(bm, frame, profile, 12, [FABRIC] * (len(profile) - 1), cap_mat=None if tip else FABRIC)
    for face in set(bm.faces) - before:
        face.normal_update()
        along = (face.calc_center_median() - joint).dot(direction)
        if (face.normal.dot(UP) < -0.35 and along > 0.22 * length) or (tip and along > length - 0.60 * r):
            face.material_index = PAD


def blob(bm, centre, radius, squash, mat):
    """Flattened sphere, used for the padded joints of the glove."""
    frame = Matrix.Translation(centre) @ Matrix.Diagonal((1.0, 1.0, squash, 1.0)) @ RXM90
    steps = 7
    profile = [(radius * sin(pi * i / steps), -radius * cos(pi * i / steps)) for i in range(steps + 1)]
    profile[0], profile[-1] = (0.0, -radius), (0.0, radius)
    lathe(bm, frame, profile, 14, [mat] * steps)


def build_arm():
    """Forearm in hand space: wrist disconnect ring with its lock collar, suit sleeve and identification stripe."""
    bm = new_bm()
    profile = [(0.0440, 0.004), (0.0500, 0.012), (0.0525, 0.016), (0.0525, 0.028), (0.0560, 0.028), (0.0560, 0.040), (0.0525, 0.040),
               (0.0525, 0.050), (0.0490, 0.054), (0.0500, 0.075), (0.0530, 0.120), (0.0560, 0.200), (0.0600, 0.300), (0.0600, ARM_L)]
    mats = [RING, RING, RING, STEEL, STEEL, STEEL, RING, RING, FABRIC, FABRIC, FABRIC, FABRIC, FABRIC]
    lathe(bm, RXM90.copy(), profile, 24, mats, cap_mat=DARK, start_cap=True)
    lathe(bm, RXM90.copy(), [(0.0571, 0.214), (0.0580, 0.236)], 24, [RED])                    # identification stripe
    for k in range(3):                                                                        # lock tabs on the collar
        tab = Matrix.Rotation(radians(120.0 * k + 30.0), 4, "Y") @ Matrix.Translation((0.0, 0.034, 0.0575))
        box_m(bm, tab, (0.016, 0.008, 0.004), RING, bevel=0.001)
    return bm


def build_hand():
    """Palm in hand space: padded palm, wrist and thumb-base joints, knuckle guard, grip pad and strap."""
    bm = new_bm()
    box(bm, -PALM_W, PALM_W, -PALM_L - 0.004, 0.004, -PALM_T, PALM_T, FABRIC, bevel=0.018, segs=4)
    blob(bm, Vector((0.0, 0.008, 0.0)), 0.0435, 0.80, FABRIC)                                 # wrist joint
    blob(bm, Vector((0.033, -0.036, -0.007)), 0.0215, 0.85, FABRIC)                           # thumb base
    blob(bm, Vector((-0.036, -0.045, -0.006)), 0.0170, 0.80, FABRIC)                          # heel of the hand
    box(bm, -0.045, 0.045, -0.103, -0.064, PALM_T - 0.006, PALM_T + 0.0055, GRAY, bevel=0.005, segs=3)     # knuckle guard
    box(bm, -0.043, 0.043, -0.096, -0.018, -PALM_T - 0.004, -PALM_T + 0.004, PAD, bevel=0.003, segs=2)     # grip pad
    box(bm, -PALM_W - 0.002, PALM_W + 0.002, -0.040, -0.024, -PALM_T - 0.002, PALM_T + 0.002, GRAPHITE, bevel=0.002)   # strap
    box(bm, -0.012, 0.012, -0.043, -0.021, PALM_T + 0.002, PALM_T + 0.006, STEEL, bevel=0.0015)             # buckle
    return bm


def right_hand_plan():
    """Every part of the right hand in hand space: (name, parent, joint, builder, flex axis)."""
    plan = [("Arm", None, Vector((0.0, 0.0, 0.0)), build_arm, None),
            ("Hand", "Arm", Vector((0.0, 0.0, 0.0)), build_hand, Vector((1.0, 0.0, 0.0)))]

    def chain(name, base, direction, lengths, radii, axis):
        joint, parent = Vector(base), "Hand"
        for i, (length, radius) in enumerate(zip(lengths, radii)):
            def builder(joint=joint.copy(), length=length, radius=radius, tip=(i == 2)):
                bm = new_bm()
                segment(bm, joint, direction, length, radius, tip)
                return bm
            plan.append((name + str(i + 1), parent, joint.copy(), builder, axis))
            parent = name + str(i + 1)
            joint = joint + direction * length

    chain("Thumb", THUMB_BASE, THUMB_DIR, THUMB_LEN, THUMB_RAD, flex_axis(THUMB_DIR, THUMB_CURL))
    for name, base, splay, lengths, radius in FINGERS:
        direction = finger_dir(splay)
        chain(name, base, direction, lengths, (radius, radius * 0.96, radius * 0.92), flex_axis(direction))
    return plan


def turn(axis, degrees):
    return Matrix.Rotation(radians(degrees), 3, Vector(axis).normalized())


def pose_rotations(pose):
    """Local rotation of every joint of the right hand for a pose, as 3x3 matrices in rest axes."""
    spec = POSES[pose]
    result = {"Arm": Matrix.Identity(3)}
    flex, side = spec["Hand"]
    result["Hand"] = turn((0, 0, 1), side) @ turn((1, 0, 0), flex)
    (flex, spread, opposition), middle, end = spec["Thumb"]
    axis = flex_axis(THUMB_DIR, THUMB_CURL)
    result["Thumb1"] = turn((0, 1, 0), opposition) @ turn((0, 0, 1), spread) @ turn(axis, flex)
    result["Thumb2"], result["Thumb3"] = turn(axis, middle), turn(axis, end)
    for name, _base, splay, _lengths, _radius in FINGERS:
        (flex, spread), middle, end = spec[name]
        axis = flex_axis(finger_dir(splay))
        result[name + "1"] = turn((0, 0, 1), spread) @ turn(axis, flex)
        result[name + "2"], result[name + "3"] = turn(axis, middle), turn(axis, end)
    return result


def index_tip(rotations):
    """Tip of the index finger in hand space for the given joint rotations."""
    _name, base, splay, lengths, radius = FINGERS[0]
    d = finger_dir(splay)
    j1 = Vector(base)
    j2 = j1 + d * lengths[0]
    j3 = j2 + d * lengths[1]
    end = j3 + d * (lengths[2] + 0.02 * radius)
    r1, r2, r3 = rotations["Index1"], rotations["Index2"], rotations["Index3"]
    return j1 + r1 @ (j2 - j1) + r1 @ r2 @ (j3 - j2) + r1 @ r2 @ r3 @ (end - j3)


def placement(pose):
    """Suggested position and rotation of the right arm for a pose, in Blender axes from the eye."""
    spec = PLACEMENTS[pose]
    yaw, pitch, roll = spec["turn"]
    arm = turn((0, 0, 1), yaw) @ turn((1, 0, 0), pitch) @ turn((0, -1, 0), roll)
    if "wrist" in spec:
        return Vector(spec["wrist"]), arm
    rotations = pose_rotations(pose)
    if "grip" in spec:
        target, local = RAIL + Vector(spec["grip"]), GRIP_CENTRE
    else:
        target, local = BUTTON + Vector(spec["touch"]), index_tip(rotations)
    return target - arm @ rotations["Hand"] @ local, arm


def mirrored(matrix3):
    """The same rotation for the left hand."""
    flip = Matrix.Diagonal((-1.0, 1.0, 1.0))
    return flip @ matrix3 @ flip


def unity_quaternion(matrix3):
    """Rotation in Unity axes as (x, y, z, w)."""
    q = (TO_UNITY @ matrix3 @ TO_UNITY.transposed()).to_quaternion()
    return [round(q.x, 6), round(q.y, 6), round(q.z, 6), round(q.w, 6)]


def unity_point(vector):
    p = TO_UNITY @ Vector(vector)
    return [round(p.x, 5), round(p.y, 5), round(p.z, 5)]


def unity_axis(vector):
    """A rotation axis in Unity axes. The axis change mirrors x, so the axis turns round to keep the same sense."""
    p = -(TO_UNITY @ Vector(vector))
    return [round(p.x + 0.0, 5), round(p.y + 0.0, 5), round(p.z + 0.0, 5)]


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
    """Nothing on the hands is hidden against a wall."""
    return


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
    """Lights, cameras and stand-ins that are not part of the model: a handrail, a panel with a button and a floor."""
    collection = bpy.data.collections.new("AH_RenderSet")
    scene.collection.children.link(collection)
    stand_ins = {}

    def material(name, hex_color, metallic, roughness):
        mat = bpy.data.materials.new(name)
        nodes_on(mat)
        principled(mat).inputs["Base Color"].default_value = hex_rgba(hex_color)
        principled(mat).inputs["Metallic"].default_value = metallic
        principled(mat).inputs["Roughness"].default_value = roughness
        return mat

    def solid(name, mat, build):
        bm = bmesh.new()
        build(bm)
        mesh = bpy.data.meshes.new(name)
        bm.to_mesh(mesh)
        bm.free()
        mesh.materials.append(mat)
        obj = bpy.data.objects.new(name, mesh)
        collection.objects.link(obj)
        stand_ins[name] = obj
        return obj

    def rod(bm, p0, p1, radius, segs=20):
        p0, p1 = Vector(p0), Vector(p1)
        axis = p1 - p0
        frame = Matrix.Translation(p0) @ axis.to_track_quat("Z", "Y").to_matrix().to_4x4()
        a = [bm.verts.new(frame @ Vector((radius * cos(2 * pi * k / segs), radius * sin(2 * pi * k / segs), 0.0))) for k in range(segs)]
        b = [bm.verts.new(frame @ Vector((radius * cos(2 * pi * k / segs), radius * sin(2 * pi * k / segs), axis.length))) for k in range(segs)]
        for k in range(segs):
            bm.faces.new((a[k], a[(k + 1) % segs], b[(k + 1) % segs], b[k]))
        bm.faces.new(tuple(reversed(a)))
        bm.faces.new(tuple(b))

    def slab(bm, centre, size):
        bmesh.ops.create_cube(bm, size=1.0, matrix=Matrix.Translation(centre) @ Matrix.Diagonal((size[0], size[1], size[2], 1.0)))

    gold = material("AH_RenderGold", "#C39B3B", 1.0, 0.36)
    putty = material("AH_RenderPutty", "#8A8884", 0.0, 0.60)
    amber = material("AH_RenderAmber", "#C68A1C", 0.0, 0.50)
    floor = material("AH_RenderFloor", "#2A2D31", 0.2, 0.62)

    def rail(bm):
        rod(bm, RAIL + Vector((-0.42, 0.0, 0.0)), RAIL + Vector((0.42, 0.0, 0.0)), RAIL_R)
        for side in (-1, 1):
            rod(bm, RAIL + Vector((side * 0.40, 0.0, 0.0)), RAIL + Vector((side * 0.40, 0.0, -0.09)), RAIL_R, 12)

    solid("AH_RenderRail", gold, rail)
    solid("AH_RenderRailPlate", putty, lambda bm: slab(bm, RAIL + Vector((0.0, -0.10, -0.10)), (1.6, 0.9, 0.02)))
    solid("AH_RenderPanel", putty, lambda bm: slab(bm, Vector((0.0, BUTTON.y - 0.022, BUTTON.z)), (1.2, 0.02, 0.8)))
    solid("AH_RenderButton", amber, lambda bm: rod(bm, BUTTON + Vector((0.0, -0.012, 0.0)), BUTTON + Vector((0.0, 0.004, 0.0)), 0.016))
    solid("AH_RenderFloor", floor, lambda bm: slab(bm, Vector((0.0, -1.0, -1.20)), (12.0, 12.0, 0.02)))

    def area(name, location, target, power, size, color):
        data = bpy.data.lights.new(name, "AREA")
        data.energy, data.size, data.color = power, size, color
        obj = bpy.data.objects.new(name, data)
        obj.location = location
        look_at(obj, target)
        collection.objects.link(obj)

    area("AH_Key", (-0.45, 0.25, 0.75), (0.0, -0.45, -0.20), 26.0, 0.9, (0.96, 0.98, 1.0))
    area("AH_Fill", (0.80, -0.10, 0.05), (0.0, -0.45, -0.20), 7.0, 1.2, (1.0, 0.95, 0.88))
    area("AH_Front", (0.0, -1.30, 0.30), (0.0, -0.45, -0.20), 5.0, 1.0, (1.0, 1.0, 1.0))

    cameras = {}

    def camera(key, location, target=None, lens=None, vertical_fov=None):
        data = bpy.data.cameras.new("AH_Cam_" + key)
        data.clip_start = 0.05
        if vertical_fov is not None:
            data.sensor_fit = "VERTICAL"
            data.angle_y = radians(vertical_fov)
        else:
            data.lens = lens
        obj = bpy.data.objects.new("AH_Cam_" + key, data)
        obj.location = location
        if target is None:
            obj.rotation_euler = (radians(90.0), 0.0, radians(180.0))       # looks along -Y with +Z up: the eye
        else:
            look_at(obj, target)
        collection.objects.link(obj)
        cameras[key] = obj

    camera("eye", (0.0, 0.0, 0.0), vertical_fov=68.0)              # the game's camera: FOV 68, near clip 0.05
    camera("hero", (-0.62, -0.92, 0.02), (-0.165, -0.50, -0.225), lens=85.0)
    camera("grip", (-0.66, -0.80, 0.02), RAIL + Vector((-0.12, 0.03, 0.03)), lens=85.0)

    world = bpy.data.worlds.new("AH_World")
    nodes_on(world)
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.020, 0.023, 0.028, 1.0)
    scene.world = world
    scene.camera = cameras["eye"]
    for look in ("AgX - Medium High Contrast", "Medium High Contrast"):
        try:
            scene.view_settings.look = look
            break
        except TypeError:
            continue
    return cameras, stand_ins


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
    mat = bpy.data.materials.new("AH_WireOverlay")
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

    full = STAGE == "full"
    surface = [make_proc_material(k) if full else make_flat_material(k) for k in MAT_KEYS]
    if full:
        PROC.extend(surface)

    plan = right_hand_plan()
    joints = {name: joint for name, _parent, joint, _builder, _axis in plan}          # hand space
    parents = {name: parent for name, parent, _joint, _builder, _axis in plan}
    right = {}
    for name, _parent, joint, builder, _axis in plan:
        bm = builder()
        bmesh.ops.translate(bm, vec=WRIST, verts=bm.verts[:])
        right[name] = finish(bm, "R_" + name, surface, origin=WRIST + joint)
    baked = list(right.values())
    report = {"blender": bpy.app.version_string, "stage": STAGE, "cycles_device": device}

    if full:
        unwrap(baked)
        color = new_image("AHbake_color", False)
        metal = new_image("AHbake_metal", True)
        rough = new_image("AHbake_rough", True)
        occlusion = new_image("AHbake_ao", True)
        together = [(obj, []) for obj in baked]
        bake_pass("color", color, together, 32)
        bake_pass("metal", metal, together, 32)
        bake_pass("rough", rough, together, 32)
        bake_pass("ao", occlusion, together, 64)          # with the neighbours: creases between the fingers and at the joints

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
        metal_smooth = bpy.data.images.new("AHbake_ms", TEX, TEX, alpha=True)
        metal_smooth.colorspace_settings.name = "Non-Color"
        metal_smooth.alpha_mode = "CHANNEL_PACKED"
        metal_smooth.pixels.foreach_set(packed)

        paths = {
            "BaseColor": save_image(color, NAME + "_BaseColor.png"),
            "Metallic": save_image(metal, NAME + "_Metallic.png"),
            "Roughness": save_image(rough, NAME + "_Roughness.png"),
            "Occlusion": save_image(occlusion, NAME + "_Occlusion.png"),
            "MetallicSmoothness": save_image(metal_smooth, NAME + "_MetallicSmoothness.png"),
        }
        for image in (color, metal, rough, occlusion, metal_smooth):
            bpy.data.images.remove(image)

        final = bpy.data.materials.new("AH_" + NAME)
        nodes_on(final)
        tree = final.node_tree
        for index, (key, socket_name, non_color) in enumerate((("BaseColor", "Base Color", False), ("Metallic", "Metallic", True), ("Roughness", "Roughness", True))):
            node = tree.nodes.new("ShaderNodeTexImage")
            node.image = load_image(paths[key], non_color)
            node.location = (-520, 320 - index * 300)
            tree.links.new(node.outputs["Color"], principled(final).inputs[socket_name])
        for obj in baked:
            obj.data.materials.clear()
            obj.data.materials.append(final)
            for polygon in obj.data.polygons:
                polygon.material_index = 0
        for mat in PROC:
            bpy.data.materials.remove(mat)
        report["textures"] = {key: os.path.relpath(value, OUT).replace(os.sep, "/") for key, value in paths.items()}
        report["texture_size"] = TEX
    else:
        for obj in baked:
            compact_materials(obj)

    # The left hand mirrors the right one and shares its UVs, so one texture set serves both.
    left = {}
    for name, obj in right.items():
        mesh = obj.data.copy()
        mesh.name = NAME + "_L_" + name
        mesh.transform(MIRROR)
        mesh.flip_normals()
        twin = bpy.data.objects.new("L_" + name, mesh)
        scene.collection.objects.link(twin)
        twin.location = Vector((-obj.location.x, obj.location.y, obj.location.z))
        left[name] = twin
    hands = {"R": right, "L": left}
    for side in hands.values():
        for name, obj in side.items():
            parent = parents[name]
            if parent is not None:
                world = obj.location.copy()
                obj.parent = side[parent]
                obj.location = world - side[parent].matrix_world.translation
            bpy.context.view_layer.update()
    parts = [obj for side in ("R", "L") for obj in hands[side].values()]
    for obj in parts:
        obj.rotation_mode = "QUATERNION"
        weighted_normals(obj)
    bpy.context.view_layer.update()

    def pose(name, sides=("R", "L"), place=True):
        """Puts the given hands into a pose; the others return to the rest pose."""
        rotations = pose_rotations(name)
        for side, objects in hands.items():
            active = side in sides
            for part, obj in objects.items():
                rotation = rotations[part] if active else Matrix.Identity(3)
                if part == "Arm":
                    position, turn_ = placement(name) if (active and place) else (WRIST.copy(), Matrix.Identity(3))
                    if side == "L":
                        position, turn_ = Vector((-position.x, position.y, position.z)), mirrored(turn_)
                    obj.location = position
                    rotation = turn_
                elif side == "L":
                    rotation = mirrored(rotation)
                obj.rotation_quaternion = rotation.to_quaternion()
        bpy.context.view_layer.update()

    def rest():
        for side, objects in hands.items():
            for part, obj in objects.items():
                obj.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
            objects["Arm"].location = WRIST if side == "R" else Vector((-WRIST.x, WRIST.y, WRIST.z))
        bpy.context.view_layer.update()

    lo, hi = world_bounds(parts)
    triangles = {obj.name: tri_count(obj) for obj in parts}
    report.update({
        "dimensions_m": {"width_x": round(hi.x - lo.x, 4), "depth_y": round(hi.y - lo.y, 4), "height_z": round(hi.z - lo.z, 4)},
        "bounds_min": [round(v, 4) for v in lo],
        "bounds_max": [round(v, 4) for v in hi],
        "triangles_total": sum(triangles.values()),
        "triangles_per_hand": sum(count for name, count in triangles.items() if name.startswith("R_")),
        "objects": len(parts),
        "materials": sorted({mat.name for obj in parts for mat in obj.data.materials}),
    })
    log("dimensions", report["dimensions_m"], "objects", len(parts), "triangles", report["triangles_total"])

    # rig and poses in Unity axes, for the import check and for whoever drives the hands from a script
    rig = {"units": "metres; Unity axes: x right, y up, z forward; the model origin is the eye",
           "rest": "all local rotations identity; positions are local to the parent",
           "joints": [], "poses": []}
    for side, sign in (("R", 1.0), ("L", -1.0)):
        for name, _parent, joint, _builder, axis in plan:
            parent = parents[name]
            local = joint - joints[parent] if parent else WRIST + joint
            local = Vector((sign * local.x, local.y, local.z))
            entry = {"name": side + "_" + name, "parent": (side + "_" + parent) if parent else "", "position": unity_point(local)}
            if axis is not None:
                bend = Vector((sign * axis.x, axis.y, axis.z)) * sign       # mirrored axis, turning the same way towards the palm
                entry["flexAxis"] = unity_axis(bend)
            rig["joints"].append(entry)
    for name in POSES:
        rotations = pose_rotations(name)
        position, turn_ = placement(name)
        entry = {"name": name, "rotations": [], "placements": []}
        for side in ("R", "L"):
            for part, rotation in rotations.items():
                if part != "Arm":
                    entry["rotations"].append({"name": side + "_" + part, "rotation": unity_quaternion(rotation if side == "R" else mirrored(rotation))})
            p = position if side == "R" else Vector((-position.x, position.y, position.z))
            entry["placements"].append({"name": side + "_Arm", "position": unity_point(p), "rotation": unity_quaternion(turn_ if side == "R" else mirrored(turn_))})
        rig["poses"].append(entry)
    with open(os.path.join(OUT, NAME + "_rig.json"), "w", encoding="utf-8", newline=chr(10)) as handle:
        json.dump(rig, handle, indent=1)

    cameras, stand_ins = build_render_set(scene)

    def show(*names):
        for name, obj in stand_ins.items():
            obj.hide_render = name not in names and name != "AH_RenderFloor"

    views = (("Neutral", ("R", "L"), ()), ("Grip", ("R", "L"), ("AH_RenderRail", "AH_RenderRailPlate")),
             ("Press", ("R",), ("AH_RenderPanel", "AH_RenderButton")))
    if full:
        for name, sides, props in views:
            show(*props)
            pose(name, sides)
            if name == "Press":
                pose_left_neutral = pose_rotations("Neutral")
                position, turn_ = placement("Neutral")
                hands["L"]["Arm"].location = Vector((-position.x, position.y, position.z))
                hands["L"]["Arm"].rotation_quaternion = mirrored(turn_).to_quaternion()
                for part, rotation in pose_left_neutral.items():
                    if part != "Arm":
                        hands["L"][part].rotation_quaternion = mirrored(rotation).to_quaternion()
                bpy.context.view_layer.update()
            render(scene, cameras["eye"], NAME + "_fp_" + name.lower() + ".png", (1920, 1080), SAMPLES)
        show()
        pose("Neutral", ("R",), place=False)
        render(scene, cameras["hero"], NAME + "_hero.png", (1800, 1200), SAMPLES)
        show("AH_RenderRail", "AH_RenderRailPlate")
        pose("Grip", ("R", "L"))
        render(scene, cameras["grip"], NAME + "_grip.png", (1800, 1200), SAMPLES)
        show()
        rest()
        bpy.context.view_layer.material_override = wire_material()
        render(scene, cameras["hero"], NAME + "_wire.png", (1800, 1200), 48)
        bpy.context.view_layer.material_override = None
        scene.camera = cameras["eye"]

        # FBX first, while the scene is still unsaved and its texture links are cut: the exporter writes the
        # path of the .blend and of every linked texture into the file, and those are paths on this machine.
        cut = []
        for mat in {slot for obj in parts for slot in obj.data.materials if slot is not None and slot.node_tree}:
            for link in list(mat.node_tree.links):
                if link.from_node.type == "TEX_IMAGE":
                    cut.append((mat.node_tree, link.from_socket, link.to_socket))
                    mat.node_tree.links.remove(link)
        for obj in parts:
            obj.rotation_mode = "XYZ"
        # The exporter's baked axis conversion goes wrong below the first level of a hierarchy (it was checked in
        # Unity), so the parts are exported side by side at their rest places. The prefab and the rig file carry
        # the hierarchy.
        links = [(obj, obj.parent) for obj in parts]
        worlds = {obj: obj.matrix_world.copy() for obj in parts}
        for obj in parts:
            obj.parent = None
            obj.matrix_world = worlds[obj]
        bpy.context.view_layer.update()
        select_only(parts, active=hands["R"]["Arm"])
        bpy.ops.export_scene.fbx(
            filepath=os.path.join(OUT, NAME + ".fbx"), use_selection=True, object_types={"MESH"},
            apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
            bake_space_transform=True, use_mesh_modifiers=True, mesh_smooth_type="OFF", use_triangles=True,
            add_leaf_bones=False, bake_anim=False, path_mode="RELATIVE", embed_textures=False)
        for obj, parent in links:
            if parent is not None:
                obj.parent = parent
                obj.location = worlds[obj].translation - worlds[parent].translation
        bpy.context.view_layer.update()
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
        for name, sides, props in views:
            show(*props)
            pose(name, sides)
            render(scene, cameras["eye"], "geometry_fp_" + name.lower() + ".png", (1280, 720), 32)
        show()
        pose("Neutral", ("R",), place=False)
        render(scene, cameras["hero"], "geometry_hero.png", (1200, 800), 32)
        show("AH_RenderRail", "AH_RenderRailPlate")
        pose("Grip", ("R", "L"))
        render(scene, cameras["grip"], "geometry_grip.png", (1200, 800), 32)
        show()
        rest()

    with open(os.path.join(OUT, NAME + "_report.json" if full else "geometry_report.json"), "w", encoding="utf-8", newline=chr(10)) as handle:
        json.dump(report, handle, indent=2)
    log("done")


main()
