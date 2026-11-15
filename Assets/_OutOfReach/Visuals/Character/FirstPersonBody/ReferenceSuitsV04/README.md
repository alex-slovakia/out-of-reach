# ReferenceSuitsV04 — WORK and EVA suits, alternative to V03 (SUITS-V04)

Alternative attempt for the 18:30 CET suit decision (#7 claim 6895641830). **WORK** (first delivery) and **EVA**
(second delivery, same folder, same method). Nothing outside this folder is changed: no FitV02, V03, presenter, scene or
prefab edit.

## What it is

Both FBX files use the **unchanged FitV02 rig** `A7_ContinuousBody_Rig` (164 bones, same names, parents and bind: max
difference 7e-7 m after a Blender round trip) and re-export the FitV02 takes `BODY_StandRelaxed`, `BODY_FloatNeutral`,
`BODY_SeatedFit` unchanged (take names `A7_ContinuousBody_Rig|BODY_*`, clips renamed `BODY_*` in the `.meta`).

### WORK — `Meshes/A7_SuitV04_WORK.fbx` (30,832 triangles + 13,580 for the third-person head)

| Object | Triangles | Content |
|---|---:|---|
| `A7_WORK_TorsoLegs` | 8,500 | male torso and legs, navy knit shirt with rib hem, charcoal twill cargo trousers (side zip pockets with zips, knee seams, back pockets), dark low shoes |
| `A7_WORK_Arm_L` / `A7_WORK_Arm_R` | 4,376 each | sleeves with rib cuffs, bare hands (`MAT_Cabin_BareHands` submesh, like FitV02) |
| `A7_WORK_Head` | 13,580 | head, hair, eyes — **third person only**; hide it in first person (FitV02 has no head) |

### EVA — `Meshes/A7_SuitV04_EVA.fbx` (28,104 first-person triangles + 23,424 for the third-person head and helmet)

| Object | Triangles | Content |
|---|---:|---|
| `A7_EVA_TorsoLegs` | 15,790 | layered warm-ivory suit (panel seams, ripstop, wear toward the boots), chest pouch and chest control pack (display, keypad, knobs, yellow connector ring), harness straps, belt with titanium buckles, leg straps and thigh loops, red knee stripes, knee convolutes, thigh and shin pockets, ankle straps, ivory boots with grey toe/heel and rubber sole, PLSS backpack (lid panel, handle, red stripe, connector plate, lower canister with straps, hose, radio and antenna) |
| `A7_EVA_Arm_L` / `A7_EVA_Arm_R` | 6,158 / 6,156 | bulky sleeves with red upper-arm band and elbow convolutes, titanium wrist disconnect ring, glove gauntlet, gloves (ivory back, flex-grey palm side and fingertips) |
| `A7_EVA_Head` | 23,424 | head, hair, eyes, helmet (clear bubble, gold visor, ivory shell, red helmet ring, side lamps) and the suit's neck bearing — **third person only**; hide it in first person |

EVA materials: `MAT_V04E_*` (18, URP Lit). The glass is transparent, the display and lamps are emissive. There is no
bare-hand submesh in EVA (gloved hands), so the compatibility mapping finds no palm submesh — like FitV02 EVA.

## How it fits the rig without moving it

The FitV02 bind is exactly MakeHuman's default skeleton on the neutral MakeHuman base mesh (max joint difference 2e-7 m).
So the new body is the CC0 base mesh with CC0 male targets, skinned with MakeHuman's own CC0 default weights (no weight
transfer), and anchored to the rig's joints. **Arms and hands keep the exact base geometry** (hand joints 0.0 m off), so
grip and contact calibration stay valid; the male shape fades in at the shoulders. Residual torso joint offset after the
male morph: up to 6.4 cm at the neck base and 4 cm at the clavicles (the torso surface follows the rig; no bone moved).
Garments are MakeHuman CC0 clothes fitted through their `.mhclo`; body verts under clothes are removed as MakeHuman does,
which prevents poke-through.

EVA on top of that: the casualsuit02 garment is inflated along its normals by a thickness blended from its bone weights
(torso 3.0 cm, upper arms and thighs 2.9 cm, forearms 2.4 cm, narrowing into the wrist rings and ankles); the boots are
the CC0 shoes03 shell inflated 1.1 cm with the foot part compressed so the sole sits at the rig origin (-1.4 cm, like
WORK); the gloves are the hand skin itself pushed out 2.5 mm (fingers) to 4 mm (hand), so they deform exactly like
the bare hands. Straps, rings, ribs, pockets and bands are patches ray-projected onto the suit and skinned like the
nearest suit vertex. Hard goods are rigid: chest pack `spine02`, chest pouch and PLSS `spine01`, helmet `head` (the face
never clips the bubble; the helmet turns with the head).

## Unity (checked in an isolated batch project, Unity 6000.3.28f1)

Importer as FitV02: Generic, avatar from this model, animation compression Off, scale 1, Use File Scale, no axis bake,
clips renamed `BODY_*`, materials via description remapped to `Materials/*.mat` (URP Lit).
- WORK, `Source~/unity_import_check.txt`: rig lossy scale 1, 164 bone transforms, four skins with 164 bones each,
  materials bound, three non-legacy Generic clips, baked bind bounds in metres (feet 0.0, neck 1.60, head top 1.79).
- EVA, `Source~/unity_import_check_eva.txt`: rig lossy scale 1, 164 bone transforms, four skins with 164 bones each
  and one submesh per material (TorsoLegs 12, arms 7, Head 10), all `MAT_V04E_*` bound, three non-legacy Generic clips,
  baked bind bounds in metres (boot soles -0.014, antenna tip 1.91, helmet top 1.84, hands 1.00).

Integration (root): add a replacement `AvatarPresentationProfile` outfit pointing at the FBX (roles
`A7_<VARIANT>_TorsoLegs`, `A7_<VARIANT>_Arm_L`, `A7_<VARIANT>_Arm_R`; WORK palm submesh = `MAT_Cabin_BareHands`, EVA none),
keep `A7_<VARIANT>_Head` disabled in first person. The compatibility generator expects unique names: they are unique here.

## Evidence

- `Previews~/work_final_front_back.png`, `Previews~/eva_final_front_back.png` — front/back next to the approved reference
  (Blender Cycles, soft light).
- `Previews~/work_final_lookdown.png` — WORK first-person look-down (eye 1.60 m, leaned 0.32 m forward, 72° down, vFOV 68).
- `Previews~/eva_final_lookdown.png` — EVA first person: the same leaned camera, and the root fixture (eye 1.65 m, 0.20 m in
  front of the neck, 75° down, vFOV 68), Head part hidden.
- `Source~/blender_rig_verify.json`, `Source~/blender_rig_verify_eva.json` — Blender re-import of each FBX vs FitV02
  (164 bones, names, parents, bind, scale 1).
- `Source~/unity_import_check.txt`, `Source~/unity_import_check_eva.txt` — Unity batch import results.
- `Meshes/A7_SuitV04_WORK_report.json`, `Meshes/A7_SuitV04_EVA_report.json` — triangles, materials, vertex groups per object.

## Rebuild

Inputs (CC0, see `Provenance/MakeHumanCC0-sources.json`): MakeHuman `base.obj`, `default.mhskel`, `default_weights.mhw`,
male targets and the system-assets pack.
- WORK: `python -I Source~/make_textures.py <inputs> <tex>`, then
  `blender -b --factory-startup --python Source~/build_v04.py -- --render <prefix> --export <dir>` (Blender 5.3).
- EVA: `python -I Source~/make_textures_eva.py <inputs> <tex>`, then
  `blender -b --factory-startup --python Source~/build_v04_eva.py -- --export <dir>` and, as a separate run (export strips
  the texture nodes), `... -- --render <prefix> --samples 64`.

## Limits (honest)

- The face is MakeHuman's generic young male, not the reference actor; it is only for third person (EVA: seen through
  the bubble; its eyes are seated 3 mm deeper and 10 % smaller than the CC0 fit, which bulged out of the male lids).
- WORK: shirt and trousers come from one MakeHuman garment (folds from its normal map); the reference's raglan seams and
  the exact cargo pocket shape are approximated by modelled patches.
- EVA: the suit is one inflated garment, not separate layers; the reference's dense quilting is approximated by panel
  seams, ripstop and ribs in texture and geometry. Hard goods are clean bevelled boxes and cylinders (no small labels,
  cables or fasteners). Arm-to-torso and inner-thigh surfaces can touch where the bulk meets in the A-pose. The chest pack
  hides the boots in a first-person look-down, as in a real suit.
- WORK shoe soles sit about 2 cm below the rig origin (MakeHuman base foot), like the base mesh; EVA boot soles 1.4 cm.
- The neck/clavicle surface sits up to 6 cm off the joints because the male morph lengthens the torso; shoulder bends
  may look slightly softer than FitV02. Hands are exact.
