# LAUNCH-GRASS-V04: pampas field, launch look and thruster gust (#11 R07)

An original wind-reactive golden pampas / feather-grass field for the launch site, inspired by the
direction Alex described for the reference: tall back-lit feathery seed heads, waist-high density,
rolling wind waves, warm haze to a soft horizon. No asset from the reference is used. Meshes,
shaders and patterns are procedural and were written for OUT OF REACH.

Built on Apoliak's TERRAIN-V03 (`2a70bd0`, cherry-picked here): its golden/olive ground shader
remains the base layer and stands in for the far field beyond the grass draw distance. The installer
switches off its shell meadow (`Launch grass V03 - dense meadow`, kept, not deleted) because the
V04 field replaces it, and the shells measured 136 ms/frame in the ground close-up on Intel UHD
(`docs/art-direction/terrain-v03/qa/perf-after.txt`). LAUNCH-FX-V03 (`c5c5faf`..`0331c91`) is
already integrated in the base and is unchanged. The gust reads the same departure playhead.

## Files (all owned, new)

| Path | What |
|---|---|
| `Visuals/LaunchGrassV04/PampasGrassV04.shader` | HLSL URP grass: stalks, arching leaves, crossed feather plumes with procedural barbs, far 3-quad cards, back-light translucency, wind gust waves, radial thruster push, aerial haze, LOD cross-fade, Forward/DepthOnly/DepthNormals |
| `Visuals/LaunchGrassV04/LaunchHazeDomeV04.shader` | Camera-centred haze dome: warm horizon band glowing towards the sun azimuth |
| `Scripts/Presentation/LaunchGrassV04/PampasGrassField.cs` | Instance generation from the real `LT_TerrainInner` heights + clear areas; per-camera chunk culling; `Graphics.RenderMeshPrimitives` |
| `Scripts/Presentation/LaunchGrassV04/PampasGrassMeshes.cs` | Procedural clump (near), card (far) and dome meshes |
| `Scripts/Presentation/LaunchGrassV04/LaunchGrassGustPresenter.cs` | Read-only departure sampler → `_OOR_GrassGust*`, Volume weight, haze |
| `Scripts/Presentation/LaunchGrassV04/Editor/LaunchGrassV04Installer.cs` | Menu installer, batch entry, Volume profile and materials |
| `Integration~/ClaudeIntegrationAuthoring-LaunchGrassV04.patch` | Optional composition hook (root-owned file) |

The installer creates `PampasGrassV04.mat`, `LaunchHazeDomeV04.mat` and `LaunchCinematicV04.asset` in
`Visuals/LaunchGrassV04/` the first time it runs. Commit them with the scene.

## Install

* **Editor, open scene (no save):** open `Bootstrap.unity`, then choose **Tools → OUT OF REACH →
  Launch Grass V04 - install into open scene (no save)**. Review the scene and save it yourself.
* **Batch (integrator, saves Bootstrap):**
  `Unity -batchmode -quit -projectPath <clone> -executeMethod OutOfReach.Presentation.LaunchGrassV04.Editor.LaunchGrassV04Installer.InstallIntoBootstrap`
* **Composition hook (optional):** `git apply` the patch in this folder. It adds
  `ClaudeIntegrationAuthoring.ApplyLaunchGrassV04` and the two asmdef references.

The installer is idempotent. It adds no collider and checks that the active collider set has not
changed. The field goes under `Launch site - stationary`, so it switches off with the site. The look
object goes beside the site, so the grade can blend out before the site switches off at
`UpperAirSeconds`. It binds the scene's one `TransportDeparturePresenter`, and uses
`Land pad top plane` as the gust origin.

## Runtime contract (globals, presentation only)

* `_OOR_GrassWind` = (dir.x, dir.z, sway rad, gust rad). `_OOR_GrassWindWave` = (speed m/s, wavelength m, reduced 0/1, -).
* `_OOR_GrassGust` = (origin xyz world, strength 0..1). `_OOR_GrassGustWave` = (front radius m, front width m, sustained reach m, flutter).
* `_OOR_LaunchHaze` = haze multiplier. `_OOR_GrassLod` = (near m, band m, max m, far count fraction).

Gust envelope on the transport playhead: zero before `IgnitionSeconds` (9 s). It rises in 0.35 s. The
front runs out at about 46 m/s and eases to a 260 m radius. A sustained push and shiver follow, and fade
from 3.5 s after liftoff. The grade is at full weight through ignition and the pad and climb shots, and
fades over 15.3–17.3 s. A pause (`IsSuspended`) holds every value. Reduced effects come from the
departure's `ReducedMotion`, which composition already sets from `SetReducedEffects`. They give half the
gust with no shiver, no film grain, 55 % bloom, 55 % density and 65 % draw distance.
