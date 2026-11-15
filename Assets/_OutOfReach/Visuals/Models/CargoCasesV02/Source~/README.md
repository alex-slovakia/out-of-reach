# CARGO-CASES-V02: preflight cargo containers and supply rack

Seven original, fully scripted flight-cargo containers that replace the single recoloured `PreflightCargo_Visual`
case, plus a welded supply rack with a cradle, tie-down straps and a sign stand per crate. No third-party assets; the
only external input is the project's own OFL font `Assets/_OutOfReach/UI/Typography/BarlowCondensed-Bold.ttf` for the
stencils. No logos.

| Equipment id | Model | Container | Stencil | LOD0 / 1 / 2 triangles |
|---|---|---|---|---|
| `water-tank` | `CC2_Water` | quilted insulated bladder case on a rigid tray, cinch straps, fill cap, drain valve | WATER 48 kg | 3748 / 2322 / 1190 |
| `oxygen-tank` | `CC2_Oxygen` | composite bottle in a white tube cradle, band clamps, valve, regulator, gauge | OXYGEN 30 kg | 4906 / 2856 / 1944 |
| `food-locker` | `CC2_Food` | rotomoulded insulated locker with a front door, hinges, paddle latches | FOOD 35 kg | 3446 / 2246 / 1034 |
| `battery-module` | `CC2_Battery` | finned power module, connectors, isolator, charge bar, front lifting bails | BATTERY 55 kg | 4534 / 2506 / 1312 |
| `solar-wing` | `CC2_Solar` | roll-out wing canister on a skid, clamp bands, deployment slot showing the cells | SOLAR 70 kg | 3834 / 2222 / 1378 |
| `spare-parts-kit` | `CC2_Spares` | moulded hard case, gasket, flanges, ribs, draw latches, hinges, purge valve | SPARES 25 kg | 4014 / 2732 / 1128 |
| `spectrometer` | `CC2_Science` | beaded aluminium transit case, edge profiles, cast corners, toggle latches | SCIENCE 90 kg | 5038 / 3278 / 1182 |
| rack | `CC2_RackFrame` | six posts on base plates, three crate levels, cargo tracks, end and rear bracing, header rail | TWO-HAND LIFT | 2944 / 2296 / 864 |
| rack cells | `CC2_RackCell_<Key>` | cradle runners, stop angles, two ratchet straps fitted to that container, sign stand | | about 1000 / 900 / 330 |

Masses are the AuroraMission catalogue unit masses (the same numbers `CargoSignText` shows), not the "23 kg" example
in the task. Colours are `CargoVisualPresenter.EquipmentColour`, baked into the per-case shell materials.

## What stays untouched

Every `PreflightCargo_Visual` instance, its `Additive cargo side grips` with both hand grip frames, every
`InteractableTarget`, collider, manifest sign, presenter binding and `Bootstrap.unity` itself. Each model has its two
carry grips as 34 mm tubes centred at x = +/-0.278, y = 0.175 along z, the exact `PreflightCargoHandsAuthoring`
constants, so the existing grip frames sit on the modelled handles. Bodies stay inside the delivered case envelope
(0.59 m over the grips, at most 0.30 m tall).

## Install

1. Open `Bootstrap`.
2. Menu `OUT OF REACH > Cargo Cases V02 > Install into open scene`. The scene is marked dirty, not saved. Check, then save.
3. `Uninstall from open scene` restores the delivered cases and the old rack.

Batch: `-executeMethod OutOfReach.Visuals.CargoCasesV02.Editor.CargoCasesV02Installer.InstallIntoBootstrapBatch` (opens Bootstrap, installs, saves).

What the installer does:

- Under each case root it switches the old renderers off, parents the V02 prefab (yawed 180 degrees: the FBX import
  has the stencilled front on local -Z, the delivered case has its labelled face on +Z) and adds `CargoCaseV02Skin`.
- The one carried case gets all seven models. The skin shows the one named on the carried manifest label that
  `CargoVisualPresenter` already writes, and keeps the runtime "Case name plate (legible)" hidden (the stencil is baked).
- The rack frame goes under `Temporary preflight supply` at dock (-2.655, 0, -1.10), no yaw. It stands on the modelled
  cargo plinth between the dock's orange fences.
- Each supply case gets its rack cell as a child, so cradle, straps and sign stand leave with the crate (R02).
- The old sticks (`Aligned supply cells V02`, `Source station label upright/rail`) are deactivated, not deleted.
  `PreflightRackDressing` still finds the old top rail by name and puts its "SUPPLY RACK" header on the new header rail.

## Rebuild

    python Source~/cc2_tex.py
    blender --background --python Source~/build_cargo_cases_v02.py -- --blend
    Unity menu: OUT OF REACH > Cargo Cases V02 > Rebuild Materials and Prefabs

18 URP Lit materials `CC2_*`, 8 tiling 1024 px BaseColor + Normal pairs and one 2048 px alpha-clipped label atlas.
Decals sit at least 2.6 mm off their surface: Bootstrap is about 10 km from the world origin, where float32 positions
resolve about 1 mm, and a closer stencil z-fights.

## Verification actually run (Unity 6000.3.28f1, own Editor on this worktree)

- Scripts compile in the Editor; `Rebuild Materials and Prefabs` returns OK for the 15 models.
- `Install` on the real open Bootstrap: 7 supply cases with their cells, 7 aboard cases, the carried case and the rack
  frame; no cell warning; scene dirty, never saved.
- Play mode, Practice: the rack, signs and header render in place (`Previews~/urp_play_rack_*.png`).
- `MissionRuntime.TryGetInteractable("cargo-supply:water-tank").TryInteract()` succeeded ("Carrying water tank") and
  the carried skin showed `water-tank`. A later pickup of the solar wing switched the carried model to `solar-wing`.
- Player view while carrying the solar canister, level and looking down: stencil facing the player, both gloves closed
  on the two side handles, body-arm reach shortfall 0 (`Previews~/urp_play_carry_solar_*.png`).
- One still per container in Play mode lighting (`Previews~/urp_play_crate_<Key>.png`).
- Cycles renders of the final meshes with the shipped textures, made without Unity after the decal re-export:
  `Previews~/blender_crate_<Key>.png` and `Previews~/blender_rack_assembled.png` (`Source~/render_evidence.py`
  writes them to `Evidence~/`, which this repository excludes locally, so the committed copies are in `Previews~/`).
- Short integration checklist: `Source~/HANDOFF.md`.

## Not verified

- **The final FBX files are a re-export made after the stills.** The stills showed half of the oxygen placard stencil
  missing (`urp_play_crate_Oxygen.png`), which is the z-fight described under Rebuild. All decals were then moved out to
  2.6 mm and the 15 FBX files re-exported. That re-export has a Blender FBX round trip (15 of 15 triangle counts
  match the report) but no Unity import and no render: the Editor did not come back up in time.
- Only two of the seven containers were actually carried (water without a still, solar with stills).
- The aboard bay cases were swapped by the installer but not looked at.
- LOD switching distances are set (22 % / 7 % / 1.2 % screen height) but not looked at in game.
- No build, no EditMode test run.
