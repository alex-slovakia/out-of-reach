"""make_readme - WORK-HAND-CLASP-01: README.md from the measured work_hand_clasp.json, so every number in the text is
the measured one.

Run: python make_readme.py <package folder>
"""
import json
import os
import sys

ROOT = os.path.abspath(sys.argv[1])
rep = json.load(open(os.path.join(ROOT, "work_hand_clasp.json"), encoding="utf-8"))
P = rep["poses"]
TILT = rep.get("grip", {}).get("pouch_tilt_deg", 24.0)
DIG = {"finger1": "thumb", "finger2": "index", "finger3": "middle", "finger4": "ring", "finger5": "little"}
SEG = {"1": "proximal", "2": "middle", "3": "distal"}


def bone_name(b):
    if not b or "-" not in b:
        return str(b)
    head, _ = b.split(".")
    f, seg = head.split("-")
    return "%s %s phalanx" % (DIG[f], SEG[seg])


rows, contact_rows, closing_lines, angle_lines = [], [], [], []
for name, p in P.items():
    fc = p["finger_contacts"]
    pairs = ", ".join("%s %d" % (v.replace("GC_", ""), r["intersecting_pairs"]) for v, r in p["clearance_by_region"].items())
    rows.append("| `%s` | %.2f / %.2f mm | %.2f mm | %.1f / %.1f mm | (%.3f, %.3f, %.3f), %d mm from the grip anchor | %s |" % (
        name, fc["index"]["aperture_from_thumb_contact_along_normal_mm"], fc["middle"]["aperture_from_thumb_contact_along_normal_mm"],
        p["pouch_thickness_at_clasp_mm"], fc["index"]["offset_from_thumb_contact_along_face_mm"],
        fc["middle"]["offset_from_thumb_contact_along_face_mm"], p["clasp_point_prop_space_unity"][0],
        p["clasp_point_prop_space_unity"][1], p["clasp_point_prop_space_unity"][2],
        p["clasp_point_from_grip_anchor_towards_held_edge_mm"], pairs))
    contact_rows.append("| `%s` | %.2f | %s | %s | %.1f mm (%s) |" % (
        name, p["thumb_pad_gap_mm"],
        " · ".join("%s %.2f" % (d, fc[d]["gap_mm"]) for d in ("index", "middle", "ring", "little")),
        " · ".join("%s: %s" % (d, bone_name(fc[d]["bone"])) for d in ("index", "middle", "ring", "little")),
        p["aperture_mm"], bone_name(p["thumb_opposes_bone"])))
    parts = []
    for step in p["closing_from_bind"]:
        hits = step["intersecting_pairs_by_region"]
        parts.append("%d %%: %s" % (round(step["fraction"] * 100), ", ".join("%s %d" % kv for kv in hits.items()) if hits else "0"))
    closing_lines.append("- `%s`: %s." % (name, "; ".join(parts)))
    k, t = p["knuckle_flexion_deg"], p["thumb"]
    angle_lines.append("- `%s`: knuckles index %.1f°, middle %.1f°, ring %.1f°, little %.1f°; thumb CMC rotation vector %s° "
                       "(Blender armature axes), MP %.1f°, IP %.1f°." % (name, k["index"], k["middle"], k["ring"], k["little"],
                                                                       t["cmc_rotation_vector_deg_armature_space"],
                                                                       t["mp_flexion_deg"], t["ip_flexion_deg"]))

misses = [p["thumb"]["miss_from_ideal_target_mm"] for p in P.values()]
study = json.load(open(os.path.join(ROOT, "tilt_study.json"), encoding="utf-8"))
tilt_rows = ["  | Tilt | Thumb miss from its target | Opening along the normal, thumb to index / middle | Clasp shift |",
             "  |---|---|---|---|"]
for r in study["runs"]:
    tilt_rows.append("  | %d° | %.1f mm | %s | %d mm |" % (
        r["tilt_deg"], r["thumb_miss_from_target_mm"],
        "%.2f / %.2f mm" % (r["index_aperture_along_normal_mm"], r["middle_aperture_along_normal_mm"])
        if "index_aperture_along_normal_mm" in r else "not recorded in that run", r["clasp_shift_mm"]))
text = """# WORK-HAND-CLASP-01 — a measured bare-hand clasp for the meal and drink pouches

**Status:** a Blender study for review, by the Claude model writer on 14 November 2026 (#7, Codex GO 6883199083, claim 6883234903).
- **The rig:** the clasp is posed on the published unsuited WORK body (`A7_BodyFit02_WORK.fbx` at `85b0994`), read only. Bone lengths, the bind pose, meshes and weights are unchanged, and no EVA glove is substituted.
- **Finger-local only:** only finger bones rotate (thumb CMC, MP and IP; the knuckles of index to little). The wrist and every arm bone stay at bind, so the clasp is independent of the arm or float pose root chooses.
- **Four poses:** `WORK_Clasp_Drink_R`, `WORK_Clasp_Drink_L`, `WORK_Clasp_Meal_R` and `WORK_Clasp_Meal_L`.
- **Measured, not eyeballed:** every number comes from the evaluated (skinned) arm mesh against the actual GALLEY-PROPS-01 pouch meshes, in world space.
- **Not imported in Unity.** No production Body, Hands, prefab, scene or Core file is touched, and no prop texture is copied.

![Clasp sheet](renders/WORK_Clasp_sheet.png)

The sheet has one row per pose: drink R, drink L, meal R, meal L. Its columns are the front (label side, thumb), the back (fingers) and the top (the aperture).

## The grip

**A pad-to-pad pinch at the pouch centre was tried first and measured impossible.**
- That pinch would put the thumb pad on the front face and the index and middle pads on the back face.
- With a 25 mm aperture, the curled fingers and the palm close a ring only about 39 mm wide around the contact: the fingers cross the pouch's mid-plane 13 mm to one side, the palm 26 mm to the other.
- The pouches are 95 mm (drink) and 125 mm (meal) wide, so every variant cut the pouch in hundreds of triangle pairs.

**The delivered clasp is an edge clasp, the way a flat pack is held in one hand:**
- **Fingers:** index to little stay straight and bend only at the knuckle, each until its skin rests on the back face (bisection, 0.1°).
- **Pouch angle:** the pouch is tilted %.0f° about its own y axis against the straightened fingers, with its far side lifted off the fingertips. The tilt study (`tilt_study.json`; right hand, drink) tested five angles:

%s

  - At every tilt, all four fingers rest on the back face.
  - Only at %.0f° does the thumb reach its target, opposite the index-middle middle phalanges, and the opening between the opposing contacts equal the pouch's thickness there.
- **Pouch edge:** the near edge sits in the web between the thumb and the index.
- **Thumb:** its pad presses the front (label) face.
  - Nelder–Mead over the CMC rotation, MP and IP flexion, then settled on the evaluated skin to a 0.5 mm gap.
  - In the final poses, the thumb pad lands %.1f–%.1f mm from its target.
  - The finger pads it opposes sit beside it along the face, not directly across: those offsets are in the first results table.
- **Clasp point:** it slides from the grip anchor towards the held edge, in 5 mm steps, only until 0 triangles of the whole arm skin intersect the pouch.

## Results

| Pose | Aperture: thumb contact to index / middle contact, along the clasp normal | Pouch thickness at the clasp | Thumb contact to index / middle contact, along the face | Clasp point (prop space) | Intersecting triangle pairs, whole arm skin |
|---|---|---|---|---|---|
%s

| Pose | Thumb pad gap (mm) | Finger gaps to the back face (mm) | Finger contact segments | Straight across from the thumb (to the nearest finger skin) |
|---|---|---|---|---|
%s

**Reading the tables:**
- **Gaps:** each digit's closest skin to the pouch. About 0.1 mm means it rests on the face.
- **Aperture:** the opening between the opposing contacts, measured along the clasp normal. It matches the pouch's thickness to within 1 mm: the finger contacts lie towards the pouch centre, where the pillow is slightly thicker than at the clasp point.
- **Straight across:** the last column measures from the thumb contact through the pouch to the nearest finger skin. That skin lies beside the thumb rather than under it, so the value is larger.

**Finger-local angles** (the exact rotations are in the JSON):
%s

The thumb's MP and IP joints end slightly hyperextended: the IP rests at the solver's −10° limit. That is within a normal thumb's range, but it is the stiffest part of the pose. Only the knuckles of index to little rotate; their middle and distal joints stay at bind.

## Closing from bind

Every rotated bone slerps from the bind pose to the clasp, with the pouch already in place. The intersecting pairs by region at each step are:
%s

Intersections at the early steps mean the published bind pose (open hand, thumb at rest) already overlaps the pouch's final place. The hand must therefore approach with the thumb opened away first: the clasp is a destination pose, not a path through the pouch.

## Using it in Unity (not verified in Unity here)

**Paths:** `bones[...].unity_path` is relative to the imported model's root, with the armature node `A7_ContinuousBody_Rig`. As root found for the V04 clip, that node exists only with `preserveHierarchy = true`.

**Convention-free values.** Every `*_unity*` value is given in the Unity model basis.
- That basis is (−x, z, −y) of the Blender armature axes, which root verified for this rig's imported bone positions to 1.06 µm (`docs/verification/i06-body-float-v04`).
- The values describe the body at its bind pose and are measured from the wrist head.
- Unity local rotations are deliberately not given, because they depend on the importer's per-bone axes, which this kit has not verified.

**Applying a pose,** with all rotations in model space:
1. At bind (the model's default state), capture `Wb`, the wrist rotation, and `Bb`, each listed bone's rotation.
2. At runtime, with the wrist's current rotation `Wr` and position `Wp`, set each listed bone's rotation to `Wr * Inverse(Wb) * model_delta_at_bind_unity_xyzw * Bb`, parent before child.
3. A point `p` given "from the wrist" lies at `Wp + Wr * Inverse(Wb) * p`. These points are the anchors ThumbPad, OpposingFingerSkin and ClaspPoint, each finger's contact point, and the pouch origin.
4. **Check:** every listed `head_from_wrist_unity_m` and `tail_from_wrist_unity_m` must then land within 2 mm. This is the same kind of check as root's V04 bone test.

**Placing the pouch:** its prop origin and axes relative to the wrist are in `pouch_from_wrist_unity` (prop x; y up the pouch; z the label side, towards the thumb). Equivalently, put the prop point `clasp_point_prop_space_unity` on the ClaspPoint anchor.

**Effect on the galley motion clips:** in GALLEY-USE-MOTION-01, the clasp targets sat at the grip anchor. With this hand, the clasp point lies at the prop-space offsets above instead.

## Files

| File | What it is |
|---|---|
| `work_hand_clasp.json` | For each pose: the rotated bones with Unity paths, model-space deltas at bind, joint head and tail targets, and Blender bone-local deltas; the anchors, finger contacts and pouch from the wrist; apertures and gaps; clearance by region for every pouch variant; the closing steps |
| `wc_manifest.json` | Every file with its size and SHA-256, plus the inputs |
| `renders/` | 12 Blender stills (3 views × 4 poses) and the sheet |
| `source/clasp_solve.py` | The solver and measurements (`--final --render --tilt %.0f`) |
| `source/inspect_work.py`, `probe_axes.py`, `probe_pads.py`, `quick_geo.py` | The diagnostics behind the decisions above: the rig listing, the takes' finger rotations (none), the pad directions, and the pinch ring |
| `source/make_sheet.py`, `make_readme.py`, `make_manifest.py` | The packaging scripts |
| `source/a7kit.py`, `hb_kit.py`, `build_habitat.py`, `gc_kit.py`, `*_materials.json` | The released pouch and material helpers, copied unchanged |

The kit has no `.blend` and no copy of the body or of any texture. The study is rebuilt from the published files.

## Checks actually run

| Check | Result |
|---|---|
| Body input | the LFS object `46b3051a…` (7,111,548 bytes) at `85b0994`, imported in Blender 5.3: 164 bones, `A7_WORK_Arm_L/R` with bare hands |
| Bind-pose takes | none of the three takes rotates a finger, so the clasp starts from bind |
| Thumb pad direction | the published thumb's bending plane points at its pulp; checked against the flat nail side of the skin (0.996) |
| Pinch at the pouch centre | measured impossible: a 39 mm ring around the contact |
| Pouch tilt | 0°, 6°, 12°, 18° and 24° compared on the right hand with the drink; %.0f° kept |
| Clasp and clearance | the evaluated arm skin against every pouch variant (world-space BVH), as tabulated above |
| Closing from bind | 5 steps per pose, as listed above |
| Frames | 12 stills rendered and inspected |

**Not verified:**
- Unity import, the local rotations, and playback.
- The arm's reach to the face: no camera or mouth assumption is used here.
- How the clasp reads in play.

## Rebuild

```
blender -b --factory-startup --python source/clasp_solve.py -- --fbx <A7_BodyFit02_WORK.fbx> --gc <a folder whose textures/ holds the pouch and ship textures> --out <work folder> --final --render --samples 48 --tilt %.0f
python source/make_sheet.py <work folder>/renders
python source/make_readme.py .
python source/make_manifest.py .
```
""" % (TILT, "\n".join(tilt_rows), TILT, min(misses), max(misses), "\n".join(rows), "\n".join(contact_rows),
       "\n".join(angle_lines), "\n".join(closing_lines), TILT, TILT, TILT)
with open(os.path.join(ROOT, "README.md"), "w", encoding="utf-8", newline="\n") as h:
    h.write(text)
print("README written")
