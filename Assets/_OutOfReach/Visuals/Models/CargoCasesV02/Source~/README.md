# CARGO-CASES-V02: preflight cargo containers

Seven original, fully scripted flight-cargo containers that replace the single recoloured `PreflightCargo_Visual`
case. No third-party assets; the only external input is the project's own OFL font
`Assets/_OutOfReach/UI/Typography/BarlowCondensed-Bold.ttf` for the stencils. No logos.

| Equipment id | Model | Container | Stencil | LOD0 / 1 / 2 triangles |
|---|---|---|---|---|
| `water-tank` | `CC2_Water` | quilted insulated bladder case on a rigid tray, cinch straps, fill cap, drain valve | WATER 48 kg | 3748 / 2322 / 1190 |
| `oxygen-tank` | `CC2_Oxygen` | composite bottle in a white tube cradle, band clamps, valve, regulator, gauge | OXYGEN 30 kg | 4906 / 2856 / 1944 |
| `food-locker` | `CC2_Food` | rotomoulded insulated locker with a front door, hinges, paddle latches | FOOD 35 kg | 3446 / 2246 / 1034 |
| `battery-module` | `CC2_Battery` | finned power module, connectors, isolator, charge bar, front lifting bails | BATTERY 55 kg | 4536 / 2508 / 1314 |
| `solar-wing` | `CC2_Solar` | roll-out wing canister on a skid, clamp bands, deployment slot showing the cells | SOLAR 70 kg | 3834 / 2222 / 1378 |
| `spare-parts-kit` | `CC2_Spares` | moulded hard case, gasket, flanges, ribs, draw latches, hinges, purge valve | SPARES 25 kg | 4014 / 2732 / 1128 |
| `spectrometer` | `CC2_Science` | beaded aluminium transit case, edge profiles, cast corners, toggle latches | SCIENCE 90 kg | 5038 / 3278 / 1182 |

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
3. `Uninstall from open scene` restores the delivered cases.

Batch: `-executeMethod OutOfReach.Visuals.CargoCasesV02.Editor.CargoCasesV02Installer.InstallIntoBootstrapBatch` (opens Bootstrap, installs, saves).

The installer switches the old renderers off under each case root, parents the V02 prefab there (yawed 180 degrees:
the FBX import has the stencilled front on local -Z, the delivered case has its labelled face on +Z) and adds
`CargoCaseV02Skin`. The one carried case gets all seven models; the skin shows the one named on the carried manifest
label that `CargoVisualPresenter` already writes. The skin also keeps the runtime "Case name plate (legible)" hidden,
because the stencil is baked.

## Rebuild

    python Source~/cc2_tex.py
    blender --background --python Source~/build_cargo_cases_v02.py -- --blend
    Unity menu: OUT OF REACH > Cargo Cases V02 > Rebuild Materials and Prefabs

18 URP Lit materials `CC2_*`, 8 tiling 1024 px BaseColor + Normal pairs and one 2048 px alpha-clipped label atlas.

## Verification actually run (Unity 6000.3.28f1, own Editor on this worktree)

- Scripts compiled in the Editor; `Rebuild Materials and Prefabs` returned OK.
- `Install` ran on the real open Bootstrap: 7 supply, 7 aboard and the carried case swapped, scene dirty, not saved.
- Two Edit mode URP stills of the rack: `Previews~/urp_bootstrap_rack_editmode_before_yaw_fix_*.png`. They show the
  seven models, materials and stencils in place, and that the models faced backwards.
- After those stills: the 180 degree yaw fix in the installer and one string-compare change in the skin. Both
  assemblies then compiled with the Unity Roslyn compiler (exit 0). The Editor was gone by then (machine RAM cap),
  so the yaw fix is not re-rendered.

## Not done / not verified

- **No Play mode run.** Picking up a case, the carried model switching per item, and the hands on the handles are
  not checked in game.
- **The rack is not remodelled.** The installer keeps the existing rack (it reports "rack prefab not built: old rack
  kept"). The builder and installer already accept `CC2_RackFrame.fbx` and `CC2_RackCell_<Key>.fbx` if they are added.
- LOD switching distances are set (22 % / 7 % / 1.2 % screen height) but not looked at in game.
