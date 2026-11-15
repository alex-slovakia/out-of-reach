# ReferenceSuitsV03 — CHAR-REF-01 (two male characters on the unchanged FitV02 rig)

Branch `feature/reference-suits-v03`, new folder only; nothing existing was edited. **V03c** (after the user's "horrible,
do it properly" on V03b): the characters are assembled from the official **MakeHuman CC0 system assets** (the same project
that FitV02's skeleton and body come from), so skin, hair, eyes and cloth are real textured assets instead of flat colours.

| File | Content |
|---|---|
| `Meshes/A7_SuitV03_WORK.fbx` | WORK, 32.0 k tris: textured middle-aged male head (skin, eyes, brows, lashes, short dark hair), navy long-sleeve crew-neck tee and charcoal cargo trousers (MakeHuman `male_casualsuit02` retinted, logo and piping removed, side zip cargo pockets with flaps and knee panels added), dark trainers (`shoes06` retinted), bare hands. Renderers `_Head`, `_TorsoLegs`, `_Arm_L`, `_Arm_R`. |
| `Meshes/A7_SuitV03_EVA.fbx` | EVA, 62.6 k tris: same head behind a bubble visor with gold shade and lamps; bulky cream suit (same cloth textures/normals, inflated) with waist/wrist rings, harness, layer seams, knee pads with bellows, red bands at the knees and on the upper arms, elbow bellows, front/side pockets, thick gloves with gauntlets, cream boots (`shoes03` retinted), two chest hoses; chest control panel with display, PLSS. Renderers `_Head`, `_TorsoLegs`, `_Arm_L`, `_Arm_R`, `_ChestPanel`, `_Plss`, `_Helmet`, `_Visor`. |
| `Materials/` | 26 URP Lit materials (cloth: grayscale texture x tint + normal map; hair/brows/lashes/eyes: alpha cutout; visor: transparent). |
| `Textures/` | 11 textures (MakeHuman CC0 or derived from them; display texture original). |
| `Source~/` | Blender 5.3 / Python pipeline: `build_mh.py` (assembly), `mhproxy.py` (MakeHuman .mhclo proxy fitting + default weights), `male_shape.py`, `assemble.py` (renderer split with identical seam normals), `prep_textures.py`, `package2.py`, Unity check `SuitV03Check.cs`, `.blend` sources (image paths blanked). |
| `Previews~/` | `compare_work.png`, `compare_eva.png` (reference left, model right, Cycles), `first_person_work_eva.png`, `head_work.png`, `head_eva.png`, `proof_v03b/`. |
| `Provenance/` | build reports with bounds, materials.json, male-shape report, Blender + Unity rig checks, license note. |

## Rig contract (identical to FitV02)
164 bones, same names/parents, root `CharacterRoot`, bone-name hash `459a989457d9af1e`. Blender: heads/tails <= 0.7 um, bone
matrices <= 9e-5 vs FitV02. Unity 6000.3.28f1: 164 transforms, rig scale (1,1,1), every renderer's bindposes equal to
FitV02's by bone name (<= 9.8e-5), RESULT PASS. All meshes skinned (max 4 influences, sums 1) with MakeHuman's default
weights for the same skeleton; FBX store the bind pose and no clips. Male shape: MakeHuman CC0 male targets re-fitted onto
the original joints (limb/spine joints 0 mm drift), the face carried rigidly by the head bone.

## Integration notes
- First person: disable `_Head` (camera inside) and EVA `_Helmet`, `_Visor`, `_Plss`; `_ChestPanel` may stay. Arms: disable
  `_Arm_L/_R` as before when camera-owned hands are used (sleeves and hands are in the arm renderers).
- Hair, brows, lashes and eyes use alpha cutout (`_AlphaClip`, Cull Off); the clear visor is transparent.
- Importer settings = FitV02's (Generic, external materials searched by name in `Materials/`).

## Known limits
Not photo-real: game-resolution MakeHuman textures (<= 2048 px), no wear/dirt paint on the EVA suit, hair is MakeHuman's
card hair; rest-pose renders only (no clip played on these meshes); URP look in the game scene not checked by me.
