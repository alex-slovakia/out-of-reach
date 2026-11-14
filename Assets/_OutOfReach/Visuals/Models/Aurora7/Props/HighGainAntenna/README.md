# HighGainAntenna — HGA-1 high-gain antenna assembly

Hero prop for the **AntennaRepair** location (exterior, EVA). Authored by the prop chat (Claude / Apoliak,
"Blender fork") outside the repository in Blender 5.3 from a script, then imported and checked in Unity in a
private clone. It reaches the repository on the branch `feature/prop-high-gain-antenna`, which adds only this
folder and the folder `.meta` files above it, and the same files are offered to the ship model writer as a
hand-off package.

A 1.6 m parabolic dish on a two-axis gimbal: base plate, tapered pedestal and short boom, azimuth drive,
fork with elevation drive and encoder, reflector with eight ribs and a stiffening ring, tripod and feed horn.
On the boom sit the replaceable electronics unit EU-1 (two gold EVA handles, four captive bolts) on a
bracket with a status plate, a gold EVA handrail and a tether staple. It contains no gameplay state, scripts,
camera, light or audio listener.

| Fact | Value |
|---|---|
| Size in Unity (X × Y × Z) | 1.60 × 2.20 × 1.055 m in the neutral pose |
| Pivot | centre of the underside of the base plate, which is the boom base. Bounds min (−0.8, 0, −0.21), max (0.8, 2.2, 0.845) |
| Axes | boom along **+Y**; in the neutral pose the dish looks along **+Z** |
| Triangles | 7,636 total: Base 1,928, Azimuth 982, Elevation 3,602, ElectronicsBox 944, StatusIndicators 180 |
| Meshes | `Base`, `Azimuth` with its child `Elevation`, `ElectronicsBox`, `StatusIndicators`; identity rotation and unit scale; all carry normals and UV0 |
| Materials | 5 URP Lit: `HG_HighGainAntenna`, `HG_Labels`, `HG_Indicator_Cyan`, `HG_Indicator_Amber`, `HG_Indicator_Red` |
| Textures | 4 × 2048²: BaseColor, MetallicSmoothness (metallic in RGB, smoothness in alpha), Occlusion, Labels |
| Collision | two `BoxCollider` proxies: `CollisionProxy` under the root for the mast, `DishCollisionProxy` under `Elevation` for the dish. Visible meshes have none |

## Files

- `HighGainAntenna.prefab` — use this. The model with the materials and the two collision proxies.
- `HighGainAntenna.fbx` — five triangulated meshes with exported normals, no texture records and no local paths.
  `Elevation` is a child of `Azimuth` in the file. Its `.meta` remaps the five materials to `Materials/`.
- `Materials/`, `Textures/` — as listed above. The textures are clamped atlases; MetallicSmoothness and Occlusion are linear.
- `Previews/` — five genuine Cycles renders of this mesh with these textures (`hero`, `back`, `detail`, `slewed`,
  `wire`) and two captures made in Unity by the import check (`HighGainAntenna_unity_urp.png`,
  `HighGainAntenna_unity_urp_slewed.png`). The grey slab under the antenna in the Cycles renders has the size
  of the hull pad; slab and floor are stand-ins and not part of the model. The Unity captures use two plain
  directional lights and no tone mapping; they show that the import works, not final lighting.
- `Source/` — `build_high_gain_antenna.py` regenerates the model, label atlas, baked textures, renders, FBX and `.blend`:
  `blender -b --factory-startup --python build_high_gain_antenna.py -- --stage full --out <folder outside Assets>`.
  `verify_fbx.py` re-imports an FBX and reports what a consumer receives. `HighGainAntenna_report.json` is the
  report of the build that produced these files. The `.blend` is not in the package: a saved `.blend` stores
  the absolute path it was saved to, and the script rebuilds it in a few minutes. The prop author keeps the built file.

## What must stay together

The prefab points at the FBX and the materials by GUID, the materials point at the textures by GUID, and the
FBX `.meta` holds the material remap and import settings. Keep the `.meta` of every **file** unchanged. The
folder may be renamed or moved freely as long as each file travels with its `.meta`.

## Integration notes

- **Placement.** Put the pivot on the centre of the top face of the antenna pad at AntennaRepair and turn the
  model so that its +Y is the pad normal. The base plate is 0.42 × 0.42 m and stays inside the pad bolts.
  Which way the model's +Z looks along the hull is free, because azimuth turns the dish anyway. No anchor or
  layout change is requested.
- **Gimbal.** `Azimuth` (turntable and fork) has its pivot on the boom axis at local (0, 0.8, 0); turn it about its
  local Y. `Elevation` (trunnion, dish, tripod, feed) is its child with the pivot on the elevation axis, at
  (0, 0.6, 0) under `Azimuth`, which is 1.40 m above the model pivot; turn it about its local X. Negative X angles raise
  the dish: 0 looks at the horizon, −90 looks along the boom. Keep the dish at or above the horizon: about 25°
  below it the rim meets the boom. In the neutral pose no part of the dish is lower than 0.60 m above the pad
  or farther than 0.92 m from the boom axis, so azimuth can turn all the way round.
- **Electronics unit.** `ElectronicsBox` is its own object with the pivot at the centre of its back edge at
  shelf level, at local (0, 0.125, 0.09). It slides out along the root's +Z between two guide rails: 0 is seated, 0.25 m was
  used for the slewed views and clears the shelf. It has no collider of its own and is not animated.
- **Lamps.** `StatusIndicators` has one submesh per lamp, in the order cyan (PWR), amber (NO LINK), red (FAULT).
  Drive a lamp through `_EmissionColor` on its material instance. Delivered values stay within 0–1 so the hue
  survives without tone mapping: cyan and amber lit, red dim. Lamps are presentation only; they must follow
  `SystemConditionChanged` and decide nothing.
- **Collision.** `CollisionProxy` is a box around base, boom, bracket and fork. `DishCollisionProxy` is a box
  around reflector and feed and is parented to `Elevation`, so it follows both axes. They are crude on purpose;
  replace them when the EVA route is decided.
- **Labels.** HGA-1 HIGH GAIN ANTENNA, HGA-1 STATUS, PWR, NO LINK, FAULT, CAUTION RF RADIATION KEEP CLEAR OF
  DISH, EU-1 HGA ELECTRONICS UNIT, 4 CAPTIVE BOLTS, TETHER POINT. American spelling, as in the contracts.
- **EVA aids.** Handrail and box handles use the ship's anodised gold (#C39B3B) and the hull handrail section
  (17 mm tube on 90 mm standoffs or longer). The tether staple is on the base plate beside the pedestal.
- **Palette.** Painted parts use the ship's warm putty grey (#B5B3AE) and graphite (#2B2B2C) as they stood on the
  models branch on 14 Nov; the reflector is a warm white (#D9D8D1) with twelve gore seams and one ring seam
  baked into the texture, after the antenna in the review-01 concept views R05 and R06.
- **Occlusion** was baked per part without its neighbours, because every part moves against the others.
- **First-frame note.** In a batch run the first URP frame can draw emissive lamps in magenta while the shader
  variant compiles. The import check compiles synchronously and renders warm-up frames before reading a capture
  back; do the same before judging a first screenshot.

## Licences and sources

| Item | Source | Licence |
|---|---|---|
| Geometry, UVs, baked textures, label artwork | generated by `Source/build_high_gain_antenna.py`; no external model or texture | original team work |
| Label typeface | Inter, as bundled with Blender 5.3 (`datafiles/fonts/Inter.woff2`), rasterised into `HighGainAntenna_Labels.png`. The font file is not in the package | SIL Open Font License 1.1. Credit: "Inter typeface © The Inter Project Authors" |
| Tool | Blender 5.3.0 (Cycles) | GPL tool; its output carries no licence obligation |

Reference for function only: gimballed high-gain dish antennas on spacecraft and space stations, and the
handrails and orbital replacement units used for EVA maintenance. No NASA model, texture, drawing,
photograph, logo or insignia is copied or included, and no third-party game asset or downloaded texture is
used. The label wording and every value printed on the antenna are fictional. The dish geometry (1.6 m
diameter, focal ratio 0.35, prime-focus feed) is a plausible shape, not an RF design.

## Verification actually performed (14 Nov 2026, Apoliak's machine)

- **Blender 5.3.0, Cycles/OptiX.** Full build exit 0: 1.60 × 1.055 × 2.20 m (Blender X, Y, Z), 7,636 triangles,
  five renders written.
- **FBX round trip.** `verify_fbx.py` re-imported the FBX into an empty Blender scene: same size,
  7,636 triangles, only triangles, five objects at unit scale, `Elevation` parented to `Azimuth`, custom
  normals present, one UV map on each mesh, no texture reference. The file contains no drive-letter path.
- **Unity 6000.3.28f1, URP**, batch run in a private clone of the repository: exit 0, no validation problem.
  Measured there: bounds (1.6, 2.2, 1.055) m with min (−0.8, 0, −0.21); 7,636 triangles; all five meshes at identity rotation and unit scale;
  every renderer uses the external URP Lit materials with matching submesh counts; two colliders; no camera or
  audio listener. The two Unity captures in `Previews/` come from that run; the second one has azimuth at 35°,
  elevation raised 40° and `ElectronicsBox` drawn out 0.25 m, which shows that both pivots sit on their axes.
- `tools/verify_repository.py` passes in that clone with this folder in place (every file has a `.meta`, GUIDs unique).

## Limits

- One LOD, no lightmap UVs, no normal map. Wear is deliberately light: roughness variation, grime in recesses
  and sparse edge chips.
- Base textures average about 2.1 mm per texel (484 px/m); the label atlas is 0.24 mm per texel.
  The build script takes `--tex 4096` if the antenna needs a finer base texture.
- The reflector is one smooth shell; its panel seams exist only in the texture. Ribs and tripod are simple bars.
  Nothing is animated, and there is no damaged state.
- Not checked: placement on the hull, lighting in the EVA scene, a Windows player build, performance, and the
  import on any other branch. No gameplay is attached.
