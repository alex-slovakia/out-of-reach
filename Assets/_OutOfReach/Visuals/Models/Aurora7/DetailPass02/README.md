# SHIP-DETAIL-02: AURORA-7 interior detail pass (first bounded package)

Procedural detail that makes the ship's rooms look inhabited and mechanically believable in microgravity. It is delivered room by room, and each room imports on its own.

| Room | FBX | Status |
|---|---|---|
| Engineering | `Meshes/DP02_Engineering.fbx` | done and checked (round trip, Unity import, clearance) |
| Crew habitat | `Meshes/DP02_Habitat.fbx` | not started in this checkpoint |
| Cockpit | `Meshes/DP02_Cockpit.fbx` | not started in this checkpoint |

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

## Kit (`Source/dp02_kit.py`)
The pieces are functions that are instanced in deliberate clusters; nothing is scattered at random:
- `ring_band`, `solid_lathe`;
- `loom`, `cable_tie`, `p_clamp`, `round_connector`;
- `quarter_turn`, `barrel_hinge`, `junction_box`, `service_panel`;
- clusters: `frame_loom_cluster`, `access_panel_cluster`.

Every piece keeps a 0.5 mm gap above its host surface. The placements live in `Source/dp02_layout.py`.

## Rebuild
From `DetailPass02/`, with Blender 5.3 and Python 3 with numpy:
```
blender -b --factory-startup --python Source/build_dp02.py -- --rooms Engineering
blender -b --factory-startup --python Source/roundtrip_habitat.py -- --out . --report "Source~/reports/dp02_build_report.json"
blender -b --factory-startup --python Source/build_dp02.py -- --rooms Engineering --check
blender -b --factory-startup --python Source/build_dp02.py -- --rooms Engineering --render
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

## Unfinished
- Crew habitat: strapped stowage, ration and water packs, berth personal items, and plumbing at the grow module.
- Cockpit.
- The contact sheet.
- Material slots show as `null` in the isolated Unity project only, because the A7 material assets are not in that project. In the game project, `externalObjects` points them at the existing assets. This is not tested in the game project.
