"""
dp02_layout - where the SHIP-DETAIL-02 clusters go in AURORA-7 (ship coordinates, Unity metres, y up).

Every cluster is one object. Its pivot is the origin of its mount frame: a point on the host surface at deck
level (y = 0) under the cluster, so the FBX can be dropped at the ship origin with identity rotation.
Hosts are taken from a7_ship.py / the integrated habitat:
  ring-frame flange faces  x = +-2.204 at frame stations z 4.6, 6.2, 7.8, 9.4 (engineering)
  portal wing faces        z = 10.92 (engineering side of the storage/habitat portal), wings |x| 1.04 .. 2.40
CKP-REFINE (room "CockpitRefine") sits on the cockpit console and the two crew seats; its hosts are taken from
a7_ship.console() and a7_ship.seat() (see dp02_cockpit_refine.py).
"""
import sys

sys.dont_write_bytecode = True
from a7kit import front_frame  # noqa: E402
import dp02_cockpit as ckp  # noqa: E402
import dp02_cockpit_refine as ckr  # noqa: E402
import dp02_habitat as hab  # noqa: E402
import dp02_kit as kit  # noqa: E402

FLANGE_X = 2.4 - 0.196     # room-facing face of a ring-frame flange (a7_ship.ring_frame)
QUILT_X = 2.38 - 0.0535    # blanket-bay host plane: quilt face (2.33) plus 3.5 mm, clear of the quilt's decals
WALL_X = 2.38              # bare side wall (habitat bay without a bay unit)
CKP_FLANGE_X = 3.0 - 0.196             # cockpit (6 m wide) ring-frame flange face
CKP_QUILT_X = 2.98 - 0.0535            # cockpit blanket-bay host plane (quilt face plus 3.5 mm)
FWD_WING_Z = 3.0 - 0.08                # cockpit-side face of the portal at z = 3
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
    "Cockpit": {
        "fbx": "DP02_Cockpit",
        "clusters": [
            {"name": "DP02_CKP_FrameLoom_L_Z000", "pivot": (-CKP_FLANGE_X, 0.0, 0.0), "out": (1, 0, 0),
             "host": "port (-x) cockpit ring-frame flange face at z 0.0, between the locker bay (z -0.75) and the "
                     "blanket bay (z 0.75); beside and behind the commander's seat",
             "build": lambda c: kit.frame_loom_cluster(c["decals"], "c_hv", -0.116, (0, 1, 2, 3))},
            {"name": "DP02_CKP_FrameLoom_R_Z150", "pivot": (CKP_FLANGE_X, 0.0, 1.5), "out": (-1, 0, 0),
             "host": "starboard (+x) cockpit ring-frame flange face at z 1.5, between the locker bay (z 0.75) and "
                     "the blanket bay (z 2.25); clear of the deep stowage bay at z -0.75",
             "build": lambda c: kit.frame_loom_cluster(c["decals"], "c_hv", -0.116, (8, 9, 10, 11))},
            {"name": "DP02_CKP_CrewNotes_L_Z075", "pivot": (-CKP_QUILT_X, 0.0, 0.75), "out": (1, 0, 0),
             "host": "port cockpit blanket bay z 0.75 (behind the commander's seat): quilt face plus 3.5 mm, above "
                     "the bay handrail",
             "build": lambda c: ckp.crew_notes_cluster(c["decals"], c["hb"])},
            {"name": "DP02_CKP_AccessPanel_AftL", "pivot": (-1.98, 0.0, FWD_WING_Z), "out": (0, 0, -1),
             "host": "port wing of the portal at z 3, cockpit face, outboard of the vertical strip",
             "build": lambda c: kit.access_panel_cluster(c["decals"], -1, "pn_14")},
            {"name": "DP02_CKP_AccessPanel_AftR", "pivot": (1.98, 0.0, FWD_WING_Z), "out": (0, 0, -1),
             "host": "starboard wing of the portal at z 3, cockpit face, outboard of the vertical strip",
             "build": lambda c: kit.access_panel_cluster(c["decals"], 1, "pn_15")},
        ],
        "review": {"anchor": "DP02_CKP_ReviewEye", "eye": (0.3, 1.62, 0.95), "target": (-2.8, 1.2, 0.3), "fov": 70},
        "extra_views": [{"name": "CockpitAft", "eye": (0.2, 1.62, 0.2), "target": (1.4, 1.25, 2.9), "fov": 72}],
    },
    "CockpitRefine": {
        "fbx": "DP02_CockpitRefine",
        "clusters": [
            {"name": "DP02_CKR_Cabinets", "pivot": (0.0, 0.0, round(ckr.CONSOLE_Z + ckr.CAB_FACE_Z, 4)), "out": (0, 0, 1),
             "host": "front faces of the three console cabinets (ship z -1.62): seam battens over the two cabinet "
                     "joints, edge angles on the outer front edges, knurled rubber kick strips at the cabinet feet",
             "build": lambda c: ckr.cabinets_cluster()},
            {"name": "DP02_CKR_WorkSurfaceEdge", "pivot": (0.0, 0.0, round(ckr.CONSOLE_Z + ckr.TOP_FRONT_Z, 4)), "out": (0, 0, 1),
             "host": "console work surface (top y 0.97, crew edge ship z -1.525): J-nosing split around the handrail "
                     "standoffs, end caps on both ends, guard cheeks on the red switch's base plate",
             "build": lambda c: ckr.work_surface_cluster()},
            {"name": "DP02_CKR_PanelBezels", "pivot": (0.0, 0.0, round(ckr.CONSOLE_Z + ckr.PANEL_BASE_Z, 4)), "out": (0, 0, 1),
             "host": "instrument panel leaning back 15 degrees (base ship z -2.12): filler strips and quarter-turn "
                     "fasteners, display and key-column bezel screws, end posts, glare-shield hood lip",
             "build": lambda c: ckr.panel_cluster()},
            {"name": "DP02_CKR_SeatSpine_L", "pivot": ckr.spine_pivot(-1), "out": (0, 0, 1),
             "host": "rear face of the commander's seat spine (seat at x -1.15, z -0.45; back reclined 12 degrees)",
             "build": lambda c: ckr.seat_spine_cluster(-1, ckr.spine_pivot(-1))},
            {"name": "DP02_CKR_SeatSpine_R", "pivot": ckr.spine_pivot(1), "out": (0, 0, 1),
             "host": "rear face of the flight engineer's seat spine (seat at x 1.15, z -0.45)",
             "build": lambda c: ckr.seat_spine_cluster(1, ckr.spine_pivot(1))},
        ],
        # first person: standing (1.65 m eye) at the console, where the player uses the CockpitConsole interaction
        "review": {"anchor": "DP02_CKR_ReviewEye", "eye": (0.25, 1.65, -1.05), "target": (-0.2, 1.02, -2.0), "fov": 80,
                   "file": "DP02_CKR_FirstPerson.png"},
        "extra_views": [
            {"name": "CKR_Front", "file": "DP02_CKR_Front.png", "isolated": True, "ortho": 3.45,
             "eye": (0.0, 0.93, 1.5), "target": (0.0, 0.93, -1.95)},
            {"name": "CKR_Side", "file": "DP02_CKR_Side.png", "isolated": True, "ortho": 3.0,
             "eye": (3.5, 0.83, -1.95), "target": (0.0, 0.83, -1.95)},
            {"name": "CKR_Before", "file": "DP02_CKR_Before.png", "hide_rooms": ["CockpitRefine"], "res": (1280, 720),
             "eye": (-0.45, 1.48, -1.02), "target": (0.95, 0.98, -1.95), "fov": 58},
            {"name": "CKR_After", "file": "DP02_CKR_After.png", "res": (1280, 720),
             "eye": (-0.45, 1.48, -1.02), "target": (0.95, 0.98, -1.95), "fov": 58},
        ],
    },
}


def frame_of(cluster):
    return front_frame(cluster["pivot"], cluster["out"])
