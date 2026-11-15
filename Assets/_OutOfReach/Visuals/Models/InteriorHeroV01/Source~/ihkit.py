"""
ihkit - shared hardware helpers for INTERIOR-HERO-V01 (original work), built on the ship's own a7kit.

Pure numpy like a7kit, so it runs in Blender 3.6's Python and in plain Python.
Coordinates and winding are a7kit's: Unity axes (x right, y up, z forward), metres, front-view assemblies with local
+z towards the viewer, placed with front_frame(). See a7kit.py for the conventions.

Every helper takes the prop's level of detail into account: Prop(name, lod). LOD0 is the full hero mesh, LOD1 drops
bevels, fasteners and small hardware and halves the round segments, LOD2 keeps the silhouette only.
Use  if p.d(0):  for detail that exists only on LOD0, p.d(1) for detail kept on LOD0 and LOD1.
"""
import json
import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "..", "Aurora7", "Source")))
import a7kit  # noqa: E402
from a7kit import Model, vec, at, compose, rotation, front_frame, basis, unit, UP  # noqa: E402,F401

TEX = os.path.normpath(os.path.join(HERE, "..", "Textures"))
with open(os.path.join(TEX, "ih_labels.json")) as _f:
    LABELS = json.load(_f)
with open(os.path.join(TEX, "ih_screens.json")) as _f:
    SCREENS = json.load(_f)

# ----------------------------------------------------------------------------- materials (Unity side: IH_*.mat)
PAINT, PAINT_E = "IH_Paint", "IH_PaintEdge"                # warm putty powder coat / its chipped bevels
GRAPH, GRAPH_E = "IH_Graphite", "IH_GraphiteEdge"          # graphite anodise / worn bright bevels
ALU = "IH_Aluminium"
CHROME = "IH_Chrome"
GOLD = "IH_AnodGold"
ANO_RED = "IH_AnodRed"
ANO_BLUE = "IH_AnodBlue"
AMBER = "IH_PaintAmber"                                    # handholds, the ship's amber
RED = "IH_PaintRed"
FAB_SEAT = "IH_FabricSeat"
FAB_WHITE = "IH_FabricWhite"
FAB_BLUE = "IH_FabricBlue"
STRAP = "IH_Strap"
QUILT_SEAT = "IH_QuiltSeat"
QUILT_BAG = "IH_QuiltBag"
RUBBER = "IH_Rubber"
PLASTIC_W = "IH_PlasticWhite"
PLASTIC_D = "IH_PlasticDark"
GLASS = "IH_Glass"
WATER = "IH_WaterBag"
LABEL = "IH_Labels"
SCREEN = "IH_Screens"
LED_A, LED_C, LED_G, LED_R = "IH_LedAmber", "IH_LedCyan", "IH_LedGreen", "IH_LedRed"
GROW_LIGHT = "IH_GrowLight"
LAMP = "IH_LampWarm"
CABLE_O, CABLE_B, CABLE_Y = "IH_CableOrange", "IH_CableBlue", "IH_CableYellow"
BUNGEE = "IH_Bungee"
MEDIUM = "IH_Medium"

EDGE = {PAINT: PAINT_E, GRAPH: GRAPH_E}
ALL_MATERIALS = [PAINT, PAINT_E, GRAPH, GRAPH_E, ALU, CHROME, GOLD, ANO_RED, ANO_BLUE, AMBER, RED, FAB_SEAT, FAB_WHITE, FAB_BLUE,
                 STRAP, QUILT_SEAT, QUILT_BAG, RUBBER, PLASTIC_W, PLASTIC_D, GLASS, WATER, LABEL, SCREEN, LED_A, LED_C, LED_G,
                 LED_R, GROW_LIGHT, LAMP, CABLE_O, CABLE_B, CABLE_Y, BUNGEE, MEDIUM]


def superellipsoid(size, power=4.0, lon=14, lat=8):
    """Rounded cushion block centred on the origin: |x/a|^p + |y/b|^p + |z/c|^p = 1 sampled on a lat/long grid."""
    a, b, c = (float(s) / 2.0 for s in size)
    e = 2.0 / power

    def f(w):
        return np.sign(np.cos(w)) * np.abs(np.cos(w)) ** e

    def g(w):
        return np.sign(np.sin(w)) * np.abs(np.sin(w)) ** e

    verts = []
    for i in range(lat + 1):
        phi = -math.pi / 2 + math.pi * i / lat
        for k in range(lon):
            theta = 2 * math.pi * k / lon
            verts.append((a * f(phi) * f(theta), b * g(phi), c * f(phi) * g(theta)))
    v = np.asarray(verts)
    tris = []
    for i in range(lat):
        for k in range(lon):
            k1 = (k + 1) % lon
            p0, p1, p2, p3 = i * lon + k, i * lon + k1, (i + 1) * lon + k1, (i + 1) * lon + k
            if i > 0:
                tris.append((p0, p2, p1))
            if i < lat - 1:
                tris.append((p0, p3, p2))
    t = a7kit._outward_convex(v, np.asarray(tris, dtype=np.int64), np.zeros(3))
    return v, t


class Prop(Model):
    """A Model with a level of detail and the hero-prop hardware vocabulary."""

    def __init__(self, name, lod=0):
        super().__init__(name)
        self.lod = lod

    # ---- level of detail
    def d(self, level):
        return self.lod <= level

    def sg(self, n):
        if self.lod == 0:
            return n
        if self.lod == 1:
            return max(6, int(round(n * 0.6)))
        return max(4, n // 3)

    # ---- structure
    def wbox(self, mat, centre, size, chamfer=0.004, m=None):
        """Bevelled box; on LOD0 the bevel strips of painted / anodised parts take the worn-edge material."""
        if self.lod >= 1:
            chamfer = 0.0 if (self.lod == 2 or max(size) < 0.25) else chamfer
        v, t = a7kit.box(size, chamfer)
        v = v + vec(centre)
        edge = EDGE.get(mat) if (self.lod == 0 and 1e-6 < chamfer <= 0.0065) else None
        if edge is None:
            return self.add(mat, v, t, m)
        n = np.cross(v[t[:, 1]] - v[t[:, 0]], v[t[:, 2]] - v[t[:, 0]])
        n = np.abs(n) / (np.linalg.norm(n, axis=1, keepdims=True) + 1e-12)
        flat = n.max(axis=1) > 0.999
        self.add(mat, v, t[flat], m)
        return self.add(edge, v, t[~flat], m)

    def rcyl(self, mat, start, end, radius, segs=14, chamfer=0.0, m=None, caps=(True, True)):
        return self.cyl(mat, start, end, radius, self.sg(segs), chamfer if self.lod == 0 else 0.0, m, caps)

    def rtube(self, mat, points, radius, segs=8, fillet=0.0, m=None, closed=False, caps=True):
        return self.tube(mat, points, radius, self.sg(segs), fillet if self.lod < 2 else 0.0, m, closed, caps)

    def pillow(self, mat, centre, size, m=None, power=4.0, lon=16, lat=8):
        if self.lod == 2:
            return self.add(mat, a7kit.box(size, 0.0)[0] * 0.94 + vec(centre), a7kit.box(size, 0.0)[1], m)
        lon, lat = (lon, lat) if self.lod == 0 else (max(8, lon // 2), max(4, lat // 2))
        v, t = superellipsoid(size, power, lon, lat)
        return self.add(mat, v + vec(centre), t, m)

    def rplate(self, mat, centre, width, height, depth, radius=0.01, chamfer=0.003, m=None):
        """Rounded plate in the x/y plane of the frame, back at centre.z, front face at centre.z + depth."""
        steps = 3 if self.lod == 0 else 1
        return self.plate(mat, centre, width, height, depth, radius, chamfer if self.lod == 0 else 0.0, m, steps)

    # ---- atlas quads
    def label(self, key, centre, m=None, width=None, lift=0.0009, turn=0.0):
        """Label facing +z of the frame. turn rotates it in its own plane (degrees)."""
        if self.lod == 2:
            return self
        entry = LABELS["entries"][key]
        w = entry["w"] if width is None else width
        h = w * entry["h"] / entry["w"]
        v, t = a7kit.quad(w, h)
        uv = np.array([[entry["u0"], entry["v0"]], [entry["u1"], entry["v0"]], [entry["u1"], entry["v1"]], [entry["u0"], entry["v1"]]])
        frame = compose(m if m is not None else np.eye(4), at(*(vec(centre) + np.array([0.0, 0.0, lift]))), rotation((0, 0, 1), turn))
        return self.add(LABEL, v, t, frame, uv[t])

    def screen(self, key, centre, width, m=None, lift=0.001, height=None):
        entry = SCREENS["entries"][key]
        h = width * entry["h"] / entry["w"] if height is None else height
        v, t = a7kit.quad(width, h)
        uv = np.array([[entry["u0"], entry["v0"]], [entry["u1"], entry["v0"]], [entry["u1"], entry["v1"]], [entry["u0"], entry["v1"]]])
        return self.add(SCREEN, v + vec(centre) + np.array([0.0, 0.0, lift]), t, m, uv[t])

    # ---- fasteners and controls; all in a front-view frame `m`, mounted on the plane z of `centre`
    def bolt(self, x, y, z, m=None, radius=0.005, height=0.003, mat=ALU):
        if self.lod == 0:
            self.cyl(mat, (x, y, z), (x, y, z + height), radius, 6, 0.0, m, (False, True))
        return self

    def bolts(self, rect, z, m=None, inset=0.016, radius=0.005, mat=ALU):
        x0, y0, x1, y1 = rect
        for bx in (x0 + inset, x1 - inset):
            for by in (y0 + inset, y1 - inset):
                self.bolt(bx, by, z, m, radius, 0.003, mat)
        return self

    def captive(self, x, y, z, m=None):
        """Quarter-turn captive fastener: collar and a slotted head."""
        if self.lod == 0:
            self.cyl(ALU, (x, y, z), (x, y, z + 0.003), 0.009, 10, 0.0, m, (False, True))
            self.cyl(CHROME, (x, y, z + 0.003), (x, y, z + 0.007), 0.006, 8, 0.001, m, (False, True))
            self.box(GRAPH, (x, y, z + 0.0072), (0.009, 0.0016, 0.0006), 0.0, m)
        return self

    def handle(self, a, b, z, m=None, standoff=0.035, radius=0.006, mat=ALU):
        """U pull between a and b (x, y), standing off the plane z."""
        if self.lod == 2:
            return self
        (ax, ay), (bx, by) = a, b
        return self.rtube(mat, [(ax, ay, z), (ax, ay, z + standoff), (bx, by, z + standoff), (bx, by, z)], radius, 8, standoff * 0.45, m)

    def handrail(self, a, b, z, m=None, standoff=0.06, radius=0.013):
        """Amber translation aid on two aluminium stand-offs."""
        (ax, ay), (bx, by) = a, b
        self.rcyl(AMBER, (ax, ay, z + standoff), (bx, by, z + standoff), radius, 10, 0.003, m)
        direction = unit(vec(bx - ax, by - ay, 0.0))
        length = float(np.linalg.norm(vec(bx - ax, by - ay)))
        if self.d(1):
            for s in (0.08, 0.92):
                px, py = ax + direction[0] * length * s, ay + direction[1] * length * s
                self.rcyl(ALU, (px, py, z), (px, py, z + standoff), 0.008, 8, 0.0, m)
                self.wbox(ALU, (px, py, z + 0.003), (0.04, 0.04, 0.006), 0.001, m)
        return self

    def toggle(self, x, y, z, m=None, on=True, mat=CHROME):
        """Bat-handle toggle on a hex bushing; `on` leans the lever up."""
        if self.lod == 2:
            return self
        if self.lod == 0:
            self.cyl(ALU, (x, y, z), (x, y, z + 0.005), 0.0075, 6, 0.0, m, (False, True))
        lean = 0.009 if on else -0.009
        self.cyl(mat, (x, y, z + 0.004), (x, y + lean, z + 0.027), 0.0032, 6 if self.lod == 0 else 4, 0.0, m)
        if self.lod == 0:
            self.sphere(mat, (x, y + lean * 1.05, z + 0.0285), 0.0046, 8, 5, m)
        return self

    def wire_guard(self, x, y, z, m=None, span=0.034, height=0.032):
        """Bent-wire guard beside a toggle (two of them fence a switch)."""
        if self.lod == 0:
            self.tube(ALU, [(x, y - span / 2, z), (x, y - span / 2, z + height), (x, y + span / 2, z + height), (x, y + span / 2, z)],
                      0.0022, 5, 0.008, m)
        return self

    def toggle_row(self, x0, x1, y, z, count, m=None, legend=None, states=None, guards=True):
        """Row of guarded toggles on a graphite sub-panel; the legend strip sits below."""
        pitch = (x1 - x0) / count
        self.wbox(GRAPH, ((x0 + x1) / 2, y - 0.006, z + 0.002), (x1 - x0 + 0.02, 0.092, 0.004), 0.0015, m)
        for i in range(count):
            x = x0 + pitch * (i + 0.5)
            on = True if states is None else bool(states[i % len(states)])
            self.toggle(x, y + 0.012, z + 0.004, m, on)
            if guards:
                self.wire_guard(x - pitch * 0.5 + 0.004, y + 0.012, z + 0.004, m)
        if guards:
            self.wire_guard(x1 - 0.004, y + 0.012, z + 0.004, m)
        if legend:
            self.label(legend, ((x0 + x1) / 2, y - 0.034, z + 0.004), m, width=x1 - x0)
        return self

    def knob(self, x, y, z, m=None, radius=0.014, height=0.02):
        if self.lod == 2:
            return self
        self.rcyl(GRAPH, (x, y, z), (x, y, z + 0.004), radius * 1.35, 14, 0.001, m, (False, True))
        self.rcyl(PLASTIC_D, (x, y, z + 0.004), (x, y, z + height), radius, 12, 0.002, m, (False, True))
        if self.lod == 0:
            self.box(PLASTIC_W, (x, y + radius * 0.55, z + height + 0.0004), (0.002, radius * 0.8, 0.0008), 0.0, m)
        return self

    def connector(self, x, y, z, m=None, radius=0.014, cap=False, cable=None, cable_mat=RUBBER, cable_radius=0.0055):
        """Circular bulkhead connector: square flange, shell, coupling ring; optional tethered dust cap or a cable
        (list of points in the same frame, starting anywhere - the backshell is added at the connector)."""
        if self.lod == 2:
            return self
        f = radius * 1.55
        self.wbox(ALU, (x, y, z + 0.002), (f * 2, f * 2, 0.004), 0.001, m)
        if self.lod == 0:
            for sx in (-1, 1):
                for sy in (-1, 1):
                    self.bolt(x + sx * f * 0.74, y + sy * f * 0.74, z + 0.004, m, 0.0028, 0.002, GRAPH)
        self.rcyl(ALU, (x, y, z + 0.004), (x, y, z + 0.016), radius, 12, 0.001, m, (False, True))
        if cap:
            self.rcyl(RED, (x, y, z + 0.016), (x, y, z + 0.026), radius * 1.12, 12, 0.002, m, (False, True))
            if self.lod == 0:
                self.tube(ALU, [(x + radius, y, z + 0.02), (x + radius * 1.5, y - radius * 1.2, z + 0.012), (x + f * 0.74, y - f * 0.74, z + 0.006)],
                          0.0012, 4, 0.0, m)
        elif cable is not None:
            self.rcyl(GRAPH, (x, y, z + 0.012), (x, y, z + 0.030), radius * 1.18, 12, 0.002, m, (False, True))
            self.rcyl(ALU, (x, y, z + 0.030), (x, y, z + 0.046), radius * 0.8, 10, 0.002, m, (False, True))
            self.rtube(cable_mat, [(x, y, z + 0.044)] + list(cable), cable_radius, 7, 0.03, m)
        else:
            self.rcyl(GRAPH, (x, y, z + 0.016), (x, y, z + 0.0165), radius * 0.78, 12, 0.0, m, (False, True))
        return self

    def louvre(self, rect, z, m=None, slats=None, mat=GRAPH):
        """Recessed louvred vent: frame and tilted slats."""
        x0, y0, x1, y1 = rect
        w, h = x1 - x0, y1 - y0
        cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
        self.frame(PAINT, (cx, cy, z), w, h, 0.016, 0.01, 0.01, m, 3 if self.lod == 0 else 1)
        self.box(PLASTIC_D, (cx, cy, z - 0.012), (w - 0.03, h - 0.03, 0.002), 0.0, m)
        if self.lod == 2:
            return self
        count = slats or max(3, int(h / 0.032))
        if self.lod == 1:
            count = max(3, count // 2)
        for i in range(count):
            y = y0 + 0.02 + (h - 0.04) * (i + 0.5) / count
            tilt = compose(m if m is not None else np.eye(4), at(cx, y, z - 0.002), rotation((1, 0, 0), 28))
            self.box(mat, (0, 0, 0), (w - 0.034, (h - 0.04) / count * 0.74, 0.003), 0.0, tilt)
        return self

    def velcro(self, key, x, y, z, m=None, width=None, turn=0.0):
        """Hook-and-loop patch: a slightly proud pad carrying the atlas texture."""
        if self.lod == 2:
            return self
        entry = LABELS["entries"][key]
        w = entry["w"] if width is None else width
        h = w * entry["h"] / entry["w"]
        frame = compose(m if m is not None else np.eye(4), at(x, y, z), rotation((0, 0, 1), turn))
        if self.lod == 0:
            self.box(PLASTIC_D, (0, 0, 0.0008), (w, h, 0.0016), 0.0, frame)
        return self.label(key, (0, 0, 0.0016), frame, w)

    def bungee(self, points, m=None, radius=0.004, mat=BUNGEE, hooks=True):
        """Elastic cord with wire hooks at both ends."""
        if self.lod == 2:
            return self
        self.rtube(mat, points, radius, 6, 0.02, m)
        if hooks and self.lod == 0:
            for end, nxt in ((points[0], points[1]), (points[-1], points[-2])):
                e, n = vec(end), vec(nxt)
                out = unit(e - n)
                self.cyl(CHROME, e - out * 0.004, e + out * 0.012, 0.0055, 6, 0.001, m)
        return self

    def strap(self, points, width, m=None, thickness=0.003, mat=STRAP, normal=(0, 0, 1)):
        """Flat webbing along a polyline; `normal` is the face direction of the webbing."""
        nrm = unit(normal)
        pts = [vec(p) for p in points]
        for a, b in zip(pts[:-1], pts[1:]):
            along = b - a
            length = float(np.linalg.norm(along))
            if length < 1e-6:
                continue
            along = along / length
            side = unit(np.cross(nrm, along))
            up = np.cross(along, side)
            frame = compose(m if m is not None else np.eye(4), basis((a + b) / 2, side, up, along))
            self.box(mat, (0, 0, 0), (width, thickness, length + thickness), 0.0, frame)
        return self

    def buckle(self, centre, m=None, width=0.05, mat=ALU):
        if self.lod == 2:
            return self
        cx, cy, cz = centre
        self.frame(mat, (cx, cy, cz), width, width * 0.62, width * 0.12, 0.004, 0.004, m, 2)
        self.box(mat, (cx, cy, cz + 0.002), (width * 0.1, width * 0.62, 0.004), 0.0, m)
        return self

    def led(self, x, y, z, m=None, mat=LED_G, radius=0.005):
        if self.lod == 2:
            return self
        if self.lod == 0:
            self.cyl(ALU, (x, y, z), (x, y, z + 0.002), radius * 1.5, 8, 0.0, m, (False, True))
        return self.cyl(mat, (x, y, z), (x, y, z + 0.004), radius, 8 if self.lod == 0 else 5, 0.001 if self.lod == 0 else 0.0, m, (False, True))

    def cable_clamp(self, x, y, z, m=None, radius=0.012):
        """P-clamp holding a cable against the plane z."""
        if self.lod == 0:
            self.box(ALU, (x, y, z + radius), (radius * 2.6, 0.012, radius * 2.2), 0.002, m)
            self.bolt(x + radius * 1.6, y, z, m, 0.004, 0.004)
        return self


def side_frame(x, z, facing):
    """Front-view frame for wall equipment: origin on the floor at (x, 0, z), viewer side `facing` (+1 looks from +x,
    -1 looks from -x). Local +x is the viewer's right, +y up, +z towards the viewer."""
    return front_frame((x, 0.0, z), (float(facing), 0.0, 0.0))
