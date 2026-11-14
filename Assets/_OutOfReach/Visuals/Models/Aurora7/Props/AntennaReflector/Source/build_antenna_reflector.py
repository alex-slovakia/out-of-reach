"""
OUT OF REACH - reflector assembly for the ship's high-gain antenna (original asset, built from code).

A drop-in replacement for the elevation part of the antenna on feature/aurora7-models (Antenna_Elevation):
trunnion, hub, neck, 1.24 m reflector with panel seams, eight ribs and a stiffening ring, tripod and feed horn.
It keeps that part's envelope: trunnion 0.36 m long and 35 mm in radius, reflector vertex 0.20 m from the
trunnion axis, rim radius 0.626 m, depth 0.19 m, feed no further out than 0.02 m beyond the focus.

    blender -b --factory-startup --python build_antenna_reflector.py -- --stage full
    blender -b --factory-startup --python build_antenna_reflector.py -- --stage geometry   (fast shape check)

Axes (Blender): X = trunnion axis, Z = hull normal at the pad (outboard), -Y = aft along the hull.
Model pivot: centre of the trunnion (0, 0, 0). Units: metres. The mesh is delivered at the pose of the part it
replaces: boresight raised 60 degrees from the -Y direction, which is 30 degrees aft of the hull normal.
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
    print("[reflector %6.1fs]" % (time.time() - T0), *parts, flush=True)


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
NAME = "AntennaReflector"
TILT = 60.0                                      # delivered pose: boresight this many degrees above the -Y direction
TRUNNION_X, TRUNNION_R = 0.180, 0.035            # half-length and radius of the trunnion between the yoke arms
HUB = (0.110, 0.080, 0.080)                      # half-size of the hub block: along the trunnion, along the boresight, across
VERTEX = 0.200                                   # reflector vertex in front of the trunnion axis
DISH_R, DISH_F, DISH_T = 0.616, 0.5058, 0.012    # reflecting radius, focal length (depth 0.19 m at 0.62 m), shell thickness
RIM_R = 0.626
SEAM_RING = 0.340                                # radius of the ring seam on the reflector
LOWERED = 35.0                                   # the "lowered" preview turns the assembly this far towards the horizon


# ------------------------------------------------------------------ materials
MAT_KEYS = ["gray", "graphite", "steel", "dark", "dish"]
GRAY, GRAPHITE, STEEL, DARK, DISH = range(5)
MAT_SPEC = {
    #             base sRGB  metal rough  wear  worn sRGB  worn metal  dirt
    "gray":      ("#8A8884", 0.00, 0.44, 0.55, "#A9ABAE", 1.0, 0.55),     # the ship's A7_PaintGray, as on the yoke
    "graphite":  ("#2B2B2C", 0.30, 0.50, 0.50, "#5E6165", 0.7, 0.45),
    "steel":     ("#B6B9BC", 1.00, 0.38, 0.00, "#B6B9BC", 1.0, 0.50),     # clear anodised aluminium
    "dark":      ("#0D0D0E", 0.00, 0.58, 0.00, "#0D0D0E", 0.0, 0.30),
    "dish":      ("#D9D8D1", 0.00, 0.50, 0.30, "#9A9C9E", 1.0, 0.40),     # warm white thermal paint
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


def add_dish_seams(mat):
    """Panel seams of the reflector, drawn into the bake source: twelve gores and one ring.

    The mesh is stored tilted, so the reflector's own axes are spelled out: its axis passes through the object
    origin, x is unchanged and the second transverse direction is (0, sin TILT, cos TILT)."""
    g = Graph(mat)
    split = g.nodes.new("ShaderNodeSeparateXYZ")
    g.links.new(g.nodes.new("ShaderNodeTexCoord").outputs["Object"], split.inputs[0])
    tilt = radians(TILT)
    x = split.outputs["X"]
    across = g.math("ADD", g.math("MULTIPLY", split.outputs["Y"], sin(tilt)), g.math("MULTIPLY", split.outputs["Z"], cos(tilt)))
    radius = g.math("SQRT", g.math("ADD", g.math("MULTIPLY", x, x), g.math("MULTIPLY", across, across)))
    arc = g.math("MULTIPLY", g.math("PINGPONG", g.math("ARCTAN2", across, x), pi / 12), radius)       # arc length to the nearest gore seam
    half = 0.0022
    gore = g.math("MULTIPLY", g.math("LESS_THAN", arc, half), g.math("GREATER_THAN", radius, 0.070))
    ring_seam = g.math("LESS_THAN", g.math("ABSOLUTE", g.math("SUBTRACT", radius, SEAM_RING)), half)
    seam = g.math("MULTIPLY", g.math("MAXIMUM", gore, ring_seam), 0.62)
    color = g.mix(seam, SOCK[mat.name]["color"], hex_rgba("#6F6E69"))
    g.links.new(color, principled(mat).inputs["Base Color"])
    SOCK[mat.name]["color"] = color


def sag(r):
    """Depth of the paraboloid at radius r."""
    return r * r / (4.0 * DISH_F)


def build_reflector():
    """Trunnion, hub, neck, reflector with its back structure, tripod and feed horn; built looking along -Y, then tilted."""
    bm = new_bm()
    d = front(0.0, -VERTEX, 0.0)                    # origin at the reflector vertex, local +Z along the boresight
    rings = 8
    lip = DISH_R - 0.010
    edge = sag(DISH_R)
    back = [(0.0, -DISH_T)] + [(DISH_R * i / rings, sag(DISH_R * i / rings) - DISH_T) for i in range(1, rings + 1)]
    rim = [(RIM_R, edge - DISH_T), (RIM_R, edge + 0.004), (lip, edge + 0.004)]
    inner = [(lip * i / rings, sag(lip * i / rings)) for i in range(rings, 0, -1)] + [(0.0, 0.0)]
    profile = back + rim + inner
    lathe(bm, d, profile, 64, [DISH] * (len(profile) - 1))                                                         # reflector shell
    lathe(bm, d, [(0.060, sag(0.060) - 0.001), (0.060, 0.0055), (0.0, 0.0055)], 24, [STEEL, STEEL])                # vertex plate

    def rib_depth(r):
        return 0.055 - 0.039 * (r - 0.100) / 0.485

    for k in range(8):                                                                                             # radial ribs, tapering outwards
        spin = d @ Matrix.Rotation(radians(45.0 * k + 22.5), 4, "Z")
        stations = (0.100, 0.260, 0.420, 0.585)
        for r0, r1 in zip(stations[:-1], stations[1:]):
            corners = []
            for r in (r0, r1):
                top = sag(r) - DISH_T + 0.004
                corners += [spin @ Vector((r, side, z)) for side in (-0.005, 0.005) for z in (top, top - rib_depth(r))]
            hull(bm, corners, STEEL)
    r_ring = 0.370                                                                                                 # stiffening ring
    zt = sag(r_ring) - DISH_T + 0.004
    lathe(bm, d, [(r_ring - 0.008, zt), (r_ring - 0.008, zt - 0.040), (r_ring + 0.008, zt - 0.040), (r_ring + 0.008, zt)], 48, [STEEL] * 3)

    lathe(bm, d, [(0.050, -0.135), (0.050, -0.075), (0.130, -0.075), (0.130, -DISH_T + 0.003)], 28, [GRAY] * 3, start_cap=True)     # neck and flange
    hx, hy, hz = HUB
    box(bm, -hx, hx, -hy, hy, -hz, hz, GRAY, bevel=0.010)                                                          # hub block
    lathe(bm, Matrix.Translation((-TRUNNION_X, 0.0, 0.0)) @ RY90, [(TRUNNION_R, 0.0), (TRUNNION_R, 2 * TRUNNION_X)], 20, [STEEL],
          cap_mat=STEEL, start_cap=True)                                                                           # trunnion

    feed = d @ Matrix.Translation((0.0, 0.0, DISH_F))
    for k in range(3):                                                                                             # tripod
        a = radians(90.0 + 120.0 * k)
        foot = d @ Vector(((DISH_R - 0.001) * cos(a), (DISH_R - 0.001) * sin(a), edge + 0.003))
        head = feed @ Vector((0.036 * cos(a), 0.036 * sin(a), -0.008))
        tube(bm, foot, head, 0.0075, 8, STEEL, caps=False)
    lathe(bm, feed, [(0.045, -0.100), (0.055, -0.100), (0.024, -0.035), (0.040, -0.035), (0.040, 0.020)], 20,
          [DARK, STEEL, GRAPHITE, GRAPHITE], cap_mat=GRAPHITE, start_cap=True)                                     # feed horn
    bmesh.ops.transform(bm, matrix=Matrix.Rotation(radians(-TILT), 4, "X"), verts=bm.verts[:])                     # delivered pose
    return bm


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
    """Nothing on this part is hidden against a wall."""
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
    """Lights, cameras and stand-ins that are not part of the model: the ship's yoke and mast from its dimensions, and a floor."""
    collection = bpy.data.collections.new("AR_RenderSet")
    scene.collection.children.link(collection)
    stand = bpy.data.materials.new("AR_RenderStandIn")
    nodes_on(stand)
    principled(stand).inputs["Base Color"].default_value = hex_rgba("#6F6D6A")
    principled(stand).inputs["Roughness"].default_value = 0.55
    ground = bpy.data.materials.new("AR_RenderFloor")
    nodes_on(ground)
    principled(ground).inputs["Base Color"].default_value = hex_rgba("#2A2D31")
    principled(ground).inputs["Roughness"].default_value = 0.62
    principled(ground).inputs["Metallic"].default_value = 0.2

    bm = bmesh.new()

    def block(centre, size):
        bmesh.ops.create_cube(bm, size=1.0, matrix=Matrix.Translation(centre) @ Matrix.Diagonal((size[0], size[1], size[2], 1.0)))

    def rod(p0, p1, radius, segs=20):
        p0, p1 = Vector(p0), Vector(p1)
        axis = p1 - p0
        frame = Matrix.Translation(p0) @ axis.to_track_quat("Z", "Y").to_matrix().to_4x4()
        ring0 = [bm.verts.new(frame @ Vector((radius * cos(2 * pi * k / segs), radius * sin(2 * pi * k / segs), 0.0))) for k in range(segs)]
        ring1 = [bm.verts.new(frame @ Vector((radius * cos(2 * pi * k / segs), radius * sin(2 * pi * k / segs), axis.length))) for k in range(segs)]
        for k in range(segs):
            bm.faces.new((ring0[k], ring0[(k + 1) % segs], ring1[(k + 1) % segs], ring1[k]))
        bm.faces.new(tuple(reversed(ring0)))
        bm.faces.new(tuple(ring1))

    # the yoke of the ship's antenna: trunnion 0.34 m above the turntable, arms 0.36 m apart inside
    for side in (-1, 1):
        block((side * 0.205, 0.0, -0.090), (0.05, 0.13, 0.34))
        rod((side * 0.180, 0.0, 0.0), (side * 0.240, 0.0, 0.0), 0.050)
    rod((0.240, 0.0, 0.0), (0.330, 0.0, 0.0), 0.060)
    block((0.0, 0.0, -0.255), (0.46, 0.15, 0.05))
    rod((0.0, 0.0, -0.340), (0.0, 0.0, -0.280), 0.130, 28)
    rod((0.0, 0.0, -0.660), (0.0, 0.0, -0.340), 0.085, 24)
    mesh = bpy.data.meshes.new("AR_RenderYoke")
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(stand)
    collection.objects.link(bpy.data.objects.new("AR_RenderYoke", mesh))

    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0, matrix=Matrix.Translation((0.0, 0.0, -0.670)) @ Matrix.Diagonal((14.0, 14.0, 0.02, 1.0)))
    mesh = bpy.data.meshes.new("AR_RenderFloor")
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(ground)
    collection.objects.link(bpy.data.objects.new("AR_RenderFloor", mesh))

    def area(name, location, target, power, size, color):
        data = bpy.data.lights.new(name, "AREA")
        data.energy, data.size, data.color = power, size, color
        obj = bpy.data.objects.new(name, data)
        obj.location = location
        look_at(obj, target)
        collection.objects.link(obj)

    area("AR_Key", (-2.6, -3.4, 3.6), (0.0, -0.15, 0.25), 560.0, 2.4, (0.94, 0.97, 1.0))
    area("AR_Fill", (3.0, -2.4, 0.5), (0.0, -0.15, 0.15), 150.0, 3.2, (1.0, 0.95, 0.88))
    area("AR_Rim", (0.6, 2.6, 2.6), (0.0, 0.0, 0.25), 300.0, 1.8, (1.0, 1.0, 1.0))

    cameras = {}

    def camera(key, location, target, lens):
        data = bpy.data.cameras.new("AR_Cam_" + key)
        data.lens = lens
        obj = bpy.data.objects.new("AR_Cam_" + key, data)
        obj.location = location
        look_at(obj, target)
        collection.objects.link(obj)
        cameras[key] = obj

    camera("hero", (-2.5, -3.1, 1.25), (0.0, -0.15, 0.10), 70.0)
    camera("back", (1.9, 3.0, 0.55), (0.0, -0.05, 0.12), 62.0)
    camera("detail", (0.95, 1.05, -0.30), (0.02, 0.02, 0.10), 50.0)
    camera("side", (3.6, -1.9, 0.75), (0.0, -0.15, 0.08), 70.0)

    world = bpy.data.worlds.new("AR_World")
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
    mat = bpy.data.materials.new("AR_WireOverlay")
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
        add_dish_seams(surface[DISH])

    reflector = finish(build_reflector(), "Reflector", surface)
    parts = [reflector]
    report = {"blender": bpy.app.version_string, "stage": STAGE, "cycles_device": device}

    if full:
        unwrap(parts)
        color = new_image("ARbake_color", False)
        metal = new_image("ARbake_metal", True)
        rough = new_image("ARbake_rough", True)
        occlusion = new_image("ARbake_ao", True)
        alone = [(reflector, [])]
        bake_pass("color", color, alone, 48)
        bake_pass("metal", metal, alone, 48)
        bake_pass("rough", rough, alone, 48)
        bake_pass("ao", occlusion, alone, 96)

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
        metal_smooth = bpy.data.images.new("ARbake_ms", TEX, TEX, alpha=True)
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

        final = bpy.data.materials.new("AR_" + NAME)
        nodes_on(final)
        tree = final.node_tree
        for index, (key, socket_name, non_color) in enumerate((("BaseColor", "Base Color", False), ("Metallic", "Metallic", True), ("Roughness", "Roughness", True))):
            node = tree.nodes.new("ShaderNodeTexImage")
            node.image = load_image(paths[key], non_color)
            node.location = (-520, 320 - index * 300)
            tree.links.new(node.outputs["Color"], principled(final).inputs[socket_name])
        reflector.data.materials.clear()
        reflector.data.materials.append(final)
        for polygon in reflector.data.polygons:
            polygon.material_index = 0
        for mat in PROC:
            bpy.data.materials.remove(mat)
        report["textures"] = {key: os.path.relpath(value, OUT).replace(os.sep, "/") for key, value in paths.items()}
        report["texture_size"] = TEX
    else:
        compact_materials(reflector)

    weighted_normals(reflector)
    bpy.context.view_layer.update()

    lo, hi = world_bounds(parts)
    triangles = {obj.name: tri_count(obj) for obj in parts}
    tilt = radians(TILT)
    report.update({
        "dimensions_m": {"width_x": round(hi.x - lo.x, 4), "depth_y": round(hi.y - lo.y, 4), "height_z": round(hi.z - lo.z, 4)},
        "bounds_min": [round(v, 4) for v in lo],
        "bounds_max": [round(v, 4) for v in hi],
        "triangles": triangles,
        "triangles_total": sum(triangles.values()),
        "object_origins": {obj.name: [round(v, 4) for v in obj.matrix_world.translation] for obj in parts},
        "boresight": [0.0, round(-cos(tilt), 4), round(sin(tilt), 4)],
        "materials": {obj.name: [mat.name for mat in obj.data.materials] for obj in parts},
    })
    log("dimensions", report["dimensions_m"], "triangles", triangles, "total", report["triangles_total"])

    cameras = build_render_set(scene)

    def pose(lowered):
        """Delivered pose, or turned about the trunnion towards the horizon."""
        reflector.rotation_euler = (radians(LOWERED) if lowered else 0.0, 0.0, 0.0)

    if full:
        render(scene, cameras["hero"], NAME + "_hero.png", (1500, 2000), SAMPLES)
        render(scene, cameras["back"], NAME + "_back.png", (1500, 2000), SAMPLES)
        render(scene, cameras["detail"], NAME + "_detail.png", (1800, 1200), SAMPLES)
        pose(True)
        render(scene, cameras["side"], NAME + "_lowered.png", (1500, 2000), SAMPLES)
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
        select_only(parts, active=reflector)
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
        render(scene, cameras["side"], "geometry_lowered.png", (900, 1200), 48)
        pose(False)

    with open(os.path.join(OUT, NAME + "_report.json" if full else "geometry_report.json"), "w", encoding="utf-8", newline=chr(10)) as handle:
        json.dump(report, handle, indent=2)
    log("done")


main()
