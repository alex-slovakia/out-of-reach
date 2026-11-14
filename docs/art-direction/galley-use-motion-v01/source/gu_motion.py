"""gu_motion - GALLEY-USE-MOTION-01: staged meal and drink motions for the AURORA-7 galley (original work).

Pure numpy, Unity space (metres, y up, left-handed; rotations as proper 3x3 matrices, quaternions x, y, z, w).
World = the HABITAT galley unit's own space (feature/habitat-details-v1): unit origin on the wall at deck level,
its front +z towards the room; the deployed table top is at y 0.9975. Mount offsets for the ship are in the README.

Camera: the player's eye stands in front of the galley and looks at the table. The gloves' rig (AstronautHands,
f72224c) has its root at the eye with x right, y up, z forward, so arm placements are given in camera space.
Mouth: assumed at (0, -0.085, 0.03) in camera space (8.5 cm below the eye pivot, 3 cm ahead). The body rig should
confirm or replace it; it is the anchor this kit cannot prove.

Hands: the approved rig has Rest, Neutral, Grip (a fist round a 34 mm tube) and Press. None of them can carry a
pouch (measured in build_motion's fit check: a fist round the drink adapter is cut by the straw, the septum and the
foil; the pouch bodies are 95 and 125 mm wide), so both clips move the prop along the path a carrying hand would
take and record the exact missing anchor, a clasp target, in every frame. The left glove's Press pose is proven on
the drink's pinch clamp: its index tip rests on the clamp and slides it down at the Closed -> Open swap.
Unsuited only: the mouth is the bare face; these clips must never play with the suit or helmet on.
"""
import json
import math

import numpy as np

FPS = 30
EYE = np.array([0.30, 1.62, 1.28])           # galley space; player standing ~0.5 m in front of the table edge
LOOK = np.array([0.33, 1.05, 0.62])
MOUTH_CAM = np.array([0.0, -0.085, 0.03])
TABLE_TOP = 0.9975


# ----------------------------------------------------------------------------- rotation helpers
def unit(v):
    v = np.asarray(v, dtype=float)
    return v / np.linalg.norm(v)


def frame(x=None, y=None, z=None):
    """Proper rotation from two given axes (the third by the numeric cross product, Unity convention)."""
    if x is not None and z is not None:
        x, z = unit(x), unit(z)
        z = unit(z - np.dot(z, x) * x)
        y = np.cross(z, x)
    elif y is not None and z is not None:
        y, z = unit(y), unit(z)
        y = unit(y - np.dot(y, z) * z)
        x = np.cross(y, z)
    else:
        raise ValueError("two axes needed")
    return np.column_stack([x, y, z])


def axis_angle(axis, deg):
    a = unit(axis)
    t = math.radians(deg)
    k = np.array([[0, -a[2], a[1]], [a[2], 0, -a[0]], [-a[1], a[0], 0]])
    return np.eye(3) + math.sin(t) * k + (1 - math.cos(t)) * k @ k


def to_quat(R):
    w = math.sqrt(max(0.0, 1.0 + R[0, 0] + R[1, 1] + R[2, 2])) / 2
    x = math.sqrt(max(0.0, 1.0 + R[0, 0] - R[1, 1] - R[2, 2])) / 2
    y = math.sqrt(max(0.0, 1.0 - R[0, 0] + R[1, 1] - R[2, 2])) / 2
    z = math.sqrt(max(0.0, 1.0 - R[0, 0] - R[1, 1] + R[2, 2])) / 2
    x = math.copysign(x, R[2, 1] - R[1, 2])
    y = math.copysign(y, R[0, 2] - R[2, 0])
    z = math.copysign(z, R[1, 0] - R[0, 1])
    q = np.array([x, y, z, w])
    return q / np.linalg.norm(q)


def from_quat(q):
    x, y, z, w = q
    return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                     [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                     [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])


def slerp(qa, qb, s):
    qa, qb = np.asarray(qa, float), np.asarray(qb, float)
    d = float(np.dot(qa, qb))
    if d < 0:
        qb, d = -qb, -d
    if d > 0.9995:
        q = qa + s * (qb - qa)
        return q / np.linalg.norm(q)
    th = math.acos(d)
    return (math.sin((1 - s) * th) * qa + math.sin(s * th) * qb) / math.sin(th)


def smooth(s):
    return s * s * (3 - 2 * s)


def pose(p, R):
    m = np.eye(4)
    m[:3, :3], m[:3, 3] = R, p
    return m


def inv(m):
    r = np.eye(4)
    r[:3, :3] = m[:3, :3].T
    r[:3, 3] = -m[:3, :3].T @ m[:3, 3]
    return r


def camera():
    f = unit(LOOK - EYE)
    r = unit(np.cross(np.array([0.0, 1.0, 0.0]), f))
    u = np.cross(f, r)
    return pose(EYE, np.column_stack([r, u, f]))


CAM = camera()


def cam_point(p):
    return (CAM @ np.append(p, 1.0))[:3]


def cam_dir(v):
    return CAM[:3, :3] @ np.asarray(v, dtype=float)


MOUTH = cam_point(MOUTH_CAM)


# ----------------------------------------------------------------------------- glove rig (AstronautHands)
class Rig:
    def __init__(self, path):
        data = json.load(open(path, encoding="utf-8"))
        self.joints = {j["name"]: j for j in data["joints"]}
        self.poses = {p["name"]: {r["name"]: np.array(r["rotation"]) for r in p["rotations"]} for p in data["poses"]}
        self.placements = {p["name"]: {pl["name"]: (np.array(pl["position"]), np.array(pl["rotation"]))
                                       for pl in p["placements"]} for p in data["poses"]}
        self.poses["Rest"] = {}

    def children(self, root):
        out = []
        for name, j in self.joints.items():
            n = name
            while n:
                if n == root:
                    out.append(name)
                    break
                n = self.joints[n]["parent"]
        return out

    def local(self, name, pose_name, blend_from=None, weight=1.0):
        q = np.array(self.poses[pose_name].get(name, [0, 0, 0, 1]), dtype=float)
        if blend_from is not None and weight < 1.0:
            q = slerp(np.array(self.poses[blend_from].get(name, [0, 0, 0, 1]), dtype=float), q, weight)
        return from_quat(q)

    def world(self, side, arm, pose_name, blend_from=None, weight=1.0):
        """World transforms of every part of one hand, given the Arm placement (4x4 in world space); the pose can
        be a slerp from blend_from (weight 0) to pose_name (weight 1), as the rig README suggests."""
        result = {}

        def walk(name, parent_m):
            j = self.joints[name]
            m = parent_m @ pose(np.array(j["position"]) if j["parent"] else np.zeros(3),
                                np.eye(3) if not j["parent"] else self.local(name, pose_name, blend_from, weight))
            if not j["parent"]:
                m = arm.copy()
            result[name] = m
            for child, cj in self.joints.items():
                if cj["parent"] == name:
                    walk(child, m)
        walk(side + "_Arm", np.eye(4))
        return result

    def hand_from_arm(self, side, pose_name):
        """Hand joint relative to Arm: same pivot, the pose's wrist rotation."""
        return pose(np.zeros(3), self.local(side + "_Hand", pose_name))

    def point_in_hand(self, side, pose_name, joint, offset=(0, 0, 0)):
        """A point given in a finger joint's frame, expressed in the Hand joint's frame for this pose."""
        chain, n = [], side + "_" + joint
        while n != side + "_Hand":
            chain.append(n)
            n = self.joints[n]["parent"]
        m = np.eye(4)
        for n in reversed(chain):
            m = m @ pose(np.array(self.joints[n]["position"]), self.local(n, pose_name))
        return (m @ np.append(np.asarray(offset, float), 1.0))[:3]


# ----------------------------------------------------------------------------- grips and clips
GRIP_AXIS_POINT = np.array([0.0, -0.033, 0.082])      # AstronautHands README: Grip tube-axis point in R_Hand space
PRESS_TIP_L = np.array([0.0411, -0.0094, 0.1945])     # README: Press index tip, R_Hand (-0.0411, ...); x negated for L
DRINK_ADAPTER = np.array([0.0, 0.078, 0.0])           # gc_kit: adapter centre, prop space (after placing, x mirrors)
CLAMP_CLOSED = np.array([-0.002, 0.126, 0.0])         # gc_kit: clamp jaw centre when closed; open sits 20 mm lower
CLAMP_DROP = 0.020
DRINK_CLAMP_OPEN_T = 1.30                             # Closed -> Open swap: clamp slid down, cap off on its tether
PRESS_T = (0.95, 1.15, 1.40, 1.60)                    # left index: leave neutral, on the clamp, leave it, neutral
PRESS_OFFSET_CAM = np.array([-0.007, 0.004, 0.0])     # contact beside the straw, on the clamp's hinge side
PRESS_R = frame(y=cam_dir((0.0, 1.0, 0.15)), z=cam_dir((0.92, -0.20, 0.30)))   # left Hand: finger across the straw


def adapter_fist_grip():
    """REJECTED, fit study only: the drink pouch in the right Hand frame with the Grip fist round its adapter (fist
    axis along the prop's x). The straw (prop +y) then runs into the fingers and the body back into the palm."""
    R = np.column_stack([[1.0, 0.0, 0.0], [0.0, 0.0, 1.0], [0.0, -1.0, 0.0]])
    t = GRIP_AXIS_POINT - R @ DRINK_ADAPTER
    return pose(t, R)


def keyed(keys, t):
    """keys: list of (time, 4x4); returns the smoothly interpolated pose at t."""
    if t <= keys[0][0]:
        return keys[0][1]
    for (ta, ma), (tb, mb) in zip(keys, keys[1:]):
        if t <= tb:
            s = smooth((t - ta) / (tb - ta)) if tb > ta else 1.0
            q = slerp(to_quat(ma[:3, :3]), to_quat(mb[:3, :3]), s)
            return pose(ma[:3, 3] + (mb[:3, 3] - ma[:3, 3]) * s, from_quat(q))
    return keys[-1][1]


def lying(p, yaw=0.0):
    """Prop lying on its back on the table, label up, straw/top pointing away from the player (-z)."""
    R = axis_angle((0, 1, 0), yaw) @ np.column_stack([[1.0, 0, 0], [0, 0, -1.0], [0, 1.0, 0]])
    return pose(p, R)


def facing_player(p, tilt_back=0.0):
    """Prop upright in front of the face, label towards the eye, tilted top-towards-face by tilt_back degrees."""
    z = unit(EYE - p)
    z[1] = 0.0
    z = unit(z)
    R = frame(y=np.array([0.0, 1.0, 0.0]), z=z)
    R = axis_angle(R[:, 0], -tilt_back) @ R
    return pose(p, R)


def anchor_at(prop_pose_fn, anchor_local, target, *args):
    """Place a prop so that one of its anchors lands on target (keeping the orientation from prop_pose_fn)."""
    m = prop_pose_fn(np.zeros(3), *args)
    m[:3, 3] = target - m[:3, :3] @ anchor_local
    return m


def meal_clip(meal_mouth, meal_grip, thickness):
    rest = lying(np.array([0.638, TABLE_TOP + thickness / 2 + 0.0005, 0.620]))
    lifted = facing_player(np.array([0.50, 1.20, 0.80]), 10)
    near = anchor_at(facing_player, meal_mouth, MOUTH + cam_dir((0.0, -0.03, 0.05)), 30)
    eat = anchor_at(facing_player, meal_mouth, MOUTH + cam_dir((0.0, -0.005, 0.012)), 40)
    keys = [(0.0, rest), (0.4, rest), (0.9, lifted), (1.6, near), (1.8, eat), (2.3, eat), (2.6, near), (3.4, rest)]
    return keys, 3.4


def drink_clip(straw_tip):
    rest = lying(np.array([0.035, TABLE_TOP + 0.0125 + 0.0005, 0.640]), 180.0)
    hold = facing_player(cam_point((0.04, -0.15, 0.36)), 5)   # in the lower third of the view while the clamp opens
    tip_cam = MOUTH + cam_dir((0.0, -0.004, 0.006))
    drink = anchor_at(facing_player, straw_tip, tip_cam, 35)
    near = anchor_at(facing_player, straw_tip, MOUTH + cam_dir((0.0, -0.05, 0.06)), 20)
    keys = [(0.0, rest), (0.45, rest), (0.95, hold), (1.55, hold), (2.15, near), (2.35, drink), (2.95, drink),
            (3.25, near), (4.1, rest)]
    return keys, 4.1


def sample(keys, duration):
    n = int(round(duration * FPS)) + 1
    return [(i / FPS, keyed(keys, i / FPS)) for i in range(n)]


def to_record(m):
    return {"position": [round(float(c), 5) for c in m[:3, 3]], "rotation": [round(float(c), 6) for c in to_quat(m[:3, :3])]}


def hand_placement(rig, side, pose_name, hand_world):
    """Arm placement in camera space (what AstronautHandsPresenter sets on <side>_Arm) for a Hand world pose."""
    arm_world = hand_world @ inv(rig.hand_from_arm(side, pose_name))
    return inv(CAM) @ arm_world


def neutral_arm(rig, side):
    pos, rot = rig.placements["Neutral"][side + "_Arm"]
    return CAM @ pose(pos, from_quat(rot))
