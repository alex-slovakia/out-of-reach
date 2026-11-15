# RESUME - LAND-TERRAIN-01 checkpoints

Branch `feature/land-terrain-v1` (worktree `NASA/out-of-reach-terrain`, sparse: this folder and `docs/playtest-review/`), cut from `origin/codex/i01-integration` at `62ced0cf0cc2f74f2c23dfd292cdc1823a1ccca5`. Write scope: `docs/art-direction/land-terrain-v01/**` only.

## Checkpoint 1: terrain package

- Pushed as `9ee0985a9643dc5cbb1a5aaecdc78f578b481929`; handoff posted on issue #7.
- Superseded by checkpoint 2: the rocks there were seated on the analytic h and could float up to about 1 m above the rendered mesh.

## Checkpoint 2: terrain fix and LAND-SURROUNDINGS v02 (this commit)

**Finished:**
- **Terrain update:**
  - rocks are seated on the rendered mesh (`source/lt_surface.py`): 470/470 bottoms below the lowest mesh ground under them;
  - dunes fade out over 500–1,300 m (mesh resolution).
  - Re-run on the new files: round trip PASS 7/7, boundary and seam PASS, Unity PASS 63,824 = 63,824.
- **`LT_Surroundings_v02.fbx`:** the 7 v01 groups (+ `_v02`), 29,088 triangles, 7 anchors, re-seated on the terrain mesh. Round trip PASS 14/14, Unity PASS 29,088 = 29,088, seating 0 floating base points.
- **Combined renders:** `renders/LT2_*`.
- **`lt_manifest.json`:** covers both packages (SHA-256 of every file except this one).

The commit SHA is in the second handoff comment on issue #7.

**Unfinished, optional:**
- material check in a Unity URP scene (the import checks are mesh-only);
- a per-pixel overlap check of hummocks, berms and canopy against the terrain;
- colliders.

**Next exact action, if asked:** integrate in root's scene. Disable the old ground plane at y −36.6, import both FBX with Bake Axis Conversion ON, build `LT_Terrain`, `LT_TerrainFar` and `LT_Rock` from `source/lt_materials.json`, and set the camera far clip to at least 10 km or use fog.

## Commands (from the local work folder `NASA/blender/land-terrain-v01`)

```
python -B src/lt_textures.py out/textures
blender -b --factory-startup --python src/build_terrain.py -- --out out --textures out/textures --land-textures ../launch-site-v01/out/textures --land-src <LAND package>/source --render --samples 32 > out/build.txt 2>&1
blender -b --factory-startup --python src/roundtrip_habitat.py -- --out out --report lt_build_report.json > out/roundtrip.txt 2>&1
blender -b --factory-startup --python src/check_terrain.py -- --out out --land-fbx <LAND package>/LS_LandLaunchSite.fbx > out/boundary.txt 2>&1
python -B src/make_sheet.py out/renders LT
blender -b --factory-startup --python src/build_surroundings_v02.py -- --out out --textures ../land-surroundings-v01/out/textures --lt-textures out/textures > out/build_v02.txt 2>&1
blender -b --factory-startup --python src/roundtrip_habitat.py -- --out out --report lss_v02_build_report.json > out/roundtrip_v02.txt 2>&1
blender -b --factory-startup --python src/build_terrain.py -- --out out2 --textures out/textures --land-textures ../launch-site-v01/out/textures --lss-textures ../land-surroundings-v01/out/textures --land-src <LAND package>/source --render --surroundings --samples 32 > out2/build.txt 2>&1
python -B src/make_sheet.py out2/renders LT2
Unity.exe -batchmode -nographics -projectPath <scratch>/ls_unity_check -executeMethod LTCheck.Run -ltFbx Assets/LT/<fbx> -ltOut <tsv> -logFile <txt>
python unity_check/lt_compare.py <tsv> out/<report>.json
python -B src/make_manifest.py <this folder>
```

## Known problems and limits

- Seen from the crew deck at 38 m, the relief reads as gentle: it is held to the requested 0.5–4 m nearby.
- The preview sun (from the v01 renders, elevation 31.5° from the north-east) back-lights the deck views; root's sun may differ.
- Between vertices, h and the mesh differ by about ±0.9 m (p1–p99) at 80–2,900 m. Seat things on the mesh (`lt_surface`, or a raycast in Unity), not on h.
- The v01 scrub clumps and tree lines keep their v01 look: dark olive domes and bands.
- No collider. The URP materials are documented but were not built or checked in Unity.
