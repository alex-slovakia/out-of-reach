"""
ih_cockpit - flight-deck hero props for INTERIOR-HERO-V01 (original designs): the two crew seats and the main console.

Both keep the footprint, the placement frames and the envelope of the V01 seat and console (a7_ship.py): the seats
at (-1.15 / +1.15, 0, -0.45) facing the console, the console at (0, 0, -1.95) with its crew side towards +z.
"""
import numpy as np

from ihkit import (Prop, vec, at, compose, rotation, front_frame, PAINT, GRAPH, ALU, CHROME, GOLD, ANO_RED, AMBER, RED,
                   FAB_SEAT, FAB_WHITE, STRAP, QUILT_SEAT, RUBBER, PLASTIC_W, PLASTIC_D, LED_A, LED_C, LAMP, CABLE_O, CABLE_B,
                   CABLE_Y)


def _pt(frame, x, y, z):
    return (frame @ np.array([x, y, z, 1.0]))[:3]


def seat(lod, label_key, rhc_side=1):
    """Crew seat, occupant facing local +z. The player meets it from behind, so the back shell carries the detail."""
    p = Prop("Seat", lod)
    # ---- floor rails, carriage, legs and stroke attenuators
    for sx in (-1, 1):
        p.wbox(GRAPH, (sx * 0.24, 0.012, 0.0), (0.056, 0.024, 0.96), 0.004)
        if p.d(0):
            for z in np.arange(-0.42, 0.43, 0.105):
                p.cyl(ALU, (sx * 0.24, 0.024, z), (sx * 0.24, 0.027, z), 0.007, 6, 0.0, None, (False, True))
        if p.d(1):
            p.wbox(ANO_RED, (sx * 0.24, 0.036, -0.445), (0.03, 0.024, 0.07), 0.004)          # rail lock lever
            for z in (-0.30, 0.30):
                p.wbox(ALU, (sx * 0.24, 0.045, z), (0.07, 0.042, 0.10), 0.006)                # carriage shoes
        p.rtube(ALU, [(sx * 0.24, 0.06, 0.30), (sx * 0.215, 0.415, 0.21)], 0.017, 10)
        p.rtube(ALU, [(sx * 0.24, 0.06, -0.30), (sx * 0.215, 0.415, -0.20)], 0.017, 10)
        a, b = vec(sx * 0.24, 0.07, -0.26), vec(sx * 0.217, 0.40, 0.13)
        mid = a + (b - a) * 0.58
        if p.d(1):
            p.rcyl(GOLD, a, mid, 0.021, 12, 0.003)
            p.rcyl(CHROME, mid, b, 0.011, 10)
            p.rcyl(RUBBER, mid - (b - a) * 0.02, mid + (b - a) * 0.035, 0.024, 12, 0.003)
            p.rcyl(ALU, a - vec(0.024, 0, 0), a + vec(0.024, 0, 0), 0.014, 8, 0.002)
            p.rcyl(ALU, b - vec(0.02, 0, 0), b + vec(0.02, 0, 0), 0.012, 8, 0.002)
    for z in (-0.30, 0.30):
        p.rtube(ALU, [(-0.24, 0.06, z), (0.24, 0.06, z)], 0.014, 8)
    # ---- pedestal: seat electronics, umbilical connectors on its rear face
    p.wbox(GRAPH, (0, 0.215, -0.03), (0.30, 0.25, 0.34), 0.012)
    rear = compose(at(0, 0, -0.20), rotation((0, 1, 0), 180))
    if p.d(1):
        p.wbox(ALU, (0, 0.235, 0.002), (0.26, 0.15, 0.004), 0.002, rear)
        p.connector(-0.07, 0.215, 0.004, rear, 0.017, cable=[(-0.07, 0.19, 0.11), (-0.20, 0.12, 0.13), (-0.335, 0.20, 0.02),
                                                             (-0.345, 0.50, -0.14), (-0.33, 0.645, -0.24)], cable_mat=CABLE_B, cable_radius=0.009)
        p.connector(0.07, 0.215, 0.004, rear, 0.013, cable=[(0.07, 0.19, 0.09), (0.19, 0.11, 0.11), (0.33, 0.18, 0.02),
                                                            (0.345, 0.48, -0.12), (0.33, 0.645, -0.22)], cable_mat=CABLE_O, cable_radius=0.0055)
        p.label("seat_o2", (-0.07, 0.285, 0.004), rear)
        p.label("seat_comm", (0.07, 0.285, 0.004), rear)
        p.bolts((-0.13, 0.16, 0.13, 0.31), 0.004, rear, 0.012, 0.004)
    # ---- seat pan and cushions
    p.wbox(GRAPH, (0, 0.432, 0.03), (0.52, 0.05, 0.52), 0.014)
    for sx in (-1, 1):
        p.wbox(GRAPH, (sx * 0.262, 0.47, 0.03), (0.016, 0.11, 0.50), 0.003)
    p.pillow(QUILT_SEAT, (0, 0.505, 0.04), (0.40, 0.10, 0.46), None, 4.5)
    for sx in (-1, 1):
        p.pillow(QUILT_SEAT, (sx * 0.215, 0.52, 0.04), (0.085, 0.12, 0.44), None, 3.5)
    p.pillow(QUILT_SEAT, (0, 0.50, 0.262), (0.42, 0.085, 0.085), None, 3.0)
    # ---- back, leaning 12 degrees
    back = compose(at(0, 0.47, -0.24), rotation((1, 0, 0), -12))
    p.wbox(GRAPH, (0, 0.47, -0.05), (0.52, 0.96, 0.03), 0.012, back)
    for sx in (-1, 1):
        p.wbox(GRAPH, (sx * 0.262, 0.42, 0.0), (0.016, 0.80, 0.10), 0.003, back)
    p.pillow(QUILT_SEAT, (0, 0.20, 0.012), (0.40, 0.36, 0.095), back, 4.5)
    p.pillow(QUILT_SEAT, (0, 0.585, 0.008), (0.40, 0.38, 0.085), back, 4.5)
    for sx in (-1, 1):
        p.pillow(QUILT_SEAT, (sx * 0.215, 0.40, 0.035), (0.085, 0.70, 0.12), back, 3.5)
    p.wbox(GRAPH, (0, 0.99, -0.04), (0.32, 0.27, 0.03), 0.012, back)
    p.pillow(QUILT_SEAT, (0, 0.99, 0.025), (0.26, 0.21, 0.10), back, 4.0)
    for sx in (-1, 1):
        p.pillow(QUILT_SEAT, (sx * 0.155, 0.99, 0.06), (0.06, 0.20, 0.15), back, 3.5)
        if p.d(1):
            p.rcyl(CHROME, (sx * 0.08, 0.83, -0.045), (sx * 0.08, 0.90, -0.045), 0.009, 8, 0.0, back)
    # ---- five-point harness
    if p.d(1):
        for sx in (-1, 1):
            p.box(STRAP, (sx * 0.095, 0.57, 0.066), (0.046, 0.56, 0.004), 0.0, back)
            p.wbox(ALU, (sx * 0.095, 0.86, 0.058), (0.062, 0.03, 0.022), 0.003, back)
            p.wbox(GRAPH, (sx * 0.095, 0.33, 0.071), (0.056, 0.046, 0.010), 0.002, back)
            p.box(ALU, (sx * 0.095, 0.33, 0.0775), (0.05, 0.008, 0.004), 0.0, back)
            p.strap([_pt(back, sx * 0.095, 0.30, 0.068), (sx * 0.03, 0.59, 0.165)], 0.046, None, 0.004, STRAP, (0, 0.6, 1))
            p.box(STRAP, (sx * 0.145, 0.562, 0.13), (0.21, 0.005, 0.046), 0.0)
            p.wbox(ALU, (sx * 0.255, 0.50, 0.11), (0.012, 0.05, 0.05), 0.003)
        p.box(STRAP, (0, 0.562, 0.255), (0.042, 0.005, 0.15), 0.0)
        p.rcyl(ALU, (0, 0.562, 0.17), (0, 0.582, 0.17), 0.037, 16, 0.004)
        p.rcyl(ANO_RED, (0, 0.582, 0.17), (0, 0.590, 0.17), 0.021, 12, 0.002)
    # ---- rear of the back: ribs, harness reel, grab bar, name plate, stowage pocket with bungees and a cue card
    for rx in (-0.17, 0.0, 0.17):
        p.wbox(GRAPH, (rx, 0.47, -0.075), (0.028, 0.86, 0.022), 0.003, back)
    for ry in (0.10, 0.80):
        p.wbox(GRAPH, (0, ry, -0.073), (0.48, 0.028, 0.018), 0.003, back)
    p.wbox(ALU, (0, 0.90, -0.078), (0.27, 0.075, 0.022), 0.004, back)
    p.rcyl(GRAPH, (-0.10, 0.90, -0.112), (0.10, 0.90, -0.112), 0.042, 16, 0.004, back)
    if p.d(1):
        for sx in (-1, 1):
            p.rcyl(ALU, (sx * 0.10, 0.90, -0.112), (sx * 0.116, 0.90, -0.112), 0.047, 16, 0.003, back)
        p.rtube(AMBER, [(-0.15, 0.715, -0.062), (-0.15, 0.715, -0.128), (0.15, 0.715, -0.128), (0.15, 0.715, -0.062)], 0.012, 8, 0.03, back)
        p.wbox(ALU, (0, 0.615, -0.088), (0.19, 0.064, 0.004), 0.0015, back)
    behind = compose(back, at(0, 0, -0.0905), rotation((0, 1, 0), 180))
    p.label(label_key, (0, 0.615, 0.0), behind, 0.17)
    p.pillow(FAB_SEAT, (0, 0.36, -0.096), (0.38, 0.30, 0.05), back, 5.0)
    if p.d(1):
        p.wbox(PLASTIC_W, (-0.07, 0.522, -0.098), (0.20, 0.06, 0.014), 0.002, back)       # manual in the pocket
        p.velcro("velcro_strip", 0.0, 0.485, 0.0312, behind, 0.20)
        p.box(PLASTIC_W, (0.085, 0.345, 0.0312), (0.108, 0.153, 0.0015), 0.0, behind)
        p.label("card_ascent", (0.085, 0.345, 0.0322), behind, 0.105)
        p.label("tape_spare", (-0.10, 0.28, 0.0312), behind, 0.075, turn=4)
        for s in (-1, 1):
            p.bungee([(s * -0.215, 0.54, -0.066), (s * -0.12, 0.44, -0.126), (s * 0.12, 0.27, -0.126), (s * 0.215, 0.18, -0.066)], back)
    # ---- armrests and hand controllers
    for sx in (-1, 1):
        x = sx * 0.31
        if p.d(1):
            p.rcyl(ALU, (x, 0.43, -0.10), (x, 0.655, -0.10), 0.015, 10)
            p.wbox(ALU, (sx * 0.285, 0.44, -0.10), (0.06, 0.03, 0.06), 0.004)
        p.wbox(ALU, (x, 0.667, 0.04), (0.058, 0.024, 0.38), 0.006)
        p.pillow(FAB_SEAT, (x, 0.697, -0.01), (0.07, 0.04, 0.26), None, 4.0)
        p.wbox(GRAPH, (x, 0.70, 0.205), (0.08, 0.052, 0.09), 0.008)
        top = compose(at(0, 0.7265, 0), rotation((1, 0, 0), -90))
        if sx == rhc_side:
            if p.d(1):
                p.rcyl(RUBBER, (x, 0.726, 0.205), (x, 0.752, 0.205), 0.021, 12, 0.004)
                p.rcyl(GRAPH, (x, 0.75, 0.205), (x, 0.80, 0.209), 0.009, 8)
                p.pillow(PLASTIC_D, (x, 0.848, 0.211), (0.036, 0.10, 0.042), None, 3.0, 10, 6)
            if p.d(0):
                p.wbox(ANO_RED, (x, 0.862, 0.236), (0.012, 0.03, 0.012), 0.002)
                p.rcyl(PLASTIC_W, (x, 0.898, 0.211), (x, 0.905, 0.211), 0.008, 8, 0.001)
                p.label("rhc", (x, -0.171, 0.0), top, 0.06, turn=180)
        else:
            if p.d(1):
                p.rcyl(ALU, (x, 0.726, 0.205), (x, 0.734, 0.205), 0.019, 10)
                p.rcyl(GRAPH, (x, 0.73, 0.205), (x, 0.778, 0.205), 0.010, 8)
                p.rcyl(RUBBER, (x - 0.042, 0.787, 0.205), (x + 0.042, 0.787, 0.205), 0.013, 10, 0.003)
            if p.d(0):
                p.label("thc", (x, -0.171, 0.0), top, 0.06, turn=180)
    # ---- foot rest with toe loops
    foot = compose(at(0, 0.165, 0.45), rotation((1, 0, 0), 18))
    p.wbox(ALU, (0, 0, 0), (0.44, 0.018, 0.20), 0.005, foot)
    if p.d(1):
        p.box(RUBBER, (0, 0.0105, 0), (0.40, 0.003, 0.16), 0.0, foot)
        for sx in (-1, 1):
            p.rtube(RUBBER, [(sx * 0.11 - 0.055, 0.01, -0.02), (sx * 0.11 - 0.055, 0.075, -0.02), (sx * 0.11 + 0.055, 0.075, -0.02),
                             (sx * 0.11 + 0.055, 0.01, -0.02)], 0.007, 6, 0.03, foot)
            p.rtube(ALU, [(sx * 0.20, 0.16, 0.40), (sx * 0.225, 0.40, 0.27)], 0.012, 8)
        p.label("foot_restraint", (0, -0.075, 0.0), compose(foot, at(0, 0.0125, 0), rotation((1, 0, 0), -90)), 0.14, turn=180)
    # ---- side stowage: beta-cloth pouch with a torch, hook-and-loop and a hand-written tape label
    if p.d(1):
        p.pillow(FAB_WHITE, (-0.287, 0.50, 0.07), (0.036, 0.15, 0.22), None, 4.0, 10, 6)
        p.rcyl(GOLD, (-0.292, 0.55, 0.02), (-0.292, 0.625, 0.02), 0.012, 10, 0.002)
        side = compose(at(0.2705, 0, 0), rotation((0, 1, 0), 90))
        p.velcro("velcro_hook", -0.13, 0.47, 0.0, side, 0.045)
        p.label("tape_mira", (0.02, 0.47, 0.0), side, 0.09, turn=-3)
    return p


def console(lod):
    """Main flight console, crew side towards local +z."""
    p = Prop("Console", lod)
    zf = 0.30
    p.wbox(GRAPH, (0, 0.04, -0.03), (2.98, 0.08, 0.54), 0.004)
    drawers = {-0.99: ("drawer_gnc", "esd", "pn_a"), 0.99: ("drawer_comm", "rf_haz", "pn_b")}
    for cx in (-0.99, 0.0, 0.99):
        p.wbox(PAINT, (cx, 0.50, -0.03), (0.97, 0.84, 0.66), 0.008)
    for cx, (title, caution, pn) in drawers.items():
        p.rplate(PAINT, (cx, 0.70, zf), 0.86, 0.36, 0.014, 0.012, 0.004)
        for hx in (-0.30, 0.30):
            p.handle((cx + hx, 0.61), (cx + hx, 0.79), zf + 0.014, None, 0.04, 0.007)
        if p.d(0):
            for sx in (-1, 1):
                for sy in (-1, 1):
                    p.captive(cx + sx * 0.405, 0.70 + sy * 0.15, zf + 0.014)
        p.label(title, (cx - 0.04, 0.765, zf + 0.014), None, 0.25)
        p.label(caution, (cx + 0.15, 0.64, zf + 0.014), None, 0.11)
        p.label(pn, (cx - 0.13, 0.63, zf + 0.014))
        p.louvre((cx - 0.43, 0.13, cx + 0.43, 0.46), zf + 0.014)
    # centre bay: connector patch panel over the flight computer drawer
    p.wbox(GRAPH, (0, 0.74, zf + 0.004), (0.86, 0.28, 0.008), 0.003)
    cables = {0: CABLE_O, 2: CABLE_B, 5: RUBBER}
    for i in range(6):
        x = -0.33 + i * 0.132
        if i in cables:
            p.connector(x, 0.715, zf + 0.008, None, 0.016, cable=[(x, 0.68, zf + 0.10), (x * 0.7, 0.34, zf + 0.13),
                                                                  (x * 0.45, 0.07, zf + 0.07), (x * 0.45, 0.03, zf - 0.03)],
                        cable_mat=cables[i], cable_radius=0.0065)
        else:
            p.connector(x, 0.715, zf + 0.008, None, 0.016, cap=(i != 3))
    p.label("j_row_a", (0, 0.835, zf + 0.008), None, 0.792)
    p.label("j_row_b", (0, 0.782, zf + 0.008), None, 0.792)
    p.label("cap_tether", (0.30, 0.628, zf + 0.008), None, 0.09)
    p.label("pn_c", (-0.30, 0.632, zf + 0.008))
    p.rplate(PAINT, (0, 0.34, zf), 0.86, 0.42, 0.014, 0.012, 0.004)
    for hx in (-0.30, 0.30):
        p.handle((hx, 0.24), (hx, 0.44), zf + 0.014, None, 0.04, 0.007)
    if p.d(0):
        for sx in (-1, 1):
            for sy in (-1, 1):
                p.captive(sx * 0.405, 0.34 + sy * 0.18, zf + 0.014)
    p.label("drawer_av", (0, 0.45, zf + 0.014), None, 0.25)
    p.label("esd", (0.13, 0.27, zf + 0.014), None, 0.11)
    p.label("pn_d", (-0.13, 0.25, zf + 0.014))
    # ---- work surface
    p.wbox(GRAPH, (0, 0.9475, 0.0), (3.10, 0.045, 0.86), 0.008)
    p.rcyl(AMBER, (-1.32, 0.948, 0.478), (1.32, 0.948, 0.478), 0.014, 10, 0.003)
    if p.d(1):
        for x in (-1.2, -0.6, 0.0, 0.6, 1.2):
            p.rcyl(ALU, (x, 0.948, 0.43), (x, 0.948, 0.478), 0.009, 8)
    p.pillow(FAB_SEAT, (0, 0.981, 0.365), (2.70, 0.026, 0.10), None, 6.0, 12, 6)
    top = compose(at(0, 0.97, 0), rotation((1, 0, 0), -90))      # local x = x, local y = -z (away from the crew), z up
    lit = {(0, 3): LED_C, (2, 9): LED_A, (4, 12): LED_C}
    for kx in (-0.74, 0.74):
        kb = compose(at(kx, 0.972, 0.16), rotation((1, 0, 0), 8))
        p.wbox(GRAPH, (0, 0.008, 0), (0.40, 0.016, 0.16), 0.004, kb)
        if p.d(0):
            for row in range(5):
                for col in range(14):
                    p.box(lit.get((row, col), PLASTIC_D), (-0.169 + col * 0.026, 0.021, -0.052 + row * 0.026), (0.021, 0.009, 0.021), 0.0, kb)
        elif p.d(1):
            p.box(PLASTIC_D, (0, 0.02, 0), (0.37, 0.008, 0.132), 0.0, kb)
        tx = kx + (-0.29 if kx < 0 else 0.29)
        if p.d(1):
            p.wbox(GRAPH, (tx, 0.982, 0.16), (0.10, 0.024, 0.12), 0.006)
            p.sphere(ANO_RED, (tx, 1.0, 0.168), 0.024, p.sg(12), 8)
            for bx in (-0.028, 0.028):
                p.box(PLASTIC_D, (tx + bx, 0.996, 0.115), (0.034, 0.006, 0.022), 0.0)
    p.toggle_row(-0.50, -0.06, 0.04, 0.0, 6, top, "leg_power", (1, 1, 0, 1, 1, 0))
    p.toggle_row(-0.50, -0.06, -0.075, 0.0, 6, top, "leg_rcs", (0, 0, 0, 0, 1, 0))
    p.toggle_row(0.06, 0.50, 0.04, 0.0, 6, top, "leg_eclss", (1, 1, 1, 1, 0, 1))
    p.toggle_row(0.06, 0.50, -0.075, 0.0, 6, top, "leg_light", (1, 1, 0, 0, 1, 0))
    p.knob(-0.20, -0.205, 0.0, top, 0.016, 0.022)
    p.label("knob_att", (-0.20, -0.243, 0.0), top)
    p.knob(-0.05, -0.205, 0.0, top, 0.012, 0.018)
    p.label("knob_dim", (-0.05, -0.243, 0.0), top)
    p.wbox(GRAPH, (0.17, -0.21, 0.005), (0.18, 0.07, 0.01), 0.002, top)
    p.screen("keys_lit", (0.17, -0.21, 0.0102), 0.16, top)
    # mission clock in a wedge housing, flight log with pencil, cue card
    clock = compose(at(-1.22, 0.99, -0.10), rotation((1, 0, 0), -38))
    p.wbox(GRAPH, (0, 0, 0), (0.24, 0.07, 0.034), 0.004, clock)
    p.screen("clock", (0, 0, 0.0175), 0.21, clock)
    p.label("clock_frame", (-1.22, 0.035, 0.0), top)
    if p.d(1):
        note = compose(top, at(-1.22, -0.19, 0), rotation((0, 0, 1), 9))
        p.wbox(PLASTIC_W, (0, 0, 0.006), (0.145, 0.205, 0.012), 0.002, note)
        p.label("notebook", (0, 0, 0.012), note, 0.14)
        p.box(RUBBER, (0, 0.062, 0.0126), (0.147, 0.008, 0.001), 0.0, note)
        p.rcyl(CABLE_Y, (0.092, -0.085, 0.0045), (0.092, 0.075, 0.0045), 0.0036, 6, 0.0, note)
        card = compose(top, at(1.22, 0.055, 0), rotation((0, 0, 1), -6))
        p.box(PLASTIC_W, (0, 0, 0.001), (0.108, 0.153, 0.002), 0.0, card)
        p.label("card_comm", (0, 0, 0.002), card, 0.105)
    # guarded ENGINE STOP on a hazard-striped plate
    stop = compose(top, at(1.22, -0.15, 0))
    p.wbox(GRAPH, (0, 0, 0.003), (0.21, 0.21, 0.006), 0.002, stop)
    p.label("haz_sq", (0, 0, 0.006), stop, 0.20)
    p.rcyl(GRAPH, (0, 0, 0.006), (0, 0, 0.013), 0.06, 20, 0.002, stop, (False, True))
    p.rcyl(RED, (0, 0, 0.013), (0, 0, 0.032), 0.034, 16, 0.006, stop, (False, True))
    if p.d(1):
        for gy in (-0.05, 0.05):
            p.rtube(ANO_RED, [(-0.075, gy, 0.006), (-0.075, gy, 0.062), (0.075, gy, 0.062), (0.075, gy, 0.006)], 0.006, 8, 0.02, stop)
        for gx in (-0.03, 0.03):
            p.rtube(ANO_RED, [(gx, -0.05, 0.062), (gx, 0.05, 0.062)], 0.005, 6, 0.0, stop)
    p.label("eng_stop", (0, 0.135, 0.0), stop)
    p.label("eng_stop_guard", (0, -0.13, 0.0), stop)
    # ---- instrument panel, leaning back 15 degrees
    panel = compose(at(0, 0.97, -0.17), rotation((1, 0, 0), -15))
    p.wbox(GRAPH, (0, 0.29, -0.035), (3.0, 0.60, 0.07), 0.01, panel)
    for cx, key, title in ((-0.98, "mfd_systems", "con_title_l"), (0.0, "mfd_flight", "con_title_c"), (0.98, "mfd_comm", "con_title_r")):
        p.frame(PLASTIC_D, (cx, 0.30, 0.0), 0.74, 0.50, 0.05, 0.028, 0.02, panel, 3 if lod == 0 else 1)
        p.box(PLASTIC_D, (cx, 0.30, 0.001), (0.66, 0.42, 0.002), 0.0, panel)
        p.screen(key, (cx, 0.30, 0.004), 0.64, panel)
        if p.d(0):
            for i in range(8):
                p.box(ALU, (cx - 0.28 + i * 0.08, 0.075, 0.030), (0.045, 0.016, 0.005), 0.0, panel)
            for i in range(5):
                for sx in (-1, 1):
                    p.box(ALU, (cx + sx * 0.345, 0.16 + i * 0.07, 0.030), (0.014, 0.034, 0.005), 0.0, panel)
            p.bolts((cx - 0.37, 0.05, cx + 0.37, 0.55), 0.028, panel, 0.014, 0.004, GRAPH)
        p.label(title, (cx, 0.027, 0.0), panel)
    for sx, key in ((-1, "ann_left"), (1, "ann_right")):
        p.wbox(GRAPH, (sx * 0.49, 0.30, 0.005), (0.17, 0.44, 0.014), 0.003, panel)
        p.screen(key, (sx * 0.49, 0.30, 0.0122), 0.15, panel)
        p.handrail((sx * 1.44, 0.10), (sx * 1.44, 0.50), 0.0, panel, 0.05, 0.012)
    p.wbox(GRAPH, (0, 0.60, 0.075), (3.10, 0.03, 0.30), 0.01, panel)
    p.pillow(FAB_SEAT, (0, 0.60, 0.235), (3.10, 0.05, 0.06), panel, 3.0, 12, 6)
    if p.d(1):
        p.box(LAMP, (0, 0.583, 0.12), (2.6, 0.004, 0.02), 0.0, panel)
    return p


def props():
    """name, builder(lod) -> Prop in ship space, origin (ship space), swap target (existing scene object) or None."""
    left = front_frame((-1.15, 0, -0.45), (0, 0, -1))
    right = front_frame((1.15, 0, -0.45), (0, 0, -1))
    centre = front_frame((0, 0, -1.95), (0, 0, 1))

    def placed(builder, frame):
        def build(lod):
            placed_prop = Prop("placed", lod)
            placed_prop.instance(builder(lod), frame)
            return placed_prop
        return build

    return [
        {"name": "IH_SeatCdr", "build": placed(lambda lod: seat(lod, "seat_cdr", 1), left), "origin": (-1.15, 0.0, -0.45),
         "carve": [{"target": "A7_Cockpit", "min": [-1.57, 0.004, -1.08], "max": [-0.73, 1.72, 0.24]}],
         "views": [("seat_rear", (-0.35, 1.6, 0.9), (-1.15, 0.8, -0.3), 60, [((-0.3, 2.6, 0.9), 260, 1.2), ((-2.2, 2.2, -1.2), 120, 1.0)]),
                   ("seat_front", (-0.25, 1.5, -1.55), (-1.15, 0.6, -0.4), 60, [((-0.3, 2.6, -1.4), 260, 1.2), ((-2.2, 2.0, 0.3), 100, 1.0)])]},
        {"name": "IH_SeatFe", "build": placed(lambda lod: seat(lod, "seat_fe", -1), right), "origin": (1.15, 0.0, -0.45),
         "carve": [{"target": "A7_Cockpit", "min": [0.73, 0.004, -1.08], "max": [1.57, 1.72, 0.24]}],
         "views": [("seat_fe_side", (0.1, 1.65, -0.9), (1.15, 0.7, -0.5), 68, [((0.3, 2.6, -0.9), 260, 1.2)])]},
        {"name": "IH_Console", "build": placed(console, centre), "origin": (0.0, 0.0, -1.95),
         "carve": [{"target": "A7_Cockpit", "min": [-1.63, 0.004, -2.46], "max": [1.63, 1.68, -1.44]}],
         "views": [("console", (0.0, 1.6, -0.35), (0.0, 1.1, -1.95), 86, [((0.0, 2.7, -0.6), 420, 2.0), ((1.6, 2.0, -0.9), 120, 1.0)]),
                   ("console_switches", (0.25, 1.5, -1.3), (0.12, 1.0, -1.92), 60, [((0.0, 2.6, -1.0), 300, 1.5)]),
                   ("console_left", (-0.6, 1.45, -1.2), (-1.1, 1.0, -1.95), 60, [((-0.6, 2.6, -1.0), 300, 1.5)])]},
    ]
