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
import dp02_habitat as hab  # noqa: E402
import dp02_kit as kit  # noqa: E402

FLANGE_X = 2.4 - 0.196     # room-facing face of a ring-frame flange (a7_ship.ring_frame)
QUILT_X = 2.38 - 0.0535    # blanket-bay host plane: quilt face (2.33) plus 3.5 mm, clear of the quilt's decals
WALL_X = 2.38              # bare side wall (habitat bay without a bay unit)
AFT_WING_Z = 11.0 - 0.08   # engineering-side face of the portal at z = 11 (a7_ship.portal)

ROOMS = {
    "Engineering": {
        "fbx": "DP02_Engineering",
        "clusters": [
            {"name": "DP02_ENG_FrameLoom_L_Z460", "pivot": (-FLANGE_X, 0.0, 4.6), "out": (1, 0, 0),
             "host": "port (-x) ring-frame flange face at z 4.6, between bays z 3.8 and 5.4",
             "build": lambda c: kit.frame_loom_cluster(c["decals"], "c_hv", -0.116, (4, 5, 6, 7))},
            {"name": "DP02_ENG_FrameLoom_R_Z940", "pivot": (FLANGE_X, 0.0, 9.4), "out": (-1, 0, 0),
             "host": "starboard (+x) ring-frame flange face at z 9.4, between bays z 8.6 and 10.2",
             "build": lambda c: kit.frame_loom_cluster(c["decals"], "c_hv", -0.116, (10, 11, 12, 13))},
            {"name": "DP02_ENG_AccessPanel_AftL", "pivot": (-1.98, 0.0, AFT_WING_Z), "out": (0, 0, -1),
             "host": "port wing of the aft portal (z 11), engineering face, outboard of the vertical strip",
             "build": lambda c: kit.access_panel_cluster(c["decals"], -1, "pn_11")},
            {"name": "DP02_ENG_AccessPanel_AftR", "pivot": (1.98, 0.0, AFT_WING_Z), "out": (0, 0, -1),
             "host": "starboard wing of the aft portal (z 11), engineering face, outboard of the vertical strip",
             "build": lambda c: kit.access_panel_cluster(c["decals"], 1, "pn_12")},
        ],
        "review": {"anchor": "DP02_ENG_ReviewEye", "eye": (0.75, 1.62, 8.55), "target": (2.1, 1.05, 10.0), "fov": 74},
    },
    "Habitat": {
        "fbx": "DP02_Habitat",
        "clusters": [
            {"name": "DP02_HAB_StrapStow_R_Z1325", "pivot": (QUILT_X, 0.0, 13.25), "out": (-1, 0, 0),
             "host": "starboard (+x) blanket bay z 13.25 (opposite the berth): quilt face plus 3.5 mm, edge strips",
             "build": lambda c: hab.stowage_cluster(c["decals"], c["hb"], c["art"])},
            {"name": "DP02_HAB_CrewKit_L_Z1175", "pivot": (-QUILT_X, 0.0, 11.75), "out": (1, 0, 0),
             "host": "port (-x) blanket bay z 11.75 (beside the berth entrance): quilt face plus 3.5 mm",
             "build": lambda c: hab.crew_kit_cluster(c["decals"], c["hb"])},
            {"name": "DP02_HAB_GrowPlumbing_R_Z1414", "pivot": (WALL_X, 0.0, 14.145), "out": (-1, 0, 0),
             "host": "starboard wall strip z 14.04-14.25 between the ring frame (z 14.0) and the grow cassette's "
                     "forward side face (z 14.25), plus fittings on that face",
             "build": lambda c: hab.grow_plumbing_cluster(c["decals"], c["hb"])},
            {"name": "DP02_HAB_GrowCables_R_Z1536", "pivot": (WALL_X, 0.0, 15.355), "out": (-1, 0, 0),
             "host": "starboard wall strip z 15.25-15.46 between the grow cassette's aft side face and the ring "
                     "frame (z 15.5), plus connectors on that face",
             "build": lambda c: hab.grow_cable_cluster(c["decals"], c["hb"])},
        ],
        "review": {"anchor": "DP02_HAB_ReviewEye", "eye": (-0.9, 1.62, 12.15), "target": (2.2, 1.15, 13.95), "fov": 76},
        "extra_views": [{"name": "HabitatCrewKit", "eye": (0.55, 1.62, 13.1), "target": (-2.3, 1.7, 11.75), "fov": 62},
                        {"name": "HabitatGrowPlumbing", "eye": (1.05, 1.42, 13.35), "target": (2.3, 0.72, 14.3), "fov": 58}],
    },
}


def frame_of(cluster):
    return front_frame(cluster["pivot"], cluster["out"])
