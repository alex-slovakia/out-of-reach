# MARINE-VISUAL-CORRECTIONS-V01

Released bounded art correction candidate. No Unity process, live Assets edit, branch/worktree edit, material-culling workaround or redesign. Root imports and visually accepts it in Unity6000.3.28f1. Geometry/export checks passed; **native art acceptance remains pending**.

## Root import: one file, existing metadata

Copy `Export/TransportLaunch.fbx` over:

`Assets/_OutOfReach/Visuals/Models/MarinePrologue/Transport/Models/TransportLaunch.fbx`

Keep its existing `.meta` untouched: GUID **2b697c9e944c4977af450e3f30b66e33**. Preserve all material remaps and `_Cull=2`. No model importer, camera, lighting, collision, anchor or layout change is requested. All native FBX object IDs and geometry names are retained.

Old FBX: `142c2053cda0e639781eae8e8b7973e1514fd385abfc399b5b621962572fcb62`, 830268 bytes.

New FBX: **`e21cfd832ffb50a190eb317be848b2da53624987bb6bd9b1152c50eece060e2d`**, **835068 bytes**.

Update the matching entry in integrator-owned `MarinePrologue/SOURCE.json` to the new hash/size and record this correction package/source hash as a derivative. Keep the original transport commit `855efbc328cb0c703bf7a92e92767f5eab25eabc` as upstream provenance; it is not the new corrected file's commit. No texture, material, MarineLayout or collision-manifest hash changes are needed. `import-manifest.json` gives the precise replacement and source hashes.

## Confirmed causes and exact changes

1. **BoosterStack exterior faces were reversed.** Original generator `Source/build_transport_launch.py:335` documents an ascending exterior profile for `lathe`; its `stack()` at line693 supplies five descending profiles. Source FBX main-cylinder exterior winding has radial dot about−0.999761, while capsule exterior is correctly positive. Both scene nodes have the same unit scale, zero translation and imported−90X under the same vehicle parent; ascent translates that parent without rotating it. This was not an intended cutaway or parenting defect.

   Reverse winding and corresponding per-corner normal direction on only the five disconnected lathe components: service288, interstage96, booster480, engine144, bell288 triangles = **1296 of2276**. Keep all1316 vertex positions, UV-to-vertex associations, material slots and the other980 triangles unchanged. Cap orientation is corrected with its parent section. The bell's return profile remains a real inward cavity surface. No boxes, legs, fins or capsule faces are flipped.

2. **The pale sky quad was the removed mast's orphan lamp lens.** The fit derivative removes the original mast head but retains its lens inside `DeckLamps`. Its actual Unity bounds are x1.48…1.72,y4.54…4.60,z−7.02…−6.78. Projection through the recorded dock camera/FOV68 gives x784.78…815.36,y44.11…62.14 pixels, matching the reported image. No celestial object is involved.

   Remove only that connected island: **24 vertices,44 triangles**. Four lower barge lamps remain:96 vertices/176 triangles. The DeckLamps node stays present under the stationary site.

Expected output: **11 meshes,16 anchors,24570 source FBX triangles** (old24614 minus44). BoosterStack still2276; DeckLamps176; all other per-mesh source counts unchanged. Prior author Unity report counted24566 rather than24614; if the same native degenerate filtering persists, the expected native total is24522. Treat that as a prediction to inspect, not a recorded Unity result. Do not change a blanket triangle assertion without checking the actual per-mesh delta.

## Reproducible source and verification

The exact original derivative `.blend` was retrieved with authorized bounded authenticated LFS access and SHA verified: `Source/OriginalTransportLaunch.blend`, **1d8bf140e2d6a1a0d159d31cacfb95d23f240494ef4c8417fb43206a02d1db58**,3198322 bytes. Installed Blender3.6.1 cannot read its newer `BLENDER17…v050` file format. No newer application/dependency was downloaded. Root explicitly authorized an actual-FBX derivative fallback.

`Source/TransportLaunch_Corrected.blend` is a genuine editable Blender3.6 derivative of the exact production FBX, containing its11 meshes/16 empties and only the two corrections. It is not a regeneration from a concept or the stale source generator. The original file remains unchanged alongside it. Existing baked maps are copied unchanged to `Source/textures` for inspection/reference; Unity retains the established seven production material bindings. The imported FBX does not carry the original procedural shader graph, so this derivative does not claim that graph or a polished Blender material preview.

`build_package.ps1` runs three bounded Blender-background steps:

- `correct_transport.py`: select the five components by measured topology/bounds; make the actual editable Blender correction; assert unchanged geometry/UV/material/transform fingerprints for the other25 objects; validate positive cylinder and cap winding.
- `encode_exact_fbx.py`: use Blender's bundled FBX parser/encoder to preserve original native IDs and unrelated channels exactly, changing only the two geometry nodes. The normal ordinary Blender3.6 re-export was rejected because it renamed meshes `.001`→`.002` and emitted invalid normal data on an untouched mesh. The final encoder avoids that collateral change. Original59 other FBX objects and every non-Objects top-level node compare exactly by parsed byte semantics. Every unchanged normal/UV/material channel is preserved; the changed corners receive only the required order/sign correction. Corrected band normals are finite, minimum outward radial dot0.996192; three48-triangle caps have verified outward signs.
- `verify_roundtrip.py`: import the **final encoded FBX** back into Blender; all27 objects, per-face topology/material assignments and UV associations match the corrected editable mesh. Measured maximum vertex error **0m**, UV error **0**, transform-matrix error **0**. Source triangle total24570. Split-normal proof uses exact FBX channel comparison, rather than Blender3.6's unreliable transient normal cache.

Evidence: `Evidence/correction-report.json`, `exact-fbx-report.json`, `roundtrip.json` and logs. Final source/geometry scripts are the correction patch; no teammate source was edited. The cached LFS object is not an import artifact. Standard exporter failure is disclosed above and is not the released export route.

Root acceptance: rebuild scene instances if required, capture the same dock and exterior-ascent views, confirm closed front-facing stack surfaces and absence of the pixel800 sky island, then inspect hatch/cargo/arm behavior and existing anchor/collision checks. Keep all original scene transforms and opaque backface culling. No native render or AAA-fidelity pass is claimed by this package.

Original team geometry, UVs and baked textures retain existing Transport provenance; Inter raster labels retain their existing SIL OFL attribution. These bounded original team corrections introduce no third-party assets or license changes.
