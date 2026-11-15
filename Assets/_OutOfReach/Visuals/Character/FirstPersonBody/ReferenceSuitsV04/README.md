# ReferenceSuitsV04 — WORK suit, alternative to V03 (SUITS-V04)

Alternative attempt for the 18:30 CET suit decision (#7 claim 6895641830). **WORK only**; EVA was not attempted in V04.
Nothing outside this folder is changed: no FitV02, V03, presenter, scene or prefab edit.

## What it is

`Meshes/A7_SuitV04_WORK.fbx` (30,832 triangles + 13,580 for the third-person head) on the **unchanged FitV02 rig**
`A7_ContinuousBody_Rig` (164 bones, same names, parents and bind: max difference 7e-7 m after a Blender round trip):

| Object | Triangles | Content |
|---|---:|---|
| `A7_WORK_TorsoLegs` | 8,500 | male torso and legs, navy knit shirt with rib hem, charcoal twill cargo trousers (side zip pockets with zips, knee seams, back pockets), dark low shoes |
| `A7_WORK_Arm_L` / `A7_WORK_Arm_R` | 4,376 each | sleeves with rib cuffs, bare hands (`MAT_Cabin_BareHands` submesh, like FitV02) |
| `A7_WORK_Head` | 13,580 | head, hair, eyes — **third person only**; hide it in first person (FitV02 has no head) |

Takes `BODY_StandRelaxed`, `BODY_FloatNeutral`, `BODY_SeatedFit` are re-exported unchanged from FitV02 (same take names
`A7_ContinuousBody_Rig|BODY_*`, clips renamed `BODY_*` in the `.meta` like FitV02's).

## How it fits the rig without moving it

The FitV02 bind is exactly MakeHuman's default skeleton on the neutral MakeHuman base mesh (max joint difference 2e-7 m).
So the new body is the CC0 base mesh with CC0 male targets, skinned with MakeHuman's own CC0 default weights (no weight
transfer), and anchored to the rig's joints. **Arms and hands keep the exact base geometry** (hand joints 0.0 m off), so
grip and contact calibration stay valid; the male shape fades in at the shoulders. Residual torso joint offset after the
male morph: up to 6.4 cm at the neck base and 4 cm at the clavicles (the torso surface follows the rig; no bone moved).
Garments are MakeHuman CC0 clothes fitted through their `.mhclo` (original folds via the normal map), repainted navy knit
and charcoal twill; trousers widen slightly below the knee (relaxed straight). Body verts under clothes are removed as
MakeHuman does, which prevents poke-through.

## Unity (checked in an isolated batch project, Unity 6000.3.28f1)

Importer as FitV02: Generic, avatar from this model, animation compression Off, scale 1, Use File Scale, no axis bake,
clips renamed `BODY_*`, materials via description remapped to `Materials/*.mat` (URP Lit). Result
`Source~/unity_import_check.txt`: rig lossy scale 1, 164 bone transforms, four skins with 164 bones each, materials bound,
three non-legacy Generic clips, baked bind bounds in metres (feet 0.0, neck 1.60, head top 1.79).

Integration (root): add a replacement `AvatarPresentationProfile` WORK outfit pointing at this FBX (roles
`A7_WORK_TorsoLegs`, `A7_WORK_Arm_L`, `A7_WORK_Arm_R`; palm submesh = `MAT_Cabin_BareHands`), keep `A7_WORK_Head`
disabled in first person. The compatibility generator expects unique names: they are unique here.

## Evidence

- `Previews~/work_final_front_back.png` — front/back next to the approved reference (Blender Cycles, soft light).
- `Previews~/work_final_lookdown.png` — first-person look-down preview (eye 1.60 m, leaned 0.32 m forward, 72° down, vFOV 68).
- `Source~/blender_rig_verify.json` — Blender re-import of the FBX vs FitV02 (164 bones, names, parents, bind, scale 1).
- `Source~/unity_import_check.txt` — Unity batch import result.
- `Meshes/A7_SuitV04_WORK_report.json` — triangles, materials, vertex groups per object.

## Rebuild

Inputs (CC0, see `Provenance/MakeHumanCC0-sources.json`): MakeHuman `base.obj`, `default.mhskel`, `default_weights.mhw`,
male targets and the system-assets pack. `python -I Source~/make_textures.py <inputs> <tex>`, then
`blender -b --factory-startup --python Source~/build_v04.py -- --render <prefix> --export <dir>` (Blender 5.3).

## Limits (honest)

- The face is MakeHuman's generic young male, not the reference actor; it is only for third person.
- Shirt and trousers come from one MakeHuman garment (folds from its normal map); the reference's raglan seams and the
  exact cargo pocket shape are approximated by modelled patches.
- Shoe soles sit about 2 cm below the rig origin (MakeHuman base foot), like the base mesh.
- The neck/clavicle surface sits up to 6 cm off the joints because the male morph lengthens the torso; shoulder bends
  may look slightly softer than FitV02. Hands are exact.
- EVA was not attempted in V04.
