# EVA full-arm fit V1 — FitV02 EVA arms with the approved gloves (EVA-FULL-ARM-FIT-01)

A derivative asset that gives the EVA first-person body full-length arms: shoulder, elbow, wrist, a continuous sleeve and
the approved AstronautHands gloves bound to the forearms, with no floating cut ends. It answers Codex's request in
issue #7 (19:50 UTC, 14 Nov 2026) and follows the plan Codex approved (6883401383 / 6883527455).

- **Author:** the Claude prop chat (Apoliak's machine). Built in Blender 5.3 by `Source/build_eva_arm_fit.py`.
- **Inputs, read-only:** the FitV02 EVA body (`A7_BodyFit02_EVA.fbx`, `915a194`), the V04 floating clip
  (`BODY_FloatNeutral_V04.fbx`, `26d0ee0`), the AstronautHands glove (`f72224c`) and, for the wrench target, the antenna
  kit (`1c57692`) and its motion (`9fea334`).
- **Scope:** only this folder. The Body and Hands sources, the production rig, Core and the scenes are unchanged; this is
  a proposed asset, not an integration.

## What is in the FBX (binding API)

- **Unchanged:** the 164-bone `A7_ContinuousBody_Rig` (bind, names and lengths), `A7_EVA_TorsoLegs` and `A7_EVA_Arm_L/R`
  (meshes, weights and materials).
- **Per side (`R`/`L`):**
  - `GloveWrist_<S>`: an empty, child of the bone `lowerarm02.<S>`. It is the glove's wrist pivot.
  - `<S>_Hand` and the 15 finger parts: the approved glove geometry, unchanged, in the glove rig's hierarchy (`Hand >
    Thumb1-3 / Index1-3 / Middle1-3 / Ring1-3 / Little1-3`), with `<S>_Hand` under `GloveWrist_<S>`.
  - `<S>_WristRing`: the unchanged first 54 mm of the glove's `Arm` part (disconnect ring, lock collar, tabs), child of
    `lowerarm02.<S>`. The rest of `Arm` (the 0.32 m forearm stub with the cut end) is not used.
  - `<S>_AdapterBoot`: new, about 26 mm, `MAT_EVA_FlexGrey`. It bridges the elliptical sleeve end into the round ring.
    Child of `lowerarm02.<S>`.
- **Full path:** `A7_ContinuousBody_Rig/CharacterRoot/root/spine05/spine04/spine03/spine02/spine01/clavicle.R/shoulder01.R/upperarm01.R/upperarm02.R/lowerarm01.R/lowerarm02.R/GloveWrist_R/R_Hand/...`
  (see `binding.json`).
- **Import:** `preserveHierarchy = true`, Generic, *Anim. Compression: Off*. Put the root at the player's feet with zero
  visual offset (the V04 convention).
- **Clip `Scene`, 30 fps, constant keys:** frame 0 base pose (V04), 10 Neutral, 20 RailGrip, 30 WrenchLockStart,
  40 WrenchLocked.
- **Controls:**
  - Wrist flex: rotate `GloveWrist_<S>` locally. The body's `wrist.<S>` stays at bind for EVA, so the sleeve end does not
    move under the ring.
  - Fingers: the glove rig's Neutral, Grip and Press rotations still apply unchanged.
  - Grip on a 34–35 mm handle uses an explicit retarget: each finger chain's flexion is scaled from rest (Thumb 0.2, Index 0.8, Middle 0.9, Ring 0.8, Little 0.7), and
    the grip centre is moved to the palm-pad surface (`binding.json`).

## Fit at the wrist (bind, measured on the meshes)

- **Sleeve end.** Elliptical, about 97 × 69 mm, its end axis 20.2° off the forearm bone line. The glove is aligned to
  this axis, not to the bone: offset 0.0 mm along it and -1.5 mm towards the little finger.
- **Boot.** Its proximal radius follows the sleeve (37–59 mm) with 1.5 mm clearance and an inward lip.
- **Result.** 0 of 98 sleeve vertices under the glove zone can be seen. A ray test (12 radial and 4 distal
  directions per vertex) finds the ring, boot or hand in every direction, on both sides and in every pose below.

## Reach and poses (right hand; real segment lengths, no stretching)

- **Base pose.** V04 at zero offset, with the eye at 1.65 m over the feet root.
  - The right shoulder joint sits at (0.179, −0.239, −0.133) from the eye, that is 0.133 m behind it.
  - Upper arm 0.2424 m, elbow to glove pivot 0.2680 m.
- **Reachable** means an elbow interior angle of at most 150°.
- **Unreachable targets are not stretched to.** Each one is moved towards the eye along the view axis until it is
  reachable, and the table states by how much.
- **Rail.** The stand-in rail of the glove build: 34 mm tube, 0.47 m ahead and 0.205 m below the eye.
- **Wrench.** The technician's eye is 0.40 m from the wall and 0.30 m above the nut (the near-wall stance Codex
  approved).

| Target (frame) | Original glove-pivot target from the eye (x, y, z) | Short with real arms | Moved towards the eye | Elbow | Forearm twist | Clavicle share | Glove wrist turn | Glove pivot error |
|---|---|---|---|---|---|---|---|---|
| Neutral (10) | (0.190, -0.215, 0.400) | 23 mm | 41 mm | 142° | -45° | 20° | 21° | 0.00 mm |
| RailGrip (20) | (0.137, -0.166, 0.386) | 15 mm | 33 mm | 142° | -79° | 20° | 12° | 0.00 mm |
| WrenchLockStart (30) | (0.179, -0.256, 0.273) | reachable | 0 | 105° | -79° | 13° | 17° | 0.00 mm |
| WrenchLocked (40) | (0.181, -0.332, 0.273) | reachable | 0 | 110° | -65° | 10° | 6° | 0.00 mm |

## Contact and intersection checks (per pose, right side)

| Target | Sleeve vertices visible under the glove | Thumb vs sleeve/boot/ring | Glove vs torso | Sleeve vs torso: shoulder seam / limb | Grip |
|---|---|---|---|---|---|
| Neutral | 0 of 98 | 0 | 0 | 149 / 2 | - |
| RailGrip | 0 of 98 | 0 | 0 | 172 / 46 | centre on the axis (0.0 mm); deepest glove vertex 0.75 mm inside the 34.0 mm tube |
| WrenchLockStart | 0 of 98 | 0 | 0 | 89 / 1 | centre on the axis (0.0 mm); deepest glove vertex 2.23 mm inside the 35.0 mm tube |
| WrenchLocked | 0 of 98 | 0 | 0 | 83 / 11 | centre on the axis (0.0 mm); deepest glove vertex 2.23 mm inside the 35.0 mm tube |

- **Shoulder seam.** It counts sleeve triangles within 0.15 m of the shoulder joint. There the FitV02 arm and torso
  renderers overlap by design: the arm's upper boundary sits inside the torso's shoulder cap.
- **Limb.** It counts the rest of the sleeve against the torso.
- **Eye clearance.** The eye stays 0.154 m from the nearest surface, above the 0.05 m near clip.

## Verification actually performed (14 Nov 2026, Apoliak's machine)

- **Blender 5.3.0, Cycles/OptiX.**
  - Full build exit 0, with all measurements above.
  - Eight real renders: first-person from the eye (vertical FOV 68°) and from the side, for each target, with the kit's
    wrench and connector or the stand-in rail in place.
- **Unity 6000.3.28f1, URP** (`evidence/unity_check.json`): batch import of this exact FBX in a private clone with
  `preserveHierarchy`, Generic and compression off gave `EAF_CHECK_PASS`, exit 0.
  - Each `GloveWrist`, `WristRing` and `AdapterBoot` is a child of `lowerarm02.<S>`, and each `Hand` is a child of
    `GloveWrist`.
  - The clip is 30 fps and 1.33 s.
  - At frames 10–40 the glove pivots match the solved targets within 0.058 mm.
  - The upper-arm and elbow-to-pivot lengths stay constant within 0.00045 mm.
  - There is no camera or light.
  - Three first-person captures are in `evidence/`.
- **No local paths.** A byte search finds no path of the build machine in the FBX or the `.blend`. The `.blend` links
  its textures repo-relatively to the existing glove and FitV02 textures; nothing is duplicated.

## Limits and what is not verified

- **Shoulder seam under raised arms.** With the arm raised forward, the FitV02 torso's shoulder cap and the upper sleeve
  interpenetrate. This shows as a jagged ivory/grey edge at the shoulder, including at the bottom corner of the
  first-person view (see the renders).
  - The clavicle share and the elbow pole reduce it but do not remove it.
  - A clean fix needs the FitV02 owner to adjust the torso/arm split or the shoulder weights. It is outside this scope.
- **Reach.** The original Neutral and rail targets are 15–23 mm beyond real reach from the V04 shoulder; the moved
  targets are in the table. Root decides the pose-specific offset or eye lean (Codex 6883527455).
- **Grip retarget.** The approved Grip pose is the glove's original stylised fist. On a real 34–35 mm handle its palm
  and thumb would pass through the tube, so the retarget is explicit and measured. The thumb opens to 0.2 of its flexion.
- **Other.** Kinematic poses only: no hand IK runtime, no collision proxies, no prefab. Unity was checked only with flat
  material colours, and performance is not measured.

## Rights and attribution

| Item | Source | Licence |
|---|---|---|
| Body mesh, rig and sleeves | FitV02 EVA (MakeHuman CC0 core plus the team's garment work), unchanged | As stated in FitV02 `Provenance/` |
| Gloves, wrist ring | AstronautHands (`f72224c`), unchanged geometry | Original team work |
| Adapter boot, poses, retarget, renders | Generated by `Source/build_eva_arm_fit.py` | Original team work |
| Tool | Blender 5.3.0 (Cycles) | GPL tool; its output carries no licence obligation |
