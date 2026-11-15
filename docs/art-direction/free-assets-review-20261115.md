# Free asset review and integration handoff — 15 November 2026

The highest-value changes for this slice are readable supplies and tools, believable service equipment, photographic ground surfaces and a properly exposed space background. Trees and a replacement terrain mesh can wait: they add preparation and placement work without fixing the movement, hydration or interaction problems reported by Alex.

This review separates **download size**, **prepared source-package size**, **Unity import**, **scene placement** and **visual acceptance**. None is a substitute for the next. Prices and licenses below are the official listings reviewed by the team on 15 November 2026; no purchase or subscription was made.

## What is actually delivered

- **Poly Haven:** 13 prepared models plus two ground materials. Raw downloads total **199,420,866 bytes / 199.42 MB**. The prepared package is **147,276,025 bytes / 147.28 MB**, before Unity-generated materials and prefabs. This is **not the Windows build size**, not GPU memory, and not a claim that all 13 models are visible at once. The raw model subset alone is 167.50 MB; the two ground sources account for the remainder of the 199.42 MB total.
- **ambientCG ground:** Ground109 and Ground110, selected 2K members. **45,789,319 bytes / 45.79 MB transferred**, including metadata and ZIP-directory reads; source maps themselves total 45,760,397 bytes. Full ZIPs would total 66.92 MB, so unused members were not fetched.
- **ambientCG metal:** Metal009 and Metal027, 1K packages totaling **11,078,401 bytes / 11.08 MB**.
- **NASA sky:** the unmodified 4096×2048 EXR is **35,997,085 bytes / 35.997 MB**. Website receipt pages are additional small files.
- **Price paid for the above: €0.** Existing Blender, FFmpeg, Python/Pillow/NumPy and Unity were reused; no new runtime importer was installed.

MB means 1,000,000 bytes. MiB means 1,048,576 bytes. Numbers below are not estimates of the eventual compressed player build.

## Alex's Poly Haven selections

All linked Poly Haven assets are **€0, CC0, permitted for commercial game use** under the [official license](https://polyhaven.com/license). Source files, authors, API checksums and local SHA-256 receipts for chosen assets are in [the source manifest](polyhaven-import-20261115/source-manifest.json). Deferred entries use the already-read official file catalogues in `.local/asset-review/`; their quoted package sizes were inspected, not downloaded.

For chosen assets, the size is the actual selected source payload, including its required maps. For deferred assets, the size is the 1K FBX plus its API-listed dependencies. “1K” describes texture resolution; it does **not** guarantee a lightweight mesh.

| Alex's asset | Selected or reviewed source | Reduction and quality target | Scene use / decision | Value for this slice |
|---|---:|---|---|---|
| [jacaranda_tree](https://polyhaven.com/a/jacaranda_tree) | Deferred 1K FBX: **165.39 MB** | Requires geometry reduction, foliage cards/alpha review and sensible LODs; lowering texture resolution alone does not solve it. | **Deferred.** A decorative launch-site tree must not delay player movement or create expensive foliage overdraw. | Low now; useful only after the route and lighting are stable. |
| [flood_lights](https://polyhaven.com/a/flood_lights) | Chosen 1K: **14.72 MB** | Prepared one complete standing twin-lamp tripod, brackets and cable: about **11.8k triangles**, rather than the full source collection. | **Chosen/imported.** Earth launch apron and staging area; scene placement still needs final visual review. | High: readable scale and functional industrial detail. |
| [portable_generator](https://polyhaven.com/a/portable_generator) | Chosen 1K: **5.10 MB** | About **26.4k triangles**; metre-scale source parts retained. | **Chosen/imported.** Outdoor ground-service equipment. Static scenery, not a new battery/resource system and not a running combustion generator inside the spacecraft. | High outside; inappropriate as decorative cabin machinery. |
| [long_life_food](https://polyhaven.com/a/long_life_food) | Chosen 1K: **2.41 MB** | About **4.7k triangles** across the food pack. | **Chosen/imported.** Secured galley/storage supplies. Their visual presence does not create food inventory or a consume command. | High: makes the purpose of the supplies recognizable. |
| [tree_small_02](https://polyhaven.com/a/tree_small_02) | Deferred 1K FBX: **95.80 MB** | Needs reduced branches/foliage and LODs; texture downsizing is insufficient. | **Deferred.** Consider a few distant instances only after native frame-time measurement. | Low before movement acceptance. |
| [namaqualand_boulder_02](https://polyhaven.com/a/namaqualand_boulder_02) | Chosen 2K: **17.49 MB** | Lowest supplied LOD selected, then bounded to **30k triangles**; preserve silhouette and source normals. | **Chosen/imported.** Sparse rocky margins outside the protected launch and plume routes. | High: large, readable shapes give the ground depth. |
| [searsia_lucida](https://polyhaven.com/a/searsia_lucida) | Deferred 1K FBX: **16.47 MB** | More manageable than the large trees, but foliage alpha, LOD and placement still need work. | **Deferred.** Best candidate of these three for a later sparse shrub pass, if the fictional coastal setting and budget justify it. | Medium later; does not solve current playability. |
| [boulder_01](https://polyhaven.com/a/boulder_01) | Deferred 1K FBX: **10.39 MB** | Can be decimated and instanced after inspecting silhouette. | **Deferred.** Two selected rock families already cover the immediate scene need. | Medium; diminishing benefit before the existing rocks are placed well. |
| [rock_moss_set_02](https://polyhaven.com/a/rock_moss_set_02) | Deferred 1K FBX: **4.31 MB** | Small package; inspect individual objects and keep only useful variants. | **Deferred.** Moss introduces a different environmental cue; use only in deliberately damp/shaded areas. | Medium/low for this dry, maintained launch apron. |
| [coast_land_rocks_04](https://polyhaven.com/a/coast_land_rocks_04) | Chosen 2K: **102.77 MB** | The source FBX alone is **87.17 MB** and contains several LODs. Prepared LOD3 is reduced to **29,999 triangles**; the full high-detail collection is not used in-game. | **Chosen/imported.** Low coastal outcrops seated on the actual terrain mesh, clear of travel and plume zones. | High silhouette benefit, expensive source download already paid in time rather than money. |
| [combination_wrench](https://polyhaven.com/a/combination_wrench) | Chosen 1K: **2.19 MB** | **1,986 triangles**; retain original detail below the 5k tool target. | **Chosen/imported.** Secured repair-tool display. No implied new repair animation or interaction. | High close-range specificity for little geometry. |
| [adjustable_wrench](https://polyhaven.com/a/adjustable_wrench) | Chosen 1K: **3.66 MB** | **4,996 triangles**, measured metre dimensions and bottom-centre pack pivot. | **Chosen/imported.** Secured maintenance kit; verify orientation and mounting. | High when visible near the repair workflow. |
| [pliers](https://polyhaven.com/a/pliers) | Chosen 1K: **2.79 MB** | **4,540 triangles**. | **Chosen/imported.** Tool retention panel; no floating loose object in microgravity. | High local detail, low global impact. |
| [screwdrivers_02](https://polyhaven.com/a/screwdrivers_02) | Chosen 1K: **3.04 MB** | **3,548 triangles** for the selected set. | **Chosen/imported.** Secured tool group; ensure handles remain recognizable from the player view. | High when composed as one coherent tool station. |

The nine chosen assets from Alex's list plus the four additions below make the **13-model** preparation package. No trees or other foliage were downloaded in this package.

## Our additional finds

These additions also use Poly Haven's €0/CC0 commercial-use terms. They are supplemental props, not parallel gameplay systems.

| Added find | Actual selected source | Preparation / intended use | Decision and value |
|---|---:|---|---|
| [vintage_spacecraft_instrument](https://polyhaven.com/a/vintage_spacecraft_instrument) | 1K, **4.26 MB** | About 13.3k triangles; bounded service-panel detail. It must read as a secured instrument, not a replacement for an authoritative mission display. | Chosen/imported. Useful spacecraft-specific detail after mounting and scale review. |
| [medical_box](https://polyhaven.com/a/medical_box) | 1K, **2.94 MB** | About 3.7k triangles; retained case in a clearly identified storage location. | Chosen/imported. Recognizable emergency supply category; no healing mechanic added. |
| [metal_toolbox](https://polyhaven.com/a/metal_toolbox) | 1K, **3.66 MB** | About 14.2k triangles; ground-service and secured workshop context. | Chosen/imported. Helps organize the selected tools into a plausible station. |
| [binoculars](https://polyhaven.com/a/binoculars) | 1K, **2.48 MB** | About 19.2k triangles; secured observation equipment. | Chosen/imported. Lower priority than supplies/tools; retain only if the actual framing justifies the visual density. |
| [coast_sand_02](https://polyhaven.com/a/coast_sand_02) | 2K maps, **14.67 MB** | Albedo/normal/roughness; official reference footprint **15×15 m per repeat**. No displacement downloaded. | Imported material alternative. Do not claim that its texture alone creates terrain geometry. |
| [gravelly_sand](https://polyhaven.com/a/gravelly_sand) | 2K maps, **17.25 MB** | Albedo/normal/roughness; official reference footprint **2.48×2.48 m per repeat**. | Imported material alternative. Prefer a deliberate blend or one consistent surface treatment over stacking competing ground systems. |

`camera_01` returned HTTP 404 during the earlier selection and was skipped. No substitute is presented as that unavailable asset.

## Ground and metal surfaces

All assets in this section are **€0 / CC0 1.0**, with commercial game use and redistribution allowed by the [ambientCG license policy](https://docs.ambientcg.com/license/). The source and derivative hashes for the installed ground and metal maps are recorded in [ground receipts](ground-finish-20261115/source-manifest.json) and [metal receipts](ground-finish-20261115/metal-source-manifest.json).

| Source | Origin | Size and resolution | Reduction / integration choice | Decision and value |
|---|---|---|---|---|
| [Ground109](https://ambientcg.com/a/Ground109) | Our selection from Alex's ground catalogue | 2K selected source maps **21.64 MB**; full package 31.76 MB | Compacted soil, 3.4 m authored repeat; correct sRGB color and linear normal/roughness/AO. | Chosen and imported into the near-ground finish. High value close to the apron. |
| [Ground110](https://ambientcg.com/a/Ground110) | Our selection from Alex's ground catalogue | 2K selected source maps **24.12 MB**; full package 35.16 MB | Rocky gravel, 2.6 m authored repeat, rotated coordinates and irregular broad blending with Ground109. | Chosen and imported. Gives believable small-scale rocky variation without placing thousands of colliders. |
| [Metal009](https://ambientcg.com/a/Metal009) | Our selection from Alex's metal catalogue | 1K package **4.77 MB** | Brushed steel; OpenGL normals and packed metallic/smoothness. Source scan width is unspecified, so prop UV scale is an authored choice. | Chosen/imported for new mounting brackets and service details only. High value at hand distance. |
| [Metal027](https://ambientcg.com/a/Metal027) | Our selection from Alex's metal catalogue | 1K package **6.31 MB** | Dark powder coat; use the actual metalness/roughness maps rather than making every surface mirror-like. | Chosen/imported for new service panels/brackets. Existing Claude ship materials are not globally remapped. |
| [Terrain003](https://ambientcg.com/a/Terrain003) | Alex's terrain request | 1K OBJ **118,504,093 bytes / 113.01 MiB**; 2K OBJ **501,016,687 bytes / 477.81 MiB** | Alternative 2K heightfield route: EXR **16,857,273 bytes** + JPG **17,732,979 bytes**, combined **32.99 MiB**. Requires a deliberately bounded mesh and preservation of pad/corridor heights. | **Deferred as a full mesh.** The existing authored macro geography already fits the mission. A complete replacement risks route, foundation and horizon regressions for limited immediate benefit. |
| [Ground098](https://ambientcg.com/a/Ground098) | Additional reviewed alternative | Reviewed 2K package **21.64 MB** | Candidate photographic surface; would still need texture scale, normal and roughness validation in this scene. | Deferred. Do not download another similar surface before reviewing the two installed ones. |
| [Ground031](https://ambientcg.com/a/Ground031) | Additional reviewed alternative | Reviewed 2K package **31.82 MB** | Another surface alternative, not geometry. | Deferred; reserve for a specifically identified missing ground type. |
| [Rock039](https://ambientcg.com/a/Rock039) | Additional reviewed alternative | Reviewed 2K package **35.59 MB** | Rock surface candidate; geometry/UV silhouette remains a separate task. | Deferred. Existing rock scans and the new gravel material cover the current placement pass. |

Terrain003 and the three alternative package figures are the team's already-checked official catalogue observations, not newly downloaded files in this handoff. The reproducible metadata endpoint for Terrain003 is [the official API request](https://ambientcg.com/api/v2/full_json?id=Terrain003&include=downloadData).

The ground solution retains the existing landscape's dunes, drainage, graded zones and distant horizon. It clones the inner mesh, adds local conforming subdivisions and bakes small visual relief; it does not move the gameplay floor or add a second terrain collider. Root's native import run measured **53,092 vertices / 105,908 triangles**, **28,167 displaced vertices**, maximum displacement **0.023632 m**, and **zero displaced protected-zone vertices**. These are geometry checks, not proof that every camera angle looks good.

## BlenderKit grass and NASA sky

| Source | Price / commercial license | Size | Scene use and decision | Preparation and value |
|---|---|---:|---|---|
| [Grass Generic Small](https://www.blendkit.com/asset-gallery-detail/178a5199-4133-4670-b78d-bbfabec75bd0/) | Listing reviewed as **Free / Royalty Free**, commercial use allowed under the [BlenderKit license](https://www.blendkit.com/docs/licenses/). **Not CC0**; do not resell/redistribute it as an asset product. | Listing: **19 MiB**, **1,615 polygons**; exact download bytes and dependencies not acquired. | **Deferred.** Potentially useful for small perimeter patches, not blanket coverage or travel-route obstruction. | Check the exact asset license at acquisition, convert with existing Blender, inspect alpha edges and instance sparsely. Polygon count alone does not capture foliage overdraw. Medium later value. |
| [NASA Deep Star Maps 2020](https://svs.gsfc.nasa.gov/4851/) | **€0.** NASA SVS public-domain reuse policy applies unless otherwise marked; keep NASA/Goddard SVS and ESA/Gaia/DPAC credit, no endorsement implication. This is not a CC0 claim. | 4096×2048 EXR, **35,997,085 bytes / 35.997 MB** | **Chosen/imported source** for the deep-space sky; native exposure/framing review remains necessary. | Real catalogue-based star map with the Milky Way, not a 2026 camera photograph. Control exposure beside a sunlit hull; do not make faint stars equally bright under every lighting condition. High visual value without adding environment geometry. |

NASA's exact [4K EXR source](https://svs.gsfc.nasa.gov/vis/a000000/a004800/a004851/starmap_2020_4k.exr), retrieval hashes and reuse-policy receipts are preserved in [the sky handoff](nasa-deep-sky-20261115/README.md) and [retrieval record](nasa-deep-sky-20261115/retrieval.json). The image is a dated celestial reference in ICRF/J2000 coordinates; source color choices and game exposure are artistic presentation decisions, not spacecraft telemetry.

## Quality target and next steps

1. **Finish and verify movement first.** No material pack fixes failed wall/ceiling pushes, body clipping, slow traversal or missing grip animation. Judge those using a short physical route with actual controls, not screenshots.
2. **Make supplies legible.** Food, drinking water, batteries and tools need distinctive shape, restrained category color, readable labels and a visible path to their actual interaction. A decorative food can or medical case must not imply a working command it does not have.
3. **Use fewer, well-mounted props.** Metre-correct scale, restrained materials, visible fasteners/retention and a clear silhouette matter more than filling every surface. Keep cargo, handholds, doors and repair approach zones unobstructed. Do not put loose tools in a microgravity cabin.
4. **Review ground at player height.** Check photographic texture scale, blending, centimetre relief, contacts beneath rocks and the transition to the existing far terrain. Keep the pad, crawlerway, trench/plume route and collision authority intact.
5. **Review sky and metals under the actual grade.** Verify normal-map orientation, roughness and reflection exposure. More gloss, larger textures or a brighter Milky Way do not automatically make the scene more realistic.
6. **Stop adding catalogue assets until the latest captures pass.** Only then consider sparse grass/shrubs or another rock variant. Measure frame time, loaded textures and player-build size on the named target machine before making performance claims.

The intended quality is a coherent, believable, readable mission slice: correct scale and contact, controlled material response, clear interactions and reliable movement. “AAA” is an artistic reference, not a completion percentage or a guarantee that a short asset-import pass can deliver studio-scale polish.

## Verification boundary and handoff

Root's **Unity 6000.3.28f1 `polyhaven-scene-03` import/authoring run passed**. Its log records 15 curated model/material entries, the ground measurements above, 20 mounted static assets, one camera/listener/runtime/player, 29 interaction targets unobstructed within the existing 2 m structural check, and two gates. [Unity import evidence](polyhaven-import-20261115/unity-import-evidence.json) and [placement record](polyhaven-import-20261115/scene-placement.txt) document that checkpoint.

**Latest layout changes and screenshots remain pending review.** The successful import is not a complete gameplay traversal, a passing Windows build, acceptance of every mounting position, or proof of the new movement package. The native review scope explicitly uses editor review cameras and is not gameplay traversal evidence. Older “pending Unity import” text in worker preparation notes describes the earlier preparation stage; the root `03` run is the later import checkpoint, without extending its evidence to subsequent revisions.

This report was assembled from existing local metadata and worker receipts only. No additional asset downloads, scene changes, runtime edits or purchases were made for the report.
