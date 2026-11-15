# LAND-SURROUNDINGS-V01: static surroundings for the land launch site (v01)

A small, original, procedural kit that fills the empty flat ground around the LAND launch site. It adds:
- low coastal grass and scrub;
- broken tree lines and hammocks on the horizon;
- low earth berms;
- riprap along two drainage lines, with culvert headwalls;
- a perimeter security fence;
- a utility pole line.

It is a flat coastal site: no mountains, no water, nothing taller than 18 m.

**Status:** source delivery for root's native check. This kit adds no scene, composition, collider, light, particle or script change, and does not touch the 11 LAND modules.

![deck north, deck east, aerial, ground west](renders/LSS_sheet.png)

## Placement

- Unity metres, y up, local to the prologue origin. **Every object's pivot is the prologue origin.** Put the FBX root there with identity rotation and scale 1.
- **Importer:** the ship's settings, **with Bake Axis Conversion ON**:
  - `bakeAxisConversion 1`;
  - `useFileUnits 1`, `useFileScale 1`, `globalScale 1`;
  - no cameras or lights; `preserveHierarchy 0`.
- **Ground:** everything stands on y −36.6, root's ground.
- **Kept clear:** everything stays out of these zones, with margins:

| Zone | x | z |
|---|---|---|
| Launch foundation | −72..72 | −62..72 |
| Plume and outlet corridor | −35..35 | 60..320 |
| Crawlerway corridor and facility | −55..95 | −1020..−50 |

  - Ground patches and scrub keep their whole footprint plus 3–4 m clear; every point of a hammock keeps 12 m clear.
  - The boarding and launch volume is inside the foundation zone.
- **North view:** the crawlerway view towards the facility stays open. The tree line behind the facility is at 2.1 km and 10–18 m tall, so the 90 m building stands fully in front of it.

## Groups (one object each)

| Object | Triangles | Materials | What |
|---|---:|---|---|
| `LSS_GroundBreakup` | 2,400 | LSS_GrassDry | 80 wide, low grass and sand mounds (18–60 m across, 0.15–0.45 m high) at 95–1,500 m, to break up the ground colour |
| `LSS_Scrub` | 3,360 | LSS_Scrub | 160 scrub clumps (2–6.5 m across, 0.6–1.7 m high) at 80–950 m |
| `LSS_TreeLines` | 5,230 | LSS_Canopy | Broken tree lines at 1.9–2.5 km (10–18 m tall), a middle ring at about 1.1 km (8–15 m), and 9 hammocks at 450–900 m (6–11 m) |
| `LSS_Berms` | 660 | LSS_GrassDry | 6 low earth berms (2–4 m high, 14–20 m wide, 140–200 m long) at 350–950 m |
| `LSS_Drainage` | 1,548 | LSS_Rock, LS_Concrete, A7_PaintGraphite | Riprap rock clusters along x −100 (z −40..250) and z 150 (x 48..190), with four concrete culvert headwalls at the line ends |
| `LSS_Fence` | 2,712 | A7_Aluminum | Perimeter fence: posts every 8 m and two rails, 2.5 m high. It runs on x −200..200, z −200..330, with a gate gap at x −62..102 on the north side for the crawlerway |
| `LSS_UtilityPoles` | 1,122 | LS_ConcreteBarrier, A7_PaintGraphite | 15 concrete poles (11 m) with crossarms every 60 m along x 112, z −70..−910, east of the crawlerway corridor, carrying three sagging wires |

**Anchors** (all at y −36.6):

| Anchor | x, z |
|---|---|
| `LSS_FenceGate_North` | 20, −200 |
| `LSS_FenceCorner_SW` | −200, 330 |
| `LSS_FenceCorner_NE` | 200, −200 |
| `LSS_PoleLine_North` | 112, −910 |
| `LSS_PoleLine_South` | 112, −70 |
| `LSS_Drainage_West_Mid` | −100, 105 |
| `LSS_Drainage_South_Mid` | 119, 150 |

## Materials (Unity URP Lit)

- **New: `LSS_GrassDry`, `LSS_Scrub`, `LSS_Canopy`.** They share one new map set, `textures/LSS_Vegetation_*`:
  - 1024², a 4 m tile;
  - BaseColor sRGB, Normal OpenGL-style, MODS linear (R metallic, G occlusion, A smoothness).
  - The materials only change the tint and tiling; see `source/lss_materials.json`.
- **New: `LSS_Rock`** uses LAND's `LS_Concrete` maps with a grey tint.
- **Reused by name:** `LS_Concrete` and `LS_ConcreteBarrier` (LAND), and `A7_PaintGraphite` and `A7_Aluminum` (ship). Remap them to the existing materials.
- **UVs** are world-scale: 1 UV unit = 1 m.

## Verification

- **Blender 5.3 FBX round trip** (`lss_build_roundtrip.json`): **PASS, 14 checks, 0 failed.** It checks every object (name, triangles, pivot, bounds within 1 mm, material slots) and every anchor.
- **Unity 6000.3.28f1 import** (`unity_check/`; my isolated project, the exact FBX, the ship's importer settings):
  - all 7 renderers' world bounds and all 7 anchors equal the manifest, with a maximum deviation of 0.3 mm (float precision at 2.5 km);
  - rotation 0 and scale 1 on every node;
  - **17,032 triangles in Unity = 17,032 in the FBX.** There are no lathe spheres, so nothing gets welded away.
- **Budget:** 17,032 triangles (limit 20,000); one new 1024² map set.
- **Not verified:**
  - the look in root's scene, with its lighting, ground colour and fog;
  - depth precision at a distance on root's depth format. The ground mounds sit at least 2 cm above the ground at their edges.

## Rebuild

Needs Blender 5.3, and Python 3 with NumPy and Pillow.

```
python source/lss_textures.py <out>/textures
blender -b --factory-startup --python source/build_surroundings.py -- --out <out> --textures <dir with LSS_, LS_, A7_Paint_, A7_Brushed_ maps and ls_markings.json> --land-src docs/art-direction/land-launch-site-v01/source [--render --samples 32]
blender -b --factory-startup --python source/roundtrip_habitat.py -- --out <out> --report lss_build_report.json
python source/make_sheet.py <out>/renders
```

- Fixed seeds, so the result is the same on every run.
- The .blend's preview context (the LAND kit, a vehicle and mast proxy, a brown ground plane, sky, sun) is not exported.

Made by Claude Opus 5.5 (AI) for Apoliak7777's team. All geometry and maps are original and procedural; no third-party assets were used.
