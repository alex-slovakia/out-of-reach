# Celestial frame of the NASA deep sky (R20 science support)

This note covers how the sky is oriented. Image provenance stays in `docs/art-direction/nasa-deep-sky-20261115/README.md`.

## What is placed, and what is not

| Item | Type | Source |
|---|---|---|
| Star and Milky Way positions | NASA reference data | NASA SVS Deep Star Maps 2020, `starmap_2020_4k.exr`, https://svs.gsfc.nasa.gov/4851/ (Gaia DR2: ESA/Gaia/DPAC). ICRF/J2000 right ascension and declination, equirectangular. |
| Sun and Earth directions | NASA/JPL ephemeris | Horizons archive `docs/data/reference-orbits/normalized.json` (SHA-256 `f3f3e5bd…ecaffc`). Daily from 2026-11-14 08:00 UTC to 2026-12-15 08:00 UTC, geometric (`VEC_CORR=NONE`), ICRF / mean ecliptic of J2000, km. Cross-checked against `docs/data/horizons/earth-mars-2026-11-14--2026-12-15.csv` (SHA-256 `694f3606…79e5af`). |
| Observer | simplified simulation | Mars body centre. This is the same Mars-vicinity proxy that the communications model uses. AURORA-7's real position is fictional. At Mars, moving 10⁶ km shifts the Sun's direction by less than 0.25°. |
| Ship attitude | fictional, rule-based | The rule (below): the real Sun is placed where the authored scene light comes from, and ecliptic north is kept as close to cabin "up" as possible. No measured attitude exists. |
| Sun disc, planets in the sky | not drawn | The texture has no Sun, Earth or Mars in it. `CelestialSkyPresenter.WorldDirectionToEarth` gives the true direction for anyone who places a distant Earth point. |

These corrections are left out: stellar proper motion from the Gaia epoch to 2026, annual and planetary aberration (≤ 20″ at Mars), light-time, and parallax of the stars. All of them are far below one texel, which is 0.088° on the 4096-px map.

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

## 5. Runtime behaviour

- `OutOfReach/Skybox/Celestial Panoramic` uses the same sampling, tint, [Gamma] exposure and HDR decode as `Skybox/Panoramic`, with a full quaternion in place of yaw only. It also adds seam-free mip selection at RA 12h. `NasaDeepSky_Celestial.mat` carries q(day 0) as a static fallback. Its texture, tint and 0.15 exposure are the same as `NasaDeepSky.mat`.
- `CelestialSkyPresenter` publishes the global `_OorCelestialSkyRotation`. It recomputes q whenever the Sun light rotates (for example, the Earth-orbit lighting change in `I06MarineBindings`) or the day changes. The day index is `floor(ElapsedSimulationSeconds / 86400)` from the read-only `ISimulationClock`, clamped to 0–31, using the last sample at or before that time. This is the same rule as the communications dataset; there is no interpolation, so the step is about 0.46° per day. If no clock is bound, it uses day 0. The presenter writes no state.
- In the Earth-orbit departure scene the observer should strictly be Earth. The presenter still matches the Sun to the light there, but keeps the Mars-proxy star field. The departure epoch is unspecified, so treat this as a disclosed cinematic simplification.

## 6. Milky Way visibility (estimate, not a render)

The project is in linear colour space, so the [Gamma] exposure value 0.15 becomes a linear multiplier of 0.0196. Tint × `unity_ColorSpaceDouble` = 0.984 and CinematicGrade adds +0.45 EV (×1.366). ACES is approximated with the Hill RRT+ODT fit; the 2 % contrast, −5 saturation and vignette are ignored. Source luminance is area-weighted from the EXR.

| Exposure slider | Linear | MW band (\|b\| < 5°) median | MW band p90 | Dark sky (\|b\| > 40°) | Saturated star |
|---|---|---|---|---|---|
| 0.15 (current) | 0.020 | 0 | 0 | 0 | 16 |
| 0.45 (DeepView) | 0.171 | 3 | 13 | 0 | 108 |
| **0.60 (proposed)** | 0.319 | 8 | 27 | 0 | 153 |
| 0.70 | 0.448 | 13 | 37 | 0 | 177 |

(8-bit sRGB display values.)

**At the current exposure the Milky Way is not visible, and even the brightest stars reach only about 16/255.** This fits the "space has no stars" report in #11. The proposed setting is the menu item *Celestial sky → Apply proposed Milky Way exposure (0.60)*. It changes only the new material. The dark sky stays black, and the brightest stars stay below the 1.25 bloom threshold in HDR (≈ 0.43). Nothing animates, flickers or adapts, so reduced effects need no special case.

If 0.60 is adopted, the credit text must say the image is **exposure-adjusted**. "Unmodified" may then describe only the source file. These are estimates: confirm with a native Unity 6000.3.28f1 screenshot before stating visibility as fact.
