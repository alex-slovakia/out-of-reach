# EVA-SHOULDER-SEAM-02 — measured study (seam NOT fixed yet)

This continues EVA-FULL-ARM-FIT-01 (`93e0e1a`, folder `../eva-full-arm-fit-v01/`, unchanged). It answers item 1 of
Codex's 6884216267. **No new FBX is delivered**: none of the variants below removes the visible shoulder seam, so
shipping one would not meet the request. This folder records what was tried and measured, so the next pass starts
from evidence.

## What was tried (all with V04 at zero offset, the same poses and checks as v01)

`Source/build_eva_arm_fit.py` (v01 script plus `--seam-radius`, `--seam-medial`, `--seam-recess`) blends the weights
of torso vertices near the sleeve towards the nearest sleeve vertex. The blend falls off with a smoothstep, stays
within 0.18 m of the shoulder joint and leaves bone names, rest lengths, the arm meshes and the gloves unchanged.
`variants/` holds the side view at the rail grip and the full `fit-report.json` for each variant:

| # | Variant | Torso vertices changed | Right-side seam / limb overlap tris (Neutral, Rail, Wrench start, Wrench locked) | Visible result |
|---|---|---|---|---|
| 0 | v01 baseline | 0 | 149/2, 172/46, 89/1, 83/11 | jagged ivory/grey edge at the shoulder |
| 1 | blend R 0.06 m | 404 | 167/3, 178/46, 83/0, 77/0 | top of the shoulder a little smoother; edge remains |
| 2 | blend R 0.09 m | 685 | 170/0, 200/41, 83/0, 79/0 | smoother top; **chest patch stretched** |
| 3 | R 0.06 + 8 mm torso recess | 404 | 164/3, 177/46, 81/0, 76/0 | edge unchanged; **chest patch torn** |
| 4 | R 0.10, outer shoulder only | 148 | 185/0, 189/46, 84/0, 81/0 | chest fine; edge unchanged |

Sleeve vertices visible under the glove stay 0 in every variant.

## Findings

- **Triangle overlap counts do not measure this artefact.** The FitV02 arm renderer's top sits inside the torso's
  shoulder cap by design, and after any blend both surfaces move together and still overlap.
- **The visible edge comes from another source.** It is where the torso's **armpit/side and rear-shoulder skin** cross
  the raised sleeve at a shallow angle. Weights near the boundary cannot fix it without dragging the chest (variants 2
  and 3).
- **A clean fix needs geometry, not weights.** Two options:
  - (a) move the torso/arm split boundary off the shoulder, with a re-modelled cap that the sleeve fully covers;
  - (b) add a modelled transition piece: a scye-bearing ring or a fabric gusset skinned to the clavicle and upper arm,
    over the junction, with the torso skin under it trimmed or recessed only under the piece.
- Both options need several render-and-measure iterations. This chat's context was nearly full, so the pass was
  stopped here rather than delivering a guess.

## Reuse

Run `Source/build_eva_arm_fit.py` with the v01 inputs (see `../eva-full-arm-fit-v01/README.md`) and add
`--seam-radius R [--seam-medial m] [--seam-recess d]`. The report adds `seam_weight_blend` per side.
