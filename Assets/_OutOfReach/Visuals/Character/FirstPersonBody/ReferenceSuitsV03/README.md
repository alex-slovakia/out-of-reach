# ReferenceSuitsV03 — CHAR-REF-01 (two original male suits on the FitV02 rig)

Branch `feature/reference-suits-v03`, new folder only. Re-dressed FitV02 bodies matching
`docs/art-direction/astronaut-reference-v01/cabin-front-back.png` (WORK) and `eva-front-back.png` (EVA).
Nothing existing was edited (FitV02, FloatNeutralV04, animators, prefabs, scenes, code untouched).

## Files
| File | Content |
|---|---|
| `Meshes/A7_SuitV03_WORK.fbx` | WORK: navy shirt, charcoal cargo trousers (side zip pockets, knee/back-pocket panels), dark low shoes, existing bare hands. 76.5 k tris (Unity count). Renderers `A7_SuitV03_WORK_TorsoLegs`, `_Arm_L`, `_Arm_R`. |
| `Meshes/A7_SuitV03_EVA.fbx` | EVA: off-white layered suit, metal waist/neck/wrist rings, harness straps, red bands, gloves, chest control panel, PLSS, helmet, gold visor, lights. 107 k tris. Renderers `A7_SuitV03_EVA_TorsoLegs`, `_Arm_L`, `_Arm_R` (gloves inside), `_ChestPanel`, `_Plss`, `_Helmet`, `_Visor`. |
| `Materials/MAT_V03_*.mat` | URP Lit materials (22). FBX importer = FitV02's (Generic, external materials, search by name). |
| `Source~/` | Blender 5.3 scripts, `.blend` sources, Unity check script (`~` = Unity ignores it). |
| `Previews~/` | `compare_work.png`, `compare_eva.png` (reference left, model front/back right), `v_q3.png`. |
| `Provenance/` | build reports, Blender and Unity rig checks, license note. |

## Rig contract (identical to FitV02)
- 164 bones, same names/parents, single root `CharacterRoot`. Bone-name hash (sha256 of names joined by `\n`, first 16 hex) `459a989457d9af1e`.
- Bone heads/tails equal to FitV02 (max 0.7 micrometre), bone matrices max 9e-5. Unity: skinned `bindposes` equal to FitV02's by bone name (max element diff 9.8e-5), 164 transforms under `CharacterRoot`, rig local scale (1,1,1).
- Export: `apply_unit_scale=True`, `FBX_SCALE_UNITS`, `-Z`/`Y`, no leaf bones, no animation clips inside (use FitV02 / FloatNeutralV04 clips on this rig). Importer settings copied from FitV02 minus its clip list.
- **Note:** FitV02 FBX files store a posed node state (their static node transforms differ from the bind pose); these files store the **bind pose**. Skin binding is identical; drive the pose with your clips as before (Animator root motion off).
- Every vertex has at most 4 influences, weights sum to 1. All parts are skinned (rigid parts 100 % to `spine02` or `head`); no bone-parented objects, so `preserveHierarchy` is not needed.

## Integration notes for root
- First person: hide `_Helmet`, `_Visor`, `_Plss` (and `_ChestPanel` if it blocks the view) by disabling those renderers. Arms: disable `_Arm_L/_R` renderers as before when camera-owned gloves are used.
- EVA: the PLSS reaches 0.245 m behind the FitV02 back (bbox in `Provenance/build-report-EVA.json`: y up to +0.375 m); helmet 0.456 x 0.400 x 0.390 m, top z 1.895 m. The chest panel protrudes 0.07 m forward of the chest.
- Materials: solid colours (+ weave normal on cloth). Red bands are raised strips in the TorsoLegs/Arm renderers (material `MAT_V03_EvaRed`).

## Known limits (honest)
- No head in WORK (FitV02 has none); a skin neck ends at z 1.512. The EVA helmet is a shell with a dark interior front and a gold visor: no head inside.
- Trouser legs are close to but a little less straight than the reference; detail is geometric (panels, hems, straps) with no baked wear textures or decals.
- Verified: Blender rig compare, Unity 6000.3.28f1 batch import of both FBX in an isolated project (no URP there, so materials load without the shader: names resolve). Not verified: URP look in the game scene, animation clips on these meshes, player/collision fit.
