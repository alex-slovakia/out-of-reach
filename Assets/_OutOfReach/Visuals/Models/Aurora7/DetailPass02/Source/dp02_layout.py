"""
dp02_layout - where the SHIP-DETAIL-02 clusters go in AURORA-7 (ship coordinates, Unity metres, y up).

Every cluster is one object. Its pivot is the origin of its mount frame: a point on the host surface at deck
level (y = 0) under the cluster, so the FBX can be dropped at the ship origin with identity rotation.
Hosts are taken from a7_ship.py / the integrated habitat:
  ring-frame flange faces  x = +-2.204 at frame stations z 4.6, 6.2, 7.8, 9.4 (engineering)
  portal wing faces        z = 10.92 (engineering side of the storage/habitat portal), wings |x| 1.04 .. 2.40
"""
import sys

sys.dont_write_bytecode = True
from a7kit import front_frame  # noqa: E402
import dp02_kit as kit  # noqa: E402

FLANGE_X = 2.4 - 0.196     # room-facing face of a ring-frame flange (a7_ship.ring_frame)
AFT_WING_Z = 11.0 - 0.08   # engineering-side face of the portal at z = 11 (a7_ship.portal)

ROOMS = {
    "Engineering": {
        "fbx": "DP02_Engineering",
        "clusters": [
            {"name": "DP02_ENG_FrameLoom_L_Z460", "pivot": (-FLANGE_X, 0.0, 4.6), "out": (1, 0, 0),
             "host": "port (-x) ring-frame flange face at z 4.6, between bays z 3.8 and 5.4",
             "build": lambda atlas: kit.frame_loom_cluster(atlas, "c_hv", -0.116, (4, 5, 6, 7))},
            {"name": "DP02_ENG_FrameLoom_R_Z940", "pivot": (FLANGE_X, 0.0, 9.4), "out": (-1, 0, 0),
             "host": "starboard (+x) ring-frame flange face at z 9.4, between bays z 8.6 and 10.2",
             "build": lambda atlas: kit.frame_loom_cluster(atlas, "c_hv", -0.116, (10, 11, 12, 13))},
            {"name": "DP02_ENG_AccessPanel_AftL", "pivot": (-1.98, 0.0, AFT_WING_Z), "out": (0, 0, -1),
             "host": "port wing of the aft portal (z 11), engineering face, outboard of the vertical strip",
             "build": lambda atlas: kit.access_panel_cluster(atlas, -1, "pn_11")},
            {"name": "DP02_ENG_AccessPanel_AftR", "pivot": (1.98, 0.0, AFT_WING_Z), "out": (0, 0, -1),
             "host": "starboard wing of the aft portal (z 11), engineering face, outboard of the vertical strip",
             "build": lambda atlas: kit.access_panel_cluster(atlas, 1, "pn_12")},
        ],
        "review": {"anchor": "DP02_ENG_ReviewEye", "eye": (0.75, 1.62, 8.55), "target": (2.1, 1.05, 10.0), "fov": 74},
    },
}


def frame_of(cluster):
    return front_frame(cluster["pivot"], cluster["out"])
