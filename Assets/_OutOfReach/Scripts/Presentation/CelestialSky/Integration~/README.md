# Celestial sky: integrator steps

The math and provenance are in `Assets/_OutOfReach/Visuals/Environment/NasaDeepSkyV01/CELESTIAL_FRAME.md`. This lane does not edit any scene, shared composition file or existing material.

1. **Scene binding (required).** In Unity 6000.3.28f1, open `Bootstrap.unity` in Edit mode and run **OUT OF REACH → Presentation → Celestial sky → Install in open Bootstrap scene**. It does three things:
   - builds or updates `NasaDeepSky_Celestial.mat`, with the same texture, tint and exposure as `NasaDeepSky.mat`;
   - changes `RenderSettings.skybox` from `NasaDeepSky.mat` to the new material, and only if `NasaDeepSky.mat` is the current skybox;
   - adds one `CelestialSkyPresenter` bound to `RenderSettings.sun`.

   It marks the scene dirty but does **not** save it. Review the result and save it yourself.
2. **Clock binding (optional, recommended).** Run `git apply` on `I01MissionComposition-celestial-sky-clock.patch`. It adds one asmdef reference and three lines after `cinematics.Configure(...)`. Without it, the sky stays at day 0 (2026-11-14). With it, the Sun direction follows the day: about 0.46° per day, 14.3° by day 31.
3. **Earth-orbit sky (optional).** `I06MarineBindings.orbitalSky` still points to `NasaDeepSky.mat`, which uses yaw-only Panoramic. If that field is switched to `NasaDeepSky_Celestial.mat`, the departure scene also gets a Sun that matches the sky. That scene uses the Mars-proxy star field, a disclosed simplification.
4. **Exposure (decision for Alex or art).** At 0.15 the Milky Way is estimated at 0/255 on screen. **Apply proposed Milky Way exposure (0.60)** changes only the new material. If you adopt it, update the credit (see the doc corrections below).
5. **Finale (AppleX, R20).** Any finale shot that uses `RenderSettings.skybox` inherits the orientation. For markers, use `CelestialSkyPresenter.WorldDirectionFromRaDec(ra, dec)` and `WorldDirectionToEarth`. This lane did not edit any `RecoveryArrival`, `MarsDeparture` or rescue file.

Verify: `python Assets/_OutOfReach/Scripts/Presentation/CelestialSky/Tools~/celestial_sky_table.py` prints `CELESTIAL_SKY_TABLE_PASS`. EditMode tests are in `OutOfReach.Tests.CelestialSky`.
