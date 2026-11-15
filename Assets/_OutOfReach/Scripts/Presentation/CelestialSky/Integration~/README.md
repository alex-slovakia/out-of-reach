# Celestial sky: integrator steps

The math and provenance are in `Assets/_OutOfReach/Visuals/Environment/NasaDeepSkyV01/CELESTIAL_FRAME.md`. This lane does not edit any scene, shared composition file or existing material.

1. **Scene binding (required).** Run `git lfs pull` if the 8K EXR is only a pointer. Then, in Unity 6000.3.28f1, open `Bootstrap.unity` in Edit mode and run **OUT OF REACH → Presentation → Celestial sky → Install in open Bootstrap scene**. It does three things:
   - verifies the 8K map hash and builds or updates `NasaDeepSky_Celestial.mat` (8K map, rotation, Sun and Earth day-0 values);
   - changes `RenderSettings.skybox` from `NasaDeepSky.mat` to the new material, and only if `NasaDeepSky.mat` is the current skybox;
   - adds one `CelestialSkyPresenter` bound to `RenderSettings.sun`.

   It marks the scene dirty but does **not** save it. Review the result and save it yourself.
2. **Clock binding (optional, recommended).** Run `git apply` on `I01MissionComposition-celestial-sky-clock.patch`. It adds one asmdef reference and three lines after `cinematics.Configure(...)`. Without it, the sky stays at day 0 (2026-11-14). With it, the Sun direction follows the day: about 0.46° per day, 14.3° by day 31.
3. **Earth-orbit sky (optional).** `I06MarineBindings.orbitalSky` still points to `NasaDeepSky.mat`, which uses yaw-only Panoramic. If that field is switched to `NasaDeepSky_Celestial.mat`, the departure scene also gets a Sun that matches the sky. That scene uses the Mars-proxy star field, a disclosed simplification.
4. **8K map and exposure (approved by Alex).** The celestial material uses NASA's `starmap_2020_8k.exr`. It is a 130.5 MB Git LFS object stored locally and not pushed; the installer checks its SHA-256. The material is at exposure 1.0, the native map values, which makes the Milky Way a faint visible band. It also draws the real-size Sun disc and the Earth point. `NasaDeepSky.mat` and the 4K map are unchanged. The sky credit must stop saying "Unmodified" for the displayed image; wording is in CELESTIAL_FRAME.md §6.
5. **Finale (Apoliak, R20).** Any finale shot that uses `RenderSettings.skybox` inherits the orientation. For markers, use `CelestialSkyPresenter.WorldDirectionFromRaDec(ra, dec)` and `WorldDirectionToEarth`. This lane did not edit any `RecoveryArrival`, `MarsDeparture` or rescue file.

Verify:
- `python Assets/_OutOfReach/Scripts/Presentation/CelestialSky/Tools~/celestial_sky_table.py` prints `CELESTIAL_SKY_TABLE_PASS`.
- `blender -b --factory-startup --python Assets/_OutOfReach/Scripts/Presentation/CelestialSky/Tools~/verify_sky_chain_blender.py -- <absolute path to starmap_2020_8k.exr>` prints `CELESTIAL_SKY_CHAIN_PASS`. This runs 9 bright stars and the sunlight through the exact shader lookup on the real EXR.
- EditMode tests are in `OutOfReach.Tests.CelestialSky`.
