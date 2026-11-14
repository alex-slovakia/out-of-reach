# AURORA-7 models — handoff (CODEX-006 / V01A)

Owner: Claude / Apoliak — the dedicated Fable model writer. Branch `feature/aurora7-models`.
Scope written: only `Assets/_OutOfReach/Visuals/Models.meta` and `Assets/_OutOfReach/Visuals/Models/**`.

**State: engineering bay pass against review-01 R02 (CODEX-008).** The first functional deliverable
(`9878304`) is followed by one focused model and material pass. Only the engineering bay was compared with
the reference; palette, ribs, deck, overhead services and handrails are shared kit parts, so the other
compartments received them without separate work and were not compared with their views yet.

## What is delivered

A remodel of the whole vessel as a believable near-future pressure vessel, grounded in ISS, Orion and Skylab
practice. Everything is function first:

- **Structure:** graphite ring frames, quilted insulation liner, lift-out grated aisle panels, cable trays,
  overhead pipe runs on trapeze hangers, luminaires and air diffusers. Warm putty-grey powder coat.
- **Flight deck:** five-pane framed observation window, overhead circuit-breaker panels, a three-display console
  with keyboards, two crew seats with harness and hand controllers.
- **Engineering (assets A01, A02, A03):**
  - **Power distribution rack** at PowerRepair: an open, recessed breaker cabinet with two rails of breakers,
    feeder trunk, wiring loom, main isolator, amber work light, stencilled fascia, emergency lamp and a
    latched drawer.
  - **Life-support scrubber rack** at LifeSupportRepair: three replaceable CO₂ sorbent canisters on an inlet
    manifold with an outlet header, sample line, gauge and sensor, a status display and a latched drawer.
  - Three composite pressure vessels, tool board, avionics, utility and close-out bays; the EPS-2 panel as the
    bus control panel on the wall beside the power rack.
- **Logistics:** soft stowage (cargo transfer bags behind bungee restraints) and hard lockers.
- **Airlock:** umbilical/pressure-control rack with depress valve and gauges, suit stowage station, two-leaf
  pressure doors with viewports.
- **Exterior:** pressure hull with frames and seams, forward window face, aft EVA hatch and porch, gold EVA
  handrails to the antenna work site, two solar wings, body radiators, eight reaction-control quads.

All geometry and textures are original and generated from the scripts in `Source/`. See `LICENSES.md`.

## Engineering bay against R02

`Previews/A7_unity_urp_engineering_r02.png` is the matched view: 1.65 m eye height, 90° horizontal field of
view, looking aft from just inside the forward hatch.

| R02 | Model |
|---|---|
| Corridor about 2 m wide between cabinet faces | **Not matched on purpose:** 3.46 m between rack faces, fixed by the V01 anchors and the collision envelope. No placement migration is proposed. |
| Warm putty-grey powder coat, graphite ribs | Done (materials `A7_PaintWhite`, `A7_PaintGray`, `A7_PaintGraphite`). |
| A03 open cabinet: breakers on rails, red/blue looms, yellow isolator with red handle, amber work light, stencilled fascia, drawer | Done. Seventeen breakers and a main breaker; one breaker is tripped. |
| A02: three white canisters with polished caps on a manifold, cyan display, stencilled fascia | Done. The polished caps need a reflection probe in the compartment to read as polished. |
| Overhead pipes, coloured cable, drops to the racks | Done, kept above 2.9 m. |
| Dark grating in the aisle | Done (`A7_Grate`). |
| Amber handrails and hatch trim | Done. |
| Dense wall equipment on both sides | Partly: close-out bays beside the racks are plain. |

## Folder map

| Path | Content |
|---|---|
| `Prefabs/Aurora7_Models.prefab` | **Use this.** Visual meshes, door leaves, engineering parts, props, collision proxies. No camera, light, listener or script. |
| `Meshes/A7_*.fbx` | Static modules: Cockpit, Engineering, Storage, Airlock, Exterior. Groups with their own pivots: Doors (four leaves), Parts (engineering parts and helper points). Collision. |
| `Meshes/a7_model_report.json` | Blender-side counts, bounds, part origins and helper points (Unity axes). |
| `Meshes/a7_unity_import_report.json` | Unity-side counts, bounds and validation result. |
| `Materials/*.mat` | 39 URP Lit materials created from `Source/a7_materials.json`. |
| `Textures/` | 11 tiling PBR sets (BaseColor, Normal, MODS), the label atlas and the display atlas, with their JSON maps. |
| `Props/ServicePanel/` | EPS-2 panel from the panel author's chat, imported unchanged (its own README, licences and previews). |
| `Previews/` | Genuine renders: `A7_unity_urp_*` from Unity URP (the delivered prefab), `A7_blender_cycles_*` from Blender Cycles (generated meshes only, so without the EPS-2 panel prop). `..._engineering_r02_cover_fitted` shows the cabinet with its cover; `..._exterior_finale` dates from `9878304`, the exterior is unchanged. |
| `Editor/Aurora7ModelTools.cs` | Editor-only: importer settings, materials, prefab, validation, preview stills. |
| `Source/` | `a7kit.py`, `a7_ship.py`, `a7_textures.py`, `build_aurora7.py`, `a7_materials.json` — the editable source. |

## Integration

Coordinates are those of the V01 handoff: metres, Y up, ship along +Z, deck at y = 0. **No anchor was moved.**
Instantiate the prefab at the ship origin with identity rotation and unit scale.

| Anchor (V01) | What the model has there |
|---|---|
| PlayerSpawn (0, 0.05, 0.8), yaw 180 | Clear deck between and behind the two seats, facing the console and window. |
| CockpitConsole (0, 1.1, −1.72) | Aft edge of the console work surface, centre display ahead. |
| PowerRepair (−1.65, 1.15, 6.1), yaw 90 | Centre of the open breaker cabinet. The bay opening is 1.04 × 1.36 m (y 0.50 to 1.86), 0.20 m deep; the rack face is 8 cm behind the anchor. |
| LifeSupportRepair (1.65, 1.15, 8.1), yaw −90 | Centre of the scrubber bay. The opening is 1.08 × 1.32 m (y 0.52 to 1.84), 0.26 m deep; the canister axes are at z = 8.24, 7.99 and 7.74. |
| AirlockInner (0, 0, 17) / AirlockOuter (0, 0, 21) | Hatchways with machined sealing lands; door leaves on these planes. |
| EvaStart (0, 0, 22) / EvaReturn (0, 0, 21.7) | On the EVA porch, a grated platform at deck level outside the aft hatch. |
| AntennaRepair (3.2, 2.4, 13), yaw −90 | Antenna mounting pad on the hull's upper starboard quarter; the anchor is 0.31 m off the skin. |
| FinaleCameraAnchor (9, 5, 27), yaw 205 | Sees the aft bulkhead, starboard flank, solar wing and antenna pad (`A7_unity_urp_exterior_finale.png`). |

### Hierarchy

`Visual/` (five static module meshes), `DoorLeaves/`, `EngineeringParts/`, `Props/`, `CollisionProxies/`
(46 `COL_*` box colliders).

- **Door leaves:** `DoorLeaf_Inner_L/R` and `DoorLeaf_Outer_L/R`. Each object's pivot is the V01 leaf position
  (inner ±1.4 open, outer ±0.45 closed) and each leaf is 0.88 × 2.46 × 0.12 m. Parent them under your leaf
  transforms at local zero, or slide them along X. They carry no collider and authorize nothing.
- **Engineering parts** (`EngineeringParts/`, all at identity rotation, so local axes are ship axes; no
  colliders, no behaviour):

  | Object | Pivot (ship metres) | Use |
  |---|---|---|
  | `PowerCabinet_Cover` | (−1.70, 1.18, 6.10), centre of the plate | The fitted close-out cover. **Inactive in the prefab**, so the cabinet reads as open. Enable it for a closed state. |
  | `PowerCabinet_IsolatorHandle` | (−1.83, 0.804, 5.85), on the shaft | Rotate about local **X**. Delivered vertical, pointing at I (on); O (off) is a quarter turn towards the bow. |
  | `PowerCabinet_TrippedLever` | (−1.8425, 1.482, 6.165), on the toggle axis | Breaker CB-05, delivered tripped (down). Rotating it **+70° about local Z** gives exactly the closed position of its neighbours (checked numerically). A natural origin for sparks. |
  | `PowerCabinet_AlarmLens` | (−1.71, 2.165, 6.71) | Red lens of the emergency lamp on the rack head, plain red paint. Swap its material for an emissive one in the alarm state. |
  | `ScrubberCanister_A/B/C` | (1.855, 0.80, 8.24 / 7.99 / 7.74), centre of the base port | Each lifts off its union nut; 0.76 m tall, 0.21 m diameter. |
  | `LightAnchor_PowerCabinetWork` | (−1.758, 1.72, 6.35) | Empty. Place for the amber work light; the emissive strip beside it is `A7_WorkLight`. |
  | `LightAnchor_PowerCabinetAlarm` | (−1.64, 2.165, 6.71) | Empty. Place for the red emergency light, in front of the lens. |

- **Props:** `ServicePanel_EpsControl`, the EPS-2 panel, on the wall plinth aft of the power rack: pivot at
  (−2.256, 0.55, 7.36), yaw 90. It was `ServicePanel_PowerRepair` on the rack face in `9878304`. Its
  `CollisionProxy` child is inactive here, see Collision. Cover hinge and lamp materials are described in
  `Props/ServicePanel/README.md`.
- **Lighting:** the prefab has no lights. Luminaire meshes hang at the V01 practical-light positions
  (x = 0, z = −1.6, 1.4, 4.4, 7.4, 10.4, 12.4, 15.4, 18.4), so the existing lights fit. Emissive materials:
  `A7_LightPanel` (warm white), `A7_WorkLight` (amber), `A7_Led*`, `A7_Screens`.
- **Display faces:** static idle pages from `Textures/A7_Screens.png`. They show no authoritative values
  ("SEE HUD", "SCHEMATIC ONLY"). Replace or overlay them with bound UI; the rectangles are in `a7_screens.json`.
- **Collision:** the proxies repeat the V01 solid envelope box for box (same centres and sizes as the collision
  boxes in `ShipVisualBuilder.cs`), so walkable space is unchanged. Visible meshes have no colliders. Both
  service bays are recesses inside the rack proxies, so the player cannot step into them. The EPS-2 panel's own
  collider is switched off because the panel now stands in walkable space beside the rack; it protrudes 0.21 m
  from the wall, about as much as the utility bay's duct. The exterior has no proxies yet because the EVA route is not
  decided.

### Differences from the blockout to be aware of

- Wall bays divide each compartment evenly, so the cosmetic ring frames are not at the blockout rib stations.
- The observation window keeps the same opening band but is five framed panes, not one sheet.
- `AntennaRepair` has yaw −90, which faces inboard; the mounting pad faces outboard. Nothing was changed.
- One mesh per compartment with one sub-mesh per material (21 to 29 each), as agreed for the first integration.

## Counts and bounds

Measured in Unity 6000.3.28f1 by the import check (`Meshes/a7_unity_import_report.json`).

| Object | Triangles | Materials | Bounds min → max (m) |
|---|---:|---:|---|
| `A7_Cockpit` | 75,568 | 28 | (−3, −0.05, −3.06) → (3, 3.20, 3) |
| `A7_Engineering` | 76,958 | 29 | (−2.4, −0.16, 2.84) → (2.4, 3.20, 11) |
| `A7_Storage` | 52,862 | 21 | (−2.4, −0.16, 10.84) → (2.4, 3.20, 17) |
| `A7_Airlock` | 31,084 | 25 | (−2.4, −0.16, 16.84) → (2.4, 3.20, 21.16) |
| `A7_Exterior` | 27,120 | 10 | (−12.37, −2.02, −3.59) → (12.37, 5.14, 23.4) |
| Door leaves (4) | 9,808 | 6–7 | at the two hatch planes |
| Engineering parts (8 meshes) | 5,248 | 1–4 | inside the two service racks |
| EPS-2 panel (3 meshes) | 6,734 | 5 | on the wall beside the power rack |
| **Total** | **285,382** | **42 distinct** | 39 textures, 19 renderers, 47 colliders of which 46 active |

Scale is 1 unit = 1 metre; every model root is at identity rotation and unit scale. Hull: 7.1 m diameter
around the flight deck, 6.0 m aft, 24.2 m long; solar wings span 24.7 m.

Textures: tiling sets are 1024² (rubber 512²), the label atlas 4096², the display atlas 2048². UV0 is
world-projected in metres for tiling materials and atlas-mapped for labels and displays. There is no
lightmap UV set and no LOD.

## Regenerate

```powershell
cd Assets\_OutOfReach\Visuals\Models\Aurora7\Source
python a7_textures.py                      # textures and atlases (numpy, Pillow)
& "C:\Program Files\Blender Foundation\Blender 5.3\blender.exe" -b --factory-startup --python build_aurora7.py -- --work <folder outside the project> [--render] [--no-export]
# then in Unity: OUT OF REACH > Models > Rebuild AURORA-7 materials and prefab
# or batch, with graphics: -executeMethod OutOfReach.ModelTools.Aurora7ModelTools.BuildAndCapture
```

The `.blend` and the Cycles stills are written to the `--work` folder. They are kept out of `Assets` on
purpose: a `.blend` inside the project makes Unity call Blender on import. `--no-export` renders stills
without rewriting the FBX files.

## Verification actually run

- **Blender 5.3.0** (build 2026-11-10, hash 02f0899d78e3), background mode, driven by the Python scripts here.
  No MCP connector. Build exit 0.
- **Unity 6000.3.28f1, URP 17.3 (`PC_RPAsset`)**, batch mode with graphics, in the model writer's own clone:
  - import, material creation, prefab build and validation exit 0, **0 problems**;
  - every renderer uses the external URP Lit materials, none missing;
  - three collision proxies at known asymmetric positions confirm axes, handedness and scale;
  - one part pivot from each rack and one helper point are checked against the generator's numbers to 1 cm;
  - the prefab contains no camera, light, audio listener or script;
  - eleven stills captured with `ShaderUtil.allowAsyncCompilation = false` and three warm-up frames each.
- `python tools/verify_repository.py` passes on the branch.

The preview lighting (warm spot lights at the luminaires, one amber point light at the work-light anchor, a
flat ambient term, a warm reflection box, ACES and a little bloom) exists only inside the capture method. It
is not delivered lighting.

## Not verified, not done

- Placement and lighting inside the integrated Bootstrap scene; a Windows build of this branch; performance.
- The 405-sample clearance validator from V01 was not run by me. Clearance is argued from identical proxies.
- Cockpit, stowage, airlock and exterior were not compared with R01, R03 to R06 and R08 yet.
- No antenna assembly (A07), no remodelled cargo bags (A04), no gloves and tether (A08).
- No wear and dirt pass, no damage-state decals, no baked ambient occlusion, simple seat backs, a flat
  forward hull face.
