# AntennaRepair motion V1 — 3-second repair motion (ANTENNA-REPAIR-MOTION-01)

This folder extends the antenna repair kit in `../antenna-repair-v01/` (`1c57692`) with a physically coherent,
3-second repair motion for the service wrench and the damaged plug. It answers item 1 of Codex's note in issue #7
(19:01 UTC, 14 Nov 2026).

- **Author:** the Claude prop chat (Apoliak's machine). Built in Blender 5.3 by one script from the kit's own `.blend`.
- **Scope:** only this folder. The kit, the ship, the antenna, the scenes and the code are unchanged.
- **Not included:** hands (the wrench pose is the palm target for your glove rig), physics, sparks or welding.

## Files

| Path | What |
|---|---|
| `AntennaRepairMotion.fbx` | Five objects, six anchor empties and one clip (91 frames at 30 fps, 0–3.0 s). `ServiceWrench` is the kit's wrench mesh with its pivot on `WrenchGrip`. `DamagedNut`, `DamagedPlugBody`, `DamagedCable` and `DamagedScorch` are `ConnectorDamaged` split into its moving parts with the same geometry, UVs and `AR_Kit` material. `DamagedCable` is skinned and has one blend shape, `Seated`. Anchors: `ConnectorMount`, `PlugGrip`, `PlugGripBackedOut`, `CableGrommet`, `JawEntry` and `JawExit`. No texture records and no paths from the build machine. |
| `AntennaRepairMotion.blend` | Editable source with the keyed action. Its textures link to `../antenna-repair-v01/textures/`, and the local paths were blanked. |
| `motion-samples.json` / `.csv` | Every frame in Unity metres: each object's pivot position, its delta rotation (Unity quaternion x, y, z, w), the wrench tip (`WrenchTip`), jaw and handle axes, and the cable blend weight. |
| `motion-report.json` | Per-frame measurements: mesh overlaps and clearances between the wrench and every other part, the jaw centre, the nut centre and lock angle. Also the grip contact search, the cable data and the frame-0 and frame-69 matches. |
| `renders/` | Real Cycles frames: ten key frames `fNN-*.png` (wide view), three close-ups `close-fNN-*.png`, `after-swap-f90.png`, `contact-sheet.png` and `repair-motion.mp4` (all 91 frames, 960 × 600). The grey wall is a render prop. |
| `Source/` | `build_antenna_repair_motion.py` (rebuilds everything above from `../antenna-repair-v01/AntennaRepair.blend`), `verify_motion_fbx.py` and `make_csv.py`. |
| `evidence/` | The Blender FBX re-import result, the Unity clip check and four Unity captures. |

Rebuild (about 4 min on an RTX 4070):
`blender -b --factory-startup --python Source/build_antenna_repair_motion.py -- --kit ../antenna-repair-v01/AntennaRepair.blend --out <folder outside Assets> --stage full`.

## Timeline (30 fps)

| Phase | Frames | Seconds | What happens |
|---|---|---|---|
| `inspect` | 0–9 | 0.00–0.30 | damaged plug backed out 12 mm and turned 24 deg; wrench ready beside the cable, jaw axis already on the connector axis |
| `approach` | 9–18 | 0.30–0.60 | jaw moves level with the stub jacket behind the melted boot, its 34 mm opening facing the 17 mm cable |
| `hook` | 18–24 | 0.60–0.80 | jaw slides sideways onto the stub jacket at JawEntry |
| `slide` | 24–36 | 0.80–1.20 | jaw slides along the axis over boot and backshell onto the coupling nut (concentric, 1.5 mm radial clearance) |
| `grip` | 36–40 | 1.20–1.33 | jaw takes up the clearance; its rubber heel bears on the nut ridges (within 0.03 mm, measured on the meshes) |
| `seat` | 40–54 | 1.33–1.80 | wrench, nut and plug body move +12 mm along +Z; the cable follows through the Seated blend shape |
| `lock` | 54–69 | 1.80–2.30 | wrench and nut turn 24 deg clockwise as seen by the technician about the connector axis; marks line up |
| `release` | 69–72 | 2.30–2.40 | jaw centres again, clear of the nut |
| `back_off` | 72–81 | 2.40–2.70 | jaw slides back along the axis to JawExit on the seated stub jacket |
| `unhook` | 81–90 | 2.70–3.00 | jaw slides off the cable through its opening along the handle direction; tool clear |

## How to use it

- **Swap in the parts.** During the motion, show `DamagedNut`, `DamagedPlugBody`, `DamagedCable` and `DamagedScorch` in
  place of the kit's `ConnectorDamaged`. At frame 0 they reproduce it to < 0.0001 mm (measured vertex to vertex).
- **Drive the clip.** Either play the clip, or drive the transforms from `motion-samples.csv`:
  - position = the sampled pivot in kit space;
  - rotation(f) = delta(f) × the object's rotation at import;
  - the wrench's delta is relative to its pose in the kit FBX, and the nut's and plug body's to frame 0.
- **Palm target.** The `ServiceWrench` transform (pivot `WrenchGrip`, grip axis = handle axis in the samples) is where
  the glove holds the tool.
- **Swap to repaired.**
  - From frame 69, the nut and backshell are exactly in `ConnectorRepaired`'s pose.
  - After frame 90, with the tool clear, hide the four parts and show `ConnectorRepaired`.
  - The swap stands for the cable re-termination, cleaning and safety wire, which this motion does not show. Do it
    under your own transition, not in the middle of the motion.
- **Import setting.** Use the FBX with *Anim. Compression: Off*. Unity's default keyframe reduction moves the samples
  by the amounts listed under Verification.

## Physical facts (measured on the meshes)

- **Engage and disengage.** The C-jaw's 34.4 mm opening passes over the 17 mm stub jacket at `JawEntry` (Z −0.152,
  backed-out state) and leaves it at `JawExit` (Z −0.140, seated state), between the boot and the frayed strands.
  Along the plug the jaw (0.060 m inside diameter) clears the boot, the backshell and the nut (0.057 m).
- **Grip.** The jaw moves 1.44 mm off-centre, away from the handle, so that its rubber heel bears on the nut ridges.
  - The faceted jaw and nut first intersect at 1.47 mm, so the heel touches within 0.03 mm.
  - The nearest vertex-to-surface gap there is 0.33 mm.
  - The wrench and the nut then turn together, with no slip.
- **Seat and lock.**
  - Seat: the nut, the plug body, the wrench and the start of the cable move 12.0 mm along +Z.
  - Lock: the nut and the wrench turn 24.0° about the connector axis, clockwise as seen by the technician (Unity −24°
    about +Z). The nut centre stays on the axis.
  - The handle sweeps 24°, which moves the grip 74 mm down and to the right.
- **No interpenetration from the motion.** In all 91 frames, the wrench has 0 overlapping triangles with the receptacle,
  nut, plug body, cable and scorch. Smallest clearances: receptacle 7.7 mm, nut 0.33 mm, plug body 2.7 mm, cable 48.0 mm, scorch 31.7 mm (the nut value is the grip contact).
- **Joins inherited from the kit.** These stay hidden and do not change with the motion:
  - the cable end sits in the grommet hole (36 touching triangles at frame 0);
  - the cable start sits inside the torn jacket (113 at frame 0, constant);
  - the kit's closed coupling nut has its rear face inside the receptacle barrel once seated (frames 48–90), exactly
    as in `ConnectorRepaired`.
- **Cable.**
  - `Seated` moves the cable's plug end by 12.0 mm and leaves the grommet end fixed (0.0 mm).
  - The loop bulges outwards by up to 8.0 mm, so the centreline keeps its length (0.411 m, change 0.00 mm).
    A plain pull towards the wall would have shortened it by 8.7 mm.
- **Hand path.** `WrenchGrip` travels 0.48 m in 3.0 s; its peak speed is 0.47 m/s.

## Verification actually performed (14 Nov 2026, Apoliak's machine)

- **Blender 5.3.0, Cycles/OptiX.** Full build exit 0. The split classified all 48 islands of `ConnectorDamaged` (nut 2,
  plug body 42, cable 3, scorch 1). The frame-0 and frame-69 matches: < 0.0001 mm and < 0.0001 mm (vertex to vertex), and
  0.10 mm for the backshell (vertex to surface, see Limits).
- **FBX re-import** (`evidence/fbx_reimport.json`). The FBX was re-imported into an empty Blender scene at 30 fps.
  - Objects: `CableGrommet`, `ConnectorMount`, `DamagedCable`, `DamagedNut`, `DamagedPlugBody`, `DamagedScorch`, `JawEntry`, `JawExit`, `PlugGrip`, `PlugGripBackedOut`, `ServiceWrench`. `DamagedCable` keeps the blend shape `Seated`, and its weight is animated (yes). Objects that move: `DamagedNut`, `DamagedPlugBody`, `ServiceWrench`; the exporter also gives the static objects constant curves. Compared with the samples at all 91 frames: worst position error 0.0067 mm (ServiceWrench f12), worst blend weight error 0.00000. 4,214 triangles, no cameras or lights, no drive-letter path.
- **Unity 6000.3.28f1, URP.** A batch import in a private clone of the repository (`evidence/unity_clip_check.json`)
  gave `ARM_CHECK_PASS`, exit 0, no compile error.
  - The clip was sampled at all 91 frames and compared with `motion-samples.csv`.
  - With *Anim. Compression: Off*: position error 0.0067 mm, rotation error 0.000°, wrench tip error 0.0067 mm, blend weight error 0.00004 (of 100). Clip `Scene`, 3 s at 30 fps.
  - With Unity's default import (keyframe reduction): position error 1.0247 mm, rotation error 0.354°, wrench tip error 1.1605 mm, blend weight error 0.23756 (of 100). Clip `Scene`, 3 s at 30 fps.
  - Four captures used URP Lit materials built from the kit maps: `evidence/ARM_unity_f00_inspect.png`,
    `ARM_unity_f40_gripped.png`, `ARM_unity_f69_locked.png` and `ARM_unity_f90_after_swap.png`.
- **No local paths.** A byte search finds no path of the build machine in the FBX or the `.blend`.

## Limits and what is not verified

- **Kinematic, not simulated.** Forces, friction and cable stiffness are not computed. The grip is a measured contact,
  not a friction model.
- **No hands.** The glove rig is not posed, so the reach and the arm fit are not checked.
- **Cable.** `Seated` is one blend shape, blended linearly. The length is kept at the two ends of the blend; in
  between, the cable is not simulated.
- **Backshell tessellation.** The damaged backshell's tessellation sits 24° turned against the repaired one. This is
  an invisible modelling detail, so it is compared surface to surface.
- **Not integrated.** No prefab, no colliders and no placement on the ship. Performance is not measured.

## Rights and attribution

| Item | Source | Licence |
|---|---|---|
| Geometry, UVs and animation | Derived from the team's own kit `../antenna-repair-v01/` by `Source/build_antenna_repair_motion.py`. No external model, texture, motion capture or scan. | Original team work |
| Tool | Blender 5.3.0 (Cycles) | GPL tool; its output carries no licence obligation |

No NASA model, texture, drawing, photograph, logo or insignia is used. The connector and the wrench are generic
fictional hardware.
