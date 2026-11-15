"""grip_solve - WORK-BARE-GRIP-01: bare WORK hand wrapped around a round 15 mm-radius rail, on the original
A7_BodyFit02_WORK rig (164 bones, original bind, metre scale). Finger and thumb bones only.

Per hand:
  1. Rail across the palm (axis along the index->little knuckle line), moved in from the palmar side until the bind
     skin first touches it (no penetration).
  2. Fingers: MCP, then PIP, then DIP each bend (about the axis that curls the bone towards the palm) to the largest
     angle at which the evaluated skin of that finger does not enter the rail.
  3. Thumb: CMC opposition picked from a grid (no rail penetration, no thumb/finger triangle intersections, closest
     pad), then MP and IP bend to contact the same way.
  4. Evidence on the evaluated skin: penetrating vertices, minimum gap per region, digit/digit intersections, the
     whole open->closed path sampled per frame.
Exports per hand an animation-only FBX (FBX_SCALE_ALL: unit rig scale, metre keys; finger/thumb curves only; bind
keyed at frame 100 outside the take so the bone nodes stay the original bind), grip_anchors.json and stills.

Run: blender -b --factory-startup --python grip_solve.py -- --fbx <A7_BodyFit02_WORK.fbx> --out <dir> [--render]
"""
import json
import math
import os
import sys

import bpy
import numpy as np
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
sys.dont_write_bytecode = True
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
get = lambda k, d=None: ARGS[ARGS.index(k) + 1] if k in ARGS else d
FBX = os.path.abspath(get("--fbx"))
OUT = os.path.abspath(get("--out"))
RENDER = "--render" in ARGS
RADIUS, RAIL_LEN, TOL = 0.015, 0.30, -0.00005   # contact = closer than 0.05 mm: strict clearance
FPS, LAST, REST_FRAME = 30, 21, 100
DIGITS = {"thumb": ("finger1-1", "finger1-2", "finger1-3"), "index": ("finger2-1", "finger2-2", "finger2-3"),
          "middle": ("finger3-1", "finger3-2", "finger3-3"), "ring": ("finger4-1", "finger4-2", "finger4-3"),
          "little": ("finger5-1", "finger5-2", "finger5-3")}
JOINT_MAX = (math.radians(95.0), math.radians(110.0), math.radians(85.0))
M_U = np.array([[-1.0, 0.0, 0.0], [0.0, 0.0, 1.0], [0.0, -1.0, 0.0]])   # Blender armature -> Unity model (-x, z, -y)
LOG = []


def log(*a):
    s = " ".join(str(x) for x in a)
    LOG.append(s)
    print("[grip]", s, flush=True)


def unit(v):
    v = np.asarray(v, float)
    return v / np.linalg.norm(v)


def rot(axis, ang):
    if abs(ang) < 1e-12 or np.linalg.norm(axis) < 1e-12:
        return np.eye(3)                      # zero angle: identity (a zero axis must not reach unit())
    a = unit(axis)
    K = np.array([[0, -a[2], a[1]], [a[2], 0, -a[0]], [-a[1], a[0], 0]])
    return np.eye(3) + math.sin(ang) * K + (1 - math.cos(ang)) * K @ K


def quat(R):
    w = math.sqrt(max(0.0, 1.0 + R[0, 0] + R[1, 1] + R[2, 2])) / 2
    x = math.copysign(math.sqrt(max(0.0, 1.0 + R[0, 0] - R[1, 1] - R[2, 2])) / 2, R[2, 1] - R[1, 2])
    y = math.copysign(math.sqrt(max(0.0, 1.0 - R[0, 0] + R[1, 1] - R[2, 2])) / 2, R[0, 2] - R[2, 0])
    z = math.copysign(math.sqrt(max(0.0, 1.0 - R[0, 0] - R[1, 1] + R[2, 2])) / 2, R[1, 0] - R[0, 1])
    q = np.array([w, x, y, z])
    return q / np.linalg.norm(q)


def axis_angle(R):
    q = quat(R)
    ang = 2 * math.acos(min(1.0, q[0]))
    if np.linalg.norm(q[1:]) < 1e-12:
        return np.array([1.0, 0.0, 0.0]), 0.0
    ax = q[1:] / np.linalg.norm(q[1:])
    return ax, ang


class Body:
    def __init__(self):
        for o in list(bpy.data.objects):
            bpy.data.objects.remove(o, do_unlink=True)
        bpy.ops.import_scene.fbx(filepath=FBX)
        self.rig = bpy.data.objects["A7_ContinuousBody_Rig"]
        self.rig.animation_data_clear()
        self.arms = {"R": bpy.data.objects["A7_WORK_Arm_R"], "L": bpy.data.objects["A7_WORK_Arm_L"]}
        self.torso = bpy.data.objects["A7_WORK_TorsoLegs"]
        for pb in self.rig.pose.bones:
            pb.rotation_mode = "QUATERNION"
            pb.rotation_quaternion = (1, 0, 0, 0)
            pb.location = (0, 0, 0)
            pb.scale = (1, 1, 1)
        bpy.context.view_layer.update()
        self.rest = {b.name: np.array(b.matrix_local) for b in self.rig.data.bones}
        self.length = {b.name: b.length for b in self.rig.data.bones}
        self.region, self.tris = {}, {}
        for side, obj in self.arms.items():
            names = [g.name for g in obj.vertex_groups]
            self.region[side] = np.array([names[max(v.groups, key=lambda g: g.weight).group] if len(v.groups) else ""
                                          for v in obj.data.vertices])
            obj.data.calc_loop_triangles()
            t = np.zeros(len(obj.data.loop_triangles) * 3, dtype=np.int32)
            obj.data.loop_triangles.foreach_get("vertices", t)
            self.tris[side] = t.reshape(-1, 3)

    def flex_local(self, bone, normal):
        B = self.rest[bone][:3, :3]
        return B.T @ unit(np.cross(B[:, 1], normal))

    def apply(self, deltas):
        for pb in self.rig.pose.bones:
            pb.rotation_quaternion = quat(deltas[pb.name]) if pb.name in deltas else (1, 0, 0, 0)
        bpy.context.view_layer.update()

    def skin(self, side):
        dg = bpy.context.evaluated_depsgraph_get()
        ev = self.arms[side].evaluated_get(dg)
        me = ev.to_mesh()
        co = np.zeros(len(me.vertices) * 3)
        me.vertices.foreach_get("co", co)
        ev.to_mesh_clear()
        return co.reshape(-1, 3) @ np.array(self.arms[side].matrix_world)[:3, :3].T + np.array(self.arms[side].matrix_world)[:3, 3]

    def masks(self, side):
        s, reg = "." + side, self.region[side]
        m = {d: np.isin(reg, [b + s for b in bones]) for d, bones in DIGITS.items()}
        m["palm"] = np.isin(reg, ["wrist" + s] + ["metacarpal%d%s" % (i, s) for i in range(1, 5)])
        m["hand"] = m["palm"] | np.any([m[d] for d in DIGITS], axis=0)
        return m


class Rail:
    def __init__(self, centre, axis):
        self.c, self.u = np.asarray(centre, float), unit(axis)

    def gap(self, P):
        rel = P - self.c
        t = rel @ self.u
        d = np.linalg.norm(rel - t[:, None] * self.u, axis=1)
        return np.where(np.abs(t) <= RAIL_LEN / 2, d - RADIUS, np.inf)


def tris_of(tris, mask):
    return tris[np.all(mask[tris], axis=1)]


def intersections(verts, tris, ma, mb):
    ta, tb = tris_of(tris, ma), tris_of(tris, mb)
    if not len(ta) or not len(tb):
        return 0
    A = BVHTree.FromPolygons([tuple(v) for v in verts], ta.tolist())
    B = BVHTree.FromPolygons([tuple(v) for v in verts], tb.tolist())
    return len(A.overlap(B))


def solve_side(body, side):
    s, R = "." + side, body.rest
    pos = lambda b: R[b + s][:3, 3]
    d = unit(pos("finger3-1") - pos("wrist"))
    across = unit(pos("finger5-1") - pos("finger2-1"))
    u = unit(across - np.dot(across, d) * d)
    n = unit(np.cross(d, u))
    tipd = lambda b: R[b + s][:3, 3] + R[b + s][:3, 1] * body.length[b + s]
    if np.dot(tipd("finger1-3") - pos("finger2-1"), n) < 0:      # n points to the palm side (where the thumb is)
        n = -n
    masks = body.masks(side)
    body.apply({})
    skin0 = body.skin(side)
    mid = (pos("finger2-1") + pos("finger5-1")) / 2
    place = masks["hand"] & ~masks["thumb"]          # the thumb moves out of the way later; place against palm+fingers
    # 1. rail placement: for a few proximal shifts, the closest no-penetration offset; keep the one with the palm nearest
    best = None
    for shift in (0.006, 0.010, 0.014, 0.018, 0.022, 0.026):
        lo, hi = 0.0, 0.09
        for _ in range(30):
            h = (lo + hi) / 2
            pen = np.sum(Rail(mid - d * shift + n * h, u).gap(skin0[place]) < -TOL)
            lo, hi = (h, hi) if pen else (lo, h)
        rail = Rail(mid - d * shift + n * hi, u)
        g = rail.gap(skin0)
        palm = float(np.min(g[masks["palm"]]))
        if best is None or palm < best[0]:
            best = (palm, shift, hi, rail)
    palm_gap, shift, h, rail = best
    log(side, "rail: shift %.3f m proximal, %.4f m palmar, bind palm gap %.2f mm" % (shift, h, palm_gap * 1000))
    deltas = {}

    def pen_of(mask):
        body.apply(deltas)
        g = rail.gap(body.skin(side)[mask])
        return int(np.sum(g < -TOL)), float(np.min(g))

    # 2. fingers: all joints close together (MCP 1.0 : PIP 1.25 : DIP 0.9 deg per step); when a segment would enter
    #    the rail, that joint and every joint proximal to it stop at the last clear step (contact-lock closing)
    seg = lambda digit, i: body.region[side] == DIGITS[digit][i] + s
    chains = []
    for digit in ("index", "middle", "ring", "little"):
        for i, (bone, jmax, rate) in enumerate(zip(DIGITS[digit], JOINT_MAX, (1.0, 1.25, 0.9))):
            mask = seg(digit, i)
            if i == 0:   # the MCP also drags the palm skin over its metacarpal head: that skin must stay clear too
                k = ("index", "middle", "ring", "little").index(digit) + 1
                mask = mask | (body.region[side] == "metacarpal%d%s" % (k, s))
            chains.append({"digit": digit, "i": i, "bone": bone + s, "ax": body.flex_local(bone + s, n),
                           "max": jmax, "rate": math.radians(rate), "a": 0.0, "lock": False, "mask": mask})
    close(body, side, rail, deltas, chains, lambda sk, c: bool(np.sum(rail.gap(sk[c["mask"]]) < -TOL)))
    # seat the rail in the palm: after a closing, slide it proximally (towards the wrist) and towards the palm as far
    # as the skin allows, keeping the offset with the smallest palm gap; then the fingers continue closing; 3 rounds
    hand_nt = masks["hand"] & ~masks["thumb"]
    for _ in range(3):
        body.apply(deltas)
        sk = body.skin(side)
        best = None
        for dd in np.arange(0.0, 0.0161, 0.002):
            lo, hi = 0.0, 0.03
            if np.sum(Rail(rail.c - d * dd, u).gap(sk[hand_nt]) < -TOL):
                continue
            for _ in range(24):
                dn = (lo + hi) / 2
                ok = not np.sum(Rail(rail.c - d * dd - n * dn, u).gap(sk[hand_nt]) < -TOL)
                lo, hi = (dn, hi) if ok else (lo, dn)
            cand = Rail(rail.c - d * dd - n * lo, u)
            pg = float(np.min(cand.gap(sk[masks["palm"]])))
            if best is None or pg < best[0]:
                best = (pg, dd, lo, cand)
        if best is None:
            log(side, "rail seating: no clear offset, rail kept")
            break
        pg, dd, dn, rail = best
        for c in chains:
            c["lock"] = c["a"] >= c["max"]
        close(body, side, rail, deltas, chains, lambda sk, c: bool(np.sum(rail.gap(sk[c["mask"]]) < -TOL)))
        log(side, "rail seated %.1f mm proximal, %.1f mm palmward (palm gap before re-closing %.2f mm)" % (
            dd * 1000, dn * 1000, pg * 1000))
    for digit in ("index", "middle", "ring", "little"):
        log(side, digit, "angles", [round(math.degrees(c["a"]), 1) for c in chains if c["digit"] == digit],
            "gap %.2f mm" % (pen_of(masks[digit])[1] * 1000))
    # 3. thumb: CMC opposition from a grid (clear of rail, fingers and palm; distal pad nearest the rail), then MP
    #    and IP close with the same contact lock, where touching a finger counts as contact too
    t1, t2, t3 = (b + s for b in DIGITS["thumb"])
    a_pal, a_uln = body.flex_local(t1, n), body.flex_local(t1, u)
    others = masks["index"] | masks["middle"] | masks["ring"] | masks["little"] | masks["palm"]
    pad = seg("thumb", 2)
    cands = []
    for al in np.radians(np.arange(-20, 81, 10)):
        for be in np.radians(np.arange(-40, 81, 10)):
            deltas[t1] = rot(a_pal, al) @ rot(a_uln, be)
            deltas.pop(t2, None)
            deltas.pop(t3, None)
            body.apply(deltas)
            sk = body.skin(side)
            g = rail.gap(sk[masks["thumb"] | masks["palm"]])
            if np.sum(g < -TOL) or intersections(sk, body.tris[side], masks["thumb"], others):
                continue
            cands.append((float(np.min(rail.gap(sk[pad]))), al, be))
    cands.sort()
    if not cands:
        raise RuntimeError("no feasible thumb opposition")
    gmin, al, be = cands[0]
    deltas[t1] = rot(a_pal, al) @ rot(a_uln, be)
    log(side, "thumb CMC palmar %.0f deg, ulnar %.0f deg, pad gap %.2f mm (%d feasible)" % (
        math.degrees(al), math.degrees(be), gmin * 1000, len(cands)))
    tchain = [{"digit": "thumb", "i": i, "bone": bone, "ax": body.flex_local(bone, n), "max": mx,
               "rate": math.radians(rt), "a": 0.0, "lock": False, "mask": seg("thumb", i)}
              for i, (bone, mx, rt) in enumerate(((t2, math.radians(60), 1.0), (t3, math.radians(80), 1.2)), start=1)]

    def thumb_contact(sk, c):
        return bool(np.sum(rail.gap(sk[c["mask"]]) < -TOL)) or intersections(sk, body.tris[side], c["mask"], others) > 0
    close(body, side, rail, deltas, tchain, thumb_contact)
    # final seating with the thumb in place: the thumb's opposition pulls the thenar skin back, so slide the rail into
    # the palm again (whole hand must stay clear), then let fingers and thumb close onto it
    for _ in range(3):
        body.apply(deltas)
        now = float(np.min(rail.gap(body.skin(side)[masks["palm"]])))
        for c in chains + tchain:          # open the digits part way so their bases do not hold the rail off the palm
            c["a"] *= 0.55
            deltas[c["bone"]] = rot(c["ax"], c["a"])
        body.apply(deltas)
        sk = body.skin(side)
        best = None
        for dd in np.arange(0.0, 0.0161, 0.002):
            if np.sum(Rail(rail.c - d * dd, u).gap(sk[masks["hand"]]) < -TOL):
                continue
            lo, hi = 0.0, 0.03
            for _ in range(24):
                dn = (lo + hi) / 2
                ok = not np.sum(Rail(rail.c - d * dd - n * dn, u).gap(sk[masks["hand"]]) < -TOL)
                lo, hi = (dn, hi) if ok else (lo, dn)
            cand = Rail(rail.c - d * dd - n * lo, u)
            pg = float(np.min(cand.gap(sk[masks["palm"]])))
            if best is None or pg < best[0]:
                best = (pg, dd, lo, cand)
        if best is None or best[0] >= now - 1e-5:
            for c in chains + tchain:
                c["lock"] = c["a"] >= c["max"]
            close(body, side, rail, deltas, chains, lambda sk, c: bool(np.sum(rail.gap(sk[c["mask"]]) < -TOL)))
            close(body, side, rail, deltas, tchain, thumb_contact)
            break
        rail = best[3]
        for c in chains + tchain:
            c["lock"] = c["a"] >= c["max"]
        close(body, side, rail, deltas, chains, lambda sk, c: bool(np.sum(rail.gap(sk[c["mask"]]) < -TOL)))
        close(body, side, rail, deltas, tchain, thumb_contact)
        log(side, "final seating %.1f mm proximal, %.1f mm palmward: palm gap %.2f -> %.2f mm" % (
            best[1] * 1000, best[2] * 1000, now * 1000, best[0] * 1000))
    log(side, "thumb gap %.2f mm" % (pen_of(masks["thumb"])[1] * 1000))
    return {"rail": rail, "deltas": deltas, "n": n, "d": d, "u": u, "shift": shift, "h": h,  # rail = final
            "axes": {c["bone"]: c["ax"] for c in chains + tchain}, "angles": {c["bone"]: c["a"] for c in chains + tchain}}


def close(body, side, rail, deltas, chains, touching, step=1.0):
    """Contact-lock closing: every unlocked joint advances by rate*step degrees; a segment that would touch locks its
    own joint and all proximal joints of the same digit at the previous angle."""
    for _ in range(400):
        live = [c for c in chains if not c["lock"]]
        if not live:
            break
        prev = {id(c): c["a"] for c in chains}
        for c in live:
            c["a"] = min(c["max"], c["a"] + c["rate"] * step)
        for c in chains:
            deltas[c["bone"]] = rot(c["ax"], c["a"])
        body.apply(deltas)
        sk = body.skin(side)
        for c in chains:
            if c["lock"] or not touching(sk, c):
                continue
            for d in chains:
                if d["digit"] == c["digit"] and d["i"] <= c["i"]:
                    d["a"], d["lock"] = prev[id(d)], True
        for c in live:
            if c["a"] >= c["max"]:
                c["lock"] = True
    for c in chains:
        deltas[c["bone"]] = rot(c["ax"], c["a"])
    body.apply(deltas)


TIMING = {"1": (0.0, 0.6), "2": (0.25, 0.6), "3": (0.45, 0.55)}


def smooth(x):
    x = min(1.0, max(0.0, x))
    return x * x * (3 - 2 * x)


OPEN = math.radians(5.0)      # a joint whose solved angle is ~0 opens 5 degrees at the open frame (keeps its curve)
CMC_OPEN = 1.15               # thumb CMC at the open frame: 15 % further out than the solved opposition (spread)


def at(res, f):
    """Deltas at take fraction f. Fingers and thumb MP/IP close from the bind (or 5 deg open, for a joint whose solved
    angle is ~0) to the solved angle about their own flex axis (MCP leads, PIP/MP follow, DIP/IP last); the thumb
    CMC closes from 115 % to 100 % of its rotation (spread to grip) in the first 60 %. Every joint moves, so every finger/thumb bone
    keeps its curve in the FBX."""
    out = {}
    for b, Rf in res["deltas"].items():
        if b.startswith("finger1-1"):
            ax, ang = axis_angle(Rf)
            out[b] = rot(ax, ang * (CMC_OPEN + (1.0 - CMC_OPEN) * smooth(f / 0.6)))
            continue
        start, span = TIMING[b.split(".")[0][-1]]
        a = res["angles"][b]
        o = OPEN if abs(a) < math.radians(1.0) else 0.0      # a joint that stays at bind still gets a 5 deg motion
        out[b] = rot(res["axes"][b], -o + (a + o) * smooth((f - start) / span))
    return out


def measure(body, side, res):
    masks = body.masks(side)
    body.apply(res["deltas"])
    sk = body.skin(side)
    g = res["rail"].gap(sk)
    row = {}
    for r in ("palm", "thumb", "index", "middle", "ring", "little"):
        row[r] = {"min_gap_mm": round(float(np.min(g[masks[r]])) * 1000, 3),
                  "penetrating_vertices": int(np.sum(g[masks[r]] < 0.0)),
                  "within_2mm_vertices": int(np.sum((g[masks[r]] >= 0) & (g[masks[r]] <= 0.002)))}
    row["whole_arm_penetrating_vertices"] = int(np.sum(g < 0.0))
    pairs = {}
    names = ["thumb", "index", "middle", "ring", "little", "palm"]
    for i, a in enumerate(names):
        for b in names[i + 1:]:
            k = intersections(sk, body.tris[side], masks[a], masks[b])
            if k:
                pairs[a + "/" + b] = k
    row["digit_digit_triangle_intersections"] = pairs
    path = []
    for f in range(LAST):
        body.apply(at(res, f / (LAST - 1)))
        skp = body.skin(side)
        gp = res["rail"].gap(skp)
        others = masks["index"] | masks["middle"] | masks["ring"] | masks["little"] | masks["palm"]
        path.append([int(np.sum(gp < 0.0)), intersections(skp, body.tris[side], masks["thumb"], others)])
    row["path_per_frame_rail_penetrating_vertices_and_thumb_intersections"] = path
    body.apply(res["deltas"])
    return row, sk


def key_and_export(body, side, res, path, take):
    rig = body.rig
    act = bpy.data.actions.new(take)
    rig.animation_data_create()
    rig.animation_data.action = act
    bones = [b for b in res["deltas"]]
    for f in range(LAST):
        D = at(res, f / (LAST - 1))
        for b in bones:
            pb = rig.pose.bones[b]
            assert np.all(np.isfinite(D[b])), (b, f)
            pb.rotation_quaternion = quat(D[b])
            pb.keyframe_insert("rotation_quaternion", frame=f)
    for b in bones:
        pb = rig.pose.bones[b]
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.keyframe_insert("rotation_quaternion", frame=REST_FRAME)
    scene = bpy.context.scene
    scene.frame_start, scene.frame_end = 0, LAST - 1
    scene.render.fps = FPS
    scene.name = take
    for o in bpy.data.objects:
        o.select_set(o == rig)
    bpy.context.view_layer.objects.active = rig
    scene.frame_set(REST_FRAME)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"ARMATURE"}, add_leaf_bones=False,
                             bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
                             bake_anim_use_all_bones=False, bake_anim_force_startend_keying=True,
                             bake_anim_simplify_factor=0.01, axis_forward="-Z", axis_up="Y",
                             primary_bone_axis="Y", secondary_bone_axis="X", global_scale=1.0,
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", use_armature_deform_only=False)
    rig.animation_data.action = None
    body.apply(res["deltas"])


def anchors(body, side, res):
    s = "." + side
    W = body.rest["wrist" + s]
    Wi = np.linalg.inv(W)
    c, u = res["rail"].c, res["rail"].u
    to_local = lambda p: (Wi @ np.append(p, 1.0))[:3]
    joint = {b: {"unity_model_delta_xyzw": None, "blender_local_axis": None} for b in res["deltas"]}
    for b, Rf in res["deltas"].items():
        ax, ang = axis_angle(Rf)
        B = body.rest[b][:3, :3]
        Rm = B @ Rf @ B.T                                  # the same delta expressed in armature (model) space
        Ru = M_U @ Rm @ M_U.T
        qu = quat(Ru)
        joint[b] = {"blender_local_axis": [round(float(x), 6) for x in ax], "angle_deg": round(math.degrees(ang), 3),
                    "unity_model_delta_xyzw": [round(float(x), 6) for x in (qu[1], qu[2], qu[3], qu[0])]}
    body.apply(res["deltas"])
    heads = {pb.name: [round(float(x), 6) for x in M_U @ np.array(pb.head)] for pb in body.rig.pose.bones
             if pb.name.startswith(("finger", "wrist")) and pb.name.endswith(s)}
    tails = {pb.name: [round(float(x), 6) for x in M_U @ np.array(pb.tail)] for pb in body.rig.pose.bones
             if pb.name.startswith("finger") and pb.name.endswith("-3" + s)}
    return {
        "closed_pose_bone_heads_unity_model_m": heads, "closed_pose_fingertip_bone_tails_unity_model_m": tails,
        "rail": {"radius_m": RADIUS, "length_checked_m": RAIL_LEN,
                 "axis_point_unity_model_m": [round(float(x), 6) for x in M_U @ c],
                 "axis_dir_unity_model": [round(float(x), 6) for x in M_U @ u],
                 "axis_point_in_wrist_bone_bind_frame_m": [round(float(x), 6) for x in to_local(c)],
                 "axis_dir_in_wrist_bone_bind_frame": [round(float(x), 6) for x in Wi[:3, :3] @ u]},
        "palm_normal_unity_model": [round(float(x), 6) for x in M_U @ res["n"]],
        "finger_direction_unity_model": [round(float(x), 6) for x in M_U @ res["d"]],
        "joints": joint}


def still(body, side, res, path, view):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "OBJECT"
    scene.display.shading.show_cavity = True
    scene.render.resolution_x, scene.render.resolution_y = 700, 560
    body.torso.hide_render = True
    for sd, ob in body.arms.items():
        ob.hide_render = sd != side
        ob.color = (0.86, 0.72, 0.62, 1.0)
    name = "RAIL_" + side
    if name not in bpy.data.objects:
        bpy.ops.mesh.primitive_cylinder_add(vertices=48, radius=RADIUS, depth=0.16)
        rail_obj = bpy.context.active_object
        rail_obj.name = name
        rail_obj.color = (0.35, 0.45, 0.6, 1.0)
        z = np.array([0.0, 0.0, 1.0])
        u = res["rail"].u
        ax = np.cross(z, u)
        ang = math.acos(max(-1.0, min(1.0, float(z @ u))))
        R = rot(ax, ang) if np.linalg.norm(ax) > 1e-9 else np.eye(3)
        M = np.eye(4)
        M[:3, :3], M[:3, 3] = R, res["rail"].c
        from mathutils import Matrix
        rail_obj.matrix_world = Matrix(M.tolist())
    for nm in ("RAIL_R", "RAIL_L"):
        if nm in bpy.data.objects:
            bpy.data.objects[nm].hide_render = nm != name
    c, n, d, u = res["rail"].c, res["n"], res["d"], res["u"]
    eye = {"palm": c + 0.22 * unit(n + 0.25 * d), "side": c - 0.22 * unit(u - 0.2 * n),
           "back": c + 0.22 * unit(-n + 0.35 * d)}[view]
    cam = bpy.data.objects.get("CAM") or bpy.data.objects.new("CAM", bpy.data.cameras.new("CAM"))
    if cam.name not in scene.collection.objects:
        scene.collection.objects.link(cam)
    from mathutils import Vector
    cam.location = Vector(eye.tolist())
    cam.rotation_euler = (Vector(c.tolist()) - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam.data.lens = 60
    scene.camera = cam
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def main():
    os.makedirs(OUT, exist_ok=True)
    body = Body()
    report, anc = {"rail_radius_m": RADIUS, "penetration_tolerance_m": TOL, "frames": LAST, "fps": FPS}, {}
    results = {}
    for side in ("R", "L"):
        res = solve_side(body, side)
        results[side] = res
        row, _ = measure(body, side, res)
        report[side] = row
        anc[side] = anchors(body, side, res)
        log(side, json.dumps(row))
    for side in ("R", "L"):
        body.apply(results[side]["deltas"])
        key_and_export(body, side, results[side], os.path.join(OUT, "BODY_WORK_GripRail_%s_V01.fbx" % side),
                       "WORK_GripRail_" + side)
    json.dump(report, open(os.path.join(OUT, "grip_report.json"), "w", encoding="utf-8", newline="\n"), indent=1)
    json.dump(anc, open(os.path.join(OUT, "grip_anchors.json"), "w", encoding="utf-8", newline="\n"), indent=1)
    if RENDER:
        os.makedirs(os.path.join(OUT, "renders"), exist_ok=True)
        for side in ("R", "L"):
            body.apply(results[side]["deltas"])
            for view in ("palm", "side", "back"):
                still(body, side, results[side], os.path.join(OUT, "renders", "grip_%s_%s.png" % (side, view)), view)
    open(os.path.join(OUT, "grip_solve.log"), "w", encoding="utf-8", newline="\n").write("\n".join(LOG) + "\n")


main()
