# EVA-SHOULDER-SEAM-02 — clean sleeve colour break (v02 of the EVA full-arm fit)

A proposed derivative of EVA-FULL-ARM-FIT-01 (`93e0e1a`, `../eva-full-arm-fit-v01/`, unchanged); root chooses the
import. It answers item 1 of Codex's 6884216267: the "jagged ivory/grey edge at the shoulder" that v01 reported.

## What the edge was

v01 called it a torso/sleeve interpenetration. **It is not.** Renders with diagnostic colours (torso green, sleeve
WarmIvory red, sleeve FlexGrey blue; `diagnostic/material_colours_v01_vs_v02.png`) show that it is the FitV02 sleeve's
own colour break.
- The sleeve changes from WarmIvory to FlexGrey just above the elbow, about 0.20 m down the upper arm.
- The change runs along a zigzag of whole faces (44 boundary vertices). It reaches 34 mm to one side of its best-fit
  plane and 21 mm to the other, 55 mm in total.
- Large triangles poke through as ivory spikes on grey, and the raised arms put that zigzag in the first-person view.

## The fix: one straight colour break

`sleeve_cuff()` in `Source/build_eva_arm_fit.py` (flag `--cuff`) works on each sleeve in bind pose:
1. It fits a plane to the FitV02 ivory/grey boundary. The plane sits 0.195 m from the shoulder joint and is tilted
   29° to the upper arm axis, the same slant FitV02 drew.
2. It bisects the sleeve along that plane. The new vertices interpolate the skin weights and UVs.
3. It colours each face near the plane by its side of it: ivory towards the shoulder, grey towards the elbow.

Result:
- **Break:** one straight loop of 50 vertices lying on the plane (0.0 mm off it).
- **Per sleeve:** 50 vertices and 50 faces added, 86 faces recoloured.
- **Weights:** every vertex still sums to 1 (0 vertices below 1).
- **Unchanged:**
  - the sleeve surface (the new vertices lie on its existing edges), the torso (no blend shapes), and the 164 bones,
    their names and rest lengths;
  - all glove and finger geometry, the hierarchy, the clip, the poses and the grip retarget;
  - V04 at zero offset.

Every v01 measurement is identical in v02 (`fit-report.json`; columns as in v01):

| Target | Sleeve vertices visible under the glove | Thumb vs sleeve/boot/ring | Glove vs torso | Sleeve vs torso: shoulder seam / limb | Glove pivot error | Grip: deepest glove vertex in the handle |
|---|---|---|---|---|---|---|
| Neutral (right) | 0 of 98 | 0 | 0 | 149 / 2 | 0.00 mm | - |
| RailGrip (right) | 0 of 98 | 0 | 0 | 172 / 46 | 0.00 mm | 0.75 mm |
| WrenchLockStart (right) | 0 of 98 | 0 | 0 | 89 / 1 | 0.00 mm | 2.23 mm |
| WrenchLocked (right) | 0 of 98 | 0 | 0 | 83 / 11 | 0.00 mm | 2.23 mm |

## Evidence

- **Comparisons:** `compare/shoulder_v01_vs_v02.png`, `compare/fp_v01_vs_v02.png` and `compare/side_v01_vs_v02.png`.
  Each shows v01 and v02 for all four poses, rendered with the same settings.
- **Renders:** `renders/` holds the delivered build at 64 samples: first-person from the eye, side and a shoulder
  close-up for Neutral, RailGrip, WrenchLockStart and WrenchLocked.
- **Unity 6000.3.28f1** (`evidence/`), on a temporary check that is not committed. Import: preserveHierarchy, Generic,
  compression Off, the same as v01.
  - Rig `A7_ContinuousBody_Rig` localScale (1, 1, 1); the maximum bone localScale deviation is 0.
  - Skinned result, not only bone heads. The torso and both sleeves are baked with `BakeMesh` at all five clip frames
    and compared with the Blender evaluated meshes (`expected_bounds.csv`). The worst bounding-box corner differs by
    0.001 mm. In the base pose the torso measures 0.483 × 1.383 × 0.480 m and each sleeve 0.147 × 0.565 × 0.300 m, the same as in Blender.
  - Glove pivots are within 0.058 mm of the solved targets. Arm segment lengths change by 0.0004 mm at most.
  - Blend shapes: none on any mesh (A7_EVA_Arm_L 1041 vertices, 2 submeshes, A7_EVA_Arm_R 1041 vertices, 2 submeshes, A7_EVA_TorsoLegs 12102 vertices, 7 submeshes).
  - Captures from Unity: first-person, side and shoulder for Neutral, RailGrip and WrenchLockStart, in flat FitV02
    colours.

## What was tried and not delivered

- **Torso weight blends** (variants 1–4 in `variants/`, from the earlier study `baffbe9`). They smooth the top of the
  shoulder but leave the edge, and two of them stretch or tear the chest patch.
- **Pose-corrective torso blend shapes** (`variants/5_torso_correctives_rejected_*`). They tuck the torso shoulder skin
  under the posed sleeve. Torso vertices 0–25 mm outside the sleeve at the junction drop as follows (right side):
  Neutral 231 → 1, RailGrip 261 → 0, WrenchLockStart 205 → 0, WrenchLocked 202 → 0.
  - In the renders, though, they crumple the SafetyRed patch and the zip puller and open a slit under the arm.
  - The torso cap over the top of the sleeve is FitV02's intended layering, not the defect, so they are rejected.
- **A scye bearing ring and a re-split of the weights at the armhole.** These change nothing visible: the edge was
  never at the armhole.

The flags stay in the script, off by default: `--corrective`, `--scye`, `--scye-split`, `--seam-radius`, and the
diagnostic `--debug-colours`. The delivered FBX uses `--cuff` only.

## Import (unchanged from v01)

Unity ModelImporter with preserveHierarchy = true, animation type Generic, Anim. Compression Off. Place the FBX root at
the player's feet with zero visual offset, the FloatNeutral V04 convention. Object, bone and clip names are the same as
in v01. Only the two `A7_EVA_Arm_*` meshes differ. Textures resolve repo-relatively to the existing glove and FitV02
textures. `binding.json` lists the glove binding, as in v01.

## Limits

- The break follows FitV02's own position and slant; the grey zone is not moved or resized.
- Unchanged from v01:
  - the right glove's grip retarget;
  - the left arm's V04 hang in the one-handed poses: glove vs torso 99 triangles and limb 148, both as in v01;
  - the 15–23 mm reach note.
- Kinematic poses only: no hand IK runtime, colliders or prefab. Performance is not measured.
