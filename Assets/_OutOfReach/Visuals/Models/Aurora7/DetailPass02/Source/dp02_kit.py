"""
dp02_kit - SHIP-DETAIL-02 interior detail kit for AURORA-7 (original, procedural work).

Small reusable pieces as functions; dp02_layout.py instances them in deliberate clusters.
Conventions follow a7kit: Unity metres, y up. Each piece is built in a front-view mount frame:
local x to the viewer's right, y up, z out of the host surface. The host surface is the plane z = 0
and every piece keeps GAP above it, so nothing is buried in the ship and nothing floats.
Only existing AURORA-7 (A7_*) and habitat (HB_*) material names are used.
"""
import math
import sys

import numpy as np

sys.dont_write_bytecode = True
from a7kit import (Model, _align_y, at, compose, fillet_path, lathe, rotation, signed_volume,  # noqa: E402
                   unit, vec)

GAP = 0.0005          # clearance between a mounted piece and its host surface

WHITE, GRAY, GRAPHITE = "A7_PaintWhite", "A7_PaintGray", "A7_PaintGraphite"
AMBER, PLASTIC = "A7_PaintAmber", "A7_PlasticWhite"
ANO, ANO_CLEAR, ANO_BLUE, ANO_GOLD = "A7_AnodizedBlack", "A7_AnodizedClear", "A7_AnodizedBlue", "A7_AnodizedGold"
ALU = "A7_Aluminum"
FAB, FAB_DARK, STRAP, CANVAS = "A7_FabricWhite", "A7_FabricDark", "A7_FabricStrap", "A7_FabricCanvas"
RUBBER, C_ORANGE, C_BLUE, C_RED, C_YELLOW = "A7_Rubber", "A7_CableOrange", "A7_CableBlue", "A7_CableRed", "A7_CableYellow"
DECALS, HB_LABELS = "A7_Decals", "HB_Labels"
LED_C = "A7_LedCyan"


# ----------------------------------------------------------------------------- generic helpers
def ring_band(m, mat, centre, axis, r_in, r_out, width, segs=12, frame=None):
    """Closed annular band (rectangular section) around `axis` through `centre`: clamp straps, ties, grommets."""
    h = width / 2.0
    profile = [(r_in, -h), (r_out, -h), (r_out, h), (r_in, h), (r_in, -h)]
    v, t = lathe(profile, segs, False, False)
    if signed_volume(v, t) < 0:
        t = t[:, ::-1]
    place = compose(frame if frame is not None else np.eye(4), _align_y(vec(centre), vec(axis)))
    m.add(mat, v, t, place)


def solid_lathe(m, mat, origin, axis, profile, segs=12, frame=None):
    """Closed surface of revolution (both ends capped) with outward faces."""
    v, t = lathe(profile, segs, True, True)
    if signed_volume(v, t) < 0:
        t = t[:, ::-1]
    place = compose(frame if frame is not None else np.eye(4), _align_y(vec(origin), vec(axis)))
    m.add(mat, v, t, place)


def frames_along(path, ref):
    """Parallel-transport frames along a polyline. The first normal is `ref` with its tangent part removed."""
    path = np.asarray(path, dtype=np.float64)
    n = len(path)
    tangents = np.zeros_like(path)
    for i in range(n):
        if i == 0:
            d = path[1] - path[0]
        elif i == n - 1:
            d = path[-1] - path[-2]
        else:
            d = unit(path[i + 1] - path[i]) + unit(path[i] - path[i - 1])
        tangents[i] = unit(d)
    normals, binormals = np.zeros_like(path), np.zeros_like(path)
    nrm = vec(ref)
    for helper in (vec(ref), vec(1.0, 0.0, 0.0), vec(0.0, 1.0, 0.0)):   # a reference along the first tangent
        nrm = helper - tangents[0] * np.dot(helper, tangents[0])      # would collapse every ring to a point
        if np.linalg.norm(nrm) > 1e-3:
            break
    for i in range(n):
        t = tangents[i]
        nrm = unit(nrm - t * np.dot(nrm, t))
        normals[i] = nrm
        binormals[i] = np.cross(t, nrm)
    return tangents, normals, binormals


def densify(points, step=0.05):
    """Splits long straight segments so offset cables follow bends smoothly."""
    points = [vec(p) for p in points]
    out = [points[0]]
    for a, b in zip(points[:-1], points[1:]):
        count = max(1, int(math.ceil(np.linalg.norm(b - a) / step)))
        for k in range(1, count + 1):
            out.append(a + (b - a) * k / count)
    return np.asarray(out)


# Four cables in a square: (offset along normal, offset along binormal, radius, material)
LOOM_POWER = [(0.0093, 0.0, 0.0065, RUBBER), (0.0, 0.0093, 0.0065, C_ORANGE),
              (-0.0093, 0.0, 0.0065, RUBBER), (0.0, -0.0093, 0.0065, C_BLUE)]
LOOM_R = 0.0093 + 0.0065   # bundle radius


def loom(m, points, cables=LOOM_POWER, fillet=0.05, ref=(0.0, 0.0, 1.0), segs=7, frame=None):
    """Cable bundle along a polyline; returns the dense centre path and its frames (for ties and clamps)."""
    centre = fillet_path(points, fillet, 6) if fillet > 0 else np.asarray([vec(p) for p in points])
    tangents, normals, binormals = frames_along(centre, ref)
    for on, ob, radius, mat in cables:
        path = centre + on * normals + ob * binormals
        m.tube(mat, path, radius, segs, 0.0, frame)
    return centre, tangents


def cable_tie(m, centre, axis, bundle_r=LOOM_R, frame=None, mat=GRAPHITE):
    ring_band(m, mat, centre, axis, bundle_r + 0.0002, bundle_r + 0.0018, 0.0045, 10, frame)


# ----------------------------------------------------------------------------- loom hardware
def p_clamp(m, y, loom_x, loom_z, bundle_r=LOOM_R, frame=None, tail=-1):
    """Cushioned P-clamp holding a vertical loom (axis local y at x = loom_x, z = loom_z), its doubled tail
    bolted on a hex standoff that stands on the host surface (z = 0). `tail` is the side of the standoff."""
    r_in = bundle_r + 0.0005
    ring_band(m, RUBBER, (loom_x, y, loom_z), (0, 1, 0), r_in, r_in + 0.0025, 0.0155, 12, frame)
    ring_band(m, ALU, (loom_x, y, loom_z), (0, 1, 0), r_in + 0.0025, r_in + 0.0040, 0.0125, 12, frame)
    top = loom_z - r_in - 0.0025                  # tail top = band inner bottom
    bolt_x = loom_x + tail * (bundle_r + 0.019)
    x_far = loom_x + tail * (bundle_r + 0.029)
    m.box(ALU, ((loom_x + x_far) / 2, y, top - 0.0016), (abs(x_far - loom_x), 0.0125, 0.0032), 0.0006, frame)
    m.cyl(ALU, (bolt_x, y, GAP), (bolt_x, y, GAP + 0.0012), 0.0085, 10, 0.0, frame)
    m.cyl(ALU, (bolt_x, y, GAP + 0.0012), (bolt_x, y, top - 0.0032), 0.0055, 6, 0.0, frame)
    m.cyl(ANO, (bolt_x, y, top), (bolt_x, y, top + 0.0032), 0.0048, 6, 0.0006, frame)


def round_connector(m, base, axis, frame=None, cable_r=0.0065, shell=ANO_CLEAR, nut=ANO_CLEAR):
    """Panel receptacle with its mated plug: square flange, shell, coupling nut with grip rings, tapered
    backshell and strain-relief boot. Returns the point (in the caller's frame) where the cable leaves."""
    base, axis = vec(base), unit(axis)
    f = compose(frame if frame is not None else np.eye(4), _align_y(base, axis))
    m.box(ALU, (0, GAP + 0.0015, 0), (0.024, 0.003, 0.024), 0.0006, f)
    solid_lathe(m, shell, (0, 0, 0), (0, 1, 0), [(0.0085, GAP + 0.003), (0.0085, 0.010)], 12, f)
    solid_lathe(m, nut, (0, 0, 0), (0, 1, 0), [(0.0095, 0.010), (0.0105, 0.011), (0.0105, 0.021), (0.0095, 0.022)], 12, f)
    ring_band(m, ANO, (0, 0.0155, 0), (0, 1, 0), 0.0100, 0.0109, 0.0016, 10, f)
    solid_lathe(m, ANO, (0, 0, 0), (0, 1, 0), [(0.0092, 0.022), (0.0092, 0.029), (0.0072, 0.038),
                                               (cable_r + 0.0016, 0.046)], 10, f)
    solid_lathe(m, RUBBER, (0, 0, 0), (0, 1, 0), [(cable_r + 0.0016, 0.046), (cable_r + 0.0006, 0.060)], 10, f)
    return base + axis * 0.058


def quarter_turn(m, x, y, z, frame=None, head=ANO):
    """Quarter-turn (cam-lock) fastener: cup washer, domed head and a screwdriver slot, all set in the locked
    (horizontal) position."""
    m.cyl(ALU, (x, y, z - 0.0005), (x, y, z + 0.0015), 0.0075, 10, 0.0, frame)
    m.cyl(head, (x, y, z + 0.0015), (x, y, z + 0.0038), 0.0058, 10, 0.0009, frame)
    m.box(GRAPHITE, (x, y, z + 0.0038), (0.0088, 0.0013, 0.0014), 0.0, frame)


def barrel_hinge(m, x, y0, y1, z, frame=None, knuckles=5):
    """Exposed barrel hinge: alternating knuckles on a pin, two leaves."""
    length = y1 - y0
    k = length / knuckles
    for i in range(knuckles):
        a, b = y0 + i * k + 0.001, y0 + (i + 1) * k - 0.001
        m.cyl(ALU, (x, a, z), (x, b, z), 0.0048, 8, 0.0006, frame)
    m.cyl(ALU, (x, y0 - 0.002, z), (x, y1 + 0.002, z), 0.0018, 6, 0.0, frame)
    m.box(ALU, (x + 0.009, (y0 + y1) / 2, z - 0.0035), (0.012, length, 0.0016), 0.0004, frame)
    m.box(ALU, (x - 0.009, (y0 + y1) / 2, z - 0.0035), (0.012, length, 0.0016), 0.0004, frame)


def junction_box(m, atlas, centre_y, size, frame=None, label="c_hv"):
    """Flange junction box: two mounting ears on a backing strip, grey body, gasket line, bevelled lid held by
    four quarter-turns on its side edges, caution label. Front-view frame on the host surface.
    Returns (top y, bottom y, lid z)."""
    w, h, d = size
    for ey in (centre_y - h / 2 - 0.012, centre_y + h / 2 + 0.012):
        m.box(ALU, (0, ey, GAP + 0.0015), (0.034, 0.03, 0.003), 0.0006, frame)
        m.cyl(ANO, (0, ey, GAP + 0.0028), (0, ey, GAP + 0.0056), 0.0045, 6, 0.0005, frame)
    m.box(ALU, (0, centre_y, GAP + 0.0014), (0.026, h + 0.024, 0.0028), 0.0006, frame)
    z0 = GAP + 0.0027
    m.box(GRAY, (0, centre_y, z0 + d / 2), (w, h, d), 0.006, frame)
    front = z0 + d
    m.frame(RUBBER, (0, centre_y, front - 0.0005), w - 0.010, h - 0.010, 0.0035, 0.0030, 0.006, frame)
    m.plate(WHITE, (0, centre_y, front + 0.0005), w - 0.018, h - 0.018, 0.006, 0.006, 0.0025, frame)
    lid = front + 0.0065
    for sx in (-1, 1):
        for dy in (-0.04, 0.04):
            quarter_turn(m, sx * (w / 2 - 0.016), centre_y + dy, lid - 0.0005, frame)
    if label:
        m.decal(atlas, label, (0, centre_y + 0.005, lid), frame, width=0.07)
    return centre_y + h / 2, centre_y - h / 2, lid


# ----------------------------------------------------------------------------- service panel
def service_panel(m, atlas, w, h, cy, frame=None, hinge_side=-1, part="pn_11", note="fastener_note"):
    """Flush service access panel: stepped bevel bezel, black gasket line, bevelled door, two barrel hinges on
    one side and four quarter-turn fasteners on the others. Centred on local x = 0, bottom-up from the host."""
    bar = 0.026
    m.frame(GRAPHITE, (0, cy, GAP), w, h, bar, 0.006, 0.018, frame)
    m.frame(GRAPHITE, (0, cy, GAP + 0.006), w - 0.008, h - 0.008, bar - 0.008, 0.004, 0.015, frame)
    m.box(GRAPHITE, (0, cy, GAP + 0.001), (w - 2 * bar + 0.004, h - 2 * bar + 0.004, 0.002), 0.0, frame)
    dw, dh = w - 2 * bar - 0.006, h - 2 * bar - 0.006
    m.frame(RUBBER, (0, cy, GAP + 0.002), dw + 0.005, dh + 0.005, 0.003, 0.009, 0.010, frame)
    m.plate(WHITE, (0, cy, GAP + 0.002), dw, dh, 0.010, 0.010, 0.003, frame)
    door = GAP + 0.012
    hx = hinge_side * (dw / 2 + 0.001)
    for hy in (cy - dh / 2 + 0.05, cy + dh / 2 - 0.13):
        barrel_hinge(m, hx, hy, hy + 0.08, door + 0.0012, frame)
    fx = -hinge_side * (dw / 2 - 0.022)
    for fy in (cy - dh / 2 + 0.05, cy + dh / 2 - 0.05):
        quarter_turn(m, fx, fy, door, frame)
    for fy in (cy - dh / 2 + 0.022, cy + dh / 2 - 0.022):
        quarter_turn(m, -hinge_side * dw * 0.12, fy, door, frame)
    if note:
        m.decal(atlas, note, (0.0, cy + dh / 2 - 0.085, door), frame, width=min(0.14, dw - 0.06))
    if part:
        m.decal(atlas, part, (0.0, cy - dh / 2 + 0.085, door), frame, width=min(0.12, dw - 0.07))
    m.decal(atlas, "barcode_07", (0.0, cy, door), frame, 0.7)


# ----------------------------------------------------------------------------- engineering cluster
def frame_loom_cluster(atlas, label="c_hv", trough_z=-0.116, conn_labels=(4, 5, 6, 7)):
    """Cable loom dropping down a ring-frame flange (front frame on the flange face, local x = along the ship).
    A grommet in the flange under the shoulder, four cushioned P-clamps on standoffs, cable ties, a junction box
    with four plug connectors (backshells) on top, two outgoing feeders with backshells dropping beside the flange
    edge into the deck cable trough. `trough_z` is the trough centre in this frame (behind the flange face)."""
    m = Model("FrameLoom")
    jb_y, jb_size = 0.83, (0.15, 0.20, 0.068)
    top_y, bottom_y, lid = junction_box(m, atlas, jb_y, jb_size, None, label)
    jb_mid_z = GAP + 0.0027 + jb_size[2] / 2
    lx, lz = 0.017, 0.0365                    # loom centre over the flange
    bundle_start, grommet_y = top_y + 0.24, 2.50
    centre_pts = [(lx, bundle_start, lz), (lx, grommet_y, lz), (lx, grommet_y, GAP + 0.003)]
    loom(m, centre_pts, LOOM_POWER, 0.035, (0.0, 0.0, 1.0))
    # fan-out, connectors ordered by where their cable sits in the bundle so no two cables cross
    order = (3, 0, 2, 1)
    conn_x = (-0.042, -0.014, 0.014, 0.042)
    _, normals, binormals = frames_along(densify(centre_pts[:2], 0.5), (0.0, 0.0, 1.0))
    n0, b0 = normals[0], binormals[0]
    for i, cx in enumerate(conn_x):
        end = round_connector(m, (cx, top_y, jb_mid_z), (0, 1, 0))
        on, ob, radius, mat = LOOM_POWER[order[i]]
        target = vec(lx, bundle_start, lz) + on * n0 + ob * b0
        path = [end, end + vec(0, 0.03, 0), vec(target[0], bundle_start - 0.07, target[2]), target]
        m.tube(mat, fillet_path(path, 0.03, 6), radius, 7)
        m.decal(atlas, "conn_%02d" % conn_labels[i], (cx, top_y - 0.022, lid), None, width=0.022)
    for y in (bundle_start + 0.02, 1.44, 1.82, 2.20):
        cable_tie(m, (lx, y, lz), (0, 1, 0))
    for y in (1.25, 1.63, 2.01, 2.39):
        p_clamp(m, y, lx, lz, LOOM_R, None, -1)
    ring_band(m, RUBBER, (lx, grommet_y, GAP + 0.003), (0, 0, 1), LOOM_R + 0.0003, 0.0178, 0.006, 14)
    # outgoing feeders: plugs under the box, cables beside the flange edge down into the trough grommets
    for k, cx in enumerate((-0.042, 0.042)):
        end = round_connector(m, (cx, bottom_y, jb_mid_z), (0, -1, 0), None, 0.0065)
        mat = (C_RED, RUBBER)[k]
        path = [end, end + vec(0, -0.04, 0), vec(cx, 0.42, jb_mid_z), vec(cx, 0.24, trough_z), vec(cx, 0.1215, trough_z)]
        m.tube(mat, fillet_path(path, 0.08, 6), 0.0065, 7)
        ring_band(m, RUBBER, (cx, 0.1235, trough_z), (0, 1, 0), 0.0068, 0.0125, 0.006, 12)
        m.decal(atlas, "conn_%02d" % (conn_labels[-1] + 1 + k), (cx, bottom_y + 0.022, lid), None, width=0.022)
    return m


def access_panel_cluster(atlas, hinge_side, part="pn_11"):
    """One service access panel on a bulkhead wing (front frame on the wing face; pivot at deck level)."""
    m = Model("AccessPanel")
    service_panel(m, atlas, 0.34, 0.70, 1.40, None, hinge_side, part)
    return m
