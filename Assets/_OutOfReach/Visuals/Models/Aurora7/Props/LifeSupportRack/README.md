# LifeSupportRack — ARS-1 air revitalization unit

Hero prop for the **LifeSupportRepair** location. Authored by the prop chat (Claude / Apoliak, "Blender fork")
outside the repository in Blender 5.3 from a script, then imported and checked in Unity in a private clone.
It reaches the repository on the branch `feature/prop-life-support-rack`, which adds only this folder and the
folder `.meta` files above it, and the same files are offered to the ship model writer as a hand-off package.

Air revitalization unit that mounts in the centre bay of the ship's ECLSS-1 rack: graphite frame on a mounting
plate, light painted enclosure, an ATMOSPHERE STATUS sub-panel with three lamps, an oxygen supply pressure
gauge, two removable CO2 bed canisters seated in collared sockets, a guarded cabin air inlet fan whose rotor
can spin, a particulate filter drawer and a supply manifold with three blue lever valves. It contains no
gameplay state, scripts, camera, light or audio listener.

| Fact | Value |
|---|---|
| Size in Unity (X × Y × Z) | 0.80 × 1.40 × 0.22 m |
| Pivot | bottom centre of the bounding box; bounds min (−0.40, 0, −0.11). The wall side is 0.11 m behind the pivot |
| Front | faces local **+Z** |
| Triangles | 7,300 total: Body 5,520, CanisterA 766, CanisterB 766, FanRotor 68, StatusIndicators 180 |
| Meshes | `Body`, `CanisterA`, `CanisterB`, `FanRotor`, `StatusIndicators`; identity rotation and unit scale; all carry normals and UV0 |
| Materials | 5 URP Lit: `LS_LifeSupportRack`, `LS_Labels`, `LS_Indicator_Cyan`, `LS_Indicator_Amber`, `LS_Indicator_Red` |
| Textures | 4 × 2048²: BaseColor, MetallicSmoothness (metallic in RGB, smoothness in alpha), Occlusion, Labels |
| Collision | one `BoxCollider` (0.80 × 1.40 × 0.22 m) on the separate child `CollisionProxy`; visible meshes have none |

## Files

- `LifeSupportRack.prefab` — use this. The model with the materials and the collision proxy.
- `LifeSupportRack.fbx` — five triangulated meshes with exported normals, no texture records and no local paths.
  Its `.meta` remaps the five materials to `Materials/`.
- `Materials/`, `Textures/` — as listed above. The textures are clamped atlases; MetallicSmoothness and Occlusion are linear.
- `Previews/` — five genuine Cycles renders of this mesh with these textures (`hero`, `front`, `detail`, `pulled`,
  `wire`) and two captures made in Unity by the import check (`LifeSupportRack_unity_urp.png`,
  `LifeSupportRack_unity_urp_pulled.png`). The Unity captures use two plain directional lights and no tone
  mapping; they show that the import works, not final lighting.
- `Source/` — `build_life_support_rack.py` regenerates the model, label atlas, baked textures, renders, `.blend` and FBX:
  `blender -b --factory-startup --python build_life_support_rack.py -- --stage full --out <folder outside Assets>`.
  `verify_fbx.py` re-imports an FBX and reports what a consumer receives. `LifeSupportRack_report.json` is the
  report of the build that produced these files. The `.blend` is not in the repository: a saved `.blend` stores
  the absolute path it was saved to, and the script rebuilds it in about two minutes. The prop author keeps the
  built file.

## What must stay together

The prefab points at the FBX and the materials by GUID, the materials point at the textures by GUID, and the
FBX `.meta` holds the material remap and import settings. Keep the `.meta` of every **file** unchanged. The
folder may be renamed or moved freely as long as each file travels with its `.meta`.

## Integration notes

- **Placement.** Back plane on the rack face, local +Z into the room. The ship model writer mounts it in the
  centre bay of the ECLSS-1 rack, whose face is at x = +1.73 and looks towards −X: pivot at (1.62, 0.45, 8.1),
  rotation (0, −90, 0). The unit then spans 0.45 to 1.85 m above the deck with its centre on the
  LifeSupportRepair anchor height of 1.15 m, and the canister row is 1.32 m above the deck. No anchor or layout
  change is requested.
- **Collision.** The unit stands 0.22 m proud of the rack face. Keep `CollisionProxy` if the player must not
  walk into the canisters; drop it if the rack's own collision box has to stay the only one.
- **Canisters.** `CanisterA` at local (0.165, 0.87, 0.009) is on the viewer's left, `CanisterB` at
  (−0.165, 0.87, 0.009) on the right. Each has its pivot at the centre of its base, on the canister axis, and
  slides along the root's +Z: 0 is seated, 0.04 m clears the socket collar, 0.14 m was used for the pulled
  views. One canister fits a cylinder of radius 0.103 m and length 0.101 m, handle included. The canisters
  have no collider of their own and are not animated.
- **Fan.** `FanRotor` (hub and five blades) has its pivot on the fan axis, at local (0.165, 0.47, 0). Spin it
  about its local Z. The housing and the wire guard stay in `Body`.
- **Lamps.** `StatusIndicators` has one submesh per lamp, in the order cyan (AIRFLOW), amber (CO2 HIGH),
  red (FAULT). Drive a lamp through `_EmissionColor` on its material instance. Delivered values stay within 0–1
  so the hue survives without tone mapping: cyan and amber lit, red dim. Lamps are presentation only; they must
  follow `SystemConditionChanged` and decide nothing.
- **Labels.** ARS-1 AIR REVITALIZATION UNIT, CO2 BED A, CO2 BED B, LiOH CANISTER, O2 SUPPLY PRESSURE, INLET FAN,
  PARTICULATE FILTER, V1 O2 SUPPLY, V2 CONDENSATE, V3 CABIN VENT. Bed, canister and supply names follow the
  labels of the ship's ECLSS-1 rack. The spelling is American, as in the contracts (`Depressurizing`).
- **Static story cues.** The gauge needle rests near 140 kPa on a 0–200 kPa dial, and valve V3 CABIN VENT is
  closed (lever across the line) while V1 and V2 are open. Needle, levers and the filter drawer are part of
  `Body` and do not move. Nothing printed or modelled on the unit is an authoritative value; the HUD is.
- **Occlusion** was baked per part without its neighbours, so pulling a canister reveals no baked shadow. The
  rotor is the one exception: it was baked inside its housing, which shades it alike at every angle.
- **First-frame note.** In a batch run the first URP frame can draw emissive lamps in magenta while the shader
  variant compiles. The import check compiles synchronously and renders warm-up frames before reading a capture
  back; do the same before judging a first screenshot.

## Licences and sources

| Item | Source | Licence |
|---|---|---|
| Geometry, UVs, baked textures, label artwork | generated by `Source/build_life_support_rack.py`; no external model or texture | original team work |
| Label typeface | Inter, as bundled with Blender 5.3 (`datafiles/fonts/Inter.woff2`), rasterised into `LifeSupportRack_Labels.png`. The font file is not in the repository | SIL Open Font License 1.1. Credit: "Inter typeface © The Inter Project Authors" |
| Tool | Blender 5.3.0 (Cycles) | GPL tool; its output carries no licence obligation |

Reference for function only: air revitalization racks on the ISS and the lithium hydroxide CO2 canisters flown
on Apollo and the Space Shuttle. No NASA model, texture, drawing, photograph, logo or insignia is copied or
included, and no third-party game asset or downloaded texture is used. The label wording and every value
printed on the unit are fictional. The valve levers use the ship's anodised blue (#2B58A8).

## Verification actually performed (14 Nov 2026, Apoliak's machine)

- **Blender 5.3.0, Cycles/OptiX.** Full build exit 0: 0.80 × 0.22 × 1.40 m (Blender X, Y, Z), 7,300 triangles,
  five renders written.
- **FBX round trip.** `verify_fbx.py` re-imported the committed FBX into an empty Blender scene: same size,
  7,300 triangles, only triangles, five objects at unit scale, custom normals present, one UV map on each mesh,
  no texture reference. The file contains no drive-letter path.
- **Unity 6000.3.28f1, URP**, batch run in a private clone of `main@43be436`: exit 0, no validation problem.
  Measured there: bounds (0.80, 1.40, 0.22) m with min (−0.40, 0, −0.11); 7,300 triangles; all five meshes at
  identity rotation and unit scale; every renderer uses the external URP Lit materials with matching submesh
  counts; one collider; no camera or audio listener. The two Unity captures in `Previews/` come from that run;
  the second one has `CanisterA` pulled 0.14 m and `FanRotor` turned 36°.
- `tools/verify_repository.py` passes in that clone with this folder in place (every file has a `.meta`, GUIDs unique).
- A trial merge of the first commit of this branch with `feature/aurora7-models` and with
  `feature/visuals-cinematics` reported no conflict: the four folder `.meta` files above this folder are
  byte-identical on all three branches, and the second commit changes files inside this folder only.

## Limits

- One LOD, no lightmap UVs, no normal map. Wear is deliberately light: roughness variation, grime in recesses
  and sparse edge chips.
- Base textures average about 1.6 mm per texel (623 px/m); the label atlas is 0.24 mm per texel.
- The fan blades are single-sided planes behind the guard. Nothing is animated.
- Not checked: Windows player build, performance, placement and lighting in the ship, and the import on any
  other branch. No gameplay is attached.
