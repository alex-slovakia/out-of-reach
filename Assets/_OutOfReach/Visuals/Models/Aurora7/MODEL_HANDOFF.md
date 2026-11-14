# AURORA-7 models — handoff (CODEX-006 / V01A)

Owner: Claude / Apoliak — the dedicated Fable model writer. Branch `feature/aurora7-models`.
Scope written: only `Assets/_OutOfReach/Visuals/Models.meta` and `Assets/_OutOfReach/Visuals/Models/**`.

**State: first functional deliverable.** Geometry, footprints, collision proxies, FBX import and module
structure are in place and checked. Surface and detail polish is **paused** until the team approves the
reference boards (issue #7, 10:43 UTC); nothing here is a final art pass.

## What is delivered

A remodel of the whole vessel as a believable near-future pressure vessel, grounded in ISS, Orion and Skylab
practice. Everything is function first:

- **Structure:** ring frames, quilted insulation liner, deck plates on seat track, cable trays, luminaires and
  air diffusers.
- **Flight deck:** five-pane framed observation window, overhead circuit-breaker panels, a three-display console
  with keyboards, two crew seats with harness and hand controllers.
- **Engineering:** EPS rack with the EPS-2 service panel, air-revitalisation rack with twin CO₂ beds, three
  composite pressure vessels, tool board, avionics and utility bays.
- **Logistics:** soft stowage (cargo transfer bags behind bungee restraints) and hard lockers.
- **Airlock:** umbilical/pressure-control rack with depress valve and gauges, suit stowage station, two-leaf
  pressure doors with viewports.
- **Exterior:** pressure hull with frames and seams, forward window face, aft EVA hatch and porch, gold EVA
  handrails to the antenna work site, two solar wings, body radiators, eight reaction-control quads.

All geometry and textures are original and generated from the scripts in `Source/`. See `LICENSES.md`.

## Folder map

| Path | Content |
|---|---|
| `Prefabs/Aurora7_Models.prefab` | **Use this.** Visual meshes, door leaves, props, collision proxies. No camera, light, listener or script. |
| `Meshes/A7_*.fbx` | One FBX per module: Cockpit, Engineering, Storage, Airlock, Exterior, Doors (four leaves), Collision. |
| `Meshes/a7_model_report.json` | Blender-side counts and bounds (Unity axes). |
| `Meshes/a7_unity_import_report.json` | Unity-side counts, bounds and validation result. |
| `Materials/*.mat` | 32 URP Lit materials created from `Source/a7_materials.json`. |
| `Textures/` | 10 tiling PBR sets (BaseColor, Normal, MODS), the label atlas and the display atlas, with their JSON maps. |
| `Props/ServicePanel/` | EPS-2 panel from the panel author's chat, imported unchanged (its own README, licences and previews). |
| `Previews/` | Genuine renders: `A7_unity_urp_*` from Unity URP, `A7_blender_cycles_*` from Blender Cycles. |
| `Editor/Aurora7ModelTools.cs` | Editor-only: importer settings, materials, prefab, validation, preview stills. |
| `Source/` | `a7kit.py`, `a7_ship.py`, `a7_textures.py`, `build_aurora7.py`, `a7_materials.json` — the editable source. |

## Integration

Coordinates are those of the V01 handoff: metres, Y up, ship along +Z, deck at y = 0. **No anchor was moved.**
Instantiate the prefab at the ship origin with identity rotation and unit scale.

| Anchor (V01) | What the model has there |
|---|---|
| PlayerSpawn (0, 0.05, 0.8), yaw 180 | Clear deck between and behind the two seats, facing the console and window. |
| CockpitConsole (0, 1.1, −1.72) | Aft edge of the console work surface, centre display ahead. |
| PowerRepair (−1.65, 1.15, 6.1), yaw 90 | EPS rack; the EPS-2 service panel is centred at 1.15 m. |
| LifeSupportRepair (1.65, 1.15, 8.1), yaw −90 | Air-revitalisation rack; CO₂ beds at anchor height. |
| AirlockInner (0, 0, 17) / AirlockOuter (0, 0, 21) | Hatchways with machined sealing lands; door leaves on these planes. |
| EvaStart (0, 0, 22) / EvaReturn (0, 0, 21.7) | On the EVA porch, a grated platform at deck level outside the aft hatch. |
| AntennaRepair (3.2, 2.4, 13), yaw −90 | Antenna mounting pad on the hull's upper starboard quarter; the anchor is 0.31 m off the skin. |
| FinaleCameraAnchor (9, 5, 27), yaw 205 | Sees the aft bulkhead, starboard flank, solar wing and antenna pad (`A7_unity_urp_exterior_finale.png`). |

- **Hierarchy:** `Visual/` (five static module meshes), `DoorLeaves/` (four movable leaves), `Props/`
  (`ServicePanel_PowerRepair`), `CollisionProxies/` (46 `COL_*` box colliders).
- **Door leaves:** `DoorLeaf_Inner_L/R` and `DoorLeaf_Outer_L/R`. Each object's pivot is the V01 leaf position
  (inner ±1.4 open, outer ±0.45 closed) and each leaf is 0.88 × 2.46 × 0.12 m. Parent them under your leaf
  transforms at local zero, or slide them along X. They carry no collider and authorize nothing.
- **Lighting:** the prefab has no lights. Luminaire meshes hang at the V01 practical-light positions
  (x = 0, z = −1.6, 1.4, 4.4, 7.4, 10.4, 12.4, 15.4, 18.4), so the existing lights fit. Emissive materials:
  `A7_LightPanel`, `A7_Led*`, `A7_Screens`.
- **Display faces:** static idle pages from `Textures/A7_Screens.png`. They show no authoritative values
  ("SEE HUD", "SCHEMATIC ONLY"). Replace or overlay them with bound UI; the rectangles are in `a7_screens.json`.
- **Collision:** the proxies repeat the V01 solid envelope box for box (same centres and sizes as the collision
  boxes in `ShipVisualBuilder.cs`), so walkable space is unchanged. Visible meshes have no colliders. The
  exterior has no proxies yet because the EVA route is not decided.
- **Service panel:** pivot at (−1.63, 0.55, 6.1), yaw 90. Its cover hinge and lamp materials are described in
  `Props/ServicePanel/README.md`.

### Differences from the blockout to be aware of

- Wall bays divide each compartment evenly, so the cosmetic ring frames are not at the blockout rib stations.
- The observation window keeps the same opening band but is five framed panes, not one sheet.
- `AntennaRepair` has yaw −90, which faces inboard; the mounting pad faces outboard. Nothing was changed.
  Decide the working side in V03.
- One mesh per compartment with one sub-mesh per material (17 to 24 each). Ask if you need a finer split
  for culling.

## Counts and bounds

Measured in Unity 6000.3.28f1 by the import check (`Meshes/a7_unity_import_report.json`).

| Object | Triangles | Materials | Bounds min → max (m) |
|---|---:|---:|---|
| `A7_Cockpit` | 72,296 | 24 | (−3, −0.05, −3.06) → (3, 3.20, 3) |
| `A7_Engineering` | 68,736 | 19 | (−2.4, −0.16, 2.84) → (2.4, 3.20, 11) |
| `A7_Storage` | 49,300 | 17 | (−2.4, −0.16, 10.84) → (2.4, 3.20, 17) |
| `A7_Airlock` | 28,608 | 21 | (−2.4, −0.16, 16.84) → (2.4, 3.20, 21.16) |
| `A7_Exterior` | 27,120 | 10 | (−12.37, −2.02, −3.59) → (12.37, 5.14, 23.4) |
| Door leaves (4) | 9,808 | 6–7 | at the two hatch planes |
| EPS-2 service panel (3 meshes) | 6,734 | 5 | on the power rack |
| **Total** | **262,602** | **34 distinct** | 36 textures, 47 colliders |

Scale is 1 unit = 1 metre; every model root is at identity rotation and unit scale. Hull: 7.1 m diameter
around the flight deck, 6.0 m aft, 24.2 m long; solar wings span 24.7 m.

Textures: tiling sets are 1024² (rubber 512²), the label atlas 4096², the display atlas 2048². UV0 is
world-projected in metres for tiling materials and atlas-mapped for labels and displays. There is no
lightmap UV set and no LOD.

## Regenerate

```powershell
cd Assets\_OutOfReach\Visuals\Models\Aurora7\Source
python a7_textures.py                      # textures and atlases (numpy, Pillow)
& "C:\Program Files\Blender Foundation\Blender 5.3\blender.exe" -b --factory-startup --python build_aurora7.py -- --work <folder outside the project> [--render]
# then in Unity: OUT OF REACH > Models > Rebuild AURORA-7 materials and prefab
# or batch, with graphics: -executeMethod OutOfReach.ModelTools.Aurora7ModelTools.BuildAndCapture
```

The `.blend` and the Cycles stills are written to the `--work` folder. They are kept out of `Assets` on
purpose: a `.blend` inside the project makes Unity call Blender on import.

## Verification actually run

- **Blender 5.3.0** (build 2026-11-10, hash 02f0899d78e3), background mode, driven by the Python scripts here.
  No MCP connector. Build exit 0; kit self-test passes (closed shapes have positive volume, mirrored placement
  keeps faces outward).
- **Unity 6000.3.28f1, URP 17.3 (`PC_RPAsset`)**, batch mode with graphics, in the model writer's own clone:
  - import, material creation, prefab build and validation exit 0, **0 problems**;
  - every renderer uses the external URP Lit materials, none missing;
  - three collision proxies at known asymmetric positions confirm axes, handedness and scale;
  - the prefab contains no camera, light, audio listener or script;
  - ten stills captured with `ShaderUtil.allowAsyncCompilation = false` and three warm-up frames each.
- `python tools/verify_repository.py` passes on the branch.

The preview lighting (spot lights at the luminaires, a flat ambient term, a grey reflection box, ACES and a
little bloom) exists only inside the capture method. It is not delivered lighting.

## Not verified, not done

- Placement and lighting inside the integrated Bootstrap scene; a Windows build of this branch; performance.
- The 405-sample clearance validator from V01 was not run by me. Clearance is argued from identical proxies.
- No dedicated antenna assembly and no dedicated life-support hero prop yet; both are requested from the
  panel author's chat and will be imported the same way as the EPS-2 panel.
- No final surface pass: no wear and dirt pass, no decals for damage states, no baked ambient occlusion,
  simple seat backs, a flat forward hull face, plain side deck panels.
