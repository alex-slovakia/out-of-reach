# LAND-LAUNCH-SITE-01: AURORA-7 land launch complex (v01)

An original, procedural art kit that replaces the prologue's ocean surroundings with a small land launch complex:
pad and apron, flame trench, launch mount, cable trays and a pipe rack, safety barriers, flood-light towers, a water
tower, pad markings, a crawlerway and a distant assembly building. It carries only the fictional "LC-7" and
"AURORA-7" marks: no agency logos, insignia or copied assets.

**Status:** source delivery for root's native check. This kit has **not** been imported into Unity.
- No scene, terrain, collider, light or camera edits.
- The existing transport, access mast, arm, cabin and staging deck are neither modelled nor moved.

![overview, crew deck, ground level, towards the assembly building](renders/LS_sheet.png)

## Files

| Path | What |
|---|---|
| `LS_LandLaunchSite.fbx` | 11 mesh objects (one per module) and 16 anchor empties. No lights, cameras or colliders. |
| `LS_LandLaunchSite.blend` | Blender 5.3 source scene with the same objects and anchors. It also holds the preview-only `PV_*` proxies, sun and cameras used for the renders; they are not exported. Image paths are relative: `//textures/` for the new maps, and the ship's existing maps in `Assets/_OutOfReach/Visuals/Models/Aurora7/Textures/` (not copied). |
| `ls_manifest.json` | Objects (triangles, bounds, materials), anchors, materials, the terrain interface, provenance and the SHA-256 of every other file. |
| `ls_build_report.json`, `ls_build_roundtrip.json` | The build report, and Blender's re-import check against it. |
| `textures/` | New maps: `LS_Concrete_*` and `LS_Gravel_*` (BaseColor, MODS, Normal; 1024²), and the 1024² `LS_Markings.png` atlas with its rectangles in `ls_markings.json`. |
| `renders/` | Review renders (Cycles, 48 samples, 1280 × 720): `LS_overview`, `LS_crew_deck`, `LS_ground`, `LS_towards_building`, and `LS_sheet`. |
| `source/` | The scripts that rebuild everything (see Rebuild). |

## Space and placement

- Unity metres, y up, local to the existing prologue origin.
- **Every object's pivot is the prologue origin.** Put the FBX root at that origin with identity rotation and scale 1, and each module lands in place.
- Exported like the ship kit: `bake_space_transform`, −Z forward, Y up, `FBX_SCALE_ALL` (unit object scale, metre coordinates).
- **Import it with the ship's ModelImporter settings.** **Bake Axis Conversion must be ON**, as in every `A7_*.fbx.meta`. The other settings are:
  - Use File Units on, Use File Scale on, Scale Factor 1;
  - no cameras, no lights; hierarchy not preserved.
- **With Bake Axis Conversion off (Unity's default), everything comes in rotated 180° about y**, at (−x, y, −z). The trench outlet then lands at z −72, and the assembly building at z +925..+995, behind the transport.
- Read from `ae6df60` only to stay clear of them, never changed:
  - transport axis x 0, z 0; booster Ø 4.7 m from y −30.6 to −0.55; engine emitter (0, −31.4, 0);
  - access mast centre x 0, z −8.5; staging deck z −9.9; crew deck y ≈ 0;
  - pad top y −32.6 (the barge box: centre (0, −33.1, −2), size (36, 1, 24)).

## Terrain interface (root's 6885008839)

- **Foundation top:** y −32.6, flush with the pad.
- **Ground datum:** y **−36.6**. The foundation falls 4 m on a 1:3 slope (12 m wide) on all four sides, with hipped corners.
- **Footprint:** the closed rectangle x [−72, 72], z [−62, 72]. Every outer edge lies exactly on y −36.6, so the hole in the terrain can be exactly this rectangle. The boundary vertices on it are:
  - x = ±72 at z −62, −50, 60 and 72;
  - z = −62 at x ±72 and ±60;
  - z = +72 at x ±72, ±60, ±4 and ±3 (±3 and ±4 are the trench floor and wall edges).
- **Trench outlet: no notch below the datum is needed beyond z 72.**
  - The open slot is 6 m wide (x −3..3) and runs from z −3 to z 72.
  - The floor is level at y −39.6 from z −3 to z 6, under the nozzle and the deflector. A straight ramp then rises to y −36.6 exactly at z 72.
  - The ground can continue flat at the datum from z 72.
  - Below the datum the walls are 1 m thick (out to x ±4); the back wall is at z −4..−3.
- **Crawlerway:** two 12 m gravel lanes (x −18..−6 and 6..18) climb the north slope from the apron edge at z −50, then run on the ground from z −62 to z −880.
  - The lanes stand 0.15 m proud of the datum, with their bottom 0.2 m below it, so they sit on flat terrain without coplanar faces.
- **Assembly building:** stands on the datum at z −925..−995.

## Modules

| Object | Triangles | Materials | What |
|---|---:|---|---|
| `LS_PadDeck_Optional` | 36 | LS_Concrete | Fills the barge-box footprint (x ±18, z −14..10) from the datum up to y −32.6, leaving the exhaust slot open (x ±3 from z −3). Use it unless your own pad leaves the same slot open; otherwise the trench is covered. |
| `LS_LaunchFoundation` | 124 | LS_Concrete | Apron at y −32.6 over x ±60, z −50..60, with the 1:3 slopes and hipped corners down to the datum. |
| `LS_FlameTrench` | 80 | LS_ConcreteSoot, A7_PaintGraphite | Walls, floor and outlet ramp. A deflector wedge from (z −3, y −35.0) down to (z 5.5, y −39.6), faced with a steel plate, turns the plume towards +z, away from the access side. |
| `LS_LaunchMount` | 108 | LS_Concrete, A7_PaintGray, A7_PaintGraphite | Two pedestals beside the slot (x ±3.1..4.7, z ±4.6) and four hold-down arms at z ±1.4 reaching in to x ±1.75. The arms' tops are at y −30.62, 2 cm below the booster's base. A low plinth stands behind the slot. |
| `LS_Umbilicals` | 2,840 | LS_Concrete, A7_PaintGray, A7_PaintWhite, A7_Aluminum | A cable tray 1.2 m up on posts runs from behind the mast (x −1.4, z −10.6) to z −33 and into the east wall of the pad-terminal shelter (x −29..−23, z −35..−31), clear of its door, which faces the pad. A pipe rack with three pipes 2.0 m up runs from (x 1.4, z −10.6) to the propellant sphere (r 6.5 m, centre (42, −23.6, −42)) on eight legs. |
| `LS_Barriers` | 840 | LS_ConcreteBarrier | Jersey barriers along both edges of the open trench, z 10.6..58.4. |
| `LS_FloodTowers` | 1,144 | A7_PaintGray, A7_PaintGraphite, A7_Glass | Two 28 m lattice towers at (±46, z 46) with lamp heads facing the pad. Structure only, no lights. |
| `LS_WaterTower` | 1,208 | A7_PaintGray, A7_PaintWhite | Sound-suppression water tower at (−46, z −38): a tank of r 5.2 m on braced legs, top at y 26.2. |
| `LS_Markings` | 84 | LS_Markings | Hazard bands along the slot; a yellow 1.2 m walkway from the mast's west face to the shelter door; a white edge line 0.6 m inside the apron edge; "LC-7", "AURORA-7" and "BLAST AREA · KEEP CLEAR". All 12 mm above the concrete, and no two strips overlap. |
| `LS_Crawlerway` | 48 | LS_Gravel | See the terrain interface. |
| `LS_AssemblyBuilding` | 50 | A7_PaintWhite, A7_PaintGray, A7_PaintGraphite, LS_Markings | A 70 × 90 × 70 m block centred at z −960 with a roof cap, a 40 m annex to the east, an 18 × 64 m dark door facing the pad and an "AURORA-7" mark. |

## Anchors (Unity metres, prologue-local)

| Anchor | x, y, z | Use |
|---|---|---|
| `LS_PadTop_Centre` | 0, −32.6, −2 | Pad top at the barge-box centre. |
| `LS_NozzleAxis_PadTop` | 0, −32.6, 0 | The vehicle axis at pad level. |
| `LS_TrenchFloor_UnderNozzle` | 0, −39.6, 0 | Trench floor under the nozzle. |
| `LS_DeflectorPeak` | 0, −35.0, −3 | Top edge of the flame deflector. |
| `LS_TrenchExit_Ground` | 0, −36.6, 72 | Trench outlet at the datum, where the plume leaves (for VFX). |
| `LS_GroundLevel_SlopeFoot_West` | −72, −36.6, 0 | The ground datum at the west slope's foot. |
| `LS_CableTray_MastEnd` | −1.4, −31.4, −10.6 | Cable-tray end behind the mast, 1.2 m above the pad. |
| `LS_PadTerminal_Door` | −26, −32.6, −30.98 | Shelter door. |
| `LS_PipeRack_MastEnd` | 1.4, −30.36, −10.6 | Pipe-rack end behind the mast; the pipes are 2.24 m above the pad. |
| `LS_PropellantSphere_Centre` | 42, −23.6, −42 | Sphere centre. |
| `LS_FloodLamp_West`, `LS_FloodLamp_East` | ∓46, −4.0, 45.7 | Lamp heads: suggested positions for root's practical lights, aimed at the pad. |
| `LS_WaterTower_Base` | −46, −32.6, −38 | Water-tower base centre. |
| `LS_Crawlerway_PadEnd` | 0, −32.6, −50 | Apron edge where the lanes start. |
| `LS_Crawlerway_FarEnd` | 0, −36.6, −880 | Far end of the lanes. |
| `LS_AssemblyBuilding_Door` | 0, −36.6, −925 | Door base centre. |

## Materials (Unity URP Lit)

- **New maps follow the ship's convention:**
  - BaseColor is sRGB.
  - Normal is OpenGL-style (Unity's), as are the ship's A7 maps.
  - MODS is linear: R metallic, G occlusion, B unused, A smoothness.
- **UVs are world-scale box projections:** 1 UV unit = 1 m, so the material's tiling sets the tile size:
  - LS_Concrete 0.25 (4 m slabs with sawn joints);
  - LS_ConcreteBarrier 0.5;
  - LS_Gravel 0.5 (2 m).
- **LS_Markings** uses atlas UVs at tiling 1, with alpha clipping at 0.5.
- **Tints and normal strengths** are in `source/ls_materials.json`: LS_Concrete #E2E0DA, LS_ConcreteSoot #77726C (trench and deflector), LS_ConcreteBarrier #EFEDE7, LS_Gravel white.
- **Reused ship materials:** A7_PaintGray, A7_PaintGraphite, A7_PaintWhite, A7_Aluminum and A7_Glass are the ship's own material names. Remap them by name to the existing ship materials rather than extracting duplicates.

## Budgets

- **Triangles:** 6,562 in total (limit 80,000). The largest module is LS_Umbilicals at 2,840.
- **Textures:** two 1024² PBR sets and one 1024² atlas (limit 2k). The steel parts reuse the ship's maps.

## Not included (root's side)

- No terrain outside the footprint and no water.
- No lights: the lamp anchors are for root's practical lights.
- No cameras and no colliders. Root adds the colliders it chooses; the apron is flat at y −32.6 and the slopes are 1:3.
- The `PV_*` objects in the .blend and the renders are stand-ins: the transport, mast, staging deck and arm, a ground plane at the datum, and a sky. They are not exported and do not show root's real geometry.

## Verification

- **Blender 5.3 round trip** (`ls_build_roundtrip.json`): **PASS, 27 checks, 0 failed.**
  - It re-imports the FBX and checks all 11 objects (name, triangle count, pivot, bounds within 1 mm, material slots) and all 16 anchors.
  - Nothing else is in the file: no lights, cameras or extra objects.
- **Terrain boundary:** a scan of the delivered FBX finds exactly the boundary vertices listed above, all at y −36.6 (the trench walls also reach y −40.2 below the datum at z 72). Only LS_Crawlerway and LS_AssemblyBuilding have vertices outside the footprint.
- **Rebuild:** the scripts in `source/`, run on their own in an empty folder, give an identical `ls_build_report.json` and byte-identical textures.
- **No local paths:** a byte search of every delivered file finds none. The .blend's image paths are relative.
- **Unity 6000.3.28f1 import check** (`unity_check/`, run in an isolated empty project on this exact FBX):
  - **Bake Axis Conversion on:** all 11 renderers' world bounds and all 16 anchors equal this manifest. The maximum deviation is 0.0000 m at 0.1 mm output precision. Rotation is 0 and scale is 1 on every node.
  - **Bake Axis Conversion off:** the same data maps exactly to (−x, y, −z).
  - `LSAxisCheck.cs` does the import and the logging; `ls_axis_compare.py` finds the best axis map.
- **Not verified:**
  - the URP look of the materials;
  - lighting and collision;
  - the joins with root's terrain and pad;
  - clearance against root's real transport, mast and arm. The renders use the proxies.

## Rebuild

Needs Blender 5.3, plus Python 3 with NumPy and Pillow.

```
python source/ls_textures.py <out>/textures
blender -b --factory-startup --python source/build_site.py -- --out <out> --ship-textures <dir with the A7_Paint_* and A7_Brushed_* maps> [--render --samples 48]
blender -b --factory-startup --python source/roundtrip_habitat.py -- --out <out> --report ls_build_report.json
python source/make_sheet.py <out>/renders
```

- `ls_textures.py` uses fixed seeds, so the maps come out identical.
- `ls_kit.py` holds all the geometry and constants.
- `build_site.py` builds the materials, objects, anchors, FBX, report, .blend and renders.
- `a7kit.py`, `a7_textures.py`, `build_habitat.py`, `hb_kit.py` and `roundtrip_habitat.py` are the unchanged helpers from the habitat kit.

Made by Claude Opus 5.5 (AI) for Apoliak7777's team; see `ATTRIBUTION.md`.
