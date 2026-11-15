"""
ih_systems - life-support and airlock hero props for INTERIOR-HERO-V01 (original designs).

IH_EclssRack    the CO2 scrubber rack at LifeSupportRepair. Same envelope and placement frame as eclss_rack() in
                a7_ship.py (front_frame((2.02, 0, 8.1), (-1, 0, 0)), 1.6 x 2.3 x 0.58 m, face at x = 1.73, bay back
                plate at x = 1.99). The three ScrubberCanister_A/B/C scene objects stay; the manifold tees, union
                nuts and outlet risers are rebuilt on their axes (x = 1.855, z = 8.24 / 7.99 / 7.74, base y = 0.80).
IH_AirlockPanel the pressure station on the inner face of the aft airlock bulkhead (z = 20.92), rebuilt around the
                driven AirlockGauge_Needle (pivot (1.45, 1.70, 20.832)), plus a procedures placard beside it.

Each prop names the old triangles to remove from the merged ship meshes ("carve": every triangle whose three
vertices lie inside the box, ship space). The boxes were checked against the real FBX meshes in Blender.
"""
import numpy as np

from ihkit import (Prop, at, compose, rotation, front_frame, PAINT, GRAPH, ALU, CHROME, GOLD, ANO_RED, ANO_BLUE, AMBER, RED,
                   FAB_WHITE, STRAP, RUBBER, PLASTIC_W, PLASTIC_D, GLASS, LAMP, CABLE_O, CABLE_B, CABLE_Y)

# ----------------------------------------------------------------------------- scrubber rack (front view)
ZF = 0.29                      # rack face
ZB = 0.03                      # back plate of the service bay
CZ = ZB + 0.135                # canister axis
CAN_X = (-0.14, 0.11, 0.36)    # canisters A, B, C
BASE_Y = 0.80                  # canister base port
OPEN = (-0.54, 0.52, 0.54, 1.84)


def _fan(p, fx, fy, z):
    """Axial fan behind a wire finger guard, facing +z."""
    steps = 5 if p.lod == 0 else 2
    p.frame(GRAPH, (fx, fy, z), 0.14, 0.14, 0.012, 0.012, 0.07, None, steps)
    p.rcyl(PLASTIC_D, (fx, fy, z + 0.0005), (fx, fy, z + 0.001), 0.058, 16, 0.0, None, (False, True))
    if p.d(1):
        p.rcyl(ALU, (fx, fy, z + 0.001), (fx, fy, z + 0.014), 0.018, 10, 0.002, None, (False, True))
    if p.d(0):
        for k in range(7):
            blade = compose(at(fx, fy, z + 0.006), rotation((0, 0, 1), k * 360.0 / 7.0), rotation((1, 0, 0), 28))
            p.box(ALU, (0.037, 0, 0), (0.04, 0.024, 0.0016), 0.0, blade)
        for ring in (0.084, 0.118):
            p.frame(CHROME, (fx, fy, z + 0.014), ring, ring, 0.003, 0.003, ring / 2, None, 5)
        p.box(CHROME, (fx, fy, z + 0.0155), (0.124, 0.003, 0.003), 0.0)
        p.box(CHROME, (fx, fy, z + 0.0155), (0.003, 0.124, 0.003), 0.0)


def _valve(p, x, y, z, along_x=True, m=None):
    """Quarter-turn isolation valve on a line running along x; the amber lever lies along the line (open)."""
    p.rcyl(ANO_BLUE, (x - 0.026, y, z), (x + 0.026, y, z), 0.036, 12, 0.004, m)
    if p.d(1):
        p.rcyl(ALU, (x, y, z + 0.03), (x, y, z + 0.072), 0.008, 8, 0.0, m)
        p.wbox(AMBER, (x + (0.04 if along_x else 0.0), y + (0.0 if along_x else 0.04), z + 0.078), (0.12, 0.024, 0.012) if along_x else (0.024, 0.12, 0.012),
               0.004, m)


def eclss_rack(lod):
    p = Prop("EclssRack", lod)
    x0, y0, x1, y1 = OPEN
    cy, h, w = (y0 + y1) / 2, y1 - y0, x1 - x0
    steps = 5 if lod == 0 else 2
    # ---- shell: plinth block, head block, two cheeks, graphite bay liner
    p.wbox(PAINT, (0, y0 / 2, 0), (1.6, y0, 0.58), 0.006)
    p.wbox(PAINT, (0, (y1 + 2.3) / 2, 0), (1.6, 2.3 - y1, 0.58), 0.006)
    p.box(PAINT, ((x0 - 0.8) / 2, cy, 0), (x0 + 0.8, h, 0.58), 0.0)
    p.box(PAINT, ((x1 + 0.8) / 2, cy, 0), (0.8 - x1, h, 0.58), 0.0)
    p.box(GRAPH, (0, cy, ZB - 0.01), (w, h, 0.02), 0.0)
    for sx, x in ((1, x0), (-1, x1)):
        p.box(GRAPH, (x + sx * 0.003, cy, (ZB + ZF) / 2), (0.006, h, ZF - ZB), 0.0)
    for sy, y in ((1, y0), (-1, y1)):
        p.box(GRAPH, (0, y + sy * 0.003, (ZB + ZF) / 2), (w, 0.006, ZF - ZB), 0.0)
    # rack rails with fasteners, bezel flange and raised lip around the bay
    p.frame(GRAPH, (0, 1.15, ZF), 1.58, 2.28, 0.045, 0.012, 0.02, None, 3 if lod == 0 else 1)
    p.frame(PAINT, (0, cy, ZF), w + 0.13, h + 0.13, 0.08, 0.016, 0.13, None, steps)
    p.frame(GRAPH, (0, cy, ZF + 0.016), w + 0.085, h + 0.085, 0.05, 0.010, 0.105, None, steps)
    if p.d(0):
        for sx in (-1, 1):
            for y in np.arange(0.16, 2.2, 0.22):
                p.bolt(sx * 0.7675, y, ZF + 0.012, None, 0.006)
        for bx in np.linspace(x0 + 0.12, x1 - 0.12, 5):
            for by in (y0 - 0.054, y1 + 0.054):
                p.bolt(bx, by, ZF + 0.016, None, 0.0055)
        for by in np.linspace(y0 + 0.12, y1 - 0.12, 6):
            for bx in (x0 - 0.054, x1 + 0.054):
                p.bolt(bx, by, ZF + 0.016, None, 0.0055)
    p.box(GRAPH, (0, 0.04, ZF + 0.004), (1.46, 0.062, 0.008), 0.0)
    for hx in (-0.45, 0.45):
        p.label("haz_band", (hx, 0.04, ZF + 0.008), None, 0.30)
    # ---- head block: name plate, flow stencil, status display
    p.rplate(PAINT, (-0.29, 2.10, ZF), 0.90, 0.29, 0.016, 0.03, 0.004)
    p.label("ecl_title", (-0.29, 2.15, ZF + 0.016), None, 0.62)
    p.label("ecl_flow", (-0.29, 2.032, ZF + 0.016), None, 0.44)
    if p.d(0):
        for sx in (-1, 1):
            for sy in (-1, 1):
                p.captive(-0.29 + sx * 0.415, 2.10 + sy * 0.11, ZF + 0.016)
    p.wbox(GRAPH, (0.44, 2.07, ZF + 0.03), (0.40, 0.32, 0.06), 0.01)
    p.frame(PLASTIC_D, (0.44, 2.095, ZF + 0.06), 0.34, 0.235, 0.022, 0.014, 0.012, None, 3 if lod == 0 else 1)
    p.box(PLASTIC_D, (0.44, 2.095, ZF + 0.061), (0.31, 0.20, 0.002), 0.0)
    p.screen("eclss_status", (0.44, 2.095, ZF + 0.0625), 0.296)
    if p.d(0):
        for i in range(4):
            p.box(ALU, (0.335 + i * 0.07, 1.945, ZF + 0.0625), (0.04, 0.016, 0.005), 0.0)
        p.bolts((0.24, 1.91, 0.64, 2.23), ZF + 0.06, None, 0.014, 0.0035, ALU)
    # ---- left cheek: translation aid, fire port, hook-and-loop, part number
    p.handrail((-0.69, 1.00), (-0.69, 1.56), ZF, None, 0.06, 0.013)
    if p.d(1):
        p.rcyl(GRAPH, (-0.69, 1.685, ZF), (-0.69, 1.685, ZF + 0.006), 0.032, 14, 0.001, None, (False, True))
        p.rcyl(RED, (-0.69, 1.685, ZF + 0.006), (-0.69, 1.685, ZF + 0.009), 0.022, 12, 0.001, None, (False, True))
        p.label("fire_port", (-0.69, 1.775, ZF), None, 0.07)
        p.label("pn_d", (-0.69, 0.60, ZF), None, 0.09)
        p.velcro("velcro_loop", -0.69, 0.80, ZF, None, 0.05)
        p.velcro("velcro_hook", -0.69, 0.735, ZF, None, 0.05)
    # ---- right cheek: insulated return duct to the overhead drop, in saddle clamps
    dz = ZF + 0.036
    p.rcyl(FAB_WHITE, (0.69, 0.46, dz), (0.69, 2.245, dz), 0.032, 14, 0.0, None, (True, False))
    p.rcyl(ALU, (0.69, 2.235, dz), (0.69, 2.29, dz), 0.037, 14, 0.003)
    p.wbox(ALU, (0.69, 0.445, ZF + 0.035), (0.10, 0.05, 0.07), 0.006)
    if p.d(1):
        for y in (0.56, 1.02, 1.50, 2.12):
            p.wbox(ALU, (0.69, y, ZF + 0.02), (0.10, 0.026, 0.04), 0.004)
        for y in np.arange(0.70, 2.2, 0.27):
            p.rcyl(STRAP, (0.69, y - 0.011, dz), (0.69, y + 0.011, dz), 0.0345, 14, 0.0)
    # ---- plinth block: filter drawer with pulls, captive fasteners and inlet louvre
    p.rplate(PAINT, (0, 0.255, ZF), 1.48, 0.31, 0.016, 0.03, 0.004)
    for hx in (-0.52, 0.52):
        p.handle((hx - 0.07, 0.31), (hx + 0.07, 0.31), ZF + 0.016, None, 0.04, 0.007)
    if p.d(0):
        for sx in (-1, 1):
            for sy in (-1, 1):
                p.captive(sx * 0.705, 0.255 + sy * 0.12, ZF + 0.016)
    p.louvre((-0.30, 0.135, 0.30, 0.295), ZF + 0.030)
    p.label("ecl_filter", (0, 0.36, ZF + 0.016), None, 0.30)
    p.label("pn_d", (0.52, 0.185, ZF + 0.016), None, 0.09)
    # ---- service bay: saddles behind the three canisters
    if p.d(1):
        for x in CAN_X:
            for by in (BASE_Y + 0.22, BASE_Y + 0.54):
                p.wbox(ALU, (x, by, ZB + 0.02), (0.17, 0.036, 0.04), 0.004)
    # fan pack A / B feeding the inlet manifold
    p.wbox(PAINT, (-0.405, 0.70, 0.135), (0.25, 0.30, 0.21), 0.006)
    _fan(p, -0.405, 0.775, 0.24)
    _fan(p, -0.405, 0.625, 0.24)
    p.label("ecl_fan", (-0.405, 0.885, ZB), None, 0.10)
    p.rcyl(ALU, (-0.28, 0.70, CZ), (-0.262, 0.70, CZ), 0.036, 12, 0.003)
    # inlet manifold along the bottom: isolation valve, a tee and union nut under every canister
    p.rcyl(CHROME, (-0.27, 0.70, CZ), (0.46, 0.70, CZ), 0.026, 12, 0.0)
    p.rcyl(CHROME, (0.46, 0.70, CZ), (0.495, 0.70, CZ), 0.034, 12, 0.004)
    _valve(p, -0.215, 0.70, CZ)
    p.label("ecl_valve", (-0.215, 0.60, ZB), None, 0.07)
    for x in CAN_X:
        p.rcyl(CHROME, (x, 0.70, CZ), (x, BASE_Y, CZ), 0.03, 12, 0.0)
        if p.d(1):
            p.rcyl(CHROME, (x - 0.042, 0.70, CZ), (x + 0.042, 0.70, CZ), 0.034, 12, 0.004)
            p.cyl(ALU, (x, BASE_Y - 0.034, CZ), (x, BASE_Y - 0.004, CZ), 0.042, 6, 0.004)
    for ax in (-0.015, 0.235):
        p.label("util_arrow", (ax, 0.61, ZB), None, 0.05)
    # outlet header across the top, through the bay wall to the return duct; outlet isolation valve
    hy = BASE_Y + 0.83
    p.rcyl(CHROME, (CAN_X[0] - 0.03, hy, CZ), (x1, hy, CZ), 0.018, 10, 0.0)
    p.rcyl(CHROME, (CAN_X[0] - 0.045, hy, CZ), (CAN_X[0] - 0.03, hy, CZ), 0.024, 10, 0.003)
    p.rcyl(ALU, (x1 - 0.024, hy, CZ), (x1 - 0.006, hy, CZ), 0.03, 10, 0.003)
    _valve(p, 0.455, hy, CZ)
    for x in CAN_X:
        p.rcyl(CHROME, (x, BASE_Y + 0.76, CZ), (x, hy, CZ), 0.015, 10, 0.0)
        if p.d(1):
            p.cyl(ALU, (x, BASE_Y + 0.765, CZ), (x, BASE_Y + 0.79, CZ), 0.026, 6, 0.003)
    # left column: bed control box with guarded toggles and connectors, chamber gauge, air sample port, sample line
    p.wbox(PAINT, (-0.405, 1.10, ZB + 0.03), (0.25, 0.26, 0.06), 0.006)
    p.toggle_row(-0.52, -0.29, 1.135, ZB + 0.06, 5, None, "ecl_leg", (1, 1, 1, 1, 0))
    if p.d(1):
        p.connector(-0.47, 1.02, ZB + 0.06, None, 0.012, cable=[(-0.47, 1.00, 0.155), (-0.47, 0.90, 0.155), (-0.47, 0.852, 0.13)],
                    cable_mat=CABLE_O, cable_radius=0.005)
        p.connector(-0.34, 1.02, ZB + 0.06, None, 0.012, cable=[(-0.34, 1.00, 0.155), (-0.34, 0.90, 0.155), (-0.34, 0.852, 0.13)],
                    cable_mat=CABLE_B, cable_radius=0.005)
    p.wbox(GRAPH, (-0.405, 1.37, ZB + 0.02), (0.14, 0.14, 0.04), 0.006)
    p.label("alk_gauge", (-0.405, 1.37, ZB + 0.04), None, 0.11)
    p.frame(ALU, (-0.405, 1.37, ZB + 0.04), 0.13, 0.13, 0.012, 0.012, 0.02, None, 3 if lod == 0 else 1)
    if p.d(1):
        needle = compose(at(-0.405, 1.37, ZB + 0.0445), rotation((0, 0, 1), 62))
        p.box(ANO_RED, (0.012, 0, 0), (0.046, 0.004, 0.002), 0.0, needle)
        p.rcyl(GRAPH, (-0.405, 1.37, ZB + 0.041), (-0.405, 1.37, ZB + 0.047), 0.006, 8, 0.0, None, (False, True))
        p.wbox(ALU, (-0.405, 1.57, ZB + 0.006), (0.17, 0.10, 0.012), 0.003)
        p.connector(-0.447, 1.57, ZB + 0.012, None, 0.014, cap=True)
        p.label("ecl_sample", (-0.367, 1.57, ZB + 0.012), None, 0.07)
        p.rcyl(GOLD, (-0.512, 1.23, ZB + 0.03), (-0.512, y1 - 0.006, ZB + 0.03), 0.008, 8, 0.0, None, (False, False))
        for y in (1.30, 1.72):
            p.wbox(ALU, (-0.512, y, ZB + 0.02), (0.036, 0.02, 0.04), 0.003)
    # right column: power and data cables in P-clamps
    if p.d(1):
        p.rtube(CABLE_O, [(0.503, y1 - 0.004, ZB + 0.014), (0.503, y0 + 0.004, ZB + 0.014)], 0.006, 6)
        p.rtube(CABLE_B, [(0.520, y1 - 0.004, ZB + 0.014), (0.520, y0 + 0.004, ZB + 0.014)], 0.006, 6)
        for y in (0.62, 0.98, 1.34, 1.74):
            p.wbox(ALU, (0.5115, y, ZB + 0.013), (0.05, 0.016, 0.026), 0.003)
    # work light under the head of the bay
    p.wbox(GRAPH, (0, y1 - 0.012, 0.20), (0.86, 0.024, 0.05), 0.003)
    p.box(LAMP, (0, y1 - 0.0255, 0.20), (0.80, 0.005, 0.03), 0.0)
    # labels on the raised lip: bed positions below, hot-surface caution above
    for x in CAN_X:
        p.label("ecl_bed", (x, y0 - 0.0175, ZF + 0.026), None, 0.10)
    p.label("ecl_hot", (-0.30, y1 + 0.0175, ZF + 0.026), None, 0.10)
    p.label("haz_band_short", (0.24, y1 + 0.0175, ZF + 0.026), None, 0.16)
    return p


# ----------------------------------------------------------------------------- airlock control panel (front view)
def airlock_panel(lod):
    """Seen from inside the airlock: local x to starboard, z out of the bulkhead (ship -z), origin (0, 0, 20.92)."""
    p = Prop("AirlockPanel", lod)
    px = 1.45
    steps = 3 if lod == 0 else 1
    # ---- chassis bridging the bulkhead stiffener, raised rim
    p.wbox(PAINT, (px, 1.4425, 0.035), (0.50, 1.025, 0.07), 0.008)
    p.frame(GRAPH, (px, 1.4425, 0.07), 0.50, 1.025, 0.018, 0.006, 0.012, None, steps)
    if p.d(0):
        for by in np.linspace(0.96, 1.925, 6):
            for sx in (-1, 1):
                p.bolt(px + sx * 0.241, by, 0.076, None, 0.0045, 0.003)
    p.label("alk_title", (px, 1.905, 0.07), None, 0.34)
    # ---- chamber pressure gauge behind the driven pointer (pivot (1.45, 1.70), needle plane z = 0.088)
    gy = 1.70
    p.wbox(GRAPH, (px, gy, 0.078), (0.27, 0.27, 0.016), 0.004)
    p.label("alk_gauge", (px, gy, 0.086), None, 0.20, 0.0006)
    p.frame(ALU, (px, gy, 0.086), 0.26, 0.26, 0.03, 0.022, 0.045, None, steps)
    if p.d(1):
        p.box(GLASS, (px, gy, 0.1055), (0.20, 0.20, 0.002), 0.0)
    if p.d(0):
        p.bolts((px - 0.13, gy - 0.13, px + 0.13, gy + 0.13), 0.108, None, 0.015, 0.004, GRAPH)
    # oxygen recharge quick-disconnect and service connectors beside the gauge
    if p.d(1):
        p.connector(1.2665, 1.755, 0.07, None, 0.014, cap=True)
        p.label("alk_o2", (1.2665, 1.675, 0.07), None, 0.075)
        p.connector(1.642, 1.63, 0.07, None, 0.013, cap=True)
        p.connector(1.642, 1.765, 0.07, None, 0.013, cable=[(1.642, 1.80, 0.125), (1.642, 1.90, 0.12), (1.642, 1.985, 0.085),
                                                             (1.642, 2.06, 0.045), (1.638, 2.2, 0.04)], cable_mat=RUBBER, cable_radius=0.0055)
    # ---- status display (schematic only)
    dy = 1.415
    p.wbox(GRAPH, (px, dy, 0.076), (0.32, 0.22, 0.012), 0.003)
    p.frame(PLASTIC_D, (px, dy, 0.082), 0.30, 0.20, 0.022, 0.014, 0.012, None, steps)
    p.screen("airlock_status", (px, dy, 0.0825), 0.256)
    if p.d(0):
        for i in range(4):
            p.box(ALU, (px - 0.09 + i * 0.06, 1.292, 0.0725), (0.036, 0.014, 0.005), 0.0)
    # ---- hatch interlock toggles
    p.toggle_row(1.28, 1.62, 1.225, 0.07, 5, None, "alk_leg", (1, 1, 0, 1, 0))
    # ---- guarded DEPRESS / REPRESS push-buttons and the equalise valve between them
    by = 1.055
    for bx, cap, guard, key in ((1.315, RED, ANO_RED, "alk_depress"), (1.585, ANO_BLUE, ALU, "alk_repress")):
        p.wbox(GRAPH, (bx, by, 0.076), (0.11, 0.10, 0.012), 0.003)
        p.rcyl(ALU, (bx, by, 0.082), (bx, by, 0.088), 0.036, 16, 0.002, None, (False, True))
        p.rcyl(cap, (bx, by, 0.088), (bx, by, 0.108), 0.028, 16, 0.006, None, (False, True))
        if p.d(1):
            for o in (-0.03, 0.03):
                p.rtube(guard, [(bx - 0.046, by + o, 0.082), (bx - 0.046, by + o, 0.126), (bx + 0.046, by + o, 0.126), (bx + 0.046, by + o, 0.082)],
                        0.0045, 6, 0.015)
        p.label(key, (bx, 1.135, 0.07), None, 0.115)
    p.rcyl(ALU, (px, 1.04, 0.07), (px, 1.04, 0.086), 0.03, 12, 0.003, None, (False, True))
    if p.d(1):
        p.rcyl(GRAPH, (px, 1.04, 0.086), (px, 1.04, 0.106), 0.014, 10, 0.0)
        p.wbox(AMBER, (px, 1.066, 0.112), (0.026, 0.11, 0.012), 0.004)
    p.label("alk_equal", (px, 0.968, 0.07), None, 0.07)
    # ---- conduits: down to a deck junction box, up through the bulkhead
    p.wbox(GRAPH, (px, 0.06, 0.045), (0.20, 0.12, 0.09), 0.006)
    for cx, radius, mat in ((1.42, 0.016, ALU), (1.475, 0.011, RUBBER)):
        p.rcyl(mat, (cx, 0.12, 0.035), (cx, 0.93, 0.035), radius, 10, 0.0, None, (False, False))
        if p.d(0):
            p.cyl(ALU, (cx, 0.912, 0.035), (cx, 0.93, 0.035), radius * 1.5, 6)
            p.cyl(ALU, (cx, 0.12, 0.035), (cx, 0.138, 0.035), radius * 1.5, 6)
    if p.d(1):
        for cy in (0.45, 0.90):
            p.wbox(ALU, (1.447, cy, 0.03), (0.11, 0.024, 0.052), 0.003)
        p.rtube(ALU, [(1.60, 1.955, 0.035), (1.60, 2.38, 0.035), (1.60, 2.44, 0.0)], 0.013, 8, 0.04)
        p.rcyl(RUBBER, (1.60, 2.44, 0.0), (1.60, 2.44, 0.008), 0.03, 12, 0.002, None, (False, True))
        p.wbox(ALU, (1.62, 2.2, 0.027), (0.09, 0.022, 0.054), 0.003)
    # ---- procedures placard to starboard of the stiffener: caution, cue card on a clip, pencil, hook-and-loop
    qx = 1.93
    p.wbox(PAINT, (qx, 1.42, 0.008), (0.30, 0.52, 0.016), 0.004)
    p.label("alk_warn", (qx, 1.60, 0.016), None, 0.26)
    p.box(PLASTIC_W, (qx - 0.02, 1.385, 0.0175), (0.165, 0.235, 0.003), 0.0)
    p.label("alk_steps", (qx - 0.02, 1.385, 0.019), None, 0.16)
    if p.d(1):
        p.wbox(CHROME, (qx - 0.02, 1.503, 0.022), (0.07, 0.022, 0.008), 0.002)
        p.rcyl(CABLE_Y, (qx + 0.105, 1.29, 0.0205), (qx + 0.105, 1.44, 0.0205), 0.0036, 6, 0.0)
        p.velcro("velcro_loop", qx - 0.02, 1.205, 0.016, None, 0.05)
        p.label("pn_a", (qx + 0.08, 1.20, 0.016), None, 0.09)
    if p.d(0):
        p.bolts((qx - 0.15, 1.16, qx + 0.15, 1.68), 0.016, None, 0.014, 0.0045)
    return p


def props():
    """name, build(lod) -> Prop in ship space, origin (ship space), views, carve boxes (ship space)."""
    rack = front_frame((2.02, 0, 8.1), (-1, 0, 0))
    bulkhead = front_frame((0, 0, 20.92), (0, 0, -1))

    def placed(builder, frame):
        def build(lod):
            placed_prop = Prop("placed", lod)
            placed_prop.instance(builder(lod), frame)
            return placed_prop
        return build

    return [
        {"name": "IH_EclssRack", "build": placed(eclss_rack, rack), "origin": (2.02, 0.0, 8.1),
         "carve": [{"target": "A7_Engineering", "min": [1.60, -0.005, 7.29], "max": [2.312, 2.32, 8.91]}],
         "views": [("eclss_rack", (0.75, 1.6, 8.1), (1.9, 1.2, 8.1), 80, [((0.5, 2.6, 8.1), 380, 1.8), ((1.2, 1.2, 9.2), 90, 1.0)]),
                   ("eclss_rack_bay", (1.05, 1.6, 8.75), (1.95, 1.05, 8.05), 62, [((0.6, 2.5, 8.4), 300, 1.5)])]},
        {"name": "IH_AirlockPanel", "build": placed(airlock_panel, bulkhead), "origin": (1.45, 1.4425, 20.92),
         "carve": [{"target": "A7_Airlock", "min": [1.26, 0.11, 20.80], "max": [1.64, 1.89, 20.925]}],
         "views": [("airlock_panel", (1.5, 1.6, 19.75), (1.58, 1.4, 20.92), 62, [((1.3, 2.5, 19.9), 220, 1.3), ((0.4, 1.7, 20.2), 60, 0.8)]),
                   ("airlock_panel_side", (0.75, 1.6, 20.05), (1.62, 1.35, 20.92), 60, [((0.9, 2.5, 19.9), 220, 1.3)])]},
    ]
