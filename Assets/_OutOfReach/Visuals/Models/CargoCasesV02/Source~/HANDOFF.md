# CARGO-CASES-V02 handoff

Branch `feature/claude-max-crates-models`, rebased onto `origin/codex/nasa-2330-staging` (5479e87). Only new files
under `Assets/_OutOfReach/Visuals/Models/CargoCasesV02/` (+ its folder `.meta`). `Bootstrap.unity` is not touched.

## Install

Open Bootstrap, menu **OUT OF REACH > Cargo Cases V02 > Install into open scene**. The scene is marked dirty, not
saved: look, then save. **Uninstall from open scene** restores the delivered cases and the old rack.
Batch (saves): `-executeMethod OutOfReach.Visuals.CargoCasesV02.Editor.CargoCasesV02Installer.InstallIntoBootstrapBatch`.

## Triangles (LOD0 / LOD1 / LOD2)

| Model | Triangles |
|---|---|
| Water | 3748 / 2322 / 1190 |
| Oxygen | 4906 / 2856 / 1944 |
| Food | 3446 / 2246 / 1034 |
| Battery | 4534 / 2506 / 1312 |
| Solar | 3834 / 2222 / 1378 |
| Spares | 4014 / 2732 / 1128 |
| Science | 5038 / 3278 / 1182 |
| Rack frame | 2944 / 2296 / 864 |
| Rack cell, each of 7 | 956 to 1164 / 832 to 1008 / 288 to 384 |

Rack fully stocked at LOD0: about 40,000 triangles. 18 materials, 17 textures (1024 px, label atlas 2048 px).

## What Codex must check natively

1. Run the install menu on the staging Bootstrap. The report must list 7 supply cases with a rack cell each, 7 aboard
   cases, `carried x7`, the rack frame and no `WARNING` line. Staging has the same cargo code and object names as the
   base this was verified on, but the install was not run on staging itself.
2. Play mode, Practice: the stencils face the aisle, the "SUPPLY RACK" header sits on the new header rail, signs sit on
   their stands, and a taken crate takes its cradle, straps and sign with it.
3. **Oxygen placard.** The earlier still shows half of its stencil missing (z-fight 10 km from the origin). All decals
   were then moved to 2.6 mm off the surface and every FBX re-exported. That re-export was never imported in Unity:
   look at the oxygen front plate and at any stencil for flicker while walking.
4. Pick up two different cases: the carried model must match the item, the stencil must face the player and both
   gloves must close on the side handles (verified for the solar canister only).
5. Aboard bays in the transport: cases were swapped by the installer but never looked at.
6. Save the scene only after 1 to 5.

## Evidence

- `Previews~/urp_play_*.png`: Unity stills from the real Bootstrap in Play mode, taken before the decal re-export.
- `Previews~/blender_*.png`: Cycles renders of the final meshes with the shipped textures (also in the local,
  git-excluded `Evidence~/`).
- Roslyn compile of both assemblies on this rebased tree: exit 0. Blender FBX round trip: 15 of 15 triangle counts match.
