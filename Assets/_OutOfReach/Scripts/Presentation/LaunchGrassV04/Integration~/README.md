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

* `_OOR_GrassWind` = (dir.x, dir.z, sway rad, gust rad). `_OOR_GrassWindWave` = (speed m/s, wavelength m, reduced 0/1, wind time s).
* `_OOR_GrassGust` = (origin xyz world, strength 0..1). `_OOR_GrassGustWave` = (front radius m, front width m, sustained reach m, flutter).
* `_OOR_LaunchHaze` = haze multiplier. `_OOR_GrassLod` = (near m, band m, max m, far count fraction).

Gust envelope on the transport playhead: zero before `IgnitionSeconds` (9 s). It rises in 0.35 s. The
front runs out at about 46 m/s and eases to a 300 m radius. A sustained push (out to 230 m) and shiver
follow, and fade from 3.5 s after liftoff. The grade is at full weight through ignition and the pad and
climb shots, and fades over 15.3-17.3 s. A pause (`IsSuspended`) holds every value. Reduced effects come
from the departure's `ReducedMotion`, which composition already sets from `SetReducedEffects`. They give
half the gust with no shiver, no film grain, 55 % bloom, 55 % density and 65 % draw distance.

`LaunchGrassGustPresenter.PreviewAt(secondsAfterIgnition, reduced)` and
`PampasGrassField.PreviewWindTime(seconds)` scrub the look in Edit mode. The presenter's
`previewSecondsAfterIgnition` field does the same from the Inspector.

## Cost (measured)

| | |
|---|---|
| Instances | 510,158 clumps in 993 chunks of 24 m (1 m spacing, 430 m radius, clear areas removed) |
| GPU memory | 8.0 MB instance buffer (16 bytes each); two procedural meshes: 246 and 12 triangles |
| Generation | 0.6-0.9 s once per enable. Worker thread in Play mode, so the field appears when ready |
| Drawn per frame | 143-177 draw calls; 2,300-4,600 full clumps and 17,000-37,000 cards; 0.44-1.57 M triangles |
| Reduced effects | 47-96 draw calls; 0.14-0.86 M triangles |

Frame time, scratch URP project, RTX 3070, D3D12, 1600x900, fastest of 6 interleaved rounds (the
GPU was shared with two open editors, so medians are 1.5-2.5 ms higher):

| View | No grass | V04 | V04 reduced | V03 shells |
|---|---|---|---|---|
| Eye level, towards the sun | 1.96 ms | 5.16 ms | 4.50 ms | 4.91 ms |
| Eye level, towards the pad | 2.33 ms | 4.99 ms | 3.61 ms | 3.99 ms |
| 30 m above the field | 3.43 ms | 5.92 ms | 4.28 ms | 3.37 ms |
| Ground close-up | 3.10 ms | 7.18 ms | 5.57 ms | 6.38 ms |

So V04 adds 2.5-4.1 ms per frame here, and 0.9-2.5 ms with reduced effects. Not measured: Intel UHD
(where the shells measured 136 ms) and the real Bootstrap scene.

## Verification actually run

* Roslyn `csc` from Unity 6000.3.28f1 against Alex's Library references: `OutOfReach.LaunchGrassV04`,
  `OutOfReach.LaunchGrassV04.Editor`, and `OutOfReach.Composition.Editor` with the patch applied. All exit 0,
  0 errors, 0 warnings. `git apply --check` on the patch passes.
* Native Unity 6000.3.28f1 batch run in a **scratch URP project**, not the game project. It used the
  committed `LT_TerrainInner_Microrelief` mesh, `GroundFinishV02`, the far-land sectors and the V03 shells,
  the launch sun/fog/ambient values from `I06MarineBindings.ApplyMarineEnvironment`, grey stand-ins for the
  apron, vehicle and tower, a procedural sky, and a stub with the same public members the presenter reads.
  The real installer ran on that scene: both shaders compile with no messages, 3 + 1 passes, the Volume
  profile has its 7 overrides, the shell group was switched off, and the collider set was unchanged.
* `Evidence~/`: 19 renders and `evidence-report.txt` (per-shot draw statistics and the timing table).
  01-05 calm field; 06/07 the gust at 0.4, 1.2, 2.4 and 4.0 s after ignition; 08/09 reduced effects;
  10 without the launch grade; 11 wind waves at 0, 2 and 4 s.

**Not run:** the installer on the real `Bootstrap.unity`, Play mode, the real departure, the real camera
and facility geometry, and a player build. The facility-footprint clearing found 0 footprints in the
scratch scene, so it is untested on real geometry.
