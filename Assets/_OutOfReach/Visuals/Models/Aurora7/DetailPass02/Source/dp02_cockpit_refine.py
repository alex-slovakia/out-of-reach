"""
dp02_cockpit_refine - CKP-REFINE (SHIP-DETAIL-02): close-range refinement of the AURORA-7 cockpit console and crew
seats. Original, procedural work.

Additive only. Every piece sits on an existing surface built by a7_ship.console() or a7_ship.seat(), keeps GAP
(0.5 mm) above it and stays clear of the rails, the controls, the screens, the seat occupant volumes and the
passage. Nothing here is operable: no lit parts, no new switches, no colliders.

Real-world scale, so the console stops reading as a toy:
  fastener heads   button heads 6-7 mm (r 3.0-3.5 mm), quarter-turn cups 15 mm (r 7.5 mm), flush rivets 5.6 mm
  trims and bezels 2-3 mm sheet: 44 mm seam battens, 40 x 40 mm edge angles, a J-nosing on the work-surface edge,
                   a C-channel lip on the glare-shield hood, 47 mm end posts on the instrument panel
  gaps             2.5 mm reveals between the sub-panels (the instrument panel's filler strips) and beside the posts

Coordinates. The console is written in its own front-view frame ("console-local"): a7_ship.build_cockpit() places
it with front_frame((0, 0, -1.95), (0, 0, 1)), so console-local (x, y, z) is ship (-x, y, -1.95 + z). Each cluster's
mount frame has the same orientation, so console-local reaches a cluster by one translation along z.
The seats are written in a7_ship.seat()'s frames (seat frame, then the 12 degree back frame).
"""
import math
import sys

import numpy as np

sys.dont_write_bytecode = True
from a7kit import Model, at, basis, compose, front_frame, rotation, signed_volume  # noqa: E402
from dp02_kit import ALU, ANO, ANO_CLEAR, GAP, quarter_turn  # noqa: E402

WORN = "DP02_PaintWorn"        # new: graphite paint with wear, chips and roughness variation (DetailPass02/Textures)
KNURL = "DP02_RubberKnurl"     # new: knurled rubber scuff strip

# ----------------------------------------------------------------------------- a7_ship.console() facts (console-local)
CONSOLE_Z = -1.95                      # console frame origin (ship z)
CAB_FACE_Z = 0.33                      # cabinet fronts (three boxes 0.95 x 0.86 x 0.66 at x -0.98, 0, 0.98)
TOP_FRONT_Z = 0.425                    # work-surface front face (box 3.1 x 0.05 x 0.85 at y 0.945)
TOP_Y = 0.97                           # work-surface top
PANEL_BASE_Z = -0.17                   # instrument panel frame origin (console-local z)
PANEL = compose(at(0.0, TOP_Y, PANEL_BASE_Z), rotation((1, 0, 0), -15))   # a7_ship.console() frame p
SWITCHES = compose(at(0.0, 0.9705, -0.02), rotation((1, 0, 0), -90))      # a7_ship.console() frame s (flat)
SIN15, COS15 = math.sin(math.radians(15.0)), math.cos(math.radians(15.0))

# ----------------------------------------------------------------------------- a7_ship.seat() facts
SEAT_X, SEAT_Z = 1.15, -0.45
BACK = compose(at(0.0, 0.47, -0.24), rotation((1, 0, 0), -12))            # seat back frame (12 degree recline)
SPINE_REAR_Z = -0.081                  # rear face of the spine box (0.16 x 0.82 x 0.03 at back-local z -0.066)


def seat_frame(side):
    return front_frame((side * SEAT_X, 0.0, SEAT_Z), (0, 0, -1))


def spine_frame(side):
    """Front-view frame on the rear face of a seat's spine: local z points out of the spine (aft and a little down),
    local y up the spine, local x is the right of a viewer standing behind the seat."""
    return compose(seat_frame(side), BACK, at(0.0, 0.0, SPINE_REAR_Z), rotation((0, 1, 0), 180))


def spine_pivot(side):
    """Deck point under the centre of the spine's rear face (ship coordinates, 4 decimals)."""
    p = spine_frame(side) @ np.array([0.0, 0.44, 0.0, 1.0])
    return (round(float(p[0]), 4), 0.0, round(float(p[2]), 4))


# ----------------------------------------------------------------------------- polygon tools
def _area(poly):
    p = np.asarray(poly, dtype=np.float64)
    return 0.5 * float(np.sum(p[:, 0] * np.roll(p[:, 1], -1) - np.roll(p[:, 0], -1) * p[:, 1]))


def triangulate(poly):
    """Ear clipping of a simple counter-clockwise polygon; returns index triples (counter-clockwise)."""
    pts = [tuple(map(float, p)) for p in poly]
    idx = list(range(len(pts)))

    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])

    tris = []
    guard = 0
    while len(idx) > 3:
        guard += 1
        if guard > 4 * len(pts) * len(pts):
            raise ValueError("polygon could not be triangulated")
        for k in range(len(idx)):
            i0, i1, i2 = idx[k - 1], idx[k], idx[(k + 1) % len(idx)]
            a, b, c = pts[i0], pts[i1], pts[i2]
            if cross(a, b, c) <= 1e-14:
                continue
            if any(cross(a, b, pts[j]) >= 0 and cross(b, c, pts[j]) >= 0 and cross(c, a, pts[j]) >= 0
                   for j in idx if j not in (i0, i1, i2)):
                continue
            tris.append((i0, i1, i2))
            idx.pop(k)
            break
    tris.append(tuple(idx))
    return tris


def inset(poly, d):
    """Mitred inward offset of a counter-clockwise polygon."""
    p = np.asarray(poly, dtype=np.float64)
    n = len(p)
    out = []
    for i in range(n):
        a, b, c = p[i - 1], p[i], p[(i + 1) % n]
        e1, e2 = (b - a) / np.linalg.norm(b - a), (c - b) / np.linalg.norm(c - b)
        n1, n2 = np.array([-e1[1], e1[0]]), np.array([-e2[1], e2[0]])
        mid = n1 + n2
        mid /= np.linalg.norm(mid)
        out.append(b + mid * d / max(0.3, float(np.dot(mid, n1))))
    return np.asarray(out)


def prism(outline, depth, chamfer=0.0):
    """Closed prism: a simple x/y outline extruded from z = 0 to z = depth, both end rims bevelled by `chamfer`.
    Works for non-convex outlines (L angles, C channels, stepped posts)."""
    p = np.asarray(outline, dtype=np.float64)
    if _area(p) < 0:
        p = p[::-1].copy()
    n = len(p)
    c = min(float(chamfer), depth * 0.45)
    if c > 1e-7:
        s = inset(p, c)
        layers = [(s, 0.0), (p, c), (p, depth - c), (s, depth)]
    else:
        layers = [(p, 0.0), (p, depth)]
    v = np.concatenate([np.column_stack([ring, np.full(n, z)]) for ring, z in layers], axis=0)
    tris = []
    for layer in range(len(layers) - 1):
        for k in range(n):
            k1 = (k + 1) % n
            a, b = layer * n + k, layer * n + k1
            d, e = (layer + 1) * n + k1, (layer + 1) * n + k
            tris += [(a, b, d), (a, d, e)]
    top = (len(layers) - 1) * n
    for i, j, k in triangulate(layers[0][0]):
        tris.append((top + i, top + j, top + k))
        tris.append((k, j, i))
    t = np.asarray(tris, dtype=np.int64)
    if signed_volume(v, t) < 0:
        t = t[:, ::-1]
    return v, t


def add_prism(m, mat, outline, depth, frame, chamfer=0.0):
    v, t = prism(outline, depth, chamfer)
    m.add(mat, v, t, frame)


def extrude_x(x0):
    """Frame for a prism whose outline is drawn in (z, y) and which is extruded along +x from x0."""
    return basis((x0, 0.0, 0.0), (0, 0, 1), (0, 1, 0), (1, 0, 0))


def extrude_y(y0):
    """Frame for a prism whose outline is drawn in (x, z) and which is extruded along +y from y0."""
    return basis((0.0, y0, 0.0), (1, 0, 0), (0, 0, 1), (0, 1, 0))


# ----------------------------------------------------------------------------- fixings
def button_screw(m, x, y, z, frame=None, r=0.0035, mat=ANO_CLEAR, segs=10):
    """Button-head cap screw (head 2r, 2.7 mm high) with a dark hex socket; base on z."""
    m.lathe(mat, (x, y, z), (0, 0, 1), [(r, 0.0), (r, 0.0010), (r * 0.82, 0.0021), (r * 0.5, 0.0027)], segs, frame,
            (False, True))
    m.cyl(ANO, (x, y, z + 0.00274), (x, y, z + 0.0030), r * 0.34, 6, 0.0, frame, (False, True))


def flush_rivet(m, x, y, z, frame=None, r=0.0028, mat=ALU):
    """Low oval-head rivet, 0.8 mm proud."""
    m.lathe(mat, (x, y, z), (0, 0, 1), [(r, 0.0), (r * 0.9, 0.0005), (r * 0.55, 0.0008)], 8, frame, (False, True))


def dzus(m, x, y, z, frame=None):
    """Quarter-turn panel fastener (kit piece; 15 mm cup washer, slotted head); cup base on z."""
    quarter_turn(m, x, y, z + 0.0005, frame)


# ----------------------------------------------------------------------------- 1. console cabinets
BATTEN_X = 0.49                        # cabinet seams (30 mm gaps between the three cabinets)
CAB_Y0, CAB_Y1 = 0.075, 0.905          # flat part of the cabinet fronts (boxes y 0.06 .. 0.92, 10 mm chamfer)


def edge_angle(m, side, frame):
    """40 x 40 mm painted edge angle (2.5 mm) wrapping the outer front edge of an end cabinet, five screws."""
    x_face, z_face = 1.455, CAB_FACE_Z
    outline = [(1.4180, z_face + GAP), (x_face + GAP, z_face + GAP), (x_face + GAP, 0.2950), (x_face + GAP + 0.0025, 0.2950),
               (x_face + GAP + 0.0025, z_face + GAP + 0.0013), (x_face + GAP + 0.0013, z_face + GAP + 0.0025),
               (1.4180, z_face + GAP + 0.0025)]
    if side < 0:
        outline = [(-x, z) for x, z in outline][::-1]
    add_prism(m, WORN, outline, CAB_Y1 - CAB_Y0, compose(frame, extrude_y(CAB_Y0)), 0.0004)
    for y in (0.12, 0.30, 0.49, 0.68, 0.86):
        button_screw(m, side * 1.437, y, z_face + GAP + 0.0025, frame)


def cabinets_cluster():
    """Seam battens over the two cabinet joints, edge angles on the two outer front edges, knurled rubber kick strips
    along the cabinet feet (flush rivets)."""
    m = Model("CKR_Cabinets")
    c = at(0.0, 0.0, -CAB_FACE_Z)                 # console-local -> cluster (pivot on the cabinet face plane)
    z0 = CAB_FACE_Z + GAP
    for side in (-1, 1):
        x = side * BATTEN_X
        m.box(WORN, (x, (CAB_Y0 + CAB_Y1) / 2, z0 + 0.0015), (0.044, CAB_Y1 - CAB_Y0, 0.003), 0.001, c)
        for y in (0.12, 0.30, 0.49, 0.68, 0.86):
            button_screw(m, x, y, z0 + 0.003, c)
        edge_angle(m, side, c)
    for x0, x1 in ((-1.405, -0.525), (-0.455, 0.455), (0.525, 1.405)):
        m.box(KNURL, ((x0 + x1) / 2, 0.092, z0 + 0.0015), (x1 - x0, 0.040, 0.003), 0.001, c)
        for x in (x0 + 0.022, (x0 + x1) / 2, x1 - 0.022):
            flush_rivet(m, x, 0.092, z0 + 0.003, c)
    return m


# ----------------------------------------------------------------------------- 2. work-surface edge
RAIL_X = 1.30                          # front handrail standoffs (base plates 50 mm wide)
NOSING_SEGMENTS = ((-1.262, 1.262), (1.338, 1.536), (-1.536, -1.338))


def nosing_outline():
    """J-section brushed nosing on the work surface's front-top edge (2 mm sheet), drawn in (z, y)."""
    zf, yt = TOP_FRONT_Z + GAP, TOP_Y + GAP
    return [(0.3920, yt), (zf, yt), (zf, 0.9380), (zf + 0.0020, 0.9380), (zf + 0.0020, yt - 0.0012),
            (zf + 0.0010, yt + 0.0010), (zf - 0.0012, yt + 0.0020), (0.3930, yt + 0.0020), (0.3920, yt + 0.0010)]


def work_surface_cluster():
    """Brushed J-nosing along the work-surface edge (split around the handrail standoffs) with flush screws, painted
    end caps on both work-surface ends, and a pair of guard cheeks beside the existing red switch (inert)."""
    m = Model("CKR_WorkSurfaceEdge")
    w = at(0.0, 0.0, -TOP_FRONT_Z)                # console-local -> cluster (pivot on the work-surface front plane)
    outline = nosing_outline()
    for x0, x1 in NOSING_SEGMENTS:
        add_prism(m, ALU, outline, x1 - x0, compose(w, extrude_x(x0)), 0.0005)
    top = TOP_Y + GAP + 0.0020
    for x in (-1.2, -0.9, -0.6, -0.3, 0.0, 0.3, 0.6, 0.9, 1.2, -1.437, 1.437):
        button_screw(m, 0.0, 0.0, 0.0, compose(w, at(x, top, 0.409), rotation((1, 0, 0), -90)), 0.0025)
    for side in (-1, 1):
        xc = side * (1.55 + GAP + 0.00125)
        m.box(WORN, (xc, 0.945, 0.0), (0.0025, 0.023, 0.80), 0.0008, w)
        for z in (-0.30, 0.0, 0.30):
            button_screw(m, 0.0, 0.0, 0.0, compose(w, at(side * (1.55 + GAP + 0.0025), 0.945, z),
                                                   rotation((0, 1, 0), side * 90)), 0.0025)
    # guard cheeks either side of the red switch (s-frame: x across, y away from the crew, z up)
    cheek = [(0.000, 0.0085), (0.090, 0.0085), (0.090, 0.046), (0.078, 0.058), (0.012, 0.058), (0.000, 0.048)]
    for x0 in (0.691, 0.785):
        add_prism(m, WORN, cheek, 0.004, compose(w, SWITCHES, basis((x0, 0.0, 0.0), (0, 1, 0), (0, 0, 1), (1, 0, 0))),
                  0.0006)
    return m


# ----------------------------------------------------------------------------- 3. instrument panel
DISPLAYS = (-0.98, 0.0, 0.98)          # display frames 0.72 x 0.47 (30 mm bar, 22 mm deep) at p-y 0.27
KEY_COLUMNS = (-0.49, 0.49)            # lit-key columns 0.16 x 0.44 x 0.012
FILLERS = ((0.3625, 0.4075), (0.5725, 0.6175), (1.3425, 1.4100))
POST_X0, POST_X1 = 1.5005, 1.5475      # end posts between the panel end (1.5) and the hood / work-surface ends (1.55)


def surface_y(pz, gap=GAP):
    """p-frame height of the work surface (plus gap) at p-z."""
    return (gap - pz * SIN15) / COS15


def post_outline():
    """End post section in (p-z, p-y): from the work surface up the panel's end to under the top rail and hood."""
    return [(-0.0700, surface_y(-0.07)), (0.0300, surface_y(0.03)), (0.0300, 0.5590), (0.0105, 0.5590),
            (0.0105, 0.5190), (-0.0700, 0.5190)]


def hood_lip_outline():
    """C-channel (2 mm) over the glare-shield hood's front edge, drawn in (p-z, p-y)."""
    return [(0.2120, 0.5905), (0.2405, 0.5905), (0.2405, 0.5595), (0.2120, 0.5595), (0.2120, 0.5575),
            (0.2425, 0.5575), (0.2425, 0.5925), (0.2120, 0.5925)]


def panel_cluster():
    """Instrument panel: painted filler strips with quarter-turn fasteners between the displays and key columns (2.5 mm
    reveals), a bottom filler with screws, bezel screws on the three display frames and the two key columns, painted
    end posts closing the panel ends under the top rail, and a brushed C-channel lip on the glare-shield hood."""
    m = Model("CKR_PanelBezels")
    p = compose(at(0.0, 0.0, -PANEL_BASE_Z), PANEL)    # cluster pivot under the panel base; then the console's frame p
    zf = GAP + 0.0015                                   # filler front
    for x0, x1 in FILLERS:
        for side in (-1, 1):
            xc = side * (x0 + x1) / 2
            m.box(WORN, (xc, (0.0375 + 0.5165) / 2, GAP + 0.00075), (x1 - x0, 0.5165 - 0.0375, 0.0015), 0.0005, p)
            for y in (0.065, 0.485):
                dzus(m, xc, y, zf, p)
    m.box(WORN, (0.0, 0.018, GAP + 0.00075), (2.82, 0.030, 0.0015), 0.0005, p)
    for x in (-1.2, -0.8, -0.49, -0.2, 0.2, 0.49, 0.8, 1.2):
        button_screw(m, x, 0.018, zf, p, 0.003)
    for cx in DISPLAYS:
        for x, y in ((-0.345, 0.05), (0.345, 0.05), (-0.345, 0.49), (0.345, 0.49), (-0.12, 0.49), (0.12, 0.49)):
            button_screw(m, cx + x, y, 0.022 + GAP, p)
    for cx in KEY_COLUMNS:
        for x in (-0.06, 0.06):
            for y in (0.064, 0.474):
                button_screw(m, cx + x, y, 0.012 + GAP, p, 0.003)
    outline = post_outline()
    for side in (-1, 1):
        x0 = POST_X0 if side > 0 else -POST_X1
        add_prism(m, WORN, outline, POST_X1 - POST_X0, compose(p, extrude_x(x0)), 0.002)
        for y in (0.10, 0.27, 0.44):
            button_screw(m, 0.0, 0.0, 0.0, compose(p, at(side * POST_X1, y, -0.02), rotation((0, 1, 0), side * 90)))
        for y in (0.12, 0.42):
            button_screw(m, side * (POST_X0 + POST_X1) / 2, y, 0.03, p)
    add_prism(m, ALU, hood_lip_outline(), 3.09, compose(p, extrude_x(-1.545)), 0.0004)
    return m


# ----------------------------------------------------------------------------- 4. seat spines
SPINE_SCREWS = [(sx * 0.060, y) for y in (0.10, 0.22, 0.52, 0.77) for sx in (-1, 1)]


def seat_spine_cluster(side, pivot):
    """Eight button-head screws holding the spine plate on the rear of a crew seat back, clear of the seat label
    (y 0.34-0.38), the grab bar (y 0.66) and the harness reel (from y 0.815)."""
    m = Model("CKR_SeatSpine")
    local = np.linalg.inv(front_frame(pivot, (0, 0, 1))) @ spine_frame(side)
    for x, y in SPINE_SCREWS:
        button_screw(m, x, y, GAP, local)
    return m
