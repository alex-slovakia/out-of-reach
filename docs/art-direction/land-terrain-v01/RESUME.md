# RESUME - LAND-TERRAIN-01 checkpoints

Branch `feature/land-terrain-v1` (worktree `NASA/out-of-reach-terrain`, sparse: this folder and `docs/playtest-review/`), cut from `origin/codex/i01-integration` at `62ced0cf0cc2f74f2c23dfd292cdc1823a1ccca5`. Write scope: `docs/art-direction/land-terrain-v01/**` only.

## Checkpoint 1: terrain package (this commit)

**Finished:**
- `LT_LandTerrain.fbx` (LT_TerrainInner 48,212 tris, LT_TerrainFar 6,212, LT_Rocks 9,400; total 63,824) and `LT_LandTerrain.blend`;
- `textures/` (macro 2048², detail albedo and normal 1024², fallback tile 1024²), `renders/` (4 views and `LT_sheet.png`), `source/`, `unity_check/`;
- `lt_build_report.json`, `lt_build_roundtrip.json` (PASS), `lt_boundary_check.json` (PASS), `lt_manifest.json` (SHA-256 of every file except this one);
- Unity 6000.3.28f1 isolated import: PASS, 63,824 = 63,824 triangles.

The commit SHA is in the handoff comment on issue #7 and in the next checkpoint.

**Unfinished:** LAND-SURROUNDINGS v02 (second package). The plan:
1. `source/lss_kit_v02.py` takes the height function instead of the constant G. Every base, mound, berm and tree-line bottom follows `lt_height`.
2. Export `LT_Surroundings_v02.fbx` here, with the v01 group names plus the suffix `_v02`.
3. Run the round trip, the Unity check, and renders with the terrain.

**Next exact action:** write `source/lss_kit_v02.py` (copy of v01 `lss_kit.py`, G → `lt_height.ground_y` / `seat_y`) and `source/build_surroundings_v02.py`; build in the local work folder `NASA/blender/land-terrain-v01/out`.

## Commands (from the local work folder `NASA/blender/land-terrain-v01`)

```
python -B src/lt_textures.py out/textures
blender -b --factory-startup --python src/build_terrain.py -- --out out --textures out/textures --land-textures ../launch-site-v01/out/textures --land-src <LAND package>/source --render --samples 32 > out/build.txt 2>&1
blender -b --factory-startup --python src/roundtrip_habitat.py -- --out out --report lt_build_report.json > out/roundtrip.txt 2>&1
blender -b --factory-startup --python src/check_terrain.py -- --out out --land-fbx <LAND package>/LS_LandLaunchSite.fbx > out/boundary.txt 2>&1
python -B src/make_sheet.py out/renders LT
Unity.exe -batchmode -nographics -projectPath <scratch>/ls_unity_check -executeMethod LTCheck.Run -ltFbx Assets/LT/LT_LandTerrain.fbx -ltOut lt_check.tsv -logFile unity_lt.txt
python unity_check/lt_compare.py <project>/lt_check.tsv out/lt_build_report.json
python -B src/make_manifest.py <this folder>
```

## Known problems and limits

- Seen from the crew deck at 38 m, the relief reads as gentle: it is held to the requested 0.5–4 m nearby. Scrub, tree lines and berms (v02) do most of the work of breaking up the near ground.
- The preview sun (from v01 renders, elevation 31.5° from the north-east) back-lights the deck views; root's sun may differ.
- No collider. The URP material is documented but was not built or checked in Unity.
