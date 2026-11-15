"""CARGO-CASES-V02: seven original flight-cargo containers, fully scripted (Blender 3.6).

    blender --background --python build_cargo_cases_v02.py            (FBX + report)
    blender --background --python build_cargo_cases_v02.py -- --blend (also saves CargoCasesV02.blend)

Frame (same as the delivered PreflightCargo case): metres, origin at the floor centre, +Z up, labelled front on -Y
(Unity +Z after export). Both side grips are 34 mm tubes along Y centred at x = +/-0.278, z = 0.175: exactly the
"Additive cargo side grips" the carry rig (PreflightCargoHandsAuthoring / CARRY-HANDS) already holds.
Each case exports as CC2_<Key>.fbx with <Key>_LOD0/1/2 meshes (Unity builds the LODGroup from the suffixes).
"""
import importlib
import json
import math
import os
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import cc2_lib as L  # noqa: E402

importlib.reload(L)
from cc2_lib import (X, Y, Z, Part, bolt, box, cyl, cyl_decal, cyl_patch, decal, fillet, lathe, obox, rbox, ribbon,  # noqa: E402
                     tube)

ROOT = os.path.normpath(os.path.join(HERE, ".."))
MODELS = os.path.join(ROOT, "Models")
ATLAS = json.load(open(os.path.join(HERE, "cc2_atlas.json")))

HX, HZ, HR = .278, .175, .017  # grip tube centre and radius (PreflightCargoHandsAuthoring constants)
ALU, RUB, GRA, WEB, WEBA, RED, LED = ("CC2_Aluminium", "CC2_Rubber", "CC2_Graphite", "CC2_Webbing", "CC2_WebbingAmber",
                                      "CC2_RedCap", "CC2_LED")
FRONT, BACK, UP = -Y, Y, Z

# preview colours only; Unity materials are built by CargoCasesV02Builder from the same names
PREVIEW = {
    "CC2_Shell_Water": (.08, .48, .62), "CC2_Shell_Food": (.37, .49, .16), "CC2_Shell_Battery": (.79, .40, .055),
    "CC2_Shell_Oxygen": (.86, .9, .9), "CC2_Shell_Solar": (.18, .29, .52), "CC2_Shell_Spares": (.57, .19, .12),
    "CC2_Shell_Science": (.39, .28, .49), ALU: (.72, .74, .76), RUB: (.03, .03, .035), GRA: (.1, .11, .12),
    WEB: (.25, .26, .27), WEBA: (.8, .5, .1), RED: (.7, .08, .05), LED: (.2, 1, .4), "CC2_Composite": (.84, .84, .8),
    "CC2_SolarCell": (.05, .08, .25), "CC2_Labels": (.95, .95, .92), "CC2_RackSteel": (.16, .18, .2),
}


def material(name):
    m = bpy.data.materials.get(name)
    if not m:
        m = bpy.data.materials.new(name)
        m.diffuse_color = PREVIEW.get(name, (.5, .5, .5)) + (1,)
        m.use_nodes = True
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        if bsdf:
            bsdf.inputs["Base Color"].default_value = PREVIEW.get(name, (.5, .5, .5)) + (1,)
            bsdf.inputs["Metallic"].default_value = .85 if name == ALU else 0
            bsdf.inputs["Roughness"].default_value = .4 if name == ALU else .6
    return m


# ------------------------------------------------------------------ shared hardware
def side_handles(p, wall_x, plate=ALU, plate_h=.058, plate_z=None, plate_w=.168):
    """The two carry grips. Tube centre (+/-HX, y, HZ), straight grip along Y, bent back to a bolted wall plate."""
    pz = HZ if plate_z is None else plate_z
    for s in (-1, 1):
        xo, xw = s * HX, s * wall_x
        pts = fillet([(xw, -.066, HZ), (xo, -.066, HZ), (xo, .066, HZ), (xw, .066, HZ)], .016, 5 if p.lod == 0 else 3)
        tube(p, pts, .0115, ALU, n=12)
        # knurled rubber sleeve, 34 mm: this is what the gloves close around
        lathe(p, (xo, -.047, HZ), Y, [(0, .0118), (.004, HR), (.090, HR), (.094, .0118)], RUB, n=16, tile=.06)
        obox(p, (xw + s * .0035, 0, pz), (s, 0, 0), UP, (plate_w, plate_h, .007), plate, bevel=.002)
        for y in (-.066, .066):
            lathe(p, (xw + s * .006, y, HZ), (s, 0, 0), [(0, .019), (.006, .019), (.010, .014)], ALU, n=14)
            for dz in (-.02, .02):
                bolt(p, (xw + s * .007, y, pz + dz), (s, 0, 0))


def feet(p, x=.16, y=.10, size=(.05, .04, .01), mat=RUB):
    for sx in (-1, 1):
        for sy in (-1, 1):
            box(p, (sx * x, sy * y, size[2] / 2), size, mat, bevel=.003, seg=1, tile=.08)


def draw_latch(p, x, y, z_seam, body=GRA, w=.06):
    """Double-throw latch across a lid seam on the front (-Y) wall whose surface is at y."""
    obox(p, (x, y - .006, z_seam + .012), FRONT, UP, (w, .018, .012), body, bevel=.003)          # keeper on the lid
    obox(p, (x, y - .009, z_seam - .022), FRONT, UP, (w, .05, .016), body, bevel=.004)           # lever
    obox(p, (x, y - .016, z_seam - .034), FRONT, UP, (w * .62, .018, .006), body, bevel=.002)    # finger lip
    if p.lod < 2:
        cyl(p, (x - w / 2 - .003, y - .008, z_seam + .003), (x + w / 2 + .003, y - .008, z_seam + .003), .0035, ALU, n=8)
        cyl(p, (x - w / 2 - .003, y - .006, z_seam - .044), (x + w / 2 + .003, y - .006, z_seam - .044), .0035, ALU, n=8)


def toggle_latch(p, x, y, z_seam):
    """Sprung aluminium toggle latch (transit-case style)."""
    obox(p, (x, y - .003, z_seam - .028), FRONT, UP, (.04, .05, .006), ALU, bevel=.002)
    obox(p, (x, y - .009, z_seam - .02), FRONT, UP, (.028, .058, .006), ALU, bevel=.002)
    obox(p, (x, y - .004, z_seam + .016), FRONT, UP, (.04, .016, .008), ALU, bevel=.002)
    if p.lod < 2:
        loop = fillet([(x - .011, y - .011, z_seam - .005), (x - .011, y - .011, z_seam + .02), (x + .011, y - .011, z_seam + .02),
                       (x + .011, y - .011, z_seam - .005)], .004, 3)
        tube(p, loop, .0022, ALU, n=6)
        for dx in (-.013, .013):
            bolt(p, (x + dx, y - .006, z_seam - .045), FRONT, r=.003)


def hinge(p, x, y, z, length=.07, mat=GRA, r=.008):
    cyl(p, (x - length / 2, y + r * .6, z), (x + length / 2, y + r * .6, z), r, mat, n=10)
    obox(p, (x, y + .002, z - .016), BACK, UP, (length, .024, .005), mat, bevel=.0015)
    obox(p, (x, y + .002, z + .016), BACK, UP, (length, .024, .005), mat, bevel=.0015)


# ------------------------------------------------------------------ 1. spares: moulded hard case
def spares(p):
    sh = "CC2_Shell_Spares"
    seam = .205
    rbox(p, (0, 0, .1025), (.44, .31, .195), sh, rv=.032, rh=.012, tile=.2)
    rbox(p, (0, 0, .196), (.452, .322, .013), sh, rv=.037, rh=.003, segh=1, tile=.2)             # base flange
    rbox(p, (0, 0, .2035), (.43, .30, .007), RUB, rv=.03, rh=0, tile=.08)                         # gasket
    rbox(p, (0, 0, .2115), (.452, .322, .013), sh, rv=.037, rh=.003, segh=1, tile=.2)            # lid flange
    rbox(p, (0, 0, .2335), (.44, .31, .047), sh, rv=.032, rh=.014, tile=.2)                       # lid
    for y in (-.1, .1):                                                                           # stacking bars
        rbox(p, (0, y, .2575), (.34, .03, .009), sh, rv=.01, rh=.003, segv=2, segh=1, tile=.2)
    for x in (-.195, .195):                                                                       # corner ribs
        for y, n in ((-.156, FRONT), (.156, BACK)):
            obox(p, (x, y, .1025), n, UP, (.022, .165, .012), sh, bevel=.004, tile=.2)
            obox(p, (x, y, .235), n, UP, (.022, .036, .012), sh, bevel=.004, tile=.2)
    if p.lod < 2:
        for x in (-.065, .065):
            obox(p, (x, .156, .1025), BACK, UP, (.022, .165, .012), sh, bevel=.004, tile=.2)
        for sx in (-1, 1):
            for y in (-.118, .118):
                obox(p, (sx * .221, y, .1025), (sx, 0, 0), UP, (.022, .165, .012), sh, bevel=.004, tile=.2)
    for x in (-.125, .125):
        draw_latch(p, x, -.161, seam)
        hinge(p, x, .161, seam + .001)
    # pressure equalisation valve and padlock hasps
    lathe(p, (0, -.155, .232), FRONT, [(0, .014), (.005, .014), (.005, .010), (.010, .010), (.010, .004), (.007, 0)], GRA, n=14)
    if p.lod == 0:
        for x in (-.2, .2):
            obox(p, (x, -.163, seam), FRONT, UP, (.016, .034, .006), ALU, bevel=.002)
    side_handles(p, .221, plate=GRA, plate_z=.166)
    feet(p)
    decal(p, "name:spare-parts-kit", (0, -.155, .095), FRONT, UP, w=.25)
    decal(p, "name:spare-parts-kit", (0, 0, .257), UP, Y, w=.3)
    decal(p, "icon:plate", (-.13, .155, .11), BACK, UP, w=.11)
    decal(p, "icon:up", (.14, .155, .1), BACK, UP, w=.07)


# ------------------------------------------------------------------ 2. science: beaded aluminium transit case
def science(p):
    sh = "CC2_Shell_Science"
    seam, top = .2, .288
    box(p, (0, 0, .1), (.432, .302, .19), sh, bevel=.003, seg=1, tile=.3)
    box(p, (0, 0, .2435), (.432, .302, .083), sh, bevel=.003, seg=1, tile=.3)
    box(p, (0, 0, .2), (.424, .294, .012), RUB, tile=.08)                                         # gasket line
    # extruded edge profiles: corner posts, top/bottom frames, seam frames
    for sx in (-1, 1):
        for sy in (-1, 1):
            box(p, (sx * .2145, sy * .1495, .1465), (.019, .019, .283), ALU, bevel=.004, seg=2, tile=.3)
            for z in (.016, top - .013):                                                          # cast stacking corners
                box(p, (sx * .2125, sy * .1475, z), (.042, .042, .032), ALU, bevel=.008, seg=2, tile=.3)
    for z, h in ((.011, .016), (top - .008, .016), (seam - .008, .012), (seam + .008, .012)):
        for sy in (-1, 1):
            box(p, (0, sy * .1505, z), (.41, .014, h), ALU, bevel=.003, seg=1, tile=.3)
        for sx in (-1, 1):
            box(p, (sx * .2155, 0, z), (.014, .28, h), ALU, bevel=.003, seg=1, tile=.3)
    # pressed beading on the panels
    if p.lod < 2:
        for z in (.04, .066):
            for sy in (-1, 1):
                cyl(p, (-.185, sy * .1505, z), (.185, sy * .1505, z), .0065, sh, n=8, tile=.3)
            for sx in (-1, 1):
                cyl(p, (sx * .2155, -.12, z), (sx * .2155, .12, z), .0065, sh, n=8, tile=.3)
        for y in (-.118, .118):
            cyl(p, (-.17, y, top - .0025), (.17, y, top - .0025), .0065, sh, n=8, tile=.3)
    for x in (-.135, .135):
        toggle_latch(p, x, -.153, seam)
        hinge(p, x, .152, seam, length=.06, mat=ALU, r=.006)
    # desiccant / shock indicator
    lathe(p, (.108, -.151, .105), FRONT, [(0, .021), (.006, .021), (.006, .017), (.004, .017), (.004, 0)], ALU, n=18)
    decal(p, "icon:gauge", (.108, -.1555, .105), FRONT, UP, w=.032, off=.0002)
    side_handles(p, .217, plate_z=.166)
    feet(p, x=.15, y=.095)
    decal(p, "name:spectrometer", (-.045, -.151, .133), FRONT, UP, w=.24)
    decal(p, "name:spectrometer", (0, 0, top + .0015), UP, Y, w=.3, off=.0004)
    decal(p, "icon:fragile", (.062, -.151, .247), FRONT, UP, w=.085)
    decal(p, "icon:up", (-.062, -.151, .247), FRONT, UP, w=.05)
    decal(p, "icon:plate", (0, .151, .12), BACK, UP, w=.11)


# ------------------------------------------------------------------ 3. food: rotomoulded insulated locker, front door
def food(p):
    sh = "CC2_Shell_Food"
    rbox(p, (0, 0, .1475), (.44, .31, .285), sh, rv=.036, rh=.02, segv=4, segh=3, tile=.2)
    for z in (.045, .262):                                                                        # moulded bands
        rbox(p, (0, 0, z), (.45, .32, .016), sh, rv=.04, rh=.005, segv=4, segh=1, tile=.2)
    rbox(p, (-.008, -.158, .15), (.33, .016, .2), sh, rv=.024, rh=.005, segv=3, segh=1, axis=1, tile=.2)   # door
    rbox(p, (-.008, -.165, .15), (.27, .006, .145), sh, rv=.014, rh=.002, segv=2, segh=1, axis=1, tile=.2)  # door pad
    rbox(p, (-.008, -.1525, .15), (.342, .008, .212), RUB, rv=.028, rh=0, axis=1, tile=.08)       # door gasket
    for z in (.095, .205):
        cyl(p, (-.181, -.163, z - .028), (-.181, -.163, z + .028), .0075, GRA, n=10)              # hinge barrels
        obox(p, (-.171, -.166, z), FRONT, UP, (.024, .05, .004), GRA, bevel=.0015)
        # paddle latch on the right jamb
        obox(p, (.176, -.163, z), FRONT, UP, (.034, .066, .014), GRA, bevel=.004)
        obox(p, (.166, -.172, z), FRONT, UP, (.03, .034, .006), GRA, bevel=.002)
        if p.lod < 2:
            obox(p, (.198, -.159, z), FRONT, UP, (.014, .05, .008), ALU, bevel=.002)
    lathe(p, (.115, -.168, .235), FRONT, [(0, .013), (.004, .013), (.006, .009), (.006, 0)], GRA, n=12)   # vent plug
    for sx in (-1, 1):                                                                            # stacking lugs
        for sy in (-1, 1):
            rbox(p, (sx * .15, sy * .1, .291), (.075, .05, .01), sh, rv=.014, rh=.003, segv=2, segh=1, tile=.2)
    side_handles(p, .221, plate=GRA, plate_z=.17)
    feet(p, x=.15, y=.1)
    decal(p, "name:food-locker", (-.008, -.168, .158), FRONT, UP, w=.23, off=.0006)
    decal(p, "name:food-locker", (0, 0, .29), UP, Y, w=.2, off=.0006)
    decal(p, "icon:plate", (0, .155, .15), BACK, UP, w=.11)
    decal(p, "icon:up", (.14, .155, .15), BACK, UP, w=.06)


# ------------------------------------------------------------------ 4. battery: finned power module
def battery(p):
    sh = "CC2_Shell_Battery"
    box(p, (0, .004, .125), (.43, .286, .22), sh, bevel=.006, seg=2, tile=.3)
    box(p, (0, -.147, .125), (.44, .018, .232), GRA, bevel=.004, seg=1, tile=.2)                  # front panel
    box(p, (0, .152, .125), (.44, .012, .232), GRA, bevel=.004, seg=1, tile=.2)                   # rear plate
    # heat sink over the rear half of the lid
    box(p, (0, .082, .238), (.405, .126, .008), ALU, tile=.3)
    nf = 14 if p.lod == 0 else 7 if p.lod == 1 else 0
    for i in range(nf):
        x = -.195 + i * (.39 / (nf - 1))
        box(p, (x, .082, .252), (.006 if p.lod == 0 else .012, .12, .022), ALU, tile=.3, smooth=False)
    if p.lod == 2:
        box(p, (0, .082, .25), (.4, .12, .024), ALU, tile=.3)
    # front: painted name plate, connectors, isolator, charge bar, lifting bails
    obox(p, (0, -.1565, .066), FRONT, UP, (.3, .088, .004), sh, bevel=.0015, tile=.3)
    for x, cap in ((-.105, RED), (-.045, RUB)):
        lathe(p, (x, -.156, .168), FRONT, [(0, .023), (.006, .023), (.006, .018), (.020, .018), (.020, .0145), (.0, .0145)], ALU, n=18)
        lathe(p, (x, -.158, .168), FRONT, [(.0, .014), (.024, .014), (.027, .011), (.027, 0)], cap, n=14, tile=.08)
    lathe(p, (.085, -.156, .165), FRONT, [(0, .028), (.005, .028), (.007, .024), (.007, 0)], ALU, n=18)
    obox(p, (.085, -.171, .165), FRONT, (.35, 0, .94), (.014, .058, .016), RED, bevel=.004)
    if p.lod < 2:
        for i in range(5):
            obox(p, (-.106 + i * .0155, -.157, .118), FRONT, UP, (.012, .009, .003), LED if i < 4 else GRA)
        obox(p, (-.075, -.1565, .118), FRONT, UP, (.086, .017, .002), RUB)
    for x in (-.192, .192):
        pts = fillet([(x, -.154, .062), (x, -.182, .062), (x, -.182, .19), (x, -.154, .19)], .012, 4 if p.lod == 0 else 2)
        tube(p, pts, .0075, ALU, n=10)
        for z in (.062, .19):
            lathe(p, (x, -.156, z), FRONT, [(0, .013), (.004, .013), (.006, .009)], ALU, n=10)
    # side vents and rubber corner bumpers
    if p.lod < 2:
        for sx in (-1, 1):
            for y in (-.108, .108):
                for i in range(5):
                    obox(p, (sx * .2152, y, .07 + i * .017), (sx, 0, 0), UP, (.05, .007, .002), RUB)
    for sx in (-1, 1):
        for sy in (-1, 1):
            for z in (.017, .233):
                box(p, (sx * .207, sy * .143, z), (.036, .04, .034), RUB, bevel=.008, seg=2, tile=.08)
    side_handles(p, .215, plate_z=.17)
    decal(p, "name:battery-module", (0, -.1585, .066), FRONT, UP, w=.24, off=.0004)
    decal(p, "name:battery-module", (0, -.068, .235), UP, Y, w=.27, off=.0006)
    decal(p, "icon:hazard", (.018, -.1565, .162), FRONT, UP, w=.046, off=.0004)
    decal(p, "icon:plate", (0, .158, .12), BACK, UP, w=.11, off=.0004)


# ------------------------------------------------------------------ 5. oxygen: composite bottle in a tube cradle
def oxygen(p):
    sh = "CC2_Shell_Oxygen"
    R, az = .098, .142
    x0 = -.2
    prof = [(0, 0), (.004, .03), (.014, .062), (.032, .085), (.055, R), (.3, R), (.322, .086), (.34, .064), (.352, .036),
            (.358, .024), (.378, .024)]
    lathe(p, (x0, 0, az), X, prof, "CC2_Composite", n=28, tile=.25)
    lathe(p, (x0 + .29, 0, az), X, [(0, R + .0006), (.036, .084), (.052, .066), (.052, .060)], sh, n=28, tile=.3)   # shoulder paint
    # valve, handwheel, regulator and gauge
    box(p, (x0 + .385, 0, az), (.028, .04, .04), ALU, bevel=.004, seg=1, tile=.3)
    lathe(p, (x0 + .385, 0, az + .02), UP, [(0, .008), (.014, .008), (.014, .024), (.026, .024), (.028, .02), (.028, 0)], GRA, n=14)
    cyl(p, (x0 + .385, -.02, az), (x0 + .385, -.05, az), .011, ALU, n=10)
    lathe(p, (x0 + .385, -.05, az), FRONT, [(0, .016), (.02, .016), (.02, 0)], ALU, n=12)
    lathe(p, (x0 + .355, -.062, az + .024), FRONT, [(-.01, .0), (-.01, .028), (.012, .028), (.012, .024), (.009, .024), (.009, 0)], ALU, n=18)
    decal(p, "icon:gauge", (x0 + .355, -.0745, az + .024), FRONT, UP, w=.046, off=.0002)
    # band clamps on rubber saddles
    for x in (-.11, .05):
        lathe(p, (x - .013, 0, az), X, [(0, R), (0, R + .003), (.026, R + .003), (.026, R)], ALU, n=28, tile=.3)
        obox(p, (x, -R - .006, az + .02), FRONT, UP, (.03, .034, .012), ALU, bevel=.003)
        box(p, (x, 0, .034), (.034, .176, .045), RUB, bevel=.006, seg=1, tile=.08)
    # tube cradle: two end hoops and six longitudinals
    tr = .0095
    for x in (-.203, .203):
        hoop = fillet([(x, -.12, .012), (x, .12, .012), (x, .12, .252), (x, -.12, .252)], .03, 5 if p.lod == 0 else 3, closed=True)
        tube(p, hoop, tr, sh, n=10, tile=.3, closed=True)
        rbox(p, (x * 1.012, 0, .17), (.005, .2, .1), ALU, rv=.012, rh=0, axis=0, tile=.3)          # handle plate
    for y, z in ((-.085, .012), (.085, .012), (-.085, .252), (.085, .252), (-.12, .13), (.12, .13)):
        cyl(p, (-.203, y, z), (.203, y, z), tr, sh, n=10, tile=.3)
    for x in (-.11, .05):
        cyl(p, (x, -.085, .012), (x, .085, .012), tr * .9, sh, n=8, tile=.3)
    side_handles(p, .2085, plate_z=.17, plate_h=.05, plate_w=.16)
    feet(p, x=.17, y=.085, size=(.04, .03, .008))
    # placards: painted plate on the front rail, stencil on the bottle crown
    obox(p, (0, -.131, .112), FRONT, UP, (.25, .086, .003), sh, bevel=.001, tile=.3)
    decal(p, "name:oxygen-tank", (0, -.1325, .112), FRONT, UP, w=.215, off=.0004)
    cyl_decal(p, "name:oxygen-tank", -.03, R, 62, 0, az, w=.2)
    cyl_patch(p, L.LABELS, -.03, .1, R, 118, .053, 0, az, uv_rect=ATLAS["icon:nooil"])


# ------------------------------------------------------------------ 6. solar: roll-out wing canister on a skid
def solar(p):
    sh = "CC2_Shell_Solar"
    R, az = .118, .158
    lathe(p, (-.205, 0, az), X, [(0, 0), (0, .09), (.006, .108), (.016, R), (.394, R), (.404, .108), (.41, .09), (.41, 0)], sh, n=32, tile=.3)
    for x in (-.2, .184, -.148, .132):
        lathe(p, (x, 0, az), X, [(0, R), (0, R + .0045), (.016, R + .0045), (.016, R)], ALU, n=32, tile=.3)
    for x in (-.14, .14):                                                                         # clamp latches
        obox(p, (x, -R - .008, az - .012), FRONT, UP, (.026, .05, .014), ALU, bevel=.003)
        box(p, (x, 0, .038), (.036, .2, .06), GRA, bevel=.006, seg=1, tile=.2)                    # saddles
    if p.lod == 0:
        for sx in (-1, 1):
            for k in range(8):
                a = k * math.pi / 4 + math.pi / 8
                bolt(p, (sx * .205, math.cos(a) * .096, az + math.sin(a) * .096), (sx, 0, 0), r=.005, h=.004)
    for y in (-.088, .088):                                                                       # skid rails
        box(p, (0, y, .013), (.42, .028, .022), ALU, bevel=.004, seg=1, tile=.3)
    # stowed wing visible through the deployment slot
    cyl_patch(p, "CC2_SolarCell", 0, .25, R, 87, .058, 0, az, uv_tile=.16, off=.003)
    for a in (71.5, 102.5):
        y, z = -(R + .003) * math.cos(math.radians(a)), az + (R + .003) * math.sin(math.radians(a))
        n = Vector((0, -math.cos(math.radians(a)), math.sin(math.radians(a))))
        obox(p, (0, y, z), n, X, (.008, .27, .006), ALU, bevel=.002)
    for x in (-.13, .13):
        cyl_patch(p, ALU, x, .01, R + .006, 87, .068, 0, az, uv_tile=.3, off=0)
    side_handles(p, .2065, plate_z=.17, plate_h=.05, plate_w=.16)
    feet(p, x=.17, y=.088, size=(.045, .03, .008))
    cyl_decal(p, "name:solar-wing", 0, R, 36, 0, az, w=.215)
    decal(p, "line:solar-wing", (0, -.1025, .013), FRONT, UP, h=.016, off=.0004)


# ------------------------------------------------------------------ 7. water: insulated bladder case
def water(p):
    sh = "CC2_Shell_Water"
    rbox(p, (0, 0, .03), (.44, .31, .05), GRA, rv=.03, rh=.006, tile=.2)                          # rigid tray
    rbox(p, (0, 0, .152), (.425, .295, .2), sh, rv=.034, rh=.03, segv=4, segh=4, tile=.32)        # quilted jacket
    top = .252
    for x in (-.166, .166):                                                                       # cinch straps
        loop = fillet([(x, -.1485, .05), (x, -.1485, top + .001), (x, .1485, top + .001), (x, .1485, .05)], .032,
                      6 if p.lod == 0 else 3)
        ribbon(p, loop, X, .032, .003, WEB)
        obox(p, (x, -.154, .178), FRONT, UP, (.04, .052, .012), GRA, bevel=.004)                  # side-release buckle
        if p.lod < 2:
            obox(p, (x, -.158, .188), FRONT, UP, (.026, .018, .006), GRA, bevel=.002)
            obox(p, (x, -.152, .07), FRONT, UP, (.04, .014, .008), ALU, bevel=.002)               # tray anchor
            obox(p, (x, .152, .07), BACK, UP, (.04, .014, .008), ALU, bevel=.002)
    # fill port with knurled cap, vent
    lathe(p, (.105, .0, top - .002), UP, [(0, .042), (.006, .042), (.008, .036), (.008, .031), (.03, .031), (.033, .027), (.033, 0)], ALU, n=20, tile=.1)
    lathe(p, (.105, .0, top + .006), UP, [(0, .0325), (.02, .0325)], RUB, n=20, tile=.05) if p.lod < 2 else None
    lathe(p, (.105, -.085, top - .002), UP, [(0, .014), (.01, .014), (.014, .009), (.014, 0)], GRA, n=12)
    # drain valve on the tray front
    lathe(p, (.14, -.153, .031), FRONT, [(0, .016), (.008, .016), (.008, .011), (.026, .011), (.026, .014), (.03, .014), (.03, 0)], ALU, n=14)
    lathe(p, (.14, -.181, .031), FRONT, [(0, .0155), (.014, .0155), (.016, .012), (.016, 0)], RED, n=14, tile=.08)
    obox(p, (.14, -.166, .054), FRONT, (.5, 0, .86), (.01, .04, .006), RED, bevel=.002)
    # rigid end brackets carrying the grips
    for sx in (-1, 1):
        rbox(p, (sx * .2175, 0, .118), (.007, .19, .18), ALU, rv=.02, rh=0, axis=0, tile=.3)
        for y in (-.07, .07):
            bolt(p, (sx * .221, y, .04), (sx, 0, 0))
    side_handles(p, .221, plate_z=.172, plate_h=.05)
    feet(p)
    decal(p, "name:water-tank", (0, -.1478, .15), FRONT, UP, w=.24, off=.0012)
    decal(p, "name:water-tank", (-.04, 0, top), UP, Y, w=.19, off=.0012)
    decal(p, "icon:potable", (-.13, -.155, .03), FRONT, UP, w=.075, off=.0006)
    decal(p, "icon:plate", (0, .155, .03), BACK, UP, h=.04, off=.0006)


CASES = [("Water", "water-tank", water), ("Oxygen", "oxygen-tank", oxygen), ("Food", "food-locker", food),
         ("Battery", "battery-module", battery), ("Solar", "solar-wing", solar), ("Spares", "spare-parts-kit", spares),
         ("Science", "spectrometer", science)]


def export(objs, name):
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objs:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=os.path.join(MODELS, name + ".fbx"), use_selection=True, object_types={'MESH'},
                             use_mesh_modifiers=True, mesh_smooth_type='OFF', apply_unit_scale=True,
                             apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
                             bake_space_transform=True, add_leaf_bones=False, bake_anim=False, use_tspace=False)


def main():
    os.makedirs(MODELS, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = 'METRIC'
    report = {}
    extra = []
    try:
        import cc2_rack
        importlib.reload(cc2_rack)
        extra = cc2_rack.PARTS
    except ImportError:
        pass
    jobs = [(key, cid, fn) for key, cid, fn in CASES] + list(extra)
    for i, (key, cid, fn) in enumerate(jobs):
        col = bpy.data.collections.new("CC2_" + key)
        scene.collection.children.link(col)
        objs = []
        entry = {"id": cid, "triangles": []}
        for lod in range(3):
            part = Part("%s_LOD%d" % (key, lod), lod, ATLAS)
            fn(part)
            entry["triangles"].append(part.tris())
            entry["materials"] = list(part.mats)
            ob = L.build_object(part, material, col)
            objs.append(ob)
        bb = [v.co for v in objs[0].data.vertices]
        entry["bounds_min"] = [round(min(v[k] for v in bb), 4) for k in range(3)]
        entry["bounds_max"] = [round(max(v[k] for v in bb), 4) for k in range(3)]
        export(objs, "CC2_" + key)
        report[key] = entry
        # lay the LOD0 objects out in a row for the .blend / previews
        for lod, ob in enumerate(objs):
            ob.location = ((i - 3) * .75, lod * .8, 0)
    json.dump(report, open(os.path.join(MODELS, "CargoCasesV02_report.json"), "w"), indent=1)
    for k, e in report.items():
        print("CC2 %-8s tris %s bounds %s %s" % (k, e["triangles"], e["bounds_min"], e["bounds_max"]))
    if "--blend" in sys.argv:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "CargoCasesV02.blend"))


if __name__ == "__main__":
    main()
