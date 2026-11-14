# AURORA-7 models — handoff (CODEX-006 / V01A)

Owner: Claude / Apoliak — the dedicated Fable model writer. Branch `feature/aurora7-models`.
Scope written: only `Assets/_OutOfReach/Visuals/Models.meta` and `Assets/_OutOfReach/Visuals/Models/**`.

**State: engineering bay pass against review-01 R02 (CODEX-008).** The first functional deliverable
(`9878304`) is followed by one focused model and material pass. Only the engineering bay was compared with
the reference; palette, ribs, deck, overhead services and handrails are shared kit parts, so the other
compartments received them without separate work. The high-gain antenna (asset A07) followed as a second
step, because the EVA repair had nothing to repair before it, and the airlock (asset A05) was then compared
with R04. Cockpit, stowage and the exterior were not compared with their views yet.

## What is delivered

A remodel of the whole vessel as a believable near-future pressure vessel, grounded in ISS, Orion and Skylab
practice. Everything is function first:

- **Structure:** graphite ring frames, quilted insulation liner, lift-out grated aisle panels, cable trays,
  overhead pipe runs on trapeze hangers, luminaires and air diffusers. Warm putty-grey powder coat.
- **Flight deck:** five-pane framed observation window, overhead circuit-breaker panels, a three-display console
  with keyboards, two crew seats with harness and hand controllers.
- **Engineering (assets A01, A02, A03):**
  - **ARS-1 air revitalisation unit** (prop from the prop author's chat) on the wall forward of the scrubber
    rack: status lamps, gauge, two canister sockets, inlet fan, filter drawer and valve manifold.
  - **Power distribution rack** at PowerRepair: an open, recessed breaker cabinet with two rails of breakers,
    feeder trunk, wiring loom, main isolator, amber work light, stencilled fascia, emergency lamp and a
    latched drawer.
  - **Life-support scrubber rack** at LifeSupportRepair: three replaceable CO₂ sorbent canisters on an inlet
    manifold with an outlet header, sample line, gauge and sensor, a status display and a latched drawer.
  - Three composite pressure vessels, tool board, avionics, utility and close-out bays; the EPS-2 panel as the
    bus control panel on the wall beside the power rack.
- **Logistics:** soft stowage (cargo transfer bags behind bungee restraints) and hard lockers.
- **Airlock (asset A05):** sealed suit locker with a glazed door and the suit inside, a pressure station
  beside the outer hatch (dial with a driven pointer, display, two keys), intercom and conduits on the aft
  bulkhead, umbilical/pressure-control rack with depress valve, two-leaf sliding pressure doors with pressed
  panels and viewports.
- **Exterior:** pressure hull with frames and seams, forward window face, aft EVA hatch and porch, gold EVA
  handrails to the antenna work site, two solar wings, body radiators, eight reaction-control quads.
- **Antenna (asset A07):** a two-axis gimbal on the hull pad at AntennaRepair. The base housing carries an
  amber-edged service deck with the manual drive socket, an access cover, a tether point and two test
  connectors. The 1.24 m reflector assembly on the trunnion (gore seams, eight ribs and a ring behind the
  dish, tripod, feed horn) is the prop author's part, option B of the integrator's decision of 14 Nov.

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

## Airlock against R04

`Previews/A7_unity_urp_airlock_r04.png` is the matched view: 1.65 m eye height, 90° horizontal field of view,
from just inside the inner hatch towards the outer hatch and the starboard wall.

| R04 | Model |
|---|---|
| One hinged hatch with a single window | **Not matched on purpose:** the two sliding leaves of V01 are kept, with their names, pivots and size, because the hatch gates are already bound to them. Their look moved towards the board: pressed panels, edge dogs, bolted viewports, amber grab bars. |
| Pressure instrument beside the hatch | Done: dial, display and two keys on the aft bulkhead. The pointer is a separate object. The display shows no value. |
| Sealed white suit locker with the suit behind glass | Done, on the starboard wall inside the blockout's suit-station envelope. On the board it stands beside the hatch; here it is seen at an angle. The suit is a simple figure, not a sculpted one. |
| Conduits and wall boxes left of the hatch | Done: two conduits on the bulkhead stiffeners and an intercom. |

## Folder map

| Path | Content |
|---|---|
| `Prefabs/Aurora7_Models.prefab` | **Use this.** Visual meshes, door leaves, engineering parts, props, collision proxies. No camera, light, listener or script. |
| `Meshes/A7_*.fbx` | Static modules: Cockpit, Engineering, Storage, Airlock, Exterior. Groups with their own pivots: Doors (four leaves), Parts (engineering parts and helper points), AirlockParts, ExteriorParts (antenna gimbal and its helper point). Collision. |
| `Meshes/a7_model_report.json` | Blender-side counts, bounds, part origins and helper points (Unity axes). |
| `Meshes/a7_unity_import_report.json` | Unity-side counts, bounds and validation result. |
| `Materials/*.mat` | 39 URP Lit materials created from `Source/a7_materials.json`. |
| `Textures/` | 11 tiling PBR sets (BaseColor, Normal, MODS), the label atlas and the display atlas, with their JSON maps. |
| `Props/ServicePanel/` | EPS-2 panel from the prop author's chat (its own README, licences and previews). The FBX is its author's second export, which holds no local path; the saved Blender file is left out for the same reason, as its README recommends. |
| `Props/AntennaReflector/` | Reflector assembly of the antenna from the same chat, imported unchanged (manifest 39 of 39). It replaces the procedural reflector of `c1fa9c2` inside the same envelope. |
| `Props/LifeSupportRack/` | ARS-1 unit from the same chat, imported unchanged; identical to branch `feature/prop-life-support-rack` at `8c28054`. Its README still describes the rack-face mount it was built for. |
| `Previews/` | Genuine renders: `A7_unity_urp_*` from Unity URP (the delivered prefab), `A7_blender_cycles_*` from Blender Cycles (generated meshes only, so interiors without the EPS-2 and ARS-1 props; their wall plinths show instead. There is no Cycles exterior still, because the antenna's reflector is a prop too). `..._engineering_r02_cover_fitted` shows the cabinet with its cover. |
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
| AirlockInner (0, 0, 17) / AirlockOuter (0, 0, 21) | Hatchways with machined sealing lands; door leaves on these planes. The pressure station is on the aft bulkhead 1.45 m to starboard of AirlockOuter, 1.02 to 1.88 m above the deck. |
| EvaStart (0, 0, 22) / EvaReturn (0, 0, 21.7) | On the EVA porch, a grated platform at deck level outside the aft hatch. |
| AntennaRepair (3.2, 2.4, 13), yaw −90 | The antenna's manual drive socket, on the aft service deck of the base housing. The socket is on the anchor to within 1 mm and faces outboard along the hull normal, as agreed; the dish hangs about 0.65 m above the deck. |
| FinaleCameraAnchor (9, 5, 27), yaw 205 | Sees the aft bulkhead, starboard flank, solar wing and the antenna (`A7_unity_urp_exterior_finale.png`). |

### Hierarchy

`Visual/` (five static module meshes), `DoorLeaves/`, `EngineeringParts/`, `AirlockParts/`, `ExteriorParts/`,
`Props/`, `CollisionProxies/` (46 `COL_*` box colliders).

- **Door leaves:** `DoorLeaf_Inner_L/R` and `DoorLeaf_Outer_L/R`. Each object's pivot is the V01 leaf position
  (inner ±1.4 open, outer ±0.45 closed) and each leaf is 0.88 × 2.46 m, 0.13 m thick over its pressed panels;
  the grab bars stand out to 0.13 m from the leaf's centre plane on both faces. Parent them under your leaf
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

- **Airlock parts** (`AirlockParts/`, all at identity rotation, so local axes are ship axes; no colliders, no
  behaviour):

  | Object | Pivot (ship metres) | Use |
  |---|---|---|
  | `AirlockGauge_Needle` | (1.45, 1.7, 20.832), centre of the dial | Pointer of the airlock pressure dial (0 to 120 kPa over 270°). Delivered at 101.3 kPa. For a pressure p in kPa, rotate **(101.3 − p) × 2.25° about local Z** from the delivered pose; vacuum is +227.9° (checked numerically). |
  | `SuitLocker_Suit` | (2.07, 0.155, 19.19), on the locker floor under the suit | The suit on its stand. Hide it while the crew member wears it. |
  | `SuitLocker_Door` | (1.812, 0.16, 19.588), foot of the hinge line at the aft edge | Glazed door of the suit bay. A **positive turn about local Y** swings it into the room (checked numerically). Nothing limits it. |
  | `LightAnchor_SuitLocker` | (1.86, 2.13, 19.19) | Empty. Place for the locker's interior light; the emissive strip beside it is `A7_LightPanel`. |

- **Antenna gimbal** (`ExteriorParts/`, no colliders, no behaviour). The meshes keep ship axes; two empty
  pivots carry the usable axes:

  | Object | Position (ship metres) | Use |
  |---|---|---|
  | `Antenna_AzimuthPivot` | (3.501, 2.479, 12.72) | Local **Y** is the hull normal at the pad. Rotate about local Y to slew in azimuth. Holds `Antenna_Azimuth` (turntable and yoke) and the elevation pivot. |
  | `Antenna_ElevationPivot` | (3.830, 2.565, 12.72), child of the azimuth pivot | Local **X** is the trunnion. Rotate about local X to change elevation: a positive angle lowers the boresight towards aft, a negative one raises it towards the hull normal. Holds `Antenna_Elevation`, an instance of `Props/AntennaReflector` (trunnion, hub, reflector, feed; the renderer is its child `Reflector`). Delivered with the boresight leaning 30° aft of the hull normal, as in `c1fa9c2`. |
  | `Antenna_ServiceSocket` | (3.2, 2.4, 13.0) | Empty on the manual drive socket, on the AntennaRepair anchor. |

  The base housing, mast and service deck are part of the static `A7_Exterior` mesh. Deck labels read from
  aft, where the handrail path arrives. `Antenna_Elevation` is authored in the elevation pivot's frame, so it
  is the one model object that is not at identity world rotation; the pivots and the socket are exactly where
  `c1fa9c2` had them. Nothing limits the gimbal's travel: a large elevation change will put
  the dish through the yoke or the hull.
- **Props:** `LifeSupportRack_Ars1`, the ARS-1 unit, on the wall plinth forward of the scrubber rack: pivot at
  (2.236, 0.45, 6.67), yaw −90. `CanisterA`, `CanisterB` and `FanRotor` are separate objects, see
  `Props/LifeSupportRack/README.md`. Its `CollisionProxy` child is inactive, like the panel's.
- **Props:** `ServicePanel_EpsControl`, the EPS-2 panel, on the wall plinth aft of the power rack: pivot at
  (−2.256, 0.55, 7.36), yaw 90. It was `ServicePanel_PowerRepair` on the rack face in `9878304`. Its
  `CollisionProxy` child is inactive here, see Collision. Cover hinge and lamp materials are described in
  `Props/ServicePanel/README.md`.
- **Lighting:** the prefab has no lights. Luminaire meshes hang at the V01 practical-light positions
  (x = 0, z = −1.6, 1.4, 4.4, 7.4, 10.4, 12.4, 15.4, 18.4), so the existing lights fit. Emissive materials:
  `A7_LightPanel` (warm white), `A7_WorkLight` (amber), `A7_Led*`, `A7_Screens`.
- **Display faces:** static idle pages from `Textures/A7_Screens.png`. They show no authoritative values
  ("SEE HUD", "SCHEMATIC ONLY"); the airlock station's display reads only "AIRLOCK PRESSURE". Dials other than
  the airlock's have fixed pointers. Replace or overlay them with bound UI; the rectangles are in `a7_screens.json`.
- **Collision:** the proxies repeat the V01 solid envelope box for box (same centres and sizes as the collision
  boxes in `ShipVisualBuilder.cs`), so walkable space is unchanged. Visible meshes have no colliders. Both
  service bays are recesses inside the rack proxies, so the player cannot step into them. The EPS-2 panel's and
  the ARS-1 unit's own colliders are switched off because both stand in walkable space beside the racks; they
  protrude 0.21 m and 0.25 m from the wall, about as much as the utility bay's duct. The exterior has no proxies yet because the EVA route is not
  decided.

### Differences from the blockout to be aware of

- Wall bays divide each compartment evenly, so the cosmetic ring frames are not at the blockout rib stations.
- The observation window keeps the same opening band but is five framed panes, not one sheet.
- `AntennaRepair` has yaw −90, which faces inboard. The service face is oriented outboard and the crew works
  facing the hull, as the integrator decided; the anchor itself is unchanged.
- One mesh per compartment with one sub-mesh per material (21 to 29 each), as agreed for the first integration.

## Counts and bounds

Measured in Unity 6000.3.28f1 by the import check (`Meshes/a7_unity_import_report.json`).

| Object | Triangles | Materials | Bounds min → max (m) |
|---|---:|---:|---|
| `A7_Cockpit` | 76,184 | 28 | (−3, −0.05, −3.06) → (3, 3.20, 3) |
| `A7_Engineering` | 76,166 | 30 | (−2.4, −0.16, 2.84) → (2.4, 3.20, 11) |
| `A7_Storage` | 52,862 | 21 | (−2.4, −0.16, 10.84) → (2.4, 3.20, 17) |
| `A7_Airlock` | 31,348 | 24 | (−2.4, −0.16, 16.84) → (2.4, 3.20, 21.16) |
| `A7_Exterior` | 29,556 | 16 | (−12.37, −2.02, −3.59) → (12.37, 5.14, 23.4) |
| Door leaves (4) | 12,352 | 6–8 | at the two hatch planes |
| Engineering parts (7 meshes) | 5,248 | 1–4 | inside the two service racks |
| Airlock parts (3 meshes) | 4,374 | 2–12 | suit locker and pressure station |
| Antenna gimbal (2 meshes) | 4,346 | 1–2 | on the hull pad at AntennaRepair; the reflector is 3,638 of them |
| EPS-2 panel (3 meshes) | 6,734 | 5 | on the wall beside the power rack |
| ARS-1 unit (5 meshes) | 7,300 | 5 | on the wall forward of the scrubber rack |
| **Total** | **306,470** | **48 distinct** | 46 textures, 29 renderers, 48 colliders of which 46 active |

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
  - the antenna's drive socket is checked against the AntennaRepair anchor to 1 cm, and the reflector's pivot
    against the elevation pivot;
  - the dial pointer's and the locker door's pivots are checked the same way;
  - the prefab contains no camera, light, audio listener or script;
  - thirteen stills captured with `ShaderUtil.allowAsyncCompilation = false` and three warm-up frames each.
- `python tools/verify_repository.py` passes on the branch.
- The FBX files are now exported without texture records and hold no local path (checked by searching the
  files). The exports in `9878304` and `18339a3` embedded the absolute texture paths of the build machine;
  nothing depends on those paths. The EPS-2 panel's FBX carried such paths up to `c1fa9c2` and was replaced by
  its author's clean export after it.

The preview lighting (warm spot lights at the luminaires, one amber point light at the work-light anchor, one
warm point light at the suit locker's anchor, a flat ambient term, a warm reflection box, ACES and a little bloom) exists only inside the capture method. It
is not delivered lighting.

## Not verified, not done

- Placement and lighting inside the integrated Bootstrap scene; a Windows build of this branch; performance.
- The 405-sample clearance validator from V01 was not run by me. Clearance is argued from identical proxies.
- Cockpit, stowage and exterior were not compared with R01, R03, R05, R06 and R08 yet.
- No remodelled cargo bags (A04), no gloves and tether (A08). The antenna has no damaged state and no collider.
- No wear and dirt pass, no damage-state decals, no baked ambient occlusion, simple seat backs, a flat
  forward hull face.
