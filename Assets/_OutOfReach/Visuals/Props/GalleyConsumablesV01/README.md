# GalleyConsumablesV01 — the meal pouch in Unity (MEAL-PRES-01)

Unity import of the already reviewed meal-pouch art for the physical galley meal. **No new art:** the mesh and the label
atlas are byte-identical copies of the GALLEY-PROPS-01 kit; only Unity metadata and three URP materials are new.

| File | Source | SHA-256 |
|---|---|---|
| `GC_MealPouch.fbx` | `feature/galley-consumables-v1@12100dd` `docs/art-direction/galley-consumables-v01/GC_MealPouch.fbx` | `770ba03b…d996f9` |
| `Textures/GC_Labels.png` | same kit, `textures/GC_Labels.png` | `bb5c075e…ab054f9` |
| `WORK_Clasp_Meal_R.json` | `feature/work-hand-clasp-v1@7e9ee5c` `work_hand_clasp.json`, pose `WORK_Clasp_Meal_R`, extracted unchanged by `Source~/make_meal_clasp.py` | — |

- **Variants:** `GC_MealPouch_Sealed` (1,736 triangles) and `GC_MealPouch_Opened` (2,484) share the prop origin, so the
  meal swaps them in place when the pouch is cut open. Prop space: Unity metres, y up the pouch, +z label side.
- **Importer:** the habitat kit's settings (`bakeAxisConversion: 1`, `preserveHierarchy: 1`, metres), with the FBX
  material slots remapped to `Materials/GC_Foil.mat`, `GC_Food.mat` and `GC_Labels.mat`. The EditMode test
  `DeliveredClaspDataIsTheMeasuredMealPose` checks the imported bounds against the kit manifest (0.141 × 0.178 × 0.029 m).
- **Materials:** URP Lit. `GC_Foil` reuses the ship's existing `A7_MLI_*` maps at tiling 6 (no texture copied),
  `GC_Food` is flat, `GC_Labels` is alpha-clipped like `HB_Labels`.
- **Clasp:** the finger-local, model-space deltas at bind, the joint head targets (2 mm check) and the pouch frame relative
  to the wrist of the 164-bone WORK rig (`A7_BodyFit02_WORK.fbx` at `85b0994`). Values are copied, not re-solved.
- **Rights:** see `ATTRIBUTION.md` (the kit's own attribution, unchanged in substance).
