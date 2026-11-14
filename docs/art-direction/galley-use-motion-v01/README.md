# GALLEY-USE-MOTION-01 — staged meal and drink use at the AURORA-7 galley

**Status:** an authored Blender animation set for review, by the Claude model writer on 14 November 2026 (#7, Codex task 6882653430 item 2, claim 6882668849).
- **No approved glove pose can carry a pouch** (measured below). The pouches therefore move along the path a carrying hand would take, and every frame gives the exact missing anchor: a clasp target. This is not a finished hand animation.
- **The left glove's Press pose is proven** on the drink's pinch clamp. Its index tip rests on the clamp and slides it down when the pouch switches from Closed to Open, with 0 intersecting triangles over the whole hand motion.
- **Nothing is integrated in Unity,** and no Core, scene, prefab or runtime file is touched.
- **The source geometry is used unchanged:** the habitat, the consumables and the gloves. No FBX, texture or material is copied.
- **The frames are Blender 5.3 Cycles renders,** not Unity captures.

![Drink clip](renders/GU_drink_sheet.png)

![Meal clip](renders/GU_meal_sheet.png)

How to read the clip sheets:
- **Top row:** a witness view from beside and behind the player. The eye (dark) and the assumed mouth (amber) are shown as markers.
- **Bottom row:** the first-person view through the game camera (vertical FOV 68).
- **Frame times:** drink at 0.45, 1.27, 1.33 and 2.60 s; meal at 0.20, 0.90, 1.60 and 2.10 s.

![Index on the closed clamp, on the open clamp, and the rejected fist](renders/GU_study_sheet.png)

The study sheet shows zoomed first-person stills, left to right:
1. The left index on the closed clamp (1.27 s).
2. The same index on the open clamp after the swap (1.33 s). The cap now hangs on its tether.
3. The rejected Grip fist round the adapter.

## Read first: suit state and timing

**Unsuited only.**
- The clips bring a pouch to the bare face, with the helmet off. They must never play while the suit or helmet is on: nothing is eaten through a visor.
- `AstronautHandsPresenter` shows the suit gloves only while the suit is worn.
- The left hand in the frames is the approved glove rig (AstronautHands). Its base colour is darkened like the separate work-glove instance (`MAT_Cargo_WorkGloves`). This only marks unsuited use.
- The unsuited WORK body (`A7_BodyFit02_WORK` at `85b0994`, with bare hands) can be driven to the same targets, which do not depend on the rig: `index_tip_*` and `clasp_target_*`.
- Posing the WORK body's fingers belongs to the body owner, so it is not done here.

**Animation time is not simulation time.**
- One clip is one staged bite (meal, 3.4 s) or one sip (drink, 4.1 s).
- It does not stand for the meal's duration, the amount eaten, the water volume or the moment Core awards anything. Core owns all of those.
- A clip only presents a command that has already succeeded.

## Clips (30 fps)

| Clip | Blender frames | Length | Prop and variant | Keys (s) |
|---|---|---|---|---|
| meal | 1–103 | 3.4 s | `GC_MealPouch_Opened` | on the table 0–0.4 · lifted 0.9 · near the face 1.6 · opening at the lips 1.8–2.3 · near 2.6 · back on the table 3.4 |
| drink | 121–244 | 4.1 s | `GC_DrinkPouch_Closed`, then `_Open` from 1.30 s | on the table 0–0.45 · held low in the view 0.95–1.55 · near the face 2.15 · straw at the lips 2.35–2.95 · near 3.25 · back on the table 4.1 |

**Drink, left hand:**
1. **0.95–1.15 s:** the index leaves Neutral, passes 15 mm above the clamp and comes down onto it.
2. **1.15–1.30 s:** the index rests on the closed clamp.
3. **At 1.30 s:** the pouch switches to Open.
   - The clamp jumps 20 mm down the straw, and the index goes with it in the same frame (a quick flick).
   - The cap comes off onto its tether in the same cut.
   - The kit has no separate clamp or cap objects, and its geometry stays unchanged.
4. **1.30–1.40 s:** the index rests on the open clamp.
5. **1.40–1.60 s:** the index slides 15 mm up the straw, then the hand relaxes back to Neutral.

**Meal note:**
- The "at the lips" key brings the cut opening to the lips, as with a squeeze pouch.
- A spoon is the usual way to eat from an opened thermostabilised pouch, but no spoon is among the approved props.
- If that reads wrong, end the motion at the "near the face" key.

## Coordinates and anchors

- **Space:** the HB_Galley unit's own space, as in `habitat-v01`.
  - Unity metres, y up, the unit's front +z towards the room.
  - The deployed table top is at y 0.9975.
- **In the ship:** HB_Galley sits at (−2.38, 0, 14.75) with yaw +90° (`habitat-v01/integration/hb_integration_manifest.json`).
  - Ship point = (gz − 2.38, gy, 14.75 − gx).
  - Ship rotation = (0, 0.70711, 0, 0.70711) × galley rotation.
- **Eye (camera):**
  - At (0.30, 1.62, 1.28) in galley space, looking at (0.33, 1.05, 0.62).
  - Its frame is x right, y up, z forward, like the glove rig's root.
  - In the ship that is (−1.10, 1.62, 14.45), about 0.5 m in front of the deployed table.
- **Mouth (assumed):**
  - (0, −0.085, 0.03) in camera space, which is (0.2985, 1.5360, 1.3128) in galley space.
  - This is the one anchor the kit cannot prove. The body rig should confirm or replace it.
- **Re-targeting:**
  - Every authored key is stored in both spaces, with a `space` tag.
  - Table keys are defined in galley space, and face keys relative to the eye.
  - For another stance, keep each key in its own space and recompute the other.
- **Prop anchors (GALLEY-PROPS-01):**
  - MealGrip and DrinkGrip at (0, −0.020, 0).
  - MealMouth at (0, 0.058, 0).
  - DrinkStrawTip at (−0.004, 0.160, 0.006).
  - DrinkFillPort at (0, 0.0985, 0).

## Measurements

| What | Value | Source |
|---|---|---|
| Grip fist | closes round a 34 mm tube; axis point (0, −0.033, 0.082) in `R_Hand` | AstronautHands README |
| Grip fist width along its axis (four fingers) | 93.7 mm | measured on the glove meshes |
| Press index tip | (−0.0411, −0.0094, 0.1945) in `R_Hand`; x negated for L | AstronautHands README |
| Meal pouch body | 125 × 150 × 28 mm | `gc_kit.py` |
| Drink pouch: body, adapter, straw | body 95 × 150 × 24 mm; adapter 42 × 26 × 17 mm; straw Ø 6.4 mm, its tip 62 mm above the septum | `gc_kit.py` |
| Free neck between the top seal and the clamp | 24 mm (open) to 44 mm (closed) | `gc_kit.py` |
| Index on the closed clamp | 0 intersecting pairs; the tip is 0.7 mm from the clamp and 3.3 mm from the straw; the rest of the hand is at least 21.5 mm away | BVH search and check |
| Index on the open clamp | 0 intersecting pairs; the tip is 1.1 mm from the clamp and 1.7 mm from the straw; the rest of the hand is at least 21.4 mm away | BVH search and check |
| Left-hand motion, every frame from 0.95 to 1.60 s | 0 intersecting pairs | build-time check |

## Why no hand carries a pouch, and the exact missing anchor

The rig has four poses: Rest, Neutral, Grip and Press.

**What a closed fist can hold:**
- The gripped part must be narrower than the fist's 34 mm tube in both directions across the fist.
- Nothing may leave the fist sideways.

**Why each pose fails:**
- **The pouch bodies** are too wide: the meal body is 125 × 150 × 28 mm and the drink body 95 × 150 × 24 mm.
- **The drink adapter** fits the tube, but the straw leaves it upwards and the body downwards.
- **A fist round the adapter, axis across the pouch,** was measured at the hold pose in world space and rejected (see the third study still):
  - The hand, arm and thumb cut the foil (731 intersecting triangle pairs) and the labels (71).
  - The thumb, index, middle and ring fingers cut the adapter (237), the straw (92), the tether cord (76), the clamp (56) and the septum (32).
- **A fist along the straw** is 93.7 mm wide. The free neck between the top seal and the clamp is only 24 to 44 mm long, so the fist would cover the clamp and the straw, or close round the 95 mm body.
- **Neutral and Rest** are open hands and cannot hold anything.

**The missing anchor, exactly:**
- **A Clasp pose for R and L:** the thumb pad opposite the index and middle finger pads, with an aperture of 24 mm (drink) to 28 mm (meal).
- **A ClaspPoint in each Hand frame:** the middle of that aperture, given in the same way as the rig's Grip and Press reference points.
- **Targets in every frame of both clips:** `clasp_target_galley` and `clasp_target_camera`.
  - Each is the pouch's grip anchor (MealGrip or DrinkGrip) with the pouch's own axes.
  - +z is the label side (thumb), −z the back (fingers), +y up the pouch.
- **Placing the hand once the pose exists:**
  - hand = clasp_target × inverse(ClaspPoint frame).
  - `R_Arm` in camera space = inverse(eye) × hand × inverse(Clasp wrist rotation), as for Grip and Press in the rig README.
- **On the table:** the pouch lies label up, so the fingers cannot get under it. A clasp has to start from its near edge.

## Files

| File | What it is |
|---|---|
| `gu_motion_clips.json` | Both clips sampled at 30 fps (see the list below) |
| `gu_fit.json` | Build-time measurements: the fist width, the rejected fist, the Press settling on both clamps, and the worst frame of the left-hand motion |
| `gu_fit_check.json` | An independent check of the saved `.blend` at 12 frames of the drink clip |
| `gu_manifest.json` | Every file with its size and SHA-256, plus the inputs |
| `GU_GalleyUse.blend` | The keyed Blender 5.3 scene: the galley unit, both pouches in both variants, the gloves, the eye and mouth markers, cameras and lights |
| `renders/` | 16 clip frames, three studies and three contact sheets |
| `source/` | `gu_motion.py`, `build_motion.py`, `fit_check.py`, `make_sheets.py` and `make_manifest.py`, plus the released helpers they import, copied unchanged |

`gu_motion_clips.json` contains:
- **Every frame:** the prop transform and the clasp target, in galley and camera space.
- **The authored keys,** each with its space tag.
- **For the drink:** the variant per frame and the left hand.
  - The left hand has its pose and Press weight.
  - Its `L_Arm` placement is in camera space, as AstronautHandsPresenter sets it.
  - While the Press pose is fully applied (1.10–1.43 s; in contact 1.15–1.40 s), its index-tip target is included.
- **Notes:** the eye, the mouth, the galley mount, the suit state, the timing and the missing anchor.

**The .blend has no copied textures.**
- Its image paths point at `../habitat-v01/textures/`, `../galley-consumables-v01/textures/` and the glove texture in `Assets/_OutOfReach/Visuals/Models/Aurora7/Props/AstronautHands/Textures/`.
- It opens complete once the habitat and consumables kits sit beside this folder. Until then, rebuild it (see the end of this README).

**Inputs, compared with the released branches byte for byte:**
- `feature/habitat-details-v1@5b0f1ca`:
  - `hb_kit.py`, `build_habitat.py`, `a7kit.py` and `hb_materials.json`; the JSON differs only in line endings.
  - 28 textures, which match their LFS hashes.
- `feature/galley-consumables-v1@12100dd`: `gc_kit.py`, `gc_materials.json` and `GC_Labels.png`.
- AstronautHands: the FBX, the rig and the base-colour texture.
  - The same blobs at `33f474f`, `4a34a6b` and `85b0994`.
  - Last changed in `610ed02`.

## Checks actually run

| Check | Result |
|---|---|
| Blender 5.3.0 build (`build_motion.py --render`) | exit 0; clip and fit JSON, the `.blend` and 19 frames |
| Press settling (world-space BVH, 0.25 mm steps) | closed clamp: lifted 5.0 mm to clear, 0 pairs; open clamp: 3.75 mm, 0 pairs |
| Every frame of the left-hand motion (20 frames, 0.95–1.60 s) | 0 intersecting pairs |
| Independent check of the saved `.blend` (`fit_check.py`, 12 frames) | 0 intersecting pairs; contact distances as in the table above |
| Rejected fist | 1,295 intersecting pairs (by material above) |
| Inputs against the released branches | helpers identical; 30 textures match their LFS hashes; glove blobs identical |
| Frames | 16 clip frames and 3 studies rendered and inspected |

**Not verified:**
- Unity import or playback of these clips.
- The mouth anchor against the body rig.
- A clasp pose, which does not exist yet.
- How the clips feel in play.

## Rebuild

Use any work folder for `--out`, not this folder, because the build copies the textures there.

```
blender -b --factory-startup --python source/build_motion.py -- --out <work folder> --gc ../galley-consumables-v01 --habitat ../habitat-v01 --hands ../../../Assets/_OutOfReach/Visuals/Models/Aurora7/Props/AstronautHands --render --samples 48
python source/make_sheets.py <work folder>/renders
blender -b <work folder>/GU_GalleyUse.blend --factory-startup --python source/fit_check.py -- --out <work folder> --frames 135,150,153,156,159,160,161,163,166,169,199,230
python source/make_manifest.py .
```
