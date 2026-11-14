# AstronautHands — gloved hands and forearms for the first-person view

Requested by the integrator in issue #7 on 14 Nov 2026 (12:21 UTC): both hands, a simple documented rig and the
poses neutral, grip and press. Authored by the prop chat (Claude / Apoliak, "Blender fork") in Blender 5.3 from a
script and checked in Unity in a private clone.

The hands are EVA-style gloves on suit sleeves: padded fingers with grip pads, a knuckle guard, a palm strap, a
wrist disconnect ring with a lock collar and a red identification stripe on the sleeve. Each hand is a hierarchy
of **rigid parts on plain transforms: no skinning, no bones**. No gameplay state, script, Animator, camera,
light, collider or audio listener.

**The hands belong to the player rig, not to the ship.** The model writer imports this folder as files only and
does not place the prefab in `Aurora7_Models.prefab`. The integrator or the gameplay owner attaches
`AstronautHands.prefab` to the player's camera. The prefab works on its own: apart from the render pipeline's
URP Lit shader it uses nothing outside this folder, and the import check ran in a checkout that does not
contain the ship model.

## Interface

| Fact | Value |
|---|---|
| Prefab | `AstronautHands.prefab`: root `AstronautHands` with `R_Arm` and `L_Arm` below it |
| Origin and axes | the root's origin is the eye; X right, Y up, Z forward (the view direction); metres |
| How to mount | child of the first-person camera (or of a transform that follows it) with zero local position, identity local rotation and unit scale |
| Hierarchy per hand | `Arm` > `Hand` > `Thumb1` > `Thumb2` > `Thumb3`, and likewise `Index1-3`, `Middle1-3`, `Ring1-3`, `Little1-3` below `Hand`; every name carries the prefix `R_` or `L_` |
| Pivots | `Arm` and `Hand` on the wrist, every finger part on its own knuckle |
| Rest pose | both hands flat, palms down, fingers forward, wrists at (±0.17, −0.22, 0.45); **every joint has identity local rotation and unit scale** |
| Size at rest (X × Y × Z) | 0.46 × 0.12 × 0.528 m for the pair; bounds min (−0.23, −0.28, 0.13), max (0.23, −0.16, 0.658) |
| One hand | forearm 0.32 m long and 0.12 m across at its end; wrist to the tip of the middle finger 0.208 m; palm 0.10 m wide. This is glove size, larger than a bare hand |
| Meshes, triangles | 34 rigid parts (17 per hand), 8,112 triangles (4,056 per hand) |
| Material | one URP Lit material for both hands, `AH_AstronautHands` |
| Textures | 3 × 2048²: BaseColor, MetallicSmoothness (metallic in RGB, smoothness in alpha), Occlusion. The left hand is the mirrored geometry on the same UVs |

## Rig file

`AstronautHands_rig.json` holds everything a script needs, in Unity axes and metres:

- `joints`: `name`, `parent` (empty for the two arms), `position` (local to the parent, the rest pose) and
  `flexAxis`. `Quaternion.AngleAxis(angle, flexAxis)` with a positive angle curls that part towards the palm; for
  `Hand` it bends the wrist palm-wards, for the thumb it carries the tip across the palm. The axis is given in
  the joint's own frame.
- `poses`: `Neutral`, `Grip`, `Press`. Each has `rotations` (the local rotation of every joint except the arms,
  as x, y, z, w) and `placements` (a suggested local position and rotation of `R_Arm` and `L_Arm`, see below).

The rest pose is not listed as a pose: it is identity on every joint.

`JsonUtility` reads the file with these classes (the import check used exactly them); the two text fields
`units` and `rest` are notes and are skipped:

```csharp
[Serializable] class RigFile { public Joint[] joints; public Pose[] poses; }
[Serializable] class Joint { public string name; public string parent; public float[] position; public float[] flexAxis; }
[Serializable] class Pose { public string name; public JointRotation[] rotations; public Placement[] placements; }
[Serializable] class JointRotation { public string name; public float[] rotation; }
[Serializable] class Placement { public string name; public float[] position; public float[] rotation; }
```

## Poses and clips

| Pose | What it is | Clips |
|---|---|---|
| Rest | flat open hand, the pose of the model file; it also serves as a hand braced on a flat surface | `R_Rest`, `L_Rest` |
| Neutral | relaxed hand, fingers slightly curled | `R_Neutral`, `L_Neutral` |
| Grip | fist closed around a tube 34 mm in diameter | `R_Grip`, `L_Grip` |
| Press | index finger straight, the other fingers folded, thumb tucked | `R_Press`, `L_Press` |

The eight clips in `Animations/` are generic (not legacy) clips, two equal keys 1/30 s apart. Each one sets
`localRotation` of the wrist (`Hand`) and the fifteen finger parts of **one** hand and nothing else, so the two
hands can hold different poses. No clip moves an `Arm`: where a hand goes depends on what it holds.

Two ways to use them, both without an Animator:

```csharp
gripRight.SampleAnimation(handsRoot, 0f);     // handsRoot = the instance of AstronautHands.prefab
```

or read the rig file and set (or `Quaternion.Slerp` between) the joints' `localRotation` yourself, which also
gives a smooth change between two poses and does not depend on how a player build samples clips. The clips can
equally go into an Animator with one layer per hand.

## Placing a hand

Suggested placements of the arms for the game's camera (FOV 68, near clip 0.05), as stored in the rig file and
used for every preview here. For `L_Arm` negate X of the position and the y and z of the quaternion.

| Pose | `R_Arm` local position | What it does |
|---|---|---|
| Neutral | (0.19, −0.215, 0.40) | hands low in the view, palms rolled 30° towards each other |
| Grip | (0.1355, −0.1769, 0.3864) | closes the hand on a rail through (0, −0.205, 0.47) that runs left to right, 0.13 m right of the centre |
| Press | (0.1189, −0.1351, 0.4155) | puts the index fingertip on (0.062, −0.128, 0.606), a button face 0.61 m ahead |

To put a hand on a real object, use these reference points. They are given in the frame of `R_Arm` with the
pose's clip applied, so they include that pose's wrist bend; for `L_Arm` negate X of a point.

| Pose | Reference | In `R_Arm` space | In `R_Hand` space |
|---|---|---|---|
| Grip | a point on the axis of the gripped tube | (0.0149, −0.0635, 0.0597) | (0, −0.033, 0.082) |
| Grip | direction of that axis | (0.9703, 0, −0.2419); for `L_Arm` (0.9703, 0, 0.2419) | (1, 0, 0) |
| Press | tip of the index finger | (−0.0089, −0.0690, 0.1864) | (−0.0411, −0.0094, 0.1945) |

Choose the arm's rotation, then `arm.position = target − arm.rotation * point`. The grip fits a tube of 17 mm
radius; the ship model's handrails are 15 mm in radius (`handrail()` in `a7_ship.py` at `4ccc17c`), so they sit
inside the fingers with 2 mm to spare.

## Files

- `AstronautHands.prefab` — the nested rig with its material. It uses the meshes of the model file. **Use the
  prefab, not the model file**: the model file has no hierarchy.
- `AstronautHands.fbx` — 34 triangulated meshes with exported normals, side by side at their rest places, no
  texture records and no local paths. Its `.meta` remaps the material to `Materials/`.
- `AstronautHands_rig.json` — joints, flex axes, poses and placements.
- `Animations/` — the eight pose clips.
- `Materials/`, `Textures/` — as listed above. The textures are clamped atlases; MetallicSmoothness and Occlusion are linear.
- `Previews/` — six genuine Cycles renders of these meshes with these textures: `fp_neutral`, `fp_grip` and
  `fp_press` from the game's camera (at the eye, vertical FOV 68°, 16:9), and `hero`, `grip`, `wire`. Three
  captures made in Unity by the import check from the same camera values, posed with the clips and the
  placements above: `AstronautHands_unity_fp_neutral.png`, `_grip.png`, `_press.png`. The rail, plate, panel and
  button are stand-ins, not part of this prop. In both press pictures the left hand waits in the neutral pose.
  The Unity captures use two plain directional lights and no tone mapping; they show that the import, the rig
  and the clips work, not final lighting.
- `Source/` — `build_astronaut_hands.py` regenerates the model, baked textures, renders, rig file, FBX and `.blend`:
  `blender -b --factory-startup --python build_astronaut_hands.py -- --stage full --out <folder outside Assets>`.
  `verify_fbx.py` re-imports an FBX and reports what a consumer receives. `AstronautHands_report.json` is the
  report of the build that produced these files. No `.blend` is included: a saved `.blend` stores the absolute
  path it was saved to, and the script rebuilds it in under three minutes.

## What must stay together

The prefab points at the meshes of the FBX and at the material by GUID, the material points at the textures by
GUID, and the FBX `.meta` holds the material remap and import settings. Keep the `.meta` of every **file**
unchanged. The clips address the joints by path below the prefab root (`R_Arm/R_Hand/R_Index1/…`), so the
joints must keep their names and nesting. The folder may be renamed or moved freely as long as each file
travels with its `.meta`.

## Notes

- **Why the model file is flat.** Blender's FBX exporter, with the axis conversion baked in, gave every second
  level of the hierarchy a wrong rest rotation and unconverted offsets in Unity (the first import check found
  joints up to 147 mm off). The parts are therefore exported without parents, and the prefab was nested from
  the rig file by the import check, which compared every position with the rig file before and after.
- **Colours.** Suit fabric warm white (#D5D3CB), grip pads blue-grey (#59636E), knuckle guard in the ship's
  `A7_PaintGray` (#8A8884), strap and wrist ring graphite (#2B2B2C), lock collar and buckle clear metal
  (#B6B9BC), stripe in the ship's `A7_PaintRed` (#B32A22).
- **The grip preview is a look, not a fit.** In the suggested Grip placement the forearms are turned 14°
  outwards, which leaves the hand's own tube axis 7° off the rail. To seat a hand exactly, use the reference
  point and direction above.

## Licences and sources

| Item | Source | Licence |
|---|---|---|
| Geometry, UVs, baked textures, rig file, clips | generated by `Source/build_astronaut_hands.py` and the Unity import check; no external model or texture | original team work |
| Tool | Blender 5.3.0 (Cycles) | GPL tool; its output carries no licence obligation |

Reference for function only: pressurised EVA gloves with a wrist disconnect and padded fingers. No NASA model,
texture, drawing, photograph, logo or insignia is copied or included, no third-party game asset or downloaded
texture is used, and there is no weapon or weapon pose.

## Verification actually performed (14 Nov 2026, Apoliak's machine)

- **Blender 5.3.0, Cycles/OptiX.** Full build exit 0: 34 objects, 8,112 triangles, 0.46 × 0.528 × 0.12 m
  (Blender X, Y, Z), six renders written.
- **FBX round trip.** `verify_fbx.py` re-imported the FBX into an empty Blender scene: 34 objects without
  parents at unit scale, same size, 8,112 triangles, only triangles, custom normals present, one UV map, one
  material name, no texture reference. The file contains no drive-letter path.
- **Rig file arithmetic**, with a separate script and no Blender: `AngleAxis(angle, flexAxis)` equals the stored
  rotation of every middle and end joint within 0.001°; with the stored placements the grip point lands on
  (±0.13, −0.205, 0.47) and the index fingertip on (±0.062, −0.128, 0.606), the places of the stand-ins.
- **Unity 6000.3.28f1, URP**, batch run in a private clone of the repository: exit 0, no validation problem.
  Measured there: 34 meshes for 34 joints; every part of the model file within 0.008 mm of the rest place the
  rig file gives it, with identity rotation and unit scale; after nesting, every joint within 0.006 mm of the
  rig file's local position; 8,112 triangles; rest bounds min (−0.23, −0.28, 0.13), max (0.23, −0.16, 0.6582);
  every renderer uses the one external URP Lit material; no collider or camera. Each of the six pose clips,
  sampled on the prefab instance, gave the rig file's rotations (difference 0° at Unity's precision) and left
  the other hand untouched; each Rest clip brought every joint back to identity. Three captures were taken
  from a camera at the eye with FOV 68 and near clip 0.05.
- `tools/verify_repository.py` passes in that clone with this folder in place (every file has a `.meta`, GUIDs unique).

## Limits

- **Not verified in the game.** Nothing here was tried under the real player camera, with the game's lighting,
  post-processing or HUD, or in a Windows player build. `SampleAnimation` was exercised in the Editor only.
  Performance was not measured.
- **Rigid parts.** There is no skinning: at a joint the padded parts overlap instead of deforming. That reads
  well at arm's length in first person and shows in a close-up. A skinned mesh with bones is not provided.
- **Static poses.** There is no finger animation, no blending logic and no IK; a consumer blends between poses
  and places the arms.
- The sleeve ends 0.32 m behind the wrist with a dark cap. With the suggested placements that end is at least
  27° outside a 16:9 view and 0.08 m or more in front of the eye, beyond the near clip of 0.05; another
  placement must keep it out of view.
- The left hand mirrors the right one, including its wear marks. One LOD, no normal map, no lightmap UVs.
  Base texture about 0.33 mm per texel (3,060 px/m).
