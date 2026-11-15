# Sun, Earth and Mars reference — 15 November 2026

This is a **fixed, dated NASA reference**, separate from the game's authority and fictional spacecraft. Body centres are shown at **2026-11-14 08:00 UTC**, the existing reference epoch. Paths contain701 inclusive daily samples through **2028-10-14 08:00 UTC** (700days), enough to show approximately one Mars revolution and almost two Earth revolutions. Daily samples form straight segments, not exact continuous curves or decorative ellipses.

## Sources and reproduction

- NASA/JPL [Horizons API documentation](https://ssd-api.jpl.nasa.gov/doc/horizons.html).
- NASA/JPL [Planetary Physical Parameters](https://ssd.jpl.nasa.gov/planets/phys_par.html): volumetric mean radius Earth6371.0084km and Mars3389.50km.
- NASA Goddard [Sun Fact Sheet](https://nssdc.gsfc.nasa.gov/planetary/factsheet/sunfact.html): mean radius695700km.

`manifest.json` records the complete encoded Horizons request URLs and parameters, retrieval UTC timestamps, API signatures, raw-response SHA-256 hashes, source-page hashes and normalized runtime hash. `earth.raw.json` and `mars.raw.json` retain the complete official responses, including metadata and time/ephemeris caveats. The physical-radius HTML pages are preserved for the cited values. These are computed reference ephemerides, not live observations.

Run `python tools/nasa/reference_orbits.py` from the project root for **offline verification**. `--fetch` deliberately refreshes the two source responses, physical-page snapshots and runtime data; it is not part of routine validation. Only Python standard library is used. The mission's existing distance dataset is untouched.

The offline verifier checks every source hash, all701 epochs/spacings, target/origin/frame metadata, finite vectors, source range and light-time consistency, fixed radii, Sun origin, exact runtime JSON regeneration and epoch Earth–Mars3D agreement with the separately committed geocentric snapshot within0.01km.

## Geometry and display

Horizons targets399/499 are Earth/Mars **body centres**, with Sun centre10 as origin (`CENTER=500@10`). UnitsKM-S, reference ICRF with mean ecliptic of J2000 plane, geometric vectors (`VEC_CORR=NONE`), UT timestamps. Sun's heliocentric coordinates are exactly0,0,0 by definition.

`ReferenceOrbitView` draws an orthographic **XY projection** of those3D vectors. It retains Z in data and separately displays the actual3D Earth–Mars range and smaller projected range. It does not claim that the projected separation equals3D distance. Planet tilts, oblateness, terrain, atmospheres and real surface appearance are not represented; discs are the orthographic silhouettes of **mean-radius spheres**.

All body radii and projected positions use **the same pixels-per-kilometre value**. Planet sizes are never inflated to a minimum visible radius. At the complete-orbit view Earth/Mars are subpixel, as their physical dimensions require. Optional crosshairs are explicitly labeled **locator crosses, not physical size**. Focus buttons centre and zoom to each body's physical disc; dragging/wheel/keyboard arrows adjust the same coordinate transform. Reset returns to the full-reference scale. Body focus changes only the view, never the data.

Segment clipping is performed in double precision before creating float drawing vertices. Sphere silhouettes are intersected with the viewport before drawing; extremely close zooms do not send huge-radius arcs to UI Toolkit. A straight-line daily trace remains only a sampled approximation when zoomed close to a planet.

## Numeric checkpoint

`verification.json` records the actual offline result:

| Quantity at reference epoch | Value |
|---|---:|
| Earth–Sun3D centre distance |148,021,859.03745073km|
| Mars–Sun3D centre distance |240,578,403.04619855km|
| Earth–Mars3D centre distance |197,707,975.99085307km|
| Earth–Mars projected XY distance |197,600,084.84137115km|
| Sun/Earth mean-radius ratio |109.19778413728038|
| Earth/Mars mean-radius ratio |1.8796307419973446|

Runtime JSON SHA-256: `f3f3e5bdff720a4b277b933f0b31d675d8f49a7429e1a42f12feb5704aecaffc`.

## Integration and evidence boundary

Public constructor: `new OutOfReach.Presentation.UI.Report.ReferenceOrbitView()`.

It loads committed Resources/OorReferenceOrbits.json and Resources/OorReferenceOrbitStyle.uss; **no runtime networking**. Put it in the Sources scroll content with automatic height. Its drawing surface is220px high plus controls and explanatory text; a fixed240px outer container would clip it. Expanded scale/range/trace assumptions are in the initially closed Scale & data details foldout. Stable names: `reference-orbit-view`, `reference-orbits-canvas`, `orbit-reset`, `orbit-zoom-in`, `orbit-zoom-out`, `orbit-focus-sun`, `orbit-focus-earth`, `orbit-focus-mars`, `orbit-locators`, `reference-orbits-scale`.

Only new view/data/style/tool/document files were authored by this worker. Root owns the existing report binding. Offline numeric verification and `git diff --check` passed. **Unity compile/native rendering is not claimed by this worker**; root must verify the integrated revision, focus/zoom/pan/reset, and layout. No scene or gameplay state is changed.
