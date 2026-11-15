"""
a7kit - a small triangle-mesh modelling kit for the AURORA-7 models (original work).

Pure numpy, so it runs in Blender's Python and in plain Python (python a7kit.py runs the self-test).

Conventions
- Coordinates are the Unity ones: x right, y up, z forward, metres. Floor is y = 0.
- A triangle (a, b, c) faces outward when cross(b - a, c - a) points outward, evaluated with the
  ordinary cross product on those coordinate values ("kit-outward"). The Blender exporter swaps
  y and z (a mirror) and reverses the winding once, so faces stay outward there and in Unity.
- Assemblies are designed in a front view: local x to the viewer's right, y up, z towards the viewer.
  front_frame() places such an assembly in the world; it is a mirrored frame (determinant -1), which
  is exactly what makes local +x the viewer's right in Unity's left-handed space. Model.add() flips
  the winding whenever a transform mirrors, so nothing else has to care.
- Tiling materials get box-projected UVs in metres at export time. Decals and screens carry
  explicit atlas UVs.
"""
import math

import numpy as np

UP = np.array([0.0, 1.0, 0.0])


def vec(*values):
    if len(values) == 1:
        return np.asarray(values[0], dtype=np.float64)
    return np.asarray(values, dtype=np.float64)


def unit(v):
    v = vec(v)
    length = np.linalg.norm(v)
    return v / length if length > 1e-12 else v


def translation(offset):
    m = np.eye(4)
    m[:3, 3] = vec(offset)
    return m


def scaling(sx, sy=None, sz=None):
    m = np.eye(4)
    m[0, 0] = sx
    m[1, 1] = sx if sy is None else sy
    m[2, 2] = sx if sz is None else sz
    return m


def rotation(axis, degrees):
    a = unit(axis)
    c = math.cos(math.radians(degrees))
    s = math.sin(math.radians(degrees))
    x, y, z = a
    r = np.array([[c + x * x * (1 - c), x * y * (1 - c) - z * s, x * z * (1 - c) + y * s],
                  [y * x * (1 - c) + z * s, c + y * y * (1 - c), y * z * (1 - c) - x * s],
                  [z * x * (1 - c) - y * s, z * y * (1 - c) + x * s, c + z * z * (1 - c)]])
    m = np.eye(4)
    m[:3, :3] = r
    return m


def basis(origin, x_axis, y_axis, z_axis):
    m = np.eye(4)
    m[:3, 0] = vec(x_axis)
    m[:3, 1] = vec(y_axis)
    m[:3, 2] = vec(z_axis)
    m[:3, 3] = vec(origin)
    return m


def front_frame(origin, out, up=UP):
    """Places a front-view assembly: local z = out (towards the viewer), y = up, x = viewer's right."""
    out = unit(out)
    up = unit(up)
    right = unit(np.cross(out, up))
    up = unit(np.cross(right, out))
    return basis(origin, right, up, out)


def compose(*matrices):
    result = np.eye(4)
    for m in matrices:
        result = result @ m
    return result


def at(x, y, z):
    return translation((x, y, z))


# --------------------------------------------------------------------------- primitives -> (V, T)
def _quads_to_tris(faces):
    tris = []
    for face in faces:
        for i in range(1, len(face) - 1):
            tris.append((face[0], face[i], face[i + 1]))
    return np.asarray(tris, dtype=np.int64)


def _outward_convex(v, t, centre=None):
    """Flips triangles of a convex shape so that all face away from its centre."""
    c = v.mean(axis=0) if centre is None else centre
    a, b, d = v[t[:, 0]], v[t[:, 1]], v[t[:, 2]]
    n = np.cross(b - a, d - a)
    flip = np.einsum("ij,ij->i", n, (a + b + d) / 3.0 - c) < 0
    t = t.copy()
    t[flip] = t[flip][:, ::-1]
    return t


def box(size, chamfer=0.0):
    """Box centred on the origin with all twelve edges chamfered (single segment)."""
    hx, hy, hz = (float(s) / 2.0 for s in size)
    c = min(float(chamfer), 0.45 * min(hx, hy, hz))
    if c <= 1e-6:
        v = np.array([[sx * hx, sy * hy, sz * hz] for sz in (-1, 1) for sy in (-1, 1) for sx in (-1, 1)])
        faces = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
        return v, _outward_convex(v, _quads_to_tris(faces), np.zeros(3))
    verts = []
    index = {}
    for sz in (-1, 1):
        for sy in (-1, 1):
            for sx in (-1, 1):
                index[(sx, sy, sz, 0)] = len(verts)
                verts.append((sx * hx, sy * (hy - c), sz * (hz - c)))
                index[(sx, sy, sz, 1)] = len(verts)
                verts.append((sx * (hx - c), sy * hy, sz * (hz - c)))
                index[(sx, sy, sz, 2)] = len(verts)
                verts.append((sx * (hx - c), sy * (hy - c), sz * hz))
    faces = []
    loop = [(-1, -1), (1, -1), (1, 1), (-1, 1)]
    for s in (-1, 1):
        faces.append([index[(s, a, b, 0)] for a, b in loop])
        faces.append([index[(a, s, b, 1)] for a, b in loop])
        faces.append([index[(a, b, s, 2)] for a, b in loop])
    for sa in (-1, 1):
        for sb in (-1, 1):
            faces.append([index[(sa, sb, -1, 0)], index[(sa, sb, 1, 0)], index[(sa, sb, 1, 1)], index[(sa, sb, -1, 1)]])
            faces.append([index[(sa, -1, sb, 0)], index[(sa, 1, sb, 0)], index[(sa, 1, sb, 2)], index[(sa, -1, sb, 2)]])
            faces.append([index[(-1, sa, sb, 1)], index[(1, sa, sb, 1)], index[(1, sa, sb, 2)], index[(-1, sa, sb, 2)]])
    for sz in (-1, 1):
        for sy in (-1, 1):
            for sx in (-1, 1):
                faces.append([index[(sx, sy, sz, 0)], index[(sx, sy, sz, 1)], index[(sx, sy, sz, 2)]])
    v = np.asarray(verts, dtype=np.float64)
    return v, _outward_convex(v, _quads_to_tris(faces), np.zeros(3))


def lathe(profile, segs=16, cap_bottom=True, cap_top=True, arc=360.0):
    """Surface of revolution about +y. profile = [(radius, y), ...] from bottom to top."""
    profile = [(float(r), float(y)) for r, y in profile]
    full = abs(arc - 360.0) < 1e-6
    count = segs if full else segs + 1
    angles = np.radians(np.linspace(0.0, arc, segs + 1))[:count]
    rings = []
    for r, y in profile:
        rings.append(np.stack([r * np.cos(angles), np.full(count, y), r * np.sin(angles)], axis=1))
    v = np.concatenate(rings, axis=0)
    tris = []
    for i in range(len(profile) - 1):
        for k in range(segs):
            k1 = (k + 1) % count if full else k + 1
            a, b = i * count + k, i * count + k1
            c, d = (i + 1) * count + k1, (i + 1) * count + k
            tris.append((a, c, b))
            tris.append((a, d, c))
    extra = []
    if cap_bottom and profile[0][0] > 1e-9:
        centre = len(v) + len(extra)
        extra.append((0.0, profile[0][1], 0.0))
        for k in range(segs):
            k1 = (k + 1) % count if full else k + 1
            tris.append((centre, k, k1))
    if cap_top and profile[-1][0] > 1e-9:
        centre = len(v) + len(extra)
        extra.append((0.0, profile[-1][1], 0.0))
        base = (len(profile) - 1) * count
        for k in range(segs):
            k1 = (k + 1) % count if full else k + 1
            tris.append((centre, base + k1, base + k))
    if extra:
        v = np.concatenate([v, np.asarray(extra)], axis=0)
    return v, np.asarray(tris, dtype=np.int64)


def cylinder(radius, length, segs=16, chamfer=0.0, caps=(True, True)):
    """Cylinder along +y from 0 to length, optionally with chamfered rims."""
    c = min(chamfer, radius * 0.45, length * 0.45)
    if c > 1e-6:
        profile = [(radius - c, 0.0), (radius, c), (radius, length - c), (radius - c, length)]
    else:
        profile = [(radius, 0.0), (radius, length)]
    return lathe(profile, segs, caps[0], caps[1])


def sphere(radius, segs=16, rings=10):
    profile = []
    for i in range(rings + 1):
        a = -math.pi / 2 + math.pi * i / rings
        profile.append((max(0.0, radius * math.cos(a)), radius * math.sin(a)))
    profile[0] = (1e-6, -radius)
    profile[-1] = (1e-6, radius)
    return lathe(profile, segs, False, False)


def capsule_tank(radius, length, segs=20, dome=6):
    """Pressure vessel along +y: cylinder with elliptical domes; total height = length."""
    k = 0.62
    dome_h = radius * k
    profile = []
    for i in range(dome + 1):
        a = -math.pi / 2 + (math.pi / 2) * i / dome
        profile.append((max(1e-6, radius * math.cos(a)), dome_h + dome_h * math.sin(a)))
    for i in range(dome + 1):
        a = (math.pi / 2) * i / dome
        profile.append((max(1e-6, radius * math.cos(a)), length - dome_h + dome_h * math.sin(a)))
    return lathe(profile, segs, False, False)


def fillet_path(points, radius, steps=5):
    """Rounds the interior corners of a polyline with quadratic Bezier arcs."""
    points = [vec(p) for p in points]
    if len(points) < 3 or radius <= 0:
        return np.asarray(points)
    result = [points[0]]
    for i in range(1, len(points) - 1):
        prev_point, corner, next_point = points[i - 1], points[i], points[i + 1]
        d1 = np.linalg.norm(corner - prev_point)
        d2 = np.linalg.norm(next_point - corner)
        d = min(radius, d1 * 0.5, d2 * 0.5)
        if d < 1e-6:
            result.append(corner)
            continue
        start = corner + (prev_point - corner) / d1 * d
        end = corner + (next_point - corner) / d2 * d
        for s in range(steps + 1):
            t = s / steps
            result.append((1 - t) ** 2 * start + 2 * t * (1 - t) * corner + t * t * end)
    result.append(points[-1])
    return np.asarray(result)


def tube(path, radius, segs=8, caps=True, closed=False):
    """Round tube swept along a polyline (parallel-transport frames)."""
    path = np.asarray(path, dtype=np.float64)
    n = len(path)
    tangents = np.zeros_like(path)
    for i in range(n):
        if closed:
            tangents[i] = path[(i + 1) % n] - path[i - 1]
        elif i == 0:
            tangents[i] = path[1] - path[0]
        elif i == n - 1:
            tangents[i] = path[-1] - path[-2]
        else:
            tangents[i] = unit(path[i + 1] - path[i]) + unit(path[i] - path[i - 1])
        tangents[i] = unit(tangents[i])
    helper = UP if abs(tangents[0][1]) < 0.9 else np.array([1.0, 0.0, 0.0])
    normal = unit(np.cross(tangents[0], np.cross(helper, tangents[0])))
    angles = np.linspace(0.0, math.tau, segs, endpoint=False)
    rings = []
    for i in range(n):
        t = tangents[i]
        normal = unit(normal - t * np.dot(normal, t))
        binormal = np.cross(t, normal)
        rings.append(path[i] + radius * (np.outer(np.cos(angles), normal) + np.outer(np.sin(angles), binormal)))
    v = np.concatenate(rings, axis=0)
    tris = []
    spans = n if closed else n - 1
    for i in range(spans):
        j = (i + 1) % n
        for k in range(segs):
            k1 = (k + 1) % segs
            a, b, c, d = i * segs + k, i * segs + k1, j * segs + k1, j * segs + k
            tris.append((a, b, c))
            tris.append((a, c, d))
    t = np.asarray(tris, dtype=np.int64)
    a, b, c = v[t[0, 0]], v[t[0, 1]], v[t[0, 2]]
    if np.dot(np.cross(b - a, c - a), (a + b + c) / 3.0 - (path[0] + path[1 % n]) / 2.0) < 0:
        t = t[:, ::-1]
    if caps and not closed:
        extra = [path[0], path[-1]]
        c0, c1 = len(v), len(v) + 1
        v = np.concatenate([v, np.asarray(extra)], axis=0)
        cap = []
        for k in range(segs):
            k1 = (k + 1) % segs
            cap.append((c0, k, k1))
            cap.append((c1, (n - 1) * segs + k1, (n - 1) * segs + k))
        cap = np.asarray(cap, dtype=np.int64)
        a, b, c = v[cap[0, 0]], v[cap[0, 1]], v[cap[0, 2]]
        if np.dot(np.cross(b - a, c - a), -tangents[0]) < 0:
            cap = cap[:, ::-1]
        t = np.concatenate([t, cap], axis=0)
    return v, t


def rounded_rect(width, height, radius, steps=4):
    """Counter-clockwise outline (in the x/y plane) of a rounded rectangle centred on the origin."""
    r = max(0.0, min(radius, width / 2 - 1e-6, height / 2 - 1e-6))
    points = []
    corners = [(width / 2 - r, height / 2 - r, 0), (-width / 2 + r, height / 2 - r, 90),
               (-width / 2 + r, -height / 2 + r, 180), (width / 2 - r, -height / 2 + r, 270)]
    for cx, cy, start in corners:
        for s in range(steps + 1):
            a = math.radians(start + 90.0 * s / steps)
            points.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return np.asarray(points)


def plate(outline, depth, chamfer=0.0, back=False):
    """Extrudes a convex x/y outline from z = 0 to z = depth; the front (z = depth) faces +z."""
    outline = np.asarray(outline, dtype=np.float64)
    n = len(outline)
    centre = outline.mean(axis=0)
    c = min(chamfer, depth * 0.9)
    layers = [np.column_stack([outline, np.zeros(n)])]
    if c > 1e-6:
        layers.append(np.column_stack([outline, np.full(n, depth - c)]))
        shrunk = centre + (outline - centre) * (1.0 - c / max(1e-6, np.abs(outline - centre).max()))
        layers.append(np.column_stack([shrunk, np.full(n, depth)]))
    else:
        layers.append(np.column_stack([outline, np.full(n, depth)]))
    v = np.concatenate(layers, axis=0)
    tris = []
    for layer in range(len(layers) - 1):
        for k in range(n):
            k1 = (k + 1) % n
            a, b = layer * n + k, layer * n + k1
            d, e = (layer + 1) * n + k1, (layer + 1) * n + k
            tris.append((a, b, d))
            tris.append((a, d, e))
    top = (len(layers) - 1) * n
    front_centre = len(v)
    extra = [(centre[0], centre[1], depth)]
    for k in range(n):
        tris.append((front_centre, top + k, top + (k + 1) % n))
    if back:
        back_centre = len(v) + 1
        extra.append((centre[0], centre[1], 0.0))
        for k in range(n):
            tris.append((back_centre, (k + 1) % n, k))
    v = np.concatenate([v, np.asarray(extra)], axis=0)
    return v, np.asarray(tris, dtype=np.int64)


def ring(outer, inner, depth, back=False):
    """Frame between two outlines with equal point counts, from z = 0 to z = depth, front faces +z."""
    outer = np.asarray(outer, dtype=np.float64)
    inner = np.asarray(inner, dtype=np.float64)
    n = len(outer)
    v = np.concatenate([np.column_stack([outer, np.zeros(n)]), np.column_stack([outer, np.full(n, depth)]),
                        np.column_stack([inner, np.full(n, depth)]), np.column_stack([inner, np.zeros(n)])], axis=0)
    tris = []
    for k in range(n):
        k1 = (k + 1) % n
        ob, ob1, of, of1 = k, k1, n + k, n + k1
        inf, inf1, inb, inb1 = 2 * n + k, 2 * n + k1, 3 * n + k, 3 * n + k1
        tris += [(ob, ob1, of1), (ob, of1, of)]          # outer wall
        tris += [(of, of1, inf1), (of, inf1, inf)]       # front face
        tris += [(inf, inf1, inb1), (inf, inb1, inb)]    # inner wall
        if back:
            tris += [(inb, inb1, ob1), (inb, ob1, ob)]
    return v, np.asarray(tris, dtype=np.int64)


def quad(width, height):
    """Single quad in the x/y plane facing +z, centred on the origin."""
    v = np.array([[-width / 2, -height / 2, 0.0], [width / 2, -height / 2, 0.0],
                  [width / 2, height / 2, 0.0], [-width / 2, height / 2, 0.0]])
    return v, np.array([[0, 1, 2], [0, 2, 3]], dtype=np.int64)


def signed_volume(v, t):
    a, b, c = v[t[:, 0]], v[t[:, 1]], v[t[:, 2]]
    return float(np.einsum("ij,ij->i", a, np.cross(b, c)).sum() / 6.0)


# --------------------------------------------------------------------------- model container
class Model:
    """Triangles grouped by material name. Sub-assemblies are Models too and can be instanced."""

    def __init__(self, name="model"):
        self.name = name
        self.parts = {}

    def add(self, material, v, t, m=None, uv=None):
        v = np.asarray(v, dtype=np.float64)
        t = np.asarray(t, dtype=np.int64)
        if m is not None:
            v = v @ m[:3, :3].T + m[:3, 3]
            if np.linalg.det(m[:3, :3]) < 0:
                t = t[:, ::-1]
                if uv is not None:
                    uv = uv[:, ::-1, :]
        self.parts.setdefault(material, []).append((v, t, uv))
        return self

    def instance(self, other, m=None):
        for material, chunks in other.parts.items():
            for v, t, uv in chunks:
                self.add(material, v, t, m, uv)
        return self

    # ---- primitive helpers; `m` places the primitive, `centre` offsets it inside that frame
    def box(self, material, centre, size, chamfer=0.004, m=None):
        v, t = box(size, chamfer)
        return self.add(material, v + vec(centre), t, m)

    def cyl(self, material, start, end, radius, segs=14, chamfer=0.0, m=None, caps=(True, True)):
        start, end = vec(start), vec(end)
        axis = end - start
        length = float(np.linalg.norm(axis))
        v, t = cylinder(radius, length, segs, chamfer, caps)
        return self.add(material, v, t, compose(m if m is not None else np.eye(4), _align_y(start, axis)))

    def lathe(self, material, origin, axis, profile, segs=16, m=None, caps=(True, True)):
        v, t = lathe(profile, segs, caps[0], caps[1])
        return self.add(material, v, t, compose(m if m is not None else np.eye(4), _align_y(vec(origin), vec(axis))))

    def tube(self, material, points, radius, segs=8, fillet=0.0, m=None, closed=False, caps=True):
        path = fillet_path(points, fillet) if fillet > 0 else np.asarray([vec(p) for p in points])
        v, t = tube(path, radius, segs, caps, closed)
        return self.add(material, v, t, m)

    def plate(self, material, centre, width, height, depth, radius=0.01, chamfer=0.003, m=None, steps=3):
        v, t = plate(rounded_rect(width, height, radius, steps), depth, chamfer)
        return self.add(material, v + vec(centre), t, m)

    def frame(self, material, centre, width, height, bar, depth, radius=0.02, m=None, steps=3):
        outer = rounded_rect(width, height, radius, steps)
        inner = rounded_rect(width - 2 * bar, height - 2 * bar, max(0.002, radius - bar), steps)
        v, t = ring(outer, inner, depth)
        return self.add(material, v + vec(centre), t, m)

    def sphere(self, material, centre, radius, segs=14, rings=8, m=None, squash=(1.0, 1.0, 1.0)):
        v, t = sphere(radius, segs, rings)
        return self.add(material, v * vec(squash) + vec(centre), t, m)

    def decal(self, atlas, key, centre, m=None, scale=1.0, width=None, lift=0.0006, material="A7_Decals"):
        """Atlas label facing +z of the frame `m`; physical size comes from the atlas unless width is given."""
        entry = atlas["entries"][key]
        w = entry["w"] * scale if width is None else width
        h = w * entry["h"] / entry["w"]
        v, t = quad(w, h)
        uv_corner = np.array([[entry["u0"], entry["v0"]], [entry["u1"], entry["v0"]],
                              [entry["u1"], entry["v1"]], [entry["u0"], entry["v1"]]])
        return self.add(material, v + vec(centre) + np.array([0.0, 0.0, lift]), t, m, uv_corner[t])

    def screen(self, atlas, key, centre, width, height, m=None, lift=0.0008):
        entry = atlas["entries"][key]
        v, t = quad(width, height)
        uv_corner = np.array([[entry["u0"], entry["v0"]], [entry["u1"], entry["v0"]],
                              [entry["u1"], entry["v1"]], [entry["u0"], entry["v1"]]])
        return self.add("A7_Screens", v + vec(centre) + np.array([0.0, 0.0, lift]), t, m, uv_corner[t])

    # ---- output
    def triangle_count(self):
        return sum(len(t) for chunks in self.parts.values() for _, t, _ in chunks)

    def bounds(self):
        points = np.concatenate([v for chunks in self.parts.values() for v, _, _ in chunks], axis=0)
        return points.min(axis=0), points.max(axis=0)

    def arrays(self, material):
        """Returns (vertices, triangles, per-corner uv or None-filled, explicit-uv mask per triangle)."""
        vs, ts, uvs, masks = [], [], [], []
        offset = 0
        for v, t, uv in self.parts[material]:
            vs.append(v)
            ts.append(t + offset)
            offset += len(v)
            if uv is None:
                uvs.append(np.zeros((len(t), 3, 2)))
                masks.append(np.zeros(len(t), dtype=bool))
            else:
                uvs.append(uv)
                masks.append(np.ones(len(t), dtype=bool))
        v = np.concatenate(vs, axis=0)
        t = np.concatenate(ts, axis=0)
        uv = np.concatenate(uvs, axis=0)
        mask = np.concatenate(masks, axis=0)
        projected = ~mask
        if projected.any():
            uv[projected] = box_project(v, t[projected])
        return v, t, uv


def _align_y(origin, axis):
    """Proper rotation taking +y onto `axis`, then translating to origin."""
    y = unit(axis)
    helper = np.array([1.0, 0.0, 0.0]) if abs(y[0]) < 0.9 else np.array([0.0, 0.0, 1.0])
    z = unit(np.cross(helper, y))
    x = np.cross(y, z)
    return basis(origin, x, y, z)


def box_project(v, t):
    """World-space box projection in metres: one UV unit is one metre along the two minor axes."""
    a, b, c = v[t[:, 0]], v[t[:, 1]], v[t[:, 2]]
    n = np.abs(np.cross(b - a, c - a))
    axis = np.argmax(n, axis=1)
    corners = np.stack([a, b, c], axis=1)
    uv = np.zeros((len(t), 3, 2))
    for dominant, (ua, va) in ((0, (2, 1)), (1, (0, 2)), (2, (0, 1))):
        sel = axis == dominant
        uv[sel, :, 0] = corners[sel][:, :, ua]
        uv[sel, :, 1] = corners[sel][:, :, va]
    return uv


def to_blender(v, t):
    """Unity (x, y, z) to Blender (x, z, y); the swap mirrors, so the winding is reversed once.

    Measured, not assumed: with bake_space_transform on export and Bake Axis Conversion on import,
    Unity reads Blender (x, y, z) as (x, z, y). The Unity import check verifies this on every build.
    """
    return np.column_stack([v[:, 0], v[:, 2], v[:, 1]]), t[:, ::-1]


def _selftest():
    for name, (v, t) in {
        "box": box((1, 2, 3)), "chamfered box": box((1, 2, 3), 0.05), "cylinder": cylinder(0.5, 2, 12, 0.03),
        "sphere": sphere(0.5), "tank": capsule_tank(0.3, 1.5),
        "tube": tube(fillet_path([(0, 0, 0), (1, 0, 0), (1, 1, 0), (1, 1, 1)], 0.2), 0.05),
        "plate": plate(rounded_rect(1, 0.5, 0.1), 0.05, 0.01, back=True),
    }.items():
        volume = signed_volume(v, t)
        assert volume > 0, (name, volume)
        print("%-14s tris %5d  volume %+.5f" % (name, len(t), volume))
    v, t = ring(rounded_rect(1, 1, 0.1), rounded_rect(0.8, 0.8, 0.05), 0.1, back=True)
    assert signed_volume(v, t) > 0, "ring"
    v, t = quad(1, 1)
    assert np.cross(v[t[0, 1]] - v[t[0, 0]], v[t[0, 2]] - v[t[0, 0]])[2] > 0, "quad faces +z"
    model = Model()
    model.box("m", (0, 0, 0), (1, 1, 1), 0.01, front_frame((0, 0, 0), (1, 0, 0)))
    v, t, _ = model.arrays("m")
    assert signed_volume(v, t) > 0, "mirrored placement keeps faces outward"
    frame = front_frame((0, 0, 0), (0, 0, 1))
    assert np.allclose(frame[:3, 0], (-1, 0, 0)), "viewer's right for a +z facing surface is -x in Unity"
    print("self-test passed")


if __name__ == "__main__":
    _selftest()
