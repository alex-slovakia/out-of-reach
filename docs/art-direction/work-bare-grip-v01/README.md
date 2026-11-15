# WORK-BARE-GRIP-01: bare WORK hand grip on a 15 mm-radius rail (v01)

Staged art for root's BareArmContactPresenter: an open-to-closed grip take and an exact closed endpoint for each bare WORK hand around a **round rail of 15 mm radius (30 mm diameter)**, the size of every current authored rail. It is solved against the evaluated skin of the original body.

- **Not claimed:** that one pose fits the bridge's whole 12–17 mm range. It was solved and checked for 15 mm only.
- **Not touched:** Player, Core, contact contracts, the scene, physics, reach or the rig.

**Status:** source delivery for root's integration check. The arm and wrist placement onto a real rail is root's presenter's job.

![right hand (top) and left hand (bottom): palm, side and back views](renders/grip_sheet.png)

## Files

| Path | What |
|---|---|
| `BODY_WORK_GripRail_R_V01.fbx`, `BODY_WORK_GripRail_L_V01.fbx` | Animation-only clips on the original 164-bone `A7_ContinuousBody_Rig` bind, takes `WORK_GripRail_R` and `WORK_GripRail_L` |
| `grip_anchors.json` | Per hand: the rail axis and radius, in Unity model space and in the wrist bone's bind frame; palm normal; finger direction; every joint's closed delta (Blender local axis and angle, and the Unity model-space quaternion); closed-pose bone heads and fingertip tails |
| `grip_report.json`, `grip_solve.log` | Skin evidence per region and per frame (below), and the solver's log |
| `grip_pose_samples.json` | Every finger and thumb bone's local rotation at all 21 frames, re-read from the FBX |
| `renders/` | Workbench stills: palm, side and back views per hand, and the sheet |
| `source/grip_solve.py` | The solver and exporter |
| `unity_check/` | `GripCheck.cs`, its raw output, and the endpoint comparison |

**Input:** `A7_BodyFit02_WORK.fbx` from `Assets/_OutOfReach/Visuals/Character/FirstPersonBody/FitV02/Meshes/`, LFS sha256 `46b3051a592f2e2fe3a0e7579ad281411415a6eb71ad4546c2899e47cf5eec90`.

## Clips

- **Length:** 21 frames, exported 0..20 and imported as 1..21, at 30 fps (0.667 s).
- **Open → closed:**
  - The fingers close from the bind. A joint whose solved angle is about 0 starts 5° open, so that it still moves.
  - MCP leads, PIP and MP follow, DIP and IP close last. Each joint follows a smoothstep over its own window (0–0.6, 0.25–0.85 and 0.45–1.0 of the take).
  - The thumb CMC closes from 115 % of its opposition (spread) to 100 % in the first 60 % of the take.
- **The last frame is the exact closed endpoint.**
- **Curves:** only the 15 finger and thumb bones of that hand, rotation only. Unity shows 60 bindings, `m_LocalRotation` on `finger1-1`..`finger5-3` of that side. No wrist, arm, body, scale or position curves.
- **Export:**
  - `FBX_SCALE_ALL` (unit rig scale, metre units), −Z forward, Y up.
  - The bind is keyed at frame 100, outside the take, so the FBX bone nodes are the original bind. This is the representation of V04 and of STANDING-IDLE v02.
- **Unity importer:** V04's settings: Generic, `preserveHierarchy 1`, animation compression Off, `globalScale 1`, `useFileScale 1`, `useFileUnits 1`, `bakeAxisConversion 0`.

## Rail and placement

- The rail lies across the palm along the index→little knuckle line. It is seated into the palm, then the digits close onto it.
- Coordinates are in metres, at the closed pose:

| Hand | Rail axis point, wrist bone bind frame | Axis direction, wrist bone bind frame | Point, Unity model space | Direction, Unity model space |
|---|---|---|---|---|
| R | (−0.0105, 0.0737, 0.0089) | (−0.9981, −0.0563, 0.0263) | (0.4520, 1.0740, 0.2275) | (0.4837, −0.5817, −0.6540) |
| L | (−0.0108, 0.0760, −0.0040) | (−0.9981, −0.0563, −0.0264) | (−0.4561, 1.0736, 0.2309) | (−0.4837, −0.5817, −0.6540) |

- **To place a hand on a rail,** pose the wrist so that the world rail axis maps to that point and direction in the wrist bone's frame, then play or sample the take.
- **Unity model space** is (−x, z, −y) of the Blender armature axes, the mapping root verified for the WORK body.

## Evidence (Blender 5.3, evaluated skin of `A7_WORK_Arm_R` and `A7_WORK_Arm_L`)

Closed endpoint; minimum skin-to-rail gap in mm (the solver keeps at least 0.05 mm), then penetrating vertices:

| Region | R | L |
|---|---|---|
| palm | 1.75 / 0 | 0.05 / 0 |
| thumb | 0.05 / 0 | 0.37 / 0 |
| index | 0.05 / 0 | 0.40 / 0 |
| middle | 0.15 / 0 | 0.71 / 0 |
| ring | 0.51 / 0 | 0.85 / 0 |
| little | 0.44 / 0 | 0.49 / 0 |
| **whole arm skin** | **0 penetrating** | **0 penetrating** |

- **Palm, all four fingers and the thumb are within 2 mm of the rail on both hands.**
- **The whole take was sampled at all 21 frames:** 0 skin vertices inside the rail, and 0 triangle intersections between the thumb and the fingers or palm, on both hands.
- **Digit/digit skin intersections at the closed pose:**
  - R: 1 triangle pair, index/palm. This is a skin crease fold at the flexed index MCP, not the rail.
  - L: none.
- **The hands were solved independently, not mirrored,** so the angles differ a little (see `grip_anchors.json`).
  - R thumb: CMC 24.9° opposition, IP 55.8°.
  - L thumb: CMC 32.2°, with MP and IP at bind; it is the CMC that brings the pad to the rail.

### Method (`source/grip_solve.py`)

1. **Rail placement:** the rail moves in from the palm side until the bind skin of the palm and fingers first touches it.
2. **Contact-lock closing:**
   - All finger joints advance together (MCP 1.0 : PIP 1.25 : DIP 0.9).
   - A segment that would come closer than 0.05 mm to the rail stops its own joint and every joint proximal to it.
   - The palm skin over each knuckle counts for that finger's MCP.
3. **Seating:** the rail is seated into the palm (proximally and palmward) and the digits re-close, up to three rounds.
4. **Thumb CMC:** picked from an 11 × 13 grid with no rail penetration (thumb and palm) and no thumb/finger triangle intersections, with the pad nearest the rail.
5. **Thumb MP and IP:** close with the same lock, where touching a finger counts as contact.

## Unity 6000.3.28f1 check (isolated project, exact FBX, V04 importer settings)

- **Rig:** `A7_ContinuousBody_Rig` localScale (1, 1, 1) on both clips. This checks the STANDING-IDLE unit trap explicitly.
- **Clip:** length 0.6667 s at 30 fps; 60 bindings, all `m_LocalRotation` on that hand's 15 finger and thumb bones.
- **Closed endpoint:** the clip is sampled at its end and 16 bones per hand (wrist and fingers) are compared with Blender's closed pose in model space. The maximum deviation is **0.001 mm** on both hands.
- **Blender re-import** of both FBX files gives the same, with rig scale 1.

## Not verified

- the presenter integration: wrist and arm alignment onto real rails, transitions, hand-over-hand travel;
- the in-game look;
- the skin in Unity: Unity's bake of the WORK skin was not compared. The bones match to 0.001 mm, on the same bind and skin weights;
- rails other than 15 mm radius.

Made by Claude Opus 5.5 (AI) for Apoliak7777's team. The solver and the evidence are original; the body is the team's existing WORK asset.
