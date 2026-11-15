"""Shared constants: label atlas layout (u0, v0, u1, v1, v up) and existing-structure anchors measured from
MarinePrologue/Transport/TransportLaunch.fbx and MarineDock.fbx (Unity metres, root frame of the I06 prologue)."""

ATLAS_W, ATLAS_H = 2048, 1024
# plate name -> (rect in atlas, plate aspect w:h)
LABELS = {
    'tower_main': (0.0, 0.75, 1.0, 1.0),       # 8:1  AURORA-7 / CREW ACCESS TOWER
    'crew_hood':  (0.0, 0.5, 0.75, 0.75),      # 6:1  AURORA-7 / CREW HATCH CONNECTION
    'deluge':     (0.0, 0.25, 0.5, 0.5),       # 4:1  DELUGE RISER DN200
    'cable':      (0.5, 0.25, 1.0, 0.5),       # 4:1  CABLE TRAY C-1
    'valve':      (0.75, 0.5, 1.0, 0.75),      # 1.5:1 V-07 DELUGE ISOLATION
}
LEVELS = {i: (0.1 * (i - 1), 0.0, 0.1 * i, 0.25) for i in range(1, 11)}   # "L1".."L10"  (0.8:1)

# measured anchors (Unity root frame)
PAD_Y = -32.6                      # tower foot
SHAFT_X, SHAFT_Z0, SHAFT_Z1 = 1.65, -10.15, -6.85
SHAFT_CZ = -8.5
DECK_X, DECK_Z0, DECK_Z1 = 4.0, -12.9, -6.9
ARM_PIVOT = (-0.9, 0.0, -6.9)
ARM_X, ARM_Z0, ARM_Z1 = 0.98, -6.9, -2.48
HATCH_PIVOT = (-0.72, 0.0, -2.4)
