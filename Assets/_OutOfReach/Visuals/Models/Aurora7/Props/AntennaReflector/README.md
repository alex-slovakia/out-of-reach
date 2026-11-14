# AntennaReflector — reflector assembly for the ship's high-gain antenna

The part approved in issue #7 on 14 Nov 2026 as option B for asset A07: a more detailed replacement for
`Antenna_Elevation`, the elevation part of the antenna on `feature/aurora7-models`. Everything else of that
antenna stays as it is: base housing, service deck, manual drive socket, mast, `Antenna_Azimuth` and the three
objects the integrator binds. Authored by the prop chat (Claude / Apoliak, "Blender fork") in Blender 5.3 from a
script and checked in Unity in a private clone. The model writer does the swap.

It is one mesh: trunnion, hub block, neck and flange, a 1.24 m reflector with twelve gore seams and a ring
seam baked into the texture, eight tapering ribs and a stiffening ring behind it, a tripod and a feed horn.
No gameplay state, scripts, camera, light, collider or audio listener.

## Interface

| Fact | Value |
|---|---|
| Object | `Reflector`, the only object in `AntennaReflector.fbx`; identity rotation, unit scale, normals and UV0 |
| Pivot | centre of the trunnion |
| Axes in Unity | X = trunnion, Y = hull normal at the pad (outboard), Z = aft along the hull. This is the local frame of `Antenna_ElevationPivot` |
| Delivered pose | the pose of the part it replaces: boresight (0, 0.866, 0.5), which is 30° aft of the hull normal |
| How to mount | child of `Antenna_ElevationPivot` with zero local position and identity local rotation, in place of `Antenna_Elevation` |
| Elevation | whatever turns the pivot about its local X turns this part the same way: a positive angle lowers the boresight towards aft, a negative one raises it towards the hull normal |
| Size in Unity (X × Y × Z) | 1.252 × 0.758 × 1.092 m; bounds min (−0.626, −0.104, −0.354), max (0.626, 0.653, 0.738) |
| Triangles | 3,638 |
| Material | one URP Lit material, `AR_AntennaReflector` |
| Textures | 3 × 2048²: BaseColor, MetallicSmoothness (metallic in RGB, smoothness in alpha), Occlusion |

## Envelope against the part it replaces

Dimensions of the replaced part are read from `antenna()` and `dish()` in `Source/a7_ship.py` at `4ccc17c`.

| | `Antenna_Elevation` at `4ccc17c` | `Reflector` |
|---|---|---|
| Trunnion | 0.36 m long, radius 35 mm | the same |
| Hub block | 0.22 × 0.16 × 0.16 m, square to the hull | the same size, square to the boresight |
| Reflector vertex | 0.20 m from the trunnion axis | the same |
| Rim radius, depth | 0.626 m, 0.19 m | the same; the rim has a 4 mm lip, so its front is 0.392 m from the trunnion axis instead of 0.390 m |
| Feed | radius up to 55 mm, tip 0.02 m beyond the focus, 0.726 m from the trunnion axis | the same limits |
| Largest radius swept about the trunnion | 0.738 m | 0.738 m |
| Behind the reflector | hub cylinder reaching 75 mm behind the vertex at a radius of 0.13 m, eight tubes 24 mm proud of the back, one ring | flange reaching the same 75 mm at the same radius, eight ribs 55 mm deep at the hub tapering to 16 mm at the rim, one ring 40 mm deep at a radius of 0.37 m. Between the hub and the ring the ribs stand up to 30 mm further out than the tubes did |

So the dish clears the yoke, the deck and the hull exactly where the old part did. As before, nothing limits
the travel: a large elevation change still puts the dish through the yoke or the hull.

## Files

- `AntennaReflector.prefab` — the model with its material. No collider, like the antenna it belongs to.
- `AntennaReflector.fbx` — one triangulated mesh with exported normals, no texture records and no local paths.
  Its `.meta` remaps the material to `Materials/` and keeps the object as a child of the model root.
- `Materials/`, `Textures/` — as listed above. The textures are clamped atlases; MetallicSmoothness and Occlusion are linear.
- `Previews/` — five genuine Cycles renders of this mesh with these textures (`hero`, `back`, `detail`, `lowered`,
  `wire`) and two captures made in Unity by the import check (`AntennaReflector_unity_urp.png`,
  `AntennaReflector_unity_urp_lowered.png`). The grey yoke and mast in the Cycles renders are stand-ins built
  from the ship model's dimensions; they are not part of this prop. The Unity captures use two plain
  directional lights and no tone mapping; they show that the import works, not final lighting.
- `Source/` — `build_antenna_reflector.py` regenerates the model, baked textures, renders, FBX and `.blend`:
  `blender -b --factory-startup --python build_antenna_reflector.py -- --stage full --out <folder outside Assets>`.
  `verify_fbx.py` re-imports an FBX and reports what a consumer receives. `AntennaReflector_report.json` is the
  report of the build that produced these files. No `.blend` is included: a saved `.blend` stores the absolute
  path it was saved to, and the script rebuilds it in under two minutes.

## What must stay together

The prefab points at the FBX and the material by GUID, the material points at the textures by GUID, and the
FBX `.meta` holds the material remap and import settings. Keep the `.meta` of every **file** unchanged. The
folder may be renamed or moved freely as long as each file travels with its `.meta`.

## Notes

- **Colours.** Hub, neck and flange use the ship's `A7_PaintGray` (#8A8884) so they sit with the yoke; the
  reflector is a warm white (#D9D8D1); ribs, ring, tripod and trunnion are clear anodised aluminium (#B6B9BC);
  the feed can is graphite (#2B2B2C).
- **Occlusion** was baked for this part alone, so it carries no shadow of the yoke.
- **Relation to HGA-1.** `Props/HighGainAntenna` (branch `feature/prop-high-gain-antenna`) is the whole antenna
  this reflector was derived from. The integrator did not select it for this checkpoint; nothing here needs it.

## Licences and sources

| Item | Source | Licence |
|---|---|---|
| Geometry, UVs, baked textures | generated by `Source/build_antenna_reflector.py`; no external model or texture | original team work |
| Tool | Blender 5.3.0 (Cycles) | GPL tool; its output carries no licence obligation |

Reference for function only: gimballed high-gain dish antennas on spacecraft. No NASA model, texture, drawing,
photograph, logo or insignia is copied or included, and no third-party game asset or downloaded texture is
used. The dish geometry is the ship model's own (1.24 m, depth 0.19 m, prime-focus feed), not an RF design.

## Verification actually performed (14 Nov 2026, Apoliak's machine)

- **Blender 5.3.0, Cycles/OptiX.** Full build exit 0: 1.252 × 1.092 × 0.758 m (Blender X, Y, Z), 3,638 triangles,
  five renders written.
- **FBX round trip.** `verify_fbx.py` re-imported the FBX into an empty Blender scene: same size, 3,638
  triangles, only triangles, one object at unit scale with its origin on the trunnion, custom normals present,
  one UV map, no texture reference. The file contains no drive-letter path.
- **Unity 6000.3.28f1, URP**, batch run in a private clone of the repository: exit 0, no validation problem.
  Measured there: bounds (1.252, 0.7575, 1.0923) m with min (−0.626, −0.1043, −0.3544); 3,638 triangles; the
  object at the root's origin with identity rotation and unit scale; the renderer uses the external URP Lit
  material; no collider, camera or audio listener. The second Unity capture has the object turned 35° about
  its local X, which lowers the dish and shows that the pivot sits on the trunnion.
- `tools/verify_repository.py` passes in that clone with this folder in place (every file has a `.meta`, GUIDs unique).

## Limits

- **Not verified: the swap itself.** The part was checked on its own, against dimensions read from the ship
  model's source, not inside the ship prefab. The fit on the yoke, the look beside the ship's materials and the
  lighting of the EVA scene are for the model writer's import check.
- One LOD, no lightmap UVs, no normal map. Wear is deliberately light. Base texture about 1.4 mm per texel
  (712 px/m); the build script takes `--tex 4096` for a finer one.
- The reflector is one smooth shell; its panel seams exist only in the texture. Nothing is animated, and there
  is no damaged state.
- Not checked: a Windows player build, performance.
