# TERRAIN-V03: simpler continuous launch-site land

Task: issue #7 comment 6895002547 item 4 (user rejected `c95cccd` from the elevated boarding deck: brown patchwork,
flat green macro terrain, black distant shapes). Claim 6895092001. Writer: Claude (Apoliak7777), Claude Opus 5.5.
Branch `feature/terrain-v03` from `codex/i01-integration@dc9ff8b` (contains `c95cccd`). Not merged.

## What changed

1. **One continuous ground shader.** `Visuals/Environment/GroundFinishV02/GroundFinishV02.shader`, same asset GUID,
   now "OutOfReach/Launch Ground V03".
   - A single grass palette (dry straw, olive, green in swales) blended by broad, domain-warped world-space fields
     (hundreds of metres, smooth transitions).
   - A five-octave meadow mottle (1-50 m) that fades by pixel footprint towards its own mean, so it carries from the
     apron edge to the horizon without shimmer or bands.
   - Ground109 (CC0, already in the repo) only as near-camera luminance and normal detail, sampled with
     anti-repetition (no tiling grid).
   - No Ground110 gravel blobs and no camouflage macro map any more.
   - Slopes turn into olive scrub, steep or high ground into warm ochre rock.
   - Aerial-perspective haze: blue away from the sun, warm towards it, thinner with altitude. It applies only while
     the scene fog is on.
   - `GroundFinishV02.mat` keeps its GUID and texture bindings; the new palette values are stored on it.
2. **Far land with mountains.**
   - 16 generated meshes `Visuals/LaunchLandscapeV02/Generated/Far land V03 sector 00-15.asset`: 210,180 triangles,
     105,860 vertices, no colliders, shadows off.
   - The inner edge is stitched vertex-for-vertex to `LT_TerrainInner`'s outer boundary (260 seam vertices, its
     heights and normals). No overlap, no z-fighting.
   - It rises through rolling foothills into a ridged mountain range at about 6-10 km: crests up to 898 m above the
     datum, heights varying by direction (massifs and lower passes).
   - The rim at 10.2 km dissolves into the haze (camera far clip 10.5 km).
   - Same material as the near ground, so near and far share one look.
3. **Clutter off (deactivated, nothing deleted).** Paths are relative to `Launch site - stationary`:
   - `Coastal relief and launch pad detail/LT_LandTerrain/LT_TerrainFar`: the flat green far ring, replaced.
   - `Coastal relief and launch pad detail/LT_LandTerrain/LT_Rocks`: 470 rocks.
   - `Coastal relief and launch pad detail/LT_Surroundings_v02`: berms, drainage riprap and culverts, hummocks,
     fence, utility poles and the dark tree lines (the "black distant shapes").
   - `Launch landscape V02 - ridges and dry scrub`: sand-dune ridgelines.
   - `Launch vegetation V03 - photographed foreground`: 16 patches of tufts, shrubs and pebbles, about 2.3 M LOD0
     triangles.
   - The six loose Poly Haven rocks in `Curated CC0 service details`. Their colliders were already disabled; the
     installer refuses to touch a rock whose collider is enabled.

**Unchanged:** buildings, huts, pad, apron joints, crawlerway, tower, ship, staging platform, the generator,
flood lights and toolbox with their active colliders, every collider, the global fog/sun/sky set by
`I06MarineBindings`, and `Bootstrap.unity` in this branch.

## Root integration (one integrator)

1. Bring this branch's files into the integration checkout. All paths are terrain-owned and disjoint from the other
   lanes.
2. Install into Bootstrap. It opens the scene, applies, saves the assets and the scene:

   ```
   Unity.exe -batchmode -quit -projectPath <checkout> -executeMethod OutOfReach.Presentation.LaunchLandscape.Editor.LaunchTerrainV03Installer.InstallIntoBootstrap -logFile <log>
   ```

   - Expected log on the `dc9ff8b` scene: `LAUNCH_TERRAIN_V03_INSTALLED ... farTriangles=210180 ... deactivated=11 ... collidersChanged=0`.
   - Re-running is safe: it replaces only its own group and updates the meshes in place (same GUIDs), and logs
     `deactivated=0`.
   - The method throws, and leaves the scene unchanged, if the active set of colliders would change.
3. Without batch mode, use the editor menu **Tools > OUT OF REACH > Terrain V03 - apply to open scene (no save)**,
   check the result, then save. This works in Alex's own editor if he wants to do it by hand.
4. Run it **after** any re-run of `I06GroundFinishAuthoring`, `I06PolyHavenSceneAuthoring`,
   `LaunchLandscapeAuthoring` or `LaunchVegetationAuthoring`. Those recreate or re-enable the old groups. Better:
   stop re-running the two V02/V03 clutter tools.

C# API: `LaunchTerrainV03Installer.Apply(Transform site)` (Edit mode, no scene save). `LastReport` holds the
measured counts. Assembly `OutOfReach.LaunchLandscape.Editor` (unchanged asmdef, no new assembly).

## Verification actually run (this clone only)

Environment: Unity 6000.3.28f1, batch mode, D3D11 on the real GPU (Intel UHD, no window), own worktree
`NASA\out-of-reach-terrain3`.

1. **Before:** saved Bootstrap at `dc9ff8b`, Play mode, waited for the real Planning bind (fog exp2 0.00013, far clip
   10,500 m, sky `MAT_LaunchSky_RockyRidge`), then rendered fixed views. Harness: `qa/TerrainV03Qa.cs.txt`; views:
   `qa/views.json`.
2. **Install:** `InstallIntoBootstrap` run twice in batch. First run: `deactivated=11`. Second run: `deactivated=0`,
   exactly one V03 group in the saved scene, `collidersChanged=0` (the check compares instance id plus
   enabled-and-active state of every collider under the site). Lines: `qa/install-log-lines.txt`.
3. **After:** the same views captured from the **saved, installed** scene in Play mode at Planning
   (`qa/installed-capture-notes.txt`). The only console exception is the known `UnityEditor.Search` indexing
   error at editor start.
4. Bootstrap and the Poly Haven `.mat` files that Unity rewrote on import were restored afterwards. Neither is part
   of this branch.

Captures (`captures/`):

- `compare-*.jpg`: before and after side by side. Views: deck west and east, deck towards the facility, deck SW and
  SE, at the facility looking at the pad, ground at the apron, root's ground reference, aerial.
- `after-*.jpg`: the player's own start view, deck down-view, and two high ascent-altitude views.
- `overview-deck-before-after.jpg`: the deck views at a glance.

## Honest limits

- **Performance:** no frame-time benchmark. The ground fragment shader does about 28 value-noise evaluations and 10
  texture samples per pixel, plus 210 k far-land triangles. The 2.3 M vegetation triangles are gone.
- **Eye-level grass** is shader-only (no 3D blades). It reads as dry meadow from the deck; at eye level it is a
  textured surface.
- **Ascent views:** from more than about 1 km altitude, the far-land rim shows as a hazy line inside the far clip.
  The old 9 km ring had the same kind of edge.
- **Fence and poles** went with `LT_Surroundings_v02` (the "no props" direction). Root can re-enable only
  `LSS_Fence_v02` if a perimeter fence is wanted.
- **Not terrain:** a pre-existing thin white vertical line about 10 km west, near the ship origin at world
  (0,0,1200), shows in both the before and after frames.
- **Not touched:** the sky HDR, sun and fog (root/launch-fx files).
- **No playthrough:** no PlayMode test suite or full route was run for this art change. Only the saved-scene
  Planning bind and the camera captures above.
