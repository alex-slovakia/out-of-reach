# Ground Finish V02 — integration candidate

Ground109(compacted soil) and Ground110(rocky gravel) from ambientCG are CC0 photographic materials, selected after viewing official previews. Official metadata, URLs, source dimensions, ZIP CRCs and SHA-256 hashes are in source-manifest.json. The download script fetched only the selected2K ZIP members using supported HTTP ranges:45,789,319bytes total, below the50MB budget. Original downloads stay ignored in source-cache/. Each output map is2048x2048.

## Root integration

Call `OutOfReach.Composition.Editor.I06GroundFinishAuthoring.Apply(site)` with the stationary launch-site transform. Root then owns SaveAssets/scene save. No worker scene or Unity process was used. New files only. Existing LandTerrainV01 assets and colliders are untouched.

The shader uses2.6m gravel/3.4m soil repeats, rotated independent coordinates, irregular broad blend patches, photographic normals/roughness/AO, and a larger low-contrast color variation. It uses URP PBR lighting with main/additional shadows, fog, depth and normal passes. The authored macro texture returns between180–360m; the existing far renderer/material stays untouched. Existing near ground hummocks get the matching finish without geometry changes.

The inner terrain is cloned to a NEW mesh asset. Three conforming edge-refinement passes outside protected zones add local resolution only within230m; the budget is160,000triangles. Original macro positions interpolate exactly before the visual displacement. New relief is bounded to±2.5cm and fades out by225m. It is zero inside and within10m of the launchpad, crawler/facility corridor and plume/trench corridor. The outer seam is unchanged. Original normals are adjusted by the authored offset gradient, preserving distant analytic normals. No collision geometry is added, moved or modified. `Microrelief(x,z)` exposes only the added offset, not the original mesh surface. To seat objects, root should raycast a temporary collider against the actual cloned mesh and then remove it.

`LastReport` contains measured original/new vertices, triangles, displaced count, maximum displacement and protected displaced count after root calls Apply. Native shader compile, saved-scene render, clearance checks and performance remain **pending root verification**. No visual acceptance is claimed from source inspection.
