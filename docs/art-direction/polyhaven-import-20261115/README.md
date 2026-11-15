# Poly Haven Unity preparation —15 November2026

## Delivered source assets

13 curated models plus2 ground surfaces, selected from Poly Haven's official API and downloaded from its official asset CDN. [Poly Haven license](https://polyhaven.com/license) permits free commercial use and redistribution under [CC0](https://creativecommons.org/publicdomain/zero/1.0/). **Price paid: EUR0**. No purchase, subscription, new software or importer was installed. Attribution is included voluntarily and API credit retained in the packaged NOTICE. No preview thumbnails or unrelated website illustrations are redistributed.

Exact source URLs, authors, selected resolutions, source byte counts, API MD5 and locally computed SHA-256 values: [source-manifest.json](source-manifest.json). Per-asset metadata/file catalogues are preserved under metadata/. Raw originals remain in the ignored .local/polyhaven-prepare staging directory; only normalized models and selected/converted textures enter Assets.

Downloads total199.42MB. The87.17MB coast_land_rocks_04 sourceFBX accounts for almost half and contains four sourceLODs; only its reduced lowerLOD enters the game. Prepared payload before Unity-generated materials/prefabs is 147.28 MB. No8K/16K textures, trees or vegetation were downloaded. camera_01 returned404 and was skipped. No placeholder is substituted for that unavailable ID.

## Preparation

- Blender3.6.1 imported/exported FBX. Export: -Z forward, Y up, metre scale1; whole-pack bottom-centre pivots. Source relative component layout preserved.
- Source flood-light collection narrowed to one complete **standing_double_a** twin-lamp tripod, brackets and its cable. Other fixtures and empty curve helpers are omitted.
- Coast rockLOD3 and lowest provided NamaqualandLOD selected, then decimated to approximately30k triangles. This is one bounded mesh per rock prefab, not an untested automatic LODGroup.
- Small tools target5000 triangles; original detail retained below budget. Props below30000 triangles. Exact counts and dimensions appear below.
- Small props/tools use1K textures; rocks/ground use2K. Reimport audit confirms all53 prepared textures have maximum dimension<=2048. Ground albedo, normal and roughness are the only three source channels downloaded.
- Albedo remains sRGB. Source alpha is packed into albedo where present. OpenGL normal maps are non-colour data. URP metallic/smoothness packs put source metallic intoR (0 when none) and **1−roughness intoA**. No speculative brightness boost or glass roughness replacement.
- Unity builder assigns existing URP/Lit shader, correct normal-map/linear imports, mipmaps and high-quality texture compression. Flood glass uses its own material; intentional source emissive map remains a material effect, not a power source or added Light.

| Model ID | Triangles | Unity XYZ bounds (m) |
|---|---:|---|
| portable_generator | 26419 | 0.818 × 0.577 × 0.564 |
| long_life_food | 4712 | 0.524 × 0.238 × 0.114 |
| coast_land_rocks_04 | 29999 | 9.962 × 0.839 × 11.498 |
| namaqualand_boulder_02 | 30000 | 2.533 × 0.883 × 1.234 |
| combination_wrench | 1986 | 0.057 × 0.022 × 0.337 |
| adjustable_wrench | 4996 | 0.068 × 0.254 × 0.031 |
| pliers | 4540 | 0.055 × 0.180 × 0.018 |
| screwdrivers_02 | 3548 | 0.100 × 0.027 × 0.270 |
| vintage_spacecraft_instrument | 13279 | 0.410 × 0.269 × 0.204 |
| medical_box | 3718 | 0.525 × 0.098 × 0.350 |
| metal_toolbox | 14228 | 0.400 × 0.305 × 0.271 |
| binoculars | 19248 | 0.167 × 0.066 × 0.288 |
| flood_lights | 11758 | 0.778 × 1.788 × 1.282 |

The generator, food, medical case and tools are static props only. No inventory, healing, battery, generator or resource authority is added. Root must place Earth launch equipment sensibly and secure/tether cabin props. The controller and environment collision owners remain unchanged.

Ground texture reference footprints from official API dimensions: **coast_sand_02 15×15m/repeat**; **gravelly_sand2.48×2.48m/repeat**. Root should use these to size meshUVs. No displacement map was downloaded; actual ground relief is root-owned geometry.

## Stable Unity interface

Editor entrypoint: `OutOfReach.Composition.Editor.PolyHavenAssetAuthoring.BuildAssets`.

Menu: **OUT OF REACH → Import curated Poly Haven assets**. Requires Unity6000.3.28f1, Edit mode. It reads Assets/_OutOfReach/Visuals/ThirdParty/PolyHaven/prepared-manifest.json and generates owned assets only:

- `Models/{id}.fbx` — prepared metre-scale source.
- `Textures/{id}_BaseColor.*`, `_Normal.png`, `_MetalSmooth.png` — prepared maps.
- `Materials/PH_{id}.mat`; flood glass additionally `PH_flood_lights_glass.mat`.
- `Prefabs/PH_{id}.prefab` — static mesh hierarchy with one conservative bounding BoxCollider.
- Ground materials exactly `Materials/PH_coast_sand_02.mat` and `Materials/PH_gravelly_sand.mat`.

**No scenes are opened/saved and no object is placed by this builder.** Root owns placement, sensible rotations, clear travel routes and any changes to the conservative colliders. Tall/thin fixtures and tool groups need placement review; their bounding boxes are deliberate simple proxies, not detailed physical collision.

The builder compares actual imported Unity metre bounds and bottom pivot against Blender measurements, counts imported triangles and writes `unity-import-evidence.json` here when it succeeds. It is idempotent for its owned generated materials/prefabs. Existing project assets outside the PolyHaven folder are untouched.

## Reproduction and checks

`python tools/assets/polyhaven_prepare.py --download` downloads the selected official files, verifies source MD5/size and records SHA-256. It requires the selected API responses in .local/asset-review; a future refresh must consciously review the current catalog. No runtime download occurs.

`blender --background --factory-startup --python tools/assets/polyhaven_prepare.py -- --blender` performs full preparation. `--only flood_lights` updates that one prepared model, retaining others.

`python tools/assets/polyhaven_prepare.py` verifies the downloaded source SHA-256/bytes and prepared artifact hashes offline. [prepared-manifest.json](prepared-manifest.json), [prepared-hashes.json](prepared-hashes.json) preserve the exact preparation output. Unity-generated materials/prefabs are validated separately by the editor entrypoint.

Blender reimport audit [blender-reimport-verification.json](blender-reimport-verification.json): **13 models,53 textures passed** finite geometry, triangle count, UV presence, bottom-centre pivot, metre bounds and maximum texture size. The source floodFBX emits warnings for unsupported normals on empty curve helpers; those non-rendered helpers were discarded. The selected fixture exports/reimports with the recorded geometry. The initial whole-pack exploratory preparation was superseded by the complete selected fixture.

**Unity compile/import/visual acceptance is pending root execution.** No Unity process was launched by this worker, no scene was changed and no commit was made. A successful Blender export is not claimed as a passing Unity import, native texture appearance or gameplay collision test.
