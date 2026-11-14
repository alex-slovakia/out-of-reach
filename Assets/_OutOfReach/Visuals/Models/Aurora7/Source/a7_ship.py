"""
AURORA-7 ship model (original work): geometry for every compartment, built with a7kit.

Layout follows the V01 handoff exactly (Unity metres, floor at y = 0, ship along +z):
    cockpit      6.0 m wide, z -3 ..  3      engineering  4.8 m wide, z  3 .. 11
    storage      4.8 m wide, z 11 .. 17      airlock      4.8 m wide, z 17 .. 21
Portals are 1.8 m x 2.5 m clear. Racks stand at the PowerRepair and LifeSupportRepair anchors.
Visible detail stays inside the blockout's solid envelope or above head height.

Design references (proportions and function only, nothing copied): ISS rack bays, seat track,
handrails, CTB soft stowage and luminaires; Orion display-and-control layout and seats; Skylab-class
ring frames; Quest airlock hatch hardware. Everything here is a fictional near-future vessel.
"""
import math
import sys

import numpy as np

sys.dont_write_bytecode = True  # keep __pycache__ out of the Unity project
from a7kit import Model, at, basis, compose, front_frame, rotation, vec  # noqa: E402

# ----------------------------------------------------------------------------- material names
WHITE, GRAY, GRAPHITE = "A7_PaintWhite", "A7_PaintGray", "A7_PaintGraphite"
RED, YELLOW = "A7_PaintRed", "A7_PaintYellow"
ANO, ANO_CLEAR, ANO_BLUE, ANO_GOLD = "A7_AnodizedBlack", "A7_AnodizedClear", "A7_AnodizedBlue", "A7_AnodizedGold"
ALU = "A7_Aluminum"
FAB, FAB_DARK, STRAP, CANVAS = "A7_FabricWhite", "A7_FabricDark", "A7_FabricStrap", "A7_FabricCanvas"
QUILT = "A7_QuiltWhite"
DECK = "A7_Deck"
RUBBER, CABLE_ORANGE, CABLE_BLUE = "A7_Rubber", "A7_CableOrange", "A7_CableBlue"
CARBON = "A7_Carbon"
GLASS = "A7_Glass"
LIGHT = "A7_LightPanel"
LED_G, LED_A, LED_R, LED_C = "A7_LedGreen", "A7_LedAmber", "A7_LedRed", "A7_LedCyan"
DECALS, SCREENS = "A7_Decals", "A7_Screens"
HULL, SOLAR, MLI_GOLD, MLI_SILVER = "A7_HullBlanket", "A7_SolarCell", "A7_MLIGold", "A7_MLISilver"
AMBER, PLASTIC, CHROME, GRATE, WORKLIGHT = "A7_PaintAmber", "A7_PlasticWhite", "A7_Chrome", "A7_Grate", "A7_WorkLight"
CABLE_RED, CABLE_YELLOW = "A7_CableRed", "A7_CableYellow"

AXIS_Y = 1.56             # hull centreline height: midway between deck (0) and ceiling (3.12)
R_FWD, R_AFT = 3.55, 3.0  # outer radius around the 6 m flight deck and around the 4.8 m compartments

CEILING = 3.12
RIB_PITCH = 1.6
ROOMS = {
    "Cockpit": {"width": 6.0, "z0": -3.0, "z1": 3.0, "fixtures": [-1.6, 1.4]},
    "Engineering": {"width": 4.8, "z0": 3.0, "z1": 11.0, "fixtures": [4.4, 7.4, 10.4]},
    "Storage": {"width": 4.8, "z0": 11.0, "z1": 17.0, "fixtures": [12.4, 15.4]},
    "Airlock": {"width": 4.8, "z0": 17.0, "z1": 21.0, "fixtures": [18.4]},
}


class Art:
    """Shared atlases and a deterministic random source."""

    def __init__(self, decals, screens, seed=7):
        self.decals = decals
        self.screens = screens
        self.rng = np.random.default_rng(seed)
        self._cycle = {}

    def next(self, prefix, count):
        index = self._cycle.get(prefix, 0)
        self._cycle[prefix] = index + 1
        return "%s_%02d" % (prefix, index % count)


# ----------------------------------------------------------------------------- small hardware
def bolt(m, x, y, z, radius=0.005, height=0.003, mat=ALU, frame=None):
    m.cyl(mat, (x, y, z), (x, y, z + height), radius, 6, 0.0, frame, (False, True))


def corner_bolts(m, rect, z, inset=0.018, mat=ALU, frame=None, radius=0.005):
    x0, y0, x1, y1 = rect
    for x in (x0 + inset, x1 - inset):
        for y in (y0 + inset, y1 - inset):
            bolt(m, x, y, z, radius, 0.003, mat, frame)


def u_handle(m, a, b, z, standoff=0.035, radius=0.006, mat=ALU, frame=None):
    path = [(a[0], a[1], z), (a[0], a[1], z + standoff), (b[0], b[1], z + standoff), (b[0], b[1], z)]
    m.tube(mat, path, radius, 8, 0.012, frame)


def handrail(m, a, b, z, frame=None, standoff=0.06, mat=AMBER):
    """Translation aid: an amber bar on two standoffs with base plates."""
    path = [(a[0], a[1], z), (a[0], a[1], z + standoff), (b[0], b[1], z + standoff), (b[0], b[1], z)]
    m.tube(mat, path, 0.015, 10, 0.03, frame)
    for p in (a, b):
        m.box(ALU, (p[0], p[1], z + 0.003), (0.05, 0.05, 0.006), 0.002, frame)


def panel(m, rect, z, mat=WHITE, depth=0.012, frame=None, bolts=True, radius=0.012):
    x0, y0, x1, y1 = rect
    m.plate(mat, ((x0 + x1) / 2, (y0 + y1) / 2, z), x1 - x0, y1 - y0, depth, radius, 0.003, frame)
    if bolts:
        corner_bolts(m, rect, z + depth, frame=frame)


def vent(m, rect, z, frame=None, slats=None):
    """Louvred grille in a frame."""
    x0, y0, x1, y1 = rect
    w, h = x1 - x0, y1 - y0
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    m.frame(ANO, (cx, cy, z), w, h, 0.014, 0.012, 0.008, frame)
    m.box(GRAPHITE, (cx, cy, z + 0.001), (w - 0.02, h - 0.02, 0.002), 0.0, frame)
    count = slats or max(3, int(h / 0.022))
    for i in range(count):
        y = y0 + 0.02 + (h - 0.04) * (i + 0.5) / count
        placement = compose(frame if frame is not None else np.eye(4), at(cx, y, z + 0.007), rotation((1, 0, 0), 32))
        m.box(ANO, (0, 0, 0), (w - 0.034, 0.003, 0.016), 0.0, placement)


def locker(m, art, rect, z, frame=None, title=None, barcode=True):
    """Stowage locker door: pressed panel, bar pull in a recess, hinge barrels and inventory labels."""
    x0, y0, x1, y1 = rect
    w, h = x1 - x0, y1 - y0
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    m.plate(WHITE, (cx, cy, z), w - 0.012, h - 0.012, 0.014, 0.014, 0.004, frame)
    press_w, press_h = w - 0.22, h - 0.07
    px = x0 + 0.045 + press_w / 2
    top = z + 0.019
    if press_w > 0.12 and press_h > 0.10:
        m.plate(WHITE, (px, cy, z + 0.014), press_w, press_h, 0.005, 0.02, 0.003, frame)
    else:
        top = z + 0.014
    lx, ly = x1 - 0.085, cy
    pull = min(0.17, h * 0.55)
    m.box(GRAPHITE, (lx, ly, z + 0.015), (0.07, pull, 0.004), 0.002, frame)
    m.cyl(ALU, (lx, ly - pull * 0.36, z + 0.028), (lx, ly + pull * 0.36, z + 0.028), 0.008, 8, 0.0, frame)
    for dy in (-pull * 0.36, pull * 0.36):
        m.cyl(ALU, (lx, ly + dy, z + 0.016), (lx, ly + dy, z + 0.028), 0.006, 6, 0.0, frame)
    for hy in (y0 + h * 0.2, y1 - h * 0.2):
        m.cyl(ALU, (x0 + 0.012, hy - 0.03, z + 0.014), (x0 + 0.012, hy + 0.03, z + 0.014), 0.007, 8, 0.001, frame)
    if title:
        m.decal(art.decals, title, (px, cy + press_h / 2 - 0.04, top), frame, width=min(0.30, press_w - 0.04))
    if barcode:
        m.decal(art.decals, art.next("barcode", 24), (px - press_w / 2 + 0.06, cy - press_h / 2 + 0.04, top), frame, 0.8)
        m.decal(art.decals, art.next("pn", 28), (px + 0.03, cy - press_h / 2 + 0.03, top), frame, 0.85)
    if art.rng.random() < 0.6:
        m.decal(art.decals, "velcro_blue", (px - press_w / 2 + 0.05 + art.rng.random() * max(0.02, press_w - 0.12), cy - h * 0.12, top), frame)


def switch_panel(m, art, rect, z, frame=None, legend="leg_power", count=5):
    """Guarded toggle switches under an engraved legend, with status lamps."""
    x0, y0, x1, y1 = rect
    w, h = x1 - x0, y1 - y0
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    m.plate(GRAPHITE, (cx, cy, z), w, h, 0.010, 0.008, 0.003, frame)
    corner_bolts(m, rect, z + 0.010, 0.012, ANO_CLEAR, frame, 0.004)
    legend_w = min(w - 0.03, 0.30)
    m.decal(art.decals, legend, (cx, y1 - 0.026, z + 0.010), frame, width=legend_w)
    cell = legend_w / count
    sy = y0 + h * 0.36
    for i in range(count):
        sx = cx - legend_w / 2 + cell * (i + 0.5)
        m.cyl(ALU, (sx, sy, z + 0.010), (sx, sy, z + 0.016), 0.008, 6, 0.001, frame)
        up = art.rng.random() < 0.7
        tip = (sx, sy + (0.012 if up else -0.012), z + 0.036)
        m.cyl(ANO_CLEAR, (sx, sy, z + 0.016), tip, 0.0032, 6, 0.0, frame)
        lamp = LED_G if up else LED_A
        m.cyl(lamp, (sx, sy + 0.034, z + 0.010), (sx, sy + 0.034, z + 0.013), 0.0042, 8, 0.0, frame, (False, True))
    for gy in (sy - 0.026, sy + 0.022):
        m.tube(ANO_CLEAR, [(cx - legend_w / 2, gy, z + 0.010), (cx - legend_w / 2, gy, z + 0.03),
                           (cx + legend_w / 2, gy, z + 0.03), (cx + legend_w / 2, gy, z + 0.010)], 0.0025, 6, 0.008, frame)


def display(m, art, rect, z, frame=None, key="sm_power", buttons=6):
    x0, y0, x1, y1 = rect
    w, h = x1 - x0, y1 - y0
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    m.frame(ANO, (cx, cy, z), w, h, 0.022, 0.018, 0.012, frame)
    m.box(GRAPHITE, (cx, cy, z + 0.004), (w - 0.03, h - 0.03, 0.008), 0.0, frame)
    m.screen(art.screens, key, (cx, cy, z + 0.0085), w - 0.05, h - 0.05, frame)
    for i in range(buttons):
        bx = x0 + 0.03 + (w - 0.06) * (i + 0.5) / buttons
        m.box(ANO_CLEAR, (bx, y0 + 0.011, z + 0.019), (0.022, 0.008, 0.004), 0.001, frame)


def connectors(m, art, rect, z, frame=None, count=4, cable_to=0.14, first_label=0):
    """Circular connectors with labels and cables running down into the floor trough."""
    x0, y0, x1, y1 = rect
    cy = (y0 + y1) / 2
    colours = [RUBBER, RUBBER, CABLE_ORANGE, RUBBER, CABLE_BLUE]
    for i in range(count):
        x = x0 + (x1 - x0) * (i + 0.5) / count
        m.box(ALU, (x, cy, z + 0.002), (0.036, 0.036, 0.004), 0.001, frame)
        m.cyl(ANO_CLEAR, (x, cy, z + 0.004), (x, cy, z + 0.022), 0.012, 10, 0.002, frame)
        m.decal(art.decals, "conn_%02d" % ((first_label + i) % 16), (x, cy + 0.034, z), frame, 0.75)
        if art.rng.random() < 0.8:
            m.cyl(RUBBER, (x, cy, z + 0.022), (x, cy, z + 0.04), 0.009, 8, 0.002, frame)
            drift = (art.rng.random() - 0.5) * 0.05
            path = [(x, cy, z + 0.04), (x, cy, z + 0.075), (x + drift, cy - 0.10, z + 0.075),
                    (x + drift, cable_to + 0.05, z + 0.03), (x + drift, cable_to, z + 0.01)]
            m.tube(colours[(first_label + i) % len(colours)], path, 0.0055, 6, 0.03, frame)


def oru(m, art, rect, z, frame=None, title="t_battery", caution="c_hv", proud=0.03):
    """Orbital replacement unit: a removable box with handles and captive fasteners."""
    x0, y0, x1, y1 = rect
    w, h = x1 - x0, y1 - y0
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    m.box(GRAY, (cx, cy, z + proud / 2), (w - 0.016, h - 0.016, proud), 0.006, frame)
    for x in (x0 + 0.03, x1 - 0.03):
        for y in (y0 + 0.03, y1 - 0.03):
            m.cyl(ANO_GOLD, (x, y, z + proud), (x, y, z + proud + 0.006), 0.009, 8, 0.002, frame)
    for x in (x0 + 0.075, x1 - 0.075):
        u_handle(m, (x, cy - h * 0.2), (x, cy + h * 0.2), z + proud, 0.04, 0.007, ALU, frame)
    m.decal(art.decals, title, (cx, y1 - 0.05, z + proud), frame, width=min(0.30, w - 0.2))
    if caution and h > 0.3:
        m.decal(art.decals, caution, (cx, cy - 0.02, z + proud), frame, 0.75)
    m.decal(art.decals, art.next("pn", 28), (cx, y0 + 0.03, z + proud), frame, 0.8)
    m.decal(art.decals, "fastener_note", (cx, y0 + 0.058, z + proud), frame, 0.7)


def gauge_needle(radius, fraction):
    """Pointer of a dial whose scale runs 270 degrees clockwise from the lower left; pivot at the origin.

    fraction 0 is the start of the scale and 1 its end; the angle is 225 - 270 * fraction degrees,
    counter-clockwise from the viewer's right."""
    n = Model("Needle")
    n.box(RED, (radius * 0.30, 0, 0.0015), (radius, radius * 0.05, 0.003), 0.0, rotation((0, 0, 1), 225.0 - 270.0 * fraction))
    n.cyl(ANO, (0, 0, 0), (0, 0, 0.005), radius * 0.10, 10)
    return n


def gauge(m, art, x, y, z, frame=None, key="gauge_cabin", radius=0.05, needle=0.84):
    """Dial in a bezel. `needle` is the pointer position as a fraction of the scale, or None for no pointer."""
    m.lathe(ALU, (x, y, z), (0, 0, 1), [(radius + 0.012, 0.0), (radius + 0.012, 0.012), (radius + 0.004, 0.016),
                                        (radius, 0.016), (radius, 0.006)], 20, frame, (False, False))
    m.decal(art.decals, key, (x, y, z + 0.0065), frame, width=radius * 2)
    if needle is not None:
        m.instance(gauge_needle(radius, needle), compose(frame if frame is not None else np.eye(4), at(x, y, z + 0.008)))


def blank(m, art, rect, z, frame=None, label=True):
    panel(m, rect, z, WHITE, 0.012, frame)
    if label:
        x0, y0, x1, y1 = rect
        m.decal(art.decals, art.next("pn", 28), ((x0 + x1) / 2, y0 + 0.035, z + 0.012), frame, 0.85)


def ctb(m, art, centre, size, frame=None, label=None, straps=True):
    """Cargo transfer bag: soft white fabric box with webbing, a handle and a contents label."""
    cx, cy, cz = centre
    w, h, d = size
    m.box(FAB, centre, size, min(0.03, 0.2 * min(size)), frame)
    front = cz + d / 2
    if straps:
        for sx in (cx - w * 0.28, cx + w * 0.28):
            m.box(STRAP, (sx, cy, front + 0.002), (0.03, h * 0.98, 0.004), 0.0, frame)
    m.box(STRAP, (cx, cy + h * 0.36, front + 0.003), (w * 0.9, 0.012, 0.005), 0.0, frame)
    m.tube(STRAP, [(cx - 0.05, cy + h * 0.5 - 0.01, front), (cx - 0.05, cy + h * 0.5 + 0.015, front + 0.03),
                   (cx + 0.05, cy + h * 0.5 + 0.015, front + 0.03), (cx + 0.05, cy + h * 0.5 - 0.01, front)],
           0.006, 6, 0.015, frame)
    if label:
        m.decal(art.decals, label, (cx, cy - h * 0.05, front + 0.0045), frame, width=min(0.2, w * 0.6))
    m.decal(art.decals, art.next("barcode", 24), (cx + w * 0.26, cy - h * 0.34, front + 0.001), frame, 0.6)


def luminaire(m, x, z, frame_y=CEILING):
    """General luminaire: housing, emissive diffuser, guard bars. Hangs under the ceiling at (x, z)."""
    f = front_frame((x, frame_y, z), (0, -1, 0), (0, 0, 1))
    m.box(GRAY, (0, 0, 0.055), (1.30, 0.42, 0.11), 0.012, f)
    m.frame(ANO, (0, 0, 0.11), 1.20, 0.34, 0.03, 0.012, 0.02, f)
    m.box(LIGHT, (0, 0, 0.113), (1.14, 0.28, 0.006), 0.0, f)
    for gx in (-0.3, 0.0, 0.3):
        m.tube(ANO_CLEAR, [(gx, -0.16, 0.122), (gx, -0.16, 0.14), (gx, 0.16, 0.14), (gx, 0.16, 0.122)], 0.004, 6, 0.01, f)
    for bx in (-0.61, 0.61):
        for by in (-0.17, 0.17):
            bolt(m, bx, by, 0.11, 0.006, 0.004, ALU, f)


# ----------------------------------------------------------------------------- racks
def rack_shell(m, width=1.6, height=2.3, depth=0.58):
    """Freestanding rack body; the front face is z = depth / 2 in front-view space."""
    front = depth / 2
    m.box(GRAY, (0, height / 2, 0), (width, height, depth), 0.012)
    m.frame(ANO, (0, height / 2, front), width - 0.02, height - 0.02, 0.045, 0.012, 0.02)
    for sx in (-1, 1):
        x = sx * (width / 2 - 0.0225)
        for i in range(int((height - 0.2) / 0.1)):
            m.cyl(GRAPHITE, (x, 0.12 + i * 0.1, front + 0.012), (x, 0.12 + i * 0.1, front + 0.0125), 0.007, 6, 0.0,
                  None, (False, True))
    m.box(GRAPHITE, (0, 0.035, front - 0.02), (width - 0.06, 0.07, 0.03), 0.0)
    return front + 0.002


# Named helper points, name -> (export group, point in ship coordinates); exported as empties for the integrator.
ANCHORS = {}


def curved_label(m, atlas, key, centre, radius, width=None, frame=None, segments=8, lift=0.0008):
    """Atlas label wrapped around a vertical cylinder whose axis passes through `centre`; faces +z."""
    entry = atlas["entries"][key]
    w = entry["w"] if width is None else width
    h = w * entry["h"] / entry["w"]
    half = w / (2.0 * radius)
    r = radius + lift
    cx, cy, cz = centre
    n = segments + 1
    v, uv = [], []
    for y, tv in ((cy - h / 2, entry["v0"]), (cy + h / 2, entry["v1"])):
        for k in range(n):
            a = -half + 2.0 * half * k / segments
            v.append((cx + r * math.sin(a), y, cz + r * math.cos(a)))
            uv.append((entry["u0"] + (entry["u1"] - entry["u0"]) * k / segments, tv))
    t = []
    for k in range(segments):
        t += [(k, k + 1, n + k + 1), (k, n + k + 1, n + k)]
    t = np.asarray(t, dtype=np.int64)
    m.add(DECALS, np.asarray(v), t, frame, np.asarray(uv)[t])


def rack_open(m, opening, recess, width=1.6, height=2.3, depth=0.58):
    """Rack body with a recessed service bay behind a radiused bezel.

    opening = (x0, y0, x1, y1) on the face. Returns (z of the face, z of the bay's back plate)."""
    front = depth / 2
    half = width / 2
    x0, y0, x1, y1 = opening
    cx, cy, w, h = (x0 + x1) / 2, (y0 + y1) / 2, x1 - x0, y1 - y0
    back = front - recess
    m.box(GRAY, (0, y0 / 2, 0), (width, y0, depth), 0.012)
    m.box(GRAY, (0, (y1 + height) / 2, 0), (width, height - y1, depth), 0.012)
    m.box(GRAY, ((x0 - half) / 2, cy, 0), (x0 + half, h, depth), 0.0)
    m.box(GRAY, ((x1 + half) / 2, cy, 0), (half - x1, h, depth), 0.0)
    m.box(GRAPHITE, (cx, cy, (back - front) / 2), (w, h, back + front), 0.0)
    for sx, x in ((1, x0), (-1, x1)):
        m.box(GRAPHITE, (x + sx * 0.003, cy, (back + front) / 2), (0.006, h, recess), 0.0)
    for sy, y in ((1, y0), (-1, y1)):
        m.box(GRAPHITE, (cx, y + sy * 0.003, (back + front) / 2), (w, 0.006, recess), 0.0)
    # face frame with captive fasteners down both rails
    m.frame(WHITE, (0, height / 2, front), width - 0.02, height - 0.02, 0.045, 0.012, 0.03)
    for sx in (-1, 1):
        for y in np.arange(0.16, height - 0.1, 0.22):
            bolt(m, sx * (half - 0.0325), y, front + 0.012, 0.006, 0.003, ALU)
    # bezel: flange and raised lip, radiused inside and out
    m.frame(WHITE, (cx, cy, front), w + 0.13, h + 0.13, 0.08, 0.016, 0.13, None, 5)
    m.frame(WHITE, (cx, cy, front + 0.016), w + 0.085, h + 0.085, 0.05, 0.010, 0.105, None, 5)
    for bx in np.linspace(x0 + 0.12, x1 - 0.12, 5):
        for by in (y0 - 0.054, y1 + 0.054):
            bolt(m, bx, by, front + 0.016, 0.0055, 0.003, ALU)
    for by in np.linspace(y0 + 0.12, y1 - 0.12, 6):
        for bx in (x0 - 0.054, x1 + 0.054):
            bolt(m, bx, by, front + 0.016, 0.0055, 0.003, ALU)
    return front + 0.002, back


def fascia(m, art, rect, z, key, text_width=0.60):
    """Stencilled name plate across the head of a service rack."""
    x0, y0, x1, y1 = rect
    panel(m, rect, z, WHITE, 0.016, None, True, 0.035)
    m.decal(art.decals, key, (x0 + 0.06 + text_width / 2, (y0 + y1) / 2, z + 0.016), None, width=text_width)


def drawer(m, art, rect, z, title):
    """Stowage drawer under a service bay: paddle latch in a recess, hinge barrels, labels."""
    x0, y0, x1, y1 = rect
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    panel(m, rect, z, WHITE, 0.016, None, True, 0.03)
    m.box(GRAPHITE, (cx, cy, z + 0.0165), (0.12, 0.09, 0.003), 0.0)
    m.box(ALU, (cx, cy + 0.012, z + 0.024), (0.08, 0.034, 0.012), 0.004)
    m.cyl(ALU, (cx - 0.035, cy - 0.026, z + 0.021), (cx + 0.035, cy - 0.026, z + 0.021), 0.005, 8)
    for hx in (x0 + 0.12, x1 - 0.12):
        m.cyl(ALU, (hx - 0.045, y0 + 0.002, z + 0.016), (hx + 0.045, y0 + 0.002, z + 0.016), 0.008, 8, 0.001)
    m.decal(art.decals, title, (x0 + 0.22, y1 - 0.055, z + 0.016), None, width=0.30)
    m.decal(art.decals, art.next("pn", 28), (x1 - 0.13, y0 + 0.04, z + 0.016), None, 0.85)


BREAKER_W = 0.05


LEVER_TILT = 35.0


def breaker_lever(on=True, width=BREAKER_W):
    """Toggle of a breaker with its pivot at the origin: a paddle tilted up when closed, down when tripped.

    The two states differ by a pure rotation of 2 x LEVER_TILT about the local x axis."""
    m = Model("Lever")
    m.box(ANO, (0, 0, 0.012), (width * 0.40, 0.013, 0.032), 0.003, rotation((1, 0, 0), -LEVER_TILT if on else LEVER_TILT))
    return m


def breaker(m, x, y, zb, width=BREAKER_W, on=True, lever=True):
    """Rail-mounted circuit breaker, 0.16 m tall. (x, y) is the body centre and zb the back plate."""
    w = width - 0.003
    m.box(PLASTIC, (x, y, zb + 0.040), (w, 0.160, 0.056), 0.003)
    m.box(PLASTIC, (x, y, zb + 0.076), (w, 0.082, 0.020), 0.003)
    m.box(GRAPHITE, (x, y - 0.018, zb + 0.0865), (w * 0.55, 0.034, 0.002), 0.0)
    for sy in (-1, 1):
        m.cyl(ALU, (x, y + sy * 0.062, zb + 0.068), (x, y + sy * 0.062, zb + 0.0705), 0.007, 8, 0.0, None, (False, True))
    if lever:
        m.instance(breaker_lever(on, width), at(x, y - 0.018, zb + 0.0875))


def power_rack(art):
    """EPS distribution rack at the PowerRepair anchor (asset A03): an open, recessed breaker cabinet.

    Returns (rack, parts, anchors). Parts are separately addressable objects carrying `local_origin`;
    everything is in the rack's front-view space."""
    m = Model("PowerRack")
    opening = (-0.52, 0.50, 0.52, 1.86)
    x0, y0, x1, y1 = opening
    z, zb = rack_open(m, opening, 0.20)
    cy = (y0 + y1) / 2

    fascia(m, art, (-0.60, 1.955, 0.46, 2.25), z, "fascia_power")      # clear of the feeder conduit
    m.decal(art.decals, "rack_05", (0.61, 1.985, z + 0.001), width=0.14)
    for i, lamp in enumerate((LED_G, LED_G, LED_A)):
        lx = 0.55 + i * 0.06
        m.cyl(ANO, (lx, 2.06, z), (lx, 2.06, z + 0.008), 0.014, 10, 0.002)
        m.cyl(lamp, (lx, 2.06, z + 0.008), (lx, 2.06, z + 0.011), 0.008, 10, 0.0, None, (False, True))
    drawer(m, art, (-0.74, 0.10, 0.74, 0.41), z, "t_spares")

    # fore column: armoured feeder conduit in saddle clamps; aft column: bus status and caution
    m.cyl(RUBBER, (-0.67, 0.44, z + 0.03), (-0.67, 2.285, z + 0.03), 0.024, 12)
    for y in (0.58, 0.98, 1.38, 1.78, 2.14):
        m.box(ALU, (-0.67, y, z + 0.022), (0.085, 0.026, 0.05), 0.004)
        for bx in (-0.705, -0.635):
            bolt(m, bx, y, z + 0.047, 0.004, 0.003, ANO_CLEAR)
    display(m, art, (0.595, 1.50, 0.75, 1.62), z, None, "sm_power", 3)
    m.decal(art.decals, "c_hv", (0.672, 1.30, z + 0.001), None, 0.72)
    m.decal(art.decals, art.next("pn", 28), (0.672, 0.52, z + 0.001), None, 0.9)

    # incoming feeders down the fore side of the bay, entering through a gland plate
    for tx, radius, mat in ((-0.462, 0.013, RUBBER), (-0.432, 0.011, CABLE_RED), (-0.406, 0.010, CABLE_BLUE)):
        m.cyl(mat, (tx, y0 + 0.006, zb + 0.03), (tx, y1 - 0.006, zb + 0.03), radius, 8, 0.0, None, (False, False))
    for y in (0.60, 0.93, 1.26, 1.52):
        m.box(ALU, (-0.434, y, zb + 0.024), (0.105, 0.02, 0.048), 0.003)
    m.box(ALU, (-0.434, y1 - 0.012, zb + 0.05), (0.14, 0.012, 0.09), 0.002)
    # feed loom above the upper rail, leaving a junction block on the trunk
    m.box(ALU, (-0.425, 1.744, zb + 0.036), (0.11, 0.10, 0.06), 0.006)
    for dy, radius, mat in ((0.0, 0.009, CABLE_RED), (0.021, 0.009, CABLE_BLUE), (0.043, 0.010, RUBBER)):
        m.cyl(mat, (-0.40, 1.722 + dy, zb + 0.04), (0.25, 1.722 + dy, zb + 0.04), radius, 8, 0.0, None, (False, True))
    for lx in (-0.06, 0.20):
        m.box(ALU, (lx, 1.744, zb + 0.032), (0.018, 0.088, 0.058), 0.003)

    # two rails of breakers
    top_y, bottom_y = 1.50, 0.98
    main_x = -0.21
    top_x = [-0.135 + BREAKER_W * i for i in range(8)]
    bottom_x = [-0.055 + BREAKER_W * j for j in range(9)]
    tripped = 4
    for y, xa, xb in ((top_y, -0.30, 0.30), (bottom_y, -0.12, 0.42)):
        m.box(ALU, ((xa + xb) / 2, y, zb + 0.006), (xb - xa, 0.035, 0.012), 0.002)
        for ex in (xa + 0.012, xb - 0.012):
            m.box(ANO_CLEAR, (ex, y, zb + 0.022), (0.012, 0.05, 0.03), 0.002)
    breaker(m, main_x, top_y, zb, 0.10)
    for i, x in enumerate(top_x):
        breaker(m, x, top_y, zb, BREAKER_W, True, i != tripped)
    for x in bottom_x:
        breaker(m, x, bottom_y, zb)
    m.decal(art.decals, "brk_main", (main_x, top_y + 0.022, zb + 0.086), None, width=0.10)
    m.decal(art.decals, "brk_row_a", ((top_x[0] + top_x[-1]) / 2, top_y + 0.022, zb + 0.086), None, width=0.40)
    m.decal(art.decals, "brk_row_b", ((bottom_x[0] + bottom_x[-1]) / 2, bottom_y + 0.022, zb + 0.086), None, width=0.45)

    # wiring: loom to the upper terminals, links between the rails, outgoing circuits swept to the trunk
    zw = zb + 0.045
    for i, x in enumerate([main_x] + top_x):
        m.tube(CABLE_RED if i % 2 == 0 else CABLE_BLUE,
               [(x, top_y + 0.078, zw), (x, top_y + 0.16, zw), (x - 0.03, top_y + 0.215, zb + 0.042), (x - 0.06, top_y + 0.235, zb + 0.04)],
               0.007 if i == 0 else 0.0045, 6, 0.03)
    colours = (CABLE_RED, CABLE_BLUE, CABLE_YELLOW)
    for i, x in enumerate(top_x):
        xb = bottom_x[i]
        m.tube(colours[i % 3], [(x, top_y - 0.078, zw), (x, top_y - 0.15, zw), ((x + xb) / 2, 1.24, zb + 0.03),
                                (xb, bottom_y + 0.15, zw), (xb, bottom_y + 0.078, zw)], 0.0045, 6, 0.04)
    m.tube(CABLE_RED, [(bottom_x[8], bottom_y + 0.078, zw), (bottom_x[8], 1.15, zw), (0.43, 1.32, zb + 0.035), (0.43, 1.62, zb + 0.035),
                       (0.25, 1.722, zb + 0.04)], 0.0055, 6, 0.05)
    m.tube(CABLE_RED, [(main_x, top_y - 0.078, zw), (main_x, 1.25, zw), (-0.25, 1.05, zb + 0.055), (-0.25, 0.935, zb + 0.055)],
           0.008, 8, 0.05)
    for j, x in enumerate(bottom_x):
        yy = 0.545 + 0.0105 * j
        m.tube((CABLE_BLUE, RUBBER, CABLE_RED)[j % 3],
               [(x, bottom_y - 0.078, zw), (x, bottom_y - 0.16, zw), (x - 0.04, 0.70, zb + 0.035), (x - 0.13, yy, zb + 0.03),
                (-0.395, yy, zb + 0.03)], 0.0045, 6, 0.04)
    for lx in (-0.30, 0.02):
        m.box(ALU, (lx, 0.587, zb + 0.022), (0.02, 0.115, 0.05), 0.003)

    # main isolator: yellow enclosure, the red rotary handle is a separate part
    ix, iy = -0.25, 0.80
    hy = iy + 0.0036
    m.box(YELLOW, (ix, iy, zb + 0.05), (0.22, 0.28, 0.10), 0.012)
    m.decal(art.decals, "isolator_face", (ix, iy - 0.004, zb + 0.10), None, width=0.19)
    m.tube(CABLE_RED, [(-0.432, iy, zb + 0.03), (-0.395, iy, zb + 0.045), (-0.355, iy, zb + 0.05)], 0.009, 8, 0.02)
    handle = Model("PowerCabinet_IsolatorHandle")
    handle.cyl(RED, (ix, hy, zb + 0.10), (ix, hy, zb + 0.118), 0.052, 20, 0.004)
    handle.box(RED, (ix, hy, zb + 0.133), (0.034, 0.118, 0.03), 0.008)
    handle.local_origin = (ix, hy, zb + 0.10)

    # amber work light under the head of the bay
    m.box(ALU, (0.31, y1 - 0.028, z - 0.06), (0.32, 0.03, 0.06), 0.004)
    m.box(WORKLIGHT, (0.31, y1 - 0.046, z - 0.045), (0.28, 0.012, 0.022), 0.0)
    m.decal(art.decals, "c_hv", (0.40, 0.75, zb), None, 0.9)
    m.decal(art.decals, "torque_note", (0.40, 0.645, zb), None, 0.9)

    lever = Model("PowerCabinet_TrippedLever")
    pivot = (top_x[tripped], top_y - 0.018, zb + 0.0875)
    lever.instance(breaker_lever(False), at(*pivot))
    lever.local_origin = pivot

    # close-out cover in its fitted position; the prefab keeps it inactive so the bay reads as open
    cover = Model("PowerCabinet_Cover")
    cz = z + 0.028
    cover.plate(WHITE, (0, cy, cz), 1.105, 1.425, 0.012, 0.10, 0.004, None, 5)
    cover.plate(WHITE, (0, cy, cz + 0.012), 0.86, 1.18, 0.006, 0.05, 0.004, None, 4)
    for fx, fy in ((-0.50, -0.66), (0.0, -0.66), (0.50, -0.66), (-0.50, 0.66), (0.0, 0.66), (0.50, 0.66),
                   (-0.50, -0.24), (0.50, -0.24), (-0.50, 0.24), (0.50, 0.24)):
        cover.cyl(ANO_GOLD, (fx, cy + fy, cz + 0.012), (fx, cy + fy, cz + 0.018), 0.011, 10, 0.002)
    for hx in (-0.30, 0.30):
        u_handle(cover, (hx, cy - 0.16), (hx, cy + 0.16), cz + 0.018, 0.030, 0.007, ALU)
    cover.decal(art.decals, "t_eps", (0, cy + 0.50, cz + 0.018), None, width=0.34)
    cover.decal(art.decals, "c_hv", (0, cy + 0.02, cz + 0.018), None, 0.9)
    cover.decal(art.decals, "hazard_long", (0, cy - 0.47, cz + 0.018), None, width=0.70)
    cover.decal(art.decals, "fastener_note", (0, cy - 0.52, cz + 0.018), None, 0.9)
    cover.local_origin = (0.0, cy, cz)

    # emergency lamp on the head of the rack; the lens is its own part so its material can be switched
    ax, ay = 0.61, 2.165
    m.box(ANO, (ax, ay, z + 0.009), (0.18, 0.09, 0.018), 0.004)
    for gx in (-0.045, 0.0, 0.045):
        m.tube(ANO_CLEAR, [(ax + gx, ay - 0.04, z + 0.018), (ax + gx, ay - 0.04, z + 0.062), (ax + gx, ay + 0.04, z + 0.062),
                           (ax + gx, ay + 0.04, z + 0.018)], 0.003, 6, 0.012)
    lens = Model("PowerCabinet_AlarmLens")
    lens.box(RED, (ax, ay, z + 0.036), (0.15, 0.062, 0.036), 0.012)
    lens.local_origin = (ax, ay, z + 0.018)

    parts = {"PowerCabinet_Cover": cover, "PowerCabinet_IsolatorHandle": handle, "PowerCabinet_TrippedLever": lever,
             "PowerCabinet_AlarmLens": lens}
    anchors = {"LightAnchor_PowerCabinetWork": (0.25, y1 - 0.14, z - 0.03), "LightAnchor_PowerCabinetAlarm": (ax, ay, z + 0.09)}
    return m, parts, anchors


def canister(art, label):
    """Replaceable CO2 sorbent canister, 0.76 m tall, origin at the centre of its base port."""
    m = Model("Canister")
    r = 0.10
    m.lathe(CHROME, (0, 0, 0), (0, 1, 0), [(0.030, 0.0), (0.030, 0.034), (0.052, 0.044), (0.052, 0.060), (0.086, 0.074),
                                           (0.104, 0.094), (0.104, 0.128)], 24, None, (True, False))
    m.cyl(PLASTIC, (0, 0.120, 0), (0, 0.640, 0), r, 24, 0.0, None, (False, False))
    m.lathe(CHROME, (0, 0, 0), (0, 1, 0), [(0.104, 0.632), (0.104, 0.666), (0.086, 0.686), (0.052, 0.700), (0.052, 0.716),
                                           (0.028, 0.726), (0.028, 0.760)], 24, None, (False, True))
    for y in (0.150, 0.610):
        m.cyl(CHROME, (0, y - 0.010, 0), (0, y + 0.010, 0), r + 0.005, 24, 0.002)
        m.box(CHROME, (0, y, r + 0.012), (0.034, 0.022, 0.024), 0.004, rotation((0, 1, 0), 42))
    curved_label(m, art.decals, label, (0, 0.40, 0), r, 0.115)
    return m


def eclss_rack(art):
    """Air revitalisation rack at the LifeSupportRepair anchor (asset A02): three replaceable sorbent
    canisters on a manifold in a recessed bay. Returns (rack, parts, anchors) like power_rack."""
    m = Model("EclssRack")
    opening = (-0.54, 0.52, 0.54, 1.84)
    x0, y0, x1, y1 = opening
    z, zb = rack_open(m, opening, 0.26)

    fascia(m, art, (-0.74, 1.955, 0.20, 2.25), z, "fascia_scrubber")
    # status display on the near corner of the bay, seated over the bezel
    m.box(GRAY, (0.44, 1.955, z + 0.03), (0.38, 0.30, 0.06), 0.012)
    display(m, art, (0.28, 1.85, 0.60, 2.06), z + 0.06, None, "sm_eclss", 4)
    m.decal(art.decals, "rack_10", (0.61, 2.22, z + 0.001), width=0.14)
    drawer(m, art, (-0.74, 0.10, 0.74, 0.41), z, "t_filter")

    # near column: insulated return duct in saddle clamps; far column: translation aid and caution
    m.cyl(RUBBER, (0.69, 0.44, z + 0.036), (0.69, 2.285, z + 0.036), 0.03, 12)
    for y in (0.58, 1.02, 1.46, 2.16):
        m.box(ALU, (0.69, y, z + 0.026), (0.095, 0.026, 0.056), 0.004)
    handrail(m, (-0.67, 1.00), (-0.67, 1.56), z)
    m.decal(art.decals, "c_press", (-0.672, 1.74, z + 0.001), None, 0.72)
    m.decal(art.decals, art.next("pn", 28), (-0.672, 0.52, z + 0.001), None, 0.9)

    cz = zb + 0.135                       # canister axis
    base_y = 0.80                         # height of the canister base ports
    xs = (-0.14, 0.11, 0.36)
    # inlet manifold along the bottom with a tee and union nut under every canister
    my = 0.70
    m.tube(CHROME, [(-0.44, y0 + 0.006, cz), (-0.44, my, cz), (0.46, my, cz)], 0.026, 12, 0.06)
    m.cyl(CHROME, (0.46, my, cz), (0.495, my, cz), 0.034, 12, 0.004)
    m.cyl(ALU, (0.47, my, cz + 0.02), (0.47, my, cz + 0.07), 0.008, 8)
    m.box(YELLOW, (0.445, my, cz + 0.076), (0.12, 0.024, 0.014), 0.005)
    for fx in (-0.30, -0.01, 0.235):
        m.cyl(CHROME, (fx - 0.014, my, cz), (fx + 0.014, my, cz), 0.036, 12, 0.004)
    for x in xs:
        m.cyl(CHROME, (x, my, cz), (x, base_y, cz), 0.03, 12)
        m.cyl(ALU, (x, base_y - 0.034, cz), (x, base_y - 0.004, cz), 0.042, 6, 0.004)
    # outlet header across the top, leaving through the head of the bay
    hy = base_y + 0.83
    m.tube(CHROME, [(xs[2], hy, cz), (-0.34, hy, cz), (-0.34, y1 - 0.006, cz)], 0.018, 10, 0.05)
    m.cyl(CHROME, (xs[2], hy, cz), (xs[2] + 0.03, hy, cz), 0.024, 10, 0.003)
    for x in xs:
        m.cyl(CHROME, (x, base_y + 0.76, cz), (x, hy, cz), 0.015, 10)
        m.cyl(ALU, (x, base_y + 0.765, cz), (x, base_y + 0.79, cz), 0.026, 6, 0.003)
    # saddle brackets behind the canisters
    for x in xs:
        for by in (base_y + 0.22, base_y + 0.54):
            m.box(ALU, (x, by, zb + 0.02), (0.17, 0.036, 0.04), 0.004)
    # far side of the bay: sample line with a valve, pressure gauge, sensor box
    m.cyl(ANO_GOLD, (-0.49, y0 + 0.006, zb + 0.04), (-0.49, y1 - 0.006, zb + 0.04), 0.011, 8, 0.0, None, (False, False))
    for by in (0.66, 1.08, 1.50):
        m.box(ALU, (-0.49, by, zb + 0.022), (0.04, 0.022, 0.044), 0.003)
    m.cyl(ALU, (-0.49, 1.24, zb + 0.04), (-0.49, 1.31, zb + 0.04), 0.02, 10, 0.003)
    m.cyl(ALU, (-0.49, 1.275, zb + 0.04), (-0.49, 1.275, zb + 0.085), 0.007, 8)
    m.box(ANO_BLUE, (-0.49, 1.275, zb + 0.09), (0.085, 0.02, 0.012), 0.004)
    gauge(m, art, -0.40, 1.52, zb, None, "gauge_cabin", 0.05)
    m.box(GRAY, (-0.40, 1.02, zb + 0.035), (0.12, 0.16, 0.07), 0.008)
    m.cyl(LED_C, (-0.40, 1.07, zb + 0.07), (-0.40, 1.07, zb + 0.073), 0.007, 8, 0.0, None, (False, True))
    m.decal(art.decals, "conn_08", (-0.40, 0.99, zb + 0.07), None, 0.9)
    m.tube(RUBBER, [(-0.40, 0.94, zb + 0.035), (-0.40, 0.86, zb + 0.035), (-0.44, 0.80, zb + 0.03)], 0.006, 6, 0.03)
    m.decal(art.decals, "airflow", (-0.015, base_y - 0.05, zb), None, 0.9)

    parts = {}
    for index, (x, tag) in enumerate(zip(xs, "abc")):
        part = Model("ScrubberCanister_" + tag.upper())
        part.instance(canister(art, "can_label_" + tag), at(x, base_y, cz))
        part.local_origin = (x, base_y, cz)
        parts[part.name] = part
    return m, parts, {}


def airlock_rack(art):
    """Umbilical interface and pressure control rack inside the airlock."""
    m = Model("AirlockRack")
    z = rack_shell(m)
    x0, x1 = -0.705, 0.705
    display(m, art, (x0 + 0.02, 1.90, x0 + 0.52, 2.21), z, None, "sm_airlock")
    switch_panel(m, art, (x0 + 0.56, 1.90, x1 - 0.02, 2.21), z, None, "leg_airlock")
    panel(m, (x0 + 0.02, 1.02, x1 - 0.02, 1.86), z, WHITE, 0.012)
    m.decal(art.decals, "t_uia", (0, 1.80, z + 0.012), width=0.34)
    gauge(m, art, -0.42, 1.52, z + 0.012, None, "gauge_airlock", 0.085)
    gauge(m, art, -0.12, 1.56, z + 0.012, None, "gauge_o2", 0.05)
    gauge(m, art, 0.06, 1.42, z + 0.012, None, "gauge_coolant", 0.045)
    # depress valve: large guarded handle
    m.cyl(ALU, (0.42, 1.48, z + 0.012), (0.42, 1.48, z + 0.05), 0.05, 16, 0.006)
    m.box(RED, (0.42, 1.48, z + 0.058), (0.20, 0.035, 0.016), 0.006)
    m.decal(art.decals, "t_depress", (0.42, 1.64, z + 0.012), width=0.24)
    m.decal(art.decals, "w_depress", (0.42, 1.28, z + 0.012), None, 0.8)
    m.decal(art.decals, "p_eva", (-0.34, 1.20, z + 0.012), None, 0.85)
    connectors(m, art, (x0 + 0.06, 1.04, x1 - 0.3, 1.14), z + 0.012, None, 4, 0.62, 8)
    oru(m, art, (x0 + 0.02, 0.56, -0.02, 0.98), z, None, "t_pump", "c_rotate")
    locker(m, art, (0.02, 0.56, x1 - 0.02, 0.98), z, None, "t_eva")
    vent(m, (x0 + 0.02, 0.12, x1 - 0.02, 0.52), z)
    m.decal(art.decals, "rack_22", (x1 - 0.1, 2.275, z - 0.001), width=0.14)
    return m


# ----------------------------------------------------------------------------- wall bays (front view, 1.5 m wide)
BAY_W = 1.44
BAY_Y0, BAY_Y1 = 0.16, 2.60


def bay_backing(m):
    m.box(WHITE, (0, (BAY_Y0 + BAY_Y1) / 2, 0.006), (1.50, BAY_Y1 - BAY_Y0, 0.012), 0.0)


def bay_blanket(art):
    """Quilted insulation liner with snaps, a translation aid and hook-and-loop patches."""
    m = Model("BayBlanket")
    bay_backing(m)
    h = BAY_Y1 - BAY_Y0
    cy = (BAY_Y0 + BAY_Y1) / 2
    m.box(QUILT, (0, cy, 0.03), (BAY_W, h - 0.06, 0.04), 0.016)
    for sx in (-1, 1):
        m.box(ALU, (sx * (BAY_W / 2 - 0.012), cy, 0.052), (0.02, h - 0.10, 0.004), 0.001)
    for y in np.linspace(BAY_Y0 + 0.12, BAY_Y1 - 0.12, 8):
        for sx in (-1, 1):
            m.cyl(ANO_CLEAR, (sx * (BAY_W / 2 - 0.012), y, 0.054), (sx * (BAY_W / 2 - 0.012), y, 0.058), 0.006, 8, 0.001)
    handrail(m, (-0.3, 1.12), (0.3, 1.12), 0.05)
    m.decal(art.decals, "blue_handrail", (0, 1.04, 0.051), None, 0.8)
    for _ in range(5):
        x = (art.rng.random() - 0.5) * (BAY_W - 0.3)
        y = BAY_Y0 + 0.3 + art.rng.random() * (h - 0.9)
        if abs(y - 1.12) > 0.12:
            m.decal(art.decals, "velcro_blue" if art.rng.random() < 0.6 else "velcro_white", (x, y, 0.051))
    return m


def bay_lockers(art, titles=("t_stow", "t_spares", "t_tools", "t_med"), columns=2, rows=4):
    m = Model("BayLockers")
    bay_backing(m)
    m.frame(ANO, (0, (BAY_Y0 + BAY_Y1) / 2, 0.012), BAY_W, BAY_Y1 - BAY_Y0 - 0.04, 0.03, 0.02, 0.012)
    cw = (BAY_W - 0.06) / columns
    rh = (BAY_Y1 - BAY_Y0 - 0.10) / rows
    index = 0
    for r in range(rows):
        for c in range(columns):
            x0 = -BAY_W / 2 + 0.03 + c * cw
            y0 = BAY_Y0 + 0.05 + r * rh
            locker(m, art, (x0, y0, x0 + cw, y0 + rh), 0.014, None, titles[index % len(titles)])
            index += 1
    return m


def bay_equipment(art, screen="sm_thermal", legend="leg_light", title="t_avionics", conn_start=4):
    """Avionics bay: display and switching on top, a removable unit, intake grille and connectors."""
    m = Model("BayEquipment")
    bay_backing(m)
    x0, x1 = -BAY_W / 2 + 0.02, BAY_W / 2 - 0.02
    m.frame(ANO, (0, (BAY_Y0 + BAY_Y1) / 2, 0.012), BAY_W, BAY_Y1 - BAY_Y0 - 0.04, 0.03, 0.02, 0.012)
    z = 0.014
    display(m, art, (x0 + 0.02, 2.14, x0 + 0.50, 2.50), z, None, screen)
    switch_panel(m, art, (x0 + 0.54, 2.30, x1 - 0.02, 2.50), z, None, legend)
    blank(m, art, (x0 + 0.54, 2.14, x1 - 0.02, 2.27), z)
    oru(m, art, (x0 + 0.02, 1.42, 0.0, 2.10), z, None, title, "c_esd")
    oru(m, art, (0.02, 1.42, x1 - 0.02, 2.10), z, None, "t_thermal", None)
    locker(m, art, (x0 + 0.02, 0.98, 0.0, 1.38), z, None, "t_filter")
    blank(m, art, (0.02, 0.98, x1 - 0.02, 1.38), z)
    gauge(m, art, 0.36, 1.19, z + 0.012, None, "gauge_coolant", 0.05)
    panel(m, (x0 + 0.02, 0.62, x1 - 0.02, 0.94), z, WHITE, 0.010)
    connectors(m, art, (x0 + 0.08, 0.70, x1 - 0.08, 0.86), z + 0.010, None, 7, 0.22, conn_start)
    vent(m, (x0 + 0.02, 0.22, x1 - 0.02, 0.58), z)
    handrail(m, (x0 + 0.06, 1.40), (x0 + 0.06, 2.0), 0.03)
    return m


def bay_stowage(art, first_bag=0, rows=3, title="t_stow"):
    """Soft stowage: cargo bags on shelves behind bungee restraints."""
    m = Model("BayStowage")
    bay_backing(m)
    m.frame(ALU, (0, (BAY_Y0 + BAY_Y1) / 2, 0.012), BAY_W, BAY_Y1 - BAY_Y0 - 0.04, 0.035, 0.11, 0.012)
    x0, x1 = -BAY_W / 2 + 0.05, BAY_W / 2 - 0.05
    rh = (BAY_Y1 - BAY_Y0 - 0.34) / rows
    bag = first_bag
    for r in range(rows):
        y0 = BAY_Y0 + 0.06 + r * rh
        m.box(ALU, (0, y0, 0.066), (x1 - x0, 0.012, 0.11), 0.002)
        count = 2 if r % 2 == 0 else 3
        gap = 0.02
        bw = (x1 - x0 - gap * (count + 1)) / count
        for i in range(count):
            cx = x0 + gap + bw / 2 + i * (bw + gap)
            bh = rh - 0.07 - art.rng.random() * 0.05
            ctb(m, art, (cx, y0 + 0.008 + bh / 2, 0.066), (bw, bh, 0.10 + art.rng.random() * 0.02), None, "bag_%02d" % (bag % 16))
            bag += 1
        for by in (y0 + rh * 0.35, y0 + rh * 0.7):
            m.tube(CABLE_BLUE, [(x0 - 0.01, by, 0.10), (x0 - 0.01, by, 0.135), (x1 + 0.01, by, 0.135), (x1 + 0.01, by, 0.10)],
                   0.004, 6, 0.012)
    m.decal(art.decals, title, (0, BAY_Y1 - 0.14, 0.014), width=0.34)
    m.decal(art.decals, "p_stow", (-0.42, BAY_Y1 - 0.15, 0.014), None, 0.6)
    return m


def bay_utility(art):
    """Exposed utilities: an air duct with flexible sections, fluid lines with valves, cable bundles."""
    m = Model("BayUtility")
    bay_backing(m)
    m.cyl(ALU, (-0.42, BAY_Y0 + 0.02, 0.10), (-0.42, BAY_Y1 - 0.02, 0.10), 0.085, 18, 0.004)
    for y in np.arange(0.9, 1.5, 0.045):
        m.cyl(RUBBER, (-0.42, y, 0.10), (-0.42, y + 0.022, 0.10), 0.092, 18, 0.006)
    for y in (0.55, 2.05):
        m.cyl(ALU, (-0.42, y - 0.02, 0.10), (-0.42, y + 0.02, 0.10), 0.095, 18, 0.004)
        m.box(ALU, (-0.42, y, 0.02), (0.24, 0.04, 0.04), 0.004)
    m.decal(art.decals, "airflow", (-0.42, 1.75, 0.186), None, 0.8)
    for i, (x, mat) in enumerate(((-0.12, ALU), (-0.02, ANO_BLUE), (0.08, ALU))):
        m.cyl(mat, (x, BAY_Y0 + 0.02, 0.05), (x, BAY_Y1 - 0.02, 0.05), 0.017, 10, 0.0)
        for y in (0.5, 1.3, 2.1):
            m.box(ALU, (x, y, 0.035), (0.05, 0.03, 0.05), 0.003)
        vy = 1.0 + i * 0.25
        m.cyl(ALU, (x, vy - 0.03, 0.05), (x, vy + 0.03, 0.05), 0.026, 10, 0.004)
        m.cyl(ALU, (x, vy, 0.05), (x, vy, 0.11), 0.008, 8)
        m.box(ANO_GOLD if i != 1 else ANO_BLUE, (x, vy, 0.115), (0.07, 0.016, 0.01), 0.003)
        m.decal(art.decals, "conn_%02d" % (8 + i), (x, vy + 0.07, 0.068), None, 0.7)
    for i, mat in enumerate((RUBBER, CABLE_ORANGE, RUBBER, CABLE_BLUE, RUBBER)):
        x = 0.30 + i * 0.045
        m.tube(mat, [(x, BAY_Y0 + 0.02, 0.03), (x, 1.1 + i * 0.03, 0.03), (x + 0.02, 1.25 + i * 0.03, 0.045),
                     (x + 0.02, BAY_Y1 - 0.02, 0.045)], 0.012, 8, 0.05)
    for y in (0.6, 1.5, 2.3):
        m.box(ANO, (0.39, y, 0.05), (0.27, 0.025, 0.012), 0.003)
        for bx in (0.27, 0.51):
            bolt(m, bx, y, 0.056, 0.005, 0.003)
    m.decal(art.decals, "c_press", (0.08, 0.36, 0.013), None, 0.7)
    m.decal(art.decals, "t_thermal", (0.0, 2.48, 0.013), width=0.3)
    return m


def bay_tools(art):
    """IVA tool board: shadowed, tethered hand tools on a perforated panel."""
    m = Model("BayTools")
    bay_backing(m)
    m.box(GRAY, (0, 1.42, 0.03), (1.25, 1.45, 0.036), 0.008)
    m.frame(ANO, (0, 1.42, 0.048), 1.25, 1.45, 0.025, 0.008, 0.012)
    m.decal(art.decals, "t_tools", (0, 2.08, 0.049), width=0.34)
    z = 0.05
    for i in range(6):
        x = -0.48 + i * 0.105
        m.box(ALU, (x, 1.62, z + 0.008), (0.022, 0.30 + i * 0.02, 0.008), 0.003)
        m.cyl(ALU, (x, 1.62 + 0.15 + i * 0.01, z), (x, 1.62 + 0.15 + i * 0.01, z + 0.016), 0.02, 8, 0.003)
        m.cyl(ALU, (x, 1.62 - 0.15 - i * 0.01, z), (x, 1.62 - 0.15 - i * 0.01, z + 0.016), 0.016, 8, 0.003)
    for i in range(5):
        x = 0.16 + i * 0.085
        m.cyl(ANO_GOLD if i % 2 == 0 else ANO_BLUE, (x, 1.52, z + 0.015), (x, 1.64, z + 0.015), 0.016, 10, 0.004)
        m.cyl(ALU, (x, 1.64, z + 0.015), (x, 1.80, z + 0.015), 0.005, 8)
    m.box(ANO, (-0.25, 1.05, z + 0.02), (0.30, 0.06, 0.04), 0.008)
    m.cyl(ALU, (-0.42, 1.05, z + 0.02), (-0.50, 1.05, z + 0.02), 0.022, 10, 0.004)
    m.box(ANO_GOLD, (0.25, 1.05, z + 0.025), (0.22, 0.12, 0.05), 0.012)
    m.cyl(ANO, (0.25, 1.11, z + 0.025), (0.25, 1.19, z + 0.025), 0.012, 8)
    for x in (-0.5, -0.25, 0.0, 0.25, 0.5):
        m.tube(CABLE_ORANGE, [(x, 0.95, z), (x, 0.88, z + 0.02), (x + 0.03, 0.80, z)], 0.003, 5, 0.02)
        m.cyl(ALU, (x + 0.03, 0.80, z - 0.002), (x + 0.03, 0.80, z + 0.008), 0.012, 8, 0.002)
    locker(m, art, (-0.62, 0.22, 0.0, 0.66), 0.014, None, "t_spares")
    locker(m, art, (0.0, 0.22, 0.62, 0.66), 0.014, None, "t_tools")
    m.decal(art.decals, "p_repair", (0.42, 2.38, 0.013), None, 0.75)
    m.decal(art.decals, "c_pinch", (-0.45, 2.38, 0.013), None, 0.7)
    return m


def bay_emergency(art):
    """Emergency equipment: extinguisher, breathing apparatus pouch, fire port and procedures."""
    m = Model("BayEmergency")
    bay_backing(m)
    m.box(WHITE, (0, 1.38, 0.02), (BAY_W, 2.36, 0.016), 0.006)
    z = 0.03
    m.cyl(RED, (-0.36, 0.62, z + 0.075), (-0.36, 1.14, z + 0.075), 0.07, 16, 0.012)
    m.cyl(ALU, (-0.36, 1.14, z + 0.075), (-0.36, 1.20, z + 0.075), 0.03, 10, 0.004)
    m.box(ANO, (-0.36, 1.22, z + 0.085), (0.11, 0.04, 0.06), 0.008)
    m.tube(RUBBER, [(-0.30, 1.22, z + 0.085), (-0.24, 1.22, z + 0.085), (-0.24, 0.90, z + 0.085)], 0.009, 8, 0.03)
    for y in (0.74, 1.02):
        m.box(ALU, (-0.36, y, z + 0.08), (0.17, 0.03, 0.16), 0.004)
    m.decal(art.decals, "w_extinguisher", (-0.36, 1.36, z - 0.001), None, 0.85)
    ctb(m, art, (0.30, 0.90, z + 0.07), (0.46, 0.50, 0.13), None, None)
    m.decal(art.decals, "w_emerg", (0.30, 1.26, z - 0.001), None, 0.85)
    m.cyl(ALU, (0.0, 1.62, z - 0.01), (0.0, 1.62, z + 0.012), 0.05, 16, 0.004)
    m.cyl(GRAPHITE, (0.0, 1.62, z + 0.012), (0.0, 1.62, z + 0.0125), 0.03, 16, 0.0, None, (False, True))
    m.decal(art.decals, "w_fire", (0.0, 1.76, z - 0.001), None, 0.8)
    m.decal(art.decals, "p_fire", (-0.40, 2.16, z - 0.001), None, 0.85)
    m.decal(art.decals, "p_egress", (0.40, 2.16, z - 0.001), None, 0.85)
    m.decal(art.decals, "hazard_red", (0, 0.36, z - 0.001), width=1.2)
    handrail(m, (-0.2, 0.30), (0.2, 0.30), z - 0.01)
    return m


def bay_service(art, mount=None):
    """Painted close-out beside a service rack. `mount` = (centre x, width, height, bottom y, title key) adds
    the plinth for a wall-mounted prop (the EPS-2 control panel or the ARS-1 unit) with its junction box."""
    m = Model("BayService")
    bay_backing(m)
    for cx in (-0.36, 0.36):
        panel(m, (cx - 0.34, BAY_Y0 + 0.04, cx + 0.34, BAY_Y1 - 0.04), 0.012, WHITE, 0.012, None, True, 0.03)
    if mount:
        mx, width, height, bottom, title = mount
        top = bottom + height
        m.box(GRAPHITE, (mx, bottom + height / 2, 0.029), (width + 0.06, height + 0.06, 0.010), 0.003)
        m.box(GRAY, (mx, 2.12, 0.06), (0.34, 0.22, 0.09), 0.01)
        m.decal(art.decals, title, (mx, 2.12, 0.105), None, width=0.28)
        for dx, mat in ((-0.08, RUBBER), (0.0, CABLE_RED), (0.08, RUBBER)):
            m.cyl(mat, (mx + dx, 2.23, 0.05), (mx + dx, BAY_Y1 - 0.02, 0.05), 0.014, 8, 0.0, None, (False, False))
            m.cyl(mat, (mx + dx, top - 0.005, 0.05), (mx + dx, 2.01, 0.05), 0.012, 8, 0.0, None, (False, False))
        m.box(ALU, (mx, 2.40, 0.04), (0.24, 0.024, 0.05), 0.003)
    else:
        handrail(m, (-0.42, 1.0), (-0.42, 1.6), 0.024)
        m.cyl(RUBBER, (-0.62, BAY_Y0 + 0.02, 0.045), (-0.62, BAY_Y1 - 0.02, 0.045), 0.018, 10, 0.0, None, (False, False))
        for y in (0.5, 1.3, 2.1):
            m.box(ALU, (-0.62, y, 0.036), (0.06, 0.024, 0.04), 0.003)
        m.decal(art.decals, art.next("pn", 28), (-0.30, 0.34, 0.024), None, 0.9)
    return m


# ----------------------------------------------------------------------------- structure
def wall_frame(side, width, z, inset=0.02):
    """Front-view frame on a side wall. side -1 is the -x wall (faces +x), +1 the +x wall."""
    return front_frame((side * (width / 2 - inset), 0.0, z), (-side, 0.0, 0.0))


def bay_layout(z0, z1):
    """Bay centres and interior ring-frame stations; bays divide the compartment evenly (1.5 to 2 m)."""
    length = z1 - z0
    count = max(1, int(round(length / RIB_PITCH)))
    pitch = length / count
    centres = [z0 + pitch * (i + 0.5) for i in range(count)]
    frames = [z0 + pitch * i for i in range(1, count)]
    return centres, frames


def ring_frame(m, width, rib_z):
    """Graphite-coated ring frame: web and flange up both walls, across the shoulders and the ceiling."""
    half = width / 2
    for side in (-1, 1):
        m.box(GRAPHITE, (side * (half - 0.105), 1.33, rib_z), (0.17, 2.66, 0.012), 0.002)
        m.box(GRAPHITE, (side * (half - 0.19), 1.33, rib_z), (0.012, 2.66, 0.07), 0.003)
        for y in np.arange(0.3, 2.6, 0.38):
            f = front_frame((side * (half - 0.196), y, rib_z), (-side, 0, 0))
            bolt(m, -0.022, 0, 0, 0.006, 0.004, ALU, f)
            bolt(m, 0.022, 0, 0, 0.006, 0.004, ALU, f)
        # shoulder: 45 degree member from the wall top to the ceiling beam
        a = np.array([side * (half - 0.19), 2.66])
        b = np.array([side * (half - 0.62), 3.06])
        mid = (a + b) / 2
        length = float(np.linalg.norm(b - a))
        angle = math.degrees(math.atan2(b[1] - a[1], b[0] - a[0]))
        placement = compose(at(mid[0], mid[1], rib_z), rotation((0, 0, 1), angle))
        m.box(GRAPHITE, (0, 0.04, 0), (length + 0.06, 0.012, 0.07), 0.003, placement)
        m.box(GRAPHITE, (0, 0.09, 0), (length + 0.10, 0.10, 0.012), 0.002, placement)
    m.box(GRAPHITE, (0, 3.02, rib_z), (width - 1.16, 0.012, 0.07), 0.003)
    m.box(GRAPHITE, (0, 3.07, rib_z), (width - 1.16, 0.10, 0.012), 0.002)


def deck(m, art, width, z0, z1, deck_ids=True):
    half = width / 2
    length = z1 - z0
    zc = (z0 + z1) / 2
    m.box(GRAPHITE, (0, -0.03, zc), (width, 0.036, length), 0.0)
    count = int(round(length / 0.8))
    step = length / count
    for i in range(count):
        z = z0 + step * (i + 0.5)
        # lift-out aisle panel: painted frame around a perforated anti-slip grate
        m.box(GRAY, (0, -0.007, z), (1.46, 0.012, step - 0.02), 0.003)
        m.box(GRATE, (0, -0.0045, z), (1.36, 0.012, step - 0.11), 0.0)
        for bx in (-0.705, 0.0, 0.705):
            for bz in (-step / 2 + 0.032, step / 2 - 0.032):
                m.cyl(ANO_CLEAR, (bx, -0.002, z + bz), (bx, 0.0, z + bz), 0.008, 8, 0.0, None, (False, True))
    for side in (-1, 1):
        m.box(ANO, (side * 0.77, -0.004, zc), (0.045, 0.011, length - 0.04), 0.002)
        for z in np.arange(z0 + 0.2, z1 - 0.1, 0.4):
            m.cyl(ALU, (side * 0.77, 0.0, z), (side * 0.77, 0.0022, z), 0.008, 6, 0.0, None, (False, True))
        outer = half - 0.14
        sw = outer - 0.81
        bays = int(round(length / RIB_PITCH))
        bstep = length / bays
        for i in range(bays):
            z = z0 + bstep * (i + 0.5)
            cx = side * (0.81 + sw / 2)
            m.box(DECK, (cx, -0.006, z), (sw - 0.012, 0.012, bstep - 0.02), 0.003)
            m.box(GRAPHITE, (cx, 0.0004, z + bstep * 0.3), (0.12, 0.002, 0.028), 0.0)
            for bx in (-sw / 2 + 0.05, sw / 2 - 0.05):
                for bz in (-bstep / 2 + 0.06, bstep / 2 - 0.06):
                    m.cyl(ANO_CLEAR, (cx + bx, -0.001, z + bz), (cx + bx, 0.0008, z + bz), 0.008, 8, 0.0, None, (False, True))
            if deck_ids and side < 0 and i % 2 == 0:
                f = front_frame((cx, 0.0, z - bstep * 0.15), (0, 1, 0), (0, 0, 1))
                m.decal(art.decals, art.next("deck", 8), (0, 0, 0), f, 0.9)
        # cable trough and kick strip along the wall
        m.box(GRAPHITE, (side * (half - 0.08), 0.06, zc), (0.10, 0.12, length), 0.004)
        m.box(ALU, (side * (half - 0.135), 0.118, zc), (0.012, 0.006, length - 0.02), 0.001)


def ceiling(m, art, width, z0, z1, fixtures):
    half = width / 2
    length = z1 - z0
    zc = (z0 + z1) / 2
    m.box(WHITE, (0, CEILING + 0.01, zc), (width - 0.9, 0.02, length), 0.0)
    for side in (-1, 1):
        # shoulder liner: quilted blanket on the 45 degree standoff
        a = np.array([side * (half - 0.02), 2.64])
        b = np.array([side * (half - 0.48), CEILING - 0.02])
        mid = (a + b) / 2
        normal = np.array([-(b[1] - a[1]) * side, (b[0] - a[0]) * side, 0.0])
        normal = normal / np.linalg.norm(normal) * (-1.0 if normal[1] > 0 else 1.0)
        along = np.array([b[0] - a[0], b[1] - a[1], 0.0])
        f = front_frame((mid[0], mid[1], zc), normal, along)
        m.box(QUILT, (0, 0, -0.012), (length, float(np.linalg.norm(b - a)), 0.03), 0.01, f)
        # cable tray under the ceiling with a bundle of cables and tie bars
        tx = side * 0.9
        m.box(ALU, (tx, 3.012, zc), (0.24, 0.006, length - 0.02), 0.001)
        for lip in (-0.12, 0.12):
            m.box(ALU, (tx + lip, 3.035, zc), (0.006, 0.05, length - 0.02), 0.001)
        for i, (dx, radius, mat) in enumerate(((-0.088, 0.013, RUBBER), (-0.055, 0.012, CABLE_RED), (-0.02, 0.016, RUBBER),
                                               (0.015, 0.011, CABLE_YELLOW), (0.045, 0.011, CABLE_BLUE),
                                               (0.082, 0.014, RUBBER))):
            m.cyl(mat, (tx + dx, 3.016 + radius, z0 + 0.01), (tx + dx, 3.016 + radius, z1 - 0.01), radius, 8, 0.0, None, (False, False))
        for z in np.arange(z0 + 0.4, z1, 0.8):
            m.box(ANO, (tx, 3.058, z), (0.25, 0.006, 0.03), 0.001)
    # service runs outboard of the trays, resting on trapeze hangers: coolant, gas lines, insulated air duct
    rest = 2.909
    for x, radius, mat in ((-1.18, 0.032, ALU), (-1.29, 0.014, ANO_BLUE), (1.42, 0.045, FAB), (1.28, 0.014, ANO_GOLD),
                           (1.19, 0.022, ALU)):
        segs = 12 if radius > 0.02 else 8
        m.cyl(mat, (x, rest + radius, z0 + 0.01), (x, rest + radius, z1 - 0.01), radius, segs, 0.0, None, (False, False))
        for z in np.arange(z0 + 0.8, z1 - 0.1, 1.6):
            m.cyl(ALU, (x, rest + radius, z - 0.02), (x, rest + radius, z + 0.02), radius + 0.009, segs, 0.003)
        if mat == FAB:
            for z in np.arange(z0 + 0.3, z1 - 0.1, 0.5):
                m.cyl(ALU, (x, rest + radius, z - 0.008), (x, rest + radius, z + 0.008), radius + 0.003, segs, 0.0, None,
                      (False, False))
    for z in np.arange(z0 + 0.2, z1, 0.8):
        for xa, xb in ((-1.33, -1.12), (1.14, 1.49)):
            m.box(ALU, ((xa + xb) / 2, rest - 0.004, z), (xb - xa, 0.008, 0.03), 0.001)
            for hx in (xa + 0.01, xb - 0.01):
                m.cyl(ALU, (hx, rest - 0.004, z), (hx, CEILING - 0.01, z), 0.004, 6, 0.0, None, (False, False))
    for z in fixtures:
        luminaire(m, 0.0, z)
    # air diffusers and access panels in the bays without a luminaire
    for z in bay_layout(z0, z1)[0]:
        if min(abs(z - fz) for fz in fixtures) < 0.75:
            continue
        f = front_frame((0, CEILING, z), (0, -1, 0), (0, 0, 1))
        m.frame(WHITE, (0, 0, 0), 0.62, 0.62, 0.05, 0.02, 0.03, f)
        for i, size in enumerate((0.50, 0.38, 0.26)):
            m.frame(ANO_CLEAR, (0, 0, 0.004 + i * 0.006), size, size, 0.035, 0.008, 0.02, f)
        m.box(GRAPHITE, (0, 0, 0.002), (0.5, 0.5, 0.004), 0.0, f)
        m.cyl(WHITE, (0.45, CEILING, z + 0.3), (0.45, CEILING - 0.035, z + 0.3), 0.055, 14, 0.008)
        m.cyl(LED_G, (0.45, CEILING - 0.035, z + 0.3), (0.45, CEILING - 0.037, z + 0.3), 0.006, 8, 0.0, None, (False, True))


def portal(m, art, width, z, sign, emergency_side=None):
    """Pressure bulkhead with a 1.8 x 2.5 m hatchway: wings, lintel, machined hatch land, handrails."""
    half = width / 2
    wing = half - 1.04
    for side in (-1, 1):
        cx = side * (1.04 + wing / 2)
        m.box(WHITE, (cx, 1.56, z), (wing, 3.12, 0.16), 0.004)
        for face in (-1, 1):
            f = front_frame((cx, 0, z + face * 0.08), (0, 0, face))
            for sy in (0.9, 1.9):
                m.box(ALU, (0, sy, 0.012), (wing - 0.06, 0.04, 0.024), 0.004, f)
            m.box(ALU, (0, 1.4, 0.008), (0.04, 2.6, 0.016), 0.004, f)
            rail_x = (0.9 + 0.2 - (1.04 + wing / 2)) * (-side if face > 0 else side)
            handrail(m, (rail_x, 1.0), (rail_x, 1.6), 0.0, f)
    m.box(WHITE, (0, 2.81, z), (width, 0.62, 0.16), 0.004)
    for face in (-1, 1):
        f = front_frame((0, 0, z + face * 0.08), (0, 0, face))
        # machined sealing land: square outside, radiused inside at the top corners only
        m.frame(ALU, (0, 1.24, 0.0), 2.08, 2.80, 0.14, 0.035, 0.30, f, 5)
        m.frame(RUBBER, (0, 1.24, 0.030), 1.86, 2.58, 0.03, 0.012, 0.19, f, 5)
        m.frame(AMBER, (0, 1.24, 0.035), 1.95, 2.67, 0.028, 0.002, 0.235, f, 5)
        for bx in np.linspace(-0.97, 0.97, 9):
            bolt(m, bx, 2.57, 0.035, 0.008, 0.006, ALU, f)
        for by in np.linspace(0.2, 2.3, 8):
            for bx in (-0.97, 0.97):
                bolt(m, bx, by, 0.035, 0.008, 0.006, ALU, f)
        m.decal(art.decals, sign, (0, 2.84, 0.0), f, 1.0)
        m.decal(art.decals, "exit_right" if face > 0 else "exit_left", (0.72 * (1 if face > 0 else -1), 2.69, 0.0), f, 0.8)
        m.decal(art.decals, "c_keepclear", (1.42, 1.72, 0.0), f, 0.8)
        for hx in (-1.0, 1.0):
            m.box(ANO, (hx, 0.75, 0.05), (0.08, 0.16, 0.03), 0.008, f)
            m.cyl(ALU, (hx, 0.67, 0.07), (hx, 0.83, 0.07), 0.014, 10, 0.003, f)
        m.decal(art.decals, "hazard_long", (0, 2.535, 0.0), f, width=1.6)
    # cable grommets where the overhead trays pass through the lintel
    for tx in (-0.9, 0.9):
        m.cyl(RUBBER, (tx, 3.03, z - 0.09), (tx, 3.03, z + 0.09), 0.07, 14, 0.008)
    if emergency_side is not None:
        f = front_frame((emergency_side * (1.04 + wing / 2), 0, z - 0.08), (0, 0, -1))
        m.decal(art.decals, "emblem", (0, 1.75, 0.0), f, 1.0)


def build_shell(m, art, name, left_bays, right_bays, entrance_sign=None):
    room = ROOMS[name]
    width, z0, z1 = room["width"], room["z0"], room["z1"]
    half = width / 2
    deck(m, art, width, z0, z1)
    ceiling(m, art, width, z0, z1, room["fixtures"])
    zc = (z0 + z1) / 2
    for side in (-1, 1):
        m.box(WHITE, (side * (half - 0.01), 1.33, zc), (0.02, 2.66, z1 - z0), 0.0)
    centres, frames = bay_layout(z0, z1)
    for frame_z in frames:
        ring_frame(m, width, frame_z)
    for index, bay_z in enumerate(centres):
        for side, bays in ((-1, left_bays), (1, right_bays)):
            bay = bays[index] if index < len(bays) else None
            if bay is not None:
                m.instance(bay, wall_frame(side, width, bay_z))
    if entrance_sign:
        portal(m, art, width, z0, entrance_sign)


# ----------------------------------------------------------------------------- cockpit
def seat(art, label):
    """Crew seat, occupant facing +z in front-view space. Frame, cushions, harness, controllers."""
    m = Model("Seat")
    for sx in (-1, 1):
        m.box(ANO, (sx * 0.24, 0.015, 0.0), (0.05, 0.03, 0.9), 0.004)
        for z in np.arange(-0.4, 0.41, 0.1):
            m.cyl(GRAPHITE, (sx * 0.24, 0.03, z), (sx * 0.24, 0.0305, z), 0.008, 6, 0.0, None, (False, True))
        m.tube(ALU, [(sx * 0.24, 0.03, 0.30), (sx * 0.22, 0.42, 0.20)], 0.018, 10)
        m.tube(ALU, [(sx * 0.24, 0.03, -0.30), (sx * 0.22, 0.42, -0.20)], 0.018, 10)
        m.tube(ALU, [(sx * 0.24, 0.03, -0.30), (sx * 0.22, 0.42, 0.20)], 0.010, 8)
    m.cyl(ALU, (0, 0.05, -0.36), (0, 0.45, -0.37), 0.03, 12, 0.004)
    m.cyl(ANO_CLEAR, (0, 0.45, -0.37), (0, 0.86, -0.385), 0.018, 10, 0.002)
    m.box(ALU, (0, 0.435, 0.02), (0.50, 0.03, 0.50), 0.008)
    m.box(FAB_DARK, (0, 0.492, 0.03), (0.46, 0.075, 0.46), 0.025)
    back = compose(at(0, 0.47, -0.24), rotation((1, 0, 0), -12))
    m.box(ALU, (0, 0.44, -0.025), (0.50, 0.90, 0.03), 0.008, back)
    m.box(FAB_DARK, (0, 0.22, 0.03), (0.44, 0.38, 0.08), 0.03, back)
    m.box(FAB_DARK, (0, 0.63, 0.03), (0.44, 0.40, 0.08), 0.03, back)
    m.box(FAB_DARK, (0, 0.97, 0.035), (0.28, 0.20, 0.09), 0.035, back)
    for sx in (-1, 1):
        m.box(FAB_DARK, (sx * 0.17, 0.97, 0.07), (0.05, 0.18, 0.12), 0.02, back)
        m.box(STRAP, (sx * 0.10, 0.52, 0.073), (0.045, 0.62, 0.005), 0.0, back)
        m.box(ALU, (sx * 0.10, 0.84, 0.076), (0.05, 0.035, 0.008), 0.002, back)
        m.box(STRAP, (sx * 0.15, 0.535, 0.12), (0.20, 0.006, 0.045), 0.0)
        # armrest with a controller at its front end
        m.cyl(ALU, (sx * 0.30, 0.44, -0.08), (sx * 0.30, 0.66, -0.08), 0.014, 8)
        m.box(ALU, (sx * 0.30, 0.67, 0.03), (0.06, 0.02, 0.34), 0.006)
        m.box(FAB_DARK, (sx * 0.30, 0.69, 0.0), (0.065, 0.025, 0.24), 0.01)
        m.box(ANO, (sx * 0.30, 0.70, 0.19), (0.07, 0.045, 0.07), 0.01)
        m.cyl(RUBBER, (sx * 0.30, 0.72, 0.19), (sx * 0.30, 0.755, 0.19), 0.018, 10, 0.004)
        m.cyl(ANO_CLEAR, (sx * 0.30, 0.755, 0.19), (sx * 0.30, 0.80, 0.195), 0.009, 8)
        m.box(ANO, (sx * 0.30, 0.845, 0.198), (0.034, 0.09, 0.036), 0.012)
        m.cyl(RED if sx > 0 else ANO_GOLD, (sx * 0.30, 0.89, 0.198), (sx * 0.30, 0.897, 0.198), 0.008, 8, 0.002)
    m.cyl(ALU, (0, 0.535, 0.17), (0, 0.548, 0.17), 0.035, 14, 0.004)
    m.box(STRAP, (0, 0.50, 0.24), (0.04, 0.006, 0.16), 0.0)
    m.box(ALU, (0, 0.16, 0.44), (0.42, 0.02, 0.18), 0.006)
    for sx in (-1, 1):
        m.tube(ALU, [(sx * 0.19, 0.16, 0.40), (sx * 0.22, 0.40, 0.26)], 0.012, 8)
    plate_frame = compose(at(0, 0.47, -0.24), rotation((1, 0, 0), -12), at(0, 0.62, -0.041), rotation((0, 1, 0), 180))
    m.decal(art.decals, label, (0, 0, 0), plate_frame, 1.0)
    return m


def console(art):
    """Flight console, viewed from the crew side: cabinets, work surface, three displays, keyboards."""
    m = Model("Console")
    for cx, title in ((-0.98, "t_gnc"), (0.0, "t_avionics"), (0.98, "t_comm")):
        m.box(GRAY, (cx, 0.49, 0.0), (0.95, 0.86, 0.66), 0.01)
        z = 0.331
        if cx == 0.0:
            oru(m, art, (cx - 0.42, 0.34, cx + 0.42, 0.88), z, None, title, "c_esd", 0.02)
            connectors(m, art, (cx - 0.38, 0.12, cx + 0.38, 0.30), z, None, 6, 0.06, 4)
        else:
            vent(m, (cx - 0.42, 0.12, cx + 0.42, 0.44), z)
            locker(m, art, (cx - 0.44, 0.47, cx + 0.44, 0.89), z, None, title)
    m.box(GRAPHITE, (0, 0.035, 0.0), (2.9, 0.07, 0.56), 0.0)
    m.box(GRAPHITE, (0, 0.945, 0.0), (3.1, 0.05, 0.85), 0.012)
    m.box(FAB_DARK, (0, 0.975, 0.33), (2.7, 0.014, 0.11), 0.006)
    handrail(m, (-1.3, 0.955), (1.3, 0.955), 0.425)
    for kx in (-0.74, 0.74):
        kb = compose(at(kx, 0.975, 0.14), rotation((1, 0, 0), 9))
        m.box(ANO, (0, 0.008, 0), (0.38, 0.016, 0.15), 0.004, kb)
        for row in range(5):
            for col in range(14):
                m.box(GRAPHITE, (-0.169 + col * 0.026, 0.02, -0.052 + row * 0.026), (0.021, 0.008, 0.021), 0.002, kb)
        m.box(ANO, (kx + (0.28 if kx < 0 else -0.28), 0.985, 0.14), (0.10, 0.02, 0.12), 0.008)
        m.sphere(ALU, (kx + (0.28 if kx < 0 else -0.28), 1.003, 0.14), 0.024, 12, 8)
    for i in range(4):
        x = -0.12 + i * 0.08
        m.cyl(ANO, (x, 0.97, 0.16), (x, 0.995, 0.16), 0.016, 12, 0.003)
        m.box(ANO_CLEAR, (x, 0.9965, 0.167), (0.004, 0.003, 0.014), 0.0)
    m.decal(art.decals, "leg_gnc", (0.04, 0.9705, 0.22), compose(at(0, 0, 0), rotation((1, 0, 0), -90)), 0.9)
    # instrument panel, leaning back 15 degrees
    p = compose(at(0, 0.97, -0.17), rotation((1, 0, 0), -15))
    m.box(GRAPHITE, (0, 0.27, -0.035), (3.0, 0.56, 0.07), 0.012, p)
    for cx, key in ((-0.98, "mfd_systems"), (0.0, "mfd_flight"), (0.98, "mfd_comm")):
        m.frame(ANO, (cx, 0.27, 0.0), 0.72, 0.47, 0.03, 0.022, 0.016, p)
        m.screen(art.screens, key, (cx, 0.285, 0.004), 0.64, 0.40, p)
        m.box(GRAPHITE, (cx, 0.27, 0.001), (0.66, 0.42, 0.002), 0.0, p)
        for i in range(8):
            m.box(ANO_CLEAR, (cx - 0.28 + i * 0.08, 0.052, 0.024), (0.04, 0.014, 0.005), 0.001, p)
        for i in range(5):
            for sx in (-1, 1):
                m.box(ANO_CLEAR, (cx + sx * 0.344, 0.13 + i * 0.07, 0.024), (0.012, 0.03, 0.005), 0.001, p)
    for cx in (-0.49, 0.49):
        m.box(ANO, (cx, 0.27, 0.006), (0.16, 0.44, 0.012), 0.004, p)
        for row in range(6):
            for col in range(2):
                lit = art.rng.random() < 0.22
                m.box(LED_A if lit else GRAPHITE, (cx - 0.035 + col * 0.07, 0.10 + row * 0.066, 0.014),
                      (0.055, 0.04, 0.004), 0.001, p)
    m.box(FAB_DARK, (0, 0.575, 0.09), (3.1, 0.03, 0.30), 0.012, p)
    m.box(GRAPHITE, (0, 0.55, -0.03), (3.1, 0.06, 0.08), 0.01, p)
    return m


def forward_bulkhead(m, art):
    """Five-pane observation window in a bolted frame, overhead breaker panel, sill handrail."""
    f = front_frame((0, 0, -2.975), (0, 0, 1))
    panes = [(-1.82, 0.74), (-0.94, 0.74), (0.0, 0.86), (0.94, 0.74), (1.82, 0.74)]
    y0, y1 = 1.22, 2.78
    m.box(WHITE, (0, y0 / 2, -0.04), (6.0, y0, 0.08), 0.0, f)
    m.box(WHITE, (0, (y1 + CEILING) / 2, -0.04), (6.0, CEILING - y1, 0.08), 0.0, f)
    for sx in (-1, 1):
        m.box(WHITE, (sx * (2.19 + 0.405), (y0 + y1) / 2, -0.04), (0.81, y1 - y0, 0.08), 0.0, f)
    edges = [-2.19]
    for cx, w in panes:
        edges.append(cx + w / 2)
    for i in range(len(panes) - 1):
        left = panes[i][0] + panes[i][1] / 2
        right = panes[i + 1][0] - panes[i + 1][1] / 2
        m.box(WHITE, ((left + right) / 2, (y0 + y1) / 2, -0.02), (right - left, y1 - y0, 0.12), 0.006, f)
    from a7kit import ring, rounded_rect, plate
    for cx, w in panes:
        cy, h = (y0 + y1) / 2, y1 - y0
        v, t = ring(rounded_rect(w + 0.02, h + 0.02, 0.012, 5), rounded_rect(w - 0.10, h - 0.10, 0.16, 5), 0.05)
        m.add(ALU, v + vec(cx, cy, 0.0), t, f)
        v, t = ring(rounded_rect(w - 0.10, h - 0.10, 0.16, 5), rounded_rect(w - 0.15, h - 0.15, 0.14, 5), 0.03)
        m.add(RUBBER, v + vec(cx, cy, 0.0), t, f)
        v, t = plate(rounded_rect(w - 0.12, h - 0.12, 0.15, 5), 0.012, 0.0, True)
        m.add(GLASS, v + vec(cx, cy, -0.004), t, f)
        for bx in np.linspace(-w / 2 + 0.08, w / 2 - 0.08, 5):
            for by in (cy - h / 2 + 0.035, cy + h / 2 - 0.035):
                bolt(m, cx + bx, by, 0.05, 0.007, 0.005, ALU, f)
        for by in np.linspace(cy - h / 2 + 0.2, cy + h / 2 - 0.2, 6):
            for bx in (-w / 2 + 0.035, w / 2 - 0.035):
                bolt(m, cx + bx, by, 0.05, 0.007, 0.005, ALU, f)
    m.decal(art.decals, "stencil_aurora", (0, 2.955, 0.001), f, 1.0)
    m.decal(art.decals, "stencil_vessel", (0, 1.13, 0.001), f, 1.0)
    handrail(m, (-1.6, 1.16), (-0.5, 1.16), 0.0, f)
    handrail(m, (0.5, 1.16), (1.6, 1.16), 0.0, f)
    # overhead circuit breaker panel, tilted down towards the crew
    o = compose(f, at(0, 2.90, 0.10), rotation((1, 0, 0), 28))
    for cx, legend in ((-1.75, "leg_power"), (-0.9, "leg_eclss"), (0.9, "leg_comm"), (1.75, "leg_light")):
        m.box(GRAPHITE, (cx, 0.0, 0.0), (0.74, 0.26, 0.03), 0.008, o)
        m.decal(art.decals, legend, (cx, 0.095, 0.0155), o, 1.0)
        for row in range(2):
            for col in range(8):
                x = cx - 0.30 + col * 0.086
                y = -0.075 + row * 0.085
                m.cyl(ANO, (x, y, 0.015), (x, y, 0.023), 0.012, 8, 0.002, o)
                m.cyl(WHITE if art.rng.random() < 0.9 else YELLOW, (x, y, 0.023), (x, y, 0.035), 0.007, 8, 0.001, o)
    for sx in (-1, 1):
        g = front_frame((sx * 2.62, 0, -2.975), (0, 0, 1))
        gauge(m, art, 0, 2.2, 0.0, g, "gauge_cabin", 0.07)
        gauge(m, art, 0, 2.0, 0.0, g, "gauge_o2", 0.05)
        m.decal(art.decals, "t_o2_stored", (0, 1.88, 0.0), g, 0.8)
        m.box(GRAPHITE, (0, 1.62, 0.02), (0.36, 0.26, 0.04), 0.008, g)
        m.frame(ANO_CLEAR, (0, 1.62, 0.04), 0.30, 0.20, 0.012, 0.004, 0.006, g)
        for i in range(9):
            m.box(ANO, (0, 1.545 + i * 0.019, 0.042), (0.27, 0.004, 0.004), 0.0, g)


def build_cockpit(art, bays):
    m = Model("Cockpit")
    left = [bays["equipment_gnc"], bays["lockers"], bays["blanket"], bays["emergency"]]
    right = [bays["equipment_comm"], bays["stowage_a"], bays["lockers"], bays["blanket"]]
    build_shell(m, art, "Cockpit", left, right)
    forward_bulkhead(m, art)
    m.instance(console(art), front_frame((0, 0, -1.95), (0, 0, 1)))
    m.instance(seat(art, "seat_cmd"), front_frame((-1.15, 0, -0.45), (0, 0, -1)))
    m.instance(seat(art, "seat_fe"), front_frame((1.15, 0, -0.45), (0, 0, -1)))
    return m


# ----------------------------------------------------------------------------- separately addressable parts
def place_parts(frame, local_parts, local_anchors, group):
    """Moves parts and helper points from an assembly's own space into ship space and tags their group."""
    placed = {}
    for name, part in local_parts.items():
        model = Model(name)
        model.instance(part, frame)
        model.origin = tuple(float(c) for c in (frame @ np.append(vec(part.local_origin), 1.0))[:3])
        model.group = group
        placed[name] = model
    for name, point in local_anchors.items():
        ANCHORS[name] = (group, tuple(float(c) for c in (frame @ np.append(vec(point), 1.0))[:3]))
    return placed


# ----------------------------------------------------------------------------- engineering
def tank_rack(art):
    """Three composite pressure vessels in a restraint frame (oxygen and water reserves)."""
    m = Model("TankRack")
    from a7kit import capsule_tank
    for i in range(3):
        x = -0.58 + i * 0.58
        v, t = capsule_tank(0.185, 1.50, 22, 6)
        m.add(CARBON, v + vec(x, 0.34, 0.0), t)
        m.cyl(ALU, (x, 1.83, 0.0), (x, 1.92, 0.0), 0.045, 12, 0.006)
        m.cyl(ANO_BLUE if i < 2 else ALU, (x, 1.92, 0.0), (x, 1.96, 0.0), 0.03, 12, 0.004)
        m.box(ANO_GOLD, (x, 1.985, 0.0), (0.10, 0.02, 0.02), 0.006)
        m.cyl(ALU, (x, 0.26, 0.0), (x, 0.36, 0.0), 0.05, 12, 0.006)
        for by in (0.72, 1.46):
            m.cyl(ALU, (x, by - 0.02, 0.0), (x, by + 0.02, 0.0), 0.192, 22, 0.004)
        m.decal(art.decals, "t_o2" if i < 2 else "t_h2o", (x, 1.14, 0.187), None, width=0.22)
        m.decal(art.decals, "c_o2" if i < 2 else "c_press", (x, 0.98, 0.187), None, 0.55)
        m.tube(ALU, [(x, 1.96, 0.0), (x, 2.06, 0.0), (x, 2.06, -0.14)], 0.008, 8, 0.04)
    for by in (0.72, 1.46):
        m.box(ALU, (0, by, -0.13), (1.74, 0.05, 0.03), 0.004)
        m.box(ALU, (0, by, 0.20), (1.74, 0.03, 0.006), 0.002)
        for sx in (-1, 1):
            m.box(ALU, (sx * 0.87, by, 0.03), (0.02, 0.05, 0.36), 0.004)
    m.box(ALU, (0, 0.24, 0.0), (1.74, 0.04, 0.42), 0.006)
    for sx in (-1, 1):
        m.box(ALU, (sx * 0.86, 0.12, 0.0), (0.03, 0.24, 0.40), 0.004)
    m.tube(ALU, [(-0.58, 2.06, -0.14), (0.86, 2.06, -0.14), (0.86, 2.30, -0.14)], 0.008, 8, 0.04)
    gauge(m, art, 0.86, 2.14, -0.10, None, "gauge_o2", 0.05)
    return m


def build_engineering(art, bays):
    """Returns the static compartment and its separately addressable parts (ship coordinates)."""
    m = Model("Engineering")
    left = [bays["equipment_thermal"], bays["closeout"], bays["service"], bays["tools"], bays["lockers_b"]]
    right = [bays["utility"], bays["blanket"], bays["service_ars"], bays["closeout"], bays["equipment_science"]]
    build_shell(m, art, "Engineering", left, right, "sign_engineering")
    parts = {}
    for (body, rack_parts, anchors), frame in ((power_rack(art), front_frame((-2.02, 0, 6.1), (1, 0, 0))),
                                               (eclss_rack(art), front_frame((2.02, 0, 8.1), (-1, 0, 0)))):
        m.instance(body, frame)
        parts.update(place_parts(frame, rack_parts, anchors, "Parts"))
    m.instance(tank_rack(art), front_frame((1.99, 0, 5.43), (-1, 0, 0)))
    # drops from the overhead services: feeder conduit and bus cables to the EPS rack, return duct to the scrubber
    m.tube(RUBBER, [(-1.698, 2.26, 5.43), (-1.698, 2.66, 5.43), (-1.42, 2.84, 5.43), (-0.94, 2.875, 5.43), (-0.94, 3.02, 5.43)],
           0.024, 12, 0.10)
    for k, mat in enumerate((CABLE_RED, RUBBER, CABLE_BLUE)):
        zc = 6.35 + 0.045 * k
        tx = -0.90 + 0.03 * (k - 1)
        m.tube(mat, [(-2.08, 2.29, zc), (-2.08, 2.62, zc), (-1.50, 2.80 + 0.012 * k, zc), (tx, 2.86, zc), (tx, 3.02, zc)],
               0.011, 8, 0.10)
    m.tube(RUBBER, [(1.692, 2.26, 7.41), (1.692, 2.72, 7.41), (1.62, 2.954, 7.41), (1.44, 2.954, 7.41)], 0.03, 12, 0.10)
    return m, parts


# ----------------------------------------------------------------------------- storage
def pillow(size, strap_xs=(), pinch=0.014, sag=0.04, exponent=5.0, segs=8):
    """Pillowed canvas body of a soft bag: a rounded box whose faces bulge, drawn in where straps bind it.

    Centre at the origin, front towards +z. Returns (vertices, triangles, surface), where surface(cx, cy, cz)
    maps a point of the cube [-1, 1]^3 to the body, so straps and fittings can be laid on it."""
    half = np.asarray(size, dtype=np.float64) / 2.0

    def surface(cx, cy, cz):
        c = np.array([cx, cy, cz], dtype=np.float64)
        p = c / (np.abs(c) ** exponent).sum() ** (1.0 / exponent) * half
        if p[1] > 0:                                   # an under-filled bag sinks along its top
            p[1] *= 1.0 - sag * (1.0 - (p[0] / half[0]) ** 2)
        for sx in strap_xs:                            # straps draw the girth in
            p[1:] *= 1.0 - pinch / min(half[1], half[2]) * math.exp(-((p[0] - sx) / 0.03) ** 2)
        return p

    index, verts, tris = {}, [], []

    def vertex(c):
        key = tuple(np.round(c, 6))
        if key not in index:
            index[key] = len(verts)
            verts.append(surface(*c))
        return index[key]

    uniform = np.linspace(-1.0, 1.0, segs + 1)
    # extra grid lines along the width, so each strap has a groove of its own
    grooves = [(sx + k * 0.045) / half[0] for sx in strap_xs for k in (-1, 0, 1)]
    along = [np.unique(np.round(np.concatenate([uniform, grooves]), 6)), uniform, uniform]
    for axis in range(3):
        for sign in (-1.0, 1.0):
            first, second = [a for a in range(3) if a != axis]
            for i in range(len(along[first]) - 1):
                for j in range(len(along[second]) - 1):
                    quad = []
                    for a, b in ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1)):
                        c = [0.0, 0.0, 0.0]
                        c[axis], c[first], c[second] = sign, along[first][a], along[second][b]
                        quad.append(vertex(c))
                    tris += [(quad[0], quad[1], quad[2]), (quad[0], quad[2], quad[3])]
    v = np.asarray(verts)
    t = np.asarray(tris, dtype=np.int64)
    # the body is star-shaped about its centre: turn every triangle to face away from it
    normals = np.cross(v[t[:, 1]] - v[t[:, 0]], v[t[:, 2]] - v[t[:, 0]])
    inward = (normals * v[t].mean(axis=1)).sum(axis=1) < 0
    t[inward] = t[inward][:, ::-1]
    return v, t, surface


def girth_band(surface, x, width=0.036, lift=0.003, segs=32):
    """Webbing strap around the girth of a pillow at x: a closed band lying on the body. Returns (v, t)."""
    def cube_x(target, cy, cz):                         # the cube coordinate whose image has this x
        lo, hi = -1.0, 1.0
        for _ in range(24):
            mid = (lo + hi) / 2
            if surface(mid, cy, cz)[0] < target:
                lo = mid
            else:
                hi = mid
        return (lo + hi) / 2

    rings = ([], [])
    for k in range(segs):
        a = math.tau * k / segs
        cy, cz = math.cos(a), math.sin(a)
        scale = 1.0 / max(abs(cy), abs(cz))            # onto the square perimeter
        cy, cz = cy * scale, cz * scale
        for ring, target in zip(rings, (x - width / 2, x + width / 2)):
            p = surface(cube_x(target, cy, cz), cy, cz)
            radial = np.array([0.0, p[1], p[2]])
            ring.append(p + radial / np.linalg.norm(radial) * lift)
    v = np.asarray(rings[0] + rings[1])
    t = []
    for k in range(segs):
        k1 = (k + 1) % segs
        t += [(k, segs + k, segs + k1), (k, segs + k1, k1)]
    t = np.asarray(t, dtype=np.int64)
    normals = np.cross(v[t[:, 1]] - v[t[:, 0]], v[t[:, 2]] - v[t[:, 0]])
    centre = v[t].mean(axis=1)
    centre[:, 0] = 0.0
    inward = (normals * centre).sum(axis=1) < 0
    t[inward] = t[inward][:, ::-1]
    return v, t


def soft_bag(art, size, label=None, sag=0.04):
    """Soft cargo bag (asset A04): pillowed canvas body, two girth straps with buckles, a grab loop and a
    contents label. Centre at the origin, front towards +z; size = (width, height, depth)."""
    m = Model("SoftBag")
    w, h, d = size
    strap_xs = (-w * 0.27, w * 0.27)
    v, t, surface = pillow(size, strap_xs, 0.014, sag)
    m.add(CANVAS, v, t)
    for sx in strap_xs:
        bv, bt = girth_band(surface, sx)
        m.add(STRAP, bv, bt)
        front = surface(sx / (w / 2), -0.12, 1.0)
        m.box(ANO_CLEAR, (sx, front[1], front[2] + 0.008), (0.05, 0.034, 0.012), 0.003)
        m.box(ANO, (sx, front[1], front[2] + 0.015), (0.03, 0.012, 0.004), 0.001)
    face = d / 2
    m.tube(STRAP, [(-0.07, h * 0.20, face - 0.004), (-0.07, h * 0.20 + 0.02, face + 0.03), (0.07, h * 0.20 + 0.02, face + 0.03),
                   (0.07, h * 0.20, face - 0.004)], 0.007, 6, 0.02)
    if label:
        m.box(WHITE, (0.0, -h * 0.03, face + 0.0005), (min(0.22, w * 0.42), 0.075, 0.003), 0.001)
        m.decal(art.decals, label, (0.0, -h * 0.03, face + 0.0022), None, width=min(0.19, w * 0.36))
    return m


def transit_case(art, size, label=None):
    """Hard transit case under the soft stowage: ribbed shell, two latches, a recessed handle. Front towards +z."""
    m = Model("TransitCase")
    w, h, d = size
    m.box(GRAY, (0, 0, 0), (w, h, d), 0.02)
    for ry in (-h * 0.28, 0.0, h * 0.28):
        m.box(GRAY, (0, ry, 0.004), (w + 0.008, 0.03, d), 0.008)
    m.box(ANO, (0, h * 0.14, d / 2 + 0.001), (w * 0.98, 0.006, 0.004), 0.0)                 # lid line
    for sx in (-1, 1):
        m.box(ALU, (sx * w * 0.30, h * 0.14, d / 2 + 0.008), (0.05, 0.08, 0.014), 0.004)
    m.box(GRAPHITE, (0, -h * 0.12, d / 2 + 0.001), (0.16, 0.06, 0.004), 0.0)
    m.cyl(ALU, (-0.06, -h * 0.12, d / 2 + 0.012), (0.06, -h * 0.12, d / 2 + 0.012), 0.007, 8)
    if label:
        m.decal(art.decals, label, (-w * 0.26, -h * 0.30, d / 2 + 0.0005), None, 0.8)
    m.decal(art.decals, art.next("barcode", 24), (w * 0.30, -h * 0.30, d / 2 + 0.0005), None, 0.7)
    return m


def stowage_wall(art, kind, first_bag=0):
    """Three-bay stowage rack inside the blockout's locker envelope (0.65 m deep, 4.5 m long, 2.6 m tall).

    kind "soft": strapped cargo bags over hard transit cases (asset A04); kind "hard": standard lockers."""
    m = Model("StowageWall")
    m.box(GRAY, (0, 1.3, -0.30), (4.5, 2.6, 0.05), 0.006)
    for x in (-2.25, -0.75, 0.75, 2.25):
        m.box(ALU, (x, 1.3, 0.0), (0.04, 2.6, 0.65), 0.006)
        for i in range(24):
            m.cyl(GRAPHITE, (x, 0.15 + i * 0.1, 0.325), (x, 0.15 + i * 0.1, 0.3255), 0.007, 6, 0.0, None, (False, True))
    for y in (0.03, 2.57):
        m.box(ALU, (0, y, 0.0), (4.5, 0.06, 0.65), 0.006)
    bag = first_bag
    for bay in range(3):
        cx = -1.5 + bay * 1.5
        if kind == "hard":
            for r in range(4):
                for c in range(2):
                    x0 = cx - 0.70 + c * 0.70
                    y0 = 0.09 + r * 0.615
                    locker(m, art, (x0, y0, x0 + 0.70, y0 + 0.60), 0.30, None, ("t_food", "t_spares", "t_tools", "t_med")[(r + bay) % 4])
            m.decal(art.decals, "rack_%02d" % (18 + bay), (cx + 0.6, 2.585, 0.326), None, width=0.13)
            continue
        # level 0: two transit cases on the deck rail
        for c in range(2):
            x = cx - 0.355 + c * 0.71
            m.instance(transit_case(art, (0.69, 0.50, 0.54), "t_spares" if (bay + c) % 2 else "t_h2o"), at(x, 0.07 + 0.25, 0.02))
        # three levels of bags, each on a deck plate behind one webbing restraint
        levels = ((0.60, 0.66), (1.29, 0.62), (1.94, 0.56))
        for level, (y0, room) in enumerate(levels):
            m.box(ALU, (cx, y0 + 0.007, 0.02), (1.42, 0.014, 0.58), 0.003)
            pattern = (2, 3, 1)[(level + bay) % 3]
            if pattern == 1:                              # one long duffel lying across the bay
                bh = min(room - 0.08, 0.46)
                m.instance(soft_bag(art, (1.34, bh, 0.48), "bag_%02d" % (bag % 16), 0.07),
                           at(cx, y0 + 0.016 + bh / 2, 0.29 - 0.24))
                bag += 1
            else:
                bw = (1.40 - 0.02 * (pattern + 1)) / pattern
                for i in range(pattern):
                    bh = room - 0.04 - art.rng.random() * 0.05
                    bd = 0.46 + art.rng.random() * 0.06
                    bx = cx - 0.70 + 0.02 + bw / 2 + i * (bw + 0.02)
                    m.instance(soft_bag(art, (bw, bh, bd), "bag_%02d" % (bag % 16), 0.02 + art.rng.random() * 0.05),
                               at(bx, y0 + 0.016 + bh / 2, 0.29 - bd / 2))
                    bag += 1
            by = y0 + 0.016 + 0.13                        # restraint across the lower part of the row
            m.box(STRAP, (cx, by, 0.307), (1.46, 0.045, 0.004), 0.0)
            for sx in (-1, 1):
                m.box(ANO_CLEAR, (cx + sx * 0.70, by, 0.312), (0.06, 0.06, 0.016), 0.004)
        m.decal(art.decals, "rack_%02d" % (14 + bay), (cx + 0.6, 2.585, 0.326), None, width=0.13)
    handrail(m, (-0.75, 1.0), (-0.75, 1.6), 0.325)
    handrail(m, (0.75, 1.0), (0.75, 1.6), 0.325)
    return m


def build_storage(art, bays):
    m = Model("Storage")
    blank_bays = [bays["blanket"]] * 4
    build_shell(m, art, "Storage", blank_bays, blank_bays, "sign_logistics")
    m.instance(stowage_wall(art, "soft", 0), front_frame((-1.95, 0, 14.0), (1, 0, 0)))
    m.instance(stowage_wall(art, "hard"), front_frame((1.95, 0, 14.0), (-1, 0, 0)))
    return m


# ----------------------------------------------------------------------------- airlock
def suit_figure(art):
    """EVA suit hanging on its donning stand, facing +z; origin on the floor under the suit's centre line."""
    s = Model("Suit")
    # life-support backpack and the stand post behind it
    s.box(GRAY, (0, 1.30, -0.17), (0.50, 0.82, 0.17), 0.03)
    s.box(ALU, (0, 1.05, -0.27), (0.07, 2.00, 0.03), 0.004)
    s.box(ALU, (0, 1.62, -0.24), (0.30, 0.04, 0.05), 0.006)
    # hard upper torso with the chest control module and the mission patch
    s.box(FAB, (0, 1.36, 0.0), (0.44, 0.46, 0.28), 0.07)
    s.box(GRAY, (0, 1.30, 0.155), (0.22, 0.16, 0.05), 0.012)
    for i, mat in enumerate((ANO_BLUE, ANO_GOLD, ANO_CLEAR)):
        s.cyl(mat, (-0.06 + i * 0.06, 1.33, 0.18), (-0.06 + i * 0.06, 1.33, 0.192), 0.013, 8, 0.002)
    s.cyl(LED_G, (0.07, 1.265, 0.18), (0.07, 1.265, 0.183), 0.006, 8, 0.0, None, (False, True))
    s.decal(art.decals, "emblem", (-0.065, 1.47, 0.1405), None, width=0.085)
    for sx in (-1, 1):
        s.box(STRAP, (sx * 0.135, 1.36, 0.141), (0.03, 0.30, 0.005), 0.0)
    # neck ring, helmet with a gold visor, ear bearings
    s.cyl(ALU, (0, 1.595, 0.0), (0, 1.645, 0.0), 0.135, 20, 0.006)
    s.sphere(WHITE, (0, 1.80, 0.0), 0.165, 20, 12)
    s.sphere(ANO_GOLD, (0, 1.80, 0.04), 0.15, 18, 10, None, (1.0, 0.95, 0.9))
    for sx in (-1, 1):
        s.cyl(ALU, (sx * 0.150, 1.80, 0.0), (sx * 0.176, 1.80, 0.0), 0.03, 10, 0.004)
    # waist ring and brief
    s.cyl(ALU, (0, 1.10, 0.0), (0, 1.135, 0.0), 0.17, 20, 0.006)
    s.box(FAB, (0, 0.99, 0.0), (0.38, 0.24, 0.26), 0.07)
    for sx in (-1, 1):
        # arms hang slightly forward: sleeve, shoulder and wrist bearings, glove
        s.tube(FAB, [(sx * 0.255, 1.52, 0.0), (sx * 0.31, 1.24, 0.02), (sx * 0.30, 0.985, 0.06)], 0.058, 10, 0.08)
        s.cyl(ALU, (sx * 0.235, 1.52, 0.0), (sx * 0.275, 1.50, 0.0), 0.07, 14, 0.004)
        s.cyl(ALU, (sx * 0.30, 0.995, 0.058), (sx * 0.30, 0.965, 0.064), 0.052, 12, 0.003)
        s.box(GRAY, (sx * 0.30, 0.885, 0.07), (0.085, 0.16, 0.065), 0.025)
        # legs: trouser, ankle bearing, boot
        s.tube(FAB, [(sx * 0.105, 0.90, 0.0), (sx * 0.115, 0.52, 0.02), (sx * 0.115, 0.17, 0.0)], 0.082, 10, 0.08)
        s.cyl(ALU, (sx * 0.115, 0.185, 0.0), (sx * 0.115, 0.15, 0.0), 0.078, 14, 0.003)
        s.box(GRAY, (sx * 0.115, 0.075, 0.035), (0.13, 0.15, 0.27), 0.04)
        s.cyl(RED, (sx * 0.112, 0.645, 0.013), (sx * 0.112, 0.675, 0.013), 0.086, 14, 0.0, None, (False, False))
    # umbilical from the backpack to the chest module
    s.tube(CABLE_BLUE, [(0.20, 1.02, -0.14), (0.27, 1.02, 0.02), (0.20, 1.20, 0.16), (0.11, 1.28, 0.18)], 0.012, 8, 0.06)
    return s


def suit_locker(art):
    """Sealed suit stowage (asset A05): a glazed door over the suit bay, a service door, a lit interior.

    Front-view space; the envelope is the blockout's suit station: 1.25 wide, 2.4 tall, z from -0.45 (wall)
    to +0.15 (collision face). Returns (locker, parts, anchors) like the service racks."""
    m = Model("SuitLocker")
    back, front = -0.45, 0.15
    depth, mid = front - back, (front + back) / 2
    half, top = 0.62, 2.38
    bay_x0, bay_x1 = -0.58, 0.20           # suit bay; the service column lies between 0.24 and 0.58
    door_y0, door_y1 = 0.16, 2.18
    # carcass: sides, divider, top, plinth and back
    for x in (-half + 0.02, half - 0.02, 0.22):
        m.box(WHITE, (x, top / 2 + 0.01, mid), (0.04, top - 0.02, depth), 0.004)
    m.box(WHITE, (0, top - 0.10, mid), (2 * half, 0.20, depth), 0.006)
    m.box(WHITE, (0, 0.08, mid), (2 * half, 0.14, depth), 0.006)
    m.box(GRAY, (0, top / 2, back + 0.015), (2 * half, top, 0.03), 0.0)
    m.frame(WHITE, (0, top / 2 + 0.01, front), 2 * half, top - 0.02, 0.05, 0.012, 0.05, None, 4)
    # head: name plate and status lamp; plinth: ventilation grille
    m.decal(art.decals, "t_suit", (-0.19, top - 0.10, front + 0.0005), None, width=0.34)
    m.cyl(ANO, (0.40, top - 0.10, front), (0.40, top - 0.10, front + 0.008), 0.014, 10, 0.002)
    m.cyl(LED_G, (0.40, top - 0.10, front + 0.008), (0.40, top - 0.10, front + 0.011), 0.008, 10, 0.0, None, (False, True))
    vent(m, (-0.50, 0.035, 0.10, 0.125), front)
    # lit suit bay: warm strip under the head, pale liner behind the suit
    m.box(ALU, (-0.19, door_y1 + 0.005, 0.07), (0.50, 0.03, 0.10), 0.004)
    m.box(LIGHT, (-0.19, door_y1 - 0.012, 0.07), (0.44, 0.006, 0.07), 0.0)
    m.box(WHITE, (-0.19, (door_y0 + door_y1) / 2, back + 0.035), (bay_x1 - bay_x0, door_y1 - door_y0, 0.01), 0.0)
    m.box(DECK, (-0.19, 0.152, mid), (bay_x1 - bay_x0, 0.006, depth - 0.06), 0.0)
    # service column: fixed door with a bar handle, paddle latch, hinges and the EVA checklist
    sx0, sx1 = 0.25, 0.58
    scx = (sx0 + sx1) / 2
    m.plate(WHITE, (scx, (door_y0 + door_y1) / 2, front - 0.02), sx1 - sx0, door_y1 - door_y0, 0.02, 0.03, 0.004, None, 3)
    u_handle(m, (sx0 + 0.07, 1.00), (sx0 + 0.07, 1.36), front, 0.045, 0.009, ALU)
    m.box(GRAPHITE, (sx0 + 0.07, 1.52, front + 0.001), (0.06, 0.10, 0.003), 0.0)
    m.box(ALU, (sx0 + 0.07, 1.53, front + 0.008), (0.04, 0.045, 0.012), 0.004)
    for hy in (0.45, 1.90):
        m.cyl(ALU, (sx1 - 0.012, hy - 0.04, front), (sx1 - 0.012, hy + 0.04, front), 0.008, 8, 0.001)
    m.decal(art.decals, "p_eva", (scx + 0.03, 1.80, front + 0.0005), None, 0.72)
    m.decal(art.decals, "t_eva", (scx + 0.01, 2.08, front + 0.0005), None, width=0.26)
    m.decal(art.decals, art.next("pn", 28), (scx + 0.02, 0.24, front + 0.0005), None, 0.85)

    # the suit is its own object, so it can be hidden while the crew member wears it
    suit = Model("SuitLocker_Suit")
    suit_origin = (-0.19, 0.155, -0.12)
    suit.instance(suit_figure(art), at(*suit_origin))
    suit.local_origin = suit_origin

    # glazed door, pivot on its hinge line at the aft edge
    door = Model("SuitLocker_Door")
    dcx, dcy = (bay_x0 + bay_x1) / 2, (door_y0 + door_y1) / 2
    dw, dh = bay_x1 - bay_x0 + 0.03, door_y1 - door_y0
    door.frame(WHITE, (dcx, dcy, front - 0.03), dw, dh, 0.075, 0.03, 0.09, None, 5)
    door.frame(RUBBER, (dcx, dcy, front - 0.034), dw - 0.13, dh - 0.13, 0.014, 0.006, 0.03, None, 5)
    door.box(GLASS, (dcx, dcy, front - 0.018), (dw - 0.14, dh - 0.14, 0.006), 0.0)
    u_handle(door, (bay_x1 - 0.035, 1.00), (bay_x1 - 0.035, 1.36), front, 0.045, 0.009, ALU)
    door.box(ANO, (bay_x1 - 0.035, 1.52, front + 0.006), (0.045, 0.09, 0.012), 0.004)
    for hy in (0.42, 1.17, 1.92):
        door.cyl(ALU, (bay_x0 - 0.008, hy - 0.05, front - 0.012), (bay_x0 - 0.008, hy + 0.05, front - 0.012), 0.011, 8, 0.001)
    door.local_origin = (bay_x0 - 0.008, door_y0, front - 0.012)

    parts = {"SuitLocker_Suit": suit, "SuitLocker_Door": door}
    anchors = {"LightAnchor_SuitLocker": (-0.19, door_y1 - 0.05, 0.09)}     # under the head, in front of the helmet
    return m, parts, anchors


def door_leaf(art, outer):
    """Sliding pressure door leaf, 0.88 x 2.46 x 0.12 m, origin at floor centre like the blockout leaf.

    Both faces carry pressed stiffening panels, edge dogs and an amber grab bar; outer leaves have a viewport."""
    m = Model("DoorLeaf")
    m.box(WHITE, (0, 1.23, 0), (0.86, 2.44, 0.09), 0.01)
    for face in (-1, 1):
        f = compose(at(0, 0, face * 0.045), rotation((0, 1, 0), 0 if face > 0 else 180))
        m.frame(WHITE, (0, 1.23, 0.0), 0.86, 2.44, 0.07, 0.016, 0.05, f)
        m.box(GRAY, (0, 1.23, 0.004), (0.70, 2.28, 0.008), 0.004, f)
        m.plate(WHITE, (0, 1.66, 0.008), 0.64, 0.98, 0.012, 0.09, 0.006, f, 4)
        m.plate(WHITE, (0, 0.66, 0.008), 0.64, 0.70, 0.012, 0.09, 0.006, f, 4)
        m.box(ALU, (0, 1.09, 0.014), (0.70, 0.045, 0.012), 0.004, f)
        for bx in (-0.40, 0.40):
            for by in np.linspace(0.15, 2.31, 9):
                bolt(m, bx, by, 0.016, 0.006, 0.004, ALU, f)
        for dy in (0.45, 1.23, 2.02):
            m.box(ALU, (-0.385, dy, 0.022), (0.05, 0.11, 0.012), 0.004, f)
        if outer:
            m.lathe(ALU, (0, 1.66, 0.020), (0, 0, 1), [(0.19, 0.0), (0.19, 0.02), (0.150, 0.024), (0.145, 0.008)], 24, f, (False, False))
            # a dark disc behind the pane reads as the view into the next volume
            m.cyl(GRAPHITE, (0, 1.66, 0.0200), (0, 1.66, 0.0212), 0.146, 24, 0.0, f, (False, True))
            m.cyl(GLASS, (0, 1.66, 0.022), (0, 1.66, 0.028), 0.145, 24, 0.0, f)
            for k in range(12):
                a = math.tau * k / 12
                bolt(m, 0.170 * math.cos(a), 1.66 + 0.170 * math.sin(a), 0.040, 0.006, 0.004, ALU, f)
        else:
            m.decal(art.decals, "sign_airlock", (0, 1.72, 0.0205), f, 0.9)
        handrail(m, (0.27, 1.26), (0.27, 1.62), 0.020, f, 0.05)
        m.decal(art.decals, "hazard_short", (0, 0.20, 0.0125), f, width=0.66)
        m.decal(art.decals, art.next("pn", 28), (-0.12, 0.40, 0.0205), f, 0.8)
    f = front_frame((0, 0, 0.045), (0, 0, 1))
    m.decal(art.decals, "stencil_eva" if outer else "w_depress", (0, 2.02 if outer else 1.36, 0.0205), f, 1.0)
    for sx in (-1, 1):
        m.box(RUBBER, (sx * 0.435, 1.23, 0), (0.014, 2.44, 0.10), 0.004)
    return m


def build_airlock(art, bays):
    """Returns the static compartment and its separately addressable parts (ship coordinates)."""
    m = Model("Airlock")
    build_shell(m, art, "Airlock", [bays["blanket"], bays["blanket"]], [bays["blanket"], bays["emergency"]], "sign_airlock")
    portal(m, art, 4.8, 21.0, "sign_airlock")
    m.instance(airlock_rack(art), front_frame((-2.02, 0, 19.0), (1, 0, 0)))
    locker_frame = front_frame((1.95, 0, 19.0), (-1, 0, 0))
    locker, locker_parts, locker_anchors = suit_locker(art)
    m.instance(locker, locker_frame)
    parts = place_parts(locker_frame, locker_parts, locker_anchors, "AirlockParts")
    # door tracks above and below both hatchways
    for z in (17.0, 21.0):
        for y in (2.52, -0.012):
            m.box(ANO, (0, y, z), (3.7, 0.03, 0.15), 0.004)
    f = front_frame((0, 0.0, 20.45), (0, 1, 0), (0, 0, 1))
    m.decal(art.decals, "hazard_long", (0, 0, 0), f, width=1.7)
    m.decal(art.decals, "stencil_noslip", (0, -0.5, 0), f, 1.6)

    # inner face of the aft pressure bulkhead, seen from inside the airlock (x to starboard)
    b = front_frame((0, 0.0, 20.92), (0, 0, -1))
    # pressure station beside the outer hatch: dial with a separate pointer, display, two keys, conduit
    px, py = 1.45, 1.45
    m.box(WHITE, (px, py, 0.04), (0.36, 0.86, 0.08), 0.02, b)
    gauge(m, art, px, py + 0.25, 0.08, b, "gauge_airlock", 0.10, None)
    display(m, art, (px - 0.14, py - 0.18, px + 0.14, py + 0.03), 0.08, b, "sm_airlock", 0)
    m.box(ANO, (px - 0.06, py - 0.29, 0.084), (0.07, 0.05, 0.008), 0.003, b)
    m.box(LED_A, (px - 0.06, py - 0.29, 0.092), (0.052, 0.034, 0.008), 0.003, b)
    m.box(ANO, (px + 0.06, py - 0.29, 0.084), (0.07, 0.05, 0.008), 0.003, b)
    m.box(ANO_CLEAR, (px + 0.06, py - 0.29, 0.092), (0.040, 0.034, 0.008), 0.003, b)
    m.decal(art.decals, "t_depress", (px, py - 0.37, 0.0805), b, width=0.26)
    m.cyl(ALU, (px, 0.12, 0.03), (px, py - 0.43, 0.03), 0.016, 10, 0.0, b, (False, False))
    for cy in (0.45, 0.90):
        m.box(ALU, (px, cy, 0.02), (0.07, 0.024, 0.045), 0.003, b)
    needle = Model("AirlockGauge_Needle")
    needle_origin = (px, py + 0.25, 0.088)
    needle.instance(gauge_needle(0.10, 101.3 / 120.0), at(*needle_origin))
    needle.local_origin = needle_origin
    parts.update(place_parts(b, {"AirlockGauge_Needle": needle}, {}, "AirlockParts"))
    # port side of the hatch: conduits clamped to the bulkhead stiffeners and the intercom
    for cx, radius, mat in ((-1.30, 0.020, RUBBER), (-1.365, 0.014, ALU)):
        m.cyl(mat, (cx, 0.12, 0.034), (cx, 2.46, 0.034), radius, 10, 0.0, b, (False, False))
    for cy in (0.45, 0.90, 1.40, 1.90, 2.35):
        m.box(ALU, (-1.33, cy, 0.03), (0.13, 0.026, 0.05), 0.003, b)
    m.box(GRAY, (-1.56, 1.45, 0.03), (0.24, 0.34, 0.06), 0.012, b)
    vent(m, (-1.65, 1.48, -1.47, 1.58), 0.06, b, 5)
    for i, lamp in enumerate((LED_G, LED_A)):
        m.cyl(lamp, (-1.62 + i * 0.05, 1.42, 0.06), (-1.62 + i * 0.05, 1.42, 0.063), 0.007, 8, 0.0, b, (False, True))
    m.box(ANO, (-1.50, 1.37, 0.064), (0.06, 0.04, 0.008), 0.003, b)
    m.decal(art.decals, "conn_07", (-1.56, 1.31, 0.0605), b, 0.9)
    return m, parts


def build_doors(art):
    """Four door leaves as separate objects, placed like the blockout: inner open, outer closed."""
    leaves = {}
    for name, outer, x, z in (("DoorLeaf_Inner_L", False, -1.4, 17.0), ("DoorLeaf_Inner_R", False, 1.4, 17.0),
                              ("DoorLeaf_Outer_L", True, -0.45, 21.0), ("DoorLeaf_Outer_R", True, 0.45, 21.0)):
        leaf = Model(name)
        leaf.instance(door_leaf(art, outer), at(x, 0, z))
        leaf.origin = (x, 0.0, z)
        leaf.group = "Doors"
        leaves[name] = leaf
    return leaves


# ----------------------------------------------------------------------------- exterior
def hull_shell(m, stations, segs=64, mat=HULL):
    """Pressure hull skin through (z, radius) stations with cylindrical UVs in metres (no projection seams)."""
    rows = []
    uvs = []
    for z, radius in stations:
        angles = np.linspace(0.0, math.tau, segs + 1)
        rows.append(np.stack([radius * np.cos(angles), AXIS_Y + radius * np.sin(angles), np.full(segs + 1, z)], axis=1))
        uvs.append(np.stack([angles * R_AFT, np.full(segs + 1, z)], axis=1))
    v = np.concatenate(rows, axis=0)
    uv = np.concatenate(uvs, axis=0)
    tris = []
    n = segs + 1
    for i in range(len(stations) - 1):
        for k in range(segs):
            a, b, c, d = i * n + k, i * n + k + 1, (i + 1) * n + k + 1, (i + 1) * n + k
            tris.append((a, b, c))
            tris.append((a, c, d))
    t = np.asarray(tris, dtype=np.int64)
    a, b, c = v[t[0, 0]], v[t[0, 1]], v[t[0, 2]]
    centre = np.array([0.0, AXIS_Y, (a[2] + c[2]) / 2])
    if np.dot(np.cross(b - a, c - a), (a + b + c) / 3 - centre) < 0:
        t = t[:, ::-1]
    m.add(mat, v, t, None, uv[t])


def hull_band(m, z, radius, width=0.07, height=0.025, mat=ALU, segs=64):
    m.lathe(mat, (0, AXIS_Y, z - width / 2), (0, 0, 1),
            [(radius, 0.0), (radius + height, 0.012), (radius + height, width - 0.012), (radius, width)], segs, None, (False, False))


def on_hull(angle_deg, radius, z, lift=0.0):
    """Point on the hull at a clock angle (0 = starboard +x, 90 = top) and the outward normal there."""
    a = math.radians(angle_deg)
    normal = np.array([math.cos(a), math.sin(a), 0.0])
    return np.array([0.0, AXIS_Y, z]) + normal * (radius + lift), normal


def eva_handrail(m, angle_deg, radius, z0, z1, mat=ANO_GOLD):
    """EVA handrail along the hull between two stations, on standoffs."""
    p0, normal = on_hull(angle_deg, radius, z0)
    p1, _ = on_hull(angle_deg, radius, z1)
    off = normal * 0.09
    m.tube(mat, [p0, p0 + off, p1 + off, p1], 0.017, 8, 0.04)
    for p in (p0, p1):
        m.box(ALU, p + normal * 0.004, (0.07, 0.07, 0.07), 0.004)


def end_cap(m, z, radius, opening, facing, mat=HULL, steps=6):
    """Flat end bulkhead skin with one rounded-rectangular opening, facing -z (facing = -1) or +z (+1).

    opening = (centre x, centre y, width, height, corner radius), all in world coordinates.
    """
    from a7kit import rounded_rect
    cx, cy, w, h, r = opening
    corners = rounded_rect(w, h, r, steps) + np.array([cx, cy])
    # subdivide the long straight edges so the outer edge follows the hull circle instead of a chord
    dense = []
    for i in range(len(corners)):
        a, b = corners[i], corners[(i + 1) % len(corners)]
        count = max(1, int(math.ceil(float(np.linalg.norm(b - a)) / 0.12)))
        for k in range(count):
            dense.append(a + (b - a) * k / count)
    inner = np.asarray(dense)
    angles = np.arctan2(inner[:, 1] - AXIS_Y, inner[:, 0])
    outer = np.stack([radius * np.cos(angles), AXIS_Y + radius * np.sin(angles)], axis=1)
    n = len(inner)
    v = np.concatenate([np.column_stack([outer, np.full(n, z)]), np.column_stack([inner, np.full(n, z)])], axis=0)
    tris = []
    for k in range(n):
        k1 = (k + 1) % n
        tris.append((k, k1, n + k1))
        tris.append((k, n + k1, n + k))
    t = np.asarray(tris, dtype=np.int64)
    a, b, c = v[t[0, 0]], v[t[0, 1]], v[t[0, 2]]
    if np.cross(b - a, c - a)[2] * facing < 0:
        t = t[:, ::-1]
    m.add(mat, v, t)


def solar_wing(m, side, z, root_radius):
    """Deployable array on a mast: three hinged panels with cell faces up and a structural back."""
    base, normal = on_hull(0 if side > 0 else 180, root_radius, z)
    mast_end = base + normal * 1.6
    m.cyl(ALU, base, mast_end, 0.07, 14, 0.006)
    m.box(ALU, base + normal * 0.06, (0.36, 0.36, 0.36), 0.02)
    m.cyl(ANO, mast_end - np.array([0, 0.16, 0]), mast_end + np.array([0, 0.16, 0]), 0.11, 16, 0.01)
    for i in range(3):
        cx = mast_end[0] + side * (0.25 + 2.55 * i + 1.2)
        centre = np.array([cx, AXIS_Y, z])
        m.box(ALU, centre, (2.44, 0.035, 2.9), 0.006)
        m.box(SOLAR, centre + np.array([0, 0.019, 0]), (2.36, 0.006, 2.82), 0.0)
        m.box(MLI_GOLD, centre - np.array([0, 0.019, 0]), (2.36, 0.006, 2.82), 0.0)
        for dz in (-1.0, 0.0, 1.0):
            m.box(ALU, centre + np.array([0, -0.03, dz]), (2.44, 0.03, 0.04), 0.004)
        if i < 2:
            hx = cx + side * 1.275
            for dz in (-1.0, 0.0, 1.0):
                m.cyl(ALU, (hx, AXIS_Y, z + dz - 0.12), (hx, AXIS_Y, z + dz + 0.12), 0.025, 10, 0.004)
    m.tube(ALU, [mast_end, mast_end + np.array([side * 0.25, 0, 0])], 0.05, 10)


def rcs_quad(m, angle_deg, radius, z):
    """Reaction control quad: insulated housing with four small nozzles."""
    base, normal = on_hull(angle_deg, radius, z)
    f = front_frame(base, normal, (0, 0, 1))
    m.box(MLI_SILVER, (0, 0, 0.14), (0.46, 0.46, 0.28), 0.04, f)
    m.box(ALU, (0, 0, 0.01), (0.56, 0.56, 0.02), 0.006, f)
    for origin, axis in (((0.0, 0.0, 0.28), (0, 0, 1)), ((0.21, 0.0, 0.17), (1, 0, 0)),
                         ((-0.21, 0.0, 0.17), (-1, 0, 0)), ((0.0, 0.21, 0.17), (0, 1, 0))):
        m.lathe(ANO, origin, axis, [(0.03, 0.0), (0.035, 0.03), (0.075, 0.14), (0.07, 0.14), (0.028, 0.03)], 16, f, (False, False))


# ----------------------------------------------------------------------------- antenna (asset A07)
def antenna(art):
    """High-gain antenna on its hull pad (asset A07). Pad space: z outwards from the skin, y aft, x around the hull.

    Returns (static base, parts, anchors). The manual drive socket is on the aft service deck, on the pad's
    centre line and level with the AntennaRepair anchor (0.31 m off the skin). Base, mast and yoke are built
    here; the reflector assembly on the trunnion is a prop."""
    base = Model("AntennaBase")
    deck = 0.30
    mast_y = -0.28
    base.box(GRAY, (0, -0.02, 0.075), (0.66, 1.00, 0.03), 0.006)
    for bx in (-0.305, 0.305):
        for by in (-0.44, -0.16, 0.12, 0.40):
            bolt(base, bx, by, 0.09, 0.012, 0.008, ALU)
    base.box(GRAY, (0, -0.02, 0.19), (0.56, 0.92, 0.22), 0.02)
    # aft service deck: amber-edged access, manual drive socket, access cover, tether point, test connectors
    base.frame(AMBER, (0, 0.17, deck), 0.50, 0.52, 0.022, 0.002, 0.03)
    base.cyl(YELLOW, (0, 0, deck), (0, 0, deck + 0.03), 0.05, 16, 0.006)
    base.cyl(GRAPHITE, (0, 0, deck + 0.03), (0, 0, deck + 0.031), 0.026, 6, 0.0, None, (False, True))

    def deck_label(key, x, y, z, scale):
        # the crew arrives from aft with their head away from the hull, so deck labels read from that side
        base.decal(art.decals, key, (0, 0, 0), compose(at(x, y, z), rotation((0, 0, 1), 180)), scale)

    deck_label("ant_drive", 0.0, 0.10, deck, 1.0)
    deck_label("torque_note", 0.0, 0.135, deck, 0.85)
    base.plate(WHITE, (0.0, 0.29, deck), 0.30, 0.20, 0.008, 0.02)
    corner_bolts(base, (-0.15, 0.19, 0.15, 0.39), deck + 0.008, 0.022, ANO_GOLD, None, 0.008)
    deck_label("c_esd", 0.0, 0.29, deck + 0.008, 0.7)
    u_handle(base, (0.20, 0.10), (0.20, 0.30), deck, 0.05, 0.009, ANO_GOLD)
    deck_label("blue_tether", 0.165, 0.065, deck, 0.6)
    # aft face of the housing: a proper (unmirrored) frame, because pad space is already a front-view space
    aft = basis((0, 0.44, 0.19), (-1, 0, 0), (0, 0, 1), (0, 1, 0))
    connectors(base, art, (-0.13, -0.03, 0.13, 0.05), 0.0, aft, 2, -0.115, 0)
    # fore half: azimuth bearing and mast, with a service loop of cable
    base.cyl(ALU, (0, mast_y, deck), (0, mast_y, deck + 0.05), 0.16, 24, 0.008)
    for a in range(0, 360, 45):
        bolt(base, 0.135 * math.cos(math.radians(a)), mast_y + 0.135 * math.sin(math.radians(a)), deck + 0.05, 0.008, 0.006, ALU)
    base.cyl(WHITE, (0, mast_y, deck + 0.05), (0, mast_y, 0.62), 0.085, 20, 0.004)
    base.tube(RUBBER, [(0.19, -0.08, deck), (0.19, -0.08, deck + 0.12), (0.14, mast_y, 0.50), (0.08, mast_y, 0.56)], 0.012, 8, 0.05)

    azimuth = Model("Antenna_Azimuth")
    azimuth.cyl(ALU, (0, mast_y, 0.62), (0, mast_y, 0.68), 0.13, 24, 0.006)
    azimuth.box(GRAY, (0, mast_y, 0.705), (0.46, 0.15, 0.05), 0.008)
    for sx in (-1, 1):
        azimuth.box(GRAY, (sx * 0.205, mast_y, 0.87), (0.05, 0.13, 0.34), 0.008)
        azimuth.cyl(ALU, (sx * 0.18, mast_y, 0.96), (sx * 0.24, mast_y, 0.96), 0.05, 16, 0.004)
    azimuth.cyl(GRAY, (0.24, mast_y, 0.96), (0.33, mast_y, 0.96), 0.06, 16, 0.006)
    azimuth.local_origin = (0.0, mast_y, 0.62)

    # The elevation part (trunnion, hub, reflector, feed) is the prop Props/AntennaReflector; the editor tool hangs
    # it on a pivot created at this helper point, 0.34 m above the azimuth pivot.
    parts = {"Antenna_Azimuth": azimuth}
    anchors = {"Antenna_ServiceSocket": (0.0, 0.0, 0.308), "Antenna_ElevationMount": (0.0, mast_y, 0.96)}
    return base, parts, anchors


def build_exterior(art):
    from a7kit import ring, rounded_rect
    m = Model("Exterior")
    # pressure hull: wide flight-deck section, conical adapter, long aft section
    hull_shell(m, [(-3.12, R_FWD), (3.0, R_FWD), (3.9, R_AFT), (21.1, R_AFT)])
    for z in (-3.08, -1.5, 0.0, 1.5, 3.0):
        hull_band(m, z, R_FWD)
    for z in np.arange(3.9, 21.2, 1.5):
        hull_band(m, float(min(z, 21.06)), R_AFT)
    for angle in range(0, 360, 30):
        for z0, z1, radius in ((-3.0, 2.9, R_FWD), (4.0, 21.0, R_AFT)):
            p0, normal = on_hull(angle, radius, z0, 0.006)
            p1, _ = on_hull(angle, radius, z1, 0.006)
            f = front_frame((p0 + p1) / 2, normal, (0, 0, 1))
            m.box(ALU, (0, 0, 0), (0.03, z1 - z0, 0.012), 0.002, f)

    # forward face: skin around the window band, exterior pane frames, a sun brow and the ship's name
    end_cap(m, -3.12, R_FWD, (0.0, 2.0, 4.42, 1.60, 0.02), -1)
    f = front_frame((0, 0, -3.12), (0, 0, -1))
    for cx, w in ((-1.82, 0.74), (-0.94, 0.74), (0.0, 0.86), (0.94, 0.74), (1.82, 0.74)):
        v, t = ring(rounded_rect(w + 0.14, 1.60, 0.012, 5), rounded_rect(w - 0.10, 1.46, 0.16, 5), 0.07)
        m.add(ALU, v + vec(cx, 2.0, -0.02), t, f)
        for bx in np.linspace(-w / 2 + 0.06, w / 2 - 0.06, 5):
            for by in (2.0 - 0.765, 2.0 + 0.765):
                bolt(m, cx + bx, by, 0.05, 0.009, 0.007, ALU, f)
    m.box(ALU, (0, 2.95, 0.22), (4.7, 0.05, 0.5), 0.01, f)
    for sx in (-2.2, 0.0, 2.2):
        m.box(ALU, (sx, 2.9, 0.1), (0.05, 0.14, 0.24), 0.006, f)
    m.decal(art.decals, "stencil_aurora", (0, 0.72, 0.004), f, 2.4)
    m.decal(art.decals, "emblem", (0, 4.25, 0.004), f, 2.6)

    # aft bulkhead with the EVA hatch, porch, lights and translation aids
    end_cap(m, 21.1, R_AFT, (0.0, 1.24, 2.1, 2.84, 0.3), 1)
    a = front_frame((0, 0, 21.1), (0, 0, 1))
    m.frame(ALU, (0, 1.24, 0.0), 2.24, 2.96, 0.14, 0.06, 0.36, a, 5)
    for by in np.linspace(0.25, 2.3, 7):
        for bx in (-1.05, 1.05):
            bolt(m, bx, by, 0.06, 0.012, 0.01, ALU, a)
    m.decal(art.decals, "stencil_eva", (0, 3.0, 0.004), a, 2.2)
    m.decal(art.decals, "hazard_long", (0, 2.78, 0.004), a, width=2.0)
    for sx in (-1.35, 1.35):
        handrail(m, (sx, 0.9), (sx, 1.9), 0.0, a, 0.09, ANO_GOLD)
        m.cyl(ALU, (sx * 1.25, 2.9, 0.0), (sx * 1.25, 2.9, 0.12), 0.07, 14, 0.01, a)
        m.cyl(LIGHT, (sx * 1.25, 2.9, 0.12), (sx * 1.25, 2.9, 0.126), 0.055, 14, 0.0, a, (False, True))
    # porch: grated platform at deck level with guard rails
    m.box(ALU, (0, -0.05, 22.25), (2.9, 0.06, 2.3), 0.01)
    for x in np.arange(-1.3, 1.31, 0.2):
        m.box(ANO, (x, -0.012, 22.25), (0.035, 0.02, 2.16), 0.003)
    for z in np.arange(21.3, 23.3, 0.36):
        m.box(ANO, (0, -0.006, z), (2.76, 0.012, 0.03), 0.002)
    for sx in (-1.42, 1.42):
        m.tube(ANO_GOLD, [(sx, -0.02, 21.2), (sx, 1.05, 21.2), (sx, 1.05, 23.35), (sx, -0.02, 23.35)], 0.02, 10, 0.08)
        m.tube(ANO_GOLD, [(sx, 0.52, 21.2), (sx, 0.52, 23.35)], 0.014, 8)
        for z in (21.9, 22.6):
            m.tube(ANO_GOLD, [(sx, -0.02, z), (sx, 1.05, z)], 0.014, 8)
    m.tube(ANO_GOLD, [(-1.42, 1.05, 23.35), (1.42, 1.05, 23.35)], 0.02, 10)
    m.tube(ANO_GOLD, [(-1.42, 0.52, 23.35), (1.42, 0.52, 23.35)], 0.014, 8)
    for sx in (-1.3, 1.3):
        m.tube(ALU, [(sx, -0.08, 21.2), (sx * 0.9, -1.0, 21.12)], 0.03, 8)
    deck_frame = front_frame((0, 0.0, 22.2), (0, 1, 0), (0, 0, 1))
    m.decal(art.decals, "hazard_long", (0, 0.95, 0.001), deck_frame, width=2.6)

    # translation path from the hatch to the antenna work site (upper starboard quarter, z = 13)
    antenna_angle = math.degrees(math.atan2(2.4 - AXIS_Y, 3.2))
    for z0 in np.arange(13.4, 20.6, 1.5):
        eva_handrail(m, antenna_angle + 14, R_AFT, z0, z0 + 1.1)
        eva_handrail(m, antenna_angle - 16, R_AFT, z0 + 0.2, z0 + 1.3)
    for angle in np.arange(antenna_angle - 16, 91, 22):
        p0, normal = on_hull(angle, R_AFT, 20.75)
        p1, normal1 = on_hull(angle + 14, R_AFT, 20.75)
        m.tube(ANO_GOLD, [p0, p0 + normal * 0.09, p1 + normal1 * 0.09, p1], 0.017, 8, 0.04)
    # antenna mounting pad at the AntennaRepair anchor, with the high-gain antenna on it
    pad, normal = on_hull(antenna_angle, R_AFT, 13.0)
    f = front_frame(pad, normal, (0, 0, 1))
    m.box(ALU, (0, 0, 0.03), (0.9, 1.1, 0.06), 0.012, f)
    for bx in (-0.40, 0.40):
        for by in (-0.50, 0.50):
            bolt(m, bx, by, 0.06, 0.016, 0.012, ALU, f)
    m.cyl(ANO_GOLD, (0.55, 0, 0.0), (0.55, 0, 0.08), 0.05, 14, 0.008, f)
    antenna_base, antenna_parts, antenna_anchors = antenna(art)
    m.instance(antenna_base, f)
    parts = place_parts(f, antenna_parts, antenna_anchors, "ExteriorParts")

    # power and thermal: two solar wings, body-mounted radiators, reaction control quads
    for side in (-1, 1):
        solar_wing(m, side, 8.0, R_AFT)
        for z in (15.0, 17.6):
            base, normal = on_hull(200 if side < 0 else -20, R_AFT, z)
            f = front_frame(base, normal, (0, 0, 1))
            m.box(WHITE, (0, 0, 0.09), (2.0, 2.3, 0.04), 0.01, f)
            for dy in (-0.9, 0.0, 0.9):
                m.cyl(ALU, (-1.0, dy, 0.05), (1.0, dy, 0.05), 0.025, 8, 0.0, f)
            for bx in (-0.85, 0.85):
                m.cyl(ALU, (bx, -1.0, 0.0), (bx, -1.0, 0.09), 0.03, 8, 0.004, f)
                m.cyl(ALU, (bx, 1.0, 0.0), (bx, 1.0, 0.09), 0.03, 8, 0.004, f)
    for angle in (45, 135, 225, 315):
        rcs_quad(m, angle, R_FWD, -1.0)
        rcs_quad(m, angle, R_AFT, 19.4)
    # ship name along both flanks
    for side in (-1, 1):
        base, normal = on_hull(0 if side > 0 else 180, R_AFT, 11.2, 0.004)
        f = front_frame(base, normal, (0, 1, 0))
        m.decal(art.decals, "stencil_aurora", (0, 0, 0), f, 4.0)
    return m, parts


# ----------------------------------------------------------------------------- collision proxies
def build_collision():
    """Simple boxes with the blockout's solid envelope; delivered separately from visible detail."""
    boxes = []

    def add(name, centre, size):
        boxes.append((name, centre, size))

    for name, room in ROOMS.items():
        w, z0, z1 = room["width"], room["z0"], room["z1"]
        zc, length = (z0 + z1) / 2, z1 - z0
        add("COL_%s_Floor" % name, (0, -0.12, zc), (w, 0.24, length))
        add("COL_%s_Ceiling" % name, (0, 3.22, zc), (w, 0.2, length))
        for side, tag in ((-1, "L"), (1, "R")):
            add("COL_%s_Wall%s" % (name, tag), (side * (w / 2 + 0.06), 1.5, zc), (0.12, 3.2, length))
    for name, z, w in (("Engineering", 3.0, 4.8), ("Storage", 11.0, 4.8), ("Airlock", 17.0, 4.8), ("Exterior", 21.0, 4.8)):
        wing = (w - 1.8) / 2
        for side, tag in ((-1, "L"), (1, "R")):
            add("COL_Portal%s_Wing%s" % (name, tag), (side * (0.9 + wing / 2), 1.55, z), (wing, 3.1, 0.18))
        add("COL_Portal%s_Lintel" % name, (0, 2.82, z), (w, 0.64, 0.2))
    add("COL_Cockpit_ForwardBulkhead", (0, 1.55, -3.05), (6, 3.1, 0.15))
    add("COL_Cockpit_ConsolePlinth", (0, 0.46, -1.95), (2.9, 0.92, 0.7))
    add("COL_Cockpit_ConsoleTop", (0, 0.96, -1.95), (3.1, 0.12, 0.85))
    for side, tag in ((-1, "L"), (1, "R")):
        add("COL_Cockpit_Seat%s_Pedestal" % tag, (side * 1.15, 0.3, -0.45), (0.4, 0.6, 0.5))
        add("COL_Cockpit_Seat%s_Pan" % tag, (side * 1.15, 0.6, -0.45), (0.62, 0.15, 0.65))
        add("COL_Cockpit_Seat%s_Back" % tag, (side * 1.15, 1.05, -0.17), (0.61, 0.9, 0.14))
    add("COL_Engineering_PowerRack", (-2.02, 1.15, 6.1), (0.58, 2.3, 1.6))
    add("COL_Engineering_EclssRack", (2.02, 1.15, 8.1), (0.58, 2.3, 1.6))
    for i in range(3):
        add("COL_Engineering_Tank%d" % (i + 1), (1.99, 1.1, 4.85 + i * 0.58), (0.38, 1.56, 0.38))
    add("COL_Storage_LockersL", (-1.95, 1.3, 14.0), (0.65, 2.6, 4.5))
    add("COL_Storage_LockersR", (1.95, 1.3, 14.0), (0.65, 2.6, 4.5))
    add("COL_Airlock_Rack", (-2.02, 1.15, 19.0), (0.58, 2.3, 1.6))
    add("COL_Airlock_SuitStation", (1.95, 1.25, 19.0), (0.3, 2.4, 1.25))
    return boxes


# ----------------------------------------------------------------------------- entry point
def build_all(decals, screens):
    art = Art(decals, screens)
    bays = {
        "blanket": bay_blanket(art),
        "lockers": bay_lockers(art),
        "lockers_b": bay_lockers(art, ("t_filter", "t_scrub", "t_spares", "t_stow"), 2, 3),
        "equipment_gnc": bay_equipment(art, "sm_thermal", "leg_gnc", "t_gnc", 4),
        "equipment_comm": bay_equipment(art, "sm_comm", "leg_comm", "t_comm", 0),
        "equipment_thermal": bay_equipment(art, "sm_thermal", "leg_light", "t_thermal", 8),
        "equipment_science": bay_equipment(art, "sm_stow", "leg_light", "t_science", 2),
        "stowage_a": bay_stowage(art, 0),
        "utility": bay_utility(art),
        "tools": bay_tools(art),
        "emergency": bay_emergency(art),
        "service": bay_service(art, (0.36, 0.70, 1.20, 0.55, "t_bus_a")),       # EPS-2 control panel
        "service_ars": bay_service(art, (0.33, 0.80, 1.40, 0.45, "t_eclss")),   # ARS-1 air revitalisation unit
        "closeout": bay_service(art),
    }
    engineering, engineering_parts = build_engineering(art, bays)
    exterior, exterior_parts = build_exterior(art)
    airlock, airlock_parts = build_airlock(art, bays)
    models = {
        "Cockpit": build_cockpit(art, bays),
        "Engineering": engineering,
        "Storage": build_storage(art, bays),
        "Airlock": airlock,
        "Exterior": exterior,
    }
    models.update(build_doors(art))
    models.update(engineering_parts)
    models.update(exterior_parts)
    models.update(airlock_parts)
    return models, build_collision()


if __name__ == "__main__":
    import json
    import os
    here = os.path.dirname(os.path.abspath(__file__))
    textures = os.path.join(here, "..", "Textures")
    with open(os.path.join(textures, "a7_decals.json"), encoding="utf-8") as handle:
        decal_atlas = json.load(handle)
    with open(os.path.join(textures, "a7_screens.json"), encoding="utf-8") as handle:
        screen_atlas = json.load(handle)
    built, proxies = build_all(decal_atlas, screen_atlas)
    total = 0
    for model_name, model in built.items():
        lo, hi = model.bounds()
        print("%-18s %8d tris  %2d materials  bounds %s .. %s" % (model_name, model.triangle_count(), len(model.parts),
                                                                 np.round(lo, 2), np.round(hi, 2)))
        total += model.triangle_count()
    print("total triangles", total, "| collision proxies", len(proxies))
