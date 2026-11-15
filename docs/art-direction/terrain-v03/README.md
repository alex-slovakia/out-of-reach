# TERRAIN-V03: simpler continuous launch-site land, golden/olive meadow

**Tasks:**
- Issue #7 comment 6895002547 item 4: the user rejected `c95cccd` from the elevated boarding deck (brown
  patchwork, flat green macro terrain, black distant shapes).
- Alex's grass reference #7 6895376530: dense coherent golden/olive grass, fine blades, soft wind, warm backlit
  tips, no clutter.

**Claims and reports:** claim 6895092001, first DONE 6896034555, reference ACK 6896135968.
Writer: Claude (Apoliak7777), Claude Opus 5.5.

**Branch:** `feature/terrain-v03` from `codex/i01-integration@dc9ff8b` (contains `c95cccd`). Not merged.

## What changed

1. **One continuous ground shader.** `Visuals/Environment/GroundFinishV02/GroundFinishV02.shader`, same asset GUID,
   now "OutOfReach/Launch Ground V03".
   - A golden/olive grass palette over broad, domain-warped world-space fields.
   - A five-octave meadow mottle and "combed" streaks along a slowly turning wind direction. All fine detail fades
     by pixel footprint, so nothing shimmers and there are no bands.
   - Soft wind: gust bands roll across the field.
   - Backlit sheen.
   - The canopy darkens towards grazing views: a dense field shows shaded stems at a low angle.
   - Ground109 (CC0, already in the repo) is used only as fading near detail with anti-repetition.
   - No gravel blobs and no camouflage macro map.
   - Scrub and warm ochre rock on slopes and heights. Aerial haze is blue away from the sun and warm towards it,
     only while the scene fog is on.
2. **Dense meadow with real blades.** `Visuals/LaunchVegetationV03/GrassShellsV03.shader` + `.mat`, and 8 meshes
   `Generated/Grass shells V03 sector 00-07.asset` (24 layers, 317,928 triangles).
   - A shell base grid starts at the apron edge and reaches about 440 m. Its heights and normals are sampled from
     the real `LT_TerrainInner`.
   - A per-pixel height field (0.68–0.95 m, shorter near the facility) decides which layers exist.
   - Near the camera: individual tapering ribbon blades on two jittered grids, wind bend, golden tips with warm
     back-light, dark self-shadowed bases, grounded by the scene's shadows.
   - The real-blade zone is limited by the material property `_ShellRange` (46 m). Beyond it the ground shader alone
     carries the meadow, and the shells leave after one distance test. From the deck (at least 77 m away) they cost
     nothing.
   - **Left clear in the height field:** the crawlerway corridor (x ±24, z −915..−55); the flame trench / plume
     corridor (x ±30, z 62..335); a mown shoulder at the apron edge.
   - No colliders, no shadow casting.
3. **Far land with mountains.** 16 meshes `Visuals/LaunchLandscapeV02/Generated/Far land V03 sector 00-15.asset`:
   210,180 triangles.
   - The inner edge is stitched vertex-for-vertex to `LT_TerrainInner`'s outer boundary: 260 seam vertices with their
     heights and normals, no overlap.
   - Rolling foothills rise into a ridged range at about 6–10 km, crests up to 898 m above the datum.
   - The rim at 10.2 km dissolves into the haze (camera far clip 10.5 km).
4. **Clutter off (deactivated, nothing deleted).** Paths are relative to `Launch site - stationary`:
   - `Coastal relief and launch pad detail/LT_LandTerrain/LT_TerrainFar` (replaced by the far land)
   - `Coastal relief and launch pad detail/LT_LandTerrain/LT_Rocks` (470 rocks)
   - `Coastal relief and launch pad detail/LT_Surroundings_v02` (berms, culverts, riprap, hummocks, fence, poles,
     the dark tree lines)
   - `Launch landscape V02 - ridges and dry scrub` (sand-dune ridgelines)
   - `Launch vegetation V03 - photographed foreground` (tufts, shrubs, pebbles; about 2.3 M LOD0 triangles)
   - The six loose Poly Haven rocks in `Curated CC0 service details`. Their colliders were already disabled; the
     installer refuses a rock whose collider is enabled.

**Unchanged:** buildings, huts, pad, apron joints, crawlerway, tower, ship, staging platform, the generator, flood
lights and toolbox with their active colliders, every collider, the global fog/sun/sky set by `I06MarineBindings`,
and `Bootstrap.unity` in this branch.

## Integrator steps (one integrator; schedule names local_12d9929a)

1. Bring this branch's files in. All paths are terrain-owned.
2. Run the installer. It opens Bootstrap, applies, saves the assets and the scene:

   ```
   Unity.exe -batchmode -quit -projectPath <checkout> -executeMethod OutOfReach.Presentation.LaunchLandscape.Editor.LaunchTerrainV03Installer.InstallIntoBootstrap -logFile <log>
   ```

   - Expected log on the `dc9ff8b` scene: `LAUNCH_TERRAIN_V03_INSTALLED ... meadowShells=24x8 meadowTriangles=317928 deactivated=11 ... collidersChanged=0`.
   - A re-run is safe: it replaces only its own two groups, updates its meshes in place (same GUIDs) and logs
     `deactivated=0`.
   - It throws, and leaves the scene unchanged, if the set of active colliders would change.
3. Without batch mode: menu **Tools > OUT OF REACH > Terrain V03 - apply to open scene (no save)**, check, then save.
4. Run it after any re-run of `I06GroundFinishAuthoring`, `I06PolyHavenSceneAuthoring`, `LaunchLandscapeAuthoring`
   or `LaunchVegetationAuthoring`; those recreate the old groups.
5. If a cinematic camera sits inside the grass on a weak GPU, lower `_ShellRange` on `GrassShellsV03.mat` (for
   example 25). The installer writes 46.

## Verification actually run (this clone only)

Environment: Unity 6000.3.28f1, batch mode, D3D11 on the real GPU (Intel UHD integrated, no window), own worktree
`NASA\out-of-reach-terrain3`.

1. **Before:** saved Bootstrap at `dc9ff8b`, Play mode, the real Planning bind (fog exp2 0.00013, far clip
   10,500 m), fixed views. The before frames were captured with 4× MSAA on the capture target. All after frames use
   none, like the game (camera FXAA only, PC URP asset MSAA off).
2. **Install:** `InstallIntoBootstrap` run twice. First run: `deactivated=11`. Second run: `deactivated=0`, exactly
   one of each V03 group, `collidersChanged=0` (instance id plus enabled-and-active state of every collider under
   the site). Lines: `qa/install-log-lines.txt`.
3. **After:** the same views plus three ground close-ups (camera 1.7 m above the sampled real ground) captured from
   the **saved, installed** scene in Play mode (`qa/installed-capture-notes.txt`).
   - The only console exception is the known `UnityEditor.Search` indexing error at editor start.
   - Bootstrap and the Poly Haven `.mat` files that Unity rewrote on import were restored afterwards.
4. **Frame cost** (`qa/perf-*.txt`): 1920×1080, wall time median of 150 frames, the same harness and the same Intel
   UHD. The before run used the original ground shader and material from `dc9ff8b`. FrameTimingManager GPU time was
   not available in batch mode.

| View | Before (`c95cccd`) | After (V03) |
|---|---|---|
| Deck west | 36.4 ms | 36.0 ms |
| Deck east | 25.4 ms | 28.8 ms |
| Deck towards facility | 31.0 ms | 28.1 ms |
| Ground close-up in the grass (backlit) | 31.7 ms | 136.5 ms |

Captures (`captures/`):
- `compare-*.jpg`: before | after for deck west and east, deck towards the facility, deck SW and SE, at the
  facility, ground at the apron, root's ground reference, aerial.
- `after-*.jpg`: the player's start view, deck down-view, ascent altitude, and three ground close-ups (13 backlit,
  14 side, 15 down).
- `overview-deck-before-after.jpg`: the deck views at a glance.

## Honest limits

- **Close-up cost:** standing in the grass costs about 4× more on integrated graphics (24 layers near the camera).
  The player never stands in the grass in the prologue; `_ShellRange` trades blade distance for cost.
- **Close-up look:** these are shells. At very grazing angles their layered nature can show, and the edge of the
  real-blade zone remains a soft brighter band when looking straight into the sun.
- **Ascent views:** from more than about 1 km altitude, the far-land rim shows as a hazy line inside the far clip,
  as the old ring did.
- **Fence and poles** went with `LT_Surroundings_v02` ("no props"); re-enable `LSS_Fence_v02` alone if wanted.
- **Not terrain:** a pre-existing thin white vertical line about 10 km west, near the ship origin, shows in both
  before and after frames.
- **Not touched:** the sky HDR, sun and fog (root/launch-fx files).
- **Not run:** no PlayMode suite or full route for this art change; the GPU look acceptance is the user's.
