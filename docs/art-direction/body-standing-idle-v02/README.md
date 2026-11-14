# STANDING-IDLE-UNIT-02 — the approved standing idle, re-exported in V04's units

**Status:** a corrected animation-only package for root's native review, by a Claude body unit-fix chat on 14 November 2026 (#7, Codex 6884896538; claim 6885013471).
- **Pose:** unchanged. It is the STANDING-IDLE-01 idle (knees 11.4°, hips 40 mm back, feet planted).
- **Untouched:** v01 (`body-standing-idle-v01/**`), V04 floating, the clasp, the glove meshes, every mesh, prefab, scene and runtime file.
- **Checked in Unity 6000.3.28f1:** in an isolated throw-away project, not in the game's Bootstrap fixture. Root reruns its own capture.

![Unity native evidence](renders/UNITY_native_sheet.png)

## What was wrong and what changed

**Cause:** v01 was exported with Blender's default `apply_scale_options='FBX_SCALE_NONE'`.
- That writes centimetre FBX units (`UnitScaleFactor` 1) and scales the armature node ×100.
- Unity converts only the translations (file scale 0.01). The rig keeps localScale 100, and every bone position key comes out ÷100.
- Bone heads still land in the right place (100 × 0.01 = 1). The skin does not: each bone matrix carries the ×100 scale, so the mesh blows up around its bones.

**Fix:** the same script exports with `apply_scale_options='FBX_SCALE_ALL'`.
- That gives metre units (`UnitScaleFactor` 100) and a unit rig scale, the representation of the published WORK body and of V04.
- The pose, the rest bind, the 164 bone names and lengths, the hierarchy and the timing are the same as v01.
- The file and take are now `BODY_StandIdle_V02`, so the two files cannot be mixed up. If root's fixture names `BODY_StandIdle_V01`, it changes that reference.
- `source/standing_idle.py` differs from v01 only in the docstring, the clip name and this one export argument.

**No workaround:** there is no importer compensation, no mesh rescale and no larger bounds.

## Raw FBX nodes (`idle_fbx_check.json` → `units`)

Read with Blender's own FBX parser, without importing.

| | v01 (`b79a7c7`) | **V02 (this)** | V04 | WORK body |
|---|---|---|---|---|
| `UnitScaleFactor` | 1 (cm) | **100 (m)** | 100 | 100 |
| Rig `Lcl Scaling`, static and keyed | 100 | **1, on all 31 keys** | 1 | 1 |
| Rig `Lcl Rotation` | −90° X | **−90° X** (equal to V04 within 1·10⁻⁴°) | −90° X | −90° X |
| Keyed `root` translation | (0, 0.9356, −0.1210) in cm units | **(0, 0.9356452, −0.1210442) m** | — | — |

## Unity 6000.3.28f1, native (`unity_check.json`, `unity_skin_check.json`)

**Setup:**
- An isolated project holds the canonical `A7_BodyFit02_WORK.fbx` and `A7_BodyFit02_EVA.fbx` with their committed `.meta` files, V04 with its `.meta`, and the v01 and V02 clips.
- Both clips use V04's exact importer settings, with only the guid and the take name changed (`source/unity_check_project/BODY_StandIdle_V02.fbx.meta`).
- Each clip is sampled with `AnimationClip.SampleAnimation` on a fresh instance of each body at the origin. Every `SkinnedMeshRenderer` is then baked with `BakeMesh`.

**Importer and keys:**

| | v01 | **V02** | V04 / StandRelaxed |
|---|---|---|---|
| `ModelImporter.fileScale` | 0.01 | **1** | 1 |
| Rig `m_LocalScale` keys | 100 | **1 (31 keys)** | 1 |
| `root` `m_LocalPosition` y, z | 0.009356452, −0.001210442 | **0.9356452, −0.1210442** | 0.9356453, −0.08104417 |
| `upperleg01.L` `m_LocalPosition` y | 0.001203359 | **0.1203359** | 0.1203359 |
| Sampled rig localScale, root lossyScale | 100, 100 | **1, 1** | 1, 1 |

- **Held pose:** the first and the last frame are identical (0 m joint difference).
- **Joints:** all 164 sampled bone heads are within 0.0014 mm of `idle_joint_expectations_unity.json`.
  - v01's heads also match (0.0013 mm). That is why the head-only check missed the bug.

**Full skin, every vertex.**
- **Reference:** Blender's own armature deform of the same canonical meshes (`source/skin_reference.py`). It uses the V02 file re-imported from disk, and the published StandRelaxed take as a control.
- **Vertex mapping:** each Unity vertex is matched to its Blender vertex through the bind mesh (max 0.0003 mm). Every Blender vertex is covered.

| Outfit, skin (Unity vertices) | StandRelaxed, control: max / mean | **V02: max / mean** | V02 vertices > 2 mm | v01: max |
|---|---|---|---|---|
| WORK TorsoLegs (11,583) | 0.118 / 0.0005 mm | **0.118 / 0.0006 mm** | **0** | 35.7 m |
| WORK Arm_L, Arm_R (8,310 each) | 0.067 / 0.001 mm | **0.067 / 0.001 mm** | **0** | 14.5 m |
| EVA TorsoLegs (12,093) | 0.122 / 0.0005 mm | **0.122 / 0.0006 mm** | **0** | 36.5 m |
| EVA Arm_L, Arm_R (990 each) | 0.075 / 0.001 mm | **0.075 / 0.001 mm** | **0** | 15.2 m |

**Bounds and feet:**

| | WORK TorsoLegs, world size | EVA TorsoLegs, world size | Lowest point (both) |
|---|---|---|---|
| Published StandRelaxed | 0.567 × 1.545 × 0.367 m | 0.589 × 1.555 × 0.378 m | −1.511 mm |
| **V02** | **0.572 × 1.545 × 0.400 m** | **0.593 × 1.555 × 0.410 m** | **−1.511 mm** |
| v01 | 36.3 × 49.8 × 39.7 m | 38.6 × 51.5 × 40.7 m | −35.1 m (WORK), −35.9 m (EVA) |

- **Against Blender:** every V02 skin's bounds match Blender's within 0.001 mm.
- **Depth:** V02 is about 32 mm deeper than StandRelaxed because the hips sit 40 mm back.
- **Feet:** the lowest point is the published take's own sole, 1.5 mm below the feet root, on both outfits.
- **Baked local extents** (the baked mesh's own bounds, half sizes, z up) for WORK TorsoLegs:
  - V02 (0.286, 0.200, 0.772) m;
  - StandRelaxed (0.284, 0.184, 0.772) m;
  - v01 (18.1, 19.8, 24.9) m.
  - Root's fixture reported other absolute values for this measure ((30.36, 30.94, 43.37) against (0.33, 0.31, 0.84)), so the two definitions differ. Both show the same failure: tens of metres instead of less than one metre.

## Same-eye look-down

**Setup:** as in STANDING-IDLE-01, with Unity's eye at (0, 1.65, setback):
- the eye 1.65 m above the feet root, 80° down, vertical FOV 68°, 16:9, near clip 0.05 m;
- setbacks 0.33 m (root's current standing offset) and 0.197 m (recommended);
- TorsoLegs only, as with the camera hands.

**Two measurements:**
- **Unity:** measured on Unity's own baked triangles (`compare_skin.py`).
- **Blender:** the unchanged STANDING-IDLE-01 method (`standing_idle_report.json`, sheet below).

| Outfit, pose | Setback | Unity: boots visible | Unity: eye to nearest skin | Blender: boots visible | Blender: eye to skin |
|---|---|---|---|---|---|
| WORK, StandRelaxed (published) | 0.33 m | 1,750 / 2,195 | 269.0 mm | 1,751 / 2,195 | 269.0 mm |
| WORK, StandRelaxed (published) | 0.197 m | 464 / 2,195 | 162.7 mm | 464 / 2,195 | 162.7 mm |
| **WORK, V02** | 0.33 m | **1,827 / 2,195** | **305.1 mm** | 1,829 / 2,195 | 305.1 mm |
| **WORK, V02** | 0.197 m | **1,372 / 2,195** | **191.2 mm** | 1,370 / 2,195 | 191.2 mm |
| EVA, StandRelaxed (published) | 0.33 m | 1,640 / 2,215 | 268.4 mm | — | — |
| EVA, StandRelaxed (published) | 0.197 m | 122 / 2,215 | 156.2 mm | — | — |
| **EVA, V02** | 0.33 m | **1,814 / 2,215** | **304.9 mm** | 1,814 / 2,215 | 304.9 mm |
| **EVA, V02** | 0.197 m | **1,042 / 2,215** | **187.5 mm** | 1,042 / 2,215 | 187.5 mm |

- **Unity against Blender:** the counts differ by at most 2 vertices. That comes from assigning regions to Unity's re-split triangles.
- **Coverage:** all boot vertices are inside the view in every case.
- **Clearance:** with V02 the nearest skin is at least 187 mm from the eye, well beyond the 0.05 m near clip.

![Blender evidence](renders/STANDING_IDLE_sheet.png)

## Files

| File | What it is |
|---|---|
| `BODY_StandIdle_V02.fbx` | The animation-only clip: armature `A7_ContinuousBody_Rig` (164 bones, original bind), one take `BODY_StandIdle_V02`, 30 fps, frames 1–31 after import |
| `idle_fbx_check.json` | Blender re-import check and the raw FBX unit check against V04 and the WORK body |
| `idle_joint_expectations_unity.json` | Posed head and tail of every bone, Unity model basis; equal to v01's within 0.0014 mm |
| `standing_idle_report.json` | Authoring values and the Blender look-down evidence |
| `unity_check.json` | Raw output of the native Unity check (importers, clip keys, sampled rig, skins) |
| `unity_skin_check.json` | Per-vertex skin, bounds, joints and look-down results, with pass flags per outfit |
| `renders/` | 18 Blender views, 14 Unity captures (`*_unity.png`) and the two sheets |
| `source/standing_idle.py`, `source/lookdown.py` | Authoring, export and Blender evidence (lookdown is v01's, unchanged) |
| `source/verify_idle.py` | Blender re-import check plus the raw FBX unit check |
| `source/skin_reference.py`, `source/compare_skin.py` | Blender reference skin and the Unity-against-Blender comparison |
| `source/StandingIdleUnitCheck.cs`, `source/unity_check_project/` | The Unity editor check, the project manifest and the clip's importer `.meta` |
| `source/make_sheets.py` | The two evidence sheets |

**Pass flags** (`unity_skin_check.json` → `outfits.*.checks`), all `true` for WORK and EVA:
- rig scale 1 and root in metres;
- every skin vertex within 2 mm of Blender;
- V02's error no higher than the published take's;
- bounds within 1 mm;
- joints within 2 mm;
- feet equal to the published take;
- v01 reproduces root's finding.

## Rebuild

```
blender -b --factory-startup --python source/standing_idle.py -- --body <A7_BodyFit02_WORK.fbx> --eva <A7_BodyFit02_EVA.fbx> --v04 <BODY_FloatNeutral_V04.fbx> --out <dir>
blender -b --factory-startup --python source/verify_idle.py -- --body <WORK fbx> --v04 <V04 fbx> --idle <dir>/BODY_StandIdle_V02.fbx --pose <dir>/idle_pose_bone_heads_tails.json --expect <v01 idle_joint_expectations_unity.json> --out <dir>
blender -b --factory-startup --python source/skin_reference.py -- --body <WORK fbx> --eva <EVA fbx> --v04 <V04 fbx> --idle <dir>/BODY_StandIdle_V02.fbx --out <ref>
Unity -batchmode -projectPath <isolated project> -executeMethod StandingIdleUnitCheck.Run -logFile <log>   (UNITCHECK_OUT=<unity out>)
blender -b --factory-startup --python source/compare_skin.py -- --unity <unity out> --ref <ref> --expect idle_joint_expectations_unity.json --out <dir>
python source/make_sheets.py <this folder>
```

**Run times on this PC:**
- Authoring and Blender evidence: 44 s.
- Unity check: 82 s, including the project's first import.
- Comparison: 11 s.

## Limits

**Not verified:**
- the game's own Bootstrap fixture and its URP materials (the captures use flat Standard materials in a built-in-pipeline project);
- the arms in the eye views (TorsoLegs only, as in v01; the arms are checked vertex by vertex above);
- a playtest.

**Unchanged scope:** this is a standing pose only. Floating keeps V04, and its validated contact frame is not touched. The eye and the player capsule never move; root chooses between 0.33 m and 0.197 m.

**Provenance:** the WORK and EVA bodies are the team's published assets (MakeHuman CC0 base; licences in `FitV02/Provenance/`). They are read only and not copied here.
