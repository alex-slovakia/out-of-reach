# HABITAT-FIT-02 — placeable habitat layout for AURORA-7 Storage

**Status:** a derivative art package for review, made at Codex's request on 14 November 2026 at 17:42 UTC (#7 comment 6881635025).
- The **live Aurora7 assets, Bootstrap, collision and runtime files are not changed.**
- Nothing here is integrated in Unity.
- The renders below are genuine Blender 5.3 Cycles stills of exactly this layout. They are not Unity captures.

![Storage with the habitat, wide](renders/HB_fit_fit_wide.png)
![Port wall: berth and galley](renders/HB_fit_fit_left_wall.png)

More views:
- [grow cassette on the starboard wall](renders/HB_fit_fit_grow.png)
- [view from the SleepCamera anchor](renders/HB_fit_fit_sleep_camera.png)

## What is in this folder

| File | Contents |
|---|---|
| `A7_Storage_Habitat.fbx` | **Derivative Storage shell** (one object, 25,690 triangles, origin 0 like `A7_Storage`). It is built by the ship's own generator at `ba8b87f` (the ship inside `33f474f`), consuming the shared label sequence exactly as the live build does, so the deck IDs and signs match the live Storage. |
| `HB_Habitat_Placed.fbx` | The 7 kit objects and 7 anchor empties **already placed in ship space**. Pivots sit on their hinge or slide lines. This is a convenience copy: the same units are also in `../HB_*.fbx` in unit space. |
| `HB_Habitat_Collision.fbx` | The 5 added collision boxes, `COL_Habitat_*`, in ship space. |
| `HB_StorageHabitat.blend` | All of the above in one editable scene (colliders shown as wire, hidden in renders), with texture paths relative to `textures/`. |
| `hb_integration_manifest.json` | Placements, anchors with forward directions, motion axes in ship space, collider changes and clearances. |
| `hb_integration_report.json`, `hb_integration_roundtrip.json` | Build report and FBX round-trip result. |
| `source/` | `build_integration.py` and `roundtrip_habitat.py`. They use the kit scripts in `../source/`. |

## Applying it (root's call)

1. **Swap the shell.** Use `A7_Storage_Habitat` instead of `A7_Storage`, with the same pose (prefab space, origin 0). Every other Aurora7 module stays as it is.
2. **Bays.** The shell is built with these bays (bay centres at z 11.75, 13.25, 14.75 and 16.25):
   - port (−x) wall: blanket, –, –, blanket;
   - starboard (+x) wall: blanket, blanket, –, blanket.
3. **Place the units** in ship space, rotating about +y only:

   | Unit | Position (m) | Yaw |
   |---|---|---|
   | `HB_CrewBerth` | (−2.38, 0, 13.25) | +90° |
   | `HB_Galley` | (−2.38, 0, 14.75) | +90° |
   | `HB_GrowCassette` | (+2.38, 0, 14.75) | −90° |

   Alternatively, take the objects of `HB_Habitat_Placed.fbx` as they are.
4. **Change the colliders.**
   - Remove `COL_Storage_LockersL` (−1.95, 1.3, 14.0; size 0.65 × 2.6 × 4.5) and `COL_Storage_LockersR` (+1.95, 1.3, 14.0; same size). They belonged to the removed stowage walls.
   - Add the five boxes below (centre and size in metres, ship space):

   | Collider | Centre | Size | Note |
   |---|---|---|---|
   | `COL_Habitat_Berth` | (−2.1125, 1.275, 13.25) | 0.535 × 2.55 × 1.446 | whole berth envelope |
   | `COL_Habitat_Galley` | (−2.145, 1.2, 14.75) | 0.47 × 2.4 × 1.446 | cabinet |
   | `COL_Habitat_GalleyDrawerOpen` | (−1.785, 1.675, 14.395) | 0.25 × 0.2 × 0.67 | disable when the drawer is closed |
   | `COL_Habitat_GalleyTable` | (−1.7485, 0.9025, 14.47) | 0.343 × 0.297 × 0.84 | disable when the table is stowed |
   | `COL_Habitat_GrowCassette` | (2.1175, 1.227, 14.75) | 0.525 × 1.654 × 1.006 | from 0.40 m above the deck |

   Every other live collider stays.

## Anchors in ship space

| Anchor | Point (m) | Forward |
|---|---|---|
| SleepEntry | (−1.58, 0, 13.03) | −x, towards the berth |
| SleepCamera | (−2.14, 1.96, 13.03) | +x, out of the berth |
| PersonalStowage | (−1.83, 1.25, 13.87) | +x |
| RationPickup | (−1.72, 1.72, 14.395) | +x |
| WaterDispenser | (−2.08, 1.13, 15.195) | +x (the nozzle points down) |
| GrowInteraction | (1.85, 1.125, 14.75) | −x |
| GrowTray | (2.12, 1.21, 14.75) | +y |

The empties carry positions only; forward directions are in the manifest, for your wrappers.

**Motion axes in ship space:**
- `HB_GalleyTable` hinges about z at (−1.92, 0.97, 14.75).
- `HB_GrowLid` hinges about z at (1.89, 1.655, 14.75).
- `HB_GrowTray` slides along −x from (1.91, 1.11, 14.75).
- `HB_GrowSeedlings` shares the tray's pivot.

## Clearances (measured on the collider boxes)

| Situation | Free width across the room (galley to grow cassette) |
|---|---|
| Everything stowed | **3.765 m** |
| Galley table deployed | **3.432 m** (your 3.434 m) |
| Table deployed and grow tray pulled out 0.30 m | **3.132 m** |

- Opposite the berth, the starboard wall has a blanket bay; 4.225 m are free to its lining.
- The units span z 12.527 to 15.473, clear of the portal wings at z 10.91–11.09 and 16.91–17.09.
- The graphite ring-frame flanges at z 12.5, 14.0 and 15.5 enter the berth and galley side panels by 8 mm. Each flange is 0.07 m deep and centred on its rib; the 1.446 m units leave 0.027 m to the rib. The overlap is hidden inside the panels, and no collider is involved. (The first version of this README said 5 mm; the overlap test below measured 8 mm.)

## Removed or moved features

- **Removed:** the live Storage room's combined soft stowage wall on the port side (soft bags, transit cases and lockers, with their part-number labels) and the hard stowage wall on the starboard side (lockers and hard cases).
- No Aurora7 anchor sat on them; all ten V01 anchors are elsewhere. **But if a Bootstrap target is placed against those walls** (for example C05 cargo bay targets in Storage), it needs a new home. That is root's decision.
- **Moved:** nothing else. The deck, ceiling, side walls, ring frames, the z 11 portal and its signs are the live ones.

## Checks actually run

| Check | Result |
|---|---|
| Blender 5.3.0 build and export (`build_integration.py`) | exit 0, 3 FBX files, `.blend` with relative texture paths only (verified on reopening) |
| FBX round trip (`roundtrip_habitat.py --report hb_integration_report.json`) | **PASS, 20/20**: exact triangle counts; pivots, bounds and anchors within 1 mm; material slots match |
| Cycles renders of this layout | 4 stills, inspected |

**Not verified:** the Bootstrap swap, player collision in play, performance.

## Self-review (14 November, 18:15 UTC, as Codex asked at 18:01 UTC)

**Unity import check.**
- **Setup:** Unity 6000.3.28f1, in a throwaway folder of the model writer's clone that was deleted afterwards. Importer settings are the AURORA-7 ship's: scale 1, file scale, Bake Axis Conversion, material descriptions.
- **Scope:** all 6 FBX files, the three unit-space units in `../` and the three files here, with all 34 objects and anchors.
- **Result:** pivots, rotations (all 0), scale (1), bounds (within 2 mm), material slot names, anchor positions and triangle counts **all match the build reports**. Unity welds no triangles away: 100,170 in total.
- `A7_Storage_Habitat.fbx` has one object, so Unity puts its mesh on the root GameObject, as it does for the single-module ship FBX files.

**Edge-collision test (Blender, world-space triangle overlap of the delivered files).**
- **Between units:** no overlaps, including the deployed galley table against the grow lid and tray.
- **Between units and the derivative shell,** only these, all hidden or intended:
  - the ring-frame flanges named above, 8 mm into the side panels;
  - the deck's wall-side cable trough, kick strip and two deck bolts per unit end, which run inside the units' plinths;
  - the rear plates sitting flush on the wall lining;
  - the berth's supply duct and the grow cassette's power cable ending in the wall lining, as designed.
- No visible clipping was found.

**Materials and maps.**
- The FBX files carry no texture records, so materials bind by name.
- Every material's maps are listed in `hb_integration_manifest.json`, and all of them are in `textures/` (32 files).
- The `.blend` reopens with relative paths only.

## Rebuild

```
blender -b --factory-startup --python source/build_integration.py -- --out . --kit <habitat build folder with textures/hb_labels.json>
        --ship-source <Assets/_OutOfReach/Visuals/Models/Aurora7/Source at ba8b87f> --ship-textures <.../Aurora7/Textures> [--render]
blender -b --factory-startup --python source/roundtrip_habitat.py -- --out . --report hb_integration_report.json
```
