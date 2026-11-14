# WORK-LOOKDOWN-STUDY-01 — why looking down shows shirt and no boots

**Status:** a Blender study for review, by the Claude model writer on 14 November 2026 (#7, Codex 6884216267 item 2; claim 6884248082).
- **Inputs, read only:**
  - `A7_BodyFit02_WORK.fbx` at `85b0994`.
  - `BODY_FloatNeutral_V04.fbx` at `26d0ee0`, frame 1, with `CharacterRoot` at (0, 0, 0), i.e. zero offset.
- **Camera:** as in root's V04 fixture: the eye 1.65 m above the feet root, pitch down 80°, vertical FOV 68°, 16:9, near clip 0.05 m.
- **Shown:** only `A7_WORK_TorsoLegs`. The arms are off, as with the separate camera hands.
- **No edits:** no FBX, prefab, runtime or Core file is changed. The released clasp (`7e9ee5c`) is untouched.

![Look-down study](renders/LOOKDOWN_sheet.png)

How to read the sheet:
- **Rows:**
  - **A:** V04 at zero setback, root's fixture.
  - **B:** a standing idle plus a 0.197 m visual setback.
  - **C:** the same idle plus a 0.33 m setback.
- **Columns:**
  1. the eye view;
  2. the region mask (red torso, yellow hips, blue legs, green boots);
  3. the side view, with the eye and the view's top, centre and bottom rays.

**Reproduction check:** row A matches root's native capture `docs/verification/i06-body-float-v04/after-hierarchy-fix/05-work-floating-contact-fit.png`, with the shirt, zip and amber patch across the lower half of the view.

## Why: measured on A

- **Screen coverage:** the torso covers 30.4 % of the screen; hips, legs and boots 0 %.
- **The boots are in view but hidden.** All 2,195 boot vertices lie inside the view, 84.8–97.8° below the horizon (the view spans 46–114°). Yet every sight line from the eye to them hits the torso first: 2,195 of 2,195.
- **The cause is geometric.** The eye sits straight above the feet root, so the boots lie directly below it, and the chest and belly are in between.
- **V04 makes it worse.** Its floating posture flexes the knees 68.8° (thighs 21° forward of vertical). With the feet on the floor at zero offset, that crouches the body under the eye: the nearest body skin is 157 mm from the eye.

## A pose change alone cannot fix it

**The test:** a grid of 48 variants (`grid_zero_setback`), eye fixed, feet planted.
- Each leg reaches its unchanged V04 ankle by IK. Foot drift was 0.0 mm in every variant.
- No bone length changes.
- The grid crossed three changes:
  - hips up 0–120 mm, which straightens the knees from 69° to 30°;
  - hips back 0–120 mm;
  - the trunk leaned back 0–12°, spread over `spine05`…`spine01`.

| Variant | Torso % | Boot vertices visible | Knees |
|---|---|---|---|
| V04 as is | 30.4 | 0 / 2,195 | 68.8° |
| trunk 12° back | 20.1 | 0 | 68.8° |
| hips 120 mm back | 12.4 | 0 | 72.0° |
| knees straightened (hips 120 mm up) | 48.7 | 0 | 29.5° |
| hips 120 mm up and 120 mm back, trunk 12° back (best of 48) | 13.8 | 156 (0.10 % of the screen) | 35.8° |

**What the grid shows:**
- Leaning back trims the shirt but uncovers no boot.
- Straightening the knees raises the torso into the view.
- Nothing in natural ranges clears the sight lines while the eye stays straight above the feet.

## What works: the anatomical-eye setback plus a standing idle

**The setback.** The body sits behind the eye, as root's presentation setback already does for the other poses.
- `BODY-FIT-02-README` gives 0.197 m as the anatomical-eye setback and 0.33 m as the render-matching value.
- The eye and the player capsule do not move; only the body wrapper's presentation offset does.

**The setback alone is not enough with V04's knees** (`setbacks_with_V04_like_poses`):

| Pose | Setback | Boot vertices visible | Hidden by |
|---|---|---|---|
| V04 as is | 0.197 m | 0 | torso 2,005 · legs 190 |
| V04 as is | 0.33 m | 12 | torso 1,239 · legs 944 |
| hips 40 mm back | 0.33 m | 108 | legs 1,620 · torso 467 |

**With a standing idle as well.** The knees go to about 11° (hips 150 mm up, which removes V04's crouch), and the hips 40 mm back. The feet stay planted, with 0.0 mm drift (`cases`, `setbacks_with_standing_idle`):

| Case | Setback | Knees | Boot vertices visible | Boots % | Torso % | Legs % | Hips % | Eye to nearest skin |
|---|---|---|---|---|---|---|---|---|
| A, V04 (fixture) | 0 | 68.8° | 0 / 2,195 | 0.00 | 30.44 | 0.00 | 0.00 | 157 mm |
| B, standing idle | 0.197 m | 11.2° | 762 / 2,195 | 0.61 | 7.30 | 0.59 | 0.19 | 232 mm |
| C, standing idle | 0.33 m | 11.2° | 1,436 / 2,195 | 1.08 | 0.91 | 1.93 | 0.00 | 340 mm |

The boots stay a small share of the screen because they sit 92–111° below the horizon, at the bottom edge of the 80°-down view.

## Recommendation (minimal)

1. **With the feet planted, use a standing idle,** not V04's floating crouch.
   - The knees go to about 11°, close to relaxed standing.
   - The thighs stand vertical (−1.5° from vertical).
   - The trunk keeps V04's angle (21.4° back of vertical, measured `spine05` to `spine01`).
   - The hips sit 40 mm back.
2. **Keep the body's anatomical-eye setback** (0.197 m) instead of zero. Without it, no natural pose clears the sight lines; see the grid.
3. **Keep V04 for floating,** where the feet are not planted. This study did not change it.

**Joint ranges used:**
- Knees: 11.2° (relaxed standing is roughly 0–10°; V04 has 68.8°).
- Thighs: 1.5° behind vertical.
- Trunk: unchanged from V04.
- Feet: unchanged (0.0 mm drift).
- Bone lengths: unchanged.
- The extra trunk lean in the grid (up to 12°) is well inside a normal spine's extension, but it did not help, so it is not recommended.

## Not verified

- **No Unity import.** Coverage comes from Blender Workbench flat renders (960×540, anti-aliasing off). The eye position is root's fixture value.
- **The standing idle is a proposal.** Its exact values (root shift and knee angles) are in `lookdown_study.json`. No FBX or clip is authored yet, as asked. Root decides the setback and the idle.
- **Arms are off** in every view.

## Files

| File | What it is |
|---|---|
| `lookdown_study.json` | Everything measured, every value in Blender armature axes: the inputs, methods, the three cases, the 48-variant grid, and both setback tables (coverage, boot sight lines, joints) |
| `renders/` | The eye, region and side views of A, B and C, and the sheet |
| `source/lookdown.py` | The study script. Modes: baseline (always), `--variants` (grid), `--setbacks` (`--stand`), `--final` (the cases) |

**Rebuild:**

```
blender -b --factory-startup --python source/lookdown.py -- --body <A7_BodyFit02_WORK.fbx> --v04 <BODY_FloatNeutral_V04.fbx> --out <dir> --final
```

**Provenance:** the body is the team's published asset (MakeHuman CC0 base; licences in `FitV02/Provenance/`), read only and not copied here. The study renders are original Blender output.
