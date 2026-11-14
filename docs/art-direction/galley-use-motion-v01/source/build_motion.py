"""build_motion - GALLEY-USE-MOTION-01 in Blender 5.3: staged meal and drink clips, sampled JSON, fit check, frames.

Builds the habitat galley unit (hb_kit), the two consumables (gc_kit) and the original gloves (AstronautHands.fbx,
f72224c, imported read-only and posed from AstronautHands_rig.json), computes the clips with gu_motion and keys
every object per frame. Timeline at 30 fps: meal frames 1-103 (3.4 s), drink frames 121-244 (4.1 s).

No approved glove pose can carry a pouch, so the props follow the path a carrying hand would take and every frame
records the clasp target (the missing anchor). The left glove's Press pose works the drink's pinch clamp: its
placement is settled on the clamp by a world-space BVH search (no intersection, contact within one 0.25 mm step).
The rejected alternative, the Grip fist round the drink adapter, is measured and rendered as a study.

Writes gu_motion_clips.json, gu_fit.json, GU_GalleyUse.blend (its texture paths point at the released kits in the
repository, see the README) and Blender frames in renders/.

Run: blender -b --factory-startup --python build_motion.py -- --out <dir> --gc <galley-consumables build dir>
     --habitat <habitat build dir> --hands <AstronautHands folder> [--render --samples 64]
"""
import json
import math
import os
import shutil
import sys

import bpy
import numpy as np
from mathutils import Matrix
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
sys.dont_write_bytecode = True
sys.path.insert(0, HERE)
import a7kit  # noqa: E402
import build_habitat as bh  # noqa: E402
import gc_kit  # noqa: E402
import gu_motion as gm  # noqa: E402
import hb_kit  # noqa: E402

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def arg(name, default=None):
    if name in ARGS:
        i = ARGS.index(name)
        return ARGS[i + 1] if i + 1 < len(ARGS) and not ARGS[i + 1].startswith("--") else True
    return default


OUT = os.path.abspath(arg("--out"))
GC = os.path.abspath(arg("--gc"))
HAB = os.path.abspath(arg("--habitat"))
HANDS = os.path.abspath(arg("--hands"))
RENDER = bool(arg("--render", False))
SAMPLES = int(arg("--samples", 64))
TEX = os.path.join(OUT, "textures")
MEAL_START, DRINK_START = 1, 121
WORK_VALUE = 0.43     # MAT_Cargo_WorkGloves (_BaseColor 0.42, 0.43, 0.42): the separate work-glove instance
REPO_TEXTURES = (("GC_", "//../galley-consumables-v01/textures/"),
                 ("AstronautHands", "//../../../Assets/_OutOfReach/Visuals/Models/Aurora7/Props/AstronautHands/Textures/"),
                 ("", "//../habitat-v01/textures/"))

T = Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))       # Unity <-> Blender (y, z swap)
RZ180 = Matrix.Rotation(math.pi, 4, "Z")                                   # glove FBX import axes -> ours


def mat(m):
    return Matrix([list(map(float, row)) for row in m])


def ours(m_unity):
    """World matrix for an object built by bh.make_object at its own origin, from a Unity 4x4."""
    return T @ mat(m_unity) @ T


def glove(m_unity):
    """World matrix for a glove part imported from the FBX, from its joint's Unity world 4x4."""
    return T @ mat(m_unity) @ T @ RZ180


def key(obj, frame, matrix):
    obj.matrix_world = matrix
    obj.keyframe_insert("location", frame=frame)
    obj.keyframe_insert("rotation_quaternion", frame=frame)


def visible(obj, frame, shown):
    obj.hide_render = not shown
    obj.hide_viewport = not shown
    obj.keyframe_insert("hide_render", frame=frame)
    obj.keyframe_insert("hide_viewport", frame=frame)


def copy_textures():
    os.makedirs(TEX, exist_ok=True)
    for folder in (os.path.join(HAB, "textures"), os.path.join(GC, "textures")):
        for f in os.listdir(folder):
            if f.endswith((".png", ".json")) and not os.path.exists(os.path.join(TEX, f)):
                shutil.copyfile(os.path.join(folder, f), os.path.join(TEX, f))
    shutil.copyfile(os.path.join(HANDS, "Textures", "AstronautHands_BaseColor.png"),
                    os.path.join(TEX, "AstronautHands_BaseColor.png"))


def glove_material():
    """The gloves' base colour darkened like the separate work-glove instance (unsuited use)."""
    m = bpy.data.materials.new("AH_WorkGloves")
    m.use_nodes = True
    nodes, links = m.node_tree.nodes, m.node_tree.links
    bsdf = next(n for n in nodes if n.type == "BSDF_PRINCIPLED")
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(os.path.join(TEX, "AstronautHands_BaseColor.png"), check_existing=True)
    hsv = nodes.new("ShaderNodeHueSaturation")
    hsv.inputs["Value"].default_value = WORK_VALUE
    links.new(tex.outputs["Color"], hsv.inputs["Color"])
    links.new(hsv.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.62
    return m


def build():
    bh.clear_scene()
    copy_textures()
    scene = bpy.context.scene
    scene.render.fps = gm.FPS
    with open(os.path.join(TEX, "hb_labels.json"), encoding="utf-8") as h:
        hb_atlas = json.load(h)
    with open(os.path.join(TEX, "gc_labels.json"), encoding="utf-8") as h:
        gc_atlas = json.load(h)
    specs = {}
    for path in (os.path.join(HERE, "hb_materials.json"), os.path.join(HERE, "gc_materials.json")):
        specs.update({e["name"]: e for e in json.load(open(path, encoding="utf-8"))["materials"]})
    materials = {n: bh.build_material(n, s, TEX) for n, s in specs.items()}
    coll = scene.collection
    # galley unit (habitat) at the origin, a plain floor and wall behind it
    for name, (model, origin, motion) in hb_kit.build_kit(hb_atlas)["HB_Galley"]["objects"].items():
        bh.make_object(name, model, materials, coll, origin)
    room = a7kit.Model("GU_Room")
    room.box("A7_PaintGray", (0, 1.3, -0.02), (3.4, 2.6, 0.02), 0.0)
    room.box("A7_PaintGray", (0, -0.01, 1.2), (3.4, 0.02, 2.4), 0.0)
    bh.make_object("GU_Room", room, materials, coll)
    # consumables: both variants of each, built at their own origin and keyed per frame
    kit = gc_kit.build_kit(gc_atlas)
    props = {}
    for prop in kit.values():
        for name, model in prop["objects"].items():
            obj, _ = bh.make_object(name, model, materials, coll)
            obj.rotation_mode = "QUATERNION"
            props[name] = obj
    # gloves: imported read-only, flat parts with pivots on their joints
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(HANDS, "AstronautHands.fbx"), bake_space_transform=True)
    gloves = {o.name.split(".")[0]: o for o in set(bpy.data.objects) - before}
    gmat = glove_material()
    for o in gloves.values():
        o.rotation_mode = "QUATERNION"
        o.data.materials.clear()
        o.data.materials.append(gmat)
    # markers for the witness view (eye and assumed mouth)
    markers = {}
    for name, p, colour in (("GU_EyeMarker", gm.EYE, "#2B2B2C"), ("GU_MouthMarker", gm.MOUTH, "#D08A1C")):
        mk = a7kit.Model(name)
        mk.sphere(name, (0, 0, 0), 0.012, 16, 8)
        materials[name] = bh.build_material(name, {"tint": colour, "smoothness": 0.4}, TEX)
        obj, _ = bh.make_object(name, mk, materials, coll)
        obj.matrix_world = ours(gm.pose(p, np.eye(3)))
        markers[name] = obj
    return kit, props, gloves, markers


# ----------------------------------------------------------------------------- fit measurements (world space)
def tris_world(obj, matrix):
    me = obj.data
    me.calc_loop_triangles()
    return [matrix @ v.co for v in me.vertices], [tuple(t.vertices) for t in me.loop_triangles], me


def material_trees(obj, matrix):
    verts, _, me = tris_world(obj, matrix)
    names = [s.material.name if s.material else "" for s in obj.material_slots]
    groups = {}
    for t in me.loop_triangles:
        groups.setdefault(names[t.material_index], []).append(tuple(t.vertices))
    return {k: BVHTree.FromPolygons(verts, v) for k, v in groups.items()}


def overlaps(gloves, parts, prop_obj, prop_m):
    """Intersecting triangle pairs per glove part and prop material."""
    trees = material_trees(prop_obj, ours(prop_m))
    rows = {}
    for name, m in parts.items():
        v, t, _ = tris_world(gloves[name], glove(m))
        gt = BVHTree.FromPolygons(v, t)
        hits = {k: len(gt.overlap(pt)) for k, pt in sorted(trees.items())}
        hits = {k: n for k, n in hits.items() if n}
        if hits:
            rows[name] = hits
    return rows


def clearance(gloves, parts, prop_obj, prop_m, names):
    verts, tris, _ = tris_world(prop_obj, ours(prop_m))
    tree = BVHTree.FromPolygons(verts, tris)
    return min(tree.find_nearest(p)[3] for n in names for p in tris_world(gloves[n], glove(parts[n]))[0])


def settle(rig, gloves, prop_obj, prop_m, arm_at, step=0.00025, low=-0.008, high=0.012):
    """Lowest lift along the prop's +y above every intersecting one: the index tip then rests on the clamp."""
    verts, tris, _ = tris_world(prop_obj, ours(prop_m))
    tree = BVHTree.FromPolygons(verts, tris)
    last_hit, k = None, 0
    while low + k * step <= high + 1e-9:
        lift = low + k * step
        parts = rig.world("L", arm_at(lift), "Press")
        if any(BVHTree.FromPolygons(*tris_world(gloves[n], glove(m))[:2]).overlap(tree) for n, m in parts.items()):
            last_hit = lift
        k += 1
    if last_hit is None or last_hit + step > high:
        raise RuntimeError("press: no clear contact between %.1f and %.1f mm" % (low * 1000, high * 1000))
    return last_hit + step


def fist_span(rig, gloves):
    """Grip fist measured in R_Hand's frame: extent of the four fingers along the fist axis (x)."""
    parts = rig.world("R", np.eye(4), "Grip")
    inv_hand = np.array(mat(gm.inv(parts["R_Hand"])))
    xs = []
    for name, m in parts.items():
        if any(f in name for f in ("Index", "Middle", "Ring", "Little")):
            to_unity = np.array(T @ glove(m))
            xs += [(inv_hand @ to_unity @ np.array([*v.co, 1.0]))[0] for v in gloves[name].data.vertices]
    return round((max(xs) - min(xs)) * 1000, 1)


# ----------------------------------------------------------------------------- clips
def run():
    kit, props, gloves, markers = build()
    rig = gm.Rig(os.path.join(HANDS, "AstronautHands_rig.json"))
    meal, drink = kit["GC_MealPouch"], kit["GC_DrinkPouch"]
    meal_mouth = np.array(meal["anchors"]["MealMouth"][0])
    meal_grip = np.array(meal["anchors"]["MealGrip"][0])
    drink_grip = np.array(drink["anchors"]["DrinkGrip"][0])
    straw_tip = np.array(drink["anchors"]["DrinkStrawTip"][0])
    lo, hi = meal["objects"]["GC_MealPouch_Opened"].bounds()
    meal_keys, meal_len = gm.meal_clip(meal_mouth, meal_grip, hi[2] - lo[2])
    drink_keys, drink_len = gm.drink_clip(straw_tip)
    meal_rest, drink_rest = meal_keys[0][1], drink_keys[0][1]
    neutral = {s: gm.neutral_arm(rig, s) for s in ("R", "L")}
    cam_inv = gm.inv(gm.CAM)

    def show_side(frame, side, shown):
        for name, o in gloves.items():
            if side is None or name.startswith(side + "_"):
                visible(o, frame, shown)

    def clasp(m, anchor):
        target = m @ gm.pose(anchor, np.eye(3))
        return {"clasp_target_galley": gm.to_record(target), "clasp_target_camera": gm.to_record(cam_inv @ target)}

    def key_list(keys, names, spaces):
        """The authored keys; 'space' says where a key is defined (table keys in galley space, face keys relative
        to the eye), which is how to re-target the clip to another stance."""
        return [{"t": t, "name": n, "space": s, "galley": gm.to_record(m), "camera": gm.to_record(cam_inv @ m)}
                for (t, m), n, s in zip(keys, names, spaces)]

    # ---- fit: the rejected fist round the drink adapter, and the left index settled on the clamp (pouch at hold)
    hold = gm.keyed(drink_keys, gm.PRESS_T[1])
    rejected_arm = hold @ gm.inv(gm.adapter_fist_grip()) @ gm.inv(rig.hand_from_arm("R", "Grip"))
    fit = {"grip_fist_tube_mm": 34, "grip_fist_span_mm": fist_span(rig, gloves),
           "rejected_adapter_fist": {
               "what": "R Grip fist round the drink adapter (fist axis across the pouch), pouch at the hold pose",
               "intersecting_pairs": overlaps(gloves, rig.world("R", rejected_arm, "Grip"),
                                              props["GC_DrinkPouch_Closed"], hold)}}
    press_at, press = {}, {}
    for variant, drop in (("GC_DrinkPouch_Closed", 0.0), ("GC_DrinkPouch_Open", gm.CLAMP_DROP)):
        def arm_at(lift, drop=drop):
            p = gm.CLAMP_CLOSED + np.array([0.0, lift - drop, 0.0])
            contact = (hold @ np.append(p, 1.0))[:3] + gm.cam_dir(gm.PRESS_OFFSET_CAM)
            hand = gm.pose(contact - gm.PRESS_R @ gm.PRESS_TIP_L, gm.PRESS_R)
            return hand @ gm.inv(rig.hand_from_arm("L", "Press"))
        lift = settle(rig, gloves, props[variant], hold, arm_at)
        press_at[variant], press[variant] = (lambda dl, f=arm_at, b=lift: f(b + dl)), arm_at(lift)
        parts = rig.world("L", press[variant], "Press")
        others = [n for n in parts if n != "L_Index3"]
        fit["press_" + variant] = {
            "lift_mm": round(lift * 1000, 2),
            "index_tip_clearance_mm": round(clearance(gloves, parts, props[variant], hold, ["L_Index3"]) * 1000, 2),
            "other_left_parts_clearance_mm": round(clearance(gloves, parts, props[variant], hold, others) * 1000, 1),
            "intersecting_pairs": overlaps(gloves, parts, props[variant], hold)}
    bh.log("fit", json.dumps({k: v for k, v in fit.items() if k != "rejected_adapter_fist"}))

    out = {"fps": gm.FPS,
           "space": "galley unit space (HB_Galley): Unity metres, y up, unit front +z; camera = the eye (x right, y up, z forward)",
           "galley_mount_ship": {"position": [-2.38, 0.0, 14.75], "yaw_deg": 90.0,
                                 "rule": "ship = (gz - 2.38, gy, 14.75 - gx); rotation (0, 0.70711, 0, 0.70711) * galley",
                                 "source": "habitat-v01/integration/hb_integration_manifest.json (HB_Galley)"},
           "eye": gm.to_record(gm.CAM), "mouth_camera": gm.MOUTH_CAM.tolist(),
           "mouth_galley": [round(float(c), 5) for c in gm.MOUTH],
           "suit_state": "unsuited only: helmet off, bare face. Never play these clips with the suit or helmet on. "
                         "AstronautHandsPresenter shows the suit gloves only while the suit is worn. The left hand in "
                         "the frames is the approved glove rig, darkened like the separate work-glove instance "
                         "(MAT_Cargo_WorkGloves) to mark unsuited use; the unsuited WORK body (A7_BodyFit02_WORK, bare "
                         "hands) can be driven to the same targets (index_tip_*, clasp_target_*).",
           "timing": "presentation only: one staged bite (meal, 3.4 s) or sip (drink, 4.1 s). Not the simulated "
                     "meal duration, amount, water volume or award moment; Core owns those.",
           "carry": {"status": "missing pose: no approved glove pose carries a pouch; the props follow the path a "
                               "carrying hand would take",
                     "why": "Grip closes round a 34 mm tube. The meal body is 125 x 150 x 28 mm and the drink body "
                            "95 x 150 x 24 mm. The drink adapter (42 x 26 x 17 mm) sits between the straw (up) and the "
                            "body (down): a fist across it is cut by both (fit.rejected_adapter_fist), and a fist along "
                            "the straw spans %.1f mm while the free neck between the top seal and the clamp is 24 mm "
                            "(open) to 44 mm (closed)." % fit["grip_fist_span_mm"],
                     "missing_anchor": {
                         "pose": "Clasp for R and L: thumb pad opposed to the index and middle finger pads, aperture "
                                 "24 mm (drink) to 28 mm (meal)",
                         "point": "R_ClaspPoint / L_ClaspPoint: the middle of that aperture, given in the Hand frame "
                                  "like the rig's Grip and Press reference points",
                         "target": "clasp_target_* in every frame: the pouch's grip anchor (MealGrip / DrinkGrip, "
                                   "(0, -0.020, 0) in prop space) with the prop's axes: +z label side (thumb), "
                                   "-z back (fingers), +y up the pouch",
                         "then": "hand_world = clasp_target @ inverse(ClaspPoint frame in Hand); "
                                 "R_Arm (camera space) = inverse(eye) @ hand_world @ inverse(Clasp wrist rotation)"}},
           "fit": fit, "clips": {}}

    # ---- meal: the prop moves along the carry path; the clasp target is recorded
    frames = []
    for i, (t, m) in enumerate(gm.sample(meal_keys, meal_len)):
        f = MEAL_START + i
        key(props["GC_MealPouch_Opened"], f, ours(m))
        key(props["GC_MealPouch_Sealed"], f, ours(meal_rest))
        key(props["GC_DrinkPouch_Closed"], f, ours(drink_rest))
        key(props["GC_DrinkPouch_Open"], f, ours(drink_rest))
        frames.append({"t": round(t, 4), "prop_galley": gm.to_record(m), "prop_camera": gm.to_record(cam_inv @ m),
                       **clasp(m, meal_grip)})
    visible(props["GC_MealPouch_Opened"], MEAL_START, True)
    visible(props["GC_MealPouch_Sealed"], MEAL_START, False)
    visible(props["GC_DrinkPouch_Closed"], MEAL_START, True)
    visible(props["GC_DrinkPouch_Open"], MEAL_START, False)
    show_side(MEAL_START, None, False)
    out["clips"]["meal"] = {"frames_blender": [MEAL_START, MEAL_START + len(frames) - 1], "duration_s": meal_len,
                            "variant": "GC_MealPouch_Opened", "anchor": "MealGrip",
                            "keys": key_list(meal_keys, ["rest", "rest", "lifted", "near_face", "at_lips", "at_lips",
                                                         "near_face", "rest"],
                                             ["galley", "galley", "galley", "camera", "camera", "camera", "camera",
                                              "galley"]),
                            "frames": frames}

    # ---- drink: carry path + clasp target; the left index (Press) slides the clamp down at the variant swap
    frames = []
    show_side(DRINK_START, "R", False)
    show_side(DRINK_START, "L", True)
    visible(props["GC_DrinkPouch_Closed"], DRINK_START, True)
    visible(props["GC_DrinkPouch_Open"], DRINK_START, False)
    a0, a1, a2, a3 = gm.PRESS_T
    closed, opened = press["GC_DrinkPouch_Closed"], press["GC_DrinkPouch_Open"]
    above_closed = press_at["GC_DrinkPouch_Closed"](0.015)
    above_open = press_at["GC_DrinkPouch_Open"](0.015)
    n = int(round(drink_len * gm.FPS)) + 1
    worst = 0
    for i in range(n):
        t = i / gm.FPS
        f = DRINK_START + i
        m = gm.keyed(drink_keys, t)
        is_open = t >= gm.DRINK_CLAMP_OPEN_T - 1e-9
        variant = "GC_DrinkPouch_Open" if is_open else "GC_DrinkPouch_Closed"
        for name in ("GC_DrinkPouch_Closed", "GC_DrinkPouch_Open"):
            key(props[name], f, ours(m))
        key(props["GC_MealPouch_Opened"], f, ours(meal_rest))
        key(props["GC_MealPouch_Sealed"], f, ours(meal_rest))
        if abs(t - gm.DRINK_CLAMP_OPEN_T) < 0.5 / gm.FPS:
            visible(props["GC_DrinkPouch_Closed"], f, False)
            visible(props["GC_DrinkPouch_Open"], f, True)
        if t < a0 or t > a3:
            l_arm, w = neutral["L"], 0.0
        elif t < a1:      # from neutral, over the clamp, down onto it
            u = (t - a0) / (a1 - a0)
            l_arm, w = gm.keyed([(0.0, neutral["L"]), (0.7, above_closed), (1.0, closed)], u), gm.smooth(min(1.0, u / 0.7))
        elif t < a2:      # on the clamp; at the swap the tip goes down with it
            l_arm, w = (opened if is_open else closed), 1.0
        else:             # up along the straw, then back to neutral
            u = (t - a2) / (a3 - a2)
            l_arm, w = gm.keyed([(0.0, opened), (0.3, above_open), (1.0, neutral["L"])], u), 1.0 - gm.smooth(max(0.0, (u - 0.3) / 0.7))
        l_parts = rig.world("L", l_arm, "Press", "Neutral", w)
        for name, mm in l_parts.items():
            key(gloves[name], f, glove(mm))
        left = {"pose": "Press" if w > 0 else "Neutral", "press_weight": round(float(w), 3),
                "arm_camera": gm.to_record(cam_inv @ l_arm)}
        if a0 <= t <= a3:
            hits = overlaps(gloves, l_parts, props[variant], m)
            worst = max(worst, sum(sum(r.values()) for r in hits.values()))
        if w >= 0.999:    # rig-agnostic contact target: where any index fingertip must be (glove or WORK hand)
            tip = (l_parts["L_Hand"] @ np.append(gm.PRESS_TIP_L, 1.0))[:3]
            left["index_tip_galley"] = [round(float(c), 5) for c in tip]
            left["index_tip_camera"] = [round(float(c), 5) for c in (cam_inv @ np.append(tip, 1.0))[:3]]
        frames.append({"t": round(t, 4), "variant": variant, "prop_galley": gm.to_record(m),
                       "prop_camera": gm.to_record(cam_inv @ m), **clasp(m, drink_grip), "L": left})
    fit["press_window_max_intersecting_pairs"] = worst
    out["clips"]["drink"] = {"frames_blender": [DRINK_START, DRINK_START + n - 1], "duration_s": drink_len,
                             "anchor": "DrinkGrip", "variant_switch_t": gm.DRINK_CLAMP_OPEN_T,
                             "keys": key_list(drink_keys, ["rest", "rest", "hold", "hold", "near_face", "at_lips",
                                                           "at_lips", "near_face", "rest"],
                                              ["galley", "galley", "camera", "camera", "camera", "camera", "camera",
                                               "camera", "galley"]),
                             "events_s": {"left_index_reach": [a0, a1], "left_index_on_clamp": [a1, a2],
                                          "clamp_slid_cap_off": gm.DRINK_CLAMP_OPEN_T, "left_index_back": [a2, a3]},
                             "frames": frames}
    with open(os.path.join(OUT, "gu_motion_clips.json"), "w", encoding="utf-8") as h:
        json.dump(out, h, indent=1)
    with open(os.path.join(OUT, "gu_fit.json"), "w", encoding="utf-8") as h:
        json.dump(fit, h, indent=1)
    bpy.context.scene.frame_start, bpy.context.scene.frame_end = MEAL_START, DRINK_START + n - 1
    bpy.ops.file.make_paths_relative()
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "GU_GalleyUse.blend"), compress=True)
    bh.log("saved; meal %d frames, drink %d frames; press window worst %d pairs" % (len(out["clips"]["meal"]["frames"]), n, worst))
    return {"markers": markers, "gloves": gloves, "props": props, "rig": rig, "drink_keys": drink_keys, "hold": hold}


# ----------------------------------------------------------------------------- frames
def camera(scene, name, pos, target, fov, vertical=False):
    data = bpy.data.cameras.new("GU_Cam_" + name)
    if vertical:
        data.sensor_fit = "VERTICAL"
        data.angle_y = math.radians(fov)
    else:
        data.angle = math.radians(fov)
    data.clip_start = 0.02
    cam = bpy.data.objects.new("GU_Cam_" + name, data)
    cam.location = bh.to_blender_point(tuple(pos))
    cam.rotation_euler = (bh.to_blender_point(tuple(target)) - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.collection.objects.link(cam)
    return cam


def still(scene, cam, frame, markers, show_markers, path):
    scene.camera = cam
    scene.frame_set(frame)
    for mk in markers.values():
        mk.hide_render = not show_markers
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def render(state):
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    prefs = bpy.context.preferences.addons["cycles"].preferences
    for kind in ("OPTIX", "CUDA"):
        try:
            prefs.compute_device_type = kind
            prefs.get_devices()
            if any(d.type == kind for d in prefs.devices):
                for dev in prefs.devices:
                    dev.use = dev.type == kind
                scene.cycles.device = "GPU"
                break
        except (TypeError, RuntimeError):
            continue
    scene.cycles.samples = SAMPLES
    scene.cycles.use_denoising = True
    scene.render.resolution_x, scene.render.resolution_y = 960, 720
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.exposure = 0.4
    world = bpy.data.worlds.new("GU_World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.05, 0.055, 0.06, 1.0)
    scene.world = world
    for name, p, size, energy in (("GU_Key", (0.6, 2.6, 1.6), (1.2, 0.6), 160.0), ("GU_Fill", (-0.8, 1.8, 1.4), (1.0, 0.6), 60.0)):
        data = bpy.data.lights.new(name, "AREA")
        data.size, data.size_y, data.energy = size[0], size[1], energy
        light = bpy.data.objects.new(name, data)
        light.location = bh.to_blender_point(p)
        light.rotation_euler = (bh.to_blender_point((0.3, 1.1, 0.6)) - light.location).to_track_quat("-Z", "Y").to_euler()
        scene.collection.objects.link(light)
    hold, markers = state["hold"], state["markers"]
    zoom_target = (hold @ np.array([0.0, 0.100, 0.0, 1.0]))[:3]
    cams = {"witness": camera(scene, "witness", (1.75, 1.80, 2.05), (0.30, 1.32, 1.00), 44),
            "firstperson": camera(scene, "firstperson", gm.EYE, gm.LOOK, 68, vertical=True),
            "zoom": camera(scene, "zoom", gm.EYE, zoom_target, 16, vertical=True)}
    folder = os.path.join(OUT, "renders")
    os.makedirs(folder, exist_ok=True)
    shots = {"meal": [(MEAL_START + int(round(t * gm.FPS))) for t in (0.2, 0.9, 1.6, 2.1)],
             "drink": [(DRINK_START + int(round(t * gm.FPS))) for t in (0.45, 1.25, 1.35, 2.6)]}
    for clip, frame_list in shots.items():
        for view in ("witness", "firstperson"):
            for k, frame in enumerate(frame_list):
                still(scene, cams[view], frame, markers, view == "witness",
                      os.path.join(folder, "GU_%s_%s_%d.png" % (clip, view, k + 1)))
        bh.log("rendered", clip)
    for name, t in (("press_closed", 1.25), ("press_open", 1.35)):
        still(scene, cams["zoom"], DRINK_START + int(round(t * gm.FPS)), markers, False,
              os.path.join(folder, "GU_study_%s.png" % name))
    # rejected fit: unanimated copies of the right glove placed round the adapter (left glove still neutral)
    frame = DRINK_START + int(round(0.97 * gm.FPS))     # pouch at the hold pose, left glove still near neutral
    m = gm.keyed(state["drink_keys"], frame_time(frame))
    rig, gloves = state["rig"], state["gloves"]
    arm = m @ gm.inv(gm.adapter_fist_grip()) @ gm.inv(rig.hand_from_arm("R", "Grip"))
    copies = []
    for name, mm in rig.world("R", arm, "Grip").items():
        c = gloves[name].copy()
        c.animation_data_clear()
        scene.collection.objects.link(c)
        c.hide_render = c.hide_viewport = False
        c.matrix_world = glove(mm)
        copies.append(c)
    still(scene, cams["zoom"], frame, markers, False, os.path.join(folder, "GU_study_rejected_fist.png"))
    for c in copies:
        bpy.data.objects.remove(c, do_unlink=True)
    bh.log("rendered studies")


def frame_time(frame):
    return (frame - DRINK_START) / gm.FPS


def deliver():
    """Point every image at the released kits in the repository (no copies are delivered) and save the .blend."""
    for img in bpy.data.images:
        f = os.path.basename(bpy.path.abspath(img.filepath)) if img.filepath else ""
        if f:
            img.filepath = next(base for prefix, base in REPO_TEXTURES if f.startswith(prefix)) + f
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "GU_GalleyUse.blend"), compress=True)
    bh.log("delivered GU_GalleyUse.blend with repository texture paths")


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    state = run()
    if RENDER:
        render(state)
    deliver()
    bh.log("done")
