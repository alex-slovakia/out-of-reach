# SHIP-DETAIL-02: AURORA-7 interior detail pass (first bounded package)

Procedural detail that makes the ship's rooms look inhabited and mechanically believable in microgravity. It is delivered room by room, and each room imports on its own.

| Room | FBX | Status |
|---|---|---|
| Engineering | `Meshes/DP02_Engineering.fbx` | done and checked (round trip, Unity import, clearance) |
| Crew habitat | `Meshes/DP02_Habitat.fbx` | done and checked (round trip, Unity import, clearance) |
| Cockpit | `Meshes/DP02_Cockpit.fbx` | done and checked (round trip, Unity import, clearance) |

**Total: 58,882 triangles.** It uses 21 existing materials (19 `A7_*` plus `HB_BagQuilt` and `HB_Labels`) and adds no new materials or textures.

`DetailPass02Layout.json` is the placement manifest. It covers only the rooms that are present.

## How to place it
- **Placement.** Drop each FBX at the AURORA-7 ship origin with identity rotation and scale 1. Each object is already in ship coordinates (Unity metres, y up, deck at y = 0).
- **Pivots.** An object's pivot is the origin of its mount frame: a point on its host surface at deck level under the cluster.
- **Importer.** Every FBX `.meta` copies the habitat FBX importer: Bake Axis Conversion on, file units and scale, global scale 1, no cameras or lights. It also remaps every material by name onto the existing `A7_*` asset (`externalObjects`). No new materials or textures are added.
- **Static only.** There are no colliders, no switches and no new gameplay anchors. `DP02_*_ReviewEye` is an empty that marks the preview camera.

## Engineering (18,312 triangles, 11 existing materials)
| Object | Host | Triangles | What it is |
|---|---|---|---|
| `DP02_ENG_FrameLoom_L_Z460` | Port ring-frame flange face, x = -2.204, z = 4.6 | 7,378 | Four-cable power loom. It comes out of a flange grommet under the shoulder, runs through four cushioned P-clamps on hex standoffs and four cable ties, and ends at a junction box: bevelled lid, gasket, four quarter-turn fasteners, CAUTION label. Four round plug connectors with backshells and boots sit on top. Two feeders with backshells drop beside the flange edge into deck-trough grommets. |
| `DP02_ENG_FrameLoom_R_Z940` | Starboard ring-frame flange face, x = +2.204, z = 9.4 | 7,378 | The same cluster, mirrored by its mount frame, with its own connector labels. |
| `DP02_ENG_AccessPanel_AftL` / `_AftR` | Aft portal wings, engineering face z = 10.92, x = -/+1.98 | 1,778 each | Flush access panel: stepped bevel bezel, black gasket line, bevelled door, two barrel hinges (outboard), four quarter-turn fasteners, fastener note, part number and barcode. |

Labels are taken from the existing AURORA-7 decal atlas (`A7_Decals`), so they are fictional ship text with no agency marks.

## Crew habitat (19,476 triangles, 18 existing materials: 16 `A7_*` plus `HB_BagQuilt` and `HB_Labels`)
The habitat room is the integrated storage-room habitat, `../../Habitat/Meshes/A7_Storage_Habitat.fbx`, together with `HB_Habitat_Placed.fbx`. Nothing was placed on or in the existing berth, galley or grow units, apart from the fittings that are mounted on the grow cassette's side faces.

| Object | Host | Triangles | What it is |
|---|---|---|---|
| `DP02_HAB_StrapStow_R_Z1325` | Starboard blanket bay at z 13.25, opposite the berth. The host plane is the quilt face plus 3.5 mm, which keeps it clear of the quilt's decals. | 7,596 | **Above the bay handrail:** two canvas soft bags with girth straps and buckles (the ship's `soft_bag`). Two cargo straps with cam buckles cross them and are tied down to D-rings on the edge strips, placed between the snaps. **Below the rail:** three labelled ration pouches (breakfast, meal, snack) and three drink packs with caps, held by blue elastic cords on anchor points, with RATIONS · MEALS and RATIONS · DRINKS placards. |
| `DP02_HAB_CrewKit_L_Z1175` | Port blanket bay at z 11.75, beside the berth entrance | 3,626 | A spare sleeping bag rolled up (`HB_BagQuilt`, the berth bag's fabric) with two keeper bands. Two tie-down straps with cam buckles hold it between two riveted anchor rails. Below it is a personal board: a hardcover notebook with an elastic closure and a tethered pen on a hook-and-loop patch, a photo card under an elastic strap, and over-ear headphones hung from a hook-and-loop strap, with their cable. |
| `DP02_HAB_GrowPlumbing_R_Z1414` | Starboard wall strip at z 14.04 to 14.25, between ring frame z 14.0 and the grow cassette's forward side face | 5,590 | Potable supply (blue anodised) and return lines rise from deck-trough grommets. They pass through two twin-pipe clamp blocks on wall brackets, past capped needle valves (lock-wired caps, no levers), to hex unions. Corrugated flex hoses with ferrules run from there to bulkhead fittings on the cassette's side face. A POTABLE placard sits above. |
| `DP02_HAB_GrowCables_R_Z1536` | Starboard wall strip at z 15.25 to 15.46, between the cassette's aft side face and ring frame z 15.5 | 2,664 | Grow-light feed: two cables run in three cushioned P-clamps on standoffs, with three cable ties, from a trough grommet up to two plug connectors with backshells on the cassette's side face. Connector labels sit beside them. |

## Kit (`Source/dp02_kit.py`)
The pieces are functions that are instanced in deliberate clusters; nothing is scattered at random:
- `ring_band`, `solid_lathe`;
- `loom`, `cable_tie`, `p_clamp`, `round_connector`;
- `quarter_turn`, `barrel_hinge`, `junction_box`, `service_panel`;
- clusters: `frame_loom_cluster`, `access_panel_cluster`.

## Cockpit (21,094 triangles, 15 existing materials)
| Object | Host | Triangles | What it is |
|---|---|---|---|
| `DP02_CKP_FrameLoom_L_Z000` | Port cockpit ring-frame flange (-2.804, 0, 0.0), beside and behind the commander's seat | 7,378 | The kit's frame loom cluster, with connector labels 0 to 5. |
| `DP02_CKP_FrameLoom_R_Z150` | Starboard cockpit ring-frame flange (2.804, 0, 1.5). It is not on frame z 0.0 because the deep stowage bay at z -0.75 is next to that frame. | 7,378 | The same cluster, with labels 8 to 13. |
| `DP02_CKP_CrewNotes_L_Z075` | Port cockpit blanket bay at z 0.75, behind the commander's seat, above the bay rail | 2,782 | The flight log: a larger hardcover notebook with a tethered pen, held by a strap with a cam buckle between two tie-down points. Also a laminated procedure card (`p_repair` from `A7_Decals`) under an elastic strap, and a comm headset on a hook-and-loop strap. |
| `DP02_CKP_AccessPanel_AftL` / `_AftR` | Wings of the portal at z 3, cockpit face z 2.92, x = -/+1.98 | 1,778 each | The kit's access panel. |

Nothing is placed on the console, the seats, the forward bulkhead, the windows or any control panel.

Habitat pieces (`Source/dp02_habitat.py`):
- straps and stowage: `strap`, `cam_buckle`, `strap_anchor`, `pouch`, `elastic`, `placard`;
- personal items: `notebook`, `photo`, `headphones`, `rolled_bag`, `wrap_strap`;
- plumbing: `sweep`, `flex_hose`, `hex_union`, `capped_valve`, `twin_clamp`, `bulkhead_fitting`;
- clusters: `stowage_cluster`, `crew_kit_cluster`, `grow_plumbing_cluster`, `grow_cable_cluster`.

The cockpit has one extra piece, `checklist_card`, and one cluster, `crew_notes_cluster`, both in `Source/dp02_cockpit.py`.

Every piece keeps a 0.5 mm gap above its host surface. The placements live in `Source/dp02_layout.py`. `Source/hb_labels_map.json` is the UV map of the delivered `HB_Labels` atlas: SHA-256 `47805ca0…` matches `Habitat/Textures/HB_Labels.png`.

## Rebuild
From `DetailPass02/`, with Blender 5.3 and Python 3 with numpy:
```
blender -b --factory-startup --python Source/build_dp02.py -- --export Engineering,Habitat,Cockpit
blender -b --factory-startup --python Source/roundtrip_habitat.py -- --out . --report "Source~/reports/dp02_build_report.json"
blender -b --factory-startup --python Source/build_dp02.py -- --rooms Engineering,Habitat,Cockpit --check
blender -b --factory-startup --python Source/build_dp02.py -- --rooms Engineering,Habitat,Cockpit --render [--views Cockpit]
python -B Source/make_sheet.py
python -B Source/make_metas.py
python -B Source/make_manifest.py
```
- **Unity check.** Copy `Meshes/DP02_*.fbx` together with their `.meta` files into `Assets/DP02` of an empty Unity 6000.3 project, and `Source~/unity_check/DP02Check.cs` into `Assets/Editor`. Run Unity with `-batchmode -nographics -executeMethod DP02Check.Run`. Then run `python -B "Source~/unity_check/dp02_unity_compare.py" dp02_unity_check.tsv "Source~/reports/dp02_build_report.json"`.
- **Editable source.** `Source~/DP02.blend` uses relative texture paths. Unity ignores `Source~` and `Previews~`.

## Evidence for Engineering (in `Source~/reports/`)
- **Blender round trip** (`dp02_build_roundtrip.json`): PASS. Five checks covering 4 objects and 1 anchor: names, triangles, pivots, bounds and material slots, with 1 mm tolerance.
- **Isolated Unity 6000.3.28f1 import** of the exact FBX and `.meta` (`dp02_unity_compare.json`): PASS.
  - The importer reads Bake Axis Conversion on, file units and scale on, global scale 1, cameras and lights off, and 11 material remaps.
  - World bounds, pivots and the anchor match the report with 0.0000 m error.
  - Unity counts 18,312 triangles, the same as the report. The sub-mesh count equals the material count for every object.
- **Clearance and overlap** (`dp02_check.json`). The ship is rebuilt from `../Source/a7_ship.py`, with the integrated habitat FBX in place of the storage room. Every BVH is built in world space from `matrix_world @ v.co`.
  - Overlap with ship and habitat geometry: 0 triangle pairs for every object.
  - Mount gap: 0.5 mm for every object (seated, not floating). Maximum standoff from the host: 0.087 m (loom junction box) and 0.019 m (panels).
  - Nearest amber handrail or translation aid: 0.143 m (`AftR` panel, to the science bay rail at z 10.85). The others are 0.32 to 0.54 m away.
  - Nearest control: 0.181 m (looms, to the equipment-bay switch panel). The panels are 0.45 m and 4.06 m away. Gameplay parts are at least 0.98 m away.
  - Airlock door sweep: 5.95 m or more. Corridor box (|x| < 0.9, y < 2.5): 0.91 m or more. Corridor centreline: 1.82 m or more.
- **Preview** (`Previews~/DP02_Engineering.png`): a Cycles render inside the real ship, built by the generator with its textures, from the `DP02_ENG_ReviewEye` anchor.

## Evidence for the crew habitat
- **Blender round trip:** PASS for both FBX files, 10 checks.
- **Isolated Unity import:** PASS.
  - Importer settings are as above, with 18 material remaps.
  - Unity counts 19,476 triangles, the same as the report.
  - The maximum bounds and pivot error is 0.0001 m, which comes from the report's 4-decimal rounding.
  - The first import found 2,400 zero-area hose triangles, which Unity dropped. The rings had collapsed because the sweep reference was parallel to the hose start. This is fixed in `frames_along`, and all clusters now have 0 zero-area triangles.
- **Clearance and overlap** (the room is the integrated habitat FBX):
  - Overlap: 0 triangle pairs for all 4 objects.
  - Mount gap: 4.0 mm for the stowage (host plane 3.5 mm off the quilt) and 1.6 mm for the crew kit (to a quilt decal). The plumbing and cables are at 0.5 mm.
  - Maximum standoff: 0.289 m (the bags).
  - Nearest amber handrail: 0.251 m or more.
  - Nearest control: 1.65 m or more. Nearest habitat anchor: 0.61 m (`GrowTray`).
  - Habitat colliders: the stowage is 0.27 m from them and the crew kit 0.295 m from `COL_Habitat_Berth`. The fittings and connectors on the cassette's side faces lie 2.5 mm inside the `COL_Habitat_GrowCassette` box, because that box is 3 mm larger than the cassette.
  - Airlock door sweep: 1.47 m or more. Corridor box: 1.13 m or more. Corridor centreline: 2.07 m or more.
- **Previews:**
  - `Previews~/DP02_Habitat.png`, from `DP02_HAB_ReviewEye`;
  - `Previews~/DP02_HabitatCrewKit.png`;
  - `Previews~/DP02_HabitatGrowPlumbing.png`.

## Evidence for the cockpit
- **Blender round trip:** PASS for all three FBX files. 16 checks, 0 failed.
- **Isolated Unity import:** PASS. Importer settings are as above, with 15 material remaps. Unity counts 21,094 triangles, the same as the report. Bounds, pivots and the anchor match with 0.0000 m error.
- **Clearance and overlap:**
  - Overlap: 0 triangle pairs for all 5 objects.
  - Mount gap: 0.5 mm (crew notes: 3.8 mm, from the quilt host plane). Maximum standoff: 0.087 m.
  - Nearest amber handrail: 0.264 m or more.
  - Nearest seat or control: 1.26 m (left loom to the commander's seat). The crew notes are 1.58 m away.
  - `AftL` panel: 0.49 m to an engineering-side switch panel behind the 0.16 m portal wing.
  - Corridor box: 0.91 m or more. Airlock door sweep: 13.9 m or more.
- **Previews:**
  - `Previews~/DP02_Cockpit.png`, from `DP02_CKP_ReviewEye`;
  - `Previews~/DP02_CockpitAft.png`;
  - contact sheet `Previews~/DP02_Sheet.png` (six tiles).

## Unfinished
- The photo on the crew board reuses `photo_hills` from `HB_Labels`. The same picture is one of the three photos inside the berth, because no new atlas was made.
- Material slots show as `null` in the isolated Unity project only, because the A7 material assets are not in that project. In the game project, `externalObjects` points them at the existing assets. This is not tested in the game project.
