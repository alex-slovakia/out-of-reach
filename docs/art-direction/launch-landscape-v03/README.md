# Launch vegetation V03 — SURFACE-03

Branch `codex/i01-integration`; worker changes uncommitted. Ownership: LaunchVegetationV03 visual assets/editor authoring, LaunchLandscapeV02 authoring, and isolated VegetationDiagnostic editor QA. No buildings, ship, platform, scene layout or gameplay colliders changed. Root saves Bootstrap.

## Sources and reproduction

All foliage uses original photographic Poly Haven CC0 textures and meshes: [Grass Medium 02](https://polyhaven.com/a/grass_medium_02), [Shrub 04](https://polyhaven.com/a/shrub_04), and prepared-but-unused [Shrub 02](https://polyhaven.com/a/shrub_02). [License](https://polyhaven.com/license) permits commercial redistribution. Verified download manifest: 18 files / 18,782,170 bytes, €0, no new dependencies. Existing [Namaqualand Boulder 02](https://polyhaven.com/a/namaqualand_boulder_02) is reused for small scanned stones; original source/texture receipts remain in `Assets/_OutOfReach/Visuals/ThirdParty/PolyHaven/prepared-manifest.json`. No additional download for stones.

`prepare_dense_grass.py` assembles four differently rotated original grass clumps per tuft and reduces them to 1,698–1,699 triangles / 258–259 LOD1. Three variants retain original UVs, dry photographic RGB and opacity. Dense shrubs combine seven real source branches: 9,798 / 2,800 triangles. Small rock derivative: 419 / 100 triangles, normalized from measured bounds to 10–40 cm diameter and partly buried. Source models are preserved; derivatives are under owned Models.

Reproducers in `.local/launch-landscape-v03/`: `download.py`, `prepare_blender.py`, **then `pack-basecolors.py`** (preserves original RGB), `prepare_dense.py`, `prepare_dense_grass.py`, `compile-reference.ps1`. Existing Blender 3.6.1 only. Existing preview `prepared-foliage-preview.png` is an older Blender asset preview, not final game acceptance.

## Integration

Call `OutOfReach.Presentation.LaunchVegetation.Editor.LaunchVegetationAuthoring.Apply(site)` in Edit mode on the existing active `Launch site - stationary` Transform. API only creates/replaces its named vegetation group and generated owned assets. Caller must save assets/scene. Assembly: `OutOfReach.LaunchVegetation.Editor`. Read `LastReport` after actual authoring.

Sixteen margin patches use eight irregular colonies per patch spread over an 8.5 m radius, with broader photographic tufts, sparse low green shrubs and 20 attempted scanned stones per patch. Existing apron, facility corridor, outlet and actual transformed large-rock footprints remain excluded. Roots seat on real terrain triangle height and normal; stones bury 15–35% of their height. No colliders added or changed. All meshes are combined per patch/material and use two LODs; limits remain 2.6M aggregate high triangles / 650k low. Runtime frame time is not yet measured.

Root should keep V02 ridges, disable its old procedural grass/shrub groups and preserve existing scenery. No further hill redesign is part of the latest grass/stone correction.

## Causal native diagnostics

1. Imported FBX root axis was canceled. Full imported MeshFilter matrix now retained; strict bottom/Y-up/metre validation remains.
2. Project texture preset imported maps as Cubemap, causing null BaseMap. Explicit Texture2D import and non-null dimension/binding assertions now enforced.
3. Original source preparation darkened dry RGB. Direct original RGB packing fixes it without AI-generated textures.
4. Rock slab overlapped initial grass placement. Actual transformed rock footprint rejection prevents hidden roots.
5. Native isolated test `TestResults/vegetation-diagnostic-01.log`, images `.local/vegetation-diagnostic/20261115T125033338Z`, finally establishes the remaining visual cause: source grass consists of extremely thin individual stalks and colonies covered too little ground. ForceLOD0, alpha-off and hiding ground overlays did not restore grass mass. Unlit magenta clearly showed thin geometry. Final correction therefore changes actual tuft density/coverage and reduces white specular, rather than disabling valid alpha or lifting roots.

Diagnostic loads Bootstrap unsaved, renders the root review camera at site(-78,-34.75,-60) toward(-95,-34.8,15), isolates source grass/material/LOD/overlay variables, and exits. It never saves Bootstrap. Original editor PID70052 is untouched.

## Current verification

Reference compile against Unity 6000.3.28f1: PASS. First native diagnostic completed cleanly with actual GPU PNGs; no shader/import error. Final revised native result is recorded below when complete. Native edit-mode rendering is not a playtest/performance measurement. Root owns final save and PlayMode validation.

Final worker native diagnostic: `TestResults/vegetation-diagnostic-03.log` completed with `VEGETATION_DIAGNOSTIC_DONE`. Screenshot: `.local/vegetation-diagnostic/20261115T125904118Z/00-revised-grass-and-stones.png`. Native authoring: 963 grass tufts, 45 shrubs, 15 details, 291 scanned stones, 84 grass within 15m of reference camera; 2,297,479 aggregate LOD0 / 426,700 LOD1 triangles; 17 rejected placements inside existing rocks; actual grass height .430â€“.700m; no protected-footprint plants or collider changes. No shader/compiler error reported. Diagnostic used D3D11 with one job worker after the preceding D3D12 process crashed during Unity GizmoManager startup, before executeMethod or scene load.

A remaining GPU-update defect was inferred from unchanged visible silhouettes despite materially different native mesh bounds/CPU data: `EditorUtility.CopySerialized` on previously loaded mesh assets. Final authoring now uses explicit `Mesh.Clear`, vertex/normal/tangent/UV/color/index setters and `UploadMeshData(false)` to rebuild buffers while preserving asset GUIDs. This final correction reference-compiles; root's next saved-scene PlayMode capture is the visual verification gate. The screenshot above precedes that correction. No further worker Unity or Assets mutation after handoff.

Prepared-hashes/export-verification documents predating this correction are historical; `dense-grass-pebbles-manifest.json` records the new source derivatives. Root must refresh final hashes after final authoring if shipping those evidence files.
