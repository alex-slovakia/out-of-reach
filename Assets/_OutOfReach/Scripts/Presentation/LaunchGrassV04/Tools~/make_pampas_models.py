"""LAUNCH-GRASS-V04: the modeled pampas clump (original geometry, generated from scratch).

Run with Blender 3.6 in background mode:
    blender -b -P make_pampas_models.py -- <output folder> [<preview.png> [<texture folder>]]
Writes PampasClumpV04.fbx (objects PampasClump_LOD0 and PampasClump_LOD1) and PampasClumpV04.blend.
With a preview path it renders the two models (Workbench); with the texture folder as well it also renders a
scattered field with the real atlases in Eevee (<preview>_field_*.png), a stand-in for the engine look.

The clump is modeled at unit height (the tallest seed head tip is at z = 1; the shader scales each instance):
  - a fountain of leaf blades: creased, twisting ribbons that climb, arch over and hang (plus a few dry ones);
  - ground cover, so no bare soil shows between plants: mats of litter and short grass under the clump (flat
    quads from the right half of PampasTuftsV04.png), dead leaves lying around it, and tufts of short
    understory grass out to about one clump height from the centre;
  - bare culms (three-sided tubes) of different heights, nodding at the top;
  - seed heads modeled as a volume: two crossed core cards along the rachis and, spiralling around it at the
    golden angle, many small drooping branch cards that each carry one feathery tuft.
Vertex data:
  UVMap  texture coordinates (atlas cells are chosen here, per card)
  Attr   x = part: 0 leaf, .33 culm, .5 branch card or ground mat (PampasTuftsV04.png), .66 core card (PampasPlumesV04.png)
         y = per-part random 0..1 (tone variation; leaves above .85 are dry)
  Bend   x = wind bend weight 0..1
Normals: blades carry their creased surface normals; seed-head cards carry normals that radiate from the
rachis, so a head shades as one soft volume instead of as flat cards.
"""
import math
import random
import sys

import bpy
from mathutils import Matrix, Vector

Z = Vector((0, 0, 1))
PART_LEAF, PART_CULM, PART_TUFT, PART_CORE = 0.0, 0.33, 0.5, 0.66


class Builder:
    def __init__(self):
        self.v, self.n, self.uv, self.attr, self.bend, self.faces = [], [], [], [], [], []

    def vert(self, p, n, uv, part, rnd, bend):
        self.v.append((p.x, p.y, p.z))
        n = n.normalized() if n.length > 1e-9 else Z
        self.n.append((n.x, n.y, n.z))
        self.uv.append(uv); self.attr.append((part, rnd)); self.bend.append((bend, 0.0))
        return len(self.v) - 1

    def quad(self, a, b, c, d):
        self.faces.append((a, b, c)); self.faces.append((a, c, d))


def lerp(a, b, t):
    return a + (b - a) * t


def frames(points, side0):
    """Tangents and parallel-transported side vectors along a polyline."""
    n = len(points)
    tang = []
    for i in range(n):
        d = points[min(i + 1, n - 1)] - points[max(i - 1, 0)]
        tang.append(d.normalized() if d.length > 1e-9 else Z.copy())
    sides = []
    s = side0 - tang[0] * side0.dot(tang[0])
    s = s.normalized() if s.length > 1e-6 else tang[0].orthogonal().normalized()
    for i in range(n):
        s = s - tang[i] * s.dot(tang[i])
        s = s.normalized() if s.length > 1e-6 else tang[i].orthogonal().normalized()
        sides.append(s.copy())
    return tang, sides


def rotate(v, axis, angle):
    return Matrix.Rotation(angle, 3, axis) @ v


def leaf(b, rng, az, r0, pitch0, length, bend_deg, w0, twist, segs, creased, rnd, wind, origin=None, floor=.004):
    pts = []
    p = Vector((math.cos(az) * r0, math.sin(az) * r0, 0))
    if origin is not None:
        p = p + origin
    wander = math.radians(rng.uniform(-22, 22))
    for i in range(segs + 1):
        s = i / segs
        pitch = math.radians(pitch0) - math.radians(bend_deg) * s ** 1.35
        a = az + wander * s
        d = Vector((math.cos(a) * math.cos(pitch), math.sin(a) * math.cos(pitch), math.sin(pitch)))
        pts.append(p.copy())
        p = p + d * (length / segs)
        p.z = max(p.z, floor + .004 * s)                      # hanging tips rest on the ground
    out = Vector((math.cos(az), math.sin(az), 0))
    tang, sides = frames(pts, out.cross(Z))
    roll0 = math.radians(rng.uniform(-25, 25))
    rows = []
    for i in range(segs + 1):
        s = i / segs
        side = rotate(sides[i], tang[i], roll0 + twist * s)
        nrm = side.cross(tang[i]).normalized()
        w = w0 * min(1.0, .35 + s * 5) * max(1 - s ** 2.5, .03)
        c = pts[i]
        if creased:
            k = .42
            rows.append((b.vert(c - side * w, nrm + side * k, (0, s), PART_LEAF, rnd, wind),
                         b.vert(c - nrm * (w * .38), nrm, (.5, s), PART_LEAF, rnd, wind),
                         b.vert(c + side * w, nrm - side * k, (1, s), PART_LEAF, rnd, wind)))
        else:
            rows.append((b.vert(c - side * w, nrm, (0, s), PART_LEAF, rnd, wind),
                         b.vert(c + side * w, nrm, (1, s), PART_LEAF, rnd, wind)))
    for i in range(segs):
        r0_, r1_ = rows[i], rows[i + 1]
        for k in range(len(r0_) - 1):
            b.quad(r0_[k], r0_[k + 1], r1_[k + 1], r1_[k])


def tube(b, pts, radius0, radius1, side0, rnd):
    tang, sides = frames(pts, side0)
    n = len(pts)
    rings = []
    for i in range(n):
        s = i / (n - 1)
        r = lerp(radius0, radius1, s)
        binorm = tang[i].cross(sides[i])
        ring = []
        for j in range(3):
            th = j * math.tau / 3
            radial = sides[i] * math.cos(th) + binorm * math.sin(th)
            ring.append(b.vert(pts[i] + radial * r, radial, (j / 3, s), PART_CULM, rnd, 1.0))
        rings.append(ring)
    for i in range(n - 1):
        for j in range(3):
            k = (j + 1) % 3
            b.quad(rings[i][j], rings[i][k], rings[i + 1][k], rings[i + 1][j])


def head(b, rng, base, direction, nod_dir, length, rnd, branch_cards, core_segs, card_segs):
    """A seed head (panicle) modeled as a volume of cards around a nodding rachis."""
    steps = 24
    pts = []
    p = base.copy()
    d = direction.normalized()
    nod = math.radians(rng.uniform(22, 55))
    for i in range(steps + 1):
        s = i / steps
        dd = (d + nod_dir * math.tan(nod) * s ** 1.7 - Z * .25 * s ** 2.2).normalized()
        pts.append(p.copy())
        p = p + dd * (length / steps)
    tang, sides = frames(pts, nod_dir.cross(Z) if nod_dir.cross(Z).length > .1 else Vector((1, 0, 0)))

    def axis(t):
        f = min(max(t, 0.0), 1.0) * steps
        i = min(int(f), steps - 1)
        u = f - i
        return pts[i].lerp(pts[i + 1], u), tang[i].lerp(tang[i + 1], u).normalized(), sides[i].lerp(sides[i + 1], u).normalized()

    # Core: two crossed cards along the whole head (one cell of the plume atlas, aspect 1:2).
    variant = rng.randrange(4)
    flip = rng.random() < .5
    for q in range(2):
        rows = []
        for i in range(core_segs + 1):
            s = i / core_segs
            c, t, sd = axis(s)
            sd = sd if q == 0 else t.cross(sd).normalized()
            hw = length * .21
            u0, u1 = (1, 0) if flip else (0, 1)
            rows.append((b.vert(c - sd * hw, -sd * .8 + Z * .35 + t * .1, ((variant + u0) / 4, s), PART_CORE, rnd, 1.0),
                         b.vert(c + sd * hw, sd * .8 + Z * .35 + t * .1, ((variant + u1) / 4, s), PART_CORE, rnd, 1.0)))
        for i in range(core_segs):
            b.quad(rows[i][0], rows[i][1], rows[i + 1][1], rows[i + 1][0])

    # Branch cards: golden-angle spiral, longest in the lower middle, steeper towards the tip, drooping outwards.
    for k in range(branch_cards):
        t = lerp(.04, .93, (k + rng.uniform(.2, .8)) / branch_cards)
        c, tg, sd = axis(t)
        phi = k * 2.399963 + rng.uniform(-.35, .35)
        radial = (sd * math.cos(phi) + tg.cross(sd) * math.sin(phi)).normalized()
        env = math.sin(math.pi * t ** .6) ** .8 * (1 - .35 * t)
        blen = env * length * .40 * rng.uniform(.75, 1.1)
        if blen < length * .03:
            continue
        open_angle = math.radians(lerp(56, 24, t)) * rng.uniform(.85, 1.15)
        d0 = (tg * math.cos(open_angle) + radial * math.sin(open_angle)).normalized()
        droop = rng.uniform(.5, 1.05)
        bank = math.radians(rng.uniform(-70, 70))
        tuft = rng.randrange(4)
        mirror = rng.random() < .5
        tone = min(1.0, max(0.0, rnd + rng.uniform(-.12, .12)))
        rows = []
        q = c.copy()
        for i in range(card_segs + 1):
            s = i / card_segs
            dd = (d0 + (radial * .35 - Z * droop) * s ** 1.4).normalized()
            w = dd.cross(radial)
            w = w.normalized() if w.length > 1e-4 else dd.orthogonal().normalized()
            w = rotate(w, dd, bank)
            hw = blen * .40 * (.3 + 1.4 * s - .85 * s * s)          # pointed base, widest past the middle
            nrm = radial * .75 + tg * .15 + Z * .3
            u0, u1 = (1, 0) if mirror else (0, 1)
            rows.append((b.vert(q - w * hw, nrm, ((tuft + u0) / 8, s), PART_TUFT, tone, 1.0),
                         b.vert(q + w * hw, nrm, ((tuft + u1) / 8, s), PART_TUFT, tone, 1.0)))
            q = q + dd * (blen / card_segs)
        for i in range(card_segs):
            b.quad(rows[i][0], rows[i][1], rows[i + 1][1], rows[i + 1][0])
    return pts


def ground_mat(b, rng, radius, z, cell, rnd):
    """A flat quad of litter and short grass (one of the two mat cells in the right half of the tuft atlas)."""
    yaw = rng.uniform(0, math.tau)
    c, s = math.cos(yaw), math.sin(yaw)
    corners = []
    for cx, cy in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
        p = Vector(((cx * c - cy * s) * radius, (cx * s + cy * c) * radius, z))
        corners.append(b.vert(p, Z, (.5 + (cell + cx * .5 + .5) / 4, cy * .5 + .5), PART_TUFT, rnd, 0.0))
    b.quad(*corners)


def clump(seed, lod):
    rng = random.Random(seed)
    b = Builder()
    detailed = lod == 0
    # ---- leaves ----
    count = 56 if detailed else 22
    for i in range(count):
        az = (i + rng.uniform(0, 1)) / count * math.tau
        inner = rng.random()                                   # 0 = outer, long and hanging; 1 = inner, upright
        length = lerp(.95, .48, inner) * rng.uniform(.85, 1.1)
        pitch0 = lerp(58, 84, inner) + rng.uniform(-6, 6)
        bend = lerp(165, 85, inner) + rng.uniform(-18, 18)
        w0 = lerp(.0125, .009, inner) * (1.0 if detailed else 1.75)
        twist = math.radians(rng.uniform(-110, 110))
        rnd = rng.uniform(0, .84)
        leaf(b, rng, az, rng.uniform(.015, .085), pitch0, length, bend, w0, twist,
             (8 if inner < .5 else 5) if detailed else 3, detailed, rnd, lerp(.75, .5, inner))
    for i in range(8 if detailed else 3):                      # dry, low, bleached leaves
        az = rng.uniform(0, math.tau)
        leaf(b, rng, az, rng.uniform(.03, .1), rng.uniform(25, 50), rng.uniform(.35, .6), rng.uniform(70, 130),
             .011 * (1.0 if detailed else 1.75), math.radians(rng.uniform(-160, 160)),
             5 if detailed else 3, detailed, rng.uniform(.86, 1.0), .6)
    # ---- ground cover: mats, litter (dead leaves lying flat) and short understory grass around the clump ----
    ground_mat(b, rng, .66, .046, 0, rng.uniform(.25, .6))
    if detailed:
        ground_mat(b, rng, .50, .060, 1, rng.uniform(.4, .8))
    for i in range(36 if detailed else 14):
        az = rng.uniform(0, math.tau)
        heading = az + rng.uniform(-.9, .9)
        r0 = rng.uniform(.04, .5)
        # The field sinks every clump 4 cm into the ground, so the ground cover is modeled 5-8 % of the
        # clump height up: above the soil for every clump height, a few centimetres at most.
        start = Vector((math.cos(az) * r0, math.sin(az) * r0, rng.uniform(.05, .075)))
        leaf(b, rng, heading, 0.0, rng.uniform(1, 8), rng.uniform(.3, .62), rng.uniform(5, 30),
             rng.uniform(.017, .027) * (1.0 if detailed else 1.5), math.radians(rng.uniform(-40, 40)),
             3 if detailed else 2, False, rng.uniform(.3, 1.0), .12, origin=start, floor=.048)
    for i in range(30 if detailed else 10):
        az = rng.uniform(0, math.tau)
        r = .20 + .62 * rng.random() ** .8
        centre = Vector((math.cos(az) * r, math.sin(az) * r, -.012))
        for k in range(6 if detailed else 3):
            leaf(b, rng, rng.uniform(0, math.tau), rng.uniform(.0, .025), rng.uniform(52, 86), rng.uniform(.14, .34),
                 rng.uniform(25, 95), rng.uniform(.007, .0105) * (1.0 if detailed else 1.9), math.radians(rng.uniform(-60, 60)),
                 2 if detailed else 1, False, rng.uniform(0, .95), .8, origin=centre)
    # ---- culms and seed heads ----
    stems = 9 if detailed else 7
    tips = []
    for i in range(stems):
        az = (i + rng.uniform(.15, .85)) / stems * math.tau
        out = Vector((math.cos(az), math.sin(az), 0))
        base = out * rng.uniform(.01, .06)
        height = 1.0 if i == 0 else rng.uniform(.60, .97)
        head_len = rng.uniform(.30, .40) * (height ** .5)
        lean = math.radians(rng.uniform(5, 26))
        segs = 5 if detailed else 2
        pts = []
        p = base.copy()
        stalk = height - head_len * .92
        for k in range(segs + 1):
            s = k / segs
            a = lean * (.6 + .8 * s)
            d = (Z * math.cos(a) + out * math.sin(a)).normalized()
            pts.append(p.copy())
            p = p + d * (stalk / segs)
        rnd = rng.random()
        tube(b, pts, .0048, .0032, out.cross(Z), rnd)
        direction = (pts[-1] - pts[-2]).normalized()
        nod_dir = (out * rng.uniform(.4, 1) + Vector((rng.uniform(-.5, .5), rng.uniform(-.5, .5), 0))).normalized()
        axis_pts = head(b, rng, pts[-1], direction, nod_dir, head_len, rnd,
                        26 if detailed else 7, 3 if detailed else 2, 2 if detailed else 1)
        tips.append(max(q.z for q in axis_pts))
        # the rachis continues inside the lower part of the head
        if detailed:
            tube(b, axis_pts[:13:4], .003, .0015, out.cross(Z), rnd)
    # normalise: tallest point of the clump at z = 1
    top = max(v[2] for v in b.v)
    scale = 1.0 / top
    b.v = [(x * scale, y * scale, z * scale) for (x, y, z) in b.v]
    return b


def to_object(b, name):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(b.v, [], b.faces)
    mesh.update()
    for poly in mesh.polygons:
        poly.use_smooth = True
    for layer, data in (("UVMap", b.uv), ("Attr", b.attr), ("Bend", b.bend)):
        uv = mesh.uv_layers.new(name=layer)
        flat = []
        for loop in mesh.loops:
            flat.extend(data[loop.vertex_index])
        uv.data.foreach_set("uv", flat)
    mesh.use_auto_smooth = True
    mesh.normals_split_custom_set_from_vertices(b.n)
    # preview colours only (not exported)
    col = mesh.color_attributes.new(name="Preview", type='BYTE_COLOR', domain='POINT')
    palette = {PART_LEAF: (.42, .40, .18, 1), PART_CULM: (.70, .58, .34, 1), PART_TUFT: (.95, .86, .66, 1), PART_CORE: (.84, .72, .50, 1)}
    for i, (part, rnd) in enumerate(b.attr):
        c = palette[part]
        k = .8 + .3 * rnd
        col.data[i].color = (c[0] * k, c[1] * k, c[2] * k, 1)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def preview(path, objects):
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'VERTEX'
    scene.display.shading.show_backface_culling = False
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.world = bpy.data.worlds.new("Preview")
    scene.world.color = (.035, .04, .055)
    objects[1].location = (1.25, 0, 0)
    cam_data = bpy.data.cameras.new("Preview")
    cam_data.lens = 40
    cam = bpy.data.objects.new("Preview", cam_data)
    scene.collection.objects.link(cam)
    cam.location = (.62, -3.1, .72)
    look = Vector((.62, 0, .5)) - Vector(cam.location)
    cam.rotation_euler = look.to_track_quat('-Z', 'Y').to_euler()
    scene.camera = cam
    scene.render.resolution_x, scene.render.resolution_y = 1500, 900
    scene.render.filepath = path
    scene.render.image_settings.file_format = 'PNG'
    bpy.ops.render.render(write_still=True)
    objects[1].location = (0, 0, 0)
    bpy.data.objects.remove(cam)


def field_preview(prefix, lod0, lod1, tex_dir):
    """A scattered field with the real atlases in Eevee: checks ground cover, density and the look without Unity."""
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.eevee.use_gtao = True
    scene.eevee.use_soft_shadows = True
    scene.eevee.taa_render_samples = 24
    scene.eevee.shadow_cascade_size = '2048'
    scene.render.resolution_x, scene.render.resolution_y = 1600, 900
    mat = bpy.data.materials.new("PampasPreview")
    mat.use_nodes = True
    mat.blend_method = 'CLIP'; mat.alpha_threshold = .4; mat.shadow_method = 'CLIP'; mat.use_backface_culling = False
    nt = mat.node_tree; nodes = nt.nodes; links = nt.links
    nodes.clear()
    out = nodes.new('ShaderNodeOutputMaterial')
    bsdf = nodes.new('ShaderNodeBsdfPrincipled')
    bsdf.inputs['Roughness'].default_value = .9
    bsdf.inputs['Specular'].default_value = .05
    trans = nodes.new('ShaderNodeBsdfTranslucent')
    mixs = nodes.new('ShaderNodeMixShader')
    links.new(bsdf.outputs['BSDF'], mixs.inputs[1]); links.new(trans.outputs['BSDF'], mixs.inputs[2])
    clear = nodes.new('ShaderNodeBsdfTransparent')
    cut = nodes.new('ShaderNodeMixShader')                      # coverage: transparent where the atlas is empty
    links.new(clear.outputs['BSDF'], cut.inputs[1]); links.new(mixs.outputs['Shader'], cut.inputs[2])
    links.new(cut.outputs['Shader'], out.inputs['Surface'])
    uv0 = nodes.new('ShaderNodeUVMap'); uv0.uv_map = "UVMap"
    attr = nodes.new('ShaderNodeUVMap'); attr.uv_map = "Attr"
    sep = nodes.new('ShaderNodeSeparateXYZ'); links.new(attr.outputs['UV'], sep.inputs[0])
    sepuv = nodes.new('ShaderNodeSeparateXYZ'); links.new(uv0.outputs['UV'], sepuv.inputs[0])

    def image(name):
        n = nodes.new('ShaderNodeTexImage')
        n.image = bpy.data.images.load(tex_dir + "/" + name)
        n.extension = 'CLIP'
        links.new(uv0.outputs['UV'], n.inputs['Vector'])
        return n

    def math_node(op, a, b=None, value=None):
        n = nodes.new('ShaderNodeMath'); n.operation = op
        links.new(a, n.inputs[0])
        if b is not None: links.new(b, n.inputs[1])
        if value is not None: n.inputs[1].default_value = value
        return n.outputs[0]

    def mix(fac, a, b):
        n = nodes.new('ShaderNodeMixRGB')
        links.new(fac, n.inputs['Fac'])
        if isinstance(a, tuple): n.inputs['Color1'].default_value = a
        else: links.new(a, n.inputs['Color1'])
        if isinstance(b, tuple): n.inputs['Color2'].default_value = b
        else: links.new(b, n.inputs['Color2'])
        return n.outputs['Color']

    tuft, plume = image("PampasTuftsV04.png"), image("PampasPlumesV04.png")
    is_head = math_node('GREATER_THAN', sep.outputs['X'], value=.42)
    is_core = math_node('GREATER_THAN', sep.outputs['X'], value=.58)
    is_dry = math_node('GREATER_THAN', sep.outputs['Y'], value=.85)
    head_col = mix(is_core, tuft.outputs['Color'], plume.outputs['Color'])
    head_alpha = mix(is_core, tuft.outputs['Alpha'], plume.outputs['Alpha'])
    ramp = nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].color = (.022, .020, .005, 1)
    ramp.color_ramp.elements[1].color = (.12, .08, .025, 1)
    links.new(sepuv.outputs['Y'], ramp.inputs['Fac'])
    leaf_col = mix(is_dry, ramp.outputs['Color'], (.27, .19, .065, 1))
    # self-shadowed bases: darker near the ground (object space z is the clump height fraction)
    tc = nodes.new('ShaderNodeTexCoord')
    sepo = nodes.new('ShaderNodeSeparateXYZ'); links.new(tc.outputs['Object'], sepo.inputs[0])
    ao = nodes.new('ShaderNodeMapRange'); ao.inputs['From Min'].default_value = 0; ao.inputs['From Max'].default_value = .35
    ao.inputs['To Min'].default_value = .42; ao.inputs['To Max'].default_value = 1
    links.new(sepo.outputs['Z'], ao.inputs['Value'])
    shaded = nodes.new('ShaderNodeMixRGB'); shaded.blend_type = 'MULTIPLY'; shaded.inputs['Fac'].default_value = 1
    links.new(leaf_col, shaded.inputs['Color1']); links.new(ao.outputs['Result'], shaded.inputs['Color2'])
    head_scaled = nodes.new('ShaderNodeMixRGB'); head_scaled.blend_type = 'MULTIPLY'; head_scaled.inputs['Fac'].default_value = 1
    links.new(head_col, head_scaled.inputs['Color1']); head_scaled.inputs['Color2'].default_value = (.3, .29, .27, 1)
    colour = mix(is_head, shaded.outputs['Color'], head_scaled.outputs['Color'])
    alpha = mix(is_head, (1, 1, 1, 1), head_alpha)
    links.new(colour, bsdf.inputs['Base Color']); links.new(colour, trans.inputs['Color'])
    links.new(alpha, cut.inputs['Fac'])
    half = math_node('MULTIPLY', is_head, value=.45)
    links.new(half, mixs.inputs['Fac'])
    for mesh in (lod0.data, lod1.data):
        mesh.materials.clear(); mesh.materials.append(mat)

    ground_mat = bpy.data.materials.new("Ground"); ground_mat.use_nodes = True
    g = ground_mat.node_tree.nodes['Principled BSDF']
    g.inputs['Base Color'].default_value = (.36, .28, .14, 1); g.inputs['Roughness'].default_value = 1; g.inputs['Specular'].default_value = 0
    bpy.ops.mesh.primitive_plane_add(size=600, location=(0, 0, 0))
    bpy.context.active_object.data.materials.append(ground_mat)

    rng = random.Random(7)
    lod0.hide_render = lod1.hide_render = True
    spacing = 1.15
    n = int(40 / spacing)
    count = 0
    for j in range(n):
        for i in range(-n, n + 1):
            x = (i + rng.random()) * spacing; y = (j + rng.random()) * spacing + .9
            r = math.hypot(x, y)
            if r > 38 or abs(math.atan2(x, y)) > math.radians(50) or rng.random() < .1:
                continue
            src = lod0 if r < 11 else lod1
            o = bpy.data.objects.new("c", src.data)
            height = lerp(1.05, 2.35, min(1.0, max(0.0, rng.random() * .4 + rng.random() * .75 - .1)))
            if rng.random() > .88: height *= 1.22
            o.location = (x, y, -.04)
            o.rotation_euler = (rng.uniform(-.1, .1), rng.uniform(-.1, .1), rng.uniform(0, math.tau))
            w = height * rng.uniform(.9, 1.15)
            o.scale = (w, w, height)
            scene.collection.objects.link(o)
            count += 1
    print("PAMPAS_FIELD_PREVIEW clumps=%d" % count)

    scene.world = bpy.data.worlds.new("Sky"); scene.world.use_nodes = True
    bg = scene.world.node_tree.nodes['Background']
    bg.inputs['Color'].default_value = (.36, .50, .78, 1); bg.inputs['Strength'].default_value = 1.1
    sun_data = bpy.data.lights.new("Sun", 'SUN'); sun_data.energy = 5.0; sun_data.color = (1, .87, .69); sun_data.angle = math.radians(3)
    sun = bpy.data.objects.new("Sun", sun_data); scene.collection.objects.link(sun)
    cam_data = bpy.data.cameras.new("Field"); cam_data.lens = 20; cam_data.clip_end = 2000
    cam = bpy.data.objects.new("Field", cam_data); scene.collection.objects.link(cam); scene.camera = cam

    def aim(obj, origin, target):
        obj.location = origin
        obj.rotation_euler = (Vector(target) - Vector(origin)).to_track_quat('-Z', 'Y').to_euler()

    def sun_from(azimuth_deg):                                   # azimuth measured from +Y towards +X; elevation 18 degrees
        az, el = math.radians(azimuth_deg), math.radians(18)
        d = Vector((math.sin(az) * math.cos(el), math.cos(az) * math.cos(el), math.sin(el)))   # towards the sun
        sun.rotation_euler = d.to_track_quat('Z', 'Y').to_euler()

    shots = (("backlit", 12, (0, 0, 1.55), (0, 14, 1.9)), ("crosslit", 100, (0, 0, 1.55), (0, 12, 1.0)),
             ("lookdown", 100, (0, 0, 1.6), (0, 3.6, 0.1)), ("frontlit", 190, (0, 0, 1.55), (0, 12, 1.0)))
    for name, azimuth, origin, target in shots:
        sun_from(azimuth)
        aim(cam, origin, target)
        scene.render.filepath = prefix + "_field_" + name + ".png"
        bpy.ops.render.render(write_still=True)


def main():
    args = sys.argv[sys.argv.index("--") + 1:]
    out = args[0]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    objects = []
    for lod in (0, 1):
        b = clump(1977, lod)
        obj = to_object(b, "PampasClump_LOD%d" % lod)
        objects.append(obj)
        zs = [v[2] for v in b.v]; rs = [math.hypot(v[0], v[1]) for v in b.v]
        print("PAMPAS_MODEL LOD%d vertices=%d triangles=%d height=%.3f radius=%.3f" % (lod, len(b.v), len(b.faces), max(zs), max(rs)))
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(filepath=out + "/PampasClumpV04.fbx", use_selection=True, object_types={'MESH'},
                             apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y', bake_space_transform=True,
                             mesh_smooth_type='OFF', use_mesh_modifiers=False, add_leaf_bones=False, bake_anim=False,
                             colors_type='NONE', use_custom_props=False)
    if len(args) > 1:
        preview(args[1], objects)
    bpy.ops.wm.save_as_mainfile(filepath=out + "/PampasClumpV04.blend", check_existing=False)
    if len(args) > 2:
        field_preview(args[1].rsplit('.', 1)[0], objects[0], objects[1], args[2])


main()
