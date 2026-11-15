# Celestial frame of the NASA deep sky (R20 science support)

This note covers how the sky is oriented, the 8K star map, and the real-size Sun and Earth. The 4K image's provenance stays in `docs/art-direction/nasa-deep-sky-20261115/README.md`.

## What is placed, and what is not

| Item | Type | Source |
|---|---|---|
| Star and Milky Way positions, brightness, colour | NASA reference data | NASA SVS Deep Star Maps 2020, https://svs.gsfc.nasa.gov/4851/: 1.7 billion stars from Hipparcos-2, Tycho-2 and Gaia DR2 (ESA/Gaia/DPAC). ICRF/J2000 right ascension and declination, equirectangular. The celestial material uses **`starmap_2020_8k.exr`** (8192×4096, 0.044° per texel), source https://svs.gsfc.nasa.gov/vis/a000000/a004800/a004851/starmap_2020_8k.exr, retrieved 2026-11-15T20:09:46Z–20:11:19Z, 130,530,278 bytes, SHA-256 `dc6c4f413e85707a29a25a9451148154554ecca2c996f84fa8f47b65ef9ff7c4`. Source pixels are unmodified. |
| Sun and Earth directions | NASA/JPL ephemeris | Horizons archive `docs/data/reference-orbits/normalized.json` (SHA-256 `f3f3e5bd…ecaffc`). Daily from 2026-11-14 08:00 UTC to 2026-12-15 08:00 UTC, geometric (`VEC_CORR=NONE`), ICRF / mean ecliptic of J2000, km. Cross-checked against `docs/data/horizons/earth-mars-2026-11-14--2026-12-15.csv` (SHA-256 `694f3606…79e5af`). |
| Observer | simplified simulation | Mars body centre. This is the same Mars-vicinity proxy that the communications model uses. AURORA-7's real position is fictional. At Mars, moving 10⁶ km shifts the Sun's direction by less than 0.25°. |
| Ship attitude | fictional, rule-based | The rule (below): the real Sun is placed where the authored scene light comes from, and ecliptic north is kept as close to cabin "up" as possible. No measured attitude exists. |
| Sun disc | NASA data, true size | Angular radius asin(695,700 km / Sun–observer distance). The radius is from the NASA Sun fact sheet in the same archive. It is 0.331° across on day 0 (62 % of the Sun's size seen from Earth) and 0.326° on day 30. Limb darkening uses the linear law with u = 0.6, an approximate visible-band value. Brightness is an HDR art value that saturates to white, like any eye or camera. |
| Earth | NASA data, true direction and size, estimated brightness | Earth subtends 13.3″ on day 0 and 16.6″ on day 30, well under one pixel, so it is drawn as a point that conserves flux. Its V magnitude uses NASA's Earth V(1,0) = −3.99 (Earth fact sheet), the Horizons distances and a Lambert-sphere phase law: −2.25 on day 0 (phase angle 86.9°, 53 % lit) and −2.11 on day 30. That is about 2× Sirius (V −1.46). Flux is calibrated against Sirius in the same 8K map. The pale blue-white tint is an art choice. |
| Sun glare | cinematic shortcut | A soft optical halo, falling as (r/θ)^2.5 outside the disc, with a peak HDR value of 4. It is an eye or lens effect, not sky radiance, and reduced effects removes it. |
| Departure in Earth orbit | simplified, reference epoch | `NasaDeepSky_Celestial_EarthOrbit.mat` uses Earth as the observer on day 0, because the departure date is not defined. The Sun is at RA 229.165°, Dec −18.158°, 0.5386° across. It uses the orbit light Euler(26, 220, 0) from `I06MarineBindings`, a static rotation q = (−0.141957, 0.918569, −0.368556, −0.015665), and no Earth point. |
| Moon, Mars, other planets | not drawn | The Moon would be about 0.11° from Earth; it needs one more Horizons query. Mars' size depends on the ship's undefined distance to Mars. |

These corrections are left out: stellar proper motion from the Gaia epoch to 2026, annual and planetary aberration (≤ 20″ at Mars), light-time, and parallax of the stars. All of them are far below one texel, which is 0.044° on the 8192-px map.

## 1. Map frame (verified on the pixels)

SVS `starmap_*` files are equatorial; the separate `milkyway_*` files are galactic. Pixel u is 0..1 left to right, and v is 0..1 from bottom to top:

    u = 0.5 − RA/360°   (RA 0h at the centre, increasing to the left as seen from inside)
    v = 0.5 + Dec/180°  (north at the top)

Check: Blender 3.6.1 read the committed EXR. Ten bright stars landed within ≤ 3 px of the positions this formula predicts: Sirius, Canopus, Arcturus, Vega, Rigel, Betelgeuse, Polaris, α Cen, Capella and Procyon. For example, Sirius is predicted at (895.6, 1214.2) and the saturated peak is at (894, 1213). Brightness along galactic latitude b = 0 has a median of 0.035, against 0.0008 at the galactic poles. The Milky Way band is therefore in the texture, about 40× brighter than the polar sky.

## 2. Sky lookup frame

Unity's `Skybox/Panoramic` samples with `u = 0.5 − atan2(d.z, d.x)/2π` and `v = 1 − acos(d.y)/π`. Matching this with §1 gives the **sky frame**:

    +X = RA 0h, Dec 0     +Z = RA 6h, Dec 0     +Y = celestial north (Dec +90°)
    sky(x_eq, y_eq, z_eq) = (x_eq, z_eq, y_eq)

ICRF is right-handed, and Unity's display is left-handed with Y up. Swapping y and z is the standard non-mirroring conversion between the two. Facing RA 0h with north up, RA 6h (east) falls on the viewer's left, which matches the real sky.

## 3. Ephemeris directions

Horizons heliocentric vectors are in the ecliptic frame. They are rotated to ICRF equatorial about +x by the J2000 obliquity ε = 84381.448″ = 23.4392911°:

    x_eq = x_ec
    y_eq = y_ec·cos ε − z_ec·sin ε
    z_eq = y_ec·sin ε + z_ec·cos ε

    ŝ(day) = −r_Mars / |r_Mars|                       (Sun from the observer)
    ê(day) = (r_Earth − r_Mars) / |r_Earth − r_Mars|  (Earth from the observer)
    n̂_ecl  = sky(0, −sin ε, cos ε)                     (ecliptic north pole)

**Frame cross-check.** Rotating the heliocentric (r_Mars − r_Earth) by ε reproduces the independent geocentric ICRF Horizons vectors. The largest difference over all 32 days is 0.000000 km, so the frame and ε are right.

| Day (index) | UTC | Sun from Mars RA, Dec | Earth from Mars RA, Dec | Sun–Earth elongation |
|---|---|---|---|---|
| 0 | 2026-11-14 08:00 | 288.340°, −23.935° | 327.592°, −15.091° | 37.91° |
| 13 | 2026-11-27 08:00 | 294.875°, −23.145° | 333.042°, −13.488° | 37.38° |
| 30 | 2026-12-14 08:00 | 303.147°, −21.741° | 338.724°, −11.843° | 35.37° |

Seen from Mars, the Sun is in Sagittarius, about 20° from the galactic centre. So the brightest part of the Milky Way sits on the sunlit side of the sky, and Orion and Sirius are on the opposite side. The Sun's ecliptic latitude is −1.55°, which reflects Mars' 1.85° orbital inclination. Over days 0→31 the Sun drifts 14.27° against the stars.

## 4. Attitude rule (TRIAD)

Inputs are the light's forward vector f_w and world up Ŷ. World vectors are a₁ = −f_w (towards the Sun) and a₂ = Ŷ. Sky vectors are b₁ = ŝ(day) and b₂ = n̂_ecl. The rotation is

    q = LookRotation(b₁, b₂) · LookRotation(a₁, a₂)⁻¹

Then q·a₁ = b₁ exactly. Rotations preserve angles, so q·Ŷ lies in the plane of b₁ and b₂, on the b₂ side, as close to ecliptic north as the light allows. The skybox shader samples the map at d_sky = q·d_world.

Bootstrap's "Sunlight – authored direction" is Euler(34°, −55°, 0). This gives

    −f_w = (+0.679108, +0.559193, −0.475516)   (elevation 34.0°, heading +125.0°)
    q(day 0) = (x −0.425012484, y +0.269377633, z −0.238544697, w +0.830600088)

The Sun is 34° above the world horizontal plane, but it really lies on the ecliptic. So the fictional ship is tilted 35.55° from the ecliptic. Its "up" points as near to ecliptic north as that allows.

Resulting world directions at day 0 (heading = atan2(x, z), like Unity yaw):

| Object | World direction | Elevation | Heading |
|---|---|---|---|
| Sun | (+0.679, +0.559, −0.476) | +34.0° | +125.0° |
| Earth (point, not drawn) | (+0.894, +0.432, +0.124) | +25.6° | +82.1° |
| Galactic centre | (+0.476, +0.466, −0.746) | +27.8° | +147.5° |
| North galactic pole | (−0.880, +0.260, −0.399) | +15.0° | −114.4° |
| Polaris | (−0.620, +0.520, +0.588) | +31.3° | −46.5° |
| Vega | (−0.111, +0.992, +0.063) | +82.7° | −60.2° |
| Betelgeuse | (−0.308, −0.756, +0.577) | −49.1° | −28.1° |
| Sirius | (−0.189, −0.966, +0.176) | −75.1° | −47.1° |

All values come from `Scripts/Presentation/CelestialSky/Tools~/celestial_sky_table.py`, which uses only the Python standard library and runs offline. It checks the input hashes and the frame cross-check, then regenerates `CelestialEphemeris2026.g.cs` byte for byte.

**End-to-end check on the pixels.** `Tools~/verify_sky_chain_blender.py` runs in Blender. For 9 bright stars (Sirius, Canopus, Arcturus, Vega, Rigel, Betelgeuse, Polaris, Capella, Procyon), it takes the world direction above and sends it through the exact shader lookup with the material's rotation:
- all 9 land on a saturated star pixel;
- the old unrotated material, used as a control, misses all 9;
- the default sunlight direction samples RA 288.33°, Dec −23.95°, which is 0.02° from the Horizons Sun.

## 5. Runtime behaviour

- `OutOfReach/Skybox/Celestial Panoramic` uses the same sampling, tint, [Gamma] exposure and HDR decode as `Skybox/Panoramic`, with a full quaternion in place of yaw only. It also adds seam-free mip selection at RA 12h, the Sun disc, and the Earth point. The Earth point is one pixel in radius, with radiance = flux / solid angle, so its brightness relative to the stars holds at any resolution or field of view.
- `NasaDeepSky_Celestial.mat` uses the 8K map at exposure **1.0**, which is linear 1.0: the NASA pixel values unscaled before the game's camera grade. It carries the day-0 rotation, Sun and Earth as static fallbacks. `NasaDeepSky.mat` and the 4K map are untouched.
- `CelestialSkyPresenter` publishes the global `_OorCelestialSkyRotation`. It recomputes q whenever the Sun light rotates (for example, the Earth-orbit lighting change in `I06MarineBindings`) or the day changes. The day index is `floor(ElapsedSimulationSeconds / 86400)` from the read-only `ISimulationClock`, clamped to 0–31, using the last sample at or before that time. This assumes mission T0 is the first Horizons epoch, 2026-11-14 08:00 UTC, which is what `MissionDirector` uses because `AuroraMission` sets no other `MissionEpochAtStartUtc`. This is the same rule as the communications dataset; there is no interpolation, so the step is about 0.46° per day. If no clock is bound, it uses day 0. The presenter writes no state.
- In the Earth-orbit departure scene the observer should strictly be Earth. The presenter still matches the Sun to the light there, but keeps the Mars-proxy star field. The departure epoch is unspecified, so treat this as a disclosed cinematic simplification.

## 6. Milky Way visibility (estimate, not a render)

The project is in linear colour space, so a [Gamma] exposure value e becomes the linear multiplier ((e + 0.055)/1.055)^2.4. That is 0.0196 at the old 0.15 and exactly 1.0 at 1.0. Tint × `unity_ColorSpaceDouble` = 0.984, and CinematicGrade adds +0.45 EV (×1.366). ACES is approximated with the Hill RRT+ODT fit; the 2 % contrast, −5 saturation and vignette are ignored. Source luminance is area-weighted from the EXRs.

The 8K map stores the diffuse Milky Way at about 1/3.9 of the 4K per-texel value: band median 0.00957 against 0.0374. So it needs more exposure than the 4K map did.

| Map, exposure | Linear | MW band (\|b\| < 5°) median | MW band p90 | Dark sky (\|b\| > 40°) | Saturated star | Star HDR |
|---|---|---|---|---|---|---|
| 4K, 0.15 (old) | 0.020 | 0 | 0 | 0 | 16 | 0.03 |
| 8K, 0.60 | 0.319 | 0 | 4 | 0 | 153 | 0.43 |
| **8K, 1.00 (committed)** | 1.000 | 5 | 22 | 0 | 219 | 1.34 |
| 8K, 1.20 | 1.517 | 10 | 34 | 0 | 232 | 2.04 |

(8-bit sRGB display values. Star HDR is compared against the bloom threshold of 1.25.)

**At 1.0 the Milky Way is a faint visible band.** The dark sky stays black. Stars are crisp points: one 8K texel is 0.044°, against about 0.063° per pixel at 1080p with the 68° field of view. Only the cores of the brightest stars just cross the bloom threshold. Nothing animates, flickers or adapts, so reduced effects need no special case.

**Galaxy lift (committed: `_GalaxyBoost` B = 2, `_GalaxyKnee` k = 0.05).** This is a static tone curve on source luminance L:

    f(L) = L · (1 + (B − 1) / (1 + (L/k)²))
    f'(L) = 1 + (B − 1)(1 − x²)/(1 + x²)²,  x = L/k   →   minimum 1 − (B − 1)/8 = 0.875 at x = √3

Faint diffuse light is about doubled. The lift is halved at L = 0.05 and vanishes for star cores (L = 1 → ×1.0025). The curve is strictly increasing, so it never reorders brightness. An earlier smoothstep version had a near-plateau, with slope 0.017 at L ≈ 0.155, that would have merged medium-bright stars; this curve replaces it. Estimated result at exposure 1.0:

| Region | Before | After |
|---|---|---|
| Milky Way band median | 5/255 | 15/255 |
| Milky Way band p90 | 22/255 | 39/255 |
| Dark sky | 0 | 0 |
| Saturated stars | 219/255 | 219/255 |

This is what "exposure-adjusted" in the credit refers to.

This is a dark-adapted view, not what an eye sees next to a sunlit hull, where the Milky Way would wash out. The credit must say **exposure-adjusted**. The patch `Scripts/Presentation/CelestialSky/Integration~/sky-credit-exposure-adjusted.patch` makes that change in `FlightRecorderPresenter.cs` (UI lane) and `NOTICE.txt`. ## 7. Unity verification (15 Nov 2026, 21:50 CET)

Unity 6000.3.28f1 ran in batch mode on Direct3D 12, on an isolated scratch project, never the game checkout. The project had:
- the repository's ProjectSettings, linear colour space;
- URP 17.3.0 with `PC_RPAsset`;
- a global Volume with the game's `CinematicGrade` (+0.45 EV, contrast, ACES, bloom, vignette);
- these exact CelestialSky files, both materials and the 8K map.

Results, from run 3:
- `ShaderUtil.ShaderHasError` = false and `isSupported` = true.
- The installer's `EnsureMaterial` verified the 8K SHA-256 and built both materials. The map imported as 8192×4096 BC6H with 14 mips.
- Five 1920×1080 renders at a 68° field of view: flight toward the Sun and Earth, flight anti-Sun, the static fallback without a presenter, departure toward the Sun, and departure away from it.
- The presenter published exactly q(day 0) = (−0.43, 0.27, −0.24, 0.83). The no-presenter fallback rendered identically: mean 9.14/255, 319 pixels above 200.
- In the flight view, the Earth point is at (691, 602) against a predicted (692, 604). The Sun's peak is at (1225, 473) against a predicted (1227, 477).
- The Milky Way core and dust lanes are clearly visible. Toward the anti-Sun, Orion, the Hyades and the Pleiades are recognisable, with the faint anticentre band.

Not verified: the real Bootstrap scene with the ship hull, the composition patch inside the full project, a standalone build, and reduced-effects behaviour at runtime. The global path (`ConfigureReducedEffects`) compiles but was not rendered.
