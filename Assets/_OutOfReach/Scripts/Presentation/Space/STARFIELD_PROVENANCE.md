# Distant star background provenance

The committed six `Assets/_OutOfReach/Visuals/Space/Stars{Front,Back,Left,Right,Up,Down}.png` faces are 1024-pixel projections of the NASA GMAT star graphics data, not the former seed-74031 random-star artwork. All six file hashes match `docs/data/gmat-skybox-v01/manifest.json`; `DistantStars.mat` binds their preserved GUIDs. `DistantSpaceAuthoring.Build()` now validates these existing files and bindings; it does not regenerate procedural stars. Bootstrap's saved space skybox references `NasaDeepSky.mat` ([NASA SVS Deep Star Maps 2020](https://svs.gsfc.nasa.gov/4851/), Gaia DR2: ESA/Gaia/DPAC). The marine prologue temporarily applies its daylight sky before restoring the saved environment.

## Source, units and limits

Upstream: [NASA GMAT](https://github.com/nasa/GMAT), commit `b8aec552a8f34a9212063adc3f249bd2315397a3`, [`application/data/graphics/stars/inp_StarCatalog.txt`](https://raw.githubusercontent.com/nasa/GMAT/b8aec552a8f34a9212063adc3f249bd2315397a3/application/data/graphics/stars/inp_StarCatalog.txt). Local exact bytes: `docs/data/gmat-starfield-v01/gmat-stars.raw.txt`, SHA-256 `d6b66913176142dd12610fa5c2220f6c0f85e1ed2bd817230cf5619ab8f569f5`. Derived numerical subset: `gmat-stars.json`, SHA-256 `7077c2df92e8b5e53772ceef200dce3f6b9a5e0c3665ff83c4ea44d7bae5ce20`. Retrieval URLs, date and hashes are in that folder's `gmat-retrieval.json`; texture derivation and per-face hashes are in `docs/data/gmat-skybox-v01/`.

2,797 selected rows (visual magnitude <=5.5 plus the first Polaris marker) become 2,785 distinct coordinate pairs for the textures. RA/declination are degrees; converted directions are unitless, with +Z at RA 0 degrees, +X at RA 90 degrees, and +Y celestial north. Relative flux is `10^(-0.4*m)`; neutral color, 0.0009-radian point spread, exposure and tone response are artistic, not calibrated photometry or stellar diameters.

J2000 alignment is inferred from the upstream renderer. The raw star asset does **not** specify its underlying measurement catalogue or stellar observation epoch. Do not label this as Hipparcos, an epoch-of-date sky or independently verified precision astrometry. No HEASARC BSC data is included. Proper motion, stellar parallax and aberration are not modeled; no GMAT orbit solver or spacecraft trajectory was run for this background. The fictional ship's orientation is an authored view, not measured attitude telemetry.

## License and presentation

The inspected GMAT repository/application distribution supplies Apache License 2.0; the selected star path contained no separate asset-specific notice. This is the recorded licensing basis, not a claim about unrelated GMAT assets. Ship `Assets/StreamingAssets/ThirdParty/NASA-GMAT/LICENSE.txt` and `NOTICE.txt` with the derivative. The full source audit is `docs/data/gmat-starfield-v01/source-audit.json`.

`DistantStars.mat` retains the archived GMAT projection. The SVS runtime image and reuse credit are recorded in `Assets/_OutOfReach/Visuals/Environment/NasaDeepSkyV01/NOTICE.txt`.

Suggested credit: "Star directions from NASA GMAT star graphics data, Apache-2.0, converted for OUT OF REACH. Approximate source magnitudes; artistic exposure and point spread; stellar epoch unspecified." Attribution does not imply NASA endorsement.
