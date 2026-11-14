# STANDING-IDLE-01 — a planted-feet standing idle for the 164-bone WORK / EVA rig

**Status:** an animation-only package for review, by the Claude model writer on 14 November 2026 (#7, Codex 6884483742; claim 6884509033).
- **Pose:** the idle recommended by WORK-LOOKDOWN-STUDY-01. Knees about 11°, hips 40 mm back, feet planted.
- **Untouched:** V04 floating, the clasp, the glove meshes, every mesh, prefab and runtime file.
- **Not checked in Unity:** root imports it after a native check.

![Standing idle evidence](renders/STANDING_IDLE_sheet.png)

How to read the sheet:
- **Rows,** top to bottom:
  1. the current WORK `BODY_StandRelaxed` at 0.33 m;
  2. the same at 0.197 m;
  3. WORK with the new idle at 0.33 m;
  4. WORK with the new idle at 0.197 m;
  5. EVA with the new idle at 0.33 m;
  6. EVA with the new idle at 0.197 m.
- **Columns:**
  1. the eye view (1.65 m, 80° down, vertical FOV 68°, 16:9);
  2. the region mask (red torso, yellow hips, blue legs, green boots);
  3. the side view, with the eye and the view's rays.

## The clip

- **File:** `BODY_StandIdle_V01.fbx`. It holds the armature only (`A7_ContinuousBody_Rig`, 164 bones) and one take, `BODY_StandIdle_V01`.
- **Timing:** 30 fps, frames 1–31 after import, like the published takes. The pose is held, and all 164 bones are keyed.
- **Bind unchanged:** the bone nodes are written from the original bind. The exporter takes them from a rest key outside the take, so no posed rest skeleton is baked.
  - A first export without that rest key did bake the pose into the rest: hands 457 mm off, toes 9° off. It was caught by the check below and replaced.
- **Import:** use `preserveHierarchy = true`, as root found for V04.

**How the pose was built:**
1. Start from the reviewed `BODY_StandRelaxed`, frame 1, which keeps its upper body and low arms.
   - Its knees are already at 10.1°, so the main change is the hips.
2. Move the root 40 mm back.
   - The knees then reach 11.4° without any vertical shift; the bisection towards the 11° target ended at 0 mm down.
3. Each leg reaches its unchanged StandRelaxed foot by IK (chain of 4, no pole), and the foot keeps its rotation.
4. Bake the visual result into plain keys, then remove the constraints.

**Measured pose:**
- Foot drift 0.0 mm and foot rotation change 0.0° on both sides.
- Root shift (0, 40, 0) mm in Blender axes, i.e. backwards.
- Knees 11.4° on both sides.

## Checks (fresh Blender re-import, `idle_fbx_check.json`)

| Check | Result |
|---|---|
| Bones, names, parents | 164 / 164, equal, equal |
| Bind matrices against the published WORK body | max difference 7.4·10⁻⁵ (float precision) |
| Bone lengths | max difference 0.0005 mm |
| The clip on the published WORK rig against the authored pose | max joint deviation 0.0009 mm |
| Take name and range | `BODY_StandIdle_V01`, frames 1–31 |

**Unity joint expectations:** `idle_joint_expectations_unity.json` lists every bone's posed head and tail.
- The values are in root's verified Unity model basis (−x, z, −y), with a zero visual root and a 2 mm tolerance.
- This matches the style of root's `independent-Blender-bone-expectations.json`, so a native check can compare against it directly.

## Look-down evidence (`standing_idle_report.json`)

**Setup:** the same eye for every case, 1.65 m above the feet root, 80° down. Only TorsoLegs is shown (arms off, as with the camera hands).
- 0.33 m is root's existing standing offset (`ConfigureScene`, `(0, 0, −0.330)`).
- 0.197 m is the body author's anatomical-eye setback, the one the study recommended.

| Outfit, pose | Setback | Boot vertices visible | Boots % | Torso % | Legs % | Eye to nearest skin | Knees |
|---|---|---|---|---|---|---|---|
| WORK, StandRelaxed (current) | 0.33 m | 1,751 / 2,195 | 0.91 | 3.81 | 0.63 | 269 mm | 10.1° |
| WORK, StandRelaxed (current) | 0.197 m | 464 / 2,195 | 0.22 | 15.06 | 0.00 | 163 mm | 10.1° |
| **WORK, StandIdle_V01** | 0.33 m | 1,829 / 2,195 | 0.94 | 2.48 | 1.14 | 305 mm | 11.4° |
| **WORK, StandIdle_V01** | 0.197 m | 1,370 / 2,195 | 0.76 | 9.37 | 0.08 | 191 mm | 11.4° |
| **EVA, StandIdle_V01** | 0.33 m | 1,814 / 2,215 | 0.98 | 2.93 | 1.10 | 305 mm | 11.4° |
| **EVA, StandIdle_V01** | 0.197 m | 1,042 / 2,215 | 0.67 | 10.87 | 0.00 | 188 mm | 11.4° |

**Reading the results:**
- At 0.197 m, the idle shows about three times as many boot vertices as the current standing pose (1,370 against 464) and less shirt (9.4 % against 15.1 %).
- At 0.33 m, both poses already show the boots; the idle adds a little and trims the shirt.
- The nearest skin stays at least 188 mm from the eye in every case, well beyond the 0.05 m near clip.
- The boots stay a small share of the screen because they sit at the bottom edge of an 80°-down view.

**Limits:**
- This is a standing pose only. Floating keeps V04, and its validated contact frame is not touched.
- The eye and the player capsule never move. The setback is the body wrapper's presentation offset, and root decides between 0.33 m and 0.197 m.
- Not verified: Unity import, the clip in a native player, the arms in these views, and the dock scene.

## Files

| File | What it is |
|---|---|
| `BODY_StandIdle_V01.fbx` | The animation-only clip (armature with the original bind, one take) |
| `idle_fbx_check.json` | The re-import check |
| `idle_joint_expectations_unity.json` | Posed head and tail of every bone, in the Unity model basis |
| `standing_idle_report.json` | Authoring values and the look-down evidence for every case |
| `renders/` | 18 views and the sheet |
| `source/standing_idle.py` | Authoring, export and evidence |
| `source/verify_idle.py` | The re-import check |
| `source/lookdown.py` | The shared camera, region and IK helpers |

**Rebuild:**

```
blender -b --factory-startup --python source/standing_idle.py -- --body <A7_BodyFit02_WORK.fbx> --eva <A7_BodyFit02_EVA.fbx> --v04 <BODY_FloatNeutral_V04.fbx> --out <dir>
blender -b --factory-startup --python source/verify_idle.py -- --body <A7_BodyFit02_WORK.fbx> --idle <dir>/BODY_StandIdle_V01.fbx --pose <dir>/idle_pose_bone_heads_tails.json --out <dir>
```

**Provenance:** the WORK and EVA bodies are the team's published assets (MakeHuman CC0 base; licences in `FitV02/Provenance/`). They are read only and not copied here; the package holds only the armature and the clip.
