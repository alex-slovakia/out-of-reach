"""ls_kit - LAND-LAUNCH-SITE-01: a small, believable land launch complex around the existing AURORA-7 prologue
transport, in prologue-local Unity metres (y up). Original procedural geometry; no third-party assets.

Fixed by the existing scene (not modelled here, never moved): transport axis x0 z0, booster 4.7 m from y -30.6 to
-0.55, engine emitter (0, -31.4, 0), access mast 3.5 x 3.5 m at z -8.5 from the pad to the crew deck (y ~0),
staging deck at z -9.9. Pad: the barge box footprint, centre (0, -33.1, -2), size (36, 1, 24), top y -32.6.

Modules (one object each; every one optional):
  LS_PadDeck_Optional     concrete pad filling that footprint, top -32.6, with the exhaust slot (x +/-3, z -3..10)
  LS_LaunchFoundation     apron around the pad at the same top, 1:3 slopes with hipped corners down to y -36.6
  LS_FlameTrench          trench below the slot: floor -39.6 under the nozzle rising to ground at z +72, deflector
  LS_LaunchMount          two pedestals beside the slot and four hold-down posts under the booster skirt
  LS_Umbilicals           cable tray (mast -> pad-terminal wall), pipe rack (mast -> propellant sphere), the sphere
  LS_Barriers             jersey barriers along the open trench
  LS_FloodTowers          two lattice light towers (structure only; lamp positions are anchors for root's lights)
  LS_WaterTower           sound-suppression water tower on four braced legs
  LS_Markings             hazard bands, walkway and apron edge lines, pad number and mission mark (fictional)
  LS_Crawlerway           two gravel lanes up the north slope and 820 m towards the assembly building
  LS_AssemblyBuilding     modest distant silhouette with a tall door, at z -960
"""
import numpy as np

import a7kit
from a7kit import Model, front_frame

PAD_TOP, GROUND, FLOOR = -32.6, -36.6, -39.6
PAD = (-18.0, 18.0, -14.0, 10.0)                 # x0, x1, z0, z1
FOUND = (-60.0, 60.0, -50.0, 60.0)
SLOPE = 12.0
SLOT_X, SLOT_Z0 = 3.0, -3.0
BOOSTER_BOTTOM = -30.6
LINE_W = 0.15                                    # painted line width (m)
ROAD_BED = 0.15                                  # crawlerway lanes stand 15 cm proud of the ground datum


def slab(m, mat, x0, x1, z0, z1, y0, y1):
    m.box(mat, ((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), (x1 - x0, y1 - y0, z1 - z0), 0.0)


def prism(m, mat, profile, axis, a0, a1):
    """Extrude a convex profile. axis 'z': profile is (x, y), extruded over z a0..a1; axis 'x': (z, y) over x."""
    p = np.asarray(profile, dtype=float)
    k = len(p)
    if axis == "z":
        v = np.concatenate([np.column_stack([p[:, 0], p[:, 1], np.full(k, a0)]),
                            np.column_stack([p[:, 0], p[:, 1], np.full(k, a1)])])
    else:
        v = np.concatenate([np.column_stack([np.full(k, a0), p[:, 1], p[:, 0]]),
                            np.column_stack([np.full(k, a1), p[:, 1], p[:, 0]])])
    t = []
    for i in range(k):
        j = (i + 1) % k
        t += [(i, j, k + j), (i, k + j, k + i)]
    for i in range(1, k - 1):
        t += [(0, i + 1, i), (k, k + i, k + i + 1)]
    t = np.asarray(t)
    t = a7kit._outward_convex(v, t)
    m.add(mat, v, t)


def pad_deck():
    m = Model("LS_PadDeck_Optional")
    x0, x1, z0, z1 = PAD
    slab(m, "LS_Concrete", x0, -SLOT_X, z0, z1, GROUND, PAD_TOP)
    slab(m, "LS_Concrete", SLOT_X, x1, z0, z1, GROUND, PAD_TOP)
    slab(m, "LS_Concrete", -SLOT_X, SLOT_X, z0, SLOT_Z0, GROUND, PAD_TOP)
    return m


def foundation():
    m = Model("LS_LaunchFoundation")
    fx0, fx1, fz0, fz1 = FOUND
    px0, px1, pz0, pz1 = PAD
    slab(m, "LS_Concrete", fx0, px0, fz0, fz1, GROUND, PAD_TOP)
    slab(m, "LS_Concrete", px1, fx1, fz0, fz1, GROUND, PAD_TOP)
    slab(m, "LS_Concrete", px0, px1, fz0, pz0, GROUND, PAD_TOP)
    slab(m, "LS_Concrete", px0, -SLOT_X, pz1, fz1, GROUND, PAD_TOP)
    slab(m, "LS_Concrete", SLOT_X, px1, pz1, fz1, GROUND, PAD_TOP)
    drop = PAD_TOP - GROUND
    prism(m, "LS_Concrete", [(fx0, PAD_TOP), (fx0 - SLOPE, GROUND), (fx0, GROUND)], "z", fz0, fz1)
    prism(m, "LS_Concrete", [(fx1, PAD_TOP), (fx1, GROUND), (fx1 + SLOPE, GROUND)], "z", fz0, fz1)
    prism(m, "LS_Concrete", [(fz0, PAD_TOP), (fz0 - SLOPE, GROUND), (fz0, GROUND)], "x", fx0, fx1)
    prism(m, "LS_Concrete", [(fz1, PAD_TOP), (fz1, GROUND), (fz1 + SLOPE, GROUND)], "x", fx0, -SLOT_X)
    prism(m, "LS_Concrete", [(fz1, PAD_TOP), (fz1, GROUND), (fz1 + SLOPE, GROUND)], "x", SLOT_X, fx1)
    for cx, sx in ((fx0, -1.0), (fx1, 1.0)):
        for cz, sz in ((fz0, -1.0), (fz1, 1.0)):
            hip_corner(m, "LS_Concrete", cx, cz, sx, sz)
    assert drop > 0
    return m


def hip_corner(m, mat, cx, cz, sx, sz):
    """Hipped slope corner: a pyramid over the corner square with its apex on the foundation's top corner, so the
    two side slopes meet along the diagonal hip line and the footprint stays a closed rectangle at ground level."""
    v = np.array([(cx, PAD_TOP, cz), (cx, GROUND, cz), (cx + sx * SLOPE, GROUND, cz),
                  (cx + sx * SLOPE, GROUND, cz + sz * SLOPE), (cx, GROUND, cz + sz * SLOPE)])
    t = np.array([(0, 2, 3), (0, 3, 4), (0, 1, 2), (0, 4, 1), (1, 3, 2), (1, 4, 3)])
    m.add(mat, v, a7kit._outward_convex(v, t))


def flame_trench():
    m = Model("LS_FlameTrench")
    z_end = FOUND[3] + SLOPE
    bottom = FLOOR - 0.6
    # side walls below the foundation and the back wall behind the nozzle
    slab(m, "LS_ConcreteSoot", -SLOT_X - 1.0, -SLOT_X, SLOT_Z0 - 1.0, z_end, bottom, GROUND)
    slab(m, "LS_ConcreteSoot", SLOT_X, SLOT_X + 1.0, SLOT_Z0 - 1.0, z_end, bottom, GROUND)
    slab(m, "LS_ConcreteSoot", -SLOT_X, SLOT_X, SLOT_Z0 - 1.0, SLOT_Z0, bottom, GROUND)
    # floor: level under the vehicle, then a ramp up to the ground at the exit
    slab(m, "LS_ConcreteSoot", -SLOT_X, SLOT_X, SLOT_Z0, 6.0, bottom, FLOOR)
    prism(m, "LS_ConcreteSoot", [(6.0, FLOOR), (6.0, bottom), (z_end, bottom), (z_end, GROUND)], "x", -SLOT_X, SLOT_X)
    # flame deflector: a wedge under the nozzle turning the plume towards +z, faced with a steel plate
    prism(m, "LS_ConcreteSoot", [(SLOT_Z0, FLOOR), (SLOT_Z0, -35.0), (5.5, FLOOR)], "x", -SLOT_X + 0.05, SLOT_X - 0.05)
    d = np.array([8.5, -4.6])
    n = np.array([-d[1], d[0]]) / np.linalg.norm(d)
    top = np.array([SLOT_Z0, -35.0]) + n * 0.002
    tip = np.array([5.5, FLOOR]) + n * 0.002
    prism(m, "A7_PaintGraphite", [tuple(top), tuple(tip), tuple(tip + n * 0.06), tuple(top + n * 0.06)], "x",
          -SLOT_X + 0.1, SLOT_X - 0.1)
    return m


def launch_mount():
    m = Model("LS_LaunchMount")
    top = BOOSTER_BOTTOM - 0.35
    for s in (-1.0, 1.0):
        x0, x1 = sorted((s * (SLOT_X + 0.1), s * (SLOT_X + 1.7)))
        slab(m, "LS_Concrete", x0, x1, -4.6, 4.6, PAD_TOP, top - 0.1)
        slab(m, "A7_PaintGraphite", x0, x1, -4.6, 4.6, top - 0.1, top)
        for z in (-1.4, 1.4):
            xa, xb = sorted((s * 1.75, s * (SLOT_X + 0.3)))
            slab(m, "A7_PaintGray", xa, xb, z - 0.2, z + 0.2, top, BOOSTER_BOTTOM - 0.02)
    slab(m, "LS_Concrete", -SLOT_X - 1.7, SLOT_X + 1.7, -4.6, SLOT_Z0 - 0.4, PAD_TOP, top - 0.6)
    return m


def tray_run(m, points, height):
    y = PAD_TOP + height
    for a, b in zip(points, points[1:]):
        a, b = np.array(a, float), np.array(b, float)
        d = b - a
        length = float(np.linalg.norm(d))
        along_x = abs(d[0]) > abs(d[1])
        cx, cz = (a + b) / 2
        if along_x:
            slab(m, "A7_Aluminum", cx - length / 2, cx + length / 2, cz - 0.3, cz + 0.3, y, y + 0.03)
            for s in (-1, 1):
                slab(m, "A7_Aluminum", cx - length / 2, cx + length / 2, cz + s * 0.3 - 0.015, cz + s * 0.3 + 0.015, y, y + 0.15)
        else:
            slab(m, "A7_Aluminum", cx - 0.3, cx + 0.3, cz - length / 2, cz + length / 2, y, y + 0.03)
            for s in (-1, 1):
                slab(m, "A7_Aluminum", cx + s * 0.3 - 0.015, cx + s * 0.3 + 0.015, cz - length / 2, cz + length / 2, y, y + 0.15)
        steps = max(1, int(length // 3.0))
        for i in range(steps + 1):
            px, pz = a + d * (i / steps)
            m.box("A7_PaintGray", (px, PAD_TOP + height / 2, pz), (0.12, height, 0.12), 0.0)


def pipe_rack(m, points, height, pipes=3):
    for a, b in zip(points, points[1:]):
        a, b = np.array(a, float), np.array(b, float)
        d = b - a
        length = float(np.linalg.norm(d))
        side = np.array([-d[1], d[0]]) / length
        steps = max(1, int(length // 5.0))
        for i in range(steps + 1):
            p = a + d * (i / steps)
            for s in (-0.75, 0.75):
                q = p + side * s
                m.box("A7_PaintGray", (q[0], PAD_TOP + height / 2, q[1]), (0.18, height, 0.18), 0.0)
            c = p
            m.cyl("A7_PaintGray", (c[0] - side[0] * 0.85, PAD_TOP + height, c[1] - side[1] * 0.85),
                  (c[0] + side[0] * 0.85, PAD_TOP + height, c[1] + side[1] * 0.85), 0.09, 6)
    for k in range(pipes):
        off = (k - (pipes - 1) / 2) * 0.45
        path = []
        for a, b in zip(points, points[1:]):
            a, b = np.array(a, float), np.array(b, float)
            d = (b - a) / np.linalg.norm(b - a)
            side = np.array([-d[1], d[0]])
            path.append((a + side * off, b + side * off))
        pts = [path[0][0]] + [seg[1] for seg in path]
        m.tube("A7_PaintWhite", [(p[0], PAD_TOP + height + 0.24, p[1]) for p in pts], 0.14, 10, fillet=0.6)


def umbilicals():
    m = Model("LS_Umbilicals")
    tray_run(m, [(-1.4, -10.6), (-1.4, -33.0), (-23.0, -33.0)], 1.2)
    slab(m, "LS_Concrete", -29.0, -23.0, -35.0, -31.0, PAD_TOP, PAD_TOP + 3.2)
    slab(m, "A7_PaintGray", -26.7, -25.3, -31.02, -30.98, PAD_TOP, PAD_TOP + 2.2)
    pipe_rack(m, [(1.4, -10.6), (1.4, -24.0), (34.0, -24.0), (34.0, -38.0)], 2.0)
    centre = np.array([42.0, PAD_TOP + 9.0, -42.0])
    m.sphere("A7_PaintWhite", tuple(centre), 6.5, 24, 14)
    for i in range(8):
        a = 2 * np.pi * i / 8
        foot = centre + np.array([np.cos(a) * 6.8, 0.0, np.sin(a) * 6.8])
        m.cyl("A7_PaintGray", (foot[0], PAD_TOP, foot[2]), (foot[0], centre[1] + 0.5, foot[2]), 0.22, 8)
    m.tube("A7_PaintWhite", [(34.0, PAD_TOP + 2.24, -38.0), (34.0, PAD_TOP + 2.24, -42.0), (35.6, PAD_TOP + 4.0, -42.0)], 0.14, 10, fillet=0.6)
    return m


def jersey(m, x, z0, z1):
    profile = [(x - 0.30, PAD_TOP), (x + 0.30, PAD_TOP), (x + 0.30, PAD_TOP + 0.08), (x + 0.12, PAD_TOP + 0.33),
               (x + 0.075, PAD_TOP + 0.81), (x - 0.075, PAD_TOP + 0.81), (x - 0.12, PAD_TOP + 0.33), (x - 0.30, PAD_TOP + 0.08)]
    prism(m, "LS_ConcreteBarrier", profile, "z", z0, z1)


def barriers():
    m = Model("LS_Barriers")
    z = PAD[3] + 0.6
    while z + 3.0 <= FOUND[3] - 0.5:
        for x in (-(SLOT_X + 0.9), SLOT_X + 0.9):
            jersey(m, x, z, z + 3.0)
        z += 3.2
    return m


def tower(m, base, height, lamp_face):
    bx, bz = base
    for sx in (-1, 1):
        for sz in (-1, 1):
            m.cyl("A7_PaintGray", (bx + sx * 1.6, PAD_TOP, bz + sz * 1.6), (bx + sx * 0.7, PAD_TOP + height, bz + sz * 0.7), 0.12, 6)
    for level in np.arange(4.0, height, 4.0):
        r = 1.6 - 0.9 * level / height
        for sx, sz, ex, ez in ((-1, -1, 1, -1), (1, -1, 1, 1), (1, 1, -1, 1), (-1, 1, -1, -1)):
            m.cyl("A7_PaintGray", (bx + sx * r, PAD_TOP + level, bz + sz * r), (bx + ex * r, PAD_TOP + level, bz + ez * r), 0.05, 4)
    head = np.array([bx, PAD_TOP + height + 0.6, bz])
    m.box("A7_PaintGraphite", tuple(head), (3.2, 1.2, 0.5), 0.02)
    for i in range(4):
        m.box("A7_Glass", (bx - 1.2 + i * 0.8, head[1], bz + lamp_face * 0.27), (0.6, 0.9, 0.04), 0.0)
    return (float(head[0]), float(head[1]), float(bz + lamp_face * 0.3))


def water_tower():
    """Sound-suppression water tower: a tank on four braced legs, with its feed line down to the apron."""
    m = Model("LS_WaterTower")
    bx, bz, leg_top = -46.0, -38.0, 46.0
    for sx in (-1, 1):
        for sz in (-1, 1):
            m.cyl("A7_PaintGray", (bx + sx * 4.2, PAD_TOP, bz + sz * 4.2), (bx + sx * 3.0, PAD_TOP + leg_top, bz + sz * 3.0), 0.28, 8)
    for level in np.arange(8.0, leg_top, 9.0):
        r = 4.2 - 1.2 * level / leg_top
        for sx, sz, ex, ez in ((-1, -1, 1, -1), (1, -1, 1, 1), (1, 1, -1, 1), (-1, 1, -1, -1)):
            m.cyl("A7_PaintGray", (bx + sx * r, PAD_TOP + level, bz + sz * r), (bx + ex * r, PAD_TOP + level, bz + ez * r), 0.12, 6)
    m.cyl("A7_PaintWhite", (bx, PAD_TOP + leg_top, bz), (bx, PAD_TOP + leg_top + 11.0, bz), 5.2, 28)
    m.sphere("A7_PaintWhite", (bx, PAD_TOP + leg_top + 11.0, bz), 5.2, 28, 8, squash=(1.0, 0.35, 1.0))
    m.cyl("A7_PaintGray", (bx, PAD_TOP, bz), (bx, PAD_TOP + leg_top, bz), 0.45, 10)
    return m


def flood_towers():
    m = Model("LS_FloodTowers")
    lamps = [tower(m, (-46.0, 46.0), 28.0, -1), tower(m, (46.0, 46.0), 28.0, -1)]
    return m, lamps


def markings(atlas):
    m = Model("LS_Markings")
    lift = PAD_TOP + 0.012
    for x in (-(SLOT_X + 0.25), SLOT_X + 0.25):
        z = SLOT_Z0
        while z + 4.0 <= FOUND[3]:
            fr = front_frame((x, lift, z + 2.0), (0, 1, 0), (1, 0, 0))
            m.decal(atlas, "hazard_band", (0, 0, 0), fr, material="LS_Markings", lift=0.0)
            z += 4.0
    m.decal(atlas, "pad_number", (0, 0, 0), front_frame((-32.0, lift, 24.0), (0, 1, 0), (0, 0, 1)), material="LS_Markings", lift=0.0)
    m.decal(atlas, "mission_mark", (0, 0, 0), front_frame((32.0, lift, 24.0), (0, 1, 0), (0, 0, 1)), material="LS_Markings", lift=0.0)
    m.decal(atlas, "keep_clear", (0, 0, 0), front_frame((0.0, lift, 14.0), (0, 1, 0), (0, 0, 1)), material="LS_Markings", lift=0.0)
    # yellow walkway, 1.2 m wide: from the mast's west face along z -8.5, then to the pad-terminal door at z -31.
    # Runs along x fill the corner squares; runs along z start at their edge, so no two strips overlap.
    h = LINE_W / 2
    for a, b in (((-2.2, -7.9), (-26.6 - h, -7.9)), ((-2.2, -9.1), (-25.4 - h, -9.1)),
                 ((-26.6, -7.9 - h), (-26.6, -31.0)), ((-25.4, -9.1 - h), (-25.4, -31.0))):
        line_run(m, atlas, "line_yellow", a, b, lift)
    # white edge line 0.6 m inside the apron edge; the south side stops short of the trench barriers
    e = 0.6
    x0, x1, z0, z1 = FOUND[0] + e, FOUND[1] - e, FOUND[2] + e, FOUND[3] - e
    for a, b in (((x0 - h, z0), (x1 + h, z0)), ((x0 - h, z1), (-4.5, z1)), ((4.5, z1), (x1 + h, z1)),
                 ((x0, z0 + h), (x0, z1 - h)), ((x1, z0 + h), (x1, z1 - h))):
        line_run(m, atlas, "line_white", a, b, lift)
    return m


def line_run(m, atlas, key, a, b, lift):
    """Axis-aligned painted line from a to b (x, z) as one strip LINE_W wide. The atlas tile is a solid colour, so
    the strip samples a 2 px inset of it, stretched to any length."""
    entry = atlas["entries"][key]
    px = 2.0 / 1024.0
    u0, u1, v0, v1 = entry["u0"] + px, entry["u1"] - px, entry["v0"] + px, entry["v1"] - px
    a, b = np.array(a, float), np.array(b, float)
    d = b - a
    c = (a + b) / 2
    up = (1, 0, 0) if abs(d[1]) > abs(d[0]) else (0, 0, 1)
    v, t = a7kit.quad(float(np.linalg.norm(d)), LINE_W)
    uv = np.array([[u0, v0], [u1, v0], [u1, v1], [u0, v1]])
    m.add("LS_Markings", v, t, front_frame((c[0], lift, c[1]), (0, 1, 0), up), uv[t])


def crawlerway():
    m = Model("LS_Crawlerway")
    fz0 = FOUND[2]
    for x0, x1 in ((-18.0, -6.0), (6.0, 18.0)):
        prism(m, "LS_Gravel", [(fz0, PAD_TOP + 0.03), (fz0 - SLOPE, GROUND + ROAD_BED), (fz0 - SLOPE, GROUND - 0.2), (fz0, PAD_TOP - 0.2)], "x", x0, x1)
        slab(m, "LS_Gravel", x0, x1, -880.0, fz0 - SLOPE, GROUND - 0.2, GROUND + ROAD_BED)
    return m


def assembly_building(atlas):
    m = Model("LS_AssemblyBuilding")
    zc, w, h, d = -960.0, 70.0, 90.0, 70.0
    m.box("A7_PaintWhite", (0.0, GROUND + h / 2, zc), (w, h, d), 0.0)
    m.box("A7_PaintGraphite", (0.0, GROUND + 32.0, zc + d / 2 + 0.05), (18.0, 64.0, 0.2), 0.0)
    m.box("A7_PaintWhite", (55.0, GROUND + 12.5, zc + 10.0), (40.0, 25.0, 40.0), 0.0)
    m.box("A7_PaintGray", (0.0, GROUND + h + 1.0, zc), (w - 4.0, 2.0, d - 4.0), 0.0)
    fr = front_frame((0.0, GROUND + 78.0, zc + d / 2 + 0.2), (0, 0, 1), (0, 1, 0))
    m.decal(atlas, "mission_mark", (0, 0, 0), fr, width=40.0, material="LS_Markings", lift=0.0)
    return m


def build_kit(atlas):
    lamps = None
    towers, lamps = flood_towers()
    models = [pad_deck(), foundation(), flame_trench(), launch_mount(), umbilicals(), barriers(), towers,
              water_tower(), markings(atlas), crawlerway(), assembly_building(atlas)]
    anchors = {
        "LS_PadTop_Centre": (0.0, PAD_TOP, -2.0),
        "LS_NozzleAxis_PadTop": (0.0, PAD_TOP, 0.0),
        "LS_TrenchFloor_UnderNozzle": (0.0, FLOOR, 0.0),
        "LS_DeflectorPeak": (0.0, -35.0, SLOT_Z0),
        "LS_TrenchExit_Ground": (0.0, GROUND, FOUND[3] + SLOPE),
        "LS_GroundLevel_SlopeFoot_West": (FOUND[0] - SLOPE, GROUND, 0.0),
        "LS_CableTray_MastEnd": (-1.4, PAD_TOP + 1.2, -10.6),
        "LS_PadTerminal_Door": (-26.0, PAD_TOP, -30.98),
        "LS_PipeRack_MastEnd": (1.4, PAD_TOP + 2.24, -10.6),
        "LS_PropellantSphere_Centre": (42.0, PAD_TOP + 9.0, -42.0),
        "LS_FloodLamp_West": lamps[0],
        "LS_FloodLamp_East": lamps[1],
        "LS_WaterTower_Base": (-46.0, PAD_TOP, -38.0),
        "LS_Crawlerway_PadEnd": (0.0, PAD_TOP, FOUND[2]),
        "LS_Crawlerway_FarEnd": (0.0, GROUND, -880.0),
        "LS_AssemblyBuilding_Door": (0.0, GROUND, -925.0),
    }
    return models, anchors
